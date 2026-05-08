```
 .exclusive => true,
    };
    syscall = try .start();
    while (true) switch (w.ntdll.NtLockFile(
        handle,
        null,
        null,
        null,
        &io_status_block,
        &windows_lock_range_off,
        &windows_lock_range_len,
        null,
        .fromBool(flags.lock_nonblocking),
        .fromBool(exclusive),
    )) {
        .SUCCESS => break syscall.finish(),
        .INSUFFICIENT_RESOURCES => return syscall.fail(error.SystemResources),
        .LOCK_NOT_GRANTED => return syscall.fail(error.WouldBlock),
        .ACCESS_VIOLATION => |err| return syscall.ntstatusBug(err), // bad io_status_block pointer
        else => |status| return syscall.unexpectedNtstatus(status),
    };
    return .{
        .handle = handle,
        .flags = .{ .nonblocking = false },
    };
}

fn dirOpenFileWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: Dir.OpenFileOptions,
) File.OpenError!File {
    if (builtin.link_libc) return dirOpenFilePosix(userdata, dir, sub_path, flags);
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const wasi = std.os.wasi;
    var base: std.os.wasi.rights_t = .{};
    // POLL_FD_READWRITE only grants extra rights if the corresponding FD_READ and/or FD_WRITE
    // is also set.
    if (flags.isRead()) {
        base.FD_READ = true;
        base.FD_TELL = true;
        base.FD_SEEK = true;
        base.FD_FILESTAT_GET = true;
        base.POLL_FD_READWRITE = true;
    }
    if (flags.isWrite()) {
        base.FD_WRITE = true;
        base.FD_TELL = true;
        base.FD_SEEK = true;
        base.FD_DATASYNC = true;
        base.FD_FDSTAT_SET_FLAGS = true;
        base.FD_SYNC = true;
        base.FD_ALLOCATE = true;
        base.FD_ADVISE = true;
        base.FD_FILESTAT_SET_TIMES = true;
        base.FD_FILESTAT_SET_SIZE = true;
        base.POLL_FD_READWRITE = true;
    }
    const lookup_flags: wasi.lookupflags_t = .{};
    const oflags: wasi.oflags_t = .{};
    const inheriting: wasi.rights_t = .{};
    const fdflags: wasi.fdflags_t = .{};
    var fd: posix.fd_t = undefined;
    const syscall: Syscall = try .start();
    while (true) {
        switch (wasi.path_open(dir.handle, lookup_flags, sub_path.ptr, sub_path.len, oflags, base, inheriting, fdflags, &fd)) {
            .SUCCESS => {
                syscall.finish();
                break;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .FAULT => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .ACCES => return error.AccessDenied,
                    .FBIG => return error.FileTooBig,
                    .OVERFLOW => return error.FileTooBig,
                    .ISDIR => return error.IsDir,
                    .LOOP => return error.SymLinkLoop,
                    .MFILE => return error.ProcessFdQuotaExceeded,
                    .NFILE => return error.SystemFdQuotaExceeded,
                    .NODEV => return error.NoDevice,
                    .NOENT => return error.FileNotFound,
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => return error.NotDir,
                    .PERM => return error.PermissionDenied,
                    .BUSY => return error.DeviceBusy,
                    .NOTCAPABLE => return error.AccessDenied,
                    .NAMETOOLONG => return error.NameTooLong,
                    .INVAL => return error.BadPathName,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
    errdefer closeFd(fd);

    if (!flags.allow_directory) {
        const is_dir = is_dir: {
            const stat = fileStat(t, .{ .handle = fd, .flags = .{ .nonblocking = false } }) catch |err| switch (err) {
                // The directory-ness is either unknown or unknowable
                error.Streaming => break :is_dir false,
                else => |e| return e,
            };
            break :is_dir stat.kind == .directory;
        };
        if (is_dir) return error.IsDir;
    }

    return .{
        .handle = fd,
        .flags = .{ .nonblocking = false },
    };
}

const dirOpenDir = switch (native_os) {
    .wasi => dirOpenDirWasi,
    .haiku => dirOpenDirHaiku,
    else => dirOpenDirPosix,
};

/// This function is also used for WASI when libc is linked.
fn dirOpenDirPosix(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.OpenOptions,
) Dir.OpenError!Dir {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        const sub_path_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
        return dirOpenDirWindows(dir, sub_path_w.span(), options);
    }

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var flags: posix.O = switch (native_os) {
        .wasi => .{
            .read = true,
            .NOFOLLOW = !options.follow_symlinks,
            .DIRECTORY = true,
        },
        else => .{
            .ACCMODE = .RDONLY,
            .NOFOLLOW = !options.follow_symlinks,
            .DIRECTORY = true,
            .CLOEXEC = true,
        },
    };

    if (@hasField(posix.O, "PATH") and !options.iterate)
        flags.PATH = true;

    const mode: posix.mode_t = 0;

    const syscall: Syscall = try .start();
    while (true) {
        const rc = openat_sym(dir.handle, sub_path_posix, flags, mode);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                return .{ .handle = @intCast(rc) };
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .INVAL => return syscall.fail(error.BadPathName),
            .ACCES => return syscall.fail(error.AccessDenied),
            .LOOP => return syscall.fail(error.SymLinkLoop),
            .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
            .NAMETOOLONG => return syscall.fail(error.NameTooLong),
            .NFILE => return syscall.fail(error.SystemFdQuotaExceeded),
            .NODEV => return syscall.fail(error.NoDevice),
            .NOENT => return syscall.fail(error.FileNotFound),
            .NOMEM => return syscall.fail(error.SystemResources),
            .NOTDIR => return syscall.fail(error.NotDir),
            .PERM => return syscall.fail(error.PermissionDenied),
            .NXIO => return syscall.fail(error.NoDevice),
            .ILSEQ => return syscall.fail(error.BadPathName),
            .FAULT => |err| return syscall.errnoBug(err),
            .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
            .BUSY => |err| return syscall.errnoBug(err), // O_EXCL not passed
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn dirOpenDirHaiku(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.OpenOptions,
) Dir.OpenError!Dir {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    _ = options;

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system._kern_open_dir(dir.handle, sub_path_posix);
        if (rc >= 0) {
            syscall.finish();
            return .{ .handle = rc };
        }
        switch (@as(posix.E, @enumFromInt(rc))) {
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .ACCES => return error.AccessDenied,
                    .LOOP => return error.SymLinkLoop,
                    .MFILE => return error.ProcessFdQuotaExceeded,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NFILE => return error.SystemFdQuotaExceeded,
                    .NODEV => return error.NoDevice,
                    .NOENT => return error.FileNotFound,
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => return error.NotDir,
                    .PERM => return error.PermissionDenied,
                    .BUSY => return error.DeviceBusy,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

pub fn dirOpenDirWindows(
    dir: Dir,
    sub_path_w: []const u16,
    options: Dir.OpenOptions,
) Dir.OpenError!Dir {
    const w = windows;

    var io_status_block: w.IO_STATUS_BLOCK = undefined;
    var result: Dir = .{ .handle = undefined };

    const attr: w.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w)) null else dir.handle,
        .ObjectName = @constCast(&w.UNICODE_STRING.init(sub_path_w)),
    };

    const syscall: Syscall = try .start();
    while (true) switch (w.ntdll.NtCreateFile(
        &result.handle,
        // TODO remove some of these flags if options.access_sub_paths is false
        .{
            .SPECIFIC = .{ .FILE_DIRECTORY = .{
                .LIST = options.iterate,
                .READ_EA = true,
                .TRAVERSE = true,
                .READ_ATTRIBUTES = true,
            } },
            .STANDARD = .{
                .RIGHTS = .READ,
                .SYNCHRONIZE = true,
            },
        },
        &attr,
        &io_status_block,
        null,
        .{ .NORMAL = true },
        .VALID_FLAGS,
        .OPEN,
        .{
            .DIRECTORY_FILE = true,
            .IO = .SYNCHRONOUS_NONALERT,
            .OPEN_FOR_BACKUP_INTENT = true,
            .OPEN_REPARSE_POINT = !options.follow_symlinks,
        },
        null,
        0,
    )) {
        .SUCCESS => {
            syscall.finish();
            return result;
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
        .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .OBJECT_NAME_COLLISION => |err| return w.statusBug(err),
        .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
        // This can happen if the directory has 'List folder contents' permission set to 'Deny'
        // and the directory is trying to be opened for iteration.
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
        else => |rc| return syscall.unexpectedNtstatus(rc),
    };
}

fn dirClose(userdata: ?*anyopaque, dirs: []const Dir) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    for (dirs) |dir| {
        if (is_windows) {
            windows.CloseHandle(dir.handle);
        } else {
            closeFd(dir.handle);
        }
    }
}

const dirRead = switch (native_os) {
    .linux => dirReadLinux,
    .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => dirReadDarwin,
    .freebsd, .netbsd, .dragonfly, .openbsd => dirReadBsd,
    .illumos => dirReadIllumos,
    .haiku => dirReadHaiku,
    .windows => dirReadWindows,
    .wasi => dirReadWasi,
    else => dirReadUnimplemented,
};

fn dirReadLinux(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const linux = std.os.linux;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        if (dr.end - dr.index == 0) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;
            if (dr.state == .reset) {
                posixSeekTo(dr.dir.handle, 0) catch |err| switch (err) {
                    error.Unseekable => return error.Unexpected,
                    else => |e| return e,
                };
                dr.state = .reading;
            }
            const syscall: Syscall = try .start();
            const n = while (true) {
                const rc = linux.getdents64(dr.dir.handle, dr.buffer.ptr, dr.buffer.len);
                switch (linux.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        break rc;
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .BADF => |err| return errnoBug(err), // Dir is invalid or was opened without iteration ability.
                            .FAULT => |err| return errnoBug(err),
                            .NOTDIR => |err| return errnoBug(err),
                            // To be consistent across platforms, iteration
                            // ends if the directory being iterated is deleted
                            // during iteration. This matches the behavior of
                            // non-Linux, non-WASI UNIX platforms.
                            .NOENT => {
                                dr.state = .finished;
                                return 0;
                            },
                            // This can occur when reading /proc/$PID/net, or
                            // if the provided buffer is too small. Neither
                            // scenario is intended to be handled by this API.
                            .INVAL => return error.Unexpected,
                            .ACCES => return error.AccessDenied, // Lacking permission to iterate this directory.
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            };
            if (n == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = 0;
            dr.end = n;
        }
        // Linux aligns the header by padding after the null byte of the name
        // to align the next entry. This means we can find the end of the name
        // by looking at only the 8 bytes before the next record. However since
        // file names are usually short it's better to keep the machine code
        // simpler.
        //
        // Furthermore, I observed qemu user mode to not align this struct, so
        // this code makes the conservative choice to not assume alignment.
        const linux_entry: *align(1) linux.dirent64 = @ptrCast(&dr.buffer[dr.index]);
        const next_index = dr.index + linux_entry.reclen;
        dr.index = next_index;
        const name_ptr: [*]u8 = &linux_entry.name;
        const padded_name = name_ptr[0 .. linux_entry.reclen - @offsetOf(linux.dirent64, "name")];
        const name_len = std.mem.findScalar(u8, padded_name, 0).?;
        const name = name_ptr[0..name_len :0];

        if (std.mem.eql(u8, name, ".") or std.mem.eql(u8, name, "..")) continue;

        const entry_kind: File.Kind = switch (linux_entry.type) {
            linux.DT.BLK => .block_device,
            linux.DT.CHR => .character_device,
            linux.DT.DIR => .directory,
            linux.DT.FIFO => .named_pipe,
            linux.DT.LNK => .sym_link,
            linux.DT.REG => .file,
            linux.DT.SOCK => .unix_domain_socket,
            else => .unknown,
        };
        buffer[buffer_index] = .{
            .name = name,
            .kind = entry_kind,
            .inode = linux_entry.ino,
        };
        buffer_index += 1;
    }
    return buffer_index;
}

fn dirReadDarwin(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const Header = extern struct {
        seek: i64,
    };
    const header: *Header = @ptrCast(dr.buffer.ptr);
    const header_end: usize = @sizeOf(Header);
    if (dr.index < header_end) {
        // Initialize header.
        dr.index = header_end;
        dr.end = header_end;
        header.* = .{ .seek = 0 };
    }
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        if (dr.end - dr.index == 0) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;
            if (dr.state == .reset) {
                posixSeekTo(dr.dir.handle, 0) catch |err| switch (err) {
                    error.Unseekable => return error.Unexpected,
                    else => |e| return e,
                };
                dr.state = .reading;
            }
            const dents_buffer = dr.buffer[header_end..];
            const syscall: Syscall = try .start();
            const n: usize = while (true) {
                const rc = posix.system.getdirentries(dr.dir.handle, dents_buffer.ptr, dents_buffer.len, &header.seek);
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        break @intCast(rc);
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .BADF => |err| return errnoBug(err), // Dir is invalid or was opened without iteration ability.
                            .FAULT => |err| return errnoBug(err),
                            .NOTDIR => |err| return errnoBug(err),
                            .INVAL => |err| return errnoBug(err),
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            };
            if (n == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = header_end;
            dr.end = header_end + n;
        }
        const darwin_entry = @as(*align(1) posix.system.dirent, @ptrCast(&dr.buffer[dr.index]));
        const next_index = dr.index + darwin_entry.reclen;
        dr.index = next_index;

        const name = @as([*]u8, @ptrCast(&darwin_entry.name))[0..darwin_entry.namlen];
        if (std.mem.eql(u8, name, ".") or std.mem.eql(u8, name, "..") or (darwin_entry.ino == 0))
            continue;

        const entry_kind: File.Kind = switch (darwin_entry.type) {
            posix.DT.BLK => .block_device,
            posix.DT.CHR => .character_device,
            posix.DT.DIR => .directory,
            posix.DT.FIFO => .named_pipe,
            posix.DT.LNK => .sym_link,
            posix.DT.REG => .file,
            posix.DT.SOCK => .unix_domain_socket,
            posix.DT.WHT => .whiteout,
            else => .unknown,
        };
        buffer[buffer_index] = .{
            .name = name,
            .kind = entry_kind,
            .inode = darwin_entry.ino,
        };
        buffer_index += 1;
    }
    return buffer_index;
}

fn dirReadBsd(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        if (dr.end - dr.index == 0) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;
            if (dr.state == .reset) {
                posixSeekTo(dr.dir.handle, 0) catch |err| switch (err) {
                    error.Unseekable => return error.Unexpected,
                    else => |e| return e,
                };
                dr.state = .reading;
            }
            const syscall: Syscall = try .start();
            const n: usize = while (true) {
                const rc = posix.system.getdents(dr.dir.handle, dr.buffer.ptr, dr.buffer.len);
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        break @intCast(rc);
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .BADF => |err| return errnoBug(err), // Dir is invalid or was opened without iteration ability
                            .FAULT => |err| return errnoBug(err),
                            .NOTDIR => |err| return errnoBug(err),
                            .INVAL => |err| return errnoBug(err),
                            // Introduced in freebsd 13.2: directory unlinked
                            // but still open. To be consistent, iteration ends
                            // if the directory being iterated is deleted
                            // during iteration.
                            .NOENT => {
                                dr.state = .finished;
                                return 0;
                            },
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            };
            if (n == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = 0;
            dr.end = n;
        }
        const bsd_entry = @as(*align(1) posix.system.dirent, @ptrCast(&dr.buffer[dr.index]));
        const next_index = dr.index +
            if (@hasField(posix.system.dirent, "reclen")) bsd_entry.reclen else bsd_entry.reclen();
        dr.index = next_index;

        const name = @as([*]u8, @ptrCast(&bsd_entry.name))[0..bsd_entry.namlen];

        const skip_zero_fileno = switch (native_os) {
            // fileno=0 is used to mark invalid entries or deleted files.
            .openbsd, .netbsd => true,
            else => false,
        };
        if (std.mem.eql(u8, name, ".") or std.mem.eql(u8, name, "..") or
            (skip_zero_fileno and bsd_entry.fileno == 0))
        {
            continue;
        }

        const entry_kind: File.Kind = switch (bsd_entry.type) {
            posix.DT.BLK => .block_device,
            posix.DT.CHR => .character_device,
            posix.DT.DIR => .directory,
            posix.DT.FIFO => .named_pipe,
            posix.DT.LNK => .sym_link,
            posix.DT.REG => .file,
            posix.DT.SOCK => .unix_domain_socket,
            posix.DT.WHT => .whiteout,
            else => .unknown,
        };
        buffer[buffer_index] = .{
            .name = name,
            .kind = entry_kind,
            .inode = bsd_entry.fileno,
        };
        buffer_index += 1;
    }
    return buffer_index;
}

fn dirReadIllumos(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        if (dr.end - dr.index == 0) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;
            if (dr.state == .reset) {
                posixSeekTo(dr.dir.handle, 0) catch |err| switch (err) {
                    error.Unseekable => return error.Unexpected,
                    else => |e| return e,
                };
                dr.state = .reading;
            }
            const syscall: Syscall = try .start();
            const n: usize = while (true) {
                const rc = posix.system.getdents(dr.dir.handle, dr.buffer.ptr, dr.buffer.len);
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        break rc;
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .BADF => |err| return errnoBug(err), // Dir is invalid or was opened without iteration ability
                            .FAULT => |err| return errnoBug(err),
                            .NOTDIR => |err| return errnoBug(err),
                            .INVAL => |err| return errnoBug(err),
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            };
            if (n == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = 0;
            dr.end = n;
        }
        const entry = @as(*align(1) posix.system.dirent, @ptrCast(&dr.buffer[dr.index]));
        const next_index = dr.index + entry.reclen;
        dr.index = next_index;

        const name = std.mem.sliceTo(@as([*:0]u8, @ptrCast(&entry.name)), 0);
        if (std.mem.eql(u8, name, ".") or std.mem.eql(u8, name, "..")) continue;

        // illumos dirent doesn't expose type, so we have to call stat to get it.
        const stat = try posixStatFile(dr.dir.handle, name, posix.AT.SYMLINK_NOFOLLOW);

        buffer[buffer_index] = .{
            .name = name,
            .kind = stat.kind,
            .inode = entry.ino,
        };
        buffer_index += 1;
    }
    return buffer_index;
}

fn dirReadHaiku(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    _ = userdata;
    _ = dr;
    _ = buffer;
    @panic("TODO implement dirReadHaiku");
}

fn dirReadWindows(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const w = windows;

    // We want to be able to use the `dr.buffer` for both the NtQueryDirectoryFile call (which
    // returns WTF-16 names) *and* as a buffer for storing those WTF-16 names as WTF-8 to be able
    // to return them in `Dir.Entry.name`. However, the problem that needs to be overcome in order to do
    // that is that each WTF-16 code unit can be encoded as a maximum of 3 WTF-8 bytes, which means
    // that it's not guaranteed that the memory used for the WTF-16 name will be sufficient
    // for the WTF-8 encoding of the same name (for example, € is encoded as one WTF-16 code unit,
    // [2 bytes] but encoded in WTF-8 as 3 bytes).
    //
    // The approach taken here is to "reserve" enough space in the `dr.buffer` to ensure that
    // at least one entry with the maximum possible WTF-8 name length can be stored without clobbering
    // any entries that follow it. That is, we determine how much space is needed to allow that,
    // and then only provide the remaining portion of `dr.buffer` to the NtQueryDirectoryFile
    // call. The WTF-16 names can then be safely converted using the full `dr.buffer` slice, making
    // sure that each name can only potentially overwrite the data of its own entry.
    //
    // The worst case, where an entry's name is both the maximum length of a component and
    // made up entirely of code points that are encoded as one WTF-16 code unit/three WTF-8 bytes,
    // would therefore look like the diagram below, and only one entry would be able to be returned:
    //
    //     |   reserved  | remaining unreserved buffer |
    //                   | entry 1 | entry 2 |   ...   |
    //     | wtf-8 name of entry 1 |
    //
    // However, in the average case we will be able to store more than one WTF-8 name at a time in the
    // available buffer and therefore we will be able to populate more than one `Dir.Entry` at a time.
    // That might look something like this (where name 1, name 2, etc are the converted WTF-8 names):
    //
    //     |   reserved  | remaining unreserved buffer |
    //                   | entry 1 | entry 2 |   ...   |
    //     | name 1 | name 2 | name 3 | name 4 |  ...  |
    //
    // Note: More than the minimum amount of space could be reserved to make the "worst case"
    // less likely, but since the worst-case also requires a maximum length component to matter,
    // it's unlikely for it to become a problem in normal scenarios even if all names on the filesystem
    // are made up of non-ASCII characters that have the "one WTF-16 code unit <-> three WTF-8 bytes"
    // property (e.g. code points >= U+0800 and <= U+FFFF), as it's unlikely for a significant
    // number of components to be maximum length.

    // We need `3 * NAME_MAX` bytes to store a max-length component as WTF-8 safely.
    // Because needing to store a max-length component depends on a `FileName` *with* the maximum
    // component length, we know that the corresponding populated `FILE_BOTH_DIR_INFORMATION` will
    // be of size `@sizeOf(w.FILE_BOTH_DIR_INFORMATION) + 2 * NAME_MAX` bytes, so we only need to
    // reserve enough to get us to up to having `3 * NAME_MAX` bytes available when taking into account
    // that we have the ability to write over top of the reserved memory + the full footprint of that
    // particular `FILE_BOTH_DIR_INFORMATION`.
    const max_info_len = @sizeOf(w.FILE_BOTH_DIR_INFORMATION) + w.NAME_MAX * 2;
    const info_align = @alignOf(w.FILE_BOTH_DIR_INFORMATION);
    const reserve_needed = std.mem.alignForward(usize, Dir.max_name_bytes, info_align) - max_info_len;
    const unreserved_start = std.mem.alignForward(usize, reserve_needed, info_align);
    const unreserved_buffer = dr.buffer[unreserved_start..];
    // This is enforced by `Dir.Reader`
    assert(unreserved_buffer.len >= max_info_len);

    var name_index: usize = 0;
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        if (dr.end - dr.index == 0) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;

            var io_status_block: w.IO_STATUS_BLOCK = undefined;
            const syscall: Syscall = try .start();
            const rc = while (true) switch (w.ntdll.NtQueryDirectoryFile(
                dr.dir.handle,
                null,
                null,
                null,
                &io_status_block,
                unreserved_buffer.ptr,
                std.math.lossyCast(w.ULONG, unreserved_buffer.len),
                .BothDirectory,
                .FALSE,
                null,
                .fromBool(dr.state == .reset),
            )) {
                .CANCELLED => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |rc| {
                    syscall.finish();
                    break rc;
                },
            };
            dr.state = .reading;
            if (io_status_block.Information == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = 0;
            dr.end = io_status_block.Information;
            switch (rc) {
                .SUCCESS => {},
                .ACCESS_DENIED => return error.AccessDenied, // Double-check that the Dir was opened with iteration ability
                else => return w.unexpectedStatus(rc),
            }
        }

        // While the official API docs guarantee FILE_BOTH_DIR_INFORMATION to be aligned properly
        // this may not always be the case (e.g. due to faulty VM/sandboxing tools)
        const dir_info: *align(2) w.FILE_BOTH_DIR_INFORMATION = @ptrCast(@alignCast(&unreserved_buffer[dr.index]));
        const backtrack_index = dr.index;
        if (dir_info.NextEntryOffset != 0) {
            dr.index += dir_info.NextEntryOffset;
        } else {
            dr.index = dr.end;
        }

        const name_wtf16le = @as([*]u16, @ptrCast(&dir_info.FileName))[0 .. dir_info.FileNameLength / 2];

        if (std.mem.eql(u16, name_wtf16le, &[_]u16{'.'}) or std.mem.eql(u16, name_wtf16le, &[_]u16{ '.', '.' })) {
            continue;
        }

        // Read any relevant information from the `dir_info` now since it's possible the WTF-8
        // name will overwrite it.
        const kind: File.Kind = blk: {
            const attrs = dir_info.FileAttributes;
            if (attrs.REPARSE_POINT) break :blk .sym_link;
            if (attrs.DIRECTORY) break :blk .directory;
            break :blk .file;
        };
        const inode: File.INode = dir_info.FileIndex;

        // If there's no more space for WTF-8 names without bleeding over into
        // the remaining unprocessed entries, then backtrack and return what we have so far.
        if (name_index + std.unicode.calcWtf8Len(name_wtf16le) > unreserved_start + dr.index) {
            // We should always be able to fit at least one entry into the buffer no matter what
            assert(buffer_index != 0);
            dr.index = backtrack_index;
            break;
        }

        const name_buf = dr.buffer[name_index..];
        const name_wtf8_len = std.unicode.wtf16LeToWtf8(name_buf, name_wtf16le);
        const name_wtf8 = name_buf[0..name_wtf8_len];
        name_index += name_wtf8_len;

        buffer[buffer_index] = .{
            .name = name_wtf8,
            .kind = kind,
            .inode = inode,
        };
        buffer_index += 1;
    }

    return buffer_index;
}

fn dirReadWasi(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    // We intentinally use fd_readdir even when linked with libc, since its
    // implementation is exactly the same as below, and we avoid the code
    // complexity here.
    const wasi = std.os.wasi;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const Header = extern struct {
        cookie: u64,
    };
    const header: *align(@alignOf(usize)) Header = @ptrCast(dr.buffer.ptr);
    const header_end: usize = @sizeOf(Header);
    if (dr.index < header_end) {
        // Initialize header.
        dr.index = header_end;
        dr.end = header_end;
        header.* = .{ .cookie = wasi.DIRCOOKIE_START };
    }
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        // According to the WASI spec, the last entry might be truncated, so we
        // need to check if the remaining buffer contains the whole dirent.
        if (dr.end - dr.index < @sizeOf(wasi.dirent_t)) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;
            if (dr.state == .reset) {
                header.* = .{ .cookie = wasi.DIRCOOKIE_START };
                dr.state = .reading;
            }
            const dents_buffer = dr.buffer[header_end..];
            var n: usize = undefined;
            const syscall: Syscall = try .start();
            while (true) {
                switch (wasi.fd_readdir(dr.dir.handle, dents_buffer.ptr, dents_buffer.len, header.cookie, &n)) {
                    .SUCCESS => {
                        syscall.finish();
                        break;
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .BADF => |err| return errnoBug(err), // Dir is invalid or was opened without iteration ability.
                            .FAULT => |err| return errnoBug(err),
                            .NOTDIR => |err| return errnoBug(err),
                            .INVAL => |err| return errnoBug(err),
                            // To be consistent across platforms, iteration
                            // ends if the directory being iterated is deleted
                            // during iteration. This matches the behavior of
                            // non-Linux, non-WASI UNIX platforms.
                            .NOENT => {
                                dr.state = .finished;
                                return 0;
                            },
                            .NOTCAPABLE => return error.AccessDenied,
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            }
            if (n == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = header_end;
            dr.end = header_end + n;
        }
        const entry: *align(1) wasi.dirent_t = @ptrCast(&dr.buffer[dr.index]);
        const entry_size = @sizeOf(wasi.dirent_t);
        const name_index = dr.index + entry_size;
        if (name_index + entry.namlen > dr.end) {
            // This case, the name is truncated, so we need to call readdir to store the entire name.
            dr.end = dr.index; // Force fd_readdir in the next loop.
            continue;
        }
        const name = dr.buffer[name_index..][0..entry.namlen];
        const next_index = name_index + entry.namlen;
        dr.index = next_index;
        header.cookie = entry.next;

        if (std.mem.eql(u8, name, ".") or std.mem.eql(u8, name, ".."))
            continue;

        const entry_kind: File.Kind = switch (entry.type) {
            .BLOCK_DEVICE => .block_device,
            .CHARACTER_DEVICE => .character_device,
            .DIRECTORY => .directory,
            .SYMBOLIC_LINK => .sym_link,
            .REGULAR_FILE => .file,
            .SOCKET_STREAM, .SOCKET_DGRAM => .unix_domain_socket,
            else => .unknown,
        };
        buffer[buffer_index] = .{
            .name = name,
            .kind = entry_kind,
            .inode = entry.ino,
        };
        buffer_index += 1;
    }
    return buffer_index;
}

fn dirReadUnimplemented(userdata: ?*anyopaque, dir_reader: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    _ = userdata;
    _ = dir_reader;
    _ = buffer;
    return error.Unexpected;
}

const dirRealPathFile = switch (native_os) {
    .windows => dirRealPathFileWindows,
    else => dirRealPathFilePosix,
};

fn dirRealPathFileWindows(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, out_buffer: []u8) Dir.RealPathFileError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_name_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});

    const h_file = handle: {
        if (OpenFile(path_name_w.span(), .{
            .dir = dir.handle,
            .access_mask = .{
                .GENERIC = .{ .READ = true },
                .STANDARD = .{ .SYNCHRONIZE = true },
            },
            .creation = .OPEN,
            .filter = .any,
        })) |handle| {
            break :handle handle;
        } else |err| switch (err) {
            error.WouldBlock => unreachable,
            else => |e| return e,
        }
    };
    defer windows.CloseHandle(h_file);

    // We can re-use the path buffer for the WTF-16 representation since
    // we don't need the prefixed path anymore
    return realPathWindowsBuf(h_file, out_buffer, &path_name_w.data);
}

fn realPathWindows(h_file: windows.HANDLE, out_buffer: []u8) File.RealPathError!usize {
    var wide_buf: [windows.PATH_MAX_WIDE]u16 = undefined;
    return realPathWindowsBuf(h_file, out_buffer, &wide_buf);
}

fn realPathWindowsBuf(h_file: windows.HANDLE, out_buffer: []u8, wtf16_buffer: []u16) File.RealPathError!usize {
    const wide_slice = try GetFinalPathNameByHandle(h_file, .{}, wtf16_buffer);

    const len = std.unicode.calcWtf8Len(wide_slice);
    if (len > out_buffer.len)
        return error.NameTooLong;

    return std.unicode.wtf16LeToWtf8(out_buffer, wide_slice);
}

/// Specifies how to format volume path in the result of `GetFinalPathNameByHandle`.
/// Defaults to DOS volume names.
pub const GetFinalPathNameByHandleFormat = struct {
    volume_name: enum {
        /// Format as DOS volume name
        Dos,
        /// Format as NT volume name
        Nt,
    } = .Dos,
};

pub const GetFinalPathNameByHandleError = error{
    AccessDenied,
    FileNotFound,
    NameTooLong,
    /// The volume does not contain a recognized file system. File system
    /// drivers might not be loaded, or the volume may be corrupt.
    UnrecognizedVolume,
} || Io.Cancelable || Io.UnexpectedError;

/// Returns canonical (normalized) path of handle.
/// Use `GetFinalPathNameByHandleFormat` to specify whether the path is meant to include
/// NT or DOS volume name (e.g., `\Device\HarddiskVolume0\foo.txt` versus `C:\foo.txt`).
/// If DOS volume name format is selected, note that this function does *not* prepend
/// `\\?\` prefix to the resultant path.
pub fn GetFinalPathNameByHandle(
    hFile: windows.HANDLE,
    fmt: GetFinalPathNameByHandleFormat,
    out_buffer: []u16,
) GetFinalPathNameByHandleError![]u16 {
    const final_path = QueryObjectName(hFile, out_buffer) catch |err| switch (err) {
        // we assume InvalidHandle is close enough to FileNotFound in semantics
        // to not further complicate the error set
        error.InvalidHandle => return error.FileNotFound,
        else => |e| return e,
    };

    switch (fmt.volume_name) {
        .Nt => {
            // the returned path is already in .Nt format
            return final_path;
        },
        .Dos => {
            // parse the string to separate volume path from file path
            const device_prefix = std.unicode.utf8ToUtf16LeStringLiteral("\\Device\\");

            // We aren't entirely sure of the structure of the path returned by
            // QueryObjectName in all contexts/environments.
            // This code is written to cover the various cases that have
            // been encountered and solved appropriately. But note that there's
            // no easy way to verify that they have all been tackled!
            // (Unless you, the reader knows of one then please do action that!)
            if (!std.mem.startsWith(u16, final_path, device_prefix)) {
                // Wine seems to return NT namespaced paths starting with \??\ from QueryObjectName
                // (e.g. `\??\Z:\some\path\to\a\file.txt`), in which case we can just strip the
                // prefix to turn it into an absolute path.
                // https://github.com/ziglang/zig/issues/26029
                // https://bugs.winehq.org/show_bug.cgi?id=39569
                return windows.ntToWin32Namespace(final_path, out_buffer) catch |err| switch (err) {
                    error.NotNtPath => return error.Unexpected,
                    error.NameTooLong => |e| return e,
                };
            }

            const file_path_begin_index = std.mem.findPos(u16, final_path, device_prefix.len, &[_]u16{'\\'}) orelse unreachable;
            const volume_name_u16 = final_path[0..file_path_begin_index];
            const device_name_u16 = volume_name_u16[device_prefix.len..];
            const file_name_u16 = final_path[file_path_begin_index..];

            // MUP is Multiple UNC Provider, and indicates that the path is a UNC
            // path. In this case, the canonical UNC path can be gotten by just
            // dropping the \Device\Mup\ and making sure the path begins with \\
            if (std.mem.eql(u16, device_name_u16, std.unicode.utf8ToUtf16LeStringLiteral("Mup"))) {
                out_buffer[0] = '\\';
                @memmove(out_buffer[1..][0..file_name_u16.len], file_name_u16);
                return out_buffer[0 .. 1 + file_name_u16.len];
            }

            // Get DOS volume name. DOS volume names are actually symbolic link objects to the
            // actual NT volume. For example:
            // (NT) \Device\HarddiskVolume4 => (DOS) \DosDevices\C: == (DOS) C:
            const MIN_SIZE = @sizeOf(windows.MOUNTMGR_MOUNT_POINT) + windows.MAX_PATH;
            // We initialize the input buffer to all zeros for convenience since
            // `DeviceIoControl` with `IOCTL_MOUNTMGR_QUERY_POINTS` expects this.
            var input_buf: [MIN_SIZE]u8 align(@alignOf(windows.MOUNTMGR_MOUNT_POINT)) = [_]u8{0} ** MIN_SIZE;
            var output_buf: [MIN_SIZE * 4]u8 align(@alignOf(windows.MOUNTMGR_MOUNT_POINTS)) = undefined;

            // This surprising path is a filesystem path to the mount manager on Windows.
            // Source: https://stackoverflow.com/questions/3012828/using-ioctl-mountmgr-query-points
            // This is the NT namespaced version of \\.\MountPointManager
            const mgmt_path_u16 = std.unicode.utf8ToUtf16LeStringLiteral("\\??\\MountPointManager");
            const mgmt_handle = OpenFile(mgmt_path_u16, .{
                .access_mask = .{ .STANDARD = .{ .SYNCHRONIZE = true } },
                .creation = .OPEN,
            }) catch |err| switch (err) {
                error.IsDir => return error.Unexpected,
                error.NotDir => return error.Unexpected,
                error.NoDevice => return error.Unexpected,
                error.AccessDenied => return error.Unexpected,
                error.PipeBusy => return error.Unexpected,
                error.FileBusy => return error.Unexpected,
                error.PathAlreadyExists => return error.Unexpected,
                error.WouldBlock => return error.Unexpected,
                error.NetworkNotFound => return error.Unexpected,
                error.AntivirusInterference => return error.Unexpected,
                error.BadPathName => return error.Unexpected,
                else => |e| return e,
            };
            defer windows.CloseHandle(mgmt_handle);

            var input_struct: *windows.MOUNTMGR_MOUNT_POINT = @ptrCast(&input_buf[0]);
            input_struct.DeviceNameOffset = @sizeOf(windows.MOUNTMGR_MOUNT_POINT);
            input_struct.DeviceNameLength = @intCast(volume_name_u16.len * 2);
            @memcpy(input_buf[@sizeOf(windows.MOUNTMGR_MOUNT_POINT)..][0 .. volume_name_u16.len * 2], @as([*]const u8, @ptrCast(volume_name_u16.ptr)));

            switch ((try deviceIoControl(&.{
                .file = .{ .handle = mgmt_handle, .flags = .{ .nonblocking = false } },
                .code = windows.IOCTL.MOUNTMGR.QUERY_POINTS,
                .in = &input_buf,
                .out = &output_buf,
            })).u.Status) {
                .SUCCESS => {},
                .CANCELLED => unreachable,
                .OBJECT_NAME_NOT_FOUND => return error.FileNotFound,
                else => |status| return windows.unexpectedStatus(status),
            }
            const mount_points_struct: *const windows.MOUNTMGR_MOUNT_POINTS = @ptrCast(&output_buf[0]);

            const mount_points = @as(
                [*]const windows.MOUNTMGR_MOUNT_POINT,
                @ptrCast(&mount_points_struct.MountPoints[0]),
            )[0..mount_points_struct.NumberOfMountPoints];

            for (mount_points) |mount_point| {
                const symlink = @as(
                    [*]const u16,
                    @ptrCast(@alignCast(&output_buf[mount_point.SymbolicLinkNameOffset])),
                )[0 .. mount_point.SymbolicLinkNameLength / 2];

                // Look for `\DosDevices\` prefix. We don't really care if there are more than one symlinks
                // with traditional DOS drive letters, so pick the first one available.
                var prefix_buf = std.unicode.utf8ToUtf16LeStringLiteral("\\DosDevices\\");
                const prefix = prefix_buf[0..prefix_buf.len];

                if (std.mem.startsWith(u16, symlink, prefix)) {
                    const drive_letter = symlink[prefix.len..];

                    if (out_buffer.len < drive_letter.len + file_name_u16.len) return error.NameTooLong;

                    @memcpy(out_buffer[0..drive_letter.len], drive_letter);
                    @memmove(out_buffer[drive_letter.len..][0..file_name_u16.len], file_name_u16);
                    const total_len = drive_letter.len + file_name_u16.len;

                    // Validate that DOS does not contain any spurious nul bytes.
                    assert(std.mem.findScalar(u16, out_buffer[0..total_len], 0) == null);

                    return out_buffer[0..total_len];
                } else if (mountmgrIsVolumeName(symlink)) {
                    // If the symlink is a volume GUID like \??\Volume{383da0b0-717f-41b6-8c36-00500992b58d},
                    // then it is a volume mounted as a path rather than a drive letter. We need to
                    // query the mount manager again to get the DOS path for the volume.

                    // 49 is the maximum length accepted by mountmgrIsVolumeName
                    const vol_input_size = @sizeOf(windows.MOUNTMGR_TARGET_NAME) + (49 * 2);
                    var vol_input_buf: [vol_input_size]u8 align(@alignOf(windows.MOUNTMGR_TARGET_NAME)) = [_]u8{0} ** vol_input_size;
                    // Note: If the path exceeds MAX_PATH, the Disk Management GUI doesn't accept the full path,
                    // and instead if must be specified using a shortened form (e.g. C:\FOO~1\BAR~1\<...>).
                    // However, just to be sure we can handle any path length, we use PATH_MAX_WIDE here.
                    const min_output_size = @sizeOf(windows.MOUNTMGR_VOLUME_PATHS) + (windows.PATH_MAX_WIDE * 2);
                    var vol_output_buf: [min_output_size]u8 align(@alignOf(windows.MOUNTMGR_VOLUME_PATHS)) = undefined;

                    var vol_input_struct: *windows.MOUNTMGR_TARGET_NAME = @ptrCast(&vol_input_buf[0]);
                    vol_input_struct.DeviceNameLength = @intCast(symlink.len * 2);
                    @memcpy(@as([*]windows.WCHAR, &vol_input_struct.DeviceName)[0..symlink.len], symlink);

                    switch ((try deviceIoControl(&.{
                        .file = .{ .handle = mgmt_handle, .flags = .{ .nonblocking = true } },
                        .code = windows.IOCTL.MOUNTMGR.QUERY_DOS_VOLUME_PATH,
                        .in = &vol_input_buf,
                        .out = &vol_output_buf,
                    })).u.Status) {
                        .SUCCESS => {},
                        .CANCELLED => unreachable,
                        .UNRECOGNIZED_VOLUME => return error.UnrecognizedVolume,
                        else => |status| return windows.unexpectedStatus(status),
                    }
                    const volume_paths_struct: *const windows.MOUNTMGR_VOLUME_PATHS = @ptrCast(&vol_output_buf[0]);
                    const volume_path = std.mem.sliceTo(@as(
                        [*]const u16,
                        &volume_paths_struct.MultiSz,
                    )[0 .. volume_paths_struct.MultiSzLength / 2], 0);

                    if (out_buffer.len < volume_path.len + file_name_u16.len) return error.NameTooLong;

                    // `out_buffer` currently contains the memory of `file_name_u16`, so it can overlap with where
                    // we want to place the filename before returning. Here are the possible overlapping cases:
                    //
                    // out_buffer:       [filename]
                    //       dest: [___(a)___] [___(b)___]
                    //
                    // In the case of (a), we need to copy forwards, and in the case of (b) we need
                    // to copy backwards. We also need to do this before copying the volume path because
                    // it could overwrite the file_name_u16 memory.
                    const file_name_dest = out_buffer[volume_path.len..][0..file_name_u16.len];
                    @memmove(file_name_dest, file_name_u16);
                    @memcpy(out_buffer[0..volume_path.len], volume_path);
                    const total_len = volume_path.len + file_name_u16.len;

                    // Validate that DOS does not contain any spurious nul bytes.
                    assert(std.mem.findScalar(u16, out_buffer[0..total_len], 0) == null);

                    return out_buffer[0..total_len];
                }
            }

            // If we've ended up here, then something went wrong/is corrupted in the OS,
            // so error out!
            return error.FileNotFound;
        },
    }
}

test GetFinalPathNameByHandle {
    if (builtin.os.tag != .windows)
        return;

    //any file will do
    var tmp = std.testing.tmpDir(.{});
    defer tmp.cleanup();
    const handle = tmp.dir.handle;
    var buffer: [windows.PATH_MAX_WIDE]u16 = undefined;

    //check with sufficient size
    const nt_path = try GetFinalPathNameByHandle(handle, .{ .volume_name = .Nt }, &buffer);
    _ = try GetFinalPathNameByHandle(handle, .{ .volume_name = .Dos }, &buffer);

    const required_len_in_u16 = nt_path.len + @divExact(@intFromPtr(nt_path.ptr) - @intFromPtr(&buffer), 2) + 1;
    //check with insufficient size
    try std.testing.expectError(error.NameTooLong, GetFinalPathNameByHandle(handle, .{ .volume_name = .Nt }, buffer[0 .. required_len_in_u16 - 1]));
    try std.testing.expectError(error.NameTooLong, GetFinalPathNameByHandle(handle, .{ .volume_name = .Dos }, buffer[0 .. required_len_in_u16 - 1]));

    //check with exactly-sufficient size
    _ = try GetFinalPathNameByHandle(handle, .{ .volume_name = .Nt }, buffer[0..required_len_in_u16]);
    _ = try GetFinalPathNameByHandle(handle, .{ .volume_name = .Dos }, buffer[0..required_len_in_u16]);
}

/// Equivalent to the MOUNTMGR_IS_VOLUME_NAME macro in mountmgr.h
fn mountmgrIsVolumeName(name: []const u16) bool {
    return (name.len == 48 or (name.len == 49 and name[48] == std.mem.nativeToLittle(u16, '\\'))) and
        name[0] == std.mem.nativeToLittle(u16, '\\') and
        (name[1] == std.mem.nativeToLittle(u16, '?') or name[1] == std.mem.nativeToLittle(u16, '\\')) and
        name[2] == std.mem.nativeToLittle(u16, '?') and
        name[3] == std.mem.nativeToLittle(u16, '\\') and
        std.mem.startsWith(u16, name[4..], std.unicode.utf8ToUtf16LeStringLiteral("Volume{")) and
        name[19] == std.mem.nativeToLittle(u16, '-') and
        name[24] == std.mem.nativeToLittle(u16, '-') and
        name[29] == std.mem.nativeToLittle(u16, '-') and
        name[34] == std.mem.nativeToLittle(u16, '-') and
        name[47] == std.mem.nativeToLittle(u16, '}');
}

test mountmgrIsVolumeName {
    @setEvalBranchQuota(2000);
    const L = std.unicode.utf8ToUtf16LeStringLiteral;
    try std.testing.expect(mountmgrIsVolumeName(L("\\\\?\\Volume{383da0b0-717f-41b6-8c36-00500992b58d}")));
    try std.testing.expect(mountmgrIsVolumeName(L("\\??\\Volume{383da0b0-717f-41b6-8c36-00500992b58d}")));
    try std.testing.expect(mountmgrIsVolumeName(L("\\\\?\\Volume{383da0b0-717f-41b6-8c36-00500992b58d}\\")));
    try std.testing.expect(mountmgrIsVolumeName(L("\\??\\Volume{383da0b0-717f-41b6-8c36-00500992b58d}\\")));
    try std.testing.expect(!mountmgrIsVolumeName(L("\\\\.\\Volume{383da0b0-717f-41b6-8c36-00500992b58d}")));
    try std.testing.expect(!mountmgrIsVolumeName(L("\\??\\Volume{383da0b0-717f-41b6-8c36-00500992b58d}\\foo")));
    try std.testing.expect(!mountmgrIsVolumeName(L("\\??\\Volume{383da0b0-717f-41b6-8c36-00500992b58}")));
}

pub const QueryObjectNameError = error{
    AccessDenied,
    InvalidHandle,
    NameTooLong,
    Unexpected,
};

pub fn QueryObjectName(handle: windows.HANDLE, out_buffer: []u16) QueryObjectNameError![]u16 {
    const out_buffer_aligned = std.mem.alignInSlice(out_buffer, @alignOf(windows.OBJECT.NAME_INFORMATION)) orelse return error.NameTooLong;

    const info: *windows.OBJECT.NAME_INFORMATION = @ptrCast(out_buffer_aligned);
    // buffer size is specified in bytes
    const out_buffer_len = std.math.cast(windows.ULONG, out_buffer_aligned.len * 2) orelse std.math.maxInt(windows.ULONG);
    // last argument would return the length required for full_buffer, not exposed here
    return switch (windows.ntdll.NtQueryObject(handle, .Name, info, out_buffer_len, null)) {
        .SUCCESS => {
            // info.Name from ObQueryNameString is documented to be empty if the object
            // was "unnamed", not sure if this can happen for file handles
            return if (info.Name.isEmpty()) error.Unexpected else info.Name.slice();
        },
        .ACCESS_DENIED => error.AccessDenied,
        .INVALID_HANDLE => error.InvalidHandle,
        // triggered when the buffer is too small for the OBJECT_NAME_INFORMATION object (.INFO_LENGTH_MISMATCH),
        // or if the buffer is too small for the file path returned (.BUFFER_OVERFLOW, .BUFFER_TOO_SMALL)
        .INFO_LENGTH_MISMATCH, .BUFFER_OVERFLOW, .BUFFER_TOO_SMALL => error.NameTooLong,
        else => |e| windows.unexpectedStatus(e),
    };
}

test QueryObjectName {
    if (builtin.os.tag != .windows)
        return;

    //any file will do; canonicalization works on NTFS junctions and symlinks, hardlinks remain separate paths.
    var tmp = std.testing.tmpDir(.{});
    defer tmp.cleanup();
    const handle = tmp.dir.handle;
    var out_buffer: [windows.PATH_MAX_WIDE]u16 = undefined;

    const result_path = try QueryObjectName(handle, &out_buffer);
    const required_len_in_u16 = result_path.len + @divExact(@intFromPtr(result_path.ptr) - @intFromPtr(&out_buffer), 2) + 1;
    //insufficient size
    try std.testing.expectError(error.NameTooLong, QueryObjectName(handle, out_buffer[0 .. required_len_in_u16 - 1]));
    //exactly-sufficient size
    _ = try QueryObjectName(handle, out_buffer[0..required_len_in_u16]);
}

const Wtf16ToPrefixedFileWError = error{
    AccessDenied,
    FileNotFound,
} || Dir.PathNameError || Io.Cancelable || Io.UnexpectedError;

const Wtf16ToPrefixedFileWOptions = struct {
    allow_relative: bool = true,
};

/// Converts the `path` to WTF16, null-terminated. If the path contains any
/// namespace prefix, or is anything but a relative path (rooted, drive relative,
/// etc) the result will have the NT-style prefix `\??\`.
///
/// Similar to RtlDosPathNameToNtPathName_U with a few differences:
/// - Does not allocate on the heap.
/// - Relative paths are kept as relative unless they contain too many ..
///   components, in which case they are resolved against the `dir` if it
///   is non-null, or the CWD if it is null.
/// - Special case device names like COM1, NUL, etc are not handled specially (TODO)
/// - . and space are not stripped from the end of relative paths (potential TODO)
pub fn wToPrefixedFileW(dir: ?windows.HANDLE, path: [:0]const u16, options: Wtf16ToPrefixedFileWOptions) Wtf16ToPrefixedFileWError!WindowsPathSpace {
    const nt_prefix = [_]u16{ '\\', '?', '?', '\\' };
    if (windows.hasCommonNtPrefix(u16, path)) {
        // TODO: Figure out a way to design an API that can avoid the copy for NT,
        //       since it is always returned fully unmodified.
        var path_space: WindowsPathSpace = undefined;
        path_space.data[0..nt_prefix.len].* = nt_prefix;
        const len_after_prefix = path.len - nt_prefix.len;
        @memcpy(path_space.data[nt_prefix.len..][0..len_after_prefix], path[nt_prefix.len..]);
        path_space.len = path.len;
        path_space.data[path_space.len] = 0;
        return path_space;
    } else {
        const path_type = Dir.path.getWin32PathType(u16, path);
        var path_space: WindowsPathSpace = undefined;
        if (path_type == .local_device) switch (getLocalDevicePathType(u16, path)) {
            .verbatim => {
                path_space.data[0..nt_prefix.len].* = nt_prefix;
                const len_after_prefix = path.len - nt_prefix.len;
                @memcpy(path_space.data[nt_prefix.len..][0..len_after_prefix], path[nt_prefix.len..]);
                path_space.len = path.len;
                path_space.data[path_space.len] = 0;
                return path_space;
            },
            .local_device, .fake_verbatim => {
                const path_byte_len = windows.ntdll.RtlGetFullPathName_U(
                    path.ptr,
                    path_space.data.len * 2,
                    &path_space.data,
                    null,
                );
                if (path_byte_len == 0) {
                    // TODO: This may not be the right error
                    return error.BadPathName;
                } else if (path_byte_len / 2 > path_space.data.len) {
                    return error.NameTooLong;
                }
                path_space.len = path_byte_len / 2;
                // Both prefixes will be normalized but retained, so all
                // we need to do now is replace them with the NT prefix
                path_space.data[0..nt_prefix.len].* = nt_prefix;
                return path_space;
            },
        };
        if (options.allow_relative and path_type == .relative) relative: {
            // TODO: Handle special case device names like COM1, AUX, NUL, CONIN$, CONOUT$, etc.
            //       See https://googleprojectzero.blogspot.com/2016/02/the-definitive-guide-on-win32-to-nt.html

            // TODO: Potentially strip all trailing . and space characters from the
            //       end of the path. This is something that both RtlDosPathNameToNtPathName_U
            //       and RtlGetFullPathName_U do. Technically, trailing . and spaces
            //       are allowed, but such paths may not interact well with Windows (i.e.
            //       files with these paths can't be deleted from explorer.exe, etc).
            //       This could be something that normalizePath may want to do.

            @memcpy(path_space.data[0..path.len], path);
            // Try to normalize, but if we get too many parent directories,
            // then we need to start over and use RtlGetFullPathName_U instead.
            path_space.len = windows.normalizePath(u16, path_space.data[0..path.len]) catch |err| switch (err) {
                error.TooManyParentDirs => break :relative,
            };
            path_space.data[path_space.len] = 0;
            return path_space;
        }
        // We now know we are going to return an absolute NT path, so
        // we can unconditionally prefix it with the NT prefix.
        path_space.data[0..nt_prefix.len].* = nt_prefix;
        if (path_type == .root_local_device) {
            // `\\.` and `\\?` always get converted to `\??\` exactly, so
            // we can just stop here
            path_space.len = nt_prefix.len;
            path_space.data[path_space.len] = 0;
            return path_space;
        }
        const path_buf_offset = switch (path_type) {
            // UNC paths will always start with `\\`. However, we want to
            // end up with something like `\??\UNC\server\share`, so to get
            // RtlGetFullPathName to write into the spot we want the `server`
            // part to end up, we need to provide an offset such that
            // the `\\` part gets written where the `C\` of `UNC\` will be
            // in the final NT path.
            .unc_absolute => nt_prefix.len + 2,
            else => nt_prefix.len,
        };
        const buf_len: u32 = @intCast(path_space.data.len - path_buf_offset);
        const path_to_get: [:0]const u16 = path_to_get: {
            // If dir is null, then we don't need to bother with GetFinalPathNameByHandle because
            // RtlGetFullPathName_U will resolve relative paths against the CWD for us.
            if (path_type != .relative or dir == null) {
                break :path_to_get path;
            }
            // We can also skip GetFinalPathNameByHandle if the handle matches
            // the handle returned by Io.Dir.cwd()
            if (dir.? == Io.Dir.cwd().handle) {
                break :path_to_get path;
            }
            // At this point, we know we have a relative path that had too many
            // `..` components to be resolved by normalizePath, so we need to
            // convert it into an absolute path and let RtlGetFullPathName_U
            // canonicalize it. We do this by getting the path of the `dir`
            // and appending the relative path to it.
            var dir_path_buf: [windows.PATH_MAX_WIDE:0]u16 = undefined;
            const dir_path = GetFinalPathNameByHandle(dir.?, .{}, &dir_path_buf) catch |err| switch (err) {
                // This mapping is not correct; it is actually expected
                // that calling GetFinalPathNameByHandle might return
                // error.UnrecognizedVolume, and in fact has been observed
                // in the wild. The problem is that wToPrefixedFileW was
                // never intended to make *any* OS syscall APIs. It's only
                // supposed to convert a string to one that is eligible to
                // be used in the ntdll syscalls.
                //
                // To solve this, this function needs to no longer call
                // GetFinalPathNameByHandle under any conditions, or the
                // calling function needs to get reworked to not need to
                // call this function.
                //
                // This may involve making breaking API changes.
                error.UnrecognizedVolume => return error.Unexpected,
                else => |e| return e,
            };
            if (dir_path.len + 1 + path.len > windows.PATH_MAX_WIDE) {
                return error.NameTooLong;
            }
            // We don't have to worry about potentially doubling up path separators
            // here since RtlGetFullPathName_U will handle canonicalizing it.
            dir_path_buf[dir_path.len] = '\\';
            @memcpy(dir_path_buf[dir_path.len + 1 ..][0..path.len], path);
            const full_len = dir_path.len + 1 + path.len;
            dir_path_buf[full_len] = 0;
            break :path_to_get dir_path_buf[0..full_len :0];
        };
        const path_byte_len = windows.ntdll.RtlGetFullPathName_U(
            path_to_get.ptr,
            buf_len * 2,
            path_space.data[path_buf_offset..].ptr,
            null,
        );
        if (path_byte_len == 0) {
            // TODO: This may not be the right error
            return error.BadPathName;
        } else if (path_byte_len / 2 > buf_len) {
            return error.NameTooLong;
        }
        path_space.len = path_buf_offset + (path_byte_len / 2);
        if (path_type == .unc_absolute) {
            // Now add in the UNC, the `C` should overwrite the first `\` of the
            // FullPathName, ultimately resulting in `\??\UNC\<the rest of the path>`
            assert(path_space.data[path_buf_offset] == '\\');
            assert(path_space.data[path_buf_offset + 1] == '\\');
            const unc = [_]u16{ 'U', 'N', 'C' };
            path_space.data[nt_prefix.len..][0..unc.len].* = unc;
        }
        return path_space;
    }
}

const LocalDevicePathType = enum {
    /// `\\.\` (path separators can be `\` or `/`)
    local_device,
    /// `\\?\`
    /// When converted to an NT path, everything past the prefix is left
    /// untouched and `\\?\` is replaced by `\??\`.
    verbatim,
    /// `\\?\` without all path separators being `\`.
    /// This seems to be recognized as a prefix, but the 'verbatim' aspect
    /// is not respected (i.e. if `//?/C:/foo` is converted to an NT path,
    /// it will become `\??\C:\foo` [it will be canonicalized and the //?/ won't
    /// be treated as part of the final path])
    fake_verbatim,
};

/// Only relevant for Win32 -> NT path conversion.
/// Asserts `path` is of type `Dir.path.Win32PathType.local_device`.
fn getLocalDevicePathType(comptime T: type, path: []const T) LocalDevicePathType {
    if (std.debug.runtime_safety) {
        assert(Dir.path.getWin32PathType(T, path) == .local_device);
    }

    const backslash = std.mem.nativeToLittle(T, '\\');
    const all_backslash = path[0] == backslash and
        path[1] == backslash and
        path[3] == backslash;
    return switch (path[2]) {
        std.mem.nativeToLittle(T, '?') => if (all_backslash) .verbatim else .fake_verbatim,
        std.mem.nativeToLittle(T, '.') => .local_device,
        else => unreachable,
    };
}

pub const Wtf8ToPrefixedFileWError = Wtf16ToPrefixedFileWError;

/// Same as `wToPrefixedFileW` but accepts a WTF-8 encoded path.
/// https://wtf-8.codeberg.page/
pub fn sliceToPrefixedFileW(dir: ?windows.HANDLE, path: []const u8, options: Wtf16ToPrefixedFileWOptions) Wtf8ToPrefixedFileWError!WindowsPathSpace {
    var temp_path: WindowsPathSpace = undefined;
    temp_path.len = std.unicode.wtf8ToWtf16Le(&temp_path.data, path) catch |err| switch (err) {
        error.InvalidWtf8 => return error.BadPathName,
    };
    temp_path.data[temp_path.len] = 0;
    return wToPrefixedFileW(dir, temp_path.span(), options);
}

pub const WindowsPathSpace = struct {
    data: [windows.PATH_MAX_WIDE:0]u16,
    len: usize,

    pub fn span(wps: *const WindowsPathSpace) [:0]const u16 {
        return wps.data[0..wps.len :0];
    }

    pub fn string(wps: *const WindowsPathSpace) windows.UNICODE_STRING {
        return .init(wps.span());
    }
};

fn dirRealPathFilePosix(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, out_buffer: []u8) Dir.RealPathFileError!usize {
    if (native_os == .wasi) return error.OperationUnsupported;

    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    if (builtin.link_libc and dir.handle == posix.AT.FDCWD) {
        if (out_buffer.len < posix.PATH_MAX) return error.NameTooLong;
        const syscall: Syscall = try .start();
        while (true) {
            if (std.c.realpath(sub_path_posix, out_buffer.ptr)) |redundant_pointer| {
                syscall.finish();
                assert(redundant_pointer == out_buffer.ptr);
                return std.mem.indexOfScalar(u8, out_buffer, 0) orelse out_buffer.len;
            }
            const err: posix.E = @enumFromInt(std.c._errno().*);
            if (err == .INTR) {
                try syscall.checkCancel();
                continue;
            }
            syscall.finish();
            switch (err) {
                .INVAL => return errnoBug(err),
                .BADF => return errnoBug(err),
                .FAULT => return errnoBug(err),
                .ACCES => return error.AccessDenied,
                .NOENT => return error.FileNotFound,
                .OPNOTSUPP => return error.OperationUnsupported,
                .NOTDIR => return error.NotDir,
                .NAMETOOLONG => return error.NameTooLong,
                .LOOP => return error.SymLinkLoop,
                .IO => return error.InputOutput,
                else => return posix.unexpectedErrno(err),
            }
        }
    }

    var flags: posix.O = .{};
    if (@hasField(posix.O, "NONBLOCK")) flags.NONBLOCK = true;
    if (@hasField(posix.O, "CLOEXEC")) flags.CLOEXEC = true;
    if (@hasField(posix.O, "PATH")) flags.PATH = true;

    const mode: posix.mode_t = 0;

    const syscall: Syscall = try .start();
    const fd: posix.fd_t = while (true) {
        const rc = openat_sym(dir.handle, sub_path_posix, flags, mode);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                break @intCast(rc);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
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
                    .SRCH => return error.FileNotFound, // Linux when accessing procfs.
                    .NOMEM => return error.SystemResources,
                    .NOSPC => return error.NoSpaceLeft,
                    .NOTDIR => return error.NotDir,
                    .PERM => return error.PermissionDenied,
                    .EXIST => return error.PathAlreadyExists,
                    .BUSY => return error.DeviceBusy,
                    .NXIO => return error.NoDevice,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    };
    defer closeFd(fd);
    return realPathPosix(fd, out_buffer);
}

const dirRealPath = switch (native_os) {
    .windows => dirRealPathWindows,
    else => dirRealPathPosix,
};

fn dirRealPathPosix(userdata: ?*anyopaque, dir: Dir, out_buffer: []u8) Dir.RealPathError!usize {
    if (native_os == .wasi) return error.OperationUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return realPathPosix(dir.handle, out_buffer);
}

fn dirRealPathWindows(userdata: ?*anyopaque, dir: Dir, out_buffer: []u8) Dir.RealPathError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return realPathWindows(dir.handle, out_buffer);
}

const fileRealPath = switch (native_os) {
    .windows => fileRealPathWindows,
    else => fileRealPathPosix,
};

fn fileRealPathWindows(userdata: ?*anyopaque, file: File, out_buffer: []u8) File.RealPathError!usize {
    if (native_os == .wasi) return error.OperationUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return realPathWindows(file.handle, out_buffer);
}

fn fileRealPathPosix(userdata: ?*anyopaque, file: File, out_buffer: []u8) File.RealPathError!usize {
    if (native_os == .wasi) return error.OperationUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return realPathPosix(file.handle, out_buffer);
}

fn realPathPosix(fd: posix.fd_t, out_buffer: []u8) File.RealPathError!usize {
    switch (native_os) {
        .dragonfly, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
            var sufficient_buffer: [posix.PATH_MAX]u8 = undefined;
            @memset(&sufficient_buffer, 0);
            const syscall: Syscall = try .start();
            while (true) {
                switch (posix.errno(posix.system.fcntl(fd, posix.F.GETPATH, &sufficient_buffer))) {
                    .SUCCESS => {
                        syscall.finish();
                        break;
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .ACCES => return error.AccessDenied,
                            .BADF => return error.FileNotFound,
                            .NOENT => return error.FileNotFound,
                            .NOMEM => return error.SystemResources,
                            .NOSPC => return error.NameTooLong,
                            .RANGE => return error.NameTooLong,
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            }
            const n = std.mem.indexOfScalar(u8, &sufficient_buffer, 0) orelse sufficient_buffer.len;
            if (n > out_buffer.len) return error.NameTooLong;
            @memcpy(out_buffer[0..n], sufficient_buffer[0..n]);
            return n;
        },
        .linux, .serenity, .illumos => {
            var procfs_buf: ["/proc/self/path/-2147483648\x00".len]u8 = undefined;
            const template = if (native_os == .illumos) "/proc/self/path/{d}" else "/proc/self/fd/{d}";
            const proc_path = std.fmt.bufPrintSentinel(&procfs_buf, template, .{fd}, 0) catch unreachable;
            const syscall: Syscall = try .start();
            while (true) {
                const rc = posix.system.readlink(proc_path, out_buffer.ptr, out_buffer.len);
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        const len: usize = @bitCast(rc);
                        return len;
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |e| {
                        syscall.finish();
                        switch (e) {
                            .ACCES => return error.AccessDenied,
                            .FAULT => |err| return errnoBug(err),
                            .IO => return error.FileSystem,
                            .LOOP => return error.SymLinkLoop,
                            .NAMETOOLONG => return error.NameTooLong,
                            .NOENT => return error.FileNotFound,
                            .NOMEM => return error.SystemResources,
                            .NOTDIR => return error.NotDir,
                            .ILSEQ => |err| return errnoBug(err),
                            else => |err| return posix.unexpectedErrno(err),
                        }
                    },
                }
            }
        },
        .freebsd => {
            var k_file: std.c.kinfo_file = undefined;
            k_file.structsize = std.c.KINFO_FILE_SIZE;
            const syscall: Syscall = try .start();
            while (true) {
                switch (posix.errno(std.c.fcntl(fd, std.c.F.KINFO, @intFromPtr(&k_file)))) {
                    .SUCCESS => {
                        syscall.finish();
                        break;
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    .BADF => {
                        syscall.finish();
                        return error.FileNotFound;
                    },
                    else => |err| {
                        syscall.finish();
                        return posix.unexpectedErrno(err);
                    },
                }
            }
            const len = std.mem.findScalar(u8, &k_file.path, 0) orelse k_file.path.len;
            if (len == 0) return error.NameTooLong;
            @memcpy(out_buffer[0..len], k_file.path[0..len]);
            return len;
        },
        else => return error.OperationUnsupported,
    }
    comptime unreachable;
}

fn fileHardLink(
    userdata: ?*anyopaque,
    file: File,
    new_dir: Dir,
    new_sub_path: []const u8,
    options: File.HardLinkOptions,
) File.HardLinkError!void {
    _ = userdata;
    if (native_os != .linux) return error.OperationUnsupported;

    var new_path_buffer: [posix.PATH_MAX]u8 = undefined;
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    const flags: u32 = if (options.follow_symlinks)
        posix.AT.SYMLINK_FOLLOW | posix.AT.EMPTY_PATH
    else
        posix.AT.EMPTY_PATH;

    return linkat(file.handle, "", new_dir.handle, new_sub_path_posix, flags) catch |err| switch (err) {
        error.FileNotFound => {
            if (options.follow_symlinks) return error.FileNotFound;
            var proc_buf: ["/proc/self/fd/-2147483648\x00".len]u8 = undefined;
            const proc_path = std.fmt.bufPrintSentinel(&proc_buf, "/proc/self/fd/{d}", .{file.handle}, 0) catch
                unreachable;
            return linkat(posix.AT.FDCWD, proc_path, new_dir.handle, new_sub_path_posix, posix.AT.SYMLINK_FOLLOW);
        },
        else => |e| return e,
    };
}

fn linkat(
    old_dir: posix.fd_t,
    old_path: [*:0]const u8,
    new_dir: posix.fd_t,
    new_path: [*:0]const u8,
    flags: u32,
) File.HardLinkError!void {
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.linkat(old_dir, old_path, new_dir, new_path, flags))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ACCES => return syscall.fail(error.AccessDenied),
            .DQUOT => return syscall.fail(error.DiskQuota),
            .EXIST => return syscall.fail(error.PathAlreadyExists),
            .IO => return syscall.fail(error.HardwareFailure),
            .LOOP => return syscall.fail(error.SymLinkLoop),
            .MLINK => return syscall.fail(error.LinkQuotaExceeded),
            .NAMETOOLONG => return syscall.fail(error.NameTooLong),
            .NOENT => return syscall.fail(error.FileNotFound),
            .NOMEM => return syscall.fail(error.SystemResources),
            .NOSPC => return syscall.fail(error.NoSpaceLeft),
            .NOTDIR => return syscall.fail(error.NotDir),
            .PERM => return syscall.fail(error.PermissionDenied),
            .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
            .XDEV => return syscall.fail(error.CrossDevice),
            .ILSEQ => return syscall.fail(error.BadPathName),
            .FAULT => |err| return syscall.errnoBug(err),
            .INVAL => |err| return syscall.errnoBug(err),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

const dirDeleteFile = switch (native_os) {
    .windows => dirDeleteFileWindows,
    .wasi => dirDeleteFileWasi,
    else => dirDeleteFilePosix,
};

fn dirDeleteFileWindows(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteFileError!void {
    return dirDeleteWindows(userdata, dir, sub_path, false) catch |err| switch (err) {
        error.DirNotEmpty => unreachable,
        else => |e| return e,
    };
}

fn dirDeleteFileWasi(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteFileError!void {
    if (builtin.link_libc) return dirDeleteFilePosix(userdata, dir, sub_path);
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const syscall: Syscall = try .start();
    while (true) {
        const res = std.os.wasi.path_unlink_file(dir.handle, sub_path.ptr, sub_path.len);
        switch (res) {
            .SUCCESS => {
                syscall.finish();
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => return error.AccessDenied,
                    .PERM => return error.PermissionDenied,
                    .BUSY => return error.FileBusy,
                    .FAULT => |err| return errnoBug(err),
                    .IO => return error.FileSystem,
                    .ISDIR => return error.IsDir,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    .INVAL => |err| return errnoBug(err), // invalid flags, or pathname has . as last component
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirDeleteFilePosix(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteFileError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.unlinkat(dir.handle, sub_path_posix, 0))) {
            .SUCCESS => {
                syscall.finish();
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            // Some systems return permission errors when trying to delete a
            // directory, so we need to handle that case specifically and
            // translate the error.
            .PERM => switch (native_os) {
                .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .freebsd, .netbsd, .dragonfly, .openbsd, .illumos => {

                    // Don't follow symlinks to match unlinkat (which acts on symlinks rather than follows them).
                    var st = std.mem.zeroes(posix.Stat);
                    while (true) {
                        try syscall.checkCancel();
                        switch (posix.errno(fstatat_sym(dir.handle, sub_path_posix, &st, posix.AT.SYMLINK_NOFOLLOW))) {
                            .SUCCESS => {
                                syscall.finish();
                                break;
                            },
                            .INTR => continue,
                            else => {
                                syscall.finish();
                                return error.PermissionDenied;
                            },
                        }
                    }
                    const is_dir = st.mode & posix.S.IFMT == posix.S.IFDIR;
                    if (is_dir)
                        return error.IsDir
                    else
                        return error.PermissionDenied;
                },
                else => {
                    syscall.finish();
                    return error.PermissionDenied;
                },
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => return error.AccessDenied,
                    .BUSY => return error.FileBusy,
                    .FAULT => |err| return errnoBug(err),
                    .IO => return error.FileSystem,
                    .ISDIR => return error.IsDir,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .EXIST => |err| return errnoBug(err),
                    .NOTEMPTY => |err| return errnoBug(err), // Not passing AT.REMOVEDIR
                    .ILSEQ => return error.BadPathName,
                    .INVAL => |err| return errnoBug(err), // invalid flags, or pathname has . as last component
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

const dirDeleteDir = switch (native_os) {
    .windows => dirDeleteDirWindows,
    .wasi => dirDeleteDirWasi,
    else => dirDeleteDirPosix,
};

fn dirDeleteDirWindows(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteDirError!void {
    return dirDeleteWindows(userdata, dir, sub_path, true) catch |err| switch (err) {
        error.IsDir => unreachable,
        else => |e| return e,
    };
}

fn dirDeleteWindows(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, remove_dir: bool) (Dir.DeleteDirError || Dir.DeleteFileError)!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const w = windows;

    if (std.mem.eql(u8, sub_path, "..")) {
        // Can't remove the parent directory with an open handle.
        return error.FileBusy;
    }
    var sub_path_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
    if (std.mem.eql(u8, sub_path, ".")) {
        // Windows does not recognize this, but it does work with empty string.
        sub_path_w.len = 0;
    }
    const attr: w.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w.span())) null else dir.handle,
        .ObjectName = @constCast(&sub_path_w.string()),
    };

    var io_status_block: w.IO_STATUS_BLOCK = undefined;
    var tmp_handle: w.HANDLE = undefined;
    {
        const syscall: Syscall = try .start();
        while (true) switch (w.ntdll.NtCreateFile(
            &tmp_handle,
            .{ .STANDARD = .{
                .RIGHTS = .{ .DELETE = true },
                .SYNCHRONIZE = true,
            } },
            &attr,
            &io_status_block,
            null,
            .{},
            .VALID_FLAGS,
            .OPEN,
            .{
                .DIRECTORY_FILE = remove_dir,
                .IO = .SYNCHRONOUS_NONALERT,
                .NON_DIRECTORY_FILE = !remove_dir,
                .OPEN_REPARSE_POINT = true, // would we ever want to delete the target instead?
            },
            null,
            0,
        )) {
            .SUCCESS => break syscall.finish(),
            .OBJECT_NAME_INVALID => |err| return syscall.ntstatusBug(err),
            .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .BAD_NETWORK_PATH => return syscall.fail(error.NetworkNotFound), // \\server was not found
            .BAD_NETWORK_NAME => return syscall.fail(error.NetworkNotFound), // \\server was found but \\server\share wasn't
            .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
            .FILE_IS_A_DIRECTORY => return syscall.fail(error.IsDir),
            .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
            .SHARING_VIOLATION => return syscall.fail(error.FileBusy),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .DELETE_PENDING => return syscall.finish(),
            else => |rc| return syscall.unexpectedNtstatus(rc),
        };
    }
    defer w.CloseHandle(tmp_handle);

    // FileDispositionInformationEx has varying levels of support:
    // - FILE_DISPOSITION_INFORMATION_EX requires >= win10_rs1
    //   (INVALID_INFO_CLASS is returned if not supported)
    // - Requires the NTFS filesystem
    //   (on filesystems like FAT32, INVALID_PARAMETER is returned)
    // - FILE_DISPOSITION_POSIX_SEMANTICS requires >= win10_rs1
    // - FILE_DISPOSITION_IGNORE_READONLY_ATTRIBUTE requires >= win10_rs5
    //   (NOT_SUPPORTED is returned if a flag is unsupported)
    //
    // The strategy here is just to try using FileDispositionInformationEx and fall back to
    // FileDispositionInformation if the return value lets us know that some aspect of it is not supported.
    const rc = rc: {
        // Deletion with posix semantics if the filesystem supports it.
        var info: w.FILE.DISPOSITION.INFORMATION.EX = .{ .Flags = .{
            .DELETE = true,
            .POSIX_SEMANTICS = true,
            .IGNORE_READONLY_ATTRIBUTE = true,
        } };

        const syscall: Syscall = try .start();
        while (true) switch (w.ntdll.NtSetInformationFile(
            tmp_handle,
            &io_status_block,
            &info,
            @sizeOf(w.FILE.DISPOSITION.INFORMATION.EX),
            .DispositionEx,
        )) {
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            // The filesystem does not support FileDispositionInformationEx
            .INVALID_PARAMETER,
            // The operating system does not support FileDispositionInformationEx
            .INVALID_INFO_CLASS,
            // The operating system does not support one of the flags
            .NOT_SUPPORTED,
            => break, // use fallback path below; `syscall` still active

            // For all other statuses, fall down to the switch below to handle them.
            else => |rc| {
                syscall.finish();
                break :rc rc;
            },
        };

        // Deletion with file pending semantics, which requires waiting or moving
        // files to get them removed (from here).
        var file_dispo: w.FILE.DISPOSITION.INFORMATION = .{ .DeleteFile = .TRUE };

        while (true) switch (w.ntdll.NtSetInformationFile(
            tmp_handle,
            &io_status_block,
            &file_dispo,
            @sizeOf(w.FILE.DISPOSITION.INFORMATION),
            .Disposition,
        )) {
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            else => |rc| {
                syscall.finish();
                break :rc rc;
            },
        };
    };
    switch (rc) {
        .SUCCESS => {},
        .DIRECTORY_NOT_EMPTY => return error.DirNotEmpty,
        .INVALID_PARAMETER => |err| return w.statusBug(err),
        .CANNOT_DELETE => return error.AccessDenied,
        .MEDIA_WRITE_PROTECTED => return error.AccessDenied,
        .ACCESS_DENIED => return error.AccessDenied,
        else => return w.unexpectedStatus(rc),
    }
}

fn dirDeleteDirWasi(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteDirError!void {
    if (builtin.link_libc) return dirDeleteDirPosix(userdata, dir, sub_path);

    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    const syscall: Syscall = try .start();
    while (true) {
        const res = std.os.wasi.path_remove_directory(dir.handle, sub_path.ptr, sub_path.len);
        switch (res) {
            .SUCCESS => {
                syscall.finish();
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => return error.AccessDenied,
                    .PERM => return error.PermissionDenied,
                    .BUSY => return error.FileBusy,
                    .FAULT => |err| return errnoBug(err),
                    .IO => return error.FileSystem,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .NOTEMPTY => return error.DirNotEmpty,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    .INVAL => |err| return errnoBug(err), // invalid flags, or pathname has . as last component
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirDeleteDirPosix(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteDirError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.unlinkat(dir.handle, sub_path_posix, posix.AT.REMOVEDIR))) {
            .SUCCESS => {
                syscall.finish();
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => return error.AccessDenied,
                    .PERM => return error.PermissionDenied,
                    .BUSY => return error.FileBusy,
                    .FAULT => |err| return errnoBug(err),
                    .IO => return error.FileSystem,
                    .ISDIR => |err| return errnoBug(err),
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .EXIST => |err| return errnoBug(err),
                    .NOTEMPTY => return error.DirNotEmpty,
                    .ILSEQ => return error.BadPathName,
                    .INVAL => |err| return errnoBug(err), // invalid flags, or pathname has . as last component
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

const dirRename = switch (native_os) {
    .windows => dirRenameWindows,
    .wasi => dirRenameWasi,
    else => dirRenamePosix,
};

fn dirRenameWindows(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenameError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return dirRenameWindowsInner(old_dir, old_sub_path, new_dir, new_sub_path, true) catch |err| switch (err) {
        error.PathAlreadyExists => return error.Unexpected,
        error.OperationUnsupported => return error.Unexpected,
        else => |e| return e,
    };
}

fn dirRenamePreserve(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenamePreserveError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (is_windows) return dirRenameWindowsInner(old_dir, old_sub_path, new_dir, new_sub_path, false);
    if (native_os == .linux) return dirRenamePreserveLinux(old_dir, old_sub_path, new_dir, new_sub_path);
    // Make a hard link then delete the original.
    try dirHardLink(t, old_dir, old_sub_path, new_dir, new_sub_path, .{ .follow_symlinks = false });
    const prev = swapCancelProtection(t, .blocked);
    defer _ = swapCancelProtection(t, prev);
    dirDeleteFile(t, old_dir, old_sub_path) catch {};
}

fn dirRenameWindowsInner(
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
    replace_if_exists: bool,
) Dir.RenamePreserveError!void {
    const w = windows;
    const old_path_w_buf = try sliceToPrefixedFileW(old_dir.handle, old_sub_path, .{});
    const old_path_w = old_path_w_buf.span();
    const new_path_w_buf = try sliceToPrefixedFileW(new_dir.handl
```
