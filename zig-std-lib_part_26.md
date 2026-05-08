```
const plaintext = "Nonce-based encryption";
        const nonce: [12]u8 = @splat(0x01);
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        Aes128Siv.encrypt(&ciphertext, &tag, plaintext, null, &nonce, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try Aes128Siv.decrypt(&decrypted, &ciphertext, tag, null, &nonce, key);
        try testing.expectEqualSlices(u8, plaintext, &decrypted);
    }

    // Test 4: With both AD and nonce
    {
        const plaintext = "Full featured";
        const ad = "context";
        const nonce: [16]u8 = @splat(0x02);
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        Aes128Siv.encrypt(&ciphertext, &tag, plaintext, ad, &nonce, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try Aes128Siv.decrypt(&decrypted, &ciphertext, tag, ad, &nonce, key);
        try testing.expectEqualSlices(u8, plaintext, &decrypted);
    }
}

test "Aes128Siv - authentication failure" {
    const key: [32]u8 = @splat(0x13);
    const plaintext = "Secret message";
    const ad = "";

    var ciphertext: [plaintext.len]u8 = undefined;
    var tag: [16]u8 = undefined;

    Aes128Siv.encrypt(&ciphertext, &tag, plaintext, ad, null, key);

    // Corrupt the tag
    tag[0] ^= 0x01;

    var decrypted: [plaintext.len]u8 = undefined;
    try testing.expectError(error.AuthenticationFailed, Aes128Siv.decrypt(&decrypted, &ciphertext, tag, ad, null, key));
}



---
File: /std/crypto/aes.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");
const testing = std.testing;

const has_aesni = builtin.cpu.has(.x86, .aes);
const has_avx = builtin.cpu.has(.x86, .avx);
const has_armaes = builtin.cpu.has(.aarch64, .aes);
// C backend doesn't currently support passing vectors to inline asm.
const impl = if (builtin.cpu.arch == .x86_64 and builtin.zig_backend != .stage2_c and has_aesni and has_avx) impl: {
    break :impl @import("aes/aesni.zig");
} else if (builtin.cpu.arch == .aarch64 and builtin.zig_backend != .stage2_c and has_armaes) impl: {
    break :impl @import("aes/armcrypto.zig");
} else impl: {
    break :impl @import("aes/soft.zig");
};

/// `true` if AES is backed by hardware (AES-NI on x86_64, ARM Crypto Extensions on AArch64).
/// Software implementations are much slower, and should be avoided if possible.
pub const has_hardware_support =
    (builtin.cpu.arch == .x86_64 and has_aesni and has_avx) or
    (builtin.cpu.arch == .aarch64 and has_armaes);

pub const Block = impl.Block;
pub const BlockVec = impl.BlockVec;
pub const AesEncryptCtx = impl.AesEncryptCtx;
pub const AesDecryptCtx = impl.AesDecryptCtx;
pub const Aes128 = impl.Aes128;
pub const Aes256 = impl.Aes256;

test "encrypt" {
    // Appendix B
    {
        const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
        const in = [_]u8{ 0x32, 0x43, 0xf6, 0xa8, 0x88, 0x5a, 0x30, 0x8d, 0x31, 0x31, 0x98, 0xa2, 0xe0, 0x37, 0x07, 0x34 };
        const exp_out = [_]u8{ 0x39, 0x25, 0x84, 0x1d, 0x02, 0xdc, 0x09, 0xfb, 0xdc, 0x11, 0x85, 0x97, 0x19, 0x6a, 0x0b, 0x32 };

        var out: [exp_out.len]u8 = undefined;
        var ctx = Aes128.initEnc(key);
        ctx.encrypt(out[0..], in[0..]);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }

    // Appendix C.3
    {
        const key = [_]u8{
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f,
        };
        const in = [_]u8{ 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff };
        const exp_out = [_]u8{ 0x8e, 0xa2, 0xb7, 0xca, 0x51, 0x67, 0x45, 0xbf, 0xea, 0xfc, 0x49, 0x90, 0x4b, 0x49, 0x60, 0x89 };

        var out: [exp_out.len]u8 = undefined;
        var ctx = Aes256.initEnc(key);
        ctx.encrypt(out[0..], in[0..]);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }
}

test "decrypt" {
    // Appendix B
    {
        const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
        const in = [_]u8{ 0x39, 0x25, 0x84, 0x1d, 0x02, 0xdc, 0x09, 0xfb, 0xdc, 0x11, 0x85, 0x97, 0x19, 0x6a, 0x0b, 0x32 };
        const exp_out = [_]u8{ 0x32, 0x43, 0xf6, 0xa8, 0x88, 0x5a, 0x30, 0x8d, 0x31, 0x31, 0x98, 0xa2, 0xe0, 0x37, 0x07, 0x34 };

        var out: [exp_out.len]u8 = undefined;
        var ctx = Aes128.initDec(key);
        ctx.decrypt(out[0..], in[0..]);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }

    // Appendix C.3
    {
        const key = [_]u8{
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f,
        };
        const in = [_]u8{ 0x8e, 0xa2, 0xb7, 0xca, 0x51, 0x67, 0x45, 0xbf, 0xea, 0xfc, 0x49, 0x90, 0x4b, 0x49, 0x60, 0x89 };
        const exp_out = [_]u8{ 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff };

        var out: [exp_out.len]u8 = undefined;
        var ctx = Aes256.initDec(key);
        ctx.decrypt(out[0..], in[0..]);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }
}

test "expand 128-bit key" {
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const exp_enc = [_]*const [32:0]u8{
        "2b7e151628aed2a6abf7158809cf4f3c", "a0fafe1788542cb123a339392a6c7605", "f2c295f27a96b9435935807a7359f67f", "3d80477d4716fe3e1e237e446d7a883b", "ef44a541a8525b7fb671253bdb0bad00", "d4d1c6f87c839d87caf2b8bc11f915bc", "6d88a37a110b3efddbf98641ca0093fd", "4e54f70e5f5fc9f384a64fb24ea6dc4f", "ead27321b58dbad2312bf5607f8d292f", "ac7766f319fadc2128d12941575c006e", "d014f9a8c9ee2589e13f0cc8b6630ca6",
    };
    const exp_dec = [_]*const [32:0]u8{
        "d014f9a8c9ee2589e13f0cc8b6630ca6", "0c7b5a631319eafeb0398890664cfbb4", "df7d925a1f62b09da320626ed6757324", "12c07647c01f22c7bc42d2f37555114a", "6efcd876d2df54807c5df034c917c3b9", "6ea30afcbc238cf6ae82a4b4b54a338d", "90884413d280860a12a128421bc89739", "7c1f13f74208c219c021ae480969bf7b", "cc7505eb3e17d1ee82296c51c9481133", "2b3708a7f262d405bc3ebdbf4b617d62", "2b7e151628aed2a6abf7158809cf4f3c",
    };
    const enc = Aes128.initEnc(key);
    const dec = Aes128.initDec(key);
    var exp: [16]u8 = undefined;

    for (enc.key_schedule.round_keys, 0..) |round_key, i| {
        _ = try std.fmt.hexToBytes(&exp, exp_enc[i]);
        try testing.expectEqualSlices(u8, &exp, &round_key.toBytes());
    }
    for (dec.key_schedule.round_keys, 0..) |round_key, i| {
        _ = try std.fmt.hexToBytes(&exp, exp_dec[i]);
        try testing.expectEqualSlices(u8, &exp, &round_key.toBytes());
    }
}

test "invMixColumns" {
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const enc_ctx = Aes128.initEnc(key);
    const dec_ctx = Aes128.initDec(key);

    for (1..10) |i| {
        const enc_rk = enc_ctx.key_schedule.round_keys[10 - i];
        const dec_rk = dec_ctx.key_schedule.round_keys[i];
        const computed = enc_rk.invMixColumns();
        try testing.expectEqualSlices(u8, &dec_rk.toBytes(), &computed.toBytes());
    }
}

test "BlockVec invMixColumns" {
    const input = [_]u8{
        0x5f, 0x57, 0xf7, 0x1d, 0x72, 0xf5, 0xbe, 0xb9, 0x64, 0xbc, 0x3b, 0xf9, 0x15, 0x92, 0x29, 0x1a,
        0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c,
    };

    const vec2 = BlockVec(2).fromBytes(&input);
    const result_vec = vec2.invMixColumns();
    const result_bytes = result_vec.toBytes();

    for (0..2) |i| {
        const block = Block.fromBytes(input[i * 16 ..][0..16]);
        const expected = block.invMixColumns().toBytes();
        try testing.expectEqualSlices(u8, &expected, result_bytes[i * 16 ..][0..16]);
    }
}

test "expand 256-bit key" {
    const key = [_]u8{
        0x60, 0x3d, 0xeb, 0x10,
        0x15, 0xca, 0x71, 0xbe,
        0x2b, 0x73, 0xae, 0xf0,
        0x85, 0x7d, 0x77, 0x81,
        0x1f, 0x35, 0x2c, 0x07,
        0x3b, 0x61, 0x08, 0xd7,
        0x2d, 0x98, 0x10, 0xa3,
        0x09, 0x14, 0xdf, 0xf4,
    };
    const exp_enc = [_]*const [32:0]u8{
        "603deb1015ca71be2b73aef0857d7781", "1f352c073b6108d72d9810a30914dff4", "9ba354118e6925afa51a8b5f2067fcde",
        "a8b09c1a93d194cdbe49846eb75d5b9a", "d59aecb85bf3c917fee94248de8ebe96", "b5a9328a2678a647983122292f6c79b3",
        "812c81addadf48ba24360af2fab8b464", "98c5bfc9bebd198e268c3ba709e04214", "68007bacb2df331696e939e46c518d80",
        "c814e20476a9fb8a5025c02d59c58239", "de1369676ccc5a71fa2563959674ee15", "5886ca5d2e2f31d77e0af1fa27cf73c3",
        "749c47ab18501ddae2757e4f7401905a", "cafaaae3e4d59b349adf6acebd10190d", "fe4890d1e6188d0b046df344706c631e",
    };
    const exp_dec = [_]*const [32:0]u8{
        "fe4890d1e6188d0b046df344706c631e", "ada23f4963e23b2455427c8a5c709104", "57c96cf6074f07c0706abb07137f9241",
        "b668b621ce40046d36a047ae0932ed8e", "34ad1e4450866b367725bcc763152946", "32526c367828b24cf8e043c33f92aa20",
        "c440b289642b757227a3d7f114309581", "d669a7334a7ade7a80c8f18fc772e9e3", "25ba3c22a06bc7fb4388a28333934270",
        "54fb808b9c137949cab22ff547ba186c", "6c3d632985d1fbd9e3e36578701be0f3", "4a7459f9c8e8f9c256a156bc8d083799",
        "42107758e9ec98f066329ea193f8858b", "8ec6bff6829ca03b9e49af7edba96125", "603deb1015ca71be2b73aef0857d7781",
    };
    const enc = Aes256.initEnc(key);
    const dec = Aes256.initDec(key);
    var exp: [16]u8 = undefined;

    for (enc.key_schedule.round_keys, 0..) |round_key, i| {
        _ = try std.fmt.hexToBytes(&exp, exp_enc[i]);
        try testing.expectEqualSlices(u8, &exp, &round_key.toBytes());
    }
    for (dec.key_schedule.round_keys, 0..) |round_key, i| {
        _ = try std.fmt.hexToBytes(&exp, exp_dec[i]);
        try testing.expectEqualSlices(u8, &exp, &round_key.toBytes());
    }
}



---
File: /std/crypto/argon2.zig
---

// https://datatracker.ietf.org/doc/rfc9106
// https://github.com/golang/crypto/tree/master/argon2
// https://github.com/P-H-C/phc-winner-argon2

const builtin = @import("builtin");

const std = @import("std");
const blake2 = crypto.hash.blake2;
const crypto = std.crypto;
const Io = std.Io;
const math = std.math;
const mem = std.mem;
const phc_format = pwhash.phc_format;
const pwhash = crypto.pwhash;
const Blake2b512 = blake2.Blake2b512;
const Blocks = std.array_list.AlignedManaged([block_length]u64, .@"16");
const H0 = [Blake2b512.digest_length + 8]u8;

const EncodingError = crypto.errors.EncodingError;
const KdfError = pwhash.KdfError;
const HasherError = pwhash.HasherError;
const Error = pwhash.Error;

const version = 0x13;
const block_length = 128;
const sync_points = 4;
const max_int = 0xffff_ffff;

const default_salt_len = 32;
const default_hash_len = 32;
const max_salt_len = 64;
const max_hash_len = 64;

/// Argon2 type
pub const Mode = enum {
    /// Argon2d is faster and uses data-depending memory access, which makes it highly resistant
    /// against GPU cracking attacks and suitable for applications with no threats from side-channel
    /// timing attacks (eg. cryptocurrencies).
    argon2d,

    /// Argon2i instead uses data-independent memory access, which is preferred for password
    /// hashing and password-based key derivation, but it is slower as it makes more passes over
    /// the memory to protect from tradeoff attacks.
    argon2i,

    /// Argon2id is a hybrid of Argon2i and Argon2d, using a combination of data-depending and
    /// data-independent memory accesses, which gives some of Argon2i's resistance to side-channel
    /// cache timing attacks and much of Argon2d's resistance to GPU cracking attacks.
    argon2id,
};

/// Argon2 parameters
pub const Params = struct {
    const Self = @This();

    /// Time cost, which defines the amount of computation realized and therefore the execution
    /// time, given in number of iterations.
    t: u32,

    /// Memory cost, which defines the memory usage, given in kibibytes.
    m: u32,

    /// Parallelism degree, which defines the number of independent tasks,
    /// to be multiplexed onto threads when possible.
    p: u24,

    /// The secret parameter, which is used for keyed hashing. This allows a secret key to be input
    /// at hashing time (from some external location) and be folded into the value of the hash. This
    /// means that even if your salts and hashes are compromised, an attacker cannot brute-force to
    /// find the password without the key.
    secret: ?[]const u8 = null,

    /// The ad parameter, which is used to fold any additional data into the hash value. Functionally,
    /// this behaves almost exactly like the secret or salt parameters; the ad parameter is folding
    /// into the value of the hash. However, this parameter is used for different data. The salt
    /// should be a random string stored alongside your password. The secret should be a random key
    /// only usable at hashing time. The ad is for any other data.
    ad: ?[]const u8 = null,

    /// Baseline parameters for interactive logins using argon2i type
    pub const interactive_2i = Self.fromLimits(4, 33554432);
    /// Baseline parameters for normal usage using argon2i type
    pub const moderate_2i = Self.fromLimits(6, 134217728);
    /// Baseline parameters for offline usage using argon2i type
    pub const sensitive_2i = Self.fromLimits(8, 536870912);

    /// Baseline parameters for interactive logins using argon2id type
    pub const interactive_2id = Self.fromLimits(2, 67108864);
    /// Baseline parameters for normal usage using argon2id type
    pub const moderate_2id = Self.fromLimits(3, 268435456);
    /// Baseline parameters for offline usage using argon2id type
    pub const sensitive_2id = Self.fromLimits(4, 1073741824);

    /// Recommended parameters for argon2id type according to the
    /// [OWASP cheat sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
    pub const owasp_2id = Self{ .t = 2, .m = 19 * 1024, .p = 1 };

    /// Create parameters from ops and mem limits, where mem_limit given in bytes
    pub fn fromLimits(ops_limit: u32, mem_limit: usize) Self {
        const m = mem_limit / 1024;
        std.debug.assert(m <= max_int);
        return .{ .t = ops_limit, .m = @as(u32, @intCast(m)), .p = 1 };
    }
};

fn initHash(
    password: []const u8,
    salt: []const u8,
    params: Params,
    dk_len: usize,
    mode: Mode,
) H0 {
    var h0: H0 = undefined;
    var parameters: [24]u8 = undefined;
    var tmp: [4]u8 = undefined;
    var b2 = Blake2b512.init(.{});
    mem.writeInt(u32, parameters[0..4], params.p, .little);
    mem.writeInt(u32, parameters[4..8], @as(u32, @intCast(dk_len)), .little);
    mem.writeInt(u32, parameters[8..12], params.m, .little);
    mem.writeInt(u32, parameters[12..16], params.t, .little);
    mem.writeInt(u32, parameters[16..20], version, .little);
    mem.writeInt(u32, parameters[20..24], @intFromEnum(mode), .little);
    b2.update(&parameters);
    mem.writeInt(u32, &tmp, @as(u32, @intCast(password.len)), .little);
    b2.update(&tmp);
    b2.update(password);
    mem.writeInt(u32, &tmp, @as(u32, @intCast(salt.len)), .little);
    b2.update(&tmp);
    b2.update(salt);
    const secret = params.secret orelse "";
    std.debug.assert(secret.len <= max_int);
    mem.writeInt(u32, &tmp, @as(u32, @intCast(secret.len)), .little);
    b2.update(&tmp);
    b2.update(secret);
    const ad = params.ad orelse "";
    std.debug.assert(ad.len <= max_int);
    mem.writeInt(u32, &tmp, @as(u32, @intCast(ad.len)), .little);
    b2.update(&tmp);
    b2.update(ad);
    b2.final(h0[0..Blake2b512.digest_length]);
    return h0;
}

fn blake2bLong(out: []u8, in: []const u8) void {
    const H = Blake2b512;
    var outlen_bytes: [4]u8 = undefined;
    mem.writeInt(u32, &outlen_bytes, @as(u32, @intCast(out.len)), .little);

    var out_buf: [H.digest_length]u8 = undefined;

    if (out.len <= H.digest_length) {
        var h = H.init(.{ .expected_out_bits = out.len * 8 });
        h.update(&outlen_bytes);
        h.update(in);
        h.final(&out_buf);
        @memcpy(out, out_buf[0..out.len]);
        return;
    }

    var h = H.init(.{});
    h.update(&outlen_bytes);
    h.update(in);
    h.final(&out_buf);
    var out_slice = out;
    out_slice[0 .. H.digest_length / 2].* = out_buf[0 .. H.digest_length / 2].*;
    out_slice = out_slice[H.digest_length / 2 ..];

    var in_buf: [H.digest_length]u8 = undefined;
    while (out_slice.len > H.digest_length) {
        in_buf = out_buf;
        H.hash(&in_buf, &out_buf, .{});
        out_slice[0 .. H.digest_length / 2].* = out_buf[0 .. H.digest_length / 2].*;
        out_slice = out_slice[H.digest_length / 2 ..];
    }
    in_buf = out_buf;
    H.hash(&in_buf, &out_buf, .{ .expected_out_bits = out_slice.len * 8 });
    @memcpy(out_slice, out_buf[0..out_slice.len]);
}

fn initBlocks(
    blocks: *Blocks,
    h0: *H0,
    memory: u32,
    threads: u24,
) void {
    var block0: [1024]u8 = undefined;
    var lane: u24 = 0;
    while (lane < threads) : (lane += 1) {
        const j = lane * (memory / threads);
        mem.writeInt(u32, h0[Blake2b512.digest_length + 4 ..][0..4], lane, .little);

        mem.writeInt(u32, h0[Blake2b512.digest_length..][0..4], 0, .little);
        blake2bLong(&block0, h0);
        for (&blocks.items[j + 0], 0..) |*v, i| {
            v.* = mem.readInt(u64, block0[i * 8 ..][0..8], .little);
        }

        mem.writeInt(u32, h0[Blake2b512.digest_length..][0..4], 1, .little);
        blake2bLong(&block0, h0);
        for (&blocks.items[j + 1], 0..) |*v, i| {
            v.* = mem.readInt(u64, block0[i * 8 ..][0..8], .little);
        }
    }
}

fn processBlocks(
    blocks: *Blocks,
    time: u32,
    memory: u32,
    threads: u24,
    mode: Mode,
    io: Io,
) Io.Cancelable!void {
    const lanes = memory / threads;
    const segments = lanes / sync_points;

    if (builtin.single_threaded or threads == 1) {
        processBlocksSync(blocks, time, memory, threads, mode, lanes, segments);
    } else {
        try processBlocksAsync(blocks, time, memory, threads, mode, lanes, segments, io);
    }
}

fn processBlocksSync(
    blocks: *Blocks,
    time: u32,
    memory: u32,
    threads: u24,
    mode: Mode,
    lanes: u32,
    segments: u32,
) void {
    var n: u32 = 0;
    while (n < time) : (n += 1) {
        var slice: u32 = 0;
        while (slice < sync_points) : (slice += 1) {
            var lane: u24 = 0;
            while (lane < threads) : (lane += 1) {
                processSegment(blocks, time, memory, threads, mode, lanes, segments, n, slice, lane);
            }
        }
    }
}

fn processBlocksAsync(
    blocks: *Blocks,
    time: u32,
    memory: u32,
    threads: u24,
    mode: Mode,
    lanes: u32,
    segments: u32,
    io: Io,
) Io.Cancelable!void {
    var n: u32 = 0;
    while (n < time) : (n += 1) {
        var slice: u32 = 0;
        while (slice < sync_points) : (slice += 1) {
            var group: Io.Group = .init;
            defer group.cancel(io);
            var lane: u24 = 0;
            while (lane < threads) : (lane += 1) {
                group.async(io, processSegment, .{
                    blocks, time, memory, threads, mode, lanes, segments, n, slice, lane,
                });
            }
            try group.await(io);
        }
    }
}

fn processSegment(
    blocks: *Blocks,
    passes: u32,
    memory: u32,
    threads: u24,
    mode: Mode,
    lanes: u32,
    segments: u32,
    n: u32,
    slice: u32,
    lane: u24,
) void {
    var addresses align(16) = [_]u64{0} ** block_length;
    var in align(16) = [_]u64{0} ** block_length;
    const zero align(16) = [_]u64{0} ** block_length;
    if (mode == .argon2i or (mode == .argon2id and n == 0 and slice < sync_points / 2)) {
        in[0] = n;
        in[1] = lane;
        in[2] = slice;
        in[3] = memory;
        in[4] = passes;
        in[5] = @intFromEnum(mode);
    }
    var index: u32 = 0;
    if (n == 0 and slice == 0) {
        index = 2;
        if (mode == .argon2i or mode == .argon2id) {
            in[6] += 1;
            processBlock(&addresses, &in, &zero);
            processBlock(&addresses, &addresses, &zero);
        }
    }
    var offset = lane * lanes + slice * segments + index;
    var random: u64 = 0;
    while (index < segments) : ({
        index += 1;
        offset += 1;
    }) {
        var prev = offset -% 1;
        if (index == 0 and slice == 0) {
            prev +%= lanes;
        }
        if (mode == .argon2i or (mode == .argon2id and n == 0 and slice < sync_points / 2)) {
            if (index % block_length == 0) {
                in[6] += 1;
                processBlock(&addresses, &in, &zero);
                processBlock(&addresses, &addresses, &zero);
            }
            random = addresses[index % block_length];
        } else {
            random = blocks.items[prev][0];
        }
        const new_offset = indexAlpha(random, lanes, segments, threads, n, slice, lane, index);
        processBlockXor(&blocks.items[offset], &blocks.items[prev], &blocks.items[new_offset]);
    }
}

fn processBlock(
    out: *align(16) [block_length]u64,
    in1: *align(16) const [block_length]u64,
    in2: *align(16) const [block_length]u64,
) void {
    processBlockGeneric(out, in1, in2, false);
}

fn processBlockXor(
    out: *[block_length]u64,
    in1: *const [block_length]u64,
    in2: *const [block_length]u64,
) void {
    processBlockGeneric(out, in1, in2, true);
}

fn processBlockGeneric(
    out: *[block_length]u64,
    in1: *const [block_length]u64,
    in2: *const [block_length]u64,
    comptime xor: bool,
) void {
    var t: [block_length]u64 = undefined;
    for (&t, 0..) |*v, i| {
        v.* = in1[i] ^ in2[i];
    }
    var i: usize = 0;
    while (i < block_length) : (i += 16) {
        blamkaGeneric(t[i..][0..16]);
    }
    i = 0;
    var buffer: [16]u64 = undefined;
    while (i < block_length / 8) : (i += 2) {
        var j: usize = 0;
        while (j < block_length / 8) : (j += 2) {
            buffer[j] = t[j * 8 + i];
            buffer[j + 1] = t[j * 8 + i + 1];
        }
        blamkaGeneric(&buffer);
        j = 0;
        while (j < block_length / 8) : (j += 2) {
            t[j * 8 + i] = buffer[j];
            t[j * 8 + i + 1] = buffer[j + 1];
        }
    }
    if (xor) {
        for (t, 0..) |v, j| {
            out[j] ^= in1[j] ^ in2[j] ^ v;
        }
    } else {
        for (t, 0..) |v, j| {
            out[j] = in1[j] ^ in2[j] ^ v;
        }
    }
}

const QuarterRound = struct { a: usize, b: usize, c: usize, d: usize };

fn Rp(a: usize, b: usize, c: usize, d: usize) QuarterRound {
    return .{ .a = a, .b = b, .c = c, .d = d };
}

fn fBlaMka(x: u64, y: u64) u64 {
    const xy = @as(u64, @as(u32, @truncate(x))) * @as(u64, @as(u32, @truncate(y)));
    return x +% y +% 2 *% xy;
}

fn blamkaGeneric(x: *[16]u64) void {
    const rounds = comptime [_]QuarterRound{
        Rp(0, 4, 8, 12),
        Rp(1, 5, 9, 13),
        Rp(2, 6, 10, 14),
        Rp(3, 7, 11, 15),
        Rp(0, 5, 10, 15),
        Rp(1, 6, 11, 12),
        Rp(2, 7, 8, 13),
        Rp(3, 4, 9, 14),
    };
    inline for (rounds) |r| {
        x[r.a] = fBlaMka(x[r.a], x[r.b]);
        x[r.d] = math.rotr(u64, x[r.d] ^ x[r.a], 32);
        x[r.c] = fBlaMka(x[r.c], x[r.d]);
        x[r.b] = math.rotr(u64, x[r.b] ^ x[r.c], 24);
        x[r.a] = fBlaMka(x[r.a], x[r.b]);
        x[r.d] = math.rotr(u64, x[r.d] ^ x[r.a], 16);
        x[r.c] = fBlaMka(x[r.c], x[r.d]);
        x[r.b] = math.rotr(u64, x[r.b] ^ x[r.c], 63);
    }
}

fn finalize(
    blocks: *Blocks,
    memory: u32,
    threads: u24,
    out: []u8,
) void {
    const lanes = memory / threads;
    var lane: u24 = 0;
    while (lane < threads - 1) : (lane += 1) {
        for (blocks.items[(lane * lanes) + lanes - 1], 0..) |v, i| {
            blocks.items[memory - 1][i] ^= v;
        }
    }
    var block: [1024]u8 = undefined;
    for (blocks.items[memory - 1], 0..) |v, i| {
        mem.writeInt(u64, block[i * 8 ..][0..8], v, .little);
    }
    blake2bLong(out, &block);
}

fn indexAlpha(
    rand: u64,
    lanes: u32,
    segments: u32,
    threads: u24,
    n: u32,
    slice: u32,
    lane: u24,
    index: u32,
) u32 {
    var ref_lane = @as(u32, @intCast(rand >> 32)) % threads;
    if (n == 0 and slice == 0) {
        ref_lane = lane;
    }
    var m = 3 * segments;
    var s = ((slice + 1) % sync_points) * segments;
    if (lane == ref_lane) {
        m += index;
    }
    if (n == 0) {
        m = slice * segments;
        s = 0;
        if (slice == 0 or lane == ref_lane) {
            m += index;
        }
    }
    if (index == 0 or lane == ref_lane) {
        m -= 1;
    }
    var p = @as(u64, @as(u32, @truncate(rand)));
    p = (p * p) >> 32;
    p = (p * m) >> 32;
    return ref_lane * lanes + @as(u32, @intCast(((s + m - (p + 1)) % lanes)));
}

/// Derives a key from the password, salt, and argon2 parameters.
///
/// Derived key has to be at least 4 bytes length.
///
/// Salt has to be at least 8 bytes length.
pub fn kdf(
    allocator: mem.Allocator,
    derived_key: []u8,
    password: []const u8,
    salt: []const u8,
    params: Params,
    mode: Mode,
    io: Io,
) KdfError!void {
    if (derived_key.len < 4) return KdfError.WeakParameters;
    if (derived_key.len > max_int) return KdfError.OutputTooLong;

    if (password.len > max_int) return KdfError.WeakParameters;
    if (salt.len < 8 or salt.len > max_int) return KdfError.WeakParameters;
    if (params.t < 1 or params.p < 1) return KdfError.WeakParameters;
    if (params.m / 8 < params.p) return KdfError.WeakParameters;

    var h0 = initHash(password, salt, params, derived_key.len, mode);
    const memory = @max(
        params.m / (sync_points * params.p) * (sync_points * params.p),
        2 * sync_points * params.p,
    );

    var blocks = try Blocks.initCapacity(allocator, memory);
    defer blocks.deinit();

    blocks.appendNTimesAssumeCapacity(@splat(0), memory);

    initBlocks(&blocks, &h0, memory, params.p);
    try processBlocks(&blocks, params.t, memory, params.p, mode, io);
    finalize(&blocks, memory, params.p, derived_key);
}

const PhcFormatHasher = struct {
    const BinValue = phc_format.BinValue;

    const HashResult = struct {
        alg_id: []const u8,
        alg_version: ?u32,
        m: u32,
        t: u32,
        p: u24,
        salt: BinValue(max_salt_len),
        hash: BinValue(max_hash_len),
    };

    pub fn create(
        allocator: mem.Allocator,
        password: []const u8,
        params: Params,
        mode: Mode,
        buf: []u8,
        io: Io,
    ) HasherError![]const u8 {
        if (params.secret != null or params.ad != null) return HasherError.InvalidEncoding;

        var salt: [default_salt_len]u8 = undefined;
        io.random(&salt);

        var hash: [default_hash_len]u8 = undefined;
        try kdf(allocator, &hash, password, &salt, params, mode, io);

        return phc_format.serialize(HashResult{
            .alg_id = @tagName(mode),
            .alg_version = version,
            .m = params.m,
            .t = params.t,
            .p = params.p,
            .salt = try BinValue(max_salt_len).fromSlice(&salt),
            .hash = try BinValue(max_hash_len).fromSlice(&hash),
        }, buf);
    }

    pub fn verify(
        allocator: mem.Allocator,
        str: []const u8,
        password: []const u8,
        io: Io,
    ) HasherError!void {
        const hash_result = try phc_format.deserialize(HashResult, str);

        const mode = std.meta.stringToEnum(Mode, hash_result.alg_id) orelse
            return HasherError.PasswordVerificationFailed;
        if (hash_result.alg_version) |v| {
            if (v != version) return HasherError.InvalidEncoding;
        }
        const params = Params{ .t = hash_result.t, .m = hash_result.m, .p = hash_result.p };

        const expected_hash = hash_result.hash.constSlice();
        var hash_buf: [max_hash_len]u8 = undefined;
        if (expected_hash.len > hash_buf.len) return HasherError.InvalidEncoding;
        const hash = hash_buf[0..expected_hash.len];

        try kdf(allocator, hash, password, hash_result.salt.constSlice(), params, mode, io);
        if (!mem.eql(u8, hash, expected_hash)) return HasherError.PasswordVerificationFailed;
    }
};

/// Options for hashing a password.
///
/// Allocator is required for argon2.
///
/// Only phc encoding is supported.
pub const HashOptions = struct {
    allocator: ?mem.Allocator,
    params: Params,
    mode: Mode = .argon2id,
    encoding: pwhash.Encoding = .phc,
};

/// Compute a hash of a password using the argon2 key derivation function.
/// The function returns a string that includes all the parameters required for verification.
pub fn strHash(
    password: []const u8,
    options: HashOptions,
    out: []u8,
    io: Io,
) Error![]const u8 {
    const allocator = options.allocator orelse return Error.AllocatorRequired;
    switch (options.encoding) {
        .phc => return PhcFormatHasher.create(
            allocator,
            password,
            options.params,
            options.mode,
            out,
            io,
        ),
        .crypt => return Error.InvalidEncoding,
    }
}

/// Options for hash verification.
///
/// Allocator is required for argon2.
pub const VerifyOptions = struct {
    allocator: ?mem.Allocator,
};

/// Verify that a previously computed hash is valid for a given password.
pub fn strVerify(
    str: []const u8,
    password: []const u8,
    options: VerifyOptions,
    io: Io,
) Error!void {
    const allocator = options.allocator orelse return Error.AllocatorRequired;
    return PhcFormatHasher.verify(allocator, str, password, io);
}

test "argon2d" {
    if (true) return error.SkipZigTest; // https://codeberg.org/ziglang/zig/issues/30074

    const password = [_]u8{0x01} ** 32;
    const salt = [_]u8{0x02} ** 16;
    const secret = [_]u8{0x03} ** 8;
    const ad = [_]u8{0x04} ** 12;

    var dk: [32]u8 = undefined;
    try kdf(
        std.testing.allocator,
        &dk,
        &password,
        &salt,
        .{ .t = 3, .m = 32, .p = 4, .secret = &secret, .ad = &ad },
        .argon2d,
        std.testing.io,
    );

    const want = [_]u8{
        0x51, 0x2b, 0x39, 0x1b, 0x6f, 0x11, 0x62, 0x97,
        0x53, 0x71, 0xd3, 0x09, 0x19, 0x73, 0x42, 0x94,
        0xf8, 0x68, 0xe3, 0xbe, 0x39, 0x84, 0xf3, 0xc1,
        0xa1, 0x3a, 0x4d, 0xb9, 0xfa, 0xbe, 0x4a, 0xcb,
    };
    try std.testing.expectEqualSlices(u8, &dk, &want);
}

test "argon2i" {
    const password = [_]u8{0x01} ** 32;
    const salt = [_]u8{0x02} ** 16;
    const secret = [_]u8{0x03} ** 8;
    const ad = [_]u8{0x04} ** 12;

    var dk: [32]u8 = undefined;
    try kdf(
        std.testing.allocator,
        &dk,
        &password,
        &salt,
        .{ .t = 3, .m = 32, .p = 4, .secret = &secret, .ad = &ad },
        .argon2i,
        std.testing.io,
    );

    const want = [_]u8{
        0xc8, 0x14, 0xd9, 0xd1, 0xdc, 0x7f, 0x37, 0xaa,
        0x13, 0xf0, 0xd7, 0x7f, 0x24, 0x94, 0xbd, 0xa1,
        0xc8, 0xde, 0x6b, 0x01, 0x6d, 0xd3, 0x88, 0xd2,
        0x99, 0x52, 0xa4, 0xc4, 0x67, 0x2b, 0x6c, 0xe8,
    };
    try std.testing.expectEqualSlices(u8, &dk, &want);
}

test "argon2id" {
    const password = [_]u8{0x01} ** 32;
    const salt = [_]u8{0x02} ** 16;
    const secret = [_]u8{0x03} ** 8;
    const ad = [_]u8{0x04} ** 12;

    var dk: [32]u8 = undefined;
    try kdf(
        std.testing.allocator,
        &dk,
        &password,
        &salt,
        .{ .t = 3, .m = 32, .p = 4, .secret = &secret, .ad = &ad },
        .argon2id,
        std.testing.io,
    );

    const want = [_]u8{
        0x0d, 0x64, 0x0d, 0xf5, 0x8d, 0x78, 0x76, 0x6c,
        0x08, 0xc0, 0x37, 0xa3, 0x4a, 0x8b, 0x53, 0xc9,
        0xd0, 0x1e, 0xf0, 0x45, 0x2d, 0x75, 0xb6, 0x5e,
        0xb5, 0x25, 0x20, 0xe9, 0x6b, 0x01, 0xe6, 0x59,
    };
    try std.testing.expectEqualSlices(u8, &dk, &want);
}

test "kdf" {
    if (true) return error.SkipZigTest; // https://codeberg.org/ziglang/zig/issues/31402

    const password = "password";
    const salt = "somesalt";

    const TestVector = struct {
        mode: Mode,
        time: u32,
        memory: u32,
        threads: u8,
        hash: []const u8,
    };
    const test_vectors = [_]TestVector{
        .{
            .mode = .argon2i,
            .time = 1,
            .memory = 64,
            .threads = 1,
            .hash = "b9c401d1844a67d50eae3967dc28870b22e508092e861a37",
        },
        .{
            .mode = .argon2d,
            .time = 1,
            .memory = 64,
            .threads = 1,
            .hash = "8727405fd07c32c78d64f547f24150d3f2e703a89f981a19",
        },
        .{
            .mode = .argon2id,
            .time = 1,
            .memory = 64,
            .threads = 1,
            .hash = "655ad15eac652dc59f7170a7332bf49b8469be1fdb9c28bb",
        },
        .{
            .mode = .argon2i,
            .time = 2,
            .memory = 64,
            .threads = 1,
            .hash = "8cf3d8f76a6617afe35fac48eb0b7433a9a670ca4a07ed64",
        },
        .{
            .mode = .argon2d,
            .time = 2,
            .memory = 64,
            .threads = 1,
            .hash = "3be9ec79a69b75d3752acb59a1fbb8b295a46529c48fbb75",
        },
        .{
            .mode = .argon2id,
            .time = 2,
            .memory = 64,
            .threads = 1,
            .hash = "068d62b26455936aa6ebe60060b0a65870dbfa3ddf8d41f7",
        },
        .{
            .mode = .argon2i,
            .time = 2,
            .memory = 64,
            .threads = 2,
            .hash = "2089f3e78a799720f80af806553128f29b132cafe40d059f",
        },
        .{
            .mode = .argon2d,
            .time = 2,
            .memory = 64,
            .threads = 2,
            .hash = "68e2462c98b8bc6bb60ec68db418ae2c9ed24fc6748a40e9",
        },
        .{
            .mode = .argon2id,
            .time = 2,
            .memory = 64,
            .threads = 2,
            .hash = "350ac37222f436ccb5c0972f1ebd3bf6b958bf2071841362",
        },
        .{
            .mode = .argon2i,
            .time = 3,
            .memory = 256,
            .threads = 2,
            .hash = "f5bbf5d4c3836af13193053155b73ec7476a6a2eb93fd5e6",
        },
        .{
            .mode = .argon2d,
            .time = 3,
            .memory = 256,
            .threads = 2,
            .hash = "f4f0669218eaf3641f39cc97efb915721102f4b128211ef2",
        },
        .{
            .mode = .argon2id,
            .time = 3,
            .memory = 256,
            .threads = 2,
            .hash = "4668d30ac4187e6878eedeacf0fd83c5a0a30db2cc16ef0b",
        },
        .{
            .mode = .argon2i,
            .time = 4,
            .memory = 256,
            .threads = 4,
            .hash = "f7dbbacbf16999e3700817a7e06f65a8db2e9fa9504ede4c",
        },
        .{
            .mode = .argon2d,
            .time = 4,
            .memory = 256,
            .threads = 4,
            .hash = "ea2970501cf49faa5ba1d2e6370204e9b57ca90a8fea937b",
        },
        .{
            .mode = .argon2id,
            .time = 4,
            .memory = 256,
            .threads = 4,
            .hash = "fbd40d5a8cb92f88c20bda4b3cdb1f9d5af1efa937032410",
        },
        .{
            .mode = .argon2i,
            .time = 4,
            .memory = 256,
            .threads = 8,
            .hash = "15d3c398364e53f68fd12d19baf3f21432d964254fe27467",
        },
        .{
            .mode = .argon2d,
            .time = 4,
            .memory = 256,
            .threads = 8,
            .hash = "23c9adc06f06e21e4612c1466a1be02627690932b02c0df0",
        },
        .{
            .mode = .argon2id,
            .time = 4,
            .memory = 256,
            .threads = 8,
            .hash = "f22802f8ca47be93f9954e4ce20c1e944e938fbd4a125d9d",
        },
        .{
            .mode = .argon2i,
            .time = 2,
            .memory = 64,
            .threads = 3,
            .hash = "5cab452fe6b8479c8661def8cd703b611a3905a6d5477fe6",
        },
        .{
            .mode = .argon2d,
            .time = 2,
            .memory = 64,
            .threads = 3,
            .hash = "22474a423bda2ccd36ec9afd5119e5c8949798cadf659f51",
        },
        .{
            .mode = .argon2id,
            .time = 2,
            .memory = 64,
            .threads = 3,
            .hash = "4a15b31aec7c2590b87d1f520be7d96f56658172deaa3079",
        },
        .{
            .mode = .argon2i,
            .time = 3,
            .memory = 256,
            .threads = 6,
            .hash = "ebc8f91964abd8ceab49a12963b0a9e57d635bfa2aad2884",
        },
        .{
            .mode = .argon2d,
            .time = 3,
            .memory = 256,
            .threads = 6,
            .hash = "1dd7202fd68da6675f769f4034b7a1db30d8785331954117",
        },
        .{
            .mode = .argon2id,
            .time = 3,
            .memory = 256,
            .threads = 6,
            .hash = "424436b6ee22a66b04b9d0cf78f190305c5c166bae8baa09",
        },
    };
    for (test_vectors) |v| {
        var want: [24]u8 = undefined;
        _ = try std.fmt.hexToBytes(&want, v.hash);

        var dk: [24]u8 = undefined;
        try kdf(
            std.testing.allocator,
            &dk,
            password,
            salt,
            .{ .t = v.time, .m = v.memory, .p = v.threads },
            v.mode,
            std.testing.io,
        );

        try std.testing.expectEqualSlices(u8, &dk, &want);
    }
}

test "phc format hasher" {
    const allocator = std.testing.allocator;
    const password = "testpass";
    const io = std.testing.io;

    var buf: [128]u8 = undefined;
    const hash = try PhcFormatHasher.create(
        allocator,
        password,
        .{ .t = 3, .m = 32, .p = 4 },
        .argon2id,
        &buf,
        io,
    );
    try PhcFormatHasher.verify(allocator, hash, password, io);
}

test "password hash and password verify" {
    const allocator = std.testing.allocator;
    const password = "testpass";
    const io = std.testing.io;

    var buf: [128]u8 = undefined;
    const hash = try strHash(
        password,
        .{ .allocator = allocator, .params = .{ .t = 3, .m = 32, .p = 4 } },
        &buf,
        io,
    );
    try strVerify(hash, password, .{ .allocator = allocator }, io);
}

test "kdf derived key length" {
    if (true) return error.SkipZigTest; // https://codeberg.org/ziglang/zig/issues/31504

    const allocator = std.testing.allocator;
    const io = std.testing.io;

    const password = "testpass";
    const salt = "saltsalt";
    const params = Params{ .t = 3, .m = 32, .p = 4 };
    const mode = Mode.argon2id;

    var dk1: [11]u8 = undefined;
    try kdf(allocator, &dk1, password, salt, params, mode, io);

    var dk2: [77]u8 = undefined;
    try kdf(allocator, &dk2, password, salt, params, mode, io);

    var dk3: [111]u8 = undefined;
    try kdf(allocator, &dk3, password, salt, params, mode, io);
}



---
File: /std/crypto/ascon.zig
---

//! Ascon is a 320-bit permutation, selected as new standard for lightweight cryptography
//! in the NIST Lightweight Cryptography competition (2019–2023).
//! https://csrc.nist.gov/pubs/sp/800/232/ipd
//!
//! The permutation is compact, and optimized for timing and side channel resistance,
//! making it a good choice for embedded applications.
//!
//! It is not meant to be used directly, but as a building block for symmetric cryptography.

const std = @import("std");
const builtin = @import("builtin");
const crypto = std.crypto;
const debug = std.debug;
const mem = std.mem;
const testing = std.testing;
const rotr = std.math.rotr;
const native_endian = builtin.cpu.arch.endian();

/// An Ascon state.
///
/// The state is represented as 5 64-bit words.
///
/// The original NIST submission (v1.2) serializes these words as big-endian,
/// but NIST SP 800-232 switched to a little-endian representation.
/// Software implementations are free to use native endianness with no security degradation.
pub fn State(comptime endian: std.builtin.Endian) type {
    return struct {
        const Self = @This();

        /// Number of bytes in the state.
        pub const block_bytes = 40;

        const Block = [5]u64;

        st: Block,

        /// Initialize the state from a slice of bytes.
        ///
        /// Parameters:
        ///   - initial_state: A 40-byte array to initialize the state
        ///
        /// Returns: A new State initialized with the provided bytes
        pub fn init(initial_state: [block_bytes]u8) Self {
            var state = Self{ .st = undefined };
            @memcpy(state.asBytes(), &initial_state);
            state.endianSwap();
            return state;
        }

        /// Initialize the state from u64 words in native endianness.
        ///
        /// Parameters:
        ///   - initial_state: An array of 5 u64 words in native endianness
        ///
        /// Returns: A new State with the provided words
        pub fn initFromWords(initial_state: [5]u64) Self {
            return .{ .st = initial_state };
        }

        /// Initialize the state for Ascon XOF.
        ///
        /// Returns: A new State initialized with the Ascon XOF initialization vector
        pub fn initXof() Self {
            return Self{ .st = Block{
                0xb57e273b814cd416,
                0x2b51042562ae2420,
                0x66a3a7768ddf2218,
                0x5aad0a7a8153650c,
                0x4f3e0e32539493b6,
            } };
        }

        /// Initialize the state for Ascon XOFa.
        ///
        /// Returns: A new State initialized with the Ascon XOFa initialization vector
        pub fn initXofA() Self {
            return Self{ .st = Block{
                0x44906568b77b9832,
                0xcd8d6cae53455532,
                0xf7b5212756422129,
                0x246885e1de0d225b,
                0xa8cb5ce33449973f,
            } };
        }

        /// A representation of the state as bytes. The byte order is architecture-dependent.
        ///
        /// Returns: A pointer to the state's internal byte representation
        pub fn asBytes(self: *Self) *[block_bytes]u8 {
            return mem.asBytes(&self.st);
        }

        /// Byte-swap the entire state if the architecture doesn't match the required endianness.
        ///
        /// This ensures the state is in the correct endianness for the current platform.
        pub fn endianSwap(self: *Self) void {
            for (&self.st) |*w| {
                w.* = mem.toNative(u64, w.*, endian);
            }
        }

        /// Set bytes starting at the beginning of the state.
        ///
        /// Parameters:
        ///   - bytes: Slice of bytes to write into the state (up to 40 bytes)
        ///
        /// Note: If bytes.len < 40, remaining state words are zero-padded
        pub fn setBytes(self: *Self, bytes: []const u8) void {
            var i: usize = 0;
            while (i + 8 <= bytes.len) : (i += 8) {
                self.st[i / 8] = mem.readInt(u64, bytes[i..][0..8], endian);
            }
            if (i < bytes.len) {
                var padded: [8]u8 = @splat(0);
                @memcpy(padded[0 .. bytes.len - i], bytes[i..]);
                self.st[i / 8] = mem.readInt(u64, padded[0..], endian);
            }
        }

        /// XOR a byte into the state at a given offset.
        ///
        /// Parameters:
        ///   - byte: The byte to XOR into the state
        ///   - offset: The byte offset in the state (0-39)
        pub fn addByte(self: *Self, byte: u8, offset: usize) void {
            const z = switch (endian) {
                .big => 64 - 8 - 8 * @as(u6, @truncate(offset % 8)),
                .little => 8 * @as(u6, @truncate(offset % 8)),
            };
            self.st[offset / 8] ^= @as(u64, byte) << z;
        }

        /// XOR bytes into the beginning of the state.
        ///
        /// Parameters:
        ///   - bytes: Slice of bytes to XOR into the state (up to 40 bytes)
        ///
        /// Note: Handles partial blocks with zero-padding
        pub fn addBytes(self: *Self, bytes: []const u8) void {
            var i: usize = 0;
            while (i + 8 <= bytes.len) : (i += 8) {
                self.st[i / 8] ^= mem.readInt(u64, bytes[i..][0..8], endian);
            }
            if (i < bytes.len) {
                var padded: [8]u8 = @splat(0);
                @memcpy(padded[0 .. bytes.len - i], bytes[i..]);
                self.st[i / 8] ^= mem.readInt(u64, padded[0..], endian);
            }
        }

        /// Extract the first bytes of the state.
        ///
        /// Parameters:
        ///   - out: Output buffer to receive the extracted bytes
        ///
        /// Note: Extracts up to out.len bytes from the beginning of the state
        pub fn extractBytes(self: *Self, out: []u8) void {
            var i: usize = 0;
            while (i + 8 <= out.len) : (i += 8) {
                mem.writeInt(u64, out[i..][0..8], self.st[i / 8], endian);
            }
            if (i < out.len) {
                var padded: [8]u8 = @splat(0);
                mem.writeInt(u64, padded[0..], self.st[i / 8], endian);
                @memcpy(out[i..], padded[0 .. out.len - i]);
            }
        }

        /// XOR the first bytes of the state into a slice of bytes.
        ///
        /// Parameters:
        ///   - out: Output buffer for the XORed result
        ///   - in: Input bytes to XOR with the state
        ///
        /// Requires: out.len == in.len
        pub fn xorBytes(self: *Self, out: []u8, in: []const u8) void {
            debug.assert(out.len == in.len);

            var i: usize = 0;
            while (i + 8 <= in.len) : (i += 8) {
                const x = mem.readInt(u64, in[i..][0..8], native_endian) ^ mem.nativeTo(u64, self.st[i / 8], endian);
                mem.writeInt(u64, out[i..][0..8], x, native_endian);
            }
            if (i < in.len) {
                var padded: [8]u8 = @splat(0);
                @memcpy(padded[0 .. in.len - i], in[i..]);
                const x = mem.readInt(u64, &padded, native_endian) ^ mem.nativeTo(u64, self.st[i / 8], endian);
                mem.writeInt(u64, &padded, x, native_endian);
                @memcpy(out[i..], padded[0 .. in.len - i]);
            }
        }

        /// Set the words storing the bytes of a given range to zero.
        ///
        /// Parameters:
        ///   - from: Starting byte offset (inclusive)
        ///   - to: Ending byte offset (inclusive)
        ///
        /// Note: Clears complete words that contain the specified byte range
        pub fn clear(self: *Self, from: usize, to: usize) void {
            @memset(self.st[from / 8 .. (to + 7) / 8], 0);
        }

        /// Clear the entire state, disabling compiler optimizations.
        ///
        /// Uses secure zeroing to prevent the compiler from optimizing away
        /// the clearing operation. Use for sensitive data cleanup.
        pub fn secureZero(self: *Self) void {
            crypto.secureZero(u64, &self.st);
        }

        /// Apply a reduced-round permutation to the state.
        ///
        /// Parameters:
        ///   - rounds: Number of rounds to apply (1-12)
        ///
        /// Note: Uses the last `rounds` round constants from the full set
        pub fn permuteR(state: *Self, comptime rounds: u4) void {
            const rks = [16]u64{ 0x3c, 0x2d, 0x1e, 0x0f, 0xf0, 0xe1, 0xd2, 0xc3, 0xb4, 0xa5, 0x96, 0x87, 0x78, 0x69, 0x5a, 0x4b };
            inline for (rks[rks.len - rounds ..]) |rk| {
                state.round(rk);
            }
        }

        /// Apply a full-round permutation to the state.
        ///
        /// Applies the standard 12-round Ascon permutation.
        pub fn permute(state: *Self) void {
            state.permuteR(12);
        }

        /// Apply a permutation to the state and prevent backtracking.
        ///
        /// Parameters:
        ///   - rounds: Number of permutation rounds to apply
        ///   - rate: Rate in bytes (must be multiple of 8, < 40)
        ///
        /// The capacity portion is XORed before and after permutation to
        /// provide forward security (ratcheting).
        pub fn permuteRatchet(state: *Self, comptime rounds: u4, comptime rate: u6) void {
            const capacity = block_bytes - rate;
            debug.assert(capacity > 0 and capacity % 8 == 0); // capacity must be a multiple of 64 bits
            var mask: [capacity / 8]u64 = undefined;
            inline for (&mask, state.st[state.st.len - mask.len ..]) |*m, x| m.* = x;
            state.permuteR(rounds);
            inline for (mask, state.st[state.st.len - mask.len ..]) |m, *x| x.* ^= m;
        }

        /// Core Ascon permutation round function.
        ///
        /// Parameters:
        ///   - rk: Round constant for this round
        ///
        /// Implements one round of the Ascon permutation with S-box and linear layer.
        fn round(state: *Self, rk: u64) void {
            const x = &state.st;
            x[2] ^= rk;

            x[0] ^= x[4];
            x[4] ^= x[3];
            x[2] ^= x[1];
            var t: Block = .{
                x[0] ^ (~x[1] & x[2]),
                x[1] ^ (~x[2] & x[3]),
                x[2] ^ (~x[3] & x[4]),
                x[3] ^ (~x[4] & x[0]),
                x[4] ^ (~x[0] & x[1]),
            };
            t[1] ^= t[0];
            t[3] ^= t[2];
            t[0] ^= t[4];

            x[2] = t[2] ^ rotr(u64, t[2], 6 - 1);
            x[3] = t[3] ^ rotr(u64, t[3], 17 - 10);
            x[4] = t[4] ^ rotr(u64, t[4], 41 - 7);
            x[0] = t[0] ^ rotr(u64, t[0], 28 - 19);
            x[1] = t[1] ^ rotr(u64, t[1], 61 - 39);
            x[2] = t[2] ^ rotr(u64, x[2], 1);
            x[3] = t[3] ^ rotr(u64, x[3], 10);
            x[4] = t[4] ^ rotr(u64, x[4], 7);
            x[0] = t[0] ^ rotr(u64, x[0], 19);
            x[1] = t[1] ^ rotr(u64, x[1], 39);
            x[2] = ~x[2];
        }
    };
}

test "ascon" {
    const Ascon = State(.big);
    var bytes: [Ascon.block_bytes]u8 = undefined;
    @memset(&bytes, 1);
    var st = Ascon.init(bytes);
    var out: [Ascon.block_bytes]u8 = undefined;
    st.permute();
    st.extractBytes(&out);
    const expected1 = [_]u8{ 148, 147, 49, 226, 218, 221, 208, 113, 186, 94, 96, 10, 183, 219, 119, 150, 169, 206, 65, 18, 215, 97, 78, 106, 118, 81, 211, 150, 52, 17, 117, 64, 216, 45, 148, 240, 65, 181, 90, 180 };
    try testing.expectEqualSlices(u8, &expected1, &out);
    st.clear(0, 10);
    st.extractBytes(&out);
    const expected2 = [_]u8{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 169, 206, 65, 18, 215, 97, 78, 106, 118, 81, 211, 150, 52, 17, 117, 64, 216, 45, 148, 240, 65, 181, 90, 180 };
    try testing.expectEqualSlices(u8, &expected2, &out);
    st.addByte(1, 5);
    st.addByte(2, 5);
    st.extractBytes(&out);
    const expected3 = [_]u8{ 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 169, 206, 65, 18, 215, 97, 78, 106, 118, 81, 211, 150, 52, 17, 117, 64, 216, 45, 148, 240, 65, 181, 90, 180 };
    try testing.expectEqualSlices(u8, &expected3, &out);
    st.addBytes(&bytes);
    st.extractBytes(&out);
    const expected4 = [_]u8{ 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 168, 207, 64, 19, 214, 96, 79, 107, 119, 80, 210, 151, 53, 16, 116, 65, 217, 44, 149, 241, 64, 180, 91, 181 };
    try testing.expectEqualSlices(u8, &expected4, &out);
}

const AsconState = State(.little);
const AuthenticationError = crypto.errors.AuthenticationError;

/// Ascon-AEAD128 as specified in NIST SP 800-232 Section 4
pub const AsconAead128 = struct {
    pub const tag_length = 16;
    pub const nonce_length = 16;
    pub const key_length = 16;
    pub const block_length = 16;

    const AeadState = struct {
        st: AsconState,
        k0: u64,
        k1: u64,

        /// Initialize AEAD state with key and nonce.
        ///
        /// Parameters:
        ///   - key: 16-byte secret key
        ///   - nonce: 16-byte nonce
        ///
        /// Returns: Initialized AEAD state ready for processing
        fn init(key: [16]u8, nonce: [16]u8) AeadState {
            const k0 = mem.readInt(u64, key[0..8], .little);
            const k1 = mem.readInt(u64, key[8..16], .little);
            const n0 = mem.readInt(u64, nonce[0..8], .little);
            const n1 = mem.readInt(u64, nonce[8..16], .little);

            // IV for Ascon-AEAD128 (Ascon-128a)
            const iv: u64 = 0x00001000808C0001;
            const words: [5]u64 = .{ iv, k0, k1, n0, n1 };

            var st = AsconState.initFromWords(words);
            st.permuteR(12);

            st.st[3] ^= k0;
            st.st[4] ^= k1;

            return AeadState{ .st = st, .k0 = k0, .k1 = k1 };
        }

        /// Process associated data for authentication.
        ///
        /// Parameters:
        ///   - ad: Associated data to authenticate
        ///
        /// Updates the state to include AD in authentication tag computation.
        fn processAd(self: *AeadState, ad: []const u8) void {
            if (ad.len == 0) return;

            var i: usize = 0;
            // Process full 128-bit blocks
            while (i + 16 <= ad.len) : (i += 16) {
                self.st.addBytes(ad[i..][0..16]);
                self.st.permuteR(8);
            }

            // Process final partial AD block
            const adrem = ad.len - i;
            if (adrem > 0) {
                if (adrem >= 8) {
                    var buf: [8]u8 = @splat(0);
                    @memcpy(buf[0..8], ad[i..][0..8]);
                    self.st.st[0] ^= mem.readInt(u64, &buf, .little);

                    buf = @splat(0);
                    @memcpy(buf[0 .. adrem - 8], ad[i + 8 ..]);
                    buf[adrem - 8] = 0x01;
                    self.st.st[1] ^= mem.readInt(u64, &buf, .little);
                } else {
                    var buf: [8]u8 = @splat(0);
                    @memcpy(buf[0..adrem], ad[i..]);
                    buf[adrem] = 0x01;
                    self.st.st[0] ^= mem.readInt(u64, &buf, .little);
                }
                self.st.permuteR(8);
            }
        }

        /// Finalize the AEAD operation and prepare tag.
        ///
        /// Applies final permutation and XORs key for tag generation.
        fn finalize(self: *AeadState) void {
            // XOR key before final permutation
            self.st.st[2] ^= self.k0;
            self.st.st[3] ^= self.k1;
            self.st.permuteR(12);

            // XOR key again for tag generation
            self.st.st[3] ^= self.k0;
            self.st.st[4] ^= self.k1;
        }
    };

    /// Encrypt a message with Ascon-AEAD128.
    ///
    /// Parameters:
    ///   - c: Output buffer for ciphertext (must be same length as m)
    ///   - tag: Output buffer for authentication tag (16 bytes)
    ///   - m: Plaintext message to encrypt
    ///   - ad: Associated data to authenticate but not encrypt
    ///   - npub: Public nonce (16 bytes, must be unique per message)
    ///   - k: Secret key (16 bytes)
    ///
    /// Note: The ciphertext and tag must be transmitted together for decryption
    pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) void {
        debug.assert(c.len == m.len);

        var state = AeadState.init(k, npub);

        // Process associated data
        state.processAd(ad);

        // Domain separation (DSEP = 0x80 at byte 7 in little-endian)
        state.st.st[4] ^= 0x8000000000000000;

        // Process plaintext
        var i: usize = 0;
        while (i + 16 <= m.len) : (i += 16) {
            state.st.addBytes(m[i..][0..16]);
            state.st.extractBytes(c[i..][0..16]);
            state.st.permuteR(8);
        }

        // Process final partial block
        const remaining = m.len - i;
        if (remaining > 8) {
            // Split between two words
            state.st.addBytes(m[i..][0..8]);
            state.st.extractBytes(c[i..][0..8]);

            var buf: [8]u8 = @splat(0);
            @memcpy(buf[0 .. remaining - 8], m[i + 8 ..]);
            const m1 = mem.readInt(u64, &buf, .little);
            state.st.st[1] ^= m1;
            mem.writeInt(u64, buf[0..], state.st.st[1], .little);
            @memcpy(c[i + 8 ..], buf[0 .. remaining - 8]);

            // Add padding
            state.st.st[1] ^= @as(u64, 0x01) << @intCast((remaining - 8) * 8);
        } else if (remaining == 8) {
            // Exactly 8 bytes - all in word 0, padding in word 1
            state.st.addBytes(m[i..][0..8]);
            state.st.extractBytes(c[i..][0..8]);

            // Add padding to word 1 at position 0
            state.st.st[1] ^= 0x01;
        } else if (remaining > 0) {
            // All in first word
            var temp: [8]u8 = @splat(0);
            @memcpy(temp[0..remaining], m[i..]);
            state.st.addBytes(&temp);
            state.st.extractBytes(c[i..][0..remaining]);
            // Add padding
            temp = @splat(0);
            temp[remaining] = 0x01;
            state.st.addBytes(&temp);
            // Second word stays zero
        } else {
            // Empty message or exact multiple - add padding block
            var padded: [16]u8 = @splat(0);
            padded[0] = 0x01;
            state.st.addBytes(&padded);
        }

        // Finalization
        state.finalize();

        // Extract tag
        mem.writeInt(u64, tag[0..8], state.st.st[3], .little);
        mem.writeInt(u64, tag[8..16], state.st.st[4], .little);
    }

    /// Decrypt a message with Ascon-AEAD128.
    ///
    /// Parameters:
    ///   - m: Output buffer for plaintext (must be same length as c)
    ///   - c: Ciphertext to decrypt
    ///   - tag: Authentication tag (16 bytes)
    ///   - ad: Associated data that was authenticated
    ///   - npub: Public nonce used during encryption (16 bytes)
    ///   - k: Secret key (16 bytes)
    ///
    /// Returns: AuthenticationError if tag verification fails
    ///
    /// Note: On authentication failure, the output buffer is securely zeroed
    pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) AuthenticationError!void {
        debug.assert(m.len == c.len);

        var state = AeadState.init(k, npub);

        // Process associated data
        state.processAd(ad);

        // Domain separation (DSEP = 0x80 at byte 7 in little-endian)
        state.st.st[4] ^= 0x8000000000000000;

        // Process ciphertext
        var i: usize = 0;
        while (i + 16 <= c.len) : (i += 16) {
            const ct_block = c[i..][0..16].*; // Save ciphertext block for in-place operation support
            state.st.xorBytes(m[i..][0..16], &ct_block);
            state.st.setBytes(&ct_block);
            state.st.permuteR(8);
        }

        // Final partial ciphertext block
        const crem = c.len - i;
        if (crem > 8) {
            // Save ciphertext for in-place operation support
            var saved_ct: [16]u8 = undefined;
            @memcpy(saved_ct[0..crem], c[i..]);

            const c0 = mem.readInt(u64, saved_ct[0..8], .little);
            state.st.st[0] ^= c0;
            mem.writeInt(u64, m[i..][0..8], state.st.st[0], .little);
            state.st.st[0] = c0;

            var buf: [8]u8 = @splat(0);
            @memcpy(buf[0 .. crem - 8], saved_ct[8..][0 .. crem - 8]);
            const c1 = mem.readInt(u64, &buf, .little);
            const m1 = state.st.st[1] ^ c1;
            mem.writeInt(u64, buf[0..], m1, .little);
            @memcpy(m[i + 8 ..], buf[0 .. crem - 8]);

            // Replace only the bytes we've read, keeping upper bytes intact
            const mask = (@as(u64, 1) << @intCast((crem - 8) * 8)) - 1;
            state.st.st[1] = (state.st.st[1] & ~mask) | (c1 & mask);

            state.st.st[1] ^= @as(u64, 0x01) << @intCast((crem - 8) * 8);
        } else if (crem == 8) {
            // Exactly 8 bytes - process only word 0, add padding to word 1
            const saved_ct = c[i..][0..8].*;

            const c0 = mem.readInt(u64, &saved_ct, .little);
            state.st.st[0] ^= c0;
            mem.writeInt(u64, m[i..][0..8], state.st.st[0], .little);
            state.st.st[0] = c0;

            // Add padding to word 1 at position 0
            state.st.st[1] ^= 0x01;
        } else if (crem > 0) {
            var buf: [8]u8 = @splat(0);
            @memcpy(buf[0..crem], c[i..]);
            const c0 = mem.readInt(u64, &buf, .little);
            const m0 = state.st.st[0] ^ c0;
            mem.writeInt(u64, buf[0..], m0, .little);
            @memcpy(m[i..], buf[0..crem]);

            // Replace only the bytes we've read, keeping upper bytes intact
            const mask = (@as(u64, 1) << @intCast(crem * 8)) - 1;
            state.st.st[0] = (state.st.st[0] & ~mask) | (c0 & mask);

            state.st.st[0] ^= @as(u64, 0x01) << @intCast(crem * 8);
        } else {
            state.st.st[0] ^= 0x01;
        }

        // Finalization
        state.finalize();

        // Verify tag
        var computed_tag: [tag_length]u8 = undefined;
        mem.writeInt(u64, computed_tag[0..8], state.st.st[3], .little);
        mem.writeInt(u64, computed_tag[8..16], state.st.st[4], .little);

        if (!crypto.timing_safe.eql([tag_length]u8, tag, computed_tag)) {
            crypto.secureZero(u8, m);
            return error.AuthenticationFailed;
        }
    }
};

/// Ascon-Hash256 as specified in NIST SP 800-232 Section 5
pub const AsconHash256 = struct {
    pub const digest_length = 32;
    pub const block_length = 8;

    st: AsconState,

    pub const Options = struct {};

    /// Initialize a new Ascon-Hash256 hasher.
    ///
    /// Parameters:
    ///   - options: Configuration options (currently unused)
    ///
    /// Returns: An initialized AsconHash256 hasher
    pub fn init(options: Options) AsconHash256 {
        _ = options;

        // IV for Ascon-Hash256: 0x0000080100cc0002
        const iv: u64 = 0x0000080100cc0002;
        const words: [5]u64 = .{ iv, 0, 0, 0, 0 };
        var st = AsconState.initFromWords(words);
        st.permuteR(12);
        return AsconHash256{ .st = st };
    }

    /// Compute Ascon-Hash256 hash of input data in one call.
    ///
    /// Parameters:
    ///   - b: Input data to hash
    ///   - out: Output buffer for 32-byte hash digest
    ///   - options: Configuration options (currently unused)
    pub fn hash(b: []const u8, out: *[digest_length]u8, options: Options) void {
        var h = init(options);
        h.update(b);
        h.final(out);
    }

    /// Update the hash state with additional data.
    ///
    /// Parameters:
    ///   - b: Data to add to the hash
    ///
    /// Note: Can be called multiple times before final()
    pub fn update(self: *AsconHash256, b: []const u8) void {
        var i: usize = 0;

        // Process full 64-bit blocks
        while (i + 8 <= b.len) : (i += 8) {
            self.st.addBytes(b[i..][0..8]);
            self.st.permuteR(12);
        }

        // Store partial block for finalization
        if (i < b.len) {
            var padded: [8]u8 = @splat(0);
            const remaining = b.len - i;
            @memcpy(padded[0..remaining], b[i..]);
            padded[remaining] = 0x01;
            self.st.addBytes(&padded);
        } else {
            // Add padding block
            var padded: [8]u8 = @splat(0);
            padded[0] = 0x01;
            self.st.addBytes(&padded);
        }
    }

    /// Finalize the hash and output the digest.
    ///
    /// Parameters:
    ///   - out: Output buffer for 32-byte hash digest
    ///
    /// Note: After calling final(), the hasher should not be used again
    pub fn final(self: *AsconHash256, out: *[digest_length]u8) void {
        // Final permutation after padding
        self.st.permuteR(12);

        // Extract hash output (4 × 64 bits = 256 bits)
        var h: [4]u64 = undefined;
        for (0..4) |i| {
            h[i] = self.st.st[0];
            self.st.permuteR(12);
        }

        // Write output
        for (0..4) |i| {
            mem.writeInt(u64, out[i * 8 ..][0..8], h[i], .little);
        }
    }
};

/// Ascon-XOF128 as specified in NIST SP 800-232 Section 5
pub const AsconXof128 = struct {
    pub const block_length = 8;

    st: AsconState,
    squeezed: bool,

    pub const Options = struct {};

    /// Initialize a new Ascon-XOF128 extendable output function.
    ///
    /// Parameters:
    ///   - options: Configuration options (currently unused)
    ///
    /// Returns: An initialized AsconXof128 instance
    pub fn init(options: Options) AsconXof128 {
        _ = options;

        // IV for Ascon-XOF128: 0x0000080000cc0003
        const iv: u64 = 0x0000080000cc0003;
        const words: [5]u64 = .{ iv, 0, 0, 0, 0 };
        var st = AsconState.initFromWords(words);
        st.permuteR(12);
        return AsconXof128{ .st = st, .squeezed = false };
    }

    /// Hash a slice of bytes with variable-length output.
    ///
    /// Parameters:
    ///   - bytes: Input data to hash
    ///   - out: Output buffer (can be any length)
    ///   - options: Configuration options (currently unused)
    ///
    /// Note: Convenience function that combines init, update, and squeeze
    pub fn hash(bytes: []const u8, out: []u8, options: Options) void {
        var st = init(options);
        st.update(bytes);
        st.squeeze(out);
    }

    /// Update the XOF state with additional data.
    ///
    /// Parameters:
    ///   - b: Data to absorb into the XOF state
    ///
    /// Note: Cannot be called after squeeze() has been called
    pub fn update(self: *AsconXof128, b: []const u8) void {
        debug.assert(!self.squeezed); // Cannot update after squeezing

        var i: usize = 0;

        // Process full 64-bit blocks
        while (i + 8 <= b.len) : (i += 8) {
            self.st.addBytes(b[i..][0..8]);
            self.st.permuteR(12);
        }

        // Store partial block for finalization
        if (i < b.len) {
            var padded: [8]u8 = @splat(0);
            const remaining = b.len - i;
            @memcpy(padded[0..remaining], b[i..]);
            padded[remaining] = 0x01;
            self.st.addBytes(&padded);
        } else {
            // Add padding block
            var padded: [8]u8 = @splat(0);
            padded[0] = 0x01;
            self.st.addBytes(&padded);
        }
    }

    /// Squeeze output bytes from the XOF.
    ///
    /// Parameters:
    ///   - out: Output buffer to fill with pseudorandom bytes
    ///
    /// Note: Can be called multiple times to generate more output.
    /// After first call, no more data can be absorbed with update().
    pub fn squeeze(self: *AsconXof128, out: []u8) void {
        if (!self.squeezed) {
            // First squeeze - apply final permutation
            self.st.permuteR(12);
            self.squeezed = true;
        }

        var i: usize = 0;
        while (i < out.len) {
            const to_copy = @min(8, out.len - i);
            var block: [8]u8 = undefined;
            mem.writeInt(u64, &block, self.st.st[0], .little);
            @memcpy(out[i..][0..to_copy], block[0..to_copy]);
            i += to_copy;

            if (i < out.len) {
                self.st.permuteR(12);
            }
        }
    }
};

/// Ascon-CXOF128 as specified in NIST SP 800-232 Section 5
pub const AsconCxof128 = struct {
    pub const block_length = 8;
    pub const max_custom_length = 256; // 2048 bits

    st: AsconState,
    squeezed: bool,

    pub const Options = struct { custom: []const u8 = "" };

    /// Initialize a new Ascon-CXOF128 customizable XOF.
    ///
    /// Parameters:
    ///   - options: Configuration with optional customization string
    ///     - custom: Customization string (max 256 bytes)
    ///
    /// Returns: An initialized AsconCxof128 instance
    ///
    /// Note: Different customization strings produce independent XOF instances
    pub fn init(options: Options) AsconCxof128 {
        debug.assert(options.custom.len <= max_custom_length);

        // IV for Ascon-CXOF128: 0x0000080000cc0004
        const iv: u64 = 0x0000080000cc0004;
        const words: [5]u64 = .{ iv, 0, 0, 0, 0 };
        var st = AsconState.initFromWords(words);
        st.permuteR(12);

        var self = AsconCxof128{ .st = st, .squeezed = false };

        // Process customization string - always process length and padding
        // First block: length of customization string
        const len_block = @as(u64, options.custom.len * 8); // Length in bits
        self.st.st[0] ^= len_block;
        self.st.permuteR(12);

        if (options.custom.len > 0) {
            // Process customization string blocks
            var i: usize = 0;
            while (i + 8 <= options.custom.len) : (i += 8) {
                self.st.addBytes(options.custom[i..][0..8]);
                self.st.permuteR(12);
            }

            // Process final partial block with padding
            if (i < options.custom.len) {
                var padded: [8]u8 = @splat(0);
                const remaining = options.custom.len - i;
                @memcpy(padded[0..remaining], options.custom[i..]);
                padded[remaining] = 0x01;
                self.st.addBytes(&padded);
                self.st.permuteR(12);
            } else {
                // Add padding block
                var padded: [8]u8 = @splat(0);
                padded[0] = 0x01;
                self.st.addBytes(&padded);
                self.st.permuteR(12);
            }
        } else {
            // Empty customization still needs padding
            var padded: [8]u8 = @splat(0);
            padded[0] = 0x01;
            self.st.addBytes(&padded);
            self.st.permuteR(12);
        }

        return self;
    }

    /// Hash a slice of bytes with customization and variable-length output.
    ///
    /// Parameters:
    ///   - bytes: Input data to hash
    ///   - out: Output buffer (can be any length)
    ///   - options: Configuration with optional customization string
    ///
    /// Note: Convenience function that combines init, update, and squeeze
    pub fn hash(bytes: []const u8, out: []u8, options: Options) void {
        var st = init(options);
        st.update(bytes);
        st.squeeze(out);
    }

    /// Update the CXOF state with additional data.
    ///
    /// Parameters:
    ///   - b: Data to absorb into the CXOF state
    ///
    /// Note: Cannot be called after squeeze() has been called
    pub fn update(self: *AsconCxof128, b: []const u8) void {
        debug.assert(!self.squeezed);

        var i: usize = 0;

        // Process full 64-bit blocks
        while (i + 8 <= b.len) : (i += 8) {
            self.st.addBytes(b[i..][0..8]);
            self.st.permuteR(12);
        }

        // Store partial block for finalization
        if (i < b.len) {
            var padded: [8]u8 = @splat(0);
            const remaining = b.len - i;
            @memcpy(padded[0..remaining], b[i..]);
            padded[remaining] = 0x01;
            self.st.addBytes(&padded);
        } else {
            // Add padding block
            var padded: [8]u8 = @splat(0);
            padded[0] = 0x01;
            self.st.addBytes(&padded);
        }
    }

    /// Squeeze output bytes from the customizable XOF.
    ///
    /// Parameters:
    ///   - out: Output buffer to fill with pseudorandom bytes
    ///
    /// Note: Can be called multiple times to generate more output.
    /// After first call, no more data can be absorbed with update().
    pub fn squeeze(self: *AsconCxof128, out: []u8) void {
        if (!self.squeezed) {
            // First squeeze - apply final permutation
            self.st.permuteR(12);
            self.squeezed = true;
        }

        var i: usize = 0;
        while (i < out.len) {
            const to_copy = @min(8, out.len - i);
            var block: [8]u8 = undefined;
            mem.writeInt(u64, &block, self.st.st[0], .little);
            @memcpy(out[i..][0..to_copy], block[0..to_copy]);
            i += to_copy;

            if (i < out.len) {
                self.st.permuteR(12);
            }
        }
    }
};

test "Ascon-Hash256 basic test" {
    const message = "The quick brown fox jumps over the lazy dog";
    var hash: [32]u8 = undefined;

    AsconHash256.hash(message, &hash, .{});

    // Verify hash is generated (exact value depends on test vectors)
    try testing.expect(hash.len == 32);
}

test "Ascon-XOF128 basic test" {
    var xof = AsconXof128.init(.{});
    xof.update("Hello, ");
    xof.update("World!");

    var out1: [16]u8 = undefined;
    xof.squeeze(&out1);

    var out2: [32]u8 = undefined;
    xof.squeeze(&out2);

    // XOF outputs should be continuous - out2 should NOT match out1
    // Each squeeze produces new output
    try testing.expect(!mem.eql(u8, &out1, out2[0..16]));
}

test "Ascon-CXOF128 with customization" {
    const custom = "MyCustomString";
    var xof = AsconCxof128.init(.{ .custom = custom });
    xof.update("Test message");

    var out: [32]u8 = undefined;
    xof.squeeze(&out);

    // Different customization should give different output
    var xof2 = AsconCxof128.init(.{ .custom = "DifferentCustom" });
    xof2.update("Test message");

    var out2: [32]u8 = undefined;
    xof2.squeeze(&out2);

    try testing.expect(!mem.eql(u8, &out, &out2));
}

test "Ascon-AEAD128 round trip with various data sizes" {
    if (builtin.cpu.has(.riscv, .v) and builtin.zig_backend == .stage2_llvm) return error.SkipZigTest;

    const key = [_]u8{ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0xFE, 0xDC, 0xBA, 0x98, 0x76, 0x54, 0x32, 0x10 };
    const nonce = [_]u8{ 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };

    // Test with empty plaintext
    {
        const plaintext = "";
        const ad = "metadata";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try AsconAead128.decrypt(&decrypted, &ciphertext, tag, ad, nonce, key);
        try testing.expectEqualStrings(plaintext, &decrypted);
    }

    // Test with small plaintext
    {
        const plaintext = "Short";
        const ad = "";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try AsconAead128.decrypt(&decrypted, &ciphertext, tag, ad, nonce, key);
        try testing.expectEqualStrings(plaintext, &decrypted);
    }

    // Test with longer plaintext and associated data
    {
        const plaintext = "This is a longer message to test the round trip encryption and decryption process";
        const ad = "Additional authenticated data that is not encrypted but is authenticated";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try AsconAead128.decrypt(&decrypted, &ciphertext, tag, ad, nonce, key);
        try testing.expectEqualStrings(plaintext, &decrypted);
    }

    // Test authentication failure with tampered ciphertext
    {
        const plaintext = "Tamper test";
        const ad = "metadata";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        // Tamper with ciphertext
        ciphertext[0] ^= 0xFF;

        var decrypted: [plaintext.len]u8 = undefined;
        const result = AsconAead128.decrypt(&decrypted, &ciphertext, tag, ad, nonce, key);
        try testing.expectError(error.AuthenticationFailed, result);
    }

    // Test authentication failure with wrong tag
    {
        const plaintext = "Tag test";
        const ad = "metadata";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        // Tamper with tag
        var wrong_tag = tag;
        wrong_tag[0] ^= 0xFF;

        var decrypted: [plaintext.len]u8 = undefined;
        const result = AsconAead128.decrypt(&decrypted, &ciphertext, wrong_tag, ad, nonce, key);
        try testing.expectError(error.AuthenticationFailed, result);
    }

    // Test authentication failure with wrong associated data
    {
        const plaintext = "AD test";
        const ad = "original";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        var decrypted: [plaintext.len]u8 = undefined;
        const wrong_ad = "modified";
        const result = AsconAead128.decrypt(&decrypted, &ciphertext, tag, wrong_ad, nonce, key);
        try testing.expectError(error.AuthenticationFailed, result);
    }
}

// Test vectors from NIST SP 800-232 / ascon-c reference implementation
test "Ascon-AEAD128 official test vectors" {

    // Test vector 1: Empty PT, Empty AD
    {
        var key: [16]u8 = undefined;
        var nonce: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&key, "000102030405060708090A0B0C0D0E0F") catch unreachable;
        _ = std.fmt.hexToBytes(&nonce, "101112131415161718191A1B1C1D1E1F") catch unreachable;

        const plaintext = "";
        const ad = "";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, ad, nonce, key);

        var expected_tag: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected_tag, "4F9C278211BEC9316BF68F46EE8B2EC6") catch unreachable;
        try testing.expectEqualSlices(u8, &expected_tag, &tag);
    }

    // Test vector 2: Empty PT, AD = "30"
    {
        var key: [16]u8 = undefined;
        var nonce: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&key, "000102030405060708090A0B0C0D0E0F") catch unreachable;
        _ = std.fmt.hexToBytes(&nonce, "101112131415161718191A1B1C1D1E1F") catch unreachable;

        const plaintext = "";
        var ad: [1]u8 = undefined;
        _ = std.fmt.hexToBytes(&ad, "30") catch unreachable;
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, plaintext, &ad, nonce, key);

        var expected_tag: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected_tag, "CCCB674FE18A09A285D6AB11B35675C0") catch unreachable;
        try testing.expectEqualSlices(u8, &expected_tag, &tag);
    }

    // Test vector 34: Single byte plaintext 0x20
    {
        var key: [16]u8 = undefined;
        var nonce: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&key, "000102030405060708090A0B0C0D0E0F") catch unreachable;
        _ = std.fmt.hexToBytes(&nonce, "101112131415161718191A1B1C1D1E1F") catch unreachable;

        var plaintext: [1]u8 = undefined;
        _ = std.fmt.hexToBytes(&plaintext, "20") catch unreachable;
        const ad = "";
        var ciphertext: [1]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, &plaintext, ad, nonce, key);

        var expected_ct: [1]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected_ct, "E8") catch unreachable;
        var expected_tag: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected_tag, "DD576ABA1CD3E6FC704DE02AEDB79588") catch unreachable;

        try testing.expectEqualSlices(u8, &expected_ct, &ciphertext);
        try testing.expectEqualSlices(u8, &expected_tag, &tag);

        // Verify decryption
        var decrypted: [1]u8 = undefined;
        try AsconAead128.decrypt(&decrypted, &ciphertext, tag, ad, nonce, key);
        try testing.expectEqualSlices(u8, &plaintext, &decrypted);
    }

    // Test vector with 3-byte plaintext
    {
        var key: [16]u8 = undefined;
        var nonce: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&key, "000102030405060708090A0B0C0D0E0F") catch unreachable;
        _ = std.fmt.hexToBytes(&nonce, "101112131415161718191A1B1C1D1E1F") catch unreachable;

        var plaintext: [3]u8 = undefined;
        _ = std.fmt.hexToBytes(&plaintext, "202122") catch unreachable;
        const ad = "";
        var ciphertext: [3]u8 = undefined;
        var tag: [16]u8 = undefined;

        AsconAead128.encrypt(&ciphertext, &tag, &plaintext, ad, nonce, key);

        var expected_ct: [3]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected_ct, "E8C3DE") catch unreachable;
        var expected_tag: [16]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected_tag, "AF8E12816B8EDF39AD1571A9492B7CA2") catch unreachable;

        try testing.expectEqualSlices(u8, &expected_ct, &ciphertext);
        try testing.expectEqualSlices(u8, &expected_tag, &tag);

        // Verify decryption
        var decrypted: [3]u8 = undefined;
        try AsconAead128.decrypt(&decrypted, &ciphertext, tag, ad, nonce, key);
        try testing.expectEqualSlices(u8, &plaintext, &decrypted);
    }
}

test "Ascon-Hash256 official test vectors" {

    // Test vector 1: Empty message
    {
        const message = "";
        var hash: [32]u8 = undefined;
        AsconHash256.hash(message, &hash, .{});

        var expected: [32]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "0B3BE5850F2F6B98CAF29F8FDEA89B64A1FA70AA249B8F839BD53BAA304D92B2") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &hash);
    }

    // Test vector 2: Single byte 0x00
    {
        const message = [_]u8{0x00};
        var hash: [32]u8 = undefined;
        AsconHash256.hash(&message, &hash, .{});

        var expected: [32]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "0728621035AF3ED2BCA03BF6FDE900F9456F5330E4B5EE23E7F6A1E70291BC80") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &hash);
    }

    // Test vector 3: 0x00, 0x01
    {
        const message = [_]u8{ 0x00, 0x01 };
        var hash: [32]u8 = undefined;
        AsconHash256.hash(&message, &hash, .{});

        var expected: [32]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "6115E7C9C4081C2797FC8FE1BC57A836AFA1C5381E556DD583860CA2DFB48DD2") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &hash);
    }

    // Test vector 4: 0x00, 0x01, 0x02
    {
        const message = [_]u8{ 0x00, 0x01, 0x02 };
        var hash: [32]u8 = undefined;
        AsconHash256.hash(&message, &hash, .{});

        var expected: [32]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "265AB89A609F5A05DCA57E83FBBA700F9A2D2C4211BA4CC9F0A1A369E17B915C") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &hash);
    }

    // Test vector 5: 0x00..0x03
    {
        const message = [_]u8{ 0x00, 0x01, 0x02, 0x03 };
        var hash: [32]u8 = undefined;
        AsconHash256.hash(&message, &hash, .{});

        var expected: [32]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "D7E4C7ED9B8A325CD08B9EF259F8877054ECD8304FE1B2D7FD847137DF6727EE") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &hash);
    }
}

test "Ascon-XOF128 official test vectors" {

    // Test vector 1: Empty message, 64-byte output
    {
        var xof = AsconXof128.init(.{});
        xof.update("");

        var output: [64]u8 = undefined;
        xof.squeeze(&output);

        var expected: [64]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "473D5E6164F58B39DFD84AACDB8AE42EC2D91FED33388EE0D960D9B3993295C6AD77855A5D3B13FE6AD9E6098988373AF7D0956D05A8F1665D2C67D1A3AD10FF") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &output);
    }

    // Test vector 2: Single byte 0x00, 64-byte output
    {
        var xof = AsconXof128.init(.{});
        const msg = [_]u8{0x00};
        xof.update(&msg);

        var output: [64]u8 = undefined;
        xof.squeeze(&output);

        var expected: [64]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "51430E0438ECDF642B393630D977625F5F337656BA58AB1E960784AC32A16E0D446405551F5469384F8EA283CF12E64FA72C426BFEBAEA3AA1529E2C4AB23A2F") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &output);
    }

    // Test vector 3: 0x00, 0x01, 64-byte output
    {
        var xof = AsconXof128.init(.{});
        const msg = [_]u8{ 0x00, 0x01 };
        xof.update(&msg);

        var output: [64]u8 = undefined;
        xof.squeeze(&output);

        var expected: [64]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "A05383077AF971D3830BD37E7B981497A773D441DB077C6494CC73125953846EB6427FBA4CD308FF90A11385D51101341BF5379249217BFDACE9CCA1148CC966") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &output);
    }
}

test "Ascon-CXOF128 official test vectors" {

    // Test vector 1: Empty message, empty customization, 64-byte output
    {
        var xof = AsconCxof128.init(.{});
        xof.update("");

        var output: [64]u8 = undefined;
        xof.squeeze(&output);

        var expected: [64]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "4F50159EF70BB3DAD8807E034EAEBD44C4FA2CBBC8CF1F05511AB66CDCC529905CA12083FC186AD899B270B1473DC5F7EC88D1052082DCDFE69FB75D269E7B74") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &output);
    }

    // Test vector 2: Empty message, customization = 0x10, 64-byte output
    {
        const custom = [_]u8{0x10};
        var xof = AsconCxof128.init(.{ .custom = &custom });
        xof.update("");

        var output: [64]u8 = undefined;
        xof.squeeze(&output);

        var expected: [64]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "0C93A483E7D574D49FE52CCE03EE646117977D57A8AA57704AB4DAF44B501430FF6AC11A5D1FD6F2154B5C65728268270C8BB578508487B8965718ADA6272FD6") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &output);
    }

    // Test vector 3: Empty message, customization = 0x10, 0x11, 64-byte output
    {
        const custom = [_]u8{ 0x10, 0x11 };
        var xof = AsconCxof128.init(.{ .custom = &custom });
        xof.update("");

        var output: [64]u8 = undefined;
        xof.squeeze(&output);

        var expected: [64]u8 = undefined;
        _ = std.fmt.hexToBytes(&expected, "D1106C7622E79FE955BD9D79E03B918E770FE0E0CDDDE28BEB924B02C5FC936B33ACCA299C89ECA5D71886CBBFA4D54A21C55FDE2B679F5E2488063A1719DC32") catch unreachable;
        try testing.expectEqualSlices(u8, &expected, &output);
    }
}



---
File: /std/crypto/bcrypt.zig
---

const std = @import("std");
const base64 = std.base64;
const crypto = std.crypto;
const debug = std.debug;
const fmt = std.fmt;
const math = std.math;
const mem = std.mem;
const pwhash = crypto.pwhash;
const testing = std.testing;
const HmacSha512 = crypto.auth.hmac.sha2.HmacSha512;
const Sha512 = crypto.hash.sha2.Sha512;

const phc_format = @import("phc_encoding.zig");

const KdfError = pwhash.KdfError;
const HasherError = pwhash.HasherError;
const EncodingError = phc_format.Error;
const Error = pwhash.Error;

pub const salt_length: usize = 16;
const salt_str_length: usize = 22;
const ct_str_length: usize = 31;
const ct_length: usize = 24;
const dk_length: usize = ct_length - 1;

/// Length (in bytes) of a password hash in crypt encoding
pub const hash_length: usize = 60;

pub const State = struct {
    sboxes: [4][256]u32 = [4][256]u32{
        .{
            0xd1310ba6, 0x98dfb5ac, 0x2ffd72db, 0xd01adfb7,
            0xb8e1afed, 0x6a267e96, 0xba7c9045, 0xf12c7f99,
            0x24a19947, 0xb3916cf7, 0x0801f2e2, 0x858efc16,
            0x636920d8, 0x71574e69, 0xa458fea3, 0xf4933d7e,
            0x0d95748f, 0x728eb658, 0x718bcd58, 0x82154aee,
            0x7b54a41d, 0xc25a59b5, 0x9c30d539, 0x2af26013,
            0xc5d1b023, 0x286085f0, 0xca417918, 0xb8db38ef,
            0x8e79dcb0, 0x603a180e, 0x6c9e0e8b, 0xb01e8a3e,
            0xd71577c1, 0xbd314b27, 0x78af2fda, 0x55605c60,
            0xe65525f3, 0xaa55ab94, 0x57489862, 0x63e81440,
            0x55ca396a, 0x2aab10b6, 0xb4cc5c34, 0x1141e8ce,
            0xa15486af, 0x7c72e993, 0xb3ee1411, 0x636fbc2a,
            0x2ba9c55d, 0x741831f6, 0xce5c3e16, 0x9b87931e,
            0xafd6ba33, 0x6c24cf5c, 0x7a325381, 0x28958677,
            0x3b8f4898, 0x6b4bb9af, 0xc4bfe81b, 0x66282193,
            0x61d809cc, 0xfb21a991, 0x487cac60, 0x5dec8032,
            0xef845d5d, 0xe98575b1, 0xdc262302, 0xeb651b88,
            0x23893e81, 0xd396acc5, 0x0f6d6ff3, 0x83f44239,
            0x2e0b4482, 0xa4842004, 0x69c8f04a, 0x9e1f9b5e,
            0x21c66842, 0xf6e96c9a, 0x670c9c61, 0xabd388f0,
            0x6a51a0d2, 0xd8542f68, 0x960fa728, 0xab5133a3,
            0x6eef0b6c, 0x137a3be4, 0xba3bf050, 0x7efb2a98,
            0xa1f1651d, 0x39af0176, 0x66ca593e, 0x82430e88,
            0x8cee8619, 0x456f9fb4, 0x7d84a5c3, 0x3b8b5ebe,
            0xe06f75d8, 0x85c12073, 0x401a449f, 0x56c16aa6,
            0x4ed3aa62, 0x363f7706, 0x1bfedf72, 0x429b023d,
            0x37d0d724, 0xd00a1248, 0xdb0fead3, 0x49f1c09b,
            0x075372c9, 0x80991b7b, 0x25d479d8, 0xf6e8def7,
            0xe3fe501a, 0xb6794c3b, 0x976ce0bd, 0x04c006ba,
            0xc1a94fb6, 0x409f60c4, 0x5e5c9ec2, 0x196a2463,
            0x68fb6faf, 0x3e6c53b5, 0x1339b2eb, 0x3b52ec6f,
            0x6dfc511f, 0x9b30952c, 0xcc814544, 0xaf5ebd09,
            0xbee3d004, 0xde334afd, 0x660f2807, 0x192e4bb3,
            0xc0cba857, 0x45c8740f, 0xd20b5f39, 0xb9d3fbdb,
            0x5579c0bd, 0x1a60320a, 0xd6a100c6, 0x402c7279,
            0x679f25fe, 0xfb1fa3cc, 0x8ea5e9f8, 0xdb3222f8,
            0x3c7516df, 0xfd616b15, 0x2f501ec8, 0xad0552ab,
            0x323db5fa, 0xfd238760, 0x53317b48, 0x3e00df82,
            0x9e5c57bb, 0xca6f8ca0, 0x1a87562e, 0xdf1769db,
            0xd542a8f6, 0x287effc3, 0xac6732c6, 0x8c4f5573,
            0x695b27b0, 0xbbca58c8, 0xe1ffa35d, 0xb8f011a0,
            0x10fa3d98, 0xfd2183b8, 0x4afcb56c, 0x2dd1d35b,
            0x9a53e479, 0xb6f84565, 0xd28e49bc, 0x4bfb9790,
            0xe1ddf2da, 0xa4cb7e33, 0x62fb1341, 0xcee4c6e8,
            0xef20cada, 0x36774c01, 0xd07e9efe, 0x2bf11fb4,
            0x95dbda4d, 0xae909198, 0xeaad8e71, 0x6b93d5a0,
            0xd08ed1d0, 0xafc725e0, 0x8e3c5b2f, 0x8e7594b7,
            0x8ff6e2fb, 0xf2122b64, 0x8888b812, 0x900df01c,
            0x4fad5ea0, 0x688fc31c, 0xd1cff191, 0xb3a8c1ad,
            0x2f2f2218, 0xbe0e1777, 0xea752dfe, 0x8b021fa1,
            0xe5a0cc0f, 0xb56f74e8, 0x18acf3d6, 0xce89e299,
            0xb4a84fe0, 0xfd13e0b7, 0x7cc43b81, 0xd2ada8d9,
            0x165fa266, 0x80957705, 0x93cc7314, 0x211a1477,
            0xe6ad2065, 0x77b5fa86, 0xc75442f5, 0xfb9d35cf,
            0xebcdaf0c, 0x7b3e89a0, 0xd6411bd3, 0xae1e7e49,
            0x00250e2d, 0x2071b35e, 0x226800bb, 0x57b8e0af,
            0x2464369b, 0xf009b91e, 0x5563911d, 0x59dfa6aa,
            0x78c14389, 0xd95a537f, 0x207d5ba2, 0x02e5b9c5,
            0x83260376, 0x6295cfa9, 0x11c81968, 0x4e734a41,
            0xb3472dca, 0x7b14a94a, 0x1b510052, 0x9a532915,
            0xd60f573f, 0xbc9bc6e4, 0x2b60a476, 0x81e67400,
            0x08ba6fb5, 0x571be91f, 0xf296ec6b, 0x2a0dd915,
            0xb6636521, 0xe7b9f9b6, 0xff34052e, 0xc5855664,
            0x53b02d5d, 0xa99f8fa1, 0x08ba4799, 0x6e85076a,
        },
        .{
            0x4b7a70e9, 0xb5b32944, 0xdb75092e, 0xc4192623,
            0xad6ea6b0, 0x49a7df7d, 0x9cee60b8, 0x8fedb266,
            0xecaa8c71, 0x699a17ff, 0x5664526c, 0xc2b19ee1,
            0x193602a5, 0x75094c29, 0xa0591340, 0xe4183a3e,
            0x3f54989a, 0x5b429d65, 0x6b8fe4d6, 0x99f73fd6,
            0xa1d29c07, 0xefe830f5, 0x4d2d38e6, 0xf0255dc1,
            0x4cdd2086, 0x8470eb26, 0x6382e9c6, 0x021ecc5e,
            0x09686b3f, 0x3ebaefc9, 0x3c971814, 0x6b6a70a1,
            0x687f3584, 0x52a0e286, 0xb79c5305, 0xaa500737,
            0x3e07841c, 0x7fdeae5c, 0x8e7d44ec, 0x5716f2b8,
            0xb03ada37, 0xf0500c0d, 0xf01c1f04, 0x0200b3ff,
            0xae0cf51a, 0x3cb574b2, 0x25837a58, 0xdc0921bd,
            0xd19113f9, 0x7ca92ff6, 0x94324773, 0x22f54701,
            0x3ae5e581, 0x37c2dadc, 0xc8b57634, 0x9af3dda7,
            0xa9446146, 0x0fd0030e, 0xecc8c73e, 0xa4751e41,
            0xe238cd99, 0x3bea0e2f, 0x3280bba1, 0x183eb331,
            0x4e548b38, 0x4f6db908, 0x6f420d03, 0xf60a04bf,
            0x2cb81290, 0x24977c79, 0x5679b072, 0xbcaf89af,
            0xde9a771f, 0xd9930810, 0xb38bae12, 0xdccf3f2e,
            0x5512721f, 0x2e6b7124, 0x501adde6, 0x9f84cd87,
            0x7a584718, 0x7408da17, 0xbc9f9abc, 0xe94b7d8c,
            0xec7aec3a, 0xdb851dfa, 0x63094366, 0xc464c3d2,
            0xef1c1847, 0x3215d908, 0xdd433b37, 0x24c2ba16,
            0x12a14d43, 0x2a65c451, 0x50940002, 0x133ae4dd,
            0x71dff89e, 0x10314e55, 0x81ac77d6, 0x5f11199b,
            0x043556f1, 0xd7a3c76b, 0x3c11183b, 0x5924a509,
            0xf28fe6ed, 0x97f1fbfa, 0x9ebabf2c, 0x1e153c6e,
            0x86e34570, 0xeae96fb1, 0x860e5e0a, 0x5a3e2ab3,
            0x771fe71c, 0x4e3d06fa, 0x2965dcb9, 0x99e71d0f,
            0x803e89d6, 0x5266c825, 0x2e4cc978, 0x9c10b36a,
            0xc6150eba, 0x94e2ea78, 0xa5fc3c53, 0x1e0a2df4,
            0xf2f74ea7, 0x361d2b3d, 0x1939260f, 0x19c27960,
            0x5223a708, 0xf71312b6, 0xebadfe6e, 0xeac31f66,
            0xe3bc4595, 0xa67bc883, 0xb17f37d1, 0x018cff28,
            0xc332ddef, 0xbe6c5aa5, 0x65582185, 0x68ab9802,
            0xeecea50f, 0xdb2f953b, 0x2aef7dad, 0x5b6e2f84,
            0x1521b628, 0x29076170, 0xecdd4775, 0x619f1510,
            0x13cca830, 0xeb61bd96, 0x0334fe1e, 0xaa0363cf,
            0xb5735c90, 0x4c70a239, 0xd59e9e0b, 0xcbaade14,
            0xeecc86bc, 0x60622ca7, 0x9cab5cab, 0xb2f3846e,
            0x648b1eaf, 0x19bdf0ca, 0xa02369b9, 0x655abb50,
            0x40685a32, 0x3c2ab4b3, 0x319ee9d5, 0xc021b8f7,
            0x9b540b19, 0x875fa099, 0x95f7997e, 0x623d7da8,
            0xf837889a, 0x97e32d77, 0x11ed935f, 0x16681281,
            0x0e358829, 0xc7e61fd6, 0x96dedfa1, 0x7858ba99,
            0x57f584a5, 0x1b227263, 0x9b83c3ff, 0x1ac24696,
            0xcdb30aeb, 0x532e3054, 0x8fd948e4, 0x6dbc3128,
            0x58ebf2ef, 0x34c6ffea, 0xfe28ed61, 0xee7c3c73,
            0x5d4a14d9, 0xe864b7e3, 0x42105d14, 0x203e13e0,
            0x45eee2b6, 0xa3aaabea, 0xdb6c4f15, 0xfacb4fd0,
            0xc742f442, 0xef6abbb5, 0x654f3b1d, 0x41cd2105,
            0xd81e799e, 0x86854dc7, 0xe44b476a, 0x3d816250,
            0xcf62a1f2, 0x5b8d2646, 0xfc8883a0, 0xc1c7b6a3,
            0x7f1524c3, 0x69cb7492, 0x47848a0b, 0x5692b285,
            0x095bbf00, 0xad19489d, 0x1462b174, 0x23820e00,
            0x58428d2a, 0x0c55f5ea, 0x1dadf43e, 0x233f7061,
            0x3372f092, 0x8d937e41, 0xd65fecf1, 0x6c223bdb,
            0x7cde3759, 0xcbee7460, 0x4085f2a7, 0xce77326e,
            0xa6078084, 0x19f8509e, 0xe8efd855, 0x61d99735,
            0xa969a7aa, 0xc50c06c2, 0x5a04abfc, 0x800bcadc,
            0x9e447a2e, 0xc3453484, 0xfdd56705, 0x0e1e9ec9,
            0xdb73dbd3, 0x105588cd, 0x675fda79, 0xe3674340,
            0xc5c43465, 0x713e38d8, 0x3d28f89e, 0xf16dff20,
            0x153e21e7, 0x8fb03d4a, 0xe6e39f2b, 0xdb83adf7,
        },
        .{
            0xe93d5a68, 0x948140f7, 0xf64c261c, 0x94692934,
            0x411520f7, 0x7602d4f7, 0xbcf46b2e, 0xd4a20068,
            0xd4082471, 0x3320f46a, 0x43b7d4b7, 0x500061af,
            0x1e39f62e, 0x97244546, 0x14214f74, 0xbf8b8840,
            0x4d95fc1d, 0x96b591af, 0x70f4ddd3, 0x66a02f45,
            0xbfbc09ec, 0x03bd9785, 0x7fac6dd0, 0x31cb8504,
            0x96eb27b3, 0x55fd3941, 0xda2547e6, 0xabca0a9a,
            0x28507825, 0x530429f4, 0x0a2c86da, 0xe9b66dfb,
            0x68dc1462, 0xd7486900, 0x680ec0a4, 0x27a18dee,
            0x4f3ffea2, 0xe887ad8c, 0xb58ce006, 0x7af4d6b6,
            0xaace1e7c, 0xd3375fec, 0xce78a399, 0x406b2a42,
            0x20fe9e35, 0xd9f385b9, 0xee39d7ab, 0x3b124e8b,
            0x1dc9faf7, 0x4b6d1856, 0x26a36631, 0xeae397b2,
            0x3a6efa74, 0xdd5b4332, 0x6841e7f7, 0xca7820fb,
            0xfb0af54e, 0xd8feb397, 0x454056ac, 0xba489527,
            0x55533a3a, 0x20838d87, 0xfe6ba9b7, 0xd096954b,
            0x55a867bc, 0xa1159a58, 0xcca92963, 0x99e1db33,
            0xa62a4a56, 0x3f3125f9, 0x5ef47e1c, 0x9029317c,
            0xfdf8e802, 0x04272f70, 0x80bb155c, 0x05282ce3,
            0x95c11548, 0xe4c66d22, 0x48c1133f, 0xc70f86dc,
            0x07f9c9ee, 0x41041f0f, 0x404779a4, 0x5d886e17,
            0x325f51eb, 0xd59bc0d1, 0xf2bcc18f, 0x41113564,
            0x257b7834, 0x602a9c60, 0xdff8e8a3, 0x1f636c1b,
            0x0e12b4c2, 0x02e1329e, 0xaf664fd1, 0xcad18115,
            0x6b2395e0, 0x333e92e1, 0x3b240b62, 0xeebeb922,
            0x85b2a20e, 0xe6ba0d99, 0xde720c8c, 0x2da2f728,
            0xd0127845, 0x95b794fd, 0x647d0862, 0xe7ccf5f0,
            0x5449a36f, 0x877d48fa, 0xc39dfd27, 0xf33e8d1e,
            0x0a476341, 0x992eff74, 0x3a6f6eab, 0xf4f8fd37,
            0xa812dc60, 0xa1ebddf8, 0x991be14c, 0xdb6e6b0d,
            0xc67b5510, 0x6d672c37, 0x2765d43b, 0xdcd0e804,
            0xf1290dc7, 0xcc00ffa3, 0xb5390f92, 0x690fed0b,
            0x667b9ffb, 0xcedb7d9c, 0xa091cf0b, 0xd9155ea3,
            0xbb132f88, 0x515bad24, 0x7b9479bf, 0x763bd6eb,
            0x37392eb3, 0xcc115979, 0x8026e297, 0xf42e312d,
            0x6842ada7, 0xc66a2b3b, 0x12754ccc, 0x782ef11c,
            0x6a124237, 0xb79251e7, 0x06a1bbe6, 0x4bfb6350,
            0x1a6b1018, 0x11caedfa, 0x3d25bdd8, 0xe2e1c3c9,
            0x44421659, 0x0a121386, 0xd90cec6e, 0xd5abea2a,
            0x64af674e, 0xda86a85f, 0xbebfe988, 0x64e4c3fe,
            0x9dbc8057, 0xf0f7c086, 0x60787bf8, 0x6003604d,
            0xd1fd8346, 0xf6381fb0, 0x7745ae04, 0xd736fccc,
            0x83426b33, 0xf01eab71, 0xb0804187, 0x3c005e5f,
            0x77a057be, 0xbde8ae24, 0x55464299, 0xbf582e61,
            0x4e58f48f, 0xf2ddfda2, 0xf474ef38, 0x8789bdc2,
            0x5366f9c3, 0xc8b38e74, 0xb475f255, 0x46fcd9b9,
            0x7aeb2661, 0x8b1ddf84, 0x846a0e79, 0x915f95e2,
            0x466e598e, 0x20b45770, 0x8cd55591, 0xc902de4c,
            0xb90bace1, 0xbb8205d0, 0x11a86248, 0x7574a99e,
            0xb77f19b6, 0xe0a9dc09, 0x662d09a1, 0xc4324633,
            0xe85a1f02, 0x09f0be8c, 0x4a99a025, 0x1d6efe10,
            0x1ab93d1d, 0x0ba5a4df, 0xa186f20f, 0x2868f169,
            0xdcb7da83, 0x573906fe, 0xa1e2ce9b, 0x4fcd7f52,
            0x50115e01, 0xa70683fa, 0xa002b5c4, 0x0de6d027,
            0x9af88c27, 0x773f8641, 0xc3604c06, 0x61a806b5,
            0xf0177a28, 0xc0f586e0, 0x006058aa, 0x30dc7d62,
            0x11e69ed7, 0x2338ea63, 0x53c2dd94, 0xc2c21634,
            0xbbcbee56, 0x90bcb6de, 0xebfc7da1, 0xce591d76,
            0x6f05e409, 0x4b7c0188, 0x39720a3d, 0x7c927c24,
            0x86e3725f, 0x724d9db9, 0x1ac15bb4, 0xd39eb8fc,
            0xed545578, 0x08fca5b5, 0xd83d7cd3, 0x4dad0fc4,
            0x1e50ef5e, 0xb161e6f8, 0xa28514d9, 0x6c51133c,
            0x6fd5c7e7, 0x56e14ec4, 0x362abfce, 0xddc6c837,
            0xd79a3234, 0x92638212, 0x670efa8e, 0x406000e0,
        },
     
```
