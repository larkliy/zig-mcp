```
            defer mutexUnlock(&t.mutex);
                        if (t.random_file.fd >= 0) {
                            closeFd(fd);
                            return t.random_file.fd;
                        } else if (!posix.S.ISCHR(statx.mode)) {
                            t.random_file.fd = -2;
                            return error.EntropyUnavailable;
                        } else {
                            t.random_file.fd = fd;
                            return fd;
                        }
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => return syscall.fail(error.EntropyUnavailable),
                }
            }
        },
        else => {
            const syscall: Syscall = try .start();
            while (true) {
                var stat = std.mem.zeroes(posix.Stat);
                switch (posix.errno(fstat_sym(fd, &stat))) {
                    .SUCCESS => {
                        syscall.finish();
                        mutexLock(&t.mutex); // Another thread might have won the race.
                        defer mutexUnlock(&t.mutex);
                        if (t.random_file.fd >= 0) {
                            closeFd(fd);
                            return t.random_file.fd;
                        } else if (!posix.S.ISCHR(stat.mode)) {
                            t.random_file.fd = -2;
                            return error.EntropyUnavailable;
                        } else {
                            t.random_file.fd = fd;
                            return fd;
                        }
                    },
                    .INTR => {
                        try syscall.checkCancel();
                        continue;
                    },
                    else => return syscall.fail(error.EntropyUnavailable),
                }
            }
        },
    }
}

test {
    _ = @import("Threaded/test.zig");
}

const use_parking_futex = switch (native_os) {
    .windows => true, // RtlWaitOnAddress is a userland implementation anyway
    .netbsd => true, // NetBSD has `futex(2)`, but it's historically been quite buggy. TODO: evaluate whether it's okay to use now.
    .illumos => true, // Illumos has no futex mechanism
    else => false,
};
const use_parking_sleep = switch (native_os) {
    // On Windows, we can implement sleep either with `NtDelayExecution` (which is how `SleepEx` in
    // kernel32 works) or `NtWaitForAlertByThreadId` (thread parking). We're already using the
    // latter for futex, so we may as well use it for sleeping too, to maximise code reuse. I'm
    // also more confident that it will always correctly handle the cancelation race (so "unpark"
    // before "park" causes "park" to return immediately): it *seems* like alertable sleeps paired
    // with `NtAlertThread` do actually do this too, but there could be some caveat (e.g. it might
    // fail under some specific condition), whereas `NtWaitForAlertByThreadId` must reliably trigger
    // this behavior because `RtlWaitOnAddress` relies on it.
    .windows => true,

    // These targets have `_lwp_park`, which is superior to POSIX nanosleep because it has a better
    // cancelation mechanism.
    .netbsd,
    .illumos,
    => true,

    else => false,
};

const parking_futex = struct {
    comptime {
        assert(use_parking_futex);
    }

    const Bucket = struct {
        /// Used as a fast check for `wake` to avoid having to acquire `mutex` to discover there are no
        /// waiters. It is important for `wait` to increment this *before* checking the futex value to
        /// avoid a race.
        num_waiters: std.atomic.Value(u32),
        /// Protects `waiters`.
        mutex: ParkingMutex,
        waiters: std.DoublyLinkedList,

        /// Prevent false sharing between buckets.
        _: void align(std.atomic.cache_line) = {},

        const init: Bucket = .{ .num_waiters = .init(0), .mutex = .init, .waiters = .{} };
    };

    const Waiter = struct {
        node: std.DoublyLinkedList.Node,
        address: usize,
        tid: std.Thread.Id,
        /// `thread_status.cancelation` is `.parked` while the thread is waiting. The single thread
        /// which atomically updates it (to `.none` or `.canceling`) is responsible for:
        ///
        /// * Removing the `Waiter` from `Bucket.waiters`
        /// * Decrementing `Bucket.num_waiters`
        /// * Unparking the thread (*after* the above, so that the `Waiter` does not go out of scope
        ///   while it is still in the `Bucket`).
        thread_status: *std.atomic.Value(Thread.Status),
        unpark_flag: if (need_unpark_flag) *UnparkFlag else void,
    };

    fn bucketForAddress(address: usize) *Bucket {
        const global = struct {
            /// Length must be a power of two. The longer this array, the less likely contention is
            /// between different futexes. This length seems like it'll provide a reasonable balance
            /// between contention and memory usage: assuming a 128-byte `Bucket` (due to cache line
            /// alignment), this uses 32 KiB of memory.
            var buckets: [256]Bucket = @splat(.init);
        };

        // Here we use Fibonacci hashing: the golden ratio can be used to evenly redistribute input
        // values across a range, giving a poor, but extremely quick to compute, hash.

        // This literal is the rounded value of '2^64 / phi' (where 'phi' is the golden ratio). The
        // shift then converts it to '2^b / phi', where 'b' is the pointer bit width.
        const fibonacci_multiplier = 0x9E3779B97F4A7C15 >> (64 - @bitSizeOf(usize));
        const hashed = address *% fibonacci_multiplier;

        comptime assert(std.math.isPowerOfTwo(global.buckets.len));
        // The high bits of `hashed` have better entropy than the low bits.
        const index = hashed >> (@bitSizeOf(usize) - @ctz(global.buckets.len));

        return &global.buckets[index];
    }

    fn wait(ptr: *const u32, expect: u32, uncancelable: bool, timeout: Io.Timeout) Io.Cancelable!void {
        const bucket = bucketForAddress(@intFromPtr(ptr));

        // Put the threadlocal access outside of the critical section.
        const opt_thread = Thread.current;
        const self_tid = if (opt_thread) |thread| thread.id else std.Thread.getCurrentId();

        var waiter: Waiter = .{
            .node = undefined, // populated by list append
            .address = @intFromPtr(ptr),
            .tid = self_tid,
            .thread_status = undefined, // populated in critical section
            .unpark_flag = undefined, // populated in critical section
        };

        var status_buf: std.atomic.Value(Thread.Status) = undefined;
        var unpark_flag_buf: UnparkFlag = unpark_flag_init;

        {
            bucket.mutex.lock();
            defer bucket.mutex.unlock();

            _ = bucket.num_waiters.fetchAdd(1, .acquire);

            if (@atomicLoad(u32, ptr, .monotonic) != expect) {
                assert(bucket.num_waiters.fetchSub(1, .monotonic) > 0);
                return;
            }

            // This is in the critical section to avoid marking the thread as parked until we're
            // certain that we're actually going to park.
            waiter.thread_status, waiter.unpark_flag = status: {
                cancelable: {
                    if (uncancelable) break :cancelable;
                    const thread = opt_thread orelse break :cancelable;
                    switch (thread.cancel_protection) {
                        .blocked => break :cancelable,
                        .unblocked => {},
                    }
                    thread.futex_waiter = &waiter;
                    const old_status = thread.status.fetchOr(
                        .{ .cancelation = @enumFromInt(0b001), .awaitable = .null },
                        .release, // release `thread.futex_waiter`
                    );
                    switch (old_status.cancelation) {
                        .none => {}, // status is now `.parked`
                        .canceling => {
                            // status is now `.canceled`
                            assert(bucket.num_waiters.fetchSub(1, .monotonic) > 0);
                            return error.Canceled;
                        },
                        .canceled => break :cancelable, // status is still `.canceled`
                        .parked => unreachable,
                        .blocked => unreachable,
                        .blocked_alertable => unreachable,
                        .blocked_alertable_canceling => unreachable,
                        .blocked_canceling => unreachable,
                    }
                    // We could now be unparked for a cancelation at any time!
                    break :status .{ &thread.status, if (need_unpark_flag) &thread.unpark_flag };
                }
                // This is an uncancelable wait, so just use `status_buf`. Note that the value of
                // `status_buf.awaitable` is irrelevant because this is only visible to futex code,
                // while only cancelation cares about `awaitable`.
                status_buf.raw = .{ .cancelation = .parked, .awaitable = .null };
                break :status .{ &status_buf, if (need_unpark_flag) &unpark_flag_buf };
            };

            bucket.waiters.append(&waiter.node);
        }

        if (park(timeout, ptr, waiter.unpark_flag)) {
            // We were unparked by either `wake` or cancelation, so our current status is either
            // `.none` or `.canceling`. In either case, they've already removed `waiter` from
            // `bucket`, so we have nothing more to do!
        } else |err| switch (err) {
            error.Timeout => {
                // We're not out of the woods yet: an unpark could race with the timeout.
                const old_status = waiter.thread_status.fetchAnd(
                    .{ .cancelation = @enumFromInt(0b110), .awaitable = .all_ones },
                    .monotonic,
                );
                switch (old_status.cancelation) {
                    .parked => {
                        // No race. It is our responsibility to remove `waiter` from `bucket`.
                        // New status is `.none`.
                        bucket.mutex.lock();
                        defer bucket.mutex.unlock();
                        bucket.waiters.remove(&waiter.node);
                        assert(bucket.num_waiters.fetchSub(1, .monotonic) > 0);
                    },
                    .none, .canceling => {
                        // Race condition: the timeout was reached, then `wake` or a canceler tried
                        // to unpark us. Whoever did that will remove us from `bucket`. Wait for
                        // that (and drop the unpark request in doing so).
                        // New status is `.none` or `.canceling` respectively.
                        park(.none, ptr, waiter.unpark_flag) catch |e| switch (e) {
                            error.Timeout => unreachable,
                        };
                    },
                    .canceled => unreachable,
                    .blocked => unreachable,
                    .blocked_alertable => unreachable,
                    .blocked_canceling => unreachable,
                    .blocked_alertable_canceling => unreachable,
                }
            },
        }
    }

    fn wake(ptr: *const u32, max_waiters: u32) void {
        if (max_waiters == 0) return;

        const bucket = bucketForAddress(@intFromPtr(ptr));

        // To ensure the store to `ptr` is ordered before this check, we effectively want a `.release`
        // load, but that doesn't exist in the C11 memory model, so emulate it with a non-mutating rmw.
        if (bucket.num_waiters.fetchAdd(0, .release) == 0) {
            @branchHint(.likely);
            return; // no waiters
        }

        // Waiters removed from the linked list under the mutex so we can unpark their threads outside
        // of the critical section. This forms a singly-linked list of waiters using `Waiter.node.next`.
        var waking_head: ?*std.DoublyLinkedList.Node = null;
        {
            bucket.mutex.lock();
            defer bucket.mutex.unlock();

            var num_removed: u32 = 0;
            var it = bucket.waiters.first;
            while (num_removed < max_waiters) {
                const waiter: *Waiter = @fieldParentPtr("node", it orelse break);
                it = waiter.node.next;
                if (waiter.address != @intFromPtr(ptr)) continue;
                const old_status = waiter.thread_status.fetchAnd(
                    .{ .cancelation = @enumFromInt(0b110), .awaitable = .all_ones },
                    .monotonic,
                );
                switch (old_status.cancelation) {
                    .parked => {}, // state updated to `.none`
                    .none => continue, // race with timeout; they are about to lock `bucket.mutex` and remove themselves from the bucket
                    .canceling => continue, // race with a canceler who hasn't called `removeCanceledWaiter` yet
                    .canceled => unreachable,
                    .blocked => unreachable,
                    .blocked_alertable => unreachable,
                    .blocked_alertable_canceling => unreachable,
                    .blocked_canceling => unreachable,
                }
                // We're waking this waiter. Remove them from the bucket and add them to our local list.
                bucket.waiters.remove(&waiter.node);
                waiter.node.next = waking_head;
                waking_head = &waiter.node;
                num_removed += 1;
            }
            _ = bucket.num_waiters.fetchSub(num_removed, .monotonic);
        }

        var unpark_buf: [128]UnparkTid = undefined;
        var unpark_len: usize = 0;

        // Finally, unpark the threads.
        while (waking_head) |node| {
            waking_head = node.next;
            const waiter: *Waiter = @fieldParentPtr("node", node);
            unpark_buf[unpark_len] = waiter.tid;
            if (need_unpark_flag) setUnparkFlag(waiter.unpark_flag);
            unpark_len += 1;
            if (unpark_len == unpark_buf.len) {
                unpark(&unpark_buf, ptr);
                unpark_len = 0;
            }
        }
        if (unpark_len > 0) {
            unpark(unpark_buf[0..unpark_len], ptr);
        }
    }

    fn removeCanceledWaiter(waiter: *Waiter) void {
        const bucket = bucketForAddress(waiter.address);
        bucket.mutex.lock();
        defer bucket.mutex.unlock();
        bucket.waiters.remove(&waiter.node);
        assert(bucket.num_waiters.fetchSub(1, .monotonic) > 0);
    }
};
const parking_sleep = struct {
    comptime {
        assert(use_parking_sleep);
    }
    fn sleep(timeout: Io.Timeout) Io.Cancelable!void {
        const opt_thread = Thread.current;
        cancelable: {
            const thread = opt_thread orelse break :cancelable;
            switch (thread.cancel_protection) {
                .blocked => break :cancelable,
                .unblocked => {},
            }
            thread.futex_waiter = null;
            {
                const old_status = thread.status.fetchOr(
                    .{ .cancelation = @enumFromInt(0b001), .awaitable = .null },
                    .release, // release `thread.futex_waiter`
                );
                switch (old_status.cancelation) {
                    .none => {}, // status is now `.parked`
                    .canceling => return error.Canceled, // status is now `.canceled`
                    .canceled => break :cancelable, // status is still `.canceled`
                    .parked => unreachable,
                    .blocked => unreachable,
                    .blocked_alertable => unreachable,
                    .blocked_alertable_canceling => unreachable,
                    .blocked_canceling => unreachable,
                }
            }
            if (park(timeout, null, if (need_unpark_flag) &thread.unpark_flag)) {
                // The only reason this could possibly happen is cancelation.
                const old_status = thread.status.load(.monotonic);
                assert(old_status.cancelation == .canceling);
                thread.status.store(
                    .{ .cancelation = .canceled, .awaitable = old_status.awaitable },
                    .monotonic,
                );
                return error.Canceled;
            } else |err| switch (err) {
                error.Timeout => {
                    // We're not out of the woods yet: an unpark could race with the timeout.
                    const old_status = thread.status.fetchAnd(
                        .{ .cancelation = @enumFromInt(0b110), .awaitable = .all_ones },
                        .monotonic,
                    );
                    switch (old_status.cancelation) {
                        .parked => return, // No race; new status is `.none`
                        .canceling => {
                            // Race condition: the timeout was reached, then someone tried to unpark
                            // us for a cancelation. Whoever did that will have called `unpark`, so
                            // drop that unpark request by waiting for it.
                            // Status is still `.canceling`.
                            park(.none, null, if (need_unpark_flag) &thread.unpark_flag) catch |e| switch (e) {
                                error.Timeout => unreachable,
                            };
                            return;
                        },
                        .none => unreachable,
                        .canceled => unreachable,
                        .blocked => unreachable,
                        .blocked_alertable => unreachable,
                        .blocked_canceling => unreachable,
                        .blocked_alertable_canceling => unreachable,
                    }
                },
            }
        }
        // Uncancelable sleep; we expect not to be manually unparked.
        var dummy_flag: UnparkFlag = unpark_flag_init;
        if (park(timeout, null, if (need_unpark_flag) &dummy_flag)) {
            unreachable; // unexpected unpark
        } else |err| switch (err) {
            error.Timeout => return,
        }
    }
};
const ParkingMutex = struct {
    state: std.atomic.Value(State),

    const init: ParkingMutex = .{ .state = .init(.unlocked) };

    comptime {
        assert(use_parking_futex);
    }

    const State = enum(usize) {
        unlocked = 1,
        /// This value is intentionally 0 so that `waiter` returns `null`.
        locked_once = 0,
        /// Contended; value is a `*Waiter`.
        _,
        /// Returns the head of the waiter list. Illegal to call if `s == .unlocked`.
        fn waiter(s: State) ?*Waiter {
            return @ptrFromInt(@intFromEnum(s));
        }
        /// Returns a locked state where `w` is contending the lock.
        /// If `w` is `null`, returns `.locked_once`.
        fn fromWaiter(w: ?*Waiter) State {
            return @enumFromInt(@intFromPtr(w));
        }
    };
    const Waiter = struct {
        unpark_flag: UnparkFlag,
        /// Never modified once the `Waiter` is in the linked list.
        next: ?*Waiter,
        /// Never modified once the `Waiter` is in the linked list.
        tid: std.Thread.Id,
    };
    fn lock(m: *ParkingMutex) void {
        state: switch (State.unlocked) { // assume 'unlocked' to optimize for uncontended case
            .unlocked => continue :state m.state.cmpxchgWeak(
                .unlocked,
                .locked_once,
                .acquire, // acquire lock
                .monotonic,
            ) orelse {
                @branchHint(.likely);
                return;
            },

            .locked_once, _ => |last_state| {
                const old_waiter = last_state.waiter();
                const self_tid = if (Thread.current) |t| t.id else std.Thread.getCurrentId();
                var waiter: Waiter = .{
                    .next = old_waiter,
                    .unpark_flag = unpark_flag_init,
                    .tid = self_tid,
                };
                if (m.state.cmpxchgWeak(
                    .fromWaiter(old_waiter),
                    .fromWaiter(&waiter),
                    .release, // release `waiter`
                    .monotonic,
                )) |new_state| {
                    continue :state new_state;
                }
                // We're now in the list of waiters---park until we're given the lock.
                park(.none, m, if (need_unpark_flag) &waiter.unpark_flag) catch |err| switch (err) {
                    error.Timeout => unreachable,
                };
                return;
            },
        }
    }
    fn unlock(m: *ParkingMutex) void {
        state: switch (State.locked_once) { // assume 'locked_once' to optimize for uncontended case
            .unlocked => unreachable, // we hold the lock

            .locked_once => continue :state m.state.cmpxchgWeak(
                .locked_once,
                .unlocked,
                .release, // release lock
                .acquire, // acquire any `Waiter` memory
            ) orelse {
                @branchHint(.likely);
                return;
            },

            _ => |last_state| {
                // The logic here does not have ABA problems, and does some accesses non-atomically,
                // because `Waiter.next` is owned by the lock holder (that's us!) once the waiter is
                // in the linked list, up until we unpark the waiter.

                // Run through the waiter list to the end to ensure fairness. This is obviously not
                // ideal, but it shouldn't be a big deal in practice provided the critical section
                // is fairly small (so we won't get too many threads contending the mutex at once).
                // There's a *chance* we could get away with a LIFO queue for our use case, but I
                // don't wanna risk that.
                var parent: ?*Waiter = null;
                var waiter: *Waiter = last_state.waiter().?;
                while (waiter.next) |next| {
                    parent = waiter;
                    waiter = next;
                }
                // `waiter` is next in line for the lock. Remove them from the list.
                if (parent) |p| {
                    assert(p.next == waiter);
                    p.next = null;
                } else {
                    // We're waking the last waiter, so clear the list head.
                    if (m.state.cmpxchgWeak(
                        .fromWaiter(last_state.waiter().?),
                        .locked_once,
                        .acquire,
                        .acquire, // acquire any new `Waiter` memory
                    )) |new_state| {
                        continue :state new_state;
                    }
                }
                // Now we're ready to actually hand the lock over to them.
                const tid = waiter.tid; // load before the unpark below potentially invalidates `waiter`
                if (need_unpark_flag) setUnparkFlag(&waiter.unpark_flag);
                unpark(&.{tid}, m);
                return;
            },
        }
    }
};

fn timeoutToWindowsInterval(timeout: Io.Timeout) ?windows.LARGE_INTEGER {
    // ntdll only supports two combinations:
    // * real-time (`.real`) sleeps with absolute deadlines
    // * monotonic (`.awake`/`.boot`) sleeps with relative durations
    const clock = switch (timeout) {
        .none => return null,
        .duration => |d| d.clock,
        .deadline => |d| d.clock,
    };
    switch (clock) {
        .cpu_process, .cpu_thread => unreachable, // cannot sleep for CPU time
        .real => {
            const deadline = switch (timeout) {
                .none => unreachable,
                .duration => |d| nowWindows(clock).addDuration(d.raw),
                .deadline => |d| d.raw,
            };
            const epoch_ns = std.time.epoch.windows * std.time.ns_per_s;
            return @intCast(@max(@divTrunc(deadline.nanoseconds - epoch_ns, 100), 0));
        },
        .awake, .boot => {
            const duration = switch (timeout) {
                .none => unreachable,
                .duration => |d| d.raw,
                .deadline => |d| nowWindows(clock).durationTo(d.raw),
            };
            return @intCast(@min(@divTrunc(-duration.nanoseconds, 100), -1));
        },
    }
}

/// The API on NetBSD and Illumos sucks and can unpark spuriously (well, it *can't*, but signals
/// cause an indistinguishable unblock, and libpthread really likes to leave unparks pending).
/// As such, on these targets only, we need to pass around a flag to track whether a thread is
/// "actually" being unparked.
const need_unpark_flag = switch (native_os) {
    .netbsd, .illumos => true,
    else => false,
};
const UnparkFlag = if (need_unpark_flag) std.atomic.Value(bool) else void;
const unpark_flag_init: UnparkFlag = if (need_unpark_flag) .init(false);
/// Must be called before `unpark`. After this function is called, the thread may be unparked at any
/// time, so the caller must not reference values on its stack.
fn setUnparkFlag(f: *UnparkFlag) void {
    f.store(true, .release);
}

/// The type passed into `unpark` for the thread ID. You'd think this was just a `std.Thread.Id`,
/// but it seems that someone at Microsoft forgot how big their TIDs are supposed to be.
const UnparkTid = switch (native_os) {
    .windows => usize,
    else => std.Thread.Id,
};

fn park(
    timeout: Io.Timeout,
    /// This value has no semantic effect, but may allow the OS to optimize the operation.
    addr_hint: ?*const anyopaque,
    unpark_flag: if (need_unpark_flag) *UnparkFlag else void,
) error{Timeout}!void {
    comptime assert(use_parking_futex or use_parking_sleep);
    switch (native_os) {
        .windows => {
            const raw_timeout = timeoutToWindowsInterval(timeout);
            // `RtlWaitOnAddress` passes the futex address in as the first argument to this call,
            // but it's unclear what that actually does, especially since `NtAlertThreadByThreadId`
            // does *not* accept the address so the kernel can't really be using it as a hint. An
            // old Microsoft blog post discusses a more traditional futex-like mechanism in the
            // kernel which definitely isn't how `RtlWaitOnAddress` works today:
            //
            // https://devblogs.microsoft.com/oldnewthing/20160826-00/?p=94185
            //
            // ...so it's possible this argument is simply a remnant which no longer does anything
            // (perhaps the implementation changed during development but someone forgot to remove
            // this parameter). However, to err on the side of caution, let's match the behavior of
            // `RtlWaitOnAddress` and pass the pointer, in case the kernel ever does something
            // stupid such as trying to dereference it.
            switch (windows.ntdll.NtWaitForAlertByThreadId(
                addr_hint,
                if (raw_timeout) |*t| t else null,
            )) {
                .ALERTED => return,
                .TIMEOUT => return error.Timeout,
                else => unreachable,
            }
        },
        .netbsd => {
            var ts_buf: posix.timespec = undefined;
            const ts: ?*posix.timespec, const abstime: bool, const clock_real: bool = switch (timeout) {
                .none => .{ null, false, false },
                .deadline => |timestamp| timeout: {
                    ts_buf = timestampToPosix(timestamp.raw.nanoseconds);
                    break :timeout .{ &ts_buf, true, timestamp.clock == .real };
                },
                .duration => |duration| timeout: {
                    ts_buf = timestampToPosix(duration.raw.nanoseconds);
                    break :timeout .{ &ts_buf, false, duration.clock == .real };
                },
            };
            // It's okay to pass the same timeout in a loop. If it's a duration, the OS actually
            // writes the remaining time into the buffer when the syscall returns.
            while (!unpark_flag.swap(false, .acquire)) {
                switch (posix.errno(std.c._lwp_park(
                    if (clock_real) .REALTIME else .MONOTONIC,
                    .{ .ABSTIME = abstime },
                    ts,
                    0,
                    addr_hint,
                    null,
                ))) {
                    .SUCCESS, .ALREADY, .INTR => {},
                    .TIMEDOUT => return error.Timeout,
                    .INVAL => unreachable,
                    .SRCH => unreachable,
                    else => unreachable,
                }
            }
        },
        .illumos => @panic("TODO: illumos lwp_park"),
        else => comptime unreachable,
    }
}
/// `addr_hint` has no semantic effect, but may allow the OS to optimize this operation.
fn unpark(tids: []const UnparkTid, addr_hint: ?*const anyopaque) void {
    comptime assert(use_parking_futex or use_parking_sleep);
    switch (native_os) {
        .windows => {
            // TODO: this condition is currently disabled because mingw-w64 does not contain this
            // symbol. Once it's added, enable this check to use the new bulk API where possible.
            if (false and (builtin.os.version_range.windows.isAtLeast(.win11_dt) orelse false)) {
                _ = windows.ntdll.NtAlertMultipleThreadByThreadId(tids.ptr, @intCast(tids.len), null, null);
            } else {
                for (tids) |tid| {
                    _ = windows.ntdll.NtAlertThreadByThreadId(@intCast(tid));
                }
            }
        },
        .netbsd => {
            switch (posix.errno(std.c._lwp_unpark_all(@ptrCast(tids.ptr), tids.len, addr_hint))) {
                .SUCCESS => return,
                // For errors, fall through to a loop over `tids`, though this is only expected to
                // be possible for ENOMEM (even that is questionable) and ESRCH (see comment below).
                .SRCH => {},
                .FAULT => recoverableOsBugDetected(),
                .INVAL => recoverableOsBugDetected(),
                .NOMEM => {},
                else => recoverableOsBugDetected(),
            }
            for (tids) |tid| {
                switch (posix.errno(std.c._lwp_unpark(@bitCast(tid), addr_hint))) {
                    .SUCCESS => {},
                    .SRCH => {
                        // This can happen in a rare race: the thread might have been spuriously
                        // unparked, so already observed the changing status, and from there have
                        // exited. That's okay, because the thread has woken up like we wanted.
                    },
                    else => recoverableOsBugDetected(),
                }
            }
        },
        .illumos => @panic("TODO: illumos lwp_unpark"),
        else => comptime unreachable,
    }
}

pub const PipeError = error{
    SystemFdQuotaExceeded,
    ProcessFdQuotaExceeded,
} || Io.UnexpectedError;

pub fn pipe2(flags: posix.O) PipeError![2]posix.fd_t {
    var fds: [2]posix.fd_t = undefined;

    if (@TypeOf(posix.system.pipe2) != void) {
        switch (posix.errno(posix.system.pipe2(&fds, flags))) {
            .SUCCESS => return fds,
            .INVAL => |err| return errnoBug(err), // Invalid flags
            .NFILE => return error.SystemFdQuotaExceeded,
            .MFILE => return error.ProcessFdQuotaExceeded,
            else => |err| return posix.unexpectedErrno(err),
        }
    }

    switch (posix.errno(posix.system.pipe(&fds))) {
        .SUCCESS => {},
        .NFILE => return error.SystemFdQuotaExceeded,
        .MFILE => return error.ProcessFdQuotaExceeded,
        else => |err| return posix.unexpectedErrno(err),
    }
    errdefer {
        closeFd(fds[0]);
        closeFd(fds[1]);
    }

    // https://github.com/ziglang/zig/issues/18882
    if (@as(u32, @bitCast(flags)) == 0) return fds;

    // CLOEXEC is special, it's a file descriptor flag and must be set using
    // F.SETFD.
    if (flags.CLOEXEC) for (fds) |fd| {
        switch (posix.errno(posix.system.fcntl(fd, posix.F.SETFD, @as(u32, posix.FD_CLOEXEC)))) {
            .SUCCESS => {},
            else => |err| return posix.unexpectedErrno(err),
        }
    };

    const new_flags: u32 = f: {
        var new_flags = flags;
        new_flags.CLOEXEC = false;
        break :f @bitCast(new_flags);
    };

    // Set every other flag affecting the file status using F.SETFL.
    if (new_flags != 0) for (fds) |fd| {
        switch (posix.errno(posix.system.fcntl(fd, posix.F.SETFL, new_flags))) {
            .SUCCESS => {},
            .INVAL => |err| return errnoBug(err),
            else => |err| return posix.unexpectedErrno(err),
        }
    };

    return fds;
}

pub const DupError = error{
    ProcessFdQuotaExceeded,
    SystemResources,
} || Io.UnexpectedError || Io.Cancelable;

pub fn dup2(old_fd: posix.fd_t, new_fd: posix.fd_t) DupError!void {
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.dup2(old_fd, new_fd))) {
        .SUCCESS => return syscall.finish(),
        .BUSY, .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .INVAL => |err| return syscall.errnoBug(err), // invalid parameters
        .BADF => |err| return syscall.errnoBug(err), // use after free
        .MFILE => return syscall.fail(error.ProcessFdQuotaExceeded),
        .NOMEM => return syscall.fail(error.SystemResources),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

pub const FchdirError = error{
    AccessDenied,
    NotDir,
    FileSystem,
} || Io.Cancelable || Io.UnexpectedError;

pub fn fchdir(fd: posix.fd_t) FchdirError!void {
    if (fd == posix.AT.FDCWD) return;
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.fchdir(fd))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .ACCES => return syscall.fail(error.AccessDenied),
        .NOTDIR => return syscall.fail(error.NotDir),
        .IO => return syscall.fail(error.FileSystem),
        .BADF => |err| return syscall.errnoBug(err),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

pub const ChdirError = error{
    AccessDenied,
    FileSystem,
    SymLinkLoop,
    NameTooLong,
    FileNotFound,
    SystemResources,
    NotDir,
    BadPathName,
} || Io.Cancelable || Io.UnexpectedError;

pub fn chdir(dir_path: []const u8) ChdirError!void {
    var path_buffer: [posix.PATH_MAX]u8 = undefined;
    const dir_path_posix = try pathToPosix(dir_path, &path_buffer);
    const syscall: Syscall = try .start();
    while (true) switch (posix.errno(posix.system.chdir(dir_path_posix))) {
        .SUCCESS => return syscall.finish(),
        .INTR => {
            try syscall.checkCancel();
            continue;
        },
        .ACCES => return syscall.fail(error.AccessDenied),
        .IO => return syscall.fail(error.FileSystem),
        .LOOP => return syscall.fail(error.SymLinkLoop),
        .NAMETOOLONG => return syscall.fail(error.NameTooLong),
        .NOENT => return syscall.fail(error.FileNotFound),
        .NOMEM => return syscall.fail(error.SystemResources),
        .NOTDIR => return syscall.fail(error.NotDir),
        .ILSEQ => return syscall.fail(error.BadPathName),
        .FAULT => |err| return syscall.errnoBug(err),
        else => |err| return syscall.unexpectedErrno(err),
    };
}

fn fileMemoryMapCreate(
    userdata: ?*anyopaque,
    file: File,
    options: File.MemoryMap.CreateOptions,
) File.MemoryMap.CreateError!File.MemoryMap {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const offset = options.offset;
    const len = options.len;

    if (!t.disable_memory_mapping) {
        if (createFileMap(file, options.protection, offset, options.populate, len)) |result| {
            return result;
        } else |err| switch (err) {
            error.Unseekable, error.Canceled, error.AccessDenied => |e| return e,
            error.OperationUnsupported => {},
            else => {
                if (builtin.mode == .Debug)
                    std.log.warn("memory mapping failed with {t}, falling back to file operations", .{err});
            },
        }
    }

    const gpa = t.allocator;
    const page_size = std.heap.pageSize();
    const alignment: Alignment = .fromByteUnits(page_size);
    const memory = m: {
        const ptr = gpa.rawAlloc(len, alignment, @returnAddress()) orelse return error.OutOfMemory;
        break :m ptr[0..len];
    };
    errdefer gpa.rawFree(memory, alignment, @returnAddress());

    if (!options.undefined_contents) try mmSyncRead(file, memory, offset);

    return .{
        .file = file,
        .offset = offset,
        .memory = @alignCast(memory),
        .section = null,
    };
}

const CreateFileMapError = error{
    /// MaximumSize is greater than the system-defined maximum for sections, or
    /// greater than the specified file and the section is not writable.
    SectionOversize,
    /// A file descriptor refers to a non-regular file. Or a file mapping was requested,
    /// but the file descriptor is not open for reading. Or `MAP.SHARED` was requested
    /// and `PROT_WRITE` is set, but the file descriptor is not open in `RDWR` mode.
    /// Or `PROT_WRITE` is set, but the file is append-only.
    AccessDenied,
    /// The `prot` argument asks for `PROT_EXEC` but the mapped area belongs to a file on
    /// a filesystem that was mounted no-exec.
    PermissionDenied,
    FileBusy,
    LockedMemoryLimitExceeded,
    OperationUnsupported,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    OutOfMemory,
    MappingAlreadyExists,
    Unseekable,
    LockViolation,
} || Io.Cancelable || Io.UnexpectedError;

fn createFileMap(
    file: File,
    protection: std.process.MemoryProtection,
    offset: u64,
    populate: bool,
    len: usize,
) CreateFileMapError!File.MemoryMap {
    if (is_windows) {
        try Thread.checkCancel();

        var section = windows.INVALID_HANDLE_VALUE;
        const section_size: windows.LARGE_INTEGER = @intCast(len);
        const page = windows.PAGE.fromProtection(protection) orelse return error.AccessDenied;
        switch (windows.ntdll.NtCreateSection(
            &section,
            .{
                .SPECIFIC = .{ .SECTION = .{
                    .QUERY = true,
                    .MAP_WRITE = protection.write,
                    .MAP_READ = protection.read,
                    .MAP_EXECUTE = protection.execute,
                    .EXTEND_SIZE = true,
                } },
                .STANDARD = .{ .RIGHTS = .REQUIRED },
            },
            null,
            &section_size,
            page,
            .{ .COMMIT = populate },
            file.handle,
        )) {
            .SUCCESS => {},
            .FILE_LOCK_CONFLICT => return error.LockViolation,
            .INVALID_FILE_FOR_SECTION => return error.OperationUnsupported,
            .ACCESS_DENIED => return error.AccessDenied,
            .SECTION_TOO_BIG => return error.SectionOversize,
            else => |status| return windows.unexpectedStatus(status),
        }
        var contents_ptr: ?[*]align(std.heap.page_size_min) u8 = null;
        var contents_len = len;
        switch (windows.ntdll.NtMapViewOfSection(
            section,
            windows.current_process,
            @ptrCast(&contents_ptr),
            null,
            0,
            null,
            &contents_len,
            .Unmap,
            .{},
            page,
        )) {
            .SUCCESS => {},
            .CONFLICTING_ADDRESSES => return error.MappingAlreadyExists,
            .SECTION_PROTECTION => return error.PermissionDenied,
            .ACCESS_DENIED => return error.AccessDenied,
            .INVALID_VIEW_SIZE => |status| return windows.statusBug(status),
            else => |status| return windows.unexpectedStatus(status),
        }
        if (builtin.mode == .Debug) {
            const page_size = std.heap.pageSize();
            const alignment: Alignment = .fromByteUnits(page_size);
            assert(contents_len == alignment.forward(len));
        }
        return .{
            .file = file,
            .offset = offset,
            .memory = contents_ptr.?[0..len],
            .section = section,
        };
    } else if (have_mmap) {
        const prot: posix.PROT = .{
            .READ = protection.read,
            .WRITE = protection.write,
            .EXEC = protection.execute,
        };
        const flags: posix.MAP = switch (native_os) {
            .linux => .{
                .TYPE = .SHARED_VALIDATE,
                .POPULATE = populate,
            },
            else => .{
                .TYPE = .SHARED,
            },
        };

        const page_align = std.heap.page_size_min;

        const contents = while (true) {
            const syscall: Syscall = try .start();
            const casted_offset = std.math.cast(i64, offset) orelse return error.Unseekable;
            const rc = mmap_sym(null, len, prot, flags, file.handle, casted_offset);
            syscall.finish();
            const err: posix.E = if (builtin.link_libc) e: {
                if (rc != std.c.MAP_FAILED) {
                    break @as([*]align(page_align) u8, @ptrCast(@alignCast(rc)))[0..len];
                }
                break :e @enumFromInt(posix.system._errno().*);
            } else e: {
                const err = posix.errno(rc);
                if (err == .SUCCESS) {
                    break @as([*]align(page_align) u8, @ptrFromInt(rc))[0..len];
                }
                break :e err;
            };
            switch (err) {
                .SUCCESS => unreachable,
                .INTR => continue,
                .ACCES => return error.AccessDenied,
                .AGAIN => return error.LockedMemoryLimitExceeded,
                .EXIST => return error.MappingAlreadyExists,
                .MFILE => return error.ProcessFdQuotaExceeded,
                .NFILE => return error.SystemFdQuotaExceeded,
                .NODEV => return error.OperationUnsupported,
                .NOMEM => return error.OutOfMemory,
                .PERM => return error.PermissionDenied,
                .TXTBSY => return error.FileBusy,
                .OVERFLOW => return error.Unseekable,
                .BADF => return errnoBug(err), // Always a race condition.
                .INVAL => return errnoBug(err), // Invalid parameters to mmap()
                .OPNOTSUPP => return errnoBug(err), // Bad flags with MAP.SHARED_VALIDATE on Linux.
                else => return posix.unexpectedErrno(err),
            }
        };
        return .{
            .file = file,
            .offset = offset,
            .memory = contents,
            .section = {},
        };
    }

    return error.OperationUnsupported;
}

fn fileMemoryMapDestroy(userdata: ?*anyopaque, mm: *File.MemoryMap) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const memory = mm.memory;
    if (mm.section) |section| switch (native_os) {
        .windows => {
            if (section == windows.INVALID_HANDLE_VALUE) return;
            _ = windows.ntdll.NtUnmapViewOfSection(windows.current_process, memory.ptr);
            windows.CloseHandle(section);
        },
        .wasi => unreachable,
        else => {
            if (memory.len == 0) return;
            switch (posix.errno(posix.system.munmap(memory.ptr, memory.len))) {
                .SUCCESS => {},
                else => |e| {
                    if (builtin.mode == .Debug)
                        std.log.err("failed to unmap {d} bytes at {*}: {t}", .{ memory.len, memory.ptr, e });
                },
            }
        },
    } else {
        const gpa = t.allocator;
        gpa.rawFree(memory, .fromByteUnits(std.heap.pageSize()), @returnAddress());
    }
    mm.* = undefined;
}

fn fileMemoryMapSetLength(
    userdata: ?*anyopaque,
    mm: *File.MemoryMap,
    new_len: usize,
) File.MemoryMap.SetLengthError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const page_size = std.heap.pageSize();
    const alignment: Alignment = .fromByteUnits(page_size);
    const page_align = std.heap.page_size_min;
    const old_memory = mm.memory;

    if (mm.section) |section| {
        _ = section;
        if (alignment.forward(new_len) == alignment.forward(old_memory.len)) {
            mm.memory.len = new_len;
            return;
        }
        switch (native_os) {
            .wasi => unreachable,
            .linux => {
                const flags: posix.MREMAP = .{ .MAYMOVE = true };
                const addr_hint: ?[*]const u8 = null;
                const new_memory = while (true) {
                    const syscall: Syscall = try .start();
                    const rc = posix.system.mremap(old_memory.ptr, old_memory.len, new_len, flags, addr_hint);
                    syscall.finish();
                    const err: posix.E = if (builtin.link_libc) e: {
                        if (rc != std.c.MAP_FAILED) break @as([*]align(page_align) u8, @ptrCast(@alignCast(rc)))[0..new_len];
                        break :e @enumFromInt(posix.system._errno().*);
                    } else e: {
                        const err = posix.errno(rc);
                        if (err == .SUCCESS) break @as([*]align(page_align) u8, @ptrFromInt(rc))[0..new_len];
                        break :e err;
                    };
                    switch (err) {
                        .SUCCESS => unreachable,
                        .INTR => continue,
                        .AGAIN => return error.LockedMemoryLimitExceeded,
                        .NOMEM => return error.OutOfMemory,
                        .INVAL => return errnoBug(err),
                        .FAULT => return errnoBug(err),
                        else => return posix.unexpectedErrno(err),
                    }
                };
                mm.memory = new_memory;
                return;
            },
            else => return error.OperationUnsupported,
        }
    } else {
        const gpa = t.allocator;
        if (gpa.rawRemap(old_memory, alignment, new_len, @returnAddress())) |new_ptr| {
            mm.memory = @alignCast(new_ptr[0..new_len]);
        } else {
            const new_ptr: [*]align(page_align) u8 = @alignCast(
                gpa.rawAlloc(new_len, alignment, @returnAddress()) orelse return error.OutOfMemory,
            );
            const copy_len = @min(new_len, old_memory.len);
            @memcpy(new_ptr[0..copy_len], old_memory[0..copy_len]);
            mm.memory = new_ptr[0..new_len];
            gpa.rawFree(old_memory, alignment, @returnAddress());
        }
    }
}

fn fileMemoryMapRead(userdata: ?*anyopaque, mm: *File.MemoryMap) File.ReadPositionalError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const section = mm.section orelse return mmSyncRead(mm.file, mm.memory, mm.offset);
    _ = section;
}

fn fileMemoryMapWrite(userdata: ?*anyopaque, mm: *File.MemoryMap) File.WritePositionalError!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const section = mm.section orelse return mmSyncWrite(mm.file, mm.memory, mm.offset);
    _ = section;
}

fn mmSyncRead(file: File, memory: []u8, offset: u64) File.ReadPositionalError!void {
    if (is_windows) {
        var i: usize = 0;
        while (true) {
            const buf = memory[i..];
            if (buf.len == 0) break;
            const n = try readFilePositionalWindows(file, buf, offset + i);
            if (n == 0) {
                @memset(memory[i..], 0);
                break;
            }
            i += n;
        }
    } else if (native_os == .wasi and !builtin.link_libc) {
        var i: usize = 0;
        const syscall: Syscall = try .start();
        while (true) {
            const buf = memory[i..];
            if (buf.len == 0) {
                syscall.finish();
                break;
            }
            var n: usize = undefined;
            const vec: std.os.wasi.iovec_t = .{ .base = buf.ptr, .len = buf.len };
            switch (std.os.wasi.fd_pread(file.handle, (&vec)[0..1], 1, offset + i, &n)) {
                .SUCCESS => {
                    if (n == 0) {
                        syscall.finish();
                        @memset(memory[i..], 0);
                        break;
                    }
                    i += n;
                    try syscall.checkCancel();
                    continue;
                },
                .INTR, .TIMEDOUT => {
                    try syscall.checkCancel();
                    continue;
                },
                .NOTCONN => |err| return syscall.errnoBug(err), // not a socket
                .CONNRESET => |err| return syscall.errnoBug(err), // not a socket
                .BADF => |err| return syscall.errnoBug(err), // use after free
                .INVAL => |err| return syscall.errnoBug(err),
                .FAULT => |err| return syscall.errnoBug(err), // segmentation fault
                .AGAIN => |err| return syscall.errnoBug(err),
                .IO => return syscall.fail(error.InputOutput),
                .ISDIR => return syscall.fail(error.IsDir),
                .NOBUFS => return syscall.fail(error.SystemResources),
                .NOMEM => return syscall.fail(error.SystemResources),
                .NXIO => return syscall.fail(error.Unseekable),
                .SPIPE => return syscall.fail(error.Unseekable),
                .OVERFLOW => return syscall.fail(error.Unseekable),
                .NOTCAPABLE => return syscall.fail(error.AccessDenied),
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    } else {
        var i: usize = 0;
        const syscall: Syscall = try .start();
        while (true) {
            const buf = memory[i..];
            if (buf.len == 0) {
                syscall.finish();
                break;
            }
            const rc = pread_sym(file.handle, buf.ptr, buf.len, @intCast(offset + i));
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    const n: usize = @intCast(rc);
                    if (n == 0) {
                        syscall.finish();
                        @memset(memory[i..], 0);
                        break;
                    }
                    i += n;
                    try syscall.checkCancel();
                    continue;
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
                .BADF => |err| return syscall.errnoBug(err), // use after free
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    }
}

fn mmSyncWrite(file: File, memory: []u8, offset: u64) File.WritePositionalError!void {
    if (is_windows) {
        var i: usize = 0;
        while (true) {
            const buf = memory[i..];
            if (buf.len == 0) break;
            i += try writeFilePositionalWindows(file, memory[i..], offset + i);
        }
    } else if (native_os == .wasi and !builtin.link_libc) {
        var i: usize = 0;
        var n: usize = undefined;
        const syscall: Syscall = try .start();
        while (true) {
            const buf = memory[i..];
            if (buf.len == 0) {
                syscall.finish();
                break;
            }
            const iovec: std.os.wasi.ciovec_t = .{ .base = buf.ptr, .len = buf.len };
            switch (std.os.wasi.fd_pwrite(file.handle, (&iovec)[0..1], 1, offset + i, &n)) {
                .SUCCESS => {
                    i += n;
                    try syscall.checkCancel();
                    continue;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                .DQUOT => return syscall.fail(error.DiskQuota),
                .FBIG => return syscall.fail(error.FileTooBig),
                .IO => return syscall.fail(error.InputOutput),
                .NOSPC => return syscall.fail(error.NoSpaceLeft),
                .PERM => return syscall.fail(error.PermissionDenied),
                .PIPE => return syscall.fail(error.BrokenPipe),
                .NOTCAPABLE => return syscall.fail(error.AccessDenied),
                .NXIO => return syscall.fail(error.Unseekable),
                .SPIPE => return syscall.fail(error.Unseekable),
                .OVERFLOW => return syscall.fail(error.Unseekable),
                .INVAL => |err| return syscall.errnoBug(err),
                .FAULT => |err| return syscall.errnoBug(err),
                .AGAIN => |err| return syscall.errnoBug(err),
                .BADF => |err| return syscall.errnoBug(err), // use after free
                .DESTADDRREQ => |err| return syscall.errnoBug(err), // not a socket
                else => |err| return syscall.unexpectedErrno(err),
            }
        }
    } else {
        var i: usize = 0;
        const syscall: Syscall = try .start();
        while (true) {
            const buf = memory[i..];
            if (buf.len == 0) {
                syscall.finish();
                break;
            }
            const rc = pwrite_sym(file.handle, buf.ptr, buf.len, @intCast(offset + i));
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    const n: usize = @bitCast(rc);
                    i += n;
                    try syscall.checkCancel();
                    continue;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                .INVAL => |err| return syscall.errnoBug(err),
                .FAULT => |err| return syscall.errnoBug(err),
                .DESTADDRREQ => |err| return syscall.errnoBug(err), // not a socket
                .CONNRESET => |err| return syscall.errnoBug(err), // not a socket
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
}

fn deviceIoControl(o: *const Io.Operation.DeviceIoControl) Io.Cancelable!Io.Operation.DeviceIoControl.Result {
    if (is_windows) {
        const NtControlFile = switch (o.code.DeviceType) {
            .FILE_SYSTEM, .NAMED_PIPE => &windows.ntdll.NtFsControlFile,
            else => &windows.ntdll.NtDeviceIoControlFile,
        };
        var iosb: windows.IO_STATUS_BLOCK = undefined;
        if (o.file.flags.nonblocking) {
            var done: bool = false;
            switch (NtControlFile(
                o.file.handle,
                null, // event
                flagApc,
                &done, // APC context
                &iosb,
                o.code,
                if (o.in.len > 0) o.in.ptr else null,
                @intCast(o.in.len),
                if (o.out.len > 0) o.out.ptr else null,
                @intCast(o.out.len),
            )) {
                // We must wait for the APC routine.
                .PENDING, .SUCCESS => while (!done) {
                    // Once we get here we must not return from the function until the
                    // operation completes, thereby releasing reference to io_status_block.
                    const alertable_syscall = AlertableSyscall.start() catch |err| switch (err) {
                        error.Canceled => |e| {
                            var cancel_iosb: windows.IO_STATUS_BLOCK = undefined;
                            _ = windows.ntdll.NtCancelIoFileEx(o.file.handle, &iosb, &cancel_iosb);
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
            while (true) switch (NtControlFile(
                o.file.handle,
                null, // event
                null, // APC routine
                null, // APC context
                &iosb,
                o.code,
                if (o.in.len > 0) o.in.ptr else null,
                @intCast(o.in.len),
                if (o.out.len > 0) o.out.ptr else null,
                @intCast(o.out.len),
            )) {
                .PENDING => unreachable, // unrecoverable: wrong asynchronous flag
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
        return iosb;
    } else {
        const syscall: Syscall = try .start();
        while (true) {
            const rc = posix.system.ioctl(o.file.handle, @bitCast(o.code), @intFromPtr(o.arg));
            switch (posix.errno(rc)) {
                .SUCCESS => {
                    syscall.finish();
                    if (@TypeOf(rc) == usize) return @bitCast(@as(u32, @truncate(rc)));
                    return rc;
                },
                .INTR => {
                    try syscall.checkCancel();
                    continue;
                },
                else => |err| {
                    syscall.finish();
                    return -@as(i32, @intFromEnum(err));
                },
            }
        }
    }
}

const WaitGroup = struct {
    state: std.atomic.Value(usize),
    event: Io.Event,

    const init: WaitGroup = .{ .state = .{ .raw = 0 }, .event = .unset };

    const is_waiting: usize = 1 << 0;
    const one_pending: usize = 1 << 1;

    fn start(wg: *WaitGroup) void {
        const prev_state = wg.state.fetchAdd(one_pending, .monotonic);
        assert((prev_state / one_pending) < (std.math.maxInt(usize) / one_pending));
    }

    fn value(wg: *WaitGroup) usize {
        return wg.state.load(.monotonic) / one_pending;
    }

    fn wait(wg: *WaitGroup) void {
        const prev_state = wg.state.fetchAdd(is_waiting, .acquire);
        assert(prev_state & is_waiting == 0);
        if ((prev_state / one_pending) > 0) eventWait(&wg.event);
    }

    fn finish(wg: *WaitGroup) void {
        const state = wg.state.fetchSub(one_pending, .acq_rel);
        assert((state / one_pending) > 0);

        if (state == (one_pending | is_waiting)) {
            eventSet(&wg.event);
        }
    }
};

/// Same as `Io.Event.wait` but avoids the VTable.
fn eventWait(event: *Io.Event) void {
    if (@cmpxchgStrong(Io.Event, event, .unset, .waiting, .acquire, .acquire)) |prev| switch (prev) {
        .unset => unreachable,
        .waiting => {},
        .is_set => return,
    };
    while (true) {
        Thread.futexWaitUncancelable(@ptrCast(event), @intFromEnum(Io.Event.waiting), null);
        switch (@atomicLoad(Io.Event, event, .acquire)) {
            .unset => unreachable, // `reset` called before pending `wait` returned
            .waiting => continue,
            .is_set => return,
        }
    }
}

/// Same as `Io.Event.set` but avoids the VTable.
fn eventSet(event: *Io.Event) void {
    switch (@atomicRmw(Io.Event, event, .Xchg, .is_set, .release)) {
        .unset, .is_set => {},
        .waiting => Thread.futexWake(@ptrCast(event), std.math.maxInt(u32)),
    }
}

/// Same as `Io.Condition.broadcast` but avoids the VTable.
fn condBroadcast(cond: *Io.Condition) void {
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
            Thread.futexWake(&cond.epoch.raw, prev_state.waiters - prev_state.signals);
            return;
        };
    }
}

/// Same as `Io.Condition.signal` but avoids the VTable.
fn condSignal(cond: *Io.Condition) void {
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
            Thread.futexWake(&cond.epoch.raw, 1);
            return;
        };
    }
}

/// Same as `Io.Condition.waitUncancelable` but avoids the VTable.
fn condWait(cond: *Io.Condition, mutex: *Io.Mutex) void {
    var epoch = cond.epoch.load(.acquire); // `.acquire` to ensure ordered before state load

    {
        const prev_state = cond.state.fetchAdd(.{ .waiters = 1, .signals = 0 }, .monotonic);
        assert(prev_state.waiters < std.math.maxInt(u16)); // overflow caused by too many waiters
    }

    mutexUnlock(mutex);
    defer mutexLock(mutex);

    while (true) {
        Thread.futexWaitUncancelable(&cond.epoch.raw, epoch, null);

        epoch = cond.epoch.load(.acquire); // `.acquire` to ensure ordered before `state` laod

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
}

/// Same as `Io.Mutex.lockUncancelable` but avoids the VTable.
pub fn mutexLock(m: *Io.Mutex) void {
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
        Thread.futexWaitUncancelable(@ptrCast(&m.state.raw), @intFromEnum(Io.Mutex.State.contended), null);
    }
    while (m.state.swap(.contended, .acquire) != .unlocked) {
        Thread.futexWaitUncancelable(@ptrCast(&m.state.raw), @intFromEnum(Io.Mutex.State.contended), null);
    }
}

/// Same as `Io.Mutex.unlock` but avoids the VTable.
pub fn mutexUnlock(m: *Io.Mutex) void {
    switch (m.state.swap(.unlocked, .release)) {
        .unlocked => unreachable,
        .locked_once => {},
        .contended => {
            @branchHint(.unlikely);
            Thread.futexWake(@ptrCast(&m.state.raw), 1);
        },
    }
}

const OpenError = error{
    IsDir,
    NotDir,
    FileNotFound,
    NoDevice,
    AccessDenied,
    PipeBusy,
    PathAlreadyExists,
    WouldBlock,
    NetworkNotFound,
    AntivirusInterference,
    FileBusy,
} || Dir.PathNameError || Io.Cancelable || Io.UnexpectedError;

const OpenFileOptions = struct {
    access_mask: windows.ACCESS_MASK,
    dir: ?windows.HANDLE = null,
    sa: ?*const windows.SECURITY_ATTRIBUTES = null,
    share_access: windows.FILE.SHARE = .VALID_FLAGS,
    creation: windows.FILE.CREATE_DISPOSITION,
    filter: Filter = .non_directory_only,
    /// If false, tries to open path as a reparse point without dereferencing it.
    /// Defaults to true.
    follow_symlinks: bool = true,

    pub const Filter = enum {
        /// Causes `OpenFile` to return `error.IsDir` if the opened handle would be a directory.
        non_directory_only,
        /// Causes `OpenFile` to return `error.NotDir` if the opened handle is not a directory.
        dir_only,
        /// `OpenFile` does not discriminate between opening files and directories.
        any,
    };
};

/// TODO: inline this logic everywhere and delete this function
fn OpenFile(sub_path_w: []const u16, options: OpenFileOptions) OpenError!windows.HANDLE {
    if (std.mem.eql(u16, sub_path_w, &.{'.'}) and options.filter == .non_directory_only) {
        return error.IsDir;
    }
    if (std.mem.eql(u16, sub_path_w, &.{ '.', '.' }) and options.filter == .non_directory_only) {
        return error.IsDir;
    }

    var result: windows.HANDLE = undefined;

    const attr: windows.OBJECT.ATTRIBUTES = .{
        .RootDirectory = if (Dir.path.isAbsoluteWindowsWtf16(sub_path_w)) null else options.dir,
        .Attributes = .{ .INHERIT = if (options.sa) |sa| sa.bInheritHandle.toBool() else false },
        .ObjectName = @constCast(&windows.UNICODE_STRING.init(sub_path_w)),
        .SecurityDescriptor = if (options.sa) |ptr| ptr.lpSecurityDescriptor else null,
    };

    var iosb: windows.IO_STATUS_BLOCK = undefined;
    var attempt: u5 = 0;
    var syscall: Syscall = try .start();
    while (true) {
        switch (windows.ntdll.NtCreateFile(
            &result,
            options.access_mask,
            &attr,
            &iosb,
            null,
            .{ .NORMAL = true },
            options.share_access,
            options.creation,
            .{
                .DIRECTORY_FILE = options.filter == .dir_only,
                .NON_DIRECTORY_FILE = options.filter == .non_directory_only,
                .IO = if (options.follow_symlinks) .SYNCHRONOUS_NONALERT else .ASYNCHRONOUS,
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
                // call has failed. There is not really a sane way to handle
                // this other than retrying the creation after the OS finishes
                // the deletion.
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
            .INVALID_PARAMETER => |status| return syscall.ntstatusBug(status),
            .OBJECT_PATH_SYNTAX_BAD => |status| return syscall.ntstatusBug(status),
            .INVALID_HANDLE => |status| return syscall.ntstatusBug(status),
            else => |status| return syscall.unexpectedNtstatus(status),
        }
    }
}

pub fn closeFd(fd: posix.fd_t) void {
    if (native_os == .wasi and !builtin.link_libc) {
        switch (std.os.wasi.fd_close(fd)) {
            .SUCCESS, .INTR => {},
            .BADF => recoverableOsBugDetected(), // use after free
            else => recoverableOsBugDetected(), // unexpected failure
        }
    } else switch (posix.errno(posix.system.close(fd))) {
        .SUCCESS, .INTR => {}, // INTR still a success, see https://github.com/ziglang/zig/issues/2425
        .BADF => recoverableOsBugDetected(), // use after free
        else => recoverableOsBugDetected(), // unexpected failure
    }
}



---
File: /std/Io/Uring.zig
---

const addressFromPosix = Io.Threaded.addressFromPosix;
const addressToPosix = Io.Threaded.addressToPosix;
const Alignment = std.mem.Alignment;
const Allocator = std.mem.Allocator;
const Argv0 = Io.Threaded.Argv0;
const assert = std.debug.assert;
const builtin = @import("builtin");
const ChdirError = Io.Threaded.ChdirError;
const clockToPosix = Io.Threaded.clockToPosix;
const Csprng = Io.Threaded.Csprng;
const default_PATH = Io.Threaded.default_PATH;
const Dir = Io.Dir;
const Environ = Io.Threaded.Environ;
const errnoBug = Io.Threaded.errnoBug;
const Evented = @This();
const fallbackSeed = Io.Threaded.fallbackSeed;
const fd_t = linux.fd_t;
const File = Io.File;
const Io = std.Io;
const IoUring = linux.IoUring;
const iovec = std.posix.iovec;
const iovec_const = std.posix.iovec_const;
const linux = std.os.linux;
const linux_statx_request = Io.Threaded.linux_statx_request;
const LOCK = std.posix.LOCK;
const log = std.log.scoped(.@"io-uring");
const max_iovecs_len = Io.Threaded.max_iovecs_len;
const nanosecondsFromPosix = Io.Threaded.nanosecondsFromPosix;
const net = Io.net;
const PATH_MAX = linux.PATH_MAX;
const pathToPosix = Io.Threaded.pathToPosix;
const pid_t = linux.pid_t;
const PosixAddress = Io.Threaded.PosixAddress;
const posixAddressFamily = Io.Threaded.posixAddressFamily;
const posixSocketModeProtocol = Io.Threaded.posixSocketModeProtocol;
const process = std.process;
const recoverableOsBugDetected = Io.Threaded.recoverableOsBugDetected;
const setTimestampToPosix = Io.Threaded.setTimestampToPosix;
const splat_buffer_size = Io.Threaded.splat_buffer_size;
const statFromLinux = Io.Threaded.statFromLinux;
const statxKind = Io.Threaded.statxKind;
const std = @import("../std.zig");
const timestampFromPosix = Io.Threaded.timestampFromPosix;
const unexpectedErrno = std.posix.unexpectedErrno;
const winsize = std.posix.winsize;

const tracy = if (@hasDecl(@import("root"), "tracy")) @import("root").tracy else struct {
    const enable = false;
    inline fn fiberEnter(fiber: [*:0]const u8) void {
        _ = fiber;
    }
    inline fn fiberLeave() void {}
};

/// Empirically saw >128KB being used by the self-hosted backend to panic.
/// Empirically saw glibc complain about 256KB.
const idle_stack_size = 512 * 1024;

const max_idle_search = 1;
const max_steal_ready_search = 2;
const max_steal_free_search = 4;

backing_allocator_needs_mutex: bool,
backing_allocator_mutex: Io.Mutex,
/// Does not need to be thread-safe if not used elsewhere.
backing_allocator: Allocator,
main_fiber_buffer: [
    std.mem.alignForward(usize, @sizeOf(Fiber), @alignOf(Completion)) + @sizeOf(Completion)
]u8 align(@max(@alignOf(Fiber), @alignOf(Completion))),
log2_ring_entries: u4,
threads: Thread.List,
sync_limit: ?Io.Semaphore,

stderr_writer_initialized: bool = false,
stderr_mutex: Io.Mutex,
stderr_writer: File.Writer = .{
    .io = undefined,
    .interface = Io.File.Writer.initInterface(&.{}),
    .file = .stderr(),
    .mode = .streaming,
},
stderr_mode: Io.Terminal.Mode = .no_color,

environ_mutex: Io.Mutex,
environ_initialized: bool,
environ: Environ,

null_fd: CachedFd,
random_fd: CachedFd,

csprng_mutex: Io.Mutex,
csprng: Csprng,

const Thread = struct {
    required_align: void align(4),
    thread: std.Thread,
    idle_context: Io.fiber.Context,
    current_context: *Io.fiber.Context,
    ready_queue: ?*Fiber,
    free_queue: ?*Fiber,
    io_uring: IoUring,
    idle_search_index: u32,
    steal_ready_search_index: u32,
    steal_free_search_index: u32,
    name_arena: if (tracy.enable) std.heap.ArenaAllocator.State else struct {},
    csprng: Csprng,

    threadlocal var self: ?*Thread = null;

    noinline fn current() *Thread {
        return self.?;
    }

    fn deinit(thread: *Thread, gpa: Allocator) void {
        var next_fiber = thread.free_queue;
        while (next_fiber) |free_fiber| {
            next_fiber = free_fiber.status.free_next;
            gpa.free(free_fiber.allocatedSlice());
        }
        thread.io_uring.deinit();
    }

    fn currentFiber(thread: *Thread) *Fiber {
        assert(thread.current_context != &thread.idle_context);
        return @fieldParentPtr("context", thread.current_context);
    }

    fn enqueue(thread: *Thread) *linux.io_uring_sqe {
        while (true) return thread.io_uring.get_sqe() catch {
            thread.submit();
            continue;
        };
    }

    fn submit(thread: *Thread) void {
        _ = thread.io_uring.submit() catch |err| switch (err) {
            error.SignalInterrupt => {},
            else => |e| @panic(@errorName(e)),
        };
    }

    const List = struct {
        allocated: []Thread,
        reserved: u32,
        active: u32,
    };
};

const Fiber = struct {
    required_align: void align(4),
    context: Io.fiber.Context,
    link: union {
        awaiter: ?*Fiber,
        group: struct { prev: ?*Fiber, next: ?*Fiber },
    },
    status: union(enum) {
        queue_next: ?*Fiber,
        awaiting_group: Group,
        free_next: ?*Fiber,
    },
    cancel_status: CancelStatus,
    cancel_protection: CancelProtection,
    name: if (tracy.enable) [*:0]const u8 else void,

    var next_name: u64 = 0;

    const CancelStatus = packed struct(u32) {
        requested: bool,
        awaiting: Awaiting,

        const unrequested: CancelStatus = .{ .requested = false, .awaiting = .nothing };

        const Awaiting = enum(u31) {
            nothing = std.math.maxInt(u31),
            group = std.math.maxInt(u31) - 1,
            /// An io_uring fd.
            _,

            fn subWrap(lhs: Awaiting, rhs: Awaiting) Awaiting {
                return @enumFromInt(@intFromEnum(lhs) -% @intFromEnum(rhs));
            }

            fn fromIoUringFd(fd: fd_t) Awaiting {
                const awaiting: Awaiting = @enumFromInt(fd);
                switch (awaiting) {
                    .nothing, .group => unreachable,
                    _ => return awaiting,
                }
            }

            fn toIoUringFd(awaiting: Awaiting) fd_t {
                switch (awaiting) {
                    .nothing, .group => unreachable,
                    _ => return @intFromEnum(awaiting),
                }
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
            }, .monotonic);
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
    comptime {
        assert(max_result_align.compare(.gte, .of(Completion)));
        assert(max_result_size >= @sizeOf(Completion));
    }

    fn create(ev: *Evented) error{OutOfMemory}!*Fiber {
        const thread: *Thread = .current();
        if (@atomicRmw(?*Fiber, &thread.free_queue, .Xchg, finished, .acquire)) |free_fiber| {
            assert(free_fiber != finished);
            @atomicStore(?*Fiber, &thread.free_queue, free_fiber.status.free_next, .release);
            return free_fiber;
        }
        const active_threads = @atomicLoad(u32, &ev.threads.active, .acquire);
        for (0..@min(max_steal_free_search, active_threads)) |_| {
            defer thread.steal_free_search_index += 1;
            if (thread.steal_free_search_index == active_threads) thread.steal_free_search_index = 0;
            const steal_free_search_thread =
                &ev.threads.allocated[0..active_threads][thread.steal_free_search_index];
            if (steal_free_search_thread == thread) continue;
            const free_fiber =
                @atomicLoad(?*Fiber, &steal_free_search_thread.free_queue, .monotonic) orelse continue;
            if (free_fiber == finished) continue;
            if (@cmpxchgWeak(
                ?*Fiber,
                &steal_free_search_thread.free_queue,
                free_fiber,
                null,
                .acquire,
                .monotonic,
            )) |_| continue;
            @atomicStore(?*Fiber, &thread.free_queue, free_fiber.status.free_next, .release);
            return free_fiber;
        }
        @atomicStore(?*Fiber, &thread.free_queue, null, .monotonic);
        return @ptrCast(try ev.allocator().alignedAlloc(u8, .of(Fiber), allocation_size));
    }

    fn destroy(fiber: *Fiber) void {
        const thread: *Thread = .current();
        assert(fiber.status.queue_next == null);
        fiber.status = .{ .free_next = @atomicLoad(?*Fiber, &thread.free_queue, .acquire) };
        while (true) fiber.status.free_next = @cmpxchgWeak(
            ?*Fiber,
            &thread.free_queue,
            fiber.status.free_next,
            fiber,
            .acq_rel,
            .acquire,
        ) orelse break;
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
            .{ .requested = true, .awaiting = @enumFromInt(0) },
            .acquire,
        );
        assert(!cancel_status.requested);
        switch (cancel_status.awaiting) {
            .nothing => {},
            .group => {
                // The awaiter received a cancelation request while awaiting a group,
                // so propagate the cancelation to the group.
                if (fiber.status.awaiting_group.cancel(ev, null)) {
                    fiber.status = .{ .queue_next = null };
                    _ = ev.schedule(.current(), .{ .head = fiber, .tail = fiber });
                }
            },
            _ => |awaiting| {
                const awaiting_io_uring_fd = awaiting.toIoUringFd();
                const thread: *Thread = .current();
                thread.enqueue().* = if (thread.io_uring.fd == awaiting_io_uring_fd) .{
                    .opcode = .ASYNC_CANCEL,
                    .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
                    .ioprio = 0,
                    .fd = 0,
                    .off = 0,
                    .addr = @intFromPtr(fiber),
                    .len = 0,
                    .rw_flags = 0,
                    .user_data = @intFromEnum(Completion.Userdata.wakeup),
                    .buf_index = 0,
                    .personality = 0,
                    .splice_fd_in = 0,
                    .addr3 = 0,
                    .resv = 0,
                } else .{
                    .opcode = .MSG_RING,
                    .flags = linux.IOSQE_CQE_SKIP_SUCCESS,
                    .ioprio = 0,
                    .fd = awaiting_io_uring_fd,
                    .off = @intFromPtr(fiber) | 0b01,
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

const CancelRegion = struct {
    fiber: *Fiber,
    status: Fiber.CancelStatus,
    fn init() CancelRegion {
        const fiber = Thread.current().currentFiber();
        return .{
            .fiber = fiber,
            .status = .{
                .requested = fiber.cancel_protection.check() == .unblocked,
                .awaiting = .nothing,
            },
        };
    }
    fn initBlocked() CancelRegion {
        return .{
            .fiber = Thread.current().currentFiber(),
            .status = .{ .requested = false, .awaiting = .nothing },
        };
    }
    fn deinit(cancel_region: *CancelRegion) void {
        if (cancel_region.status.requested) {
            @branchHint(.likely);
            _ = cancel_region.fiber.cancel_status.changeAwaiting(
                cancel_region.status.awaiting,
                .nothing,
            );
        }
        cancel_region.* = undefined;
    }
    fn await(cancel_region: *CancelRegion, awaiting: Fiber.CancelStatus.Awaiting) Io.Cancelable!void {
        if (!cancel_region.status.requested) {
            @branchHint(.unlikely);
            return;
        }
        const status: Fiber.CancelStatus = .{ .requested = true, .awaiting = awaiting };
        if (cancel_region.fiber.cancel_status.changeAwaiting(
            cancel_region.status.awaiting,
            status.awaiting,
        )) {
            @branchHint(.unlikely);
            cancel_region.fiber.cancel_protection.acknowledge();
            cancel_region.status = .unrequested;
            return error.Canceled;
        }
        cancel_region.status = status;
    }
    fn awaitIoUring(cancel_region: *CancelRegion) Io.Cancelable!*Thread {
        const thread: *Thread = .current();
        try cancel_region.await(.fromIoUringFd(thread.io_uring.fd));
        return thread;
    }
    fn completion(cancel_region: *const CancelRegion) Completion {
        return cancel_region.fiber.resultPointer(Completion).*;
    }
    fn errno(cancel_region: *const CancelRegion) linux.E {
        return cancel_region.completion().errno();
    }

    const Sync = struct {
        cancel_region: CancelRegion,
        fn init(ev: *Evented) Io.Cancelable!Sync {
            if (ev.sync_limit) |*sync_limit| try sync_limit.wait(ev.io());
            return .{ .cancel_region = .init() };
        }
        fn initBlocked(ev: *Evented) Sync {
            if (ev.sync_limit) |*sync_limit| sync_limit.waitUncancelable(ev.io());
            return .{ .cancel_region = .initBlocked() };
        }
        fn deinit(sync: *Sync, ev: *Evented) void {
            sync.cancel_region.deinit();
            if (ev.sync_limit) |*sync_limit| sync_limit.post(ev.io());
        }

        const Maybe = union(enum) {
            cancel_region: CancelRegion,
            sync: Sync,

            fn deinit(maybe: *Maybe, ev: *Evented) void {
                switch (maybe.*) {
                    .cancel_region => |*cancel_region| cancel_region.deinit(),
                    .sync => |*sync| sync.deinit(ev),
                }
            }

            fn enterSync(maybe: *Maybe, ev: *Evented) Io.Cancelable!*Sync {
                switch (maybe.*) {
                    .cancel_region => |cancel_region| {
                        if (ev.sync_limit) |*sync_limit| try sync_limit.wait(ev.io());
                        maybe.* = .{ .sync = .{ .cancel_region = cancel_region } };
                    },
                    .sync => {},
                }
                return &maybe.sync;
            }

            fn leaveSync(maybe: *Maybe, ev: *Evented) void {
                switch (maybe.*) {
                    .cancel_region => {},
                    .sync => |sync| {
                        if (ev.sync_limit) |*sync_limit| sync_limit.post(ev.io());
                        maybe.* = .{ .cancel_region = sync.cancel_region };
                    },
                }
            }

            fn cancelRegion(maybe: *Maybe) *CancelRegion {
                return switch (maybe.*) {
                    .cancel_region => |*cancel_region| cancel_region,
                    .sync => |*sync| &sync.cancel_region,
                };
            }
        };
    };
};

const CachedFd = struct {
    once: Once,

    const Once = enum(fd_t) {
        uninitialized = -1,
        initializing = -2,
        /// fd
        _,

        fn fromFd(fd: fd_t) Once {
            return @enumFromInt(@as(u31, @intCast(fd)));
        }

        fn toFd(once: Once) fd_t {
            return @as(u31, @intCast(@intFromEnum(once)));
        }
    };

    const init: CachedFd = .{ .once = .uninitialized };

    fn close(cached_fd: *CachedFd) void {
        switch (cached_fd.once) {
            .uninitialized => {},
            .initializing => unreachable,
            _ => |fd| {
                assert(@intFromEnum(fd) >= 0);
                _ = linux.close(@intFromEnum(fd));
                cached_fd.* = .init;
            },
        }
    }

    fn open(
        cached_fd: *CachedFd,
        ev: *Evented,
        cancel_region: *CancelRegion,
        path: [*:0]const u8,
        flags: linux.O,
    ) File.OpenError!fd_t {
        var once = @atomicLoad(Once, &cached_fd.once, .monotonic);
        while (true) {
            switch (once) {
                .uninitialized => {},
                .initializing => try futexWait(
                    ev,
                    @ptrCast(&cached_fd.once),
                    @bitCast(@intFromEnum(once)),
                    .none,
                ),
                _ => |fd| {
                    @branchHint(.likely);
                    return fd.toFd();
                },
            }
            once = @cmpxchgWeak(
                Once,
                &cached_fd.once,
                .uninitialized,
                .initializing,
                .monotonic,
                .monotonic,
            ) orelse {
                errdefer {
                    @atomicStore(Once, &cached_fd.once, .uninitialized, .monotonic);
                    futexWake(ev, @ptrCast(&cached_fd.once), 1);
                }
                const fd = ev.openat(cancel_region, linux.AT.FDCWD, path, flags, 0) catch |err| switch (err) {
                    error.OperationUnsupported => return error.Unexpected, // TMPFILE unset.
                    else => |e| return e,
                };
                @atomicStore(Once, &cached_fd.once, .fromFd(fd), .monotonic);
                futexWake(ev, @ptrCast(&cached_fd.once), std.math.maxInt(u32));
                return fd;
            };
        }
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
    const ev_io = ev.io();
    ev.backing_allocator_mutex.lockUncancelable(ev_io);
    defer ev.backing_allocator_mutex.unlock(ev_io);
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
    const ev_io = ev.io();
    ev.backing_allocator_mutex.lockUncancelable(ev_io);
    defer ev.backing_allocator_mutex.unlock(ev_io);
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
    const ev_io = ev.io();
    ev.backing_allocator_mutex.lockUncancelable(ev_io);
    defer ev.backing_allocator_mutex.unlock(ev_io);
    return ev.backing_allocator.rawRemap(memory, alignment, new_len, ret_addr);
}

fn free(userdata: *anyopaque, memory: []u8, alignment: std.mem.Alignment, ret_addr: usize) void {
    const ev: *Evented = @ptrCast(@alignCast(userdata));
    const ev_io = ev.io();
    ev.backing_allocator_mutex.lockUncancelable(ev_io);
    defer ev.backing_allocator_mutex.unlock(ev_io);
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

  
```
