```
nst nvptx = @import("Target/nvptx.zig");
pub const or1k = @import("Target/generic.zig");
pub const powerpc = @import("Target/powerpc.zig");
pub const propeller = @import("Target/propeller.zig");
pub const riscv = @import("Target/riscv.zig");
pub const s390x = @import("Target/s390x.zig");
pub const sh = @import("Target/generic.zig");
pub const sparc = @import("Target/sparc.zig");
pub const spirv = @import("Target/spirv.zig");
pub const ve = @import("Target/ve.zig");
pub const wasm = @import("Target/wasm.zig");
pub const x86 = @import("Target/x86.zig");
pub const xcore = @import("Target/xcore.zig");
pub const xtensa = @import("Target/xtensa.zig");

pub const Abi = enum {
    none,
    gnu,
    gnuabin32,
    gnuabi64,
    gnueabi,
    gnueabihf,
    gnuf32,
    gnusf,
    gnux32,
    eabi,
    eabihf,
    ilp32,
    android,
    androideabi,
    musl,
    muslabin32,
    muslabi64,
    musleabi,
    musleabihf,
    muslf32,
    muslsf,
    muslx32,
    msvc,
    itanium,
    simulator,
    ohos,
    ohoseabi,

    // LLVM tags deliberately omitted:
    // - amplification
    // - anyhit
    // - callable
    // - closesthit
    // - compute
    // - coreclr
    // - domain
    // - geometry
    // - gnueabit64
    // - gnueabihft64
    // - gnuf64
    // - gnut64
    // - hull
    // - intersection
    // - library
    // - llvm
    // - mesh
    // - miss
    // - mlibc
    // - mtia
    // - pauthtest
    // - pixel
    // - raygeneration
    // - rootsignature
    // - vertex

    pub fn default(arch: Cpu.Arch, os_tag: Os.Tag) Abi {
        return switch (os_tag) {
            .freestanding, .other => switch (arch) {
                // Soft float is usually a sane default for freestanding.
                .arm,
                .armeb,
                .csky,
                .hppa,
                .mips,
                .mipsel,
                .powerpc,
                .powerpcle,
                .sh,
                .sheb,
                .thumb,
                .thumbeb,
                => .eabi,
                else => .none,
            },
            .haiku => switch (arch) {
                .arm,
                .powerpc,
                => .eabihf,
                else => .none,
            },
            .hurd => .gnu,
            .linux => switch (arch) {
                .arm,
                .armeb,
                .powerpc,
                .powerpcle,
                .thumb,
                .thumbeb,
                => .musleabihf,
                .mips,
                .mipsel,
                => .musleabi,
                .mips64,
                .mips64el,
                => .muslabi64,

                // No musl support.
                .arc,
                .arceb,
                => .gnu,
                .csky,
                => .gnueabi,
                .hppa,
                .sh,
                .sheb,
                => .gnueabihf,

                // No glibc or musl support.
                .xtensa,
                .xtensaeb,
                => .none,

                else => .musl,
            },
            .rtems => switch (arch) {
                .arm,
                .armeb,
                .thumb,
                .thumbeb,
                .mips,
                .mipsel,
                => .eabi,
                .powerpc,
                => .eabihf,
                else => .none,
            },
            .freebsd => switch (arch) {
                .arm,
                => .eabihf,
                else => .none,
            },
            .netbsd => switch (arch) {
                .arm,
                .armeb,
                .powerpc,
                => .eabihf,
                // Soft float tends to be more common for MIPS.
                .mips,
                .mipsel,
                => .eabi,
                else => .none,
            },
            .openbsd => switch (arch) {
                .arm,
                => .eabi,
                .powerpc,
                => .eabihf,
                else => .none,
            },
            .windows => .gnu,
            .uefi => .msvc,
            .@"3ds" => .eabihf,
            .psp => .eabihf,
            .vita => .eabihf,
            .wasi, .emscripten => .musl,

            .contiki,
            .fuchsia,
            .hermit,
            .illumos,
            .managarm,
            .plan9,
            .serenity,
            .dragonfly,
            .driverkit,
            .ios,
            .maccatalyst,
            .macos,
            .tvos,
            .visionos,
            .watchos,
            .ps3,
            .ps4,
            .ps5,
            .amdhsa,
            .amdpal,
            .cuda,
            .mesa3d,
            .nvcl,
            .opencl,
            .opengl,
            .vulkan,
            => .none,
        };
    }

    pub inline fn isGnu(abi: Abi) bool {
        return switch (abi) {
            .gnu,
            .gnuabin32,
            .gnuabi64,
            .gnueabi,
            .gnueabihf,
            .gnuf32,
            .gnusf,
            .gnux32,
            => true,
            else => false,
        };
    }

    pub inline fn isMusl(abi: Abi) bool {
        return switch (abi) {
            .musl,
            .muslabin32,
            .muslabi64,
            .musleabi,
            .musleabihf,
            .muslf32,
            .muslsf,
            .muslx32,
            => true,
            else => abi.isOpenHarmony(),
        };
    }

    pub inline fn isOpenHarmony(abi: Abi) bool {
        return switch (abi) {
            .ohos, .ohoseabi => true,
            else => false,
        };
    }

    pub inline fn isAndroid(abi: Abi) bool {
        return switch (abi) {
            .android, .androideabi => true,
            else => false,
        };
    }

    pub const Float = enum {
        hard,
        soft,
    };

    pub inline fn float(abi: Abi) Float {
        return switch (abi) {
            .androideabi,
            .eabi,
            .gnueabi,
            .musleabi,
            .gnusf,
            .ohoseabi,
            => .soft,
            else => .hard,
        };
    }
};

pub const ObjectFormat = enum {
    /// C source code.
    c,
    /// The Common Object File Format used by Windows and UEFI.
    coff,
    /// The Executable and Linkable Format used by many Unixes.
    elf,
    /// The Intel HEX format for storing binary code in ASCII text.
    hex,
    /// The Mach object format used by macOS and other Apple platforms.
    macho,
    /// The a.out format used by Plan 9 from Bell Labs.
    plan9,
    /// Machine code with no metadata.
    raw,
    /// The Khronos Group's Standard Portable Intermediate Representation V.
    spirv,
    /// The WebAssembly binary format.
    wasm,

    // LLVM tags deliberately omitted:
    // - dxcontainer

    pub fn fileExt(of: ObjectFormat, arch: Cpu.Arch) [:0]const u8 {
        return switch (of) {
            .c => ".c",
            .coff => ".obj",
            .elf, .macho, .wasm => ".o",
            .hex => ".ihex",
            .plan9 => arch.plan9Ext(),
            .raw => ".bin",
            .spirv => ".spv",
        };
    }

    pub fn default(os_tag: Os.Tag, arch: Cpu.Arch) ObjectFormat {
        return switch (os_tag) {
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => .macho,
            .plan9 => .plan9,
            .uefi, .windows => .coff,
            else => switch (arch) {
                .spirv32, .spirv64 => .spirv,
                .wasm32, .wasm64 => .wasm,
                else => .elf,
            },
        };
    }
};

pub fn toElfMachine(target: *const Target) std.elf.EM {
    return switch (target.cpu.arch) {
        .aarch64, .aarch64_be => .AARCH64,
        .alpha => .ALPHA,
        .amdgcn => .AMDGPU,
        .arc, .arceb => .ARC_COMPACT2,
        .arm, .armeb, .thumb, .thumbeb => .ARM,
        .avr => .AVR,
        .bpfeb, .bpfel => .BPF,
        .csky => .CSKY,
        .hexagon => .QDSP6,
        .hppa, .hppa64 => .PARISC,
        .kalimba => .CSR_KALIMBA,
        .kvx => .KVX,
        .lanai => .LANAI,
        .loongarch32, .loongarch64 => .LOONGARCH,
        .m68k => .@"68K",
        .microblaze, .microblazeel => .MICROBLAZE,
        .mips, .mips64, .mipsel, .mips64el => .MIPS,
        .msp430 => .MSP430,
        .or1k => .OR1K,
        .powerpc, .powerpcle => .PPC,
        .powerpc64, .powerpc64le => .PPC64,
        .propeller => .PROPELLER,
        .riscv32, .riscv32be, .riscv64, .riscv64be => .RISCV,
        .s390x => .S390,
        .sh, .sheb => .SH,
        .sparc => if (target.cpu.has(.sparc, .v9)) .SPARC32PLUS else .SPARC,
        .sparc64 => .SPARCV9,
        .ve => .VE,
        .x86_16, .x86 => .@"386",
        .x86_64 => .X86_64,
        .xcore => .XCORE,
        .xtensa, .xtensaeb => .XTENSA,

        .nvptx,
        .nvptx64,
        .spirv32,
        .spirv64,
        .wasm32,
        .wasm64,
        => .NONE,
    };
}

pub fn toCoffMachine(target: *const Target) std.coff.IMAGE.FILE.MACHINE {
    return switch (target.cpu.arch) {
        .alpha => .ALPHA64,
        .arm => .ARM,
        .thumb => .ARMNT,
        .aarch64 => .ARM64,
        .loongarch32 => .LOONGARCH32,
        .loongarch64 => .LOONGARCH64,
        .mips => .R3000BE,
        .mipsel => .R3000,
        .mips64el => .R4000,
        .powerpcle => .POWERPC,
        .riscv32 => .RISCV32,
        .riscv64 => .RISCV64,
        .sh => .SH3,
        .x86 => .I386,
        .x86_64 => .AMD64,

        .aarch64_be,
        .amdgcn,
        .arc,
        .arceb,
        .armeb,
        .avr,
        .bpfeb,
        .bpfel,
        .csky,
        .hexagon,
        .hppa,
        .hppa64,
        .kalimba,
        .kvx,
        .lanai,
        .m68k,
        .microblaze,
        .microblazeel,
        .mips64,
        .msp430,
        .nvptx,
        .nvptx64,
        .or1k,
        .powerpc,
        .powerpc64,
        .powerpc64le,
        .propeller,
        .riscv32be,
        .riscv64be,
        .s390x,
        .sheb,
        .sparc,
        .sparc64,
        .spirv32,
        .spirv64,
        .thumbeb,
        .ve,
        .wasm32,
        .wasm64,
        .x86_16,
        .xcore,
        .xtensa,
        .xtensaeb,
        => .UNKNOWN,
    };
}

/// Deprecated; use 'std.zig.Subsystem' instead. To be removed after 0.16.0 is tagged.
pub const SubSystem = std.zig.Subsystem;

pub const Cpu = struct {
    /// Architecture
    arch: Arch,

    /// The CPU model to target. It has a set of features
    /// which are overridden with the `features` field.
    model: *const Model,

    /// An explicit list of the entire CPU feature set. It may differ from the specific CPU model's features.
    features: Feature.Set,

    pub const Feature = struct {
        /// The bit index into `Set`. Has a default value of `undefined` because the canonical
        /// structures are populated via comptime logic.
        index: Set.Index = undefined,

        /// Has a default value of `undefined` because the canonical
        /// structures are populated via comptime logic.
        name: []const u8 = undefined,

        /// If this corresponds to an LLVM-recognized feature, this will be populated;
        /// otherwise null.
        llvm_name: ?[:0]const u8,

        /// Human-friendly UTF-8 text.
        description: []const u8,

        /// Sparse `Set` of features this depends on.
        dependencies: Set,

        /// A bit set of all the features.
        pub const Set = struct {
            ints: [usize_count]usize,

            pub const needed_bit_count = 317;
            pub const byte_count = (needed_bit_count + 7) / 8;
            pub const usize_count = (byte_count + (@sizeOf(usize) - 1)) / @sizeOf(usize);
            pub const Index = std.math.Log2Int(std.meta.Int(.unsigned, usize_count * @bitSizeOf(usize)));
            pub const ShiftInt = std.math.Log2Int(usize);

            pub const empty: Set = .{ .ints = @splat(0) };

            pub fn isEmpty(set: Set) bool {
                return for (set.ints) |x| {
                    if (x != 0) break false;
                } else true;
            }

            pub fn count(set: Set) std.math.IntFittingRange(0, needed_bit_count) {
                var sum: usize = 0;
                for (set.ints) |x| sum += @popCount(x);
                return @intCast(sum);
            }

            pub fn isEnabled(set: Set, arch_feature_index: Index) bool {
                const usize_index = arch_feature_index / @bitSizeOf(usize);
                const bit_index: ShiftInt = @intCast(arch_feature_index % @bitSizeOf(usize));
                return (set.ints[usize_index] & (@as(usize, 1) << bit_index)) != 0;
            }

            /// Adds the specified feature but not its dependencies.
            pub fn addFeature(set: *Set, arch_feature_index: Index) void {
                const usize_index = arch_feature_index / @bitSizeOf(usize);
                const bit_index: ShiftInt = @intCast(arch_feature_index % @bitSizeOf(usize));
                set.ints[usize_index] |= @as(usize, 1) << bit_index;
            }

            /// Adds the specified feature set but not its dependencies.
            pub fn addFeatureSet(set: *Set, other_set: Set) void {
                set.ints = @as(@Vector(usize_count, usize), set.ints) | @as(@Vector(usize_count, usize), other_set.ints);
            }

            /// Removes the specified feature but not its dependents.
            pub fn removeFeature(set: *Set, arch_feature_index: Index) void {
                const usize_index = arch_feature_index / @bitSizeOf(usize);
                const bit_index: ShiftInt = @intCast(arch_feature_index % @bitSizeOf(usize));
                set.ints[usize_index] &= ~(@as(usize, 1) << bit_index);
            }

            /// Removes the specified feature but not its dependents.
            pub fn removeFeatureSet(set: *Set, other_set: Set) void {
                set.ints = @as(@Vector(usize_count, usize), set.ints) & ~@as(@Vector(usize_count, usize), other_set.ints);
            }

            pub fn populateDependencies(set: *Set, all_features_list: []const Cpu.Feature) void {
                @setEvalBranchQuota(1000000);

                var old = set.ints;
                while (true) {
                    for (all_features_list, 0..) |feature, index_usize| {
                        const index: Index = @intCast(index_usize);
                        if (set.isEnabled(index)) {
                            set.addFeatureSet(feature.dependencies);
                        }
                    }
                    const nothing_changed = std.mem.eql(usize, &old, &set.ints);
                    if (nothing_changed) return;
                    old = set.ints;
                }
            }

            pub fn asBytes(set: *const Set) *const [byte_count]u8 {
                return std.mem.sliceAsBytes(&set.ints)[0..byte_count];
            }

            pub fn eql(set: Set, other_set: Set) bool {
                return std.mem.eql(usize, &set.ints, &other_set.ints);
            }

            pub fn isSuperSetOf(set: Set, other_set: Set) bool {
                const V = @Vector(usize_count, usize);
                const set_v: V = set.ints;
                const other_v: V = other_set.ints;
                return @reduce(.And, (set_v & other_v) == other_v);
            }
        };

        pub fn FeatureSetFns(comptime F: type) type {
            return struct {
                /// Populates only the feature bits specified.
                pub fn featureSet(features: []const F) Set {
                    var x = Set.empty;
                    for (features) |feature| {
                        x.addFeature(@intFromEnum(feature));
                    }
                    return x;
                }

                /// Returns true if the specified feature is enabled.
                pub fn featureSetHas(set: Set, feature: F) bool {
                    return set.isEnabled(@intFromEnum(feature));
                }

                /// Returns true if any specified feature is enabled.
                pub fn featureSetHasAny(set: Set, features: anytype) bool {
                    inline for (features) |feature| {
                        if (set.isEnabled(@intFromEnum(@as(F, feature)))) return true;
                    }
                    return false;
                }

                /// Returns true if every specified feature is enabled.
                pub fn featureSetHasAll(set: Set, features: anytype) bool {
                    inline for (features) |feature| {
                        if (!set.isEnabled(@intFromEnum(@as(F, feature)))) return false;
                    }
                    return true;
                }
            };
        }
    };

    pub const Arch = enum {
        aarch64,
        aarch64_be,
        alpha,
        amdgcn,
        arc,
        arceb,
        arm,
        armeb,
        avr,
        bpfeb,
        bpfel,
        csky,
        hexagon,
        hppa,
        hppa64,
        kalimba,
        kvx,
        lanai,
        loongarch32,
        loongarch64,
        m68k,
        microblaze,
        microblazeel,
        mips,
        mipsel,
        mips64,
        mips64el,
        msp430,
        nvptx,
        nvptx64,
        or1k,
        powerpc,
        powerpcle,
        powerpc64,
        powerpc64le,
        propeller,
        riscv32,
        riscv32be,
        riscv64,
        riscv64be,
        s390x,
        sh,
        sheb,
        sparc,
        sparc64,
        spirv32,
        spirv64,
        thumb,
        thumbeb,
        ve,
        wasm32,
        wasm64,
        x86_16,
        x86,
        x86_64,
        xcore,
        xtensa,
        xtensaeb,

        // LLVM tags deliberately omitted:
        // - aarch64_32
        // - amdil
        // - amdil64
        // - dxil
        // - r600
        // - hsail
        // - hsail64
        // - renderscript32
        // - renderscript64
        // - shave
        // - sparcel
        // - spir
        // - spir64
        // - spirv
        // - tce
        // - tcele

        /// An architecture family can encompass multiple architectures as represented by `Arch`.
        /// For a given family tag, it is guaranteed that an `std.Target.<tag>` namespace exists
        /// containing CPU model and feature data.
        pub const Family = enum {
            aarch64,
            alpha,
            amdgcn,
            arc,
            arm,
            avr,
            bpf,
            csky,
            hexagon,
            hppa,
            kalimba,
            kvx,
            lanai,
            loongarch,
            m68k,
            microblaze,
            mips,
            msp430,
            nvptx,
            or1k,
            powerpc,
            propeller,
            riscv,
            s390x,
            sh,
            sparc,
            spirv,
            ve,
            wasm,
            x86,
            xcore,
            xtensa,
        };

        pub inline fn family(arch: Arch) Family {
            return switch (arch) {
                .aarch64, .aarch64_be => .aarch64,
                .alpha => .alpha,
                .amdgcn => .amdgcn,
                .arc, .arceb => .arc,
                .arm, .armeb, .thumb, .thumbeb => .arm,
                .avr => .avr,
                .bpfeb, .bpfel => .bpf,
                .csky => .csky,
                .hexagon => .hexagon,
                .hppa, .hppa64 => .hppa,
                .kalimba => .kalimba,
                .kvx => .kvx,
                .lanai => .lanai,
                .loongarch32, .loongarch64 => .loongarch,
                .m68k => .m68k,
                .microblaze, .microblazeel => .microblaze,
                .mips, .mipsel, .mips64, .mips64el => .mips,
                .msp430 => .msp430,
                .or1k => .or1k,
                .nvptx, .nvptx64 => .nvptx,
                .powerpc, .powerpcle, .powerpc64, .powerpc64le => .powerpc,
                .propeller => .propeller,
                .riscv32, .riscv32be, .riscv64, .riscv64be => .riscv,
                .s390x => .s390x,
                .sh, .sheb => .sh,
                .sparc, .sparc64 => .sparc,
                .spirv32, .spirv64 => .spirv,
                .ve => .ve,
                .wasm32, .wasm64 => .wasm,
                .x86_16, .x86, .x86_64 => .x86,
                .xcore => .xcore,
                .xtensa, .xtensaeb => .xtensa,
            };
        }

        pub inline fn isX86(arch: Arch) bool {
            return switch (arch) {
                .x86_16, .x86, .x86_64 => true,
                else => false,
            };
        }

        /// Note that this includes Thumb.
        pub inline fn isArm(arch: Arch) bool {
            return switch (arch) {
                .arm, .armeb => true,
                else => arch.isThumb(),
            };
        }

        pub inline fn isThumb(arch: Arch) bool {
            return switch (arch) {
                .thumb, .thumbeb => true,
                else => false,
            };
        }

        pub inline fn isAARCH64(arch: Arch) bool {
            return switch (arch) {
                .aarch64, .aarch64_be => true,
                else => false,
            };
        }

        pub inline fn isArc(arch: Arch) bool {
            return switch (arch) {
                .arc, .arceb => true,
                else => false,
            };
        }

        pub inline fn isHppa(arch: Arch) bool {
            return switch (arch) {
                .hppa, .hppa64 => true,
                else => false,
            };
        }

        pub inline fn isWasm(arch: Arch) bool {
            return switch (arch) {
                .wasm32, .wasm64 => true,
                else => false,
            };
        }

        pub inline fn isLoongArch(arch: Arch) bool {
            return switch (arch) {
                .loongarch32, .loongarch64 => true,
                else => false,
            };
        }

        pub inline fn isRISCV(arch: Arch) bool {
            return arch.isRiscv32() or arch.isRiscv64();
        }

        pub inline fn isRiscv32(arch: Arch) bool {
            return switch (arch) {
                .riscv32, .riscv32be => true,
                else => false,
            };
        }

        pub inline fn isRiscv64(arch: Arch) bool {
            return switch (arch) {
                .riscv64, .riscv64be => true,
                else => false,
            };
        }

        pub inline fn isMicroblaze(arch: Arch) bool {
            return switch (arch) {
                .microblaze, .microblazeel => true,
                else => false,
            };
        }

        pub inline fn isMIPS(arch: Arch) bool {
            return arch.isMIPS32() or arch.isMIPS64();
        }

        pub inline fn isMIPS32(arch: Arch) bool {
            return switch (arch) {
                .mips, .mipsel => true,
                else => false,
            };
        }

        pub inline fn isMIPS64(arch: Arch) bool {
            return switch (arch) {
                .mips64, .mips64el => true,
                else => false,
            };
        }

        pub inline fn isPowerPC(arch: Arch) bool {
            return arch.isPowerPC32() or arch.isPowerPC64();
        }

        pub inline fn isPowerPC32(arch: Arch) bool {
            return switch (arch) {
                .powerpc, .powerpcle => true,
                else => false,
            };
        }

        pub inline fn isPowerPC64(arch: Arch) bool {
            return switch (arch) {
                .powerpc64, .powerpc64le => true,
                else => false,
            };
        }

        pub inline fn isSPARC(arch: Arch) bool {
            return switch (arch) {
                .sparc, .sparc64 => true,
                else => false,
            };
        }

        pub inline fn isSpirV(arch: Arch) bool {
            return switch (arch) {
                .spirv32, .spirv64 => true,
                else => false,
            };
        }

        pub inline fn isSh(arch: Arch) bool {
            return switch (arch) {
                .sh, .sheb => true,
                else => false,
            };
        }

        pub inline fn isBpf(arch: Arch) bool {
            return switch (arch) {
                .bpfel, .bpfeb => true,
                else => false,
            };
        }

        pub inline fn isNvptx(arch: Arch) bool {
            return switch (arch) {
                .nvptx, .nvptx64 => true,
                else => false,
            };
        }

        pub inline fn isXtensa(arch: Arch) bool {
            return switch (arch) {
                .xtensa, .xtensaeb => true,
                else => false,
            };
        }

        pub fn parseCpuModel(arch: Arch, cpu_name: []const u8) !*const Cpu.Model {
            for (arch.allCpuModels()) |cpu| {
                if (std.mem.eql(u8, cpu_name, cpu.name)) {
                    return cpu;
                }
            }
            return error.UnknownCpuModel;
        }

        pub fn endian(arch: Arch) std.builtin.Endian {
            return switch (arch) {
                .aarch64,
                .alpha,
                .arm,
                .arc,
                .avr,
                .bpfel,
                .csky,
                .hexagon,
                .kalimba,
                .kvx,
                .loongarch32,
                .loongarch64,
                .microblazeel,
                .mipsel,
                .mips64el,
                .msp430,
                .powerpcle,
                .powerpc64le,
                .propeller,
                .riscv32,
                .riscv64,
                .sh,
                .thumb,
                .ve,
                .wasm32,
                .wasm64,
                .x86_16,
                .x86,
                .x86_64,
                .xcore,
                .xtensa,
                => .little,

                .aarch64_be,
                .arceb,
                .armeb,
                .bpfeb,
                .hppa,
                .hppa64,
                .lanai,
                .m68k,
                .microblaze,
                .mips,
                .mips64,
                .or1k,
                .powerpc,
                .powerpc64,
                .riscv32be,
                .riscv64be,
                .s390x,
                .sheb,
                .thumbeb,
                .sparc,
                .sparc64,
                .xtensaeb,
                => .big,

                // GPU endianness is opaque. For now, assume little endian.
                .amdgcn,
                .nvptx,
                .nvptx64,
                .spirv32,
                .spirv64,
                => .little,
            };
        }

        /// All CPU features Zig is aware of, sorted lexicographically by name.
        pub fn allFeaturesList(arch: Arch) []const Cpu.Feature {
            return switch (arch.family()) {
                inline else => |f| &@field(Target, @tagName(f)).all_features,
            };
        }

        /// All processors Zig is aware of, sorted lexicographically by name.
        pub fn allCpuModels(arch: Arch) []const *const Cpu.Model {
            return switch (arch.family()) {
                inline else => |f| comptime allCpusFromDecls(@field(Target, @tagName(f)).cpu),
            };
        }

        fn allCpusFromDecls(comptime cpus: type) []const *const Cpu.Model {
            @setEvalBranchQuota(2000);
            const decls = @typeInfo(cpus).@"struct".decls;
            var array: [decls.len]*const Cpu.Model = undefined;
            for (decls, 0..) |decl, i| {
                array[i] = &@field(cpus, decl.name);
            }
            const finalized = array;
            return &finalized;
        }

        /// 0c spim    little-endian MIPS 3000 family
        /// 1c 68000   Motorola MC68000
        /// 2c 68020   Motorola MC68020
        /// 5c arm     little-endian ARM
        /// 6c amd64   AMD64 and compatibles (e.g., Intel EM64T)
        /// 7c arm64   ARM64 (ARMv8)
        /// 8c 386     Intel x86, i486, Pentium, etc.
        /// kc sparc   Sun SPARC
        /// qc power   Power PC
        /// vc mips    big-endian MIPS 3000 family
        pub fn plan9Ext(arch: Cpu.Arch) [:0]const u8 {
            return switch (arch) {
                .arm => ".5",
                .x86_64 => ".6",
                .aarch64 => ".7",
                .x86 => ".8",
                .sparc => ".k",
                .powerpc, .powerpcle => ".q",
                .mips, .mipsel => ".v",
                // ISAs without designated characters get 'X' for lack of a better option.
                else => ".X",
            };
        }

        /// Returns the array of `Arch` to which a specific `std.builtin.CallingConvention` applies.
        /// Asserts that `cc` is not `.auto`, `.@"async"`, `.naked`, or `.@"inline"`.
        pub fn fromCallingConvention(cc: std.builtin.CallingConvention.Tag) []const Arch {
            return switch (cc) {
                .auto,
                .async,
                .naked,
                .@"inline",
                => unreachable,

                .x86_64_sysv,
                .x86_64_x32,
                .x86_64_win,
                .x86_64_regcall_v3_sysv,
                .x86_64_regcall_v4_win,
                .x86_64_vectorcall,
                .x86_64_interrupt,
                => &.{.x86_64},

                .x86_sysv,
                .x86_win,
                .x86_stdcall,
                .x86_fastcall,
                .x86_thiscall,
                .x86_thiscall_mingw,
                .x86_regcall_v3,
                .x86_regcall_v4_win,
                .x86_vectorcall,
                .x86_interrupt,
                => &.{.x86},

                .x86_16_cdecl,
                .x86_16_stdcall,
                .x86_16_regparmcall,
                .x86_16_interrupt,
                => &.{.x86_16},

                .aarch64_aapcs,
                .aarch64_aapcs_darwin,
                .aarch64_aapcs_win,
                .aarch64_vfabi,
                .aarch64_vfabi_sve,
                => &.{ .aarch64, .aarch64_be },

                .alpha_osf,
                => &.{.alpha},

                .arm_aapcs,
                .arm_aapcs_vfp,
                .arm_interrupt,
                => &.{ .arm, .armeb, .thumb, .thumbeb },

                .mips64_n64,
                .mips64_n32,
                .mips64_interrupt,
                => &.{ .mips64, .mips64el },

                .mips_o32,
                .mips_interrupt,
                => &.{ .mips, .mipsel },

                .riscv64_lp64,
                .riscv64_lp64_v,
                .riscv64_interrupt,
                => &.{ .riscv64, .riscv64be },

                .riscv32_ilp32,
                .riscv32_ilp32_v,
                .riscv32_interrupt,
                => &.{ .riscv32, .riscv32be },

                .sparc64_sysv,
                => &.{.sparc64},

                .sparc_sysv,
                => &.{.sparc},

                .powerpc64_elf,
                .powerpc64_elf_altivec,
                .powerpc64_elf_v2,
                => &.{ .powerpc64, .powerpc64le },

                .powerpc_sysv,
                .powerpc_sysv_altivec,
                .powerpc_aix,
                .powerpc_aix_altivec,
                => &.{ .powerpc, .powerpcle },

                .wasm_mvp,
                => &.{ .wasm64, .wasm32 },

                .arc_sysv,
                .arc_interrupt,
                => &.{ .arc, .arceb },

                .avr_gnu,
                .avr_builtin,
                .avr_signal,
                .avr_interrupt,
                => &.{.avr},

                .bpf_std,
                => &.{ .bpfel, .bpfeb },

                .csky_sysv,
                .csky_interrupt,
                => &.{.csky},

                .hexagon_sysv,
                .hexagon_sysv_hvx,
                => &.{.hexagon},

                .hppa_elf,
                => &.{.hppa},

                .hppa64_elf,
                => &.{.hppa64},

                .kvx_lp64,
                .kvx_ilp32,
                => &.{.kvx},

                .lanai_sysv,
                => &.{.lanai},

                .loongarch64_lp64,
                => &.{.loongarch64},

                .loongarch32_ilp32,
                => &.{.loongarch32},

                .m68k_sysv,
                .m68k_gnu,
                .m68k_rtd,
                .m68k_interrupt,
                => &.{.m68k},

                .microblaze_std,
                .microblaze_interrupt,
                => &.{ .microblaze, .microblazeel },

                .msp430_eabi,
                .msp430_interrupt,
                => &.{.msp430},

                .or1k_sysv,
                => &.{.or1k},

                .propeller_sysv,
                => &.{.propeller},

                .s390x_sysv,
                .s390x_sysv_vx,
                => &.{.s390x},

                .sh_gnu,
                .sh_renesas,
                .sh_interrupt,
                => &.{ .sh, .sheb },

                .ve_sysv,
                => &.{.ve},

                .xcore_xs1,
                .xcore_xs2,
                => &.{.xcore},

                .xtensa_call0,
                .xtensa_windowed,
                => &.{ .xtensa, .xtensaeb },

                .amdgcn_device,
                .amdgcn_kernel,
                .amdgcn_cs,
                => &.{.amdgcn},

                .nvptx_device,
                .nvptx_kernel,
                => &.{ .nvptx, .nvptx64 },

                .spirv_device,
                .spirv_kernel,
                .spirv_fragment,
                .spirv_vertex,
                => &.{ .spirv32, .spirv64 },
            };
        }
    };

    pub const Model = struct {
        name: []const u8,
        llvm_name: ?[:0]const u8,
        features: Feature.Set,

        pub fn toCpu(model: *const Model, arch: Arch) Cpu {
            var features = model.features;
            features.populateDependencies(arch.allFeaturesList());
            return .{
                .arch = arch,
                .model = model,
                .features = features,
            };
        }

        /// Returns the most bare-bones CPU model that is valid for `arch`. Note that this function
        /// can return CPU models that are understood by LLVM, but *not* understood by Clang. If
        /// Clang compatibility is important, consider using `baseline` instead.
        pub fn generic(arch: Arch) *const Model {
            return switch (arch) {
                .alpha => &alpha.cpu.ev4,
                .amdgcn => &amdgcn.cpu.gfx600,
                .avr => &avr.cpu.avr1,
                .hppa => &hppa.cpu.ts_1,
                .hppa64 => &hppa.cpu.pa_8000,
                .kvx => &kvx.cpu.coolidge_v1,
                .loongarch32 => &loongarch.cpu.generic_la32,
                .loongarch64 => &loongarch.cpu.generic_la64,
                .mips, .mipsel => &mips.cpu.mips32,
                .mips64, .mips64el => &mips.cpu.mips64,
                .nvptx, .nvptx64 => &nvptx.cpu.sm_20,
                .powerpc, .powerpcle => &powerpc.cpu.ppc,
                .powerpc64, .powerpc64le => &powerpc.cpu.ppc64,
                .propeller => &propeller.cpu.p1,
                .riscv32, .riscv32be => &riscv.cpu.generic_rv32,
                .riscv64, .riscv64be => &riscv.cpu.generic_rv64,
                .sparc64 => &sparc.cpu.v9, // SPARC can only be 64-bit from v9 and up.
                .wasm32, .wasm64 => &wasm.cpu.mvp,
                .x86_16 => &x86.cpu.i86,
                .x86 => &x86.cpu.i386,
                .x86_64 => &x86.cpu.x86_64,
                inline else => |a| &@field(Target, @tagName(a.family())).cpu.generic,
            };
        }

        /// Returns a conservative CPU model for `arch` that is expected to be compatible with the
        /// vast majority of hardware available. This function is guaranteed to return CPU models
        /// that are understood by both LLVM and Clang, unlike `generic`.
        ///
        /// For certain `os` values, this function will additionally bump the baseline higher than
        /// the baseline would be for `arch` in isolation; for example, for `aarch64-macos`, the
        /// baseline is considered to be `apple_m1`. To avoid this behavior entirely, pass
        /// `Os.Tag.freestanding`.
        pub fn baseline(arch: Arch, os: Os) *const Model {
            return switch (arch) {
                .alpha => &alpha.cpu.ev6,
                .amdgcn => &amdgcn.cpu.gfx906,
                .arm => switch (os.tag) {
                    .@"3ds" => &arm.cpu.mpcore,
                    .vita => &arm.cpu.cortex_a9,
                    else => &arm.cpu.baseline,
                },
                .thumb => switch (os.tag) {
                    .vita => &arm.cpu.cortex_a9,
                    else => &arm.cpu.baseline,
                },
                .armeb, .thumbeb => &arm.cpu.baseline,
                .aarch64 => switch (os.tag) {
                    .driverkit, .maccatalyst, .macos => &aarch64.cpu.apple_m1,
                    .ios, .tvos => &aarch64.cpu.apple_a7,
                    .visionos => &aarch64.cpu.apple_m2,
                    .watchos => &aarch64.cpu.apple_s4,
                    else => generic(arch),
                },
                .avr => &avr.cpu.avr2,
                .bpfel, .bpfeb => &bpf.cpu.v3,
                .csky => &csky.cpu.ck810, // gcc/clang do not have a generic csky model.
                .hexagon => &hexagon.cpu.hexagonv68, // gcc/clang do not have a generic hexagon model.
                .hppa => &hppa.cpu.pa_7300lc,
                .kvx => &kvx.cpu.coolidge_v2,
                .lanai => &lanai.cpu.v11, // clang does not have a generic lanai model.
                .loongarch64 => &loongarch.cpu.la64v1_0,
                .m68k => &m68k.cpu.M68000,
                .mips => &mips.cpu.mips32r2,
                .mipsel => switch (os.tag) {
                    .psp => &mips.cpu.allegrex,
                    else => &mips.cpu.mips32r2,
                },
                .mips64, .mips64el => &mips.cpu.mips64r2,
                .msp430 => &msp430.cpu.msp430,
                .nvptx, .nvptx64 => &nvptx.cpu.sm_52,
                .powerpc64le => &powerpc.cpu.ppc64le,
                .riscv32, .riscv32be => &riscv.cpu.baseline_rv32,
                .riscv64, .riscv64be => &riscv.cpu.baseline_rv64,
                // gcc/clang do not have a generic s390x model.
                .s390x => &s390x.cpu.arch8,
                .sparc => &sparc.cpu.v9, // glibc does not work with 'plain' v8.
                .x86 => &x86.cpu.pentium4,
                .x86_64 => switch (os.tag) {
                    .driverkit, .maccatalyst => &x86.cpu.nehalem,
                    .macos => &x86.cpu.core2,
                    .ps4 => &x86.cpu.btver2,
                    .ps5 => &x86.cpu.znver2,
                    else => generic(arch),
                },
                .xcore => &xcore.cpu.xs1b_generic,
                .wasm32, .wasm64 => &wasm.cpu.lime1,

                else => generic(arch),
            };
        }
    };

    /// The "default" set of CPU features for cross-compiling. A conservative set
    /// of features that is expected to be supported on most available hardware.
    pub fn baseline(arch: Arch, os: Os) Cpu {
        return Model.baseline(arch, os).toCpu(arch);
    }

    /// Returns true if `feature` is enabled.
    pub fn has(cpu: Cpu, comptime family: Arch.Family, feature: @field(Target, @tagName(family)).Feature) bool {
        if (family != cpu.arch.family()) return false;
        return cpu.features.isEnabled(@intFromEnum(feature));
    }

    /// Returns true if any feature in `features` is enabled.
    pub fn hasAny(cpu: Cpu, comptime family: Arch.Family, features: []const @field(Target, @tagName(family)).Feature) bool {
        if (family != cpu.arch.family()) return false;
        for (features) |feature| {
            if (cpu.features.isEnabled(@intFromEnum(feature))) return true;
        }
        return false;
    }

    /// Returns true if all features in `features` are enabled.
    pub fn hasAll(cpu: Cpu, comptime family: Arch.Family, features: []const @field(Target, @tagName(family)).Feature) bool {
        if (family != cpu.arch.family()) return false;
        for (features) |feature| {
            if (!cpu.features.isEnabled(@intFromEnum(feature))) return false;
        }
        return true;
    }
};

pub fn zigTriple(target: *const Target, allocator: Allocator) Allocator.Error![]u8 {
    return Query.fromTarget(target).zigTriple(allocator);
}

pub fn hurdTupleSimple(allocator: Allocator, arch: Cpu.Arch, abi: Abi) ![]u8 {
    return std.fmt.allocPrint(allocator, "{s}-{s}", .{ @tagName(arch), @tagName(abi) });
}

pub fn hurdTuple(target: *const Target, allocator: Allocator) ![]u8 {
    return hurdTupleSimple(allocator, target.cpu.arch, target.abi);
}

pub fn linuxTripleSimple(allocator: Allocator, arch: Cpu.Arch, os_tag: Os.Tag, abi: Abi) ![]u8 {
    return std.fmt.allocPrint(allocator, "{s}-{s}-{s}", .{ @tagName(arch), @tagName(os_tag), @tagName(abi) });
}

pub fn linuxTriple(target: *const Target, allocator: Allocator) ![]u8 {
    return linuxTripleSimple(allocator, target.cpu.arch, target.os.tag, target.abi);
}

pub fn exeFileExt(target: *const Target) [:0]const u8 {
    return target.os.tag.exeFileExt(target.cpu.arch);
}

pub fn staticLibSuffix(target: *const Target) [:0]const u8 {
    return target.os.tag.staticLibSuffix(target.abi);
}

pub fn dynamicLibSuffix(target: *const Target) [:0]const u8 {
    return target.os.tag.dynamicLibSuffix();
}

pub fn libPrefix(target: *const Target) [:0]const u8 {
    return target.os.tag.libPrefix(target.abi);
}

pub inline fn isMinGW(target: *const Target) bool {
    return target.os.tag == .windows and target.abi.isGnu();
}

pub inline fn isGnuLibC(target: *const Target) bool {
    return switch (target.os.tag) {
        .hurd, .linux => target.abi.isGnu(),
        else => false,
    };
}

pub inline fn isMuslLibC(target: *const Target) bool {
    return target.os.tag == .linux and target.abi.isMusl();
}

pub inline fn isBionicLibC(target: *const Target) bool {
    return target.os.tag == .linux and target.abi.isAndroid();
}

pub inline fn isDarwinLibC(target: *const Target) bool {
    return switch (target.abi) {
        .none, .simulator => target.os.tag.isDarwin(),
        else => false,
    };
}

pub inline fn isFreeBSDLibC(target: *const Target) bool {
    return switch (target.abi) {
        .none, .eabihf => target.os.tag == .freebsd,
        else => false,
    };
}

pub inline fn isNetBSDLibC(target: *const Target) bool {
    return switch (target.abi) {
        .none, .eabi, .eabihf => target.os.tag == .netbsd,
        else => false,
    };
}

pub inline fn isOpenBSDLibC(target: *const Target) bool {
    return switch (target.abi) {
        .none, .eabi, .eabihf => target.os.tag == .openbsd,
        else => false,
    };
}

pub inline fn isWasiLibC(target: *const Target) bool {
    return target.os.tag == .wasi and target.abi.isMusl();
}

/// Does this target require linking libc? This may be the case if the target has an unstable
/// syscall interface, for example.
pub fn requiresLibC(target: *const Target) bool {
    return switch (target.os.tag) {
        .illumos,
        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .watchos,
        .visionos,
        .dragonfly,
        .haiku,
        .serenity,
        => true,

        // Android API levels prior to 29 did not have native TLS support. For these API levels, TLS
        // is implemented through calls to `__emutls_get_address`. We provide this function in
        // compiler-rt, but it's implemented by way of `pthread_key_create` et al, so linking libc
        // is required.
        .linux => target.abi.isAndroid() and target.os.version_range.linux.android < 29,

        .windows,
        .freebsd,
        .netbsd,
        .openbsd,
        .freestanding,
        .fuchsia,
        .managarm,
        .ps3,
        .rtems,
        .cuda,
        .nvcl,
        .amdhsa,
        .ps4,
        .ps5,
        .psp,
        .vita,
        .mesa3d,
        .contiki,
        .amdpal,
        .hermit,
        .hurd,
        .wasi,
        .emscripten,
        .uefi,
        .opencl,
        .opengl,
        .vulkan,
        .plan9,
        .other,
        .@"3ds",
        => false,
    };
}

/// The places where a user can specify an address space attribute
pub const AddressSpaceContext = enum {
    /// A function is specified to be placed in a certain address space.
    function,
    /// A (global) variable is specified to be placed in a certain address space. In contrast to
    /// `.constant`, these values (and thus the address space they will be placed in) are required
    /// to be mutable.
    variable,
    /// A (global) constant value is specified to be placed in a certain address space. In contrast
    /// to `.variable`, values placed in this address space are not required to be mutable.
    constant,
    /// A pointer is ascripted to point into a certain address space.
    pointer,
};

/// Returns whether this target supports `address_space`. If `context` is `null`, this
/// function simply answers the general question of whether the target has any concept
/// of `address_space`; if non-`null`, the function additionally checks whether
/// `address_space` is valid in that context.
pub fn supportsAddressSpace(
    target: Target,
    address_space: std.builtin.AddressSpace,
    context: ?AddressSpaceContext,
) bool {
    const arch = target.cpu.arch;

    const is_nvptx = arch.isNvptx();
    const is_spirv = arch.isSpirV();
    const is_gpu = is_nvptx or is_spirv or arch == .amdgcn;

    return switch (address_space) {
        .generic => true,
        .fs, .gs, .ss => (arch == .x86_64 or arch == .x86 or arch == .x86_16) and (context == null or context == .pointer),
        // Technically x86 can use segmentation...
        .far => (arch == .x86_16),

        .flash, .flash1, .flash2, .flash3, .flash4, .flash5 => arch == .avr, // TODO this should also check how many flash banks the cpu has
        .cog, .hub => arch == .propeller,
        .lut => arch == .propeller and std.Target.propeller.featureSetHas(target.cpu.features, .p2),

        .global, .local, .shared => is_gpu,
        .constant => is_gpu and (context == null or context == .constant),
        .param => is_nvptx,
        .input, .output, .uniform, .push_constant, .storage_buffer, .physical_storage_buffer => is_spirv,
    };
}

pub const DynamicLinker = struct {
    /// Contains the memory used to store the dynamic linker path. This field
    /// should not be used directly. See `get` and `set`. This field exists so
    /// that this API requires no allocator.
    buffer: [255]u8,

    /// Used to construct the dynamic linker path. This field should not be used
    /// directly. See `get` and `set`.
    len: u8,

    pub const none: DynamicLinker = .{ .buffer = undefined, .len = 0 };

    /// Asserts that the length is less than or equal to 255 bytes.
    pub fn init(maybe_path: ?[]const u8) DynamicLinker {
        var dl: DynamicLinker = undefined;
        dl.set(maybe_path);
        return dl;
    }

    pub fn initFmt(comptime fmt_str: []const u8, args: anytype) !DynamicLinker {
        var dl: DynamicLinker = undefined;
        try dl.setFmt(fmt_str, args);
        return dl;
    }

    /// The returned memory has the same lifetime as the `DynamicLinker`.
    pub fn get(dl: *const DynamicLinker) ?[]const u8 {
        return if (dl.len > 0) dl.buffer[0..dl.len] else null;
    }

    /// Asserts that the length is less than or equal to 255 bytes.
    pub fn set(dl: *DynamicLinker, maybe_path: ?[]const u8) void {
        const path = maybe_path orelse "";
        @memcpy(dl.buffer[0..path.len], path);
        dl.len = @intCast(path.len);
    }

    /// Asserts that the length is less than or equal to 255 bytes.
    pub fn setFmt(dl: *DynamicLinker, comptime fmt_str: []const u8, args: anytype) !void {
        dl.len = @intCast((try std.fmt.bufPrint(&dl.buffer, fmt_str, args)).len);
    }

    pub fn eql(lhs: DynamicLinker, rhs: DynamicLinker) bool {
        return std.mem.eql(u8, lhs.buffer[0..lhs.len], rhs.buffer[0..rhs.len]);
    }

    pub const Kind = enum {
        /// No dynamic linker.
        none,
        /// Dynamic linker path is determined by the arch/OS components.
        arch_os,
        /// Dynamic linker path is determined by the arch/OS/ABI components.
        arch_os_abi,
    };

    pub fn kind(os: Os.Tag) Kind {
        return switch (os) {
            .fuchsia,

            .haiku,
            .illumos,
            .serenity,

            .dragonfly,
            .freebsd,
            .netbsd,
            .openbsd,

            .driverkit,
            .ios,
            .maccatalyst,
            .macos,
            .tvos,
            .visionos,
            .watchos,
            => .arch_os,
            .hurd,
            .linux,
            => .arch_os_abi,
            .freestanding,
            .other,

            .contiki,
            .hermit,
            .managarm, // Needs to be double-checked.

            .plan9,
            .rtems,

            .uefi,
            .windows,

            .@"3ds",

            .emscripten,
            .wasi,

            .amdhsa,
            .amdpal,
            .cuda,
            .mesa3d,
            .nvcl,
            .opencl,
            .opengl,
            .vulkan,

            .ps3,
            .ps4,
            .ps5,
            .psp,
            .vita,
            => .none,
        };
    }

    /// The strictness of this function depends on the value of `kind(os.tag)`:
    ///
    /// * `.none`: Ignores all arguments and just returns `none`.
    /// * `.arch_os`: Ignores `abi` and returns the dynamic linker matching `cpu` and `os`.
    /// * `.arch_os_abi`: Returns the dynamic linker matching `cpu`, `os`, and `abi`.
    ///
    /// In the case of `.arch_os` in particular, callers should be aware that a valid dynamic linker
    /// being returned only means that the `cpu` + `os` combination represents a platform that
    /// actually exists and which has an established dynamic linker path that does not change with
    /// the ABI; it does not necessarily mean that `abi` makes any sense at all for that platform.
    /// The responsibility for determining whether `abi` is valid in this case rests with the
    /// caller. `Abi.default()` can be used to pick a best-effort default ABI for such platforms.
    pub fn standard(cpu: Cpu, os: Os, abi: Abi) DynamicLinker {
        return switch (os.tag) {
            .fuchsia => switch (cpu.arch) {
                .aarch64,
                .riscv64,
                .x86_64,
                => init("ld.so.1"), // Fuchsia is unusual in that `DT_INTERP` is just a basename.
                else => none,
            },

            .haiku => switch (cpu.arch) {
                .arm,
                .aarch64,
                .m68k,
                .powerpc,
                .riscv64,
                .sparc64,
                .x86,
                .x86_64,
                => init("/system/runtime_loader"),
                else => none,
            },

            .hurd => switch (cpu.arch) {
                .aarch64,
                .aarch64_be,
                => |arch| if (abi == .gnu) initFmt("/lib/ld-{s}.so.1", .{@tagName(arch)}) else none,

                .x86 => if (abi == .gnu) init("/lib/ld.so.1") else none,
                .x86_64 => initFmt("/lib/ld-{s}.so.1", .{switch (abi) {
                    .gnu => "x86-64",
                    .gnux32 => "x32",
                    else => return none,
                }}),

                else => none,
            },

            .illumos,
            => switch (cpu.arch) {
                .x86,
                .x86_64,
                => initFmt("/lib/{s}ld.so.1", .{if (ptrBitWidth_cpu_abi(cpu, .none) == 64) "64/" else ""}),
                else => none,
            },

            .linux => if (abi.isAndroid())
                switch (cpu.arch) {
                    .arm => if (abi == .androideabi) init("/system/bin/linker") else none,

                    .aarch64,
                    .riscv64,
                    .x86,
                    .x86_64,
                    => if (abi == .android) initFmt("/system/bin/linker{s}", .{
                        if (ptrBitWidth_cpu_abi(cpu, abi) == 64) "64" else "",
                    }) else none,

                    else => none,
                }
            else if (abi.isMusl())
                switch (cpu.arch) {
                    .arm,
                    .armeb,
                    .thumb,
                    .thumbeb,
                    => |arch| initFmt("/lib/ld-musl-arm{s}{s}.so.1", .{
                        if (arch == .armeb or arch == .thumbeb) "eb" else "",
                        switch (abi) {
                            .musleabi => "",
                            .musleabihf => "hf",
                            else => return none,
                        },
                    }),

                    .loongarch32,
                    .loongarch64,
                    => |arch| initFmt("/lib/ld-musl-{s}{s}.so.1", .{
                        @tagName(arch),
                        switch (abi) {
                            .musl => "",
                            .muslf32 => "-sp",
                            .muslsf => "-sf",
                            else => return none,
                        },
                    }),

                    .aarch64,
                    .aarch64_be,
                    .hexagon,
                    .kvx,
                    .m68k,
                    .microblaze,
                    .microblazeel,
                    .powerpc64,
                    .powerpc64le,
                    .s390x,
                    => |arch| if (abi == .musl) initFmt("/lib/ld-musl-{s}.so.1", .{@tagName(arch)}) else none,

                    .mips,
                    .mipsel,
                    => |arch| initFmt("/lib/ld-musl-mips{s}{s}{s}.so.1", .{
                        if (cpu.has(.mips, .mips32r6)) "r6" else "",
                        if (arch == .mipsel) "el" else "",
                        switch (abi) {
                            .musleabi => "-sf",
                            .musleabihf => "",
                            else => return none,
                        },
                    }),

                    .mips64,
                    .mips64el,
                    => |arch| initFmt("/lib/ld-musl-mips{s}{s}{s}.so.1", .{
                        switch (abi) {
                            .muslabi64 => "64",
                            .muslabin32 => "n32",
                            else => return none,
                        },
                        if (cpu.has(.mips, .mips64r6)) "r6" else "",
                        if (arch == .mips64el) "el" else "",
                    }),

                    .powerpc => initFmt("/lib/ld-musl-powerpc{s}.so.1", .{switch (abi) {
                        .musleabi => "-sf",
                        .musleabihf => "",
                        else => return none,
                    }}),

                    .sh,
                    .sheb,
                    => |arch| initFmt("/lib/ld-musl-{t}{s}.so.1", .{
                        arch,
                        switch (abi) {
                            .musleabi => "-nofpu",
                            .musleabihf => "",
                            else => return none,
                        },
                    }),

                    .riscv32,
                    .riscv64,
                    => |arch| if (abi == .musl) initFmt("/lib/ld-musl-{s}{s}.so.1", .{
                        @tagName(arch),
                        if (cpu.has(.riscv, .d))
                            ""
                        else if (cpu.has(.riscv, .f))
                            "-sp"
                        else
                            "-sf",
                    }) else none,

                    .x86 => if (abi == .musl) init("/lib/ld-musl-i386.so.1") else none,

                    .x86_64 => initFmt("/lib/ld-musl-{s}.so.1", .{switch (abi) {
                        .musl => "x86_64",
                        .muslx32 => "x32",
                        else => return none,
                    }}),

                    else => none,
                }
            else if (abi.isGnu())
                switch (cpu.arch) {
                    // TODO: `700` ABI support.
                    .arc,
                    .arceb,
                    => |arch| if (abi == .gnu) initFmt("/lib/ld-linux-{t}.so.2", .{arch}) else none,

                    .arm,
                    .armeb,
                    .thumb,
                    .thumbeb,
                    => initFmt("/lib/ld-linux{s}.so.3", .{switch (abi) {
                        .gnueabi => "",
                        .gnueabihf => "-armhf",
                        else => return none,
                    }}),

                    .aarch64,
                    .aarch64_be,
                    => |arch| if (abi == .gnu) initFmt("/lib/ld-linux-{s}.so.1", .{@tagName(arch)}) else none,

                    // TODO: `-be` architecture support.
                    .csky => initFmt("/lib/ld-linux-cskyv2{s}.so.1", .{switch (abi) {
                        .gnueabi => "",
                        .gnueabihf => "-hf",
                        else => return none,
                    }}),

                    .loongarch64 => initFmt("/lib64/ld-linux-loongarch-{s}.so.1", .{switch (abi) {
                        .gnu => "lp64d",
                        .gnuf32 => "lp64f",
                        .gnusf => "lp64s",
                        else => return none,
                    }}),

                    .hppa,
                    .m68k,
                    .microblaze,
                    .microblazeel,
                    .xtensa,
                    .xtensaeb,
                    => if (abi == .gnu) init("/lib/ld.so.1") else none,

                    .mips,
                    .mipsel,
                    => switch (abi) {
                        .gnueabi,
                        .gnueabihf,
                        => initFmt("/lib/ld{s}.so.1", .{
                            if (cpu.has(.mips, .nan2008)) "-linux-mipsn8" else "",
                        }),
                        else => none,
                    },

                    .mips64,
                    .mips64el,
                    => initFmt("/lib{s}/ld{s}.so.1", .{
                        switch (abi) {
                            .gnuabi64 => "64",
                            .gnuabin32 => "32",
                            else => return none,
                        },
                        if (cpu.has(.mips, .nan2008)) "-linux-mipsn8" else "",
                    }),

                    .powerpc => switch (abi) {
                        .gnueabi,
                        .gnueabihf,
                        => init("/lib/ld.so.1"),
                        else => none,
                    },

                    .powerpc64,
                    .powerpc64le,
                    => if (abi == .gnu) init("/lib64/ld64.so.2") else none,

                    .riscv32,
                    .riscv64,
                    => |arch| if (abi == .gnu) initFmt("/lib/ld-linux-{s}{s}.so.1", .{
                        switch (arch) {
                            .riscv32 => "riscv32-ilp32",
                            .riscv64 => "riscv64-lp64",
                            else => unreachable,
                        },
                        if (cpu.has(.riscv, .d))
                            "d"
                        else if (cpu.has(.riscv, .f))
                            "f"
                        else
                            "",
                    }) else none,

                    .s390x => if (abi == .gnu) init("/lib/ld64.so.1") else none,

                    .sh,
                    .sheb,
                    => switch (abi) {
                        .gnueabi,
                        .gnueabihf,
                        => init("/lib/ld-linux.so.2"),
                        else => none,
                    },

                    .alpha,
                    .sparc,
                    .x86,
                    => if (abi == .gnu) init("/lib/ld-linux.so.2") else none,

                    .sparc64 => if (abi == .gnu) init("/lib64/ld-linux.so.2") else none,

                    .x86_64 => switch (abi) {
                        .gnu => init("/lib64/ld-linux-x86-64.so.2"),
                        .gnux32 => init("/libx32/ld-linux-x32.so.2"),
                        else => none,
                    },

                    else => none,
                }
            else
                none, // Not a known Linux libc.

            .serenity => switch (cpu.arch) {
                .aarch64,
                .riscv64,
                .x86_64,
                => init("/usr/lib/Loader.so"),
                else => none,
            },

            .dragonfly => if (cpu.arch == .x86_64) initFmt("{s}/libexec/ld-elf.so.2", .{
                if (os.version_range.semver.isAtLeast(.{ .major = 3, .minor = 8, .patch = 0 }) orelse false)
                    ""
                else
                    "/usr",
            }) else none,

            .freebsd => switch (cpu.arch) {
                .arm,
                .aarch64,
                .powerpc,
                .powerpc64,
                .powerpc64le,
                .riscv64,
                .x86,
                .x86_64,
                => initFmt("{s}/libexec/ld-elf.so.1", .{
                    if (os.version_range.semver.isAtLeast(.{ .major = 6, .minor = 0, .patch = 0 }) orelse false)
                        ""
                    else
                        "/usr",
                }),
                else => none,
            },

            .netbsd => switch (cpu.arch) {
                .alpha,
                .arm,
                .armeb,
                .aarch64,
                .aarch64_be,
                .hppa,
                .m68k,
                .mips,
                .mipsel,
                .mips64,
                .mips64el,
                .powerpc,
                .sh,
                .sheb,
                .sparc,
                .sparc64,
                .x86,
                .x86_64,
                => init("/libexec/ld.elf_so"),
                else => none,
            },

            .openbsd => switch (cpu.arch) {
                .alpha,
                .arm,
                .aarch64,
                .hppa,
                .mips64,
                .mips64el,
                .powerpc,
                .powerpc64,
                .riscv64,
                .sh,
                .sparc64,
                .x86,
                .x86_64,
                => init("/usr/libexec/ld.so"),
                else => none,
            },

            .driverkit,
            .ios,
            .maccatalyst,
            .macos,
            .tvos,
            .visionos,
            .watchos,
            => switch (cpu.arch) {
                .aarch64,
                .x86_64,
                => init("/usr/lib/dyld"),
                else => none,
            },

            // Operating systems in this list have been verified as not having a standard
            // dynamic linker path.
            .freestanding,
            .other,

            .contiki,
            .hermit,

            .plan9,
            .rtems,

            .uefi,
            .windows,

            .@"3ds",

            .psp,
            .vita,

            .emscripten,
            .wasi,

            .amdhsa,
            .amdpal,
            .cuda,
            .mesa3d,
            .nvcl,
            .opencl,
            .opengl,
            .vulkan,
            => none,

            // TODO go over each item in this list and either move it to the above list, or
            // implement the standard dynamic linker path code for it.
            .managarm,

            .ps3,
            .ps4,
            .ps5,
            => none,
        } catch unreachable;
    }
};

pub fn standardDynamicLinkerPath(target: *const Target) DynamicLinker {
    return DynamicLinker.standard(target.cpu, target.os, target.abi);
}

pub fn ptrBitWidth_cpu_abi(cpu: Cpu, abi: Abi) u16 {
    return ptrBitWidth_arch_abi(cpu.arch, abi);
}

pub fn ptrBitWidth_arch_abi(cpu_arch: Cpu.Arch, abi: Abi) u16 {
    switch (abi) {
        .gnux32, .muslx32, .gnuabin32, .muslabin32, .ilp32 => return 32,
        .gnuabi64, .muslabi64 => return 64,
        else => {},
    }
    return switch (cpu_arch) {
        .avr,
        .msp430,
        .x86_16,
        => 16,

        .arc,
        .arceb,
        .arm,
        .armeb,
        .csky,
        .hexagon,
        .hppa,
        .kalimba,
        .lanai,
        .loongarch32,
        .m68k,
        .microblaze,
        .microblazeel,
        .mips,
        .mipsel,
        .nvptx,
        .or1k,
        .powerpc,
        .powerpcle,
        .propeller,
        .riscv32,
        .riscv32be,
        .sh,
        .sheb,
        .sparc,
        .spirv32,
        .thumb,
        .thumbeb,
        .wasm32,
        .x86,
        .xcore,
        .xtensa,
        .xtensaeb,
        => 32,

        .aarch64,
        .aarch64_be,
        .alpha,
        .amdgcn,
        .bpfeb,
        .bpfel,
        .hppa64,
        .kvx,
        .loongarch64,
        .mips64,
        .mips64el,
        .nvptx64,
        .powerpc64,
        .powerpc64le,
        .riscv64,
        .riscv64be,
        .s390x,
        .sparc64,
        .spirv64,
        .ve,
        .wasm64,
        .x86_64,
        => 64,
    };
}

pub fn ptrBitWidth(target: *const Target) u16 {
    return ptrBitWidth_cpu_abi(target.cpu, target.abi);
}

pub fn stackAlignment(target: *const Target) u16 {
    // Overrides for when the stack alignment is not equal to the pointer width.
    switch (target.cpu.arch) {
        .m68k,
        => return 2,
        .amdgcn,
        => return 4,
        .arm,
        .armeb,
        .hppa,
        .lanai,
        .mips,
        .mipsel,
        .sparc,
        .thumb,
        .thumbeb,
        => return 8,
        .aarch64,
        .aarch64_be,
        .alpha,
        .bpfeb,
        .bpfel,
        .hppa64,
        .loongarch32,
        .loongarch64,
        .mips64,
        .mips64el,
        .sparc64,
        .ve,
        .wasm32,
        .wasm64,
        .x86_64,
        => return 16,
        // Some of the following prongs should really be testing the ABI, but our current `Abi` enum
        // can't handle that level of nuance yet.
        .powerpc64,
        .powerpc64le,
        => if (target.os.tag == .linux) return 16,
        .riscv32,
        .riscv32be,
        .riscv64,
        .riscv64be,
        => if (!target.cpu.has(.riscv, .e)) return 16,
        .x86 => if (target.os.tag != .windows and target.os.tag != .uefi) return 16,
        .kvx => return 32,
        else => {},
    }

    return @divExact(target.ptrBitWidth(), 8);
}

pub const StackGrowth = enum {
    down,
    up,
};

pub fn stackGrowth(target: *const Target) StackGrowth {
    // Strictly speaking, most architectures don't inherently define the stack growth direction; you
    // could quite easily argue that it is in fact a property of the ABI. However, that's just not
    // really how it plays out in the real world. And besides, we have no mechanism for indicating
    // a different stack growth ABI, nor a compelling use case for creating such a mechanism.
    return switch (target.cpu.arch) {
        .hppa,
        .hppa64,
        => .up,
        else => .down,
    };
}

/// Default signedness of `char` for the native C compiler for this target
/// Note that char signedness is implementation-defined and many compilers provide
/// an option to override the default signedness e.g. GCC's -funsigned-char / -fsigned-char
pub fn cCharSignedness(target: *const Target) std.builtin.Signedness {
    if (target.os.tag.isDarwin() or target.os.tag == .windows or target.os.tag == .uefi) return .signed;

    return switch (target.cpu.arch) {
        .aarch64,
        .aarch64_be,
        .arm,
        .armeb,
        .arc,
        .arceb,
        .csky,
        .hexagon,
        .msp430,
        .powerpc,
        .powerpcle,
        .powerpc64,
        .powerpc64le,
        .s390x,
        .riscv32,
        .riscv32be,
        .riscv64,
        .riscv64be,
        .thumb,
        .thumbeb,
        .xcore,
        .xtensa,
        .xtensaeb,
        => .unsigned,
        else => .signed,
    };
}

pub const CType = enum {
    char,
    short,
    ushort,
    int,
    uint,
    long,
    ulong,
    longlong,
    ulonglong,
    float,
    double,
    longdouble,
};

pub fn cTypeByteSize(t: *const Target, c_type: CType) u16 {
    return switch (c_type) {
        .char,
        .short,
        .ushort,
        .int,
        .uint,
        .long,
        .ulong,
        .longlong,
        .ulonglong,
        .float,
        .double,
        => @divExact(cTypeBitSize(t, c_type), 8),

        .longdouble => switch (cTypeBitSize(t, c_type)) {
            16 => 2,
            32 => 4,
            64 => 8,
            80 => @intCast(std.mem.alignForward(usize, 10, cTypeAlignment(t, .longdouble))),
            128 => 16,
            else => unreachable,
        },
    };
}

pub fn cTypeBitSize(target: *const Target, c_type: CType) u16 {
    switch (target.os.tag) {
        .freestanding, .other => switch (target.cpu.arch) {
            .msp430, .x86_16 => switch (c_type) {
                .char => return 8,
                .short, .ushort, .int, .uint => return 16,
                .float, .long, .ulong => return 32,
                .longlong, .ulonglong, .double, .longdouble => return 64,
            },
            .avr => switch (c_type) {
                .char => return 8,
                .short, .ushort, .int, .uint => return 16,
                .long, .ulong, .float, .double, .longdouble => return 32,
                .longlong, .ulonglong => return 64,
            },
            .mips64, .mips64el => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => switch (target.abi) {
                    .gnuabin32, .muslabin32 => return 32,
                    else => return 64,
                },
                .longlong, .ulonglong, .double => return 64,
                .longdouble => return 128,
            },
            .x86_64 => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => switch (target.abi) {
                    .gnux32, .muslx32 => return 32,
                    else => return 64,
                },
                .longlong, .ulonglong, .double => return 64,
                .longdouble => return 80,
            },
            else => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => return target.ptrBitWidth(),
                .longlong, .ulonglong, .double => return 64,
                .longdouble => switch (target.cpu.arch) {
                    .x86 => switch (target.abi) {
                        .android => return 64,
                        else => return 80,
                    },

                    .powerpc,
                    .powerpcle,
                    .powerpc64,
                    .powerpc64le,
                    => switch (target.abi) {
                        .musl,
                        .muslabin32,
                        .muslabi64,
                        .musleabi,
                        .musleabihf,
                        .muslx32,
                        => return 64,
                        else => return 128,
                    },

                    .alpha,
                    .riscv32,
                    .riscv32be,
                    .riscv64,
                    .riscv64be,
                    .aarch64,
                    .aarch64_be,
                    .s390x,
                    .sparc64,
                    .wasm32,
                    .wasm64,
                    .loongarch32,
                    .loongarch64,
                    .ve,
                    => return 128,

                    else => return 64,
                },
            },
        },

        .fuchsia,
        .hermit,

        .haiku,
        .hurd,
        .illumos,
        .linux,
        .plan9,
        .rtems,
        .serenity,

        .freebsd,
        .dragonfly,
        .netbsd,
        .openbsd,

        .wasi,
        .emscripten,
        => switch (target.cpu.arch) {
            .mips64, .mips64el => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => switch (target.abi) {
                    .gnuabin32, .muslabin32 => return 32,
                    else => return 64,
                },
                .longlong, .ulonglong, .double => return 64,
                .longdouble => if (target.os.tag == .freebsd) return 64 else return 128,
            },
            .x86_64 => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => switch (target.abi) {
                    .gnux32, .muslx32 => return 32,
                    else => return 64,
                },
                .longlong, .ulonglong, .double => return 64,
                .longdouble => return 80,
            },
            else => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => return target.ptrBitWidth(),
                .longlong, .ulonglong, .double => return 64,
                .longdouble => switch (target.cpu.arch) {
                    .x86 => switch (target.abi) {
                        .android => return 64,
                        else => return 80,
                    },

                    .powerpc,
                    .powerpcle,
                    => switch (target.abi) {
                        .musl,
                        .muslabin32,
                        .muslabi64,
                        .musleabi,
                        .musleabihf,
                        .muslx32,
                        => return 64,
                        else => switch (target.os.tag) {
                            .freebsd, .netbsd, .openbsd => return 64,
                            else => return 128,
                        },
                    },

                    .powerpc64,
                    .powerpc64le,
                    => switch (target.abi) {
                        .musl,
                        .muslabin32,
                        .muslabi64,
                        .musleabi,
                        .musleabihf,
                        .muslx32,
                        => return 64,
                        else => switch (target.os.tag) {
                            .freebsd, .openbsd => return 64,
                            else => return 128,
                        },
                    },

                    .alpha,
                    .riscv32,
                    .riscv32be,
                    .riscv64,
                    .riscv64be,
                    .aarch64,
                    .aarch64_be,
                    .s390x,
                    .mips64,
                    .mips64el,
                    .sparc64,
                    .wasm32,
                    .wasm64,
                    .loongarch32,
                    .loongarch64,
                    .ve,
                    => return 128,

                    else => return 64,
                },
            },
        },

        .windows, .uefi => switch (target.cpu.arch) {
            .x86 => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => return 32,
                .longlong, .ulonglong, .double => return 64,
                .longdouble => switch (target.abi) {
                    .gnu, .ilp32 => return 80,
                    else => return 64,
                },
            },
            .x86_64 => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => return 32,
                .longlong, .ulonglong, .double => return 64,
                .longdouble => switch (target.abi) {
                    .gnu, .ilp32 => return 80,
                    else => return 64,
                },
            },
            else => switch (c_type) {
                .char => return 8,
                .short, .ushort => return 16,
                .int, .uint, .float => return 32,
                .long, .ulong => return 32,
                .longlong, .ulonglong, .double => return 64,
                .longdouble => return 64,
            },
        },

        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .visionos,
        .watchos,
        => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float => return 32,
            .long, .ulong => switch (target.cpu.arch) {
                .x86_64 => return 64,
                else => switch (target.abi) {
                    .ilp32 => return 32,
                    else => return 64,
                },
            },
            .longlong, .ulonglong, .double => return 64,
            .longdouble => switch (target.cpu.arch) {
                .x86_64 => return 80,
                else => return 64,
            },
        },

        .nvcl, .cuda => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float => return 32,
            .long, .ulong => switch (target.cpu.arch) {
                .nvptx => return 32,
                .nvptx64 => return 64,
                else => return 64,
            },
            .longlong, .ulonglong, .double => return 64,
            .longdouble => return 64,
        },

        .amdhsa, .amdpal, .mesa3d => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float => return 32,
            .long, .ulong, .longlong, .ulonglong, .double => return 64,
            .longdouble => return 128,
        },

        .opencl, .vulkan => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float => return 32,
            .long, .ulong, .double => return 64,
            .longlong, .ulonglong => return 128,
            // Note: The OpenCL specification does not guarantee a particular size for long double,
            // but clang uses 128 bits.
            .longdouble => return 128,
        },

        .@"3ds" => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float, .long, .ulong => return 32,
            .longlong, .ulonglong, .double, .longdouble => return 64,
        },

        .ps4, .ps5 => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float => return 32,
            .long, .ulong => return 64,
            .longlong, .ulonglong, .double => return 64,
            .longdouble => return 80,
        },
        .psp, .vita => switch (c_type) {
            .char => return 8,
            .short, .ushort => return 16,
            .int, .uint, .float => return 32,
            .long, .ulong => return 64,
            .longlong, .ulonglong, .double, .longdouble => return 64,
        },

        .ps3,
        .contiki,
        .managarm,
        .opengl,
        => @panic("specify the C integer and float type sizes for this OS"),
    }
}

pub fn cTypeAlignment(target: *const Target, c_type: CType) u16 {
    // Overrides for unusual alignments
    switch (target.cpu.arch) {
        .avr => return 1,
        .x86 => switch (target.os.tag) {
            .windows, .uefi => switch (c_type) {
                .longlong, .ulonglong, .double => return 8,
                .longdouble => switch (target.abi) {
                    .gnu, .ilp32 => return 4,
                    else => return 8,
                },
                else => {},
            },
            else => {},
        },
        .m68k => switch (c_type) {
            .int, .uint, .long, .ulong => return 2,
            else => {},
        },
        .wasm32, .wasm64 => switch (target.os.tag) {
            .emscripten => switch (c_type) {
                .longdouble => return 8,
                else => {},
            },
            else => {},
        },
        else => {},
    }

    // Next-power-of-two-aligned, up to a maximum.
    return @min(
        std.math.ceilPowerOfTwoAssert(u16, (cTypeBitSize(target, c_type) + 7) / 8),
        @as(u16, switch (target.cpu.arch) {
            .msp430,
            .x86_16,
            => 2,

            .arc,
            .arceb,
            .csky,
            .kalimba,
            .microblaze,
            .microblazeel,
            .or1k,
            .propeller,
            .sh,
            .sheb,
            .x86,
            .xcore,
            .xtensa,
            .xtensaeb,
            => 4,

            .amdgcn,
            .arm,
            .armeb,
            .bpfeb,
            .bpfel,
            .hexagon,
            .hppa,
            .lanai,
            .m68k,
            .mips,
            .mipsel,
            .nvptx,
            .nvptx64,
            .s390x,
            .sparc,
            .thumb,
            .thumbeb,
            => 8,

            .aarch64,
            .aarch64_be,
            .alpha,
            .hppa64,
            .kvx,
            .loongarch32,
            .loongarch64,
            .mips64,
            .mips64el,
            .powerpc,
            .powerpcle,
            .powerpc64,
            .powerpc64le,
            .riscv32,
            .riscv32be,
            .riscv64,
            .riscv64be,
            .sparc64,
            .spirv32,
            .spirv64,
            .ve,
            .wasm32,
            .wasm64,
            .x86_64,
            => 16,

            .avr,
            => unreachable, // Handled above.
        }),
    );
}

pub fn cTypePreferredAlignment(target: *const Target, c_type: CType) u16 {
    // Overrides for unusual alignments
    switch (target.cpu.arch) {
        .arc, .arceb => switch (c_type) {
            .longdouble => return 4,
            else => {},
        },
        .avr => return 1,
        .x86 => switch (target.os.tag) {
            .windows, .uefi => switch (c_type) {
                .longdouble => switch (target.abi) {
                    .gnu, .ilp32 => return 4,
                    else => return 8,
                },
                else => {},
            },
            else => switch (c_type) {
                .longdouble => return 4,
                else => {},
            },
        },
        .m68k => switch (c_type) {
            .int, .uint, .long, .ulong => return 2,
            else => {},
        },
        .wasm32, .wasm64 => switch (target.os.tag) {
            .emscripten => switch (c_type) {
                .longdouble => return 8,
                else => {},
            },
            else => {},
        },
        else => {},
    }

    // Next-power-of-two-aligned, up to a maximum.
    return @min(
        std.math.ceilPowerOfTwoAssert(u16, (cTypeBitSize(target, c_type) + 7) / 8),
        @as(u16, switch (target.cpu.arch) {
            .x86_16, .msp430 => 2,

            .arc,
            .arceb,
            .csky,
            .kalimba,
            .microblaze,
            .microblazeel,
            .or1k,
            .propeller,
            .sh,
            .sheb,
            .xcore,
            .xtensa,
            .xtensaeb,
            => 4,

            .amdgcn,
            .arm,
            .armeb,
            .bpfeb,
            .bpfel,
            .hexagon,
            .hppa,
            .lanai,
            .m68k,
            .mips,
            .mipsel,
            .nvptx,
            .nvptx64,
            .s390x,
            .sparc,
            .thumb,
            .thumbeb,
            .x86,
            => 8,

            .aarch64,
            .aarch64_be,
            .alpha,
            .hppa64,
            .kvx,
            .loongarch32,
            .loongarch64,
            .mips64,
            .mips64el,
            .powerpc,
            .powerpcle,
            .powerpc64,
            .powerpc64le,
            .riscv32,
            .riscv32be,
            .riscv64,
            .riscv64be,
            .sparc64,
            .spirv32,
            .spirv64,
            .ve,
            .wasm32,
            .wasm64,
            .x86_64,
            => 16,

            .avr,
            => unreachable, // Handled above.
        }),
    );
}

pub fn cMaxIntAlignment(target: *const Target) u16 {
    return switch (target.cpu.arch) {
        .avr => 1,

        .msp430, .x86_16 => 2,

        .arc,
        .arceb,
        .csky,
        .kalimba,
        .microblaze,
        .microblazeel,
        .or1k,
        .propeller,
        .sh,
        .sheb,
        .xcore,
        => 4,

        .arm,
        .armeb,
        .hexagon,
        .hppa,
        .lanai,
        .loongarch32,
        .m68k,
        .mips,
        .mipsel,
        .powerpc,
        .powerpcle,
        .riscv32,
        .riscv32be,
        .s390x,
        .sparc,
        .thumb,
        .thumbeb,
        .x86,
        .xtensa,
        .xtensaeb,
        => 8,

        .aarch64,
        .aarch64_be,
        .alpha,
        .amdgcn,
        .bpfel,
        .bpfeb,
        .hppa64,
        .kvx,
        .loongarch64,
        .mips64,
        .mips64el,
        .nvptx,
        .nvptx64,
        .powerpc64,
        .powerpc64le,
        .riscv64,
        .riscv64be,
        .sparc64,
        .spirv32,
        .spirv64,
        .ve,
        .wasm32,
        .wasm64,
        .x86_64,
        => 16,
    };
}

pub fn cCallingConvention(target: *const Target) ?std.builtin.CallingConvention {
    return switch (target.cpu.arch) {
        .x86_64 => switch (target.os.tag) {
            .windows, .uefi => .{ .x86_64_win = .{} },
            else => switch (target.abi) {
                .gnuabin32, .muslabin32 => .{ .x86_64_x32 = .{} },
                else => .{ .x86_64_sysv = .{} },
            },
        },
        .x86 => switch (target.os.tag) {
            .windows, .uefi => .{ .x86_win = .{} },
            else => .{ .x86_sysv = .{} },
        },
        .x86_16 => .{ .x86_16_cdecl = .{} },
        .aarch64, .aarch64_be => if (target.os.tag.isDarwin())
            .{ .aarch64_aapcs_darwin = .{} }
        else switch (target.os.tag) {
            .windows => .{ .aarch64_aapcs_win = .{} },
            else => .{ .aarch64_aapcs = .{} },
        },
        .alpha => .{ .alpha_osf = .{} },
        .arm, .armeb, .thumb, .thumbeb => switch (target.abi.float()) {
            .soft => .{ .arm_aapcs = .{} },
            .hard => .{ .arm_aapcs_vfp = .{} },
        },
        .mips64, .mips64el => switch (target.abi) {
            .gnuabin32, .muslabin32 => .{ .mips64_n32 = .{} },
            else => .{ .mips64_n64 = .{} },
        },
        .mips, .mipsel => .{ .mips_o32 = .{} },
        .riscv64, .riscv64be => .{ .riscv64_lp64 = .{} },
        .riscv32, .riscv32be => .{ .riscv32_ilp32 = .{} },
        .sparc64 => .{ .sparc64_sysv = .{} },
        .sparc => .{ .sparc_sysv = .{} },
        .powerpc64 => if (target.abi.isMusl())
            .{ .powerpc64_elf_v2 = .{} }
        else
            .{ .powerpc64_elf = .{} },
        .powerpc64le => .{ .powerpc64_elf_v2 = .{} },
        .powerpc, .powerpcle => .{ .powerpc_sysv = .{} },
        .wasm32, .wasm64 => .{ .wasm_mvp = .{} },
        .arc, .arceb => .{ .arc_sysv = .{} },
        .avr => .avr_gnu,
        .bpfel, .bpfeb => .{ .bpf_std = .{} },
        .csky => .{ .csky_sysv = .{} },
        .hexagon => .{ .hexagon_sysv = .{} },
        .hppa => .{ .hppa_elf = .{} },
        .hppa64 => .{ .hppa64_elf = .{} },
        .kalimba => null,
        .kvx => switch (target.abi) {
            .ilp32 => .{ .kvx_ilp32 = .{} },
            else => .{ .kvx_lp64 = .{} },
        },
        .lanai => .{ .lanai_sysv = .{} },
        .loongarch64 => .{ .loongarch64_lp64 = .{} },
        .loongarch32 => .{ .loongarch32_ilp32 = .{} },
        .m68k => if (target.abi.isGnu() or target.abi.isMusl())
            .{ .m68k_gnu = .{} }
        else
            .{ .m68k_sysv = .{} },
        .microblaze, .microblazeel => .{ .microblaze_std = .{} },
        .msp430 => .{ .msp430_eabi = .{} },
        .or1k => .{ .or1k_sysv = .{} },
        .propeller => .{ .propeller_sysv = .{} },
        .s390x => .{ .s390x_sysv = .{} },
        .sh, .sheb => .{ .sh_gnu = .{} },
        .ve => .{ .ve_sysv = .{} },
        .xcore => .{ .xcore_xs1 = .{} },
        .xtensa, .xtensaeb => .{ .xtensa_call0 = .{} },
        .amdgcn => .{ .amdgcn_device = .{} },
        .nvptx, .nvptx64 => .nvptx_device,
        .spirv32, .spirv64 => .spirv_device,
    };
}

const Target = @This();
const std = @import("std.zig");
const builtin = @import("builtin");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

test {
    std.testing.refAllDecls(Cpu.Arch);
}



---
File: /std/testing.zig
---

const builtin = @import("builtin");

const std = @import("std.zig");
const Io = std.Io;
const Environ = std.process.Environ;
const assert = std.debug.assert;
const math = std.math;

/// Provides deterministic randomness in unit tests.
/// Initialized on startup. Read-only after that.
pub var random_seed: u32 = 0;

pub const FailingAllocator = @import("testing/FailingAllocator.zig");
pub const failing_allocator = failing_allocator_instance.allocator();
var failing_allocator_instance = FailingAllocator.init(base_allocator_instance.allocator(), .{
    .fail_index = 0,
});
var base_allocator_instance = std.heap.FixedBufferAllocator.init("");

/// This should only be used in temporary test programs.
pub const allocator = allocator_instance.allocator();
pub var allocator_instance: std.heap.DebugAllocator(.{
    .stack_trace_frames = if (std.debug.sys_can_stack_trace) 10 else 0,
    .resize_stack_traces = true,
    // A unique value so that when a default-constructed
    // DebugAllocator is incorrectly passed to testing allocator, or
    // vice versa, panic occurs.
    .canary = @truncate(0x2731e675c3a701ba),
}) = b: {
    if (!builtin.is_test) @compileError("testing allocator used when not testing");
    break :b .init;
};

pub var io_instance: Io.Threaded = undefined;
pub const io = if (builtin.is_test) io_instance.io() else @compileError("not testing");

pub var environ: Environ = if (builtin.is_test) undefined else @compileError("not testing");

/// TODO https://github.com/ziglang/zig/issues/5738
pub var log_level = std.log.Level.warn;

// Disable printing in tests for simple backends.
pub const backend_can_print = switch (builtin.zig_backend) {
    .stage2_aarch64,
    .stage2_powerpc,
    .stage2_riscv64,
    .stage2_spirv,
    => false,
    else => true,
};

fn print(comptime fmt: []const u8, args: anytype) void {
    if (@inComptime()) {
        @compileError(std.fmt.comptimePrint(fmt, args));
    } else if (backend_can_print) {
        std.debug.print(fmt, args);
    }
}

/// This function is intended to be used only in tests. It prints diagnostics to stderr
/// and then returns a test failure error when actual_error_union is not expected_error.
pub fn expectError(expected_error: anyerror, actual_error_union: anytype) !void {
    if (actual_error_union) |actual_payload| {
        print("expected error.{s}, found {any}\n", .{ @errorName(expected_error), actual_payload });
        return error.TestExpectedError;
    } else |actual_error| {
        if (expected_error != actual_error) {
            print("expected error.{s}, found error.{s}\n", .{
                @errorName(expected_error),
                @errorName(actual_error),
            });
            return error.TestUnexpectedError;
        }
    }
}

/// This function is intended to be used only in tests. When the two values are not
/// equal, prints diagnostics to stderr to show exactly how they are not equal,
/// then returns a test failure error.
/// `actual` and `expected` are coerced to a common type using peer type resolution.
pub inline fn expectEqual(expected: anytype, actual: anytype) !void {
    const T = @TypeOf(expected, actual);
    return expectEqualInner(T, expected, actual);
}

fn expectEqualInner(comptime T: type, expected: T, actual: T) !void {
    switch (@typeInfo(@TypeOf(actual))) {
        .noreturn,
        .@"opaque",
        .frame,
        .@"anyframe",
        => @compileError("value of type " ++ @typeName(@TypeOf(actual)) ++ " encountered"),

        .undefined,
        .null,
        .void,
        => return,

        .type => {
            if (actual != expected) {
                print("expected type {s}, found type {s}\n", .{ @typeName(expected), @typeName(actual) });
                return error.TestExpectedEqual;
            }
        },

        .bool,
        .int,
        .float,
        .comptime_float,
        .comptime_int,
        .enum_literal,
        .@"enum",
        .@"fn",
        .error_set,
        => {
            if (actual != expected) {
                print("expected {any}, found {any}\n", .{ expected, actual });
                return error.TestExpectedEqual;
            }
        },

        .pointer => |pointer| {
            switch (pointer.size) {
                .one, .many, .c => {
                    if (actual != expected) {
                        print("expected {*}, found {*}\n", .{ expected, actual });
                        return error.TestExpectedEqual;
                    }
                },
                .slice => {
                    if (actual.ptr != expected.ptr) {
                        print("expected slice ptr {*}, found {*}\n", .{ expected.ptr, actual.ptr });
                        return error.TestExpectedEqual;
                    }
                    if (actual.len != expected.len) {
                        print("expected slice len {}, found {}\n", .{ expected.len, actual.len });
                        return error.TestExpectedEqual;
                    }
                },
            }
        },

        .array => |array| try expectEqualSlices(array.child, &expected, &actual),

        .vector => |info| {
            const expect_array: [info.len]info.child = expected;
            const actual_array: [info.len]info.child = actual;
            try expectEqualSlices(info.child, &expect_array, &actual_array);
        },

        .@"struct" => |structType| {
            inline for (structType.fields) |field| {
                try expectEqual(@field(expected, field.name), @field(actual, field.name));
            }
        },

        .@"union" => |union_info| {
            if (union_info.tag_type == null) {
                const first_size = @bitSizeOf(union_info.fields[0].type);
                inline for (union_info.fields) |field| {
                    if (@bitSizeOf(field.type) != first_size) {
                        @compileError("Unable to compare untagged unions with varying field sizes for type " ++ @typeName(@TypeOf(actual)));
                    }
                }

                const BackingInt = std.meta.Int(.unsigned, @bitSizeOf(T));
                return expectEqual(
                    @as(BackingInt, @bitCast(expected)),
                    @as(BackingInt, @bitCast(actual)),
                );
            }

            const Tag = std.meta.Tag(@TypeOf(expected));

            const expectedTag = @as(Tag, expected);
            const actualTag = @as(Tag, actual);

            try expectEqual(expectedTag, actualTag);

            // we only reach this switch if the tags are equal
            switch (expected) {
                inline else => |val, tag| try expectEqual(val, @field(actual, @tagName(tag))),
            }
        },

        .optional => {
            if (expected) |expected_payload| {
                if (actual) |actual_payload| {
                    try expectEqual(expected_payload, actual_payload);
                } else {
                    print("expected {any}, found null\n", .{expected_payload});
                    return error.TestExpectedEqual;
                }
            } else {
                if (actual) |actual_payload| {
                    print("expected null, found {any}\n", .{actual_payload});
                    return error.TestExpectedEqual;
                }
            }
        },

        .error_union => {
            if (expected) |expected_payload| {
                if (actual) |actual_payload| {
                    try expectEqual(expected_payload, actual_payload);
                } else |actual_err| {
                    print("expected {any}, found {}\n", .{ expected_payload, actual_err });
                    return error.TestExpectedEqual;
                }
            } else |expected_err| {
                if (actual) |actual_payload| {
                    print("expected {}, found {any}\n", .{ expected_err, actual_payload });
                    return error.TestExpectedEqual;
                } else |actual_err| {
                    try expectEqual(expected_err, actual_err);
                }
            }
        },
    }
}

test "expectEqual.union(enum)" {
    const T = union(enum) {
        a: i32,
        b: f32,
    };

    const a10 = T{ .a = 10 };

    try expectEqual(a10, a10);
}

test "expectEqual union with comptime-only field" {
    const U = union(enum) {
        a: void,
        b: void,
        c: comptime_int,
    };

    try expectEqual(U{ .a = {} }, .a);
}

test "expectEqual nested array" {
    const a = [2][2]f32{
        [_]f32{ 1.0, 0.0 },
        [_]f32{ 0.0, 1.0 },
    };

    const b = [2][2]f32{
        [_]f32{ 1.0, 0.0 },
        [_]f32{ 0.0, 1.0 },
    };

    try expectEqual(a, b);
}

test "expectEqual vector" {
    const a: @Vector(4, u32) = @splat(4);
    const b: @Vector(4, u32) = @splat(4);

    try expectEqual(a, b);
}

test "expectEqual null" {
    const a = .{null};
    const b = @Vector(1, ?*u8){null};

    try expectEqual(a, b);
}

/// This function is intended to be used only in tests. When the formatted result of the template
/// and its arguments does not equal the expected text, it prints diagnostics to stderr to show how
/// they are not equal, then returns an error. It depends on `expectEqualStrings` for printing
/// diagnostics.
pub fn expectFmt(expected: []const u8, comptime template: []const u8, args: anytype) !void {
    if (@inComptime()) {
        var buffer: [std.fmt.count(template, args)]u8 = undefined;
        return expectEqualStrings(expected, try std.fmt.bufPrint(&buffer, template, args));
    }
    const actual = try std.fmt.allocPrint(allocator, template, args);
    defer allocator.free(actual);
    return expectEqu
```
