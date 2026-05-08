```
          .progressParentFile = progressParentFile,

            .now = now,
            .clockResolution = clockResolution,
            .sleep = sleep,

            .random = random,
            .randomSecure = randomSecure,

            .netListenIp = netListenIpUnavailable,
            .netAccept = netAcceptUnavailable,
            .netBindIp = netBindIp,
            .netConnectIp = netConnectIpUnavailable,
            .netListenUnix = netListenUnixUnavailable,
            .netConnectUnix = netConnectUnixUnavailable,
            .netSocketCreatePair = netSocketCreatePairUnavailable,
            .netSend = netSendUnavailable,
            .netRead = netReadUnavailable,
            .netWrite = netWriteUnavailable,
            .netWriteFile = netWriteFileUnavailable,
            .netClose = netClose,
            .netShutdown = netShutdown,
            .netInterfaceNameResolve = netInterfaceNameResolveUnavailable,
            .netInterfaceName = netInterfaceNameUnavailable,
            .netLookup = netLookupUnavailable,
        },
    };
}

pub const InitOptions = struct {
    backing_allocator_needs_mutex: bool = true,

    /// Maximum thread pool size (excluding the main thread).
    /// Defaults to one less than the number of logical CPU cores.
    thread_limit: ?usize = null,
    /// Maximum number of threads that may perform synchronous syscalls.
    sync_limit: Io.Limit = .unlimited,

    log2_ring_entries: u4 = 3,

    /// Affects the following operations:
    /// * `processExecutablePath` on OpenBSD and Haiku.
    argv0: Argv0 = .empty,
    /// Affects the following operations:
    /// * `fileIsTty`
    /// * `processSpawn`, `processSpawnPath`, `processReplace`, `processReplacePath`
    environ: process.Environ = .empty,
};

pub fn init(ev: *Evented, backing_allocator: Allocator, options: InitOptions) !void {
    const threads_size = @sizeOf(Thread) * if (options.thread_limit) |thread_limit|
        1 + thread_limit
    else
        @max(std.Thread.getCpuCount() catch 1, 1);
    const idle_stack_end_offset =
        std.mem.alignForward(usize, threads_size + idle_stack_size, std.heap.pageSize());
    const allocated_slice = try backing_allocator.alignedAlloc(u8, .of(Thread), idle_stack_end_offset);
    errdefer backing_allocator.free(allocated_slice);
    ev.* = .{
        .backing_allocator_needs_mutex = options.backing_allocator_needs_mutex,
        .backing_allocator_mutex = .init,
        .backing_allocator = backing_allocator,
        .main_fiber_buffer = undefined,
        .log2_ring_entries = options.log2_ring_entries,
        .threads = .{
            .allocated = @ptrCast(allocated_slice[0..threads_size]),
            .reserved = 1,
            .active = 1,
        },
        .sync_limit = if (options.sync_limit.toInt()) |sync_limit| .{ .permits = sync_limit } else null,

        .stderr_writer_initialized = false,
        .stderr_mutex = .init,
        .stderr_writer = .{
            .io = ev.io(),
            .interface = Io.File.Writer.initInterface(&.{}),
            .file = .stderr(),
            .mode = .streaming,
        },
        .stderr_mode = .no_color,

        .environ_mutex = .init,
        .environ_initialized = options.environ.block.isEmpty(),
        .environ = .{ .process_environ = options.environ },

        .null_fd = .init,
        .random_fd = .init,

        .csprng_mutex = .init,
        .csprng = .uninitialized,
    };
    const main_fiber: *Fiber = @ptrCast(&ev.main_fiber_buffer);
    main_fiber.* = .{
        .required_align = {},
        .context = undefined,
        .link = .{ .awaiter = null },
        .status = .{ .queue_next = null },
        .cancel_status = .unrequested,
        .cancel_protection = .unblocked,
        .name = if (tracy.enable) "main task",
    };
    const main_thread = &ev.threads.allocated[0];
    Thread.self = main_thread;
    main_thread.* = .{
        .required_align = {},
        .thread = undefined,
        .idle_context = switch (builtin.cpu.arch) {
            .aarch64 => .{
                .sp = @intFromPtr(allocated_slice[idle_stack_end_offset..].ptr),
                .fp = @intFromPtr(ev),
                .pc = @intFromPtr(&mainIdleEntry),
            },
            .riscv64 => .{
                .sp = @intFromPtr(allocated_slice[idle_stack_end_offset..].ptr),
                .fp = @intFromPtr(ev),
                .pc = @intFromPtr(&mainIdleEntry),
            },
            .x86_64 => .{
                .rsp = @intFromPtr(allocated_slice[idle_stack_end_offset..].ptr),
                .rbp = @intFromPtr(ev),
                .rip = @intFromPtr(&mainIdleEntry),
            },
            else => @compileError("unimplemented architecture"),
        },
        .current_context = &main_fiber.context,
        .ready_queue = null,
        .free_queue = null,
        .io_uring = try .init(
            @as(u16, 1) << ev.log2_ring_entries,
            linux.IORING_SETUP_COOP_TASKRUN | linux.IORING_SETUP_SINGLE_ISSUER,
        ),
        .idle_search_index = 1,
        .steal_ready_search_index = 1,
        .steal_free_search_index = 1,
        .name_arena = .{},
        .csprng = .uninitialized,
    };
    errdefer main_thread.io_uring.deinit();
    if (tracy.enable) tracy.fiberEnter(main_fiber.name);
}

pub fn deinit(ev: *Evented) void {
    const main_fiber: *Fiber = @ptrCast(&ev.main_fiber_buffer);
    assert(Thread.current().currentFiber() == main_fiber);
    const active_threads = @atomicLoad(u32, &ev.threads.active, .acquire);
    for (ev.threads.allocated[0..active_threads]) |*thread| {
        const ready_fiber = @atomicLoad(?*Fiber, &thread.ready_queue, .monotonic);
        assert(ready_fiber == null or ready_fiber == Fiber.finished); // pending async
    }
    ev.yield(null, .exit);
    ev.null_fd.close();
    ev.random_fd.close();
    const allocated_ptr: [*]align(@alignOf(Thread)) u8 = @ptrCast(@alignCast(ev.threads.allocated.ptr));
    const idle_stack_end_offset = std.mem.alignForward(
        usize,
        ev.threads.allocated.len * @sizeOf(Thread) + idle_stack_size,
        std.heap.page_size_max,
    );
    for (ev.threads.allocated[1..active_threads]) |*thread| thread.thread.join();
    for (ev.threads.allocated[0..active_threads]) |*thread| thread.deinit(ev.backing_allocator);
    assert(active_threads == ev.threads.active); // spawned threads while there was no pending async?
    ev.backing_allocator.free(allocated_ptr[0..idle_stack_end_offset]);
    ev.* = undefined;
}

fn findReadyFiber(ev: *Evented, thread: *Thread) ?*Fiber {
    if (@atomicRmw(?*Fiber, &thread.ready_queue, .Xchg, Fiber.finished, .acquire)) |ready_fiber| {
        assert(ready_fiber != Fiber.finished);
        @atomicStore(?*Fiber, &thread.ready_queue, ready_fiber.status.queue_next, .release);
        ready_fiber.status.queue_next = null;
        return ready_fiber;
    }
    const active_threads = @atomicLoad(u32, &ev.threads.active, .acquire);
    for (0..@min(max_steal_ready_search, active_threads)) |_| {
        defer thread.steal_ready_search_index += 1;
        if (thread.steal_ready_search_index == active_threads) thread.steal_ready_search_index = 0;
        const steal_ready_search_thread =
            &ev.threads.allocated[0..active_threads][thread.steal_ready_search_index];
        if (steal_ready_search_thread == thread) continue;
        const ready_fiber =
            @atomicLoad(?*Fiber, &steal_ready_search_thread.ready_queue, .monotonic) orelse continue;
        if (ready_fiber == Fiber.finished) continue;
        if (@cmpxchgWeak(
            ?*Fiber,
            &steal_ready_search_thread.ready_queue,
            ready_fiber,
            null,
            .acquire,
            .monotonic,
        )) |_| continue;
        @atomicStore(?*Fiber, &thread.ready_queue, ready_fiber.status.queue_next, .release);
        ready_fiber.status.queue_next = null;
        return ready_fiber;
    }
    // couldn't find anything to do, so we are now open for business
    @atomicStore(?*Fiber, &thread.ready_queue, null, .monotonic);
    return null;
}

fn yield(ev: *Evented, maybe_ready_fiber: ?*Fiber, pending_task: SwitchMessage.PendingTask) void {
    const thread: *Thread = .current();
    const ready_context = if (maybe_ready_fiber orelse ev.findReadyFiber(thread)) |ready_fiber|
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
    contextSwitch(&message).handle(ev);
}

fn schedule(ev: *Evented, thread: *Thread, ready_queue: Fiber.Queue) bool {
    // shared fields of previous `Thread` must be initialized before later ones are marked as active
    const new_thread_index = @atomicLoad(u32, &ev.threads.active, .acquire);
    for (0..@min(max_idle_search, new_thread_index)) |_| {
        defer thread.idle_search_index += 1;
        if (thread.idle_search_index == new_thread_index) thread.idle_search_index = 0;
        const idle_search_thread = &ev.threads.allocated[0..new_thread_index][thread.idle_search_index];
        if (idle_search_thread == thread) continue;
        if (@cmpxchgWeak(
            ?*Fiber,
            &idle_search_thread.ready_queue,
            null,
            ready_queue.head,
            .release,
            .monotonic,
        )) |_| continue;
        thread.enqueue().* = .{
            .opcode = .MSG_RING,
            .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
            .ioprio = 0,
            .fd = idle_search_thread.io_uring.fd,
            .off = @intFromEnum(Completion.Userdata.wakeup),
            .addr = @intFromEnum(linux.IORING_MSG_RING_COMMAND.DATA),
            .len = 0,
            .rw_flags = 0,
            .user_data = @intFromEnum(Completion.Userdata.wakeup),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        return true;
    }
    spawn_thread: {
        // previous failed reservations must have completed before retrying
        if (new_thread_index == ev.threads.allocated.len or @cmpxchgWeak(
            u32,
            &ev.threads.reserved,
            new_thread_index,
            new_thread_index + 1,
            .acquire,
            .monotonic,
        ) != null) break :spawn_thread;
        const new_thread = &ev.threads.allocated[new_thread_index];
        const next_thread_index = new_thread_index + 1;
        var params = std.mem.zeroInit(linux.io_uring_params, .{
            .flags = linux.IORING_SETUP_ATTACH_WQ |
                linux.IORING_SETUP_R_DISABLED |
                linux.IORING_SETUP_COOP_TASKRUN |
                linux.IORING_SETUP_SINGLE_ISSUER,
            .wq_fd = @as(u32, @intCast(ev.threads.allocated[0].io_uring.fd)),
        });
        new_thread.* = .{
            .required_align = {},
            .thread = undefined,
            .idle_context = undefined,
            .current_context = &new_thread.idle_context,
            .ready_queue = ready_queue.head,
            .free_queue = null,
            .io_uring = IoUring.init_params(@as(u16, 1) << ev.log2_ring_entries, &params) catch |err| {
                @atomicStore(u32, &ev.threads.reserved, new_thread_index, .release);
                // no more access to `thread` after giving up reservation
                log.warn("unable to create worker thread due to io_uring init failure: {s}", .{
                    @errorName(err),
                });
                break :spawn_thread;
            },
            .idle_search_index = 0,
            .steal_ready_search_index = 0,
            .steal_free_search_index = 0,
            .name_arena = .{},
            .csprng = .uninitialized,
        };
        new_thread.thread = std.Thread.spawn(.{
            .stack_size = idle_stack_size,
            .allocator = ev.allocator(),
        }, threadEntry, .{ ev, new_thread_index }) catch |err| {
            new_thread.io_uring.deinit();
            @atomicStore(u32, &ev.threads.reserved, new_thread_index, .release);
            // no more access to `thread` after giving up reservation
            log.warn("unable to create worker thread due spawn failure: {s}", .{@errorName(err)});
            break :spawn_thread;
        };
        // shared fields of `Thread` must be initialized before being marked active
        @atomicStore(u32, &ev.threads.active, next_thread_index, .release);
        return false;
    }
    // nobody wanted it, so just queue it on ourselves
    while (true) ready_queue.tail.status.queue_next = @cmpxchgWeak(
        ?*Fiber,
        &thread.ready_queue,
        ready_queue.tail.status.queue_next,
        ready_queue.head,
        .acq_rel,
        .acquire,
    ) orelse break;
    return false;
}

fn threadEntry(ev: *Evented, index: u32) void {
    const thread: *Thread = &ev.threads.allocated[index];
    Thread.self = thread;
    switch (linux.errno(linux.io_uring_register(thread.io_uring.fd, .REGISTER_ENABLE_RINGS, null, 0))) {
        .SUCCESS => ev.idle(thread),
        else => |err| @panic(@tagName(err)),
    }
}

const Completion = struct {
    result: i32,
    flags: u32,

    const Userdata = enum(usize) {
        unused,
        wakeup,
        futex_wake,
        close,
        cleanup,
        exit,
        /// If bit 0 is 1, a pointer to the `context` field of `Io.Batch.Storage.Pending`.
        /// If bits 0 and 1 are 0, a `*Fiber`.
        _,
    };

    fn errno(completion: Completion) linux.E {
        return linux.errno(@bitCast(@as(isize, completion.result)));
    }
};

fn mainIdleEntry() callconv(.naked) void {
    switch (builtin.cpu.arch) {
        .aarch64 => asm volatile (
            \\ mov x0, fp
            \\ mov fp, #0
            \\ b %[mainIdle]
            :
            : [mainIdle] "X" (&mainIdle),
        ),
        .riscv64 => asm volatile (
            \\ mv a0, fp
            \\ mv fp, zero
            \\ tail %[mainIdle]@plt
            :
            : [mainIdle] "X" (&mainIdle),
        ),
        .x86_64 => asm volatile (
            \\ movq %%rbp, %%rdi
            \\ xor %%ebp, %%ebp
            \\ jmp %[mainIdle:P]
            :
            : [mainIdle] "X" (&mainIdle),
        ),
        else => |arch| @compileError("unimplemented architecture: " ++ @tagName(arch)),
    }
}

fn mainIdle(
    ev: *Evented,
    message: *const SwitchMessage,
) callconv(.withStackAlign(.c, @max(@alignOf(Thread), @alignOf(Io.fiber.Context)))) noreturn {
    message.handle(ev);
    ev.idle(&ev.threads.allocated[0]);
    ev.yield(@ptrCast(&ev.main_fiber_buffer), .nothing);
    unreachable; // switched to dead fiber
}

fn idle(ev: *Evented, thread: *Thread) void {
    var maybe_ready_fiber: ?*Fiber = null;
    while (true) {
        while (maybe_ready_fiber orelse ev.findReadyFiber(thread)) |ready_fiber| {
            ev.yield(ready_fiber, .nothing);
            maybe_ready_fiber = null;
        }
        _ = thread.io_uring.submit_and_wait(1) catch |err| switch (err) {
            error.SignalInterrupt => {},
            else => |e| @panic(@errorName(e)),
        };
        var maybe_ready_queue: ?Fiber.Queue = null;
        while (true) {
            var cqes_buffer: [1 << 8]linux.io_uring_cqe = undefined;
            const cqes = cqes_buffer[0 .. thread.io_uring.copy_cqes(&cqes_buffer, 0) catch |err| switch (err) {
                error.SignalInterrupt => 0,
                else => |e| @panic(@errorName(e)),
            }];
            if (cqes.len == 0) break;
            for (cqes) |cqe| if (cqe.flags & linux.IORING_CQE_F_SKIP == 0) switch (@as(
                Completion.Userdata,
                @enumFromInt(cqe.user_data),
            )) {
                .unused => unreachable, // bad submission queued?
                .wakeup => {},
                .futex_wake => switch (Completion.errno(.{ .result = cqe.res, .flags = cqe.flags })) {
                    .SUCCESS => recoverableOsBugDetected(), // success is skipped
                    .INVAL => {}, // invalid futex_wait() on ptr done elsewhere
                    .INTR, .CANCELED => recoverableOsBugDetected(), // `Completion.Userdata.futex_wake` is not cancelable
                    .FAULT => {}, // pointer became invalid while doing the wake
                    else => recoverableOsBugDetected(), // deadlock due to operating system bug
                },
                .close => switch (Completion.errno(.{ .result = cqe.res, .flags = cqe.flags })) {
                    .BADF => recoverableOsBugDetected(), // Always a race condition.
                    .INTR => {}, // This is still a success. See https://github.com/ziglang/zig/issues/2425
                    else => {},
                },
                .cleanup => @panic("failed to notify other threads that we are exiting"),
                .exit => {
                    assert(maybe_ready_fiber == null and maybe_ready_queue == null); // pending async
                    return;
                },
                _ => if (@as(?*Fiber, ready_fiber: switch (@as(u2, @truncate(cqe.user_data))) {
                    0b00 => {
                        const ready_fiber: *Fiber = @ptrFromInt(cqe.user_data & ~@as(usize, 0b11));
                        ready_fiber.resultPointer(Completion).* = .{
                            .result = cqe.res,
                            .flags = cqe.flags,
                        };
                        break :ready_fiber ready_fiber;
                    },
                    0b01 => {
                        thread.enqueue().* = .{
                            .opcode = .ASYNC_CANCEL,
                            .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
                            .ioprio = 0,
                            .fd = 0,
                            .off = 0,
                            .addr = cqe.user_data & ~@as(usize, 0b11),
                            .len = 0,
                            .rw_flags = 0,
                            .user_data = @intFromEnum(Completion.Userdata.wakeup),
                            .buf_index = 0,
                            .personality = 0,
                            .splice_fd_in = 0,
                            .addr3 = 0,
                            .resv = 0,
                        };
                        break :ready_fiber null;
                    },
                    0b10 => {
                        const batch_userdata: *Io.Operation.Storage.Pending.Userdata =
                            @ptrFromInt(cqe.user_data & ~@as(usize, 0b11));
                        const batch: *Io.Batch = @ptrFromInt(batch_userdata[0]);
                        var next: usize = 0b00;
                        batch_userdata[0..3].* = .{ next, @as(u32, @bitCast(cqe.res)), cqe.flags };
                        while (true) {
                            next = @cmpxchgWeak(
                                usize,
                                @as(*usize, @ptrCast(&batch.userdata)),
                                next,
                                cqe.user_data,
                                .release,
                                .acquire,
                            ) orelse break;
                            batch_userdata[0] = next;
                        }
                        break :ready_fiber switch (@as(u2, @truncate(next))) {
                            0b00, 0b01 => @ptrFromInt(next & ~@as(usize, 0b11)),
                            0b10, 0b11 => null,
                        };
                    },
                    0b11 => switch (Completion.errno(.{ .result = cqe.res, .flags = cqe.flags })) {
                        .SUCCESS => unreachable, // no event count specified
                        .TIME => {
                            const context: *usize = @ptrFromInt(cqe.user_data & ~@as(usize, 0b11));
                            const fiber = @atomicRmw(usize, context, .Add, 0b01, .acquire);
                            break :ready_fiber switch (@as(u2, @truncate(fiber))) {
                                else => unreachable, // timeout completed multiple times
                                0b00 => @ptrFromInt(fiber & ~@as(usize, 0b11)),
                                0b10 => null,
                            };
                        },
                        .CANCELED => null, // user data may have been invalidated
                        else => |err| unexpectedErrno(err) catch null,
                    },
                })) |ready_fiber| {
                    assert(ready_fiber.status.queue_next == null);
                    if (maybe_ready_fiber == null) {
                        maybe_ready_fiber = ready_fiber;
                    } else if (maybe_ready_queue) |*ready_queue| {
                        ready_queue.tail.status.queue_next = ready_fiber;
                        ready_queue.tail = ready_fiber;
                    } else maybe_ready_queue = .{ .head = ready_fiber, .tail = ready_fiber };
                },
            };
        }
        if (maybe_ready_queue) |ready_queue| _ = ev.schedule(thread, ready_queue);
    }
}

const SwitchMessage = struct {
    contexts: Io.fiber.Switch,
    pending_task: PendingTask,

    const PendingTask = union(enum) {
        nothing,
        reschedule,
        await: *Fiber,
        group_await: Group,
        group_cancel: Group,
        batch_await: *Io.Batch,
        destroy,
        exit,
    };

    fn handle(message: *const SwitchMessage, ev: *Evented) void {
        const thread: *Thread = .current();
        thread.current_context = message.contexts.new;
        if (tracy.enable) {
            if (message.contexts.new != &thread.idle_context) {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.new));
                tracy.fiberEnter(fiber.name);
            } else tracy.fiberLeave();
        }
        switch (message.pending_task) {
            .nothing => {},
            .reschedule => if (message.contexts.old != &thread.idle_context) {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                assert(fiber.status.queue_next == null);
                _ = ev.schedule(thread, .{ .head = fiber, .tail = fiber });
            },
            .await => |awaiting| {
                const awaiter: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                assert(awaiter.status.queue_next == null);
                if (@atomicRmw(?*Fiber, &awaiting.link.awaiter, .Xchg, awaiter, .acq_rel) ==
                    Fiber.finished) _ = ev.schedule(thread, .{ .head = awaiter, .tail = awaiter });
            },
            .group_await => |group| {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                if (group.await(ev, fiber))
                    _ = ev.schedule(thread, .{ .head = fiber, .tail = fiber });
            },
            .group_cancel => |group| {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                if (group.cancel(ev, fiber))
                    _ = ev.schedule(thread, .{ .head = fiber, .tail = fiber });
            },
            .batch_await => |batch| {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                if (@cmpxchgStrong(
                    ?*anyopaque,
                    &batch.userdata,
                    null,
                    fiber,
                    .release,
                    .monotonic,
                )) |head| {
                    assert(@as(u2, @truncate(@intFromPtr(head))) != 0b00);
                    _ = ev.schedule(thread, .{ .head = fiber, .tail = fiber });
                }
            },
            .destroy => {
                const fiber: *Fiber = @alignCast(@fieldParentPtr("context", message.contexts.old));
                fiber.destroy();
            },
            .exit => for (
                ev.threads.allocated[0..@atomicLoad(u32, &ev.threads.active, .acquire)],
            ) |*each_thread| {
                thread.enqueue().* = .{
                    .opcode = .MSG_RING,
                    .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
                    .ioprio = 0,
                    .fd = each_thread.io_uring.fd,
                    .off = @intFromEnum(Completion.Userdata.exit),
                    .addr = @intFromEnum(linux.IORING_MSG_RING_COMMAND.DATA),
                    .len = 0,
                    .rw_flags = 0,
                    .user_data = @intFromEnum(Completion.Userdata.cleanup),
                    .buf_index = 0,
                    .personality = 0,
                    .splice_fd_in = 0,
                    .addr3 = 0,
                    .resv = 0,
                };
            },
        }
    }
};

inline fn contextSwitch(message: *const SwitchMessage) *const SwitchMessage {
    return @fieldParentPtr("contexts", Io.fiber.contextSwitch(&message.contexts));
}

fn crashHandler(userdata: ?*anyopaque) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const thread = Thread.self orelse std.process.abort();
    if (thread.current_context == &thread.idle_context) std.process.abort();
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
            .riscv64 => asm volatile (
                \\ mv a0, sp
                \\ tail %[call]@plt
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
        ev.yield(@atomicRmw(?*Fiber, &fiber.link.awaiter, .Xchg, Fiber.finished, .acq_rel), .nothing);
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
    assert(result_alignment.compare(.lte, Fiber.max_result_align)); // TODO
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
        .context = switch (builtin.cpu.arch) {
            .aarch64 => .{
                .sp = @intFromPtr(closure),
                .fp = 0,
                .pc = @intFromPtr(&AsyncClosure.entry),
            },
            .riscv64 => .{
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
        .status = .{ .queue_next = null },
        .cancel_status = .unrequested,
        .cancel_protection = .unblocked,
        .name = if (tracy.enable) name: {
            const thread: *Thread = .current();
            var name_arena = thread.name_arena.promote(std.heap.page_allocator);
            defer thread.name_arena = name_arena.state;
            break :name std.fmt.allocPrintSentinel(
                name_arena.allocator(),
                "task {d}",
                .{@atomicRmw(u64, &Fiber.next_name, .Add, 1, .monotonic)},
                0,
            ) catch return error.ConcurrencyUnavailable;
        },
    };
    closure.* = .{
        .evented = ev,
        .fiber = fiber,
        .start = start,
        .result_align = result_alignment,
    };
    @memcpy(closure.contextPointer(), context);

    const thread: *Thread = .current();
    if (ev.schedule(thread, .{ .head = fiber, .tail = fiber })) thread.submit();
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
        ev.yield(null, .{ .await = awaiting });
    @memcpy(result, awaiting.resultBytes(result_alignment));
    awaiting.destroy();
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
    fn mutexPtr(group: Group) *Mutex {
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
                Mutex,
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
                Mutex,
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
            Mutex,
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
                assert(awaiter.status.awaiting_group.ptr == group.ptr);
                awaiter.status = .{ .queue_next = null };
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
        assert(awaiter.status.queue_next == null);
        awaiter.status = .{ .awaiting_group = group };
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
                .riscv64 => asm volatile (
                    \\ mv a0, sp
                    \\ tail %[call]@plt
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
            assert(fiber.status.queue_next == null);
            closure.start(closure.contextPointer());
            ev.yield(closure.group.removeFiber(ev, fiber), .destroy);
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
        .context = switch (builtin.cpu.arch) {
            .aarch64 => .{
                .sp = @intFromPtr(closure),
                .fp = 0,
                .pc = @intFromPtr(&Group.AsyncClosure.entry),
            },
            .riscv64 => .{
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
        .status = .{ .queue_next = null },
        .cancel_status = .unrequested,
        .cancel_protection = .unblocked,
        .name = if (tracy.enable) name: {
            const thread: *Thread = .current();
            var name_arena = thread.name_arena.promote(std.heap.page_allocator);
            defer thread.name_arena = name_arena.state;
            break :name std.fmt.allocPrintSentinel(
                name_arena.allocator(),
                "group task {d}",
                .{@atomicRmw(u64, &Fiber.next_name, .Add, 1, .monotonic)},
                0,
            ) catch return error.ConcurrencyUnavailable;
        },
    };
    closure.* = .{
        .evented = ev,
        .group = group,
        .fiber = fiber,
        .start = start,
    };
    @memcpy(closure.contextPointer(), context);
    group.addFiber(ev, fiber);
    const thread: *Thread = .current();
    if (ev.schedule(thread, .{ .head = fiber, .tail = fiber })) thread.submit();
}

fn groupAwait(
    userdata: ?*anyopaque,
    type_erased: *Io.Group,
    initial_token: *anyopaque,
) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = initial_token;
    ev.yield(null, .{ .group_await = .{ .ptr = type_erased } });
}

fn groupCancel(userdata: ?*anyopaque, type_erased: *Io.Group, initial_token: *anyopaque) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = initial_token;
    ev.yield(null, .{ .group_cancel = .{ .ptr = type_erased } });
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

fn futexWait(
    userdata: ?*anyopaque,
    ptr: *const u32,
    expected: u32,
    timeout: Io.Timeout,
) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const timespec: ?linux.kernel_timespec, const clock: Io.Clock, const timeout_flags: u32 = timespec: switch (timeout) {
        .none => .{
            null,
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
        .opcode = .FUTEX_WAIT,
        .flags = if (timespec) |_| linux.IOSQE_IO_LINK else 0,
        .ioprio = 0,
        .fd = @bitCast(linux.FUTEX2_FLAGS{ .size = .U32, .private = true }),
        .off = expected,
        .addr = @intFromPtr(ptr),
        .len = 0,
        .rw_flags = 0,
        .user_data = @intFromPtr(cancel_region.fiber),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = std.math.maxInt(u32),
        .resv = 0,
    };
    if (timespec) |*timespec_ptr| thread.enqueue().* = .{
        .opcode = .LINK_TIMEOUT,
        .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
        .ioprio = 0,
        .fd = 0,
        .off = 0,
        .addr = @intFromPtr(timespec_ptr),
        .len = 1,
        .rw_flags = timeout_flags | @as(u32, switch (clock) {
            .real => linux.IORING_TIMEOUT_REALTIME,
            else => 0,
            .boot => linux.IORING_TIMEOUT_BOOTTIME,
        }),
        .user_data = @intFromEnum(Completion.Userdata.wakeup),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = 0,
        .resv = 0,
    };
    ev.yield(null, .nothing);
    switch (cancel_region.errno()) {
        .SUCCESS => {}, // notified by `wake()`
        .INTR, .CANCELED => {}, // caller's responsibility to retry
        .AGAIN => {}, // ptr.* != expect
        .INVAL => {}, // possibly timeout overflow
        .TIMEDOUT => unreachable,
        .FAULT => recoverableOsBugDetected(), // ptr was invalid
        else => recoverableOsBugDetected(),
    }
}

fn futexWaitUncancelable(userdata: ?*anyopaque, ptr: *const u32, expected: u32) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .initBlocked();
    defer cancel_region.deinit();
    const thread = cancel_region.awaitIoUring() catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    };
    thread.enqueue().* = .{
        .opcode = .FUTEX_WAIT,
        .flags = 0,
        .ioprio = 0,
        .fd = @bitCast(linux.FUTEX2_FLAGS{ .size = .U32, .private = true }),
        .off = expected,
        .addr = @intFromPtr(ptr),
        .len = 0,
        .rw_flags = 0,
        .user_data = @intFromPtr(cancel_region.fiber),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = std.math.maxInt(u32),
        .resv = 0,
    };
    ev.yield(null, .nothing);
    switch (cancel_region.errno()) {
        .SUCCESS => {}, // notified by `wake()`
        .INTR, .CANCELED => {}, // caller's responsibility to retry
        .AGAIN => {}, // ptr.* != expect
        .INVAL => {}, // possibly timeout overflow
        .FAULT => recoverableOsBugDetected(), // ptr was invalid
        else => recoverableOsBugDetected(),
    }
}

fn futexWake(userdata: ?*anyopaque, ptr: *const u32, max_waiters: u32) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    const thread: *Thread = .current();
    thread.enqueue().* = .{
        .opcode = .FUTEX_WAKE,
        .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
        .ioprio = 0,
        .fd = @bitCast(linux.FUTEX2_FLAGS{ .size = .U32, .private = true }),
        .off = max_waiters,
        .addr = @intFromPtr(ptr),
        .len = 0,
        .rw_flags = 0,
        .user_data = @intFromEnum(Completion.Userdata.futex_wake),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = std.math.maxInt(u32),
        .resv = 0,
    };
    thread.submit();
}

fn operate(userdata: ?*anyopaque, operation: Io.Operation) Io.Cancelable!Io.Operation.Result {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    return switch (operation) {
        .file_read_streaming => |o| .{
            .file_read_streaming = ev.fileReadStreaming(
                &maybe_sync.cancel_region,
                o.file,
                o.data,
            ) catch |err| switch (err) {
                error.Canceled => |e| return e,
                else => |e| e,
            },
        },
        .file_write_streaming => |o| .{
            .file_write_streaming = ev.fileWriteStreaming(
                &maybe_sync.cancel_region,
                o.file,
                o.header,
                o.data,
                o.splat,
            ) catch |err| switch (err) {
                error.Canceled => |e| return e,
                else => |e| e,
            },
        },
        .device_io_control => |o| .{
            .device_io_control = try ev.deviceIoControl(try maybe_sync.enterSync(ev), o),
        },
        .net_receive => |o| .{
            .net_receive = r: {
                const opt_err, const n = ev.netReceive(&maybe_sync.cancel_region, o.socket_handle, o.message_buffer, o.data_buffer, o.flags);
                break :r .{
                    if (opt_err) |err| switch (err) {
                        error.Canceled => |e| return e,
                        else => |e| e,
                    } else null,
                    n,
                };
            },
        },
    };
}

fn fileReadStreaming(
    ev: *Evented,
    cancel_region: *CancelRegion,
    file: File,
    data: []const []u8,
) File.ReadStreamingError!usize {
    var iovecs_buffer: [max_iovecs_len]iovec = undefined;
    var i: usize = 0;
    for (data) |buf| {
        if (iovecs_buffer.len - i == 0) break;
        if (buf.len > 0) {
            iovecs_buffer[i] = .{ .base = buf.ptr, .len = buf.len };
            i += 1;
        }
    }
    const dest = iovecs_buffer[0..i];
    assert(dest[0].len > 0);

    const n = try ev.preadv(cancel_region, file.handle, dest, null);
    return if (n == 0) error.EndOfStream else n;
}

fn fileWriteStreaming(
    ev: *Evented,
    cancel_region: *CancelRegion,
    file: File,
    header: []const u8,
    data: []const []const u8,
    splat: usize,
) File.Writer.Error!usize {
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
    return ev.pwritev(cancel_region, file.handle, iovecs[0..iovlen], null);
}

fn deviceIoControl(
    ev: *Evented,
    sync: *CancelRegion.Sync,
    o: Io.Operation.DeviceIoControl,
) Io.Cancelable!i32 {
    _ = ev;
    while (true) {
        try sync.cancel_region.await(.nothing);
        const rc = linux.ioctl(o.file.handle, @bitCast(o.code), @intFromPtr(o.arg));
        switch (linux.errno(rc)) {
            .SUCCESS => return @bitCast(@as(u32, @truncate(rc))),
            .INTR => {},
            else => |err| return -@as(i32, @intFromEnum(err)),
        }
    }
}

fn batchAwaitAsync(userdata: ?*anyopaque, batch: *Io.Batch) Io.Cancelable!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    ev.batchDrainSubmitted(&maybe_sync, batch, false) catch |err| switch (err) {
        error.ConcurrencyUnavailable => unreachable, // passed concurrency=false
        error.Canceled => |e| return e,
    };
    maybe_sync.leaveSync(ev);
    while (true) {
        batchDrainReady(batch) catch |err| switch (err) {
            error.Timeout => unreachable, // no timeout
        };
        if (batch.completed.head != .none or batch.pending.head == .none) return;
        ev.yield(null, .{ .batch_await = batch });
    }
}

fn batchAwaitConcurrent(
    userdata: ?*anyopaque,
    batch: *Io.Batch,
    timeout: Io.Timeout,
) Io.Batch.AwaitConcurrentError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    try ev.batchDrainSubmitted(&maybe_sync, batch, true);
    maybe_sync.leaveSync(ev);
    const timespec: linux.kernel_timespec, const clock: Io.Clock, const timeout_flags: u32 = while (true) {
        batchDrainReady(batch) catch |err| switch (err) {
            error.Timeout => unreachable, // no timeout
        };
        if (batch.completed.head != .none or batch.pending.head == .none) return;
        switch (timeout) {
            .none => ev.yield(null, .{ .batch_await = batch }),
            .duration => |duration| {
                const ns = duration.raw.toNanoseconds();
                break .{
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
                break .{
                    .{
                        .sec = @intCast(@divFloor(ns, std.time.ns_per_s)),
                        .nsec = @intCast(@mod(ns, std.time.ns_per_s)),
                    },
                    deadline.clock,
                    linux.IORING_TIMEOUT_ABS,
                };
            },
        }
    };
    {
        const thread = try maybe_sync.cancel_region.awaitIoUring();
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
            .user_data = @intFromPtr(&batch.userdata) | 0b11,
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
    }
    while (batch.completed.head == .none and batch.pending.head != .none) {
        ev.yield(null, .{ .batch_await = batch });
        batchDrainReady(batch) catch |err| switch (err) {
            error.Timeout => |e| return if (batch.completed.head == .none and
                batch.pending.head != .none) e,
        };
    }
    const thread = try maybe_sync.cancel_region.awaitIoUring();
    thread.enqueue().* = .{
        .opcode = .TIMEOUT_REMOVE,
        .flags = 0,
        .ioprio = 0,
        .fd = 0,
        .off = 0,
        .addr = @intFromPtr(&batch.userdata) | 0b11,
        .len = 0,
        .rw_flags = 0,
        .user_data = @intFromPtr(maybe_sync.cancel_region.fiber),
        .buf_index = 0,
        .personality = 0,
        .splice_fd_in = 0,
        .addr3 = 0,
        .resv = 0,
    };
    ev.yield(null, .nothing);
    switch (maybe_sync.cancel_region.errno()) {
        .SUCCESS => return,
        .BUSY, .NOENT => {},
        else => |err| unexpectedErrno(err) catch {},
    }
    while (true) {
        batchDrainReady(batch) catch |err| switch (err) {
            error.Timeout => return,
        };
        ev.yield(null, .{ .batch_await = batch });
    }
}

/// If `concurrency` is false, `error.ConcurrencyUnavailable` is unreachable.
fn batchDrainSubmitted(
    ev: *Evented,
    maybe_sync: *CancelRegion.Sync.Maybe,
    batch: *Io.Batch,
    concurrency: bool,
) (Io.ConcurrentError || Io.Cancelable)!void {
    var index = batch.submitted.head;
    if (index == .none) return;
    const thread = try maybe_sync.cancelRegion().awaitIoUring();
    errdefer batch.submitted.head = index;
    while (index != .none) {
        const storage = &batch.storage[index.toIndex()];
        const next_index = storage.submission.node.next;
        if (@as(?Io.Operation.Result, result: switch (storage.submission.operation) {
            .file_read_streaming => |o| {
                const buffer = for (o.data) |buffer| {
                    if (buffer.len > 0) break buffer;
                } else break :result .{ .file_read_streaming = 0 };
                const fd = o.file.handle;
                storage.* = .{ .pending = .{
                    .node = .{ .prev = batch.pending.tail, .next = .none },
                    .tag = .file_read_streaming,
                    .userdata = undefined,
                } };
                thread.enqueue().* = .{
                    .opcode = .READ,
                    .flags = 0,
                    .ioprio = 0,
                    .fd = fd,
                    .off = std.math.maxInt(u64),
                    .addr = @intFromPtr(buffer.ptr),
                    .len = @min(buffer.len, 0xfffff000),
                    .rw_flags = 0,
                    .user_data = @intFromPtr(&storage.pending.userdata) | 0b10,
                    .buf_index = 0,
                    .personality = 0,
                    .splice_fd_in = 0,
                    .addr3 = 0,
                    .resv = 0,
                };
                break :result null;
            },
            .file_write_streaming => |o| {
                const buffer = buffer: {
                    if (o.header.len != 0) break :buffer o.header;
                    for (o.data[0 .. o.data.len - 1]) |buffer| {
                        if (buffer.len > 0) break :buffer buffer;
                    }
                    if (o.splat > 0) break :buffer o.data[o.data.len - 1];
                    break :result .{ .file_write_streaming = 0 };
                };
                const fd = o.file.handle;
                storage.* = .{ .pending = .{
                    .node = .{ .prev = batch.pending.tail, .next = .none },
                    .tag = .file_write_streaming,
                    .userdata = undefined,
                } };
                thread.enqueue().* = .{
                    .opcode = .WRITE,
                    .flags = 0,
                    .ioprio = 0,
                    .fd = fd,
                    .off = std.math.maxInt(u64),
                    .addr = @intFromPtr(buffer.ptr),
                    .len = @min(buffer.len, 0xfffff000),
                    .rw_flags = 0,
                    .user_data = @intFromPtr(&storage.pending.userdata) | 0b10,
                    .buf_index = 0,
                    .personality = 0,
                    .splice_fd_in = 0,
                    .addr3 = 0,
                    .resv = 0,
                };
                break :result null;
            },
            .device_io_control => |o| if (concurrency)
                return error.ConcurrencyUnavailable
            else
                .{ .device_io_control = try ev.deviceIoControl(try maybe_sync.enterSync(ev), o) },
            .net_receive => |o| {
                _ = o;
                @panic("TODO implement batchDrainSubmitted for net_receive");
            },
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
            storage.pending.userdata[0] = @intFromPtr(batch);
        }
        index = next_index;
    }
    batch.submitted = .{ .head = .none, .tail = .none };
}

fn batchDrainReady(batch: *Io.Batch) Io.Timeout.Error!void {
    while (@atomicRmw(?*anyopaque, &batch.userdata, .Xchg, null, .acquire)) |head| {
        var next: usize = @intFromPtr(head);
        var timeout = false;
        while (cond: switch (@as(u2, @truncate(next))) {
            0b00 => if (timeout) return error.Timeout else false,
            0b01 => {
                assert(!timeout);
                return error.Timeout;
            },
            0b10 => true,
            0b11 => {
                assert(!timeout);
                timeout = true;
                break :cond true;
            },
        }) {
            const operation_userdata: *Io.Operation.Storage.Pending.Userdata =
                @ptrFromInt(next & ~@as(usize, 0b11));
            next = operation_userdata[0];
            const completion: Completion = .{
                .result = @bitCast(@as(u32, @intCast(operation_userdata[1]))),
                .flags = @intCast(operation_userdata[2]),
            };
            const pending: *Io.Operation.Storage.Pending =
                @fieldParentPtr("userdata", operation_userdata);
            const storage: *Io.Operation.Storage = @fieldParentPtr("pending", pending);
            const index: Io.Operation.OptionalIndex = .fromIndex(storage - batch.storage.ptr);
            assert(completion.flags & linux.IORING_CQE_F_SKIP == 0);
            switch (pending.node.prev) {
                .none => batch.pending.head = pending.node.next,
                else => |prev_index| batch.storage[prev_index.toIndex()].pending.node.next =
                    pending.node.next,
            }
            switch (pending.node.next) {
                .none => batch.pending.tail = pending.node.prev,
                else => |prev_index| batch.storage[prev_index.toIndex()].pending.node.prev =
                    pending.node.prev,
            }
            if (@as(?Io.Operation.Result, result: switch (pending.tag) {
                .file_read_streaming => .{
                    .file_read_streaming = switch (completion.errno()) {
                        .SUCCESS => @as(u32, @bitCast(completion.result)),
                        .INTR => 0,
                        .CANCELED => break :result null,
                        .INVAL => |err| errnoBug(err),
                        .FAULT => |err| errnoBug(err),
                        .AGAIN => error.WouldBlock,
                        .BADF => |err| errnoBug(err), // File descriptor used after closed
                        .IO => error.InputOutput,
                        .ISDIR => error.IsDir,
                        .NOBUFS => error.SystemResources,
                        .NOMEM => error.SystemResources,
                        .NOTCONN => error.SocketUnconnected,
                        .CONNRESET => error.ConnectionResetByPeer,
                        else => |err| unexpectedErrno(err),
                    },
                },
                .file_write_streaming => .{
                    .file_write_streaming = switch (completion.errno()) {
                        .SUCCESS => @as(u32, @bitCast(completion.result)),
                        .INTR => 0,
                        .CANCELED => break :result null,
                        .INVAL => |err| errnoBug(err),
                        .FAULT => |err| errnoBug(err),
                        .AGAIN => error.WouldBlock,
                        .BADF => error.NotOpenForWriting, // Can be a race condition.
                        .DESTADDRREQ => |err| errnoBug(err), // `connect` was never called.
                        .DQUOT => error.DiskQuota,
                        .FBIG => error.FileTooBig,
                        .IO => error.InputOutput,
                        .NOSPC => error.NoSpaceLeft,
                        .PERM => error.PermissionDenied,
                        .PIPE => error.BrokenPipe,
                        .CONNRESET => |err| errnoBug(err), // Not a socket handle.
                        .BUSY => error.DeviceBusy,
                        else => |err| unexpectedErrno(err),
                    },
                },
                .device_io_control => unreachable,
                .net_receive => @panic("TODO"),
            })) |result| {
                switch (batch.completed.tail) {
                    .none => batch.completed.head = index,
                    else => |tail_index| batch.storage[tail_index.toIndex()].completion.node.next =
                        index,
                }
                storage.* = .{ .completion = .{ .node = .{ .next = .none }, .result = result } };
                batch.completed.tail = index;
            } else {
                switch (batch.unused.tail) {
                    .none => batch.unused.head = index,
                    else => |tail_index| batch.storage[tail_index.toIndex()].unused.next = index,
                }
                storage.* = .{ .unused = .{ .prev = batch.unused.tail, .next = .none } };
                batch.unused.tail = index;
            }
        }
    }
}

fn batchCancel(userdata: ?*anyopaque, batch: *Io.Batch) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = ev;
    batchDrainReady(batch) catch |err| switch (err) {
        error.Timeout => unreachable, // no timeout
    };
    var index = batch.pending.head;
    if (index == .none) return;
    var cancel_region: CancelRegion = .initBlocked();
    defer cancel_region.deinit();
    const thread = cancel_region.awaitIoUring() catch |err| switch (err) {
        error.Canceled => unreachable, // blocked
    };
    while (index != .none) {
        const pending = &batch.storage[index.toIndex()].pending;
        thread.enqueue().* = .{
            .opcode = .ASYNC_CANCEL,
            .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
            .ioprio = 0,
            .fd = 0,
            .off = 0,
            .addr = @intFromPtr(&pending.userdata) | 0b10,
            .len = 0,
            .rw_flags = 0,
            .user_data = @intFromEnum(Completion.Userdata.wakeup),
            .buf_index = 0,
            .personality = 0,
            .splice_fd_in = 0,
            .addr3 = 0,
            .resv = 0,
        };
        index = pending.node.next;
    }
    while (batch.pending.head != .none) batchDrainReady(batch) catch |err| switch (err) {
        error.Timeout => unreachable, // no timeout
    };
}

fn dirCreateDir(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    permissions: Dir.Permissions,
) Dir.CreateDirError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .MKDIRAT,
            .flags = 0,
            .ioprio = 0,
            .fd = dir.handle,
            .off = 0,
            .addr = @intFromPtr(sub_path_posix.ptr),
            .len = permissions.toMode(),
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
            .ILSEQ => return error.BadPathName,
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
                // stat the file and return an error if it's not a directory
                // this is important because otherwise a dangling symlink
                // could cause an infinite loop
                const kind = try ev.filePathKind(dir, component.path);
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

fn filePathKind(ev: *Evented, dir: Dir, sub_path: []const u8) !File.Kind {
    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        var statx_buf = std.mem.zeroes(linux.Statx);
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .STATX,
            .flags = 0,
            .ioprio = 0,
            .fd = dir.handle,
            .off = @intFromPtr(&statx_buf),
            .addr = @intFromPtr(sub_path_posix.ptr),
            .len = @bitCast(linux.STATX{ .TYPE = true }),
            .rw_flags = linux.AT.NO_AUTOMOUNT | linux.AT.SYMLINK_NOFOLLOW,
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
                if (!statx_buf.mask.TYPE) return error.Unexpected;
                return statxKind(statx_buf.mode);
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

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return .{
        .handle = ev.openat(&cancel_region, dir.handle, sub_path_posix, .{
            .ACCMODE = .RDONLY,
            .DIRECTORY = true,
            .NOFOLLOW = !options.follow_symlinks,
            .CLOEXEC = true,
            .PATH = !options.iterate,
        }, 0) catch |err| switch (err) {
            error.IsDir => return errnoBug(.ISDIR),
            error.WouldBlock => return errnoBug(.AGAIN),
            error.FileTooBig => return errnoBug(.FBIG),
            error.NoSpaceLeft => return errnoBug(.NOSPC),
            error.DeviceBusy => return errnoBug(.BUSY), // EXCL unset.
            error.FileBusy => return errnoBug(.TXTBSY),
            error.PathAlreadyExists => return errnoBug(.EXIST), // Not creating.
            error.OperationUnsupported => return errnoBug(.OPNOTSUPP), // No TMPFILE, no locks.
            else => |e| return e,
        },
    };
}

fn dirStat(userdata: ?*anyopaque, dir: Dir) Dir.StatError!Dir.Stat {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.stat(&cancel_region, dir.handle);
}

fn dirStatFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.StatFileOptions,
) Dir.StatFileError!File.Stat {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);
    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.statx(&cancel_region, dir.handle, sub_path_posix, linux.AT.NO_AUTOMOUNT |
        @as(u32, if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW));
}

fn dirAccess(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    options: Dir.AccessOptions,
) Dir.AccessError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    const mode: u32 =
        @as(u32, if (options.read) linux.R_OK else 0) |
        @as(u32, if (options.write) linux.W_OK else 0) |
        @as(u32, if (options.execute) linux.X_OK else 0);
    const flags: u32 = if (options.follow_symlinks) 0 else linux.AT.SYMLINK_NOFOLLOW;

    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    while (true) {
        try sync.cancel_region.await(.nothing);
        switch (linux.errno(linux.faccessat(dir.handle, sub_path_posix, mode, flags))) {
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
        }
    }
}

fn dirCreateFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    flags: File.CreateFlags,
) File.OpenError!File {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    const fd = ev.openat(&maybe_sync.cancel_region, dir.handle, sub_path_posix, .{
        .ACCMODE = if (flags.read) .RDWR else .WRONLY,
        .CREAT = true,
        .TRUNC = flags.truncate,
        .EXCL = flags.exclusive,
        .CLOEXEC = true,
    }, flags.permissions.toMode()) catch |err| switch (err) {
        error.OperationUnsupported => return error.Unexpected, // TMPFILE unset.
        else => |e| return e,
    };
    errdefer ev.closeAsync(fd);

    switch (flags.lock) {
        .none => {},
        .shared, .exclusive => try ev.flock(
            try maybe_sync.enterSync(ev),
            fd,
            flags.lock,
            if (flags.lock_nonblocking) .nonblocking else .blocking,
        ),
    }

    return .{ .handle = fd, .flags = .{ .nonblocking = false } };
}

fn dirCreateFileAtomic(
    userdata: ?*anyopaque,
    dir: Dir,
    dest_path: []const u8,
    options: Dir.CreateFileAtomicOptions,
) Dir.CreateFileAtomicError!File.Atomic {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    // Linux has O_TMPFILE, but linkat() does not support AT_REPLACE, so it's
    // useless when we have to make up a bogus path name to do the rename()
    // anyway.
    if (!options.replace) tmpfile: {
        const flags: linux.O = if (@hasField(linux.O, "TMPFILE")) .{
            .ACCMODE = .RDWR,
            .TMPFILE = true,
            .DIRECTORY = true,
            .CLOEXEC = true,
        } else if (@hasField(linux.O, "TMPFILE0") and !@hasField(linux.O, "TMPFILE2")) .{
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
            _ = dirCreateDirPath(ev, dir, dirname, .default_dir) catch |err| switch (err) {
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

        var path_buffer: [PATH_MAX]u8 = undefined;
        const sub_path_posix = try pathToPosix(dest_dirname orelse ".", &path_buffer);

        var cancel_region: CancelRegion = .init();
        defer cancel_region.deinit();
        return .{
            .file = .{
                .handle = ev.openat(
                    &cancel_region,
                    dir.handle,
                    sub_path_posix,
                    flags,
                    options.permissions.toMode(),
                ) catch |err| switch (err) {
                    error.IsDir, error.FileNotFound, error.OperationUnsupported => {
                        // Ambiguous error code. It might mean the file system
                        // does not support O_TMPFILE. Therefore, we must fall
                        // back to not using O_TMPFILE.
                        break :tmpfile;
                    },
                    error.FileTooBig => return errnoBug(.FBIG),
                    error.DeviceBusy => return errnoBug(.BUSY), // O_EXCL not passed
                    error.PathAlreadyExists => return errnoBug(.EXIST), // Not creating.
                    else => |e| return e,
                },
                .flags = .{ .nonblocking = false },
            },
            .file_basename_hex = 0,
            .dest_sub_path = dest_path,
            .file_open = true,
            .file_exists = false,
            .close_dir_on_deinit = false,
            .dir = dir,
        };
    }

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

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    const fd = ev.openat(&maybe_sync.cancel_region, dir.handle, sub_path_posix, .{
        .ACCMODE = switch (flags.mode) {
            .read_only => .RDONLY,
            .write_only => .WRONLY,
            .read_write => .RDWR,
        },
        .NOCTTY = !flags.allow_ctty,
        .NOFOLLOW = !flags.follow_symlinks,
        .CLOEXEC = true,
        .PATH = flags.path_only,
    }, 0) catch |err| switch (err) {
        error.OperationUnsupported => return error.Unexpected, // TMPFILE unset.
        else => |e| return e,
    };
    errdefer ev.closeAsync(fd);

    if (!flags.allow_directory) {
        const is_dir = is_dir: {
            const s = ev.stat(&maybe_sync.cancel_region, fd) catch |err| switch (err) {
                // The directory-ness is either unknown or unknowable
                error.Streaming => break :is_dir false,
                else => |e| return e,
            };
            break :is_dir s.kind == .directory;
        };
        if (is_dir) return error.IsDir;
    }

    switch (flags.lock) {
        .none => {},
        .shared, .exclusive => try ev.flock(
            try maybe_sync.enterSync(ev),
            fd,
            flags.lock,
            if (flags.lock_nonblocking) .nonblocking else .blocking,
        ),
    }

    return .{ .handle = fd, .flags = .{ .nonblocking = false } };
}

fn dirClose(userdata: ?*anyopaque, dirs: []const Dir) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    for (dirs) |dir| ev.close(dir.handle);
}

fn dirRead(userdata: ?*anyopaque, dr: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var buffer_index: usize = 0;
    while (buffer.len - buffer_index != 0) {
        if (dr.end - dr.index == 0) {
            // Refill the buffer, unless we've already created references to
            // buffered data.
            if (buffer_index != 0) break;
            var sync: CancelRegion.Sync = try .init(ev);
            defer sync.deinit(ev);
            if (dr.state == .reset) {
                ev.lseek(&sync, dr.dir.handle, 0, linux.SEEK.SET) catch |err| switch (err) {
                    error.Unseekable => return error.Unexpected,
                    else => |e| return e,
                };
                dr.state = .reading;
            }
            const n = while (true) {
                try sync.cancel_region.await(.nothing);
                const rc = linux.getdents64(dr.dir.handle, dr.buffer.ptr, dr.buffer.len);
                switch (linux.errno(rc)) {
                    .SUCCESS => break rc,
                    .INTR => {},
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
                    else => |err| return unexpectedErrno(err),
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

fn dirRealPath(userdata: ?*anyopaque, dir: Dir, out_buffer: []u8) Dir.RealPathError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    var sync: CancelRegion.Sync = try .init(ev);
    defer sync.deinit(ev);
    return ev.realPath(&sync, dir.handle, out_buffer);
}

fn dirRealPathFile(
    userdata: ?*anyopaque,
    dir: Dir,
    sub_path: []const u8,
    out_buffer: []u8,
) Dir.RealPathFileError!usize {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var maybe_sync: CancelRegion.Sync.Maybe = .{ .cancel_region = .init() };
    defer maybe_sync.deinit(ev);
    const fd = ev.openat(&maybe_sync.cancel_region, dir.handle, sub_path_posix, .{
        .CLOEXEC = true,
        .PATH = true,
    }, 0) catch |err| switch (err) {
        error.WouldBlock => return errnoBug(.AGAIN),
        error.OperationUnsupported => return errnoBug(.OPNOTSUPP), // Not asking for locks.
        else => |e| return e,
    };
    defer ev.closeAsync(fd);
    return ev.realPath(try maybe_sync.enterSync(ev), fd, out_buffer);
}

fn dirDeleteFile(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteFileError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .UNLINKAT,
            .flags = 0,
            .ioprio = 0,
            .fd = dir.handle,
            .off = 0,
            .addr = @intFromPtr(sub_path_posix.ptr),
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
            .PERM => return error.PermissionDenied,
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
        }
    }
}

fn dirDeleteDir(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteDirError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var path_buffer: [PATH_MAX]u8 = undefined;
    const sub_path_posix = try pathToPosix(sub_path, &path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .UNLINKAT,
            .flags = 0,
            .ioprio = 0,
            .fd = dir.handle,
            .off = 0,
            .addr = @intFromPtr(sub_path_posix.ptr),
            .len = 0,
            .rw_flags = linux.AT.REMOVEDIR,
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
        }
    }
}

fn dirRename(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenameError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var old_path_buffer: [PATH_MAX]u8 = undefined;
    var new_path_buffer: [PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.renameat(
        &cancel_region,
        old_dir.handle,
        old_sub_path_posix,
        new_dir.handle,
        new_sub_path_posix,
        .{},
    );
}

fn dirRenamePreserve(
    userdata: ?*anyopaque,
    old_dir: Dir,
    old_sub_path: []const u8,
    new_dir: Dir,
    new_sub_path: []const u8,
) Dir.RenamePreserveError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));

    var old_path_buffer: [PATH_MAX]u8 = undefined;
    var new_path_buffer: [PATH_MAX]u8 = undefined;

    const old_sub_path_posix = try pathToPosix(old_sub_path, &old_path_buffer);
    const new_sub_path_posix = try pathToPosix(new_sub_path, &new_path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    return ev.renameat(
        &cancel_region,
        old_dir.handle,
        old_sub_path_posix,
        new_dir.handle,
        new_sub_path_posix,
        .{ .NOREPLACE = true },
    );
}

fn dirSymLink(
    userdata: ?*anyopaque,
    dir: Dir,
    target_path: []const u8,
    sym_link_path: []const u8,
    flags: Dir.SymLinkFlags,
) Dir.SymLinkError!void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    _ = flags;

    var target_path_buffer: [PATH_MAX]u8 = undefined;
    var sym_link_path_buffer: [PATH_MAX]u8 = undefined;

    const target_path_posix = try pathToPosix(target_path, &target_path_buffer);
    const sym_link_path_posix = try pathToPosix(sym_link_path, &sym_link_path_buffer);

    var cancel_region: CancelRegion = .init();
    defer cancel_region.deinit();
    while (true) {
        const thread = try cancel_region.awaitIoUring();
        thread.enqueue().* = .{
            .opcode = .SYMLINKAT,
            .flags = 0,
            .ioprio = 0,
            .fd = dir.handle,
            .off = @intFromPtr(sym_link_path_posix.ptr),
            .addr = @intFromPtr(target_path_posix.ptr),
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
            .ROFS => return error.
```
