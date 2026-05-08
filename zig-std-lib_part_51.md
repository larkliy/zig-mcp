```
2d091b5fe0a9, .input = "a" },
    .{ .seed = 2, .expected = 0x32dd92e4b2915153, .input = "abc" },
    .{ .seed = 3, .expected = 0x8619124089a3a16b, .input = "message digest" },
    .{ .seed = 4, .expected = 0x7a43afb61d7f5f40, .input = "abcdefghijklmnopqrstuvwxyz" },
    .{ .seed = 5, .expected = 0xff42329b90e50d58, .input = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" },
    .{ .seed = 6, .expected = 0xc39cab13b115aad3, .input = "12345678901234567890123456789012345678901234567890123456789012345678901234567890" },
};

test "test vectors" {
    for (vectors) |e| {
        try expectEqual(e.expected, Wyhash.hash(e.seed, e.input));
    }
}

test "test vectors at comptime" {
    comptime {
        for (vectors) |e| {
            try expectEqual(e.expected, Wyhash.hash(e.seed, e.input));
        }
    }
}

test "smhasher" {
    const Test = struct {
        fn do() !void {
            try expectEqual(verify.smhasher(Wyhash.hash), 0xBD5E840C);
        }
    };
    try Test.do();
    @setEvalBranchQuota(50000);
    try comptime Test.do();
}

test "iterative api" {
    const Test = struct {
        fn do() !void {
            try verify.iterativeApi(Wyhash);
        }
    };
    try Test.do();
    @setEvalBranchQuota(50000);
    try comptime Test.do();
}

test "iterative maintains last sixteen" {
    const input = "Z" ** 48 ++ "01234567890abcdefg";
    const seed = 0;

    for (0..17) |i| {
        const payload = input[0 .. input.len - i];
        const non_iterative_hash = Wyhash.hash(seed, payload);

        var wh = Wyhash.init(seed);
        wh.update(payload);
        const iterative_hash = wh.final();

        try expectEqual(non_iterative_hash, iterative_hash);
    }
}



---
File: /std/hash/xxhash.zig
---

const std = @import("std");
const builtin = @import("builtin");
const mem = std.mem;
const expectEqual = std.testing.expectEqual;
const native_endian = builtin.cpu.arch.endian();

const rotl = std.math.rotl;

pub const XxHash64 = struct {
    accumulator: Accumulator,
    seed: u64,
    buf: [32]u8,
    buf_len: usize,
    byte_count: usize,

    const prime_1 = 0x9E3779B185EBCA87; // 0b1001111000110111011110011011000110000101111010111100101010000111
    const prime_2 = 0xC2B2AE3D27D4EB4F; // 0b1100001010110010101011100011110100100111110101001110101101001111
    const prime_3 = 0x165667B19E3779F9; // 0b0001011001010110011001111011000110011110001101110111100111111001
    const prime_4 = 0x85EBCA77C2B2AE63; // 0b1000010111101011110010100111011111000010101100101010111001100011
    const prime_5 = 0x27D4EB2F165667C5; // 0b0010011111010100111010110010111100010110010101100110011111000101

    const Accumulator = struct {
        acc1: u64,
        acc2: u64,
        acc3: u64,
        acc4: u64,

        fn init(seed: u64) Accumulator {
            return .{
                .acc1 = seed +% prime_1 +% prime_2,
                .acc2 = seed +% prime_2,
                .acc3 = seed,
                .acc4 = seed -% prime_1,
            };
        }

        fn updateEmpty(self: *Accumulator, input: anytype, comptime unroll_count: usize) usize {
            var i: usize = 0;

            if (unroll_count > 0) {
                const unrolled_bytes = unroll_count * 32;
                while (i + unrolled_bytes <= input.len) : (i += unrolled_bytes) {
                    inline for (0..unroll_count) |j| {
                        self.processStripe(input[i + j * 32 ..][0..32]);
                    }
                }
            }

            while (i + 32 <= input.len) : (i += 32) {
                self.processStripe(input[i..][0..32]);
            }

            return i;
        }

        fn processStripe(self: *Accumulator, buf: *const [32]u8) void {
            self.acc1 = round(self.acc1, mem.readInt(u64, buf[0..8], .little));
            self.acc2 = round(self.acc2, mem.readInt(u64, buf[8..16], .little));
            self.acc3 = round(self.acc3, mem.readInt(u64, buf[16..24], .little));
            self.acc4 = round(self.acc4, mem.readInt(u64, buf[24..32], .little));
        }

        fn merge(self: Accumulator) u64 {
            var acc = rotl(u64, self.acc1, 1) +% rotl(u64, self.acc2, 7) +%
                rotl(u64, self.acc3, 12) +% rotl(u64, self.acc4, 18);
            acc = mergeAccumulator(acc, self.acc1);
            acc = mergeAccumulator(acc, self.acc2);
            acc = mergeAccumulator(acc, self.acc3);
            acc = mergeAccumulator(acc, self.acc4);
            return acc;
        }

        fn mergeAccumulator(acc: u64, other: u64) u64 {
            const a = acc ^ round(0, other);
            const b = a *% prime_1;
            return b +% prime_4;
        }
    };

    fn finalize(
        unfinished: u64,
        byte_count: usize,
        partial: anytype,
    ) u64 {
        std.debug.assert(partial.len < 32);
        var acc = unfinished +% @as(u64, byte_count) +% @as(u64, partial.len);

        switch (partial.len) {
            inline 0, 1, 2, 3 => |count| {
                inline for (0..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 4, 5, 6, 7 => |count| {
                acc = finalize4(acc, partial[0..4]);
                inline for (4..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 8, 9, 10, 11 => |count| {
                acc = finalize8(acc, partial[0..8]);
                inline for (8..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 12, 13, 14, 15 => |count| {
                acc = finalize8(acc, partial[0..8]);
                acc = finalize4(acc, partial[8..12]);
                inline for (12..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 16, 17, 18, 19 => |count| {
                acc = finalize8(acc, partial[0..8]);
                acc = finalize8(acc, partial[8..16]);
                inline for (16..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 20, 21, 22, 23 => |count| {
                acc = finalize8(acc, partial[0..8]);
                acc = finalize8(acc, partial[8..16]);
                acc = finalize4(acc, partial[16..20]);
                inline for (20..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 24, 25, 26, 27 => |count| {
                acc = finalize8(acc, partial[0..8]);
                acc = finalize8(acc, partial[8..16]);
                acc = finalize8(acc, partial[16..24]);
                inline for (24..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 28, 29, 30, 31 => |count| {
                acc = finalize8(acc, partial[0..8]);
                acc = finalize8(acc, partial[8..16]);
                acc = finalize8(acc, partial[16..24]);
                acc = finalize4(acc, partial[24..28]);
                inline for (28..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            else => unreachable,
        }
    }

    fn finalize8(v: u64, bytes: *const [8]u8) u64 {
        var acc = v;
        const lane = mem.readInt(u64, bytes, .little);
        acc ^= round(0, lane);
        acc = rotl(u64, acc, 27) *% prime_1;
        acc +%= prime_4;
        return acc;
    }

    fn finalize4(v: u64, bytes: *const [4]u8) u64 {
        var acc = v;
        const lane = @as(u64, mem.readInt(u32, bytes, .little));
        acc ^= lane *% prime_1;
        acc = rotl(u64, acc, 23) *% prime_2;
        acc +%= prime_3;
        return acc;
    }

    fn finalize1(v: u64, byte: u8) u64 {
        var acc = v;
        const lane = @as(u64, byte);
        acc ^= lane *% prime_5;
        acc = rotl(u64, acc, 11) *% prime_1;
        return acc;
    }

    fn avalanche(value: u64) u64 {
        var result = value ^ (value >> 33);
        result *%= prime_2;
        result ^= result >> 29;
        result *%= prime_3;
        result ^= result >> 32;

        return result;
    }

    pub fn init(seed: u64) XxHash64 {
        return XxHash64{
            .accumulator = Accumulator.init(seed),
            .seed = seed,
            .buf = undefined,
            .buf_len = 0,
            .byte_count = 0,
        };
    }

    pub fn update(self: *XxHash64, input: anytype) void {
        if (input.len < 32 - self.buf_len) {
            @memcpy(self.buf[self.buf_len..][0..input.len], input);
            self.buf_len += input.len;
            return;
        }

        var i: usize = 0;

        if (self.buf_len > 0) {
            i = 32 - self.buf_len;
            @memcpy(self.buf[self.buf_len..][0..i], input[0..i]);
            self.accumulator.processStripe(&self.buf);
            self.byte_count += self.buf_len;
        }

        i += self.accumulator.updateEmpty(input[i..], 32);
        self.byte_count += i;

        const remaining_bytes = input[i..];
        @memcpy(self.buf[0..remaining_bytes.len], remaining_bytes);
        self.buf_len = remaining_bytes.len;
    }

    fn round(acc: u64, lane: u64) u64 {
        const a = acc +% (lane *% prime_2);
        const b = rotl(u64, a, 31);
        return b *% prime_1;
    }

    pub fn final(self: *XxHash64) u64 {
        const unfinished = if (self.byte_count < 32)
            self.seed +% prime_5
        else
            self.accumulator.merge();

        return finalize(unfinished, self.byte_count, self.buf[0..self.buf_len]);
    }

    const Size = enum {
        small,
        large,
        unknown,
    };

    pub fn hash(seed: u64, input: anytype) u64 {
        if (input.len < 32) {
            return finalize(seed +% prime_5, 0, input);
        } else {
            var hasher = Accumulator.init(seed);
            const i = hasher.updateEmpty(input, 0);
            return finalize(hasher.merge(), i, input[i..]);
        }
    }
};

pub const XxHash32 = struct {
    accumulator: Accumulator,
    seed: u32,
    buf: [16]u8,
    buf_len: usize,
    byte_count: usize,

    const prime_1 = 0x9E3779B1; // 0b10011110001101110111100110110001
    const prime_2 = 0x85EBCA77; // 0b10000101111010111100101001110111
    const prime_3 = 0xC2B2AE3D; // 0b11000010101100101010111000111101
    const prime_4 = 0x27D4EB2F; // 0b00100111110101001110101100101111
    const prime_5 = 0x165667B1; // 0b00010110010101100110011110110001

    const Accumulator = struct {
        acc1: u32,
        acc2: u32,
        acc3: u32,
        acc4: u32,

        fn init(seed: u32) Accumulator {
            return .{
                .acc1 = seed +% prime_1 +% prime_2,
                .acc2 = seed +% prime_2,
                .acc3 = seed,
                .acc4 = seed -% prime_1,
            };
        }

        fn updateEmpty(self: *Accumulator, input: anytype, comptime unroll_count: usize) usize {
            var i: usize = 0;

            if (unroll_count > 0) {
                const unrolled_bytes = unroll_count * 16;
                while (i + unrolled_bytes <= input.len) : (i += unrolled_bytes) {
                    inline for (0..unroll_count) |j| {
                        self.processStripe(input[i + j * 16 ..][0..16]);
                    }
                }
            }

            while (i + 16 <= input.len) : (i += 16) {
                self.processStripe(input[i..][0..16]);
            }

            return i;
        }

        fn processStripe(self: *Accumulator, buf: *const [16]u8) void {
            self.acc1 = round(self.acc1, mem.readInt(u32, buf[0..4], .little));
            self.acc2 = round(self.acc2, mem.readInt(u32, buf[4..8], .little));
            self.acc3 = round(self.acc3, mem.readInt(u32, buf[8..12], .little));
            self.acc4 = round(self.acc4, mem.readInt(u32, buf[12..16], .little));
        }

        fn merge(self: Accumulator) u32 {
            return rotl(u32, self.acc1, 1) +% rotl(u32, self.acc2, 7) +%
                rotl(u32, self.acc3, 12) +% rotl(u32, self.acc4, 18);
        }
    };

    pub fn init(seed: u32) XxHash32 {
        return XxHash32{
            .accumulator = Accumulator.init(seed),
            .seed = seed,
            .buf = undefined,
            .buf_len = 0,
            .byte_count = 0,
        };
    }

    pub fn update(self: *XxHash32, input: []const u8) void {
        if (input.len < 16 - self.buf_len) {
            @memcpy(self.buf[self.buf_len..][0..input.len], input);
            self.buf_len += input.len;
            return;
        }

        var i: usize = 0;

        if (self.buf_len > 0) {
            i = 16 - self.buf_len;
            @memcpy(self.buf[self.buf_len..][0..i], input[0..i]);
            self.accumulator.processStripe(&self.buf);
            self.byte_count += self.buf_len;
            self.buf_len = 0;
        }

        i += self.accumulator.updateEmpty(input[i..], 16);
        self.byte_count += i;

        const remaining_bytes = input[i..];
        @memcpy(self.buf[0..remaining_bytes.len], remaining_bytes);
        self.buf_len = remaining_bytes.len;
    }

    fn round(acc: u32, lane: u32) u32 {
        const a = acc +% (lane *% prime_2);
        const b = rotl(u32, a, 13);
        return b *% prime_1;
    }

    pub fn final(self: *XxHash32) u32 {
        const unfinished = if (self.byte_count < 16)
            self.seed +% prime_5
        else
            self.accumulator.merge();

        return finalize(unfinished, self.byte_count, self.buf[0..self.buf_len]);
    }

    fn finalize(unfinished: u32, byte_count: usize, partial: anytype) u32 {
        std.debug.assert(partial.len < 16);
        var acc = unfinished +% @as(u32, @intCast(byte_count)) +% @as(u32, @intCast(partial.len));

        switch (partial.len) {
            inline 0, 1, 2, 3 => |count| {
                inline for (0..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 4, 5, 6, 7 => |count| {
                acc = finalize4(acc, partial[0..4]);
                inline for (4..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 8, 9, 10, 11 => |count| {
                acc = finalize4(acc, partial[0..4]);
                acc = finalize4(acc, partial[4..8]);
                inline for (8..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            inline 12, 13, 14, 15 => |count| {
                acc = finalize4(acc, partial[0..4]);
                acc = finalize4(acc, partial[4..8]);
                acc = finalize4(acc, partial[8..12]);
                inline for (12..count) |i| acc = finalize1(acc, partial[i]);
                return avalanche(acc);
            },
            else => unreachable,
        }

        return avalanche(acc);
    }

    fn finalize4(v: u32, bytes: *const [4]u8) u32 {
        var acc = v;
        const lane = mem.readInt(u32, bytes, .little);
        acc +%= lane *% prime_3;
        acc = rotl(u32, acc, 17) *% prime_4;
        return acc;
    }

    fn finalize1(v: u32, byte: u8) u32 {
        var acc = v;
        const lane = @as(u32, byte);
        acc +%= lane *% prime_5;
        acc = rotl(u32, acc, 11) *% prime_1;
        return acc;
    }

    fn avalanche(value: u32) u32 {
        var acc = value ^ value >> 15;
        acc *%= prime_2;
        acc ^= acc >> 13;
        acc *%= prime_3;
        acc ^= acc >> 16;

        return acc;
    }

    pub fn hash(seed: u32, input: anytype) u32 {
        if (input.len < 16) {
            return finalize(seed +% prime_5, 0, input);
        } else {
            var hasher = Accumulator.init(seed);
            const i = hasher.updateEmpty(input, 0);
            return finalize(hasher.merge(), i, input[i..]);
        }
    }
};

pub const XxHash3 = struct {
    const Block = @Vector(8, u64);
    const default_secret: [192]u8 = .{
        0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, 0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
        0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, 0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
        0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, 0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
        0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, 0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
        0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, 0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
        0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, 0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
        0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, 0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
        0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, 0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,
        0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, 0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
        0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, 0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
        0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, 0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
        0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, 0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e,
    };

    const prime_mx1 = 0x165667919E3779F9;
    const prime_mx2 = 0x9FB21C651E98DF25;

    inline fn avalanche(mode: union(enum) { h3, h64, rrmxmx: u64 }, x0: u64) u64 {
        switch (mode) {
            .h3 => {
                const x1 = (x0 ^ (x0 >> 37)) *% prime_mx1;
                return x1 ^ (x1 >> 32);
            },
            .h64 => {
                const x1 = (x0 ^ (x0 >> 33)) *% XxHash64.prime_2;
                const x2 = (x1 ^ (x1 >> 29)) *% XxHash64.prime_3;
                return x2 ^ (x2 >> 32);
            },
            .rrmxmx => |len| {
                const x1 = (x0 ^ rotl(u64, x0, 49) ^ rotl(u64, x0, 24)) *% prime_mx2;
                const x2 = (x1 ^ ((x1 >> 35) +% len)) *% prime_mx2;
                return x2 ^ (x2 >> 28);
            },
        }
    }

    inline fn fold(a: u64, b: u64) u64 {
        const wide: [2]u64 = @bitCast(@as(u128, a) *% b);
        return wide[0] ^ wide[1];
    }

    inline fn swap(x: anytype) @TypeOf(x) {
        return if (native_endian == .big) @byteSwap(x) else x;
    }

    inline fn disableAutoVectorization(x: anytype) void {
        if (!@inComptime()) asm volatile (""
            :
            : [x] "r" (x),
        );
    }

    inline fn mix16(seed: u64, input: []const u8, secret: []const u8) u64 {
        const blk: [4]u64 = @bitCast([_][16]u8{ input[0..16].*, secret[0..16].* });
        disableAutoVectorization(seed);

        return fold(
            swap(blk[0]) ^ (swap(blk[2]) +% seed),
            swap(blk[1]) ^ (swap(blk[3]) -% seed),
        );
    }

    const Accumulator = extern struct {
        consumed: usize = 0,
        seed: u64,
        secret: [192]u8 = undefined,
        state: Block = Block{
            XxHash32.prime_3,
            XxHash64.prime_1,
            XxHash64.prime_2,
            XxHash64.prime_3,
            XxHash64.prime_4,
            XxHash32.prime_2,
            XxHash64.prime_5,
            XxHash32.prime_1,
        },

        inline fn init(seed: u64) Accumulator {
            var self = Accumulator{ .seed = seed };
            for (
                std.mem.bytesAsSlice(Block, &self.secret),
                std.mem.bytesAsSlice(Block, &default_secret),
            ) |*dst, src| {
                dst.* = swap(swap(src) +% Block{
                    seed, @as(u64, 0) -% seed,
                    seed, @as(u64, 0) -% seed,
                    seed, @as(u64, 0) -% seed,
                    seed, @as(u64, 0) -% seed,
                });
            }
            return self;
        }

        inline fn round(
            noalias state: *Block,
            noalias input_block: *align(1) const Block,
            noalias secret_block: *align(1) const Block,
        ) void {
            const data = swap(input_block.*);
            const mixed = data ^ swap(secret_block.*);
            state.* +%= (mixed & @as(Block, @splat(0xffffffff))) *% (mixed >> @splat(32));
            state.* +%= @shuffle(u64, data, undefined, [_]i32{ 1, 0, 3, 2, 5, 4, 7, 6 });
        }

        fn accumulate(noalias self: *Accumulator, blocks: []align(1) const Block) void {
            const secret = std.mem.bytesAsSlice(u64, self.secret[self.consumed * 8 ..]);
            for (blocks, secret[0..blocks.len]) |*input_block, *secret_block| {
                @prefetch(@as([*]const u8, @ptrCast(input_block)) + 320, .{});
                round(&self.state, input_block, @ptrCast(secret_block));
            }
        }

        fn scramble(self: *Accumulator) void {
            const secret_block: Block = @bitCast(self.secret[192 - @sizeOf(Block) .. 192].*);
            self.state ^= self.state >> @splat(47);
            self.state ^= swap(secret_block);
            self.state *%= @as(Block, @splat(XxHash32.prime_1));
        }

        fn consume(noalias self: *Accumulator, input_blocks: []align(1) const Block) void {
            const blocks_per_scramble = 1024 / @sizeOf(Block);
            std.debug.assert(self.consumed <= blocks_per_scramble);

            var blocks = input_blocks;
            var blocks_until_scramble = blocks_per_scramble - self.consumed;
            while (blocks.len >= blocks_until_scramble) {
                self.accumulate(blocks[0..blocks_until_scramble]);
                self.scramble();

                self.consumed = 0;
                blocks = blocks[blocks_until_scramble..];
                blocks_until_scramble = blocks_per_scramble;
            }

            self.accumulate(blocks);
            self.consumed += blocks.len;
        }

        fn digest(noalias self: *Accumulator, total_len: u64, noalias last_block: *align(1) const Block) u64 {
            const secret_block = self.secret[192 - @sizeOf(Block) - 7 ..][0..@sizeOf(Block)];
            round(&self.state, last_block, @ptrCast(secret_block));

            const merge_block: Block = @bitCast(self.secret[11 .. 11 + @sizeOf(Block)].*);
            self.state ^= swap(merge_block);

            var result = XxHash64.prime_1 *% total_len;
            inline for (0..4) |i| {
                result +%= fold(self.state[i * 2], self.state[i * 2 + 1]);
            }
            return avalanche(.h3, result);
        }
    };

    // Public API - Oneshot

    pub fn hash(seed: u64, input: anytype) u64 {
        const secret = &default_secret;
        if (input.len > 240) return hashLong(seed, input);
        if (input.len > 128) return hash240(seed, input, secret);
        if (input.len > 16) return hash128(seed, input, secret);
        if (input.len > 8) return hash16(seed, input, secret);
        if (input.len > 3) return hash8(seed, input, secret);
        if (input.len > 0) return hash3(seed, input, secret);

        const flip: [2]u64 = @bitCast(secret[56..72].*);
        const key = swap(flip[0]) ^ swap(flip[1]);
        return avalanche(.h64, seed ^ key);
    }

    fn hash3(seed: u64, input: anytype, noalias secret: *const [192]u8) u64 {
        @branchHint(.unlikely);
        std.debug.assert(input.len > 0 and input.len < 4);

        const flip: [2]u32 = @bitCast(secret[0..8].*);
        const blk: u32 = @bitCast([_]u8{
            input[input.len - 1],
            @truncate(input.len),
            input[0],
            input[input.len / 2],
        });

        const key = @as(u64, swap(flip[0]) ^ swap(flip[1])) +% seed;
        return avalanche(.h64, key ^ swap(blk));
    }

    fn hash8(seed: u64, input: anytype, noalias secret: *const [192]u8) u64 {
        @branchHint(.cold);
        std.debug.assert(input.len >= 4 and input.len <= 8);

        const flip: [2]u64 = @bitCast(secret[8..24].*);
        const blk: [2]u32 = @bitCast([_][4]u8{
            input[0..4].*,
            input[input.len - 4 ..][0..4].*,
        });

        const mixed = seed ^ (@as(u64, @byteSwap(@as(u32, @truncate(seed)))) << 32);
        const key = (swap(flip[0]) ^ swap(flip[1])) -% mixed;
        const combined = (@as(u64, swap(blk[0])) << 32) +% swap(blk[1]);
        return avalanche(.{ .rrmxmx = input.len }, key ^ combined);
    }

    fn hash16(seed: u64, input: anytype, noalias secret: *const [192]u8) u64 {
        @branchHint(.unlikely);
        std.debug.assert(input.len > 8 and input.len <= 16);

        const flip: [4]u64 = @bitCast(secret[24..56].*);
        const blk: [2]u64 = @bitCast([_][8]u8{
            input[0..8].*,
            input[input.len - 8 ..][0..8].*,
        });

        const lo = swap(blk[0]) ^ ((swap(flip[0]) ^ swap(flip[1])) +% seed);
        const hi = swap(blk[1]) ^ ((swap(flip[2]) ^ swap(flip[3])) -% seed);
        const combined = @as(u64, input.len) +% @byteSwap(lo) +% hi +% fold(lo, hi);
        return avalanche(.h3, combined);
    }

    fn hash128(seed: u64, input: anytype, noalias secret: *const [192]u8) u64 {
        @branchHint(.unlikely);
        std.debug.assert(input.len > 16 and input.len <= 128);

        var acc = XxHash64.prime_1 *% @as(u64, input.len);
        inline for (0..4) |i| {
            const in_offset = 48 - (i * 16);
            const scrt_offset = 96 - (i * 32);
            if (input.len > scrt_offset) {
                acc +%= mix16(seed, input[in_offset..], secret[scrt_offset..]);
                acc +%= mix16(seed, input[input.len - (in_offset + 16) ..], secret[scrt_offset + 16 ..]);
            }
        }
        return avalanche(.h3, acc);
    }

    fn hash240(seed: u64, input: anytype, noalias secret: *const [192]u8) u64 {
        @branchHint(.unlikely);
        std.debug.assert(input.len > 128 and input.len <= 240);

        var acc = XxHash64.prime_1 *% @as(u64, input.len);
        inline for (0..8) |i| {
            acc +%= mix16(seed, input[i * 16 ..], secret[i * 16 ..]);
        }

        var acc_end = mix16(seed, input[input.len - 16 ..], secret[136 - 17 ..]);
        for (8..(input.len / 16)) |i| {
            acc_end +%= mix16(seed, input[i * 16 ..], secret[((i - 8) * 16) + 3 ..]);
            disableAutoVectorization(i);
        }

        acc = avalanche(.h3, acc) +% acc_end;
        return avalanche(.h3, acc);
    }

    noinline fn hashLong(seed: u64, input: []const u8) u64 {
        @branchHint(.unlikely);
        std.debug.assert(input.len >= 240);

        const block_count = ((input.len - 1) / @sizeOf(Block)) * @sizeOf(Block);
        const last_block = input[input.len - @sizeOf(Block) ..][0..@sizeOf(Block)];

        var acc = Accumulator.init(seed);
        acc.consume(std.mem.bytesAsSlice(Block, input[0..block_count]));
        return acc.digest(input.len, @ptrCast(last_block));
    }

    // Public API - Streaming

    buffered: usize = 0,
    buffer: [256]u8 = undefined,
    total_len: usize = 0,
    accumulator: Accumulator,

    pub fn init(seed: u64) XxHash3 {
        return .{ .accumulator = Accumulator.init(seed) };
    }

    pub fn update(self: *XxHash3, input: anytype) void {
        self.total_len += input.len;
        std.debug.assert(self.buffered <= self.buffer.len);

        // Copy the input into the buffer if we haven't filled it up yet.
        const remaining = self.buffer.len - self.buffered;
        if (input.len <= remaining) {
            @memcpy(self.buffer[self.buffered..][0..input.len], input);
            self.buffered += input.len;
            return;
        }

        // Input will overflow the buffer. Fill up the buffer with some input and consume it.
        var consumable: []const u8 = input;
        if (self.buffered > 0) {
            @memcpy(self.buffer[self.buffered..], consumable[0..remaining]);
            consumable = consumable[remaining..];

            self.accumulator.consume(std.mem.bytesAsSlice(Block, &self.buffer));
            self.buffered = 0;
        }

        // The input isn't small enough to fit in the buffer. Consume it directly.
        if (consumable.len > self.buffer.len) {
            const block_count = ((consumable.len - 1) / @sizeOf(Block)) * @sizeOf(Block);
            self.accumulator.consume(std.mem.bytesAsSlice(Block, consumable[0..block_count]));
            consumable = consumable[block_count..];

            // In case we consume all remaining input, write the last block to end of the buffer
            // to populate the last_block_copy in final() similar to hashLong()'s last_block.
            @memcpy(
                self.buffer[self.buffer.len - @sizeOf(Block) .. self.buffer.len],
                (consumable.ptr - @sizeOf(Block))[0..@sizeOf(Block)],
            );
        }

        // Copy in any remaining input into the buffer.
        std.debug.assert(consumable.len <= self.buffer.len);
        @memcpy(self.buffer[0..consumable.len], consumable);
        self.buffered = consumable.len;
    }

    pub fn final(self: *XxHash3) u64 {
        std.debug.assert(self.buffered <= self.total_len);
        std.debug.assert(self.buffered <= self.buffer.len);

        // Use Oneshot hashing for smaller sizes as it doesn't use Accumulator like hashLong.
        if (self.total_len <= 240) {
            return hash(self.accumulator.seed, self.buffer[0..self.total_len]);
        }

        // Make a copy of the Accumulator state in case `self` needs to update() / be used later.
        var accumulator_copy = self.accumulator;
        var last_block_copy: [@sizeOf(Block)]u8 = undefined;

        // Digest the last block onthe Accumulator copy.
        return accumulator_copy.digest(self.total_len, last_block: {
            if (self.buffered >= @sizeOf(Block)) {
                const block_count = ((self.buffered - 1) / @sizeOf(Block)) * @sizeOf(Block);
                accumulator_copy.consume(std.mem.bytesAsSlice(Block, self.buffer[0..block_count]));
                break :last_block @ptrCast(self.buffer[self.buffered - @sizeOf(Block) ..][0..@sizeOf(Block)]);
            } else {
                const remaining = @sizeOf(Block) - self.buffered;
                @memcpy(last_block_copy[0..remaining], self.buffer[self.buffer.len - remaining ..][0..remaining]);
                @memcpy(last_block_copy[remaining..][0..self.buffered], self.buffer[0..self.buffered]);
                break :last_block @ptrCast(&last_block_copy);
            }
        });
    }
};

const verify = @import("verify.zig");

fn testExpect(comptime H: type, seed: anytype, input: []const u8, expected: u64) !void {
    try expectEqual(expected, H.hash(seed, input));

    var hasher = H.init(seed);
    hasher.update(input);
    try expectEqual(expected, hasher.final());
}

test "xxhash3" {
    if (builtin.cpu.arch.isMIPS64()) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/23807

    const H = XxHash3;
    // Non-Seeded Tests
    try testExpect(H, 0, "", 0x2d06800538d394c2);
    try testExpect(H, 0, "a", 0xe6c632b61e964e1f);
    try testExpect(H, 0, "abc", 0x78af5f94892f3950);
    try testExpect(H, 0, "message", 0x0b1ca9b8977554fa);
    try testExpect(H, 0, "message digest", 0x160d8e9329be94f9);
    try testExpect(H, 0, "abcdefghijklmnopqrstuvwxyz", 0x810f9ca067fbb90c);
    try testExpect(H, 0, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 0x643542bb51639cb2);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890", 0x7f58aa2520c681f9);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678", 0xb66ea795b5edc38c);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890", 0x8845e0b1b57330de);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123123", 0xf031f373d63c5653);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890", 0xf1bf601f9d868dce);

    // Seeded Tests
    try testExpect(H, 1, "", 0x4dc5b0cc826f6703);
    try testExpect(H, 1, "a", 0xd2f6d0996f37a720);
    try testExpect(H, 1, "abc", 0x6b4467b443c76228);
    try testExpect(H, 1, "message", 0x73fb1cf20d561766);
    try testExpect(H, 1, "message digest", 0xfe71a82a70381174);
    try testExpect(H, 1, "abcdefghijklmnopqrstuvwxyz", 0x902a2c2d016a37ba);
    try testExpect(H, 1, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 0xbf552e540c5c6882);
    try testExpect(H, 1, "12345678901234567890123456789012345678901234567890123456789012345678901234567890", 0xf2ca33235a6b865b);
    try testExpect(H, 1, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678", 0x6ef5cf958ba52c4);
    try testExpect(H, 1, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890", 0xfbc5f9c53d21cb2f);
    try testExpect(H, 1, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123123", 0x48682aca3b1c5c18);
    try testExpect(H, 1, "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890", 0x3903c5437fc4e726);
}

test "xxhash3 smhasher" {
    if (builtin.cpu.arch.isMIPS64()) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/23807

    const Test = struct {
        fn do() !void {
            try expectEqual(verify.smhasher(XxHash3.hash), 0x9a636405);
        }
    };
    try Test.do();
    @setEvalBranchQuota(75000);
    comptime try Test.do();
}

test "xxhash3 iterative api" {
    if (builtin.cpu.arch.isMIPS64()) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/23807

    const Test = struct {
        fn do() !void {
            try verify.iterativeApi(XxHash3);
        }
    };
    try Test.do();
    @setEvalBranchQuota(30000);
    comptime try Test.do();
}

test "xxhash64" {
    const H = XxHash64;
    try testExpect(H, 0, "", 0xef46db3751d8e999);
    try testExpect(H, 0, "a", 0xd24ec4f1a98c6e5b);
    try testExpect(H, 0, "abc", 0x44bc2cf5ad770999);
    try testExpect(H, 0, "message digest", 0x066ed728fceeb3be);
    try testExpect(H, 0, "abcdefghijklmnopqrstuvwxyz", 0xcfe1f278fa89835c);
    try testExpect(H, 0, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 0xaaa46907d3047814);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890", 0xe04a477f19ee145d);
}

test "xxhash64 smhasher" {
    const Test = struct {
        fn do() !void {
            try expectEqual(verify.smhasher(XxHash64.hash), 0x024B7CF4);
        }
    };
    try Test.do();
    @setEvalBranchQuota(75000);
    comptime try Test.do();
}

test "xxhash64 iterative api" {
    const Test = struct {
        fn do() !void {
            try verify.iterativeApi(XxHash64);
        }
    };
    try Test.do();
    @setEvalBranchQuota(30000);
    comptime try Test.do();
}

test "xxhash32" {
    const H = XxHash32;

    try testExpect(H, 0, "", 0x02cc5d05);
    try testExpect(H, 0, "a", 0x550d7456);
    try testExpect(H, 0, "abc", 0x32d153ff);
    try testExpect(H, 0, "message digest", 0x7c948494);
    try testExpect(H, 0, "abcdefghijklmnopqrstuvwxyz", 0x63a14d5f);
    try testExpect(H, 0, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 0x9c285e64);
    try testExpect(H, 0, "12345678901234567890123456789012345678901234567890123456789012345678901234567890", 0x9c05f475);
}

test "xxhash32 smhasher" {
    const Test = struct {
        fn do() !void {
            try expectEqual(verify.smhasher(XxHash32.hash), 0xBA88B743);
        }
    };
    try Test.do();
    @setEvalBranchQuota(85000);
    comptime try Test.do();
}

test "xxhash32 iterative api" {
    const Test = struct {
        fn do() !void {
            try verify.iterativeApi(XxHash32);
        }
    };
    try Test.do();
    @setEvalBranchQuota(30000);
    comptime try Test.do();
}



---
File: /std/heap/ArenaAllocator.zig
---

//! This allocator takes an existing allocator, wraps it, and provides an interface where
//! you can allocate and then free it all together. Calls to free an individual item only
//! free the item if it was the most recent allocation, otherwise calls to free do
//! nothing.
//!
//! The `Allocator` implementation provided is threadsafe, given that `child_allocator`
//! is threadsafe as well.
const ArenaAllocator = @This();

child_allocator: Allocator,
state: State,

/// Inner state of ArenaAllocator. Can be stored rather than the entire ArenaAllocator
/// as a memory-saving optimization.
///
/// Default initialization of this struct is deprecated; use `init` instead.
pub const State = struct {
    used_list: ?*Node = null,
    free_list: ?*Node = null,

    pub const init: State = .{
        .used_list = null,
        .free_list = null,
    };

    pub fn promote(state: State, child_allocator: Allocator) ArenaAllocator {
        return .{
            .child_allocator = child_allocator,
            .state = state,
        };
    }
};

pub fn allocator(arena: *ArenaAllocator) Allocator {
    return .{
        .ptr = arena,
        .vtable = &.{
            .alloc = alloc,
            .resize = resize,
            .remap = remap,
            .free = free,
        },
    };
}

pub fn init(child_allocator: Allocator) ArenaAllocator {
    return State.init.promote(child_allocator);
}

/// Not threadsafe.
pub fn deinit(arena: ArenaAllocator) void {
    // NOTE: When changing this, make sure `reset()` is adjusted accordingly!

    for ([_]?*Node{ arena.state.used_list, arena.state.free_list }) |first_node| {
        var it = first_node;
        while (it) |node| {
            // this has to occur before the free because the free frees node
            it = node.next;
            arena.child_allocator.rawFree(node.allocatedSliceUnsafe(), .of(Node), @returnAddress());
        }
    }
}

/// Queries the current memory use of this arena.
/// This will **not** include the storage required for internal keeping.
///
/// Not threadsafe.
pub fn queryCapacity(arena: ArenaAllocator) usize {
    var capacity: usize = 0;
    for ([_]?*Node{ arena.state.used_list, arena.state.free_list }) |first_node| {
        capacity += countListCapacity(first_node);
    }
    return capacity;
}
fn countListCapacity(first_node: ?*Node) usize {
    var capacity: usize = 0;
    var it = first_node;
    while (it) |node| : (it = node.next) {
        // Compute the actually allocated size excluding the
        // linked list node.
        capacity += node.size.toInt() - @sizeOf(Node);
    }
    return capacity;
}

pub const ResetMode = union(enum) {
    /// Releases all allocated memory in the arena.
    free_all,
    /// This will pre-heat the arena for future allocations by allocating a
    /// large enough buffer for all previously done allocations.
    /// Preheating will speed up the allocation process by invoking the backing allocator
    /// less often than before. If `reset()` is used in a loop, this means that after the
    /// biggest operation, no memory allocations are performed anymore.
    retain_capacity,
    /// This is the same as `retain_capacity`, but the memory will be shrunk to
    /// this value if it exceeds the limit.
    retain_with_limit: usize,
};
/// Resets the arena allocator and frees all allocated memory.
///
/// `mode` defines how the currently allocated memory is handled.
/// See the variant documentation for `ResetMode` for the effects of each mode.
///
/// The function will return whether the reset operation was successful or not.
/// If the reallocation  failed `false` is returned. The arena will still be fully
/// functional in that case, all memory is released. Future allocations just might
/// be slower.
///
/// Not threadsafe.
///
/// NOTE: If `mode` is `free_all`, the function will always return `true`.
pub fn reset(arena: *ArenaAllocator, mode: ResetMode) bool {
    // Some words on the implementation:
    // The reset function can be implemented with two basic approaches:
    // - Counting how much bytes were allocated since the last reset, and storing that
    //   information in State. This will make reset fast and alloc only a teeny tiny bit
    //   slower.
    // - Counting how much bytes were allocated by iterating the chunk linked list. This
    //   will make reset slower, but alloc() keeps the same speed when reset() as if reset()
    //   would not exist.
    //
    // The second variant was chosen for implementation, as with more and more calls to reset(),
    // the function will get faster and faster. At one point, the complexity of the function
    // will drop to amortized O(1), as we're only ever having a single chunk that will not be
    // reallocated, and we're not even touching the backing allocator anymore.
    //
    // Thus, only the first hand full of calls to reset() will actually need to iterate the linked
    // list, all future calls are just taking the first node, and only resetting the `end_index`
    // value.

    const limit: ?usize = switch (mode) {
        .retain_capacity => null,
        .retain_with_limit => |limit| limit,
        .free_all => 0,
    };
    if (limit == 0) {
        // just reset when we don't have anything to reallocate
        arena.deinit();
        arena.state = .init;
        return true;
    }

    const used_capacity = countListCapacity(arena.state.used_list);
    const free_capacity = countListCapacity(arena.state.free_list);

    const new_used_capacity = if (limit) |lim| @min(lim, used_capacity) else used_capacity;
    const new_free_capacity = if (limit) |lim| @min(lim - new_used_capacity, free_capacity) else free_capacity;

    var ok = true;

    for (
        [_]*?*Node{ &arena.state.used_list, &arena.state.free_list },
        [_]usize{ new_used_capacity, new_free_capacity },
    ) |first_node_ptr, new_capacity| {
        // Free all nodes except for the last one
        var it = first_node_ptr.*;
        const node: *Node = while (it) |node| {
            // this has to occur before the free because the free frees node
            it = node.next;
            if (it == null) break node;
            arena.child_allocator.rawFree(node.allocatedSliceUnsafe(), .of(Node), @returnAddress());
        } else {
            continue;
        };
        const allocated_slice = node.allocatedSliceUnsafe();

        // Align backwards to always stay below limit.
        const new_size = mem.alignBackward(usize, @sizeOf(Node) + new_capacity, 2);

        if (new_size == @sizeOf(Node)) {
            arena.child_allocator.rawFree(allocated_slice, .of(Node), @returnAddress());
            first_node_ptr.* = null;
            continue;
        }

        node.end_index = 0;
        first_node_ptr.* = node;

        if (allocated_slice.len == new_size) {
            // perfect, no need to invoke the child_allocator
            continue;
        }

        if (arena.child_allocator.rawResize(allocated_slice, .of(Node), new_size, @returnAddress())) {
            // successful resize
            node.size = .fromInt(new_size);
        } else {
            // manual realloc
            const new_ptr = arena.child_allocator.rawAlloc(new_size, .of(Node), @returnAddress()) orelse {
                // we failed to preheat the arena properly, signal this to the user.
                ok = false;
                continue;
            };
            arena.child_allocator.rawFree(allocated_slice, .of(Node), @returnAddress());
            const new_first_node: *Node = @ptrCast(@alignCast(new_ptr));
            new_first_node.* = .{
                .size = .fromInt(new_size),
                .end_index = 0,
                .next = null,
            };
            first_node_ptr.* = new_first_node;
        }
    }

    return ok;
}

/// Concurrent accesses to node pointers generally have to have acquire/release
/// semantics to guarantee that newly allocated notes are in a valid state when
/// being inserted into a list. Exceptions are possible, e.g. a cmpxchg loop that
/// never accesses the node returned on failure can use monotonic semantics on
/// failure, but must still use release semantics on success to protect the node
/// it's trying to push.
const Node = struct {
    /// Only meant to be accessed indirectly via the methods supplied by this type,
    /// except if the node is owned by the thread accessing it.
    /// Must always be an even number to accommodate `resize` bit.
    size: Size,
    /// Any increase of `end_index` has to use acquire semantics;
    /// any decrease of `end_index` that invalidates (formerly) active allocations
    /// has to use release semantics.
    /// This guarantees that all accesses to memory that's about to be freed
    /// happen-before the free is published.
    /// Since `size` can only grow and never shrink, memory access depending on
    /// any `end_index` <= any `size` can never be OOB.
    end_index: usize,
    /// This field should only be accessed if the node is owned by the thread
    /// accessing it.
    next: ?*Node,

    const Size = packed struct(usize) {
        resizing: bool,
        _: @Int(.unsigned, @bitSizeOf(usize) - 1) = 0,

        fn fromInt(int: usize) Size {
            assert(int >= @sizeOf(Node));
            const size: Size = @bitCast(int);
            assert(!size.resizing);
            return size;
        }

        fn toInt(size: Size) usize {
            var int = size;
            int.resizing = false;
            return @bitCast(int);
        }

        comptime {
            assert(Size{ .resizing = true } == @as(Size, @bitCast(@as(usize, 1))));
        }
    };

    fn loadBuf(node: *Node) []u8 {
        // `size` can only ever grow, so the buffer returned by this function is
        // always valid memory.
        const size = @atomicLoad(Size, &node.size, .monotonic);
        return @as([*]u8, @ptrCast(node))[0..size.toInt()][@sizeOf(Node)..];
    }

    /// Returns allocated slice or `null` if node is already (being) resized.
    fn beginResize(node: *Node) ?[]u8 {
        const size = @atomicRmw(Size, &node.size, .Or, .{ .resizing = true }, .acquire); // syncs with release in `endResize`
        if (size.resizing) return null;
        return @as([*]u8, @ptrCast(node))[0..size.toInt()];
    }

    fn endResize(node: *Node, size: usize, prev_size: usize) void {
        assert(size >= prev_size); // nodes must not shrink
        assert(@atomicLoad(Size, &node.size, .unordered).toInt() == prev_size);
        return @atomicStore(Size, &node.size, .fromInt(size), .release); // syncs with acquire in `beginResize`
    }

    /// Not threadsafe.
    fn allocatedSliceUnsafe(node: *Node) []u8 {
        return @as([*]u8, @ptrCast(node))[0..node.size.toInt()];
    }
};

fn loadFirstNode(arena: *ArenaAllocator) ?*Node {
    return @atomicLoad(?*Node, &arena.state.used_list, .acquire); // syncs with release in successful `tryPushNode`
}

const PushResult = union(enum) {
    success,
    failure: ?*Node,
};
fn tryPushNode(arena: *ArenaAllocator, node: *Node) PushResult {
    assert(node != node.next);
    if (@cmpxchgStrong( // strong because retrying means discarding a fitting node -> expensive
        ?*Node,
        &arena.state.used_list,
        node.next,
        node,
        .release, // syncs with acquire in failure path or `loadFirstNode`
        .acquire, // syncs with release in success path
    )) |old_node| {
        return .{ .failure = old_node };
    } else {
        return .success;
    }
}

fn stealFreeList(arena: *ArenaAllocator) ?*Node {
    // We don't need acq_rel here because we're always swapping in `null`, so
    // there's no node we'd need to release.
    return @atomicRmw(?*Node, &arena.state.free_list, .Xchg, null, .acquire); // syncs with release in `pushFreeList`
}

fn pushFreeList(arena: *ArenaAllocator, first: *Node, last: *Node) void {
    assert(first != last.next);
    assert(first != first.next);
    assert(last != last.next);
    while (@cmpxchgWeak(
        ?*Node,
        &arena.state.free_list,
        last.next,
        first,
        .release, // syncs with acquire in `stealFreeList`
        .monotonic, // we never access any fields of `old_free_list`, we only care about the pointer
    )) |old_free_list| {
        last.next = old_free_list;
    }
}

fn alignedIndex(buf_ptr: [*]u8, end_index: usize, alignment: Alignment) usize {
    // Wrapping arithmetic to avoid overflows since `end_index` isn't bounded by
    // `size`. This is always ok since the max alignment in byte units is also
    // the max value of `usize` so wrapped values are correctly aligned anyway.
    return alignment.forward(@intFromPtr(buf_ptr) +% end_index) -% @intFromPtr(buf_ptr);
}

fn alloc(ctx: *anyopaque, n: usize, alignment: Alignment, ret_addr: usize) ?[*]u8 {
    const arena: *ArenaAllocator = @ptrCast(@alignCast(ctx));
    _ = ret_addr;

    assert(n > 0);

    var cur_first_node = arena.loadFirstNode();

    var cur_new_node: ?*Node = null;
    defer if (cur_new_node) |node| {
        node.next = null; // optimize for empty free list
        arena.pushFreeList(node, node);
    };

    retry: while (true) {
        const first_node: ?*Node, const prev_size: usize = first_node: {
            const node = cur_first_node orelse break :first_node .{ null, 0 };
            const buf = node.loadBuf();

            // To avoid using a CAS loop in the hot path we atomically increase
            // `end_index` by a large enough amount to be able to always provide
            // the required alignment within the reserved memory. To recover the
            // space this potentially wastes we try to subtract the 'overshoot'
            // with a single cmpxchg afterwards, which may fail.

            const alignable = n + alignment.toByteUnits() - 1;
            const end_index = @atomicRmw(usize, &node.end_index, .Add, alignable, .acquire); // acquire any memory that may have been freed
            const aligned_index = alignedIndex(buf.ptr, end_index, alignment);
            assert(end_index + alignable >= aligned_index + n);
            if (end_index + alignable != aligned_index + n) {
                _ = @cmpxchgStrong(
                    usize,
                    &node.end_index,
                    end_index + alignable,
                    aligned_index + n,
                    .monotonic, // no need to release alignment padding; there's no one accessing it!
                    .monotonic,
                );
            }

            if (aligned_index + n > buf.len) break :first_node .{ node, buf.len };
            return buf[aligned_index..][0..n].ptr;
        };

        resize: {
            // Before attempting to get our hands on a new node, we try to resize
            // the one we're currently holding. This is an exclusive operation;
            // if another thread is already in this section we can never resize.

            const node = first_node orelse break :resize;
            const allocated_slice = node.beginResize() orelse break :resize;
            var size = allocated_slice.len;
            defer node.endResize(size, allocated_slice.len);

            const buf = allocated_slice[@sizeOf(Node)..];
            const end_index = @atomicLoad(usize, &node.end_index, .monotonic);
            const aligned_index = alignedIndex(buf.ptr, end_index, alignment);
            const new_size = mem.alignForward(usize, @sizeOf(Node) + aligned_index + n, 2);

            if (new_size <= allocated_slice.len) {
                // A `resize` or `free` call managed to sneak in and we need to
                // guarantee that `size` is only ever increased; retry!
                continue :retry;
            }

            if (arena.child_allocator.rawResize(allocated_slice, .of(Node), new_size, @returnAddress())) {
                size = new_size;

                // strong because a spurious failure could result in suboptimal
                // usage of this node
                if (null == @cmpxchgStrong(
                    usize,
                    &node.end_index,
                    end_index,
                    aligned_index + n,
                    .acquire, // acquire any memory that may have been freed
                    .monotonic,
                )) {
                    const new_buf = allocated_slice.ptr[0..new_size][@sizeOf(Node)..];
                    return new_buf[aligned_index..][0..n].ptr;
                }
            }
        }

        // We need a new node! First, we search `free_list` for one that's big
        // enough, if we don't find one there we fall back to allocating a new
        // node with `child_allocator` (if we haven't already done that!).

        from_free_list: {
            // We 'steal' the entire free list to operate on it without other
            // threads getting up into our business.
            // This is a rather pragmatic approach, but since the free list isn't
            // used very frequently it's fine performance-wise, even under load.
            // Also this avoids the ABA problem; stealing the list with an atomic
            // swap doesn't introduce any potentially stale `next` pointers.

            const free_list = arena.stealFreeList() orelse break :from_free_list;

            const first_free: *Node, const last_free: *Node, const node: *Node, const prev: ?*Node = find: {
                var best_fit_prev: ?*Node = null;
                var best_fit: ?*Node = null;
                var best_fit_diff: usize = std.math.maxInt(usize);

                var it_prev: ?*Node = null;
                var it: ?*Node = free_list;
                while (it) |node| : ({
                    it_prev = node;
                    it = node.next;
                }) {
                    assert(!node.size.resizing);
                    const buf = node.allocatedSliceUnsafe()[@sizeOf(Node)..];
                    const aligned_index = alignedIndex(buf.ptr, 0, alignment);

                    const diff = aligned_index + n -| buf.len;
                    if (diff < best_fit_diff) {
                        best_fit_prev = it_prev;
                        best_fit = node;
                        best_fit_diff = diff;
                    }
                }

                break :find .{ free_list, it_prev.?, best_fit.?, best_fit_prev };
            };

            const aligned_index, const need_resize = aligned_index: {
                const buf = node.allocatedSliceUnsafe()[@sizeOf(Node)..];
                const aligned_index = alignedIndex(buf.ptr, 0, alignment);
                break :aligned_index .{ aligned_index, aligned_index + n > buf.len };
            };

            if (need_resize) {
                // Ideally we want to use all nodes in `free_list` eventually,
                // so even if none fit we'll try to resize the one that was the
                // closest to being large enough.
                const new_size = mem.alignForward(usize, @sizeOf(Node) + aligned_index + n, 2);
                if (arena.child_allocator.rawResize(node.allocatedSliceUnsafe(), .of(Node), new_size, @returnAddress())) {
                    node.size = .fromInt(new_size);
                } else {
                    arena.pushFreeList(first_free, last_free);
                    break :from_free_list; // we couldn't find a fitting free node
                }
            }

            const buf = node.allocatedSliceUnsafe()[@sizeOf(Node)..];
            const old_next = node.next;

            node.end_index = aligned_index + n;
            node.next = first_node;

            switch (arena.tryPushNode(node)) {
                .success => {
                    // Finish removing node from free list.
                    if (prev) |p| p.next = old_next;

                    // Push remaining stolen free list back onto `arena.state.free_list`.
                    const new_first_free = if (node == first_free) old_next else first_free;
                    const new_last_free = if (node == last_free) prev else last_free;
                    if (new_first_free) |first| {
                        const last = new_last_free.?;
                        arena.pushFreeList(first, last);
                    }

                    return buf[aligned_index..][0..n].ptr;
                },
                .failure => |old_first_node| {
                    // restore free list to as we found it
                    node.next = old_next;
                    arena.pushFreeList(first_free, last_free);

                    cur_first_node = old_first_node;
                    continue :retry; // there's a new first node; retry!
                },
            }
        }

        const new_node: *Node = new_node: {
            if (cur_new_node) |new_node| {
                break :new_node new_node;
            } else {
                @branchHint(.cold);
            }

            const size: Node.Size = size: {
                const min_size = @sizeOf(Node) + alignment.toByteUnits() + n;
                const big_enough_size = prev_size + min_size + 16;
                const size = mem.alignForward(usize, big_enough_size + big_enough_size / 2, 2);
                break :size .fromInt(size);
            };
            const ptr = arena.child_allocator.rawAlloc(size.toInt(), .of(Node), @returnAddress()) orelse
                return null;
            const new_node: *Node = @ptrCast(@alignCast(ptr));
            new_node.* = .{
                .size = size,
                .end_index = undefined, // set below
                .next = undefined, // set below
            };
            cur_new_node = new_node;
            break :new_node new_node;
        };

        const buf = new_node.allocatedSliceUnsafe()[@sizeOf(Node)..];
        const aligned_index = alignedIndex(buf.ptr, 0, alignment);
        assert(new_node.size.toInt() >= @sizeOf(Node) + aligned_index + n);

        new_node.end_index = aligned_index + n;
        new_node.next = first_node;

        switch (arena.tryPushNode(new_node)) {
            .success => {
                cur_new_node = null;
                return buf[aligned_index..][0..n].ptr;
            },
            .failure => |old_first_node| {
                cur_first_node = old_first_node;
            },
        }
    }
}

fn resize(ctx: *anyopaque, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) bool {
    const arena: *ArenaAllocator = @ptrCast(@alignCast(ctx));
    _ = alignment;
    _ = ret_addr;

    assert(memory.len > 0);
    assert(new_len > 0);

    const node = arena.loadFirstNode().?;
    const buf_ptr = @as([*]u8, @ptrCast(node)) + @sizeOf(Node);

    const cur_end_index = @atomicLoad(usize, &node.end_index, .monotonic);

    if (buf_ptr + cur_end_index != memory.ptr + memory.len) {
        // It's not the most recent allocation, so it cannot be expanded,
        // but it's fine if they want to make it smaller.
        return new_len <= memory.len;
    }

    if (new_len <= memory.len) {
        const new_end_index = cur_end_index - (memory.len - new_len);
        assert(buf_ptr + new_end_index == memory.ptr + new_len);

        _ = @cmpxchgStrong(
            usize,
            &node.end_index,
            cur_end_index,
            new_end_index,
            .release, // release freed memory
            .monotonic,
        );
        return true; // Shrinking allocations should always succeed.
    }

    // Saturating arithmetic because `end_index` is not guaranteed to be `<= size`.
    // The allocation we're trying to resize *could* belong to a different node!
    if (node.loadBuf().len -| cur_end_index >= new_len - memory.len) {
        const new_end_index = cur_end_index + (new_len - memory.len);
        assert(buf_ptr + new_end_index == memory.ptr + new_len);

        return null == @cmpxchgStrong(
            usize,
            &node.end_index,
            cur_end_index,
            new_end_index,
            .acquire, // acquire any memory that may have been freed
            .monotonic,
        );
    }

    return false;
}

fn remap(ctx: *anyopaque, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) ?[*]u8 {
    return if (resize(ctx, memory, alignment, new_len, ret_addr)) memory.ptr else null;
}

fn free(ctx: *anyopaque, memory: []u8, alignment: Alignment, ret_addr: usize) void {
    const arena: *ArenaAllocator = @ptrCast(@alignCast(ctx));
    _ = alignment;
    _ = ret_addr;

    assert(memory.len > 0);

    const node = arena.loadFirstNode().?;
    const buf_ptr = @as([*]u8, @ptrCast(node)) + @sizeOf(Node);

    const cur_end_index = @atomicLoad(usize, &node.end_index, .monotonic);

    if (buf_ptr + cur_end_index != memory.ptr + memory.len) {
        // Not the most recent allocation; we cannot free it.
        return;
    }

    const new_end_index = cur_end_index - memory.len;
    assert(buf_ptr + new_end_index == memory.ptr);

    _ = @cmpxchgStrong(
        usize,
        &node.end_index,
        cur_end_index,
        new_end_index,
        .release, // release freed memory
        .monotonic,
    );
}

const std = @import("std");
const assert = std.debug.assert;
const mem = std.mem;
const Allocator = std.mem.Allocator;
const Alignment = std.mem.Alignment;

test "reset with preheating" {
    var arena_allocator = ArenaAllocator.init(std.testing.allocator);
    defer arena_allocator.deinit();
    // provides some variance in the allocated data
    var rng_src = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = rng_src.random();
    var rounds: usize = 25;
    while (rounds > 0) {
        rounds -= 1;
        _ = arena_allocator.reset(.retain_capacity);
        var alloced_bytes: usize = 0;
        const total_size: usize = random.intRangeAtMost(usize, 256, 16384);
        while (alloced_bytes < total_size) {
            const size = random.intRangeAtMost(usize, 16, 256);
            const alignment: Alignment = .@"32";
            const slice = try arena_allocator.allocator().alignedAlloc(u8, alignment, size);
            try std.testing.expect(alignment.check(@intFromPtr(slice.ptr)));
            try std.testing.expectEqual(size, slice.len);
            alloced_bytes += slice.len;
        }
    }
}

test "reset while retaining a buffer" {
    var arena_allocator = ArenaAllocator.init(std.testing.allocator);
    defer arena_allocator.deinit();
    const a = arena_allocator.allocator();

    // Create two internal buffers
    _ = try a.alloc(u8, 1);
    _ = try a.alloc(u8, 1000);

    try std.testing.expect(arena_allocator.state.used_list != null);

    // Check that we have at least two buffers
    try std.testing.expect(arena_allocator.state.used_list.?.next != null);

    // This retains the first allocated buffer
    try std.testing.expect(arena_allocator.reset(.{ .retain_with_limit = 2 }));
    try std.testing.expect(arena_allocator.state.used_list.?.next == null);
    try std.testing.expectEqual(2, arena_allocator.queryCapacity());
}

test "fuzz multi threaded" {
    @disableInstrumentation();
    if (@import("builtin").single_threaded) return error.SkipZigTest;

    const gpa = std.heap.smp_allocator;

    var io_instance: std.Io.Threaded = .init(gpa, .{});
    defer io_instance.deinit();

    var arena_state: ArenaAllocator.State = .init;
    // No need to deinit arena_state, all allocations are in `sample_buffer`!

    const buffer_size = FuzzContext.max_alloc_count * FuzzContext.max_alloc_size;

    const control_buffer = try gpa.alloc(u8, buffer_size);
    defer gpa.free(control_buffer);
    var control_instance: std.heap.FixedBufferAllocator = .init(control_buffer);

    const sample_buffer = try gpa.alloc(u8, buffer_size);
    defer gpa.free(sample_buffer);
    var sample_instance: FuzzAllocator = .init(sample_buffer);

    try std.testing.fuzz(FuzzContext.Init{
        .threaded_instance = &io_instance,
        .arena_state = &arena_state,
        .control_instance = &control_instance,
        .sample_instance = &sample_instance,
    }, fuzzMultiThreaded, .{});
}

fn fuzzMultiThreaded(fuzz_init: FuzzContext.Init, smith: *std.testing.Smith) anyerror!void {
    @disableInstrumentation();
    const testing = std.testing;
    const io = fuzz_init.threaded_instance.io();

    fuzz_init.sample_instance.prepareFailures(smith);

    const control_allocator = fuzz_init.control_instance.threadSafeAllocator();
    const sample_child_allocator = fuzz_init.sample_instance.allocator();

    var arena_instance = fuzz_init.arena_state.*.promote(sample_child_allocator);
    defer fuzz_init.arena_state.* = arena_instance.state;

    var ctx: FuzzContext = .init(
        control_allocator,
        arena_instance.allocator(),
    );
    defer ctx.deinit();

    var group: std.Io.Group = .init;
    defer group.cancel(io);

    var n_allocs: usize = 0;
    var n_actions: usize = 0;
    while (!smith.eosWeightedSimple(99, 1) and n_actions < FuzzContext.max_action_count) {
        errdefer comptime unreachable;

        const weights: []const testing.Smith.Weight = if (n_allocs == FuzzContext.max_alloc_count)
            &.{
                .value(FuzzContext.Action, .resize, 1),
                .value(FuzzContext.Action, .remap, 1),
                .value(FuzzContext.Action, .free, 1),
            }
        else
            &.{
                .value(FuzzContext.Action, .resize, 1),
                .value(FuzzContext.Action, .remap, 1),
                .value(FuzzContext.Action, .free, 1),
                .value(FuzzContext.Action, .alloc, 3),
            };
        switch (smith.valueWeighted(FuzzContext.Action, weights)) {
            .alloc => {
                const alloc_index = n_allocs;
                n_allocs += 1;
                ctx.allocs[alloc_index].common.len = .free;
                group.concurrent(io, FuzzContext.doOneAlloc, .{
                    &ctx,
                    nextLen(smith),
                    smith.valueRangeAtMost(
                        Alignment,
                        .@"1",
                        .fromByteUnits(2 * std.heap.page_size_max),
                    ),
                    @enumFromInt(alloc_index),
                }) catch unreachable;
            },
            .resize => group.concurrent(io, FuzzContext.doOneResize, .{ &ctx, nextLen(smith) }) catch unreachable,
            .remap => group.concurrent(io, FuzzContext.doOneRemap, .{ &ctx, nextLen(smith) }) catch unreachable,
            .free => group.concurrent(io, FuzzContext.doOneFree, .{&ctx}) catch unreachable,
        }
        n_actions += 1;
    }

    try group.await(io);
    try ctx.check(n_allocs);

    // This also covers the `deinit` logic since `free_all` uses it internally.

    const old_capacity = arena_instance.queryCapacity();
    const reset_mode: ResetMode = switch (smith.value(@typeInfo(ResetMode).@"union".tag_type.?)) {
        .free_all => .free_all,
        .retain_capacity => .retain_capacity,
        .retain_with_limit => .{ .retain_with_limit = smith.value(usize) },
    };
    const ok = arena_instance.reset(reset_mode);
    const new_capacity = arena_instance.queryCapacity();
    switch (reset_mode) {
        .free_all => {
            try testing.expect(ok);
            try testing.expectEqual(0, new_capacity);
            fuzz_init.sample_instance.reset();
        },
        .retain_with_limit => |limit| if (ok) try testing.expect(new_capacity <= limit),
        .retain_capacity => if (ok) try testing.expectEqual(old_capacity, new_capacity),
    }

    fuzz_init.control_instance.reset();
}
fn nextLen(smith: *std.testing.Smith) @typeInfo(FuzzContext.Alloc.Len).@"enum".tag_type {
    @disableInstrumentation();
    const BackingInt = @typeInfo(FuzzContext.Alloc.Len).@"enum".tag_type;
    return smith.valueRangeAtMost(BackingInt, 1, FuzzContext.max_alloc_size);
}

const FuzzContext = struct {
    control_allocator: Allocator,
    sample_allocator: Allocator,

    last_alloc_index: Alloc.Index,
    allocs: [max_alloc_count]Alloc,

    const max_alloc_count = 64;
    const max_action_count = 2 * max_alloc_count;

    const max_alloc_size = 16 << 10;

    const Alloc = struct {
        control_ptr: [*]u8,
        sample_ptr: [*]u8,
        common: packed struct(usize) {
            len: Len,
            alignment: Alignment,
            _: @Int(.unsigned, padding_bits) = 0,
        },

        const Len = enum(@Int(.unsigned, len_bits)) {
            free = (1 << len_bits) - 1,
            _,
        };
        const len_bits = @min(64, @bitSizeOf(usize)) - @bitSizeOf(Alignment);
        const padding_bits = @bitSizeOf(usize) - (len_bits + @bitSizeOf(Alignment));

        const Index = enum(usize) {
            none = std.math.maxInt(usize),
            _,
        };
    };

    const Action = enum {
        alloc,
        resize,
        remap,
        free,
    };

    const Init = struct {
        threaded_instance: *std.Io.Threaded,
        arena_state: *ArenaAllocator.State,
        control_instance: *std.heap.FixedBufferAllocator,
        sample_instance: *FuzzAllocator,
    };

    fn init(
        control_allocator: Allocator,
        sample_allocator: Allocator,
    ) FuzzContext {
        @disableInstrumentation();
        return .{
            .control_allocator = control_allocator,
            .sample_allocator = sample_allocator,
            .last_alloc_index = .none,
            .allocs = undefined,
        };
    }

    fn deinit(ctx: *FuzzContext) void {
        @disableInstrumentation();
        ctx.* = undefined;
    }

    fn check(ctx: *const FuzzContext, n_allocs: usize) !void {
        @disableInstrumentation();
        for (ctx.allocs[0..n_allocs]) |allocation| {
            const len: usize = switch (allocation.common.len) {
                .free => continue,
                _ => |len| @intFromEnum(len),
            };
            const control = allocation.control_ptr[0..len];
            const sample = allocation.sample_ptr[0..len];
            try std.testing.expectEqualSlices(u8, control, sample);
        }
    }

    fn doOneAlloc(ctx: *FuzzContext, len: usize, alignment: Alignment, index: Alloc.Index) void {
        @disableInstrumentation();
        assert(ctx.allocs[@intFromEnum(index)].common.len == .free);

        const control_ptr = ctx.control_allocator.rawAlloc(len, alignment, @returnAddress()) orelse
            return;
        const sample_ptr = ctx.sample_allocator.rawAlloc(len, alignment, @returnAddress()) orelse {
            ctx.control_allocator.rawFree(control_ptr[0..len], alignment, @returnAddress());
            return;
        };

        ctx.allocs[@intFromEnum(index)] = .{
            .control_ptr = control_ptr,
            .sample_ptr = sample_ptr,
            .common = .{
                .len = @enumFromInt(len),
                .alignment = alignment,
            },
        };

        for (control_ptr[0..len], sample_ptr[0..len], 0..) |*control, *sample, i| {
            control.* = @truncate(i);
            sample.* = @truncate(i);
        }

        @atomicStore(Alloc.Index, &ctx.last_alloc_index, index, .release);
    }
    fn doOneResize(ctx: *FuzzContext, new_len: usize) void {
        @disableInstrumentation();

        const index = @atomicRmw(Alloc.Index, &ctx.last_alloc_index, .Xchg, .none, .acquire);
        if (index == .none) return;

        const allocation = &ctx.allocs[@intFromEnum(index)];
        assert(allocation.common.len != .free);
        const memory = allocation.sample_ptr[0..@intFromEnum(allocation.common.len)];
        const alignment = allocation.common.alignment;

        assert(alignment.check(@intFromPtr(allocation.control_ptr)));
        assert(alignment.check(@intFromPtr(allocation.sample_ptr)));

        // Since `resize` is fallible, we have to ensure that `control_allocator`
        // is always successful by reserving the memory we need beforehand.
        const new_control_ptr = ctx.control_allocator.rawAlloc(new_len, alignment, @returnAddress()) orelse
            return;
        if (ctx.sample_allocator.rawResize(memory, alignment, new_len, @returnAddress())) {
            const old_control = allocation.control_ptr[0..memory.len];
            const overlap = @min(memory.len, new_len);
            @memcpy(new_control_ptr[0..overlap], old_control[0..overlap]);
            ctx.control_allocator.rawFree(old_control, alignment, @returnAddress());
        } else {
            ctx.control_allocator.rawFree(new_control_ptr[0..new_len], alignment, @returnAddress());
            return;
        }

        ctx.allocs[@intFromEnum(index)] = .{
            .control_ptr = new_control_ptr,
            .sample_ptr = memory.ptr,
            .common = .{
                .len = @enumFromInt(new_len),
                .alignment = alignment,
            },
        };

        if (new_len > memory.len) {
            for (
                allocation.control_ptr[memory.len..new_len],
                allocation.sample_ptr[memory.len..new_len],
                0..,
            ) |*control, *sample, i| {
                control.* = @truncate(i);
                sample.* = @truncate(i);
            }
        }

        @atomicStore(Alloc.Index, &ctx.last_alloc_index, index, .release);
    }
    fn doOneRemap(ctx: *FuzzContext, new_len: usize) void {
        @disableInstrumentation();
        return doOneResize(ctx, new_len);
    }
    fn doOneFree(ctx: *FuzzContext) void {
        @disableInstrumentation();

        const index = @atomicRmw(Alloc.Index, &ctx.last_alloc_index, .Xchg, .none, .acquire);
        if (index == .none) return;

        const allocation = &ctx.allocs[@intFromEnum(index)];
        assert(allocation.common.len != .free);
        const len: usize = @intFromEnum(allocation.common.len);
        const alignment = allocation.common.alignment;

        assert(alignment.check(@intFromPtr(allocation.control_ptr)));
        assert(alignment.check(@intFromPtr(allocation.sample_ptr)));

        ctx.control_allocator.rawFree(allocation.control_ptr[0..len], alignment, @returnAddress());
        ctx.sample_allocator.rawFree(allocation.sample_ptr[0..len], alignment, @returnAddress());

        ctx.allocs[@intFromEnum(index)] = .{
            .control_ptr = undefined,
            .sample_ptr = undefined,
            .common = .{
                .len = .free,
                .alignment = .@"1",
            },
        };
    }
};

const FuzzAllocator = struct {
    fba: std.heap.FixedBufferAllocator,
    spurious_failures: [256]u8,
    index: u8,

    fn init(buffer: []u8) FuzzAllocator {
        @disableInstrumentation();
        return .{
            .fba = .init(buffer),
            .spurious_failures = undefined, // set with `preprepareFailures`
            .index = 0,
        };
    }

    fn prepareFailures(fa: *FuzzAllocator, smith: *std.testing.Smith) void {
        @disableInstrumentation();
        const bool_weights: []const std.testing.Smith.Weight = &.{
            .value(u8, 0, 10),
            .value(u8, 1, 1),
        };
        smith.bytesWeighted(&fa.spurious_failures, bool_weights);
        fa.index = 0;
    }

    fn reset(fa: *FuzzAllocator) void {
        @disableInstrumentation();
        fa.fba.reset();
    }

    fn allocator(fa: *FuzzAllocator) Allocator {
        @disableInstrumentation();
        return .{
            .ptr = fa,
            .vtable = &.{
                .alloc = FuzzAllocator.alloc,
                .resize = FuzzAllocator.resize,
                .remap = FuzzAllocator.remap,
                .free = FuzzAllocator.free,
            },
        };
    }

    fn alloc(ctx: *anyopaque, len: usize, alignment: Alignment, ret_addr: usize) ?[*]u8 {
        @disableInstrumentation();
        const fa: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        _ = ret_addr;

        const index = @atomicRmw(u8, &fa.index, .Add, 1, .monotonic);
        if (fa.spurious_failures[index] != 0) return null;
        return fa.fba.threadSafeAllocator().rawAlloc(len, alignment, @returnAddress());
    }

    fn resize(ctx: *anyopaque, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) bool {
        @disableInstrumentation();
        const fa: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        _ = ret_addr;

        const index = @atomicRmw(u8, &fa.index, .Add, 1, .monotonic);
        if (fa.spurious_failures[index] != 0) return false;
        return fa.fba.threadSafeAllocator().rawResize(memory, alignment, new_len, @returnAddress());
    }

    fn remap(ctx: *anyopaque, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) ?[*]u8 {
        @disableInstrumentation();
        const fa: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        _ = ret_addr;

        const index = @atomicRmw(u8, &fa.index, .Add, 1, .monotonic);
        if (fa.spurious_failures[index] != 0) return null;
        return fa.fba.threadSafeAllocator().rawRemap(memory, alignment, new_len, @returnAddress());
    }

    fn free(ctx: *anyopaque, memory: []u8, alignment: Alignment, ret_addr: usize) void {
        @disableInstrumentation();
        const fa: *FuzzAllocator = @ptrCast(@alignCast(ctx));
        _ = ret_addr;
        return fa.fba.threadSafeAllocator().rawFree(memory, alignment, @returnAddress());
    }
};



---
File: /std/heap/BrkAllocator.zig
---

//! Supports single-threaded targets that have a sbrk-like primitive which includes
//! Linux and WebAssembly.
//!
//! On Linux, assumes exclusive access to the brk syscall.
const BrkAllocator = @This();
const builtin = @import("builtin");

const std = @import("../std.zig");
const Allocator = std.mem.Allocator;
const Alignment = std.mem.Alignment;
const assert = std.debug.assert;
const math = std.math;

comptime {
    if (!builtin.single_threaded) @compileError("unsupported");
}

next_addrs: [size_class_count]usize = @splat(0),
/// For each size class, points to the freed pointer.
frees: [size_class_count]usize = @splat(0),
/// For each big size class, points to the freed pointer.
big_frees: [big_size_class_count]usize = @splat(0),
prev_brk: usize = 0,

var global: BrkAllocator = .{};

pub const vtable: Allocator.VTable = .{
    .alloc = alloc,
    .resize = resize,
    .remap = remap,
    .free = free,
};

pub const Error = Allocator.Error;

const max_usize = math.maxInt(usize);
const ushift = math.Log2Int(usize);
const bigpage_size: comptime_int = @max(64 * 1024, std.heap.page_size_max);
const bigpage_count = max_usize / bigpage_size;

/// Because of storing free list pointers, the minimum size class is 3.
const min_class = math.log2(math.ceilPowerOfTwoAssert(usize, 1 + @sizeOf(usize)));
const size_class_count = math.log2(bigpage_size) - min_class;
/// 0 - 1 bigpage
/// 1 - 2 bigpages
/// 2 - 4 bigpages
/// etc.
const big_size_class_count = math.log2(bigpage_count);

fn alloc(ctx: *anyopaque, len: usize, alignment: Alignment, return_address: usize) ?[*]u8 {
    _ = ctx;
    _ = return_address;
    // Make room for the freelist next pointer.
    const actual_len = @max(len +| @sizeOf(usize), alignment.toByteUnits());
    const slot_size = math.ceilPowerOfTwo(usize, actual_len) catch return null;
    const class = math.log2(slot_size) - min_class;
    if (class < size_class_count) {
        const addr = a: {
            const top_free_ptr = global.frees[class];
            if (top_free_ptr != 0) {
                const node: *usize = @ptrFromInt(top_free_ptr + (slot_size - @sizeOf(usize)));
                global.frees[class] = node.*;
                break :a top_free_ptr;
            }

            const next_addr = global.next_addrs[class];
            if (next_addr % bigpage_size == 0) {
                const addr = allocBigPages(1);
                if (addr == 0) return null;
                //std.debug.print("allocated fresh slot_size={d} class={d} addr=0x{x}\n", .{
                //    slot_size, class, addr,
                //});
                global.next_addrs[class] = addr + slot_size;
                break :a addr;
            } else {
                global.next_addrs[class] = next_addr + slot_size;
                break :a next_addr;
            }
        };
        return @ptrFromInt(addr);
    }
    const bigpages_needed = bigPagesNeeded(actual_len);
    return @ptrFromInt(allocBigPages(bigpages_needed));
}

fn resize(
    ctx: *anyopaque,
    buf: []u8,
    alignment: Alignment,
    new_len: usize,
    return_address: usize,
) bool {
    _ = ctx;
    _ = return_address;
    // We don't want to move anything from one size class to another, but we
    // can recover bytes in between powers of two.
    const buf_align = alignment.toByteUnits();
    const old_actual_len = @max(buf.len + @sizeOf(usize), buf_align);
    const new_actual_len = @max(new_len +| @sizeOf(usize), buf_align);
    const old_small_slot_size = math.ceilPowerOfTwoAssert(usize, old_actual_len);
    const old_small_class = math.log2(old_small_slot_size) - min_class;
    if (old_small_class < size_class_count) {
        const new_small_slot_size = math.ceilPowerOfTwo(usize, new_actual_len) catch return false;
        return old_small_slot_size == new_small_slot_size;
    } else {
        const old_bigpages_needed = bigPagesNeeded(old_actual_len);
        const old_big_slot_pages = math.ceilPowerOfTwoAssert(usize, old_bigpages_needed);
        const new_bigpages_needed = bigPagesNeeded(new_actual_len);
        const new_big_slot_pages = math.ceilPowerOfTwo(usize, new_bigpages_needed) catch return false;
        return old_big_slot_pages == new_big_slot_pages;
    }
}

fn remap(
    context: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    new_len: usize,
    return_address: usize,
) ?[*]u8 {
    return if (resize(context, memory, alignment, new_len, return_address)) memory.ptr else null;
}

fn free(
    ctx: *anyopaque,
    buf: []u8,
    alignment: Alignment,
    return_address: usize,
) void {
    _ = ctx;
    _ = return_address;
    const buf_align = alignment.toByteUnits();
    const actual_len = @max(buf.len + @sizeOf(usize), buf_align);
    const slot_size = math.ceilPowerOfTwoAssert(usize, actual_len);
    const class = math.log2(slot_size) - min_class;
    const addr = @intFromPtr(buf.ptr);
    if (class < size_class_count) {
        const node: *usize = @ptrFromInt(addr + (slot_size - @sizeOf(usize)));
        node.* = global.frees[class];
        global.frees[class] = addr;
    } else {
        const bigpages_needed = bigPagesNeeded(actual_len);
        const pow2_pages = math.ceilPowerOfTwoAssert(usize, bigpages_needed);
        const big_slot_size_bytes = pow2_pages * bigpage_size;
        const node: *usize = @ptrFromInt(addr + (big_slot_size_bytes - @sizeOf(usize)));
        const big_class = math.log2(pow2_pages);
        node.* = global.big_frees[big_class];
        global.big_frees[big_class] = addr;
    }
}

inline fn bigPagesNeeded(byte_count: usize) usize {
    return (byte_count + (bigpage_size + (@sizeOf(usize) - 1))) / bigpage_size;
}

fn allocBigPages(n: usize) usize {
    const pow2_pages = math.ceilPowerOfTwoAssert(usize, n);
    const slot_size_bytes = pow2_pages * bigpage_size;
    const class = math.log2(pow2_pages);

    const top_free_ptr = global.big_frees[class];
    if (top_free_ptr != 0) {
        const node: *usize = @ptrFromInt(top_free_ptr + (slot_size_bytes - @sizeOf(usize)));
        global.big_frees[class] = node.*;
        return top_free_ptr;
    }

    if (builtin.cpu.arch.isWasm()) {
        comptime assert(std.heap.page_size_max == std.heap.page_size_min);
        const page_size = std.heap.page_size_max;
        const pages_per_bigpage = bigpage_size / page_size;
        const page_index = @wasmMemoryGrow(0, pow2_pages * pages_per_bigpage);
        if (page_index == -1) return 0;
        return @as(usize, @intCast(page_index)) * page_size;
    } else if (builtin.os.tag == .linux) {
        const prev_brk = global.prev_brk;
        const start_brk = if (prev_brk == 0)
            std.mem.alignForward(usize, std.os.linux.brk(0), bigpage_size)
        else
            prev_brk;
        const end_brk = start_brk + pow2_pages * bigpage_size;
        const new_prev_brk = std.os.linux.brk(end_brk);
        global.prev_brk = new_prev_brk;
        if (new_prev_brk != end_brk) return 0;
        return start_brk;
    } else {
        @compileError("no sbrk-like OS primitive available");
    }
}

const test_ally: Allocator = .{
    .ptr = undefined,
    .vtable = &vtable,
};

test "small allocations - free in same order" {
    var list: [513]*u64 = undefined;

    var i: usize = 0;
    while (i < 513) : (i += 1) {
        const ptr = try test_ally.create(u64);
        list[i] = ptr;
    }

    for (list) |ptr| {
        test_ally.destroy(ptr);
    }
}

test "small allocations - free in reverse order" {
    var list: [513]*u64 = undefined;

    var i: usize = 0;
    while (i < 513) : (i += 1) {
        const ptr = try test_ally.create(u64);
        list[i] = ptr;
    }

    i = list.len;
    while (i > 0) {
        i -= 1;
        const ptr = list[i];
        test_ally.destroy(ptr);
    }
}

test "large allocations" {
    const ptr1 = try test_ally.alloc(u64, 42768);
    const ptr2 = try test_ally.alloc(u64, 52768);
    test_ally.free(ptr1);
    const ptr3 = try test_ally.alloc(u64, 62768);
    test_ally.free(ptr3);
    test_ally.free(ptr2);
}

test "very large allocation" {
    try std.testing.expectError(error.OutOfMemory, test_ally.alloc(u8, math.maxInt(usize)));
}

test "realloc" {
    var slice = try test_ally.alignedAlloc(u8, .of(u32), 1);
    defer test_ally.free(slice);
    slice[0] = 0x12;

    // This reallocation should keep its pointer address.
    const old_slice = slice;
    slice = try test_ally.realloc(slice, 2);
    try std.testing.expect(old_slice.ptr == slice.ptr);
    try std.testing.expect(slice[0] == 0x12);
    slice[1] = 0x34;

    // This requires upgrading to a larger size class
    slice = try test_ally.realloc(slice, 17);
    try std.testing.expect(slice[0] == 0x12);
    try std.testing.expect(slice[1] == 0x34);
}

test "shrink" {
    var slice = try test_ally.alloc(u8, 20);
    defer test_ally.free(slice);

    @memset(slice, 0x11);

    try std.testing.expect(test_ally.resize(slice, 17));
    slice = slice[0..17];

    for (slice) |b| {
        try std.testing.expect(b == 0x11);
    }

    try std.testing.expect(test_ally.resize(slice, 16));
    slice = slice[0..16];

    for (slice) |b| {
        try std.testing.expect(b == 0x11);
    }
}

test "large object - grow" {
    if (builtin.os.tag == .linux) return error.SkipZigTest;

    var slice1 = try test_ally.alloc(u8, bigpage_size * 2 - 20);
    defer test_ally.free(slice1);

    const old = slice1;
    slice1 = try test_ally.realloc(slice1, bigpage_size * 2 - 10);
    try std.testing.expectEqual(slice1.ptr, old.ptr);

    slice1 = try test_ally.realloc(slice1, bigpage_size * 2);
    slice1 = try test_ally.realloc(slice1, bigpage_size * 2 + 1);
}

test "realloc small object to large object" {
    var slice = try test_ally.alloc(u8, 70);
    defer test_ally.free(slice);
    slice[0] = 0x12;
    slice[60] = 0x34;

    // This requires upgrading to a large object
    const large_object_size = bigpage_size * 2 + 50;
    slice = try test_ally.realloc(slice, large_object_size);
    try std.testing.expect(slice[0] == 0x12);
    try std.testing.expect(slice[60] == 0x34);
}

test "shrink large object to large object" {
    var slice = try test_ally.alloc(u8, bigpage_size * 2 + 50);
    defer test_ally.free(slice);
    slice[0] = 0x12;
    slice[60] = 0x34;

    try std.testing.expect(test_ally.resize(slice, bigpage_size * 2 + 1));
    slice = slice[0 .. bigpage_size * 2 + 1];
    try std.testing.expect(slice[0] == 0x12);
    try std.testing.expect(slice[60] == 0x34);

    try std.testing.expect(test_ally.resize(slice, bigpage_size * 2 + 1));
    try std.testing.expect(slice[0] == 0x12);
    try std.testing.expect(slice[60] == 0x34);

    slice = try test_ally.realloc(slice, bigpage_size * 2);
    try std.testing.expect(slice[0] == 0x12);
    try std.testing.expect(slice[60] == 0x34);
}

test "realloc large object to small object" {
    var slice = try test_ally.alloc(u8, bigpage_size * 2 + 50);
    defer test_ally.free(slice);
    slice[0] = 0x12;
    slice[16] = 0x34;

    slice = try test_ally.realloc(slice, 19);
    try std.testing.expect(slice[0] == 0x12);
    try std.testing.expect(slice[16] == 0x34);
}

test "objects of size 1024 and 2048" {
    const slice = try test_ally.alloc(u8, 1025);
    const slice2 = try test_ally.alloc(u8, 3000);

    test_ally.free(slice);
    test_ally.free(slice2);
}

test "standard allocator tests" {
    try std.heap.testAllocator(test_ally);
    try std.heap.testAllocatorAligned(test_ally);
}



---
File: /std/heap/debug_allocator.zig
---

//! An allocator that is intended to be used in Debug mode.
//!
//! ## Features
//!
//! * Captures stack traces on allocation, free, and optionally resize.
//! * Double free detection, which prints all three traces (first alloc, first
//!   free, second free).
//! * Leak detection, with stack traces.
//! * Never reuses memory addresses, making it easier for Zig to detect branch
//!   on undefined values in case of dangling pointers. This relies on
//!   the backing allocator to also not reuse addresses.
//! * Uses a minimum backing allocation size to avoid operating system errors
//!   from having too many active memory mappings.
//! * When a page of memory is no longer needed, give it back to resident
//!   memory as soon as possible, so that it causes page faults when used.
//! * Cross platform. Operates based on a backing allocator which makes it work
//!   everywhere, even freestanding.
//! * Compile-time configuration.
//!
//! These features require the allocator to be quite slow and wasteful. For
//! example, when allocating a single byte, the efficiency is less than 1%;
//! it requires more than 100 bytes of overhead to manage the allocation for
//! one byte. The efficiency gets better with larger allocations.
//!
//! ## Basic Design
//!
//! Allocations are divided into two categories, small and large.
//!
//! Small allocations are divided into buckets based on `page_size`:
//!
//! ```
//! index obj_size
//! 0     1
//! 1     2
//! 2     4
//! 3     8
//! 4     16
//! 5     32
//! 6     64
//! 7     128
//! 8     256
//! 9     512
//! 10    1024
//! 11    2048
//! ...
//! ```
//!
//! This goes on for `small_bucket_count` indexes.
//!
//! Allocations are grouped into an object size based on max(len, alignment),
//! rounded up to the next power of two.
//!
//! The main allocator state has an array of all the "current" buckets for each
//! size class. Each slot in the array can be null, meaning the bucket for that
//! size class is not allocated. When the first object is allocated for a given
//! size class, it makes one `page_size` allocation from the backing allocator.
//! This allocation is divided into "slots" - one per allocated object, leaving
//! room for the allocation metadata (starting with `BucketHeader`), which is
//! located at the very end of the "page".
//!
//! The allocation metadata includes "used bits" - 1 bit per slot representing
//! whether the slot is used. Allocations always take the next available slot
//! from the current bucket, setting the corresponding used bit, as well as
//! incrementing `allocated_count`.
//!
//! Frees recover the allocation metadata based on the address, length, and
//! alignment, relying on the backing allocation's large alignment, combined
//! with the fact that allocations are never moved from small to large, or vice
//! versa.
//!
//! When a bucket is full, a new one is allocated, containing a pointer to the
//! previous one. This doubly-linked list is iterated during leak detection.
//!
//! Resizing and remapping work the same on small allocations: if the size
//! class would not change, then the operation succeeds, and the address is
//! unchanged. Otherwise, the request is rejected.
//!
//! Large objects are allocated directly using the backing allocator. Metadata
//! is stored separately in a `std.HashMap` using the backing allocator.
//!
//! Resizing and remapping are forwarded directly to the backing allocator,
//! except where such operations would change the category from large to small.
const builtin = @import("builtin");
const StackTrace = std.debug.StackTrace;

const std = @import("std");
const log = std.log.scoped(.DebugAllocator);
const math = std.math;
const assert = std.debug.assert;
const mem = std.mem;
const Allocator =
```
