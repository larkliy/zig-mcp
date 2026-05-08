```
             crypto.hash.sha2.Sha384 => .{
                    0x30, 0x41, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
                    0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x02, 0x05,
                    0x00, 0x04, 0x30,
                },
                crypto.hash.sha2.Sha512 => .{
                    0x30, 0x51, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
                    0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x03, 0x05,
                    0x00, 0x04, 0x40,
                },
                else => @compileError("unreachable"),
            };
            em_index -= hash_der.len;
            @memcpy(em[em_index..][0..hash_der.len], hash_der);

            // 3. If emLen < tLen + 11, output "intended encoded message length too
            //    short" and stop.

            // 4. Generate an octet string PS consisting of emLen - tLen - 3 octets
            //    with hexadecimal value 0xff.  The length of PS will be at least 8
            //    octets.
            em_index -= 1;
            @memset(em[2..em_index], 0xff);

            // 5. Concatenate PS, the DER encoding T, and other padding to form the
            //    encoded message EM as
            //
            //       EM = 0x00 || 0x01 || PS || 0x00 || T.
            em[em_index] = 0x00;
            em[1] = 0x01;
            em[0] = 0x00;

            // 6. Output EM.
            return em;
        }
    };

    pub const PublicKey = struct {
        n: Modulus,
        e: Fe,

        pub const FromBytesError = error{CertificatePublicKeyInvalid};

        pub fn fromBytes(pub_bytes: []const u8, modulus_bytes: []const u8) FromBytesError!PublicKey {
            // Reject modulus below 512 bits.
            // 512-bit RSA was factored in 1999, so this limit barely means anything,
            // but establish some limit now to ratchet in what we can.
            const _n = Modulus.fromBytes(modulus_bytes, .big) catch return error.CertificatePublicKeyInvalid;
            if (_n.bits() < 512) return error.CertificatePublicKeyInvalid;

            // Exponent must be odd and greater than 2.
            // Also, it must be less than 2^32 to mitigate DoS attacks.
            // Windows CryptoAPI doesn't support values larger than 32 bits [1], so it is
            // unlikely that exponents larger than 32 bits are being used for anything
            // Windows commonly does.
            // [1] https://learn.microsoft.com/en-us/windows/win32/api/wincrypt/ns-wincrypt-rsapubkey
            if (pub_bytes.len > 4) return error.CertificatePublicKeyInvalid;
            const _e = Fe.fromBytes(_n, pub_bytes, .big) catch return error.CertificatePublicKeyInvalid;
            if (!_e.isOdd()) return error.CertificatePublicKeyInvalid;
            const e_v = _e.toPrimitive(u32) catch return error.CertificatePublicKeyInvalid;
            if (e_v < 2) return error.CertificatePublicKeyInvalid;

            return .{
                .n = _n,
                .e = _e,
            };
        }

        pub const ParseDerError = der.Element.ParseError || error{CertificateFieldHasWrongDataType};

        pub fn parseDer(pub_key: []const u8) ParseDerError!struct { modulus: []const u8, exponent: []const u8 } {
            const pub_key_seq = try der.Element.parse(pub_key, 0);
            if (pub_key_seq.identifier.tag != .sequence) return error.CertificateFieldHasWrongDataType;
            const modulus_elem = try der.Element.parse(pub_key, pub_key_seq.slice.start);
            if (modulus_elem.identifier.tag != .integer) return error.CertificateFieldHasWrongDataType;
            const exponent_elem = try der.Element.parse(pub_key, modulus_elem.slice.end);
            if (exponent_elem.identifier.tag != .integer) return error.CertificateFieldHasWrongDataType;
            // Skip over meaningless zeroes in the modulus.
            const modulus_raw = pub_key[modulus_elem.slice.start..modulus_elem.slice.end];
            const modulus_offset = for (modulus_raw, 0..) |byte, i| {
                if (byte != 0) break i;
            } else modulus_raw.len;
            return .{
                .modulus = modulus_raw[modulus_offset..],
                .exponent = pub_key[exponent_elem.slice.start..exponent_elem.slice.end],
            };
        }
    };

    const EncryptError = error{MessageTooLong};

    fn encrypt(comptime modulus_len: usize, msg: [modulus_len]u8, public_key: PublicKey) EncryptError![modulus_len]u8 {
        const m = Fe.fromBytes(public_key.n, &msg, .big) catch return error.MessageTooLong;
        const e = public_key.n.powPublic(m, public_key.e) catch unreachable;
        var res: [modulus_len]u8 = undefined;
        e.toBytes(&res, .big) catch unreachable;
        return res;
    }
};



---
File: /std/crypto/chacha20.zig
---

// Based on public domain Supercop by Daniel J. Bernstein

const std = @import("../std.zig");
const builtin = @import("builtin");
const crypto = std.crypto;
const math = std.math;
const mem = std.mem;
const assert = std.debug.assert;
const testing = std.testing;
const maxInt = math.maxInt;
const Poly1305 = crypto.onetimeauth.Poly1305;
const AuthenticationError = crypto.errors.AuthenticationError;

/// IETF-variant of the ChaCha20 stream cipher, as designed for TLS.
pub const ChaCha20IETF = ChaChaIETF(20);

/// IETF-variant of the ChaCha20 stream cipher, reduced to 12 rounds.
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const ChaCha12IETF = ChaChaIETF(12);

/// IETF-variant of the ChaCha20 stream cipher, reduced to 8 rounds.
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const ChaCha8IETF = ChaChaIETF(8);

/// Original ChaCha20 stream cipher.
pub const ChaCha20With64BitNonce = ChaChaWith64BitNonce(20);

/// Original ChaCha20 stream cipher, reduced to 12 rounds.
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const ChaCha12With64BitNonce = ChaChaWith64BitNonce(12);

/// Original ChaCha20 stream cipher, reduced to 8 rounds.
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const ChaCha8With64BitNonce = ChaChaWith64BitNonce(8);

/// XChaCha20 (nonce-extended version of the IETF ChaCha20 variant) stream cipher
pub const XChaCha20IETF = XChaChaIETF(20);

/// XChaCha20 (nonce-extended version of the IETF ChaCha20 variant) stream cipher, reduced to 12 rounds
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const XChaCha12IETF = XChaChaIETF(12);

/// XChaCha20 (nonce-extended version of the IETF ChaCha20 variant) stream cipher, reduced to 8 rounds
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const XChaCha8IETF = XChaChaIETF(8);

/// ChaCha20-Poly1305 authenticated cipher, as designed for TLS
pub const ChaCha20Poly1305 = ChaChaPoly1305(20);

/// ChaCha20-Poly1305 authenticated cipher, reduced to 12 rounds
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const ChaCha12Poly1305 = ChaChaPoly1305(12);

/// ChaCha20-Poly1305 authenticated cipher, reduced to 8 rounds
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const ChaCha8Poly1305 = ChaChaPoly1305(8);

/// XChaCha20-Poly1305 authenticated cipher
pub const XChaCha20Poly1305 = XChaChaPoly1305(20);

/// XChaCha20-Poly1305 authenticated cipher
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const XChaCha12Poly1305 = XChaChaPoly1305(12);

/// XChaCha20-Poly1305 authenticated cipher
/// Reduced-rounds versions are faster than the full-round version, but have a lower security margin.
/// However, ChaCha is still believed to have a comfortable security even with only 8 rounds.
pub const XChaCha8Poly1305 = XChaChaPoly1305(8);

// Vectorized implementation of the core function
fn ChaChaVecImpl(comptime rounds_nb: usize, comptime degree: comptime_int) type {
    return struct {
        const Lane = @Vector(4 * degree, u32);
        const BlockVec = [4]Lane;

        fn initContext(key: [8]u32, d: [4]u32) BlockVec {
            const c = "expand 32-byte k";
            switch (degree) {
                1 => {
                    const constant_le = Lane{
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                    };
                    return BlockVec{
                        constant_le,
                        Lane{ key[0], key[1], key[2], key[3] },
                        Lane{ key[4], key[5], key[6], key[7] },
                        Lane{ d[0], d[1], d[2], d[3] },
                    };
                },
                2 => {
                    const constant_le = Lane{
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                    };
                    const n1 = @addWithOverflow(d[0], 1);
                    return BlockVec{
                        constant_le,
                        Lane{ key[0], key[1], key[2], key[3], key[0], key[1], key[2], key[3] },
                        Lane{ key[4], key[5], key[6], key[7], key[4], key[5], key[6], key[7] },
                        Lane{ d[0], d[1], d[2], d[3], n1[0], d[1] +% n1[1], d[2], d[3] },
                    };
                },
                4 => {
                    const n1 = @addWithOverflow(d[0], 1);
                    const n2 = @addWithOverflow(d[0], 2);
                    const n3 = @addWithOverflow(d[0], 3);
                    const constant_le = Lane{
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                        mem.readInt(u32, c[0..4], .little),
                        mem.readInt(u32, c[4..8], .little),
                        mem.readInt(u32, c[8..12], .little),
                        mem.readInt(u32, c[12..16], .little),
                    };
                    return BlockVec{
                        constant_le,
                        Lane{ key[0], key[1], key[2], key[3], key[0], key[1], key[2], key[3], key[0], key[1], key[2], key[3], key[0], key[1], key[2], key[3] },
                        Lane{ key[4], key[5], key[6], key[7], key[4], key[5], key[6], key[7], key[4], key[5], key[6], key[7], key[4], key[5], key[6], key[7] },
                        Lane{ d[0], d[1], d[2], d[3], n1[0], d[1] +% n1[1], d[2], d[3], n2[0], d[1] +% n2[1], d[2], d[3], n3[0], d[1] +% n3[1], d[2], d[3] },
                    };
                },
                else => @compileError("invalid degree"),
            }
        }

        fn chacha20Core(x: *BlockVec, input: BlockVec) void {
            x.* = input;

            const m0 = switch (degree) {
                1 => [_]i32{ 3, 0, 1, 2 },
                2 => [_]i32{ 3, 0, 1, 2 } ++ [_]i32{ 7, 4, 5, 6 },
                4 => [_]i32{ 3, 0, 1, 2 } ++ [_]i32{ 7, 4, 5, 6 } ++ [_]i32{ 11, 8, 9, 10 } ++ [_]i32{ 15, 12, 13, 14 },
                else => @compileError("invalid degree"),
            };
            const m1 = switch (degree) {
                1 => [_]i32{ 2, 3, 0, 1 },
                2 => [_]i32{ 2, 3, 0, 1 } ++ [_]i32{ 6, 7, 4, 5 },
                4 => [_]i32{ 2, 3, 0, 1 } ++ [_]i32{ 6, 7, 4, 5 } ++ [_]i32{ 10, 11, 8, 9 } ++ [_]i32{ 14, 15, 12, 13 },
                else => @compileError("invalid degree"),
            };
            const m2 = switch (degree) {
                1 => [_]i32{ 1, 2, 3, 0 },
                2 => [_]i32{ 1, 2, 3, 0 } ++ [_]i32{ 5, 6, 7, 4 },
                4 => [_]i32{ 1, 2, 3, 0 } ++ [_]i32{ 5, 6, 7, 4 } ++ [_]i32{ 9, 10, 11, 8 } ++ [_]i32{ 13, 14, 15, 12 },
                else => @compileError("invalid degree"),
            };

            var r: usize = 0;
            while (r < rounds_nb) : (r += 2) {
                x[0] +%= x[1];
                x[3] ^= x[0];
                x[3] = math.rotl(Lane, x[3], 16);

                x[2] +%= x[3];
                x[1] ^= x[2];
                x[1] = math.rotl(Lane, x[1], 12);

                x[0] +%= x[1];
                x[3] ^= x[0];
                x[0] = @shuffle(u32, x[0], undefined, m0);
                x[3] = math.rotl(Lane, x[3], 8);

                x[2] +%= x[3];
                x[3] = @shuffle(u32, x[3], undefined, m1);
                x[1] ^= x[2];
                x[2] = @shuffle(u32, x[2], undefined, m2);
                x[1] = math.rotl(Lane, x[1], 7);

                x[0] +%= x[1];
                x[3] ^= x[0];
                x[3] = math.rotl(Lane, x[3], 16);

                x[2] +%= x[3];
                x[1] ^= x[2];
                x[1] = math.rotl(Lane, x[1], 12);

                x[0] +%= x[1];
                x[3] ^= x[0];
                x[0] = @shuffle(u32, x[0], undefined, m2);
                x[3] = math.rotl(Lane, x[3], 8);

                x[2] +%= x[3];
                x[3] = @shuffle(u32, x[3], undefined, m1);
                x[1] ^= x[2];
                x[2] = @shuffle(u32, x[2], undefined, m0);
                x[1] = math.rotl(Lane, x[1], 7);
            }
        }

        fn hashToBytes(comptime dm: usize, out: *[64 * dm]u8, x: *const BlockVec) void {
            inline for (0..dm) |d| {
                for (0..4) |i| {
                    mem.writeInt(u32, out[64 * d + 16 * i + 0 ..][0..4], x[i][0 + 4 * d], .little);
                    mem.writeInt(u32, out[64 * d + 16 * i + 4 ..][0..4], x[i][1 + 4 * d], .little);
                    mem.writeInt(u32, out[64 * d + 16 * i + 8 ..][0..4], x[i][2 + 4 * d], .little);
                    mem.writeInt(u32, out[64 * d + 16 * i + 12 ..][0..4], x[i][3 + 4 * d], .little);
                }
            }
        }

        fn contextFeedback(x: *BlockVec, ctx: BlockVec) void {
            x[0] +%= ctx[0];
            x[1] +%= ctx[1];
            x[2] +%= ctx[2];
            x[3] +%= ctx[3];
        }

        fn chacha20Xor(out: []u8, in: []const u8, key: [8]u32, nonce_and_counter: [4]u32, comptime count64: bool) void {
            var ctx = initContext(key, nonce_and_counter);
            var x: BlockVec = undefined;
            var buf: [64 * degree]u8 = undefined;
            var i: usize = 0;
            inline for ([_]comptime_int{ 4, 2, 1 }) |d| {
                while (degree >= d and i + 64 * d <= in.len) : (i += 64 * d) {
                    chacha20Core(x[0..], ctx);
                    contextFeedback(&x, ctx);
                    hashToBytes(d, buf[0 .. 64 * d], &x);

                    var xout = out[i..];
                    const xin = in[i..];
                    for (0..64 * d) |j| {
                        xout[j] = xin[j];
                    }
                    for (0..64 * d) |j| {
                        xout[j] ^= buf[j];
                    }
                    inline for (0..d) |d_| {
                        if (count64) {
                            const next = @addWithOverflow(ctx[3][4 * d_], d);
                            ctx[3][4 * d_] = next[0];
                            ctx[3][4 * d_ + 1] +%= next[1];
                        } else {
                            ctx[3][4 * d_] +%= d;
                        }
                    }
                }
            }
            if (i < in.len) {
                chacha20Core(x[0..], ctx);
                contextFeedback(&x, ctx);
                hashToBytes(1, buf[0..64], &x);

                var xout = out[i..];
                const xin = in[i..];
                for (0..in.len % 64) |j| {
                    xout[j] = xin[j] ^ buf[j];
                }
            }
        }

        fn chacha20Stream(out: []u8, key: [8]u32, nonce_and_counter: [4]u32, comptime count64: bool) void {
            var ctx = initContext(key, nonce_and_counter);
            var x: BlockVec = undefined;
            var i: usize = 0;
            inline for ([_]comptime_int{ 4, 2, 1 }) |d| {
                while (degree >= d and i + 64 * d <= out.len) : (i += 64 * d) {
                    chacha20Core(x[0..], ctx);
                    contextFeedback(&x, ctx);
                    hashToBytes(d, out[i..][0 .. 64 * d], &x);
                    inline for (0..d) |d_| {
                        if (count64) {
                            const next = @addWithOverflow(ctx[3][4 * d_], d);
                            ctx[3][4 * d_] = next[0];
                            ctx[3][4 * d_ + 1] +%= next[1];
                        } else {
                            ctx[3][4 * d_] +%= d;
                        }
                    }
                }
            }
            if (i < out.len) {
                chacha20Core(x[0..], ctx);
                contextFeedback(&x, ctx);

                var buf: [64]u8 = undefined;
                hashToBytes(1, buf[0..], &x);
                @memcpy(out[i..], buf[0 .. out.len - i]);
            }
        }

        fn hchacha20(input: [16]u8, key: [32]u8) [32]u8 {
            var c: [4]u32 = undefined;
            for (c, 0..) |_, i| {
                c[i] = mem.readInt(u32, input[4 * i ..][0..4], .little);
            }
            const ctx = initContext(keyToWords(key), c);
            var x: BlockVec = undefined;
            chacha20Core(x[0..], ctx);
            var out: [32]u8 = undefined;
            mem.writeInt(u32, out[0..4], x[0][0], .little);
            mem.writeInt(u32, out[4..8], x[0][1], .little);
            mem.writeInt(u32, out[8..12], x[0][2], .little);
            mem.writeInt(u32, out[12..16], x[0][3], .little);
            mem.writeInt(u32, out[16..20], x[3][0], .little);
            mem.writeInt(u32, out[20..24], x[3][1], .little);
            mem.writeInt(u32, out[24..28], x[3][2], .little);
            mem.writeInt(u32, out[28..32], x[3][3], .little);
            return out;
        }
    };
}

// Non-vectorized implementation of the core function
fn ChaChaNonVecImpl(comptime rounds_nb: usize) type {
    return struct {
        const BlockVec = [16]u32;

        fn initContext(key: [8]u32, d: [4]u32) BlockVec {
            const c = "expand 32-byte k";
            const constant_le = comptime [4]u32{
                mem.readInt(u32, c[0..4], .little),
                mem.readInt(u32, c[4..8], .little),
                mem.readInt(u32, c[8..12], .little),
                mem.readInt(u32, c[12..16], .little),
            };
            return BlockVec{
                constant_le[0], constant_le[1], constant_le[2], constant_le[3],
                key[0],         key[1],         key[2],         key[3],
                key[4],         key[5],         key[6],         key[7],
                d[0],           d[1],           d[2],           d[3],
            };
        }

        const QuarterRound = struct {
            a: usize,
            b: usize,
            c: usize,
            d: usize,
        };

        fn Rp(a: usize, b: usize, c: usize, d: usize) QuarterRound {
            return QuarterRound{
                .a = a,
                .b = b,
                .c = c,
                .d = d,
            };
        }

        fn chacha20Core(x: *BlockVec, input: BlockVec) void {
            x.* = input;

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

            comptime var j: usize = 0;
            inline while (j < rounds_nb) : (j += 2) {
                inline for (rounds) |r| {
                    x[r.a] +%= x[r.b];
                    x[r.d] = math.rotl(u32, x[r.d] ^ x[r.a], @as(u32, 16));
                    x[r.c] +%= x[r.d];
                    x[r.b] = math.rotl(u32, x[r.b] ^ x[r.c], @as(u32, 12));
                    x[r.a] +%= x[r.b];
                    x[r.d] = math.rotl(u32, x[r.d] ^ x[r.a], @as(u32, 8));
                    x[r.c] +%= x[r.d];
                    x[r.b] = math.rotl(u32, x[r.b] ^ x[r.c], @as(u32, 7));
                }
            }
        }

        fn hashToBytes(out: *[64]u8, x: *const BlockVec) void {
            for (0..4) |i| {
                mem.writeInt(u32, out[16 * i + 0 ..][0..4], x[i * 4 + 0], .little);
                mem.writeInt(u32, out[16 * i + 4 ..][0..4], x[i * 4 + 1], .little);
                mem.writeInt(u32, out[16 * i + 8 ..][0..4], x[i * 4 + 2], .little);
                mem.writeInt(u32, out[16 * i + 12 ..][0..4], x[i * 4 + 3], .little);
            }
        }

        fn contextFeedback(x: *BlockVec, ctx: BlockVec) void {
            for (0..16) |i| {
                x[i] +%= ctx[i];
            }
        }

        fn chacha20Xor(out: []u8, in: []const u8, key: [8]u32, nonce_and_counter: [4]u32, comptime count64: bool) void {
            var ctx = initContext(key, nonce_and_counter);
            var x: BlockVec = undefined;
            var buf: [64]u8 = undefined;
            var i: usize = 0;
            while (i + 64 <= in.len) : (i += 64) {
                chacha20Core(x[0..], ctx);
                contextFeedback(&x, ctx);
                hashToBytes(buf[0..], &x);

                var xout = out[i..];
                const xin = in[i..];
                for (0..64) |j| {
                    xout[j] = xin[j];
                }
                for (0..64) |j| {
                    xout[j] ^= buf[j];
                }
                if (count64) {
                    const next = @addWithOverflow(ctx[12], 1);
                    ctx[12] = next[0];
                    ctx[13] +%= next[1];
                } else {
                    ctx[12] +%= 1;
                }
            }
            if (i < in.len) {
                chacha20Core(x[0..], ctx);
                contextFeedback(&x, ctx);
                hashToBytes(buf[0..], &x);

                var xout = out[i..];
                const xin = in[i..];
                for (0..in.len % 64) |j| {
                    xout[j] = xin[j] ^ buf[j];
                }
            }
        }

        fn chacha20Stream(out: []u8, key: [8]u32, nonce_and_counter: [4]u32, comptime count64: bool) void {
            var ctx = initContext(key, nonce_and_counter);
            var x: BlockVec = undefined;
            var i: usize = 0;
            while (i + 64 <= out.len) : (i += 64) {
                chacha20Core(x[0..], ctx);
                contextFeedback(&x, ctx);
                hashToBytes(out[i..][0..64], &x);
                if (count64) {
                    const next = @addWithOverflow(ctx[12], 1);
                    ctx[12] = next[0];
                    ctx[13] +%= next[1];
                } else {
                    ctx[12] +%= 1;
                }
            }
            if (i < out.len) {
                chacha20Core(x[0..], ctx);
                contextFeedback(&x, ctx);

                var buf: [64]u8 = undefined;
                hashToBytes(buf[0..], &x);
                @memcpy(out[i..], buf[0 .. out.len - i]);
            }
        }

        fn hchacha20(input: [16]u8, key: [32]u8) [32]u8 {
            var c: [4]u32 = undefined;
            for (c, 0..) |_, i| {
                c[i] = mem.readInt(u32, input[4 * i ..][0..4], .little);
            }
            const ctx = initContext(keyToWords(key), c);
            var x: BlockVec = undefined;
            chacha20Core(x[0..], ctx);
            var out: [32]u8 = undefined;
            mem.writeInt(u32, out[0..4], x[0], .little);
            mem.writeInt(u32, out[4..8], x[1], .little);
            mem.writeInt(u32, out[8..12], x[2], .little);
            mem.writeInt(u32, out[12..16], x[3], .little);
            mem.writeInt(u32, out[16..20], x[12], .little);
            mem.writeInt(u32, out[20..24], x[13], .little);
            mem.writeInt(u32, out[24..28], x[14], .little);
            mem.writeInt(u32, out[28..32], x[15], .little);
            return out;
        }
    };
}

fn ChaChaImpl(comptime rounds_nb: usize) type {
    switch (builtin.cpu.arch) {
        .x86_64 => {
            if (builtin.zig_backend != .stage2_x86_64 and builtin.cpu.has(.x86, .avx512f)) return ChaChaVecImpl(rounds_nb, 4);
            if (builtin.cpu.has(.x86, .avx2)) return ChaChaVecImpl(rounds_nb, 2);
            return ChaChaVecImpl(rounds_nb, 1);
        },
        .aarch64 => {
            if (builtin.zig_backend != .stage2_aarch64 and builtin.cpu.has(.aarch64, .neon)) return ChaChaVecImpl(rounds_nb, 4);
            return ChaChaNonVecImpl(rounds_nb);
        },
        else => return ChaChaNonVecImpl(rounds_nb),
    }
}

fn keyToWords(key: [32]u8) [8]u32 {
    var k: [8]u32 = undefined;
    for (0..8) |i| {
        k[i] = mem.readInt(u32, key[i * 4 ..][0..4], .little);
    }
    return k;
}

fn extend(key: [32]u8, nonce: [24]u8, comptime rounds_nb: usize) struct { key: [32]u8, nonce: [12]u8 } {
    var subnonce: [12]u8 = undefined;
    @memset(subnonce[0..4], 0);
    subnonce[4..].* = nonce[16..24].*;
    return .{
        .key = ChaChaImpl(rounds_nb).hchacha20(nonce[0..16].*, key),
        .nonce = subnonce,
    };
}

fn ChaChaIETF(comptime rounds_nb: usize) type {
    return struct {
        /// Nonce length in bytes.
        pub const nonce_length = 12;
        /// Key length in bytes.
        pub const key_length = 32;
        /// Block length in bytes.
        pub const block_length = 64;

        /// Add the output of the ChaCha20 stream cipher to `in` and stores the result into `out`.
        /// WARNING: This function doesn't provide authenticated encryption.
        /// Using the AEAD or one of the `box` versions is usually preferred.
        pub fn xor(out: []u8, in: []const u8, counter: u32, key: [key_length]u8, nonce: [nonce_length]u8) void {
            assert(in.len == out.len);
            assert(in.len <= 64 * (@as(u39, 1 << 32) - counter));

            var d: [4]u32 = undefined;
            d[0] = counter;
            d[1] = mem.readInt(u32, nonce[0..4], .little);
            d[2] = mem.readInt(u32, nonce[4..8], .little);
            d[3] = mem.readInt(u32, nonce[8..12], .little);
            ChaChaImpl(rounds_nb).chacha20Xor(out, in, keyToWords(key), d, false);
        }

        /// Write the output of the ChaCha20 stream cipher into `out`.
        pub fn stream(out: []u8, counter: u32, key: [key_length]u8, nonce: [nonce_length]u8) void {
            assert(out.len <= 64 * (@as(u39, 1 << 32) - counter));

            var d: [4]u32 = undefined;
            d[0] = counter;
            d[1] = mem.readInt(u32, nonce[0..4], .little);
            d[2] = mem.readInt(u32, nonce[4..8], .little);
            d[3] = mem.readInt(u32, nonce[8..12], .little);
            ChaChaImpl(rounds_nb).chacha20Stream(out, keyToWords(key), d, false);
        }
    };
}

fn ChaChaWith64BitNonce(comptime rounds_nb: usize) type {
    return struct {
        /// Nonce length in bytes.
        pub const nonce_length = 8;
        /// Key length in bytes.
        pub const key_length = 32;
        /// Block length in bytes.
        pub const block_length = 64;

        /// Add the output of the ChaCha20 stream cipher to `in` and stores the result into `out`.
        /// WARNING: This function doesn't provide authenticated encryption.
        /// Using the AEAD or one of the `box` versions is usually preferred.
        pub fn xor(out: []u8, in: []const u8, counter: u64, key: [key_length]u8, nonce: [nonce_length]u8) void {
            assert(in.len == out.len);
            assert(in.len <= 64 * (@as(u71, 1 << 64) - counter));

            const k = keyToWords(key);
            var c: [4]u32 = undefined;
            c[0] = @truncate(counter);
            c[1] = @truncate(counter >> 32);
            c[2] = mem.readInt(u32, nonce[0..4], .little);
            c[3] = mem.readInt(u32, nonce[4..8], .little);
            ChaChaImpl(rounds_nb).chacha20Xor(out, in, k, c, true);
        }

        /// Write the output of the ChaCha20 stream cipher into `out`.
        pub fn stream(out: []u8, counter: u64, key: [key_length]u8, nonce: [nonce_length]u8) void {
            assert(out.len <= 64 * (@as(u71, 1 << 64) - counter));

            const k = keyToWords(key);
            var c: [4]u32 = undefined;
            c[0] = @truncate(counter);
            c[1] = @truncate(counter >> 32);
            c[2] = mem.readInt(u32, nonce[0..4], .little);
            c[3] = mem.readInt(u32, nonce[4..8], .little);
            ChaChaImpl(rounds_nb).chacha20Stream(out, k, c, true);
        }
    };
}

fn XChaChaIETF(comptime rounds_nb: usize) type {
    return struct {
        /// Nonce length in bytes.
        pub const nonce_length = 24;
        /// Key length in bytes.
        pub const key_length = 32;
        /// Block length in bytes.
        pub const block_length = 64;

        /// Add the output of the XChaCha20 stream cipher to `in` and stores the result into `out`.
        /// WARNING: This function doesn't provide authenticated encryption.
        /// Using the AEAD or one of the `box` versions is usually preferred.
        pub fn xor(out: []u8, in: []const u8, counter: u32, key: [key_length]u8, nonce: [nonce_length]u8) void {
            const extended = extend(key, nonce, rounds_nb);
            ChaChaIETF(rounds_nb).xor(out, in, counter, extended.key, extended.nonce);
        }

        /// Write the output of the XChaCha20 stream cipher into `out`.
        pub fn stream(out: []u8, counter: u32, key: [key_length]u8, nonce: [nonce_length]u8) void {
            const extended = extend(key, nonce, rounds_nb);
            ChaChaIETF(rounds_nb).stream(out, counter, extended.key, extended.nonce);
        }
    };
}

fn ChaChaPoly1305(comptime rounds_nb: usize) type {
    return struct {
        pub const tag_length = 16;
        pub const nonce_length = 12;
        pub const key_length = 32;

        /// c: ciphertext: output buffer should be of size m.len
        /// tag: authentication tag: output MAC
        /// m: message
        /// ad: Associated Data
        /// npub: public nonce
        /// k: private key
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) void {
            assert(c.len == m.len);
            assert(m.len <= 64 * (@as(u39, 1 << 32) - 1));

            var polyKey = [_]u8{0} ** 32;
            ChaChaIETF(rounds_nb).xor(polyKey[0..], polyKey[0..], 0, k, npub);

            ChaChaIETF(rounds_nb).xor(c[0..m.len], m, 1, k, npub);

            var mac = Poly1305.init(polyKey[0..]);
            mac.update(ad);
            if (ad.len % 16 != 0) {
                const zeros = [_]u8{0} ** 16;
                const padding = 16 - (ad.len % 16);
                mac.update(zeros[0..padding]);
            }
            mac.update(c[0..m.len]);
            if (m.len % 16 != 0) {
                const zeros = [_]u8{0} ** 16;
                const padding = 16 - (m.len % 16);
                mac.update(zeros[0..padding]);
            }
            var lens: [16]u8 = undefined;
            mem.writeInt(u64, lens[0..8], ad.len, .little);
            mem.writeInt(u64, lens[8..16], m.len, .little);
            mac.update(lens[0..]);
            mac.final(tag);
        }

        /// `m`: Message
        /// `c`: Ciphertext
        /// `tag`: Authentication tag
        /// `ad`: Associated data
        /// `npub`: Public nonce
        /// `k`: Private key
        /// Asserts `c.len == m.len`.
        ///
        /// Contents of `m` are undefined if an error is returned.
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);

            var polyKey = [_]u8{0} ** 32;
            ChaChaIETF(rounds_nb).xor(polyKey[0..], polyKey[0..], 0, k, npub);

            var mac = Poly1305.init(polyKey[0..]);

            mac.update(ad);
            if (ad.len % 16 != 0) {
                const zeros = [_]u8{0} ** 16;
                const padding = 16 - (ad.len % 16);
                mac.update(zeros[0..padding]);
            }
            mac.update(c);
            if (c.len % 16 != 0) {
                const zeros = [_]u8{0} ** 16;
                const padding = 16 - (c.len % 16);
                mac.update(zeros[0..padding]);
            }
            var lens: [16]u8 = undefined;
            mem.writeInt(u64, lens[0..8], ad.len, .little);
            mem.writeInt(u64, lens[8..16], c.len, .little);
            mac.update(lens[0..]);
            var computed_tag: [16]u8 = undefined;
            mac.final(computed_tag[0..]);

            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
            ChaChaIETF(rounds_nb).xor(m[0..c.len], c, 1, k, npub);
        }
    };
}

fn XChaChaPoly1305(comptime rounds_nb: usize) type {
    return struct {
        pub const tag_length = 16;
        pub const nonce_length = 24;
        pub const key_length = 32;

        /// c: ciphertext: output buffer should be of size m.len
        /// tag: authentication tag: output MAC
        /// m: message
        /// ad: Associated Data
        /// npub: public nonce
        /// k: private key
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) void {
            const extended = extend(k, npub, rounds_nb);
            return ChaChaPoly1305(rounds_nb).encrypt(c, tag, m, ad, extended.nonce, extended.key);
        }

        /// `m`: Message
        /// `c`: Ciphertext
        /// `tag`: Authentication tag
        /// `ad`: Associated data
        /// `npub`: Public nonce
        /// `k`: Private key
        /// Asserts `c.len == m.len`.
        ///
        /// Contents of `m` are undefined if an error is returned.
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) AuthenticationError!void {
            const extended = extend(k, npub, rounds_nb);
            return ChaChaPoly1305(rounds_nb).decrypt(m, c, tag, ad, extended.nonce, extended.key);
        }
    };
}

test "AEAD API" {
    const aeads = [_]type{ ChaCha20Poly1305, XChaCha20Poly1305 };
    const m = "Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.";
    const ad = "Additional data";

    inline for (aeads) |aead| {
        const key = [_]u8{69} ** aead.key_length;
        const nonce = [_]u8{42} ** aead.nonce_length;
        var c: [m.len]u8 = undefined;
        var tag: [aead.tag_length]u8 = undefined;
        var out: [m.len]u8 = undefined;

        aead.encrypt(c[0..], tag[0..], m, ad, nonce, key);
        try aead.decrypt(out[0..], c[0..], tag, ad[0..], nonce, key);
        try testing.expectEqualSlices(u8, out[0..], m);
        c[0] +%= 1;
        try testing.expectError(error.AuthenticationFailed, aead.decrypt(out[0..], c[0..], tag, ad[0..], nonce, key));
    }
}

// https://tools.ietf.org/html/rfc7539#section-2.4.2
test "test vector sunscreen" {
    const expected_result = [_]u8{
        0x6e, 0x2e, 0x35, 0x9a, 0x25, 0x68, 0xf9, 0x80,
        0x41, 0xba, 0x07, 0x28, 0xdd, 0x0d, 0x69, 0x81,
        0xe9, 0x7e, 0x7a, 0xec, 0x1d, 0x43, 0x60, 0xc2,
        0x0a, 0x27, 0xaf, 0xcc, 0xfd, 0x9f, 0xae, 0x0b,
        0xf9, 0x1b, 0x65, 0xc5, 0x52, 0x47, 0x33, 0xab,
        0x8f, 0x59, 0x3d, 0xab, 0xcd, 0x62, 0xb3, 0x57,
        0x16, 0x39, 0xd6, 0x24, 0xe6, 0x51, 0x52, 0xab,
        0x8f, 0x53, 0x0c, 0x35, 0x9f, 0x08, 0x61, 0xd8,
        0x07, 0xca, 0x0d, 0xbf, 0x50, 0x0d, 0x6a, 0x61,
        0x56, 0xa3, 0x8e, 0x08, 0x8a, 0x22, 0xb6, 0x5e,
        0x52, 0xbc, 0x51, 0x4d, 0x16, 0xcc, 0xf8, 0x06,
        0x81, 0x8c, 0xe9, 0x1a, 0xb7, 0x79, 0x37, 0x36,
        0x5a, 0xf9, 0x0b, 0xbf, 0x74, 0xa3, 0x5b, 0xe6,
        0xb4, 0x0b, 0x8e, 0xed, 0xf2, 0x78, 0x5e, 0x42,
        0x87, 0x4d,
    };
    const m = "Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.";
    var result: [114]u8 = undefined;
    const key = [_]u8{
        0,  1,  2,  3,  4,  5,  6,  7,
        8,  9,  10, 11, 12, 13, 14, 15,
        16, 17, 18, 19, 20, 21, 22, 23,
        24, 25, 26, 27, 28, 29, 30, 31,
    };
    const nonce = [_]u8{
        0, 0, 0, 0,
        0, 0, 0, 0x4a,
        0, 0, 0, 0,
    };

    ChaCha20IETF.xor(result[0..], m[0..], 1, key, nonce);
    try testing.expectEqualSlices(u8, &expected_result, &result);

    var m2: [114]u8 = undefined;
    ChaCha20IETF.xor(m2[0..], result[0..], 1, key, nonce);
    try testing.expect(mem.order(u8, m, &m2) == .eq);
}

// https://tools.ietf.org/html/draft-agl-tls-chacha20poly1305-04#section-7
test "test vector 1" {
    const expected_result = [_]u8{
        0x76, 0xb8, 0xe0, 0xad, 0xa0, 0xf1, 0x3d, 0x90,
        0x40, 0x5d, 0x6a, 0xe5, 0x53, 0x86, 0xbd, 0x28,
        0xbd, 0xd2, 0x19, 0xb8, 0xa0, 0x8d, 0xed, 0x1a,
        0xa8, 0x36, 0xef, 0xcc, 0x8b, 0x77, 0x0d, 0xc7,
        0xda, 0x41, 0x59, 0x7c, 0x51, 0x57, 0x48, 0x8d,
        0x77, 0x24, 0xe0, 0x3f, 0xb8, 0xd8, 0x4a, 0x37,
        0x6a, 0x43, 0xb8, 0xf4, 0x15, 0x18, 0xa1, 0x1c,
        0xc3, 0x87, 0xb6, 0x69, 0xb2, 0xee, 0x65, 0x86,
    };
    const m = [_]u8{
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    var result: [64]u8 = undefined;
    const key = [_]u8{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
    };
    const nonce = [_]u8{ 0, 0, 0, 0, 0, 0, 0, 0 };

    ChaCha20With64BitNonce.xor(result[0..], m[0..], 0, key, nonce);
    try testing.expectEqualSlices(u8, &expected_result, &result);
}

test "test vector 2" {
    const expected_result = [_]u8{
        0x45, 0x40, 0xf0, 0x5a, 0x9f, 0x1f, 0xb2, 0x96,
        0xd7, 0x73, 0x6e, 0x7b, 0x20, 0x8e, 0x3c, 0x96,
        0xeb, 0x4f, 0xe1, 0x83, 0x46, 0x88, 0xd2, 0x60,
        0x4f, 0x45, 0x09, 0x52, 0xed, 0x43, 0x2d, 0x41,
        0xbb, 0xe2, 0xa0, 0xb6, 0xea, 0x75, 0x66, 0xd2,
        0xa5, 0xd1, 0xe7, 0xe2, 0x0d, 0x42, 0xaf, 0x2c,
        0x53, 0xd7, 0x92, 0xb1, 0xc4, 0x3f, 0xea, 0x81,
        0x7e, 0x9a, 0xd2, 0x75, 0xae, 0x54, 0x69, 0x63,
    };
    const m = [_]u8{
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    var result: [64]u8 = undefined;
    const key = [_]u8{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 1,
    };
    const nonce = [_]u8{ 0, 0, 0, 0, 0, 0, 0, 0 };

    ChaCha20With64BitNonce.xor(result[0..], m[0..], 0, key, nonce);
    try testing.expectEqualSlices(u8, &expected_result, &result);
}

test "test vector 3" {
    const expected_result = [_]u8{
        0xde, 0x9c, 0xba, 0x7b, 0xf3, 0xd6, 0x9e, 0xf5,
        0xe7, 0x86, 0xdc, 0x63, 0x97, 0x3f, 0x65, 0x3a,
        0x0b, 0x49, 0xe0, 0x15, 0xad, 0xbf, 0xf7, 0x13,
        0x4f, 0xcb, 0x7d, 0xf1, 0x37, 0x82, 0x10, 0x31,
        0xe8, 0x5a, 0x05, 0x02, 0x78, 0xa7, 0x08, 0x45,
        0x27, 0x21, 0x4f, 0x73, 0xef, 0xc7, 0xfa, 0x5b,
        0x52, 0x77, 0x06, 0x2e, 0xb7, 0xa0, 0x43, 0x3e,
        0x44, 0x5f, 0x41, 0xe3,
    };
    const m = [_]u8{
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };
    var result: [60]u8 = undefined;
    const key = [_]u8{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
    };
    const nonce = [_]u8{ 0, 0, 0, 0, 0, 0, 0, 1 };

    ChaCha20With64BitNonce.xor(result[0..], m[0..], 0, key, nonce);
    try testing.expectEqualSlices(u8, &expected_result, &result);
}

test "test vector 4" {
    const expected_result = [_]u8{
        0xef, 0x3f, 0xdf, 0xd6, 0xc6, 0x15, 0x78, 0xfb,
        0xf5, 0xcf, 0x35, 0xbd, 0x3d, 0xd3, 0x3b, 0x80,
        0x09, 0x63, 0x16, 0x34, 0xd2, 0x1e, 0x42, 0xac,
        0x33, 0x96, 0x0b, 0xd1, 0x38, 0xe5, 0x0d, 0x32,
        0x11, 0x1e, 0x4c, 0xaf, 0x23, 0x7e, 0xe5, 0x3c,
        0xa8, 0xad, 0x64, 0x26, 0x19, 0x4a, 0x88, 0x54,
        0x5d, 0xdc, 0x49, 0x7a, 0x0b, 0x46, 0x6e, 0x7d,
        0x6b, 0xbd, 0xb0, 0x04, 0x1b, 0x2f, 0x58, 0x6b,
    };
    const m = [_]u8{
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    var result: [64]u8 = undefined;
    const key = [_]u8{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
    };
    const nonce = [_]u8{ 1, 0, 0, 0, 0, 0, 0, 0 };

    ChaCha20With64BitNonce.xor(result[0..], m[0..], 0, key, nonce);
    try testing.expectEqualSlices(u8, &expected_result, &result);
}

test "test vector 5" {
    const expected_result = [_]u8{
        0xf7, 0x98, 0xa1, 0x89, 0xf1, 0x95, 0xe6, 0x69,
        0x82, 0x10, 0x5f, 0xfb, 0x64, 0x0b, 0xb7, 0x75,
        0x7f, 0x57, 0x9d, 0xa3, 0x16, 0x02, 0xfc, 0x93,
        0xec, 0x01, 0xac, 0x56, 0xf8, 0x5a, 0xc3, 0xc1,
        0x34, 0xa4, 0x54, 0x7b, 0x73, 0x3b, 0x46, 0x41,
        0x30, 0x42, 0xc9, 0x44, 0x00, 0x49, 0x17, 0x69,
        0x05, 0xd3, 0xbe, 0x59, 0xea, 0x1c, 0x53, 0xf1,
        0x59, 0x16, 0x15, 0x5c, 0x2b, 0xe8, 0x24, 0x1a,

        0x38, 0x00, 0x8b, 0x9a, 0x26, 0xbc, 0x35, 0x94,
        0x1e, 0x24, 0x44, 0x17, 0x7c, 0x8a, 0xde, 0x66,
        0x89, 0xde, 0x95, 0x26, 0x49, 0x86, 0xd9, 0x58,
        0x89, 0xfb, 0x60, 0xe8, 0x46, 0x29, 0xc9, 0xbd,
        0x9a, 0x5a, 0xcb, 0x1c, 0xc1, 0x18, 0xbe, 0x56,
        0x3e, 0xb9, 0xb3, 0xa4, 0xa4, 0x72, 0xf8, 0x2e,
        0x09, 0xa7, 0xe7, 0x78, 0x49, 0x2b, 0x56, 0x2e,
        0xf7, 0x13, 0x0e, 0x88, 0xdf, 0xe0, 0x31, 0xc7,

        0x9d, 0xb9, 0xd4, 0xf7, 0xc7, 0xa8, 0x99, 0x15,
        0x1b, 0x9a, 0x47, 0x50, 0x32, 0xb6, 0x3f, 0xc3,
        0x85, 0x24, 0x5f, 0xe0, 0x54, 0xe3, 0xdd, 0x5a,
        0x97, 0xa5, 0xf5, 0x76, 0xfe, 0x06, 0x40, 0x25,
        0xd3, 0xce, 0x04, 0x2c, 0x56, 0x6a, 0xb2, 0xc5,
        0x07, 0xb1, 0x38, 0xdb, 0x85, 0x3e, 0x3d, 0x69,
        0x59, 0x66, 0x09, 0x96, 0x54, 0x6c, 0xc9, 0xc4,
        0xa6, 0xea, 0xfd, 0xc7, 0x77, 0xc0, 0x40, 0xd7,

        0x0e, 0xaf, 0x46, 0xf7, 0x6d, 0xad, 0x39, 0x79,
        0xe5, 0xc5, 0x36, 0x0c, 0x33, 0x17, 0x16, 0x6a,
        0x1c, 0x89, 0x4c, 0x94, 0xa3, 0x71, 0x87, 0x6a,
        0x94, 0xdf, 0x76, 0x28, 0xfe, 0x4e, 0xaa, 0xf2,
        0xcc, 0xb2, 0x7d, 0x5a, 0xaa, 0xe0, 0xad, 0x7a,
        0xd0, 0xf9, 0xd4, 0xb6, 0xad, 0x3b, 0x54, 0x09,
        0x87, 0x46, 0xd4, 0x52, 0x4d, 0x38, 0x40, 0x7a,
        0x6d, 0xeb, 0x3a, 0xb7, 0x8f, 0xab, 0x78, 0xc9,
    };
    const m = [_]u8{
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,

        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };
    var result: [256]u8 = undefined;
    const key = [_]u8{
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f,
    };
    const nonce = [_]u8{
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
    };

    ChaCha20With64BitNonce.xor(result[0..], m[0..], 0, key, nonce);
    try testing.expectEqualSlices(u8, &expected_result, &result);
}

test "seal" {
    {
        const m = "";
        const ad = "";
        const key = [_]u8{
            0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x8d, 0x8e, 0x8f,
            0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9d, 0x9e, 0x9f,
        };
        const nonce = [_]u8{ 0x7, 0x0, 0x0, 0x0, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
        const exp_out = [_]u8{ 0xa0, 0x78, 0x4d, 0x7a, 0x47, 0x16, 0xf3, 0xfe, 0xb4, 0xf6, 0x4e, 0x7f, 0x4b, 0x39, 0xbf, 0x4 };

        var out: [exp_out.len]u8 = undefined;
        ChaCha20Poly1305.encrypt(out[0..m.len], out[m.len..], m, ad, nonce, key);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }
    {
        const m = [_]u8{
            0x4c, 0x61, 0x64, 0x69, 0x65, 0x73, 0x20, 0x61, 0x6e, 0x64, 0x20, 0x47, 0x65, 0x6e, 0x74, 0x6c,
            0x65, 0x6d, 0x65, 0x6e, 0x20, 0x6f, 0x66, 0x20, 0x74, 0x68, 0x65, 0x20, 0x63, 0x6c, 0x61, 0x73,
            0x73, 0x20, 0x6f, 0x66, 0x20, 0x27, 0x39, 0x39, 0x3a, 0x20, 0x49, 0x66, 0x20, 0x49, 0x20, 0x63,
            0x6f, 0x75, 0x6c, 0x64, 0x20, 0x6f, 0x66, 0x66, 0x65, 0x72, 0x20, 0x79, 0x6f, 0x75, 0x20, 0x6f,
            0x6e, 0x6c, 0x79, 0x20, 0x6f, 0x6e, 0x65, 0x20, 0x74, 0x69, 0x70, 0x20, 0x66, 0x6f, 0x72, 0x20,
            0x74, 0x68, 0x65, 0x20, 0x66, 0x75, 0x74, 0x75, 0x72, 0x65, 0x2c, 0x20, 0x73, 0x75, 0x6e, 0x73,
            0x63, 0x72, 0x65, 0x65, 0x6e, 0x20, 0x77, 0x6f, 0x75, 0x6c, 0x64, 0x20, 0x62, 0x65, 0x20, 0x69,
            0x74, 0x2e,
        };
        const ad = [_]u8{ 0x50, 0x51, 0x52, 0x53, 0xc0, 0xc1, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7 };
        const key = [_]u8{
            0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x8d, 0x8e, 0x8f,
            0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9d, 0x9e, 0x9f,
        };
        const nonce = [_]u8{ 0x7, 0x0, 0x0, 0x0, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
        const exp_out = [_]u8{
            0xd3, 0x1a, 0x8d, 0x34, 0x64, 0x8e, 0x60, 0xdb, 0x7b, 0x86, 0xaf, 0xbc, 0x53, 0xef, 0x7e, 0xc2,
            0xa4, 0xad, 0xed, 0x51, 0x29, 0x6e, 0x8,  0xfe, 0xa9, 0xe2, 0xb5, 0xa7, 0x36, 0xee, 0x62, 0xd6,
            0x3d, 0xbe, 0xa4, 0x5e, 0x8c, 0xa9, 0x67, 0x12, 0x82, 0xfa, 0xfb, 0x69, 0xda, 0x92, 0x72, 0x8b,
            0x1a, 0x71, 0xde, 0xa,  0x9e, 0x6,  0xb,  0x29, 0x5,  0xd6, 0xa5, 0xb6, 0x7e, 0xcd, 0x3b, 0x36,
            0x92, 0xdd, 0xbd, 0x7f, 0x2d, 0x77, 0x8b, 0x8c, 0x98, 0x3,  0xae, 0xe3, 0x28, 0x9,  0x1b, 0x58,
            0xfa, 0xb3, 0x24, 0xe4, 0xfa, 0xd6, 0x75, 0x94, 0x55, 0x85, 0x80, 0x8b, 0x48, 0x31, 0xd7, 0xbc,
            0x3f, 0xf4, 0xde, 0xf0, 0x8e, 0x4b, 0x7a, 0x9d, 0xe5, 0x76, 0xd2, 0x65, 0x86, 0xce, 0xc6, 0x4b,
            0x61, 0x16, 0x1a, 0xe1, 0xb,  0x59, 0x4f, 0x9,  0xe2, 0x6a, 0x7e, 0x90, 0x2e, 0xcb, 0xd0, 0x60,
            0x6,  0x91,
        };

        var out: [exp_out.len]u8 = undefined;
        ChaCha20Poly1305.encrypt(out[0..m.len], out[m.len..], m[0..], ad[0..], nonce, key);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }
}

test "open" {
    {
        const c = [_]u8{ 0xa0, 0x78, 0x4d, 0x7a, 0x47, 0x16, 0xf3, 0xfe, 0xb4, 0xf6, 0x4e, 0x7f, 0x4b, 0x39, 0xbf, 0x4 };
        const ad = "";
        const key = [_]u8{
            0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x8d, 0x8e, 0x8f,
            0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9d, 0x9e, 0x9f,
        };
        const nonce = [_]u8{ 0x7, 0x0, 0x0, 0x0, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
        const exp_out = "";

        var out: [exp_out.len]u8 = undefined;
        try ChaCha20Poly1305.decrypt(out[0..], c[0..exp_out.len], c[exp_out.len..].*, ad[0..], nonce, key);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);
    }
    {
        const c = [_]u8{
            0xd3, 0x1a, 0x8d, 0x34, 0x64, 0x8e, 0x60, 0xdb, 0x7b, 0x86, 0xaf, 0xbc, 0x53, 0xef, 0x7e, 0xc2,
            0xa4, 0xad, 0xed, 0x51, 0x29, 0x6e, 0x8,  0xfe, 0xa9, 0xe2, 0xb5, 0xa7, 0x36, 0xee, 0x62, 0xd6,
            0x3d, 0xbe, 0xa4, 0x5e, 0x8c, 0xa9, 0x67, 0x12, 0x82, 0xfa, 0xfb, 0x69, 0xda, 0x92, 0x72, 0x8b,
            0x1a, 0x71, 0xde, 0xa,  0x9e, 0x6,  0xb,  0x29, 0x5,  0xd6, 0xa5, 0xb6, 0x7e, 0xcd, 0x3b, 0x36,
            0x92, 0xdd, 0xbd, 0x7f, 0x2d, 0x77, 0x8b, 0x8c, 0x98, 0x3,  0xae, 0xe3, 0x28, 0x9,  0x1b, 0x58,
            0xfa, 0xb3, 0x24, 0xe4, 0xfa, 0xd6, 0x75, 0x94, 0x55, 0x85, 0x80, 0x8b, 0x48, 0x31, 0xd7, 0xbc,
            0x3f, 0xf4, 0xde, 0xf0, 0x8e, 0x4b, 0x7a, 0x9d, 0xe5, 0x76, 0xd2, 0x65, 0x86, 0xce, 0xc6, 0x4b,
            0x61, 0x16, 0x1a, 0xe1, 0xb,  0x59, 0x4f, 0x9,  0xe2, 0x6a, 0x7e, 0x90, 0x2e, 0xcb, 0xd0, 0x60,
            0x6,  0x91,
        };
        const ad = [_]u8{ 0x50, 0x51, 0x52, 0x53, 0xc0, 0xc1, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7 };
        const key = [_]u8{
            0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x8d, 0x8e, 0x8f,
            0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9d, 0x9e, 0x9f,
        };
        const nonce = [_]u8{ 0x7, 0x0, 0x0, 0x0, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
        const exp_out = [_]u8{
            0x4c, 0x61, 0x64, 0x69, 0x65, 0x73, 0x20, 0x61, 0x6e, 0x64, 0x20, 0x47, 0x65, 0x6e, 0x74, 0x6c,
            0x65, 0x6d, 0x65, 0x6e, 0x20, 0x6f, 0x66, 0x20, 0x74, 0x68, 0x65, 0x20, 0x63, 0x6c, 0x61, 0x73,
            0x73, 0x20, 0x6f, 0x66, 0x20, 0x27, 0x39, 0x39, 0x3a, 0x20, 0x49, 0x66, 0x20, 0x49, 0x20, 0x63,
            0x6f, 0x75, 0x6c, 0x64, 0x20, 0x6f, 0x66, 0x66, 0x65, 0x72, 0x20, 0x79, 0x6f, 0x75, 0x20, 0x6f,
            0x6e, 0x6c, 0x79, 0x20, 0x6f, 0x6e, 0x65, 0x20, 0x74, 0x69, 0x70, 0x20, 0x66, 0x6f, 0x72, 0x20,
            0x74, 0x68, 0x65, 0x20, 0x66, 0x75, 0x74, 0x75, 0x72, 0x65, 0x2c, 0x20, 0x73, 0x75, 0x6e, 0x73,
            0x63, 0x72, 0x65, 0x65, 0x6e, 0x20, 0x77, 0x6f, 0x75, 0x6c, 0x64, 0x20, 0x62, 0x65, 0x20, 0x69,
            0x74, 0x2e,
        };

        var out: [exp_out.len]u8 = undefined;
        try ChaCha20Poly1305.decrypt(out[0..], c[0..exp_out.len], c[exp_out.len..].*, ad[0..], nonce, key);
        try testing.expectEqualSlices(u8, exp_out[0..], out[0..]);

        // corrupting the ciphertext, data, key, or nonce should cause a failure
        var bad_c = c;
        bad_c[0] ^= 1;
        try testing.expectError(error.AuthenticationFailed, ChaCha20Poly1305.decrypt(out[0..], bad_c[0..out.len], bad_c[out.len..].*, ad[0..], nonce, key));
        var bad_ad = ad;
        bad_ad[0] ^= 1;
        try testing.expectError(error.AuthenticationFailed, ChaCha20Poly1305.decrypt(out[0..], c[0..out.len], c[out.len..].*, bad_ad[0..], nonce, key));
        var bad_key = key;
        bad_key[0] ^= 1;
        try testing.expectError(error.AuthenticationFailed, ChaCha20Poly1305.decrypt(out[0..], c[0..out.len], c[out.len..].*, ad[0..], nonce, bad_key));
        var bad_nonce = nonce;
        bad_nonce[0] ^= 1;
        try testing.expectError(error.AuthenticationFailed, ChaCha20Poly1305.decrypt(out[0..], c[0..out.len], c[out.len..].*, ad[0..], bad_nonce, key));
    }
}

test "xchacha20" {
    const key = [_]u8{69} ** 32;
    const nonce = [_]u8{42} ** 24;
    const m = "Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.";
    {
        var c: [m.len]u8 = undefined;
        XChaCha20IETF.xor(c[0..], m[0..], 0, key, nonce);
        var buf: [2 * c.len]u8 = undefined;
        try testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&c}), "E0A1BCF939654AFDBDC1746EC49832647C19D891F0D1A81FC0C1703B4514BDEA584B512F6908C2C5E9DD18D5CBC1805DE5803FE3B9CA5F193FB8359E91FAB0C3BB40309A292EB1CF49685C65C4A3ADF4F11DB0CD2B6B67FBC174BC2E860E8F769FD3565BBFAD1C845E05A0FED9BE167C240D");
    }
    {
        const ad = "Additional data";
        var c: [m.len + XChaCha20Poly1305.tag_length]u8 = undefined;
        XChaCha20Poly1305.encrypt(c[0..m.len], c[m.len..], m, ad, nonce, key);
        var out: [m.len]u8 = undefined;
        try XChaCha20Poly1305.decrypt(out[0..], c[0..m.len], c[m.len..].*, ad, nonce, key);
        var buf: [2 * c.len]u8 = undefined;
        try testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&c}), "994D2DD32333F48E53650C02C7A2ABB8E018B0836D7175AEC779F52E961780768F815C58F1AA52D211498DB89B9216763F569C9433A6BBFCEFB4D4A49387A4C5207FBB3B5A92B5941294DF30588C6740D39DC16FA1F0E634F7246CF7CDCB978E44347D89381B7A74EB7084F754B90BDE9AAF5A94B8F2A85EFD0B50692AE2D425E234");
        try testing.expectEqualSlices(u8, out[0..], m);
        c[0] +%= 1;
        try testing.expectError(error.AuthenticationFailed, XChaCha20Poly1305.decrypt(out[0..], c[0..m.len], c[m.len..].*, ad, nonce, key));
    }
}



---
File: /std/crypto/cmac.zig
---

const std = @import("std");
const crypto = std.crypto;
const mem = std.mem;

/// CMAC with AES-128 - RFC 4493 https://www.rfc-editor.org/rfc/rfc4493
pub const CmacAes128 = Cmac(crypto.core.aes.Aes128);

/// NIST Special Publication 800-38B - The CMAC Mode for Authentication
/// https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-38b.pdf
pub fn Cmac(comptime BlockCipher: type) type {
    const BlockCipherCtx = @typeInfo(@TypeOf(BlockCipher.initEnc)).@"fn".return_type.?;
    const Block = [BlockCipher.block.block_length]u8;

    return struct {
        const Self = @This();
        pub const key_length = BlockCipher.key_bits / 8;
        pub const block_length = BlockCipher.block.block_length;
        pub const mac_length = block_length;

        cipher_ctx: BlockCipherCtx,
        k1: Block,
        k2: Block,
        buf: Block = [_]u8{0} ** block_length,
        pos: usize = 0,

        pub fn create(out: *[mac_length]u8, msg: []const u8, key: *const [key_length]u8) void {
            var ctx = Self.init(key);
            ctx.update(msg);
            ctx.final(out);
        }

        pub fn init(key: *const [key_length]u8) Self {
            const cipher_ctx = BlockCipher.initEnc(key.*);
            const zeros = [_]u8{0} ** block_length;
            var k1: Block = undefined;
            cipher_ctx.encrypt(&k1, &zeros);
            k1 = double(k1);
            return Self{
                .cipher_ctx = cipher_ctx,
                .k1 = k1,
                .k2 = double(k1),
            };
        }

        pub fn update(self: *Self, msg: []const u8) void {
            const left = block_length - self.pos;
            var m = msg;
            if (m.len > left) {
                for (self.buf[self.pos..], 0..) |*b, i| b.* ^= m[i];
                m = m[left..];
                self.cipher_ctx.encrypt(&self.buf, &self.buf);
                self.pos = 0;
            }
            while (m.len > block_length) {
                for (self.buf[0..block_length], 0..) |*b, i| b.* ^= m[i];
                m = m[block_length..];
                self.cipher_ctx.encrypt(&self.buf, &self.buf);
                self.pos = 0;
            }
            if (m.len > 0) {
                for (self.buf[self.pos..][0..m.len], 0..) |*b, i| b.* ^= m[i];
                self.pos += m.len;
            }
        }

        pub fn final(self: *Self, out: *[mac_length]u8) void {
            var mac = self.k1;
            if (self.pos < block_length) {
                mac = self.k2;
                mac[self.pos] ^= 0x80;
            }
            for (&mac, 0..) |*b, i| b.* ^= self.buf[i];
            self.cipher_ctx.encrypt(out, &mac);
        }

        fn double(l: Block) Block {
            const Int = std.meta.Int(.unsigned, block_length * 8);
            const l_ = mem.readInt(Int, &l, .big);
            const l_2 = switch (block_length) {
                8 => (l_ << 1) ^ (0x1b & -%(l_ >> 63)), // mod x^64 + x^4 + x^3 + x + 1
                16 => (l_ << 1) ^ (0x87 & -%(l_ >> 127)), // mod x^128 + x^7 + x^2 + x + 1
                32 => (l_ << 1) ^ (0x0425 & -%(l_ >> 255)), // mod x^256 + x^10 + x^5 + x^2 + 1
                64 => (l_ << 1) ^ (0x0125 & -%(l_ >> 511)), // mod x^512 + x^8 + x^5 + x^2 + 1
                else => @compileError("unsupported block length"),
            };
            var l2: Block = undefined;
            mem.writeInt(Int, &l2, l_2, .big);
            return l2;
        }
    };
}

const testing = std.testing;

test "CmacAes128 - Example 1: len = 0" {
    const key = [_]u8{
        0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c,
    };
    var msg: [0]u8 = undefined;
    const exp = [_]u8{
        0xbb, 0x1d, 0x69, 0x29, 0xe9, 0x59, 0x37, 0x28, 0x7f, 0xa3, 0x7d, 0x12, 0x9b, 0x75, 0x67, 0x46,
    };
    var out: [CmacAes128.mac_length]u8 = undefined;
    CmacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &exp);
}

test "CmacAes128 - Example 2: len = 16" {
    const key = [_]u8{
        0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c,
    };
    const msg = [_]u8{
        0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
    };
    const exp = [_]u8{
        0x07, 0x0a, 0x16, 0xb4, 0x6b, 0x4d, 0x41, 0x44, 0xf7, 0x9b, 0xdd, 0x9d, 0xd0, 0x4a, 0x28, 0x7c,
    };
    var out: [CmacAes128.mac_length]u8 = undefined;
    CmacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &exp);
}

test "CmacAes128 - Example 3: len = 40" {
    const key = [_]u8{
        0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c,
    };
    const msg = [_]u8{
        0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
        0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
        0x30, 0xc8, 0x1c, 0x46, 0xa3, 0x5c, 0xe4, 0x11,
    };
    const exp = [_]u8{
        0xdf, 0xa6, 0x67, 0x47, 0xde, 0x9a, 0xe6, 0x30, 0x30, 0xca, 0x32, 0x61, 0x14, 0x97, 0xc8, 0x27,
    };
    var out: [CmacAes128.mac_length]u8 = undefined;
    CmacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &exp);
}

test "CmacAes128 - Example 4: len = 64" {
    const key = [_]u8{
        0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c,
    };
    const msg = [_]u8{
        0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
        0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
        0x30, 0xc8, 0x1c, 0x46, 0xa3, 0x5c, 0xe4, 0x11, 0xe5, 0xfb, 0xc1, 0x19, 0x1a, 0x0a, 0x52, 0xef,
        0xf6, 0x9f, 0x24, 0x45, 0xdf, 0x4f, 0x9b, 0x17, 0xad, 0x2b, 0x41, 0x7b, 0xe6, 0x6c, 0x37, 0x10,
    };
    const exp = [_]u8{
        0x51, 0xf0, 0xbe, 0xbf, 0x7e, 0x3b, 0x9d, 0x92, 0xfc, 0x49, 0x74, 0x17, 0x79, 0x36, 0x3c, 0xfe,
    };
    var out: [CmacAes128.mac_length]u8 = undefined;
    CmacAes128.create(&out, &msg, &key);
    try testing.expectEqualSlices(u8, &out, &exp);
}



---
File: /std/crypto/codecs.zig
---

pub const asn1 = @import("codecs/asn1.zig");
pub const base64 = @import("codecs/base64_hex_ct.zig").base64;
pub const hex = @import("codecs/base64_hex_ct.zig").hex;



---
File: /std/crypto/ecdsa.zig
---

const builtin = @import("builtin");
const std = @import("std");
const crypto = std.crypto;
const fmt = std.fmt;
const mem = std.mem;
const sha3 = crypto.hash.sha3;
const testing = std.testing;

const EncodingError = crypto.errors.EncodingError;
const IdentityElementError = crypto.errors.IdentityElementError;
const NonCanonicalError = crypto.errors.NonCanonicalError;
const SignatureVerificationError = crypto.errors.SignatureVerificationError;

/// ECDSA over P-256 with SHA-256.
pub const EcdsaP256Sha256 = Ecdsa(crypto.ecc.P256, crypto.hash.sha2.Sha256);
/// ECDSA over P-256 with SHA3-256.
pub const EcdsaP256Sha3_256 = Ecdsa(crypto.ecc.P256, crypto.hash.sha3.Sha3_256);
/// ECDSA over P-384 with SHA-384.
pub const EcdsaP384Sha384 = Ecdsa(crypto.ecc.P384, crypto.hash.sha2.Sha384);
/// ECDSA over P-384 with SHA3-384.
pub const EcdsaP384Sha3_384 = Ecdsa(crypto.ecc.P384, crypto.hash.sha3.Sha3_384);
/// ECDSA over Secp256k1 with SHA-256.
pub const EcdsaSecp256k1Sha256 = Ecdsa(crypto.ecc.Secp256k1, crypto.hash.sha2.Sha256);
/// ECDSA over Secp256k1 with SHA-256(SHA-256()) -- The Bitcoin signature system.
pub const EcdsaSecp256k1Sha256oSha256 = Ecdsa(crypto.ecc.Secp256k1, crypto.hash.composition.Sha256oSha256);

/// Elliptic Curve Digital Signature Algorithm (ECDSA).
pub fn Ecdsa(comptime Curve: type, comptime Hash: type) type {
    const Prf = switch (Hash) {
        sha3.Shake128 => sha3.KMac128,
        sha3.Shake256 => sha3.KMac256,
        else => crypto.auth.hmac.Hmac(Hash),
    };

    return struct {
        /// Length (in bytes) of optional random bytes, for non-deterministic signatures.
        pub const noise_length = Curve.scalar.encoded_length;

        /// An ECDSA secret key.
        pub const SecretKey = struct {
            /// Length (in bytes) of a raw secret key.
            pub const encoded_length = Curve.scalar.encoded_length;

            bytes: Curve.scalar.CompressedScalar,

            pub fn fromBytes(bytes: [encoded_length]u8) !SecretKey {
                return SecretKey{ .bytes = bytes };
            }

            pub fn toBytes(sk: SecretKey) [encoded_length]u8 {
                return sk.bytes;
            }
        };

        /// An ECDSA public key.
        pub const PublicKey = struct {
            /// Length (in bytes) of a compressed sec1-encoded key.
            pub const compressed_sec1_encoded_length = 1 + Curve.Fe.encoded_length;
            /// Length (in bytes) of an uncompressed sec1-encoded key.
            pub const uncompressed_sec1_encoded_length = 1 + 2 * Curve.Fe.encoded_length;

            p: Curve,

            /// Create a public key from a SEC-1 representation.
            pub fn fromSec1(sec1: []const u8) !PublicKey {
                return PublicKey{ .p = try Curve.fromSec1(sec1) };
            }

            /// Encode the public key using the compressed SEC-1 format.
            pub fn toCompressedSec1(pk: PublicKey) [compressed_sec1_encoded_length]u8 {
                return pk.p.toCompressedSec1();
            }

            /// Encoding the public key using the uncompressed SEC-1 format.
            pub fn toUncompressedSec1(pk: PublicKey) [uncompressed_sec1_encoded_length]u8 {
                return pk.p.toUncompressedSec1();
            }
        };

        /// An ECDSA signature.
        pub const Signature = struct {
            /// Length (in bytes) of a raw signature.
            pub const encoded_length = Curve.scalar.encoded_length * 2;
            /// Maximum length (in bytes) of a DER-encoded signature.
            pub const der_encoded_length_max = encoded_length + 2 + 2 * 3;

            /// The R component of an ECDSA signature.
            r: Curve.scalar.CompressedScalar,
            /// The S component of an ECDSA signature.
            s: Curve.scalar.CompressedScalar,

            /// Create a Verifier for incremental verification of a signature.
            pub fn verifier(sig: Signature, public_key: PublicKey) Verifier.InitError!Verifier {
                return Verifier.init(sig, public_key);
            }

            pub const VerifyError = Verifier.InitError || Verifier.VerifyError;

            /// Verify the signature against a message and public key.
            /// Return IdentityElement or NonCanonical if the public key or signature are not in the expected range,
            /// or SignatureVerificationError if the signature is invalid for the given message and key.
            pub fn verify(sig: Signature, msg: []const u8, public_key: PublicKey) VerifyError!void {
                var st = try sig.verifier(public_key);
                st.update(msg);
                try st.verify();
            }

            /// Verify the signature against a pre-hashed message and public key.
            /// The message must have already been hashed using the scheme's hash function.
            /// Returns SignatureVerificationError if the signature is invalid for the given message and key.
            pub fn verifyPrehashed(sig: Signature, msg_hash: [Hash.digest_length]u8, public_key: PublicKey) VerifyError!void {
                var st = try sig.verifier(public_key);
                return st.verifyPrehashed(msg_hash);
            }

            /// Return the raw signature (r, s) in big-endian format.
            pub fn toBytes(sig: Signature) [encoded_length]u8 {
                var bytes: [encoded_length]u8 = undefined;
                @memcpy(bytes[0 .. encoded_length / 2], &sig.r);
                @memcpy(bytes[encoded_length / 2 ..], &sig.s);
                return bytes;
            }

            /// Create a signature from a raw encoding of (r, s).
            /// ECDSA always assumes big-endian.
            pub fn fromBytes(bytes: [encoded_length]u8) Signature {
                return Signature{
                    .r = bytes[0 .. encoded_length / 2].*,
                    .s = bytes[encoded_length / 2 ..].*,
                };
            }

            /// Encode the signature using the DER format.
            /// The maximum length of the DER encoding is der_encoded_length_max.
            /// The function returns a slice, that can be shorter than der_encoded_length_max.
            pub fn toDer(sig: Signature, buf: *[der_encoded_length_max]u8) []u8 {
                var w: std.Io.Writer = .fixed(buf);
                const sig_r = mem.trimStart(u8, &sig.r, &.{0});
                const sig_s = mem.trimStart(u8, &sig.s, &.{0});
                const r_len = @as(u8, @intCast(sig_r.len + (sig_r[0] >> 7)));
                const s_len = @as(u8, @intCast(sig_s.len + (sig_s[0] >> 7)));
                const seq_len = @as(u8, @intCast(2 + r_len + 2 + s_len));
                w.writeAll(&[_]u8{ 0x30, seq_len }) catch unreachable;
                w.writeAll(&[_]u8{ 0x02, r_len }) catch unreachable;
                if (sig_r[0] >> 7 != 0) {
                    w.writeByte(0x00) catch unreachable;
                }
                w.writeAll(sig_r) catch unreachable;
                w.writeAll(&[_]u8{ 0x02, s_len }) catch unreachable;
                if (sig_s[0] >> 7 != 0) {
                    w.writeByte(0x00) catch unreachable;
                }
                w.writeAll(sig_s) catch unreachable;
                return w.buffered();
            }

            // Read a DER-encoded integer.
            fn readDerInt(out: []u8, reader: *std.Io.Reader) EncodingError!void {
                const buf = reader.takeArray(2) catch return error.InvalidEncoding;
                if (buf[0] != 0x02) return error.InvalidEncoding;
                var expected_len: usize = buf[1];
                if (expected_len == 0 or expected_len > 1 + out.len) return error.InvalidEncoding;
                var has_top_bit = false;
                if (expected_len == 1 + out.len) {
                    if ((reader.takeByte() catch return error.InvalidEncoding) != 0) return error.InvalidEncoding;
                    expected_len -= 1;
                    has_top_bit = true;
                }
                const out_slice = out[out.len - expected_len ..];
                reader.readSliceAll(out_slice) catch return error.InvalidEncoding;
                if (@intFromBool(has_top_bit) != out[0] >> 7) return error.InvalidEncoding;
            }

            /// Create a signature from a DER representation.
            /// Returns InvalidEncoding if the DER encoding is invalid.
            pub fn fromDer(der: []const u8) EncodingError!Signature {
                var sig: Signature = mem.zeroInit(Signature, .{});
                var reader: std.Io.Reader = .fixed(der);
                const buf = reader.takeArray(2) catch return error.InvalidEncoding;
                if (buf[0] != 0x30 or @as(usize, buf[1]) + 2 != der.len) {
                    return error.InvalidEncoding;
                }
                try readDerInt(&sig.r, &reader);
                try readDerInt(&sig.s, &reader);
                if (reader.seek != der.len) return error.InvalidEncoding;

                return sig;
            }
        };

        /// A Signer is used to incrementally compute a signature.
        /// It can be obtained from a `KeyPair`, using the `signer()` function.
        pub const Signer = struct {
            h: Hash,
            secret_key: SecretKey,
            noise: ?[noise_length]u8,

            fn init(secret_key: SecretKey, noise: ?[noise_length]u8) !Signer {
                return Signer{
                    .h = Hash.init(.{}),
                    .secret_key = secret_key,
                    .noise = noise,
                };
            }

            /// Add new data to the message being signed.
            pub fn update(self: *Signer, data: []const u8) void {
                self.h.update(data);
            }

            /// Compute a signature over a hash.
            fn finalizePrehashed(self: *Signer, msg_hash: [Hash.digest_length]u8) (IdentityElementError || NonCanonicalError)!Signature {
                const scalar_encoded_length = Curve.scalar.encoded_length;
                const h_len = @max(Hash.digest_length, scalar_encoded_length);
                var h: [h_len]u8 = [_]u8{0} ** (h_len - Hash.digest_length) ++ msg_hash;

                std.debug.assert(h.len >= scalar_encoded_length);
                const z = reduceToScalar(scalar_encoded_length, h[0..scalar_encoded_length].*);

                const k = deterministicScalar(msg_hash, self.secret_key.bytes, self.noise);

                const p = try Curve.basePoint.mul(k.toBytes(.big), .big);
                const xs = p.affineCoordinates().x.toBytes(.big);
                const r = reduceToScalar(Curve.Fe.encoded_length, xs);
                if (r.isZero()) return error.IdentityElement;

                const k_inv = k.invert();
                const zrs = z.add(r.mul(try Curve.scalar.Scalar.fromBytes(self.secret_key.bytes, .big)));
                const s = k_inv.mul(zrs);
                if (s.isZero()) return error.IdentityElement;

                return Signature{ .r = r.toBytes(.big), .s = s.toBytes(.big) };
            }

            /// Compute a signature over the entire message.
            pub fn finalize(self: *Signer) (IdentityElementError || NonCanonicalError)!Signature {
                var h_slice: [Hash.digest_length]u8 = undefined;
                self.h.final(&h_slice);
                return self.finalizePrehashed(h_slice);
            }
        };

        /// A Verifier is used to incrementally verify a signature.
        /// It can be obtained from a `Signature`, using the `verifier()` function.
        pub const Verifier = struct {
            h: Hash,
            r: Curve.scalar.Scalar,
            s: Curve.scalar.Scalar,
            public_key: PublicKey,

            pub const InitError = IdentityElementError || NonCanonicalError;

            fn init(sig: Signature, public_key: PublicKey) InitError!Verifier {
                const r = try Curve.scalar.Scalar.fromBytes(sig.r, .big);
                const s = try Curve.scalar.Scalar.fromBytes(sig.s, .big);
                if (r.isZero() or s.isZero()) return error.IdentityElement;

                return Verifier{
                    .h = Hash.init(.{}),
                    .r = r,
                    .s = s,
                    .public_key = public_key,
                };
            }

            /// Add new content to the message to be verified.
            pub fn update(self: *Verifier, data: []const u8) void {
                self.h.update(data);
            }

            pub const VerifyError = IdentityElementError || NonCanonicalError ||
                SignatureVerificationError;

            /// Verify that the signature is valid for the hash.
            fn verifyPrehashed(self: *Verifier, msg_hash: [Hash.digest_length]u8) VerifyError!void {
                const ht = Curve.scalar.encoded_length;
                const h_len = @max(Hash.digest_length, ht);
                var h: [h_len]u8 = [_]u8{0} ** (h_len - Hash.digest_length) ++ msg_hash;

                const z = reduceToScalar(ht, h[0..ht].*);
                if (z.isZero()) {
                    return error.SignatureVerificationFailed;
                }

                const s_inv = self.s.invert();
                const v1 = z.mul(s_inv).toBytes(.little);
                const v2 = self.r.mul(s_inv).toBytes(.little);
                const v1g = try Curve.basePoint.mulPublic(v1, .little);
                const v2pk = try self.public_key.p.mulPublic(v2, .little);
                const vxs = v1g.add(v2pk).affineCoordinates().x.toBytes(.big);
                const vr = reduceToScalar(Curve.Fe.encoded_length, vxs);
                if (!self.r.equivalent(vr)) {
                    return error.SignatureVerificationFailed;
                }
            }

            /// Verify that the signature is valid for the entire message.
            pub fn verify(self: *Verifier) VerifyError!void {
                var h_slice: [Hash.digest_length]u8 = undefined;
                self.h.final(&h_slice);
                return self.verifyPrehashed(h_slice);
            }
        };

        /// An ECDSA key pair.
        pub const KeyPair = struct {
            /// Length (in bytes) of a seed required to create a key pair.
            pub const seed_length = noise_length;

            /// Public part.
            public_key: PublicKey,
            /// Secret scalar.
            secret_key: SecretKey,

            /// Deterministically derive a key pair from a cryptograpically secure secret seed.
            ///
            /// Except in tests, applications should generally call `generate()` instead of this function.
            pub fn generateDeterministic(seed: [seed_length]u8) IdentityElementError!KeyPair {
                const h = [_]u8{0x00} ** Hash.digest_length;
                const k0 = [_]u8{0x01} ** SecretKey.encoded_length;
                const secret_key = deterministicScalar(h, k0, seed).toBytes(.big);
                return fromSecretKey(SecretKey{ .bytes = secret_key });
            }

            /// Generate a new, random key pair.
            pub fn generate(io: std.Io) KeyPair {
                var random_seed: [seed_length]u8 = undefined;
                while (true) {
                    io.random(&random_seed);
                    return generateDeterministic(random_seed) catch {
                        @branchHint(.unlikely);
                        continue;
                    };
                }
            }

            /// Return the public key corresponding to the secret key.
            pub fn fromSecretKey(secret_key: SecretKey) IdentityElementError!KeyPair {
                const public_key = try Curve.basePoint.mul(secret_key.bytes, .big);
                return KeyPair{ .secret_key = secret_key, .public_key = PublicKey{ .p = public_key } };
            }

            /// Sign a message using the key pair.
            /// The noise can be null in order to create deterministic signatures.
            /// If deterministic signatures are not required, the noise should be randomly generated instead.
            /// This helps defend against fault attacks.
            pub fn sign(key_pair: KeyPair, msg: []const u8, noise: ?[noise_length]u8) (IdentityElementError || NonCanonicalError)!Signature {
                var st = try key_pair.signer(noise);
                st.update(msg);
                return st.finalize();
            }

            /// Sign a pre-hashed message using the key pair.
            /// The message must have already been hashed using the scheme's hash function.
            /// The noise parameter can be null for deterministic signatures, or random bytes for enhanced security against fault attacks.
            pub fn signPrehashed(key_pair: KeyPair, msg_hash: [Hash.digest_length]u8, noise: ?[noise_length]u8) (IdentityElementError || NonCanonicalError)!Signature {
                var st = try key_pair.signer(noise);
                return st.finalizePrehashed(msg_hash);
            }

            /// Create a Signer, that can be used for incremental signature verification.
            pub fn signer(key_pair: KeyPair, noise: ?[noise_length]u8) !Signer {
                return Signer.init(key_pair.secret_key, noise);
            }
        };

        // Reduce the coordinate of a field element to the scalar field.
        fn reduceToScalar(comptime unreduced_len: usize, s: [unreduced_len]u8) Curve.scalar.Scalar {
            if (unreduced_len >= 48) {
                var xs = [_]u8{0} ** 64;
                @memcpy(xs[xs.len - s.len ..], s[0..]);
                return Curve.scalar.Scalar.fromBytes64(xs, .big);
            }
            var xs = [_]u8{0} ** 48;
            @memcpy(xs[xs.len - s.len ..], s[0..]);
            return Curve.scalar.Scalar.fromBytes48(xs, .big);
        }

        // Create a deterministic scalar according to a secret key and optional noise.
        // This uses the overly conservative scheme from the "Deterministic ECDSA and EdDSA Signatures with Additional Randomness" draft.
        fn deterministicScalar(h: [Hash.digest_length]u8, secret_key: Curve.scalar.CompressedScalar, noise: ?[noise_length]u8) Curve.scalar.Scalar {
            var k = [_]u8{0x00} ** h.len;
            var m = [_]u8{0x00} ** (h.len + 1 + noise_length + secret_key.len + h.len);
            var t = [_]u8{0x00} ** Curve.scalar.encoded_length;
            const m_v = m[0..h.len];
            const m_i = &m[m_v.len];
            const m_z = m[m_v.len + 1 ..][0..noise_length];
            const m_x = m[m_v.len + 1 + noise_length ..][0..secret_key.len];
            const m_h = m[m.len - h.len ..];

            @memset(m_v, 0x01);
            m_i.* = 0x00;
            if (noise) |n| @memcpy(m_z, &n);
            @memcpy(m_x, &secret_key);
            @memcpy(m_h, &h);
            Prf.create(&k, &m, &k);
            Prf.create(m_v, m_v, &k);
            m_i.* = 0x01;
            Prf.create(&k, &m, &k);
            Prf.create(m_v, m_v, &k);
            while (true) {
                var t_off: usize = 0;
                while (t_off < t.len) : (t_off += m_v.len) {
                    const t_end = @min(t_off + m_v.len, t.len);
                    Prf.create(m_v, m_v, &k);
                    @memcpy(t[t_off..t_end], m_v[0 .. t_end - t_off]);
                }
                if (Curve.scalar.Scalar.fromBytes(t, .big)) |s| return s else |_| {}
                m_i.* = 0x00;
                Prf.create(&k, m[0 .. m_v.len + 1], &k);
                Prf.create(m_v, m_v, &k);
            }
        }
    };
}

test "Basic operations over EcdsaP384Sha384" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    const io = testing.io;
    const Scheme = EcdsaP384Sha384;
    const kp = Scheme.KeyPair.generate(io);
    const msg = "test";

    var noise: [Scheme.noise_length]u8 = undefined;
    io.random(&noise);
    const sig = try kp.sign(msg, noise);
    try sig.verify(msg, kp.public_key);

    const sig2 = try kp.sign(msg, null);
    try sig2.verify(msg, kp.public_key);
}

test "Basic operations over Secp256k1" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    const io = testing.io;
    const Scheme = EcdsaSecp256k1Sha256oSha256;
    const kp = Scheme.KeyPair.generate(io);
    const msg = "test";

    var noise: [Scheme.noise_length]u8 = undefined;
    io.random(&noise);
    const sig = try kp.sign(msg, noise);
    try sig.verify(msg, kp.public_key);

    const sig2 = try kp.sign(msg, null);
    try sig2.verify(msg, kp.public_key);
}

test "Basic operations over EcdsaP384Sha256" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    const io = testing.io;
    const Scheme = Ecdsa(crypto.ecc.P384, crypto.hash.sha2.Sha256);
    const kp = Scheme.KeyPair.generate(io);
    const msg = "test";

    var noise: [Scheme.noise_length]u8 = undefined;
    io.random(&noise);
    const sig = try kp.sign(msg, noise);
    try sig.verify(msg, kp.public_key);

    const sig2 = try kp.sign(msg, null);
    try sig2.verify(msg, kp.public_key);
}

test "Verifying a existing signature with EcdsaP384Sha256" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    const Scheme = Ecdsa(crypto.ecc.P384, crypto.hash.sha2.Sha256);
    // zig fmt: off
    const sk_bytes = [_]u8{
    0x6a, 0x53, 0x9c, 0x83, 0x0f, 0x06, 0x86, 0xd9, 0xef, 0xf1, 0xe7, 0x5c, 0xae,
    0x93, 0xd9, 0x5b, 0x16, 0x1e, 0x96, 0x7c, 0xb0, 0x86, 0x35, 0xc9, 0xea, 0x20,
    0xdc, 0x2b, 0x02, 0x37, 0x6d, 0xd2, 0x89, 0x72, 0x0a, 0x37, 0xf6, 0x5d, 0x4f,
    0x4d, 0xf7, 0x97, 0xcb, 0x8b, 0x03, 0x63, 0xc3, 0x2d
    };
    const msg = [_]u8{
    0x64, 0x61, 0x74, 0x61, 0x20, 0x66, 0x6f, 0x72, 0x20, 0x73, 0x69, 0x67, 0x6e,
    0x69, 0x6e, 0x67, 0x0a
    };
    const sig_ans_bytes = [_]u8{
    0x30, 0x64, 0x02, 0x30, 0x7a, 0x31, 0xd8, 0xe0, 0xf8, 0x40, 0x7d, 0x6a, 0xf3,
    0x1a, 0x5d, 0x02, 0xe5, 0xcb, 0x24, 0x29, 0x1a, 0xac, 0x15, 0x94, 0xd1, 0x5b,
    0xcd, 0x75, 0x2f, 0x45, 0x79, 0x98, 0xf7, 0x60, 0x9a, 0xd5, 0xca, 0x80, 0x15,
    0x87, 0x9b, 0x0c, 0x27, 0xe3, 0x01, 0x8b, 0x73, 0x4e, 0x57, 0xa3, 0xd2, 0x9a,
    0x02, 0x30, 0x33, 0xe0, 0x04, 0x5e, 0x76, 0x1f, 0xc8, 0xcf, 0xda, 0xbe, 0x64,
    0x95, 0x0a, 0xd4, 0x85, 0x34, 0x33, 0x08, 0x7a, 0x81, 0xf2, 0xf6, 0xb6, 0x94,
    0x68, 0xc3, 0x8c, 0x5f, 0x88, 0x92, 0x27, 0x5e, 0x4e, 0x84, 0x96, 0x48, 0x42,
    0x84, 0x28, 0xac, 0x37, 0x93, 0x07, 0xd3, 0x50, 0x32, 0x71, 0xb0
    };
    // zig fmt: on

    const sk = try Scheme.SecretKey.fromBytes(sk_bytes);
    const kp = try Scheme.KeyPair.fromSecretKey(sk);

    const sig_ans = try Scheme.Signature.fromDer(&sig_ans_bytes);
    try sig_ans.verify(&msg, kp.public_key);

    const sig = try kp.sign(&msg, null);
    try sig.verify(&msg, kp.public_key);
}

test "Prehashed message operations" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    const io = testing.io;

    const Scheme = EcdsaP256Sha256;
    const kp = Scheme.KeyPair.generate(io);
    const msg = "test message for prehashed signing";

    const Hash = crypto.hash.sha2.Sha256;
    var msg_hash: [Hash.digest_length]u8 = undefined;
    Hash.hash(msg, &msg_hash, .{});

    const sig = try kp.signPrehashed(msg_hash, null);
    try sig.verifyPrehashed(msg_hash, kp.public_key);

    var bad_hash = msg_hash;
    bad_hash[0] ^= 1;
    try testing.expectError(error.SignatureVerificationFailed, sig.verifyPrehashed(bad_hash, kp.public_key));

    var noise: [Scheme.noise_length]u8 = undefined;
    io.random(&noise);
    const sig_with_noise = try kp.signPrehashed(msg_hash, noise);
    try sig_with_noise.verifyPrehashed(msg_hash, kp.public_key);

    const regular_sig = try kp.sign(msg, null);
    try regular_sig.verifyPrehashed(msg_hash, kp.public_key);
    try sig.verify(msg, kp.public_key);
}

const TestVector = struct {
    key: []const u8,
    msg: []const u8,
    sig: []const u8,
};

test "Test vectors from Project Wycheproof - EcdsaP256Sha256 valid" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    const vectors: []const TestVector = &.{
        // well-formed DER encoding -> expected valid
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "313233343030", .sig = "304402202ba3a8be6b94d5ec80a6d9d1190a436effe50d85a1eee859b8cc6af9bd5c2e1802204cd60b855d442f5b3c7b11eb6c4e0ae7525fe710fab9aa7c77a67f79e6fadd76" },
        // canonical sign-bit padding on S -> expected valid
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "313233343030", .sig = "304502202ba3a8be6b94d5ec80a6d9d1190a436effe50d85a1eee859b8cc6af9bd5c2e18022100b329f479a2bbd0a5c384ee1493b1f5186a87139cac5df4087c134b49156847db" },
        // canonical sign-bit padding on R -> expected valid
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "37313338363834383931", .sig = "30450221009cc98be2347d469bf476dfc26b9b733df2d26d6ef524af917c665baccb23c8820220093496459effe2d8d70727b82462f61d0ec1b7847929d10ea631dacb16b56c32" },
        // canonical sign-bit padding on R; canonical sign-bit padding on S -> expected valid
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "33393439343031323135", .sig = "3046022100bfab3098252847b328fadf2f89b95c851a7f0eb390763378f37e90119d5ba3dd022100bdd64e234e832b1067c2d058ccb44d978195ccebb65c2aaf1e2da9b8b4987e3b" },
        // minimal R length; canonical sign-bit padding on S -> expected valid
        .{ .key = "040ad99500288d466940031d72a9f5445a4d43784640855bf0a69874d2de5fe103c5011e6ef2c42dcd50d5d3d29f99ae6eba2c80c9244f4c5422f0979ff0c3ba5e", .msg = "313233343030", .sig = "303502104319055358e8617b0c46353d039cdaab022100ffffffff00000000ffffffffffffffffbce6faada7179e84f3b9cac2fc63254e" },
        // minimal R length; minimal S length -> expected valid
        .{ .key = "04a71af64de5126a4a4e02b7922d66ce9415ce88a4c9d25514d91082c8725ac9575d47723c8fbe580bb369fec9c2665d8e30a435b9932645482e7c9f11e872296b", .msg = "313233343030", .sig = "3006020105020101" },
        // minimal S length -> expected valid
        .{ .key = "048aeb368a7027a4d64abdea37390c0c1d6a26f399e2d9734de1eb3d0e1937387405bd13834715e1dbae9b875cf07bd55e1b6691c7f7536aef3b19bf7a4adf576d", .msg = "313233343030", .sig = "30250220555555550000000055555555555555553ef7a8e48d07df81a693439654210c70020101" },
    };

    const additional_vectors: []const TestVector = &.{
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "3639383139", .sig = "3044022064a1aab5000d0e804f3e2fc02bdee9be8ff312334e2ba16d11547c97711c898e02206af015971cc30be6d1a206d4e013e0997772a2f91d73286ffd683b9bb2cf4f1b" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "343236343739373234", .sig = "3044022016aea964a2f6506d6f78c81c91fc7e8bded7d397738448de1e19a0ec580bf2660220252cd762130c6667cfe8b7bc47d27d78391e8e80c578d1cd38c3ff033be928e9" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "3130333539333331363638", .sig = "3044022073b3c90ecd390028058164524dde892703dce3dea0d53fa8093999f07ab8aa4302202f67b0b8e20636695bb7d8bf0a651c802ed25a395387b5f4188c0c4075c88634" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31333434323933303739", .sig = "30440220204a9784074b246d8bf8bf04a4ceb1c1f1c9aaab168b1596d17093c5cd21d2cd022051cce41670636783dc06a759c8847868a406c2506fe17975582fe648d1d88b52" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "33373036323131373132", .sig = "3046022100ed66dc34f551ac82f63d4aa4f81fe2cb0031a91d1314f835027bca0f1ceeaa0302210099ca123aa09b13cd194a422e18d5fda167623c3f6e5d4d6abb8953d67c0c48c7" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "333433363838373132", .sig = "30450220060b700bef665c68899d44f2356a578d126b062023ccc3c056bf0f60a237012b0221008d186c027832965f4fcc78a3366ca95dedbb410cbef3f26d6be5d581c11d3610" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31333531353330333730", .sig = "30460221009f6adfe8d5eb5b2c24d7aa7934b6cf29c93ea76cd313c9132bb0c8e38c96831d022100b26a9c9e40e55ee0890c944cf271756c906a33e66b5bd15e051593883b5e9902" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "36353533323033313236", .sig = "3045022100a1af03ca91677b673ad2f33615e56174a1abf6da168cebfa8868f4ba273f16b7022020aa73ffe48afa6435cd258b173d0c2377d69022e7d098d75caf24c8c5e06b1c" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31353634333436363033", .sig = "3045022100fdc70602766f8eed11a6c99a71c973d5659355507b843da6e327a28c11893db902203df5349688a085b137b1eacf456a9e9e0f6d15ec0078ca60a7f83f2b10d21350" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "34343239353339313137", .sig = "3046022100b516a314f2fce530d6537f6a6c49966c23456f63c643cf8e0dc738f7b876e675022100d39ffd033c92b6d717dd536fbc5efdf1967c4bd80954479ba66b0120cd16fff2" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "3130393533323631333531", .sig = "304402203b2cbf046eac45842ecb7984d475831582717bebb6492fd0a485c101e29ff0a802204c9b7b47a98b0f82de512bc9313aaf51701099cac5f76e68c8595fc1c1d99258" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "35393837333530303431", .sig = "3044022030c87d35e636f540841f14af54e2f9edd79d0312cfa1ab656c3fb15bfde48dcf022047c15a5a82d24b75c85a692bd6ecafeb71409ede23efd08e0db9abf6340677ed" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "33343633303036383738", .sig = "3044022038686ff0fda2cef6bc43b58cfe6647b9e2e8176d168dec3c68ff262113760f520220067ec3b651f422669601662167fa8717e976e2db5e6a4cf7c2ddabb3fde9d67d" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "39383137333230323837", .sig = "3044022044a3e23bf314f2b344fc25c7f2de8b6af3e17d27f5ee844b225985ab6e2775cf02202d48e223205e98041ddc87be532abed584f0411f5729500493c9cc3f4dd15e86" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "33323232303431303436", .sig = "304402202ded5b7ec8e90e7bf11f967a3d95110c41b99db3b5aa8d330eb9d638781688e902207d5792c53628155e1bfc46fb1a67e3088de049c328ae1f44ec69238a009808f9" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "36363636333037313034", .sig = "3046022100bdae7bcb580bf335efd3bc3d31870f923eaccafcd40ec2f605976f15137d8b8f022100f6dfa12f19e525270b0106eecfe257499f373a4fb318994f24838122ce7ec3c7" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31303335393531383938", .sig = "3045022050f9c4f0cd6940e162720957ffff513799209b78596956d21ece251c2401f1c6022100d7033a0a787d338e889defaaabb106b95a4355e411a59c32aa5167dfab244726" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31383436353937313935", .sig = "3045022100f612820687604fa01906066a378d67540982e29575d019aabe90924ead5c860d02203f9367702dd7dd4f75ea98afd20e328a1a99f4857b316525328230ce294b0fef" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "33313336303436313839", .sig = "30460221009505e407657d6e8bc93db5da7aa6f5081f61980c1949f56b0f2f507da5782a7a022100c60d31904e3669738ffbeccab6c3656c08e0ed5cb92b3cfa5e7f71784f9c5021" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "32363633373834323534", .sig = "3046022100bbd16fbbb656b6d0d83e6a7787cd691b08735aed371732723e1c68a40404517d0221009d8e35dba96028b7787d91315be675877d2d097be5e8ee34560e3e7fd25c0f00" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31363532313030353234", .sig = "304402202ec9760122db98fd06ea76848d35a6da442d2ceef7559a30cf57c61e92df327e02207ab271da90859479701fccf86e462ee3393fb6814c27b760c4963625c0a19878" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "35373438303831363936", .sig = "3044022054e76b7683b6650baa6a7fc49b1c51eed9ba9dd463221f7a4f1005a89fe00c5902202ea076886c773eb937ec1cc8374b7915cfd11b1c1ae1166152f2f7806a31c8fd" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "36333433393133343638", .sig = "304402205291deaf24659ffbbce6e3c26f6021097a74abdbb69be4fb10419c0c496c9466022065d6fcf336d27cc7cdb982bb4e4ecef5827f84742f29f10abf83469270a03dc3" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31353431313033353938", .sig = "30450220207a3241812d75d947419dc58efb05e8003b33fc17eb50f9d15166a88479f107022100cdee749f2e492b213ce80b32d0574f62f1c5d70793cf55e382d5caadf7592767" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "3130343738353830313238", .sig = "304502206554e49f82a855204328ac94913bf01bbe84437a355a0a37c0dee3cf81aa7728022100aea00de2507ddaf5c94e1e126980d3df16250a2eaebc8be486effe7f22b4f929" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "3130353336323835353638", .sig = "3046022100a54c5062648339d2bff06f71c88216c26c6e19b4d80a8c602990ac82707efdfc022100e99bbe7fcfafae3e69fd016777517aa01056317f467ad09aff09be73c9731b0d" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "393533393034313035", .sig = "3045022100975bd7157a8d363b309f1f444012b1a1d23096593133e71b4ca8b059cff37eaf02207faa7a28b1c822baa241793f2abc930bd4c69840fe090f2aacc46786bf919622" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "393738383438303339", .sig = "304402205694a6f84b8f875c276afd2ebcfe4d61de9ec90305afb1357b95b3e0da43885e02200dffad9ffd0b757d8051dec02ebdf70d8ee2dc5c7870c0823b6ccc7c679cbaa4" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "33363130363732343432", .sig = "3045022100a0c30e8026fdb2b4b4968a27d16a6d08f7098f1a98d21620d7454ba9790f1ba602205e470453a8a399f15baf463f9deceb53acc5ca64459149688bd2760c65424339" },
        .{ .key = "042927b10512bae3eddcfe467828128bad2903269919f7086069c8c4df6c732838c7787964eaac00e5921fb1498a60f4606766b3d9685001558d1a974e7341513e", .msg = "31303534323430373035", .sig = "30440220614ea84acf736527dd73602cd4bb4eea1dfebebd5ad8aca52aa0228cf7b99a880220737cc85f5f2d2f60d1b8183f3e
```
