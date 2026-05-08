```
xpectEqualStrings("//a/b/", it.root().?);
        // Malformed UNC path with empty server name
        it = ComponentIterator(.windows, u8).init("\\\\\\a\\b\\c");
        try std.testing.expectEqualStrings("\\\\\\a\\", it.root().?);
    }
}

/// Format a path encoded as bytes for display as UTF-8.
/// Returns a Formatter for the given path. The path will be converted to valid UTF-8
/// during formatting. This is a lossy conversion if the path contains any ill-formed UTF-8.
/// Ill-formed UTF-8 byte sequences are replaced by the replacement character (U+FFFD)
/// according to "U+FFFD Substitution of Maximal Subparts" from Chapter 3 of
/// the Unicode standard, and as specified by https://encoding.spec.whatwg.org/#utf-8-decoder
pub const fmtAsUtf8Lossy = std.unicode.fmtUtf8;

/// Format a path encoded as WTF-16 LE for display as UTF-8.
/// Return a Formatter for a (potentially ill-formed) UTF-16 LE path.
/// The path will be converted to valid UTF-8 during formatting. This is
/// a lossy conversion if the path contains any unpaired surrogates.
/// Unpaired surrogates are replaced by the replacement character (U+FFFD).
pub const fmtWtf16LeAsUtf8Lossy = std.unicode.fmtUtf16Le;

/// Similar to `RTL_PATH_TYPE`, but without the `UNKNOWN` path type.
pub const Win32PathType = enum {
    /// `\\server\share\foo`
    unc_absolute,
    /// `C:\foo`
    drive_absolute,
    /// `C:foo`
    drive_relative,
    /// `\foo`
    rooted,
    /// `foo`
    relative,
    /// `\\.\foo`, `\\?\foo`
    local_device,
    /// `\\.`, `\\?`
    root_local_device,
};

/// Get the path type of a Win32 namespace path.
/// Similar to `RtlDetermineDosPathNameType_U`.
/// If `T` is `u16`, then `path` should be encoded as WTF-16LE.
pub fn getWin32PathType(comptime T: type, path: []const T) Win32PathType {
    if (path.len < 1) return .relative;

    const windows_path = std.fs.path.PathType.windows;
    if (windows_path.isSep(T, path[0])) {
        // \x
        if (path.len < 2 or !windows_path.isSep(T, path[1])) return .rooted;
        // \\. or \\?
        if (path.len > 2 and (path[2] == mem.nativeToLittle(T, '.') or path[2] == mem.nativeToLittle(T, '?'))) {
            // exactly \\. or \\? with nothing trailing
            if (path.len == 3) return .root_local_device;
            // \\.\x or \\?\x
            if (windows_path.isSep(T, path[3])) return .local_device;
        }
        // \\x
        return .unc_absolute;
    } else {
        // Some choice has to be made about how non-ASCII code points as drive-letters are handled, since
        // path[0] is a different size for WTF-16 vs WTF-8, leading to a potential mismatch in classification
        // for a WTF-8 path and its WTF-16 equivalent. For example, `€:\` encoded in WTF-16 is three code
        // units `<0x20AC>:\` whereas `€:\` encoded as WTF-8 is 6 code units `<0xE2><0x82><0xAC>:\` so
        // checking path[0], path[1] and path[2] would not behave the same between WTF-8/WTF-16.
        //
        // `RtlDetermineDosPathNameType_U` exclusively deals with WTF-16 and considers
        // `€:\` a drive-absolute path, but code points that take two WTF-16 code units to encode get
        // classified as a relative path (e.g. with U+20000 as the drive-letter that'd be encoded
        // in WTF-16 as `<0xD840><0xDC00>:\` and be considered a relative path).
        //
        // The choice made here is to emulate the behavior of `RtlDetermineDosPathNameType_U` for both
        // WTF-16 and WTF-8. This is because, while unlikely and not supported by the Disk Manager GUI,
        // drive letters are not actually restricted to A-Z. Using `SetVolumeMountPointW` will allow you
        // to set any byte value as a drive letter, and going through `IOCTL_MOUNTMGR_CREATE_POINT` will
        // allow you to set any WTF-16 code unit as a drive letter.
        //
        // Non-A-Z drive letters don't interact well with most of Windows, but certain things do work, e.g.
        // `cd /D €:\` will work, filesystem functions still work, etc.
        //
        // The unfortunate part of this is that this makes handling WTF-8 more complicated as we can't
        // just check path[0], path[1], path[2].
        const colon_i: usize = switch (T) {
            u8 => i: {
                const code_point_len = std.unicode.utf8ByteSequenceLength(path[0]) catch return .relative;
                // Conveniently, 4-byte sequences in WTF-8 have the same starting code point
                // as 2-code-unit sequences in WTF-16.
                if (code_point_len > 3) return .relative;
                break :i code_point_len;
            },
            u16 => 1,
            else => @compileError("unsupported type: " ++ @typeName(T)),
        };
        // x
        if (path.len < colon_i + 1 or path[colon_i] != mem.nativeToLittle(T, ':')) return .relative;
        // x:\
        if (path.len > colon_i + 1 and windows_path.isSep(T, path[colon_i + 1])) return .drive_absolute;
        // x:
        return .drive_relative;
    }
}

test getWin32PathType {
    try std.testing.expectEqual(.relative, getWin32PathType(u8, ""));
    try std.testing.expectEqual(.relative, getWin32PathType(u8, "x"));
    try std.testing.expectEqual(.relative, getWin32PathType(u8, "x\\"));

    try std.testing.expectEqual(.root_local_device, getWin32PathType(u8, "//."));
    try std.testing.expectEqual(.root_local_device, getWin32PathType(u8, "/\\?"));
    try std.testing.expectEqual(.root_local_device, getWin32PathType(u8, "\\\\?"));

    try std.testing.expectEqual(.local_device, getWin32PathType(u8, "//./x"));
    try std.testing.expectEqual(.local_device, getWin32PathType(u8, "/\\?\\x"));
    try std.testing.expectEqual(.local_device, getWin32PathType(u8, "\\\\?\\x"));
    // local device paths require a path separator after the root, otherwise it is considered a UNC path
    try std.testing.expectEqual(.unc_absolute, getWin32PathType(u8, "\\\\?x"));
    try std.testing.expectEqual(.unc_absolute, getWin32PathType(u8, "//.x"));

    try std.testing.expectEqual(.unc_absolute, getWin32PathType(u8, "//"));
    try std.testing.expectEqual(.unc_absolute, getWin32PathType(u8, "\\\\x"));
    try std.testing.expectEqual(.unc_absolute, getWin32PathType(u8, "//x"));

    try std.testing.expectEqual(.rooted, getWin32PathType(u8, "\\x"));
    try std.testing.expectEqual(.rooted, getWin32PathType(u8, "/"));

    try std.testing.expectEqual(.drive_relative, getWin32PathType(u8, "x:"));
    try std.testing.expectEqual(.drive_relative, getWin32PathType(u8, "x:abc"));
    try std.testing.expectEqual(.drive_relative, getWin32PathType(u8, "x:a/b/c"));

    try std.testing.expectEqual(.drive_absolute, getWin32PathType(u8, "x:\\"));
    try std.testing.expectEqual(.drive_absolute, getWin32PathType(u8, "x:\\abc"));
    try std.testing.expectEqual(.drive_absolute, getWin32PathType(u8, "x:/a/b/c"));

    // Non-ASCII code point that is encoded as one WTF-16 code unit is considered a valid drive letter
    try std.testing.expectEqual(.drive_absolute, getWin32PathType(u8, "€:\\"));
    try std.testing.expectEqual(.drive_absolute, getWin32PathType(u16, std.unicode.wtf8ToWtf16LeStringLiteral("€:\\")));
    try std.testing.expectEqual(.drive_relative, getWin32PathType(u8, "€:"));
    try std.testing.expectEqual(.drive_relative, getWin32PathType(u16, std.unicode.wtf8ToWtf16LeStringLiteral("€:")));
    // But code points that are encoded as two WTF-16 code units are not
    try std.testing.expectEqual(.relative, getWin32PathType(u8, "\u{10000}:\\"));
    try std.testing.expectEqual(.relative, getWin32PathType(u16, std.unicode.wtf8ToWtf16LeStringLiteral("\u{10000}:\\")));
}



---
File: /std/fs/test.zig
---




---
File: /std/hash/crc/impl.zig
---

// There is a generic CRC implementation "Crc()" which can be parameterized via
// the Algorithm struct for a plethora of uses.
//
// The primary interface for all of the standard CRC algorithms is the
// generated file "crc.zig", which uses the implementation code here to define
// many standard CRCs.

const std = @import("std");

pub fn Algorithm(comptime W: type) type {
    return struct {
        polynomial: W,
        initial: W,
        reflect_input: bool,
        reflect_output: bool,
        xor_output: W,
    };
}

pub fn Crc(comptime W: type, comptime algorithm: Algorithm(W)) type {
    return struct {
        const Self = @This();
        const I = if (@bitSizeOf(W) < 8) u8 else W;
        const lookup_table = blk: {
            @setEvalBranchQuota(2500);

            const poly = if (algorithm.reflect_input)
                @bitReverse(@as(I, algorithm.polynomial)) >> (@bitSizeOf(I) - @bitSizeOf(W))
            else
                @as(I, algorithm.polynomial) << (@bitSizeOf(I) - @bitSizeOf(W));

            var table: [256]I = undefined;
            for (&table, 0..) |*e, i| {
                var crc: I = i;
                if (algorithm.reflect_input) {
                    var j: usize = 0;
                    while (j < 8) : (j += 1) {
                        crc = (crc >> 1) ^ ((crc & 1) * poly);
                    }
                } else {
                    crc <<= @bitSizeOf(I) - 8;
                    var j: usize = 0;
                    while (j < 8) : (j += 1) {
                        crc = (crc << 1) ^ (((crc >> (@bitSizeOf(I) - 1)) & 1) * poly);
                    }
                }
                e.* = crc;
            }
            break :blk table;
        };

        crc: I,

        pub fn init() Self {
            const initial = if (algorithm.reflect_input)
                @bitReverse(@as(I, algorithm.initial)) >> (@bitSizeOf(I) - @bitSizeOf(W))
            else
                @as(I, algorithm.initial) << (@bitSizeOf(I) - @bitSizeOf(W));
            return Self{ .crc = initial };
        }

        inline fn tableEntry(index: I) I {
            return lookup_table[@as(u8, @intCast(index & 0xFF))];
        }

        pub fn update(self: *Self, bytes: []const u8) void {
            var i: usize = 0;
            if (@bitSizeOf(I) <= 8) {
                while (i < bytes.len) : (i += 1) {
                    self.crc = tableEntry(self.crc ^ bytes[i]);
                }
            } else if (algorithm.reflect_input) {
                while (i < bytes.len) : (i += 1) {
                    const table_index = self.crc ^ bytes[i];
                    self.crc = tableEntry(table_index) ^ (self.crc >> 8);
                }
            } else {
                while (i < bytes.len) : (i += 1) {
                    const table_index = (self.crc >> (@bitSizeOf(I) - 8)) ^ bytes[i];
                    self.crc = tableEntry(table_index) ^ (self.crc << 8);
                }
            }
        }

        pub fn final(self: Self) W {
            var c = self.crc;
            if (algorithm.reflect_input != algorithm.reflect_output) {
                c = @bitReverse(c);
            }
            if (!algorithm.reflect_output) {
                c >>= @bitSizeOf(I) - @bitSizeOf(W);
            }
            return @as(W, @intCast(c ^ algorithm.xor_output));
        }

        pub fn hash(bytes: []const u8) W {
            var c = Self.init();
            c.update(bytes);
            return c.final();
        }
    };
}



---
File: /std/hash/crc/test.zig
---




---
File: /std/hash/Adler32.zig
---

//! https://tools.ietf.org/html/rfc1950#section-9
//! https://github.com/madler/zlib/blob/master/adler32.c

const Adler32 = @This();
const std = @import("std");
const testing = std.testing;

adler: u32 = 1,

pub fn permute(state: u32, input: []const u8) u32 {
    const base = 65521;
    const nmax = 5552;

    var s1 = state & 0xffff;
    var s2 = (state >> 16) & 0xffff;

    if (input.len == 1) {
        s1 +%= input[0];
        if (s1 >= base) {
            s1 -= base;
        }
        s2 +%= s1;
        if (s2 >= base) {
            s2 -= base;
        }
    } else if (input.len < 16) {
        for (input) |b| {
            s1 +%= b;
            s2 +%= s1;
        }
        if (s1 >= base) {
            s1 -= base;
        }

        s2 %= base;
    } else {
        const n = nmax / 16; // note: 16 | nmax

        var i: usize = 0;

        while (i + nmax <= input.len) {
            var rounds: usize = 0;
            while (rounds < n) : (rounds += 1) {
                comptime var j: usize = 0;
                inline while (j < 16) : (j += 1) {
                    s1 +%= input[i + j];
                    s2 +%= s1;
                }
                i += 16;
            }

            s1 %= base;
            s2 %= base;
        }

        if (i < input.len) {
            while (i + 16 <= input.len) : (i += 16) {
                comptime var j: usize = 0;
                inline while (j < 16) : (j += 1) {
                    s1 +%= input[i + j];
                    s2 +%= s1;
                }
            }
            while (i < input.len) : (i += 1) {
                s1 +%= input[i];
                s2 +%= s1;
            }

            s1 %= base;
            s2 %= base;
        }
    }

    return s1 | (s2 << 16);
}

pub fn update(a: *Adler32, input: []const u8) void {
    a.adler = permute(a.adler, input);
}

pub fn hash(input: []const u8) u32 {
    return permute(1, input);
}

test "sanity" {
    try testing.expectEqual(@as(u32, 0x620062), hash("a"));
    try testing.expectEqual(@as(u32, 0xbc002ed), hash("example"));
}

test "long" {
    const long1 = [_]u8{1} ** 1024;
    try testing.expectEqual(@as(u32, 0x06780401), hash(long1[0..]));

    const long2 = [_]u8{1} ** 1025;
    try testing.expectEqual(@as(u32, 0x0a7a0402), hash(long2[0..]));
}

test "very long" {
    const long = [_]u8{1} ** 5553;
    try testing.expectEqual(@as(u32, 0x707f15b2), hash(long[0..]));
}

test "very long with variation" {
    const long = comptime blk: {
        @setEvalBranchQuota(7000);
        var result: [6000]u8 = undefined;

        var i: usize = 0;
        while (i < result.len) : (i += 1) {
            result[i] = @as(u8, @truncate(i));
        }

        break :blk result;
    };

    try testing.expectEqual(@as(u32, 0x5af38d6e), hash(long[0..]));
}



---
File: /std/hash/auto_hash.zig
---

const std = @import("std");
const assert = std.debug.assert;
const mem = std.mem;

/// Describes how pointer types should be hashed.
pub const HashStrategy = enum {
    /// Do not follow pointers, only hash their value.
    Shallow,

    /// Follow pointers, hash the pointee content.
    /// Only dereferences one level, ie. it is changed into .Shallow when a
    /// pointer type is encountered.
    Deep,

    /// Follow pointers, hash the pointee content.
    /// Dereferences all pointers encountered.
    /// Assumes no cycle.
    DeepRecursive,
};

/// Helper function to hash a pointer and mutate the strategy if needed.
pub fn hashPointer(hasher: anytype, key: anytype, comptime strat: HashStrategy) void {
    const info = @typeInfo(@TypeOf(key));

    switch (info.pointer.size) {
        .one => switch (strat) {
            .Shallow => hash(hasher, @intFromPtr(key), .Shallow),
            .Deep => hash(hasher, key.*, .Shallow),
            .DeepRecursive => hash(hasher, key.*, .DeepRecursive),
        },

        .slice => {
            switch (strat) {
                .Shallow => {
                    hashPointer(hasher, key.ptr, .Shallow);
                },
                .Deep => hashArray(hasher, key, .Shallow),
                .DeepRecursive => hashArray(hasher, key, .DeepRecursive),
            }
            hash(hasher, key.len, .Shallow);
        },

        .many,
        .c,
        => switch (strat) {
            .Shallow => hash(hasher, @intFromPtr(key), .Shallow),
            else => @compileError(
                \\ unknown-length pointers and C pointers cannot be hashed deeply.
                \\ Consider providing your own hash function.
            ),
        },
    }
}

/// Helper function to hash a set of contiguous objects, from an array or slice.
pub fn hashArray(hasher: anytype, key: anytype, comptime strat: HashStrategy) void {
    for (key) |element| {
        hash(hasher, element, strat);
    }
}

/// Provides generic hashing for any eligible type.
/// Strategy is provided to determine if pointers should be followed or not.
pub fn hash(hasher: anytype, key: anytype, comptime strat: HashStrategy) void {
    const Key = @TypeOf(key);
    const Hasher = switch (@typeInfo(@TypeOf(hasher))) {
        .pointer => |ptr| ptr.child,
        else => @TypeOf(hasher),
    };

    if (strat == .Shallow and std.meta.hasUniqueRepresentation(Key)) {
        @call(.always_inline, Hasher.update, .{ hasher, mem.asBytes(&key) });
        return;
    }

    switch (@typeInfo(Key)) {
        .noreturn,
        .@"opaque",
        .undefined,
        .null,
        .comptime_float,
        .comptime_int,
        .type,
        .enum_literal,
        .frame,
        .float,
        => @compileError("unable to hash type " ++ @typeName(Key)),

        .void => return,

        // Help the optimizer see that hashing an int is easy by inlining!
        // TODO Check if the situation is better after #561 is resolved.
        .int => |int| switch (int.signedness) {
            .signed => hash(hasher, @as(@Int(.unsigned, int.bits), @bitCast(key)), strat),
            .unsigned => {
                if (std.meta.hasUniqueRepresentation(Key)) {
                    @call(.always_inline, Hasher.update, .{ hasher, std.mem.asBytes(&key) });
                } else {
                    // Take only the part containing the key value, the remaining
                    // bytes are undefined and must not be hashed!
                    const byte_size = comptime std.math.divCeil(comptime_int, @bitSizeOf(Key), 8) catch unreachable;
                    @call(.always_inline, Hasher.update, .{ hasher, std.mem.asBytes(&key)[0..byte_size] });
                }
            },
        },

        .bool => hash(hasher, @intFromBool(key), strat),
        .@"enum" => hash(hasher, @intFromEnum(key), strat),
        .error_set => hash(hasher, @intFromError(key), strat),
        .@"anyframe", .@"fn" => hash(hasher, @intFromPtr(key), strat),

        .pointer => @call(.always_inline, hashPointer, .{ hasher, key, strat }),

        .optional => if (key) |k| hash(hasher, k, strat),

        .array => hashArray(hasher, key, strat),

        .vector => |info| {
            if (std.meta.hasUniqueRepresentation(Key)) {
                hasher.update(mem.asBytes(&key));
            } else {
                comptime var i = 0;
                inline while (i < info.len) : (i += 1) {
                    hash(hasher, key[i], strat);
                }
            }
        },

        .@"struct" => |info| {
            inline for (info.fields) |field| {
                // We reuse the hash of the previous field as the seed for the
                // next one so that they're dependant.
                hash(hasher, @field(key, field.name), strat);
            }
        },

        .@"union" => |info| blk: {
            if (info.tag_type) |tag_type| {
                const tag = std.meta.activeTag(key);
                hash(hasher, tag, strat);
                inline for (info.fields) |field| {
                    if (@field(tag_type, field.name) == tag) {
                        if (field.type != void) {
                            hash(hasher, @field(key, field.name), strat);
                        }
                        break :blk;
                    }
                }
                unreachable;
            } else @compileError("cannot hash untagged union type: " ++ @typeName(Key) ++ ", provide your own hash function");
        },

        .error_union => blk: {
            const payload = key catch |err| {
                hash(hasher, err, strat);
                break :blk;
            };
            hash(hasher, payload, strat);
        },
    }
}

inline fn typeContainsSlice(comptime K: type) bool {
    return switch (@typeInfo(K)) {
        .pointer => |info| info.size == .slice,

        inline .@"struct", .@"union" => |info| {
            inline for (info.fields) |field| {
                if (typeContainsSlice(field.type)) {
                    return true;
                }
            }
            return false;
        },

        else => false,
    };
}

/// Provides generic hashing for any eligible type.
/// Only hashes `key` itself, pointers are not followed.
/// Slices as well as unions and structs containing slices are rejected to avoid
/// ambiguity on the user's intention.
pub fn autoHash(hasher: anytype, key: anytype) void {
    const Key = @TypeOf(key);
    if (comptime typeContainsSlice(Key)) {
        @compileError("std.hash.autoHash does not allow slices as well as unions and structs containing slices here (" ++ @typeName(Key) ++
            ") because the intent is unclear. Consider using std.hash.autoHashStrat or providing your own hash function instead.");
    }

    hash(hasher, key, .Shallow);
}

const testing = std.testing;
const Wyhash = std.hash.Wyhash;

fn testHash(key: anytype) u64 {
    // Any hash could be used here, for testing autoHash.
    var hasher = Wyhash.init(0);
    hash(&hasher, key, .Shallow);
    return hasher.final();
}

fn testHashShallow(key: anytype) u64 {
    // Any hash could be used here, for testing autoHash.
    var hasher = Wyhash.init(0);
    hash(&hasher, key, .Shallow);
    return hasher.final();
}

fn testHashDeep(key: anytype) u64 {
    // Any hash could be used here, for testing autoHash.
    var hasher = Wyhash.init(0);
    hash(&hasher, key, .Deep);
    return hasher.final();
}

fn testHashDeepRecursive(key: anytype) u64 {
    // Any hash could be used here, for testing autoHash.
    var hasher = Wyhash.init(0);
    hash(&hasher, key, .DeepRecursive);
    return hasher.final();
}

test "typeContainsSlice" {
    comptime {
        try testing.expect(!typeContainsSlice(std.meta.Tag(std.builtin.Type)));

        try testing.expect(typeContainsSlice([]const u8));
        try testing.expect(!typeContainsSlice(u8));
        const A = struct { x: []const u8 };
        const B = struct { a: A };
        const C = struct { b: B };
        const D = struct { x: u8 };
        try testing.expect(typeContainsSlice(A));
        try testing.expect(typeContainsSlice(B));
        try testing.expect(typeContainsSlice(C));
        try testing.expect(!typeContainsSlice(D));
    }
}

test "hash pointer" {
    const array = [_]u32{ 123, 123, 123 };
    const a = &array[0];
    const b = &array[1];
    const c = &array[2];
    const d = a;

    try testing.expect(testHashShallow(a) == testHashShallow(d));
    try testing.expect(testHashShallow(a) != testHashShallow(c));
    try testing.expect(testHashShallow(a) != testHashShallow(b));

    try testing.expect(testHashDeep(a) == testHashDeep(a));
    try testing.expect(testHashDeep(a) == testHashDeep(c));
    try testing.expect(testHashDeep(a) == testHashDeep(b));

    try testing.expect(testHashDeepRecursive(a) == testHashDeepRecursive(a));
    try testing.expect(testHashDeepRecursive(a) == testHashDeepRecursive(c));
    try testing.expect(testHashDeepRecursive(a) == testHashDeepRecursive(b));
}

test "hash slice shallow" {
    // Allocate one array dynamically so that we're assured it is not merged
    // with the other by the optimization passes.
    const array1 = try std.testing.allocator.create([6]u32);
    defer std.testing.allocator.destroy(array1);
    array1.* = [_]u32{ 1, 2, 3, 4, 5, 6 };
    const array2 = [_]u32{ 1, 2, 3, 4, 5, 6 };
    // TODO audit deep/shallow - maybe it has the wrong behavior with respect to array pointers and slices
    var runtime_zero: usize = 0;
    _ = &runtime_zero;
    const a = array1[runtime_zero..];
    const b = array2[runtime_zero..];
    const c = array1[runtime_zero..3];
    try testing.expect(testHashShallow(a) == testHashShallow(a));
    try testing.expect(testHashShallow(a) != testHashShallow(array1));
    try testing.expect(testHashShallow(a) != testHashShallow(b));
    try testing.expect(testHashShallow(a) != testHashShallow(c));
}

test "hash slice deep" {
    // Allocate one array dynamically so that we're assured it is not merged
    // with the other by the optimization passes.
    const array1 = try std.testing.allocator.create([6]u32);
    defer std.testing.allocator.destroy(array1);
    array1.* = [_]u32{ 1, 2, 3, 4, 5, 6 };
    const array2 = [_]u32{ 1, 2, 3, 4, 5, 6 };
    const a = array1[0..];
    const b = array2[0..];
    const c = array1[0..3];
    try testing.expect(testHashDeep(a) == testHashDeep(a));
    try testing.expect(testHashDeep(a) == testHashDeep(array1));
    try testing.expect(testHashDeep(a) == testHashDeep(b));
    try testing.expect(testHashDeep(a) != testHashDeep(c));
}

test "hash struct deep" {
    const Foo = struct {
        a: u32,
        b: u16,
        c: *bool,

        const Self = @This();

        pub fn init(allocator: mem.Allocator, a_: u32, b_: u16, c_: bool) !Self {
            const ptr = try allocator.create(bool);
            ptr.* = c_;
            return Self{ .a = a_, .b = b_, .c = ptr };
        }
    };

    const allocator = std.testing.allocator;
    const foo = try Foo.init(allocator, 123, 10, true);
    const bar = try Foo.init(allocator, 123, 10, true);
    const baz = try Foo.init(allocator, 123, 10, false);
    defer allocator.destroy(foo.c);
    defer allocator.destroy(bar.c);
    defer allocator.destroy(baz.c);

    try testing.expect(testHashDeep(foo) == testHashDeep(bar));
    try testing.expect(testHashDeep(foo) != testHashDeep(baz));
    try testing.expect(testHashDeep(bar) != testHashDeep(baz));

    var hasher = Wyhash.init(0);
    const h = testHashDeep(foo);
    autoHash(&hasher, foo.a);
    autoHash(&hasher, foo.b);
    autoHash(&hasher, foo.c.*);
    try testing.expectEqual(h, hasher.final());

    const h2 = testHashDeepRecursive(&foo);
    try testing.expect(h2 != testHashDeep(&foo));
    try testing.expect(h2 == testHashDeep(foo));
}

test "testHash optional" {
    const a: ?u32 = 123;
    const b: ?u32 = null;
    try testing.expectEqual(testHash(a), testHash(@as(u32, 123)));
    try testing.expect(testHash(a) != testHash(b));
    try testing.expectEqual(testHash(b), 0x409638ee2bde459); // wyhash empty input hash
}

test "testHash array" {
    const a = [_]u32{ 1, 2, 3 };
    const h = testHash(a);
    var hasher = Wyhash.init(0);
    autoHash(&hasher, @as(u32, 1));
    autoHash(&hasher, @as(u32, 2));
    autoHash(&hasher, @as(u32, 3));
    try testing.expectEqual(h, hasher.final());
}

test "testHash multi-dimensional array" {
    const a = [_][]const u32{ &.{ 1, 2, 3 }, &.{ 4, 5 } };
    const b = [_][]const u32{ &.{ 1, 2 }, &.{ 3, 4, 5 } };
    try testing.expect(testHash(a) != testHash(b));
}

test "testHash struct" {
    const Foo = struct {
        a: u32 = 1,
        b: u32 = 2,
        c: u32 = 3,
    };
    const f = Foo{};
    const h = testHash(f);
    var hasher = Wyhash.init(0);
    autoHash(&hasher, @as(u32, 1));
    autoHash(&hasher, @as(u32, 2));
    autoHash(&hasher, @as(u32, 3));
    try testing.expectEqual(h, hasher.final());
}

test "testHash union" {
    const Foo = union(enum) {
        A: u32,
        B: bool,
        C: u32,
        D: void,
    };

    const a = Foo{ .A = 18 };
    var b = Foo{ .B = true };
    const c = Foo{ .C = 18 };
    const d: Foo = .D;
    try testing.expect(testHash(a) == testHash(a));
    try testing.expect(testHash(a) != testHash(b));
    try testing.expect(testHash(a) != testHash(c));
    try testing.expect(testHash(a) != testHash(d));

    b = Foo{ .A = 18 };
    try testing.expect(testHash(a) == testHash(b));

    b = .D;
    try testing.expect(testHash(d) == testHash(b));
}

test "testHash vector" {
    const a: @Vector(4, u32) = [_]u32{ 1, 2, 3, 4 };
    const b: @Vector(4, u32) = [_]u32{ 1, 2, 3, 5 };
    try testing.expect(testHash(a) == testHash(a));
    try testing.expect(testHash(a) != testHash(b));

    const c: @Vector(4, u31) = [_]u31{ 1, 2, 3, 4 };
    const d: @Vector(4, u31) = [_]u31{ 1, 2, 3, 5 };
    try testing.expect(testHash(c) == testHash(c));
    try testing.expect(testHash(c) != testHash(d));
}

test "testHash error union" {
    const Errors = error{Test};
    const Foo = struct {
        a: u32 = 1,
        b: u32 = 2,
        c: u32 = 3,
    };
    const f = Foo{};
    const g: Errors!Foo = Errors.Test;
    try testing.expect(testHash(f) != testHash(g));
    try testing.expect(testHash(f) == testHash(Foo{}));
    try testing.expect(testHash(g) == testHash(Errors.Test));
}



---
File: /std/hash/benchmark.zig
---

// zig run -O ReleaseFast --zig-lib-dir ../.. benchmark.zig
const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const time = std.time;
const hash = std.hash;

const KiB = 1024;
const MiB = 1024 * KiB;
const GiB = 1024 * MiB;

var prng = std.Random.DefaultPrng.init(0);
const random = prng.random();

const Hash = struct {
    ty: type,
    name: []const u8,
    has_iterative_api: bool = true,
    has_crypto_api: bool = false,
    has_anytype_api: ?[]const comptime_int = null,
    /// `final` value should be read from this field.
    has_struct_api: ?[]const u8 = null,
    init_u8s: ?[]const u8 = null,
    init_u64: ?u64 = null,
    init_default: bool = false,
};

const hashes = [_]Hash{
    Hash{
        .ty = hash.XxHash3,
        .name = "xxh3",
        .init_u64 = 0,
        .has_anytype_api = @as([]const comptime_int, &[_]comptime_int{ 8, 16, 32, 48, 64, 80, 96, 112, 128 }),
    },
    Hash{
        .ty = hash.XxHash64,
        .name = "xxhash64",
        .init_u64 = 0,
        .has_anytype_api = @as([]const comptime_int, &[_]comptime_int{ 8, 16, 32, 48, 64, 80, 96, 112, 128 }),
    },
    Hash{
        .ty = hash.XxHash32,
        .name = "xxhash32",
        .init_u64 = 0,
        .has_anytype_api = @as([]const comptime_int, &[_]comptime_int{ 8, 16, 32, 48, 64, 80, 96, 112, 128 }),
    },
    Hash{
        .ty = hash.Wyhash,
        .name = "wyhash",
        .init_u64 = 0,
    },
    Hash{
        .ty = hash.Fnv1a_64,
        .name = "fnv1a",
    },
    Hash{
        .ty = hash.Adler32,
        .name = "adler32",
        .has_struct_api = "adler",
        .init_default = true,
    },
    Hash{
        .ty = hash.crc.Crc32,
        .name = "crc32",
    },
    Hash{
        .ty = hash.CityHash32,
        .name = "cityhash-32",
        .has_iterative_api = false,
    },
    Hash{
        .ty = hash.CityHash64,
        .name = "cityhash-64",
        .has_iterative_api = false,
    },
    Hash{
        .ty = hash.Murmur2_32,
        .name = "murmur2-32",
        .has_iterative_api = false,
    },
    Hash{
        .ty = hash.Murmur2_64,
        .name = "murmur2-64",
        .has_iterative_api = false,
    },
    Hash{
        .ty = hash.Murmur3_32,
        .name = "murmur3-32",
        .has_iterative_api = false,
    },
    Hash{
        .ty = hash.SipHash64(1, 3),
        .name = "siphash64",
        .has_crypto_api = true,
        .init_u8s = &[_]u8{0} ** 16,
    },
    Hash{
        .ty = hash.SipHash128(1, 3),
        .name = "siphash128",
        .has_crypto_api = true,
        .init_u8s = &[_]u8{0} ** 16,
    },
};

const Result = struct {
    hash: u64,
    throughput: u64,
};

const block_size: usize = 8 * 8192;

pub fn benchTime(io: Io) i96 {
    return Io.Clock.awake.now(io).nanoseconds;
}

pub fn benchmarkHash(comptime H: anytype, bytes: usize, allocator: std.mem.Allocator, io: Io) !Result {
    var blocks = try allocator.alloc(u8, bytes);
    defer allocator.free(blocks);
    random.bytes(blocks);

    const block_count = bytes / block_size;

    var h: H.ty = blk: {
        if (H.init_u8s) |init| {
            break :blk .init(init[0..H.ty.key_length]);
        }
        if (H.init_u64) |init| {
            break :blk .init(init);
        }
        if (H.init_default) {
            break :blk .{};
        }
        break :blk .init();
    };

    const start = benchTime(io);
    for (0..block_count) |i| {
        h.update(blocks[i * block_size ..][0..block_size]);
    }
    const final = if (H.has_struct_api) |field_name|
        @field(h, field_name)
    else if (H.has_crypto_api)
        @as(u64, @truncate(h.finalInt()))
    else
        h.final();
    std.mem.doNotOptimizeAway(final);

    const elapsed_ns = benchTime(io) - start;

    const elapsed_s = @as(f64, @floatFromInt(elapsed_ns)) / time.ns_per_s;
    const size_float: f64 = @floatFromInt(block_size * block_count);
    const throughput: u64 = @intFromFloat(size_float / elapsed_s);

    return Result{
        .hash = final,
        .throughput = throughput,
    };
}

pub fn benchmarkHashSmallKeys(comptime H: anytype, key_size: usize, bytes: usize, allocator: std.mem.Allocator, io: Io) !Result {
    var blocks = try allocator.alloc(u8, bytes);
    defer allocator.free(blocks);
    random.bytes(blocks);

    const key_count = bytes / key_size;

    const start = benchTime(io);

    var sum: u64 = 0;
    for (0..key_count) |i| {
        const small_key = blocks[i * key_size ..][0..key_size];
        const final = blk: {
            if (H.init_u8s) |init| {
                if (H.has_crypto_api) {
                    break :blk @as(u64, @truncate(H.ty.toInt(small_key, init[0..H.ty.key_length])));
                } else {
                    break :blk H.ty.hash(init, small_key);
                }
            }
            if (H.init_u64) |init| {
                break :blk H.ty.hash(init, small_key);
            }
            break :blk H.ty.hash(small_key);
        };
        sum +%= final;
    }
    const elapsed_ns = benchTime(io) - start;

    const elapsed_s = @as(f64, @floatFromInt(elapsed_ns)) / time.ns_per_s;
    const size_float: f64 = @floatFromInt(key_count * key_size);
    const throughput: u64 = @intFromFloat(size_float / elapsed_s);

    std.mem.doNotOptimizeAway(sum);

    return Result{
        .hash = sum,
        .throughput = throughput,
    };
}

// the array and array pointer benchmarks for xxhash are very sensitive to in-lining,
// if you see strange performance changes consider using `.never_inline` or `.always_inline`
// to ensure the changes are not only due to the optimiser inlining the benchmark differently
pub fn benchmarkHashSmallKeysArrayPtr(
    comptime H: anytype,
    comptime key_size: usize,
    bytes: usize,
    allocator: std.mem.Allocator,
    io: Io,
) !Result {
    var blocks = try allocator.alloc(u8, bytes);
    defer allocator.free(blocks);
    random.bytes(blocks);

    const key_count = bytes / key_size;

    const start = benchTime(io);

    var sum: u64 = 0;
    for (0..key_count) |i| {
        const small_key = blocks[i * key_size ..][0..key_size];
        const final: u64 = blk: {
            if (H.init_u8s) |init| {
                if (H.has_crypto_api) {
                    break :blk @truncate(H.ty.toInt(small_key, init[0..H.ty.key_length]));
                } else {
                    break :blk H.ty.hash(init, small_key);
                }
            }
            if (H.init_u64) |init| {
                break :blk H.ty.hash(init, small_key);
            }
            break :blk H.ty.hash(small_key);
        };
        sum +%= final;
    }
    const elapsed_ns = benchTime(io) - start;

    const elapsed_s = @as(f64, @floatFromInt(elapsed_ns)) / time.ns_per_s;
    const throughput: u64 = @intFromFloat(@as(f64, @floatFromInt(bytes)) / elapsed_s);

    std.mem.doNotOptimizeAway(sum);

    return Result{
        .hash = sum,
        .throughput = throughput,
    };
}

// the array and array pointer benchmarks for xxhash are very sensitive to in-lining,
// if you see strange performance changes consider using `.never_inline` or `.always_inline`
// to ensure the changes are not only due to the optimiser inlining the benchmark differently
pub fn benchmarkHashSmallKeysArray(
    comptime H: anytype,
    comptime key_size: usize,
    bytes: usize,
    allocator: std.mem.Allocator,
    io: Io,
) !Result {
    var blocks = try allocator.alloc(u8, bytes);
    defer allocator.free(blocks);
    random.bytes(blocks);

    const key_count = bytes / key_size;

    var i: usize = 0;
    const start = benchTime(io);

    var sum: u64 = 0;
    while (i < key_count) : (i += 1) {
        const small_key = blocks[i * key_size ..][0..key_size];
        const final: u64 = blk: {
            if (H.init_u8s) |init| {
                if (H.has_crypto_api) {
                    break :blk @truncate(H.ty.toInt(small_key, init[0..H.ty.key_length]));
                } else {
                    break :blk H.ty.hash(init, small_key.*);
                }
            }
            if (H.init_u64) |init| {
                break :blk H.ty.hash(init, small_key.*);
            }
            break :blk H.ty.hash(small_key.*);
        };
        sum +%= final;
    }
    const elapsed_ns = benchTime(io) - start;

    const elapsed_s = @as(f64, @floatFromInt(elapsed_ns)) / time.ns_per_s;
    const throughput: u64 = @intFromFloat(@as(f64, @floatFromInt(bytes)) / elapsed_s);

    std.mem.doNotOptimizeAway(sum);

    return Result{
        .hash = sum,
        .throughput = throughput,
    };
}

pub fn benchmarkHashSmallApi(comptime H: anytype, key_size: usize, bytes: usize, allocator: std.mem.Allocator, io: Io) !Result {
    var blocks = try allocator.alloc(u8, bytes);
    defer allocator.free(blocks);
    random.bytes(blocks);

    const key_count = bytes / key_size;

    const start = benchTime(io);

    var sum: u64 = 0;
    for (0..key_count) |i| {
        const small_key = blocks[i * key_size ..][0..key_size];
        const final: u64 = blk: {
            if (H.init_u8s) |init| {
                if (H.has_crypto_api) {
                    break :blk @truncate(H.ty.toInt(small_key, init[0..H.ty.key_length]));
                } else {
                    break :blk H.ty.hashSmall(init, small_key);
                }
            }
            if (H.init_u64) |init| {
                break :blk H.ty.hashSmall(init, small_key);
            }
            break :blk H.ty.hashSmall(small_key);
        };
        sum +%= final;
    }
    const elapsed_ns = benchTime(io) - start;

    const elapsed_s = @as(f64, @floatFromInt(elapsed_ns)) / time.ns_per_s;
    const throughput: u64 = @intFromFloat(@as(f64, @floatFromInt(bytes)) / elapsed_s);

    std.mem.doNotOptimizeAway(sum);

    return Result{
        .throughput = throughput,
        .hash = sum,
    };
}

fn usage() void {
    std.debug.print(
        \\throughput_test [options]
        \\
        \\Options:
        \\  --filter    [test-name]
        \\  --seed      [int]
        \\  --count     [int]
        \\  --key-size  [int]
        \\  --iterative-only
        \\  --small-key-only
        \\  --help
        \\
    , .{});
}

fn mode(comptime x: comptime_int) comptime_int {
    return if (builtin.mode == .Debug) x / 64 else x;
}

pub fn main(init: std.process.Init) !void {
    const io = init.io;
    const arena = init.arena.allocator();

    var stdout_buffer: [0x100]u8 = undefined;
    var stdout_writer = Io.File.stdout().writer(io, &stdout_buffer);
    const stdout = &stdout_writer.interface;

    const args = try init.minimal.args.toSlice(arena);

    var filter: ?[]const u8 = null;
    var count: usize = mode(128 * MiB);
    var key_size: ?usize = null;
    var seed: u32 = 0;
    var test_small_key_only = false;
    var test_iterative_only = false;
    var test_arrays = false;

    const default_small_key_size = 32;

    var i: usize = 1;
    while (i < args.len) : (i += 1) {
        if (std.mem.eql(u8, args[i], "--mode")) {
            try stdout.print("{}\n", .{builtin.mode});
            try stdout.flush();
            return;
        } else if (std.mem.eql(u8, args[i], "--seed")) {
            i += 1;
            if (i == args.len) {
                usage();
                std.process.exit(1);
            }

            seed = try std.fmt.parseUnsigned(u32, args[i], 10);
            // we seed later
        } else if (std.mem.eql(u8, args[i], "--filter")) {
            i += 1;
            if (i == args.len) {
                usage();
                std.process.exit(1);
            }

            filter = args[i];
        } else if (std.mem.eql(u8, args[i], "--count")) {
            i += 1;
            if (i == args.len) {
                usage();
                std.process.exit(1);
            }

            const c = try std.fmt.parseUnsigned(usize, args[i], 10);
            count = c * MiB;
        } else if (std.mem.eql(u8, args[i], "--key-size")) {
            i += 1;
            if (i == args.len) {
                usage();
                std.process.exit(1);
            }

            key_size = try std.fmt.parseUnsigned(usize, args[i], 10);
            if (key_size.? > block_size) {
                try stdout.print("key_size cannot exceed block size of {}\n", .{block_size});
                try stdout.flush();
                std.process.exit(1);
            }
        } else if (std.mem.eql(u8, args[i], "--iterative-only")) {
            test_iterative_only = true;
        } else if (std.mem.eql(u8, args[i], "--small-key-only")) {
            test_small_key_only = true;
        } else if (std.mem.eql(u8, args[i], "--include-array")) {
            test_arrays = true;
        } else if (std.mem.eql(u8, args[i], "--help")) {
            usage();
            return;
        } else {
            usage();
            std.process.exit(1);
        }
    }

    if (test_iterative_only and test_small_key_only) {
        try stdout.print("Cannot use iterative-only and small-key-only together!\n", .{});
        try stdout.flush();
        usage();
        std.process.exit(1);
    }

    var gpa: std.heap.DebugAllocator(.{}) = .init;
    defer std.testing.expect(gpa.deinit() == .ok) catch @panic("leak");
    const allocator = gpa.allocator();

    inline for (hashes) |H| {
        if (filter == null or std.mem.find(u8, H.name, filter.?) != null) hash: {
            if (!test_iterative_only or H.has_iterative_api) {
                try stdout.print("{s}\n", .{H.name});
                try stdout.flush();

                // Always reseed prior to every call so we are hashing the same buffer contents.
                // This allows easier comparison between different implementations.
                if (H.has_iterative_api and !test_small_key_only) {
                    prng.seed(seed);
                    const result = try benchmarkHash(H, count, allocator, io);
                    try stdout.print("   iterative: {:5} MiB/s [{x:0<16}]\n", .{ result.throughput / (1 * MiB), result.hash });
                    try stdout.flush();
                }

                if (!test_iterative_only) {
                    if (key_size) |size| {
                        prng.seed(seed);
                        const result_small = try benchmarkHashSmallKeys(H, size, count, allocator, io);
                        try stdout.print("  small keys: {:3}B {:5} MiB/s {} Hashes/s [{x:0<16}]\n", .{
                            size,
                            result_small.throughput / (1 * MiB),
                            result_small.throughput / size,
                            result_small.hash,
                        });
                        try stdout.flush();

                        if (!test_arrays) break :hash;
                        if (H.has_anytype_api) |sizes| {
                            inline for (sizes) |exact_size| {
                                if (size == exact_size) {
                                    prng.seed(seed);
                                    const result_array = try benchmarkHashSmallKeysArray(H, exact_size, count, allocator, io);
                                    prng.seed(seed);
                                    const result_ptr = try benchmarkHashSmallKeysArrayPtr(H, exact_size, count, allocator, io);
                                    try stdout.print("       array: {:5} MiB/s [{x:0<16}]\n", .{
                                        result_array.throughput / (1 * MiB),
                                        result_array.hash,
                                    });
                                    try stdout.print("   array ptr: {:5} MiB/s [{x:0<16}]\n", .{
                                        result_ptr.throughput / (1 * MiB),
                                        result_ptr.hash,
                                    });
                                    try stdout.flush();
                                }
                            }
                        }
                    } else {
                        prng.seed(seed);
                        const result_small = try benchmarkHashSmallKeys(H, default_small_key_size, count, allocator, io);
                        try stdout.print("  small keys: {:3}B {:5} MiB/s {} Hashes/s [{x:0<16}]\n", .{
                            default_small_key_size,
                            result_small.throughput / (1 * MiB),
                            result_small.throughput / default_small_key_size,
                            result_small.hash,
                        });
                        try stdout.flush();

                        if (!test_arrays) break :hash;
                        if (H.has_anytype_api) |sizes| {
                            try stdout.print("       array:\n", .{});
                            inline for (sizes) |exact_size| {
                                prng.seed(seed);
                                const result = try benchmarkHashSmallKeysArray(H, exact_size, count, allocator, io);
                                try stdout.print("       {d: >3}B {:5} MiB/s [{x:0<16}]\n", .{
                                    exact_size,
                                    result.throughput / (1 * MiB),
                                    result.hash,
                                });
                                try stdout.flush();
                            }
                            try stdout.print("   array ptr: \n", .{});
                            inline for (sizes) |exact_size| {
                                prng.seed(seed);
                                const result = try benchmarkHashSmallKeysArrayPtr(H, exact_size, count, allocator, io);
                                try stdout.print("       {d: >3}B {:5} MiB/s [{x:0<16}]\n", .{
                                    exact_size,
                                    result.throughput / (1 * MiB),
                                    result.hash,
                                });
                                try stdout.flush();
                            }
                        }
                    }
                }
            }
        }
    }
}



---
File: /std/hash/cityhash.zig
---

const std = @import("std");

inline fn offsetPtr(ptr: [*]const u8, offset: usize) [*]const u8 {
    // ptr + offset doesn't work at comptime so we need this instead.
    return @as([*]const u8, @ptrCast(&ptr[offset]));
}

fn fetch32(ptr: [*]const u8, offset: usize) u32 {
    return std.mem.readInt(u32, offsetPtr(ptr, offset)[0..4], .little);
}

fn fetch64(ptr: [*]const u8, offset: usize) u64 {
    return std.mem.readInt(u64, offsetPtr(ptr, offset)[0..8], .little);
}

pub const CityHash32 = struct {
    const Self = @This();

    // Magic numbers for 32-bit hashing.  Copied from Murmur3.
    const c1: u32 = 0xcc9e2d51;
    const c2: u32 = 0x1b873593;

    // A 32-bit to 32-bit integer hash copied from Murmur3.
    fn fmix(h: u32) u32 {
        var h1: u32 = h;
        h1 ^= h1 >> 16;
        h1 *%= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *%= 0xc2b2ae35;
        h1 ^= h1 >> 16;
        return h1;
    }

    // Rotate right helper
    fn rotr32(x: u32, comptime r: u32) u32 {
        return (x >> r) | (x << (32 - r));
    }

    // Helper from Murmur3 for combining two 32-bit values.
    fn mur(a: u32, h: u32) u32 {
        var a1: u32 = a;
        var h1: u32 = h;
        a1 *%= c1;
        a1 = rotr32(a1, 17);
        a1 *%= c2;
        h1 ^= a1;
        h1 = rotr32(h1, 19);
        return h1 *% 5 +% 0xe6546b64;
    }

    fn hash32Len0To4(str: []const u8) u32 {
        const len: u32 = @as(u32, @truncate(str.len));
        var b: u32 = 0;
        var c: u32 = 9;
        for (str) |v| {
            b = b *% c1 +% @as(u32, @bitCast(@as(i32, @intCast(@as(i8, @bitCast(v))))));
            c ^= b;
        }
        return fmix(mur(b, mur(len, c)));
    }

    fn hash32Len5To12(str: []const u8) u32 {
        var a: u32 = @as(u32, @truncate(str.len));
        var b: u32 = a *% 5;
        var c: u32 = 9;
        const d: u32 = b;

        a +%= fetch32(str.ptr, 0);
        b +%= fetch32(str.ptr, str.len - 4);
        c +%= fetch32(str.ptr, (str.len >> 1) & 4);

        return fmix(mur(c, mur(b, mur(a, d))));
    }

    fn hash32Len13To24(str: []const u8) u32 {
        const len: u32 = @as(u32, @truncate(str.len));
        const a: u32 = fetch32(str.ptr, (str.len >> 1) - 4);
        const b: u32 = fetch32(str.ptr, 4);
        const c: u32 = fetch32(str.ptr, str.len - 8);
        const d: u32 = fetch32(str.ptr, str.len >> 1);
        const e: u32 = fetch32(str.ptr, 0);
        const f: u32 = fetch32(str.ptr, str.len - 4);

        return fmix(mur(f, mur(e, mur(d, mur(c, mur(b, mur(a, len)))))));
    }

    pub fn hash(str: []const u8) u32 {
        if (str.len <= 24) {
            if (str.len <= 4) {
                return hash32Len0To4(str);
            } else {
                if (str.len <= 12)
                    return hash32Len5To12(str);
                return hash32Len13To24(str);
            }
        }

        const len: u32 = @as(u32, @truncate(str.len));
        var h: u32 = len;
        var g: u32 = c1 *% len;
        var f: u32 = g;

        const a0: u32 = rotr32(fetch32(str.ptr, str.len - 4) *% c1, 17) *% c2;
        const a1: u32 = rotr32(fetch32(str.ptr, str.len - 8) *% c1, 17) *% c2;
        const a2: u32 = rotr32(fetch32(str.ptr, str.len - 16) *% c1, 17) *% c2;
        const a3: u32 = rotr32(fetch32(str.ptr, str.len - 12) *% c1, 17) *% c2;
        const a4: u32 = rotr32(fetch32(str.ptr, str.len - 20) *% c1, 17) *% c2;

        h ^= a0;
        h = rotr32(h, 19);
        h = h *% 5 +% 0xe6546b64;
        h ^= a2;
        h = rotr32(h, 19);
        h = h *% 5 +% 0xe6546b64;
        g ^= a1;
        g = rotr32(g, 19);
        g = g *% 5 +% 0xe6546b64;
        g ^= a3;
        g = rotr32(g, 19);
        g = g *% 5 +% 0xe6546b64;
        f +%= a4;
        f = rotr32(f, 19);
        f = f *% 5 +% 0xe6546b64;
        var iters = (str.len - 1) / 20;
        var ptr = str.ptr;
        while (iters != 0) : (iters -= 1) {
            const b0: u32 = rotr32(fetch32(ptr, 0) *% c1, 17) *% c2;
            const b1: u32 = fetch32(ptr, 4);
            const b2: u32 = rotr32(fetch32(ptr, 8) *% c1, 17) *% c2;
            const b3: u32 = rotr32(fetch32(ptr, 12) *% c1, 17) *% c2;
            const b4: u32 = fetch32(ptr, 16);

            h ^= b0;
            h = rotr32(h, 18);
            h = h *% 5 +% 0xe6546b64;
            f +%= b1;
            f = rotr32(f, 19);
            f = f *% c1;
            g +%= b2;
            g = rotr32(g, 18);
            g = g *% 5 +% 0xe6546b64;
            h ^= b3 +% b1;
            h = rotr32(h, 19);
            h = h *% 5 +% 0xe6546b64;
            g ^= b4;
            g = @byteSwap(g) *% 5;
            h +%= b4 *% 5;
            h = @byteSwap(h);
            f +%= b0;
            const t: u32 = h;
            h = f;
            f = g;
            g = t;
            ptr = offsetPtr(ptr, 20);
        }
        g = rotr32(g, 11) *% c1;
        g = rotr32(g, 17) *% c1;
        f = rotr32(f, 11) *% c1;
        f = rotr32(f, 17) *% c1;
        h = rotr32(h +% g, 19);
        h = h *% 5 +% 0xe6546b64;
        h = rotr32(h, 17) *% c1;
        h = rotr32(h +% f, 19);
        h = h *% 5 +% 0xe6546b64;
        h = rotr32(h, 17) *% c1;
        return h;
    }
};

pub const CityHash64 = struct {
    const Self = @This();

    // Some primes between 2^63 and 2^64 for various uses.
    const k0: u64 = 0xc3a5c85c97cb3127;
    const k1: u64 = 0xb492b66fbe98f273;
    const k2: u64 = 0x9ae16a3b2f90404f;

    // Rotate right helper
    fn rotr64(x: u64, comptime r: u64) u64 {
        return (x >> r) | (x << (64 - r));
    }

    fn shiftmix(v: u64) u64 {
        return v ^ (v >> 47);
    }

    fn hashLen16(u: u64, v: u64) u64 {
        return @call(.always_inline, hash128To64, .{ u, v });
    }

    fn hashLen16Mul(low: u64, high: u64, mul: u64) u64 {
        var a: u64 = (low ^ high) *% mul;
        a ^= (a >> 47);
        var b: u64 = (high ^ a) *% mul;
        b ^= (b >> 47);
        b *%= mul;
        return b;
    }

    fn hash128To64(low: u64, high: u64) u64 {
        return @call(.always_inline, hashLen16Mul, .{ low, high, 0x9ddfea08eb382d69 });
    }

    fn hashLen0To16(str: []const u8) u64 {
        const len: u64 = @as(u64, str.len);
        if (len >= 8) {
            const mul: u64 = k2 +% len *% 2;
            const a: u64 = fetch64(str.ptr, 0) +% k2;
            const b: u64 = fetch64(str.ptr, str.len - 8);
            const c: u64 = rotr64(b, 37) *% mul +% a;
            const d: u64 = (rotr64(a, 25) +% b) *% mul;
            return hashLen16Mul(c, d, mul);
        }
        if (len >= 4) {
            const mul: u64 = k2 +% len *% 2;
            const a: u64 = fetch32(str.ptr, 0);
            return hashLen16Mul(len +% (a << 3), fetch32(str.ptr, str.len - 4), mul);
        }
        if (len > 0) {
            const a: u8 = str[0];
            const b: u8 = str[str.len >> 1];
            const c: u8 = str[str.len - 1];
            const y: u32 = @as(u32, @intCast(a)) +% (@as(u32, @intCast(b)) << 8);
            const z: u32 = @as(u32, @truncate(str.len)) +% (@as(u32, @intCast(c)) << 2);
            return shiftmix(@as(u64, @intCast(y)) *% k2 ^ @as(u64, @intCast(z)) *% k0) *% k2;
        }
        return k2;
    }

    fn hashLen17To32(str: []const u8) u64 {
        const len: u64 = @as(u64, str.len);
        const mul: u64 = k2 +% len *% 2;
        const a: u64 = fetch64(str.ptr, 0) *% k1;
        const b: u64 = fetch64(str.ptr, 8);
        const c: u64 = fetch64(str.ptr, str.len - 8) *% mul;
        const d: u64 = fetch64(str.ptr, str.len - 16) *% k2;

        return hashLen16Mul(rotr64(a +% b, 43) +% rotr64(c, 30) +% d, a +% rotr64(b +% k2, 18) +% c, mul);
    }

    fn hashLen33To64(str: []const u8) u64 {
        const len: u64 = @as(u64, str.len);
        const mul: u64 = k2 +% len *% 2;
        const a: u64 = fetch64(str.ptr, 0) *% k2;
        const b: u64 = fetch64(str.ptr, 8);
        const c: u64 = fetch64(str.ptr, str.len - 24);
        const d: u64 = fetch64(str.ptr, str.len - 32);
        const e: u64 = fetch64(str.ptr, 16) *% k2;
        const f: u64 = fetch64(str.ptr, 24) *% 9;
        const g: u64 = fetch64(str.ptr, str.len - 8);
        const h: u64 = fetch64(str.ptr, str.len - 16) *% mul;

        const u: u64 = rotr64(a +% g, 43) +% (rotr64(b, 30) +% c) *% 9;
        const v: u64 = ((a +% g) ^ d) +% f +% 1;
        const w: u64 = @byteSwap((u +% v) *% mul) +% h;
        const x: u64 = rotr64(e +% f, 42) +% c;
        const y: u64 = (@byteSwap((v +% w) *% mul) +% g) *% mul;
        const z: u64 = e +% f +% c;
        const a1: u64 = @byteSwap((x +% z) *% mul +% y) +% b;
        const b1: u64 = shiftmix((z +% a1) *% mul +% d +% h) *% mul;
        return b1 +% x;
    }

    const WeakPair = struct {
        first: u64,
        second: u64,
    };

    fn weakHashLen32WithSeedsHelper(w: u64, x: u64, y: u64, z: u64, a: u64, b: u64) WeakPair {
        var a1: u64 = a;
        var b1: u64 = b;
        a1 +%= w;
        b1 = rotr64(b1 +% a1 +% z, 21);
        const c: u64 = a1;
        a1 +%= x;
        a1 +%= y;
        b1 +%= rotr64(a1, 44);
        return WeakPair{ .first = a1 +% z, .second = b1 +% c };
    }

    fn weakHashLen32WithSeeds(ptr: [*]const u8, a: u64, b: u64) WeakPair {
        return @call(.always_inline, weakHashLen32WithSeedsHelper, .{
            fetch64(ptr, 0),
            fetch64(ptr, 8),
            fetch64(ptr, 16),
            fetch64(ptr, 24),
            a,
            b,
        });
    }

    pub fn hash(str: []const u8) u64 {
        if (str.len <= 32) {
            if (str.len <= 16) {
                return hashLen0To16(str);
            } else {
                return hashLen17To32(str);
            }
        } else if (str.len <= 64) {
            return hashLen33To64(str);
        }

        var len: u64 = @as(u64, str.len);

        var x: u64 = fetch64(str.ptr, str.len - 40);
        var y: u64 = fetch64(str.ptr, str.len - 16) +% fetch64(str.ptr, str.len - 56);
        var z: u64 = hashLen16(fetch64(str.ptr, str.len - 48) +% len, fetch64(str.ptr, str.len - 24));
        var v: WeakPair = weakHashLen32WithSeeds(offsetPtr(str.ptr, str.len - 64), len, z);
        var w: WeakPair = weakHashLen32WithSeeds(offsetPtr(str.ptr, str.len - 32), y +% k1, x);

        x = x *% k1 +% fetch64(str.ptr, 0);
        len = (len - 1) & ~@as(u64, @intCast(63));

        var ptr: [*]const u8 = str.ptr;
        while (true) {
            x = rotr64(x +% y +% v.first +% fetch64(ptr, 8), 37) *% k1;
            y = rotr64(y +% v.second +% fetch64(ptr, 48), 42) *% k1;
            x ^= w.second;
            y +%= v.first +% fetch64(ptr, 40);
            z = rotr64(z +% w.first, 33) *% k1;
            v = weakHashLen32WithSeeds(ptr, v.second *% k1, x +% w.first);
            w = weakHashLen32WithSeeds(offsetPtr(ptr, 32), z +% w.second, y +% fetch64(ptr, 16));
            const t: u64 = z;
            z = x;
            x = t;

            ptr = offsetPtr(ptr, 64);
            len -= 64;
            if (len == 0)
                break;
        }

        return hashLen16(hashLen16(v.first, w.first) +% shiftmix(y) *% k1 +% z, hashLen16(v.second, w.second) +% x);
    }

    pub fn hashWithSeed(str: []const u8, seed: u64) u64 {
        return @call(.always_inline, Self.hashWithSeeds, .{ str, k2, seed });
    }

    pub fn hashWithSeeds(str: []const u8, seed0: u64, seed1: u64) u64 {
        return hashLen16(hash(str) -% seed0, seed1);
    }
};

fn CityHash32hashIgnoreSeed(str: []const u8, seed: u32) u32 {
    _ = seed;
    return CityHash32.hash(str);
}

const verify = @import("verify.zig");

test "cityhash32" {
    const Test = struct {
        fn do() !void {
            // SMHasher doesn't provide a 32bit version of the algorithm.
            // The implementation was verified against the Google Abseil version.
            try std.testing.expectEqual(verify.smhasher(CityHash32hashIgnoreSeed), 0x68254F81);
        }
    };
    try Test.do();
    @setEvalBranchQuota(75000);
    try comptime Test.do();
}

test "cityhash64" {
    const Test = struct {
        fn do() !void {
            // This is not compliant with the SMHasher implementation of CityHash64!
            // The implementation was verified against the Google Abseil version.
            try std.testing.expectEqual(verify.smhasher(CityHash64.hashWithSeed), 0x5FABC5C5);
        }
    };
    try Test.do();
    @setEvalBranchQuota(75000);
    try comptime Test.do();
}



---
File: /std/hash/crc.zig
---

//! This file is auto-generated by tools/update_crc_catalog.zig.

const impl = @import("crc/impl.zig");

pub const Crc = impl.Crc;
pub const Polynomial = impl.Polynomial;
pub const Crc32WithPoly = impl.Crc32WithPoly;
pub const Crc32SmallWithPoly = impl.Crc32SmallWithPoly;

pub const Crc32 = Crc32IsoHdlc;

test {
    _ = @import("crc/test.zig");
}

pub const Crc3Gsm = Crc(u3, .{
    .polynomial = 0x3,
    .initial = 0x0,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x7,
});

pub const Crc3Rohc = Crc(u3, .{
    .polynomial = 0x3,
    .initial = 0x7,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0,
});

pub const Crc4G704 = Crc(u4, .{
    .polynomial = 0x3,
    .initial = 0x0,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0,
});

pub const Crc4Interlaken = Crc(u4, .{
    .polynomial = 0x3,
    .initial = 0xf,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xf,
});

pub const Crc5EpcC1g2 = Crc(u5, .{
    .polynomial = 0x09,
    .initial = 0x09,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc5G704 = Crc(u5, .{
    .polynomial = 0x15,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc5Usb = Crc(u5, .{
    .polynomial = 0x05,
    .initial = 0x1f,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x1f,
});

pub const Crc6Cdma2000A = Crc(u6, .{
    .polynomial = 0x27,
    .initial = 0x3f,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc6Cdma2000B = Crc(u6, .{
    .polynomial = 0x07,
    .initial = 0x3f,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc6Darc = Crc(u6, .{
    .polynomial = 0x19,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc6G704 = Crc(u6, .{
    .polynomial = 0x03,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc6Gsm = Crc(u6, .{
    .polynomial = 0x2f,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x3f,
});

pub const Crc7Mmc = Crc(u7, .{
    .polynomial = 0x09,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc7Rohc = Crc(u7, .{
    .polynomial = 0x4f,
    .initial = 0x7f,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc7Umts = Crc(u7, .{
    .polynomial = 0x45,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Autosar = Crc(u8, .{
    .polynomial = 0x2f,
    .initial = 0xff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xff,
});

pub const Crc8Bluetooth = Crc(u8, .{
    .polynomial = 0xa7,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc8Cdma2000 = Crc(u8, .{
    .polynomial = 0x9b,
    .initial = 0xff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Darc = Crc(u8, .{
    .polynomial = 0x39,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc8DvbS2 = Crc(u8, .{
    .polynomial = 0xd5,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8GsmA = Crc(u8, .{
    .polynomial = 0x1d,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8GsmB = Crc(u8, .{
    .polynomial = 0x49,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xff,
});

pub const Crc8Hitag = Crc(u8, .{
    .polynomial = 0x1d,
    .initial = 0xff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8I4321 = Crc(u8, .{
    .polynomial = 0x07,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x55,
});

pub const Crc8ICode = Crc(u8, .{
    .polynomial = 0x1d,
    .initial = 0xfd,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Lte = Crc(u8, .{
    .polynomial = 0x9b,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8MaximDow = Crc(u8, .{
    .polynomial = 0x31,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc8MifareMad = Crc(u8, .{
    .polynomial = 0x1d,
    .initial = 0xc7,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Nrsc5 = Crc(u8, .{
    .polynomial = 0x31,
    .initial = 0xff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Opensafety = Crc(u8, .{
    .polynomial = 0x2f,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Rohc = Crc(u8, .{
    .polynomial = 0x07,
    .initial = 0xff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc8SaeJ1850 = Crc(u8, .{
    .polynomial = 0x1d,
    .initial = 0xff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xff,
});

pub const Crc8Smbus = Crc(u8, .{
    .polynomial = 0x07,
    .initial = 0x00,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00,
});

pub const Crc8Tech3250 = Crc(u8, .{
    .polynomial = 0x1d,
    .initial = 0xff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc8Wcdma = Crc(u8, .{
    .polynomial = 0x9b,
    .initial = 0x00,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00,
});

pub const Crc10Atm = Crc(u10, .{
    .polynomial = 0x233,
    .initial = 0x000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000,
});

pub const Crc10Cdma2000 = Crc(u10, .{
    .polynomial = 0x3d9,
    .initial = 0x3ff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000,
});

pub const Crc10Gsm = Crc(u10, .{
    .polynomial = 0x175,
    .initial = 0x000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x3ff,
});

pub const Crc11Flexray = Crc(u11, .{
    .polynomial = 0x385,
    .initial = 0x01a,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000,
});

pub const Crc11Umts = Crc(u11, .{
    .polynomial = 0x307,
    .initial = 0x000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000,
});

pub const Crc12Cdma2000 = Crc(u12, .{
    .polynomial = 0xf13,
    .initial = 0xfff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000,
});

pub const Crc12Dect = Crc(u12, .{
    .polynomial = 0x80f,
    .initial = 0x000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000,
});

pub const Crc12Gsm = Crc(u12, .{
    .polynomial = 0xd31,
    .initial = 0x000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xfff,
});

pub const Crc12Umts = Crc(u12, .{
    .polynomial = 0x80f,
    .initial = 0x000,
    .reflect_input = false,
    .reflect_output = true,
    .xor_output = 0x000,
});

pub const Crc13Bbc = Crc(u13, .{
    .polynomial = 0x1cf5,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc14Darc = Crc(u14, .{
    .polynomial = 0x0805,
    .initial = 0x0000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc14Gsm = Crc(u14, .{
    .polynomial = 0x202d,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x3fff,
});

pub const Crc15Can = Crc(u15, .{
    .polynomial = 0x4599,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc15Mpt1327 = Crc(u15, .{
    .polynomial = 0x6815,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0001,
});

pub const Crc16Arc = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0x0000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16Cdma2000 = Crc(u16, .{
    .polynomial = 0xc867,
    .initial = 0xffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Cms = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0xffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Dds110 = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0x800d,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16DectR = Crc(u16, .{
    .polynomial = 0x0589,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0001,
});

pub const Crc16DectX = Crc(u16, .{
    .polynomial = 0x0589,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Dnp = Crc(u16, .{
    .polynomial = 0x3d65,
    .initial = 0x0000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffff,
});

pub const Crc16En13757 = Crc(u16, .{
    .polynomial = 0x3d65,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffff,
});

pub const Crc16Genibus = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0xffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffff,
});

pub const Crc16Gsm = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffff,
});

pub const Crc16Ibm3740 = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0xffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16IbmSdlc = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0xffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffff,
});

pub const Crc16IsoIec144433A = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0xc6c6,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16Kermit = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0x0000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16Lj1200 = Crc(u16, .{
    .polynomial = 0x6f63,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16M17 = Crc(u16, .{
    .polynomial = 0x5935,
    .initial = 0xffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16MaximDow = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0x0000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffff,
});

pub const Crc16Mcrf4xx = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0xffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16Modbus = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0xffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16Nrsc5 = Crc(u16, .{
    .polynomial = 0x080b,
    .initial = 0xffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16OpensafetyA = Crc(u16, .{
    .polynomial = 0x5935,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16OpensafetyB = Crc(u16, .{
    .polynomial = 0x755b,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Profibus = Crc(u16, .{
    .polynomial = 0x1dcf,
    .initial = 0xffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffff,
});

pub const Crc16Riello = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0xb2aa,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16SpiFujitsu = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0x1d0f,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16T10Dif = Crc(u16, .{
    .polynomial = 0x8bb7,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Teledisk = Crc(u16, .{
    .polynomial = 0xa097,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Tms37157 = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0x89ec,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000,
});

pub const Crc16Umts = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc16Usb = Crc(u16, .{
    .polynomial = 0x8005,
    .initial = 0xffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffff,
});

pub const Crc16Xmodem = Crc(u16, .{
    .polynomial = 0x1021,
    .initial = 0x0000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000,
});

pub const Crc17CanFd = Crc(u17, .{
    .polynomial = 0x1685b,
    .initial = 0x00000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00000,
});

pub const Crc21CanFd = Crc(u21, .{
    .polynomial = 0x102899,
    .initial = 0x000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000000,
});

pub const Crc24Ble = Crc(u24, .{
    .polynomial = 0x00065b,
    .initial = 0x555555,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x000000,
});

pub const Crc24FlexrayA = Crc(u24, .{
    .polynomial = 0x5d6dcb,
    .initial = 0xfedcba,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000000,
});

pub const Crc24FlexrayB = Crc(u24, .{
    .polynomial = 0x5d6dcb,
    .initial = 0xabcdef,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000000,
});

pub const Crc24Interlaken = Crc(u24, .{
    .polynomial = 0x328b63,
    .initial = 0xffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffffff,
});

pub const Crc24LteA = Crc(u24, .{
    .polynomial = 0x864cfb,
    .initial = 0x000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000000,
});

pub const Crc24LteB = Crc(u24, .{
    .polynomial = 0x800063,
    .initial = 0x000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000000,
});

pub const Crc24Openpgp = Crc(u24, .{
    .polynomial = 0x864cfb,
    .initial = 0xb704ce,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x000000,
});

pub const Crc24Os9 = Crc(u24, .{
    .polynomial = 0x800063,
    .initial = 0xffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffffff,
});

pub const Crc30Cdma = Crc(u30, .{
    .polynomial = 0x2030b9c7,
    .initial = 0x3fffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x3fffffff,
});

pub const Crc31Philips = Crc(u31, .{
    .polynomial = 0x04c11db7,
    .initial = 0x7fffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x7fffffff,
});

pub const Crc32Aixm = Crc(u32, .{
    .polynomial = 0x814141ab,
    .initial = 0x00000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00000000,
});

pub const Crc32Autosar = Crc(u32, .{
    .polynomial = 0xf4acfb13,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffff,
});

pub const Crc32Base91D = Crc(u32, .{
    .polynomial = 0xa833982b,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffff,
});

pub const Crc32Bzip2 = Crc(u32, .{
    .polynomial = 0x04c11db7,
    .initial = 0xffffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffffffff,
});

pub const Crc32CdRomEdc = Crc(u32, .{
    .polynomial = 0x8001801b,
    .initial = 0x00000000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00000000,
});

pub const Crc32Cksum = Crc(u32, .{
    .polynomial = 0x04c11db7,
    .initial = 0x00000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffffffff,
});

pub const Crc32Iscsi = Crc(u32, .{
    .polynomial = 0x1edc6f41,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffff,
});

pub const Crc32IsoHdlc = Crc(u32, .{
    .polynomial = 0x04c11db7,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffff,
});

pub const Crc32Jamcrc = Crc(u32, .{
    .polynomial = 0x04c11db7,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00000000,
});

pub const Crc32Koopman = Crc(u32, .{
    .polynomial = 0x741b8cd7,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffff,
});

pub const Crc32Mef = Crc(u32, .{
    .polynomial = 0x741b8cd7,
    .initial = 0xffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x00000000,
});

pub const Crc32Mpeg2 = Crc(u32, .{
    .polynomial = 0x04c11db7,
    .initial = 0xffffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00000000,
});

pub const Crc32Xfer = Crc(u32, .{
    .polynomial = 0x000000af,
    .initial = 0x00000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x00000000,
});

pub const Crc40Gsm = Crc(u40, .{
    .polynomial = 0x0004820009,
    .initial = 0x0000000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffffffffff,
});

pub const Crc64Ecma182 = Crc(u64, .{
    .polynomial = 0x42f0e1eba9ea3693,
    .initial = 0x0000000000000000,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0x0000000000000000,
});

pub const Crc64GoIso = Crc(u64, .{
    .polynomial = 0x000000000000001b,
    .initial = 0xffffffffffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffffffffffff,
});

pub const Crc64Ms = Crc(u64, .{
    .polynomial = 0x259c84cba6426349,
    .initial = 0xffffffffffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000000000000000,
});

pub const Crc64Redis = Crc(u64, .{
    .polynomial = 0xad93d23594c935a9,
    .initial = 0x0000000000000000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x0000000000000000,
});

pub const Crc64We = Crc(u64, .{
    .polynomial = 0x42f0e1eba9ea3693,
    .initial = 0xffffffffffffffff,
    .reflect_input = false,
    .reflect_output = false,
    .xor_output = 0xffffffffffffffff,
});

pub const Crc64Xz = Crc(u64, .{
    .polynomial = 0x42f0e1eba9ea3693,
    .initial = 0xffffffffffffffff,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0xffffffffffffffff,
});

pub const Crc82Darc = Crc(u82, .{
    .polynomial = 0x0308c0111011401440411,
    .initial = 0x000000000000000000000,
    .reflect_input = true,
    .reflect_output = true,
    .xor_output = 0x000000000000000000000,
});



---
File: /std/hash/fnv.zig
---

// FNV1a - Fowler-Noll-Vo hash function
//
// FNV1a is a fast, non-cryptographic hash function with fairly good distribution properties.
//
// https://tools.ietf.org/html/draft-eastlake-fnv-14

const std = @import("std");
const testing = std.testing;

pub const Fnv1a_32 = Fnv1a(u32, 0x01000193, 0x811c9dc5);
pub const Fnv1a_64 = Fnv1a(u64, 0x100000001b3, 0xcbf29ce484222325);
pub const Fnv1a_128 = Fnv1a(u128, 0x1000000000000000000013b, 0x6c62272e07bb014262b821756295c58d);

fn Fnv1a(comptime T: type, comptime prime: T, comptime offset: T) type {
    return struct {
        const Self = @This();

        value: T,

        pub fn init() Self {
            return Self{ .value = offset };
        }

        pub fn update(self: *Self, input: []const u8) void {
            for (input) |b| {
                self.value ^= b;
                self.value *%= prime;
            }
        }

        pub fn final(self: *Self) T {
            return self.value;
        }

        pub fn hash(input: []const u8) T {
            var c = Self.init();
            c.update(input);
            return c.final();
        }
    };
}

const verify = @import("verify.zig");

test "fnv1a-32" {
    try testing.expect(Fnv1a_32.hash("") == 0x811c9dc5);
    try testing.expect(Fnv1a_32.hash("a") == 0xe40c292c);
    try testing.expect(Fnv1a_32.hash("foobar") == 0xbf9cf968);
    try verify.iterativeApi(Fnv1a_32);
}

test "fnv1a-64" {
    try testing.expect(Fnv1a_64.hash("") == 0xcbf29ce484222325);
    try testing.expect(Fnv1a_64.hash("a") == 0xaf63dc4c8601ec8c);
    try testing.expect(Fnv1a_64.hash("foobar") == 0x85944171f73967e8);
    try verify.iterativeApi(Fnv1a_64);
}

test "fnv1a-128" {
    try testing.expect(Fnv1a_128.hash("") == 0x6c62272e07bb014262b821756295c58d);
    try testing.expect(Fnv1a_128.hash("a") == 0xd228cb696f1a8caf78912b704e4a8964);
    try verify.iterativeApi(Fnv1a_128);
}



---
File: /std/hash/murmur.zig
---

const std = @import("std");
const builtin = @import("builtin");
const testing = std.testing;
const native_endian = builtin.target.cpu.arch.endian();

const default_seed: u32 = 0xc70f6907;

pub const Murmur2_32 = struct {
    const Self = @This();

    pub fn hash(str: []const u8) u32 {
        return @call(.always_inline, Self.hashWithSeed, .{ str, default_seed });
    }

    pub fn hashWithSeed(str: []const u8, seed: u32) u32 {
        const m: u32 = 0x5bd1e995;
        const len: u32 = @truncate(str.len);
        var h1: u32 = seed ^ len;
        for (@as([*]align(1) const u32, @ptrCast(str.ptr))[0..(len >> 2)]) |v| {
            var k1: u32 = v;
            if (native_endian == .big)
                k1 = @byteSwap(k1);
            k1 *%= m;
            k1 ^= k1 >> 24;
            k1 *%= m;
            h1 *%= m;
            h1 ^= k1;
        }
        const offset = len & 0xfffffffc;
        const rest = len & 3;
        if (rest >= 3) {
            h1 ^= @as(u32, @intCast(str[offset + 2])) << 16;
        }
        if (rest >= 2) {
            h1 ^= @as(u32, @intCast(str[offset + 1])) << 8;
        }
        if (rest >= 1) {
            h1 ^= @as(u32, @intCast(str[offset + 0]));
            h1 *%= m;
        }
        h1 ^= h1 >> 13;
        h1 *%= m;
        h1 ^= h1 >> 15;
        return h1;
    }

    pub fn hashUint32(v: u32) u32 {
        return @call(.always_inline, Self.hashUint32WithSeed, .{ v, default_seed });
    }

    pub fn hashUint32WithSeed(v: u32, seed: u32) u32 {
        const m: u32 = 0x5bd1e995;
        const len: u32 = 4;
        var h1: u32 = seed ^ len;
        var k1: u32 = undefined;
        k1 = v *% m;
        k1 ^= k1 >> 24;
        k1 *%= m;
        h1 *%= m;
        h1 ^= k1;
        h1 ^= h1 >> 13;
        h1 *%= m;
        h1 ^= h1 >> 15;
        return h1;
    }

    pub fn hashUint64(v: u64) u32 {
        return @call(.always_inline, Self.hashUint64WithSeed, .{ v, default_seed });
    }

    pub fn hashUint64WithSeed(v: u64, seed: u32) u32 {
        const m: u32 = 0x5bd1e995;
        const len: u32 = 8;
        var h1: u32 = seed ^ len;
        var k1: u32 = undefined;
        k1 = @as(u32, @truncate(v)) *% m;
        k1 ^= k1 >> 24;
        k1 *%= m;
        h1 *%= m;
        h1 ^= k1;
        k1 = @as(u32, @truncate(v >> 32)) *% m;
        k1 ^= k1 >> 24;
        k1 *%= m;
        h1 *%= m;
        h1 ^= k1;
        h1 ^= h1 >> 13;
        h1 *%= m;
        h1 ^= h1 >> 15;
        return h1;
    }
};

pub const Murmur2_64 = struct {
    const Self = @This();

    pub fn hash(str: []const u8) u64 {
        return @call(.always_inline, Self.hashWithSeed, .{ str, default_seed });
    }

    pub fn hashWithSeed(str: []const u8, seed: u64) u64 {
        const m: u64 = 0xc6a4a7935bd1e995;
        var h1: u64 = seed ^ (@as(u64, str.len) *% m);
        for (@as([*]align(1) const u64, @ptrCast(str.ptr))[0 .. str.len / 8]) |v| {
            var k1: u64 = v;
            if (native_endian == .big)
                k1 = @byteSwap(k1);
            k1 *%= m;
            k1 ^= k1 >> 47;
            k1 *%= m;
            h1 ^= k1;
            h1 *%= m;
        }
        const rest = str.len & 7;
        const offset = str.len - rest;
        if (rest > 0) {
            var k1: u64 = 0;
            @memcpy(@as([*]u8, @ptrCast(&k1))[0..rest], str[offset..]);
            if (native_endian == .big)
                k1 = @byteSwap(k1);
            h1 ^= k1;
            h1 *%= m;
        }
        h1 ^= h1 >> 47;
        h1 *%= m;
        h1 ^= h1 >> 47;
        return h1;
    }

    pub fn hashUint32(v: u32) u64 {
        return @call(.always_inline, Self.hashUint32WithSeed, .{ v, default_seed });
    }

    pub fn hashUint32WithSeed(v: u32, seed: u64) u64 {
        const m: u64 = 0xc6a4a7935bd1e995;
        const len: u64 = 4;
        var h1: u64 = seed ^ (len *% m);
        const k1: u64 = v;
        h1 ^= k1;
        h1 *%= m;
        h1 ^= h1 >> 47;
        h1 *%= m;
        h1 ^= h1 >> 47;
        return h1;
    }

    pub fn hashUint64(v: u64) u64 {
        return @call(.always_inline, Self.hashUint64WithSeed, .{ v, default_seed });
    }

    pub fn hashUint64WithSeed(v: u64, seed: u64) u64 {
        const m: u64 = 0xc6a4a7935bd1e995;
        const len: u64 = 8;
        var h1: u64 = seed ^ (len *% m);
        var k1: u64 = undefined;
        k1 = v *% m;
        k1 ^= k1 >> 47;
        k1 *%= m;
        h1 ^= k1;
        h1 *%= m;
        h1 ^= h1 >> 47;
        h1 *%= m;
        h1 ^= h1 >> 47;
        return h1;
    }
};

pub const Murmur3_32 = struct {
    const Self = @This();

    fn rotl32(x: u32, comptime r: u32) u32 {
        return (x << r) | (x >> (32 - r));
    }

    pub fn hash(str: []const u8) u32 {
        return @call(.always_inline, Self.hashWithSeed, .{ str, default_seed });
    }

    pub fn hashWithSeed(str: []const u8, seed: u32) u32 {
        const c1: u32 = 0xcc9e2d51;
        const c2: u32 = 0x1b873593;
        const len: u32 = @truncate(str.len);
        var h1: u32 = seed;
        for (@as([*]align(1) const u32, @ptrCast(str.ptr))[0..(len >> 2)]) |v| {
            var k1: u32 = v;
            if (native_endian == .big)
                k1 = @byteSwap(k1);
            k1 *%= c1;
            k1 = rotl32(k1, 15);
            k1 *%= c2;
            h1 ^= k1;
            h1 = rotl32(h1, 13);
            h1 *%= 5;
            h1 +%= 0xe6546b64;
        }
        {
            var k1: u32 = 0;
            const offset = len & 0xfffffffc;
            const rest = len & 3;
            if (rest == 3) {
                k1 ^= @as(u32, @intCast(str[offset + 2])) << 16;
            }
            if (rest >= 2) {
                k1 ^= @as(u32, @intCast(str[offset + 1])) << 8;
            }
            if (rest >= 1) {
                k1 ^= @as(u32, @intCast(str[offset + 0]));
                k1 *%= c1;
                k1 = rotl32(k1, 15);
                k1 *%= c2;
                h1 ^= k1;
            }
        }
        h1 ^= len;
        h1 ^= h1 >> 16;
        h1 *%= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *%= 0xc2b2ae35;
        h1 ^= h1 >> 16;
        return h1;
    }

    pub fn hashUint32(v: u32) u32 {
        return @call(.always_inline, Self.hashUint32WithSeed, .{ v, default_seed });
    }

    pub fn hashUint32WithSeed(v: u32, seed: u32) u32 {
        const c1: u32 = 0xcc9e2d51;
        const c2: u32 = 0x1b873593;
        const len: u32 = 4;
        var h1: u32 = seed;
        var k1: u32 = undefined;
        k1 = v *% c1;
        k1 = rotl32(k1, 15);
        k1 *%= c2;
        h1 ^= k1;
        h1 = rotl32(h1, 13);
        h1 *%= 5;
        h1 +%= 0xe6546b64;
        h1 ^= len;
        h1 ^= h1 >> 16;
        h1 *%= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *%= 0xc2b2ae35;
        h1 ^= h1 >> 16;
        return h1;
    }

    pub fn hashUint64(v: u64) u32 {
        return @call(.always_inline, Self.hashUint64WithSeed, .{ v, default_seed });
    }

    pub fn hashUint64WithSeed(v: u64, seed: u32) u32 {
        const c1: u32 = 0xcc9e2d51;
        const c2: u32 = 0x1b873593;
        const len: u32 = 8;
        var h1: u32 = seed;
        var k1: u32 = undefined;
        k1 = @as(u32, @truncate(v)) *% c1;
        k1 = rotl32(k1, 15);
        k1 *%= c2;
        h1 ^= k1;
        h1 = rotl32(h1, 13);
        h1 *%= 5;
        h1 +%= 0xe6546b64;
        k1 = @as(u32, @truncate(v >> 32)) *% c1;
        k1 = rotl32(k1, 15);
        k1 *%= c2;
        h1 ^= k1;
        h1 = rotl32(h1, 13);
        h1 *%= 5;
        h1 +%= 0xe6546b64;
        h1 ^= len;
        h1 ^= h1 >> 16;
        h1 *%= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *%= 0xc2b2ae35;
        h1 ^= h1 >> 16;
        return h1;
    }
};

const verify = @import("verify.zig");

test "murmur2_32" {
    const v0: u32 = 0x12345678;
    const v1: u64 = 0x1234567812345678;
    const v0le: u32, const v1le: u64 = switch (native_endian) {
        .little => .{ v0, v1 },
        .big => .{ @byteSwap(v0), @byteSwap(v1) },
    };
    try testing.expectEqual(Murmur2_32.hash(@as([*]const u8, @ptrCast(&v0le))[0..4]), Murmur2_32.hashUint32(v0));
    try testing.expectEqual(Murmur2_32.hash(@as([*]const u8, @ptrCast(&v1le))[0..8]), Murmur2_32.hashUint64(v1));
}

test "murmur2_32 smhasher" {
    const Test = struct {
        fn do() !void {
            try testing.expectEqual(verify.smhasher(Murmur2_32.hashWithSeed), 0x27864C1E);
        }
    };
    try Test.do();
    @setEvalBranchQuota(30000);
    try comptime Test.do();
}

test "murmur2_64" {
    const v0: u32 = 0x12345678;
    const v1: u64 = 0x1234567812345678;
    const v0le: u32, const v1le: u64 = switch (native_endian) {
        .little => .{ v0, v1 },
        .big => .{ @byteSwap(v0), @byteSwap(v1) },
    };
    try testing.expectEqual(Murmur2_64.hash(@as([*]const u8, @ptrCast(&v0le))[0..4]), Murmur2_64.hashUint32(v0));
    try testing.expectEqual(Murmur2_64.hash(@as([*]const u8, @ptrCast(&v1le))[0..8]), Murmur2_64.hashUint64(v1));
}

test "mumur2_64 smhasher" {
    const Test = struct {
        fn do() !void {
            try std.testing.expectEqual(verify.smhasher(Murmur2_64.hashWithSeed), 0x1F0D3804);
        }
    };
    try Test.do();
    @setEvalBranchQuota(30000);
    try comptime Test.do();
}

test "murmur3_32" {
    const v0: u32 = 0x12345678;
    const v1: u64 = 0x1234567812345678;
    const v0le: u32, const v1le: u64 = switch (native_endian) {
        .little => .{ v0, v1 },
        .big => .{ @byteSwap(v0), @byteSwap(v1) },
    };
    try testing.expectEqual(Murmur3_32.hash(@as([*]const u8, @ptrCast(&v0le))[0..4]), Murmur3_32.hashUint32(v0));
    try testing.expectEqual(Murmur3_32.hash(@as([*]const u8, @ptrCast(&v1le))[0..8]), Murmur3_32.hashUint64(v1));
}

test "mumur3_32 smhasher" {
    const Test = struct {
        fn do() !void {
            try std.testing.expectEqual(verify.smhasher(Murmur3_32.hashWithSeed), 0xB0F57EE3);
        }
    };
    try Test.do();
    @setEvalBranchQuota(30000);
    try comptime Test.do();
}



---
File: /std/hash/verify.zig
---

const std = @import("std");

fn hashMaybeSeed(comptime hash_fn: anytype, seed: anytype, buf: []const u8) @typeInfo(@TypeOf(hash_fn)).@"fn".return_type.? {
    const HashFn = @typeInfo(@TypeOf(hash_fn)).@"fn";
    if (HashFn.params.len > 1) {
        if (@typeInfo(HashFn.params[0].type.?) == .int) {
            return hash_fn(@intCast(seed), buf);
        } else {
            return hash_fn(buf, @intCast(seed));
        }
    } else {
        return hash_fn(buf);
    }
}

fn initMaybeSeed(comptime Hash: anytype, seed: anytype) Hash {
    const HashFn = @typeInfo(@TypeOf(Hash.init)).@"fn";
    if (HashFn.params.len == 1) {
        return Hash.init(@intCast(seed));
    } else {
        return Hash.init();
    }
}

// Returns a verification code, the same as used by SMHasher.
//
// Hash keys of the form {0}, {0,1}, {0,1,2}... up to N=255, using 256-N as seed.
// First four-bytes of the hash, interpreted as little-endian is the verification code.
pub fn smhasher(comptime hash_fn: anytype) u32 {
    const HashFnTy = @typeInfo(@TypeOf(hash_fn)).@"fn";
    const HashResult = HashFnTy.return_type.?;
    const hash_size = @sizeOf(HashResult);

    var buf: [256]u8 = undefined;
    var buf_all: [256 * hash_size]u8 = undefined;

    for (0..256) |i| {
        buf[i] = @intCast(i);
        const h = hashMaybeSeed(hash_fn, 256 - i, buf[0..i]);
        std.mem.writeInt(HashResult, buf_all[i * hash_size ..][0..hash_size], h, .little);
    }

    return @truncate(hashMaybeSeed(hash_fn, 0, buf_all[0..]));
}

pub fn iterativeApi(comptime Hash: anytype) !void {
    // Sum(1..32) = 528
    var buf: [528]u8 = @splat(0);
    var len: usize = 0;
    const seed = 0;

    var hasher = initMaybeSeed(Hash, seed);
    for (1..32) |i| {
        const r = hashMaybeSeed(Hash.hash, seed, buf[0 .. len + i]);
        hasher.update(buf[len..][0..i]);
        const f1 = hasher.final();
        const f2 = hasher.final();
        if (f1 != f2) return error.IterativeHashWasNotIdempotent;
        if (f1 != r) return error.IterativeHashDidNotMatchDirect;
        len += i;
    }
}



---
File: /std/hash/wyhash.zig
---

const std = @import("std");

pub const Wyhash = struct {
    const secret = [_]u64{
        0xa0761d6478bd642f,
        0xe7037ed1a0b428db,
        0x8ebc6af09c88c6e3,
        0x589965cc75374cc3,
    };

    a: u64,
    b: u64,
    state: [3]u64,
    total_len: usize,

    buf: [48]u8,
    buf_len: usize,

    pub fn init(seed: u64) Wyhash {
        var self = Wyhash{
            .a = undefined,
            .b = undefined,
            .state = undefined,
            .total_len = 0,
            .buf = undefined,
            .buf_len = 0,
        };

        self.state[0] = seed ^ mix(seed ^ secret[0], secret[1]);
        self.state[1] = self.state[0];
        self.state[2] = self.state[0];
        return self;
    }

    // This is subtly different from other hash function update calls. Wyhash requires the last
    // full 48-byte block to be run through final1 if is exactly aligned to 48-bytes.
    pub fn update(self: *Wyhash, input: []const u8) void {
        self.total_len += input.len;

        if (input.len <= 48 - self.buf_len) {
            @memcpy(self.buf[self.buf_len..][0..input.len], input);
            self.buf_len += input.len;
            return;
        }

        var i: usize = 0;

        if (self.buf_len > 0) {
            i = 48 - self.buf_len;
            @memcpy(self.buf[self.buf_len..][0..i], input[0..i]);
            self.round(&self.buf);
            self.buf_len = 0;
        }

        while (i + 48 < input.len) : (i += 48) {
            self.round(input[i..][0..48]);
        }

        const remaining_bytes = input[i..];
        if (remaining_bytes.len < 16 and i >= 48) {
            const rem = 16 - remaining_bytes.len;
            @memcpy(self.buf[self.buf.len - rem ..], input[i - rem .. i]);
        }
        @memcpy(self.buf[0..remaining_bytes.len], remaining_bytes);
        self.buf_len = remaining_bytes.len;
    }

    pub fn final(self: *Wyhash) u64 {
        var input: []const u8 = self.buf[0..self.buf_len];
        var newSelf = self.shallowCopy(); // ensure idempotency

        if (self.total_len <= 16) {
            newSelf.smallKey(input);
        } else {
            var offset: usize = 0;
            var scratch: [16]u8 = undefined;
            if (self.buf_len < 16) {
                const rem = 16 - self.buf_len;
                @memcpy(scratch[0..rem], self.buf[self.buf.len - rem ..][0..rem]);
                @memcpy(scratch[rem..][0..self.buf_len], self.buf[0..self.buf_len]);

                // Same as input but with additional bytes preceding start in case of a short buffer
                input = &scratch;
                offset = rem;
            }

            newSelf.final0();
            newSelf.final1(input, offset);
        }

        return newSelf.final2();
    }

    // Copies the core wyhash state but not any internal buffers.
    inline fn shallowCopy(self: *Wyhash) Wyhash {
        return .{
            .a = self.a,
            .b = self.b,
            .state = self.state,
            .total_len = self.total_len,
            .buf = undefined,
            .buf_len = undefined,
        };
    }

    inline fn smallKey(self: *Wyhash, input: []const u8) void {
        std.debug.assert(input.len <= 16);

        if (input.len >= 4) {
            const end = input.len - 4;
            const quarter = (input.len >> 3) << 2;
            self.a = (read(4, input[0..]) << 32) | read(4, input[quarter..]);
            self.b = (read(4, input[end..]) << 32) | read(4, input[end - quarter ..]);
        } else if (input.len > 0) {
            self.a = (@as(u64, input[0]) << 16) | (@as(u64, input[input.len >> 1]) << 8) | input[input.len - 1];
            self.b = 0;
        } else {
            self.a = 0;
            self.b = 0;
        }
    }

    inline fn round(self: *Wyhash, input: *const [48]u8) void {
        inline for (0..3) |i| {
            const a = read(8, input[8 * (2 * i) ..]);
            const b = read(8, input[8 * (2 * i + 1) ..]);
            self.state[i] = mix(a ^ secret[i + 1], b ^ self.state[i]);
        }
    }

    inline fn read(comptime bytes: usize, data: []const u8) u64 {
        std.debug.assert(bytes <= 8);
        const T = std.meta.Int(.unsigned, 8 * bytes);
        return @as(u64, std.mem.readInt(T, data[0..bytes], .little));
    }

    inline fn mum(a: *u64, b: *u64) void {
        const x = @as(u128, a.*) *% b.*;
        a.* = @as(u64, @truncate(x));
        b.* = @as(u64, @truncate(x >> 64));
    }

    inline fn mix(a_: u64, b_: u64) u64 {
        var a = a_;
        var b = b_;
        mum(&a, &b);
        return a ^ b;
    }

    inline fn final0(self: *Wyhash) void {
        self.state[0] ^= self.state[1] ^ self.state[2];
    }

    // input_lb must be at least 16-bytes long (in shorter key cases the smallKey function will be
    // used instead). We use an index into a slice to for comptime processing as opposed to if we
    // used pointers.
    inline fn final1(self: *Wyhash, input_lb: []const u8, start_pos: usize) void {
        std.debug.assert(input_lb.len >= 16);
        std.debug.assert(input_lb.len - start_pos <= 48);
        const input = input_lb[start_pos..];

        var i: usize = 0;
        while (i + 16 < input.len) : (i += 16) {
            self.state[0] = mix(read(8, input[i..]) ^ secret[1], read(8, input[i + 8 ..]) ^ self.state[0]);
        }

        self.a = read(8, input_lb[input_lb.len - 16 ..][0..8]);
        self.b = read(8, input_lb[input_lb.len - 8 ..][0..8]);
    }

    inline fn final2(self: *Wyhash) u64 {
        self.a ^= secret[1];
        self.b ^= self.state[0];
        mum(&self.a, &self.b);
        return mix(self.a ^ secret[0] ^ self.total_len, self.b ^ secret[1]);
    }

    pub fn hash(seed: u64, input: []const u8) u64 {
        var self = Wyhash.init(seed);

        if (input.len <= 16) {
            self.smallKey(input);
        } else {
            var i: usize = 0;
            if (input.len >= 48) {
                while (i + 48 < input.len) : (i += 48) {
                    self.round(input[i..][0..48]);
                }
                self.final0();
            }
            self.final1(input, i);
        }

        self.total_len = input.len;
        return self.final2();
    }
};

const verify = @import("verify.zig");
const expectEqual = std.testing.expectEqual;

const TestVector = struct {
    expected: u64,
    seed: u64,
    input: []const u8,
};

// Run https://github.com/wangyi-fudan/wyhash/blob/77e50f267fbc7b8e2d09f2d455219adb70ad4749/test_vector.cpp directly.
const vectors = [_]TestVector{
    .{ .seed = 0, .expected = 0x409638ee2bde459, .input = "" },
    .{ .seed = 1, .expected = 0xa841
```
