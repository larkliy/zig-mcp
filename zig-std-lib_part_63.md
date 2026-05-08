```
e, new_sub_path, .{});
    const new_path_w = new_path_w_buf.span();

    const src_fd = src_fd: {
        if (OpenFile(old_path_w, .{
            .dir = old_dir.handle,
            .access_mask = .{
                .GENERIC = .{ .WRITE = true },
                .STANDARD = .{
                    .RIGHTS = .{ .DELETE = true },
                    .SYNCHRONIZE = true,
                },
            },
            .creation = .OPEN,
            .filter = .any, // This function is supposed to rename both files and directories.
            .follow_symlinks = false,
        })) |handle| {
            break :src_fd handle;
        } else |err| switch (err) {
            error.WouldBlock => unreachable, // Not possible without `.share_access_nonblocking = true`.
            else => |e| return e,
        }
    };
    defer w.CloseHandle(src_fd);

    var rc: w.NTSTATUS = undefined;
    // FileRenameInformationEx has varying levels of support:
    // - FILE_RENAME_INFORMATION_EX requires >= win10_rs1
    //   (INVALID_INFO_CLASS is returned if not supported)
    // - Requires the NTFS filesystem
    //   (on filesystems like FAT32, INVALID_PARAMETER is returned)
    // - FILE_RENAME_POSIX_SEMANTICS requires >= win10_rs1
    // - FILE_RENAME_IGNORE_READONLY_ATTRIBUTE requires >= win10_rs5
    //   (NOT_SUPPORTED is returned if a flag is unsupported)
    //
    // The strategy here is just to try using FileRenameInformationEx and fall back to
    // FileRenameInformation if the return value lets us know that some aspect of it is not supported.
    const need_fallback = need_fallback: {
        var rename_info: w.FILE.RENAME_INFORMATION = .init(.{
            .Flags = .{
                .REPLACE_IF_EXISTS = replace_if_exists,
                .POSIX_SEMANTICS = true,
                .IGNORE_READONLY_ATTRIBUTE = true,
            },
            .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(new_path_w)) null else new_dir.handle,
            .FileName = new_path_w,
        });
        var io_status_block: w.IO_STATUS_BLOCK = undefined;
        const rename_info_buf = rename_info.toBuffer();
        rc = w.ntdll.NtSetInformationFile(
            src_fd,
            &io_status_block,
            rename_info_buf.ptr,
            @intCast(rename_info_buf.len),
            .RenameEx,
        );
        switch (rc) {
            .SUCCESS => return,
            // The filesystem does not support FileDispositionInformationEx
            .INVALID_PARAMETER,
            // The operating system does not support FileDispositionInformationEx
            .INVALID_INFO_CLASS,
            // The operating system does not support one of the flags
            .NOT_SUPPORTED,
            => break :need_fallback true,
            // For all other statuses, fall down to the switch below to handle them.
            else => break :need_fallback false,
        }
    };

    if (need_fallback) {
        var rename_info: w.FILE.RENAME_INFORMATION = .init(.{
            .Flags = .{ .REPLACE_IF_EXISTS = replace_if_exists },
            .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(new_path_w)) null else new_dir.handle,
            .FileName = new_path_w,
        });
        var io_status_block: w.IO_STATUS_BLOCK = undefined;
        const rename_info_buf = rename_info.toBuffer();
        rc = w.ntdll.NtSetInformationFile(
            src_fd,
            &io_status_block,
            rename_info_buf.ptr,
            @intCast(rename_info_buf.len),
            .Rename,
        );
    }

    switch (rc) {
        .SUCCESS => {},
        .INVALID_HANDLE => |err| return w.statusBug(err),
        .INVALID_PARAMETER => |err| return w.statusBug(err),
        .OBJECT_PATH_SYNTAX_BAD => |err| return w.statusBug(err),
        .ACCESS_DENIED => return error.AccessDenied,
        .OBJECT_NAME_NOT_FOUND => return error.FileNotFound,
        .OBJECT_PATH_NOT_FOUND => return error.FileNotFound,
        .NOT_SAME_DEVICE => return error.CrossDevice,
        .OBJECT_NAME_COLLISION => return error.PathAlreadyExists,
        .DIRECTORY_NOT_EMPTY => return error.DirNotEmpty,
        .FILE_IS_A_DIRECTORY => return error.IsDir,
        .NOT_A_DIRECTORY => return error.NotDir,
        else => return w.unexpectedStatus(rc),
    }
}

fn dirRenameWasi(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenameError!void {
    if (builtin.link_libc) return dirRenamePosix(userdata, old_dir, old_sub_path, new_dir, new_sub_path);

    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    const syscall: Syscall = try .start();
    while (true) {
        switch (std.os.wasi.path_rename(old_dir.handle, old_sub_path.ptr, old_sub_path.len, new_dir.handle, new_sub_path.ptr, new_sub_path.len)) {
            .SUCCESS => return syscall.finish(),
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
                    .DQUOT => return error.DiskQuota,
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .ISDIR => return error.IsDir,
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
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirRenamePosix(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenameError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var old_path_buffer: [posix.PATH_MAX]u8 = undefined;
    var new_path_buffer: [posix.PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    return renameat(old_dir.handle, old_sub_path_posix, new_dir.handle, new_sub_path_posix);
}

fn dirRenamePreserveLinux(
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenamePreserveError!void {
    const linux = std.os.linux;

    var old_path_buffer: [linux.PATH_MAX]u8 = undefined;
    var new_path_buffer: [linux.PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    const syscall: Syscall = try .start();
    while (true) switch (linux.errno(linux.renameat2(
        old_dir.handle,
        old_sub_path_posix,
        new_dir.handle,
        new_sub_path_posix,
        .{ .NOREPLACE = true },
    ))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .ACCES => return syscall.fail(error.AccessDenied),
        .PERM => return syscall.fail(error.PermissionDenied),
        .BUSY => return syscall.fail(error.FileBusy),
        .DQUOT => return syscall.fail(error.DiskQuota),
        .ISDIR => return syscall.fail(error.IsDir),
        .LOOP => return syscall.fail(error.SymLinkLoop),
        .MLINK => return syscall.fail(error.LinkQuotaExceeded),
        .NAMETOOLONG => return syscall.fail(error.NameTooLong),
        .NOENT => return syscall.fail(error.FileNotFound),
        .NOTDIR => return syscall.fail(error.NotDir),
        .NOMEM => return syscall.fail(error.SystemResources),
        .NOSPC => return syscall.fail(error.NoSpaceLeft),
        .EXIST => return syscall.fail(error.PathAlreadyExists),
        .NOTEMPTY => return syscall.fail(error.DirNotEmpty),
        .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
        .XDEV => return syscall.fail(error.CrossDevice),
        .ILSEQ => return syscall.fail(error.BadPathName),
        .FAULT => |err| return syscall.errnoBug(err),
        .INVAL => |err| return syscall.errnoBug(err),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn renameat(
    old_dir: posix.fd_t,
    old_sub_path: [*:0]const u8,
    new_dir: posix.fd_t,
    new_sub_path: [*:0]const u8,
) Dir.RenameError!void {
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.renameat(old_dir, old_sub_path, new_dir, new_sub_path))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .ACCES => return syscall.fail(error.AccessDenied),
        .PERM => return syscall.fail(error.PermissionDenied),
        .BUSY => return syscall.fail(error.FileBusy),
        .DQUOT => return syscall.fail(error.DiskQuota),
        .ISDIR => return syscall.fail(error.IsDir),
        .IO => return syscall.fail(error.HardwareFailure),
        .LOOP => return syscall.fail(error.SymLinkLoop),
        .MLINK => return syscall.fail(error.LinkQuotaExceeded),
        .NAMETOOLONG => return syscall.fail(error.NameTooLong),
        .NOENT => return syscall.fail(error.FileNotFound),
        .NOTDIR => return syscall.fail(error.NotDir),
        .NOMEM => return syscall.fail(error.SystemResources),
        .NOSPC => return syscall.fail(error.NoSpaceLeft),
        .EXIST => return syscall.fail(error.DirNotEmpty),
        .NOTEMPTY => return syscall.fail(error.DirNotEmpty),
        .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
        .XDEV => return syscall.fail(error.CrossDevice),
        .ILSEQ => return syscall.fail(error.BadPathName),
        .FAULT => |err| return syscall.errnoBug(err),
        .INVAL => |err| return syscall.errnoBug(err),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn renameatPreserve(
    old_dir: posix.fd_t,
    old_sub_path: [*:0]const u8,
    new_dir: posix.fd_t,
    new_sub_path: [*:0]const u8,
) Dir.RenameError!void {
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.renameat(old_dir, old_sub_path, new_dir, new_sub_path))) {
            .SUCCESS => return syscall.finish(),
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
                    .DQUOT => return error.DiskQuota,
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .ISDIR => return error.IsDir,
                    .LOOP => return error.SymLinkLoop,
                    .MLINK => return error.LinkQuotaExceeded,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .NOSPC => return error.NoSpaceLeft,
                    .EXIST => return error.PathAlreadyExists,
                    .NOTEMPTY => return error.PathAlreadyExists,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .XDEV => return error.CrossDevice,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

const dirSymLink = switch (native_os) {
    .windows => dirSymLinkWindows,
    .wasi => dirSymLinkWasi,
    else => dirSymLinkPosix,
};

fn dirSymLinkWindows(
    userdata: ?*anyopaque,
    dir: Dir,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: Dir.SymLinkFlags,
) Dir.SymLinkError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const w = windows;

    // Target path does not use sliceToPrefixedFileW because certain paths
    // are handled differently when creating a symlink than they would be
    // when converting to an NT namespaced path.
    var target_path_w: WindowsPathSpace = undefined;
    target_path_w.len = try w.wtf8ToWtf16Le(&target_path_w.data, target_path);
    target_path_w.data[target_path_w.len] = 0;
    // However, we need to canonicalize any path separators to `\`, since if
    // the target path is relative, then it must use `\` as the path separator.
    std.mem.replaceScalar(
        u16,
        target_path_w.data[0..target_path_w.len],
        std.mem.nativeToLittle(u16, '/'),
        std.mem.nativeToLittle(u16, '\\'),
    );

    const sym_link_path_w = try sliceToPrefixedFileW(dir.handle, sym_link_path, .{});

    const SYMLINK_DATA = extern struct {
        ReparseTag: w.IO_REPARSE_TAG,
        ReparseDataLength: w.USHORT,
        Reserved: w.USHORT,
        SubstituteNameOffset: w.USHORT,
        SubstituteNameLength: w.USHORT,
        PrintNameOffset: w.USHORT,
        PrintNameLength: w.USHORT,
        Flags: w.ULONG,
    };

    const symlink_handle = handle: {
        if (OpenFile(sym_link_path_w.span(), .{
            .access_mask = .{
                .GENERIC = .{ .READ = true, .WRITE = true },
                .STANDARD = .{ .SYNCHRONIZE = true },
            },
            .dir = dir.handle,
            .creation = .CREATE,
            .filter = if (flags.is_directory) .dir_only else .non_directory_only,
        })) |handle| {
            break :handle handle;
        } else |err| switch (err) {
            error.IsDir => return error.PathAlreadyExists,
            error.NotDir => return error.Unexpected,
            error.WouldBlock => return error.Unexpected,
            error.PipeBusy => return error.Unexpected,
            error.FileBusy => return error.Unexpected,
            error.NoDevice => return error.Unexpected,
            error.AntivirusInterference => return error.Unexpected,
            else => |e| return e,
        }
    };
    defer w.CloseHandle(symlink_handle);

    // Relevant portions of the documentation:
    // > Relative links are specified using the following conventions:
    // > - Root relative—for example, "\Windows\System32" resolves to "current drive:\Windows\System32".
    // > - Current working directory–relative—for example, if the current working directory is
    // >   C:\Windows\System32, "C:File.txt" resolves to "C:\Windows\System32\File.txt".
    // > Note: If you specify a current working directory–relative link, it is created as an absolute
    // > link, due to the way the current working directory is processed based on the user and the thread.
    // https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createsymboliclinkw
    var is_target_absolute = false;
    const final_target_path = target_path: {
        if (w.hasCommonNtPrefix(u16, target_path_w.span())) {
            // Already an NT path, no need to do anything to it
            break :target_path target_path_w.span();
        } else {
            switch (Dir.path.getWin32PathType(u16, target_path_w.span())) {
                // Rooted paths need to avoid getting put through wToPrefixedFileW
                // (and they are treated as relative in this context)
                // Note: It seems that rooted paths in symbolic links are relative to
                //       the drive that the symbolic exists on, not to the CWD's drive.
                //       So, if the symlink is on C:\ and the CWD is on D:\,
                //       it will still resolve the path relative to the root of
                //       the C:\ drive.
                .rooted => break :target_path target_path_w.span(),
                // Keep relative paths relative, but anything else needs to get NT-prefixed.
                else => if (!Dir.path.isAbsoluteWindowsWtf16(target_path_w.span()))
                    break :target_path target_path_w.span(),
            }
        }
        var prefixed_target_path = try wToPrefixedFileW(dir.handle, target_path_w.span(), .{});
        // We do this after prefixing to ensure that drive-relative paths are treated as absolute
        is_target_absolute = Dir.path.isAbsoluteWindowsWtf16(prefixed_target_path.span());
        break :target_path prefixed_target_path.span();
    };

    // prepare reparse data buffer
    var buffer: [w.MAXIMUM_REPARSE_DATA_BUFFER_SIZE]u8 = undefined;
    const buf_len = @sizeOf(SYMLINK_DATA) + final_target_path.len * 4;
    const header_len = @sizeOf(w.ULONG) + @sizeOf(w.USHORT) * 2;
    const target_is_absolute = Dir.path.isAbsoluteWindowsWtf16(final_target_path);
    const symlink_data: SYMLINK_DATA = .{
        .ReparseTag = .SYMLINK,
        .ReparseDataLength = @intCast(buf_len - header_len),
        .Reserved = 0,
        .SubstituteNameOffset = @intCast(final_target_path.len * 2),
        .SubstituteNameLength = @intCast(final_target_path.len * 2),
        .PrintNameOffset = 0,
        .PrintNameLength = @intCast(final_target_path.len * 2),
        .Flags = if (!target_is_absolute) w.SYMLINK_FLAG_RELATIVE else 0,
    };

    @memcpy(buffer[0..@sizeOf(SYMLINK_DATA)], std.mem.asBytes(&symlink_data));
    @memcpy(buffer[@sizeOf(SYMLINK_DATA)..][0 .. final_target_path.len * 2], @as([*]const u8, @ptrCast(final_target_path)));
    const paths_start = @sizeOf(SYMLINK_DATA) + final_target_path.len * 2;
    @memcpy(buffer[paths_start..][0 .. final_target_path.len * 2], @as([*]const u8, @ptrCast(final_target_path)));
    switch ((try deviceIoControl(&.{
        .file = .{ .handle = symlink_handle, .flags = .{ .nonblocking = false } },
        .code = .SET_REPARSE_POINT,
        .in = buffer[0..buf_len],
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INSUFFICIENT_RESOURCES => return error.SystemResources,
        .PRIVILEGE_NOT_HELD => return error.PermissionDenied,
        .ACCESS_DENIED => return error.AccessDenied,
        .INVALID_DEVICE_REQUEST => return error.FileSystem,
        else => |status| return w.unexpectedStatus(status),
    }
}

fn dirSymLinkWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: Dir.SymLinkFlags,
) Dir.SymLinkError!void {
    if (builtin.link_libc) return dirSymLinkPosix(userdata, dir, target_path, sym_link_path, flags);

    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    const syscall: Syscall = try .start();
    while (true) {
        switch (std.os.wasi.path_symlink(target_path.ptr, target_path.len, dir.handle, sym_link_path.ptr, sym_link_path.len)) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err),
                    .ACCES => return error.AccessDenied,
                    .PERM => return error.PermissionDenied,
                    .DQUOT => return error.DiskQuota,
                    .EXIST => return error.PathAlreadyExists,
                    .IO => return error.FileSystem,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .NOSPC => return error.NoSpaceLeft,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirSymLinkPosix(
    userdata: ?*anyopaque,
    dir: Dir,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: Dir.SymLinkFlags,
) Dir.SymLinkError!void {
    _ = flags;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var target_path_buffer: [posix.PATH_MAX]u8 = undefined;
    var sym_link_path_buffer: [posix.PATH_MAX]u8 = undefined;

    const target_path_posix = try pathToPosix(target_path, &target_path_buffer);
    const sym_link_path_posix = try pathToPosix(sym_link_path, &sym_link_path_buffer);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.symlinkat(target_path_posix, dir.handle, sym_link_path_posix))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .ACCES => return error.AccessDenied,
                    .PERM => return error.PermissionDenied,
                    .DQUOT => return error.DiskQuota,
                    .EXIST => return error.PathAlreadyExists,
                    .IO => return error.FileSystem,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    .NOSPC => return error.NoSpaceLeft,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirReadLink(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, buffer: []u8) Dir.ReadLinkError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    switch (native_os) {
        .windows => return dirReadLinkWindows(dir, sub_path, buffer),
        .wasi => return dirReadLinkWasi(dir, sub_path, buffer),
        else => return dirReadLinkPosix(dir, sub_path, buffer),
    }
}

fn dirReadLinkWindows(dir: Dir, sub_path: []const u8, buffer: []u8) Dir.ReadLinkError!usize {
    // This gets used once for `sub_path` and then reused again temporarily
    // before converting back to `buffer`.
    var sub_path_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
    const attr: windows.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w.span())) null else dir.handle,
        .ObjectName = @constCast(&sub_path_w.string()),
    };
    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var result_handle: windows.HANDLE = undefined;
    var attempt: u5 = 0;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtCreateFile(
        &result_handle,
        .{
            .SPECIFIC = .{ .FILE = .{
                .READ_ATTRIBUTES = true,
            } },
            .STANDARD = .{ .SYNCHRONIZE = true },
        },
        &attr,
        &io_status_block,
        null,
        .{ .NORMAL = true },
        .VALID_FLAGS,
        .OPEN,
        .{
            .DIRECTORY_FILE = false,
            .NON_DIRECTORY_FILE = false,
            .IO = .ASYNCHRONOUS,
            .OPEN_REPARSE_POINT = true,
        },
        null,
        0,
    )) {
        .SUCCESS => {
            syscall.finish();
            break;
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .SHARING_VIOLATION => {
            // This occurs if the file attempting to be opened is a running
            // executable. However, there's a kernel bug: the error may be
            // incorrectly returned for an indeterminate amount of time
            // after an executable file is closed. Here we work around the
            // kernel bug with retry attempts.
            syscall.finish();
            if (max_windows_kernel_bug_retries - attempt == 0) return error.FileBusy;
            try parking_sleep.sleep(.{ .duration = .{
                .raw = .fromMilliseconds((@as(u32, 1) << attempt) >> 1),
                .clock = .awake,
            } });
            attempt += 1;
            syscall = try .start();
            continue;
        },
        .DELETE_PENDING => {
            // This error means that there *was* a file in this location on
            // the file system, but it was deleted. However, the OS is not
            // finished with the deletion operation, and so this CreateFile
            // call has failed. Here, we simulate the kernel bug being
            // fixed by sleeping and retrying until the error goes away.
            syscall.finish();
            if (max_windows_kernel_bug_retries - attempt == 0) return error.FileBusy;
            try parking_sleep.sleep(.{ .duration = .{
                .raw = .fromMilliseconds((@as(u32, 1) << attempt) >> 1),
                .clock = .awake,
            } });
            attempt += 1;
            syscall = try .start();
            continue;
        },
        .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
        .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .BAD_NETWORK_PATH => return syscall.fail(error.NetworkNotFound), // \\server was not found
        .BAD_NETWORK_NAME => return syscall.fail(error.NetworkNotFound), // \\server was found but \\server\share wasn't
        .NO_MEDIA_IN_DEVICE => return syscall.fail(error.FileNotFound),
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .PIPE_BUSY => return syscall.fail(error.AccessDenied),
        .PIPE_NOT_AVAILABLE => return syscall.fail(error.FileNotFound),
        .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
        .VIRUS_INFECTED, .VIRUS_DELETED => return syscall.fail(error.AntivirusInterference),
        .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
        .OBJECT_PATH_SYNTAX_BAD => |status| return syscall.ntstatusBug(status),
        .INVALID_HANDLE => |status| return syscall.ntstatusBug(status),
        else => |status| return syscall.unexpectedNtstatus(status),
    };
    defer windows.CloseHandle(result_handle);

    var reparse_buf: [windows.MAXIMUM_REPARSE_DATA_BUFFER_SIZE]u8 align(@alignOf(windows.REPARSE_DATA_BUFFER)) = undefined;

    syscall = try .start();
    while (true) switch (windows.ntdll.NtFsControlFile(
        result_handle,
        null, // event
        null, // APC routine
        null, // APC context
        &io_status_block,
        .GET_REPARSE_POINT,
        null, // input buffer
        0, // input buffer length
        &reparse_buf,
        reparse_buf.len,
    )) {
        .SUCCESS => {
            syscall.finish();
            break;
        },
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .NOT_A_REPARSE_POINT => return syscall.fail(error.NotLink),
        else => |status| return syscall.unexpectedNtstatus(status),
    };

    const reparse_struct: *const windows.REPARSE_DATA_BUFFER = @ptrCast(@alignCast(&reparse_buf));
    const IoReparseTagInt = @typeInfo(windows.IO_REPARSE_TAG).@"struct".backing_integer.?;
    const result_w = switch (@as(IoReparseTagInt, @bitCast(reparse_struct.ReparseTag))) {
        @as(IoReparseTagInt, @bitCast(windows.IO_REPARSE_TAG.SYMLINK)) => r: {
            const buf: *const windows.SYMBOLIC_LINK_REPARSE_BUFFER = @ptrCast(@alignCast(&reparse_struct.DataBuffer[0]));
            const offset = buf.SubstituteNameOffset >> 1;
            const len = buf.SubstituteNameLength >> 1;
            const path_buf = @as([*]const u16, &buf.PathBuffer);
            const is_relative = buf.Flags & windows.SYMLINK_FLAG_RELATIVE != 0;
            break :r try parseReadLinkPath(path_buf[offset..][0..len], is_relative, &sub_path_w.data);
        },
        @as(IoReparseTagInt, @bitCast(windows.IO_REPARSE_TAG.MOUNT_POINT)) => r: {
            const buf: *const windows.MOUNT_POINT_REPARSE_BUFFER = @ptrCast(@alignCast(&reparse_struct.DataBuffer[0]));
            const offset = buf.SubstituteNameOffset >> 1;
            const len = buf.SubstituteNameLength >> 1;
            const path_buf = @as([*]const u16, &buf.PathBuffer);
            break :r try parseReadLinkPath(path_buf[offset..][0..len], false, &sub_path_w.data);
        },
        else => return error.UnsupportedReparsePointType,
    };
    const len = std.unicode.calcWtf8Len(result_w);
    if (len > buffer.len) return error.NameTooLong;

    return std.unicode.wtf16LeToWtf8(buffer, result_w);
}

fn parseReadLinkPath(path: []const u16, is_relative: bool, out_buffer: []u16) error{NameTooLong}![]u16 {
    path: {
        if (is_relative) break :path;
        return windows.ntToWin32Namespace(path, out_buffer) catch |err| switch (err) {
            error.NameTooLong => |e| return e,
            error.NotNtPath => break :path,
        };
    }
    if (out_buffer.len < path.len) return error.NameTooLong;
    const dest = out_buffer[0..path.len];
    @memcpy(dest, path);
    return dest;
}

fn dirReadLinkWasi(dir: Dir, sub_path: []const u8, buffer: []u8) Dir.ReadLinkError!usize {
    if (builtin.link_libc) return dirReadLinkPosix(dir, sub_path, buffer);

    var n: usize = undefined;
    const syscall: Syscall = try .start();
    while (true) {
        switch (std.os.wasi.path_readlink(dir.handle, sub_path.ptr, sub_path.len, buffer.ptr, buffer.len, &n)) {
            .SUCCESS => {
                syscall.finish();
                return n;
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
                    .INVAL => return error.NotLink,
                    .IO => return error.FileSystem,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => return error.NotDir,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirReadLinkPosix(dir: Dir, sub_path: []const u8, buffer: []u8) Dir.ReadLinkError!usize {
    var sub_path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &sub_path_buffer);

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.readlinkat(dir.handle, sub_path_posix, buffer.ptr, buffer.len);
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
                    .INVAL => return error.NotLink,
                    .IO => return error.FileSystem,
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => return error.NotDir,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

const dirSetPermissions = switch (native_os) {
    .windows => dirSetPermissionsWindows,
    else => dirSetPermissionsPosix,
};

fn dirSetPermissionsWindows(userdata: ?*anyopaque, dir: Dir, permissions: Dir.Permissions) Dir.SetPermissionsError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    _ = dir;
    _ = permissions;
    @panic("TODO implement dirSetPermissionsWindows");
}

fn dirSetPermissionsPosix(userdata: ?*anyopaque, dir: Dir, permissions: Dir.Permissions) Dir.SetPermissionsError!void {
    if (@sizeOf(Dir.Permissions) == 0) return;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return setPermissionsPosix(dir.handle, permissions.toMode());
}

fn dirSetFilePermissions(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.SetFilePermissionsOptions,
) Dir.SetFilePermissionsError!void {
    if (@sizeOf(Dir.Permissions) == 0) return;
    if (is_windows) @panic("TODO implement dirSetFilePermissions windows");
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const mode = permissions.toMode();
    const flags: u32 = if (!options.follow_symlinks) posix.AT.SYMLINK_NOFOLLOW else 0;

    return posixFchmodat(t, dir.handle, sub_path_posix, mode, flags);
}

fn posixFchmodat(
    t: *Threaded,
    dir_fd: posix.fd_t,
    path: [*:0]const u8,
    mode: posix.mode_t,
    flags: u32,
) Dir.SetFilePermissionsError!void {
    // No special handling for linux is needed if we can use the libc fallback
    // or `flags` is empty. Glibc only added the fallback in 2.32.
    if (have_fchmodat_flags or flags == 0) {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = if (have_fchmodat_flags or builtin.link_libc)
                posix.system.fchmodat(dir_fd, path, mode, flags)
            else
                posix.system.fchmodat(dir_fd, path, mode);
            switch (posix.errno(rc)) {
                .SUCCESS => return syscall.finish(),
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .BADF => |err| return errnoBug(err),
                        .FAULT => |err| return errnoBug(err),
                        .INVAL => |err| return errnoBug(err),
                        .ACCES => return error.AccessDenied,
                        .IO => return error.InputOutput,
                        .LOOP => return error.SymLinkLoop,
                        .MFILE => return error.ProcessFdQuotaExceeded,
                        .NAMETOOLONG => return error.NameTooLong,
                        .NFILE => return error.SystemFdQuotaExceeded,
                        .NOENT => return error.FileNotFound,
                        .NOTDIR => return error.FileNotFound,
                        .NOMEM => return error.SystemResources,
                        .OPNOTSUPP => return error.OperationUnsupported,
                        .PERM => return error.PermissionDenied,
                        .ROFS => return error.ReadOnlyFileSystem,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    if (@atomicLoad(UseFchmodat2, &t.use_fchmodat2, .monotonic) == .disabled)
        return fchmodatFallback(dir_fd, path, mode);

    comptime assert(native_os == .linux);

    const syscall: Syscall = try .start();
    while (true) {
        switch (std.os.linux.errno(std.os.linux.fchmodat2(dir_fd, path, mode, flags))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
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
                    .NOSYS => {
                        @atomicStore(UseFchmodat2, &t.use_fchmodat2, .disabled, .monotonic);
                        return fchmodatFallback(dir_fd, path, mode);
                    },
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fchmodatFallback(
    dir_fd: posix.fd_t,
    path: [*:0]const u8,
    mode: posix.mode_t,
) Dir.SetFilePermissionsError!void {
    comptime assert(native_os == .linux);

    // Fallback to changing permissions using procfs:
    //
    // 1. Open `path` as a `PATH` descriptor.
    // 2. Stat the fd and check if it isn't a symbolic link.
    // 3. Generate the procfs reference to the fd via `/proc/self/fd/{fd}`.
    // 4. Pass the procfs path to `chmod` with the `mode`.
    const path_fd: posix.fd_t = fd: {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = posix.system.openat(dir_fd, path, .{
                .PATH = true,
                .NOFOLLOW = true,
                .CLOEXEC = true,
            }, @as(posix.mode_t, 0));
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    break :fd @intCast(rc);
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .FAULT => |err| return errnoBug(err),
                        .INVAL => |err| return errnoBug(err),
                        .ACCES => return error.AccessDenied,
                        .PERM => return error.PermissionDenied,
                        .LOOP => return error.SymLinkLoop,
                        .MFILE => return error.ProcessFdQuotaExceeded,
                        .NAMETOOLONG => return error.NameTooLong,
                        .NFILE => return error.SystemFdQuotaExceeded,
                        .NOENT => return error.FileNotFound,
                        .NOMEM => return error.SystemResources,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    };
    defer closeFd(path_fd);

    const path_mode = mode: {
        const sys = if (statx_use_c) std.c else std.os.linux;
        const syscall: Syscall = try .start();
        while (true) {
            var statx = std.mem.zeroes(std.os.linux.Statx);
            switch (sys.errno(sys.statx(path_fd, "", posix.AT.EMPTY_PATH, .{ .TYPE = true }, &statx))) {
                .SUCCESS => {
                    syscall.finish();
                    if (!statx.mask.TYPE) return error.Unexpected;
                    break :mode statx.mode;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .ACCES => return error.AccessDenied,
                        .LOOP => return error.SymLinkLoop,
                        .NOMEM => return error.SystemResources,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    };

    // Even though we only wanted TYPE, the kernel can still fill in the additional bits.
    if ((path_mode & posix.S.IFMT) == posix.S.IFLNK)
        return error.OperationUnsupported;

    var procfs_buf: ["/proc/self/fd/-2147483648\x00".len]u8 = undefined;
    const proc_path = std.fmt.bufPrintSentinel(&procfs_buf, "/proc/self/fd/{d}", .{path_fd}, 0) catch unreachable;
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.chmod(proc_path, mode))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .NOENT => return error.OperationUnsupported, // procfs not mounted.
                    .BADF => |err| return errnoBug(err),
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .ACCES => return error.AccessDenied,
                    .IO => return error.InputOutput,
                    .LOOP => return error.SymLinkLoop,
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => return error.FileNotFound,
                    .PERM => return error.PermissionDenied,
                    .ROFS => return error.ReadOnlyFileSystem,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

const dirSetOwner = switch (native_os) {
    .windows => dirSetOwnerUnsupported,
    else => dirSetOwnerPosix,
};

fn dirSetOwnerUnsupported(userdata: ?*anyopaque, dir: Dir, owner: ?File.Uid, group: ?File.Gid) Dir.SetOwnerError!void {
    _ = userdata;
    _ = dir;
    _ = owner;
    _ = group;
    return error.Unexpected;
}

fn dirSetOwnerPosix(userdata: ?*anyopaque, dir: Dir, owner: ?File.Uid, group: ?File.Gid) Dir.SetOwnerError!void {
    if (!have_fchown) return error.Unexpected; // Unsupported OS, don't call this function.
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const uid = owner orelse ~@as(posix.uid_t, 0);
    const gid = group orelse ~@as(posix.gid_t, 0);
    return posixFchown(dir.handle, uid, gid);
}

fn posixFchown(fd: posix.fd_t, uid: posix.uid_t, gid: posix.gid_t) File.SetOwnerError!void {
    comptime assert(have_fchown);
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.fchown(fd, uid, gid))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
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
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirSetFileOwner(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    owner: ?File.Uid,
    group: ?File.Gid,
    options: Dir.SetFileOwnerOptions,
) Dir.SetFileOwnerError!void {
    if (!have_fchown) return error.Unexpected; // Unsupported OS, don't call this function.
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    _ = dir;
    _ = sub_path_posix;
    _ = owner;
    _ = group;
    _ = options;
    @panic("TODO implement dirSetFileOwner");
}

const fileSync = switch (native_os) {
    .windows => fileSyncWindows,
    .wasi => fileSyncWasi,
    else => fileSyncPosix,
};

fn fileSyncWindows(userdata: ?*anyopaque, file: File) File.SyncError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    const syscall: Syscall = try .start();
    while (true) {
        switch (windows.ntdll.NtFlushBuffersFile(file.handle, &io_status_block)) {
            .SUCCESS => break syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INVALID_HANDLE => unreachable,
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied), // a sync was performed but the system couldn't update the access time
            .UNEXPECTED_NETWORK_ERROR => return syscall.fail(error.InputOutput),
            else => |status| return syscall.unexpectedNtstatus(status),
        }
    }
}

fn fileSyncPosix(userdata: ?*anyopaque, file: File) File.SyncError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.fsync(file.handle))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .BADF => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .ROFS => |err| return errnoBug(err),
                    .IO => return error.InputOutput,
                    .NOSPC => return error.NoSpaceLeft,
                    .DQUOT => return error.DiskQuota,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileSyncWasi(userdata: ?*anyopaque, file: File) File.SyncError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const syscall: Syscall = try .start();
    while (true) {
        switch (std.os.wasi.fd_sync(file.handle)) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .BADF => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .ROFS => |err| return errnoBug(err),
                    .IO => return error.InputOutput,
                    .NOSPC => return error.NoSpaceLeft,
                    .DQUOT => return error.DiskQuota,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileIsTty(userdata: ?*anyopaque, file: File) Io.Cancelable!bool {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return isTty(file);
}

fn isTty(file: File) Io.Cancelable!bool {
    if (is_windows) {
        var get_console_mode = windows.CONSOLE.USER_IO.GET_MODE;
        switch ((try deviceIoControl(&.{
            .file = .{
                .handle = windows.peb().ProcessParameters.ConsoleHandle,
                .flags = .{ .nonblocking = false },
            },
            .code = windows.IOCTL.CONDRV.ISSUE_USER_IO,
            .in = @ptrCast(&get_console_mode.request(file, 0, .{}, 0, .{})),
        })).u.Status) {
            .SUCCESS => return true,
            .CANCELLED => unreachable,
            .INVALID_HANDLE => return isCygwinPty(file),
            else => return false,
        }
    }

    if (builtin.link_libc) {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = posix.system.isatty(file.handle);
            switch (posix.errno(rc - 1)) {
                .SUCCESS => {
                    syscall.finish();
                    return true;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => {
                    syscall.finish();
                    return false;
                },
            }
        }
    }

    if (native_os == .wasi) {
        var statbuf: std.os.wasi.fdstat_t = undefined;
        const err = std.os.wasi.fd_fdstat_get(file.handle, &statbuf);
        if (err != .SUCCESS)
            return false;

        // A tty is a character device that we can't seek or tell on.
        if (statbuf.fs_filetype != .CHARACTER_DEVICE)
            return false;
        if (statbuf.fs_rights_base.FD_SEEK or statbuf.fs_rights_base.FD_TELL)
            return false;

        return true;
    }

    if (native_os == .linux) {
        const linux = std.os.linux;
        const syscall: Syscall = try .start();
        while (true) {
            var wsz: posix.winsize = undefined;
            const fd: usize = @bitCast(@as(isize, file.handle));
            const rc = linux.syscall3(.ioctl, fd, linux.T.IOCGWINSZ, @intFromPtr(&wsz));
            switch (linux.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    return true;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => {
                    syscall.finish();
                    return false;
                },
            }
        }
    }

    @compileError("unimplemented");
}

fn fileEnableAnsiEscapeCodes(userdata: ?*anyopaque, file: File) File.EnableAnsiEscapeCodesError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (!is_windows) return if (!try supportsAnsiEscapeCodes(file)) error.NotTerminalDevice;

    // For Windows Terminal, VT Sequences processing is enabled by default.
    const console: File = .{
        .handle = windows.peb().ProcessParameters.ConsoleHandle,
        .flags = .{ .nonblocking = false },
    };
    var get_console_mode = windows.CONSOLE.USER_IO.GET_MODE;
    switch ((try deviceIoControl(&.{
        .file = console,
        .code = windows.IOCTL.CONDRV.ISSUE_USER_IO,
        .in = @ptrCast(&get_console_mode.request(file, 0, .{}, 0, .{})),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        .INVALID_HANDLE => return if (!try isCygwinPty(file)) error.NotTerminalDevice,
        else => return error.NotTerminalDevice,
    }

    if (get_console_mode.Data & windows.ENABLE_VIRTUAL_TERMINAL_PROCESSING != 0) return;

    // For Windows Console, VT Sequences processing support was added in Windows 10 build 14361, but disabled by default.
    // https://devblogs.microsoft.com/commandline/tmux-support-arrives-for-bash-on-ubuntu-on-windows/
    //
    // Note: In Microsoft's example for enabling virtual terminal processing, it
    // shows attempting to enable `DISABLE_NEWLINE_AUTO_RETURN` as well:
    // https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#example-of-enabling-virtual-terminal-processing
    // This is avoided because in the old Windows Console, that flag causes \n (as opposed to \r\n)
    // to behave unexpectedly (the cursor moves down 1 row but remains on the same column).
    // Additionally, the default console mode in Windows Terminal does not have
    // `DISABLE_NEWLINE_AUTO_RETURN` set, so by only enabling `ENABLE_VIRTUAL_TERMINAL_PROCESSING`
    // we end up matching the mode of Windows Terminal.
    var set_console_mode = windows.CONSOLE.USER_IO.SET_MODE(
        get_console_mode.Data | windows.ENABLE_VIRTUAL_TERMINAL_PROCESSING,
    );
    switch ((try deviceIoControl(&.{
        .file = console,
        .code = windows.IOCTL.CONDRV.ISSUE_USER_IO,
        .in = @ptrCast(&set_console_mode.request(file, 0, .{}, 0, .{})),
    })).u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn fileSupportsAnsiEscapeCodes(userdata: ?*anyopaque, file: File) Io.Cancelable!bool {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return supportsAnsiEscapeCodes(file);
}

fn supportsAnsiEscapeCodes(file: File) Io.Cancelable!bool {
    if (is_windows) {
        var get_console_mode = windows.CONSOLE.USER_IO.GET_MODE;
        switch ((try deviceIoControl(&.{
            .file = .{
                .handle = windows.peb().ProcessParameters.ConsoleHandle,
                .flags = .{ .nonblocking = false },
            },
            .code = windows.IOCTL.CONDRV.ISSUE_USER_IO,
            .in = @ptrCast(&get_console_mode.request(file, 0, .{}, 0, .{})),
        })).u.Status) {
            .SUCCESS => if (get_console_mode.Data & windows.ENABLE_VIRTUAL_TERMINAL_PROCESSING != 0)
                return true,
            .CANCELLED => unreachable,
            .INVALID_HANDLE => return isCygwinPty(file),
            else => return false,
        }
    }

    if (try isTty(file)) return true;

    return false;
}

fn isCygwinPty(file: File) Io.Cancelable!bool {
    if (!is_windows) return false;

    const handle = file.handle;

    // If this is a MSYS2/cygwin pty, then it will be a named pipe with a name in one of these formats:
    //   msys-[...]-ptyN-[...]
    //   cygwin-[...]-ptyN-[...]
    //
    // Example: msys-1888ae32e00d56aa-pty0-to-master

    // First, just check that the handle is a named pipe.
    // This allows us to avoid the more costly NtQueryInformationFile call
    // for handles that aren't named pipes.
    {
        var io_status: windows.IO_STATUS_BLOCK = undefined;
        var device_info: windows.FILE.FS_DEVICE_INFORMATION = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtQueryVolumeInformationFile(
            handle,
            &io_status,
            &device_info,
            @sizeOf(windows.FILE.FS_DEVICE_INFORMATION),
            .Device,
        )) {
            .SUCCESS => break syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            else => {
                syscall.finish();
                return false;
            },
        };
        if (device_info.DeviceType.FileDevice != .NAMED_PIPE) return false;
    }

    const name_bytes_offset = @offsetOf(windows.FILE.NAME_INFORMATION, "FileName");
    // `NAME_MAX` UTF-16 code units (2 bytes each)
    // This buffer may not be long enough to handle *all* possible paths
    // (PATH_MAX_WIDE would be necessary for that), but because we only care
    // about certain paths and we know they must be within a reasonable length,
    // we can use this smaller buffer and just return false on any error from
    // NtQueryInformationFile.
    const num_name_bytes = windows.MAX_PATH * 2;
    var name_info_bytes align(@alignOf(windows.FILE.NAME_INFORMATION)) = [_]u8{0} ** (name_bytes_offset + num_name_bytes);

    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    const syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtQueryInformationFile(
        handle,
        &io_status_block,
        &name_info_bytes,
        @intCast(name_info_bytes.len),
        .Name,
    )) {
        .SUCCESS => break syscall.finish(),
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .INVALID_PARAMETER => unreachable,
        else => {
            syscall.finish();
            return false;
        },
    };

    const name_info: *const windows.FILE.NAME_INFORMATION = @ptrCast(&name_info_bytes);
    const name_bytes = name_info_bytes[name_bytes_offset .. name_bytes_offset + name_info.FileNameLength];
    const name_wide = std.mem.bytesAsSlice(u16, name_bytes);
    // The name we get from NtQueryInformationFile will be prefixed with a '\', e.g. \msys-1888ae32e00d56aa-pty0-to-master
    return (std.mem.startsWith(u16, name_wide, &[_]u16{ '\\', 'm', 's', 'y', 's', '-' }) or
        std.mem.startsWith(u16, name_wide, &[_]u16{ '\\', 'c', 'y', 'g', 'w', 'i', 'n', '-' })) and
        std.mem.indexOf(u16, name_wide, &[_]u16{ '-', 'p', 't', 'y' }) != null;
}

fn fileSetLength(userdata: ?*anyopaque, file: File, length: u64) File.SetLengthError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    const signed_len: i64 = @bitCast(length);
    if (signed_len < 0) return error.FileTooBig; // Avoid ambiguous EINVAL errors.

    if (is_windows) {
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        var eof_info: windows.FILE.END_OF_FILE_INFORMATION = .{
            .EndOfFile = signed_len,
        };

        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtSetInformationFile(
            file.handle,
            &io_status_block,
            &eof_info,
            @sizeOf(windows.FILE.END_OF_FILE_INFORMATION),
            .EndOfFile,
        )) {
            .SUCCESS => return syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INVALID_HANDLE => |err| return syscall.ntstatusBug(err), // Handle not open for writing.
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
            .INVALID_PARAMETER => return syscall.fail(error.FileTooBig),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    if (native_os == .wasi and !builtin.link_libc) {
        const syscall: Syscall = try .start();
        while (true) {
            switch (std.os.wasi.fd_filestat_set_size(file.handle, length)) {
                .SUCCESS => return syscall.finish(),
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .FBIG => return error.FileTooBig,
                        .IO => return error.InputOutput,
                        .PERM => return error.PermissionDenied,
                        .TXTBSY => return error.FileBusy,
                        .BADF => |err| return errnoBug(err), // Handle not open for writing
                        .INVAL => return error.NonResizable,
                        .NOTCAPABLE => return error.AccessDenied,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(ftruncate_sym(file.handle, signed_len))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .FBIG => return error.FileTooBig,
                    .IO => return error.InputOutput,
                    .PERM => return error.PermissionDenied,
                    .TXTBSY => return error.FileBusy,
                    .BADF => |err| return errnoBug(err), // Handle not open for writing.
                    .INVAL => return error.NonResizable, // This is returned for /dev/null for example.
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileSetOwner(userdata: ?*anyopaque, file: File, owner: ?File.Uid, group: ?File.Gid) File.SetOwnerError!void {
    if (!have_fchown) return error.Unexpected; // Unsupported OS, don't call this function.
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const uid = owner orelse ~@as(posix.uid_t, 0);
    const gid = group orelse ~@as(posix.gid_t, 0);
    return posixFchown(file.handle, uid, gid);
}

fn fileSetPermissions(userdata: ?*anyopaque, file: File, permissions: File.Permissions) File.SetPermissionsError!void {
    if (@sizeOf(File.Permissions) == 0) return;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    switch (native_os) {
        .windows => {
            var io_status_block: windows.IO_STATUS_BLOCK = undefined;
            var info: windows.FILE.BASIC_INFORMATION = .{
                .CreationTime = 0,
                .LastAccessTime = 0,
                .LastWriteTime = 0,
                .ChangeTime = 0,
                .FileAttributes = permissions.toAttributes(),
            };
            const syscall: Syscall = try .start();
            while (true) switch (windows.ntdll.NtSetInformationFile(
                file.handle,
                &io_status_block,
                &info,
                @sizeOf(windows.FILE.BASIC_INFORMATION),
                .Basic,
            )) {
                .SUCCESS => return syscall.finish(),
                .CANCELLED => {
                    try syscall.checkCancel();
                    continue;
                },
                .INVALID_HANDLE => |err| return syscall.ntstatusBug(err),
                .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
                else => |status| return syscall.unexpectedNtstatus(status),
            };
        },
        .wasi => return error.Unexpected, // Unsupported OS.
        else => return setPermissionsPosix(file.handle, permissions.toMode()),
    }
}

fn setPermissionsPosix(fd: posix.fd_t, mode: posix.mode_t) File.SetPermissionsError!void {
    comptime assert(have_fchmod);
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.fchmod(fd, mode))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .BADF => |err| return errnoBug(err),
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
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirSetTimestamps(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.SetTimestampsOptions,
) Dir.SetTimestampsError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        @panic("TODO implement dirSetTimestamps windows");
    }

    if (native_os == .wasi and !builtin.link_libc) {
        @panic("TODO implement dirSetTimestamps wasi");
    }

    var times_buffer: [2]posix.timespec = undefined;
    const times = if (options.modify_timestamp == .now and options.access_timestamp == .now) null else p: {
        times_buffer = .{
            setTimestampToPosix(options.access_timestamp),
            setTimestampToPosix(options.modify_timestamp),
        };
        break :p &times_buffer;
    };

    const flags: u32 = if (!options.follow_symlinks) posix.AT.SYMLINK_NOFOLLOW else 0;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.utimensat(dir.handle, sub_path_posix, times, flags))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .BADF => |err| return syscall.errnoBug(err), // always a race condition
        .FAULT => |err| return syscall.errnoBug(err),
        .INVAL => |err| return syscall.errnoBug(err),
        .ACCES => return syscall.fail(error.AccessDenied),
        .PERM => return syscall.fail(error.PermissionDenied),
        .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn fileSetTimestamps(
    userdata: ?*anyopaque,
    file: File,
    options: File.SetTimestampsOptions,
) File.SetTimestampsError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        const now_sys = if (options.access_timestamp == .now or options.modify_timestamp == .now)
            windows.ntdll.RtlGetSystemTimePrecise()
        else
            undefined;
        var iosb: windows.IO_STATUS_BLOCK = undefined;
        var info: windows.FILE.BASIC_INFORMATION = .{
            .CreationTime = 0,
            .LastAccessTime = switch (options.access_timestamp) {
                .unchanged => 0,
                .now => now_sys,
                .new => |ts| windows.toSysTime(ts),
            },
            .LastWriteTime = switch (options.modify_timestamp) {
                .unchanged => 0,
                .now => now_sys,
                .new => |ts| windows.toSysTime(ts),
            },
            .ChangeTime = 0,
            .FileAttributes = .{},
        };
        var syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtSetInformationFile(
            file.handle,
            &iosb,
            &info,
            @sizeOf(windows.FILE.BASIC_INFORMATION),
            .Basic,
        )) {
            .SUCCESS => return syscall.finish(),
            .CANCELLED => try syscall.checkCancel(),
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    if (native_os == .wasi and !builtin.link_libc) {
        var atime: std.os.wasi.timestamp_t = 0;
        var mtime: std.os.wasi.timestamp_t = 0;
        var flags: std.os.wasi.fstflags_t = .{};

        switch (options.access_timestamp) {
            .unchanged => {},
            .now => flags.ATIM_NOW = true,
            .new => |ts| {
                atime = timestampToPosix(ts.nanoseconds).toTimestamp();
                flags.ATIM = true;
            },
        }

        switch (options.modify_timestamp) {
            .unchanged => {},
            .now => flags.MTIM_NOW = true,
            .new => |ts| {
                mtime = timestampToPosix(ts.nanoseconds).toTimestamp();
                flags.MTIM = true;
            },
        }

        const syscall: Syscall = try .start();
        while (true) switch (std.os.wasi.fd_filestat_set_times(file.handle, atime, mtime, flags)) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .BADF => |err| return syscall.errnoBug(err), // File descriptor use-after-free.
            .FAULT => |err| return syscall.errnoBug(err),
            .INVAL => |err| return syscall.errnoBug(err),
            .ACCES => return syscall.fail(error.AccessDenied),
            .PERM => return syscall.fail(error.PermissionDenied),
            .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
            else => |err| return syscall.unexpectedErrno(err),
        };
    }

    var times_buffer: [2]posix.timespec = undefined;
    const times = if (options.modify_timestamp == .now and options.access_timestamp == .now) null else p: {
        times_buffer = .{
            setTimestampToPosix(options.access_timestamp),
            setTimestampToPosix(options.modify_timestamp),
        };
        break :p &times_buffer;
    };

    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.futimens(file.handle, times))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .BADF => |err| return syscall.errnoBug(err), // always a race condition
        .FAULT => |err| return syscall.errnoBug(err),
        .INVAL => |err| return syscall.errnoBug(err),
        .ACCES => return syscall.fail(error.AccessDenied),
        .PERM => return syscall.fail(error.PermissionDenied),
        .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

const windows_lock_range_off: windows.LARGE_INTEGER = 0;
const windows_lock_range_len: windows.LARGE_INTEGER = 1;

fn fileLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!void {
    if (native_os == .wasi) return error.FileLocksUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        const exclusive = switch (lock) {
            .none => {
                // To match the non-Windows behavior, unlock
                var io_status_block: windows.IO_STATUS_BLOCK = undefined;
                while (true) switch (windows.ntdll.NtUnlockFile(
                    file.handle,
                    &io_status_block,
                    &windows_lock_range_off,
                    &windows_lock_range_len,
                    0,
                )) {
                    .SUCCESS => return,
                    .CANCELLED => continue,
                    .RANGE_NOT_LOCKED => return,
                    .ACCESS_VIOLATION => |err| return windows.statusBug(err), // bad io_status_block pointer
                    else => |status| return windows.unexpectedStatus(status),
                };
            },
            .shared => false,
            .exclusive => true,
        };
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtLockFile(
            file.handle,
            null,
            null,
            null,
            &io_status_block,
            &windows_lock_range_off,
            &windows_lock_range_len,
            null,
            .FALSE,
            .fromBool(exclusive),
        )) {
            .SUCCESS => return syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INSUFFICIENT_RESOURCES => return syscall.fail(error.SystemResources),
            .LOCK_NOT_GRANTED => |err| return syscall.ntstatusBug(err), // passed FailImmediately=false
            .ACCESS_VIOLATION => |err| return syscall.ntstatusBug(err), // bad io_status_block pointer
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    const operation: i32 = switch (lock) {
        .none => posix.LOCK.UN,
        .shared => posix.LOCK.SH,
        .exclusive => posix.LOCK.EX,
    };
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.flock(file.handle, operation))) {
            .SUCCESS => return syscall.finish(),
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .BADF => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err), // invalid parameters
                    .NOLCK => return error.SystemResources,
                    .AGAIN => |err| return errnoBug(err),
                    .OPNOTSUPP => return error.FileLocksUnsupported,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileTryLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!bool {
    if (native_os == .wasi) return error.FileLocksUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        const exclusive = switch (lock) {
            .none => {
                // To match the non-Windows behavior, unlock
                var io_status_block: windows.IO_STATUS_BLOCK = undefined;
                while (true) switch (windows.ntdll.NtUnlockFile(
                    file.handle,
                    &io_status_block,
                    &windows_lock_range_off,
                    &windows_lock_range_len,
                    0,
                )) {
                    .SUCCESS => return true,
                    .CANCELLED => continue,
                    .RANGE_NOT_LOCKED => return false,
                    .ACCESS_VIOLATION => |err| return windows.statusBug(err), // bad io_status_block pointer
                    else => |status| return windows.unexpectedStatus(status),
                };
            },
            .shared => false,
            .exclusive => true,
        };
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtLockFile(
            file.handle,
            null,
            null,
            null,
            &io_status_block,
            &windows_lock_range_off,
            &windows_lock_range_len,
            null,
            .TRUE,
            .fromBool(exclusive),
        )) {
            .SUCCESS => {
                syscall.finish();
                return true;
            },
            .LOCK_NOT_GRANTED => {
                syscall.finish();
                return false;
            },
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INSUFFICIENT_RESOURCES => return syscall.fail(error.SystemResources),
            .ACCESS_VIOLATION => |err| return syscall.ntstatusBug(err), // bad io_status_block pointer
            else => |status| return syscall.unexpectedNtstatus(status),
        };
    }

    const operation: i32 = switch (lock) {
        .none => posix.LOCK.UN,
        .shared => posix.LOCK.SH | posix.LOCK.NB,
        .exclusive => posix.LOCK.EX | posix.LOCK.NB,
    };
    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.flock(file.handle, operation))) {
            .SUCCESS => {
                syscall.finish();
                return true;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .AGAIN => {
                syscall.finish();
                return false;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .BADF => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err), // invalid parameters
                    .NOLCK => return error.SystemResources,
                    .OPNOTSUPP => return error.FileLocksUnsupported,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileUnlock(userdata: ?*anyopaque, file: File) void {
    if (native_os == .wasi) return;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        while (true) switch (windows.ntdll.NtUnlockFile(
            file.handle,
            &io_status_block,
            &windows_lock_range_off,
            &windows_lock_range_len,
            0,
        )) {
            .SUCCESS => return,
            .CANCELLED => continue,
            .RANGE_NOT_LOCKED => if (is_debug) unreachable else return, // Function asserts unlocked.
            .ACCESS_VIOLATION => if (is_debug) unreachable else return, // bad io_status_block pointer
            else => if (is_debug) unreachable else return, // Resource deallocation must succeed.
        };
    }

    while (true) {
        switch (posix.errno(posix.system.flock(file.handle, posix.LOCK.UN))) {
            .SUCCESS => return,
            .CANCELED, .INTR => continue,
            .AGAIN => return assert(!is_debug), // unlocking can't block
            .BADF => return assert(!is_debug), // File descriptor used after closed.
            .INVAL => return assert(!is_debug), // invalid parameters
            .NOLCK => return assert(!is_debug), // Resource deallocation.
            .OPNOTSUPP => return assert(!is_debug), // We already got the lock.
            else => return assert(!is_debug), // Resource deallocation must succeed.
        }
    }
}

fn fileDowngradeLock(userdata: ?*anyopaque, file: File) File.DowngradeLockError!void {
    if (native_os == .wasi) return;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (is_windows) {
        // On Windows it works like a semaphore + exclusivity flag. To
        // implement this function, we first obtain another lock in shared
        // mode. This changes the exclusivity flag, but increments the
        // semaphore to 2. So we follow up with an NtUnlockFile which
        // decrements the semaphore but does not modify the exclusivity flag.
        var io_status_block: windows.IO_STATUS_BLOCK = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtLockFile(
            file.handle,
            null,
            null,
            null,
            &io_status_block,
            &windows_lock_range_off,
            &windows_lock_range_len,
            null,
            .TRUE,
            .FALSE,
        )) {
            .SUCCESS => break syscall.finish(),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .INSUFFICIENT_RESOURCES => |err| return syscall.ntstatusBug(err),
            .LOCK_NOT_GRANTED => |err| return syscall.ntstatusBug(err), // File was not locked in exclusive mode.
            .ACCESS_VIOLATION => |err| return syscall.ntstatusBug(err), // bad io_status_block pointer
            else => |status| return syscall.unexpectedNtstatus(status),
        };
        while (true) switch (windows.ntdll.NtUnlockFile(
            file.handle,
            &io_status_block,
            &windows_lock_range_off,
            &windows_lock_range_len,
            0,
        )) {
            .SUCCESS => return,
            .CANCELLED => continue,
            .RANGE_NOT_LOCKED => if (is_debug) unreachable else return, // File was not locked.
            .ACCESS_VIOLATION => if (is_debug) unreachable else return, // bad io_status_block pointer
            else => if (is_debug) unreachable else return, // Resource deallocation must succeed.
        };
    }

    const operation = posix.LOCK.SH | posix.LOCK.NB;

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.flock(file.handle, operation))) {
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
                    .AGAIN => |err| return errnoBug(err), // File was not locked in exclusive mode.
                    .BADF => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err), // invalid parameters
                    .NOLCK => |err| return errnoBug(err), // Lock already obtained.
                    .OPNOTSUPP => |err| return errnoBug(err), // Lock already obtained.
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirOpenDirWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.OpenOptions,
) Dir.OpenError!Dir {
    if (builtin.link_libc) return dirOpenDirPosix(userdata, dir, sub_path, options);
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const wasi = std.os.wasi;

    var base: std.os.wasi.rights_t = .{
        .FD_FILESTAT_GET = true,
        .FD_FDSTAT_SET_FLAGS = true,
        .FD_FILESTAT_SET_TIMES = true,
    };
    if (options.access_sub_paths) {
        base.FD_READDIR = true;
        base.PATH_CREATE_DIRECTORY = true;
        base.PATH_CREATE_FILE = true;
        base.PATH_LINK_SOURCE = true;
        base.PATH_LINK_TARGET = true;
        base.PATH_OPEN = true;
        base.PATH_READLINK = true;
        base.PATH_RENAME_SOURCE = true;
        base.PATH_RENAME_TARGET = true;
        base.PATH_FILESTAT_GET = true;
        base.PATH_FILESTAT_SET_SIZE = true;
        base.PATH_FILESTAT_SET_TIMES = true;
        base.PATH_SYMLINK = true;
        base.PATH_REMOVE_DIRECTORY = true;
        base.PATH_UNLINK_FILE = true;
    }

    const lookup_flags: wasi.lookupflags_t = .{ .SYMLINK_FOLLOW = options.follow_symlinks };
    const oflags: wasi.oflags_t = .{ .DIRECTORY = true };
    const fdflags: wasi.fdflags_t = .{};
    var fd: posix.fd_t = undefined;
    const syscall: Syscall = try .start();
    while (true) {
        switch (wasi.path_open(dir.handle, lookup_flags, sub_path.ptr, sub_path.len, oflags, base, base, fdflags, &fd)) {
            .SUCCESS => {
                syscall.finish();
                return .{ .handle = fd };
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
                    .LOOP => return error.SymLinkLoop,
                    .MFILE => return error.ProcessFdQuotaExceeded,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NFILE => return error.SystemFdQuotaExceeded,
                    .NODEV => return error.NoDevice,
                    .NOENT => return error.FileNotFound,
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => return error.NotDir,
                    .PERM => return error.PermissionDenied,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirHardLink(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
    options: Dir.HardLinkOptions,
) Dir.HardLinkError!void {
    if (is_windows) return error.OperationUnsupported;
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (native_os == .wasi and !builtin.link_libc) {
        const flags: std.os.wasi.lookupflags_t = .{
            .SYMLINK_FOLLOW = options.follow_symlinks,
        };
        const syscall: Syscall = try .start();
        while (true) {
            switch (std.os.wasi.path_link(
                old_dir.handle,
                flags,
                old_sub_path.ptr,
                old_sub_path.len,
                new_dir.handle,
                new_sub_path.ptr,
                new_sub_path.len,
            )) {
                .SUCCESS => return syscall.finish(),
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .ACCES => return error.AccessDenied,
                        .DQUOT => return error.DiskQuota,
                        .EXIST => return error.PathAlreadyExists,
                        .FAULT => |err| return errnoBug(err),
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
                        .INVAL => |err| return errnoBug(err),
                        .ILSEQ => return error.BadPathName,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    var old_path_buffer: [posix.PATH_MAX]u8 = undefined;
    var new_path_buffer: [posix.PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    const flags: u32 = if (options.follow_symlinks) posix.AT.SYMLINK_FOLLOW else 0;
    return linkat(old_dir.handle, old_sub_path_posix, new_dir.handle, new_sub_path_posix, flags);
}

fn fileClose(userdata: ?*anyopaque, files: []const File) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    for (files) |file| {
        if (is_windows) {
            windows.CloseHandle(file.handle);
        } else {
            closeFd(file.handle);
        }
    }
}

fn fileReadStreaming(userdata: ?*anyopaque, file: File, data: []const []u8) File.ReadStreamingError!usize {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    if (is_windows) return fileReadStreamingWindows(file, data);
    return fileReadStreamingPosix(file, data);
}

fn fileReadStreamingPosix(file: File, data: []const []u8) File.ReadStreamingError!usize {
    var iovecs_buffer: [max_iovecs_len]posix.iovec = undefined;
    var i: usize = 0;
    for (data) |buf| {
        if (iovecs_buffer.len - i == 0) break;
        if (buf.len != 0) {
            iovecs_buffer[i] = .{ .base = buf.ptr, .len = buf.len };
            i += 1;
        }
    }
    if (i == 0) return 0;
    const dest = iovecs_buffer[0..i];
    assert(dest[0].len > 0);

    if (native_os == .wasi and !builtin.link_libc) {
        const syscall: Syscall = try .start();
        while (true) {
            var nread: usize = undefined;
            switch (std.os.wasi.fd_read(file.handle, dest.ptr, dest.len, &nread)) {
                .SUCCESS => {
                    syscall.finish();
                    if (nread == 0) return error.EndOfStream;
                    return nread;
                },
                .INTR, .TIMEDOUT => {
                    try syscall.checkCancel();
                    continue;
                },
                .BADF => return syscall.fail(error.IsDir), // File operation on directory.
                .IO => return syscall.fail(error.InputOutput),
                .ISDIR => return syscall.fail(error.IsDir),
                .NOBUFS => return syscall.fail(error.SystemResources),
                .NOMEM => return syscall.fail(error.SystemResources),
                .NOTCONN => return syscall.fail(error.SocketUnconnected),
                .CONNRESET => return syscall.fail(error.ConnectionResetByPeer),
                .NOTCAPABLE => return syscall.fail(error.AccessDenied),
                .INVAL => |err| return syscall.errnoBug(err),
                .FAULT => |err| return syscall.errnoBug(err),
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.system.readv(file.handle, dest.ptr, @intCast(dest.len));
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                if (rc == 0) return error.EndOfStream;
                return @intCast(rc);
            },
            .INTR, .TIMEDOUT => {
                try syscall.checkCancel();
                continue;
            },
            .BADF => {
                syscall.finish();
                if (native_os == .wasi) return error.IsDir; // File operation on directory.
                return error.NotOpenForReading;
            },
            .AGAIN => return syscall.fail(error.WouldBlock),
            .IO => return syscall.fail(error.InputOutput),
            .ISDIR => return syscall.fail(error.IsDir),
            .NOBUFS => return syscall.fail(error.SystemResources),
            .NOMEM => return syscall.fail(error.SystemResources),
            .NOTCONN => return syscall.fail(error.SocketUnconnected),
            .CONNRESET => return syscall.fail(error.ConnectionResetByPeer),
            .INVAL => |err| return syscall.errnoBug(err),
            .FAULT => |err| return syscall.errnoBug(err),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn fileReadStreamingWindows(file: File, data: []const []u8) File.ReadStreamingError!usize {
    var iosb: windows.IO_STATUS_BLOCK = undefined;
    var index: usize = 0;
    while (data.len - index != 0 and data[index].len == 0) index += 1;
    if (data.len - index == 0) return 0;
    const buffer = data[index];
    const short_buffer_len = std.math.lossyCast(u32, buffer.len);
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
            null, // byte offset
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
            null, // byte offset
            null, // key
        )) {
            .PENDING => unreachable, // unrecoverable: wrong File nonblocking flag
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            else => |status| {
                syscall.finish();
                iosb.u.Status = status;
                break;
            },
        };
    }
    return ntReadFileResult(&iosb);
}

fn flagApc(userdata: ?*anyopaque, _: *windows.IO_STATUS_BLOCK, _: windows.ULONG) align(apc_align) callconv(.winapi) void {
    const flag: *bool = @ptrCast(userdata);
    flag.* = true;
}

fn ntReadFileResult(io_status_block: *const windows.IO_STATUS_BLOCK) !usize {
    switch (io_status_block.u.Status) {
        .PENDING => unreachable,
        .CANCELLED => unreachable,
        .SUCCESS => return io_status_block.Information,
        .END_OF_FILE, .PIPE_BROKEN => return error.EndOfStream,
        .INVALID_HANDLE => return error.NotOpenForReading,
        .INVALID_DEVICE_REQUEST => return error.IsDir,
        .FILE_LOCK_CONFLICT => return error.LockViolation,
        .ACCESS_DENIED => return error.AccessDenied,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn ntWriteFileResult(io_status_block: *const windows.IO_STATUS_BLOCK) !usize {
    switch (io_status_block.u.Status) {
        .PENDING => unreachable,
        .CANCELLED => unreachable,
        .SUCCESS => return io_status_block.Information,
        .INVALID_USER_BUFFER => return error.SystemResources,
        .NO_MEMORY => return error.SystemResources,
        .QUOTA_EXCEEDED => return error.SystemResources,
        .PIPE_BROKEN => return error.BrokenPipe,
        .INVALID_HANDLE => return error.NotOpenForWriting,
        .FILE_LOCK_CONFLICT => return error.LockViolation,
        .ACCESS_DENIED => return error.AccessDenied,
        .WORKING_SET_QUOTA => return error.SystemResources,
        .DISK_FULL => return error.NoSpaceLeft,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn fileReadPositionalPosix(file: File, data: []const []u8, offset: u64) File.ReadPositionalError!usize {
    var iovecs_buffer: [max_iovecs_len]posix.iovec = undefined;
    var i: usize = 0;
    for (data) |buf| {
        if (iovecs_buffer.len - i == 0) break;
        if (buf.len != 0) {
            iovecs_buffer[i] = .{ .base = buf.ptr, .len = buf.len };
            i += 1;
        }
    }
    if (i == 0) return 0;
    const dest = iovecs_buffer[0..i];
    assert(dest[0].len > 0);

    if (native_os == .wasi and !builtin.link_libc) {
        const syscall: Syscall = try .start();
        while (true) {
            var nread: usize = undefined;
            switch (std.os.wasi.fd_pread(file.handle, dest.ptr, dest.len, offset, &nread)) {
                .SUCCESS => {
                    syscall.finish();
                    return nread;
                },
                .INTR, .TIMEDOUT => {
                    try syscall.checkCancel();
                    continue;
                },
                .NOTCONN => |err| return syscall.errnoBug(err), // not a socket
                .CONNRESET => |err| return syscall.errnoBug(err), // not a socket
                .INVAL => |err| return syscall.errnoBug(err),
                .FAULT => |err| return syscall.errnoBug(err), // segmentation fault
                .AGAIN => |err| return syscall.errnoBug(err),
                .IO => return syscall.fail(error.InputOutput),
                .ISDIR => return syscall.fail(error.IsDir),
                .BADF => return syscall.fail(error.IsDir),
                .NOBUFS => return syscall.fail(error.SystemResources),
                .NOMEM => return syscall.fail(error.SystemResources),
                .NXIO => return syscall.fail(error.Unseekable),
                .SPIPE => return syscall.fail(error.Unseekable),
                .OVERFLOW => return syscall.fail(error.Unseekable),
                .NOTCAPABLE => return syscall.fail(error.AccessDenied),
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    }

    if (have_preadv) {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = preadv_sym(file.handle, dest.ptr, @intCast(dest.len), @bitCast(offset));
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    return @bitCast(rc);
                },
                .INTR, .TIMEDOUT => {
                    try syscall.checkCancel();
                    continue;
                },
                .NXIO => return syscall.fail(error.Unseekable),
                .SPIPE => return syscall.fail(error.Unseekable),
                .OVERFLOW => return syscall.fail(error.Unseekable),
                .NOBUFS => return syscall.fail(error.SystemResources),
                .NOMEM => return syscall.fail(error.SystemResources),
                .AGAIN => return syscall.fail(error.WouldBlock),
                .IO => return syscall.fail(error.InputOutput),
                .ISDIR => return syscall.fail(error.IsDir),
                .NOTCONN => |err| return syscall.errnoBug(err), // not a socket
                .CONNRESET => |err| return syscall.errnoBug(err), // not a socket
                .INVAL => |err| return syscall.errnoBug(err),
                .FAULT => |err| return syscall.errnoBug(err),
                .BADF => {
                    syscall.finish();
                    if (native_os == .wasi) return error.IsDir; // File operation on directory.
                    return error.NotOpenForReading;
                },
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    }

    const syscall: Syscall = try .start();
    while (true) {
        const rc = posix.pread(file.handle, dest[0].ptr, @intCast(dest[0].len), @bitCast(offset));
        switch (posix.errno(rc)) {
            .SUCCESS => {
                syscall.finish();
                return @bitCast(rc);
            },
            .INTR, .TIMEDOUT => {
                try syscall.checkCancel();
                continue;
            },
            .NXIO => return syscall.fail(error.Unseekable),
            .SPIPE => return syscall.fail(error.Unseekable),
            .OVERFLOW => return syscall.fail(error.Unseekable),
            .NOBUFS => return syscall.fail(error.SystemResources),
            .NOMEM => return syscall.fail(error.SystemResources),
            .AGAIN => return syscall.fail(error.WouldBlock),
            .IO => return syscall.fail(error.InputOutput),
            .ISDIR => return syscall.fail(error.IsDir),
            .NOTCONN => |err| return syscall.errnoBug(err), // not a socket
            .CONNRESET => |err| return syscall.errnoBug(err), // not a socket
            .INVAL => |err| return syscall.errnoBug(err),
            .FAULT => |err| return syscall.errnoBug(err),
            .BADF => {
                syscall.finish();
                if (native_os == .wasi) return error.IsDir; // File operation on directory.
                return error.NotOpenForReading;
            },
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn fileReadPo
```
