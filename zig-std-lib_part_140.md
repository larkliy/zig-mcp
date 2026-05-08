```
                    , .{}) catch {};
                    @panic("misconfigured build script");
                },
            } },
        };
    }

    pub fn path(lazy_path: LazyPath, b: *Build, sub_path: []const u8) LazyPath {
        return lazy_path.join(b.allocator, sub_path) catch @panic("OOM");
    }

    pub fn join(lazy_path: LazyPath, arena: Allocator, sub_path: []const u8) Allocator.Error!LazyPath {
        return switch (lazy_path) {
            .src_path => |src| .{ .src_path = .{
                .owner = src.owner,
                .sub_path = try fs.path.resolve(arena, &.{ src.sub_path, sub_path }),
            } },
            .generated => |gen| .{ .generated = .{
                .file = gen.file,
                .up = gen.up,
                .sub_path = try fs.path.resolve(arena, &.{ gen.sub_path, sub_path }),
            } },
            .cwd_relative => |cwd_relative| .{
                .cwd_relative = try fs.path.resolve(arena, &.{ cwd_relative, sub_path }),
            },
            .dependency => |dep| .{ .dependency = .{
                .dependency = dep.dependency,
                .sub_path = try fs.path.resolve(arena, &.{ dep.sub_path, sub_path }),
            } },
        };
    }

    /// Returns a string that can be shown to represent the file source.
    /// Either returns the path, `"generated"`, or `"dependency"`.
    pub fn getDisplayName(lazy_path: LazyPath) []const u8 {
        return switch (lazy_path) {
            .src_path => |sp| sp.sub_path,
            .cwd_relative => |p| p,
            .generated => "generated",
            .dependency => "dependency",
        };
    }

    /// Adds dependencies this file source implies to the given step.
    pub fn addStepDependencies(lazy_path: LazyPath, other_step: *Step) void {
        switch (lazy_path) {
            .src_path, .cwd_relative, .dependency => {},
            .generated => |gen| other_step.dependOn(gen.file.step),
        }
    }

    /// Deprecated, see `getPath4`.
    pub fn getPath(lazy_path: LazyPath, src_builder: *Build) []const u8 {
        return getPath2(lazy_path, src_builder, null);
    }

    /// Deprecated, see `getPath4`.
    pub fn getPath2(lazy_path: LazyPath, src_builder: *Build, asking_step: ?*Step) []const u8 {
        const p = getPath3(lazy_path, src_builder, asking_step);
        return src_builder.pathResolve(&.{ p.root_dir.path orelse ".", p.sub_path });
    }

    /// Deprecated, see `getPath4`.
    pub fn getPath3(lazy_path: LazyPath, src_builder: *Build, asking_step: ?*Step) Cache.Path {
        return getPath4(lazy_path, src_builder, asking_step) catch |err| switch (err) {
            error.Canceled => std.process.exit(1),
        };
    }

    /// Intended to be used during the make phase only.
    ///
    /// `asking_step` is only used for debugging purposes; it's the step being
    /// run that is asking for the path.
    pub fn getPath4(lazy_path: LazyPath, src_builder: *Build, asking_step: ?*Step) Io.Cancelable!Cache.Path {
        switch (lazy_path) {
            .src_path => |sp| return .{
                .root_dir = sp.owner.build_root,
                .sub_path = sp.sub_path,
            },
            .cwd_relative => |sub_path| return .{
                .root_dir = Cache.Directory.cwd(),
                .sub_path = sub_path,
            },
            .generated => |gen| {
                // TODO make gen.file.path not be absolute and use that as the
                // basis for not traversing up too many directories.

                const graph = src_builder.graph;

                var file_path: Cache.Path = .{
                    .root_dir = Cache.Directory.cwd(),
                    .sub_path = gen.file.path orelse {
                        const io = graph.io;
                        const stderr = try io.lockStderr(&.{}, graph.stderr_mode);
                        dumpBadGetPathHelp(gen.file.step, stderr.terminal(), src_builder, asking_step) catch {};
                        io.unlockStderr();
                        @panic("misconfigured build script");
                    },
                };

                if (gen.up > 0) {
                    const cache_root_path = src_builder.cache_root.path orelse
                        (src_builder.cache_root.join(src_builder.allocator, &.{"."}) catch @panic("OOM"));

                    for (0..gen.up) |_| {
                        if (mem.eql(u8, file_path.sub_path, cache_root_path)) {
                            // If we hit the cache root and there's still more to go,
                            // the script attempted to go too far.
                            dumpBadDirnameHelp(gen.file.step, asking_step,
                                \\dirname() attempted to traverse outside the cache root.
                                \\This is not allowed.
                                \\
                            , .{}) catch {};
                            @panic("misconfigured build script");
                        }

                        // path is absolute.
                        // dirname will return null only if we're at root.
                        // Typically, we'll stop well before that at the cache root.
                        file_path.sub_path = fs.path.dirname(file_path.sub_path) orelse {
                            dumpBadDirnameHelp(gen.file.step, asking_step,
                                \\dirname() reached root.
                                \\No more directories left to go up.
                                \\
                            , .{}) catch {};
                            @panic("misconfigured build script");
                        };
                    }
                }

                return file_path.join(src_builder.allocator, gen.sub_path) catch @panic("OOM");
            },
            .dependency => |dep| return .{
                .root_dir = dep.dependency.builder.build_root,
                .sub_path = dep.sub_path,
            },
        }
    }

    pub fn basename(lazy_path: LazyPath, src_builder: *Build, asking_step: ?*Step) []const u8 {
        return fs.path.basename(switch (lazy_path) {
            .src_path => |sp| sp.sub_path,
            .cwd_relative => |sub_path| sub_path,
            .generated => |gen| if (gen.sub_path.len > 0)
                gen.sub_path
            else
                gen.file.getPath2(src_builder, asking_step),
            .dependency => |dep| dep.sub_path,
        });
    }

    /// Copies the internal strings.
    ///
    /// The `b` parameter is only used for its allocator. All *Build instances
    /// share the same allocator.
    pub fn dupe(lazy_path: LazyPath, b: *Build) LazyPath {
        return lazy_path.dupeInner(b.allocator);
    }

    fn dupeInner(lazy_path: LazyPath, allocator: std.mem.Allocator) LazyPath {
        return switch (lazy_path) {
            .src_path => |sp| .{ .src_path = .{
                .owner = sp.owner,
                .sub_path = sp.owner.dupePath(sp.sub_path),
            } },
            .cwd_relative => |p| .{ .cwd_relative = dupePathInner(allocator, p) },
            .generated => |gen| .{ .generated = .{
                .file = gen.file,
                .up = gen.up,
                .sub_path = dupePathInner(allocator, gen.sub_path),
            } },
            .dependency => |dep| .{ .dependency = .{
                .dependency = dep.dependency,
                .sub_path = dupePathInner(allocator, dep.sub_path),
            } },
        };
    }
};

fn dumpBadDirnameHelp(
    fail_step: ?*Step,
    asking_step: ?*Step,
    comptime msg: []const u8,
    args: anytype,
) anyerror!void {
    const stderr = std.debug.lockStderr(&.{}).terminal();
    defer std.debug.unlockStderr();
    const w = stderr.writer;

    try w.print(msg, args);

    if (fail_step) |s| {
        stderr.setColor(.red) catch {};
        try w.writeAll("    The step was created by this stack trace:\n");
        stderr.setColor(.reset) catch {};

        s.dump(stderr);
    }

    if (asking_step) |as| {
        stderr.setColor(.red) catch {};
        try w.print("    The step '{s}' that is missing a dependency on the above step was created by this stack trace:\n", .{as.name});
        stderr.setColor(.reset) catch {};

        as.dump(stderr);
    }

    stderr.setColor(.red) catch {};
    try w.writeAll("    Proceeding to panic.\n");
    stderr.setColor(.reset) catch {};
}

/// In this function the stderr mutex has already been locked.
pub fn dumpBadGetPathHelp(s: *Step, t: Io.Terminal, src_builder: *Build, asking_step: ?*Step) anyerror!void {
    const w = t.writer;
    try w.print(
        \\getPath() was called on a GeneratedFile that wasn't built yet.
        \\  source package path: {s}
        \\  Is there a missing Step dependency on step '{s}'?
        \\
    , .{
        src_builder.build_root.path orelse ".",
        s.name,
    });

    t.setColor(.red) catch {};
    try w.writeAll("    The step was created by this stack trace:\n");
    t.setColor(.reset) catch {};

    s.dump(t);
    if (asking_step) |as| {
        t.setColor(.red) catch {};
        try w.print("    The step '{s}' that is missing a dependency on the above step was created by this stack trace:\n", .{as.name});
        t.setColor(.reset) catch {};

        as.dump(t);
    }
    t.setColor(.red) catch {};
    try w.writeAll("    Proceeding to panic.\n");
    t.setColor(.reset) catch {};
}

pub const InstallDir = union(enum) {
    prefix: void,
    lib: void,
    bin: void,
    header: void,
    /// A path relative to the prefix
    custom: []const u8,

    /// Duplicates the install directory including the path if set to custom.
    pub fn dupe(dir: InstallDir, builder: *Build) InstallDir {
        if (dir == .custom) {
            return .{ .custom = builder.dupe(dir.custom) };
        } else {
            return dir;
        }
    }
};

/// Creates a path leading to a directory inside "tmp" subdirectory of
/// `cache_root` which is created on demand and cleaned up by the build runner
/// upon success.
pub fn tmpPath(b: *Build) LazyPath {
    const wf = b.addTempFiles();
    return wf.getDirectory();
}

/// A pair of target query and fully resolved target.
/// This type is generally required by build system API that need to be given a
/// target. The query is kept because the Zig toolchain needs to know which parts
/// of the target are "native". This can apply to the CPU, the OS, or even the ABI.
pub const ResolvedTarget = struct {
    query: Target.Query,
    result: Target,
};

/// Converts a target query into a fully resolved target that can be passed to
/// various parts of the API.
pub fn resolveTargetQuery(b: *Build, query: Target.Query) ResolvedTarget {
    if (query.isNative()) {
        // Hot path. This is faster than querying the native CPU and OS again.
        return b.graph.host;
    }
    const io = b.graph.io;
    return .{
        .query = query,
        .result = std.zig.system.resolveTargetQuery(io, query) catch
            @panic("unable to resolve target query"),
    };
}

pub fn wantSharedLibSymLinks(target: Target) bool {
    return target.os.tag != .windows;
}

pub const SystemIntegrationOptionConfig = struct {
    /// If left as null, then the default will depend on system_package_mode.
    default: ?bool = null,
};

pub fn systemIntegrationOption(
    b: *Build,
    name: []const u8,
    config: SystemIntegrationOptionConfig,
) bool {
    const gop = b.graph.system_library_options.getOrPut(b.allocator, name) catch @panic("OOM");
    if (gop.found_existing) switch (gop.value_ptr.*) {
        .user_disabled => {
            gop.value_ptr.* = .declared_disabled;
            return false;
        },
        .user_enabled => {
            gop.value_ptr.* = .declared_enabled;
            return true;
        },
        .declared_disabled => return false,
        .declared_enabled => return true,
    } else {
        gop.key_ptr.* = b.dupe(name);
        if (config.default orelse b.graph.system_package_mode) {
            gop.value_ptr.* = .declared_enabled;
            return true;
        } else {
            gop.value_ptr.* = .declared_disabled;
            return false;
        }
    }
}

test {
    _ = Cache;
    _ = Step;
}



---
File: /std/builtin.zig
---

//! Types and values provided by the Zig language.

const builtin = @import("builtin");
const std = @import("std.zig");
const root = @import("root");

pub const assembly = @import("builtin/assembly.zig");

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const StackTrace = struct {
    index: usize,
    instruction_addresses: []usize,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const GlobalLinkage = enum(u2) {
    internal,
    strong,
    weak,
    link_once,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const SymbolVisibility = enum(u2) {
    default,
    hidden,
    protected,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const AtomicOrder = enum {
    unordered,
    monotonic,
    acquire,
    release,
    acq_rel,
    seq_cst,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const ReduceOp = enum {
    And,
    Or,
    Xor,
    Min,
    Max,
    Add,
    Mul,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const AtomicRmwOp = enum {
    /// Exchange - store the operand unmodified.
    /// Supports enums, integers, and floats.
    Xchg,
    /// Add operand to existing value.
    /// Supports integers and floats.
    /// For integers, two's complement wraparound applies.
    Add,
    /// Subtract operand from existing value.
    /// Supports integers and floats.
    /// For integers, two's complement wraparound applies.
    Sub,
    /// Perform bitwise AND on existing value with operand.
    /// Supports integers.
    And,
    /// Perform bitwise NAND on existing value with operand.
    /// Supports integers.
    Nand,
    /// Perform bitwise OR on existing value with operand.
    /// Supports integers.
    Or,
    /// Perform bitwise XOR on existing value with operand.
    /// Supports integers.
    Xor,
    /// Store operand if it is larger than the existing value.
    /// Supports integers and floats.
    Max,
    /// Store operand if it is smaller than the existing value.
    /// Supports integers and floats.
    Min,
};

/// The code model puts constraints on the location of symbols and the size of code and data.
/// The selection of a code model is a trade off on speed and restrictions that needs to be selected on a per application basis to meet its requirements.
/// A slightly more detailed explanation can be found in (for example) the [System V Application Binary Interface (x86_64)](https://github.com/hjl-tools/x86-psABI/wiki/x86-64-psABI-1.0.pdf) 3.5.1.
///
/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const CodeModel = enum {
    default,
    extreme,
    kernel,
    large,
    medany,
    medium,
    medlow,
    medmid,
    normal,
    small,
    tiny,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const OptimizeMode = enum {
    Debug,
    ReleaseSafe,
    ReleaseFast,
    ReleaseSmall,
};

/// The calling convention of a function defines how arguments and return values are passed, as well
/// as any other requirements which callers and callees must respect, such as register preservation
/// and stack alignment.
///
/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const CallingConvention = union(enum(u8)) {
    pub const Tag = @typeInfo(CallingConvention).@"union".tag_type.?;

    /// This is an alias for the default C calling convention for this target.
    /// Functions marked as `extern` or `export` are given this calling convention by default.
    pub const c = builtin.target.cCallingConvention().?;

    pub const winapi: CallingConvention = switch (builtin.target.cpu.arch) {
        .x86_64 => .{ .x86_64_win = .{} },
        .x86 => .{ .x86_stdcall = .{} },
        .aarch64 => .{ .aarch64_aapcs_win = .{} },
        .thumb => .{ .arm_aapcs_vfp = .{} },
        else => unreachable,
    };

    pub const kernel: CallingConvention = switch (builtin.target.cpu.arch) {
        .amdgcn => .amdgcn_kernel,
        .nvptx, .nvptx64 => .nvptx_kernel,
        .spirv32, .spirv64 => .spirv_kernel,
        else => unreachable,
    };

    /// The default Zig calling convention when neither `export` nor `inline` is specified.
    /// This calling convention makes no guarantees about stack alignment, registers, etc.
    /// It can only be used within this Zig compilation unit.
    auto,

    /// The calling convention of a function that can be called with `async` syntax. An `async` call
    /// of a runtime-known function must target a function with this calling convention.
    /// Comptime-known functions with other calling conventions may be coerced to this one.
    async,

    /// Functions with this calling convention have no prologue or epilogue, making the function
    /// uncallable in regular Zig code. This can be useful when integrating with assembly.
    naked,

    /// This calling convention is exactly equivalent to using the `inline` keyword on a function
    /// definition. This function will be semantically inlined by the Zig compiler at call sites.
    /// Pointers to inline functions are comptime-only.
    @"inline",

    // Calling conventions for the `x86_64` architecture.
    x86_64_sysv: CommonOptions,
    x86_64_x32: CommonOptions,
    x86_64_win: CommonOptions,
    x86_64_regcall_v3_sysv: CommonOptions,
    x86_64_regcall_v4_win: CommonOptions,
    x86_64_vectorcall: CommonOptions,
    x86_64_interrupt: CommonOptions,

    // Calling conventions for the `x86` architecture.
    x86_sysv: X86RegparmOptions,
    x86_win: X86RegparmOptions,
    x86_stdcall: X86RegparmOptions,
    x86_fastcall: CommonOptions,
    x86_thiscall: CommonOptions,
    x86_thiscall_mingw: CommonOptions,
    x86_regcall_v3: CommonOptions,
    x86_regcall_v4_win: CommonOptions,
    x86_vectorcall: CommonOptions,
    x86_interrupt: CommonOptions,

    // Calling conventions for the `x86_16` architecture.

    x86_16_cdecl: CommonOptions,
    x86_16_stdcall: CommonOptions,
    x86_16_regparmcall: CommonOptions,
    x86_16_interrupt: CommonOptions,

    // Calling conventions for the `aarch64` and `aarch64_be` architectures.
    aarch64_aapcs: CommonOptions,
    aarch64_aapcs_darwin: CommonOptions,
    aarch64_aapcs_win: CommonOptions,
    aarch64_vfabi: CommonOptions,
    aarch64_vfabi_sve: CommonOptions,

    /// The standard `alpha` calling convention.
    alpha_osf: CommonOptions,

    // Calling convetions for the `arm`, `armeb`, `thumb`, and `thumbeb` architectures.
    /// ARM Architecture Procedure Call Standard
    arm_aapcs: CommonOptions,
    /// ARM Architecture Procedure Call Standard Vector Floating-Point
    arm_aapcs_vfp: CommonOptions,
    arm_interrupt: ArmInterruptOptions,

    // Calling conventions for the `mips64` and `mips64el` architectures.
    mips64_n64: CommonOptions,
    mips64_n32: CommonOptions,
    mips64_interrupt: MipsInterruptOptions,

    // Calling conventions for the `mips` and `mipsel` architectures.
    mips_o32: CommonOptions,
    mips_interrupt: MipsInterruptOptions,

    // Calling conventions for the `riscv64` architecture.
    riscv64_lp64: CommonOptions,
    riscv64_lp64_v: CommonOptions,
    riscv64_interrupt: RiscvInterruptOptions,

    // Calling conventions for the `riscv32` architecture.
    riscv32_ilp32: CommonOptions,
    riscv32_ilp32_v: CommonOptions,
    riscv32_interrupt: RiscvInterruptOptions,

    // Calling conventions for the `sparc64` architecture.
    sparc64_sysv: CommonOptions,

    // Calling conventions for the `sparc` architecture.
    sparc_sysv: CommonOptions,

    // Calling conventions for the `powerpc64` and `powerpc64le` architectures.
    powerpc64_elf: CommonOptions,
    powerpc64_elf_altivec: CommonOptions,
    powerpc64_elf_v2: CommonOptions,

    // Calling conventions for the `powerpc` and `powerpcle` architectures.
    powerpc_sysv: CommonOptions,
    powerpc_sysv_altivec: CommonOptions,
    powerpc_aix: CommonOptions,
    powerpc_aix_altivec: CommonOptions,

    /// The standard `wasm32` and `wasm64` calling convention, as specified in the WebAssembly Tool Conventions.
    wasm_mvp: CommonOptions,

    /// The standard `arc`/`arceb` calling convention.
    arc_sysv: CommonOptions,
    arc_interrupt: ArcInterruptOptions,

    // Calling conventions for the `avr` architecture.
    avr_gnu,
    avr_builtin,
    avr_signal,
    avr_interrupt,

    /// The standard `bpfel`/`bpfeb` calling convention.
    bpf_std: CommonOptions,

    // Calling conventions for the `csky` architecture.
    csky_sysv: CommonOptions,
    csky_interrupt: CommonOptions,

    // Calling conventions for the `hexagon` architecture.
    hexagon_sysv: CommonOptions,
    hexagon_sysv_hvx: CommonOptions,

    /// The standard `hppa` calling convention.
    hppa_elf: CommonOptions,

    /// The standard `hppa64` calling convention.
    hppa64_elf: CommonOptions,

    kvx_lp64: CommonOptions,
    kvx_ilp32: CommonOptions,

    /// The standard `lanai` calling convention.
    lanai_sysv: CommonOptions,

    /// The standard `loongarch64` calling convention.
    loongarch64_lp64: CommonOptions,

    /// The standard `loongarch32` calling convention.
    loongarch32_ilp32: CommonOptions,

    // Calling conventions for the `m68k` architecture.
    m68k_sysv: CommonOptions,
    m68k_gnu: CommonOptions,
    m68k_rtd: CommonOptions,
    m68k_interrupt: CommonOptions,

    /// The standard `microblaze`/`microblazeel` calling convention.
    microblaze_std: CommonOptions,
    microblaze_interrupt: MicroblazeInterruptOptions,

    /// The standard `msp430` calling convention.
    msp430_eabi: CommonOptions,
    msp430_interrupt: CommonOptions,

    /// The standard `or1k` calling convention.
    or1k_sysv: CommonOptions,

    /// The standard `propeller` calling convention.
    propeller_sysv: CommonOptions,

    // Calling conventions for the `s390x` architecture.
    s390x_sysv: CommonOptions,
    s390x_sysv_vx: CommonOptions,

    // Calling conventions for the `sh`/`sheb` architecture.
    sh_gnu: CommonOptions,
    sh_renesas: CommonOptions,
    sh_interrupt: ShInterruptOptions,

    /// The standard `ve` calling convention.
    ve_sysv: CommonOptions,

    // Calling conventions for the `xcore` architecture.
    xcore_xs1: CommonOptions,
    xcore_xs2: CommonOptions,

    // Calling conventions for the `xtensa`/`xtensaeb` architecture.
    xtensa_call0: CommonOptions,
    xtensa_windowed: CommonOptions,

    // Calling conventions for the `amdgcn` architecture.
    amdgcn_device: CommonOptions,
    amdgcn_kernel,
    amdgcn_cs: CommonOptions,

    // Calling conventions for the `nvptx` and `nvptx64` architectures.
    nvptx_device,
    nvptx_kernel,

    // Calling conventions for kernels and shaders on the `spirv`, `spirv32`, and `spirv64` architectures.
    spirv_device,
    spirv_kernel,
    spirv_fragment,
    spirv_vertex,

    /// Options shared across most calling conventions.
    pub const CommonOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
    };

    /// Options for x86 calling conventions which support the regparm attribute to pass some
    /// arguments in registers.
    pub const X86RegparmOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        /// The number of arguments to pass in registers before passing the remaining arguments
        /// according to the calling convention.
        /// Equivalent to `__attribute__((regparm(x)))` in Clang and GCC.
        register_params: u2 = 0,
    };

    /// Options for the `arc_interrupt` calling convention.
    pub const ArcInterruptOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        /// The kind of interrupt being received.
        type: InterruptType,

        pub const InterruptType = enum(u2) {
            ilink1,
            ilink2,
            ilink,
            firq,
        };
    };

    /// Options for the `arm_interrupt` calling convention.
    pub const ArmInterruptOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        /// The kind of interrupt being received.
        type: InterruptType = .generic,

        pub const InterruptType = enum(u3) {
            generic,
            irq,
            fiq,
            swi,
            abort,
            undef,
        };
    };

    /// Options for the `microblaze_interrupt` calling convention.
    pub const MicroblazeInterruptOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        type: InterruptType = .regular,

        pub const InterruptType = enum(u2) {
            /// User exception; return with `rtsd`.
            user,
            /// Regular interrupt; return with `rtid`.
            regular,
            /// Fast interrupt; return with `rtid`.
            fast,
            /// Software breakpoint; return with `rtbd`.
            breakpoint,
        };
    };

    /// Options for the `mips_interrupt` and `mips64_interrupt` calling conventions.
    pub const MipsInterruptOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        /// The interrupt mode.
        mode: InterruptMode = .eic,

        pub const InterruptMode = enum(u4) {
            eic,
            sw0,
            sw1,
            hw0,
            hw1,
            hw2,
            hw3,
            hw4,
            hw5,
        };
    };

    /// Options for the `riscv32_interrupt` and `riscv64_interrupt` calling conventions.
    pub const RiscvInterruptOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        /// The privilege mode.
        mode: PrivilegeMode,

        pub const PrivilegeMode = enum(u2) {
            supervisor,
            machine,
        };
    };

    /// Options for the `sh_interrupt` calling convention.
    pub const ShInterruptOptions = struct {
        /// The boundary the stack is aligned to when the function is called.
        /// `null` means the default for this calling convention.
        incoming_stack_alignment: ?u64 = null,
        save: SaveBehavior = .full,

        pub const SaveBehavior = enum(u3) {
            /// Save only fpscr (if applicable).
            fpscr,
            /// Save only high-numbered registers, i.e. r0 through r7 are *not* saved.
            high,
            /// Save all registers normally.
            full,
            /// Save all registers using the CPU's fast register bank.
            bank,
        };
    };

    /// Returns the array of `std.Target.Cpu.Arch` to which this `CallingConvention` applies.
    /// Asserts that `cc` is not `.auto`, `.@"async"`, `.naked`, or `.@"inline"`.
    pub fn archs(cc: CallingConvention) []const std.Target.Cpu.Arch {
        return std.Target.Cpu.Arch.fromCallingConvention(cc);
    }

    pub fn eql(a: CallingConvention, b: CallingConvention) bool {
        return std.meta.eql(a, b);
    }

    pub fn withStackAlign(cc: CallingConvention, incoming_stack_alignment: u64) CallingConvention {
        const tag: CallingConvention.Tag = cc;
        var result = cc;
        @field(result, @tagName(tag)).incoming_stack_alignment = incoming_stack_alignment;
        return result;
    }
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const AddressSpace = enum(u5) {
    // CPU address spaces.
    generic,
    gs,
    fs,
    ss,

    // x86_16 extra address spaces.
    /// Allows addressing the entire address space by storing both segment and offset.
    far,

    // GPU address spaces.
    global,
    constant,
    param,
    shared,
    local,
    input,
    output,
    uniform,
    push_constant,
    storage_buffer,
    physical_storage_buffer,

    // AVR address spaces.
    flash,
    flash1,
    flash2,
    flash3,
    flash4,
    flash5,

    // Propeller address spaces.

    /// This address space only addresses the cog-local ram.
    cog,

    /// This address space only addresses shared hub ram.
    hub,

    /// This address space only addresses the "lookup" ram
    lut,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const SourceLocation = struct {
    /// The name chosen when compiling. Not a file path.
    module: [:0]const u8,
    /// Relative to the root directory of its module.
    file: [:0]const u8,
    fn_name: [:0]const u8,
    line: u32,
    column: u32,
};

pub const TypeId = std.meta.Tag(Type);

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const Type = union(enum) {
    type,
    void,
    bool,
    noreturn,
    int: Int,
    float: Float,
    pointer: Pointer,
    array: Array,
    @"struct": Struct,
    comptime_float,
    comptime_int,
    undefined,
    null,
    optional: Optional,
    error_union: ErrorUnion,
    error_set: ErrorSet,
    @"enum": Enum,
    @"union": Union,
    @"fn": Fn,
    @"opaque": Opaque,
    frame: Frame,
    @"anyframe": AnyFrame,
    vector: Vector,
    enum_literal,

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Int = struct {
        signedness: Signedness,
        bits: u16,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Float = struct {
        bits: u16,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Pointer = struct {
        size: Size,
        is_const: bool,
        is_volatile: bool,
        /// `null` means implicit alignment, which is equivalent to `@alignOf(child)`.
        alignment: ?usize,
        address_space: AddressSpace,
        child: type,
        is_allowzero: bool,

        /// The type of the sentinel is the element type of the pointer, which is
        /// the value of the `child` field in this struct. However there is no way
        /// to refer to that type here, so we use `*const anyopaque`.
        /// See also: `sentinel`
        sentinel_ptr: ?*const anyopaque,

        /// Loads the pointer type's sentinel value from `sentinel_ptr`.
        /// Returns `null` if the pointer type has no sentinel.
        pub inline fn sentinel(comptime ptr: Pointer) ?ptr.child {
            const sp: *const ptr.child = @ptrCast(@alignCast(ptr.sentinel_ptr orelse return null));
            return sp.*;
        }

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Size = enum(u2) {
            one,
            many,
            slice,
            c,
        };

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Attributes = struct {
            @"const": bool = false,
            @"volatile": bool = false,
            @"allowzero": bool = false,
            @"addrspace": ?AddressSpace = null,
            @"align": ?usize = null,
        };
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Array = struct {
        len: comptime_int,
        child: type,

        /// The type of the sentinel is the element type of the array, which is
        /// the value of the `child` field in this struct. However there is no way
        /// to refer to that type here, so we use `*const anyopaque`.
        /// See also: `sentinel`.
        sentinel_ptr: ?*const anyopaque,

        /// Loads the array type's sentinel value from `sentinel_ptr`.
        /// Returns `null` if the array type has no sentinel.
        pub inline fn sentinel(comptime arr: Array) ?arr.child {
            const sp: *const arr.child = @ptrCast(@alignCast(arr.sentinel_ptr orelse return null));
            return sp.*;
        }
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const ContainerLayout = enum(u2) {
        auto,
        @"extern",
        @"packed",
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const StructField = struct {
        name: [:0]const u8,
        type: type,
        /// The type of the default value is the type of this struct field, which
        /// is the value of the `type` field in this struct. However there is no
        /// way to refer to that type here, so we use `*const anyopaque`.
        /// See also: `defaultValue`.
        default_value_ptr: ?*const anyopaque,
        is_comptime: bool,
        /// `null` means the field alignment was not explicitly specified. The
        /// field will still be aligned to at least `@alignOf` its `type`.
        alignment: ?usize,

        /// Loads the field's default value from `default_value_ptr`.
        /// Returns `null` if the field has no default value.
        pub inline fn defaultValue(comptime sf: StructField) ?sf.type {
            const dp: *const sf.type = @ptrCast(@alignCast(sf.default_value_ptr orelse return null));
            return dp.*;
        }

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Attributes = struct {
            @"comptime": bool = false,
            @"align": ?usize = null,
            default_value_ptr: ?*const anyopaque = null,
        };
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Struct = struct {
        layout: ContainerLayout,
        /// Only valid if layout is .@"packed"
        backing_integer: ?type = null,
        fields: []const StructField,
        decls: []const Declaration,
        is_tuple: bool,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Optional = struct {
        child: type,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const ErrorUnion = struct {
        error_set: type,
        payload: type,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Error = struct {
        name: [:0]const u8,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const ErrorSet = ?[]const Error;

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const EnumField = struct {
        name: [:0]const u8,
        value: comptime_int,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Enum = struct {
        tag_type: type,
        fields: []const EnumField,
        decls: []const Declaration,
        is_exhaustive: bool,

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Mode = enum { exhaustive, nonexhaustive };
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const UnionField = struct {
        name: [:0]const u8,
        type: type,
        /// `null` means the field alignment was not explicitly specified. The
        /// field will still be aligned to at least `@alignOf` its `type`.
        alignment: ?usize,

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Attributes = struct {
            @"align": ?usize = null,
        };
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Union = struct {
        layout: ContainerLayout,
        tag_type: ?type,
        fields: []const UnionField,
        decls: []const Declaration,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Fn = struct {
        calling_convention: CallingConvention,
        is_generic: bool,
        is_var_args: bool,
        /// TODO change the language spec to make this not optional.
        return_type: ?type,
        params: []const Param,

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Param = struct {
            is_generic: bool,
            is_noalias: bool,
            type: ?type,

            /// This data structure is used by the Zig language code generation and
            /// therefore must be kept in sync with the compiler implementation.
            pub const Attributes = struct {
                @"noalias": bool = false,
            };
        };

        /// This data structure is used by the Zig language code generation and
        /// therefore must be kept in sync with the compiler implementation.
        pub const Attributes = struct {
            @"callconv": CallingConvention = .auto,
            varargs: bool = false,
        };
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Opaque = struct {
        decls: []const Declaration,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Frame = struct {
        function: *const anyopaque,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const AnyFrame = struct {
        child: ?type,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Vector = struct {
        len: comptime_int,
        child: type,
    };

    /// This data structure is used by the Zig language code generation and
    /// therefore must be kept in sync with the compiler implementation.
    pub const Declaration = struct {
        name: [:0]const u8,
    };
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const FloatMode = enum {
    strict,
    optimized,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const Endian = enum {
    big,
    little,

    pub const native = builtin.target.cpu.arch.endian();
    pub const foreign: Endian = @enumFromInt(1 - @intFromEnum(native));
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const Signedness = enum(u1) {
    signed,
    unsigned,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const OutputMode = enum {
    Exe,
    Lib,
    Obj,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const LinkMode = enum {
    static,
    dynamic,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const UnwindTables = enum {
    none,
    sync,
    async,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const WasiExecModel = enum {
    command,
    reactor,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const CallModifier = enum {
    /// Equivalent to function call syntax.
    auto,
    /// Prevents tail call optimization. This guarantees that the return
    /// address will point to the callsite, as opposed to the callsite's
    /// callsite. If the call is otherwise required to be tail-called
    /// or inlined, a compile error is emitted instead.
    never_tail,
    /// Guarantees that the call will not be inlined. If the call is
    /// otherwise required to be inlined, a compile error is emitted instead.
    never_inline,
    /// Asserts that the function call will not suspend. This allows a
    /// non-async function to call an async function.
    no_suspend,
    /// Guarantees that the call will be generated with tail call optimization.
    /// If this is not possible, a compile error is emitted instead.
    always_tail,
    /// Guarantees that the call will be inlined at the callsite.
    /// If this is not possible, a compile error is emitted instead.
    always_inline,
    /// Evaluates the call at compile-time. If the call cannot be completed at
    /// compile-time, a compile error is emitted instead.
    compile_time,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListAarch64 = extern struct {
    __stack: *anyopaque,
    __gr_top: *anyopaque,
    __vr_top: *anyopaque,
    __gr_offs: c_int,
    __vr_offs: c_int,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListAlpha = extern struct {
    __base: *anyopaque,
    __offset: c_int,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListArm = extern struct {
    __ap: *anyopaque,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListHexagon = extern struct {
    __gpr: c_long,
    __fpr: c_long,
    __overflow_arg_area: *anyopaque,
    __reg_save_area: *anyopaque,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListPowerPc = extern struct {
    gpr: u8,
    fpr: u8,
    reserved: c_ushort,
    overflow_arg_area: *anyopaque,
    reg_save_area: *anyopaque,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListS390x = extern struct {
    __current_saved_reg_area_pointer: *anyopaque,
    __saved_reg_area_end_pointer: *anyopaque,
    __overflow_area_pointer: *anyopaque,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListSh = extern struct {
    __va_next_o: *anyopaque,
    __va_next_o_limit: *anyopaque,
    __va_next_fp: *anyopaque,
    __va_next_fp_limit: *anyopaque,
    __va_next_stack: *anyopaque,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListX86_64 = extern struct {
    gp_offset: c_uint,
    fp_offset: c_uint,
    overflow_arg_area: *anyopaque,
    reg_save_area: *anyopaque,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaListXtensa = extern struct {
    __va_stk: *c_int,
    __va_reg: *c_int,
    __va_ndx: c_int,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const VaList = switch (builtin.cpu.arch) {
    .amdgcn,
    .msp430,
    .nvptx,
    .nvptx64,
    .powerpc64,
    .powerpc64le,
    .x86,
    => *u8,
    .arc,
    .arceb,
    .avr,
    .bpfel,
    .bpfeb,
    .csky,
    .hppa,
    .hppa64,
    .kvx,
    .lanai,
    .loongarch32,
    .loongarch64,
    .m68k,
    .microblaze,
    .microblazeel,
    .mips,
    .mipsel,
    .mips64,
    .mips64el,
    .riscv32,
    .riscv32be,
    .riscv64,
    .riscv64be,
    .sparc,
    .sparc64,
    .spirv32,
    .spirv64,
    .ve,
    .wasm32,
    .wasm64,
    .xcore,
    => *anyopaque,
    .aarch64, .aarch64_be => switch (builtin.os.tag) {
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .windows => *u8,
        else => switch (builtin.zig_backend) {
            else => VaListAarch64,
            .stage2_llvm => @compileError("disabled due to miscompilations"),
        },
    },
    .alpha => VaListAlpha,
    .arm, .armeb, .thumb, .thumbeb => VaListArm,
    .hexagon => if (builtin.target.abi.isMusl()) VaListHexagon else *u8,
    .powerpc, .powerpcle => VaListPowerPc,
    .s390x => VaListS390x,
    .sh, .sheb => VaListSh, // This is wrong for `sh_renesas`: https://github.com/ziglang/zig/issues/24692#issuecomment-3150779829
    .x86_64 => switch (builtin.os.tag) {
        .uefi, .windows => switch (builtin.zig_backend) {
            else => *u8,
            .stage2_llvm => @compileError("disabled due to miscompilations"),
        },
        else => VaListX86_64,
    },
    .xtensa, .xtensaeb => VaListXtensa,
    else => @compileError("VaList not supported for this target yet"),
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const PrefetchOptions = struct {
    /// Whether the prefetch should prepare for a read or a write.
    rw: Rw = .read,
    /// The data's locality in an inclusive range from 0 to 3.
    ///
    /// 0 means no temporal locality. That is, the data can be immediately
    /// dropped from the cache after it is accessed.
    ///
    /// 3 means high temporal locality. That is, the data should be kept in
    /// the cache as it is likely to be accessed again soon.
    locality: u2 = 3,
    /// The cache that the prefetch should be performed on.
    cache: Cache = .data,

    pub const Rw = enum(u1) {
        read,
        write,
    };

    pub const Cache = enum(u1) {
        instruction,
        data,
    };
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const ExportOptions = struct {
    name: []const u8,
    linkage: GlobalLinkage = .strong,
    section: ?[]const u8 = null,
    visibility: SymbolVisibility = .default,
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const ExternOptions = struct {
    name: []const u8,
    library_name: ?[]const u8 = null,
    linkage: GlobalLinkage = .strong,
    visibility: SymbolVisibility = .default,
    /// Setting this to `true` makes the `@extern` a runtime value.
    is_thread_local: bool = false,
    is_dll_import: bool = false,
    relocation: Relocation = .any,
    decoration: ?Decoration = null,

    pub const Decoration = union(enum) {
        location: u32,
        descriptor: Descriptor,

        pub const Descriptor = struct {
            binding: u32,
            set: u32,
        };
    };

    pub const Relocation = enum(u1) {
        /// Any type of relocation is allowed.
        any,
        /// A program-counter-relative relocation is required.
        /// Using this value makes the `@extern` a runtime value.
        pcrel,
    };
};

/// This data structure is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const BranchHint = enum(u3) {
    /// Equivalent to no hint given.
    none,
    /// This branch of control flow is more likely to be reached than its peers.
    /// The optimizer should optimize for reaching it.
    likely,
    /// This branch of control flow is less likely to be reached than its peers.
    /// The optimizer should optimize for not reaching it.
    unlikely,
    /// This branch of control flow is unlikely to *ever* be reached.
    /// The optimizer may place it in a different page of memory to optimize other branches.
    cold,
    /// It is difficult to predict whether this branch of control flow will be reached.
    /// The optimizer should avoid branching behavior with expensive mispredictions.
    unpredictable,
};

/// This enum is set by the compiler and communicates which compiler backend is
/// used to produce machine code.
/// Think carefully before deciding to observe this value. Nearly all code should
/// be agnostic to the backend that implements the language. The use case
/// to use this value is to **work around problems with compiler implementations.**
///
/// Avoid failing the compilation if the compiler backend does not match a
/// whitelist of backends; rather one should detect that a known problem would
/// occur in a blacklist of backends.
///
/// The enum is nonexhaustive so that alternate Zig language implementations may
/// choose a number as their tag (please use a random number generator rather
/// than a "cute" number) and codebases can interact with these values even if
/// this upstream enum does not have a name for the number. Of course, upstream
/// is happy to accept pull requests to add Zig implementations to this enum.
///
/// This data structure is part of the Zig language specification.
pub const CompilerBackend = enum(u64) {
    /// It is allowed for a compiler implementation to not reveal its identity,
    /// in which case this value is appropriate. Be cool and make sure your
    /// code supports `other` Zig compilers!
    other = 0,
    /// The original Zig compiler created in 2015 by Andrew Kelley. Implemented
    /// in C++. Used LLVM. Deleted from the ZSF ziglang/zig codebase on
    /// December 6th, 2022.
    stage1 = 1,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// LLVM backend.
    stage2_llvm = 2,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// backend that generates C source code.
    /// Note that one can observe whether the compilation will output C code
    /// directly with `object_format` value rather than the `compiler_backend` value.
    stage2_c = 3,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// WebAssembly backend.
    stage2_wasm = 4,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// arm backend.
    stage2_arm = 5,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// x86_64 backend.
    stage2_x86_64 = 6,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// aarch64 backend.
    stage2_aarch64 = 7,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// x86 backend.
    stage2_x86 = 8,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// riscv64 backend.
    stage2_riscv64 = 9,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// sparc64 backend.
    stage2_sparc64 = 10,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// spirv backend.
    stage2_spirv = 11,
    /// The reference implementation self-hosted compiler of Zig, using the
    /// powerpc backend.
    stage2_powerpc = 12,

    _,
};

/// This function type is used by the Zig language code generation and
/// therefore must be kept in sync with the compiler implementation.
pub const TestFn = struct {
    name: []const u8,
    func: *const fn () anyerror!void,
};

/// This namespace is used by the Zig compiler to emit various kinds of safety
/// panics. These can be overridden by making a public `panic` namespace in the
/// root source file.
pub const panic: type = p: {
    if (@hasDecl(root, "panic")) {
        if (@TypeOf(root.panic) != type) {
            // Deprecated; make `panic` a namespace instead.
            break :p std.debug.FullPanic(struct {
                fn panic(msg: []const u8, ra: ?usize) noreturn {
                    root.panic(msg, @errorReturnTrace(), ra);
                }
            }.panic);
        }
        break :p root.panic;
    }
    break :p switch (builtin.zig_backend) {
        .stage2_powerpc,
        .stage2_riscv64,
        => std.debug.simple_panic,
        else => std.debug.FullPanic(std.debug.defaultPanic),
    };
};

pub noinline fn returnError() void {
    @branchHint(.unlikely);
    @setRuntimeSafety(false);
    const st = @errorReturnTrace().?;
    if (st.index < st.instruction_addresses.len)
        st.instruction_addresses[st.index] = @returnAddress();
    st.index += 1;
}



---
File: /std/c.zig
---

const builtin = @import("builtin");
const native_abi = builtin.abi;
const native_arch = builtin.cpu.arch;
const native_os = builtin.os.tag;
const native_endian = builtin.cpu.arch.endian();

const std = @import("std");
const c = @This();
const maxInt = std.math.maxInt;
const assert = std.debug.assert;
const page_size = std.heap.page_size_min;
const linux = std.os.linux;
const emscripten = std.os.emscripten;
const wasi = std.os.wasi;
const windows = std.os.windows;
const ws2_32 = std.os.windows.ws2_32;
const darwin = @import("c/darwin.zig");
const freebsd = @import("c/freebsd.zig");
const illumos = @import("c/illumos.zig");
const netbsd = @import("c/netbsd.zig");
const dragonfly = @import("c/dragonfly.zig");
const haiku = @import("c/haiku.zig");
const openbsd = @import("c/openbsd.zig");
const serenity = @import("c/serenity.zig");

// These constants are shared among all operating systems even when not linking
// libc.

pub const iovec = std.posix.iovec;
pub const iovec_const = std.posix.iovec_const;
pub const LOCK = std.posix.LOCK;
pub const winsize = std.posix.winsize;

/// The value of the link editor defined symbol _MH_EXECUTE_SYM is the address
/// of the mach header in a Mach-O executable file type.  It does not appear in
/// any file type other than a MH_EXECUTE file type.  The type of the symbol is
/// absolute as the header is not part of any section.
/// This symbol is populated when linking the system's libc, which is guaranteed
/// on this operating system. However when building object files or libraries,
/// the system libc won't be linked until the final executable. So we
/// export a weak symbol here, to be overridden by the real one.
pub extern var _mh_execute_header: mach_hdr;
var dummy_execute_header: mach_hdr = undefined;
comptime {
    if (native_os.isDarwin()) {
        @export(&dummy_execute_header, .{ .name = "_mh_execute_header", .linkage = .weak });
    }
}

/// * If not linking libc, returns `false`.
/// * If linking musl libc, returns `true`.
/// * If linking GNU libc (glibc), returns `true` if the target version is greater than or equal to
///   `version`.
/// * If linking Android libc (bionic), returns `true` if the target API level is greater than or
///   equal to `version.major`, ignoring other components.
/// * If linking a libc other than these, returns `false`.
pub inline fn versionCheck(comptime version: std.SemanticVersion) bool {
    return comptime blk: {
        if (!builtin.link_libc) break :blk false;
        if (native_abi.isMusl()) break :blk true;
        if (builtin.target.isGnuLibC()) {
            const ver = builtin.os.versionRange().gnuLibCVersion().?;
            break :blk switch (ver.order(version)) {
                .gt, .eq => true,
                .lt => false,
            };
        } else if (builtin.abi.isAndroid()) {
            break :blk builtin.os.version_range.linux.android >= version.major;
        } else {
            break :blk false;
        }
    };
}

/// Get the errno if rc is -1 and SUCCESS if rc is not -1.
pub fn errno(rc: anytype) E {
    return if (rc == -1) @enumFromInt(_errno().*) else .SUCCESS;
}

pub const ino_t = switch (native_os) {
    .linux => linux.ino_t,
    .emscripten => emscripten.ino_t,
    .wasi => wasi.inode_t,
    .windows => windows.LARGE_INTEGER,
    .haiku => i64,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L38
    else => u64,
};

pub const off_t = switch (native_os) {
    .linux => linux.off_t,
    .emscripten => emscripten.off_t,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L39
    else => i64,
};

/// For use with `utimensat` and `futimens`.
pub const UTIME = switch (native_os) {
    .dragonfly, .freebsd, .illumos, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .wasi => struct {
        pub const NOW: timespec = .{ .sec = 0, .nsec = -1 };
        pub const OMIT: timespec = .{ .sec = 0, .nsec = -2 };
    },
    .emscripten => emscripten.UTIME,
    .haiku => struct {
        pub const NOW: timespec = .{ .sec = 0, .nsec = 1000000000 };
        pub const OMIT: timespec = .{ .sec = 0, .nsec = 1000000001 };
    },
    .linux => linux.UTIME,
    .netbsd => struct {
        pub const NOW: timespec = .{ .sec = 0, .nsec = 0x3fffffff };
        pub const OMIT: timespec = .{ .sec = 0, .nsec = 0x3ffffffe };
    },
    .openbsd, .serenity => struct {
        pub const NOW: timespec = .{ .sec = 0, .nsec = -2 };
        pub const OMIT: timespec = .{ .sec = 0, .nsec = -1 };
    },
    else => void,
};

pub const timespec = switch (native_os) {
    .linux => linux.timespec,
    .emscripten => emscripten.timespec,
    // lib/libc/include/wasm-wasi-musl/__struct_timespec.h
    .wasi => extern struct {
        sec: time_t,
        nsec: c_long,

        pub fn fromTimestamp(tm: wasi.timestamp_t) timespec {
            const sec: wasi.timestamp_t = tm / 1_000_000_000;
            const nsec = tm - sec * 1_000_000_000;
            return .{
                .sec = @as(time_t, @intCast(sec)),
                .nsec = @as(isize, @intCast(nsec)),
            };
        }

        pub fn toTimestamp(ts: timespec) wasi.timestamp_t {
            return @as(wasi.timestamp_t, @intCast(ts.sec * 1_000_000_000)) +
                @as(wasi.timestamp_t, @intCast(ts.nsec));
        }
    },
    // https://github.com/SerenityOS/serenity/blob/0a78056453578c18e0a04a0b45ebfb1c96d59005/Kernel/API/POSIX/time.h#L17-L20
    .dragonfly, .freebsd, .netbsd, .openbsd, .illumos, .haiku, .serenity, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .windows => extern struct {
        sec: time_t,
        nsec: c_long,
    },
    else => void,
};

pub const dev_t = switch (native_os) {
    .linux => linux.dev_t,
    .emscripten => emscripten.dev_t,
    .wasi => wasi.device_t,
    .openbsd, .haiku, .illumos, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => i32,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L43
    .netbsd, .freebsd, .serenity => u64,
    else => void,
};

pub const mode_t = switch (native_os) {
    .linux => linux.mode_t,
    .emscripten => emscripten.mode_t,
    .openbsd, .haiku, .netbsd, .illumos, .windows => u32,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L44
    .freebsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .dragonfly, .serenity => u16,
    .wasi => if (builtin.link_libc) u32 else u0, // WASI libc emulates mode.
    else => u0,
};

pub const nlink_t = switch (native_os) {
    .linux => linux.nlink_t,
    .emscripten => emscripten.nlink_t,
    .wasi => c_ulonglong,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L45
    .freebsd, .serenity => u64,
    .openbsd, .netbsd, .dragonfly, .illumos, .windows => u32,
    .haiku => i32,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => u16,
    else => u0,
};

pub const uid_t = switch (native_os) {
    .linux => linux.uid_t,
    .emscripten => emscripten.uid_t,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L28
    else => u32,
};

pub const gid_t = switch (native_os) {
    .linux => linux.gid_t,
    .emscripten => emscripten.gid_t,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L29
    else => u32,
};

pub const blksize_t = switch (native_os) {
    .linux => linux.blksize_t,
    .emscripten => emscripten.blksize_t,
    .wasi => c_long,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L42
    .serenity => u64,
    else => i32,
};

pub const passwd = switch (native_os) {
    // https://github.com/SerenityOS/serenity/blob/7442cfb5072b74a62c0e061e6e9ff44fda08780d/Userland/Libraries/LibC/pwd.h#L15-L23
    .linux, .serenity => extern struct {
        name: ?[*:0]const u8, // username
        passwd: ?[*:0]const u8, // user password
        uid: uid_t, // user ID
        gid: gid_t, // group ID
        gecos: ?[*:0]const u8, // user information
        dir: ?[*:0]const u8, // home directory
        shell: ?[*:0]const u8, // shell program
    },
    .netbsd, .openbsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        name: ?[*:0]const u8, // user name
        passwd: ?[*:0]const u8, // encrypted password
        uid: uid_t, // user uid
        gid: gid_t, // user gid
        change: time_t, // password change time
        class: ?[*:0]const u8, // user access class
        gecos: ?[*:0]const u8, // Honeywell login info
        dir: ?[*:0]const u8, // home directory
        shell: ?[*:0]const u8, // default shell
        expire: time_t, // account expiration
    },
    .dragonfly, .freebsd => extern struct {
        name: ?[*:0]const u8, // user name
        passwd: ?[*:0]const u8, // encrypted password
        uid: uid_t, // user uid
        gid: gid_t, // user gid
        change: time_t, // password change time
        class: ?[*:0]const u8, // user access class
        gecos: ?[*:0]const u8, // Honeywell login info
        dir: ?[*:0]const u8, // home directory
        shell: ?[*:0]const u8, // default shell
        expire: time_t, // account expiration
        fields: c_int, // internal
    },
    else => void,
};

pub const group = switch (native_os) {
    .linux, .freebsd, .openbsd, .dragonfly, .netbsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        name: ?[*:0]const u8,
        passwd: ?[*:0]const u8,
        gid: gid_t,
        mem: [*:null]?[*:0]const u8,
    },
    else => void,
};

pub const blkcnt_t = switch (native_os) {
    .linux => linux.blkcnt_t,
    .emscripten => emscripten.blkcnt_t,
    .wasi => c_longlong,
    // https://github.com/SerenityOS/serenity/blob/b98f537f117b341788023ab82e0c11ca9ae29a57/Kernel/API/POSIX/sys/types.h#L41
    .serenity => u64,
    else => i64,
};

pub const fd_t = switch (native_os) {
    .linux => linux.fd_t,
    .wasi => wasi.fd_t,
    .windows => windows.HANDLE,
    .serenity => c_int,
    else => i32,
};

pub const ARCH = switch (native_os) {
    .linux => linux.ARCH,
    else => void,
};

// For use with posix.timerfd_create()
// Actually, the parameter for the timerfd_create() function is an integer,
// which means that the developer has to figure out which value is appropriate.
// To make this easier and, above all, safer, because an incorrect value leads
// to a panic, an enum is introduced which only allows the values
// that actually work.
pub const TIMERFD_CLOCK = timerfd_clockid_t;
pub const timerfd_clockid_t = switch (native_os) {
    .freebsd => enum(u32) {
        REALTIME = 0,
        MONOTONIC = 4,
        _,
    },
    .linux => linux.timerfd_clockid_t,
    else => clockid_t,
};

pub const CLOCK = clockid_t;
pub const clockid_t = switch (native_os) {
    .linux, .emscripten => linux.clockid_t,
    .wasi => wasi.clockid_t,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => enum(u32) {
        REALTIME = 0,
        MONOTONIC = 6,
        MONOTONIC_RAW = 4,
        MONOTONIC_RAW_APPROX = 5,
        UPTIME_RAW = 8,
        UPTIME_RAW_APPROX = 9,
        PROCESS_CPUTIME_ID = 12,
        THREAD_CPUTIME_ID = 16,
        _,
    },
    .haiku => enum(i32) {
        /// system-wide monotonic clock (aka system time)
        MONOTONIC = 0,
        /// system-wide real time clock
        REALTIME = -1,
        /// clock measuring the used CPU time of the current process
        PROCESS_CPUTIME_ID = -2,
        /// clock measuring the used CPU time of the current thread
        THREAD_CPUTIME_ID = -3,
    },
    .freebsd => enum(u32) {
        REALTIME = 0,
        VIRTUAL = 1,
        PROF = 2,
        MONOTONIC = 4,
        UPTIME = 5,
        UPTIME_PRECISE = 7,
        UPTIME_FAST = 8,
        REALTIME_PRECISE = 9,
        REALTIME_FAST = 10,
        MONOTONIC_PRECISE = 11,
        MONOTONIC_FAST = 12,
        SECOND = 13,
        THREAD_CPUTIME_ID = 14,
        PROCESS_CPUTIME_ID = 15,
    },
    .illumos => enum(u32) {
        VIRTUAL = 1,
        THREAD_CPUTIME_ID = 2,
        REALTIME = 3,
        MONOTONIC = 4,
        PROCESS_CPUTIME_ID = 5,
    },
    .netbsd => enum(u32) {
        REALTIME = 0,
        VIRTUAL = 1,
        PROF = 2,
        MONOTONIC = 3,
        THREAD_CPUTIME_ID = 0x20000000,
        PROCESS_CPUTIME_ID = 0x40000000,
    },
    .dragonfly => enum(u32) {
        REALTIME = 0,
        VIRTUAL = 1,
        PROF = 2,
        MONOTONIC = 4,
        UPTIME = 5,
        UPTIME_PRECISE = 7,
        UPTIME_FAST = 8,
        REALTIME_PRECISE = 9,
        REALTIME_FAST = 10,
        MONOTONIC_PRECISE = 11,
        MONOTONIC_FAST = 12,
        SECOND = 13,
        THREAD_CPUTIME_ID = 14,
        PROCESS_CPUTIME_ID = 15,
    },
    .openbsd => enum(u32) {
        REALTIME = 0,
        PROCESS_CPUTIME_ID = 2,
        MONOTONIC = 3,
        THREAD_CPUTIME_ID = 4,
    },
    // https://github.com/SerenityOS/serenity/blob/0a78056453578c18e0a04a0b45ebfb1c96d59005/Kernel/API/POSIX/time.h#L24-L36
    .serenity => enum(c_int) {
        REALTIME = 0,
        MONOTONIC = 1,
        MONOTONIC_RAW = 2,
        REALTIME_COARSE = 3,
        MONOTONIC_COARSE = 4,
    },
    else => void,
};
pub const CPU_COUNT = switch (native_os) {
    .linux => linux.CPU_COUNT,
    .emscripten => emscripten.CPU_COUNT,
    else => void,
};
pub const E = switch (native_os) {
    .linux => linux.E,
    .emscripten => emscripten.E,
    .wasi => wasi.errno_t,
    .windows => enum(u16) {
        /// No error occurred.
        SUCCESS = 0,
        PERM = 1,
        NOENT = 2,
        SRCH = 3,
        INTR = 4,
        IO = 5,
        NXIO = 6,
        @"2BIG" = 7,
        NOEXEC = 8,
        BADF = 9,
        CHILD = 10,
        AGAIN = 11,
        NOMEM = 12,
        ACCES = 13,
        FAULT = 14,
        BUSY = 16,
        EXIST = 17,
        XDEV = 18,
        NODEV = 19,
        NOTDIR = 20,
        ISDIR = 21,
        NFILE = 23,
        MFILE = 24,
        NOTTY = 25,
        FBIG = 27,
        NOSPC = 28,
        SPIPE = 29,
        ROFS = 30,
        MLINK = 31,
        PIPE = 32,
        DOM = 33,
        /// Also means `DEADLOCK`.
        DEADLK = 36,
        NAMETOOLONG = 38,
        NOLCK = 39,
        NOSYS = 40,
        NOTEMPTY = 41,

        INVAL = 22,
        RANGE = 34,
        ILSEQ = 42,

        // POSIX Supplement
        ADDRINUSE = 100,
        ADDRNOTAVAIL = 101,
        AFNOSUPPORT = 102,
        ALREADY = 103,
        BADMSG = 104,
        CANCELED = 105,
        CONNABORTED = 106,
        CONNREFUSED = 107,
        CONNRESET = 108,
        DESTADDRREQ = 109,
        HOSTUNREACH = 110,
        IDRM = 111,
        INPROGRESS = 112,
        ISCONN = 113,
        LOOP = 114,
        MSGSIZE = 115,
        NETDOWN = 116,
        NETRESET = 117,
        NETUNREACH = 118,
        NOBUFS = 119,
        NODATA = 120,
        NOLINK = 121,
        NOMSG = 122,
        NOPROTOOPT = 123,
        NOSR = 124,
        NOSTR = 125,
        NOTCONN = 126,
        NOTRECOVERABLE = 127,
        NOTSOCK = 128,
        NOTSUP = 129,
        OPNOTSUPP = 130,
        OTHER = 131,
        OVERFLOW = 132,
        OWNERDEAD = 133,
        PROTO = 134,
        PROTONOSUPPORT = 135,
        PROTOTYPE = 136,
        TIME = 137,
        TIMEDOUT = 138,
        TXTBSY = 139,
        WOULDBLOCK = 140,
        DQUOT = 10069,
        _,
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => darwin.E,
    .freebsd => freebsd.E,
    .illumos => enum(u16) {
        /// No error occurred.
        SUCCESS = 0,
        /// Not super-user
        PERM = 1,
        /// No such file or directory
        NOENT = 2,
        /// No such process
        SRCH = 3,
        /// interrupted system call
        INTR = 4,
        /// I/O error
        IO = 5,
        /// No such device or address
        NXIO = 6,
        /// Arg list too long
        @"2BIG" = 7,
        /// Exec format error
        NOEXEC = 8,
        /// Bad file number
        BADF = 9,
        /// No children
        CHILD = 10,
        /// Resource temporarily unavailable.
        /// also: WOULDBLOCK: Operation would block.
        AGAIN = 11,
        /// Not enough core
        NOMEM = 12,
        /// Permission denied
        ACCES = 13,
        /// Bad address
        FAULT = 14,
        /// Block device required
        NOTBLK = 15,
        /// Mount device busy
        BUSY = 16,
        /// File exists
        EXIST = 17,
        /// Cross-device link
        XDEV = 18,
        /// No such device
        NODEV = 19,
        /// Not a directory
        NOTDIR = 20,
        /// Is a directory
        ISDIR = 21,
        /// Invalid argument
        INVAL = 22,
        /// File table overflow
        NFILE = 23,
        /// Too many open files
        MFILE = 24,
        /// Inappropriate ioctl for device
        NOTTY = 25,
        /// Text file busy
        TXTBSY = 26,
        /// File too large
        FBIG = 27,
        /// No space left on device
        NOSPC = 28,
        /// Illegal seek
        SPIPE = 29,
        /// Read only file system
        ROFS = 30,
        /// Too many links
        MLINK = 31,
        /// Broken pipe
        PIPE = 32,
        /// Math arg out of domain of func
        DOM = 33,
        /// Math result not representable
        RANGE = 34,
        /// No message of desired type
        NOMSG = 35,
        /// Identifier removed
        IDRM = 36,
        /// Channel number out of range
        CHRNG = 37,
        /// Level 2 not synchronized
        L2NSYNC = 38,
        /// Level 3 halted
        L3HLT = 39,
        /// Level 3 reset
        L3RST = 40,
        /// Link number out of range
        LNRNG = 41,
        /// Protocol driver not attached
        UNATCH = 42,
        /// No CSI structure available
        NOCSI = 43,
        /// Level 2 halted
        L2HLT = 44,
        /// Deadlock condition.
        DEADLK = 45,
        /// No record locks available.
        NOLCK = 46,
        /// Operation canceled
        CANCELED = 47,
        /// Operation not supported
        NOTSUP = 48,

        // Filesystem Quotas
        /// Disc quota exceeded
        DQUOT = 49,

        // Convergent Error Returns
        /// invalid exchange
        BADE = 50,
        /// invalid request descriptor
        BADR = 51,
        /// exchange full
        XFULL = 52,
        /// no anode
        NOANO = 53,
        /// invalid request code
        BADRQC = 54,
        /// invalid slot
        BADSLT = 55,
        /// file locking deadlock error
        DEADLOCK = 56,
        /// bad font file fmt
        BFONT = 57,

        // Interprocess Robust Locks
        /// process died with the lock
        OWNERDEAD = 58,
        /// lock is not recoverable
        NOTRECOVERABLE = 59,
        /// locked lock was unmapped
        LOCKUNMAPPED = 72,
        /// Facility is not active
        NOTACTIVE = 73,
        /// multihop attempted
        MULTIHOP = 74,
        /// trying to read unreadable message
        BADMSG = 77,
        /// path name is too long
        NAMETOOLONG = 78,
        /// value too large to be stored in data type
        OVERFLOW = 79,
        /// given log. name not unique
        NOTUNIQ = 80,
        /// f.d. invalid for this operation
        BADFD = 81,
        /// Remote address changed
        REMCHG = 82,

        // Stream Problems
        /// Device not a stream
        NOSTR = 60,
        /// no data (for no delay io)
        NODATA = 61,
        /// timer expired
        TIME = 62,
        /// out of streams resources
        NOSR = 63,
        /// Machine is not on the network
        NONET = 64,
        /// Package not installed
        NOPKG = 65,
        /// The object is remote
        REMOTE = 66,
        /// the link has been severed
        NOLINK = 67,
        /// advertise error
        ADV = 68,
        /// srmount error
        SRMNT = 69,
        /// Communication error on send
        COMM = 70,
        /// Protocol error
        PROTO = 71,

        // Shared Library Problems
        /// Can't access a needed shared lib.
        LIBACC = 83,
        /// Accessing a corrupted shared lib.
        LIBBAD = 84,
        /// .lib section in a.out corrupted.
        LIBSCN = 85,
        /// Attempting to link in too many libs.
        LIBMAX = 86,
        /// Attempting to exec a shared library.
        LIBEXEC = 87,
        /// Illegal byte sequence.
        ILSEQ = 88,
        /// Unsupported file system operation
        NOSYS = 89,
        /// Symbolic link loop
        LOOP = 90,
        /// Restartable system call
        RESTART = 91,
        /// if pipe/FIFO, don't sleep in stream head
        STRPIPE = 92,
        /// directory not empty
        NOTEMPTY = 93,
        /// Too many users (for UFS)
        USERS = 94,

        // BSD Networking Software
        // Argument Errors
        /// Socket operation on non-socket
        NOTSOCK = 95,
        /// Destination address required
        DESTADDRREQ = 96,
        /// Message too long
        MSGSIZE = 97,
        /// Protocol wrong type for socket
        PROTOTYPE = 98,
        /// Protocol not available
        NOPROTOOPT = 99,
        /// Protocol not supported
        PROTONOSUPPORT = 120,
        /// Socket type not supported
        SOCKTNOSUPPORT = 121,
        /// Operation not supported on socket
        OPNOTSUPP = 122,
        /// Protocol family not supported
        PFNOSUPPORT = 123,
        /// Address family not supported by
        AFNOSUPPORT = 124,
        /// Address already in use
        ADDRINUSE = 125,
        /// Can't assign requested address
        ADDRNOTAVAIL = 126,

        // Operational Errors
        /// Network is down
        NETDOWN = 127,
        /// Network is unreachable
        NETUNREACH = 128,
        /// Network dropped connection because
        NETRESET = 129,
        /// Software caused connection abort
        CONNABORTED = 130,
        /// Connection reset by peer
        CONNRESET = 131,
        /// No buffer space available
        NOBUFS = 132,
        /// Socket is already connected
        ISCONN = 133,
        /// Socket is not connected
        NOTCONN = 134,
        /// Can't send after socket shutdown
        SHUTDOWN = 143,
        /// Too many references: can't splice
        TOOMANYREFS = 144,
        /// Connection timed out
        TIMEDOUT = 145,
        /// Connection refused
        CONNREFUSED = 146,
        /// Host is down
        HOSTDOWN = 147,
        /// No route to host
        HOSTUNREACH = 148,
        /// operation already in progress
        ALREADY = 149,
        /// operation now in progress
        INPROGRESS = 150,

        // SUN Network File System
        /// Stale NFS file handle
        STALE = 151,

        _,
    },
    .netbsd => netbsd.E,
    .dragonfly => dragonfly.E,
    .haiku => haiku.E,
    .openbsd => openbsd.E,
    // https://github.com/SerenityOS/serenity/blob/dd59fe35c7e5bbaf6b6b3acb3f9edc56619d4b66/Kernel/API/POSIX/errno.h
    .serenity => enum(c_int) {
        SUCCESS = 0,
        PERM = 1,
        NOENT = 2,
        SRCH = 3,
        INTR = 4,
        IO = 5,
        NXIO = 6,
        @"2BIG" = 7,
        NOEXEC = 8,
        BADF = 9,
        CHILD = 10,
        AGAIN = 11,
        NOMEM = 12,
        ACCES = 13,
        FAULT = 14,
        NOTBLK = 15,
        BUSY = 16,
        EXIST = 17,
        XDEV = 18,
        NODEV = 19,
        NOTDIR = 20,
        ISDIR = 21,
        INVAL = 22,
        NFILE = 23,
        MFILE = 24,
        NOTTY = 25,
        TXTBSY = 26,
        FBIG = 27,
        NOSPC = 28,
        SPIPE = 29,
        ROFS = 30,
        MLINK = 31,
        PIPE = 32,
        RANGE = 33,
        NAMETOOLONG = 34,
        LOOP = 35,
        OVERFLOW = 36,
        OPNOTSUPP = 37,
        NOSYS = 38,
        NOTIMPL = 39,
        AFNOSUPPORT = 40,
        NOTSOCK = 41,
        ADDRINUSE = 42,
        NOTEMPTY = 43,
        DOM = 44,
        CONNREFUSED = 45,
        HOSTDOWN = 46,
        ADDRNOTAVAIL = 47,
        ISCONN = 48,
        CONNABORTED = 49,
        ALREADY = 50,
        CONNRESET = 51,
        DESTADDRREQ = 52,
        HOSTUNREACH = 53,
        ILSEQ = 54,
        MSGSIZE = 55,
        NETDOWN = 56,
        NETUNREACH = 57,
        NETRESET = 58,
        NOBUFS = 59,
        NOLCK = 60,
        NOMSG = 61,
        NOPROTOOPT = 62,
        NOTCONN = 63,
        SHUTDOWN = 64,
        TOOMANYREFS = 65,
        SOCKTNOSUPPORT = 66,
        PROTONOSUPPORT = 67,
        DEADLK = 68,
        TIMEDOUT = 69,
        PROTOTYPE = 70,
        INPROGRESS = 71,
        NOTHREAD = 72,
        PROTO = 73,
        NOTSUP = 74,
        PFNOSUPPORT = 75,
        DIRINTOSELF = 76,
        DQUOT = 77,
        NOTRECOVERABLE = 78,
        CANCELED = 79,
        PROMISEVIOLATION = 80,
        STALE = 81,
        SRCNOTFOUND = 82,
        _,
    },
    else => void,
};
pub const Elf_Symndx = switch (native_os) {
    .linux => linux.Elf_Symndx,
    else => void,
};
/// Command flags for fcntl(2).
pub const F = switch (native_os) {
    .linux => linux.F,
    .emscripten => emscripten.F,
    .wasi => struct {
        // Match `F_*` constants from lib/libc/include/wasm-wasi-musl/__header_fcntl.h
        pub const GETFD = 1;
        pub const SETFD = 2;
        pub const GETFL = 3;
        pub const SETFL = 4;
    },
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => struct {
        /// duplicate file descriptor
        pub const DUPFD = 0;
        /// get file descriptor flags
        pub const GETFD = 1;
        /// set file descriptor flags
        pub const SETFD = 2;
        /// get file status flags
        pub const GETFL = 3;
        /// set file status flags
        pub const SETFL = 4;
        /// get SIGIO/SIGURG proc/pgrp
        pub const GETOWN = 5;
        /// set SIGIO/SIGURG proc/pgrp
        pub const SETOWN = 6;
        /// get record locking information
        pub const GETLK = 7;
        /// set record locking information
        pub const SETLK = 8;
        /// F.SETLK; wait if blocked
        pub const SETLKW = 9;
        /// F.SETLK; wait if blocked, return on timeout
        pub const SETLKWTIMEOUT = 10;
        pub const FLUSH_DATA = 40;
        /// Used for regression test
        pub const CHKCLEAN = 41;
        /// Preallocate storage
        pub const PREALLOCATE = 42;
        /// Truncate a file without zeroing space
        pub const SETSIZE = 43;
        /// Issue an advisory read async with no copy to user
        pub const RDADVISE = 44;
        /// turn read ahead off/on for this fd
        pub const RDAHEAD = 45;
        /// turn data caching off/on for this fd
        pub const NOCACHE = 48;
        /// file offset to device offset
        pub const LOG2PHYS = 49;
        /// return the full path of the fd
        pub const GETPATH = 50;
        /// fsync + ask the drive to flush to the media
        pub const FULLFSYNC = 51;
        /// find which component (if any) is a package
        pub const PATHPKG_CHECK = 52;
        /// "freeze" all fs operations
        pub const FREEZE_FS = 53;
        /// "thaw" all fs operations
        pub const THAW_FS = 54;
        /// turn data caching off/on (globally) for this file
        pub const GLOBAL_NOCACHE = 55;
        /// add detached signatures
        pub const ADDSIGS = 59;
        /// add signature from same file (used by dyld for shared libs)
        pub const ADDFILESIGS = 61;
        /// used in conjunction with F.NOCACHE to indicate that DIRECT, synchronous writes
        /// should not be used (i.e. its ok to temporarily create cached pages)
        pub const NODIRECT = 62;
        /// Get the protection class of a file from the EA, returns int
        pub const GETPROTECTIONCLASS = 63;
        /// Set the protection class of a file for the EA, requires int
        pub const SETPROTECTIONCLASS = 64;
        /// file offset to device offset, extended
        pub const LOG2PHYS_EXT = 65;
        /// get record locking information, per-process
        pub const GETLKPID = 66;
        /// Mark the file as being the backing store for another filesystem
        pub const SETBACKINGSTORE = 70;
        /// return the full path of the FD, but error in specific mtmd circumstances
        pub const GETPATH_MTMINFO = 71;
        /// Returns the code directory, with associated hashes, to the caller
        pub const GETCODEDIR = 72;
        /// No SIGPIPE generated on EPIPE
        pub const SETNOSIGPIPE = 73;
        /// Status of SIGPIPE for this fd
        pub const GETNOSIGPIPE = 74;
        /// For some cases, we need to rewrap the key for AKS/MKB
        pub const TRANSCODEKEY = 75;
        /// file being written to a by single writer... if throttling enabled, writes
        /// may be broken into smaller chunks with throttling in between
        pub const SINGLE_WRITER = 76;
        /// Get the protection version number for this filesystem
        pub const GETPROTECTIONLEVEL = 77;
        /// Add detached code signatures (used by dyld for shared libs)
        pub const FINDSIGS = 78;
        /// Add signature from same file, only if it is signed by Apple (used by dyld for simulator)
        pub const ADDFILESIGS_FOR_DYLD_SIM = 83;
        /// fsync + issue barrier to drive
        pub const BARRIERFSYNC = 85;
        /// Add signature from same file, return end offset in structure on success
        pub const ADDFILESIGS_RETURN = 97;
        /// Check if Library Validation allows this Mach-O file to be mapped into the calling process
        pub const CHECK_LV = 98;
        /// Deallocate a range of the file
        pub const PUNCHHOLE = 99;
        /// Trim an active file
        pub const TRIM_ACTIVE_FILE = 100;
        /// mark the dup with FD_CLOEXEC
        pub const DUPFD_CLOEXEC = 67;
        /// shared or read lock
        pub const RDLCK = 1;
        /// unlock
        pub const UNLCK = 2;
        /// exclusive or write lock
        pub const WRLCK = 3;
    },
    .freebsd => struct {
        /// Duplicate file descriptor.
        pub const DUPFD = 0;
        /// Get file descriptor flags.
        pub const GETFD = 1;
        /// Set file descriptor flags.
        pub const SETFD = 2;
        /// Get file status flags.
        pub const GETFL = 3;
        /// Set file status flags.
        pub const SETFL = 4;

        /// Get SIGIO/SIGURG proc/pgrrp.
        pub const GETOWN = 5;
        /// Set SIGIO/SIGURG proc/pgrrp.
        pub const SETOWN = 6;

        /// Get record locking information.
        pub const GETLK = 11;
        /// Set record locking information.
        pub const SETLK = 12;
        /// Set record locking information and wait if blocked.
        pub const SETLKW = 13;

        /// Debugging support for remote locks.
        pub const SETLK_REMOTE = 14;
        /// Read ahead.
        pub const READAHEAD = 15;

        /// DUPFD with FD_CLOEXEC set.
        pub const DUPFD_CLOEXEC = 17;
        /// DUP2FD with FD_CLOEXEC set.
        pub const DUP2FD_CLOEXEC = 18;

        pub const ADD_SEALS = 19;
        pub const GET_SEALS = 20;
        /// Return `kinfo_file` for a file descriptor.
        pub const KINFO = 22;

        // Seals (ADD_SEALS, GET_SEALS)
        /// Prevent adding sealings.
        pub const SEAL_SEAL = 0x0001;
        /// May not shrink
        pub const SEAL_SHRINK = 0x0002;
        /// May not grow.
        pub const SEAL_GROW = 0x0004;
        /// May not write.
        pub const SEAL_WRITE = 0x0008;

        // Record locking flags (GETLK, SETLK, SETLKW).
        /// Shared or read lock.
        pub const RDLCK = 1;
        /// Unlock.
        pub const UNLCK = 2;
        /// Exclusive or write lock.
        pub const WRLCK = 3;
        /// Purge locks for a given system ID.
        pub const UNLCKSYS = 4;
        /// Cancel an async lock request.
        pub const CANCEL = 5;

        pub const SETOWN_EX = 15;
        pub const GETOWN_EX = 16;

        pub const GETOWNER_UIDS = 17;
    },
    .illumos => struct {
        /// Unlock a previously locked region
        pub const ULOCK = 0;
        /// Lock a region for exclusive use
        pub const LOCK = 1;
        /// Test and lock a region for exclusive use
        pub const TLOCK = 2;
        /// Test a region for other processes locks
        pub const TEST = 3;

        /// Duplicate fildes
        pub const DUPFD = 0;
        /// Get fildes flags
        pub const GETFD = 1;
        /// Set fildes flags
        pub const SETFD = 2;
        /// Get file flags
        pub const GETFL = 3;
        /// Get file flags including open-only flags
        pub const GETXFL = 45;
        /// Set file flags
        pub const SETFL = 4;

        /// Unused
        pub const CHKFL = 8;
        /// Duplicate fildes at third arg
        pub const DUP2FD = 9;
        /// Like DUP2FD with O_CLOEXEC set EINVAL is fildes matches arg1
        pub const DUP2FD_CLOEXEC = 36;
        /// Like DUPFD with O_CLOEXEC set
        pub const DUPFD_CLOEXEC = 37;

        /// Is the file desc. a stream ?
        pub const ISSTREAM = 13;
        /// Turn on private access to file
        pub const PRIV = 15;
        /// Turn off private access to file
        pub const NPRIV = 16;
        /// UFS quota call
        pub const QUOTACTL = 17;
        /// Get number of BLKSIZE blocks allocated
        pub const BLOCKS = 18;
        /// Get optimal I/O block size
        pub const BLKSIZE = 19;
        /// Get owner (socket emulation)
        pub const GETOWN = 23;
        /// Set owner (socket emulation)
        pub const SETOWN = 24;
        /// Object reuse revoke access to file desc.
        pub const REVOKE = 25;
        /// Does vp have NFS locks private to lock manager
        pub const HASREMOTELOCKS = 26;

        /// Set file lock
        pub const SETLK = 6;
        /// Set file lock and wait
        pub const SETLKW = 7;
        /// Allocate file space
        pub const ALLOCSP = 10;
        /// Free file space
        pub const FREESP = 11;
        /// Get file lock
        pub const GETLK = 14;
        /// Get file lock owned by file
        pub const OFD_GETLK = 47;
        /// Set file lock owned by file
        pub const OFD_SETLK = 48;
        /// Set file lock owned by file and wait
        pub const OFD_SETLKW = 49;
        /// Set a file share reservation
        pub const SHARE = 40;
        /// Remove a file share reservation
        pub const UNSHARE = 41;
        /// Create Poison FD
        pub const BADFD = 46;

        /// Read lock
        pub const RDLCK = 1;
        /// Write lock
        pub const WRLCK = 2;
        /// Remove lock(s)
        pub const UNLCK = 3;
        /// remove remote locks for a given system
        pub const UNLKSYS = 4;

        // f_access values
        /// Read-only share access
        pub const RDACC = 0x1;
        /// Write-only share access
        pub const WRACC = 0x2;
        /// Read-Write share access
        pub const RWACC = 0x3;

        // f_deny values
        /// Don't deny others access
        pub const NODNY = 0x0;
        /// Deny others read share access
        pub const RDDNY = 0x1;
        /// Deny others write share access
        pub const WRDNY = 0x2;
        /// Deny others read or write share access
        pub const RWDNY = 0x3;
        /// private flag: Deny delete share access
        pub const RMDNY = 0x4;
    },
    .netbsd => struct {
        pub const DUPFD = 0;
        pub const GETFD = 1;
        pub const SETFD = 2;
        pub const GETFL = 3;
        pub const SETFL = 4;
        pub const GETOWN = 5;
        pub const SETOWN = 6;
        pub const GETLK = 7;
        pub const SETLK = 8;
        pub const SETLKW = 9;
        pub const CLOSEM = 10;
        pub const MAXFD = 11;
        pub const DUPFD_CLOEXEC = 12;
        pub const GETNOSIGPIPE = 13;
        pub const SETNOSIGPIPE = 14;
        pub const GETPATH = 15;

        pub const RDLCK = 1;
        pub const WRLCK = 3;
        pub const UNLCK = 2;
    },
    .dragonfly => struct {
        pub const ULOCK = 0;
        pub const LOCK = 1;
        pub const TLOCK = 2;
        pub const TEST = 3;

        pub const DUPFD = 0;
        pub const GETFD = 1;
        pub const RDLCK = 1;
        pub const SETFD = 2;
        pub const UNLCK = 2;
        pub const WRLCK = 3;
        pub const GETFL = 3;
        pub const SETFL = 4;
        pub const GETOWN = 5;
        pub const SETOWN = 6;
        pub const GETLK = 7;
        pub const SETLK = 8;
        pub const SETLKW = 9;
        pub const DUP2FD = 10;
        pub const DUPFD_CLOEXEC = 17;
        pub const DUP2FD_CLOEXEC = 18;
        pub const GETPATH = 19;
    },
    .haiku => struct {
        pub const DUPFD = 0x0001;
        pub const GETFD = 0x0002;
        pub const SETFD = 0x0004;
        pub const GETFL = 0x0008;
        pub const SETFL = 0x0010;

        pub const GETLK = 0x0020;
        pub const SETLK = 0x0080;
        pub const SETLKW = 0x0100;
        pub const DUPFD_CLOEXEC = 0x0200;

        pub const RDLCK = 0x0040;
        pub const UNLCK = 0x0200;
        pub const WRLCK = 0x0400;
    },
    .openbsd => struct {
        pub const DUPFD = 0;
        pub const GETFD = 1;
        pub const SETFD = 2;
        pub const GETFL = 3;
        pub const SETFL = 4;

        pub const GETOWN = 5;
        pub const SETOWN = 6;

        pub const GETLK = 7;
        pub const SETLK = 8;
        pub const SETLKW = 9;

        pub const RDLCK = 1;
        pub const UNLCK = 2;
        pub const WRLCK = 3;
    },
    .serenity => struct {
        // https://github.com/SerenityOS/serenity/blob/2808b0376406a40e31293bb3bcb9170374e90506/Kernel/API/POSIX/fcntl.h#L15-L24
        pub const DUPFD = 0;
        pub const GETFD = 1;
        pub const SETFD = 2;
        pub const GETFL = 3;
        pub const SETFL = 4;
        pub const ISTTY = 5;
        pub const GETLK = 6;
        pub const SETLK = 7;
        pub const SETLKW = 8;
        pub const DUPFD_CLOEXEC = 9;

        // https://github.com/SerenityOS/serenity/blob/2808b0376406a40e31293bb3bcb9170374e90506/Kernel/API/POSIX/fcntl.h#L45-L47
        pub const RDLCK = 0;
        pub const WRLCK = 1;
        pub const UNLCK = 2;
    },
    else => void,
};
pub const FD_CLOEXEC = switch (native_os) {
    .linux => linux.FD_CLOEXEC,
    .emscripten => emscripten.FD_CLOEXEC,
    else => 1,
};

/// Test for existence of file.
pub const F_OK = switch (native_os) {
    .linux => linux.F_OK,
    .emscripten => emscripten.F_OK,
    else => 0,
};
/// Test for execute or search permission.
pub const X_OK = switch (native_os) {
    .linux => linux.X_OK,
    .emscripten => emscripten.X_OK,
    else => 1,
};
/// Test for write permission.
pub const W_OK = switch (native_os) {
    .linux => linux.W_OK,
    .emscripten => emscripten.W_OK,
    else => 2,
};
/// Test for read permission.
pub const R_OK = switch (native_os) {
    .linux => linux.R_OK,
    .emscripten => emscripten.R_OK,
    else => 4,
};

pub const Flock = switch (native_os) {
    .linux => linux.Flock,
    .emscripten => emscripten.Flock,
    .openbsd, .dragonfly, .netbsd, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => extern struct {
        start: off_t,
        len: off_t,
        pid: pid_t,
        type: i16,
        whence: i16,
    },
    .freebsd => extern struct {
        /// Starting offset.
        start: off_t,
        /// Number of consecutive bytes to be locked.
        /// A value of 0 means to the end of the file.
        len: off_t,
        /// Lock owner.
        pid: pid_t,
        /// Lock type.
        type: i16,
        /// Type of the start member.
        whence: i16,
        /// Remote system id or zero for local.
        sysid: i32,
    },
    .illumos => extern struct {
        type: c_short,
        whence: c_short,
        start: off_t,
        // len == 0 means until end of file.
        len: off_t,
        sysid: c_int,
        pid: pid_t,
        __pad: [4]c_long,
    },
    .haiku => extern struct {
        type: i16,
        whence: i16,
        start: off_t,
        len: off_t,
        pid: pid_t,
    },
    // https://github.com/SerenityOS/serenity/blob/2808b0376406a40e31293bb3bcb9170374e90506/Kernel/API/POSIX/fcntl.h#L54-L60
    .serenity => extern struct {
        type: c_short,
        whence: c_short,
        start: off_t,
        len: off_t,
        pid: pid_t,
    },
    else => void,
};
pub const HOST_NAME_MAX = switch (native_os) {
    .linux => linux.HOST_NAME_MAX,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => 72,
    .openbsd, .haiku, .dragonfly, .netbsd, .illumos, .freebsd => 255,
    // https://github.com/SerenityOS/serenity/blob/c87557e9c1865fa1a6440de34ff6ce6fc858a2b7/Kernel/API/POSIX/sys/limits.h#L22
    .serenity => 64,
    else => {},
};
pub const IOV_MAX = switch (native_os) {
    .linux => linux.IOV_MAX,
    .emscripten => emscripten.IOV_MAX,
    // https://github.com/SerenityOS/serenity/blob/098af0f846a87b651731780ff48420205fd33754/Kernel/API/POSIX/sys/uio.h#L16
    .openbsd, .haiku, .illumos, .wasi, .serenity => 1024,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => 16,
    .dragonfly, .netbsd, .freebsd => KERN.IOV_MAX,
    else => {},
};
pub const CTL = switch (native_os) {
    .freebsd => struct {
        pub const KERN = 1;
        pub const DEBUG = 5;
    },
    .netbsd => struct {
        pub const KERN = 1;
        pub const DEBUG = 5;
    },
    .dragonfly => struct {
        pub const UNSPEC = 0;
        pub const KERN = 1;
        pub const VM = 2;
        pub const VFS = 3;
        pub const NET = 4;
        pub const DEBUG = 5;
        pub const HW = 6;
        pub const MACHDEP = 7;
        pub const USER = 8;
        pub const LWKT = 10;
        pub const MAXID = 11;
        pub const MAXNAME = 12;
    },
    .openbsd => struct {
        pub const UNSPEC = 0;
        pub const KERN = 1;
        pub const VM = 2;
        pub const FS = 3;
        pub const NET = 4;
        pub const DEBUG = 5;
        pub const HW = 6;
        pub const MACHDEP = 7;

        pub const DDB = 9;
        pub const VFS = 10;
    },
    else => void,
};
pub const KERN = switch (native_os) {
    .freebsd => struct {
        /// struct: process entries
        pub const PROC = 14;
        /// path to executable
        pub const PROC_PATHNAME = 12;
        /// file descriptors for process
        pub const PROC_FILEDESC = 33;
        pub const IOV_MAX = 35;
    },
    .netbsd => struct {
        /// struct: process argv/env
        pub const PROC_ARGS = 48;
        /// path to executable
        pub const PROC_PATHNAME = 5;
        pub const IOV_MAX = 38;
    },
    .dragonfly => struct {
        pub const PROC_ALL = 0;
        pub const OSTYPE = 1;
        pub const PROC_PID = 1;
        pub const OSRELEASE = 2;
        pub const PROC_PGRP = 2;
        pub const OSREV = 3;
        pub const PROC_SESSION = 3;
        pub const VERSION = 4;
        pub const PROC_TTY = 4;
        pub const MAXVNODES = 5;
        pub const PROC_UID = 5;
        pub const MAXPROC = 6;
        pub const PROC_RUID = 6;
        pub const MAXFILES = 7;
        pub const PROC_ARGS = 7;
        pub const ARGMAX = 8;
        pub const PROC_CWD = 8;
        pub const PROC_PATHNAME = 9;
        pub const SECURELVL = 9;
        pub const PROC_SIGTRAMP = 10;
        pub const HOSTNAME = 10;
        pub const HOSTID = 11;
        pub const CLOCKRATE = 12;
        pub const VNODE = 13;
        pub const PROC = 14;
        pub const FILE = 15;
        pub const PROC_FLAGMASK = 16;
        pub const PROF = 16;
        pub const PROC_FLAG_LWP = 16;
       
```
