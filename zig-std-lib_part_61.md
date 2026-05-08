```
ll take this
                        // opportunity to find additional ready operations.
                        break :t 0;
                    }
                    break :t std.math.maxInt(i32);
                };
                const syscall = try Syscall.start();
                const rc = posix.system.poll(&poll_buffer, poll_len, timeout_ms);
                syscall.finish();
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        if (rc == 0) {
                            if (b.completed.head != .none) {
                                // Since there are already completions available in the
                                // queue, this is neither a timeout nor a case for
                                // retrying.
                                return;
                            }
                            continue;
                        }
                        var prev_index: Io.Operation.OptionalIndex = .none;
                        var index = b.submitted.head;
                        for (poll_buffer[0..poll_len]) |poll_entry| {
                            const storage = &b.storage[index.toIndex()];
                            const submission = &storage.submission;
                            const next_index = submission.node.next;
                            if (poll_entry.revents != 0) {
                                const result = try operate(t, submission.operation);

                                switch (prev_index) {
                                    .none => b.submitted.head = next_index,
                                    else => b.storage[prev_index.toIndex()].submission.node.next = next_index,
                                }
                                if (next_index == .none) b.submitted.tail = prev_index;

                                switch (b.completed.tail) {
                                    .none => b.completed.head = index,
                                    else => |tail_index| b.storage[tail_index.toIndex()].completion.node.next = index,
                                }
                                storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
                                b.completed.tail = index;
                            } else prev_index = index;
                            index = next_index;
                        }
                        assert(index == .none);
                        return;
                    },
                    .INTR => continue,
                    else => break,
                }
            },
        }
    }

    var tail_index = b.completed.tail;
    defer b.completed.tail = tail_index;
    var index = b.submitted.head;
    errdefer b.submitted.head = index;
    while (index != .none) {
        const storage = &b.storage[index.toIndex()];
        const submission = &storage.submission;
        const next_index = submission.node.next;
        const result = try operate(t, submission.operation);

        switch (tail_index) {
            .none => b.completed.head = index,
            else => b.storage[tail_index.toIndex()].completion.node.next = index,
        }
        storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
        tail_index = index;
        index = next_index;
    }
    b.submitted = .{ .head = .none, .tail = .none };
}

fn batchAwaitConcurrent(userdata: ?*anyopaque, b: *Io.Batch, timeout: Io.Timeout) Io.Batch.AwaitConcurrentError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (is_windows) {
        const deadline: ?Io.Clock.Timestamp = timeout.toTimestamp(io(t));
        try batchDrainSubmittedWindows(t, b, true);
        while (b.pending.head != .none and b.completed.head == .none) {
            var delay_interval: windows.LARGE_INTEGER = interval: {
                const d = deadline orelse break :interval std.math.minInt(windows.LARGE_INTEGER);
                break :interval timeoutToWindowsInterval(.{ .deadline = d }).?;
            };
            const alertable_syscall = try AlertableSyscall.start();
            const delay_rc = windows.ntdll.NtDelayExecution(.TRUE, &delay_interval);
            alertable_syscall.finish();
            switch (delay_rc) {
                .SUCCESS, .TIMEOUT => {
                    // The thread woke due to the timeout. Although spurious
                    // timeouts are OK, when no deadline is passed we must not
                    // return `error.Timeout`.
                    if (timeout != .none and b.completed.head == .none) return error.Timeout;
                },
                else => {},
            }
        }
        return;
    }
    if (native_os == .wasi) {
        // TODO call poll_oneoff
        return error.ConcurrencyUnavailable;
    }
    if (!have_poll) return error.ConcurrencyUnavailable;
    var poll_buffer: [poll_buffer_len]posix.pollfd = undefined;
    var poll_storage: struct {
        gpa: Allocator,
        batch: *Io.Batch,
        slice: []posix.pollfd,
        len: u32,

        fn add(storage: *@This(), fd: File.Handle, events: @FieldType(posix.pollfd, "events")) Io.ConcurrentError!void {
            const len = storage.len;
            if (len == poll_buffer_len) {
                const slice: []posix.pollfd = if (storage.batch.userdata) |batch_userdata|
                    @as([*]posix.pollfd, @ptrCast(@alignCast(batch_userdata)))[0..storage.batch.storage.len]
                else allocation: {
                    const allocation = storage.gpa.alloc(posix.pollfd, storage.batch.storage.len) catch
                        return error.ConcurrencyUnavailable;
                    storage.batch.userdata = allocation.ptr;
                    break :allocation allocation;
                };
                @memcpy(slice[0..poll_buffer_len], storage.slice);
                storage.slice = slice;
            }
            storage.slice[len] = .{
                .fd = fd,
                .events = events,
                .revents = 0,
            };
            storage.len = len + 1;
        }
    } = .{ .gpa = t.allocator, .batch = b, .slice = &poll_buffer, .len = 0 };
    {
        var index = b.submitted.head;
        while (index != .none) {
            const storage = &b.storage[index.toIndex()];
            const submission = storage.submission;
            switch (submission.operation) {
                .file_read_streaming => |o| try poll_storage.add(o.file.handle, posix.POLL.IN | posix.POLL.ERR),
                .file_write_streaming => |o| try poll_storage.add(o.file.handle, posix.POLL.OUT | posix.POLL.ERR),
                .device_io_control => |o| try poll_storage.add(o.file.handle, posix.POLL.IN | posix.POLL.OUT | posix.POLL.ERR),
                .net_receive => |*o| nb: {
                    var data_i: usize = 0;
                    const result: Io.Operation.Result = .{ .net_receive = for (o.message_buffer, 0..) |*msg, msg_i| {
                        const remaining_data_buffer = o.data_buffer[data_i..];
                        netReceivePosix(o.socket_handle, msg, remaining_data_buffer, o.flags, true) catch |err| switch (err) {
                            error.Canceled => |e| return e,
                            error.WouldBlock => {
                                if (msg_i != 0) break .{ null, msg_i };
                                try poll_storage.add(o.socket_handle, posix.POLL.IN | posix.POLL.ERR);
                                break :nb;
                            },
                            else => |e| break .{ e, 0 },
                        };
                        data_i += msg.data.len;
                    } else .{ null, o.message_buffer.len } };
                    switch (b.completed.tail) {
                        .none => b.completed.head = index,
                        else => |tail_index| b.storage[tail_index.toIndex()].completion.node.next = index,
                    }
                    storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
                    b.completed.tail = index;
                },
            }
            index = submission.node.next;
        }
    }
    switch (poll_storage.len) {
        0 => return,
        1 => if (timeout == .none and b.completed.head == .none) {
            const index = b.submitted.head;
            const storage = &b.storage[index.toIndex()];
            const result = try operate(t, storage.submission.operation);

            b.submitted = .{ .head = .none, .tail = .none };

            switch (b.completed.tail) {
                .none => b.completed.head = index,
                else => |tail_index| b.storage[tail_index.toIndex()].completion.node.next = index,
            }
            storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
            b.completed.tail = index;
            return;
        },
        else => {},
    }
    const t_io = io(t);
    const deadline = timeout.toTimestamp(t_io);
    while (true) {
        const timeout_ms: i32 = t: {
            if (b.completed.head != .none) {
                // It is legal to call batchWait with already completed
                // operations in the ring. In such case, we need to avoid
                // blocking in the poll syscall, but we can still take this
                // opportunity to find additional ready operations.
                break :t 0;
            }
            const d = deadline orelse break :t -1;
            const duration = d.durationFromNow(t_io);
            break :t @min(@max(0, duration.raw.toMilliseconds()), std.math.maxInt(i32));
        };
        const syscall = try Syscall.start();
        const rc = posix.system.poll(poll_storage.slice.ptr, poll_storage.len, timeout_ms);
        syscall.finish();
        switch (posix.errno(rc)) {
            .SUCCESS => {
                if (rc == 0) {
                    if (b.completed.head != .none) {
                        // Since there are already completions available in the
                        // queue, this is neither a timeout nor a case for
                        // retrying.
                        return;
                    }
                    // Although spurious timeouts are OK, when no deadline is
                    // passed we must not return `error.Timeout`.
                    if (deadline == null) continue;
                    return error.Timeout;
                }
                var prev_index: Io.Operation.OptionalIndex = .none;
                var index = b.submitted.head;
                for (poll_storage.slice[0..poll_storage.len]) |poll_entry| {
                    const submission = &b.storage[index.toIndex()].submission;
                    const next_index = submission.node.next;
                    if (poll_entry.revents != 0) {
                        const result = try operate(t, submission.operation);

                        switch (prev_index) {
                            .none => b.submitted.head = next_index,
                            else => b.storage[prev_index.toIndex()].submission.node.next = next_index,
                        }
                        if (next_index == .none) b.submitted.tail = prev_index;

                        switch (b.completed.tail) {
                            .none => b.completed.head = index,
                            else => |tail_index| b.storage[tail_index.toIndex()].completion.node.next = index,
                        }
                        b.completed.tail = index;
                        b.storage[index.toIndex()] = .{ .completion = .{
                            .node = .{ .next = .none },
                            .result = result,
                        } };
                    } else prev_index = index;
                    index = next_index;
                }
                assert(index == .none);
                return;
            },
            .INTR => continue,
            else => return error.ConcurrencyUnavailable,
        }
    }
}

const WindowsBatchOperationUserdata = extern struct {
    file: windows.HANDLE,
    iosb: windows.IO_STATUS_BLOCK,

    const Erased = Io.Operation.Storage.Pending.Userdata;

    comptime {
        assert(@sizeOf(WindowsBatchOperationUserdata) <= @sizeOf(Erased));
    }

    fn toErased(userdata: *WindowsBatchOperationUserdata) *Erased {
        return @ptrCast(userdata);
    }

    fn fromErased(erased: *Erased) *WindowsBatchOperationUserdata {
        return @ptrCast(erased);
    }
};

fn batchCancel(userdata: ?*anyopaque, b: *Io.Batch) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (is_windows) {
        if (b.pending.head == .none) return;
        waitForApcOrAlert();
        var index = b.pending.head;
        while (index != .none) {
            const pending = &b.storage[index.toIndex()].pending;
            const operation_userdata: *WindowsBatchOperationUserdata = .fromErased(&pending.userdata);
            var cancel_iosb: windows.IO_STATUS_BLOCK = undefined;
            _ = windows.ntdll.NtCancelIoFileEx(operation_userdata.file, &operation_userdata.iosb, &cancel_iosb);
            index = pending.node.next;
        }
        while (b.pending.head != .none) waitForApcOrAlert();
    } else if (b.userdata) |batch_userdata| {
        const poll_storage: [*]posix.pollfd = @ptrCast(@alignCast(batch_userdata));
        t.allocator.free(poll_storage[0..b.storage.len]);
        b.userdata = null;
    }
}

fn batchCompleteBlockingWindows(
    b: *Io.Batch,
    operation_userdata: *WindowsBatchOperationUserdata,
    result: Io.Operation.Result,
) void {
    const erased_userdata = operation_userdata.toErased();
    const pending: *Io.Operation.Storage.Pending = @fieldParentPtr("userdata", erased_userdata);
    switch (pending.node.prev) {
        .none => b.pending.head = pending.node.next,
        else => |prev_index| b.storage[prev_index.toIndex()].pending.node.next = pending.node.next,
    }
    switch (pending.node.next) {
        .none => b.pending.tail = pending.node.prev,
        else => |next_index| b.storage[next_index.toIndex()].pending.node.prev = pending.node.prev,
    }
    const storage: *Io.Operation.Storage = @fieldParentPtr("pending", pending);
    const index: Io.Operation.OptionalIndex = .fromIndex(storage - b.storage.ptr);
    switch (b.completed.tail) {
        .none => b.completed.head = index,
        else => |tail_index| b.storage[tail_index.toIndex()].completion.node.next = index,
    }
    b.completed.tail = index;
    storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
}

fn batchApc(
    apc_context: ?*anyopaque,
    iosb: *windows.IO_STATUS_BLOCK,
    _: windows.ULONG,
) align(apc_align) callconv(.winapi) void {
    const b: *Io.Batch = @ptrCast(@alignCast(apc_context));
    const operation_userdata: *WindowsBatchOperationUserdata = @fieldParentPtr("iosb", iosb);
    const erased_userdata = operation_userdata.toErased();
    const pending: *Io.Operation.Storage.Pending = @fieldParentPtr("userdata", erased_userdata);
    switch (pending.node.prev) {
        .none => b.pending.head = pending.node.next,
        else => |prev_index| b.storage[prev_index.toIndex()].pending.node.next = pending.node.next,
    }
    switch (pending.node.next) {
        .none => b.pending.tail = pending.node.prev,
        else => |next_index| b.storage[next_index.toIndex()].pending.node.prev = pending.node.prev,
    }
    const storage: *Io.Operation.Storage = @fieldParentPtr("pending", pending);
    const index: Io.Operation.OptionalIndex = .fromIndex(storage - b.storage.ptr);
    switch (iosb.u.Status) {
        .CANCELLED => {
            const tail_index = b.unused.tail;
            switch (tail_index) {
                .none => b.unused.head = index,
                else => b.storage[tail_index.toIndex()].unused.next = index,
            }
            storage.* = .{ .unused = .{ .prev = tail_index, .next = .none } };
            b.unused.tail = index;
        },
        else => {
            switch (b.completed.tail) {
                .none => b.completed.head = index,
                else => |tail_index| b.storage[tail_index.toIndex()].completion.node.next = index,
            }
            b.completed.tail = index;
            const result: Io.Operation.Result = switch (pending.tag) {
                .file_read_streaming => .{ .file_read_streaming = ntReadFileResult(iosb) },
                .file_write_streaming => .{ .file_write_streaming = ntWriteFileResult(iosb) },
                .device_io_control => .{ .device_io_control = iosb.* },
                .net_receive => unreachable,
            };
            storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
        },
    }
}

/// If `concurrency` is false, `error.ConcurrencyUnavailable` is unreachable.
fn batchDrainSubmittedWindows(t: *Threaded, b: *Io.Batch, concurrency: bool) (Io.ConcurrentError || Io.Cancelable)!void {
    var index = b.submitted.head;
    errdefer b.submitted.head = index;
    while (index != .none) {
        const storage = &b.storage[index.toIndex()];
        const submission = storage.submission;
        storage.* = .{ .pending = .{
            .node = .{ .prev = b.pending.tail, .next = .none },
            .tag = submission.operation,
            .userdata = undefined,
        } };
        switch (b.pending.tail) {
            .none => b.pending.head = index,
            else => |tail_index| b.storage[tail_index.toIndex()].pending.node.next = index,
        }
        b.pending.tail = index;
        const operation_userdata: *WindowsBatchOperationUserdata = .fromErased(&storage.pending.userdata);
        errdefer {
            operation_userdata.iosb = .{ .u = .{ .Status = .CANCELLED }, .Information = undefined };
            batchApc(b, &operation_userdata.iosb, 0);
        }
        switch (submission.operation) {
            .file_read_streaming => |o| o: {
                var data_index: usize = 0;
                while (o.data.len - data_index != 0 and o.data[data_index].len == 0) data_index += 1;
                if (o.data.len - data_index == 0) {
                    operation_userdata.iosb = .{ .u = .{ .Status = .SUCCESS }, .Information = 0 };
                    batchApc(b, &operation_userdata.iosb, 0);
                    break :o;
                }
                const buffer = o.data[data_index];
                const short_buffer_len = std.math.lossyCast(u32, buffer.len);

                if (o.file.flags.nonblocking) {
                    operation_userdata.file = o.file.handle;
                    switch (windows.ntdll.NtReadFile(
                        o.file.handle,
                        null, // event
                        &batchApc,
                        b,
                        &operation_userdata.iosb,
                        buffer.ptr,
                        short_buffer_len,
                        null, // byte offset
                        null, // key
                    )) {
                        .PENDING, .SUCCESS => {},
                        .CANCELLED => unreachable,
                        else => |status| {
                            operation_userdata.iosb.u.Status = status;
                            batchApc(b, &operation_userdata.iosb, 0);
                        },
                    }
                } else {
                    if (concurrency) return error.ConcurrencyUnavailable;

                    const syscall: Syscall = try .start();
                    while (true) switch (windows.ntdll.NtReadFile(
                        o.file.handle,
                        null, // event
                        null, // APC routine
                        null, // APC context
                        &operation_userdata.iosb,
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
                            operation_userdata.iosb.u.Status = status;
                            batchApc(b, &operation_userdata.iosb, 0);
                            break;
                        },
                    };
                }
            },
            .file_write_streaming => |o| o: {
                const buffer = windowsWriteBuffer(o.header, o.data, o.splat);
                if (buffer.len == 0) {
                    operation_userdata.iosb = .{ .u = .{ .Status = .SUCCESS }, .Information = 0 };
                    batchApc(b, &operation_userdata.iosb, 0);
                    break :o;
                }
                if (o.file.flags.nonblocking) {
                    operation_userdata.file = o.file.handle;
                    switch (windows.ntdll.NtWriteFile(
                        o.file.handle,
                        null, // event
                        &batchApc,
                        b,
                        &operation_userdata.iosb,
                        buffer.ptr,
                        @intCast(buffer.len),
                        null, // byte offset
                        null, // key
                    )) {
                        .PENDING, .SUCCESS => {},
                        .CANCELLED => unreachable,
                        else => |status| {
                            operation_userdata.iosb.u.Status = status;
                            batchApc(b, &operation_userdata.iosb, 0);
                        },
                    }
                } else {
                    if (concurrency) return error.ConcurrencyUnavailable;

                    const syscall: Syscall = try .start();
                    while (true) switch (windows.ntdll.NtWriteFile(
                        o.file.handle,
                        null, // event
                        null, // APC routine
                        null, // APC context
                        &operation_userdata.iosb,
                        buffer.ptr,
                        @intCast(buffer.len),
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
                            operation_userdata.iosb.u.Status = status;
                            batchApc(b, &operation_userdata.iosb, 0);
                            break;
                        },
                    };
                }
            },
            .device_io_control => |o| {
                const NtControlFile = switch (o.code.DeviceType) {
                    .FILE_SYSTEM, .NAMED_PIPE => &windows.ntdll.NtFsControlFile,
                    else => &windows.ntdll.NtDeviceIoControlFile,
                };
                if (o.file.flags.nonblocking) {
                    operation_userdata.file = o.file.handle;
                    switch (NtControlFile(
                        o.file.handle,
                        null, // event
                        &batchApc,
                        b,
                        &operation_userdata.iosb,
                        o.code,
                        if (o.in.len > 0) o.in.ptr else null,
                        @intCast(o.in.len),
                        if (o.out.len > 0) o.out.ptr else null,
                        @intCast(o.out.len),
                    )) {
                        .PENDING, .SUCCESS => {},
                        .CANCELLED => unreachable,
                        else => |status| {
                            operation_userdata.iosb.u.Status = status;
                            batchApc(b, &operation_userdata.iosb, 0);
                        },
                    }
                } else {
                    if (concurrency) return error.ConcurrencyUnavailable;

                    const syscall: Syscall = try .start();
                    while (true) switch (NtControlFile(
                        o.file.handle,
                        null, // event
                        null, // APC routine
                        null, // APC context
                        &operation_userdata.iosb,
                        o.code,
                        if (o.in.len > 0) o.in.ptr else null,
                        @intCast(o.in.len),
                        if (o.out.len > 0) o.out.ptr else null,
                        @intCast(o.out.len),
                    )) {
                        .PENDING => unreachable, // unrecoverable: wrong File nonblocking flag
                        .CANCELLED => {
                            try syscall.checkCancel();
                            continue;
                        },
                        else => |status| {
                            syscall.finish();
                            operation_userdata.iosb.u.Status = status;
                            batchApc(b, &operation_userdata.iosb, 0);
                            break;
                        },
                    };
                }
            },
            .net_receive => |*o| {
                // TODO integrate with overlapped I/O or equivalent to avoid this error
                if (concurrency) return error.ConcurrencyUnavailable;
                batchCompleteBlockingWindows(b, operation_userdata, .{
                    .net_receive = netReceiveWindows(t, o.socket_handle, o.message_buffer, o.data_buffer, o.flags),
                });
            },
        }
        index = submission.node.next;
    }
    b.submitted = .{ .head = .none, .tail = .none };
}

/// Since Windows only supports writing one contiguous buffer, returns the
/// first one, while also limiting it to a length representable by 32-bit
/// unsigned integer.
fn windowsWriteBuffer(header: []const u8, data: []const []const u8, splat: usize) []const u8 {
    const buffer = b: {
        if (header.len != 0) break :b header;
        for (data[0 .. data.len - 1]) |buffer| {
            if (buffer.len != 0) break :b buffer;
        }
        if (splat == 0) return &.{};
        break :b data[data.len - 1];
    };
    return buffer[0..std.math.lossyCast(u32, buffer.len)];
}

fn submitComplete(ring: []u32, complete_tail: *Io.Batch.RingIndex, op: u32) void {
    const ct = complete_tail.*;
    const len: u31 = @intCast(ring.len);
    ring[ct.index(len)] = op;
    complete_tail.* = ct.next(len);
}

const dirCreateDir = switch (native_os) {
    .windows => dirCreateDirWindows,
    .wasi => dirCreateDirWasi,
    else => dirCreateDirPosix,
};

fn dirCreateDirPosix(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: Dir.Permissions) Dir.CreateDirError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.mkdirat(dir.handle, sub_path_posix, permissions.toMode()))) {
            .SUCCESS => {
                syscall.finish();
                return;
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            .ACCES => return syscall.fail(error.AccessDenied),
            .PERM => return syscall.fail(error.PermissionDenied),
            .DQUOT => return syscall.fail(error.DiskQuota),
            .EXIST => return syscall.fail(error.PathAlreadyExists),
            .LOOP => return syscall.fail(error.SymLinkLoop),
            .MLINK => return syscall.fail(error.LinkQuotaExceeded),
            .NAMETOOLONG => return syscall.fail(error.NameTooLong),
            .NOENT => return syscall.fail(error.FileNotFound),
            .NOMEM => return syscall.fail(error.SystemResources),
            .NOSPC => return syscall.fail(error.NoSpaceLeft),
            .NOTDIR => return syscall.fail(error.NotDir),
            .ROFS => return syscall.fail(error.ReadOnlyFileSystem),
            // dragonfly: when dir_fd is unlinked from filesystem
            .NOTCONN => return syscall.fail(error.FileNotFound),
            .ILSEQ => return syscall.fail(error.BadPathName),
            .BADF => |err| return syscall.errnoBug(err), // File descriptor used after closed.
            .FAULT => |err| return syscall.errnoBug(err),
            else => |err| return syscall.unexpectedErrno(err),
        }
    }
}

fn dirCreateDirWasi(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: Dir.Permissions) Dir.CreateDirError!void {
    if (builtin.link_libc) return dirCreateDirPosix(userdata, dir, sub_path, permissions);
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const syscall: Syscall = try .start();
    while (true) {
        switch (std.os.wasi.path_create_directory(dir.handle, sub_path.ptr, sub_path.len)) {
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
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .PERM => return error.PermissionDenied,
                    .DQUOT => return error.DiskQuota,
                    .EXIST => return error.PathAlreadyExists,
                    .FAULT => |err| return errnoBug(err),
                    .LOOP => return error.SymLinkLoop,
                    .MLINK => return error.LinkQuotaExceeded,
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOMEM => return error.SystemResources,
                    .NOSPC => return error.NoSpaceLeft,
                    .NOTDIR => return error.NotDir,
                    .ROFS => return error.ReadOnlyFileSystem,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirCreateDirWindows(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: Dir.Permissions) Dir.CreateDirError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    _ = permissions; // TODO use this value

    const sub_path_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
    const attr: windows.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w.span())) null else dir.handle,
        .Attributes = .{ .INHERIT = false },
        .ObjectName = @constCast(&windows.UNICODE_STRING.init(sub_path_w.span())),
        .SecurityDescriptor = null,
        .SecurityQualityOfService = null,
    };

    var sub_dir_handle: windows.HANDLE = undefined;
    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var attempt: u5 = 0;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtCreateFile(
        &sub_dir_handle,
        .{
            .GENERIC = .{ .READ = true },
            .STANDARD = .{ .SYNCHRONIZE = true },
        },
        &attr,
        &io_status_block,
        null,
        .{ .NORMAL = true },
        .VALID_FLAGS,
        .CREATE,
        .{
            .DIRECTORY_FILE = true,
            .NON_DIRECTORY_FILE = false,
            .IO = .SYNCHRONOUS_NONALERT,
            .OPEN_REPARSE_POINT = false,
        },
        null,
        0,
    )) {
        .SUCCESS => {
            syscall.finish();
            windows.CloseHandle(sub_dir_handle);
            return;
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
            if (max_windows_kernel_bug_retries - attempt == 0) return error.Unexpected;
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
            // call has failed. There is not really a sane way to handle
            // this other than retrying the creation after the OS finishes
            // the deletion.
            syscall.finish();
            if (max_windows_kernel_bug_retries - attempt == 0) return error.Unexpected;
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
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .OBJECT_NAME_COLLISION => return syscall.fail(error.PathAlreadyExists),
        .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
        .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
        .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
        .OBJECT_PATH_SYNTAX_BAD => |status| return syscall.ntstatusBug(status),
        .INVALID_HANDLE => |status| return syscall.ntstatusBug(status),
        else => |status| return syscall.unexpectedNtstatus(status),
    };
}

fn dirCreateDirPath(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
) Dir.CreateDirPathError!Dir.CreatePathStatus {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    var it = Dir.path.componentIterator(sub_path);
    var status: Dir.CreatePathStatus = .existed;
    var component = it.last() orelse return error.BadPathName;
    while (true) {
        if (dirCreateDir(t, dir, component.path, permissions)) |_| {
            status = .created;
        } else |err| switch (err) {
            error.PathAlreadyExists => {
                // It is important to return an error if it's not a directory
                // because otherwise a dangling symlink could cause an infinite
                // loop.
                const kind = try filePathKind(t, dir, component.path);
                if (kind != .directory) return error.NotDir;
            },
            error.FileNotFound => |e| {
                component = it.previous() orelse return e;
                continue;
            },
            else => |e| return e,
        }
        component = it.next() orelse return status;
    }
}

const dirCreateDirPathOpen = switch (native_os) {
    .windows => dirCreateDirPathOpenWindows,
    .wasi => dirCreateDirPathOpenWasi,
    else => dirCreateDirPathOpenPosix,
};

fn dirCreateDirPathOpenPosix(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.OpenOptions,
) Dir.CreateDirPathOpenError!Dir {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const t_io = io(t);
    return dirOpenDirPosix(t, dir, sub_path, options) catch |err| switch (err) {
        error.FileNotFound => {
            _ = try dir.createDirPathStatus(t_io, sub_path, permissions);
            return dirOpenDirPosix(t, dir, sub_path, options);
        },
        else => |e| return e,
    };
}

fn dirCreateDirPathOpenWindows(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.OpenOptions,
) Dir.CreateDirPathOpenError!Dir {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const w = windows;

    _ = permissions; // TODO apply these permissions

    var it = Dir.path.componentIterator(sub_path);
    // If there are no components in the path, then create a dummy component with the full path.
    var component: Dir.path.NativeComponentIterator.Component = it.last() orelse .{
        .name = "",
        .path = sub_path,
    };

    components: while (true) {
        const sub_path_w = try sliceToPrefixedFileW(dir.handle, component.path, .{});
        const attr: windows.OBJECT.ATTRIBUTES = .{
            .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w.span())) null else dir.handle,
            .ObjectName = @constCast(&sub_path_w.string()),
        };
        const is_last = it.peekNext() == null;
        var result: Dir = .{ .handle = undefined };
        var iosb: w.IO_STATUS_BLOCK = undefined;
        const syscall: Syscall = try .start();
        while (true) switch (w.ntdll.NtCreateFile(
            &result.handle,
            .{
                .SPECIFIC = .{ .FILE_DIRECTORY = .{
                    .LIST = options.iterate,
                    .READ_EA = true,
                    .READ_ATTRIBUTES = true,
                    .TRAVERSE = true,
                } },
                .STANDARD = .{
                    .RIGHTS = .READ,
                    .SYNCHRONIZE = true,
                },
            },
            &attr,
            &iosb,
            null,
            .{ .NORMAL = true },
            .VALID_FLAGS,
            if (is_last) .OPEN_IF else .CREATE,
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
                component = it.next() orelse return result;
                w.CloseHandle(result.handle);
                continue :components;
            },
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
            .OBJECT_NAME_COLLISION => {
                syscall.finish();
                assert(!is_last);
                // stat the file and return an error if it's not a directory
                // this is important because otherwise a dangling symlink
                // could cause an infinite loop
                const fstat = try dirStatFileWindows(t, dir, component.path, .{
                    .follow_symlinks = options.follow_symlinks,
                });
                if (fstat.kind != .directory) return error.NotDir;

                component = it.next().?;
                continue :components;
            },

            .OBJECT_NAME_NOT_FOUND,
            .OBJECT_PATH_NOT_FOUND,
            => {
                syscall.finish();
                component = it.previous() orelse return error.FileNotFound;
                continue :components;
            },

            .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
            // This can happen if the directory has 'List folder contents' permission set to 'Deny'
            // and the directory is trying to be opened for iteration.
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .DISK_FULL => return syscall.fail(error.NoSpaceLeft),
            .INVALID_PARAMETER => |s| return syscall.ntstatusBug(s),
            else => |s| return syscall.unexpectedNtstatus(s),
        };
    }
}

fn dirCreateDirPathOpenWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.OpenOptions,
) Dir.CreateDirPathOpenError!Dir {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const t_io = io(t);
    return dirOpenDirWasi(t, dir, sub_path, options) catch |err| switch (err) {
        error.FileNotFound => {
            _ = try dir.createDirPathStatus(t_io, sub_path, permissions);
            return dirOpenDirWasi(t, dir, sub_path, options);
        },
        else => |e| return e,
    };
}

fn dirStat(userdata: ?*anyopaque, dir: Dir) Dir.StatError!Dir.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    return fileStat(t, .{
        .handle = dir.handle,
        .flags = .{ .nonblocking = false },
    });
}

const dirStatFile = switch (native_os) {
    .linux => dirStatFileLinux,
    .windows => dirStatFileWindows,
    .wasi => dirStatFileWasi,
    else => dirStatFilePosix,
};

fn dirStatFileLinux(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.StatFileOptions,
) Dir.StatFileError!File.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const linux = std.os.linux;
    const sys = if (statx_use_c) std.c else std.os.linux;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const flags: u32 = linux.AT.NO_AUTOMOUNT |
        @as(u32, if (!options.follow_symlinks) linux.AT.SYMLINK_NOFOLLOW else 0);

    const syscall: Syscall = try .start();
    while (true) {
        var statx = std.mem.zeroes(linux.Statx);
        switch (sys.errno(sys.statx(dir.handle, sub_path_posix, flags, linux_statx_request, &statx))) {
            .SUCCESS => {
                syscall.finish();
                return statFromLinux(&statx);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => return error.AccessDenied,
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .LOOP => return error.SymLinkLoop,
                    .NAMETOOLONG => |err| return errnoBug(err), // Handled by pathToPosix() above.
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.NotDir,
                    .NOMEM => return error.SystemResources,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirStatFilePosix(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.StatFileOptions,
) Dir.StatFileError!File.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const flags: u32 = if (!options.follow_symlinks) posix.AT.SYMLINK_NOFOLLOW else 0;

    return posixStatFile(dir.handle, sub_path_posix, flags);
}

fn posixStatFile(dir_fd: posix.fd_t, sub_path: [:0]const u8, flags: u32) Dir.StatFileError!File.Stat {
    const syscall: Syscall = try .start();
    while (true) {
        var stat = std.mem.zeroes(posix.Stat);
        switch (posix.errno(fstatat_sym(dir_fd, sub_path, &stat, flags))) {
            .SUCCESS => {
                syscall.finish();
                return statFromPosix(&stat);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOMEM => return error.SystemResources,
                    .ACCES => return error.AccessDenied,
                    .PERM => return error.PermissionDenied,
                    .FAULT => |err| return errnoBug(err),
                    .NAMETOOLONG => return error.NameTooLong,
                    .LOOP => return error.SymLinkLoop,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.FileNotFound,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirStatFileWindows(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.StatFileOptions,
) Dir.StatFileError!File.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const file = try dirOpenFileWindows(t, dir, sub_path, .{
        .follow_symlinks = options.follow_symlinks,
    });
    defer windows.CloseHandle(file.handle);
    return fileStatWindows(t, file);
}

fn dirStatFileWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.StatFileOptions,
) Dir.StatFileError!File.Stat {
    if (builtin.link_libc) return dirStatFilePosix(userdata, dir, sub_path, options);
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const wasi = std.os.wasi;
    const flags: wasi.lookupflags_t = .{
        .SYMLINK_FOLLOW = options.follow_symlinks,
    };
    var stat: wasi.filestat_t = undefined;
    const syscall: Syscall = try .start();
    while (true) {
        switch (wasi.path_filestat_get(dir.handle, flags, sub_path.ptr, sub_path.len, &stat)) {
            .SUCCESS => {
                syscall.finish();
                return statFromWasi(&stat);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOMEM => return error.SystemResources,
                    .ACCES => return error.AccessDenied,
                    .FAULT => |err| return errnoBug(err),
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.FileNotFound,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn filePathKind(t: *Threaded, dir: Dir, sub_path: []const u8) !File.Kind {
    if (native_os == .linux) {
        var path_buffer: [posix.PATH_MAX]u8 = undefined;
        const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

        const linux = std.os.linux;
        const syscall: Syscall = try .start();
        while (true) {
            var statx = std.mem.zeroes(linux.Statx);
            switch (linux.errno(linux.statx(
                dir.handle,
                sub_path_posix,
                linux.AT.NO_AUTOMOUNT | linux.AT.SYMLINK_NOFOLLOW,
                .{ .TYPE = true },
                &statx,
            ))) {
                .SUCCESS => {
                    syscall.finish();
                    if (!statx.mask.TYPE) return error.Unexpected;
                    return statxKind(statx.mode);
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                .NOMEM => return syscall.fail(error.SystemResources),
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    }

    const stat = try dirStatFile(t, dir, sub_path, .{ .follow_symlinks = false });
    return stat.kind;
}

fn fileLength(userdata: ?*anyopaque, file: File) File.LengthError!u64 {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    if (native_os == .linux) {
        const linux = std.os.linux;

        const syscall: Syscall = try .start();
        while (true) {
            var statx = std.mem.zeroes(linux.Statx);
            switch (linux.errno(linux.statx(file.handle, "", linux.AT.EMPTY_PATH, .{ .SIZE = true }, &statx))) {
                .SUCCESS => {
                    syscall.finish();
                    if (!statx.mask.SIZE) return error.Unexpected;
                    return statx.size;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |e| {
                    syscall.finish();
                    switch (e) {
                        .ACCES => |err| return errnoBug(err),
                        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                        .FAULT => |err| return errnoBug(err),
                        .INVAL => |err| return errnoBug(err),
                        .LOOP => |err| return errnoBug(err),
                        .NAMETOOLONG => |err| return errnoBug(err),
                        .NOENT => |err| return errnoBug(err),
                        .NOMEM => return error.SystemResources,
                        .NOTDIR => |err| return errnoBug(err),
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    } else if (is_windows) {
        // TODO call NtQueryInformationFile and ask for only the size instead of "all"
    }

    const stat = try fileStat(t, file);
    return stat.size;
}

const fileStat = switch (native_os) {
    .linux => fileStatLinux,
    .windows => fileStatWindows,
    .wasi => fileStatWasi,
    else => fileStatPosix,
};

fn fileStatPosix(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (posix.Stat == void) return error.Streaming;

    const syscall: Syscall = try .start();
    while (true) {
        var stat = std.mem.zeroes(posix.Stat);
        switch (posix.errno(fstat_sym(file.handle, &stat))) {
            .SUCCESS => {
                syscall.finish();
                return statFromPosix(&stat);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOMEM => return error.SystemResources,
                    .ACCES => return error.AccessDenied,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileStatLinux(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const linux = std.os.linux;
    const sys = if (statx_use_c) std.c else std.os.linux;

    const syscall: Syscall = try .start();
    while (true) {
        var statx = std.mem.zeroes(linux.Statx);
        switch (sys.errno(sys.statx(file.handle, "", linux.AT.EMPTY_PATH, linux_statx_request, &statx))) {
            .SUCCESS => {
                syscall.finish();
                return statFromLinux(&statx);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .ACCES => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .FAULT => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    .LOOP => |err| return errnoBug(err),
                    .NAMETOOLONG => |err| return errnoBug(err),
                    .NOENT => |err| return errnoBug(err),
                    .NOMEM => return error.SystemResources,
                    .NOTDIR => |err| return errnoBug(err),
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn fileStatWindows(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    const block_size: u32 = if (t.systemBasicInformation()) |sbi|
        @intCast(@max(sbi.PageSize, sbi.AllocationGranularity))
    else
        std.heap.page_size_max;

    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var info: windows.FILE.ALL_INFORMATION = undefined;
    {
        const syscall: Syscall = try .start();
        while (true) switch (windows.ntdll.NtQueryInformationFile(
            file.handle,
            &io_status_block,
            &info,
            @sizeOf(windows.FILE.ALL_INFORMATION),
            .All,
        )) {
            .SUCCESS => break syscall.finish(),
            // Buffer overflow here indicates that there is more information available than was able to be stored in the buffer
            // size provided. This is treated as success because the type of variable-length information that this would be relevant for
            // (name, volume name, etc) we don't care about.
            .BUFFER_OVERFLOW => break syscall.finish(),
            .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .CANCELLED => {
                try syscall.checkCancel();
                continue;
            },
            else => |s| return syscall.unexpectedNtstatus(s),
        };
    }
    return .{
        .inode = info.InternalInformation.IndexNumber,
        .size = @as(u64, @bitCast(info.StandardInformation.EndOfFile)),
        .permissions = .default_file,
        .kind = if (info.BasicInformation.FileAttributes.REPARSE_POINT) reparse_point: {
            var tag_info: windows.FILE.ATTRIBUTE_TAG_INFO = undefined;
            const syscall: Syscall = try .start();
            while (true) switch (windows.ntdll.NtQueryInformationFile(
                file.handle,
                &io_status_block,
                &tag_info,
                @sizeOf(windows.FILE.ATTRIBUTE_TAG_INFO),
                .AttributeTag,
            )) {
                .SUCCESS => break syscall.finish(),
                // INFO_LENGTH_MISMATCH and ACCESS_DENIED are the only documented possible errors
                // https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/d295752f-ce89-4b98-8553-266d37c84f0e
                .INFO_LENGTH_MISMATCH => |err| return syscall.ntstatusBug(err),
                .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
                .CANCELLED => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |s| return syscall.unexpectedNtstatus(s),
            };
            if (tag_info.ReparseTag.IsSurrogate) break :reparse_point .sym_link;
            // Unknown reparse point
            break :reparse_point .unknown;
        } else if (info.BasicInformation.FileAttributes.DIRECTORY)
            .directory
        else
            .file,
        .atime = windows.fromSysTime(info.BasicInformation.LastAccessTime),
        .mtime = windows.fromSysTime(info.BasicInformation.LastWriteTime),
        .ctime = windows.fromSysTime(info.BasicInformation.ChangeTime),
        .nlink = info.StandardInformation.NumberOfLinks,
        .block_size = block_size,
    };
}

fn systemBasicInformation(t: *Threaded) ?*const windows.SYSTEM.BASIC_INFORMATION {
    if (!t.system_basic_information.initialized.load(.acquire)) {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);

        switch (windows.ntdll.NtQuerySystemInformation(
            .Basic,
            &t.system_basic_information.buffer,
            @sizeOf(windows.SYSTEM.BASIC_INFORMATION),
            null,
        )) {
            .SUCCESS => {},
            else => return null,
        }

        t.system_basic_information.initialized.store(true, .release);
    }
    return &t.system_basic_information.buffer;
}

fn fileStatWasi(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    if (builtin.link_libc) return fileStatPosix(userdata, file);

    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    const syscall: Syscall = try .start();
    while (true) {
        var stat: std.os.wasi.filestat_t = undefined;
        switch (std.os.wasi.fd_filestat_get(file.handle, &stat)) {
            .SUCCESS => {
                syscall.finish();
                return statFromWasi(&stat);
            },
            .INTR => {
                try syscall.checkCancel();
                continue;
            },
            else => |e| {
                syscall.finish();
                switch (e) {
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOMEM => return error.SystemResources,
                    .ACCES => return error.AccessDenied,
                    .NOTCAPABLE => return error.AccessDenied,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

const dirAccess = switch (native_os) {
    .windows => dirAccessWindows,
    .wasi => dirAccessWasi,
    else => dirAccessPosix,
};

fn dirAccessPosix(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.AccessOptions,
) Dir.AccessError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const flags: u32 = @as(u32, if (!options.follow_symlinks) posix.AT.SYMLINK_NOFOLLOW else 0);

    const mode: u32 =
        @as(u32, if (options.read) posix.R_OK else 0) |
        @as(u32, if (options.write) posix.W_OK else 0) |
        @as(u32, if (options.execute) posix.X_OK else 0);

    const syscall: Syscall = try .start();
    while (true) {
        switch (posix.errno(posix.system.faccessat(dir.handle, sub_path_posix, mode, flags))) {
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
                    .ROFS => return error.ReadOnlyFileSystem,
                    .LOOP => return error.SymLinkLoop,
                    .TXTBSY => return error.FileBusy,
                    .NOTDIR => return error.FileNotFound,
                    .NOENT => return error.FileNotFound,
                    .NAMETOOLONG => return error.NameTooLong,
                    .INVAL => |err| return errnoBug(err),
                    .FAULT => |err| return errnoBug(err),
                    .IO => return error.InputOutput,
                    .NOMEM => return error.SystemResources,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirAccessWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.AccessOptions,
) Dir.AccessError!void {
    if (builtin.link_libc) return dirAccessPosix(userdata, dir, sub_path, options);
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const wasi = std.os.wasi;
    const flags: wasi.lookupflags_t = .{
        .SYMLINK_FOLLOW = options.follow_symlinks,
    };
    var stat: wasi.filestat_t = undefined;

    const syscall: Syscall = try .start();
    while (true) {
        switch (wasi.path_filestat_get(dir.handle, flags, sub_path.ptr, sub_path.len, &stat)) {
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
                    .INVAL => |err| return errnoBug(err),
                    .BADF => |err| return errnoBug(err), // File descriptor used after closed.
                    .NOMEM => return error.SystemResources,
                    .ACCES => return error.AccessDenied,
                    .FAULT => |err| return errnoBug(err),
                    .NAMETOOLONG => return error.NameTooLong,
                    .NOENT => return error.FileNotFound,
                    .NOTDIR => return error.FileNotFound,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }

    if (!options.read and !options.write and !options.execute)
        return;

    var directory: wasi.fdstat_t = undefined;
    if (wasi.fd_fdstat_get(dir.handle, &directory) != .SUCCESS)
        return error.AccessDenied;

    var rights: wasi.rights_t = .{};
    if (options.read) {
        if (stat.filetype == .DIRECTORY) {
            rights.FD_READDIR = true;
        } else {
            rights.FD_READ = true;
        }
    }
    if (options.write)
        rights.FD_WRITE = true;

    // No validation for execution.

    // https://github.com/ziglang/zig/issues/18882
    const rights_int: u64 = @bitCast(rights);
    const inheriting_int: u64 = @bitCast(directory.fs_rights_inheriting);
    if ((rights_int & inheriting_int) != rights_int)
        return error.AccessDenied;
}

fn dirAccessWindows(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.AccessOptions,
) Dir.AccessError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    _ = options; // TODO

    if (std.mem.eql(u8, sub_path, ".") or std.mem.eql(u8, sub_path, "..")) return;
    const sub_path_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
    const attr: windows.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w.span())) null else dir.handle,
        .ObjectName = @constCast(&sub_path_w.string()),
    };
    var basic_info: windows.FILE.BASIC_INFORMATION = undefined;
    const syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtQueryAttributesFile(&attr, &basic_info)) {
        .SUCCESS => return syscall.finish(),
        .CANCELLED => {
            try syscall.checkCancel();
            continue;
        },
        .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
        .OBJECT_NAME_INVALID => |err| return syscall.ntstatusBug(err),
        .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .OBJECT_PATH_SYNTAX_BAD => |err| return syscall.ntstatusBug(err),
        else => |rc| return syscall.unexpectedNtstatus(rc),
    };
}

const dirCreateFile = switch (native_os) {
    .windows => dirCreateFileWindows,
    .wasi => dirCreateFileWasi,
    else => dirCreateFilePosix,
};

fn dirCreateFilePosix(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.CreateFileOptions,
) File.OpenError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var flags: posix.O = .{
        .ACCMODE = if (options.read) .RDWR else .WRONLY,
        .CREAT = true,
        .TRUNC = options.truncate,
        .EXCL = options.exclusive,
    };
    if (@hasField(posix.O, "LARGEFILE")) flags.LARGEFILE = true;
    if (@hasField(posix.O, "CLOEXEC")) flags.CLOEXEC = true;
    if (@hasField(posix.O, "RESOLVE_BENEATH")) flags.RESOLVE_BENEATH = options.resolve_beneath;

    // Use the O locking flags if the os supports them to acquire the lock
    // atomically. Note that the NONBLOCK flag is removed after the openat()
    // call is successful.
    if (have_flock_open_flags) switch (options.lock) {
        .none => {},
        .shared => {
            flags.SHLOCK = true;
            flags.NONBLOCK = options.lock_nonblocking;
        },
        .exclusive => {
            flags.EXLOCK = true;
            flags.NONBLOCK = options.lock_nonblocking;
        },
    };

    const fd: posix.fd_t = fd: {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = openat_sym(dir.handle, sub_path_posix, flags, options.permissions.toMode());
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
                        .OPNOTSUPP => return error.FileLocksUnsupported,
                        .AGAIN => return error.WouldBlock,
                        .TXTBSY => return error.FileBusy,
                        .NXIO => return error.NoDevice,
                        .ROFS => return error.ReadOnlyFileSystem,
                        .ILSEQ => return error.BadPathName,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    };
    errdefer closeFd(fd);

    if (have_flock and !have_flock_open_flags and options.lock != .none) {
        const lock_nonblocking: i32 = if (options.lock_nonblocking) posix.LOCK.NB else 0;
        const lock_flags = switch (options.lock) {
            .none => unreachable,
            .shared => posix.LOCK.SH | lock_nonblocking,
            .exclusive => posix.LOCK.EX | lock_nonblocking,
        };

        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(posix.system.flock(fd, lock_flags))) {
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
                        .INVAL => |err| return errnoBug(err), // invalid parameters
                        .NOLCK => return error.SystemResources,
                        .AGAIN => return error.WouldBlock,
                        .OPNOTSUPP => return error.FileLocksUnsupported,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    if (have_flock_open_flags and options.lock_nonblocking) {
        var fl_flags: usize = fl: {
            const syscall: Syscall = try .start();
            while (true) {
                const rc = posix.system.fcntl(fd, posix.F.GETFL, @as(usize, 0));
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        break :fl @intCast(rc);
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |err| {
                        syscall.finish();
                        return posix.unexpectedErrno(err);
                    },
                }
            }
        };

        fl_flags |= @as(usize, 1 << @bitOffsetOf(posix.O, "NONBLOCK"));

        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(posix.system.fcntl(fd, posix.F.SETFL, fl_flags))) {
                .SUCCESS => {
                    syscall.finish();
                    break;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |err| {
                    syscall.finish();
                    return posix.unexpectedErrno(err);
                },
            }
        }
    }

    return .{
        .handle = fd,
        .flags = .{ .nonblocking = false },
    };
}

fn dirCreateFileWindows(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: Dir.CreateFileOptions,
) File.OpenError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;

    if (std.mem.eql(u8, sub_path, ".")) return error.IsDir;
    if (std.mem.eql(u8, sub_path, "..")) return error.IsDir;

    const sub_path_w = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
    const attr: windows.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w.span())) null else dir.handle,
        .ObjectName = @constCast(&sub_path_w.string()),
    };
    const create_disposition: windows.FILE.CREATE_DISPOSITION = if (flags.exclusive)
        .CREATE
    else if (flags.truncate)
        .OVERWRITE_IF
    else
        .OPEN_IF;

    const access_mask: windows.ACCESS_MASK = .{
        .STANDARD = .{ .SYNCHRONIZE = true },
        .GENERIC = .{
            .WRITE = true,
            .READ = flags.read,
        },
    };

    var io_status_block: windows.IO_STATUS_BLOCK = undefined;
    var attempt: u5 = 0;
    var handle: windows.HANDLE = undefined;
    var syscall: Syscall = try .start();
    while (true) switch (windows.ntdll.NtCreateFile(
        &handle,
        access_mask,
        &attr,
        &io_status_block,
        null,
        .{ .NORMAL = true },
        .VALID_FLAGS, // share access
        create_disposition,
        .{
            .NON_DIRECTORY_FILE = true,
            .IO = .SYNCHRONOUS_NONALERT,
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
        .NO_MEDIA_IN_DEVICE => return syscall.fail(error.NoDevice),
        .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
        .PIPE_BUSY => return syscall.fail(error.PipeBusy),
        .PIPE_NOT_AVAILABLE => return syscall.fail(error.NoDevice),
        .OBJECT_NAME_COLLISION => return syscall.fail(error.PathAlreadyExists),
        .FILE_IS_A_DIRECTORY => return syscall.fail(error.IsDir),
        .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
        .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
        .VIRUS_INFECTED, .VIRUS_DELETED => return syscall.fail(error.AntivirusInterference),
        .DISK_FULL => return syscall.fail(error.NoSpaceLeft),
        .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
        .OBJECT_PATH_SYNTAX_BAD => |status| return syscall.ntstatusBug(status),
        .INVALID_HANDLE => |status| return syscall.ntstatusBug(status),
        else => |status| return syscall.unexpectedNtstatus(status),
    };
    errdefer windows.CloseHandle(handle);

    const exclusive = switch (flags.lock) {
        .none => return .{
            .handle = handle,
            .flags = .{ .nonblocking = false },
        },
        .shared => false,
        .exclusive => true,
    };

    syscall = try .start();
    while (true) switch (windows.ntdll.NtLockFile(
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
        .SUCCESS => {
            syscall.finish();
            return .{
                .handle = handle,
                .flags = .{ .nonblocking = false },
            };
        },
        .INSUFFICIENT_RESOURCES => return syscall.fail(error.SystemResources),
        .LOCK_NOT_GRANTED => return syscall.fail(error.WouldBlock),
        .ACCESS_VIOLATION => |err| return syscall.ntstatusBug(err), // bad io_status_block pointer
        else => |status| return syscall.unexpectedNtstatus(status),
    };
}

fn dirCreateFileWasi(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: Dir.CreateFileOptions,
) File.OpenError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const wasi = std.os.wasi;
    const lookup_flags: wasi.lookupflags_t = .{};
    const oflags: wasi.oflags_t = .{
        .CREAT = true,
        .TRUNC = flags.truncate,
        .EXCL = flags.exclusive,
    };
    const fdflags: wasi.fdflags_t = .{};
    const base: wasi.rights_t = .{
        .FD_READ = flags.read,
        .FD_WRITE = true,
        .FD_DATASYNC = true,
        .FD_SEEK = true,
        .FD_TELL = true,
        .FD_FDSTAT_SET_FLAGS = true,
        .FD_SYNC = true,
        .FD_ALLOCATE = true,
        .FD_ADVISE = true,
        .FD_FILESTAT_SET_TIMES = true,
        .FD_FILESTAT_SET_SIZE = true,
        .FD_FILESTAT_GET = true,
        // POLL_FD_READWRITE only grants extra rights if the corresponding FD_READ and/or
        // FD_WRITE is also set.
        .POLL_FD_READWRITE = true,
    };
    const inheriting: wasi.rights_t = .{};
    var fd: posix.fd_t = undefined;
    const syscall: Syscall = try .start();
    while (true) {
        switch (wasi.path_open(dir.handle, lookup_flags, sub_path.ptr, sub_path.len, oflags, base, inheriting, fdflags, &fd)) {
            .SUCCESS => {
                syscall.finish();
                return .{
                    .handle = fd,
                    .flags = .{ .nonblocking = false },
                };
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
                    .NOMEM => return error.SystemResources,
                    .NOSPC => return error.NoSpaceLeft,
                    .NOTDIR => return error.NotDir,
                    .PERM => return error.PermissionDenied,
                    .EXIST => return error.PathAlreadyExists,
                    .BUSY => return error.DeviceBusy,
                    .NOTCAPABLE => return error.AccessDenied,
                    .ILSEQ => return error.BadPathName,
                    else => |err| return posix.unexpectedErrno(err),
                }
            },
        }
    }
}

fn dirCreateFileAtomic(
    userdata: ?*anyopaque,
    dir: Dir,
    dest_path: []const u8,
    options: Dir.CreateFileAtomicOptions,
) Dir.CreateFileAtomicError!File.Atomic {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const t_io = io(t);

    // Linux has O_TMPFILE, but linkat() does not support AT_REPLACE, so it's
    // useless when we have to make up a bogus path name to do the rename()
    // anyway.
    if (native_os == .linux and !options.replace) tmpfile: {
        const flags: posix.O = if (@hasField(posix.O, "TMPFILE")) .{
            .ACCMODE = .RDWR,
            .TMPFILE = true,
            .DIRECTORY = true,
            .CLOEXEC = true,
        } else if (@hasField(posix.O, "TMPFILE0") and !@hasField(posix.O, "TMPFILE2")) .{
            .ACCMODE = .RDWR,
            .TMPFILE0 = true,
            .TMPFILE1 = true,
            .DIRECTORY = true,
            .CLOEXEC = true,
        } else break :tmpfile;

        const dest_dirname = Dir.path.dirname(dest_path);
        if (dest_dirname) |dirname| {
            // This has a nice side effect of preemptively triggering EISDIR or
            // ENOENT, avoiding the ambiguity below.
            if (options.make_path) dir.createDirPath(t_io, dirname) catch |err| switch (err) {
                // None of these make sense in this context.
                error.IsDir,
                error.Streaming,
                error.DiskQuota,
                error.PathAlreadyExists,
                error.LinkQuotaExceeded,
                error.PipeBusy,
                error.FileTooBig,
                error.DeviceBusy,
                error.FileLocksUnsupported,
                error.FileBusy,
                => return error.Unexpected,

                else => |e| return e,
            };
        }

        var path_buffer: [posix.PATH_MAX]u8 = undefined;
        const sub_path_posix = try pathToPosix(dest_dirname orelse ".", &path_buffer);

        const syscall: Syscall = try .start();
        while (true) {
            const rc = openat_sym(dir.handle, sub_path_posix, flags, options.permissions.toMode());
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    return .{
                        .file = .{
                            .handle = @intCast(rc),
                            .flags = .{ .nonblocking = false },
                        },
                        .file_basename_hex = 0,
                        .dest_sub_path = dest_path,
                        .file_open = true,
                        .file_exists = false,
                        .close_dir_on_deinit = false,
                        .dir = dir,
                    };
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                .ISDIR, .NOENT, .OPNOTSUPP => {
                    // Ambiguous error code. It might mean the file system
                    // does not support O_TMPFILE. Therefore, we must fall
                    // back to not using O_TMPFILE.
                    syscall.finish();
                    break :tmpfile;
                },
                .INVAL => return syscall.fail(error.BadPathName),
                .ACCES => return syscall.fail(error.AccessDenied),
                .LOOP => return syscall.fail(error.SymLinkLoop),
                .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
                .NAMETOOLONG => return syscall.fail(error.NameTooLong),
                .NFILE => return syscall.fail(error.SystemFdQuotaExceeded),
                .NODEV => return syscall.fail(error.NoDevice),
                .NOMEM => return syscall.fail(error.SystemResources),
                .NOSPC => return syscall.fail(error.NoSpaceLeft),
                .NOTDIR => return syscall.fail(error.NotDir),
                .PERM => return syscall.fail(error.PermissionDenied),
                .AGAIN => return syscall.fail(error.WouldBlock),
                .NXIO => return syscall.fail(error.NoDevice),
                .ILSEQ => return syscall.fail(error.BadPathName),
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    }

    if (Dir.path.dirname(dest_path)) |dirname| {
        const new_dir = if (options.make_path)
            dir.createDirPathOpen(t_io, dirname, .{}) catch |err| switch (err) {
                // None of these make sense in this context.
                error.IsDir,
                error.Streaming,
                error.DiskQuota,
                error.PathAlreadyExists,
                error.LinkQuotaExceeded,
                error.PipeBusy,
                error.FileTooBig,
                error.FileLocksUnsupported,
                error.DeviceBusy,
                => return error.Unexpected,

                else => |e| return e,
            }
        else
            try dir.openDir(t_io, dirname, .{});

        return atomicFileInit(t_io, Dir.path.basename(dest_path), options.permissions, new_dir, true);
    }

    return atomicFileInit(t_io, dest_path, options.permissions, dir, false);
}

fn atomicFileInit(
    t_io: Io,
    dest_basename: []const u8,
    permissions: File.Permissions,
    dir: Dir,
    close_dir_on_deinit: bool,
) Dir.CreateFileAtomicError!File.Atomic {
    while (true) {
        var random_integer: u64 = undefined;
        t_io.random(@ptrCast(&random_integer));
        const tmp_sub_path = std.fmt.hex(random_integer);
        const file = dir.createFile(t_io, &tmp_sub_path, .{
            .permissions = permissions,
            .exclusive = true,
        }) catch |err| switch (err) {
            error.PathAlreadyExists => continue,
            error.DeviceBusy => continue,
            error.FileBusy => continue,

            error.IsDir => return error.Unexpected, // No path components.
            error.FileTooBig => return error.Unexpected, // Creating, not opening.
            error.FileLocksUnsupported => return error.Unexpected, // Not asking for locks.
            error.PipeBusy => return error.Unexpected, // Not opening a pipe.

            else => |e| return e,
        };
        return .{
            .file = file,
            .file_basename_hex = random_integer,
            .dest_sub_path = dest_basename,
            .file_open = true,
            .file_exists = true,
            .close_dir_on_deinit = close_dir_on_deinit,
            .dir = dir,
        };
    }
}

const dirOpenFile = switch (native_os) {
    .windows => dirOpenFileWindows,
    .wasi => dirOpenFileWasi,
    else => dirOpenFilePosix,
};

fn dirOpenFilePosix(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.OpenFileOptions,
) File.OpenError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));

    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var flags: posix.O = switch (native_os) {
        .wasi => .{
            .read = options.mode != .write_only,
            .write = options.mode != .read_only,
            .NOFOLLOW = !options.follow_symlinks,
        },
        else => .{
            .ACCMODE = switch (options.mode) {
                .read_only => .RDONLY,
                .write_only => .WRONLY,
                .read_write => .RDWR,
            },
            .NOFOLLOW = !options.follow_symlinks,
        },
    };
    if (@hasField(posix.O, "CLOEXEC")) flags.CLOEXEC = true;
    if (@hasField(posix.O, "LARGEFILE")) flags.LARGEFILE = true;
    if (@hasField(posix.O, "NOCTTY")) flags.NOCTTY = !options.allow_ctty;
    if (@hasField(posix.O, "PATH")) flags.PATH = options.path_only;
    if (@hasField(posix.O, "RESOLVE_BENEATH")) flags.RESOLVE_BENEATH = options.resolve_beneath;

    // Use the O locking options if the os supports them to acquire the lock
    // atomically. Note that the NONBLOCK flag is removed after the openat()
    // call is successful.
    if (have_flock_open_flags) switch (options.lock) {
        .none => {},
        .shared => {
            flags.SHLOCK = true;
            flags.NONBLOCK = options.lock_nonblocking;
        },
        .exclusive => {
            flags.EXLOCK = true;
            flags.NONBLOCK = options.lock_nonblocking;
        },
    };

    const mode: posix.mode_t = 0;

    const fd: posix.fd_t = fd: {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = openat_sym(dir.handle, sub_path_posix, flags, mode);
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
                        .OPNOTSUPP => return error.FileLocksUnsupported,
                        .AGAIN => return error.WouldBlock,
                        .TXTBSY => return error.FileBusy,
                        .NXIO => return error.NoDevice,
                        .ROFS => return error.ReadOnlyFileSystem,
                        .ILSEQ => return error.BadPathName,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    };
    errdefer closeFd(fd);

    if (!options.allow_directory) {
        const is_dir = is_dir: {
            const stat = fileStat(t, .{
                .handle = fd,
                .flags = .{ .nonblocking = false },
            }) catch |err| switch (err) {
                // The directory-ness is either unknown or unknowable
                error.Streaming => break :is_dir false,
                else => |e| return e,
            };
            break :is_dir stat.kind == .directory;
        };
        if (is_dir) return error.IsDir;
    }

    if (have_flock and !have_flock_open_flags and options.lock != .none) {
        const lock_nonblocking: i32 = if (options.lock_nonblocking) posix.LOCK.NB else 0;
        const lock_flags = switch (options.lock) {
            .none => unreachable,
            .shared => posix.LOCK.SH | lock_nonblocking,
            .exclusive => posix.LOCK.EX | lock_nonblocking,
        };
        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(posix.system.flock(fd, lock_flags))) {
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
                        .INVAL => |err| return errnoBug(err), // invalid parameters
                        .NOLCK => return error.SystemResources,
                        .AGAIN => return error.WouldBlock,
                        .OPNOTSUPP => return error.FileLocksUnsupported,
                        else => |err| return posix.unexpectedErrno(err),
                    }
                },
            }
        }
    }

    if (have_flock_open_flags and options.lock_nonblocking) {
        var fl_flags: usize = fl: {
            const syscall: Syscall = try .start();
            while (true) {
                const rc = posix.system.fcntl(fd, posix.F.GETFL, @as(usize, 0));
                switch (posix.errno(rc)) {
                    .SUCCESS => {
                        syscall.finish();
                        break :fl @intCast(rc);
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => |err| {
                        syscall.finish();
                        return posix.unexpectedErrno(err);
                    },
                }
            }
        };

        fl_flags |= @as(usize, 1 << @bitOffsetOf(posix.O, "NONBLOCK"));

        const syscall: Syscall = try .start();
        while (true) {
            switch (posix.errno(posix.system.fcntl(fd, posix.F.SETFL, fl_flags))) {
                .SUCCESS => {
                    syscall.finish();
                    break;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |err| {
                    syscall.finish();
                    return posix.unexpectedErrno(err);
                },
            }
        }
    }

    return .{
        .handle = fd,
        .flags = .{ .nonblocking = false },
    };
}

fn dirOpenFileWindows(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: Dir.OpenFileOptions,
) File.OpenError!File {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const sub_path_w_array = try sliceToPrefixedFileW(dir.handle, sub_path, .{});
    const sub_path_w = sub_path_w_array.span();
    const dir_handle = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w)) null else dir.handle;
    return dirOpenFileWtf16(dir_handle, sub_path_w, flags);
}

pub fn dirOpenFileWtf16(
    dir_handle: ?windows.HANDLE,
    sub_path_w: []const u16,
    flags: Dir.OpenFileOptions,
) File.OpenError!File {
    const allow_directory = flags.allow_directory and !flags.isWrite();
    if (!allow_directory and std.mem.eql(u16, sub_path_w, &.{'.'})) return error.IsDir;
    if (!allow_directory and std.mem.eql(u16, sub_path_w, &.{ '.', '.' })) return error.IsDir;
    const w = windows;

    var io_status_block: w.IO_STATUS_BLOCK = undefined;
    var attempt: u5 = 0;
    var syscall: Syscall = try .start();
    const handle = while (true) {
        var result: w.HANDLE = undefined;
        switch (w.ntdll.NtCreateFile(
            &result,
            .{
                .STANDARD = .{ .SYNCHRONIZE = true },
                .GENERIC = .{
                    .READ = flags.isRead(),
                    .WRITE = flags.isWrite(),
                },
            },
            &.{
                .RootDirectory = dir_handle,
                .ObjectName = @constCast(&w.UNICODE_STRING.init(sub_path_w)),
            },
            &io_status_block,
            null,
            .{ .NORMAL = true },
            .VALID_FLAGS,
            .OPEN,
            .{
                .IO = if (flags.follow_symlinks) .SYNCHRONOUS_NONALERT else .ASYNCHRONOUS,
                .NON_DIRECTORY_FILE = !allow_directory,
                .OPEN_REPARSE_POINT = !flags.follow_symlinks,
            },
            null,
            0,
        )) {
            .SUCCESS => {
                syscall.finish();
                break result;
            },
            .OBJECT_NAME_INVALID => return syscall.fail(error.BadPathName),
            .OBJECT_NAME_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .OBJECT_PATH_NOT_FOUND => return syscall.fail(error.FileNotFound),
            .BAD_NETWORK_PATH => return syscall.fail(error.NetworkNotFound), // \\server was not found
            .BAD_NETWORK_NAME => return syscall.fail(error.NetworkNotFound), // \\server was found but \\server\share wasn't
            .NO_MEDIA_IN_DEVICE => return syscall.fail(error.NoDevice),
            .INVALID_PARAMETER => |err| return syscall.ntstatusBug(err),
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
            .ACCESS_DENIED => return syscall.fail(error.AccessDenied),
            .PIPE_BUSY => return syscall.fail(error.PipeBusy),
            .PIPE_NOT_AVAILABLE => return syscall.fail(error.NoDevice),
            .OBJECT_PATH_SYNTAX_BAD => |err| return syscall.ntstatusBug(err),
            .OBJECT_NAME_COLLISION => return syscall.fail(error.PathAlreadyExists),
            .FILE_IS_A_DIRECTORY => return syscall.fail(error.IsDir),
            .NOT_A_DIRECTORY => return syscall.fail(error.NotDir),
            .USER_MAPPED_FILE => return syscall.fail(error.AccessDenied),
            .INVALID_HANDLE => |err| return syscall.ntstatusBug(err),
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
            .VIRUS_INFECTED, .VIRUS_DELETED => return syscall.fail(error.AntivirusInterference),
            else => |rc| return syscall.unexpectedNtstatus(rc),
        }
    };
    errdefer w.CloseHandle(handle);

    const exclusive = switch (flags.lock) {
        .none => return .{
            .handle = handle,
            .flags = .{ .nonblocking = false },
        },
        .shared => false,
       
```
