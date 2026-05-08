```
   .AGAIN => |err| return errnoBug(err),
        .OPNOTSUPP => return error.FileLocksUnsupported,
        else => |err| return unexpectedErrno(err),
    };
}

fn fileTryLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!bool {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const operation: i32 = switch (lock) {
        .none => c.LOCK.UN,
        .shared => c.LOCK.SH | c.LOCK.NB,
        .exclusive => c.LOCK.EX | c.LOCK.NB,
    };
    while (true) switch (c.errno(c.flock(file.handle, operation))) {
        .SUCCESS => return true,
        .INTR => {},
        .AGAIN => return false,
        .BADF => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err), // invalid parameters
        .NOLCK => return error.SystemResources,
        .OPNOTSUPP => return error.FileLocksUnsupported,
        else => |err| return unexpectedErrno(err),
    };
}

fn fileUnlock(userdata: ?*anyopaque, file: File) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    while (true) switch (c.errno(c.flock(file.handle, c.LOCK.UN))) {
        .SUCCESS => return,
        .INTR => {},
        .AGAIN => return recoverableOsBugDetected(), // unlocking can't block
        .BADF => return recoverableOsBugDetected(), // File descriptor used after closed.
        .INVAL => return recoverableOsBugDetected(), // invalid parameters
        .NOLCK => return recoverableOsBugDetected(), // Resource deallocation.
        .OPNOTSUPP => return recoverableOsBugDetected(), // We already got the lock.
        else => return recoverableOsBugDetected(), // Resource deallocation must succeed.
    };
}

fn fileDowngradeLock(userdata: ?*anyopaque, file: File) File.DowngradeLockError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const operation = c.LOCK.SH | c.LOCK.NB;
    while (true) switch (c.errno(c.flock(file.handle, operation))) {
        .SUCCESS => return,
        .INTR => {},
        .AGAIN => |err| return errnoBug(err), // File was not locked in exclusive mode.
        .BADF => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err), // invalid parameters
        .NOLCK => |err| return errnoBug(err), // Lock already obtained.
        .OPNOTSUPP => |err| return errnoBug(err), // Lock already obtained.
        else => |err| return unexpectedErrno(err),
    };
}

fn fileRealPath(userdata: ?*anyopaque, file: File, out_buffer: []u8) File.RealPathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    var buffer: [c.PATH_MAX]u8 = undefined;
    @memset(&buffer, 0);
    while (true) {
        switch (c.errno(c.fcntl(file.handle, c.F.GETPATH, &buffer))) {
            .SUCCESS => break,
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .BADF => return error.FileNotFound,
            .NOENT => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOSPC => return error.NameTooLong,
            .RANGE => return error.NameTooLong,
            else => |err| return unexpectedErrno(err),
        }
    }
    const n = std.mem.indexOfScalar(u8, &buffer, 0) orelse buffer.len;
    if (n > out_buffer.len) return error.NameTooLong;
    @memcpy(out_buffer[0..n], buffer[0..n]);
    return n;
}

fn fileHardLink(
    userdata: ?*anyopaque,
    file: File,
    new_dir: Dir,
    new_sub_path: []const u8,
    options: File.HardLinkOptions,
) File.HardLinkError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = file;
    _ = new_dir;
    _ = new_sub_path;
    _ = options;
    return error.OperationUnsupported;
}

fn linkat(
    old_dir: c.fd_t,
    old_path: [*:0]const u8,
    new_dir: c.fd_t,
    new_path: [*:0]const u8,
    flags: u32,
) File.HardLinkError!void {
    while (true) switch (c.errno(c.linkat(old_dir, old_path, new_dir, new_path, flags))) {
        .SUCCESS => return,
        .INTR => {},
        .ACCES => return error.AccessDenied,
        .DQUOT => return error.DiskQuota,
        .EXIST => return error.PathAlreadyExists,
        .IO => return error.HardwareFailure,
        .LOOP => return error.SymLinkLoop,
        .MLINK => return error.LinkQuotaExceeded,
        .NAMETOOLONG => return error.NameTooLong,
        .NOENT => return error.FileNotFound,
        .NOMEM => return error.SystemResources,
        .NOSPC => return error.NoSpaceLeft,
        .NOTDIR => return error.NotDir,
        .PERM => return error.PermissionDenied,
        .ROFS => return error.ReadOnlyFileSystem,
        .XDEV => return error.CrossDevice,
        .ILSEQ => return error.BadPathName,
        .FAULT => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err),
        else => |err| return unexpectedErrno(err),
    };
}

fn fileMemoryMapCreate(
    userdata: ?*anyopaque,
    file: File,
    options: File.MemoryMap.CreateOptions,
) File.MemoryMap.CreateError!File.MemoryMap {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    const prot: c.PROT = .{
        .READ = options.protection.read,
        .WRITE = options.protection.write,
        .EXEC = options.protection.execute,
    };
    const flags: c.MAP = .{
        .TYPE = .SHARED,
    };

    const page_align = std.heap.page_size_min;

    const contents = while (true) {
        const casted_offset = std.math.cast(i64, options.offset) orelse return error.Unseekable;
        const rc = c.mmap(null, options.len, prot, flags, file.handle, casted_offset);
        const err: c.E = if (rc != c.MAP_FAILED) .SUCCESS else @enumFromInt(c._errno().*);
        switch (err) {
            .SUCCESS => break @as([*]align(page_align) u8, @ptrCast(@alignCast(rc)))[0..options.len],
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .AGAIN => return error.LockedMemoryLimitExceeded,
            .MFILE => return error.ProcessFdQuotaExceeded,
            .NFILE => return error.SystemFdQuotaExceeded,
            .NOMEM => return error.OutOfMemory,
            .PERM => return error.PermissionDenied,
            .OVERFLOW => return error.Unseekable,
            .BADF => return errnoBug(err), // Always a race condition.
            .INVAL => return errnoBug(err), // Invalid parameters to mmap()
            else => return unexpectedErrno(err),
        }
    };
    return .{
        .file = file,
        .offset = options.offset,
        .memory = contents,
        .section = {},
    };
}

fn fileMemoryMapDestroy(userdata: ?*anyopaque, mm: *File.MemoryMap) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const memory = mm.memory;
    if (memory.len == 0) return;
    switch (c.errno(c.munmap(memory.ptr, memory.len))) {
        .SUCCESS => {},
        else => |err| if (builtin.mode == .Debug)
            std.log.err("failed to unmap {d} bytes at {*}: {t}", .{ memory.len, memory.ptr, err }),
    }
    mm.* = undefined;
}

fn fileMemoryMapSetLength(
    userdata: ?*anyopaque,
    mm: *File.MemoryMap,
    new_len: usize,
) File.MemoryMap.SetLengthError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    const page_size = std.heap.pageSize();
    const alignment: Alignment = .fromByteUnits(page_size);
    const old_memory = mm.memory;

    if (alignment.forward(new_len) == alignment.forward(old_memory.len)) {
        mm.memory.len = new_len;
        return;
    }
    return error.OperationUnsupported;
}

fn fileMemoryMapRead(userdata: ?*anyopaque, mm: *File.MemoryMap) File.ReadPositionalError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = mm;
}

fn fileMemoryMapWrite(userdata: ?*anyopaque, mm: *File.MemoryMap) File.WritePositionalError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = mm;
}

fn processExecutableOpen(
    userdata: ?*anyopaque,
    flags: File.OpenFlags,
) process.OpenExecutableError!File {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    // _NSGetExecutablePath() returns a path that might be a symlink to
    // the executable. Here it does not matter since we open it.
    var symlink_path_buf: [c.PATH_MAX + 1]u8 = undefined;
    var n: u32 = symlink_path_buf.len;
    const rc = c._NSGetExecutablePath(&symlink_path_buf, &n);
    if (rc != 0) return error.NameTooLong;
    const symlink_path = std.mem.sliceTo(&symlink_path_buf, 0);
    return dirOpenFile(ev, .cwd(), symlink_path, flags);
}

fn processExecutablePath(userdata: ?*anyopaque, out_buffer: []u8) process.ExecutablePathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    // _NSGetExecutablePath() returns a path that might be a symlink to
    // the executable.
    var symlink_path_buf: [c.PATH_MAX + 1]u8 = undefined;
    var n: u32 = symlink_path_buf.len;
    const rc = c._NSGetExecutablePath(&symlink_path_buf, &n);
    if (rc != 0) return error.NameTooLong;
    const symlink_path = std.mem.sliceTo(&symlink_path_buf, 0);
    assert(Dir.path.isAbsolute(symlink_path));
    return dirRealPathFile(ev, .cwd(), symlink_path, out_buffer) catch |err| switch (err) {
        error.NetworkNotFound => unreachable, // Windows-only
        error.FileBusy => unreachable, // Windows-only
        else => |e| return e,
    };
}

fn lockStderr(userdata: ?*anyopaque, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!Io.LockedStderr {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    try ev.stderr_mutex.lock(ev);
    errdefer ev.stderr_mutex.unlock();
    return ev.initLockedStderr(terminal_mode);
}

fn tryLockStderr(
    userdata: ?*anyopaque,
    terminal_mode: ?Io.Terminal.Mode,
) Io.Cancelable!?Io.LockedStderr {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (!ev.stderr_mutex.tryLock()) return null;
    errdefer ev.stderr_mutex.unlock();
    return try ev.initLockedStderr(terminal_mode);
}

fn initLockedStderr(ev: *Evented, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!Io.LockedStderr {
    ev.init_stderr_writer.once(ev, &initStderrWriter);
    return .{
        .file_writer = &ev.stderr_writer,
        .terminal_mode = terminal_mode orelse ev.stderr_mode,
    };
}

fn initStderrWriter(context: ?*anyopaque) callconv(.c) void {
    const ev: *Evented = @ptrCast(@alignCast(context));
    const cancel_protection = swapCancelProtection(ev, .blocked);
    defer assert(swapCancelProtection(ev, cancel_protection) == .blocked);
    ev.scan_environ.once(ev, &scanEnviron);
    const NO_COLOR = ev.environ.exist.NO_COLOR;
    const CLICOLOR_FORCE = ev.environ.exist.CLICOLOR_FORCE;
    ev.stderr_mode = Io.Terminal.Mode.detect(
        ev.io(),
        ev.stderr_writer.file,
        NO_COLOR,
        CLICOLOR_FORCE,
    ) catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    };
}

fn unlockStderr(userdata: ?*anyopaque) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (ev.stderr_writer.err == null) ev.stderr_writer.interface.flush() catch {};
    if (ev.stderr_writer.err) |err| {
        switch (err) {
            error.Canceled => Thread.current().currentFiber().cancel_protection.recancel(),
            else => {},
        }
        ev.stderr_writer.err = null;
    }
    ev.stderr_writer.interface.end = 0;
    ev.stderr_writer.interface.buffer.len = 0;
    ev.stderr_mutex.unlock();
}

fn processCurrentPath(userdata: ?*anyopaque, buffer: []u8) process.CurrentPathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const err: c.E = if (c.getcwd(buffer.ptr, buffer.len)) |_| .SUCCESS else @enumFromInt(c._errno().*);
    switch (err) {
        .SUCCESS => return std.mem.findScalar(u8, buffer, 0).?,
        .NOENT => return error.CurrentDirUnlinked,
        .RANGE => return error.NameTooLong,
        .FAULT => |e| return errnoBug(e),
        .INVAL => |e| return errnoBug(e),
        else => return unexpectedErrno(err),
    }
}

fn processSetCurrentDir(userdata: ?*anyopaque, dir: Dir) process.SetCurrentDirError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    if (dir.handle == c.AT.FDCWD) return;
    while (true) switch (c.errno(c.fchdir(dir.handle))) {
        .SUCCESS => return,
        .INTR => {},
        .ACCES => return error.AccessDenied,
        .NOTDIR => return error.NotDir,
        .IO => return error.FileSystem,
        .BADF => |err| return errnoBug(err),
        else => |err| return unexpectedErrno(err),
    };
}

fn processSetCurrentPath(userdata: ?*anyopaque, dir_path: []const u8) process.SetCurrentPathError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const dir_path_posix = try pathToPosix(dir_path, &path_buffer);
    while (true) switch (c.errno(c.chdir(dir_path_posix))) {
        .SUCCESS => return,
        .INTR => {},
        .ACCES => return error.AccessDenied,
        .IO => return error.FileSystem,
        .LOOP => return error.SymLinkLoop,
        .NAMETOOLONG => return error.NameTooLong,
        .NOENT => return error.FileNotFound,
        .NOMEM => return error.SystemResources,
        .NOTDIR => return error.NotDir,
        .ILSEQ => return error.BadPathName,
        .FAULT => |err| return errnoBug(err),
        else => |err| return unexpectedErrno(err),
    };
}

fn processReplace(userdata: ?*anyopaque, options: process.ReplaceOptions) process.ReplaceError {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    if (!process.can_replace) return error.OperationUnsupported;

    ev.scan_environ.once(ev, &scanEnviron); // for PATH
    const PATH = ev.environ.string.PATH orelse default_PATH;

    var arena_allocator = std.heap.ArenaAllocator.init(ev.allocator());
    defer arena_allocator.deinit();
    const arena = arena_allocator.allocator();

    const argv_buf = try arena.allocSentinel(?[*:0]const u8, options.argv.len, null);
    for (options.argv, 0..) |arg, i| argv_buf[i] = (try arena.dupeZ(u8, arg)).ptr;

    const env_block = env_block: {
        const prog_fd: i32 = -1;
        if (options.environ_map) |environ_map| break :env_block try environ_map.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
        break :env_block try ev.environ.process_environ.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
    };

    return ev.execv(options.expand_arg0, argv_buf.ptr[0].?, argv_buf.ptr, env_block, PATH);
}

fn processReplacePath(
    userdata: ?*anyopaque,
    dir: Dir,
    options: process.ReplaceOptions,
) process.ReplaceError {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = dir;
    _ = options;
    @panic("TODO processReplacePath");
}

fn processSpawn(userdata: ?*anyopaque, options: process.SpawnOptions) process.SpawnError!process.Child {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const spawned = try ev.spawn(options);
    defer fileClose(ev, &.{spawned.err_pipe});

    // Wait for the child to report any errors in or before `execvpe`.
    var child_err: ForkBailError = undefined;
    ev.readAll(spawned.err_pipe, @ptrCast(&child_err)) catch |read_err| {
        switch (read_err) {
            error.Canceled => unreachable, // blocked
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
    };
    return child_err;
}

fn processSpawnPath(
    userdata: ?*anyopaque,
    dir: Dir,
    options: process.SpawnOptions,
) process.SpawnError!process.Child {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = dir;
    _ = options;
    @panic("TODO processSpawnPath");
}

const prog_fileno = @max(c.STDIN_FILENO, c.STDOUT_FILENO, c.STDERR_FILENO) + 1;

const Spawned = struct {
    pid: c.pid_t,
    err_pipe: File,
    stdin: ?File,
    stdout: ?File,
    stderr: ?File,
};
fn spawn(ev: *Evented, options: process.SpawnOptions) process.SpawnError!Spawned {
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
    const pipe_flags: c.O = .{ .CLOEXEC = true };

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

    const any_ignore =
        options.stdin == .ignore or options.stdout == .ignore or options.stderr == .ignore;
    const dev_null_file = if (any_ignore) dev_null_file: {
        ev.open_dev_null.once(ev, &openDevNullFile);
        break :dev_null_file try ev.dev_null_file;
    } else undefined;

    const prog_pipe: [2]c.fd_t = if (options.progress_node.index != .none)
        // We use CLOEXEC for the same reason as in `pipe_flags`.
        try pipe2(.{ .NONBLOCK = true, .CLOEXEC = true })
    else
        .{ -1, -1 };
    errdefer destroyPipe(prog_pipe);

    var arena_allocator = std.heap.ArenaAllocator.init(ev.allocator());
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

    const env_block = env_block: {
        const prog_fd: i32 = if (prog_pipe[1] == -1) -1 else prog_fileno;
        if (options.environ_map) |environ_map| break :env_block try environ_map.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
        break :env_block try ev.environ.process_environ.createPosixBlock(arena, .{
            .zig_progress_fd = prog_fd,
        });
    };

    // This pipe communicates to the parent errors in the child between `fork` and `execvpe`.
    // It is closed by the child (via CLOEXEC) without writing if `execvpe` succeeds.
    const err_pipe: [2]File = err_pipe: {
        const err_pipe = try pipe2(.{ .CLOEXEC = true });
        break :err_pipe .{
            .{ .handle = err_pipe[0], .flags = .{ .nonblocking = false } },
            .{ .handle = err_pipe[1], .flags = .{ .nonblocking = false } },
        };
    };
    errdefer fileClose(ev, &err_pipe);

    ev.scan_environ.once(ev, &scanEnviron); // for PATH
    const PATH = ev.environ.string.PATH orelse default_PATH;

    const pid_result: c.pid_t = fork: {
        const rc = c.fork();
        switch (c.errno(rc)) {
            .SUCCESS => break :fork @intCast(rc),
            .AGAIN => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .NOSYS => return error.OperationUnsupported,
            else => |err| return unexpectedErrno(err),
        }
    };

    if (pid_result == 0) {
        defer comptime unreachable; // We are the child.
        const err = ev.setUpChild(.{
            .stdin_pipe = stdin_pipe[0],
            .stdout_pipe = stdout_pipe[1],
            .stderr_pipe = stderr_pipe[1],
            .dev_null_fd = dev_null_file.handle,
            .prog_pipe = prog_pipe[1],
            .argv_buf = argv_buf,
            .env_block = env_block,
            .PATH = PATH,
            .spawn = options,
        });
        ev.writeAll(err_pipe[1], @ptrCast(&err)) catch {};
        c.exit(1);
    }

    const pid: c.pid_t = @intCast(pid_result); // We are the parent.
    errdefer comptime unreachable; // The child is forked; we must not error from now on

    fileClose(ev, err_pipe[1..2]); // make sure only the child holds the write end open

    if (options.stdin == .pipe) closeFd(stdin_pipe[0]);
    if (options.stdout == .pipe) closeFd(stdout_pipe[1]);
    if (options.stderr == .pipe) closeFd(stderr_pipe[1]);

    if (prog_pipe[1] != -1) closeFd(prog_pipe[1]);

    options.progress_node.setIpcFile(ev, .{ .handle = prog_pipe[0], .flags = .{ .nonblocking = true } });

    return .{
        .pid = pid,
        .err_pipe = err_pipe[0],
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

fn openDevNullFile(context: ?*anyopaque) callconv(.c) void {
    const ev: *Evented = @ptrCast(@alignCast(context));
    ev.dev_null_file = dirOpenFile(ev, .cwd(), "/dev/null", .{ .mode = .read_write });
}

/// Errors that can occur between fork() and execv()
const ForkBailError = process.SetCurrentDirError || ChdirError ||
    process.SpawnError || process.ReplaceError;
fn setUpChild(ev: *Evented, options: struct {
    stdin_pipe: c.fd_t,
    stdout_pipe: c.fd_t,
    stderr_pipe: c.fd_t,
    dev_null_fd: c.fd_t,
    prog_pipe: c.fd_t,
    argv_buf: [:null]?[*:0]const u8,
    env_block: process.Environ.Block,
    PATH: []const u8,
    spawn: process.SpawnOptions,
}) ForkBailError {
    try ev.setUpChildIo(
        options.spawn.stdin,
        options.stdin_pipe,
        c.STDIN_FILENO,
        options.dev_null_fd,
    );
    try ev.setUpChildIo(
        options.spawn.stdout,
        options.stdout_pipe,
        c.STDOUT_FILENO,
        options.dev_null_fd,
    );
    try ev.setUpChildIo(
        options.spawn.stderr,
        options.stderr_pipe,
        c.STDERR_FILENO,
        options.dev_null_fd,
    );

    switch (options.spawn.cwd) {
        .inherit => {},
        .dir => |cwd_dir| try processSetCurrentDir(ev, cwd_dir),
        .path => |cwd_path| try processSetCurrentPath(ev, cwd_path),
    }

    // Must happen after fchdir above, the cwd file descriptor might be
    // equal to prog_fileno and be clobbered by this dup2 call.
    if (options.prog_pipe != -1) try ev.dup2(options.prog_pipe, prog_fileno);

    if (options.spawn.gid) |gid| while (true) switch (c.errno(c.setregid(gid, gid))) {
        .SUCCESS => break,
        .INTR => {},
        .AGAIN => return error.ResourceLimitReached,
        .INVAL => return error.InvalidUserId,
        .PERM => return error.PermissionDenied,
        else => return error.Unexpected,
    };

    if (options.spawn.uid) |uid| while (true) switch (c.errno(c.setreuid(uid, uid))) {
        .SUCCESS => break,
        .INTR => {},
        .AGAIN => return error.ResourceLimitReached,
        .INVAL => return error.InvalidUserId,
        .PERM => return error.PermissionDenied,
        else => return error.Unexpected,
    };

    if (options.spawn.pgid) |pid| while (true) switch (c.errno(c.setpgid(0, pid))) {
        .SUCCESS => break,
        .INTR => {},
        .ACCES => return error.ProcessAlreadyExec,
        .INVAL => return error.InvalidProcessGroupId,
        .PERM => return error.PermissionDenied,
        else => return error.Unexpected,
    };

    if (options.spawn.start_suspended) while (true) switch (c.errno(c.kill(0, .STOP))) {
        .SUCCESS => break,
        .INTR => {},
        .PERM => return error.PermissionDenied,
        else => return error.Unexpected,
    };

    return ev.execv(
        options.spawn.expand_arg0,
        options.argv_buf.ptr[0].?,
        options.argv_buf.ptr,
        options.env_block,
        options.PATH,
    );
}

fn setUpChildIo(
    ev: *Evented,
    stdio: process.SpawnOptions.StdIo,
    pipe_fd: c.fd_t,
    std_fileno: i32,
    dev_null_fd: c.fd_t,
) !void {
    switch (stdio) {
        .pipe => try ev.dup2(pipe_fd, std_fileno),
        .close => closeFd(std_fileno),
        .inherit => {},
        .ignore => try ev.dup2(dev_null_fd, std_fileno),
        .file => |file| try ev.dup2(file.handle, std_fileno),
    }
}

const PipeError = error{
    SystemFdQuotaExceeded,
    ProcessFdQuotaExceeded,
} || Io.UnexpectedError;

fn pipe2(flags: c.O) PipeError![2]c.fd_t {
    var fds: [2]c.fd_t = undefined;

    while (true) switch (c.errno(c.pipe(&fds))) {
        .SUCCESS => break,
        .INTR => {},
        .NFILE => return error.SystemFdQuotaExceeded,
        .MFILE => return error.ProcessFdQuotaExceeded,
        else => |err| return unexpectedErrno(err),
    };
    errdefer {
        closeFd(fds[0]);
        closeFd(fds[1]);
    }

    // https://github.com/ziglang/zig/issues/18882
    if (@as(u32, @bitCast(flags)) == 0) return fds;

    // CLOEXEC is special, it's a file descriptor flag and must be set using
    // F.SETFD.
    if (flags.CLOEXEC) for (fds) |fd| while (true) switch (c.errno(c.fcntl(fd, c.F.SETFD, @as(u32, c.FD_CLOEXEC)))) {
        .SUCCESS => break,
        .INTR => {},
        else => |err| return unexpectedErrno(err),
    };

    const new_flags: u32 = f: {
        var new_flags = flags;
        new_flags.CLOEXEC = false;
        break :f @bitCast(new_flags);
    };

    // Set every other flag affecting the file status using F.SETFL.
    if (new_flags != 0) for (fds) |fd| while (true) switch (c.errno(c.fcntl(fd, c.F.SETFL, new_flags))) {
        .SUCCESS => break,
        .INTR => {},
        .INVAL => |err| return errnoBug(err),
        else => |err| return unexpectedErrno(err),
    };

    return fds;
}

fn destroyPipe(pipe: [2]c.fd_t) void {
    if (pipe[0] != -1) closeFd(pipe[0]);
    if (pipe[0] != pipe[1]) closeFd(pipe[1]);
}

const DupError = error{
    ProcessFdQuotaExceeded,
    SystemResources,
} || Io.UnexpectedError || Io.Cancelable;
fn dup2(ev: *Evented, old_fd: c.fd_t, new_fd: c.fd_t) DupError!void {
    _ = ev;
    while (true) switch (c.errno(c.dup2(old_fd, new_fd))) {
        .SUCCESS => return,
        .BUSY, .INTR => {},
        .INVAL => |err| return errnoBug(err), // invalid parameters
        .BADF => |err| return errnoBug(err), // use after free
        .MFILE => return error.ProcessFdQuotaExceeded,
        .NOMEM => return error.SystemResources,
        else => |err| return unexpectedErrno(err),
    };
}

fn execv(
    ev: *Evented,
    arg0_expand: process.ArgExpansion,
    file: [*:0]const u8,
    child_argv: [*:null]?[*:0]const u8,
    env_block: process.Environ.PosixBlock,
    PATH: []const u8,
) process.ReplaceError {
    const file_slice = std.mem.sliceTo(file, 0);
    if (std.mem.findScalar(u8, file_slice, '/') != null) return ev.execvPath(file, child_argv, env_block);

    // Use of PATH_MAX here is valid as the path_buf will be passed
    // directly to the operating system in posixExecvPath.
    var path_buf: [c.PATH_MAX]u8 = undefined;
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
        err = ev.execvPath(full_path, child_argv, env_block);
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
fn execvPath(
    ev: *Evented,
    path: [*:0]const u8,
    child_argv: [*:null]const ?[*:0]const u8,
    env_block: process.Environ.PosixBlock,
) process.ReplaceError {
    _ = ev;
    switch (c.errno(c.execve(path, child_argv, env_block.slice.ptr))) {
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
        .BADEXEC => return error.InvalidExe,
        .BADARCH => return error.InvalidExe,
        else => |err| return unexpectedErrno(err),
    }
}

fn childWait(userdata: ?*anyopaque, child: *process.Child) process.Child.WaitError!process.Child.Term {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    defer ev.childCleanup(child);
    const pid = child.id.?;
    const source = c.dispatch.source_create(
        .PROC,
        @bitCast(@as(isize, pid)),
        .{ .PROC = .{ .EXIT = true } },
        ev.queue,
    ) orelse return error.Unexpected;
    source.as_object().set_context(Thread.current().currentFiber());
    source.set_event_handler(&Fiber.@"resume");
    ev.yield(.{ .activate = source.as_object() });
    source.as_object().release();
    var status: c_int = undefined;
    var ru: c.rusage = undefined;
    const ru_ptr = if (child.request_resource_usage_statistics) &ru else null;
    while (true) switch (c.errno(c.wait4(pid, &status, 0, ru_ptr))) {
        .SUCCESS => {
            if (ru_ptr) |p| child.resource_usage_statistics.rusage = p.*;
            return statusToTerm(@bitCast(status));
        },
        .INTR => {},
        .CHILD => |err| return errnoBug(err), // Double-free.
        else => |err| return unexpectedErrno(err),
    };
}

fn childKill(userdata: ?*anyopaque, child: *process.Child) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    defer ev.childCleanup(child);
    const pid = child.id.?;
    while (true) switch (c.errno(c.kill(pid, .TERM))) {
        .SUCCESS => break,
        .INTR => {},
        .PERM => return,
        .INVAL => |err| errnoBug(err) catch return,
        .SRCH => |err| errnoBug(err) catch return,
        else => |err| unexpectedErrno(err) catch return,
    };
    var status: c_int = undefined;
    while (true) switch (c.errno(c.wait4(pid, &status, 0, null))) {
        .SUCCESS => return,
        .INTR => {},
        .CHILD => |err| errnoBug(err) catch return, // Double-free.
        else => |err| unexpectedErrno(err) catch return,
    };
}

fn childCleanup(ev: *Evented, child: *process.Child) void {
    if (child.stdin) |stdin| {
        fileClose(ev, &.{stdin});
        child.stdin = null;
    }
    if (child.stdout) |stdout| {
        fileClose(ev, &.{stdout});
        child.stdout = null;
    }
    if (child.stderr) |stderr| {
        fileClose(ev, &.{stderr});
        child.stderr = null;
    }
    child.id = null;
}

fn progressParentFile(userdata: ?*anyopaque) std.Progress.ParentFileError!File {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    ev.scan_environ.once(ev, &scanEnviron);
    return ev.environ.zig_progress_file;
}

fn scanEnviron(context: ?*anyopaque) callconv(.c) void {
    const ev: *Evented = @ptrCast(@alignCast(context));
    ev.environ.scan(ev.allocator());
}

fn now(userdata: ?*anyopaque, clock: Io.Clock) Io.Timestamp {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const clock_id: c.clockid_t = clockToPosix(clock);
    var timespec: c.timespec = undefined;
    switch (c.errno(c.clock_gettime(clock_id, &timespec))) {
        .SUCCESS => return timestampFromPosix(&timespec),
        else => return .zero,
    }
}

fn clockResolution(userdata: ?*anyopaque, clock: Io.Clock) Io.Clock.ResolutionError!Io.Duration {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const clock_id: c.clockid_t = clockToPosix(clock);
    var timespec: c.timespec = undefined;
    return switch (c.errno(c.clock_getres(clock_id, &timespec))) {
        .SUCCESS => .fromNanoseconds(nanosecondsFromPosix(&timespec)),
        .INVAL => return error.ClockUnavailable,
        else => |err| return unexpectedErrno(err),
    };
}

const SleepWaiter = struct {
    sleeper: Sleeper = undefined,
    cancelable: Cancelable,
    timer: c.dispatch.source_t,
    started: bool = false,

    fn start(context: ?*anyopaque) callconv(.c) void {
        const waiter: *SleepWaiter = @ptrCast(@alignCast(context));
        waiter.cancelable.enter(waiter.sleeper.fiber) catch |err| switch (err) {
            error.CancelRequested => waiter.timer.cancel(),
        };
        waiter.timer.as_object().activate();
    }

    fn timedOut(context: ?*anyopaque) callconv(.c) void {
        const waiter: *SleepWaiter = @ptrCast(@alignCast(context));
        waiter.cancelable.leave(waiter.sleeper.fiber) catch |err| switch (err) {
            error.CancelRequested => return,
        };
        waiter.timer.cancel();
    }

    fn canceled(context: ?*anyopaque) callconv(.c) void {
        const cancelable: *Cancelable = @ptrCast(@alignCast(context));
        const waiter: *SleepWaiter = @fieldParentPtr("cancelable", cancelable);
        cancelable.requested(waiter.sleeper.fiber);
        waiter.timer.cancel();
    }

    fn wake(context: ?*anyopaque) callconv(.c) void {
        const waiter: *SleepWaiter = @ptrCast(@alignCast(context));
        var sleeper = waiter.sleeper;
        waiter.* = undefined;
        Sleeper.wake(&sleeper);
    }
};

fn sleep(userdata: ?*anyopaque, timeout: Io.Timeout) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const queue = c.dispatch.queue_create_with_target(
        "org.ziglang.std.Io.Dispatch.sleep",
        .SERIAL(),
        ev.queue,
    ) orelse {
        log.warn("failed to create serial queue for sleep", .{});
        return ev.yield(.{ .after = ev.timeFromTimeout(timeout) });
    };
    defer queue.as_object().release();
    const timer = c.dispatch.source_create(.TIMER, 0, .none, queue) orelse {
        log.warn("failed to create timer for sleep", .{});
        return ev.yield(.{ .after = ev.timeFromTimeout(timeout) });
    };
    var waiter: SleepWaiter = .{
        .cancelable = .{ .queue = queue, .cancel = &Futex.Waiter.canceled },
        .timer = timer,
    };
    timer.as_object().set_context(&waiter);
    timer.set_event_handler(&SleepWaiter.timedOut);
    timer.set_cancel_handler(&SleepWaiter.wake);
    timer.set_timer(ev.timeFromTimeout(timeout), c.dispatch.TIME_FOREVER, ev.leeway);
    ev.yield(.{ .sleep_wait = &waiter });
    timer.as_object().release();
    try waiter.cancelable.acknowledge(waiter.sleeper.fiber);
}

fn timeFromTimeout(ev: *Evented, timeout: Io.Timeout) c.dispatch.time_t {
    return timeout: switch (timeout) {
        .none => .FOREVER,
        .duration => |duration| .time(switch (duration.clock) {
            .real => .WALL_NOW,
            else => .NOW,
        }, std.math.lossyCast(i64, duration.raw.toNanoseconds())),
        .deadline => |deadline| switch (deadline.clock) {
            .real => .walltime(&.{
                .sec = @intCast(@divFloor(deadline.raw.toNanoseconds(), std.time.ns_per_s)),
                .nsec = @intCast(@mod(deadline.raw.toNanoseconds(), std.time.ns_per_s)),
            }, 0),
            else => continue :timeout .{ .duration = deadline.durationFromNow(ev.io()) },
        },
    };
}

const Random = struct {
    evented: *Evented,
    thread: *Thread,
    buffer: []u8,

    fn seed(context: ?*anyopaque) callconv(.c) void {
        const rand: *Random = @ptrCast(@alignCast(context));
        const ev = rand.evented;
        ev.csprng_mutex.lockUncancelable(ev);
        defer ev.csprng_mutex.unlock();
        var buffer: [Csprng.seed_len]u8 = undefined;
        if (!ev.csprng.isInitialized()) {
            @branchHint(.unlikely);
            const cancel_protection = swapCancelProtection(ev, .blocked);
            defer assert(swapCancelProtection(ev, cancel_protection) == .blocked);
            randomSecure(ev, &buffer) catch |err| switch (err) {
                error.Canceled => unreachable, // blocked
                error.EntropyUnavailable => fallbackSeed(ev, &buffer),
            };
            ev.csprng.rng = .init(buffer);
        }
        ev.csprng.rng.fill(&buffer);
        rand.thread.csprng.rng = .init(buffer);
        rand.thread.csprng.rng.fill(rand.buffer);
        rand.buffer.len = 0;
    }
};

fn random(userdata: ?*anyopaque, buffer: []u8) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (buffer.len == 0) return;
    const thread: *Thread = .current();
    var rand: Random = .{ .evented = ev, .thread = thread, .buffer = buffer };
    thread.seed_csprng.once(&rand, &Random.seed);
    if (rand.buffer.len > 0) thread.csprng.rng.fill(buffer);
}

fn randomSecure(userdata: ?*anyopaque, buffer: []u8) Io.RandomSecureError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    if (buffer.len > 0) c.arc4random_buf(buffer.ptr, buffer.len);
}

fn netListenIpUnavailable(
    userdata: ?*anyopaque,
    address: *const net.IpAddress,
    options: net.IpAddress.ListenOptions,
) net.IpAddress.ListenError!net.Socket {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

fn netAcceptUnavailable(
    userdata: ?*anyopaque,
    listen_handle: net.Socket.Handle,
    options: net.Server.AcceptOptions,
) net.Server.AcceptError!net.Socket {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = listen_handle;
    _ = options;
    return error.NetworkDown;
}

fn netBindIpUnavailable(
    userdata: ?*anyopaque,
    address: *const net.IpAddress,
    options: net.IpAddress.BindOptions,
) net.IpAddress.BindError!net.Socket {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

fn netConnectIpUnavailable(
    userdata: ?*anyopaque,
    address: *const net.IpAddress,
    options: net.IpAddress.ConnectOptions,
) net.IpAddress.ConnectError!net.Socket {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

fn netListenUnixUnavailable(
    userdata: ?*anyopaque,
    address: *const net.UnixAddress,
    options: net.UnixAddress.ListenOptions,
) net.UnixAddress.ListenError!net.Socket.Handle {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = address;
    _ = options;
    return error.AddressFamilyUnsupported;
}

fn netConnectUnixUnavailable(
    userdata: ?*anyopaque,
    address: *const net.UnixAddress,
) net.UnixAddress.ConnectError!net.Socket.Handle {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = address;
    return error.AddressFamilyUnsupported;
}

fn netSocketCreatePairUnavailable(
    userdata: ?*anyopaque,
    options: net.Socket.CreatePairOptions,
) net.Socket.CreatePairError![2]net.Socket {
    _ = userdata;
    _ = options;
    return error.OperationUnsupported;
}

fn netSendUnavailable(
    userdata: ?*anyopaque,
    handle: net.Socket.Handle,
    messages: []net.OutgoingMessage,
    flags: net.SendFlags,
) struct { ?net.Socket.SendError, usize } {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = handle;
    _ = messages;
    _ = flags;
    return .{ error.NetworkDown, 0 };
}

fn netReadUnavailable(
    userdata: ?*anyopaque,
    fd: net.Socket.Handle,
    data: [][]u8,
) net.Stream.Reader.Error!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = fd;
    _ = data;
    return error.NetworkDown;
}

fn netWriteUnavailable(
    userdata: ?*anyopaque,
    handle: net.Socket.Handle,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
) net.Stream.Writer.Error!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = handle;
    _ = header;
    _ = data;
    _ = splat;
    return error.NetworkDown;
}

fn netWriteFileUnavailable(
    userdata: ?*anyopaque,
    socket_handle: net.Socket.Handle,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
) net.Stream.Writer.WriteFileError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = socket_handle;
    _ = header;
    _ = file_reader;
    _ = limit;
    return error.NetworkDown;
}

fn netClose(userdata: ?*anyopaque, handles: []const net.Socket.Handle) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    for (handles) |handle| closeFd(handle);
}

fn netShutdownUnavailable(
    userdata: ?*anyopaque,
    handle: net.Socket.Handle,
    how: net.ShutdownHow,
) net.ShutdownError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = handle;
    _ = how;
    unreachable; // How you gonna shutdown something that was impossible to open?
}

fn netInterfaceNameResolveUnavailable(
    userdata: ?*anyopaque,
    name: *const net.Interface.Name,
) net.Interface.Name.ResolveError!net.Interface {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = name;
    return error.InterfaceNotFound;
}

fn netInterfaceNameUnavailable(
    userdata: ?*anyopaque,
    interface: net.Interface,
) net.Interface.NameError!net.Interface.Name {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = interface;
    return error.Unexpected;
}

fn netLookupUnavailable(
    userdata: ?*anyopaque,
    host_name: net.HostName,
    resolved: *Io.Queue(net.HostName.LookupResult),
    options: net.HostName.LookupOptions,
) net.HostName.LookupError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = host_name;
    _ = options;
    resolved.close(ev.io());
    return error.NetworkDown;
}

fn readAll(ev: *Evented, file: File, buffer: []u8) File.ReadStreamingError!void {
    var index: usize = 0;
    while (buffer.len - index != 0) {
        const len = try ev.fileReadStreaming(file, &.{buffer[index..]});
        if (len == 0) return error.EndOfStream;
        index += len;
    }
}

fn writeAll(ev: *Evented, file: File, buffer: []const u8) (File.Writer.Error || error{EndOfStream})!void {
    var index: usize = 0;
    while (buffer.len - index != 0) {
        const len = try ev.fileWriteStreaming(file, &.{}, &.{buffer[index..]}, 1);
        if (len == 0) return error.EndOfStream;
        index += len;
    }
}

/// This is either usize or u32. Since, either is fine, let's use the same
/// `addBuf` function for both writing to a file and sending network messages.
const iovlen_t = @FieldType(c.msghdr_const, "iovlen");

fn addConstBuf(v: []iovec_const, i: *iovlen_t, remaining: ?*usize, bytes: []const u8) void {
    if (v.len - i.* == 0) return;
    const len = @min(remaining.*, bytes.len);
    if (len == 0) return;
    v[i.*] = .{ .base = bytes.ptr, .len = len };
    i.* += 1;
    remaining.* -= len;
}
fn addBuf(
    comptime is_const: bool,
    vec: []if (is_const) iovec_const else iovec,
    vec_len: *iovlen_t,
    remaining: *Io.Limit,
    bytes: if (is_const) []const u8 else []u8,
) void {
    if (vec.len - vec_len.* == 0) return;
    const len = remaining.minInt(bytes.len);
    if (len == 0) return;
    vec[vec_len.*] = .{ .base = bytes.ptr, .len = len };
    vec_len.* += 1;
    remaining.* = remaining.subtract(len).?;
}

test {
    _ = Fiber.CancelProtection;
}



---
File: /std/Io/fiber.zig
---

pub const supported = switch (builtin.cpu.arch) {
    .aarch64, .riscv64, .x86_64 => true,
    else => false,
};

/// Stores the cpu state of an inactive fiber.
pub const Context = switch (builtin.cpu.arch) {
    .aarch64 => extern struct {
        sp: u64,
        fp: u64,
        pc: u64,
    },
    .riscv64 => extern struct {
        sp: u64,
        fp: u64,
        pc: u64,
    },
    .x86_64 => extern struct {
        rsp: u64,
        rbp: u64,
        rip: u64,
    },
    else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
};

pub const Switch = extern struct { old: *Context, new: *Context };

/// Fills `s.old` with the current cpu state, and restores the cpu state stored in `s.new`.
pub inline fn contextSwitch(s: *const Switch) *const Switch {
    return switch (builtin.cpu.arch) {
        .aarch64 => asm volatile (
            \\ ldp x0, x2, [x1]
            \\ ldr x3, [x2, #16]
            \\ mov x4, sp
            \\ stp x4, fp, [x0]
            \\ adr x5, 0f
            \\ ldp x4, fp, [x2]
            \\ str x5, [x0, #16]
            \\ mov sp, x4
            \\ br x3
            \\0:
            : [received_message] "={x1}" (-> *const Switch),
            : [message_to_send] "{x1}" (s),
            : .{
              .x0 = true,
              .x1 = true,
              .x2 = true,
              .x3 = true,
              .x4 = true,
              .x5 = true,
              .x6 = true,
              .x7 = true,
              .x8 = true,
              .x9 = true,
              .x10 = true,
              .x11 = true,
              .x12 = true,
              .x13 = true,
              .x14 = true,
              .x15 = true,
              .x16 = true,
              .x17 = true,
              .x19 = true,
              .x20 = true,
              .x21 = true,
              .x22 = true,
              .x23 = true,
              .x24 = true,
              .x25 = true,
              .x26 = true,
              .x27 = true,
              .x28 = true,
              .x30 = true,
              .z0 = true,
              .z1 = true,
              .z2 = true,
              .z3 = true,
              .z4 = true,
              .z5 = true,
              .z6 = true,
              .z7 = true,
              .z8 = true,
              .z9 = true,
              .z10 = true,
              .z11 = true,
              .z12 = true,
              .z13 = true,
              .z14 = true,
              .z15 = true,
              .z16 = true,
              .z17 = true,
              .z18 = true,
              .z19 = true,
              .z20 = true,
              .z21 = true,
              .z22 = true,
              .z23 = true,
              .z24 = true,
              .z25 = true,
              .z26 = true,
              .z27 = true,
              .z28 = true,
              .z29 = true,
              .z30 = true,
              .z31 = true,
              .p0 = true,
              .p1 = true,
              .p2 = true,
              .p3 = true,
              .p4 = true,
              .p5 = true,
              .p6 = true,
              .p7 = true,
              .p8 = true,
              .p9 = true,
              .p10 = true,
              .p11 = true,
              .p12 = true,
              .p13 = true,
              .p14 = true,
              .p15 = true,
              .fpcr = true,
              .fpsr = true,
              .ffr = true,
              .memory = true,
            }),
        .riscv64 => asm volatile (
            \\ ld a0, 0(a1)
            \\ ld a2, 8(a1)
            \\ lla a3, 0f
            \\ sd sp, 0(a0)
            \\ sd fp, 8(a0)
            \\ sd a3, 16(a0)
            \\ ld sp, 0(a2)
            \\ ld fp, 8(a2)
            \\ ld a3, 16(a2)
            \\ jr a3
            \\0:
            : [received_message] "={a1}" (-> *const Switch),
            : [message_to_send] "{a1}" (s),
            : .{
              .x1 = true,
              .x3 = true,
              .x4 = true,
              .x5 = true,
              .x6 = true,
              .x7 = true,
              .x9 = true,
              .x10 = true,
              .x11 = true,
              .x12 = true,
              .x13 = true,
              .x14 = true,
              .x15 = true,
              .x16 = true,
              .x17 = true,
              .x18 = true,
              .x19 = true,
              .x20 = true,
              .x21 = true,
              .x22 = true,
              .x23 = true,
              .x24 = true,
              .x25 = true,
              .x26 = true,
              .x27 = true,
              .x28 = true,
              .x29 = true,
              .x30 = true,
              .x31 = true,
              .f0 = true,
              .f1 = true,
              .f2 = true,
              .f3 = true,
              .f4 = true,
              .f5 = true,
              .f6 = true,
              .f7 = true,
              .f8 = true,
              .f9 = true,
              .f10 = true,
              .f11 = true,
              .f12 = true,
              .f13 = true,
              .f14 = true,
              .f15 = true,
              .f16 = true,
              .f17 = true,
              .f18 = true,
              .f19 = true,
              .f20 = true,
              .f21 = true,
              .f22 = true,
              .f23 = true,
              .f24 = true,
              .f25 = true,
              .f26 = true,
              .f27 = true,
              .f28 = true,
              .f29 = true,
              .f30 = true,
              .f31 = true,
              .v0 = true,
              .v1 = true,
              .v2 = true,
              .v3 = true,
              .v4 = true,
              .v5 = true,
              .v6 = true,
              .v7 = true,
              .v8 = true,
              .v9 = true,
              .v10 = true,
              .v11 = true,
              .v12 = true,
              .v13 = true,
              .v14 = true,
              .v15 = true,
              .v16 = true,
              .v17 = true,
              .v18 = true,
              .v19 = true,
              .v20 = true,
              .v21 = true,
              .v22 = true,
              .v23 = true,
              .v24 = true,
              .v25 = true,
              .v26 = true,
              .v27 = true,
              .v28 = true,
              .v29 = true,
              .v30 = true,
              .v31 = true,
              .vtype = true,
              .vl = true,
              .vxsat = true,
              .vxrm = true,
              .vcsr = true,
              .fflags = true,
              .frm = true,
              .memory = true,
            }),
        .x86_64 => asm volatile (
            \\ movq 0(%%rsi), %%rax
            \\ movq 8(%%rsi), %%rcx
            \\ leaq 0f(%%rip), %%rdx
            \\ movq %%rsp, 0(%%rax)
            \\ movq %%rbp, 8(%%rax)
            \\ movq %%rdx, 16(%%rax)
            \\ movq 0(%%rcx), %%rsp
            \\ movq 8(%%rcx), %%rbp
            \\ jmpq *16(%%rcx)
            \\0:
            : [received_message] "={rsi}" (-> *const Switch),
            : [message_to_send] "{rsi}" (s),
            : .{
              .rax = true,
              .rcx = true,
              .rdx = true,
              .rbx = true,
              .rsi = true,
              .rdi = true,
              .r8 = true,
              .r9 = true,
              .r10 = true,
              .r11 = true,
              .r12 = true,
              .r13 = true,
              .r14 = true,
              .r15 = true,
              .mm0 = true,
              .mm1 = true,
              .mm2 = true,
              .mm3 = true,
              .mm4 = true,
              .mm5 = true,
              .mm6 = true,
              .mm7 = true,
              .zmm0 = true,
              .zmm1 = true,
              .zmm2 = true,
              .zmm3 = true,
              .zmm4 = true,
              .zmm5 = true,
              .zmm6 = true,
              .zmm7 = true,
              .zmm8 = true,
              .zmm9 = true,
              .zmm10 = true,
              .zmm11 = true,
              .zmm12 = true,
              .zmm13 = true,
              .zmm14 = true,
              .zmm15 = true,
              .zmm16 = true,
              .zmm17 = true,
              .zmm18 = true,
              .zmm19 = true,
              .zmm20 = true,
              .zmm21 = true,
              .zmm22 = true,
              .zmm23 = true,
              .zmm24 = true,
              .zmm25 = true,
              .zmm26 = true,
              .zmm27 = true,
              .zmm28 = true,
              .zmm29 = true,
              .zmm30 = true,
              .zmm31 = true,
              .fpsr = true,
              .fpcr = true,
              .mxcsr = true,
              .rflags = true,
              .dirflag = true,
              .memory = true,
            }),
        else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
    };
}

const builtin = @import("builtin");



---
File: /std/Io/File.zig
---

const File = @This();

const builtin = @import("builtin");
const native_os = builtin.os.tag;
const is_windows = native_os == .windows;

const std = @import("../std.zig");
const Io = std.Io;
const assert = std.debug.assert;
const Dir = std.Io.Dir;

handle: Handle,
flags: Flags,

pub const Flags = struct {
    /// * true:
    ///   - windows: opened with MODE.IO.ASYNCHRONOUS
    ///   - POSIX: O_NONBLOCK is set
    /// * false:
    ///   - windows: opened with SYNCHRONOUS_ALERT or SYNCHRONOUS_NONALERT, or
    ///     not a file.
    ///   - POSIX: O_NONBLOCK is unset
    nonblocking: bool,
};

pub const Handle = std.posix.fd_t;

pub const Reader = @import("File/Reader.zig");
pub const Writer = @import("File/Writer.zig");
pub const Atomic = @import("File/Atomic.zig");
/// Memory intended to remain consistent with file contents.
pub const MemoryMap = @import("File/MemoryMap.zig");
/// Concurrently read from multiple file streams, eliminating risk of
/// deadlocking.
pub const MultiReader = @import("File/MultiReader.zig");

pub const INode = std.posix.ino_t;
pub const NLink = std.posix.nlink_t;
pub const Uid = std.posix.uid_t;
pub const Gid = std.posix.gid_t;
pub const BlockSize = u32;

pub const Kind = enum {
    block_device,
    character_device,
    directory,
    named_pipe,
    sym_link,
    file,
    unix_domain_socket,
    whiteout,
    door,
    event_port,
    unknown,
};

pub const Stat = struct {
    /// A number that the system uses to point to the file metadata. This
    /// number is not guaranteed to be unique across time, as some file
    /// systems may reuse an inode after its file has been deleted. Some
    /// systems may change the inode of a file over time.
    ///
    /// On Linux, the inode is a structure that stores the metadata, and
    /// the inode _number_ is what you see here: the index number of the
    /// inode.
    ///
    /// The FileIndex on Windows is similar. It is a number for a file that
    /// is unique to each filesystem.
    inode: INode,
    nlink: NLink,
    size: u64,
    permissions: Permissions,
    kind: Kind,
    /// Last access time in nanoseconds, relative to UTC 1970-01-01.
    ///
    /// Filesystems generally find this value problematic to keep updated since
    /// it turns read-only file system accesses into file system mutations.
    /// Some systems report stale values, and some systems explicitly refuse to
    /// report this value. The latter case is handled by `null`.
    atime: ?Io.Timestamp,
    /// Last modification time in nanoseconds, relative to UTC 1970-01-01.
    mtime: Io.Timestamp,
    /// Last status/metadata change time in nanoseconds, relative to UTC 1970-01-01.
    ctime: Io.Timestamp,
    /// Smallest chunk length in bytes appropriate for optimal I/O. This will
    /// be set to `1` for operating systems or file systems that do not
    /// recognize this concept. Not always a power of two.
    block_size: BlockSize,
};

pub fn stdout() File {
    return switch (native_os) {
        .windows => .{
            .handle = std.os.windows.peb().ProcessParameters.hStdOutput,
            .flags = .{ .nonblocking = false },
        },
        else => .{
            .handle = std.posix.STDOUT_FILENO,
            .flags = .{ .nonblocking = false },
        },
    };
}

pub fn stderr() File {
    return switch (native_os) {
        .windows => .{
            .handle = std.os.windows.peb().ProcessParameters.hStdError,
            .flags = .{ .nonblocking = false },
        },
        else => .{
            .handle = std.posix.STDERR_FILENO,
            .flags = .{ .nonblocking = false },
        },
    };
}

pub fn stdin() File {
    return switch (native_os) {
        .windows => .{
            .handle = std.os.windows.peb().ProcessParameters.hStdInput,
            .flags = .{ .nonblocking = false },
        },
        else => .{
            .handle = std.posix.STDIN_FILENO,
            .flags = .{ .nonblocking = false },
        },
    };
}

pub const StatError = error{
    SystemResources,
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to get its filestat information.
    AccessDenied,
    PermissionDenied,
    /// Attempted to stat a non-file stream.
    Streaming,
} || Io.Cancelable || Io.UnexpectedError;

/// Returns `Stat` containing basic information about the `File`.
pub fn stat(file: File, io: Io) StatError!Stat {
    return io.vtable.fileStat(io.userdata, file);
}

/// Deprecated, renamed to `Dir.OpenFileOptions.Mode`.
pub const OpenMode = Dir.OpenFileOptions.Mode;

pub const Lock = enum {
    none,
    shared,
    exclusive,
};

/// Deprecated, renamed to `Dir.OpenFileOptions`
pub const OpenFlags = Dir.OpenFileOptions;

/// Deprecated, renamed to `Dir.CreateFileOptions`.
pub const CreateFlags = Dir.CreateFileOptions;

pub const OpenError = error{
    PipeBusy,
    NoDevice,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
    /// On Windows, antivirus software is enabled by default. It can be
    /// disabled, but Windows Update sometimes ignores the user's preference
    /// and re-enables it. When enabled, antivirus software on Windows
    /// intercepts file system operations and makes them significantly slower
    /// in addition to possibly failing with this error code.
    AntivirusInterference,
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to open a new resource relative to it.
    AccessDenied,
    PermissionDenied,
    SymLinkLoop,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    /// Either:
    /// * One of the path components does not exist.
    /// * Cwd was used, but cwd has been deleted.
    /// * The path associated with the open directory handle has been deleted.
    /// * On macOS, multiple processes or threads raced to create the same file
    ///   with `O.EXCL` set to `false`.
    FileNotFound,
    /// The path exceeded `max_path_bytes` bytes.
    /// Insufficient kernel memory was available, or
    /// the named file is a FIFO and per-user hard limit on
    /// memory allocation for pipes has been reached.
    SystemResources,
    /// The file is too large to be opened. This error is unreachable
    /// for 64-bit targets, as well as when opening directories.
    FileTooBig,
    /// Either:
    /// * The path refers to a directory and write permissions were requested.
    /// * The path refers to a directory and `allow_directory` was set to false.
    IsDir,
    /// A new path cannot be created because the device has no room for the new file.
    /// This error is only reachable when the `CREAT` flag is provided.
    NoSpaceLeft,
    /// A component used as a directory in the path was not, in fact, a directory, or
    /// `DIRECTORY` was specified and the path was not a directory.
    NotDir,
    /// The path already exists and the `CREAT` and `EXCL` flags were provided.
    PathAlreadyExists,
    ReadOnlyFileSystem,
    DeviceBusy,
    FileLocksUnsupported,
    /// One of these three things:
    /// * pathname  refers to an executable image which is currently being
    ///   executed and write access was requested.
    /// * pathname refers to a file that is currently in  use  as  a  swap
    ///   file, and the O_TRUNC flag was specified.
    /// * pathname  refers  to  a file that is currently being read by the
    ///   kernel (e.g., for module/firmware loading), and write access was
    ///   requested.
    FileBusy,
    /// Non-blocking was requested and the operation cannot return immediately.
    WouldBlock,
} || Dir.PathNameError || Io.Cancelable || Io.UnexpectedError;

pub fn close(file: File, io: Io) void {
    return io.vtable.fileClose(io.userdata, (&file)[0..1]);
}

pub fn closeMany(io: Io, files: []const File) void {
    return io.vtable.fileClose(io.userdata, files);
}

pub const SyncError = error{
    InputOutput,
    NoSpaceLeft,
    DiskQuota,
    AccessDenied,
} || Io.Cancelable || Io.UnexpectedError;

/// Blocks until all pending file contents and metadata modifications for the
/// file have been synchronized with the underlying filesystem.
///
/// This does not ensure that metadata for the directory containing the file
/// has also reached disk.
pub fn sync(file: File, io: Io) SyncError!void {
    return io.vtable.fileSync(io.userdata, file);
}

/// Test whether the file refers to a terminal (similar to libc "isatty").
///
/// See also:
/// * `enableAnsiEscapeCodes`
/// * `supportsAnsiEscapeCodes`.
pub fn isTty(file: File, io: Io) Io.Cancelable!bool {
    return io.vtable.fileIsTty(io.userdata, file);
}

pub const EnableAnsiEscapeCodesError = error{
    NotTerminalDevice,
} || Io.Cancelable || Io.UnexpectedError;

pub fn enableAnsiEscapeCodes(file: File, io: Io) EnableAnsiEscapeCodesError!void {
    return io.vtable.fileEnableAnsiEscapeCodes(io.userdata, file);
}

/// Test whether ANSI escape codes will be treated as such without
/// attempting to enable support for ANSI escape codes.
pub fn supportsAnsiEscapeCodes(file: File, io: Io) Io.Cancelable!bool {
    return io.vtable.fileSupportsAnsiEscapeCodes(io.userdata, file);
}

pub const SetLengthError = error{
    FileTooBig,
    InputOutput,
    FileBusy,
    AccessDenied,
    PermissionDenied,
    NonResizable,
} || Io.Cancelable || Io.UnexpectedError;

/// Truncates or expands the file, populating any new data with zeroes.
///
/// The file offset after this call is left unchanged.
pub fn setLength(file: File, io: Io, new_length: u64) SetLengthError!void {
    return io.vtable.fileSetLength(io.userdata, file, new_length);
}

pub const LengthError = StatError;

/// Retrieve the ending byte index of the file.
///
/// Sometimes cheaper than `stat` if only the length is needed.
pub fn length(file: File, io: Io) LengthError!u64 {
    return io.vtable.fileLength(io.userdata, file);
}

pub const SetPermissionsError = error{
    AccessDenied,
    PermissionDenied,
    InputOutput,
    SymLinkLoop,
    FileNotFound,
    SystemResources,
    ReadOnlyFileSystem,
} || Io.Cancelable || Io.UnexpectedError;

/// Also known as "chmod".
///
/// The process must have the correct privileges in order to do this
/// successfully, or must have the effective user ID matching the owner of the
/// file.
pub fn setPermissions(file: File, io: Io, new_permissions: Permissions) SetPermissionsError!void {
    return io.vtable.fileSetPermissions(io.userdata, file, new_permissions);
}

pub const SetOwnerError = error{
    AccessDenied,
    PermissionDenied,
    InputOutput,
    SymLinkLoop,
    FileNotFound,
    SystemResources,
    ReadOnlyFileSystem,
} || Io.Cancelable || Io.UnexpectedError;

/// Also known as "chown".
///
/// The process must have the correct privileges in order to do this
/// successfully. The group may be changed by the owner of the file to any
/// group of which the owner is a member. If the owner or group is specified as
/// `null`, the ID is not changed.
pub fn setOwner(file: File, io: Io, owner: ?Uid, group: ?Gid) SetOwnerError!void {
    return io.vtable.fileSetOwner(io.userdata, file, owner, group);
}

/// Cross-platform representation of permissions on a file.
///
/// On POSIX systems this corresponds to "mode" and on Windows this corresponds to "attributes".
pub const Permissions = std.Options.FilePermissions orelse if (is_windows) enum(std.os.windows.DWORD) {
    default_file = 0,
    _,

    pub const default_dir: @This() = .default_file;
    pub const executable_file: @This() = .default_file;
    pub const has_executable_bit = false;

    const windows = std.os.windows;

    pub fn toAttributes(self: @This()) windows.FILE.ATTRIBUTE {
        return @bitCast(@intFromEnum(self));
    }

    pub fn readOnly(self: @This()) bool {
        const attributes = toAttributes(self);
        return attributes & windows.FILE_ATTRIBUTE_READONLY != 0;
    }

    pub fn setReadOnly(self: @This(), read_only: bool) @This() {
        const attributes = toAttributes(self);
        return @enumFromInt(if (read_only)
            attributes | windows.FILE_ATTRIBUTE_READONLY
        else
            attributes & ~@as(windows.DWORD, windows.FILE_ATTRIBUTE_READONLY));
    }
} else if (std.posix.mode_t != u0) enum(std.posix.mode_t) {
    /// This is the default mode given to POSIX operating systems for creating
    /// files. `0o666` is "-rw-rw-rw-" which is counter-intuitive at first,
    /// since most people would expect "-rw-r--r--", for example, when using
    /// the `touch` command, which would correspond to `0o644`. However, POSIX
    /// libc implementations use `0o666` inside `fopen` and then rely on the
    /// process-scoped "umask" setting to adjust this number for file creation.
    default_file = 0o666,
    /// This is the default mode given to POSIX operating systems for creating
    /// directories. `0o777` is "-rwxrwxrwx" which is counter-intuitive at first,
    /// since most people would expect "-rwxr-xr-x", for example, when using
    /// the `touch` command, which would correspond to `0o755`.
    default_dir = 0o777,
    _,

    pub const has_executable_bit = native_os != .wasi;

    pub const executable_file: @This() = .default_dir;

    pub fn toMode(self: @This()) std.posix.mode_t {
        return @intFromEnum(self);
    }

    pub fn fromMode(mode: std.posix.mode_t) @This() {
        return @enumFromInt(mode);
    }

    /// Returns `true` if and only if no class has write permissions.
    pub fn readOnly(self: @This()) bool {
        const mode = toMode(self);
        return mode & 0o222 == 0;
    }

    /// Enables write permission for all classes.
    pub fn setReadOnly(self: @This(), read_only: bool) @This() {
        const mode = toMode(self);
        const o222 = @as(std.posix.mode_t, 0o222);
        return @enumFromInt(if (read_only) mode & ~o222 else mode | o222);
    }
} else enum(u0) {
    default_file = 0,
    pub const default_dir: @This() = .default_file;
    pub const executable_file: @This() = .default_file;
    pub const has_executable_bit = false;
};

pub const SetTimestampsError = error{
    /// times is NULL, or both nsec values are UTIME_NOW, and either:
    /// *  the effective user ID of the caller does not match the  owner
    ///    of  the  file,  the  caller does not have write access to the
    ///    file, and the caller is not privileged (Linux: does not  have
    ///    either  the  CAP_FOWNER  or the CAP_DAC_OVERRIDE capability);
    ///    or,
    /// *  the file is marked immutable (see chattr(1)).
    AccessDenied,
    /// The caller attempted to change one or both timestamps to a value
    /// other than the current time, or to change one of the  timestamps
    /// to the current time while leaving the other timestamp unchanged,
    /// (i.e., times is not NULL, neither nsec  field  is  UTIME_NOW,
    /// and neither nsec field is UTIME_OMIT) and either:
    /// *  the  caller's  effective  user ID does not match the owner of
    ///    file, and the caller is not privileged (Linux: does not  have
    ///    the CAP_FOWNER capability); or,
    /// *  the file is marked append-only or immutable (see chattr(1)).
    PermissionDenied,
    ReadOnlyFileSystem,
} || Io.Cancelable || Io.UnexpectedError;

pub const SetTimestampsOptions = struct {
    access_timestamp: SetTimestamp = .unchanged,
    modify_timestamp: SetTimestamp = .unchanged,
};

pub const SetTimestamp = union(enum) {
    /// Leave the existing timestamp unmodified.
    unchanged,
    /// Set to current time using `Io.Clock.real`.
    now,
    /// Set to provided timestamp using `Io.Clock.real`.
    new: Io.Timestamp,

    /// Convenience for interacting with `Stat`, in which `null` indicates `unchanged`.
    pub fn init(optional: ?Io.Timestamp) SetTimestamp {
        return if (optional) |t| .{ .new = t } else .unchanged;
    }
};

/// The granularity that ultimately is stored depends on the combination of
/// operating system and file system. When a value as provided that exceeds
/// this range, the value is clamped to the maximum.
pub fn setTimestamps(file: File, io: Io, options: SetTimestampsOptions) SetTimestampsError!void {
    return io.vtable.fileSetTimestamps(io.userdata, file, options);
}

/// Sets the accessed and modification timestamps of `file` to the current wall
/// clock time.
///
/// The granularity that ultimately is stored depends on the combination of
/// operating system and file system.
pub fn setTimestampsNow(file: File, io: Io) SetTimestampsError!void {
    return io.vtable.fileSetTimestamps(io.userdata, file, .{
        .access_timestamp = .now,
        .modify_timestamp = .now,
    });
}

pub const ReadStreamingError = error{EndOfStream} || Reader.Error;

/// May return fewer bytes than buffer space available, including 0.
/// End-of-stream is indicated by `error.EndOfStream`.
///
/// See also:
/// * `reader`
pub fn readStreaming(file: File, io: Io, buffer: []const []u8) ReadStreamingError!usize {
    return (try io.operate(.{ .file_read_streaming = .{
        .file = file,
        .data = buffer,
    } })).file_read_streaming;
}

pub const ReadPositionalError = error{
    InputOutput,
    SystemResources,
    /// Trying to read a directory file descriptor as if it were a file.
    IsDir,
    /// Non-blocking has been enabled, and reading from the file descriptor
    /// would block.
    WouldBlock,
    /// In WASI, this error occurs when the file descriptor does
    /// not hold the required rights to read from it.
    AccessDenied,
    /// Unable to read file due to lock. Depending on the `Io` implementation,
    /// reading from a locked file may return this error, or may ignore the
    /// lock.
    LockViolation,
    /// This file cannot be read positionally.
    Unseekable,
    /// File was not opened with read capability.
    NotOpenForReading,
} || Io.Cancelable || Io.UnexpectedError;

/// Returns 0 on stream end or if `buffer` has no space available for data.
///
/// See also:
/// * `reader`
pub fn readPositional(file: File, io: Io, buffer: []const []u8, offset: u64) ReadPositionalError!usize {
    return io.vtable.fileReadPositional(io.userdata, file, buffer, offset);
}

pub const WritePositionalError = error{
    DiskQuota,
    FileTooBig,
    InputOutput,
    NoSpaceLeft,
    DeviceBusy,
    /// File descriptor does not hold the required rights to write to it.
    AccessDenied,
    PermissionDenied,
    /// File is an unconnected socket, or closed its read end.
    BrokenPipe,
    /// Insufficient kernel memory to read from in_fd.
    SystemResources,
    /// The process cannot access the file because another process has locked
    /// a portion of the file. Windows-only.
    LockViolation,
    /// Non-blocking has been enabled and this operation would block.
    WouldBlock,
    /// This error occurs when a device gets disconnected before or mid-flush
    /// while it's being written to - errno(6): No such device or address.
    NoDevice,
    FileBusy,
    /// This file cannot be written positionally.
    Unseekable,
    /// File was not opened with write capability.
    NotOpenForWriting,
} || Io.Cancelable || Io.UnexpectedError;

/// See also:
/// * `writer`
pub fn writePositional(file: File, io: Io, buffer: []const []const u8, offset: u64) WritePositionalError!usize {
    return io.vtable.fileWritePositional(io.userdata, file, &.{}, buffer, 1, offset);
}

/// Equivalent to creating a positional writer, writing `bytes`, and then flushing.
pub fn writePositionalAll(file: File, io: Io, bytes: []const u8, offset: u64) WritePositionalError!void {
    var index: usize = 0;
    while (index < bytes.len)
        index += try io.vtable.fileWritePositional(io.userdata, file, &.{}, &.{bytes[index..]}, 1, offset + index);
}

pub const SeekError = error{
    Unseekable,
    /// The file descriptor does not hold the required rights to seek on it.
    AccessDenied,
} || Io.Cancelable || Io.UnexpectedError;

pub const WriteFilePositionalError = Writer.WriteFileError || error{Unseekable};

/// Defaults to positional reading; falls back to streaming.
///
/// Positional is more threadsafe, since the global seek position is not
/// affected.
///
/// See also:
/// * `readerStreaming`
pub fn reader(file: File, io: Io, buffer: []u8) Reader {
    return .init(file, io, buffer);
}

/// Equivalent to creating a positional reader and reading multiple times to fill `buffer`.
///
/// Returns number of bytes read into `buffer`. If less than `buffer.len`, end of file occurred.
///
/// See also:
/// * `reader`
pub fn readPositionalAll(file: File, io: Io, buffer: []u8, offset: u64) ReadPositionalError!usize {
    var index: usize = 0;
    while (index != buffer.len) {
        const amt = try file.readPositional(io, &.{buffer[index..]}, offset + index);
        if (amt == 0) break;
        index += amt;
    }
    return index;
}

/// Positional is more threadsafe, since the global seek position is not
/// affected, but when such syscalls are not available, preemptively
/// initializing in streaming mode skips a failed syscall.
///
/// See also:
/// * `reader`
pub fn readerStreaming(file: File, io: Io, buffer: []u8) Reader {
    return .initStreaming(file, io, buffer);
}

/// Defaults to positional reading; falls back to streaming.
///
/// Positional is more threadsafe, since the global seek position is not
/// affected.
pub fn writer(file: File, io: Io, buffer: []u8) Writer {
    return .init(file, io, buffer);
}

/// Positional is more threadsafe, since the global seek position is not
/// affected, but when such syscalls are not available, preemptively
/// initializing in streaming mode will skip a failed syscall.
pub fn writerStreaming(file: File, io: Io, buffer: []u8) Writer {
    return .initStreaming(file, io, buffer);
}

/// This is a low-level API that calls the `Io` interface function directly.
/// For a higher level API, see `writerStreaming`.
pub fn writeStreaming(file: File, io: Io, header: []const u8, data: []const []const u8, splat: usize) Writer.Error!usize {
    return (try io.operate(.{ .file_write_streaming = .{
        .file = file,
        .header = header,
        .data = data,
        .splat = splat,
    } })).file_write_streaming;
}

/// Equivalent to creating a streaming writer, writing `bytes`, and then flushing.
pub fn writeStreamingAll(file: File, io: Io, bytes: []const u8) Writer.Error!void {
    var index: usize = 0;
    while (index < bytes.len) {
        index += try writeStreaming(file, io, &.{}, &.{bytes[index..]}, 1);
    }
}

pub const LockError = error{
    SystemResources,
    FileLocksUnsupported,
} || Io.Cancelable || Io.UnexpectedError;

/// Blocks when an incompatible lock is held by another process. A process may
/// hold only one type of lock (shared or exclusive) on a file. When a process
/// terminates in any way, the lock is released.
///
/// Assumes the file is unlocked.
pub fn lock(file: File, io: Io, l: Lock) LockError!void {
    return io.vtable.fileLock(io.userdata, file, l);
}

/// Assumes the file is locked.
pub fn unlock(file: File, io: Io) void {
    return io.vtable.fileUnlock(io.userdata, file);
}

/// Attempts to obtain a lock, returning `true` if the lock is obtained, and
/// `false` if there was an existing incompatible lock held. A process may hold
/// only one type of lock (shared or exclusive) on a file. When a process
/// terminates in any way, the lock is released.
///
/// Assumes the file is unlocked.
pub fn tryLock(file: File, io: Io, l: Lock) LockError!bool {
    return io.vtable.fileTryLock(io.userdata, file, l);
}

pub const DowngradeLockError = Io.Cancelable || Io.UnexpectedError;

/// Assumes the file is already locked in exclusive mode.
/// Atomically modifies the lock to be in shared mode, without releasing it.
pub fn downgradeLock(file: File, io: Io) LockError!void {
    return io.vtable.fileDowngradeLock(io.userdata, file);
}

pub const RealPathError = error{
    /// This operating system, file system, or `Io` implementation does not
    /// support realpath operations.
    OperationUnsupported,
    /// The full file system path could not fit into the provided buffer, or
    /// due to its length could not be obtained via realpath functions no
    /// matter the buffer size provided.
    NameTooLong,
    FileNotFound,
    AccessDenied,
    PermissionDenied,
    NotDir,
    SymLinkLoop,
    InputOutput,
    FileTooBig,
    IsDir,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    NoDevice,
    SystemResources,
    NoSpaceLeft,
    FileSystem,
    DeviceBusy,
    FileBusy,
    PipeBusy,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
    PathAlreadyExists,
    /// On Windows, antivirus software is enabled by default. It can be
    /// disabled, but Windows Update sometimes ignores the user's preference
    /// and re-enables it. When enabled, antivirus software on Windows
    /// intercepts file system operations and makes them significantly slower
    /// in addition to possibly failing with this error code.
    AntivirusInterference,
    /// On Windows, the volume does not contain a recognized file system. File
    /// system drivers might not be loaded, or the volume may be corrupt.
    UnrecognizedVolume,
} || Io.Cancelable || Io.UnexpectedError;

/// Obtains the canonicalized absolute path name corresponding to an open file
/// handle.
///
/// This function has limited platform support, and using it can lead to
/// unnecessary failures and race conditions. It is generally advisable to
/// avoid this function entirely.
pub fn realPath(file: File, io: Io, out_buffer: []u8) RealPathError!usize {
    return io.vtable.fileRealPath(io.userdata, file, out_buffer);
}

pub const HardLinkOptions = struct {
    follow_symlinks: bool = false,
};

pub const HardLinkError = error{
    AccessDenied,
    PermissionDenied,
    DiskQuota,
    PathAlreadyExists,
    HardwareFailure,
    /// Either the OS or the filesystem does not support hard links.
    OperationUnsupported,
    SymLinkLoop,
    LinkQuotaExceeded,
    FileNotFound,
    SystemResources,
    NoSpaceLeft,
    ReadOnlyFileSystem,
    CrossDevice,
    NotDir,
} || Io.Cancelable || Dir.PathNameError || Io.UnexpectedError;

pub fn hardLink(
    file: File,
    io: Io,
    new_dir: Dir,
    new_sub_path: []const u8,
    options: HardLinkOptions,
) HardLinkError!void {
    return io.vtable.fileHardLink(io.userdata, file, new_dir, new_sub_path, options);
}

pub fn createMemoryMap(file: File, io: Io, options: MemoryMap.CreateOptions) MemoryMap.CreateError!MemoryMap {
    return .create(io, file, options);
}

test {
    _ = Reader;
    _ = Writer;
    _ = Atomic;
    _ = MemoryMap;
}



---
File: /std/Io/Kqueue.zig
---

const Kqueue = @This();
const builtin = @import("builtin");

const std = @import("../std.zig");
const Io = std.Io;
const Dir = std.Io.Dir;
const File = std.Io.File;
const net = std.Io.net;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const Alignment = std.mem.Alignment;
const IpAddress = std.Io.net.IpAddress;
const errnoBug = std.Io.Threaded.errnoBug;
const closeFd = std.Io.Threaded.closeFd;
const posix = std.posix;
const posixSocketModeProtocol = Io.Threaded.posixSocketModeProtocol;

/// Must be a thread-safe allocator.
gpa: Allocator,
mutex: Io.Mutex,
main_fiber_buffer: [@sizeOf(Fiber) + Fiber.max_result_size]u8 align(@alignOf(Fiber)),
threads: Thread.List,

/// Empirically saw >128KB being used by the self-hosted backend to panic.
const idle_stack_size = 256 * 1024;

const max_idle_search = 4;
const max_steal_ready_search = 4;
const max_iovecs_len = 8;

const changes_buffer_len = 64;

const Thread = struct {
    thread: std.Thread,
    idle_context: Io.fiber.Context,
    current_context: *Io.fiber.Context,
    ready_queue: ?*Fiber,
    kq_fd: posix.fd_t,
    idle_search_index: u32,
    steal_ready_search_index: u32,
    /// For ensuring multiple fibers waiting on the same file descriptor and
    /// filter use the same kevent.
    wait_queues: std.AutoArrayHashMapUnmanaged(WaitQueueKey, *Fiber),

    const WaitQueueKey = struct {
        ident: usize,
        filter: i32,
    };

    const canceling: ?*Thread = @ptrFromInt(@alignOf(Thread));

    threadlocal var self: *Thread = undefined;

    fn current() *Thread {
        return self;
    }

    fn currentFiber(thread: *Thread) *Fiber {
        return @fieldParentPtr("context", thread.current_context);
    }

    const List = struct {
        allocated: []Thread,
        reserved: u32,
        active: u32,
    };

    fn deinit(thread: *Thread, gpa: Allocator) void {
        closeFd(thread.kq_fd);
        assert(thread.wait_queues.count() == 0);
        thread.wait_queues.deinit(gpa);
        thread.* = undefined;
    }
};

const Fiber = struct {
    required_align: void align(4),
    context: Io.fiber.Context,
    awaiter: ?*Fiber,
    queue_next: ?*Fiber,
    cancel_thread: ?*Thread,
    awaiting_completions: std.StaticBitSet(3),

    const finished: ?*Fiber = @ptrFromInt(@alignOf(Thread));

    const max_result_align: Alignment = .@"16";
    const max_result_size = max_result_align.forward(64);
    /// This includes any stack realignments that need to happen, and also the
    /// initial frame return address slot and argument frame, depending on target.
    const min_stack_size = 4 * 1024 * 1024;
    const max_context_align: Alignment = .@"16";
    const max_context_size = max_context_align.forward(1024);
    const max_closure_size: usize = @sizeOf(AsyncClosure);
    const max_closure_align: Alignment = .of(AsyncClosure);
    const allocation_size = std.mem.alignForward(
        usize,
        max_closure_align.max(max_context_align).forward(
            max_result_align.forward(@sizeOf(Fiber)) + max_result_size + min_stack_size,
        ) + max_closure_size + max_context_size,
        std.heap.page_size_max,
    );

    fn allocate(k: *Kqueue) error{OutOfMemory}!*Fiber {
        return @ptrCast(try k.gpa.alignedAlloc(u8, .of(Fiber), allocation_size));
    }

    fn allocatedSlice(f: *Fiber) []align(@alignOf(Fiber)) u8 {
        return @as([*]align(@alignOf(Fiber)) u8, @ptrCast(f))[0..allocation_size];
    }

    fn allocatedEnd(f: *Fiber) [*]u8 {
        const allocated_slice = f.allocatedSlice();
        return allocated_slice[allocated_slice.len..].ptr;
    }

    fn resultPointer(f: *Fiber, comptime Result: type) *Result {
        return @ptrCast(@alignCast(f.resultBytes(.of(Result))));
    }

    fn resultBytes(f: *Fiber, alignment: Alignment) [*]u8 {
        return @ptrFromInt(alignment.forward(@intFromPtr(f) + @sizeOf(Fiber)));
    }

    fn enterCancelRegion(fiber: *Fiber, thread: *Thread) error{Canceled}!void {
        if (@cmpxchgStrong(
            ?*Thread,
            &fiber.cancel_thread,
            null,
            thread,
            .acq_rel,
            .acquire,
        )) |cancel_thread| {
            assert(cancel_thread == Thread.canceling);
            return error.Canceled;
        }
    }

    fn exitCancelRegion(fiber: *Fiber, thread: *Thread) void {
        if (@cmpxchgStrong(
            ?*Thread,
            &fiber.cancel_thread,
            thread,
            null,
            .acq_rel,
            .acquire,
        )) |cancel_thread| assert(cancel_thread == Thread.canceling);
    }

    const Queue = struct { head: *Fiber, tail: *Fiber };
};

fn recycle(k: *Kqueue, fiber: *Fiber) void {
    std.log.debug("recyling {*}", .{fiber});
    assert(fiber.queue_next == null);
    k.gpa.free(fiber.allocatedSlice());
}

pub const InitOptions = struct {
    n_threads: ?usize = null,
};

pub const InitError = Allocator.Error || CreateFileDescriptorError;

pub fn init(k: *Kqueue, gpa: Allocator, options: InitOptions) !void {
    assert(options.n_threads != 0);

    const n_threads = @max(1, options.n_threads orelse std.Thread.getCpuCount() catch 1);
    const threads_size = n_threads * @sizeOf(Thread);
    const idle_stack_end_offset = std.mem.alignForward(usize, threads_size + idle_stack_size, std.heap.page_size_max);
    const allocated_slice = try gpa.alignedAlloc(u8, .of(Thread), idle_stack_end_offset);
    errdefer gpa.free(allocated_slice);
    k.* = .{
        .gpa = gpa,
        .mutex = .init,
        .main_fiber_buffer = undefined,
        .threads = .{
            .allocated = @ptrCast(allocated_slice[0..threads_size]),
            .reserved = 1,
            .active = 1,
        },
    };
    const main_fiber: *Fiber = @ptrCast(&k.main_fiber_buffer);
    main_fiber.* = .{
        .required_align = {},
        .context = undefined,
        .awaiter = null,
        .queue_next = null,
        .cancel_thread = null,
        .awaiting_completions = .empty,
    };
    const main_thread = &k.threads.allocated[0];
    Thread.self = main_thread;
    const idle_stack_end: [*]align(16) usize = @ptrCast(@alignCast(allocated_slice[idle_stack_end_offset..].ptr));
    (idle_stack_end - 1)[0..1].* = .{@intFromPtr(k)};
    main_thread.* = .{
        .thread = undefined,
        .idle_context = switch (builtin.cpu.arch) {
            .aarch64 => .{
                .sp = @intFromPtr(idle_stack_end),
                .fp = 0,
                .pc = @intFromPtr(&mainIdleEntry),
            },
            .x86_64 => .{
                .rsp = @intFromPtr(idle_stack_end - 1),
                .rbp = 0,
                .rip = @intFromPtr(&mainIdleEntry),
            },
            else => @compileError("unimplemented architecture"),
        },
        .current_context = &main_fiber.context,
        .ready_queue = null,
        .kq_fd = try createFileDescriptor(),
        .idle_search_index = 1,
        .steal_ready_search_index = 1,
        .wait_queues = .empty,
    };
    errdefer closeFd(main_thread.kq_fd);
    std.log.debug("created main idle {*}", .{&main_thread.idle_context});
    std.log.debug("created main {*}", .{main_fiber});
}

pub fn deinit(k: *Kqueue) void {
    const active_threads = @atomicLoad(u32, &k.threads.active, .acquire);
    for (k.threads.allocated[0..active_threads]) |*thread| {
        const ready_fiber = @atomicLoad(?*Fiber, &thread.ready_queue, .monotonic);
        assert(ready_fiber == null or ready_fiber == Fiber.finished); // pending async
    }
    k.yield(null, .exit);
    const main_thread = &k.threads.allocated[0];
    const gpa = k.gpa;
    main_thread.deinit(gpa);
    const allocated_ptr: [*]align(@alignOf(Thread)) u8 = @ptrCast(@alignCast(k.threads.allocated.ptr));
    const idle_stack_end_offset = std.mem.alignForward(usize, k.threads.allocated.len * @sizeOf(Thread) + idle_stack_size, std.heap.page_size_max);
    for (k.threads.allocated[1..active_threads]) |*thread| thread.thread.join();
    gpa.free(allocated_ptr[0..idle_stack_end_offset]);
    k.* = undefined;
}

pub const CreateFileDescriptorError = error{
    /// The per-process limit on the number of open file descriptors has been reached.
    ProcessFdQuotaExceeded,
    /// The system-wide limit on the total number of open files has been reached.
    SystemFdQuotaExceeded,
} || Io.UnexpectedError;

pub fn createFileDescriptor() CreateFileDescriptorError!posix.fd_t {
    const rc = posix.system.kqueue();
    switch (posix.errno(rc)) {
        .SUCCESS => return @intCast(rc),
        .MFILE => return error.ProcessFdQuotaExceeded,
        .NFILE => return error.SystemFdQuotaExceeded,
        else => |err| return posix.unexpectedErrno(err),
    }
}

fn findReadyFiber(k: *Kqueue, thread: *Thread) ?*Fiber {
    if (@atomicRmw(?*Fiber, &thread.ready_queue, .Xchg, Fiber.finished, .acquire)) |ready_fiber| {
        @atomicStore(?*Fiber, &thread.ready_queue, ready_fiber.queue_next, .release);
        ready_fiber.queue_next = null;
        return ready_fiber;
    }
    const active_threads = @atomicLoad(u32, &k.threads.active, .acquire);
    for (0..@min(max_steal_ready_search, active_threads)) |_| {
        defer thread.steal_ready_search_index += 1;
        if (thread.steal_ready_search_index == active_threads) thread.steal_ready_search_index = 0;
        const steal_ready_search_thread = &k.threads.allocated[0..active_threads][thread.steal_ready_search_index];
        if (steal_ready_search_thread == thread) continue;
        const ready_fiber = @atomicLoad(?*Fiber, &steal_ready_search_thread.ready_queue, .acquire) orelse continue;
        if (ready_fiber == Fiber.finished) continue;
        if (@cmpxchgWeak(
            ?*Fiber,
            &steal_ready_search_thread.ready_queue,
            ready_fiber,
            null,
            .acquire,
            .monotonic,
        )) |_| continue;
        @atomicStore(?*Fiber, &thread.ready_queue, ready_fiber.queue_next, .release);
        ready_fiber.queue_next = null;
        return ready_fiber;
    }
    // couldn't find anything to do, so we are now open for business
    @atomicStore(?*Fiber, &thread.ready_queue, null, .monotonic);
    return null;
}

fn yield(k: *Kqueue, maybe_ready_fiber: ?*Fiber, pending_task: SwitchMessage.PendingTask) void {
    const thread: *Thread = .current();
    const ready_context = if (maybe_ready_fiber orelse k.findReadyFiber(thread)) |ready_fiber|
        &ready_fiber.context
    else
        &thread.idle_context;
    const message: SwitchMessage = .{
        .contexts = .{
            .old = thread.current_context,
            .new = ready_context,
        },
        .pending_task = pending_task,
    };
    std.log.debug("switching from {*} to {*}", .{ message.contexts.old, message.contexts.new });
    contextSwitch(&message).handle(k);
}

fn schedule(k: *Kqueue, thread: *Thread, ready_queue: Fiber.Queue) void {
    {
        var fiber = ready_queue.head;
        while (true) {
            std.log.debug("scheduling {*}", .{fiber});
            fiber = fiber.queue_next orelse break;
        }
        assert(fiber == ready_queue.tail);
    }
    // shared fields of previous `Thread` must be initialized before later ones are marked as active
    const new_thread_index = @atomicLoad(u32, &k.threads.active, .acquire);
    for (0..@min(max_idle_search, new_thread_index)) |_| {
        defer thread.idle_search_index += 1;
        if (thread.idle_search_index == new_thread_index) thread.idle_search_index = 0;
        const idle_search_thread = &k.threads.allocated[0..new_thread_index][thread.idle_search_index];
        if (idle_search_thread == thread) continue;
        if (@cmpxchgWeak(
            ?*Fiber,
            &idle_search_thread.ready_queue,
            null,
            ready_queue.head,
            .release,
            .monotonic,
        )) |_| continue;
        const changes = [_]posix.Kevent{
            .{
                .ident = 0,
                .filter = std.c.EVFILT.USER,
                .flags = std.c.EV.ADD | std.c.EV.ONESHOT,
                .fflags = std.c.NOTE.TRIGGER,
                .data = 0,
                .udata = @intFromEnum(Completion.UserData.wakeup),
            },
        };
        // If an error occurs it only pessimises scheduling.
        _ = kevent(idle_search_thread.kq_fd, &changes, &.{}, null) catch |err| {
            // TODO handle EINTR for cancellation purposes
            @panic(@errorName(err)); // TODO
        };
        return;
    }
    spawn_thread: {
        // previous failed reservations must have completed before retrying
        if (new_thread_index == k.threads.allocated.len or @cmpxchgWeak(
            u32,
            &k.threads.reserved,
            new_thread_index,
            new_thread_index + 1,
            .acquire,
            .monotonic,
        ) != null) break :spawn_thread;
        const new_thread = &k.threads.allocated[new_thread_index];
        const next_thread_index = new_thread_index + 1;
        new_thread.* = .{
            .thread = undefined,
            .idle_context = undefined,
            .current_context = &new_thread.idle_context,
            .ready_queue = ready_queue.head,
            .kq_fd = createFileDescriptor() catch |err| {
                @atomicStore(u32, &k.threads.reserved, new_thread_index, .release);
                // no more access to `thread` after giving up reservation
                std.log.warn("unable to create worker thread due to kqueue init failure: {t}", .{err});
                break :spawn_thread;
            },
            .idle_search_index = 0,
            .steal_ready_search_index = 0,
            .wait_queues = .empty,
        };
        new_thread.thread = std.Thread.spawn(.{
            .stack_size = idle_stack_size,
            .allocator = k.gpa,
        }, threadEntry, .{ k, new_thread_index }) catch |err| {
            closeFd(new_thread.kq_fd);
            @atomicStore(u32, &k.threads.reserved, new_thread_index, .release);
            // no more access to `thread` after giving up reservation
            std.log.warn("unable to create worker thread due spawn failure: {s}", .{@errorName(err)});
            break :spawn_thread;
        };
        // shared fields of `Thread` must be initialized before being marked active
        @atomicStore(u32, &k.threads.active, next_thread_index, .release);
        return;
    }
    // nobody wanted it, so just queue it on ourselves
    while (@cmpxchgWeak(
        ?*Fiber,
        &thread.ready_queue,
        ready_queue.tail.queue_next,
        ready_queue.head,
        .acq_rel,
        .acquire,
    )) |old_head| ready_queue.tail.queue_next = old_head;
}

fn mainIdle(k: *Kqueue, message: *const SwitchMessage) callconv(.withStackAlign(.c, @max(@alignOf(Thread), @alignOf(Io.fiber.Context)))) noreturn {
    message.handle(k);
    k.idle(&k.threads.allocated[0]);
    k.yield(@ptrCast(&k.main_fiber_buffer), .nothing);
    unreachable; // switched to dead fiber
}

fn threadEntry(k: *Kqueue, index: u32) void {
    const thread: *Thread = &k.threads.allocated[index];
    Thread.self = thread;
    std.log.debug("created thread idle {*}", .{&thread.idle_context});
    k.idle(thread);
    thread.deinit(k.gpa);
}

const Completion = struct {
    const UserData = enum(usize) {
        unused,
        wakeup,
        cleanup,
        exit,
        /// *Fiber
        _,
    };
    /// Corresponds to Kevent field.
    flags: u16,
    /// Corresponds to Kevent field.
    fflags: u32,
    /// Corresponds to Kevent field.
    data: isize,
};

fn idle(k: *Kqueue, thread: *Thread) void {
    var events_buffer: [changes_buffer_len]posix.Kevent = undefined;
    var maybe_ready_fiber: ?*Fiber = null;
    while (true) {
        while (maybe_ready_fiber orelse k.findReadyFiber(thread)) |ready_fiber| {
            k.yield(ready_fiber, .nothing);
            maybe_ready_fiber = null;
        }
        const n = kevent(thread.kq_fd, &.{}, &events_buffer, null) catch |err| {
            // TODO handle EINTR for cancellation purposes
            @panic(@errorName(err)); // TODO
        };
        var maybe_ready_queue: ?Fiber.Queue = null;
        for (events_buffer[0..n]) |event| switch (@as(Completion.UserData, @enumFromInt(event.udata))) {
            .unused => unreachable, // bad submission queued?
            .wakeup => {},
            .cleanup => @panic("failed to notify other threads that we are exiting"),
            .exit => {
                assert(maybe_ready_fiber == null and maybe_ready_queue == null); // pending async
                return;
            },
            _ => {
                const event_head_fiber: *Fiber = @ptrFromInt(event.udata);
                const event_tail_fiber = thread.wait_queues.fetchSwapRemove(.{
                    .ident = event.ident,
                    .filter = event.filter,
                }).?.value;
                assert(event_tail_fiber.queue_next == null);

                // TODO reevaluate this logic
                event_head_fiber.resultPointer(Completion).* = .{
                    .flags = event.flags,
                    .fflags = event.fflags,
                    .data = event.data,
                };

                queue_ready: {
                    const head: *Fiber = if (maybe_ready_fiber == null) f: {
                        maybe_ready_fiber = event_head_fiber;
                        const next = event_head_fiber.queue_next orelse break :queue_ready;
                        event_head_fiber.queue_next = null;
                        break :f next;
                    } else event_head_fiber;

                    if (maybe_ready_queue) |*ready_queue| {
                        ready_queue.tail.queue_next = head;
                        ready_queue.tail = event_tail_fiber;
                    } else {
                        maybe_ready_queue = .{ .head = head, .tail = event_tail_fiber };
                    }
                }
            },
        };
        if (maybe_ready_queue) |ready_queue| k.schedule(thread, ready_queue);
    }
}

const SwitchMessage = struct {
    contexts: Io.fiber.Switch,
    pending_task: PendingTask,

    const PendingTask = union(enum) {
        nothing,
        reschedule,
        recycle: *Fiber,
     
```
