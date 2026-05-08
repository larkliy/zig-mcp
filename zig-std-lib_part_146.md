```
             // Choose the shorter part to copy.
                    const head = new_buffer[deque.head..old_buffer.len];
                    const tail = new_buffer[0 .. deque.len - head.len];
                    if (head.len > tail.len and new_buffer.len - old_buffer.len > tail.len) {
                        @memcpy(new_buffer[old_buffer.len..][0..tail.len], tail);
                    } else {
                        // In this case overlap is possible if e.g. the capacity increase is 1
                        // and head.len is greater than 1.
                        deque.head = new_buffer.len - head.len;
                        @memmove(new_buffer[deque.head..][0..head.len], head);
                    }
                }
                deque.buffer = new_buffer;
            } else {
                const new_buffer = try gpa.alloc(T, new_capacity);
                if (deque.head < old_buffer.len - deque.len) {
                    @memcpy(new_buffer[0..deque.len], old_buffer[deque.head..][0..deque.len]);
                } else {
                    const head = old_buffer[deque.head..];
                    const tail = old_buffer[0 .. deque.len - head.len];
                    @memcpy(new_buffer[0..head.len], head);
                    @memcpy(new_buffer[head.len..][0..tail.len], tail);
                }
                deque.head = 0;
                deque.buffer = new_buffer;
                gpa.free(old_buffer);
            }
        }

        /// Modify the deque so that it can hold at least `additional_count` **more** items.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureUnusedCapacity(
            deque: *Self,
            gpa: Allocator,
            additional_count: usize,
        ) Allocator.Error!void {
            return deque.ensureTotalCapacity(gpa, try addOrOom(deque.len, additional_count));
        }

        /// Add one item to the front of the deque.
        ///
        /// Invalidates element pointers if additional memory is needed.
        pub fn pushFront(deque: *Self, gpa: Allocator, item: T) error{OutOfMemory}!void {
            try deque.ensureUnusedCapacity(gpa, 1);
            deque.pushFrontAssumeCapacity(item);
        }

        /// Add one item to the front of the deque.
        ///
        /// Never invalidates element pointers.
        ///
        /// If the deque lacks unused capacity for the additional item, returns
        /// `error.OutOfMemory`.
        pub fn pushFrontBounded(deque: *Self, item: T) error{OutOfMemory}!void {
            if (deque.buffer.len - deque.len == 0) return error.OutOfMemory;
            return deque.pushFrontAssumeCapacity(item);
        }

        /// Add one item to the front of the deque.
        ///
        /// Never invalidates element pointers.
        ///
        /// Asserts that the deque can hold one additional item.
        pub fn pushFrontAssumeCapacity(deque: *Self, item: T) void {
            assert(deque.len < deque.buffer.len);
            if (deque.head == 0) {
                deque.head = deque.buffer.len;
            }
            deque.head -= 1;
            deque.buffer[deque.head] = item;
            deque.len += 1;
        }

        /// Add one item to the back of the deque.
        ///
        /// Invalidates element pointers if additional memory is needed.
        pub fn pushBack(deque: *Self, gpa: Allocator, item: T) error{OutOfMemory}!void {
            try deque.ensureUnusedCapacity(gpa, 1);
            deque.pushBackAssumeCapacity(item);
        }

        /// Add one item to the back of the deque.
        ///
        /// Never invalidates element pointers.
        ///
        /// If the deque lacks unused capacity for the additional item, returns
        /// `error.OutOfMemory`.
        pub fn pushBackBounded(deque: *Self, item: T) error{OutOfMemory}!void {
            if (deque.buffer.len - deque.len == 0) return error.OutOfMemory;
            deque.pushBackAssumeCapacity(item);
        }

        /// Add one item to the back of the deque.
        ///
        /// Never invalidates element pointers.
        ///
        /// Asserts that the deque can hold one additional item.
        pub fn pushBackAssumeCapacity(deque: *Self, item: T) void {
            assert(deque.len < deque.buffer.len);
            const buffer_index = deque.bufferIndex(deque.len);
            deque.buffer[buffer_index] = item;
            deque.len += 1;
        }

        /// Add `items` to the front of the deque.
        /// This is equivalent to iterating `items` in reverse and calling
        /// `pushFront` on every single entry.
        ///
        /// Invalidates element pointers if additional memory is needed.
        pub fn pushFrontSlice(deque: *Self, gpa: Allocator, items: []const T) error{OutOfMemory}!void {
            try deque.ensureUnusedCapacity(gpa, items.len);
            return deque.pushFrontSliceAssumeCapacity(items);
        }

        /// Add `items` to the front of the deque.
        /// This is equivalent to iterating `items` in reverse and calling
        /// `pushFront` on every single entry.
        ///
        /// Never invalidates element pointers.
        ///
        /// If the deque lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        pub fn pushFrontSliceBounded(deque: *Self, items: []const T) error{OutOfMemory}!void {
            if (deque.buffer.len - deque.len < items.len) return error.OutOfMemory;
            return deque.pushFrontSliceAssumeCapacity(items);
        }

        /// Add `items` to the front of the deque.
        /// This is equivalent to iterating `items` in reverse and calling
        /// `pushFront` on every single entry.
        ///
        /// Never invalidates element pointers.
        ///
        /// Asserts that the deque can hold the additional items.
        pub fn pushFrontSliceAssumeCapacity(deque: *Self, items: []const T) void {
            assert(deque.buffer.len - deque.len >= items.len);
            if (deque.head < items.len) {
                @memcpy(deque.buffer[0..deque.head], items[items.len - deque.head ..]);
                deque.head = deque.buffer.len - items.len + deque.head;
                @memcpy(deque.buffer[deque.head..], items.ptr);
            } else {
                deque.head -= items.len;
                @memcpy(deque.buffer[deque.head..][0..items.len], items);
            }
            deque.len += items.len;
        }

        /// Add `items` to the back of the deque.
        /// This is equivalent to iterating `items` in order and calling
        /// `pushBack` on every single entry.
        ///
        /// Invalidates element pointers if additional memory is needed.
        pub fn pushBackSlice(deque: *Self, gpa: Allocator, items: []const T) error{OutOfMemory}!void {
            try deque.ensureUnusedCapacity(gpa, items.len);
            return deque.pushBackSliceAssumeCapacity(items);
        }

        /// Add `items` to the back of the deque.
        /// This is equivalent to iterating `items` in order and calling
        /// `pushBack` on every single entry.
        ///
        /// Never invalidates element pointers.
        ///
        /// If the deque lacks unused capacity for the additional items, returns
        /// `error.OutOfMemory`.
        pub fn pushBackSliceBounded(deque: *Self, items: []const T) error{OutOfMemory}!void {
            if (deque.buffer.len - deque.len < items.len) return error.OutOfMemory;
            return deque.pushBackSliceAssumeCapacity(items);
        }

        /// Add `items` to the back of the deque.
        /// This is equivalent to iterating `items` in order and calling
        /// `pushBack` on every single entry.
        ///
        /// Never invalidates element pointers.
        ///
        /// Asserts that the deque can hold the additional items.
        pub fn pushBackSliceAssumeCapacity(deque: *Self, items: []const T) void {
            assert(deque.buffer.len - deque.len >= items.len);
            const trailing_buffer = deque.buffer[deque.bufferIndex(deque.len)..];
            if (trailing_buffer.len < items.len) {
                @memcpy(trailing_buffer, items[0..trailing_buffer.len]);
                @memcpy(deque.buffer.ptr, items[trailing_buffer.len..]);
            } else {
                @memcpy(trailing_buffer[0..items.len], items);
            }
            deque.len += items.len;
        }

        /// Return the first item in the deque or null if empty.
        pub fn front(deque: *const Self) ?T {
            if (deque.len == 0) return null;
            return deque.buffer[deque.head];
        }

        /// Return pointer to the first item in the deque or null if empty.
        pub fn frontPtr(deque: *const Self) ?*T {
            if (deque.len == 0) return null;
            return &deque.buffer[deque.head];
        }

        /// Return the last item in the deque or null if empty.
        pub fn back(deque: *const Self) ?T {
            if (deque.len == 0) return null;
            return deque.buffer[deque.bufferIndex(deque.len - 1)];
        }

        /// Return the last item in the deque or null if empty.
        pub fn backPtr(deque: *const Self) ?*T {
            if (deque.len == 0) return null;
            return &deque.buffer[deque.bufferIndex(deque.len - 1)];
        }

        /// Return the item at the given index in the deque.
        ///
        /// The first item in the queue is at index 0.
        ///
        /// Asserts that the index is in-bounds.
        pub fn at(deque: *const Self, index: usize) T {
            assert(index < deque.len);
            return deque.buffer[deque.bufferIndex(index)];
        }

        /// Return pointer to the item at the given index in the deque.
        ///
        /// The first item in the queue is at index 0.
        ///
        /// Asserts that the index is in-bounds.
        pub fn atPtr(deque: *const Self, index: usize) *T {
            assert(index < deque.len);
            return &deque.buffer[deque.bufferIndex(index)];
        }

        /// Remove and return the first item in the deque or null if empty.
        pub fn popFront(deque: *Self) ?T {
            if (deque.len == 0) return null;
            const pop_index = deque.head;
            deque.head = deque.bufferIndex(1);
            deque.len -= 1;
            return deque.buffer[pop_index];
        }

        /// Remove and return the last item in the deque or null if empty.
        pub fn popBack(deque: *Self) ?T {
            if (deque.len == 0) return null;
            deque.len -= 1;
            return deque.buffer[deque.bufferIndex(deque.len)];
        }

        pub const Iterator = struct {
            deque: *const Self,
            index: usize,

            pub fn peek(it: Iterator) ?T {
                if (it.index >= it.deque.len) return null;
                return it.deque.at(it.index);
            }
            pub fn next(it: *Iterator) ?T {
                const item = it.peek() orelse return null;
                it.index += 1;
                return item;
            }

            pub fn peekPtr(it: Iterator) ?*T {
                if (it.index >= it.deque.len) return null;
                return it.deque.atPtr(it.index);
            }
            pub fn nextPtr(it: *Iterator) ?*T {
                const item_ptr = it.peekPtr() orelse return null;
                it.index += 1;
                return item_ptr;
            }
        };

        /// Iterates over all items in the deque in order from front to back.
        pub fn iterator(deque: *const Self) Iterator {
            return .{ .deque = deque, .index = 0 };
        }

        /// Returns the index in `buffer` where the element at the given
        /// index in the logical deque is stored.
        fn bufferIndex(deque: *const Self, index: usize) usize {
            // This function is written in this way to avoid overflow and
            // expensive division.
            const head_len = deque.buffer.len - deque.head;
            if (index < head_len) {
                return deque.head + index;
            } else {
                return index - head_len;
            }
        }
    };
}

/// Integer addition returning `error.OutOfMemory` on overflow.
fn addOrOom(a: usize, b: usize) error{OutOfMemory}!usize {
    const result, const overflow = @addWithOverflow(a, b);
    if (overflow != 0) return error.OutOfMemory;
    return result;
}

test "basic" {
    const testing = std.testing;
    const gpa = testing.allocator;

    var q: Deque(u32) = .empty;
    defer q.deinit(gpa);

    try testing.expectEqual(null, q.popFront());
    try testing.expectEqual(null, q.popBack());

    try q.pushBack(gpa, 1);
    try q.pushBack(gpa, 2);
    try q.pushBack(gpa, 3);
    try q.pushFront(gpa, 0);

    try testing.expectEqual(0, q.popFront());
    try testing.expectEqual(1, q.popFront());
    try testing.expectEqual(3, q.popBack());
    try testing.expectEqual(2, q.popFront());
    try testing.expectEqual(null, q.popFront());
    try testing.expectEqual(null, q.popBack());
}

test "buffer" {
    const testing = std.testing;

    var buffer: [4]u32 = undefined;
    var q: Deque(u32) = .initBuffer(&buffer);

    try testing.expectEqual(null, q.popFront());
    try testing.expectEqual(null, q.popBack());

    try q.pushBackBounded(1);
    try q.pushBackBounded(2);
    try q.pushBackBounded(3);
    try q.pushFrontBounded(0);
    try testing.expectError(error.OutOfMemory, q.pushBackBounded(4));

    try testing.expectEqual(0, q.popFront());
    try testing.expectEqual(1, q.popFront());
    try testing.expectEqual(3, q.popBack());
    try testing.expectEqual(2, q.popFront());
    try testing.expectEqual(null, q.popFront());
    try testing.expectEqual(null, q.popBack());
}

test "slow growth" {
    const testing = std.testing;
    const gpa = testing.allocator;

    var q: Deque(i32) = .empty;
    defer q.deinit(gpa);

    try q.ensureTotalCapacityPrecise(gpa, 1);
    q.pushBackAssumeCapacity(1);
    try q.ensureTotalCapacityPrecise(gpa, 2);
    q.pushFrontAssumeCapacity(0);
    try q.ensureTotalCapacityPrecise(gpa, 3);
    q.pushBackAssumeCapacity(2);
    try q.ensureTotalCapacityPrecise(gpa, 5);
    q.pushBackAssumeCapacity(3);
    q.pushFrontAssumeCapacity(-1);
    try q.ensureTotalCapacityPrecise(gpa, 6);
    q.pushFrontAssumeCapacity(-2);

    try testing.expectEqual(-2, q.popFront());
    try testing.expectEqual(-1, q.popFront());
    try testing.expectEqual(3, q.popBack());
    try testing.expectEqual(0, q.popFront());
    try testing.expectEqual(2, q.popBack());
    try testing.expectEqual(1, q.popBack());
    try testing.expectEqual(null, q.popFront());
    try testing.expectEqual(null, q.popBack());
}

test "slice" {
    const testing = std.testing;
    const gpa = testing.allocator;

    var q: Deque(i32) = .empty;
    defer q.deinit(gpa);

    try q.pushBackSlice(gpa, &.{ 3, 4, 5 });
    try q.pushBackSlice(gpa, &.{ 6, 7 });
    try q.pushFrontSlice(gpa, &.{2});
    try q.pushBackSlice(gpa, &.{});
    try q.pushFrontSlice(gpa, &.{ 0, 1 });
    try q.pushFrontSlice(gpa, &.{});

    try testing.expectEqual(0, q.popFront());
    try testing.expectEqual(1, q.popFront());
    try testing.expectEqual(7, q.popBack());
    try testing.expectEqual(6, q.popBack());

    try q.pushFrontSlice(gpa, &.{ 0, 1 });
    try q.pushBackSlice(gpa, &.{ 6, 7 });

    try testing.expectEqual(0, q.popFront());
    try testing.expectEqual(1, q.popFront());
    try testing.expectEqual(2, q.popFront());
    try testing.expectEqual(7, q.popBack());
    try testing.expectEqual(6, q.popBack());
    try testing.expectEqual(3, q.popFront());
    try testing.expectEqual(4, q.popFront());
    try testing.expectEqual(5, q.popBack());
    try testing.expectEqual(null, q.popFront());
    try testing.expectEqual(null, q.popBack());
}

test "iterator" {
    const testing = std.testing;
    const gpa = testing.allocator;

    var q: Deque(i32) = .empty;
    defer q.deinit(gpa);

    const items: []const i32 = &.{ 0, 1, 2, 3, 4, 5 };
    try q.pushFrontSlice(gpa, items);

    {
        var it = q.iterator();
        for (items) |item| {
            try testing.expectEqual(item, it.peek());
            try testing.expectEqual(item, it.next());
        }
        try testing.expectEqual(null, it.peek());
        try testing.expectEqual(null, it.next());
    }
    {
        var it = q.iterator();
        for (items) |item| {
            if (it.peekPtr()) |ptr| {
                try testing.expectEqual(item, ptr.*);
            } else return error.TestExpectedNonNull;
            if (it.nextPtr()) |ptr| {
                try testing.expectEqual(item, ptr.*);
            } else return error.TestExpectedNonNull;
        }
        try testing.expectEqual(null, it.peekPtr());
        try testing.expectEqual(null, it.nextPtr());
    }
}

test "fuzz against ArrayList oracle" {
    try std.testing.fuzz({}, fuzzAgainstArrayList, .{});
}

const FuzzAllocator = struct {
    smith: *std.testing.Smith,
    bufs: [2][256 * 4]u8 align(4),
    used_bitmap: u2,
    used_len: [2]usize,

    pub fn init(smith: *std.testing.Smith) FuzzAllocator {
        return .{
            .smith = smith,
            .bufs = undefined,
            .used_len = undefined,
            .used_bitmap = 0,
        };
    }

    pub fn allocator(f: *FuzzAllocator) std.mem.Allocator {
        return .{
            .ptr = f,
            .vtable = &.{
                .alloc = alloc,
                .resize = resize,
                .remap = remap,
                .free = free,
            },
        };
    }

    pub fn allocCount(f: *FuzzAllocator) u2 {
        return @popCount(f.used_bitmap);
    }

    fn alloc(ctx: *anyopaque, len: usize, a: std.mem.Alignment, _: usize) ?[*]u8 {
        const f: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        assert(a == .@"4");
        assert(len % 4 == 0);

        const slot: u1 = @intCast(@ctz(~f.used_bitmap));
        const buf: []u8 = &f.bufs[slot];
        if (len > buf.len) return null;
        f.used_bitmap |= @as(u2, 1) << slot;
        f.used_len[slot] = len;
        return buf.ptr;
    }

    fn memSlot(f: *FuzzAllocator, mem: []u8) u1 {
        const slot: u1 = if (&mem[0] == &f.bufs[0][0])
            0
        else if (&mem[0] == &f.bufs[1][0])
            1
        else
            unreachable;
        assert((f.used_bitmap >> slot) & 1 == 1);
        assert(mem.len == f.used_len[slot]);
        return slot;
    }

    fn resize(ctx: *anyopaque, mem: []u8, a: std.mem.Alignment, new_len: usize, _: usize) bool {
        const f: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        assert(a == .@"4");
        assert(f.allocCount() == 1);

        const slot = f.memSlot(mem);
        if (new_len > f.bufs[slot].len or f.smith.value(bool)) return false;
        f.used_len[slot] = new_len;
        return true;
    }

    fn remap(ctx: *anyopaque, mem: []u8, a: std.mem.Alignment, new_len: usize, _: usize) ?[*]u8 {
        const f: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        assert(a == .@"4");
        assert(f.allocCount() == 1);

        const slot = f.memSlot(mem);
        if (new_len > f.bufs[slot].len or f.smith.value(bool)) return null;

        if (f.smith.value(bool)) {
            f.used_len[slot] = new_len;
            // remap in place
            return mem.ptr;
        } else {
            // moving remap
            const new_slot = ~slot;
            f.used_bitmap = ~f.used_bitmap;
            f.used_len[new_slot] = new_len;

            const new_buf = &f.bufs[new_slot];
            @memcpy(new_buf[0..mem.len], mem);
            return new_buf.ptr;
        }
    }

    fn free(ctx: *anyopaque, mem: []u8, a: std.mem.Alignment, _: usize) void {
        const f: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        assert(a == .@"4");
        f.used_bitmap ^= @as(u2, 1) << f.memSlot(mem);
    }
};

fn fuzzAgainstArrayList(_: void, smith: *std.testing.Smith) anyerror!void {
    const testing = std.testing;

    var q_gpa_inst: FuzzAllocator = .init(smith);
    var l_gpa_buf: [q_gpa_inst.bufs[0].len]u8 align(4) = undefined;
    var l_gpa_inst: std.heap.FixedBufferAllocator = .init(&l_gpa_buf);
    const q_gpa = q_gpa_inst.allocator();
    const l_gpa = l_gpa_inst.allocator();

    var q: Deque(u32) = .empty;
    var l: std.ArrayList(u32) = .empty;

    const Action = enum(u8) {
        grow,
        push_back,
        push_front,
        push_back_slice,
        push_front_slice,
        pop_back,
        pop_front,
    };

    while (!smith.eosWeightedSimple(15, 1)) {
        const baseline = testing.Smith.baselineWeights(Action);
        const grow_weight: testing.Smith.Weight = .value(Action, .grow, 3);
        switch (smith.valueWeighted(Action, baseline ++ .{grow_weight})) {
            .push_back => {
                const item = smith.value(u32);
                try testing.expectEqual(
                    l.appendBounded(item),
                    q.pushBackBounded(item),
                );
            },
            .push_front => {
                const item = smith.value(u32);
                try testing.expectEqual(
                    l.insertBounded(0, item),
                    q.pushFrontBounded(item),
                );
            },
            .push_back_slice => {
                var buffer: [std.math.maxInt(u3)]u32 = undefined;
                const items = buffer[0..smith.value(u3)];
                for (items) |*item| {
                    item.* = smith.value(u32);
                }
                try testing.expectEqual(
                    l.appendSliceBounded(items),
                    q.pushBackSliceBounded(items),
                );
            },
            .push_front_slice => {
                var buffer: [std.math.maxInt(u3)]u32 = undefined;
                const items = buffer[0..smith.value(u3)];
                for (items) |*item| {
                    item.* = smith.value(u32);
                }
                try testing.expectEqual(
                    l.insertSliceBounded(0, items),
                    q.pushFrontSliceBounded(items),
                );
            },
            .pop_back => {
                try testing.expectEqual(l.pop(), q.popBack());
            },
            .pop_front => {
                try testing.expectEqual(
                    if (l.items.len > 0) l.orderedRemove(0) else null,
                    q.popFront(),
                );
            },
            // Growing by small, random, linear amounts seems to better test
            // ensureTotalCapacityPrecise(), which is the most complex part
            // of the Deque implementation.
            .grow => {
                const growth = smith.value(u3);
                try l.ensureTotalCapacityPrecise(l_gpa, l.items.len + growth);
                try q.ensureTotalCapacityPrecise(q_gpa, q.len + growth);
            },
        }
        try testing.expectEqual(l.getLastOrNull(), q.back());
        try testing.expectEqual(
            if (l.items.len > 0) l.items[0] else null,
            q.front(),
        );
        try testing.expectEqual(l.items.len, q.len);
        try testing.expectEqual(l.capacity, q.buffer.len);
        {
            var it = q.iterator();
            for (l.items) |item| {
                try testing.expectEqual(item, it.next());
            }
            try testing.expectEqual(null, it.next());
        }
        try testing.expectEqual(@intFromBool(q.buffer.len != 0), q_gpa_inst.allocCount());
    }
    q.deinit(q_gpa);
    try testing.expectEqual(0, q_gpa_inst.allocCount());
}



---
File: /std/DoublyLinkedList.zig
---

//! A doubly-linked list has a pair of pointers to both the head and
//! tail of the list. List elements have pointers to both the previous
//! and next elements in the sequence. The list can be traversed both
//! forward and backward. Some operations that take linear O(n) time
//! with a singly-linked list can be done without traversal in constant
//! O(1) time with a doubly-linked list:
//!
//! * Removing an element.
//! * Inserting a new element before an existing element.
//! * Pushing or popping an element from the end of the list.

const std = @import("std.zig");
const debug = std.debug;
const assert = debug.assert;
const testing = std.testing;
const DoublyLinkedList = @This();

first: ?*Node = null,
last: ?*Node = null,

/// This struct contains only the prev and next pointers and not any data
/// payload. The intended usage is to embed it intrusively into another data
/// structure and access the data with `@fieldParentPtr`.
pub const Node = struct {
    prev: ?*Node = null,
    next: ?*Node = null,
};

pub fn insertAfter(list: *DoublyLinkedList, existing_node: *Node, new_node: *Node) void {
    new_node.prev = existing_node;
    if (existing_node.next) |next_node| {
        // Intermediate node.
        new_node.next = next_node;
        next_node.prev = new_node;
    } else {
        // Last element of the list.
        new_node.next = null;
        list.last = new_node;
    }
    existing_node.next = new_node;
}

pub fn insertBefore(list: *DoublyLinkedList, existing_node: *Node, new_node: *Node) void {
    new_node.next = existing_node;
    if (existing_node.prev) |prev_node| {
        // Intermediate node.
        new_node.prev = prev_node;
        prev_node.next = new_node;
    } else {
        // First element of the list.
        new_node.prev = null;
        list.first = new_node;
    }
    existing_node.prev = new_node;
}

/// Concatenate list2 onto the end of list1, removing all entries from the former.
///
/// Arguments:
///     list1: the list to concatenate onto
///     list2: the list to be concatenated
pub fn concatByMoving(list1: *DoublyLinkedList, list2: *DoublyLinkedList) void {
    const l2_first = list2.first orelse return;
    if (list1.last) |l1_last| {
        l1_last.next = list2.first;
        l2_first.prev = list1.last;
    } else {
        // list1 was empty
        list1.first = list2.first;
    }
    list1.last = list2.last;
    list2.first = null;
    list2.last = null;
}

/// Insert a new node at the end of the list.
///
/// Arguments:
///     new_node: Pointer to the new node to insert.
pub fn append(list: *DoublyLinkedList, new_node: *Node) void {
    if (list.last) |last| {
        // Insert after last.
        list.insertAfter(last, new_node);
    } else {
        // Empty list.
        list.prepend(new_node);
    }
}

/// Insert a new node at the beginning of the list.
///
/// Arguments:
///     new_node: Pointer to the new node to insert.
pub fn prepend(list: *DoublyLinkedList, new_node: *Node) void {
    if (list.first) |first| {
        // Insert before first.
        list.insertBefore(first, new_node);
    } else {
        // Empty list.
        list.first = new_node;
        list.last = new_node;
        new_node.prev = null;
        new_node.next = null;
    }
}

/// Remove a node from the list.
/// Assumes the node is in the list.
///
/// Arguments:
///     node: Pointer to the node to be removed.
pub fn remove(list: *DoublyLinkedList, node: *Node) void {
    if (node.prev) |prev_node| {
        // Intermediate node.
        prev_node.next = node.next;
    } else {
        // First element of the list.
        list.first = node.next;
    }

    if (node.next) |next_node| {
        // Intermediate node.
        next_node.prev = node.prev;
    } else {
        // Last element of the list.
        list.last = node.prev;
    }
}

/// Remove and return the last node in the list.
///
/// Returns:
///     A pointer to the last node in the list.
pub fn pop(list: *DoublyLinkedList) ?*Node {
    const last = list.last orelse return null;
    list.remove(last);
    return last;
}

/// Remove and return the first node in the list.
///
/// Returns:
///     A pointer to the first node in the list.
pub fn popFirst(list: *DoublyLinkedList) ?*Node {
    const first = list.first orelse return null;
    list.remove(first);
    return first;
}

/// Iterate over all nodes, returning the count.
///
/// This operation is O(N). Consider tracking the length separately rather than
/// computing it.
pub fn len(list: DoublyLinkedList) usize {
    var count: usize = 0;
    var it: ?*const Node = list.first;
    while (it) |n| : (it = n.next) count += 1;
    return count;
}

test "basics" {
    const L = struct {
        data: u32,
        node: DoublyLinkedList.Node = .{},
    };
    var list: DoublyLinkedList = .{};

    var one: L = .{ .data = 1 };
    var two: L = .{ .data = 2 };
    var three: L = .{ .data = 3 };
    var four: L = .{ .data = 4 };
    var five: L = .{ .data = 5 };

    list.append(&two.node); // {2}
    list.append(&five.node); // {2, 5}
    list.prepend(&one.node); // {1, 2, 5}
    list.insertBefore(&five.node, &four.node); // {1, 2, 4, 5}
    list.insertAfter(&two.node, &three.node); // {1, 2, 3, 4, 5}

    // Traverse forwards.
    {
        var it = list.first;
        var index: u32 = 1;
        while (it) |node| : (it = node.next) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == index);
            index += 1;
        }
    }

    // Traverse backwards.
    {
        var it = list.last;
        var index: u32 = 1;
        while (it) |node| : (it = node.prev) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == (6 - index));
            index += 1;
        }
    }

    _ = list.popFirst(); // {2, 3, 4, 5}
    _ = list.pop(); // {2, 3, 4}
    list.remove(&three.node); // {2, 4}

    try testing.expect(@as(*L, @fieldParentPtr("node", list.first.?)).data == 2);
    try testing.expect(@as(*L, @fieldParentPtr("node", list.last.?)).data == 4);
    try testing.expect(list.len() == 2);
}

test "concatenation" {
    const L = struct {
        data: u32,
        node: DoublyLinkedList.Node = .{},
    };
    var list1: DoublyLinkedList = .{};
    var list2: DoublyLinkedList = .{};

    var one: L = .{ .data = 1 };
    var two: L = .{ .data = 2 };
    var three: L = .{ .data = 3 };
    var four: L = .{ .data = 4 };
    var five: L = .{ .data = 5 };

    list1.append(&one.node);
    list1.append(&two.node);
    list2.append(&three.node);
    list2.append(&four.node);
    list2.append(&five.node);

    list1.concatByMoving(&list2);

    try testing.expect(list1.last == &five.node);
    try testing.expect(list1.len() == 5);
    try testing.expect(list2.first == null);
    try testing.expect(list2.last == null);
    try testing.expect(list2.len() == 0);

    // Traverse forwards.
    {
        var it = list1.first;
        var index: u32 = 1;
        while (it) |node| : (it = node.next) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == index);
            index += 1;
        }
    }

    // Traverse backwards.
    {
        var it = list1.last;
        var index: u32 = 1;
        while (it) |node| : (it = node.prev) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == (6 - index));
            index += 1;
        }
    }

    // Swap them back, this verifies that concatenating to an empty list works.
    list2.concatByMoving(&list1);

    // Traverse forwards.
    {
        var it = list2.first;
        var index: u32 = 1;
        while (it) |node| : (it = node.next) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == index);
            index += 1;
        }
    }

    // Traverse backwards.
    {
        var it = list2.last;
        var index: u32 = 1;
        while (it) |node| : (it = node.prev) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == (6 - index));
            index += 1;
        }
    }
}



---
File: /std/dwarf.zig
---

//! DWARF debugging data format.
//!
//! This namespace contains unopinionated types and data definitions only. For
//! an implementation of parsing and caching DWARF information, see
//! `std.debug.Dwarf`.

pub const TAG = @import("dwarf/TAG.zig");
pub const AT = @import("dwarf/AT.zig");
pub const OP = @import("dwarf/OP.zig");
pub const LANG = @import("dwarf/LANG.zig");
pub const FORM = @import("dwarf/FORM.zig");
pub const ATE = @import("dwarf/ATE.zig");
pub const EH = @import("dwarf/EH.zig");
pub const Format = enum { @"32", @"64" };

pub const LLE = struct {
    pub const end_of_list = 0x00;
    pub const base_addressx = 0x01;
    pub const startx_endx = 0x02;
    pub const startx_length = 0x03;
    pub const offset_pair = 0x04;
    pub const default_location = 0x05;
    pub const base_address = 0x06;
    pub const start_end = 0x07;
    pub const start_length = 0x08;
};

pub const CFA = struct {
    pub const advance_loc = 0x40;
    pub const offset = 0x80;
    pub const restore = 0xc0;
    pub const nop = 0x00;
    pub const set_loc = 0x01;
    pub const advance_loc1 = 0x02;
    pub const advance_loc2 = 0x03;
    pub const advance_loc4 = 0x04;
    pub const offset_extended = 0x05;
    pub const restore_extended = 0x06;
    pub const @"undefined" = 0x07;
    pub const same_value = 0x08;
    pub const register = 0x09;
    pub const remember_state = 0x0a;
    pub const restore_state = 0x0b;
    pub const def_cfa = 0x0c;
    pub const def_cfa_register = 0x0d;
    pub const def_cfa_offset = 0x0e;

    // DWARF 3.
    pub const def_cfa_expression = 0x0f;
    pub const expression = 0x10;
    pub const offset_extended_sf = 0x11;
    pub const def_cfa_sf = 0x12;
    pub const def_cfa_offset_sf = 0x13;
    pub const val_offset = 0x14;
    pub const val_offset_sf = 0x15;
    pub const val_expression = 0x16;

    pub const lo_user = 0x1c;
    pub const hi_user = 0x3f;

    // SGI/MIPS specific.
    pub const MIPS_advance_loc8 = 0x1d;

    // GNU extensions.
    pub const GNU_window_save = 0x2d;
    pub const GNU_args_size = 0x2e;
    pub const GNU_negative_offset_extended = 0x2f;
};

pub const CHILDREN = struct {
    pub const no = 0x00;
    pub const yes = 0x01;
};

pub const LNS = struct {
    pub const extended_op = 0x00;
    pub const copy = 0x01;
    pub const advance_pc = 0x02;
    pub const advance_line = 0x03;
    pub const set_file = 0x04;
    pub const set_column = 0x05;
    pub const negate_stmt = 0x06;
    pub const set_basic_block = 0x07;
    pub const const_add_pc = 0x08;
    pub const fixed_advance_pc = 0x09;
    pub const set_prologue_end = 0x0a;
    pub const set_epilogue_begin = 0x0b;
    pub const set_isa = 0x0c;
};

pub const LNE = struct {
    pub const padding = 0x00;
    pub const end_sequence = 0x01;
    pub const set_address = 0x02;
    pub const define_file = 0x03;
    pub const set_discriminator = 0x04;
    pub const lo_user = 0x80;
    pub const hi_user = 0xff;

    // Zig extensions
    pub const ZIG_set_decl = 0xec;
};

pub const UT = struct {
    pub const compile = 0x01;
    pub const @"type" = 0x02;
    pub const partial = 0x03;
    pub const skeleton = 0x04;
    pub const split_compile = 0x05;
    pub const split_type = 0x06;

    pub const lo_user = 0x80;
    pub const hi_user = 0xff;
};

pub const LNCT = struct {
    pub const path = 0x1;
    pub const directory_index = 0x2;
    pub const timestamp = 0x3;
    pub const size = 0x4;
    pub const MD5 = 0x5;

    pub const lo_user = 0x2000;
    pub const hi_user = 0x3fff;

    pub const LLVM_source = 0x2001;
};

pub const RLE = struct {
    pub const end_of_list = 0x00;
    pub const base_addressx = 0x01;
    pub const startx_endx = 0x02;
    pub const startx_length = 0x03;
    pub const offset_pair = 0x04;
    pub const base_address = 0x05;
    pub const start_end = 0x06;
    pub const start_length = 0x07;
};

pub const CC = enum(u8) {
    normal = 0x1,
    program = 0x2,
    nocall = 0x3,

    pass_by_reference = 0x4,
    pass_by_value = 0x5,

    GNU_renesas_sh = 0x40,
    GNU_borland_fastcall_i386 = 0x41,

    BORLAND_safecall = 0xb0,
    BORLAND_stdcall = 0xb1,
    BORLAND_pascal = 0xb2,
    BORLAND_msfastcall = 0xb3,
    BORLAND_msreturn = 0xb4,
    BORLAND_thiscall = 0xb5,
    BORLAND_fastcall = 0xb6,

    LLVM_vectorcall = 0xc0,
    LLVM_Win64 = 0xc1,
    LLVM_X86_64SysV = 0xc2,
    LLVM_AAPCS = 0xc3,
    LLVM_AAPCS_VFP = 0xc4,
    LLVM_IntelOclBicc = 0xc5,
    LLVM_SpirFunction = 0xc6,
    LLVM_OpenCLKernel = 0xc7,
    LLVM_Swift = 0xc8,
    LLVM_PreserveMost = 0xc9,
    LLVM_PreserveAll = 0xca,
    LLVM_X86RegCall = 0xcb,
    LLVM_M68kRTD = 0xcc,
    LLVM_PreserveNone = 0xcd,
    LLVM_RISCVVectorCall = 0xce,
    LLVM_SwiftTail = 0xcf,

    pub const lo_user = 0x40;
    pub const hi_user = 0xff;
};

pub const ACCESS = struct {
    pub const public = 0x01;
    pub const protected = 0x02;
    pub const private = 0x03;
};



---
File: /std/dynamic_library.zig
---

const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("std.zig");
const Io = std.Io;
const mem = std.mem;
const testing = std.testing;
const elf = std.elf;
const windows = std.os.windows;
const posix = std.posix;

/// Cross-platform dynamic library loading and symbol lookup.
/// Platform-specific functionality is available through the `inner` field.
pub const DynLib = struct {
    const InnerType = switch (native_os) {
        .linux => if (!builtin.link_libc or builtin.abi == .musl and builtin.link_mode == .static)
            ElfDynLib
        else
            DlDynLib,
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos, .freebsd, .netbsd, .openbsd, .dragonfly, .illumos => DlDynLib,
        else => struct {
            const open = @compileError("unsupported platform");
            const openZ = @compileError("unsupported platform");
        },
    };

    inner: InnerType,

    pub const Error = ElfDynLibError || DlDynLibError;

    /// Trusts the file. Malicious file will be able to execute arbitrary code.
    pub fn open(path: []const u8) Error!DynLib {
        if (InnerType == ElfDynLib) {
            return .{ .inner = try InnerType.open(path, null) };
        } else {
            return .{ .inner = try InnerType.open(path) };
        }
    }

    /// Trusts the file. Malicious file will be able to execute arbitrary code.
    pub fn openZ(path_c: [*:0]const u8) Error!DynLib {
        if (InnerType == ElfDynLib) {
            return .{ .inner = try InnerType.openZ(path_c, null) };
        } else {
            return .{ .inner = try InnerType.openZ(path_c) };
        }
    }

    /// Trusts the file.
    pub fn close(self: *DynLib) void {
        return self.inner.close();
    }

    pub fn lookup(self: *DynLib, comptime T: type, name: [:0]const u8) ?T {
        return self.inner.lookup(T, name);
    }
};

// The link_map structure is not completely specified beside the fields
// reported below, any libc is free to store additional data in the remaining
// space.
// An iterator is provided in order to traverse the linked list in a idiomatic
// fashion.
const LinkMap = extern struct {
    addr: usize,
    name: [*:0]const u8,
    ld: ?*elf.Dyn,
    next: ?*LinkMap,
    prev: ?*LinkMap,

    pub const Iterator = struct {
        current: ?*LinkMap,

        pub fn end(self: *Iterator) bool {
            return self.current == null;
        }

        pub fn next(self: *Iterator) ?*LinkMap {
            if (self.current) |it| {
                self.current = it.next;
                return it;
            }
            return null;
        }
    };
};

const RDebug = extern struct {
    version: i32,
    map: ?*LinkMap,
    brk: usize,
    ldbase: usize,
};

/// TODO fix comparisons of extern symbol pointers so we don't need this helper function.
pub fn get_DYNAMIC() ?[*]const elf.Dyn {
    return @extern([*]const elf.Dyn, .{
        .name = "_DYNAMIC",
        .linkage = .weak,
        .visibility = .hidden,
    });
}

pub fn linkmap_iterator() error{InvalidExe}!LinkMap.Iterator {
    const _DYNAMIC = get_DYNAMIC() orelse {
        // No PT_DYNAMIC means this is a statically-linked non-PIE program.
        return .{ .current = null };
    };

    const link_map_ptr = init: {
        var i: usize = 0;
        while (_DYNAMIC[i].d_tag != elf.DT_NULL) : (i += 1) {
            switch (_DYNAMIC[i].d_tag) {
                elf.DT_DEBUG => {
                    const ptr = @as(?*RDebug, @ptrFromInt(_DYNAMIC[i].d_val));
                    if (ptr) |r_debug| {
                        if (r_debug.version != 1) return error.InvalidExe;
                        break :init r_debug.map;
                    }
                },
                elf.DT_PLTGOT => {
                    const ptr = @as(?[*]usize, @ptrFromInt(_DYNAMIC[i].d_val));
                    if (ptr) |got_table| {
                        // The address to the link_map structure is stored in
                        // the second slot
                        break :init @as(?*LinkMap, @ptrFromInt(got_table[1]));
                    }
                },
                else => {},
            }
        }
        return .{ .current = null };
    };

    return .{ .current = link_map_ptr };
}

/// Separated to avoid referencing `ElfDynLib`, because its field types may not
/// be valid on other targets.
const ElfDynLibError = error{
    FileTooBig,
    NotElfFile,
    NotDynamicLibrary,
    MissingDynamicLinkingInformation,
    ElfStringSectionNotFound,
    ElfSymSectionNotFound,
    ElfHashTableNotFound,
    Canceled,
    Streaming,
} || Io.File.OpenError || posix.MMapError;

pub const ElfDynLib = struct {
    strings: [*:0]u8,
    syms: [*]elf.Sym,
    hash_table: HashTable,
    versym: ?[*]elf.Versym,
    verdef: ?*elf.Verdef,
    memory: []align(std.heap.page_size_min) u8,

    pub const Error = ElfDynLibError;

    const HashTable = union(enum) {
        dt_hash: [*]posix.Elf_Symndx,
        dt_gnu_hash: *elf.gnu_hash.Header,
    };

    fn openPath(io: Io, path: []const u8) !Io.Dir {
        if (path.len == 0) return error.NotDir;
        var parts = std.mem.tokenizeScalar(u8, path, '/');
        var parent = if (path[0] == '/') try Io.Dir.cwd().openDir(io, "/", .{}) else Io.Dir.cwd();
        while (parts.next()) |part| {
            const child = try parent.openDir(io, part, .{});
            parent.close(io);
            parent = child;
        }
        return parent;
    }

    fn resolveFromSearchPath(io: Io, search_path: []const u8, file_name: []const u8, delim: u8) ?Io.File {
        var paths = std.mem.tokenizeScalar(u8, search_path, delim);
        while (paths.next()) |p| {
            var dir = openPath(io, p) catch continue;
            defer dir.close(io);
            return dir.openFile(io, file_name, .{}) catch continue;
        }
        return null;
    }

    fn resolveFromParent(io: Io, dir_path: []const u8, file_name: []const u8) ?Io.File {
        var dir = Io.Dir.cwd().openDir(io, dir_path, .{}) catch return null;
        defer dir.close(io);
        return dir.openFile(io, file_name, .{}) catch null;
    }

    // This implements enough to be able to load system libraries in general
    // Places where it differs from dlopen:
    // - DT_RPATH of the calling binary is not used as a search path
    // - DT_RUNPATH of the calling binary is not used as a search path
    // - /etc/ld.so.cache is not read
    fn resolveFromName(io: Io, path_or_name: []const u8, LD_LIBRARY_PATH: ?[]const u8) !Io.File {
        // If filename contains a slash ("/"), then it is interpreted as a (relative or absolute) pathname
        if (std.mem.findScalarPos(u8, path_or_name, 0, '/')) |_| {
            return Io.Dir.cwd().openFile(io, path_or_name, .{});
        }

        // Only read LD_LIBRARY_PATH if the binary is not setuid/setgid
        if (std.os.linux.geteuid() == std.os.linux.getuid() and
            std.os.linux.getegid() == std.os.linux.getgid())
        {
            if (LD_LIBRARY_PATH) |ld_library_path| {
                if (resolveFromSearchPath(io, ld_library_path, path_or_name, ':')) |file| {
                    return file;
                }
            }
        }

        // Lastly the directories /lib and /usr/lib are searched (in this exact order)
        if (resolveFromParent(io, "/lib", path_or_name)) |file| return file;
        if (resolveFromParent(io, "/usr/lib", path_or_name)) |file| return file;
        return error.FileNotFound;
    }

    /// Trusts the file. Malicious file will be able to execute arbitrary code.
    pub fn open(path: []const u8, LD_LIBRARY_PATH: ?[]const u8) Error!ElfDynLib {
        const io = std.Options.debug_io;

        const file = try resolveFromName(io, path, LD_LIBRARY_PATH);
        defer file.close(io);

        const stat = try file.stat(io);
        const size = std.math.cast(usize, stat.size) orelse return error.FileTooBig;

        const page_size = std.heap.pageSize();

        // This one is to read the ELF info. We do more mmapping later
        // corresponding to the actual LOAD sections.
        const file_bytes = try posix.mmap(
            null,
            mem.alignForward(usize, size, page_size),
            .{ .READ = true },
            .{ .TYPE = .PRIVATE },
            file.handle,
            0,
        );
        defer posix.munmap(file_bytes);

        const eh = @as(*elf.Ehdr, @ptrCast(file_bytes.ptr));
        if (!mem.eql(u8, eh.e_ident[0..4], elf.MAGIC)) return error.NotElfFile;
        if (eh.e_type != elf.ET.DYN) return error.NotDynamicLibrary;

        const elf_addr = @intFromPtr(file_bytes.ptr);

        // Iterate over the program header entries to find out the
        // dynamic vector as well as the total size of the virtual memory.
        var maybe_dynv: ?[*]usize = null;
        var virt_addr_end: usize = 0;
        {
            var i: usize = 0;
            var ph_addr: usize = elf_addr + eh.e_phoff;
            while (i < eh.e_phnum) : ({
                i += 1;
                ph_addr += eh.e_phentsize;
            }) {
                const ph = @as(*elf.Phdr, @ptrFromInt(ph_addr));
                switch (ph.p_type) {
                    elf.PT_LOAD => virt_addr_end = @max(virt_addr_end, ph.p_vaddr + ph.p_memsz),
                    elf.PT_DYNAMIC => maybe_dynv = @as([*]usize, @ptrFromInt(elf_addr + ph.p_offset)),
                    else => {},
                }
            }
        }
        const dynv = maybe_dynv orelse return error.MissingDynamicLinkingInformation;

        // Reserve the entire range (with no permissions) so that we can do MAP.FIXED below.
        const all_loaded_mem = try posix.mmap(
            null,
            virt_addr_end,
            .{},
            .{ .TYPE = .PRIVATE, .ANONYMOUS = true },
            -1,
            0,
        );
        errdefer posix.munmap(all_loaded_mem);

        const base = @intFromPtr(all_loaded_mem.ptr);

        // Now iterate again and actually load all the program sections.
        {
            var i: usize = 0;
            var ph_addr: usize = elf_addr + eh.e_phoff;
            while (i < eh.e_phnum) : ({
                i += 1;
                ph_addr += eh.e_phentsize;
            }) {
                const ph = @as(*elf.Phdr, @ptrFromInt(ph_addr));
                switch (ph.p_type) {
                    elf.PT_LOAD => {
                        // The VirtAddr may not be page-aligned; in such case there will be
                        // extra nonsense mapped before/after the VirtAddr,MemSiz
                        const aligned_addr = (base + ph.p_vaddr) & ~(@as(usize, page_size) - 1);
                        const extra_bytes = (base + ph.p_vaddr) - aligned_addr;
                        const extended_memsz = mem.alignForward(usize, ph.p_memsz + extra_bytes, page_size);
                        const ptr = @as([*]align(std.heap.page_size_min) u8, @ptrFromInt(aligned_addr));
                        const prot = elfToProt(ph.p_flags);
                        if ((ph.p_flags & elf.PF_W) == 0) {
                            // If it does not need write access, it can be mapped from the fd.
                            _ = try posix.mmap(
                                ptr,
                                extended_memsz,
                                prot,
                                .{ .TYPE = .PRIVATE, .FIXED = true },
                                file.handle,
                                ph.p_offset - extra_bytes,
                            );
                        } else {
                            const sect_mem = try posix.mmap(
                                ptr,
                                extended_memsz,
                                prot,
                                .{ .TYPE = .PRIVATE, .FIXED = true, .ANONYMOUS = true },
                                -1,
                                0,
                            );
                            @memcpy(sect_mem[0..ph.p_filesz], file_bytes[0..ph.p_filesz]);
                        }
                    },
                    else => {},
                }
            }
        }

        var maybe_strings: ?[*:0]u8 = null;
        var maybe_syms: ?[*]elf.Sym = null;
        var maybe_hashtab: ?[*]posix.Elf_Symndx = null;
        var maybe_gnu_hash: ?*elf.gnu_hash.Header = null;
        var maybe_versym: ?[*]elf.Versym = null;
        var maybe_verdef: ?*elf.Verdef = null;

        {
            var i: usize = 0;
            while (dynv[i] != 0) : (i += 2) {
                const p = base + dynv[i + 1];
                switch (dynv[i]) {
                    elf.DT_STRTAB => maybe_strings = @ptrFromInt(p),
                    elf.DT_SYMTAB => maybe_syms = @ptrFromInt(p),
                    elf.DT_HASH => maybe_hashtab = @ptrFromInt(p),
                    elf.DT_GNU_HASH => maybe_gnu_hash = @ptrFromInt(p),
                    elf.DT_VERSYM => maybe_versym = @ptrFromInt(p),
                    elf.DT_VERDEF => maybe_verdef = @ptrFromInt(p),
                    else => {},
                }
            }
        }

        const hash_table: HashTable = if (maybe_gnu_hash) |gnu_hash|
            .{ .dt_gnu_hash = gnu_hash }
        else if (maybe_hashtab) |hashtab|
            .{ .dt_hash = hashtab }
        else
            return error.ElfHashTableNotFound;

        return .{
            .memory = all_loaded_mem,
            .strings = maybe_strings orelse return error.ElfStringSectionNotFound,
            .syms = maybe_syms orelse return error.ElfSymSectionNotFound,
            .hash_table = hash_table,
            .versym = maybe_versym,
            .verdef = maybe_verdef,
        };
    }

    /// Trusts the file. Malicious file will be able to execute arbitrary code.
    pub fn openZ(path_c: [*:0]const u8, LD_LIBRARY_PATH: ?[]const u8) Error!ElfDynLib {
        return open(mem.sliceTo(path_c, 0), LD_LIBRARY_PATH);
    }

    /// Trusts the file
    pub fn close(self: *ElfDynLib) void {
        posix.munmap(self.memory);
        self.* = undefined;
    }

    pub fn lookup(self: *const ElfDynLib, comptime T: type, name: [:0]const u8) ?T {
        if (self.lookupAddress("", name)) |symbol| {
            return @as(T, @ptrFromInt(symbol));
        } else {
            return null;
        }
    }

    pub const GnuHashSection32 = struct {
        symoffset: u32,
        bloom_shift: u32,
        bloom: []u32,
        buckets: []u32,
        chain: [*]elf.gnu_hash.ChainEntry,

        pub fn fromPtr(header: *elf.gnu_hash.Header) @This() {
            const header_offset = @intFromPtr(header);
            const bloom_offset = header_offset + @sizeOf(elf.gnu_hash.Header);
            const buckets_offset = bloom_offset + header.bloom_size * @sizeOf(u32);
            const chain_offset = buckets_offset + header.nbuckets * @sizeOf(u32);

            const bloom_ptr: [*]u32 = @ptrFromInt(bloom_offset);
            const buckets_ptr: [*]u32 = @ptrFromInt(buckets_offset);
            const chain_ptr: [*]elf.gnu_hash.ChainEntry = @ptrFromInt(chain_offset);

            return .{
                .symoffset = header.symoffset,
                .bloom_shift = header.bloom_shift,
                .bloom = bloom_ptr[0..header.bloom_size],
                .buckets = buckets_ptr[0..header.nbuckets],
                .chain = chain_ptr,
            };
        }
    };

    pub const GnuHashSection64 = struct {
        symoffset: u32,
        bloom_shift: u32,
        bloom: []u64,
        buckets: []u32,
        chain: [*]elf.gnu_hash.ChainEntry,

        pub fn fromPtr(header: *elf.gnu_hash.Header) @This() {
            const header_offset = @intFromPtr(header);
            const bloom_offset = header_offset + @sizeOf(elf.gnu_hash.Header);
            const buckets_offset = bloom_offset + header.bloom_size * @sizeOf(u64);
            const chain_offset = buckets_offset + header.nbuckets * @sizeOf(u32);

            const bloom_ptr: [*]u64 = @ptrFromInt(bloom_offset);
            const buckets_ptr: [*]u32 = @ptrFromInt(buckets_offset);
            const chain_ptr: [*]elf.gnu_hash.ChainEntry = @ptrFromInt(chain_offset);

            return .{
                .symoffset = header.symoffset,
                .bloom_shift = header.bloom_shift,
                .bloom = bloom_ptr[0..header.bloom_size],
                .buckets = buckets_ptr[0..header.nbuckets],
                .chain = chain_ptr,
            };
        }
    };

    /// ElfDynLib specific
    /// Returns the address of the symbol
    pub fn lookupAddress(self: *const ElfDynLib, vername: []const u8, name: []const u8) ?usize {
        const maybe_versym = if (self.verdef == null) null else self.versym;

        const OK_TYPES = (1 << elf.STT_NOTYPE | 1 << elf.STT_OBJECT | 1 << elf.STT_FUNC | 1 << elf.STT_COMMON);
        const OK_BINDS = (1 << elf.STB_GLOBAL | 1 << elf.STB_WEAK | 1 << elf.STB_GNU_UNIQUE);

        switch (self.hash_table) {
            .dt_hash => |hashtab| {
                var i: usize = 0;
                while (i < hashtab[1]) : (i += 1) {
                    if (0 == (@as(u32, 1) << @as(u5, @intCast(self.syms[i].st_info & 0xf)) & OK_TYPES)) continue;
                    if (0 == (@as(u32, 1) << @as(u5, @intCast(self.syms[i].st_info >> 4)) & OK_BINDS)) continue;
                    if (0 == self.syms[i].st_shndx) continue;
                    if (!mem.eql(u8, name, mem.sliceTo(self.strings + self.syms[i].st_name, 0))) continue;
                    if (maybe_versym) |versym| {
                        if (!checkver(self.verdef.?, versym[i], vername, self.strings))
                            continue;
                    }
                    return @intFromPtr(self.memory.ptr) + self.syms[i].st_value;
                }
            },
            .dt_gnu_hash => |gnu_hash_header| {
                const GnuHashSection = switch (@bitSizeOf(usize)) {
                    32 => GnuHashSection32,
                    64 => GnuHashSection64,
                    else => |bit_size| @compileError("Unsupported bit size " ++ bit_size),
                };

                const gnu_hash_section: GnuHashSection = .fromPtr(gnu_hash_header);
                const hash = elf.gnu_hash.calculate(name);

                const bloom_index = (hash / @bitSizeOf(usize)) % gnu_hash_header.bloom_size;
                const bloom_val = gnu_hash_section.bloom[bloom_index];

                const bit_index_0 = hash % @bitSizeOf(usize);
                const bit_index_1 = (hash >> @intCast(gnu_hash_header.bloom_shift)) % @bitSizeOf(usize);

                const one: usize = 1;
                const bit_mask: usize = (one << @intCast(bit_index_0)) | (one << @intCast(bit_index_1));

                if (bloom_val & bit_mask != bit_mask) {
                    // Symbol is not in bloom filter, so it definitely isn't here.
                    return null;
                }

                const bucket_index = hash % gnu_hash_header.nbuckets;
                const chain_index = gnu_hash_section.buckets[bucket_index] - gnu_hash_header.symoffset;

                const chains = gnu_hash_section.chain;
                const hash_as_entry: elf.gnu_hash.ChainEntry = @bitCast(hash);

                var current_index = chain_index;
                var at_end_of_chain = false;
                while (!at_end_of_chain) : (current_index += 1) {
                    const current_entry = chains[current_index];
                    at_end_of_chain = current_entry.end_of_chain;

                    if (current_entry.hash != hash_as_entry.hash) continue;

                    // check that symbol matches
                    const symbol_index = current_index + gnu_hash_header.symoffset;
                    const symbol = self.syms[symbol_index];

                    if (0 == (@as(u32, 1) << @as(u5, @intCast(symbol.st_info & 0xf)) & OK_TYPES)) continue;
                    if (0 == (@as(u32, 1) << @as(u5, @intCast(symbol.st_info >> 4)) & OK_BINDS)) continue;
                    if (0 == symbol.st_shndx) continue;

                    const symbol_name = mem.sliceTo(self.strings + symbol.st_name, 0);
                    if (!mem.eql(u8, name, symbol_name)) {
                        continue;
                    }

                    if (maybe_versym) |versym| {
                        if (!checkver(self.verdef.?, versym[symbol_index], vername, self.strings)) {
                            continue;
                        }
                    }

                    return @intFromPtr(self.memory.ptr) + symbol.st_value;
                }
            },
        }

        return null;
    }

    fn elfToProt(elf_prot: u64) posix.PROT {
        return .{
            .READ = (elf_prot & elf.PF_R) != 0,
            .WRITE = (elf_prot & elf.PF_W) != 0,
            .EXEC = (elf_prot & elf.PF_X) != 0,
        };
    }
};

fn checkver(def_arg: *elf.Verdef, vsym_arg: elf.Versym, vername: []const u8, strings: [*:0]u8) bool {
    var def = def_arg;
    const vsym_index = vsym_arg.VERSION;
    while (true) {
        if (0 == (def.flags & elf.VER_FLG_BASE) and @intFromEnum(def.ndx) == vsym_index) break;
        if (def.next == 0) return false;
        def = @ptrFromInt(@intFromPtr(def) + def.next);
    }
    const aux: *elf.Verdaux = @ptrFromInt(@intFromPtr(def) + def.aux);
    return mem.eql(u8, vername, mem.sliceTo(strings + aux.name, 0));
}

test "ElfDynLib" {
    if (native_os != .linux) return error.SkipZigTest;
    try testing.expectError(error.FileNotFound, ElfDynLib.open("invalid_so.so", null));
    try testing.expectError(error.FileNotFound, ElfDynLib.openZ("invalid_so.so", null));
}

/// Separated to avoid referencing `DlDynLib`, because its field types may not
/// be valid on other targets.
const DlDynLibError = error{ FileNotFound, NameTooLong };

pub const DlDynLib = struct {
    pub const Error = DlDynLibError;

    handle: *anyopaque,

    pub fn open(path: []const u8) Error!DlDynLib {
        const path_c = try posix.toPosixPath(path);
        return openZ(&path_c);
    }

    pub fn openZ(path_c: [*:0]const u8) Error!DlDynLib {
        return .{
            .handle = std.c.dlopen(path_c, .{ .LAZY = true }) orelse {
                return error.FileNotFound;
            },
        };
    }

    pub fn close(self: *DlDynLib) void {
        switch (posix.errno(std.c.dlclose(self.handle))) {
            .SUCCESS => return,
            else => unreachable,
        }
        self.* = undefined;
    }

    pub fn lookup(self: *DlDynLib, comptime T: type, name: [:0]const u8) ?T {
        // dlsym (and other dl-functions) secretly take shadow parameter - return address on stack
        // https://gcc.gnu.org/bugzilla/show_bug.cgi?id=66826
        if (@call(.never_tail, std.c.dlsym, .{ self.handle, name.ptr })) |symbol| {
            return @as(T, @ptrCast(@alignCast(symbol)));
        } else {
            return null;
        }
    }

    /// DlDynLib specific
    /// Returns human readable string describing most recent error than occurred from `lookup`
    /// or `null` if no error has occurred since initialization or when `getError` was last called.
    pub fn getError() ?[:0]const u8 {
        return mem.span(std.c.dlerror());
    }
};

test "dynamic_library" {
    const libname = switch (native_os) {
        .linux, .freebsd, .openbsd, .illumos => "invalid_so.so",
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => "invalid_dylib.dylib",
        else => return error.SkipZigTest,
    };

    try testing.expectError(error.FileNotFound, DynLib.open(libname));
    try testing.expectError(error.FileNotFound, DynLib.openZ(libname.ptr));
}



---
File: /std/elf.zig
---

//! Executable and Linkable Format.

const std = @import("std.zig");
const Io = std.Io;
const math = std.math;
const mem = std.mem;
const assert = std.debug.assert;
const Endian = std.builtin.Endian;
const native_endian = @import("builtin").target.cpu.arch.endian();

pub const AT_NULL = 0;
pub const AT_IGNORE = 1;
pub const AT_EXECFD = 2;
pub const AT_PHDR = 3;
pub const AT_PHENT = 4;
pub const AT_PHNUM = 5;
pub const AT_PAGESZ = 6;
pub const AT_BASE = 7;
pub const AT_FLAGS = 8;
pub const AT_ENTRY = 9;
pub const AT_NOTELF = 10;
pub const AT_UID = 11;
pub const AT_EUID = 12;
pub const AT_GID = 13;
pub const AT_EGID = 14;
pub const AT_CLKTCK = 17;
pub const AT_PLATFORM = 15;
pub const AT_HWCAP = 16;
pub const AT_FPUCW = 18;
pub const AT_DCACHEBSIZE = 19;
pub const AT_ICACHEBSIZE = 20;
pub const AT_UCACHEBSIZE = 21;
pub const AT_IGNOREPPC = 22;
pub const AT_SECURE = 23;
pub const AT_BASE_PLATFORM = 24;
pub const AT_RANDOM = 25;
pub const AT_HWCAP2 = 26;
pub const AT_EXECFN = 31;
pub const AT_SYSINFO = 32;
pub const AT_SYSINFO_EHDR = 33;
pub const AT_L1I_CACHESHAPE = 34;
pub const AT_L1D_CACHESHAPE = 35;
pub const AT_L2_CACHESHAPE = 36;
pub const AT_L3_CACHESHAPE = 37;
pub const AT_L1I_CACHESIZE = 40;
pub const AT_L1I_CACHEGEOMETRY = 41;
pub const AT_L1D_CACHESIZE = 42;
pub const AT_L1D_CACHEGEOMETRY = 43;
pub const AT_L2_CACHESIZE = 44;
pub const AT_L2_CACHEGEOMETRY = 45;
pub const AT_L3_CACHESIZE = 46;
pub const AT_L3_CACHEGEOMETRY = 47;
pub const AT_MINSIGSTKSZ = 51;

pub const DT_NULL = 0;
pub const DT_NEEDED = 1;
pub const DT_PLTRELSZ = 2;
pub const DT_PLTGOT = 3;
pub const DT_HASH = 4;
pub const DT_STRTAB = 5;
pub const DT_SYMTAB = 6;
pub const DT_RELA = 7;
pub const DT_RELASZ = 8;
pub const DT_RELAENT = 9;
pub const DT_STRSZ = 10;
pub const DT_SYMENT = 11;
pub const DT_INIT = 12;
pub const DT_FINI = 13;
pub const DT_SONAME = 14;
pub const DT_RPATH = 15;
pub const DT_SYMBOLIC = 16;
pub const DT_REL = 17;
pub const DT_RELSZ = 18;
pub const DT_RELENT = 19;
pub const DT_PLTREL = 20;
pub const DT_DEBUG = 21;
pub const DT_TEXTREL = 22;
pub const DT_JMPREL = 23;
pub const DT_BIND_NOW = 24;
pub const DT_INIT_ARRAY = 25;
pub const DT_FINI_ARRAY = 26;
pub const DT_INIT_ARRAYSZ = 27;
pub const DT_FINI_ARRAYSZ = 28;
pub const DT_RUNPATH = 29;
pub const DT_FLAGS = 30;
pub const DT_ENCODING = 32;
pub const DT_PREINIT_ARRAY = 32;
pub const DT_PREINIT_ARRAYSZ = 33;
pub const DT_SYMTAB_SHNDX = 34;
pub const DT_RELRSZ = 35;
pub const DT_RELR = 36;
pub const DT_RELRENT = 37;
pub const DT_NUM = 38;
pub const DT_LOOS = 0x6000000d;
pub const DT_HIOS = 0x6ffff000;
pub const DT_LOPROC = 0x70000000;
pub const DT_HIPROC = 0x7fffffff;
pub const DT_PROCNUM = DT_MIPS_NUM;

pub const DT_VALRNGLO = 0x6ffffd00;
pub const DT_GNU_PRELINKED = 0x6ffffdf5;
pub const DT_GNU_CONFLICTSZ = 0x6ffffdf6;
pub const DT_GNU_LIBLISTSZ = 0x6ffffdf7;
pub const DT_CHECKSUM = 0x6ffffdf8;
pub const DT_PLTPADSZ = 0x6ffffdf9;
pub const DT_MOVEENT = 0x6ffffdfa;
pub const DT_MOVESZ = 0x6ffffdfb;
pub const DT_FEATURE_1 = 0x6ffffdfc;
pub const DT_POSFLAG_1 = 0x6ffffdfd;

pub const DT_SYMINSZ = 0x6ffffdfe;
pub const DT_SYMINENT = 0x6ffffdff;
pub const DT_VALRNGHI = 0x6ffffdff;
pub const DT_VALNUM = 12;

pub const DT_ADDRRNGLO = 0x6ffffe00;
pub const DT_GNU_HASH = 0x6ffffef5;
pub const DT_TLSDESC_PLT = 0x6ffffef6;
pub const DT_TLSDESC_GOT = 0x6ffffef7;
pub const DT_GNU_CONFLICT = 0x6ffffef8;
pub const DT_GNU_LIBLIST = 0x6ffffef9;
pub const DT_CONFIG = 0x6ffffefa;
pub const DT_DEPAUDIT = 0x6ffffefb;
pub const DT_AUDIT = 0x6ffffefc;
pub const DT_PLTPAD = 0x6ffffefd;
pub const DT_MOVETAB = 0x6ffffefe;
pub const DT_SYMINFO = 0x6ffffeff;
pub const DT_ADDRRNGHI = 0x6ffffeff;
pub const DT_ADDRNUM = 11;

pub const DT_VERSYM = 0x6ffffff0;

pub const DT_RELACOUNT = 0x6ffffff9;
pub const DT_RELCOUNT = 0x6ffffffa;

pub const DT_FLAGS_1 = 0x6ffffffb;
pub const DT_VERDEF = 0x6ffffffc;

pub const DT_VERDEFNUM = 0x6ffffffd;
pub const DT_VERNEED = 0x6ffffffe;

pub const DT_VERNEEDNUM = 0x6fffffff;
pub const DT_VERSIONTAGNUM = 16;

pub const DT_AUXILIARY = 0x7ffffffd;
pub const DT_FILTER = 0x7fffffff;
pub const DT_EXTRANUM = 3;

pub const DT_SPARC_REGISTER = 0x70000001;
pub const DT_SPARC_NUM = 2;

pub const DT_MIPS_RLD_VERSION = 0x70000001;
pub const DT_MIPS_TIME_STAMP = 0x70000002;
pub const DT_MIPS_ICHECKSUM = 0x70000003;
pub const DT_MIPS_IVERSION = 0x70000004;
pub const DT_MIPS_FLAGS = 0x70000005;
pub const DT_MIPS_BASE_ADDRESS = 0x70000006;
pub const DT_MIPS_MSYM = 0x70000007;
pub const DT_MIPS_CONFLICT = 0x70000008;
pub const DT_MIPS_LIBLIST = 0x70000009;
pub const DT_MIPS_LOCAL_GOTNO = 0x7000000a;
pub const DT_MIPS_CONFLICTNO = 0x7000000b;
pub const DT_MIPS_LIBLISTNO = 0x70000010;
pub const DT_MIPS_SYMTABNO = 0x70000011;
pub const DT_MIPS_UNREFEXTNO = 0x70000012;
pub const DT_MIPS_GOTSYM = 0x70000013;
pub const DT_MIPS_HIPAGENO = 0x70000014;
pub const DT_MIPS_RLD_MAP = 0x70000016;
pub const DT_MIPS_DELTA_CLASS = 0x70000017;
pub const DT_MIPS_DELTA_CLASS_NO = 0x70000018;

pub const DT_MIPS_DELTA_INSTANCE = 0x70000019;
pub const DT_MIPS_DELTA_INSTANCE_NO = 0x7000001a;

pub const DT_MIPS_DELTA_RELOC = 0x7000001b;
pub const DT_MIPS_DELTA_RELOC_NO = 0x7000001c;

pub const DT_MIPS_DELTA_SYM = 0x7000001d;

pub const DT_MIPS_DELTA_SYM_NO = 0x7000001e;

pub const DT_MIPS_DELTA_CLASSSYM = 0x70000020;

pub const DT_MIPS_DELTA_CLASSSYM_NO = 0x70000021;

pub const DT_MIPS_CXX_FLAGS = 0x70000022;
pub const DT_MIPS_PIXIE_INIT = 0x70000023;
pub const DT_MIPS_SYMBOL_LIB = 0x70000024;
pub const DT_MIPS_LOCALPAGE_GOTIDX = 0x70000025;
pub const DT_MIPS_LOCAL_GOTIDX = 0x70000026;
pub const DT_MIPS_HIDDEN_GOTIDX = 0x70000027;
pub const DT_MIPS_PROTECTED_GOTIDX = 0x70000028;
pub const DT_MIPS_OPTIONS = 0x70000029;
pub const DT_MIPS_INTERFACE = 0x7000002a;
pub const DT_MIPS_DYNSTR_ALIGN = 0x7000002b;
pub const DT_MIPS_INTERFACE_SIZE = 0x7000002c;
pub const DT_MIPS_RLD_TEXT_RESOLVE_ADDR = 0x7000002d;

pub const DT_MIPS_PERF_SUFFIX = 0x7000002e;

pub const DT_MIPS_COMPACT_SIZE = 0x7000002f;
pub const DT_MIPS_GP_VALUE = 0x70000030;
pub const DT_MIPS_AUX_DYNAMIC = 0x70000031;

pub const DT_MIPS_PLTGOT = 0x70000032;

pub const DT_MIPS_RWPLT = 0x70000034;
pub const DT_MIPS_RLD_MAP_REL = 0x70000035;
pub const DT_MIPS_NUM = 0x36;

pub const DT_ALPHA_PLTRO = (DT_LOPROC + 0);
pub const DT_ALPHA_NUM = 1;

pub const DT_PPC_GOT = (DT_LOPROC + 0);
pub const DT_PPC_OPT = (DT_LOPROC + 1);
pub const DT_PPC_NUM = 2;

pub const DT_PPC64_GLINK = (DT_LOPROC + 0);
pub const DT_PPC64_OPD = (DT_LOPROC + 1);
pub const DT_PPC64_OPDSZ = (DT_LOPROC + 2);
pub const DT_PPC64_OPT = (DT_LOPROC + 3);
pub const DT_PPC64_NUM = 4;

pub const DT_IA_64_PLT_RESERVE = (DT_LOPROC + 0);
pub const DT_IA_64_NUM = 1;

pub const DT_NIOS2_GP = 0x70000002;

pub const DF_ORIGIN = 0x00000001;
pub const DF_SYMBOLIC = 0x00000002;
pub const DF_TEXTREL = 0x00000004;
pub const DF_BIND_NOW = 0x00000008;
pub const DF_STATIC_TLS = 0x00000010;

pub const DF_1_NOW = 0x00000001;
pub const DF_1_GLOBAL = 0x00000002;
pub const DF_1_GROUP = 0x00000004;
pub const DF_1_NODELETE = 0x00000008;
pub const DF_1_LOADFLTR = 0x00000010;
pub const DF_1_INITFIRST = 0x00000020;
pub const DF_1_NOOPEN = 0x00000040;
pub const DF_1_ORIGIN = 0x00000080;
pub const DF_1_DIRECT = 0x00000100;
pub const DF_1_TRANS = 0x00000200;
pub const DF_1_INTERPOSE = 0x00000400;
pub const DF_1_NODEFLIB = 0x00000800;
pub const DF_1_NODUMP = 0x00001000;
pub const DF_1_CONFALT = 0x00002000;
pub const DF_1_ENDFILTEE = 0x00004000;
pub const DF_1_DISPRELDNE = 0x00008000;
pub const DF_1_DISPRELPND = 0x00010000;
pub const DF_1_NODIRECT = 0x00020000;
pub const DF_1_IGNMULDEF = 0x00040000;
pub const DF_1_NOKSYMS = 0x00080000;
pub const DF_1_NOHDR = 0x00100000;
pub const DF_1_EDITED = 0x00200000;
pub const DF_1_NORELOC = 0x00400000;
pub const DF_1_SYMINTPOSE = 0x00800000;
pub const DF_1_GLOBAUDIT = 0x01000000;
pub const DF_1_SINGLETON = 0x02000000;
pub const DF_1_STUB = 0x04000000;
pub const DF_1_PIE = 0x08000000;

pub const Versym = packed struct(u16) {
    VERSION: u15,
    HIDDEN: bool,

    pub const LOCAL: Versym = @bitCast(@intFromEnum(VER_NDX.LOCAL));
    pub const GLOBAL: Versym = @bitCast(@intFromEnum(VER_NDX.GLOBAL));
};

pub const VER_NDX = enum(u16) {
    /// Symbol is local
    LOCAL = 0,
    /// Symbol is global
    GLOBAL = 1,
    /// Beginning of reserved entries
    LORESERVE = 0xff00,
    /// Symbol is to be eliminated
    ELIMINATE = 0xff01,
    UNSPECIFIED = 0xffff,
    _,
};

/// Version definition of the file itself
pub const VER_FLG_BASE = 1;
/// Weak version identifier
pub const VER_FLG_WEAK = 2;

/// Deprecated, use `@intFromEnum(std.elf.PT.NULL)`
pub const PT_NULL = @intFromEnum(std.elf.PT.NULL);
/// Deprecated, use `@intFromEnum(std.elf.PT.LOAD)`
pub const PT_LOAD = @intFromEnum(std.elf.PT.LOAD);
/// Deprecated, use `@intFromEnum(std.elf.PT.DYNAMIC)`
pub const PT_DYNAMIC = @intFromEnum(std.elf.PT.DYNAMIC);
/// Deprecated, use `@intFromEnum(std.elf.PT.INTERP)`
pub const PT_INTERP = @intFromEnum(std.elf.PT.INTERP);
/// Deprecated, use `@intFromEnum(std.elf.PT.NOTE)`
pub const PT_NOTE = @intFromEnum(std.elf.PT.NOTE);
/// Deprecated, use `@intFromEnum(std.elf.PT.SHLIB)`
pub const PT_SHLIB = @intFromEnum(std.elf.PT.SHLIB);
/// Deprecated, use `@intFromEnum(std.elf.PT.PHDR)`
pub const PT_PHDR = @intFromEnum(std.elf.PT.PHDR);
/// Deprecated, use `@intFromEnum(std.elf.PT.TLS)`
pub const PT_TLS = @intFromEnum(std.elf.PT.TLS);
/// Deprecated, use `std.elf.PT.NUM`.
pub const PT_NUM = PT.NUM;
/// Deprecated, use `@intFromEnum(std.elf.PT.LOOS)`
pub const PT_LOOS = @intFromEnum(std.elf.PT.LOOS);
/// Deprecated, use `@intFromEnum(std.elf.PT.GNU_EH_FRAME)`
pub const PT_GNU_EH_FRAME = @intFromEnum(std.elf.PT.GNU_EH_FRAME);
/// Deprecated, use `@intFromEnum(std.elf.PT.GNU_STACK)`
pub const PT_GNU_STACK = @intFromEnum(std.elf.PT.GNU_STACK);
/// Deprecated, use `@intFromEnum(std.elf.PT.GNU_RELRO)`
pub const PT_GNU_RELRO = @intFromEnum(std.elf.PT.GNU_RELRO);
/// Deprecated, use `@intFromEnum(std.elf.PT.LOSUNW)`
pub const PT_LOSUNW = @intFromEnum(std.elf.PT.LOSUNW);
/// Deprecated, use `@intFromEnum(std.elf.PT.SUNWBSS)`
pub const PT_SUNWBSS = @intFromEnum(std.elf.PT.SUNWBSS);
/// Deprecated, use `@intFromEnum(std.elf.PT.SUNWSTACK)`
pub const PT_SUNWSTACK = @intFromEnum(std.elf.PT.SUNWSTACK);
/// Deprecated, use `@intFromEnum(std.elf.PT.HISUNW)`
pub const PT_HISUNW = @intFromEnum(std.elf.PT.HISUNW);
/// Deprecated, use `@intFromEnum(std.elf.PT.HIOS)`
pub const PT_HIOS = @intFromEnum(std.elf.PT.HIOS);
/// Deprecated, use `@intFromEnum(std.elf.PT.LOPROC)`
pub const PT_LOPROC = @intFromEnum(std.elf.PT.LOPROC);
/// Deprecated, use `@intFromEnum(std.elf.PT.HIPROC)`
pub const PT_HIPROC = @intFromEnum(std.elf.PT.HIPROC);

pub const PN_XNUM = 0xffff;

/// Deprecated, use `@intFromEnum(std.elf.SHT.NULL)`
pub const SHT_NULL = @intFromEnum(std.elf.SHT.NULL);
/// Deprecated, use `@intFromEnum(std.elf.SHT.PROGBITS)`
pub const SHT_PROGBITS = @intFromEnum(std.elf.SHT.PROGBITS);
/// Deprecated, use `@intFromEnum(std.elf.SHT.SYMTAB)`
pub const SHT_SYMTAB = @intFromEnum(std.elf.SHT.SYMTAB);
/// Deprecated, use `@intFromEnum(std.elf.SHT.STRTAB)`
pub const SHT_STRTAB = @intFromEnum(std.elf.SHT.STRTAB);
/// Deprecated, use `@intFromEnum(std.elf.SHT.RELA)`
pub const SHT_RELA = @intFromEnum(std.elf.SHT.RELA);
/// Deprecated, use `@intFromEnum(std.elf.SHT.HASH)`
pub const SHT_HASH = @intFromEnum(std.elf.SHT.HASH);
/// Deprecated, use `@intFromEnum(std.elf.SHT.DYNAMIC)`
pub const SHT_DYNAMIC = @intFromEnum(std.elf.SHT.DYNAMIC);
/// Deprecated, use `@intFromEnum(std.elf.SHT.NOTE)`
pub const SHT_NOTE = @intFromEnum(std.elf.SHT.NOTE);
/// Deprecated, use `@intFromEnum(std.elf.SHT.NOBITS)`
pub const SHT_NOBITS = @intFromEnum(std.elf.SHT.NOBITS);
/// Deprecated, use `@intFromEnum(std.elf.SHT.REL)`
pub const SHT_REL = @intFromEnum(std.elf.SHT.REL);
/// Deprecated, use `@intFromEnum(std.elf.SHT.SHLIB)`
pub const SHT_SHLIB = @intFromEnum(std.elf.SHT.SHLIB);
/// Deprecated, use `@intFromEnum(std.elf.SHT.DYNSYM)`
pub const SHT_DYNSYM = @intFromEnum(std.elf.SHT.DYNSYM);
/// Deprecated, use `@intFromEnum(std.elf.SHT.INIT_ARRAY)`
pub const SHT_INIT_ARRAY = @intFromEnum(std.elf.SHT.INIT_ARRAY);
/// Deprecated, use `@intFromEnum(std.elf.SHT.FINI_ARRAY)`
pub const SHT_FINI_ARRAY = @intFromEnum(std.elf.SHT.FINI_ARRAY);
/// Deprecated, use `@intFromEnum(std.elf.SHT.PREINIT_ARRAY)`
pub const SHT_PREINIT_ARRAY = @intFromEnum(std.elf.SHT.PREINIT_ARRAY);
/// Deprecated, use `@intFromEnum(std.elf.SHT.GROUP)`
pub const SHT_GROUP = @intFromEnum(std.elf.SHT.GROUP);
/// Deprecated, use `@intFromEnum(std.elf.SHT.SYMTAB_SHNDX)`
pub const SHT_SYMTAB_SHNDX = @intFromEnum(std.elf.SHT.SYMTAB_SHNDX);
/// Deprecated, use `@intFromEnum(std.elf.SHT.RELR)`
pub const SHT_RELR = @intFromEnum(std.elf.SHT.RELR);
/// Deprecated, use `std.elf.SHT.NUM`.
pub const SHT_NUM = SHT.NUM;
/// Deprecated, use `@intFromEnum(std.elf.SHT.LOOS)`
pub const SHT_LOOS = @intFromEnum(std.elf.SHT.LOOS);
/// Deprecated, use `@intFromEnum(std.elf.SHT.LLVM_ADDRSIG)`
pub const SHT_LLVM_ADDRSIG = @intFromEnum(std.elf.SHT.LLVM_ADDRSIG);
/// Deprecated, use `@intFromEnum(std.elf.SHT.GNU_HASH)`
pub const SHT_GNU_HASH = @intFromEnum(std.elf.SHT.GNU_HASH);
/// Deprecated, use `@intFromEnum(std.elf.SHT.GNU_VERDEF)`
pub const SHT_GNU_VERDEF = @intFromEnum(std.elf.SHT.GNU_VERDEF);
/// Deprecated, use `@intFromEnum(std.elf.SHT.GNU_VERNEED)`
pub const SHT_GNU_VERNEED = @intFromEnum(std.elf.SHT.GNU_VERNEED);
/// Deprecated, use `@intFromEnum(std.elf.SHT.GNU_VERSYM)`
pub const SHT_GNU_VERSYM = @intFromEnum(std.elf.SHT.GNU_VERSYM);
/// Deprecated, use `@intFromEnum(std.elf.SHT.HIOS)`
pub const SHT_HIOS = @intFromEnum(std.elf.SHT.HIOS);
/// Deprecated, use `@intFromEnum(std.elf.SHT.LOPROC)`
pub const SHT_LOPROC = @intFromEnum(std.elf.SHT.LOPROC);
/// Deprecated, use `@intFromEnum(std.elf.SHT.X86_64_UNWIND)`
pub const SHT_X86_64_UNWIND = @intFromEnum(std.elf.SHT.X86_64_UNWIND);
/// Deprecated, use `@intFromEnum(std.elf.SHT.HIPROC)`
pub const SHT_HIPROC = @intFromEnum(std.elf.SHT.HIPROC);
/// Deprecated, use `@intFromEnum(std.elf.SHT.LOUSER)`
pub const SHT_LOUSER = @intFromEnum(std.elf.SHT.LOUSER);
/// Deprecated, use `@intFromEnum(std.elf.SHT.HIUSER)`
pub const SHT_HIUSER = @intFromEnum(std.elf.SHT.HIUSER);

// Note type for .note.gnu.build_id
pub const NT_GNU_BUILD_ID = 3;

/// Deprecated, use `@intFromEnum(std.elf.STB.LOCAL)`
pub const STB_LOCAL = @intFromEnum(STB.LOCAL);
/// Deprecated, use `@intFromEnum(std.elf.STB.GLOBAL)`
pub const STB_GLOBAL = @intFromEnum(STB.GLOBAL);
/// Deprecated, use `@intFromEnum(std.elf.STB.WEAK)`
pub const STB_WEAK = @intFromEnum(STB.WEAK);
/// Deprecated, use `std.elf.STB.NUM`
pub const STB_NUM = STB.NUM;
/// Deprecated, use `@intFromEnum(std.elf.STB.LOOS)`
pub const STB_LOOS = @intFromEnum(STB.LOOS);
/// Deprecated, use `@intFromEnum(std.elf.STB.GNU_UNIQUE)`
pub const STB_GNU_UNIQUE = @intFromEnum(STB.GNU_UNIQUE);
/// Deprecated, use `@intFromEnum(std.elf.STB.HIOS)`
pub const STB_HIOS = @intFromEnum(STB.HIOS);
/// Deprecated, use `@intFromEnum(std.elf.STB.LOPROC)`
pub const STB_LOPROC = @intFromEnum(STB.LOPROC);
/// Deprecated, use `@intFromEnum(std.elf.STB.HIPROC)`
pub const STB_HIPROC = @intFromEnum(STB.HIPROC);

/// Deprecated, use `@intFromEnum(std.elf.STB.MIPS_SPLIT_COMMON)`
pub const STB_MIPS_SPLIT_COMMON = @intFromEnum(STB.MIBS_SPLIT_COMMON);

/// Deprecated, use `@intFromEnum(std.elf.STT.NOTYPE)`
pub const STT_NOTYPE = @intFromEnum(STT.NOTYPE);
/// Deprecated, use `@intFromEnum(std.elf.STT.OBJECT)`
pub const STT_OBJECT = @intFromEnum(STT.OBJECT);
/// Deprecated, use `@intFromEnum(std.elf.STT.FUNC)`
pub const STT_FUNC = @intFromEnum(STT.FUNC);
/// Deprecated, use `@intFromEnum(std.elf.STT.SECTION)`
pub const STT_SECTION = @intFromEnum(STT.SECTION);
/// Deprecated, use `@intFromEnum(std.elf.STT.FILE)`
pub const STT_FILE = @intFromEnum(STT.FILE);
/// Deprecated, use `@intFromEnum(std.elf.STT.COMMON)`
pub const STT_COMMON = @intFromEnum(STT.COMMON);
/// Deprecated, use `@intFromEnum(std.elf.STT.TLS)`
pub const STT_TLS = @intFromEnum(STT.TLS);
/// Deprecated, use `std.elf.STT.NUM`
pub const STT_NUM = STT.NUM;
/// Deprecated, use `@intFromEnum(std.elf.STT.LOOS)`
pub const STT_LOOS = @intFromEnum(STT.LOOS);
/// Deprecated, use `@intFromEnum(std.elf.STT.GNU_IFUNC)`
pub const STT_GNU_IFUNC = @intFromEnum(STT.GNU_IFUNC);
/// Deprecated, use `@intFromEnum(std.elf.STT.HIOS)`
pub const STT_HIOS = @intFromEnum(STT.HIOS);
/// Deprecated, use `@intFromEnum(std.elf.STT.LOPROC)`
pub const STT_LOPROC = @intFromEnum(STT.LOPROC);
/// Deprecated, use `@intFromEnum(std.elf.STT.HIPROC)`
pub const STT_HIPROC = @intFromEnum(STT.HIPROC);

/// Deprecated, use `@intFromEnum(std.elf.STT.SPARC_REGISTER)`
pub const STT_SPARC_REGISTER = @intFromEnum(STT.SPARC_REGISTER);

/// Deprecated, use `@intFromEnum(std.elf.STT.PARISC_MILLICODE)`
pub const STT_PARISC_MILLICODE = @intFromEnum(STT.PARISC_MILLICODE);

/// Deprecated, use `@intFromEnum(std.elf.STT.HP_OPAQUE)`
pub const STT_HP_OPAQUE = @intFromEnum(STT.HP_OPAQUE);
/// Deprecated, use `@intFromEnum(std.elf.STT.HP_STUB)`
pub const STT_HP_STUB = @intFromEnum(STT.HP_STUB);

/// Deprecated, use `@intFromEnum(std.elf.STT.ARM_TFUNC)`
pub const STT_ARM_TFUNC = @intFromEnum(STT.ARM_TFUNC);
/// Deprecated, use `@intFromEnum(std.elf.STT.ARM_16BIT)`
pub const STT_ARM_16BIT = @intFromEnum(STT.ARM_16BIT);

pub const PT = enum(Word) {
    /// Program header table entry unused
    NULL = 0,
    /// Loadable program segment
    LOAD = 1,
    /// Dynamic linking information
    DYNAMIC = 2,
    /// Program interpreter
    INTERP = 3,
    /// Auxiliary information
    NOTE = 4,
    /// Reserved
    SHLIB = 5,
    /// Entry for header table itself
    PHDR = 6,
    /// Thread-local storage segment
    TLS = 7,
    _,

    /// Number of defined types
    pub const NUM = @typeInfo(PT).@"enum".fields.len;

    /// Start of OS-specific
    pub const LOOS: PT = @enumFromInt(0x60000000);
    /// End of OS-specific
    pub const HIOS: PT = @enumFromInt(0x6fffffff);

    /// GCC .eh_frame_hdr segment
    pub const GNU_EH_FRAME: PT = @enumFromInt(0x6474e550);
    /// Indicates stack executability
    pub const GNU_STACK: PT = @enumFromInt(0x6474e551);
    /// Read-only after relocation
    pub const GNU_RELRO: PT = @enumFromInt(0x6474e552);

    pub const LOSUNW: PT = @enumFromInt(0x6ffffffa);
    pub const HISUNW: PT = @enumFromInt(0x6fffffff);

    /// Sun specific segment
    pub const SUNWBSS: PT = @enumFromInt(0x6ffffffa);
    /// Stack segment
    pub const SUNWSTACK: PT = @enumFromInt(0x6ffffffb);

    /// Start of processor-specific
    pub const LOPROC: PT = @enumFromInt(0x70000000);
    /// End of processor-specific
    pub const HIPROC: PT = @enumFromInt(0x7fffffff);
};

pub const SHT = enum(Word) {
    /// Section header table entry unused
    NULL = 0,
    /// Program data
    PROGBITS = 1,
    /// Symbol table
    SYMTAB = 2,
    /// String table
    STRTAB = 3,
    /// Relocation entries with addends
    RELA = 4,
    /// Symbol hash table
    HASH = 5,
    /// Dynamic linking information
    DYNAMIC = 6,
    /// Notes
    NOTE = 7,
    /// Program space with no data (bss)
    NOBITS = 8,
    /// Relocation entries, no addends
    REL = 9,
    /// Reserved
    SHLIB = 10,
    /// Dynamic linker symbol table
    DYNSYM = 11,
    /// Array of constructors
    INIT_ARRAY = 14,
    /// Array of destructors
    FINI_ARRAY = 15,
    /// Array of pre-constructors
    PREINIT_ARRAY = 16,
    /// Section group
    GROUP = 17,
    /// Extended section indices
    SYMTAB_SHNDX = 18,
    /// RELR relative relocations
    RELR = 19,
    _,

    /// Number of defined types
    pub const NUM = @typeInfo(SHT).@"enum".fields.len;

    /// Start of OS-specific
    pub const LOOS: SHT = @enumFromInt(0x60000000);
    /// End of OS-specific
    pub const HIOS: SHT = @enumFromInt(0x6fffffff);

    /// LLVM address-significance table
    pub const LLVM_ADDRSIG: SHT = @enumFromInt(0x6fff4c03);

    /// GNU hash table
    pub const GNU_HASH: SHT = @enumFromInt(0x6ffffff6);
    /// GNU version definition table
    pub const GNU_VERDEF: SHT = @enumFromInt(0x6ffffffd);
    /// GNU needed versions table
    pub const GNU_VERNEED: SHT = @enumFromInt(0x6ffffffe);
    /// GNU symbol version table
    pub const GNU_VERSYM: SHT = @enumFromInt(0x6fffffff);

    /// Start of processor-specific
    pub const LOPROC: SHT = @enumFromInt(0x70000000);
    /// End of processor-specific
    pub const HIPROC: SHT = @enumFromInt(0x7fffffff);

    /// Unwind information
    pub const X86_64_UNWIND: SHT = @enumFromInt(0x70000001);

    /// Start of application-specific
    pub const LOUSER: SHT = @enumFromInt(0x80000000);
    /// End of application-specific
    pub const HIUSER: SHT = @enumFromInt(0xffffffff);
};

pub const STB = enum(u4) {
    /// Local symbol
    LOCAL = 0,
    /// Global symbol
    GLOBAL = 1,
    /// Weak symbol
    WEAK = 2,
    _,

    /// Number of defined types
    pub const NUM = @typeInfo(STB).@"enum".fields.len;

    /// Start of OS-specific
    pub const LOOS: STB = @enumFromInt(10);
    /// End of OS-specific
    pub const HIOS: STB = @enumFromInt(12);

    /// Unique symbol
    pub const GNU_UNIQUE: STB = @enumFromInt(@intFromEnum(LOOS) + 0);

    /// Start of processor-specific
    pub const LOPROC: STB = @enumFromInt(13);
    /// End of processor-specific
    pub const HIPROC: STB = @enumFromInt(15);

    pub const MIPS_SPLIT_COMMON: STB = @enumFromInt(@intFromEnum(LOPROC) + 0);
};

pub const STT = enum(u4) {
    /// Symbol type is unspecified
    NOTYPE = 0,
    /// Symbol is a data object
    OBJECT = 1,
    /// Symbol is a code object
    FUNC = 2,
    /// Symbol associated with a section
    SECTION = 3,
    /// Symbol's name is file name
    FILE = 4,
    /// Symbol is a common data object
    COMMON = 5,
    /// Symbol is thread-local data object
    TLS = 6,
    _,

    /// Number of defined types
    pub const NUM = @typeInfo(STT).@"enum".fields.len;

    /// Start of OS-specific
    pub const LOOS: STT = @enumFromInt(10);
    /// End of OS-specific
    pub const HIOS: STT = @enumFromInt(12);

    /// Symbol is indirect code object
    pub const GNU_IFUNC: STT = @enumFromInt(@intFromEnum(LOOS) + 0);

    pub const HP_OPAQUE: STT = @enumFromInt(@intFromEnum(LOOS) + 1);
    pub const HP_STUB: STT = @enumFromInt(@intFromEnum(LOOS) + 2);

    /// Start of processor-specific
    pub const LOPROC: STT = @enumFromInt(13);
    /// End of processor-specific
    pub const HIPROC: STT = @enumFromInt(15);

    pub const SPARC_REGISTER: STT = @enumFromInt(@intFromEnum(LOPROC) + 0);

    pub const PARISC_MILLICODE: STT = @enumFromInt(@intFromEnum(LOPROC) + 0);

    pub const ARM_TFUNC: STT = @enumFromInt(@intFromEnum(LOPROC) + 0);
    pub const ARM_16BIT: STT = @enumFromInt(@intFromEnum(HIPROC) + 2);
};

pub const STV = enum(u3) {
    DEFAULT = 0,
    INTERNAL = 1,
    HIDDEN = 2,
    PROTECTED = 3,
};

pub const MAGIC = "\x7fELF";

/// File types
pub const ET = enum(u16) {
    /// No file type
    NONE = 0,

    /// Relocatable file
    REL = 1,

    /// Executable file
    EXEC = 2,

    /// Shared object file
    DYN = 3,

    /// Core file
    CORE = 4,

    _,

    /// Beginning of OS-specific codes
    pub const LOOS = 0xfe00;

    /// End of OS-specific codes
    pub const HIOS = 0xfeff;

    /// Beginning of processor-specific codes
    pub const LOPROC = 0xff00;

    /// End of processor-specific codes
    pub const HIPROC = 0xffff;
};

/// All integers are native endian.
pub const Header = struct {
    is_64: bool,
    endian: Endian,
    os_abi: OSABI,
    /// The meaning of this value depends on `os_abi`.
    abi_version: u8,
    type: ET,
    machine: EM,
    entry: u64,
    phoff: u64,
    shoff: u64,
    phentsize: u16,
    phnum: u16,
    shentsize: u16,
    shnum: u16,
    shstrndx: u16,

    pub fn iterateProgramHeaders(h: *const Header, file_reader: *Io.File.Reader) ProgramHeaderIterator {
        return .{
            .is_64 = h.is_64,
            .endian = h.endian,
            .phnum = h.phnum,
            .phoff = h.phoff,
            .file_reader = file_reader,
        };
    }

    pub fn iterateProgramHeadersBuffer(h: *const Header, buf: []const u8) ProgramHeaderBufferIterator {
        return .{
            .is_64 = h.is_64,
            .endian = h.endian,
            .phnum = h.phnum,
            .phoff = h.phoff,
            .buf = buf,
        };
    }

    pub fn iterateSectionHeaders(h: *const Header, file_reader: *Io.File.Reader) SectionHeaderIterator {
        return .{
            .is_64 = h.is_64,
            .endian = h.endian,
            .shnum = h.shnum,
            .shoff = h.shoff,
            .file_reader = file_reader,
        };
    }

    pub fn iterateSectionHeadersBuffer(h: *const Header, buf: []const u8) SectionHeaderBufferIterator {
        return .{
            .is_64 = h.is_64,
            .endian = h.endian,
            .shnum = h.shnum,
            .shoff = h.shoff,
            .buf = buf,
        };
    }

    pub fn iterateDynamicSection(
        h: *const Header,
        file_reader: *Io.File.Reader,
        offset: u64,
        size: u64,
    ) DynamicSectionIterator {
        return .{
            .is_64 = h.is_64,
            .endian = h.endian,
            .offset = offset,
            .end_offset = offset + size,
            .file_reader = file_reader,
        };
    }

    pub fn iterateDynamicSectionBuffer(
        h: *const Header,
        buf: []const u8,
        offset: u64,
        size: u64,
    ) DynamicSectionBufferIterator {
        return .{
            .is_64 = h.is_64,
            .endian = h.endian,
            .offset = offset,
            .end_offset = offset + size,
            .buf = buf,
        };
    }

    pub const ReadError = Io.Reader.Error || error{
        InvalidElfMagic,
        InvalidElfVersion,
        InvalidElfClass,
        InvalidElfEndian,
    };

    /// If this function fails, seek position of `r` is unchanged.
    pub fn read(r: *Io.Reader) ReadError!Header {
        const buf = try r.peek(@sizeOf(Elf64_Ehdr));

        if (!mem.eql(u8, buf[0..4], MAGIC)) return error.InvalidElfMagic;
        if (buf[EI.VERSION] != 1) return error.InvalidElfVersion;

        const endian: Endian = switch (buf[EI.DATA]) {
            ELFDATA2LSB => .little,
            ELFDATA2MSB => .big,
            else => return error.InvalidElfEndian,
        };

        return switch (buf[EI.CLASS]) {
            ELFCLASS32 => .init(try r.takeStruct(Elf32_Ehdr, endian), endian),
            ELFCLASS64 => .init(try r.takeStruct(Elf64_Ehdr, endian), endian),
            else => return error.InvalidElfClass,
        };
    }

    pub fn init(hdr: anytype, endian: Endian) Header {
        // Converting integers to exhaustive enums using `@enumFromInt` could cause a panic.
        comptime assert(!@typeInfo(OSABI).@"enum".is_exhaustive);
        return .{
            .is_64 = switch (@TypeOf(hdr)) {
                Elf32_Ehdr => false,
                Elf64_Ehdr => true,
                else => @compileError("bad type"),
            },
            .endian = endian,
            .os_abi = @enumFromInt(hdr.e_ident[EI.OSABI]),
            .abi_version = hdr.e_ident[EI.ABIVERSION],
            .type = hdr.e_type,
            .machine = hdr.e_machine,
            .entry = hdr.e_entry,
            .phoff = hdr.e_phoff,
            .shoff = hdr.e_shoff,
            .phentsize = hdr.e_phentsize,
            .phnum = hdr.e_phnum,
            .shentsize = hdr.e_shentsize,
            .shnum = hdr.e_shnum,
            .shstrndx = hdr.e_shstrndx,
        };
    }
};

pub const ProgramHeaderIterator = struct {
    is_64: bool,
    endian: Endian,
    phnum: u16,
    phoff: u64,

    file_reader: *Io.File.Reader,
    index: usize = 0,

    pub fn next(it: *ProgramHeaderIterator) !?Elf64_Phdr {
        if (it.index >= it.phnum) return null;
        defer it.index += 1;

        const size: u64 = if (it.is_64) @sizeOf(Elf64_Phdr) else @sizeOf(Elf32_Phdr);
        const offset = it.phoff + size * it.index;
        try it.file_reader.seekTo(offset);

        return try takeProgramHeader(&it.file_reader.interface, it.is_64, it.endian);
    }
};

pub const ProgramHeaderBufferIterator = struct {
    is_64: bool,
    endian: Endian,
    phnum: u16,
    phoff: u64,

    buf: []const u8,
    index: usize = 0,

    pub fn next(it: *ProgramHeaderBufferIterator) !?Elf64_Phdr {
        if (it.index >= it.phnum) return null;
        defer it.index += 1;

        const size: u64 = if (it.is_64) @sizeOf(Elf64_Phdr) else @sizeOf(Elf32_Phdr);
        const offset = it.phoff + size * it.index;
        var reader = Io.Reader.fixed(it.buf[offset..]);

        return try takeProgramHeader(&reader, it.is_64, it.endian);
    }
};

pub fn takeProgramHeader(reader: *Io.Reader, is_64: bool, endian: Endian) !Elf64_Phdr {
    if (is_64) {
        const phdr = try reader.takeStruct(Elf64_Phdr, endian);
        return phdr;
    }

    const phdr = try reader.takeStruct(Elf32_Phdr, endian);
    return .{
        .p_type = phdr.p_type,
        .p_offset = phdr.p_offset,
        .p_vaddr = phdr.p_vaddr,
        .p_paddr = phdr.p_paddr,
        .p_filesz = phdr.p_filesz,
        .p_memsz = phdr.p_memsz,
        .p_flags = phdr.p_flags,
        .p_align = phdr.p_align,
    };
}

pub const SectionHeaderIterator = struct {
    is_64: bool,
    endian: Endian,
    shnum: u16,
    shoff: u64,

    file_reader: *Io.File.Reader,
    index: usize = 0,

    pub fn next(it: *SectionHeaderIterator) !?Elf64_Shdr {
        if (it.index >= it.shnum) return null;
        defer it.index += 1;

        const size: u64 = if (it.is_64) @sizeOf(Elf64_Shdr) else @sizeOf(Elf32_Shdr);
        const offset = it.shoff + size * it.index;
        try it.file_reader.seekTo(offset);

        return try takeSectionHeader(&it.file_reader.interface, it.is_64, it.endian);
    }
};

pub const SectionHeaderBufferIterator = struct {
    is_64: bool,
    endian: Endian,
    shnum: u16,
    shoff: u64,

    buf: []const u8,
    index: usize = 0,

    pub fn next(it: *SectionHeaderBufferIterator) !?Elf64_Shdr {
        if (it.index >= it.shnum) return null;
        defer it.index += 1;

        const size: u64 = if (it.is_64) @sizeOf(Elf64_Shdr) else @sizeOf(Elf32_Shdr);
        const offset = it.shoff + size * it.index;
        if (offset > it.buf.len) return error.EndOfStream;
        var reader = Io.Reader.fixed(it.buf[@intCast(offset)..]);

        return try takeSectionHeader(&reader, it.is_64, it.endian);
    }
};

pub fn takeSectionHeader(reader: *Io.Reader, is_64: bool, endian: Endian) !Elf64_Shdr {
    if (is_64) {
        const shdr = try reader.takeStruct(Elf64_Shdr, endian);
        return shdr;
    }

    const shdr = try reader.takeStruct(Elf32_Shdr, endian);
    return .{
        .sh_name = shdr.sh_name,
        .sh_type = shdr.sh_type,
        .sh_flags = shdr.sh_flags,
        .sh_addr = shdr.sh_addr,
        .sh_offset = shdr.sh_offset,
        .sh_size = shdr.sh_size,
        .sh_link = shdr.sh_link,
        .sh_info = shdr.sh_info,
        .sh_addralign = shdr.sh_addralign,
        .sh_entsize = shdr.sh_entsize,
    };
}

pub const DynamicSectionIterator = struct {
    is_64: bool,
    endian: Endian,
    offset: u64,
    end_offset: u64,

    file_reader: *Io.File.Reader,

    pub fn next(it: *DynamicSectionIterator) !?Elf64_Dyn {
        if (it.offset >= it.end_offset) return null;
        const size: u64 = if (it.is_64) @sizeOf(Elf64_Dyn) else @sizeOf(Elf32_Dyn);
        defer it.offset += size;
        try it.file_reader.seekTo(it.offset);
        return try takeDynamicSection(&it.file_reader.interface, it.is_64, it.endian);
    }
};

pub const DynamicSectionBufferIterator = struct {
    is_64: bool,
    endian: Endian,
    offset: u64,
    end_offset: u64,

    buf: []const u8,

    pub fn next(it: *DynamicSectionBufferIterator) !?Elf64_Dyn {
        if (it.offset >= it.end_offset) return null;
        const size: u64 = if (it.is_64) @sizeOf(Elf64_Dyn) else @sizeOf(Elf32_Dyn);
        defer it.offset += size;
        var reader: std.Io.Reader = .fixed(it.buf[it.offset..]);
        return try takeDynamicSection(&reader, it.is_64, it.endian);
    }
};

pub fn takeDynamicSection(reader: *Io.Reader, is_64: bool, endian: Endian) !Elf64_Dyn {
    if (is_64) {
        const dyn = try reader.takeStruct(Elf64_Dyn, endian);
        return dyn;
    }

    const dyn = try reader.takeStruct(Elf32_Dyn, endian);
    return .{
        .d_tag = dyn.d_tag,
        .d_val = dyn.d_val,
    };
}

pub const EI = struct {
    pub const CLASS = 4;
    pub const DATA = 5;
    pub const VERSION = 6;
    pub const OSABI = 7;
    pub const ABIVERSION = 8;
    pub const PAD = 9;
    pub const NIDENT = 16;
};

/// Deprecated, use `std.elf.EI.CLASS`
pub const EI_CLASS = EI.CLASS;
/// Deprecated, use `std.elf.EI.DATA`
pub const EI_DATA = EI.DATA;
/// Deprecated, use `std.elf.EI.VERSION`
pub const EI_VERSION = EI.VERSION;
/// Deprecated, use `std.elf.EI.OSABI`
pub const EI_OSABI = EI.OSABI;
/// Deprecated, use `std.elf.EI.ABIVERSION`
pub const EI_ABIVERSION = EI.ABIVERSION;
/// Deprecated, use `std.elf.EI.PAD`
pub const EI_PAD = EI.PAD;
/// Deprecated, use `std.elf.EI.NIDENT`
pub const EI_NIDENT = EI.NIDENT;

pub const Half = u16;
pub const Word = u32;
pub const Sword = i32;
pub const Xword = u64;
pub const Sxword = i64;
pub const Section = u16;
pub const Elf32 = struct {
    pub const Addr = u32;
    pub const Off = u32;
    pub const Ehdr = extern struct {
        ident: [EI.NIDENT]u8,
        type: ET,
        machine: EM,
        version: Word,
        entry: Elf32.Addr,
        phoff: Elf32.Off,
        shoff: Elf32.Off,
        flags: Word,
        ehsize: Half,
        phentsize: Half,
        phnum: Half,
        shentsize: Half,
        shnum: Half,
        shstrndx: Half,
    };
    pub const Phdr = extern struct {
        type: PT,
        offset: Elf32.Off,
        vaddr: Elf32.Addr,
        paddr: Elf32.Addr,
        filesz: Word,
        memsz: Word,
        flags: PF,
        @"align": Word,
    };
    pub const Shdr = extern struct {
        name: Word,
        type: SHT,
        flags: packed struct(Word) { shf: SHF },
        addr: Elf32.Addr,
        offset: Elf32.Off,
        size: Word,
        link: Word,
        info: Word,
        addralign: Word,
        entsize: Word,
    };
    pub const Chdr = extern struct {
        type: COMPRESS,
        size: Word,
        addralign: Word,
    };
    pub const Sym = extern struct {
        name: Word,
        value: Elf32.Addr,
        size: Word,
        info: Info,
        other: Other,
        shndx: Section,

        pub const Info = packed struct(u8) {
            type: STT,
            bind: STB,
        };

        pub const Other = packed struct(u8) {
            visibility: STV,
            unused: u5 = 0,
        };
    };
    pub const Rel = extern struct {
        offset: Elf32.Addr,
        info: Info,
        addend: u0 = 0,

        pub const Info = packed struct(u32) {
            type: u8,
            sym: u24,
        };
    };
    pub const Rela = extern struct {
        offset: Elf32.Addr,
        info: Info,
        addend: i32,

        pub const Info = Elf32.Rel.Info;
    };
    comptime {
        assert(@sizeOf(Elf32.Ehdr) == 52);
        assert(@sizeOf(Elf32.Phdr) == 32);
        assert(@sizeOf(Elf32.Shdr) == 40);
        assert(@sizeOf(Elf32.Sym) == 16);
        assert(@sizeOf(Elf32.Rel) == 8);
        assert(@sizeOf(Elf32.Rela) == 12);
    }
};
pub const Elf64 = struct {
    pub const Addr = u64;
    pub const Off = u64;
    pub const Ehdr = extern struct {
        ident: [EI.NIDENT]u8,
        type: ET,
        machine: EM,
        version: Word,
        entry: Elf64.Addr,
        phoff: Elf64.Off,
        shoff: Elf64.Off,
        flags: Word,
        ehsize: Half,
        phentsize: Half,
        phnum: Half,
        shentsize: Half,
        shnum: Half,
        shstrndx: Half,
    };
    pub const Phdr = extern struct {
        type: PT,
        flags: PF,
        offset: Elf64.Off,
        vaddr: Elf64.Addr,
        paddr: Elf64.Addr,
        filesz: Xword,
        memsz: Xword,
        @"align": Xword,
    };
    pub const Shdr = extern struct {
        name: Word,
        type: SHT,
        flags: packed struct(Xword) { shf: SHF, unused: Word = 0 },
        addr: Elf64.Addr,
        offset: Elf64.Off,
        size: Xword,
        link: Word,
        info: Word,
        addralign: Xword,
        entsize: Xword,
    };
    pub const Chdr = extern struct {
        type: COMPRESS,
        reserved: Word = 0,
        size: Xword,
        addralign: Xword,
    };
    pub const Sym = extern struct {
        name: Word,
        info: Info,
        other: Other,
        shndx: Section,
        value: Elf64.Addr,
        size: Xword,

        pub const Info = Elf32.Sym.Info;
        pub const Other = Elf32.Sym.Other;
    };
    pub const Rel = extern struct {
        offset: Elf64.Addr,
        info: Info,
        addend: u0 = 0,

        pub const Info = packed struct(u64) {
            type: u32,
            sym: u32,
        };
    };
    pub const Rela = extern struct {
        offset: Elf64.Addr,
        info: Info,
        addend: i64,

        pub const Info = Elf64.Rel.Info;
    };
    comptime {
        assert(@sizeOf(Elf64.Ehdr) == 64);
        assert(@sizeOf(Elf64.Phdr) == 56);
        assert(@sizeOf(Elf64.Shdr) == 64);
        assert(@sizeOf(Elf64.Sym) == 24);
        assert(@sizeOf(Elf64.Rel) == 16);
        assert(@sizeOf(Elf64.Rela) == 24);
    }
};
pub const ElfN = switch (@sizeOf(usize)) {
    4 => Elf32,
    8 => Elf64,
    else => @compileError("expected pointer size of 32 or 64"),
};

/// Deprecated, use `std.elf.Xword`
pub const Elf32_Xword = Xword;
/// Deprecated, use `std.elf.Sxword`
pub const Elf32_Sxword = Sxword;
/// Deprecated, use `std.elf.Xword`
pub const Elf64_Xword = Xword;
/// Deprecated, use `std.elf.Sxword`
pub const Elf64_Sxword = i64;
/// Deprecated, use `std.elf.Elf32.Addr`
pub const Elf32_Addr = u32;
/// Deprecated, use `std.elf.Elf64.Addr`
pub const Elf64_Addr = u64;
/// Deprecated, use `std.elf.Elf32.Off`
pub const Elf32_Off = u32;
/// Deprecated, use `std.elf.Elf64.Off`
pub const Elf64_Off = u64;
/// Deprecated, use `std.elf.Section`
pub const Elf32_Section = u16;
/// Deprecated, use `std.elf.Section`
pub const Elf64_Section = u16;
/// Deprecated, use `std.elf.Elf32.Ehdr`
pub const Elf32_Ehdr = extern struct {
    e_ident: [EI_NIDENT]u8,
    e_type: ET,
    e_machine: EM,
    e_version: Word,
    e_entry: Elf32_Addr,
    e_phoff: Elf32_Off,
    e_shoff: Elf32_Off,
    e_flags: Word,
    e_ehsize: Half,
    e_phentsize: Half,
    e_phnum: Half,
    e_shentsize: Half,
    e_shnum: Half,
    e_shstrndx: Half,
};
/// Deprecated, use `std.elf.Elf64.Ehdr`
pub const Elf64_Ehdr = extern struct {
    e_ident: [EI.NIDENT]u8,
    e_type: ET,
    e_machine: EM,
    e_version: Word,
    e_entry: Elf64_Addr,
    e_phoff: Elf64_Off,
    e_shoff: Elf64_Off,
    e_flags: Word,
    e_ehsize: Half,
    e_phentsize: Half,
    e_phnum: Half,
    e_shentsize: Half,
    e_shnum: Half,
    e_shstrndx: Half,
};
/// Deprecated, use `std.elf.Elf32.Phdr`
pub const Elf32_Phdr = extern struct {
    p_type: Word,
    p_offset: Elf32_Off,
    p_vaddr: Elf32_Addr,
    p
```
