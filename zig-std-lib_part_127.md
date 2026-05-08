```
e found with `cc -print-file-name=crt1.o`.
        \\# Not needed when targeting MacOS.
        \\crt_dir={s}
        \\
        \\# The directory that contains `vcruntime.lib`.
        \\# Only needed when targeting MSVC on Windows.
        \\msvc_lib_dir={s}
        \\
        \\# The directory that contains `kernel32.lib`.
        \\# Only needed when targeting MSVC on Windows.
        \\kernel32_lib_dir={s}
        \\
        \\# The directory that contains `crtbeginS.o` and `crtendS.o`
        \\# Only needed when targeting Haiku.
        \\gcc_dir={s}
        \\
    , .{
        include_dir,
        sys_include_dir,
        crt_dir,
        msvc_lib_dir,
        kernel32_lib_dir,
        gcc_dir,
    });
}

pub const FindNativeOptions = struct {
    target: *const std.Target,
    environ_map: *const Environ.Map,

    /// If enabled, will print human-friendly errors to stderr.
    verbose: bool = false,
};

/// Finds the default, native libc.
pub fn findNative(gpa: Allocator, io: Io, args: FindNativeOptions) FindError!LibCInstallation {
    var self: LibCInstallation = .{};

    if (is_darwin and args.target.os.tag.isDarwin()) {
        if (!std.zig.system.darwin.isSdkInstalled(gpa, io))
            return error.DarwinSdkNotFound;
        const sdk = std.zig.system.darwin.getSdk(gpa, io, args.target) orelse
            return error.DarwinSdkNotFound;
        defer gpa.free(sdk);

        self.include_dir = try fs.path.join(gpa, &.{
            sdk, "usr/include",
        });
        self.sys_include_dir = try fs.path.join(gpa, &.{
            sdk, "usr/include",
        });
        return self;
    } else if (is_windows) {
        const sdk = std.zig.WindowsSdk.find(gpa, io, args.target.cpu.arch, args.environ_map) catch |err| switch (err) {
            error.NotFound => return error.WindowsSdkNotFound,
            error.PathTooLong => return error.WindowsSdkNotFound,
            error.OutOfMemory => return error.OutOfMemory,
        };
        defer sdk.free(gpa);

        try self.findNativeMsvcIncludeDir(gpa, io, sdk);
        try self.findNativeMsvcLibDir(gpa, sdk);
        try self.findNativeKernel32LibDir(gpa, io, args, sdk);
        try self.findNativeIncludeDirWindows(gpa, io, sdk);
        try self.findNativeCrtDirWindows(gpa, io, args.target, sdk);
    } else if (is_haiku) {
        try self.findNativeIncludeDirPosix(gpa, io, args);
        try self.findNativeGccDirHaiku(gpa, io, args);
        self.crt_dir = try gpa.dupe(u8, "/system/develop/lib");
    } else if (builtin.target.os.tag == .illumos) {
        // There is only one libc, and its headers/libraries are always in the same spot.
        self.include_dir = try gpa.dupe(u8, "/usr/include");
        self.sys_include_dir = try gpa.dupe(u8, "/usr/include");
        self.crt_dir = try gpa.dupe(u8, "/usr/lib/64");
    } else if (std.process.can_spawn) {
        try self.findNativeIncludeDirPosix(gpa, io, args);
        switch (builtin.target.os.tag) {
            .freebsd, .netbsd, .openbsd, .dragonfly => self.crt_dir = try gpa.dupe(u8, "/usr/lib"),
            .linux => try self.findNativeCrtDirPosix(gpa, io, args),
            else => {},
        }
    } else {
        return error.LibCRuntimeNotFound;
    }
    return self;
}

/// Must be the same allocator passed to `parse` or `findNative`.
pub fn deinit(self: *LibCInstallation, allocator: Allocator) void {
    const fields = std.meta.fields(LibCInstallation);
    inline for (fields) |field| {
        if (@field(self, field.name)) |payload| {
            allocator.free(payload);
        }
    }
    self.* = undefined;
}

fn findNativeIncludeDirPosix(self: *LibCInstallation, gpa: Allocator, io: Io, args: FindNativeOptions) FindError!void {
    // Detect infinite loops.
    var environ_map = try args.environ_map.clone(gpa);
    defer environ_map.deinit();
    const skip_cc_env_var = if (environ_map.get(inf_loop_env_key)) |phase| blk: {
        if (std.mem.eql(u8, phase, "1")) {
            try environ_map.put(inf_loop_env_key, "2");
            break :blk true;
        } else {
            return error.ZigIsTheCCompiler;
        }
    } else blk: {
        try environ_map.put(inf_loop_env_key, "1");
        break :blk false;
    };

    const dev_null = if (is_windows) "nul" else "/dev/null";

    var argv = std.array_list.Managed([]const u8).init(gpa);
    defer argv.deinit();

    try appendCcExe(&argv, skip_cc_env_var, &environ_map);
    try argv.appendSlice(&.{
        "-E",
        "-Wp,-v",
        "-xc",
        dev_null,
    });

    const run_res = std.process.run(gpa, io, .{
        .stdout_limit = .limited(1024 * 1024),
        .stderr_limit = .limited(1024 * 1024),
        .argv = argv.items,
        .environ_map = &environ_map,
        // Some C compilers, such as Clang, are known to rely on argv[0] to find the path
        // to their own executable, without even bothering to resolve PATH. This results in the message:
        // error: unable to execute command: Executable "" doesn't exist!
        // So we use the expandArg0 variant of ChildProcess to give them a helping hand.
        .expand_arg0 = .expand,
    }) catch |err| switch (err) {
        error.OutOfMemory => return error.OutOfMemory,
        else => {
            printVerboseInvocation(argv.items, null, args.verbose, null);
            return error.UnableToSpawnCCompiler;
        },
    };
    defer {
        gpa.free(run_res.stdout);
        gpa.free(run_res.stderr);
    }
    switch (run_res.term) {
        .exited => |code| if (code != 0) {
            printVerboseInvocation(argv.items, null, args.verbose, run_res.stderr);
            return error.CCompilerExitCode;
        },
        else => {
            printVerboseInvocation(argv.items, null, args.verbose, run_res.stderr);
            return error.CCompilerCrashed;
        },
    }

    var it = std.mem.tokenizeAny(u8, run_res.stderr, "\n\r");
    var search_paths = std.array_list.Managed([]const u8).init(gpa);
    defer search_paths.deinit();
    while (it.next()) |line| {
        if (line.len != 0 and line[0] == ' ') {
            try search_paths.append(line);
        }
    }
    if (search_paths.items.len == 0) {
        return error.CCompilerCannotFindHeaders;
    }

    const include_dir_example_file = if (is_haiku) "posix/stdlib.h" else "stdlib.h";
    const sys_include_dir_example_file = if (is_windows)
        "sys\\types.h"
    else if (is_haiku)
        "errno.h"
    else
        "sys/errno.h";

    var path_i: usize = 0;
    while (path_i < search_paths.items.len) : (path_i += 1) {
        // search in reverse order
        const search_path_untrimmed = search_paths.items[search_paths.items.len - path_i - 1];
        const search_path = std.mem.trimStart(u8, search_path_untrimmed, " ");
        var search_dir = Io.Dir.cwd().openDir(io, search_path, .{}) catch |err| switch (err) {
            error.FileNotFound,
            error.NotDir,
            error.NoDevice,
            => continue,

            else => return error.FileSystem,
        };
        defer search_dir.close(io);

        if (self.include_dir == null) {
            if (search_dir.access(io, include_dir_example_file, .{})) |_| {
                self.include_dir = try gpa.dupe(u8, search_path);
            } else |err| switch (err) {
                error.FileNotFound => {},
                else => return error.FileSystem,
            }
        }

        if (self.sys_include_dir == null) {
            if (search_dir.access(io, sys_include_dir_example_file, .{})) |_| {
                self.sys_include_dir = try gpa.dupe(u8, search_path);
            } else |err| switch (err) {
                error.FileNotFound => {},
                else => return error.FileSystem,
            }
        }

        if (self.include_dir != null and self.sys_include_dir != null) {
            // Success.
            return;
        }
    }

    return error.LibCStdLibHeaderNotFound;
}

fn findNativeIncludeDirWindows(
    self: *LibCInstallation,
    gpa: Allocator,
    io: Io,
    sdk: std.zig.WindowsSdk,
) FindError!void {
    var install_buf: [2]std.zig.WindowsSdk.Installation = undefined;
    const installs = fillInstallations(&install_buf, sdk);

    var result_buf = std.array_list.Managed(u8).init(gpa);
    defer result_buf.deinit();

    for (installs) |install| {
        result_buf.shrinkAndFree(0);
        try result_buf.print("{s}\\Include\\{s}\\ucrt", .{ install.path, install.version });

        var dir = Io.Dir.cwd().openDir(io, result_buf.items, .{}) catch |err| switch (err) {
            error.FileNotFound,
            error.NotDir,
            error.NoDevice,
            => continue,

            else => return error.FileSystem,
        };
        defer dir.close(io);

        dir.access(io, "stdlib.h", .{}) catch |err| switch (err) {
            error.FileNotFound => continue,
            else => return error.FileSystem,
        };

        self.include_dir = try result_buf.toOwnedSlice();
        return;
    }

    return error.LibCStdLibHeaderNotFound;
}

fn findNativeCrtDirWindows(
    self: *LibCInstallation,
    gpa: Allocator,
    io: Io,
    target: *const std.Target,
    sdk: std.zig.WindowsSdk,
) FindError!void {
    var install_buf: [2]std.zig.WindowsSdk.Installation = undefined;
    const installs = fillInstallations(&install_buf, sdk);

    var result_buf = std.array_list.Managed(u8).init(gpa);
    defer result_buf.deinit();

    const arch_sub_dir = switch (target.cpu.arch) {
        .x86 => "x86",
        .x86_64 => "x64",
        .arm, .armeb => "arm",
        .aarch64 => "arm64",
        else => return error.UnsupportedArchitecture,
    };

    for (installs) |install| {
        result_buf.shrinkAndFree(0);
        try result_buf.print("{s}\\Lib\\{s}\\ucrt\\{s}", .{ install.path, install.version, arch_sub_dir });

        var dir = Io.Dir.cwd().openDir(io, result_buf.items, .{}) catch |err| switch (err) {
            error.FileNotFound,
            error.NotDir,
            error.NoDevice,
            => continue,

            else => return error.FileSystem,
        };
        defer dir.close(io);

        dir.access(io, "ucrt.lib", .{}) catch |err| switch (err) {
            error.FileNotFound => continue,
            else => return error.FileSystem,
        };

        self.crt_dir = try result_buf.toOwnedSlice();
        return;
    }
    return error.LibCRuntimeNotFound;
}

fn findNativeCrtDirPosix(self: *LibCInstallation, gpa: Allocator, io: Io, args: FindNativeOptions) FindError!void {
    self.crt_dir = try ccPrintFileName(gpa, io, .{
        .environ_map = args.environ_map,
        .search_basename = switch (args.target.os.tag) {
            .linux => if (args.target.abi.isAndroid()) "crtbegin_dynamic.o" else "crt1.o",
            else => "crt1.o",
        },
        .want_dirname = .only_dir,
        .verbose = args.verbose,
    });
}

fn findNativeGccDirHaiku(self: *LibCInstallation, gpa: Allocator, io: Io, args: FindNativeOptions) FindError!void {
    self.gcc_dir = try ccPrintFileName(gpa, io, .{
        .search_basename = "crtbeginS.o",
        .want_dirname = .only_dir,
        .verbose = args.verbose,
    });
}

fn findNativeKernel32LibDir(
    self: *LibCInstallation,
    gpa: Allocator,
    io: Io,
    args: FindNativeOptions,
    sdk: std.zig.WindowsSdk,
) FindError!void {
    var install_buf: [2]std.zig.WindowsSdk.Installation = undefined;
    const installs = fillInstallations(&install_buf, sdk);

    var result_buf = std.array_list.Managed(u8).init(gpa);
    defer result_buf.deinit();

    const arch_sub_dir = switch (args.target.cpu.arch) {
        .x86 => "x86",
        .x86_64 => "x64",
        .arm, .armeb => "arm",
        .aarch64 => "arm64",
        else => return error.UnsupportedArchitecture,
    };

    for (installs) |install| {
        result_buf.shrinkAndFree(0);
        try result_buf.print("{s}\\Lib\\{s}\\um\\{s}", .{ install.path, install.version, arch_sub_dir });

        var dir = Io.Dir.cwd().openDir(io, result_buf.items, .{}) catch |err| switch (err) {
            error.FileNotFound,
            error.NotDir,
            error.NoDevice,
            => continue,

            else => return error.FileSystem,
        };
        defer dir.close(io);

        dir.access(io, "kernel32.lib", .{}) catch |err| switch (err) {
            error.FileNotFound => continue,
            else => return error.FileSystem,
        };

        self.kernel32_lib_dir = try result_buf.toOwnedSlice();
        return;
    }
    return error.LibCKernel32LibNotFound;
}

fn findNativeMsvcIncludeDir(
    self: *LibCInstallation,
    gpa: Allocator,
    io: Io,
    sdk: std.zig.WindowsSdk,
) FindError!void {
    const msvc_lib_dir = sdk.msvc_lib_dir orelse return error.LibCStdLibHeaderNotFound;
    const up1 = fs.path.dirname(msvc_lib_dir) orelse return error.LibCStdLibHeaderNotFound;
    const up2 = fs.path.dirname(up1) orelse return error.LibCStdLibHeaderNotFound;

    const dir_path = try fs.path.join(gpa, &[_][]const u8{ up2, "include" });
    errdefer gpa.free(dir_path);

    var dir = Io.Dir.cwd().openDir(io, dir_path, .{}) catch |err| switch (err) {
        error.FileNotFound,
        error.NotDir,
        error.NoDevice,
        => return error.LibCStdLibHeaderNotFound,

        else => return error.FileSystem,
    };
    defer dir.close(io);

    dir.access(io, "vcruntime.h", .{}) catch |err| switch (err) {
        error.FileNotFound => return error.LibCStdLibHeaderNotFound,
        else => return error.FileSystem,
    };

    self.sys_include_dir = dir_path;
}

fn findNativeMsvcLibDir(
    self: *LibCInstallation,
    gpa: Allocator,
    sdk: std.zig.WindowsSdk,
) FindError!void {
    const msvc_lib_dir = sdk.msvc_lib_dir orelse return error.LibCRuntimeNotFound;
    self.msvc_lib_dir = try gpa.dupe(u8, msvc_lib_dir);
}

pub const CCPrintFileNameOptions = struct {
    environ_map: *const Environ.Map,
    search_basename: []const u8,
    want_dirname: enum { full_path, only_dir },
    verbose: bool = false,
};

/// caller owns returned memory
fn ccPrintFileName(gpa: Allocator, io: Io, args: CCPrintFileNameOptions) ![]u8 {
    // Detect infinite loops.
    var environ_map = try args.environ_map.clone(gpa);
    defer environ_map.deinit();
    const skip_cc_env_var = if (environ_map.get(inf_loop_env_key)) |phase| blk: {
        if (std.mem.eql(u8, phase, "1")) {
            try environ_map.put(inf_loop_env_key, "2");
            break :blk true;
        } else {
            return error.ZigIsTheCCompiler;
        }
    } else blk: {
        try environ_map.put(inf_loop_env_key, "1");
        break :blk false;
    };

    var argv = std.array_list.Managed([]const u8).init(gpa);
    defer argv.deinit();

    const arg1 = try std.fmt.allocPrint(gpa, "-print-file-name={s}", .{args.search_basename});
    defer gpa.free(arg1);

    try appendCcExe(&argv, skip_cc_env_var, &environ_map);
    try argv.append(arg1);

    const run_res = std.process.run(gpa, io, .{
        .stdout_limit = .limited(1024 * 1024),
        .stderr_limit = .limited(1024 * 1024),
        .argv = argv.items,
        .environ_map = &environ_map,
        // Some C compilers, such as Clang, are known to rely on argv[0] to find the path
        // to their own executable, without even bothering to resolve PATH. This results in the message:
        // error: unable to execute command: Executable "" doesn't exist!
        // So we use the expandArg0 variant of ChildProcess to give them a helping hand.
        .expand_arg0 = .expand,
    }) catch |err| switch (err) {
        error.OutOfMemory => return error.OutOfMemory,
        else => return error.UnableToSpawnCCompiler,
    };
    defer {
        gpa.free(run_res.stdout);
        gpa.free(run_res.stderr);
    }
    switch (run_res.term) {
        .exited => |code| if (code != 0) {
            printVerboseInvocation(argv.items, args.search_basename, args.verbose, run_res.stderr);
            return error.CCompilerExitCode;
        },
        else => {
            printVerboseInvocation(argv.items, args.search_basename, args.verbose, run_res.stderr);
            return error.CCompilerCrashed;
        },
    }

    var it = std.mem.tokenizeAny(u8, run_res.stdout, "\n\r");
    const line = it.next() orelse return error.LibCRuntimeNotFound;
    // When this command fails, it returns exit code 0 and duplicates the input file name.
    // So we detect failure by checking if the output matches exactly the input.
    if (std.mem.eql(u8, line, args.search_basename)) return error.LibCRuntimeNotFound;
    switch (args.want_dirname) {
        .full_path => return gpa.dupe(u8, line),
        .only_dir => {
            const dirname = fs.path.dirname(line) orelse return error.LibCRuntimeNotFound;
            return gpa.dupe(u8, dirname);
        },
    }
}

fn printVerboseInvocation(
    argv: []const []const u8,
    search_basename: ?[]const u8,
    verbose: bool,
    stderr: ?[]const u8,
) void {
    if (!verbose) return;

    if (search_basename) |s| {
        std.debug.print("Zig attempted to find the file '{s}' by executing this command:\n", .{s});
    } else {
        std.debug.print("Zig attempted to find the path to native system libc headers by executing this command:\n", .{});
    }
    for (argv, 0..) |arg, i| {
        if (i != 0) std.debug.print(" ", .{});
        std.debug.print("{s}", .{arg});
    }
    std.debug.print("\n", .{});
    if (stderr) |s| {
        std.debug.print("Output:\n==========\n{s}\n==========\n", .{s});
    }
}

fn fillInstallations(
    installs: *[2]std.zig.WindowsSdk.Installation,
    sdk: std.zig.WindowsSdk,
) []std.zig.WindowsSdk.Installation {
    var installs_len: usize = 0;
    if (sdk.windows10sdk) |windows10sdk| {
        installs[installs_len] = windows10sdk;
        installs_len += 1;
    }
    if (sdk.windows81sdk) |windows81sdk| {
        installs[installs_len] = windows81sdk;
        installs_len += 1;
    }
    return installs[0..installs_len];
}

const inf_loop_env_key = "ZIG_IS_DETECTING_LIBC_PATHS";

fn appendCcExe(
    args: *std.array_list.Managed([]const u8),
    skip_cc_env_var: bool,
    environ_map: *const Environ.Map,
) !void {
    const default_cc_exe = if (is_windows) "cc.exe" else "cc";
    try args.ensureUnusedCapacity(1);
    if (skip_cc_env_var) {
        args.appendAssumeCapacity(default_cc_exe);
        return;
    }
    const cc_env_var = std.zig.EnvVar.CC.get(environ_map) orelse {
        args.appendAssumeCapacity(default_cc_exe);
        return;
    };
    // Respect space-separated flags to the C compiler.
    var it = std.mem.tokenizeScalar(u8, cc_env_var, ' ');
    while (it.next()) |arg| {
        try args.append(arg);
    }
}

/// These are basenames. This data is produced with a pure function. See also
/// `CsuPaths`.
pub const CrtBasenames = struct {
    crt0: ?[]const u8 = null,
    crti: ?[]const u8 = null,
    crtbegin: ?[]const u8 = null,
    crtend: ?[]const u8 = null,
    crtn: ?[]const u8 = null,

    pub const GetArgs = struct {
        target: *const std.Target,
        link_libc: bool,
        output_mode: std.builtin.OutputMode,
        link_mode: std.builtin.LinkMode,
        pie: bool,
    };

    /// Determine file system path names of C runtime startup objects for supported
    /// link modes.
    pub fn get(args: GetArgs) CrtBasenames {
        // crt objects are only required for libc.
        if (!args.link_libc) return .{};

        // Flatten crt cases.
        const mode: enum {
            dynamic_lib,
            dynamic_exe,
            dynamic_pie,
            static_exe,
            static_pie,
        } = switch (args.output_mode) {
            .Obj => return .{},
            .Lib => switch (args.link_mode) {
                .dynamic => .dynamic_lib,
                .static => return .{},
            },
            .Exe => switch (args.link_mode) {
                .dynamic => if (args.pie) .dynamic_pie else .dynamic_exe,
                .static => if (args.pie) .static_pie else .static_exe,
            },
        };

        const target = args.target;

        if (target.abi.isAndroid()) return switch (mode) {
            .dynamic_lib => .{
                .crtbegin = "crtbegin_so.o",
                .crtend = "crtend_so.o",
            },
            .dynamic_exe, .dynamic_pie => .{
                .crtbegin = "crtbegin_dynamic.o",
                .crtend = "crtend_android.o",
            },
            .static_exe, .static_pie => .{
                .crtbegin = "crtbegin_static.o",
                .crtend = "crtend_android.o",
            },
        };

        return switch (target.os.tag) {
            .linux => switch (mode) {
                .dynamic_lib => .{
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
                .dynamic_exe => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
                .dynamic_pie => .{
                    .crt0 = "Scrt1.o",
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
                .static_exe => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
                .static_pie => .{
                    .crt0 = "rcrt1.o",
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
            },
            .dragonfly => switch (mode) {
                .dynamic_lib => .{
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .dynamic_exe => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .dynamic_pie => .{
                    .crt0 = "Scrt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .static_exe => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .static_pie => .{
                    .crt0 = "Scrt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
            },
            .freebsd => switch (mode) {
                .dynamic_lib => .{
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .dynamic_exe => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .dynamic_pie => .{
                    .crt0 = "Scrt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .static_exe => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginT.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .static_pie => .{
                    .crt0 = "Scrt1.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
            },
            .netbsd => switch (mode) {
                .dynamic_lib => .{
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .dynamic_exe => .{
                    .crt0 = "crt0.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .dynamic_pie => .{
                    .crt0 = "crt0.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .static_exe => .{
                    .crt0 = "crt0.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginT.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .static_pie => .{
                    .crt0 = "crt0.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginT.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
            },
            .openbsd => switch (mode) {
                .dynamic_lib => .{
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                },
                .dynamic_exe, .dynamic_pie => .{
                    .crt0 = "crt0.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                },
                .static_exe, .static_pie => .{
                    .crt0 = "rcrt0.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                },
            },
            .haiku => switch (mode) {
                .dynamic_lib => .{
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .dynamic_exe => .{
                    .crt0 = "start_dyn.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .dynamic_pie => .{
                    .crt0 = "start_dyn.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
                .static_exe => .{
                    .crt0 = "start_dyn.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbegin.o",
                    .crtend = "crtend.o",
                    .crtn = "crtn.o",
                },
                .static_pie => .{
                    .crt0 = "start_dyn.o",
                    .crti = "crti.o",
                    .crtbegin = "crtbeginS.o",
                    .crtend = "crtendS.o",
                    .crtn = "crtn.o",
                },
            },
            .illumos => switch (mode) {
                .dynamic_lib => .{
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
                .dynamic_exe, .dynamic_pie => .{
                    .crt0 = "crt1.o",
                    .crti = "crti.o",
                    .crtn = "crtn.o",
                },
                .static_exe, .static_pie => .{},
            },
            else => .{},
        };
    }
};

pub const CrtPaths = struct {
    crt0: ?Path = null,
    crti: ?Path = null,
    crtbegin: ?Path = null,
    crtend: ?Path = null,
    crtn: ?Path = null,
};

pub fn resolveCrtPaths(
    lci: LibCInstallation,
    arena: Allocator,
    crt_basenames: CrtBasenames,
    target: *const std.Target,
) error{ OutOfMemory, LibCInstallationMissingCrtDir }!CrtPaths {
    const crt_dir_path: Path = .{
        .root_dir = std.Build.Cache.Directory.cwd(),
        .sub_path = lci.crt_dir orelse return error.LibCInstallationMissingCrtDir,
    };
    switch (target.os.tag) {
        .dragonfly => {
            const gccv: []const u8 = if (target.os.version_range.semver.isAtLeast(.{
                .major = 5,
                .minor = 4,
                .patch = 0,
            }) orelse true) "gcc80" else "gcc54";
            return .{
                .crt0 = if (crt_basenames.crt0) |basename| try crt_dir_path.join(arena, basename) else null,
                .crti = if (crt_basenames.crti) |basename| try crt_dir_path.join(arena, basename) else null,
                .crtbegin = if (crt_basenames.crtbegin) |basename| .{
                    .root_dir = crt_dir_path.root_dir,
                    .sub_path = try fs.path.join(arena, &.{ crt_dir_path.sub_path, gccv, basename }),
                } else null,
                .crtend = if (crt_basenames.crtend) |basename| .{
                    .root_dir = crt_dir_path.root_dir,
                    .sub_path = try fs.path.join(arena, &.{ crt_dir_path.sub_path, gccv, basename }),
                } else null,
                .crtn = if (crt_basenames.crtn) |basename| try crt_dir_path.join(arena, basename) else null,
            };
        },
        .haiku => {
            const gcc_dir_path: Path = .{
                .root_dir = std.Build.Cache.Directory.cwd(),
                .sub_path = lci.gcc_dir orelse return error.LibCInstallationMissingCrtDir,
            };
            return .{
                .crt0 = if (crt_basenames.crt0) |basename| try crt_dir_path.join(arena, basename) else null,
                .crti = if (crt_basenames.crti) |basename| try crt_dir_path.join(arena, basename) else null,
                .crtbegin = if (crt_basenames.crtbegin) |basename| try gcc_dir_path.join(arena, basename) else null,
                .crtend = if (crt_basenames.crtend) |basename| try gcc_dir_path.join(arena, basename) else null,
                .crtn = if (crt_basenames.crtn) |basename| try crt_dir_path.join(arena, basename) else null,
            };
        },
        else => {
            return .{
                .crt0 = if (crt_basenames.crt0) |basename| try crt_dir_path.join(arena, basename) else null,
                .crti = if (crt_basenames.crti) |basename| try crt_dir_path.join(arena, basename) else null,
                .crtbegin = if (crt_basenames.crtbegin) |basename| try crt_dir_path.join(arena, basename) else null,
                .crtend = if (crt_basenames.crtend) |basename| try crt_dir_path.join(arena, basename) else null,
                .crtn = if (crt_basenames.crtn) |basename| try crt_dir_path.join(arena, basename) else null,
            };
        },
    }
}



---
File: /std/zig/llvm.zig
---

pub const BitcodeReader = @import("llvm/BitcodeReader.zig");
pub const bitcode_writer = @import("llvm/bitcode_writer.zig");
pub const Builder = @import("llvm/Builder.zig");

test {
    _ = BitcodeReader;
    _ = bitcode_writer;
    _ = Builder;
}



---
File: /std/zig/number_literal.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;
const utf8Decode = std.unicode.utf8Decode;
const utf8Encode = std.unicode.utf8Encode;

pub const ParseError = error{
    OutOfMemory,
    InvalidLiteral,
};

pub const Base = enum(u8) { decimal = 10, hex = 16, binary = 2, octal = 8 };
pub const FloatBase = enum(u8) { decimal = 10, hex = 16 };

pub const Result = union(enum) {
    /// Result fits if it fits in u64
    int: u64,
    /// Result is an int that doesn't fit in u64. Payload is the base, if it is
    /// not `.decimal` then the slice has a two character prefix.
    big_int: Base,
    /// Result is a float. Payload is the base, if it is not `.decimal` then
    /// the slice has a two character prefix.
    float: FloatBase,
    failure: Error,
};

pub const Error = union(enum) {
    /// The number has leading zeroes.
    leading_zero,
    /// Expected a digit after base prefix.
    digit_after_base,
    /// The base prefix is in uppercase.
    upper_case_base: usize,
    /// Float literal has an invalid base prefix.
    invalid_float_base: usize,
    /// Repeated '_' digit separator.
    repeated_underscore: usize,
    /// '_' digit separator after special character (+-.)
    invalid_underscore_after_special: usize,
    /// Invalid digit for the specified base.
    invalid_digit: struct { i: usize, base: Base },
    /// Invalid digit for an exponent.
    invalid_digit_exponent: usize,
    /// Float literal has multiple periods.
    duplicate_period,
    /// Float literal has multiple exponents.
    duplicate_exponent: usize,
    /// Exponent comes directly after '_' digit separator.
    exponent_after_underscore: usize,
    /// Special character (+-.) comes directly after exponent.
    special_after_underscore: usize,
    /// Number ends in special character (+-.)
    trailing_special: usize,
    /// Number ends in '_' digit separator.
    trailing_underscore: usize,
    /// Character not in [0-9a-zA-Z.+-_]
    invalid_character: usize,
    /// [+-] not immediately after [pPeE]
    invalid_exponent_sign: usize,
    /// Period comes directly after exponent.
    period_after_exponent: usize,
};

/// Parse Zig number literal accepted by fmt.parseInt, fmt.parseFloat and big_int.setString.
/// Valid for any input.
pub fn parseNumberLiteral(bytes: []const u8) Result {
    var i: usize = 0;
    var base: u8 = 10;
    if (bytes.len >= 2 and bytes[0] == '0') switch (bytes[1]) {
        'b' => {
            base = 2;
            i = 2;
        },
        'o' => {
            base = 8;
            i = 2;
        },
        'x' => {
            base = 16;
            i = 2;
        },
        'B', 'O', 'X' => return .{ .failure = .{ .upper_case_base = 1 } },
        '.', 'e', 'E' => {},
        else => return .{ .failure = .leading_zero },
    };
    if (bytes.len == 2 and base != 10) return .{ .failure = .digit_after_base };

    var x: u64 = 0;
    var overflow = false;
    var underscore = false;
    var period = false;
    var special: u8 = 0;
    var exponent = false;
    var float = false;
    while (i < bytes.len) : (i += 1) {
        const c = bytes[i];
        switch (c) {
            '_' => {
                if (i == 2 and base != 10) return .{ .failure = .{ .invalid_underscore_after_special = i } };
                if (special != 0) return .{ .failure = .{ .invalid_underscore_after_special = i } };
                if (underscore) return .{ .failure = .{ .repeated_underscore = i } };
                underscore = true;
                continue;
            },
            'e', 'E' => if (base == 10) {
                float = true;
                if (exponent) return .{ .failure = .{ .duplicate_exponent = i } };
                if (underscore) return .{ .failure = .{ .exponent_after_underscore = i } };
                special = c;
                exponent = true;
                continue;
            },
            'p', 'P' => if (base == 16) {
                if (i == 2) {
                    return .{ .failure = .{ .digit_after_base = {} } };
                }
                float = true;
                if (exponent) return .{ .failure = .{ .duplicate_exponent = i } };
                if (underscore) return .{ .failure = .{ .exponent_after_underscore = i } };
                special = c;
                exponent = true;
                continue;
            },
            '.' => {
                if (exponent) {
                    const digit_index = i - ".e".len;
                    if (digit_index < bytes.len) {
                        switch (bytes[digit_index]) {
                            '0'...'9' => return .{ .failure = .{ .period_after_exponent = i } },
                            else => {},
                        }
                    }
                }
                float = true;
                if (base != 10 and base != 16) return .{ .failure = .{ .invalid_float_base = 2 } };
                if (period) return .{ .failure = .duplicate_period };
                period = true;
                if (underscore) return .{ .failure = .{ .special_after_underscore = i } };
                special = c;
                continue;
            },
            '+', '-' => {
                switch (special) {
                    'p', 'P' => {},
                    'e', 'E' => if (base != 10) return .{ .failure = .{ .invalid_exponent_sign = i } },
                    else => return .{ .failure = .{ .invalid_exponent_sign = i } },
                }
                special = c;
                continue;
            },
            else => {},
        }
        const digit = switch (c) {
            '0'...'9' => c - '0',
            'A'...'Z' => c - 'A' + 10,
            'a'...'z' => c - 'a' + 10,
            else => return .{ .failure = .{ .invalid_character = i } },
        };
        if (digit >= base) return .{ .failure = .{ .invalid_digit = .{ .i = i, .base = @as(Base, @enumFromInt(base)) } } };
        if (exponent and digit >= 10) return .{ .failure = .{ .invalid_digit_exponent = i } };
        underscore = false;
        special = 0;

        if (float) continue;
        if (x != 0) {
            const res = @mulWithOverflow(x, base);
            if (res[1] != 0) overflow = true;
            x = res[0];
        }
        const res = @addWithOverflow(x, digit);
        if (res[1] != 0) overflow = true;
        x = res[0];
    }
    if (underscore) return .{ .failure = .{ .trailing_underscore = bytes.len - 1 } };
    if (special != 0) return .{ .failure = .{ .trailing_special = bytes.len - 1 } };

    if (float) return .{ .float = @as(FloatBase, @enumFromInt(base)) };
    if (overflow) return .{ .big_int = @as(Base, @enumFromInt(base)) };
    return .{ .int = x };
}



---
File: /std/zig/Parse.zig
---

//! Represents in-progress parsing, will be converted to an Ast after completion.

pub const Error = error{ParseError} || Allocator.Error;

gpa: Allocator,
source: []const u8,
tokens: Ast.TokenList.Slice,
tok_i: TokenIndex,
errors: std.ArrayList(AstError),
nodes: Ast.NodeList,
extra_data: std.ArrayList(u32),
scratch: std.ArrayList(Node.Index),

fn tokenTag(p: *const Parse, token_index: TokenIndex) Token.Tag {
    return p.tokens.items(.tag)[token_index];
}

fn tokenStart(p: *const Parse, token_index: TokenIndex) Ast.ByteOffset {
    return p.tokens.items(.start)[token_index];
}

fn nodeTag(p: *const Parse, node: Node.Index) Node.Tag {
    return p.nodes.items(.tag)[@intFromEnum(node)];
}

fn nodeMainToken(p: *const Parse, node: Node.Index) TokenIndex {
    return p.nodes.items(.main_token)[@intFromEnum(node)];
}

fn nodeData(p: *const Parse, node: Node.Index) Node.Data {
    return p.nodes.items(.data)[@intFromEnum(node)];
}

const SmallSpan = union(enum) {
    zero_or_one: Node.OptionalIndex,
    multi: Node.SubRange,
};

const Members = struct {
    len: usize,
    /// Must be either `.opt_node_and_opt_node` if `len <= 2` or `.extra_range` otherwise.
    data: Node.Data,
    trailing: bool,

    fn toSpan(self: Members, p: *Parse) !Node.SubRange {
        return switch (self.len) {
            0 => p.listToSpan(&.{}),
            1 => p.listToSpan(&.{self.data.opt_node_and_opt_node[0].unwrap().?}),
            2 => p.listToSpan(&.{ self.data.opt_node_and_opt_node[0].unwrap().?, self.data.opt_node_and_opt_node[1].unwrap().? }),
            else => self.data.extra_range,
        };
    }
};

fn listToSpan(p: *Parse, list: []const Node.Index) Allocator.Error!Node.SubRange {
    try p.extra_data.appendSlice(p.gpa, @ptrCast(list));
    return .{
        .start = @enumFromInt(p.extra_data.items.len - list.len),
        .end = @enumFromInt(p.extra_data.items.len),
    };
}

fn addNode(p: *Parse, elem: Ast.Node) Allocator.Error!Node.Index {
    const result: Node.Index = @enumFromInt(p.nodes.len);
    try p.nodes.append(p.gpa, elem);
    return result;
}

fn setNode(p: *Parse, i: usize, elem: Ast.Node) Node.Index {
    p.nodes.set(i, elem);
    return @enumFromInt(i);
}

fn reserveNode(p: *Parse, tag: Ast.Node.Tag) !usize {
    try p.nodes.resize(p.gpa, p.nodes.len + 1);
    p.nodes.items(.tag)[p.nodes.len - 1] = tag;
    return p.nodes.len - 1;
}

fn unreserveNode(p: *Parse, node_index: usize) void {
    if (p.nodes.len == node_index) {
        p.nodes.resize(p.gpa, p.nodes.len - 1) catch unreachable;
    } else {
        // There is zombie node left in the tree, let's make it as inoffensive as possible
        // (sadly there's no no-op node)
        p.nodes.items(.tag)[node_index] = .unreachable_literal;
        p.nodes.items(.main_token)[node_index] = p.tok_i;
    }
}

fn addExtra(p: *Parse, extra: anytype) Allocator.Error!ExtraIndex {
    const fields = std.meta.fields(@TypeOf(extra));
    try p.extra_data.ensureUnusedCapacity(p.gpa, fields.len);
    const result: ExtraIndex = @enumFromInt(p.extra_data.items.len);
    inline for (fields) |field| {
        const data: u32 = switch (field.type) {
            Node.Index,
            Node.OptionalIndex,
            OptionalTokenIndex,
            ExtraIndex,
            => @intFromEnum(@field(extra, field.name)),
            TokenIndex,
            => @field(extra, field.name),
            else => @compileError("unexpected field type"),
        };
        p.extra_data.appendAssumeCapacity(data);
    }
    return result;
}

fn warnExpected(p: *Parse, expected_token: Token.Tag) error{OutOfMemory}!void {
    @branchHint(.cold);
    try p.warnMsg(.{
        .tag = .expected_token,
        .token = p.tok_i,
        .extra = .{ .expected_tag = expected_token },
    });
}

fn warn(p: *Parse, error_tag: AstError.Tag) error{OutOfMemory}!void {
    @branchHint(.cold);
    try p.warnMsg(.{ .tag = error_tag, .token = p.tok_i });
}

fn warnMsg(p: *Parse, msg: Ast.Error) error{OutOfMemory}!void {
    @branchHint(.cold);
    switch (msg.tag) {
        .expected_semi_after_decl,
        .expected_semi_after_stmt,
        .expected_comma_after_field,
        .expected_comma_after_arg,
        .expected_comma_after_param,
        .expected_comma_after_initializer,
        .expected_comma_after_switch_prong,
        .expected_comma_after_for_operand,
        .expected_comma_after_capture,
        .expected_semi_or_else,
        .expected_semi_or_lbrace,
        .expected_token,
        .expected_block,
        .expected_block_or_assignment,
        .expected_block_or_expr,
        .expected_block_or_field,
        .expected_expr,
        .expected_expr_or_assignment,
        .expected_fn,
        .expected_inlinable,
        .expected_labelable,
        .expected_param_list,
        .expected_prefix_expr,
        .expected_primary_type_expr,
        .expected_pub_item,
        .expected_return_type,
        .expected_suffix_op,
        .expected_type_expr,
        .expected_var_decl,
        .expected_var_decl_or_fn,
        .expected_loop_payload,
        .expected_container,
        => if (msg.token != 0 and !p.tokensOnSameLine(msg.token - 1, msg.token)) {
            var copy = msg;
            copy.token_is_prev = true;
            copy.token -= 1;
            return p.errors.append(p.gpa, copy);
        },
        else => {},
    }
    try p.errors.append(p.gpa, msg);
}

fn fail(p: *Parse, tag: Ast.Error.Tag) error{ ParseError, OutOfMemory } {
    @branchHint(.cold);
    return p.failMsg(.{ .tag = tag, .token = p.tok_i });
}

fn failExpected(p: *Parse, expected_token: Token.Tag) error{ ParseError, OutOfMemory } {
    @branchHint(.cold);
    return p.failMsg(.{
        .tag = .expected_token,
        .token = p.tok_i,
        .extra = .{ .expected_tag = expected_token },
    });
}

fn failMsg(p: *Parse, msg: Ast.Error) error{ ParseError, OutOfMemory } {
    @branchHint(.cold);
    try p.warnMsg(msg);
    return error.ParseError;
}

/// Root <- skip ContainerMembers eof
pub fn parseRoot(p: *Parse) !void {
    // Root node must be index 0.
    p.nodes.appendAssumeCapacity(.{
        .tag = .root,
        .main_token = 0,
        .data = undefined,
    });
    const root_members = try p.parseContainerMembers();
    const root_decls = try root_members.toSpan(p);
    if (p.tokenTag(p.tok_i) != .eof) {
        try p.warnExpected(.eof);
    }
    p.nodes.items(.data)[0] = .{ .extra_range = root_decls };
}

/// Parse in ZON mode. Subset of the language.
/// TODO: set a flag in Parse struct, and honor that flag
/// by emitting compilation errors when non-zon nodes are encountered.
pub fn parseZon(p: *Parse) !void {
    // We must use index 0 so that 0 can be used as null elsewhere.
    p.nodes.appendAssumeCapacity(.{
        .tag = .root,
        .main_token = 0,
        .data = undefined,
    });
    const node_index = p.expectExpr() catch |err| switch (err) {
        error.ParseError => {
            assert(p.errors.items.len > 0);
            return;
        },
        else => |e| return e,
    };
    if (p.tokenTag(p.tok_i) != .eof) {
        try p.warnExpected(.eof);
    }
    p.nodes.items(.data)[0] = .{ .node = node_index };
}

/// ContainerMembers <- container_doc_comment? ContainerDeclaration* (ContainerField COMMA)* (ContainerField / ContainerDeclaration*)
///
/// ContainerDeclaration <- TestDecl / ComptimeDecl / doc_comment? KEYWORD_pub? Decl
///
/// ComptimeDecl <- KEYWORD_comptime Block
fn parseContainerMembers(p: *Parse) Allocator.Error!Members {
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);

    var field_state: union(enum) {
        /// No fields have been seen.
        none,
        /// Currently parsing fields.
        seen,
        /// Saw fields and then a declaration after them.
        /// Payload is first token of previous declaration.
        end: Node.Index,
        /// There was a declaration between fields, don't report more errors.
        err,
    } = .none;

    var last_field: TokenIndex = undefined;

    // Skip container doc comments.
    while (p.eatToken(.container_doc_comment)) |_| {}

    var trailing = false;
    while (true) {
        const doc_comment = try p.eatDocComments();

        sw: switch (p.tokenTag(p.tok_i)) {
            .keyword_test => {
                if (doc_comment) |some| {
                    try p.warnMsg(.{ .tag = .test_doc_comment, .token = some });
                }
                const maybe_test_decl_node = try p.expectTestDeclRecoverable();
                if (maybe_test_decl_node) |test_decl_node| {
                    if (field_state == .seen) {
                        field_state = .{ .end = test_decl_node };
                    }
                    try p.scratch.append(p.gpa, test_decl_node);
                }
                trailing = false;
            },
            .keyword_comptime => switch (p.tokenTag(p.tok_i + 1)) {
                .l_brace => {
                    if (doc_comment) |some| {
                        try p.warnMsg(.{ .tag = .comptime_doc_comment, .token = some });
                    }
                    const comptime_token = p.nextToken();
                    const opt_block = p.parseBlock() catch |err| switch (err) {
                        error.OutOfMemory => return error.OutOfMemory,
                        error.ParseError => blk: {
                            p.findNextContainerMember();
                            break :blk null;
                        },
                    };
                    if (opt_block) |block| {
                        const comptime_node = try p.addNode(.{
                            .tag = .@"comptime",
                            .main_token = comptime_token,
                            .data = .{ .node = block },
                        });
                        if (field_state == .seen) {
                            field_state = .{ .end = comptime_node };
                        }
                        try p.scratch.append(p.gpa, comptime_node);
                    }
                    trailing = false;
                },
                else => {
                    const identifier = p.tok_i;
                    defer last_field = identifier;
                    const container_field = p.expectContainerField() catch |err| switch (err) {
                        error.OutOfMemory => return error.OutOfMemory,
                        error.ParseError => {
                            p.findNextContainerMember();
                            continue;
                        },
                    };
                    switch (field_state) {
                        .none => field_state = .seen,
                        .err, .seen => {},
                        .end => |node| {
                            try p.warnMsg(.{
                                .tag = .decl_between_fields,
                                .token = p.nodeMainToken(node),
                            });
                            try p.warnMsg(.{
                                .tag = .previous_field,
                                .is_note = true,
                                .token = last_field,
                            });
                            try p.warnMsg(.{
                                .tag = .next_field,
                                .is_note = true,
                                .token = identifier,
                            });
                            // Continue parsing; error will be reported later.
                            field_state = .err;
                        },
                    }
                    try p.scratch.append(p.gpa, container_field);
                    switch (p.tokenTag(p.tok_i)) {
                        .comma => {
                            p.tok_i += 1;
                            trailing = true;
                            continue;
                        },
                        .r_brace, .eof => {
                            trailing = false;
                            break;
                        },
                        else => {},
                    }
                    // There is not allowed to be a decl after a field with no comma.
                    // Report error but recover parser.
                    try p.warn(.expected_comma_after_field);
                    p.findNextContainerMember();
                },
            },
            .keyword_pub,
            .keyword_const,
            .keyword_var,
            .keyword_threadlocal,
            .keyword_export,
            .keyword_extern,
            .keyword_inline,
            .keyword_noinline,
            .keyword_fn,
            => |t| {
                if (t == .keyword_extern) {
                    switch (p.tokenTag(p.tok_i + 1)) {
                        .keyword_struct,
                        .keyword_union,
                        .keyword_enum,
                        .keyword_opaque,
                        => |ct| continue :sw ct,
                        else => {},
                    }
                }
                if (t == .keyword_inline) {
                    switch (p.tokenTag(p.tok_i + 1)) {
                        .keyword_for,
                        .keyword_while,
                        => |ct| continue :sw ct,
                        else => {},
                    }
                }

                p.tok_i += @intFromBool(t == .keyword_pub);
                const opt_top_level_decl = try p.expectTopLevelDeclRecoverable();
                if (opt_top_level_decl) |top_level_decl| {
                    if (field_state == .seen) {
                        field_state = .{ .end = top_level_decl };
                    }
                    try p.scratch.append(p.gpa, top_level_decl);
                }
                trailing = p.tokenTag(p.tok_i - 1) == .semicolon;
            },
            .eof, .r_brace => {
                if (doc_comment) |tok| {
                    try p.warnMsg(.{
                        .tag = .unattached_doc_comment,
                        .token = tok,
                    });
                }
                break;
            },
            else => {
                const c_container = p.parseCStyleContainer() catch |err| switch (err) {
                    error.OutOfMemory => return error.OutOfMemory,
                    error.ParseError => false,
                };
                if (c_container) continue;

                const identifier = p.tok_i;
                defer last_field = identifier;
                const container_field = p.expectContainerField() catch |err| switch (err) {
                    error.OutOfMemory => return error.OutOfMemory,
                    error.ParseError => {
                        p.findNextContainerMember();
                        continue;
                    },
                };
                switch (field_state) {
                    .none => field_state = .seen,
                    .err, .seen => {},
                    .end => |node| {
                        try p.warnMsg(.{
                            .tag = .decl_between_fields,
                            .token = p.nodeMainToken(node),
                        });
                        try p.warnMsg(.{
                            .tag = .previous_field,
                            .is_note = true,
                            .token = last_field,
                        });
                        try p.warnMsg(.{
                            .tag = .next_field,
                            .is_note = true,
                            .token = identifier,
                        });
                        // Continue parsing; error will be reported later.
                        field_state = .err;
                    },
                }
                try p.scratch.append(p.gpa, container_field);
                switch (p.tokenTag(p.tok_i)) {
                    .comma => {
                        p.tok_i += 1;
                        trailing = true;
                        continue;
                    },
                    .r_brace, .eof => {
                        trailing = false;
                        break;
                    },
                    else => {},
                }
                // There is not allowed to be a decl after a field with no comma.
                // Report error but recover parser.
                try p.warn(.expected_comma_after_field);
                if (p.tokenTag(p.tok_i) == .semicolon and p.tokenTag(identifier) == .identifier) {
                    try p.warnMsg(.{
                        .tag = .var_const_decl,
                        .is_note = true,
                        .token = identifier,
                    });
                }
                p.findNextContainerMember();
                continue;
            },
        }
    }

    const items = p.scratch.items[scratch_top..];
    if (items.len <= 2) {
        return Members{
            .len = items.len,
            .data = .{ .opt_node_and_opt_node = .{
                if (items.len >= 1) items[0].toOptional() else .none,
                if (items.len >= 2) items[1].toOptional() else .none,
            } },
            .trailing = trailing,
        };
    } else {
        return Members{
            .len = items.len,
            .data = .{ .extra_range = try p.listToSpan(items) },
            .trailing = trailing,
        };
    }
}

/// Attempts to find next container member by searching for certain tokens
fn findNextContainerMember(p: *Parse) void {
    var level: u32 = 0;
    while (true) {
        const tok = p.nextToken();
        switch (p.tokenTag(tok)) {
            // Any of these can start a new top level declaration.
            .keyword_test,
            .keyword_comptime,
            .keyword_pub,
            .keyword_export,
            .keyword_extern,
            .keyword_inline,
            .keyword_noinline,
            .keyword_threadlocal,
            .keyword_const,
            .keyword_var,
            .keyword_fn,
            => {
                if (level == 0) {
                    p.tok_i -= 1;
                    return;
                }
            },
            .identifier => {
                if (p.tokenTag(tok + 1) == .comma and level == 0) {
                    p.tok_i -= 1;
                    return;
                }
            },
            .comma, .semicolon => {
                // this decl was likely meant to end here
                if (level == 0) {
                    return;
                }
            },
            .l_paren, .l_bracket, .l_brace => level += 1,
            .r_paren, .r_bracket => {
                if (level != 0) level -= 1;
            },
            .r_brace => {
                if (level == 0) {
                    // end of container, exit
                    p.tok_i -= 1;
                    return;
                }
                level -= 1;
            },
            .eof => {
                p.tok_i -= 1;
                return;
            },
            else => {},
        }
    }
}

/// Attempts to find the next statement by searching for a semicolon
fn findNextStmt(p: *Parse) void {
    var level: u32 = 0;
    while (true) {
        const tok = p.nextToken();
        switch (p.tokenTag(tok)) {
            .l_brace => level += 1,
            .r_brace => {
                if (level == 0) {
                    p.tok_i -= 1;
                    return;
                }
                level -= 1;
            },
            .semicolon => {
                if (level == 0) {
                    return;
                }
            },
            .eof => {
                p.tok_i -= 1;
                return;
            },
            else => {},
        }
    }
}

/// TestDecl <- KEYWORD_test (STRINGLITERALSINGLE / IDENTIFIER)? Block
fn expectTestDecl(p: *Parse) Error!Node.Index {
    const test_token = p.assertToken(.keyword_test);
    const name_token: OptionalTokenIndex = switch (p.tokenTag(p.tok_i)) {
        .string_literal, .identifier => .fromToken(p.nextToken()),
        else => .none,
    };
    const block_node = try p.parseBlock() orelse return p.fail(.expected_block);
    return p.addNode(.{
        .tag = .test_decl,
        .main_token = test_token,
        .data = .{ .opt_token_and_node = .{
            name_token,
            block_node,
        } },
    });
}

fn expectTestDeclRecoverable(p: *Parse) error{OutOfMemory}!?Node.Index {
    if (p.expectTestDecl()) |node| {
        return node;
    } else |err| switch (err) {
        error.OutOfMemory => return error.OutOfMemory,
        error.ParseError => {
            p.findNextContainerMember();
            return null;
        },
    }
}

/// Decl
///     <- (KEYWORD_export / KEYWORD_inline / KEYWORD_noinline)? FnProto (SEMICOLON / Block)
///      / KEYWORD_extern STRINGLITERALSINGLE? FnProto SEMICOLON
///      / (KEYWORD_export / KEYWORD_extern STRINGLITERALSINGLE?)? KEYWORD_threadlocal? VarDecl
fn expectTopLevelDecl(p: *Parse) !?Node.Index {
    const extern_export_inline_token = p.nextToken();
    var is_extern: bool = false;
    var expect_fn: bool = false;
    var expect_var_or_fn: bool = false;
    switch (p.tokenTag(extern_export_inline_token)) {
        .keyword_extern => {
            _ = p.eatToken(.string_literal);
            is_extern = true;
            expect_var_or_fn = true;
        },
        .keyword_export => expect_var_or_fn = true,
        .keyword_inline, .keyword_noinline => expect_fn = true,
        else => p.tok_i -= 1,
    }
    const opt_fn_proto = try p.parseFnProto();
    if (opt_fn_proto) |fn_proto| {
        switch (p.tokenTag(p.tok_i)) {
            .semicolon => {
                p.tok_i += 1;
                return fn_proto;
            },
            .l_brace => {
                if (is_extern) {
                    try p.warnMsg(.{ .tag = .extern_fn_body, .token = extern_export_inline_token });
                    return null;
                }
                const fn_decl_index = try p.reserveNode(.fn_decl);
                errdefer p.unreserveNode(fn_decl_index);

                const body_block = try p.parseBlock();
                return p.setNode(fn_decl_index, .{
                    .tag = .fn_decl,
                    .main_token = p.nodeMainToken(fn_proto),
                    .data = .{ .node_and_node = .{
                        fn_proto,
                        body_block.?,
                    } },
                });
            },
            else => {
                // Since parseBlock only return error.ParseError on
                // a missing '}' we can assume this function was
                // supposed to end here.
                try p.warn(.expected_semi_or_lbrace);
                return null;
            },
        }
    }
    if (expect_fn) {
        try p.warn(.expected_fn);
        return error.ParseError;
    }

    const thread_local_token = p.eatToken(.keyword_threadlocal);
    if (try p.parseGlobalVarDecl()) |var_decl| return var_decl;
    if (thread_local_token != null) {
        return p.fail(.expected_var_decl);
    }
    if (expect_var_or_fn) {
        return p.fail(.expected_var_decl_or_fn);
    }
    return p.fail(.expected_pub_item);
}

fn expectTopLevelDeclRecoverable(p: *Parse) error{OutOfMemory}!?Node.Index {
    return p.expectTopLevelDecl() catch |err| switch (err) {
        error.OutOfMemory => return error.OutOfMemory,
        error.ParseError => {
            p.findNextContainerMember();
            return null;
        },
    };
}

/// FnProto <- KEYWORD_fn IDENTIFIER? LPAREN ParamDeclList RPAREN ByteAlign? AddrSpace? LinkSection? CallConv? EXCLAMATIONMARK? TypeExpr !ExprSuffix
fn parseFnProto(p: *Parse) !?Node.Index {
    const fn_token = p.eatToken(.keyword_fn) orelse return null;

    // We want the fn proto node to be before its children in the array.
    const fn_proto_index = try p.reserveNode(.fn_proto);
    errdefer p.unreserveNode(fn_proto_index);

    _ = p.eatToken(.identifier);
    const params = try p.parseParamDeclList();
    const align_expr = try p.parseByteAlign();
    const addrspace_expr = try p.parseAddrSpace();
    const section_expr = try p.parseLinkSection();
    const callconv_expr = try p.parseCallconv();
    _ = p.eatToken(.bang);

    const return_type_expr = try p.parseTypeExpr();
    if (return_type_expr == null) {
        // most likely the user forgot to specify the return type.
        // Mark return type as invalid and try to continue.
        try p.warn(.expected_return_type);
    }

    if (align_expr == null and section_expr == null and callconv_expr == null and addrspace_expr == null) {
        switch (params) {
            .zero_or_one => |param| return p.setNode(fn_proto_index, .{
                .tag = .fn_proto_simple,
                .main_token = fn_token,
                .data = .{ .opt_node_and_opt_node = .{
                    param,
                    .fromOptional(return_type_expr),
                } },
            }),
            .multi => |span| {
                return p.setNode(fn_proto_index, .{
                    .tag = .fn_proto_multi,
                    .main_token = fn_token,
                    .data = .{ .extra_and_opt_node = .{
                        try p.addExtra(Node.SubRange{
                            .start = span.start,
                            .end = span.end,
                        }),
                        .fromOptional(return_type_expr),
                    } },
                });
            },
        }
    }
    switch (params) {
        .zero_or_one => |param| return p.setNode(fn_proto_index, .{
            .tag = .fn_proto_one,
            .main_token = fn_token,
            .data = .{ .extra_and_opt_node = .{
                try p.addExtra(Node.FnProtoOne{
                    .param = param,
                    .align_expr = .fromOptional(align_expr),
                    .addrspace_expr = .fromOptional(addrspace_expr),
                    .section_expr = .fromOptional(section_expr),
                    .callconv_expr = .fromOptional(callconv_expr),
                }),
                .fromOptional(return_type_expr),
            } },
        }),
        .multi => |span| {
            return p.setNode(fn_proto_index, .{
                .tag = .fn_proto,
                .main_token = fn_token,
                .data = .{ .extra_and_opt_node = .{
                    try p.addExtra(Node.FnProto{
                        .params_start = span.start,
                        .params_end = span.end,
                        .align_expr = .fromOptional(align_expr),
                        .addrspace_expr = .fromOptional(addrspace_expr),
                        .section_expr = .fromOptional(section_expr),
                        .callconv_expr = .fromOptional(callconv_expr),
                    }),
                    .fromOptional(return_type_expr),
                } },
            });
        },
    }
}

fn setVarDeclInitExpr(p: *Parse, var_decl: Node.Index, init_expr: Node.OptionalIndex) void {
    const init_expr_result = switch (p.nodeTag(var_decl)) {
        .simple_var_decl => &p.nodes.items(.data)[@intFromEnum(var_decl)].opt_node_and_opt_node[1],
        .aligned_var_decl => &p.nodes.items(.data)[@intFromEnum(var_decl)].node_and_opt_node[1],
        .local_var_decl, .global_var_decl => &p.nodes.items(.data)[@intFromEnum(var_decl)].extra_and_opt_node[1],
        else => unreachable,
    };
    init_expr_result.* = init_expr;
}

/// VarDeclProto <- (KEYWORD_const / KEYWORD_var) IDENTIFIER (COLON TypeExpr)? ByteAlign? AddrSpace? LinkSection?
/// Returns a `*_var_decl` node with its rhs (init expression) initialized to .none.
fn parseVarDeclProto(p: *Parse) !?Node.Index {
    const mut_token = p.eatToken(.keyword_const) orelse
        p.eatToken(.keyword_var) orelse
        return null;

    _ = try p.expectToken(.identifier);
    const opt_type_node = if (p.eatToken(.colon) == null) null else try p.expectTypeExpr();
    const opt_align_node = try p.parseByteAlign();
    const opt_addrspace_node = try p.parseAddrSpace();
    const opt_section_node = try p.parseLinkSection();

    if (opt_section_node == null and opt_addrspace_node == null) {
        const align_node = opt_align_node orelse {
            return try p.addNode(.{
                .tag = .simple_var_decl,
                .main_token = mut_token,
                .data = .{
                    .opt_node_and_opt_node = .{
                        .fromOptional(opt_type_node),
                        .none, // set later with `setVarDeclInitExpr
                    },
                },
            });
        };

        const type_node = opt_type_node orelse {
            return try p.addNode(.{
                .tag = .aligned_var_decl,
                .main_token = mut_token,
                .data = .{
                    .node_and_opt_node = .{
                        align_node,
                        .none, // set later with `setVarDeclInitExpr
                    },
                },
            });
        };

        return try p.addNode(.{
            .tag = .local_var_decl,
            .main_token = mut_token,
            .data = .{
                .extra_and_opt_node = .{
                    try p.addExtra(Node.LocalVarDecl{
                        .type_node = type_node,
                        .align_node = align_node,
                    }),
                    .none, // set later with `setVarDeclInitExpr
                },
            },
        });
    } else {
        return try p.addNode(.{
            .tag = .global_var_decl,
            .main_token = mut_token,
            .data = .{
                .extra_and_opt_node = .{
                    try p.addExtra(Node.GlobalVarDecl{
                        .type_node = .fromOptional(opt_type_node),
                        .align_node = .fromOptional(opt_align_node),
                        .addrspace_node = .fromOptional(opt_addrspace_node),
                        .section_node = .fromOptional(opt_section_node),
                    }),
                    .none, // set later with `setVarDeclInitExpr
                },
            },
        });
    }
}

/// GlobalVarDecl <- VarDeclProto (EQUAL Expr?) SEMICOLON
fn parseGlobalVarDecl(p: *Parse) !?Node.Index {
    const var_decl = try p.parseVarDeclProto() orelse return null;

    const init_node: ?Node.Index = switch (p.tokenTag(p.tok_i)) {
        .equal_equal => blk: {
            try p.warn(.wrong_equal_var_decl);
            p.tok_i += 1;
            break :blk try p.expectExpr();
        },
        .equal => blk: {
            p.tok_i += 1;
            break :blk try p.expectExpr();
        },
        else => null,
    };

    p.setVarDeclInitExpr(var_decl, .fromOptional(init_node));

    try p.expectSemicolon(.expected_semi_after_decl, false);
    return var_decl;
}

/// ContainerField <- doc_comment? (KEYWORD_comptime / !KEYWORD_comptime) !KEYWORD_fn (IDENTIFIER COLON / !(IDENTIFIER COLON))? TypeExpr ByteAlign? (EQUAL Expr)?
fn expectContainerField(p: *Parse) !Node.Index {
    _ = p.eatToken(.keyword_comptime);
    const main_token = p.tok_i;
    _ = p.eatTokens(&.{ .identifier, .colon });
    const type_expr = try p.expectTypeExpr();
    const align_expr = try p.parseByteAlign();
    const value_expr = if (p.eatToken(.equal) == null) null else try p.expectExpr();

    if (align_expr == null) {
        return p.addNode(.{
            .tag = .container_field_init,
            .main_token = main_token,
            .data = .{ .node_and_opt_node = .{
                type_expr,
                .fromOptional(value_expr),
            } },
        });
    } else if (value_expr == null) {
        return p.addNode(.{
            .tag = .container_field_align,
            .main_token = main_token,
            .data = .{ .node_and_node = .{
                type_expr,
                align_expr.?,
            } },
        });
    } else {
        return p.addNode(.{
            .tag = .container_field,
            .main_token = main_token,
            .data = .{ .node_and_extra = .{
                type_expr,
                try p.addExtra(Node.ContainerField{
                    .align_expr = align_expr.?,
                    .value_expr = value_expr.?,
                }),
            } },
        });
    }
}

/// BlockStatement
///     <- Statement
///      / KEYWORD_defer BlockExprStatement
///      / KEYWORD_errdefer Payload? BlockExprStatement
///      / !ExprStatement (KEYWORD_comptime !BlockExpr)? VarAssignStatement
///
/// Statement
///     <- ExprStatement
///      / KEYWORD_suspend BlockExprStatement
///      / !ExprStatement (KEYWORD_comptime !BlockExpr)? AssignExpr SEMICOLON
///
/// ExprStatement
///     <- IfStatement
///      / LabeledStatement
///      / KEYWORD_nosuspend BlockExprStatement
///      / KEYWORD_comptime BlockExpr
fn expectStatement(p: *Parse, is_block_level: bool) Error!Node.Index {
    if (p.eatToken(.keyword_comptime)) |comptime_token| {
        const opt_block_expr = try p.parseBlockExpr();
        if (opt_block_expr) |block_expr| {
            return p.addNode(.{
                .tag = .@"comptime",
                .main_token = comptime_token,
                .data = .{ .node = block_expr },
            });
        }

        if (is_block_level) {
            return p.expectVarDeclExprStatement(comptime_token);
        } else {
            const assign = try p.expectAssignExpr();
            try p.expectSemicolon(.expected_semi_after_stmt, true);
            if (p.nodeTag(assign) != .assign_destructure) {
                return p.addNode(.{
                    .tag = .@"comptime",
                    .main_token = comptime_token,
                    .data = .{ .node = assign },
                });
            } else {
                return assign;
            }
        }
    }

    switch (p.tokenTag(p.tok_i)) {
        .keyword_nosuspend => {
            return p.addNode(.{
                .tag = .@"nosuspend",
                .main_token = p.nextToken(),
                .data = .{ .node = try p.expectBlockExprStatement() },
            });
        },
        .keyword_suspend => {
            const token = p.nextToken();
            const block_expr = try p.expectBlockExprStatement();
            return p.addNode(.{
                .tag = .@"suspend",
                .main_token = token,
                .data = .{ .node = block_expr },
            });
        },
        .keyword_defer => if (is_block_level) return p.addNode(.{
            .tag = .@"defer",
            .main_token = p.nextToken(),
            .data = .{ .node = try p.expectBlockExprStatement() },
        }),
        .keyword_errdefer => if (is_block_level) return p.addNode(.{
            .tag = .@"errdefer",
            .main_token = p.nextToken(),
            .data = .{ .opt_token_and_node = .{
                try p.parsePayload(),
                try p.expectBlockExprStatement(),
            } },
        }),
        .keyword_if => return p.expectIfStatement(),
        .keyword_enum, .keyword_struct, .keyword_union => {
            const identifier = p.tok_i + 1;
            if (try p.parseCStyleContainer()) {
                // Return something so that `expectStatement` is happy.
                return p.addNode(.{
                    .tag = .identifier,
                    .main_token = identifier,
                    .data = undefined,
                });
            }
        },
        else => {},
    }

    if (try p.parseLabeledStatement()) |labeled_statement| return labeled_statement;

    if (is_block_level) {
        return p.expectVarDeclExprStatement(null);
    } else {
        const assign = try p.expectAssignExpr();
        try p.expectSemicolon(.expected_semi_after_stmt, true);
        return assign;
    }
}

/// ComptimeStatement
///     <- BlockExpr
///      / VarDeclExprStatement
fn expectComptimeStatement(p: *Parse, comptime_token: TokenIndex) !Node.Index {
    const maybe_block_expr = try p.parseBlockExpr();
    if (maybe_block_expr) |block_expr| {
        return p.addNode(.{
            .tag = .@"comptime",
            .main_token = comptime_token,
            .data = .{
                .lhs = .{ .node = block_expr },
                .rhs = undefined,
            },
        });
    }
    return p.expectVarDeclExprStatement(comptime_token);
}

/// VarDeclExprStatement
///    <- Expr
///     / VarAssignStatement
///
/// VarAssignStatement <- (VarDeclProto / Expr) (COMMA (VarDeclProto / Expr))* EQUAL Expr SEMICOLON
fn expectVarDeclExprStatement(p: *Parse, comptime_token: ?TokenIndex) !Node.Index {
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);

    while (true) {
        const opt_var_decl_proto = try p.parseVarDeclProto();
        if (opt_var_decl_proto) |var_decl| {
            try p.scratch.append(p.gpa, var_decl);
        } else {
            const expr = try p.parseExpr() orelse {
                if (p.scratch.items.len == scratch_top) {
                    // We parsed nothing
                    return p.fail(.expected_statement);
                } else {
                    // We've had at least one LHS, but had a bad comma
                    return p.fail(.expected_expr_or_var_decl);
                }
            };
            try p.scratch.append(p.gpa, expr);
        }
        _ = p.eatToken(.comma) orelse break;
    }

    const lhs_count = p.scratch.items.len - scratch_top;
    assert(lhs_count > 0);

    const equal_token = p.eatToken(.equal) orelse eql: {
        if (lhs_count > 1) {
            // Definitely a destructure, so allow recovering from ==
            if (p.eatToken(.equal_equal)) |tok| {
                try p.warnMsg(.{ .tag = .wrong_equal_var_decl, .token = tok });
                break :eql tok;
            }
            return p.failExpected(.equal);
        }
        const lhs = p.scratch.items[scratch_top];
        switch (p.nodeTag(lhs)) {
            .global_var_decl, .local_var_decl, .simple_var_decl, .aligned_var_decl => {
                // Definitely a var decl, so allow recovering from ==
                if (p.eatToken(.equal_equal)) |tok| {
                    try p.warnMsg(.{ .tag = .wrong_equal_var_decl, .token = tok });
                    break :eql tok;
                }
                return p.failExpected(.equal);
            },
            else => {},
        }

        const expr = try p.finishAssignExpr(lhs);
        try p.expectSemicolon(.expected_semi_after_stmt, true);
        if (comptime_token) |t| {
            return p.addNode(.{
                .tag = .@"comptime",
                .main_token = t,
                .data = .{ .node = expr },
            });
        } else {
            return expr;
        }
    };

    const rhs = try p.expectExpr();
    try p.expectSemicolon(.expected_semi_after_stmt, true);

    if (lhs_count == 1) {
        const lhs = p.scratch.items[scratch_top];
        switch (p.nodeTag(lhs)) {
            .simple_var_decl, .aligned_var_decl, .local_var_decl, .global_var_decl => {
                p.setVarDeclInitExpr(lhs, rhs.toOptional());
                // Don't need to wrap in comptime
                return lhs;
            },
            else => {},
        }
        const expr = try p.addNode(.{
            .tag = .assign,
            .main_token = equal_token,
            .data = .{ .node_and_node = .{
                lhs,
                rhs,
            } },
        });
        if (comptime_token) |t| {
            return p.addNode(.{
                .tag = .@"comptime",
                .main_token = t,
                .data = .{ .node = expr },
            });
        } else {
            return expr;
        }
    }

    // An actual destructure! No need for any `comptime` wrapper here.

    const extra_start: ExtraIndex = @enumFromInt(p.extra_data.items.len);
    try p.extra_data.ensureUnusedCapacity(p.gpa, lhs_count + 1);
    p.extra_data.appendAssumeCapacity(@intCast(lhs_count));
    p.extra_data.appendSliceAssumeCapacity(@ptrCast(p.scratch.items[scratch_top..]));

    return p.addNode(.{
        .tag = .assign_destructure,
        .main_token = equal_token,
        .data = .{ .extra_and_node = .{
            extra_start,
            rhs,
        } },
    });
}

/// If a parse error occurs, reports an error, but then finds the next statement
/// and returns that one instead. If a parse error occurs but there is no following
/// statement, returns 0.
fn expectStatementRecoverable(p: *Parse) Error!?Node.Index {
    while (true) {
        return p.expectStatement(true) catch |err| switch (err) {
            error.OutOfMemory => return error.OutOfMemory,
            error.ParseError => {
                p.findNextStmt(); // Try to skip to the next statement.
                switch (p.tokenTag(p.tok_i)) {
                    .r_brace => return null,
                    .eof => return error.ParseError,
                    else => continue,
                }
            },
        };
    }
}

/// IfStatement
///     <- IfPrefix BlockExpr ( KEYWORD_else Payload? Statement )?
///      / IfPrefix !BlockExpr AssignExpr ( SEMICOLON / KEYWORD_else Payload? Statement )
fn expectIfStatement(p: *Parse) !Node.Index {
    const if_token = p.assertToken(.keyword_if);
    _ = try p.expectToken(.l_paren);
    const condition = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    _ = try p.parsePtrPayload();

    // TODO propose to change the syntax so that semicolons are always required
    // inside if statements, even if there is an `else`.
    var else_required = false;
    const then_expr = blk: {
        const block_expr = try p.parseBlockExpr();
        if (block_expr) |block| break :blk block;
        const assign_expr = try p.parseAssignExpr() orelse {
            return p.fail(.expected_block_or_assignment);
        };
        if (p.eatToken(.semicolon)) |_| {
            return p.addNode(.{
                .tag = .if_simple,
                .main_token = if_token,
                .data = .{ .node_and_node = .{
                    condition,
                    assign_expr,
                } },
            });
        }
        else_required = true;
        break :blk assign_expr;
    };
    _ = p.eatToken(.keyword_else) orelse {
        if (else_required) {
            try p.warn(.expected_semi_or_else);
        }
        return p.addNode(.{
            .tag = .if_simple,
            .main_token = if_token,
            .data = .{ .node_and_node = .{
                condition,
                then_expr,
            } },
        });
    };
    _ = try p.parsePayload();
    const else_expr = try p.expectStatement(false);
    return p.addNode(.{
        .tag = .@"if",
        .main_token = if_token,
        .data = .{ .node_and_extra = .{
            condition,
            try p.addExtra(Node.If{
                .then_expr = then_expr,
                .else_expr = else_expr,
            }),
        } },
    });
}

/// LabeledStatement <- BlockLabel? (Block / LoopStatement / SwitchExpr)
fn parseLabeledStatement(p: *Parse) !?Node.Index {
    const opt_label_token = p.parseBlockLabel();

    if (try p.parseBlock()) |block| return block;
    if (try p.parseLoopStatement()) |loop_stmt| return loop_stmt;
    if (try p.parseSwitchExpr(opt_label_token != null)) |switch_expr| return switch_expr;

    const label_token = opt_label_token orelse return null;

    const after_colon = p.tok_i;
    if (try p.parseTypeExpr()) |_| {
        const a = try p.parseByteAlign();
        const b = try p.parseAddrSpace();
        const c = try p.parseLinkSection();
        const d = if (p.eatToken(.equal) == null) null else try p.expectExpr();
        if (a != null or b != null or c != null or d != null) {
            return p.failMsg(.{ .tag = .expected_var_const, .token = label_token });
        }
    }
    return p.failMsg(.{ .tag = .expected_labelable, .token = after_colon });
}

/// LoopStatement <- KEYWORD_inline? (ForStatement / WhileStatement)
fn parseLoopStatement(p: *Parse) !?Node.Index {
    const inline_token = p.eatToken(.keyword_inline);

    if (try p.parseForStatement()) |for_statement| return for_statement;
    if (try p.parseWhileStatement()) |while_statement| return while_statement;

    if (inline_token == null) return null;

    // If we've seen "inline", there should have been a "for" or "while"
    return p.fail(.expected_inlinable);
}

/// ForStatement
///     <- ForPrefix BlockExpr ( KEYWORD_else Statement / !KEYWORD_else )
///      / ForPrefix !BlockExpr AssignExpr ( SEMICOLON / KEYWORD_else Statement )
fn parseForStatement(p: *Parse) !?Node.Index {
    const for_token = p.eatToken(.keyword_for) orelse return null;

    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);
    const inputs = try p.forPrefix();

    var else_required = false;
    var seen_semicolon = false;
    const then_expr = blk: {
        const block_expr = try p.parseBlockExpr();
        if (block_expr) |block| break :blk block;
        const assign_expr = try p.parseAssignExpr() orelse {
            return p.fail(.expected_block_or_assignment);
        };
        if (p.eatToken(.semicolon)) |_| {
            seen_semicolon = true;
            break :blk assign_expr;
        }
        else_required = true;
        break :blk assign_expr;
    };
    var has_else = false;
    if (!seen_semicolon and p.eatToken(.keyword_else) != null) {
        try p.scratch.append(p.gpa, then_expr);
        const else_stmt = try p.expectStatement(false);
        try p.scratch.append(p.gpa, else_stmt);
        has_else = true;
    } else if (inputs == 1) {
        if (else_required) try p.warn(.expected_semi_or_else);
        return try p.addNode(.{
            .tag = .for_simple,
            .main_token = for_token,
            .data = .{ .node_and_node = .{
                p.scratch.items[scratch_top],
                then_expr,
            } },
        });
    } else {
        if (else_required) try p.warn(.expected_semi_or_else);
        try p.scratch.append(p.gpa, then_expr);
    }
    return try p.addNode(.{
        .tag = .@"for",
        .main_token = for_token,
        .data = .{ .@"for" = .{
            (try p.listToSpan(p.scratch.items[scratch_top..])).start,
            .{ .inputs = @intCast(inputs), .has_else = has_else },
        } },
    });
}

/// WhilePrefix <- KEYWORD_while LPAREN Expr RPAREN PtrPayload? WhileContinueExpr?
///
/// WhileStatement
///     <- WhilePrefix BlockExpr ( KEYWORD_else Payload? Statement )?
///      / WhilePrefix !BlockExpr AssignExpr ( SEMICOLON / KEYWORD_else Payload? Statement )
fn parseWhileStatement(p: *Parse) !?Node.Index {
    const while_token = p.eatToken(.keyword_while) orelse return null;
    _ = try p.expectToken(.l_paren);
    const condition = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    _ = try p.parsePtrPayload();
    const cont_expr = try p.parseWhileContinueExpr();

    // TODO propose to change the syntax so that semicolons are always required
    // inside while statements, even if there is an `else`.
    var else_required = false;
    const then_expr = blk: {
        const block_expr = try p.parseBlockExpr();
        if (block_expr) |block| break :blk block;
        const assign_expr = try p.parseAssignExpr() orelse {
            return p.fail(.expected_block_or_assignment);
        };
        if (p.eatToken(.semicolon)) |_| {
            if (cont_expr == null) {
                return try p.addNode(.{
                    .tag = .while_simple,
                    .main_token = while_token,
                    .data = .{ .node_and_node = .{
                        condition,
                        assign_expr,
                    } },
                });
            } else {
                return try p.addNode(.{
                    .tag = .while_cont,
                    .main_token = while_token,
                    .data = .{ .node_and_extra = .{
                        condition,
                        try p.addExtra(Node.WhileCont{
                            .cont_expr = cont_expr.?,
                            .then_expr = assign_expr,
                        }),
                    } },
                });
            }
        }
        else_required = true;
        break :blk assign_expr;
    };
    _ = p.eatToken(.keyword_else) orelse {
        if (else_required) {
            try p.warn(.expected_semi_or_else);
        }
        if (cont_expr == null) {
            return try p.addNode(.{
                .tag = .while_simple,
                .main_token = while_token,
                .data = .{ .node_and_node = .{
                    condition,
                    then_expr,
                } },
            });
        } else {
            return try p.addNode(.{
                .tag = .while_cont,
                .main_token = while_token,
                .data = .{ .node_and_extra = .{
                    condition,
                    try p.addExtra(Node.WhileCont{
                        .cont_expr = cont_expr.?,
                        .then_expr = then_expr,
                    }),
                } },
            });
        }
    };
    _ = try p.parsePayload();
    const else_expr = try p.expectStatement(false);
    return try p.addNode(.{
        .tag = .@"while",
        .main_token = while_token,
        .data = .{ .node_and_extra = .{
            condition,
            try p.addExtra(Node.While{
                .cont_expr = .fromOptional(cont_expr),
                .then_expr = then_expr,
                .else_expr = else_expr,
            }),
        } },
    });
}

/// BlockExprStatement
///     <- BlockExpr
///      / !BlockExpr AssignExpr SEMICOLON
fn parseBlockExprStatement(p: *Parse) !?Node.Index {
    const block_expr = try p.parseBlockExpr();
    if (block_expr) |expr| return expr;
    const assign_expr = try p.parseAssignExpr();
    if (assign_expr) |expr| {
        try p.expectSemicolon(.expected_semi_after_stmt, true);
        return expr;
    }
    return null;
}

fn expectBlockExprStatement(p: *Parse) !Node.Index {
    return try p.parseBlockExprStatement() orelse return p.fail(.expected_block_or_expr);
}

/// BlockExpr <- BlockLabel? Block
fn parseBlockExpr(p: *Parse) Error!?Node.Index {
    switch (p.tokenTag(p.tok_i)) {
        .identifier => {
            if (p.tokenTag(p.tok_i + 1) == .colon and
                p.tokenTag(p.tok_i + 2) == .l_brace)
            {
                p.tok_i += 2;
                return p.parseBlock();
            } else {
                return null;
            }
        },
        .l_brace => return p.parseBlock(),
        else => return null,
    }
}

/// AssignExpr <- Expr (AssignOp Expr / (COMMA Expr)+ EQUAL Expr)?
///
/// AssignOp
///     <- ASTERISKEQUAL
///      / ASTERISKPIPEEQUAL
///      / SLASHEQUAL
///      / PERCENTEQUAL
///      / PLUSEQUAL
///      / PLUSPIPEEQUAL
///      / MINUSEQUAL
///      / MINUSPIPEEQUAL
///      / LARROW2EQUAL
///      / LARROW2PIPEEQUAL
///      / RARROW2EQUAL
///      / AMPERSANDEQUAL
///      / CARETEQUAL
///      / PIPEEQUAL
///      / ASTERISKPERCENTEQUAL
///      / PLUSPERCENTEQUAL
///      / MINUSPERCENTEQUAL
///      / EQUAL
fn parseAssignExpr(p: *Parse) !?Node.Index {
    const expr = try p.parseExpr() orelse return null;
    return try p.finishAssignExpr(expr);
}

/// SingleAssignExpr <- Expr (AssignOp Expr)?
fn parseSingleAssignExpr(p: *Parse) !?Node.Index {
    const lhs = try p.parseExpr() orelse return null;
    const tag = assignOpNode(p.tokenTag(p.tok_i)) orelse return lhs;
    return try p.addNode(.{
        .tag = tag,
        .main_token = p.nextToken(),
        .data = .{ .node_and_node = .{
            lhs,
            try p.expectExpr(),
        } },
    });
}

fn finishAssignExpr(p: *Parse, lhs: Node.Index) !Node.Index {
    const tok = p.tokenTag(p.tok_i);
    if (tok == .comma) return p.finishAssignDestructureExpr(lhs);
    const tag = assignOpNode(tok) orelse return lhs;
    return p.addNode(.{
        .tag = tag,
        .main_token = p.nextToken(),
        .data = .{ .node_and_node = .{
            lhs,
            try p.expectExpr(),
        } },
    });
}

fn assignOpNode(tok: Token.Tag) ?Node.Tag {
    return switch (tok) {
        .asterisk_equal => .assign_mul,
        .slash_equal => .assign_div,
        .percent_equal => .assign_mod,
        .plus_equal => .assign_add,
        .minus_equal => .assign_sub,
        .angle_bracket_angle_bracket_left_equal => .assign_shl,
        .angle_bracket_angle_bracket_left_pipe_equal => .assign_shl_sat,
        .angle_bracket_angle_bracket_right_equal => .assign_shr,
        .ampersand_equal => .assign_bit_and,
        .caret_equal => .assign_bit_xor,
        .pipe_equal => .assign_bit_or,
        .asterisk_percent_equal => .assign_mul_wrap,
        .plus_percent_equal => .assign_add_wrap,
        .minus_percent_equal => .assign_sub_wrap,
        .asterisk_pipe_equal => .assign_mul_sat,
        .plus_pipe_equal => .assign_add_sat,
        .minus_pipe_equal => .assign_sub_sat,
        .equal => .assign,
        else => null,
    };
}

fn finishAssignDestructureExpr(p: *Parse, first_lhs: Node.Index) !Node.Index {
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);

    try p.scratch.append(p.gpa, first_lhs);

    while (p.eatToken(.comma)) |_| {
        const expr = try p.expectExpr();
        try p.scratch.append(p.gpa, expr);
    }

    const equal_token = try p.expectToken(.equal);

    const rhs = try p.expectExpr();

    const lhs_count = p.scratch.items.len - scratch_top;
    assert(lhs_count > 1); // we already had first_lhs, and must have at least one more lvalue

    const extra_start: ExtraIndex = @enumFromInt(p.extra_data.items.len);
    try p.extra_data.ensureUnusedCapacity(p.gpa, lhs_count + 1);
    p.extra_data.appendAssumeCapacity(@intCast(lhs_count));
    p.extra_data.appendSliceAssumeCapacity(@ptrCast(p.scratch.items[scratch_top..]));

    return p.addNode(.{
        .tag = .assign_destructure,
        .main_token = equal_token,
        .data = .{ .extra_and_node = .{
            extra_start,
            rhs,
        } },
    });
}

fn expectSingleAssignExpr(p: *Parse) !Node.Index {
    return try p.parseSingleAssignExpr() orelse return p.fail(.expected_expr_or_assignment);
}

fn expectAssignExpr(p: *Parse) !Node.Index {
    return try p.parseAssignExpr() orelse return p.fail(.expected_expr_or_assignment);
}

fn parseExpr(p: *Parse) Error!?Node.Index {
    return p.parseExprPrecedence(0);
}

fn expectExpr(p: *Parse) Error!Node.Index {
    return try p.parseExpr() orelse return p.fail(.expected_expr);
}

const Assoc = enum {
    left,
    none,
};

const OperInfo = struct {
    prec: i8,
    tag: Node.Tag,
    assoc: Assoc = Assoc.left,
};

// A table of binary operator information. Higher precedence numbers are
// stickier. All operators at the same precedence level should have the same
// associativity.
const operTable = std.enums.directEnumArrayDefault(Token.Tag, OperInfo, .{ .prec = -1, .tag = Node.Tag.root }, 0, .{
    .keyword_or = .{ .prec = 10, .tag = .bool_or },

    .keyword_and = .{ .prec = 20, .tag = .bool_and },

    .equal_equal = .{ .prec = 30, .tag = .equal_equal, .assoc = Assoc.none },
    .bang_equal = .{ .prec = 30, .tag = .bang_equal, .assoc = Assoc.none },
    .angle_bracket_left = .{ .prec = 30, .tag = .less_than, .assoc = Assoc.none },
    .angle_bracket_right = .{ .prec = 30, .tag = .greater_than, .assoc = Assoc.none },
    .angle_bracket_left_equal = .{ .prec = 30, .tag = .less_or_equal, .assoc = Assoc.none },
    .angle_bracket_right_equal = .{ .prec = 30, .tag = .greater_or_equal, .assoc = Assoc.none },

    .ampersand = .{ .prec = 40, .tag = .bit_and },
    .caret = .{ .prec = 40, .tag = .bit_xor },
    .pipe = .{ .prec = 40, .tag = .bit_or },
    .keyword_orelse = .{ .prec = 40, .tag = .@"orelse" },
    .keyword_catch = .{ .prec = 40, .tag = .@"catch" },

    .angle_bracket_angle_bracket_left = .{ .prec = 50, .tag = .shl },
    .angle_bracket_angle_bracket_left_pipe = .{ .prec = 50, .tag = .shl_sat },
    .angle_bracket_angle_bracket_right = .{ .prec = 50, .tag = .shr },

    .plus = .{ .prec = 60, .tag = .add },
    .minus = .{ .prec = 60, .tag = .sub },
    .plus_plus = .{ .prec = 60, .tag = .array_cat },
    .plus_percent = .{ .prec = 60, .tag = .add_wrap },
    .minus_percent = .{ .prec = 60, .tag = .sub_wrap },
    .plus_pipe = .{ .prec = 60, .tag = .add_sat },
    .minus_pipe = .{ .prec = 60, .tag = .sub_sat },

    .pipe_pipe = .{ .prec = 70, .tag = .merge_error_sets },
    .asterisk = .{ .prec = 70, .tag = .mul },
    .slash = .{ .prec = 70, .tag = .div },
    .percent = .{ .prec = 70, .tag = .mod },
    .asterisk_asterisk = .{ .prec = 70, .tag = .array_mult },
    .asterisk_percent = .{ .prec = 70, .tag = .mul_wrap },
    .asterisk_pipe = .{ .prec = 70, .tag = .mul_sat },
});

fn parseExprPrecedence(p: *Parse, min_prec: i32) Error!?Node.Index {
    assert(min_prec >= 0);
    var node = try p.parsePrefixExpr() orelse return null;

    var banned_prec: i8 = -1;

    while (true) {
        const tok_tag = p.tokenTag(p.tok_i);
        const info = operTable[@as(usize, @intCast(@intFromEnum(tok_tag)))];
        if (info.prec < min_prec) {
            break;
        }
        if (info.prec == banned_prec) {
            return p.fail(.chained_comparison_operators);
        }

        const oper_token = p.nextToken();
        // Special-case handling for "catch"
        if (tok_tag == .keyword_catch) {
            _ = try p.parsePayload();
        }
        const rhs = try p.parseExprPrecedence(info.prec + 1) orelse {
            try p.warn(.expected_expr);
            return node;
        };

        {
            const tok_len = tok_tag.lexeme().?.len;
            const char_before = p.source[p.tokenStart(oper_token) - 1];
            const char_after = p.source[p.tokenStart(oper_token) + tok_len];
            if (tok_tag == .ampersand and char_after == '&') {
                // without types we don't know if '&&' was intended as 'bitwise_and address_of', or a c-style logical_and
                // The best the parser can do is recommend changing it to 'and' or ' & &'
                try p.warnMsg(.{ .tag = .invalid_ampersand_ampersand, .token = oper_token });
            } else if (std.ascii.isWhitespace(char_before) != std.ascii.isWhitespace(char_after)) {
                try p.warnMsg(.{ .tag = .mismatched_binary_op_whitespace, .token = oper_token });
            }
        }

        node = try p.addNode(.{
            .tag = info.tag,
            .main_token = oper_token,
            .data = .{ .node_and_node = .{ node, rhs } },
        });

        if (info.assoc == Assoc.none) {
            banned_prec = info.prec;
        }
    }

    return node;
}

/// PrefixExpr <- PrefixOp* PrimaryExpr
///
/// PrefixOp
///     <- EXCLAMATIONMARK
///      / MINUS
///      / TILDE
///      / MINUSPERCENT
///      / AMPERSAND
///      / KEYWORD_try
fn parsePrefixExpr(p: *Parse) Error!?Node.Index {
    const tag: Node.Tag = switch (p.tokenTag(p.tok_i)) {
        .bang => .bool_not,
        .minus => .negation,
        .tilde => .bit_not,
        .minus_percent => .negation_wrap,
        .ampersand => .address_of,
        .keyword_try => .@"try",
        else => return p.parsePrimaryExpr(),
    };
    return try p.addNode(.{
        .tag = tag,
        .main_token = p.nextToken(),
        .data = .{ .node = try p.expectPrefixExpr() },
    });
}

fn expectPrefixExpr(p: *Parse) Error!Node.Index {
    return try p.parsePrefixExpr() orelse return p.fail(.expected_prefix_expr);
}

/// TypeExpr <- PrefixTypeOp* ErrorUnionExpr
///
/// PrefixTypeOp
///     <- QUESTIONMARK
///      / KEYWORD_anyframe MINUSRARROW
///      / (ManyPtrTypeStart / SliceTypeStart) KEYWORD_allowzero? ByteAlign? AddrSpace? KEYWORD_const? KEYWORD_volatile?
///      / SinglePtrTypeStart KEYWORD_allowzero? BitAlign? AddrSpace? KEYWORD_const? KEYWORD_volatile?
///      / PtrTypeStart (AddrSpace / KEYWORD_align LPAREN Expr (COLON Expr COLON Expr)? RPAREN / KEYWORD_const / KEYWORD_volatile / KEYWORD_allowzero)*
///      / ArrayTypeStart
///
/// SliceTypeStart <- LBRACKET (COLON Expr)? RBRACKET
///
/// SinglePtrTypeStart <- ASTERISK / ASTERISK2
///
/// ManyPtrTypeStart <- LBRACKET ASTERISK (LETTERC / COLON Expr)? RBRACKET
///
/// ArrayTypeStart <- LBRACKET Expr !(ASTERISK / ASTERISK2) (COLON Expr)? RBRACKET
///
/// BitAlign <- KEYWORD_align LPAREN Expr (COLON Expr COLON Expr)? RPAREN
fn parseTypeExpr(p: *Parse) Error!?Node.Index {
    switch (p.tokenTag(p.tok_i)) {
        .question_mark => return try p.addNode(.{
            .tag = .optional_type,
            .main_token = p.nextToken(),
            .data = .{ .node = try p.expectTypeExpr() },
        }),
        .keyword_anyframe => switch (p.tokenTag(p.tok_i + 1)) {
            .arrow => return try p.addNode(.{
                .tag = .anyframe_type,
                .main_token = p.nextToken(),
                .data = .{ .token_and_node = .{
                    p.nextToken(),
                    try p.expectTypeExpr(),
                } },
            }),
            else => return try p.parseErrorUnionExpr(),
        },
        .asterisk => {
            const asterisk = p.nextToken();
            const mods = try p.parsePtrModifiers();
            const elem_type = try p.expectTypeExpr();
            if (mods.bit_range_start != .none) {
                return try p.addNode(.{
                    .tag = .ptr_type_bit_range,
                    .main_token = asterisk,
                    .data = .{ .extra_and_node = .{
                        try p.addExtra(Node.PtrTypeBitRange{
                            .sentinel = .none,
                            .align_n
```
