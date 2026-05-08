```
onst Csprng) bool {
        return c.rng.offset != std.math.maxInt(usize);
    }
};

pub const Argv0 = switch (native_os) {
    .openbsd, .haiku => struct {
        value: ?[*:0]const u8,

        pub const empty: Argv0 = .{ .value = null };

        pub fn init(args: process.Args) Argv0 {
            return .{ .value = args.vector[0] };
        }
    },
    else => struct {
        pub const empty: Argv0 = .{};

        pub fn init(args: process.Args) Argv0 {
            _ = args;
            return .{};
        }
    },
};

pub const Environ = struct {
    /// Unmodified data directly from the OS.
    process_environ: process.Environ,
    /// Protected by `mutex`. Memoized based on `process_environ`. Tracks whether the
    /// environment variables are present, ignoring their value.
    exist: Exist = .{},
    /// Protected by `mutex`. Memoized based on `process_environ`.
    string: String = .{},
    /// ZIG_PROGRESS
    zig_progress_file: std.Progress.ParentFileError!File = error.EnvironmentVariableMissing,
    /// Protected by `mutex`. Tracks the problem, if any, that occurred when
    /// trying to scan environment variables.
    ///
    /// Errors are only possible on WASI.
    err: ?Error = null,

    pub const empty: Environ = .{ .process_environ = .empty };

    pub const Error = Allocator.Error || Io.UnexpectedError;

    pub const Exist = struct {
        NO_COLOR: bool = false,
        CLICOLOR_FORCE: bool = false,
    };

    pub const String = switch (native_os) {
        .windows, .wasi => struct {},
        else => struct {
            PATH: ?[:0]const u8 = null,
            DEBUGINFOD_CACHE_PATH: ?[:0]const u8 = null,
            XDG_CACHE_HOME: ?[:0]const u8 = null,
            HOME: ?[:0]const u8 = null,
        },
    };

    pub fn scan(environ: *Environ, allocator: Allocator) void {
        if (is_windows) {
            // This value expires with any call that modifies the environment,
            // which is outside of this Io implementation's control, so references
            // must be short-lived.
            const peb = windows.peb();
            assert(windows.ntdll.RtlEnterCriticalSection(peb.FastPebLock) == .SUCCESS);
            defer assert(windows.ntdll.RtlLeaveCriticalSection(peb.FastPebLock) == .SUCCESS);
            const ptr = peb.ProcessParameters.Environment;

            var i: usize = 0;
            while (ptr[i] != 0) {
                // There are some special environment variables that start with =,
                // so we need a special case to not treat = as a key/value separator
                // if it's the first character.
                // https://devblogs.microsoft.com/oldnewthing/20100506-00/?p=14133
                const key_start = i;
                if (ptr[i] == '=') i += 1;
                while (ptr[i] != 0 and ptr[i] != '=') : (i += 1) {}
                const key_w = ptr[key_start..i];

                const value_start = i + 1;
                while (ptr[i] != 0) : (i += 1) {} // skip over '=' and value
                const value_w = ptr[value_start..i];
                i += 1; // skip over null byte

                if (windows.eqlIgnoreCaseWtf16(key_w, &.{ 'N', 'O', '_', 'C', 'O', 'L', 'O', 'R' })) {
                    environ.exist.NO_COLOR = true;
                } else if (windows.eqlIgnoreCaseWtf16(key_w, &.{ 'C', 'L', 'I', 'C', 'O', 'L', 'O', 'R', '_', 'F', 'O', 'R', 'C', 'E' })) {
                    environ.exist.CLICOLOR_FORCE = true;
                } else if (windows.eqlIgnoreCaseWtf16(key_w, &.{ 'Z', 'I', 'G', '_', 'P', 'R', 'O', 'G', 'R', 'E', 'S', 'S' })) {
                    environ.zig_progress_file = file: {
                        var value_buf: [std.fmt.count("{d}", .{std.math.maxInt(usize)})]u8 = undefined;
                        const len = std.unicode.calcWtf8Len(value_w);
                        if (len > value_buf.len) break :file error.UnrecognizedFormat;
                        assert(std.unicode.wtf16LeToWtf8(&value_buf, value_w) == len);
                        break :file .{
                            .handle = @ptrFromInt(std.fmt.parseInt(usize, value_buf[0..len], 10) catch
                                break :file error.UnrecognizedFormat),
                            .flags = .{ .nonblocking = true },
                        };
                    };
                }
                comptime assert(@sizeOf(String) == 0);
            }
        } else if (native_os == .wasi and !builtin.link_libc) {
            var environ_size: usize = undefined;
            var environ_buf_size: usize = undefined;

            switch (std.os.wasi.environ_sizes_get(&environ_size, &environ_buf_size)) {
                .SUCCESS => {},
                else => |err| {
                    environ.err = posix.unexpectedErrno(err);
                    return;
                },
            }
            if (environ_size == 0) return;

            const wasi_environ = allocator.alloc([*:0]u8, environ_size) catch |err| {
                environ.err = err;
                return;
            };
            defer allocator.free(wasi_environ);
            const wasi_environ_buf = allocator.alloc(u8, environ_buf_size) catch |err| {
                environ.err = err;
                return;
            };
            defer allocator.free(wasi_environ_buf);

            switch (std.os.wasi.environ_get(wasi_environ.ptr, wasi_environ_buf.ptr)) {
                .SUCCESS => {},
                else => |err| {
                    environ.err = posix.unexpectedErrno(err);
                    return;
                },
            }

            for (wasi_environ) |env| {
                const pair = std.mem.sliceTo(env, 0);
                var parts = std.mem.splitScalar(u8, pair, '=');
                const key = parts.first();
                if (std.mem.eql(u8, key, "NO_COLOR")) {
                    environ.exist.NO_COLOR = true;
                } else if (std.mem.eql(u8, key, "CLICOLOR_FORCE")) {
                    environ.exist.CLICOLOR_FORCE = true;
                }
                comptime assert(@sizeOf(String) == 0);
            }
        } else {
            for (environ.process_environ.block.slice) |opt_entry| {
                const entry = opt_entry.?;
                var entry_i: usize = 0;
                while (entry[entry_i] != 0 and entry[entry_i] != '=') : (entry_i += 1) {}
                const key = entry[0..entry_i];

                var end_i: usize = entry_i;
                while (entry[end_i] != 0) : (end_i += 1) {}
                const value = entry[entry_i + 1 .. end_i :0];

                if (std.mem.eql(u8, key, "NO_COLOR")) {
                    environ.exist.NO_COLOR = true;
                } else if (std.mem.eql(u8, key, "CLICOLOR_FORCE")) {
                    environ.exist.CLICOLOR_FORCE = true;
                } else if (std.mem.eql(u8, key, "ZIG_PROGRESS")) {
                    environ.zig_progress_file = file: {
                        break :file .{
                            .handle = std.fmt.parseInt(u31, value, 10) catch
                                break :file error.UnrecognizedFormat,
                            .flags = .{ .nonblocking = true },
                        };
                    };
                } else inline for (@typeInfo(String).@"struct".fields) |field| {
                    if (std.mem.eql(u8, key, field.name)) @field(environ.string, field.name) = value;
                }
            }
        }
    }
};

pub const NullFile = switch (native_os) {
    .windows => struct {
        handle: ?windows.HANDLE = null,

        fn deinit(this: *@This()) void {
            if (this.handle) |handle| {
                windows.CloseHandle(handle);
                this.handle = null;
            }
        }
    },
    .wasi, .ios, .tvos, .visionos, .watchos => struct {
        fn deinit(this: @This()) void {
            _ = this;
        }
    },
    else => struct {
        fd: posix.fd_t = -1,

        fn deinit(this: *@This()) void {
            if (this.fd >= 0) {
                closeFd(this.fd);
                this.fd = -1;
            }
        }
    },
};

pub const RandomFile = switch (native_os) {
    .windows => NullFile,
    else => if (use_dev_urandom) NullFile else struct {
        fn deinit(this: @This()) void {
            _ = this;
        }
    },
};

pub const PipeFile = switch (native_os) {
    .windows => struct {
        handle: ?windows.HANDLE = null,

        fn deinit(this: *@This()) void {
            if (this.handle) |handle| {
                windows.CloseHandle(handle);
                this.handle = null;
            }
        }
    },
    else => struct {
        fn deinit(this: @This()) void {
            _ = this;
        }
    },
};

pub const Pid = if (native_os == .linux) enum(posix.pid_t) {
    unknown = 0,
    _,
} else enum(u0) { unknown = 0 };

pub const UseSendfile = if (have_sendfile) enum {
    enabled,
    disabled,
    pub const default: UseSendfile = .enabled;
} else enum {
    disabled,
    pub const default: UseSendfile = .disabled;
};

pub const UseCopyFileRange = if (have_copy_file_range) enum {
    enabled,
    disabled,
    pub const default: UseCopyFileRange = .enabled;
} else enum {
    disabled,
    pub const default: UseCopyFileRange = .disabled;
};

pub const UseFcopyfile = if (have_fcopyfile) enum {
    enabled,
    disabled,
    pub const default: UseFcopyfile = .enabled;
} else enum {
    disabled,
    pub const default: UseFcopyfile = .disabled;
};

pub const UseFchmodat2 = if (have_fchmodat2 and !have_fchmodat_flags) enum {
    enabled,
    disabled,
    pub const default: UseFchmodat2 = .enabled;
} else enum {
    disabled,
    pub const default: UseFchmodat2 = .disabled;
};

pub const apc_align = @max(default_fn_align, 2);

const default_fn_align = switch (builtin.mode) {
    .Debug, .ReleaseSafe, .ReleaseFast => switch (builtin.cpu.arch) {
        else => |arch| @compileError("Unsupported architecture: " ++ @tagName(arch)),
        .arm, .thumb => 4,
        .aarch64, .x86, .x86_64 => 16,
    },
    .ReleaseSmall => 1,
};

const Runnable = struct {
    node: std.SinglyLinkedList.Node,
    startFn: *const fn (*Runnable, *Thread, *Threaded) void,
};

const Group = struct {
    ptr: *Io.Group,

    /// Returns a correctly-typed pointer to the `Io.Group.token` field.
    ///
    /// The status indicates how many pending tasks are in the group, whether the group has been
    /// canceled, and whether the group has been awaited.
    ///
    /// Note that the zero value of `Status` intentionally represents the initial group state (empty
    /// with no awaiters). This is a requirement of `Io.Group`.
    fn status(g: Group) *std.atomic.Value(Status) {
        return @ptrCast(&g.ptr.token);
    }
    /// Returns a correctly-typed pointer to the `Io.Group.state` field. The double-pointer here is
    /// intentional, because the `state` field itself stores a pointer, and this function returns a
    /// pointer to that field.
    ///
    /// On completion of the whole group, if `status` indicates that there is an awaiter, the last
    /// task must increment this `u32` and do a futex wake on it to signal that awaiter.
    fn awaiter(g: Group) **std.atomic.Value(u32) {
        return @ptrCast(&g.ptr.state);
    }

    const Status = packed struct(usize) {
        num_running: @Int(.unsigned, @bitSizeOf(usize) - 2),
        have_awaiter: bool,
        canceled: bool,
    };

    const Task = struct {
        runnable: Runnable,
        group: *Io.Group,
        func: *const fn (context: *const anyopaque) void,
        context_alignment: Alignment,
        alloc_len: usize,

        /// `Task.runnable.node` is `undefined` in the created `Task`.
        fn create(
            gpa: Allocator,
            group: Group,
            context: []const u8,
            context_alignment: Alignment,
            func: *const fn (context: *const anyopaque) void,
        ) Allocator.Error!*Task {
            const max_context_misalignment = context_alignment.toByteUnits() -| @alignOf(Task);
            const worst_case_context_offset = context_alignment.forward(@sizeOf(Task) + max_context_misalignment);
            const alloc_len = worst_case_context_offset + context.len;

            const task: *Task = @ptrCast(@alignCast(try gpa.alignedAlloc(u8, .of(Task), alloc_len)));
            errdefer comptime unreachable;

            task.* = .{
                .runnable = .{
                    .node = undefined,
                    .startFn = &start,
                },
                .group = group.ptr,
                .func = func,
                .context_alignment = context_alignment,
                .alloc_len = alloc_len,
            };
            @memcpy(task.contextPointer()[0..context.len], context);
            return task;
        }

        fn destroy(task: *Task, gpa: Allocator) void {
            const base: [*]align(@alignOf(Task)) u8 = @ptrCast(task);
            gpa.free(base[0..task.alloc_len]);
        }

        fn contextPointer(task: *Task) [*]u8 {
            const base: [*]u8 = @ptrCast(task);
            const offset = task.context_alignment.forward(@intFromPtr(base) + @sizeOf(Task)) - @intFromPtr(base);
            return base + offset;
        }

        fn start(r: *Runnable, thread: *Thread, t: *Threaded) void {
            const task: *Task = @fieldParentPtr("runnable", r);
            const group: Group = .{ .ptr = task.group };

            // This would be a simple store, but it's upgraded to an RMW so we can use `.acquire` to
            // enforce the ordering between this and the `group.status().load` below. Paired with
            // the `.release` rmw on `Thread.status` in `cancelThreads`, this creates a StoreLoad
            // barrier which guarantees that when a group is canceled, either we see the cancelation
            // in the group status, or the canceler sees our thread status so can directly notify us
            // of the cancelation.
            _ = thread.status.swap(.{
                .cancelation = .none,
                .awaitable = .fromGroup(group.ptr),
            }, .acquire);
            if (group.status().load(.monotonic).canceled) {
                thread.status.store(.{
                    .cancelation = .canceling,
                    .awaitable = .fromGroup(group.ptr),
                }, .monotonic);
            }

            task.func(task.contextPointer());

            thread.status.store(.{ .cancelation = .none, .awaitable = .null }, .monotonic);
            const old_status = group.status().fetchSub(.{
                .num_running = 1,
                .have_awaiter = false,
                .canceled = false,
            }, .acq_rel); // acquire `group.awaiter()`, release task results
            assert(old_status.num_running > 0);
            if (old_status.have_awaiter and old_status.num_running == 1) {
                const to_signal = group.awaiter().*;
                // `awaiter` should only be modified by us. For another thread to see `num_running`
                // drop to 0 after this point would indicate that another task started up, meaning
                // `async`/`cancel` was racing with awaited group completion.
                group.awaiter().* = undefined;
                _ = to_signal.fetchAdd(1, .release); // release results
                Thread.futexWake(&to_signal.raw, 1);
            }

            // Task completed. Self-destruct sequence initiated.
            task.destroy(t.allocator);
        }
    };

    /// Assumes the caller has already atomically updated the group status to indicate cancelation,
    /// and notifies any already-running threads of this cancelation.
    fn cancelThreads(g: Group, t: *Threaded) bool {
        var any_blocked = false;
        var it = t.worker_threads.load(.acquire); // acquire `Thread` values
        while (it) |thread| : (it = thread.next) {
            // This non-mutating RMW exists for ordering reasons: see comment in `Group.Task.start` for reasons.
            _ = thread.status.fetchOr(.{ .cancelation = @enumFromInt(0), .awaitable = .null }, .release);
            if (thread.cancelAwaitable(.fromGroup(g.ptr))) any_blocked = true;
        }
        return any_blocked;
    }

    /// Uses `Thread.signalCanceledSyscall` to signal any threads which are still blocked in a
    /// syscall for this group and have not observed a cancelation request yet. Returns `true` if
    /// more signals may be necessary, in which case the caller must call this again after a delay.
    fn signalAllCanceledSyscalls(g: Group, t: *Threaded) bool {
        var any_signaled = false;
        var it = t.worker_threads.load(.acquire); // acquire `Thread` values
        while (it) |thread| : (it = thread.next) {
            if (thread.signalCanceledSyscall(t, .fromGroup(g.ptr))) any_signaled = true;
        }
        return any_signaled;
    }

    /// The caller has canceled `g`. Inform any threads working on that group of the cancelation if
    /// necessary, and wait for `g` to finish (indicated by `num_completed` being incremented from 0
    /// to 1), while sending regular signals to threads if necessary for them to unblock from any
    /// cancelable syscalls.
    ///
    /// `skip_signals` means it is already known that no threads are currently working on the group
    /// so no notifications or signals are necessary.
    fn waitForCancelWithSignaling(
        g: Group,
        t: *Threaded,
        num_completed: *std.atomic.Value(u32),
        skip_signals: bool,
    ) void {
        var need_signal: bool = !skip_signals and g.cancelThreads(t);
        var timeout_ns: u64 = 1 << 10;
        while (true) {
            need_signal = need_signal and g.signalAllCanceledSyscalls(t);
            Thread.futexWaitUncancelable(&num_completed.raw, 0, if (need_signal) timeout_ns else null);
            switch (num_completed.load(.acquire)) { // acquire task results
                0 => {},
                1 => break,
                else => unreachable,
            }
            timeout_ns <<|= 1;
        }
    }
};

/// Trailing data:
/// 1. context
/// 2. result
const Future = struct {
    runnable: Runnable,
    func: *const fn (context: *const anyopaque, result: *anyopaque) void,
    status: std.atomic.Value(Status),
    /// On completion, increment this `u32` and do a futex wake on it.
    awaiter: *std.atomic.Value(u32),
    context_alignment: Alignment,
    result_offset: usize,
    alloc_len: usize,

    const Status = packed struct(usize) {
        /// The values of this enum are chosen so that await/cancel can just OR with 0b01 and 0b11
        /// respectively. That *does* clobber `.done`, but that's actually fine, because if the tag
        /// is `.done` then only the awaiter is referencing this `Future` anyway.
        tag: enum(u2) {
            /// The future is queued or running (depending on whether `thread` is set).
            pending = 0b00,
            /// Like `pending`, but the future is being awaited. `Future.awaiter` is populated.
            pending_awaited = 0b01,
            /// Like `pending`, but the future is being canceled. `Future.awaiter` is populated.
            pending_canceled = 0b11,
            /// The future has already completed. `thread` is `.null`, unless the future terminated
            /// with an acknowledged cancel request, in which case `thread` is `.all_ones`.
            done = 0b10,
        },
        /// When the future begins execution, this is atomically updated from `null` to the thread running the
        /// `Future`, so that cancelation knows which thread to cancel.
        thread: Thread.PackedPtr,
    };

    /// `Future.runnable.node` is `undefined` in the created `Future`.
    fn create(
        gpa: Allocator,
        result_len: usize,
        result_alignment: Alignment,
        context: []const u8,
        context_alignment: Alignment,
        func: *const fn (context: *const anyopaque, result: *anyopaque) void,
    ) Allocator.Error!*Future {
        const max_context_misalignment = context_alignment.toByteUnits() -| @alignOf(Future);
        const worst_case_context_offset = context_alignment.forward(@sizeOf(Future) + max_context_misalignment);
        const worst_case_result_offset = result_alignment.forward(worst_case_context_offset + context.len);
        const alloc_len = worst_case_result_offset + result_len;

        const future: *Future = @ptrCast(@alignCast(try gpa.alignedAlloc(u8, .of(Future), alloc_len)));
        errdefer comptime unreachable;

        const actual_context_addr = context_alignment.forward(@intFromPtr(future) + @sizeOf(Future));
        const actual_result_addr = result_alignment.forward(actual_context_addr + context.len);
        const actual_result_offset = actual_result_addr - @intFromPtr(future);
        future.* = .{
            .runnable = .{
                .node = undefined,
                .startFn = &start,
            },
            .func = func,
            .status = .init(.{
                .tag = .pending,
                .thread = .null,
            }),
            .awaiter = undefined,
            .context_alignment = context_alignment,
            .result_offset = actual_result_offset,
            .alloc_len = alloc_len,
        };
        @memcpy(future.contextPointer()[0..context.len], context);
        return future;
    }

    fn destroy(future: *Future, gpa: Allocator) void {
        const base: [*]align(@alignOf(Future)) u8 = @ptrCast(future);
        gpa.free(base[0..future.alloc_len]);
    }

    fn resultPointer(future: *Future) [*]u8 {
        const base: [*]u8 = @ptrCast(future);
        return base + future.result_offset;
    }

    fn contextPointer(future: *Future) [*]u8 {
        const base: [*]u8 = @ptrCast(future);
        const context_offset = future.context_alignment.forward(@intFromPtr(future) + @sizeOf(Future)) - @intFromPtr(future);
        return base + context_offset;
    }

    fn start(r: *Runnable, thread: *Thread, t: *Threaded) void {
        _ = t;
        const future: *Future = @fieldParentPtr("runnable", r);

        thread.status.store(.{
            .cancelation = .none,
            .awaitable = .fromFuture(future),
        }, .monotonic);
        {
            const old_status = future.status.fetchOr(.{
                .tag = .pending,
                .thread = .pack(thread),
            }, .release);
            assert(old_status.thread == .null);
            switch (old_status.tag) {
                .pending, .pending_awaited => {},
                .pending_canceled => thread.status.store(.{
                    .cancelation = .canceling,
                    .awaitable = .fromFuture(future),
                }, .monotonic),
                .done => unreachable,
            }
        }

        future.func(future.contextPointer(), future.resultPointer());

        const had_acknowledged_cancel = switch (thread.status.load(.monotonic).cancelation) {
            .none, .canceling => false,
            .canceled => true,
            .parked => unreachable,
            .blocked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_alertable_canceling => unreachable,
            .blocked_canceling => unreachable,
        };
        thread.status.store(.{ .cancelation = .none, .awaitable = .null }, .monotonic);
        const old_status = future.status.swap(.{
            .tag = .done,
            .thread = if (had_acknowledged_cancel) .all_ones else .null,
        }, .acq_rel); // acquire `future.awaiter`, release results
        switch (old_status.tag) {
            .pending => {},
            .pending_awaited, .pending_canceled => {
                const to_signal = future.awaiter;
                _ = to_signal.fetchAdd(1, .release); // release results
                Thread.futexWake(&to_signal.raw, 1);
            },
            .done => unreachable,
        }
    }

    /// The caller has canceled `future`. `thread` is the thread currently running that future.
    /// Inform `thread` of the cancelation if necessary, and wait for `future` to finish (indicated
    /// by `num_completed` being incremented from 0 to 1), while sending regular signals to `thread`
    /// if necessary for it to unblock from a cancelable syscall.
    fn waitForCancelWithSignaling(
        future: *Future,
        t: *Threaded,
        num_completed: *std.atomic.Value(u32),
        thread: ?*Thread,
    ) void {
        var need_signal: bool = if (thread) |th| th.cancelAwaitable(.fromFuture(future)) else false;
        var timeout_ns: u64 = 1 << 10;
        while (true) {
            need_signal = need_signal and thread.?.signalCanceledSyscall(t, .fromFuture(future));
            Thread.futexWaitUncancelable(&num_completed.raw, 0, if (need_signal) timeout_ns else null);
            switch (num_completed.load(.acquire)) { // acquire task results
                0 => {},
                1 => break,
                else => unreachable,
            }
            timeout_ns <<|= 1;
        }
    }
};

/// A sequence of (ptr_bit_width - 3) bits which uniquely identifies a group or future. The bits are
/// the MSBs of the `*Io.Group` or `*Future`. These things do not necessarily have 3 zero bits at
/// the end (they are pointer-aligned, so on 32-bit targets only have 2), but because they both have
/// a *size* of at least 8 bytes, no two groups/futures in memory at the same time will have the
/// same value for all of these bits. In other words, given a group/future pointer, the next group
/// or future must be at least 8 bytes later, so its address will have a different value for one of
/// the top (ptr_bit_width - 3) bits.
const AwaitableId = enum(@Int(.unsigned, @bitSizeOf(usize) - 3)) {
    comptime {
        assert(@sizeOf(Future) >= 8);
        assert(@sizeOf(Io.Group) >= 8);
    }
    null = 0,
    all_ones = std.math.maxInt(@Int(.unsigned, @bitSizeOf(usize) - 3)),
    _,
    const Split = packed struct(usize) { low: u3, high: AwaitableId };
    fn fromGroup(g: *Io.Group) AwaitableId {
        const split: Split = @bitCast(@intFromPtr(g));
        return split.high;
    }
    fn fromFuture(f: *Future) AwaitableId {
        const split: Split = @bitCast(@intFromPtr(f));
        return split.high;
    }
};

const Thread = struct {
    next: ?*Thread,

    id: std.Thread.Id,
    handle: Handle,

    status: std.atomic.Value(Status),

    cancel_protection: Io.CancelProtection,
    /// Always released when `Status.cancelation` is set to `.parked`.
    futex_waiter: if (use_parking_futex) ?*parking_futex.Waiter else ?noreturn,
    unpark_flag: UnparkFlag,

    csprng: Csprng,

    const Handle = Handle: {
        if (std.Thread.use_pthreads) break :Handle std.c.pthread_t;
        if (is_windows) break :Handle windows.HANDLE;
        break :Handle void;
    };

    const Status = packed struct(usize) {
        /// The specific values of these enum fields are chosen to simplify the implementation of
        /// the transformations we need to apply to this state.
        cancelation: enum(u3) {
            /// The thread has not yet been canceled, and is not in a cancelable operation.
            /// To request cancelation, just set the status to `.canceling`.
            none = 0b000,

            /// The thread is parked in a cancelable futex wait or sleep.
            /// Only applicable if `use_parking_futex` or `use_parking_sleep`.
            /// To request cancelation, set the status to `.canceling` and unpark the thread.
            /// To unpark for another reason (futex wake), set the status to `.none` and unpark the thread.
            parked = 0b001,

            /// The thread is blocked in a cancelable system call.
            /// To request cancelation, set the status to `.blocked_canceling` and repeatedly interrupt the system call until the status changes.
            blocked = 0b011,

            /// Windows-only: the thread is blocked in an alertable wait via
            /// `NtDelayExecution`. To request cancelation, set the status to
            /// `blocked_alertable_canceling` and repeatedly alert the thread
            /// until the status changes.
            blocked_alertable = 0b010,

            /// The thread has an outstanding cancelation request but is not in a cancelable operation.
            /// When it acknowledges the cancelation, it will set the status to `.canceled`.
            canceling = 0b110,

            /// The thread has received and acknowledged a cancelation request.
            /// If `recancel` is called, the status will revert to `.canceling`, but otherwise, the status
            /// will not change for the remainder of this task's execution.
            canceled = 0b111,

            /// The thread is blocked in a cancelable system call, and is being
            /// canceled. The thread which triggered the cancelation will send
            /// signals to this thread until its status changes.
            blocked_canceling = 0b101,

            /// Windows-only: the thread is blocked in an alertable wait via
            /// `NtDelayExecution`, and is being canceled. The thread which
            /// triggered the cancelation will send signals to this thread
            /// until its status changes.
            blocked_alertable_canceling = 0b100,
        },

        /// We cannot turn this value back into a pointer. Instead, it exists so that a task can be
        /// canceled by a cmpxchg on thread status: if it is running the task we want to cancel,
        /// then update the `cancelation` field.
        awaitable: AwaitableId,
    };

    const SignaleeId = if (std.Thread.use_pthreads) std.c.pthread_t else std.Thread.Id;

    threadlocal var current: ?*Thread = null;

    /// A value that does not alias any other thread id.
    const invalid_id: std.Thread.Id = std.math.maxInt(std.Thread.Id);

    fn currentId() std.Thread.Id {
        return if (current) |t| t.id else std.Thread.getCurrentId();
    }

    /// The thread is neither in a syscall nor entering one, but we want to check for cancelation
    /// anyway. If there is a pending cancel request, acknowledge it and return `error.Canceled`.
    fn checkCancel() Io.Cancelable!void {
        const thread = Thread.current orelse return;
        switch (thread.cancel_protection) {
            .blocked => return,
            .unblocked => {},
        }
        // Here, unlike `Syscall.checkCancel`, it's not particularly likely that we're canceled, so
        // it seems preferable to do a cheap atomic load and, in the unlikely case, a separate store
        // to acknowledge. Besides, the state transitions we need here can't be done with one atomic
        // OR/AND/XOR on `Status.cancelation`, so we don't actually have any other option.
        const status = thread.status.load(.monotonic);
        switch (status.cancelation) {
            .parked => unreachable,
            .blocked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_alertable_canceling => unreachable,
            .blocked_canceling => unreachable,
            .none, .canceled => {},
            .canceling => {
                thread.status.store(.{
                    .cancelation = .canceled,
                    .awaitable = status.awaitable,
                }, .monotonic);
                return error.Canceled;
            },
        }
    }

    fn futexWaitUncancelable(ptr: *const u32, expect: u32, timeout_ns: ?u64) void {
        return Thread.futexWaitInner(ptr, expect, true, timeout_ns) catch unreachable;
    }

    fn futexWait(ptr: *const u32, expect: u32, timeout_ns: ?u64) Io.Cancelable!void {
        return Thread.futexWaitInner(ptr, expect, false, timeout_ns);
    }

    fn futexWaitInner(ptr: *const u32, expect: u32, uncancelable: bool, timeout_ns: ?u64) Io.Cancelable!void {
        @branchHint(.cold);

        if (builtin.single_threaded) unreachable; // nobody would ever wake us

        if (use_parking_futex) {
            return parking_futex.wait(
                ptr,
                expect,
                uncancelable,
                if (timeout_ns) |ns| .{ .duration = .{
                    .raw = .fromNanoseconds(ns),
                    .clock = .boot,
                } } else .none,
            );
        } else if (builtin.cpu.arch.isWasm()) {
            comptime assert(builtin.cpu.has(.wasm, .atomics));
            // TODO implement cancelation for WASM futex waits by signaling the futex
            if (!uncancelable) try Thread.checkCancel();
            const to: i64 = if (timeout_ns) |ns| ns else -1;
            const signed_expect: i32 = @bitCast(expect);
            const result = asm volatile (
                \\local.get %[ptr]
                \\local.get %[expected]
                \\local.get %[timeout]
                \\memory.atomic.wait32 0
                \\local.set %[ret]
                : [ret] "=r" (-> u32),
                : [ptr] "r" (ptr),
                  [expected] "r" (signed_expect),
                  [timeout] "r" (to),
            );
            switch (result) {
                0 => {}, // ok
                1 => {}, // expected != loaded
                2 => {}, // timeout
                else => assert(!is_debug),
            }
        } else switch (native_os) {
            .linux => {
                const linux = std.os.linux;
                var ts_buffer: linux.timespec = undefined;
                const ts: ?*linux.timespec = if (timeout_ns) |ns| ts: {
                    ts_buffer = timestampToPosix(ns);
                    break :ts &ts_buffer;
                } else null;
                const syscall: Syscall = if (uncancelable) .{ .thread = null } else try .start();
                const rc = linux.futex_4arg(ptr, .{ .cmd = .WAIT, .private = true }, expect, ts);
                syscall.finish();
                switch (linux.errno(rc)) {
                    .SUCCESS => {}, // notified by `wake()`
                    .INTR => {}, // caller's responsibility to retry
                    .AGAIN => {}, // ptr.* != expect
                    .INVAL => {}, // possibly timeout overflow
                    .TIMEDOUT => {},
                    .FAULT => recoverableOsBugDetected(), // ptr was invalid
                    else => recoverableOsBugDetected(),
                }
            },
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
                const c = std.c;
                const flags: c.UL = .{
                    .op = .COMPARE_AND_WAIT,
                    .NO_ERRNO = true,
                };
                const syscall: Syscall = if (uncancelable) .{ .thread = null } else try .start();
                const status = switch (darwin_supports_ulock_wait2) {
                    true => c.__ulock_wait2(flags, ptr, expect, ns: {
                        const ns = timeout_ns orelse break :ns 0;
                        if (ns == 0) break :ns 1;
                        break :ns ns;
                    }, 0),
                    false => c.__ulock_wait(flags, ptr, expect, us: {
                        const ns = timeout_ns orelse break :us 0;
                        const us = std.math.lossyCast(u32, ns / std.time.ns_per_us);
                        if (us == 0) break :us 1;
                        break :us us;
                    }),
                };
                syscall.finish();
                if (status >= 0) return;
                switch (@as(c.E, @enumFromInt(-status))) {
                    .INTR => {}, // spurious wake
                    // Address of the futex was paged out. This is unlikely, but possible in theory, and
                    // pthread/libdispatch on darwin bother to handle it. In this case we'll return
                    // without waiting, but the caller should retry anyway.
                    .FAULT => {},
                    .TIMEDOUT => {}, // timeout
                    else => recoverableOsBugDetected(),
                }
            },
            .freebsd => {
                const flags = @intFromEnum(std.c.UMTX_OP.WAIT_UINT_PRIVATE);
                var tm_size: usize = 0;
                var tm: std.c._umtx_time = undefined;
                var tm_ptr: ?*const std.c._umtx_time = null;
                if (timeout_ns) |ns| {
                    tm_ptr = &tm;
                    tm_size = @sizeOf(@TypeOf(tm));
                    tm.flags = 0; // use relative time not UMTX_ABSTIME
                    tm.clockid = .MONOTONIC;
                    tm.timeout = timestampToPosix(ns);
                }
                const syscall: Syscall = if (uncancelable) .{ .thread = null } else try .start();
                const rc = std.c._umtx_op(@intFromPtr(ptr), flags, @as(c_ulong, expect), tm_size, @intFromPtr(tm_ptr));
                syscall.finish();
                if (is_debug) switch (posix.errno(rc)) {
                    .SUCCESS => {},
                    .FAULT => unreachable, // one of the args points to invalid memory
                    .INVAL => unreachable, // arguments should be correct
                    .TIMEDOUT => {}, // timeout
                    .INTR => {}, // spurious wake
                    else => unreachable,
                };
            },
            .openbsd => {
                var tm: std.c.timespec = undefined;
                var tm_ptr: ?*const std.c.timespec = null;
                if (timeout_ns) |ns| {
                    tm_ptr = &tm;
                    tm = timestampToPosix(ns);
                }
                const syscall: Syscall = if (uncancelable) .{ .thread = null } else try .start();
                const rc = std.c.futex(
                    ptr,
                    std.c.FUTEX.WAIT | std.c.FUTEX.PRIVATE_FLAG,
                    @as(c_int, @bitCast(expect)),
                    tm_ptr,
                    null, // uaddr2 is ignored
                );
                syscall.finish();
                if (is_debug) switch (posix.errno(rc)) {
                    .SUCCESS => {},
                    .NOSYS => unreachable, // constant op known good value
                    .AGAIN => {}, // contents of uaddr != val
                    .INVAL => unreachable, // invalid timeout
                    .TIMEDOUT => {}, // timeout
                    .INTR => {}, // a signal arrived
                    .CANCELED => {}, // a signal arrived and SA_RESTART was set
                    else => unreachable,
                };
            },
            .dragonfly => {
                var timeout_us: c_int = undefined;
                if (timeout_ns) |ns| {
                    timeout_us = std.math.cast(c_int, ns / std.time.ns_per_us) orelse std.math.maxInt(c_int);
                } else {
                    timeout_us = 0;
                }
                const syscall: Syscall = if (uncancelable) .{ .thread = null } else try .start();
                const rc = std.c.umtx_sleep(@ptrCast(ptr), @bitCast(expect), timeout_us);
                syscall.finish();
                if (is_debug) switch (std.posix.errno(rc)) {
                    .SUCCESS => {},
                    .BUSY => {}, // ptr != expect
                    .AGAIN => {}, // maybe timed out, or paged out, or hit 2s kernel refresh
                    .INTR => {}, // spurious wake
                    .INVAL => unreachable, // invalid timeout
                    else => unreachable,
                };
            },
            else => @compileError("unimplemented: futexWait"),
        }
    }

    fn futexWake(ptr: *const u32, max_waiters: u32) void {
        @branchHint(.cold);
        assert(max_waiters != 0);

        if (builtin.single_threaded) return; // nothing to wake up

        if (use_parking_futex) {
            return parking_futex.wake(ptr, max_waiters);
        } else if (builtin.cpu.arch.isWasm()) {
            comptime assert(builtin.cpu.has(.wasm, .atomics));
            const woken_count = asm volatile (
                \\local.get %[ptr]
                \\local.get %[waiters]
                \\memory.atomic.notify 0
                \\local.set %[ret]
                : [ret] "=r" (-> u32),
                : [ptr] "r" (ptr),
                  [waiters] "r" (max_waiters),
            );
            _ = woken_count; // can be 0 when linker flag 'shared-memory' is not enabled
        } else switch (native_os) {
            .linux => {
                const linux = std.os.linux;
                switch (linux.errno(linux.futex_3arg(
                    ptr,
                    .{ .cmd = .WAKE, .private = true },
                    @min(max_waiters, std.math.maxInt(i32)),
                ))) {
                    .SUCCESS => return, // successful wake up
                    .INVAL => return, // invalid futex_wait() on ptr done elsewhere
                    .FAULT => return, // pointer became invalid while doing the wake
                    else => return recoverableOsBugDetected(), // deadlock due to operating system bug
                }
            },
            .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
                const c = std.c;
                const flags: c.UL = .{
                    .op = .COMPARE_AND_WAIT,
                    .NO_ERRNO = true,
                    .WAKE_ALL = max_waiters > 1,
                };
                while (true) {
                    const status = c.__ulock_wake(flags, ptr, 0);
                    if (status >= 0) return;
                    switch (@as(c.E, @enumFromInt(-status))) {
                        .INTR, .CANCELED => continue, // spurious wake()
                        .FAULT => unreachable, // __ulock_wake doesn't generate EFAULT according to darwin pthread_cond_t
                        .NOENT => return, // nothing was woken up
                        .ALREADY => unreachable, // only for UL.Op.WAKE_THREAD
                        else => unreachable, // deadlock due to operating system bug
                    }
                }
            },
            .freebsd => {
                const rc = std.c._umtx_op(
                    @intFromPtr(ptr),
                    @intFromEnum(std.c.UMTX_OP.WAKE_PRIVATE),
                    @as(c_ulong, @min(max_waiters, std.math.maxInt(c_int))),
                    0, // there is no timeout struct
                    0, // there is no timeout struct pointer
                );
                switch (posix.errno(rc)) {
                    .SUCCESS => {},
                    .FAULT => {}, // it's ok if the ptr doesn't point to valid memory
                    .INVAL => unreachable, // arguments should be correct
                    else => unreachable, // deadlock due to operating system bug
                }
            },
            .openbsd => {
                const rc = std.c.futex(
                    ptr,
                    std.c.FUTEX.WAKE | std.c.FUTEX.PRIVATE_FLAG,
                    @min(max_waiters, std.math.maxInt(c_int)),
                    null, // timeout is ignored
                    null, // uaddr2 is ignored
                );
                assert(rc >= 0);
            },
            .dragonfly => {
                // will generally return 0 unless the address is bad
                _ = std.c.umtx_wakeup(
                    @ptrCast(ptr),
                    @min(max_waiters, std.math.maxInt(c_int)),
                );
            },
            else => @compileError("unimplemented: futexWake"),
        }
    }

    /// Cancels `thread` if it is working on `awaitable`.
    ///
    /// It is possible that `thread` gets canceled by this function, but is blocked in a syscall. In
    /// that case, the thread may need to be sent a signal to interrupt the call. This function will
    /// return `true` to indicate this, in which case the caller must call `signalCanceledSyscall`.
    fn cancelAwaitable(thread: *Thread, awaitable: AwaitableId) bool {
        var status = thread.status.load(.monotonic);
        while (true) {
            if (status.awaitable != awaitable) return false; // thread is working on something else
            status = switch (status.cancelation) {
                .none => thread.status.cmpxchgWeak(
                    .{ .cancelation = .none, .awaitable = awaitable },
                    .{ .cancelation = .canceling, .awaitable = awaitable },
                    .monotonic,
                    .monotonic,
                ) orelse return false,

                .parked => thread.status.cmpxchgWeak(
                    .{ .cancelation = .parked, .awaitable = awaitable },
                    .{ .cancelation = .canceling, .awaitable = awaitable },
                    .acquire, // acquire `thread.futex_waiter`
                    .monotonic,
                ) orelse {
                    if (!use_parking_futex and !use_parking_sleep) unreachable;
                    if (thread.futex_waiter) |futex_waiter| {
                        parking_futex.removeCanceledWaiter(futex_waiter);
                    }
                    if (need_unpark_flag) setUnparkFlag(&thread.unpark_flag);
                    unpark(&.{thread.id}, null);
                    return false;
                },

                .blocked => thread.status.cmpxchgWeak(
                    .{ .cancelation = .blocked, .awaitable = awaitable },
                    .{ .cancelation = .blocked_canceling, .awaitable = awaitable },
                    .monotonic,
                    .monotonic,
                ) orelse return true,

                .blocked_alertable => thread.status.cmpxchgWeak(
                    .{ .cancelation = .blocked_alertable, .awaitable = awaitable },
                    .{ .cancelation = .blocked_alertable_canceling, .awaitable = awaitable },
                    .monotonic,
                    .monotonic,
                ) orelse {
                    if (!is_windows) unreachable;
                    return true;
                },

                .canceling, .canceled => {
                    // This can happen when the task start raced with the cancelation, so the thread
                    // saw the cancelation on the future/group *and* we are trying to signal the
                    // thread here.
                    return false;
                },

                .blocked_canceling => unreachable, // `awaitable` has not been canceled before now
                .blocked_alertable_canceling => unreachable, // `awaitable` has not been canceled before now
            };
        }
    }

    /// Sends a signal to `thread` if it is still blocked in a syscall (i.e. has not yet observed
    /// the cancelation request from `cancelAwaitable`).
    ///
    /// Unfortunately, the signal could arrive before the syscall actually starts, so the interrupt
    /// is missed. To handle this, we may need to send multiple signals. As such, if this function
    /// returns `true`, then it should be called again after a short delay to send another signal if
    /// the thread is still blocked. For the implementation, `Future.waitForCancelWithSignaling` and
    /// `Group.waitForCancelWithSignaling`: they use exponential backoff starting at a 1us delay and
    /// doubling each call. In practice, it is rare to send more than one signal.
    fn signalCanceledSyscall(thread: *Thread, t: *Threaded, awaitable: AwaitableId) bool {
        const status = thread.status.load(.monotonic);
        if (status.awaitable != awaitable) {
            // The thread has moved on and is working on something totally different.
            return false;
        }

        // The thread ID and/or handle can be read non-atomically because they never change and were
        // released by the store that made `thread` available to us.

        switch (status.cancelation) {
            .blocked_canceling => if (std.Thread.use_pthreads) {
                return switch (std.c.pthread_kill(thread.handle, .IO)) {
                    0 => true,
                    else => false,
                };
            } else switch (native_os) {
                .linux => {
                    const pid: posix.pid_t = pid: {
                        const cached_pid = @atomicLoad(Pid, &t.pid, .monotonic);
                        if (cached_pid != .unknown) break :pid @intFromEnum(cached_pid);
                        const pid = std.os.linux.getpid();
                        @atomicStore(Pid, &t.pid, @enumFromInt(pid), .monotonic);
                        break :pid pid;
                    };
                    return switch (std.os.linux.tgkill(pid, @bitCast(thread.id), .IO)) {
                        0 => true,
                        else => false,
                    };
                },
                .windows => {
                    var iosb: windows.IO_STATUS_BLOCK = undefined;
                    return switch (windows.ntdll.NtCancelSynchronousIoFile(thread.handle, null, &iosb)) {
                        .NOT_FOUND => true, // this might mean the operation hasn't started yet
                        .SUCCESS => false, // the OS confirmed that our cancelation worked
                        else => false,
                    };
                },
                else => return false,
            },

            .blocked_alertable_canceling => {
                if (!is_windows) unreachable;
                return switch (windows.ntdll.NtAlertThread(thread.handle)) {
                    .SUCCESS => true,
                    else => false,
                };
            },

            else => {
                // The thread is working on `awaitable`, but no longer needs signaling (they already
                // woke up and saw the cancelation).
                return false;
            },
        }
    }

    /// Like a `*Thread`, but 2 bits smaller than a pointer (because the LSBs are always 0 due to
    /// alignment) so that those two bits can be used in a `packed struct`.
    const PackedPtr = enum(@Int(.unsigned, @bitSizeOf(usize) - 2)) {
        null = 0,
        all_ones = std.math.maxInt(@Int(.unsigned, @bitSizeOf(usize) - 2)),
        _,

        const Split = packed struct(usize) { low: u2, high: PackedPtr };
        fn pack(ptr: *Thread) PackedPtr {
            const split: Split = @bitCast(@intFromPtr(ptr));
            assert(split.low == 0);
            return split.high;
        }
        fn unpack(ptr: PackedPtr) ?*Thread {
            const split: Split = .{ .low = 0, .high = ptr };
            return @ptrFromInt(@as(usize, @bitCast(split)));
        }
    };
};

const Syscall = struct {
    thread: ?*Thread,
    /// Marks entry to a syscall region. This should be tightly scoped around the actual syscall
    /// to minimize races. The syscall must be marked as "finished" by `checkCancel`, `finish`,
    /// or one of the wrappers of `finish`.
    fn start() Io.Cancelable!Syscall {
        const thread = Thread.current orelse return .{ .thread = null };
        switch (thread.cancel_protection) {
            .blocked => return .{ .thread = null },
            .unblocked => {},
        }
        switch (thread.status.fetchOr(.{
            .cancelation = @enumFromInt(0b011),
            .awaitable = .null,
        }, .monotonic).cancelation) {
            .parked => unreachable,
            .blocked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_alertable_canceling => unreachable,
            .blocked_canceling => unreachable,
            .none => return .{ .thread = thread }, // new status is `.blocked`
            .canceling => return error.Canceled, // new status is `.canceled`
            .canceled => return .{ .thread = null }, // new status is `.canceled` (unchanged)
        }
    }
    /// Checks whether this syscall has been canceled. This should be called when a syscall is
    /// interrupted through a mechanism which may indicate cancelation, or may be spurious. If
    /// the syscall was canceled, it is finished and `error.Canceled` is returned. Otherwise,
    /// the syscall is not marked finished, and the caller should retry.
    fn checkCancel(s: Syscall) Io.Cancelable!void {
        const thread = s.thread orelse return;
        switch (thread.status.fetchOr(.{
            .cancelation = @enumFromInt(0b010),
            .awaitable = .null,
        }, .monotonic).cancelation) {
            .none => unreachable,
            .parked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_alertable_canceling => unreachable,
            .canceling => unreachable,
            .canceled => unreachable,
            .blocked => {}, // new status is `.blocked` (unchanged)
            .blocked_canceling => return error.Canceled, // new status is `.canceled`
        }
    }
    /// Marks this syscall as finished.
    fn finish(s: Syscall) void {
        const thread = s.thread orelse return;
        switch (thread.status.fetchXor(.{
            .cancelation = @enumFromInt(0b011),
            .awaitable = .null,
        }, .monotonic).cancelation) {
            .none => unreachable,
            .parked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_alertable_canceling => unreachable,
            .canceling => unreachable,
            .canceled => unreachable,
            .blocked => {}, // new status is `.none`
            .blocked_canceling => {}, // new status is `.canceling`
        }
    }
    /// Indicates instead of `NtCancelSynchronousIoFile` we need to use
    /// `NtAlertThread` to interrupt the wait.
    ///
    /// Windows only, called from blocked state only.
    fn toAlertable(s: Syscall) Io.Cancelable!AlertableSyscall {
        comptime assert(is_windows);
        const thread = s.thread orelse return .{ .thread = null };
        var prev = thread.status.load(.monotonic);
        while (true) prev = switch (prev.cancelation) {
            .none => unreachable,
            .parked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_alertable_canceling => unreachable,
            .canceling => unreachable,
            .canceled => unreachable,

            .blocked => thread.status.cmpxchgWeak(prev, .{
                .cancelation = .blocked_alertable,
                .awaitable = prev.awaitable,
            }, .monotonic, .monotonic) orelse return .{ .thread = thread },

            .blocked_canceling => thread.status.cmpxchgWeak(prev, .{
                .cancelation = .canceled,
                .awaitable = prev.awaitable,
            }, .monotonic, .monotonic) orelse return error.Canceled,
        };
    }
    /// Convenience wrapper which calls `finish`, then returns `err`.
    fn fail(s: Syscall, err: anytype) @TypeOf(err) {
        s.finish();
        return err;
    }
    /// Convenience wrapper which calls `finish`, then calls `Threaded.errnoBug`.
    fn errnoBug(s: Syscall, err: posix.E) Io.UnexpectedError {
        @branchHint(.cold);
        s.finish();
        return Threaded.errnoBug(err);
    }
    /// Convenience wrapper which calls `finish`, then calls `posix.unexpectedErrno`.
    fn unexpectedErrno(s: Syscall, err: posix.E) Io.UnexpectedError {
        @branchHint(.cold);
        s.finish();
        return posix.unexpectedErrno(err);
    }
    /// Convenience wrapper which calls `finish`, then calls `windows.statusBug`.
    fn ntstatusBug(s: Syscall, status: windows.NTSTATUS) Io.UnexpectedError {
        @branchHint(.cold);
        s.finish();
        return windows.statusBug(status);
    }
    /// Convenience wrapper which calls `finish`, then calls `windows.unexpectedStatus`.
    fn unexpectedNtstatus(s: Syscall, status: windows.NTSTATUS) Io.UnexpectedError {
        @branchHint(.cold);
        s.finish();
        return windows.unexpectedStatus(status);
    }
};

const AlertableSyscall = struct {
    thread: ?*Thread,

    comptime {
        assert(is_windows);
    }

    fn start() Io.Cancelable!AlertableSyscall {
        const thread = Thread.current orelse return .{ .thread = null };
        switch (thread.cancel_protection) {
            .blocked => return .{ .thread = null },
            .unblocked => {},
        }
        const old_status = thread.status.fetchOr(.{
            .cancelation = @enumFromInt(0b010),
            .awaitable = .null,
        }, .monotonic);
        switch (old_status.cancelation) {
            .parked => unreachable,
            .blocked => unreachable,
            .blocked_alertable => unreachable,
            .blocked_canceling => unreachable,
            .blocked_alertable_canceling => unreachable,
            .none => return .{ .thread = thread }, // new status is `.blocked_alertable`
            .canceling => {
                // Status is unchanged (still `.canceling`)---change to `.canceled` before return.
                thread.status.store(.{ .cancelation = .canceled, .awaitable = old_status.awaitable }, .monotonic);
                return error.Canceled;
            },
            .canceled => return .{ .thread = null }, // new status is `.canceled` (unchanged)
        }
    }

    fn checkCancel(s: AlertableSyscall) Io.Cancelable!void {
        comptime assert(is_windows);
        const thread = s.thread orelse return;
        const old_status = thread.status.fetchOr(.{
            .cancelation = @enumFromInt(0b010),
            .awaitable = .null,
        }, .monotonic);
        switch (old_status.cancelation) {
            .none => unreachable,
            .parked => unreachable,
            .blocked => unreachable,
            .blocked_canceling => unreachable,
            .canceling => unreachable,
            .canceled => unreachable,
            .blocked_alertable => {}, // new status is `.blocked_alertable` (unchanged)
            .blocked_alertable_canceling => {
                // New status is `.canceling`---change to `.canceled` before return.
                thread.status.store(.{ .cancelation = .canceled, .awaitable = old_status.awaitable }, .monotonic);
                return error.Canceled;
            },
        }
    }

    fn finish(s: AlertableSyscall) void {
        comptime assert(is_windows);
        const thread = s.thread orelse return;
        switch (thread.status.fetchXor(.{
            .cancelation = @enumFromInt(0b010),
            .awaitable = .null,
        }, .monotonic).cancelation) {
            .none => unreachable,
            .parked => unreachable,
            .blocked => unreachable,
            .blocked_canceling => unreachable,
            .canceling => unreachable,
            .canceled => unreachable,
            .blocked_alertable => {}, // new status is `.none`
            .blocked_alertable_canceling => {}, // new status is `.canceling`
        }
    }

    fn fail(s: AlertableSyscall, err: anytype) @TypeOf(err) {
        s.finish();
        return err;
    }

    fn ntstatusBug(s: AlertableSyscall, status: windows.NTSTATUS) Io.UnexpectedError {
        @branchHint(.cold);
        s.finish();
        return windows.statusBug(status);
    }

    fn unexpectedNtstatus(s: AlertableSyscall, status: windows.NTSTATUS) Io.UnexpectedError {
        @branchHint(.cold);
        s.finish();
        return windows.unexpectedStatus(status);
    }
};

pub fn waitForApcOrAlert() void {
    const infinite_timeout: windows.LARGE_INTEGER = std.math.minInt(windows.LARGE_INTEGER);
    _ = windows.ntdll.NtDelayExecution(.TRUE, &infinite_timeout);
}

pub const max_iovecs_len = 8;
pub const splat_buffer_size = 64;
/// Happens to be the same number that matches maximum number of handles that
/// NtWaitForMultipleObjects accepts. We use this value also for poll() on
/// posix systems.
const poll_buffer_len = 64;
pub const default_PATH = "/usr/local/bin:/bin/:/usr/bin";
/// There are multiple kernel bugs being worked around with retries.
const max_windows_kernel_bug_retries = 13;

comptime {
    if (@TypeOf(posix.IOV_MAX) != void) assert(max_iovecs_len <= posix.IOV_MAX);
}

pub const InitOptions = struct {
    /// Affects how many bytes are memory-mapped for threads.
    stack_size: usize = std.Thread.SpawnConfig.default_stack_size,
    /// Maximum thread pool size (excluding main thread) when dispatching async
    /// tasks. Until this limit, calls to `Io.async` when all threads are busy will
    /// cause a new thread to be spawned and permanently added to the pool. After
    /// this limit, calls to `Io.async` when all threads are busy run the task
    /// immediately.
    ///
    /// Defaults to one less than the number of logical CPU cores.
    ///
    /// Protected by `Threaded.mutex` once the I/O instance is already in use. See
    /// `setAsyncLimit`.
    async_limit: ?Io.Limit = null,
    /// Maximum thread pool size (excluding main thread) for dispatching concurrent
    /// tasks. Until this limit, calls to `Io.concurrent` will increase the thread
    /// pool size.
    ///
    /// After this number, calls to `Io.concurrent` return `error.ConcurrencyUnavailable`.
    concurrent_limit: Io.Limit = .unlimited,
    /// Affects the following operations:
    /// * `processExecutablePath` on OpenBSD and Haiku.
    argv0: Argv0 = .empty,
    /// Affects the following operations:
    /// * `fileIsTty`
    /// * `processExecutablePath` on OpenBSD and Haiku (observes "PATH").
    /// * `processSpawn`, `processSpawnPath`, `processReplace`, `processReplacePath`
    environ: process.Environ = .empty,
    /// If set to `true`, `File.MemoryMap` APIs will always take the fallback path.
    disable_memory_mapping: bool = false,
};

/// Related:
/// * `init_single_threaded`
pub fn init(
    /// Must be threadsafe. Only used for the following functions:
    /// * `Io.VTable.async`
    /// * `Io.VTable.concurrent`
    /// * `Io.VTable.groupAsync`
    /// * `Io.VTable.groupConcurrent`
    /// If these functions are avoided, then `Allocator.failing` may be passed
    /// here.
    gpa: Allocator,
    options: InitOptions,
) Threaded {
    if (builtin.single_threaded) return .{
        .allocator = gpa,
        .stack_size = options.stack_size,
        .async_limit = options.async_limit orelse init_single_threaded.async_limit,
        .cpu_count_error = init_single_threaded.cpu_count_error,
        .concurrent_limit = options.concurrent_limit,
        .old_sig_io = undefined,
        .old_sig_pipe = undefined,
        .have_signal_handler = init_single_threaded.have_signal_handler,
        .argv0 = options.argv0,
        .environ_initialized = options.environ.block.isEmpty(),
        .environ = .{ .process_environ = options.environ },
        .worker_threads = init_single_threaded.worker_threads,
        .disable_memory_mapping = options.disable_memory_mapping,
    };

    const cpu_count = std.Thread.getCpuCount();

    var t: Threaded = .{
        .allocator = gpa,
        .stack_size = options.stack_size,
        .async_limit = options.async_limit orelse if (cpu_count) |n| .limited(n - 1) else |_| .nothing,
        .concurrent_limit = options.concurrent_limit,
        .cpu_count_error = if (cpu_count) |_| null else |e| e,
        .old_sig_io = undefined,
        .old_sig_pipe = undefined,
        .have_signal_handler = false,
        .argv0 = options.argv0,
        .environ_initialized = options.environ.block.isEmpty(),
        .environ = .{ .process_environ = options.environ },
        .worker_threads = .init(null),
        .disable_memory_mapping = options.disable_memory_mapping,
    };

    if (posix.Sigaction != void) {
        // This causes sending `posix.SIG.IO` to thread to interrupt blocking
        // syscalls, returning `posix.E.INTR`.
        const act: posix.Sigaction = .{
            .handler = .{ .handler = doNothingSignalHandler },
            .mask = posix.sigemptyset(),
            .flags = 0,
        };
        if (have_sig_io) posix.sigaction(.IO, &act, &t.old_sig_io);
        if (have_sig_pipe) posix.sigaction(.PIPE, &act, &t.old_sig_pipe);
        t.have_signal_handler = true;
    }

    return t;
}

/// Statically initialize such that calls to `Io.VTable.concurrent` will fail
/// with `error.ConcurrencyUnavailable`.
///
/// When initialized this way:
/// * cancel requests have no effect.
/// * `deinit` is safe, but unnecessary to call.
pub const init_single_threaded: Threaded = init: {
    const env_block: process.Environ.Block = if (is_windows) .global else .empty;
    break :init .{
        .allocator = .failing,
        .stack_size = std.Thread.SpawnConfig.default_stack_size,
        .async_limit = .nothing,
        .cpu_count_error = null,
        .concurrent_limit = .nothing,
        .old_sig_io = undefined,
        .old_sig_pipe = undefined,
        .have_signal_handler = false,
        .argv0 = .empty,
        .environ_initialized = env_block.isEmpty(),
        .environ = .{ .process_environ = .{ .block = env_block } },
        .worker_threads = .init(null),
        .disable_memory_mapping = false,
    };
};

var global_single_threaded_instance: Threaded = .init_single_threaded;

/// In general, the application is responsible for choosing the `Io`
/// implementation and library code should accept an `Io` parameter rather than
/// accessing this declaration. Most code should avoid referencing this
/// declaration entirely.
///
/// However, in some cases such as debugging, it is desirable to hardcode a
/// reference to this `Io` implementation.
///
/// This instance does not support concurrency or cancelation.
pub const global_single_threaded: *Threaded = &global_single_threaded_instance;

pub fn setAsyncLimit(t: *Threaded, new_limit: Io.Limit) void {
    mutexLock(&t.mutex);
    defer mutexUnlock(&t.mutex);
    t.async_limit = new_limit;
}

pub fn deinit(t: *Threaded) void {
    t.join();
    if (posix.Sigaction != void and t.have_signal_handler) {
        if (have_sig_io) posix.sigaction(.IO, &t.old_sig_io, null);
        if (have_sig_pipe) posix.sigaction(.PIPE, &t.old_sig_pipe, null);
    }
    t.dl.deinit();
    t.null_file.deinit();
    t.random_file.deinit();
    t.pipe_file.deinit();
    t.* = undefined;
}

fn join(t: *Threaded) void {
    if (builtin.single_threaded) return;
    {
        mutexLock(&t.mutex);
        defer mutexUnlock(&t.mutex);
        t.join_requested = true;
    }
    condBroadcast(&t.cond);
    t.wait_group.wait();
}

fn worker(t: *Threaded) void {
    var thread: Thread = .{
        .next = undefined,
        .id = std.Thread.getCurrentId(),
        .handle = handle: {
            if (std.Thread.use_pthreads) break :handle std.c.pthread_self();
            if (is_windows) break :handle undefined; // populated below
        },
        .status = .init(.{
            .cancelation = .none,
            .awaitable = .null,
        }),
        .cancel_protection = .unblocked,
        .futex_waiter = undefined,
        .unpark_flag = unpark_flag_init,
        .csprng = .uninitialized,
    };
    Thread.current = &thread;

    if (is_windows) {
        assert(windows.ntdll.NtOpenThread(
            &thread.handle,
            .{
                .SPECIFIC = .{
                    .THREAD = .{
                        .TERMINATE = true, // for `NtCancelSynchronousIoFile`
                        .ALERT = true, // for `NtAlertThread`
                    },
                },
            },
            &.{ .ObjectName = null },
            &windows.teb().ClientId,
        ) == .SUCCESS);
    }
    defer if (is_windows) {
        windows.CloseHandle(thread.handle);
    };

    {
        var head = t.worker_threads.load(.monotonic);
        while (true) {
            thread.next = head;
            head = t.worker_threads.cmpxchgWeak(
                head,
                &thread,
                .release,
                .monotonic,
            ) orelse break;
        }
    }

    defer t.wait_group.finish();

    mutexLock(&t.mutex);
    defer mutexUnlock(&t.mutex);

    while (true) {
        while (t.run_queue.popFirst()) |runnable_node| {
            mutexUnlock(&t.mutex);
            thread.cancel_protection = .unblocked;
            const runnable: *Runnable = @fieldParentPtr("node", runnable_node);
            runnable.startFn(runnable, &thread, t);
            mutexLock(&t.mutex);
            t.busy_count -= 1;
        }
        if (t.join_requested) break;
        condWait(&t.cond, &t.mutex);
    }
}

pub fn io(t: *Threaded) Io {
    return .{
        .userdata = t,
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
            .dirStat = dirStat,
            .dirStatFile = dirStatFile,
            .dirAccess = dirAccess,
            .dirCreateFile = dirCreateFile,
            .dirCreateFileAtomic = dirCreateFileAtomic,
            .dirOpenFile = dirOpenFile,
            .dirOpenDir = dirOpenDir,
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
            .fileSupportsAnsiEscapeCodes = fileSupportsAnsiEscapeCodes,
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

            .netListenIp = switch (native_os) {
                .windows => netListenIpWindows,
                else => netListenIpPosix,
            },
            .netListenUnix = switch (native_os) {
                .windows => netListenUnixWindows,
                else => netListenUnixPosix,
            },
            .netAccept = switch (native_os) {
                .windows => netAcceptWindows,
                else => netAcceptPosix,
            },
            .netBindIp = switch (native_os) {
                .windows => netBindIpWindows,
                else => netBindIpPosix,
            },
            .netConnectIp = switch (native_os) {
                .windows => netConnectIpWindows,
                else => netConnectIpPosix,
            },
            .netConnectUnix = switch (native_os) {
                .windows => netConnectUnixWindows,
                else => netConnectUnixPosix,
            },
            .netSocketCreatePair = netSocketCreatePair,
            .netClose = netClose,
            .netShutdown = switch (native_os) {
                .windows => netShutdownWindows,
                else => netShutdownPosix,
            },
            .netRead = switch (native_os) {
                .windows => netReadWindows,
                else => netReadPosix,
            },
            .netWrite = switch (native_os) {
                .windows => netWriteWindows,
                else => netWritePosix,
            },
            .netWriteFile = netWriteFile,
            .netSend = switch (native_os) {
                .windows => netSendWindows,
                else => netSendPosix,
            },
            .netInterfaceNameResolve = netInterfaceNameResolve,
            .netInterfaceName = netInterfaceName,
            .netLookup = netLookup,
        },
    };
}

pub const socket_flags_unsupported = is_darwin or native_os == .haiku;
const have_accept4 = !socket_flags_unsupported;
const have_flock_open_flags = @hasField(posix.O, "EXLOCK");
const have_networking = std.options.networking and native_os != .wasi;
const have_flock = @TypeOf(posix.system.flock) != void;
const have_sendmmsg = native_os == .linux;
const have_futex = switch (builtin.cpu.arch) {
    .wasm32, .wasm64 => builtin.cpu.has(.wasm, .atomics),
    else => true,
};
const have_preadv = switch (native_os) {
    .windows, .haiku => false,
    else => true,
};
const have_sig_io = posix.SIG != void and @hasField(posix.SIG, "IO");
const have_sig_pipe = posix.SIG != void and @hasField(posix.SIG, "PIPE");
const have_sendfile = if (builtin.link_libc) @TypeOf(std.c.sendfile) != void else native_os == .linux;
const have_copy_file_range = switch (native_os) {
    .linux, .freebsd => true,
    else => false,
};
const have_fcopyfile = is_darwin;
const have_fchmodat2 = native_os == .linux and
    (builtin.os.isAtLeast(.linux, .{ .major = 6, .minor = 6, .patch = 0 }) orelse true) and
    (builtin.abi.isAndroid() or !std.c.versionCheck(.{ .major = 2, .minor = 32, .patch = 0 }));
const have_fchmodat_flags = native_os != .linux or
    (!builtin.abi.isAndroid() and std.c.versionCheck(.{ .major = 2, .minor = 32, .patch = 0 }));

const have_fchown = switch (native_os) {
    .wasi, .windows => false,
    else => true,
};

const have_fchmod = switch (native_os) {
    .windows => false,
    .wasi => builtin.link_libc,
    else => true,
};

const have_waitid = switch (native_os) {
    .linux => @hasField(std.os.linux.SYS, "waitid"),
    else => false,
};

const have_wait4 = switch (native_os) {
    .linux => @hasField(std.os.linux.SYS, "wait4"),
    .dragonfly, .freebsd, .netbsd, .openbsd, .illumos, .serenity, .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => true,
    else => false,
};

const have_mmap = switch (native_os) {
    .wasi, .windows => false,
    else => true,
};
const have_poll = switch (native_os) {
    .wasi, .windows => false,
    else => true,
};

const open_sym = if (posix.lfs64_abi) posix.system.open64 else posix.system.open;
const openat_sym = if (posix.lfs64_abi) posix.system.openat64 else posix.system.openat;
const fstat_sym = if (posix.lfs64_abi) posix.system.fstat64 else posix.system.fstat;
const fstatat_sym = if (posix.lfs64_abi) posix.system.fstatat64 else posix.system.fstatat;
const lseek_sym = if (posix.lfs64_abi) posix.system.lseek64 else posix.system.lseek;
const preadv_sym = if (posix.lfs64_abi) posix.system.preadv64 else posix.system.preadv;
const pread_sym = if (posix.lfs64_abi) posix.system.pread64 else posix.system.pread;
const ftruncate_sym = if (posix.lfs64_abi) posix.system.ftruncate64 else posix.system.ftruncate;
const pwritev_sym = if (posix.lfs64_abi) posix.system.pwritev64 else posix.system.pwritev;
const pwrite_sym = if (posix.lfs64_abi) posix.system.pwrite64 else posix.system.pwrite;
const sendfile_sym = if (posix.lfs64_abi) posix.system.sendfile64 else posix.system.sendfile;
const mmap_sym = if (posix.lfs64_abi) posix.system.mmap64 else posix.system.mmap;

const linux_copy_file_range_use_c = std.c.versionCheck(if (builtin.abi.isAndroid()) .{
    .major = 34,
    .minor = 0,
    .patch = 0,
} else .{
    .major = 2,
    .minor = 27,
    .patch = 0,
});
const linux_copy_file_range_sys = if (linux_copy_file_range_use_c) std.c else std.os.linux;

const statx_use_c = std.c.versionCheck(if (builtin.abi.isAndroid())
    .{ .major = 30, .minor = 0, .patch = 0 }
else
    .{ .major = 2, .minor = 28, .patch = 0 });

const use_libc_getrandom = std.c.versionCheck(if (builtin.abi.isAndroid()) .{
    .major = 28,
    .minor = 0,
    .patch = 0,
} else .{
    .major = 2,
    .minor = 25,
    .patch = 0,
});

const use_dev_urandom = @TypeOf(posix.system.getrandom) == void and native_os == .linux;

fn crashHandler(userdata: ?*anyopaque) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const thread = Thread.current orelse return;
    thread.status.store(.{ .cancelation = .canceled, .awaitable = .null }, .monotonic);
    thread.cancel_protection = .blocked;
}

fn async(
    userdata: ?*anyopaque,
    result: []u8,
    result_alignment: Alignment,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque, result: *anyopaque) void,
) ?*Io.AnyFuture {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (builtin.single_threaded) {
        start(context.ptr, result.ptr);
        return null;
    }

    const gpa = t.allocator;
    const future = Future.create(gpa, result.len, result_alignment, context, context_alignment, start) catch |err| switch (err) {
        error.OutOfMemory => {
            start(context.ptr, result.ptr);
            return null;
        },
    };

    mutexLock(&t.mutex);

    const busy_count = t.busy_count;

    if (busy_count >= @intFromEnum(t.async_limit)) {
        mutexUnlock(&t.mutex);
        future.destroy(gpa);
        start(context.ptr, result.ptr);
        return null;
    }

    t.busy_count = busy_count + 1;

    const pool_size = t.wait_group.value();
    if (pool_size - busy_count == 0) {
        t.wait_group.start();
        const thread = std.Thread.spawn(.{ .stack_size = t.stack_size }, worker, .{t}) catch {
            t.wait_group.finish();
            t.busy_count = busy_count;
            mutexUnlock(&t.mutex);
            future.destroy(gpa);
            start(context.ptr, result.ptr);
            return null;
        };
        thread.detach();
    }

    t.run_queue.prepend(&future.runnable.node);

    mutexUnlock(&t.mutex);
    condSignal(&t.cond);
    return @ptrCast(future);
}

fn concurrent(
    userdata: ?*anyopaque,
    result_len: usize,
    result_alignment: Alignment,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque, result: *anyopaque) void,
) Io.ConcurrentError!*Io.AnyFuture {
    if (builtin.single_threaded) return error.ConcurrencyUnavailable;

    const t: *Threaded = @ptrCast(@alignCast(userdata));

    const gpa = t.allocator;
    const future = Future.create(gpa, result_len, result_alignment, context, context_alignment, start) catch |err| switch (err) {
        error.OutOfMemory => return error.ConcurrencyUnavailable,
    };
    errdefer future.destroy(gpa);

    mutexLock(&t.mutex);
    defer mutexUnlock(&t.mutex);

    const busy_count = t.busy_count;

    if (busy_count >= @intFromEnum(t.concurrent_limit))
        return error.ConcurrencyUnavailable;

    t.busy_count = busy_count + 1;
    errdefer t.busy_count = busy_count;

    const pool_size = t.wait_group.value();
    if (pool_size - busy_count == 0) {
        t.wait_group.start();
        errdefer t.wait_group.finish();

        const thread = std.Thread.spawn(.{ .stack_size = t.stack_size }, worker, .{t}) catch
            return error.ConcurrencyUnavailable;

        thread.detach();
    }

    t.run_queue.prepend(&future.runnable.node);

    condSignal(&t.cond);
    return @ptrCast(future);
}

fn groupAsync(
    userdata: ?*anyopaque,
    type_erased: *Io.Group,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque) void,
) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const g: Group = .{ .ptr = type_erased };

    if (builtin.single_threaded) return groupAsyncEager(start, context.ptr);

    const gpa = t.allocator;
    const task = Group.Task.create(gpa, g, context, context_alignment, start) catch |err| switch (err) {
        error.OutOfMemory => return groupAsyncEager(start, context.ptr),
    };

    mutexLock(&t.mutex);

    const busy_count = t.busy_count;

    if (busy_count >= @intFromEnum(t.async_limit)) {
        mutexUnlock(&t.mutex);
        task.destroy(gpa);
        return groupAsyncEager(start, context.ptr);
    }

    t.busy_count = busy_count + 1;

    const pool_size = t.wait_group.value();
    if (pool_size - busy_count == 0) {
        t.wait_group.start();
        const thread = std.Thread.spawn(.{ .stack_size = t.stack_size }, worker, .{t}) catch {
            t.wait_group.finish();
            t.busy_count = busy_count;
            mutexUnlock(&t.mutex);
            task.destroy(gpa);
            return groupAsyncEager(start, context.ptr);
        };
        thread.detach();
    }

    // TODO: if this logic is changed to be lock-free, this `fetchAdd` must be released by the queue
    // prepend so that the task doesn't finish without observing this and try to decrement the count
    // below zero.
    _ = g.status().fetchAdd(.{
        .num_running = 1,
        .have_awaiter = false,
        .canceled = false,
    }, .monotonic);
    t.run_queue.prepend(&task.runnable.node);

    mutexUnlock(&t.mutex);
    condSignal(&t.cond);
}
fn groupAsyncEager(
    start: *const fn (context: *const anyopaque) void,
    context: *const anyopaque,
) void {
    start(context);
}

fn groupConcurrent(
    userdata: ?*anyopaque,
    type_erased: *Io.Group,
    context: []const u8,
    context_alignment: Alignment,
    start: *const fn (context: *const anyopaque) void,
) Io.ConcurrentError!void {
    if (builtin.single_threaded) return error.ConcurrencyUnavailable;

    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const g: Group = .{ .ptr = type_erased };

    const gpa = t.allocator;
    const task = Group.Task.create(gpa, g, context, context_alignment, start) catch |err| switch (err) {
        error.OutOfMemory => return error.ConcurrencyUnavailable,
    };
    errdefer task.destroy(gpa);

    mutexLock(&t.mutex);
    defer mutexUnlock(&t.mutex);

    const busy_count = t.busy_count;

    if (busy_count >= @intFromEnum(t.concurrent_limit))
        return error.ConcurrencyUnavailable;

    t.busy_count = busy_count + 1;
    errdefer t.busy_count = busy_count;

    const pool_size = t.wait_group.value();
    if (pool_size - busy_count == 0) {
        t.wait_group.start();
        errdefer t.wait_group.finish();

        const thread = std.Thread.spawn(.{ .stack_size = t.stack_size }, worker, .{t}) catch
            return error.ConcurrencyUnavailable;

        thread.detach();
    }

    // TODO: if this logic is changed to be lock-free, this `fetchAdd` must be released by the queue
    // prepend so that the task doesn't finish without observing this and try to decrement the count
    // below zero.
    _ = g.status().fetchAdd(.{
        .num_running = 1,
        .have_awaiter = false,
        .canceled = false,
    }, .monotonic);
    t.run_queue.prepend(&task.runnable.node);

    condSignal(&t.cond);
}

fn groupAwait(userdata: ?*anyopaque, type_erased: *Io.Group, initial_token: *anyopaque) Io.Cancelable!void {
    _ = initial_token; // we need to load `token` *after* the group finishes
    if (builtin.single_threaded) unreachable; // nothing to await
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const g: Group = .{ .ptr = type_erased };

    var num_completed: std.atomic.Value(u32) = .init(0);
    g.awaiter().* = &num_completed;

    const pre_await_status = g.status().fetchOr(.{
        .num_running = 0,
        .have_awaiter = true,
        .canceled = false,
    }, .acq_rel); // acquire results if complete; release `g.awaiter()`

    assert(!pre_await_status.have_awaiter);
    assert(!pre_await_status.canceled);
    if (pre_await_status.num_running == 0) {
        // Already done. Since the group is finished, it's illegal to spawn more tasks in it
        // until we return, so we can access `g.status()` non-atomically.
        g.status().raw.have_awaiter = false;
        return;
    }

    while (Thread.futexWait(&num_completed.raw, 0, null)) {
        switch (num_completed.load(.acquire)) { // acquire task results
            0 => continue,
            1 => break,
            else => unreachable, // group was reused before `await` returned
        }
    } else |err| switch (err) {
        error.Canceled => {
            const pre_cancel_status = g.status().fetchOr(.{
                .num_running = 0,
                .have_awaiter = false,
                .canceled = true,
            }, .acq_rel); // acquire results if complete; release `g.awaiter()`
            assert(pre_cancel_status.have_awaiter);
            assert(!pre_cancel_status.canceled);

            // Even if `pre_cancel_status.num_running == 0`, we still need to wait for the signal,
            // because in that case the last member of the group is already trying to modify it.
            // However, if we know everything is done, we *can* skip signaling blocked threads.
            const skip_signals = pre_cancel_status.num_running == 0;
            g.waitForCancelWithSignaling(t, &num_completed, skip_signals);

            // The group is finished, so it's illegal to spawn more tasks in it until we return, so
            // we can access `g.status()` non-atomically.
            g.status().raw.canceled = false;
            g.status().raw.have_awaiter = false;
            return error.Canceled;
        },
    }

    // The group is finished, so it's illegal to spawn more tasks in it until we return, so
    // we can access `g.status()` non-atomically.
    g.status().raw.have_awaiter = false;
}

fn groupCancel(userdata: ?*anyopaque, type_erased: *Io.Group, initial_token: *anyopaque) void {
    _ = initial_token;
    if (builtin.single_threaded) unreachable; // nothing to cancel
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const g: Group = .{ .ptr = type_erased };

    var num_completed: std.atomic.Value(u32) = .init(0);
    g.awaiter().* = &num_completed;

    const pre_cancel_status = g.status().fetchOr(.{
        .num_running = 0,
        .have_awaiter = true,
        .canceled = true,
    }, .acq_rel); // acquire results if complete; release `g.awaiter()`

    assert(!pre_cancel_status.have_awaiter);
    assert(!pre_cancel_status.canceled);
    if (pre_cancel_status.num_running == 0) {
        // Already done. Since the group is finished, it's illegal to spawn more tasks in it
        // until we return, so we can access `g.status()` non-atomically.
        g.status().raw.have_awaiter = false;
        g.status().raw.canceled = false;
        return;
    }

    g.waitForCancelWithSignaling(t, &num_completed, false);

    g.status().raw = .{ .num_running = 0, .have_awaiter = false, .canceled = false };
}

fn recancel(userdata: ?*anyopaque) void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    recancelInner();
}
fn recancelInner() void {
    const thread = Thread.current.?; // called `recancel` but was not canceled
    switch (thread.status.fetchXor(.{
        .cancelation = @enumFromInt(0b001),
        .awaitable = .null,
    }, .monotonic).cancelation) {
        .canceled => {},
        .none => unreachable, // called `recancel` but was not canceled
        .canceling => unreachable, // called `recancel` but cancelation was already pending
        .parked => unreachable,
        .blocked => unreachable,
        .blocked_alertable => unreachable,
        .blocked_alertable_canceling => unreachable,
        .blocked_canceling => unreachable,
    }
}

fn swapCancelProtection(userdata: ?*anyopaque, new: Io.CancelProtection) Io.CancelProtection {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    const thread = Thread.current orelse return .unblocked;
    const old = thread.cancel_protection;
    thread.cancel_protection = new;
    return old;
}

fn checkCancel(userdata: ?*anyopaque) Io.Cancelable!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    return Thread.checkCancel();
}

fn await(
    userdata: ?*anyopaque,
    any_future: *Io.AnyFuture,
    result: []u8,
    result_alignment: Alignment,
) void {
    _ = result_alignment;
    if (builtin.single_threaded) unreachable; // nothing to await
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const future: *Future = @ptrCast(@alignCast(any_future));

    var num_completed: std.atomic.Value(u32) = .init(0);
    future.awaiter = &num_completed;

    const pre_await_status = future.status.fetchOr(.{
        .tag = .pending_awaited,
        .thread = .null,
    }, .acq_rel); // acquire results if complete; release `future.awaiter`
    switch (pre_await_status.tag) {
        .pending => while (Thread.futexWait(&num_completed.raw, 0, null)) {
            switch (num_completed.load(.acquire)) { // acquire task results
                0 => continue,
                1 => break,
                else => unreachable, // group was reused before `await` returned
            }
        } else |err| switch (err) {
            error.Canceled => {
                const pre_cancel_status = future.status.fetchOr(.{
                    .tag = .pending_canceled,
                    .thread = .null,
                }, .acq_rel); // acquire results if complete; release `future.awaiter`
                const done_status = switch (pre_cancel_status.tag) {
                    .pending => unreachable, // invalid state: we already awaited
                    .pending_awaited => done_status: {
                        const working_thread = pre_cancel_status.thread.unpack();
                        future.waitForCancelWithSignaling(t, &num_completed, @alignCast(working_thread));
                        break :done_status future.status.load(.monotonic);
                    },
                    .pending_canceled => unreachable, // `await` raced with `cancel`
                    .done => done_status: {
                        // The task just finished, but we still need to wait for the signal, because the
                        // task thread already figured out that they need to update `future.awaiter`.
                        future.waitForCancelWithSignaling(t, &num_completed, null);
                        // Also, we have clobbered `future.status.tag` to `.pending_canceled`, but that's
                        // not actually a problem for the logic below.
                        break :done_status pre_cancel_status;
                    },
                };
                // If the future did not acknowledge the cancelation, we need to mark it outstanding
                // for us. Because `done_status.tag == .done`, the information about whether there
                // was an acknowledged cancelation is encoded in `done_status.thread`.
                assert(done_status.tag == .done);
                switch (done_status.thread) {
                    .null => recancelInner(), // cancelation was not acknowledged, so it's ours
                    .all_ones => {}, // cancelation was acknowledged, so it was this task's job to propagate it
                    _ => unreachable,
                }
            },
        },
        .pending_awaited => unreachable, // `await` raced with `await`
        .pending_canceled => unreachable, // `await` raced with `cancel`
        .done => {},
    }
    @memcpy(result, future.resultPointer());
    future.destroy(t.allocator);
}

fn cancel(
    userdata: ?*anyopaque,
    any_future: *Io.AnyFuture,
    result: []u8,
    result_alignment: Alignment,
) void {
    _ = result_alignment;
    if (builtin.single_threaded) unreachable; // nothing to cancel
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const future: *Future = @ptrCast(@alignCast(any_future));

    var num_completed: std.atomic.Value(u32) = .init(0);
    future.awaiter = &num_completed;

    const pre_cancel_status = future.status.fetchOr(.{
        .tag = .pending_canceled,
        .thread = .null,
    }, .acq_rel); // acquire results if complete; release `future.awaiter`
    switch (pre_cancel_status.tag) {
        .pending => {
            const working_thread = pre_cancel_status.thread.unpack();
            future.waitForCancelWithSignaling(t, &num_completed, @alignCast(working_thread));
        },
        .pending_awaited => unreachable, // `await` raced with `await`
        .pending_canceled => unreachable, // `await` raced with `cancel`
        .done => {},
    }
    @memcpy(result, future.resultPointer());
    future.destroy(t.allocator);
}

fn futexWait(userdata: ?*anyopaque, ptr: *const u32, expected: u32, timeout: Io.Timeout) Io.Cancelable!void {
    if (builtin.single_threaded) {
        assert(timeout != .none); // Deadlock.
        return;
    }
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    const t_io = io(t);
    const timeout_ns: ?u64 = ns: {
        const d = timeout.toDurationFromNow(t_io) orelse break :ns null;
        break :ns std.math.lossyCast(u64, d.raw.toNanoseconds());
    };
    return Thread.futexWait(ptr, expected, timeout_ns);
}

fn futexWaitUncancelable(userdata: ?*anyopaque, ptr: *const u32, expected: u32) void {
    if (builtin.single_threaded) unreachable; // Deadlock.
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    Thread.futexWaitUncancelable(ptr, expected, null);
}

fn futexWake(userdata: ?*anyopaque, ptr: *const u32, max_waiters: u32) void {
    if (builtin.single_threaded) return; // Nothing to wake up.
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    _ = t;
    Thread.futexWake(ptr, max_waiters);
}

fn operate(userdata: ?*anyopaque, operation: Io.Operation) Io.Cancelable!Io.Operation.Result {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    switch (operation) {
        .file_read_streaming => |o| return .{
            .file_read_streaming = fileReadStreaming(t, o.file, o.data) catch |err| switch (err) {
                error.Canceled => |e| return e,
                else => |e| e,
            },
        },
        .file_write_streaming => |o| return .{
            .file_write_streaming = fileWriteStreaming(t, o.file, o.header, o.data, o.splat) catch |err| switch (err) {
                error.Canceled => |e| return e,
                else => |e| e,
            },
        },
        .device_io_control => |*o| return .{ .device_io_control = try deviceIoControl(o) },
        .net_receive => |*o| return .{ .net_receive = o: {
            if (!have_networking) break :o .{ error.NetworkDown, 0 };
            if (is_windows) break :o netReceiveWindows(t, o.socket_handle, o.message_buffer, o.data_buffer, o.flags);
            netReceivePosix(o.socket_handle, &o.message_buffer[0], o.data_buffer, o.flags, false) catch |err| switch (err) {
                error.Canceled => |e| return e,
                error.WouldBlock => unreachable,
                else => |e| break :o .{ e, 0 },
            };
            break :o .{ null, 1 };
        } },
    }
}

fn batchAwaitAsync(userdata: ?*anyopaque, b: *Io.Batch) Io.Cancelable!void {
    const t: *Threaded = @ptrCast(@alignCast(userdata));
    if (is_windows) {
        batchDrainSubmittedWindows(t, b, false) catch |err| switch (err) {
            error.ConcurrencyUnavailable => unreachable, // passed concurrency=false
            else => |e| return e,
        };
        const alertable_syscall = try AlertableSyscall.start();
        while (b.pending.head != .none and b.completed.head == .none) waitForApcOrAlert();
        alertable_syscall.finish();
        return;
    }
    if (have_poll) {
        var poll_buffer: [poll_buffer_len]posix.pollfd = undefined;
        var poll_len: u32 = 0;
        {
            var index = b.submitted.head;
            while (index != .none and poll_len < poll_buffer_len) {
                const submission = &b.storage[index.toIndex()].submission;
                switch (submission.operation) {
                    .file_read_streaming => |o| {
                        poll_buffer[poll_len] = .{
                            .fd = o.file.handle,
                            .events = posix.POLL.IN | posix.POLL.ERR,
                            .revents = 0,
                        };
                        poll_len += 1;
                    },
                    .file_write_streaming => |o| {
                        poll_buffer[poll_len] = .{
                            .fd = o.file.handle,
                            .events = posix.POLL.OUT | posix.POLL.ERR,
                            .revents = 0,
                        };
                        poll_len += 1;
                    },
                    .device_io_control => |o| {
                        poll_buffer[poll_len] = .{
                            .fd = o.file.handle,
                            .events = posix.POLL.OUT | posix.POLL.IN | posix.POLL.ERR,
                            .revents = 0,
                        };
                        poll_len += 1;
                    },
                    .net_receive => |*o| {
                        poll_buffer[poll_len] = .{
                            .fd = o.socket_handle,
                            .events = posix.POLL.IN | posix.POLL.ERR,
                            .revents = 0,
                        };
                        poll_len += 1;
                    },
                }
                index = submission.node.next;
            }
        }
        switch (poll_len) {
            0 => return,
            1 => {},
            else => while (true) {
                const timeout_ms: i32 = t: {
                    if (b.completed.head != .none) {
                        // It is legal to call batchWait with already completed
                        // operations in the ring. In such case, we need to avoid
                        // blocking in the poll syscall, but we can sti
```
