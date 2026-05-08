```
, .{R.name});
                    try stdout.flush();

                    const result_short = try benchmark(R, io, count, short_block_size);
                    try stdout.print("    {:5} MiB/s\n", .{result_short.throughput / (1 * MiB)});
                }
            }
        }
    }
    try stdout.flush();
}



---
File: /std/Random/ChaCha.zig
---

//! CSPRNG based on the ChaCha8 stream cipher, with forward security.
//!
//! References:
//! - Fast-key-erasure random-number generators https://blog.cr.yp.to/20170723-random.html

const std = @import("std");
const mem = std.mem;
const Self = @This();

const Cipher = std.crypto.stream.chacha.ChaCha8IETF;

const State = [8 * Cipher.block_length]u8;

state: State,
offset: usize,

const nonce = [_]u8{0} ** Cipher.nonce_length;

pub const secret_seed_length = Cipher.key_length;

/// The seed must be uniform, secret and `secret_seed_length` bytes long.
pub fn init(secret_seed: [secret_seed_length]u8) Self {
    var self: Self = .{ .state = undefined, .offset = 0 };
    Cipher.stream(&self.state, 0, secret_seed, nonce);
    return self;
}

/// Inserts entropy to refresh the internal state.
pub fn addEntropy(self: *Self, bytes: []const u8) void {
    var i: usize = 0;
    while (i + Cipher.key_length <= bytes.len) : (i += Cipher.key_length) {
        Cipher.xor(
            self.state[0..Cipher.key_length],
            self.state[0..Cipher.key_length],
            0,
            bytes[i..][0..Cipher.key_length].*,
            nonce,
        );
    }
    if (i < bytes.len) {
        var k = [_]u8{0} ** Cipher.key_length;
        const src = bytes[i..];
        @memcpy(k[0..src.len], src);
        Cipher.xor(
            self.state[0..Cipher.key_length],
            self.state[0..Cipher.key_length],
            0,
            k,
            nonce,
        );
    }
    self.refill();
}

/// Returns a `std.Random` structure backed by the current RNG.
pub fn random(self: *Self) std.Random {
    return std.Random.init(self, fill);
}

// Refills the buffer with random bytes, overwriting the previous key.
fn refill(self: *Self) void {
    Cipher.stream(&self.state, 0, self.state[0..Cipher.key_length].*, nonce);
    self.offset = 0;
}

/// Fills the buffer with random bytes.
pub fn fill(self: *Self, buf_: []u8) void {
    const bytes = self.state[Cipher.key_length..];
    var buf = buf_;

    const avail = bytes.len - self.offset;
    if (avail > 0) {
        // Bytes from the current block
        const n = @min(avail, buf.len);
        @memcpy(buf[0..n], bytes[self.offset..][0..n]);
        @memset(bytes[self.offset..][0..n], 0);
        buf = buf[n..];
        self.offset += n;
    }
    if (buf.len == 0) return;

    self.refill();

    // Full blocks
    while (buf.len >= bytes.len) {
        @memcpy(buf[0..bytes.len], bytes);
        buf = buf[bytes.len..];
        self.refill();
    }

    // Remaining bytes
    if (buf.len > 0) {
        @memcpy(buf, bytes[0..buf.len]);
        @memset(bytes[0..buf.len], 0);
        self.offset = buf.len;
    }
}



---
File: /std/Random/Isaac64.zig
---

//! ISAAC64 - http://www.burtleburtle.net/bob/rand/isaacafa.html
//!
//! Follows the general idea of the implementation from here with a few shortcuts.
//! https://doc.rust-lang.org/rand/src/rand/prng/isaac64.rs.html

const std = @import("std");
const mem = std.mem;
const Isaac64 = @This();

r: [256]u64,
m: [256]u64,
a: u64,
b: u64,
c: u64,
i: usize,

pub fn init(init_s: u64) Isaac64 {
    var isaac = Isaac64{
        .r = undefined,
        .m = undefined,
        .a = undefined,
        .b = undefined,
        .c = undefined,
        .i = undefined,
    };

    // seed == 0 => same result as the unseeded reference implementation
    isaac.seed(init_s, 1);
    return isaac;
}

pub fn random(self: *Isaac64) std.Random {
    return std.Random.init(self, fill);
}

fn step(self: *Isaac64, mix: u64, base: usize, comptime m1: usize, comptime m2: usize) void {
    const x = self.m[base + m1];
    self.a = mix +% self.m[base + m2];

    const y = self.a +% self.b +% self.m[@as(usize, @intCast((x >> 3) % self.m.len))];
    self.m[base + m1] = y;

    self.b = x +% self.m[@as(usize, @intCast((y >> 11) % self.m.len))];
    self.r[self.r.len - 1 - base - m1] = self.b;
}

fn refill(self: *Isaac64) void {
    const midpoint = self.r.len / 2;

    self.c +%= 1;
    self.b +%= self.c;

    {
        var i: usize = 0;
        while (i < midpoint) : (i += 4) {
            self.step(~(self.a ^ (self.a << 21)), i + 0, 0, midpoint);
            self.step(self.a ^ (self.a >> 5), i + 1, 0, midpoint);
            self.step(self.a ^ (self.a << 12), i + 2, 0, midpoint);
            self.step(self.a ^ (self.a >> 33), i + 3, 0, midpoint);
        }
    }

    {
        var i: usize = 0;
        while (i < midpoint) : (i += 4) {
            self.step(~(self.a ^ (self.a << 21)), i + 0, midpoint, 0);
            self.step(self.a ^ (self.a >> 5), i + 1, midpoint, 0);
            self.step(self.a ^ (self.a << 12), i + 2, midpoint, 0);
            self.step(self.a ^ (self.a >> 33), i + 3, midpoint, 0);
        }
    }

    self.i = 0;
}

fn next(self: *Isaac64) u64 {
    if (self.i >= self.r.len) {
        self.refill();
    }

    const value = self.r[self.i];
    self.i += 1;
    return value;
}

fn seed(self: *Isaac64, init_s: u64, comptime rounds: usize) void {
    // We ignore the multi-pass requirement since we don't currently expose full access to
    // seeding the self.m array completely.
    @memset(self.m[0..], 0);
    self.m[0] = init_s;

    // prescrambled golden ratio constants
    var a = [_]u64{
        0x647c4677a2884b7c,
        0xb9f8b322c73ac862,
        0x8c0ea5053d4712a0,
        0xb29b2e824a595524,
        0x82f053db8355e0ce,
        0x48fe4a0fa5a09315,
        0xae985bf2cbfc89ed,
        0x98f5704f6c44c0ab,
    };

    comptime var i: usize = 0;
    inline while (i < rounds) : (i += 1) {
        var j: usize = 0;
        while (j < self.m.len) : (j += 8) {
            comptime var x1: usize = 0;
            inline while (x1 < 8) : (x1 += 1) {
                a[x1] +%= self.m[j + x1];
            }

            a[0] -%= a[4];
            a[5] ^= a[7] >> 9;
            a[7] +%= a[0];
            a[1] -%= a[5];
            a[6] ^= a[0] << 9;
            a[0] +%= a[1];
            a[2] -%= a[6];
            a[7] ^= a[1] >> 23;
            a[1] +%= a[2];
            a[3] -%= a[7];
            a[0] ^= a[2] << 15;
            a[2] +%= a[3];
            a[4] -%= a[0];
            a[1] ^= a[3] >> 14;
            a[3] +%= a[4];
            a[5] -%= a[1];
            a[2] ^= a[4] << 20;
            a[4] +%= a[5];
            a[6] -%= a[2];
            a[3] ^= a[5] >> 17;
            a[5] +%= a[6];
            a[7] -%= a[3];
            a[4] ^= a[6] << 14;
            a[6] +%= a[7];

            comptime var x2: usize = 0;
            inline while (x2 < 8) : (x2 += 1) {
                self.m[j + x2] = a[x2];
            }
        }
    }

    @memset(self.r[0..], 0);
    self.a = 0;
    self.b = 0;
    self.c = 0;
    self.i = self.r.len; // trigger refill on first value
}

pub fn fill(self: *Isaac64, buf: []u8) void {
    var i: usize = 0;
    const aligned_len = buf.len - (buf.len & 7);

    // Fill complete 64-byte segments
    while (i < aligned_len) : (i += 8) {
        var n = self.next();
        comptime var j: usize = 0;
        inline while (j < 8) : (j += 1) {
            buf[i + j] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }

    // Fill trailing, ignoring excess (cut the stream).
    if (i != buf.len) {
        var n = self.next();
        while (i < buf.len) : (i += 1) {
            buf[i] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }
}

test "sequence" {
    var r = Isaac64.init(0);

    // from reference implementation
    const seq = [_]u64{
        0xf67dfba498e4937c,
        0x84a5066a9204f380,
        0xfee34bd5f5514dbb,
        0x4d1664739b8f80d6,
        0x8607459ab52a14aa,
        0x0e78bc5a98529e49,
        0xfe5332822ad13777,
        0x556c27525e33d01a,
        0x08643ca615f3149f,
        0xd0771faf3cb04714,
        0x30e86f68a37b008d,
        0x3074ebc0488a3adf,
        0x270645ea7a2790bc,
        0x5601a0a8d3763c6a,
        0x2f83071f53f325dd,
        0xb9090f3d42d2d2ea,
    };

    for (seq) |s| {
        try std.testing.expect(s == r.next());
    }
}

test fill {
    var r = Isaac64.init(0);

    // from reference implementation
    const seq = [_]u64{
        0xf67dfba498e4937c,
        0x84a5066a9204f380,
        0xfee34bd5f5514dbb,
        0x4d1664739b8f80d6,
        0x8607459ab52a14aa,
        0x0e78bc5a98529e49,
        0xfe5332822ad13777,
        0x556c27525e33d01a,
        0x08643ca615f3149f,
        0xd0771faf3cb04714,
        0x30e86f68a37b008d,
        0x3074ebc0488a3adf,
        0x270645ea7a2790bc,
        0x5601a0a8d3763c6a,
        0x2f83071f53f325dd,
        0xb9090f3d42d2d2ea,
    };

    for (seq) |s| {
        var buf0: [8]u8 = undefined;
        var buf1: [7]u8 = undefined;
        std.mem.writeInt(u64, &buf0, s, .little);
        r.fill(&buf1);
        try std.testing.expect(std.mem.eql(u8, buf0[0..7], buf1[0..]));
    }
}



---
File: /std/Random/lcg.zig
---

//! Linear congruential generator
//!
//! X(n+1) = (a * Xn + c) mod m
//!
//! PRNG

const std = @import("std");

/// Linear congruent generator where the modulo is `std.math.maxInt(T)`,
/// wrapping over the integer.
pub fn Wrapping(comptime T: type) type {
    return struct {
        xi: T,
        a: T,
        c: T,

        pub fn init(xi: T, a: T, c: T) LcgSelf {
            return .{ .xi = xi, .a = a, .c = c };
        }

        pub fn next(lcg: *LcgSelf) T {
            lcg.xi = (lcg.a *% lcg.xi) +% lcg.c;
            return lcg.xi;
        }

        const LcgSelf = @This();
    };
}



---
File: /std/Random/Pcg.zig
---

//! PCG32 - http://www.pcg-random.org/
//!
//! PRNG

const std = @import("std");
const Pcg = @This();

const default_multiplier = 6364136223846793005;

s: u64,
i: u64,

pub fn init(init_s: u64) Pcg {
    var pcg = Pcg{
        .s = undefined,
        .i = undefined,
    };

    pcg.seed(init_s);
    return pcg;
}

pub fn random(self: *Pcg) std.Random {
    return std.Random.init(self, fill);
}

fn next(self: *Pcg) u32 {
    const l = self.s;
    self.s = l *% default_multiplier +% (self.i | 1);

    const xor_s: u32 = @truncate(((l >> 18) ^ l) >> 27);
    const rot: u32 = @intCast(l >> 59);

    return (xor_s >> @as(u5, @intCast(rot))) | (xor_s << @as(u5, @intCast((0 -% rot) & 31)));
}

fn seed(self: *Pcg, init_s: u64) void {
    // Pcg requires 128-bits of seed.
    var gen = std.Random.SplitMix64.init(init_s);
    self.seedTwo(gen.next(), gen.next());
}

fn seedTwo(self: *Pcg, init_s: u64, init_i: u64) void {
    self.s = 0;
    self.i = (init_s << 1) | 1;
    self.s = self.s *% default_multiplier +% self.i;
    self.s +%= init_i;
    self.s = self.s *% default_multiplier +% self.i;
}

pub fn fill(self: *Pcg, buf: []u8) void {
    var i: usize = 0;
    const aligned_len = buf.len - (buf.len & 3);

    // Complete 4 byte segments.
    while (i < aligned_len) : (i += 4) {
        var n = self.next();
        comptime var j: usize = 0;
        inline while (j < 4) : (j += 1) {
            buf[i + j] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }

    // Remaining. (cuts the stream)
    if (i != buf.len) {
        var n = self.next();
        while (i < buf.len) : (i += 1) {
            buf[i] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }
}

test "sequence" {
    var r = Pcg.init(0);
    const s0: u64 = 0x9394bf54ce5d79de;
    const s1: u64 = 0x84e9c579ef59bbf7;
    r.seedTwo(s0, s1);

    const seq = [_]u32{
        2881561918,
        3063928540,
        1199791034,
        2487695858,
        1479648952,
        3247963454,
    };

    for (seq) |s| {
        try std.testing.expect(s == r.next());
    }
}

test fill {
    var r = Pcg.init(0);
    const s0: u64 = 0x9394bf54ce5d79de;
    const s1: u64 = 0x84e9c579ef59bbf7;
    r.seedTwo(s0, s1);

    const seq = [_]u32{
        2881561918,
        3063928540,
        1199791034,
        2487695858,
        1479648952,
        3247963454,
    };

    var i: u32 = 0;
    while (i < seq.len) : (i += 2) {
        var buf0: [8]u8 = undefined;
        std.mem.writeInt(u32, buf0[0..4], seq[i], .little);
        std.mem.writeInt(u32, buf0[4..8], seq[i + 1], .little);

        var buf1: [7]u8 = undefined;
        r.fill(&buf1);

        try std.testing.expect(std.mem.eql(u8, buf0[0..7], buf1[0..]));
    }
}



---
File: /std/Random/RomuTrio.zig
---

// Website: romu-random.org
// Reference paper:   http://arxiv.org/abs/2002.11331
// Beware: this PRNG is trivially predictable. While fast, it should *never* be used for cryptographic purposes.

const std = @import("std");
const math = std.math;
const RomuTrio = @This();

x_state: u64,
y_state: u64,
z_state: u64, // set to nonzero seed

pub fn init(init_s: u64) RomuTrio {
    var x = RomuTrio{ .x_state = undefined, .y_state = undefined, .z_state = undefined };
    x.seed(init_s);
    return x;
}

pub fn random(self: *RomuTrio) std.Random {
    return std.Random.init(self, fill);
}

fn next(self: *RomuTrio) u64 {
    const xp = self.x_state;
    const yp = self.y_state;
    const zp = self.z_state;
    self.x_state = 15241094284759029579 *% zp;
    self.y_state = yp -% xp;
    self.y_state = std.math.rotl(u64, self.y_state, 12);
    self.z_state = zp -% yp;
    self.z_state = std.math.rotl(u64, self.z_state, 44);
    return xp;
}

pub fn seedWithBuf(self: *RomuTrio, buf: [24]u8) void {
    const seed_buf = @as([3]u64, @bitCast(buf));
    self.x_state = seed_buf[0];
    self.y_state = seed_buf[1];
    self.z_state = seed_buf[2];
}

pub fn seed(self: *RomuTrio, init_s: u64) void {
    // RomuTrio requires 192-bits of seed.
    var gen = std.Random.SplitMix64.init(init_s);

    self.x_state = gen.next();
    self.y_state = gen.next();
    self.z_state = gen.next();
}

pub fn fill(self: *RomuTrio, buf: []u8) void {
    var i: usize = 0;
    const aligned_len = buf.len - (buf.len & 7);

    // Complete 8 byte segments.
    while (i < aligned_len) : (i += 8) {
        var n = self.next();
        comptime var j: usize = 0;
        inline while (j < 8) : (j += 1) {
            buf[i + j] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }

    // Remaining. (cuts the stream)
    if (i != buf.len) {
        var n = self.next();
        while (i < buf.len) : (i += 1) {
            buf[i] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }
}

test "sequence" {
    // Unfortunately there does not seem to be an official test sequence.
    var r = RomuTrio.init(0);

    const seq = [_]u64{
        16294208416658607535,
        13964609475759908645,
        4703697494102998476,
        3425221541186733346,
        2285772463536419399,
        9454187757529463048,
        13695907680080547496,
        8328236714879408626,
        12323357569716880909,
        12375466223337721820,
    };

    for (seq) |s| {
        try std.testing.expectEqual(s, r.next());
    }
}

test fill {
    // Unfortunately there does not seem to be an official test sequence.
    var r = RomuTrio.init(0);

    const seq = [_]u64{
        16294208416658607535,
        13964609475759908645,
        4703697494102998476,
        3425221541186733346,
        2285772463536419399,
        9454187757529463048,
        13695907680080547496,
        8328236714879408626,
        12323357569716880909,
        12375466223337721820,
    };

    for (seq) |s| {
        var buf0: [8]u8 = undefined;
        var buf1: [7]u8 = undefined;
        std.mem.writeInt(u64, &buf0, s, .little);
        r.fill(&buf1);
        try std.testing.expect(std.mem.eql(u8, buf0[0..7], buf1[0..]));
    }
}

test "buf seeding test" {
    const buf0 = @as([24]u8, @bitCast([3]u64{ 16294208416658607535, 13964609475759908645, 4703697494102998476 }));
    const resulting_state = .{ .x = 16294208416658607535, .y = 13964609475759908645, .z = 4703697494102998476 };
    var r = RomuTrio.init(0);
    r.seedWithBuf(buf0);
    try std.testing.expect(r.x_state == resulting_state.x);
    try std.testing.expect(r.y_state == resulting_state.y);
    try std.testing.expect(r.z_state == resulting_state.z);
}



---
File: /std/Random/Sfc64.zig
---

//! Sfc64 pseudo-random number generator from Practically Random.
//! Fastest engine of pracrand and smallest footprint.
//! See http://pracrand.sourceforge.net/

const std = @import("std");
const math = std.math;
const Sfc64 = @This();

a: u64 = undefined,
b: u64 = undefined,
c: u64 = undefined,
counter: u64 = undefined,

const Rotation = 24;
const RightShift = 11;
const LeftShift = 3;

pub fn init(init_s: u64) Sfc64 {
    var x = Sfc64{};

    x.seed(init_s);
    return x;
}

pub fn random(self: *Sfc64) std.Random {
    return std.Random.init(self, fill);
}

fn next(self: *Sfc64) u64 {
    const tmp = self.a +% self.b +% self.counter;
    self.counter += 1;
    self.a = self.b ^ (self.b >> RightShift);
    self.b = self.c +% (self.c << LeftShift);
    self.c = math.rotl(u64, self.c, Rotation) +% tmp;
    return tmp;
}

fn seed(self: *Sfc64, init_s: u64) void {
    self.a = init_s;
    self.b = init_s;
    self.c = init_s;
    self.counter = 1;
    var i: u32 = 0;
    while (i < 12) : (i += 1) {
        _ = self.next();
    }
}

pub fn fill(self: *Sfc64, buf: []u8) void {
    var i: usize = 0;
    const aligned_len = buf.len - (buf.len & 7);

    // Complete 8 byte segments.
    while (i < aligned_len) : (i += 8) {
        var n = self.next();
        comptime var j: usize = 0;
        inline while (j < 8) : (j += 1) {
            buf[i + j] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }

    // Remaining. (cuts the stream)
    if (i != buf.len) {
        var n = self.next();
        while (i < buf.len) : (i += 1) {
            buf[i] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }
}

test "sequence" {
    // Unfortunately there does not seem to be an official test sequence.
    var r = Sfc64.init(0);

    const seq = [_]u64{
        0x3acfa029e3cc6041,
        0xf5b6515bf2ee419c,
        0x1259635894a29b61,
        0xb6ae75395f8ebd6,
        0x225622285ce302e2,
        0x520d28611395cb21,
        0xdb909c818901599d,
        0x8ffd195365216f57,
        0xe8c4ad5e258ac04a,
        0x8f8ef2c89fdb63ca,
        0xf9865b01d98d8e2f,
        0x46555871a65d08ba,
        0x66868677c6298fcd,
        0x2ce15a7e6329f57d,
        0xb2f1833ca91ca79,
        0x4b0890ac9bf453ca,
    };

    for (seq) |s| {
        try std.testing.expectEqual(s, r.next());
    }
}

test fill {
    // Unfortunately there does not seem to be an official test sequence.
    var r = Sfc64.init(0);

    const seq = [_]u64{
        0x3acfa029e3cc6041,
        0xf5b6515bf2ee419c,
        0x1259635894a29b61,
        0xb6ae75395f8ebd6,
        0x225622285ce302e2,
        0x520d28611395cb21,
        0xdb909c818901599d,
        0x8ffd195365216f57,
        0xe8c4ad5e258ac04a,
        0x8f8ef2c89fdb63ca,
        0xf9865b01d98d8e2f,
        0x46555871a65d08ba,
        0x66868677c6298fcd,
        0x2ce15a7e6329f57d,
        0xb2f1833ca91ca79,
        0x4b0890ac9bf453ca,
    };

    for (seq) |s| {
        var buf0: [8]u8 = undefined;
        var buf1: [7]u8 = undefined;
        std.mem.writeInt(u64, &buf0, s, .little);
        r.fill(&buf1);
        try std.testing.expect(std.mem.eql(u8, buf0[0..7], buf1[0..]));
    }
}



---
File: /std/Random/SplitMix64.zig
---

//! Generator to extend 64-bit seed values into longer sequences.
//!
//! The number of cycles is thus limited to 64-bits regardless of the engine, but this
//! is still plenty for practical purposes.

const SplitMix64 = @This();

s: u64,

pub fn init(seed: u64) SplitMix64 {
    return SplitMix64{ .s = seed };
}

pub fn next(self: *SplitMix64) u64 {
    self.s +%= 0x9e3779b97f4a7c15;

    var z = self.s;
    z = (z ^ (z >> 30)) *% 0xbf58476d1ce4e5b9;
    z = (z ^ (z >> 27)) *% 0x94d049bb133111eb;
    return z ^ (z >> 31);
}



---
File: /std/Random/test.zig
---




---
File: /std/Random/Xoroshiro128.zig
---

//! Xoroshiro128+ - http://xoroshiro.di.unimi.it/
//!
//! PRNG

const std = @import("std");
const math = std.math;
const Xoroshiro128 = @This();

s: [2]u64,

pub fn init(init_s: u64) Xoroshiro128 {
    var x = Xoroshiro128{ .s = undefined };

    x.seed(init_s);
    return x;
}

pub fn random(self: *Xoroshiro128) std.Random {
    return std.Random.init(self, fill);
}

pub fn next(self: *Xoroshiro128) u64 {
    const s0 = self.s[0];
    var s1 = self.s[1];
    const r = s0 +% s1;

    s1 ^= s0;
    self.s[0] = math.rotl(u64, s0, @as(u8, 55)) ^ s1 ^ (s1 << 14);
    self.s[1] = math.rotl(u64, s1, @as(u8, 36));

    return r;
}

// Skip 2^64 places ahead in the sequence
pub fn jump(self: *Xoroshiro128) void {
    var s0: u64 = 0;
    var s1: u64 = 0;

    const table = [_]u64{
        0xbeac0467eba5facb,
        0xd86b048b86aa9922,
    };

    inline for (table) |entry| {
        var b: usize = 0;
        while (b < 64) : (b += 1) {
            if ((entry & (@as(u64, 1) << @as(u6, @intCast(b)))) != 0) {
                s0 ^= self.s[0];
                s1 ^= self.s[1];
            }
            _ = self.next();
        }
    }

    self.s[0] = s0;
    self.s[1] = s1;
}

pub fn seed(self: *Xoroshiro128, init_s: u64) void {
    // Xoroshiro requires 128-bits of seed.
    var gen = std.Random.SplitMix64.init(init_s);

    self.s[0] = gen.next();
    self.s[1] = gen.next();
}

pub fn fill(self: *Xoroshiro128, buf: []u8) void {
    var i: usize = 0;
    const aligned_len = buf.len - (buf.len & 7);

    // Complete 8 byte segments.
    while (i < aligned_len) : (i += 8) {
        var n = self.next();
        comptime var j: usize = 0;
        inline while (j < 8) : (j += 1) {
            buf[i + j] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }

    // Remaining. (cuts the stream)
    if (i != buf.len) {
        var n = self.next();
        while (i < buf.len) : (i += 1) {
            buf[i] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }
}

test "sequence" {
    var r = Xoroshiro128.init(0);
    r.s[0] = 0xaeecf86f7878dd75;
    r.s[1] = 0x01cd153642e72622;

    const seq1 = [_]u64{
        0xb0ba0da5bb600397,
        0x18a08afde614dccc,
        0xa2635b956a31b929,
        0xabe633c971efa045,
        0x9ac19f9706ca3cac,
        0xf62b426578c1e3fb,
    };

    for (seq1) |s| {
        try std.testing.expect(s == r.next());
    }

    r.jump();

    const seq2 = [_]u64{
        0x95344a13556d3e22,
        0xb4fb32dafa4d00df,
        0xb2011d9ccdcfe2dd,
        0x05679a9b2119b908,
        0xa860a1da7c9cd8a0,
        0x658a96efe3f86550,
    };

    for (seq2) |s| {
        try std.testing.expect(s == r.next());
    }
}

test fill {
    var r = Xoroshiro128.init(0);
    r.s[0] = 0xaeecf86f7878dd75;
    r.s[1] = 0x01cd153642e72622;

    const seq = [_]u64{
        0xb0ba0da5bb600397,
        0x18a08afde614dccc,
        0xa2635b956a31b929,
        0xabe633c971efa045,
        0x9ac19f9706ca3cac,
        0xf62b426578c1e3fb,
    };

    for (seq) |s| {
        var buf0: [8]u8 = undefined;
        var buf1: [7]u8 = undefined;
        std.mem.writeInt(u64, &buf0, s, .little);
        r.fill(&buf1);
        try std.testing.expect(std.mem.eql(u8, buf0[0..7], buf1[0..]));
    }
}



---
File: /std/Random/Xoshiro256.zig
---

//! Xoshiro256++ - http://xoroshiro.di.unimi.it/
//!
//! PRNG

const std = @import("std");
const math = std.math;
const Xoshiro256 = @This();

s: [4]u64,

pub fn init(init_s: u64) Xoshiro256 {
    var x = Xoshiro256{
        .s = undefined,
    };

    x.seed(init_s);
    return x;
}

pub fn random(self: *Xoshiro256) std.Random {
    return std.Random.init(self, fill);
}

pub fn next(self: *Xoshiro256) u64 {
    const r = math.rotl(u64, self.s[0] +% self.s[3], 23) +% self.s[0];

    const t = self.s[1] << 17;

    self.s[2] ^= self.s[0];
    self.s[3] ^= self.s[1];
    self.s[1] ^= self.s[2];
    self.s[0] ^= self.s[3];

    self.s[2] ^= t;

    self.s[3] = math.rotl(u64, self.s[3], 45);

    return r;
}

// Skip 2^128 places ahead in the sequence
pub fn jump(self: *Xoshiro256) void {
    var s: u256 = 0;

    var table: u256 = 0x39abdc4529b1661ca9582618e03fc9aad5a61266f0c9392c180ec6d33cfd0aba;

    while (table != 0) : (table >>= 1) {
        if (@as(u1, @truncate(table)) != 0) {
            s ^= @as(u256, @bitCast(self.s));
        }
        _ = self.next();
    }

    self.s = @as([4]u64, @bitCast(s));
}

pub fn seed(self: *Xoshiro256, init_s: u64) void {
    // Xoshiro requires 256-bits of seed.
    var gen = std.Random.SplitMix64.init(init_s);

    self.s[0] = gen.next();
    self.s[1] = gen.next();
    self.s[2] = gen.next();
    self.s[3] = gen.next();
}

pub fn fill(self: *Xoshiro256, buf: []u8) void {
    var i: usize = 0;
    const aligned_len = buf.len - (buf.len & 7);

    // Complete 8 byte segments.
    while (i < aligned_len) : (i += 8) {
        var n = self.next();
        comptime var j: usize = 0;
        inline while (j < 8) : (j += 1) {
            buf[i + j] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }

    // Remaining. (cuts the stream)
    if (i != buf.len) {
        var n = self.next();
        while (i < buf.len) : (i += 1) {
            buf[i] = @as(u8, @truncate(n));
            n >>= 8;
        }
    }
}

test "sequence" {
    if (@import("builtin").zig_backend == .stage2_c) return error.SkipZigTest;

    var r = Xoshiro256.init(0);

    const seq1 = [_]u64{
        0x53175d61490b23df,
        0x61da6f3dc380d507,
        0x5c0fdf91ec9a7bfc,
        0x02eebf8c3bbe5e1a,
        0x7eca04ebaf4a5eea,
        0x0543c37757f08d9a,
    };

    for (seq1) |s| {
        try std.testing.expect(s == r.next());
    }

    r.jump();

    const seq2 = [_]u64{
        0xae1db5c5e27807be,
        0xb584c6a7fd8709fe,
        0xc46a0ee9330fb6e,
        0xdc0c9606f49ed76e,
        0x1f5bb6540f6651fb,
        0x72fa2ca734601488,
    };

    for (seq2) |s| {
        try std.testing.expect(s == r.next());
    }
}

test fill {
    var r = Xoshiro256.init(0);

    const seq = [_]u64{
        0x53175d61490b23df,
        0x61da6f3dc380d507,
        0x5c0fdf91ec9a7bfc,
        0x02eebf8c3bbe5e1a,
        0x7eca04ebaf4a5eea,
        0x0543c37757f08d9a,
    };

    for (seq) |s| {
        var buf0: [8]u8 = undefined;
        var buf1: [7]u8 = undefined;
        std.mem.writeInt(u64, &buf0, s, .little);
        r.fill(&buf1);
        try std.testing.expect(std.mem.eql(u8, buf0[0..7], buf1[0..]));
    }
}



---
File: /std/Random/ziggurat.zig
---

//! Implements [ZIGNOR][1] (Jurgen A. Doornik, 2005, Nuffield College, Oxford).
//!
//! [1]: https://www.doornik.com/research/ziggurat.pdf
//!
//! rust/rand used as a reference;
//!
//! NOTE: This seems interesting but reference code is a bit hard to grok:
//! https://sbarral.github.io/etf.

const std = @import("../std.zig");
const builtin = @import("builtin");
const math = std.math;
const Random = std.Random;

pub fn next_f64(random: Random, comptime tables: ZigTable) f64 {
    while (true) {
        // We manually construct a float from parts as we can avoid an extra random lookup here by
        // using the unused exponent for the lookup table entry.
        const bits = random.int(u64);
        const i = @as(usize, @as(u8, @truncate(bits)));

        const u = blk: {
            if (tables.is_symmetric) {
                // Generate a value in the range [2, 4) and scale into [-1, 1)
                const repr = ((0x3ff + 1) << 52) | (bits >> 12);
                break :blk @as(f64, @bitCast(repr)) - 3.0;
            } else {
                // Generate a value in the range [1, 2) and scale into (0, 1)
                const repr = (0x3ff << 52) | (bits >> 12);
                break :blk @as(f64, @bitCast(repr)) - (1.0 - math.floatEps(f64) / 2.0);
            }
        };

        const x = u * tables.x[i];
        const test_x = if (tables.is_symmetric) @abs(x) else x;

        // equivalent to |u| < tables.x[i+1] / tables.x[i] (or u < tables.x[i+1] / tables.x[i])
        if (test_x < tables.x[i + 1]) {
            return x;
        }

        if (i == 0) {
            return tables.zero_case(random, u);
        }

        // equivalent to f1 + DRanU() * (f0 - f1) < 1
        if (tables.f[i + 1] + (tables.f[i] - tables.f[i + 1]) * random.float(f64) < tables.pdf(x)) {
            return x;
        }
    }
}

pub const ZigTable = struct {
    r: f64,
    x: [257]f64,
    f: [257]f64,

    // probability density function used as a fallback
    pdf: fn (f64) f64,
    // whether the distribution is symmetric
    is_symmetric: bool,
    // fallback calculation in the case we are in the 0 block
    zero_case: fn (Random, f64) f64,
};

// zigNorInit
pub fn ZigTableGen(
    comptime is_symmetric: bool,
    comptime r: f64,
    comptime v: f64,
    comptime f: fn (f64) f64,
    comptime f_inv: fn (f64) f64,
    comptime zero_case: fn (Random, f64) f64,
) ZigTable {
    var tables: ZigTable = undefined;

    tables.is_symmetric = is_symmetric;
    tables.r = r;
    tables.pdf = f;
    tables.zero_case = zero_case;

    tables.x[0] = v / f(r);
    tables.x[1] = r;

    for (tables.x[2..256], 0..) |*entry, i| {
        const last = tables.x[2 + i - 1];
        entry.* = f_inv(v / last + f(last));
    }
    tables.x[256] = 0;

    for (tables.f[0..], 0..) |*entry, i| {
        entry.* = f(tables.x[i]);
    }

    return tables;
}

// N(0, 1)
pub const NormDist = blk: {
    @setEvalBranchQuota(30000);
    break :blk ZigTableGen(true, norm_r, norm_v, norm_f, norm_f_inv, norm_zero_case);
};

pub const norm_r = 3.6541528853610088;
pub const norm_v = 0.00492867323399;

pub fn norm_f(x: f64) f64 {
    return @exp(-x * x / 2.0);
}
pub fn norm_f_inv(y: f64) f64 {
    return @sqrt(-2.0 * @log(y));
}
pub fn norm_zero_case(random: Random, u: f64) f64 {
    var x: f64 = 1;
    var y: f64 = 0;

    while (-2.0 * y < x * x) {
        x = @log(random.float(f64)) / norm_r;
        y = @log(random.float(f64));
    }

    if (u < 0) {
        return x - norm_r;
    } else {
        return norm_r - x;
    }
}

test "normal dist smoke test" {
    // Hardcode 0 as the seed because it's possible a seed exists that fails
    // this test.
    var prng = Random.DefaultPrng.init(0);
    const random = prng.random();

    var i: usize = 0;
    while (i < 1000) : (i += 1) {
        _ = random.floatNorm(f64);
    }
}

// Exp(1)
pub const ExpDist = blk: {
    @setEvalBranchQuota(30000);
    break :blk ZigTableGen(false, exp_r, exp_v, exp_f, exp_f_inv, exp_zero_case);
};

pub const exp_r = 7.69711747013104972;
pub const exp_v = 0.0039496598225815571993;

pub fn exp_f(x: f64) f64 {
    return @exp(-x);
}
pub fn exp_f_inv(y: f64) f64 {
    return -@log(y);
}
pub fn exp_zero_case(random: Random, _: f64) f64 {
    return exp_r - @log(random.float(f64));
}

test "exp dist smoke test" {
    var prng = Random.DefaultPrng.init(0);
    const random = prng.random();

    var i: usize = 0;
    while (i < 1000) : (i += 1) {
        _ = random.floatExp(f64);
    }
}

test {
    _ = NormDist;
}



---
File: /std/sort/block.zig
---

const builtin = @import("builtin");
const std = @import("../std.zig");
const sort = std.sort;
const math = std.math;
const mem = std.mem;

const Range = struct {
    start: usize,
    end: usize,

    fn init(start: usize, end: usize) Range {
        return Range{
            .start = start,
            .end = end,
        };
    }

    fn length(self: Range) usize {
        return self.end - self.start;
    }
};

const Iterator = struct {
    size: usize,
    power_of_two: usize,
    numerator: usize,
    decimal: usize,
    denominator: usize,
    decimal_step: usize,
    numerator_step: usize,

    fn init(size2: usize, min_level: usize) Iterator {
        const power_of_two = math.floorPowerOfTwo(usize, size2);
        const denominator = power_of_two / min_level;
        return Iterator{
            .numerator = 0,
            .decimal = 0,
            .size = size2,
            .power_of_two = power_of_two,
            .denominator = denominator,
            .decimal_step = size2 / denominator,
            .numerator_step = size2 % denominator,
        };
    }

    fn begin(self: *Iterator) void {
        self.numerator = 0;
        self.decimal = 0;
    }

    fn nextRange(self: *Iterator) Range {
        const start = self.decimal;

        self.decimal += self.decimal_step;
        self.numerator += self.numerator_step;
        if (self.numerator >= self.denominator) {
            self.numerator -= self.denominator;
            self.decimal += 1;
        }

        return Range{
            .start = start,
            .end = self.decimal,
        };
    }

    fn finished(self: *Iterator) bool {
        return self.decimal >= self.size;
    }

    fn nextLevel(self: *Iterator) bool {
        self.decimal_step += self.decimal_step;
        self.numerator_step += self.numerator_step;
        if (self.numerator_step >= self.denominator) {
            self.numerator_step -= self.denominator;
            self.decimal_step += 1;
        }

        return (self.decimal_step < self.size);
    }

    fn length(self: *Iterator) usize {
        return self.decimal_step;
    }
};

const Pull = struct {
    from: usize,
    to: usize,
    count: usize,
    range: Range,
};

/// Stable in-place sort. O(n) best case, O(n*log(n)) worst case and average case.
/// O(1) memory (no allocator required).
/// Sorts in ascending order with respect to the given `lessThan` function.
///
/// NOTE: The algorithm only works when the comparison is less-than or greater-than.
///       (See https://github.com/ziglang/zig/issues/8289)
pub fn block(
    comptime T: type,
    items: []T,
    context: anytype,
    comptime lessThanFn: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    const lessThan = if (builtin.mode == .Debug) struct {
        fn lessThan(ctx: @TypeOf(context), lhs: T, rhs: T) bool {
            const lt = lessThanFn(ctx, lhs, rhs);
            const gt = lessThanFn(ctx, rhs, lhs);
            std.debug.assert(!(lt and gt));
            return lt;
        }
    }.lessThan else lessThanFn;

    // Implementation ported from https://github.com/BonzaiThePenguin/WikiSort/blob/master/WikiSort.c
    var cache: [512]T = undefined;

    if (items.len < 4) {
        if (items.len == 3) {
            // hard coded insertion sort
            if (lessThan(context, items[1], items[0])) mem.swap(T, &items[0], &items[1]);
            if (lessThan(context, items[2], items[1])) {
                mem.swap(T, &items[1], &items[2]);
                if (lessThan(context, items[1], items[0])) mem.swap(T, &items[0], &items[1]);
            }
        } else if (items.len == 2) {
            if (lessThan(context, items[1], items[0])) mem.swap(T, &items[0], &items[1]);
        }
        return;
    }

    // sort groups of 4-8 items at a time using an unstable sorting network,
    // but keep track of the original item orders to force it to be stable
    // http://pages.ripco.net/~jgamble/nw.html
    var iterator = Iterator.init(items.len, 4);
    while (!iterator.finished()) {
        var order = [_]u8{ 0, 1, 2, 3, 4, 5, 6, 7 };
        const range = iterator.nextRange();

        const sliced_items = items[range.start..];
        switch (range.length()) {
            8 => {
                swap(T, sliced_items, &order, 0, 1, context, lessThan);
                swap(T, sliced_items, &order, 2, 3, context, lessThan);
                swap(T, sliced_items, &order, 4, 5, context, lessThan);
                swap(T, sliced_items, &order, 6, 7, context, lessThan);
                swap(T, sliced_items, &order, 0, 2, context, lessThan);
                swap(T, sliced_items, &order, 1, 3, context, lessThan);
                swap(T, sliced_items, &order, 4, 6, context, lessThan);
                swap(T, sliced_items, &order, 5, 7, context, lessThan);
                swap(T, sliced_items, &order, 1, 2, context, lessThan);
                swap(T, sliced_items, &order, 5, 6, context, lessThan);
                swap(T, sliced_items, &order, 0, 4, context, lessThan);
                swap(T, sliced_items, &order, 3, 7, context, lessThan);
                swap(T, sliced_items, &order, 1, 5, context, lessThan);
                swap(T, sliced_items, &order, 2, 6, context, lessThan);
                swap(T, sliced_items, &order, 1, 4, context, lessThan);
                swap(T, sliced_items, &order, 3, 6, context, lessThan);
                swap(T, sliced_items, &order, 2, 4, context, lessThan);
                swap(T, sliced_items, &order, 3, 5, context, lessThan);
                swap(T, sliced_items, &order, 3, 4, context, lessThan);
            },
            7 => {
                swap(T, sliced_items, &order, 1, 2, context, lessThan);
                swap(T, sliced_items, &order, 3, 4, context, lessThan);
                swap(T, sliced_items, &order, 5, 6, context, lessThan);
                swap(T, sliced_items, &order, 0, 2, context, lessThan);
                swap(T, sliced_items, &order, 3, 5, context, lessThan);
                swap(T, sliced_items, &order, 4, 6, context, lessThan);
                swap(T, sliced_items, &order, 0, 1, context, lessThan);
                swap(T, sliced_items, &order, 4, 5, context, lessThan);
                swap(T, sliced_items, &order, 2, 6, context, lessThan);
                swap(T, sliced_items, &order, 0, 4, context, lessThan);
                swap(T, sliced_items, &order, 1, 5, context, lessThan);
                swap(T, sliced_items, &order, 0, 3, context, lessThan);
                swap(T, sliced_items, &order, 2, 5, context, lessThan);
                swap(T, sliced_items, &order, 1, 3, context, lessThan);
                swap(T, sliced_items, &order, 2, 4, context, lessThan);
                swap(T, sliced_items, &order, 2, 3, context, lessThan);
            },
            6 => {
                swap(T, sliced_items, &order, 1, 2, context, lessThan);
                swap(T, sliced_items, &order, 4, 5, context, lessThan);
                swap(T, sliced_items, &order, 0, 2, context, lessThan);
                swap(T, sliced_items, &order, 3, 5, context, lessThan);
                swap(T, sliced_items, &order, 0, 1, context, lessThan);
                swap(T, sliced_items, &order, 3, 4, context, lessThan);
                swap(T, sliced_items, &order, 2, 5, context, lessThan);
                swap(T, sliced_items, &order, 0, 3, context, lessThan);
                swap(T, sliced_items, &order, 1, 4, context, lessThan);
                swap(T, sliced_items, &order, 2, 4, context, lessThan);
                swap(T, sliced_items, &order, 1, 3, context, lessThan);
                swap(T, sliced_items, &order, 2, 3, context, lessThan);
            },
            5 => {
                swap(T, sliced_items, &order, 0, 1, context, lessThan);
                swap(T, sliced_items, &order, 3, 4, context, lessThan);
                swap(T, sliced_items, &order, 2, 4, context, lessThan);
                swap(T, sliced_items, &order, 2, 3, context, lessThan);
                swap(T, sliced_items, &order, 1, 4, context, lessThan);
                swap(T, sliced_items, &order, 0, 3, context, lessThan);
                swap(T, sliced_items, &order, 0, 2, context, lessThan);
                swap(T, sliced_items, &order, 1, 3, context, lessThan);
                swap(T, sliced_items, &order, 1, 2, context, lessThan);
            },
            4 => {
                swap(T, sliced_items, &order, 0, 1, context, lessThan);
                swap(T, sliced_items, &order, 2, 3, context, lessThan);
                swap(T, sliced_items, &order, 0, 2, context, lessThan);
                swap(T, sliced_items, &order, 1, 3, context, lessThan);
                swap(T, sliced_items, &order, 1, 2, context, lessThan);
            },
            else => {},
        }
    }
    if (items.len < 8) return;

    // then merge sort the higher levels, which can be 8-15, 16-31, 32-63, 64-127, etc.
    while (true) {
        // if every A and B block will fit into the cache, use a special branch
        // specifically for merging with the cache
        // (we use < rather than <= since the block size might be one more than
        // iterator.length())
        if (iterator.length() < cache.len) {
            // if four subarrays fit into the cache, it's faster to merge both
            // pairs of subarrays into the cache,
            // then merge the two merged subarrays from the cache back into the original array
            if ((iterator.length() + 1) * 4 <= cache.len and iterator.length() * 4 <= items.len) {
                iterator.begin();
                while (!iterator.finished()) {
                    // merge A1 and B1 into the cache
                    var A1 = iterator.nextRange();
                    var B1 = iterator.nextRange();
                    var A2 = iterator.nextRange();
                    var B2 = iterator.nextRange();

                    if (lessThan(context, items[B1.end - 1], items[A1.start])) {
                        // the two ranges are in reverse order, so copy them in reverse order into the cache
                        const a1_items = items[A1.start..A1.end];
                        @memcpy(cache[B1.length()..][0..a1_items.len], a1_items);
                        const b1_items = items[B1.start..B1.end];
                        @memcpy(cache[0..b1_items.len], b1_items);
                    } else if (lessThan(context, items[B1.start], items[A1.end - 1])) {
                        // these two ranges weren't already in order, so merge them into the cache
                        mergeInto(T, items, A1, B1, cache[0..], context, lessThan);
                    } else {
                        // if A1, B1, A2, and B2 are all in order, skip doing anything else
                        if (!lessThan(context, items[B2.start], items[A2.end - 1]) and !lessThan(context, items[A2.start], items[B1.end - 1])) continue;

                        // copy A1 and B1 into the cache in the same order
                        const a1_items = items[A1.start..A1.end];
                        @memcpy(cache[0..a1_items.len], a1_items);
                        const b1_items = items[B1.start..B1.end];
                        @memcpy(cache[A1.length()..][0..b1_items.len], b1_items);
                    }
                    A1 = Range.init(A1.start, B1.end);

                    // merge A2 and B2 into the cache
                    if (lessThan(context, items[B2.end - 1], items[A2.start])) {
                        // the two ranges are in reverse order, so copy them in reverse order into the cache
                        const a2_items = items[A2.start..A2.end];
                        @memcpy(cache[A1.length() + B2.length() ..][0..a2_items.len], a2_items);
                        const b2_items = items[B2.start..B2.end];
                        @memcpy(cache[A1.length()..][0..b2_items.len], b2_items);
                    } else if (lessThan(context, items[B2.start], items[A2.end - 1])) {
                        // these two ranges weren't already in order, so merge them into the cache
                        mergeInto(T, items, A2, B2, cache[A1.length()..], context, lessThan);
                    } else {
                        // copy A2 and B2 into the cache in the same order
                        const a2_items = items[A2.start..A2.end];
                        @memcpy(cache[A1.length()..][0..a2_items.len], a2_items);
                        const b2_items = items[B2.start..B2.end];
                        @memcpy(cache[A1.length() + A2.length() ..][0..b2_items.len], b2_items);
                    }
                    A2 = Range.init(A2.start, B2.end);

                    // merge A1 and A2 from the cache into the items
                    const A3 = Range.init(0, A1.length());
                    const B3 = Range.init(A1.length(), A1.length() + A2.length());

                    if (lessThan(context, cache[B3.end - 1], cache[A3.start])) {
                        // the two ranges are in reverse order, so copy them in reverse order into the items
                        const a3_items = cache[A3.start..A3.end];
                        @memcpy(items[A1.start + A2.length() ..][0..a3_items.len], a3_items);
                        const b3_items = cache[B3.start..B3.end];
                        @memcpy(items[A1.start..][0..b3_items.len], b3_items);
                    } else if (lessThan(context, cache[B3.start], cache[A3.end - 1])) {
                        // these two ranges weren't already in order, so merge them back into the items
                        mergeInto(T, cache[0..], A3, B3, items[A1.start..], context, lessThan);
                    } else {
                        // copy A3 and B3 into the items in the same order
                        const a3_items = cache[A3.start..A3.end];
                        @memcpy(items[A1.start..][0..a3_items.len], a3_items);
                        const b3_items = cache[B3.start..B3.end];
                        @memcpy(items[A1.start + A1.length() ..][0..b3_items.len], b3_items);
                    }
                }

                // we merged two levels at the same time, so we're done with this level already
                // (iterator.nextLevel() is called again at the bottom of this outer merge loop)
                _ = iterator.nextLevel();
            } else {
                iterator.begin();
                while (!iterator.finished()) {
                    const A = iterator.nextRange();
                    const B = iterator.nextRange();

                    if (lessThan(context, items[B.end - 1], items[A.start])) {
                        // the two ranges are in reverse order, so a simple rotation should fix it
                        mem.rotate(T, items[A.start..B.end], A.length());
                    } else if (lessThan(context, items[B.start], items[A.end - 1])) {
                        // these two ranges weren't already in order, so we'll need to merge them!
                        const a_items = items[A.start..A.end];
                        @memcpy(cache[0..a_items.len], a_items);
                        mergeExternal(T, items, A, B, cache[0..], context, lessThan);
                    }
                }
            }
        } else {
            // this is where the in-place merge logic starts!
            // 1. pull out two internal buffers each containing √A unique values
            //    1a. adjust block_size and buffer_size if we couldn't find enough unique values
            // 2. loop over the A and B subarrays within this level of the merge sort
            // 3. break A and B into blocks of size 'block_size'
            // 4. "tag" each of the A blocks with values from the first internal buffer
            // 5. roll the A blocks through the B blocks and drop/rotate them where they belong
            // 6. merge each A block with any B values that follow, using the cache or the second internal buffer
            // 7. sort the second internal buffer if it exists
            // 8. redistribute the two internal buffers back into the items
            var block_size: usize = math.sqrt(iterator.length());
            var buffer_size = iterator.length() / block_size + 1;

            // as an optimization, we really only need to pull out the internal buffers once for each level of merges
            // after that we can reuse the same buffers over and over, then redistribute it when we're finished with this level
            var A: Range = undefined;
            var B: Range = undefined;
            var index: usize = 0;
            var last: usize = 0;
            var count: usize = 0;
            var find: usize = 0;
            var start: usize = 0;
            var pull_index: usize = 0;
            var pull = [_]Pull{
                Pull{
                    .from = 0,
                    .to = 0,
                    .count = 0,
                    .range = Range.init(0, 0),
                },
                Pull{
                    .from = 0,
                    .to = 0,
                    .count = 0,
                    .range = Range.init(0, 0),
                },
            };

            var buffer1 = Range.init(0, 0);
            var buffer2 = Range.init(0, 0);

            // find two internal buffers of size 'buffer_size' each
            find = buffer_size + buffer_size;
            var find_separately = false;

            if (block_size <= cache.len) {
                // if every A block fits into the cache then we won't need the second internal buffer,
                // so we really only need to find 'buffer_size' unique values
                find = buffer_size;
            } else if (find > iterator.length()) {
                // we can't fit both buffers into the same A or B subarray, so find two buffers separately
                find = buffer_size;
                find_separately = true;
            }

            // we need to find either a single contiguous space containing 2√A unique values (which will be split up into two buffers of size √A each),
            // or we need to find one buffer of < 2√A unique values, and a second buffer of √A unique values,
            // OR if we couldn't find that many unique values, we need the largest possible buffer we can get

            // in the case where it couldn't find a single buffer of at least √A unique values,
            // all of the Merge steps must be replaced by a different merge algorithm (MergeInPlace)
            iterator.begin();
            while (!iterator.finished()) {
                A = iterator.nextRange();
                B = iterator.nextRange();

                // just store information about where the values will be pulled from and to,
                // as well as how many values there are, to create the two internal buffers

                // check A for the number of unique values we need to fill an internal buffer
                // these values will be pulled out to the start of A
                last = A.start;
                count = 1;
                while (count < find) : ({
                    last = index;
                    count += 1;
                }) {
                    index = findLastForward(T, items, items[last], Range.init(last + 1, A.end), find - count, context, lessThan);
                    if (index == A.end) break;
                }
                index = last;

                if (count >= buffer_size) {
                    // keep track of the range within the items where we'll need to "pull out" these values to create the internal buffer
                    pull[pull_index] = Pull{
                        .range = Range.init(A.start, B.end),
                        .count = count,
                        .from = index,
                        .to = A.start,
                    };
                    pull_index = 1;

                    if (count == buffer_size + buffer_size) {
                        // we were able to find a single contiguous section containing 2√A unique values,
                        // so this section can be used to contain both of the internal buffers we'll need
                        buffer1 = Range.init(A.start, A.start + buffer_size);
                        buffer2 = Range.init(A.start + buffer_size, A.start + count);
                        break;
                    } else if (find == buffer_size + buffer_size) {
                        // we found a buffer that contains at least √A unique values, but did not contain the full 2√A unique values,
                        // so we still need to find a second separate buffer of at least √A unique values
                        buffer1 = Range.init(A.start, A.start + count);
                        find = buffer_size;
                    } else if (block_size <= cache.len) {
                        // we found the first and only internal buffer that we need, so we're done!
                        buffer1 = Range.init(A.start, A.start + count);
                        break;
                    } else if (find_separately) {
                        // found one buffer, but now find the other one
                        buffer1 = Range.init(A.start, A.start + count);
                        find_separately = false;
                    } else {
                        // we found a second buffer in an 'A' subarray containing √A unique values, so we're done!
                        buffer2 = Range.init(A.start, A.start + count);
                        break;
                    }
                } else if (pull_index == 0 and count > buffer1.length()) {
                    // keep track of the largest buffer we were able to find
                    buffer1 = Range.init(A.start, A.start + count);
                    pull[pull_index] = Pull{
                        .range = Range.init(A.start, B.end),
                        .count = count,
                        .from = index,
                        .to = A.start,
                    };
                }

                // check B for the number of unique values we need to fill an internal buffer
                // these values will be pulled out to the end of B
                last = B.end - 1;
                count = 1;
                while (count < find) : ({
                    last = index - 1;
                    count += 1;
                }) {
                    index = findFirstBackward(T, items, items[last], Range.init(B.start, last), find - count, context, lessThan);
                    if (index == B.start) break;
                }
                index = last;

                if (count >= buffer_size) {
                    // keep track of the range within the items where we'll need to "pull out" these values to create the internal buffe
                    pull[pull_index] = Pull{
                        .range = Range.init(A.start, B.end),
                        .count = count,
                        .from = index,
                        .to = B.end,
                    };
                    pull_index = 1;

                    if (count == buffer_size + buffer_size) {
                        // we were able to find a single contiguous section containing 2√A unique values,
                        // so this section can be used to contain both of the internal buffers we'll need
                        buffer1 = Range.init(B.end - count, B.end - buffer_size);
                        buffer2 = Range.init(B.end - buffer_size, B.end);
                        break;
                    } else if (find == buffer_size + buffer_size) {
                        // we found a buffer that contains at least √A unique values, but did not contain the full 2√A unique values,
                        // so we still need to find a second separate buffer of at least √A unique values
                        buffer1 = Range.init(B.end - count, B.end);
                        find = buffer_size;
                    } else if (block_size <= cache.len) {
                        // we found the first and only internal buffer that we need, so we're done!
                        buffer1 = Range.init(B.end - count, B.end);
                        break;
                    } else if (find_separately) {
                        // found one buffer, but now find the other one
                        buffer1 = Range.init(B.end - count, B.end);
                        find_separately = false;
                    } else {
                        // buffer2 will be pulled out from a 'B' subarray, so if the first buffer was pulled out from the corresponding 'A' subarray,
                        // we need to adjust the end point for that A subarray so it knows to stop redistributing its values before reaching buffer2
                        if (pull[0].range.start == A.start) pull[0].range.end -= pull[1].count;

                        // we found a second buffer in an 'B' subarray containing √A unique values, so we're done!
                        buffer2 = Range.init(B.end - count, B.end);
                        break;
                    }
                } else if (pull_index == 0 and count > buffer1.length()) {
                    // keep track of the largest buffer we were able to find
                    buffer1 = Range.init(B.end - count, B.end);
                    pull[pull_index] = Pull{
                        .range = Range.init(A.start, B.end),
                        .count = count,
                        .from = index,
                        .to = B.end,
                    };
                }
            }

            // pull out the two ranges so we can use them as internal buffers
            pull_index = 0;
            while (pull_index < 2) : (pull_index += 1) {
                const length = pull[pull_index].count;

                if (pull[pull_index].to < pull[pull_index].from) {
                    // we're pulling the values out to the left, which means the start of an A subarray
                    index = pull[pull_index].from;
                    count = 1;
                    while (count < length) : (count += 1) {
                        index = findFirstBackward(T, items, items[index - 1], Range.init(pull[pull_index].to, pull[pull_index].from - (count - 1)), length - count, context, lessThan);
                        const range = Range.init(index + 1, pull[pull_index].from + 1);
                        mem.rotate(T, items[range.start..range.end], range.length() - count);
                        pull[pull_index].from = index + count;
                    }
                } else if (pull[pull_index].to > pull[pull_index].from) {
                    // we're pulling values out to the right, which means the end of a B subarray
                    index = pull[pull_index].from + 1;
                    count = 1;
                    while (count < length) : (count += 1) {
                        index = findLastForward(T, items, items[index], Range.init(index, pull[pull_index].to), length - count, context, lessThan);
                        const range = Range.init(pull[pull_index].from, index - 1);
                        mem.rotate(T, items[range.start..range.end], count);
                        pull[pull_index].from = index - 1 - count;
                    }
                }
            }

            // adjust block_size and buffer_size based on the values we were able to pull out
            buffer_size = buffer1.length();
            block_size = iterator.length() / buffer_size + 1;

            // the first buffer NEEDS to be large enough to tag each of the evenly sized A blocks,
            // so this was originally here to test the math for adjusting block_size above
            // assert((iterator.length() + 1)/block_size <= buffer_size);

            // now that the two internal buffers have been created, it's time to merge each A+B combination at this level of the merge sort!
            iterator.begin();
            while (!iterator.finished()) {
                A = iterator.nextRange();
                B = iterator.nextRange();

                // remove any parts of A or B that are being used by the internal buffers
                start = A.start;
                if (start == pull[0].range.start) {
                    if (pull[0].from > pull[0].to) {
                        A.start += pull[0].count;

                        // if the internal buffer takes up the entire A or B subarray, then there's nothing to merge
                        // this only happens for very small subarrays, like √4 = 2, 2 * (2 internal buffers) = 4,
                        // which also only happens when cache.len is small or 0 since it'd otherwise use MergeExternal
                        if (A.length() == 0) continue;
                    } else if (pull[0].from < pull[0].to) {
                        B.end -= pull[0].count;
                        if (B.length() == 0) continue;
                    }
                }
                if (start == pull[1].range.start) {
                    if (pull[1].from > pull[1].to) {
                        A.start += pull[1].count;
                        if (A.length() == 0) continue;
                    } else if (pull[1].from < pull[1].to) {
                        B.end -= pull[1].count;
                        if (B.length() == 0) continue;
                    }
                }

                if (lessThan(context, items[B.end - 1], items[A.start])) {
                    // the two ranges are in reverse order, so a simple rotation should fix it
                    mem.rotate(T, items[A.start..B.end], A.length());
                } else if (lessThan(context, items[A.end], items[A.end - 1])) {
                    // these two ranges weren't already in order, so we'll need to merge them!
                    var findA: usize = undefined;

                    // break the remainder of A into blocks. firstA is the uneven-sized first A block
                    var blockA = Range.init(A.start, A.end);
                    var firstA = Range.init(A.start, A.start + blockA.length() % block_size);

                    // swap the first value of each A block with the value in buffer1
                    var indexA = buffer1.start;
                    index = firstA.end;
                    while (index < blockA.end) : ({
                        indexA += 1;
                        index += block_size;
                    }) {
                        mem.swap(T, &items[indexA], &items[index]);
                    }

                    // start rolling the A blocks through the B blocks!
                    // whenever we leave an A block behind, we'll need to merge the previous A block with any B blocks that follow it, so track that information as well
                    var lastA = firstA;
                    var lastB = Range.init(0, 0);
                    var blockB = Range.init(B.start, B.start + @min(block_size, B.length()));
                    blockA.start += firstA.length();
                    indexA = buffer1.start;

                    // if the first unevenly sized A block fits into the cache, copy it there for when we go to Merge it
                    // otherwise, if the second buffer is available, block swap the contents into that
                    if (lastA.length() <= cache.len) {
                        const last_a_items = items[lastA.start..lastA.end];
                        @memcpy(cache[0..last_a_items.len], last_a_items);
                    } else if (buffer2.length() > 0) {
                        blockSwap(T, items, lastA.start, buffer2.start, lastA.length());
                    }

                    if (blockA.length() > 0) {
                        while (true) {
                            // if there's a previous B block and the first value of the minimum A block is <= the last value of the previous B block,
                            // then drop that minimum A block behind. or if there are no B blocks left then keep dropping the remaining A blocks.
                            if ((lastB.length() > 0 and !lessThan(context, items[lastB.end - 1], items[indexA])) or blockB.length() == 0) {
                                // figure out where to split the previous B block, and rotate it at the split
                                const B_split = binaryFirst(T, items, items[indexA], lastB, context, lessThan);
                                const B_remaining = lastB.end - B_split;

                                // swap the minimum A block to the beginning of the rolling A blocks
                                var minA = blockA.start;
                                findA = minA + block_size;
                                while (findA < blockA.end) : (findA += block_size) {
                                    if (lessThan(context, items[findA], items[minA])) {
                                        minA = findA;
                                    }
                                }
                                blockSwap(T, items, blockA.start, minA, block_size);

                                // swap the first item of the previous A block back with its original value, which is stored in buffer1
                                mem.swap(T, &items[blockA.start], &items[indexA]);
                                indexA += 1;

                                // locally merge the previous A block with the B values that follow it
                                // if lastA fits into the external cache we'll use that (with MergeExternal),
                                // or if the second internal buffer exists we'll use that (with MergeInternal),
                                // or failing that we'll use a strictly in-place merge algorithm (MergeInPlace)

                                if (lastA.length() <= cache.len) {
                                    mergeExternal(T, items, lastA, Range.init(lastA.end, B_split), cache[0..], context, lessThan);
                                } else if (buffer2.length() > 0) {
                                    mergeInternal(T, items, lastA, Range.init(lastA.end, B_split), buffer2, context, lessThan);
                                } else {
                                    mergeInPlace(T, items, lastA, Range.init(lastA.end, B_split), context, lessThan);
                                }

                                if (buffer2.length() > 0 or block_size <= cache.len) {
                                    // copy the previous A block into the cache or buffer2, since that's where we need it to be when we go to merge it anyway
                                    if (block_size <= cache.len) {
                                        @memcpy(cache[0..block_size], items[blockA.start..][0..block_size]);
                                    } else {
                                        blockSwap(T, items, blockA.start, buffer2.start, block_size);
                                    }

                                    // this is equivalent to rotating, but faster
                                    // the area normally taken up by the A block is either the contents of buffer2, or data we don't need anymore since we memcopied it
                                    // either way, we don't need to retain the order of those items, so instead of rotating we can just block swap B to where it belongs
                                    blockSwap(T, items, B_split, blockA.start + block_size - B_remaining, B_remaining);
                                } else {
                                    // we are unable to use the 'buffer2' trick to speed up the rotation operation since buffer2 doesn't exist, so perform a normal rotation
                                    mem.rotate(T, items[B_split .. blockA.start + block_size], blockA.start - B_split);
                                }

                                // update the range for the remaining A blocks, and the range remaining from the B block after it was split
                                lastA = Range.init(blockA.start - B_remaining, blockA.start - B_remaining + block_size);
                                lastB = Range.init(lastA.end, lastA.end + B_remaining);

                                // if there are no more A blocks remaining, this step is finished!
                                blockA.start += block_size;
                                if (blockA.length() == 0) break;
                            } else if (blockB.length() < block_size) {
                                // move the last B block, which is unevenly sized, to before the remaining A blocks, by using a rotation
                                // the cache is disabled here since it might contain the contents of the previous A block
                                mem.rotate(T, items[blockA.start..blockB.end], blockB.start - blockA.start);

                                lastB = Range.init(blockA.start, blockA.start + blockB.length());
                                blockA.start += blockB.length();
                                blockA.end += blockB.length();
                                blockB.end = blockB.start;
                            } else {
                                // roll the leftmost A block to the end by swapping it with the next B block
                                blockSwap(T, items, blockA.start, blockB.start, block_size);
                                lastB = Range.init(blockA.start, blockA.start + block_size);

                                blockA.start += block_size;
                                blockA.end += block_size;
                                blockB.start += block_size;

                                if (blockB.end > B.end - block_size) {
                                    blockB.end = B.end;
                                } else {
                                    blockB.end += block_size;
                                }
                            }
                        }
                    }

                    // merge the last A block with the remaining B values
                    if (lastA.length() <= cache.len) {
                        mergeExternal(T, items, lastA, Range.init(lastA.end, B.end), cache[0..], context, lessThan);
                    } else if (buffer2.length() > 0) {
                        mergeInternal(T, items, lastA, Range.init(lastA.end, B.end), buffer2, context, lessThan);
                    } else {
                        mergeInPlace(T, items, lastA, Range.init(lastA.end, B.end), context, lessThan);
                    }
                }
            }

            // when we're finished with this merge step we should have the one
            // or two internal buffers left over, where the second buffer is all jumbled up
            // insertion sort the second buffer, then redistribute the buffers
            // back into the items using the opposite process used for creating the buffer

            // while an unstable sort like quicksort could be applied here, in benchmarks
            // it was consistently slightly slower than a simple insertion sort,
            // even for tens of millions of items. this may be because insertion
            // sort is quite fast when the data is already somewhat sorted, like it is here
            sort.insertion(T, items[buffer2.start..buffer2.end], context, lessThan);

            pull_index = 0;
            while (pull_index < 2) : (pull_index += 1) {
                var unique = pull[pull_index].count * 2;
                if (pull[pull_index].from > pull[pull_index].to) {
                    // the values were pulled out to the left, so redistribute them back to the right
                    var buffer = Range.init(pull[pull_index].range.start, pull[pull_index].range.start + pull[pull_index].count);
                    while (buffer.length() > 0) {
                        index = findFirstForward(T, items, items[buffer.start], Range.init(buffer.end, pull[pull_index].range.end), unique, context, lessThan);
                        const amount = index - buffer.end;
                        mem.rotate(T, items[buffer.start..index], buffer.length());
                        buffer.start += (amount + 1);
                        buffer.end += amount;
                        unique -= 2;
                    }
                } else if (pull[pull_index].from < pull[pull_index].to) {
                    // the values were pulled out to the right, so redistribute them back to the left
                    var buffer = Range.init(pull[pull_index].range.end - pull[pull_index].count, pull[pull_index].range.end);
                    while (buffer.length() > 0) {
                        index = findLastBackward(T, items, items[buffer.end - 1], Range.init(pull[pull_index].range.start, buffer.start), unique, context, lessThan);
                        const amount = buffer.start - index;
                        mem.rotate(T, items[index..buffer.end], amount);
                        buffer.start -= amount;
                        buffer.end -= (amount + 1);
                        unique -= 2;
                    }
                }
            }
        }

        // double the size of each A and B subarray that will be merged in the next level
        if (!iterator.nextLevel()) break;
    }
}
// merge operation without a buffer
fn mergeInPlace(
    comptime T: type,
    items: []T,
    A_arg: Range,
    B_arg: Range,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    if (A_arg.length() == 0 or B_arg.length() == 0) return;

    // this just repeatedly binary searches into B and rotates A into position.
    // the paper suggests using the 'rotation-based Hwang and Lin algorithm' here,
    // but I decided to stick with this because it had better situational performance
    //
    // (Hwang and Lin is designed for merging subarrays of very different sizes,
    // but WikiSort almost always uses subarrays that are roughly the same size)
    //
    // normally this is incredibly suboptimal, but this function is only called
    // when none of the A or B blocks in any subarray contained 2√A unique values,
    // which places a hard limit on the number of times this will ACTUALLY need
    // to binary search and rotate.
    //
    // according to my analysis the worst case is √A rotations performed on √A items
    // once the constant factors are removed, which ends up being O(n)
    //
    // again, this is NOT a general-purpose solution – it only works well in this case!
    // kind of like how the O(n^2) insertion sort is used in some places

    var A = A_arg;
    var B = B_arg;

    while (true) {
        // find the first place in B where the first item in A needs to be inserted
        const mid = binaryFirst(T, items, items[A.start], B, context, lessThan);

        // rotate A into place
        const amount = mid - A.end;
        mem.rotate(T, items[A.start..mid], A.length());
        if (B.end == mid) break;

        // calculate the new A and B ranges
        B.start = mid;
        A = Range.init(A.start + amount, B.start);
        A.start = binaryLast(T, items, items[A.start], A, context, lessThan);
        if (A.length() == 0) break;
    }
}

// merge operation using an internal buffer
fn mergeInternal(
    comptime T: type,
    items: []T,
    A: Range,
    B: Range,
    buffer: Range,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    // whenever we find a value to add to the final array, swap it with the value that's already in that spot
    // when this algorithm is finished, 'buffer' will contain its original contents, but in a different order
    var A_count: usize = 0;
    var B_count: usize = 0;
    var insert: usize = 0;

    if (B.length() > 0 and A.length() > 0) {
        while (true) {
            if (!lessThan(context, items[B.start + B_count], items[buffer.start + A_count])) {
                mem.swap(T, &items[A.start + insert], &items[buffer.start + A_count]);
                A_count += 1;
                insert += 1;
                if (A_count >= A.length()) break;
            } else {
                mem.swap(T, &items[A.start + insert], &items[B.start + B_count]);
                B_count += 1;
                insert += 1;
                if (B_count >= B.length()) break;
            }
        }
    }

    // swap the remainder of A into the final array
    blockSwap(T, items, buffer.start + A_count, A.start + insert, A.length() - A_count);
}

fn blockSwap(comptime T: type, items: []T, start1: usize, start2: usize, block_size: usize) void {
    var index: usize = 0;
    while (index < block_size) : (index += 1) {
        mem.swap(T, &items[start1 + index], &items[start2 + index]);
    }
}

// combine a linear search with a binary search to reduce the number of comparisons in situations
// where have some idea as to how many unique values there are and where the next value might be
fn findFirstForward(
    comptime T: type,
    items: []T,
    value: T,
    range: Range,
    unique: usize,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) usize {
    if (range.length() == 0) return range.start;
    const skip = @max(range.length() / unique, @as(usize, 1));

    var index = range.start + skip;
    while (lessThan(context, items[index - 1], value)) : (index += skip) {
        if (index >= range.end - skip) {
            return binaryFirst(T, items, value, Range.init(index, range.end), context, lessThan);
        }
    }

    return binaryFirst(T, items, value, Range.init(index - skip, index), context, lessThan);
}

fn findFirstBackward(
    comptime T: type,
    items: []T,
    value: T,
    range: Range,
    unique: usize,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) usize {
    if (range.length() == 0) return range.start;
    const skip = @max(range.length() / unique, @as(usize, 1));

    var index = range.end - skip;
    while (index > range.start and !lessThan(context, items[index - 1], value)) : (index -= skip) {
        if (index < range.start + skip) {
            return binaryFirst(T, items, value, Range.init(range.start, index), context, lessThan);
        }
    }

    return binaryFirst(T, items, value, Range.init(index, index + skip), context, lessThan);
}

fn findLastForward(
    comptime T: type,
    items: []T,
    value: T,
    range: Range,
    unique: usize,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) usize {
    if (range.length() == 0) return range.start;
    const skip = @max(range.length() / unique, @as(usize, 1));

    var index = range.start + skip;
    while (!lessThan(context, value, items[index - 1])) : (index += skip) {
        if (index >= range.end - skip) {
            return binaryLast(T, items, value, Range.init(index, range.end), context, lessThan);
        }
    }

    return binaryLast(T, items, value, Range.init(index - skip, index), context, lessThan);
}

fn findLastBackward(
    comptime T: type,
    items: []T,
    value: T,
    range: Range,
    unique: usize,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) usize {
    if (range.length() == 0) return range.start;
    const skip = @max(range.length() / unique, @as(usize, 1));

    var index = range.end - skip;
    while (index > range.start and lessThan(context, value, items[index - 1])) : (index -= skip) {
        if (index < range.start + skip) {
            return binaryLast(T, items, value, Range.init(range.start, index), context, lessThan);
        }
    }

    return binaryLast(T, items, value, Range.init(index, index + skip), context, lessThan);
}

fn binaryFirst(
    comptime T: type,
    items: []T,
    value: T,
    range: Range,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) usize {
    var curr = range.start;
    var size = range.length();
    if (range.start >= range.end) return range.end;
    while (size > 0) {
        const offset = size % 2;

        size /= 2;
        const mid_item = items[curr + size];
        if (lessThan(context, mid_item, value)) {
            curr += size + offset;
        }
    }
    return curr;
}

fn binaryLast(
    comptime T: type,
    items: []T,
    value: T,
    range: Range,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) usize {
    var curr = range.start;
    var size = range.length();
    if (range.start >= range.end) return range.end;
    while (size > 0) {
        const offset = size % 2;

        size /= 2;
        const mid_item = items[curr + size];
        if (!lessThan(context, value, mid_item)) {
            curr += size + offset;
        }
    }
    return curr;
}

fn mergeInto(
    comptime T: type,
    from: []T,
    A: Range,
    B: Range,
    into: []T,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    var A_index: usize = A.start;
    var B_index: usize = B.start;
    const A_last = A.end;
    const B_last = B.end;
    var insert_index: usize = 0;

    while (true) {
        if (!lessThan(context, from[B_index], from[A_index])) {
            into[insert_index] = from[A_index];
            A_index += 1;
            insert_index += 1;
            if (A_index == A_last) {
                // copy the remainder of B into the final array
                const from_b = from[B_index..B_last];
                @memcpy(into[insert_index..][0..from_b.len], from_b);
                break;
            }
        } else {
            into[insert_index] = from[B_index];
            B_index += 1;
            insert_index += 1;
            if (B_index == B_last) {
                // copy the remainder of A into the final array
                const from_a = from[A_index..A_last];
                @memcpy(into[insert_index..][0..from_a.len], from_a);
                break;
            }
        }
    }
}

fn mergeExternal(
    comptime T: type,
    items: []T,
    A: Range,
    B: Range,
    cache: []T,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    // A fits into the cache, so use that instead of the internal buffer
    var A_index: usize = 0;
    var B_index: usize = B.start;
    var insert_index: usize = A.start;
    const A_last = A.length();
    const B_last = B.end;

    if (B.length() > 0 and A.length() > 0) {
        while (true) {
            if (!lessThan(context, items[B_index], cache[A_index])) {
                items[insert_index] = cache[A_index];
                A_index += 1;
                insert_index += 1;
                if (A_index == A_last) break;
            } else {
                items[insert_index] = items[B_index];
                B_index += 1;
                insert_index += 1;
                if (B_index == B_last) break;
            }
        }
    }

    // copy the remainder of A into the final array
    const cache_a = cache[A_index..A_last];
    @memcpy(items[insert_index..][0..cache_a.len], cache_a);
}

fn swap(
    comptime T: type,
    items: []T,
    order: *[8]u8,
    x: usize,
    y: usize,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    if (lessThan(context, items[y], items[x]) or ((order.*)[x] > (order.*)[y] and !lessThan(context, items[x], items[y]))) {
        mem.swap(T, &items[x], &items[y]);
        mem.swap(u8, &(order.*)[x], &(order.*)[y]);
    }
}



---
File: /std/sort/pdq.zig
---

const std = @import("../std.zig");
const sort = std.sort;
const mem = std.mem;
const math = std.math;
const testing = std.testing;

/// Unstable in-place sort. n best case, n*log(n) worst case and average case.
/// log(n) memory (no allocator required).
///
/// Sorts in ascending order with respect to the given `lessThan` function.
pub fn pdq(
    comptime T: type,
    items: []T,
    context: anytype,
    comptime lessThanFn: fn (context: @TypeOf(context), lhs: T, rhs: T) bool,
) void {
    const Context = struct {
        items: []T,
        sub_ctx: @TypeOf(context),

        pub fn lessThan(ctx: @This(), a: usize, b: usize) bool {
            return lessThanFn(ctx.sub_ctx, ctx.items[a], ctx.items[b]);
        }

        pub fn swap(ctx: @This(), a: usize, b: usize) void {
            return mem.swap(T, &ctx.items[a], &ctx.items[b]);
        }
    };
    pdqContext(0, items.len, Context{ .items = items, .sub_ctx = context });
}

const Hint = enum {
    increasing,
    decreasing,
    unknown,
};

/// Unstable in-place sort. O(n) best case, O(n*log(n)) worst case and average case.
/// O(log(n)) memory (no allocator required).
/// `context` must have methods `swap` and `lessThan`,
/// which each take 2 `usize` parameters indicating the index of an item.
/// Sorts in ascending order with respect to `lessThan`.
pub fn pdqContext(a: usize, b: usize, context: anytype) void {
    // slices of up to this length get sorted using insertion sort.
    const max_insertion = 24;
    // number of allowed imbalanced partitions before switching to heap sort.
    const max_limit = std.math.floorPowerOfTwo(usize, b - a) + 1;

    // set upper bound on stack memory usage.
    const Range = struct { a: usize, b: usize, limit: usize };
    const stack_size = math.log2(math.maxInt(usize) + 1);
    var stack: [stack_size]Range = undefined;
    var range = Range{ .a = a, .b = b, .limit = max_limit };
    var top: usize = 0;

    while (true) {
        var was_balanced = true;
        var was_partitioned = true;

        while (true) {
            const len = range.b - range.a;

            // very short slices get sorted using insertion sort.
            if (len <= max_insertion) {
                break sort.insertionContext(range.a, range.b, context);
            }

            // if too many bad pivot choices were made, simply fall back to heapsort in order to
            // guarantee O(n*log(n)) worst-case.
            if (range.limit == 0) {
                break sort.heapContext(range.a, range.b, context);
            }

            // if the last partitioning was imbalanced, try breaking patterns in the slice by shuffling
            // some elements around. Hopefully we'll choose a better pivot this time.
            if (!was_balanced) {
                breakPatterns(range.a, range.b, context);
                range.limit -= 1;
            }

            // choose a pivot and try guessing whether the slice is already sorted.
            var pivot: usize = 0;
            var hint = chosePivot(range.a, range.b, &pivot, context);

            if (hint == .decreasing) {
                // The maximum number of swaps was performed, so items are likely
                // in reverse order. Reverse it to make sorting faster.
                reverseRange(range.a, range.b, context);
                pivot = (range.b - 1) - (pivot - range.a);
                hint = .increasing;
            }

            // if the last partitioning was decently balanced and didn't shuffle elements, and if pivot
            // selection predicts the slice is likely already sorted...
            if (was_balanced and was_partitioned and hint == .increasing) {
                // try identifying several out-of-order elements and shifting them to correct
                // positions. If the slice ends up being completely sorted, we're done.
                if (partialInsertionSort(range.a, range.b, context)) break;
            }

            // if the chosen pivot is equal to the predecessor, then it's the smallest element in the
            // slice. Partition the slice into elements equal to and elements greater than the pivot.
            // This case is usually hit when the slice contains many duplicate elements.
            if (range.a > a and !context.lessThan(range.a - 1, pivot)) {
                range.a = partitionEqual(range.a, range.b, pivot, context);
                continue;
            }

            // partition the slice.
            var mid = pivot;
            was_partitioned = partition(range.a, range.b, &mid, context);

            const left_len = mid - range.a;
            const right_len = range.b - mid;
            const balanced_threshold = len / 8;
            if (left_len < right_len) {
                was_balanced = left_len >= balanced_threshold;
                stack[top] = .{ .a = range.a, .b = mid, .limit = range.limit };
                top += 1;
                range.a = mid + 1;
            } else {
                was_balanced = right_len >= balanced_threshold;
                stack[top] = .{ .a = mid + 1, .b = range.b, .limit = range.limit };
                top += 1;
                range.b = mid;
            }
        }

        top = math.sub(usize, top, 1) catch break;
        range = stack[top];
    }
}

/// partitions `items[a..b]` into elements smaller than `items[pivot]`,
/// followed by elements greater than or equal to `items[pivot]`.
///
/// sets the new pivot.
/// returns `true` if already partitioned.
fn partition(a: usize, b: usize, pivot: *usize, context: anytype) bool {
    // move pivot to the first place
    context.swap(a, pivot.*);

    var i = a + 1;
    var j = b - 1;

    while (i <= j and context.lessThan(i, a)) i += 1;
    while (i <= j and !context.lessThan(j, a)) j -= 1;

    // check if items are already partitioned (no item to swap)
    if (i > j) {
        // put pivot back to the middle
        context.swap(j, a);
        pivot.* = j;
        return true;
    }

    context.swap(i, j);
    i += 1;
    j -= 1;

    while (true) {
        while (i <= j and context.lessThan(i, a)) i += 1;
        while (i <= j and !context.lessThan(j, a)) j -= 1;
        if (i > j) break;

        context.swap(i, j);
        i += 1;
        j -= 1;
    }

    // TODO: Enable the BlockQuicksort optimization

    context.swap(j, a);
    pivot.* = j;
    return false;
}

/// partitions items into elements equal to `items[pivot]`
/// followed by elements greater than `items[pivot]`.
///
/// it assumed that `items[a..b]` does not contain elements smaller than the `items[pivot]`.
fn partitionEqual(a: usize, b: usize, pivot: usize, context: anytype) usize {
    // move pivot to the first place
    context.swap(a, pivot);

    var i = a + 1;
    var j = b - 1;

    while (true) {
        while (i <= j and !context.lessThan(a, i)) i += 1;
        while (i <= j and context.lessThan(a, j)) j -= 1;
        if (i > j) break;

        context.swap(i, j);
        i += 1;
        j -= 1;
    }

    return i;
}

/// partially sorts a slice by shifting several out-of-order elements around.
///
/// returns `true` if the slice is sorted at the end. This function is `O(n)` worst-case.
fn partialInsertionSort(a: usize, b: usize, context: anytype) bool {
    @branchHint(.cold);

    // maximum number of adjacent out-of-order pairs that will get shifted
    const max_steps = 5;
    // if the slice is shorter than this, don't shift any elements
    const shortest_shifting = 50;

    var i = a + 1;
    for (0..max_steps) |_| {
        // find the next pair of adjacent out-of-order elements.
        while (i < b and !context.lessThan(i, i - 1)) i += 1;

        // are we done?
        if (i == b) return true;

        // don't shift elements on short arrays, that has a performance cost.
        if (b - a < shortest_shifting) return false;

        // swap the found pair of elements. This puts them in correct order.
        context.swap(i, i - 1);

        // shift the smaller element to the left.
        if (i - a >= 2) {
            var j = i - 1;
            while (j > a) : (j -= 1) {
                if (!context.lessThan(j, j - 1)) break;
                context.swap(j, j - 1);
            }
        }

        // shift the greater element to the right.
        if (b - i >= 2) {
            var j = i + 1;
            while (j < b) : (j += 1) {
                if (!context.lessThan(j, j - 1)) break;
                context.swap(j, j - 1);
            }
        }
    }

    return false;
}

fn breakPatterns(a: usize, b: usize, context: anytype) void {
    @branchHint(.cold);

    const len = b - a;
    if (len < 8) return;

    var rand = @as(u64, @intCast(len));
    const modulus = math.ceilPowerOfTwoAssert(u64, len);

    var i = a + (len / 4) * 2 - 1;
    while (i <= a + (len / 4) * 2 + 1) : (i += 1) {
        // xorshift64
        rand ^= rand << 13;
        rand ^= rand >> 7;
        rand ^= rand << 17;

        var other = @as(usize, @intCast(rand & (modulus - 1)));
        if (other >= len) other -= len;
        context.swap(i, a + other);
    }
}

/// chooses a pivot in `items[a..b]`.
/// swaps likely_sorted when `items[a..b]` seems to be already sorted.
fn chosePivot(a: usize, b: usize, pivot: *usize, context: anytype) Hint {
    // minimum length for using the Tukey's ninther method
    const shortest_ninther = 50;
    // max_swaps is the maximum number of swaps allowed in this function
    const max_swaps = 4 * 3;

    const len = b - a;
    const i = a + len / 4 * 1;
    const j = a + len / 4 * 2;
    const k = a + len / 4 * 3;
    var swaps: usize = 0;

    if (len >= 8) {
        if (len >= shortest_ninther) {
            // find medians in the neighborhoods of `i`, `j` and `k`
            sort3(i - 1, i, i + 1, &swaps, context);
            sort3(j - 1, j, j + 1, &swaps, context);
            sort3(k - 1, k, k + 1, &swaps, context);
        }

        // find the median among `i`, `j` and `k` and stores it in `j`
        sort3(i, j, k, &swaps, context);
    }

    pivot.* = j;
    return switch (swaps) {
        0 => .increasing,
        max_swaps => .decreasing,
        else => .unknown,
    };
}

fn sort3(a: usize, b: usize, c: usize, swaps: *usize, context: anytype) void {
    if (context.lessThan(b, a)) {
        swaps.* += 1;
        context.swap(b, a);
    }

    if (context.lessThan(c, b)) {
        swaps.* += 1;
        context.swap(c, b);
    }

    if (context.lessThan(b, a)) {
        swaps.* += 1;
        context.swap(b, a);
    }
}

fn reverseRange(a: usize, b: usize, context: anytype) void {
    var i = a;
    var j = b - 1;
    while (i < j) {
        context.swap(i, j);
        i += 1;
        j -= 1;
    }
}

test "pdqContext respects arbitrary range boundaries" {
    // Regression test for issue #25250
    // pdqsort should never access indices outside the specified [a, b) range
    var data: [2000]i32 = @splat(0);

    // Fill with data that triggers the partialInsertionSort path
    for (0..data.len) |i| {
        data[i] = @intCast(@mod(@as(i32, @intCast(i)) * 7, 100));
    }

    const TestContext = struct {
        items: []i32,
        range_start: usize,
        range_end: usize,

        pub fn lessThan(ctx: @This(), a: usize, b: usize) bool {
            // Assert indices are within the expected range
            testing.expect(a >= ctx.range_start and a < ctx.range_end) catch @panic("index a out of range");
            testing.expect(b >= ctx.range_start and b < ctx.range_end) catch @panic("index b out of range");
            return ctx.items[a] < ctx.items[b];
        }

        pub fn swap(ctx: @This(), a: usize, b: usize) void {
            // Assert indices are within the expected range
            testing.expect(a >= ctx.range_start and a < ctx.range_end) catch @panic("index a out of range");
            testing.expect(b >= ctx.range_start and b < ctx.range_end) catch @panic("index b out of range");
            mem.swap(i32, &ctx.items[a], &ctx.items[b]);
        }
    };

    // Test sorting a sub-range that doesn't start at 0
    const start = 1118;
    const end = 1764;
    const ctx = TestContext{
        .items = &data,
        .range_start = start,
        .range_end = end,
    };

    pdqContext(start, end, ctx);

    // Verify the range is sorted
    for ((start + 1)..end) |i| {
        try testing.expect(data[i - 1] <= data[i]);
    }
}



---
File: /std/tar/test.zig
---




---
File: /std/tar/Writer.zig
---

const Writer = @This();

const std = @import("std");
const Io = std.Io;
const assert = std.debug.assert;
const testing = std.testing;

const block_size = @sizeOf(Header);

/// Options for writing file/dir/link. If left empty 0o664 is used for
/// file mode and current time for mtime.
pub const Options = struct {
    /// File system permission mode.
    mode: u32 = 0,
    /// File system modification time.
    mtime: u64 = 0,
};

underlying_writer: *Io.Writer,
/// Assumed to use `/` for any path separators.
prefix: []const u8 = "",

const Error = error{
    WriteFailed,
    OctalOverflow,
    NameTooLong,
};

/// Sets prefix for all other write* method paths.
/// `root` is assumed to use `/` for any path separators.
pub fn setRoot(w: *Writer, root: []const u8) Error!void {
    if (root.len > 0)
        try w.writeDir(root, .{});

    w.prefix = root;
}

pub fn writeDir(w: *Writer, sub_path: []const u8, options: Options) Error!void {
    try w.writeHeader(.directory, sub_path, "", 0, options);
}

pub const WriteFileError = Io.Writer.FileError || Error || Io.File.Reader.SizeError;

pub fn writeFileTimestamp(
    w: *Writer,
    /// Assumed to use `/` for any path separators.
    sub_path: []const u8,
    file_reader: *Io.File.Reader,
    mtime: Io.Timestamp,
) WriteFileError!void {
    return writeFile(w, sub_path, file_reader, @intCast(mtime.toSeconds()));
}

pub fn writeFile(
    w: *Writer,
    /// Assumed to use `/` for any path separators.
    sub_path: []const u8,
    file_reader: *Io.File.Reader,
    /// If you want to match the file format's expectations, it wants number of
    /// seconds since POSIX epoch. Zero is also a great option here to make
    /// generated tarballs more reproducible.
    mtime: u64,
) WriteFileError!void {
    const size = try file_reader.getSize();

    var header: Header = .{};
    try w.setPath(&header, sub_path);
    try header.setSize(size);
    try header.setMtime(mtime);
    try header.updateChecksum();

    try w.underlying_writer.writeAll(@ptrCast((&header)[0..1]));
    _ = try w.underlying_writer.sendFileAll(file_reader, .unlimited);
    try w.writePadding64(size);
}

pub const WriteFileStreamError = Error || Io.Reader.StreamError;

/// Writes file reading file content from `reader`. Reads exactly `size` bytes
/// from `reader`, or returns `error.EndOfStream`.
pub fn writeFileStream(
    w: *Writer,
    /// Assumed to use `/` for any path separators.
    sub_path: []const u8,
    size: u64,
    reader: *Io.Reader,
    options: Options,
) WriteFileStreamError!void {
    try w.writeHeader(.regular, sub_path, "", size, options);
    try reader.streamExact64(w.underlying_writer, size);
    try w.writePadding64(size);
}

/// Writes file using bytes buffer `content` for size and file content.
pub fn writeFileBytes(
    w: *Writer,
    /// Assumed to use `/` for all path separators.
    sub_path: []const u8,
    content: []const u8,
    options: Options,
) Error!void {
    try w.writeHeader(.regular, sub_path, "", content.len, options);
    try w.underlying_writer.writeAll(content);
    try w.writePadding(content.len);
}

pub fn writeLink(w: *Writer, sub_path: []const u8, link_name: []const u8, options: Options) Error!void {
    try w.writeHeader(.symbolic_link, sub_path, link_name, 0, options);
}

fn writeHeader(
    w: *Writer,
    typeflag: Header.FileType,
    sub_path: []const u8,
    link_name: []const u8,
    size: u64,
    options: Options,
) Error!void {
    var header = Header.init(typeflag);
    try w.setPath(&header, sub_path);
    try header.setSize(size);
    try header.setMtime(options.mtime);
    if (options.mode != 0)
        try header.setMode(options.mode);
    if (typeflag == .symbolic_link)
        header.setLinkname(link_name) catch |err| switch (err) {
            error.NameTooLong => try w.writeExtendedHeader(.gnu_long_link, &.{link_name}),
            else => |e| return e,
        };
    try header.write(w.underlying_writer);
}

/// Writes path in posix header, if don't fit (in name+prefix; 100+155
/// bytes) writes it in gnu extended header.
fn setPath(w: *Writer, header: *Header, sub_path: []const u8) Error!void {
    header.setPath(w.prefix, sub_path) catch |err| switch (err) {
        error.NameTooLong => {
            // write extended header
            const buffers: []const []const u8 = if (w.prefix.len == 0)
                &.{sub_path}
            else
                &.{ w.prefix, "/", sub_path };
            try w.writeExtendedHeader(.gnu_long_name, buffers
```
