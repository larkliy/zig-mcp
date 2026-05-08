```
            .AGAIN => return error.ThreadQuotaExceeded,
            .INVAL => unreachable,
            .NOMEM => return error.SystemResources,
            .NOSPC => unreachable,
            .PERM => unreachable,
            .USERS => unreachable,
            else => |err| return posix.unexpectedErrno(err),
        }
    }

    fn getHandle(self: Impl) ThreadHandle {
        return self.thread.parent_tid;
    }

    fn detach(self: Impl) void {
        switch (self.thread.completion.swap(.detached, .seq_cst)) {
            .running => {},
            .completed => self.join(),
            .detached => unreachable,
        }
    }

    fn join(self: Impl) void {
        defer posix.munmap(self.thread.mapped);

        while (true) {
            const tid = self.thread.child_tid.load(.seq_cst);
            if (tid == 0) break;

            switch (linux.errno(linux.futex_4arg(
                &self.thread.child_tid.raw,
                .{ .cmd = .WAIT, .private = false },
                @bitCast(tid),
                null,
            ))) {
                .SUCCESS => continue,
                .INTR => continue,
                .AGAIN => continue,
                else => unreachable,
            }
        }
    }
};

fn testThreadName(io: Io, thread: *Thread) !void {
    const testCases = &[_][]const u8{
        "mythread",
        "b" ** max_name_len,
    };

    inline for (testCases) |tc| {
        try thread.setName(io, tc);

        var name_buffer: [max_name_len:0]u8 = undefined;

        const name = try thread.getName(&name_buffer);
        if (name) |value| {
            try std.testing.expectEqual(tc.len, value.len);
            try std.testing.expectEqualStrings(tc, value);
        }
    }
}

test "setName, getName" {
    if (builtin.single_threaded) return error.SkipZigTest;

    const io = testing.io;

    const Context = struct {
        start_wait_event: Io.Event = .unset,
        test_done_event: Io.Event = .unset,
        thread_done_event: Io.Event = .unset,

        done: std.atomic.Value(bool) = std.atomic.Value(bool).init(false),
        thread: Thread = undefined,

        pub fn run(ctx: *@This()) !void {
            // Wait for the main thread to have set the thread field in the context.
            try ctx.start_wait_event.wait(io);

            switch (native_os) {
                .windows => testThreadName(io, &ctx.thread) catch |err| switch (err) {
                    error.Unsupported => return error.SkipZigTest,
                    else => return err,
                },
                else => try testThreadName(io, &ctx.thread),
            }

            // Signal our test is done
            ctx.test_done_event.set(io);

            // wait for the thread to property exit
            try ctx.thread_done_event.wait(io);
        }
    };

    var context = Context{};
    var thread = try spawn(.{}, Context.run, .{&context});

    context.thread = thread;
    context.start_wait_event.set(io);
    try context.test_done_event.wait(io);

    switch (native_os) {
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => {
            const res = thread.setName(io, "foobar");
            try std.testing.expectError(error.Unsupported, res);
        },
        .windows => testThreadName(io, &thread) catch |err| switch (err) {
            error.Unsupported => return error.SkipZigTest,
            else => return err,
        },
        else => try testThreadName(io, &thread),
    }

    context.thread_done_event.set(io);
    thread.join();
}

fn testIncrementNotify(io: Io, value: *usize, event: *Io.Event) void {
    value.* += 1;
    event.set(io);
}

test join {
    if (builtin.single_threaded) return error.SkipZigTest;

    const io = testing.io;

    var value: usize = 0;
    var event: Io.Event = .unset;

    const thread = try Thread.spawn(.{}, testIncrementNotify, .{ io, &value, &event });
    thread.join();

    try std.testing.expectEqual(value, 1);
}

test detach {
    if (builtin.single_threaded) return error.SkipZigTest;

    const io = testing.io;

    var value: usize = 0;
    var event: Io.Event = .unset;

    const thread = try Thread.spawn(.{}, testIncrementNotify, .{ io, &value, &event });
    thread.detach();

    try event.wait(io);
    try std.testing.expectEqual(value, 1);
}

test "Thread.getCpuCount" {
    if (native_os == .wasi) return error.SkipZigTest;

    const cpu_count = try Thread.getCpuCount();
    try std.testing.expect(cpu_count >= 1);
}

fn testThreadIdFn(thread_id: *Thread.Id) void {
    thread_id.* = Thread.getCurrentId();
}

test "Thread.getCurrentId" {
    if (builtin.single_threaded) return error.SkipZigTest;

    var thread_current_id: Thread.Id = undefined;
    const thread = try Thread.spawn(.{}, testThreadIdFn, .{&thread_current_id});
    thread.join();
    try std.testing.expect(Thread.getCurrentId() != thread_current_id);
}

test "thread local storage" {
    if (builtin.single_threaded) return error.SkipZigTest;
    if (@sizeOf(usize) == 4) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/25498

    const thread1 = try Thread.spawn(.{}, testTls, .{});
    const thread2 = try Thread.spawn(.{}, testTls, .{});
    try testTls();
    thread1.join();
    thread2.join();
}

threadlocal var x: i32 = 1234;
fn testTls() !void {
    if (x != 1234) return error.TlsBadStartValue;
    x += 1;
    if (x != 1235) return error.TlsBadEndValue;
}

/// Configures the per-thread alternative signal stack requested by `std.options.signal_stack_size`.
pub fn maybeAttachSignalStack() void {
    const size = std.options.signal_stack_size orelse return;
    switch (builtin.target.os.tag) {
        // TODO: Windows vectored exception handlers always run on the main stack, but we could use
        // some target-specific inline assembly to swap the stack pointer.
        .windows => return,
        .wasi => return,
        else => {},
    }
    const global = struct {
        threadlocal var signal_stack: [size]u8 = undefined;
    };
    std.posix.sigaltstack(&.{
        .sp = &global.signal_stack,
        .flags = 0,
        .size = size,
    }, null) catch |err| switch (err) {
        error.SizeTooSmall => unreachable, // `std.options.signal_stack_size` must be sufficient for the target
        error.PermissionDenied => unreachable, // called `maybeAttachSignalStack` from a signal handler
        error.Unexpected => unreachable,
    };
}



---
File: /std/time.zig
---

pub const epoch = @import("time/epoch.zig");

// Divisions of a nanosecond.
pub const ns_per_us = 1000;
pub const ns_per_ms = 1000 * ns_per_us;
pub const ns_per_s = 1000 * ns_per_ms;
pub const ns_per_min = 60 * ns_per_s;
pub const ns_per_hour = 60 * ns_per_min;
pub const ns_per_day = 24 * ns_per_hour;
pub const ns_per_week = 7 * ns_per_day;

// Divisions of a microsecond.
pub const us_per_ms = 1000;
pub const us_per_s = 1000 * us_per_ms;
pub const us_per_min = 60 * us_per_s;
pub const us_per_hour = 60 * us_per_min;
pub const us_per_day = 24 * us_per_hour;
pub const us_per_week = 7 * us_per_day;

// Divisions of a millisecond.
pub const ms_per_s = 1000;
pub const ms_per_min = 60 * ms_per_s;
pub const ms_per_hour = 60 * ms_per_min;
pub const ms_per_day = 24 * ms_per_hour;
pub const ms_per_week = 7 * ms_per_day;

// Divisions of a second.
pub const s_per_min = 60;
pub const s_per_hour = s_per_min * 60;
pub const s_per_day = s_per_hour * 24;
pub const s_per_week = s_per_day * 7;

test {
    _ = epoch;
}



---
File: /std/treap.zig
---

const std = @import("std.zig");
const assert = std.debug.assert;
const testing = std.testing;
const Order = std.math.Order;

pub fn Treap(comptime Key: type, comptime compareFn: anytype) type {
    return struct {
        const Self = @This();

        // Allow for compareFn to be fn (anytype, anytype) anytype
        // which allows the convenient use of std.math.order.
        fn compare(a: Key, b: Key) Order {
            return compareFn(a, b);
        }

        root: ?*Node = null,
        prng: Prng = .{},

        /// A customized pseudo random number generator for the treap.
        /// This just helps reducing the memory size of the treap itself
        /// as std.Random.DefaultPrng requires larger state (while producing better entropy for randomness to be fair).
        const Prng = struct {
            xorshift: usize = 0,

            fn random(self: *Prng, seed: usize) usize {
                // Lazily seed the prng state
                if (self.xorshift == 0) {
                    self.xorshift = seed;
                }

                // Since we're using usize, decide the shifts by the integer's bit width.
                const shifts = switch (@bitSizeOf(usize)) {
                    64 => .{ 13, 7, 17 },
                    32 => .{ 13, 17, 5 },
                    16 => .{ 7, 9, 8 },
                    else => @compileError("platform not supported"),
                };

                self.xorshift ^= self.xorshift >> shifts[0];
                self.xorshift ^= self.xorshift << shifts[1];
                self.xorshift ^= self.xorshift >> shifts[2];

                assert(self.xorshift != 0);
                return self.xorshift;
            }
        };

        /// A Node represents an item or point in the treap with a uniquely associated key.
        pub const Node = struct {
            key: Key,
            priority: usize,
            parent: ?*Node,
            children: [2]?*Node,

            pub fn next(node: *Node) ?*Node {
                return nextOnDirection(node, 1);
            }
            pub fn prev(node: *Node) ?*Node {
                return nextOnDirection(node, 0);
            }
        };

        fn extremeInSubtreeOnDirection(node: *Node, direction: u1) *Node {
            var cur = node;
            while (cur.children[direction]) |next| cur = next;
            return cur;
        }

        fn nextOnDirection(node: *Node, direction: u1) ?*Node {
            if (node.children[direction]) |child| {
                return extremeInSubtreeOnDirection(child, direction ^ 1);
            }
            var cur = node;
            // Traversing upward until we find `parent` to `cur` is NOT on
            // `direction`, or equivalently, `cur` to `parent` IS on
            // `direction` thus `parent` is the next.
            while (true) {
                if (cur.parent) |parent| {
                    // If `parent -> node` is NOT on `direction`, then
                    // `node -> parent` IS on `direction`
                    if (parent.children[direction] != cur) return parent;
                    cur = parent;
                } else {
                    return null;
                }
            }
        }

        /// Returns the smallest Node by key in the treap if there is one.
        /// Use `getEntryForExisting()` to replace/remove this Node from the treap.
        pub fn getMin(self: Self) ?*Node {
            if (self.root) |root| return extremeInSubtreeOnDirection(root, 0);
            return null;
        }

        /// Returns the largest Node by key in the treap if there is one.
        /// Use `getEntryForExisting()` to replace/remove this Node from the treap.
        pub fn getMax(self: Self) ?*Node {
            if (self.root) |root| return extremeInSubtreeOnDirection(root, 1);
            return null;
        }

        /// Lookup the Entry for the given key in the treap.
        /// The Entry act's as a slot in the treap to insert/replace/remove the node associated with the key.
        pub fn getEntryFor(self: *Self, key: Key) Entry {
            var parent: ?*Node = undefined;
            const node = self.find(key, &parent);

            return Entry{
                .key = key,
                .treap = self,
                .node = node,
                .context = .{ .inserted_under = parent },
            };
        }

        /// Get an entry for a Node that currently exists in the treap.
        /// It is undefined behavior if the Node is not currently inserted in the treap.
        /// The Entry act's as a slot in the treap to insert/replace/remove the node associated with the key.
        pub fn getEntryForExisting(self: *Self, node: *Node) Entry {
            assert(node.priority != 0);

            return Entry{
                .key = node.key,
                .treap = self,
                .node = node,
                .context = .{ .inserted_under = node.parent },
            };
        }

        /// An Entry represents a slot in the treap associated with a given key.
        pub const Entry = struct {
            /// The associated key for this entry.
            key: Key,
            /// A reference to the treap this entry is apart of.
            treap: *Self,
            /// The current node at this entry.
            node: ?*Node,
            /// The current state of the entry.
            context: union(enum) {
                /// A find() was called for this entry and the position in the treap is known.
                inserted_under: ?*Node,
                /// The entry's node was removed from the treap and a lookup must occur again for modification.
                removed,
            },

            /// Update's the Node at this Entry in the treap with the new node (null for deleting). `new_node`
            /// can have `undefind` content because the value will be initialized internally.
            pub fn set(self: *Entry, new_node: ?*Node) void {
                // Update the entry's node reference after updating the treap below.
                defer self.node = new_node;

                if (self.node) |old| {
                    if (new_node) |new| {
                        self.treap.replace(old, new);
                        return;
                    }

                    self.treap.remove(old);
                    self.context = .removed;
                    return;
                }

                if (new_node) |new| {
                    // A previous treap.remove() could have rebalanced the nodes
                    // so when inserting after a removal, we have to re-lookup the parent again.
                    // This lookup shouldn't find a node because we're yet to insert it..
                    var parent: ?*Node = undefined;
                    switch (self.context) {
                        .inserted_under => |p| parent = p,
                        .removed => assert(self.treap.find(self.key, &parent) == null),
                    }

                    self.treap.insert(self.key, parent, new);
                    self.context = .{ .inserted_under = parent };
                }
            }
        };

        fn find(self: Self, key: Key, parent_ref: *?*Node) ?*Node {
            var node = self.root;
            parent_ref.* = null;

            // basic binary search while tracking the parent.
            while (node) |current| {
                const order = compare(key, current.key);
                if (order == .eq) break;

                parent_ref.* = current;
                node = current.children[@intFromBool(order == .gt)];
            }

            return node;
        }

        fn insert(self: *Self, key: Key, parent: ?*Node, node: *Node) void {
            // generate a random priority & prepare the node to be inserted into the tree
            node.key = key;
            node.priority = self.prng.random(@intFromPtr(node));
            node.parent = parent;
            node.children = [_]?*Node{ null, null };

            // point the parent at the new node
            const link = if (parent) |p| &p.children[@intFromBool(compare(key, p.key) == .gt)] else &self.root;
            assert(link.* == null);
            link.* = node;

            // rotate the node up into the tree to balance it according to its priority
            while (node.parent) |p| {
                if (p.priority <= node.priority) break;

                const is_right = p.children[1] == node;
                assert(p.children[@intFromBool(is_right)] == node);

                const rotate_right = !is_right;
                self.rotate(p, rotate_right);
            }
        }

        fn replace(self: *Self, old: *Node, new: *Node) void {
            // copy over the values from the old node
            new.key = old.key;
            new.priority = old.priority;
            new.parent = old.parent;
            new.children = old.children;

            // point the parent at the new node
            const link = if (old.parent) |p| &p.children[@intFromBool(p.children[1] == old)] else &self.root;
            assert(link.* == old);
            link.* = new;

            // point the children's parent at the new node
            for (old.children) |child_node| {
                const child = child_node orelse continue;
                assert(child.parent == old);
                child.parent = new;
            }
        }

        fn remove(self: *Self, node: *Node) void {
            // rotate the node down to be a leaf of the tree for removal, respecting priorities.
            while (node.children[0] orelse node.children[1]) |_| {
                self.rotate(node, rotate_right: {
                    const right = node.children[1] orelse break :rotate_right true;
                    const left = node.children[0] orelse break :rotate_right false;
                    break :rotate_right (left.priority < right.priority);
                });
            }

            // node is a now a leaf; remove by nulling out the parent's reference to it.
            const link = if (node.parent) |p| &p.children[@intFromBool(p.children[1] == node)] else &self.root;
            assert(link.* == node);
            link.* = null;

            // clean up after ourselves
            node.priority = 0;
            node.parent = null;
            node.children = [_]?*Node{ null, null };
        }

        fn rotate(self: *Self, node: *Node, right: bool) void {
            // if right, converts the following:
            //      parent -> (node (target YY adjacent) XX)
            //      parent -> (target YY (node adjacent XX))
            //
            // if left (!right), converts the following:
            //      parent -> (node (target YY adjacent) XX)
            //      parent -> (target YY (node adjacent XX))
            const parent = node.parent;
            const target = node.children[@intFromBool(!right)] orelse unreachable;
            const adjacent = target.children[@intFromBool(right)];

            // rotate the children
            target.children[@intFromBool(right)] = node;
            node.children[@intFromBool(!right)] = adjacent;

            // rotate the parents
            node.parent = target;
            target.parent = parent;
            if (adjacent) |adj| adj.parent = node;

            // fix the parent link
            const link = if (parent) |p| &p.children[@intFromBool(p.children[1] == node)] else &self.root;
            assert(link.* == node);
            link.* = target;
        }

        /// Usage example:
        ///   var iter = treap.inorderIterator();
        ///   while (iter.next()) |node| {
        ///     ...
        ///   }
        pub const InorderIterator = struct {
            current: ?*Node,

            pub fn next(it: *InorderIterator) ?*Node {
                const current = it.current;
                it.current = if (current) |cur|
                    cur.next()
                else
                    null;
                return current;
            }
        };

        pub fn inorderIterator(self: *Self) InorderIterator {
            return .{ .current = self.getMin() };
        }
    };
}

// For iterating a slice in a random order
// https://lemire.me/blog/2017/09/18/visiting-all-values-in-an-array-exactly-once-in-random-order/
fn SliceIterRandomOrder(comptime T: type) type {
    return struct {
        rng: std.Random,
        slice: []T,
        index: usize = undefined,
        offset: usize = undefined,
        co_prime: usize,

        const Self = @This();

        pub fn init(slice: []T, rng: std.Random) Self {
            return Self{
                .rng = rng,
                .slice = slice,
                .co_prime = blk: {
                    if (slice.len == 0) break :blk 0;
                    var prime = slice.len / 2;
                    while (prime < slice.len) : (prime += 1) {
                        var gcd = [_]usize{ prime, slice.len };
                        while (gcd[1] != 0) {
                            const temp = gcd;
                            gcd = [_]usize{ temp[1], temp[0] % temp[1] };
                        }
                        if (gcd[0] == 1) break;
                    }
                    break :blk prime;
                },
            };
        }

        pub fn reset(self: *Self) void {
            self.index = 0;
            self.offset = self.rng.int(usize);
        }

        pub fn next(self: *Self) ?*T {
            if (self.index >= self.slice.len) return null;
            defer self.index += 1;
            return &self.slice[((self.index *% self.co_prime) +% self.offset) % self.slice.len];
        }
    };
}

const TestTreap = Treap(u64, std.math.order);
const TestNode = TestTreap.Node;

test "insert, find, replace, remove" {
    var treap = TestTreap{};
    var nodes: [10]TestNode = undefined;

    var prng = std.Random.DefaultPrng.init(0xdeadbeef);
    var iter = SliceIterRandomOrder(TestNode).init(&nodes, prng.random());

    // insert check
    iter.reset();
    while (iter.next()) |node| {
        const key = prng.random().int(u64);

        // make sure the current entry is empty.
        var entry = treap.getEntryFor(key);
        try testing.expectEqual(entry.key, key);
        try testing.expectEqual(entry.node, null);

        // insert the entry and make sure the fields are correct.
        entry.set(node);
        try testing.expectEqual(node.key, key);
        try testing.expectEqual(entry.key, key);
        try testing.expectEqual(entry.node, node);
    }

    // find check
    iter.reset();
    while (iter.next()) |node| {
        const key = node.key;

        // find the entry by-key and by-node after having been inserted.
        const entry = treap.getEntryFor(node.key);
        try testing.expectEqual(entry.key, key);
        try testing.expectEqual(entry.node, node);
        try testing.expectEqual(entry.node, treap.getEntryForExisting(node).node);
    }

    // in-order iterator check
    {
        var it = treap.inorderIterator();
        var last_key: u64 = 0;
        while (it.next()) |node| {
            try std.testing.expect(node.key >= last_key);
            last_key = node.key;
        }
    }

    // replace check
    iter.reset();
    while (iter.next()) |node| {
        const key = node.key;

        // find the entry by node since we already know it exists
        var entry = treap.getEntryForExisting(node);
        try testing.expectEqual(entry.key, key);
        try testing.expectEqual(entry.node, node);

        var stub_node: TestNode = undefined;

        // replace the node with a stub_node and ensure future finds point to the stub_node.
        entry.set(&stub_node);
        try testing.expectEqual(entry.node, &stub_node);
        try testing.expectEqual(entry.node, treap.getEntryFor(key).node);
        try testing.expectEqual(entry.node, treap.getEntryForExisting(&stub_node).node);

        // replace the stub_node back to the node and ensure future finds point to the old node.
        entry.set(node);
        try testing.expectEqual(entry.node, node);
        try testing.expectEqual(entry.node, treap.getEntryFor(key).node);
        try testing.expectEqual(entry.node, treap.getEntryForExisting(node).node);
    }

    // remove check
    iter.reset();
    while (iter.next()) |node| {
        const key = node.key;

        // find the entry by node since we already know it exists
        var entry = treap.getEntryForExisting(node);
        try testing.expectEqual(entry.key, key);
        try testing.expectEqual(entry.node, node);

        // remove the node at the entry and ensure future finds point to it being removed.
        entry.set(null);
        try testing.expectEqual(entry.node, null);
        try testing.expectEqual(entry.node, treap.getEntryFor(key).node);

        // insert the node back and ensure future finds point to the inserted node
        entry.set(node);
        try testing.expectEqual(entry.node, node);
        try testing.expectEqual(entry.node, treap.getEntryFor(key).node);
        try testing.expectEqual(entry.node, treap.getEntryForExisting(node).node);

        // remove the node again and make sure it was cleared after the insert
        entry.set(null);
        try testing.expectEqual(entry.node, null);
        try testing.expectEqual(entry.node, treap.getEntryFor(key).node);
    }
}

test "inorderIterator" {
    var treap = TestTreap{};
    var nodes: [10]TestNode = undefined;

    // Build the tree.
    var i: usize = 0;
    while (i < 10) : (i += 1) {
        const key = @as(u64, i);
        var entry = treap.getEntryFor(key);
        entry.set(&nodes[i]);
    }

    // Test the iterator.
    var iter = treap.inorderIterator();
    i = 0;
    while (iter.next()) |node| {
        const key = @as(u64, i);
        try testing.expectEqual(key, node.key);
        i += 1;
    }
}

test "getMin, getMax, simple" {
    var treap = TestTreap{};
    var nodes: [3]TestNode = undefined;

    try testing.expectEqual(null, treap.getMin());
    try testing.expectEqual(null, treap.getMax());
    { // nodes[1]
        var entry = treap.getEntryFor(1);
        entry.set(&nodes[1]);
        try testing.expectEqual(&nodes[1], treap.getMin());
        try testing.expectEqual(&nodes[1], treap.getMax());
    }
    { // nodes[0]
        var entry = treap.getEntryFor(0);
        entry.set(&nodes[0]);
        try testing.expectEqual(&nodes[0], treap.getMin());
        try testing.expectEqual(&nodes[1], treap.getMax());
    }
    { // nodes[2]
        var entry = treap.getEntryFor(2);
        entry.set(&nodes[2]);
        try testing.expectEqual(&nodes[0], treap.getMin());
        try testing.expectEqual(&nodes[2], treap.getMax());
    }
}

test "getMin, getMax, random" {
    var nodes: [100]TestNode = undefined;
    var prng = std.Random.DefaultPrng.init(0xdeadbeef);
    var iter = SliceIterRandomOrder(TestNode).init(&nodes, prng.random());

    var treap = TestTreap{};
    var min: u64 = std.math.maxInt(u64);
    var max: u64 = 0;

    try testing.expectEqual(null, treap.getMin());
    try testing.expectEqual(null, treap.getMax());

    // Insert and check min/max after each insertion.
    iter.reset();
    while (iter.next()) |node| {
        const key = prng.random().int(u64);

        // Insert into `treap`.
        var entry = treap.getEntryFor(key);
        entry.set(node);

        if (key < min) min = key;
        if (key > max) max = key;

        const min_node = treap.getMin().?;
        try std.testing.expectEqual(null, min_node.prev());
        try std.testing.expectEqual(min, min_node.key);

        const max_node = treap.getMax().?;
        try std.testing.expectEqual(null, max_node.next());
        try std.testing.expectEqual(max, max_node.key);
    }
}

test "node.{prev(),next()} with sequential insertion and deletion" {
    // Insert order: 50, 0, 1, 2, ..., 49, 51, 52, ..., 99.
    // Delete order: 0, 1, 2, ..., 49, 51, 52, ..., 99.
    // Check 50's neighbors.
    var treap = TestTreap{};
    var nodes: [100]TestNode = undefined;
    {
        var entry = treap.getEntryFor(50);
        entry.set(&nodes[50]);
        try testing.expectEqual(50, nodes[50].key);
        try testing.expectEqual(null, nodes[50].prev());
        try testing.expectEqual(null, nodes[50].next());
    }
    // Insert others.
    var i: usize = 0;
    while (i < 50) : (i += 1) {
        const key = @as(u64, i);
        const node = &nodes[i];
        var entry = treap.getEntryFor(key);
        entry.set(node);
        try testing.expectEqual(key, node.key);
        try testing.expectEqual(node, nodes[50].prev());
        try testing.expectEqual(null, nodes[50].next());
    }
    i = 51;
    while (i < 100) : (i += 1) {
        const key = @as(u64, i);
        const node = &nodes[i];
        var entry = treap.getEntryFor(key);
        entry.set(node);
        try testing.expectEqual(key, node.key);
        try testing.expectEqual(&nodes[49], nodes[50].prev());
        try testing.expectEqual(&nodes[51], nodes[50].next());
    }
    // Remove others.
    i = 0;
    while (i < 49) : (i += 1) {
        const key = @as(u64, i);
        var entry = treap.getEntryFor(key);
        entry.set(null);
        try testing.expectEqual(&nodes[49], nodes[50].prev());
        try testing.expectEqual(&nodes[51], nodes[50].next());
    }
    { // i = 49.
        const key = @as(u64, i);
        var entry = treap.getEntryFor(key);
        entry.set(null);
        try testing.expectEqual(null, nodes[50].prev());
        try testing.expectEqual(&nodes[51], nodes[50].next());
    }
    i = 51;
    while (i < 99) : (i += 1) {
        const key = @as(u64, i);
        var entry = treap.getEntryFor(key);
        entry.set(null);
        try testing.expectEqual(null, nodes[50].prev());
        try testing.expectEqual(&nodes[i + 1], nodes[50].next());
    }
    { // i = 99.
        const key = @as(u64, i);
        var entry = treap.getEntryFor(key);
        entry.set(null);
        try testing.expectEqual(null, nodes[50].prev());
        try testing.expectEqual(null, nodes[50].next());
    }
}

fn findFirstGreaterOrEqual(array: []u64, value: u64) usize {
    var i: usize = 0;
    while (i < array.len and array[i] < value) i += 1;
    return i;
}

fn testOrderedArrayAndTreapConsistency(array: []u64, treap: *TestTreap) !void {
    var i: usize = 0;
    while (i < array.len) : (i += 1) {
        const value = array[i];

        const entry = treap.getEntryFor(value);
        try testing.expect(entry.node != null);
        const node = entry.node.?;
        try testing.expectEqual(value, node.key);

        if (i == 0) {
            try testing.expectEqual(node.prev(), null);
        } else {
            try testing.expectEqual(node.prev(), treap.getEntryFor(array[i - 1]).node);
        }
        if (i + 1 == array.len) {
            try testing.expectEqual(node.next(), null);
        } else {
            try testing.expectEqual(node.next(), treap.getEntryFor(array[i + 1]).node);
        }
    }
}

test "node.{prev(),next()} with random data" {
    var nodes: [100]TestNode = undefined;
    var prng = std.Random.DefaultPrng.init(0xdeadbeef);
    var iter = SliceIterRandomOrder(TestNode).init(&nodes, prng.random());

    var treap = TestTreap{};
    // A slow, stupid but correct reference. Ordered.
    var golden = std.array_list.Managed(u64).init(std.testing.allocator);
    defer golden.deinit();

    // Insert.
    iter.reset();
    while (iter.next()) |node| {
        const key = prng.random().int(u64);

        // Insert into `golden`.
        const i = findFirstGreaterOrEqual(golden.items, key);
        // Ensure not found. If found: `prng`'s fault.
        try testing.expect(i == golden.items.len or golden.items[i] > key);
        try golden.insert(i, key);

        // Insert into `treap`.
        var entry = treap.getEntryFor(key);
        entry.set(node);

        try testOrderedArrayAndTreapConsistency(golden.items, &treap);
    }

    // Delete.
    iter.reset();
    while (iter.next()) |node| {
        const key = node.key;

        // Delete from `golden`.
        const i = findFirstGreaterOrEqual(golden.items, key);
        try testing.expect(i < golden.items.len);
        _ = golden.orderedRemove(i);

        // Delete from `treap`.
        var entry = treap.getEntryFor(key);
        try testing.expect(entry.node != null);
        entry.set(null);

        try testOrderedArrayAndTreapConsistency(golden.items, &treap);
    }
}



---
File: /std/tz.zig
---

//! The Time Zone Information Format (TZif)
//! https://datatracker.ietf.org/doc/html/rfc8536

const builtin = @import("builtin");

const std = @import("std.zig");
const Reader = std.Io.Reader;
const Allocator = std.mem.Allocator;

pub const Transition = struct {
    ts: i64,
    timetype: *Timetype,
};

pub const Timetype = struct {
    offset: i32,
    flags: u8,
    name_data: [6:0]u8,

    pub fn name(self: *const Timetype) [:0]const u8 {
        return std.mem.sliceTo(self.name_data[0..], 0);
    }

    pub fn isDst(self: Timetype) bool {
        return (self.flags & 0x01) > 0;
    }

    pub fn standardTimeIndicator(self: Timetype) bool {
        return (self.flags & 0x02) > 0;
    }

    pub fn utIndicator(self: Timetype) bool {
        return (self.flags & 0x04) > 0;
    }
};

pub const Leapsecond = struct {
    occurrence: i48,
    correction: i16,
};

pub const Tz = struct {
    allocator: Allocator,
    transitions: []const Transition,
    timetypes: []const Timetype,
    leapseconds: []const Leapsecond,
    footer: ?[]const u8,

    const Header = extern struct {
        magic: [4]u8,
        version: u8,
        reserved: [15]u8,
        counts: extern struct {
            isutcnt: u32,
            isstdcnt: u32,
            leapcnt: u32,
            timecnt: u32,
            typecnt: u32,
            charcnt: u32,
        },
    };

    pub fn parse(allocator: Allocator, reader: *Reader) !Tz {
        const legacy_header = try reader.takeStruct(Header, .big);
        if (!std.mem.eql(u8, &legacy_header.magic, "TZif")) return error.BadHeader;
        if (legacy_header.version != 0 and legacy_header.version != '2' and legacy_header.version != '3')
            return error.BadVersion;

        if (legacy_header.version == 0)
            return parseBlock(allocator, reader, legacy_header, true);

        // If the format is modern, just skip over the legacy data
        const skip_n = legacy_header.counts.timecnt * 5 +
            legacy_header.counts.typecnt * 6 +
            legacy_header.counts.charcnt + legacy_header.counts.leapcnt * 8 +
            legacy_header.counts.isstdcnt + legacy_header.counts.isutcnt;
        try reader.discardAll(skip_n);

        var header = try reader.takeStruct(Header, .big);
        if (!std.mem.eql(u8, &header.magic, "TZif")) return error.BadHeader;
        if (header.version != '2' and header.version != '3') return error.BadVersion;

        return parseBlock(allocator, reader, header, false);
    }

    fn parseBlock(allocator: Allocator, reader: *Reader, header: Header, legacy: bool) !Tz {
        if (header.counts.isstdcnt != 0 and header.counts.isstdcnt != header.counts.typecnt) return error.Malformed; // rfc8536: isstdcnt [...] MUST either be zero or equal to "typecnt"
        if (header.counts.isutcnt != 0 and header.counts.isutcnt != header.counts.typecnt) return error.Malformed; // rfc8536: isutcnt [...] MUST either be zero or equal to "typecnt"
        if (header.counts.typecnt == 0) return error.Malformed; // rfc8536: typecnt [...] MUST NOT be zero
        if (header.counts.charcnt == 0) return error.Malformed; // rfc8536: charcnt [...] MUST NOT be zero
        if (header.counts.charcnt > 256 + 6) return error.Malformed; // Not explicitly banned by rfc8536 but nonsensical

        var leapseconds = try allocator.alloc(Leapsecond, header.counts.leapcnt);
        errdefer allocator.free(leapseconds);
        var transitions = try allocator.alloc(Transition, header.counts.timecnt);
        errdefer allocator.free(transitions);
        var timetypes = try allocator.alloc(Timetype, header.counts.typecnt);
        errdefer allocator.free(timetypes);

        // Parse transition types
        var i: usize = 0;
        while (i < header.counts.timecnt) : (i += 1) {
            transitions[i].ts = if (legacy) try reader.takeInt(i32, .big) else try reader.takeInt(i64, .big);
        }

        i = 0;
        while (i < header.counts.timecnt) : (i += 1) {
            const tt = try reader.takeByte();
            if (tt >= timetypes.len) return error.Malformed; // rfc8536: Each type index MUST be in the range [0, "typecnt" - 1]
            transitions[i].timetype = &timetypes[tt];
        }

        // Parse time types
        i = 0;
        while (i < header.counts.typecnt) : (i += 1) {
            const offset = try reader.takeInt(i32, .big);
            if (offset < -2147483648) return error.Malformed; // rfc8536: utoff [...] MUST NOT be -2**31
            const dst = try reader.takeByte();
            if (dst != 0 and dst != 1) return error.Malformed; // rfc8536: (is)dst [...] The value MUST be 0 or 1.
            const idx = try reader.takeByte();
            if (idx > header.counts.charcnt - 1) return error.Malformed; // rfc8536: (desig)idx [...] Each index MUST be in the range [0, "charcnt" - 1]
            timetypes[i] = .{
                .offset = offset,
                .flags = dst,
                .name_data = undefined,
            };

            // Temporarily cache idx in name_data to be processed after we've read the designator names below
            timetypes[i].name_data[0] = idx;
        }

        var designators_data: [256 + 6]u8 = undefined;
        try reader.readSliceAll(designators_data[0..header.counts.charcnt]);
        const designators = designators_data[0..header.counts.charcnt];
        if (designators[designators.len - 1] != 0) return error.Malformed; // rfc8536: charcnt [...] includes the trailing NUL (0x00) octet

        // Iterate through the timetypes again, setting the designator names
        for (timetypes) |*tt| {
            const name = std.mem.sliceTo(designators[tt.name_data[0]..], 0);
            // We are mandating the "SHOULD" 6-character limit so we can pack the struct better, and to conform to POSIX.
            if (name.len > 6) return error.Malformed; // rfc8536: Time zone designations SHOULD consist of at least three (3) and no more than six (6) ASCII characters.
            @memcpy(tt.name_data[0..name.len], name);
            tt.name_data[name.len] = 0;
        }

        // Parse leap seconds
        i = 0;
        while (i < header.counts.leapcnt) : (i += 1) {
            const occur: i64 = if (legacy) try reader.takeInt(i32, .big) else try reader.takeInt(i64, .big);
            if (occur < 0) return error.Malformed; // rfc8536: occur [...] MUST be nonnegative
            if (i > 0 and leapseconds[i - 1].occurrence + 2419199 > occur) return error.Malformed; // rfc8536: occur [...] each later value MUST be at least 2419199 greater than the previous value
            if (occur > std.math.maxInt(i48)) return error.Malformed; // Unreasonably far into the future

            const corr = try reader.takeInt(i32, .big);
            if (i == 0 and corr != -1 and corr != 1) return error.Malformed; // rfc8536: The correction value in the first leap-second record, if present, MUST be either one (1) or minus one (-1)
            if (i > 0 and leapseconds[i - 1].correction != corr + 1 and leapseconds[i - 1].correction != corr - 1) return error.Malformed; // rfc8536: The correction values in adjacent leap-second records MUST differ by exactly one (1)
            if (corr > std.math.maxInt(i16)) return error.Malformed; // Unreasonably large correction

            leapseconds[i] = .{
                .occurrence = @as(i48, @intCast(occur)),
                .correction = @as(i16, @intCast(corr)),
            };
        }

        // Parse standard/wall indicators
        i = 0;
        while (i < header.counts.isstdcnt) : (i += 1) {
            const stdtime = try reader.takeByte();
            if (stdtime == 1) {
                timetypes[i].flags |= 0x02;
            }
        }

        // Parse UT/local indicators
        i = 0;
        while (i < header.counts.isutcnt) : (i += 1) {
            const ut = try reader.takeByte();
            if (ut == 1) {
                timetypes[i].flags |= 0x04;
                if (!timetypes[i].standardTimeIndicator()) return error.Malformed; // rfc8536: standard/wall value MUST be one (1) if the UT/local value is one (1)
            }
        }

        // Footer
        var footer: ?[]u8 = null;
        if (!legacy) {
            if ((try reader.takeByte()) != '\n') return error.Malformed; // An rfc8536 footer must start with a newline
            const footer_mem = reader.takeSentinel('\n') catch |err| switch (err) {
                error.StreamTooLong => return error.OverlargeFooter, // Read more than 128 bytes, much larger than any reasonable POSIX TZ string
                else => return err,
            };
            if (footer_mem.len != 0) {
                footer = try allocator.dupe(u8, footer_mem);
            }
        }
        errdefer if (footer) |ft| allocator.free(ft);

        return .{
            .allocator = allocator,
            .transitions = transitions,
            .timetypes = timetypes,
            .leapseconds = leapseconds,
            .footer = footer,
        };
    }

    pub fn deinit(self: *Tz) void {
        if (self.footer) |footer| {
            self.allocator.free(footer);
        }
        self.allocator.free(self.leapseconds);
        self.allocator.free(self.transitions);
        self.allocator.free(self.timetypes);
    }
};

test "slim" {
    const data = @embedFile("tz/asia_tokyo.tzif");
    var in_stream: Reader = .fixed(data);

    var tz = try std.Tz.parse(std.testing.allocator, &in_stream);
    defer tz.deinit();

    try std.testing.expectEqual(tz.transitions.len, 9);
    try std.testing.expect(std.mem.eql(u8, tz.transitions[3].timetype.name(), "JDT"));
    try std.testing.expectEqual(tz.transitions[5].ts, -620298000); // 1950-05-06 15:00:00 UTC
    try std.testing.expectEqual(tz.leapseconds[13].occurrence, 567993613); // 1988-01-01 00:00:00 UTC (+23s in TAI, and +13 in the data since it doesn't store the initial 10 second offset)
}

test "fat" {
    const data = @embedFile("tz/antarctica_davis.tzif");
    var in_stream: Reader = .fixed(data);

    var tz = try std.Tz.parse(std.testing.allocator, &in_stream);
    defer tz.deinit();

    try std.testing.expectEqual(tz.transitions.len, 8);
    try std.testing.expect(std.mem.eql(u8, tz.transitions[3].timetype.name(), "+05"));
    try std.testing.expectEqual(tz.transitions[4].ts, 1268251224); // 2010-03-10 20:00:00 UTC
}

test "legacy" {
    // Taken from Slackware 8.0, from 2001
    const data = @embedFile("tz/europe_vatican.tzif");
    var in_stream: Reader = .fixed(data);

    var tz = try std.Tz.parse(std.testing.allocator, &in_stream);
    defer tz.deinit();

    try std.testing.expectEqual(tz.transitions.len, 170);
    try std.testing.expect(std.mem.eql(u8, tz.transitions[69].timetype.name(), "CET"));
    try std.testing.expectEqual(tz.transitions[123].ts, 1414285200); // 2014-10-26 01:00:00 UTC
}



---
File: /std/unicode.zig
---

const std = @import("./std.zig");
const builtin = @import("builtin");
const assert = std.debug.assert;
const testing = std.testing;
const mem = std.mem;
const native_endian = builtin.cpu.arch.endian();
const Allocator = std.mem.Allocator;

/// Use this to replace an unknown, unrecognized, or unrepresentable character.
///
/// See also: https://en.wikipedia.org/wiki/Specials_(Unicode_block)#Replacement_character
pub const replacement_character: u21 = 0xFFFD;
pub const replacement_character_utf8: [3]u8 = utf8EncodeComptime(replacement_character);

/// Returns how many bytes the UTF-8 representation would require
/// for the given codepoint.
pub fn utf8CodepointSequenceLength(c: u21) !u3 {
    if (c < 0x80) return @as(u3, 1);
    if (c < 0x800) return @as(u3, 2);
    if (c < 0x10000) return @as(u3, 3);
    if (c < 0x110000) return @as(u3, 4);
    return error.CodepointTooLarge;
}

/// Given the first byte of a UTF-8 codepoint,
/// returns a number 1-4 indicating the total length of the codepoint in bytes.
/// If this byte does not match the form of a UTF-8 start byte, returns Utf8InvalidStartByte.
pub fn utf8ByteSequenceLength(first_byte: u8) !u3 {
    // The switch is optimized much better than a "smart" approach using @clz
    return switch (first_byte) {
        0b0000_0000...0b0111_1111 => 1,
        0b1100_0000...0b1101_1111 => 2,
        0b1110_0000...0b1110_1111 => 3,
        0b1111_0000...0b1111_0111 => 4,
        else => error.Utf8InvalidStartByte,
    };
}

/// Encodes the given codepoint into a UTF-8 byte sequence.
/// c: the codepoint.
/// out: the out buffer to write to. Must have a len >= utf8CodepointSequenceLength(c).
/// Errors: if c cannot be encoded in UTF-8.
/// Returns: the number of bytes written to out.
pub fn utf8Encode(c: u21, out: []u8) error{ Utf8CannotEncodeSurrogateHalf, CodepointTooLarge }!u3 {
    return utf8EncodeImpl(c, out, .cannot_encode_surrogate_half);
}

const Surrogates = enum {
    cannot_encode_surrogate_half,
    can_encode_surrogate_half,
};

fn utf8EncodeImpl(c: u21, out: []u8, comptime surrogates: Surrogates) !u3 {
    const length = try utf8CodepointSequenceLength(c);
    assert(out.len >= length);
    switch (length) {
        // The pattern for each is the same
        // - Increasing the initial shift by 6 each time
        // - Each time after the first shorten the shifted
        //   value to a max of 0b111111 (63)
        1 => out[0] = @as(u8, @intCast(c)), // Can just do 0 + codepoint for initial range
        2 => {
            out[0] = @as(u8, @intCast(0b11000000 | (c >> 6)));
            out[1] = @as(u8, @intCast(0b10000000 | (c & 0b111111)));
        },
        3 => {
            if (surrogates == .cannot_encode_surrogate_half and isSurrogateCodepoint(c)) {
                return error.Utf8CannotEncodeSurrogateHalf;
            }
            out[0] = @as(u8, @intCast(0b11100000 | (c >> 12)));
            out[1] = @as(u8, @intCast(0b10000000 | ((c >> 6) & 0b111111)));
            out[2] = @as(u8, @intCast(0b10000000 | (c & 0b111111)));
        },
        4 => {
            out[0] = @as(u8, @intCast(0b11110000 | (c >> 18)));
            out[1] = @as(u8, @intCast(0b10000000 | ((c >> 12) & 0b111111)));
            out[2] = @as(u8, @intCast(0b10000000 | ((c >> 6) & 0b111111)));
            out[3] = @as(u8, @intCast(0b10000000 | (c & 0b111111)));
        },
        else => unreachable,
    }
    return length;
}

pub inline fn utf8EncodeComptime(comptime c: u21) [
    utf8CodepointSequenceLength(c) catch |err|
        @compileError(@errorName(err))
]u8 {
    comptime var result: [
        utf8CodepointSequenceLength(c) catch
            unreachable
    ]u8 = undefined;
    comptime assert((utf8Encode(c, &result) catch |err|
        @compileError(@errorName(err))) == result.len);
    return result;
}

const Utf8DecodeError = Utf8Decode2Error || Utf8Decode3Error || Utf8Decode4Error;

/// Deprecated. This function has an awkward API that is too easy to use incorrectly.
pub fn utf8Decode(bytes: []const u8) Utf8DecodeError!u21 {
    return switch (bytes.len) {
        1 => bytes[0],
        2 => utf8Decode2(bytes[0..2].*),
        3 => utf8Decode3(bytes[0..3].*),
        4 => utf8Decode4(bytes[0..4].*),
        else => unreachable,
    };
}

const Utf8Decode2Error = error{
    Utf8ExpectedContinuation,
    Utf8OverlongEncoding,
};
pub fn utf8Decode2(bytes: [2]u8) Utf8Decode2Error!u21 {
    assert(bytes[0] & 0b11100000 == 0b11000000);
    var value: u21 = bytes[0] & 0b00011111;

    if (bytes[1] & 0b11000000 != 0b10000000) return error.Utf8ExpectedContinuation;
    value <<= 6;
    value |= bytes[1] & 0b00111111;

    if (value < 0x80) return error.Utf8OverlongEncoding;

    return value;
}

const Utf8Decode3Error = Utf8Decode3AllowSurrogateHalfError || error{
    Utf8EncodesSurrogateHalf,
};
pub fn utf8Decode3(bytes: [3]u8) Utf8Decode3Error!u21 {
    const value = try utf8Decode3AllowSurrogateHalf(bytes);

    if (0xd800 <= value and value <= 0xdfff) return error.Utf8EncodesSurrogateHalf;

    return value;
}

const Utf8Decode3AllowSurrogateHalfError = error{
    Utf8ExpectedContinuation,
    Utf8OverlongEncoding,
};
pub fn utf8Decode3AllowSurrogateHalf(bytes: [3]u8) Utf8Decode3AllowSurrogateHalfError!u21 {
    assert(bytes[0] & 0b11110000 == 0b11100000);
    var value: u21 = bytes[0] & 0b00001111;

    if (bytes[1] & 0b11000000 != 0b10000000) return error.Utf8ExpectedContinuation;
    value <<= 6;
    value |= bytes[1] & 0b00111111;

    if (bytes[2] & 0b11000000 != 0b10000000) return error.Utf8ExpectedContinuation;
    value <<= 6;
    value |= bytes[2] & 0b00111111;

    if (value < 0x800) return error.Utf8OverlongEncoding;

    return value;
}

const Utf8Decode4Error = error{
    Utf8ExpectedContinuation,
    Utf8OverlongEncoding,
    Utf8CodepointTooLarge,
};
pub fn utf8Decode4(bytes: [4]u8) Utf8Decode4Error!u21 {
    assert(bytes[0] & 0b11111000 == 0b11110000);
    var value: u21 = bytes[0] & 0b00000111;

    if (bytes[1] & 0b11000000 != 0b10000000) return error.Utf8ExpectedContinuation;
    value <<= 6;
    value |= bytes[1] & 0b00111111;

    if (bytes[2] & 0b11000000 != 0b10000000) return error.Utf8ExpectedContinuation;
    value <<= 6;
    value |= bytes[2] & 0b00111111;

    if (bytes[3] & 0b11000000 != 0b10000000) return error.Utf8ExpectedContinuation;
    value <<= 6;
    value |= bytes[3] & 0b00111111;

    if (value < 0x10000) return error.Utf8OverlongEncoding;
    if (value > 0x10FFFF) return error.Utf8CodepointTooLarge;

    return value;
}

/// Returns true if the given unicode codepoint can be encoded in UTF-8.
pub fn utf8ValidCodepoint(value: u21) bool {
    return switch (value) {
        0xD800...0xDFFF => false, // Surrogates range
        0x110000...0x1FFFFF => false, // Above the maximum codepoint value
        else => true,
    };
}

/// Returns the length of a supplied UTF-8 string literal in terms of unicode
/// codepoints.
pub fn utf8CountCodepoints(s: []const u8) !usize {
    var len: usize = 0;

    const N = @sizeOf(usize);
    const MASK = 0x80 * (std.math.maxInt(usize) / 0xff);

    var i: usize = 0;
    while (i < s.len) {
        // Fast path for ASCII sequences
        while (i + N <= s.len) : (i += N) {
            const v = mem.readInt(usize, s[i..][0..N], native_endian);
            if (v & MASK != 0) break;
            len += N;
        }

        if (i < s.len) {
            const n = try utf8ByteSequenceLength(s[i]);
            if (i + n > s.len) return error.TruncatedInput;

            switch (n) {
                1 => {}, // ASCII, no validation needed
                else => _ = try utf8Decode(s[i..][0..n]),
            }

            i += n;
            len += 1;
        }
    }

    return len;
}

/// Returns true if the input consists entirely of UTF-8 codepoints
pub fn utf8ValidateSlice(input: []const u8) bool {
    return utf8ValidateSliceImpl(input, .cannot_encode_surrogate_half);
}

fn utf8ValidateSliceImpl(input: []const u8, comptime surrogates: Surrogates) bool {
    var remaining = input;

    if (std.simd.suggestVectorLength(u8)) |chunk_len| {
        const Chunk = @Vector(chunk_len, u8);

        // Fast path. Check for and skip ASCII characters at the start of the input.
        while (remaining.len >= chunk_len) {
            const chunk: Chunk = remaining[0..chunk_len].*;
            const mask: Chunk = @splat(0x80);
            if (@reduce(.Or, chunk & mask == mask)) {
                // found a non ASCII byte
                break;
            }
            remaining = remaining[chunk_len..];
        }
    }

    // default lowest and highest continuation byte
    const lo_cb = 0b10000000;
    const hi_cb = 0b10111111;

    const min_non_ascii_codepoint = 0x80;

    // The first nibble is used to identify the continuation byte range to
    // accept. The second nibble is the size.
    const xx = 0xF1; // invalid: size 1
    const as = 0xF0; // ASCII: size 1
    const s1 = 0x02; // accept 0, size 2
    const s2 = switch (surrogates) {
        .cannot_encode_surrogate_half => 0x13, // accept 1, size 3
        .can_encode_surrogate_half => 0x03, // accept 0, size 3
    };
    const s3 = 0x03; // accept 0, size 3
    const s4 = switch (surrogates) {
        .cannot_encode_surrogate_half => 0x23, // accept 2, size 3
        .can_encode_surrogate_half => 0x03, // accept 0, size 3
    };
    const s5 = 0x34; // accept 3, size 4
    const s6 = 0x04; // accept 0, size 4
    const s7 = 0x44; // accept 4, size 4

    // Information about the first byte in a UTF-8 sequence.
    const first = comptime ([_]u8{as} ** 128) ++ ([_]u8{xx} ** 64) ++ [_]u8{
        xx, xx, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1,
        s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1, s1,
        s2, s3, s3, s3, s3, s3, s3, s3, s3, s3, s3, s3, s3, s4, s3, s3,
        s5, s6, s6, s6, s7, xx, xx, xx, xx, xx, xx, xx, xx, xx, xx, xx,
    };

    const n = remaining.len;
    var i: usize = 0;
    while (i < n) {
        const first_byte = remaining[i];
        if (first_byte < min_non_ascii_codepoint) {
            i += 1;
            continue;
        }

        const info = first[first_byte];
        if (info == xx) {
            return false; // Illegal starter byte.
        }

        const size = info & 7;
        if (i + size > n) {
            return false; // Short or invalid.
        }

        // Figure out the acceptable low and high continuation bytes, starting
        // with our defaults.
        var accept_lo: u8 = lo_cb;
        var accept_hi: u8 = hi_cb;

        switch (info >> 4) {
            0 => {},
            1 => accept_lo = 0xA0,
            2 => accept_hi = 0x9F,
            3 => accept_lo = 0x90,
            4 => accept_hi = 0x8F,
            else => unreachable,
        }

        const c1 = remaining[i + 1];
        if (c1 < accept_lo or accept_hi < c1) {
            return false;
        }

        switch (size) {
            2 => i += 2,
            3 => {
                const c2 = remaining[i + 2];
                if (c2 < lo_cb or hi_cb < c2) {
                    return false;
                }
                i += 3;
            },
            4 => {
                const c2 = remaining[i + 2];
                if (c2 < lo_cb or hi_cb < c2) {
                    return false;
                }
                const c3 = remaining[i + 3];
                if (c3 < lo_cb or hi_cb < c3) {
                    return false;
                }
                i += 4;
            },
            else => unreachable,
        }
    }

    return true;
}

/// Utf8View iterates the code points of a utf-8 encoded string.
///
/// ```
/// var utf8 = (try std.unicode.Utf8View.init("hi there")).iterator();
/// while (utf8.nextCodepointSlice()) |codepoint| {
///   std.debug.print("got codepoint {s}\n", .{codepoint});
/// }
/// ```
pub const Utf8View = struct {
    bytes: []const u8,

    pub fn init(s: []const u8) !Utf8View {
        if (!utf8ValidateSlice(s)) {
            return error.InvalidUtf8;
        }

        return initUnchecked(s);
    }

    pub fn initUnchecked(s: []const u8) Utf8View {
        return Utf8View{ .bytes = s };
    }

    pub inline fn initComptime(comptime s: []const u8) Utf8View {
        return comptime if (init(s)) |r| r else |err| switch (err) {
            error.InvalidUtf8 => {
                @compileError("invalid utf8");
            },
        };
    }

    pub fn iterator(s: Utf8View) Utf8Iterator {
        return Utf8Iterator{
            .bytes = s.bytes,
            .i = 0,
        };
    }
};

pub const Utf8Iterator = struct {
    bytes: []const u8,
    i: usize,

    pub fn nextCodepointSlice(it: *Utf8Iterator) ?[]const u8 {
        if (it.i >= it.bytes.len) {
            return null;
        }

        const cp_len = utf8ByteSequenceLength(it.bytes[it.i]) catch unreachable;
        it.i += cp_len;
        return it.bytes[it.i - cp_len .. it.i];
    }

    pub fn nextCodepoint(it: *Utf8Iterator) ?u21 {
        const slice = it.nextCodepointSlice() orelse return null;
        return utf8Decode(slice) catch unreachable;
    }

    /// Look ahead at the next n codepoints without advancing the iterator.
    /// If fewer than n codepoints are available, then return the remainder of the string.
    pub fn peek(it: *Utf8Iterator, n: usize) []const u8 {
        const original_i = it.i;
        defer it.i = original_i;

        var end_ix = original_i;
        var found: usize = 0;
        while (found < n) : (found += 1) {
            const next_codepoint = it.nextCodepointSlice() orelse return it.bytes[original_i..];
            end_ix += next_codepoint.len;
        }

        return it.bytes[original_i..end_ix];
    }
};

pub fn utf16IsHighSurrogate(c: u16) bool {
    return c & ~@as(u16, 0x03ff) == 0xd800;
}

pub fn utf16IsLowSurrogate(c: u16) bool {
    return c & ~@as(u16, 0x03ff) == 0xdc00;
}

/// Returns how many code units the UTF-16 representation would require
/// for the given codepoint.
pub fn utf16CodepointSequenceLength(c: u21) !u2 {
    if (c <= 0xFFFF) return 1;
    if (c <= 0x10FFFF) return 2;
    return error.CodepointTooLarge;
}

test utf16CodepointSequenceLength {
    try testing.expectEqual(@as(u2, 1), try utf16CodepointSequenceLength('a'));
    try testing.expectEqual(@as(u2, 1), try utf16CodepointSequenceLength(0xFFFF));
    try testing.expectEqual(@as(u2, 2), try utf16CodepointSequenceLength(0x10000));
    try testing.expectEqual(@as(u2, 2), try utf16CodepointSequenceLength(0x10FFFF));
    try testing.expectError(error.CodepointTooLarge, utf16CodepointSequenceLength(0x110000));
}

/// Given the first code unit of a UTF-16 codepoint, returns a number 1-2
/// indicating the total length of the codepoint in UTF-16 code units.
/// If this code unit does not match the form of a UTF-16 start code unit, returns Utf16InvalidStartCodeUnit.
pub fn utf16CodeUnitSequenceLength(first_code_unit: u16) !u2 {
    if (utf16IsHighSurrogate(first_code_unit)) return 2;
    if (utf16IsLowSurrogate(first_code_unit)) return error.Utf16InvalidStartCodeUnit;
    return 1;
}

test utf16CodeUnitSequenceLength {
    try testing.expectEqual(@as(u2, 1), try utf16CodeUnitSequenceLength('a'));
    try testing.expectEqual(@as(u2, 1), try utf16CodeUnitSequenceLength(0xFFFF));
    try testing.expectEqual(@as(u2, 2), try utf16CodeUnitSequenceLength(0xDBFF));
    try testing.expectError(error.Utf16InvalidStartCodeUnit, utf16CodeUnitSequenceLength(0xDFFF));
}

/// Decodes the codepoint encoded in the given pair of UTF-16 code units.
/// Asserts that `surrogate_pair.len >= 2` and that the first code unit is a high surrogate.
/// If the second code unit is not a low surrogate, error.ExpectedSecondSurrogateHalf is returned.
pub fn utf16DecodeSurrogatePair(surrogate_pair: []const u16) !u21 {
    assert(surrogate_pair.len >= 2);
    assert(utf16IsHighSurrogate(surrogate_pair[0]));
    const high_half: u21 = surrogate_pair[0];
    const low_half = surrogate_pair[1];
    if (!utf16IsLowSurrogate(low_half)) return error.ExpectedSecondSurrogateHalf;
    return 0x10000 + ((high_half & 0x03ff) << 10) | (low_half & 0x03ff);
}

pub const Utf16LeIterator = struct {
    bytes: []const u8,
    i: usize,

    pub fn init(s: []const u16) Utf16LeIterator {
        return Utf16LeIterator{
            .bytes = mem.sliceAsBytes(s),
            .i = 0,
        };
    }

    pub const NextCodepointError = error{ DanglingSurrogateHalf, ExpectedSecondSurrogateHalf, UnexpectedSecondSurrogateHalf };

    pub fn nextCodepoint(it: *Utf16LeIterator) NextCodepointError!?u21 {
        assert(it.i <= it.bytes.len);
        if (it.i == it.bytes.len) return null;
        var code_units: [2]u16 = undefined;
        code_units[0] = mem.readInt(u16, it.bytes[it.i..][0..2], .little);
        it.i += 2;
        if (utf16IsHighSurrogate(code_units[0])) {
            // surrogate pair
            if (it.i >= it.bytes.len) return error.DanglingSurrogateHalf;
            code_units[1] = mem.readInt(u16, it.bytes[it.i..][0..2], .little);
            const codepoint = try utf16DecodeSurrogatePair(&code_units);
            it.i += 2;
            return codepoint;
        } else if (utf16IsLowSurrogate(code_units[0])) {
            return error.UnexpectedSecondSurrogateHalf;
        } else {
            return code_units[0];
        }
    }
};

/// Returns the length of a supplied UTF-16 string literal in terms of unicode
/// codepoints.
pub fn utf16CountCodepoints(utf16le: []const u16) !usize {
    var len: usize = 0;
    var it = Utf16LeIterator.init(utf16le);
    while (try it.nextCodepoint()) |_| len += 1;
    return len;
}

fn testUtf16CountCodepoints() !void {
    try testing.expectEqual(
        @as(usize, 1),
        try utf16CountCodepoints(utf8ToUtf16LeStringLiteral("a")),
    );
    try testing.expectEqual(
        @as(usize, 10),
        try utf16CountCodepoints(utf8ToUtf16LeStringLiteral("abcdefghij")),
    );
    try testing.expectEqual(
        @as(usize, 10),
        try utf16CountCodepoints(utf8ToUtf16LeStringLiteral("äåéëþüúíóö")),
    );
    try testing.expectEqual(
        @as(usize, 5),
        try utf16CountCodepoints(utf8ToUtf16LeStringLiteral("こんにちは")),
    );
}

test "utf16 count codepoints" {
    @setEvalBranchQuota(2000);
    try testUtf16CountCodepoints();
    try comptime testUtf16CountCodepoints();
}

test "utf8 encode" {
    try comptime testUtf8Encode();
    try testUtf8Encode();
}
fn testUtf8Encode() !void {
    // A few taken from wikipedia a few taken elsewhere
    var array: [4]u8 = undefined;
    try testing.expect((try utf8Encode(try utf8Decode("€"), array[0..])) == 3);
    try testing.expect(array[0] == 0b11100010);
    try testing.expect(array[1] == 0b10000010);
    try testing.expect(array[2] == 0b10101100);

    try testing.expect((try utf8Encode(try utf8Decode("$"), array[0..])) == 1);
    try testing.expect(array[0] == 0b00100100);

    try testing.expect((try utf8Encode(try utf8Decode("¢"), array[0..])) == 2);
    try testing.expect(array[0] == 0b11000010);
    try testing.expect(array[1] == 0b10100010);

    try testing.expect((try utf8Encode(try utf8Decode("𐍈"), array[0..])) == 4);
    try testing.expect(array[0] == 0b11110000);
    try testing.expect(array[1] == 0b10010000);
    try testing.expect(array[2] == 0b10001101);
    try testing.expect(array[3] == 0b10001000);
}

test "utf8 encode comptime" {
    try testing.expectEqualSlices(u8, "€", &utf8EncodeComptime('€'));
    try testing.expectEqualSlices(u8, "$", &utf8EncodeComptime('$'));
    try testing.expectEqualSlices(u8, "¢", &utf8EncodeComptime('¢'));
    try testing.expectEqualSlices(u8, "𐍈", &utf8EncodeComptime('𐍈'));
}

test "utf8 encode error" {
    try comptime testUtf8EncodeError();
    try testUtf8EncodeError();
}
fn testUtf8EncodeError() !void {
    var array: [4]u8 = undefined;
    try testErrorEncode(0xd800, array[0..], error.Utf8CannotEncodeSurrogateHalf);
    try testErrorEncode(0xdfff, array[0..], error.Utf8CannotEncodeSurrogateHalf);
    try testErrorEncode(0x110000, array[0..], error.CodepointTooLarge);
    try testErrorEncode(0x1fffff, array[0..], error.CodepointTooLarge);
}

fn testErrorEncode(codePoint: u21, array: []u8, expectedErr: anyerror) !void {
    try testing.expectError(expectedErr, utf8Encode(codePoint, array));
}

test "utf8 iterator on ascii" {
    try comptime testUtf8IteratorOnAscii();
    try testUtf8IteratorOnAscii();
}
fn testUtf8IteratorOnAscii() !void {
    const s = Utf8View.initComptime("abc");

    var it1 = s.iterator();
    try testing.expect(mem.eql(u8, "a", it1.nextCodepointSlice().?));
    try testing.expect(mem.eql(u8, "b", it1.nextCodepointSlice().?));
    try testing.expect(mem.eql(u8, "c", it1.nextCodepointSlice().?));
    try testing.expect(it1.nextCodepointSlice() == null);

    var it2 = s.iterator();
    try testing.expect(it2.nextCodepoint().? == 'a');
    try testing.expect(it2.nextCodepoint().? == 'b');
    try testing.expect(it2.nextCodepoint().? == 'c');
    try testing.expect(it2.nextCodepoint() == null);
}

test "utf8 view bad" {
    try comptime testUtf8ViewBad();
    try testUtf8ViewBad();
}
fn testUtf8ViewBad() !void {
    // Compile-time error.
    // const s3 = Utf8View.initComptime("\xfe\xf2");
    try testing.expectError(error.InvalidUtf8, Utf8View.init("hel\xadlo"));
}

test "utf8 view ok" {
    try comptime testUtf8ViewOk();
    try testUtf8ViewOk();
}
fn testUtf8ViewOk() !void {
    const s = Utf8View.initComptime("東京市");

    var it1 = s.iterator();
    try testing.expect(mem.eql(u8, "東", it1.nextCodepointSlice().?));
    try testing.expect(mem.eql(u8, "京", it1.nextCodepointSlice().?));
    try testing.expect(mem.eql(u8, "市", it1.nextCodepointSlice().?));
    try testing.expect(it1.nextCodepointSlice() == null);

    var it2 = s.iterator();
    try testing.expect(it2.nextCodepoint().? == 0x6771);
    try testing.expect(it2.nextCodepoint().? == 0x4eac);
    try testing.expect(it2.nextCodepoint().? == 0x5e02);
    try testing.expect(it2.nextCodepoint() == null);
}

test "validate slice" {
    try comptime testValidateSlice();
    try testValidateSlice();

    // We skip a variable (based on recommended vector size) chunks of
    // ASCII characters. Let's make sure we're chunking correctly.
    const str = [_]u8{'a'} ** 550 ++ "\xc0";
    for (0..str.len - 3) |i| {
        try testing.expect(!utf8ValidateSlice(str[i..]));
    }
}
fn testValidateSlice() !void {
    try testing.expect(utf8ValidateSlice("abc"));
    try testing.expect(utf8ValidateSlice("abc\xdf\xbf"));
    try testing.expect(utf8ValidateSlice(""));
    try testing.expect(utf8ValidateSlice("a"));
    try testing.expect(utf8ValidateSlice("abc"));
    try testing.expect(utf8ValidateSlice("Ж"));
    try testing.expect(utf8ValidateSlice("ЖЖ"));
    try testing.expect(utf8ValidateSlice("брэд-ЛГТМ"));
    try testing.expect(utf8ValidateSlice("☺☻☹"));
    try testing.expect(utf8ValidateSlice("a\u{fffdb}"));
    try testing.expect(utf8ValidateSlice("\xf4\x8f\xbf\xbf"));
    try testing.expect(utf8ValidateSlice("abc\xdf\xbf"));

    try testing.expect(!utf8ValidateSlice("abc\xc0"));
    try testing.expect(!utf8ValidateSlice("abc\xc0abc"));
    try testing.expect(!utf8ValidateSlice("aa\xe2"));
    try testing.expect(!utf8ValidateSlice("\x42\xfa"));
    try testing.expect(!utf8ValidateSlice("\x42\xfa\x43"));
    try testing.expect(!utf8ValidateSlice("abc\xc0"));
    try testing.expect(!utf8ValidateSlice("abc\xc0abc"));
    try testing.expect(!utf8ValidateSlice("\xf4\x90\x80\x80"));
    try testing.expect(!utf8ValidateSlice("\xf7\xbf\xbf\xbf"));
    try testing.expect(!utf8ValidateSlice("\xfb\xbf\xbf\xbf\xbf"));
    try testing.expect(!utf8ValidateSlice("\xc0\x80"));
    try testing.expect(!utf8ValidateSlice("\xed\xa0\x80"));
    try testing.expect(!utf8ValidateSlice("\xed\xbf\xbf"));
}

test "valid utf8" {
    try comptime testValidUtf8();
    try testValidUtf8();
}
fn testValidUtf8() !void {
    try testValid("\x00", 0x0);
    try testValid("\x20", 0x20);
    try testValid("\x7f", 0x7f);
    try testValid("\xc2\x80", 0x80);
    try testValid("\xdf\xbf", 0x7ff);
    try testValid("\xe0\xa0\x80", 0x800);
    try testValid("\xe1\x80\x80", 0x1000);
    try testValid("\xef\xbf\xbf", 0xffff);
    try testValid("\xf0\x90\x80\x80", 0x10000);
    try testValid("\xf1\x80\x80\x80", 0x40000);
    try testValid("\xf3\xbf\xbf\xbf", 0xfffff);
    try testValid("\xf4\x8f\xbf\xbf", 0x10ffff);
}

test "invalid utf8 continuation bytes" {
    try comptime testInvalidUtf8ContinuationBytes();
    try testInvalidUtf8ContinuationBytes();
}
fn testInvalidUtf8ContinuationBytes() !void {
    // unexpected continuation
    try testError("\x80", error.Utf8InvalidStartByte);
    try testError("\xbf", error.Utf8InvalidStartByte);
    // too many leading 1's
    try testError("\xf8", error.Utf8InvalidStartByte);
    try testError("\xff", error.Utf8InvalidStartByte);
    // expected continuation for 2 byte sequences
    try testError("\xc2", error.UnexpectedEof);
    try testError("\xc2\x00", error.Utf8ExpectedContinuation);
    try testError("\xc2\xc0", error.Utf8ExpectedContinuation);
    // expected continuation for 3 byte sequences
    try testError("\xe0", error.UnexpectedEof);
    try testError("\xe0\x00", error.UnexpectedEof);
    try testError("\xe0\xc0", error.UnexpectedEof);
    try testError("\xe0\xa0", error.UnexpectedEof);
    try testError("\xe0\xa0\x00", error.Utf8ExpectedContinuation);
    try testError("\xe0\xa0\xc0", error.Utf8ExpectedContinuation);
    // expected continuation for 4 byte sequences
    try testError("\xf0", error.UnexpectedEof);
    try testError("\xf0\x00", error.UnexpectedEof);
    try testError("\xf0\xc0", error.UnexpectedEof);
    try testError("\xf0\x90\x00", error.UnexpectedEof);
    try testError("\xf0\x90\xc0", error.UnexpectedEof);
    try testError("\xf0\x90\x80\x00", error.Utf8ExpectedContinuation);
    try testError("\xf0\x90\x80\xc0", error.Utf8ExpectedContinuation);
}

test "overlong utf8 codepoint" {
    try comptime testOverlongUtf8Codepoint();
    try testOverlongUtf8Codepoint();
}
fn testOverlongUtf8Codepoint() !void {
    try testError("\xc0\x80", error.Utf8OverlongEncoding);
    try testError("\xc1\xbf", error.Utf8OverlongEncoding);
    try testError("\xe0\x80\x80", error.Utf8OverlongEncoding);
    try testError("\xe0\x9f\xbf", error.Utf8OverlongEncoding);
    try testError("\xf0\x80\x80\x80", error.Utf8OverlongEncoding);
    try testError("\xf0\x8f\xbf\xbf", error.Utf8OverlongEncoding);
}

test "misc invalid utf8" {
    try comptime testMiscInvalidUtf8();
    try testMiscInvalidUtf8();
}
fn testMiscInvalidUtf8() !void {
    // codepoint out of bounds
    try testError("\xf4\x90\x80\x80", error.Utf8CodepointTooLarge);
    try testError("\xf7\xbf\xbf\xbf", error.Utf8CodepointTooLarge);
    // surrogate halves
    try testValid("\xed\x9f\xbf", 0xd7ff);
    try testError("\xed\xa0\x80", error.Utf8EncodesSurrogateHalf);
    try testError("\xed\xbf\xbf", error.Utf8EncodesSurrogateHalf);
    try testValid("\xee\x80\x80", 0xe000);
}

test "utf8 iterator peeking" {
    try comptime testUtf8Peeking();
    try testUtf8Peeking();
}

fn testUtf8Peeking() !void {
    const s = Utf8View.initComptime("noël");
    var it = s.iterator();

    try testing.expect(mem.eql(u8, "n", it.nextCodepointSlice().?));

    try testing.expect(mem.eql(u8, "o", it.peek(1)));
    try testing.expect(mem.eql(u8, "oë", it.peek(2)));
    try testing.expect(mem.eql(u8, "oël", it.peek(3)));
    try testing.expect(mem.eql(u8, "oël", it.peek(4)));
    try testing.expect(mem.eql(u8, "oël", it.peek(10)));

    try testing.expect(mem.eql(u8, "o", it.nextCodepointSlice().?));
    try testing.expect(mem.eql(u8, "ë", it.nextCodepointSlice().?));
    try testing.expect(mem.eql(u8, "l", it.nextCodepointSlice().?));
    try testing.expect(it.nextCodepointSlice() == null);

    try testing.expect(mem.eql(u8, &[_]u8{}, it.peek(1)));
}

fn testError(bytes: []const u8, expected_err: anyerror) !void {
    try testing.expectError(expected_err, testDecode(bytes));
}

fn testValid(bytes: []const u8, expected_codepoint: u21) !void {
    try testing.expect((testDecode(bytes) catch unreachable) == expected_codepoint);
}

fn testDecode(bytes: []const u8) !u21 {
    const length = try utf8ByteSequenceLength(bytes[0]);
    if (bytes.len < length) return error.UnexpectedEof;
    try testing.expect(bytes.len == length);
    return utf8Decode(bytes);
}

/// Print the given `utf8` string, encoded as UTF-8 bytes.
/// Ill-formed UTF-8 byte sequences are replaced by the replacement character (U+FFFD)
/// according to "U+FFFD Substitution of Maximal Subparts" from Chapter 3 of
/// the Unicode standard, and as specified by https://encoding.spec.whatwg.org/#utf-8-decoder
fn formatUtf8(utf8: []const u8, writer: *std.Io.Writer) std.Io.Writer.Error!void {
    var buf: [300]u8 = undefined; // just an arbitrary size
    var u8len: usize = 0;

    // This implementation is based on this specification:
    // https://encoding.spec.whatwg.org/#utf-8-decoder
    var codepoint: u21 = 0;
    var cont_bytes_seen: u3 = 0;
    var cont_bytes_needed: u3 = 0;
    var lower_boundary: u8 = 0x80;
    var upper_boundary: u8 = 0xBF;

    var i: usize = 0;
    while (i < utf8.len) {
        const byte = utf8[i];
        if (cont_bytes_needed == 0) {
            switch (byte) {
                0x00...0x7F => {
                    buf[u8len] = byte;
                    u8len += 1;
                },
                0xC2...0xDF => {
                    cont_bytes_needed = 1;
                    codepoint = byte & 0b00011111;
                },
                0xE0...0xEF => {
                    if (byte == 0xE0) lower_boundary = 0xA0;
                    if (byte == 0xED) upper_boundary = 0x9F;
                    cont_bytes_needed = 2;
                    codepoint = byte & 0b00001111;
                },
                0xF0...0xF4 => {
                    if (byte == 0xF0) lower_boundary = 0x90;
                    if (byte == 0xF4) upper_boundary = 0x8F;
                    cont_bytes_needed = 3;
                    codepoint = byte & 0b00000111;
                },
                else => {
                    u8len += utf8Encode(replacement_character, buf[u8len..]) catch unreachable;
                },
            }
            // consume the byte
            i += 1;
        } else if (byte < lower_boundary or byte > upper_boundary) {
            codepoint = 0;
            cont_bytes_needed = 0;
            cont_bytes_seen = 0;
            lower_boundary = 0x80;
            upper_boundary = 0xBF;
            u8len += utf8Encode(replacement_character, buf[u8len..]) catch unreachable;
            // do not consume the current byte, it should now be treated as a possible start byte
        } else {
            lower_boundary = 0x80;
            upper_boundary = 0xBF;
            codepoint <<= 6;
            codepoint |= byte & 0b00111111;
            cont_bytes_seen += 1;
            // consume the byte
            i += 1;

            if (cont_bytes_seen == cont_bytes_needed) {
                const codepoint_len = cont_bytes_seen + 1;
                const codepoint_start_i = i - codepoint_len;
                @memcpy(buf[u8len..][0..codepoint_len], utf8[codepoint_start_i..][0..codepoint_len]);
                u8len += codepoint_len;

                codepoint = 0;
                cont_bytes_needed = 0;
                cont_bytes_seen = 0;
            }
        }
        // make sure there's always enough room for another maximum length UTF-8 codepoint
        if (u8len + 4 > buf.len) {
            try writer.writeAll(buf[0..u8len]);
            u8len = 0;
        }
    }
    if (cont_bytes_needed != 0) {
        // we know there's enough room because we always flush
        // if there's less than 4 bytes remaining in the buffer.
        u8len += utf8Encode(replacement_character, buf[u8len..]) catch unreachable;
    }
    try writer.writeAll(buf[0..u8len]);
}

/// Return a Formatter for a (potentially ill-formed) UTF-8 string.
/// Ill-formed UTF-8 byte sequences are replaced by the replacement character (U+FFFD)
/// according to "U+FFFD Substitution of Maximal Subparts" from Chapter 3 of
/// the Unicode standard, and as specified by https://encoding.spec.whatwg.org/#utf-8-decoder
pub fn fmtUtf8(utf8: []const u8) std.fmt.Alt([]const u8, formatUtf8) {
    return .{ .data = utf8 };
}

test fmtUtf8 {
    const expectFmt = testing.expectFmt;
    try expectFmt("", "{f}", .{fmtUtf8("")});
    try expectFmt("foo", "{f}", .{fmtUtf8("foo")});
    try expectFmt("𐐷", "{f}", .{fmtUtf8("𐐷")});

    // Table 3-8. U+FFFD for Non-Shortest Form Sequences
    try expectFmt("��������A", "{f}", .{fmtUtf8("\xC0\xAF\xE0\x80\xBF\xF0\x81\x82A")});

    // Table 3-9. U+FFFD for Ill-Formed Sequences for Surrogates
    try expectFmt("��������A", "{f}", .{fmtUtf8("\xED\xA0\x80\xED\xBF\xBF\xED\xAFA")});

    // Table 3-10. U+FFFD for Other Ill-Formed Sequences
    try expectFmt("�����A��B", "{f}", .{fmtUtf8("\xF4\x91\x92\x93\xFFA\x80\xBFB")});

    // Table 3-11. U+FFFD for Truncated Sequences
    try expectFmt("����A", "{f}", .{fmtUtf8("\xE1\x80\xE2\xF0\x91\x92\xF1\xBFA")});
}

fn utf16LeToUtf8ArrayListImpl(
    result: *std.array_list.Managed(u8),
    utf16le: []const u16,
    comptime surrogates: Surrogates,
) (switch (surrogates) {
    .cannot_encode_surrogate_half => Utf16LeToUtf8AllocError,
    .can_encode_surrogate_half => Allocator.Error,
})!void {
    assert(result.unusedCapacitySlice().len >= utf16le.len);

    var remaining = utf16le;
    vectorized: {
        const chunk_len = std.simd.suggestVectorLength(u16) orelse break :vectorized;
        const Chunk = @Vector(chunk_len, u16);

        // Fast path. Check for and encode ASCII characters at the start of the input.
        while (remaining.len >= chunk_len) {
            const chunk: Chunk = remaining[0..chunk_len].*;
            const mask: Chunk = @splat(mem.nativeToLittle(u16, 0x7F));
            if (@reduce(.Or, chunk | mask != mask)) {
                // found a non ASCII code unit
                break;
            }
            const ascii_chunk: @Vector(chunk_len, u8) = @truncate(mem.nativeToLittle(Chunk, chunk));
            // We allocated enough space to encode every UTF-16 code unit
            // as ASCII, so if the entire string is ASCII then we are
            // guaranteed to have enough space allocated
            result.addManyAsArrayAssumeCapacity(chunk_len).* = ascii_chunk;
            remaining = remaining[chunk_len..];
        }
    }

    switch (surrogates) {
        .cannot_encode_surrogate_half => {
            var it = Utf16LeIterator.init(remaining);
            while (try it.nextCodepoint()) |codepoint| {
                const utf8_len = utf8CodepointSequenceLength(codepoint) catch unreachable;
                assert((utf8Encode(codepoint, try result.addManyAsSlice(utf8_len)) catch unreachable) == utf8_len);
            }
        },
        .can_encode_surrogate_half => {
            var it = Wtf16LeIterator.init(remaining);
            while (it.nextCodepoint()) |codepoint| {
                const utf8_len = utf8CodepointSequenceLength(codepoint) catch unreachable;
                assert((wtf8Encode(codepoint, try result.addManyAsSlice(utf8_len)) catch unreachable) == utf8_len);
            }
        },
    }
}

pub const Utf16LeToUtf8AllocError = Allocator.Error || Utf16LeToUtf8Error;

pub fn utf16LeToUtf8ArrayList(result: *std.array_list.Managed(u8), utf16le: []const u16) Utf16LeToUtf8AllocError!void {
    try result.ensureUnusedCapacity(utf16le.len);
    return utf16LeToUtf8ArrayListImpl(result, utf16le, .cannot_encode_surrogate_half);
}

/// Caller owns returned memory.
pub fn utf16LeToUtf8Alloc(allocator: Allocator, utf16le: []const u16) Utf16LeToUtf8AllocError![]u8 {
    // optimistically guess that it will all be ascii.
    var result = try std.array_list.Managed(u8).initCapacity(allocator, utf16le.len);
    errdefer result.deinit();

    try utf16LeToUtf8ArrayListImpl(&result, utf16le, .cannot_encode_surrogate_half);
    return result.toOwnedSlice();
}

/// Caller owns returned memory.
pub fn utf16LeToUtf8AllocZ(allocator: Allocator, utf16le: []const u16) Utf16LeToUtf8AllocError![:0]u8 {
    // optimistically guess that it will all be ascii (and allocate space for the null terminator)
    var result = try std.array_list.Managed(u8).initCapacity(allocator, utf16le.len + 1);
    errdefer result.deinit();

    try utf16LeToUtf8ArrayListImpl(&result, utf16le, .cannot_encode_surrogate_half);
    return result.toOwnedSliceSentinel(0);
}

pub const Utf16LeToUtf8Error = Utf16LeIterator.NextCodepointError;

/// Asserts that the output buffer is big enough.
/// Returns end byte index into utf8.
fn utf16LeToUtf8Impl(utf8: []u8, utf16le: []const u16, comptime surrogates: Surrogates) (switch (surrogates) {
    .cannot_encode_surrogate_half => Utf16LeToUtf8Error,
    .can_encode_surrogate_half => error{},
})!usize {
    var dest_index: usize = 0;

    var remaining = utf16le;
    vectorized: {
        const chunk_len = std.simd.suggestVectorLength(u16) orelse break :vectorized;
        const Chunk = @Vector(chunk_len, u16);

        // Fast path. Check for and encode ASCII characters at the start of the input.
        while (remaining.len >= chunk_len) {
            const chunk: Chunk = remaining[0..chunk_len].*;
            const mask: Chunk = @splat(mem.nativeToLittle(u16, 0x7F));
            if (@reduce(.Or, chunk | mask != mask)) {
                // found a non ASCII code unit
                break;
            }
            const ascii_chunk: @Vector(chunk_len, u8) = @truncate(mem.nativeToLittle(Chunk, chunk));
            utf8[dest_index..][0..chunk_len].* = ascii_chunk;
            dest_index += chunk_len;
            remaining = remaining[chunk_len..];
        }
    }

    switch (surrogates) {
        .cannot_encode_surrogate_half => {
            var it = Utf16LeIterator.init(remaining);
            while (try it.nextCodepoint()) |codepoint| {
                dest_index += utf8Encode(codepoint, utf8[dest_index..]) catch |err| switch (err) {
                    // The maximum possible codepoint encoded by UTF-16 is U+10FFFF,
                    // which is within the valid codepoint range.
                    error.CodepointTooLarge => unreachable,
                    // We know the codepoint was valid in UTF-16, meaning it is not
                    // an unpaired surrogate codepoint.
                    error.Utf8CannotEncodeSurrogateHalf => unreachable,
                };
            }
        },
        .can_encode_surrogate_half => {
            var it = Wtf16LeIterator.init(remaining);
            while (it.nextCodepoint()) |codepoint| {
                dest_index += wtf8Encode(codepoint, utf8[dest_index..]) catch |err| switch (err) {
                    // The maximum possible codepoint encoded by UTF-16 is U+10FFFF,
                    // which is within the valid codepoint range.
                    error.CodepointTooLarge => unreachable,
                };
            }
        },
    }
    return dest_index;
}

pub fn utf16LeToUtf8(utf8: []u8, utf16le: []const u16) Utf16LeToUtf8Error!usize {
    return utf16LeToUtf8Impl(utf8, utf16le, .cannot_encode_surrogate_half);
}

test utf16LeToUtf8 {
    var utf16le: [2]u16 = undefined;
    const utf16le_as_bytes = mem.sliceAsBytes(utf16le[0..]);

    {
        mem.writeInt(u16, utf16le_as_bytes[0..2], 'A', .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 'a', .little);
        const utf8 = try utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        defer testing.allocator.free(utf8);
        try testing.expect(mem.eql(u8, utf8, "Aa"));
    }

    {
        mem.writeInt(u16, utf16le_as_bytes[0..2], 0x80, .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 0xffff, .little);
        const utf8 = try utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        defer testing.allocator.free(utf8);
        try testing.expect(mem.eql(u8, utf8, "\xc2\x80" ++ "\xef\xbf\xbf"));
    }

    {
        // the values just outside the surrogate half range
        mem.writeInt(u16, utf16le_as_bytes[0..2], 0xd7ff, .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 0xe000, .little);
        const utf8 = try utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        defer testing.allocator.free(utf8);
        try testing.expect(mem.eql(u8, utf8, "\xed\x9f\xbf" ++ "\xee\x80\x80"));
    }

    {
        // smallest surrogate pair
        mem.writeInt(u16, utf16le_as_bytes[0..2], 0xd800, .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 0xdc00, .little);
        const utf8 = try utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        defer testing.allocator.free(utf8);
        try testing.expect(mem.eql(u8, utf8, "\xf0\x90\x80\x80"));
    }

    {
        // largest surrogate pair
        mem.writeInt(u16, utf16le_as_bytes[0..2], 0xdbff, .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 0xdfff, .little);
        const utf8 = try utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        defer testing.allocator.free(utf8);
        try testing.expect(mem.eql(u8, utf8, "\xf4\x8f\xbf\xbf"));
    }

    {
        mem.writeInt(u16, utf16le_as_bytes[0..2], 0xdbff, .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 0xdc00, .little);
        const utf8 = try utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        defer testing.allocator.free(utf8);
        try testing.expect(mem.eql(u8, utf8, "\xf4\x8f\xb0\x80"));
    }

    {
        mem.writeInt(u16, utf16le_as_bytes[0..2], 0xdcdc, .little);
        mem.writeInt(u16, utf16le_as_bytes[2..4], 0xdcdc, .little);
        const result = utf16LeToUtf8Alloc(testing.allocator, &utf16le);
        try testing.expectError(error.UnexpectedSecondSurrogateHalf, result);
    }
}

fn utf8ToUtf16LeArrayListImpl(result: *std.array_list.Managed(u16), utf8: []const u8, comptime surrogates: Surrogates) !void {
    assert(result.unusedCapacitySlice().len >= utf8.len);

    var remaining = utf8;
    vectorized: {
        const chunk_len = std.simd.suggestVectorLength(u16) orelse break :vectorized;
        const Chunk = @Vector(chunk_len, u8);

        // Fast path. Check for and encode ASCII characters at the start of the input.
        while (remaining.len >= chunk_len) {
            const chunk: Chunk = remaining[0..chunk_len].*;
            const mask: Chunk = @splat(0x80);
            if (@reduce(.Or, chunk & mask == mask)) {
                // found a non ASCII code unit
                break;
            }
            const utf16_chunk = mem.nativeToLittle(@Vector(chunk_len, u16), chunk);
            result.addManyAsArrayAssumeCapacity(chunk_len).* = utf16_chunk;
            remaining = remaining[chunk_len..];
        }
    }

    const view = switch (surrogates) {
        .cannot_encode_surrogate_half => try Utf8View.init(remaining),
        .can_encode_surrogate_half => try Wtf8View.init(remaining),
    };
    var it = view.iterator();
    while (it.nextCodepoint()) |codepoint| {
        if (codepoint < 0x10000) {
            try result.append(mem.nativeToLittle(u16, @intCast(codepoint)));
        } else {
            const high = @as(u16, @intCast((codepoint - 0x10000) >> 10)) + 0xD800;
            const low = @as(u16, @intCast(codepoint & 0x3FF)) + 0xDC00;
            try result.appendSlice(&.{ mem.nativeToLittle(u16, high), mem.nativeToLittle(u16, low) });
        }
    }
}

pub fn utf8ToUtf16LeArrayList(result: *std.array_list.Managed(u16), utf8: []const u8) error{ InvalidUtf8, OutOfMemory }!void {
    try result.ensureUnusedCapacity(utf8.len);
    return utf8ToUtf16LeArrayListImpl(result, utf8, .cannot_encode_surrogate_half);
}

pub fn utf8ToUtf16LeAlloc(allocator: Allocator, utf8: []const u8) error{ InvalidUtf8, OutOfMemory }![]u16 {
    // optimistically guess that it will not require surrogate pairs
    var result = try std.array_list.Managed(u16).initCapacity(allocator, utf8.len);
    errdefer result.deinit();

    try utf8ToUtf16LeArrayListImpl(&result, utf8, .cannot_encode_surrogate_half);
    return result.toOwnedSlice();
}

pub fn utf8ToUtf16LeAllocZ(allocator: Allocator, utf8: []const u8) error{ InvalidUtf8, OutOfMemory }![:0]u16 {
    // optimistically guess that it will not require surrogate pairs
    var result = try std.array_list.Managed(u16).initCapacity(allocator, utf8.len + 1);
    errdefer result.deinit();

    try utf8ToUtf16LeArrayListImpl(&result, utf8, .cannot_encode_surrogate_half);
    return result.toOwnedSliceSentinel(0);
}

/// Returns index of next character. If exact fit, returned index equals output slice length.
/// Assumes there is enough space for the output.
pub fn utf8ToUtf16Le(utf16le: []u16, utf8: []const u8) error{InvalidUtf8}!usize {
    return utf8ToUtf16LeImpl(utf16le, utf8, .cannot_encode_surrogate_half);
}

pub fn utf8ToUtf16LeImpl(utf16le: []u16, utf8: []const u8, comptime surrogates: Surrogates) !usize {
    var dest_index: usize = 0;

    var remaining = utf8;
    vectorized: {
        const chunk_len = std.simd.suggestVectorLength(u16) orelse break :vectorized;
        const Chunk = @Vector(chunk_len, u8);

        // Fast path. Check for and encode ASCII characters at the start of the input.
        while (remaining.len >= chunk_len) {
            const chunk: Chunk = remaining[0..chunk_len].*;
            const mask: Chunk = @splat(0x80);
            if (@reduce(.Or, chunk & mask == mask)) {
                // found a non ASCII code unit
                break;
            }
            const utf16_chunk = mem.nativeToLittle(@Vector(chunk_len, u16), chunk);
            utf16le[dest_index..][0..chunk_len].* = utf16_chunk;
            dest_index += chunk_len;
            remaining = remaining[chunk_len..];
        }
    }

    const view = switch (surrogates) {
        .cannot_encode_surrogate_half => try Utf8View.init(remaining),
        .can_encode_surrogate_half => try Wtf8View.init(remaining),
    };
    var it = view.iterator();
    while (it.nextCodepoint()) |codepoint| {
        if (codepoint < 0x10000) {
            utf16le[dest_index] = mem.nativeToLittle(u16, @intCast(codepoint));
            dest_index += 1;
        } else {
            const high = @as(u16, @intCast((codepoint - 0x10000) >> 10)) + 0xD800;
            const low = @as(u16, @intCast(codepoint & 0x3FF)) + 0xDC00;
            utf16le[dest_index..][0..2].* = .{ mem.nativeToLittle(u16, high), mem.nativeToLittle(u16, low) };
            dest_index += 2;
        }
    }
    return dest_index;
}

test utf8ToUtf16Le {
    var utf16le: [128]u16 = undefined;
    {
        const length = try utf8ToUtf16Le(utf16le[0..], "𐐷");
        try testing.expectEqualSlices(u8, "\x01\xd8\x37\xdc", mem.sliceAsBytes(utf16le[0..length]));
    }
    {
        const length = try utf8ToUtf16Le(utf16le[0..], "\u{10FFFF}");
        try testing.expectEqualSlices(u8, "\xff\xdb\xff\xdf", mem.sliceAsBytes(utf16le[0..length]));
    }
    {
        const result = utf8ToUtf16Le(utf16le[0..], "\xf4\x90\x80\x80");
        try testing.expectError(error.InvalidUtf8, result);
    }
    {
        const length = try utf8ToUtf16Le(utf16le[0..], "This string has been designed to test the vectorized implementat" ++
            "ion by beginning with one hundred twenty-seven ASCII characters¡");
        try testing.expectEqualSlices(u8, &.{
            'T', 0, 'h', 0, 'i', 0, 's', 0, ' ', 0, 's', 0, 't', 0, 'r', 0, 'i', 0, 'n', 0, 'g', 0, ' ', 0, 'h', 0, 'a', 0, 's', 0, ' ', 0,
            'b', 0, 'e', 0, 'e', 0, 'n', 0, ' ', 0, 'd', 0, 'e', 0, 's', 0, 'i', 0, 'g', 0, 'n', 0, 'e', 0, 'd', 0, ' ', 0, 't', 0, 'o', 0,
            ' ', 0, 't', 0, 'e', 0, 's', 0, 't', 0, ' ', 0, 't', 0, 'h', 0, 'e', 0, ' ', 0, 'v', 0, 'e', 0, 'c', 0, 't', 0, 'o', 0, 'r', 0,
            'i', 0, 'z', 0, 'e', 0, 'd', 0, ' ', 0, 'i', 0, 'm', 0, 'p', 0, 'l', 0, 'e', 0, 'm', 0, 'e', 0, 'n', 0, 't', 0, 'a', 0, 't', 0,
            'i', 0, 'o', 0, 'n', 0, ' ', 0, 'b', 0, 'y', 0, ' ', 0, 'b', 0, 'e', 0, 'g', 0, 'i', 0, 'n', 0, 'n', 0, 'i', 0, 'n', 0, 'g', 0,
            ' ', 0, 'w', 0, 'i', 0, 't', 0, 'h', 0, ' ', 0, 'o', 0, 'n', 0, 'e', 0, ' ', 0, 'h', 0, 'u', 0, 'n', 0, 'd', 0, 'r', 0, 'e', 0,
            'd', 0, ' ', 0, 't', 0, 'w', 0, 'e', 0, 'n', 0, 't', 0, 'y', 0, '-', 0, 's', 0, 'e', 0, 'v', 0, 'e', 0, 'n', 0, ' ', 0, 'A', 0,
            'S', 0, 'C', 0, 'I', 0, 'I', 0, ' ', 0, 'c', 0, 'h', 0, 'a', 0, 'r', 0, 'a', 0, 'c', 0, 't', 0, 'e', 0, 'r', 0, 's', 0,
            '¡',
            0,
        }, mem.sliceAsBytes(utf16le[0..length]));
    }
}

test utf8ToUtf16LeArrayList {
    {
        var list = std.array_list.Managed(u16).init(testing.allocator);
        defer list.deinit();
        try utf8ToUtf16LeArrayList(&list, "𐐷");
        try testing.expectEqualSlices(u8, "\x01\xd8\x37\xdc", mem.sliceAsBytes(list.items));
    }
    {
        var list = std.array_list.Managed(u16).init(testing.allocator);
        defer list.deinit();
        try utf8ToUtf16LeArrayList(&list, "\u{10FFFF}");
        try testing.expectEqualSlices(u8, "\xff\xdb\xff\xdf", mem.sliceAsBytes(list.items));
    }
    {
        var list = std.array_list.Managed(u16).init(testing.allocator);
        defer list.deinit();
        const result = utf8ToUtf16LeArrayList(&list, "\xf4\x90\x80\x80");
        try testing.expectError(error.InvalidUtf8, result);
    }
}

test utf8ToUtf16LeAlloc {
    {
        const utf16 = try utf8ToUtf16LeAlloc(testing.allocator, "𐐷");
        defer testing.allocator.free(utf16);
        try testing.expectEqualSlices(u8, "\x01\xd8\x37\xdc", mem.sliceAsBytes(utf16[0..]));
    }
    {
        const utf16 = try utf8ToUtf16LeAlloc(testing.allocator, "\u{10FFFF}");
        defer testing.allocator.free(utf16);
        try testing.expectEqualSlices(u8, "\xff\xdb\xff\xdf", mem.sliceAsBytes(utf16[0..]));
    }
    {
        const result = utf8ToUtf16LeAlloc(testing.allocator, "\xf4\x90\x80\x80");
        try testing.expectError(error.InvalidUtf8, result);
    }
}

test utf8ToUtf16LeAllocZ {
    {
        const utf16 = try utf8ToUtf16LeAllocZ(testing.allocator, "𐐷");
        defer testing.allocator.free(utf16);
        try testing.expectEqualSlices(u8, "\x01\xd8\x37\xdc", mem.sliceAsBytes(utf16));
        try testing.expect(utf16[2] == 0);
    }
    {
        const utf16 = try utf8ToUtf16LeAllocZ(testing.allocator, "\u{10FFFF}");
        defer testing.allocator.free(utf16);
        try testing.expectEqualSlices(u8, "\xff\xdb\xff\xdf", mem.sliceAsBytes(utf16));
        try testing.expect(utf16[2] == 0);
    }
    {
        const result = utf8ToUtf16LeAllocZ(testing.allocator, "\xf4\x90\x80\x80");
        try testing.expectError(error.InvalidUtf8, result);
    }
    {
        const utf16 = try utf8ToUtf16LeAllocZ(testing.allocator, "This string has been designed to test the vectorized implementat" ++
            "ion by beginning with one hundred twenty-seven ASCII characters¡");
        defer testing.allocator.free(utf16);
        try testing.expectEqualSlices(u8, &.{
            'T', 0, 'h', 0, 'i', 0, 's', 0, ' ', 0, 's', 0, 't', 0, 'r', 0, 'i', 0, 'n', 0, 'g', 0, ' ', 0, 'h', 0, 'a', 0, 's', 0, ' ', 0,
            'b', 0, 'e', 0, 'e', 0, 'n', 0, ' ', 0, 'd', 0, 'e', 0, 's', 0, 'i', 0, 'g', 0, 'n', 0, 'e', 0, 'd', 0, ' ', 0, 't', 0, 'o', 0,
            ' ', 0, 't', 0, 'e', 0, 's', 0, 't', 0, ' ', 0, 't', 0, 'h', 0, 'e', 0, ' ', 0, 'v', 0, 'e', 0, 'c', 0, 't', 0, 'o', 0, 'r', 0,
            'i', 0, 'z', 0, 'e', 0, 'd', 0, ' ', 0, 'i', 0, 'm', 0, 'p', 0, 'l', 0, 'e', 0, 'm', 0, 'e', 0, 'n', 0, 't', 0, 'a', 0, 't', 0,
            'i', 0, 'o', 0, 'n', 0, ' ', 0, 'b', 0, 'y', 0, ' ', 0, 'b', 0, 'e', 0, 'g', 0, 'i', 0, 'n', 0, 'n', 0, 'i', 0, 'n', 0, 'g', 0,
            ' ', 0, 'w', 0, 'i', 0, 't', 0, 'h', 0, ' ', 0, 'o', 0, 'n', 0, 'e', 0, ' ', 0, 'h', 0, 'u', 0, 'n', 0, 'd', 0, 'r', 0, 'e', 0,
            'd', 0, ' ', 0, 't', 0, 'w', 0, 'e', 0, 'n', 0, 't', 0, 'y', 0, '-', 0, 's', 0, 'e', 0, 'v', 0, 'e', 0, 'n', 0, ' ', 0, 'A', 0,
            'S', 0, 'C', 0, 'I', 0, 'I', 0, ' ', 0, 'c', 0, 'h', 0, 'a', 0, 'r', 0, 'a', 0, 'c', 0, 't', 0, 'e', 0, 'r', 0, 's', 0,
            '¡',
            0,
        }, mem.sliceAsBytes(utf16));
    }
}

test "ArrayList functions on a re-used list" {
    // utf8ToUtf16LeArrayList
    {
        var list = std.array_list.Managed(u16).init(testing.allocator);
        defer list.deinit();

        const init_slice = utf8ToUtf16LeStringLiteral("abcdefg");
        try list.ensureTotalCapacityPrecise(init_slice.len);
        list.appendSliceAssumeCapacity(init_slice);

        try utf8ToUtf16LeArrayList(&list, "hijklmnopqrstuvwyxz");

        try testing.expectEqualSlices(u16, utf8ToUtf16LeStringLiteral("abcdefghijklmnopqrstuvwyxz"), list.items);
    }

    // utf16LeToUtf8ArrayList
    {
        var list = std.array_list.Managed(u8).init(testing.allocator);
        defer list.deinit();

        const init_slice = "abcdefg";
        try list.ensureTotalCapacityPrecise(init_slice.len);
        list.appendSliceAssumeCapacity(init_slice);

        try utf16LeToUtf8ArrayList(&list, utf8ToUtf16LeStringLiteral("hijklmnopqrstuvwyxz"));

        try testing.expectEqualStrings("abcdefghijklmnopqrstuvwyxz", list.items);
    }

    // wtf8ToWtf16LeArrayList
    {
        var list = std.array_list.Managed(u16).init(testing.allocator);
        defer list.deinit();

        const init_slice = utf8ToUtf16LeStringLiteral("abcdefg");
        try list.ensureTotalCapacityPrecise(init_slice.len);
        list.appendSliceAssumeCapacity(init_slice);

        try wtf8ToWtf16LeArrayList(&list, "hijklmnopqrstuvwyxz");

        try testing.expectEqualSlices(u16, utf8ToUtf16LeStringLiteral("abcdefghijklmnopqrstuvwyxz"), list.items);
    }

    // wtf16LeToWtf8ArrayList
    {
        var list = std.array_list.Managed(u8).init(testing.allocator);
        defer list.deinit();

        const init_slice = "abcdefg";
        try list.ensureTotalCapacityPrecise(init_slice.len);
        list.appendSliceAssumeCapacity(init_slice);

        try wtf16LeToWtf8ArrayList(&list, utf8ToUtf16LeStringLiteral("hijklmnopqrstuvwyxz"));

        try testing.expectEqualStrings("abcdefghijklmnopqrstuvwyxz", list.items);
    }
}

fn utf8ToUtf16LeStringLiteralImpl(comptime utf8: []const u8, comptime surrogates: Surrogates) *const [calcUtf16LeLenImpl(utf8, surrogates) catch |err| @compileError(err):0]u16 {
    return comptime blk: {
        const len: usize = calcUtf16LeLenImpl(utf8, surrogates) catch unreachable;
        var utf16le: [len:0]u16 = [_:0]u16{0} ** len;
        const utf16le_len = utf8ToUtf16LeImpl(&utf16le, utf8[0..], surrogates) catch |err| @compileError(err);
        assert(len == utf16le_len);
        const final = utf16le;
        break :blk &final;
    };
}

/// Converts a UTF-8 string literal into a UTF-16LE string literal.
pub fn utf8ToUtf16LeStringLiteral(comptime utf8: []const u8) *const [calcUtf16LeLen(utf8) catch |err| @compileError(err):0]u16 {
    return utf8ToUtf16LeStringLiteralImpl(utf8, .cannot_encode_surrogate_half);
}

/// Converts a WTF-8 string literal into a WTF-16LE string literal.
pub fn wtf8ToWtf16LeStringLiteral(comptime wtf8: []const u8) *const [calcWtf16LeLen(wtf8) catch |err| @compileError(err):0]u16 {
    return utf8ToUtf16LeStringLiteralImpl(wtf8, .can_encode_surrogate_half);
}

pub fn calcUtf16LeLenImpl(utf8: []const u8, comptime surrogates: Surrogates) !usize {
    const utf8DecodeImpl = switch (surrogates) {
        .cannot_encode_surrogate_half => utf8Decode,
        .can_encode_surrogate_half => wtf8Decode,
    };
    var src_i: usize = 0;
    var dest_len: usize = 0;
    while (src_i < utf8.len) {
        const n = try utf8ByteSequenceLength(utf8[src_i]);
        const next_src_i = src_i + n;
        const codepoint = try utf8DecodeImpl(utf8[src_i..next_src_i]);
        if (codepoint < 0x10000) {
            dest_len += 1;
        } else {
            dest_len += 2;
        }
        src_i = next_src_i;
    }
    return dest_len;
}

const CalcUtf16LeLenError = Utf8DecodeError || error{Utf8InvalidStartByte};

/// Returns length in UTF-16LE of UTF-8 slice as length of []u16.
/// Length in []u8 is 2*len16.
pub fn calcUtf16LeLen(utf8: []const u8) CalcUtf16LeLenError!usize {
    return calcUtf16LeLenImpl(utf8, .cannot_encode_surrogate_half);
}

const CalcWtf16LeLenError = Wtf8DecodeError || error{Utf8InvalidStartByte};

/// Returns length in WTF-16LE of WTF-8 slice as length of []u16.
/// Length in []u8 is 2*len16.
pub fn calcWtf16LeLen(wtf8: []const u8) CalcWtf16LeLenError!usize {
    return calcUtf16LeLenImpl(wtf8, .can_encode_surrogate_half);
}

fn testCalcUtf16LeLenImpl(calcUtf16LeLenImpl_: anytype) !void {
    try testing.expectEqual(@as(usize, 1), try calcUtf16LeLenImpl_("a"));
    try testing.expectEqual(@as(usize, 10), try calcUtf16LeLenI
```
