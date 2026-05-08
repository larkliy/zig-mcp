```
end(result.written());
                man.hash.addBytes(file_plp.prefix);
                _ = try man.addFilePath(file_path, null);
            },
            .artifact => |pa| {
                const artifact = pa.artifact;

                if (artifact.rootModuleTarget().os.tag == .windows) {
                    // On Windows we don't have rpaths so we have to add .dll search paths to PATH
                    run.addPathForDynLibs(artifact);
                }
                const file_path = artifact.installed_path orelse artifact.generated_bin.?.path.?;

                try argv_list.append(b.fmt("{s}{s}", .{
                    pa.prefix,
                    run.convertPathArg(.{ .root_dir = .cwd(), .sub_path = file_path }),
                }));

                _ = try man.addFile(file_path, null);
            },
            .output_file, .output_directory => |output| {
                man.hash.addBytes(output.prefix);
                man.hash.addBytes(output.basename);
                // Add a placeholder into the argument list because we need the
                // manifest hash to be updated with all arguments before the
                // object directory is computed.
                try output_placeholders.append(.{
                    .index = argv_list.items.len,
                    .tag = arg,
                    .output = output,
                });
                _ = try argv_list.addOne();
            },
        }
    }

    switch (run.stdin) {
        .bytes => |bytes| {
            man.hash.addBytes(bytes);
        },
        .lazy_path => |lazy_path| {
            const file_path = lazy_path.getPath2(b, step);
            _ = try man.addFile(file_path, null);
        },
        .none => {},
    }

    if (run.captured_stdout) |captured| {
        man.hash.addBytes(captured.output.basename);
        man.hash.add(captured.trim_whitespace);
    }

    if (run.captured_stderr) |captured| {
        man.hash.addBytes(captured.output.basename);
        man.hash.add(captured.trim_whitespace);
    }

    hashStdIo(&man.hash, run.stdio);

    for (run.file_inputs.items) |lazy_path| {
        _ = try man.addFile(lazy_path.getPath2(b, step), null);
    }

    if (run.cwd) |cwd| {
        const cwd_path = cwd.getPath3(b, step);
        _ = man.hash.addBytes(try cwd_path.toString(arena));
    }

    if (!has_side_effects and try step.cacheHitAndWatch(&man)) {
        // cache hit, skip running command
        const digest = man.final();

        try populateGeneratedPaths(
            arena,
            output_placeholders.items,
            run.captured_stdout,
            run.captured_stderr,
            b.cache_root,
            &digest,
        );

        step.result_cached = true;
        return;
    }

    const dep_output_file = run.dep_output_file orelse {
        // We already know the final output paths, use them directly.
        const digest = if (has_side_effects)
            man.hash.final()
        else
            man.final();

        try populateGeneratedPaths(
            arena,
            output_placeholders.items,
            run.captured_stdout,
            run.captured_stderr,
            b.cache_root,
            &digest,
        );

        const output_dir_path = "o" ++ Dir.path.sep_str ++ &digest;
        for (output_placeholders.items) |placeholder| {
            const output_sub_path = b.pathJoin(&.{ output_dir_path, placeholder.output.basename });
            const output_sub_dir_path = switch (placeholder.tag) {
                .output_file => Dir.path.dirname(output_sub_path).?,
                .output_directory => output_sub_path,
                else => unreachable,
            };
            b.cache_root.handle.createDirPath(io, output_sub_dir_path) catch |err| {
                return step.fail("unable to make path '{f}{s}': {s}", .{
                    b.cache_root, output_sub_dir_path, @errorName(err),
                });
            };
            const arg_output_path = run.convertPathArg(.{
                .root_dir = .cwd(),
                .sub_path = placeholder.output.generated_file.getPath(),
            });
            argv_list.items[placeholder.index] = if (placeholder.output.prefix.len == 0)
                arg_output_path
            else
                b.fmt("{s}{s}", .{ placeholder.output.prefix, arg_output_path });
        }

        try runCommand(run, argv_list.items, has_side_effects, output_dir_path, options, null);
        if (!has_side_effects) try step.writeManifestAndWatch(&man);
        return;
    };

    // We do not know the final output paths yet, use temp paths to run the command.
    var rand_int: u64 = undefined;
    io.random(@ptrCast(&rand_int));
    const tmp_dir_path = "tmp" ++ Dir.path.sep_str ++ std.fmt.hex(rand_int);

    for (output_placeholders.items) |placeholder| {
        const output_components = .{ tmp_dir_path, placeholder.output.basename };
        const output_sub_path = b.pathJoin(&output_components);
        const output_sub_dir_path = switch (placeholder.tag) {
            .output_file => Dir.path.dirname(output_sub_path).?,
            .output_directory => output_sub_path,
            else => unreachable,
        };
        b.cache_root.handle.createDirPath(io, output_sub_dir_path) catch |err| {
            return step.fail("unable to make path '{f}{s}': {s}", .{
                b.cache_root, output_sub_dir_path, @errorName(err),
            });
        };
        const raw_output_path: Build.Cache.Path = .{
            .root_dir = b.cache_root,
            .sub_path = b.pathJoin(&output_components),
        };
        placeholder.output.generated_file.path = raw_output_path.toString(b.graph.arena) catch @panic("OOM");
        argv_list.items[placeholder.index] = b.fmt("{s}{s}", .{
            placeholder.output.prefix,
            run.convertPathArg(raw_output_path),
        });
    }

    try runCommand(run, argv_list.items, has_side_effects, tmp_dir_path, options, null);

    const dep_file_dir = Dir.cwd();
    const dep_file_basename = dep_output_file.generated_file.getPath2(b, step);
    if (has_side_effects)
        try man.addDepFile(dep_file_dir, dep_file_basename)
    else
        try man.addDepFilePost(dep_file_dir, dep_file_basename);

    const digest = if (has_side_effects)
        man.hash.final()
    else
        man.final();

    const any_output = output_placeholders.items.len > 0 or
        run.captured_stdout != null or run.captured_stderr != null;

    // Rename into place
    if (any_output) {
        const o_sub_path = "o" ++ Dir.path.sep_str ++ &digest;

        b.cache_root.handle.rename(tmp_dir_path, b.cache_root.handle, o_sub_path, io) catch |err| switch (err) {
            Dir.RenameError.DirNotEmpty => {
                b.cache_root.handle.deleteTree(io, o_sub_path) catch |del_err| {
                    return step.fail("unable to remove dir '{f}'{s}: {t}", .{
                        b.cache_root, tmp_dir_path, del_err,
                    });
                };
                b.cache_root.handle.rename(tmp_dir_path, b.cache_root.handle, o_sub_path, io) catch |retry_err| {
                    return step.fail("unable to rename dir '{f}{s}' to '{f}{s}': {t}", .{
                        b.cache_root, tmp_dir_path, b.cache_root, o_sub_path, retry_err,
                    });
                };
            },
            else => return step.fail("unable to rename dir '{f}{s}' to '{f}{s}': {t}", .{
                b.cache_root, tmp_dir_path, b.cache_root, o_sub_path, err,
            }),
        };
    }

    if (!has_side_effects) try step.writeManifestAndWatch(&man);

    try populateGeneratedPaths(
        arena,
        output_placeholders.items,
        run.captured_stdout,
        run.captured_stderr,
        b.cache_root,
        &digest,
    );
}

pub fn rerunInFuzzMode(
    run: *Run,
    fuzz: *std.Build.Fuzz,
    prog_node: std.Progress.Node,
) !void {
    const step = &run.step;
    const b = step.owner;
    const io = b.graph.io;
    const arena = b.allocator;
    var argv_list: std.ArrayList([]const u8) = .empty;
    for (run.argv.items) |arg| {
        switch (arg) {
            .bytes => |bytes| {
                try argv_list.append(arena, bytes);
            },
            .lazy_path => |file| {
                const file_path = file.lazy_path.getPath3(b, step);
                try argv_list.append(arena, b.fmt("{s}{s}", .{ file.prefix, run.convertPathArg(file_path) }));
            },
            .decorated_directory => |dd| {
                const file_path = dd.lazy_path.getPath3(b, step);
                try argv_list.append(arena, b.fmt("{s}{s}{s}", .{ dd.prefix, run.convertPathArg(file_path), dd.suffix }));
            },
            .file_content => |file_plp| {
                const file_path = file_plp.lazy_path.getPath3(b, step);

                var result: std.Io.Writer.Allocating = .init(arena);
                errdefer result.deinit();
                result.writer.writeAll(file_plp.prefix) catch return error.OutOfMemory;

                const file = try file_path.root_dir.handle.openFile(io, file_path.subPathOrDot(), .{});
                defer file.close(io);

                var buf: [1024]u8 = undefined;
                var file_reader = file.reader(io, &buf);
                _ = file_reader.interface.streamRemaining(&result.writer) catch |err| switch (err) {
                    error.ReadFailed => return file_reader.err.?,
                    error.WriteFailed => return error.OutOfMemory,
                };

                try argv_list.append(arena, result.written());
            },
            .artifact => |pa| {
                const artifact = pa.artifact;
                const file_path: []const u8 = p: {
                    if (artifact == run.producer.?) break :p b.fmt("{f}", .{run.rebuilt_executable.?});
                    break :p artifact.installed_path orelse artifact.generated_bin.?.path.?;
                };
                try argv_list.append(arena, b.fmt("{s}{s}", .{
                    pa.prefix,
                    run.convertPathArg(.{ .root_dir = .cwd(), .sub_path = file_path }),
                }));
            },
            .output_file, .output_directory => unreachable,
        }
    }

    if (run.step.result_failed_command) |cmd| {
        fuzz.gpa.free(cmd);
        run.step.result_failed_command = null;
    }

    const has_side_effects = false;
    var rand_int: u64 = undefined;
    io.random(@ptrCast(&rand_int));
    const tmp_dir_path = "tmp" ++ Dir.path.sep_str ++ std.fmt.hex(rand_int);
    try runCommand(run, argv_list.items, has_side_effects, tmp_dir_path, .{
        .progress_node = prog_node,
        .watch = undefined, // not used by `runCommand`
        .web_server = null, // only needed for time reports
        .unit_test_timeout_ns = null, // don't time out fuzz tests for now
        .gpa = fuzz.gpa,
    }, .{
        .fuzz = fuzz,
    });
}

fn populateGeneratedPaths(
    arena: std.mem.Allocator,
    output_placeholders: []const IndexedOutput,
    captured_stdout: ?*CapturedStdIo,
    captured_stderr: ?*CapturedStdIo,
    cache_root: Build.Cache.Directory,
    digest: *const Build.Cache.HexDigest,
) !void {
    for (output_placeholders) |placeholder| {
        placeholder.output.generated_file.path = try cache_root.join(arena, &.{
            "o", digest, placeholder.output.basename,
        });
    }

    if (captured_stdout) |captured| {
        captured.output.generated_file.path = try cache_root.join(arena, &.{
            "o", digest, captured.output.basename,
        });
    }

    if (captured_stderr) |captured| {
        captured.output.generated_file.path = try cache_root.join(arena, &.{
            "o", digest, captured.output.basename,
        });
    }
}

fn formatTerm(term: ?process.Child.Term, w: *std.Io.Writer) std.Io.Writer.Error!void {
    if (term) |t| switch (t) {
        .exited => |code| try w.print("exited with code {d}", .{code}),
        .signal => |sig| try w.print("terminated with signal {t}", .{sig}),
        .stopped => |sig| try w.print("stopped with signal {t}", .{sig}),
        .unknown => |code| try w.print("terminated for unknown reason with code {d}", .{code}),
    } else {
        try w.writeAll("exited with any code");
    }
}
fn fmtTerm(term: ?process.Child.Term) std.fmt.Alt(?process.Child.Term, formatTerm) {
    return .{ .data = term };
}

fn termMatches(expected: ?process.Child.Term, actual: process.Child.Term) bool {
    return if (expected) |e| switch (e) {
        .exited => |expected_code| switch (actual) {
            .exited => |actual_code| expected_code == actual_code,
            else => false,
        },
        .signal => |expected_sig| switch (actual) {
            .signal => |actual_sig| expected_sig == actual_sig,
            else => false,
        },
        .stopped => |expected_sig| switch (actual) {
            .stopped => |actual_sig| expected_sig == actual_sig,
            else => false,
        },
        .unknown => |expected_code| switch (actual) {
            .unknown => |actual_code| expected_code == actual_code,
            else => false,
        },
    } else switch (actual) {
        .exited => true,
        else => false,
    };
}

const FuzzContext = struct {
    fuzz: *std.Build.Fuzz,
};

fn runCommand(
    run: *Run,
    argv: []const []const u8,
    has_side_effects: bool,
    output_dir_path: []const u8,
    options: Step.MakeOptions,
    fuzz_context: ?FuzzContext,
) !void {
    const step = &run.step;
    const b = step.owner;
    const arena = b.allocator;
    const gpa = options.gpa;
    const io = b.graph.io;

    const cwd: process.Child.Cwd = if (run.cwd) |lazy_cwd| .{ .path = lazy_cwd.getPath2(b, step) } else .inherit;

    try step.handleChildProcUnsupported();
    try Step.handleVerbose2(step.owner, cwd, run.environ_map, argv);

    const allow_skip = switch (run.stdio) {
        .check, .zig_test => run.skip_foreign_checks,
        else => false,
    };

    var interp_argv = std.array_list.Managed([]const u8).init(b.allocator);
    defer interp_argv.deinit();

    var environ_map: EnvMap = env: {
        const orig = run.environ_map orelse &b.graph.environ_map;
        break :env try orig.clone(gpa);
    };
    defer environ_map.deinit();

    const opt_generic_result = spawnChildAndCollect(run, argv, &environ_map, has_side_effects, options, fuzz_context) catch |err| term: {
        // InvalidExe: cpu arch mismatch
        // FileNotFound: can happen with a wrong dynamic linker path
        if (err == error.InvalidExe or err == error.FileNotFound) interpret: {
            // TODO: learn the target from the binary directly rather than from
            // relying on it being a Compile step. This will make this logic
            // work even for the edge case that the binary was produced by a
            // third party.
            const exe = switch (run.argv.items[0]) {
                .artifact => |exe| exe.artifact,
                else => break :interpret,
            };
            switch (exe.kind) {
                .exe, .@"test" => {},
                else => break :interpret,
            }

            const root_target = exe.rootModuleTarget();
            const need_cross_libc = exe.is_linking_libc and
                (root_target.isGnuLibC() or (root_target.isMuslLibC() and exe.linkage == .dynamic));
            const other_target = exe.root_module.resolved_target.?.result;
            switch (std.zig.system.getExternalExecutor(io, &b.graph.host.result, &other_target, .{
                .qemu_fixes_dl = need_cross_libc and b.libc_runtimes_dir != null,
                .link_libc = exe.is_linking_libc,
            })) {
                .native, .rosetta => {
                    if (allow_skip) return error.MakeSkipped;
                    break :interpret;
                },
                .wine => |bin_name| {
                    if (b.enable_wine) {
                        try interp_argv.append(bin_name);
                        try interp_argv.appendSlice(argv);

                        // Wine's excessive stderr logging is only situationally helpful. Disable it by default, but
                        // allow the user to override it (e.g. with `WINEDEBUG=err+all`) if desired.
                        if (environ_map.get("WINEDEBUG") == null) {
                            try environ_map.put("WINEDEBUG", "-all");
                        }
                    } else {
                        return failForeign(run, "-fwine", argv[0], exe);
                    }
                },
                .qemu => |bin_name| {
                    if (b.enable_qemu) {
                        try interp_argv.append(bin_name);

                        if (need_cross_libc) {
                            if (b.libc_runtimes_dir) |dir| {
                                try interp_argv.append("-L");
                                try interp_argv.append(b.pathJoin(&.{
                                    dir,
                                    try if (root_target.isGnuLibC()) std.zig.target.glibcRuntimeTriple(
                                        b.allocator,
                                        root_target.cpu.arch,
                                        root_target.os.tag,
                                        root_target.abi,
                                    ) else if (root_target.isMuslLibC()) std.zig.target.muslRuntimeTriple(
                                        b.allocator,
                                        root_target.cpu.arch,
                                        root_target.abi,
                                    ) else unreachable,
                                }));
                            } else return failForeign(run, "--libc-runtimes", argv[0], exe);
                        }

                        try interp_argv.appendSlice(argv);
                    } else return failForeign(run, "-fqemu", argv[0], exe);
                },
                .darling => |bin_name| {
                    if (b.enable_darling) {
                        try interp_argv.append(bin_name);
                        try interp_argv.appendSlice(argv);
                    } else {
                        return failForeign(run, "-fdarling", argv[0], exe);
                    }
                },
                .wasmtime => |bin_name| {
                    if (b.enable_wasmtime) {
                        try interp_argv.append(bin_name);
                        try interp_argv.append("--dir=.");
                        // Wasmtime doeesn't inherit environment variables from the parent process
                        // by default. '-S inherit-env' was added in Wasmtime version 20.
                        try interp_argv.append("-Sinherit-env");
                        try interp_argv.append(argv[0]);
                        try interp_argv.appendSlice(argv[1..]);
                    } else {
                        return failForeign(run, "-fwasmtime", argv[0], exe);
                    }
                },
                .bad_dl => |foreign_dl| {
                    if (allow_skip) return error.MakeSkipped;

                    const host_dl = b.graph.host.result.dynamic_linker.get() orelse "(none)";

                    return step.fail(
                        \\the host system is unable to execute binaries from the target
                        \\  because the host dynamic linker is '{s}',
                        \\  while the target dynamic linker is '{s}'.
                        \\  consider setting the dynamic linker or enabling skip_foreign_checks in the Run step
                    , .{ host_dl, foreign_dl });
                },
                .bad_os_or_cpu => {
                    if (allow_skip) return error.MakeSkipped;

                    const host_name = try b.graph.host.result.zigTriple(b.allocator);
                    const foreign_name = try root_target.zigTriple(b.allocator);

                    return step.fail("the host system ({s}) is unable to execute binaries from the target ({s})", .{
                        host_name, foreign_name,
                    });
                },
            }

            if (root_target.os.tag == .windows) {
                // On Windows we don't have rpaths so we have to add .dll search paths to PATH
                run.addPathForDynLibs(exe);
            }

            gpa.free(step.result_failed_command.?);
            step.result_failed_command = null;
            try Step.handleVerbose2(step.owner, cwd, run.environ_map, interp_argv.items);

            break :term spawnChildAndCollect(run, interp_argv.items, &environ_map, has_side_effects, options, fuzz_context) catch |e| {
                if (!run.failing_to_execute_foreign_is_an_error) return error.MakeSkipped;
                if (e == error.MakeFailed) return error.MakeFailed; // error already reported
                return step.fail("unable to spawn interpreter {s}: {t}", .{ interp_argv.items[0], e });
            };
        }
        if (err == error.MakeFailed) return error.MakeFailed; // error already reported

        return step.fail("failed to spawn and capture stdio from {s}: {t}", .{ argv[0], err });
    };

    const generic_result = opt_generic_result orelse {
        assert(run.stdio == .zig_test);
        // Specific errors have already been reported, and test results are populated. All we need
        // to do is report step failure if any test failed.
        if (!step.test_results.isSuccess()) return error.MakeFailed;
        return;
    };

    assert(fuzz_context == null);
    assert(run.stdio != .zig_test);

    // Capture stdout and stderr to GeneratedFile objects.
    const Stream = struct {
        captured: ?*CapturedStdIo,
        bytes: ?[]const u8,
    };
    for ([_]Stream{
        .{
            .captured = run.captured_stdout,
            .bytes = generic_result.stdout,
        },
        .{
            .captured = run.captured_stderr,
            .bytes = generic_result.stderr,
        },
    }) |stream| {
        if (stream.captured) |captured| {
            const output_components = .{ output_dir_path, captured.output.basename };
            const output_path = try b.cache_root.join(arena, &output_components);
            captured.output.generated_file.path = output_path;

            const sub_path = b.pathJoin(&output_components);
            const sub_path_dirname = Dir.path.dirname(sub_path).?;
            b.cache_root.handle.createDirPath(io, sub_path_dirname) catch |err| {
                return step.fail("unable to make path '{f}{s}': {s}", .{
                    b.cache_root, sub_path_dirname, @errorName(err),
                });
            };
            const data = switch (captured.trim_whitespace) {
                .none => stream.bytes.?,
                .all => mem.trim(u8, stream.bytes.?, &std.ascii.whitespace),
                .leading => mem.trimStart(u8, stream.bytes.?, &std.ascii.whitespace),
                .trailing => mem.trimEnd(u8, stream.bytes.?, &std.ascii.whitespace),
            };
            b.cache_root.handle.writeFile(io, .{ .sub_path = sub_path, .data = data }) catch |err| {
                return step.fail("unable to write file '{f}{s}': {s}", .{
                    b.cache_root, sub_path, @errorName(err),
                });
            };
        }
    }

    switch (run.stdio) {
        .zig_test => unreachable,
        .check => |checks| for (checks.items) |check| switch (check) {
            .expect_stderr_exact => |expected_bytes| {
                if (!mem.eql(u8, expected_bytes, generic_result.stderr.?)) {
                    return step.fail(
                        \\========= expected this stderr: =========
                        \\{s}
                        \\========= but found: ====================
                        \\{s}
                    , .{
                        expected_bytes,
                        generic_result.stderr.?,
                    });
                }
            },
            .expect_stderr_match => |match| {
                if (mem.find(u8, generic_result.stderr.?, match) == null) {
                    return step.fail(
                        \\========= expected to find in stderr: =========
                        \\{s}
                        \\========= but stderr does not contain it: =====
                        \\{s}
                    , .{
                        match,
                        generic_result.stderr.?,
                    });
                }
            },
            .expect_stdout_exact => |expected_bytes| {
                if (!mem.eql(u8, expected_bytes, generic_result.stdout.?)) {
                    return step.fail(
                        \\========= expected this stdout: =========
                        \\{s}
                        \\========= but found: ====================
                        \\{s}
                    , .{
                        expected_bytes,
                        generic_result.stdout.?,
                    });
                }
            },
            .expect_stdout_match => |match| {
                if (mem.find(u8, generic_result.stdout.?, match) == null) {
                    return step.fail(
                        \\========= expected to find in stdout: =========
                        \\{s}
                        \\========= but stdout does not contain it: =====
                        \\{s}
                    , .{
                        match,
                        generic_result.stdout.?,
                    });
                }
            },
            .expect_term => |expected_term| {
                if (!termMatches(expected_term, generic_result.term)) {
                    return step.fail("process {f} (expected {f})", .{
                        fmtTerm(generic_result.term),
                        fmtTerm(expected_term),
                    });
                }
            },
        },
        else => {
            // On failure, report captured stderr like normal standard error output.
            const bad_exit = switch (generic_result.term) {
                .exited => |code| code != 0,
                .signal, .stopped, .unknown => true,
            };
            if (bad_exit) {
                if (generic_result.stderr) |bytes| {
                    run.step.result_stderr = bytes;
                }
            }

            try step.handleChildProcessTerm(generic_result.term);
        },
    }
}

const EvalGenericResult = struct {
    term: process.Child.Term,
    stdout: ?[]const u8,
    stderr: ?[]const u8,
};

fn spawnChildAndCollect(
    run: *Run,
    argv: []const []const u8,
    environ_map: *EnvMap,
    has_side_effects: bool,
    options: Step.MakeOptions,
    fuzz_context: ?FuzzContext,
) !?EvalGenericResult {
    const b = run.step.owner;
    const graph = b.graph;
    const io = graph.io;

    if (fuzz_context != null) {
        assert(!has_side_effects);
        assert(run.stdio == .zig_test);
    }

    const child_cwd: process.Child.Cwd = if (run.cwd) |lazy_cwd| .{ .path = lazy_cwd.getPath2(b, &run.step) } else .inherit;

    // If an error occurs, it's caused by this command:
    assert(run.step.result_failed_command == null);
    run.step.result_failed_command = try Step.allocPrintCmd(options.gpa, child_cwd, .{
        .child = environ_map,
        .parent = &graph.environ_map,
    }, argv);

    var spawn_options: process.SpawnOptions = .{
        .argv = argv,
        .cwd = child_cwd,
        .environ_map = environ_map,
        .request_resource_usage_statistics = true,
        .stdin = if (run.stdin != .none) s: {
            assert(run.stdio != .inherit);
            break :s .pipe;
        } else switch (run.stdio) {
            .infer_from_args => if (has_side_effects) .inherit else .ignore,
            .inherit => .inherit,
            .check => .ignore,
            .zig_test => .pipe,
        },
        .stdout = if (run.captured_stdout != null) .pipe else switch (run.stdio) {
            .infer_from_args => if (has_side_effects) .inherit else .ignore,
            .inherit => .inherit,
            .check => |checks| if (checksContainStdout(checks.items)) .pipe else .ignore,
            .zig_test => .pipe,
        },
        .stderr = if (run.captured_stderr != null) .pipe else switch (run.stdio) {
            .infer_from_args => if (has_side_effects) .inherit else .pipe,
            .inherit => .inherit,
            .check => .pipe,
            .zig_test => .pipe,
        },
    };

    if (run.stdio == .zig_test) {
        const started: Io.Clock.Timestamp = .now(io, .awake);
        const result = evalZigTest(run, spawn_options, options, fuzz_context) catch |err| switch (err) {
            error.Canceled => |e| return e,
            else => |e| e,
        };
        run.step.result_duration_ns = @intCast(started.untilNow(io).raw.nanoseconds);
        try result;
        return null;
    } else {
        const inherit = spawn_options.stdout == .inherit or spawn_options.stderr == .inherit;
        if (!run.disable_zig_progress and !inherit) {
            spawn_options.progress_node = options.progress_node;
        }
        const terminal_mode: Io.Terminal.Mode = if (inherit) m: {
            const stderr = try io.lockStderr(&.{}, graph.stderr_mode);
            break :m stderr.terminal_mode;
        } else .no_color;
        defer if (inherit) io.unlockStderr();
        try setColorEnvironmentVariables(run, environ_map, terminal_mode);

        const started: Io.Clock.Timestamp = .now(io, .awake);
        const result = evalGeneric(run, spawn_options) catch |err| switch (err) {
            error.Canceled => |e| return e,
            else => |e| e,
        };
        run.step.result_duration_ns = @intCast(started.untilNow(io).raw.nanoseconds);
        return try result;
    }
}

fn setColorEnvironmentVariables(run: *Run, environ_map: *EnvMap, terminal_mode: Io.Terminal.Mode) !void {
    color: switch (run.color) {
        .manual => {},
        .enable => {
            try environ_map.put("CLICOLOR_FORCE", "1");
            _ = environ_map.swapRemove("NO_COLOR");
        },
        .disable => {
            try environ_map.put("NO_COLOR", "1");
            _ = environ_map.swapRemove("CLICOLOR_FORCE");
        },
        .inherit => switch (terminal_mode) {
            .no_color, .windows_api => continue :color .disable,
            .escape_codes => continue :color .enable,
        },
        .auto => {
            const capture_stderr = run.captured_stderr != null or switch (run.stdio) {
                .check => |checks| checksContainStderr(checks.items),
                .infer_from_args, .inherit, .zig_test => false,
            };
            if (capture_stderr) {
                continue :color .disable;
            } else {
                continue :color .inherit;
            }
        },
    }
}

const StdioPollEnum = enum { stdout, stderr };

fn evalZigTest(
    run: *Run,
    spawn_options: process.SpawnOptions,
    options: Step.MakeOptions,
    fuzz_context: ?FuzzContext,
) !void {
    if (fuzz_context != null) {
        try evalFuzzTest(run, spawn_options, options, fuzz_context.?);
        return;
    }

    const step_owner = run.step.owner;
    const gpa = step_owner.allocator;
    const arena = step_owner.allocator;
    const io = step_owner.graph.io;

    // We will update this every time a child runs.
    run.step.result_peak_rss = 0;

    var test_results: Step.TestResults = .{
        .test_count = 0,
        .skip_count = 0,
        .fail_count = 0,
        .crash_count = 0,
        .timeout_count = 0,
        .leak_count = 0,
        .log_err_count = 0,
    };
    var test_metadata: ?TestMetadata = null;

    while (true) {
        var child = try process.spawn(io, spawn_options);
        var multi_reader_buffer: Io.File.MultiReader.Buffer(2) = undefined;
        var multi_reader: Io.File.MultiReader = undefined;
        multi_reader.init(gpa, io, multi_reader_buffer.toStreams(), &.{ child.stdout.?, child.stderr.? });
        var child_killed = false;
        defer if (!child_killed) {
            child.kill(io);
            multi_reader.deinit();
            run.step.result_peak_rss = @max(
                run.step.result_peak_rss,
                child.resource_usage_statistics.getMaxRss() orelse 0,
            );
        };

        switch (try waitZigTest(
            run,
            &child,
            options,
            &multi_reader,
            &test_metadata,
            &test_results,
        )) {
            .write_failed => |err| {
                // The runner unexpectedly closed a stdio pipe, which means a crash. Make sure we've captured
                // all available stderr to make our error output as useful as possible.
                const stderr_fr = multi_reader.fileReader(1);
                while (stderr_fr.interface.fillMore()) |_| {} else |e| switch (e) {
                    error.ReadFailed => return stderr_fr.err.?,
                    error.EndOfStream => {},
                }
                run.step.result_stderr = try arena.dupe(u8, stderr_fr.interface.buffered());

                // Clean up everything and wait for the child to exit.
                child.stdin.?.close(io);
                child.stdin = null;
                multi_reader.deinit();
                child_killed = true;
                const term = try child.wait(io);
                run.step.result_peak_rss = @max(
                    run.step.result_peak_rss,
                    child.resource_usage_statistics.getMaxRss() orelse 0,
                );

                // The individual unit test results are irrelevant: the test runner itself broke!
                // Fail immediately without populating `s.test_results`.
                return run.step.fail("unable to write stdin ({t}); test process unexpectedly {f}", .{ err, fmtTerm(term) });
            },
            .no_poll => |no_poll| {
                // This might be a success (we requested exit and the child dutifully closed stdout) or
                // a crash of some kind. Either way, the child will terminate by itself -- wait for it.
                const stderr_reader = multi_reader.reader(1);
                const stderr_owned = try arena.dupe(u8, stderr_reader.buffered());

                // Clean up everything and wait for the child to exit.
                child.stdin.?.close(io);
                child.stdin = null;
                multi_reader.deinit();
                child_killed = true;
                const term = try child.wait(io);
                run.step.result_peak_rss = @max(
                    run.step.result_peak_rss,
                    child.resource_usage_statistics.getMaxRss() orelse 0,
                );

                if (no_poll.active_test_index) |test_index| {
                    // A test was running, so this is definitely a crash. Report it against that
                    // test, and continue to the next test.
                    test_metadata.?.ns_per_test[test_index] = no_poll.ns_elapsed;
                    test_results.crash_count += 1;
                    try run.step.addError("'{s}' {f}{s}{s}", .{
                        test_metadata.?.testName(test_index),
                        fmtTerm(term),
                        if (stderr_owned.len != 0) " with stderr:\n" else "",
                        std.mem.trim(u8, stderr_owned, "\n"),
                    });
                    continue;
                }

                // Report an error if the child terminated uncleanly or if we were still trying to run more tests.
                run.step.result_stderr = stderr_owned;
                const tests_done = test_metadata != null and test_metadata.?.next_index == std.math.maxInt(u32);
                if (!tests_done or !termMatches(.{ .exited = 0 }, term)) {
                    // The individual unit test results are irrelevant: the test runner itself broke!
                    // Fail immediately without populating `s.test_results`.
                    return run.step.fail("test process unexpectedly {f}", .{fmtTerm(term)});
                }

                // We're done with all of the tests! Commit the test results and return.
                run.step.test_results = test_results;
                if (test_metadata) |tm| {
                    run.cached_test_metadata = tm.toCachedTestMetadata();
                    if (options.web_server) |ws| {
                        if (run.step.owner.graph.time_report) {
                            ws.updateTimeReportRunTest(
                                run,
                                &run.cached_test_metadata.?,
                                tm.ns_per_test,
                            );
                        }
                    }
                }
                return;
            },
            .timeout => |timeout| {
                const stderr_reader = multi_reader.reader(1);
                const stderr = stderr_reader.buffered();
                stderr_reader.tossBuffered();
                if (timeout.active_test_index) |test_index| {
                    // A test was running. Report the timeout against that test, and continue on to
                    // the next test.
                    test_metadata.?.ns_per_test[test_index] = timeout.ns_elapsed;
                    test_results.timeout_count += 1;
                    try run.step.addError("'{s}' timed out after {f}{s}{s}", .{
                        test_metadata.?.testName(test_index),
                        Io.Duration{ .nanoseconds = timeout.ns_elapsed },
                        if (stderr.len != 0) " with stderr:\n" else "",
                        std.mem.trim(u8, stderr, "\n"),
                    });
                    continue;
                }
                // Just log an error and let the child be killed.
                run.step.result_stderr = try arena.dupe(u8, stderr);
                // The individual unit test results in `results` are irrelevant: the test runner
                // is broken! Fail immediately without populating `s.test_results`.
                return run.step.fail("test runner failed to respond for {f}", .{Io.Duration{ .nanoseconds = timeout.ns_elapsed }});
            },
        }
        comptime unreachable;
    }
}

/// Reads stdout of a Zig test process until a termination condition is reached:
/// * A write fails, indicating the child unexpectedly closed stdin
/// * A test (or a response from the test runner) times out
/// * The wait fails, indicating the child closed stdout and stderr
fn waitZigTest(
    run: *Run,
    child: *process.Child,
    options: Step.MakeOptions,
    multi_reader: *Io.File.MultiReader,
    opt_metadata: *?TestMetadata,
    results: *Step.TestResults,
) !union(enum) {
    write_failed: anyerror,
    no_poll: struct {
        active_test_index: ?u32,
        ns_elapsed: u64,
    },
    timeout: struct {
        active_test_index: ?u32,
        ns_elapsed: u64,
    },
} {
    const gpa = run.step.owner.allocator;
    const arena = run.step.owner.allocator;
    const io = run.step.owner.graph.io;

    var sub_prog_node: ?std.Progress.Node = null;
    defer if (sub_prog_node) |n| n.end();

    if (opt_metadata.*) |*md| {
        // Previous unit test process died or was killed; we're continuing where it left off
        requestNextTest(io, child.stdin.?, md, &sub_prog_node) catch |err| return .{ .write_failed = err };
    } else {
        // Running unit tests normally
        run.fuzz_tests.clearRetainingCapacity();
        sendMessage(io, child.stdin.?, .query_test_metadata) catch |err| return .{ .write_failed = err };
    }

    var active_test_index: ?u32 = null;

    var last_update: Io.Clock.Timestamp = .now(io, .awake);

    // This timeout is used when we're waiting on the test runner itself rather than a user-specified
    // test. For instance, if the test runner leaves this much time between us requesting a test to
    // start and it acknowledging the test starting, we terminate the child and raise an error. This
    // *should* never happen, but could in theory be caused by some very unlucky IB in a test.
    const response_timeout: Io.Clock.Duration = t: {
        const ns = @max(options.unit_test_timeout_ns orelse 0, 60 * std.time.ns_per_s);
        break :t .{ .clock = .awake, .raw = .fromNanoseconds(ns) };
    };
    const test_timeout: ?Io.Clock.Duration = if (options.unit_test_timeout_ns) |ns| .{
        .clock = .awake,
        .raw = .fromNanoseconds(ns),
    } else null;

    const stdout = multi_reader.reader(0);
    const stderr = multi_reader.reader(1);
    const Header = std.zig.Server.Message.Header;

    while (true) {
        const timeout: Io.Timeout = t: {
            const opt_duration = if (active_test_index == null) response_timeout else test_timeout;
            const duration = opt_duration orelse break :t .none;
            break :t .{ .deadline = last_update.addDuration(duration) };
        };

        // This block is exited when `stdout` contains enough bytes for a `Header`.
        header_ready: {
            if (stdout.buffered().len >= @sizeOf(Header)) {
                // We already have one, no need to poll!
                break :header_ready;
            }

            multi_reader.fill(64, timeout) catch |err| switch (err) {
                error.Timeout => return .{ .timeout = .{
                    .active_test_index = active_test_index,
                    .ns_elapsed = @intCast(last_update.untilNow(io).raw.nanoseconds),
                } },
                error.EndOfStream => return .{ .no_poll = .{
                    .active_test_index = active_test_index,
                    .ns_elapsed = @intCast(last_update.untilNow(io).raw.nanoseconds),
                } },
                else => |e| return e,
            };

            continue;
        }
        // There is definitely a header available now -- read it.
        const header = stdout.takeStruct(Header, .little) catch unreachable;

        while (stdout.buffered().len < header.bytes_len) {
            multi_reader.fill(64, timeout) catch |err| switch (err) {
                error.Timeout => return .{ .timeout = .{
                    .active_test_index = active_test_index,
                    .ns_elapsed = @intCast(last_update.untilNow(io).raw.nanoseconds),
                } },
                error.EndOfStream => return .{ .no_poll = .{
                    .active_test_index = active_test_index,
                    .ns_elapsed = @intCast(last_update.untilNow(io).raw.nanoseconds),
                } },
                else => |e| return e,
            };
        }

        const body = stdout.take(header.bytes_len) catch unreachable;
        var body_r: std.Io.Reader = .fixed(body);
        switch (header.tag) {
            .zig_version => {
                if (!std.mem.eql(u8, builtin.zig_version_string, body)) return run.step.fail(
                    "zig version mismatch build runner vs compiler: '{s}' vs '{s}'",
                    .{ builtin.zig_version_string, body },
                );
            },
            .test_metadata => {
                // `metadata` would only be populated if we'd already seen a `test_metadata`, but we
                // only request it once (and importantly, we don't re-request it if we kill and
                // restart the test runner).
                assert(opt_metadata.* == null);

                const tm_hdr = body_r.takeStruct(std.zig.Server.Message.TestMetadata, .little) catch unreachable;
                results.test_count = tm_hdr.tests_len;

                const names = try arena.alloc(u32, results.test_count);
                for (names) |*dest| dest.* = body_r.takeInt(u32, .little) catch unreachable;

                const expected_panic_msgs = try arena.alloc(u32, results.test_count);
                for (expected_panic_msgs) |*dest| dest.* = body_r.takeInt(u32, .little) catch unreachable;

                const string_bytes = body_r.take(tm_hdr.string_bytes_len) catch unreachable;

                options.progress_node.setEstimatedTotalItems(names.len);
                opt_metadata.* = .{
                    .string_bytes = try arena.dupe(u8, string_bytes),
                    .ns_per_test = try arena.alloc(u64, results.test_count),
                    .names = names,
                    .expected_panic_msgs = expected_panic_msgs,
                    .next_index = 0,
                    .prog_node = options.progress_node,
                };
                @memset(opt_metadata.*.?.ns_per_test, std.math.maxInt(u64));

                active_test_index = null;
                last_update = .now(io, .awake);

                requestNextTest(io, child.stdin.?, &opt_metadata.*.?, &sub_prog_node) catch |err| return .{ .write_failed = err };
            },
            .test_started => {
                active_test_index = opt_metadata.*.?.next_index - 1;
                last_update = .now(io, .awake);
            },
            .test_results => {
                const md = &opt_metadata.*.?;

                const tr_hdr = body_r.takeStruct(std.zig.Server.Message.TestResults, .little) catch unreachable;
                assert(tr_hdr.index == active_test_index);

                switch (tr_hdr.flags.status) {
                    .pass => {},
                    .skip => results.skip_count +|= 1,
                    .fail => results.fail_count +|= 1,
                }
                const leak_count = tr_hdr.flags.leak_count;
                const log_err_count = tr_hdr.flags.log_err_count;
                results.leak_count +|= leak_count;
                results.log_err_count +|= log_err_count;

                if (tr_hdr.flags.fuzz) try run.fuzz_tests.append(gpa, md.testName(tr_hdr.index));

                if (tr_hdr.flags.status == .fail) {
                    const name = md.testName(tr_hdr.index);
                    const stderr_bytes = std.mem.trim(u8, stderr.buffered(), "\n");
                    stderr.tossBuffered();
                    if (stderr_bytes.len == 0) {
                        try run.step.addError("'{s}' failed without output", .{name});
                    } else {
                        try run.step.addError("'{s}' failed:\n{s}", .{ name, stderr_bytes });
                    }
                } else if (leak_count > 0) {
                    const name = md.testName(tr_hdr.index);
                    const stderr_bytes = std.mem.trim(u8, stderr.buffered(), "\n");
                    stderr.tossBuffered();
                    try run.step.addError("'{s}' leaked {d} allocations:\n{s}", .{ name, leak_count, stderr_bytes });
                } else if (log_err_count > 0) {
                    const name = md.testName(tr_hdr.index);
                    const stderr_bytes = std.mem.trim(u8, stderr.buffered(), "\n");
                    stderr.tossBuffered();
                    try run.step.addError("'{s}' logged {d} errors:\n{s}", .{ name, log_err_count, stderr_bytes });
                }

                active_test_index = null;

                const now: Io.Clock.Timestamp = .now(io, .awake);
                md.ns_per_test[tr_hdr.index] = @intCast(last_update.durationTo(now).raw.nanoseconds);
                last_update = now;

                requestNextTest(io, child.stdin.?, md, &sub_prog_node) catch |err| return .{ .write_failed = err };
            },
            else => {}, // ignore other messages
        }
    }
}

const FuzzTestRunner = struct {
    run: *Run,
    ctx: FuzzContext,
    coverage_id: ?u64,

    instances: []Instance,
    /// The indexes of this are layed out such that it is effectively an array
    /// of `[instances.len][3]Io.Operation.Storage` of stdin, stdout, stderr.
    batch: Io.Batch,
    /// LIFO. Stream of message bodies trailed by PendingBroadcastFooter.
    pending_broadcasts: std.ArrayList(u8),
    broadcast: std.ArrayList(u8),
    broadcast_undelivered: u32,

    const Instance = struct {
        child: process.Child,
        message: std.ArrayListAligned(u8, .@"4"),
        broadcast_written: usize,
        stderr: std.ArrayList(u8),
        stdin_vec: [1][]u8,
        stdout_vec: [1][]u8,
        stderr_vec: [1][]u8,
        progress_node: std.Progress.Node,

        fn messageHeader(instance: *Instance) InHeader {
            assert(instance.message.items.len >= @sizeOf(InHeader));
            const header_ptr: *InHeader = @ptrCast(instance.message.items);
            var header = header_ptr.*;
            if (std.builtin.Endian.native != .little) {
                std.mem.byteSwapAllFields(InHeader, &header);
            }
            return header;
        }
    };

    const PendingBroadcastFooter = struct {
        from_id: u32,
        body_len: u32,
    };

    const InHeader = std.zig.Server.Message.Header;
    const OutHeader = std.zig.Client.Message.Header;

    const stdin_i = 0;
    const stdout_i = 1;
    const stderr_i = 2;

    fn init(
        run: *Run,
        ctx: FuzzContext,
        progress_node: std.Progress.Node,
        spawn_options: process.SpawnOptions,
    ) !FuzzTestRunner {
        const step_owner = run.step.owner;
        const gpa = step_owner.allocator;
        const io = step_owner.graph.io;

        const n_instances = switch (ctx.fuzz.mode) {
            .forever => step_owner.graph.max_jobs orelse @min(
                std.Thread.getCpuCount() catch 1,
                (std.math.maxInt(u32) - 2) / 3,
            ),
            .limit => 1,
        };
        const instances = try gpa.alloc(Instance, n_instances);
        errdefer gpa.free(instances);
        const batch_storage = try gpa.alloc(Io.Operation.Storage, instances.len * 3);
        errdefer gpa.free(batch_storage);

        @memset(instances, .{
            .child = undefined,
            .message = .empty,
            .broadcast_written = undefined,
            .stderr = .empty,
            .stdin_vec = undefined,
            .stdout_vec = undefined,
            .stderr_vec = undefined,
            .progress_node = undefined,
        });
        for (0.., instances) |id, *instance| {
            errdefer for (instances[0..id]) |*spawned| {
                spawned.child.kill(io);
                spawned.progress_node.end();
            };
            instance.child = try process.spawn(io, spawn_options);
            instance.progress_node = progress_node.start("starting fuzzer", 0);
        }

        return .{
            .run = run,
            .ctx = ctx,
            .coverage_id = null,

            .instances = instances,
            .batch = .init(batch_storage),
            .pending_broadcasts = .empty,
            .broadcast = .empty,
            .broadcast_undelivered = 0,
        };
    }

    fn deinit(f: *FuzzTestRunner) void {
        const step_owner = f.run.step.owner;
        const gpa = step_owner.allocator;
        const io = step_owner.graph.io;

        f.batch.cancel(io);
        gpa.free(f.batch.storage);
        var total_rss: usize = 0;
        for (f.instances) |*instance| {
            instance.child.kill(io);
            instance.message.deinit(gpa);
            instance.stderr.deinit(gpa);
            instance.progress_node.end();
            total_rss += instance.child.resource_usage_statistics.getMaxRss() orelse 0;
        }
        f.run.step.result_peak_rss = @max(f.run.step.result_peak_rss, total_rss);
        gpa.free(f.instances);
    }

    fn startInstances(f: *FuzzTestRunner) !void {
        const step_owner = f.run.step.owner;
        const io = step_owner.graph.io;

        for (0.., f.instances) |id, *instance| {
            const id32: u32 = @intCast(id);
            (switch (f.ctx.fuzz.mode) {
                .forever => sendRunFuzzTestMessage(
                    io,
                    instance.child.stdin.?,
                    f.run.fuzz_tests.items,
                    .forever,
                    id32,
                ),
                .limit => |limit| sendRunFuzzTestMessage(
                    io,
                    instance.child.stdin.?,
                    f.run.fuzz_tests.items,
                    .iterations,
                    limit.amount,
                ),
            }) catch |write_err| {
                // The runner unexpectedly closed stdin, which means it crashed during initialization.
                // Clean up everything and wait for the child to exit.
                instance.child.stdin.?.close(io);
                instance.child.stdin = null;
                const term = try instance.child.wait(io);
                return f.run.step.fail(
                    "unable to write stdin ({t}); test process unexpectedly {f}",
                    .{ write_err, fmtTerm(term) },
                );
            };

            try f.addStdoutRead(id32, @sizeOf(InHeader));
            try f.addStderrRead(id32);
        }
    }

    fn listen(f: *FuzzTestRunner) !void {
        const step_owner = f.run.step.owner;
        const io = step_owner.graph.io;

        while (true) {
            try f.batch.awaitConcurrent(io, .none);
            while (f.batch.next()) |completion| {
                const id = completion.index / 3;
                const result = completion.result;
                switch (completion.index % 3) {
                    0 => try f.completeStdinWrite(id, result.file_write_streaming catch |e| switch (e) {
                        error.BrokenPipe => return f.instanceEos(id),
                        else => |write_e| return write_e,
                    }),
                    1 => try f.completeStdoutRead(id, result.file_read_streaming catch |e| switch (e) {
                        error.EndOfStream => return f.instanceEos(id),
                        else => |read_e| return read_e,
                    }),
                    2 => try f.completeStderrRead(id, result.file_read_streaming catch |e| switch (e) {
                        error.EndOfStream => return f.instanceEos(id),
                        else => |read_e| return read_e,
                    }),
                    else => unreachable,
                }
            }
        }
    }

    fn completeStdoutRead(f: *FuzzTestRunner, id: u32, n: usize) !void {
        const step_owner = f.run.step.owner;
        const gpa = step_owner.allocator;
        const io = step_owner.graph.io;
        const instance = &f.instances[id];

        instance.message.items.len += n;
        const total_read = instance.message.items.len;
        if (total_read < @sizeOf(InHeader)) {
            try f.addStdoutRead(id, @sizeOf(InHeader));
            return;
        }

        const header = instance.messageHeader();
        const body = instance.message.items[@sizeOf(InHeader)..];
        if (body.len != header.bytes_len) {
            try f.addStdoutRead(id, @sizeOf(InHeader) + header.bytes_len);
            return;
        }

        switch (header.tag) {
            .zig_version => {
                if (!std.mem.eql(u8, builtin.zig_version_string, body)) return f.run.step.fail(
                    "zig version mismatch build runner vs compiler: '{s}' vs '{s}'",
                    .{ builtin.zig_version_string, body },
                );
            },
            .coverage_id => {
                var body_r: Io.Reader = .fixed(body);
                f.coverage_id = body_r.takeInt(u64, .little) catch unreachable;
                const cumulative_runs = body_r.takeInt(u64, .little) catch unreachable;
                const cumulative_unique = body_r.takeInt(u64, .little) catch unreachable;
                const cumulative_coverage = body_r.takeInt(u64, .little) catch unreachable;

                const fuzz = f.ctx.fuzz;
                fuzz.queue_mutex.lockUncancelable(io);
                defer fuzz.queue_mutex.unlock(io);
                try fuzz.msg_queue.append(fuzz.gpa, .{ .coverage = .{
                    .id = f.coverage_id.?,
                    .cumulative = .{
                        .runs = cumulative_runs,
                        .unique = cumulative_unique,
                        .coverage = cumulative_coverage,
                    },
                    .run = f.run,
                } });
                fuzz.queue_cond.signal(io);
            },
            .fuzz_start_addr => {
                var body_r: Io.Reader = .fixed(body);
                const fuzz = f.ctx.fuzz;
                const addr = body_r.takeInt(u64, .little) catch unreachable;

                fuzz.queue_mutex.lockUncancelable(io);
                defer fuzz.queue_mutex.unlock(io);
                try fuzz.msg_queue.append(fuzz.gpa, .{ .entry_point = .{
                    .addr = addr,
                    .coverage_id = f.coverage_id.?,
                } });
                fuzz.queue_cond.signal(io);
            },
            .fuzz_test_change => {
                const test_i = std.mem.readInt(u32, body[0..4], .little);
                instance.progress_node.setName(f.run.fuzz_tests.items[test_i]);
            },
            .broadcast_fuzz_input => {
                if (f.instances.len == 1) {
                    // No other processes to broadcast to.
                } else if (f.broadcast_undelivered == 0) {
                    try f.instanceBroadcast(id, body);
                } else {
                    const footer: PendingBroadcastFooter = .{
                        .from_id = id,
                        .body_len = @intCast(body.len),
                    };
                    // There is another broadcast in progress so add this one to the queue.
                    const size = @sizeOf(PendingBroadcastFooter) + body.len;
                    try f.pending_broadcasts.ensureUnusedCapacity(gpa, size);
                    f.pending_broadcasts.appendSliceAssumeCapacity(body);
                    f.pending_broadcasts.appendSliceAssumeCapacity(@ptrCast(&footer));
                }
            },
            else => {}, // ignore other messages
        }

        instance.message.clearRetainingCapacity();
        try f.addStdoutRead(id, @sizeOf(InHeader));
    }

    fn completeStderrRead(f: *FuzzTestRunner, id: u32, n: usize) !void {
        const instance = &f.instances[id];
        instance.stderr.items.len += n;
        try f.addStderrRead(id);
    }

    fn completeStdinWrite(f: *FuzzTestRunner, id: u32, n: usize) !void {
        const instance = &f.instances[id];

        instance.broadcast_written += n;
        if (instance.broadcast_written == f.broadcast.items.len) {
            f.broadcast_undelivered -= 1;
            if (f.broadcast_undelivered == 0) {
                try f.broadcastComplete();
            }
        } else {
            f.addStdinWrite(id);
        }
    }

    fn addStdoutRead(f: *FuzzTestRunner, id: u32, end: usize) !void {
        const step_owner = f.run.step.owner;
        const gpa = step_owner.allocator;
        const instance = &f.instances[id];

        try instance.message.ensureTotalCapacity(gpa, end);
        const start = instance.message.items.len;
        instance.stdout_vec = .{instance.message.allocatedSlice()[start..end]};
        f.batch.addAt(id * 3 + stdout_i, .{ .file_read_streaming = .{
            .file = instance.child.stdout.?,
            .data = &instance.stdout_vec,
        } });
    }

    fn addStderrRead(f: *FuzzTestRunner, id: u32) !void {
        const step_owner = f.run.step.owner;
        const gpa = step_owner.allocator;
        const instance = &f.instances[id];

        try instance.stderr.ensureUnusedCapacity(gpa, 1);
        instance.stderr_vec = .{instance.stderr.unusedCapacitySlice()};
        f.batch.addAt(id * 3 + stderr_i, .{ .file_read_streaming = .{
            .file = instance.child.stderr.?,
            .data = &instance.stderr_vec,
        } });
    }

    fn addStdinWrite(f: *FuzzTestRunner, id: u32) void {
        const instance = &f.instances[id];

        assert(f.broadcast.items.len != instance.broadcast_written);
        instance.stdin_vec = .{f.broadcast.items[instance.broadcast_written..]};
        f.batch.addAt(id * 3 + stdin_i, .{ .file_write_streaming = .{
            .file = instance.child.stdin.?,
            .data = &instance.stdin_vec,
        } });
    }

    fn instanceEos(f: *FuzzTestRunner, id: u32) !void {
        const step_owner = f.run.step.owner;
        const io = step_owner.graph.io;
        const instance = &f.instances[id];

        instance.child.stdin.?.close(io);
        instance.child.stdin = null;
        const term = try instance.child.wait(io);
        if (!termMatches(.{ .exited = 0 }, term)) {
            f.run.step.result_stderr = try f.mergedStderr();
            try f.saveCrash(id, term);
            return f.run.step.fail("test process unexpectedly {f}", .{fmtTerm(term)});
        }
    }

    fn saveCrash(f: *FuzzTestRunner, id: u32, term: process.Child.Term) !void {
        const step = &f.run.step;
        const b = step.owner;
        const io = b.graph.io;

        if (f.coverage_id == null) return;

        // Search for the input file corresponding to the instance
        const InputHeader = Build.abi.fuzz.MmapInputHeader;
        var in_r_buf: [@sizeOf(InputHeader)]u8 = undefined;
        var in_r: Io.File.Reader = undefined;
        var in_f: Io.File = undefined;
        var in_name_buf: [12]u8 = undefined;
        var in_name: []const u8 = undefined;
        var i: u32 = 0;
        const header: InputHeader = while (true) {
            const name_prefix = "f" ++ Io.Dir.path.sep_str ++ "in";
            in_name = std.fmt.bufPrint(&in_name_buf, name_prefix ++ "{x}", .{i}) catch unreachable;
            in_f = b.cache_root.handle.openFile(io, in_name, .{
                .lock = .exclusive,
                .lock_nonblocking = true,
            }) catch |e| switch (e) {
                error.FileNotFound => return,
                error.WouldBlock => continue, // Can not be from
                // the crashed instance since it is still locked.
                else => return step.fail("failed to open file '{f}{s}': {t}", .{
                    b.cache_root, in_name, e,
                }),
            };

            in_r = in_f.readerStreaming(io, &in_r_buf);
            const header = in_r.interface.takeStruct(InputHeader, .little) catch |e| {
                in_f.close(io);
                switch (e) {
                    error.ReadFailed => return step.fail("failed to read file '{f}{s}': {t}", .{
                        b.cache_root, in_name, in_r.err.?,
                    }),
                    error.EndOfStream => continue,
                }
            };

            if (header.pc_digest == f.coverage_id.? and
                header.instance_id == id and
                header.test_i < f.run.fuzz_tests.items.len)
            {
                break header;
            }

            in_f.close(io);
            if (i == std.math.maxInt(u32)) return;
            i += 1;
        };
        defer in_f.close(io);

        // Save it to a seperate file
        const crash_name = "f" ++ Io.Dir.path.sep_str ++ "crash";
        const out = b.cache_root.handle.createFile(io, crash_name, .{
            .lock = .exclusive, // Multiple run steps could have found a crash at the same time
        }) catch |e| return step.fail("failed to create file '{f}{s}': {t}", .{
            b.cache_root, crash_name, e,
        });
        defer out.close(io);

        var out_w_buf: [512]u8 = undefined;
        var out_w = out.writerStreaming(io, &out_w_buf);
        _ = out_w.interface.sendFileAll(&in_r, .limited(header.len)) catch |e| switch (e) {
            error.ReadFailed => return step.fail("failed to read file '{f}{s}': {t}", .{
                b.cache_root, in_name, in_r.err.?,
            }),
            error.WriteFailed => return step.fail("failed to write file '{f}{s}': {t}", .{
                b.cache_root, crash_name, out_w.err.?,
            }),
        };

        return f.run.step.fail("test '{s}' {f}; input saved to '{f}{s}'", .{
            f.run.fuzz_tests.items[header.test_i],
            fmtTerm(term),
            b.cache_root,
            crash_name,
        });
    }

    fn instanceBroadcast(f: *FuzzTestRunner, from_id: u32, bytes: []const u8) !void {
        assert(f.instances.len > 1);
        assert(f.broadcast_undelivered == 0); // no other broadcast is progress
        assert(f.broadcast.items.len == 0);
        assert(from_id < f.instances.len);

        const step_owner = f.run.step.owner;
        const gpa = step_owner.allocator;

        var out_header: OutHeader = .{
            .tag = .new_fuzz_input,
            .bytes_len = @intCast(bytes.len),
        };
        if (std.builtin.Endian.native != .little) {
            std.mem.byteSwapAllFields(OutHeader, &out_header);
        }
        try f.broadcast.ensureTotalCapacity(gpa, @sizeOf(OutHeader) + bytes.len);
        f.broadcast.appendSliceAssumeCapacity(@ptrCast(&out_header));
        f.broadcast.appendSliceAssumeCapacity(bytes);

        f.broadcast_undelivered = @intCast(f.instances.len - 1);
        for (0.., f.instances) |to_id, *instance| {
            if (to_id == from_id) continue;
            instance.broadcast_written = 0;
            f.addStdinWrite(@intCast(to_id));
        }
    }

    fn broadcastComplete(f: *FuzzTestRunner) !void {
        assert(f.instances.len > 1);
        assert(f.broadcast_undelivered == 0);
        f.broadcast.clearRetainingCapacity();

        const pending = &f.pending_broadcasts;
        if (pending.items.len != 0) {
            // Another broadcast is pending; copy it over to `broadcast`

            const footer_len = @sizeOf(PendingBroadcastFooter);
            const footer_bytes = pending.items[pending.items.len - footer_len ..];
            const footer: *align(1) PendingBroadcastFooter = @ptrCast(footer_bytes);
            pending.items.len -= footer_len;

            const body = pending.items[pending.items.len - footer.body_len ..];
            try f.instanceBroadcast(footer.from_id, body);
            pending.items.len -= body.len;
        }
    }

    fn mergedStderr(f: *FuzzTestRunner) std.mem.Allocator.Error![]const u8 {
        const step_owner = f.run.step.owner;
        const arena = step_owner.allocator;

        // Collect any remaining stderr
        while (f.batch.next()) |completion| {
            if (completion.index % 3 != 2) continue;
            const len = completion.result.file_read_streaming catch continue;
            f.instances[completion.index / 3].stderr.items.len += len;
        }

        var stderr_len: usize = 0;
        for (f.instances) |*instance| stderr_len += instance.stderr.items.len;
        const stderr = try arena.alloc(u8, stderr_len);

        stderr_len = 0;
        for (f.instances) |*instance| {
            @memcpy(stderr[stderr_len..][0..instance.stderr.items.len], instance.stderr.items);
            stderr_len += instance.stderr.items.len;
        }
        return stderr;
    }
};

fn evalFuzzTest(
    run: *Run,
    spawn_options: process.SpawnOptions,
    options: Step.MakeOptions,
    fuzz_context: FuzzContext,
) !void {
    var f: FuzzTestRunner = try .init(run, fuzz_context, options.progress_node, spawn_options);
    defer f.deinit();
    try f.startInstances();
    try f.listen();
}

const TestMetadata = struct {
    names: []const u32,
    ns_per_test: []u64,
    expected_panic_msgs: []const u32,
    string_bytes: []const u8,
    next_index: u32,
    prog_node: std.Progress.Node,

    fn toCachedTestMetadata(tm: TestMetadata) CachedTestMetadata {
        return .{
            .names = tm.names,
            .string_bytes = tm.string_bytes,
        };
    }

    fn testName(tm: TestMetadata, index: u32) []const u8 {
        return tm.toCachedTestMetadata().testName(index);
    }
};

pub const CachedTestMetadata = struct {
    names: []const u32,
    string_bytes: []const u8,

    pub fn testName(tm: CachedTestMetadata, index: u32) []const u8 {
        return std.mem.sliceTo(tm.string_bytes[tm.names[index]..], 0);
    }
};

fn requestNextTest(io: Io, in: Io.File, metadata: *TestMetadata, sub_prog_node: *?std.Progress.Node) !void {
    while (metadata.next_index < metadata.names.len) {
        const i = metadata.next_index;
        metadata.next_index += 1;

        if (metadata.expected_panic_msgs[i] != 0) continue;

        const name = metadata.testName(i);
        if (sub_prog_node.*) |n| n.end();
        sub_prog_node.* = metadata.prog_node.start(name, 0);

        try sendRunTestMessage(io, in, .run_test, i);
        return;
    } else {
        metadata.next_index = std.math.maxInt(u32); // indicate that all tests are done
        try sendMessage(io, in, .exit);
    }
}

fn sendMessage(io: Io, file: Io.File, tag: std.zig.Client.Message.Tag) !void {
    const header: std.zig.Client.Message.Header = .{
        .tag = tag,
        .bytes_len = 0,
    };
    var w = file.writerStreaming(io, &.{});
    w.interface.writeStruct(header, .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
}

fn sendRunTestMessage(io: Io, file: Io.File, tag: std.zig.Client.Message.Tag, index: u32) !void {
    const header: std.zig.Client.Message.Header = .{
        .tag = tag,
        .bytes_len = 4,
    };
    var w = file.writerStreaming(io, &.{});
    w.interface.writeStruct(header, .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
    w.interface.writeInt(u32, index, .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
}

fn sendRunFuzzTestMessage(
    io: Io,
    file: Io.File,
    test_names: []const []const u8,
    kind: std.Build.abi.fuzz.LimitKind,
    amount_or_instance: u64,
) !void {
    const header: std.zig.Client.Message.Header = .{
        .tag = .start_fuzzing,
        .bytes_len = 1 + 8 + 4 + count: {
            var c: u32 = @intCast(test_names.len * 4);
            for (test_names) |name| {
                c += @intCast(name.len);
            }
            break :count c;
        },
    };
    var w = file.writerStreaming(io, &.{});
    w.interface.writeStruct(header, .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
    w.interface.writeByte(@intFromEnum(kind)) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
    w.interface.writeInt(u64, amount_or_instance, .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
    w.interface.writeInt(u32, @intCast(test_names.len), .little) catch |err| switch (err) {
        error.WriteFailed => return w.err.?,
    };
    for (test_names) |test_name| {
        w.interface.writeInt(u32, @intCast(test_name.len), .little) catch |err| switch (err) {
            error.WriteFailed => return w.err.?,
        };
        w.interface.writeAll(test_name) catch |err| switch (err) {
            error.WriteFailed => return w.err.?,
        };
    }
}

fn evalGeneric(run: *Run, spawn_options: process.SpawnOptions) !EvalGenericResult {
    const b = run.step.owner;
    const io = b.graph.io;
    const arena = b.allocator;
    const gpa = b.allocator;

    var child = try process.spawn(io, spawn_options);
    defer child.kill(io);

    switch (run.stdin) {
        .bytes => |bytes| {
            child.stdin.?.writeStreamingAll(io, bytes) catch |err| {
                return run.step.fail("unable to write stdin: {t}", .{err});
            };
            child.stdin.?.close(io);
            child.stdin = null;
        },
        .lazy_path => |lazy_path| {
            const path = lazy_path.getPath3(b, &run.step);
            const file = path.root_dir.handle.openFile(io, path.subPathOrDot(), .{}) catch |err| {
                return run.step.fail("unable to open stdin file: {t}", .{err});
            };
            defer file.close(io);
            // TODO https://github.com/ziglang/zig/issues/23955
            var read_buffer: [1024]u8 = undefined;
            var file_reader = file.reader(io, &read_buffer);
            var write_buffer: [1024]u8 = undefined;
            var stdin_writer = child.stdin.?.writerStreaming(io, &write_buffer);
            _ = stdin_writer.interface.sendFileAll(&file_reader, .unlimited) catch |err| switch (err) {
                error.ReadFailed => return run.step.fail("failed to read from {f}: {t}", .{
                    path, file_reader.err.?,
                }),
                error.WriteFailed => return run.step.fail("failed to write to stdin: {t}", .{
                    stdin_writer.err.?,
                }),
            };
            stdin_writer.interface.flush() catch |err| switch (err) {
                error.WriteFailed => return run.step.fail("failed to write to stdin: {t}", .{
                    stdin_writer.err.?,
                }),
            };
            child.stdin.?.close(io);
            child.stdin = null;
        },
        .none => {},
    }

    var stdout_bytes: ?[]const u8 = null;
    var stderr_bytes: ?[]const u8 = null;

    if (child.stdout) |stdout| {
        if (child.stderr) |stderr| {
            var multi_reader_buffer: Io.File.MultiReader.Buffer(2) = undefined;
            var multi_reader: Io.File.MultiReader = undefined;
            multi_reader.init(gpa, io, multi_reader_buffer.toStreams(), &.{ stdout, stderr });
            defer multi_reader.deinit();

            const stdout_reader = multi_reader.reader(0);
            const stderr_reader = multi_reader.reader(1);

            while (multi_reader.fill(64, .none)) |_| {
                if (run.stdio_limit.toInt()) |limit| {
                    if (stdout_reader.buffered().len > limit)
                        return error.StdoutStreamTooLong;
                    if (stderr_reader.buffered().len > limit)
                        return error.StderrStreamTooLong;
                }
            } else |err| switch (err) {
                error.Timeout => unreachable,
                error.EndOfStream => {},
                else => |e| return e,
            }

            try multi_reader.checkAnyError();

            stdout_bytes = try multi_reader.toOwnedSlice(0);
            stderr_bytes = try multi_reader.toOwnedSlice(1);
        } else {
            var stdout_reader = stdout.readerStreaming(io, &.{});
            stdout_bytes = stdout_reader.interface.allocRemaining(arena, run.stdio_limit) catch |err| switch (err) {
                error.OutOfMemory => return error.OutOfMemory,
                error.ReadFailed => return stdout_reader.err.?,
                error.StreamTooLong => return error.StdoutStreamTooLong,
            };
        }
    } else if (child.stderr) |stderr| {
        var stderr_reader = stderr.readerStreaming(io, &.{});
        stderr_bytes = stderr_reader.interface.allocRemaining(arena, run.stdio_limit) catch |err| switch (err) {
            error.OutOfMemory => return error.OutOfMemory,
            error.ReadFailed => return stderr_reader.err.?,
            error.StreamTooLong => return error.StderrStreamTooLong,
        };
    }

    if (stderr_bytes) |bytes| if (bytes.len > 0) {
        // Treat stderr as an error message.
        const stderr_is_diagnostic = run.captured_stderr == null and switch (run.stdio) {
            .check => |checks| !checksContainStderr(checks.items),
            else => true,
        };
        if (stderr_is_diagnostic) {
            run.step.result_stderr = bytes;
        }
    };

    run.step.result_peak_rss = child.resource_usage_statistics.getMaxRss() orelse 0;

    return .{
        .term = try child.wait(io),
        .stdout = stdout_bytes,
        .stderr = stderr_bytes,
    };
}

fn addPathForDynLibs(run: *Run, artifact: *Step.Compile) void {
    const b = run.step.owner;
    const compiles = artifact.getCompileDependencies(true);
    for (compiles) |compile| {
        if (compile.root_module.resolved_target.?.result.os.tag == .windows and
            compile.isDynamicLibrary())
        {
            addPathDir(run, Dir.path.dirname(compile.getEmittedBin().getPath2(b, &run.step)).?);
        }
    }
}

fn failForeign(
    run: *Run,
    suggested_flag: []const u8,
    argv0: []const u8,
    exe: *Step.Compile,
) error{ MakeFailed, MakeSkipped, OutOfMemory } {
    switch (run.stdio) {
        .check, .zig_test => {
            if (run.skip_foreign_checks)
                return error.MakeSkipped;

            const b = run.step.owner;
            const host_name = try b.graph.host.result.zigTriple(b.allocator);
            const foreign_name = try exe.rootModuleTarget().zigTriple(b.allocator);

            return run.step.fail(
                \\unable to spawn foreign binary '{s}' ({s}) on host system ({s})
                \\  consider using {s} or enabling skip_foreign_checks in the Run step
            , .{ argv0, foreign_name, host_name, suggested_flag });
        },
        else => {
            return run.step.fail("unable to spawn foreign binary '{s}'", .{argv0});
        },
    }
}

fn hashStdIo(hh: *std.Build.Cache.HashHelper, stdio: StdIo) void {
    switch (stdio) {
        .infer_from_args, .inherit, .zig_test => {},
        .check => |checks| for (checks.items) |check| {
            hh.add(@as(std.meta.Tag(StdIo.Check), check));
            switch (check) {
                .expect_stderr_exact,
                .expect_stderr_match,
                .expect_stdout_exact,
                .expect_stdout_match,
                => |s| hh.addBytes(s),

                .expect_term => |term| {
                    hh.add(@as(std.meta.Tag(process.Child.Term), term));
                    switch (term) {
                        inline .exited, .signal, .stopped => |x| hh.add(x),
                        .unknown => |x| hh.add(x),
                    }
                },
            }
        },
    }
}



---
File: /std/Build/Step/TranslateC.zig
---

const std = @import("std");
const Step = std.Build.Step;
const LazyPath = std.Build.LazyPath;
const fs = std.fs;
const mem = std.mem;

const TranslateC = @This();

pub const base_id: Step.Id = .translate_c;

step: Step,
source: std.Build.LazyPath,
include_dirs: std.array_list.Managed(std.Build.Module.IncludeDir),
system_libs: std.ArrayList(std.Build.Module.SystemLib),
c_macros: std.array_list.Managed([]const u8),
out_basename: []const u8,
target: std.Build.ResolvedTarget,
optimize: std.builtin.OptimizeMode,
output_file: std.Build.GeneratedFile,
link_libc: bool,

pub const Options = struct {
    root_source_file: std.Build.LazyPath,
    target: std.Build.ResolvedTarget,
    optimize: std.builtin.OptimizeMode,
    link_libc: bool = true,
};

pub fn create(owner: *std.Build, options: Options) *TranslateC {
    const translate_c = owner.allocator.create(TranslateC) catch @panic("OOM");
    const source = options.root_source_file.dupe(owner);
    translate_c.* = .{
        .step = Step.init(.{
            .id = base_id,
            .name = "translate-c",
            .owner = owner,
            .makeFn = make,
        }),
        .source = source,
        .include_dirs = std.array_list.Managed(std.Build.Module.IncludeDir).init(owner.allocator),
        .c_macros = std.array_list.Managed([]const u8).init(owner.allocator),
        .out_basename = undefined,
        .target = options.target,
        .optimize = options.optimize,
        .output_file = .{ .step = &translate_c.step },
        .link_libc = options.link_libc,
        .system_libs = .empty,
    };
    source.addStepDependencies(&translate_c.step);
    return translate_c;
}

pub const AddExecutableOptions = struct {
    name: ?[]const u8 = null,
    version: ?std.SemanticVersion = null,
    target: ?std.Build.ResolvedTarget = null,
    optimize: ?std.builtin.OptimizeMode = null,
    linkage: ?std.builtin.LinkMode = null,
};

pub fn getOutput(translate_c: *TranslateC) std.Build.LazyPath {
    return .{ .generated = .{ .file = &translate_c.output_file } };
}

/// Creates a module from the translated source and adds it to the package's
/// module set making it available to other packages which depend on this one.
/// `createModule` can be used instead to create a private module.
pub fn addModule(translate_c: *TranslateC, name: []const u8) *std.Build.Module {
    return setUpModule(translate_c, translate_c.step.owner.addModule(name, .{
        .root_source_file = translate_c.getOutput(),
        .target = translate_c.target,
        .optimize = translate_c.optimize,
        .link_libc = translate_c.link_libc,
    }));
}

/// Creates a private module from the translated source to be used by the
/// current package, but not exposed to other packages depending on this one.
/// `addModule` can be used instead to create a public module.
pub fn createModule(translate_c: *TranslateC) *std.Build.Module {
    return setUpModule(translate_c, translate_c.step.owner.createModule(.{
        .root_source_file = translate_c.getOutput(),
        .target = translate_c.target,
        .optimize = translate_c.optimize,
        .link_libc = translate_c.link_libc,
    }));
}

fn setUpModule(translate_c: *TranslateC, module: *std.Build.Module) *std.Build.Module {
    const b = translate_c.step.owner;
    const arena = b.graph.arena;

    if (translate_c.link_libc) module.link_libc = true;

    for (translate_c.system_libs.items) |system_lib| {
        module.link_objects.append(arena, .{ .system_lib = system_lib }) catch @panic("OOM");
    }

    return module;
}

pub fn addAfterIncludePath(translate_c: *TranslateC, lazy_path: LazyPath) void {
    const b = translate_c.step.owner;
    translate_c.include_dirs.append(.{ .path_after = lazy_path.dupe(b) }) catch
        @panic("OOM");
    lazy_path.addStepDependencies(&translate_c.step);
}

pub fn addSystemIncludePath(translate_c: *TranslateC, lazy_path: LazyPath) void {
    const b = translate_c.step.owner;
    translate_c.include_dirs.append(.{ .path_system = lazy_path.dupe(b) }) catch
        @panic("OOM");
    lazy_path.addStepDependencies(&translate_c.step);
}

pub fn addIncludePath(translate_c: *TranslateC, lazy_path: LazyPath) void {
    const b = translate_c.step.owner;
    translate_c.include_dirs.append(.{ .path = lazy_path.dupe(b) }) catch
        @panic("OOM");
    lazy_path.addStepDependencies(&translate_c.step);
}

pub fn addConfigHeader(translate_c: *TranslateC, config_header: *Step.ConfigHeader) void {
    translate_c.include_dirs.append(.{ .config_header_step = config_header }) catch
        @panic("OOM");
    translate_c.step.dependOn(&config_header.step);
}

pub fn addSystemFrameworkPath(translate_c: *TranslateC, directory_path: LazyPath) void {
    const b = translate_c.step.owner;
    translate_c.include_dirs.append(.{ .framework_path_system = directory_path.dupe(b) }) catch
        @panic("OOM");
    directory_path.addStepDependencies(&translate_c.step);
}

pub fn addFrameworkPath(translate_c: *TranslateC, directory_path: LazyPath) void {
    const b = translate_c.step.owner;
    translate_c.include_dirs.append(.{ .framework_path = directory_path.dupe(b) }) catch
        @panic("OOM");
    directory_path.addStepDependencies(&translate_c.step);
}

pub fn addCheckFile(translate_c: *TranslateC, expected_matches: []const []const u8) *Step.CheckFile {
    return Step.CheckFile.create(
        translate_c.step.owner,
        translate_c.getOutput(),
        .{ .expected_matches = expected_matches },
    );
}

/// If the value is omitted, it is set to 1.
/// `name` and `value` need not live longer than the function call.
pub fn defineCMacro(translate_c: *TranslateC, name: []const u8, value: ?[]const u8) void {
    const macro = translate_c.step.owner.fmt("{s}={s}", .{ name, value orelse "1" });
    translate_c.c_macros.append(macro) catch @panic("OOM");
}

/// name_and_value looks like [name]=[value]. If the value is omitted, it is set to 1.
pub fn defineCMacroRaw(translate_c: *TranslateC, name_and_value: []const u8) void {
    translate_c.c_macros.append(translate_c.step.owner.dupe(name_and_value)) catch @panic("OOM");
}

fn make(step: *Step, options: Step.MakeOptions) !void {
    const prog_node = options.progress_node;
    const b = step.owner;
    const translate_c: *TranslateC = @fieldParentPtr("step", step);
    const arena = b.graph.arena;

    var argv_list = std.array_list.Managed([]const u8).init(b.allocator);
    try argv_list.append(b.graph.zig_exe);
    try argv_list.append("translate-c");
    if (translate_c.link_libc) {
        try argv_list.append("-lc");
    }

    try argv_list.append("--cache-dir");
    try argv_list.append(b.cache_root.path orelse ".");

    try argv_list.append("--global-cache-dir");
    try argv_list.append(b.graph.global_cache_root.path orelse ".");

    if (!translate_c.target.query.isNative()) {
        try argv_list.append("-target");
        try argv_list.append(try translate_c.target.query.zigTriple(b.allocator));
    }

    switch (translate_c.optimize) {
        .Debug => {}, // Skip since it's the default.
        else => try argv_list.append(b.fmt("-O{s}", .{@tagName(translate_c.optimize)})),
    }

    for (translate_c.include_dirs.items) |include_dir| {
        try include_dir.appendZigProcessFlags(b, &argv_list, step);
    }

    for (translate_c.c_macros.items) |c_macro| {
        try argv_list.append("-D");
        try argv_list.append(c_macro);
    }

    var prev_search_strategy: std.Build.Module.SystemLib.SearchStrategy = .paths_first;
    var prev_preferred_link_mode: std.builtin.LinkMode = .dynamic;

    for (translate_c.system_libs.items) |*system_lib| {
        var seen_system_libs: std.StringHashMapUnmanaged([]const []const u8) = .empty;
        const system_lib_gop = try seen_system_libs.getOrPut(arena, system_lib.name);
        if (system_lib_gop.found_existing) {
            try argv_list.appendSlice(system_lib_gop.value_ptr.*);
            continue;
        } else {
            system_lib_gop.value_ptr.* = &.{};
        }

        if (system_lib.search_strategy != prev_search_strategy or
            system_lib.preferred_link_mode != prev_preferred_link_mode)
        {
            switch (system_lib.search_strategy) {
                .no_fallback => switch (system_lib.preferred_link_mode) {
                    .dynamic => try argv_list.append("-search_dylibs_only"),
                    .static => try argv_list.append("-search_static_only"),
                },
                .paths_first => switch (system_lib.preferred_link_mode) {
                    .dynamic => try argv_list.append("-search_paths_first"),
                    .static => try argv_list.append("-search_paths_first_static"),
                },
                .mode_first => switch (system_lib.preferred_link_mode) {
                    .dynamic => try argv_list.append("-search_dylibs_first"),
                    .static => try argv_list.append("-search_static_first"),
                },
            }
            prev_search_strategy = system_lib.search_strategy;
            prev_preferred_link_mode = system_lib.preferred_link_mode;
        }

        const prefix: []const u8 = prefix: {
            if (system_lib.needed) break :prefix "-needed-l";
            if (system_lib.weak) break :prefix "-weak-l";
            break :prefix "-l";
        };
        switch (system_lib.use_pkg_config) {
            .no => try argv_list.append(b.fmt("{s}{s}", .{ prefix, system_lib.name })),
            .yes, .force => {
                if (Step.Compile.runPkgConfig(&translate_c.step, system_lib.name)) |result| {
                    try argv_list.appendSlice(result.cflags);
                    try argv_list.appendSlice(result.libs);
                    try seen_system_libs.put(arena, system_lib.name, result.cflags);
                } else |err| switch (err) {
                    error.PkgConfigInvalidOutput,
                    error.PkgConfigCrashed,
                    error.PkgConfigFailed,
                    error.PkgConfigNotInstalled,
                    error.PackageNotFound,
                    => switch (system_lib.use_pkg_config) {
                        .yes => {
                            // pkg-config failed, so fall back to linking the library
                            // by name directly.
                            try argv_list.append(b.fmt("{s}{s}", .{
                                prefix,
                                system_lib.name,
                            }));
                        },
                        .force => {
                            std.debug.panic("pkg-config failed for library {s}", .{system_lib.name});
                        },
                        .no => unreachable,
                    },

                    else => |e| return e,
                }
            },
        }
    }

    const c_source_path = translate_c.source.getPath2(b, step);
    try argv_list.append(c_source_path);

    try argv_list.append("--listen=-");
    const output_dir = try step.evalZigProcess(argv_list.items, prog_node, false, options.web_server, options.gpa);

    const basename = std.fs.path.stem(std.fs.path.basename(c_source_path));
    translate_c.out_basename = b.fmt("{s}.zig", .{basename});
    translate_c.output_file.path = output_dir.?.joinString(b.allocator, translate_c.out_basename) catch @panic("OOM");
}

pub fn linkSystemLibrary(
    translate_c: *TranslateC,
    name: []const u8,
    options: std.Build.Module.LinkSystemLibraryOptions,
) void {
    const b = translate_c.step.owner;
    translate_c.system_libs.append(b.allocator, .{
        .name = b.dupe(name),
        .needed = options.needed,
        .weak = options.weak,
        .use_pkg_config = options.use_pkg_config,
        .preferred_link_mode = options.preferred_link_mode,
        .search_strategy = options.search_strategy,
    }) catch @panic("OOM");
}



---
File: /std/Build/Step/UpdateSourceFiles.zig
---

//! Writes data to paths relative to the package root, effectively mutating the
//! package's source files. Be careful with the latter functionality; it should
//! not be used during the normal build process, but as a utility run by a
//! developer with intention to update source files, which will then be
//! committed to version control.
const UpdateSourceFiles = @This();

const std = @import("std");
const Io = std.Io;
const Step = std.Build.Step;
const fs = std.fs;
const ArrayList = std.ArrayList;

step: Step,
output_source_files: std.ArrayList(OutputSourceFile),

pub const base_id: Step.Id = .update_source_files;

pub const OutputSourceFile = struct {
    contents: Contents,
    sub_path: []const u8,
};

pub const Contents = union(enum) {
    bytes: []const u8,
    copy: std.Build.LazyPath,
};

pub fn create(owner: *std.Build) *UpdateSourceFiles {
    const usf = owner.allocator.create(UpdateSourceFiles) catch @panic("OOM");
    usf.* = .{
        .step = Step.init(.{
            .id = base_id,
            .name = "UpdateSourceFiles",
            .owner = owner,
            .makeFn = make,
        }),
        .output_source_files = .empty,
    };
    return usf;
}

/// A path relative to the package root.
///
/// Be careful with this because it updates source files. This should not be
/// used as part of the normal build process, but as a utility occasionally
/// run by a developer with intent to modify source files and then commit
/// those changes to version control.
pub fn addCopyFileToSource(usf: *UpdateSourceFiles, source: std.Build.LazyPath, sub_path: []const u8) void {
    const b = usf.step.owner;
    usf.output_source_files.append(b.allocator, .{
        .contents = .{ .copy = source },
        .sub_path = sub_path,
    }) catch @panic("OOM");
    source.addStepDependencies(&usf.step);
}

/// A path relative to the package root.
///
/// Be careful with this because it updates source files. This should not be
/// used as part of the normal build process, but as a utility occasionally
/// run by a developer with intent to modify source files and then commit
/// those changes to version control.
pub fn addBytesToSource(usf: *UpdateSourceFiles, bytes: []const u8, sub_path: []const u8) void {
    const b = usf.step.owner;
    usf.output_source_files.append(b.allocator, .{
        .contents = .{ .bytes = bytes },
        .sub_path = sub_path,
    }) catch @panic("OOM");
}

fn make(step: *Step, options: Step.MakeOptions) !void {
    _ = options;
    const b = step.owner;
    const io = b.graph.io;
    const usf: *UpdateSourceFiles = @fieldParentPtr("step", step);

    var any_miss = false;
    for (usf.output_source_files.items) |output_source_file| {
        if (fs.path.dirname(output_source_file.sub_path)) |dirname| {
            b.build_root.handle.createDirPath(io, dirname) catch |err| {
                return step.fail("unable to make path '{f}{s}': {t}", .{ b.build_root, dirname, err });
            };
        }
        switch (output_source_file.contents) {
            .bytes => |bytes| {
                b.build_root.handle.writeFile(io, .{ .sub_path = output_source_file.sub_path, .data = bytes }) catch |err| {
                    return step.fail("unable to write file '{f}{s}': {t}", .{
                        b.build_root, output_source_file.sub_path, err,
                    });
                };
                any_miss = true;
            },
            .copy => |file_source| {
                if (!step.inputs.populated()) try step.addWatchInput(file_source);

                const source_path = file_source.getPath2(b, step);
                const prev_status = Io.Dir.updateFile(
                    .cwd(),
                    io,
                    source_path,
                    b.build_root.handle,
                    output_source_file.sub_path,
                    .{},
                ) catch |err| {
                    return step.fail("unable to update file from '{s}' to '{f}{s}': {t}", .{
                        source_path, b.build_root, output_source_file.sub_path, err,
                    });
                };
                any_miss = any_miss or prev_status == .stale;
            },
        }
    }

    step.result_cached = !any_miss;
}



---
File: /std/Build/Step/WriteFile.zig
---

//! WriteFile is used to create a directory in an appropriate location inside
//! the local cache which has a set of files that have either been generated
//! during the build, or are copied from the source package.
const WriteFile = @This();

const std = @import("std");
const Io = std.Io;
const Dir = std.Io.Dir;
const Step = std.Build.Step;
const ArrayList = std.ArrayList;
const assert = std.debug.assert;

step: Step,

/// The elements here are pointers because we need stable pointers for the GeneratedFile field.
files: std.ArrayList(File),
directories: std.ArrayList(Directory),
generated_directory: std.Build.GeneratedFile,
mode: Mode = .whole_cached,

pub const base_id: Step.Id = .write_file;

pub const Mode = union(enum) {
    /// Default mode. Integrates with the cache system. The directory should be
    /// read-only during the make phase. Any different inputs result in
    /// different "o" subdirectory.
    whole_cached,
    /// In this mode, the directory will be placed inside "tmp" rather than
    /// "o", and caching will be skipped. During the `make` phase, the step
    /// will always do all the file system operations, and on successful build
    /// completion, the dir will be deleted along with all other tmp
    /// directories. The directory is therefore eligible to be used for
    /// mutations by other steps.
    tmp,
    /// The operations will not be performed against a freshly created
    /// directory, but instead act against a temporary directory.
    mutate: std.Build.LazyPath,
};

pub const File = struct {
    sub_path: []const u8,
    contents: Contents,
};

pub const Directory = struct {
    source: std.Build.LazyPath,
    sub_path: []const u8,
    options: Options,

    pub const Options = struct {
        /// File paths that end in any of these suffixes will be excluded from copying.
        exclude_extensions: []const []const u8 = &.{},
        /// Only file paths that end in any of these suffixes will be included in copying.
        /// `null` means that all suffixes will be included.
        /// `exclude_extensions` takes precedence over `include_extensions`.
        include_extensions: ?[]const []const u8 = null,

        pub fn dupe(opts: Options, b: *std.Build) Options {
            return .{
                .exclude_extensions = b.dupeStrings(opts.exclude_extensions),
                .include_extensions = if (opts.include_extensions) |incs| b.dupeStrings(incs) else null,
            };
        }

        pub fn pathIncluded(opts: Options, path: []const u8) bool {
            for (opts.exclude_extensions) |ext| {
                if (std.mem.endsWith(u8, path, ext))
                    return false;
            }
            if (opts.include_extensions) |incs| {
                for (incs) |inc| {
                    if (std.mem.endsWith(u8, path, inc))
                        return true;
                } else {
                    return false;
                }
            }
            return true;
        }
    };
};

pub const Contents = union(enum) {
    bytes: []const u8,
    copy: std.Build.LazyPath,
};

pub fn create(owner: *std.Build) *WriteFile {
    const write_file = owner.allocator.create(WriteFile) catch @panic("OOM");
    write_file.* = .{
        .step = Step.init(.{
            .id = base_id,
            .name = "WriteFile",
            .owner = owner,
            .makeFn = make,
        }),
        .files = .empty,
        .directories = .empty,
        .generated_directory = .{ .step = &write_file.step },
    };
    return write_file;
}

pub fn add(write_file: *WriteFile, sub_path: []const u8, bytes: []const u8) std.Build.LazyPath {
    const b = write_file.step.owner;
    const gpa = b.allocator;
    const file = File{
        .sub_path = b.dupePath(sub_path),
        .contents = .{ .bytes = b.dupe(bytes) },
    };
    write_file.files.append(gpa, file) catch @panic("OOM");
    write_file.maybeUpdateName();
    return .{
        .generated = .{
            .file = &write_file.generated_directory,
            .sub_path = file.sub_path,
        },
    };
}

/// Place the file into the generated directory within the local cache,
/// along with all the rest of the files added to this step. The parameter
/// here is the destination path relative to the local cache directory
/// associated with this WriteFile. It may be a basename, or it may
/// include sub-directories, in which case this step will ensure the
/// required sub-path exists.
/// This is the option expected to be used most commonly with `addCopyFile`.
pub fn addCopyFile(write_file: *WriteFile, source: std.Build.LazyPath, sub_path: []const u8) std.Build.LazyPath {
    const b = write_file.step.owner;
    const gpa = b.allocator;
    const file = File{
        .sub_path = b.dupePath(sub_path),
        .contents = .{ .copy = source },
    };
    write_file.files.append(gpa, file) catch @panic("OOM");

    write_file.maybeUpdateName();
    source.addStepDependencies(&write_file.step);
    return .{
        .generated = .{
            .file = &write_file.generated_directory,
            .sub_path = file.sub_path,
        },
    };
}

/// Copy files matching the specified exclude/include patterns to the specified subdirectory
/// relative to this step's generated directory.
/// The returned value is a lazy path to the generated subdirectory.
pub fn addCopyDirectory(
    write_file: *WriteFile,
    source: std.Build.LazyPath,
    sub_path: []const u8,
    options: Directory.Options,
) std.Build.LazyPath {
    const b = write_file.step.owner;
    const gpa = b.allocator;
    const dir = Directory{
        .source = source.dupe(b),
        .sub_path = b.dupePath(sub_path),
        .options = options.dupe(b),
    };
    write_file.directories.append(gpa, dir) catch @panic("OOM");

  
```
