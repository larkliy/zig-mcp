```
six.MLOCK = .{ .ONFAULT = options.on_fault };
        switch (posix.errno(posix.system.mlock2(memory.ptr, memory.len, flags))) {
            .SUCCESS => return,
            .INVAL => |err| return std.Io.Threaded.errnoBug(err), // unaligned, negative, runs off end of addrspace
            .PERM => return error.PermissionDenied,
            .NOMEM => return error.LockedMemoryLimitExceeded,
            .AGAIN => return error.SystemResources,
            else => |err| return posix.unexpectedErrno(err),
        }
    }
    return error.UnsupportedOperation;
}

pub const UnlockMemoryError = error{
    PermissionDenied,
    OutOfMemory,
    SystemResources,
} || Io.UnexpectedError;

/// Withdraw request for process's virtual address space to be in RAM.
///
/// Corresponds to "munlock" in libc.
///
/// See also:
/// * `lockMemory`
pub fn unlockMemory(memory: []align(std.heap.page_size_min) const u8) UnlockMemoryError!void {
    if (@TypeOf(posix.system.munlock) == void) return;
    switch (posix.errno(posix.system.munlock(memory.ptr, memory.len))) {
        .SUCCESS => return,
        .INVAL => |err| return std.Io.Threaded.errnoBug(err), // unaligned or runs off end of addr space
        .PERM => return error.PermissionDenied,
        .NOMEM => return error.OutOfMemory,
        .AGAIN => return error.SystemResources,
        else => |err| return posix.unexpectedErrno(err),
    }
}

pub const LockMemoryAllOptions = struct {
    current: bool = false,
    future: bool = false,
    /// Asserted to be used together with `current` or `future`, or both.
    on_fault: bool = false,
};

pub fn lockMemoryAll(options: LockMemoryAllOptions) LockMemoryError!void {
    if (@TypeOf(posix.system.mlockall) == void) return error.UnsupportedOperation;
    var flags: posix.MCL = .{
        .CURRENT = options.current,
        .FUTURE = options.future,
    };
    if (options.on_fault) {
        assert(options.current or options.future);
        if (@hasField(posix.MCL, "ONFAULT")) {
            flags.ONFAULT = true;
        } else {
            return error.UnsupportedOperation;
        }
    }
    switch (posix.errno(posix.system.mlockall(flags))) {
        .SUCCESS => return,
        .INVAL => |err| return std.Io.Threaded.errnoBug(err),
        .PERM => return error.PermissionDenied,
        .NOMEM => return error.LockedMemoryLimitExceeded,
        .AGAIN => return error.SystemResources,
        else => |err| return posix.unexpectedErrno(err),
    }
}

pub fn unlockMemoryAll() UnlockMemoryError!void {
    if (@TypeOf(posix.system.munlockall) == void) return;
    switch (posix.errno(posix.system.munlockall())) {
        .SUCCESS => return,
        .PERM => return error.PermissionDenied,
        .NOMEM => return error.OutOfMemory,
        .AGAIN => return error.SystemResources,
        else => |err| return posix.unexpectedErrno(err),
    }
}

pub const ProtectMemoryError = error{
    UnsupportedOperation,
    /// OpenBSD will refuse to change memory protection if the specified region
    /// contains any pages that have previously been marked immutable using the
    /// `mimmutable` function.
    PermissionDenied,
    /// The memory cannot be given the specified access. This can happen, for
    /// example, if you memory map a file to which you have read-only access,
    /// then use `protectMemory` to mark it writable.
    AccessDenied,
    /// Changing the protection of a memory region would result in the total
    /// number of mappings with distinct attributes exceeding the allowed
    /// maximum.
    OutOfMemory,
} || Io.UnexpectedError;

pub const MemoryProtection = packed struct(u3) {
    read: bool = false,
    write: bool = false,
    execute: bool = false,
};

pub fn protectMemory(memory: []align(std.heap.page_size_min) u8, protection: MemoryProtection) ProtectMemoryError!void {
    if (native_os == .windows) {
        var addr = memory.ptr; // ntdll takes an extra level of indirection here
        var size = memory.len; // ntdll takes an extra level of indirection here
        var old: windows.PAGE = undefined;
        const current_process: windows.HANDLE = @ptrFromInt(@as(usize, @bitCast(@as(isize, -1))));
        const new = windows.PAGE.fromProtection(protection) orelse return error.AccessDenied;
        switch (windows.ntdll.NtProtectVirtualMemory(current_process, @ptrCast(&addr), &size, new, &old)) {
            .SUCCESS => return,
            .INVALID_ADDRESS => return error.AccessDenied,
            else => |st| return windows.unexpectedStatus(st),
        }
    } else if (posix.PROT != void) {
        const flags: posix.PROT = .{
            .READ = protection.read,
            .WRITE = protection.write,
            .EXEC = protection.execute,
        };
        switch (posix.errno(posix.system.mprotect(memory.ptr, memory.len, flags))) {
            .SUCCESS => return,
            .PERM => return error.PermissionDenied,
            .INVAL => |err| return std.Io.Threaded.errnoBug(err),
            .ACCES => return error.AccessDenied,
            .NOMEM => return error.OutOfMemory,
            else => |err| return posix.unexpectedErrno(err),
        }
    }
    return error.UnsupportedOperation;
}

var test_page: [std.heap.page_size_max]u8 align(std.heap.page_size_max) = undefined;

test lockMemory {
    lockMemory(&test_page, .{}) catch return error.SkipZigTest;
    unlockMemory(&test_page) catch return error.SkipZigTest;
}

test lockMemoryAll {
    lockMemoryAll(.{ .current = true }) catch return error.SkipZigTest;
    unlockMemoryAll() catch return error.SkipZigTest;
}

test protectMemory {
    protectMemory(&test_page, .{}) catch return error.SkipZigTest;
    protectMemory(&test_page, .{ .read = true, .write = true }) catch return error.SkipZigTest;
}



---
File: /std/Progress.zig
---

//! This API is non-allocating, non-fallible, thread-safe, and lock-free.
const Progress = @This();

const builtin = @import("builtin");
const is_big_endian = builtin.cpu.arch.endian() == .big;
const is_windows = builtin.os.tag == .windows;

const std = @import("std");
const Io = std.Io;
const windows = std.os.windows;
const testing = std.testing;
const assert = std.debug.assert;
const posix = std.posix;
const Writer = Io.Writer;

/// Currently this API only supports this value being set to stderr, which
/// happens automatically inside `start`.
terminal: Io.File,

io: Io,

terminal_mode: TerminalMode,

update_worker: ?Io.Future(WorkerError!void),

/// Atomically set by SIGWINCH as well as the root done() function.
redraw_event: Io.Event,
need_clear: bool,
status: Status,

refresh_rate_ns: u64,
initial_delay_ns: u64,

rows: u16,
cols: u16,

/// Accessed only by the update thread.
draw_buffer: []u8,

/// This is in a separate array from `node_storage` but with the same length so
/// that it can be iterated over efficiently without trashing too much of the
/// CPU cache.
node_parents: [node_storage_buffer_len]Node.Parent,
node_storage: [node_storage_buffer_len]Node.Storage,
node_freelist_next: [node_storage_buffer_len]Node.OptionalIndex,
node_freelist: Freelist,
/// This is the number of elements in node arrays which have been used so far. Nodes before this
/// index are either active, or on the freelist. The remaining nodes are implicitly free. This
/// value may at times temporarily exceed the node count.
node_end_index: u32,

ipc_next: Ipc.SlotAtomic,
ipc: [ipc_storage_buffer_len]Ipc,
ipc_files: [ipc_storage_buffer_len]Io.File,

start_failure: StartFailure,

pub const Status = enum {
    /// Indicates the application is progressing towards completion of a task.
    /// Unless the application is interactive, this is the only status the
    /// program will ever have!
    working,
    /// The application has completed an operation, and is now waiting for user
    /// input rather than calling exit(0).
    success,
    /// The application encountered an error, and is now waiting for user input
    /// rather than calling exit(1).
    failure,
    /// The application encountered at least one error, but is still working on
    /// more tasks.
    failure_working,
};

const Freelist = packed struct(u32) {
    head: Node.OptionalIndex,
    /// Whenever `node_freelist` is added to, this generation is incremented
    /// to avoid ABA bugs when acquiring nodes. Wrapping arithmetic is used.
    generation: u24,
};

pub const Ipc = packed struct(u32) {
    /// mutex protecting `file` use, only locked by `serializeIpc`
    locked: bool,
    /// when unlocked: whether `file` is defined
    /// when locked: whether `file` does not need to be closed
    valid: bool,
    unused: @Int(.unsigned, 32 - 2 - @bitSizeOf(Generation)) = 0,
    generation: Generation,

    pub const Slot = std.math.IntFittingRange(0, ipc_storage_buffer_len - 1);
    pub const Generation = @Int(.unsigned, 32 - @bitSizeOf(Slot));

    const SlotAtomic = @Int(.unsigned, std.math.ceilPowerOfTwoAssert(usize, @min(@bitSizeOf(Slot), 8)));

    pub const Index = packed struct(u32) {
        slot: Slot,
        generation: Generation,
    };

    const Data = struct {
        state: State,
        bytes_read: u16,
        main_index: u8,
        start_index: u8,
        nodes_len: u8,

        const State = enum { unused, pending, ready };

        /// No operations have been started on this file.
        const unused: Data = .{
            .state = .unused,
            .bytes_read = 0,
            .main_index = 0,
            .start_index = 0,
            .nodes_len = 0,
        };

        fn findLastPacket(data: *const Data, buffer: *const [max_packet_len]u8) struct { u16, u16 } {
            assert(data.state == .ready);
            var packet_start: u16 = 0;
            var packet_end: u16 = 0;
            const bytes_read = data.bytes_read;
            while (bytes_read - packet_end >= 1) {
                const nodes_len: u16 = buffer[packet_end];
                const packet_len = 1 + nodes_len * (@sizeOf(Node.Storage) + @sizeOf(Node.Parent));
                if (packet_end + packet_len > bytes_read) break;
                packet_start = packet_end;
                packet_end += packet_len;
            }
            return .{ packet_start, packet_end };
        }

        fn rebase(
            data: *Data,
            buffer: *[max_packet_len]u8,
            vec: *[1][]u8,
            batch: *std.Io.Batch,
            slot: Slot,
            packet_end: u16,
        ) void {
            assert(data.state == .ready);
            const remaining = buffer[packet_end..data.bytes_read];
            @memmove(buffer[0..remaining.len], remaining);
            vec.* = .{buffer[remaining.len..]};
            batch.addAt(slot, .{ .file_read_streaming = .{
                .file = global_progress.ipc_files[slot],
                .data = vec,
            } });
            data.state = .pending;
            data.bytes_read = @intCast(remaining.len);
        }
    };
};

pub const TerminalMode = union(enum) {
    off,
    ansi_escape_codes,
    /// This is not the same as being run on windows because other terminals
    /// exist like MSYS/git-bash.
    windows_api: if (is_windows) WindowsApi else noreturn,

    pub const WindowsApi = struct {
        /// The output code page of the console.
        code_page: windows.UINT,
    };
};

pub const Options = struct {
    /// User-provided buffer with static lifetime.
    ///
    /// Used to store the entire write buffer sent to the terminal. Progress output will be truncated if it
    /// cannot fit into this buffer which will look bad but not cause any malfunctions.
    ///
    /// Must be at least 200 bytes.
    draw_buffer: []u8 = &default_draw_buffer,
    /// How many nanoseconds between writing updates to the terminal.
    refresh_rate_ns: Io.Duration = .fromMilliseconds(80),
    /// How many nanoseconds to keep the output hidden
    initial_delay_ns: Io.Duration = .fromMilliseconds(200),
    /// If provided, causes the progress item to have a denominator.
    /// 0 means unknown.
    estimated_total_items: usize = 0,
    root_name: []const u8 = "",
    disable_printing: bool = false,
};

/// Represents one unit of progress. Each node can have children nodes, or
/// one can use integers with `update`.
pub const Node = struct {
    index: OptionalIndex,

    pub const none: Node = .{ .index = .none };

    pub const max_name_len = 120;

    const Storage = extern struct {
        /// Little endian.
        completed_count: u32,
        /// 0 means unknown.
        /// Little endian.
        estimated_total_count: u32,
        name: [max_name_len]u8 align(@alignOf(usize)),

        /// Not thread-safe.
        fn getIpcIndex(s: Storage) ?Ipc.Index {
            return if (s.estimated_total_count == std.math.maxInt(u32)) @bitCast(s.completed_count) else null;
        }

        /// Thread-safe.
        fn setIpcIndex(s: *Storage, ipc_index: Ipc.Index) void {
            // `estimated_total_count` max int indicates the special state that
            // causes `completed_count` to be treated as a file descriptor, so
            // the order here matters.
            @atomicStore(u32, &s.completed_count, @bitCast(ipc_index), .monotonic);
            @atomicStore(u32, &s.estimated_total_count, std.math.maxInt(u32), .release); // synchronizes with acquire in `serialize`
        }

        /// Not thread-safe.
        fn byteSwap(s: *Storage) void {
            s.completed_count = @byteSwap(s.completed_count);
            s.estimated_total_count = @byteSwap(s.estimated_total_count);
        }

        fn copyRoot(dest: *Node.Storage, src: *align(1) const Node.Storage) void {
            dest.* = .{
                .completed_count = src.completed_count,
                .estimated_total_count = src.estimated_total_count,
                .name = if (src.name[0] == 0) dest.name else src.name,
            };
        }

        comptime {
            assert((@sizeOf(Storage) % 4) == 0);
        }
    };

    const Parent = enum(u8) {
        /// Unallocated storage.
        unused = std.math.maxInt(u8) - 1,
        /// Indicates root node.
        none = std.math.maxInt(u8),
        /// Index into `node_storage`.
        _,

        fn unwrap(i: @This()) ?Index {
            return switch (i) {
                .unused, .none => return null,
                else => @enumFromInt(@intFromEnum(i)),
            };
        }
    };

    pub const OptionalIndex = enum(u8) {
        none = std.math.maxInt(u8),
        /// Index into `node_storage`.
        _,

        pub fn unwrap(i: @This()) ?Index {
            if (i == .none) return null;
            return @enumFromInt(@intFromEnum(i));
        }

        fn toParent(i: @This()) Parent {
            assert(@intFromEnum(i) != @intFromEnum(Parent.unused));
            return @enumFromInt(@intFromEnum(i));
        }
    };

    /// Index into `node_storage`.
    pub const Index = enum(u8) {
        _,

        fn toParent(i: @This()) Parent {
            assert(@intFromEnum(i) != @intFromEnum(Parent.unused));
            assert(@intFromEnum(i) != @intFromEnum(Parent.none));
            return @enumFromInt(@intFromEnum(i));
        }

        pub fn toOptional(i: @This()) OptionalIndex {
            return @enumFromInt(@intFromEnum(i));
        }
    };

    /// Create a new child progress node. Thread-safe.
    ///
    /// Passing 0 for `estimated_total_items` means unknown.
    pub fn start(node: Node, name: []const u8, estimated_total_items: usize) Node {
        if (noop_impl) {
            assert(node.index == .none);
            return Node.none;
        }
        const node_index = node.index.unwrap() orelse return Node.none;
        const parent = node_index.toParent();

        const freelist = &global_progress.node_freelist;
        var old_freelist = @atomicLoad(Freelist, freelist, .acquire); // acquire to ensure we have the correct "next" entry
        while (old_freelist.head.unwrap()) |free_index| {
            const next_ptr = freelistNextByIndex(free_index);
            const new_freelist: Freelist = .{
                .head = @atomicLoad(Node.OptionalIndex, next_ptr, .monotonic),
                // We don't need to increment the generation when removing nodes from the free list,
                // only when adding them. (This choice is arbitrary; the opposite would also work.)
                .generation = old_freelist.generation,
            };
            old_freelist = @cmpxchgWeak(
                Freelist,
                freelist,
                old_freelist,
                new_freelist,
                .acquire, // not theoretically necessary, but not allowed to be weaker than the failure order
                .acquire, // ensure we have the correct `node_freelist_next` entry on the next iteration
            ) orelse {
                // We won the allocation race.
                return init(free_index, parent, name, estimated_total_items);
            };
        }

        const free_index = @atomicRmw(u32, &global_progress.node_end_index, .Add, 1, .monotonic);
        if (free_index >= node_storage_buffer_len) {
            // Ran out of node storage memory. Progress for this node will not be tracked.
            _ = @atomicRmw(u32, &global_progress.node_end_index, .Sub, 1, .monotonic);
            return Node.none;
        }

        return init(@enumFromInt(free_index), parent, name, estimated_total_items);
    }

    pub fn startFmt(node: Node, estimated_total_items: usize, comptime format: []const u8, args: anytype) Node {
        var buffer: [max_name_len]u8 = undefined;
        const name = std.fmt.bufPrint(&buffer, format, args) catch &buffer;
        return Node.start(node, name, estimated_total_items);
    }

    /// This is the same as calling `start` and then `end` on the returned `Node`. Thread-safe.
    pub fn completeOne(n: Node) void {
        const index = n.index.unwrap() orelse return;
        const storage = storageByIndex(index);
        _ = @atomicRmw(u32, &storage.completed_count, .Add, 1, .monotonic);
    }

    /// Thread-safe. Bytes after '0' in `new_name` are ignored.
    pub fn setName(n: Node, new_name: []const u8) void {
        const index = n.index.unwrap() orelse return;
        const storage = storageByIndex(index);

        const name_len = @min(max_name_len, std.mem.findScalar(u8, new_name, 0) orelse new_name.len);

        copyAtomicStore(storage.name[0..name_len], new_name[0..name_len]);
        if (name_len < storage.name.len)
            @atomicStore(u8, &storage.name[name_len], 0, .monotonic);
    }

    /// Gets the name of this `Node`.
    /// A pointer to this array can later be passed to `setName` to restore the name.
    pub fn getName(n: Node) [max_name_len]u8 {
        var dest: [max_name_len]u8 align(@alignOf(usize)) = undefined;
        if (n.index.unwrap()) |index| {
            copyAtomicLoad(&dest, &storageByIndex(index).name);
        }
        return dest;
    }

    /// Thread-safe.
    pub fn setCompletedItems(n: Node, completed_items: usize) void {
        const index = n.index.unwrap() orelse return;
        const storage = storageByIndex(index);
        @atomicStore(u32, &storage.completed_count, std.math.lossyCast(u32, completed_items), .monotonic);
    }

    /// Thread-safe. 0 means unknown.
    pub fn setEstimatedTotalItems(n: Node, count: usize) void {
        const index = n.index.unwrap() orelse return;
        const storage = storageByIndex(index);
        // Avoid u32 max int which is used to indicate a special state.
        const saturated_total_count = @min(std.math.maxInt(u32) - 1, count);
        @atomicStore(u32, &storage.estimated_total_count, saturated_total_count, .monotonic);
    }

    /// Thread-safe.
    pub fn increaseEstimatedTotalItems(n: Node, count: usize) void {
        const index = n.index.unwrap() orelse return;
        const storage = storageByIndex(index);
        // Avoid u32 max int which is used to indicate a special state.
        const saturated_total_count = @min(std.math.maxInt(u32) - 1, count);
        _ = @atomicRmw(u32, &storage.estimated_total_count, .Add, saturated_total_count, .monotonic);
    }

    /// Finish a started `Node`. Thread-safe.
    pub fn end(n: Node) void {
        if (noop_impl) {
            assert(n.index == .none);
            return;
        }
        const index = n.index.unwrap() orelse return;
        const io = global_progress.io;
        const parent_ptr = parentByIndex(index);
        if (@atomicLoad(Node.Parent, parent_ptr, .monotonic).unwrap()) |parent_index| {
            _ = @atomicRmw(u32, &storageByIndex(parent_index).completed_count, .Add, 1, .monotonic);
            @atomicStore(Node.Parent, parent_ptr, .unused, .monotonic);

            if (storageByIndex(index).getIpcIndex()) |ipc_index| {
                const file = global_progress.ipc_files[ipc_index.slot];
                const ipc = @atomicRmw(
                    Ipc,
                    &global_progress.ipc[ipc_index.slot],
                    .And,
                    .{ .locked = true, .valid = false, .generation = std.math.maxInt(Ipc.Generation) },
                    .release,
                );
                assert(ipc.valid and ipc.generation == ipc_index.generation);
                if (!ipc.locked) file.close(io);
            }

            const freelist = &global_progress.node_freelist;
            var old_freelist = @atomicLoad(Freelist, freelist, .monotonic);
            while (true) {
                @atomicStore(Node.OptionalIndex, freelistNextByIndex(index), old_freelist.head, .monotonic);
                old_freelist = @cmpxchgWeak(
                    Freelist,
                    freelist,
                    old_freelist,
                    .{ .head = index.toOptional(), .generation = old_freelist.generation +% 1 },
                    .release, // ensure a matching `start` sees the freelist link written above
                    .monotonic, // our write above is irrelevant if we need to retry
                ) orelse {
                    // We won the race.
                    return;
                };
            }
        } else {
            if (global_progress.update_worker) |*worker| worker.cancel(io) catch {};
            for (&global_progress.ipc, &global_progress.ipc_files) |ipc, ipc_file| {
                assert(!ipc.locked or !ipc.valid); // missing call to end()
                if (ipc.locked or ipc.valid) ipc_file.close(io);
            }
        }
    }

    /// Used by `std.process.Child`. Thread-safe.
    pub fn setIpcFile(node: Node, expected_io_userdata: ?*anyopaque, file: Io.File) void {
        const index = node.index.unwrap() orelse return;
        const io = global_progress.io;
        assert(io.userdata == expected_io_userdata);
        for (0..ipc_storage_buffer_len) |_| {
            const slot: Ipc.Slot = @truncate(
                @atomicRmw(Ipc.SlotAtomic, &global_progress.ipc_next, .Add, 1, .monotonic),
            );
            if (slot >= ipc_storage_buffer_len) continue;
            const ipc_ptr = &global_progress.ipc[slot];
            const ipc = @atomicLoad(Ipc, ipc_ptr, .monotonic);
            if (ipc.locked or ipc.valid) continue;
            const generation = ipc.generation +% 1;
            if (@cmpxchgWeak(
                Ipc,
                ipc_ptr,
                ipc,
                .{ .locked = false, .valid = true, .generation = generation },
                .acquire,
                .monotonic,
            )) |_| continue;
            global_progress.ipc_files[slot] = file;
            storageByIndex(index).setIpcIndex(.{ .slot = slot, .generation = generation });
            break;
        } else file.close(io);
    }

    pub fn setIpcIndex(node: Node, ipc_index: Ipc.Index) void {
        storageByIndex(node.index.unwrap() orelse return).setIpcIndex(ipc_index);
    }

    /// Not thread-safe.
    pub fn takeIpcIndex(node: Node) ?Ipc.Index {
        const storage = storageByIndex(node.index.unwrap() orelse return null);
        assert(storage.estimated_total_count == std.math.maxInt(u32));
        @atomicStore(u32, &storage.estimated_total_count, 0, .monotonic);
        return @bitCast(storage.completed_count);
    }

    fn storageByIndex(index: Node.Index) *Node.Storage {
        return &global_progress.node_storage[@intFromEnum(index)];
    }

    fn parentByIndex(index: Node.Index) *Node.Parent {
        return &global_progress.node_parents[@intFromEnum(index)];
    }

    fn freelistNextByIndex(index: Node.Index) *Node.OptionalIndex {
        return &global_progress.node_freelist_next[@intFromEnum(index)];
    }

    fn init(free_index: Index, parent: Parent, name: []const u8, estimated_total_items: usize) Node {
        assert(parent == .none or @intFromEnum(parent) < node_storage_buffer_len);

        const storage = storageByIndex(free_index);
        @atomicStore(u32, &storage.completed_count, 0, .monotonic);
        // Avoid u32 max int which is used to indicate a special state.
        const saturated_total_count = @min(std.math.maxInt(u32) - 1, estimated_total_items);
        @atomicStore(u32, &storage.estimated_total_count, saturated_total_count, .monotonic);
        const name_len = @min(max_name_len, name.len);
        copyAtomicStore(storage.name[0..name_len], name[0..name_len]);
        if (name_len < storage.name.len)
            @atomicStore(u8, &storage.name[name_len], 0, .monotonic);

        const parent_ptr = parentByIndex(free_index);
        if (std.debug.runtime_safety) {
            assert(@atomicLoad(Node.Parent, parent_ptr, .monotonic) == .unused);
        }
        @atomicStore(Node.Parent, parent_ptr, parent, .monotonic);

        return .{ .index = free_index.toOptional() };
    }
};

var global_progress: Progress = .{
    .io = undefined,
    .terminal = undefined,
    .terminal_mode = .off,
    .update_worker = null,
    .redraw_event = .unset,
    .refresh_rate_ns = undefined,
    .initial_delay_ns = undefined,
    .rows = 0,
    .cols = 0,
    .draw_buffer = undefined,
    .need_clear = false,
    .status = .working,

    .node_parents = undefined,
    .node_storage = undefined,
    .node_freelist_next = undefined,
    .node_freelist = .{ .head = .none, .generation = 0 },
    .node_end_index = 0,

    .ipc_next = 0,
    .ipc = undefined,
    .ipc_files = undefined,

    .start_failure = .unstarted,
};

pub const StartFailure = union(enum) {
    unstarted,
    spawn_ipc_worker: error{ConcurrencyUnavailable},
    spawn_update_worker: error{ConcurrencyUnavailable},
    parent_ipc: error{ UnsupportedOperation, UnrecognizedFormat },
};

/// One less than a power of two ensures `max_packet_len` is already a power of two.
const node_storage_buffer_len = ipc_storage_buffer_len - 1;

/// Power of two to avoid wasted `ipc_next` increments.
const ipc_storage_buffer_len = 128;

pub const max_packet_len = std.math.ceilPowerOfTwoAssert(
    usize,
    1 + node_storage_buffer_len * (@sizeOf(Node.Storage) + @sizeOf(Node.OptionalIndex)),
);

var default_draw_buffer: [4096]u8 = undefined;

var debug_start_trace = std.debug.Trace.init;

pub const have_ipc = switch (builtin.os.tag) {
    .wasi, .freestanding => false,
    else => true,
};

const noop_impl = builtin.single_threaded or switch (builtin.os.tag) {
    .wasi, .freestanding => true,
    else => false,
} or switch (builtin.zig_backend) {
    else => false,
};

pub const ParentFileError = error{
    UnsupportedOperation,
    EnvironmentVariableMissing,
    UnrecognizedFormat,
};

/// Initializes a global Progress instance.
///
/// Asserts there is only one global Progress instance.
///
/// Call `Node.end` when done.
///
/// If an error occurs, `start_failure` will be populated.
pub fn start(io: Io, options: Options) Node {
    // Ensure there is only 1 global Progress object.
    if (global_progress.node_end_index != 0) {
        debug_start_trace.dump();
        unreachable;
    }
    debug_start_trace.add("first initialized here");

    @memset(&global_progress.node_parents, .unused);
    @memset(&global_progress.ipc, .{ .locked = false, .valid = false, .generation = 0 });
    const root_node = Node.init(@enumFromInt(0), .none, options.root_name, options.estimated_total_items);
    global_progress.node_end_index = 1;

    assert(options.draw_buffer.len >= 200);
    global_progress.draw_buffer = options.draw_buffer;
    global_progress.refresh_rate_ns = @intCast(options.refresh_rate_ns.toNanoseconds());
    global_progress.initial_delay_ns = @intCast(options.initial_delay_ns.toNanoseconds());

    if (noop_impl) return .none;

    global_progress.io = io;

    if (io.vtable.progressParentFile(io.userdata)) |ipc_file| {
        global_progress.update_worker = io.concurrent(ipcThreadRun, .{ io, ipc_file }) catch |err| {
            global_progress.start_failure = .{ .spawn_ipc_worker = err };
            return .none;
        };
    } else |env_err| switch (env_err) {
        error.EnvironmentVariableMissing => {
            if (options.disable_printing) return .none;
            const stderr: Io.File = .stderr();
            global_progress.terminal = stderr;
            if (stderr.enableAnsiEscapeCodes(io)) |_| {
                global_progress.terminal_mode = .ansi_escape_codes;
            } else |_| if (is_windows) {
                var get_console_cp = windows.CONSOLE.USER_IO.GET_CP(.Output);
                // Normally, we would pass `null` to `operate` here as the kernel32
                // function does not accept a handle, however, if we pass one anyway,
                // then we will get an error if the handle is not associated with
                // this process's console, effectively combining an `isTty` check
                // into the same syscall.
                switch (get_console_cp.operate(io, stderr) catch |err| switch (err) {
                    error.Canceled => {
                        io.recancel();
                        return .none;
                    },
                }) {
                    .SUCCESS => global_progress.terminal_mode = .{ .windows_api = .{
                        .code_page = get_console_cp.Data.CodePage,
                    } },
                    .INVALID_HANDLE => {},
                    else => {},
                }
            }
            if (future: switch (global_progress.terminal_mode) {
                .off => return .none,
                .ansi_escape_codes => {
                    if (have_sigwinch) {
                        const act: posix.Sigaction = .{
                            .handler = .{ .sigaction = handleSigWinch },
                            .mask = posix.sigemptyset(),
                            .flags = (posix.SA.SIGINFO | posix.SA.RESTART),
                        };
                        posix.sigaction(.WINCH, &act, null);
                    }
                    break :future io.concurrent(updateTask, .{io});
                },
                .windows_api => io.concurrent(windowsApiUpdateTask, .{io}),
            }) |future| {
                global_progress.update_worker = future;
            } else |err| {
                global_progress.start_failure = .{ .spawn_update_worker = err };
                return .none;
            }
        },
        else => |e| {
            global_progress.start_failure = .{ .parent_ipc = e };
            return .none;
        },
    }

    return root_node;
}

pub fn setStatus(new_status: Status) void {
    if (noop_impl) return;
    @atomicStore(Status, &global_progress.status, new_status, .monotonic);
}

/// Returns whether a resize is needed to learn the terminal size.
fn wait(io: Io, timeout_ns: u64) Io.Cancelable!bool {
    const timeout: Io.Timeout = .{ .duration = .{
        .clock = .awake,
        .raw = .fromNanoseconds(timeout_ns),
    } };
    const resize_flag = if (global_progress.redraw_event.waitTimeout(io, timeout)) |_| true else |err| switch (err) {
        error.Timeout => false,
        error.Canceled => |e| return e,
    };
    global_progress.redraw_event.reset();
    return resize_flag or (global_progress.cols == 0);
}

const WorkerError = error{WindowTooSmall} || Io.ConcurrentError || Io.Cancelable ||
    Io.File.Writer.Error || Io.Operation.FileReadStreaming.Error;

fn updateTask(io: Io) WorkerError!void {
    // Store this data in the thread so that it does not need to be part of the
    // linker data of the main executable.
    var serialized_buffer: Serialized.Buffer = undefined;
    serialized_buffer.init();
    defer serialized_buffer.batch.cancel(io);

    // In this function we bypass the wrapper code inside `Io.lockStderr` /
    // `Io.tryLockStderr` in order to avoid clearing the terminal twice.
    // We still want to go through the `Io` instance however in case it uses a
    // task-switching mutex.

    try maybeUpdateSize(io, try wait(io, global_progress.initial_delay_ns));
    errdefer {
        const cancel_protection = io.swapCancelProtection(.blocked);
        defer _ = io.swapCancelProtection(cancel_protection);
        const stderr = io.vtable.lockStderr(io.userdata, null) catch |err| switch (err) {
            error.Canceled => unreachable, // blocked
        };
        defer io.unlockStderr();
        clearWrittenWithEscapeCodes(stderr.file_writer) catch {};
    }
    while (true) {
        const buffer, _ = try computeRedraw(io, &serialized_buffer);
        if (try io.vtable.tryLockStderr(io.userdata, null)) |locked_stderr| {
            defer io.unlockStderr();
            global_progress.need_clear = true;
            locked_stderr.file_writer.interface.writeAll(buffer) catch |err| switch (err) {
                error.WriteFailed => return locked_stderr.file_writer.err.?,
            };
        }

        try maybeUpdateSize(io, try wait(io, global_progress.refresh_rate_ns));
    }
}

const WindowsApiError = Io.Cancelable || Io.UnexpectedError;

fn windowsApiWriteMarker(io: Io) WindowsApiError!void {
    // Write the marker that we will use to find the beginning of the progress when clearing.
    // Note: This doesn't have to use WriteConsoleW, but doing so avoids dealing with the code page.
    const terminal = global_progress.terminal;
    var write_console = windows.CONSOLE.USER_IO.WRITE(.WideCharacter);
    const buffer = [1]windows.WCHAR{windows_api_start_marker};
    switch ((try io.operate(.{ .device_io_control = .{
        .file = terminal,
        .code = windows.IOCTL.CONDRV.ISSUE_USER_IO,
        .in = @ptrCast(&write_console.request(null, 1, .{
            .{ .Size = @sizeOf(@TypeOf(buffer)), .Pointer = &buffer },
        }, 0, .{})),
    } })).device_io_control.u.Status) {
        .SUCCESS => {},
        .CANCELLED => unreachable,
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn windowsApiUpdateTask(io: Io) WorkerError!void {
    // Store this data in the thread so that it does not need to be part of the
    // linker data of the main executable.
    var serialized_buffer: Serialized.Buffer = undefined;
    serialized_buffer.init();
    defer serialized_buffer.batch.cancel(io);

    // In this function we bypass the wrapper code inside `Io.lockStderr` /
    // `Io.tryLockStderr` in order to avoid clearing the terminal twice.
    // We still want to go through the `Io` instance however in case it uses a
    // task-switching mutex.

    try maybeUpdateSize(io, try wait(io, global_progress.initial_delay_ns));
    errdefer {
        const cancel_protection = io.swapCancelProtection(.blocked);
        defer _ = io.swapCancelProtection(cancel_protection);
        _ = io.vtable.lockStderr(io.userdata, null) catch |err| switch (err) {
            error.Canceled => unreachable, // blocked
        };
        defer io.unlockStderr();
        clearWrittenWindowsApi(io) catch {};
    }
    while (true) {
        const buffer, const nl_n = try computeRedraw(io, &serialized_buffer);
        if (io.vtable.tryLockStderr(io.userdata, null) catch return) |locked_stderr| {
            defer io.unlockStderr();
            try clearWrittenWindowsApi(io);
            try windowsApiWriteMarker(io);
            global_progress.need_clear = true;
            locked_stderr.file_writer.interface.writeAll(buffer) catch |err| switch (err) {
                error.WriteFailed => return locked_stderr.file_writer.err.?,
            };
            windowsApiMoveToMarker(io, nl_n) catch return;
        }

        try maybeUpdateSize(io, try wait(io, global_progress.refresh_rate_ns));
    }
}

fn ipcThreadRun(io: Io, file: Io.File) WorkerError!void {
    // Store this data in the thread so that it does not need to be part of the
    // linker data of the main executable.
    var serialized_buffer: Serialized.Buffer = undefined;
    serialized_buffer.init();
    defer serialized_buffer.batch.cancel(io);
    var fw = file.writerStreaming(io, &.{});

    _ = try io.sleep(.fromNanoseconds(global_progress.initial_delay_ns), .awake);
    while (true) {
        writeIpc(&fw.interface, try serialize(io, &serialized_buffer)) catch |err| switch (err) {
            error.WriteFailed => return fw.err.?,
        };

        _ = try io.sleep(.fromNanoseconds(global_progress.refresh_rate_ns), .awake);
    }
}

const start_sync = "\x1b[?2026h";
const up_one_line = "\x1bM";
const clear = "\x1b[J";
const save = "\x1b7";
const restore = "\x1b8";
const finish_sync = "\x1b[?2026l";

const progress_remove = "\x1b]9;4;0\x1b\\";
const @"progress_normal {d}" = "\x1b]9;4;1;{d}\x1b\\";
const @"progress_error {d}" = "\x1b]9;4;2;{d}\x1b\\";
const progress_pulsing = "\x1b]9;4;3\x1b\\";
const progress_pulsing_error = "\x1b]9;4;2\x1b\\";
const progress_normal_100 = "\x1b]9;4;1;100\x1b\\";
const progress_error_100 = "\x1b]9;4;2;100\x1b\\";

const TreeSymbol = enum {
    /// ├─
    tee,
    /// │
    line,
    /// └─
    langle,

    const Encoding = enum {
        ansi_escapes,
        code_page_437,
        utf8,
        ascii,
    };

    /// The escape sequence representation as a string literal
    fn escapeSeq(symbol: TreeSymbol) *const [9:0]u8 {
        return switch (symbol) {
            .tee => "\x1B\x28\x30\x74\x71\x1B\x28\x42 ",
            .line => "\x1B\x28\x30\x78\x1B\x28\x42  ",
            .langle => "\x1B\x28\x30\x6d\x71\x1B\x28\x42 ",
        };
    }

    fn bytes(symbol: TreeSymbol, encoding: Encoding) []const u8 {
        return switch (encoding) {
            .ansi_escapes => escapeSeq(symbol),
            .code_page_437 => switch (symbol) {
                .tee => "\xC3\xC4 ",
                .line => "\xB3  ",
                .langle => "\xC0\xC4 ",
            },
            .utf8 => switch (symbol) {
                .tee => "├─ ",
                .line => "│  ",
                .langle => "└─ ",
            },
            .ascii => switch (symbol) {
                .tee => "|- ",
                .line => "|  ",
                .langle => "+- ",
            },
        };
    }

    fn maxByteLen(symbol: TreeSymbol) usize {
        var max: usize = 0;
        inline for (@typeInfo(Encoding).@"enum".fields) |field| {
            const len = symbol.bytes(@field(Encoding, field.name)).len;
            max = @max(max, len);
        }
        return max;
    }
};

fn appendTreeSymbol(symbol: TreeSymbol, buf: []u8, start_i: usize) usize {
    switch (global_progress.terminal_mode) {
        .off => unreachable,
        .ansi_escape_codes => {
            const bytes = symbol.escapeSeq();
            buf[start_i..][0..bytes.len].* = bytes.*;
            return start_i + bytes.len;
        },
        .windows_api => |windows_api| {
            const bytes = switch (windows_api.code_page) {
                // Code page 437 is the default code page and contains the box drawing symbols
                437 => symbol.bytes(.code_page_437),
                // UTF-8
                65001 => symbol.bytes(.utf8),
                // Fall back to ASCII approximation
                else => symbol.bytes(.ascii),
            };
            @memcpy(buf[start_i..][0..bytes.len], bytes);
            return start_i + bytes.len;
        },
    }
}

pub fn clearWrittenWithEscapeCodes(file_writer: *Io.File.Writer) Io.Writer.Error!void {
    if (noop_impl or !global_progress.need_clear) return;
    try file_writer.interface.writeAll(clear ++ progress_remove);
    global_progress.need_clear = false;
}

/// U+25BA or ►
const windows_api_start_marker = 0x25BA;

fn clearWrittenWindowsApi(io: Io) WindowsApiError!void {
    // This uses a 'marker' strategy. The idea is:
    // - Always write a marker (in this case U+25BA or ►) at the beginning of the progress
    // - Get the current cursor position (at the end of the progress)
    // - Subtract the number of lines written to get the expected start of the progress
    // - Check to see if the first character at the start of the progress is the marker
    // - If it's not the marker, keep checking the line before until we find it
    // - Clear the screen from that position down, and set the cursor position to the start
    //
    // This strategy works even if there is line wrapping, and can handle the window
    // being resized/scrolled arbitrarily.
    //
    // Notes:
    // - Ideally, the marker would be a zero-width character, but the Windows console
    //   doesn't seem to support rendering zero-width characters (they show up as a space)
    // - This same marker idea could technically be done with an attribute instead
    //   (https://learn.microsoft.com/en-us/windows/console/console-screen-buffers#character-attributes)
    //   but it must be a valid attribute and it actually needs to apply to the first
    //   character in order to be readable via ReadConsoleOutputAttribute. It doesn't seem
    //   like any of the available attributes are invisible/benign.
    if (!global_progress.need_clear) return;
    const terminal = global_progress.terminal;
    const screen_area = @as(windows.DWORD, global_progress.cols) * global_progress.rows;

    var get_console_info = windows.CONSOLE.USER_IO.GET_SCREEN_BUFFER_INFO;
    switch (try get_console_info.operate(io, terminal)) {
        .SUCCESS => {},
        else => |status| return windows.unexpectedStatus(status),
    }
    var fill_spaces = windows.CONSOLE.USER_IO.FILL(
        .{ .WideCharacter = ' ' },
        screen_area,
        get_console_info.Data.dwCursorPosition,
    );
    switch (try fill_spaces.operate(io, terminal)) {
        .SUCCESS => {},
        else => |status| return windows.unexpectedStatus(status),
    }
}

fn windowsApiMoveToMarker(io: Io, nl_n: usize) WindowsApiError!void {
    const terminal = global_progress.terminal;
    var get_console_info = windows.CONSOLE.USER_IO.GET_SCREEN_BUFFER_INFO;
    switch (try get_console_info.operate(io, terminal)) {
        .SUCCESS => {},
        else => |status| return windows.unexpectedStatus(status),
    }
    const cursor_pos = get_console_info.Data.dwCursorPosition;
    const expected_y = cursor_pos.Y - @as(i16, @intCast(nl_n));
    var start_pos: windows.COORD = .{ .X = 0, .Y = expected_y };
    while (start_pos.Y >= 0) : (start_pos.Y -= 1) {
        var read_output_char = windows.CONSOLE.USER_IO.READ_OUTPUT_CHARACTER(start_pos, .WideCharacter);
        var buffer: [1]windows.WCHAR = undefined;
        switch ((try io.operate(.{ .device_io_control = .{
            .file = .{
                .handle = windows.peb().ProcessParameters.ConsoleHandle,
                .flags = .{ .nonblocking = false },
            },
            .code = windows.IOCTL.CONDRV.ISSUE_USER_IO,
            .in = @ptrCast(&read_output_char.request(terminal, 0, .{}, 1, .{
                .{ .Size = @sizeOf(@TypeOf(buffer)), .Pointer = &buffer },
            })),
        } })).device_io_control.u.Status) {
            .SUCCESS => {},
            .CANCELLED => unreachable,
            else => |status| return windows.unexpectedStatus(status),
        }
        if (read_output_char.Data.nLength >= 1 and buffer[0] == windows_api_start_marker) break;
    } else {
        // If we couldn't find the marker, then just assume that no lines wrapped
        start_pos = .{ .X = 0, .Y = expected_y };
    }
    var set_cursor_position = windows.CONSOLE.USER_IO.SET_CURSOR_POSITION(start_pos);
    switch (try set_cursor_position.operate(io, terminal)) {
        .SUCCESS => {},
        else => |status| return windows.unexpectedStatus(status),
    }
}

const Children = struct {
    child: Node.OptionalIndex,
    sibling: Node.OptionalIndex,
};

const Serialized = struct {
    parents: []Node.Parent,
    storage: []Node.Storage,

    const Buffer = struct {
        parents: [node_storage_buffer_len]Node.Parent,
        storage: [node_storage_buffer_len]Node.Storage,

        ipc_start: u8,
        ipc_end: u8,
        ipc_data: [ipc_storage_buffer_len]Ipc.Data,
        ipc_buffers: [ipc_storage_buffer_len][max_packet_len]u8,
        ipc_vecs: [ipc_storage_buffer_len][1][]u8,
        batch_storage: [ipc_storage_buffer_len]Io.Operation.Storage,
        batch: Io.Batch,

        fn init(buffer: *Buffer) void {
            buffer.ipc_start = 0;
            buffer.ipc_end = 0;
            @memset(&buffer.ipc_data, .unused);
            buffer.batch = .init(&buffer.batch_storage);
        }
    };
};

fn serialize(io: Io, serialized_buffer: *Serialized.Buffer) !Serialized {
    var prev_parents: [node_storage_buffer_len]Node.Parent = undefined;
    var prev_storage: [node_storage_buffer_len]Node.Storage = undefined;
    {
        const ipc_start = serialized_buffer.ipc_start;
        const ipc_end = serialized_buffer.ipc_end;
        @memcpy(prev_parents[ipc_start..ipc_end], serialized_buffer.parents[ipc_start..ipc_end]);
        @memcpy(prev_storage[ipc_start..ipc_end], serialized_buffer.storage[ipc_start..ipc_end]);
    }

    // Iterate all of the nodes and construct a serializable copy of the state that can be examined
    // without atomics. The `@min` call is here because `node_end_index` might briefly exceed the
    // node count sometimes.
    const end_index = @min(
        @atomicLoad(u32, &global_progress.node_end_index, .monotonic),
        node_storage_buffer_len,
    );
    var map: [node_storage_buffer_len]Node.OptionalIndex = undefined;
    var serialized_len: u8 = 0;
    var maybe_ipc_start: ?u8 = null;
    for (
        global_progress.node_parents[0..end_index],
        global_progress.node_storage[0..end_index],
        map[0..end_index],
    ) |*parent_ptr, *storage_ptr, *map_entry| {
        const parent = @atomicLoad(Node.Parent, parent_ptr, .monotonic);
        if (parent == .unused) {
            // We might read "mixed" node data in this loop, due to weird atomic things
            // or just a node actually being freed while this loop runs. That could cause
            // there to be a parent reference to a nonexistent node. Without this assignment,
            // this would lead to the map entry containing stale data. By assigning none, the
            // child node with the bad parent pointer will be harmlessly omitted from the tree.
            //
            // Note that there's no concern of potentially creating "looping" data if we read
            // "mixed" node data like this, because if a node is (directly or indirectly) its own
            // parent, it will just not be printed at all. The general idea here is that performance
            // is more important than 100% correct output every frame, given that this API is likely
            // to be used in hot paths!
            map_entry.* = .none;
            continue;
        }
        const dest_storage = &serialized_buffer.storage[serialized_len];
        copyAtomicLoad(&dest_storage.name, &storage_ptr.name);
        dest_storage.estimated_total_count = @atomicLoad(u32, &storage_ptr.estimated_total_count, .acquire); // sychronizes with release in `setIpcIndex`
        dest_storage.completed_count = @atomicLoad(u32, &storage_ptr.completed_count, .monotonic);

        serialized_buffer.parents[serialized_len] = parent;
        map_entry.* = @enumFromInt(serialized_len);
        if (maybe_ipc_start == null and dest_storage.getIpcIndex() != null) maybe_ipc_start = serialized_len;
        serialized_len += 1;
    }

    // Remap parents to point inside serialized arrays.
    for (serialized_buffer.parents[0..serialized_len]) |*parent| {
        parent.* = switch (parent.*) {
            .unused => unreachable,
            .none => .none,
            _ => |p| map[@intFromEnum(p)].toParent(),
        };
    }

    // Fill pipe buffers.
    const batch = &serialized_buffer.batch;
    batch.awaitConcurrent(io, .{
        .duration = .{ .raw = .zero, .clock = .awake },
    }) catch |err| switch (err) {
        error.Timeout => {},
        else => |e| return e,
    };
    var ready_len: u8 = 0;
    while (batch.next()) |operation| switch (operation.index) {
        0...ipc_storage_buffer_len - 1 => {
            const ipc_data = &serialized_buffer.ipc_data[operation.index];
            ipc_data.bytes_read += @intCast(
                operation.result.file_read_streaming catch |err| switch (err) {
                    error.EndOfStream => {
                        const file = global_progress.ipc_files[operation.index];
                        const ipc = @atomicRmw(
                            Ipc,
                            &global_progress.ipc[operation.index],
                            .And,
                            .{
                                .locked = false,
                                .valid = true,
                                .generation = std.math.maxInt(Ipc.Generation),
                            },
                            .release,
                        );
                        assert(ipc.locked);
                        if (!ipc.valid) file.close(io);
                        ipc_data.* = .unused;
                        continue;
                    },
                    else => |e| return e,
                },
            );
            assert(ipc_data.state == .pending);
            ipc_data.state = .ready;
            ready_len += 1;
        },
        else => unreachable,
    };

    // Find nodes which correspond to child processes.
    const ipc_start = maybe_ipc_start orelse serialized_len;
    serialized_buffer.ipc_start = ipc_start;
    for (
        serialized_buffer.parents[ipc_start..serialized_len],
        serialized_buffer.storage[ipc_start..serialized_len],
        ipc_start..,
    ) |main_parent, *main_storage, main_index| {
        if (main_parent == .unused) continue;
        const ipc_index = main_storage.getIpcIndex() orelse continue;
        const ipc = &global_progress.ipc[ipc_index.slot];
        const ipc_data = &serialized_buffer.ipc_data[ipc_index.slot];
        state: switch (ipc_data.state) {
            .unused => {
                if (@cmpxchgWeak(
                    Ipc,
                    ipc,
                    .{ .locked = false, .valid = true, .generation = ipc_index.generation },
                    .{ .locked = true, .valid = true, .generation = ipc_index.generation },
                    .acquire,
                    .monotonic,
                )) |_| continue;

                const ipc_vec = &serialized_buffer.ipc_vecs[ipc_index.slot];
                ipc_vec.* = .{&serialized_buffer.ipc_buffers[ipc_index.slot]};
                batch.addAt(ipc_index.slot, .{ .file_read_streaming = .{
                    .file = global_progress.ipc_files[ipc_index.slot],
                    .data = ipc_vec,
                } });

                ipc_data.* = .{
                    .state = .pending,
                    .bytes_read = 0,
                    .main_index = @intCast(main_index),
                    .start_index = serialized_len,
                    .nodes_len = 0,
                };
                main_storage.completed_count = 0;
                main_storage.estimated_total_count = 0;
            },
            .pending => {
                const start_index = ipc_data.start_index;
                const nodes_len = @min(ipc_data.nodes_len, node_storage_buffer_len - serialized_len);

                main_storage.copyRoot(&prev_storage[ipc_data.main_index]);
                @memcpy(
                    serialized_buffer.storage[serialized_len..][0..nodes_len],
                    prev_storage[start_index..][0..nodes_len],
                );
                for (
                    serialized_buffer.parents[serialized_len..][0..nodes_len],
                    prev_parents[serialized_len..][0..nodes_len],
                ) |*parent, prev_parent| parent.* = switch (prev_parent) {
                    .none, .unused => .none,
                    _ => if (@intFromEnum(prev_parent) == ipc_data.main_index)
                        @enumFromInt(main_index)
                    else if (@intFromEnum(prev_parent) >= start_index and
                        @intFromEnum(prev_parent) < start_index + nodes_len)
                        @enumFromInt(@intFromEnum(prev_parent) - start_index + serialized_len)
                    else
                        .none,
                };

                ipc_data.main_index = @intCast(main_index);
                ipc_data.start_index = serialized_len;
                ipc_data.nodes_len = nodes_len;
                serialized_len += nodes_len;
            },
            .ready => {
                const ipc_buffer = &serialized_buffer.ipc_buffers[ipc_index.slot];
                const packet_start, const packet_end = ipc_data.findLastPacket(ipc_buffer);
                const packet_is_empty = packet_end - packet_start <= 1;
                if (!packet_is_empty) {
                    const storage, const parents, const nodes_len = packet_contents: {
                        var packet_index: usize = packet_start;
                        const nodes_len: u16 = ipc_buffer[packet_index];
                        packet_index += 1;
                        const storage_bytes =
                            ipc_buffer[packet_index..][0 .. nodes_len * @sizeOf(Node.Storage)];
                        packet_index += storage_bytes.len;
                        const parents_bytes =
                            ipc_buffer[packet_index..][0 .. nodes_len * @sizeOf(Node.Parent)];
                        packet_index += parents_bytes.len;
                        assert(packet_index == packet_end);
                        const storage: []align(1) const Node.Storage = @ptrCast(storage_bytes);
                        const parents: []align(1) const Node.Parent = @ptrCast(parents_bytes);
                        const children_nodes_len =
                            @min(nodes_len - 1, node_storage_buffer_len - serialized_len);
                        break :packet_contents .{ storage, parents, children_nodes_len };
                    };

                    // Mount the root here.
                    main_storage.copyRoot(&storage[0]);
                    if (is_big_endian) main_storage.byteSwap();

                    // Copy the rest of the tree to the end.
                    const serialized_storage =
                        serialized_buffer.storage[serialized_len..][0..nodes_len];
                    @memcpy(serialized_storage, storage[1..][0..nodes_len]);
                    if (is_big_endian) for (serialized_storage) |*s| s.byteSwap();

                    // Patch up parent pointers taking into account how the subtree is mounted.
                    for (
                        serialized_buffer.parents[serialized_len..][0..nodes_len],
                        parents[1..][0..nodes_len],
                    ) |*parent, prev_parent| parent.* = switch (prev_parent) {
                        // Fix bad data so the rest of the code does not see `unused`.
                        .none, .unused => .none,
                        // Root node is being mounted here.
                        @as(Node.Parent, @enumFromInt(0)) => @enumFromInt(main_index),
                        // Other nodes mounted at the end.
                        // Don't trust child data; if the data is outside the expected range,
                        // ignore the data. This also handles the case when data was truncated.
                        _ => if (@intFromEnum(prev_parent) <= nodes_len)
                            @enumFromInt(@intFromEnum(prev_parent) - 1 + serialized_len)
                        else
                            .none,
                    };

                    ipc_data.main_index = @intCast(main_index);
                    ipc_data.start_index = serialized_len;
                    ipc_data.nodes_len = nodes_len;
                    serialized_len += nodes_len;
                }
                const ipc_vec = &serialized_buffer.ipc_vecs[ipc_index.slot];
                ipc_data.rebase(ipc_buffer, ipc_vec, batch, ipc_index.slot, packet_end);
                ready_len -= 1;
                if (packet_is_empty) continue :state .pending;
            },
        }
    }
    serialized_buffer.ipc_end = serialized_len;

    // Ignore data from unused pipes. This ensures that if a child process exists we will
    // eventually see `EndOfStream` and close the pipe.
    if (ready_len > 0) for (
        &serialized_buffer.ipc_data,
        &serialized_buffer.ipc_buffers,
        &serialized_buffer.ipc_vecs,
        0..,
    ) |*ipc_data, *ipc_buffer, *ipc_vec, ipc_slot| switch (ipc_data.state) {
        .unused, .pending => {},
        .ready => {
            _, const packet_end = ipc_data.findLastPacket(ipc_buffer);
            ipc_data.rebase(ipc_buffer, ipc_vec, batch, @intCast(ipc_slot), packet_end);
            ready_len -= 1;
        },
    };
    assert(ready_len == 0);

    return .{
        .parents = serialized_buffer.parents[0..serialized_len],
        .storage = serialized_buffer.storage[0..serialized_len],
    };
}

fn computeRedraw(io: Io, serialized_buffer: *Serialized.Buffer) !struct { []u8, usize } {
    if (global_progress.rows == 0 or global_progress.cols == 0) return error.WindowTooSmall;

    const serialized = try serialize(io, serialized_buffer);

    // Now we can analyze our copy of the graph without atomics, reconstructing
    // children lists which do not exist in the canonical data. These are
    // needed for tree traversal below.

    var children_buffer: [node_storage_buffer_len]Children = undefined;
    const children = children_buffer[0..serialized.parents.len];

    @memset(children, .{ .child = .none, .sibling = .none });

    for (serialized.parents, 0..) |parent, child_index_usize| {
        const child_index: Node.Index = @enumFromInt(child_index_usize);
        assert(parent != .unused);
        const parent_index = parent.unwrap() orelse continue;
        const children_node = &children[@intFromEnum(parent_index)];
        if (children_node.child.unwrap()) |existing_child_index| {
            const existing_child = &children[@intFromEnum(existing_child_index)];
            children[@intFromEnum(child_index)].sibling = existing_child.sibling;
            existing_child.sibling = child_index.toOptional();
        } else {
            children_node.child = child_index.toOptional();
        }
    }

    // The strategy is, with every redraw:
    // erase to end of screen, write, move cursor to beginning of line, move cursor up N lines
    // This keeps the cursor at the beginning so that unlocked stderr writes
    // don't get eaten by the clear.

    var i: usize = 0;
    const buf = global_progress.draw_buffer;

    if (global_progress.terminal_mode == .ansi_escape_codes) {
        buf[i..][0..start_sync.len].* = start_sync.*;
        i += start_sync.len;
    }

    switch (global_progress.terminal_mode) {
        .off => unreachable,
        .ansi_escape_codes => {
            buf[i..][0..clear.len].* = clear.*;
            i += clear.len;
        },
        .windows_api => {},
    }

    const root_node_index: Node.Index = @enumFromInt(0);
    i, const nl_n = computeNode(buf, i, 0, serialized, children, root_node_index);

    if (global_progress.terminal_mode == .ansi_escape_codes) {
        {
            // Set progress state https://conemu.github.io/en/AnsiEscapeCodes.html#ConEmu_specific_OSC
            const root_storage = &serialized.storage[0];
            const storage = if (root_storage.name[0] != 0 or children[0].child == .none) root_storage else &serialized.storage[@intFromEnum(children[0].child)];
            const estimated_total = storage.estimated_total_count;
            const completed_items = storage.completed_count;
            const status = @atomicLoad(Status, &global_progress.status, .monotonic);
            switch (status) {
                .working => {
                    if (estimated_total == 0) {
                        buf[i..][0..progress_pulsing.len].* = progress_pulsing.*;
                        i += progress_pulsing.len;
                    } else {
                        const percent = completed_items * 100 / estimated_total;
                        if (std.fmt.bufPrint(buf[i..], @"progress_normal {d}", .{percent})) |b| {
                            i += b.len;
                        } else |_| {}
                    }
                },
                .success => {
                    buf[i..][0..progress_remove.len].* = progress_remove.*;
                    i += progress_remove.len;
                },
                .failure => {
                    buf[i..][0..progress_error_100.len].* = progress_error_100.*;
                    i += progress_error_100.len;
                },
                .failure_working => {
                    if (estimated_total == 0) {
                        buf[i..][0..progress_pulsing_error.len].* = progress_pulsing_error.*;
                        i += progress_pulsing_error.len;
                    } else {
                        const percent = completed_items * 100 / estimated_total;
                        if (std.fmt.bufPrint(buf[i..], @"progress_error {d}", .{percent})) |b| {
                            i += b.len;
                        } else |_| {}
                    }
                },
            }
        }

        if (nl_n > 0) {
            buf[i] = '\r';
            i += 1;
            for (0..nl_n) |_| {
                buf[i..][0..up_one_line.len].* = up_one_line.*;
                i += up_one_line.len;
            }
        }

        buf[i..][0..finish_sync.len].* = finish_sync.*;
        i += finish_sync.len;
    }

    return .{ buf[0..i], nl_n };
}

fn computePrefix(
    buf: []u8,
    start_i: usize,
    nl_n: usize,
    serialized: Serialized,
    children: []const Children,
    node_index: Node.Index,
) usize {
    var i = start_i;
    const parent_index = serialized.parents[@intFromEnum(node_index)].unwrap() orelse return i;
    if (serialized.parents[@intFromEnum(parent_index)] == .none) return i;
    if (@intFromEnum(serialized.parents[@intFromEnum(parent_index)]) == 0 and
        serialized.storage[0].name[0] == 0)
    {
        return i;
    }
    i = computePrefix(buf, i, nl_n, serialized, children, parent_index);
    if (children[@intFromEnum(parent_index)].sibling == .none) {
        const prefix = "   ";
        const upper_bound_len = prefix.len + lineUpperBoundLen(nl_n);
        if (i + upper_bound_len > buf.len) return buf.len;
        buf[i..][0..prefix.len].* = prefix.*;
        i += prefix.len;
    } else {
        const upper_bound_len = TreeSymbol.line.maxByteLen() + lineUpperBoundLen(nl_n);
        if (i + upper_bound_len > buf.len) return buf.len;
        i = appendTreeSymbol(.line, buf, i);
    }
    return i;
}

fn lineUpperBoundLen(nl_n: usize) usize {
    // \r\n on Windows, \n otherwise.
    const nl_len = if (is_windows) 2 else 1;
    return @max(TreeSymbol.tee.maxByteLen(), TreeSymbol.langle.maxByteLen()) +
        "[4294967296/4294967296] ".len + Node.max_name_len + nl_len +
        (1 + (nl_n + 1) * up_one_line.len) +
        finish_sync.len;
}

fn computeNode(
    buf: []u8,
    start_i: usize,
    start_nl_n: usize,
    serialized: Serialized,
    children: []const Children,
    node_index: Node.Index,
) struct { usize, usize } {
    var i = start_i;
    var nl_n = start_nl_n;

    i = computePrefix(buf, i, nl_n, serialized, children, node_index);

    if (i + lineUpperBoundLen(nl_n) > buf.len)
        return .{ start_i, start_nl_n };

    const storage = &serialized.storage[@intFromEnum(node_index)];
    const estimated_total = storage.estimated_total_count;
    const completed_items = storage.completed_count;
    const name = if (std.mem.findScalar(u8, &storage.name, 0)) |end| storage.name[0..end] else &storage.name;
    const parent = serialized.parents[@intFromEnum(node_index)];

    if (parent != .none) p: {
        if (@intFromEnum(parent) == 0 and serialized.storage[0].name[0] == 0) {
            break :p;
        }
        if (children[@intFromEnum(node_index)].sibling == .none) {
            i = appendTreeSymbol(.langle, buf, i);
        } else {
            i = appendTreeSymbol(.tee, buf, i);
        }
    }

    const is_empty_root = @intFromEnum(node_index) == 0 and serialized.storage[0].name[0] == 0;
    if (!is_empty_root) {
        if (name.len != 0 or estimated_total > 0) {
            if (estimated_total > 0) {
                if (std.fmt.bufPrint(buf[i..], "[{d}/{d}] ", .{ completed_items, estimated_total })) |b| {
                    i += b.len;
                } else |_| {}
            } else if (completed_items != 0) {
                if (std.fmt.bufPrint(buf[i..], "[{d}] ", .{completed_items})) |b| {
                    i += b.len;
                } else |_| {}
            }
            if (name.len != 0) {
                if (std.fmt.bufPrint(buf[i..], "{s}", .{name})) |b| {
                    i += b.len;
                } else |_| {}
            }
        }

        i = @min(global_progress.cols + start_i, i);
        if (is_windows) {
            // \r\n on Windows is necessary for the old console with the
            // ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN
            // console modes set to behave properly.
            buf[i] = '\r';
            i += 1;
        }
        buf[i] = '\n';
        i += 1;
        nl_n += 1;
    }

    if (global_progress.withinRowLimit(nl_n)) {
        if (children[@intFromEnum(node_index)].child.unwrap()) |child| {
            i, nl_n = computeNode(buf, i, nl_n, serialized, children, child);
        }
    }

    if (global_progress.withinRowLimit(nl_n)) {
        if (children[@intFromEnum(node_index)].sibling.unwrap()) |sibling| {
            i, nl_n = computeNode(buf, i, nl_n, serialized, children, sibling);
        }
    }

    return .{ i, nl_n };
}

fn withinRowLimit(p: *Progress, nl_n: usize) bool {
    // The +2 here is so that the PS1 is not scrolled off the top of the terminal.
    // one because we keep the cursor on the next line
    // one more to account for the PS1
    return nl_n + 2 < p.rows;
}

fn writeIpc(writer: *Io.Writer, serialized: Serialized) Io.Writer.Error!void {
    // Byteswap if necessary to ensure little endian over the pipe. This is
    // needed because the parent or child process might be running in qemu.
    if (is_big_endian) for (serialized.storage) |*s| s.byteSwap();

    assert(serialized.parents.len == serialized.storage.len);
    const serialized_len: u8 = @intCast(serialized.parents.len);
    const header = std.mem.asBytes(&serialized_len);
    const storage = std.mem.sliceAsBytes(serialized.storage);
    const parents = std.mem.sliceAsBytes(serialized.parents);

    var vec = [3][]const u8{ header, storage, parents };
    try writer.writeVecAll(&vec);
}

fn maybeUpdateSize(io: Io, resize_flag: bool) !void {
    if (!resize_flag) return;

    const file = global_progress.terminal;

    if (is_windows) {
        var get_console_info = windows.CONSOLE.USER_IO.GET_SCREEN_BUFFER_INFO;
        switch (try get_console_info.operate(io, file)) {
            .SUCCESS => {
                global_progress.rows = @intCast(get_console_info.Data.dwWindowSize.Y);
                global_progress.cols = @intCast(get_console_info.Data.dwWindowSize.X);
            },
            else => {
                std.log.debug("failed to determine terminal size; using conservative guess 80x25", .{});
                global_progress.rows = 25;
                global_progress.cols = 80;
            },
        }
    } else {
        var winsize: posix.winsize = .{
            .row = 0,
            .col = 0,
            .xpixel = 0,
            .ypixel = 0,
        };

        const err = (try io.operate(.{ .device_io_control = .{
            .file = file,
            .code = posix.T.IOCGWINSZ,
            .arg = &winsize,
        } })).device_io_control;

        if (err >= 0) {
            global_progress.rows = winsize.row;
            global_progress.cols = winsize.col;
        } else {
            std.log.debug("failed to determine terminal size; using conservative guess 80x25", .{});
            global_progress.rows = 25;
            global_progress.cols = 80;
        }
    }
}

fn handleSigWinch(sig: posix.SIG, info: *const posix.siginfo_t, ctx_ptr: ?*anyopaque) callconv(.c) void {
    _ = info;
    _ = ctx_ptr;
    assert(sig == .WINCH);
    global_progress.redraw_event.set(global_progress.io);
}

const have_sigwinch = switch (builtin.os.tag) {
    .linux,
    .plan9,
    .illumos,
    .netbsd,
    .openbsd,
    .haiku,
    .driverkit,
    .ios,
    .maccatalyst,
    .macos,
    .tvos,
    .visionos,
    .watchos,
    .dragonfly,
    .freebsd,
    .serenity,
    => true,

    else => false,
};

fn copyAtomicStore(dest: []align(@alignOf(usize)) u8, src: []const u8) void {
    assert(dest.len == src.len);
    const chunked_len = dest.len / @sizeOf(usize);
    const dest_chunked: []usize = @as([*]usize, @ptrCast(dest))[0..chunked_len];
    const src_chunked: []align(1) const usize = @as([*]align(1) const usize, @ptrCast(src))[0..chunked_len];
    for (dest_chunked, src_chunked) |*d, s| {
        @atomicStore(usize, d, s, .monotonic);
    }
    const remainder_start = chunked_len * @sizeOf(usize);
    for (dest[remainder_start..], src[remainder_start..]) |*d, s| {
        @atomicStore(u8, d, s, .monotonic);
    }
}

fn copyAtomicLoad(
    dest: *align(@alignOf(usize)) [Node.max_name_len]u8,
    src: *align(@alignOf(usize)) const [Node.max_name_len]u8,
) void {
    const chunked_len = @divExact(dest.len, @sizeOf(usize));
    const dest_chunked: *[chunked_len]usize = @ptrCast(dest);
    const src_chunked: *const [chunked_len]usize = @ptrCast(src);
    for (dest_chunked, src_chunked) |*d, *s| {
        d.* = @atomicLoad(usize, s, .monotonic);
    }
}



---
File: /std/Random.zig
---

//! The engines provided here should be initialized from an external source.
//! Be sure to use a CSPRNG when required, otherwise using a normal PRNG will
//! be faster and use substantially less stack space.
const Random = @This();

const std = @import("std.zig");
const math = std.math;
const mem = std.mem;
const assert = std.debug.assert;
const maxInt = std.math.maxInt;

/// Fast unbiased random numbers.
pub const DefaultPrng = Xoshiro256;

/// Cryptographically secure random numbers.
pub const DefaultCsprng = ChaCha;

pub const Ascon = @import("Random/Ascon.zig");
pub const ChaCha = @import("Random/ChaCha.zig");

pub const Isaac64 = @import("Random/Isaac64.zig");
pub const Pcg = @import("Random/Pcg.zig");
pub const Xoroshiro128 = @import("Random/Xoroshiro128.zig");
pub const Xoshiro256 = @import("Random/Xoshiro256.zig");
pub const Sfc64 = @import("Random/Sfc64.zig");
pub const RomuTrio = @import("Random/RomuTrio.zig");
pub const SplitMix64 = @import("Random/SplitMix64.zig");
pub const ziggurat = @import("Random/ziggurat.zig");
pub const lcg = @import("Random/lcg.zig");

/// Any comparison of this field may result in illegal behavior, since it may be set to
/// `undefined` in cases where the random implementation does not have any associated
/// state.
ptr: *anyopaque,
fillFn: *const fn (ptr: *anyopaque, buf: []u8) void,

pub const IoSource = struct {
    io: std.Io,

    pub fn interface(this: *const @This()) std.Random {
        return .{
            .ptr = @constCast(this),
            .fillFn = fill,
        };
    }

    fn fill(ptr: *anyopaque, buffer: []u8) void {
        const this: *const @This() = @ptrCast(@alignCast(ptr));
        this.io.random(buffer);
    }
};

pub fn init(pointer: anytype, comptime fillFn: fn (ptr: @TypeOf(pointer), buf: []u8) void) Random {
    const Ptr = @TypeOf(pointer);
    assert(@typeInfo(Ptr) == .pointer); // Must be a pointer
    assert(@typeInfo(Ptr).pointer.size == .one); // Must be a single-item pointer
    assert(@typeInfo(@typeInfo(Ptr).pointer.child) == .@"struct"); // Must point to a struct
    const gen = struct {
        fn fill(ptr: *anyopaque, buf: []u8) void {
            const self: Ptr = @ptrCast(@alignCast(ptr));
            fillFn(self, buf);
        }
    };

    return .{
        .ptr = pointer,
        .fillFn = gen.fill,
    };
}

/// Read random bytes into the specified buffer until full.
pub fn bytes(r: Random, buf: []u8) void {
    r.fillFn(r.ptr, buf);
}

pub fn array(r: Random, comptime E: type, comptime N: usize) [N]E {
    var result: [N]E = undefined;
    bytes(r, &result);
    return result;
}

pub fn boolean(r: Random) bool {
    return r.int(u1) != 0;
}

/// Returns a random value from an enum, evenly distributed.
///
/// Note that this will not yield consistent results across all targets
/// due to dependence on the representation of `usize` as an index.
/// See `enumValueWithIndex` for further commentary.
pub inline fn enumValue(r: Random, comptime EnumType: type) EnumType {
    return r.enumValueWithIndex(EnumType, usize);
}

/// Returns a random value from an enum, evenly distributed.
///
/// An index into an array of all named values is generated using the
/// specified `Index` type to determine the return value.
/// This allows for results to be independent of `usize` representation.
///
/// Prefer `enumValue` if this isn't important.
///
/// See `uintLessThan`, which this function uses in most cases,
/// for commentary on the runtime of this function.
pub fn enumValueWithIndex(r: Random, comptime EnumType: type, comptime Index: type) EnumType {
    comptime assert(@typeInfo(EnumType) == .@"enum");

    // We won't use int -> enum casting because enum elements can have
    //  arbitrary values.  Instead we'll randomly pick one of the type's values.
    const values = comptime std.enums.values(EnumType);
    comptime assert(values.len > 0); // can't return anything
    comptime assert(maxInt(Index) >= values.len - 1); // can't access all values
    if (values.len == 1) return values[0];

    const index = if (comptime values.len - 1 == maxInt(Index))
        r.int(Index)
    else
        r.uintLessThan(Index, values.len);

    const MinInt = MinArrayIndex(Index);
    return values[@as(MinInt, @intCast(index))];
}

/// Returns a random int `i` such that `minInt(T) <= i <= maxInt(T)`.
/// `i` is evenly distributed.
pub fn int(r: Random, comptime T: type) T {
    const bits = @typeInfo(T).int.bits;
    const UnsignedT = std.meta.Int(.unsigned, bits);
    const ceil_bytes = comptime std.math.divCeil(u16, bits, 8) catch unreachable;
    const ByteAlignedT = std.meta.Int(.unsigned, ceil_bytes * 8);

    var rand_bytes: [ceil_bytes]u8 = undefined;
    r.bytes(&rand_bytes);

    // use LE instead of native endian for better portability maybe?
    // TODO: endian portability is pointless if the underlying prng isn't endian portable.
    // TODO: document the endian portability of this library.
    const byte_aligned_result = mem.readInt(ByteAlignedT, &rand_bytes, .little);
    const unsigned_result: UnsignedT = @truncate(byte_aligned_result);
    return @bitCast(unsigned_result);
}

/// Constant-time implementation off `uintLessThan`.
/// The results of this function may be biased.
pub fn uintLessThanBiased(r: Random, comptime T: type, less_than: T) T {
    comptime assert(@typeInfo(T).int.signedness == .unsigned);
    assert(0 < less_than);
    return limitRangeBiased(T, r.int(T), less_than);
}

/// Returns an evenly distributed random unsigned integer `0 <= i < less_than`.
/// This function assumes that the underlying `fillFn` produces evenly distributed values.
/// Within this assumption, the runtime of this function is exponentially distributed.
/// If `fillFn` were backed by a true random generator,
/// the runtime of this function would technically be unbounded.
/// However, if `fillFn` is backed by any evenly distributed pseudo random number generator,
/// this function is guaranteed to return.
/// If you need deterministic runtime bounds, use `uintLessThanBiased`.
pub fn uintLessThan(r: Random, comptime T: type, less_than: T) T {
    comptime assert(@typeInfo(T).int.signedness == .unsigned);
    const bits = @typeInfo(T).int.bits;
    assert(0 < less_than);

    // adapted from:
    //   http://www.pcg-random.org/posts/bounded-rands.html
    //   "Lemire's (with an extra tweak from me)"
    var x = r.int(T);
    var m = math.mulWide(T, x, less_than);
    var l: T = @truncate(m);
    if (l < less_than) {
        var t = -%less_than;

        if (t >= less_than) {
            t -= less_than;
            if (t >= less_than) {
                t %= less_than;
            }
        }
        while (l < t) {
            x = r.int(T);
            m = math.mulWide(T, x, less_than);
            l = @truncate(m);
        }
    }
    return @intCast(m >> bits);
}

/// Constant-time implementation off `uintAtMost`.
/// The results of this function may be biased.
pub fn uintAtMostBiased(r: Random, comptime T: type, at_most: T) T {
    assert(@typeInfo(T).int.signedness == .unsigned);
    if (at_most == maxInt(T)) {
        // have the full range
        return r.int(T);
    }
    return r.uintLessThanBiased(T, at_most + 1);
}

/// Returns an evenly distributed random unsigned integer `0 <= i <= at_most`.
/// See `uintLessThan`, which this function uses in most cases,
/// for commentary on the runtime of this function.
pub fn uintAtMost(r: Random, comptime T: type, at_most: T) T {
    assert(@typeInfo(T).int.signedness == .unsigned);
    if (at_most == maxInt(T)) {
        // have the full range
        return r.int(T);
    }
    return r.uintLessThan(T, at_most + 1);
}

/// Constant-time implementation off `intRangeLessThan`.
/// The results of this function may be biased.
pub fn intRangeLessThanBiased(r: Random, comptime T: type, at_least: T, less_than: T) T {
    assert(at_least < less_than);
    const info = @typeInfo(T).int;
    if (info.signedness == .signed) {
        // Two's complement makes this math pretty easy.
        const UnsignedT = std.meta.Int(.unsigned, info.bits);
        const lo: UnsignedT = @bitCast(at_least);
        const hi: UnsignedT = @bitCast(less_than);
        const result = lo +% r.uintLessThanBiased(UnsignedT, hi -% lo);
        return @bitCast(result);
    } else {
        // The signed implementation would work fine, but we can use stricter arithmetic operators here.
        return at_least + r.uintLessThanBiased(T, less_than - at_least);
    }
}

/// Returns an evenly distributed random integer `at_least <= i < less_than`.
/// See `uintLessThan`, which this function uses in most cases,
/// for commentary on the runtime of this function.
pub fn intRangeLessThan(r: Random, comptime T: type, at_least: T, less_than: T) T {
    assert(at_least < less_than);
    const info = @typeInfo(T).int;
    if (info.signedness == .signed) {
        // Two's complement makes this math pretty easy.
        const UnsignedT = std.meta.Int(.unsigned, info.bits);
        const lo: UnsignedT = @bitCast(at_least);
        const hi: UnsignedT = @bitCast(less_than);
        const result = lo +% r.uintLessThan(UnsignedT, hi -% lo);
        return @bitCast(result);
    } else {
        // The signed implementation would work fine, but we can use stricter arithmetic operators here.
        return at_least + r.uintLessThan(T, less_than - at_least);
    }
}

/// Constant-time implementation off `intRangeAtMostBiased`.
/// The results of this function may be biased.
pub fn intRangeAtMostBiased(r: Random, comptime T: type, at_least: T, at_most: T) T {
    assert(at_least <= at_most);
    const info = @typeInfo(T).int;
    if (info.signedness == .signed) {
        // Two's complement makes this math pretty easy.
        const UnsignedT = std.meta.Int(.unsigned, info.bits);
        const lo: UnsignedT = @bitCast(at_least);
        const hi: UnsignedT = @bitCast(at_most);
        const result = lo +% r.uintAtMostBiased(UnsignedT, hi -% lo);
        return @bitCast(result);
    } else {
        // The signed implementation would work fine, but we can use stricter arithmetic operators here.
        return at_least + r.uintAtMostBiased(T, at_most - at_least);
    }
}

/// Returns an evenly distributed random integer `at_least <= i <= at_most`.
/// See `uintLessThan`, which this function uses in most cases,
/// for commentary on the runtime of this function.
pub fn intRangeAtMost(r: Random, comptime T: type, at_least: T, at_most: T) T {
    assert(at_least <= at_most);
    const info = @typeInfo(T).int;
    if (info.signedness == .signed) {
        // Two's complement makes this math pretty easy.
        const UnsignedT = std.meta.Int(.unsigned, info.bits);
        const lo: UnsignedT = @bitCast(at_least);
        const hi: UnsignedT = @bitCast(at_most);
        const result = lo +% r.uintAtMost(UnsignedT, hi -% lo);
        return @bitCast(result);
    } else {
        // The signed implementation would work fine, but we can use stricter arithmetic operators here.
        return at_least + r.uintAtMost(T, at_most - at_least);
    }
}

/// Return a floating point value evenly distributed in the range [0, 1).
pub fn float(r: Random, comptime T: type) T {
    // Generate a uniformly random value for the mantissa.
    // Then generate an exponentially biased random value for the exponent.
    // This covers every possible value in the range.
    switch (T) {
        f32 => {
            // Use 23 random bits for the mantissa, and the rest for the exponent.
            // If all 41 bits are zero, generate additional random bits, until a
            // set bit is found, or 126 bits have been generated.
            const rand = r.int(u64);
            var rand_lz = @clz(rand);
            if (rand_lz >= 41) {
                @branchHint(.unlikely);
                rand_lz = 41 + @clz(r.int(u64));
                if (rand_lz == 41 + 64) {
                    @branchHint(.unlikely);
                    // It is astronomically unlikely to reach this point.
                    rand_lz += @clz(r.int(u32) | 0x7FF);
                }
            }
            const mantissa: u23 = @truncate(rand);
            const exponent = @as(u32, 126 - rand_lz) << 23;
            return @bitCast(exponent | mantissa);
        },
        f64 => {
            // Use 52 random bits for the mantissa, and the rest for the exponent.
            // If all 12 bits are zero, generate additional random bits, until a
            // set bit is found, or 1022 bits have been generated.
            const rand = r.int(u64);
            var rand_lz: u64 = @clz(rand);
            if (rand_lz >= 12) {
                rand_lz = 12;
                while (true) {
                    // It is astronomically unlikely for this loop to execute more than once.
                    const addl_rand_lz = @clz(r.int(u64));
                    rand_lz += addl_rand_lz;
                    if (addl_rand_lz != 64) {
                        @branchHint(.likely);
                        break;
                    }
                    if (rand_lz >= 1022) {
                        rand_lz = 1022;
                        break;
                    }
                }
            }
            const mantissa = rand & 0xFFFFFFFFFFFFF;
            const exponent = (1022 - rand_lz) << 52;
            return @bitCast(exponent | mantissa);
        },
        else => @compileError("unknown floating point type"),
    }
}

/// Return a floating point value normally distributed with mean = 0, stddev = 1.
///
/// To use different parameters, use: floatNorm(...) * desiredStddev + desiredMean.
pub fn floatNorm(r: Random, comptime T: type) T {
    const value = ziggurat.next_f64(r, ziggurat.NormDist);
    switch (T) {
        f32 => return @floatCast(value),
        f64 => return value,
        else => @compileError("unknown floating point type"),
    }
}

/// Return an exponentially distributed float with a rate parameter of 1.
///
/// To use a different rate parameter, use: floatExp(...) / desiredRate.
pub fn floatExp(r: Random, comptime T: type) T {
    const value = ziggurat.next_f64(r, ziggurat.ExpDist);
    switch (T) {
        f32 => return @floatCast(value),
        f64 => return value,
        else => @compileError("unknown floating point type"),
    }
}

/// Shuffle a slice into a random order.
///
/// Note that this will not yield consistent results across all targets
/// due to dependence on the representation of `usize` as an index.
/// See `shuffleWithIndex` for further commentary.
pub inline fn shuffle(r: Random, comptime T: type, buf: []T) void {
    r.shuffleWithIndex(T, buf, usize);
}

/// Shuffle a slice into a random order, using an index of a
/// specified type to maintain distribution across targets.
/// Asserts the index type can represent `buf.len`.
///
/// Indexes into the slice are generated using the specified `Index`
/// type, which determines distribution properties. This allows for
/// results to be independent of `usize` representation.
///
/// Prefer `shuffle` if this isn't important.
///
/// See `intRangeLessThan`, which this function uses,
/// for commentary on the runtime of this function.
pub fn shuffleWithIndex(r: Random, comptime T: type, buf: []T, comptime Index: type) void {
    const MinInt = MinArrayIndex(Index);
    if (buf.len < 2) {
        return;
    }

    // `i <= j < max <= maxInt(MinInt)`
    const max: MinInt = @intCast(buf.len);
    var i: MinInt = 0;
    while (i < max - 1) : (i += 1) {
        const j: MinInt = @intCast(r.intRangeLessThan(Index, i, max));
        mem.swap(T, &buf[i], &buf[j]);
    }
}

/// Randomly selects an index into `proportions`, where the likelihood of each
/// index is weighted by that proportion.
/// It is more likely for the index of the last proportion to be returned
/// than the index of the first proportion in the slice, and vice versa.
///
/// This is useful for selecting an item from a slice where weights are not equal.
/// `T` must be a numeric type capable of holding the sum of `proportions`.
pub fn weightedIndex(r: Random, comptime T: type, proportions: []const T) usize {
    // This implementation works by summing the proportions and picking a
    // random point in [0, sum).  We then loop over the proportions,
    // accumulating until our accumulator is greater than the random point.

    const sum = s: {
        var sum: T = 0;
        for (proportions) |v| sum += v;
        break :s sum;
    };

    const point = switch (@typeInfo(T)) {
        .int => |int_info| switch (int_info.signedness) {
            .signed => r.intRangeLessThan(T, 0, sum),
            .unsigned => r.uintLessThan(T, sum),
        },
        // take care that imprecision doesn't lead to a value slightly greater than sum
        .float => @min(r.float(T) * sum, sum - std.math.floatEps(T)),
        else => @compileError("weightedIndex does not support proportions of type " ++
            @typeName(T)),
    };

    assert(point < sum);

    var accumulator: T = 0;
    for (proportions, 0..) |p, index| {
        accumulator += p;
        if (point < accumulator) return index;
    } else unreachable;
}

/// Convert a random integer 0 <= random_int <= maxValue(T),
/// into an integer 0 <= result < less_than.
/// This function introduces a minor bias.
pub fn limitRangeBiased(comptime T: type, random_int: T, less_than: T) T {
    comptime assert(@typeInfo(T).int.signedness == .unsigned);
    const bits = @typeInfo(T).int.bits;

    // adapted from:
    //   http://www.pcg-random.org/posts/bounded-rands.html
    //   "Integer Multiplication (Biased)"
    const m = math.mulWide(T, random_int, less_than);
    return @intCast(m >> bits);
}

/// Returns the smallest of `Index` and `usize`.
fn MinArrayIndex(comptime Index: type) type {
    const index_info = @typeInfo(Index).int;
    assert(index_info.signedness == .unsigned);
    return if (index_info.bits >= @typeInfo(usize).int.bits) usize else Index;
}

test {
    std.testing.refAllDecls(@This());
    _ = @import("Random/test.zig");
}



---
File: /std/SemanticVersion.zig
---

//! A software version formatted according to the Semantic Versioning 2.0.0 specification.
//!
//! See: https://semver.org

const std = @import("std");
const Version = @This();

major: usize,
minor: usize,
patch: usize,
pre: ?[]const u8 = null,
build: ?[]const u8 = null,

pub const Range = struct {
    min: Version,
    max: Version,

    pub fn includesVersion(self: Range, ver: Version) bool {
        if (self.min.order(ver) == .gt) return false;
        if (self.max.order(ver) == .lt) return false;
        return true;
    }

    /// Checks if system is guaranteed to be at least `version` or older than `version`.
    /// Returns `null` if a runtime check is required.
    pub fn isAtLeast(self: Range, ver: Version) ?bool {
        if (self.min.order(ver) != .lt) return true;
        if (self.max.order(ver) == .lt) return false;
        return null;
    }
};

pub fn order(lhs: Version, rhs: Version) std.math.Order {
    if (lhs.major < rhs.major) return .lt;
    if (lhs.major > rhs.major) return .gt;
    if (lhs.minor < rhs.minor) return .lt;
    if (lhs.minor > rhs.minor) return .gt;
    if (lhs.patch < rhs.patch) return .lt;
    if (lhs.patch > rhs.patch) return .gt;
    if (lhs.pre != null and rhs.pre == null) return .lt;
    if (lhs.pre == null and rhs.pre == null) return .eq;
    if (lhs.pre == null and rhs.pre != null) return .gt;

    // Iterate over pre-release identifiers until a difference is found.
    var lhs_pre_it = std.mem.splitScalar(u8, lhs.pre.?, '.');
    var rhs_pre_it = std.mem.splitScalar(u8, rhs.pre.?, '.');
    while (true) {
        const next_lid = lhs_pre_it.next();
        const next_rid = rhs_pre_it.next();

        // A larger set of pre-release fields has a higher precedence than a smaller set.
        if (next_lid == null and next_rid != null) return .lt;
        if (next_lid == null and next_rid == null) return .eq;
        if (next_lid != null and next_rid == null) return .gt;

        const lid = next_lid.?; // Left identifier
        const rid = next_rid.?; // Right identifier

        // Attempt to parse identifiers as numbers. Overflows are checked by parse.
        const lnum: ?usize = std.fmt.parseUnsigned(usize, lid, 10) catch |err| switch (err) {
            error.InvalidCharacter => null,
            error.Overflow => unreachable,
        };
        const rnum: ?usize = std.fmt.parseUnsigned(usize, rid, 10) catch |err| switch (err) {
            error.InvalidCharacter => null,
            error.Overflow => unreachable,
        };

        // Numeric identifiers always have lower precedence than non-numeric identifiers.
        if (lnum != null and rnum == null) return .lt;
        if (lnum == null and rnum != null) return .gt;

        // Identifiers consisting of only digits are compared numerically.
        // Identifiers with letters or hyphens are compared lexically in ASCII sort order.
        if (lnum != null and rnum != null) {
            if (lnum.? < rnum.?) return .lt;
            if (lnum.? > rnum.?) return .gt;
        } else {
            const ord = std.mem.order(u8, lid, rid);
            if (ord != .eq) return ord;
        }
    }
}

pub fn parse(text: []const u8) !Version {
    // Parse the required major, minor, and patch numbers.
    const extra_index = std.mem.findAny(u8, text, "-+");
    const required = text[0..(extra_index orelse text.len)];
    var it = std.mem.splitScalar(u8, required, '.');
    var ver = Version{
        .major = try parseNum(it.first()),
        .minor = try parseNum(it.next() orelse return error.InvalidVersion),
        .patch = try parseNum(it.next() orelse return error.InvalidVersion),
    };
    if (it.next() != null) return error.InvalidVersion;
    if (extra_index == null) return ver;

    // Slice optional pre-release or build metadata components.
    const extra: []const u8 = text[extra_index.?..text.len];
    if (extra[0] == '-') {
        const build_index = std.mem.findScalar(u8, extra, '+');
        ver.pre = extra[1..(build_index orelse extra.len)];
        if (build_index) |idx| ver.build = extra[(idx + 1)..];
    } else {
        ver.build = extra[1..];
    }

    // Check validity of optional pre-release identifiers.
    // See: https://semver.org/#spec-item-9
    if (ver.pre) |pre| {
        it = std.mem.splitScalar(u8, pre, '.');
        while (it.next()) |id| {
            // Identifiers MUST NOT be empty.
            if (id.len == 0) return error.InvalidVersion;

            // Identifiers MUST comprise only ASCII alphanumerics and hyphens [0-9A-Za-z-].
            for (id) |c| if (!std.ascii.isAlphanumeric(c) and c != '-') return error.InvalidVersion;

            // Numeric identifiers MUST NOT include leading zeroes.
            const is_num = for (id) |c| {
                if (!std.ascii.isDigit(c)) break false;
            } else true;
            if (is_num) _ = try parseNum(id);
        }
    }

    // Check validity of optional build metadata identifiers.
    // See: https://semver.org/#spec-item-10
    if (ver.build) |build| {
        it = std.mem.splitScalar(u8, build, '.');
        while (it.next()) |id| {
            // Identifiers MUST NOT be empty.
            if (id.len == 0) return error.InvalidVersion;

            // Identifiers MUST comprise only ASCII alphanumerics and hyphens [0-9A-Za-z-].
            for (id) |c| if (!std.ascii.isAlphanumeric(c) and c != '-') return error.InvalidVersion;
        }
    }

    return ver;
}

fn parseNum(text: []const u8) error{ InvalidVersion, Overflow }!usize {
    // Leading zeroes are not allowed.
    if (text.len > 1 and text[0] == '0') return error.InvalidVersion;

    return std.fmt.parseUnsigned(usize, text, 10) catch |err| switch (err) {
        error.InvalidCharacter => return error.InvalidVersion,
        error.Overflow => return error.Overflow,
    };
}

pub fn format(self: Version, w: *std.Io.Writer) std.Io.Writer.Error!void {
    try w.print("{d}.{d}.{d}", .{ self.major, self.minor, self.patch });
    if (self.pre) |pre| try w.print("-{s}", .{pre});
    if (self.build) |build| try w.print("+{s}", .{build});
}

const expect = std.testing.expect;
const expectError = std.testing.expectError;

test format {
    // Many of these test strings are from https://github.com/semver/semver.org/issues/59#issuecomment-390854010.

    // Valid version strings should be accepted.
    for ([_][]const u8{
        "0.0.4",
        "1.2.3",
        "10.20.30",
        "1.1.2-prerelease+meta",
        "1.1.2+meta",
        "1.1.2+meta-valid",
        "1.0.0-alpha",
        "1.0.0-beta",
        "1.0.0-alpha.beta",
        "1.0.0-alpha.beta.1",
        "1.0.0-alpha.1",
        "1.0.0-alpha0.valid",
        "1.0.0-alpha.0valid",
        "1.0.0-alpha-a.b-c-somethinglong+build.1-aef.1-its-okay",
        "1.0.0-rc.1+build.1",
        "2.0.0-rc.1+build.123",
        "1.2.3-beta",
        "10.2.3-DEV-SNAPSHOT",
        "1.2.3-SNAPSHOT-123",
        "1.0.0",
        "2.0.0",
        "1.1.7",
        "2.0.0+build.1848",
        "2.0.1-alpha.1227",
        "1.0.0-alpha+beta",
        "1.2.3----RC-SNAPSHOT.12.9.1--.12+788",
        "1.2.3----R-S.12.9.1--.12+meta",
        "1.2.3----RC-SNAPSHOT.12.9.1--.12",
        "1.0.0+0.build.1-rc.10000aaa-kk-0.1",
        "5.4.0-1018-raspi",
        "5.7.123",
    }) |valid| try std.testing.expectFmt(valid, "{f}", .{try parse(valid)});

    // Invalid version strings should be rejected.
    for ([_][]const u8{
        "",
        "1",
        "1.2",
        "1.2.3-0123",
        "1.2.3-0123.0123",
        "1.1.2+.123",
        "+invalid",
        "-invalid",
        "-invalid+invalid",
        "-invalid.01",
        "alpha",
        "alpha.beta",
        "alpha.beta.1",
        "alpha.1",
        "alpha+beta",
        "alpha_beta",
        "alpha.",
        "alpha..",
        "beta\\",
        "1.0.0-alpha_beta",
        "-alpha.",
        "1.0.0-alpha..",
        "1.0.0-alpha..1",
        "1.0.0-alpha...1",
        "1.0.0-alpha....1",
        "1.0.0-alpha.....1",
        "1.0.0-alpha......1",
        "1.0.0-alpha.......1",
        "01.1.1",
        "1.01.1",
        "1.1.01",
        "1.2",
        "1.2.3.DEV",
        "1.2-SNAPSHOT",
        "1.2.31.2.3----RC-SNAPSHOT.12.09.1--..12+788",
        "1.2-RC-SNAPSHOT",
        "-1.0.3-gamma+b7718",
        "+justmeta",
        "9.8.7+meta+meta",
        "9.8.7-whatever+meta+meta",
        "2.6.32.11-svn21605",
        "2.11.2(0.329/5/3)",
        "2.13-DEVELOPMENT",
        "2.3-35",
        "1a.4",
        "3.b1.0",
        "1.4beta",
        "2.7.pre",
        "0..3",
        "8.008.",
        "01...",
        "55",
        "foobar",
        "",
        "-1",
        "+4",
        ".",
        "....3",
    }) |invalid| try expectError(error.InvalidVersion, parse(invalid));

    // Valid version string that may overflow.
    const big_valid = "99999999999999999999999.999999999999999999.99999999999999999";
    if (parse(big_valid)) |ver| {
        try std.testing.expectFmt(big_valid, "{f}", .{ver});
    } else |err| try expect(err == error.Overflow);

    // Invalid version string that may overflow.
    const big_invalid = "99999999999999999999999.999999999999999999.99999999999999999----RC-SNAPSHOT.12.09.1--------------------------------..12";
    if (parse(big_invalid)) |ver| std.debug.panic("expected error, found {f}", .{ver}) else |_| {}
}

test "precedence" {
    // SemVer 2 spec 11.2 example: 1.0.0 < 2.0.0 < 2.1.0 < 2.1.1.
    try expect(order(try parse("1.0.0"), try parse("2.0.0")) == .lt);
    try expect(order(try parse("2.0.0"), try parse("2.1.0")) == .lt);
    try expect(order(try parse("2.1.0"), try parse("2.1.1")) == .lt);

    // SemVer 2 spec 11.3 example: 1.0.0-alpha < 1.0.0.
    try expect(order(try parse("1.0.0-alpha"), try parse("1.0.0")) == .lt);

    // SemVer 2 spec 11.4 example: 1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta <
    // 1.0.0-beta.2 < 1.0.0-beta.11 < 1.0.0-rc.1 < 1.0.0.
    try expect(order(try parse("1.0.0-alpha"), try parse("1.0.0-alpha.1")) == .lt);
    try expect(order(try parse("1.0.0-alpha.1"), try parse("1.0.0-alpha.beta")) == .lt);
    try expect(order(try parse("1.0.0-alpha.beta"), try parse("1.0.0-beta")) == .lt);
    try expect(order(try parse("1.0.0-beta"), try parse("1.0.0-beta.2")) == .lt);
    try expect(order(try parse("1.0.0-beta.2"), try parse("1.0.0-beta.11")) == .lt);
    try expect(order(try parse("1.0.0-beta.11"), try parse("1.0.0-rc.1")) == .lt);
    try expect(order(try parse("1.0.0-rc.1"), try parse("1.0.0")) == .lt);
}

test "zig_version" {
    // An approximate Zig build that predates this test.
    const older_version: Version = .{ .major = 0, .minor = 8, .patch = 0, .pre = "dev.874" };

    // Simulated compatibility check using Zig version.
    const compatible = comptime @import("builtin").zig_version.order(older_version) == .gt;
    if (!compatible) @compileError("zig_version test failed");
}



---
File: /std/simd.zig
---

//! SIMD (Single Instruction; Multiple Data) convenience functions.
//!
//! May offer a potential boost in performance on some targets by performing
//! the same operation on multiple elements at once.
//!
//! Some functions
```
