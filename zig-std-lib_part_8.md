```
32;
        const HandleTable = std.ArrayHashMapUnmanaged(FileHandle, struct { mount_id: MountId, reaction_set: ReactionSet }, FileHandle.Adapter, false);

        const fan_mask: std.os.linux.fanotify.MarkMask = .{
            .CLOSE_WRITE = true,
            .CREATE = true,
            .DELETE = true,
            .DELETE_SELF = true,
            .EVENT_ON_CHILD = true,
            .MOVED_FROM = true,
            .MOVED_TO = true,
            .MOVE_SELF = true,
            .ONDIR = true,
        };

        const FileHandle = struct {
            handle: *align(1) std.os.linux.file_handle,

            fn clone(lfh: FileHandle, gpa: Allocator) Allocator.Error!FileHandle {
                const bytes = lfh.slice();
                const new_ptr = try gpa.alignedAlloc(
                    u8,
                    .of(std.os.linux.file_handle),
                    @sizeOf(std.os.linux.file_handle) + bytes.len,
                );
                const new_header: *std.os.linux.file_handle = @ptrCast(new_ptr);
                new_header.* = lfh.handle.*;
                const new: FileHandle = .{ .handle = new_header };
                @memcpy(new.slice(), lfh.slice());
                return new;
            }

            fn destroy(lfh: FileHandle, gpa: Allocator) void {
                const ptr: [*]u8 = @ptrCast(lfh.handle);
                const allocated_slice = ptr[0 .. @sizeOf(std.os.linux.file_handle) + lfh.handle.handle_bytes];
                return gpa.free(allocated_slice);
            }

            fn slice(lfh: FileHandle) []u8 {
                const ptr: [*]u8 = &lfh.handle.f_handle;
                return ptr[0..lfh.handle.handle_bytes];
            }

            const Adapter = struct {
                pub fn hash(self: Adapter, a: FileHandle) u32 {
                    _ = self;
                    const unsigned_type: u32 = @bitCast(a.handle.handle_type);
                    return @truncate(Hash.hash(unsigned_type, a.slice()));
                }
                pub fn eql(self: Adapter, a: FileHandle, b: FileHandle, b_index: usize) bool {
                    _ = self;
                    _ = b_index;
                    return a.handle.handle_type == b.handle.handle_type and std.mem.eql(u8, a.slice(), b.slice());
                }
            };
        };

        fn init(cwd_path: []const u8) !Watch {
            _ = cwd_path;
            return .{
                .dir_table = .{},
                .dir_count = 0,
                .os = switch (builtin.os.tag) {
                    .linux => .{
                        .handle_table = .{},
                        .poll_fds = .{},
                    },
                    else => {},
                },
                .generation = 0,
            };
        }

        fn getDirHandle(gpa: Allocator, path: std.Build.Cache.Path, mount_id: *MountId) !FileHandle {
            var file_handle_buffer: [@sizeOf(std.os.linux.file_handle) + 128]u8 align(@alignOf(std.os.linux.file_handle)) = undefined;
            var buf: [std.fs.max_path_bytes]u8 = undefined;
            const adjusted_path = if (path.sub_path.len == 0) "./" else std.fmt.bufPrint(&buf, "{s}/", .{
                path.sub_path,
            }) catch return error.NameTooLong;
            const stack_ptr: *std.os.linux.file_handle = @ptrCast(&file_handle_buffer);
            stack_ptr.handle_bytes = file_handle_buffer.len - @sizeOf(std.os.linux.file_handle);
            try posix.name_to_handle_at(path.root_dir.handle.handle, adjusted_path, stack_ptr, mount_id, std.os.linux.AT.HANDLE_FID);
            const stack_lfh: FileHandle = .{ .handle = stack_ptr };
            return stack_lfh.clone(gpa);
        }

        fn markDirtySteps(w: *Watch, gpa: Allocator, fan_fd: posix.fd_t) !bool {
            const fanotify = std.os.linux.fanotify;
            const M = fanotify.event_metadata;
            var events_buf: [256 + 4096]u8 = undefined;
            var any_dirty = false;
            while (true) {
                var len = posix.read(fan_fd, &events_buf) catch |err| switch (err) {
                    error.WouldBlock => return any_dirty,
                    else => |e| return e,
                };
                var meta: [*]align(1) M = @ptrCast(&events_buf);
                while (len >= @sizeOf(M) and meta[0].event_len >= @sizeOf(M) and meta[0].event_len <= len) : ({
                    len -= meta[0].event_len;
                    meta = @ptrCast(@as([*]u8, @ptrCast(meta)) + meta[0].event_len);
                }) {
                    assert(meta[0].vers == M.VERSION);
                    if (meta[0].mask.Q_OVERFLOW) {
                        any_dirty = true;
                        std.log.warn("file system watch queue overflowed; falling back to fstat", .{});
                        markAllFilesDirty(w, gpa);
                        return true;
                    }
                    const fid: *align(1) fanotify.event_info_fid = @ptrCast(meta + 1);
                    switch (fid.hdr.info_type) {
                        .DFID_NAME => {
                            const file_handle: *align(1) std.os.linux.file_handle = @ptrCast(&fid.handle);
                            const file_name_z: [*:0]u8 = @ptrCast((&file_handle.f_handle).ptr + file_handle.handle_bytes);
                            const file_name = std.mem.span(file_name_z);
                            const lfh: FileHandle = .{ .handle = file_handle };
                            if (w.os.handle_table.getPtr(lfh)) |value| {
                                if (value.reaction_set.getPtr(".")) |glob_set|
                                    any_dirty = markStepSetDirty(gpa, glob_set, any_dirty);
                                if (value.reaction_set.getPtr(file_name)) |step_set|
                                    any_dirty = markStepSetDirty(gpa, step_set, any_dirty);
                            }
                        },
                        else => |t| std.log.warn("unexpected fanotify event '{s}'", .{@tagName(t)}),
                    }
                }
            }
        }

        fn update(w: *Watch, gpa: Allocator, steps: []const *Step) !void {
            // Add missing marks and note persisted ones.
            for (steps) |step| {
                for (step.inputs.table.keys(), step.inputs.table.values()) |path, *files| {
                    const reaction_set = rs: {
                        const gop = try w.dir_table.getOrPut(gpa, path);
                        if (!gop.found_existing) {
                            var mount_id: MountId = undefined;
                            const dir_handle = getDirHandle(gpa, path, &mount_id) catch |err| switch (err) {
                                error.FileNotFound => {
                                    std.debug.assert(w.dir_table.swapRemove(path));
                                    continue;
                                },
                                else => return err,
                            };
                            const fan_fd = blk: {
                                const fd_gop = try w.os.poll_fds.getOrPut(gpa, mount_id);
                                if (!fd_gop.found_existing) {
                                    const fan_fd = std.posix.fanotify_init(.{
                                        .CLASS = .NOTIF,
                                        .CLOEXEC = true,
                                        .NONBLOCK = true,
                                        .REPORT_NAME = true,
                                        .REPORT_DIR_FID = true,
                                        .REPORT_FID = true,
                                        .REPORT_TARGET_FID = true,
                                    }, 0) catch |err| switch (err) {
                                        error.UnsupportedFlags => fatal("fanotify_init failed due to old kernel; requires 5.17+", .{}),
                                        else => |e| return e,
                                    };
                                    fd_gop.value_ptr.* = .{
                                        .fd = fan_fd,
                                        .events = std.posix.POLL.IN,
                                        .revents = undefined,
                                    };
                                }
                                break :blk fd_gop.value_ptr.*.fd;
                            };
                            // `dir_handle` may already be present in the table in
                            // the case that we have multiple Cache.Path instances
                            // that compare inequal but ultimately point to the same
                            // directory on the file system.
                            // In such case, we must revert adding this directory, but keep
                            // the additions to the step set.
                            const dh_gop = try w.os.handle_table.getOrPut(gpa, dir_handle);
                            if (dh_gop.found_existing) {
                                _ = w.dir_table.pop();
                            } else {
                                assert(dh_gop.index == gop.index);
                                dh_gop.value_ptr.* = .{ .mount_id = mount_id, .reaction_set = .{} };
                                posix.fanotify_mark(fan_fd, .{
                                    .ADD = true,
                                    .ONLYDIR = true,
                                }, fan_mask, path.root_dir.handle.handle, path.subPathOrDot()) catch |err| {
                                    fatal("unable to watch {f}: {s}", .{ path, @errorName(err) });
                                };
                            }
                            break :rs &dh_gop.value_ptr.reaction_set;
                        }
                        break :rs &w.os.handle_table.values()[gop.index].reaction_set;
                    };
                    for (files.items) |basename| {
                        const gop = try reaction_set.getOrPut(gpa, basename);
                        if (!gop.found_existing) gop.value_ptr.* = .{};
                        try gop.value_ptr.put(gpa, step, w.generation);
                    }
                }
            }

            {
                // Remove marks for files that are no longer inputs.
                var i: usize = 0;
                while (i < w.os.handle_table.entries.len) {
                    {
                        const reaction_set = &w.os.handle_table.values()[i].reaction_set;
                        var step_set_i: usize = 0;
                        while (step_set_i < reaction_set.entries.len) {
                            const step_set = &reaction_set.values()[step_set_i];
                            var dirent_i: usize = 0;
                            while (dirent_i < step_set.entries.len) {
                                const generations = step_set.values();
                                if (generations[dirent_i] == w.generation) {
                                    dirent_i += 1;
                                    continue;
                                }
                                step_set.swapRemoveAt(dirent_i);
                            }
                            if (step_set.entries.len > 0) {
                                step_set_i += 1;
                                continue;
                            }
                            reaction_set.swapRemoveAt(step_set_i);
                        }
                        if (reaction_set.entries.len > 0) {
                            i += 1;
                            continue;
                        }
                    }

                    const path = w.dir_table.keys()[i];

                    const mount_id = w.os.handle_table.values()[i].mount_id;
                    const fan_fd = w.os.poll_fds.getEntry(mount_id).?.value_ptr.fd;
                    posix.fanotify_mark(fan_fd, .{
                        .REMOVE = true,
                        .ONLYDIR = true,
                    }, fan_mask, path.root_dir.handle.handle, path.subPathOrDot()) catch |err| switch (err) {
                        error.FileNotFound => {}, // Expected, harmless.
                        else => |e| std.log.warn("unable to unwatch '{f}': {s}", .{ path, @errorName(e) }),
                    };

                    w.dir_table.swapRemoveAt(i);
                    w.os.handle_table.swapRemoveAt(i);
                }
                w.generation +%= 1;
            }
            w.dir_count = w.dir_table.count();
        }

        fn wait(w: *Watch, gpa: Allocator, io: Io, timeout: Timeout) !WaitResult {
            _ = io;
            const events_len = try std.posix.poll(w.os.poll_fds.values(), timeout.to_i32_ms());
            if (events_len == 0)
                return .timeout;
            for (w.os.poll_fds.values()) |poll_fd| {
                if (poll_fd.revents & std.posix.POLL.IN == std.posix.POLL.IN and try markDirtySteps(w, gpa, poll_fd.fd))
                    return .dirty;
            }
            return .clean;
        }
    },
    .windows => struct {
        const windows = std.os.windows;

        /// Keyed differently but indexes correspond 1:1 with `dir_table`.
        handle_table: std.ArrayHashMapUnmanaged(*Directory, void, Directory.TableAdapter, false),
        ready_dirs: std.DoublyLinkedList,

        const FileId = struct {
            volumeSerialNumber: windows.ULONG,
            indexNumber: windows.LARGE_INTEGER,
        };

        const Directory = struct {
            reaction_set: ReactionSet,
            id: FileId,
            file: Io.File,
            state: enum { idle, listening, ready },
            iosb: windows.IO_STATUS_BLOCK,
            // 64 KB is the packet size limit when monitoring over a network.
            // https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-readdirectorychangesw#remarks
            buffer: [64 * 1024]u8 align(@alignOf(windows.FILE.NOTIFY.INFORMATION)),
            ready_node: std.DoublyLinkedList.Node,

            /// Start listening for events, buffer field will be overwritten eventually.
            fn startListening(dir: *Directory, w: *Watch) !void {
                assert(dir.file.flags.nonblocking);
                assert(dir.state == .idle);
                switch (windows.ntdll.NtNotifyChangeDirectoryFileEx(
                    dir.file.handle,
                    null,
                    &notifyApc,
                    w,
                    &dir.iosb,
                    &dir.buffer,
                    dir.buffer.len,
                    .{
                        .FILE_NAME = true,
                        .DIR_NAME = true,
                        .SIZE = true,
                        .LAST_WRITE = true,
                        .CREATION = true,
                    },
                    .FALSE,
                    .Notify,
                )) {
                    .SUCCESS, .PENDING => dir.state = .listening,
                    .ILLEGAL_FUNCTION => return error.ReadDirectoryChangesUnsupported,
                    else => |status| return windows.unexpectedStatus(status),
                }
            }

            fn notifyApc(apc_context: ?*anyopaque, iosb: *windows.IO_STATUS_BLOCK, _: windows.ULONG) align(std.Io.Threaded.apc_align) callconv(.winapi) void {
                const w: *Watch = @ptrCast(@alignCast(apc_context));
                const dir: *Directory = @fieldParentPtr("iosb", iosb);
                assert(iosb.u.Status != .PENDING);
                assert(dir.state == .listening);
                w.os.ready_dirs.append(&dir.ready_node);
                dir.state = .ready;
            }

            fn init(gpa: Allocator, path: Cache.Path) !*Directory {
                // The following code is a drawn out NtCreateFile call. (mostly adapted from Io.Dir.makeOpenDirAccessMaskW)
                // It's necessary in order to get the specific flags that are required when calling ReadDirectoryChangesW.
                var dir_handle: windows.HANDLE = undefined;
                const root_fd = path.root_dir.handle.handle;
                const sub_path = path.subPathOrDot();
                const sub_path_w = try Io.Threaded.sliceToPrefixedFileW(root_fd, sub_path, .{}); // TODO eliminate this call
                var iosb: windows.IO_STATUS_BLOCK = undefined;
                switch (windows.ntdll.NtCreateFile(
                    &dir_handle,
                    .{
                        .SPECIFIC = .{ .FILE_DIRECTORY = .{
                            .LIST = true,
                        } },
                        .STANDARD = .{ .SYNCHRONIZE = true },
                        .GENERIC = .{ .READ = true },
                    },
                    &.{
                        .RootDirectory = if (std.fs.path.isAbsoluteWindowsW(sub_path_w.span())) null else root_fd,
                        .ObjectName = @constCast(&sub_path_w.string()),
                    },
                    &iosb,
                    null,
                    .{},
                    .VALID_FLAGS,
                    .OPEN,
                    .{
                        .DIRECTORY_FILE = true,
                        .IO = .ASYNCHRONOUS,
                        .OPEN_FOR_BACKUP_INTENT = true,
                    },
                    null,
                    0,
                )) {
                    .SUCCESS => {},
                    .OBJECT_NAME_INVALID => return error.BadPathName,
                    .OBJECT_NAME_NOT_FOUND => return error.FileNotFound,
                    .OBJECT_NAME_COLLISION => return error.PathAlreadyExists,
                    .OBJECT_PATH_NOT_FOUND => return error.FileNotFound,
                    .NOT_A_DIRECTORY => return error.NotDir,
                    // This can happen if the directory has 'List folder contents' permission set to 'Deny'
                    .ACCESS_DENIED => return error.AccessDenied,
                    .INVALID_PARAMETER => unreachable,
                    else => |rc| return windows.unexpectedStatus(rc),
                }
                assert(dir_handle != windows.INVALID_HANDLE_VALUE);
                errdefer windows.CloseHandle(dir_handle);

                const dir_id = try getFileId(dir_handle);

                const dir = try gpa.create(Directory);
                dir.* = .{
                    .reaction_set = .empty,
                    .id = dir_id,
                    .file = .{ .handle = dir_handle, .flags = .{ .nonblocking = true } },
                    .state = .idle,
                    .iosb = undefined,
                    .buffer = undefined,
                    .ready_node = undefined,
                };
                return dir;
            }

            fn deinit(dir: *Directory, gpa: Allocator, w: *Watch) void {
                state: switch (dir.state) {
                    .idle => {},
                    .listening => {
                        var cancel_iosb: windows.IO_STATUS_BLOCK = undefined;
                        _ = windows.ntdll.NtCancelIoFileEx(dir.file.handle, &dir.iosb, &cancel_iosb);
                        while (switch (dir.state) {
                            .idle => unreachable,
                            .listening => true,
                            .ready => false,
                        }) Io.Threaded.waitForApcOrAlert();
                        continue :state .ready;
                    },
                    .ready => w.os.ready_dirs.remove(&dir.ready_node),
                }
                windows.CloseHandle(dir.file.handle);
                gpa.destroy(dir);
            }

            /// Useful to make `*Directory` a key in `std.ArrayHashMap`.
            const TableAdapter = struct {
                pub fn hash(_: TableAdapter, lhs_dir: *Directory) u32 {
                    return @truncate(Hash.hash(lhs_dir.id.volumeSerialNumber, @ptrCast(&lhs_dir.id.indexNumber)));
                }
                pub fn eql(_: TableAdapter, lhs_dir: *Directory, rhs_dir: *Directory, rhs_index: usize) bool {
                    _ = rhs_index;
                    return lhs_dir.id.volumeSerialNumber == rhs_dir.id.volumeSerialNumber and
                        lhs_dir.id.indexNumber == rhs_dir.id.indexNumber;
                }
            };
        };

        fn init(cwd_path: []const u8) !Watch {
            _ = cwd_path;
            return .{
                .dir_table = .{},
                .dir_count = 0,
                .os = switch (builtin.os.tag) {
                    .windows => .{
                        .handle_table = .empty,
                        .ready_dirs = .{},
                    },
                    else => {},
                },
                .generation = 0,
            };
        }

        fn getFileId(handle: windows.HANDLE) !FileId {
            var file_id: FileId = undefined;
            var io_status: windows.IO_STATUS_BLOCK = undefined;
            var volume_info: windows.FILE.FS_VOLUME_INFORMATION = undefined;
            switch (windows.ntdll.NtQueryVolumeInformationFile(
                handle,
                &io_status,
                &volume_info,
                @sizeOf(windows.FILE.FS_VOLUME_INFORMATION),
                .Volume,
            )) {
                .SUCCESS => {},
                // Buffer overflow here indicates that there is more information available than was able to be stored in the buffer
                // size provided. This is treated as success because the type of variable-length information that this would be relevant for
                // (name, volume name, etc) we don't care about.
                .BUFFER_OVERFLOW => {},
                else => |rc| return windows.unexpectedStatus(rc),
            }
            file_id.volumeSerialNumber = volume_info.VolumeSerialNumber;
            var internal_info: windows.FILE.INTERNAL_INFORMATION = undefined;
            switch (windows.ntdll.NtQueryInformationFile(
                handle,
                &io_status,
                &internal_info,
                @sizeOf(windows.FILE.INTERNAL_INFORMATION),
                .Internal,
            )) {
                .SUCCESS => {},
                else => |rc| return windows.unexpectedStatus(rc),
            }
            file_id.indexNumber = internal_info.IndexNumber;
            return file_id;
        }

        fn markDirtySteps(w: *Watch, gpa: Allocator, dir: *Directory) !bool {
            var any_dirty = false;
            const bytes_returned = dir.iosb.Information;
            if (bytes_returned == 0) {
                std.log.warn("file system watch queue overflowed; falling back to fstat", .{});
                markAllFilesDirty(w, gpa);
                try dir.startListening(w);
                return true;
            }
            var file_name_buf: [std.fs.max_path_bytes]u8 = undefined;
            var offset: usize = 0;
            while (true) {
                const notify: *windows.FILE.NOTIFY.INFORMATION = @ptrCast(@alignCast(&dir.buffer[offset]));
                const file_name = file_name_buf[0..std.unicode.wtf16LeToWtf8(&file_name_buf, notify.fileName())];
                if (dir.reaction_set.getPtr(".")) |glob_set|
                    any_dirty = markStepSetDirty(gpa, glob_set, any_dirty);
                if (dir.reaction_set.getPtr(file_name)) |step_set|
                    any_dirty = markStepSetDirty(gpa, step_set, any_dirty);
                if (notify.NextEntryOffset == 0)
                    break;

                offset += notify.NextEntryOffset;
            }

            // We call this now since at this point we have finished reading dir.buffer.
            try dir.startListening(w);
            return any_dirty;
        }

        fn update(w: *Watch, gpa: Allocator, steps: []const *Step) !void {
            // Add missing marks and note persisted ones.
            for (steps) |step| {
                for (step.inputs.table.keys(), step.inputs.table.values()) |path, *files| {
                    const dir = dir: {
                        const gop = try w.dir_table.getOrPut(gpa, path);
                        if (!gop.found_existing) {
                            const dir: *Directory = try .init(gpa, path);
                            errdefer dir.deinit(gpa, w);
                            // `dir.id` may already be present in the table in
                            // the case that we have multiple Cache.Path instances
                            // that compare inequal but ultimately point to the same
                            // directory on the file system.
                            // In such case, we must revert adding this directory, but keep
                            // the additions to the step set.
                            const dh_gop = try w.os.handle_table.getOrPut(gpa, dir);
                            if (dh_gop.found_existing) {
                                dir.deinit(gpa, w);
                                _ = w.dir_table.pop();
                                break :dir w.os.handle_table.keys()[dh_gop.index];
                            } else {
                                assert(dh_gop.index == gop.index);
                                try dir.startListening(w);
                                break :dir dir;
                            }
                        }
                        break :dir w.os.handle_table.keys()[gop.index];
                    };
                    for (files.items) |basename| {
                        const gop = try dir.reaction_set.getOrPut(gpa, basename);
                        if (!gop.found_existing) gop.value_ptr.* = .{};
                        try gop.value_ptr.put(gpa, step, w.generation);
                    }
                }
            }

            {
                // Remove marks for files that are no longer inputs.
                var i: usize = 0;
                while (i < w.os.handle_table.entries.len) {
                    const dir = w.os.handle_table.keys()[i];
                    {
                        var step_set_i: usize = 0;
                        while (step_set_i < dir.reaction_set.entries.len) {
                            const step_set = &dir.reaction_set.values()[step_set_i];
                            var dirent_i: usize = 0;
                            while (dirent_i < step_set.entries.len) {
                                const generations = step_set.values();
                                if (generations[dirent_i] == w.generation) {
                                    dirent_i += 1;
                                    continue;
                                }
                                step_set.swapRemoveAt(dirent_i);
                            }
                            if (step_set.entries.len > 0) {
                                step_set_i += 1;
                                continue;
                            }
                            dir.reaction_set.swapRemoveAt(step_set_i);
                        }
                        if (dir.reaction_set.entries.len > 0) {
                            i += 1;
                            continue;
                        }
                    }

                    w.dir_table.swapRemoveAt(i);
                    w.os.handle_table.swapRemoveAt(i);
                    dir.deinit(gpa, w);
                }
                w.generation +%= 1;
            }
            w.dir_count = w.dir_table.count();
        }

        fn wait(w: *Watch, gpa: Allocator, io: Io, timeout: Timeout) !WaitResult {
            for (0..2) |attempt| {
                while (w.os.ready_dirs.popFirst()) |ready_node| {
                    const dir: *Directory = @fieldParentPtr("ready_node", ready_node);
                    assert(dir.state == .ready);
                    dir.state = .idle;
                    switch (dir.iosb.u.Status) {
                        .SUCCESS => return if (try markDirtySteps(w, gpa, dir)) .dirty else .clean,
                        .PENDING => unreachable,
                        .CANCELLED => {},
                        else => |status| return windows.unexpectedStatus(status),
                    }
                    try dir.startListening(w);
                }
                try io.checkCancel();
                if (attempt == 1) return .timeout;
                const delay_interval: windows.LARGE_INTEGER = switch (timeout) {
                    .none => std.math.minInt(windows.LARGE_INTEGER),
                    .ms => |ms| -@as(windows.LARGE_INTEGER, ms) * (std.time.ns_per_ms / 100),
                };
                _ = windows.ntdll.NtDelayExecution(.TRUE, &delay_interval);
            } else unreachable;
        }
    },
    .dragonfly, .freebsd, .netbsd, .openbsd, .ios, .tvos, .visionos, .watchos => struct {
        const posix = std.posix;

        kq_fd: i32,
        /// Indexes correspond 1:1 with `dir_table`.
        handles: std.MultiArrayList(struct {
            rs: ReactionSet,
            /// If the corresponding dir_table Path has sub_path == "", then it
            /// suffices as the open directory handle, and this value will be
            /// -1. Otherwise, it needs to be opened in update(), and will be
            /// stored here.
            dir_fd: i32,
        }),

        const dir_open_flags: posix.O = f: {
            var f: posix.O = .{
                .ACCMODE = .RDONLY,
                .NOFOLLOW = false,
                .DIRECTORY = true,
                .CLOEXEC = true,
            };
            if (@hasField(posix.O, "EVTONLY")) f.EVTONLY = true;
            if (@hasField(posix.O, "PATH")) f.PATH = true;
            break :f f;
        };

        const EV = std.c.EV;
        const NOTE = std.c.NOTE;

        fn init(cwd_path: []const u8) !Watch {
            _ = cwd_path;
            return .{
                .dir_table = .{},
                .dir_count = 0,
                .os = .{
                    .kq_fd = try Io.Kqueue.createFileDescriptor(),
                    .handles = .empty,
                },
                .generation = 0,
            };
        }

        fn update(w: *Watch, gpa: Allocator, steps: []const *Step) !void {
            const handles = &w.os.handles;
            for (steps) |step| {
                for (step.inputs.table.keys(), step.inputs.table.values()) |path, *files| {
                    const reaction_set = rs: {
                        const gop = try w.dir_table.getOrPut(gpa, path);
                        if (!gop.found_existing) {
                            const skip_open_dir = path.sub_path.len == 0;
                            const dir_fd = if (skip_open_dir)
                                path.root_dir.handle.handle
                            else
                                posix.openat(path.root_dir.handle.handle, path.sub_path, dir_open_flags, 0) catch |err| {
                                    fatal("failed to open directory {f}: {t}", .{ path, err });
                                };
                            // Empirically the dir has to stay open or else no events are triggered.
                            errdefer if (!skip_open_dir) std.Io.Threaded.closeFd(dir_fd);
                            const changes = [1]posix.Kevent{.{
                                .ident = @bitCast(@as(isize, dir_fd)),
                                .filter = std.c.EVFILT.VNODE,
                                .flags = EV.ADD | EV.ENABLE | EV.CLEAR,
                                .fflags = NOTE.DELETE | NOTE.WRITE | NOTE.RENAME | NOTE.REVOKE,
                                .data = 0,
                                .udata = gop.index,
                            }};
                            _ = try Io.Kqueue.kevent(w.os.kq_fd, &changes, &.{}, null);
                            assert(handles.len == gop.index);
                            try handles.append(gpa, .{
                                .rs = .{},
                                .dir_fd = if (skip_open_dir) -1 else dir_fd,
                            });
                        }

                        break :rs &handles.items(.rs)[gop.index];
                    };
                    for (files.items) |basename| {
                        const gop = try reaction_set.getOrPut(gpa, basename);
                        if (!gop.found_existing) gop.value_ptr.* = .{};
                        try gop.value_ptr.put(gpa, step, w.generation);
                    }
                }
            }

            {
                // Remove marks for files that are no longer inputs.
                var i: usize = 0;
                while (i < handles.len) {
                    {
                        const reaction_set = &handles.items(.rs)[i];
                        var step_set_i: usize = 0;
                        while (step_set_i < reaction_set.entries.len) {
                            const step_set = &reaction_set.values()[step_set_i];
                            var dirent_i: usize = 0;
                            while (dirent_i < step_set.entries.len) {
                                const generations = step_set.values();
                                if (generations[dirent_i] == w.generation) {
                                    dirent_i += 1;
                                    continue;
                                }
                                step_set.swapRemoveAt(dirent_i);
                            }
                            if (step_set.entries.len > 0) {
                                step_set_i += 1;
                                continue;
                            }
                            reaction_set.swapRemoveAt(step_set_i);
                        }
                        if (reaction_set.entries.len > 0) {
                            i += 1;
                            continue;
                        }
                    }

                    // If the sub_path == "" then this patch has already the
                    // dir fd that we need to use as the ident to remove the
                    // event. If it was opened above with openat() then we need
                    // to access that data via the dir_fd field.
                    const path = w.dir_table.keys()[i];
                    const dir_fd = if (path.sub_path.len == 0)
                        path.root_dir.handle.handle
                    else
                        handles.items(.dir_fd)[i];
                    assert(dir_fd != -1);

                    // The changelist also needs to update the udata field of the last
                    // event, since we are doing a swap remove, and we store the dir_table
                    // index in the udata field.
                    const last_dir_fd = fd: {
                        const last_path = w.dir_table.keys()[handles.len - 1];
                        const last_dir_fd = if (last_path.sub_path.len == 0)
                            last_path.root_dir.handle.handle
                        else
                            handles.items(.dir_fd)[handles.len - 1];
                        assert(last_dir_fd != -1);
                        break :fd last_dir_fd;
                    };
                    const changes = [_]posix.Kevent{
                        .{
                            .ident = @bitCast(@as(isize, dir_fd)),
                            .filter = std.c.EVFILT.VNODE,
                            .flags = EV.DELETE,
                            .fflags = 0,
                            .data = 0,
                            .udata = i,
                        },
                        .{
                            .ident = @bitCast(@as(isize, last_dir_fd)),
                            .filter = std.c.EVFILT.VNODE,
                            .flags = EV.ADD,
                            .fflags = NOTE.DELETE | NOTE.WRITE | NOTE.RENAME | NOTE.REVOKE,
                            .data = 0,
                            .udata = i,
                        },
                    };
                    const filtered_changes = if (i == handles.len - 1) changes[0..1] else &changes;
                    _ = try Io.Kqueue.kevent(w.os.kq_fd, filtered_changes, &.{}, null);
                    if (path.sub_path.len != 0) std.Io.Threaded.closeFd(dir_fd);

                    w.dir_table.swapRemoveAt(i);
                    handles.swapRemove(i);
                }
                w.generation +%= 1;
            }
            w.dir_count = w.dir_table.count();
        }

        fn wait(w: *Watch, gpa: Allocator, io: Io, timeout: Timeout) !WaitResult {
            _ = io;
            var timespec_buffer: posix.timespec = undefined;
            var event_buffer: [100]posix.Kevent = undefined;
            var n = try Io.Kqueue.kevent(w.os.kq_fd, &.{}, &event_buffer, timeout.toTimespec(&timespec_buffer));
            if (n == 0) return .timeout;
            const reaction_sets = w.os.handles.items(.rs);
            var any_dirty = markDirtySteps(gpa, reaction_sets, event_buffer[0..n], false);
            timespec_buffer = .{ .sec = 0, .nsec = 0 };
            while (n == event_buffer.len) {
                n = try Io.Kqueue.kevent(w.os.kq_fd, &.{}, &event_buffer, &timespec_buffer);
                if (n == 0) break;
                any_dirty = markDirtySteps(gpa, reaction_sets, event_buffer[0..n], any_dirty);
            }
            return if (any_dirty) .dirty else .clean;
        }

        fn markDirtySteps(
            gpa: Allocator,
            reaction_sets: []ReactionSet,
            events: []const std.c.Kevent,
            start_any_dirty: bool,
        ) bool {
            var any_dirty = start_any_dirty;
            for (events) |event| {
                const index: usize = @intCast(event.udata);
                const reaction_set = &reaction_sets[index];
                // If we knew the basename of the changed file, here we would
                // mark only the step set dirty, and possibly the glob set:
                //if (reaction_set.getPtr(".")) |glob_set|
                //    any_dirty = markStepSetDirty(gpa, glob_set, any_dirty);
                //if (reaction_set.getPtr(file_name)) |step_set|
                //    any_dirty = markStepSetDirty(gpa, step_set, any_dirty);
                // However we don't know the file name so just mark all the
                // sets dirty for this directory.
                for (reaction_set.values()) |*step_set| {
                    any_dirty = markStepSetDirty(gpa, step_set, any_dirty);
                }
            }
            return any_dirty;
        }
    },
    .macos => struct {
        fse: FsEvents,

        fn init(cwd_path: []const u8) !Watch {
            return .{
                .os = .{ .fse = try .init(cwd_path) },
                .dir_count = 0,
                .dir_table = undefined,
                .generation = undefined,
            };
        }
        fn update(w: *Watch, gpa: Allocator, steps: []const *Step) !void {
            try w.os.fse.setPaths(gpa, steps);
            w.dir_count = w.os.fse.watch_roots.len;
        }
        fn wait(w: *Watch, gpa: Allocator, io: Io, timeout: Timeout) !WaitResult {
            _ = io;
            return w.os.fse.wait(gpa, switch (timeout) {
                .none => null,
                .ms => |ms| @as(u64, ms) * std.time.ns_per_ms,
            });
        }
    },
    else => void,
};

pub fn init(cwd_path: []const u8) !Watch {
    return Os.init(cwd_path);
}

pub const Match = struct {
    /// Relative to the watched directory, the file path that triggers this
    /// match.
    basename: []const u8,
    /// The step to re-run when file corresponding to `basename` is changed.
    step: *Step,

    pub const Context = struct {
        pub fn hash(self: Context, a: Match) u32 {
            _ = self;
            var hasher = Hash.init(0);
            std.hash.autoHash(&hasher, a.step);
            hasher.update(a.basename);
            return @truncate(hasher.final());
        }
        pub fn eql(self: Context, a: Match, b: Match, b_index: usize) bool {
            _ = self;
            _ = b_index;
            return a.step == b.step and std.mem.eql(u8, a.basename, b.basename);
        }
    };
};

fn markAllFilesDirty(w: *Watch, gpa: Allocator) void {
    for (switch (builtin.os.tag) {
        .windows => w.os.handle_table.keys(),
        else => w.os.handle_table.values(),
    }) |item| {
        const reaction_set = switch (builtin.os.tag) {
            .linux, .windows => item.reaction_set,
            else => item,
        };
        for (reaction_set.values()) |step_set| {
            for (step_set.keys()) |step| {
                _ = step.invalidateResult(gpa);
            }
        }
    }
}

fn markStepSetDirty(gpa: Allocator, step_set: *StepSet, any_dirty: bool) bool {
    var this_any_dirty = false;
    for (step_set.keys()) |step| {
        if (step.invalidateResult(gpa)) this_any_dirty = true;
    }
    return any_dirty or this_any_dirty;
}

pub fn update(w: *Watch, gpa: Allocator, steps: []const *Step) !void {
    return Os.update(w, gpa, steps);
}

pub const Timeout = union(enum) {
    none,
    ms: u16,

    pub fn to_i32_ms(t: Timeout) i32 {
        return switch (t) {
            .none => -1,
            .ms => |ms| ms,
        };
    }

    pub fn toTimespec(t: Timeout, buf: *std.posix.timespec) ?*std.posix.timespec {
        return switch (t) {
            .none => null,
            .ms => |ms_u16| {
                const ms: isize = ms_u16;
                buf.* = .{
                    .sec = @divTrunc(ms, std.time.ms_per_s),
                    .nsec = @rem(ms, std.time.ms_per_s) * std.time.ns_per_ms,
                };
                return buf;
            },
        };
    }
};

pub const WaitResult = enum {
    timeout,
    /// File system watching triggered on files that were marked as inputs to at least one Step.
    /// Relevant steps have been marked dirty.
    dirty,
    /// File system watching triggered but none of the events were relevant to
    /// what we are listening to. There is nothing to do.
    clean,
};

pub fn wait(w: *Watch, gpa: Allocator, io: Io, timeout: Timeout) !WaitResult {
    return Os.wait(w, gpa, io, timeout);
}



---
File: /std/Build/WebServer.zig
---

gpa: Allocator,
graph: *const Build.Graph,
all_steps: []const *Build.Step,
listen_address: net.IpAddress,
root_prog_node: std.Progress.Node,
watch: bool,

tcp_server: ?net.Server,
serve_task: ?Io.Future(Io.Cancelable!void),

/// Uses `Io.Clock.awake`.
base_timestamp: Io.Timestamp,
/// The "step name" data which trails `abi.Hello`, for the steps in `all_steps`.
step_names_trailing: []u8,

/// The bit-packed "step status" data. Values are `abi.StepUpdate.Status`. LSBs are earlier steps.
/// Accessed atomically.
step_status_bits: []u8,

fuzz: ?Fuzz,
time_report_mutex: Io.Mutex,
time_report_msgs: [][]u8,
time_report_update_times: []i64,

build_status: std.atomic.Value(abi.BuildStatus),
/// When an event occurs which means WebSocket clients should be sent updates, call `notifyUpdate`
/// to increment this value. Each client thread waits for this increment with `Io.futexWaitTimeout`, so
/// `notifyUpdate` will wake those threads. Updates are sent on a short interval regardless, so it
/// is recommended to only use `notifyUpdate` for changes which the user should see immediately. For
/// instance, we do not call `notifyUpdate` when the number of "unique runs" in the fuzzer changes,
/// because this value changes quickly so this would result in constantly spamming all clients with
/// an unreasonable number of packets.
update_id: std.atomic.Value(u32),

runner_request_mutex: Io.Mutex,
runner_request_ready_cond: Io.Condition,
runner_request_empty_cond: Io.Condition,
runner_request: ?RunnerRequest,

/// If a client is not explicitly notified of changes with `notifyUpdate`, it will be sent updates
/// on a fixed interval of this many milliseconds.
const default_update_interval_ms = 500;

pub const base_clock: Io.Clock = .awake;

/// Thread-safe. Triggers updates to be sent to connected WebSocket clients; see `update_id`.
pub fn notifyUpdate(ws: *WebServer) void {
    _ = ws.update_id.rmw(.Add, 1, .release);
    ws.graph.io.futexWake(u32, &ws.update_id.raw, 16);
}

pub const Options = struct {
    gpa: Allocator,
    graph: *const std.Build.Graph,
    all_steps: []const *Build.Step,
    root_prog_node: std.Progress.Node,
    watch: bool,
    listen_address: net.IpAddress,
    base_timestamp: Io.Clock.Timestamp,
};
pub fn init(opts: Options) WebServer {
    // The upcoming `Io` interface should allow us to use `Io.async` and `Io.concurrent`
    // instead of threads, so that the web server can function in single-threaded builds.
    comptime assert(!builtin.single_threaded);
    assert(opts.base_timestamp.clock == base_clock);

    const all_steps = opts.all_steps;

    const step_names_trailing = opts.gpa.alloc(u8, len: {
        var name_bytes: usize = 0;
        for (all_steps) |step| name_bytes += step.name.len;
        break :len name_bytes + all_steps.len * 4;
    }) catch @panic("out of memory");
    {
        const step_name_lens: []align(1) u32 = @ptrCast(step_names_trailing[0 .. all_steps.len * 4]);
        var idx: usize = all_steps.len * 4;
        for (all_steps, step_name_lens) |step, *name_len| {
            name_len.* = @intCast(step.name.len);
            @memcpy(step_names_trailing[idx..][0..step.name.len], step.name);
            idx += step.name.len;
        }
        assert(idx == step_names_trailing.len);
    }

    const step_status_bits = opts.gpa.alloc(
        u8,
        std.math.divCeil(usize, all_steps.len, 4) catch unreachable,
    ) catch @panic("out of memory");
    @memset(step_status_bits, 0);

    const time_reports_len: usize = if (opts.graph.time_report) all_steps.len else 0;
    const time_report_msgs = opts.gpa.alloc([]u8, time_reports_len) catch @panic("out of memory");
    const time_report_update_times = opts.gpa.alloc(i64, time_reports_len) catch @panic("out of memory");
    @memset(time_report_msgs, &.{});
    @memset(time_report_update_times, std.math.minInt(i64));

    return .{
        .gpa = opts.gpa,
        .graph = opts.graph,
        .all_steps = all_steps,
        .listen_address = opts.listen_address,
        .root_prog_node = opts.root_prog_node,
        .watch = opts.watch,

        .tcp_server = null,
        .serve_task = null,

        .base_timestamp = opts.base_timestamp.raw,
        .step_names_trailing = step_names_trailing,

        .step_status_bits = step_status_bits,

        .fuzz = null,
        .time_report_mutex = .init,
        .time_report_msgs = time_report_msgs,
        .time_report_update_times = time_report_update_times,

        .build_status = .init(.idle),
        .update_id = .init(0),

        .runner_request_mutex = .init,
        .runner_request_ready_cond = .init,
        .runner_request_empty_cond = .init,
        .runner_request = null,
    };
}
pub fn deinit(ws: *WebServer) void {
    const gpa = ws.gpa;
    const io = ws.graph.io;

    gpa.free(ws.step_names_trailing);
    gpa.free(ws.step_status_bits);

    if (ws.fuzz) |*f| f.deinit();
    for (ws.time_report_msgs) |msg| gpa.free(msg);
    gpa.free(ws.time_report_msgs);
    gpa.free(ws.time_report_update_times);

    if (ws.serve_task) |t| {
        if (ws.tcp_server) |*s| s.stream.close(io);
        t.await();
    }
    if (ws.tcp_server) |*s| s.deinit();

    gpa.free(ws.step_names_trailing);
}
pub fn start(ws: *WebServer) error{AlreadyReported}!void {
    assert(ws.tcp_server == null);
    assert(ws.serve_task == null);
    const io = ws.graph.io;

    ws.tcp_server = ws.listen_address.listen(io, .{ .reuse_address = true }) catch |err| {
        log.err("failed to listen to port {d}: {t}", .{ ws.listen_address.getPort(), err });
        return error.AlreadyReported;
    };
    ws.serve_task = io.concurrent(serve, .{ws}) catch |err| {
        log.err("unable to spawn web server thread: {t}", .{err});
        ws.tcp_server.?.deinit(io);
        ws.tcp_server = null;
        return error.AlreadyReported;
    };

    log.info("web interface listening at http://{f}/", .{ws.tcp_server.?.socket.address});
    if (ws.listen_address.getPort() == 0) {
        log.info("hint: pass '--webui={f}' to use the same port next time", .{ws.tcp_server.?.socket.address});
    }
}
fn serve(ws: *WebServer) Io.Cancelable!void {
    const io = ws.graph.io;
    var group: Io.Group = .init;
    defer group.cancel(io);
    while (true) {
        var stream = ws.tcp_server.?.accept(io) catch |err| switch (err) {
            error.Canceled => |e| return e,
            else => |e| {
                log.err("failed to accept connection: {t}", .{e});
                return;
            },
        };
        group.concurrent(io, accept, .{ ws, stream }) catch |err| {
            log.err("unable to spawn connection thread: {t}", .{err});
            stream.close(io);
            continue;
        };
    }
}

pub fn startBuild(ws: *WebServer) void {
    if (ws.fuzz) |*fuzz| {
        fuzz.deinit();
        ws.fuzz = null;
    }
    for (ws.step_status_bits) |*bits| @atomicStore(u8, bits, 0, .monotonic);
    ws.build_status.store(.running, .monotonic);
    ws.notifyUpdate();
}

pub fn updateStepStatus(ws: *WebServer, step: *Build.Step, new_status: abi.StepUpdate.Status) void {
    const step_idx: u32 = for (ws.all_steps, 0..) |s, i| {
        if (s == step) break @intCast(i);
    } else unreachable;
    const ptr = &ws.step_status_bits[step_idx / 4];
    const bit_offset: u3 = @intCast((step_idx % 4) * 2);
    const old_bits: u2 = @truncate(@atomicLoad(u8, ptr, .monotonic) >> bit_offset);
    const mask = @as(u8, @intFromEnum(new_status) ^ old_bits) << bit_offset;
    _ = @atomicRmw(u8, ptr, .Xor, mask, .monotonic);
    ws.notifyUpdate();
}

pub fn finishBuild(ws: *WebServer, opts: struct {
    fuzz: bool,
}) void {
    if (opts.fuzz) {
        switch (builtin.os.tag) {
            // Current implementation depends on two things that need to be ported to Windows:
            // * Memory-mapping to share data between the fuzzer and build runner.
            // * COFF/PE support added to `std.debug.Info` (it needs a batching API for resolving
            //   many addresses to source locations).
            .windows => std.process.fatal("--fuzz not yet implemented for {s}", .{@tagName(builtin.os.tag)}),
            else => {},
        }
        if (@bitSizeOf(usize) != 64) {
            // Current implementation depends on posix.mmap()'s second
            // parameter, `length: usize`, being compatible with file system's
            // u64 return value. This is not the case on 32-bit platforms.
            // Affects or affected by issues #5185, #22523, and #22464.
            std.process.fatal("--fuzz not yet implemented on {d}-bit platforms", .{@bitSizeOf(usize)});
        }

        assert(ws.fuzz == null);

        ws.build_status.store(.fuzz_init, .monotonic);
        ws.notifyUpdate();

        ws.fuzz = Fuzz.init(
            ws.gpa,
            ws.graph.io,
            ws.all_steps,
            ws.root_prog_node,
            .{ .forever = .{ .ws = ws } },
        ) catch |err| std.process.fatal("failed to start fuzzer: {s}", .{@errorName(err)});
        ws.fuzz.?.start();
    }

    ws.build_status.store(if (ws.watch) .watching else .idle, .monotonic);
    ws.notifyUpdate();
}

pub fn now(s: *const WebServer) i64 {
    const io = s.graph.io;
    const ts = base_clock.now(io);
    return @intCast(s.base_timestamp.durationTo(ts).toNanoseconds());
}

fn accept(ws: *WebServer, stream: net.Stream) void {
    const io = ws.graph.io;
    defer {
        // `net.Stream.close` wants to helpfully overwrite `stream` with
        // `undefined`, but it cannot do so since it is an immutable parameter.
        var copy = stream;
        copy.close(io);
    }
    var send_buffer: [4096]u8 = undefined;
    var recv_buffer: [4096]u8 = undefined;
    var connection_reader = stream.reader(io, &recv_buffer);
    var connection_writer = stream.writer(io, &send_buffer);
    var server: http.Server = .init(&connection_reader.interface, &connection_writer.interface);

    while (true) {
        var request = server.receiveHead() catch |err| switch (err) {
            error.HttpConnectionClosing => return,
            else => return log.err("failed to receive http request: {t}", .{err}),
        };
        switch (request.upgradeRequested()) {
            .websocket => |opt_key| {
                const key = opt_key orelse return log.err("missing websocket key", .{});
                var web_socket = request.respondWebSocket(.{ .key = key }) catch {
                    return log.err("failed to respond web socket: {t}", .{connection_writer.err.?});
                };
                ws.serveWebSocket(&web_socket) catch |err| {
                    log.err("failed to serve websocket: {t}", .{err});
                    return;
                };
                comptime unreachable;
            },
            .other => |name| return log.err("unknown upgrade request: {s}", .{name}),
            .none => {
                ws.serveRequest(&request) catch |err| switch (err) {
                    error.AlreadyReported => return,
                    else => {
                        log.err("failed to serve '{s}': {t}", .{ request.head.target, err });
                        return;
                    },
                };
            },
        }
    }
}

fn serveWebSocket(ws: *WebServer, sock: *http.Server.WebSocket) !noreturn {
    const io = ws.graph.io;

    var prev_build_status = ws.build_status.load(.monotonic);

    const prev_step_status_bits = try ws.gpa.alloc(u8, ws.step_status_bits.len);
    defer ws.gpa.free(prev_step_status_bits);
    for (prev_step_status_bits, ws.step_status_bits) |*copy, *shared| {
        copy.* = @atomicLoad(u8, shared, .monotonic);
    }

    var recv_thread = try io.concurrent(recvWebSocketMessages, .{ ws, sock });
    defer recv_thread.cancel(io);

    {
        const hello_header: abi.Hello = .{
            .status = prev_build_status,
            .flags = .{
                .time_report = ws.graph.time_report,
            },
            .timestamp = ws.now(),
            .steps_len = @intCast(ws.all_steps.len),
        };
        var bufs: [3][]const u8 = .{ @ptrCast(&hello_header), ws.step_names_trailing, prev_step_status_bits };
        try sock.writeMessageVec(&bufs, .binary);
    }

    var prev_fuzz: Fuzz.Previous = .init;
    var prev_time: i64 = std.math.minInt(i64);
    while (true) {
        const start_time = ws.now();
        const start_update_id = ws.update_id.load(.acquire);

        if (ws.fuzz) |*fuzz| {
            try fuzz.sendUpdate(sock, &prev_fuzz);
        }

        {
            try ws.time_report_mutex.lock(io);
            defer ws.time_report_mutex.unlock(io);
            for (ws.time_report_msgs, ws.time_report_update_times) |msg, update_time| {
                if (update_time <= prev_time) continue;
                // We want to send `msg`, but shouldn't block `ws.time_report_mutex` while we do, so
                // that we don't hold up the build system on the client accepting this packet.
                const owned_msg = try ws.gpa.dupe(u8, msg);
                defer ws.gpa.free(owned_msg);
                // Temporarily unlock, then re-lock after the message is sent.
                ws.time_report_mutex.unlock(io);
                defer ws.time_report_mutex.lockUncancelable(io);
                try sock.writeMessage(owned_msg, .binary);
            }
        }

        {
            const build_status = ws.build_status.load(.monotonic);
            if (build_status != prev_build_status) {
                prev_build_status = build_status;
                const msg: abi.StatusUpdate = .{ .new = build_status };
                try sock.writeMessage(@ptrCast(&msg), .binary);
            }
        }

        for (prev_step_status_bits, ws.step_status_bits, 0..) |*prev_byte, *shared, byte_idx| {
            const cur_byte = @atomicLoad(u8, shared, .monotonic);
            if (prev_byte.* == cur_byte) continue;
            const cur: [4]abi.StepUpdate.Status = .{
                @enumFromInt(@as(u2, @truncate(cur_byte >> 0))),
                @enumFromInt(@as(u2, @truncate(cur_byte >> 2))),
                @enumFromInt(@as(u2, @truncate(cur_byte >> 4))),
                @enumFromInt(@as(u2, @truncate(cur_byte >> 6))),
            };
            const prev: [4]abi.StepUpdate.Status = .{
                @enumFromInt(@as(u2, @truncate(prev_byte.* >> 0))),
                @enumFromInt(@as(u2, @truncate(prev_byte.* >> 2))),
                @enumFromInt(@as(u2, @truncate(prev_byte.* >> 4))),
                @enumFromInt(@as(u2, @truncate(prev_byte.* >> 6))),
            };
            for (cur, prev, byte_idx * 4..) |cur_status, prev_status, step_idx| {
                const msg: abi.StepUpdate = .{ .step_idx = @intCast(step_idx), .bits = .{ .status = cur_status } };
                if (cur_status != prev_status) try sock.writeMessage(@ptrCast(&msg), .binary);
            }
            prev_byte.* = cur_byte;
        }

        prev_time = start_time;

        const old_cp = io.swapCancelProtection(.blocked);
        defer _ = io.swapCancelProtection(old_cp);
        io.futexWaitTimeout(
            u32,
            &ws.update_id.raw,
            start_update_id,
            .{ .duration = .{
                .clock = .awake,
                .raw = .fromMilliseconds(default_update_interval_ms),
            } },
        ) catch |err| switch (err) {
            error.Canceled => unreachable,
        };
    }
}
fn recvWebSocketMessages(ws: *WebServer, sock: *http.Server.WebSocket) void {
    const io = ws.graph.io;

    while (true) {
        const msg = sock.readSmallMessage() catch return;
        if (msg.opcode != .binary) continue;
        if (msg.data.len == 0) continue;
        const tag: abi.ToServerTag = @enumFromInt(msg.data[0]);
        switch (tag) {
            _ => continue,
            .rebuild => while (true) {
                ws.runner_request_mutex.lock(io) catch |err| switch (err) {
                    error.Canceled => return,
                };
                defer ws.runner_request_mutex.unlock(io);
                if (ws.runner_request == null) {
                    ws.runner_request = .rebuild;
                    ws.runner_request_ready_cond.signal(io);
                    break;
                }
                ws.runner_request_empty_cond.wait(io, &ws.runner_request_mutex) catch return;
            },
        }
    }
}

fn serveRequest(ws: *WebServer, req: *http.Server.Request) !void {
    // Strip an optional leading '/debug' component from the request.
    const target: []const u8, const debug: bool = target: {
        if (mem.eql(u8, req.head.target, "/debug")) break :target .{ "/", true };
        if (mem.eql(u8, req.head.target, "/debug/")) break :target .{ "/", true };
        if (mem.startsWith(u8, req.head.target, "/debug/")) break :target .{ req.head.target["/debug".len..], true };
        break :target .{ req.head.target, false };
    };

    if (mem.eql(u8, target, "/")) return serveLibFile(ws, req, "build-web/index.html", "text/html");
    if (mem.eql(u8, target, "/main.js")) return serveLibFile(ws, req, "build-web/main.js", "application/javascript");
    if (mem.eql(u8, target, "/style.css")) return serveLibFile(ws, req, "build-web/style.css", "text/css");
    if (mem.eql(u8, target, "/time_report.css")) return serveLibFile(ws, req, "build-web/time_report.css", "text/css");
    if (mem.eql(u8, target, "/main.wasm")) return serveClientWasm(ws, req, if (debug) .Debug else .ReleaseFast);

    if (ws.fuzz) |*fuzz| {
        if (mem.eql(u8, target, "/sources.tar")) return fuzz.serveSourcesTar(req);
    }

    try req.respond("not found", .{
        .status = .not_found,
        .extra_headers = &.{
            .{ .name = "Content-Type", .value = "text/plain" },
        },
    });
}

fn serveLibFile(
    ws: *WebServer,
    request: *http.Server.Request,
    sub_path: []const u8,
    content_type: []const u8,
) !void {
    return serveFile(ws, request, .{
        .root_dir = ws.graph.zig_lib_directory,
        .sub_path = sub_path,
    }, content_type);
}
fn serveClientWasm(
    ws: *WebServer,
    req: *http.Server.Request,
    optimize_mode: std.builtin.OptimizeMode,
) !void {
    var arena_state: std.heap.ArenaAllocator = .init(ws.gpa);
    defer arena_state.deinit();
    const arena = arena_state.allocator();

    // We always rebuild the wasm on-the-fly, so that if it is edited the user can just refresh the page.
    const bin_path = try buildClientWasm(ws, arena, optimize_mode);
    return serveFile(ws, req, bin_path, "application/wasm");
}

pub fn serveFile(
    ws: *WebServer,
    request: *http.Server.Request,
    path: Cache.Path,
    content_type: []const u8,
) !void {
    const gpa = ws.gpa;
    const io = ws.graph.io;
    // The desired API is actually sendfile, which will require enhancing http.Server.
    // We load the file with every request so that the user can make changes to the file
    // and refresh the HTML page without restarting this server.
    const file_contents = path.root_dir.handle.readFileAlloc(io, path.sub_path, gpa, .limited(10 * 1024 * 1024)) catch |err| {
        log.err("failed to read '{f}': {t}", .{ path, err });
        return error.AlreadyReported;
    };
    defer gpa.free(file_contents);
    try request.respond(file_contents, .{
        .extra_headers = &.{
            .{ .name = "Content-Type", .value = content_type },
            cache_control_header,
        },
    });
}
pub fn serveTarFile(ws: *WebServer, request: *http.Server.Request, paths: []const Cache.Path) !void {
    const graph = ws.graph;
    const io = graph.io;

    var send_buffer: [0x4000]u8 = undefined;
    var response = try request.respondStreaming(&send_buffer, .{
        .respond_options = .{
            .extra_headers = &.{
                .{ .name = "Content-Type", .value = "application/x-tar" },
                cache_control_header,
            },
        },
    });

    var archiver: std.tar.Writer = .{ .underlying_writer = &response.writer };

    for (paths) |path| {
        var file = path.root_dir.handle.openFile(io, path.sub_path, .{}) catch |err| {
            log.err("failed to open '{f}': {s}", .{ path, @errorName(err) });
            continue;
        };
        defer file.close(io);
        const stat = try file.stat(io);
        var read_buffer: [1024]u8 = undefined;
        var file_reader: Io.File.Reader = .initSize(file, io, &read_buffer, stat.size);

        // TODO: this logic is completely bogus -- obviously so, because `path.root_dir.path` can
        // be cwd-relative. This is also related to why linkification doesn't work in the fuzzer UI:
        // it turns out the WASM treats the first path component as the module name, typically
        // resulting in modules named "" and "src". The compiler needs to tell the build system
        // about the module graph so that the build system can correctly encode this information in
        // the tar file.
        //
        // Additionally, this needs to ensure that all path separators for both prefix and
        // sub_path are using the POSIX-style `/` on platforms that don't use it as their native
        // path separator.
        archiver.prefix = path.root_dir.path orelse graph.cache.cwd;
        try archiver.writeFile(path.sub_path, &file_reader, @intCast(stat.mtime.toSeconds()));
    }

    // intentionally not calling `archiver.finishPedantically`
    try response.end();
}

fn buildClientWasm(ws: *WebServer, arena: Allocator, optimize: std.builtin.OptimizeMode) !Cache.Path {
    const root_name = "build-web";
    const arch_os_abi = "wasm32-freestanding";
    const cpu_features = "baseline+atomics+bulk_memory+multivalue+mutable_globals+nontrapping_fptoint+reference_types+sign_ext";

    const gpa = ws.gpa;
    const graph = ws.graph;
    const io = graph.io;

    const main_src_path: Cache.Path = .{
        .root_dir = graph.zig_lib_directory,
        .sub_path = "build-web/main.zig",
    };
    const walk_src_path: Cache.Path = .{
        .root_dir = graph.zig_lib_directory,
        .sub_path = "docs/wasm/Walk.zig",
    };
    const html_render_src_path: Cache.Path = .{
        .root_dir = graph.zig_lib_directory,
        .sub_path = "docs/wasm/html_render.zig",
    };

    var argv: std.ArrayList([]const u8) = .empty;

    try argv.appendSlice(arena, &.{
        graph.zig_exe, "build-exe", //
        "-fno-entry", //
        "-O", @tagName(optimize), //
        "-target", arch_os_abi, //
        "-mcpu", cpu_features, //
        "--cache-dir", graph.global_cache_root.path orelse ".", //
        "--global-cache-dir", graph.global_cache_root.path orelse ".", //
        "--zig-lib-dir", graph.zig_lib_directory.path orelse ".", //
        "--name", root_name, //
        "-rdynamic", //
        "-fsingle-threaded", //
        "--dep", "Walk", //
        "--dep", "html_render", //
        try std.fmt.allocPrint(arena, "-Mroot={f}", .{main_src_path}), //
        try std.fmt.allocPrint(arena, "-MWalk={f}", .{walk_src_path}), //
        "--dep", "Walk", //
        try std.fmt.allocPrint(arena, "-Mhtml_render={f}", .{html_render_src_path}), //
        "--listen=-",
    });

    var child = try std.process.spawn(io, .{
        .argv = argv.items,
        .environ_map = &graph.environ_map,
        .stdin = .pipe,
        .stdout = .pipe,
        .stderr = .pipe,
    });
    defer child.kill(io);

    var stderr_task = try io.concurrent(readStreamAlloc, .{ gpa, io, child.stderr.?, .unlimited });
    defer if (stderr_task.cancel(io)) |slice| gpa.free(slice) else |_| {};

    var stdout_buffer: [512]u8 = undefined;
    var stdout_reader: Io.File.Reader = .initStreaming(child.stdout.?, io, &stdout_buffer);
    const stdout = &stdout_reader.interface;

    {
        var w = child.stdin.?.writer(io, &.{});
        w.interface.writeStruct(std.zig.Client.Message.Header{ .tag = .update, .bytes_len = 0 }, .little) catch |err| switch (err) {
            error.WriteFailed => return w.err.?,
        };
        w.interface.writeStruct(std.zig.Client.Message.Header{ .tag = .exit, .bytes_len = 0 }, .little) catch |err| switch (err) {
            error.WriteFailed => return w.err.?,
        };
    }

    const Header = std.zig.Server.Message.Header;

    var result: ?Cache.Path = null;
    var result_error_bundle = std.zig.ErrorBundle.empty;
    var body_buffer: std.ArrayList(u8) = .empty;
    defer body_buffer.deinit(gpa);

    while (true) {
        const header = stdout.takeStruct(Header, .little) catch |e| switch (e) {
            error.ReadFailed => return error.ReadFailed,
            error.EndOfStream => break,
        };
        body_buffer.clearRetainingCapacity();
        try stdout.appendExact(gpa, &body_buffer, header.bytes_len);
        const body = body_buffer.items;

        switch (header.tag) {
            .zig_version => {
                if (!std.mem.eql(u8, builtin.zig_version_string, body)) {
                    return error.ZigProtocolVersionMismatch;
                }
            },
            .error_bundle => {
                result_error_bundle = try std.zig.Server.allocErrorBundle(arena, body);
            },
            .emit_digest => {
                const EmitDigest = std.zig.Server.Message.EmitDigest;
                const ebp_hdr: *align(1) const EmitDigest = @ptrCast(body);
                if (!ebp_hdr.flags.cache_hit) {
                    log.info("source changes detected; rebuilt wasm component", .{});
                }
                const digest = body[@sizeOf(EmitDigest)..][0..Cache.bin_digest_len];
                result = .{
                    .root_dir = graph.global_cache_root,
                    .sub_path = try arena.dupe(u8, "o" ++ std.fs.path.sep_str ++ Cache.binToHex(digest.*)),
                };
            },
            else => {}, // ignore other messages
        }
    }

    const stderr_contents = try stderr_task.await(io);
    if (stderr_contents.len > 0) {
        std.debug.print("{s}", .{stderr_contents});
    }

    // Send EOF to stdin.
    child.stdin.?.close(io);
    child.stdin = null;

    switch (try child.wait(io)) {
        .exited => |code| {
            if (code != 0) {
                log.err(
                    "the following command exited with error code {d}:\n{s}",
                    .{ code, try Build.Step.allocPrintCmd(arena, .inherit, null, argv.items) },
                );
                return error.WasmCompilationFailed;
            }
        },
        .signal => |sig| {
            log.err(
                "the following command terminated with signal {t}:\n{s}",
                .{ sig, try Build.Step.allocPrintCmd(arena, .inherit, null, argv.items) },
            );
            return error.WasmCompilationFailed;
        },
        .stopped => |sig| {
            log.err(
                "the following command stopped unexpectedly with signal {t}:\n{s}",
                .{ sig, try Build.Step.allocPrintCmd(arena, .inherit, null, argv.items) },
            );
            return error.WasmCompilationFailed;
        },
        .unknown => {
            log.err(
                "the following command terminated unexpectedly:\n{s}",
                .{try Build.Step.allocPrintCmd(arena, .inherit, null, argv.items)},
            );
            return error.WasmCompilationFailed;
        },
    }

    if (result_error_bundle.errorMessageCount() > 0) {
        try result_error_bundle.renderToStderr(io, .{}, .auto);
        log.err("the following command failed with {d} compilation errors:\n{s}", .{
            result_error_bundle.errorMessageCount(),
            try Build.Step.allocPrintCmd(arena, .inherit, null, argv.items),
        });
        return error.WasmCompilationFailed;
    }

    const base_path = result orelse {
        log.err("child process failed to report result\n{s}", .{
            try Build.Step.allocPrintCmd(arena, .inherit, null, argv.items),
        });
        return error.WasmCompilationFailed;
    };
    const bin_name = try std.zig.binNameAlloc(arena, .{
        .root_name = root_name,
        .target = &(std.zig.system.resolveTargetQuery(io, std.Build.parseTargetQuery(.{
            .arch_os_abi = arch_os_abi,
            .cpu_features = cpu_features,
        }) catch unreachable) catch unreachable),
        .output_mode = .Exe,
    });
    return base_path.join(arena, bin_name);
}

fn readStreamAlloc(gpa: Allocator, io: Io, file: Io.File, limit: Io.Limit) ![]u8 {
    var file_reader: Io.File.Reader = .initStreaming(file, io, &.{});
    return file_reader.interface.allocRemaining(gpa, limit) catch |err| switch (err) {
        error.ReadFailed => return file_reader.err.?,
        else => |e| return e,
    };
}

pub fn updateTimeReportCompile(ws: *WebServer, opts: struct {
    compile: *Build.Step.Compile,

    use_llvm: bool,
    stats: abi.time_report.CompileResult.Stats,
    ns_total: u64,

    llvm_pass_timings_len: u32,
    files_len: u32,
    decls_len: u32,

    /// The trailing data of `abi.time_report.CompileResult`, except the step name.
    trailing: []const u8,
}) void {
    const gpa = ws.gpa;
    const io = ws.graph.io;

    const step_idx: u32 = for (ws.all_steps, 0..) |s, i| {
        if (s == &opts.compile.step) break @intCast(i);
    } else unreachable;

    const old_buf = old: {
        ws.time_report_mutex.lock(io) catch return;
        defer ws.time_report_mutex.unlock(io);
        const old = ws.time_report_msgs[step_idx];
        ws.time_report_msgs[step_idx] = &.{};
        break :old old;
    };
    const buf = gpa.realloc(old_buf, @sizeOf(abi.time_report.CompileResult) + opts.trailing.len) catch @panic("out of memory");

    const out_header: *align(1) abi.time_report.CompileResult = @ptrCast(buf[0..@sizeOf(abi.time_report.CompileResult)]);
    out_header.* = .{
        .step_idx = step_idx,
        .flags = .{
            .use_llvm = opts.use_llvm,
        },
        .stats = opts.stats,
        .ns_total = opts.ns_total,
        .llvm_pass_timings_len = opts.llvm_pass_timings_len,
        .files_len = opts.files_len,
        .decls_len = opts.decls_len,
    };
    @memcpy(buf[@sizeOf(abi.time_report.CompileResult)..], opts.trailing);

    {
        ws.time_report_mutex.lock(io) catch return;
        defer ws.time_report_mutex.unlock(io);
        assert(ws.time_report_msgs[step_idx].len == 0);
        ws.time_report_msgs[step_idx] = buf;
        ws.time_report_update_times[step_idx] = ws.now();
    }
    ws.notifyUpdate();
}

pub fn updateTimeReportGeneric(ws: *WebServer, step: *Build.Step, duration: Io.Duration) void {
    const gpa = ws.gpa;
    const io = ws.graph.io;

    const step_idx: u32 = for (ws.all_steps, 0..) |s, i| {
        if (s == step) break @intCast(i);
    } else unreachable;

    const old_buf = old: {
        ws.time_report_mutex.lock(io) catch return;
        defer ws.time_report_mutex.unlock(io);
        const old = ws.time_report_msgs[step_idx];
        ws.time_report_msgs[step_idx] = &.{};
        break :old old;
    };
    const buf = gpa.realloc(old_buf, @sizeOf(abi.time_report.GenericResult)) catch @panic("out of memory");
    const out: *align(1) abi.time_report.GenericResult = @ptrCast(buf);
    out.* = .{
        .step_idx = step_idx,
        .ns_total = @intCast(duration.toNanoseconds()),
    };
    {
        ws.time_report_mutex.lock(io) catch return;
        defer ws.time_report_mutex.unlock(io);
        assert(ws.time_report_msgs[step_idx].len == 0);
        ws.time_report_msgs[step_idx] = buf;
        ws.time_report_update_times[step_idx] = ws.now();
    }
    ws.notifyUpdate();
}

pub fn updateTimeReportRunTest(
    ws: *WebServer,
    run: *Build.Step.Run,
    tests: *const Build.Step.Run.CachedTestMetadata,
    ns_per_test: []const u64,
) void {
    const gpa = ws.gpa;
    const io = ws.graph.io;

    const step_idx: u32 = for (ws.all_steps, 0..) |s, i| {
        if (s == &run.step) break @intCast(i);
    } else unreachable;

    assert(tests.names.len == ns_per_test.len);
    const tests_len: u32 = @intCast(tests.names.len);

    const new_len: u64 = len: {
        var names_len: u64 = 0;
        for (0..tests_len) |i| {
            names_len += tests.testName(@intCast(i)).len + 1;
        }
        break :len @sizeOf(abi.time_report.RunTestResult) + names_len + 8 * tests_len;
    };
    const old_buf = old: {
        ws.time_report_mutex.lock(io) catch return;
        defer ws.time_report_mutex.unlock(io);
        const old = ws.time_report_msgs[step_idx];
        ws.time_report_msgs[step_idx] = &.{};
        break :old old;
    };
    const buf = gpa.realloc(old_buf, new_len) catch @panic("out of memory");

    const out_header: *align(1) abi.time_report.RunTestResult = @ptrCast(buf[0..@sizeOf(abi.time_report.RunTestResult)]);
    out_header.* = .{
        .step_idx = step_idx,
        .tests_len = tests_len,
    };
    var offset: usize = @sizeOf(abi.time_report.RunTestResult);
    const ns_per_test_out: []align(1) u64 = @ptrCast(buf[offset..][0 .. tests_len * 8]);
    @memcpy(ns_per_test_out, ns_per_test);
    offset += tests_len * 8;
    for (0..tests_len) |i| {
        const name = tests.testName(@intCast(i));
        @memcpy(buf[offset..][0..name.len], name);
        buf[offset..][name.len] = 0;
        offset += name.len + 1;
    }
    assert(offset == buf.len);

    {
        ws.time_report_mutex.lock(io) catch return;
        defer ws.time_report_mutex.unlock(io);
        assert(ws.time_report_msgs[step_idx].len == 0);
        ws.time_report_msgs[step_idx] = buf;
        ws.time_report_update_times[step_idx] = ws.now();
    }
    ws.notifyUpdate();
}

const RunnerRequest = union(enum) {
    rebuild,
};
pub fn getRunnerRequest(ws: *WebServer) ?RunnerRequest {
    const io = ws.graph.io;
    ws.runner_request_mutex.lock(io) catch return;
    defer ws.runner_request_mutex.unlock(io);
    if (ws.runner_request) |req| {
        ws.runner_request = null;
        ws.runner_request_empty_cond.signal();
        return req;
    }
    return null;
}
pub fn wait(ws: *WebServer) Io.Cancelable!RunnerRequest {
    const io = ws.graph.io;
    try ws.runner_request_mutex.lock(io);
    defer ws.runner_request_mutex.unlock(io);
    while (true) {
        if (ws.runner_request) |req| {
            ws.runner_request = null;
            ws.runner_request_empty_cond.signal(io);
            return req;
        }
        try ws.runner_request_ready_cond.wait(io, &ws.runner_request_mutex);
    }
}

const cache_control_header: http.Header = .{
    .name = "Cache-Control",
    .value = "max-age=0, must-revalidate",
};

const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const net = std.Io.net;
const assert = std.debug.assert;
const mem = std.mem;
const log = std.log.scoped(.web_server);
const Allocator = std.mem.Allocator;
const Build = std.Build;
const Cache = Build.Cache;
const Fuzz = Build.Fuzz;
const abi = Build.abi;
const http = std.http;

const WebServer = @This();



---
File: /std/builtin/assembly.zig
---

pub const Clobbers = switch (@import("builtin").cpu.arch) {
    .x86_16, .x86, .x86_64 => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        /// Condition codes. Subset of the bits in `eflags` and `rflags`.
        cc: bool = false,
        dirflag: bool = false,
        eflags: bool = false,
        flags: bool = false,
        fpcr: bool = false,
        fpsr: bool = false,
        mxcsr: bool = false,
        rflags: bool = false,

        rax: bool = false,
        rcx: bool = false,
        rdx: bool = false,
        rbx: bool = false,
        rsp: bool = false,
        rbp: bool = false,
        rsi: bool = false,
        rdi: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,
        r15: bool = false,
        eax: bool = false,
        ecx: bool = false,
        edx: bool = false,
        ebx: bool = false,
        esp: bool = false,
        ebp: bool = false,
        esi: bool = false,
        edi: bool = false,
        r8d: bool = false,
        r9d: bool = false,
        r10d: bool = false,
        r11d: bool = false,
        r12d: bool = false,
        r13d: bool = false,
        r14d: bool = false,
        r15d: bool = false,
        ax: bool = false,
        cx: bool = false,
        dx: bool = false,
        bx: bool = false,
        sp: bool = false,
        bp: bool = false,
        si: bool = false,
        di: bool = false,
        r8w: bool = false,
        r9w: bool = false,
        r10w: bool = false,
        r11w: bool = false,
        r12w: bool = false,
        r13w: bool = false,
        r14w: bool = false,
        r15w: bool = false,
        al: bool = false,
        cl: bool = false,
        dl: bool = false,
        bl: bool = false,
        spl: bool = false,
        bpl: bool = false,
        sil: bool = false,
        dil: bool = false,
        r8b: bool = false,
        r9b: bool = false,
        r10b: bool = false,
        r11b: bool = false,
        r12b: bool = false,
        r13b: bool = false,
        r14b: bool = false,
        r15b: bool = false,
        ah: bool = false,
        ch: bool = false,
        dh: bool = false,
        bh: bool = false,
        zmm0: bool = false,
        zmm1: bool = false,
        zmm2: bool = false,
        zmm3: bool = false,
        zmm4: bool = false,
        zmm5: bool = false,
        zmm6: bool = false,
        zmm7: bool = false,
        zmm8: bool = false,
        zmm9: bool = false,
        zmm10: bool = false,
        zmm11: bool = false,
        zmm12: bool = false,
        zmm13: bool = false,
        zmm14: bool = false,
        zmm15: bool = false,
        zmm16: bool = false,
        zmm17: bool = false,
        zmm18: bool = false,
        zmm19: bool = false,
        zmm20: bool = false,
        zmm21: bool = false,
        zmm22: bool = false,
        zmm23: bool = false,
        zmm24: bool = false,
        zmm25: bool = false,
        zmm26: bool = false,
        zmm27: bool = false,
        zmm28: bool = false,
        zmm29: bool = false,
        zmm30: bool = false,
        zmm31: bool = false,
        ymm0: bool = false,
        ymm1: bool = false,
        ymm2: bool = false,
        ymm3: bool = false,
        ymm4: bool = false,
        ymm5: bool = false,
        ymm6: bool = false,
        ymm7: bool = false,
        ymm8: bool = false,
        ymm9: bool = false,
        ymm10: bool = false,
        ymm11: bool = false,
        ymm12: bool = false,
        ymm13: bool = false,
        ymm14: bool = false,
        ymm15: bool = false,
        ymm16: bool = false,
        ymm17: bool = false,
        ymm18: bool = false,
        ymm19: bool = false,
        ymm20: bool = false,
        ymm21: bool = false,
        ymm22: bool = false,
        ymm23: bool = false,
        ymm24: bool = false,
        ymm25: bool = false,
        ymm26: bool = false,
        ymm27: bool = false,
        ymm28: bool = false,
        ymm29: bool = false,
        ymm30: bool = false,
        ymm31: bool = false,
        xmm0: bool = false,
        xmm1: bool = false,
        xmm2: bool = false,
        xmm3: bool = false,
        xmm4: bool = false,
        xmm5: bool = false,
        xmm6: bool = false,
        xmm7: bool = false,
        xmm8: bool = false,
        xmm9: bool = false,
        xmm10: bool = false,
        xmm11: bool = false,
        xmm12: bool = false,
        xmm13: bool = false,
        xmm14: bool = false,
        xmm15: bool = false,
        xmm16: bool = false,
        xmm17: bool = false,
        xmm18: bool = false,
        xmm19: bool = false,
        xmm20: bool = false,
        xmm21: bool = false,
        xmm22: bool = false,
        xmm23: bool = false,
        xmm24: bool = false,
        xmm25: bool = false,
        xmm26: bool = false,
        xmm27: bool = false,
        xmm28: bool = false,
        xmm29: bool = false,
        xmm30: bool = false,
        xmm31: bool = false,
        mm0: bool = false,
        mm1: bool = false,
        mm2: bool = false,
        mm3: bool = false,
        mm4: bool = false,
        mm5: bool = false,
        mm6: bool = false,
        mm7: bool = false,
        st0: bool = false,
        st1: bool = false,
        st2: bool = false,
        st3: bool = false,
        st4: bool = false,
        st5: bool = false,
        st6: bool = false,
        st7: bool = false,
        es: bool = false,
        cs: bool = false,
        ss: bool = false,
        ds: bool = false,
        fs: bool = false,
        gs: bool = false,
    },
    .aarch64, .aarch64_be => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        nzcv: bool = false,

        x0: bool = false,
        x1: bool = false,
        x2: bool = false,
        x3: bool = false,
        x4: bool = false,
        x5: bool = false,
        x6: bool = false,
        x7: bool = false,
        x8: bool = false,
        x9: bool = false,
        x10: bool = false,
        x11: bool = false,
        x12: bool = false,
        x13: bool = false,
        x14: bool = false,
        x15: bool = false,
        x16: bool = false,
        x17: bool = false,
        x18: bool = false,
        x19: bool = false,
        x20: bool = false,
        x21: bool = false,
        x22: bool = false,
        x23: bool = false,
        x24: bool = false,
        x25: bool = false,
        x26: bool = false,
        x27: bool = false,
        x28: bool = false,
        x29: bool = false,
        x30: bool = false,

        w0: bool = false,
        w1: bool = false,
        w2: bool = false,
        w3: bool = false,
        w4: bool = false,
        w5: bool = false,
        w6: bool = false,
        w7: bool = false,
        w8: bool = false,
        w9: bool = false,
        w10: bool = false,
        w11: bool = false,
        w12: bool = false,
        w13: bool = false,
        w14: bool = false,
        w15: bool = false,
        w16: bool = false,
        w17: bool = false,
        w18: bool = false,
        w19: bool = false,
        w20: bool = false,
        w21: bool = false,
        w22: bool = false,
        w23: bool = false,
        w24: bool = false,
        w25: bool = false,
        w26: bool = false,
        w27: bool = false,
        w28: bool = false,
        w29: bool = false,

        lr: bool = false,
        sp: bool = false,
        wsp: bool = false,
        fpcr: bool = false,
        fpmr: bool = false,
        fpsr: bool = false,
        ffr: bool = false,

        p0: bool = false,
        p1: bool = false,
        p2: bool = false,
        p3: bool = false,
        p4: bool = false,
        p5: bool = false,
        p6: bool = false,
        p7: bool = false,
        p8: bool = false,
        p9: bool = false,
        p10: bool = false,
        p11: bool = false,
        p12: bool = false,
        p13: bool = false,
        p14: bool = false,
        p15: bool = false,

        z0: bool = false,
        z1: bool = false,
        z2: bool = false,
        z3: bool = false,
        z4: bool = false,
        z5: bool = false,
        z6: bool = false,
        z7: bool = false,
        z8: bool = false,
        z9: bool = false,
        z10: bool = false,
        z11: bool = false,
        z12: bool = false,
        z13: bool = false,
        z14: bool = false,
        z15: bool = false,
        z16: bool = false,
        z17: bool = false,
        z18: bool = false,
        z19: bool = false,
        z20: bool = false,
        z21: bool = false,
        z22: bool = false,
        z23: bool = false,
        z24: bool = false,
        z25: bool = false,
        z26: bool = false,
        z27: bool = false,
        z28: bool = false,
        z29: bool = false,
        z30: bool = false,
        z31: bool = false,

        v0: bool = false,
        v1: bool = false,
        v2: bool = false,
        v3: bool = false,
        v4: bool = false,
        v5: bool = false,
        v6: bool = false,
        v7: bool = false,
        v8: bool = false,
        v9: bool = false,
        v10: bool = false,
        v11: bool = false,
        v12: bool = false,
        v13: bool = false,
        v14: bool = false,
        v15: bool = false,
        v16: bool = false,
        v17: bool = false,
        v18: bool = false,
        v19: bool = false,
        v20: bool = false,
        v21: bool = false,
        v22: bool = false,
        v23: bool = false,
        v24: bool = false,
        v25: bool = false,
        v26: bool = false,
        v27: bool = false,
        v28: bool = false,
        v29: bool = false,
        v30: bool = false,
        v31: bool = false,

        d0: bool = false,
        d1: bool = false,
        d2: bool = false,
        d3: bool = false,
        d4: bool = false,
        d5: bool = false,
        d6: bool = false,
        d7: bool = false,
        d8: bool = false,
        d9: bool = false,
        d10: bool = false,
        d11: bool = false,
        d12: bool = false,
        d13: bool = false,
        d14: bool = false,
        d15: bool = false,
        d16: bool = false,
        d17: bool = false,
        d18: bool = false,
        d19: bool = false,
        d20: bool = false,
        d21: bool = false,
        d22: bool = false,
        d23: bool = false,
        d24: bool = false,
        d25: bool = false,
        d26: bool = false,
        d27: bool = false,
        d28: bool = false,
        d29: bool = false,
        d30: bool = false,
        d31: bool = false,

        s0: bool = false,
        s1: bool = false,
        s2: bool = false,
        s3: bool = false,
        s4: bool = false,
        s5: bool = false,
        s6: bool = false,
        s7: bool = false,
        s8: bool = false,
        s9: bool = false,
        s10: bool = false,
        s11: bool = false,
        s12: bool = false,
        s13: bool = false,
        s14: bool = false,
        s15: bool = false,
        s16: bool = false,
        s17: bool = false,
        s18: bool = false,
        s19: bool = false,
        s20: bool = false,
        s21: bool = false,
        s22: bool = false,
        s23: bool = false,
        s24: bool = false,
        s25: bool = false,
        s26: bool = false,
        s27: bool = false,
        s28: bool = false,
        s29: bool = false,
        s30: bool = false,
        s31: bool = false,

        h0: bool = false,
        h1: bool = false,
        h2: bool = false,
        h3: bool = false,
        h4: bool = false,
        h5: bool = false,
        h6: bool = false,
        h7: bool = false,
        h8: bool = false,
        h9: bool = false,
        h10: bool = false,
        h11: bool = false,
        h12: bool = false,
        h13: bool = false,
        h14: bool = false,
        h15: bool = false,
        h16: bool = false,
        h17: bool = false,
        h18: bool = false,
        h19: bool = false,
        h20: bool = false,
        h21: bool = false,
        h22: bool = false,
        h23: bool = false,
        h24: bool = false,
        h25: bool = false,
        h26: bool = false,
        h27: bool = false,
        h28: bool = false,
        h29: bool = false,
        h30: bool = false,
        h31: bool = false,

        b0: bool = false,
        b1: bool = false,
        b2: bool = false,
        b3: bool = false,
        b4: bool = false,
        b5: bool = false,
        b6: bool = false,
        b7: bool = false,
        b8: bool = false,
        b9: bool = false,
        b10: bool = false,
        b11: bool = false,
        b12: bool = false,
        b13: bool = false,
        b14: bool = false,
        b15: bool = false,
        b16: bool = false,
        b17: bool = false,
        b18: bool = false,
        b19: bool = false,
        b20: bool = false,
        b21: bool = false,
        b22: bool = false,
        b23: bool = false,
        b24: bool = false,
        b25: bool = false,
        b26: bool = false,
        b27: bool = false,
        b28: bool = false,
        b29: bool = false,
        b30: bool = false,
        b31: bool = false,

        za0q: bool = false,
        za1q: bool = false,
        za2q: bool = false,
        za3q: bool = false,
        za4q: bool = false,
        za5q: bool = false,
        za6q: bool = false,
        za7q: bool = false,
        za8q: bool = false,
        za9q: bool = false,
        za10q: bool = false,
        za11q: bool = false,
        za12q: bool = false,
        za13q: bool = false,
        za14q: bool = false,
        za15q: bool = false,

        za0d: bool = false,
        za1d: bool = false,
        za2d: bool = false,
        za3d: bool = false,
        za4d: bool = false,
        za5d: bool = false,
        za6d: bool = false,
        za7d: bool = false,

        za0s: bool = false,
        za1s: bool = false,
        za2s: bool = false,
        za3s: bool = false,

        za0h: bool = false,
        za1h: bool = false,
        za0b: bool = false,

        zt0: bool = false,
    },
    .arm, .armeb, .thumb, .thumbeb => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        apsr: bool = false,
        cpsr: bool = false,
        spsr: bool = false,
        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: bool = false,
        r12: bool = false,
        r13: bool = false,
        r14: bool = false,

        lr: bool = false,
        sp: bool = false,
        fpscr: bool = false,
        vpr: bool = false,

        d0: bool = false,
        d1: bool = false,
        d2: bool = false,
        d3: bool = false,
        d4: bool = false,
        d5: bool = false,
        d6: bool = false,
        d7: bool = false,
        d8: bool = false,
        d9: bool = false,
        d10: bool = false,
        d11: bool = false,
        d12: bool = false,
        d13: bool = false,
        d14: bool = false,
        d15: bool = false,
        d16: bool = false,
        d17: bool = false,
        d18: bool = false,
        d19: bool = false,
        d20: bool = false,
        d21: bool = false,
        d22: bool = false,
        d23: bool = false,
        d24: bool = false,
        d25: bool = false,
        d26: bool = false,
        d27: bool = false,
        d28: bool = false,
        d29: bool = false,
        d30: bool = false,
        d31: bool = false,

        s0: bool = false,
        s1: bool = false,
        s2: bool = false,
        s3: bool = false,
        s4: bool = false,
        s5: bool = false,
        s6: bool = false,
        s7: bool = false,
        s8: bool = false,
        s9: bool = false,
        s10: bool = false,
        s11: bool = false,
        s12: bool = false,
        s13: bool = false,
        s14: bool = false,
        s15: bool = false,
        s16: bool = false,
        s17: bool = false,
        s18: bool = false,
        s19: bool = false,
        s20: bool = false,
        s21: bool = false,
        s22: bool = false,
        s23: bool = false,
        s24: bool = false,
        s25: bool = false,
        s26: bool = false,
        s27: bool = false,
        s28: bool = false,
        s29: bool = false,
        s30: bool = false,
        s31: bool = false,

        q0: bool = false,
        q1: bool = false,
        q2: bool = false,
        q3: bool = false,
        q4: bool = false,
        q5: bool = false,
        q6: bool = false,
        q7: bool = false,
        q8: bool = false,
        q9: bool = false,
        q10: bool = false,
        q11: bool = false,
        q12: bool = false,
        q13: bool = false,
        q14: bool = false,
        q15: bool = false,
    },
    .riscv32, .riscv32be, .riscv64, .riscv64be => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        ssp: bool = false,

        x1: bool = false,
        x2: bool = false,
        x3: bool = false,
        x4: bool = false,
        x5: bool = false,
        x6: bool = false,
        x7: bool = false,
        x8: bool = false,
        x9: bool = false,
        x10: bool = false,
        x11: bool = false,
        x12: bool = false,
        x13: bool = false,
        x14: bool = false,
        x15: bool = false,
        x16: bool = false,
        x17: bool = false,
        x18: bool = false,
        x19: bool = false,
        x20: bool = false,
        x21: bool = false,
        x22: bool = false,
        x23: bool = false,
        x24: bool = false,
        x25: bool = false,
        x26: bool = false,
        x27: bool = false,
        x28: bool = false,
        x29: bool = false,
        x30: bool = false,
        x31: bool = false,

        // ABI aliases for integer registers
        ra: bool = false,
        sp: bool = false,
        gp: bool = false,
        tp: bool = false,
        t0: bool = false,
        t1: bool = false,
        t2: bool = false,
        s0: bool = false,
        fp: bool = false,
        s1: bool = false,
        a0: bool = false,
        a1: bool = false,
        a2: bool = false,
        a3: bool = false,
        a4: bool = false,
        a5: bool = false,
        a6: bool = false,
        a7: bool = false,
        s2: bool = false,
        s3: bool = false,
        s4: bool = false,
        s5: bool = false,
        s6: bool = false,
        s7: bool = false,
        s8: bool = false,
        s9: bool = false,
        s10: bool = false,
        s11: bool = false,
        t3: bool = false,
        t4: bool = false,
        t5: bool = false,
        t6: bool = false,

        fflags: bool = false,
        frm: bool = false,

        f0: bool = false,
        f1: bool = false,
        f2: bool = false,
        f3: bool = false,
        f4: bool = false,
        f5: bool = false,
        f6: bool = false,
        f7: bool = false,
        f8: bool = false,
        f9: bool = false,
        f10: bool = false,
        f11: bool = false,
        f12: bool = false,
        f13: bool = false,
        f14: bool = false,
        f15: bool = false,
        f16: bool = false,
        f17: bool = false,
        f18: bool = false,
        f19: bool = false,
        f20: bool = false,
        f21: bool = false,
        f22: bool = false,
        f23: bool = false,
        f24: bool = false,
        f25: bool = false,
        f26: bool = false,
        f27: bool = false,
        f28: bool = false,
        f29: bool = false,
        f30: bool = false,
        f31: bool = false,

        // ABI aliases for float registers
        ft0: bool = false,
        ft1: bool = false,
        ft2: bool = false,
        ft3: bool = false,
        ft4: bool = false,
        ft5: bool = false,
        ft6: bool = false,
        ft7: bool = false,
        fs0: bool = false,
        fs1: bool = false,
        fa0: bool = false,
        fa1: bool = false,
        fa2: bool = false,
        fa3: bool = false,
        fa4: bool = false,
        fa5: bool = false,
        fa6: bool = false,
        fa7: bool = false,
        fs2: bool = false,
        fs3: bool = false,
        fs4: bool = false,
        fs5: bool = false,
        fs6: bool = false,
        fs7: bool = false,
        fs8: bool = false,
        fs9: bool = false,
        fs10: bool = false,
        fs11: bool = false,
        ft8: bool = false,
        ft9: bool = false,
        ft10: bool = false,
        ft11: bool = false,

        vtype: bool = false,
        vl: bool = false,
        vxsat: bool = false,
        vxrm: bool = false,
        vcsr: bool = false,

        v0: bool = false,
        v1: bool = false,
        v2: bool = false,
        v3: bool = false,
        v4: bool = false,
        v5: bool = false,
        v6: bool = false,
        v7: bool = false,
        v8: bool = false,
        v9: bool = false,
        v10: bool = false,
        v11: bool = false,
        v12: bool = false,
        v13: bool = false,
        v14: bool = false,
        v15: bool = false,
        v16: bool = false,
        v17: bool = false,
        v18: bool = false,
        v19: bool = false,
        v20: bool = false,
        v21: bool = false,
        v22: bool = false,
        v23: bool = false,
        v24: bool = false,
        v25: bool = false,
        v26: bool = false,
        v27: bool = false,
        v28: bool = false,
        v29: bool = false,
        v30: bool = false,
        v31: bool = false,
    },
    .xcore => packed struct {
        /// Whether the inline assembly code may perform stores to memory
        /// addresses other than those derived from input pointer provenance.
        memory: bool = false,

        r0: bool = false,
        r1: bool = false,
        r2: bool = false,
        r3: bool = false,
        r4: bool = false,
        r5: bool = false,
        r6: bool = false,
        r7: bool = false,
        r8: bool = false,
        r9: bool = false,
        r10: bool = false,
        r11: 
```
