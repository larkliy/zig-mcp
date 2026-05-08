```
LT,
    ) callconv(.winapi) void {
        _ = pQueryContext;
        const lookup_dns: *LookupDnsWindows = @fieldParentPtr("results", pQueryResults);
        lookup_dns.completedFallible() catch |err| switch (err) {
            error.Closed => unreachable, // `resolved` must not be closed until `netLookup` returns
            error.Canceled => unreachable, // called from an uncancelable thread
        };
        @atomicStore(bool, &lookup_dns.done, true, .release);
        _ = windows.ntdll.NtAlertThread(lookup_dns.thread);
    }
    fn completedFallible(lookup_dns: *LookupDnsWindows) (Io.QueueClosedError || Io.Cancelable)!void {
        assert(!lookup_dns.done);
        const t = lookup_dns.threaded;
        defer t.dl.DnsFree.raw.?(lookup_dns.results.pQueryRecords, .RecordList);
        if (lookup_dns.results.QueryStatus != .SUCCESS) return;
        const t_io = t.io();
        var record_it = lookup_dns.results.pQueryRecords;
        while (record_it) |record| : (record_it = record.pNext) switch (record.wType) {
            else => {},
            .A => try lookup_dns.resolved.putOne(t_io, .{
                .address = .{ .ip4 = .{ .bytes = record.Data.A, .port = lookup_dns.options.port } },
            }),
            .AAAA => {
                const ip6: net.Ip6Address = .{
                    .bytes = record.Data.AAAA,
                    .port = lookup_dns.options.port,
                };
                try lookup_dns.resolved.putOne(t_io, .{
                    .address = if (lookup_dns.options.family) |_| .{ .ip6 = ip6 } else .fromIp6(ip6),
                });
            },
        };
        if (lookup_dns.results.pQueryRecords) |record| {
            if (lookup_dns.options.canonical_name_buffer) |buf| {
                const name_wtf16 = std.mem.span(
                    @as([*:0]const windows.WCHAR, @ptrCast(@alignCast(record.pName))),
                );
                const len = std.unicode.wtf16LeToWtf8(buf, name_wtf16);
                try lookup_dns.resolved.putOne(t_io, .{
                    .canonical_name = .{ .bytes = buf[0..len] },
                });
            }
        }
    }
};

fn copyCanon(canonical_name_buffer: ?*[HostName.max_len]u8, name: []const u8) ?HostName {
    const buf = canonical_name_buffer orelse return null;
    const dest = buf[0..name.len];
    @memcpy(dest, name);
    return .{ .bytes = dest };
}

/// Darwin XNU 7195.50.7.100.1 introduced __ulock_wait2 and migrated code paths (notably pthread_cond_t) towards it:
/// https://github.com/apple/darwin-xnu/commit/d4061fb0260b3ed486147341b72468f836ed6c8f#diff-08f993cc40af475663274687b7c326cc6c3031e0db3ac8de7b24624610616be6
///
/// This XNU version appears to correspond to 11.0.1:
/// https://kernelshaman.blogspot.com/2021/01/building-xnu-for-macos-big-sur-1101.html
///
/// ulock_wait() uses 32-bit micro-second timeouts where 0 = INFINITE or no-timeout
/// ulock_wait2() uses 64-bit nano-second timeouts (with the same convention)
const darwin_supports_ulock_wait2 = builtin.os.version_range.semver.min.major >= 11;

fn doNothingSignalHandler(_: posix.SIG) callconv(.c) void {}

const WindowsEnvironStrings = struct {
    PATH: ?[:0]const u16 = null,
    PATHEXT: ?[:0]const u16 = null,

    fn scan() WindowsEnvironStrings {
        const peb = windows.peb();
        assert(windows.ntdll.RtlEnterCriticalSection(peb.FastPebLock) == .SUCCESS);
        defer assert(windows.ntdll.RtlLeaveCriticalSection(peb.FastPebLock) == .SUCCESS);
        const ptr = peb.ProcessParameters.Environment;

        var result: WindowsEnvironStrings = .{};
        var i: usize = 0;
        while (ptr[i] != 0) {
            const key_start = i;

            // There are some special environment variables that start with =,
            // so we need a special case to not treat = as a key/value separator
            // if it's the first character.
            // https://devblogs.microsoft.com/oldnewthing/20100506-00/?p=14133
            if (ptr[key_start] == '=') i += 1;

            while (ptr[i] != 0 and ptr[i] != '=') : (i += 1) {}
            const key_w = ptr[key_start..i];

            if (ptr[i] == '=') i += 1;

            const value_start = i;
            while (ptr[i] != 0) : (i += 1) {}
            const value_w = ptr[value_start..i :0];

            i += 1; // skip over null byte

            inline for (@typeInfo(WindowsEnvironStrings).@"struct".fields) |field| {
                const field_name_w = comptime std.unicode.wtf8ToWtf16LeStringLiteral(field.name);
                if (windows.eqlIgnoreCaseWtf16(key_w, field_name_w)) @field(result, field.name) = value_w;
            }
        }

        return result;
    }
};

fn scanEnviron(t: *Threaded) void {
    mutexLock(&t.mutex);
    defer mutexUnlock(&t.mutex);
    if (t.environ_initialized) return;
    t.environ.scan(t.allocator);
    t.environ_initialized = true;
}

fn processReplace(userdata: ?*anyopaque, options: process.ReplaceOptions) process.ReplaceError {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    if (!process.can_replace) return error.OperationUnsupported;

    t.scanEnviron(); // for PATH
    const PATH = t.environ.string.PATH orelse default_PATH;

    var arena_allocator = std.heap.ArenaAllocator.init(t.allocator);
    defer arena_allocator.deinit();
    const arena = arena_allocator.allocator();

    const argv_buf = try arena.allocSentinel(?[*:0]const u8, options.argv.len, null);
    for (options.argv, 0..) |arg, i| argv_buf[i] = (try arena.dupeZ(u8, arg)).ptr;

    const env_block = env_block: {
        const prog_fd: i32 = -1;
        if (options.environ_map) |environ_map| break :env_block try environ_map.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
        break :env_block try t.environ.process_environ.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
    };

    return posixExecv(options.expand_arg0, argv_buf.ptr[0].?, argv_buf.ptr, env_block, PATH);
}

fn processReplacePath(userdata: ?*anyopaque, dir: Dir, options: process.ReplaceOptions) process.ReplaceError {
    if (!process.can_replace) return error.OperationUnsupported;
    _ = userdata;
    _ = dir;
    _ = options;
    @panic("TODO processReplacePath");
}

fn processSpawnPath(userdata: ?*anyopaque, dir: Dir, options: process.SpawnOptions) process.SpawnError!process.Child {
    if (!process.can_spawn) return error.OperationUnsupported;
    _ = userdata;
    _ = dir;
    _ = options;
    @panic("TODO processSpawnPath");
}

const processSpawn = switch (native_os) {
    .wasi, .emscripten, .ios, .tvos, .visionos, .watchos => processSpawnUnsupported,
    .windows => processSpawnWindows,
    else => processSpawnPosix,
};

fn processSpawnUnsupported(userdata: ?*anyopaque, options: process.SpawnOptions) process.SpawnError!process.Child {
    _ = userdata;
    _ = options;
    return error.OperationUnsupported;
}

const Spawned = struct {
    pid: posix.pid_t,
    err_fd: posix.fd_t,
    stdin: ?File,
    stdout: ?File,
    stderr: ?File,
};

fn spawnPosix(t: *Threaded, options: process.SpawnOptions) process.SpawnError!Spawned {
    // The child process does need to access (one end of) these pipes. However,
    // we must initially set CLOEXEC to avoid a race condition. If another thread
    // is racing to spawn a different child process, we don't want it to inherit
    // these FDs in any scenario; that would mean that, for instance, calls to
    // `poll` from the parent would not report the child's stdout as closing when
    // expected, since the other child may retain a reference to the write end of
    // the pipe. So, we create the pipes with CLOEXEC initially. After fork, we
    // need to do something in the new child to make sure we preserve the reference
    // we want. We could use `fcntl` to remove CLOEXEC from the FD, but as it
    // turns out, we `dup2` everything anyway, so there's no need!
    const pipe_flags: posix.O = .{ .CLOEXEC = true };

    const stdin_pipe = if (options.stdin == .pipe) try pipe2(pipe_flags) else undefined;
    errdefer if (options.stdin == .pipe) {
        destroyPipe(stdin_pipe);
    };

    const stdout_pipe = if (options.stdout == .pipe) try pipe2(pipe_flags) else undefined;
    errdefer if (options.stdout == .pipe) {
        destroyPipe(stdout_pipe);
    };

    const stderr_pipe = if (options.stderr == .pipe) try pipe2(pipe_flags) else undefined;
    errdefer if (options.stderr == .pipe) {
        destroyPipe(stderr_pipe);
    };

    const any_ignore = (options.stdin == .ignore or options.stdout == .ignore or options.stderr == .ignore);
    const dev_null_fd = if (any_ignore) try getDevNullFd(t) else undefined;

    const prog_pipe: [2]posix.fd_t = if (options.progress_node.index != .none) pipe: {
        // We use CLOEXEC for the same reason as in `pipe_flags`.
        const pipe = try pipe2(.{ .NONBLOCK = true, .CLOEXEC = true });
        switch (native_os) {
            .linux => _ = posix.system.fcntl(pipe[0], posix.F.SETPIPE_SZ, @as(u32, std.Progress.max_packet_len * 2)),
            else => {},
        }
        break :pipe pipe;
    } else .{ -1, -1 };
    errdefer destroyPipe(prog_pipe);

    var arena_allocator = std.heap.ArenaAllocator.init(t.allocator);
    defer arena_allocator.deinit();
    const arena = arena_allocator.allocator();

    // The POSIX standard does not allow malloc() between fork() and execve(),
    // and this allocator may be a libc allocator.
    // I have personally observed the child process deadlocking when it tries
    // to call malloc() due to a heap allocation between fork() and execve(),
    // in musl v1.1.24.
    // Additionally, we want to reduce the number of possible ways things
    // can fail between fork() and execve().
    // Therefore, we do all the allocation for the execve() before the fork().
    // This means we must do the null-termination of argv and env vars here.
    const argv_buf = try arena.allocSentinel(?[*:0]const u8, options.argv.len, null);
    for (options.argv, 0..) |arg, i| argv_buf[i] = (try arena.dupeZ(u8, arg)).ptr;

    const prog_fileno = 3;
    comptime assert(@max(posix.STDIN_FILENO, posix.STDOUT_FILENO, posix.STDERR_FILENO) + 1 == prog_fileno);

    const env_block = env_block: {
        const prog_fd: i32 = if (prog_pipe[1] == -1) -1 else prog_fileno;
        if (options.environ_map) |environ_map| break :env_block try environ_map.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
        break :env_block try t.environ.process_environ.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
    };

    // This pipe communicates to the parent errors in the child between `fork` and `execvpe`.
    // It is closed by the child (via CLOEXEC) without writing if `execvpe` succeeds.
    const err_pipe = try pipe2(.{ .CLOEXEC = true });
    errdefer destroyPipe(err_pipe);

    t.scanEnviron(); // for PATH
    const PATH = t.environ.string.PATH orelse default_PATH;

    const pid_result: posix.pid_t = fork: {
        const rc = posix.system.fork();
        switch (posix.errno(rc)) {
            .SUCCESS => break :fork @intCast(rc),
            .AGAIN => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .NOSYS => return error.OperationUnsupported,
            else => |err| return posix.unexpectedErrno(err),
        }
    };

    if (pid_result == 0) {
        defer comptime unreachable; // We are the child.
        if (Thread.current) |current_thread| current_thread.cancel_protection = .blocked;
        const ep1 = err_pipe[1];

        setUpChildIo(options.stdin, stdin_pipe[0], posix.STDIN_FILENO, dev_null_fd) catch |err| forkBail(ep1, err);
        setUpChildIo(options.stdout, stdout_pipe[1], posix.STDOUT_FILENO, dev_null_fd) catch |err| forkBail(ep1, err);
        setUpChildIo(options.stderr, stderr_pipe[1], posix.STDERR_FILENO, dev_null_fd) catch |err| forkBail(ep1, err);

        switch (options.cwd) {
            .inherit => {},
            .dir => |cwd| {
                fchdir(cwd.handle) catch |err| forkBail(ep1, err);
            },
            .path => |cwd| {
                chdir(cwd) catch |err| forkBail(ep1, err);
            },
        }

        // Must happen after fchdir above, the cwd file descriptor might be
        // equal to prog_fileno and be clobbered by this dup2 call.
        if (prog_pipe[1] != -1) dup2(prog_pipe[1], prog_fileno) catch |err| forkBail(ep1, err);

        if (options.gid) |gid| {
            switch (posix.errno(posix.system.setregid(gid, gid))) {
                .SUCCESS => {},
                .AGAIN => forkBail(ep1, error.ResourceLimitReached),
                .INVAL => forkBail(ep1, error.InvalidUserId),
                .PERM => forkBail(ep1, error.PermissionDenied),
                else => forkBail(ep1, error.Unexpected),
            }
        }

        if (options.uid) |uid| {
            switch (posix.errno(posix.system.setreuid(uid, uid))) {
                .SUCCESS => {},
                .AGAIN => forkBail(ep1, error.ResourceLimitReached),
                .INVAL => forkBail(ep1, error.InvalidUserId),
                .PERM => forkBail(ep1, error.PermissionDenied),
                else => forkBail(ep1, error.Unexpected),
            }
        }

        if (options.pgid) |pid| {
            switch (posix.errno(posix.system.setpgid(0, pid))) {
                .SUCCESS => {},
                .ACCES => forkBail(ep1, error.ProcessAlreadyExec),
                .INVAL => forkBail(ep1, error.InvalidProcessGroupId),
                .PERM => forkBail(ep1, error.PermissionDenied),
                else => forkBail(ep1, error.Unexpected),
            }
        }

        if (options.start_suspended) {
            switch (posix.errno(posix.system.kill(0, .STOP))) {
                .SUCCESS => {},
                .PERM => forkBail(ep1, error.PermissionDenied),
                else => forkBail(ep1, error.Unexpected),
            }
        }

        const err = posixExecv(options.expand_arg0, argv_buf.ptr[0].?, argv_buf.ptr, env_block, PATH);
        forkBail(ep1, err);
    }

    const pid: posix.pid_t = @intCast(pid_result); // We are the parent.
    errdefer comptime unreachable; // The child is forked; we must not error from now on

    closeFd(err_pipe[1]); // make sure only the child holds the write end open

    if (options.stdin == .pipe) closeFd(stdin_pipe[0]);
    if (options.stdout == .pipe) closeFd(stdout_pipe[1]);
    if (options.stderr == .pipe) closeFd(stderr_pipe[1]);

    if (prog_pipe[1] != -1) closeFd(prog_pipe[1]);
    options.progress_node.setIpcFile(t, .{ .handle = prog_pipe[0], .flags = .{ .nonblocking = true } });

    return .{
        .pid = pid,
        .err_fd = err_pipe[0],
        .stdin = switch (options.stdin) {
            .pipe => .{ .handle = stdin_pipe[1], .flags = .{ .nonblocking = false } },
            else => null,
        },
        .stdout = switch (options.stdout) {
            .pipe => .{ .handle = stdout_pipe[0], .flags = .{ .nonblocking = false } },
            else => null,
        },
        .stderr = switch (options.stderr) {
            .pipe => .{ .handle = stderr_pipe[0], .flags = .{ .nonblocking = false } },
            else => null,
        },
    };
}

fn getDevNullFd(t: *Threaded) !posix.fd_t {
    {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);
        if (t.null_file.fd != -1) return t.null_file.fd;
    }
    const mode: u32 = 0;
    const syscall: Syscall = try .start();
    while (true) {
        const rc = open_sym("/dev/null", .{ .ACCMODE = .RDWR }, mode);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                const fresh_fd: posix.fd_t = @intCast(rc);
                mutexLock(&t.mutex); // Another thread might have won the race.
                defer mutexUnlock(&t.mutex);
                if (t.null_file.fd != -1) {
                    closeFd(fresh_fd);
                    return t.null_file.fd;
                } else {
                    t.null_file.fd = fresh_fd;
                    return fresh_fd;
                }
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ACCES => return syscall.fail(error.AccessDenied),
            .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
            .NFILE => return syscall.fail(error.SystemFdQuotaExceeded),
            .NODEV => return syscall.fail(error.NoDevice),
            .NOENT => return syscall.fail(error.FileNotFound),
            .NOMEM => return syscall.fail(error.SystemResources),
            .PERM => return syscall.fail(error.PermissionDenied),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn processSpawnPosix(userdata: ?*anyopaque, options: process.SpawnOptions) process.SpawnError!process.Child {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const spawned = try spawnPosix(t, options);
    defer closeFd(spawned.err_fd);

    // Wait for the child to report any errors in or before `execvpe`.
    if (readIntFd(spawned.err_fd)) |child_err_int| {
        const child_err: process.SpawnError = @errorCast(@errorFromInt(child_err_int));
        return child_err;
    } else |read_err| switch (read_err) {
        error.EndOfStream => {
            // Write end closed by CLOEXEC at the time of the `execvpe` call,
            // indicating success.
        },
        else => {
            // Problem reading the error from the error reporting pipe. We
            // don't know if the child is alive or dead. Better to assume it is
            // alive so the resource does not risk being leaked.
        },
    }

    return .{
        .id = spawned.pid,
        .thread_handle = {},
        .stdin = spawned.stdin,
        .stdout = spawned.stdout,
        .stderr = spawned.stderr,
        .request_resource_usage_statistics = options.request_resource_usage_statistics,
    };
}

fn childWait(userdata: ?*anyopaque, child: *process.Child) process.Child.WaitError!process.Child.Term {
    if (native_os == .wasi) unreachable;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    switch (native_os) {
        .windows => return childWaitWindows(child),
        else => return childWaitPosix(child),
    }
}

fn childKill(userdata: ?*anyopaque, child: *process.Child) void {
    if (native_os == .wasi) unreachable;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (is_windows) {
        childKillWindows(t, child, 1) catch childCleanupWindows(child);
    } else {
        childKillPosix(child) catch {};
        childCleanupPosix(child);
    }
}

fn childKillWindows(t: *Threaded, child: *process.Child, exit_code: windows.UINT) !void {
    _ = t; // TODO cancelation
    const handle = child.id.?;
    _ = windows.ntdll.RtlReportSilentProcessExit(handle, @enumFromInt(exit_code));
    switch (windows.ntdll.NtTerminateProcess(handle, @enumFromInt(exit_code))) {
        .SUCCESS, .PROCESS_IS_TERMINATING => {
            const infinite_timeout: windows.LARGE_INTEGER = std.math.minInt(windows.LARGE_INTEGER);
            _ = windows.ntdll.NtWaitForSingleObject(handle, .FALSE, &infinite_timeout);
            childCleanupWindows(child);
        },
        .ACCESS_DENIED => {
            // Usually when TerminateProcess triggers a ACCESS_DENIED error, it
            // indicates that the process has already exited, but there may be
            // some rare edge cases where our process handle no longer has the
            // PROCESS_TERMINATE access right, so let's do another check to make
            // sure the process is really no longer running:
            const minimal_timeout: windows.LARGE_INTEGER = -1;
            return switch (windows.ntdll.NtWaitForSingleObject(handle, .FALSE, &minimal_timeout)) {
                windows.NTSTATUS.WAIT_0 => error.AlreadyTerminated,
                else => error.AccessDenied,
            };
        },
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn childWaitWindows(child: *process.Child) process.Child.WaitError!process.Child.Term {
    const handle = child.id.?;

    const alertable_syscall: AlertableSyscall = try .start();
    const infinite_timeout: windows.LARGE_INTEGER = std.math.minInt(windows.LARGE_INTEGER);
    while (true) switch (windows.ntdll.NtWaitForSingleObject(handle, .TRUE, &infinite_timeout)) {
        windows.NTSTATUS.WAIT_0 => break alertable_syscall.finish(),
        .USER_APC, .ALERTED, .TIMEOUT => {
            try alertable_syscall.checkCancel();
            continue;
        },
        else => |status| return alertable_syscall.unexpectedNtstatus(status),
    };

    var info: windows.PROCESS.BASIC_INFORMATION = undefined;
    const term: process.Child.Term = switch (windows.ntdll.NtQueryInformationProcess(
        handle,
        .BasicInformation,
        &info,
        @sizeOf(windows.PROCESS.BASIC_INFORMATION),
        null,
    )) {
        .SUCCESS => .{ .exited = @as(u8, @truncate(@intFromEnum(info.ExitStatus))) },
        else => .{ .unknown = 0 },
    };

    childCleanupWindows(child);
    return term;
}

fn childCleanupWindows(child: *process.Child) void {
    const handle = child.id orelse return;

    if (child.request_resource_usage_statistics) {
        var vmc: windows.PROCESS.VM_COUNTERS = undefined;
        switch (windows.ntdll.NtQueryInformationProcess(
            handle,
            .VmCounters,
            &vmc,
            @sizeOf(windows.PROCESS.VM_COUNTERS),
            null,
        )) {
            .SUCCESS => child.resource_usage_statistics.rusage = vmc,
            else => child.resource_usage_statistics.rusage = null,
        }
    }

    windows.CloseHandle(handle);
    child.id = null;

    windows.CloseHandle(child.thread_handle);
    child.thread_handle = undefined;

    if (child.stdin) |stdin| {
        windows.CloseHandle(stdin.handle);
        child.stdin = null;
    }
    if (child.stdout) |stdout| {
        windows.CloseHandle(stdout.handle);
        child.stdout = null;
    }
    if (child.stderr) |stderr| {
        windows.CloseHandle(stderr.handle);
        child.stderr = null;
    }
}

fn childWaitPosix(child: *process.Child) process.Child.WaitError!process.Child.Term {
    defer childCleanupPosix(child);

    const pid = child.id.?;

    var ru: posix.rusage = undefined;
    const ru_ptr = if (child.request_resource_usage_statistics) &ru else null;

    if (have_wait4) {
        var status: if (builtin.link_libc) c_int else u32 = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (posix.errno(posix.system.wait4(pid, &status, 0, ru_ptr))) {
            .SUCCESS => {
                syscall.finish();
                if (ru_ptr) |p| child.resource_usage_statistics.rusage = p.*;
                return statusToTerm(@bitCast(status));
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .CHILD => |err| return syscall.errnoBug(err), // Double-free.
            else => |err| return syscall.unexpectedErrno(err),
        };
    }

    if (have_waitid) {
        const linux = std.os.linux; // Bypass libc which has the wrong signature.
        var info: linux.siginfo_t = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (linux.errno(linux.waitid(.PID, pid, &info, linux.W.EXITED, ru_ptr))) {
            .SUCCESS => {
                syscall.finish();
                if (ru_ptr) |p| child.resource_usage_statistics.rusage = p.*;
                const status: u32 = @bitCast(info.fields.common.second.sigchld.status);
                const code: linux.CLD = @enumFromInt(info.code);
                return switch (code) {
                    .EXITED => .{ .exited = @truncate(status) },
                    .KILLED, .DUMPED => .{ .signal = @enumFromInt(status) },
                    .TRAPPED, .STOPPED => .{ .stopped = @enumFromInt(status) },
                    _, .CONTINUED => .{ .unknown = status },
                };
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .CHILD => |err| return syscall.errnoBug(err), // Double-free.
            else => |err| return syscall.unexpectedErrno(err),
        };
    }

    var status: if (builtin.link_libc) c_int else u32 = undefined;
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.waitpid(pid, &status, 0))) {
        .SUCCESS => {
            syscall.finish();
            return statusToTerm(@bitCast(status));
        },
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .CHILD => |err| return syscall.errnoBug(err), // Double-free.
        else => |err| return syscall.unexpectedErrno(err),
    };
}

pub fn statusToTerm(status: u32) process.Child.Term {
    return if (posix.W.IFEXITED(status))
        .{ .exited = posix.W.EXITSTATUS(status) }
    else if (posix.W.IFSIGNALED(status))
        .{ .signal = posix.W.TERMSIG(status) }
    else if (posix.W.IFSTOPPED(status))
        .{ .stopped = posix.W.STOPSIG(status) }
    else
        .{ .unknown = status };
}

fn childKillPosix(child: *process.Child) !void {
    // Entire function body is intentionally uncancelable.

    const pid = child.id.?;

    while (true) switch (posix.errno(posix.system.kill(pid, .TERM))) {
        .SUCCESS => break,
        .INTR => continue,
        .PERM => return error.PermissionDenied,
        .INVAL => |err| return errnoBug(err),
        .SRCH => |err| return errnoBug(err),
        else => |err| return posix.unexpectedErrno(err),
    };

    if (have_wait4) {
        var status: if (builtin.link_libc) c_int else u32 = undefined;
        while (true) switch (posix.errno(posix.system.wait4(pid, &status, 0, null))) {
            .SUCCESS => return,
            .INTR => continue,
            .CHILD => |err| return errnoBug(err), // Double-free.
            else => |err| return posix.unexpectedErrno(err),
        };
    }

    if (have_waitid) {
        const linux = std.os.linux; // Bypass libc which has the wrong signature.
        var info: linux.siginfo_t = undefined;
        while (true) switch (linux.errno(linux.waitid(.PID, pid, &info, linux.W.EXITED, null))) {
            .SUCCESS => return,
            .INTR => continue,
            .CHILD => |err| return errnoBug(err), // Double-free.
            else => |err| return posix.unexpectedErrno(err),
        };
    }

    var status: if (builtin.link_libc) c_int else u32 = undefined;
    while (true) switch (posix.errno(posix.system.waitpid(pid, &status, 0))) {
        .SUCCESS => return,
        .INTR => continue,
        .CHILD => |err| return errnoBug(err), // Double-free.
        else => |err| return posix.unexpectedErrno(err),
    };
}

fn childCleanupPosix(child: *process.Child) void {
    if (child.stdin) |stdin| {
        closeFd(stdin.handle);
        child.stdin = null;
    }
    if (child.stdout) |stdout| {
        closeFd(stdout.handle);
        child.stdout = null;
    }
    if (child.stderr) |stderr| {
        closeFd(stderr.handle);
        child.stderr = null;
    }
    child.id = null;
}

/// Errors that can occur between fork() and execv()
const ForkBailError = process.SpawnError || process.ReplaceError;

/// Child of fork calls this to report an error to the fork parent. Then the
/// child exits.
fn forkBail(fd: posix.fd_t, err: ForkBailError) noreturn {
    writeIntFd(fd, @as(ErrInt, @intFromError(err))) catch {};
    // If we're linking libc, some naughty applications may have registered atexit handlers
    // which we really do not want to run in the fork child. I caught LLVM doing this and
    // it caused a deadlock instead of doing an exit syscall. In the words of Avril Lavigne,
    // "Why'd you have to go and make things so complicated?"
    if (builtin.link_libc) {
        // The `_exit` function does nothing but make the exit syscall, unlike `exit`.
        std.c._exit(1);
    } else if (native_os == .linux and !builtin.single_threaded) {
        std.os.linux.exit_group(1);
    } else {
        posix.system.exit(1);
    }
}

fn writeIntFd(fd: posix.fd_t, value: ErrInt) !void {
    var buffer: [8]u8 = undefined;
    std.mem.writeInt(u64, &buffer, value, .little);
    // Skip the cancel mechanism.
    var i: usize = 0;
    while (true) {
        const rc = posix.system.write(fd, buffer[i..].ptr, buffer.len - i);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                const n: usize = @intCast(rc);
                i += n;
                if (buffer.len - i == 0) return;
            },
            .INTR => continue,
            else => return error.SystemResources,
        }
    }
}

fn readIntFd(fd: posix.fd_t) !ErrInt {
    var buffer: [8]u8 = undefined;
    var i: usize = 0;
    while (true) {
        const rc = posix.system.read(fd, buffer[i..].ptr, buffer.len - i);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                const n: usize = @intCast(rc);
                if (n == 0) break;
                i += n;
                continue;
            },
            .INTR => continue,
            else => |err| return posix.unexpectedErrno(err),
        }
    }
    if (buffer.len - i != 0) return error.EndOfStream;
    return @intCast(std.mem.readInt(u64, &buffer, .little));
}

const ErrInt = std.meta.Int(.unsigned, @sizeOf(anyerror) * 8);

fn destroyPipe(pipe: [2]posix.fd_t) void {
    if (pipe[0] != -1) closeFd(pipe[0]);
    if (pipe[0] != pipe[1]) closeFd(pipe[1]);
}

fn setUpChildIo(stdio: process.SpawnOptions.StdIo, pipe_fd: i32, std_fileno: i32, dev_null_fd: i32) !void {
    switch (stdio) {
        .pipe => try dup2(pipe_fd, std_fileno),
        .close => closeFd(std_fileno),
        .inherit => {},
        .ignore => try dup2(dev_null_fd, std_fileno),
        .file => |file| try dup2(file.handle, std_fileno),
    }
}

fn processSpawnWindows(userdata: ?*anyopaque, options: process.SpawnOptions) process.SpawnError!process.Child {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    const any_ignore =
        options.stdin == .ignore or
        options.stdout == .ignore or
        options.stderr == .ignore;
    const nul_handle = if (any_ignore) try getNulDevice(t) else undefined;

    const any_inherit =
        options.stdin == .inherit or
        options.stdout == .inherit or
        options.stderr == .inherit;
    const peb = if (any_inherit) windows.peb() else undefined;

    const stdin_pipe = if (options.stdin == .pipe) try t.windowsCreatePipe(.{
        .server = .{ .attributes = .{ .INHERIT = false }, .mode = .{ .IO = .SYNCHRONOUS_NONALERT } },
        .client = .{ .attributes = .{ .INHERIT = true }, .mode = .{ .IO = .SYNCHRONOUS_NONALERT } },
        .outbound = true,
    }) else undefined;
    errdefer if (options.stdin == .pipe) for (stdin_pipe) |handle| windows.CloseHandle(handle);

    const stdout_pipe = if (options.stdout == .pipe) try t.windowsCreatePipe(.{
        .server = .{ .attributes = .{ .INHERIT = false }, .mode = .{ .IO = .ASYNCHRONOUS } },
        .client = .{ .attributes = .{ .INHERIT = true }, .mode = .{ .IO = .SYNCHRONOUS_NONALERT } },
        .inbound = true,
    }) else undefined;
    errdefer if (options.stdout == .pipe) for (stdout_pipe) |handle| windows.CloseHandle(handle);

    const stderr_pipe = if (options.stderr == .pipe) try t.windowsCreatePipe(.{
        .server = .{ .attributes = .{ .INHERIT = false }, .mode = .{ .IO = .ASYNCHRONOUS } },
        .client = .{ .attributes = .{ .INHERIT = true }, .mode = .{ .IO = .SYNCHRONOUS_NONALERT } },
        .inbound = true,
    }) else undefined;
    errdefer if (options.stderr == .pipe) for (stderr_pipe) |handle| windows.CloseHandle(handle);

    const prog_pipe = if (options.progress_node.index != .none) try t.windowsCreatePipe(.{
        .server = .{ .attributes = .{ .INHERIT = false }, .mode = .{ .IO = .ASYNCHRONOUS } },
        .client = .{ .attributes = .{ .INHERIT = true }, .mode = .{ .IO = .ASYNCHRONOUS } },
        .inbound = true,
        .quota = std.Progress.max_packet_len * 2,
    }) else undefined;
    errdefer if (options.progress_node.index != .none) for (prog_pipe) |handle| windows.CloseHandle(handle);

    var siStartInfo: windows.STARTUPINFOW = .{
        .cb = @sizeOf(windows.STARTUPINFOW),
        .dwFlags = windows.STARTF_USESTDHANDLES,
        .hStdInput = switch (options.stdin) {
            .inherit => peb.ProcessParameters.hStdInput,
            .file => |file| try OpenFile(&.{}, .{
                .access_mask = .{
                    .STANDARD = .{ .SYNCHRONIZE = true },
                    .GENERIC = .{ .READ = true },
                },
                .dir = file.handle,
                .sa = &.{
                    .nLength = @sizeOf(windows.SECURITY_ATTRIBUTES),
                    .lpSecurityDescriptor = null,
                    .bInheritHandle = .TRUE,
                },
                .creation = .OPEN,
            }),
            .ignore => nul_handle,
            .pipe => stdin_pipe[1],
            .close => null,
        },
        .hStdOutput = switch (options.stdout) {
            .inherit => peb.ProcessParameters.hStdOutput,
            .file => |file| try OpenFile(&.{}, .{
                .access_mask = .{
                    .STANDARD = .{ .SYNCHRONIZE = true },
                    .GENERIC = .{ .WRITE = true },
                },
                .dir = file.handle,
                .sa = &.{
                    .nLength = @sizeOf(windows.SECURITY_ATTRIBUTES),
                    .lpSecurityDescriptor = null,
                    .bInheritHandle = .TRUE,
                },
                .creation = .OPEN,
            }),
            .ignore => nul_handle,
            .pipe => stdout_pipe[1],
            .close => null,
        },
        .hStdError = switch (options.stderr) {
            .inherit => peb.ProcessParameters.hStdError,
            .file => |file| try OpenFile(&.{}, .{
                .access_mask = .{
                    .STANDARD = .{ .SYNCHRONIZE = true },
                    .GENERIC = .{ .WRITE = true },
                },
                .dir = file.handle,
                .sa = &.{
                    .nLength = @sizeOf(windows.SECURITY_ATTRIBUTES),
                    .lpSecurityDescriptor = null,
                    .bInheritHandle = .TRUE,
                },
                .creation = .OPEN,
            }),
            .ignore => nul_handle,
            .pipe => stderr_pipe[1],
            .close => null,
        },

        .lpReserved = null,
        .lpDesktop = null,
        .lpTitle = null,
        .dwX = 0,
        .dwY = 0,
        .dwXSize = 0,
        .dwYSize = 0,
        .dwXCountChars = 0,
        .dwYCountChars = 0,
        .dwFillAttribute = 0,
        .wShowWindow = 0,
        .cbReserved2 = 0,
        .lpReserved2 = null,
    };
    var piProcInfo: windows.PROCESS.INFORMATION = undefined;

    var arena_allocator = std.heap.ArenaAllocator.init(t.allocator);
    defer arena_allocator.deinit();
    const arena = arena_allocator.allocator();

    const cwd_w = cwd_w: {
        switch (options.cwd) {
            .inherit => break :cwd_w null,
            .dir => |cwd_dir| {
                var dir_path_buffer = try arena.alloc(u16, windows.PATH_MAX_WIDE + 1);
                const dir_path = try GetFinalPathNameByHandle(
                    cwd_dir.handle,
                    .{},
                    dir_path_buffer[0..windows.PATH_MAX_WIDE],
                );
                dir_path_buffer[dir_path.len] = 0;
                // Shrink the allocation down to just the path buffer + sentinel
                dir_path_buffer = try arena.realloc(dir_path_buffer, dir_path.len + 1);
                break :cwd_w dir_path_buffer[0..dir_path.len :0];
            },
            .path => |cwd| {
                break :cwd_w try std.unicode.wtf8ToWtf16LeAllocZ(arena, cwd);
            },
        }
    };
    const cwd_w_ptr = if (cwd_w) |cwd| cwd.ptr else null;

    const env_block = env_block: {
        const prog_handle = if (options.progress_node.index != .none)
            prog_pipe[1]
        else
            windows.INVALID_HANDLE_VALUE;
        if (options.environ_map) |environ_map| break :env_block try environ_map.createWindowsBlock(arena, .{
            .zig_progress_handle = prog_handle,
        });
        break :env_block try t.environ.process_environ.createWindowsBlock(arena, .{
            .zig_progress_handle = if (options.progress_node.index != .none) prog_pipe[1] else windows.INVALID_HANDLE_VALUE,
        });
    };

    const app_name_wtf8 = options.argv[0];
    const app_name_is_absolute = Dir.path.isAbsolute(app_name_wtf8);

    // The cwd provided by options is in effect when choosing the executable
    // path to match POSIX semantics.
    const cwd_path_w = x: {
        // If the app name is absolute, then we need to use its dirname as the cwd
        if (app_name_is_absolute) {
            const dir = Dir.path.dirname(app_name_wtf8).?;
            break :x try std.unicode.wtf8ToWtf16LeAllocZ(arena, dir);
        } else if (cwd_w) |cwd| {
            break :x cwd;
        } else {
            break :x &[_:0]u16{}; // empty for cwd
        }
    };

    // If the app name has more than just a filename, then we need to separate
    // that into the basename and dirname and use the dirname as an addition to
    // the cwd path. This is because NtQueryDirectoryFile cannot accept
    // FileName params with path separators.
    const app_basename_wtf8 = Dir.path.basename(app_name_wtf8);
    // If the app name is absolute, then the cwd will already have the app's dirname in it,
    // so only populate app_dirname if app name is a relative path with > 0 path separators.
    const maybe_app_dirname_wtf8 = if (!app_name_is_absolute) Dir.path.dirname(app_name_wtf8) else null;
    const app_dirname_w: ?[:0]u16 = x: {
        if (maybe_app_dirname_wtf8) |app_dirname_wtf8| {
            break :x try std.unicode.wtf8ToWtf16LeAllocZ(arena, app_dirname_wtf8);
        }
        break :x null;
    };
    const app_name_w = try std.unicode.wtf8ToWtf16LeAllocZ(arena, app_basename_wtf8);

    const flags: windows.CreateProcessFlags = .{
        .create_suspended = options.start_suspended,
        .create_unicode_environment = true,
        .create_no_window = options.create_no_window,
    };

    run: {
        // We have to scan each time because the PEB environment pointer is not stable.
        const env_strings: WindowsEnvironStrings = .scan();
        const PATH = env_strings.PATH orelse &[_:0]u16{};
        const PATHEXT = env_strings.PATHEXT orelse &[_:0]u16{};

        // In case the command ends up being a .bat/.cmd script, we need to escape things using the cmd.exe rules
        // and invoke cmd.exe ourselves in order to mitigate arbitrary command execution from maliciously
        // constructed arguments.
        //
        // We'll need to wait until we're actually trying to run the command to know for sure
        // if the resolved command has the `.bat` or `.cmd` extension, so we defer actually
        // serializing the command line until we determine how it should be serialized.
        var cmd_line_cache = WindowsCommandLineCache.init(arena, options.argv);

        var app_buf: std.ArrayList(u16) = .empty;
        try app_buf.appendSlice(arena, app_name_w);

        var dir_buf: std.ArrayList(u16) = .empty;

        if (cwd_path_w.len > 0) {
            try dir_buf.appendSlice(arena, cwd_path_w);
        }
        if (app_dirname_w) |app_dir| {
            if (dir_buf.items.len > 0) try dir_buf.append(arena, Dir.path.sep);
            try dir_buf.appendSlice(arena, app_dir);
        }

        windowsCreateProcessPathExt(
            arena,
            &dir_buf,
            &app_buf,
            PATHEXT,
            &cmd_line_cache,
            env_block,
            cwd_w_ptr,
            flags,
            &siStartInfo,
            &piProcInfo,
        ) catch |no_path_err| {
            const original_err = switch (no_path_err) {
                // argv[0] contains unsupported characters that will never resolve to a valid exe.
                error.InvalidArg0 => return error.FileNotFound,
                error.FileNotFound, error.InvalidExe, error.AccessDenied => |e| e,
                error.UnrecoverableInvalidExe => return error.InvalidExe,
                else => |e| return e,
            };

            // If the app name had path separators, that disallows PATH searching,
            // and there's no need to search the PATH if the app name is absolute.
            // We still search the path if the cwd is absolute because of the
            // "cwd provided by options is in effect when choosing the executable path
            // to match posix semantics" behavior--we don't want to skip searching
            // the PATH just because we were trying to set the cwd of the child process.
            if (app_dirname_w != null or app_name_is_absolute) {
                return original_err;
            }

            var it = std.mem.tokenizeScalar(u16, PATH, ';');
            while (it.next()) |search_path| {
                dir_buf.clearRetainingCapacity();
                try dir_buf.appendSlice(arena, search_path);

                if (windowsCreateProcessPathExt(
                    arena,
                    &dir_buf,
                    &app_buf,
                    PATHEXT,
                    &cmd_line_cache,
                    env_block,
                    cwd_w_ptr,
                    flags,
                    &siStartInfo,
                    &piProcInfo,
                )) {
                    break :run;
                } else |err| switch (err) {
                    // argv[0] contains unsupported characters that will never resolve to a valid exe.
                    error.InvalidArg0 => return error.FileNotFound,
                    error.FileNotFound, error.AccessDenied, error.InvalidExe => continue,
                    error.UnrecoverableInvalidExe => return error.InvalidExe,
                    else => |e| return e,
                }
            } else {
                return original_err;
            }
        };
    }

    if (options.progress_node.index != .none) {
        windows.CloseHandle(prog_pipe[1]);
        options.progress_node.setIpcFile(t, .{ .handle = prog_pipe[0], .flags = .{ .nonblocking = true } });
    }

    return .{
        .id = piProcInfo.hProcess,
        .thread_handle = piProcInfo.hThread,
        .stdin = stdin: switch (options.stdin) {
            .file => {
                windows.CloseHandle(siStartInfo.hStdInput.?);
                break :stdin null;
            },
            .pipe => {
                windows.CloseHandle(stdin_pipe[1]);
                break :stdin .{ .handle = stdin_pipe[0], .flags = .{ .nonblocking = false } };
            },
            else => null,
        },
        .stdout = stdout: switch (options.stdout) {
            .file => {
                windows.CloseHandle(siStartInfo.hStdOutput.?);
                break :stdout null;
            },
            .pipe => {
                windows.CloseHandle(stdout_pipe[1]);
                break :stdout .{ .handle = stdout_pipe[0], .flags = .{ .nonblocking = true } };
            },
            else => null,
        },
        .stderr = stderr: switch (options.stderr) {
            .file => {
                windows.CloseHandle(siStartInfo.hStdError.?);
                break :stderr null;
            },
            .pipe => {
                windows.CloseHandle(stderr_pipe[1]);
                break :stderr .{ .handle = stderr_pipe[0], .flags = .{ .nonblocking = true } };
            },
            else => null,
        },
        .request_resource_usage_statistics = options.request_resource_usage_statistics,
    };
}

fn inheritFile() windows.HANDLE {}

fn getCngDevice(t: *Threaded) Io.RandomSecureError!windows.HANDLE {
    {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);
        if (t.random_file.handle) |handle| return handle;
    }

    var fresh_handle: windows.HANDLE = undefined;
    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtOpenFile(
        &fresh_handle,
        .{
            .STANDARD = .{ .SYNCHRONIZE = true },
            .SPECIFIC = .{ .FILE = .{ .READ_DATA = true } },
        },
        &.{ .ObjectName = @constCast(&windows.UNICODE_STRING.init(
            &.{ '\\', 'D', 'e', 'v', 'i', 'c', 'e', '\\', 'C', 'N', 'G' },
        )) },
        &io_status_block,
        .VALID_FLAGS,
        .{ .IO = .SYNCHRONOUS_NONALERT },
    )) {
        .SUCCESS => {
            syscall.finish();
            mutexLock(&t.mutex); // Another thread might have won the race.
            defer mutexUnlock(&t.mutex);
            if (t.random_file.handle) |prev_handle| {
                windows.CloseHandle(fresh_handle);
                return prev_handle;
            } else {
                t.random_file.handle = fresh_handle;
                return fresh_handle;
            }
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.EntropyUnavailable), // Observed on wine 10.0
        else => return syscall.fail(error.EntropyUnavailable),
    };
}

fn getNulDevice(t: *Threaded) !windows.HANDLE {
    {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);
        if (t.null_file.handle) |handle| return handle;
    }

    var fresh_handle: windows.HANDLE = undefined;
    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtOpenFile(
        &fresh_handle,
        .{
            .STANDARD = .{ .SYNCHRONIZE = true },
            .SPECIFIC = .{ .FILE = .{ .READ_DATA = true, .WRITE_DATA = true } },
        },
        &.{
            .Attributes = .{ .INHERIT = true },
            .ObjectName = @constCast(&windows.UNICODE_STRING.init(
                &.{ '\\', 'D', 'e', 'v', 'i', 'c', 'e', '\\', 'N', 'u', 'l', 'l' },
            )),
        },
        &io_status_block,
        .VALID_FLAGS,
        .{ .IO = .SYNCHRONOUS_NONALERT },
    )) {
        .SUCCESS => {
            syscall.finish();
            mutexLock(&t.mutex); // Another thread might have won the race.
            defer mutexUnlock(&t.mutex);
            if (t.null_file.handle) |prev_handle| {
                windows.CloseHandle(fresh_handle);
                return prev_handle;
            } else {
                t.null_file.handle = fresh_handle;
                return fresh_handle;
            }
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
        .OBJECT_PATH_SYNTAX_BAD => |status| return syscall.ntstatusBug(status),
        .INVALID_HANDLE => |status| return syscall.ntstatusBug(status),
        .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
        .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .NO_MEDIA_IN_DEVICE => return syscall.fail(error.NoDevice),
        .SHARING_VIOLATION => return syscall.fail(error.AccessDenied),
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .PIPE_NOT_AVAILABLE => return syscall.fail(error.NoDevice),
        .FILE_IS_A_DIRECTORY => return syscall.fail(error.IsDir),
        .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
        .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
        else => |status| return syscall.unexpectedNtstatus(status),
    };
}

fn getNamedPipeDevice(t: *Threaded) !windows.HANDLE {
    {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);
        if (t.pipe_file.handle) |handle| return handle;
    }

    var fresh_handle: windows.HANDLE = undefined;
    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtOpenFile(
        &fresh_handle,
        .{ .STANDARD = .{ .SYNCHRONIZE = true } },
        &.{
            .ObjectName = @constCast(&windows.UNICODE_STRING.init(
                &.{ '\\', 'D', 'e', 'v', 'i', 'c', 'e', '\\', 'N', 'a', 'm', 'e', 'd', 'P', 'i', 'p', 'e', '\\' },
            )),
        },
        &io_status_block,
        .VALID_FLAGS,
        .{ .IO = .SYNCHRONOUS_NONALERT },
    )) {
        .SUCCESS => {
            syscall.finish();
            mutexLock(&t.mutex); // Another thread might have won the race.
            defer mutexUnlock(&t.mutex);
            if (t.pipe_file.handle) |prev_handle| {
                windows.CloseHandle(fresh_handle);
                return prev_handle;
            } else {
                t.pipe_file.handle = fresh_handle;
                return fresh_handle;
            }
        },
        .DELETE_PENDING => {
            // This error means that there *was* a file in this location on
            // the file system, but it was deleted. However, the OS is not
            // finished with the deletion operation, and so this CreateFile
            // call has failed. There is not really a sane way to handle
            // this other than retrying the creation after the OS finishes
            // the deletion.
            syscall.finish();
            try parking_sleep.sleep(.{ .duration = .{
                .raw = .fromMilliseconds(1),
                .clock = .awake,
            } });
            syscall = try .start();
            continue;
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
        .OBJECT_PATH_SYNTAX_BAD => |status| return syscall.ntstatusBug(status),
        .INVALID_HANDLE => |status| return syscall.ntstatusBug(status),
        .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
        .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .NO_MEDIA_IN_DEVICE => return syscall.fail(error.NoDevice),
        .SHARING_VIOLATION => return syscall.fail(error.AccessDenied),
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .PIPE_NOT_AVAILABLE => return syscall.fail(error.NoDevice),
        .FILE_IS_A_DIRECTORY => return syscall.fail(error.IsDir),
        .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
        .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
        else => |status| return syscall.unexpectedNtstatus(status),
    };
}

/// Expects `app_buf` to contain exactly the app name, and `dir_buf` to contain exactly the dir path.
/// After return, `app_buf` will always contain exactly the app name and `dir_buf` will always contain exactly the dir path.
/// Note: `app_buf` should not contain any leading path separators.
/// Note: If the dir is the cwd, dir_buf should be empty (len = 0).
fn windowsCreateProcessPathExt(
    arena: Allocator,
    dir_buf: *std.ArrayList(u16),
    app_buf: *std.ArrayList(u16),
    pathext: [:0]const u16,
    cmd_line_cache: *WindowsCommandLineCache,
    env_block: ?process.Environ.WindowsBlock,
    cwd_ptr: ?[*:0]u16,
    flags: windows.CreateProcessFlags,
    lpStartupInfo: *windows.STARTUPINFOW,
    lpProcessInformation: *windows.PROCESS.INFORMATION,
) !void {
    const app_name_len = app_buf.items.len;
    const dir_path_len = dir_buf.items.len;

    if (app_name_len == 0) return error.FileNotFound;

    defer app_buf.shrinkRetainingCapacity(app_name_len);
    defer dir_buf.shrinkRetainingCapacity(dir_path_len);

    // The name of the game here is to avoid CreateProcessW calls at all costs,
    // and only ever try calling it when we have a real candidate for execution.
    // Secondarily, we want to minimize the number of syscalls used when checking
    // for each PATHEXT-appended version of the app name.
    //
    // An overview of the technique used:
    // - Open the search directory for iteration (either cwd or a path from PATH)
    // - Use NtQueryDirectoryFile with a wildcard filename of `<app name>*` to
    //   check if anything that could possibly match either the unappended version
    //   of the app name or any of the versions with a PATHEXT value appended exists.
    // - If the wildcard NtQueryDirectoryFile call found nothing, we can exit early
    //   without needing to use PATHEXT at all.
    //
    // This allows us to use a <open dir, NtQueryDirectoryFile, close dir> sequence
    // for any directory that doesn't contain any possible matches, instead of having
    // to use a separate look up for each individual filename combination (unappended +
    // each PATHEXT appended). For directories where the wildcard *does* match something,
    // we iterate the matches and take note of any that are either the unappended version,
    // or a version with a supported PATHEXT appended. We then try calling CreateProcessW
    // with the found versions in the appropriate order.
    const dir = dir: {
        // needs to be null-terminated
        try dir_buf.append(arena, 0);
        defer dir_buf.shrinkRetainingCapacity(dir_path_len);
        const dir_path_z = dir_buf.items[0 .. dir_buf.items.len - 1 :0];
        const prefixed_path = try wToPrefixedFileW(null, dir_path_z, .{});
        break :dir dirOpenDirWindows(.cwd(), prefixed_path.span(), .{
            .iterate = true,
        }) catch |err| switch (err) {
            // These errors must not be ignored because they should not be able
            // to affect which file is chosen to execute. Also `error.Canceled`
            // must never be swallowed.
            error.Canceled,
            error.SystemResources,
            error.Unexpected,
            error.ProcessFdQuotaExceeded,
            error.SystemFdQuotaExceeded,
            => |e| return e,

            error.AccessDenied,
            error.PermissionDenied,
            error.SymLinkLoop,
            error.FileNotFound,
            error.NotDir,
            error.NoDevice,
            error.NetworkNotFound,
            error.NameTooLong,
            error.BadPathName,
            => return error.FileNotFound,
        };
    };
    defer windows.CloseHandle(dir.handle);

    // Add wildcard and null-terminator
    try app_buf.append(arena, '*');
    try app_buf.append(arena, 0);
    const app_name_wildcard = app_buf.items[0 .. app_buf.items.len - 1 :0];

    // This 2048 is arbitrary, we just want it to be large enough to get multiple FILE_DIRECTORY_INFORMATION entries
    // returned per NtQueryDirectoryFile call.
    var file_information_buf: [2048]u8 align(@alignOf(windows.FILE_DIRECTORY_INFORMATION)) = undefined;
    const file_info_maximum_single_entry_size = @sizeOf(windows.FILE_DIRECTORY_INFORMATION) + (windows.NAME_MAX * 2);
    if (file_information_buf.len < file_info_maximum_single_entry_size) {
        @compileError("file_information_buf must be large enough to contain at least one maximum size FILE_DIRECTORY_INFORMATION entry");
    }
    var io_status: windows.IO_STATUS_BLOCK = undefined;

    const num_supported_pathext = @typeInfo(process.WindowsExtension).@"enum".fields.len;
    var pathext_seen = [_]bool{false} ** num_supported_pathext;
    var any_pathext_seen = false;
    var unappended_exists = false;

    // Fully iterate the wildcard matches via NtQueryDirectoryFile and take note of all versions
    // of the app_name we should try to spawn.
    // Note: This is necessary because the order of the files returned is filesystem-dependent:
    //       On NTFS, `blah.exe*` will always return `blah.exe` first if it exists.
    //       On FAT32, it's possible for something like `blah.exe.obj` to be returned first.
    while (true) {
        // If we get nothing with the wildcard, then we can just bail out
        // as we know appending PATHEXT will not yield anything.
        switch (windows.ntdll.NtQueryDirectoryFile(
            dir.handle,
            null,
            null,
            null,
            &io_status,
            &file_information_buf,
            file_information_buf.len,
            .Directory,
            .FALSE, // single result
            &.init(app_name_wildcard),
            .FALSE, // restart iteration
        )) {
            .SUCCESS => {},
            .NO_SUCH_FILE => return error.FileNotFound,
            .NO_MORE_FILES => break,
            .ACCESS_DENIED => return error.AccessDenied,
            else => |status| return windows.unexpectedStatus(status),
        }

        // According to the docs, this can only happen if there is not enough room in the
        // buffer to write at least one complete FILE_DIRECTORY_INFORMATION entry.
        // Therefore, this condition should not be possible to hit with the buffer size we use.
        std.debug.assert(io_status.Information != 0);

        var it = windows.FileInformationIterator(windows.FILE_DIRECTORY_INFORMATION){ .buf = &file_information_buf };
        while (it.next()) |info| {
            // Skip directories
            if (info.FileAttributes.DIRECTORY) continue;
            const filename = @as([*]u16, @ptrCast(&info.FileName))[0 .. info.FileNameLength / 2];
            // Because all results start with the app_name since we're using the wildcard `app_name*`,
            // if the length is equal to app_name then this is an exact match
            if (filename.len == app_name_len) {
                // Note: We can't break early here because it's possible that the unappended version
                //       fails to spawn, in which case we still want to try the PATHEXT appended versions.
                unappended_exists = true;
            } else if (windowsCreateProcessSupportsExtension(filename[app_name_len..])) |pathext_ext| {
                pathext_seen[@intFromEnum(pathext_ext)] = true;
                any_pathext_seen = true;
            }
        }
    }

    const unappended_err = unappended: {
        if (unappended_exists) {
            if (dir_path_len != 0) switch (dir_buf.items[dir_buf.items.len - 1]) {
                '/', '\\' => {},
                else => try dir_buf.append(arena, Dir.path.sep),
            };
            try dir_buf.appendSlice(arena, app_buf.items[0..app_name_len]);
            try dir_buf.append(arena, 0);
            const full_app_name = dir_buf.items[0 .. dir_buf.items.len - 1 :0];

            const is_bat_or_cmd = bat_or_cmd: {
                const app_name = app_buf.items[0..app_name_len];
                const ext_start = std.mem.lastIndexOfScalar(u16, app_name, '.') orelse break :bat_or_cmd false;
                const ext = app_name[ext_start..];
                const ext_enum = windowsCreateProcessSupportsExtension(ext) orelse break :bat_or_cmd false;
                switch (ext_enum) {
                    .cmd, .bat => break :bat_or_cmd true,
                    else => break :bat_or_cmd false,
                }
            };
            const cmd_line_w = if (is_bat_or_cmd)
                try cmd_line_cache.scriptCommandLine(full_app_name)
            else
                try cmd_line_cache.commandLine();
            const app_name_w = if (is_bat_or_cmd)
                try cmd_line_cache.cmdExePath()
            else
                full_app_name;

            if (windowsCreateProcess(
                app_name_w.ptr,
                cmd_line_w.ptr,
                env_block,
                cwd_ptr,
                flags,
                lpStartupInfo,
                lpProcessInformation,
            )) |_| {
                return;
            } else |err| switch (err) {
                error.FileNotFound,
                error.AccessDenied,
                => break :unappended err,
                error.InvalidExe => {
                    // On InvalidExe, if the extension of the app name is .exe then
                    // it's treated as an unrecoverable error. Otherwise, it'll be
                    // skipped as normal.
                    const app_name = app_buf.items[0..app_name_len];
                    const ext_start = std.mem.lastIndexOfScalar(u16, app_name, '.') orelse break :unappended err;
                    const ext = app_name[ext_start..];
                    if (windows.eqlIgnoreCaseWtf16(ext, std.unicode.utf8ToUtf16LeStringLiteral(".EXE"))) {
                        return error.UnrecoverableInvalidExe;
                    }
                    break :unappended err;
                },
                else => return err,
            }
        }
        break :unappended error.FileNotFound;
    };

    if (!any_pathext_seen) return unappended_err;

    // Now try any PATHEXT appended versions that we've seen
    var ext_it = std.mem.tokenizeScalar(u16, pathext, ';');
    while (ext_it.next()) |ext| {
        const ext_enum = windowsCreateProcessSupportsExtension(ext) orelse continue;
        if (!pathext_seen[@intFromEnum(ext_enum)]) continue;

        dir_buf.shrinkRetainingCapacity(dir_path_len);
        if (dir_path_len != 0) switch (dir_buf.items[dir_buf.items.len - 1]) {
            '/', '\\' => {},
            else => try dir_buf.append(arena, Dir.path.sep),
        };
        try dir_buf.appendSlice(arena, app_buf.items[0..app_name_len]);
        try dir_buf.appendSlice(arena, ext);
        try dir_buf.append(arena, 0);
        const full_app_name = dir_buf.items[0 .. dir_buf.items.len - 1 :0];

        const is_bat_or_cmd = switch (ext_enum) {
            .cmd, .bat => true,
            else => false,
        };
        const cmd_line_w = if (is_bat_or_cmd)
            try cmd_line_cache.scriptCommandLine(full_app_name)
        else
            try cmd_line_cache.commandLine();
        const app_name_w = if (is_bat_or_cmd)
            try cmd_line_cache.cmdExePath()
        else
            full_app_name;

        if (windowsCreateProcess(app_name_w.ptr, cmd_line_w.ptr, env_block, cwd_ptr, flags, lpStartupInfo, lpProcessInformation)) |_| {
            return;
        } else |err| switch (err) {
            error.FileNotFound => continue,
            error.AccessDenied => continue,
            error.InvalidExe => {
                // On InvalidExe, if the extension of the app name is .exe then
                // it's treated as an unrecoverable error. Otherwise, it'll be
                // skipped as normal.
                if (windows.eqlIgnoreCaseWtf16(ext, std.unicode.utf8ToUtf16LeStringLiteral(".EXE"))) {
                    return error.UnrecoverableInvalidExe;
                }
                continue;
            },
            else => return err,
        }
    }

    return unappended_err;
}

fn windowsCreateProcess(
    app_name: [*:0]u16,
    cmd_line: [*:0]u16,
    env_block: ?process.Environ.WindowsBlock,
    cwd_ptr: ?[*:0]u16,
    flags: windows.CreateProcessFlags,
    lpStartupInfo: *windows.STARTUPINFOW,
    lpProcessInformation: *windows.PROCESS.INFORMATION,
) !void {
    const syscall: Syscall = try .start();
    while (true) {
        if (windows.kernel32.CreateProcessW(
            app_name,
            cmd_line,
            null,
            null,
            .TRUE,
            flags,
            if (env_block) |block| block.slice.ptr else null,
            cwd_ptr,
            lpStartupInfo,
            lpProcessInformation,
        ).toBool()) {
            return syscall.finish();
        } else switch (windows.GetLastError()) {
            .INVALID_PARAMETER => unreachable,
            .OPERATION_ABORTED => {
                try syscall.checkCancel();
                continue;
            },
            .FILE_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .DIRECTORY => return syscall.fail(error.FileNotFound),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .INVALID_NAME => return syscall.fail(error.InvalidName),
            .FILENAME_EXCED_RANGE => return syscall.fail(error.NameTooLong),
            .SHARING_VIOLATION => return syscall.fail(error.FileBusy),
            .COMMITMENT_LIMIT => return syscall.fail(error.SystemResources),

            // These are all the system errors that are mapped to ENOEXEC by
            // the undocumented _dosmaperr (old CRT) or __acrt_errno_map_os_error
            // (newer CRT) functions. Their code can be found in crt/src/dosmap.c (old SDK)
            // or urt/misc/errno.cpp (newer SDK) in the Windows SDK.
            .BAD_FORMAT,
            .INVALID_STARTING_CODESEG, // MIN_EXEC_ERROR in errno.cpp
            .INVALID_STACKSEG,
            .INVALID_MODULETYPE,
            .INVALID_EXE_SIGNATURE,
            .EXE_MARKED_INVALID,
            .BAD_EXE_FORMAT,
            .ITERATED_DATA_EXCEEDS_64k,
            .INVALID_MINALLOCSIZE,
            .DYNLINK_FROM_INVALID_RING,
            .IOPL_NOT_ENABLED,
            .INVALID_SEGDPL,
            .AUTODATASEG_EXCEEDS_64k,
            .RING2SEG_MUST_BE_MOVABLE,
            .RELOC_CHAIN_XEEDS_SEGLIM,
            .INFLOOP_IN_RELOC_CHAIN, // MAX_EXEC_ERROR in errno.cpp
            // This one is not mapped to ENOEXEC but it is possible, for example
            // when calling CreateProcessW on a plain text file with a .exe extension
            .EXE_MACHINE_TYPE_MISMATCH,
            => return syscall.fail(error.InvalidExe),

            else => |err| {
                syscall.finish();
                return windows.unexpectedError(err);
            },
        }
    }
}

/// Case-insensitive WTF-16 lookup
fn windowsCreateProcessSupportsExtension(ext: []const u16) ?process.WindowsExtension {
    comptime {
        // Ensures keeping this function in sync with the enum.
        const fields = @typeInfo(process.WindowsExtension).@"enum".fields;
        assert(fields.len == 4);
        assert(@intFromEnum(process.WindowsExtension.bat) == 0);
        assert(@intFromEnum(process.WindowsExtension.cmd) == 1);
        assert(@intFromEnum(process.WindowsExtension.com) == 2);
        assert(@intFromEnum(process.WindowsExtension.exe) == 3);
    }

    if (ext.len != 4) return null;
    const State = enum {
        start,
        dot,
        b,
        ba,
        c,
        cm,
        co,
        e,
        ex,
    };
    var state: State = .start;
    for (ext) |c| switch (state) {
        .start => switch (c) {
            '.' => state = .dot,
            else => return null,
        },
        .dot => switch (c) {
            'b', 'B' => state = .b,
            'c', 'C' => state = .c,
            'e', 'E' => state = .e,
            else => return null,
        },
        .b => switch (c) {
            'a', 'A' => state = .ba,
            else => return null,
        },
        .c => switch (c) {
            'm', 'M' => state = .cm,
            'o', 'O' => state = .co,
            else => return null,
        },
        .e => switch (c) {
            'x', 'X' => state = .ex,
            else => return null,
        },
        .ba => switch (c) {
            't', 'T' => return .bat,
            else => return null,
        },
        .cm => switch (c) {
            'd', 'D' => return .cmd,
            else => return null,
        },
        .co => switch (c) {
            'm', 'M' => return .com,
            else => return null,
        },
        .ex => switch (c) {
            'e', 'E' => return .exe,
            else => return null,
        },
    };
    return null;
}

test windowsCreateProcessSupportsExtension {
    try std.testing.expectEqual(process.WindowsExtension.exe, windowsCreateProcessSupportsExtension(&[_]u16{ '.', 'e', 'X', 'e' }).?);
    try std.testing.expect(windowsCreateProcessSupportsExtension(&[_]u16{ '.', 'e', 'X', 'e', 'c' }) == null);
}

/// Serializes argv into a WTF-16 encoded command-line string for use with CreateProcessW.
///
/// Serialization is done on-demand and the result is cached in order to allow for:
/// - Only serializing the particular type of command line needed (`.bat`/`.cmd`
///   command line serialization is different from `.exe`/etc)
/// - Reusing the serialized command lines if necessary (i.e. if the execution
///   of a command fails and the PATH is going to be continued to be searched
///   for more candidates)
const WindowsCommandLineCache = struct {
    cmd_line: ?[:0]u16 = null,
    script_cmd_line: ?[:0]u16 = null,
    cmd_exe_path: ?[:0]u16 = null,
    argv: []const []const u8,
    allocator: Allocator,

    fn init(allocator: Allocator, argv: []const []const u8) WindowsCommandLineCache {
        return .{
            .allocator = allocator,
            .argv = argv,
        };
    }

    fn deinit(self: *WindowsCommandLineCache) void {
        if (self.cmd_line) |cmd_line| self.allocator.free(cmd_line);
        if (self.script_cmd_line) |script_cmd_line| self.allocator.free(script_cmd_line);
        if (self.cmd_exe_path) |cmd_exe_path| self.allocator.free(cmd_exe_path);
    }

    fn commandLine(self: *WindowsCommandLineCache) ![:0]u16 {
        if (self.cmd_line == null) {
            self.cmd_line = try argvToCommandLineWindows(self.allocator, self.argv);
        }
        return self.cmd_line.?;
    }

    /// Not cached, since the path to the batch script will change during PATH searching.
    /// `script_path` should be as qualified as possible, e.g. if the PATH is being searched,
    /// then script_path should include both the search path and the script filename
    /// (this allows avoiding cmd.exe having to search the PATH again).
    fn scriptCommandLine(self: *WindowsCommandLineCache, script_path: []const u16) ![:0]u16 {
        if (self.script_cmd_line) |v| self.allocator.free(v);
        self.script_cmd_line = try argvToScriptCommandLineWindows(
            self.allocator,
            script_path,
            self.argv[1..],
        );
        return self.script_cmd_line.?;
    }

    fn cmdExePath(self: *WindowsCommandLineCache) Allocator.Error![:0]u16 {
        if (self.cmd_exe_path == null) {
            // Remove trailing slash from system directory path; we'll re-add it below
            const system_dir = std.mem.trimEnd(u16, windows.getSystemDirectoryWtf16Le(), &.{ '/', '\\' });
            const suffix = std.unicode.utf8ToUtf16LeStringLiteral("\\cmd.exe");
            const buf = try self.allocator.allocSentinel(u16, system_dir.len + suffix.len, 0);
            errdefer comptime unreachable;
            @memcpy(buf[0..system_dir.len], system_dir);
            @memcpy(buf[system_dir.len..], suffix);
            self.cmd_exe_path = buf;
        }
        return self.cmd_exe_path.?;
    }
};

const ArgvToScriptCommandLineError = error{
    OutOfMemory,
    InvalidWtf8,
    /// NUL (U+0000), LF (U+000A), CR (U+000D) are not allowed
    /// within arguments when executing a `.bat`/`.cmd` script.
    /// - NUL/LF signifiies end of arguments, so anything afterwards
    ///   would be lost after execution.
    /// - CR is stripped by `cmd.exe`, so any CR codepoints
    ///   would be lost after execution.
    InvalidBatchScriptArg,
};

/// Serializes `argv` to a Windows command-line string that uses `cmd.exe /c` and `cmd.exe`-specific
/// escaping rules. The caller owns the returned slice.
///
/// Escapes `argv` using the suggested mitigation against arbitrary command execution from:
/// https://flatt.tech/research/posts/batbadbut-you-cant-securely-execute-commands-on-windows/
///
/// The return of this function will look like
/// `cmd.exe /d /e:ON /v:OFF /c "<escaped command line>"`
/// and should be used as the `lpCommandLine` of `CreateProcessW`, while the return of
/// `WindowsCommandLineCache.cmdExePath` should be used as `lpApplicationName`.
///
/// Should only be used when spawning `.bat`/`.cmd` scripts, see `argvToCommandLineWindows` otherwise.
/// The `.bat`/`.cmd` file must be known to both have the `.bat`/`.cmd` extension and exist on the filesystem.
fn argvToScriptCommandLineWindows(
    allocator: Allocator,
    /// Path to the `.bat`/`.cmd` script. If this path is relative, it is assumed to be relative to the CWD.
    /// The script must have been verified to exist at this path before calling this function.
    script_path: []const u16,
    /// Arguments, not including the script name itself. Expected to be encoded as WTF-8.
    script_args: []const []const u8,
) ArgvToScriptCommandLineError![:0]u16 {
    var buf = try std.array_list.Managed(u8).initCapacity(allocator, 64);
    defer buf.deinit();

    // `/d` disables execution of AutoRun commands.
    // `/e:ON` and `/v:OFF` are needed for BatBadBut mitigation:
    // > If delayed expansion is enabled via the registry value DelayedExpansion,
    // > it must be disabled by explicitly calling cmd.exe with the /V:OFF option.
    // > Escaping for % requires the command extension to be enabled.
    // > If it’s disabled via the registry value EnableExtensions, it must be enabled with the /E:ON option.
    // https://flatt.tech/research/posts/batbadbut-you-cant-securely-execute-commands-on-windows/
    buf.appendSliceAssumeCapacity("cmd.exe /d /e:ON /v:OFF /c \"");

    // Always quote the path to the script arg
    buf.appendAssumeCapacity('"');
    // We always want the path to the batch script to include a path separator in order to
    // avoid cmd.exe searching the PATH for the script. This is not part of the arbitrary
    // command execution mitigation, we just know exactly what script we want to execute
    // at this point, and potentially making cmd.exe re-find it is unnecessary.
    //
    // If the script path does not have a path separator, then we know its relative to CWD and
    // we can just put `.\` in the front.
    if (std.mem.findAny(u16, script_path, &[_]u16{
        std.mem.nativeToLittle(u16, '\\'), std.mem.nativeToLittle(u16, '/'),
    }) == null) {
        try buf.appendSlice(".\\");
    }
    // Note that we don't do any escaping/mitigations for this argument, since the relevant
    // characters (", %, etc) are illegal in file paths and this function should only be called
    // with script paths that have been verified to exist.
    try std.unicode.wtf16LeToWtf8ArrayList(&buf, script_path);
    buf.appendAssumeCapacity('"');

    for (script_args) |arg| {
        // Literal carriage returns get stripped when run through cmd.exe
        // and NUL/newlines act as 'end of command.' Because of this, it's basically
        // always a mistake to include these characters in argv, so it's
        // an error condition in order to ensure that the return of this
        // function can always roundtrip through cmd.exe.
        if (std.mem.findAny(u8, arg, "\x00\r\n") != null) {
            return error.InvalidBatchScriptArg;
        }

        // Separate args with a space.
        try buf.append(' ');

        // Need to quote if the argument is empty (otherwise the arg would just be lost)
        // or if the last character is a `\`, since then something like "%~2" in a .bat
        // script would cause the closing " to be escaped which we don't want.
        var needs_quotes = arg.len == 0 or arg[arg.len - 1] == '\\';
        if (!needs_quotes) {
            for (arg) |c| {
                switch (c) {
                    // Known good characters that don't need to be quoted
                    'A'...'Z', 'a'...'z', '0'...'9', '#', '$', '*', '+', '-', '.', '/', ':', '?', '@', '\\', '_' => {},
                    // When in doubt, quote
                    else => {
                        needs_quotes = true;
                        break;
                    },
                }
            }
        }
        if (needs_quotes) {
            try buf.append('"');
        }
        var backslashes: usize = 0;
        for (arg) |c| {
            switch (c) {
                '\\' => {
                    backslashes += 1;
                },
                '"' => {
                    try buf.appendNTimes('\\', backslashes);
                    try buf.append('"');
                    backslashes = 0;
                },
                // Replace `%` with `%%cd:~,%`.
                //
                // cmd.exe allows extracting a substring from an environment
                // variable with the syntax: `%foo:~<start_index>,<end_index>%`.
                // Therefore, `%cd:~,%` will always expand to an empty string
                // since both the start and end index are blank, and it is assumed
                // that `%cd%` is always available since it is a built-in variable
                // that corresponds to the current directory.
                //
                // This means that replacing `%foo%` with `%%cd:~,%foo%%cd:~,%`
                // will stop `%foo%` from being expanded and *after* expansion
                // we'll still be left with `%foo%` (the literal string).
                '%' => {
                    // the trailing `%` is appended outside the switch
                    try buf.appendSlice("%%cd:~,");
                    backslashes = 0;
                },
                else => {
                    backslashes = 0;
                },
            }
            try buf.append(c);
        }
        if (needs_quotes) {
            try buf.appendNTimes('\\', backslashes);
            try buf.append('"');
        }
    }

    try buf.append('"');

    return try std.unicode.wtf8ToWtf16LeAllocZ(allocator, buf.items);
}

const ArgvToCommandLineError = error{ OutOfMemory, InvalidWtf8, InvalidArg0 };

/// Serializes `argv` to a Windows command-line string suitable for passing to a child process and
/// parsing by the `CommandLineToArgvW` algorithm. The caller owns the returned slice.
///
/// To avoid arbitrary command execution, this function should not be used when spawning `.bat`/`.cmd` scripts.
/// https://flatt.tech/research/posts/batbadbut-you-cant-securely-execute-commands-on-windows/
///
/// When executing `.bat`/`.cmd` scripts, use `argvToScriptCommandLineWindows` instead.
fn argvToCommandLineWindows(
    allocator: Allocator,
    argv: []const []const u8,
) ArgvToCommandLineError![:0]u16 {
    var buf = std.array_list.Managed(u8).init(allocator);
    defer buf.deinit();

    if (argv.len != 0) {
        const arg0 = argv[0];

        // The first argument must be quoted if it contains spaces or ASCII control characters
        // (excluding DEL). It also follows special quoting rules where backslashes have no special
        // interpretation, which makes it impossible to pass certain first arguments containing
        // double quotes to a child process without characters from the first argument leaking into
        // subsequent ones (which could have security implications).
        //
        // Empty arguments technically don't need quotes, but we quote them anyway for maximum
        // compatibility with different implementations of the 'CommandLineToArgvW' algorithm.
        //
        // Double quotes are illegal in paths on Windows, so for the sake of simplicity we reject
        // all first arguments containing double quotes, even ones that we could theoretically
        // serialize in unquoted form.
        var needs_quotes = arg0.len == 0;
        for (arg0) |c| {
            if (c <= ' ') {
                needs_quotes = true;
            } else if (c == '"') {
                return error.InvalidArg0;
            }
        }
        if (needs_quotes) {
            try buf.append('"');
            try buf.appendSlice(arg0);
            try buf.append('"');
        } else {
            try buf.appendSlice(arg0);
        }

        for (argv[1..]) |arg| {
            try buf.append(' ');

            // Subsequent arguments must be quoted if they contain spaces, tabs or double quotes,
            // or if they are empty. For simplicity and for maximum compatibility with different
            // implementations of the 'CommandLineToArgvW' algorithm, we also quote all ASCII
            // control characters (again, excluding DEL).
            needs_quotes = for (arg) |c| {
                if (c <= ' ' or c == '"') {
                    break true;
                }
            } else arg.len == 0;
            if (!needs_quotes) {
                try buf.appendSlice(arg);
                continue;
            }

            try buf.append('"');
            var backslash_count: usize = 0;
            for (arg) |byte| {
                switch (byte) {
                    '\\' => {
                        backslash_count += 1;
                    },
                    '"' => {
                        try buf.appendNTimes('\\', backslash_count * 2 + 1);
                        try buf.append('"');
                        backslash_count = 0;
                    },
                    else => {
                        try buf.appendNTimes('\\', backslash_count);
                        try buf.append(byte);
                        backslash_count = 0;
                    },
                }
            }
            try buf.appendNTimes('\\', backslash_count * 2);
            try buf.append('"');
        }
    }

    return try std.unicode.wtf8ToWtf16LeAllocZ(allocator, buf.items);
}

test argvToCommandLineWindows {
    const t = testArgvToCommandLineWindows;

    try t(&.{
        \\C:\Program Files\zig\zig.exe
        ,
        \\run
        ,
        \\.\src\main.zig
        ,
        \\-target
        ,
        \\x86_64-windows-gnu
        ,
        \\-O
        ,
        \\ReleaseSafe
        ,
        \\--
        ,
        \\--emoji=🗿
        ,
        \\--eval=new Regex("Dwayne \"The Rock\" Johnson")
        ,
    },
        \\"C:\Program Files\zig\zig.exe" run .\src\main.zig -target x86_64-windows-gnu -O ReleaseSafe -- --emoji=🗿 "--eval=new Regex(\"Dwayne \\\"The Rock\\\" Johnson\")"
    );

    try t(&.{}, "");
    try t(&.{""}, "\"\"");
    try t(&.{" "}, "\" \"");
    try t(&.{"\t"}, "\"\t\"");
    try t(&.{"\x07"}, "\"\x07\"");
    try t(&.{"🦎"}, "🦎");

    try t(
        &.{ "zig", "aa aa", "bb\tbb", "cc\ncc", "dd\r\ndd", "ee\x7Fee" },
        "zig \"aa aa\" \"bb\tbb\" \"cc\ncc\" \"dd\r\ndd\" ee\x7Fee",
    );

    try t(
        &.{ "\\\\foo bar\\foo bar\\", "\\\\zig zag\\zig zag\\" },
        "\"\\\\foo bar\\foo bar\\\" \"\\\\zig zag\\zig zag\\\\\"",
    );

    try std.testing.expectError(
        error.InvalidArg0,
        argvToCommandLineWindows(std.testing.allocator, &.{"\"quotes\"quotes\""}),
    );
    try std.testing.expectError(
        error.InvalidArg0,
        argvToCommandLineWindows(std.testing.allocator, &.{"quotes\"quotes"}),
    );
    try std.testing.expectError(
        error.InvalidArg0,
        argvToCommandLineWindows(std.testing.allocator, &.{"q u o t e s \" q u o t e s"}),
    );
}

fn testArgvToCommandLineWindows(argv: []const []const u8, expected_cmd_line: []const u8) !void {
    const cmd_line_w = try argvToCommandLineWindows(std.testing.allocator, argv);
    defer std.testing.allocator.free(cmd_line_w);

    const cmd_line = try std.unicode.wtf16LeToWtf8Alloc(std.testing.allocator, cmd_line_w);
    defer std.testing.allocator.free(cmd_line);

    try std.testing.expectEqualStrings(expected_cmd_line, cmd_line);
}

fn posixExecv(
    arg0_expand: process.ArgExpansion,
    file: [*:0]const u8,
    child_argv: [*:null]?[*:0]const u8,
    env_block: process.Environ.PosixBlock,
    PATH: []const u8,
) process.ReplaceError {
    const file_slice = std.mem.sliceTo(file, 0);
    if (std.mem.findScalar(u8, file_slice, '/') != null) return posixExecvPath(file, child_argv, env_block);

    // Use of PATH_MAX here is valid as the path_buf will be passed
    // directly to the operating system in posixExecvPath.
    var path_buf: [posix.PATH_MAX]u8 = undefined;
    var it = std.mem.tokenizeScalar(u8, PATH, ':');
    var seen_eacces = false;
    var err: process.ReplaceError = error.FileNotFound;

    // In case of expanding arg0 we must put it back if we return with an error.
    const prev_arg0 = child_argv[0];
    defer switch (arg0_expand) {
        .expand => child_argv[0] = prev_arg0,
        .no_expand => {},
    };

    while (it.next()) |search_path| {
        const path_len = search_path.len + file_slice.len + 1;
        if (path_buf.len < path_len + 1) return error.NameTooLong;
        @memcpy(path_buf[0..search_path.len], search_path);
        path_buf[search_path.len] = '/';
        @memcpy(path_buf[search_path.len + 1 ..][0..file_slice.len], file_slice);
        path_buf[path_len] = 0;
        const full_path = path_buf[0..path_len :0].ptr;
        switch (arg0_expand) {
            .expand => child_argv[0] = full_path,
            .no_expand => {},
        }
        err = posixExecvPath(full_path, child_argv, env_block);
        switch (err) {
            error.AccessDenied => seen_eacces = true,
            error.FileNotFound, error.NotDir => {},
            else => |e| return e,
        }
    }
    if (seen_eacces) return error.AccessDenied;
    return err;
}

/// This function ignores PATH environment variable.
pub fn posixExecvPath(
    path: [*:0]const u8,
    child_argv: [*:null]const ?[*:0]const u8,
    env_block: process.Environ.PosixBlock,
) process.ReplaceError {
    try Thread.checkCancel();
    switch (posix.errno(posix.system.execve(path, child_argv, env_block.slice.ptr))) {
        .FAULT => |err| return errnoBug(err), // Bad pointer parameter.
        .@"2BIG" => return error.SystemResources,
        .MFILE => return error.ProcessFdQuotaExceeded,
        .NAMETOOLONG => return error.NameTooLong,
        .NFILE => return error.SystemFdQuotaExceeded,
        .NOMEM => return error.SystemResources,
        .ACCES => return error.AccessDenied,
        .PERM => return error.PermissionDenied,
        .INVAL => return error.InvalidExe,
        .NOEXEC => return error.InvalidExe,
        .IO => return error.FileSystem,
        .LOOP => return error.FileSystem,
        .ISDIR => return error.IsDir,
        .NOENT => return error.FileNotFound,
        .NOTDIR => return error.NotDir,
        .TXTBSY => return error.FileBusy,
        else => |err| switch (native_os) {
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (err) {
                .BADEXEC => return error.InvalidExe,
                .BADARCH => return error.InvalidExe,
                else => return posix.unexpectedErrno(err),
            },
            .linux => switch (err) {
                .LIBBAD => return error.InvalidExe,
                else => return posix.unexpectedErrno(err),
            },
            else => return posix.unexpectedErrno(err),
        },
    }
}

pub const CreatePipeOptions = struct {
    server: End,
    client: End,
    inbound: bool = false,
    outbound: bool = false,
    maximum_instances: u32 = 1,
    quota: u32 = 4096,
    default_timeout: windows.LARGE_INTEGER = -120 * std.time.ns_per_s / 100,

    pub const End = struct {
        attributes: windows.OBJECT.ATTRIBUTES.Flags = .{},
        mode: windows.FILE.MODE,
    };
};
pub fn windowsCreatePipe(t: *Threaded, options: CreatePipeOptions) ![2]windows.HANDLE {
    const named_pipe_device = try t.getNamedPipeDevice();
    const server_handle = server_handle: {
        var handle: windows.HANDLE = undefined;
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtCreateNamedPipeFile(
            &handle,
            .{
                .SPECIFIC = .{ .FILE_PIPE = .{
                    .READ_DATA = options.inbound,
                    .WRITE_DATA = options.outbound,
                    .WRITE_ATTRIBUTES = true,
                } },
                .STANDARD = .{ .SYNCHRONIZE = true },
            },
            &.{
                .RootDirectory = named_pipe_device,
                .Attributes = options.server.attributes,
            },
            &io_status_block,
            .{ .READ = true, .WRITE = true },
            .CREATE,
            options.server.mode,
            .{ .TYPE = .BYTE_STREAM },
            .{ .MODE = .BYTE_STREAM },
            .{ .OPERATION = .QUEUE },
            options.maximum_instances,
            if (options.inbound) options.quota else 0,
            if (options.outbound) options.quota else 0,
            &options.default_timeout,
        )) {
            .SUCCESS => break syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
            .INSUFFICIENT_RESOURCES => return syscall.fail(error.SystemResources),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
        break :server_handle handle;
    };
    errdefer windows.CloseHandle(server_handle);
    const client_handle = client_handle: {
        var handle: windows.HANDLE = undefined;
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtOpenFile(
            &handle,
            .{
                .SPECIFIC = .{ .FILE_PIPE = .{
                    .READ_DATA = options.outbound,
                    .WRITE_DATA = options.inbound,
                    .WRITE_ATTRIBUTES = true,
                } },
                .STANDARD = .{ .SYNCHRONIZE = true },
            },
            &.{
                .RootDirectory = server_handle,
                .Attributes = options.client.attributes,
            },
            &io_status_block,
            .{ .READ = true, .WRITE = true },
            options.client.mode,
        )) {
            .SUCCESS => break syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
            .INSUFFICIENT_RESOURCES => return syscall.fail(error.SystemResources),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
        break :client_handle handle;
    };
    errdefer windows.CloseHandle(client_handle);
    return .{ server_handle, client_handle };
}

fn progressParentFile(userdata: ?*anyopaque) std.Progress.ParentFileError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    t.scanEnviron();
    return t.environ.zig_progress_file;
}

pub fn environString(t: *Threaded, comptime name: []const u8) ?[:0]const u8 {
    t.scanEnviron();
    return @field(t.environ.string, name);
}

fn random(userdata: ?*anyopaque, buffer: []u8) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const thread = Thread.current orelse return randomMainThread(t, buffer);
    if (!thread.csprng.isInitialized()) {
        @branchHint(.unlikely);
        var seed: [Csprng.seed_len]u8 = undefined;
        randomMainThread(t, &seed);
        thread.csprng.rng = .init(seed);
    }
    thread.csprng.rng.fill(buffer);
}

fn randomMainThread(t: *Threaded, buffer: []u8) void {
    mutexLock(&t.mutex);
    defer mutexUnlock(&t.mutex);

    if (!t.csprng.isInitialized()) {
        @branchHint(.unlikely);
        var seed: [Csprng.seed_len]u8 = undefined;
        {
            mutexUnlock(&t.mutex);
            defer mutexLock(&t.mutex);

            const prev = swapCancelProtection(t, .blocked);
            defer _ = swapCancelProtection(t, prev);

            randomSecure(t, &seed) catch |err| switch (err) {
                error.Canceled => unreachable,
                error.EntropyUnavailable => fallbackSeed(t, &seed),
            };
        }
        t.csprng.rng = .init(seed);
    }

    t.csprng.rng.fill(buffer);
}

pub fn fallbackSeed(aslr_addr: ?*anyopaque, seed: *[Csprng.seed_len]u8) void {
    @memset(seed, 0);
    std.mem.writeInt(usize, seed[seed.len - @sizeOf(usize) ..][0..@sizeOf(usize)], @intFromPtr(aslr_addr), .native);
    const fallbackSeedImpl = switch (native_os) {
        .windows => fallbackSeedWindows,
        .wasi => if (builtin.link_libc) fallbackSeedPosix else fallbackSeedWasi,
        else => fallbackSeedPosix,
    };
    fallbackSeedImpl(seed);
}

fn fallbackSeedPosix(seed: *[Csprng.seed_len]u8) void {
    std.mem.writeInt(posix.pid_t, seed[0..@sizeOf(posix.pid_t)], posix.system.getpid(), .native);
    const i_1 = @sizeOf(posix.pid_t);

    var ts: posix.timespec = undefined;
    const Sec = @TypeOf(ts.sec);
    const Nsec = @TypeOf(ts.nsec);
    const i_2 = i_1 + @sizeOf(Sec);
    switch (posix.errno(posix.system.clock_gettime(.REALTIME, &ts))) {
        .SUCCESS => {
            std.mem.writeInt(Sec, seed[i_1..][0..@sizeOf(Sec)], ts.sec, .native);
            std.mem.writeInt(Nsec, seed[i_2..][0..@sizeOf(Nsec)], ts.nsec, .native);
        },
        else => {},
    }
}

fn fallbackSeedWindows(seed: *[Csprng.seed_len]u8) void {
    var pc: windows.LARGE_INTEGER = undefined;
    _ = windows.ntdll.RtlQueryPerformanceCounter(&pc);
    std.mem.writeInt(windows.LARGE_INTEGER, seed[0..@sizeOf(windows.LARGE_INTEGER)], pc, .native);
}

fn fallbackSeedWasi(seed: *[Csprng.seed_len]u8) void {
    var ts: std.os.wasi.timestamp_t = undefined;
    if (std.os.wasi.clock_time_get(.REALTIME, 1, &ts) == .SUCCESS) {
        std.mem.writeInt(std.os.wasi.timestamp_t, seed[0..@sizeOf(std.os.wasi.timestamp_t)], ts, .native);
    }
}

fn randomSecure(userdata: ?*anyopaque, buffer: []u8) Io.RandomSecureError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    if (is_windows) {
        if (buffer.len == 0) return;
        // ProcessPrng from bcryptprimitives.dll has the following properties:
        // * introduces a dependency on bcryptprimitives.dll, which apparently
        //   runs a test suite every time it is loaded
        // * heap allocates a 48-byte buffer, handling failure by returning NO_MEMORY in a BOOL
        //   despite the function being documented to always return TRUE
        // * reads from "\\Device\\CNG" which then seeds a per-CPU AES CSPRNG
        // Therefore, that function is avoided in favor of using the device directly.
        const cng_device = try getCngDevice(t);
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        var i: usize = 0;
        const syscall: Syscall = try .start();
        while (true) {
            const remaining_len = std.math.lossyCast(u32, buffer.len - i);
            switch (windows.ntdll.NtDeviceIoControlFile(
                cng_device,
                null,
                null,
                null,
                &io_status_block,
                windows.IOCTL.KSEC.GEN_RANDOM,
                null,
                0,
                buffer[i..].ptr,
                remaining_len,
            )) {
                .SUCCESS => {
                    i += remaining_len;
                    if (buffer.len - i == 0) {
                        return syscall.finish();
                    } else {
                        try syscall.checkCancel();
                        continue;
                    }
                },
                .CANCELLED => {
                    try syscall.checkCancel();
                    continue;
                },
                else => return syscall.fail(error.EntropyUnavailable),
            }
        }
    }

    if (builtin.link_libc and @TypeOf(posix.system.arc4random_buf) != void) {
        if (buffer.len == 0) return;
        posix.system.arc4random_buf(buffer.ptr, buffer.len);
        return;
    }

    if (native_os == .wasi) {
        if (buffer.len == 0) return;
        const syscall: Syscall = try .start();
        while (true) switch (std.os.wasi.random_get(buffer.ptr, buffer.len)) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => return syscall.fail(error.EntropyUnavailable),
        };
    }

    if (@TypeOf(posix.system.getrandom) != void) {
        const getrandom = if (use_libc_getrandom) std.c.getrandom else std.os.linux.getrandom;
        var i: usize = 0;
        const syscall: Syscall = try .start();
        while (buffer.len - i != 0) {
            const buf = buffer[i..];
            const rc = getrandom(buf.ptr, buf.len, 0);
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    const n: usize = @intCast(rc);
                    i += n;
                    continue;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => return syscall.fail(error.EntropyUnavailable),
            }
        }
        return;
    }

    if (native_os == .emscripten) {
        if (buffer.len == 0) return;
        const err = posix.errno(std.c.getentropy(buffer.ptr, buffer.len));
        switch (err) {
            .SUCCESS => return,
            else => return error.EntropyUnavailable,
        }
    }

    if (native_os == .linux) {
        comptime assert(use_dev_urandom);
        const urandom_fd = try getRandomFd(t);

        var i: usize = 0;
        while (buffer.len - i != 0) {
            const syscall: Syscall = try .start();
            const rc = posix.system.read(urandom_fd, buffer[i..].ptr, buffer.len - i);
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    const n: usize = @intCast(rc);
                    if (n == 0) return error.EntropyUnavailable;
                    i += n;
                    continue;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => return syscall.fail(error.EntropyUnavailable),
            }
        }
    }

    return error.EntropyUnavailable;
}

fn getRandomFd(t: *Threaded) Io.RandomSecureError!posix.fd_t {
    {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);

        if (t.random_file.fd == -2) return error.EntropyUnavailable;
        if (t.random_file.fd != -1) return t.random_file.fd;
    }

    const mode: posix.mode_t = 0;

    const fd: posix.fd_t = fd: {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = openat_sym(posix.AT.FDCWD, "/dev/urandom", .{
                .ACCMODE = .RDONLY,
                .CLOEXEC = true,
            }, mode);
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    break :fd @intCast(rc);
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => return syscall.fail(error.EntropyUnavailable),
            }
        }
    };
    errdefer closeFd(fd);

    switch (native_os) {
        .linux => {
            const sys = if (statx_use_c) std.c else std.os.linux;
            const syscall: Syscall = try .start();
            while (true) {
                var statx = std.mem.zeroes(std.os.linux.Statx);
                switch (sys.errno(sys.statx(fd, "", std.os.linux.AT.EMPTY_PATH, .{ .TYPE = true }, &statx))) {
                    .SUCCESS => {
                        syscall.finish();
                        if (!statx.mask.TYPE) return error.EntropyUnavailable;
                        mutexLock(&t.mutex); // Another thread might have won the race.
            
```
