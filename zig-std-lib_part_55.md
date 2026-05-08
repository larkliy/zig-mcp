```
s may be opened, but `error.IsDir` is still
    /// possible in certain scenarios, e.g. attempting to open a directory with
    /// write permissions.
    ///
    /// If set to false, `error.IsDir` will always be returned when opening a directory.
    ///
    /// When set to false:
    /// * On Windows, the behavior is implemented without any extra syscalls.
    /// * On other operating systems, the behavior is implemented with an additional
    ///   `fstat` syscall.
    allow_directory: bool = true,
    /// Indicates intent for only some operations to be performed on this
    /// opened file:
    /// * `close`
    /// * `stat`
    /// On Linux and FreeBSD, this corresponds to `std.posix.O.PATH`.
    path_only: bool = false,
    /// Open the file with an advisory lock to coordinate with other processes
    /// accessing it at the same time. An exclusive lock will prevent other
    /// processes from acquiring a lock. A shared lock will prevent other
    /// processes from acquiring a exclusive lock, but does not prevent
    /// other process from getting their own shared locks.
    ///
    /// The lock is advisory, except on Linux in very specific circumstances[1].
    /// This means that a process that does not respect the locking API can still get access
    /// to the file, despite the lock.
    ///
    /// On these operating systems, the lock is acquired atomically with
    /// opening the file:
    /// * Darwin
    /// * DragonFlyBSD
    /// * FreeBSD
    /// * Haiku
    /// * NetBSD
    /// * OpenBSD
    /// On these operating systems, the lock is acquired via a separate syscall
    /// after opening the file:
    /// * Linux
    /// * Windows
    ///
    /// [1]: https://www.kernel.org/doc/Documentation/filesystems/mandatory-locking.txt
    lock: File.Lock = .none,
    /// Sets whether or not to wait until the file is locked to return. If set to true,
    /// `error.WouldBlock` will be returned. Otherwise, the file will wait until the file
    /// is available to proceed.
    lock_nonblocking: bool = false,
    /// Set this to allow the opened file to automatically become the
    /// controlling TTY for the current process.
    allow_ctty: bool = false,
    follow_symlinks: bool = true,
    /// If supported by the operating system, attempted path resolution that
    /// would escape the directory instead returns `error.AccessDenied`. If
    /// unsupported, this option is ignored.
    resolve_beneath: bool = false,

    pub const Mode = enum { read_only, write_only, read_write };

    pub fn isRead(self: OpenFileOptions) bool {
        return self.mode != .write_only;
    }

    pub fn isWrite(self: OpenFileOptions) bool {
        return self.mode != .read_only;
    }
};

/// Opens a file for reading or writing, without attempting to create a new file.
///
/// To create a new file, see `createFile`.
///
/// Allocates a resource to be released with `File.close`.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn openFile(dir: Dir, io: Io, sub_path: []const u8, options: OpenFileOptions) File.OpenError!File {
    return io.vtable.dirOpenFile(io.userdata, dir, sub_path, options);
}

pub fn openFileAbsolute(io: Io, absolute_path: []const u8, options: OpenFileOptions) File.OpenError!File {
    assert(path.isAbsolute(absolute_path));
    return openFile(.cwd(), io, absolute_path, options);
}

pub const CreateFileOptions = struct {
    /// Whether the file will be created with read access.
    read: bool = false,
    /// If the file already exists, and is a regular file, and the access
    /// mode allows writing, it will be truncated to length 0.
    truncate: bool = true,
    /// Ensures that this open call creates the file, otherwise causes
    /// `error.PathAlreadyExists` to be returned.
    exclusive: bool = false,
    /// Open the file with an advisory lock to coordinate with other processes
    /// accessing it at the same time. An exclusive lock will prevent other
    /// processes from acquiring a lock. A shared lock will prevent other
    /// processes from acquiring a exclusive lock, but does not prevent
    /// other process from getting their own shared locks.
    ///
    /// The lock is advisory, except on Linux in very specific circumstances[1].
    /// This means that a process that does not respect the locking API can still get access
    /// to the file, despite the lock.
    ///
    /// On these operating systems, the lock is acquired atomically with
    /// opening the file:
    /// * Darwin
    /// * DragonFlyBSD
    /// * FreeBSD
    /// * Haiku
    /// * NetBSD
    /// * OpenBSD
    /// On these operating systems, the lock is acquired via a separate syscall
    /// after opening the file:
    /// * Linux
    /// * Windows
    ///
    /// [1]: https://www.kernel.org/doc/Documentation/filesystems/mandatory-locking.txt
    lock: File.Lock = .none,
    /// Sets whether or not to wait until the file is locked to return. If set to true,
    /// `error.WouldBlock` will be returned. Otherwise, the file will wait until the file
    /// is available to proceed.
    lock_nonblocking: bool = false,
    permissions: Permissions = .default_file,
    /// If supported by the operating system, attempted path resolution that
    /// would escape the directory instead returns `error.AccessDenied`. If
    /// unsupported, this option is ignored.
    resolve_beneath: bool = false,
};

/// Creates, opens, or overwrites a file with write access.
///
/// Allocates a resource to be dellocated with `File.close`.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn createFile(dir: Dir, io: Io, sub_path: []const u8, flags: CreateFileOptions) File.OpenError!File {
    return io.vtable.dirCreateFile(io.userdata, dir, sub_path, flags);
}

pub fn createFileAbsolute(io: Io, absolute_path: []const u8, flags: CreateFileOptions) File.OpenError!File {
    return createFile(.cwd(), io, absolute_path, flags);
}

pub const WriteFileOptions = struct {
    /// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
    /// On WASI, `sub_path` should be encoded as valid UTF-8.
    /// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
    sub_path: []const u8,
    data: []const u8,
    flags: CreateFileOptions = .{},
};

pub const WriteFileError = File.Writer.Error || File.OpenError;

/// Writes content to the file system, using the file creation flags provided.
pub fn writeFile(dir: Dir, io: Io, options: WriteFileOptions) WriteFileError!void {
    var file = try dir.createFile(io, options.sub_path, options.flags);
    defer file.close(io);
    try file.writeStreamingAll(io, options.data);
}

pub const PrevStatus = enum {
    stale,
    fresh,
};

pub const UpdateFileError = File.OpenError;

/// Check the file size, mtime, and permissions of `source_path` and `dest_path`. If
/// they are equal, does nothing. Otherwise, atomically copies `source_path` to
/// `dest_path`, creating the parent directory hierarchy as needed. The
/// destination file gains the mtime, atime, and permissions of the source file so
/// that the next call to `updateFile` will not need a copy.
///
/// Returns the previous status of the file before updating.
///
/// * On Windows, both paths should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// * On WASI, both paths should be encoded as valid UTF-8.
/// * On other platforms, both paths are an opaque sequence of bytes with no particular encoding.
pub fn updateFile(
    source_dir: Dir,
    io: Io,
    source_path: []const u8,
    dest_dir: Dir,
    /// If directories in this path do not exist, they are created.
    dest_path: []const u8,
    options: CopyFileOptions,
) !PrevStatus {
    var src_file = try source_dir.openFile(io, source_path, .{});
    defer src_file.close(io);

    const src_stat = try src_file.stat(io);
    const actual_permissions = options.permissions orelse src_stat.permissions;
    check_dest_stat: {
        const dest_stat = blk: {
            var dest_file = dest_dir.openFile(io, dest_path, .{}) catch |err| switch (err) {
                error.FileNotFound => break :check_dest_stat,
                else => |e| return e,
            };
            defer dest_file.close(io);

            break :blk try dest_file.stat(io);
        };

        if (src_stat.size == dest_stat.size and
            src_stat.mtime.nanoseconds == dest_stat.mtime.nanoseconds and
            actual_permissions == dest_stat.permissions)
        {
            return .fresh;
        }
    }

    var atomic_file = try dest_dir.createFileAtomic(io, dest_path, .{
        .permissions = actual_permissions,
        .make_path = true,
        .replace = true,
    });
    defer atomic_file.deinit(io);

    var buffer: [1024]u8 = undefined; // Used only when direct fd-to-fd is not available.
    var file_writer = atomic_file.file.writer(io, &buffer);

    var src_reader: File.Reader = .initSize(src_file, io, &.{}, src_stat.size);
    const dest_writer = &file_writer.interface;

    _ = dest_writer.sendFileAll(&src_reader, .unlimited) catch |err| switch (err) {
        error.ReadFailed => return src_reader.err.?,
        error.WriteFailed => return file_writer.err.?,
    };
    try file_writer.flush();
    try file_writer.file.setTimestamps(io, .{
        .access_timestamp = .init(src_stat.atime),
        .modify_timestamp = .init(src_stat.mtime),
    });
    try atomic_file.replace(io);
    return .stale;
}

pub const ReadFileError = File.OpenError || File.Reader.Error;

/// Read all of file contents using a preallocated buffer.
///
/// The returned slice has the same pointer as `buffer`. If the length matches `buffer.len`
/// the situation is ambiguous. It could either mean that the entire file was read, and
/// it exactly fits the buffer, or it could mean the buffer was not big enough for the
/// entire file.
///
/// * On Windows, `file_path` should be encoded as [WTF-8](https://simonsapin.github.io/wtf-8/).
/// * On WASI, `file_path` should be encoded as valid UTF-8.
/// * On other platforms, `file_path` is an opaque sequence of bytes with no particular encoding.
pub fn readFile(dir: Dir, io: Io, file_path: []const u8, buffer: []u8) ReadFileError![]u8 {
    var file = try dir.openFile(io, file_path, .{
        // We can take advantage of this on Windows since it doesn't involve any extra syscalls,
        // so we can get error.IsDir during open rather than during the read.
        .allow_directory = if (native_os == .windows) false else true,
    });
    defer file.close(io);

    var reader = file.reader(io, &.{});
    const n = reader.interface.readSliceShort(buffer) catch |err| switch (err) {
        error.ReadFailed => return reader.err.?,
    };

    return buffer[0..n];
}

pub const CreateDirError = error{
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to create a new directory relative to it.
    AccessDenied,
    PermissionDenied,
    DiskQuota,
    PathAlreadyExists,
    SymLinkLoop,
    LinkQuotaExceeded,
    FileNotFound,
    SystemResources,
    NoSpaceLeft,
    NotDir,
    ReadOnlyFileSystem,
    NoDevice,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Creates a single directory with a relative or absolute path.
///
/// * On Windows, `sub_path` should be encoded as [WTF-8](https://simonsapin.github.io/wtf-8/).
/// * On WASI, `sub_path` should be encoded as valid UTF-8.
/// * On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
///
/// Related:
/// * `createDirPath`
/// * `createDirAbsolute`
pub fn createDir(dir: Dir, io: Io, sub_path: []const u8, permissions: Permissions) CreateDirError!void {
    return io.vtable.dirCreateDir(io.userdata, dir, sub_path, permissions);
}

/// Create a new directory, based on an absolute path.
///
/// Asserts that the path is absolute. See `createDir` for a function that
/// operates on both absolute and relative paths.
///
/// On Windows, `absolute_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `absolute_path` should be encoded as valid UTF-8.
/// On other platforms, `absolute_path` is an opaque sequence of bytes with no particular encoding.
pub fn createDirAbsolute(io: Io, absolute_path: []const u8, permissions: Permissions) CreateDirError!void {
    assert(path.isAbsolute(absolute_path));
    return createDir(.cwd(), io, absolute_path, permissions);
}

test createDirAbsolute {}

pub const CreateDirPathError = CreateDirError || StatFileError;

/// Creates parent directories with default permissions as necessary to ensure
/// `sub_path` exists as a directory.
///
/// Returns success if the path already exists and is a directory.
///
/// This function may not be atomic. If it returns an error, the file system
/// may have been modified.
///
/// Fails on an empty path with `error.BadPathName` as that is not a path that
/// can be created.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://simonsapin.github.io/wtf-8/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
///
/// Paths containing `..` components are handled differently depending on the platform:
/// - On Windows, `..` are resolved before the path is passed to NtCreateFile, meaning
///   a `sub_path` like "first/../second" will resolve to "second" and only a
///   `./second` directory will be created.
/// - On other platforms, `..` are not resolved before the path is passed to `mkdirat`,
///   meaning a `sub_path` like "first/../second" will create both a `./first`
///   and a `./second` directory.
///
/// See also:
/// * `createDirPathStatus`
pub fn createDirPath(dir: Dir, io: Io, sub_path: []const u8) CreateDirPathError!void {
    _ = try io.vtable.dirCreateDirPath(io.userdata, dir, sub_path, .default_dir);
}

pub const CreatePathStatus = enum { existed, created };

/// Same as `createDirPath` except returns whether the path already existed or was
/// successfully created.
pub fn createDirPathStatus(dir: Dir, io: Io, sub_path: []const u8, permissions: Permissions) CreateDirPathError!CreatePathStatus {
    return io.vtable.dirCreateDirPath(io.userdata, dir, sub_path, permissions);
}

pub const CreateDirPathOpenError = CreateDirError || OpenError || StatFileError;

pub const CreateDirPathOpenOptions = struct {
    open_options: OpenOptions = .{},
    permissions: Permissions = .default_dir,
};

/// Performs the equivalent of `createDirPath` followed by `openDir`, atomically if possible.
///
/// When this operation is canceled, it may leave the file system in a
/// partially modified state.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn createDirPathOpen(dir: Dir, io: Io, sub_path: []const u8, options: CreateDirPathOpenOptions) CreateDirPathOpenError!Dir {
    return io.vtable.dirCreateDirPathOpen(io.userdata, dir, sub_path, options.permissions, options.open_options);
}

pub const Stat = File.Stat;
pub const StatError = File.StatError;

pub fn stat(dir: Dir, io: Io) StatError!Stat {
    return io.vtable.dirStat(io.userdata, dir);
}

pub const StatFileError = File.OpenError || File.StatError;

pub const StatFileOptions = struct {
    follow_symlinks: bool = true,
};

/// Returns metadata for a file inside the directory.
///
/// On Windows, this requires three syscalls. On other operating systems, it
/// only takes one.
///
/// Symlinks are followed.
///
/// `sub_path` may be absolute, in which case `self` is ignored.
///
/// * On Windows, `sub_path` should be encoded as [WTF-8](https://simonsapin.github.io/wtf-8/).
/// * On WASI, `sub_path` should be encoded as valid UTF-8.
/// * On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn statFile(dir: Dir, io: Io, sub_path: []const u8, options: StatFileOptions) StatFileError!Stat {
    return io.vtable.dirStatFile(io.userdata, dir, sub_path, options);
}

pub const RealPathError = File.RealPathError;

/// Obtains the canonicalized absolute path name of `sub_path` relative to this
/// `Dir`. If `sub_path` is absolute, ignores this `Dir` handle and obtains the
/// canonicalized absolute pathname of `sub_path` argument.
///
/// This function has limited platform support, and using it can lead to
/// unnecessary failures and race conditions. It is generally advisable to
/// avoid this function entirely.
pub fn realPath(dir: Dir, io: Io, out_buffer: []u8) RealPathError!usize {
    return io.vtable.dirRealPath(io.userdata, dir, out_buffer);
}

pub const RealPathFileError = RealPathError || PathNameError;

/// Obtains the canonicalized absolute path name of `sub_path` relative to this
/// `Dir`. If `sub_path` is absolute, ignores this `Dir` handle and obtains the
/// canonicalized absolute pathname of `sub_path` argument.
///
/// This function has limited platform support, and using it can lead to
/// unnecessary failures and race conditions. It is generally advisable to
/// avoid this function entirely.
///
/// See also:
/// * `realPathFileAlloc`.
/// * `realPathFileAbsolute`.
pub fn realPathFile(dir: Dir, io: Io, sub_path: []const u8, out_buffer: []u8) RealPathFileError!usize {
    return io.vtable.dirRealPathFile(io.userdata, dir, sub_path, out_buffer);
}

pub const RealPathFileAllocError = RealPathFileError || Allocator.Error;

/// Same as `realPathFile` except allocates result.
///
/// This function has limited platform support, and using it can lead to
/// unnecessary failures and race conditions. It is generally advisable to
/// avoid this function entirely.
///
/// See also:
/// * `realPathFile`.
/// * `realPathFileAbsolute`.
pub fn realPathFileAlloc(dir: Dir, io: Io, sub_path: []const u8, allocator: Allocator) RealPathFileAllocError![:0]u8 {
    var buffer: [max_path_bytes]u8 = undefined;
    const n = try realPathFile(dir, io, sub_path, &buffer);
    return allocator.dupeZ(u8, buffer[0..n]);
}

/// Same as `realPathFile` except `absolute_path` is asserted to be an absolute
/// path.
///
/// This function has limited platform support, and using it can lead to
/// unnecessary failures and race conditions. It is generally advisable to
/// avoid this function entirely.
///
/// See also:
/// * `realPathFile`.
/// * `realPathFileAlloc`.
pub fn realPathFileAbsolute(io: Io, absolute_path: []const u8, out_buffer: []u8) RealPathFileError!usize {
    assert(path.isAbsolute(absolute_path));
    return io.vtable.dirRealPathFile(io.userdata, .cwd(), absolute_path, out_buffer);
}

/// Same as `realPathFileAbsolute` except allocates result.
///
/// This function has limited platform support, and using it can lead to
/// unnecessary failures and race conditions. It is generally advisable to
/// avoid this function entirely.
///
/// See also:
/// * `realPathFileAbsolute`.
/// * `realPathFile`.
pub fn realPathFileAbsoluteAlloc(io: Io, absolute_path: []const u8, allocator: Allocator) RealPathFileAllocError![:0]u8 {
    var buffer: [max_path_bytes]u8 = undefined;
    const n = try realPathFileAbsolute(io, absolute_path, &buffer);
    return allocator.dupeZ(u8, buffer[0..n]);
}

pub const DeleteFileError = error{
    FileNotFound,
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to unlink a resource by path relative to it.
    AccessDenied,
    PermissionDenied,
    FileBusy,
    FileSystem,
    IsDir,
    SymLinkLoop,
    NotDir,
    SystemResources,
    ReadOnlyFileSystem,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Delete a file name and possibly the file it refers to, based on an open directory handle.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
///
/// Asserts that the path parameter has no null bytes.
pub fn deleteFile(dir: Dir, io: Io, sub_path: []const u8) DeleteFileError!void {
    return io.vtable.dirDeleteFile(io.userdata, dir, sub_path);
}

pub fn deleteFileAbsolute(io: Io, absolute_path: []const u8) DeleteFileError!void {
    assert(path.isAbsolute(absolute_path));
    return deleteFile(.cwd(), io, absolute_path);
}

test deleteFileAbsolute {}

pub const DeleteDirError = error{
    DirNotEmpty,
    FileNotFound,
    AccessDenied,
    PermissionDenied,
    FileBusy,
    FileSystem,
    SymLinkLoop,
    NotDir,
    SystemResources,
    ReadOnlyFileSystem,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Returns `error.DirNotEmpty` if the directory is not empty.
///
/// To delete a directory recursively, see `deleteTree`.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn deleteDir(dir: Dir, io: Io, sub_path: []const u8) DeleteDirError!void {
    return io.vtable.dirDeleteDir(io.userdata, dir, sub_path);
}

/// Same as `deleteDir` except the path is absolute.
///
/// On Windows, `dir_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `dir_path` should be encoded as valid UTF-8.
/// On other platforms, `dir_path` is an opaque sequence of bytes with no particular encoding.
pub fn deleteDirAbsolute(io: Io, absolute_path: []const u8) DeleteDirError!void {
    assert(path.isAbsolute(absolute_path));
    return deleteDir(.cwd(), io, absolute_path);
}

pub const RenameError = error{
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to rename a resource by path relative to it.
    AccessDenied,
    /// Attempted to replace a nonempty directory.
    DirNotEmpty,
    PermissionDenied,
    /// The file attempted to be moved or replaced is a running executable.
    FileBusy,
    DiskQuota,
    IsDir,
    SymLinkLoop,
    LinkQuotaExceeded,
    FileNotFound,
    NotDir,
    SystemResources,
    NoSpaceLeft,
    ReadOnlyFileSystem,
    CrossDevice,
    NoDevice,
    PipeBusy,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
    /// On Windows, antivirus software is enabled by default. It can be
    /// disabled, but Windows Update sometimes ignores the user's preference
    /// and re-enables it. When enabled, antivirus software on Windows
    /// intercepts file system operations and makes them significantly slower
    /// in addition to possibly failing with this error code.
    AntivirusInterference,
    HardwareFailure,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Change the name or location of a file or directory.
///
/// If `new_sub_path` already exists, it will be replaced.
///
/// Renaming a file over an existing directory or a directory over an existing
/// file will fail with `error.IsDir` or `error.NotDir`
///
/// * On Windows, both paths should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// * On WASI, both paths should be encoded as valid UTF-8.
/// * On other platforms, both paths are an opaque sequence of bytes with no particular encoding.
pub fn rename(
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
    io: Io,
) RenameError!void {
    return io.vtable.dirRename(io.userdata, old_dir, old_sub_path, new_dir, new_sub_path);
}

pub fn renameAbsolute(old_path: []const u8, new_path: []const u8, io: Io) RenameError!void {
    assert(path.isAbsolute(old_path));
    assert(path.isAbsolute(new_path));
    const my_cwd = cwd();
    return io.vtable.dirRename(io.userdata, my_cwd, old_path, my_cwd, new_path);
}

pub const RenamePreserveError = error{
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to rename a resource by path relative to it.
    ///
    /// On Windows, this error may be returned instead of PathAlreadyExists when
    /// renaming a directory over an existing directory.
    AccessDenied,
    PathAlreadyExists,
    /// Operating system or file system does not support atomic nonreplacing
    /// rename.
    OperationUnsupported,
} || RenameError;

/// Change the name or location of a file or directory.
///
/// If `new_sub_path` already exists, `error.PathAlreadyExists` will be returned.
///
/// Renaming a file over an existing directory or a directory over an existing
/// file will fail with `error.IsDir` or `error.NotDir`
///
/// * On Windows, both paths should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// * On WASI, both paths should be encoded as valid UTF-8.
/// * On other platforms, both paths are an opaque sequence of bytes with no particular encoding.
pub fn renamePreserve(
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
    io: Io,
) RenamePreserveError!void {
    return io.vtable.dirRenamePreserve(io.userdata, old_dir, old_sub_path, new_dir, new_sub_path);
}

pub const HardLinkOptions = File.HardLinkOptions;

pub const HardLinkError = File.HardLinkError;

pub fn hardLink(
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
    io: Io,
    options: HardLinkOptions,
) HardLinkError!void {
    return io.vtable.dirHardLink(io.userdata, old_dir, old_sub_path, new_dir, new_sub_path, options);
}

/// Use with `symLink`, `symLinkAtomic`, and `symLinkAbsolute` to
/// specify whether the symlink will point to a file or a directory. This value
/// is ignored on all hosts except Windows where creating symlinks to different
/// resource types, requires different flags. By default, `symLinkAbsolute` is
/// assumed to point to a file.
pub const SymLinkFlags = struct {
    is_directory: bool = false,
};

pub const SymLinkError = error{
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to create a new symbolic link relative to it.
    AccessDenied,
    PermissionDenied,
    DiskQuota,
    PathAlreadyExists,
    FileSystem,
    SymLinkLoop,
    FileNotFound,
    SystemResources,
    NoSpaceLeft,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
    ReadOnlyFileSystem,
    NotDir,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Creates a symbolic link named `sym_link_path` which contains the string `target_path`.
///
/// A symbolic link (also known as a soft link) may point to an existing file or to a nonexistent
/// one; the latter case is known as a dangling link.
///
/// If `sym_link_path` exists, it will not be overwritten.
///
/// On Windows, both paths should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, both paths should be encoded as valid UTF-8.
/// On other platforms, both paths are an opaque sequence of bytes with no particular encoding.
pub fn symLink(
    dir: Dir,
    io: Io,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: SymLinkFlags,
) SymLinkError!void {
    return io.vtable.dirSymLink(io.userdata, dir, target_path, sym_link_path, flags);
}

pub fn symLinkAbsolute(
    io: Io,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: SymLinkFlags,
) SymLinkError!void {
    assert(path.isAbsolute(target_path));
    assert(path.isAbsolute(sym_link_path));
    return symLink(.cwd(), io, target_path, sym_link_path, flags);
}

/// Same as `symLink`, except tries to create the symbolic link until it
/// succeeds or encounters an error other than `error.PathAlreadyExists`.
///
/// * On Windows, both paths should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// * On WASI, both paths should be encoded as valid UTF-8.
/// * On other platforms, both paths are an opaque sequence of bytes with no particular encoding.
pub fn symLinkAtomic(
    dir: Dir,
    io: Io,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: SymLinkFlags,
) !void {
    if (dir.symLink(io, target_path, sym_link_path, flags)) {
        return;
    } else |err| switch (err) {
        error.PathAlreadyExists => {},
        else => |e| return e,
    }

    const dirname = path.dirname(sym_link_path) orelse ".";

    const rand_len = @sizeOf(u64) * 2;
    const temp_path_len = dirname.len + 1 + rand_len;
    var temp_path_buf: [max_path_bytes]u8 = undefined;

    if (temp_path_len > temp_path_buf.len) return error.NameTooLong;
    @memcpy(temp_path_buf[0..dirname.len], dirname);
    temp_path_buf[dirname.len] = path.sep;

    const temp_path = temp_path_buf[0..temp_path_len];

    var random_integer: u64 = undefined;

    while (true) {
        io.random(@ptrCast(&random_integer));
        temp_path[dirname.len + 1 ..][0..rand_len].* = std.fmt.hex(random_integer);

        if (dir.symLink(io, target_path, temp_path, flags)) {
            return dir.rename(temp_path, dir, sym_link_path, io);
        } else |err| switch (err) {
            error.PathAlreadyExists => continue,
            else => |e| return e,
        }
    }
}

pub const ReadLinkError = error{
    /// In WASI, this error may occur when the file descriptor does
    /// not hold the required rights to read value of a symbolic link relative to it.
    AccessDenied,
    PermissionDenied,
    FileSystem,
    SymLinkLoop,
    FileNotFound,
    SystemResources,
    NotLink,
    NotDir,
    /// Windows-only. This error may occur if the opened reparse point is
    /// of unsupported type.
    UnsupportedReparsePointType,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
    /// On Windows, antivirus software is enabled by default. It can be
    /// disabled, but Windows Update sometimes ignores the user's preference
    /// and re-enables it. When enabled, antivirus software on Windows
    /// intercepts file system operations and makes them significantly slower
    /// in addition to possibly failing with this error code.
    AntivirusInterference,
    /// File attempted to be opened is a running executable.
    FileBusy,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Obtain target of a symbolic link.
///
/// Returns how many bytes of `buffer` are populated.
///
/// Asserts that the path parameter has no null bytes.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn readLink(dir: Dir, io: Io, sub_path: []const u8, buffer: []u8) ReadLinkError!usize {
    return io.vtable.dirReadLink(io.userdata, dir, sub_path, buffer);
}

/// Same as `readLink`, except it asserts the path is absolute.
///
/// On Windows, `path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `path` should be encoded as valid UTF-8.
/// On other platforms, `path` is an opaque sequence of bytes with no particular encoding.
pub fn readLinkAbsolute(io: Io, absolute_path: []const u8, buffer: []u8) ReadLinkError!usize {
    assert(path.isAbsolute(absolute_path));
    return io.vtable.dirReadLink(io.userdata, .cwd(), absolute_path, buffer);
}

pub const ReadFileAllocError = File.OpenError || File.Reader.Error || Allocator.Error || error{
    /// File size reached or exceeded the provided limit.
    StreamTooLong,
};

/// Reads all the bytes from the named file. On success, caller owns returned
/// buffer.
///
/// If the file size is already known, a better alternative is to initialize a
/// `File.Reader`.
///
/// If the file size cannot be obtained, an error is returned. If
/// this is a realistic possibility, a better alternative is to initialize a
/// `File.Reader` which handles this seamlessly.
pub fn readFileAlloc(
    dir: Dir,
    io: Io,
    /// On Windows, should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
    /// On WASI, should be encoded as valid UTF-8.
    /// On other platforms, an opaque sequence of bytes with no particular encoding.
    sub_path: []const u8,
    /// Used to allocate the result.
    gpa: Allocator,
    /// If reached or exceeded, `error.StreamTooLong` is returned instead.
    limit: Io.Limit,
) ReadFileAllocError![]u8 {
    return readFileAllocOptions(dir, io, sub_path, gpa, limit, .of(u8), null);
}

/// Reads all the bytes from the named file. On success, caller owns returned
/// buffer.
///
/// If the file size is already known, a better alternative is to initialize a
/// `File.Reader`.
pub fn readFileAllocOptions(
    dir: Dir,
    io: Io,
    /// On Windows, should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
    /// On WASI, should be encoded as valid UTF-8.
    /// On other platforms, an opaque sequence of bytes with no particular encoding.
    sub_path: []const u8,
    /// Used to allocate the result.
    gpa: Allocator,
    /// If reached or exceeded, `error.StreamTooLong` is returned instead.
    limit: Io.Limit,
    comptime alignment: std.mem.Alignment,
    comptime sentinel: ?u8,
) ReadFileAllocError!(if (sentinel) |s| [:s]align(alignment.toByteUnits()) u8 else []align(alignment.toByteUnits()) u8) {
    var file = try dir.openFile(io, sub_path, .{
        // We can take advantage of this on Windows since it doesn't involve any extra syscalls,
        // so we can get error.IsDir during open rather than during the read.
        .allow_directory = if (native_os == .windows) false else true,
    });
    defer file.close(io);
    var file_reader = file.reader(io, &.{});
    return file_reader.interface.allocRemainingAlignedSentinel(gpa, limit, alignment, sentinel) catch |err| switch (err) {
        error.ReadFailed => return file_reader.err.?,
        error.OutOfMemory, error.StreamTooLong => |e| return e,
    };
}

pub const DeleteTreeError = error{
    AccessDenied,
    PermissionDenied,
    FileTooBig,
    SymLinkLoop,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    NoDevice,
    SystemResources,
    ReadOnlyFileSystem,
    FileSystem,
    FileBusy,
    /// One of the path components was not a directory.
    /// This error is unreachable if `sub_path` does not contain a path separator.
    NotDir,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
} || PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Whether `sub_path` describes a symlink, file, or directory, this function
/// removes it. If it cannot be removed because it is a non-empty directory,
/// this function recursively removes its entries and then tries again.
///
/// This operation is not atomic on most file systems.
///
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn deleteTree(dir: Dir, io: Io, sub_path: []const u8) DeleteTreeError!void {
    var initial_iterable_dir = (try dir.deleteTreeOpenInitialSubpath(io, sub_path, .file)) orelse return;

    const StackItem = struct {
        name: []const u8,
        parent_dir: Dir,
        iter: Iterator,

        fn closeAll(inner_io: Io, items: []@This()) void {
            for (items) |*item| item.iter.reader.dir.close(inner_io);
        }
    };

    var stack_buffer: [16]StackItem = undefined;
    var stack = std.ArrayList(StackItem).initBuffer(&stack_buffer);
    defer StackItem.closeAll(io, stack.items);

    stack.appendAssumeCapacity(.{
        .name = sub_path,
        .parent_dir = dir,
        .iter = initial_iterable_dir.iterateAssumeFirstIteration(),
    });

    process_stack: while (stack.items.len != 0) {
        var top = &stack.items[stack.items.len - 1];
        while (try top.iter.next(io)) |entry| {
            var treat_as_dir = entry.kind == .directory;
            handle_entry: while (true) {
                if (treat_as_dir) {
                    if (stack.unusedCapacitySlice().len >= 1) {
                        var iterable_dir = top.iter.reader.dir.openDir(io, entry.name, .{
                            .follow_symlinks = false,
                            .iterate = true,
                        }) catch |err| switch (err) {
                            error.NotDir => {
                                treat_as_dir = false;
                                continue :handle_entry;
                            },
                            error.FileNotFound => {
                                // That's fine, we were trying to remove this directory anyway.
                                break :handle_entry;
                            },

                            error.AccessDenied,
                            error.PermissionDenied,
                            error.SymLinkLoop,
                            error.ProcessFdQuotaExceeded,
                            error.NameTooLong,
                            error.SystemFdQuotaExceeded,
                            error.NoDevice,
                            error.SystemResources,
                            error.Unexpected,
                            error.BadPathName,
                            error.NetworkNotFound,
                            error.Canceled,
                            => |e| return e,
                        };
                        stack.appendAssumeCapacity(.{
                            .name = entry.name,
                            .parent_dir = top.iter.reader.dir,
                            .iter = iterable_dir.iterateAssumeFirstIteration(),
                        });
                        continue :process_stack;
                    } else {
                        try top.iter.reader.dir.deleteTreeMinStackSizeWithKindHint(io, entry.name, entry.kind);
                        break :handle_entry;
                    }
                } else {
                    if (top.iter.reader.dir.deleteFile(io, entry.name)) {
                        break :handle_entry;
                    } else |err| switch (err) {
                        error.FileNotFound => break :handle_entry,

                        // Impossible because we do not pass any path separators.
                        error.NotDir => unreachable,

                        error.IsDir => {
                            treat_as_dir = true;
                            continue :handle_entry;
                        },

                        error.AccessDenied,
                        error.PermissionDenied,
                        error.SymLinkLoop,
                        error.NameTooLong,
                        error.SystemResources,
                        error.ReadOnlyFileSystem,
                        error.FileSystem,
                        error.FileBusy,
                        error.BadPathName,
                        error.NetworkNotFound,
                        error.Canceled,
                        error.Unexpected,
                        => |e| return e,
                    }
                }
            }
        }

        // On Windows, we can't delete until the dir's handle has been closed, so
        // close it before we try to delete.
        top.iter.reader.dir.close(io);

        // In order to avoid double-closing the directory when cleaning up
        // the stack in the case of an error, we save the relevant portions and
        // pop the value from the stack.
        const parent_dir = top.parent_dir;
        const name = top.name;
        stack.items.len -= 1;

        var need_to_retry: bool = false;
        parent_dir.deleteDir(io, name) catch |err| switch (err) {
            error.FileNotFound => {},
            error.DirNotEmpty => need_to_retry = true,
            else => |e| return e,
        };

        if (need_to_retry) {
            // Since we closed the handle that the previous iterator used, we
            // need to re-open the dir and re-create the iterator.
            var iterable_dir = iterable_dir: {
                var treat_as_dir = true;
                handle_entry: while (true) {
                    if (treat_as_dir) {
                        break :iterable_dir parent_dir.openDir(io, name, .{
                            .follow_symlinks = false,
                            .iterate = true,
                        }) catch |err| switch (err) {
                            error.NotDir => {
                                treat_as_dir = false;
                                continue :handle_entry;
                            },
                            error.FileNotFound => {
                                // That's fine, we were trying to remove this directory anyway.
                                continue :process_stack;
                            },

                            error.AccessDenied,
                            error.PermissionDenied,
                            error.SymLinkLoop,
                            error.ProcessFdQuotaExceeded,
                            error.NameTooLong,
                            error.SystemFdQuotaExceeded,
                            error.NoDevice,
                            error.SystemResources,
                            error.Unexpected,
                            error.BadPathName,
                            error.NetworkNotFound,
                            error.Canceled,
                            => |e| return e,
                        };
                    } else {
                        if (parent_dir.deleteFile(io, name)) {
                            continue :process_stack;
                        } else |err| switch (err) {
                            error.FileNotFound => continue :process_stack,

                            // Impossible because we do not pass any path separators.
                            error.NotDir => unreachable,

                            error.IsDir => {
                                treat_as_dir = true;
                                continue :handle_entry;
                            },

                            error.AccessDenied,
                            error.PermissionDenied,
                            error.SymLinkLoop,
                            error.NameTooLong,
                            error.SystemResources,
                            error.ReadOnlyFileSystem,
                            error.FileSystem,
                            error.FileBusy,
                            error.BadPathName,
                            error.NetworkNotFound,
                            error.Canceled,
                            error.Unexpected,
                            => |e| return e,
                        }
                    }
                }
            };
            // We know there is room on the stack since we are just re-adding
            // the StackItem that we previously popped.
            stack.appendAssumeCapacity(.{
                .name = name,
                .parent_dir = parent_dir,
                .iter = iterable_dir.iterateAssumeFirstIteration(),
            });
            continue :process_stack;
        }
    }
}

/// Like `deleteTree`, but only keeps one `Iterator` active at a time to minimize the function's stack size.
/// This is slower than `deleteTree` but uses less stack space.
/// On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `sub_path` should be encoded as valid UTF-8.
/// On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn deleteTreeMinStackSize(dir: Dir, io: Io, sub_path: []const u8) DeleteTreeError!void {
    return dir.deleteTreeMinStackSizeWithKindHint(io, sub_path, .file);
}

fn deleteTreeMinStackSizeWithKindHint(parent: Dir, io: Io, sub_path: []const u8, kind_hint: File.Kind) DeleteTreeError!void {
    start_over: while (true) {
        var dir = (try parent.deleteTreeOpenInitialSubpath(io, sub_path, kind_hint)) orelse return;
        var cleanup_dir_parent: ?Dir = null;
        defer if (cleanup_dir_parent) |*d| d.close(io);

        var cleanup_dir = true;
        defer if (cleanup_dir) dir.close(io);

        // Valid use of max_path_bytes because dir_name_buf will only
        // ever store a single path component that was returned from the
        // filesystem.
        var dir_name_buf: [max_path_bytes]u8 = undefined;
        var dir_name: []const u8 = sub_path;

        // Here we must avoid recursion, in order to provide O(1) memory guarantee of this function.
        // Go through each entry and if it is not a directory, delete it. If it is a directory,
        // open it, and close the original directory. Repeat. Then start the entire operation over.

        scan_dir: while (true) {
            var dir_it = dir.iterateAssumeFirstIteration();
            dir_it: while (try dir_it.next(io)) |entry| {
                var treat_as_dir = entry.kind == .directory;
                handle_entry: while (true) {
                    if (treat_as_dir) {
                        const new_dir = dir.openDir(io, entry.name, .{
                            .follow_symlinks = false,
                            .iterate = true,
                        }) catch |err| switch (err) {
                            error.NotDir => {
                                treat_as_dir = false;
                                continue :handle_entry;
                            },
                            error.FileNotFound => {
                                // That's fine, we were trying to remove this directory anyway.
                                continue :dir_it;
                            },

                            error.AccessDenied,
                            error.PermissionDenied,
                            error.SymLinkLoop,
                            error.ProcessFdQuotaExceeded,
                            error.NameTooLong,
                            error.SystemFdQuotaExceeded,
                            error.NoDevice,
                            error.SystemResources,
                            error.Unexpected,
                            error.BadPathName,
                            error.NetworkNotFound,
                            error.Canceled,
                            => |e| return e,
                        };
                        if (cleanup_dir_parent) |*d| d.close(io);
                        cleanup_dir_parent = dir;
                        dir = new_dir;
                        const result = dir_name_buf[0..entry.name.len];
                        @memcpy(result, entry.name);
                        dir_name = result;
                        continue :scan_dir;
                    } else {
                        if (dir.deleteFile(io, entry.name)) {
                            continue :dir_it;
                        } else |err| switch (err) {
                            error.FileNotFound => continue :dir_it,

                            // Impossible because we do not pass any path separators.
                            error.NotDir => unreachable,

                            error.IsDir => {
                                treat_as_dir = true;
                                continue :handle_entry;
                            },

                            error.AccessDenied,
                            error.PermissionDenied,
                            error.SymLinkLoop,
                            error.NameTooLong,
                            error.SystemResources,
                            error.ReadOnlyFileSystem,
                            error.FileSystem,
                            error.FileBusy,
                            error.BadPathName,
                            error.NetworkNotFound,
                            error.Canceled,
                            error.Unexpected,
                            => |e| return e,
                        }
                    }
                }
            }
            // Reached the end of the directory entries, which means we successfully deleted all of them.
            // Now to remove the directory itself.
            dir.close(io);
            cleanup_dir = false;

            if (cleanup_dir_parent) |d| {
                d.deleteDir(io, dir_name) catch |err| switch (err) {
                    // These two things can happen due to file system race conditions.
                    error.FileNotFound, error.DirNotEmpty => continue :start_over,
                    else => |e| return e,
                };
                continue :start_over;
            } else {
                parent.deleteDir(io, sub_path) catch |err| switch (err) {
                    error.FileNotFound => return,
                    error.DirNotEmpty => continue :start_over,
                    else => |e| return e,
                };
                return;
            }
        }
    }
}

/// On successful delete, returns null.
fn deleteTreeOpenInitialSubpath(dir: Dir, io: Io, sub_path: []const u8, kind_hint: File.Kind) !?Dir {
    return iterable_dir: {
        // Treat as a file by default
        var treat_as_dir = kind_hint == .directory;

        handle_entry: while (true) {
            if (treat_as_dir) {
                break :iterable_dir dir.openDir(io, sub_path, .{
                    .follow_symlinks = false,
                    .iterate = true,
                }) catch |err| switch (err) {
                    error.NotDir => {
                        treat_as_dir = false;
                        continue :handle_entry;
                    },
                    error.FileNotFound => {
                        // That's fine, we were trying to remove this directory anyway.
                        return null;
                    },

                    error.AccessDenied,
                    error.PermissionDenied,
                    error.SymLinkLoop,
                    error.ProcessFdQuotaExceeded,
                    error.NameTooLong,
                    error.SystemFdQuotaExceeded,
                    error.NoDevice,
                    error.SystemResources,
                    error.Unexpected,
                    error.BadPathName,
                    error.NetworkNotFound,
                    error.Canceled,
                    => |e| return e,
                };
            } else {
                if (dir.deleteFile(io, sub_path)) {
                    return null;
                } else |err| switch (err) {
                    error.FileNotFound => return null,

                    error.IsDir => {
                        treat_as_dir = true;
                        continue :handle_entry;
                    },

                    error.AccessDenied,
                    error.PermissionDenied,
                    error.SymLinkLoop,
                    error.NameTooLong,
                    error.SystemResources,
                    error.ReadOnlyFileSystem,
                    error.NotDir,
                    error.FileSystem,
                    error.FileBusy,
                    error.BadPathName,
                    error.NetworkNotFound,
                    error.Canceled,
                    error.Unexpected,
                    => |e| return e,
                }
            }
        }
    };
}

pub const CopyFileOptions = struct {
    /// When this is `null` the permissions are copied from the source file.
    permissions: ?File.Permissions = null,
    make_path: bool = false,
    replace: bool = true,
};

pub const CopyFileError = File.OpenError || File.StatError ||
    CreateFileAtomicError || File.Atomic.ReplaceError || File.Atomic.LinkError ||
    File.Reader.Error || File.Writer.Error || error{InvalidFileName};

/// Atomically creates a new file at `dest_path` within `dest_dir` with the
/// same contents as `source_path` within `source_dir`.
///
/// Whether to overwrite the existing file is determined by `options`.
///
/// On Linux, until https://patchwork.kernel.org/patch/9636735/ is merged and
/// readily available, there is a possibility of power loss or application
/// termination leaving temporary files present in the same directory as
/// dest_path.
///
/// On Windows, both paths should be encoded as
/// [WTF-8](https://wtf-8.codeberg.page/). On WASI, both paths should be
/// encoded as valid UTF-8. On other platforms, both paths are an opaque
/// sequence of bytes with no particular encoding.
pub fn copyFile(
    source_dir: Dir,
    source_path: []const u8,
    dest_dir: Dir,
    dest_path: []const u8,
    io: Io,
    options: CopyFileOptions,
) CopyFileError!void {
    const file = try source_dir.openFile(io, source_path, .{});
    var file_reader: File.Reader = .init(file, io, &.{});
    defer file_reader.file.close(io);

    const permissions = options.permissions orelse blk: {
        const st = try file_reader.file.stat(io);
        file_reader.size = st.size;
        break :blk st.permissions;
    };

    var atomic_file = try dest_dir.createFileAtomic(io, dest_path, .{
        .permissions = permissions,
        .make_path = options.make_path,
        .replace = options.replace,
    });
    defer atomic_file.deinit(io);

    var buffer: [1024]u8 = undefined; // Used only when direct fd-to-fd is not available.
    var file_writer = atomic_file.file.writer(io, &buffer);

    _ = file_writer.interface.sendFileAll(&file_reader, .unlimited) catch |err| switch (err) {
        error.ReadFailed => return file_reader.err.?,
        error.WriteFailed => return file_writer.err.?,
    };

    try file_writer.flush();

    switch (options.replace) {
        true => try atomic_file.replace(io),
        false => try atomic_file.link(io),
    }
}

/// Same as `copyFile`, except asserts that both `source_path` and `dest_path`
/// are absolute.
///
/// On Windows, both paths should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, both paths should be encoded as valid UTF-8.
/// On other platforms, both paths are an opaque sequence of bytes with no particular encoding.
pub fn copyFileAbsolute(
    source_path: []const u8,
    dest_path: []const u8,
    io: Io,
    options: CopyFileOptions,
) !void {
    assert(path.isAbsolute(source_path));
    assert(path.isAbsolute(dest_path));
    const my_cwd = cwd();
    return copyFile(my_cwd, source_path, my_cwd, dest_path, io, options);
}

test copyFileAbsolute {}

pub const CreateFileAtomicOptions = struct {
    permissions: File.Permissions = .default_file,
    make_path: bool = false,
    /// Tells whether the unnamed file will be ultimately created with
    /// `File.Atomic.link` or `File.Atomic.replace`.
    ///
    /// If this value is incorrect it will cause an assertion failure in
    /// `File.Atomic.replace`.
    replace: bool = false,
};

pub const CreateFileAtomicError = error{
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
    FileNotFound,
    /// Insufficient kernel memory was available.
    SystemResources,
    /// A new path cannot be created because the device has no room for the new file.
    NoSpaceLeft,
    /// A component used as a directory in the path was not, in fact, a directory.
    NotDir,
    WouldBlock,
    ReadOnlyFileSystem,
    /// The file attempted to be created is a running executable.
    FileBusy,
} || Io.Dir.PathNameError || Io.Cancelable || Io.UnexpectedError;

/// Create an unnamed ephemeral file that can eventually be atomically
/// materialized into `sub_path`.
///
/// The returned `File.Atomic` provides API to emulate the behavior in case it
/// is not directly supported by the underlying operating system.
///
/// * On Windows, `sub_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// * On WASI, `sub_path` should be encoded as valid UTF-8.
/// * On other platforms, `sub_path` is an opaque sequence of bytes with no particular encoding.
pub fn createFileAtomic(
    dir: Dir,
    io: Io,
    sub_path: []const u8,
    options: CreateFileAtomicOptions,
) CreateFileAtomicError!File.Atomic {
    return io.vtable.dirCreateFileAtomic(io.userdata, dir, sub_path, options);
}

pub const SetPermissionsError = File.SetPermissionsError;
pub const Permissions = File.Permissions;

/// Also known as "chmod".
///
/// The process must have the correct privileges in order to do this
/// successfully, or must have the effective user ID matching the owner
/// of the directory. Additionally, the directory must have been opened
/// with `OpenOptions.iterate` set to `true`.
pub fn setPermissions(dir: Dir, io: Io, new_permissions: File.Permissions) SetPermissionsError!void {
    return io.vtable.dirSetPermissions(io.userdata, dir, new_permissions);
}

pub const SetFilePermissionsError = PathNameError || SetPermissionsError || error{
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    /// `SetFilePermissionsOptions.follow_symlinks` was set to false, which is
    /// not allowed by the file system or operating system.
    OperationUnsupported,
};

pub const SetFilePermissionsOptions = struct {
    follow_symlinks: bool = true,
};

/// Also known as "fchmodat".
pub fn setFilePermissions(
    dir: Dir,
    io: Io,
    sub_path: []const u8,
    new_permissions: File.Permissions,
    options: SetFilePermissionsOptions,
) SetFilePermissionsError!void {
    return io.vtable.dirSetFilePermissions(io.userdata, dir, sub_path, new_permissions, options);
}

pub const SetOwnerError = File.SetOwnerError;

/// Also known as "chown".
///
/// The process must have the correct privileges in order to do this
/// successfully. The group may be changed by the owner of the directory to
/// any group of which the owner is a member. Additionally, the directory
/// must have been opened with `OpenOptions.iterate` set to `true`. If the
/// owner or group is specified as `null`, the ID is not changed.
pub fn setOwner(dir: Dir, io: Io, owner: ?File.Uid, group: ?File.Gid) SetOwnerError!void {
    return io.vtable.dirSetOwner(io.userdata, dir, owner, group);
}

pub const SetFileOwnerError = PathNameError || SetOwnerError;

pub const SetFileOwnerOptions = struct {
    follow_symlinks: bool = true,
};

/// Also known as "fchownat".
pub fn setFileOwner(
    dir: Dir,
    io: Io,
    sub_path: []const u8,
    owner: ?File.Uid,
    group: ?File.Gid,
    options: SetFileOwnerOptions,
) SetOwnerError!void {
    return io.vtable.dirSetFileOwner(io.userdata, dir, sub_path, owner, group, options);
}

pub const SetTimestampsError = File.SetTimestampsError || PathNameError;

pub const SetTimestampsOptions = struct {
    follow_symlinks: bool = true,
    access_timestamp: File.SetTimestamp = .unchanged,
    modify_timestamp: File.SetTimestamp = .unchanged,
};

/// The granularity that ultimately is stored depends on the combination of
/// operating system and file system. When a value as provided that exceeds
/// this range, the value is clamped to the maximum.
pub fn setTimestamps(
    dir: Dir,
    io: Io,
    sub_path: []const u8,
    options: SetTimestampsOptions,
) SetTimestampsError!void {
    return io.vtable.dirSetTimestamps(io.userdata, dir, sub_path, options);
}

pub const SetTimestampsNowOptions = struct {
    follow_symlinks: bool = true,
};

/// Sets the accessed and modification timestamps of the provided path to the
/// current wall clock time.
///
/// The granularity that ultimately is stored depends on the combination of
/// operating system and file system.
pub fn setTimestampsNow(
    dir: Dir,
    io: Io,
    sub_path: []const u8,
    options: SetTimestampsNowOptions,
) SetTimestampsError!void {
    return io.vtable.fileSetTimestamps(io.userdata, dir, sub_path, .{
        .follow_symlinks = options.follow_symlinks,
        .access_timestamp = .now,
        .modify_timestamp = .now,
    });
}



---
File: /std/Io/Dispatch.zig
---

const Alignment = std.mem.Alignment;
const Allocator = std.mem.Allocator;
const Argv0 = Io.Threaded.Argv0;
const assert = std.debug.assert;
const builtin = @import("builtin");
const c = std.c;
const ChdirError = Io.Threaded.ChdirError;
const clockToPosix = Io.Threaded.clockToPosix;
const closeFd = Io.Threaded.closeFd;
const Csprng = Io.Threaded.Csprng;
const default_PATH = Io.Threaded.default_PATH;
const Dir = Io.Dir;
const Environ = Io.Threaded.Environ;
const errnoBug = Io.Threaded.errnoBug;
const Evented = @This();
const fallbackSeed = Io.Threaded.fallbackSeed;
const File = Io.File;
const Io = std.Io;
const iovec = std.posix.iovec;
const iovec_const = std.posix.iovec_const;
const log = std.log.scoped(.dispatch);
const max_iovecs_len = Io.Threaded.max_iovecs_len;
const nanosecondsFromPosix = Io.Threaded.nanosecondsFromPosix;
const net = Io.net;
const pathToPosix = Io.Threaded.pathToPosix;
const process = std.process;
const recoverableOsBugDetected = Io.Threaded.recoverableOsBugDetected;
const setTimestampToPosix = Io.Threaded.setTimestampToPosix;
const splat_buffer_size = Io.Threaded.splat_buffer_size;
const statFromPosix = Io.Threaded.statFromPosix;
const statusToTerm = Io.Threaded.statusToTerm;
const std = @import("std");
const timestampFromPosix = Io.Threaded.timestampFromPosix;
const unexpectedErrno = std.posix.unexpectedErrno;
const UseSendfile = Io.Threaded.UseSendfile;
const UseFcopyfile = Io.Threaded.UseFcopyfile;

/// Empirically saw >4KB being used by the llvm aarch64 backend.
const main_loop_stack_size = 8 * 1024;

queue: c.dispatch.queue_t,
backing_allocator_needs_mutex: bool,
backing_allocator_mutex: Mutex,
/// Does not need to be thread-safe if not used elsewhere.
backing_allocator: Allocator,
main_fiber: Fiber,
main_loop_stack: [*]align(builtin.target.stackAlignment()) u8,
exit_semaphore: c.dispatch.semaphore_t,

use_sendfile: UseSendfile,
use_fcopyfile: UseFcopyfile,
leeway: u64,

futexes: [1 << 8]Futex,

init_stderr_writer: c.dispatch.once_t,
stderr_mutex: Mutex,
stderr_writer: File.Writer,
stderr_mode: Io.Terminal.Mode,

scan_environ: c.dispatch.once_t,
environ: Environ,

open_dev_null: c.dispatch.once_t,
dev_null_file: File.OpenError!File,

csprng_mutex: Mutex,
csprng: Csprng,

const Thread = struct {
    main_context: Io.fiber.Context,
    current_context: ?*Io.fiber.Context,
    seed_csprng: c.dispatch.once_t,
    csprng: Csprng,

    threadlocal var self: Thread = .{
        .main_context = undefined,
        .current_context = null,
        .seed_csprng = .init,
        .csprng = undefined,
    };

    noinline fn current() *Thread {
        return &self;
    }

    fn currentFiber(thread: *Thread) *Fiber {
        assert(thread.current_context != &thread.main_context);
        return @fieldParentPtr("context", thread.current_context.?);
    }

    const List = struct {
        allocated: []Thread,
        reserved: u32,
        active: u32,
    };
};

const Fiber = struct {
    required_align: void align(4),
    evented: *Evented,
    context: Io.fiber.Context,
    link: union {
        awaiter: ?*Fiber,
        group: struct { prev: ?*Fiber, next: ?*Fiber },
    },
    awaiting_group: Group,
    cancel_status: CancelStatus,
    cancel_protection: CancelProtection,

    var next_name: u64 = 0;

    const CancelStatus = packed struct(usize) {
        requested: bool,
        awaiting: Awaiting,

        const unrequested: CancelStatus = .{ .requested = false, .awaiting = .nothing };

        const Awaiting = enum(@Int(.unsigned, @bitSizeOf(usize) - shift)) {
            nothing = 0,
            group = 1,
            _,

            const shift = 1;

            fn subWrap(lhs: Awaiting, rhs: Awaiting) Awaiting {
                return @enumFromInt(@intFromEnum(lhs) -% @intFromEnum(rhs));
            }

            fn fromCancelable(cancelable: *Cancelable) Awaiting {
                return @enumFromInt(@shrExact(@intFromPtr(cancelable), shift));
            }

            fn toCancelable(awaiting: Awaiting) *Cancelable {
                return @ptrFromInt(@shlExact(@as(usize, @intFromEnum(awaiting)), shift));
            }
        };

        fn changeAwaiting(
            cancel_status: *CancelStatus,
            old_awaiting: Awaiting,
            new_awaiting: Awaiting,
        ) bool {
            const old_cancel_status = @atomicRmw(CancelStatus, cancel_status, .Add, .{
                .requested = false,
                .awaiting = new_awaiting.subWrap(old_awaiting),
            }, .release);
            assert(old_cancel_status.awaiting == old_awaiting);
            return old_cancel_status.requested;
        }
    };

    const CancelProtection = packed struct {
        user: Io.CancelProtection,
        acknowledged: bool,

        const unblocked: CancelProtection = .{ .user = .unblocked, .acknowledged = false };

        fn check(cancel_protection: CancelProtection) Io.CancelProtection {
            return @enumFromInt(@intFromBool(cancel_protection != unblocked));
        }

        fn acknowledge(cancel_protection: *CancelProtection) void {
            assert(!cancel_protection.acknowledged);
            cancel_protection.acknowledged = true;
        }

        fn recancel(cancel_protection: *CancelProtection) void {
            assert(cancel_protection.acknowledged);
            cancel_protection.acknowledged = false;
        }

        test check {
            try std.testing.expectEqual(Io.CancelProtection.unblocked, check(.unblocked));
            try std.testing.expectEqual(Io.CancelProtection.blocked, check(.{
                .user = .unblocked,
                .acknowledged = true,
            }));
            try std.testing.expectEqual(Io.CancelProtection.blocked, check(.{
                .user = .blocked,
                .acknowledged = false,
            }));
            try std.testing.expectEqual(Io.CancelProtection.blocked, check(.{
                .user = .blocked,
                .acknowledged = true,
            }));
        }
    };

    const finished: ?*Fiber = @ptrFromInt(@alignOf(Fiber));

    const max_result_align: Alignment = .@"16";
    const max_result_size = max_result_align.forward(512);
    /// This includes any stack realignments that need to happen, and also the
    /// initial frame return address slot and argument frame, depending on target.
    const min_stack_size = 60 * 1024 * 1024;
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

    fn create(ev: *Evented) error{OutOfMemory}!*Fiber {
        return @ptrCast(try ev.allocator().alignedAlloc(u8, .of(Fiber), allocation_size));
    }

    fn destroy(fiber: *Fiber, ev: *Evented) void {
        ev.allocator().free(fiber.allocatedSlice());
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

    const Queue = struct { head: *Fiber, tail: *Fiber };

    /// Like a `*Fiber`, but 2 bits smaller than a pointer (because the LSBs are always 0 due to
    /// alignment) so that those two bits can be used in a `packed struct`.
    const PackedPtr = enum(@Int(.unsigned, @bitSizeOf(usize) - 2)) {
        null = 0,
        all_ones = std.math.maxInt(@Int(.unsigned, @bitSizeOf(usize) - 2)),
        _,

        const Split = packed struct(usize) { low: u2, high: PackedPtr };
        fn pack(ptr: ?*Fiber) PackedPtr {
            const split: Split = @bitCast(@intFromPtr(ptr));
            assert(split.low == 0);
            return split.high;
        }
        fn unpack(ptr: PackedPtr) ?*Fiber {
            const split: Split = .{ .low = 0, .high = ptr };
            return @ptrFromInt(@as(usize, @bitCast(split)));
        }
    };

    fn requestCancel(fiber: *Fiber, ev: *Evented) void {
        const cancel_status = @atomicRmw(
            Fiber.CancelStatus,
            &fiber.cancel_status,
            .Or,
            .{ .requested = true, .awaiting = .nothing },
            .acquire,
        );
        assert(!cancel_status.requested);
        switch (cancel_status.awaiting) {
            .nothing => {},
            .group => {
                // The awaiter received a cancelation request while awaiting a group,
                // so propagate the cancelation to the group.
                if (fiber.awaiting_group.cancel(ev, null)) {
                    fiber.awaiting_group = undefined;
                    ev.queue.async(fiber, &Fiber.@"resume");
                }
            },
            _ => |awaiting| awaiting.toCancelable().async(),
        }
    }

    fn @"resume"(context: ?*anyopaque) callconv(.c) void {
        const fiber: *Fiber = @ptrCast(@alignCast(context));
        const thread: *Thread = .current();
        const message: SwitchMessage = .{
            .contexts = .{
                .old = &thread.main_context,
                .new = &fiber.context,
            },
            .pending_task = .nothing,
        };
        contextSwitch(&message).handle(fiber.evented);
    }
};

pub fn allocator(ev: *Evented) std.mem.Allocator {
    return if (ev.backing_allocator_needs_mutex) .{
        .ptr = ev,
        .vtable = &.{
            .alloc = alloc,
            .resize = resize,
            .remap = remap,
            .free = free,
        },
    } else ev.backing_allocator;
}

fn alloc(userdata: *anyopaque, len: usize, alignment: std.mem.Alignment, ret_addr: usize) ?[*]u8 {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    ev.backing_allocator_mutex.lockUncancelable(ev);
    defer ev.backing_allocator_mutex.unlock();
    return ev.backing_allocator.rawAlloc(len, alignment, ret_addr);
}

fn resize(
    userdata: *anyopaque,
    memory: []u8,
    alignment: std.mem.Alignment,
    new_len: usize,
    ret_addr: usize,
) bool {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    ev.backing_allocator_mutex.lockUncancelable(ev);
    defer ev.backing_allocator_mutex.unlock();
    return ev.backing_allocator.rawResize(memory, alignment, new_len, ret_addr);
}

fn remap(
    userdata: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    new_len: usize,
    ret_addr: usize,
) ?[*]u8 {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    ev.backing_allocator_mutex.lockUncancelable(ev);
    defer ev.backing_allocator_mutex.unlock();
    return ev.backing_allocator.rawRemap(memory, alignment, new_len, ret_addr);
}

fn free(userdata: *anyopaque, memory: []u8, alignment: std.mem.Alignment, ret_addr: usize) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    ev.backing_allocator_mutex.lockUncancelable(ev);
    defer ev.backing_allocator_mutex.unlock();
    return ev.backing_allocator.rawFree(memory, alignment, ret_addr);
}

pub fn io(ev: *Evented) Io {
    return .{
        .userdata = ev,
        .vtable = &.{
            .crashHandler = crashHandler,

            .async = async,
            .concurrent = concurrent,
            .await = await,
            .cancel = cancel,

            .groupAsync = groupAsync,
            .groupConcurrent = groupConcurrent,
            .groupAwait = groupAwait,
            .groupCancel = groupCancel,

            .recancel = recancel,
            .swapCancelProtection = swapCancelProtection,
            .checkCancel = checkCancel,

            .futexWait = futexWait,
            .futexWaitUncancelable = futexWaitUncancelable,
            .futexWake = futexWake,

            .operate = operate,
            .batchAwaitAsync = batchAwaitAsync,
            .batchAwaitConcurrent = batchAwaitConcurrent,
            .batchCancel = batchCancel,

            .dirCreateDir = dirCreateDir,
            .dirCreateDirPath = dirCreateDirPath,
            .dirCreateDirPathOpen = dirCreateDirPathOpen,
            .dirOpenDir = dirOpenDir,
            .dirStat = dirStat,
            .dirStatFile = dirStatFile,
            .dirAccess = dirAccess,
            .dirCreateFile = dirCreateFile,
            .dirCreateFileAtomic = dirCreateFileAtomic,
            .dirOpenFile = dirOpenFile,
            .dirClose = dirClose,
            .dirRead = dirRead,
            .dirRealPath = dirRealPath,
            .dirRealPathFile = dirRealPathFile,
            .dirDeleteFile = dirDeleteFile,
            .dirDeleteDir = dirDeleteDir,
            .dirRename = dirRename,
            .dirRenamePreserve = dirRenamePreserve,
            .dirSymLink = dirSymLink,
            .dirReadLink = dirReadLink,
            .dirSetOwner = dirSetOwner,
            .dirSetFileOwner = dirSetFileOwner,
            .dirSetPermissions = dirSetPermissions,
            .dirSetFilePermissions = dirSetFilePermissions,
            .dirSetTimestamps = dirSetTimestamps,
            .dirHardLink = dirHardLink,

            .fileStat = fileStat,
            .fileLength = fileLength,
            .fileClose = fileClose,
            .fileWritePositional = fileWritePositional,
            .fileWriteFileStreaming = fileWriteFileStreaming,
            .fileWriteFilePositional = fileWriteFilePositional,
            .fileReadPositional = fileReadPositional,
            .fileSeekBy = fileSeekBy,
            .fileSeekTo = fileSeekTo,
            .fileSync = fileSync,
            .fileIsTty = fileIsTty,
            .fileEnableAnsiEscapeCodes = fileEnableAnsiEscapeCodes,
            .fileSupportsAnsiEscapeCodes = fileIsTty,
            .fileSetLength = fileSetLength,
            .fileSetOwner = fileSetOwner,
            .fileSetPermissions = fileSetPermissions,
            .fileSetTimestamps = fileSetTimestamps,
            .fileLock = fileLock,
            .fileTryLock = fileTryLock,
            .fileUnlock = fileUnlock,
            .fileDowngradeLock = fileDowngradeLock,
            .fileRealPath = fileRealPath,
            .fileHardLink = fileHardLink,

            .fileMemoryMapCreate = fileMemoryMapCreate,
            .fileMemoryMapDestroy = fileMemoryMapDestroy,
            .fileMemoryMapSetLength = fileMemoryMapSetLength,
            .fileMemoryMapRead = fileMemoryMapRead,
            .fileMemoryMapWrite = fileMemoryMapWrite,

            .processExecutableOpen = processExecutableOpen,
            .processExecutablePath = processExecutablePath,
            .lockStderr = lockStderr,
            .tryLockStderr = tryLockStderr,
            .unlockStderr = unlockStderr,
            .processCurrentPath = processCurrentPath,
            .processSetCurrentDir = processSetCurrentDir,
            .processSetCurrentPath = processSetCurrentPath,
            .processReplace = processReplace,
            .processReplacePath = processReplacePath,
            .processSpawn = processSpawn,
            .processSpawnPath = processSpawnPath,
            .childWait = childWait,
            .childKill = childKill,

            .progressParentFile = progressParentFile,

            .now = now,
            .clockResolution = clockResolution,
            .sleep = sleep,

            .random = random,
            .randomSecure = randomSecure,

            .netListenIp = netListenIpUnavailable,
            .netAccept = netAcceptUnavailable,
            .netBindIp = netBindIpUnavailable,
            .netConnectIp = netConnectIpUnavailable,
            .netListenUnix = netListenUnixUnavailable,
            .netConnectUnix = netConnectUnixUnavailable,
            .netSocketCreatePair = netSocketCreatePairUnavailable,
            .netSend = netSendUnavailable,
            .netRead = netReadUnavailable,
            .netWrite = netWriteUnavailable,
            .netWriteFile = netWriteFileUnavailable,
            .netClose = netClose,
            .netShutdown = netShutdownUnavailable,
            .netInterfaceNameResolve = netInterfaceNameResolveUnavailable,
            .netInterfaceName = netInterfaceNameUnavailable,
            .netLookup = netLookupUnavailable,
        },
    };
}

pub const InitOptions = struct {
    backing_allocator_needs_mutex: bool = true,
    target_queue: ?c.dispatch.queue_t = .TARGET_DEFAULT,
    /// Upper limit on the allowable delay in processing timeouts in order to improve power
    /// consumption and system performance.
    leeway: Io.Duration = .fromMilliseconds(10),

    /// Affects the following operations:
    /// * `processExecutablePath` on OpenBSD and Haiku.
    argv0: Argv0 = .empty,
    /// Affects the following operations:
    /// * `fileIsTty`
    /// * `processSpawn`, `processSpawnPath`, `processReplace`, `processReplacePath`
    environ: process.Environ = .empty,
};

pub fn init(ev: *Evented, backing_allocator: Allocator, options: InitOptions) !void {
    const queue = c.dispatch.queue_create_with_target(
        "org.ziglang.std.Io.Dispatch",
        .CONCURRENT(),
        options.target_queue,
    ) orelse return error.SystemResources;
    errdefer queue.as_object().release();
    const main_loop_stack = try backing_allocator.alignedAlloc(
        u8,
        .fromByteUnits(builtin.target.stackAlignment()),
        main_loop_stack_size,
    );
    errdefer backing_allocator.free(main_loop_stack);
    const exit_semaphore = c.dispatch.semaphore_create(0) orelse return error.SystemResources;
    errdefer exit_semaphore.as_object().release();
    ev.* = .{
        .queue = queue,
        .backing_allocator_needs_mutex = options.backing_allocator_needs_mutex,
        .backing_allocator_mutex = undefined,
        .backing_allocator = backing_allocator,
        .main_fiber = .{
            .required_align = {},
            .evented = ev,
            .context = undefined,
            .link = .{ .awaiter = null },
            .awaiting_group = undefined,
            .cancel_status = .unrequested,
            .cancel_protection = .unblocked,
        },
        .main_loop_stack = main_loop_stack.ptr,
        .exit_semaphore = exit_semaphore,

        .use_fcopyfile = .default,
        .use_sendfile = .default,
        .leeway = std.math.lossyCast(u64, options.leeway.toNanoseconds()),

        .futexes = undefined,

        .init_stderr_writer = .init,
        .stderr_mutex = undefined,
        .stderr_writer = .{
            .io = ev.io(),
            .interface = Io.File.Writer.initInterface(&.{}),
            .file = .stderr(),
            .mode = .streaming,
        },
        .stderr_mode = .no_color,

        .scan_environ = if (options.environ.block.isEmpty()) .done else .init,
        .environ = .{ .process_environ = options.environ },

        .open_dev_null = .init,
        .dev_null_file = error.FileNotFound,

        .csprng_mutex = undefined,
        .csprng = .uninitialized,
    };
    try ev.backing_allocator_mutex.init(queue);
    errdefer ev.backing_allocator_mutex.deinit();
    var initialized_futexes: usize = 0;
    errdefer for (ev.futexes[0..initialized_futexes]) |*futex| futex.deinit();
    for (&ev.futexes) |*futex| {
        try futex.init(queue);
        initialized_futexes += 1;
    }
    try ev.stderr_mutex.init(queue);
    errdefer ev.stderr_mutex.deinit();
    try ev.csprng_mutex.init(queue);
    errdefer ev.csprng_mutex.deinit();
    const thread: *Thread = .current();
    thread.main_context = switch (builtin.cpu.arch) {
        .aarch64 => .{
            .sp = @intFromPtr(main_loop_stack[main_loop_stack_size..].ptr),
            .fp = @intFromPtr(ev),
            .pc = @intFromPtr(&mainLoopEntry),
        },
        .x86_64 => .{
            .rsp = @intFromPtr(main_loop_stack[main_loop_stack_size..].ptr) - 8,
            .rbp = @intFromPtr(ev),
            .rip = @intFromPtr(&mainLoopEntry),
        },
        else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
    };
    thread.current_context = &ev.main_fiber.context;
}

pub fn deinit(ev: *Evented) void {
    assert(Thread.current().currentFiber() == &ev.main_fiber);
    ev.yield(.exit);
    ev.csprng_mutex.deinit();
    if (ev.dev_null_file) |file| fileClose(ev, &.{file}) else |_| {}
    ev.stderr_mutex.deinit();
    for (&ev.futexes) |*futex| futex.deinit();
    ev.exit_semaphore.as_object().release();
    ev.backing_allocator.free(ev.main_loop_stack[0..main_loop_stack_size]);
    ev.queue.as_object().release();
}

fn yield(ev: *Evented, pending_task: SwitchMessage.PendingTask) void {
    const thread: *Thread = .current();
    const message: SwitchMessage = .{
        .contexts = .{
            .old = thread.current_context.?,
            .new = &thread.main_context,
        },
        .pending_task = pending_task,
    };
    contextSwitch(&message).handle(ev);
}

fn mainLoopEntry() callconv(.naked) void {
    switch (builtin.cpu.arch) {
        .aarch64 => asm volatile (
            \\ mov x0, fp
            \\ mov fp, #0
            \\ b %[mainLoop]
            :
            : [mainLoop] "X" (&mainLoop),
        ),
        .x86_64 => asm volatile (
            \\ movq %%rbp, %%rdi
            \\ xor %%ebp, %%ebp
            \\ jmp %[mainLoop:P]
            :
            : [mainLoop] "X" (&mainLoop),
        ),
        else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
    }
}

fn mainLoop(ev: *Evented, message: *const SwitchMessage) callconv(.c) noreturn {
    message.handle(ev);
    assert(ev.exit_semaphore.wait(.FOREVER) == 0);
    Fiber.@"resume"(&ev.main_fiber);
    unreachable; // switched to dead fiber
}

const SwitchMessage = struct {
    contexts: Io.fiber.Switch,
    pending_task: PendingTask,

    const PendingTask = union(enum) {
        nothing,
        await: *Fiber,
        activate: c.dispatch.object_t,
        @"resume": c.dispatch.object_t,
        group_await: Group,
        group_cancel: Group,
        mutex_wait: *Mutex.Waiter,
        futex_wait: *Futex.Waiter,
        futex_wake: *Futex.Waker,
        sleep_wait: *SleepWaiter,
        after: c.dispatch.time_t,
        destroy,
        exit,
    };

    fn handle(message: *const SwitchMessage, ev: *Evented) void {
        const thread: *Thread = .current();
        thread.current_context = message.contexts.new;
        switch (message.pending_task) {
            .nothing => {},
            .await => |awaiting| {
                const awaiter: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                if (@atomicRmw(?*Fiber, &awaiting.link.awaiter, .Xchg, awaiter, .acq_rel) ==
                    Fiber.finished) ev.queue.async(awaiter, &Fiber.@"resume");
            },
            .activate => |object| object.activate(),
            .@"resume" => |object| object.@"resume"(),
            .group_await => |group| {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                if (group.await(ev, fiber)) ev.queue.async(fiber, &Fiber.@"resume");
            },
            .group_cancel => |group| {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                if (group.cancel(ev, fiber)) ev.queue.async(fiber, &Fiber.@"resume");
            },
            .mutex_wait => |waiter| {
                waiter.sleeper =
                    .init(ev.queue, @alignCast(@fieldParentPtr("context", message.contexts.old)));
                switch (waiter.sleeper.fiber.cancel_protection.check()) {
                    .unblocked => {},
                    .blocked => waiter.cancelable = .blocked,
                }
                waiter.mutex.queue.async(waiter, &Mutex.Waiter.add);
            },
            .futex_wait => |waiter| {
                waiter.sleeper =
                    .init(ev.queue, @alignCast(@fieldParentPtr("context", message.contexts.old)));
                switch (waiter.sleeper.fiber.cancel_protection.check()) {
                    .unblocked => {},
                    .blocked => waiter.cancelable = .blocked,
                }
                waiter.futex.queue.async(waiter, &Futex.Waiter.add);
            },
            .futex_wake => |waker| {
                waker.sleeper =
                    .init(ev.queue, @alignCast(@fieldParentPtr("context", message.contexts.old)));
                waker.futex.queue.async(waker, &Futex.Waker.remove);
            },
            .sleep_wait => |waiter| {
                waiter.sleeper =
                    .init(ev.queue, @alignCast(@fieldParentPtr("context", message.contexts.old)));
                const queue = waiter.cancelable.queue;
                switch (waiter.sleeper.fiber.cancel_protection.check()) {
                    .unblocked => {},
                    .blocked => waiter.cancelable = .blocked,
                }
                queue.async(waiter, &SleepWaiter.start);
            },
            .after => |when| {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                when.after(ev.queue, fiber, &Fiber.@"resume");
            },
            .destroy => {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                fiber.destroy(ev);
            },
            .exit => _ = ev.exit_semaphore.signal(),
        }
    }
};

inline fn contextSwitch(message: *const SwitchMessage) *const SwitchMessage {
    return @fieldParentPtr("contexts", Io.fiber.contextSwitch(&message.contexts));
}

const Cancelable = struct {
    required_align: void align(2) = {},
    queue: c.dispatch.queue_t,
    cancel: c.dispatch.function_t,

    const fn_ptr_align = std.meta.alignment(c.dispatch.function_t);
    const is_blocked: c.dispatch.function_t = @ptrFromInt(fn_ptr_align * 1);
    const is_requested: c.dispatch.function_t = @ptrFromInt(fn_ptr_align * 2);

    const blocked: Cancelable = .{ .queue = undefined, .cancel = is_blocked };

    const RequestedError = error{CancelRequested};

    fn enter(cancelable: *Cancelable, fiber: *Fiber) RequestedError!void {
        const function = cancelable.cancel;
        assert(function != is_requested);
        if (function == is_blocked) {
            @branchHint(.unlikely);
            return;
        }
        if (@cmpxchgStrong(
            Fiber.CancelStatus,
            &fiber.cancel_status,
            .{ .requested = false, .awaiting = .nothing },
            .{ .requested = false, .awaiting = .fromCancelable(cancelable) },
            .release,
            .monotonic,
        )) |cancel_status| {
            assert(cancel_status.requested and cancel_status.awaiting == .nothing);
            cancelable.cancel = is_requested;
            return error.CancelRequested;
        }
    }

    fn leave(cancelable: *Cancelable, fiber: *Fiber) RequestedError!void {
        const function = cancelable.cancel;
        assert(function != is_requested);
        if (function == is_blocked) {
            @branchHint(.unlikely);
            return;
        }
        const cancel_status = @atomicRmw(Fiber.CancelStatus, &fiber.cancel_status, .And, .{
            .requested = true,
            .awaiting = .nothing,
        }, .monotonic);
        assert(cancel_status.awaiting.toCancelable() == cancelable);
        if (cancel_status.requested) return error.CancelRequested;
    }

    fn async(cancelable: *Cancelable) void {
        const function = cancelable.cancel;
        assert(function != is_blocked and function != is_requested);
        cancelable.queue.async(cancelable, function);
    }

    fn requested(cancelable: *Cancelable, fiber: *Fiber) void {
        const function = cancelable.cancel;
        assert(function != is_blocked and function != is_requested);
        assert(@atomicLoad(Fiber.CancelStatus, &fiber.cancel_status, .monotonic) == Fiber.CancelStatus{
            .requested = true,
            .awaiting = .fromCancelable(cancelable),
        });
        cancelable.cancel = is_requested;
        @atomicStore(Fiber.CancelStatus, &fiber.cancel_status, .{
            .requested = true,
            .awaiting = .nothing,
        }, .monotonic);
    }

    fn acknowledge(cancelable: *Cancelable, fiber: *Fiber) Io.Cancelable!void {
        if (cancelable.cancel == is_requested) {
            @branchHint(.unlikely);
            fiber.cancel_protection.acknowledge();
            return error.Canceled;
        }
    }
};

const Sleeper = struct {
    queue: c.dispatch.queue_t,
    fiber: *Fiber,

    fn init(queue: c.dispatch.queue_t, fiber: *Fiber) Sleeper {
        queue.as_object().retain();
        return .{ .queue = queue, .fiber = fiber };
    }

    fn wake(context: ?*anyopaque) callconv(.c) void {
        const sleeper: *Sleeper = @ptrCast(@alignCast(context));
        const queue = sleeper.queue;
        sleeper.queue = undefined;
        queue.async(sleeper.fiber, &Fiber.@"resume");
        queue.as_object().release();
    }
};

const Mutex = struct {
    state: State,
    queue: c.dispatch.queue_t,
    waiters: std.DoublyLinkedList,

    const State = packed struct(usize) {
        locked: bool,
        num_waiters: NumWaiters,

        const NumWaiters = @Int(.unsigned, @bitSizeOf(usize) - 1);
    };

    const Waiter = struct {
        sleeper: Sleeper = undefined,
        cancelable: Cancelable,
        mutex: *Mutex,
        node: std.DoublyLinkedList.Node = undefined,

        fn add(context: ?*anyopaque) callconv(.c) void {
            const waiter: *Waiter = @ptrCast(@alignCast(context));
            waiter.cancelable.enter(waiter.sleeper.fiber) catch |err| switch (err) {
                error.CancelRequested => return waiter.wake(),
            };
            var state = @atomicRmw(State, &waiter.mutex.state, .Add, .{
                .locked = false,
                .num_waiters = 1,
            }, .monotonic);
            state.num_waiters += 1;
            while (!state.locked) {
                @branchHint(.unlikely);
                state = @cmpxchgWeak(State, &waiter.mutex.state, state, .{
                    .locked = true,
                    .num_waiters = state.num_waiters - 1,
                }, .acquire, .monotonic) orelse break;
            } else return waiter.mutex.waiters.append(&waiter.node);
            waiter.cancelable.leave(waiter.sleeper.fiber) catch |err| switch (err) {
                error.CancelRequested => {
                    waiter.node.next = &waiter.node;
                    return;
                },
            };
            waiter.wake();
        }

        fn canceled(context: ?*anyopaque) callconv(.c) void {
            const cancelable: *Cancelable = @ptrCast(@alignCast(context));
            const waiter: *Waiter = @fieldParentPtr("cancelable", cancelable);
            cancelable.requested(waiter.sleeper.fiber);
            const mutex = waiter.mutex;
            if (waiter.node.next != &waiter.node) {
                @branchHint(.likely);
                mutex.waiters.remove(&waiter.node);
                assert(@atomicRmw(State, &mutex.state, .Sub, .{
                    .locked = false,
                    .num_waiters = 1,
                }, .monotonic).num_waiters >= 1);
            }
            waiter.node = undefined;
            waiter.wake();
        }

        fn remove(context: ?*anyopaque) callconv(.c) void {
            const mutex: *Mutex = @ptrCast(@alignCast(context));
            var state = @atomicLoad(State, &mutex.state, .monotonic);
            while (!state.locked and state.num_waiters > 0) {
                @branchHint(.likely);
                state = @cmpxchgWeak(State, &mutex.state, state, .{
                    .locked = true,
                    .num_waiters = state.num_waiters - 1,
                }, .acquire, .monotonic) orelse break;
            } else return;
            var num_removed: State.NumWaiters = 0;
            while (mutex.waiters.popFirst()) |node| {
                @branchHint(.likely);
                const waiter: *Waiter = @fieldParentPtr("node", node);
                node.* = undefined;
                waiter.cancelable.leave(waiter.sleeper.fiber) catch |err| switch (err) {
                    error.CancelRequested => {
                        num_removed += 1;
                        node.next = node;
                        continue;
                    },
                };
                break;
            }
            if (num_removed > 0) {
                @branchHint(.unlikely);
                assert(@atomicRmw(State, &mutex.state, .Sub, .{
                    .locked = false,
                    .num_waiters = num_removed,
                }, .monotonic).num_waiters >= num_removed);
            }
        }

        fn wake(waiter: *Waiter) void {
            Sleeper.wake(&waiter.sleeper);
        }
    };

    fn init(mutex: *Mutex, queue: c.dispatch.queue_t) error{SystemResources}!void {
        mutex.* = .{
            .state = .{ .locked = false, .num_waiters = 0 },
            .queue = c.dispatch.queue_create_with_target(
                "org.ziglang.std.Io.Dispatch.Mutex",
                .SERIAL(),
                queue,
            ) orelse return error.SystemResources,
            .waiters = .{},
        };
    }

    fn deinit(mutex: *Mutex) void {
        assert(mutex.state == State{ .locked = false, .num_waiters = 0 });
        assert(mutex.waiters.first == null and mutex.waiters.last == null);
        mutex.queue.as_object().release();
        mutex.* = undefined;
    }

    fn tryLock(mutex: *Mutex) bool {
        const state =
            @atomicRmw(State, &mutex.state, .Or, .{ .locked = true, .num_waiters = 0 }, .acquire);
        if (state.locked) {
            @branchHint(.unlikely);
        }
        return !state.locked;
    }

    fn lock(mutex: *Mutex, ev: *Evented) Io.Cancelable!void {
        if (mutex.tryLock()) return;
        var waiter: Waiter = .{
            .cancelable = .{ .queue = mutex.queue, .cancel = &Mutex.Waiter.canceled },
            .mutex = mutex,
        };
        ev.yield(.{ .mutex_wait = &waiter });
        try waiter.cancelable.acknowledge(waiter.sleeper.fiber);
    }

    fn lockUncancelable(mutex: *Mutex, ev: *Evented) void {
        if (mutex.tryLock()) return;
        var waiter: Waiter = .{ .cancelable = .blocked, .mutex = mutex };
        ev.yield(.{ .mutex_wait = &waiter });
        waiter.cancelable.acknowledge(waiter.sleeper.fiber) catch |err| switch (err) {
            error.Canceled => unreachable, // blocked
        };
    }

    fn unlock(mutex: *Mutex) void {
        const state = @atomicRmw(State, &mutex.state, .And, .{
            .locked = false,
            .num_waiters = std.math.maxInt(State.NumWaiters),
        }, .release);
        if (state.num_waiters > 0) {
            @branchHint(.unlikely);
            mutex.queue.async(mutex, &Waiter.remove);
        }
    }
};

fn crashHandler(userdata: ?*anyopaque) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const thread = &Thread.self;
    if (thread.current_context == null) std.process.abort();
    if (thread.current_context == &thread.main_context) std.process.abort();
    const fiber = thread.currentFiber();
    @atomicStore(
        Fiber.CancelStatus,
        &fiber.cancel_status,
        .{ .requested = true, .awaiting = .nothing },
        .monotonic,
    );
    fiber.cancel_protection = .{ .user = .blocked, .acknowledged = true };
}

const AsyncClosure = struct {
    evented: *Evented,
    fiber: *Fiber,
    start: *const fn (context: *const anyopaque, result: *anyopaque) void,
    result_align: Alignment,

    fn fromFiber(fiber: *Fiber) *AsyncClosure {
        return @ptrFromInt(Fiber.max_context_align.max(.of(AsyncClosure)).backward(
            @intFromPtr(fiber.allocatedEnd()) - Fiber.max_context_size,
        ) - @sizeOf(AsyncClosure));
    }

    fn contextPointer(closure: *AsyncClosure) [*]align(Fiber.max_context_align.toByteUnits()) u8 {
        return @alignCast(@as([*]u8, @ptrCast(closure)) + @sizeOf(AsyncClosure));
    }

    fn entry() callconv(.naked) void {
        switch (builtin.cpu.arch) {
            .aarch64 => asm volatile (
                \\ mov x0, sp
                \\ b %[call]
                :
                : [call] "X" (&call),
            ),
            .x86_64 => asm volatile (
                \\ leaq 8(%%rsp), %%rdi
                \\ jmp %[call:P]
                :
                : [call] "X" (&call),
            ),
            else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
        }
    }

    fn call(
        closure: *AsyncClosure,
        message: *const SwitchMessage,
    ) callconv(.withStackAlign(.c, @alignOf(AsyncClosure))) noreturn {
        const ev = closure.evented;
        const fiber = closure.fiber;
        message.handle(ev);
        closure.start(closure.contextPointer(), fiber.resultBytes(closure.result_align));
        if (@atomicRmw(?*Fiber, &fiber.link.awaiter, .Xchg, Fiber.finished, .acq_rel)) |awaiter|
            ev.queue.async(awaiter, &Fiber.@"resume");
        ev.yield(.nothing);
        unreachable; // switched to dead fiber
    }
};

fn async(
    userdata: ?*anyopaque,
    result: []u8,
    result_alignment: Alignment,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque, result: *anyopaque) void,
) ?*std.Io.AnyFuture {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return concurrent(ev, result.len, result_alignment, context, context_alignment, start) catch {
        start(context.ptr, result.ptr);
        return null;
    };
}

fn concurrent(
    userdata: ?*anyopaque,
    result_len: usize,
    result_alignment: Alignment,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque, result: *anyopaque) void,
) Io.ConcurrentError!*std.Io.AnyFuture {
    assert(result_alignment.co
```
