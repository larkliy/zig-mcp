```
sitional(userdata: ?*anyopaque, file: File, data: []const []u8, offset: u64) File.ReadPositionalError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    if (is_windows) return fileReadPositionalWindows(file, data, offset);
    return fileReadPositionalPosix(file, data, offset);
}

fn fileReadPositionalWindows(file: File, data: []const []u8, offset: u64) File.ReadPositionalError!usize {
    var index: usize = 0;
    while (index < data.len and data[index].len == 0) index += 1;
    if (index == data.len) return 0;
    const buffer = data[index];

    return readFilePositionalWindows(file, buffer, offset);
}

fn readFilePositionalWindows(file: File, buffer: []u8, offset: u64) File.ReadPositionalError!usize {
    var iosb: windows.IO_STATUS_BLOCK = undefined;
    const short_buffer_len = std.math.lossyCast(u32, buffer.len);
    const signed_offset: windows.LARGE_INTEGER = @intCast(offset);
    if (file.flags.nonblocking) {
        var done: bool = false;
        switch (windows.ntdll.NtReadFile(
            file.handle,
            null, // event
            flagApc,
            &done, // APC context
            &iosb,
            buffer.ptr,
            short_buffer_len,
            &signed_offset,
            null, // key
        )) {
            // We must wait for the APC routine.
            .PENDING, .SUCCESS => while (!done) {
                // Once we get here we must not return from the function until the
                // operation completes, thereby releasing reference to the iosb.
                const alertable_syscall = AlertableSyscall.start() catch |err| switch (err) {
                    error.Canceled => |e| {
                        var cancel_iosb: windows.IO_STATUS_BLOCK = undefined;
                        _ = windows.ntdll.NtCancelIoFileEx(file.handle, &iosb, &cancel_iosb);
                        while (!done) waitForApcOrAlert();
                        return e;
                    },
                };
                waitForApcOrAlert();
                alertable_syscall.finish();
            },
            else => |status| iosb.u.Status = status,
        }
    } else {
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtReadFile(
            file.handle,
            null, // event
            null, // APC routine
            null, // APC context
            &iosb,
            buffer.ptr,
            short_buffer_len,
            &signed_offset,
            null, // key
        )) {
            .PENDING => unreachable, // unrecoverable: wrong File nonblocking flag
            .CANCELLED => try syscall.checkCancel(),
            else => |status| {
                syscall.finish();
                iosb.u.Status = status;
                break;
            },
        };
    }
    return ntReadFileResult(&iosb) catch |err| switch (err) {
        error.EndOfStream => 0,
        else => |e| e,
    };
}

fn fileSeekBy(userdata: ?*anyopaque, file: File, offset: i64) File.SeekError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        var iosb: windows.IO_STATUS_BLOCK = undefined;
        var info: windows.FILE.POSITION_INFORMATION = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtQueryInformationFile(
            file.handle,
            &iosb,
            &info,
            @sizeOf(windows.FILE.POSITION_INFORMATION),
            .Position,
        )) {
            .SUCCESS => break,
            .CANCELLED => try syscall.checkCancel(),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .PIPE_NOT_AVAILABLE => return syscall.fail(error.Unseekable),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
        info.CurrentByteOffset = @bitCast((if (offset >= 0) std.math.add(
            u64,
            @bitCast(info.CurrentByteOffset),
            @intCast(offset),
        ) else std.math.sub(
            u64,
            @bitCast(info.CurrentByteOffset),
            @intCast(-offset),
        )) catch |err| switch (err) {
            error.Overflow => return syscall.fail(error.Unseekable),
        });
        while (true) switch (windows.ntdll.NtSetInformationFile(
            file.handle,
            &iosb,
            &info,
            @sizeOf(windows.FILE.POSITION_INFORMATION),
            .Position,
        )) {
            .SUCCESS => return syscall.finish(),
            .CANCELLED => try syscall.checkCancel(),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .PIPE_NOT_AVAILABLE => return syscall.fail(error.Unseekable),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    if (native_os == .wasi and !builtin.link_libc) {
        var new_offset: std.os.wasi.filesize_t = undefined;
        const syscall: Syscall = try .start();
        while (true) {
            switch (std.os.wasi.fd_seek(file.handle, offset, .CUR, &new_offset)) {
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
                        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                        .INVAL => return error.Unseekable,
                        .OVERFLOW => return error.Unseekable,
                        .SPIPE => return error.Unseekable,
                        .NXIO => return error.Unseekable,
                        .NOTCAPABLE => return error.AccessDenied,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    if (posix.SEEK == void) return error.Unseekable;

    if (native_os == .linux and !builtin.link_libc and @sizeOf(usize) == 4) {
        var result: u64 = undefined;
        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(posix.system.llseek(file.handle, @bitCast(offset), &result, posix.SEEK.CUR))) {
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
                        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                        .INVAL => return error.Unseekable,
                        .OVERFLOW => return error.Unseekable,
                        .SPIPE => return error.Unseekable,
                        .NXIO => return error.Unseekable,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(lseek_sym(file.handle, offset, posix.SEEK.CUR))) {
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
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .INVAL => return error.Unseekable,
                    .OVERFLOW => return error.Unseekable,
                    .SPIPE => return error.Unseekable,
                    .NXIO => return error.Unseekable,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileSeekTo(userdata: ?*anyopaque, file: File, offset: u64) File.SeekError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        var iosb: windows.IO_STATUS_BLOCK = undefined;
        var info: windows.FILE.POSITION_INFORMATION = .{ .CurrentByteOffset = @bitCast(offset) };
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtSetInformationFile(
            file.handle,
            &iosb,
            &info,
            @sizeOf(windows.FILE.POSITION_INFORMATION),
            .Position,
        )) {
            .SUCCESS => return syscall.finish(),
            .CANCELLED => try syscall.checkCancel(),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .PIPE_NOT_AVAILABLE => return syscall.fail(error.Unseekable),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    if (native_os == .wasi and !builtin.link_libc) {
        const syscall: Syscall = try .start();
        while (true) {
            var new_offset: std.os.wasi.filesize_t = undefined;
            switch (std.os.wasi.fd_seek(file.handle, @bitCast(offset), .SET, &new_offset)) {
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
                        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                        .INVAL => return error.Unseekable,
                        .OVERFLOW => return error.Unseekable,
                        .SPIPE => return error.Unseekable,
                        .NXIO => return error.Unseekable,
                        .NOTCAPABLE => return error.AccessDenied,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    if (posix.SEEK == void) return error.Unseekable;

    return posixSeekTo(file.handle, offset);
}

fn posixSeekTo(fd: posix.fd_t, offset: u64) File.SeekError!void {
    if (native_os == .linux and !builtin.link_libc and @sizeOf(usize) == 4) {
        const syscall: Syscall = try .start();
        while (true) {
            var result: u64 = undefined;
            switch (posix.errno(posix.system.llseek(fd, offset, &result, posix.SEEK.SET))) {
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
                        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                        .INVAL => return error.Unseekable,
                        .OVERFLOW => return error.Unseekable,
                        .SPIPE => return error.Unseekable,
                        .NXIO => return error.Unseekable,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(lseek_sym(fd, @bitCast(offset), posix.SEEK.SET))) {
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
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .INVAL => return error.Unseekable,
                    .OVERFLOW => return error.Unseekable,
                    .SPIPE => return error.Unseekable,
                    .NXIO => return error.Unseekable,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn processExecutableOpen(userdata: ?*anyopaque, flags: Dir.OpenFileOptions) process.OpenExecutableError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    switch (native_os) {
        .wasi => return error.OperationUnsupported,
        .linux, .serenity => return dirOpenFilePosix(t, .{ .handle = posix.AT.FDCWD }, "/proc/self/exe", flags),
        .windows => {
            // If ImagePathName is a symlink, then it will contain the path of the symlink,
            // not the path that the symlink points to. However, because we are opening
            // the file, we can let the openFileW call follow the symlink for us.
            const image_path_name = windows.peb().ProcessParameters.ImagePathName.sliceZ();
            const prefixed_path_w = try wToPrefixedFileW(null, image_path_name, .{});
            return dirOpenFileWtf16(null, prefixed_path_w.span(), flags);
        },
        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .visionos,
        .watchos,
        => {
            // _NSGetExecutablePath() returns a path that might be a symlink to
            // the executable. Here it does not matter since we open it.
            var symlink_path_buf: [posix.PATH_MAX + 1]u8 = undefined;
            var n: u32 = symlink_path_buf.len;
            const rc = std.c._NSGetExecutablePath(&symlink_path_buf, &n);
            if (rc != 0) return error.NameTooLong;
            const symlink_path = std.mem.sliceTo(&symlink_path_buf, 0);
            return dirOpenFilePosix(t, .cwd(), symlink_path, flags);
        },
        else => {
            var buffer: [Dir.max_path_bytes]u8 = undefined;
            const n = try processExecutablePath(t, &buffer);
            buffer[n] = 0;
            const executable_path = buffer[0..n :0];
            return dirOpenFilePosix(t, .cwd(), executable_path, flags);
        },
    }
}

fn processExecutablePath(userdata: ?*anyopaque, out_buffer: []u8) process.ExecutablePathError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    switch (native_os) {
        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .visionos,
        .watchos,
        => {
            // _NSGetExecutablePath() returns a path that might be a symlink to
            // the executable.
            var symlink_path_buf: [posix.PATH_MAX + 1]u8 = undefined;
            var n: u32 = symlink_path_buf.len;
            const rc = std.c._NSGetExecutablePath(&symlink_path_buf, &n);
            if (rc != 0) return error.NameTooLong;
            const symlink_path = std.mem.sliceTo(&symlink_path_buf, 0);
            return Io.Dir.realPathFileAbsolute(io(t), symlink_path, out_buffer) catch |err| switch (err) {
                error.NetworkNotFound => unreachable, // Windows-only
                error.FileBusy => unreachable, // Windows-only
                else => |e| return e,
            };
        },
        .linux, .serenity => return Io.Dir.readLinkAbsolute(io(t), "/proc/self/exe", out_buffer) catch |err| switch (err) {
            error.UnsupportedReparsePointType => unreachable, // Windows-only
            error.NetworkNotFound => unreachable, // Windows-only
            error.FileBusy => unreachable, // Windows-only
            else => |e| return e,
        },
        .illumos => return Io.Dir.readLinkAbsolute(io(t), "/proc/self/path/a.out", out_buffer) catch |err| switch (err) {
            error.UnsupportedReparsePointType => unreachable, // Windows-only
            error.NetworkNotFound => unreachable, // Windows-only
            error.FileBusy => unreachable, // Windows-only
            else => |e| return e,
        },
        .freebsd, .dragonfly => {
            var mib: [4]c_int = .{ posix.CTL.KERN, posix.KERN.PROC, posix.KERN.PROC_PATHNAME, -1 };
            var out_len: usize = out_buffer.len;
            const syscall: Syscall = try .start();
            while (true) switch (posix.errno(posix.system.sysctl(&mib, mib.len, out_buffer.ptr, &out_len, null, 0))) {
                .SUCCESS => {
                    syscall.finish();
                    return out_len - 1; // discard terminating NUL
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                .PERM => return syscall.fail(error.PermissionDenied),
                .NOMEM => return syscall.fail(error.SystemResources),
                .FAULT => |err| return syscall.errnoBug(err),
                .NOENT => |err| return syscall.errnoBug(err),
                else => |err| return syscall.unexpectedErrno(err),
            };
        },
        .netbsd => {
            var mib = [4]c_int{ posix.CTL.KERN, posix.KERN.PROC_ARGS, -1, posix.KERN.PROC_PATHNAME };
            var out_len: usize = out_buffer.len;
            const syscall: Syscall = try .start();
            while (true) {
                switch (posix.errno(posix.system.sysctl(&mib, mib.len, out_buffer.ptr, &out_len, null, 0))) {
                    .SUCCESS => {
                        syscall.finish();
                        return out_len - 1; // discard terminating NUL
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    .PERM => return syscall.fail(error.PermissionDenied),
                    .NOMEM => return syscall.fail(error.SystemResources),
                    .FAULT => |err| return syscall.errnoBug(err),
                    .NOENT => |err| return syscall.errnoBug(err),
                    else => |err| return syscall.unexpectedErrno(err),
                }
            }
        },
        .openbsd, .haiku => {
            // The best we can do on these operating systems is check based on
            // the first process argument.
            const argv0 = std.mem.span(t.argv0.value orelse return error.OperationUnsupported);
            if (std.mem.findScalar(u8, argv0, '/') != null) {
                // argv[0] is a path (relative or absolute): use realpath(3) directly
                var resolved_buf: [std.c.PATH_MAX]u8 = undefined;
                const syscall: Syscall = try .start();
                while (true) {
                    if (std.c.realpath(argv0, &resolved_buf)) |p| {
                        assert(p == &resolved_buf);
                        break syscall.finish();
                    } else switch (@as(std.c.E, @enumFromInt(std.c._errno().*))) {
                        .INTR => {
                            try syscall.checkCancel();
                            continue;
                        },
                        else => |e| {
                            syscall.finish();
                            switch (e) {
                                .ACCES => return error.AccessDenied,
                                .INVAL => |err| return errnoBug(err), // the pathname argument is a null pointer
                                .IO => return error.InputOutput,
                                .LOOP => return error.SymLinkLoop,
                                .NAMETOOLONG => return error.NameTooLong,
                                .NOENT => return error.FileNotFound,
                                .NOTDIR => return error.NotDir,
                                .NOMEM => |err| return errnoBug(err), // sufficient storage space is unavailable for allocation
                                else => |err| return posix.unexpectedErrno(err),
                            }
                        },
                    }
                }
                const resolved = std.mem.sliceTo(&resolved_buf, 0);
                if (resolved.len > out_buffer.len)
                    return error.NameTooLong;
                @memcpy(out_buffer[0..resolved.len], resolved);
                return resolved.len;
            } else if (argv0.len != 0) {
                // argv[0] is not empty (and not a path): search PATH
                t.scanEnviron();
                const PATH = t.environ.string.PATH orelse return error.FileNotFound;
                var it = std.mem.tokenizeScalar(u8, PATH, ':');
                it: while (it.next()) |dir| {
                    var resolved_path_buf: [std.c.PATH_MAX]u8 = undefined;
                    const resolved_path = std.fmt.bufPrintSentinel(&resolved_path_buf, "{s}/{s}", .{
                        dir, argv0,
                    }, 0) catch continue;

                    var resolved_buf: [std.c.PATH_MAX]u8 = undefined;
                    const syscall: Syscall = try .start();
                    while (true) {
                        if (std.c.realpath(resolved_path, &resolved_buf)) |p| {
                            assert(p == &resolved_buf);
                            break syscall.finish();
                        } else switch (@as(std.c.E, @enumFromInt(std.c._errno().*))) {
                            .INTR => {
                                try syscall.checkCancel();
                                continue;
                            },
                            .NAMETOOLONG => {
                                syscall.finish();
                                return error.NameTooLong;
                            },
                            .NOMEM => {
                                syscall.finish();
                                return error.SystemResources;
                            },
                            .IO => {
                                syscall.finish();
                                return error.InputOutput;
                            },
                            .ACCES, .LOOP, .NOENT, .NOTDIR => {
                                syscall.finish();
                                continue :it;
                            },
                            else => |err| {
                                syscall.finish();
                                return posix.unexpectedErrno(err);
                            },
                        }
                    }
                    const resolved = std.mem.sliceTo(&resolved_buf, 0);
                    if (resolved.len > out_buffer.len)
                        return error.NameTooLong;
                    @memcpy(out_buffer[0..resolved.len], resolved);
                    return resolved.len;
                }
            }
            return error.FileNotFound;
        },
        .windows => {
            // If ImagePathName is a symlink, then it will contain the path of the
            // symlink, not the path that the symlink points to. We want the path
            // that the symlink points to, though, so we need to get the realpath.
            var path_name_w_buf = try wToPrefixedFileW(
                null,
                windows.peb().ProcessParameters.ImagePathName.sliceZ(),
                .{},
            );

            const h_file = handle: {
                if (OpenFile(path_name_w_buf.span(), .{
                    .dir = null,
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
                    error.FileBusy => unreachable,
                    else => |e| return e,
                }
            };
            defer windows.CloseHandle(h_file);

            const wide_slice = try GetFinalPathNameByHandle(h_file, .{}, &path_name_w_buf.data);

            const len = std.unicode.calcWtf8Len(wide_slice);
            if (len > out_buffer.len)
                return error.NameTooLong;

            const end_index = std.unicode.wtf16LeToWtf8(out_buffer, wide_slice);
            return end_index;
        },
        else => return error.OperationUnsupported,
    }
}

fn fileWritePositional(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
    offset: u64,
) File.WritePositionalError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        if (header.len != 0) {
            return writeFilePositionalWindows(file, header, offset);
        }
        for (data[0 .. data.len - 1]) |buf| {
            if (buf.len == 0) continue;
            return writeFilePositionalWindows(file, buf, offset);
        }
        const pattern = data[data.len - 1];
        if (pattern.len == 0 or splat == 0) return 0;
        return writeFilePositionalWindows(file, pattern, offset);
    }

    var iovecs: [max_iovecs_len]posix.iovec_const = undefined;
    var iovlen: iovlen_t = 0;
    addBuf(&iovecs, &iovlen, header);
    for (data[0 .. data.len - 1]) |bytes| addBuf(&iovecs, &iovlen, bytes);
    const pattern = data[data.len - 1];

    var splat_backup_buffer: [splat_buffer_size]u8 = undefined;
    if (iovecs.len - iovlen != 0) switch (splat) {
        0 => {},
        1 => addBuf(&iovecs, &iovlen, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &splat_backup_buffer;
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

    if (iovlen == 0) return 0;

    if (native_os == .wasi and !builtin.link_libc) {
        var n_written: usize = undefined;
        const syscall: Syscall = try .start();
        while (true) {
            switch (std.os.wasi.fd_pwrite(file.handle, &iovecs, iovlen, offset, &n_written)) {
                .SUCCESS => {
                    syscall.finish();
                    return n_written;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .INVAL => |err| return errnoBug(err),
                        .FAULT => |err| return errnoBug(err),
                        .AGAIN => |err| return errnoBug(err),
                        .BADF => return error.NotOpenForWriting,
                        .DESTADDRREQ => |err| return errnoBug(err), // `connect` was never called.
                        .DQUOT => return error.DiskQuota,
                        .FBIG => return error.FileTooBig,
                        .IO => return error.InputOutput,
                        .NOSPC => return error.NoSpaceLeft,
                        .PERM => return error.PermissionDenied,
                        .PIPE => return error.BrokenPipe,
                        .NOTCAPABLE => return error.AccessDenied,
                        .NXIO => return error.Unseekable,
                        .SPIPE => return error.Unseekable,
                        .OVERFLOW => return error.Unseekable,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        const rc = pwritev_sym(file.handle, &iovecs, @intCast(iovlen), @bitCast(offset));
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                return @intCast(rc);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .INVAL => |err| return syscall.errnoBug(err),
            .FAULT => |err| return syscall.errnoBug(err),
            .DESTADDRREQ => |err| return syscall.errnoBug(err), // `connect` was never called.
            .CONNRESET => |err| return syscall.errnoBug(err), // Not a socket handle.
            .BADF => return syscall.fail(error.NotOpenForWriting),
            .AGAIN => return syscall.fail(error.WouldBlock),
            .DQUOT => return syscall.fail(error.DiskQuota),
            .FBIG => return syscall.fail(error.FileTooBig),
            .IO => return syscall.fail(error.InputOutput),
            .NOSPC => return syscall.fail(error.NoSpaceLeft),
            .PERM => return syscall.fail(error.PermissionDenied),
            .PIPE => return syscall.fail(error.BrokenPipe),
            .BUSY => return syscall.fail(error.DeviceBusy),
            .TXTBSY => return syscall.fail(error.FileBusy),
            .NXIO => return syscall.fail(error.Unseekable),
            .SPIPE => return syscall.fail(error.Unseekable),
            .OVERFLOW => return syscall.fail(error.Unseekable),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn writeFilePositionalWindows(file: File, buffer: []const u8, offset: u64) File.WritePositionalError!usize {
    assert(buffer.len != 0);
    var iosb: windows.IO_STATUS_BLOCK = undefined;
    const short_buffer_len = std.math.lossyCast(u32, buffer.len);
    const signed_offset: windows.LARGE_INTEGER = @intCast(offset);
    if (file.flags.nonblocking) {
        var done: bool = false;
        switch (windows.ntdll.NtWriteFile(
            file.handle,
            null, // event
            flagApc,
            &done, // APC context
            &iosb,
            buffer.ptr,
            short_buffer_len,
            &signed_offset,
            null, // key
        )) {
            // We must wait for the APC routine.
            .PENDING, .SUCCESS => while (!done) {
                // Once we get here we must not return from the function until the
                // operation completes, thereby releasing reference to the iosb.
                const alertable_syscall = AlertableSyscall.start() catch |err| switch (err) {
                    error.Canceled => |e| {
                        var cancel_iosb: windows.IO_STATUS_BLOCK = undefined;
                        _ = windows.ntdll.NtCancelIoFileEx(file.handle, &iosb, &cancel_iosb);
                        while (!done) waitForApcOrAlert();
                        return e;
                    },
                };
                waitForApcOrAlert();
                alertable_syscall.finish();
            },
            else => |status| iosb.u.Status = status,
        }
    } else {
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtWriteFile(
            file.handle,
            null, // event
            null, // APC routine
            null, // APC context
            &iosb,
            buffer.ptr,
            short_buffer_len,
            &signed_offset,
            null, // key
        )) {
            .PENDING => unreachable, // unrecoverable: wrong File nonblocking flag
            .CANCELLED => try syscall.checkCancel(),
            else => |status| {
                syscall.finish();
                iosb.u.Status = status;
                return ntWriteFileResult(&iosb);
            },
        };
    }
    return ntWriteFileResult(&iosb);
}

fn fileWriteStreaming(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
) File.Writer.Error!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        const buffer = windowsWriteBuffer(header, data, splat);
        if (buffer.len == 0) return 0;
        return fileWriteStreamingWindows(file, buffer);
    }

    var iovecs: [max_iovecs_len]posix.iovec_const = undefined;
    var iovlen: iovlen_t = 0;
    addBuf(&iovecs, &iovlen, header);
    for (data[0 .. data.len - 1]) |bytes| addBuf(&iovecs, &iovlen, bytes);
    const pattern = data[data.len - 1];

    var splat_backup_buffer: [splat_buffer_size]u8 = undefined;
    if (iovecs.len - iovlen != 0) switch (splat) {
        0 => {},
        1 => addBuf(&iovecs, &iovlen, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &splat_backup_buffer;
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

    if (iovlen == 0) return 0;

    if (native_os == .wasi and !builtin.link_libc) {
        var n_written: usize = undefined;
        const syscall: Syscall = try .start();
        while (true) {
            switch (std.os.wasi.fd_write(file.handle, &iovecs, iovlen, &n_written)) {
                .SUCCESS => {
                    syscall.finish();
                    return n_written;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .INVAL => |err| return errnoBug(err),
                        .FAULT => |err| return errnoBug(err),
                        .AGAIN => |err| return errnoBug(err),
                        .BADF => return error.NotOpenForWriting, // can be a race condition.
                        .DESTADDRREQ => |err| return errnoBug(err), // `connect` was never called.
                        .DQUOT => return error.DiskQuota,
                        .FBIG => return error.FileTooBig,
                        .IO => return error.InputOutput,
                        .NOSPC => return error.NoSpaceLeft,
                        .PERM => return error.PermissionDenied,
                        .PIPE => return error.BrokenPipe,
                        .NOTCAPABLE => return error.AccessDenied,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.writev(file.handle, &iovecs, @intCast(iovlen));
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                return @intCast(rc);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
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
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileWriteStreamingWindows(file: File, buffer: []const u8) File.Writer.Error!usize {
    assert(buffer.len != 0);
    var iosb: windows.IO_STATUS_BLOCK = undefined;
    if (file.flags.nonblocking) {
        var done: bool = false;
        switch (windows.ntdll.NtWriteFile(
            file.handle,
            null, // event
            flagApc,
            &done, // APC context
            &iosb,
            buffer.ptr,
            @intCast(buffer.len),
            null, // byte offset
            null, // key
        )) {
            // We must wait for the APC routine.
            .PENDING, .SUCCESS => while (!done) {
                // Once we get here we must not return from the function until the
                // operation completes, thereby releasing reference to io_status_block.
                const alertable_syscall = AlertableSyscall.start() catch |err| switch (err) {
                    error.Canceled => |e| {
                        var cancel_iosb: windows.IO_STATUS_BLOCK = undefined;
                        _ = windows.ntdll.NtCancelIoFileEx(file.handle, &iosb, &cancel_iosb);
                        while (!done) waitForApcOrAlert();
                        return e;
                    },
                };
                waitForApcOrAlert();
                alertable_syscall.finish();
            },
            else => |status| iosb.u.Status = status,
        }
    } else {
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtWriteFile(
            file.handle,
            null, // event
            null, // APC routine
            null, // APC context
            &iosb,
            buffer.ptr,
            @intCast(buffer.len),
            null, // byte offset
            null, // key
        )) {
            .PENDING => unreachable, // unrecoverable: wrong File nonblocking flag
            .CANCELLED => try syscall.checkCancel(),
            else => |status| {
                syscall.finish();
                iosb.u.Status = status;
                break;
            },
        };
    }
    return ntWriteFileResult(&iosb);
}

fn fileWriteFileStreaming(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
) File.Writer.WriteFileError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const reader_buffered = file_reader.interface.buffered();
    if (reader_buffered.len >= @intFromEnum(limit)) {
        const n = try fileWriteStreaming(t, file, header, &.{limit.slice(reader_buffered)}, 1);
        file_reader.interface.toss(n -| header.len);
        return n;
    }
    const file_limit = @intFromEnum(limit) - reader_buffered.len;
    const out_fd = file.handle;
    const in_fd = file_reader.file.handle;

    if (file_reader.size) |size| {
        if (size - file_reader.pos == 0) {
            if (reader_buffered.len != 0) {
                const n = try fileWriteStreaming(t, file, header, &.{limit.slice(reader_buffered)}, 1);
                file_reader.interface.toss(n -| header.len);
                return n;
            } else {
                return error.EndOfStream;
            }
        }
    }

    if (native_os == .freebsd) sf: {
        // Try using sendfile on FreeBSD.
        if (@atomicLoad(UseSendfile, &t.use_sendfile, .monotonic) == .disabled) break :sf;
        const offset = std.math.cast(std.c.off_t, file_reader.pos) orelse break :sf;
        var hdtr_data: std.c.sf_hdtr = undefined;
        var headers: [2]posix.iovec_const = undefined;
        var headers_i: u8 = 0;
        if (header.len != 0) {
            headers[headers_i] = .{ .base = header.ptr, .len = header.len };
            headers_i += 1;
        }
        if (reader_buffered.len != 0) {
            headers[headers_i] = .{ .base = reader_buffered.ptr, .len = reader_buffered.len };
            headers_i += 1;
        }
        const hdtr: ?*std.c.sf_hdtr = if (headers_i == 0) null else b: {
            hdtr_data = .{
                .headers = &headers,
                .hdr_cnt = headers_i,
                .trailers = null,
                .trl_cnt = 0,
            };
            break :b &hdtr_data;
        };
        var sbytes: std.c.off_t = 0;
        const nbytes: usize = @min(file_limit, std.math.maxInt(usize));
        const flags = 0;

        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(std.c.sendfile(in_fd, out_fd, offset, nbytes, hdtr, &sbytes, flags))) {
                .SUCCESS => {
                    syscall.finish();
                    break;
                },
                .INVAL, .OPNOTSUPP, .NOTSOCK, .NOSYS => {
                    // Give calling code chance to observe before trying
                    // something else.
                    syscall.finish();
                    @atomicStore(UseSendfile, &t.use_sendfile, .disabled, .monotonic);
                    return 0;
                },
                .INTR, .BUSY => {
                    if (sbytes == 0) {
                        try syscall.checkCancel();
                        continue;
                    } else {
                        // Even if we are being canceled, there have been side
                        // effects, so it is better to report those side
                        // effects to the caller.
                        syscall.finish();
                        break;
                    }
                },
                .AGAIN => {
                    syscall.finish();
                    if (sbytes == 0) return error.WouldBlock;
                    break;
                },
                else => |e| {
                    syscall.finish();
                    assert(error.Unexpected == switch (e) {
                        .NOTCONN => return error.BrokenPipe,
                        .IO => return error.InputOutput,
                        .PIPE => return error.BrokenPipe,
                        .NOBUFS => return error.SystemResources,
                        .BADF => |err| errnoBug(err),
                        .FAULT => |err| errnoBug(err),
                        else => |err| posix.unexpectedErrno(err),
                    });
                    // Give calling code chance to observe the error before trying
                    // something else.
                    @atomicStore(UseSendfile, &t.use_sendfile, .disabled, .monotonic);
                    return 0;
                },
            }
        }
        if (sbytes == 0) {
            file_reader.size = file_reader.pos;
            return error.EndOfStream;
        }
        const ubytes: usize = @intCast(sbytes);
        file_reader.interface.toss(ubytes -| header.len);
        return ubytes;
    }

    if (is_darwin) sf: {
        // Try using sendfile on macOS.
        if (@atomicLoad(UseSendfile, &t.use_sendfile, .monotonic) == .disabled) break :sf;
        const offset = std.math.cast(std.c.off_t, file_reader.pos) orelse break :sf;
        var hdtr_data: std.c.sf_hdtr = undefined;
        var headers: [2]posix.iovec_const = undefined;
        var headers_i: u8 = 0;
        if (header.len != 0) {
            headers[headers_i] = .{ .base = header.ptr, .len = header.len };
            headers_i += 1;
        }
        if (reader_buffered.len != 0) {
            headers[headers_i] = .{ .base = reader_buffered.ptr, .len = reader_buffered.len };
            headers_i += 1;
        }
        const hdtr: ?*std.c.sf_hdtr = if (headers_i == 0) null else b: {
            hdtr_data = .{
                .headers = &headers,
                .hdr_cnt = headers_i,
                .trailers = null,
                .trl_cnt = 0,
            };
            break :b &hdtr_data;
        };
        const max_count = std.math.maxInt(i32); // Avoid EINVAL.
        var len: std.c.off_t = @min(file_limit, max_count);
        const flags = 0;
        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(std.c.sendfile(in_fd, out_fd, offset, &len, hdtr, flags))) {
                .SUCCESS => {
                    syscall.finish();
                    break;
                },
                .OPNOTSUPP, .NOTSOCK, .NOSYS => {
                    // Give calling code chance to observe before trying
                    // something else.
                    syscall.finish();
                    @atomicStore(UseSendfile, &t.use_sendfile, .disabled, .monotonic);
                    return 0;
                },
                .INTR => {
                    if (len == 0) {
                        try syscall.checkCancel();
                        continue;
                    } else {
                        // Even if we are being canceled, there have been side
                        // effects, so it is better to report those side
                        // effects to the caller.
                        syscall.finish();
                        break;
                    }
                },
                .AGAIN => {
                    syscall.finish();
                    if (len == 0) return error.WouldBlock;
                    break;
                },
                else => |e| {
                    syscall.finish();
                    assert(error.Unexpected == switch (e) {
                        .NOTCONN => return error.BrokenPipe,
                        .IO => return error.InputOutput,
                        .PIPE => return error.BrokenPipe,
                        .BADF => |err| errnoBug(err),
                        .FAULT => |err| errnoBug(err),
                        .INVAL => |err| errnoBug(err),
                        else => |err| posix.unexpectedErrno(err),
                    });
                    // Give calling code chance to observe the error before trying
                    // something else.
                    @atomicStore(UseSendfile, &t.use_sendfile, .disabled, .monotonic);
                    return 0;
                },
            }
        }
        if (len == 0) {
            file_reader.size = file_reader.pos;
            return error.EndOfStream;
        }
        const u_len: usize = @bitCast(len);
        file_reader.interface.toss(u_len -| header.len);
        return u_len;
    }

    if (native_os == .linux) sf: {
        // Try using sendfile on Linux.
        if (@atomicLoad(UseSendfile, &t.use_sendfile, .monotonic) == .disabled) break :sf;
        // Linux sendfile does not support headers.
        if (header.len != 0 or reader_buffered.len != 0) {
            const n = try fileWriteStreaming(t, file, header, &.{limit.slice(reader_buffered)}, 1);
            file_reader.interface.toss(n -| header.len);
            return n;
        }
        const max_count = 0x7ffff000; // Avoid EINVAL.
        var off: std.os.linux.off_t = undefined;
        const off_ptr: ?*std.os.linux.off_t, const count: usize = switch (file_reader.mode) {
            .positional => o: {
                const size = file_reader.getSize() catch return 0;
                off = std.math.cast(std.os.linux.off_t, file_reader.pos) orelse return error.ReadFailed;
                break :o .{ &off, @min(@intFromEnum(limit), size - file_reader.pos, max_count) };
            },
            .streaming => .{ null, limit.minInt(max_count) },
            .streaming_simple, .positional_simple => break :sf,
            .failure => return error.ReadFailed,
        };
        const syscall: Syscall = try .start();
        const n: usize = while (true) {
            const rc = sendfile_sym(out_fd, in_fd, off_ptr, count);
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    break @intCast(rc);
                },
                .NOSYS, .INVAL => {
                    // Give calling code chance to observe before trying
                    // something else.
                    syscall.finish();
                    @atomicStore(UseSendfile, &t.use_sendfile, .disabled, .monotonic);
                    return 0;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    assert(error.Unexpected == switch (e) {
                        .NOTCONN => return error.BrokenPipe, // `out_fd` is an unconnected socket
                        .AGAIN => return error.WouldBlock,
                        .IO => return error.InputOutput,
                        .PIPE => return error.BrokenPipe,
                        .NOMEM => return error.SystemResources,
                        .NXIO, .SPIPE => {
                            file_reader.mode = file_reader.mode.toStreaming();
                            const pos = file_reader.pos;
                            if (pos != 0) {
                                file_reader.pos = 0;
                                file_reader.seekBy(@intCast(pos)) catch {
                                    file_reader.mode = .failure;
                                    return error.ReadFailed;
                                };
                            }
                            return 0;
                        },
                        .BADF => |err| errnoBug(err), // Always a race condition.
                        .FAULT => |err| errnoBug(err), // Segmentation fault.
                        .OVERFLOW => |err| errnoBug(err), // We avoid passing too large of a `count`.
                        else => |err| posix.unexpectedErrno(err),
                    });
                    // Give calling code chance to observe the error before trying
                    // something else.
                    @atomicStore(UseSendfile, &t.use_sendfile, .disabled, .monotonic);
                    return 0;
                },
            }
        };
        if (n == 0) {
            file_reader.size = file_reader.pos;
            return error.EndOfStream;
        }
        file_reader.pos += n;
        return n;
    }

    if (have_copy_file_range) cfr: {
        if (@atomicLoad(UseCopyFileRange, &t.use_copy_file_range, .monotonic) == .disabled) break :cfr;
        if (header.len != 0 or reader_buffered.len != 0) {
            const n = try fileWriteStreaming(t, file, header, &.{limit.slice(reader_buffered)}, 1);
            file_reader.interface.toss(n -| header.len);
            return n;
        }
        var len: usize = @intFromEnum(limit);
        var off_in: i64 = undefined;
        const off_in_ptr: ?*i64 = switch (file_reader.mode) {
            .positional_simple, .streaming_simple => return error.Unimplemented,
            .positional => p: {
                len = @min(len, std.math.maxInt(usize) - file_reader.pos);
                off_in = @intCast(file_reader.pos);
                break :p &off_in;
            },
            .streaming => null,
            .failure => return error.ReadFailed,
        };
        const n: usize = switch (native_os) {
            .linux => n: {
                const syscall: Syscall = try .start();
                while (true) {
                    const rc = linux_copy_file_range_sys.copy_file_range(in_fd, off_in_ptr, out_fd, null, len, 0);
                    switch (linux_copy_file_range_sys.errno(rc)) {
                        .SUCCESS => {
                            syscall.finish();
                            break :n @intCast(rc);
                        },
                        .INTR => {
                            try syscall.checkCancel();
                            continue;
                        },
                        .OPNOTSUPP, .INVAL, .NOSYS => {
                            // Give calling code chance to observe before trying
                            // something else.
                            syscall.finish();
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                        else => |e| {
                            syscall.finish();
                            assert(error.Unexpected == switch (e) {
                                .FBIG => return error.FileTooBig,
                                .IO => return error.InputOutput,
                                .NOMEM => return error.SystemResources,
                                .NOSPC => return error.NoSpaceLeft,
                                .OVERFLOW => |err| errnoBug(err), // We avoid passing too large a count.
                                .PERM => return error.PermissionDenied,
                                .BUSY => return error.DeviceBusy,
                                .TXTBSY => return error.FileBusy,
                                // copy_file_range can still work but not on
                                // this pair of file descriptors.
                                .XDEV => return error.Unimplemented,
                                .ISDIR => |err| errnoBug(err),
                                .BADF => |err| errnoBug(err),
                                else => |err| posix.unexpectedErrno(err),
                            });
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                    }
                }
            },
            .freebsd => n: {
                const syscall: Syscall = try .start();
                while (true) {
                    const rc = std.c.copy_file_range(in_fd, off_in_ptr, out_fd, null, @intFromEnum(limit), 0);
                    switch (std.c.errno(rc)) {
                        .SUCCESS => {
                            syscall.finish();
                            break :n @intCast(rc);
                        },
                        .INTR => {
                            try syscall.checkCancel();
                            continue;
                        },
                        .OPNOTSUPP, .INVAL, .NOSYS => {
                            // Give calling code chance to observe before trying
                            // something else.
                            syscall.finish();
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                        else => |e| {
                            syscall.finish();
                            assert(error.Unexpected == switch (e) {
                                .FBIG => return error.FileTooBig,
                                .IO => return error.InputOutput,
                                .INTEGRITY => return error.CorruptedData,
                                .NOSPC => return error.NoSpaceLeft,
                                .ISDIR => |err| errnoBug(err),
                                .BADF => |err| errnoBug(err),
                                else => |err| posix.unexpectedErrno(err),
                            });
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                    }
                }
            },
            else => comptime unreachable,
        };
        if (n == 0) {
            file_reader.size = file_reader.pos;
            return error.EndOfStream;
        }
        file_reader.pos += n;
        return n;
    }

    return error.Unimplemented;
}

fn netWriteFile(
    userdata: ?*anyopaque,
    socket_handle: net.Socket.Handle,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
) net.Stream.Writer.WriteFileError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    _ = socket_handle;
    _ = header;
    _ = file_reader;
    _ = limit;
    @panic("TODO implement netWriteFile");
}

fn fileWriteFilePositional(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
    offset: u64,
) File.WriteFilePositionalError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const reader_buffered = file_reader.interface.buffered();
    if (reader_buffered.len >= @intFromEnum(limit)) {
        const n = try fileWritePositional(t, file, header, &.{limit.slice(reader_buffered)}, 1, offset);
        file_reader.interface.toss(n -| header.len);
        return n;
    }
    const out_fd = file.handle;
    const in_fd = file_reader.file.handle;

    if (file_reader.size) |size| {
        if (size - file_reader.pos == 0) {
            if (reader_buffered.len != 0) {
                const n = try fileWritePositional(t, file, header, &.{limit.slice(reader_buffered)}, 1, offset);
                file_reader.interface.toss(n -| header.len);
                return n;
            } else {
                return error.EndOfStream;
            }
        }
    }

    if (have_copy_file_range) cfr: {
        if (@atomicLoad(UseCopyFileRange, &t.use_copy_file_range, .monotonic) == .disabled) break :cfr;
        if (header.len != 0 or reader_buffered.len != 0) {
            const n = try fileWritePositional(t, file, header, &.{limit.slice(reader_buffered)}, 1, offset);
            file_reader.interface.toss(n -| header.len);
            return n;
        }
        var len: usize = @min(@intFromEnum(limit), std.math.maxInt(usize) - offset);
        var off_in: i64 = undefined;
        const off_in_ptr: ?*i64 = switch (file_reader.mode) {
            .positional_simple, .streaming_simple => return error.Unimplemented,
            .positional => p: {
                len = @min(len, std.math.maxInt(usize) - file_reader.pos);
                off_in = @intCast(file_reader.pos);
                break :p &off_in;
            },
            .streaming => null,
            .failure => return error.ReadFailed,
        };
        var off_out: i64 = @intCast(offset);
        const n: usize = switch (native_os) {
            .linux => n: {
                const syscall: Syscall = try .start();
                while (true) {
                    const rc = linux_copy_file_range_sys.copy_file_range(in_fd, off_in_ptr, out_fd, &off_out, len, 0);
                    switch (linux_copy_file_range_sys.errno(rc)) {
                        .SUCCESS => {
                            syscall.finish();
                            break :n @intCast(rc);
                        },
                        .INTR => {
                            try syscall.checkCancel();
                            continue;
                        },
                        .OPNOTSUPP, .INVAL, .NOSYS => {
                            // Give calling code chance to observe before trying
                            // something else.
                            syscall.finish();
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                        else => |e| {
                            syscall.finish();
                            assert(error.Unexpected == switch (e) {
                                .FBIG => return error.FileTooBig,
                                .IO => return error.InputOutput,
                                .NOMEM => return error.SystemResources,
                                .NOSPC => return error.NoSpaceLeft,
                                .OVERFLOW => |err| errnoBug(err), // We avoid passing too large a count.
                                .NXIO => return error.Unseekable,
                                .SPIPE => return error.Unseekable,
                                .PERM => return error.PermissionDenied,
                                .TXTBSY => return error.FileBusy,
                                // copy_file_range can still work but not on
                                // this pair of file descriptors.
                                .XDEV => return error.Unimplemented,
                                .ISDIR => |err| errnoBug(err),
                                .BADF => |err| errnoBug(err),
                                else => |err| posix.unexpectedErrno(err),
                            });
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                    }
                }
            },
            .freebsd => n: {
                const syscall: Syscall = try .start();
                while (true) {
                    const rc = std.c.copy_file_range(in_fd, off_in_ptr, out_fd, &off_out, @intFromEnum(limit), 0);
                    switch (std.c.errno(rc)) {
                        .SUCCESS => {
                            syscall.finish();
                            break :n @intCast(rc);
                        },
                        .INTR => {
                            try syscall.checkCancel();
                            continue;
                        },
                        .OPNOTSUPP, .INVAL, .NOSYS => {
                            // Give calling code chance to observe before trying
                            // something else.
                            syscall.finish();
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                        else => |e| {
                            syscall.finish();
                            assert(error.Unexpected == switch (e) {
                                .FBIG => return error.FileTooBig,
                                .IO => return error.InputOutput,
                                .INTEGRITY => return error.CorruptedData,
                                .NOSPC => return error.NoSpaceLeft,
                                .OVERFLOW => return error.Unseekable,
                                .NXIO => return error.Unseekable,
                                .SPIPE => return error.Unseekable,
                                .ISDIR => |err| errnoBug(err),
                                .BADF => |err| errnoBug(err),
                                else => |err| posix.unexpectedErrno(err),
                            });
                            @atomicStore(UseCopyFileRange, &t.use_copy_file_range, .disabled, .monotonic);
                            return 0;
                        },
                    }
                }
            },
            else => comptime unreachable,
        };
        if (n == 0) {
            file_reader.size = file_reader.pos;
            return error.EndOfStream;
        }
        file_reader.pos += n;
        return n;
    }

    if (is_darwin) fcf: {
        if (@atomicLoad(UseFcopyfile, &t.use_fcopyfile, .monotonic) == .disabled) break :fcf;
        if (file_reader.pos != 0) break :fcf;
        if (offset != 0) break :fcf;
        if (limit != .unlimited) break :fcf;
        const size = file_reader.getSize() catch break :fcf;
        if (header.len != 0 or reader_buffered.len != 0) {
            const n = try fileWritePositional(t, file, header, &.{limit.slice(reader_buffered)}, 1, offset);
            file_reader.interface.toss(n -| header.len);
            return n;
        }
        const syscall: Syscall = try .start();
        while (true) {
            const rc = std.c.fcopyfile(in_fd, out_fd, null, .{ .DATA = true });
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    break;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                .OPNOTSUPP => {
                    // Give calling code chance to observe before trying
                    // something else.
                    syscall.finish();
                    @atomicStore(UseFcopyfile, &t.use_fcopyfile, .disabled, .monotonic);
                    return 0;
                },
                else => |e| {
                    syscall.finish();
                    assert(error.Unexpected == switch (e) {
                        .NOMEM => return error.SystemResources,
                        .INVAL => |err| errnoBug(err),
                        else => |err| posix.unexpectedErrno(err),
                    });
                    return 0;
                },
            }
        }
        file_reader.pos = size;
        return size;
    }

    return error.Unimplemented;
}

fn nowPosix(clock: Io.Clock) Io.Timestamp {
    const clock_id: posix.clockid_t = clockToPosix(clock);
    var timespec: posix.timespec = undefined;
    switch (posix.errno(posix.system.clock_gettime(clock_id, &timespec))) {
        .SUCCESS => return timestampFromPosix(&timespec),
        else => return .zero,
    }
}

fn now(userdata: ?*anyopaque, clock: Io.Clock) Io.Timestamp {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return switch (native_os) {
        .windows => nowWindows(clock),
        .wasi => nowWasi(clock),
        else => nowPosix(clock),
    };
}

fn clockResolution(userdata: ?*anyopaque, clock: Io.Clock) Io.Clock.ResolutionError!Io.Duration {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return switch (native_os) {
        .windows => switch (clock) {
            .awake, .boot, .real => {
                // We don't need to cache QPF as it's internally just a memory read to KUSER_SHARED_DATA
                // (a read-only page of info updated and mapped by the kernel to all processes):
                // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/ns-ntddk-kuser_shared_data
                // https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/ntexapi_x/kuser_shared_data/index.htm
                var qpf: windows.LARGE_INTEGER = undefined;
                if (!windows.ntdll.RtlQueryPerformanceFrequency(&qpf).toBool()) {
                    recoverableOsBugDetected();
                    return .zero;
                }
                // 10Mhz (1 qpc tick every 100ns) is a common enough QPF value that we can optimize on it.
                // https://github.com/microsoft/STL/blob/785143a0c73f030238ef618890fd4d6ae2b3a3a0/stl/inc/chrono#L694-L701
                const common_qpf = 10_000_000;
                if (qpf == common_qpf) return .fromNanoseconds(std.time.ns_per_s / common_qpf);

                // Convert to ns using fixed point.
                const scale = @as(u64, std.time.ns_per_s << 32) / @as(u32, @intCast(qpf));
                const result = scale >> 32;
                return .fromNanoseconds(result);
            },
            .cpu_process, .cpu_thread => return error.ClockUnavailable,
        },
        .wasi => {
            if (builtin.link_libc) return clockResolutionPosix(clock);
            var ns: std.os.wasi.timestamp_t = undefined;
            return switch (std.os.wasi.clock_res_get(clockToWasi(clock), &ns)) {
                .SUCCESS => .fromNanoseconds(ns),
                .INVAL => return error.ClockUnavailable,
                else => |err| return posix.unexpectedErrno(err),
            };
        },
        else => return clockResolutionPosix(clock),
    };
}

fn clockResolutionPosix(clock: Io.Clock) Io.Clock.ResolutionError!Io.Duration {
    const clock_id: posix.clockid_t = clockToPosix(clock);
    var timespec: posix.timespec = undefined;
    return switch (posix.errno(posix.system.clock_getres(clock_id, &timespec))) {
        .SUCCESS => .fromNanoseconds(nanosecondsFromPosix(&timespec)),
        .INVAL => return error.ClockUnavailable,
        else => |err| return posix.unexpectedErrno(err),
    };
}

fn nowWindows(clock: Io.Clock) Io.Timestamp {
    switch (clock) {
        .real => {
            // RtlGetSystemTimePrecise() has a granularity of 100 nanoseconds
            // and uses the NTFS/Windows epoch, which is 1601-01-01.
            const epoch_ns = std.time.epoch.windows * std.time.ns_per_s;
            return .{ .nanoseconds = @as(i96, windows.ntdll.RtlGetSystemTimePrecise()) * 100 + epoch_ns };
        },
        .awake, .boot => {
            // We don't need to cache QPF as it's internally just a memory read to KUSER_SHARED_DATA
            // (a read-only page of info updated and mapped by the kernel to all processes):
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/ns-ntddk-kuser_shared_data
            // https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/ntexapi_x/kuser_shared_data/index.htm
            const qpf: u64 = qpf: {
                var qpf: windows.LARGE_INTEGER = undefined;
                assert(windows.ntdll.RtlQueryPerformanceFrequency(&qpf).toBool());
                break :qpf @bitCast(qpf);
            };

            // QPC on windows doesn't fail on >= XP/2000 and includes time suspended.
            const qpc: u64 = qpc: {
                var qpc: windows.LARGE_INTEGER = undefined;
                assert(windows.ntdll.RtlQueryPerformanceCounter(&qpc).toBool());
                break :qpc @bitCast(qpc);
            };

            // 10Mhz (1 qpc tick every 100ns) is a common enough QPF value that we can optimize on it.
            // https://github.com/microsoft/STL/blob/785143a0c73f030238ef618890fd4d6ae2b3a3a0/stl/inc/chrono#L694-L701
            const common_qpf = 10_000_000;
            if (qpf == common_qpf) return .{ .nanoseconds = qpc * (std.time.ns_per_s / common_qpf) };

            // Convert to ns using fixed point.
            const scale = @as(u64, std.time.ns_per_s << 32) / @as(u32, @intCast(qpf));
            const result = (@as(u96, qpc) * scale) >> 32;
            return .{ .nanoseconds = @intCast(result) };
        },
        .cpu_process => {
            const handle = windows.GetCurrentProcess();
            var times: windows.KERNEL_USER_TIMES = undefined;

            // https://github.com/reactos/reactos/blob/master/ntoskrnl/ps/query.c#L442-L485
            if (windows.ntdll.NtQueryInformationProcess(
                handle,
                .Times,
                &times,
                @sizeOf(windows.KERNEL_USER_TIMES),
                null,
            ) != .SUCCESS) return .zero;

            const sum = @as(i96, times.UserTime) + @as(i96, times.KernelTime);
            return .{ .nanoseconds = sum * 100 };
        },
        .cpu_thread => {
            const handle = windows.GetCurrentThread();
            var times: windows.KERNEL_USER_TIMES = undefined;

            // https://github.com/reactos/reactos/blob/master/ntoskrnl/ps/query.c#L2971-L3019
            if (windows.ntdll.NtQueryInformationThread(
                handle,
                .Times,
                &times,
                @sizeOf(windows.KERNEL_USER_TIMES),
                null,
            ) != .SUCCESS) return .zero;

            const sum = @as(i96, times.UserTime) + @as(i96, times.KernelTime);
            return .{ .nanoseconds = sum * 100 };
        },
    }
}

fn nowWasi(clock: Io.Clock) Io.Timestamp {
    var ns: std.os.wasi.timestamp_t = undefined;
    const err = std.os.wasi.clock_time_get(clockToWasi(clock), 1, &ns);
    if (err != .SUCCESS) return .zero;
    return .fromNanoseconds(ns);
}

fn sleep(userdata: ?*anyopaque, timeout: Io.Timeout) Io.Cancelable!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (timeout == .none) return;
    if (use_parking_sleep) return parking_sleep.sleep(timeout);
    if (native_os == .wasi) return sleepWasi(t, timeout);
    if (@TypeOf(posix.system.clock_nanosleep) != void) return sleepPosix(timeout);
    return sleepNanosleep(t, timeout);
}

fn sleepPosix(timeout: Io.Timeout) Io.Cancelable!void {
    const clock_id: posix.clockid_t = clockToPosix(switch (timeout) {
        .none => .awake,
        .duration => |d| d.clock,
        .deadline => |d| d.clock,
    });
    const deadline_nanoseconds: i96 = switch (timeout) {
        .none => std.math.maxInt(i96),
        .duration => |duration| duration.raw.nanoseconds,
        .deadline => |deadline| deadline.raw.nanoseconds,
    };
    var timespec: posix.timespec = timestampToPosix(deadline_nanoseconds);
    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.clock_nanosleep(clock_id, .{ .ABSTIME = switch (timeout) {
            .none, .duration => false,
            .deadline => true,
        } }, &timespec, &timespec);
        // POSIX-standard libc clock_nanosleep() returns *positive* errno values directly
        switch (if (builtin.link_libc) @as(posix.E, @enumFromInt(rc)) else posix.errno(rc)) {
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            // Handles SUCCESS as well as clock not available and unexpected
            // errors. The user had a chance to check clock resolution before
            // getting here, which would have reported 0, making this a legal
            // amount of time to sleep.
            else => {
                syscall.finish();
                return;
            },
        }
    }
}

fn sleepWasi(t: *Threaded, timeout: Io.Timeout) Io.Cancelable!void {
    const t_io = io(t);
    const w = std.os.wasi;

    const clock: w.subscription_clock_t = if (timeout.toDurationFromNow(t_io)) |d| .{
        .id = clockToWasi(d.clock),
        .timeout = std.math.lossyCast(u64, d.raw.nanoseconds),
        .precision = 0,
        .flags = 0,
    } else .{
        .id = .MONOTONIC,
        .timeout = std.math.maxInt(u64),
        .precision = 0,
        .flags = 0,
    };
    const in: w.subscription_t = .{
        .userdata = 0,
        .u = .{
            .tag = .CLOCK,
            .u = .{ .clock = clock },
        },
    };
    var event: w.event_t = undefined;
    var nevents: usize = undefined;
    const syscall: Syscall = try .start();
    _ = w.poll_oneoff(&in, &event, 1, &nevents);
    syscall.finish();
}

fn sleepNanosleep(t: *Threaded, timeout: Io.Timeout) Io.Cancelable!void {
    const t_io = io(t);
    const sec_type = @typeInfo(posix.timespec).@"struct".fields[0].type;
    const nsec_type = @typeInfo(posix.timespec).@"struct".fields[1].type;

    var timespec: posix.timespec = t: {
        const d = timeout.toDurationFromNow(t_io) orelse break :t .{
            .sec = std.math.maxInt(sec_type),
            .nsec = std.math.maxInt(nsec_type),
        };
        break :t timestampToPosix(d.raw.toNanoseconds());
    };
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.nanosleep(&timespec, &timespec))) {
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            // This prong handles success as well as unexpected errors.
            else => return syscall.finish(),
        }
    }
}

fn netListenIpPosix(
    userdata: ?*anyopaque,
    address: *const IpAddress,
    options: IpAddress.ListenOptions,
) IpAddress.ListenError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const family = posixAddressFamily(address);
    const socket_fd = try openSocketPosix(family, .{ .mode = options.mode, .protocol = options.protocol });
    errdefer closeFd(socket_fd);

    if (options.reuse_address) {
        try setSocketOptionPosix(socket_fd, posix.SOL.SOCKET, posix.SO.REUSEADDR, 1);
        if (@hasDecl(posix.SO, "REUSEPORT"))
            try setSocketOptionPosix(socket_fd, posix.SOL.SOCKET, posix.SO.REUSEPORT, 1);
    }

    var storage: PosixAddress = undefined;
    var addr_len = addressToPosix(address, &storage);
    try posixBind(socket_fd, &storage.any, addr_len);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.listen(socket_fd, options.kernel_backlog))) {
            .SUCCESS => {
                syscall.finish();
                break;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ADDRINUSE => return syscall.fail(error.AddressInUse),
            .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
            else => |err| return syscall.unexpectedErrno(err),
        }
    }

    try posixGetSockName(socket_fd, &storage.any, &addr_len);
    return .{ .handle = socket_fd, .address = addressFromPosix(&storage) };
}

fn netListenIpWindows(
    userdata: ?*anyopaque,
    address: *const IpAddress,
    options: IpAddress.ListenOptions,
) IpAddress.ListenError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const family = posixAddressFamily(address);
    const socket_handle = try openSocketAfd(family, .{ .mode = options.mode, .protocol = options.protocol });
    errdefer windows.CloseHandle(socket_handle);
    if (options.reuse_address) try setSocketOptionAfd(socket_handle, ws2_32.SOL.SOCKET, ws2_32.SO.REUSEADDR, true);
    const bound_address = try bindSocketIpAfd(socket_handle, address, .Passive);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.START_LISTEN,
        .in = @ptrCast(&windows.AFD.LISTEN_INFO{
            .UseSAN = .FALSE,
            .MaximumConnectionQueue = options.kernel_backlog,
            .UseDelayedAcceptance = .FALSE,
        }),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
    return .{ .handle = socket_handle, .address = bound_address };
}

fn netListenUnixPosix(
    userdata: ?*anyopaque,
    address: *const net.UnixAddress,
    options: net.UnixAddress.ListenOptions,
) net.UnixAddress.ListenError!net.Socket.Handle {
    if (!net.has_unix_sockets) return error.AddressFamilyUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const socket_fd = openSocketPosix(posix.AF.UNIX, .{ .mode = .stream }) catch |err| switch (err) {
        error.ProtocolUnsupportedBySystem => return error.AddressFamilyUnsupported,
        error.ProtocolUnsupportedByAddressFamily => return error.AddressFamilyUnsupported,
        error.SocketModeUnsupported => return error.AddressFamilyUnsupported,
        error.OptionUnsupported => return error.Unexpected,
        else => |e| return e,
    };
    errdefer closeFd(socket_fd);

    var storage: UnixAddress = undefined;
    const addr_len = addressUnixToPosix(address, &storage);
    try posixBindUnix(socket_fd, &storage.any, addr_len);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.listen(socket_fd, options.kernel_backlog))) {
            .SUCCESS => {
                syscall.finish();
                break;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ADDRINUSE => return syscall.fail(error.AddressInUse),
            .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
            else => |err| return syscall.unexpectedErrno(err),
        }
    }

    return socket_fd;
}

fn netListenUnixWindows(
    userdata: ?*anyopaque,
    address: *const net.UnixAddress,
    options: net.UnixAddress.ListenOptions,
) net.UnixAddress.ListenError!net.Socket.Handle {
    if (!net.has_unix_sockets) return error.AddressFamilyUnsupported;
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const is_abstract = address.isAbstract();
    const wps = if (!is_abstract) sliceToPrefixedFileW(null, address.path, .{
        .allow_relative = false,
    }) catch |err| switch (err) {
        error.NameTooLong, error.BadPathName => return error.AddressUnavailable,
        else => |e| return e,
    } else undefined;
    const socket_handle = openSocketAfd(ws2_32.AF.UNIX, .{ .mode = .stream }) catch |err| switch (err) {
        error.ProtocolUnsupportedByAddressFamily => return error.AddressFamilyUnsupported,
        else => |e| return e,
    };
    errdefer windows.CloseHandle(socket_handle);
    if (!is_abstract) try socketOptionAfd(socket_handle, .special, 0, ws2_32.SO.UNIX_PATH, @constCast(
        @as([]const u8, @ptrCast(&windows.AFD.SOCKOPT_INFO.UNIX_PATH{
            .Path = wps.data,
        }))[0 .. @offsetOf(windows.AFD.SOCKOPT_INFO.UNIX_PATH, "Path") + @sizeOf(windows.WCHAR) * wps.len],
    ));
    try bindSocketUnixAfd(socket_handle, address);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.START_LISTEN,
        .in = @ptrCast(&windows.AFD.LISTEN_INFO{
            .UseSAN = .FALSE,
            .MaximumConnectionQueue = options.kernel_backlog,
            .UseDelayedAcceptance = .FALSE,
        }),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
    return socket_handle;
}

fn posixBindUnix(
    fd: posix.socket_t,
    addr: *const posix.sockaddr,
    addr_len: posix.socklen_t,
) !void {
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.bind(fd, addr, addr_len))) {
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
                    .ADDRINUSE => return error.AddressInUse,
                    .AFNOSUPPORT => return error.AddressFamilyUnsupported,
                    .ADDRNOTAVAIL => return error.AddressUnavailable,
                    .NOMEM => return error.SystemResources,

                    .LOOP => return error.SymLinkLoop,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .PERM => return error.PermissionDenied,

                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .INVAL => |err| return errnoBug(err), // invalid parameters
                    .NOTSOCK => |err| return errnoBug(err), // invalid `sockfd`
                    .FAULT => |err| return errnoBug(err), // invalid `addr` pointer
                    .NAMETOOLONG => |err| return errnoBug(err),
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn posixBind(
    socket_fd: posix.socket_t,
    addr: *const posix.sockaddr,
    addr_len: posix.socklen_t,
) !void {
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.bind(socket_fd, addr, addr_len))) {
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
                    .ADDRINUSE => return error.AddressInUse,
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .INVAL => |err| return errnoBug(err), // invalid parameters
                    .NOTSOCK => |err| return errnoBug(err), // invalid `sockfd`
                    .AFNOSUPPORT => return error.AddressFamilyUnsupported,
                    .ADDRNOTAVAIL => return error.AddressUnavailable,
                    .FAULT => |err| return errnoBug(err), // invalid `addr` pointer
                    .NOMEM => return error.SystemResources,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn posixConnect(
    socket_fd: posix.socket_t,
    addr: *const posix.sockaddr,
    addr_len: posix.socklen_t,
) !void {
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.connect(socket_fd, addr, addr_len))) {
        .SUCCESS => {
            syscall.finish();
            return;
        },
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .ADDRNOTAVAIL => return syscall.fail(error.AddressUnavailable),
        .AFNOSUPPORT => return syscall.fail(error.AddressFamilyUnsupported),
        .AGAIN, .INPROGRESS => return syscall.fail(error.WouldBlock),
        .ALREADY => return syscall.fail(error.ConnectionPending),
        .CONNREFUSED => return syscall.fail(error.ConnectionRefused),
        .CONNRESET => return syscall.fail(error.ConnectionResetByPeer),
        .HOSTUNREACH => return syscall.fail(error.HostUnreachable),
        .NETUNREACH => return syscall.fail(error.NetworkUnreachable),
        .TIMEDOUT => return syscall.fail(error.Timeout),
        .ACCES => return syscall.fail(error.AccessDenied),
        .NETDOWN => return syscall.fail(error.NetworkDown),
        .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
        .CONNABORTED => |err| return syscall.errnoBug(err),
        .FAULT => |err| return syscall.errnoBug(err),
        .ISCONN => |err| return syscall.errnoBug(err),
        .NOENT => |err| return syscall.errnoBug(err),
        .NOTSOCK => |err| return syscall.errnoBug(err),
        .PERM => |err| return syscall.errnoBug(err),
        .PROTOTYPE => |err| return syscall.errnoBug(err),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn posixConnectUnix(
    fd: posix.socket_t,
    addr: *const posix.sockaddr,
    addr_len: posix.socklen_t,
) !void {
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.connect(fd, addr, addr_len))) {
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
                    .AFNOSUPPORT => return error.AddressFamilyUnsupported,
                    .AGAIN => return error.WouldBlock,
                    .INPROGRESS => return error.WouldBlock,
                    .ACCES => return error.AccessDenied,

                    .LOOP => return error.SymLinkLoop,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .PERM => return error.PermissionDenied,

                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .CONNABORTED => |err| return errnoBug(err),
                    .FAULT => |err| return errnoBug(err),
                    .ISCONN => |err| return errnoBug(err),
                    .NOTSOCK => |err| return errnoBug(err),
                    .PROTOTYPE => |err| return errnoBug(err),
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn posixGetSockName(
    socket_fd: posix.fd_t,
    addr: *posix.sockaddr,
    addr_len: *posix.socklen_t,
) !void {
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.getsockname(socket_fd, addr, addr_len))) {
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
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err), // invalid parameters
                    .NOTSOCK => |err| return errnoBug(err), // always a race condition
                    .NOBUFS => return error.SystemResources,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn setSocketOptionPosix(fd: posix.fd_t, level: i32, opt_name: u32, option: u32) !void {
    const o: []const u8 = @ptrCast(&option);
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.setsockopt(fd, level, opt_name, o.ptr, @intCast(o.len)))) {
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
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOTSOCK => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .FAULT => |err| return errnoBug(err),
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn setSocketOptionAfd(socket: net.Socket.Handle, level: i32, opt_name: u32, opt_val: anytype) !void {
    try socketOptionAfd(socket, .set, level, opt_name, @ptrCast(@constCast(&opt_val)));
}

fn socketOptionAfd(socket: net.Socket.Handle, mode: windows.AFD.SOCKOPT_INFO.Mode, level: i32, opt_name: u32, opt_val: []u8) !void {
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.SOCKOPT,
        .in = @ptrCast(&windows.AFD.SOCKOPT_INFO{
            .mode = mode,
            .level = level,
            .optname = opt_name,
            .optval = opt_val.ptr,
            .optlen = opt_val.len,
        }),
    })).u.Status) {
        .SUCCESS => return,
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn netConnectIpPosix(
    userdata: ?*anyopaque,
    address: *const IpAddress,
    options: IpAddress.ConnectOptions,
) IpAddress.ConnectError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    if (options.timeout != .none) @panic("TODO implement netConnectIpPosix with timeout");
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const family = posixAddressFamily(address);
    const socket_fd = try openSocketPosix(family, .{ .mode = options.mode, .protocol = options.protocol });
    errdefer closeFd(socket_fd);
    var storage: PosixAddress = undefined;
    var addr_len = addressToPosix(address, &storage);
    try posixConnect(socket_fd, &storage.any, addr_len);
    try posixGetSockName(socket_fd, &storage.any, &addr_len);
    return .{ .handle = socket_fd, .address = addressFromPosix(&storage) };
}

fn netConnectIpWindows(
    userdata: ?*anyopaque,
    address: *const IpAddress,
    options: IpAddress.ConnectOptions,
) IpAddress.ConnectError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    if (options.timeout != .none) @panic("TODO implement netConnectIpWindows with timeout");
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const family = posixAddressFamily(address);
    const socket_handle = try openSocketAfd(family, .{ .mode = options.mode, .protocol = options.protocol });
    errdefer windows.CloseHandle(socket_handle);
    try setSocketOptionAfd(socket_handle, ws2_32.SOL.SOCKET, ws2_32.SO.REUSE_UNICASTPORT, true);
    const bound_address = bindSocketIpAfd(socket_handle, &switch (address.*) {
        .ip4 => .{ .ip4 = .unspecified(0) },
        .ip6 => .{ .ip6 = .unspecified(0) },
    }, .Active) catch |err| switch (err) {
        error.AddressInUse => return error.Unexpected,
        else => |e| return e,
    };
    const Storage = extern struct { Reserved0: [3]usize = @splat(0), Address: PosixAddress };
    var storage: Storage = .{ .Address = undefined };
    const addr_len = addressToPosix(address, &storage.Address);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.CONNECT,
        .in = @as([]const u8, @ptrCast(&storage))[0 .. @offsetOf(Storage, "Address") + addr_len],
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
    return .{ .handle = socket_handle, .address = bound_address };
}

fn netConnectUnixPosix(
    userdata: ?*anyopaque,
    address: *const net.UnixAddress,
) net.UnixAddress.ConnectError!net.Socket.Handle {
    if (!net.has_unix_sockets) return error.AddressFamilyUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const socket_fd = openSocketPosix(posix.AF.UNIX, .{ .mode = .stream }) catch |err| switch (err) {
        error.ProtocolUnsupportedByAddressFamily => return error.AddressFamilyUnsupported,
        error.OptionUnsupported => return error.Unexpected,
        else => |e| return e,
    };
    errdefer closeFd(socket_fd);
    var storage: UnixAddress = undefined;
    const addr_len = addressUnixToPosix(address, &storage);
    try posixConnectUnix(socket_fd, &storage.any, addr_len);
    return socket_fd;
}

fn netConnectUnixWindows(
    userdata: ?*anyopaque,
    address: *const net.UnixAddress,
) net.UnixAddress.ConnectError!net.Socket.Handle {
    if (!net.has_unix_sockets) return error.AddressFamilyUnsupported;
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const is_abstract = address.isAbstract();
    const wps = if (!is_abstract) sliceToPrefixedFileW(null, address.path, .{
        .allow_relative = false,
    }) catch |err| switch (err) {
        error.NameTooLong, error.BadPathName => return error.FileNotFound,
        else => |e| return e,
    } else undefined;
    const socket_handle = openSocketAfd(ws2_32.AF.UNIX, .{ .mode = .stream }) catch |err| switch (err) {
        error.ProtocolUnsupportedByAddressFamily => return error.AddressFamilyUnsupported,
        else => |e| return e,
    };
    errdefer windows.CloseHandle(socket_handle);
    if (!is_abstract) try socketOptionAfd(socket_handle, .special, 0, ws2_32.SO.UNIX_PATH, @constCast(
        @as([]const u8, @ptrCast(&windows.AFD.SOCKOPT_INFO.UNIX_PATH{
            .Path = wps.data,
        }))[0 .. @offsetOf(windows.AFD.SOCKOPT_INFO.UNIX_PATH, "Path") + @sizeOf(windows.WCHAR) * wps.len],
    ));
    bindSocketUnixAfd(socket_handle, &(net.UnixAddress.init("") catch |err| switch (err) {
        error.NameTooLong => unreachable,
    })) catch |err| switch (err) {
        error.AddressInUse => return error.Unexpected,
        else => |e| return e,
    };
    const Storage = extern struct { Reserved0: [3]usize = @splat(0), Address: UnixAddress };
    var storage: Storage = .{ .Address = undefined };
    const addr_len = addressUnixToPosix(address, &storage.Address);
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = socket_handle, .flags = .{ .nonblocking = true } },
        .code = windows.IOCTL.AFD.CONNECT,
        .in = @as([]const u8, @ptrCast(&storage))[0 .. @offsetOf(Storage, "Address") + addr_len],
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        else => |status| return windows.unexpectedStatus(status),
    }
    return socket_handle;
}

fn netBindIpPosix(
    userdata: ?*anyopaque,
    address: *const IpAddress,
    options: IpAddress.BindOptions,
) IpAddress.BindError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const family = posixAddressFamily(address);
    const socket_fd = try openSocketPosix(family, options);
    errdefer closeFd(socket_fd);
    var storage: PosixAddress = undefined;
    var addr_len = addressToPosix(address, &storage);
    try posixBind(socket_fd, &storage.any, addr_len);
    if (options.allow_broadcast) try setSocketOptionPosix(socket_fd, std.posix.SOL.SOCKET, std.posix.SO.BROADCAST, 1);
    try posixGetSockName(socket_fd, &storage.any, &addr_len);
    return .{ .handle = socket_fd, .address = addressFromPosix(&storage) };
}

fn netBindIpWindows(
    userdata: ?*anyopaque,
    address: *const IpAddress,
    options: IpAddress.BindOptions,
) IpAddress.BindError!net.Socket {
    if (!have_networking) return error.NetworkDown;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const family = posixAddressFamily(address);
    const socket_handle = try openSocketAfd(family, options);
    errdefer windows.CloseHandle(socket_handle);
    const bound_address = try bindSocketIpAfd(socket_handle, address, .Active);
    if (options.allow_broadcast) try setSocketOptionAfd(socket_handle, ws2_32.SOL.SOCKET, ws2_32.SO.BROADCAST, true);
    return .{ .handle = socket_handle, .address = bound_address };
}

fn openSocketPosix(
    family: posix.sa_family_t,
    options: IpAddress.BindOptions,
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
}!posix.socket_t {
    const mode, const protocol = try posixSocketModeProtocol(family, options.mode, options.protocol);
    const flags: u32 = mode | if (socket_flags_unsupported) 0 else posix.SOCK.CLOEXEC;
    const syscall: Syscall = try .start();
    const socket_fd = while (true) {
        const rc = posix.system.socket(family, flags, protocol);
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                const fd: posix.fd_t = @intCast(rc);
                errdefer closeFd(fd);
                if (socket_flags_unsupported) try setCloexec(fd);
                break fd;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .AFNOSUPPORT => return syscall.fail(error.AddressFamilyUnsupported),
            .INVAL => return syscall.fail(error.ProtocolUnsupportedBySystem),
 
```
