```
k can return
        /// an allocated resource. For that use case, see `cancel`.
        ///
        /// It is illegal to call `await` or `awaitMany` after this.
        ///
        /// It is safe to call this multiple times.
        ///
        /// Threadsafe.
        pub fn cancelDiscard(s: *S) void {
            const io = s.io;
            const token = s.group.token.load(.acquire) orelse return;
            s.queue.close(io);
            io.vtable.groupCancel(io.userdata, &s.group, token);
            assert(s.group.token.raw == null);
        }
    };
}

/// Atomically checks if the value at `ptr` equals `expected`, and if so, blocks until either:
///
/// * a matching (same `ptr` argument) `futexWake` call occurs, or
/// * a spurious ("random") wakeup occurs.
///
/// Typically, `futexWake` should be called immediately after updating the value at `ptr.*`, to
/// unblock tasks using `futexWait` to wait for the value to change from what it previously was.
///
/// The caller is responsible for identifying spurious wakeups if necessary, typically by checking
/// the value at `ptr.*`.
///
/// Asserts that `T` is 4 bytes in length and has a well-defined layout with no padding bits.
pub fn futexWait(io: Io, comptime T: type, ptr: *align(@alignOf(u32)) const T, expected: T) Cancelable!void {
    return futexWaitTimeout(io, T, ptr, expected, .none);
}
/// Same as `futexWait`, except also unblocks if `timeout` expires. As with `futexWait`, spurious
/// wakeups are possible. It remains the caller's responsibility to differentiate between these
/// three possible wake-up reasons if necessary.
pub fn futexWaitTimeout(io: Io, comptime T: type, ptr: *align(@alignOf(u32)) const T, expected: T, timeout: Timeout) Cancelable!void {
    const expected_int: u32 = switch (@typeInfo(T)) {
        .@"enum" => @bitCast(@intFromEnum(expected)),
        else => @bitCast(expected),
    };
    return io.vtable.futexWait(io.userdata, @ptrCast(ptr), expected_int, timeout);
}
/// Same as `futexWait`, except does not introduce a cancelation point.
///
/// For a description of cancelation and cancelation points, see `Future.cancel`.
pub fn futexWaitUncancelable(io: Io, comptime T: type, ptr: *align(@alignOf(u32)) const T, expected: T) void {
    const expected_int: u32 = switch (@typeInfo(T)) {
        .@"enum" => @bitCast(@intFromEnum(expected)),
        else => @bitCast(expected),
    };
    io.vtable.futexWaitUncancelable(io.userdata, @ptrCast(ptr), expected_int);
}
/// Unblocks pending futex waits on `ptr`, up to a limit of `max_waiters` calls.
pub fn futexWake(io: Io, comptime T: type, ptr: *align(@alignOf(u32)) const T, max_waiters: u32) void {
    comptime assert(@sizeOf(T) == @sizeOf(u32));
    if (max_waiters == 0) return;
    return io.vtable.futexWake(io.userdata, @ptrCast(ptr), max_waiters);
}

/// Mutex is a synchronization primitive which enforces atomic access to a
/// shared region of code known as the "critical section".
///
/// Mutex is an extern struct so that it may be used as a field inside another
/// extern struct.
pub const Mutex = extern struct {
    state: std.atomic.Value(State),

    pub const init: Mutex = .{ .state = .init(.unlocked) };

    pub const State = enum(u32) {
        unlocked,
        locked_once,
        contended,
    };

    pub fn tryLock(m: *Mutex) bool {
        return m.state.cmpxchgStrong(.unlocked, .locked_once, .acquire, .monotonic) == null;
    }

    pub fn lock(m: *Mutex, io: Io) Cancelable!void {
        const initial_state = m.state.cmpxchgStrong(
            .unlocked,
            .locked_once,
            .acquire,
            .monotonic,
        ) orelse {
            @branchHint(.likely);
            return;
        };
        if (initial_state == .contended) {
            try io.futexWait(State, &m.state.raw, .contended);
        }
        while (m.state.swap(.contended, .acquire) != .unlocked) {
            try io.futexWait(State, &m.state.raw, .contended);
        }
    }

    /// Same as `lock`, except does not introduce a cancelation point.
    ///
    /// For a description of cancelation and cancelation points, see `Future.cancel`.
    pub fn lockUncancelable(m: *Mutex, io: Io) void {
        const initial_state = m.state.cmpxchgStrong(
            .unlocked,
            .locked_once,
            .acquire,
            .monotonic,
        ) orelse {
            @branchHint(.likely);
            return;
        };
        if (initial_state == .contended) {
            io.futexWaitUncancelable(State, &m.state.raw, .contended);
        }
        while (m.state.swap(.contended, .acquire) != .unlocked) {
            io.futexWaitUncancelable(State, &m.state.raw, .contended);
        }
    }

    pub fn unlock(m: *Mutex, io: Io) void {
        switch (m.state.swap(.unlocked, .release)) {
            .unlocked => unreachable,
            .locked_once => {},
            .contended => {
                @branchHint(.unlikely);
                io.futexWake(State, &m.state.raw, 1);
            },
        }
    }
};

pub const Condition = struct {
    state: std.atomic.Value(State),
    /// Incremented whenever the condition is signaled
    epoch: std.atomic.Value(u32),

    const State = packed struct(u32) {
        waiters: u16,
        signals: u16,
    };

    pub const init: Condition = .{
        .state = .init(.{ .waiters = 0, .signals = 0 }),
        .epoch = .init(0),
    };

    pub fn wait(cond: *Condition, io: Io, mutex: *Mutex) Cancelable!void {
        try waitInner(cond, io, mutex, false);
    }

    /// Same as `wait`, except does not introduce a cancelation point.
    ///
    /// For a description of cancelation and cancelation points, see `Future.cancel`.
    pub fn waitUncancelable(cond: *Condition, io: Io, mutex: *Mutex) void {
        waitInner(cond, io, mutex, true) catch |err| switch (err) {
            error.Canceled => unreachable,
        };
    }

    fn waitInner(cond: *Condition, io: Io, mutex: *Mutex, uncancelable: bool) Cancelable!void {
        var epoch = cond.epoch.load(.acquire); // `.acquire` to ensure ordered before state load

        {
            const prev_state = cond.state.fetchAdd(.{ .waiters = 1, .signals = 0 }, .monotonic);
            assert(prev_state.waiters < math.maxInt(u16)); // overflow caused by too many waiters
        }

        mutex.unlock(io);
        defer mutex.lockUncancelable(io);

        while (true) {
            const result = if (uncancelable)
                io.futexWaitUncancelable(u32, &cond.epoch.raw, epoch)
            else
                io.futexWait(u32, &cond.epoch.raw, epoch);

            epoch = cond.epoch.load(.acquire); // `.acquire` to ensure ordered before `state` laod

            // Even on error, try to consume a pending signal first. Otherwise a race might
            // cause a signal to get stuck in the state with no corresponding waiter.
            {
                var prev_state = cond.state.load(.monotonic);
                while (prev_state.signals > 0) {
                    prev_state = cond.state.cmpxchgWeak(prev_state, .{
                        .waiters = prev_state.waiters - 1,
                        .signals = prev_state.signals - 1,
                    }, .acquire, .monotonic) orelse {
                        // We successfully consumed a signal.
                        return;
                    };
                }
            }

            // There are no more signals available; this was a spurious wakeup or an error. If it
            // was an error, we will remove ourselves as a waiter and return that error. Otherwise,
            // we'll loop back to the futex wait.
            result catch |err| {
                const prev_state = cond.state.fetchSub(.{ .waiters = 1, .signals = 0 }, .monotonic);
                assert(prev_state.waiters > 0); // underflow caused by illegal state
                return err;
            };
        }
    }

    pub fn signal(cond: *Condition, io: Io) void {
        var prev_state = cond.state.load(.monotonic);
        while (prev_state.waiters > prev_state.signals) {
            @branchHint(.unlikely);
            prev_state = cond.state.cmpxchgWeak(prev_state, .{
                .waiters = prev_state.waiters,
                .signals = prev_state.signals + 1,
            }, .release, .monotonic) orelse {
                // Update the epoch to tell the waiting threads that there are new signals for them.
                // Note that a waiting thread could miss a take if *exactly* (1<<32)-1 wakes happen
                // between it observing the epoch and sleeping on it, but this is extraordinarily
                // unlikely due to the precise number of calls required.
                _ = cond.epoch.fetchAdd(1, .release); // `.release` to ensure ordered after `state` update
                io.futexWake(u32, &cond.epoch.raw, 1);
                return;
            };
        }
    }

    pub fn broadcast(cond: *Condition, io: Io) void {
        var prev_state = cond.state.load(.monotonic);
        while (prev_state.waiters > prev_state.signals) {
            @branchHint(.unlikely);
            prev_state = cond.state.cmpxchgWeak(prev_state, .{
                .waiters = prev_state.waiters,
                .signals = prev_state.waiters,
            }, .release, .monotonic) orelse {
                // Update the epoch to tell the waiting threads that there are new signals for them.
                // Note that a waiting thread could miss a take if *exactly* (1<<32)-1 wakes happen
                // between it observing the epoch and sleeping on it, but this is extraordinarily
                // unlikely due to the precise number of calls required.
                _ = cond.epoch.fetchAdd(1, .release); // `.release` to ensure ordered after `state` update
                io.futexWake(u32, &cond.epoch.raw, prev_state.waiters - prev_state.signals);
                return;
            };
        }
    }
};

/// Logical boolean flag which can be set and unset and supports a "wait until set" operation.
pub const Event = enum(u32) {
    unset,
    waiting,
    is_set,

    /// Returns whether the logical boolean is `true`.
    pub fn isSet(event: *const Event) bool {
        return switch (@atomicLoad(Event, event, .acquire)) {
            .unset, .waiting => false,
            .is_set => true,
        };
    }

    /// Blocks until the logical boolean is `true`.
    pub fn wait(event: *Event, io: Io) Cancelable!void {
        if (@cmpxchgStrong(Event, event, .unset, .waiting, .acquire, .acquire)) |prev| switch (prev) {
            .unset => unreachable,
            .waiting => {},
            .is_set => return,
        };
        errdefer {
            // Ideally we would restore the event back to `.unset` instead of `.waiting`, but there
            // might be other threads waiting on the event. In theory we could track the *number* of
            // waiting threads in the unused bits of the `Event`, but that has its own problem: the
            // waiters would wake up when a *new waiter* was added. So it's easiest to just leave
            // the state at `.waiting`---at worst it causes one redundant call to `futexWake`.
        }
        while (true) {
            try io.futexWait(Event, event, .waiting);
            switch (@atomicLoad(Event, event, .acquire)) {
                .unset => unreachable, // `reset` called before pending `wait` returned
                .waiting => continue,
                .is_set => return,
            }
        }
    }

    /// Same as `wait`, except does not introduce a cancelation point.
    ///
    /// For a description of cancelation and cancelation points, see `Future.cancel`.
    pub fn waitUncancelable(event: *Event, io: Io) void {
        if (@cmpxchgStrong(Event, event, .unset, .waiting, .acquire, .acquire)) |prev| switch (prev) {
            .unset => unreachable,
            .waiting => {},
            .is_set => return,
        };
        while (true) {
            io.futexWaitUncancelable(Event, event, .waiting);
            switch (@atomicLoad(Event, event, .acquire)) {
                .unset => unreachable, // `reset` called before pending `wait` returned
                .waiting => continue,
                .is_set => return,
            }
        }
    }

    pub const WaitTimeoutError = error{Timeout} || Cancelable;

    /// Blocks the calling thread until either the logical boolean is set, the timeout expires, or a
    /// spurious wakeup occurs. If the timeout expires or a spurious wakeup occurs, `error.Timeout`
    /// is returned.
    pub fn waitTimeout(event: *Event, io: Io, timeout: Timeout) WaitTimeoutError!void {
        if (@cmpxchgStrong(Event, event, .unset, .waiting, .acquire, .acquire)) |prev| switch (prev) {
            .unset => unreachable,
            .waiting => {},
            .is_set => return,
        };
        errdefer {
            // Ideally we would restore the event back to `.unset` instead of `.waiting`, but there
            // might be other threads waiting on the event. In theory we could track the *number* of
            // waiting threads in the unused bits of the `Event`, but that has its own problem: the
            // waiters would wake up when a *new waiter* was added. So it's easiest to just leave
            // the state at `.waiting`---at worst it causes one redundant call to `futexWake`.
        }
        try io.futexWaitTimeout(Event, event, .waiting, timeout);
        switch (@atomicLoad(Event, event, .acquire)) {
            .unset => unreachable, // `reset` called before pending `wait` returned
            .waiting => return error.Timeout,
            .is_set => return,
        }
    }

    /// Sets the logical boolean to true, and hence unblocks any pending calls to `wait`. The
    /// logical boolean remains true until `reset` is called, so future calls to `set` have no
    /// semantic effect.
    ///
    /// Any memory accesses prior to a `set` call are "released", so that if this `set` call causes
    /// `isSet` to return `true` or a wait to finish, those tasks will be able to observe those
    /// memory accesses.
    pub fn set(e: *Event, io: Io) void {
        switch (@atomicRmw(Event, e, .Xchg, .is_set, .release)) {
            .unset, .is_set => {},
            .waiting => io.futexWake(Event, e, math.maxInt(u32)),
        }
    }

    /// Sets the logical boolean to false.
    ///
    /// Assumes that there is no pending call to `wait` or `waitUncancelable`.
    ///
    /// However, concurrent calls to `isSet`, `set`, and `reset` are allowed.
    pub fn reset(e: *Event) void {
        @atomicStore(Event, e, .unset, .monotonic);
    }
};

pub const QueueClosedError = error{Closed};

pub const TypeErasedQueue = struct {
    mutex: Mutex,
    closed: bool,

    /// Ring buffer. This data is logically *after* queued getters.
    buffer: []u8,
    start: usize,
    len: usize,

    putters: std.DoublyLinkedList,
    getters: std.DoublyLinkedList,

    const Put = struct {
        remaining: []const u8,
        needed: usize,
        condition: Condition,
        node: std.DoublyLinkedList.Node,
    };

    const Get = struct {
        remaining: []u8,
        needed: usize,
        condition: Condition,
        node: std.DoublyLinkedList.Node,
    };

    pub fn init(buffer: []u8) TypeErasedQueue {
        return .{
            .mutex = .init,
            .closed = false,
            .buffer = buffer,
            .start = 0,
            .len = 0,
            .putters = .{},
            .getters = .{},
        };
    }

    /// After this is called, the queue enters a "closed" state. A closed
    /// queue always returns `error.Closed` for put attempts even when
    /// there is space in the buffer. However, existing elements of the
    /// queue are retrieved before `error.Closed` is returned.
    ///
    /// Idempotent. Threadsafe.
    pub fn close(q: *TypeErasedQueue, io: Io) void {
        q.mutex.lockUncancelable(io);
        defer q.mutex.unlock(io);
        q.closed = true;
        {
            var it = q.getters.first;
            while (it) |node| : (it = node.next) {
                const getter: *Get = @alignCast(@fieldParentPtr("node", node));
                getter.condition.signal(io);
            }
        }
        {
            var it = q.putters.first;
            while (it) |node| : (it = node.next) {
                const putter: *Put = @alignCast(@fieldParentPtr("node", node));
                putter.condition.signal(io);
            }
        }
    }

    pub fn put(q: *TypeErasedQueue, io: Io, elements: []const u8, min: usize) (QueueClosedError || Cancelable)!usize {
        assert(elements.len >= min);
        if (elements.len == 0) return 0;
        try q.mutex.lock(io);
        defer q.mutex.unlock(io);
        return q.putLocked(io, elements, min, false);
    }

    /// Same as `put`, except does not introduce a cancelation point.
    ///
    /// For a description of cancelation and cancelation points, see `Future.cancel`.
    pub fn putUncancelable(q: *TypeErasedQueue, io: Io, elements: []const u8, min: usize) QueueClosedError!usize {
        assert(elements.len >= min);
        if (elements.len == 0) return 0;
        q.mutex.lockUncancelable(io);
        defer q.mutex.unlock(io);
        return q.putLocked(io, elements, min, true) catch |err| switch (err) {
            error.Canceled => unreachable,
            error.Closed => |e| return e,
        };
    }

    fn puttableSlice(q: *const TypeErasedQueue) ?[]u8 {
        const unwrapped_index = q.start + q.len;
        const wrapped_index, const overflow = @subWithOverflow(unwrapped_index, q.buffer.len);
        const slice = switch (overflow) {
            1 => q.buffer[unwrapped_index..],
            0 => q.buffer[wrapped_index..q.start],
        };
        return if (slice.len > 0) slice else null;
    }

    fn putLocked(q: *TypeErasedQueue, io: Io, elements: []const u8, min: usize, uncancelable: bool) (QueueClosedError || Cancelable)!usize {
        // A closed queue cannot be added to, even if there is space in the buffer.
        if (q.closed) return error.Closed;

        // Getters have first priority on the data, and only when the getters
        // queue is empty do we start populating the buffer.

        // The number of elements we add immediately, before possibly blocking.
        var n: usize = 0;

        while (q.getters.popFirst()) |getter_node| {
            const getter: *Get = @alignCast(@fieldParentPtr("node", getter_node));
            const copy_len = @min(getter.remaining.len, elements.len - n);
            assert(copy_len > 0);
            @memcpy(getter.remaining[0..copy_len], elements[n..][0..copy_len]);
            getter.remaining = getter.remaining[copy_len..];
            getter.needed -|= copy_len;
            n += copy_len;
            if (getter.needed == 0) {
                getter.condition.signal(io);
            } else {
                assert(n == elements.len); // we didn't have enough elements for the getter
                q.getters.prepend(getter_node);
            }
            if (n == elements.len) return elements.len;
        }

        while (q.puttableSlice()) |slice| {
            const copy_len = @min(slice.len, elements.len - n);
            assert(copy_len > 0);
            @memcpy(slice[0..copy_len], elements[n..][0..copy_len]);
            q.len += copy_len;
            n += copy_len;
            if (n == elements.len) return elements.len;
        }

        // Don't block if we hit the min.
        if (n >= min) return n;

        var pending: Put = .{
            .remaining = elements[n..],
            .needed = min - n,
            .condition = .init,
            .node = .{},
        };
        q.putters.append(&pending.node);
        defer if (pending.needed > 0) q.putters.remove(&pending.node);

        while (pending.needed > 0 and !q.closed) {
            if (uncancelable) {
                pending.condition.waitUncancelable(io, &q.mutex);
                continue;
            }
            pending.condition.wait(io, &q.mutex) catch |err| switch (err) {
                error.Canceled => if (pending.remaining.len == elements.len) {
                    // Canceled while waiting, and appended no elements.
                    return error.Canceled;
                } else {
                    // Canceled while waiting, but appended some elements, so report those first.
                    io.recancel();
                    return elements.len - pending.remaining.len;
                },
            };
        }
        if (pending.remaining.len == elements.len) {
            // The queue was closed while we were waiting. We appended no elements.
            assert(q.closed);
            return error.Closed;
        }
        return elements.len - pending.remaining.len;
    }

    pub fn get(q: *TypeErasedQueue, io: Io, buffer: []u8, min: usize) (QueueClosedError || Cancelable)!usize {
        assert(buffer.len >= min);
        if (buffer.len == 0) return 0;
        try q.mutex.lock(io);
        defer q.mutex.unlock(io);
        return q.getLocked(io, buffer, min, false);
    }

    /// Same as `get`, except does not introduce a cancelation point.
    ///
    /// For a description of cancelation and cancelation points, see `Future.cancel`.
    pub fn getUncancelable(q: *TypeErasedQueue, io: Io, buffer: []u8, min: usize) QueueClosedError!usize {
        assert(buffer.len >= min);
        if (buffer.len == 0) return 0;
        q.mutex.lockUncancelable(io);
        defer q.mutex.unlock(io);
        return q.getLocked(io, buffer, min, true) catch |err| switch (err) {
            error.Canceled => unreachable,
            error.Closed => |e| return e,
        };
    }

    fn gettableSlice(q: *const TypeErasedQueue) ?[]const u8 {
        const overlong_slice = q.buffer[q.start..];
        const slice = overlong_slice[0..@min(overlong_slice.len, q.len)];
        return if (slice.len > 0) slice else null;
    }

    fn getLocked(q: *TypeErasedQueue, io: Io, buffer: []u8, min: usize, uncancelable: bool) (QueueClosedError || Cancelable)!usize {
        // The ring buffer gets first priority, then data should come from any
        // queued putters, then finally the ring buffer should be filled with
        // data from putters so they can be resumed.

        // The number of elements we received immediately, before possibly blocking.
        var n: usize = 0;

        while (q.gettableSlice()) |slice| {
            const copy_len = @min(slice.len, buffer.len - n);
            assert(copy_len > 0);
            @memcpy(buffer[n..][0..copy_len], slice[0..copy_len]);
            q.start += copy_len;
            if (q.buffer.len - q.start == 0) q.start = 0;
            q.len -= copy_len;
            n += copy_len;
            if (n == buffer.len) {
                q.fillRingBufferFromPutters(io);
                return buffer.len;
            }
        }

        // Copy directly from putters into buffer.
        while (q.putters.popFirst()) |putter_node| {
            const putter: *Put = @alignCast(@fieldParentPtr("node", putter_node));
            const copy_len = @min(putter.remaining.len, buffer.len - n);
            assert(copy_len > 0);
            @memcpy(buffer[n..][0..copy_len], putter.remaining[0..copy_len]);
            putter.remaining = putter.remaining[copy_len..];
            putter.needed -|= copy_len;
            n += copy_len;
            if (putter.needed == 0) {
                putter.condition.signal(io);
            } else {
                assert(n == buffer.len); // we didn't have enough space for the putter
                q.putters.prepend(putter_node);
            }
            if (n == buffer.len) {
                q.fillRingBufferFromPutters(io);
                return buffer.len;
            }
        }

        // No need to call `fillRingBufferFromPutters` from this point onwards,
        // because we emptied the ring buffer *and* the putter queue!

        // Don't block if we hit the min or if the queue is closed. Return how
        // many elements we could get immediately, unless the queue was closed and
        // empty, in which case report `error.Closed`.
        if (n == 0 and q.closed) return error.Closed;
        if (n >= min or q.closed) return n;

        var pending: Get = .{
            .remaining = buffer[n..],
            .needed = min - n,
            .condition = .init,
            .node = .{},
        };
        q.getters.append(&pending.node);
        defer if (pending.needed > 0) q.getters.remove(&pending.node);

        while (pending.needed > 0 and !q.closed) {
            if (uncancelable) {
                pending.condition.waitUncancelable(io, &q.mutex);
                continue;
            }
            pending.condition.wait(io, &q.mutex) catch |err| switch (err) {
                error.Canceled => if (pending.remaining.len == buffer.len) {
                    // Canceled while waiting, and received no elements.
                    return error.Canceled;
                } else {
                    // Canceled while waiting, but received some elements, so report those first.
                    io.recancel();
                    return buffer.len - pending.remaining.len;
                },
            };
        }
        if (pending.remaining.len == buffer.len) {
            // The queue was closed while we were waiting. We received no elements.
            assert(q.closed);
            return error.Closed;
        }
        return buffer.len - pending.remaining.len;
    }

    /// Called when there is nonzero space available in the ring buffer and
    /// potentially putters waiting. The mutex is already held and the task is
    /// to copy putter data to the ring buffer and signal any putters whose
    /// buffers been fully copied.
    fn fillRingBufferFromPutters(q: *TypeErasedQueue, io: Io) void {
        while (q.putters.popFirst()) |putter_node| {
            const putter: *Put = @alignCast(@fieldParentPtr("node", putter_node));
            while (q.puttableSlice()) |slice| {
                const copy_len = @min(slice.len, putter.remaining.len);
                assert(copy_len > 0);
                @memcpy(slice[0..copy_len], putter.remaining[0..copy_len]);
                q.len += copy_len;
                putter.remaining = putter.remaining[copy_len..];
                putter.needed -|= copy_len;
                if (putter.needed == 0) {
                    putter.condition.signal(io);
                    break;
                }
            } else {
                q.putters.prepend(putter_node);
                break;
            }
        }
    }
};

/// Many producer, many consumer, thread-safe, runtime configurable buffer size.
/// When buffer is empty, consumers suspend and are resumed by producers.
/// When buffer is full, producers suspend and are resumed by consumers.
pub fn Queue(Elem: type) type {
    return struct {
        type_erased: TypeErasedQueue,

        pub fn init(buffer: []Elem) @This() {
            return .{ .type_erased = .init(@ptrCast(buffer)) };
        }

        /// After this is called, the queue enters a "closed" state. A closed
        /// queue always returns `error.Closed` for put attempts even when
        /// there is space in the buffer. However, existing elements of the
        /// queue are retrieved before `error.Closed` is returned.
        ///
        /// Threadsafe.
        pub fn close(q: *@This(), io: Io) void {
            q.type_erased.close(io);
        }

        /// Appends elements to the end of the queue, potentially blocking if
        /// there is insufficient capacity. Returns when any one of the
        /// following conditions is satisfied:
        ///
        /// * At least `min` elements have been added to the queue
        /// * The queue is closed
        /// * The current task is canceled
        ///
        /// Returns how many of `elements` have been added to the queue, if any.
        /// If an error is returned, no elements have been added.
        ///
        /// If the queue is closed or the task is canceled, but some items were
        /// already added before the closure or cancelation, then `put` may
        /// return a number lower than `min`, in which case future calls are
        /// guaranteed to return `error.Canceled` or `error.Closed`.
        ///
        /// A return value of 0 is only possible if `min` is 0, in which case
        /// the call is guaranteed to queue as many of `elements` as is possible
        /// *without* blocking.
        ///
        /// Asserts that `elements.len >= min`.
        pub fn put(q: *@This(), io: Io, elements: []const Elem, min: usize) (QueueClosedError || Cancelable)!usize {
            return @divExact(try q.type_erased.put(io, @ptrCast(elements), min * @sizeOf(Elem)), @sizeOf(Elem));
        }

        /// Same as `put` but blocks until all elements have been added to the queue.
        ///
        /// If the queue is closed or canceled, `error.Closed` or `error.Canceled`
        /// is returned, and it is unspecified how many, if any, of `elements` were
        /// added to the queue prior to cancelation or closure.
        pub fn putAll(q: *@This(), io: Io, elements: []const Elem) (QueueClosedError || Cancelable)!void {
            const n = try q.put(io, elements, elements.len);
            if (n != elements.len) {
                _ = try q.put(io, elements[n..], elements.len - n);
                unreachable; // partial `put` implies queue was closed or we were canceled
            }
        }

        /// Same as `put`, except does not introduce a cancelation point.
        ///
        /// For a description of cancelation and cancelation points, see `Future.cancel`.
        pub fn putUncancelable(q: *@This(), io: Io, elements: []const Elem, min: usize) QueueClosedError!usize {
            return @divExact(try q.type_erased.putUncancelable(io, @ptrCast(elements), min * @sizeOf(Elem)), @sizeOf(Elem));
        }

        /// Appends `item` to the end of the queue, blocking if the queue is full.
        pub fn putOne(q: *@This(), io: Io, item: Elem) (QueueClosedError || Cancelable)!void {
            assert(try q.put(io, &.{item}, 1) == 1);
        }

        /// Same as `putOne`, except does not introduce a cancelation point.
        ///
        /// For a description of cancelation and cancelation points, see `Future.cancel`.
        pub fn putOneUncancelable(q: *@This(), io: Io, item: Elem) QueueClosedError!void {
            assert(try q.putUncancelable(io, &.{item}, 1) == 1);
        }

        /// Receives elements from the beginning of the queue, potentially blocking
        /// if there are insufficient elements currently in the queue. Returns when
        /// any one of the following conditions is satisfied:
        ///
        /// * At least `min` elements have been received from the queue
        /// * The queue is closed and contains no buffered elements
        /// * The current task is canceled
        ///
        /// Returns how many elements of `buffer` have been populated, if any.
        /// If an error is returned, no elements have been populated.
        ///
        /// If the queue is closed or the task is canceled, but some items were
        /// already received before the closure or cancelation, then `get` may
        /// return a number lower than `min`, in which case future calls are
        /// guaranteed to return `error.Canceled` or `error.Closed`.
        ///
        /// A return value of 0 is only possible if `min` is 0, in which case
        /// the call is guaranteed to fill as much of `buffer` as is possible
        /// *without* blocking.
        ///
        /// Asserts that `buffer.len >= min`.
        pub fn get(q: *@This(), io: Io, buffer: []Elem, min: usize) (QueueClosedError || Cancelable)!usize {
            return @divExact(try q.type_erased.get(io, @ptrCast(buffer), min * @sizeOf(Elem)), @sizeOf(Elem));
        }

        /// Same as `get`, except does not introduce a cancelation point.
        ///
        /// For a description of cancelation and cancelation points, see `Future.cancel`.
        pub fn getUncancelable(q: *@This(), io: Io, buffer: []Elem, min: usize) QueueClosedError!usize {
            return @divExact(try q.type_erased.getUncancelable(io, @ptrCast(buffer), min * @sizeOf(Elem)), @sizeOf(Elem));
        }

        /// Receives one element from the beginning of the queue, blocking if the queue is empty.
        pub fn getOne(q: *@This(), io: Io) (QueueClosedError || Cancelable)!Elem {
            var buf: [1]Elem = undefined;
            assert(try q.get(io, &buf, 1) == 1);
            return buf[0];
        }

        /// Same as `getOne`, except does not introduce a cancelation point.
        ///
        /// For a description of cancelation and cancelation points, see `Future.cancel`.
        pub fn getOneUncancelable(q: *@This(), io: Io) QueueClosedError!Elem {
            var buf: [1]Elem = undefined;
            assert(try q.getUncancelable(io, &buf, 1) == 1);
            return buf[0];
        }

        /// Returns buffer length in `Elem` units.
        pub fn capacity(q: *const @This()) usize {
            return @divExact(q.type_erased.buffer.len, @sizeOf(Elem));
        }
    };
}

/// Calls `function` with `args`, such that the return value of the function is
/// not guaranteed to be available until `await` is called.
///
/// `function` *may* be called immediately, before `async` returns. This has
/// weaker guarantees than `concurrent`, making more portable and reusable.
///
/// When this function returns, it is guaranteed that `function` has already
/// been called and completed, or it has successfully been assigned a unit of
/// concurrency.
///
/// See also:
/// * `Group`
pub fn async(
    io: Io,
    function: anytype,
    args: std.meta.ArgsTuple(@TypeOf(function)),
) Future(@typeInfo(@TypeOf(function)).@"fn".return_type.?) {
    const Result = @typeInfo(@TypeOf(function)).@"fn".return_type.?;
    const Args = @TypeOf(args);
    const TypeErased = struct {
        fn start(context: *const anyopaque, result: *anyopaque) void {
            const args_casted: *const Args = @ptrCast(@alignCast(context));
            const result_casted: *Result = @ptrCast(@alignCast(result));
            result_casted.* = @call(.auto, function, args_casted.*);
        }
    };
    var future: Future(Result) = undefined;
    future.any_future = io.vtable.async(
        io.userdata,
        @ptrCast(&future.result),
        .of(Result),
        @ptrCast(&args),
        .of(Args),
        TypeErased.start,
    );
    return future;
}

pub const ConcurrentError = error{
    /// May occur due to a temporary condition such as resource exhaustion, or
    /// to the Io implementation not supporting concurrency.
    ConcurrencyUnavailable,
};

/// Calls `function` with `args`, such that the return value of the function is
/// not guaranteed to be available until `await` is called, allowing the caller
/// to progress while waiting for any `Io` operations.
///
/// This has stronger guarantee than `async`, placing restrictions on what kind
/// of `Io` implementations are supported. By calling `async` instead, one
/// allows, for example, stackful single-threaded blocking I/O.
pub fn concurrent(
    io: Io,
    function: anytype,
    args: std.meta.ArgsTuple(@TypeOf(function)),
) ConcurrentError!Future(@typeInfo(@TypeOf(function)).@"fn".return_type.?) {
    const Result = @typeInfo(@TypeOf(function)).@"fn".return_type.?;
    const Args = @TypeOf(args);
    const TypeErased = struct {
        fn start(context: *const anyopaque, result: *anyopaque) void {
            const args_casted: *const Args = @ptrCast(@alignCast(context));
            const result_casted: *Result = @ptrCast(@alignCast(result));
            result_casted.* = @call(.auto, function, args_casted.*);
        }
    };
    var future: Future(Result) = undefined;
    future.any_future = try io.vtable.concurrent(
        io.userdata,
        @sizeOf(Result),
        .of(Result),
        @ptrCast(&args),
        .of(Args),
        TypeErased.start,
    );
    return future;
}

/// Waits until a specified amount of time has passed on `clock`.
///
/// See also:
/// * `Clock.Duration.sleep`
/// * `Clock.Timestamp.wait`
/// * `Timeout.sleep`
pub fn sleep(io: Io, duration: Duration, clock: Clock) Cancelable!void {
    return io.vtable.sleep(io.userdata, .{ .duration = .{
        .raw = duration,
        .clock = clock,
    } });
}

pub const LockedStderr = struct {
    file_writer: *File.Writer,
    terminal_mode: Terminal.Mode,

    pub fn terminal(ls: LockedStderr) Terminal {
        return .{
            .writer = &ls.file_writer.interface,
            .mode = ls.terminal_mode,
        };
    }

    pub fn clear(ls: LockedStderr, buffer: []u8) Cancelable!void {
        const fw = ls.file_writer;
        std.Progress.clearWrittenWithEscapeCodes(fw) catch |err| switch (err) {
            error.WriteFailed => switch (fw.err.?) {
                error.Canceled => |e| return e,
                else => {},
            },
        };
        fw.interface.flush() catch |err| switch (err) {
            error.WriteFailed => switch (fw.err.?) {
                error.Canceled => |e| return e,
                else => {},
            },
        };
        fw.interface.buffer = buffer;
    }
};

/// For doing application-level writes to the standard error stream.
/// Coordinates also with debug-level writes that are ignorant of Io interface
/// and implementations.
///
/// See also:
/// * `tryLockStderr`
pub fn lockStderr(io: Io, buffer: []u8, terminal_mode: ?Terminal.Mode) Cancelable!LockedStderr {
    const ls = try io.vtable.lockStderr(io.userdata, terminal_mode);
    try ls.clear(buffer);
    return ls;
}

/// Same as `lockStderr` but non-blocking.
pub fn tryLockStderr(io: Io, buffer: []u8, terminal_mode: ?Terminal.Mode) Cancelable!?LockedStderr {
    const ls = (try io.vtable.tryLockStderr(io.userdata, buffer, terminal_mode)) orelse return null;
    try ls.clear(buffer);
    return ls;
}

pub fn unlockStderr(io: Io) void {
    return io.vtable.unlockStderr(io.userdata);
}

/// Obtains entropy from a cryptographically secure pseudo-random number
/// generator.
///
/// The implementation *may* store RNG state in process memory and use it to
/// fill `buffer`.
///
/// The randomness is seeded by `randomSecure`, or a less secure mechanism upon
/// failure.
///
/// Threadsafe.
///
/// See also `randomSecure`.
pub fn random(io: Io, buffer: []u8) void {
    return io.vtable.random(io.userdata, buffer);
}

pub const RandomSecureError = error{EntropyUnavailable} || Cancelable;

/// Obtains cryptographically secure entropy from outside the process.
///
/// Always makes a syscall, or otherwise avoids dependency on process memory,
/// in order to obtain fresh randomness. Does not rely on stored RNG state.
///
/// Does not have any fallback mechanisms; returns `error.EntropyUnavailable`
/// if any problems occur.
///
/// Threadsafe.
///
/// See also `random`.
pub fn randomSecure(io: Io, buffer: []u8) RandomSecureError!void {
    return io.vtable.randomSecure(io.userdata, buffer);
}

test {
    _ = net;
    _ = File;
    _ = Dir;
    _ = Reader;
    _ = Writer;
    _ = Evented;
    _ = Threaded;
    _ = RwLock;
    _ = Semaphore;
    _ = @import("Io/test.zig");
}

/// An implementation of `Io` which simulates a system supporting no `Io` operations.
///
/// This system has the following properties:
/// * Concurrency is unavailable.
/// * The stdio handles are pipes whose remote ends are already closed.
/// * The filesystem is entirely empty, including that the cwd is no longer present.
/// * The filesystem is full, so attempting to create entries always returns `error.NoSpaceLeft`.
/// * No entropy source is supported, so `randomSecure` always returns `error.EntropyUnavailable`, and `random` always returns (fills the buffer) with 0.
/// * No clocks are supported, so `now` and `sleep` always return `error.UnsupportedClock`.
/// * No network is connected, so network operations always return `error.NetworkDown`.
pub const failing: std.Io = .{
    .userdata = null,
    .vtable = &.{
        .crashHandler = noCrashHandler,

        .async = noAsync,
        .concurrent = failingConcurrent,
        .await = unreachableAwait,
        .cancel = unreachableCancel,

        .groupAsync = noGroupAsync,
        .groupConcurrent = failingGroupConcurrent,
        .groupAwait = unreachableGroupAwait,
        .groupCancel = unreachableGroupCancel,

        .recancel = unreachableRecancel,
        .swapCancelProtection = unreachableSwapCancelProtection,
        .checkCancel = unreachableCheckCancel,

        .futexWait = noFutexWait,
        .futexWaitUncancelable = noFutexWaitUncancelable,
        .futexWake = noFutexWake,

        .operate = failingOperate,
        .batchAwaitAsync = unreachableBatchAwaitAsync,
        .batchAwaitConcurrent = unreachableBatchAwaitConcurrent,
        .batchCancel = unreachableBatchCancel,

        .dirCreateDir = failingDirCreateDir,
        .dirCreateDirPath = failingDirCreateDirPath,
        .dirCreateDirPathOpen = failingDirCreateDirPathOpen,
        .dirOpenDir = failingDirOpenDir,
        .dirStat = failingDirStat,
        .dirStatFile = failingDirStatFile,
        .dirAccess = failingDirAccess,
        .dirCreateFile = failingDirCreateFile,
        .dirCreateFileAtomic = failingDirCreateFileAtomic,
        .dirOpenFile = failingDirOpenFile,
        .dirClose = unreachableDirClose,
        .dirRead = noDirRead,
        .dirRealPath = failingDirRealPath,
        .dirRealPathFile = failingDirRealPathFile,
        .dirDeleteFile = failingDirDeleteFile,
        .dirDeleteDir = failingDirDeleteDir,
        .dirRename = failingDirRename,
        .dirRenamePreserve = failingDirRenamePreserve,
        .dirSymLink = failingDirSymLink,
        .dirReadLink = failingDirReadLink,
        .dirSetOwner = failingDirSetOwner,
        .dirSetFileOwner = failingDirSetFileOwner,
        .dirSetPermissions = failingDirSetPermissions,
        .dirSetFilePermissions = failingDirSetFilePermissions,
        .dirSetTimestamps = noDirSetTimestamps,
        .dirHardLink = failingDirHardLink,

        .fileStat = failingFileStat,
        .fileLength = failingFileLength,
        .fileClose = unreachableFileClose,
        .fileWritePositional = failingFileWritePositional,
        .fileWriteFileStreaming = noFileWriteFileStreaming,
        .fileWriteFilePositional = noFileWriteFilePositional,
        .fileReadPositional = failingFileReadPositional,
        .fileSeekBy = failingFileSeekBy,
        .fileSeekTo = failingFileSeekTo,
        .fileSync = failingFileSync,
        .fileIsTty = unreachableFileIsTty,
        .fileEnableAnsiEscapeCodes = unreachableFileEnableAnsiEscapeCodes,
        .fileSupportsAnsiEscapeCodes = unreachableFileSupportsAnsiEscapeCodes,
        .fileSetLength = failingFileSetLength,
        .fileSetOwner = failingFileSetOwner,
        .fileSetPermissions = failingFileSetPermissions,
        .fileSetTimestamps = noFileSetTimestamps,
        .fileLock = failingFileLock,
        .fileTryLock = failingFileTryLock,
        .fileUnlock = unreachableFileUnlock,
        .fileDowngradeLock = failingFileDowngradeLock,
        .fileRealPath = failingFileRealPath,
        .fileHardLink = failingFileHardLink,

        .fileMemoryMapCreate = failingFileMemoryMapCreate,
        .fileMemoryMapDestroy = unreachableFileMemoryMapDestroy,
        .fileMemoryMapSetLength = unreachableFileMemoryMapSetLength,
        .fileMemoryMapRead = unreachableFileMemoryMapRead,
        .fileMemoryMapWrite = unreachableFileMemoryMapWrite,

        .processExecutableOpen = failingProcessExecutableOpen,
        .processExecutablePath = failingProcessExecutablePath,
        .lockStderr = unreachableLockStderr,
        .tryLockStderr = noTryLockStderr,
        .unlockStderr = unreachableUnlockStderr,
        .processCurrentPath = failingProcessCurrentPath,
        .processSetCurrentDir = failingProcessSetCurrentDir,
        .processSetCurrentPath = failingProcessSetCurrentPath,
        .processReplace = failingProcessReplace,
        .processReplacePath = failingProcessReplacePath,
        .processSpawn = failingProcessSpawn,
        .processSpawnPath = failingProcessSpawnPath,
        .childWait = unreachableChildWait,
        .childKill = unreachableChildKill,

        .progressParentFile = failingProgressParentFile,

        .random = noRandom,
        .randomSecure = failingRandomSecure,

        .now = noNow,
        .clockResolution = failingClockResolution,
        .sleep = noSleep,

        .netListenIp = failingNetListenIp,
        .netAccept = failingNetAccept,
        .netBindIp = failingNetBindIp,
        .netConnectIp = failingNetConnectIp,
        .netListenUnix = failingNetListenUnix,
        .netConnectUnix = failingNetConnectUnix,
        .netSocketCreatePair = failingNetSocketCreatePair,
        .netSend = failingNetSend,
        .netRead = failingNetRead,
        .netWrite = failingNetWrite,
        .netWriteFile = failingNetWriteFile,
        .netClose = unreachableNetClose,
        .netShutdown = failingNetShutdown,
        .netInterfaceNameResolve = failingNetInterfaceNameResolve,
        .netInterfaceName = unreachableNetInterfaceName,
        .netLookup = failingNetLookup,
    },
};

pub fn noCrashHandler(userdata: ?*anyopaque) void {
    _ = userdata;
}

pub fn noAsync(userdata: ?*anyopaque, result: []u8, result_alignment: std.mem.Alignment, context: []const u8, context_alignment: std.mem.Alignment, start: *const fn (context: *const anyopaque, result: *anyopaque) void) ?*AnyFuture {
    _ = userdata;
    _ = result_alignment;
    _ = context_alignment;
    start(context.ptr, result.ptr);
    return null;
}

pub fn failingConcurrent(
    userdata: ?*anyopaque,
    result_len: usize,
    result_alignment: std.mem.Alignment,
    context: []const u8,
    context_alignment: std.mem.Alignment,
    start: *const fn (context: *const anyopaque, result: *anyopaque) void,
) ConcurrentError!*AnyFuture {
    _ = userdata;
    _ = result_len;
    _ = result_alignment;
    _ = context;
    _ = context_alignment;
    _ = start;
    return error.ConcurrencyUnavailable;
}

pub fn unreachableAwait(
    userdata: ?*anyopaque,
    any_future: *AnyFuture,
    result: []u8,
    result_alignment: std.mem.Alignment,
) void {
    _ = userdata;
    _ = any_future;
    _ = result;
    _ = result_alignment;
    unreachable;
}

pub fn unreachableCancel(
    userdata: ?*anyopaque,
    any_future: *AnyFuture,
    result: []u8,
    result_alignment: std.mem.Alignment,
) void {
    _ = userdata;
    _ = any_future;
    _ = result;
    _ = result_alignment;
    unreachable;
}

pub fn noGroupAsync(
    userdata: ?*anyopaque,
    group: *Group,
    context: []const u8,
    context_alignment: std.mem.Alignment,
    start: *const fn (context: *const anyopaque) void,
) void {
    _ = userdata;
    _ = group;
    _ = context_alignment;
    start(context.ptr);
}

pub fn failingGroupConcurrent(
    userdata: ?*anyopaque,
    group: *Group,
    context: []const u8,
    context_alignment: std.mem.Alignment,
    start: *const fn (context: *const anyopaque) void,
) ConcurrentError!void {
    _ = userdata;
    _ = group;
    _ = context;
    _ = context_alignment;
    _ = start;
    return error.ConcurrencyUnavailable;
}

pub fn unreachableGroupAwait(userdata: ?*anyopaque, group: *Group, token: *anyopaque) Cancelable!void {
    _ = userdata;
    _ = group;
    _ = token;
    unreachable;
}

pub fn unreachableGroupCancel(userdata: ?*anyopaque, group: *Group, token: *anyopaque) void {
    _ = userdata;
    _ = group;
    _ = token;
    unreachable;
}

pub fn unreachableRecancel(userdata: ?*anyopaque) void {
    _ = userdata;
    unreachable;
}

pub fn unreachableSwapCancelProtection(userdata: ?*anyopaque, new: CancelProtection) CancelProtection {
    _ = userdata;
    _ = new;
    unreachable;
}

pub fn unreachableCheckCancel(userdata: ?*anyopaque) Cancelable!void {
    _ = userdata;
    unreachable;
}

pub fn noFutexWait(userdata: ?*anyopaque, ptr: *const u32, expected: u32, timeout: Timeout) Cancelable!void {
    _ = userdata;
    std.debug.assert(ptr.* == expected or timeout != .none);
}

pub fn noFutexWaitUncancelable(userdata: ?*anyopaque, ptr: *const u32, expected: u32) void {
    _ = userdata;
    std.debug.assert(ptr.* == expected);
}

pub fn noFutexWake(userdata: ?*anyopaque, ptr: *const u32, max_waiters: u32) void {
    _ = userdata;
    _ = ptr;
    _ = max_waiters;
    // no-op
}

pub fn failingOperate(userdata: ?*anyopaque, operation: Operation) Cancelable!Operation.Result {
    _ = userdata;
    return switch (operation) {
        .file_read_streaming => .{ .file_read_streaming = error.InputOutput },
        .file_write_streaming => .{ .file_write_streaming = error.InputOutput },
        .device_io_control => unreachable,
        .net_receive => .{ .net_receive = .{ error.NetworkDown, 0 } },
    };
}

pub fn unreachableBatchAwaitAsync(userdata: ?*anyopaque, b: *Batch) Cancelable!void {
    _ = userdata;
    _ = b;
    unreachable;
}

pub fn unreachableBatchAwaitConcurrent(userdata: ?*anyopaque, b: *Batch, timeout: Timeout) Batch.AwaitConcurrentError!void {
    _ = userdata;
    _ = b;
    _ = timeout;
    unreachable;
}

pub fn unreachableBatchCancel(userdata: ?*anyopaque, b: *Batch) void {
    _ = userdata;
    _ = b;
    unreachable;
}

pub fn failingDirCreateDir(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: Dir.Permissions) Dir.CreateDirError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = permissions;
    return error.NoSpaceLeft;
}

pub fn failingDirCreateDirPath(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: Dir.Permissions) Dir.CreateDirPathError!Dir.CreatePathStatus {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = permissions;
    return error.NoSpaceLeft;
}

pub fn failingDirCreateDirPathOpen(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: Dir.Permissions, options: Dir.OpenOptions) Dir.CreateDirPathOpenError!Dir {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = permissions;
    _ = options;
    return error.NoSpaceLeft;
}

pub fn failingDirOpenDir(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, options: Dir.OpenOptions) Dir.OpenError!Dir {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = options;
    return error.FileNotFound;
}

pub fn failingDirStat(userdata: ?*anyopaque, dir: Dir) Dir.StatError!Dir.Stat {
    _ = userdata;
    _ = dir;
    return error.Streaming;
}

pub fn failingDirStatFile(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, options: Dir.StatFileOptions) Dir.StatFileError!File.Stat {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = options;
    return error.FileNotFound;
}

pub fn failingDirAccess(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, options: Dir.AccessOptions) Dir.AccessError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = options;
    return error.FileNotFound;
}

pub fn failingDirCreateFile(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, options: File.CreateFlags) File.OpenError!File {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = options;
    return error.NoSpaceLeft;
}

pub fn failingDirCreateFileAtomic(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, options: Dir.CreateFileAtomicOptions) Dir.CreateFileAtomicError!File.Atomic {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = options;
    return error.NoSpaceLeft;
}

pub fn failingDirOpenFile(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, flags: File.OpenFlags) File.OpenError!File {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = flags;
    return error.FileNotFound;
}

pub fn unreachableDirClose(userdata: ?*anyopaque, dirs: []const Dir) void {
    _ = userdata;
    _ = dirs;
    unreachable;
}

pub fn noDirRead(userdata: ?*anyopaque, dir_reader: *Dir.Reader, buffer: []Dir.Entry) Dir.Reader.Error!usize {
    _ = userdata;
    _ = dir_reader;
    _ = buffer;
    return 0;
}

pub fn failingDirRealPath(userdata: ?*anyopaque, dir: Dir, out_buffer: []u8) Dir.RealPathError!usize {
    _ = userdata;
    _ = dir;
    _ = out_buffer;
    return error.FileNotFound;
}

pub fn failingDirRealPathFile(userdata: ?*anyopaque, dir: Dir, path_name: []const u8, out_buffer: []u8) Dir.RealPathFileError!usize {
    _ = userdata;
    _ = dir;
    _ = path_name;
    _ = out_buffer;
    return error.FileNotFound;
}

pub fn failingDirDeleteFile(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteFileError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    return error.FileNotFound;
}

pub fn failingDirDeleteDir(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8) Dir.DeleteDirError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    return error.FileNotFound;
}

pub fn failingDirRename(userdata: ?*anyopaque, old_dir: Dir, old_sub_path: []const u8, new_dir: Dir, new_sub_path: []const u8) Dir.RenameError!void {
    _ = userdata;
    _ = old_dir;
    _ = old_sub_path;
    _ = new_dir;
    _ = new_sub_path;
    return error.FileNotFound;
}

pub fn failingDirRenamePreserve(userdata: ?*anyopaque, old_dir: Dir, old_sub_path: []const u8, new_dir: Dir, new_sub_path: []const u8) Dir.RenamePreserveError!void {
    _ = userdata;
    _ = old_dir;
    _ = old_sub_path;
    _ = new_dir;
    _ = new_sub_path;
    return error.FileNotFound;
}

pub fn failingDirSymLink(userdata: ?*anyopaque, dir: Dir, target_path: []const u8, sym_link_path: []const u8, flags: Dir.SymLinkFlags) Dir.SymLinkError!void {
    _ = userdata;
    _ = dir;
    _ = target_path;
    _ = sym_link_path;
    _ = flags;
    return error.FileNotFound;
}

pub fn failingDirReadLink(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, buffer: []u8) Dir.ReadLinkError!usize {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = buffer;
    return error.FileNotFound;
}

pub fn failingDirSetOwner(userdata: ?*anyopaque, dir: Dir, owner: ?File.Uid, group: ?File.Gid) Dir.SetOwnerError!void {
    _ = userdata;
    _ = dir;
    _ = owner;
    _ = group;
    return error.FileNotFound;
}

pub fn failingDirSetFileOwner(userdata: ?*anyopaque, dir: std.Io.Dir, sub_path: []const u8, owner: ?File.Uid, group: ?File.Gid, options: Dir.SetFileOwnerOptions) Dir.SetFileOwnerError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = owner;
    _ = group;
    _ = options;
    return error.FileNotFound;
}

pub fn failingDirSetPermissions(userdata: ?*anyopaque, dir: Dir, permissions: Dir.Permissions) Dir.SetPermissionsError!void {
    _ = userdata;
    _ = dir;
    _ = permissions;
    return error.FileNotFound;
}

pub fn failingDirSetFilePermissions(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, permissions: File.Permissions, options: Dir.SetFilePermissionsOptions) Dir.SetFilePermissionsError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = permissions;
    _ = options;
    return error.FileNotFound;
}

pub fn noDirSetTimestamps(userdata: ?*anyopaque, dir: Dir, sub_path: []const u8, options: Dir.SetTimestampsOptions) Dir.SetTimestampsError!void {
    _ = userdata;
    _ = dir;
    _ = sub_path;
    _ = options;
    // no-op
}

pub fn failingDirHardLink(userdata: ?*anyopaque, old_dir: Dir, old_sub_path: []const u8, new_dir: Dir, new_sub_path: []const u8, options: Dir.HardLinkOptions) Dir.HardLinkError!void {
    _ = userdata;
    _ = old_dir;
    _ = old_sub_path;
    _ = new_dir;
    _ = new_sub_path;
    _ = options;
    return error.FileNotFound;
}

pub fn failingFileStat(userdata: ?*anyopaque, file: File) File.StatError!File.Stat {
    _ = userdata;
    _ = file;
    return error.Streaming;
}

pub fn failingFileLength(userdata: ?*anyopaque, file: File) File.LengthError!u64 {
    _ = userdata;
    _ = file;
    return error.Streaming;
}

pub fn unreachableFileClose(userdata: ?*anyopaque, files: []const File) void {
    _ = userdata;
    _ = files;
    unreachable;
}

pub fn failingFileWritePositional(userdata: ?*anyopaque, file: File, header: []const u8, data: []const []const u8, splat: usize, offset: u64) File.WritePositionalError!usize {
    _ = userdata;
    _ = file;
    _ = header;
    _ = offset;
    for (data[0 .. data.len - 1]) |item| {
        if (item.len > 0) return error.BrokenPipe;
    }
    if (data[data.len - 1].len != 0 and splat != 0) return error.BrokenPipe;
    return 0;
}

pub fn noFileWriteFileStreaming(userdata: ?*anyopaque, file: File, header: []const u8, file_reader: *Io.File.Reader, limit: Io.Limit) File.Writer.WriteFileError!usize {
    _ = userdata;
    _ = file;
    _ = header;
    _ = file_reader;
    _ = limit;
    return error.Unimplemented;
}

pub fn noFileWriteFilePositional(userdata: ?*anyopaque, file: File, header: []const u8, file_reader: *Io.File.Reader, limit: Io.Limit, offset: u64) File.WriteFilePositionalError!usize {
    _ = userdata;
    _ = file;
    _ = header;
    _ = file_reader;
    _ = limit;
    _ = offset;
    return error.Unimplemented;
}

pub fn failingFileReadPositional(userdata: ?*anyopaque, file: File, data: []const []u8, offset: u64) File.ReadPositionalError!usize {
    _ = userdata;
    _ = file;
    _ = offset;
    for (data) |item| {
        if (item.len > 0) return error.InputOutput;
    }
    return 0;
}

pub fn failingFileSeekBy(userdata: ?*anyopaque, file: File, relative_offset: i64) File.SeekError!void {
    _ = userdata;
    _ = file;
    _ = relative_offset;
    return error.Unseekable;
}

pub fn failingFileSeekTo(userdata: ?*anyopaque, file: File, absolute_offset: u64) File.SeekError!void {
    _ = userdata;
    _ = file;
    _ = absolute_offset;
    return error.Unseekable;
}

pub fn failingFileSync(userdata: ?*anyopaque, file: File) File.SyncError!void {
    _ = userdata;
    _ = file;
    return error.NoSpaceLeft;
}

pub fn unreachableFileIsTty(userdata: ?*anyopaque, file: File) Cancelable!bool {
    _ = userdata;
    _ = file;
    unreachable;
}

pub fn unreachableFileEnableAnsiEscapeCodes(userdata: ?*anyopaque, file: File) File.EnableAnsiEscapeCodesError!void {
    _ = userdata;
    _ = file;
    unreachable;
}

pub fn unreachableFileSupportsAnsiEscapeCodes(userdata: ?*anyopaque, file: File) Cancelable!bool {
    _ = userdata;
    _ = file;
    unreachable;
}

pub fn failingFileSetLength(userdata: ?*anyopaque, file: File, length: u64) File.SetLengthError!void {
    _ = userdata;
    _ = file;
    _ = length;
    return error.NonResizable;
}

pub fn failingFileSetOwner(userdata: ?*anyopaque, file: File, owner: ?File.Uid, group: ?File.Gid) File.SetOwnerError!void {
    _ = userdata;
    _ = file;
    _ = owner;
    _ = group;
    return error.FileNotFound;
}

pub fn failingFileSetPermissions(userdata: ?*anyopaque, file: File, permissions: File.Permissions) File.SetPermissionsError!void {
    _ = userdata;
    _ = file;
    _ = permissions;
    return error.FileNotFound;
}

pub fn noFileSetTimestamps(userdata: ?*anyopaque, file: File, options: File.SetTimestampsOptions) File.SetTimestampsError!void {
    _ = userdata;
    _ = file;
    _ = options;
    // no-op
}

pub fn failingFileLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!void {
    _ = userdata;
    _ = file;
    _ = lock;
    return error.FileLocksUnsupported;
}

pub fn failingFileTryLock(userdata: ?*anyopaque, file: File, lock: File.Lock) File.LockError!bool {
    _ = userdata;
    _ = file;
    _ = lock;
    return error.FileLocksUnsupported;
}

pub fn unreachableFileUnlock(userdata: ?*anyopaque, file: File) void {
    _ = userdata;
    _ = file;
    unreachable;
}

pub fn failingFileDowngradeLock(userdata: ?*anyopaque, file: File) File.DowngradeLockError!void {
    _ = userdata;
    _ = file;
    // no-op
}

pub fn failingFileRealPath(userdata: ?*anyopaque, file: File, out_buffer: []u8) File.RealPathError!usize {
    _ = userdata;
    _ = file;
    _ = out_buffer;
    return error.FileNotFound;
}

pub fn failingFileHardLink(userdata: ?*anyopaque, file: File, new_dir: Dir, new_sub_path: []const u8, options: File.HardLinkOptions) File.HardLinkError!void {
    _ = userdata;
    _ = file;
    _ = new_dir;
    _ = new_sub_path;
    _ = options;
    return error.FileNotFound;
}

pub fn failingFileMemoryMapCreate(userdata: ?*anyopaque, file: File, options: File.MemoryMap.CreateOptions) File.MemoryMap.CreateError!File.MemoryMap {
    _ = userdata;
    _ = file;
    _ = options;
    return error.AccessDenied;
}

pub fn unreachableFileMemoryMapDestroy(userdata: ?*anyopaque, mm: *File.MemoryMap) void {
    _ = userdata;
    _ = mm;
    unreachable;
}

pub fn unreachableFileMemoryMapSetLength(userdata: ?*anyopaque, mm: *File.MemoryMap, new_len: usize) File.MemoryMap.SetLengthError!void {
    _ = userdata;
    _ = mm;
    _ = new_len;
    unreachable;
}

pub fn unreachableFileMemoryMapRead(userdata: ?*anyopaque, mm: *File.MemoryMap) File.ReadPositionalError!void {
    _ = userdata;
    _ = mm;
    unreachable;
}

pub fn unreachableFileMemoryMapWrite(userdata: ?*anyopaque, mm: *File.MemoryMap) File.WritePositionalError!void {
    _ = userdata;
    _ = mm;
    unreachable;
}

pub fn failingProcessExecutableOpen(userdata: ?*anyopaque, flags: File.OpenFlags) std.process.OpenExecutableError!File {
    _ = userdata;
    _ = flags;
    return error.FileNotFound;
}

pub fn failingProcessExecutablePath(userdata: ?*anyopaque, buffer: []u8) std.process.ExecutablePathError!usize {
    _ = userdata;
    _ = buffer;
    return error.FileNotFound;
}

pub fn unreachableLockStderr(userdata: ?*anyopaque, terminal_mode: ?Terminal.Mode) Cancelable!LockedStderr {
    _ = userdata;
    _ = terminal_mode;
    unreachable;
}

pub fn noTryLockStderr(userdata: ?*anyopaque, terminal_mode: ?Terminal.Mode) Cancelable!?LockedStderr {
    _ = userdata;
    _ = terminal_mode;
    return null;
}

pub fn unreachableUnlockStderr(userdata: ?*anyopaque) void {
    _ = userdata;
    unreachable;
}

pub fn failingProcessCurrentPath(userdata: ?*anyopaque, buffer: []u8) std.process.CurrentPathError!usize {
    _ = userdata;
    _ = buffer;
    return error.CurrentDirUnlinked;
}

pub fn failingProcessSetCurrentDir(userdata: ?*anyopaque, dir: Dir) std.process.SetCurrentDirError!void {
    _ = userdata;
    _ = dir;
    return error.FileNotFound;
}

pub fn failingProcessSetCurrentPath(userdata: ?*anyopaque, path: []const u8) std.process.SetCurrentPathError!void {
    _ = userdata;
    _ = path;
    return error.FileNotFound;
}

pub fn failingProcessReplace(userdata: ?*anyopaque, options: std.process.ReplaceOptions) std.process.ReplaceError {
    _ = userdata;
    _ = options;
    return error.OperationUnsupported;
}

pub fn failingProcessReplacePath(userdata: ?*anyopaque, dir: Dir, options: std.process.ReplaceOptions) std.process.ReplaceError {
    _ = userdata;
    _ = dir;
    _ = options;
    return error.OperationUnsupported;
}

pub fn failingProcessSpawn(userdata: ?*anyopaque, options: std.process.SpawnOptions) std.process.SpawnError!std.process.Child {
    _ = userdata;
    _ = options;
    return error.OperationUnsupported;
}

pub fn failingProcessSpawnPath(userdata: ?*anyopaque, dir: Dir, options: std.process.SpawnOptions) std.process.SpawnError!std.process.Child {
    _ = userdata;
    _ = dir;
    _ = options;
    return error.OperationUnsupported;
}

pub fn unreachableChildWait(userdata: ?*anyopaque, child: *std.process.Child) std.process.Child.WaitError!std.process.Child.Term {
    _ = userdata;
    _ = child;
    unreachable;
}

pub fn unreachableChildKill(userdata: ?*anyopaque, child: *std.process.Child) void {
    _ = userdata;
    _ = child;
    unreachable;
}

pub fn failingProgressParentFile(userdata: ?*anyopaque) std.Progress.ParentFileError!File {
    _ = userdata;
    return error.UnsupportedOperation;
}

pub fn noRandom(userdata: ?*anyopaque, buffer: []u8) void {
    _ = userdata;
    @memset(buffer, 0);
}

pub fn failingRandomSecure(userdata: ?*anyopaque, buffer: []u8) RandomSecureError!void {
    _ = userdata;
    _ = buffer;
    return error.EntropyUnavailable;
}

pub fn noNow(userdata: ?*anyopaque, clock: Clock) Timestamp {
    _ = userdata;
    _ = clock;
    return .zero;
}

pub fn failingClockResolution(userdata: ?*anyopaque, clock: Clock) Clock.ResolutionError!Duration {
    _ = userdata;
    _ = clock;
    return error.ClockUnavailable;
}

pub fn noSleep(userdata: ?*anyopaque, clock: Timeout) Cancelable!void {
    _ = userdata;
    _ = clock;
}

pub fn failingNetListenIp(userdata: ?*anyopaque, address: *const net.IpAddress, options: net.IpAddress.ListenOptions) net.IpAddress.ListenError!net.Socket {
    _ = userdata;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

pub fn failingNetAccept(userdata: ?*anyopaque, listen_fd: net.Socket.Handle, options: net.Server.AcceptOptions) net.Server.AcceptError!net.Socket {
    _ = userdata;
    _ = listen_fd;
    _ = options;
    return error.NetworkDown;
}

pub fn failingNetBindIp(userdata: ?*anyopaque, address: *const net.IpAddress, options: net.IpAddress.BindOptions) net.IpAddress.BindError!net.Socket {
    _ = userdata;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

pub fn failingNetConnectIp(userdata: ?*anyopaque, address: *const net.IpAddress, options: net.IpAddress.ConnectOptions) net.IpAddress.ConnectError!net.Socket {
    _ = userdata;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

pub fn failingNetListenUnix(userdata: ?*anyopaque, address: *const net.UnixAddress, options: net.UnixAddress.ListenOptions) net.UnixAddress.ListenError!net.Socket.Handle {
    _ = userdata;
    _ = address;
    _ = options;
    return error.NetworkDown;
}

pub fn failingNetConnectUnix(userdata: ?*anyopaque, address: *const net.UnixAddress) net.UnixAddress.ConnectError!net.Socket.Handle {
    _ = userdata;
    _ = address;
    return error.NetworkDown;
}

pub fn failingNetSocketCreatePair(userdata: ?*anyopaque, options: net.Socket.CreatePairOptions) net.Socket.CreatePairError![2]net.Socket {
    _ = userdata;
    _ = options;
    return error.OperationUnsupported;
}

pub fn failingNetSend(userdata: ?*anyopaque, handle: net.Socket.Handle, messages: []net.OutgoingMessage, flags: net.SendFlags) struct { ?net.Socket.SendError, usize } {
    _ = userdata;
    _ = handle;
    _ = messages;
    _ = flags;
    return .{ error.NetworkDown, 0 };
}

pub fn failingNetRead(userdata: ?*anyopaque, src: net.Socket.Handle, data: [][]u8) net.Stream.Reader.Error!usize {
    _ = userdata;
    _ = src;
    _ = data;
    return error.NetworkDown;
}

pub fn failingNetWrite(userdata: ?*anyopaque, dest: net.Socket.Handle, header: []const u8, data: []const []const u8, splat: usize) net.Stream.Writer.Error!usize {
    _ = userdata;
    _ = dest;
    _ = header;
    _ = data;
    _ = splat;
    return error.NetworkDown;
}

pub fn failingNetWriteFile(userdata: ?*anyopaque, handle: net.Socket.Handle, header: []const u8, file_reader: *Io.File.Reader, limit: Io.Limit) net.Stream.Writer.WriteFileError!usize {
    _ = userdata;
    _ = handle;
    _ = header;
    _ = file_reader;
    _ = limit;
    return error.NetworkDown;
}

pub fn unreachableNetClose(userdata: ?*anyopaque, handle: []const net.Socket.Handle) void {
    _ = userdata;
    _ = handle;
    unreachable;
}

pub fn failingNetShutdown(userdata: ?*anyopaque, handle: net.Socket.Handle, how: net.ShutdownHow) net.ShutdownError!void {
    _ = userdata;
    _ = handle;
    _ = how;
    return error.NetworkDown;
}

pub fn failingNetInterfaceNameResolve(userdata: ?*anyopaque, name: *const net.Interface.Name) net.Interface.Name.ResolveError!net.Interface {
    _ = userdata;
    _ = name;
    return error.InterfaceNotFound;
}

pub fn unreachableNetInterfaceName(userdata: ?*anyopaque, interface: net.Interface) net.Interface.NameError!net.Interface.Name {
    _ = userdata;
    _ = interface;
    unreachable;
}

pub fn failingNetLookup(userdata: ?*anyopaque, host_name: net.HostName, resolved: *Queue(net.HostName.LookupResult), options: net.HostName.LookupOptions) net.HostName.LookupError!void {
    _ = userdata;
    _ = host_name;
    _ = resolved;
    _ = options;
    return error.NetworkDown;
}

test failing {
    const f: Io = .failing;
    // file stuff
    try std.testing.expectError(error.NoSpaceLeft, Dir.createDir(.cwd(), f, "test", .default_dir));
    try std.testing.expectError(error.NoSpaceLeft, Dir.createFile(.cwd(), f, "test", .{}));
    try std.testing.expectError(error.FileNotFound, Dir.openDir(.cwd(), f, "test", .{}));
    try std.testing.expectError(error.FileNotFound, Dir.openFile(.cwd(), f, "test", .{}));
    try File.writeStreamingAll(.stdout(), f, &.{});
    try std.testing.expectError(error.AccessDenied, File.MemoryMap.create(f, .stdout(), .{ .len = 0 }));
    // async stuff
    const closure = struct {
        var foo: usize = 0;
        fn doOp() void {
            foo = 4;
        }
    };
    var future = f.async(closure.doOp, .{});
    _ = future.await(f);
    try std.testing.expect(closure.foo == 4);
    // random stuff
    var buffer: [1]u8 = undefined;
    f.random(&buffer);
    try std.testing.expect(buffer[0] == 0);
}



---
File: /std/json.zig
---

//! JSON parsing and stringification conforming to RFC 8259. https://datatracker.ietf.org/doc/html/rfc8259
//!
//! The low-level `Scanner` API produces `Token`s from an input slice or successive slices of inputs,
//! The `Reader` API connects a `std.Io.GenericReader` to a `Scanner`.
//!
//! The high-level `parseFromSlice` and `parseFromTokenSource` deserialize a JSON document into a Zig type.
//! Parse into a dynamically-typed `Value` to load any JSON value for runtime inspection.
//!
//! The low-level `writeStream` emits syntax-conformant JSON tokens to a `std.Io.Writer`.
//! The high-level `stringify` serializes a Zig or `Value` type into JSON.

const builtin = @import("builtin");
const std = @import("std");
const testing = std.testing;

test Scanner {
    var scanner = Scanner.initCompleteInput(testing.allocator, "{\"foo\": 123}\n");
    defer scanner.deinit();
    try testing.expectEqual(Token.object_begin, try scanner.next());
    try testing.expectEqualSlices(u8, "foo", (try scanner.next()).string);
    try testing.expectEqualSlices(u8, "123", (try scanner.next()).number);
    try testing.expectEqual(Token.object_end, try scanner.next());
    try testing.expectEqual(Token.end_of_document, try scanner.next());
}

test parseFromSlice {
    var parsed_str = try parseFromSlice([]const u8, testing.allocator, "\"a\\u0020b\"", .{});
    defer parsed_str.deinit();
    try testing.expectEqualSlices(u8, "a b", parsed_str.value);

    const T = struct { a: i32 = -1, b: [2]u8 };
    var parsed_struct = try parseFromSlice(T, testing.allocator, "{\"b\":\"xy\"}", .{});
    defer parsed_struct.deinit();
    try testing.expectEqual(@as(i32, -1), parsed_struct.value.a); // default value
    try testing.expectEqualSlices(u8, "xy", parsed_struct.value.b[0..]);
}

test Value {
    var parsed = try parseFromSlice(Value, testing.allocator, "{\"anything\": \"goes\"}", .{});
    defer parsed.deinit();
    try testing.expectEqualSlices(u8, "goes", parsed.value.object.get("anything").?.string);
}

test Stringify {
    var out: std.Io.Writer.Allocating = .init(testing.allocator);
    var write_stream: Stringify = .{
        .writer = &out.writer,
        .options = .{ .whitespace = .indent_2 },
    };
    defer out.deinit();
    try write_stream.beginObject();
    try write_stream.objectField("foo");
    try write_stream.write(123);
    try write_stream.endObject();
    const expected =
        \\{
        \\  "foo": 123
        \\}
    ;
    try testing.expectEqualSlices(u8, expected, out.written());
}

pub const ObjectMap = @import("json/dynamic.zig").ObjectMap;
pub const Array = @import("json/dynamic.zig").Array;
pub const Value = @import("json/dynamic.zig").Value;

pub const ArrayHashMap = @import("json/hashmap.zig").ArrayHashMap;

pub const Scanner = @import("json/Scanner.zig");
pub const validate = Scanner.validate;
pub const Error = Scanner.Error;
pub const default_buffer_size = Scanner.default_buffer_size;
pub const Token = Scanner.Token;
pub const TokenType = Scanner.TokenType;
pub const Diagnostics = Scanner.Diagnostics;
pub const AllocWhen = Scanner.AllocWhen;
pub const default_max_value_len = Scanner.default_max_value_len;
pub const Reader = Scanner.Reader;
pub const isNumberFormattedLikeAnInteger = Scanner.isNumberFormattedLikeAnInteger;

pub const ParseOptions = @import("json/static.zig").ParseOptions;
pub const Parsed = @import("json/static.zig").Parsed;
pub const parseFromSlice = @import("json/static.zig").parseFromSlice;
pub const parseFromSliceLeaky = @import("json/static.zig").parseFromSliceLeaky;
pub const parseFromTokenSource = @import("json/static.zig").parseFromTokenSource;
pub const parseFromTokenSourceLeaky = @import("json/static.zig").parseFromTokenSourceLeaky;
pub const innerParse = @import("json/static.zig").innerParse;
pub const parseFromValue = @import("json/static.zig").parseFromValue;
pub const parseFromValueLeaky = @import("json/static.zig").parseFromValueLeaky;
pub const innerParseFromValue = @import("json/static.zig").innerParseFromValue;
pub const ParseError = @import("json/static.zig").ParseError;
pub const ParseFromValueError = @import("json/static.zig").ParseFromValueError;

pub const Stringify = @import("json/Stringify.zig");

/// Returns a formatter that formats the given value using stringify.
pub fn fmt(value: anytype, options: Stringify.Options) Formatter(@TypeOf(value)) {
    return Formatter(@TypeOf(value)){ .value = value, .options = options };
}

test fmt {
    const expectFmt = std.testing.expectFmt;
    try expectFmt("123", "{f}", .{fmt(@as(u32, 123), .{})});
    try expectFmt(
        \\{"num":927,"msg":"hello","sub":{"mybool":true}}
    , "{f}", .{fmt(struct {
        num: u32,
        msg: []const u8,
        sub: struct {
            mybool: bool,
        },
    }{
        .num = 927,
        .msg = "hello",
        .sub = .{ .mybool = true },
    }, .{})});
}

/// Formats the given value using stringify.
pub fn Formatter(comptime T: type) type {
    return struct {
        value: T,
        options: Stringify.Options,

        pub fn format(self: @This(), writer: *std.Io.Writer) std.Io.Writer.Error!void {
            try Stringify.value(self.value, self.options, writer);
        }
    };
}

test {
    _ = @import("json/test.zig");
    _ = Scanner;
    _ = @import("json/dynamic.zig");
    _ = @import("json/hashmap.zig");
    _ = @import("json/static.zig");
    _ = Stringify;
    _ = @import("json/JSONTestSuite_test.zig");
}



---
File: /std/leb128.zig
---

const builtin = @import("builtin");
const std = @import("std");
const testing = std.testing;

/// This is an "advanced" function. It allows one to use a fixed amount of memory to store a
/// ULEB128. This defeats the entire purpose of using this data encoding; it will no longer use
/// fewer bytes to store smaller numbers. The advantage of using a fixed width is that it makes
/// fields have a predictable size and so depending on the use case this tradeoff can be worthwhile.
/// An example use case of this is in emitting DWARF info where one wants to make a ULEB128 field
/// "relocatable", meaning that it becomes possible to later go back and patch the number to be a
/// different value without shifting all the following code.
pub fn writeUnsignedFixed(comptime l: usize, ptr: *[l]u8, int: std.meta.Int(.unsigned, l * 7)) void {
    writeUnsignedExtended(ptr, int);
}

/// Same as `writeUnsignedFixed` but with a runtime-known length.
/// Asserts `slice.len > 0`.
pub fn writeUnsignedExtended(slice: []u8, arg: anytype) void {
    const Arg = @TypeOf(arg);
    const Int = switch (Arg) {
        comptime_int => std.math.IntFittingRange(arg, arg),
        else => Arg,
    };
    const Value = if (@typeInfo(Int).int.bits < 8) u8 else Int;
    var value: Value = arg;

    for (slice[0 .. slice.len - 1]) |*byte| {
        byte.* = @truncate(0x80 | value);
        value >>= 7;
    }
    slice[slice.len - 1] = @as(u7, @intCast(value));
}

test writeUnsignedFixed {
    {
        var buf: [4]u8 = undefined;
        writeUnsignedFixed(4, &buf, 0);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(0, try reader.takeLeb128(u64));
    }
    {
        var buf: [4]u8 = undefined;
        writeUnsignedFixed(4, &buf, 1);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(1, try reader.takeLeb128(u64));
    }
    {
        var buf: [4]u8 = undefined;
        writeUnsignedFixed(4, &buf, 1000);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(1000, try reader.takeLeb128(u64));
    }
    {
        var buf: [4]u8 = undefined;
        writeUnsignedFixed(4, &buf, 10000000);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(10000000, try reader.takeLeb128(u64));
    }
}

/// This is an "advanced" function. It allows one to use a fixed amount of memory to store an
/// ILEB128. This defeats the entire purpose of using this data encoding; it will no longer use
/// fewer bytes to store smaller numbers. The advantage of using a fixed width is that it makes
/// fields have a predictable size and so depending on the use case this tradeoff can be worthwhile.
/// An example use case of this is in emitting DWARF info where one wants to make a ILEB128 field
/// "relocatable", meaning that it becomes possible to later go back and patch the number to be a
/// different value without shifting all the following code.
pub fn writeSignedFixed(comptime l: usize, ptr: *[l]u8, int: std.meta.Int(.signed, l * 7)) void {
    const T = @TypeOf(int);
    const U = if (@typeInfo(T).int.bits < 8) u8 else T;
    var value: U = @intCast(int);

    comptime var i = 0;
    inline while (i < (l - 1)) : (i += 1) {
        const byte: u8 = @bitCast(@as(i8, @truncate(value)) | -0b1000_0000);
        value >>= 7;
        ptr[i] = byte;
    }
    ptr[i] = @as(u7, @bitCast(@as(i7, @truncate(value))));
}

test writeSignedFixed {
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, 0);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(0, try reader.takeLeb128(i64));
    }
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, 1);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(1, try reader.takeLeb128(i64));
    }
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, -1);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(-1, try reader.takeLeb128(i64));
    }
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, 1000);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(1000, try reader.takeLeb128(i64));
    }
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, -1000);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(-1000, try reader.takeLeb128(i64));
    }
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, -10000000);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(-10000000, try reader.takeLeb128(i64));
    }
    {
        var buf: [4]u8 = undefined;
        writeSignedFixed(4, &buf, 10000000);
        var reader: std.Io.Reader = .fixed(&buf);
        try testing.expectEqual(10000000, try reader.takeLeb128(i64));
    }
}



---
File: /std/log.zig
---

//! std.log is a standardized interface for logging which allows for the logging
//! of programs and libraries using this interface to be formatted and filtered
//! by the implementer of the `std.options.logFn` function.
//!
//! Each log message has an associated scope enum, which can be used to give
//! context to the logging. The logging functions in std.log implicitly use a
//! scope of .default.
//!
//! A logging namespace using a custom scope can be created using the
//! std.log.scoped function, passing the scope as an argument; the logging
//! functions in the resulting struct use the provided scope parameter.
//! For example, a library called 'libfoo' might use
//! `const log = std.log.scoped(.libfoo);` to use .libfoo as the scope of its
//! log messages.
//!
//! For an example implementation of the `logFn` function, see `defaultLog`,
//! which is the default implementation. It outputs to stderr, using color if
//! supported. Its output looks like this:
//! ```
//! error: this is an error
//! error(scope): this is an error with a non-default scope
//! warning: this is a warning
//! info: this is an informative message
//! debug: this is a debugging message
//! ```

const std = @import("std.zig");
const builtin = @import("builtin");

pub const Level = enum {
    /// Error: something has gone wrong. This might be recoverable or might
    /// be followed by the program exiting.
    err,
    /// Warning: it is uncertain if something has gone wrong or not, but the
    /// circumstances would be worth investigating.
    warn,
    /// Info: general messages about the state of the program.
    info,
    /// Debug: messages only useful for debugging.
    debug,

    /// Returns a string literal of the given level in full text form.
    pub fn asText(comptime self: Level) []const u8 {
        return switch (self) {
            .err => "error",
            .warn => "warning",
            .info => "info",
            .debug => "debug",
        };
    }
};

/// The default log level is based on build mode.
pub const default_level: Level = switch (builtin.mode) {
    .Debug => .debug,
    .ReleaseSafe, .ReleaseFast, .ReleaseSmall => .info,
};

pub const ScopeLevel = struct {
    scope: @EnumLiteral(),
    level: Level,
};

fn log(
    comptime level: Level,
    comptime scope: @EnumLiteral(),
    comptime format: []const u8,
    args: anytype,
) void {
    if (comptime !logEnabled(level, scope)) return;

    std.options.logFn(level, scope, format, args);
}

/// Determine if a specific log message level and scope combination are enabled for logging.
pub fn logEnabled(comptime level: Level, comptime scope: @EnumLiteral()) bool {
    inline for (std.options.log_scope_levels) |scope_level| {
        if (scope_level.scope == scope) return @intFromEnum(level) <= @intFromEnum(scope_level.level);
    }
    return @intFromEnum(level) <= @intFromEnum(std.options.log_level);
}

pub const terminalMode = std.Options.logTerminalMode;

pub fn defaultTerminalMode() std.Io.Terminal.Mode {
    const stderr = std.debug.lockStderr(&.{}).terminal();
    std.debug.unlockStderr();
    return stderr.mode;
}

/// The default implementation for the log function. Custom log functions may
/// forward log messages to this function.
///
/// Uses a 64-byte buffer for formatted printing which is flushed before this
/// function returns.
pub fn defaultLog(
    comptime level: Level,
    comptime scope: @EnumLiteral(),
    comptime format: []const u8,
    args: anytype,
) void {
    const io = std.Options.debug_io;
    const prev = io.swapCancelProtection(.blocked);
    defer _ = io.swapCancelProtection(prev);
    var buffer: [64]u8 = undefined;
    const stderr = std.debug.lockStderr(&buffer).terminal();
    defer std.debug.unlockStderr();
    return defaultLogFileTerminal(level, scope, format, args, stderr) catch {};
}

pub fn defaultLogFileTerminal(
    comptime level: Level,
    comptime scope: @EnumLiteral(),
    comptime format: []const u8,
    args: anytype,
    t: std.Io.Terminal,
) std.Io.Writer.Error!void {
    t.setColor(switch (level) {
        .err => .red,
        .warn => .yellow,
        .info => .green,
        .debug => .magenta,
    }) catch {};
    t.setColor(.bold) catch {};
    try t.writer.writeAll(level.asText());
    t.setColor(.reset) catch {};
    t.setColor(.dim) catch {};
    t.setColor(.bold) catch {};
    if (scope != .default) try t.writer.print("({t})", .{scope});
    try t.writer.writeAll(": ");
    t.setColor(.reset) catch {};
    try t.writer.print(format ++ "\n", args);
}

/// Returns a scoped logging namespace that logs all messages using the scope
/// provided here.
pub fn scoped(comptime scope: @EnumLiteral()) type {
    return struct {
        /// Log an error message. This log level is intended to be used
        /// when something has gone wrong. This might be recoverable or might
        /// be followed by the program exiting.
        pub fn err(
            comptime format: []const u8,
            args: anytype,
        ) void {
            @branchHint(.cold);
            log(.err, scope, format, args);
        }

        /// Log a warning message. This log level is intended to be used if
        /// it is uncertain whether something has gone wrong or not, but the
        /// circumstances would be worth investigating.
        pub fn warn(
            comptime format: []const u8,
            args: anytype,
        ) void {
            log(.warn, scope, format, args);
        }

        /// Log an info message. This log level is intended to be used for
        /// general messages about the state of the program.
        pub fn info(
            comptime format: []const u8,
            args: anytype,
        ) void {
            log(.info, scope, format, args);
        }

        /// Log a debug message. This log level is intended to be used for
        /// messages which are only useful for debugging.
        pub fn debug(
            comptime format: []const u8,
            args: anytype,
        ) void {
            log(.debug, scope, format, args);
        }
    };
}

pub const default_log_scope = .default;

/// The default scoped logging namespace.
pub const default = scoped(default_log_scope);

/// Log an error message using the default scope. This log level is intended to
/// be used when something has gone wrong. This might be recoverable or might
/// be followed by the program exiting.
pub const err = default.err;

/// Log a warning message using the default scope. This log level is intended
/// to be used if it is uncertain whether something has gone wrong or not, but
/// the circumstances would be worth investigating.
pub const warn = default.warn;

/// Log an info message using the default scope. This log level is intended to
/// be used for general messages about the state of the program.
pub const info = default.info;

/// Log a debug message using the default scope. This log level is intended to
/// be used for messages which are only useful for debugging.
pub const debug = default.debug;



---
File: /std/macho.zig
---

const std = @import("std");
const builtin = @import("builtin");
const assert = std.debug.assert;
const mem = std.mem;
const meta = std.meta;
const testing = std.testing;

const Allocator = mem.Allocator;

pub const cpu_type_t = c_int;
pub const cpu_subtype_t = c_int;
pub const vm_prot_t = packed struct(u32) {
    READ: bool = false,
    WRITE: bool = false,
    EXEC: bool = false,
    _: u1 = 0,
    /// When a caller finds that they cannot obtain write permission on a
    /// mapped entry, the following flag can be used. The entry will be
    /// made "needs copy" effectively copying the object (using COW),
    /// and write permission will be added to the maximum protections for
    /// the associated entry.
    COPY: bool = false,
    __: u27 = 0,
};

pub const mach_header = extern struct {
    magic: u32,
    cputype: cpu_type_t,
    cpusubtype: cpu_subtype_t,
    filetype: u32,
    ncmds: u32,
    sizeofcmds: u32,
    flags: u32,
};

pub const mach_header_64 = extern struct {
    magic: u32 = MH_MAGIC_64,
    cputype: cpu_type_t = 0,
    cpusubtype: cpu_subtype_t = 0,
    filetype: u32 = 0,
    ncmds: u32 = 0,
    sizeofcmds: u32 = 0,
    flags: u32 = 0,
    reserved: u32 = 0,
};

pub const fat_header = extern struct {
    magic: u32,
    nfat_arch: u32,
};

pub const fat_arch = extern struct {
    cputype: cpu_type_t,
    cpusubtype: cpu_subtype_t,
    offset: u32,
    size: u32,
    @"align": u32,
};

pub const load_command = extern struct {
    cmd: LC,
    cmdsize: u32,
};

/// The uuid load command contains a single 128-bit unique random number that
/// identifies an object produced by the static link editor.
pub const uuid_command = extern struct {
    /// LC_UUID
    cmd: LC = .UUID,

    /// sizeof(struct uuid_command)
    cmdsize: u32 = @sizeOf(uuid_command),

    /// the 128-bit uuid
    uuid: [16]u8 = undefined,
};

/// The version_min_command contains the min OS version on which this
/// binary was built to run.
pub const version_min_command = extern struct {
    /// LC_VERSION_MIN_MACOSX or LC_VERSION_MIN_IPHONEOS or LC_VERSION_MIN_WATCHOS or LC_VERSION_MIN_TVOS
    cmd: LC,

    /// sizeof(struct version_min_command)
    cmdsize: u32 = @sizeOf(version_min_command),

    /// X.Y.Z is encoded in nibbles xxxx.yy.zz
    version: u32,

    /// X.Y.Z is encoded in nibbles xxxx.yy.zz
    sdk: u32,
};

/// The source_version_command is an optional load command containing
/// the version of the sources used to build the binary.
pub const source_version_command = extern struct {
    /// LC_SOURCE_VERSION
    cmd: LC = .SOURCE_VERSION,

    /// sizeof(source_version_command)
    cmdsize: u32 = @sizeOf(source_version_command),

    /// A.B.C.D.E packed as a24.b10.c10.d10.e10
    version: u64,
};

/// The build_version_command contains the min OS version on which this
/// binary was built to run for its platform. The list of known platforms and
/// tool values following it.
pub const build_version_command = extern struct {
    /// LC_BUILD_VERSION
    cmd: LC = .BUILD_VERSION,

    /// sizeof(struct build_version_command) plus
    /// ntools * sizeof(struct build_version_command)
    cmdsize: u32,

    /// platform
    platform: PLATFORM,

    /// X.Y.Z is encoded in nibbles xxxx.yy.zz
    minos: u32,

    /// X.Y.Z is encoded in nibbles xxxx.yy.zz
    sdk: u32,

    /// number of tool entries following this
    ntools: u32,
};

pub const build_tool_version = extern struct {
    /// enum for the tool
    tool: TOOL,

    /// version number of the tool
    version: u32,
};

pub const PLATFORM = enum(u32) {
    UNKNOWN = 0,
    ANY = 0xffffffff,
    MACOS = 1,
    IOS = 2,
    TVOS = 3,
    WATCHOS = 4,
    BRIDGEOS = 5,
    MACCATALYST = 6,
    IOSSIMULATOR = 7,
    TVOSSIMULATOR = 8,
    WATCHOSSIMULATOR = 9,
    DRIVERKIT = 10,
    VISIONOS = 11,
    VISIONOSSIMULATOR = 12,
    _,
};

pub const TOOL = enum(u32) {
    CLANG = 0x1,
    SWIFT = 0x2,
    LD = 0x3,
    LLD = 0x4, // LLVM's stock LLD linker
    ZIG = 0x5, // Unofficially Zig
    _,
};

/// The entry_point_command is a replacement for thread_command.
/// It is used for main executables to specify the location (file offset)
/// of main(). If -stack_size was used at link time, the stacksize
/// field will contain the stack size needed for the main thread.
pub const entry_point_command = extern struct {
    /// LC_MAIN only used in MH_EXECUTE filetypes
    cmd: LC = .MAIN,

    /// sizeof(struct entry_point_command)
    cmdsize: u32 = @sizeOf(entry_point_command),

    /// file (__TEXT) offset of main()
    entryoff: u64 = 0,

    /// if not zero, initial stack size
    stacksize: u64 = 0,
};

/// The symtab_command contains the offsets and sizes of the link-edit 4.3BSD
/// "stab" style symbol table information as described in the header files
/// <nlist.h> and <stab.h>.
pub const symtab_command = extern struct {
    /// LC_SYMTAB
    cmd: LC = .SYMTAB,

    /// sizeof(struct symtab_command)
    cmdsize: u32 = @sizeOf(symtab_command),

    /// symbol table offset
    symoff: u32 = 0,

    /// number of symbol table entries
    nsyms: u32 = 0,

    /// string table offset
    stroff: u32 = 0,

    /// string table size in bytes
    strsize: u32 = 0,
};

/// This is the second set of the symbolic information which is used to support
/// the data structures for the dynamically link editor.
///
/// The original set of symbolic information in the symtab_command which contains
/// the symbol and string tables must also be present when this load command is
/// present.  When this load command is present the symbol table is organized
/// into three groups of symbols:
///  local symbols (static and debugging symbols) - grouped by module
///  defined external symbols - grouped by module (sorted by name if not lib)
///  undefined external symbols (sorted by name if MH_BINDATLOAD is not set,
///  and in order the were seen by the static linker if MH_BINDATLOAD is set)
/// In this load command there are offsets and counts to each of the three groups
/// of symbols.
///
/// This load command contains a the offsets and sizes of the following new
/// symbolic information tables:
///  table of contents
///  module table
///  reference symbol table
///  indirect symbol table
/// The first three tables above (the table of contents, module table and
/// reference symbol table) are only present if the file is a dynamically linked
/// shared library.  For executable and object modules, which are files
/// containing only one module, the information that would be in these three
/// tables is determined as follows:
///  table of contents - the defined external symbols are sorted by name
///  module table - the file contains only one module so everything in the file
///  is part of the module.
///  reference symbol table - is the defined and undefined external symbols
///
/// For dynamically linked shared library files this load command also contains
/// offsets and sizes to the pool of relocation entries for all sections
/// separated into two groups:
///  external relocation entries
///  local relocation entries
/// For executable and object modules the relocation entries continue to hang
/// off the section structures.
pub const dysymtab_command = extern struct {
    /// LC_DYSYMTAB
    cmd: LC = .DYSYMTAB,

    /// sizeof(struct dysymtab_command)
    cmdsize: u32 = @sizeOf(dysymtab_command),

    // The symbols indicated by symoff and nsyms of the LC_SYMTAB load command
    // are grouped into the following three groups:
    //    local symbols (further grouped by the module they are from)
    //    defined external symbols (further grouped by the module they are from)
    //    undefined symbols
    //
    // The local symbols are used only for debugging.  The dynamic binding
    // process may have to use them to indicate to the debugger the local
    // symbols for a module that is being bound.
    //
    // The last two groups are used by the dynamic binding process to do the
    // binding (indirectly through the module table and the reference symbol
    // table when this is a dynamically linked shared library file).

    /// index of local symbols
    ilocalsym: u32 = 0,

    /// number of local symbols
    nlocalsym: u32 = 0,

    /// index to externally defined symbols
    iextdefsym: u32 = 0,

    /// number of externally defined symbols
    nextdefsym: u32 = 0,

    /// index to undefined symbols
    iundefsym: u32 = 0,

    /// number of undefined symbols
    nundefsym: u32 = 0,

    // For the for the dynamic binding process to find which module a symbol
    // is defined in the table of contents is used (analogous to the ranlib
    // structure in an archive) which maps defined external symbols to modules
    // they are defined in.  This exists only in a dynamically linked shared
    // library file.  For executable and object modules the defined external
    // symbols are sorted by name and is use as the table of contents.

    /// file offset to table of contents
    tocoff: u32 = 0,

    /// number of entries in table of contents
    ntoc: u32 = 0,

    // To support dynamic binding of "modules" (whole object files) the symbol
    // table must reflect the modules that the file was created from.  This is
    // done by having a module table that has indexes and counts into the merged
    // tables for each module.  The module structure that these two entries
    // refer to is described below.  This exists only in a dynamically linked
    // shared library file.  For executable and object modules the file only
    // contains one module so everything in the file belongs to the module.

    /// file offset to module table
    modtaboff: u32 = 0,

    /// number of module table entries
    nmodtab: u32 = 0,

    // To support dynamic module binding the module structure for each module
    // indicates the external references (defined and undefined) each module
    // makes.  For each module there is an offset and a count into the
    // reference symbol table for the symbols that the module references.
    // This exists only in a dynamically linked shared library file.  For
    // executable and object modules the defined external symbols and the
    // undefined external symbols indicates the external references.

    /// offset to referenced symbol table
    extrefsymoff: u32 = 0,

    /// number of referenced symbol table entries
    nextrefsyms: u32 = 0,

    // The sections that contain "symbol pointers" and "routine stubs" have
    // indexes and (implied counts based on the size of the section and fixed
    // size of the entry) into the "indirect symbol" table for each pointer
    // and stub.  For every section of these two types the index into the
    // indirect symbol table is stored in the section header in the field
    // reserved1.  An indirect symbol table entry is simply a 32bit index into
    // the symbol table to the symbol that the pointer or stub is referring to.
    // The indirect symbol table is ordered to match the entries in the section.

    /// file offset to the indirect symbol table
    indirectsymoff: u32 = 0,

    /// number of indirect symbol table entries
    nindirectsyms: u32 = 0,

    // To support relocating an individual module in a library file quickly the
    // external relocation entries for each module in the library need to be
    // accessed efficiently.  Since the relocation entries can't be accessed
    // through the section headers for a library file they are separated into
    // groups of local and external entries further grouped by module.  In this
    // case the presents of this load command who's extreloff, nextrel,
    // locreloff and nlocrel fields are non-zero indicates that the relocation
    // entries of non-merged sections are not referenced through the section
    // structures (and the reloff and nreloc fields in the section headers are
    // set to zero).
    //
    // Since the relocation entries are not accessed through the section headers
    // this requires the r_address field to be something other than a section
    // offset to identify the item to be relocated.  In this case r_address is
    // set to the offset from the vmaddr of the first LC_SEGMENT command.
    // For MH_SPLIT_SEGS images r_address is set to the the offset from the
    // vmaddr of the first read-write LC_SEGMENT command.
    //
    // The relocation entries are grouped by module and the module table
    // entries have indexes and counts into them for the group of external
    // relocation entries for that the module.
    //
    // For sections that are merged across modules there must not be any
    // remaining external relocation entries for them (for merged sections
    // remaining relocation entries must be local).

    /// offset to external relocation entries
    extreloff: u32 = 0,

    /// number of external relocation entries
    nextrel: u32 = 0,

    // All the local relocation entries are grouped together (they are not
    // grouped by their module since they are only used if the object is moved
    // from 
```
