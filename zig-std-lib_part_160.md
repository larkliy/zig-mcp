```
 V: type) type {
    return StaticStringMapWithEql(V, defaultEql);
}

/// Like `std.mem.eql`, but takes advantage of the fact that the lengths
/// of `a` and `b` are known to be equal.
pub fn defaultEql(a: []const u8, b: []const u8) bool {
    if (a.ptr == b.ptr) return true;
    for (a, b) |a_elem, b_elem| {
        if (a_elem != b_elem) return false;
    }
    return true;
}

/// Like `std.ascii.eqlIgnoreCase` but takes advantage of the fact that
/// the lengths of `a` and `b` are known to be equal.
pub fn eqlAsciiIgnoreCase(a: []const u8, b: []const u8) bool {
    if (a.ptr == b.ptr) return true;
    for (a, b) |a_c, b_c| {
        if (std.ascii.toLower(a_c) != std.ascii.toLower(b_c)) return false;
    }
    return true;
}

/// StaticStringMap, but accepts an equality function (`eql`).
/// The `eql` function is only called to determine the equality
/// of equal length strings. Any strings that are not equal length
/// are never compared using the `eql` function.
pub fn StaticStringMapWithEql(
    comptime V: type,
    comptime eql: fn (a: []const u8, b: []const u8) bool,
) type {
    return struct {
        kvs: *const KVs = &empty_kvs,
        len_indexes: [*]const u32 = &empty_len_indexes,
        len_indexes_len: u32 = 0,
        min_len: u32 = std.math.maxInt(u32),
        max_len: u32 = 0,

        pub const KV = struct {
            key: []const u8,
            value: V,
        };

        const Self = @This();
        const KVs = struct {
            keys: [*]const []const u8,
            values: [*]const V,
            len: u32,
        };
        const empty_kvs = KVs{
            .keys = &empty_keys,
            .values = &empty_vals,
            .len = 0,
        };
        const empty_len_indexes = [0]u32{};
        const empty_keys = [0][]const u8{};
        const empty_vals = [0]V{};

        /// Returns a map backed by static, comptime allocated memory.
        ///
        /// `kvs_list` must be either a list of `struct { []const u8, V }`
        /// (key-value pair) tuples, or a list of `struct { []const u8 }`
        /// (only keys) tuples if `V` is `void`.
        pub inline fn initComptime(comptime kvs_list: anytype) Self {
            comptime {
                var self = Self{};
                if (kvs_list.len == 0)
                    return self;

                // Since the KVs are sorted, a linearly-growing bound will never
                // be sufficient for extreme cases. So we grow proportional to
                // N*log2(N).
                @setEvalBranchQuota(10 * kvs_list.len * std.math.log2_int_ceil(usize, kvs_list.len));

                var sorted_keys: [kvs_list.len][]const u8 = undefined;
                var sorted_vals: [kvs_list.len]V = undefined;

                self.initSortedKVs(kvs_list, &sorted_keys, &sorted_vals);
                const final_keys = sorted_keys;
                const final_vals = sorted_vals;
                self.kvs = &.{
                    .keys = &final_keys,
                    .values = &final_vals,
                    .len = @intCast(kvs_list.len),
                };

                var len_indexes: [self.max_len + 1]u32 = undefined;
                self.initLenIndexes(&len_indexes);
                const final_len_indexes = len_indexes;
                self.len_indexes = &final_len_indexes;
                self.len_indexes_len = @intCast(len_indexes.len);
                return self;
            }
        }

        /// Returns a map backed by memory allocated with `allocator`.
        ///
        /// Handles `kvs_list` the same way as `initComptime()`.
        pub fn init(kvs_list: anytype, allocator: mem.Allocator) !Self {
            var self = Self{};
            if (kvs_list.len == 0)
                return self;

            const sorted_keys = try allocator.alloc([]const u8, kvs_list.len);
            errdefer allocator.free(sorted_keys);
            const sorted_vals = try allocator.alloc(V, kvs_list.len);
            errdefer allocator.free(sorted_vals);
            const kvs = try allocator.create(KVs);
            errdefer allocator.destroy(kvs);

            self.initSortedKVs(kvs_list, sorted_keys, sorted_vals);
            kvs.* = .{
                .keys = sorted_keys.ptr,
                .values = sorted_vals.ptr,
                .len = @intCast(kvs_list.len),
            };
            self.kvs = kvs;

            const len_indexes = try allocator.alloc(u32, self.max_len + 1);
            self.initLenIndexes(len_indexes);
            self.len_indexes = len_indexes.ptr;
            self.len_indexes_len = @intCast(len_indexes.len);
            return self;
        }

        /// this method should only be used with init() and not with initComptime().
        pub fn deinit(self: Self, allocator: mem.Allocator) void {
            allocator.free(self.len_indexes[0..self.len_indexes_len]);
            allocator.free(self.kvs.keys[0..self.kvs.len]);
            allocator.free(self.kvs.values[0..self.kvs.len]);
            allocator.destroy(self.kvs);
        }

        const SortContext = struct {
            keys: [][]const u8,
            vals: []V,

            pub fn lessThan(ctx: @This(), a: usize, b: usize) bool {
                return ctx.keys[a].len < ctx.keys[b].len;
            }

            pub fn swap(ctx: @This(), a: usize, b: usize) void {
                std.mem.swap([]const u8, &ctx.keys[a], &ctx.keys[b]);
                std.mem.swap(V, &ctx.vals[a], &ctx.vals[b]);
            }
        };

        fn initSortedKVs(
            self: *Self,
            kvs_list: anytype,
            sorted_keys: [][]const u8,
            sorted_vals: []V,
        ) void {
            for (kvs_list, 0..) |kv, i| {
                sorted_keys[i] = kv.@"0";
                sorted_vals[i] = if (V == void) {} else kv.@"1";
                self.min_len = @intCast(@min(self.min_len, kv.@"0".len));
                self.max_len = @intCast(@max(self.max_len, kv.@"0".len));
            }
            mem.sortUnstableContext(0, sorted_keys.len, SortContext{
                .keys = sorted_keys,
                .vals = sorted_vals,
            });
        }

        fn initLenIndexes(self: Self, len_indexes: []u32) void {
            var len: usize = 0;
            var i: u32 = 0;
            while (len <= self.max_len) : (len += 1) {
                // find the first keyword len == len
                while (len > self.kvs.keys[i].len) {
                    i += 1;
                }
                len_indexes[len] = i;
            }
        }

        /// Checks if the map has a value for the key.
        pub fn has(self: Self, str: []const u8) bool {
            return self.get(str) != null;
        }

        /// Returns the value for the key if any, else null.
        pub fn get(self: Self, str: []const u8) ?V {
            if (self.kvs.len == 0)
                return null;

            return self.kvs.values[self.getIndex(str) orelse return null];
        }

        pub fn getIndex(self: Self, str: []const u8) ?usize {
            const kvs = self.kvs.*;
            if (kvs.len == 0)
                return null;

            if (str.len < self.min_len or str.len > self.max_len)
                return null;

            var i = self.len_indexes[str.len];
            while (true) {
                const key = kvs.keys[i];
                if (key.len != str.len)
                    return null;
                if (eql(key, str))
                    return i;
                i += 1;
                if (i >= kvs.len)
                    return null;
            }
        }

        /// Returns the key-value pair where key is the longest prefix of `str`
        /// else null.
        ///
        /// This is effectively an O(N) algorithm which loops from `max_len` to
        /// `min_len` and calls `getIndex()` to check all keys with the given
        /// len.
        pub fn getLongestPrefix(self: Self, str: []const u8) ?KV {
            if (self.kvs.len == 0)
                return null;
            const i = self.getLongestPrefixIndex(str) orelse return null;
            const kvs = self.kvs.*;
            return .{
                .key = kvs.keys[i],
                .value = kvs.values[i],
            };
        }

        pub fn getLongestPrefixIndex(self: Self, str: []const u8) ?usize {
            if (self.kvs.len == 0)
                return null;

            if (str.len < self.min_len)
                return null;

            var len = @min(self.max_len, str.len);
            while (len >= self.min_len) : (len -= 1) {
                if (self.getIndex(str[0..len])) |i|
                    return i;
            }
            return null;
        }

        pub fn keys(self: Self) []const []const u8 {
            const kvs = self.kvs.*;
            return kvs.keys[0..kvs.len];
        }

        pub fn values(self: Self) []const V {
            const kvs = self.kvs.*;
            return kvs.values[0..kvs.len];
        }
    };
}

const TestEnum = enum { A, B, C, D, E };
const TestMap = StaticStringMap(TestEnum);
const TestKV = struct { []const u8, TestEnum };
const TestMapVoid = StaticStringMap(void);
const TestKVVoid = struct { []const u8 };
const TestMapWithEql = StaticStringMapWithEql(TestEnum, eqlAsciiIgnoreCase);
const testing = std.testing;
const test_alloc = testing.allocator;

test "list literal of list literals" {
    const slice: []const TestKV = &.{
        .{ "these", .D },
        .{ "have", .A },
        .{ "nothing", .B },
        .{ "incommon", .C },
        .{ "samelen", .E },
    };

    const map = TestMap.initComptime(slice);
    try testMap(map);
    // Default comparison is case sensitive
    try testing.expect(null == map.get("NOTHING"));

    // runtime init(), deinit()
    const map_rt = try TestMap.init(slice, test_alloc);
    defer map_rt.deinit(test_alloc);
    try testMap(map_rt);
    // Default comparison is case sensitive
    try testing.expect(null == map_rt.get("NOTHING"));
}

test "array of structs" {
    const slice = [_]TestKV{
        .{ "these", .D },
        .{ "have", .A },
        .{ "nothing", .B },
        .{ "incommon", .C },
        .{ "samelen", .E },
    };

    try testMap(TestMap.initComptime(slice));
}

test "slice of structs" {
    const slice = [_]TestKV{
        .{ "these", .D },
        .{ "have", .A },
        .{ "nothing", .B },
        .{ "incommon", .C },
        .{ "samelen", .E },
    };

    try testMap(TestMap.initComptime(slice));
}

fn testMap(map: anytype) !void {
    try testing.expectEqual(TestEnum.A, map.get("have").?);
    try testing.expectEqual(TestEnum.B, map.get("nothing").?);
    try testing.expect(null == map.get("missing"));
    try testing.expectEqual(TestEnum.D, map.get("these").?);
    try testing.expectEqual(TestEnum.E, map.get("samelen").?);

    try testing.expect(!map.has("missing"));
    try testing.expect(map.has("these"));

    try testing.expect(null == map.get(""));
    try testing.expect(null == map.get("averylongstringthathasnomatches"));
}

test "void value type, slice of structs" {
    const slice = [_]TestKVVoid{
        .{"these"},
        .{"have"},
        .{"nothing"},
        .{"incommon"},
        .{"samelen"},
    };
    const map = TestMapVoid.initComptime(slice);
    try testSet(map);
    // Default comparison is case sensitive
    try testing.expect(null == map.get("NOTHING"));
}

test "void value type, list literal of list literals" {
    const slice = [_]TestKVVoid{
        .{"these"},
        .{"have"},
        .{"nothing"},
        .{"incommon"},
        .{"samelen"},
    };

    try testSet(TestMapVoid.initComptime(slice));
}

fn testSet(map: TestMapVoid) !void {
    try testing.expectEqual({}, map.get("have").?);
    try testing.expectEqual({}, map.get("nothing").?);
    try testing.expect(null == map.get("missing"));
    try testing.expectEqual({}, map.get("these").?);
    try testing.expectEqual({}, map.get("samelen").?);

    try testing.expect(!map.has("missing"));
    try testing.expect(map.has("these"));

    try testing.expect(null == map.get(""));
    try testing.expect(null == map.get("averylongstringthathasnomatches"));
}

fn testStaticStringMapWithEql(map: TestMapWithEql) !void {
    try testMap(map);
    try testing.expectEqual(TestEnum.A, map.get("HAVE").?);
    try testing.expectEqual(TestEnum.E, map.get("SameLen").?);
    try testing.expect(null == map.get("SameLength"));
    try testing.expect(map.has("ThESe"));
}

test "StaticStringMapWithEql" {
    const slice = [_]TestKV{
        .{ "these", .D },
        .{ "have", .A },
        .{ "nothing", .B },
        .{ "incommon", .C },
        .{ "samelen", .E },
    };

    try testStaticStringMapWithEql(TestMapWithEql.initComptime(slice));
}

test "empty" {
    const m1 = StaticStringMap(usize).initComptime(.{});
    try testing.expect(null == m1.get("anything"));

    const m2 = StaticStringMapWithEql(usize, eqlAsciiIgnoreCase).initComptime(.{});
    try testing.expect(null == m2.get("anything"));

    const m3 = try StaticStringMap(usize).init(.{}, test_alloc);
    try testing.expect(null == m3.get("anything"));

    const m4 = try StaticStringMapWithEql(usize, eqlAsciiIgnoreCase).init(.{}, test_alloc);
    try testing.expect(null == m4.get("anything"));
}

test "redundant entries" {
    const slice = [_]TestKV{
        .{ "redundant", .D },
        .{ "theNeedle", .A },
        .{ "redundant", .B },
        .{ "re" ++ "dundant", .C },
        .{ "redun" ++ "dant", .E },
    };
    const map = TestMap.initComptime(slice);

    // No promises about which one you get:
    try testing.expect(null != map.get("redundant"));

    // Default map is not case sensitive:
    try testing.expect(null == map.get("REDUNDANT"));

    try testing.expectEqual(TestEnum.A, map.get("theNeedle").?);
}

test "redundant insensitive" {
    const slice = [_]TestKV{
        .{ "redundant", .D },
        .{ "theNeedle", .A },
        .{ "redundanT", .B },
        .{ "RE" ++ "dundant", .C },
        .{ "redun" ++ "DANT", .E },
    };

    const map = TestMapWithEql.initComptime(slice);

    // No promises about which result you'll get ...
    try testing.expect(null != map.get("REDUNDANT"));
    try testing.expect(null != map.get("ReDuNdAnT"));
    try testing.expectEqual(TestEnum.A, map.get("theNeedle").?);
}

test "comptime-only value" {
    const map = StaticStringMap(type).initComptime(.{
        .{ "a", struct {
            pub const foo = 1;
        } },
        .{ "b", struct {
            pub const foo = 2;
        } },
        .{ "c", struct {
            pub const foo = 3;
        } },
    });

    try testing.expect(map.get("a").?.foo == 1);
    try testing.expect(map.get("b").?.foo == 2);
    try testing.expect(map.get("c").?.foo == 3);
    try testing.expect(map.get("d") == null);
}

test "getLongestPrefix" {
    const slice = [_]TestKV{
        .{ "a", .A },
        .{ "aa", .B },
        .{ "aaa", .C },
        .{ "aaaa", .D },
    };

    const map = TestMap.initComptime(slice);

    try testing.expectEqual(null, map.getLongestPrefix(""));
    try testing.expectEqual(null, map.getLongestPrefix("bar"));
    try testing.expectEqualStrings("aaaa", map.getLongestPrefix("aaaabar").?.key);
    try testing.expectEqualStrings("aaa", map.getLongestPrefix("aaabar").?.key);
}

test "getLongestPrefix2" {
    const slice = [_]struct { []const u8, u8 }{
        .{ "one", 1 },
        .{ "two", 2 },
        .{ "three", 3 },
        .{ "four", 4 },
        .{ "five", 5 },
        .{ "six", 6 },
        .{ "seven", 7 },
        .{ "eight", 8 },
        .{ "nine", 9 },
    };
    const map = StaticStringMap(u8).initComptime(slice);

    try testing.expectEqual(1, map.get("one"));
    try testing.expectEqual(null, map.get("o"));
    try testing.expectEqual(null, map.get("onexxx"));
    try testing.expectEqual(9, map.get("nine"));
    try testing.expectEqual(null, map.get("n"));
    try testing.expectEqual(null, map.get("ninexxx"));
    try testing.expectEqual(null, map.get("xxx"));

    try testing.expectEqual(1, map.getLongestPrefix("one").?.value);
    try testing.expectEqual(1, map.getLongestPrefix("onexxx").?.value);
    try testing.expectEqual(null, map.getLongestPrefix("o"));
    try testing.expectEqual(null, map.getLongestPrefix("on"));
    try testing.expectEqual(9, map.getLongestPrefix("nine").?.value);
    try testing.expectEqual(9, map.getLongestPrefix("ninexxx").?.value);
    try testing.expectEqual(null, map.getLongestPrefix("n"));
    try testing.expectEqual(null, map.getLongestPrefix("xxx"));
}

test "sorting kvs doesn't exceed eval branch quota" {
    // from https://github.com/ziglang/zig/issues/19803
    const TypeToByteSizeLUT = std.StaticStringMap(u32).initComptime(.{
        .{ "bool", 0 },
        .{ "c_int", 0 },
        .{ "c_long", 0 },
        .{ "c_longdouble", 0 },
        .{ "t20", 0 },
        .{ "t19", 0 },
        .{ "t18", 0 },
        .{ "t17", 0 },
        .{ "t16", 0 },
        .{ "t15", 0 },
        .{ "t14", 0 },
        .{ "t13", 0 },
        .{ "t12", 0 },
        .{ "t11", 0 },
        .{ "t10", 0 },
        .{ "t9", 0 },
        .{ "t8", 0 },
        .{ "t7", 0 },
        .{ "t6", 0 },
        .{ "t5", 0 },
        .{ "t4", 0 },
        .{ "t3", 0 },
        .{ "t2", 0 },
        .{ "t1", 1 },
    });
    try testing.expectEqual(1, TypeToByteSizeLUT.get("t1"));
}



---
File: /std/std.zig
---

pub const AutoHashMap = hash_map.AutoHashMap;
pub const AutoHashMapUnmanaged = hash_map.AutoHashMapUnmanaged;
pub const BitStack = @import("BitStack.zig");
pub const Build = @import("Build.zig");
pub const BufMap = @import("buf_map.zig").BufMap;
pub const BufSet = @import("buf_set.zig").BufSet;
pub const StaticStringMap = static_string_map.StaticStringMap;
pub const StaticStringMapWithEql = static_string_map.StaticStringMapWithEql;
pub const Deque = @import("deque.zig").Deque;
pub const DoublyLinkedList = @import("DoublyLinkedList.zig");
pub const DynLib = @import("dynamic_library.zig").DynLib;
pub const DynamicBitSet = bit_set.DynamicBitSet;
pub const DynamicBitSetUnmanaged = bit_set.DynamicBitSetUnmanaged;
pub const EnumArray = enums.EnumArray;
pub const EnumMap = enums.EnumMap;
pub const EnumSet = enums.EnumSet;
pub const HashMap = hash_map.HashMap;
pub const HashMapUnmanaged = hash_map.HashMapUnmanaged;
pub const Io = @import("Io.zig");
pub const MultiArrayList = @import("multi_array_list.zig").MultiArrayList;
pub const PriorityQueue = @import("priority_queue.zig").PriorityQueue;
pub const PriorityDequeue = @import("priority_dequeue.zig").PriorityDequeue;
pub const Progress = @import("Progress.zig");
pub const Random = @import("Random.zig");
pub const SemanticVersion = @import("SemanticVersion.zig");
pub const SinglyLinkedList = @import("SinglyLinkedList.zig");
pub const StaticBitSet = bit_set.StaticBitSet;
pub const StringHashMap = hash_map.StringHashMap;
pub const StringHashMapUnmanaged = hash_map.StringHashMapUnmanaged;
pub const Target = @import("Target.zig");
pub const Thread = @import("Thread.zig");
pub const Treap = @import("treap.zig").Treap;
pub const Tz = tz.Tz;
pub const Uri = @import("Uri.zig");

/// Deprecated; use `array_hash_map.Custom`.
pub const ArrayHashMapUnmanaged = array_hash_map.Custom;
/// Deprecated; use `array_hash_map.Auto`.
pub const AutoArrayHashMapUnmanaged = array_hash_map.Auto;
/// Deprecated; use `array_hash_map.String`.
pub const StringArrayHashMapUnmanaged = array_hash_map.String;

/// A contiguous, growable list of items in memory. This is a wrapper around a
/// slice of `T` values.
///
/// The same allocator must be used throughout its entire lifetime. Initialize
/// directly with `empty` or `initCapacity`, and deinitialize with `deinit` or
/// `toOwnedSlice`.
pub fn ArrayList(comptime T: type) type {
    return array_list.Aligned(T, null);
}
pub const array_list = @import("array_list.zig");

/// Deprecated; use `array_list.Aligned`.
pub const ArrayListAligned = array_list.Aligned;
/// Deprecated; use `array_list.Aligned`.
pub const ArrayListAlignedUnmanaged = array_list.Aligned;
/// Deprecated; use `ArrayList`.
pub const ArrayListUnmanaged = ArrayList;

pub const array_hash_map = @import("array_hash_map.zig");
pub const atomic = @import("atomic.zig");
pub const base64 = @import("base64.zig");
pub const bit_set = @import("bit_set.zig");
pub const builtin = @import("builtin.zig");
pub const c = @import("c.zig");
pub const coff = @import("coff.zig");
pub const compress = @import("compress.zig");
pub const static_string_map = @import("static_string_map.zig");
pub const crypto = @import("crypto.zig");
pub const debug = @import("debug.zig");
pub const dwarf = @import("dwarf.zig");
pub const elf = @import("elf.zig");
pub const enums = @import("enums.zig");
pub const fmt = @import("fmt.zig");
pub const fs = @import("fs.zig");
pub const gpu = @import("gpu.zig");
pub const hash = @import("hash.zig");
pub const hash_map = @import("hash_map.zig");
pub const heap = @import("heap.zig");
pub const http = @import("http.zig");
pub const json = @import("json.zig");
pub const leb = @import("leb128.zig");
pub const log = @import("log.zig");
pub const macho = @import("macho.zig");
pub const math = @import("math.zig");
pub const mem = @import("mem.zig");
pub const meta = @import("meta.zig");
pub const os = @import("os.zig");
pub const pdb = @import("pdb.zig");
pub const pie = @import("pie.zig");
pub const posix = @import("posix.zig");
pub const process = @import("process.zig");
pub const sort = @import("sort.zig");
pub const simd = @import("simd.zig");
pub const ascii = @import("ascii.zig");
pub const tar = @import("tar.zig");
pub const testing = @import("testing.zig");
pub const time = @import("time.zig");
pub const tz = @import("tz.zig");
pub const unicode = @import("unicode.zig");
pub const valgrind = @import("valgrind.zig");
pub const wasm = @import("wasm.zig");
pub const zig = @import("zig.zig");
pub const zip = @import("zip.zig");
pub const zon = @import("zon.zig");
pub const start = @import("start.zig");

const root = @import("root");

/// Compile-time known settings overridable by the root source file.
pub const options: Options = if (@hasDecl(root, "std_options")) root.std_options else .{};

pub const Options = struct {
    enable_segfault_handler: bool = debug.default_enable_segfault_handler,

    /// If set, `std.start` and `std.Thread` will configure an per-thread alternative signal stack
    /// of this size. Importantly, if `enable_segfault_handler` is set, the segfault handler will
    /// use this alternative stack, meaning it can still print stack traces even if a segmentation
    /// fault is caused by a stack overflow.
    ///
    /// On POSIX targets, the signal stack is configured using 'sigaltstack(2)'.
    ///
    /// On Windows, this value is currently ignored.
    signal_stack_size: ?u64 = 1 << 18, // 1<<17 observed to be sufficient for stack tracing with self-hosted x86_64 backend

    /// The current log level.
    log_level: log.Level = log.default_level,

    log_scope_levels: []const log.ScopeLevel = &.{},

    logFn: fn (
        comptime message_level: log.Level,
        comptime scope: @EnumLiteral(),
        comptime format: []const u8,
        args: anytype,
    ) void = log.defaultLog,

    /// Overrides `std.heap.page_size_min`.
    page_size_min: ?usize = null,
    /// Overrides `std.heap.page_size_max`.
    page_size_max: ?usize = null,
    /// Overrides default implementation for determining OS page size at runtime.
    queryPageSize: fn () usize = heap.defaultQueryPageSize,

    fmt_max_depth: usize = fmt.default_max_depth,

    /// By default, std.http.Client will support HTTPS connections.  Set this option to `true` to
    /// disable TLS support.
    ///
    /// This will likely reduce the size of the binary, but it will also make it impossible to
    /// make a HTTPS connection.
    http_disable_tls: bool = false,

    /// This enables `std.http.Client` to log ssl secrets to the file specified by the SSLKEYLOGFILE
    /// env var.  Creating such a log file allows other programs with access to that file to decrypt
    /// all `std.http.Client` traffic made by this program.
    http_enable_ssl_key_log_file: bool = @import("builtin").mode == .Debug,

    side_channels_mitigations: crypto.SideChannelsMitigations = crypto.default_side_channels_mitigations,

    /// Whether to allow capturing and writing stack traces. This affects the following functions:
    /// * `debug.captureCurrentStackTrace`
    /// * `debug.writeCurrentStackTrace`
    /// * `debug.dumpCurrentStackTrace`
    /// * `debug.writeStackTrace`
    /// * `debug.dumpStackTrace`
    /// * `debug.writeErrorReturnTrace`
    /// * `debug.dumpErrorReturnTrace`
    ///
    /// Stack traces can generally be collected and printed when debug info is stripped, but are
    /// often less useful since they usually cannot be mapped to source locations and/or have bad
    /// source locations. The stack tracing logic can also be quite large, which may be undesirable,
    /// particularly in ReleaseSmall.
    ///
    /// If this is `false`, then captured stack traces will always be empty, and attempts to write
    /// stack traces will just print an error to the relevant `Io.Writer` and return.
    allow_stack_tracing: bool = !@import("builtin").strip_debug_info,

    /// Allows disabling networking in std.Io implementations.
    networking: bool = true,

    /// Whether or not `error.Unexpected` will print its value and a stack trace.
    ///
    /// If this happens the fix is to add the error code to the corresponding
    /// switch expression, possibly introduce a new error in the error set, and
    /// send a patch to Zig.
    unexpected_error_tracing: bool = @import("builtin").mode == .Debug and switch (@import("builtin").zig_backend) {
        .stage2_llvm, .stage2_x86_64 => true,
        else => false,
    },

    /// TODO This is a separate decl instead of a field as a workaround around
    /// compilation errors due to zig not being lazy enough.
    pub const logTerminalMode: fn () Io.Terminal.Mode = log.defaultTerminalMode;

    /// TODO This is a separate decl instead of a field as a workaround around
    /// compilation errors due to zig not being lazy enough.
    pub const elf_debug_info_search_paths: ?fn (exe_path: []const u8) switch (@import("builtin").object_format) {
        .elf => debug.ElfFile.DebugInfoSearchPaths,
        else => void,
    } = if (@hasDecl(root, "std_options_elf_debug_info_search_paths"))
        root.std_options_elf_debug_info_search_paths
    else
        null;

    pub const debug_threaded_io: ?*Io.Threaded = if (@hasDecl(root, "std_options_debug_threaded_io"))
        root.std_options_debug_threaded_io
    else
        Io.Threaded.global_single_threaded;

    /// The `Io` instance that `std.debug` uses for `std.debug.print`,
    /// capturing stack traces, loading debug info, finding the executable's
    /// own path, and environment variables that affect terminal mode
    /// detection. The default is to use statically initialized singleton that
    /// is independent from the application's `Io` instance in order to make
    /// debugging more straightforward. For example, while debugging an `Io`
    /// implementation based on coroutines, one likely wants `std.debug.print`
    /// to directly write to stderr without trying to interact with the code
    /// being debugged.
    pub const debug_io: Io = if (@hasDecl(root, "std_options_debug_io")) root.std_options_debug_io else debug_threaded_io.?.io();

    /// Overrides `std.Io.File.Permissions`.
    pub const FilePermissions: ?type = if (@hasDecl(root, "std_options_FilePermissions")) root.std_options_FilePermissions else null;

    /// Overrides `std.Io.Dir.cwd`.
    pub const cwd: ?fn () Io.Dir = if (@hasDecl(root, "std_options_cwd")) root.std_options_cwd else null;
};

// This forces the start.zig file to be imported, and the comptime logic inside that
// file decides whether to export any appropriate start symbols, and call main.
comptime {
    _ = start;
}

test {
    testing.refAllDecls(@This());
}

comptime {
    debug.assert(@import("std") == @This()); // std lib tests require --zig-lib-dir
}



---
File: /std/tar.zig
---

//! Tar archive is single ordinary file which can contain many files (or
//! directories, symlinks, ...). It's build by series of blocks each size of 512
//! bytes. First block of each entry is header which defines type, name, size
//! permissions and other attributes. Header is followed by series of blocks of
//! file content, if any that entry has content. Content is padded to the block
//! size, so next header always starts at block boundary.
//!
//! This simple format is extended by GNU and POSIX pax extensions to support
//! file names longer than 256 bytes and additional attributes.
//!
//! This is not comprehensive tar parser. Here we are only file types needed to
//! support Zig package manager; normal file, directory, symbolic link. And
//! subset of attributes: name, size, permissions.
//!
//! GNU tar reference: https://www.gnu.org/software/tar/manual/html_node/Standard.html
//! pax reference: https://pubs.opengroup.org/onlinepubs/9699919799/utilities/pax.html#tag_20_92_13

const std = @import("std");
const builtin = @import("builtin");
const Io = std.Io;
const assert = std.debug.assert;
const testing = std.testing;

pub const Writer = @import("tar/Writer.zig");

/// Provide this to receive detailed error messages.
/// When this is provided, some errors which would otherwise be returned
/// immediately will instead be added to this structure. The API user must check
/// the errors in diagnostics to know whether the operation succeeded or failed.
pub const Diagnostics = struct {
    allocator: std.mem.Allocator,
    errors: std.ArrayList(Error) = .empty,

    entries: usize = 0,
    root_dir: []const u8 = "",

    pub const Error = union(enum) {
        unable_to_create_sym_link: struct {
            code: anyerror,
            file_name: []const u8,
            link_name: []const u8,
        },
        unable_to_create_file: struct {
            code: anyerror,
            file_name: []const u8,
        },
        unsupported_file_type: struct {
            file_name: []const u8,
            file_type: Header.Kind,
        },
        components_outside_stripped_prefix: struct {
            file_name: []const u8,
        },
    };

    fn findRoot(d: *Diagnostics, kind: FileKind, path: []const u8) !void {
        if (path.len == 0) return;

        d.entries += 1;
        const root_dir = rootDir(path, kind);
        if (d.entries == 1) {
            d.root_dir = try d.allocator.dupe(u8, root_dir);
            return;
        }
        if (d.root_dir.len == 0 or std.mem.eql(u8, root_dir, d.root_dir))
            return;
        d.allocator.free(d.root_dir);
        d.root_dir = "";
    }

    // Returns root dir of the path, assumes non empty path.
    fn rootDir(path: []const u8, kind: FileKind) []const u8 {
        const start_index: usize = if (path[0] == '/') 1 else 0;
        const end_index: usize = if (path[path.len - 1] == '/') path.len - 1 else path.len;
        const buf = path[start_index..end_index];
        if (std.mem.findScalarPos(u8, buf, 0, '/')) |idx| {
            return buf[0..idx];
        }

        return switch (kind) {
            .file => "",
            .sym_link => "",
            .directory => buf,
        };
    }

    test rootDir {
        const expectEqualStrings = testing.expectEqualStrings;
        try expectEqualStrings("", rootDir("a", .file));
        try expectEqualStrings("a", rootDir("a", .directory));
        try expectEqualStrings("b", rootDir("b", .directory));
        try expectEqualStrings("c", rootDir("/c", .directory));
        try expectEqualStrings("d", rootDir("/d/", .directory));
        try expectEqualStrings("a", rootDir("a/b", .directory));
        try expectEqualStrings("a", rootDir("a/b", .file));
        try expectEqualStrings("a", rootDir("a/b/c", .directory));
    }

    pub fn deinit(d: *Diagnostics) void {
        for (d.errors.items) |item| {
            switch (item) {
                .unable_to_create_sym_link => |info| {
                    d.allocator.free(info.file_name);
                    d.allocator.free(info.link_name);
                },
                .unable_to_create_file => |info| {
                    d.allocator.free(info.file_name);
                },
                .unsupported_file_type => |info| {
                    d.allocator.free(info.file_name);
                },
                .components_outside_stripped_prefix => |info| {
                    d.allocator.free(info.file_name);
                },
            }
        }
        d.errors.deinit(d.allocator);
        d.allocator.free(d.root_dir);
        d.* = undefined;
    }
};

/// Deprecated, renamed to `ExtractOptions`.
pub const PipeOptions = ExtractOptions;

pub const ExtractOptions = struct {
    /// Number of directory levels to skip when extracting files.
    strip_components: u32 = 0,
    /// How to handle the "mode" property of files from within the tar file.
    mode_mode: ModeMode = .executable_bit_only,
    /// Prevents creation of empty directories.
    exclude_empty_directories: bool = false,
    /// Collects error messages during unpacking
    diagnostics: ?*Diagnostics = null,

    pub const ModeMode = enum {
        /// The mode from the tar file is completely ignored. Files are created
        /// with the default mode when creating files.
        ignore,
        /// The mode from the tar file is inspected for the owner executable bit
        /// only. This bit is copied to the group and other executable bits.
        /// Other bits of the mode are left as the default when creating files.
        executable_bit_only,
    };
};

const Header = struct {
    const SIZE = 512;
    const MAX_NAME_SIZE = 100 + 1 + 155; // name(100) + separator(1) + prefix(155)
    const LINK_NAME_SIZE = 100;

    bytes: *const [SIZE]u8,

    const Kind = enum(u8) {
        normal_alias = 0,
        normal = '0',
        hard_link = '1',
        symbolic_link = '2',
        character_special = '3',
        block_special = '4',
        directory = '5',
        fifo = '6',
        contiguous = '7',
        global_extended_header = 'g',
        extended_header = 'x',
        // Types 'L' and 'K' are used by the GNU format for a meta file
        // used to store the path or link name for the next file.
        gnu_long_name = 'L',
        gnu_long_link = 'K',
        gnu_sparse = 'S',
        solaris_extended_header = 'X',
        _,
    };

    /// Includes prefix concatenated, if any.
    /// TODO: check against "../" and other nefarious things
    pub fn fullName(header: Header, buffer: []u8) ![]const u8 {
        const n = name(header);
        const p = prefix(header);
        if (buffer.len < n.len + p.len + 1) return error.TarInsufficientBuffer;
        if (!is_ustar(header) or p.len == 0) {
            @memcpy(buffer[0..n.len], n);
            return buffer[0..n.len];
        }
        @memcpy(buffer[0..p.len], p);
        buffer[p.len] = '/';
        @memcpy(buffer[p.len + 1 ..][0..n.len], n);
        return buffer[0 .. p.len + 1 + n.len];
    }

    /// When kind is symbolic_link linked-to name (target_path) is specified in
    /// the linkname field.
    pub fn linkName(header: Header, buffer: []u8) ![]const u8 {
        const link_name = header.str(157, 100);
        if (link_name.len == 0) {
            return buffer[0..0];
        }
        if (buffer.len < link_name.len) return error.TarInsufficientBuffer;
        const buf = buffer[0..link_name.len];
        @memcpy(buf, link_name);
        return buf;
    }

    pub fn name(header: Header) []const u8 {
        return header.str(0, 100);
    }

    pub fn mode(header: Header) !u32 {
        return @intCast(try header.octal(100, 8));
    }

    pub fn size(header: Header) !u64 {
        const start = 124;
        const len = 12;
        const raw = header.bytes[start..][0..len];
        //  If the leading byte is 0xff (255), all the bytes of the field
        //  (including the leading byte) are concatenated in big-endian order,
        //  with the result being a negative number expressed in two’s
        //  complement form.
        if (raw[0] == 0xff) return error.TarNumericValueNegative;
        // If the leading byte is 0x80 (128), the non-leading bytes of the
        // field are concatenated in big-endian order.
        if (raw[0] == 0x80) {
            if (raw[1] != 0 or raw[2] != 0 or raw[3] != 0) return error.TarNumericValueTooBig;
            return std.mem.readInt(u64, raw[4..12], .big);
        }
        return try header.octal(start, len);
    }

    pub fn chksum(header: Header) !u64 {
        return header.octal(148, 8);
    }

    pub fn is_ustar(header: Header) bool {
        const magic = header.bytes[257..][0..6];
        return std.mem.eql(u8, magic[0..5], "ustar") and (magic[5] == 0 or magic[5] == ' ');
    }

    pub fn prefix(header: Header) []const u8 {
        return header.str(345, 155);
    }

    pub fn kind(header: Header) Kind {
        const result: Kind = @enumFromInt(header.bytes[156]);
        if (result == .normal_alias) return .normal;
        return result;
    }

    fn str(header: Header, start: usize, len: usize) []const u8 {
        return nullStr(header.bytes[start .. start + len]);
    }

    fn octal(header: Header, start: usize, len: usize) !u64 {
        const raw = header.bytes[start..][0..len];
        // Zero-filled octal number in ASCII. Each numeric field of width w
        // contains w minus 1 digits, and a null
        const ltrimmed = std.mem.trimStart(u8, raw, "0 ");
        const rtrimmed = std.mem.trimEnd(u8, ltrimmed, " \x00");
        if (rtrimmed.len == 0) return 0;
        return std.fmt.parseInt(u64, rtrimmed, 8) catch return error.TarHeader;
    }

    const Chksums = struct {
        unsigned: u64,
        signed: i64,
    };

    // Sum of all bytes in the header block. The chksum field is treated as if
    // it were filled with spaces (ASCII 32).
    fn computeChksum(header: Header) Chksums {
        var cs: Chksums = .{ .signed = 0, .unsigned = 0 };
        for (header.bytes, 0..) |v, i| {
            const b = if (148 <= i and i < 156) 32 else v; // Treating chksum bytes as spaces.
            cs.unsigned += b;
            cs.signed += @as(i8, @bitCast(b));
        }
        return cs;
    }

    // Checks calculated chksum with value of chksum field.
    // Returns error or valid chksum value.
    // Zero value indicates empty block.
    pub fn checkChksum(header: Header) !u64 {
        const field = try header.chksum();
        const cs = header.computeChksum();
        if (field == 0 and cs.unsigned == 256) return 0;
        if (field != cs.unsigned and field != cs.signed) return error.TarHeaderChksum;
        return field;
    }
};

// Breaks string on first null character.
fn nullStr(str: []const u8) []const u8 {
    for (str, 0..) |c, i| {
        if (c == 0) return str[0..i];
    }
    return str;
}

/// Type of the file returned by iterator `next` method.
pub const FileKind = enum {
    directory,
    sym_link,
    file,
};

/// Iterator over entries in the tar file represented by reader.
pub const Iterator = struct {
    reader: *Io.Reader,
    diagnostics: ?*Diagnostics = null,

    // buffers for heeader and file attributes
    header_buffer: [Header.SIZE]u8 = undefined,
    file_name_buffer: []u8,
    link_name_buffer: []u8,

    // bytes of padding to the end of the block
    padding: usize = 0,
    // not consumed bytes of file from last next iteration
    unread_file_bytes: u64 = 0,

    /// Options for iterator.
    /// Buffers should be provided by the caller.
    pub const Options = struct {
        /// Use a buffer with length `std.fs.max_path_bytes` to match file system capabilities.
        file_name_buffer: []u8,
        /// Use a buffer with length `std.fs.max_path_bytes` to match file system capabilities.
        link_name_buffer: []u8,
        /// Collects error messages during unpacking
        diagnostics: ?*Diagnostics = null,
    };

    /// Iterates over files in tar archive.
    /// `next` returns each file in tar archive.
    pub fn init(reader: *Io.Reader, options: Options) Iterator {
        return .{
            .reader = reader,
            .diagnostics = options.diagnostics,
            .file_name_buffer = options.file_name_buffer,
            .link_name_buffer = options.link_name_buffer,
        };
    }

    pub const File = struct {
        name: []const u8, // name of file, symlink or directory
        link_name: []const u8, // target name of symlink
        size: u64 = 0, // size of the file in bytes
        mode: u32 = 0,
        kind: FileKind = .file,
    };

    fn readHeader(self: *Iterator) !?Header {
        if (self.padding > 0) {
            try self.reader.discardAll(self.padding);
        }
        const n = try self.reader.readSliceShort(&self.header_buffer);
        if (n == 0) return null;
        if (n < Header.SIZE) return error.UnexpectedEndOfStream;
        const header = Header{ .bytes = self.header_buffer[0..Header.SIZE] };
        if (try header.checkChksum() == 0) return null;
        return header;
    }

    fn readString(self: *Iterator, size: usize, buffer: []u8) ![]const u8 {
        if (size > buffer.len) return error.TarInsufficientBuffer;
        const buf = buffer[0..size];
        try self.reader.readSliceAll(buf);
        return nullStr(buf);
    }

    fn newFile(self: *Iterator) File {
        return .{
            .name = self.file_name_buffer[0..0],
            .link_name = self.link_name_buffer[0..0],
        };
    }

    // Number of padding bytes in the last file block.
    fn blockPadding(size: u64) usize {
        const block_rounded = std.mem.alignForward(u64, size, Header.SIZE); // size rounded to te block boundary
        return @intCast(block_rounded - size);
    }

    /// Iterates through the tar archive as if it is a series of files.
    /// Internally, the tar format often uses entries (header with optional
    /// content) to add meta data that describes the next file. These
    /// entries should not normally be visible to the outside. As such, this
    /// loop iterates through one or more entries until it collects a all
    /// file attributes.
    pub fn next(self: *Iterator) !?File {
        if (self.unread_file_bytes > 0) {
            // If file content was not consumed by caller
            try self.reader.discardAll64(self.unread_file_bytes);
            self.unread_file_bytes = 0;
        }
        var file: File = self.newFile();

        while (try self.readHeader()) |header| {
            const kind = header.kind();
            const size: u64 = try header.size();
            self.padding = blockPadding(size);

            switch (kind) {
                // File types to return upstream
                .directory, .normal, .symbolic_link => {
                    file.kind = switch (kind) {
                        .directory => .directory,
                        .normal => .file,
                        .symbolic_link => .sym_link,
                        else => unreachable,
                    };
                    file.mode = try header.mode();

                    // set file attributes if not already set by prefix/extended headers
                    if (file.size == 0) {
                        file.size = size;
                    }
                    if (file.link_name.len == 0) {
                        file.link_name = try header.linkName(self.link_name_buffer);
                    }
                    if (file.name.len == 0) {
                        file.name = try header.fullName(self.file_name_buffer);
                    }

                    self.padding = blockPadding(file.size);
                    self.unread_file_bytes = file.size;
                    return file;
                },
                // Prefix header types
                .gnu_long_name => {
                    file.name = try self.readString(@intCast(size), self.file_name_buffer);
                },
                .gnu_long_link => {
                    file.link_name = try self.readString(@intCast(size), self.link_name_buffer);
                },
                .extended_header => {
                    // Use just attributes from last extended header.
                    file = self.newFile();

                    var rdr: PaxIterator = .{
                        .reader = self.reader,
                        .size = @intCast(size),
                    };
                    while (try rdr.next()) |attr| {
                        switch (attr.kind) {
                            .path => {
                                file.name = try attr.value(self.file_name_buffer);
                            },
                            .linkpath => {
                                file.link_name = try attr.value(self.link_name_buffer);
                            },
                            .size => {
                                var buf: [pax_max_size_attr_len]u8 = undefined;
                                file.size = try std.fmt.parseInt(u64, try attr.value(&buf), 10);
                            },
                        }
                    }
                },
                // Ignored header type
                .global_extended_header => {
                    self.reader.discardAll64(size) catch return error.TarHeadersTooBig;
                },
                // All other are unsupported header types
                else => {
                    const d = self.diagnostics orelse return error.TarUnsupportedHeader;
                    try d.errors.append(d.allocator, .{ .unsupported_file_type = .{
                        .file_name = try d.allocator.dupe(u8, header.name()),
                        .file_type = kind,
                    } });
                    if (kind == .gnu_sparse) {
                        try self.skipGnuSparseExtendedHeaders(header);
                    }
                    self.reader.discardAll64(size) catch return error.TarHeadersTooBig;
                },
            }
        }
        return null;
    }

    pub fn streamRemaining(it: *Iterator, file: File, w: *Io.Writer) Io.Reader.StreamError!void {
        try it.reader.streamExact64(w, file.size);
        it.unread_file_bytes = 0;
    }

    fn skipGnuSparseExtendedHeaders(self: *Iterator, header: Header) !void {
        var is_extended = header.bytes[482] > 0;
        while (is_extended) {
            var buf: [Header.SIZE]u8 = undefined;
            try self.reader.readSliceAll(&buf);
            is_extended = buf[504] > 0;
        }
    }
};

const PaxAttributeKind = enum {
    path,
    linkpath,
    size,
};

// maxInt(u64) has 20 chars, base 10 in practice we got 24 chars
const pax_max_size_attr_len = 64;

pub const PaxIterator = struct {
    size: usize, // cumulative size of all pax attributes
    reader: *Io.Reader,

    const Self = @This();

    const Attribute = struct {
        kind: PaxAttributeKind,
        len: usize, // length of the attribute value
        reader: *Io.Reader, // reader positioned at value start

        // Copies pax attribute value into destination buffer.
        // Must be called with destination buffer of size at least Attribute.len.
        pub fn value(self: Attribute, dst: []u8) ![]const u8 {
            if (self.len > dst.len) return error.TarInsufficientBuffer;
            // assert(self.len <= dst.len);
            const buf = dst[0..self.len];
            const n = try self.reader.readSliceShort(buf);
            if (n < self.len) return error.UnexpectedEndOfStream;
            try validateAttributeEnding(self.reader);
            if (hasNull(buf)) return error.PaxNullInValue;
            return buf;
        }
    };

    // Iterates over pax attributes. Returns known only known attributes.
    // Caller has to call value in Attribute, to advance reader across value.
    pub fn next(self: *Self) !?Attribute {
        // Pax extended header consists of one or more attributes, each constructed as follows:
        // "%d %s=%s\n", <length>, <keyword>, <value>
        while (self.size > 0) {
            const length_buf = try self.reader.takeSentinel(' ');
            const length = try std.fmt.parseInt(usize, length_buf, 10); // record length in bytes

            const keyword = try self.reader.takeSentinel('=');
            if (hasNull(keyword)) return error.PaxNullInKeyword;

            // calculate value_len
            const value_start = length_buf.len + keyword.len + 2; // 2 separators
            if (length < value_start + 1 or self.size < length) return error.UnexpectedEndOfStream;
            const value_len = length - value_start - 1; // \n separator at end
            self.size -= length;

            const kind: PaxAttributeKind = if (eql(keyword, "path"))
                .path
            else if (eql(keyword, "linkpath"))
                .linkpath
            else if (eql(keyword, "size"))
                .size
            else {
                try self.reader.discardAll(value_len);
                try validateAttributeEnding(self.reader);
                continue;
            };
            if (kind == .size and value_len > pax_max_size_attr_len) {
                return error.PaxSizeAttrOverflow;
            }
            return .{
                .kind = kind,
                .len = value_len,
                .reader = self.reader,
            };
        }

        return null;
    }

    fn eql(a: []const u8, b: []const u8) bool {
        return std.mem.eql(u8, a, b);
    }

    fn hasNull(str: []const u8) bool {
        return (std.mem.findScalar(u8, str, 0)) != null;
    }

    // Checks that each record ends with new line.
    fn validateAttributeEnding(reader: *Io.Reader) !void {
        if (try reader.takeByte() != '\n') return error.PaxInvalidAttributeEnd;
    }
};

/// Deprecated, renamed to `extract`.
pub const pipeToFileSystem = extract;

/// Ingests tar file from `reader`, populating file contents within `dir`. If
/// any file would be extracted outside of `dir`, an error is return instead.
pub fn extract(io: Io, dir: Io.Dir, reader: *Io.Reader, options: ExtractOptions) !void {
    var file_name_buffer: [Io.Dir.max_path_bytes]u8 = undefined;
    var link_name_buffer: [Io.Dir.max_path_bytes]u8 = undefined;
    var sanitize_buffer: [Io.Dir.max_path_bytes]u8 = undefined;
    var file_contents_buffer: [1024]u8 = undefined;
    var it: Iterator = .init(reader, .{
        .file_name_buffer = &file_name_buffer,
        .link_name_buffer = &link_name_buffer,
        .diagnostics = options.diagnostics,
    });

    while (try it.next()) |file| {
        const n = sanitizePath(&sanitize_buffer, file.name, options.strip_components) catch 0;
        if (n == 0 and file.kind != .directory) {
            const d = options.diagnostics orelse return error.TarComponentsOutsideStrippedPrefix;
            try d.errors.append(d.allocator, .{ .components_outside_stripped_prefix = .{
                .file_name = try d.allocator.dupe(u8, file.name),
            } });
            continue;
        }
        const file_name = sanitize_buffer[0..n];
        if (options.diagnostics) |d| {
            try d.findRoot(file.kind, file_name);
        }

        switch (file.kind) {
            .directory => {
                if (file_name.len > 0 and !options.exclude_empty_directories) {
                    try dir.createDirPath(io, file_name);
                }
            },
            .file => {
                if (createDirAndFile(io, dir, file_name, filePermissions(file.mode, options))) |fs_file| {
                    defer fs_file.close(io);
                    var file_writer = fs_file.writer(io, &file_contents_buffer);
                    try it.streamRemaining(file, &file_writer.interface);
                    try file_writer.interface.flush();
                } else |err| {
                    const d = options.diagnostics orelse return err;
                    try d.errors.append(d.allocator, .{ .unable_to_create_file = .{
                        .code = err,
                        .file_name = try d.allocator.dupe(u8, file_name),
                    } });
                }
            },
            .sym_link => {
                const link_name = file.link_name;
                createDirAndSymlink(io, dir, link_name, file_name) catch |err| {
                    const d = options.diagnostics orelse return error.UnableToCreateSymLink;
                    try d.errors.append(d.allocator, .{ .unable_to_create_sym_link = .{
                        .code = err,
                        .file_name = try d.allocator.dupe(u8, file_name),
                        .link_name = try d.allocator.dupe(u8, link_name),
                    } });
                };
            },
        }
    }
}

fn createDirAndFile(io: Io, dir: Io.Dir, file_name: []const u8, permissions: Io.File.Permissions) !Io.File {
    const fs_file = dir.createFile(io, file_name, .{ .exclusive = true, .permissions = permissions }) catch |err| {
        if (err == error.FileNotFound) {
            if (std.fs.path.dirname(file_name)) |dir_name| {
                try dir.createDirPath(io, dir_name);
                return try dir.createFile(io, file_name, .{ .exclusive = true, .permissions = permissions });
            }
        }
        return err;
    };
    return fs_file;
}

// Creates a symbolic link at path `file_name` which points to `link_name`.
fn createDirAndSymlink(io: Io, dir: Io.Dir, link_name: []const u8, file_name: []const u8) !void {
    dir.symLink(io, link_name, file_name, .{}) catch |err| {
        if (err == error.FileNotFound) {
            if (std.fs.path.dirname(file_name)) |dir_name| {
                try dir.createDirPath(io, dir_name);
                return try dir.symLink(io, link_name, file_name, .{});
            }
        }
        return err;
    };
}

fn sanitizePath(buffer: []u8, path: []const u8, strip_components: u32) error{Invalid}!usize {
    if (path.len == 0 or path[0] == '/') return error.Invalid;
    var i: usize = 0;
    var c = strip_components;
    var it = std.mem.tokenizeScalar(u8, path, '/');
    while (it.next()) |component| {
        if (std.mem.eql(u8, component, ".")) continue;
        if (std.mem.eql(u8, component, "..")) {
            if (i == 0) return error.Invalid;
            while (true) {
                const ends_with_slash = buffer[i - 1] == '/';
                i -= 1;
                if (ends_with_slash or i == 0) break;
            }
            continue;
        }
        if (c > 0) {
            c -= 1;
            continue;
        }
        if (i > 0) {
            buffer[i] = '/';
            i += 1;
        }
        @memcpy(buffer[i..][0..component.len], component);
        i += component.len;
    }
    if (c > 0) return error.Invalid;
    return i;
}

fn testSanitizePath(expected: []const u8, input: []const u8, strip: u32) !void {
    var buffer: [Io.Dir.max_path_bytes]u8 = undefined;
    const result = buffer[0..try sanitizePath(&buffer, input, strip)];
    try testing.expectEqualStrings(expected, result);
}

fn testSanitizePathError(expected: anyerror, input: []const u8, strip: u32) !void {
    var buffer: [Io.Dir.max_path_bytes]u8 = undefined;
    try testing.expectError(expected, sanitizePath(&buffer, input, strip));
}

test sanitizePath {
    try testSanitizePath("a/b/c", "a/b/c", 0);
    try testSanitizePath("a/b/c", "a/x/y/../../b/c", 0);
    try testSanitizePath("b/c", "a/b/c", 1);
    try testSanitizePath("c", "a/b/c", 2);
    try testSanitizePath("", "a/b/c", 3);
    try testSanitizePath("", "a/b/c/../../..", 0);
    try testSanitizePathError(error.Invalid, "a/b/c", 4);
    try testSanitizePathError(error.Invalid, "..", 0);
    try testSanitizePathError(error.Invalid, "a/b/../../..", 0);
    try testSanitizePathError(error.Invalid, "a/b/../..", 1);
}

test PaxIterator {
    const Attr = struct {
        kind: PaxAttributeKind,
        value: []const u8 = undefined,
        err: ?anyerror = null,
    };
    const cases = [_]struct {
        data: []const u8,
        attrs: []const Attr,
        err: ?anyerror = null,
    }{
        .{ // valid but unknown keys
            .data =
            \\30 mtime=1350244992.023960108
            \\6 k=1
            \\13 key1=val1
            \\10 a=name
            \\9 a=name
            \\
            ,
            .attrs = &[_]Attr{},
        },
        .{ // mix of known and unknown keys
            .data =
            \\6 k=1
            \\13 path=name
            \\17 linkpath=link
            \\13 key1=val1
            \\12 size=123
            \\13 key2=val2
            \\
            ,
            .attrs = &[_]Attr{
                .{ .kind = .path, .value = "name" },
                .{ .kind = .linkpath, .value = "link" },
                .{ .kind = .size, .value = "123" },
            },
        },
        .{ // too short size of the second key-value pair
            .data =
            \\13 path=name
            \\10 linkpath=value
            \\
            ,
            .attrs = &[_]Attr{
                .{ .kind = .path, .value = "name" },
            },
            .err = error.UnexpectedEndOfStream,
        },
        .{ // too long size of the second key-value pair
            .data =
            \\13 path=name
            \\6 k=1
            \\19 linkpath=value
            \\
            ,
            .attrs = &[_]Attr{
                .{ .kind = .path, .value = "name" },
            },
            .err = error.UnexpectedEndOfStream,
        },

        .{ // too long size of the second key-value pair
            .data =
            \\13 path=name
            \\19 linkpath=value
            \\6 k=1
            \\
            ,
            .attrs = &[_]Attr{
                .{ .kind = .path, .value = "name" },
                .{ .kind = .linkpath, .err = error.PaxInvalidAttributeEnd },
            },
        },
        .{ // null in keyword is not valid
            .data = "13 path=name\n" ++ "7 k\x00b=1\n",
            .attrs = &[_]Attr{
                .{ .kind = .path, .value = "name" },
            },
            .err = error.PaxNullInKeyword,
        },
        .{ // null in value is not valid
            .data = "23 path=name\x00with null\n",
            .attrs = &[_]Attr{
                .{ .kind = .path, .err = error.PaxNullInValue },
            },
        },
        .{ // 1000 characters path
            .data = "1011 path=" ++ "0123456789" ** 100 ++ "\n",
            .attrs = &[_]Attr{
                .{ .kind = .path, .value = "0123456789" ** 100 },
            },
        },
    };
    var buffer: [1024]u8 = undefined;

    outer: for (cases) |case| {
        var reader: Io.Reader = .fixed(case.data);
        var it: PaxIterator = .{
            .size = case.data.len,
            .reader = &reader,
        };

        var i: usize = 0;
        while (it.next() catch |err| {
            if (case.err) |e| {
                try testing.expectEqual(e, err);
                continue;
            }
            return err;
        }) |attr| : (i += 1) {
            const exp = case.attrs[i];
            try testing.expectEqual(exp.kind, attr.kind);
            const value = attr.value(&buffer) catch |err| {
                if (exp.err) |e| {
                    try testing.expectEqual(e, err);
                    break :outer;
                }
                return err;
            };
            try testing.expectEqualStrings(exp.value, value);
        }
        try testing.expectEqual(case.attrs.len, i);
        try testing.expect(case.err == null);
    }
}

test "header parse size" {
    const cases = [_]struct {
        in: []const u8,
        want: u64 = 0,
        err: ?anyerror = null,
    }{
        // Test base-256 (binary) encoded values.
        .{ .in = "", .want = 0 },
        .{ .in = "\x80", .want = 0 },
        .{ .in = "\x80\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x01", .want = 1 },
        .{ .in = "\x80\x00\x00\x00\x00\x00\x00\x00\x00\x00\x01\x02", .want = 0x0102 },
        .{ .in = "\x80\x00\x00\x00\x01\x02\x03\x04\x05\x06\x07\x08", .want = 0x0102030405060708 },
        .{ .in = "\x80\x00\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09", .err = error.TarNumericValueTooBig },
        .{ .in = "\x80\x00\x00\x00\x07\x76\xa2\x22\xeb\x8a\x72\x61", .want = 537795476381659745 },
        .{ .in = "\x80\x80\x80\x00\x01\x02\x03\x04\x05\x06\x07\x08", .err = error.TarNumericValueTooBig },

        // // Test base-8 (octal) encoded values.
        .{ .in = "00000000227\x00", .want = 0o227 },
        .{ .in = "  000000227\x00", .want = 0o227 },
        .{ .in = "00000000228\x00", .err = error.TarHeader },
        .{ .in = "11111111111\x00", .want = 0o11111111111 },
    };

    for (cases) |case| {
        var bytes = [_]u8{0} ** Header.SIZE;
        @memcpy(bytes[124 .. 124 + case.in.len], case.in);
        var header = Header{ .bytes = &bytes };
        if (case.err) |err| {
            try testing.expectError(err, header.size());
        } else {
            try testing.expectEqual(case.want, try header.size());
        }
    }
}

test "header parse mode" {
    const cases = [_]struct {
        in: []const u8,
        want: u64 = 0,
        err: ?anyerror = null,
    }{
        .{ .in = "0000644\x00", .want = 0o644 },
        .{ .in = "0000777\x00", .want = 0o777 },
        .{ .in = "7777777\x00", .want = 0o7777777 },
        .{ .in = "7777778\x00", .err = error.TarHeader },
        .{ .in = "77777777", .want = 0o77777777 },
        .{ .in = "777777777777", .want = 0o77777777 },
    };
    for (cases) |case| {
        var bytes = [_]u8{0} ** Header.SIZE;
        @memcpy(bytes[100 .. 100 + case.in.len], case.in);
        var header = Header{ .bytes = &bytes };
        if (case.err) |err| {
            try testing.expectError(err, header.mode());
        } else {
            try testing.expectEqual(case.want, try header.mode());
        }
    }
}

test "create file and symlink" {
    const io = testing.io;

    var root = testing.tmpDir(.{});
    defer root.cleanup();

    var file = try createDirAndFile(io, root.dir, "file1", .default_file);
    file.close(io);
    file = try createDirAndFile(io, root.dir, "a/b/c/file2", .default_file);
    file.close(io);

    createDirAndSymlink(io, root.dir, "a/b/c/file2", "symlink1") catch |err| switch (err) {
        // On Windows, symlinks require developer mode/admin privileges and the underlying filesystem must support symlinks
        error.AccessDenied, error.PermissionDenied, error.FileSystem => if (builtin.os.tag == .windows) return error.SkipZigTest else return err,
        else => return err,
    };
    try createDirAndSymlink(io, root.dir, "../../../file1", "d/e/f/symlink2");

    // Danglink symlnik, file created later
    try createDirAndSymlink(io, root.dir, "../../../g/h/i/file4", "j/k/l/symlink3");
    file = try createDirAndFile(io, root.dir, "g/h/i/file4", .default_file);
    file.close(io);
}

test Iterator {
    // Example tar file is created from this tree structure:
    // $ tree example
    //    example
    //    ├── a
    //    │   └── file
    //    ├── b
    //    │   └── symlink -> ../a/file
    //    └── empty
    // $ cat example/a/file
    //   content
    // $ tar -cf example.tar example
    // $ tar -tvf example.tar
    //    example/
    //    example/b/
    //    example/b/symlink -> ../a/file
    //    example/a/
    //    example/a/file
    //    example/empty/

    const data = @embedFile("tar/testdata/example.tar");
    var reader: Io.Reader = .fixed(data);

    // User provided buffers to the iterator
    var file_name_buffer: [std.fs.max_path_bytes]u8 = undefined;
    var link_name_buffer: [std.fs.max_path_bytes]u8 = undefined;
    // Create iterator
    var it: Iterator = .init(&reader, .{
        .file_name_buffer = &file_name_buffer,
        .link_name_buffer = &link_name_buffer,
    });
    // Iterate over files in example.tar
    var file_no: usize = 0;
    while (try it.next()) |file| : (file_no += 1) {
        switch (file.kind) {
            .directory => {
                switch (file_no) {
                    0 => try testing.expectEqualStrings("example/", file.name),
                    1 => try testing.expectEqualStrings("example/b/", file.name),
                    3 => try testing.expectEqualStrings("example/a/", file.name),
                    5 => try testing.expectEqualStrings("example/empty/", file.name),
                    else => unreachable,
                }
            },
            .file => {
                try testing.expectEqualStrings("example/a/file", file.name);
                var buf: [16]u8 = undefined;
                var w: Io.Writer = .fixed(&buf);
                try it.streamRemaining(file, &w);
                try testing.expectEqualStrings("content\n", w.buffered());
            },
            .sym_link => {
                try testing.expectEqualStrings("example/b/symlink", file.name);
                try testing.expectEqualStrings("../a/file", file.link_name);
            },
        }
    }
}

test extract {
    const io = testing.io;
    // Example tar file is created from this tree structure:
    // $ tree example
    //    example
    //    ├── a
    //    │   └── file
    //    ├── b
    //    │   └── symlink -> ../a/file
    //    └── empty
    // $ cat example/a/file
    //   content
    // $ tar -cf example.tar example
    // $ tar -tvf example.tar
    //    example/
    //    example/b/
    //    example/b/symlink -> ../a/file
    //    example/a/
    //    example/a/file
    //    example/empty/

    const data = @embedFile("tar/testdata/example.tar");
    var reader: Io.Reader = .fixed(data);

    var tmp = testing.tmpDir(.{ .follow_symlinks = false });
    defer tmp.cleanup();
    const dir = tmp.dir;

    // Save tar from reader to the file system `dir`
    extract(io, dir, &reader, .{
        .mode_mode = .ignore,
        .strip_components = 1,
        .exclude_empty_directories = true,
    }) catch |err| {
        // Skip on platform which don't support symlinks
        if (err == error.UnableToCreateSymLink) return error.SkipZigTest;
        return err;
    };

    try testing.expectError(error.FileNotFound, dir.statFile(io, "empty", .{}));
    try testing.expect((try dir.statFile(io, "a/file", .{})).kind == .file);
    try testing.expect((try dir.statFile(io, "b/symlink", .{})).kind == .file); // statFile follows symlink

    var buf: [32]u8 = undefined;
    try testing.expectEqualSlices(
        u8,
        "../a/file",
        normalizePath(buf[0..try dir.readLink(io, "b/symlink", &buf)]),
    );
}

test "extract root_dir" {
    const io = testing.io;
    const data = @embedFile("tar/testdata/example.tar");
    var reader: Io.Reader = .fixed(data);

    // with strip_components = 1
    {
        var tmp = testing.tmpDir(.{ .follow_symlinks = false });
        defer tmp.cleanup();
        var diagnostics: Diagnostics = .{ .allocator = testing.allocator };
        defer diagnostics.deinit();

        extract(io, tmp.dir, &reader, .{
            .strip_components = 1,
            .diagnostics = &diagnostics,
        }) catch |err| {
            // Skip on platform which don't support symlinks
            if (err == error.UnableToCreateSymLink) return error.SkipZigTest;
            return err;
        };

        // there is no root_dir
        try testing.expectEqual(0, diagnostics.root_dir.len);
        try testing.expectEqual(5, diagnostics.entries);
    }

    // with strip_components = 0
    {
        reader = .fixed(data);
        var tmp = testing.tmpDir(.{ .follow_symlinks = false });
        defer tmp.cleanup();
        var diagnostics: Diagnostics = .{ .allocator = testing.allocator };
        defer diagnostics.deinit();

        extract(io, tmp.dir, &reader, .{
            .strip_components = 0,
            .diagnostics = &diagnostics,
        }) catch |err| {
            // Skip on platform which don't support symlinks
            if (err == error.UnableToCreateSymLink) return error.SkipZigTest;
            return err;
        };

        // root_dir found
        try testing.expectEqualStrings("example", diagnostics.root_dir);
        try testing.expectEqual(6, diagnostics.entries);
    }
}

test "findRoot with single file archive" {
    const io = testing.io;
    const data = @embedFile("tar/testdata/22752.tar");
    var reader: Io.Reader = .fixed(data);

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();

    var diagnostics: Diagnostics = .{ .allocator = testing.allocator };
    defer diagnostics.deinit();
    try extract(io, tmp.dir, &reader, .{ .diagnostics = &diagnostics });

    try testing.expectEqualStrings("", diagnostics.root_dir);
}

test "findRoot without explicit root dir" {
    const io = testing.io;
    const data = @embedFile("tar/testdata/19820.tar");
    var reader: Io.Reader = .fixed(data);

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();

    var diagnostics: Diagnostics = .{ .allocator = testing.allocator };
    defer diagnostics.deinit();
    try extract(io, tmp.dir, &reader, .{ .diagnostics = &diagnostics });

    try testing.expectEqualStrings("root", diagnostics.root_dir);
}

test "extract strip_components" {
    const io = testing.io;
    const data = @embedFile("tar/testdata/example.tar");
    var reader: Io.Reader = .fixed(data);

    var tmp = testing.tmpDir(.{ .follow_symlinks = false });
    defer tmp.cleanup();
    var diagnostics: Diagnostics = .{ .allocator = testing.allocator };
    defer diagnostics.deinit();

    extract(io, tmp.dir, &reader, .{
        .strip_components = 3,
        .diagnostics = &diagnostics,
    }) catch |err| {
        // Skip on platform which don't support symlinks
        if (err == error.UnableToCreateSymLink) return error.SkipZigTest;
        return err;
    };

    try testing.expectEqual(2, diagnostics.errors.items.len);
    try testing.expectEqualStrings("example/b/symlink", diagnostics.errors.items[0].components_outside_stripped_prefix.file_name);
    try testing.expectEqualStrings("example/a/file", diagnostics.errors.items[1].components_outside_stripped_prefix.file_name);
}

fn normalizePath(bytes: []u8) []u8 {
    const canonical_sep = std.fs.path.sep_posix;
    if (std.fs.path.sep == canonical_sep) return bytes;
    std.mem.replaceScalar(u8, bytes, std.fs.path.sep, canonical_sep);
    return bytes;
}

// File system mode based on tar header mode and mode_mode options.
fn filePermissions(mode: u32, options: ExtractOptions) Io.File.Permissions {
    return if (!Io.File.Permissions.has_executable_bit or options.mode_mode == .ignore or (mode & 0o100) == 0)
        .default_file
    else
        .executable_file;
}

test filePermissions {
    if (!Io.File.Permissions.has_executable_bit) return error.SkipZigTest;
    try testing.expectEqual(Io.File.Permissions.default_file, filePermissions(0o744, .{ .mode_mode = .ignore }));
    try testing.expectEqual(Io.File.Permissions.executable_file, filePermissions(0o744, .{}));
    try testing.expectEqual(Io.File.Permissions.default_file, filePermissions(0o644, .{}));
    try testing.expectEqual(Io.File.Permissions.default_file, filePermissions(0o655, .{}));
}

test "executable bit" {
    if (!Io.File.Permissions.has_executable_bit) return error.SkipZigTest;

    const io = testing.io;
    const S = std.posix.S;
    const data = @embedFile("tar/testdata/example.tar");

    for ([_]ExtractOptions.ModeMode{ .ignore, .executable_bit_only }) |opt| {
        var reader: Io.Reader = .fixed(data);

        var tmp = testing.tmpDir(.{ .follow_symlinks = false });
        //defer tmp.cleanup();

        extract(io, tmp.dir, &reader, .{
            .strip_components = 1,
            .exclude_empty_directories = true,
            .mode_mode = opt,
        }) catch |err| {
            // Skip on platform which don't support symlinks
            if (err == error.UnableToCreateSymLink) return error.SkipZigTest;
            return err;
        };

        const fs = try tmp.dir.statFile(io, "a/file", .{});
        try testing.expect(fs.kind == .file);

        const mode = fs.permissions.toMode();

        if (opt == .executable_bit_only) {
            // Executable bit is set for user, group and others
            try testing.expect(mode & S.IXUSR > 0);
            try testing.expect(mode & S.IXGRP > 0);
            try testing.expect(mode & S.IXOTH > 0);
        }
        if (opt == .ignore) {
            try testing.expect(mode & S.IXUSR == 0);
            try testing.expect(mode & S.IXGRP == 0);
            try testing.expect(mode & S.IXOTH == 0);
        }
    }
}

test {
    _ = @import("tar/test.zig");
    _ = Writer;
    _ = Diagnostics;
}



---
File: /std/Target.zig
---

//! All the details about the machine that will be executing code.
//! Unlike `Query` which might leave some things as "default" or "host", this
//! data is fully resolved into a concrete set of OS versions, CPU features,
//! etc.

cpu: Cpu,
os: Os,
abi: Abi,
ofmt: ObjectFormat,
dynamic_linker: DynamicLinker = DynamicLinker.none,

pub const Query = @import("Target/Query.zig");

pub const Os = struct {
    tag: Tag,
    version_range: VersionRange,

    pub const Tag = enum {
        freestanding,
        other,

        contiki,
        fuchsia,
        hermit,
        managarm,

        haiku,
        hurd,
        illumos,
        linux,
        plan9,
        rtems,
        serenity,

        dragonfly,
        freebsd,
        netbsd,
        openbsd,

        driverkit,
        ios,
        maccatalyst,
        macos,
        tvos,
        visionos,
        watchos,

        windows,
        uefi,

        @"3ds",

        ps3,
        ps4,
        ps5,
        psp,
        vita,

        emscripten,
        wasi,

        amdhsa,
        amdpal,
        cuda,
        mesa3d,
        nvcl,
        opencl,
        opengl,
        vulkan,

        // LLVM tags deliberately omitted:
        // - bridgeos
        // - cheriotrtos
        // - darwin
        // - kfreebsd
        // - nacl
        // - shadermodel

        pub inline fn isDarwin(tag: Tag) bool {
            return switch (tag) {
                .driverkit,
                .ios,
                .maccatalyst,
                .macos,
                .tvos,
                .visionos,
                .watchos,
                => true,
                else => false,
            };
        }

        pub inline fn isBSD(tag: Tag) bool {
            return tag.isDarwin() or switch (tag) {
                .freebsd, .openbsd, .netbsd, .dragonfly => true,
                else => false,
            };
        }

        pub fn exeFileExt(tag: Tag, arch: Cpu.Arch) [:0]const u8 {
            return switch (tag) {
                .windows => ".exe",
                .uefi => ".efi",
                .plan9 => arch.plan9Ext(),
                else => switch (arch) {
                    .wasm32, .wasm64 => ".wasm",
                    else => "",
                },
            };
        }

        pub fn staticLibSuffix(tag: Tag, abi: Abi) [:0]const u8 {
            return switch (abi) {
                .msvc, .itanium => ".lib",
                else => switch (tag) {
                    .windows, .uefi => ".lib",
                    else => ".a",
                },
            };
        }

        pub fn dynamicLibSuffix(tag: Tag) [:0]const u8 {
            return switch (tag) {
                .windows, .uefi => ".dll",
                .driverkit,
                .ios,
                .maccatalyst,
                .macos,
                .tvos,
                .visionos,
                .watchos,
                => ".dylib",
                else => ".so",
            };
        }

        pub fn libPrefix(tag: Os.Tag, abi: Abi) [:0]const u8 {
            return switch (abi) {
                .msvc, .itanium => "",
                else => switch (tag) {
                    .windows, .uefi => "",
                    else => "lib",
                },
            };
        }

        pub fn defaultVersionRange(tag: Tag, arch: Cpu.Arch, abi: Abi) Os {
            return .{
                .tag = tag,
                .version_range = .default(arch, tag, abi),
            };
        }

        pub inline fn versionRangeTag(tag: Tag) @typeInfo(TaggedVersionRange).@"union".tag_type.? {
            return switch (tag) {
                .freestanding,
                .other,

                .managarm,

                .haiku,
                .illumos,
                .plan9,
                .serenity,

                .ps3,
                .ps4,
                .ps5,

                .emscripten,

                .mesa3d,
                => .none,

                .contiki,
                .fuchsia,
                .hermit,

                .rtems,

                .dragonfly,
                .freebsd,
                .netbsd,
                .openbsd,

                .driverkit,
                .ios,
                .maccatalyst,
                .macos,
                .tvos,
                .visionos,
                .watchos,

                .uefi,

                .@"3ds",

                .psp,
                .vita,

                .wasi,

                .amdhsa,
                .amdpal,
                .cuda,
                .nvcl,
                .opencl,
                .opengl,
                .vulkan,
                => .semver,

                .hurd => .hurd,
                .linux => .linux,

                .windows => .windows,
            };
        }
    };

    /// Based on NTDDI version constants from
    /// https://docs.microsoft.com/en-us/cpp/porting/modifying-winver-and-win32-winnt
    pub const WindowsVersion = enum(u32) {
        nt4 = 0x04000000,
        win2k = 0x05000000,
        xp = 0x05010000,
        ws2003 = 0x05020000,
        vista = 0x06000000,
        win7 = 0x06010000,
        win8 = 0x06020000,
        win8_1 = 0x06030000,
        win10 = 0x0A000000, //aka win10_th1
        win10_th2 = 0x0A000001,
        win10_rs1 = 0x0A000002,
        win10_rs2 = 0x0A000003,
        win10_rs3 = 0x0A000004,
        win10_rs4 = 0x0A000005,
        win10_rs5 = 0x0A000006,
        win10_19h1 = 0x0A000007,
        win10_vb = 0x0A000008, //aka win10_19h2
        win10_mn = 0x0A000009, //aka win10_20h1
        win10_fe = 0x0A00000A, //aka win10_20h2
        win10_co = 0x0A00000B, //aka win10_21h1
        win10_ni = 0x0A00000C, //aka win10_21h2
        win10_cu = 0x0A00000D, //aka win10_22h2
        win11_zn = 0x0A00000E, //aka win11_21h2
        win11_ga = 0x0A00000F, //aka win11_22h2
        win11_ge = 0x0A000010, //aka win11_23h2
        win11_dt = 0x0A000011, //aka win11_24h2
        _,

        /// Latest Windows version that the Zig Standard Library is aware of
        pub const latest = WindowsVersion.win11_dt;

        /// Compared against build numbers reported by the runtime to distinguish win10 versions,
        /// where 0x0A000000 + index corresponds to the WindowsVersion u32 value.
        pub const known_win10_build_numbers = [_]u32{
            10240, //win10 aka win10_th1
            10586, //win10_th2
            14393, //win10_rs1
            15063, //win10_rs2
            16299, //win10_rs3
            17134, //win10_rs4
            17763, //win10_rs5
            18362, //win10_19h1
            18363, //win10_vb aka win10_19h2
            19041, //win10_mn aka win10_20h1
            19042, //win10_fe aka win10_20h2
            19043, //win10_co aka win10_21h1
            19044, //win10_ni aka win10_21h2
            19045, //win10_cu aka win10_22h2
            22000, //win11_zn aka win11_21h2
            22621, //win11_ga aka win11_22h2
            22631, //win11_ge aka win11_23h2
            26100, //win11_dt aka win11_24h2
        };

        /// Returns whether the first version `ver` is newer (greater) than or equal to the second version `ver`.
        pub inline fn isAtLeast(ver: WindowsVersion, min_ver: WindowsVersion) bool {
            return @intFromEnum(ver) >= @intFromEnum(min_ver);
        }

        pub const Range = struct {
            min: WindowsVersion,
            max: WindowsVersion,

            pub inline fn includesVersion(range: Range, ver: WindowsVersion) bool {
                return @intFromEnum(ver) >= @intFromEnum(range.min) and
                    @intFromEnum(ver) <= @intFromEnum(range.max);
            }

            /// Checks if system is guaranteed to be at least `version` or older than `version`.
            /// Returns `null` if a runtime check is required.
            pub inline fn isAtLeast(range: Range, min_ver: WindowsVersion) ?bool {
                if (@intFromEnum(range.min) >= @intFromEnum(min_ver)) return true;
                if (@intFromEnum(range.max) < @intFromEnum(min_ver)) return false;
                return null;
            }
        };

        pub fn parse(str: []const u8) !WindowsVersion {
            return std.meta.stringToEnum(WindowsVersion, str) orelse
                @enumFromInt(std.fmt.parseInt(u32, str, 0) catch
                    return error.InvalidOperatingSystemVersion);
        }

        /// This function is defined to serialize a Zig source code representation of this
        /// type, that, when parsed, will deserialize into the same data.
        pub fn format(wv: WindowsVersion, w: *std.Io.Writer) std.Io.Writer.Error!void {
            if (std.enums.tagName(WindowsVersion, wv)) |name| {
                var vecs: [2][]const u8 = .{ ".", name };
                return w.writeVecAll(&vecs);
            } else {
                return w.print("@enumFromInt(0x{X:0>8})", .{wv});
            }
        }
    };

    pub const HurdVersionRange = struct {
        range: std.SemanticVersion.Range,
        glibc: std.SemanticVersion,

        pub inline fn includesVersion(range: HurdVersionRange, ver: std.SemanticVersion) bool {
            return range.range.includesVersion(ver);
        }

        /// Checks if system is guaranteed to be at least `version` or older than `version`.
        /// Returns `null` if a runtime check is required.
        pub inline fn isAtLeast(range: HurdVersionRange, ver: std.SemanticVersion) ?bool {
            return range.range.isAtLeast(ver);
        }
    };

    pub const LinuxVersionRange = struct {
        range: std.SemanticVersion.Range,
        glibc: std.SemanticVersion,
        /// Android API level.
        android: u32,

        pub inline fn includesVersion(range: LinuxVersionRange, ver: std.SemanticVersion) bool {
            return range.range.includesVersion(ver);
        }

        /// Checks if system is guaranteed to be at least `version` or older than `version`.
        /// Returns `null` if a runtime check is required.
        pub inline fn isAtLeast(range: LinuxVersionRange, ver: std.SemanticVersion) ?bool {
            return range.range.isAtLeast(ver);
        }
    };

    /// The version ranges here represent the minimum OS version to be supported
    /// and the maximum OS version to be supported. The default values represent
    /// the range that the Zig Standard Library bases its abstractions on.
    ///
    /// The minimum version of the range is the main setting to tweak for a target.
    /// Usually, the maximum target OS version will remain the default, which is
    /// the latest released version of the OS.
    ///
    /// To test at compile time if the target is guaranteed to support a given OS feature,
    /// one should check that the minimum version of the range is greater than or equal to
    /// the version the feature was introduced in.
    ///
    /// To test at compile time if the target certainly will not support a given OS feature,
    /// one should check that the maximum version of the range is less than the version the
    /// feature was introduced in.
    ///
    /// If neither of these cases apply, a runtime check should be used to determine if the
    /// target supports a given OS feature.
    ///
    /// Binaries built with a given maximum version will continue to function on newer
    /// operating system versions. However, such a binary may not take full advantage of the
    /// newer operating system APIs.
    ///
    /// See `Os.isAtLeast`.
    pub const VersionRange = union {
        none: void,
        semver: std.SemanticVersion.Range,
        hurd: HurdVersionRange,
        linux: LinuxVersionRange,
        windows: WindowsVersion.Range,

        /// The default `VersionRange` represents the range that the Zig Standard Library
        /// bases its abstractions on.
        pub fn default(arch: Cpu.Arch, tag: Tag, abi: Abi) VersionRange {
            return switch (tag) {
                .freestanding,
                .other,

                .managarm,

                .haiku,
                .illumos,
                .plan9,
                .serenity,

                .ps3,
                .ps4,
                .ps5,

                .emscripten,

                .mesa3d,
                => .{ .none = {} },

                .contiki => .{
                    .semver = .{
                        .min = .{ .major = 4, .minor = 0, .patch = 0 },
                        .max = .{ .major = 5, .minor = 1, .patch = 0 },
                    },
                },
                .fuchsia => .{
                    .semver = .{
                        .min = .{ .major = 1, .minor = 0, .patch = 0 },
                        .max = .{ .major = 28, .minor = 0, .patch = 0 },
                    },
                },
                .hermit => .{
                    .semver = .{
                        .min = .{ .major = 0, .minor = 5, .patch = 0 },
                        .max = .{ .major = 0, .minor = 11, .patch = 0 },
                    },
                },

                .hurd => .{
                    .hurd = .{
                        .range = .{
                            .min = .{ .major = 0, .minor = 9, .patch = 0 },
                            .max = .{ .major = 0, .minor = 9, .patch = 0 },
                        },
                        .glibc = .{ .major = 2, .minor = 28, .patch = 0 },
                    },
                },
                .linux => .{
                    .linux = .{
                        .range = .{
                            .min = blk: {
                                const default_min: std.SemanticVersion = .{ .major = 5, .minor = 10, .patch = 0 };

                                for (std.zig.target.available_libcs) |libc| {
                                    if (libc.arch != arch or libc.os != tag or libc.abi != abi) continue;

                                    if (libc.os_ver) |min| {
                                        if (min.order(default_min) == .gt) break :blk min;
                                    }
                                }

                                break :blk default_min;
                            },
                            .max = .{ .major = 6, .minor = 19, .patch = 0 },
                        },
                        .glibc = blk: {
                            // For 32-bit targets that traditionally used 32-bit time, we require
                            // glibc 2.34 for full 64-bit time support. For everything else, we only
                            // require glibc 2.31.
                            const default_min: std.SemanticVersion = switch (arch) {
                                .arm,
                                .armeb,
                                .csky,
                                .m68k,
                                .mips,
                                .mipsel,
                                .powerpc,
                                .sparc,
                                .x86,
                                => .{ .major = 2, .minor = 34, .patch = 0 },
                                .mips64,
                                .mips64el,
                                => if (abi == .gnuabin32)
                                    .{ .major = 2, .minor = 34, .patch = 0 }
                                else
                                    .{ .major = 2, .minor = 31, .patch = 0 },
                                else => .{ .major = 2, .minor = 31, .patch = 0 },
                            };

                            for (std.zig.target.available_libcs) |libc| {
                                if (libc.os != tag or libc.arch != arch or libc.abi != abi) continue;

                                if (libc.glibc_min) |min| {
                                    if (min.order(default_min) == .gt) break :blk min;
                                }
                            }

                            break :blk default_min;
                        },
                        .android = 29,
                    },
                },
                .rtems => .{
                    .semver = .{
                        .min = .{ .major = 5, .minor = 1, .patch = 0 },
                        .max = .{ .major = 6, .minor = 1, .patch = 0 },
                    },
                },

                .dragonfly => .{
                    .semver = .{
                        .min = .{ .major = 6, .minor = 0, .patch = 0 },
                        .max = .{ .major = 6, .minor = 4, .patch = 2 },
                    },
                },
                .freebsd => .{
                    .semver = .{
                        .min = blk: {
                            const default_min: std.SemanticVersion = .{ .major = 14, .minor = 0, .patch = 0 };

                            for (std.zig.target.available_libcs) |libc| {
                                if (libc.arch != arch or libc.os != tag or libc.abi != abi) continue;

                                if (libc.os_ver) |min| {
                                    if (min.order(default_min) == .gt) break :blk min;
                                }
                            }

                            break :blk default_min;
                        },
                        .max = .{ .major = 15, .minor = 0, .patch = 0 },
                    },
                },
                .netbsd => .{
                    .semver = .{
                        .min = blk: {
                            const default_min: std.SemanticVersion = .{ .major = 10, .minor = 1, .patch = 0 };

                            for (std.zig.target.available_libcs) |libc| {
                                if (libc.arch != arch or libc.os != tag or libc.abi != abi) continue;

                                if (libc.os_ver) |min| {
                                    if (min.order(default_min) == .gt) break :blk min;
                                }
                            }

                            break :blk default_min;
                        },
                        .max = .{ .major = 10, .minor = 1, .patch = 0 },
                    },
                },
                .openbsd => .{
                    .semver = .{
                        .min = blk: {
                            const default_min: std.SemanticVersion = .{ .major = 7, .minor = 8, .patch = 0 };

                            for (std.zig.target.available_libcs) |libc| {
                                if (libc.arch != arch or libc.os != tag or libc.abi != abi) continue;

                                if (libc.os_ver) |min| {
                                    if (min.order(default_min) == .gt) break :blk min;
                                }
                            }

                            break :blk default_min;
                        },
                        .max = .{ .major = 7, .minor = 8, .patch = 0 },
                    },
                },

                .driverkit => .{
                    .semver = .{
                        .min = .{ .major = 20, .minor = 0, .patch = 0 },
                        .max = .{ .major = 25, .minor = 0, .patch = 0 },
                    },
                },
                .macos => .{
                    .semver = .{
                        .min = .{ .major = 13, .minor = 0, .patch = 0 },
                        .max = .{ .major = 15, .minor = 6, .patch = 0 },
                    },
                },
                .ios, .maccatalyst => .{
                    .semver = .{
                        .min = .{ .major = 15, .minor = 0, .patch = 0 },
                        .max = .{ .major = 18, .minor = 6, .patch = 0 },
                    },
                },
                .tvos => .{
                    .semver = .{
                        .min = .{ .major = 15, .minor = 0, .patch = 0 },
                        .max = .{ .major = 18, .minor = 5, .patch = 0 },
                    },
                },
                .visionos => .{
                    .semver = .{
                        .min = .{ .major = 1, .minor = 0, .patch = 0 },
                        .max = .{ .major = 2, .minor = 5, .patch = 0 },
                    },
                },
                .watchos => .{
                    .semver = .{
                        .min = .{ .major = 8, .minor = 0, .patch = 0 },
                        .max = .{ .major = 11, .minor = 6, .patch = 0 },
                    },
                },

                .windows => .{
                    .windows = .{
                        .min = .win10,
                        .max = WindowsVersion.latest,
                    },
                },
                .uefi => .{
                    .semver = .{
                        .min = .{ .major = 2, .minor = 0, .patch = 0 },
                        .max = .{ .major = 2, .minor = 11, .patch = 0 },
                    },
                },

                .@"3ds" => .{
                    .semver = .{
                        // These signify release versions (https://www.3dbrew.org/wiki/NCCH/Extended_Header#ARM11_Kernel_Capabilities)
                        // which are different from user-facing system versions (https://www.3dbrew.org/wiki/Home_Menu#System_Versions_List).
                        //
                        // Multiple system versions could refer to the same release version.
                        // The comment indicates the system version that release version was introduced (for minimum) and the latest (for maximum).
                        .min = .{ .major = 2, .minor = 27, .patch = 0 }, // 1.0.0-0
                        .max = .{ .major = 2, .minor = 58, .patch = 0 }, // 11.17.0-50
                    },
                },

                .psp => .{
                    .semver = .{
                        // https://www.psdevwiki.com/psp/Official_Firmware_(OFW)#1.XX_Kernel
                        // It appears that the kernel started with semver for 1.XX before later changing to MAJ.MINPATCH in later releases (e.g. 3.60, 6.61)
                        .min = .{ .major = 1, .minor = 0, .patch = 3 },
                        .max = .{ .major = 6, .minor = 61, .patch = 0 },
                    },
                },

                .vita => .{
                    .semver = .{
                        // 1.3 is the first public release
                        .min = .{ .major = 1, .minor = 3, .patch = 0 },
                        .max = .{ .major = 3, .minor = 60, .patch = 0 },
                    },
                },

                .wasi => .{
                    .semver = .{
                        .min = .{ .major = 0, .minor = 1, .patch = 0 },
                        .max = .{ .major = 0, .minor = 3, .patch = 0 },
                    },
                },

                .amdhsa => .{
                    .semver = .{
                        .min = .{ .major = 5, .minor = 0, .patch = 0 },
                        .max = .{ .major = 7, .minor = 1, .patch = 0 },
                    },
                },
                .amdpal => .{
                    .semver = .{
                        .min = .{ .major = 1, .minor = 1, .patch = 0 },
                        .max = .{ .major = 3, .minor = 5, .patch = 0 },
                    },
                },
                .cuda => .{
                    .semver = .{
                        .min = .{ .major = 11, .minor = 0, .patch = 1 },
                        .max = .{ .major = 13, .minor = 0, .patch = 2 },
                    },
                },
                .nvcl,
                .opencl,
                => .{
                    .semver = .{
                        .min = .{ .major = 2, .minor = 2, .patch = 0 },
                        .max = .{ .major = 3, .minor = 0, .patch = 19 },
                    },
                },
                .opengl => .{
                    .semver = .{
                        .min = .{ .major = 4, .minor = 5, .patch = 0 },
                        .max = .{ .major = 4, .minor = 6, .patch = 0 },
                    },
                },
                .vulkan => .{
                    .semver = .{
                        .min = .{ .major = 1, .minor = 2, .patch = 0 },
                        .max = .{ .major = 1, .minor = 4, .patch = 331 },
                    },
                },
            };
        }
    };

    pub const TaggedVersionRange = union(enum) {
        none: void,
        semver: std.SemanticVersion.Range,
        hurd: HurdVersionRange,
        linux: LinuxVersionRange,
        windows: WindowsVersion.Range,

        pub fn gnuLibCVersion(range: TaggedVersionRange) ?std.SemanticVersion {
            return switch (range) {
                .none, .semver, .windows => null,
                .hurd => |h| h.glibc,
                .linux => |l| l.glibc,
            };
        }
    };

    /// Provides a tagged union. `Target` does not store the tag because it is
    /// redundant with the OS tag; this function abstracts that part away.
    pub inline fn versionRange(os: Os) TaggedVersionRange {
        return switch (os.tag.versionRangeTag()) {
            .none => .{ .none = {} },
            .semver => .{ .semver = os.version_range.semver },
            .hurd => .{ .hurd = os.version_range.hurd },
            .linux => .{ .linux = os.version_range.linux },
            .windows => .{ .windows = os.version_range.windows },
        };
    }

    /// Checks if system is guaranteed to be at least `version` or older than `version`.
    /// Returns `null` if a runtime check is required.
    pub inline fn isAtLeast(os: Os, comptime tag: Tag, ver: switch (tag.versionRangeTag()) {
        .none => void,
        .semver, .hurd, .linux => std.SemanticVersion,
        .windows => WindowsVersion,
    }) ?bool {
        return if (os.tag != tag) false else switch (tag.versionRangeTag()) {
            .none => true,
            inline .semver,
            .hurd,
            .linux,
            .windows,
            => |field| @field(os.version_range, @tagName(field)).isAtLeast(ver),
        };
    }
};

pub const aarch64 = @import("Target/aarch64.zig");
pub const alpha = @import("Target/alpha.zig");
pub const amdgcn = @import("Target/amdgcn.zig");
pub const arc = @import("Target/arc.zig");
pub const arm = @import("Target/arm.zig");
pub const avr = @import("Target/avr.zig");
pub const bpf = @import("Target/bpf.zig");
pub const csky = @import("Target/csky.zig");
pub const hexagon = @import("Target/hexagon.zig");
pub const hppa = @import("Target/hppa.zig");
pub const kalimba = @import("Target/generic.zig");
pub const kvx = @import("Target/kvx.zig");
pub const lanai = @import("Target/lanai.zig");
pub const loongarch = @import("Target/loongarch.zig");
pub const m68k = @import("Target/m68k.zig");
pub const microblaze = @import("Target/generic.zig");
pub const mips = @import("Target/mips.zig");
pub const msp430 = @import("Target/msp430.zig");
pub co
```
