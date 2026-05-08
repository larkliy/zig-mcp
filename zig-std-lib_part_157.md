```
race(request: u32, pid: pid_t, addr: usize, data: usize) PtraceError!void {
    return switch (native_os) {
        .windows,
        .wasi,
        .emscripten,
        .haiku,
        .illumos,
        .plan9,
        => @compileError("ptrace unsupported by target OS"),

        .linux => switch (errno(if (builtin.link_libc) std.c.ptrace(
            @intCast(request),
            pid,
            @ptrFromInt(addr),
            @ptrFromInt(data),
        ) else linux.ptrace(request, pid, addr, data, 0))) {
            .SUCCESS => {},
            .SRCH => error.ProcessNotFound,
            .FAULT => unreachable,
            .INVAL => unreachable,
            .IO => return error.InputOutput,
            .PERM => error.PermissionDenied,
            .BUSY => error.DeviceBusy,
            else => |err| return unexpectedErrno(err),
        },

        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => switch (errno(std.c.ptrace(
            @enumFromInt(request),
            pid,
            @ptrFromInt(addr),
            @intCast(data),
        ))) {
            .SUCCESS => {},
            .SRCH => error.ProcessNotFound,
            .INVAL => unreachable,
            .PERM => error.PermissionDenied,
            .BUSY => error.DeviceBusy,
            else => |err| return unexpectedErrno(err),
        },

        .dragonfly => switch (errno(std.c.ptrace(
            @intCast(request),
            pid,
            @ptrFromInt(addr),
            @intCast(data),
        ))) {
            .SUCCESS => {},
            .SRCH => error.ProcessNotFound,
            .INVAL => unreachable,
            .PERM => error.PermissionDenied,
            .BUSY => error.DeviceBusy,
            else => |err| return unexpectedErrno(err),
        },

        .freebsd => switch (errno(std.c.ptrace(
            @intCast(request),
            pid,
            @ptrFromInt(addr),
            @intCast(data),
        ))) {
            .SUCCESS => {},
            .SRCH => error.ProcessNotFound,
            .INVAL => unreachable,
            .PERM => error.PermissionDenied,
            .BUSY => error.DeviceBusy,
            .NOENT, .NOMEM => error.OutOfMemory,
            .NAMETOOLONG => error.NameTooLong,
            else => |err| return unexpectedErrno(err),
        },

        .netbsd => switch (errno(std.c.ptrace(
            @intCast(request),
            pid,
            @ptrFromInt(addr),
            @intCast(data),
        ))) {
            .SUCCESS => {},
            .SRCH => error.ProcessNotFound,
            .INVAL => unreachable,
            .PERM => error.PermissionDenied,
            .BUSY => error.DeviceBusy,
            .DEADLK => error.DeadLock,
            else => |err| return unexpectedErrno(err),
        },

        .openbsd => switch (errno(std.c.ptrace(
            @intCast(request),
            pid,
            @ptrFromInt(addr),
            @intCast(data),
        ))) {
            .SUCCESS => {},
            .SRCH => error.ProcessNotFound,
            .INVAL => unreachable,
            .PERM => error.PermissionDenied,
            .BUSY => error.DeviceBusy,
            .NOTSUP => error.OperationUnsupported,
            else => |err| return unexpectedErrno(err),
        },

        else => @compileError("std.posix.ptrace unimplemented for target OS"),
    };
}

pub const NameToFileHandleAtError = error{
    FileNotFound,
    NotDir,
    OperationUnsupported,
    NameTooLong,
    Unexpected,
};

pub fn name_to_handle_at(
    dirfd: fd_t,
    pathname: []const u8,
    handle: *std.os.linux.file_handle,
    mount_id: *i32,
    flags: u32,
) NameToFileHandleAtError!void {
    const pathname_c = try toPosixPath(pathname);
    return name_to_handle_atZ(dirfd, &pathname_c, handle, mount_id, flags);
}

pub fn name_to_handle_atZ(
    dirfd: fd_t,
    pathname_z: [*:0]const u8,
    handle: *std.os.linux.file_handle,
    mount_id: *i32,
    flags: u32,
) NameToFileHandleAtError!void {
    switch (errno(system.name_to_handle_at(dirfd, pathname_z, handle, mount_id, flags))) {
        .SUCCESS => {},
        .FAULT => unreachable, // pathname, mount_id, or handle outside accessible address space
        .INVAL => unreachable, // bad flags, or handle_bytes too big
        .NOENT => return error.FileNotFound,
        .NOTDIR => return error.NotDir,
        .OPNOTSUPP => return error.OperationUnsupported,
        .OVERFLOW => return error.NameTooLong,
        else => |err| return unexpectedErrno(err),
    }
}

pub const lfs64_abi = native_os == .linux and builtin.link_libc and (builtin.abi.isGnu() or builtin.abi.isAndroid());

pub const UnexpectedError = std.Io.UnexpectedError;

/// Call this when you made a syscall or something that sets errno
/// and you get an unexpected error.
pub fn unexpectedErrno(err: E) UnexpectedError {
    if (std.options.unexpected_error_tracing) {
        std.debug.print("unexpected errno: {d}\n", .{@intFromEnum(err)});
        std.debug.dumpCurrentStackTrace(.{});
    }
    return error.Unexpected;
}

/// Used to convert a slice to a null terminated slice on the stack.
pub fn toPosixPath(file_path: []const u8) error{NameTooLong}![PATH_MAX - 1:0]u8 {
    if (std.debug.runtime_safety) assert(mem.findScalar(u8, file_path, 0) == null);
    var path_with_null: [PATH_MAX - 1:0]u8 = undefined;
    // >= rather than > to make room for the null byte
    if (file_path.len >= PATH_MAX) return error.NameTooLong;
    @memcpy(path_with_null[0..file_path.len], file_path);
    path_with_null[file_path.len] = 0;
    return path_with_null;
}



---
File: /std/priority_dequeue.zig
---

const std = @import("std.zig");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const Order = std.math.Order;
const testing = std.testing;
const expect = testing.expect;
const expectEqual = testing.expectEqual;
const expectError = testing.expectError;

/// Priority Dequeue for storing generic data. Initialize with `init`.
/// Provide `compareFn` that returns `Order.lt` when its second
/// argument should get min-popped before its third argument,
/// `Order.eq` if the arguments are of equal priority, or `Order.gt`
/// if the third argument should be min-popped second.
/// Popping the max element works in reverse. For example,
/// to make `popMin` return the smallest number, provide
/// `fn lessThan(context: void, a: T, b: T) Order { _ = context; return std.math.order(a, b); }`
pub fn PriorityDequeue(comptime T: type, comptime Context: type, comptime compareFn: fn (context: Context, a: T, b: T) Order) type {
    return struct {
        const Self = @This();

        items: []T,
        len: usize,
        context: Context,

        /// A priority dequeue containing no elements.
        pub const empty: Self = .{
            .items = &.{},
            .len = 0,
            .context = undefined,
        };

        /// Initialize and return a new priority dequeue with context.
        pub fn initContext(context: Context) Self {
            return Self{
                .items = &.{},
                .len = 0,
                .context = context,
            };
        }

        /// Free memory used by the dequeue.
        pub fn deinit(self: *Self, allocator: Allocator) void {
            allocator.free(self.items);
            self.* = undefined;
        }

        /// Insert a new element, maintaining priority.
        pub fn push(self: *Self, allocator: Allocator, elem: T) !void {
            try self.ensureUnusedCapacity(allocator, 1);
            pushUnchecked(self, elem);
        }

        /// Add each element in `items` to the dequeue.
        pub fn pushSlice(self: *Self, allocator: Allocator, items: []const T) !void {
            try self.ensureUnusedCapacity(allocator, items.len);
            for (items) |e| {
                self.pushUnchecked(e);
            }
        }

        fn pushUnchecked(self: *Self, elem: T) void {
            self.items[self.len] = elem;

            if (self.len > 0) {
                const start = self.getStartForSiftUp(elem, self.len);
                self.siftUp(start);
            }

            self.len += 1;
        }

        fn isMinLayer(index: usize) bool {
            // In the min-max heap structure:
            // The first element is on a min layer;
            // next two are on a max layer;
            // next four are on a min layer, and so on.
            return 1 == @clz(index +% 1) & 1;
        }

        fn nextIsMinLayer(self: *const Self) bool {
            return isMinLayer(self.len);
        }

        const StartIndexAndLayer = struct {
            index: usize,
            min_layer: bool,
        };

        fn getStartForSiftUp(self: *const Self, child: T, index: usize) StartIndexAndLayer {
            const child_index = index;
            const parent_index = parentIndex(child_index);
            const parent = self.items[parent_index];

            const min_layer = self.nextIsMinLayer();
            const order = compareFn(self.context, child, parent);
            if ((min_layer and order == .gt) or (!min_layer and order == .lt)) {
                // We must swap the item with it's parent if it is on the "wrong" layer
                self.items[parent_index] = child;
                self.items[child_index] = parent;
                return .{
                    .index = parent_index,
                    .min_layer = !min_layer,
                };
            } else {
                return .{
                    .index = child_index,
                    .min_layer = min_layer,
                };
            }
        }

        fn siftUp(self: *Self, start: StartIndexAndLayer) void {
            if (start.min_layer) {
                doSiftUp(self, start.index, .lt);
            } else {
                doSiftUp(self, start.index, .gt);
            }
        }

        fn doSiftUp(self: *Self, start_index: usize, target_order: Order) void {
            var child_index = start_index;
            while (child_index > 2) {
                const grandparent_index = grandparentIndex(child_index);
                const child = self.items[child_index];
                const grandparent = self.items[grandparent_index];

                // If the grandparent is already better or equal, we have gone as far as we need to
                if (compareFn(self.context, child, grandparent) != target_order) break;

                // Otherwise swap the item with it's grandparent
                self.items[grandparent_index] = child;
                self.items[child_index] = grandparent;
                child_index = grandparent_index;
            }
        }

        /// Look at the smallest element in the dequeue. Returns
        /// `null` if empty.
        pub fn peekMin(self: *const Self) ?T {
            return if (self.len > 0) self.items[0] else null;
        }

        /// Look at the largest element in the dequeue. Returns
        /// `null` if empty.
        pub fn peekMax(self: *const Self) ?T {
            if (self.len == 0) return null;
            if (self.len == 1) return self.items[0];
            if (self.len == 2) return self.items[1];
            return self.bestItemAtIndices(1, 2, .gt).item;
        }

        fn maxIndex(self: *const Self) ?usize {
            if (self.len == 0) return null;
            if (self.len == 1) return 0;
            if (self.len == 2) return 1;
            return self.bestItemAtIndices(1, 2, .gt).index;
        }

        /// Remove and return the smallest element from the dequeue, or `null` if empty
        pub fn popMin(self: *Self) ?T {
            return if (self.len > 0) self.popIndex(0) else null;
        }

        /// Remove and return the largest element from the dequeue, or `null` if empty
        pub fn popMax(self: *Self) ?T {
            return if (self.len > 0) self.popIndex(self.maxIndex().?) else null;
        }

        /// Remove and return element at index. Indices are in the
        /// same order as iterator, which is not necessarily priority
        /// order.
        pub fn popIndex(self: *Self, index: usize) T {
            assert(self.len > index);
            const item = self.items[index];
            const last = self.items[self.len - 1];

            self.items[index] = last;
            self.len -= 1;
            siftDown(self, index);

            return item;
        }

        fn siftDown(self: *Self, index: usize) void {
            if (isMinLayer(index)) {
                self.doSiftDown(index, .lt);
            } else {
                self.doSiftDown(index, .gt);
            }
        }

        fn doSiftDown(self: *Self, start_index: usize, target_order: Order) void {
            var index = start_index;
            const half = self.len >> 1;
            while (true) {
                const first_grandchild_index = firstGrandchildIndex(index);
                const last_grandchild_index = first_grandchild_index + 3;

                const elem = self.items[index];

                if (last_grandchild_index < self.len) {
                    // All four grandchildren exist
                    const index2 = first_grandchild_index + 1;
                    const index3 = index2 + 1;

                    // Find the best grandchild
                    const best_left = self.bestItemAtIndices(first_grandchild_index, index2, target_order);
                    const best_right = self.bestItemAtIndices(index3, last_grandchild_index, target_order);
                    const best_grandchild = self.bestItem(best_left, best_right, target_order);

                    // If the item is better than or equal to its best grandchild, we are done
                    if (compareFn(self.context, best_grandchild.item, elem) != target_order) return;

                    // Otherwise, swap them
                    self.items[best_grandchild.index] = elem;
                    self.items[index] = best_grandchild.item;
                    index = best_grandchild.index;

                    // We might need to swap the element with it's parent
                    self.swapIfParentIsBetter(elem, index, target_order);
                } else {
                    // The children or grandchildren are the last layer
                    const first_child_index = firstChildIndex(index);
                    if (first_child_index >= self.len) return;

                    const best_descendent = self.bestDescendent(first_child_index, first_grandchild_index, target_order);

                    // If the item is better than or equal to its best descendant, we are done
                    if (compareFn(self.context, best_descendent.item, elem) != target_order) return;

                    // Otherwise swap them
                    self.items[best_descendent.index] = elem;
                    self.items[index] = best_descendent.item;
                    index = best_descendent.index;

                    // If we didn't swap a grandchild, we are done
                    if (index < first_grandchild_index) return;

                    // We might need to swap the element with it's parent
                    self.swapIfParentIsBetter(elem, index, target_order);
                    return;
                }

                // If we are now in the last layer, we are done
                if (index >= half) return;
            }
        }

        fn swapIfParentIsBetter(self: *Self, child: T, child_index: usize, target_order: Order) void {
            const parent_index = parentIndex(child_index);
            const parent = self.items[parent_index];

            if (compareFn(self.context, parent, child) == target_order) {
                self.items[parent_index] = child;
                self.items[child_index] = parent;
            }
        }

        const ItemAndIndex = struct {
            item: T,
            index: usize,
        };

        fn getItem(self: *const Self, index: usize) ItemAndIndex {
            return .{
                .item = self.items[index],
                .index = index,
            };
        }

        fn bestItem(self: *const Self, item1: ItemAndIndex, item2: ItemAndIndex, target_order: Order) ItemAndIndex {
            if (compareFn(self.context, item1.item, item2.item) == target_order) {
                return item1;
            } else {
                return item2;
            }
        }

        fn bestItemAtIndices(self: *const Self, index1: usize, index2: usize, target_order: Order) ItemAndIndex {
            const item1 = self.getItem(index1);
            const item2 = self.getItem(index2);
            return self.bestItem(item1, item2, target_order);
        }

        fn bestDescendent(self: *const Self, first_child_index: usize, first_grandchild_index: usize, target_order: Order) ItemAndIndex {
            const second_child_index = first_child_index + 1;
            if (first_grandchild_index >= self.len) {
                // No grandchildren, find the best child (second may not exist)
                if (second_child_index >= self.len) {
                    return .{
                        .item = self.items[first_child_index],
                        .index = first_child_index,
                    };
                } else {
                    return self.bestItemAtIndices(first_child_index, second_child_index, target_order);
                }
            }

            const second_grandchild_index = first_grandchild_index + 1;
            if (second_grandchild_index >= self.len) {
                // One grandchild, so we know there is a second child. Compare first grandchild and second child
                return self.bestItemAtIndices(first_grandchild_index, second_child_index, target_order);
            }

            const best_left_grandchild_index = self.bestItemAtIndices(first_grandchild_index, second_grandchild_index, target_order).index;
            const third_grandchild_index = second_grandchild_index + 1;
            if (third_grandchild_index >= self.len) {
                // Two grandchildren, and we know the best. Compare this to second child.
                return self.bestItemAtIndices(best_left_grandchild_index, second_child_index, target_order);
            } else {
                // Three grandchildren, compare the min of the first two with the third
                return self.bestItemAtIndices(best_left_grandchild_index, third_grandchild_index, target_order);
            }
        }

        /// Return the number of elements remaining in the dequeue
        pub fn count(self: *const Self) usize {
            return self.len;
        }

        /// Return the number of elements that can be added to the
        /// dequeue before more memory is allocated.
        pub fn capacity(self: *const Self) usize {
            return self.items.len;
        }

        /// Dequeue takes ownership of the passed in slice. The slice must be de-initialize
        /// with `deinit`.
        pub fn fromOwnedSlice(items: []T, context: Context) Self {
            var queue = Self{
                .items = items,
                .len = items.len,
                .context = context,
            };

            if (queue.len <= 1) return queue;

            const half = (queue.len >> 1) - 1;
            var i: usize = 0;
            while (i <= half) : (i += 1) {
                const index = half - i;
                queue.siftDown(index);
            }
            return queue;
        }

        /// Ensure that the dequeue can fit at least `new_capacity` items.
        pub fn ensureTotalCapacity(self: *Self, allocator: Allocator, new_capacity: usize) !void {
            var better_capacity = self.capacity();
            if (better_capacity >= new_capacity) return;
            while (true) {
                better_capacity += better_capacity / 2 + 8;
                if (better_capacity >= new_capacity) break;
            }
            self.items = try allocator.realloc(self.items, better_capacity);
        }

        /// Ensure that the dequeue can fit at least `additional_count` **more** items.
        pub fn ensureUnusedCapacity(self: *Self, allocator: Allocator, additional_count: usize) !void {
            return self.ensureTotalCapacity(allocator, self.len + additional_count);
        }

        /// Reduce allocated capacity to `new_len`.
        pub fn shrinkAndFree(self: *Self, allocator: Allocator, new_len: usize) void {
            assert(new_len <= self.items.len);

            // Cannot shrink to smaller than the current queue size without invalidating the heap property
            assert(new_len >= self.len);

            self.items = allocator.realloc(self.items[0..], new_len) catch |e| switch (e) {
                error.OutOfMemory => { // no problem, capacity is still correct then.
                    self.items.len = new_len;
                    return;
                },
            };
        }

        pub fn update(self: *Self, elem: T, new_elem: T) !void {
            const old_index = blk: {
                var idx: usize = 0;
                while (idx < self.len) : (idx += 1) {
                    const item = self.items[idx];
                    if (compareFn(self.context, item, elem) == .eq) break :blk idx;
                }
                return error.ElementNotFound;
            };
            _ = self.popIndex(old_index);
            self.pushUnchecked(new_elem);
        }

        pub const Iterator = struct {
            queue: *PriorityDequeue(T, Context, compareFn),
            count: usize,

            pub fn next(it: *Iterator) ?T {
                if (it.count >= it.queue.len) return null;
                const out = it.count;
                it.count += 1;
                return it.queue.items[out];
            }

            pub fn reset(it: *Iterator) void {
                it.count = 0;
            }
        };

        /// Return an iterator that walks the queue without consuming
        /// it. The iteration order may differ from the priority order.
        /// Invalidated if the queue is modified.
        pub fn iterator(self: *Self) Iterator {
            return Iterator{
                .queue = self,
                .count = 0,
            };
        }

        fn dump(self: *Self) void {
            const print = std.debug.print;
            print("{{ ", .{});
            print("items: ", .{});
            for (self.items, 0..) |e, i| {
                if (i >= self.len) break;
                print("{}, ", .{e});
            }
            print("array: ", .{});
            for (self.items) |e| {
                print("{}, ", .{e});
            }
            print("len: {} ", .{self.len});
            print("capacity: {}", .{self.capacity()});
            print(" }}\n", .{});
        }

        fn parentIndex(index: usize) usize {
            return (index - 1) >> 1;
        }

        fn grandparentIndex(index: usize) usize {
            return parentIndex(parentIndex(index));
        }

        fn firstChildIndex(index: usize) usize {
            return (index << 1) + 1;
        }

        fn firstGrandchildIndex(index: usize) usize {
            return firstChildIndex(firstChildIndex(index));
        }
    };
}

/// If a min heap is constructed from slice `{5, 8, 2, 9, 7, 1, 4, 4}` using this
/// method, then the elements will be in order: {1, 2, 4, 4, 5, 7, 8, 9}
fn lessThanComparison(context: void, a: u32, b: u32) Order {
    _ = context;
    return std.math.order(a, b);
}

/// Elements with lower priority will be removed first
const MinHeap = PriorityDequeue(u32, void, lessThanComparison);

test "push and pop min in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 54);
    try queue.push(gpa, 12);
    try queue.push(gpa, 7);
    try queue.push(gpa, 23);
    try queue.push(gpa, 25);
    try queue.push(gpa, 13);

    try expectEqual(@as(u32, 7), queue.popMin());
    try expectEqual(@as(u32, 12), queue.popMin());
    try expectEqual(@as(u32, 13), queue.popMin());
    try expectEqual(@as(u32, 23), queue.popMin());
    try expectEqual(@as(u32, 25), queue.popMin());
    try expectEqual(@as(u32, 54), queue.popMin());
}

test "push and pop min structs" {
    const gpa = std.testing.allocator;

    const S = struct {
        size: u32,
    };
    var queue = PriorityDequeue(S, void, struct {
        fn order(context: void, a: S, b: S) Order {
            _ = context;
            return std.math.order(a.size, b.size);
        }
    }.order).initContext({});
    defer queue.deinit(gpa);

    try queue.push(gpa, .{ .size = 54 });
    try queue.push(gpa, .{ .size = 12 });
    try queue.push(gpa, .{ .size = 7 });
    try queue.push(gpa, .{ .size = 23 });
    try queue.push(gpa, .{ .size = 25 });
    try queue.push(gpa, .{ .size = 13 });

    try expectEqual(@as(u32, 7), queue.popMin().?.size);
    try expectEqual(@as(u32, 12), queue.popMin().?.size);
    try expectEqual(@as(u32, 13), queue.popMin().?.size);
    try expectEqual(@as(u32, 23), queue.popMin().?.size);
    try expectEqual(@as(u32, 25), queue.popMin().?.size);
    try expectEqual(@as(u32, 54), queue.popMin().?.size);
}

test "push and pop max in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 54);
    try queue.push(gpa, 12);
    try queue.push(gpa, 7);
    try queue.push(gpa, 23);
    try queue.push(gpa, 25);
    try queue.push(gpa, 13);

    try expectEqual(@as(u32, 54), queue.popMax());
    try expectEqual(@as(u32, 25), queue.popMax());
    try expectEqual(@as(u32, 23), queue.popMax());
    try expectEqual(@as(u32, 13), queue.popMax());
    try expectEqual(@as(u32, 12), queue.popMax());
    try expectEqual(@as(u32, 7), queue.popMax());
}

test "push and pop same min in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.push(gpa, 1);
    try queue.push(gpa, 1);

    try expectEqual(@as(u32, 1), queue.popMin());
    try expectEqual(@as(u32, 1), queue.popMin());
    try expectEqual(@as(u32, 1), queue.popMin());
    try expectEqual(@as(u32, 1), queue.popMin());
    try expectEqual(@as(u32, 2), queue.popMin());
    try expectEqual(@as(u32, 2), queue.popMin());
}

test "push and pop same max in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.push(gpa, 1);
    try queue.push(gpa, 1);

    try expectEqual(@as(u32, 2), queue.popMax());
    try expectEqual(@as(u32, 2), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
}

test "pop empty in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try expect(queue.popMin() == null);
    try expect(queue.popMax() == null);
}

test "edge case 3 elements popMin in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 9);
    try queue.push(gpa, 3);
    try queue.push(gpa, 2);

    try expectEqual(@as(u32, 2), queue.popMin());
    try expectEqual(@as(u32, 3), queue.popMin());
    try expectEqual(@as(u32, 9), queue.popMin());
}

test "edge case 3 elements popmax in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 9);
    try queue.push(gpa, 3);
    try queue.push(gpa, 2);

    try expectEqual(@as(u32, 9), queue.popMax());
    try expectEqual(@as(u32, 3), queue.popMax());
    try expectEqual(@as(u32, 2), queue.popMax());
}

test "peekMin in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try expect(queue.peekMin() == null);

    try queue.push(gpa, 9);
    try queue.push(gpa, 3);
    try queue.push(gpa, 2);

    try expect(queue.peekMin().? == 2);
    try expect(queue.peekMin().? == 2);
}

test "peekMax in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try expect(queue.peekMin() == null);

    try queue.push(gpa, 9);
    try queue.push(gpa, 3);
    try queue.push(gpa, 2);

    try expect(queue.peekMax().? == 9);
    try expect(queue.peekMax().? == 9);
}

test "sift up with odd indices and popMin in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    for (items) |e| {
        try queue.push(gpa, e);
    }

    const sorted_items = [_]u32{ 1, 2, 5, 6, 7, 7, 11, 12, 13, 14, 15, 15, 16, 21, 22, 24, 24, 25 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.popMin());
    }
}

test "sift up with odd indices and popMax in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    for (items) |e| {
        try queue.push(gpa, e);
    }

    const sorted_items = [_]u32{ 25, 24, 24, 22, 21, 16, 15, 15, 14, 13, 12, 11, 7, 7, 6, 5, 2, 1 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.popMax());
    }
}

test "pushSlice in min heap and popMin" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    try queue.pushSlice(gpa, items[0..]);

    const sorted_items = [_]u32{ 1, 2, 5, 6, 7, 7, 11, 12, 13, 14, 15, 15, 16, 21, 22, 24, 24, 25 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.popMin());
    }
}

test "pushSlice in min heap and popMax" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    try queue.pushSlice(gpa, items[0..]);

    const sorted_items = [_]u32{ 25, 24, 24, 22, 21, 16, 15, 15, 14, 13, 12, 11, 7, 7, 6, 5, 2, 1 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.popMax());
    }
}

test "fromOwnedSlice trivial case 0 min heap" {
    const gpa = std.testing.allocator;

    const items = [0]u32{};
    const queue_items = try gpa.dupe(u32, &items);

    var queue: MinHeap = .fromOwnedSlice(queue_items[0..], {});
    defer queue.deinit(gpa);

    try expectEqual(@as(usize, 0), queue.len);
    try expect(queue.popMin() == null);
}

test "fromOwnedSlice trivial case 1 min heap" {
    const gpa = std.testing.allocator;

    const items = [1]u32{1};
    const queue_items = try gpa.dupe(u32, &items);

    var queue: MinHeap = .fromOwnedSlice(queue_items[0..], {});
    defer queue.deinit(gpa);

    try expectEqual(@as(usize, 1), queue.len);
    try expectEqual(items[0], queue.popMin());
    try expect(queue.popMin() == null);
}

test "fromOwnedSlice min heap" {
    const gpa = std.testing.allocator;

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    const queue_items = try gpa.dupe(u32, items[0..]);

    var queue: MinHeap = .fromOwnedSlice(queue_items[0..], {});
    defer queue.deinit(gpa);

    const sorted_items = [_]u32{ 1, 2, 5, 6, 7, 7, 11, 12, 13, 14, 15, 15, 16, 21, 22, 24, 24, 25 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.popMin());
    }
}

test "update and popMin in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 55);
    try queue.push(gpa, 44);
    try queue.push(gpa, 11);
    try queue.update(55, 5);
    try queue.update(44, 4);
    try queue.update(11, 1);
    try expectEqual(@as(u32, 1), queue.popMin());
    try expectEqual(@as(u32, 4), queue.popMin());
    try expectEqual(@as(u32, 5), queue.popMin());
}

test "update same element and popMin in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.update(1, 5);
    try queue.update(2, 4);
    try expectEqual(@as(u32, 1), queue.popMin());
    try expectEqual(@as(u32, 2), queue.popMin());
    try expectEqual(@as(u32, 4), queue.popMin());
    try expectEqual(@as(u32, 5), queue.popMin());
}

test "update and popMax in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 55);
    try queue.push(gpa, 44);
    try queue.push(gpa, 11);
    try queue.update(55, 5);
    try queue.update(44, 1);
    try queue.update(11, 4);

    try expectEqual(@as(u32, 5), queue.popMax());
    try expectEqual(@as(u32, 4), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
}

test "update same element and popMax in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.update(1, 5);
    try queue.update(2, 4);
    try expectEqual(@as(u32, 5), queue.popMax());
    try expectEqual(@as(u32, 4), queue.popMax());
    try expectEqual(@as(u32, 2), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
}

test "update after pop in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try expectEqual(@as(u32, 1), queue.popMin());
    try expectError(error.ElementNotFound, queue.update(1, 1));
}

test "min heap iterator" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    var map = std.AutoHashMap(u32, void).init(testing.allocator);
    defer {
        queue.deinit(gpa);
        map.deinit();
    }

    const items = [_]u32{ 54, 12, 7, 23, 25, 13 };
    for (items) |e| {
        _ = try queue.push(gpa, e);
        _ = try map.put(e, {});
    }

    var it = queue.iterator();
    while (it.next()) |e| {
        _ = map.remove(e);
    }

    try expectEqual(@as(usize, 0), map.count());
}

test "pop at index in min heap" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 3);
    try queue.push(gpa, 2);
    try queue.push(gpa, 1);

    var it = queue.iterator();
    var elem = it.next();
    var idx: usize = 0;
    const two_idx = while (elem != null) : (elem = it.next()) {
        if (elem.? == 2)
            break idx;
        idx += 1;
    } else unreachable;

    try expectEqual(queue.popIndex(two_idx), 2);
    try expectEqual(queue.popMin(), 1);
    try expectEqual(queue.popMin(), 3);
    try expectEqual(queue.popMin(), null);
}

test "min heap iterator while empty" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    var it = queue.iterator();

    try expectEqual(it.next(), null);
}

test "min heap shrinkAndFree" {
    const gpa = std.testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.ensureTotalCapacity(gpa, 4);
    try expect(queue.capacity() >= 4);

    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 3);
    try expect(queue.capacity() >= 4);
    try expectEqual(@as(usize, 3), queue.len);

    queue.shrinkAndFree(gpa, 3);
    try expectEqual(@as(usize, 3), queue.capacity());
    try expectEqual(@as(usize, 3), queue.len);

    try expectEqual(@as(u32, 3), queue.popMax());
    try expectEqual(@as(u32, 2), queue.popMax());
    try expectEqual(@as(u32, 1), queue.popMax());
    try expect(queue.popMax() == null);
}

test "fuzz testing min" {
    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const test_case_count = 100;
    const queue_size = 1_000;

    var i: usize = 0;
    while (i < test_case_count) : (i += 1) {
        try fuzzTestMin(random, queue_size);
    }
}

fn fuzzTestMin(rng: std.Random, comptime queue_size: usize) !void {
    const gpa = std.testing.allocator;

    const items = try generateRandomSlice(gpa, rng, queue_size);

    var queue: MinHeap = .fromOwnedSlice(items, {});
    defer queue.deinit(gpa);

    var last_removed: ?u32 = null;
    while (queue.popMin()) |next| {
        if (last_removed) |last| {
            try expect(last <= next);
        }
        last_removed = next;
    }
}

test "fuzz testing max" {
    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const test_case_count = 100;
    const queue_size = 1_000;

    var i: usize = 0;
    while (i < test_case_count) : (i += 1) {
        try fuzzTestMax(random, queue_size);
    }
}

fn fuzzTestMax(rng: std.Random, queue_size: usize) !void {
    const gpa = std.testing.allocator;

    const items = try generateRandomSlice(gpa, rng, queue_size);

    var queue: MinHeap = .fromOwnedSlice(items, {});
    defer queue.deinit(gpa);

    var last_removed: ?u32 = null;
    while (queue.popMax()) |next| {
        if (last_removed) |last| {
            try expect(last >= next);
        }
        last_removed = next;
    }
}

test "fuzz testing min and max" {
    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();

    const test_case_count = 100;
    const queue_size = 1_000;

    var i: usize = 0;
    while (i < test_case_count) : (i += 1) {
        try fuzzTestMinMax(random, queue_size);
    }
}

fn fuzzTestMinMax(rng: std.Random, queue_size: usize) !void {
    const gpa = std.testing.allocator;

    const items = try generateRandomSlice(gpa, rng, queue_size);

    var queue: MinHeap = .fromOwnedSlice(items, {});
    defer queue.deinit(gpa);

    var last_min: ?u32 = null;
    var last_max: ?u32 = null;
    var i: usize = 0;
    while (i < queue_size) : (i += 1) {
        if (i % 2 == 0) {
            const next = queue.popMin().?;
            if (last_min) |last| {
                try expect(last <= next);
            }
            last_min = next;
        } else {
            const next = queue.popMax().?;
            if (last_max) |last| {
                try expect(last >= next);
            }
            last_max = next;
        }
    }
}

fn generateRandomSlice(allocator: std.mem.Allocator, rng: std.Random, size: usize) ![]u32 {
    var array = std.array_list.Managed(u32).init(allocator);
    try array.ensureTotalCapacity(size);

    var i: usize = 0;
    while (i < size) : (i += 1) {
        const elem = rng.int(u32);
        try array.append(elem);
    }

    return array.toOwnedSlice();
}

fn contextLessThanComparison(context: []const u32, a: usize, b: usize) Order {
    return std.math.order(context[a], context[b]);
}

const MinHeapWithContext = PriorityDequeue(usize, []const u32, contextLessThanComparison);

test "push and pop" {
    const gpa = std.testing.allocator;

    const context = [_]u32{ 5, 3, 4, 2, 2, 8, 0 };

    var queue: MinHeapWithContext = .initContext(context[0..]);
    defer queue.deinit(gpa);

    try queue.push(gpa, 0);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 3);
    try queue.push(gpa, 4);
    try queue.push(gpa, 5);
    try queue.push(gpa, 6);
    try expectEqual(@as(usize, 6), queue.popMin());
    try expectEqual(@as(usize, 5), queue.popMax());
    try expectEqual(@as(usize, 3), queue.popMin());
    try expectEqual(@as(usize, 0), queue.popMax());
    try expectEqual(@as(usize, 4), queue.popMin());
    try expectEqual(@as(usize, 2), queue.popMax());
    try expectEqual(@as(usize, 1), queue.popMin());
}

var all_cmps_unique = true;

test "don't compare a value to a copy of itself" {
    const gpa = std.testing.allocator;

    var depq = PriorityDequeue(u32, void, struct {
        fn uniqueLessThan(_: void, a: u32, b: u32) Order {
            all_cmps_unique = all_cmps_unique and (a != b);
            return std.math.order(a, b);
        }
    }.uniqueLessThan).initContext({});
    defer depq.deinit(gpa);

    try depq.push(gpa, 1);
    try depq.push(gpa, 2);
    try depq.push(gpa, 3);
    try depq.push(gpa, 4);
    try depq.push(gpa, 5);
    try depq.push(gpa, 6);

    _ = depq.popIndex(2);
    try expectEqual(all_cmps_unique, true);
}



---
File: /std/priority_queue.zig
---

const std = @import("std.zig");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const Order = std.math.Order;
const testing = std.testing;
const expect = testing.expect;
const expectEqual = testing.expectEqual;
const expectError = testing.expectError;

/// Priority queue for storing generic data. Initialize with `init`.
/// Provide `compareFn` that returns `Order.lt` when its second
/// argument should get popped before its third argument,
/// `Order.eq` if the arguments are of equal priority, or `Order.gt`
/// if the third argument should be popped first.
/// For example, to make `pop` return the smallest number, provide
/// `fn lessThan(context: void, a: T, b: T) Order { _ = context; return std.math.order(a, b); }`
pub fn PriorityQueue(comptime T: type, comptime Context: type, comptime compareFn: fn (context: Context, a: T, b: T) Order) type {
    return struct {
        const Self = @This();

        items: []T,
        cap: usize,
        context: Context,

        /// A priority queue containing no elements.
        pub const empty: Self = .{
            .items = &.{},
            .cap = 0,
            .context = undefined,
        };

        /// Initialize and return a priority queue with context.
        pub fn initContext(context: Context) Self {
            return Self{
                .items = &.{},
                .cap = 0,
                .context = context,
            };
        }

        /// Free memory used by the queue.
        pub fn deinit(self: *Self, allocator: Allocator) void {
            allocator.free(self.allocatedSlice());
            self.* = undefined;
        }

        /// Insert a new element, maintaining priority.
        pub fn push(self: *Self, allocator: Allocator, elem: T) !void {
            try self.ensureUnusedCapacity(allocator, 1);
            pushUnchecked(self, elem);
        }

        fn pushUnchecked(self: *Self, elem: T) void {
            self.items.len += 1;
            self.items[self.items.len - 1] = elem;
            siftUp(self, self.items.len - 1);
        }

        fn siftUp(self: *Self, start_index: usize) void {
            const child = self.items[start_index];
            var child_index = start_index;
            while (child_index > 0) {
                const parent_index = ((child_index - 1) >> 1);
                const parent = self.items[parent_index];
                if (compareFn(self.context, child, parent) != .lt) break;
                self.items[child_index] = parent;
                child_index = parent_index;
            }
            self.items[child_index] = child;
        }

        /// Add each element in `items` to the queue.
        pub fn pushSlice(self: *Self, allocator: Allocator, items: []const T) !void {
            try self.ensureUnusedCapacity(allocator, items.len);
            for (items) |e| {
                self.pushUnchecked(e);
            }
        }

        /// Look at the highest priority element in the queue. Returns
        /// `null` if empty.
        pub fn peek(self: *const Self) ?T {
            return if (self.items.len > 0) self.items[0] else null;
        }

        /// Remove and return the highest priority element from the queue.
        /// Returns `null` if empty.
        pub fn pop(self: *Self) ?T {
            return if (self.items.len > 0) self.popIndex(0) else null;
        }

        /// Remove and return element at index. Indices are in the
        /// same order as iterator, which is not necessarily priority
        /// order.
        pub fn popIndex(self: *Self, index: usize) T {
            assert(self.items.len > index);
            const last = self.items[self.items.len - 1];
            const item = self.items[index];
            self.items[index] = last;
            self.items.len -= 1;

            if (index == self.items.len) {
                // Last element removed, nothing more to do.
            } else if (index == 0) {
                siftDown(self, index);
            } else {
                const parent_index = ((index - 1) >> 1);
                const parent = self.items[parent_index];
                if (compareFn(self.context, last, parent) == .gt) {
                    siftDown(self, index);
                } else {
                    siftUp(self, index);
                }
            }

            return item;
        }

        /// Return the number of elements remaining in the priority
        /// queue.
        pub fn count(self: *const Self) usize {
            return self.items.len;
        }

        /// Return the number of elements that can be added to the
        /// queue before more memory is allocated.
        pub fn capacity(self: *const Self) usize {
            return self.cap;
        }

        /// Returns a slice of all the items plus the extra capacity, whose memory
        /// contents are `undefined`.
        fn allocatedSlice(self: *const Self) []T {
            // `items.len` is the length, not the capacity.
            return self.items.ptr[0..self.cap];
        }

        fn siftDown(self: *Self, target_index: usize) void {
            const target_element = self.items[target_index];
            var index = target_index;
            while (true) {
                var lesser_child_i = (std.math.mul(usize, index, 2) catch break) | 1;
                if (!(lesser_child_i < self.items.len)) break;

                const next_child_i = lesser_child_i + 1;
                if (next_child_i < self.items.len and compareFn(self.context, self.items[next_child_i], self.items[lesser_child_i]) == .lt) {
                    lesser_child_i = next_child_i;
                }

                if (compareFn(self.context, target_element, self.items[lesser_child_i]) == .lt) break;

                self.items[index] = self.items[lesser_child_i];
                index = lesser_child_i;
            }
            self.items[index] = target_element;
        }

        /// PriorityQueue takes ownership of the passed in slice. The slice must have been
        /// allocated with `allocator`.
        /// Deinitialize with `deinit`.
        pub fn fromOwnedSlice(items: []T, context: Context) Self {
            var self = Self{
                .items = items,
                .cap = items.len,
                .context = context,
            };

            var i = self.items.len >> 1;
            while (i > 0) {
                i -= 1;
                self.siftDown(i);
            }
            return self;
        }

        /// Ensure that the queue can fit at least `new_capacity` items.
        pub fn ensureTotalCapacity(self: *Self, allocator: Allocator, new_capacity: usize) !void {
            var better_capacity = self.cap;
            if (better_capacity >= new_capacity) return;
            while (true) {
                better_capacity += better_capacity / 2 + 8;
                if (better_capacity >= new_capacity) break;
            }
            try self.ensureTotalCapacityPrecise(allocator, better_capacity);
        }

        /// If the current capacity is less than `new_capacity`, this function will
        /// modify the array so that it can hold exactly `new_capacity` items.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureTotalCapacityPrecise(self: *Self, allocator: Allocator, new_capacity: usize) !void {
            if (self.capacity() >= new_capacity) return;

            const old_memory = self.allocatedSlice();
            const new_memory = try allocator.realloc(old_memory, new_capacity);
            self.items.ptr = new_memory.ptr;
            self.cap = new_memory.len;
        }

        /// Ensure that the queue can fit at least `additional_count` **more** item.
        pub fn ensureUnusedCapacity(self: *Self, allocator: Allocator, additional_count: usize) !void {
            return self.ensureTotalCapacity(allocator, self.items.len + additional_count);
        }

        /// Reduce allocated capacity to `new_capacity`.
        pub fn shrinkAndFree(self: *Self, allocator: Allocator, new_capacity: usize) void {
            assert(new_capacity <= self.cap);

            // Cannot shrink to smaller than the current queue size without invalidating the heap property
            assert(new_capacity >= self.items.len);

            const old_memory = self.allocatedSlice();
            const new_memory = allocator.realloc(old_memory, new_capacity) catch |e| switch (e) {
                error.OutOfMemory => { // no problem, capacity is still correct then.
                    return;
                },
            };

            self.items.ptr = new_memory.ptr;
            self.cap = new_memory.len;
        }

        /// Remove all elements from the items slice.
        pub fn clearRetainingCapacity(self: *Self) void {
            self.items.len = 0;
        }

        /// Invalidates all element pointers.
        pub fn clearAndFree(self: *Self, allocator: Allocator) void {
            allocator.free(self.allocatedSlice());
            self.items.len = 0;
            self.cap = 0;
        }

        /// Replace an element in the queue with a new element, maintaining priority.
        /// If the element being updated doesn't exist, return `error.ElementNotFound`.
        pub fn update(self: *Self, elem: T, new_elem: T) !void {
            const update_index = blk: {
                var idx: usize = 0;
                while (idx < self.items.len) : (idx += 1) {
                    const item = self.items[idx];
                    if (compareFn(self.context, item, elem) == .eq) break :blk idx;
                }
                return error.ElementNotFound;
            };
            const old_elem: T = self.items[update_index];
            self.items[update_index] = new_elem;
            switch (compareFn(self.context, new_elem, old_elem)) {
                .lt => siftUp(self, update_index),
                .gt => siftDown(self, update_index),
                .eq => {}, // Nothing to do as the items have equal priority
            }
        }

        pub const Iterator = struct {
            queue: *PriorityQueue(T, Context, compareFn),
            count: usize,

            pub fn next(it: *Iterator) ?T {
                if (it.count >= it.queue.items.len) return null;
                const out = it.count;
                it.count += 1;
                return it.queue.items[out];
            }

            pub fn reset(it: *Iterator) void {
                it.count = 0;
            }
        };

        /// Return an iterator that walks the queue without consuming
        /// it. The iteration order may differ from the priority order.
        /// Invalidated if the heap is modified.
        pub fn iterator(self: *Self) Iterator {
            return Iterator{
                .queue = self,
                .count = 0,
            };
        }
    };
}

fn lessThan(context: void, a: u32, b: u32) Order {
    _ = context;
    return std.math.order(a, b);
}

fn greaterThan(context: void, a: u32, b: u32) Order {
    return lessThan(context, a, b).invert();
}

const MinHeap = PriorityQueue(u32, void, lessThan);
const MaxHeap = PriorityQueue(u32, void, greaterThan);

test "add and remove min heap" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 54);
    try queue.push(gpa, 12);
    try queue.push(gpa, 7);
    try queue.push(gpa, 23);
    try queue.push(gpa, 25);
    try queue.push(gpa, 13);
    try expectEqual(@as(u32, 7), queue.pop());
    try expectEqual(@as(u32, 12), queue.pop());
    try expectEqual(@as(u32, 13), queue.pop());
    try expectEqual(@as(u32, 23), queue.pop());
    try expectEqual(@as(u32, 25), queue.pop());
    try expectEqual(@as(u32, 54), queue.pop());
}

test "add and remove same min heap" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 2), queue.pop());
}

test "removeOrNull on empty" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try expect(queue.pop() == null);
}

test "edge case 3 elements" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 9);
    try queue.push(gpa, 3);
    try queue.push(gpa, 2);
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 3), queue.pop());
    try expectEqual(@as(u32, 9), queue.pop());
}

test "peek" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try expect(queue.peek() == null);
    try queue.push(gpa, 9);
    try queue.push(gpa, 3);
    try queue.push(gpa, 2);
    try expectEqual(@as(u32, 2), queue.peek().?);
    try expectEqual(@as(u32, 2), queue.peek().?);
}

test "sift up with odd indices" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    for (items) |e| {
        try queue.push(gpa, e);
    }

    const sorted_items = [_]u32{ 1, 2, 5, 6, 7, 7, 11, 12, 13, 14, 15, 15, 16, 21, 22, 24, 24, 25 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.pop());
    }
}

test "addSlice" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    try queue.pushSlice(gpa, items[0..]);

    const sorted_items = [_]u32{ 1, 2, 5, 6, 7, 7, 11, 12, 13, 14, 15, 15, 16, 21, 22, 24, 24, 25 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.pop());
    }
}

test "fromOwnedSlice trivial case 0" {
    const gpa = testing.allocator;

    const items = [0]u32{};
    const queue_items = try gpa.dupe(u32, &items);

    var queue: MinHeap = .fromOwnedSlice(queue_items[0..], {});
    defer queue.deinit(gpa);

    try expectEqual(@as(usize, 0), queue.count());
    try expect(queue.pop() == null);
}

test "fromOwnedSlice trivial case 1" {
    const gpa = testing.allocator;

    const items = [1]u32{1};
    const queue_items = try gpa.dupe(u32, &items);

    var queue: MinHeap = .fromOwnedSlice(queue_items[0..], {});
    defer queue.deinit(gpa);

    try expectEqual(@as(usize, 1), queue.count());
    try expectEqual(items[0], queue.pop());
    try expect(queue.pop() == null);
}

test "fromOwnedSlice" {
    const gpa = testing.allocator;

    const items = [_]u32{ 15, 7, 21, 14, 13, 22, 12, 6, 7, 25, 5, 24, 11, 16, 15, 24, 2, 1 };
    const heap_items = try gpa.dupe(u32, items[0..]);

    var queue: MinHeap = .fromOwnedSlice(heap_items[0..], {});
    defer queue.deinit(gpa);

    const sorted_items = [_]u32{ 1, 2, 5, 6, 7, 7, 11, 12, 13, 14, 15, 15, 16, 21, 22, 24, 24, 25 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.pop());
    }
}

test "add and remove max heap" {
    const gpa = testing.allocator;

    var queue: MaxHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 54);
    try queue.push(gpa, 12);
    try queue.push(gpa, 7);
    try queue.push(gpa, 23);
    try queue.push(gpa, 25);
    try queue.push(gpa, 13);
    try expectEqual(@as(u32, 54), queue.pop());
    try expectEqual(@as(u32, 25), queue.pop());
    try expectEqual(@as(u32, 23), queue.pop());
    try expectEqual(@as(u32, 13), queue.pop());
    try expectEqual(@as(u32, 12), queue.pop());
    try expectEqual(@as(u32, 7), queue.pop());
}

test "add and remove same max heap" {
    const gpa = testing.allocator;

    var queue: MaxHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
}

test "iterator" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    var map = std.AutoHashMap(u32, void).init(testing.allocator);
    defer {
        queue.deinit(gpa);
        map.deinit();
    }

    const items = [_]u32{ 54, 12, 7, 23, 25, 13 };
    for (items) |e| {
        _ = try queue.push(gpa, e);
        try map.put(e, {});
    }

    var it = queue.iterator();
    while (it.next()) |e| {
        _ = map.remove(e);
    }

    try expectEqual(@as(usize, 0), map.count());
}

test "remove at index" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    const items = [_]u32{ 2, 1, 8, 9, 3, 4, 5 };
    for (items) |e| {
        _ = try queue.push(gpa, e);
    }

    var it = queue.iterator();
    var idx: usize = 0;
    const two_idx = while (it.next()) |elem| {
        if (elem == 2)
            break idx;
        idx += 1;
    } else unreachable;
    const sorted_items = [_]u32{ 1, 3, 4, 5, 8, 9 };
    try expectEqual(queue.popIndex(two_idx), 2);

    var i: usize = 0;
    while (queue.pop()) |n| : (i += 1) {
        try expectEqual(n, sorted_items[i]);
    }
    try expectEqual(queue.pop(), null);
}

test "iterator while empty" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    var it = queue.iterator();

    try expectEqual(it.next(), null);
}

test "shrinkAndFree" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.ensureTotalCapacity(gpa, 4);
    try expect(queue.capacity() >= 4);

    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 3);
    try expect(queue.capacity() >= 4);
    try expectEqual(@as(usize, 3), queue.count());

    queue.shrinkAndFree(gpa, 3);
    try expectEqual(@as(usize, 3), queue.capacity());
    try expectEqual(@as(usize, 3), queue.count());

    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 3), queue.pop());
    try expect(queue.pop() == null);
}

test "update min heap" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 55);
    try queue.push(gpa, 44);
    try queue.push(gpa, 11);
    try queue.update(55, 5);
    try queue.update(44, 4);
    try queue.update(11, 1);
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 4), queue.pop());
    try expectEqual(@as(u32, 5), queue.pop());
}

test "update same min heap" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.update(1, 5);
    try queue.update(2, 4);
    try expectEqual(@as(u32, 1), queue.pop());
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 4), queue.pop());
    try expectEqual(@as(u32, 5), queue.pop());
}

test "update max heap" {
    const gpa = testing.allocator;

    var queue: MaxHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 55);
    try queue.push(gpa, 44);
    try queue.push(gpa, 11);
    try queue.update(55, 5);
    try queue.update(44, 1);
    try queue.update(11, 4);
    try expectEqual(@as(u32, 5), queue.pop());
    try expectEqual(@as(u32, 4), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
}

test "update same max heap" {
    const gpa = testing.allocator;

    var queue: MaxHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 2);
    try queue.update(1, 5);
    try queue.update(2, 4);
    try expectEqual(@as(u32, 5), queue.pop());
    try expectEqual(@as(u32, 4), queue.pop());
    try expectEqual(@as(u32, 2), queue.pop());
    try expectEqual(@as(u32, 1), queue.pop());
}

test "update after remove" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.push(gpa, 1);
    try expectEqual(@as(u32, 1), queue.pop());
    try expectError(error.ElementNotFound, queue.update(1, 1));
}

test "siftUp in remove" {
    const gpa = testing.allocator;

    var queue: MinHeap = .empty;
    defer queue.deinit(gpa);

    try queue.pushSlice(gpa, &.{ 0, 1, 100, 2, 3, 101, 102, 4, 5, 6, 7, 103, 104, 105, 106, 8 });

    _ = queue.popIndex(std.mem.findScalar(u32, queue.items[0..queue.count()], 102).?);

    const sorted_items = [_]u32{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 100, 101, 103, 104, 105, 106 };
    for (sorted_items) |e| {
        try expectEqual(e, queue.pop());
    }
}

fn contextLessThan(context: []const u32, a: usize, b: usize) Order {
    return std.math.order(context[a], context[b]);
}

const MinHeapWithContext = PriorityQueue(usize, []const u32, contextLessThan);

test "add and remove min heap with context comparator" {
    const gpa = testing.allocator;

    const context = [_]u32{ 5, 3, 4, 2, 2, 8, 0 };

    var queue: MinHeapWithContext = .initContext(context[0..]);
    defer queue.deinit(gpa);

    try queue.push(gpa, 0);
    try queue.push(gpa, 1);
    try queue.push(gpa, 2);
    try queue.push(gpa, 3);
    try queue.push(gpa, 4);
    try queue.push(gpa, 5);
    try queue.push(gpa, 6);
    try expectEqual(@as(usize, 6), queue.pop());
    try expectEqual(@as(usize, 4), queue.pop());
    try expectEqual(@as(usize, 3), queue.pop());
    try expectEqual(@as(usize, 1), queue.pop());
    try expectEqual(@as(usize, 2), queue.pop());
    try expectEqual(@as(usize, 0), queue.pop());
    try expectEqual(@as(usize, 5), queue.pop());
}



---
File: /std/process.zig
---

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("std.zig");
const Io = std.Io;
const File = std.Io.File;
const fs = std.fs;
const mem = std.mem;
const math = std.math;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const testing = std.testing;
const posix = std.posix;
const windows = std.os.windows;
const unicode = std.unicode;
const max_path_bytes = std.fs.max_path_bytes;

pub const Child = @import("process/Child.zig");
pub const Args = @import("process/Args.zig");
pub const Environ = @import("process/Environ.zig");
pub const Preopens = @import("process/Preopens.zig");

/// A standard set of pre-initialized useful APIs for programs to take
/// advantage of. This is the type of the first parameter of the main function.
/// Applications wanting more flexibility can accept `Init.Minimal` instead.
///
/// Completion of https://github.com/ziglang/zig/issues/24510 will also allow
/// the second parameter of the main function to be a custom struct that
/// contain auto-parsed CLI arguments.
pub const Init = struct {
    /// `Init` is a superset of `Minimal`; the latter is included here.
    minimal: Minimal,
    /// Permanent storage for the entire process, cleaned automatically on
    /// exit. Threadsafe.
    arena: *std.heap.ArenaAllocator,
    /// A default-selected general purpose allocator for temporary heap
    /// allocations. Debug mode will set up leak checking if possible.
    /// Threadsafe.
    gpa: Allocator,
    /// An appropriate default Io implementation based on the target
    /// configuration. Debug mode will set up leak checking if possible.
    io: Io,
    /// Environment variables, initialized with `gpa`. Not threadsafe.
    environ_map: *Environ.Map,
    /// Named files that have been provided by the parent process. This is
    /// mainly useful on WASI, but can be used on other systems to mimic the
    /// behavior with respect to stdio.
    preopens: Preopens,

    /// Alternative to `Init` as the first parameter of the main function.
    pub const Minimal = struct {
        /// Environment variables.
        environ: Environ,
        /// Command line arguments.
        args: Args,
    };
};

pub const CurrentPathError = error{
    NameTooLong,
    /// Not possible on Windows. Always returned on WASI.
    CurrentDirUnlinked,
} || Io.Cancelable || Io.UnexpectedError;

/// On Windows, the result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On other platforms, the result is an opaque sequence of bytes with no
/// particular encoding.
pub fn currentPath(io: Io, buffer: []u8) CurrentPathError!usize {
    return io.vtable.processCurrentPath(io.userdata, buffer);
}

pub const CurrentPathAllocError = Allocator.Error || error{
    /// Not possible on Windows. Always returned on WASI.
    CurrentDirUnlinked,
} || Io.Cancelable || Io.UnexpectedError;

/// On Windows, the result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On other platforms, the result is an opaque sequence of bytes with no
/// particular encoding.
///
/// Caller owns returned memory.
pub fn currentPathAlloc(io: Io, allocator: Allocator) CurrentPathAllocError![:0]u8 {
    var buffer: [max_path_bytes]u8 = undefined;
    const n = currentPath(io, &buffer) catch |err| switch (err) {
        error.NameTooLong => unreachable,
        else => |e| return e,
    };
    return allocator.dupeZ(u8, buffer[0..n]);
}

test currentPathAlloc {
    const cwd = try currentPathAlloc(testing.io, testing.allocator);
    testing.allocator.free(cwd);
}

pub const UserInfo = struct {
    uid: posix.uid_t,
    gid: posix.gid_t,
};

/// POSIX function which gets a uid from username.
pub fn getUserInfo(name: []const u8) !UserInfo {
    return switch (native_os) {
        .linux,
        .driverkit,
        .ios,
        .maccatalyst,
        .macos,
        .tvos,
        .visionos,
        .watchos,
        .freebsd,
        .netbsd,
        .openbsd,
        .haiku,
        .illumos,
        .serenity,
        => posixGetUserInfo(name),
        else => @compileError("Unsupported OS"),
    };
}

/// TODO this reads /etc/passwd. But sometimes the user/id mapping is in something else
/// like NIS, AD, etc. See `man nss` or look at an strace for `id myuser`.
pub fn posixGetUserInfo(io: Io, name: []const u8) !UserInfo {
    const file = try Io.Dir.openFileAbsolute(io, "/etc/passwd", .{});
    defer file.close(io);
    var buffer: [4096]u8 = undefined;
    var file_reader = file.reader(&buffer);
    return posixGetUserInfoPasswdStream(name, &file_reader.interface) catch |err| switch (err) {
        error.ReadFailed => return file_reader.err.?,
        error.EndOfStream => return error.UserNotFound,
        error.CorruptPasswordFile => return error.CorruptPasswordFile,
    };
}

fn posixGetUserInfoPasswdStream(name: []const u8, reader: *std.Io.Reader) !UserInfo {
    const State = enum {
        start,
        wait_for_next_line,
        skip_password,
        read_user_id,
        read_group_id,
    };

    var name_index: usize = 0;
    var uid: posix.uid_t = 0;
    var gid: posix.gid_t = 0;

    sw: switch (State.start) {
        .start => switch (try reader.takeByte()) {
            ':' => {
                if (name_index == name.len) {
                    continue :sw .skip_password;
                } else {
                    continue :sw .wait_for_next_line;
                }
            },
            '\n' => return error.CorruptPasswordFile,
            else => |byte| {
                if (name_index == name.len or name[name_index] != byte) {
                    continue :sw .wait_for_next_line;
                }
                name_index += 1;
                continue :sw .start;
            },
        },
        .wait_for_next_line => switch (try reader.takeByte()) {
            '\n' => {
                name_index = 0;
                continue :sw .start;
            },
            else => continue :sw .wait_for_next_line,
        },
        .skip_password => switch (try reader.takeByte()) {
            '\n' => return error.CorruptPasswordFile,
            ':' => {
                continue :sw .read_user_id;
            },
            else => continue :sw .skip_password,
        },
        .read_user_id => switch (try reader.takeByte()) {
            ':' => {
                continue :sw .read_group_id;
            },
            '\n' => return error.CorruptPasswordFile,
            else => |byte| {
                const digit = switch (byte) {
                    '0'...'9' => byte - '0',
                    else => return error.CorruptPasswordFile,
                };
                {
                    const ov = @mulWithOverflow(uid, 10);
                    if (ov[1] != 0) return error.CorruptPasswordFile;
                    uid = ov[0];
                }
                {
                    const ov = @addWithOverflow(uid, digit);
                    if (ov[1] != 0) return error.CorruptPasswordFile;
                    uid = ov[0];
                }
                continue :sw .read_user_id;
            },
        },
        .read_group_id => switch (try reader.takeByte()) {
            '\n', ':' => return .{
                .uid = uid,
                .gid = gid,
            },
            else => |byte| {
                const digit = switch (byte) {
                    '0'...'9' => byte - '0',
                    else => return error.CorruptPasswordFile,
                };
                {
                    const ov = @mulWithOverflow(gid, 10);
                    if (ov[1] != 0) return error.CorruptPasswordFile;
                    gid = ov[0];
                }
                {
                    const ov = @addWithOverflow(gid, digit);
                    if (ov[1] != 0) return error.CorruptPasswordFile;
                    gid = ov[0];
                }
                continue :sw .read_group_id;
            },
        },
    }
    comptime unreachable;
}

pub fn getBaseAddress() usize {
    switch (native_os) {
        .linux => {
            const phdrs = std.posix.getSelfPhdrs();
            var base: usize = 0;
            for (phdrs) |phdr| switch (phdr.type) {
                .LOAD => return base + phdr.vaddr,
                .PHDR => base = @intFromPtr(phdrs.ptr) - phdr.vaddr,
                else => {},
            } else unreachable;
        },
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
            return @intFromPtr(&std.c._mh_execute_header);
        },
        .windows => return @intFromPtr(windows.peb().ImageBaseAddress),
        else => @compileError("Unsupported OS"),
    }
}

/// Tells whether the target operating system supports replacing the current
/// process image. If this is `false` then calling `replace` or `replaceFile`
/// functions will return `error.OperationUnsupported`.
pub const can_replace = switch (native_os) {
    .windows, .haiku, .wasi => false,
    else => true,
};

/// Tells whether spawning child processes is supported.
pub const can_spawn = switch (native_os) {
    .wasi, .ios, .tvos, .visionos, .watchos => false,
    else => true,
};

pub const ReplaceError = error{
    /// The target operating system cannot replace the process image with a new
    /// one.
    OperationUnsupported,
    SystemResources,
    AccessDenied,
    PermissionDenied,
    InvalidExe,
    FileSystem,
    IsDir,
    FileNotFound,
    NotDir,
    FileBusy,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
} || Allocator.Error || Io.Dir.PathNameError || Io.Cancelable || Io.UnexpectedError;

pub const ReplaceOptions = struct {
    argv: []const []const u8,
    expand_arg0: ArgExpansion = .no_expand,
    /// Replaces the environment when provided. The PATH value from here is
    /// never used to resolve `argv[0]`.
    environ_map: ?*const Environ.Map = null,
};

/// Replaces the current process image with the executed process. If this
/// function succeeds, it does not return.
///
/// `argv[0]` is the name of the process to replace the current one with. If it
/// is not already a file path (i.e. it contains '/'), it is resolved into a
/// file path based on PATH from the parent environment.
///
/// It is illegal to call this function in a fork() child.
pub fn replace(io: Io, options: ReplaceOptions) ReplaceError {
    return io.vtable.processReplace(io.userdata, options);
}

/// Replaces the current process image with the executed process. If this
/// function succeeds, it does not return.
///
/// `argv[0]` is the file path of the process to replace the current one with,
/// relative to `dir`. It is *always* treated as a file path, even if it does
/// not contain '/'.
///
/// It is illegal to call this function in a fork() child.
pub fn replacePath(io: Io, dir: Io.Dir, options: ReplaceOptions) ReplaceError {
    return io.vtable.processReplacePath(io.userdata, dir, options);
}

pub const ArgExpansion = enum { expand, no_expand };

/// File name extensions supported natively by `CreateProcess()` on Windows.
pub const WindowsExtension = enum { bat, cmd, com, exe };

pub const SpawnError = error{
    /// The operating system does not support creating child processes.
    OperationUnsupported,
    OutOfMemory,
    /// POSIX-only. `StdIo.ignore` was selected and opening `/dev/null` returned ENODEV.
    NoDevice,
    /// Windows-only. `cwd` or `argv` was provided and it was invalid WTF-8.
    /// https://wtf-8.codeberg.page/
    InvalidWtf8,
    /// Windows-only. NUL (U+0000), LF (U+000A), CR (U+000D) are not allowed
    /// within arguments when executing a `.bat`/`.cmd` script.
    /// - NUL/LF signifiies end of arguments, so anything afterwards
    ///   would be lost after execution.
    /// - CR is stripped by `cmd.exe`, so any CR codepoints
    ///   would be lost after execution.
    InvalidBatchScriptArg,
    SystemResources,
    AccessDenied,
    PermissionDenied,
    InvalidExe,
    FileSystem,
    IsDir,
    FileNotFound,
    NotDir,
    FileBusy,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    ResourceLimitReached,
    InvalidUserId,
    InvalidProcessGroupId,
    SymLinkLoop,
    InvalidName,
    /// An attempt was made to change the process group ID of one of the
    /// children of the calling process and the child had already performed an
    /// image replacement.
    ProcessAlreadyExec,
    /// On Windows, the volume does not contain a recognized file system. File
    /// system drivers might not be loaded, or the volume may be corrupt.
    UnrecognizedVolume,
} || Io.File.OpenError || Io.Dir.PathNameError || Io.Cancelable || Io.UnexpectedError;

pub const SpawnOptions = struct {
    argv: []const []const u8,

    /// Set to change the current working directory when spawning the child process.
    cwd: Child.Cwd = .inherit,
    /// Replaces the child environment when provided. The PATH value from here
    /// is not used to resolve `argv[0]`; that resolution always uses parent
    /// environment.
    environ_map: ?*const Environ.Map = null,
    expand_arg0: ArgExpansion = .no_expand,
    /// When populated, a pipe will be created for the child process to
    /// communicate progress back to the parent. The file descriptor of the
    /// write end of the pipe will be specified in the `ZIG_PROGRESS`
    /// environment variable inside the child process. The progress reported by
    /// the child will be attached to this progress node in the parent process.
    ///
    /// The child's progress tree will be grafted into the parent's progress tree,
    /// by substituting this node with the child's root node.
    progress_node: std.Progress.Node = std.Progress.Node.none,

    stdin: StdIo = .inherit,
    stdout: StdIo = .inherit,
    stderr: StdIo = .inherit,

    /// Set to true to obtain rusage information for the child process.
    /// Depending on the target platform and implementation status, the
    /// requested statistics may or may not be available. If they are
    /// available, then the `resource_usage_statistics` field will be populated
    /// after calling `wait`.
    /// On Linux and Darwin, this obtains rusage statistics from wait4().
    request_resource_usage_statistics: bool = false,

    /// Set to change the user id when spawning the child process.
    uid: ?posix.uid_t = null,
    /// Set to change the group id when spawning the child process.
    gid: ?posix.gid_t = null,
    /// Set to change the process group id when spawning the child process.
    pgid: ?posix.pid_t = null,

    /// Start child process in suspended state.
    /// For Posix systems it's started as if SIGSTOP was sent.
    start_suspended: bool = false,
    /// Windows-only. Sets the CREATE_NO_WINDOW flag in CreateProcess.
    create_no_window: bool = false,
    /// Darwin-only. Disable ASLR for the child process.
    disable_aslr: bool = false,

    /// Behavior of the child process's standard input, output, and error streams.
    pub const StdIo = union(enum) {
        /// Inherit the corresponding stream from the parent process.
        inherit,
        /// Pass an already open file from the parent to the child.
        ///
        /// Nonblocking mode will be kept in the child process if present. This is
        /// likely not supported by the child process. For example:
        /// - Zig's std.Io.File.stdout() assumes blocking mode
        /// - Rust explicity documents that nonblocking stdio may cause panics
        /// - C++ standard streams do not support nonblocking file descriptors
        file: File,
        /// Pass a null stream to the child process by opening "/dev/null" on POSIX
        /// and "NUL" on Windows.
        ignore,
        /// Create a new pipe for the stream.
        ///
        /// The corresponding field (`stdout`, `stderr`, or `stdin`) will be
        /// assigned a `File` object that can be used to read from or write to the
        /// pipe.
        pipe,
        /// Spawn the child process with the corresponding stream missing. This
        /// will likely result in the child encountering EBADF if it tries to use
        /// stdin, stdout, or stderr, or if only one stream is closed, it will
        /// result in them getting mixed up. Generally, this option is for advanced
        /// use cases only.
        close,
    };
};

/// Creates a child process.
///
/// `argv[0]` is the name of the program to execute. If it is not already a
/// file path (i.e. it contains '/'), it is resolved into a file path based on
/// PATH from the parent environment.
pub fn spawn(io: Io, options: SpawnOptions) SpawnError!Child {
    return io.vtable.processSpawn(io.userdata, options);
}

/// Creates a child process.
///
/// `argv[0]` is the file path of the program to execute, relative to `dir`. It
/// is *always* treated as a file path, even if it does not contain '/'.
pub fn spawnPath(io: Io, dir: Io.Dir, options: SpawnOptions) SpawnError!Child {
    return io.vtable.processSpawnPath(io.userdata, dir, options);
}

pub const RunError = error{
    StreamTooLong,
} || SpawnError || Io.File.MultiReader.UnendingError || Io.Timeout.Error;

pub const RunOptions = struct {
    argv: []const []const u8,
    stderr_limit: Io.Limit = .unlimited,
    stdout_limit: Io.Limit = .unlimited,
    /// How many bytes to initially allocate for stderr and stdout.
    reserve_amount: usize = 64,

    /// Set to change the current working directory when spawning the child process.
    cwd: Child.Cwd = .inherit,
    /// Replaces the child environment when provided. The PATH value from here
    /// is not used to resolve `argv[0]`; that resolution always uses parent
    /// environment.
    environ_map: ?*const Environ.Map = null,
    expand_arg0: ArgExpansion = .no_expand,
    /// When populated, a pipe will be created for the child process to
    /// communicate progress back to the parent. The file descriptor of the
    /// write end of the pipe will be specified in the `ZIG_PROGRESS`
    /// environment variable inside the child process. The progress reported by
    /// the child will be attached to this progress node in the parent process.
    ///
    /// The child's progress tree will be grafted into the parent's progress tree,
    /// by substituting this node with the child's root node.
    progress_node: std.Progress.Node = std.Progress.Node.none,
    /// Windows-only. Sets the CREATE_NO_WINDOW flag in CreateProcess.
    create_no_window: bool = true,
    /// Darwin-only. Disable ASLR for the child process.
    disable_aslr: bool = false,
    timeout: Io.Timeout = .none,
};

pub const RunResult = struct {
    term: Child.Term,
    stdout: []u8,
    stderr: []u8,
};

/// Spawns a child process, waits for it, collecting stdout and stderr, and then returns.
/// If it succeeds, the caller owns result.stdout and result.stderr memory.
pub fn run(gpa: Allocator, io: Io, options: RunOptions) RunError!RunResult {
    var child = try spawn(io, .{
        .argv = options.argv,
        .cwd = options.cwd,
        .environ_map = options.environ_map,
        .expand_arg0 = options.expand_arg0,
        .progress_node = options.progress_node,
        .create_no_window = options.create_no_window,
        .disable_aslr = options.disable_aslr,

        .stdin = .ignore,
        .stdout = .pipe,
        .stderr = .pipe,
    });
    defer child.kill(io);

    var multi_reader_buffer: Io.File.MultiReader.Buffer(2) = undefined;
    var multi_reader: Io.File.MultiReader = undefined;
    multi_reader.init(gpa, io, multi_reader_buffer.toStreams(), &.{ child.stdout.?, child.stderr.? });
    defer multi_reader.deinit();

    const stdout_reader = multi_reader.reader(0);
    const stderr_reader = multi_reader.reader(1);

    while (multi_reader.fill(options.reserve_amount, options.timeout)) |_| {
        if (options.stdout_limit.toInt()) |limit| {
            if (stdout_reader.buffered().len > limit)
                return error.StreamTooLong;
        }
        if (options.stderr_limit.toInt()) |limit| {
            if (stderr_reader.buffered().len > limit)
                return error.StreamTooLong;
        }
    } else |err| switch (err) {
        error.EndOfStream => {},
        else => |e| return e,
    }

    try multi_reader.checkAnyError();

    const term = try child.wait(io);

    const stdout_slice = try multi_reader.toOwnedSlice(0);
    errdefer gpa.free(stdout_slice);

    const stderr_slice = try multi_reader.toOwnedSlice(1);
    errdefer gpa.free(stderr_slice);

    return .{
        .stdout = stdout_slice,
        .stderr = stderr_slice,
        .term = term,
    };
}

pub const TotalSystemMemoryError = error{
    UnknownTotalSystemMemory,
};

/// Returns the total system memory, in bytes as a u64.
/// We return a u64 instead of usize due to PAE on ARM
/// and Linux's /proc/meminfo reporting more memory when
/// using QEMU user mode emulation.
pub fn totalSystemMemory() TotalSystemMemoryError!u64 {
    switch (native_os) {
        .linux => {
            var info: std.os.linux.Sysinfo = undefined;
            const result: usize = std.os.linux.sysinfo(&info);
            if (std.os.linux.errno(result) != .SUCCESS) {
                return error.UnknownTotalSystemMemory;
            }
            // Promote to u64 to avoid overflow on systems where info.totalram is a 32-bit usize
            return @as(u64, info.totalram) * info.mem_unit;
        },
        .dragonfly, .freebsd, .netbsd => {
            const name = if (native_os == .netbsd) "hw.physmem64" else "hw.physmem";
            var physmem: c_ulong = undefined;
            var len: usize = @sizeOf(c_ulong);
            switch (posix.errno(posix.system.sysctlbyname(name, &physmem, &len, null, 0))) {
                .SUCCESS => return @intCast(physmem),
                .FAULT => unreachable,
                .PERM => unreachable, // only when setting values
                .NOMEM => unreachable, // memory already on the stack
                .NOENT => unreachable,
                else => return error.UnknownTotalSystemMemory,
            }
        },
        // whole Darwin family
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
            // "hw.memsize" returns uint64_t
            var physmem: u64 = undefined;
            var len: usize = @sizeOf(u64);
            switch (posix.errno(posix.system.sysctlbyname("hw.memsize", &physmem, &len, null, 0))) {
                .SUCCESS => return physmem,
                .FAULT => unreachable,
                .PERM => unreachable, // only when setting values
                .NOMEM => unreachable, // memory already on the stack
                .NOENT => unreachable, // constant, known good value
                else => return error.UnknownTotalSystemMemory,
            }
        },
        .openbsd => {
            const mib: [2]c_int = [_]c_int{
                posix.CTL.HW,
                posix.HW.PHYSMEM64,
            };
            var physmem: i64 = undefined;
            var len: usize = @sizeOf(@TypeOf(physmem));
            posix.sysctl(&mib, &physmem, &len, null, 0) catch |err| switch (err) {
                error.NameTooLong => unreachable, // constant, known good value
                error.PermissionDenied => unreachable, // only when setting values,
                error.SystemResources => unreachable, // memory already on the stack
                error.UnknownName => unreachable, // constant, known good value
                else => return error.UnknownTotalSystemMemory,
            };
            assert(physmem >= 0);
            return @as(u64, @bitCast(physmem));
        },
        .windows => {
            var sbi: windows.SYSTEM.BASIC_INFORMATION = undefined;
            const rc = windows.ntdll.NtQuerySystemInformation(
                .Basic,
                &sbi,
                @sizeOf(windows.SYSTEM.BASIC_INFORMATION),
                null,
            );
            if (rc != .SUCCESS) {
                return error.UnknownTotalSystemMemory;
            }
            return @as(u64, sbi.NumberOfPhysicalPages) * sbi.PageSize;
        },
        else => return error.UnknownTotalSystemMemory,
    }
}

/// Indicate intent to terminate with a successful exit code.
///
/// In debug builds, this is a no-op, so that the calling code's cleanup
/// mechanisms are tested and so that external tools checking for resource
/// leaks can be accurate. In release builds, this calls `exit` with code zero,
/// and does not return.
pub fn cleanExit(io: Io) void {
    if (builtin.mode == .Debug) return;
    _ = io.lockStderr(&.{}, .no_color) catch {};
    exit(0);
}

/// Request ability to have more open file descriptors simultaneously.
///
/// On some systems, this raises the limit before seeing ProcessFdQuotaExceeded
/// errors. On other systems, this does nothing.
pub fn raiseFileDescriptorLimit() void {
    const have_rlimit = posix.rlimit_resource != void;
    if (!have_rlimit) return;

    var lim = posix.getrlimit(.NOFILE) catch return; // Oh well; we tried.
    if (native_os.isDarwin()) {
        // On Darwin, `NOFILE` is bounded by a hardcoded value `OPEN_MAX`.
        // According to the man pages for setrlimit():
        //   setrlimit() now returns with errno set to EINVAL in places that historically succeeded.
        //   It no longer accepts "rlim_cur = RLIM.INFINITY" for RLIM.NOFILE.
        //   Use "rlim_cur = min(OPEN_MAX, rlim_max)".
        lim.max = @min(std.c.OPEN_MAX, lim.max);
    }
    if (lim.cur == lim.max) return;

    // Do a binary search for the limit.
    var min: posix.rlim_t = lim.cur;
    var max: posix.rlim_t = 1 << 20;
    // But if there's a defined upper bound, don't search, just set it.
    if (lim.max != posix.RLIM.INFINITY) {
        min = lim.max;
        max = lim.max;
    }

    while (true) {
        lim.cur = min + @divTrunc(max - min, 2); // on freebsd rlim_t is signed
        if (posix.setrlimit(.NOFILE, lim)) |_| {
            min = lim.cur;
        } else |_| {
            max = lim.cur;
        }
        if (min + 1 >= max) break;
    }
}

test raiseFileDescriptorLimit {
    raiseFileDescriptorLimit();
}

/// Logs an error and then terminates the process with exit code 1.
pub fn fatal(comptime format: []const u8, format_arguments: anytype) noreturn {
    std.log.err(format, format_arguments);
    exit(1);
}

pub const ExecutablePathBaseError = error{
    FileNotFound,
    AccessDenied,
    /// The operating system does not support an executable learning its own
    /// path.
    OperationUnsupported,
    NotDir,
    SymLinkLoop,
    InputOutput,
    FileTooBig,
    IsDir,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    NoDevice,
    SystemResources,
    NoSpaceLeft,
    FileSystem,
    BadPathName,
    DeviceBusy,
    PipeBusy,
    NotLink,
    PathAlreadyExists,
    /// On Windows, `\\server` or `\\server\share` was not found.
    NetworkNotFound,
    ProcessNotFound,
    /// On Windows, antivirus software is enabled by default. It can be
    /// disabled, but Windows Update sometimes ignores the user's preference
    /// and re-enables it. When enabled, antivirus software on Windows
    /// intercepts file system operations and makes them significantly slower
    /// in addition to possibly failing with this error code.
    AntivirusInterference,
    /// On Windows, the volume does not contain a recognized file system. File
    /// system drivers might not be loaded, or the volume may be corrupt.
    UnrecognizedVolume,
    PermissionDenied,
} || Io.Cancelable || Io.UnexpectedError;

pub const ExecutablePathAllocError = ExecutablePathBaseError || Allocator.Error;

pub fn executablePathAlloc(io: Io, allocator: Allocator) ExecutablePathAllocError![:0]u8 {
    var buffer: [max_path_bytes]u8 = undefined;
    const n = executablePath(io, &buffer) catch |err| switch (err) {
        error.NameTooLong => unreachable,
        else => |e| return e,
    };
    return allocator.dupeZ(u8, buffer[0..n]);
}

pub const ExecutablePathError = ExecutablePathBaseError || error{NameTooLong};

/// Get the path to the current executable, following symlinks.
///
/// This function may return an error if the current executable
/// was deleted after spawning.
///
/// Returned value is a slice of out_buffer.
///
/// On Windows, the result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On other platforms, the result is an opaque sequence of bytes with no particular encoding.
///
/// On Linux, depends on procfs being mounted. If the currently executing binary has
/// been deleted, the file path looks something like "/a/b/c/exe (deleted)".
///
/// See also:
/// * `executableDirPath` - to obtain only the directory
/// * `openExecutable` - to obtain only an open file handle
pub fn executablePath(io: Io, out_buffer: []u8) ExecutablePathError!usize {
    return io.vtable.processExecutablePath(io.userdata, out_buffer);
}

/// Get the directory path that contains the current executable.
///
/// Returns index into `out_buffer`.
///
/// On Windows, the result is encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On other platforms, the result is an opaque sequence of bytes with no particular encoding.
pub fn executableDirPath(io: Io, out_buffer: []u8) ExecutablePathError!usize {
    const n = try executablePath(io, out_buffer);
    // Assert that the OS APIs return absolute paths, and therefore dirname
    // will not return null.
    return std.fs.path.dirname(out_buffer[0..n]).?.len;
}

/// Same as `executableDirPath` except allocates the result.
pub fn executableDirPathAlloc(io: Io, allocator: Allocator) ExecutablePathAllocError![]u8 {
    var buffer: [max_path_bytes]u8 = undefined;
    const dir_path_len = executableDirPath(io, &buffer) catch |err| switch (err) {
        error.NameTooLong => unreachable,
        else => |e| return e,
    };
    return allocator.dupe(u8, buffer[0..dir_path_len]);
}

pub const OpenExecutableError = File.OpenError || ExecutablePathError || File.LockError;

pub fn openExecutable(io: Io, flags: File.OpenFlags) OpenExecutableError!File {
    return io.vtable.processExecutableOpen(io.userdata, flags);
}

/// Causes abnormal process termination.
///
/// If linking against libc, this calls `std.c.abort`. Otherwise it raises
/// SIGABRT followed by SIGKILL.
///
/// Invokes the current signal handler for SIGABRT, if any.
pub fn abort() noreturn {
    @branchHint(.cold);
    // MSVCRT abort() sometimes opens a popup window which is undesirable, so
    // even when linking libc on Windows we use our own abort implementation.
    // See https://github.com/ziglang/zig/issues/2071 for more details.
    if (native_os == .windows) {
        if (builtin.mode == .Debug and windows.peb().BeingDebugged.toBool()) {
            @breakpoint();
        }
        windows.ntdll.RtlExitUserProcess(3);
    }
    if (!builtin.link_libc and native_os == .linux) {
        // The Linux man page says that the libc abort() function
        // "first unblocks the SIGABRT signal", but this is a footgun
        // for user-defined signal handlers that want to restore some state in
        // some program sections and crash in others.
        // So, the user-installed SIGABRT handler is run, if present.
        posix.raise(.ABRT) catch {};

        // Disable all signal handlers.
        const filledset = std.os.linux.sigfillset();
        posix.sigprocmask(posix.SIG.BLOCK, &filledset, null);

        // Only one thread may proceed to the rest of abort().
        if (!builtin.single_threaded) {
            const global = struct {
                var abort_entered: bool = false;
            };
            while (@cmpxchgWeak(bool, &global.abort_entered, false, true, .seq_cst, .seq_cst)) |_| {}
        }

        // Install default handler so that the tkill below will terminate.
        const sigact: posix.Sigaction = .{
            .handler = .{ .handler = posix.SIG.DFL },
            .mask = posix.sigemptyset(),
            .flags = 0,
        };
        posix.sigaction(.ABRT, &sigact, null);

        _ = std.os.linux.tkill(std.os.linux.gettid(), .ABRT);

        var sigabrtmask = posix.sigemptyset();
        posix.sigaddset(&sigabrtmask, .ABRT);
        posix.sigprocmask(posix.SIG.UNBLOCK, &sigabrtmask, null);

        // Beyond this point should be unreachable.
        @as(*allowzero volatile u8, @ptrFromInt(0)).* = 0;
        posix.raise(.KILL) catch {};
        exit(127); // Pid 1 might not be signalled in some containers.
    }
    switch (native_os) {
        .uefi, .wasi, .emscripten, .cuda, .amdhsa => @trap(),
        else => posix.system.abort(),
    }
}

/// Exits all threads of the program with the specified status code.
pub fn exit(status: u8) noreturn {
    if (builtin.link_libc) {
        std.c.exit(status);
    } else switch (native_os) {
        .windows => windows.ntdll.RtlExitUserProcess(status),
        .wasi => std.os.wasi.proc_exit(status),
        .linux => {
            if (!builtin.single_threaded) std.os.linux.exit_group(status);
            posix.system.exit(status);
        },
        .uefi => {
            const uefi = std.os.uefi;
            // exit() is only available if exitBootServices() has not been called yet.
            // This call to exit should not fail, so we catch-ignore errors.
            if (uefi.system_table.boot_services) |bs| {
                bs.exit(uefi.handle, @enumFromInt(status), null) catch {};
            }
            // If we can't exit, reboot the system instead.
            uefi.system_table.runtime_services.resetSystem(.cold, @enumFromInt(status), null);
        },
        else => posix.system.exit(status),
    }
}

pub const SetCurrentDirError = error{
    AccessDenied,
    BadPathName,
    FileNotFound,
    FileSystem,
    NameTooLong,
    NoDevice,
    NotDir,
    OperationUnsupported,
    UnrecognizedVolume,
} || Io.Cancelable || Io.UnexpectedError;

/// Changes the current working directory to the open directory handle.
/// Corresponds to "fchdir" in libc.
///
/// This modifies global process state and can have surprising effects in
/// multithreaded applications. Most applications and especially libraries
/// should not call this function as a general rule, however it can have use
/// cases in, for example, implementing a shell, or child process execution.
///
/// Calling this function makes code less portable and less reusable.
pub fn setCurrentDir(io: Io, dir: Io.Dir) !void {
    return io.vtable.processSetCurrentDir(io.userdata, dir);
}

pub const SetCurrentPathError = error{
    AccessDenied,
    SymLinkLoop,
    SystemResources,
    BadPathName,
    FileNotFound,
    FileSystem,
    NoDevice,
    NotDir,
    NameTooLong,
    OperationUnsupported,
    /// Windows-only. The path is invalid WTF-8.
    /// https://wtf-8.codeberg.page/
    InvalidWtf8,
} || Io.Cancelable || Io.UnexpectedError;

/// Changes the current working directory to the given path.
/// Corresponds to "chdir" in libc.
///
/// This modifies global process state and can have surprising effects in
/// multithreaded applications. Most applications and especially libraries
/// should not call this function as a general rule, however it can have use
/// cases in, for example, implementing a shell, or child process execution.
///
/// Calling this function makes code less portable and less reusable.
pub fn setCurrentPath(io: Io, path: []const u8) !void {
    return io.vtable.processSetCurrentPath(io.userdata, path);
}

pub const LockMemoryError = error{
    UnsupportedOperation,
    PermissionDenied,
    LockedMemoryLimitExceeded,
    SystemResources,
} || Io.UnexpectedError;

pub const LockMemoryOptions = struct {
    /// Lock pages that are currently resident and mark the entire range so
    /// that the remaining nonresident pages are locked when they are populated
    /// by a page fault.
    on_fault: bool = false,
};

/// Request part of the calling process's virtual address space to be in RAM,
/// preventing that memory from being paged to the swap area.
///
/// Corresponds to "mlock" or "mlock2" in libc.
///
/// See also:
/// * unlockMemory
pub fn lockMemory(memory: []align(std.heap.page_size_min) const u8, options: LockMemoryOptions) LockMemoryError!void {
    if (native_os == .windows) {
        // TODO call VirtualLock
    }
    if (!options.on_fault and @TypeOf(posix.system.mlock) != void) {
        switch (posix.errno(posix.system.mlock(memory.ptr, memory.len))) {
            .SUCCESS => return,
            .INVAL => |err| return std.Io.Threaded.errnoBug(err), // unaligned, negative, runs off end of addrspace
            .PERM => return error.PermissionDenied,
            .NOMEM => return error.LockedMemoryLimitExceeded,
            .AGAIN => return error.SystemResources,
            else => |err| return posix.unexpectedErrno(err),
        }
    }
    if (@TypeOf(posix.system.mlock2) != void) {
        const flags: po
```
