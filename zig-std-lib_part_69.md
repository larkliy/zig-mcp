```
ReadOnlyFileSystem,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn dirReadLink(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    buffer: []u8,
) Dir.ReadLinkError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var sub_path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &sub_path_buffer);

    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    while (true) {
        try sync.cancel_region.await(.nothing);
        const rc = linux.readlinkat(dir.handle, sub_path_posix, buffer.ptr, buffer.len);
        switch (linux.errno(rc)) {
            .SUCCESS => return @bitCast(rc),
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .FAULT => |err| return errnoBug(err),
            .INVAL => return error.NotLink,
            .IO => return error.FileSystem,
            .LOOP => return error.SymLinkLoop,
            .NAMETOOLONG => return error.NameTooLong,
            .NOENT => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOTDIR => return error.NotDir,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn dirSetOwner(
    userdata: ?*anyopaque,
    dir: Dir,
    owner: ?File.Uid,
    group: ?File.Gid,
) Dir.SetOwnerError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.fchownat(
        &sync,
        dir.handle,
        "",
        owner orelse std.math.maxInt(linux.uid_t),
        group orelse std.math.maxInt(linux.gid_t),
        linux.AT.EMPTY_PATH,
    );
}

fn dirSetFileOwner(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    owner: ?File.Uid,
    group: ?File.Gid,
    options: Dir.SetFileOwnerOptions,
) Dir.SetFileOwnerError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.fchownat(
        &sync,
        dir.handle,
        sub_path_posix,
        owner orelse std.math.maxInt(linux.uid_t),
        group orelse std.math.maxInt(linux.gid_t),
        if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW,
    );
}

fn dirSetPermissions(
    userdata: ?*anyopaque,
    dir: Dir,
    permissions: Dir.Permissions,
) Dir.SetPermissionsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    ev.fchmodat(
        &sync,
        dir.handle,
        "",
        permissions.toMode(),
        linux.AT.EMPTY_PATH,
    ) catch |err| switch (err) {
        error.NameTooLong => return errnoBug(.NAMETOOLONG),
        error.BadPathName => return errnoBug(.ILSEQ),
        error.ProcessFdQuotaExceeded => return errnoBug(.MFILE),
        error.SystemFdQuotaExceeded => return errnoBug(.NFILE),
        error.OperationUnsupported => return errnoBug(.OPNOTSUPP),
        else => |e| return e,
    };
}

fn dirSetFilePermissions(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.SetFilePermissionsOptions,
) Dir.SetFilePermissionsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.fchmodat(
        &sync,
        dir.handle,
        sub_path_posix,
        permissions.toMode(),
        if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW,
    );
}

fn dirSetTimestamps(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.SetTimestampsOptions,
) Dir.SetTimestampsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);
    var cancel_region: CancelRegion.Sync = try .init(ev);
    defer cancel_region.deinit(ev);
    try ev.utimensat(
        &cancel_region,
        dir.handle,
        sub_path_posix,
        if (options.modify_timestamp != .now or options.access_timestamp != .now) &.{
            setTimestampToPosix(options.access_timestamp),
            setTimestampToPosix(options.modify_timestamp),
        } else null,
        if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW,
    );
}

fn dirHardLink(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
    options: Dir.HardLinkOptions,
) Dir.HardLinkError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var old_path_buffer: [PATH_MAX]u8 = undefined;
    var new_path_buffer: [PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.linkat(
        &cancel_region,
        old_dir.handle,
        old_sub_path_posix,
        new_dir.handle,
        new_sub_path_posix,
        if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW,
    );
}

fn fileStat(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.stat(&cancel_region, file.handle);
}

fn fileLength(userdata: ?*anyopaque, file: File) File.LengthError!u64 {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        var statx_buf = std.mem.zeroes(linux.Statx);
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .STATX,
            .flags = 0,
            .ioprio = 0,
            .fd = file.handle,
            .off = @intFromPtr(&statx_buf),
            .addr = @intFromPtr(""),
            .len = @bitCast(linux.STATX{ .SIZE = true }),
            .rw_flags = linux.AT.EMPTY_PATH,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => {
                if (!statx_buf.mask.SIZE) return error.Unexpected;
                return statx_buf.size;
            },
            .INTR, .CANCELED => {},
            .ACCES => |err| return errnoBug(err),
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .LOOP => |err| return errnoBug(err),
            .NAMETOOLONG => |err| return errnoBug(err),
            .NOENT => |err| return errnoBug(err),
            .NOMEM => return error.SystemResources,
            .NOTDIR => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fileClose(userdata: ?*anyopaque, files: []const File) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    for (files) |file| ev.close(file.handle);
}

fn fileWritePositional(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
    offset: u64,
) File.WritePositionalError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var iovecs: [max_iovecs_len]iovec_const = undefined;
    var iovlen: iovlen_t = 0;
    addBuf(&iovecs, &iovlen, header);
    for (data[0 .. data.len - 1]) |bytes| addBuf(&iovecs, &iovlen, bytes);
    const pattern = data[data.len - 1];
    var backup_buffer: [splat_buffer_size]u8 = undefined;
    if (iovecs.len - iovlen != 0) switch (splat) {
        0 => {},
        1 => addBuf(&iovecs, &iovlen, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &backup_buffer;
                const memset_len = @min(splat_buffer.len, splat);
                const buf = splat_buffer[0..memset_len];
                @memset(buf, pattern[0]);
                addBuf(&iovecs, &iovlen, buf);
                var remaining_splat = splat - buf.len;
                while (remaining_splat > splat_buffer.len and iovecs.len - iovlen != 0) {
                    assert(buf.len == splat_buffer.len);
                    addBuf(&iovecs, &iovlen, splat_buffer);
                    remaining_splat -= splat_buffer.len;
                }
                addBuf(&iovecs, &iovlen, splat_buffer[0..@min(remaining_splat, splat_buffer.len)]);
            },
            else => for (0..@min(splat, iovecs.len - iovlen)) |_| {
                addBuf(&iovecs, &iovlen, pattern);
            },
        },
    };

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.pwritev(&cancel_region, file.handle, iovecs[0..iovlen], offset);
}

/// This is either usize or u32. Since, either is fine, let's use the same
/// `addBuf` function for both writing to a file and sending network messages.
const iovlen_t = @FieldType(linux.msghdr_const, "iovlen");

fn addBuf(v: []iovec_const, i: *iovlen_t, bytes: []const u8) void {
    // OS checks ptr addr before length so zero length vectors must be omitted.
    if (bytes.len == 0) return;
    if (v.len - i.* == 0) return;
    v[i.*] = .{ .base = bytes.ptr, .len = bytes.len };
    i.* += 1;
}

fn fileWriteFileStreaming(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
) File.Writer.WriteFileError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = file;
    _ = header;
    _ = file_reader;
    _ = limit;
    return error.Unimplemented;
}

fn fileWriteFilePositional(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
    offset: u64,
) File.WriteFilePositionalError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = file;
    _ = header;
    _ = file_reader;
    _ = limit;
    _ = offset;
    return error.Unimplemented;
}

fn fileReadPositional(
    userdata: ?*anyopaque,
    file: File,
    data: []const []u8,
    offset: u64,
) File.ReadPositionalError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var iovecs_buffer: [max_iovecs_len]iovec = undefined;
    var i: usize = 0;
    for (data) |buf| {
        if (iovecs_buffer.len - i == 0) break;
        if (buf.len > 0) {
            iovecs_buffer[i] = .{ .base = buf.ptr, .len = buf.len };
            i += 1;
        }
    }
    if (i == 0) return 0;
    const dest = iovecs_buffer[0..i];
    assert(dest[0].len > 0);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.preadv(&cancel_region, file.handle, dest, offset) catch |err| switch (err) {
        error.SocketUnconnected => return errnoBug(.NOTCONN), // not a socket
        error.ConnectionResetByPeer => return errnoBug(.CONNRESET), // not a socket
        else => |e| return e,
    };
}

fn fileSeekBy(userdata: ?*anyopaque, file: File, offset: i64) File.SeekError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.lseek(&sync, file.handle, @bitCast(offset), linux.SEEK.CUR);
}

fn fileSeekTo(userdata: ?*anyopaque, file: File, offset: u64) File.SeekError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.lseek(&sync, file.handle, offset, linux.SEEK.SET);
}

fn fileSync(userdata: ?*anyopaque, file: File) File.SyncError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .FSYNC,
            .flags = 0,
            .ioprio = 0,
            .fd = file.handle,
            .off = 0,
            .addr = 0,
            .len = 0,
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .BADF => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .ROFS => |err| return errnoBug(err),
            .IO => return error.InputOutput,
            .NOSPC => return error.NoSpaceLeft,
            .DQUOT => return error.DiskQuota,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fileIsTty(userdata: ?*anyopaque, file: File) Io.Cancelable!bool {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    while (true) {
        try sync.cancel_region.await(.nothing);
        var wsz: winsize = undefined;
        const rc = linux.ioctl(file.handle, linux.T.IOCGWINSZ, @intFromPtr(&wsz));
        switch (linux.errno(rc)) {
            .SUCCESS => return true,
            .INTR => {},
            else => return false,
        }
    }
}

fn fileEnableAnsiEscapeCodes(userdata: ?*anyopaque, file: File) File.EnableAnsiEscapeCodesError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (!try fileIsTty(ev, file)) return error.NotTerminalDevice;
}

fn fileSetLength(userdata: ?*anyopaque, file: File, length: u64) File.SetLengthError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .FTRUNCATE,
            .flags = 0,
            .ioprio = 0,
            .fd = file.handle,
            .off = length,
            .addr = 0,
            .len = 0,
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .FBIG => return error.FileTooBig,
            .IO => return error.InputOutput,
            .PERM => return error.PermissionDenied,
            .TXTBSY => return error.FileBusy,
            .BADF => |err| return errnoBug(err), // Handle not open for writing.
            .INVAL => return error.NonResizable, // This is returned for /dev/null for example.
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fileSetOwner(
    userdata: ?*anyopaque,
    file: File,
    owner: ?File.Uid,
    group: ?File.Gid,
) File.SetOwnerError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.fchownat(
        &sync,
        file.handle,
        "",
        owner orelse std.math.maxInt(linux.uid_t),
        group orelse std.math.maxInt(linux.gid_t),
        linux.AT.EMPTY_PATH,
    );
}

fn fileSetPermissions(
    userdata: ?*anyopaque,
    file: File,
    permissions: File.Permissions,
) File.SetPermissionsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    ev.fchmodat(
        &sync,
        file.handle,
        "",
        permissions.toMode(),
        linux.AT.EMPTY_PATH,
    ) catch |err| switch (err) {
        error.NameTooLong => return errnoBug(.NAMETOOLONG),
        error.BadPathName => return errnoBug(.ILSEQ),
        error.ProcessFdQuotaExceeded => return errnoBug(.MFILE),
        error.SystemFdQuotaExceeded => return errnoBug(.NFILE),
        error.OperationUnsupported => return errnoBug(.OPNOTSUPP),
        else => |e| return e,
    };
}

fn fileSetTimestamps(
    userdata: ?*anyopaque,
    file: File,
    options: File.SetTimestampsOptions,
) File.SetTimestampsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    try ev.utimensat(
        &sync,
        file.handle,
        "",
        if (options.modify_timestamp != .now or options.access_timestamp != .now) &.{
            setTimestampToPosix(options.access_timestamp),
            setTimestampToPosix(options.modify_timestamp),
        } else null,
        linux.AT.EMPTY_PATH,
    );
}

fn fileLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    ev.flock(&sync, file.handle, lock, .blocking) catch |err| switch (err) {
        error.WouldBlock => unreachable, // blocking
        else => |e| return e,
    };
}

fn fileTryLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!bool {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    ev.flock(&sync, file.handle, lock, switch (lock) {
        .none => .blocking,
        .shared, .exclusive => .nonblocking,
    }) catch |err| switch (err) {
        error.WouldBlock => return false,
        else => |e| return e,
    };
    return true;
}

fn fileUnlock(userdata: ?*anyopaque, file: File) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = .initBlocked(ev);
    defer sync.deinit(ev);
    ev.flock(&sync, file.handle, .none, .blocking) catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
        error.WouldBlock => unreachable, // blocking
        error.SystemResources => return recoverableOsBugDetected(), // Resource deallocation.
        error.FileLocksUnsupported => return recoverableOsBugDetected(), // We already got the lock.
        error.Unexpected => return recoverableOsBugDetected(), // Resource deallocation must succeed.
    };
}

fn fileDowngradeLock(userdata: ?*anyopaque, file: File) File.DowngradeLockError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    ev.flock(&sync, file.handle, .shared, .nonblocking) catch |err| switch (err) {
        error.WouldBlock => return errnoBug(.AGAIN), // File was not locked in exclusive mode.
        error.SystemResources => return errnoBug(.NOLCK), // Lock already obtained.
        error.FileLocksUnsupported => return errnoBug(.OPNOTSUPP), // Lock already obtained.
        else => |e| return e,
    };
}

fn fileRealPath(userdata: ?*anyopaque, file: File, out_buffer: []u8) File.RealPathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    return ev.realPath(&sync, file.handle, out_buffer);
}

fn fileHardLink(
    userdata: ?*anyopaque,
    file: File,
    new_dir: Dir,
    new_sub_path: []const u8,
    options: File.HardLinkOptions,
) File.HardLinkError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var new_path_buffer: [PATH_MAX]u8 = undefined;
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.linkat(
        &cancel_region,
        file.handle,
        "",
        new_dir.handle,
        new_sub_path_posix,
        linux.AT.EMPTY_PATH | @as(u32, if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW),
    );
}

fn fileMemoryMapCreate(
    userdata: ?*anyopaque,
    file: File,
    options: File.MemoryMap.CreateOptions,
) File.MemoryMap.CreateError!File.MemoryMap {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    const prot: linux.PROT = .{
        .READ = options.protection.read,
        .WRITE = options.protection.write,
        .EXEC = options.protection.execute,
    };
    const flags: linux.MAP = .{
        .TYPE = .SHARED_VALIDATE,
        .POPULATE = options.populate,
    };

    const page_align = std.heap.page_size_min;

    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    const contents = while (true) {
        try sync.cancel_region.await(.nothing);
        const casted_offset = std.math.cast(i64, options.offset) orelse return error.Unseekable;
        const rc = linux.mmap(null, options.len, prot, flags, file.handle, casted_offset);
        switch (linux.errno(rc)) {
            .SUCCESS => break @as([*]align(page_align) u8, @ptrFromInt(rc))[0..options.len],
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .AGAIN => return error.LockedMemoryLimitExceeded,
            .MFILE => return error.ProcessFdQuotaExceeded,
            .NFILE => return error.SystemFdQuotaExceeded,
            .NOMEM => return error.OutOfMemory,
            .PERM => return error.PermissionDenied,
            .OVERFLOW => return error.Unseekable,
            .BADF => |err| return errnoBug(err), // Always a race condition.
            .INVAL => |err| return errnoBug(err), // Invalid parameters to mmap()
            .OPNOTSUPP => |err| return errnoBug(err), // Bad flags with MAP.SHARED_VALIDATE on Linux.
            else => |err| return unexpectedErrno(err),
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
    switch (linux.errno(linux.munmap(memory.ptr, memory.len))) {
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

    const page_size = std.heap.pageSize();
    const alignment: Alignment = .fromByteUnits(page_size);
    const page_align = std.heap.page_size_min;
    const old_memory = mm.memory;

    if (alignment.forward(new_len) == alignment.forward(old_memory.len)) {
        mm.memory.len = new_len;
        return;
    }
    const flags: linux.MREMAP = .{ .MAYMOVE = true };
    const addr_hint: ?[*]const u8 = null;
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    const new_memory = while (true) {
        try sync.cancel_region.await(.nothing);
        const rc = linux.mremap(old_memory.ptr, old_memory.len, new_len, flags, addr_hint);
        switch (linux.errno(rc)) {
            .SUCCESS => break @as([*]align(page_align) u8, @ptrFromInt(rc))[0..new_len],
            .INTR => {},
            .AGAIN => return error.LockedMemoryLimitExceeded,
            .NOMEM => return error.OutOfMemory,
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    };
    mm.memory = new_memory;
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
    return dirOpenFile(ev, .{ .handle = linux.AT.FDCWD }, "/proc/self/exe", flags);
}

fn processExecutablePath(userdata: ?*anyopaque, out_buffer: []u8) process.ExecutablePathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return dirReadLink(ev, .cwd(), "/proc/self/exe", out_buffer) catch |err| switch (err) {
        error.UnsupportedReparsePointType => unreachable, // Windows-only
        error.NetworkNotFound => unreachable, // Windows-only
        error.FileBusy => unreachable, // Windows-only
        else => |e| return e,
    };
}

fn lockStderr(userdata: ?*anyopaque, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!Io.LockedStderr {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const ev_io = ev.io();
    ev.stderr_mutex.lockUncancelable(ev_io);
    errdefer ev.stderr_mutex.unlock(ev_io);
    return ev.initLockedStderr(terminal_mode);
}

fn tryLockStderr(
    userdata: ?*anyopaque,
    terminal_mode: ?Io.Terminal.Mode,
) Io.Cancelable!?Io.LockedStderr {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const ev_io = ev.io();
    if (!ev.stderr_mutex.tryLock()) return null;
    errdefer ev.stderr_mutex.unlock(ev_io);
    return try ev.initLockedStderr(terminal_mode);
}

fn initLockedStderr(ev: *Evented, terminal_mode: ?Io.Terminal.Mode) Io.Cancelable!Io.LockedStderr {
    if (!ev.stderr_writer_initialized) {
        const ev_io = ev.io();
        const cancel_protection = swapCancelProtection(ev, .blocked);
        defer assert(swapCancelProtection(ev, cancel_protection) == .blocked);
        ev.scanEnviron() catch |err| switch (err) {
            error.Canceled => unreachable, // blocked
        };
        const NO_COLOR = ev.environ.exist.NO_COLOR;
        const CLICOLOR_FORCE = ev.environ.exist.CLICOLOR_FORCE;
        ev.stderr_mode = Io.Terminal.Mode.detect(
            ev_io,
            ev.stderr_writer.file,
            NO_COLOR,
            CLICOLOR_FORCE,
        ) catch |err| switch (err) {
            error.Canceled => unreachable, // blocked
        };
        ev.stderr_writer_initialized = true;
    }
    return .{
        .file_writer = &ev.stderr_writer,
        .terminal_mode = terminal_mode orelse ev.stderr_mode,
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
    ev.stderr_writer.interface.buffer = &.{};
    ev.stderr_mutex.unlock(ev.io());
}

fn processCurrentPath(userdata: ?*anyopaque, buffer: []u8) process.CurrentPathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.getcwd(buffer.ptr, buffer.len))) {
            .SUCCESS => return std.mem.findScalar(u8, buffer, 0).?,
            .INTR => {},
            .NOENT => return error.CurrentDirUnlinked,
            .RANGE => return error.NameTooLong,
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn processSetCurrentDir(userdata: ?*anyopaque, dir: Dir) process.SetCurrentDirError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (dir.handle == linux.AT.FDCWD) return;
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    return fchdir(&sync, dir.handle);
}

fn processSetCurrentPath(userdata: ?*anyopaque, dir_path: []const u8) process.SetCurrentPathError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var path_buffer: [PATH_MAX]u8 = undefined;
    const dir_path_posix = try pathToPosix(dir_path, &path_buffer);
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    return chdir(&sync, dir_path_posix);
}

fn processReplace(userdata: ?*anyopaque, options: process.ReplaceOptions) process.ReplaceError {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    try ev.scanEnviron(); // for PATH
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

    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    return execv(&sync, options.expand_arg0, argv_buf.ptr[0].?, argv_buf.ptr, env_block, PATH);
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
    var cancel_region: CancelRegion = .initBlocked();
    defer cancel_region.deinit();
    defer ev.closeAsync(spawned.err_fd);

    // Wait for the child to report any errors in or before `execvpe`.
    var child_err: ForkBailError = undefined;
    ev.readAll(&cancel_region, spawned.err_fd, @ptrCast(&child_err)) catch |read_err| {
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

const prog_fileno = @max(linux.STDIN_FILENO, linux.STDOUT_FILENO, linux.STDERR_FILENO);

const Spawned = struct {
    pid: pid_t,
    err_fd: fd_t,
    stdin: ?File,
    stdout: ?File,
    stderr: ?File,
};
fn spawn(ev: *Evented, options: process.SpawnOptions) process.SpawnError!Spawned {
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();

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
    const pipe_flags: linux.O = .{ .CLOEXEC = true };

    const stdin_pipe = if (options.stdin == .pipe) try pipe2(pipe_flags) else undefined;
    errdefer if (options.stdin == .pipe) {
        ev.destroyPipe(stdin_pipe);
    };

    const stdout_pipe = if (options.stdout == .pipe) try pipe2(pipe_flags) else undefined;
    errdefer if (options.stdout == .pipe) {
        ev.destroyPipe(stdout_pipe);
    };

    const stderr_pipe = if (options.stderr == .pipe) try pipe2(pipe_flags) else undefined;
    errdefer if (options.stderr == .pipe) {
        ev.destroyPipe(stderr_pipe);
    };

    const any_ignore =
        options.stdin == .ignore or options.stdout == .ignore or options.stderr == .ignore;
    const dev_null_fd = if (any_ignore) try ev.null_fd.open(ev, &cancel_region, "/dev/null", .{
        .ACCMODE = .RDWR,
    }) else undefined;

    const prog_pipe: [2]fd_t = if (options.progress_node.index != .none) pipe: {
        // We use CLOEXEC for the same reason as in `pipe_flags`.
        const pipe = try pipe2(.{ .NONBLOCK = true, .CLOEXEC = true });
        _ = linux.fcntl(pipe[0], linux.F.SETPIPE_SZ, @as(u32, std.Progress.max_packet_len * 2));
        break :pipe pipe;
    } else .{ -1, -1 };
    errdefer ev.destroyPipe(prog_pipe);

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
    const err_pipe: [2]fd_t = try pipe2(.{ .CLOEXEC = true });
    errdefer ev.destroyPipe(err_pipe);

    try ev.scanEnviron(); // for PATH
    const PATH = ev.environ.string.PATH orelse default_PATH;

    const pid_result: pid_t = fork: {
        const rc = linux.fork();
        switch (linux.errno(rc)) {
            .SUCCESS => break :fork @intCast(rc),
            .AGAIN => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .NOSYS => return error.OperationUnsupported,
            else => |err| return unexpectedErrno(err),
        }
    };

    if (pid_result == 0) {
        defer comptime unreachable; // We are the child.
        // Note that the parent uring is no longer accessible, so we must no longer reference `ev`.
        var sync: CancelRegion.Sync = .{ .cancel_region = .initBlocked() };
        const err = setUpChild(&sync, .{
            .stdin_pipe = stdin_pipe[0],
            .stdout_pipe = stdout_pipe[1],
            .stderr_pipe = stderr_pipe[1],
            .dev_null_fd = dev_null_fd,
            .prog_pipe = prog_pipe[1],
            .argv_buf = argv_buf,
            .env_block = env_block,
            .PATH = PATH,
            .spawn = options,
        });
        writeAllSync(&sync, err_pipe[1], @ptrCast(&err)) catch {};
        const exit = if (builtin.single_threaded) linux.exit else linux.exit_group;
        exit(1);
    }

    const pid: pid_t = @intCast(pid_result); // We are the parent.
    errdefer comptime unreachable; // The child is forked; we must not error from now on

    ev.closeAsync(err_pipe[1]); // make sure only the child holds the write end open

    if (options.stdin == .pipe) ev.closeAsync(stdin_pipe[0]);
    if (options.stdout == .pipe) ev.closeAsync(stdout_pipe[1]);
    if (options.stderr == .pipe) ev.closeAsync(stderr_pipe[1]);

    if (prog_pipe[1] != -1) ev.closeAsync(prog_pipe[1]);

    options.progress_node.setIpcFile(ev, .{ .handle = prog_pipe[0], .flags = .{ .nonblocking = true } });

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

pub const PipeError = error{
    SystemFdQuotaExceeded,
    ProcessFdQuotaExceeded,
} || Io.UnexpectedError;
pub fn pipe2(flags: linux.O) PipeError![2]fd_t {
    var fds: [2]fd_t = undefined;
    switch (linux.errno(linux.pipe2(&fds, flags))) {
        .SUCCESS => return fds,
        .INVAL => |err| return errnoBug(err), // Invalid flags
        .NFILE => return error.SystemFdQuotaExceeded,
        .MFILE => return error.ProcessFdQuotaExceeded,
        else => |err| return unexpectedErrno(err),
    }
}
fn destroyPipe(ev: *Evented, pipe: [2]fd_t) void {
    if (pipe[0] != -1) ev.closeAsync(pipe[0]);
    if (pipe[0] != pipe[1]) ev.closeAsync(pipe[1]);
}

/// Errors that can occur between fork() and execv()
const ForkBailError = process.SetCurrentDirError || ChdirError ||
    process.SpawnError || process.ReplaceError;
fn setUpChild(sync: *CancelRegion.Sync, options: struct {
    stdin_pipe: fd_t,
    stdout_pipe: fd_t,
    stderr_pipe: fd_t,
    dev_null_fd: fd_t,
    prog_pipe: fd_t,
    argv_buf: [:null]?[*:0]const u8,
    env_block: process.Environ.Block,
    PATH: []const u8,
    spawn: process.SpawnOptions,
}) ForkBailError {
    try setUpChildIo(
        sync,
        options.spawn.stdin,
        options.stdin_pipe,
        linux.STDIN_FILENO,
        options.dev_null_fd,
    );
    try setUpChildIo(
        sync,
        options.spawn.stdout,
        options.stdout_pipe,
        linux.STDOUT_FILENO,
        options.dev_null_fd,
    );
    try setUpChildIo(
        sync,
        options.spawn.stderr,
        options.stderr_pipe,
        linux.STDERR_FILENO,
        options.dev_null_fd,
    );

    switch (options.spawn.cwd) {
        .inherit => {},
        .dir => |cwd_dir| try fchdir(sync, cwd_dir.handle),
        .path => |cwd_path| {
            var cwd_path_buffer: [PATH_MAX]u8 = undefined;
            const cwd_path_posix = try pathToPosix(cwd_path, &cwd_path_buffer);
            try chdir(sync, cwd_path_posix);
        },
    }

    // Must happen after fchdir above, the cwd file descriptor might be
    // equal to prog_fileno and be clobbered by this dup2 call.
    if (options.prog_pipe != -1) try dup2(sync, options.prog_pipe, prog_fileno);

    if (options.spawn.gid) |gid| {
        switch (linux.errno(linux.setregid(gid, gid))) {
            .SUCCESS => {},
            .AGAIN => return error.ResourceLimitReached,
            .INVAL => return error.InvalidUserId,
            .PERM => return error.PermissionDenied,
            else => return error.Unexpected,
        }
    }

    if (options.spawn.uid) |uid| {
        switch (linux.errno(linux.setreuid(uid, uid))) {
            .SUCCESS => {},
            .AGAIN => return error.ResourceLimitReached,
            .INVAL => return error.InvalidUserId,
            .PERM => return error.PermissionDenied,
            else => return error.Unexpected,
        }
    }

    if (options.spawn.pgid) |pid| {
        switch (linux.errno(linux.setpgid(0, pid))) {
            .SUCCESS => {},
            .ACCES => return error.ProcessAlreadyExec,
            .INVAL => return error.InvalidProcessGroupId,
            .PERM => return error.PermissionDenied,
            else => return error.Unexpected,
        }
    }

    if (options.spawn.start_suspended) {
        switch (linux.errno(linux.kill(0, .STOP))) {
            .SUCCESS => {},
            .PERM => return error.PermissionDenied,
            else => return error.Unexpected,
        }
    }

    return execv(
        sync,
        options.spawn.expand_arg0,
        options.argv_buf.ptr[0].?,
        options.argv_buf.ptr,
        options.env_block,
        options.PATH,
    );
}

fn setUpChildIo(
    sync: *CancelRegion.Sync,
    stdio: process.SpawnOptions.StdIo,
    pipe_fd: fd_t,
    std_fileno: i32,
    dev_null_fd: fd_t,
) !void {
    switch (stdio) {
        .pipe => try dup2(sync, pipe_fd, std_fileno),
        .close => _ = linux.close(std_fileno),
        .inherit => {},
        .ignore => try dup2(sync, dev_null_fd, std_fileno),
        .file => |file| try dup2(sync, file.handle, std_fileno),
    }
}

pub const DupError = error{
    ProcessFdQuotaExceeded,
    SystemResources,
} || Io.UnexpectedError || Io.Cancelable;
pub fn dup2(sync: *CancelRegion.Sync, old_fd: fd_t, new_fd: fd_t) DupError!void {
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.dup2(old_fd, new_fd))) {
            .SUCCESS => return,
            .BUSY, .INTR => {},
            .INVAL => |err| return errnoBug(err), // invalid parameters
            .BADF => |err| return errnoBug(err), // use after free
            .MFILE => return error.ProcessFdQuotaExceeded,
            .NOMEM => return error.SystemResources,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn execv(
    sync: *CancelRegion.Sync,
    arg0_expand: process.ArgExpansion,
    file: [*:0]const u8,
    child_argv: [*:null]?[*:0]const u8,
    env_block: process.Environ.PosixBlock,
    PATH: []const u8,
) process.ReplaceError {
    const file_slice = std.mem.sliceTo(file, 0);
    if (std.mem.findScalar(u8, file_slice, '/') != null)
        return execvPath(sync, file, child_argv, env_block);

    // Use of PATH_MAX here is valid as the path_buf will be passed
    // directly to the operating system in posixExecvPath.
    var path_buf: [PATH_MAX]u8 = undefined;
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
        err = execvPath(sync, full_path, child_argv, env_block);
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
pub fn execvPath(
    sync: *CancelRegion.Sync,
    path: [*:0]const u8,
    child_argv: [*:null]const ?[*:0]const u8,
    env_block: process.Environ.PosixBlock,
) process.ReplaceError {
    try sync.cancel_region.await(.nothing);
    switch (linux.errno(linux.execve(path, child_argv, env_block.slice.ptr))) {
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
        .LIBBAD => return error.InvalidExe,
        else => |err| return unexpectedErrno(err),
    }
}

fn childWait(userdata: ?*anyopaque, child: *process.Child) process.Child.WaitError!process.Child.Term {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    defer ev.childCleanup(child);

    const pid = child.id.?;
    var info: linux.siginfo_t = undefined;
    while (true) {
        const thread = try maybe_sync.cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .WAITID,
            .flags = 0,
            .ioprio = 0,
            .fd = pid,
            .off = @intFromPtr(&info),
            .addr = 0,
            .len = @intFromEnum(linux.P.PID),
            .rw_flags = 0,
            .user_data = @intFromPtr(maybe_sync.cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = linux.W.EXITED |
                @as(i32, if (child.request_resource_usage_statistics) linux.W.NOWAIT else 0),
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (maybe_sync.cancel_region.errno()) {
            .SUCCESS => {
                if (child.request_resource_usage_statistics) {
                    const sync = try maybe_sync.enterSync(ev);
                    while (true) {
                        try sync.cancel_region.await(.nothing);
                        var rusage: linux.rusage = undefined;
                        switch (linux.errno(linux.waitid(
                            .PID,
                            pid,
                            &info,
                            linux.W.EXITED | linux.W.NOHANG,
                            &rusage,
                        ))) {
                            .SUCCESS => {
                                child.resource_usage_statistics.rusage = rusage;
                                break;
                            },
                            .INTR, .CANCELED => {},
                            .CHILD => |err| return errnoBug(err), // Double-free.
                            else => |err| return unexpectedErrno(err),
                        }
                    }
                }
                const status: u32 = @bitCast(info.fields.common.second.sigchld.status);
                const code: linux.CLD = @enumFromInt(info.code);
                return switch (code) {
                    .EXITED => .{ .exited = @truncate(status) },
                    .KILLED, .DUMPED => .{ .signal = @enumFromInt(status) },
                    .TRAPPED, .STOPPED => .{ .stopped = @enumFromInt(status) },
                    _, .CONTINUED => .{ .unknown = status },
                };
            },
            .INTR, .CANCELED => {},
            .CHILD => |err| return errnoBug(err), // Double-free.
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn childKill(userdata: ?*anyopaque, child: *process.Child) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var maybe_sync: CancelRegion.Sync.Maybe = .{ .sync = .initBlocked(ev) };
    defer maybe_sync.deinit(ev);
    defer ev.childCleanup(child);

    const pid = child.id.?;
    while (true) switch (linux.errno(linux.kill(pid, .TERM))) {
        .SUCCESS => break,
        .INTR => {},
        .PERM => return,
        .INVAL => |err| return errnoBug(err) catch {},
        .SRCH => |err| return errnoBug(err) catch {},
        else => |err| return unexpectedErrno(err) catch {},
    };
    maybe_sync.leaveSync(ev);

    var info: linux.siginfo_t = undefined;
    while (true) {
        const thread = maybe_sync.cancel_region.awaitIoUring() catch |err| switch (err) {
            error.Canceled => unreachable, // blocked
        };
        thread.enqueue().* = .{
            .opcode = .WAITID,
            .flags = 0,
            .ioprio = 0,
            .fd = pid,
            .off = @intFromPtr(&info),
            .addr = 0,
            .len = @intFromEnum(linux.P.PID),
            .rw_flags = 0,
            .user_data = @intFromPtr(maybe_sync.cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = linux.W.EXITED,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (maybe_sync.cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .CHILD => |err| return errnoBug(err) catch {}, // Double-free.
            else => |err| return unexpectedErrno(err) catch {},
        }
    }
}

fn childCleanup(ev: *Evented, child: *process.Child) void {
    if (child.stdin) |*stdin| {
        ev.closeAsync(stdin.handle);
        child.stdin = null;
    }
    if (child.stdout) |*stdout| {
        ev.closeAsync(stdout.handle);
        child.stdout = null;
    }
    if (child.stderr) |*stderr| {
        ev.closeAsync(stderr.handle);
        child.stderr = null;
    }
    child.id = null;
}

fn progressParentFile(userdata: ?*anyopaque) std.Progress.ParentFileError!File {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const cancel_protection = swapCancelProtection(ev, .blocked);
    defer assert(swapCancelProtection(ev, cancel_protection) == .blocked);
    ev.scanEnviron() catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    };
    return ev.environ.zig_progress_file;
}

fn scanEnviron(ev: *Evented) Io.Cancelable!void {
    const ev_io = ev.io();
    try ev.environ_mutex.lock(ev_io);
    defer ev.environ_mutex.unlock(ev_io);
    if (ev.environ_initialized) return;
    ev.environ.scan(ev.allocator());
    ev.environ_initialized = true;
}

fn clockResolution(userdata: ?*anyopaque, clock: Io.Clock) Io.Clock.ResolutionError!Io.Duration {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const clock_id = clockToPosix(clock);
    var timespec: linux.timespec = undefined;
    return switch (linux.errno(linux.clock_getres(clock_id, &timespec))) {
        .SUCCESS => .fromNanoseconds(nanosecondsFromPosix(&timespec)),
        .INVAL => return error.ClockUnavailable,
        else => |err| return unexpectedErrno(err),
    };
}

fn now(userdata: ?*anyopaque, clock: Io.Clock) Io.Timestamp {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    var tp: linux.timespec = undefined;
    switch (linux.errno(linux.clock_gettime(clockToPosix(clock), &tp))) {
        .SUCCESS => return timestampFromPosix(&tp),
        else => return .zero,
    }
}

fn sleep(userdata: ?*anyopaque, timeout: Io.Timeout) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    const timespec: linux.kernel_timespec, const clock: Io.Clock, const timeout_flags: u32 = timespec: switch (timeout) {
        .none => .{
            .{
                .sec = std.math.maxInt(i64),
                .nsec = std.time.ns_per_s - 1,
            },
            .awake,
            linux.IORING_TIMEOUT_ABS,
        },
        .duration => |duration| {
            const ns = duration.raw.toNanoseconds();
            break :timespec .{
                .{
                    .sec = @intCast(@divFloor(ns, std.time.ns_per_s)),
                    .nsec = @intCast(@mod(ns, std.time.ns_per_s)),
                },
                duration.clock,
                0,
            };
        },
        .deadline => |deadline| {
            const ns = deadline.raw.toNanoseconds();
            break :timespec .{
                .{
                    .sec = @intCast(@divFloor(ns, std.time.ns_per_s)),
                    .nsec = @intCast(@mod(ns, std.time.ns_per_s)),
                },
                deadline.clock,
                linux.IORING_TIMEOUT_ABS,
            };
        },
    };
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    const thread = try cancel_region.awaitIoUring();
    thread.enqueue().* = .{
        .opcode = .TIMEOUT,
        .flags = 0,
        .ioprio = 0,
        .fd = 0,
        .off = 0,
        .addr = @intFromPtr(&timespec),
        .len = 1,
        .rw_flags = timeout_flags | @as(u32, switch (clock) {
            .real => linux.IORING_TIMEOUT_REALTIME,
            else => 0,
            .boot => linux.IORING_TIMEOUT_BOOTTIME,
        }),
        .user_data = @intFromPtr(cancel_region.fiber),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = 0,
        .resv = 0,
    };
    ev.yield(null, .nothing);
    // Handles SUCCESS as well as clock not available and unexpected
    // errors. The user had a chance to check clock resolution before
    // getting here, which would have reported 0, making this a legal
    // amount of time to sleep.
}

fn random(userdata: ?*anyopaque, buffer: []u8) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var thread: *Thread = .current();
    if (!thread.csprng.isInitialized()) {
        @branchHint(.unlikely);
        var seed: [Csprng.seed_len]u8 = undefined;
        {
            const ev_io = ev.io();
            ev.csprng_mutex.lockUncancelable(ev_io);
            defer ev.csprng_mutex.unlock(ev_io);
            if (!ev.csprng.isInitialized()) {
                @branchHint(.unlikely);
                var cancel_region: CancelRegion = .initBlocked();
                defer cancel_region.deinit();
                ev.urandomReadAll(&cancel_region, &seed) catch |err| switch (err) {
                    error.Canceled => unreachable, // blocked
                    else => fallbackSeed(ev, &seed),
                };
                ev.csprng.rng = .init(seed);
                thread = .current();
            }
            ev.csprng.rng.fill(&seed);
        }
        if (!thread.csprng.isInitialized()) {
            @branchHint(.likely);
            thread.csprng.rng = .init(seed);
        } else thread.csprng.rng.addEntropy(&seed);
    }
    thread.csprng.rng.fill(buffer);
}

fn randomSecure(userdata: ?*anyopaque, buffer: []u8) Io.RandomSecureError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (buffer.len == 0) return;
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    ev.urandomReadAll(&cancel_region, buffer) catch |err| switch (err) {
        error.Canceled => return error.Canceled,
        else => return error.EntropyUnavailable,
    };
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

fn netBindIp(
    userdata: ?*anyopaque,
    address: *const net.IpAddress,
    options: net.IpAddress.BindOptions,
) net.IpAddress.BindError!net.Socket {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const family = posixAddressFamily(address);
    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    const socket_fd = try ev.socket(&maybe_sync.cancel_region, family, options);
    errdefer ev.closeAsync(socket_fd);
    var storage: PosixAddress = undefined;
    var addr_len = addressToPosix(address, &storage);
    try ev.bind(&maybe_sync.cancel_region, socket_fd, &storage.any, addr_len);
    if (options.allow_broadcast) try ev.setsockopt(&maybe_sync.cancel_region, socket_fd, linux.SOL.SOCKET, linux.SO.BROADCAST, 1);
    try ev.getsockname(try maybe_sync.enterSync(ev), socket_fd, &storage.any, &addr_len);
    return .{ .handle = socket_fd, .address = addressFromPosix(&storage) };
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

fn netReceive(
    ev: *Evented,
    cancel_region: *CancelRegion,
    handle: net.Socket.Handle,
    message_buffer: []net.IncomingMessage,
    data_buffer: []u8,
    flags: net.ReceiveFlags,
) struct { ?net.Socket.ReceiveError, usize } {
    var message_i: usize = 0;
    var data_i: usize = 0;

    while (true) {
        if (message_buffer.len - message_i == 0) return .{ null, message_i };
        const message = &message_buffer[message_i];
        const remaining_data_buffer = data_buffer[data_i..];
        var storage: PosixAddress = undefined;
        var iov: iovec = .{ .base = remaining_data_buffer.ptr, .len = remaining_data_buffer.len };
        var msg: linux.msghdr = .{
            .name = &storage.any,
            .namelen = @sizeOf(PosixAddress),
            .iov = (&iov)[0..1],
            .iovlen = 1,
            .control = message.control.ptr,
            .controllen = @intCast(message.control.len),
            .flags = undefined,
        };

        const thread = cancel_region.awaitIoUring() catch |err| return .{ err, message_i };
        thread.enqueue().* = .{
            .opcode = .RECVMSG,
            .flags = 0,
            .ioprio = 0,
            .fd = handle,
            .off = 0,
            .addr = @intFromPtr(&msg),
            .len = 0,
            .rw_flags = linux.MSG.NOSIGNAL |
                @as(u32, if (flags.oob) linux.MSG.OOB else 0) |
                @as(u32, if (flags.peek) linux.MSG.PEEK else 0) |
                @as(u32, if (flags.trunc) linux.MSG.TRUNC else 0),
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        const completion = cancel_region.completion();
        switch (completion.errno()) {
            .SUCCESS => {
                const data = remaining_data_buffer[0..@intCast(completion.result)];
                data_i += data.len;
                message.* = .{
                    .from = addressFromPosix(&storage),
                    .data = data,
                    .control = if (msg.control) |ptr| @as([*]u8, @ptrCast(ptr))[0..msg.controllen] else message.control,
                    .flags = .{
                        .eor = msg.flags & linux.MSG.EOR != 0,
                        .trunc = msg.flags & linux.MSG.TRUNC != 0,
                        .ctrunc = msg.flags & linux.MSG.CTRUNC != 0,
                        .oob = msg.flags & linux.MSG.OOB != 0,
                        .errqueue = msg.flags & linux.MSG.ERRQUEUE != 0,
                    },
                };
                message_i += 1;
                continue;
            },
            .AGAIN => unreachable,
            .INTR, .CANCELED => {},
            .BADF => |err| return .{ errnoBug(err), message_i },
            .NFILE => return .{ error.SystemFdQuotaExceeded, message_i },
            .MFILE => return .{ error.ProcessFdQuotaExceeded, message_i },
            .FAULT => |err| return .{ errnoBug(err), message_i },
            .INVAL => |err| return .{ errnoBug(err), message_i },
            .NOBUFS => return .{ error.SystemResources, message_i },
            .NOMEM => return .{ error.SystemResources, message_i },
            .NOTCONN => return .{ error.SocketUnconnected, message_i },
            .NOTSOCK => |err| return .{ errnoBug(err), message_i },
            .MSGSIZE => return .{ error.MessageOversize, message_i },
            .PIPE => return .{ error.SocketUnconnected, message_i },
            .OPNOTSUPP => |err| return .{ errnoBug(err), message_i },
            .CONNRESET => return .{ error.ConnectionResetByPeer, message_i },
            .NETDOWN => return .{ error.NetworkDown, message_i },
            else => |err| return .{ unexpectedErrno(err), message_i },
        }
    }
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
    for (handles) |handle| ev.close(handle);
}

fn netShutdown(
    userdata: ?*anyopaque,
    handle: net.Socket.Handle,
    how: net.ShutdownHow,
) net.ShutdownError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .SHUTDOWN,
            .flags = 0,
            .ioprio = 0,
            .fd = handle,
            .off = 0,
            .addr = 0,
            .len = switch (how) {
                .recv => linux.SHUT.RD,
                .send => linux.SHUT.WR,
                .both => linux.SHUT.RDWR,
            },
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .BADF, .NOTSOCK, .INVAL => |err| return errnoBug(err),
            .NOTCONN => return error.SocketUnconnected,
            .NOBUFS => return error.SystemResources,
            else => |err| return unexpectedErrno(err),
        }
    }
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

fn bind(
    ev: *Evented,
    cancel_region: *CancelRegion,
    socket_fd: fd_t,
    addr: *const linux.sockaddr,
    addr_len: linux.socklen_t,
) !void {
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .BIND,
            .flags = 0,
            .ioprio = 0,
            .fd = socket_fd,
            .off = addr_len,
            .addr = @intFromPtr(addr),
            .len = 0,
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .ADDRINUSE => return error.AddressInUse,
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .INVAL => |err| return errnoBug(err), // invalid parameters
            .NOTSOCK => |err| return errnoBug(err), // invalid `sockfd`
            .AFNOSUPPORT => return error.AddressFamilyUnsupported,
            .ADDRNOTAVAIL => return error.AddressUnavailable,
            .FAULT => |err| return errnoBug(err), // invalid `addr` pointer
            .NOMEM => return error.SystemResources,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn chdir(sync: *CancelRegion.Sync, path: [*:0]const u8) ChdirError!void {
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.chdir(path))) {
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
        }
    }
}

fn close(ev: *Evented, fd: fd_t) void {
    var cancel_region: CancelRegion = .initBlocked();
    defer cancel_region.deinit();
    const thread = cancel_region.awaitIoUring() catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    };
    thread.enqueue().* = .{
        .opcode = .CLOSE,
        .flags = 0,
        .ioprio = 0,
        .fd = fd,
        .off = 0,
        .addr = 0,
        .len = 0,
        .rw_flags = 0,
        .user_data = @intFromPtr(cancel_region.fiber),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = 0,
        .resv = 0,
    };
    ev.yield(null, .nothing);
    switch (cancel_region.errno()) {
        .BADF => recoverableOsBugDetected(), // Always a race condition.
        .INTR => {}, // This is still a success. See https://github.com/ziglang/zig/issues/2425
        else => {},
    }
}

fn closeAsync(ev: *Evented, fd: fd_t) void {
    _ = ev;
    const thread: *Thread = .current();
    thread.enqueue().* = .{
        .opcode = .CLOSE,
        .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
        .ioprio = 0,
        .fd = fd,
        .off = 0,
        .addr = 0,
        .len = 0,
        .rw_flags = 0,
        .user_data = @intFromEnum(Completion.Userdata.close),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = 0,
        .resv = 0,
    };
}

fn fchdir(sync: *CancelRegion.Sync, dir: fd_t) process.SetCurrentDirError!void {
    if (dir == linux.AT.FDCWD) return;
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.fchdir(dir))) {
            .SUCCESS => return,
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .NOTDIR => return error.NotDir,
            .IO => return error.FileSystem,
            .BADF => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fchmodat(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    dir: fd_t,
    path: [*:0]const u8,
    mode: linux.mode_t,
    flags: u32,
) Dir.SetFilePermissionsError!void {
    _ = ev;
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.fchmodat2(dir, path, mode, flags))) {
            .SUCCESS => return,
            .INTR => {},
            .BADF => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .ACCES => return error.AccessDenied,
            .IO => return error.InputOutput,
            .LOOP => return error.SymLinkLoop,
            .NOENT => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOTDIR => return error.FileNotFound,
            .OPNOTSUPP => return error.OperationUnsupported,
            .PERM => return error.PermissionDenied,
            .ROFS => return error.ReadOnlyFileSystem,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fchownat(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    dir: fd_t,
    path: [*:0]const u8,
    owner: linux.uid_t,
    group: linux.gid_t,
    flags: u32,
) File.SetOwnerError!void {
    _ = ev;
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.fchownat(dir, path, owner, group, flags))) {
            .SUCCESS => return,
            .INTR => {},
            .BADF => |err| return errnoBug(err), // likely fd refers to directory opened without `Dir.OpenOptions.iterate`
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .ACCES => return error.AccessDenied,
            .IO => return error.InputOutput,
            .LOOP => return error.SymLinkLoop,
            .NOENT => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOTDIR => return error.FileNotFound,
            .PERM => return error.PermissionDenied,
            .ROFS => return error.ReadOnlyFileSystem,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn flock(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    fd: fd_t,
    op: File.Lock,
    blocking: enum { blocking, nonblocking },
) (File.LockError || error{WouldBlock})!void {
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.flock(fd, LOCK.NB | @as(i32, switch (op) {
            .none => LOCK.UN,
            .shared => LOCK.SH,
            .exclusive => LOCK.EX,
        })))) {
            .SUCCESS => return,
            .INTR => {},
            .BADF => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err), // invalid parameters
            .NOLCK => return error.SystemResources,
            .AGAIN => {
                const thread = try sync.cancel_region.awaitIoUring();
                thread.enqueue().* = .{
                    .opcode = .NOP,
                    .flags = 0,
                    .ioprio = 0,
                    .fd = 0,
                    .off = 0,
                    .addr = 0,
                    .len = 0,
                    .rw_flags = 0,
                    .user_data = @intFromPtr(sync.cancel_region.fiber),
                    .buf_index = 0,
                    .personality = 0,
                    .splice_fd_in = 0,
                    .addr3 = 0,
                    .resv = 0,
                };
                ev.yield(null, .nothing);
                switch (sync.cancel_region.errno()) {
                    .SUCCESS, .INTR, .CANCELED => {},
                    else => unreachable,
                }
                switch (blocking) {
                    .blocking => continue,
                    .nonblocking => return error.WouldBlock,
                }
            },
            .OPNOTSUPP => return error.FileLocksUnsupported,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn getsockname(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    socket_fd: fd_t,
    addr: *linux.sockaddr,
    addr_len: *linux.socklen_t,
) !void {
    _ = ev;
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.getsockname(socket_fd, addr, addr_len))) {
            .SUCCESS => return,
            .INTR => {},
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err), // invalid parameters
            .NOTSOCK => |err| return errnoBug(err), // always a race condition
            .NOBUFS => return error.SystemResources,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn linkat(
    ev: *Evented,
    cancel_region: *CancelRegion,
    old_dir: fd_t,
    old_path: [*:0]const u8,
    new_dir: fd_t,
    new_path: [*:0]const u8,
    flags: u32,
) File.HardLinkError!void {
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .LINKAT,
            .flags = 0,
            .ioprio = 0,
            .fd = old_dir,
            .off = @intFromPtr(new_path),
            .addr = @intFromPtr(old_path),
            .len = @bitCast(new_dir),
            .rw_flags = flags,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
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
        }
    }
}

fn lseek(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    fd: fd_t,
    offset: u64,
    whence: u32,
) File.SeekError!void {
    _ = ev;
    while (true) {
        try sync.cancel_region.await(.nothing);
        var result: u64 = undefined;
        switch (linux.errno(switch (@sizeOf(usize)) {
            else => comptime unreachable,
            4 => linux.llseek(fd, offset, &result, whence),
            8 => linux.lseek(fd, @bitCast(offset), whence),
        })) {
            .SUCCESS => return,
            .INTR => {},
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .INVAL => return error.Unseekable,
            .OVERFLOW => return error.Unseekable,
            .SPIPE => return error.Unseekable,
            .NXIO => return error.Unseekable,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn openat(
    ev: *Evented,
    cancel_region: *CancelRegion,
    dir: fd_t,
    path: [*:0]const u8,
    flags: linux.O,
    mode: linux.mode_t,
) !fd_t {
    var mut_flags = flags;
    if (@hasField(linux.O, "LARGEFILE")) mut_flags.LARGEFILE = true;
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .OPENAT,
            .flags = 0,
            .ioprio = 0,
            .fd = dir,
            .off = 0,
            .addr = @intFromPtr(path),
            .len = mode,
            .rw_flags = @bitCast(mut_flags),
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        const completion = cancel_region.completion();
        switch (completion.errno()) {
            .SUCCESS => return completion.result,
            .INTR, .CANCELED => {},
            .FAULT => |err| return errnoBug(err),
            .INVAL => return error.BadPathName,
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .ACCES => return error.AccessDenied,
            .FBIG => return error.FileTooBig,
            .OVERFLOW => return error.FileTooBig,
            .ISDIR => return error.IsDir,
            .LOOP => return error.SymLinkLoop,
            .MFILE => return error.ProcessFdQuotaExceeded,
            .NAMETOOLONG => return error.NameTooLong,
            .NFILE => return error.SystemFdQuotaExceeded,
            .NODEV => return error.NoDevice,
            .NOENT => return error.FileNotFound,
            .SRCH => return error.FileNotFound, // Linux when opening procfs files.
            .NOMEM => return error.SystemResources,
            .NOSPC => return error.NoSpaceLeft,
            .NOTDIR => return error.NotDir,
            .PERM => return error.PermissionDenied,
            .EXIST => return error.PathAlreadyExists,
            .BUSY => return error.DeviceBusy,
            // This can be triggered by file locking and TMPFILE, but those
            // flags are mutually exclusive.
            .OPNOTSUPP => return error.OperationUnsupported,
            .AGAIN => return error.WouldBlock,
            .TXTBSY => return error.FileBusy,
            .NXIO => return error.NoDevice,
            .ROFS => return error.ReadOnlyFileSystem,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn preadv(
    ev: *Evented,
    cancel_region: *CancelRegion,
    fd: fd_t,
    iov: []const iovec,
    offset: ?u64,
) File.Reader.Error!usize {
    if (iov.len == 0) return 0;
    const gather = iov.len > 1 or iov[0].len > 0xfffff000;
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = if (gather) .READV else .READ,
            .flags = 0,
            .ioprio = 0,
            .fd = fd,
            .off = offset orelse std.math.maxInt(u64),
            .addr = if (gather) @intFromPtr(iov.ptr) else @intFromPtr(iov[0].base),
            .len = @intCast(if (gather) iov.len else iov[0].len),
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        const completion = cancel_region.completion();
        switch (completion.errno()) {
            .SUCCESS => return @as(u32, @bitCast(completion.result)),
            .INTR, .CANCELED => {},
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            .AGAIN => return error.WouldBlock,
            .BADF => |err| return errnoBug(err), // File descriptor used after closed
            .IO => return error.InputOutput,
            .ISDIR => return error.IsDir,
            .NOBUFS => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .NOTCONN => return error.SocketUnconnected,
            .CONNRESET => return error.ConnectionResetByPeer,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn pwritev(
    ev: *Evented,
    cancel_region: *CancelRegion,
    fd: fd_t,
    iov: []const iovec_const,
    offset: ?u64,
) File.Writer.Error!usize {
    if (iov.len == 0) return 0;
    const scatter = iov.len > 1 or iov[0].len > 0xfffff000;
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = if (scatter) .WRITEV else .WRITE,
            .flags = 0,
            .ioprio = 0,
            .fd = fd,
            .off = offset orelse std.math.maxInt(u64),
            .addr = if (scatter) @intFromPtr(iov.ptr) else @intFromPtr(iov[0].base),
            .len = @intCast(if (scatter) iov.len else iov[0].len),
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        const completion = cancel_region.completion();
        switch (completion.errno()) {
            .SUCCESS => return @as(u32, @bitCast(completion.result)),
            .INTR, .CANCELED => {},
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            .AGAIN => return error.WouldBlock,
            .BADF => return error.NotOpenForWriting, // Can be a race condition.
            .DESTADDRREQ => |err| return errnoBug(err), // `connect` was never called.
            .DQUOT => return error.DiskQuota,
            .FBIG => return error.FileTooBig,
            .IO => return error.InputOutput,
            .NOSPC => return error.NoSpaceLeft,
            .PERM => return error.PermissionDenied,
            .PIPE => return error.BrokenPipe,
            .CONNRESET => |err| return errnoBug(err), // Not a socket handle.
            .BUSY => return error.DeviceBusy,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn readAll(
    ev: *Evented,
    cancel_region: *CancelRegion,
    fd: fd_t,
    buffer: []u8,
) (File.Reader.Error || error{EndOfStream})!void {
    var index: usize = 0;
    while (buffer.len - index != 0) {
        const len = try ev.preadv(cancel_region, fd, &.{
            .{ .base = buffer[index..].ptr, .len = buffer.len - index },
        }, null);
        if (len == 0) return error.EndOfStream;
        index += len;
    }
}

fn realPath(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    fd: fd_t,
    out_buffer: []u8,
) File.RealPathError!usize {
    _ = ev;
    var procfs_buf: [std.fmt.count("/proc/self/fd/{d}\x00", .{std.math.minInt(fd_t)})]u8 = undefined;
    const proc_path = std.fmt.bufPrintSentinel(&procfs_buf, "/proc/self/fd/{d}", .{fd}, 0) catch
        unreachable;
    while (true) {
        try sync.cancel_region.await(.nothing);
        const rc = linux.readlink(proc_path, out_buffer.ptr, out_buffer.len);
        switch (linux.errno(rc)) {
            .SUCCESS => return rc,
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .FAULT => |err| return errnoBug(err),
            .IO => return error.FileSystem,
            .LOOP => return error.SymLinkLoop,
            .NAMETOOLONG => return error.NameTooLong,
            .NOENT => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOTDIR => return error.NotDir,
            .ILSEQ => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn renameat(
    ev: *Evented,
    cancel_region: *CancelRegion,
    old_dir: fd_t,
    old_path: [*:0]const u8,
    new_dir: fd_t,
    new_path: [*:0]const u8,
    flags: linux.RENAME,
) Dir.RenameError!void {
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .RENAMEAT,
            .flags = 0,
            .ioprio = 0,
            .fd = old_dir,
            .off = @intFromPtr(new_path),
            .addr = @intFromPtr(old_path),
            .len = @bitCast(new_dir),
            .rw_flags = @bitCast(flags),
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .ACCES => return error.AccessDenied,
            .PERM => return error.PermissionDenied,
            .BUSY => return error.FileBusy,
            .DQUOT => return error.DiskQuota,
            .ISDIR => return error.IsDir,
            .IO => return error.HardwareFailure,
            .LOOP => return error.SymLinkLoop,
            .MLINK => return error.LinkQuotaExceeded,
            .NAMETOOLONG => return error.NameTooLong,
            .NOENT => return error.FileNotFound,
            .NOTDIR => return error.NotDir,
            .NOMEM => return error.SystemResources,
            .NOSPC => return error.NoSpaceLeft,
            .EXIST => return error.DirNotEmpty,
            .NOTEMPTY => return error.DirNotEmpty,
            .ROFS => return error.ReadOnlyFileSystem,
            .XDEV => return error.CrossDevice,
            .ILSEQ => return error.BadPathName,
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn setsockopt(
    ev: *Evented,
    cancel_region: *CancelRegion,
    fd: fd_t,
    level: i32,
    opt_name: u32,
    option: u32,
) !void {
    const o: []const u8 = @ptrCast(&option);
    while (true) {
        const off: extern struct {
            cmd_op: linux.IO_URING_SOCKET_OP,
            pad: u32,
        } align(@alignOf(u64)) = .{
            .cmd_op = .SETSOCKOPT,
            .pad = 0,
        };
        const addr: extern struct { level: i32, opt_name: u32 } align(@alignOf(u64)) = .{
            .level = level,
            .opt_name = opt_name,
        };
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .URING_CMD,
            .flags = 0,
            .ioprio = 0,
            .fd = fd,
            .off = @as(*const u64, @ptrCast(&off)).*,
            .addr = @as(*const u64, @ptrCast(&addr)).*,
            .len = 0,
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = @intCast(o.len),
            .addr3 = @intFromPtr(o.ptr),
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return,
            .INTR, .CANCELED => {},
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .NOTSOCK => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn socket(
    ev: *Evented,
    cancel_region: *CancelRegion,
    family: linux.sa_family_t,
    options: net.IpAddress.BindOptions,
) error{
    AddressFamilyUnsupported,
    ProtocolUnsupportedBySystem,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    SystemResources,
    ProtocolUnsupportedByAddressFamily,
    SocketModeUnsupported,
    OptionUnsupported,
    Unexpected,
    Canceled,
}!fd_t {
    const mode, const protocol = try posixSocketModeProtocol(family, options.mode, options.protocol);
    const socket_fd = while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .SOCKET,
            .flags = 0,
            .ioprio = 0,
            .fd = family,
            .off = mode | linux.SOCK.CLOEXEC,
            .addr = 0,
            .len = protocol,
            .rw_flags = 0,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        const completion = cancel_region.completion();
        switch (completion.errno()) {
            .SUCCESS => break completion.result,
            .INTR, .CANCELED => {},
            .AFNOSUPPORT => return error.AddressFamilyUnsupported,
            .INVAL => return error.ProtocolUnsupportedBySystem,
            .MFILE => return error.ProcessFdQuotaExceeded,
            .NFILE => return error.SystemFdQuotaExceeded,
            .NOBUFS => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .PROTONOSUPPORT => return error.ProtocolUnsupportedByAddressFamily,
            .PROTOTYPE => return error.SocketModeUnsupported,
            else => |err| return unexpectedErrno(err),
        }
    };
    errdefer ev.closeAsync(socket_fd);

    if (options.ip6_only) {
        if (linux.IPV6 == void) return error.OptionUnsupported;
        try ev.setsockopt(cancel_region, socket_fd, linux.IPPROTO.IPV6, linux.IPV6.V6ONLY, 0);
    }

    return socket_fd;
}

fn stat(ev: *Evented, cancel_region: *CancelRegion, fd: fd_t) Dir.StatError!Dir.Stat {
    return ev.statx(cancel_region, fd, "", linux.AT.EMPTY_PATH) catch |err| switch (err) {
        error.BadPathName, error.NameTooLong => unreachable, // path is empty
        error.AccessDenied => return errnoBug(.ACCES),
        error.SymLinkLoop => return errnoBug(.LOOP),
        error.FileNotFound => return errnoBug(.NOENT),
        error.NotDir => return errnoBug(.NOTDIR),
        else => |e| return e,
    };
}

fn statx(
    ev: *Evented,
    cancel_region: *CancelRegion,
    dir: fd_t,
    path: [*:0]const u8,
    flags: u32,
) (Dir.StatError || Dir.PathNameError || error{ FileNotFound, NotDir, SymLinkLoop })!Dir.Stat {
    while (true) {
        var statx_buf = std.mem.zeroes(linux.Statx);
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .STATX,
            .flags = 0,
            .ioprio = 0,
            .fd = dir,
            .off = @intFromPtr(&statx_buf),
            .addr = @intFromPtr(path),
            .len = @bitCast(linux_statx_request),
            .rw_flags = flags,
            .user_data = @intFromPtr(cancel_region.fiber),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        ev.yield(null, .nothing);
        switch (cancel_region.errno()) {
            .SUCCESS => return statFromLinux(&statx_buf),
            .INTR, .CANCELED => {},
            .ACCES => return error.AccessDenied,
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .LOOP => return error.SymLinkLoop,
            .NAMETOOLONG => |err| return errnoBug(err),
            .NOENT => return error.FileNotFound,
            .NOTDIR => return error.NotDir,
            .NOMEM => return error.SystemResources,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn urandomReadAll(
    ev: *Evented,
    cancel_region: *CancelRegion,
    buffer: []u8,
) (File.OpenError || File.Reader.Error || error{EndOfStream})!void {
    return ev.readAll(cancel_region, try ev.random_fd.open(ev, cancel_region, "/dev/urandom", .{
        .ACCMODE = .RDONLY,
        .CLOEXEC = true,
    }), buffer);
}

fn utimensat(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    dir: fd_t,
    path: [*:0]const u8,
    times: ?*const [2]linux.timespec,
    flags: u32,
) File.SetTimestampsError!void {
    _ = ev;
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.utimensat(dir, path, times, flags))) {
            .SUCCESS => return,
            .INTR => {},
            .BADF => |err| return errnoBug(err), // always a race condition
            .FAULT => |err| return errnoBug(err),
            .INVAL => |err| return errnoBug(err),
            .ACCES => return error.AccessDenied,
            .PERM => return error.PermissionDenied,
            .ROFS => return error.ReadOnlyFileSystem,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn writeAllSync(sync: *CancelRegion.Sync, fd: fd_t, buffer: []const u8) File.Writer.Error!void {
    var index: usize = 0;
    while (buffer.len - index != 0) index += try writeSync(sync, fd, buffer[index..]);
}

fn writeSync(sync: *CancelRegion.Sync, fd: fd_t, buffer: []const u8) File.Writer.Error!usize {
    while (true) {
        try sync.cancel_region.await(.nothing);
        const rc = linux.write(fd, buffer.ptr, buffer.len);
        switch (linux.errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .INTR => {},
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            .AGAIN => return error.WouldBlock,
            .BADF => return error.NotOpenForWriting, // Can be a race condition.
            .DESTADDRREQ => |err| return errnoBug(err), // `connect` was never called.
            .DQUOT => return error.DiskQuota,
            .FBIG => return error.FileTooBig,
            .IO => return error.InputOutput,
            .NOSPC => return error.NoSpaceLeft,
            .PERM => return error.PermissionDenied,
            .PIPE => return error.BrokenPipe,
            .CONNRESET => |err| return errnoBug(err), // Not a socket handle.
            .BUSY => return error.DeviceBusy,
            else => |err| return unexpectedErrno(err),
        }
    }
}

test {
    _ = Fiber.CancelProtection;
}



---
File: /std/Io/Writer.zig
---

const Writer = @This();

const builtin = @import("builtin");
const native_endian = builtin.target.cpu.arch.endian();

const std = @import("../std.zig");
const assert = std.debug.assert;
const Limit = std.Io.Limit;
const File = std.Io.File;
const testing = std.testing;
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;

vtable: *const VTable,
/// If this has length zero, the writer is unbuffered, and `flush` is a no-op.
buffer: []u8,
/// In `buffer` before this are buffered bytes, after this is `undefined`.
end: usize = 0,

pub const VTable = struct {
    /// Sends bytes to the logical sink. A write will only be sent here if it
    /// could not fit into `buffer`, or during a `flush` operation.
    ///
    /// `buffer[0..end]` is consumed first, followed by each slice of `data` in
    /// order. Elements of `data` may alias each other but may not alias
    /// `buffer`.
    ///
    /// This function modifies `Writer.end` and `Writer.buffer` in an
    /// implementation-defined manner.
    ///
    /// `data.len` must be nonzero.
    ///
    /// The last element of `data` is repeated as necessary so that it is
    /// written `splat` number of times, which may be zero.
    ///
    /// This function may not be called if the data to be written could have
    /// been stored in `buffer` instead, including when the amount of data to
    /// be written is zero and the buffer capacity is zero.
    ///
    /// Number of bytes consumed from `data` is returned, excluding bytes from
    /// `buffer`.
    ///
    /// Number of bytes returned may be zero, which does not indicate stream
    /// end. A subsequent call may return nonzero, or signal end of stream via
    /// `error.WriteFailed`.
    drain: *const fn (w: *Writer, data: []const []const u8, splat: usize) Error!usize,

    /// Copies contents from an open file to the logical sink. `buffer[0..end]`
    /// is consumed first, followed by `limit` bytes from `file_reader`.
    ///
    /// Number of bytes logically written is returned. This excludes bytes from
    /// `buffer` because they have already been logically written. Number of
    /// bytes consumed from `buffer` are tracked by modifying `end`.
    ///
    /// Number of bytes returned may be zero, which does not indicate stream
    /// end. A subsequent call may return nonzero, or signal end of stream via
    /// `error.WriteFailed`. Caller may check `file_reader` state
    /// (`File.Reader.atEnd`) to disambiguate between a zero-length read or
    /// write, and whether the file reached the end.
    ///
    /// `error.Unimplemented` indicates the callee cannot offer a more
    /// efficient implementation than the caller performing its own reads.
    sendFile: *const fn (
        w: *Writer,
        file_reader: *File.Reader,
        /// Maximum amount of bytes to read from the file. Implementations m
```
