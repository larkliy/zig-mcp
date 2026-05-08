```
mpare(.lte, Fiber.max_result_align)); // TODO
    assert(context_alignment.compare(.lte, Fiber.max_context_align)); // TODO
    assert(result_len <= Fiber.max_result_size); // TODO
    assert(context.len <= Fiber.max_context_size); // TODO

    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const fiber = Fiber.create(ev) catch |err| switch (err) {
        error.OutOfMemory => return error.ConcurrencyUnavailable,
    };

    const closure: *AsyncClosure = .fromFiber(fiber);
    fiber.* = .{
        .required_align = {},
        .evented = ev,
        .context = switch (builtin.cpu.arch) {
            .aarch64 => .{
                .sp = @intFromPtr(closure),
                .fp = 0,
                .pc = @intFromPtr(&AsyncClosure.entry),
            },
            .x86_64 => .{
                .rsp = @intFromPtr(closure) - 8,
                .rbp = 0,
                .rip = @intFromPtr(&AsyncClosure.entry),
            },
            else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
        },
        .link = .{ .awaiter = null },
        .awaiting_group = undefined,
        .cancel_status = .unrequested,
        .cancel_protection = .unblocked,
    };
    closure.* = .{
        .evented = ev,
        .fiber = fiber,
        .start = start,
        .result_align = result_alignment,
    };
    @memcpy(closure.contextPointer(), context);

    ev.queue.async(fiber, &Fiber.@"resume");
    return @ptrCast(fiber);
}

fn await(
    userdata: ?*anyopaque,
    future: *std.Io.AnyFuture,
    result: []u8,
    result_alignment: Alignment,
) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const awaiting: *Fiber = @ptrCast(@alignCast(future));
    if (@atomicLoad(?*Fiber, &awaiting.link.awaiter, .acquire) != Fiber.finished)
        ev.yield(.{ .await = awaiting });
    @memcpy(result, awaiting.resultBytes(result_alignment));
    awaiting.destroy(ev);
}

fn cancel(
    userdata: ?*anyopaque,
    future: *std.Io.AnyFuture,
    result: []u8,
    result_alignment: Alignment,
) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const future_fiber: *Fiber = @ptrCast(@alignCast(future));
    future_fiber.requestCancel(ev);
    await(ev, future, result, result_alignment);
}

const Group = struct {
    ptr: *Io.Group,

    const List = packed struct(usize) {
        cancel_requested: bool,
        awaiter_delayed: bool,
        fibers: Fiber.PackedPtr,
    };
    fn listPtr(group: Group) *List {
        return @ptrCast(&group.ptr.token);
    }

    const Mutex = packed struct(u32) {
        locked: bool,
        contended: bool,
        shared2: u30,
    };
    fn mutexPtr(group: Group) *Group.Mutex {
        return switch (comptime builtin.cpu.arch.endian()) {
            .little => @ptrCast(&group.ptr.state),
            .big => @ptrCast(@alignCast(
                @as([*]u8, @ptrCast(&group.ptr.state)) + @sizeOf(usize) - @sizeOf(u32),
            )),
        };
    }

    const Awaiter = packed struct(usize) {
        locked: bool,
        contended: bool,
        awaiter: Fiber.PackedPtr,
    };
    fn awaiterPtr(group: Group) *Awaiter {
        return @ptrCast(&group.ptr.state);
    }

    fn lock(group: Group, ev: *Evented) void {
        const mutex = group.mutexPtr();
        {
            const old_state = @atomicRmw(
                Group.Mutex,
                mutex,
                .Or,
                .{ .locked = true, .contended = false, .shared2 = 0 },
                .acquire,
            );
            if (!old_state.locked) {
                @branchHint(.likely);
                return;
            }
            if (old_state.contended) {
                futexWaitUncancelable(ev, @ptrCast(mutex), @bitCast(old_state));
            }
        }
        while (true) {
            var old_state = @atomicRmw(
                Group.Mutex,
                mutex,
                .Or,
                .{ .locked = true, .contended = true, .shared2 = 0 },
                .acquire,
            );
            if (!old_state.locked) {
                @branchHint(.likely);
                return;
            }
            old_state.contended = true;
            futexWaitUncancelable(ev, @ptrCast(mutex), @bitCast(old_state));
        }
    }

    fn unlock(group: Group, ev: *Evented) void {
        const mutex = group.mutexPtr();
        const old_state = @atomicRmw(
            Group.Mutex,
            mutex,
            .And,
            .{ .locked = false, .contended = false, .shared2 = std.math.maxInt(u30) },
            .release,
        );
        assert(old_state.locked);
        if (old_state.contended) futexWake(ev, @ptrCast(mutex), 1);
    }

    fn addFiber(group: Group, ev: *Evented, fiber: *Fiber) void {
        group.lock(ev);
        defer group.unlock(ev);
        const list_ptr = group.listPtr();
        const list = @atomicLoad(List, list_ptr, .monotonic);
        if (list.cancel_requested) fiber.cancel_status = .{ .requested = true, .awaiting = .nothing };
        const old_head = list.fibers.unpack();
        if (old_head) |head| head.link.group.prev = fiber;
        fiber.link.group.next = old_head;
        @atomicStore(List, list_ptr, .{
            .cancel_requested = list.cancel_requested,
            .awaiter_delayed = list.awaiter_delayed,
            .fibers = .pack(fiber),
        }, .monotonic);
    }

    fn removeFiber(group: Group, ev: *Evented, fiber: *Fiber) ?*Fiber {
        group.lock(ev);
        defer group.unlock(ev);
        const list_ptr = group.listPtr();
        const list = @atomicLoad(List, list_ptr, .monotonic);
        if (fiber.link.group.next) |next| next.link.group.prev = fiber.link.group.prev;
        if (fiber.link.group.prev) |prev| {
            prev.link.group.next = fiber.link.group.next;
        } else if (fiber.link.group.next) |new_head| {
            @atomicStore(List, list_ptr, .{
                .cancel_requested = list.cancel_requested,
                .awaiter_delayed = list.awaiter_delayed,
                .fibers = .pack(new_head),
            }, .monotonic);
        } else if (@atomicLoad(Awaiter, group.awaiterPtr(), .monotonic).awaiter.unpack()) |awaiter| {
            if (!awaiter.cancel_status.changeAwaiting(.group, .nothing) or list.cancel_requested) {
                @atomicStore(List, list_ptr, .{
                    .cancel_requested = false,
                    .awaiter_delayed = false,
                    .fibers = .null,
                }, .release);
                assert(awaiter.awaiting_group.ptr == group.ptr);
                awaiter.awaiting_group = undefined;
                return awaiter;
            }
            // Race with `Fiber.requestCancel`
            @atomicStore(List, list_ptr, .{
                .cancel_requested = false,
                .awaiter_delayed = true,
                .fibers = .null,
            }, .monotonic);
        } else @atomicStore(List, list_ptr, .{
            .cancel_requested = false,
            .awaiter_delayed = false,
            .fibers = .null,
        }, .release);
        return null;
    }

    fn await(group: Group, ev: *Evented, awaiter: *Fiber) bool {
        group.lock(ev);
        defer group.unlock(ev);
        if (@atomicLoad(List, group.listPtr(), .monotonic).fibers.unpack()) |_| {
            if (group.registerAwaiter(awaiter) and awaiter.cancel_protection.check() == .unblocked) {
                // The awaiter already had an unacknowledged cancelation request before
                // attempting to await a group, so propagate the cancelation to the group.
                assert(!group.cancelLocked(ev, null));
            }
            return false;
        }
        return true;
    }

    fn cancel(group: Group, ev: *Evented, maybe_awaiter: ?*Fiber) bool {
        group.lock(ev);
        defer group.unlock(ev);
        return group.cancelLocked(ev, maybe_awaiter);
    }

    /// Assumes the mutex is held.
    fn cancelLocked(group: Group, ev: *Evented, maybe_awaiter: ?*Fiber) bool {
        const list_ptr = group.listPtr();
        const list = @atomicRmw(
            List,
            list_ptr,
            .Add,
            .{ .cancel_requested = true, .awaiter_delayed = false, .fibers = .null },
            .monotonic,
        );
        assert(!list.cancel_requested);
        if (list.fibers.unpack()) |head| {
            var maybe_fiber: ?*Fiber = head;
            while (maybe_fiber) |fiber| {
                fiber.requestCancel(ev);
                maybe_fiber = fiber.link.group.next;
            }
            if (maybe_awaiter) |awaiter| _ = group.registerAwaiter(awaiter);
            return false;
        }
        @atomicStore(
            List,
            list_ptr,
            .{ .cancel_requested = false, .awaiter_delayed = false, .fibers = .null },
            .release,
        );
        return if (maybe_awaiter) |_| true else list.awaiter_delayed;
    }

    /// Assumes the mutex is held.
    fn registerAwaiter(group: Group, awaiter: *Fiber) bool {
        awaiter.awaiting_group = group;
        assert(@atomicRmw(
            Awaiter,
            group.awaiterPtr(),
            .Add,
            .{ .locked = false, .contended = false, .awaiter = .pack(awaiter) },
            .monotonic,
        ).awaiter == .null);
        return awaiter.cancel_status.changeAwaiting(.nothing, .group);
    }

    const AsyncClosure = struct {
        evented: *Evented,
        group: Group,
        fiber: *Fiber,
        start: *const fn (context: *const anyopaque) void,

        fn fromFiber(fiber: *Fiber) *Group.AsyncClosure {
            return @ptrFromInt(Fiber.max_context_align.max(.of(Group.AsyncClosure)).backward(
                @intFromPtr(fiber.allocatedEnd()) - Fiber.max_context_size,
            ) - @sizeOf(Group.AsyncClosure));
        }

        fn contextPointer(
            closure: *Group.AsyncClosure,
        ) [*]align(Fiber.max_context_align.toByteUnits()) u8 {
            return @alignCast(@as([*]u8, @ptrCast(closure)) + @sizeOf(Group.AsyncClosure));
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
            closure: *Group.AsyncClosure,
            message: *const SwitchMessage,
        ) callconv(.withStackAlign(.c, @alignOf(Group.AsyncClosure))) noreturn {
            const ev = closure.evented;
            const fiber = closure.fiber;
            message.handle(ev);
            closure.start(closure.contextPointer());
            if (closure.group.removeFiber(ev, fiber)) |awaiter| ev.queue.async(awaiter, &Fiber.@"resume");
            ev.yield(.destroy);
            unreachable; // switched to dead fiber
        }
    };
};

fn groupAsync(
    userdata: ?*anyopaque,
    type_erased: *Io.Group,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque) void,
) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return groupConcurrent(ev, type_erased, context, context_alignment, start) catch {
        start(context.ptr);
    };
}

fn groupConcurrent(
    userdata: ?*anyopaque,
    type_erased: *Io.Group,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque) void,
) Io.ConcurrentError!void {
    assert(context_alignment.compare(.lte, Fiber.max_context_align)); // TODO
    assert(context.len <= Fiber.max_context_size); // TODO

    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const group: Group = .{ .ptr = type_erased };
    const fiber = Fiber.create(ev) catch |err| switch (err) {
        error.OutOfMemory => return error.ConcurrencyUnavailable,
    };

    const closure: *Group.AsyncClosure = .fromFiber(fiber);
    fiber.* = .{
        .required_align = {},
        .evented = ev,
        .context = switch (builtin.cpu.arch) {
            .aarch64 => .{
                .sp = @intFromPtr(closure),
                .fp = 0,
                .pc = @intFromPtr(&Group.AsyncClosure.entry),
            },
            .x86_64 => .{
                .rsp = @intFromPtr(closure) - 8,
                .rbp = 0,
                .rip = @intFromPtr(&Group.AsyncClosure.entry),
            },
            else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
        },
        .link = .{ .group = .{ .prev = null, .next = null } },
        .awaiting_group = undefined,
        .cancel_status = .unrequested,
        .cancel_protection = .unblocked,
    };
    closure.* = .{
        .evented = ev,
        .group = group,
        .fiber = fiber,
        .start = start,
    };
    @memcpy(closure.contextPointer(), context);
    group.addFiber(ev, fiber);
    ev.queue.async(fiber, &Fiber.@"resume");
}

fn groupAwait(
    userdata: ?*anyopaque,
    type_erased: *Io.Group,
    initial_token: *anyopaque,
) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = initial_token;
    ev.yield(.{ .group_await = .{ .ptr = type_erased } });
}

fn groupCancel(userdata: ?*anyopaque, type_erased: *Io.Group, initial_token: *anyopaque) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = initial_token;
    ev.yield(.{ .group_cancel = .{ .ptr = type_erased } });
}

fn recancel(userdata: ?*anyopaque) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    Thread.current().currentFiber().cancel_protection.recancel();
}

fn swapCancelProtection(userdata: ?*anyopaque, new: Io.CancelProtection) Io.CancelProtection {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const cancel_protection = &Thread.current().currentFiber().cancel_protection;
    defer cancel_protection.user = new;
    return cancel_protection.user;
}

fn checkCancel(userdata: ?*anyopaque) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const fiber = Thread.current().currentFiber();
    switch (fiber.cancel_protection.check()) {
        .unblocked => {
            const cancel_status = @atomicLoad(Fiber.CancelStatus, &fiber.cancel_status, .monotonic);
            assert(cancel_status.awaiting == .nothing);
            if (cancel_status.requested) {
                @branchHint(.unlikely);
                fiber.cancel_protection.acknowledge();
                return error.Canceled;
            }
        },
        .blocked => {},
    }
}

const Futex = struct {
    num_waiters: usize,
    queue: c.dispatch.queue_t,
    waiters: std.DoublyLinkedList,

    const Waiter = struct {
        sleeper: Sleeper = undefined,
        cancelable: Cancelable,
        futex: *Futex,
        node: std.DoublyLinkedList.Node = .{},
        ptr: *const u32,
        expected: u32,
        timeout: c.dispatch.time_t = .FOREVER,
        leeway: u64,
        timer: ?c.dispatch.source_t = null,

        const already_signaled: c.dispatch.source_t = @ptrFromInt(1);

        fn add(context: ?*anyopaque) callconv(.c) void {
            const waiter: *Waiter = @ptrCast(@alignCast(context));
            const futex = waiter.futex;
            _ = @atomicRmw(usize, &futex.num_waiters, .Add, 1, .acquire);
            waiter.tryAdd() catch |err| switch (err) {
                error.CancelRequested => {
                    wake(waiter);
                    assert(@atomicRmw(usize, &futex.num_waiters, .Sub, 1, .monotonic) >= 1);
                },
            };
        }

        fn tryAdd(waiter: *Waiter) Cancelable.RequestedError!void {
            if (@atomicLoad(u32, waiter.ptr, .monotonic) != waiter.expected)
                return error.CancelRequested;
            try waiter.cancelable.enter(waiter.sleeper.fiber);
            const futex = waiter.futex;
            switch (waiter.timeout) {
                .FOREVER => {},
                else => |timeout| {
                    const timer = c.dispatch.source_create(.TIMER, 0, .none, futex.queue) orelse {
                        log.warn("failed to create timer for futex timeout", .{});
                        return error.CancelRequested;
                    };
                    timer.as_object().set_context(waiter);
                    timer.set_event_handler(&timedOut);
                    timer.set_cancel_handler(&wake);
                    timer.set_timer(timeout, c.dispatch.TIME_FOREVER, waiter.leeway);
                    timer.as_object().activate();
                    waiter.timer = timer;
                },
            }
            futex.waiters.append(&waiter.node);
        }

        fn canceled(context: ?*anyopaque) callconv(.c) void {
            const cancelable: *Cancelable = @ptrCast(@alignCast(context));
            const waiter: *Waiter = @fieldParentPtr("cancelable", cancelable);
            cancelable.requested(waiter.sleeper.fiber);
            const futex = waiter.futex;
            waiter.remove();
            assert(@atomicRmw(usize, &futex.num_waiters, .Sub, 1, .monotonic) >= 1);
        }

        fn timedOut(context: ?*anyopaque) callconv(.c) void {
            const waiter: *Waiter = @ptrCast(@alignCast(context));
            const futex = waiter.futex;
            waiter.tryRemove() catch |err| switch (err) {
                error.CancelRequested => return,
            };
            assert(@atomicRmw(usize, &futex.num_waiters, .Sub, 1, .monotonic) >= 1);
        }

        fn tryRemove(waiter: *Waiter) Cancelable.RequestedError!void {
            try waiter.cancelable.leave(waiter.sleeper.fiber);
            waiter.remove();
        }

        fn remove(waiter: *Waiter) void {
            waiter.futex.waiters.remove(&waiter.node);
            if (waiter.timer) |timer| timer.cancel() else wake(waiter);
        }

        fn wake(context: ?*anyopaque) callconv(.c) void {
            const waiter: *Waiter = @ptrCast(@alignCast(context));
            if (waiter.timer) |timer| timer.as_object().release();
            Sleeper.wake(&waiter.sleeper);
        }
    };

    const Waker = struct {
        sleeper: Sleeper = undefined,
        futex: *Futex,
        ptr: *const u32,
        max_waiters: u32,

        fn remove(context: ?*anyopaque) callconv(.c) void {
            const waker: *Waker = @ptrCast(@alignCast(context));
            const futex = waker.futex;
            const ptr = waker.ptr;
            const max_waiters = waker.max_waiters;

            var num_removed: usize = 0;
            var next_node = futex.waiters.first;
            while (num_removed < max_waiters) {
                const waiter: *Waiter = @fieldParentPtr("node", next_node orelse break);
                next_node = waiter.node.next;
                if (waiter.ptr != ptr) {
                    @branchHint(.unlikely);
                    continue;
                }
                waiter.tryRemove() catch |err| switch (err) {
                    error.CancelRequested => continue,
                };
                num_removed += 1;
            }
            assert(@atomicRmw(usize, &futex.num_waiters, .Sub, num_removed, .monotonic) >= num_removed);

            var sleeper = waker.sleeper;
            waker.* = undefined;
            Sleeper.wake(&sleeper);
        }
    };

    fn init(futex: *Futex, queue: c.dispatch.queue_t) error{SystemResources}!void {
        futex.* = .{
            .num_waiters = 0,
            .queue = c.dispatch.queue_create_with_target(
                "org.ziglang.std.Io.Dispatch.Futex",
                .SERIAL(),
                queue,
            ) orelse return error.SystemResources,
            .waiters = .{},
        };
    }

    fn deinit(futex: *Futex) void {
        assert(futex.num_waiters == 0 and futex.waiters.first == null and futex.waiters.last == null);
        futex.queue.as_object().release();
        futex.* = undefined;
    }
};

fn futexForAddress(ev: *Evented, address: usize) *Futex {
    // Here we use Fibonacci hashing: the golden ratio can be used to evenly redistribute input
    // values across a range, giving a poor, but extremely quick to compute, hash.

    // This literal is the rounded value of '2^64 / phi' (where 'phi' is the golden ratio). The
    // shift then converts it to '2^b / phi', where 'b' is the pointer bit width.
    const fibonacci_multiplier = 0x9E3779B97F4A7C15 >> (64 - @bitSizeOf(usize));
    const hashed = address *% fibonacci_multiplier;
    comptime assert(std.math.isPowerOfTwo(ev.futexes.len));
    // The high bits of `hashed` have better entropy than the low bits.
    return &ev.futexes[hashed >> @clz(ev.futexes.len - 1)];
}

fn futexWait(
    userdata: ?*anyopaque,
    ptr: *const u32,
    expected: u32,
    timeout: Io.Timeout,
) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const futex = ev.futexForAddress(@intFromPtr(ptr));
    var waiter: Futex.Waiter = .{
        .cancelable = .{ .queue = futex.queue, .cancel = &Futex.Waiter.canceled },
        .futex = futex,
        .ptr = ptr,
        .expected = expected,
        .timeout = ev.timeFromTimeout(timeout),
        .leeway = ev.leeway,
    };
    ev.yield(.{ .futex_wait = &waiter });
    try waiter.cancelable.acknowledge(waiter.sleeper.fiber);
}

fn futexWaitUncancelable(userdata: ?*anyopaque, ptr: *const u32, expected: u32) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const futex = ev.futexForAddress(@intFromPtr(ptr));
    var waiter: Futex.Waiter = .{
        .cancelable = .blocked,
        .futex = futex,
        .ptr = ptr,
        .expected = expected,
        .leeway = ev.leeway,
    };
    ev.yield(.{ .futex_wait = &waiter });
    waiter.cancelable.acknowledge(waiter.sleeper.fiber) catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    };
}

fn futexWake(userdata: ?*anyopaque, ptr: *const u32, max_waiters: u32) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (max_waiters == 0) return;
    const futex = ev.futexForAddress(@intFromPtr(ptr));
    switch (@atomicRmw(usize, &futex.num_waiters, .Add, 0, .release)) {
        0 => return,
        else => {
            @branchHint(.unlikely);
            var waker: Futex.Waker = .{ .futex = futex, .ptr = ptr, .max_waiters = max_waiters };
            ev.yield(.{ .futex_wake = &waker });
        },
    }
}

fn operate(userdata: ?*anyopaque, operation: Io.Operation) Io.Cancelable!Io.Operation.Result {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    switch (operation) {
        .file_read_streaming => |o| return .{
            .file_read_streaming = ev.fileReadStreaming(o.file, o.data) catch |err| switch (err) {
                error.Canceled => |e| return e,
                else => |e| e,
            },
        },
        .file_write_streaming => |o| return .{
            .file_write_streaming = ev.fileWriteStreaming(
                o.file,
                o.header,
                o.data,
                o.splat,
            ) catch |err| switch (err) {
                error.Canceled => |e| return e,
                else => |e| e,
            },
        },
        .device_io_control => |*o| return .{ .device_io_control = try deviceIoControl(o) },
        .net_receive => @panic("TODO implement net_receive operation"),
    }
}

fn fileReadStreaming(ev: *Evented, file: File, data: []const []u8) File.ReadStreamingError!usize {
    if (file.flags.nonblocking) nonblocking: {
        return fileReadStreamingLimit(file.handle, data, .unlimited) catch |err| switch (err) {
            error.WouldBlock => break :nonblocking,
            else => |e| return e,
        };
    }
    const source = c.dispatch.source_create(
        .READ,
        @bitCast(@as(isize, file.handle)),
        .none,
        ev.queue,
    ) orelse return error.SystemResources;
    source.as_object().set_context(Thread.current().currentFiber());
    source.set_event_handler(&Fiber.@"resume");
    ev.yield(.{ .activate = source.as_object() });
    const limit = source.get_data();
    source.as_object().release();
    while (true) return fileReadStreamingLimit(
        file.handle,
        data,
        .limited(limit),
    ) catch |err| switch (err) {
        error.WouldBlock => {
            ev.yield(.nothing);
            continue;
        },
        else => |e| return e,
    };
}
fn fileReadStreamingLimit(
    handle: File.Handle,
    data: []const []u8,
    limit: Io.Limit,
) File.ReadStreamingError!usize {
    var iovecs: [max_iovecs_len]iovec = undefined;
    var iovlen: iovlen_t = 0;
    // .nothing can mean that the write side has been closed,
    // in which case the buffer still needs to be drained
    var remaining = if (limit == .nothing) .unlimited else limit;
    for (data) |buf| addBuf(false, &iovecs, &iovlen, &remaining, buf);
    if (iovlen == 0) return 0;
    while (true) {
        const rc = c.readv(handle, &iovecs, iovlen);
        switch (c.errno(rc)) {
            .SUCCESS => return if (rc == 0) error.EndOfStream else @intCast(rc),
            .INTR => continue,
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

fn fileWriteStreaming(
    ev: *Evented,
    file: File,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
) File.Writer.Error!usize {
    if (file.flags.nonblocking) nonblocking: {
        return fileWriteStreamingLimit(
            file.handle,
            header,
            data,
            splat,
            .unlimited,
        ) catch |err| switch (err) {
            error.WouldBlock => break :nonblocking,
            else => |e| return e,
        };
    }
    const source = c.dispatch.source_create(
        .WRITE,
        @bitCast(@as(isize, file.handle)),
        .none,
        ev.queue,
    ) orelse return error.SystemResources;
    source.as_object().set_context(Thread.current().currentFiber());
    source.set_event_handler(&Fiber.@"resume");
    ev.yield(.{ .activate = source.as_object() });
    const limit = source.get_data();
    source.as_object().release();
    while (true) return fileWriteStreamingLimit(
        file.handle,
        header,
        data,
        splat,
        .limited(limit),
    ) catch |err| switch (err) {
        error.WouldBlock => {
            ev.yield(.nothing);
            continue;
        },
        else => |e| return e,
    };
}
fn fileWriteStreamingLimit(
    handle: File.Handle,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
    limit: Io.Limit,
) File.Writer.Error!usize {
    if (limit == .nothing) return 0;
    var iovecs: [max_iovecs_len]iovec_const = undefined;
    var iovlen: iovlen_t = 0;
    var remaining = limit;
    addBuf(true, &iovecs, &iovlen, &remaining, header);
    for (data[0 .. data.len - 1]) |bytes| addBuf(true, &iovecs, &iovlen, &remaining, bytes);
    const pattern = data[data.len - 1];
    var backup_buffer: [splat_buffer_size]u8 = undefined;
    if (iovecs.len - iovlen != 0 and remaining != .nothing) switch (splat) {
        0 => {},
        1 => addBuf(true, &iovecs, &iovlen, &remaining, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &backup_buffer;
                const memset_len = @min(splat_buffer.len, splat);
                const buf = splat_buffer[0..memset_len];
                @memset(buf, pattern[0]);
                addBuf(true, &iovecs, &iovlen, &remaining, buf);
                var remaining_splat = splat - buf.len;
                while (remaining_splat > splat_buffer.len and iovecs.len - iovlen != 0 and remaining != .nothing) {
                    assert(buf.len == splat_buffer.len);
                    addBuf(true, &iovecs, &iovlen, &remaining, splat_buffer);
                    remaining_splat -= splat_buffer.len;
                }
                addBuf(true, &iovecs, &iovlen, &remaining, splat_buffer[0..@min(remaining_splat, splat_buffer.len)]);
            },
            else => for (0..@min(splat, iovecs.len - iovlen)) |_| {
                if (remaining == .nothing) break;
                addBuf(true, &iovecs, &iovlen, &remaining, pattern);
            },
        },
    };
    if (iovlen == 0) return 0;
    while (true) {
        const rc = c.writev(handle, &iovecs, iovlen);
        switch (c.errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .INTR => continue,
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

fn deviceIoControl(o: *const Io.Operation.DeviceIoControl) Io.Cancelable!i32 {
    while (true) {
        const rc = c.ioctl(o.file.handle, @bitCast(o.code), @intFromPtr(o.arg));
        switch (c.errno(rc)) {
            .SUCCESS => return rc,
            .INTR => {},
            else => |err| return -@as(i32, @intFromEnum(err)),
        }
    }
}

const BatchWaiter = struct {
    sleeper: Sleeper,
    queue: c.dispatch.queue_t,
    timer: ?c.dispatch.source_t = null,

    const already_signaled: c.dispatch.source_t = @ptrFromInt(1);

    fn signal(context: ?*anyopaque) callconv(.c) void {
        const waiter: *BatchWaiter = @ptrCast(@alignCast(context));
        if (waiter.timer) |timer| {
            if (timer != already_signaled) timer.cancel();
        } else {
            waiter.timer = already_signaled;
            waiter.queue.async(waiter, &@"suspend");
        }
    }

    fn @"suspend"(context: ?*anyopaque) callconv(.c) void {
        const waiter: *BatchWaiter = @ptrCast(@alignCast(context));
        if (waiter.timer) |timer| if (timer != already_signaled) timer.as_object().release();
        waiter.queue.as_object().@"suspend"();
        waiter.wake();
    }

    fn wake(waiter: *BatchWaiter) void {
        var sleeper = waiter.sleeper;
        waiter.* = undefined;
        Sleeper.wake(&sleeper);
    }
};

fn batchAwaitAsync(userdata: ?*anyopaque, batch: *Io.Batch) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const queue = ev.batchDrainSubmitted(batch, false) catch |err| switch (err) {
        error.ConcurrencyUnavailable => unreachable, // passed concurrency=false
        error.Canceled => |e| return e,
    } orelse return;
    if (batch.pending.head == .none) return;
    var waiter: BatchWaiter = .{
        .sleeper = .init(ev.queue, Thread.current().currentFiber()),
        .queue = queue,
    };
    if (batch.completed.head != .none) BatchWaiter.signal(&waiter);
    queue.as_object().set_context(&waiter);
    ev.yield(.{ .@"resume" = queue.as_object() });
}

fn batchAwaitConcurrent(
    userdata: ?*anyopaque,
    batch: *Io.Batch,
    timeout: Io.Timeout,
) Io.Batch.AwaitConcurrentError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const queue = try ev.batchDrainSubmitted(batch, true) orelse return;
    if (batch.pending.head == .none) return;
    var waiter: BatchWaiter = .{
        .sleeper = .init(ev.queue, Thread.current().currentFiber()),
        .queue = queue,
    };
    if (batch.completed.head == .none) switch (timeout) {
        .none => {},
        else => {
            const timer = c.dispatch.source_create(.TIMER, 0, .none, queue) orelse
                return error.ConcurrencyUnavailable;
            assert(timer != BatchWaiter.already_signaled);
            timer.as_object().set_context(&waiter);
            timer.set_event_handler(&BatchWaiter.signal);
            timer.set_cancel_handler(&BatchWaiter.@"suspend");
            timer.set_timer(ev.timeFromTimeout(timeout), c.dispatch.TIME_FOREVER, ev.leeway);
            timer.as_object().activate();
            waiter.timer = timer;
        },
    } else BatchWaiter.signal(&waiter);
    queue.as_object().set_context(&waiter);
    ev.yield(.{ .@"resume" = queue.as_object() });
}

fn batchCancel(userdata: ?*anyopaque, batch: *Io.Batch) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var index = batch.pending.head;
    while (index != .none) {
        const storage = &batch.storage[index.toIndex()];
        const pending = &storage.pending;
        const operation_userdata: *BatchOperationUserdata = .fromErased(&pending.userdata);
        assert(operation_userdata.batch == batch);
        operation_userdata.source.cancel();
    }
    const queue: c.dispatch.queue_t = @ptrCast(batch.userdata orelse return);
    if (batch.pending.head != .none) {
        var waiter: BatchWaiter = .{
            .sleeper = .init(ev.queue, Thread.current().currentFiber()),
            .queue = queue,
            .timer = BatchWaiter.already_signaled,
        };
        if (batch.pending.head == .none) queue.async(&waiter, &BatchWaiter.signal);
        queue.as_object().set_context(&waiter);
        ev.yield(.{ .@"resume" = queue.as_object() });
    }
    batch.userdata = null;
}

const BatchOperationUserdata = extern struct {
    batch: *Io.Batch,
    source: c.dispatch.source_t,
    operation: extern union {
        file_read_streaming: extern struct {
            data_ptr: [*]const []u8,
            data_len: usize,
        },
        file_write_streaming: extern struct {
            header_ptr: [*]const u8,
            header_len: usize,
            data_ptr: [*]const []const u8,
            data_len: usize,
            splat: usize,

            fn header(operation: *const @This()) []const u8 {
                return operation.header_ptr[0..operation.header_len];
            }

            fn data(operation: *const @This()) []const []const u8 {
                return operation.data_ptr[0..operation.data_len];
            }
        },
    },

    const Erased = Io.Operation.Storage.Pending.Userdata;

    comptime {
        assert(@sizeOf(BatchOperationUserdata) <= @sizeOf(Erased));
    }

    fn toErased(userdata: *BatchOperationUserdata) *Erased {
        return @ptrCast(userdata);
    }

    fn fromErased(erased: *Erased) *BatchOperationUserdata {
        return @ptrCast(erased);
    }
};

/// If `concurrency` is false, `error.ConcurrencyUnavailable` is unreachable.
fn batchDrainSubmitted(
    ev: *Evented,
    batch: *Io.Batch,
    concurrency: bool,
) (Io.ConcurrentError || Io.Cancelable)!?c.dispatch.queue_t {
    var index = batch.submitted.head;
    if (index == .none) return @ptrCast(batch.userdata);
    errdefer batch.submitted.head = index;
    const maybe_queue: ?c.dispatch.queue_t = if (batch.userdata) |batch_userdata|
        @ptrCast(batch_userdata)
    else maybe_queue: {
        const queue = c.dispatch.queue_create_with_target(
            "org.ziglang.std.Io.Dispatch.Batch",
            .SERIAL(),
            ev.queue,
        ) orelse if (concurrency) return error.ConcurrencyUnavailable else break :maybe_queue null;
        queue.as_object().@"suspend"();
        batch.userdata = queue;
        break :maybe_queue queue;
    };
    while (index != .none) {
        const storage = &batch.storage[index.toIndex()];
        const next_index = storage.submission.node.next;
        if (@as(?Io.Operation.Result, result: {
            if (maybe_queue) |queue| switch (storage.submission.operation) {
                .file_read_streaming => |operation| {
                    const data = for (operation.data, 0..) |buffer, data_index| {
                        if (buffer.len > 0) break operation.data[data_index..];
                    } else break :result .{ .file_read_streaming = 0 };
                    const source = c.dispatch.source_create(
                        .READ,
                        @bitCast(@as(isize, operation.file.handle)),
                        .none,
                        queue,
                    ) orelse break :result .{ .file_read_streaming = error.SystemResources };
                    storage.* = .{ .pending = .{
                        .node = .{ .prev = batch.pending.tail, .next = .none },
                        .tag = .file_read_streaming,
                        .userdata = undefined,
                    } };
                    const operation_userdata: *BatchOperationUserdata =
                        .fromErased(&storage.pending.userdata);
                    operation_userdata.* = .{
                        .batch = batch,
                        .source = source,
                        .operation = .{ .file_read_streaming = .{
                            .data_ptr = data.ptr,
                            .data_len = data.len,
                        } },
                    };
                    source.as_object().set_context(storage);
                    source.set_event_handler(&batchSourceEvent);
                    source.set_cancel_handler(&batchSourceCancel);
                    source.as_object().activate();
                    break :result null;
                },
                .file_write_streaming => |operation| {
                    const data = for (operation.data, 0..) |buffer, data_index| {
                        if (buffer.len > 0) break operation.data[data_index..];
                    } else if (operation.header.len > 0)
                        operation.data[0..1]
                    else
                        break :result .{ .file_write_streaming = 0 };
                    const source = c.dispatch.source_create(
                        .WRITE,
                        @bitCast(@as(isize, operation.file.handle)),
                        .none,
                        queue,
                    ) orelse break :result .{ .file_write_streaming = error.SystemResources };
                    storage.* = .{ .pending = .{
                        .node = .{ .prev = batch.pending.tail, .next = .none },
                        .tag = .file_write_streaming,
                        .userdata = undefined,
                    } };
                    const operation_userdata: *BatchOperationUserdata =
                        .fromErased(&storage.pending.userdata);
                    operation_userdata.* = .{
                        .batch = batch,
                        .source = source,
                        .operation = .{ .file_write_streaming = .{
                            .header_ptr = operation.header.ptr,
                            .header_len = operation.header.len,
                            .data_ptr = data.ptr,
                            .data_len = data.len,
                            .splat = operation.splat,
                        } },
                    };
                    source.as_object().set_context(storage);
                    source.set_event_handler(&batchSourceEvent);
                    source.set_cancel_handler(&batchSourceCancel);
                    source.as_object().activate();
                    break :result null;
                },
                .device_io_control => {},
                .net_receive => @panic("TODO implement batched net_receive"),
            };
            if (concurrency) return error.ConcurrencyUnavailable;
            break :result try operate(ev, storage.submission.operation);
        })) |result| {
            switch (batch.completed.tail) {
                .none => batch.completed.head = index,
                else => |tail_index| batch.storage[tail_index.toIndex()].completion.node.next = index,
            }
            batch.completed.tail = index;
            storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
        } else {
            switch (batch.pending.tail) {
                .none => batch.pending.head = index,
                else => |tail_index| batch.storage[tail_index.toIndex()].pending.node.next = index,
            }
            batch.pending.tail = index;
        }
        index = next_index;
    }
    batch.submitted = .{ .head = .none, .tail = .none };
    return maybe_queue;
}

fn batchSourceEvent(context: ?*anyopaque) callconv(.c) void {
    const storage: *Io.Operation.Storage = @ptrCast(@alignCast(context));
    const pending = &storage.pending;
    const operation_userdata: *BatchOperationUserdata = .fromErased(&pending.userdata);
    const batch = operation_userdata.batch;
    const source = operation_userdata.source;
    const index: Io.Operation.OptionalIndex = .fromIndex(storage - batch.storage.ptr);
    const result: Io.Operation.Result = result: switch (pending.tag) {
        .file_read_streaming => {
            const operation = &operation_userdata.operation.file_read_streaming;
            break :result .{ .file_read_streaming = fileReadStreamingLimit(
                @intCast(source.get_handle()),
                operation.data_ptr[0..operation.data_len],
                .limited(source.get_data()),
            ) catch |err| switch (err) {
                error.Canceled => return Thread.current().currentFiber().cancel_protection.recancel(),
                error.WouldBlock => return,
                else => |e| e,
            } };
        },
        .file_write_streaming => {
            const operation = &operation_userdata.operation.file_write_streaming;
            break :result .{ .file_write_streaming = fileWriteStreamingLimit(
                @intCast(source.get_handle()),
                operation.header_ptr[0..operation.header_len],
                operation.data_ptr[0..operation.data_len],
                operation.splat,
                .limited(source.get_data()),
            ) catch |err| switch (err) {
                error.Canceled => return Thread.current().currentFiber().cancel_protection.recancel(),
                error.WouldBlock => return,
                else => |e| e,
            } };
        },
        .device_io_control => unreachable,
        .net_receive => @panic("TODO implement batched net_receive"),
    };

    switch (pending.node.prev) {
        .none => batch.pending.head = pending.node.next,
        else => |prev_index| batch.storage[prev_index.toIndex()].pending.node.next = pending.node.next,
    }
    switch (pending.node.next) {
        .none => batch.pending.tail = pending.node.prev,
        else => |next_index| batch.storage[next_index.toIndex()].pending.node.prev = pending.node.prev,
    }

    switch (batch.completed.tail) {
        .none => batch.completed.head = index,
        else => |tail_index| batch.storage[tail_index.toIndex()].completion.node.next = index,
    }
    storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
    batch.completed.tail = index;

    source.as_object().release();
    const queue: c.dispatch.queue_t = @ptrCast(batch.userdata);
    const waiter: *BatchWaiter = @ptrCast(@alignCast(queue.as_object().get_context()));
    BatchWaiter.signal(waiter);
}

fn batchSourceCancel(context: ?*anyopaque) callconv(.c) void {
    const storage: *Io.Operation.Storage = @ptrCast(@alignCast(context));
    const pending = &storage.pending;
    const operation_userdata: *BatchOperationUserdata = .fromErased(&pending.userdata);
    const batch = operation_userdata.batch;
    const source = operation_userdata.source;
    const index: Io.Operation.OptionalIndex = .fromIndex(storage - batch.storage.ptr);

    switch (pending.node.prev) {
        .none => batch.pending.head = pending.node.next,
        else => |prev_index| batch.storage[prev_index.toIndex()].pending.node.next = pending.node.next,
    }
    switch (pending.node.next) {
        .none => batch.pending.tail = pending.node.prev,
        else => |next_index| batch.storage[next_index.toIndex()].pending.node.prev = pending.node.prev,
    }

    const tail_index = batch.unused.tail;
    switch (tail_index) {
        .none => batch.unused.head = index,
        else => batch.storage[tail_index.toIndex()].unused.next = index,
    }
    storage.* = .{ .unused = .{ .prev = tail_index, .next = .none } };
    batch.unused.tail = index;

    source.as_object().release();
    if (batch.pending.head != .none) return;
    const queue: c.dispatch.queue_t = @ptrCast(batch.userdata);
    const waiter: *BatchWaiter = @ptrCast(@alignCast(queue.as_object().get_context()));
    queue.as_object().release();
    waiter.wake();
}

fn dirCreateDir(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
) Dir.CreateDirError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    while (true) {
        switch (c.errno(c.mkdirat(dir.handle, sub_path_posix, permissions.toMode()))) {
            .SUCCESS => return,
            .INTR => {},
            .ACCES => return error.AccessDenied,
            .PERM => return error.PermissionDenied,
            .DQUOT => return error.DiskQuota,
            .EXIST => return error.PathAlreadyExists,
            .LOOP => return error.SymLinkLoop,
            .MLINK => return error.LinkQuotaExceeded,
            .NAMETOOLONG => return error.NameTooLong,
            .NOENT => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOSPC => return error.NoSpaceLeft,
            .NOTDIR => return error.NotDir,
            .ROFS => return error.ReadOnlyFileSystem,
            .ILSEQ => return error.BadPathName,
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .FAULT => |err| return errnoBug(err),
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn dirCreateDirPath(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
) Dir.CreateDirPathError!Dir.CreatePathStatus {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var it = Dir.path.componentIterator(sub_path);
    var status: Dir.CreatePathStatus = .existed;
    var component = it.last() orelse return error.BadPathName;
    while (true) {
        if (dirCreateDir(ev, dir, component.path, permissions)) |_| {
            status = .created;
        } else |err| switch (err) {
            error.PathAlreadyExists => {
                // It is important to return an error if it's not a directory
                // because otherwise a dangling symlink could cause an infinite
                // loop.
                const fstat = try dirStatFile(ev, dir, component.path, .{});
                if (fstat.kind != .directory) return error.NotDir;
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

fn dirCreateDirPathOpen(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.OpenOptions,
) Dir.CreateDirPathOpenError!Dir {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return dirOpenDir(ev, dir, sub_path, options) catch |err| switch (err) {
        error.FileNotFound => {
            _ = try dirCreateDirPath(ev, dir, sub_path, permissions);
            return dirOpenDir(ev, dir, sub_path, options);
        },
        else => |e| return e,
    };
}

fn dirOpenDir(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.OpenOptions,
) Dir.OpenError!Dir {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const flags: c.O = .{
        .ACCMODE = .RDONLY,
        .NOFOLLOW = !options.follow_symlinks,
        .DIRECTORY = true,
        .CLOEXEC = true,
    };

    while (true) {
        const rc = c.openat(dir.handle, sub_path_posix, flags);
        switch (c.errno(rc)) {
            .SUCCESS => return .{ .handle = @intCast(rc) },
            .INTR => {},
            .INVAL => return error.BadPathName,
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
            .NXIO => return error.NoDevice,
            .ILSEQ => return error.BadPathName,
            .FAULT => |err| return errnoBug(err),
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .BUSY => |err| return errnoBug(err), // O_EXCL not passed
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn dirStat(userdata: ?*anyopaque, dir: Dir) Dir.StatError!Dir.Stat {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return fileStat(ev, .{
        .handle = dir.handle,
        .flags = .{ .nonblocking = false },
    });
}

fn dirStatFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.StatFileOptions,
) Dir.StatFileError!File.Stat {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const flags: u32 = if (options.follow_symlinks) 0 else c.AT.SYMLINK_NOFOLLOW;

    while (true) {
        var stat = std.mem.zeroes(c.Stat);
        switch (c.errno(c.fstatat(dir.handle, sub_path_posix, &stat, flags))) {
            .SUCCESS => return statFromPosix(&stat),
            .INTR => {},
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
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn dirAccess(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.AccessOptions,
) Dir.AccessError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const flags: u32 = if (options.follow_symlinks) 0 else c.AT.SYMLINK_NOFOLLOW;

    const mode: u32 =
        @as(u32, if (options.read) c.R_OK else 0) |
        @as(u32, if (options.write) c.W_OK else 0) |
        @as(u32, if (options.execute) c.X_OK else 0);

    while (true) switch (c.errno(c.faccessat(dir.handle, sub_path_posix, mode, flags))) {
        .SUCCESS => return,
        .INTR => {},
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
        else => |err| return unexpectedErrno(err),
    };
}

fn dirCreateFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: File.CreateFlags,
) File.OpenError!File {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const os_flags: c.O = .{
        .ACCMODE = if (flags.read) .RDWR else .WRONLY,
        .NONBLOCK = flags.lock == .none or flags.lock_nonblocking,
        .SHLOCK = flags.lock == .shared,
        .EXLOCK = flags.lock == .exclusive,
        .CREAT = true,
        .TRUNC = flags.truncate,
        .EXCL = flags.exclusive,
        .CLOEXEC = true,
    };

    const fd: c.fd_t = while (true) {
        const rc = c.openat(dir.handle, sub_path_posix, os_flags, flags.permissions.toMode());
        switch (c.errno(rc)) {
            .SUCCESS => break @intCast(rc),
            .INTR => {},
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
            .OPNOTSUPP => return error.FileLocksUnsupported,
            .AGAIN => return error.WouldBlock,
            .TXTBSY => return error.FileBusy,
            .ROFS => return error.ReadOnlyFileSystem,
            .NXIO => return error.NoDevice,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    };
    errdefer closeFd(fd);

    return .{
        .handle = fd,
        .flags = .{ .nonblocking = os_flags.NONBLOCK },
    };
}

fn dirCreateFileAtomic(
    userdata: ?*anyopaque,
    dir: Dir,
    dest_path: []const u8,
    options: Dir.CreateFileAtomicOptions,
) Dir.CreateFileAtomicError!File.Atomic {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    if (Dir.path.dirname(dest_path)) |dirname| {
        const new_dir = if (options.make_path)
            dirCreateDirPathOpen(ev, dir, dirname, .default_dir, .{}) catch |err| switch (err) {
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
            try dirOpenDir(ev, dir, dirname, .{});
        return ev.atomicFileInit(Dir.path.basename(dest_path), options.permissions, new_dir, true);
    }
    return ev.atomicFileInit(dest_path, options.permissions, dir, false);
}

fn atomicFileInit(
    ev: *Evented,
    dest_basename: []const u8,
    permissions: File.Permissions,
    dir: Dir,
    close_dir_on_deinit: bool,
) Dir.CreateFileAtomicError!File.Atomic {
    while (true) {
        var random_integer: u64 = undefined;
        random(ev, @ptrCast(&random_integer));
        const tmp_sub_path = std.fmt.hex(random_integer);
        const file = dirCreateFile(ev, dir, &tmp_sub_path, .{
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

fn dirOpenFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: File.OpenFlags,
) File.OpenError!File {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const os_flags: c.O = .{
        .ACCMODE = switch (flags.mode) {
            .read_only => .RDONLY,
            .write_only => .WRONLY,
            .read_write => .RDWR,
        },
        .NONBLOCK = flags.lock == .none or flags.lock_nonblocking,
        .SHLOCK = flags.lock == .shared,
        .EXLOCK = flags.lock == .exclusive,
        .NOFOLLOW = !flags.follow_symlinks,
        .NOCTTY = !flags.allow_ctty,
        .CLOEXEC = true,
    };

    const fd: c.fd_t = while (true) {
        const rc = c.openat(dir.handle, sub_path_posix, os_flags);
        switch (c.errno(rc)) {
            .SUCCESS => break @intCast(rc),
            .INTR => {},
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
            .OPNOTSUPP => return error.FileLocksUnsupported,
            .AGAIN => return error.WouldBlock,
            .TXTBSY => return error.FileBusy,
            .NXIO => return error.NoDevice,
            .ROFS => return error.ReadOnlyFileSystem,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    };
    errdefer closeFd(fd);

    if (!flags.allow_directory) {
        const is_dir = is_dir: {
            const stat = fileStat(ev, .{
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

    return .{
        .handle = fd,
        .flags = .{ .nonblocking = os_flags.NONBLOCK },
    };
}

fn dirClose(userdata: ?*anyopaque, dirs: []const Dir) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    for (dirs) |dir| closeFd(dir.handle);
}

fn dirRead(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
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
                ev.lseek(dr.dir.handle, 0, c.SEEK.SET) catch |err| switch (err) {
                    error.Unseekable => return error.Unexpected,
                    else => |e| return e,
                };
                dr.state = .reading;
            }
            const dents_buffer = dr.buffer[header_end..];
            const n: usize = while (true) {
                const rc = c.getdirentries(dr.dir.handle, dents_buffer.ptr, dents_buffer.len, &header.seek);
                switch (c.errno(rc)) {
                    .SUCCESS => break @intCast(rc),
                    .INTR => {},
                    .BADF => |err| return errnoBug(err), // Dir is invalid or was opened without iteration ability.
                    .FAULT => |err| return errnoBug(err),
                    .NOTDIR => |err| return errnoBug(err),
                    .INVAL => |err| return errnoBug(err),
                    else => |err| return unexpectedErrno(err),
                }
            };
            if (n == 0) {
                dr.state = .finished;
                return 0;
            }
            dr.index = header_end;
            dr.end = header_end + n;
        }
        const darwin_entry = @as(*align(1) c.dirent, @ptrCast(&dr.buffer[dr.index]));
        const next_index = dr.index + darwin_entry.reclen;
        dr.index = next_index;

        const name = @as([*]u8, @ptrCast(&darwin_entry.name))[0..darwin_entry.namlen];
        if (std.mem.eql(u8, name, ".") or std.mem.eql(u8, name, "..") or (darwin_entry.ino == 0))
            continue;

        const entry_kind: File.Kind = switch (darwin_entry.type) {
            c.DT.BLK => .block_device,
            c.DT.CHR => .character_device,
            c.DT.DIR => .directory,
            c.DT.FIFO => .named_pipe,
            c.DT.LNK => .sym_link,
            c.DT.REG => .file,
            c.DT.SOCK => .unix_domain_socket,
            c.DT.WHT => .whiteout,
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

fn dirRealPath(userdata: ?*anyopaque, dir: Dir, out_buffer: []u8) Dir.RealPathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return ev.realPath(dir.handle, out_buffer);
}

fn realPath(ev: *Evented, fd: c.fd_t, out_buffer: []u8) File.RealPathError!usize {
    _ = ev;
    var buffer: [c.PATH_MAX]u8 = undefined;
    @memset(&buffer, 0);
    while (true) {
        switch (c.errno(c.fcntl(fd, c.F.GETPATH, &buffer))) {
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

fn dirRealPathFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    out_buffer: []u8,
) Dir.RealPathFileError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    if (dir.handle == c.AT.FDCWD) {
        if (out_buffer.len < c.PATH_MAX) return error.NameTooLong;
        while (true) {
            if (c.realpath(sub_path_posix, out_buffer.ptr)) |redundant_pointer| {
                assert(redundant_pointer == out_buffer.ptr);
                return std.mem.indexOfScalar(u8, out_buffer, 0) orelse out_buffer.len;
            }
            const err: c.E = @enumFromInt(c._errno().*);
            switch (err) {
                .INTR => {},
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
                else => return unexpectedErrno(err),
            }
        }
    }

    const os_flags: c.O = .{
        .NONBLOCK = true,
        .CLOEXEC = true,
    };

    const fd: c.fd_t = while (true) {
        const rc = c.openat(dir.handle, sub_path_posix, os_flags);
        switch (c.errno(rc)) {
            .SUCCESS => break @intCast(rc),
            .INTR => {},
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
            .NXIO => return error.NoDevice,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    };
    defer closeFd(fd);
    return ev.realPath(fd, out_buffer);
}

fn dirDeleteFile(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteFileError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    while (true) switch (c.errno(c.unlinkat(dir.handle, sub_path_posix, 0))) {
        .SUCCESS => return,
        .INTR => {},
        // Some systems return permission errors when trying to delete a
        // directory, so we need to handle that case specifically and
        // translate the error.
        .PERM => {
            // Don't follow symlinks to match unlinkat (which acts on symlinks rather than follows them).
            var st = std.mem.zeroes(c.Stat);
            while (true) switch (c.errno(c.fstatat(
                dir.handle,
                sub_path_posix,
                &st,
                c.AT.SYMLINK_NOFOLLOW,
            ))) {
                .SUCCESS => break,
                .INTR => {},
                else => return error.PermissionDenied,
            };
            if (st.mode & c.S.IFMT == c.S.IFDIR) return error.IsDir else return error.PermissionDenied;
        },
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
        else => |err| return unexpectedErrno(err),
    };
}

fn dirDeleteDir(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteDirError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    while (true) switch (c.errno(c.unlinkat(dir.handle, sub_path_posix, c.AT.REMOVEDIR))) {
        .SUCCESS => return,
        .INTR => {},
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
        else => |err| return unexpectedErrno(err),
    };
}

fn dirRename(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenameError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var old_path_buffer: [c.PATH_MAX]u8 = undefined;
    var new_path_buffer: [c.PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    while (true) switch (c.errno(c.renameat(old_dir.handle, old_sub_path_posix, new_dir.handle, new_sub_path_posix))) {
        .SUCCESS => return,
        .INTR => {},
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
    };
}

fn dirRenamePreserve(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenamePreserveError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    // Make a hard link then delete the original.
    try dirHardLink(ev, old_dir, old_sub_path, new_dir, new_sub_path, .{ .follow_symlinks = false });
    const prev = swapCancelProtection(ev, .blocked);
    defer _ = swapCancelProtection(ev, prev);
    dirDeleteFile(ev, old_dir, old_sub_path) catch {};
}

fn dirSymLink(
    userdata: ?*anyopaque,
    dir: Dir,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: Dir.SymLinkFlags,
) Dir.SymLinkError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    _ = flags;

    var target_path_buffer: [c.PATH_MAX]u8 = undefined;
    var sym_link_path_buffer: [c.PATH_MAX]u8 = undefined;

    const target_path_posix = try pathToPosix(target_path, &target_path_buffer);
    const sym_link_path_posix = try pathToPosix(sym_link_path, &sym_link_path_buffer);

    while (true) switch (c.errno(c.symlinkat(target_path_posix, dir.handle, sym_link_path_posix))) {
        .SUCCESS => return,
        .INTR => {},
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
        else => |err| return unexpectedErrno(err),
    };
}

fn dirReadLink(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    buffer: []u8,
) Dir.ReadLinkError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    var sub_path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &sub_path_buffer);
    while (true) {
        const rc = c.readlinkat(dir.handle, sub_path_posix, buffer.ptr, buffer.len);
        switch (c.errno(rc)) {
            .SUCCESS => return @intCast(rc),
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
    _ = ev;
    return fchown(dir.handle, owner, group);
}

fn fchown(fd: c.fd_t, owner: ?File.Uid, group: ?File.Gid) File.SetOwnerError!void {
    const uid = owner orelse std.math.maxInt(c.uid_t);
    const gid = group orelse std.math.maxInt(c.gid_t);
    while (true) switch (c.errno(c.fchown(fd, uid, gid))) {
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
    };
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
    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);
    _ = ev;
    while (true) switch (c.errno(c.fchownat(
        dir.handle,
        sub_path_posix,
        owner orelse std.math.maxInt(c.uid_t),
        group orelse std.math.maxInt(c.gid_t),
        if (options.follow_symlinks) 0 else c.AT.SYMLINK_NOFOLLOW,
    ))) {
        .SUCCESS => return,
        .INTR => continue,
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
    };
}

fn dirSetPermissions(
    userdata: ?*anyopaque,
    dir: Dir,
    permissions: Dir.Permissions,
) Dir.SetPermissionsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return ev.fchmod(dir.handle, permissions.toMode());
}

fn dirSetFilePermissions(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
    options: Dir.SetFilePermissionsOptions,
) Dir.SetFilePermissionsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const mode = permissions.toMode();
    const flags: u32 = if (options.follow_symlinks) 0 else c.AT.SYMLINK_NOFOLLOW;

    while (true) switch (c.errno(c.fchmodat(dir.handle, sub_path_posix, mode, flags))) {
        .SUCCESS => return,
        .INTR => {},
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
        else => |err| return unexpectedErrno(err),
    };
}

fn dirSetTimestamps(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.SetTimestampsOptions,
) Dir.SetTimestampsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var times_buffer: [2]c.timespec = undefined;
    const times = if (options.modify_timestamp == .now and options.access_timestamp == .now) null else p: {
        times_buffer = .{
            setTimestampToPosix(options.access_timestamp),
            setTimestampToPosix(options.modify_timestamp),
        };
        break :p &times_buffer;
    };

    const flags: u32 = if (options.follow_symlinks) 0 else c.AT.SYMLINK_NOFOLLOW;

    var path_buffer: [c.PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    while (true) switch (c.errno(c.utimensat(dir.handle, sub_path_posix, times, flags))) {
        .SUCCESS => return,
        .INTR => {},
        .BADF => |err| return errnoBug(err), // always a race condition
        .FAULT => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err),
        .ACCES => return error.AccessDenied,
        .PERM => return error.PermissionDenied,
        .ROFS => return error.ReadOnlyFileSystem,
        else => |err| return unexpectedErrno(err),
    };
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
    _ = ev;

    var old_path_buffer: [c.PATH_MAX]u8 = undefined;
    var new_path_buffer: [c.PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    const flags: u32 = if (options.follow_symlinks) c.AT.SYMLINK_FOLLOW else 0;
    return linkat(old_dir.handle, old_sub_path_posix, new_dir.handle, new_sub_path_posix, flags);
}

fn fileStat(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    while (true) {
        var stat = std.mem.zeroes(c.Stat);
        switch (c.errno(c.fstat(file.handle, &stat))) {
            .SUCCESS => return statFromPosix(&stat),
            .INTR => {},
            .INVAL => |err| return errnoBug(err),
            .BADF => |err| return errnoBug(err), // File descriptor used after closed.
            .NOMEM => return error.SystemResources,
            .ACCES => return error.AccessDenied,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fileLength(userdata: ?*anyopaque, file: File) File.LengthError!u64 {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const stat = try fileStat(ev, file);
    return stat.size;
}

fn fileClose(userdata: ?*anyopaque, files: []const File) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    for (files) |file| closeFd(file.handle);
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
    _ = ev;
    var iovecs: [max_iovecs_len]iovec_const = undefined;
    var iovlen: iovlen_t = 0;
    var remaining: Io.Limit = .unlimited;
    addBuf(true, &iovecs, &iovlen, &remaining, header);
    for (data[0 .. data.len - 1]) |bytes| addBuf(true, &iovecs, &iovlen, &remaining, bytes);
    const pattern = data[data.len - 1];
    var backup_buffer: [splat_buffer_size]u8 = undefined;
    if (iovecs.len - iovlen != 0 and remaining != .nothing) switch (splat) {
        0 => {},
        1 => addBuf(true, &iovecs, &iovlen, &remaining, pattern),
        else => switch (pattern.len) {
            0 => {},
            1 => {
                const splat_buffer = &backup_buffer;
                const memset_len = @min(splat_buffer.len, splat);
                const buf = splat_buffer[0..memset_len];
                @memset(buf, pattern[0]);
                addBuf(true, &iovecs, &iovlen, &remaining, buf);
                var remaining_splat = splat - buf.len;
                while (remaining_splat > splat_buffer.len and iovecs.len - iovlen != 0 and remaining != .nothing) {
                    assert(buf.len == splat_buffer.len);
                    addBuf(true, &iovecs, &iovlen, &remaining, splat_buffer);
                    remaining_splat -= splat_buffer.len;
                }
                addBuf(true, &iovecs, &iovlen, &remaining, splat_buffer[0..@min(remaining_splat, splat_buffer.len)]);
            },
            else => for (0..@min(splat, iovecs.len - iovlen)) |_| {
                if (remaining == .nothing) break;
                addBuf(true, &iovecs, &iovlen, &remaining, pattern);
            },
        },
    };
    if (iovlen == 0) return 0;
    while (true) {
        const rc = c.pwritev(file.handle, &iovecs, iovlen, @bitCast(offset));
        switch (c.errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .INTR => {},
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            .DESTADDRREQ => |err| return errnoBug(err), // `connect` was never called.
            .CONNRESET => |err| return errnoBug(err), // Not a socket handle.
            .BADF => return error.NotOpenForWriting,
            .AGAIN => return error.WouldBlock,
            .DQUOT => return error.DiskQuota,
            .FBIG => return error.FileTooBig,
            .IO => return error.InputOutput,
            .NOSPC => return error.NoSpaceLeft,
            .PERM => return error.PermissionDenied,
            .PIPE => return error.BrokenPipe,
            .BUSY => return error.DeviceBusy,
            .TXTBSY => return error.FileBusy,
            .NXIO => return error.Unseekable,
            .SPIPE => return error.Unseekable,
            .OVERFLOW => return error.Unseekable,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fileWriteFileStreaming(
    userdata: ?*anyopaque,
    file: File,
    header: []const u8,
    file_reader: *File.Reader,
    limit: Io.Limit,
) File.Writer.WriteFileError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const reader_buffered = file_reader.interface.buffered();
    if (reader_buffered.len >= @intFromEnum(limit)) {
        const n = try fileWriteStreaming(ev, file, header, &.{limit.slice(reader_buffered)}, 1);
        file_reader.interface.toss(n -| header.len);
        return n;
    }
    const file_limit = @intFromEnum(limit) - reader_buffered.len;
    const out_fd = file.handle;
    const in_fd = file_reader.file.handle;

    if (file_reader.size) |size| {
        if (size - file_reader.pos == 0) {
            if (reader_buffered.len != 0) {
                const n = try fileWriteStreaming(ev, file, header, &.{limit.slice(reader_buffered)}, 1);
                file_reader.interface.toss(n -| header.len);
                return n;
            } else {
                return error.EndOfStream;
            }
        }
    }

    if (@atomicLoad(UseSendfile, &ev.use_sendfile, .monotonic) == .disabled) return error.Unimplemented;
    const offset = std.math.cast(c.off_t, file_reader.pos) orelse return error.Unimplemented;
    var hdtr_data: c.sf_hdtr = undefined;
    var headers: [2]iovec_const = undefined;
    var headers_i: u8 = 0;
    if (header.len != 0) {
        headers[headers_i] = .{ .base = header.ptr, .len = header.len };
        headers_i += 1;
    }
    if (reader_buffered.len != 0) {
        headers[headers_i] = .{ .base = reader_buffered.ptr, .len = reader_buffered.len };
        headers_i += 1;
    }
    const hdtr: ?*c.sf_hdtr = if (headers_i == 0) null else b: {
        hdtr_data = .{
            .headers = &headers,
            .hdr_cnt = headers_i,
            .trailers = null,
            .trl_cnt = 0,
        };
        break :b &hdtr_data;
    };
    const max_count = std.math.maxInt(i32); // Avoid EINVAL.
    var len: c.off_t = @min(file_limit, max_count);
    const flags = 0;
    while (true) switch (c.errno(c.sendfile(in_fd, out_fd, offset, &len, hdtr, flags))) {
        .SUCCESS => break,
        .OPNOTSUPP, .NOTSOCK, .NOSYS => {
            // Give calling code chance to observe before trying
            // something else.
            @atomicStore(UseSendfile, &ev.use_sendfile, .disabled, .monotonic);
            return 0;
        },
        .INTR => if (len > 0) break,
        .AGAIN => {
            if (len == 0) return error.WouldBlock;
            break;
        },
        else => |e| {
            assert(error.Unexpected == switch (e) {
                .NOTCONN => return error.BrokenPipe,
                .IO => return error.InputOutput,
                .PIPE => return error.BrokenPipe,
                .BADF => |err| errnoBug(err),
                .FAULT => |err| errnoBug(err),
                .INVAL => |err| errnoBug(err),
                else => |err| unexpectedErrno(err),
            });
            // Give calling code chance to observe the error before trying
            // something else.
            @atomicStore(UseSendfile, &ev.use_sendfile, .disabled, .monotonic);
            return 0;
        },
    };
    if (len == 0) {
        file_reader.size = file_reader.pos;
        return error.EndOfStream;
    }
    const u_len: usize = @bitCast(len);
    file_reader.interface.toss(u_len -| header.len);
    return u_len;
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
    const reader_buffered = file_reader.interface.buffered();
    if (reader_buffered.len >= @intFromEnum(limit)) {
        const n = try fileWritePositional(
            ev,
            file,
            header,
            &.{limit.slice(reader_buffered)},
            1,
            offset,
        );
        file_reader.interface.toss(n -| header.len);
        return n;
    }
    const out_fd = file.handle;
    const in_fd = file_reader.file.handle;

    if (file_reader.size) |size| {
        if (size - file_reader.pos == 0) {
            if (reader_buffered.len != 0) {
                const n = try fileWritePositional(
                    ev,
                    file,
                    header,
                    &.{limit.slice(reader_buffered)},
                    1,
                    offset,
                );
                file_reader.interface.toss(n -| header.len);
                return n;
            } else {
                return error.EndOfStream;
            }
        }
    }

    if (@atomicLoad(UseFcopyfile, &ev.use_fcopyfile, .monotonic) == .disabled)
        return error.Unimplemented;
    if (file_reader.pos != 0) return error.Unimplemented;
    if (offset != 0) return error.Unimplemented;
    if (limit != .unlimited) return error.Unimplemented;
    const size = file_reader.getSize() catch return error.Unimplemented;
    if (header.len != 0 or reader_buffered.len != 0) {
        const n = try fileWritePositional(
            ev,
            file,
            header,
            &.{limit.slice(reader_buffered)},
            1,
            offset,
        );
        file_reader.interface.toss(n -| header.len);
        return n;
    }
    while (true) {
        const rc = c.fcopyfile(in_fd, out_fd, null, .{ .DATA = true });
        switch (c.errno(rc)) {
            .SUCCESS => break,
            .INTR => {},
            .OPNOTSUPP => {
                // Give calling code chance to observe before trying
                // something else.
                @atomicStore(UseFcopyfile, &ev.use_fcopyfile, .disabled, .monotonic);
                return 0;
            },
            else => |e| {
                assert(error.Unexpected == switch (e) {
                    .NOMEM => return error.SystemResources,
                    .INVAL => |err| errnoBug(err),
                    else => |err| unexpectedErrno(err),
                });
                return 0;
            },
        }
    }
    file_reader.pos = size;
    return size;
}

fn fileReadPositional(
    userdata: ?*anyopaque,
    file: File,
    data: []const []u8,
    offset: u64,
) File.ReadPositionalError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    var iovecs: [max_iovecs_len]iovec = undefined;
    var iovlen: iovlen_t = 0;
    var remaining: Io.Limit = .unlimited;
    for (data) |buf| addBuf(false, &iovecs, &iovlen, &remaining, buf);
    if (iovlen == 0) return 0;
    while (true) {
        const rc = c.preadv(file.handle, &iovecs, iovlen, @bitCast(offset));
        switch (c.errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .INTR => {},
            .NXIO => return error.Unseekable,
            .SPIPE => return error.Unseekable,
            .OVERFLOW => return error.Unseekable,
            .NOBUFS => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .AGAIN => return error.WouldBlock,
            .IO => return error.InputOutput,
            .ISDIR => return error.IsDir,
            .NOTCONN => |err| return errnoBug(err), // not a socket
            .CONNRESET => |err| return errnoBug(err), // not a socket
            .INVAL => |err| return errnoBug(err),
            .FAULT => |err| return errnoBug(err),
            .BADF => return error.NotOpenForReading,
            else => |err| return unexpectedErrno(err),
        }
    }
}

fn fileSeekBy(userdata: ?*anyopaque, file: File, offset: i64) File.SeekError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return ev.lseek(file.handle, @bitCast(offset), c.SEEK.CUR);
}

fn fileSeekTo(userdata: ?*anyopaque, file: File, offset: u64) File.SeekError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return ev.lseek(file.handle, offset, c.SEEK.SET);
}

fn lseek(ev: *Evented, fd: c.fd_t, offset: u64, whence: i32) File.SeekError!void {
    _ = ev;
    while (true) switch (c.errno(c.lseek(fd, @bitCast(offset), whence))) {
        .SUCCESS => return,
        .INTR => {},
        .BADF => |err| return errnoBug(err), // File descriptor used after closed.
        .INVAL => return error.Unseekable,
        .OVERFLOW => return error.Unseekable,
        .SPIPE => return error.Unseekable,
        .NXIO => return error.Unseekable,
        else => |err| return unexpectedErrno(err),
    };
}

fn fileSync(userdata: ?*anyopaque, file: File) File.SyncError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    while (true) switch (c.errno(c.fsync(file.handle))) {
        .SUCCESS => return,
        .INTR => {},
        .BADF => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err),
        .ROFS => |err| return errnoBug(err),
        .IO => return error.InputOutput,
        .NOSPC => return error.NoSpaceLeft,
        .DQUOT => return error.DiskQuota,
        else => |err| return unexpectedErrno(err),
    };
}

fn fileIsTty(userdata: ?*anyopaque, file: File) Io.Cancelable!bool {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    while (true) {
        const rc = c.isatty(file.handle);
        switch (c.errno(rc - 1)) {
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
    _ = ev;

    const signed_len: i64 = @bitCast(length);
    if (signed_len < 0) return error.FileTooBig; // Avoid ambiguous EINVAL errors.

    while (true) switch (c.errno(c.ftruncate(file.handle, signed_len))) {
        .SUCCESS => return,
        .INTR => {},
        .FBIG => return error.FileTooBig,
        .IO => return error.InputOutput,
        .PERM => return error.PermissionDenied,
        .TXTBSY => return error.FileBusy,
        .BADF => |err| return errnoBug(err), // Handle not open for writing.
        .INVAL => return error.NonResizable, // This is returned for /dev/null for example.
        else => |err| return unexpectedErrno(err),
    };
}

fn fileSetOwner(
    userdata: ?*anyopaque,
    file: File,
    owner: ?File.Uid,
    group: ?File.Gid,
) File.SetOwnerError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    return fchown(file.handle, owner, group);
}

fn fileSetPermissions(
    userdata: ?*anyopaque,
    file: File,
    permissions: File.Permissions,
) File.SetPermissionsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    return ev.fchmod(file.handle, permissions.toMode());
}

fn fchmod(ev: *Evented, fd: c.fd_t, mode: c.mode_t) File.SetPermissionsError!void {
    _ = ev;
    while (true) switch (c.errno(c.fchmod(fd, mode))) {
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
        .PERM => return error.PermissionDenied,
        .ROFS => return error.ReadOnlyFileSystem,
        else => |err| return unexpectedErrno(err),
    };
}

fn fileSetTimestamps(
    userdata: ?*anyopaque,
    file: File,
    options: File.SetTimestampsOptions,
) File.SetTimestampsError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;

    var times_buffer: [2]c.timespec = undefined;
    const times = if (options.modify_timestamp == .now and options.access_timestamp == .now) null else p: {
        times_buffer = .{
            setTimestampToPosix(options.access_timestamp),
            setTimestampToPosix(options.modify_timestamp),
        };
        break :p &times_buffer;
    };

    while (true) switch (c.errno(c.futimens(file.handle, times))) {
        .SUCCESS => return,
        .INTR => {},
        .BADF => |err| return errnoBug(err), // always a race condition
        .FAULT => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err),
        .ACCES => return error.AccessDenied,
        .PERM => return error.PermissionDenied,
        .ROFS => return error.ReadOnlyFileSystem,
        else => |err| return unexpectedErrno(err),
    };
}

fn fileLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const operation: i32 = switch (lock) {
        .none => c.LOCK.UN,
        .shared => c.LOCK.SH,
        .exclusive => c.LOCK.EX,
    };
    while (true) switch (c.errno(c.flock(file.handle, operation))) {
        .SUCCESS => return,
        .INTR => {},
        .BADF => |err| return errnoBug(err),
        .INVAL => |err| return errnoBug(err), // invalid parameters
        .NOLCK => return error.SystemResources,
     
```
