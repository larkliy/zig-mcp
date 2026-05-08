```
= @import("test.zig");
const testing = std.testing;

test "Aegis128L test vector 1" {
    const key: [Aegis128L.key_length]u8 = [_]u8{ 0x10, 0x01 } ++ [_]u8{0x00} ** 14;
    const nonce: [Aegis128L.nonce_length]u8 = [_]u8{ 0x10, 0x00, 0x02 } ++ [_]u8{0x00} ** 13;
    const ad = [8]u8{ 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
    const m = [32]u8{ 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f };
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aegis128L.tag_length]u8 = undefined;

    Aegis128L.encrypt(&c, &tag, &m, &ad, nonce, key);
    try Aegis128L.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    try htest.assertEqual("79d94593d8c2119d7e8fd9b8fc77845c5c077a05b2528b6ac54b563aed8efe84", &c);
    try htest.assertEqual("cc6f3372f6aa1bb82388d695c3962d9a", &tag);

    c[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis128L.decrypt(&m2, &c, tag, &ad, nonce, key));
    c[0] -%= 1;
    tag[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis128L.decrypt(&m2, &c, tag, &ad, nonce, key));
}

test "Aegis128L test vector 2" {
    const key: [Aegis128L.key_length]u8 = [_]u8{0x00} ** 16;
    const nonce: [Aegis128L.nonce_length]u8 = [_]u8{0x00} ** 16;
    const ad = [_]u8{};
    const m = [_]u8{0x00} ** 16;
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aegis128L.tag_length]u8 = undefined;

    Aegis128L.encrypt(&c, &tag, &m, &ad, nonce, key);
    try Aegis128L.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    try htest.assertEqual("41de9000a7b5e40e2d68bb64d99ebb19", &c);
    try htest.assertEqual("f4d997cc9b94227ada4fe4165422b1c8", &tag);
}

test "Aegis128L test vector 3" {
    const key: [Aegis128L.key_length]u8 = [_]u8{0x00} ** 16;
    const nonce: [Aegis128L.nonce_length]u8 = [_]u8{0x00} ** 16;
    const ad = [_]u8{};
    const m = [_]u8{};
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aegis128L.tag_length]u8 = undefined;

    Aegis128L.encrypt(&c, &tag, &m, &ad, nonce, key);
    try Aegis128L.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    try htest.assertEqual("83cc600dc4e3e7e62d4055826174f149", &tag);
}

test "Aegis128X2 test vector 1" {
    const key: [Aegis128X2.key_length]u8 = [_]u8{ 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };
    const nonce: [Aegis128X2.nonce_length]u8 = [_]u8{ 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f };
    var empty = [_]u8{};
    var tag: [Aegis128X2.tag_length]u8 = undefined;
    var tag256: [Aegis128X2_256.tag_length]u8 = undefined;

    Aegis128X2.encrypt(&empty, &tag, &empty, &empty, nonce, key);
    Aegis128X2_256.encrypt(&empty, &tag256, &empty, &empty, nonce, key);
    try htest.assertEqual("63117dc57756e402819a82e13eca8379", &tag);
    try htest.assertEqual("b92c71fdbd358b8a4de70b27631ace90cffd9b9cfba82028412bac41b4f53759", &tag256);
    tag[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis128X2.decrypt(&empty, &empty, tag, &empty, nonce, key));
    tag256[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis128X2_256.decrypt(&empty, &empty, tag256, &empty, nonce, key));
}

test "Aegis256 test vector 1" {
    const key: [Aegis256.key_length]u8 = [_]u8{ 0x10, 0x01 } ++ [_]u8{0x00} ** 30;
    const nonce: [Aegis256.nonce_length]u8 = [_]u8{ 0x10, 0x00, 0x02 } ++ [_]u8{0x00} ** 29;
    const ad = [8]u8{ 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
    const m = [32]u8{ 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f };
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aegis256.tag_length]u8 = undefined;

    Aegis256.encrypt(&c, &tag, &m, &ad, nonce, key);
    try Aegis256.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    try htest.assertEqual("f373079ed84b2709faee373584585d60accd191db310ef5d8b11833df9dec711", &c);
    try htest.assertEqual("8d86f91ee606e9ff26a01b64ccbdd91d", &tag);

    c[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis256.decrypt(&m2, &c, tag, &ad, nonce, key));
    c[0] -%= 1;
    tag[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis256.decrypt(&m2, &c, tag, &ad, nonce, key));
}

test "Aegis256 test vector 2" {
    const key: [Aegis256.key_length]u8 = [_]u8{0x00} ** 32;
    const nonce: [Aegis256.nonce_length]u8 = [_]u8{0x00} ** 32;
    const ad = [_]u8{};
    const m = [_]u8{0x00} ** 16;
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aegis256.tag_length]u8 = undefined;

    Aegis256.encrypt(&c, &tag, &m, &ad, nonce, key);
    try Aegis256.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    try htest.assertEqual("b98f03a947807713d75a4fff9fc277a6", &c);
    try htest.assertEqual("478f3b50dc478ef7d5cf2d0f7cc13180", &tag);
}

test "Aegis256 test vector 3" {
    const key: [Aegis256.key_length]u8 = [_]u8{0x00} ** 32;
    const nonce: [Aegis256.nonce_length]u8 = [_]u8{0x00} ** 32;
    const ad = [_]u8{};
    const m = [_]u8{};
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aegis256.tag_length]u8 = undefined;

    Aegis256.encrypt(&c, &tag, &m, &ad, nonce, key);
    try Aegis256.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    try htest.assertEqual("f7a0878f68bd083e8065354071fc27c3", &tag);
}

test "Aegis256X4 test vector 1" {
    const key: [Aegis256X4.key_length]u8 = [_]u8{ 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f };
    const nonce: [Aegis256X4.nonce_length]u8 = [_]u8{ 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b, 0x2c, 0x2d, 0x2e, 0x2f };
    var empty = [_]u8{};
    var tag: [Aegis256X4.tag_length]u8 = undefined;
    var tag256: [Aegis256X4_256.tag_length]u8 = undefined;

    Aegis256X4.encrypt(&empty, &tag, &empty, &empty, nonce, key);
    Aegis256X4_256.encrypt(&empty, &tag256, &empty, &empty, nonce, key);
    try htest.assertEqual("3b7fee6cee7bf17888ad11ed2397beb4", &tag);
    try htest.assertEqual("6093a1a8aab20ec635dc1ca71745b01b5bec4fc444c9ffbebd710d4a34d20eaf", &tag256);
    tag[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis256X4.decrypt(&empty, &empty, tag, &empty, nonce, key));
    tag256[0] +%= 1;
    try testing.expectError(error.AuthenticationFailed, Aegis256X4_256.decrypt(&empty, &empty, tag256, &empty, nonce, key));
}

test "Aegis MAC" {
    const key = [_]u8{0x00} ** Aegis128LMac.key_length;
    var msg: [64]u8 = undefined;
    for (&msg, 0..) |*m, i| {
        m.* = @as(u8, @truncate(i));
    }
    const st_init = Aegis128LMac.init(&key);
    var st = st_init;
    var tag: [Aegis128LMac.mac_length]u8 = undefined;

    st.update(msg[0..32]);
    st.update(msg[32..]);
    st.final(&tag);
    try htest.assertEqual("f5eb88d90b7d31c9a679eb94ed1374cd14816b19cdb77930d1a5158f8595983b", &tag);

    st = st_init;
    st.update(msg[0..31]);
    st.update(msg[31..]);
    st.final(&tag);
    try htest.assertEqual("f5eb88d90b7d31c9a679eb94ed1374cd14816b19cdb77930d1a5158f8595983b", &tag);

    st = st_init;
    st.update(msg[0..14]);
    st.update(msg[14..30]);
    st.update(msg[30..]);
    st.final(&tag);
    try htest.assertEqual("f5eb88d90b7d31c9a679eb94ed1374cd14816b19cdb77930d1a5158f8595983b", &tag);

    // An update whose size is not a multiple of the block size
    st = st_init;
    st.update(msg[0..33]);
    st.final(&tag);
    try htest.assertEqual("07b3ba5ad9ceee5ef1906e3396f0fa540fbcd2f33833ef97c35bdc2ae9ae0535", &tag);
}

test "AEGISMAC-128* test vectors" {
    const key = [_]u8{ 0x10, 0x01 } ++ [_]u8{0x00} ** (16 - 2);
    const nonce = [_]u8{ 0x10, 0x00, 0x02 } ++ [_]u8{0x00} ** (16 - 3);
    var msg: [35]u8 = undefined;
    for (&msg, 0..) |*byte, i| byte.* = @truncate(i);
    var mac128: [16]u8 = undefined;
    var mac256: [32]u8 = undefined;

    Aegis128LMac.createWithNonce(&mac256, &msg, &key, &nonce);
    Aegis128LMac_128.createWithNonce(&mac128, &msg, &key, &nonce);
    try htest.assertEqual("d3f09b2842ad301687d6902c921d7818", &mac128);
    try htest.assertEqual("9490e7c89d420c9f37417fa625eb38e8cad53c5cbec55285e8499ea48377f2a3", &mac256);

    Aegis128X2Mac.createWithNonce(&mac256, &msg, &key, &nonce);
    Aegis128X2Mac_128.createWithNonce(&mac128, &msg, &key, &nonce);
    try htest.assertEqual("6873ee34e6b5c59143b6d35c5e4f2c6e", &mac128);
    try htest.assertEqual("afcba3fc2d63c8d6c7f2d63f3ec8fbbbaf022e15ac120e78ffa7755abccd959c", &mac256);

    Aegis128X4Mac.createWithNonce(&mac256, &msg, &key, &nonce);
    Aegis128X4Mac_128.createWithNonce(&mac128, &msg, &key, &nonce);
    try htest.assertEqual("c45a98fd9ab8956ce616eb008cfe4e53", &mac128);
    try htest.assertEqual("26fdc76f41b1da7aec7779f6e964beae8904e662f05aca8345ae3befb357412a", &mac256);
}

test "AEGISMAC-256* test vectors" {
    const key = [_]u8{ 0x10, 0x01 } ++ [_]u8{0x00} ** (32 - 2);
    const nonce = [_]u8{ 0x10, 0x00, 0x02 } ++ [_]u8{0x00} ** (32 - 3);
    var msg: [35]u8 = undefined;
    for (&msg, 0..) |*byte, i| byte.* = @truncate(i);
    var mac128: [16]u8 = undefined;
    var mac256: [32]u8 = undefined;

    Aegis256Mac.createWithNonce(&mac256, &msg, &key, &nonce);
    Aegis256Mac_128.createWithNonce(&mac128, &msg, &key, &nonce);
    try htest.assertEqual("c08e20cfc56f27195a46c9cef5c162d4", &mac128);
    try htest.assertEqual("a5c906ede3d69545c11e20afa360b221f936e946ed2dba3d7c75ad6dc2784126", &mac256);

    Aegis256X2Mac.createWithNonce(&mac256, &msg, &key, &nonce);
    Aegis256X2Mac_128.createWithNonce(&mac128, &msg, &key, &nonce);
    try htest.assertEqual("fb319cb6dd728a764606fb14d37f2a5e", &mac128);
    try htest.assertEqual("0844b20ed5147ceae89c7a160263afd4b1382d6b154ecf560ce8a342cb6a8fd1", &mac256);

    Aegis256X4Mac.createWithNonce(&mac256, &msg, &key, &nonce);
    Aegis256X4Mac_128.createWithNonce(&mac128, &msg, &key, &nonce);
    try htest.assertEqual("a51f9bc5beae60cce77f0dbc60761edd", &mac128);
    try htest.assertEqual("b36a16ef07c36d75a91f437502f24f545b8dfa88648ed116943c29fead3bf10c", &mac256);
}



---
File: /std/crypto/aes_ccm.zig
---

//! AES-CCM (Counter with CBC-MAC) authenticated encryption.
//! AES-CCM* extends CCM to support encryption-only mode (tag_len=0).
//!
//! References:
//! - NIST SP 800-38C: https://csrc.nist.gov/publications/detail/sp/800-38c/final
//! - RFC 3610: https://datatracker.ietf.org/doc/html/rfc3610

const std = @import("std");
const assert = std.debug.assert;
const crypto = std.crypto;
const mem = std.mem;
const modes = crypto.core.modes;
const AuthenticationError = crypto.errors.AuthenticationError;
const cbc_mac = @import("cbc_mac.zig");

/// AES-128-CCM* with no authentication (encryption-only, 13-byte nonce).
pub const Aes128Ccm0 = AesCcm(crypto.core.aes.Aes128, 0, 13);
/// AES-128-CCM with 8-byte authentication tag and 13-byte nonce.
pub const Aes128Ccm8 = AesCcm(crypto.core.aes.Aes128, 8, 13);
/// AES-128-CCM with 16-byte authentication tag and 13-byte nonce.
pub const Aes128Ccm16 = AesCcm(crypto.core.aes.Aes128, 16, 13);
/// AES-256-CCM* with no authentication (encryption-only, 13-byte nonce).
pub const Aes256Ccm0 = AesCcm(crypto.core.aes.Aes256, 0, 13);
/// AES-256-CCM with 8-byte authentication tag and 13-byte nonce.
pub const Aes256Ccm8 = AesCcm(crypto.core.aes.Aes256, 8, 13);
/// AES-256-CCM with 16-byte authentication tag and 13-byte nonce.
pub const Aes256Ccm16 = AesCcm(crypto.core.aes.Aes256, 16, 13);

/// AES-CCM authenticated encryption (NIST SP 800-38C, RFC 3610).
/// CCM* mode extends CCM to support encryption-only mode when tag_len=0.
///
/// `BlockCipher`: Block cipher type (must have 16-byte blocks).
/// `tag_len`: Authentication tag length in bytes (0, 4, 6, 8, 10, 12, 14, or 16).
///            When tag_len=0, CCM* provides encryption-only (no authentication).
/// `nonce_len`: Nonce length in bytes (7 to 13).
fn AesCcm(comptime BlockCipher: type, comptime tag_len: usize, comptime nonce_len: usize) type {
    const block_length = BlockCipher.block.block_length;

    comptime {
        assert(block_length == 16); // CCM requires 16-byte blocks
        if (tag_len != 0 and (tag_len < 4 or tag_len > 16 or tag_len % 2 != 0)) {
            @compileError("CCM tag_length must be 0, 4, 6, 8, 10, 12, 14, or 16 bytes");
        }
        if (nonce_len < 7 or nonce_len > 13) {
            @compileError("CCM nonce_length must be between 7 and 13 bytes");
        }
    }

    const L = 15 - nonce_len; // Counter size in bytes (2 to 8)

    return struct {
        pub const key_length = BlockCipher.key_bits / 8;
        pub const tag_length = tag_len;
        pub const nonce_length = nonce_len;

        /// `c`: Ciphertext output buffer (must be same length as m).
        /// `tag`: Authentication tag output.
        /// `m`: Plaintext message to encrypt.
        /// `ad`: Associated data to authenticate.
        /// `npub`: Public nonce (must be unique for each message with same key).
        /// `key`: Encryption key.
        pub fn encrypt(
            c: []u8,
            tag: *[tag_length]u8,
            m: []const u8,
            ad: []const u8,
            npub: [nonce_length]u8,
            key: [key_length]u8,
        ) void {
            assert(c.len == m.len);

            // Validate message length fits in L bytes
            const max_msg_len: u64 = if (L >= 8) std.math.maxInt(u64) else (@as(u64, 1) << @as(u6, @intCast(L * 8))) - 1;
            assert(m.len <= max_msg_len);

            const cipher_ctx = BlockCipher.initEnc(key);

            // CCM*: Skip authentication if tag_length is 0 (encryption-only mode)
            if (tag_length > 0) {
                // Compute CBC-MAC using the reusable CBC-MAC module
                var mac_result: [block_length]u8 = undefined;
                computeCbcMac(&mac_result, &key, m, ad, npub);

                // Construct counter block for tag encryption (counter = 0)
                var ctr_block: [block_length]u8 = undefined;
                formatCtrBlock(&ctr_block, npub, 0);

                // Encrypt the MAC tag
                var s0: [block_length]u8 = undefined;
                cipher_ctx.encrypt(&s0, &ctr_block);
                for (tag, mac_result[0..tag_length], s0[0..tag_length]) |*t, mac_byte, s_byte| {
                    t.* = mac_byte ^ s_byte;
                }

                crypto.secureZero(u8, &mac_result);
                crypto.secureZero(u8, &s0);
            }

            // Encrypt the plaintext using CTR mode (starting from counter = 1)
            var ctr_block: [block_length]u8 = undefined;
            formatCtrBlock(&ctr_block, npub, 1);
            // CCM counter is in the last L bytes of the block
            modes.ctrSlice(@TypeOf(cipher_ctx), cipher_ctx, c, m, ctr_block, .big, 1 + nonce_len, L);
        }

        /// `m`: Plaintext output buffer (must be same length as c).
        /// `c`: Ciphertext to decrypt.
        /// `tag`: Authentication tag to verify.
        /// `ad`: Associated data (must match encryption).
        /// `npub`: Public nonce (must match encryption).
        /// `key`: Private key.
        ///
        /// Asserts `c.len == m.len`.
        /// Contents of `m` are undefined if an error is returned.
        pub fn decrypt(
            m: []u8,
            c: []const u8,
            tag: [tag_length]u8,
            ad: []const u8,
            npub: [nonce_length]u8,
            key: [key_length]u8,
        ) AuthenticationError!void {
            assert(m.len == c.len);

            const cipher_ctx = BlockCipher.initEnc(key);

            // Decrypt the ciphertext using CTR mode (starting from counter = 1)
            var ctr_block: [block_length]u8 = undefined;
            formatCtrBlock(&ctr_block, npub, 1);
            // CCM counter is in the last L bytes of the block
            modes.ctrSlice(@TypeOf(cipher_ctx), cipher_ctx, m, c, ctr_block, .big, 1 + nonce_len, L);

            // CCM*: Skip authentication if tag_length is 0 (encryption-only mode)
            if (tag_length > 0) {
                // Compute CBC-MAC over decrypted plaintext
                var mac_result: [block_length]u8 = undefined;
                computeCbcMac(&mac_result, &key, m, ad, npub);

                // Decrypt the received tag
                formatCtrBlock(&ctr_block, npub, 0);
                var s0: [block_length]u8 = undefined;
                cipher_ctx.encrypt(&s0, &ctr_block);

                // Reconstruct the expected MAC
                var expected_mac: [tag_length]u8 = undefined;
                for (&expected_mac, mac_result[0..tag_length], s0[0..tag_length]) |*e, mac_byte, s_byte| {
                    e.* = mac_byte ^ s_byte;
                }

                // Constant-time tag comparison
                const valid = crypto.timing_safe.eql([tag_length]u8, expected_mac, tag);
                if (!valid) {
                    crypto.secureZero(u8, &expected_mac);
                    crypto.secureZero(u8, &mac_result);
                    crypto.secureZero(u8, &s0);
                    crypto.secureZero(u8, m);
                    return error.AuthenticationFailed;
                }

                crypto.secureZero(u8, &expected_mac);
                crypto.secureZero(u8, &mac_result);
                crypto.secureZero(u8, &s0);
            }
        }

        /// Format the counter block for CTR mode
        /// Counter block format: [flags | nonce | counter]
        /// flags = L - 1
        fn formatCtrBlock(block: *[block_length]u8, npub: [nonce_length]u8, counter: u64) void {
            @memset(block, 0);
            block[0] = L - 1; // flags
            @memcpy(block[1..][0..nonce_length], &npub);
            // Counter goes in the last L bytes
            const CounterInt = std.meta.Int(.unsigned, L * 8);
            mem.writeInt(CounterInt, block[1 + nonce_length ..][0..L], @as(CounterInt, @intCast(counter)), .big);
        }

        /// Compute CBC-MAC over the message and associated data.
        /// CCM uses plain CBC-MAC, not CMAC (RFC 3610).
        fn computeCbcMac(mac: *[block_length]u8, key: *const [key_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8) void {
            const CbcMac = cbc_mac.CbcMac(BlockCipher);
            var ctx = CbcMac.init(key);

            // Process B_0 block
            var b0: [block_length]u8 = undefined;
            formatB0Block(&b0, m.len, ad.len, npub);
            ctx.update(&b0);

            // Process associated data if present
            // RFC 3610: AD is (encoded_length || ad) padded to block boundary
            if (ad.len > 0) {
                // Encode and add associated data length
                var ad_len_encoding: [10]u8 = undefined;
                const ad_len_size = encodeAdLength(&ad_len_encoding, ad.len);

                // Process AD with padding to block boundary
                ctx.update(ad_len_encoding[0..ad_len_size]);
                ctx.update(ad);

                // Add zero padding to reach block boundary
                const total_ad_size = ad_len_size + ad.len;
                const remainder = total_ad_size % block_length;
                if (remainder > 0) {
                    const padding = [_]u8{0} ** block_length;
                    ctx.update(padding[0 .. block_length - remainder]);
                }
            }

            // Process plaintext message
            ctx.update(m);

            // Finalize MAC
            ctx.final(mac);
        }

        /// Format the B_0 block for CBC-MAC
        /// B_0 format: [flags | nonce | message_length]
        /// flags = 64*Adata + 8*M' + L'
        /// where: Adata = (ad.len > 0), M' = (tag_length - 2)/2 if M>0 else 0, L' = L - 1
        /// CCM*: When tag_length=0, M' is encoded as 0
        fn formatB0Block(block: *[block_length]u8, msg_len: usize, ad_len: usize, npub: [nonce_length]u8) void {
            @memset(block, 0);

            const Adata: u8 = if (ad_len > 0) 1 else 0;
            const M_prime: u8 = if (tag_length > 0) @intCast((tag_length - 2) / 2) else 0;
            const L_prime: u8 = L - 1;

            block[0] = (Adata << 6) | (M_prime << 3) | L_prime;
            @memcpy(block[1..][0..nonce_length], &npub);

            // Encode message length in last L bytes
            const LengthInt = std.meta.Int(.unsigned, L * 8);
            mem.writeInt(LengthInt, block[1 + nonce_length ..][0..L], @as(LengthInt, @intCast(msg_len)), .big);
        }

        /// Encode associated data length according to CCM specification
        /// Returns the number of bytes written
        fn encodeAdLength(buf: *[10]u8, ad_len: usize) usize {
            if (ad_len < 65280) { // 2^16 - 2^8
                // Encode as 2 bytes
                mem.writeInt(u16, buf[0..2], @as(u16, @intCast(ad_len)), .big);
                return 2;
            } else if (ad_len <= std.math.maxInt(u32)) {
                // Encode as 0xff || 0xfe || 4 bytes
                buf[0] = 0xff;
                buf[1] = 0xfe;
                mem.writeInt(u32, buf[2..6], @as(u32, @intCast(ad_len)), .big);
                return 6;
            } else {
                // Encode as 0xff || 0xff || 8 bytes
                buf[0] = 0xff;
                buf[1] = 0xff;
                mem.writeInt(u64, buf[2..10], @as(u64, @intCast(ad_len)), .big);
                return 10;
            }
        }
    };
}

// Tests

const testing = std.testing;
const fmt = std.fmt;
const hexToBytes = fmt.hexToBytes;

test "Aes256Ccm8 - Encrypt decrypt round-trip" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "Hello, World! This is a test message.";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, m, "", nonce, key);

    try Aes256Ccm8.decrypt(&m2, &c, tag, "", nonce, key);

    try testing.expectEqualSlices(u8, m[0..], m2[0..]);
}

test "Aes256Ccm8 - Associated data" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "secret message";
    const ad = "additional authenticated data";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, m, ad, nonce, key);

    try Aes256Ccm8.decrypt(&m2, &c, tag, ad, nonce, key);
    try testing.expectEqualSlices(u8, m[0..], m2[0..]);

    var m3: [m.len]u8 = undefined;
    const wrong_adata = "wrong data";
    const result = Aes256Ccm8.decrypt(&m3, &c, tag, wrong_adata, nonce, key);
    try testing.expectError(error.AuthenticationFailed, result);
}

test "Aes256Ccm8 - Wrong key" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const wrong_key: [32]u8 = [_]u8{0x43} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "secret";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, m, "", nonce, key);

    const result = Aes256Ccm8.decrypt(&m2, &c, tag, "", nonce, wrong_key);
    try testing.expectError(error.AuthenticationFailed, result);
}

test "Aes256Ccm8 - Corrupted ciphertext" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "secret message";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, m, "", nonce, key);

    c[5] ^= 0xFF;

    const result = Aes256Ccm8.decrypt(&m2, &c, tag, "", nonce, key);
    try testing.expectError(error.AuthenticationFailed, result);
}

test "Aes256Ccm8 - Empty plaintext" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, m, "", nonce, key);

    try Aes256Ccm8.decrypt(&m2, &c, tag, "", nonce, key);

    try testing.expectEqual(@as(usize, 0), m2.len);
}

test "Aes128Ccm8 - Basic functionality" {
    const key: [16]u8 = [_]u8{0x42} ** 16;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "Test AES-128-CCM";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes128Ccm8.tag_length]u8 = undefined;

    Aes128Ccm8.encrypt(&c, &tag, m, "", nonce, key);

    try Aes128Ccm8.decrypt(&m2, &c, tag, "", nonce, key);

    try testing.expectEqualSlices(u8, m[0..], m2[0..]);
}

test "Aes256Ccm16 - 16-byte tag" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "Test 16-byte tag";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm16.tag_length]u8 = undefined;

    Aes256Ccm16.encrypt(&c, &tag, m, "", nonce, key);

    try testing.expectEqual(@as(usize, 16), tag.len);

    try Aes256Ccm16.decrypt(&m2, &c, tag, "", nonce, key);

    try testing.expectEqualSlices(u8, m[0..], m2[0..]);
}

test "Aes256Ccm8 - Edge case short nonce" {
    const Aes256Ccm8_7 = AesCcm(crypto.core.aes.Aes256, 8, 7);
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "eda32f751456e33195f1f499cf2dc7c97ea127b6d488f211ccc5126fbb24afa6");
    var nonce: [7]u8 = undefined;
    _ = try hexToBytes(&nonce, "a544218dadd3c1");
    var m: [1]u8 = undefined;
    _ = try hexToBytes(&m, "00");

    var c: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8_7.tag_length]u8 = undefined;

    Aes256Ccm8_7.encrypt(&c, &tag, &m, "", nonce, key);

    var m2: [c.len]u8 = undefined;

    try Aes256Ccm8_7.decrypt(&m2, &c, tag, "", nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);
}

test "Aes256Ccm8 - Edge case long nonce" {
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "e1b8a927a95efe94656677b692662000278b441c79e879dd5c0ddc758bdc9ee8");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "a544218dadd3c10583db49cf39");
    var m: [1]u8 = undefined;
    _ = try hexToBytes(&m, "00");

    var c: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, &m, "", nonce, key);

    var m2: [c.len]u8 = undefined;

    try Aes256Ccm8.decrypt(&m2, &c, tag, "", nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);
}

test "Aes256Ccm8 - With AAD and wrong AAD detection" {
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "8c5cf3457ff22228c39c051c4e05ed4093657eb303f859a9d4b0f8be0127d88a");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "a544218dadd3c10583db49cf39");
    var m: [1]u8 = undefined;
    _ = try hexToBytes(&m, "00");
    var ad: [32]u8 = undefined;
    _ = try hexToBytes(&ad, "3c0e2815d37d844f7ac240ba9d6e3a0b2a86f706e885959e09a1005e024f6907");

    var c: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, &m, &ad, nonce, key);

    var m2: [c.len]u8 = undefined;

    try Aes256Ccm8.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);

    var wrong_ad: [32]u8 = undefined;
    _ = try hexToBytes(&wrong_ad, "0000000000000000000000000000000000000000000000000000000000000000");
    var m3: [c.len]u8 = undefined;
    const result = Aes256Ccm8.decrypt(&m3, &c, tag, &wrong_ad, nonce, key);
    try testing.expectError(error.AuthenticationFailed, result);
}

test "Aes256Ccm8 - Multi-block payload" {
    const Aes256Ccm8_12 = AesCcm(crypto.core.aes.Aes256, 8, 12);

    // Test with 32-byte payload (2 AES blocks)
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "af063639e66c284083c5cf72b70d8bc277f5978e80d9322d99f2fdc718cda569");
    var nonce: [12]u8 = undefined;
    _ = try hexToBytes(&nonce, "a544218dadd3c10583db49cf");
    var m: [32]u8 = undefined;
    _ = try hexToBytes(&m, "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");

    // Encrypt
    var c: [32]u8 = undefined;
    var tag: [Aes256Ccm8_12.tag_length]u8 = undefined;

    Aes256Ccm8_12.encrypt(&c, &tag, &m, "", nonce, key);

    // Decrypt and verify
    var m2: [32]u8 = undefined;

    try Aes256Ccm8_12.decrypt(&m2, &c, tag, "", nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);
}

test "Aes256Ccm8 - Multi-block with AAD" {
    const Aes256Ccm8_12 = AesCcm(crypto.core.aes.Aes256, 8, 12);

    // Test with multi-block payload (3 AES blocks) and AAD
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "f7079dfa3b5c7b056347d7e437bcded683abd6e2c9e069d333284082cbb5d453");
    var nonce: [12]u8 = undefined;
    _ = try hexToBytes(&nonce, "5b8e40746f6b98e00f1d13ff");

    // 48-byte payload (3 AES blocks)
    var m: [48]u8 = undefined;
    _ = try hexToBytes(&m, "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f");

    // 16-byte AAD
    var ad: [16]u8 = undefined;
    _ = try hexToBytes(&ad, "000102030405060708090a0b0c0d0e0f");

    // Encrypt
    var c: [48]u8 = undefined;
    var tag: [Aes256Ccm8_12.tag_length]u8 = undefined;

    Aes256Ccm8_12.encrypt(&c, &tag, &m, &ad, nonce, key);

    // Decrypt and verify
    var m2: [48]u8 = undefined;

    try Aes256Ccm8_12.decrypt(&m2, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &m, &m2);
}

test "Aes256Ccm8 - Minimum nonce length" {
    const Aes256Ccm8_7 = AesCcm(crypto.core.aes.Aes256, 8, 7);

    // Test with 7-byte nonce (minimum allowed by CCM spec)
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f");
    var nonce: [7]u8 = undefined;
    _ = try hexToBytes(&nonce, "10111213141516");
    const m = "Test message with minimum nonce length";

    // Encrypt
    var c: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8_7.tag_length]u8 = undefined;

    Aes256Ccm8_7.encrypt(&c, &tag, m, "", nonce, key);

    // Decrypt and verify
    var m2: [m.len]u8 = undefined;

    try Aes256Ccm8_7.decrypt(&m2, &c, tag, "", nonce, key);
    try testing.expectEqualSlices(u8, m[0..], m2[0..]);
}

test "Aes256Ccm8 - Maximum nonce length" {
    // Test with 13-byte nonce (maximum allowed by CCM spec)
    var key: [32]u8 = undefined;
    _ = try hexToBytes(&key, "606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "101112131415161718191a1b1c");
    const m = "Test message with maximum nonce length";

    // Encrypt
    var c: [m.len]u8 = undefined;
    var tag: [Aes256Ccm8.tag_length]u8 = undefined;

    Aes256Ccm8.encrypt(&c, &tag, m, "", nonce, key);

    // Decrypt and verify
    var m2: [m.len]u8 = undefined;

    try Aes256Ccm8.decrypt(&m2, &c, tag, "", nonce, key);
    try testing.expectEqualSlices(u8, m[0..], m2[0..]);
}

// RFC 3610 test vectors

test "Aes128Ccm8 - RFC 3610 Packet Vector #1" {
    const Aes128Ccm8_13 = AesCcm(crypto.core.aes.Aes128, 8, 13);

    // RFC 3610 Appendix A, Packet Vector #1
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "00000003020100A0A1A2A3A4A5");
    var ad: [8]u8 = undefined;
    _ = try hexToBytes(&ad, "0001020304050607");
    var plaintext: [23]u8 = undefined;
    _ = try hexToBytes(&plaintext, "08090A0B0C0D0E0F101112131415161718191A1B1C1D1E");

    // Expected ciphertext and tag from RFC
    var expected_ciphertext: [23]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "588C979A61C663D2F066D0C2C0F989806D5F6B61DAC384");
    var expected_tag: [8]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "17E8D12CFDF926E0");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm8_13.tag_length]u8 = undefined;

    Aes128Ccm8_13.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches RFC expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches RFC expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm8_13.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

test "Aes128Ccm8 - RFC 3610 Packet Vector #2" {
    const Aes128Ccm8_13 = AesCcm(crypto.core.aes.Aes128, 8, 13);

    // RFC 3610 Appendix A, Packet Vector #2 (8-byte tag, M=8)
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "00000004030201A0A1A2A3A4A5");
    var ad: [8]u8 = undefined;
    _ = try hexToBytes(&ad, "0001020304050607");
    var plaintext: [24]u8 = undefined;
    _ = try hexToBytes(&plaintext, "08090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");

    // Expected ciphertext and tag from RFC (from total packet: header + ciphertext + tag)
    var expected_ciphertext: [24]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "72C91A36E135F8CF291CA894085C87E3CC15C439C9E43A3B");
    var expected_tag: [8]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "A091D56E10400916");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm8_13.tag_length]u8 = undefined;

    Aes128Ccm8_13.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches RFC expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches RFC expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm8_13.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

test "Aes128Ccm8 - RFC 3610 Packet Vector #3" {
    const Aes128Ccm8_13 = AesCcm(crypto.core.aes.Aes128, 8, 13);

    // RFC 3610 Appendix A, Packet Vector #3 (8-byte tag, 25-byte payload)
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "00000005040302A0A1A2A3A4A5");
    var ad: [8]u8 = undefined;
    _ = try hexToBytes(&ad, "0001020304050607");
    var plaintext: [25]u8 = undefined;
    _ = try hexToBytes(&plaintext, "08090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20");

    // Expected ciphertext and tag from RFC
    var expected_ciphertext: [25]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "51B1E5F44A197D1DA46B0F8E2D282AE871E838BB64DA859657");
    var expected_tag: [8]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "4ADAA76FBD9FB0C5");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm8_13.tag_length]u8 = undefined;

    Aes128Ccm8_13.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches RFC expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches RFC expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm8_13.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

// NIST SP 800-38C test vectors

test "Aes128Ccm4 - NIST SP 800-38C Example 1" {
    const Aes128Ccm4_7 = AesCcm(crypto.core.aes.Aes128, 4, 7);

    // Example 1 (C.1): Klen=128, Tlen=32, Nlen=56, Alen=64, Plen=32
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "404142434445464748494a4b4c4d4e4f");
    var nonce: [7]u8 = undefined;
    _ = try hexToBytes(&nonce, "10111213141516");
    var ad: [8]u8 = undefined;
    _ = try hexToBytes(&ad, "0001020304050607");
    var plaintext: [4]u8 = undefined;
    _ = try hexToBytes(&plaintext, "20212223");

    // Expected ciphertext and tag from NIST
    var expected_ciphertext: [4]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "7162015b");
    var expected_tag: [4]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "4dac255d");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm4_7.tag_length]u8 = undefined;

    Aes128Ccm4_7.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm4_7.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

test "Aes128Ccm6 - NIST SP 800-38C Example 2" {
    const Aes128Ccm6_8 = AesCcm(crypto.core.aes.Aes128, 6, 8);

    // Example 2 (C.2): Klen=128, Tlen=48, Nlen=64, Alen=128, Plen=128
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "404142434445464748494a4b4c4d4e4f");
    var nonce: [8]u8 = undefined;
    _ = try hexToBytes(&nonce, "1011121314151617");
    var ad: [16]u8 = undefined;
    _ = try hexToBytes(&ad, "000102030405060708090a0b0c0d0e0f");
    var plaintext: [16]u8 = undefined;
    _ = try hexToBytes(&plaintext, "202122232425262728292a2b2c2d2e2f");

    // Expected ciphertext and tag from NIST
    var expected_ciphertext: [16]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "d2a1f0e051ea5f62081a7792073d593d");
    var expected_tag: [6]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "1fc64fbfaccd");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm6_8.tag_length]u8 = undefined;

    Aes128Ccm6_8.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm6_8.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

test "Aes128Ccm8 - NIST SP 800-38C Example 3" {
    const Aes128Ccm8_12 = AesCcm(crypto.core.aes.Aes128, 8, 12);

    // Example 3 (C.3): Klen=128, Tlen=64, Nlen=96, Alen=160, Plen=192
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "404142434445464748494a4b4c4d4e4f");
    var nonce: [12]u8 = undefined;
    _ = try hexToBytes(&nonce, "101112131415161718191a1b");
    var ad: [20]u8 = undefined;
    _ = try hexToBytes(&ad, "000102030405060708090a0b0c0d0e0f10111213");
    var plaintext: [24]u8 = undefined;
    _ = try hexToBytes(&plaintext, "202122232425262728292a2b2c2d2e2f3031323334353637");

    // Expected ciphertext and tag from NIST
    var expected_ciphertext: [24]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "e3b201a9f5b71a7a9b1ceaeccd97e70b6176aad9a4428aa5");
    var expected_tag: [8]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "484392fbc1b09951");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm8_12.tag_length]u8 = undefined;

    Aes128Ccm8_12.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm8_12.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

test "Aes128Ccm14 - NIST SP 800-38C Example 4" {
    const Aes128Ccm14_13 = AesCcm(crypto.core.aes.Aes128, 14, 13);

    // Example 4 (C.4): Klen=128, Tlen=112, Nlen=104, Alen=524288, Plen=256
    // Note: Associated data is 65536 bytes (256-byte pattern repeated 256 times)
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "404142434445464748494a4b4c4d4e4f");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "101112131415161718191a1b1c");
    var plaintext: [32]u8 = undefined;
    _ = try hexToBytes(&plaintext, "202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f");

    // Generate 65536-byte associated data (256-byte pattern repeated 256 times)
    var pattern: [256]u8 = undefined;
    _ = try hexToBytes(&pattern, "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebfc0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff");

    var ad: [65536]u8 = undefined;
    for (0..256) |i| {
        @memcpy(ad[i * 256 .. (i + 1) * 256], &pattern);
    }

    // Expected ciphertext and tag from NIST
    var expected_ciphertext: [32]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "69915dad1e84c6376a68c2967e4dab615ae0fd1faec44cc484828529463ccf72");
    var expected_tag: [14]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "b4ac6bec93e8598e7f0dadbcea5b");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm14_13.tag_length]u8 = undefined;

    Aes128Ccm14_13.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Verify tag matches NIST expected output
    try testing.expectEqualSlices(u8, &expected_tag, &tag);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm14_13.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

// CCM* test vectors (encryption-only mode with M=0)

test "Aes128Ccm0 - IEEE 802.15.4 Data Frame (Encryption-only)" {
    // IEEE 802.15.4 test vector from section 2.7
    // Security level 0x04 (ENC, encryption without authentication)
    var key: [16]u8 = undefined;
    _ = try hexToBytes(&key, "C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF");
    var nonce: [13]u8 = undefined;
    _ = try hexToBytes(&nonce, "ACDE48000000000100000005" ++ "04");
    var plaintext: [4]u8 = undefined;
    _ = try hexToBytes(&plaintext, "61626364");
    var ad: [26]u8 = undefined;
    _ = try hexToBytes(&ad, "69DC84214302000000004DEAC010000000048DEAC04050000");

    // Expected ciphertext from IEEE spec
    var expected_ciphertext: [4]u8 = undefined;
    _ = try hexToBytes(&expected_ciphertext, "D43E022B");

    // Encrypt
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128Ccm0.tag_length]u8 = undefined;

    Aes128Ccm0.encrypt(&c, &tag, &plaintext, &ad, nonce, key);

    // Verify ciphertext matches IEEE expected output
    try testing.expectEqualSlices(u8, &expected_ciphertext, &c);

    // Decrypt and verify round-trip
    var m: [plaintext.len]u8 = undefined;
    try Aes128Ccm0.decrypt(&m, &c, tag, &ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m);
}

test "Aes128Ccm0 - Zero-length plaintext with encryption-only" {
    const key: [16]u8 = [_]u8{0x42} ** 16;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "";
    const ad = "some associated data";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes128Ccm0.tag_length]u8 = undefined;

    Aes128Ccm0.encrypt(&c, &tag, m, ad, nonce, key);

    try Aes128Ccm0.decrypt(&m2, &c, tag, ad, nonce, key);

    try testing.expectEqual(@as(usize, 0), m2.len);
}

test "Aes256Ccm0 - Basic encryption-only round-trip" {
    const key: [32]u8 = [_]u8{0x42} ** 32;
    const nonce: [13]u8 = [_]u8{0x11} ** 13;
    const m = "Hello, CCM* encryption-only mode!";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Ccm0.tag_length]u8 = undefined;

    Aes256Ccm0.encrypt(&c, &tag, m, "", nonce, key);

    try Aes256Ccm0.decrypt(&m2, &c, tag, "", nonce, key);

    try testing.expectEqualSlices(u8, m[0..], m2[0..]);
}



---
File: /std/crypto/aes_gcm_siv.zig
---

const std = @import("std");
const assert = std.debug.assert;
const crypto = std.crypto;
const debug = std.debug;
const mem = std.mem;
const math = std.math;
const modes = @import("modes.zig");
const Polyval = @import("ghash_polyval.zig").Polyval;
const AuthenticationError = crypto.errors.AuthenticationError;

pub const Aes128GcmSiv = AesGcmSiv(crypto.core.aes.Aes128);
pub const Aes256GcmSiv = AesGcmSiv(crypto.core.aes.Aes256);

/// AES-GCM-SIV: Authenticated encryption that remains secure even if you accidentally reuse a nonce.
///
/// What it does: Encrypts data and protects it from tampering. You can also attach
/// unencrypted metadata (like headers) that will be authenticated but not encrypted.
///
/// When to use AES-GCM-SIV:
/// - When you can't guarantee unique nonces (though you should still try to use unique nonces)
///
/// When to use regular AES-GCM instead:
/// - When you can guarantee unique nonces (e.g., using a counter)
/// - When you need slightly better performance
///
/// Security: If you accidentally reuse a nonce with the same key, AES-GCM-SIV only
/// reveals whether two messages are identical. Regular AES-GCM would be catastrophically
/// broken in this scenario, potentially revealing the authentication key.
///
/// Performance: Slightly slower than AES-GCM due to the additional key derivation step.
///
/// Defined in RFC 8452.
fn AesGcmSiv(comptime Aes: anytype) type {
    debug.assert(Aes.block.block_length == 16);

    return struct {
        pub const tag_length = 16;
        pub const nonce_length = 12;
        pub const key_length = Aes.key_bits / 8;

        const zeros: [16]u8 = @splat(0);

        /// Derives the authentication and message encryption keys from the master key and nonce.
        /// This implements the key derivation as specified in RFC 8452 Section 4.
        /// Generates a 128-bit authentication key for POLYVAL and a message encryption key
        /// (128 or 256 bits depending on the AES variant).
        fn deriveKeys(message_key: *[key_length]u8, auth_key: *[16]u8, key: [key_length]u8, nonce: [nonce_length]u8) void {
            const aes = Aes.initEnc(key);

            // Derive authentication and message keys per RFC 8452 Section 4
            // Each encryption produces 16 bytes, but we only use first 8 bytes of each block

            if (key_length == 16) {
                // AES-128-GCM-SIV: Process 4 blocks in parallel
                var key_blocks: [4 * 16]u8 = undefined;
                var cipher_outs: [4 * 16]u8 = undefined;

                // Set up all 4 blocks with counters 0-3 and nonce
                inline for (0..4) |i| {
                    mem.writeInt(u32, key_blocks[i * 16 ..][0..4], @intCast(i), .little);
                    key_blocks[i * 16 + 4 .. i * 16 + 16].* = nonce;
                }

                // Encrypt all 4 blocks in parallel
                aes.encryptWide(4, &cipher_outs, &key_blocks);

                // Extract the key material (first 8 bytes of each block)
                @memcpy(auth_key[0..8], cipher_outs[0..8]);
                @memcpy(auth_key[8..16], cipher_outs[16..24]);
                @memcpy(message_key[0..8], cipher_outs[32..40]);
                @memcpy(message_key[8..16], cipher_outs[48..56]);
            } else {
                // AES-256-GCM-SIV: Process 6 blocks in parallel
                var key_blocks: [6 * 16]u8 = undefined;
                var cipher_outs: [6 * 16]u8 = undefined;

                // Set up all 6 blocks with counters 0-5 and nonce
                inline for (0..6) |i| {
                    mem.writeInt(u32, key_blocks[i * 16 ..][0..4], @intCast(i), .little);
                    key_blocks[i * 16 + 4 .. i * 16 + 16].* = nonce;
                }

                // Encrypt all 6 blocks in parallel
                aes.encryptWide(6, &cipher_outs, &key_blocks);

                // Extract the key material (first 8 bytes of each block)
                @memcpy(auth_key[0..8], cipher_outs[0..8]);
                @memcpy(auth_key[8..16], cipher_outs[16..24]);
                @memcpy(message_key[0..8], cipher_outs[32..40]);
                @memcpy(message_key[8..16], cipher_outs[48..56]);
                @memcpy(message_key[16..24], cipher_outs[64..72]);
                @memcpy(message_key[24..32], cipher_outs[80..88]);
            }
        }

        /// Encrypts and authenticates a message using AES-GCM-SIV.
        ///
        /// `c`: The ciphertext buffer to write the encrypted data to.
        /// `tag`: The authentication tag buffer to write the computed tag to.
        /// `m`: The plaintext message to encrypt.
        /// `ad`: The associated data to authenticate.
        /// `npub`: The nonce to use for encryption.
        /// `key`: The encryption key.
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) void {
            debug.assert(c.len == m.len);
            debug.assert(m.len <= (1 << 36));
            debug.assert(ad.len <= (1 << 36));

            var auth_key: [16]u8 = undefined;
            var message_key: [key_length]u8 = undefined;
            deriveKeys(&message_key, &auth_key, key, npub);

            // Calculate POLYVAL over additional data and plaintext
            const block_count = (math.divCeil(usize, ad.len, Polyval.block_length) catch unreachable) +
                (math.divCeil(usize, m.len, Polyval.block_length) catch unreachable) + 1;
            var mac = Polyval.initForBlockCount(&auth_key, block_count);

            // Process additional data
            mac.update(ad);
            mac.pad();

            // Process plaintext
            mac.update(m);
            mac.pad();

            // Length block
            var length_block: [16]u8 = undefined;
            mem.writeInt(u64, length_block[0..8], @as(u64, ad.len) * 8, .little);
            mem.writeInt(u64, length_block[8..16], @as(u64, m.len) * 8, .little);
            mac.update(&length_block);

            // Get POLYVAL result
            var s: [16]u8 = undefined;
            mac.final(&s);

            // XOR with nonce to get pre-tag
            for (npub, 0..) |b, i| {
                s[i] ^= b;
            }

            // Clear most significant bit of last byte
            s[15] &= 0x7f;

            // Encrypt to get tag
            const tag_aes = Aes.initEnc(message_key);
            tag_aes.encrypt(tag, &s);

            // Use tag as initial counter for CTR mode
            var counter: [16]u8 = tag.*;
            counter[15] |= 0x80; // Set most significant bit

            // Encrypt message using CTR mode with 32-bit little-endian counter
            const aes_ctx = Aes.initEnc(message_key);
            modes.ctrSlice(@TypeOf(aes_ctx), aes_ctx, c, m, counter, .little, 0, 4);
        }

        /// Decrypts and authenticates a message using AES-GCM-SIV.
        ///
        /// `m`: Message buffer to write the decrypted data to.
        /// `c`: The ciphertext to decrypt.
        /// `tag`: The authentication tag.
        /// `ad`: The associated data.
        /// `npub`: The nonce.
        /// `key`: The decryption key.
        /// Asserts `c.len == m.len`.
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);
            assert(c.len <= (1 << 36));
            assert(ad.len <= (1 << 36));

            var auth_key: [16]u8 = undefined;
            var message_key: [key_length]u8 = undefined;
            deriveKeys(&message_key, &auth_key, key, npub);

            // Decrypt message using CTR mode with 32-bit little-endian counter
            var counter: [16]u8 = tag;
            counter[15] |= 0x80; // Set most significant bit

            const aes_ctx = Aes.initEnc(message_key);
            modes.ctrSlice(@TypeOf(aes_ctx), aes_ctx, m, c, counter, .little, 0, 4);

            // Verify tag by recalculating POLYVAL
            const block_count = (math.divCeil(usize, ad.len, Polyval.block_length) catch unreachable) +
                (math.divCeil(usize, m.len, Polyval.block_length) catch unreachable) + 1;
            var mac = Polyval.initForBlockCount(&auth_key, block_count);

            // Process additional data
            mac.update(ad);
            mac.pad();

            // Process decrypted plaintext
            mac.update(m);
            mac.pad();

            // Length block
            var length_block: [16]u8 = undefined;
            mem.writeInt(u64, length_block[0..8], @as(u64, ad.len) * 8, .little);
            mem.writeInt(u64, length_block[8..16], @as(u64, m.len) * 8, .little);
            mac.update(&length_block);

            // Get POLYVAL result
            var s: [16]u8 = undefined;
            mac.final(&s);

            // XOR with nonce to get pre-tag
            for (npub, 0..) |b, i| {
                s[i] ^= b;
            }

            // Clear most significant bit of last byte
            s[15] &= 0x7f;

            // Encrypt to get expected tag
            const tag_aes = Aes.initEnc(message_key);
            var computed_tag: [tag_length]u8 = undefined;
            tag_aes.encrypt(&computed_tag, &s);

            // Verify tag
            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
        }
    };
}

const htest = @import("test.zig");
const testing = std.testing;

test "Aes128GcmSiv - RFC 8452 Test Vector 1" {
    // Test vector from RFC 8452 Appendix C.1
    const key = [_]u8{
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    const nonce = [_]u8{
        0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };
    const ad = "";
    const m = "";
    var c: [m.len]u8 = undefined;
    var tag: [Aes128GcmSiv.tag_length]u8 = undefined;

    Aes128GcmSiv.encrypt(&c, &tag, m, ad, nonce, key);
    try htest.assertEqual("dc20e2d83f25705bb49e439eca56de25", &tag);
}

test "Aes128GcmSiv - RFC 8452 Test Vector 2" {
    // Test vector from RFC 8452 Appendix C.1
    const key = [_]u8{
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    const nonce = [_]u8{
        0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };
    const plaintext = [_]u8{
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    const ad = "";
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128GcmSiv.tag_length]u8 = undefined;

    Aes128GcmSiv.encrypt(&c, &tag, &plaintext, ad, nonce, key);
    try htest.assertEqual("b5d839330ac7b786", &c);
    try htest.assertEqual("578782fff6013b815b287c22493a364c", &tag);

    var m2: [plaintext.len]u8 = undefined;
    try Aes128GcmSiv.decrypt(&m2, &c, tag, ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m2);
}

test "Aes128GcmSiv - RFC 8452 Test Vector 3" {
    // Test vector from RFC 8452 Appendix C.1
    const key = [_]u8{
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    const nonce = [_]u8{
        0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };
    const plaintext = [_]u8{
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };
    const ad = "";
    var c: [plaintext.len]u8 = undefined;
    var tag: [Aes128GcmSiv.tag_length]u8 = undefined;

    Aes128GcmSiv.encrypt(&c, &tag, &plaintext, ad, nonce, key);
    try htest.assertEqual("7323ea61d05932260047d942", &c);
    try htest.assertEqual("a4978db357391a0bc4fdec8b0d106639", &tag);

    var m2: [plaintext.len]u8 = undefined;
    try Aes128GcmSiv.decrypt(&m2, &c, tag, ad, nonce, key);
    try testing.expectEqualSlices(u8, &plaintext, &m2);
}

test "Aes256GcmSiv - RFC 8452 Test Vector" {
    // Test vector from RFC 8452 Appendix C.2
    const key = [_]u8{
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };
    const nonce = [_]u8{
        0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };
    const ad = "";
    const m = "";
    var c: [m.len]u8 = undefined;
    var tag: [Aes256GcmSiv.tag_length]u8 = undefined;

    Aes256GcmSiv.encrypt(&c, &tag, m, ad, nonce, key);
    try htest.assertEqual("07f5f4169bbf55a8400cd47ea6fd400f", &tag);
}

test "Aes128GcmSiv - Decrypt with wrong tag" {
    const key: [Aes128GcmSiv.key_length]u8 = @splat(0x69);
    const nonce: [Aes128GcmSiv.nonce_length]u8 = @splat(0x42);
    const m = "Test message";
    const ad = "";
    var c: [m.len]u8 = undefined;
    var tag: [Aes128GcmSiv.tag_length]u8 = undefined;

    Aes128GcmSiv.encrypt(&c, &tag, m, ad, nonce, key);

    // Corrupt the tag
    tag[0] ^= 0x01;

    var m2: [m.len]u8 = undefined;
    try testing.expectError(error.AuthenticationFailed, Aes128GcmSiv.decrypt(&m2, &c, tag, ad, nonce, key));
}



---
File: /std/crypto/aes_gcm.zig
---

const std = @import("std");
const assert = std.debug.assert;
const crypto = std.crypto;
const debug = std.debug;
const Ghash = std.crypto.onetimeauth.Ghash;
const math = std.math;
const mem = std.mem;
const modes = crypto.core.modes;
const AuthenticationError = crypto.errors.AuthenticationError;

pub const Aes128Gcm = AesGcm(crypto.core.aes.Aes128);
pub const Aes256Gcm = AesGcm(crypto.core.aes.Aes256);

fn AesGcm(comptime Aes: anytype) type {
    debug.assert(Aes.block.block_length == 16);

    return struct {
        pub const tag_length = 16;
        pub const nonce_length = 12;
        pub const key_length = Aes.key_bits / 8;

        const zeros = [_]u8{0} ** 16;

        /// `c`: The ciphertext buffer to write the encrypted data to.
        /// `tag`: The authentication tag buffer to write the computed tag to.
        /// `m`: The plaintext message to encrypt.
        /// `ad`: The associated data to authenticate.
        /// `npub`: The nonce to use for encryption.
        /// `key`: The encryption key.
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) void {
            debug.assert(c.len == m.len);
            debug.assert(m.len <= 16 * ((1 << 32) - 2));

            const aes = Aes.initEnc(key);
            var h: [16]u8 = undefined;
            aes.encrypt(&h, &zeros);

            var t: [16]u8 = undefined;
            var j: [16]u8 = undefined;
            j[0..nonce_length].* = npub;
            mem.writeInt(u32, j[nonce_length..][0..4], 1, .big);
            aes.encrypt(&t, &j);

            const block_count = (math.divCeil(usize, ad.len, Ghash.block_length) catch unreachable) + (math.divCeil(usize, c.len, Ghash.block_length) catch unreachable) + 1;
            var mac = Ghash.initForBlockCount(&h, block_count);
            mac.update(ad);
            mac.pad();

            mem.writeInt(u32, j[nonce_length..][0..4], 2, .big);
            modes.ctr(@TypeOf(aes), aes, c, m, j, .big);
            mac.update(c[0..m.len][0..]);
            mac.pad();

            var final_block = h;
            mem.writeInt(u64, final_block[0..8], @as(u64, ad.len) * 8, .big);
            mem.writeInt(u64, final_block[8..16], @as(u64, m.len) * 8, .big);
            mac.update(&final_block);
            mac.final(tag);
            for (t, 0..) |x, i| {
                tag[i] ^= x;
            }
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
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);

            const aes = Aes.initEnc(key);
            var h: [16]u8 = undefined;
            aes.encrypt(&h, &zeros);

            var t: [16]u8 = undefined;
            var j: [16]u8 = undefined;
            j[0..nonce_length].* = npub;
            mem.writeInt(u32, j[nonce_length..][0..4], 1, .big);
            aes.encrypt(&t, &j);

            const block_count = (math.divCeil(usize, ad.len, Ghash.block_length) catch unreachable) + (math.divCeil(usize, c.len, Ghash.block_length) catch unreachable) + 1;
            var mac = Ghash.initForBlockCount(&h, block_count);
            mac.update(ad);
            mac.pad();

            mac.update(c);
            mac.pad();

            var final_block = h;
            mem.writeInt(u64, final_block[0..8], @as(u64, ad.len) * 8, .big);
            mem.writeInt(u64, final_block[8..16], @as(u64, m.len) * 8, .big);
            mac.update(&final_block);
            var computed_tag: [Ghash.mac_length]u8 = undefined;
            mac.final(&computed_tag);
            for (t, 0..) |x, i| {
                computed_tag[i] ^= x;
            }

            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }

            mem.writeInt(u32, j[nonce_length..][0..4], 2, .big);
            modes.ctr(@TypeOf(aes), aes, m, c, j, .big);
        }
    };
}

const htest = @import("test.zig");
const testing = std.testing;

test "Aes256Gcm - Empty message and no associated data" {
    const key: [Aes256Gcm.key_length]u8 = [_]u8{0x69} ** Aes256Gcm.key_length;
    const nonce: [Aes256Gcm.nonce_length]u8 = [_]u8{0x42} ** Aes256Gcm.nonce_length;
    const ad = "";
    const m = "";
    var c: [m.len]u8 = undefined;
    var tag: [Aes256Gcm.tag_length]u8 = undefined;

    Aes256Gcm.encrypt(&c, &tag, m, ad, nonce, key);
    try htest.assertEqual("6b6ff610a16fa4cd59f1fb7903154e92", &tag);
}

test "Aes256Gcm - Associated data only" {
    const key: [Aes256Gcm.key_length]u8 = [_]u8{0x69} ** Aes256Gcm.key_length;
    const nonce: [Aes256Gcm.nonce_length]u8 = [_]u8{0x42} ** Aes256Gcm.nonce_length;
    const m = "";
    const ad = "Test with associated data";
    var c: [m.len]u8 = undefined;
    var tag: [Aes256Gcm.tag_length]u8 = undefined;

    Aes256Gcm.encrypt(&c, &tag, m, ad, nonce, key);
    try htest.assertEqual("262ed164c2dfb26e080a9d108dd9dd4c", &tag);
}

test "Aes256Gcm - Message only" {
    const key: [Aes256Gcm.key_length]u8 = [_]u8{0x69} ** Aes256Gcm.key_length;
    const nonce: [Aes256Gcm.nonce_length]u8 = [_]u8{0x42} ** Aes256Gcm.nonce_length;
    const m = "Test with message only";
    const ad = "";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Gcm.tag_length]u8 = undefined;

    Aes256Gcm.encrypt(&c, &tag, m, ad, nonce, key);
    try Aes256Gcm.decrypt(&m2, &c, tag, ad, nonce, key);
    try testing.expectEqualSlices(u8, m[0..], m2[0..]);

    try htest.assertEqual("5ca1642d90009fea33d01f78cf6eefaf01d539472f7c", &c);
    try htest.assertEqual("07cd7fc9103e2f9e9bf2dfaa319caff4", &tag);
}

test "Aes256Gcm - Message and associated data" {
    const key: [Aes256Gcm.key_length]u8 = [_]u8{0x69} ** Aes256Gcm.key_length;
    const nonce: [Aes256Gcm.nonce_length]u8 = [_]u8{0x42} ** Aes256Gcm.nonce_length;
    const m = "Test with message";
    const ad = "Test with associated data";
    var c: [m.len]u8 = undefined;
    var m2: [m.len]u8 = undefined;
    var tag: [Aes256Gcm.tag_length]u8 = undefined;

    Aes256Gcm.encrypt(&c, &tag, m, ad, nonce, key);
    try Aes256Gcm.decrypt(&m2, &c, tag, ad, nonce, key);
    try testing.expectEqualSlices(u8, m[0..], m2[0..]);

    try htest.assertEqual("5ca1642d90009fea33d01f78cf6eefaf01", &c);
    try htest.assertEqual("64accec679d444e2373bd9f6796c0d2c", &tag);
}



---
File: /std/crypto/aes_ocb.zig
---

const std = @import("std");
const builtin = @import("builtin");
const crypto = std.crypto;
const aes = crypto.core.aes;
const assert = std.debug.assert;
const math = std.math;
const mem = std.mem;
const AuthenticationError = crypto.errors.AuthenticationError;

pub const Aes128Ocb = AesOcb(aes.Aes128);
pub const Aes256Ocb = AesOcb(aes.Aes256);

const Block = [16]u8;

/// AES-OCB (RFC 7253 - https://competitions.cr.yp.to/round3/ocbv11.pdf)
fn AesOcb(comptime Aes: anytype) type {
    const EncryptCtx = aes.AesEncryptCtx(Aes);
    const DecryptCtx = aes.AesDecryptCtx(Aes);

    return struct {
        pub const key_length = Aes.key_bits / 8;
        pub const nonce_length: usize = 12;
        pub const tag_length: usize = 16;

        const Lx = struct {
            star: Block align(16),
            dol: Block align(16),
            table: [56]Block align(16) = undefined,
            upto: usize,

            fn double(l: Block) Block {
                const l_ = mem.readInt(u128, &l, .big);
                const l_2 = (l_ << 1) ^ (0x87 & -%(l_ >> 127));
                var l2: Block = undefined;
                mem.writeInt(u128, &l2, l_2, .big);
                return l2;
            }

            fn precomp(lx: *Lx, upto: usize) []const Block {
                const table = &lx.table;
                assert(upto < table.len);
                var i = lx.upto;
                while (i + 1 <= upto) : (i += 1) {
                    table[i + 1] = double(table[i]);
                }
                lx.upto = upto;
                return lx.table[0 .. upto + 1];
            }

            fn init(aes_enc_ctx: EncryptCtx) Lx {
                const zeros = [_]u8{0} ** 16;
                var star: Block = undefined;
                aes_enc_ctx.encrypt(&star, &zeros);
                const dol = double(star);
                var lx = Lx{ .star = star, .dol = dol, .upto = 0 };
                lx.table[0] = double(dol);
                return lx;
            }
        };

        fn hash(aes_enc_ctx: EncryptCtx, lx: *Lx, a: []const u8) Block {
            const full_blocks: usize = a.len / 16;
            const x_max = if (full_blocks > 0) math.log2_int(usize, full_blocks) else 0;
            const lt = lx.precomp(x_max);
            var sum = [_]u8{0} ** 16;
            var offset = [_]u8{0} ** 16;
            var i: usize = 0;
            while (i < full_blocks) : (i += 1) {
                xorWith(&offset, lt[@ctz(i + 1)]);
                var e = xorBlocks(offset, a[i * 16 ..][0..16].*);
                aes_enc_ctx.encrypt(&e, &e);
                xorWith(&sum, e);
            }
            const leftover = a.len % 16;
            if (leftover > 0) {
                xorWith(&offset, lx.star);
                var padded = [_]u8{0} ** 16;
                @memcpy(padded[0..leftover], a[i * 16 ..][0..leftover]);
                padded[leftover] = 0x80;
                var e = xorBlocks(offset, padded);
                aes_enc_ctx.encrypt(&e, &e);
                xorWith(&sum, e);
            }
            return sum;
        }

        fn getOffset(aes_enc_ctx: EncryptCtx, npub: [nonce_length]u8) Block {
            var nx = [_]u8{0} ** 16;
            nx[0] = @as(u8, @intCast(@as(u7, @truncate(tag_length * 8)) << 1));
            nx[16 - nonce_length - 1] = 1;
            nx[nx.len - nonce_length ..].* = npub;

            const bottom: u6 = @truncate(nx[15]);
            nx[15] &= 0xc0;
            var ktop_: Block = undefined;
            aes_enc_ctx.encrypt(&ktop_, &nx);
            const ktop = mem.readInt(u128, &ktop_, .big);
            const stretch = (@as(u192, ktop) << 64) | @as(u192, @as(u64, @truncate(ktop >> 64)) ^ @as(u64, @truncate(ktop >> 56)));
            var offset: Block = undefined;
            mem.writeInt(u128, &offset, @as(u128, @truncate(stretch >> (64 - @as(u7, bottom)))), .big);
            return offset;
        }

        const has_aesni = builtin.cpu.has(.x86, .aes);
        const has_armaes = builtin.cpu.has(.aarch64, .aes);
        const wb: usize = if ((builtin.cpu.arch == .x86_64 and has_aesni) or (builtin.cpu.arch == .aarch64 and has_armaes)) 4 else 0;

        /// c: ciphertext: output buffer should be of size m.len
        /// tag: authentication tag: output MAC
        /// m: message
        /// ad: Associated Data
        /// npub: public nonce
        /// k: secret key
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) void {
            assert(c.len == m.len);

            const aes_enc_ctx = Aes.initEnc(key);
            const full_blocks: usize = m.len / 16;
            const x_max = if (full_blocks > 0) math.log2_int(usize, full_blocks) else 0;
            var lx = Lx.init(aes_enc_ctx);
            const lt = lx.precomp(x_max);

            var offset = getOffset(aes_enc_ctx, npub);
            var sum = [_]u8{0} ** 16;
            var i: usize = 0;

            while (wb > 0 and i + wb <= full_blocks) : (i += wb) {
                var offsets: [wb]Block align(16) = undefined;
                var es: [16 * wb]u8 align(16) = undefined;
                var j: usize = 0;
                while (j < wb) : (j += 1) {
                    xorWith(&offset, lt[@ctz(i + 1 + j)]);
                    offsets[j] = offset;
                    const p = m[(i + j) * 16 ..][0..16].*;
                    es[j * 16 ..][0..16].* = xorBlocks(p, offsets[j]);
                    xorWith(&sum, p);
                }
                aes_enc_ctx.encryptWide(wb, &es, &es);
                j = 0;
                while (j < wb) : (j += 1) {
                    const e = es[j * 16 ..][0..16].*;
                    c[(i + j) * 16 ..][0..16].* = xorBlocks(e, offsets[j]);
                }
            }
            while (i < full_blocks) : (i += 1) {
                xorWith(&offset, lt[@ctz(i + 1)]);
                const p = m[i * 16 ..][0..16].*;
                var e = xorBlocks(p, offset);
                aes_enc_ctx.encrypt(&e, &e);
                c[i * 16 ..][0..16].* = xorBlocks(e, offset);
                xorWith(&sum, p);
            }
            const leftover = m.len % 16;
            if (leftover > 0) {
                xorWith(&offset, lx.star);
                var pad = offset;
                aes_enc_ctx.encrypt(&pad, &pad);
                var e = [_]u8{0} ** 16;
                @memcpy(e[0..leftover], m[i * 16 ..][0..leftover]);
                e[leftover] = 0x80;
                for (m[i * 16 ..], 0..) |x, j| {
                    c[i * 16 + j] = pad[j] ^ x;
                }
                xorWith(&sum, e);
            }
            var e = xorBlocks(xorBlocks(sum, offset), lx.dol);
            aes_enc_ctx.encrypt(&e, &e);
            tag.* = xorBlocks(e, hash(aes_enc_ctx, &lx, ad));
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
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const u8, npub: [nonce_length]u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);

            const aes_enc_ctx = Aes.initEnc(key);
            const aes_dec_ctx = DecryptCtx.initFromEnc(aes_enc_ctx);
            const full_blocks: usize = m.len / 16;
            const x_max = if (full_blocks > 0) math.log2_int(usize, full_blocks) else 0;
            var lx = Lx.init(aes_enc_ctx);
            const lt = lx.precomp(x_max);

            var offset = getOffset(aes_enc_ctx, npub);
            var sum = [_]u8{0} ** 16;
            var i: usize = 0;

            while (wb > 0 and i + wb <= full_blocks) : (i += wb) {
                var offsets: [wb]Block align(16) = undefined;
                var es: [16 * wb]u8 align(16) = undefined;
                var j: usize = 0;
                while (j < wb) : (j += 1) {
                    xorWith(&offset, lt[@ctz(i + 1 + j)]);
                    offsets[j] = offset;
                    const q = c[(i + j) * 16 ..][0..16].*;
                    es[j * 16 ..][0..16].* = xorBlocks(q, offsets[j]);
                }
                aes_dec_ctx.decryptWide(wb, &es, &es);
                j = 0;
                while (j < wb) : (j += 1) {
                    const p = xorBlocks(es[j * 16 ..][0..16].*, offsets[j]);
                    m[(i + j) * 16 ..][0..16].* = p;
                    xorWith(&sum, p);
                }
            }
            while (i < full_blocks) : (i += 1) {
                xorWith(&offset, lt[@ctz(i + 1)]);
                const q = c[i * 16 ..][0..16].*;
                var e = xorBlocks(q, offset);
                aes_dec_ctx.decrypt(&e, &e);
                const p = xorBlocks(e, offset);
                m[i * 16 ..][0..16].* = p;
                xorWith(&sum, p);
            }
            const leftover = m.len % 16;
            if (leftover > 0) {
                xorWith(&offset, lx.star);
                var pad = offset;
                aes_enc_ctx.encrypt(&pad, &pad);
                for (c[i * 16 ..], 0..) |x, j| {
                    m[i * 16 + j] = pad[j] ^ x;
                }
                var e = [_]u8{0} ** 16;
                @memcpy(e[0..leftover], m[i * 16 ..][0..leftover]);
                e[leftover] = 0x80;
                xorWith(&sum, e);
            }
            var e = xorBlocks(xorBlocks(sum, offset), lx.dol);
            aes_enc_ctx.encrypt(&e, &e);
            var computed_tag = xorBlocks(e, hash(aes_enc_ctx, &lx, ad));
            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
        }
    };
}

fn xorBlocks(x: Block, y: Block) Block {
    var z: Block = x;
    for (&z, 0..) |*v, i| {
        v.* = x[i] ^ y[i];
    }
    return z;
}

fn xorWith(x: *Block, y: Block) void {
    for (x, 0..) |*v, i| {
        v.* ^= y[i];
    }
}

const hexToBytes = std.fmt.hexToBytes;
const testing = std.testing;

test "AesOcb test vector 1" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    var k: [Aes128Ocb.key_length]u8 = undefined;
    var nonce: [Aes128Ocb.nonce_length]u8 = undefined;
    var tag: [Aes128Ocb.tag_length]u8 = undefined;
    _ = try hexToBytes(&k, "000102030405060708090A0B0C0D0E0F");
    _ = try hexToBytes(&nonce, "BBAA99887766554433221100");

    var c: [0]u8 = undefined;
    Aes128Ocb.encrypt(&c, &tag, "", "", nonce, k);

    var expected_tag: [tag.len]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "785407BFFFC8AD9EDCC5520AC9111EE6");

    var m: [0]u8 = undefined;
    try Aes128Ocb.decrypt(&m, "", tag, "", nonce, k);
}

test "AesOcb test vector 2" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    var k: [Aes128Ocb.key_length]u8 = undefined;
    var nonce: [Aes128Ocb.nonce_length]u8 = undefined;
    var tag: [Aes128Ocb.tag_length]u8 = undefined;
    var ad: [40]u8 = undefined;
    _ = try hexToBytes(&k, "000102030405060708090A0B0C0D0E0F");
    _ = try hexToBytes(&ad, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627");
    _ = try hexToBytes(&nonce, "BBAA9988776655443322110E");

    var c: [0]u8 = undefined;
    Aes128Ocb.encrypt(&c, &tag, "", &ad, nonce, k);

    var expected_tag: [tag.len]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "C5CD9D1850C141E358649994EE701B68");

    try testing.expectEqualSlices(u8, &expected_tag, &tag);
    var m: [0]u8 = undefined;
    try Aes128Ocb.decrypt(&m, &c, tag, &ad, nonce, k);
}

test "AesOcb test vector 3" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    var k: [Aes128Ocb.key_length]u8 = undefined;
    var nonce: [Aes128Ocb.nonce_length]u8 = undefined;
    var tag: [Aes128Ocb.tag_length]u8 = undefined;
    var m: [40]u8 = undefined;
    var c: [m.len]u8 = undefined;
    _ = try hexToBytes(&k, "000102030405060708090A0B0C0D0E0F");
    _ = try hexToBytes(&m, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627");
    _ = try hexToBytes(&nonce, "BBAA9988776655443322110F");

    Aes128Ocb.encrypt(&c, &tag, &m, "", nonce, k);

    var expected_c: [c.len]u8 = undefined;
    var expected_tag: [tag.len]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "479AD363AC366B95A98CA5F3000B1479");
    _ = try hexToBytes(&expected_c, "4412923493C57D5DE0D700F753CCE0D1D2D95060122E9F15A5DDBFC5787E50B5CC55EE507BCB084E");

    try testing.expectEqualSlices(u8, &expected_tag, &tag);
    try testing.expectEqualSlices(u8, &expected_c, &c);
    var m2: [m.len]u8 = undefined;
    try Aes128Ocb.decrypt(&m2, &c, tag, "", nonce, k);
    assert(mem.eql(u8, &m, &m2));
}

test "AesOcb test vector 4" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    var k: [Aes128Ocb.key_length]u8 = undefined;
    var nonce: [Aes128Ocb.nonce_length]u8 = undefined;
    var tag: [Aes128Ocb.tag_length]u8 = undefined;
    var m: [40]u8 = undefined;
    var c: [m.len]u8 = undefined;
    _ = try hexToBytes(&k, "000102030405060708090A0B0C0D0E0F");
    _ = try hexToBytes(&m, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627");
    _ = try hexToBytes(&nonce, "BBAA9988776655443322110D");
    const ad = m;

    Aes128Ocb.encrypt(&c, &tag, &m, &ad, nonce, k);

    var expected_c: [c.len]u8 = undefined;
    var expected_tag: [tag.len]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "ED07BA06A4A69483A7035490C5769E60");
    _ = try hexToBytes(&expected_c, "D5CA91748410C1751FF8A2F618255B68A0A12E093FF454606E59F9C1D0DDC54B65E8628E568BAD7A");

    try testing.expectEqualSlices(u8, &expected_tag, &tag);
    try testing.expectEqualSlices(u8, &expected_c, &c);
    var m2: [m.len]u8 = undefined;
    try Aes128Ocb.decrypt(&m2, &c, tag, &ad, nonce, k);
    assert(mem.eql(u8, &m, &m2));
}

test "AesOcb in-place encryption-decryption" {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;

    var k: [Aes128Ocb.key_length]u8 = undefined;
    var nonce: [Aes128Ocb.nonce_length]u8 = undefined;
    var tag: [Aes128Ocb.tag_length]u8 = undefined;
    var m: [40]u8 = undefined;
    var original_m: [m.len]u8 = undefined;
    _ = try hexToBytes(&k, "000102030405060708090A0B0C0D0E0F");
    _ = try hexToBytes(&m, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627");
    _ = try hexToBytes(&nonce, "BBAA9988776655443322110D");
    const ad = m;

    @memcpy(&original_m, &m);

    Aes128Ocb.encrypt(&m, &tag, &m, &ad, nonce, k);

    var expected_c: [m.len]u8 = undefined;
    var expected_tag: [tag.len]u8 = undefined;
    _ = try hexToBytes(&expected_tag, "ED07BA06A4A69483A7035490C5769E60");
    _ = try hexToBytes(&expected_c, "D5CA91748410C1751FF8A2F618255B68A0A12E093FF454606E59F9C1D0DDC54B65E8628E568BAD7A");

    try testing.expectEqualSlices(u8, &expected_tag, &tag);
    try testing.expectEqualSlices(u8, &expected_c, &m);
    try Aes128Ocb.decrypt(&m, &m, tag, &ad, nonce, k);

    try testing.expectEqualSlices(u8, &original_m, &m);
}



---
File: /std/crypto/aes_siv.zig
---

const std = @import("std");
const assert = std.debug.assert;
const crypto = std.crypto;
const debug = std.debug;
const mem = std.mem;
const math = std.math;
const modes = crypto.core.modes;
const Cmac = @import("cmac.zig").Cmac;
const AuthenticationError = crypto.errors.AuthenticationError;

pub const Aes128Siv = AesSiv(crypto.core.aes.Aes128);
pub const Aes256Siv = AesSiv(crypto.core.aes.Aes256);

/// AES-SIV: Deterministic authenticated encryption - the same message always produces the same ciphertext.
///
/// What it does: Encrypts data and protects it from tampering. Unlike most encryption modes,
/// AES-SIV is deterministic: encrypting the same message with the same key always produces
/// the same ciphertext (unless you provide an optional nonce).
///
/// When to use AES-SIV:
/// - When you need deterministic encryption (e.g., for deduplication in encrypted storage)
/// - When you can't store or generate nonces
/// - For key wrapping (protecting cryptographic keys)
/// - When you need to search encrypted data without decrypting it
///
/// When NOT to use AES-SIV:
/// - When identical plaintexts must produce different ciphertexts (use AES-GCM or AES-GCM-SIV)
/// - For network protocols where replay attacks are a concern
///
/// Unique features:
/// - Optional nonce: You can add a nonce to make encryption non-deterministic, but this is optional
/// - Multiple associated data: Supports a vector of associated data strings instead of just one.
///   The algorithm cryptographically ensures each component is properly separated, preventing
///   canonicalization attacks where different splits of data could be accepted as valid.
///
/// Security properties:
/// - Deterministic: Same input always gives same output (this can leak information about patterns)
/// - Nonce misuse resistant: Doesn't catastrophically fail if you reuse a nonce
/// - Key commitment: Ciphertext can only be decrypted with the exact key that encrypted it
///
/// AES-SIV has better security properties than AES-GCM-SIV, but is must slower.
///
/// How it works: Combines two keys - one for authentication (S2V) and one for encryption (CTR mode).
/// The total key size is double the AES key size (256 bits for AES-128-SIV, 512 bits for AES-256-SIV).
///
/// Defined in RFC 5297.
fn AesSiv(comptime Aes: anytype) type {
    debug.assert(Aes.block.block_length == 16);

    return struct {
        pub const tag_length = 16;
        pub const key_length = Aes.key_bits / 8 * 2; // SIV uses 2x key size

        const CmacImpl = Cmac(Aes);

        /// S2V (String to Vector) - RFC 5297 Section 2.4
        /// Derives a synthetic IV from the key and input strings using CMAC.
        /// This function implements a cryptographic pseudo-random function that maps
        /// a variable-length vector of strings to a fixed 128-bit output.
        fn s2v(iv: *[16]u8, key: [Aes.key_bits / 8]u8, strings: []const []const u8) void {
            assert(strings.len > 0);
            assert(strings.len <= 127); // S2V limitation

            var d: [16]u8 = undefined;

            // Special case: single empty string
            if (strings.len == 1 and strings[0].len == 0) {
                CmacImpl.create(&d, &[_]u8{}, &key);
                iv.* = d;
                return;
            }

            // Initialize with CMAC of zero block
            const zero_block: [16]u8 = @splat(0);
            CmacImpl.create(&d, &zero_block, &key);

            // Process all strings except the last one
            var i: usize = 0;
            while (i < strings.len - 1) : (i += 1) {
                d = dbl(d);
                var tmp: [16]u8 = undefined;
                CmacImpl.create(&tmp, strings[i], &key);
                for (&d, tmp) |*b, t| {
                    b.* ^= t;
                }
            }

            // Process the final string
            const sn = strings[strings.len - 1];
            if (sn.len >= 16) {
                // XOR d with the last 16 bytes of Sn,
                // and give the entire Sn to CMAC incrementally.
                var cmac = CmacImpl.init(&key);
                const prefix = sn.len - 16;
                cmac.update(sn[0..prefix]);

                var tail: [16]u8 = undefined;
                for (&tail, sn[prefix..][0..16], d) |*out, s, db| {
                    out.* = s ^ db;
                }
                cmac.update(&tail);

                cmac.final(iv);
            } else {
                // Pad and XOR
                d = dbl(d);
                var padded: [16]u8 = @splat(0);
                @memcpy(padded[0..sn.len], sn);
                padded[sn.len] = 0x80;
                for (&d, padded) |*b, p| {
                    b.* ^= p;
                }
                CmacImpl.create(iv, &d, &key);
            }
        }

        /// Double operation as defined in RFC 5297.
        /// Performs multiplication by x (i.e., left shift by 1) in GF(2^128).
        /// This is the same operation used in CMAC subkey generation.
        /// If the MSB is set, XORs with the polynomial 0x87 after shifting.
        fn dbl(d: [16]u8) [16]u8 {
            // Read as big-endian 128-bit integer
            const val = mem.readInt(u128, &d, .big);

            // Left shift by 1, and XOR with 0x87 if MSB was set
            const doubled = (val << 1) ^ (0x87 & -%(@as(u128, val >> 127)));

            // Write back as big-endian
            var result: [16]u8 = undefined;
            mem.writeInt(u128, &result, doubled, .big);
            return result;
        }

        /// Encrypt plaintext using AES-SIV
        /// `c`: Output buffer for ciphertext (same size as plaintext)
        /// `tag`: Output buffer for authentication tag (synthetic IV)
        /// `m`: Plaintext to encrypt
        /// `ad`: Optional associated data
        /// `nonce`: Optional nonce (if provided, will be added as last AD component)
        /// `key`: Combined key (2x AES key size)
        pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: ?[]const u8, nonce: ?[]const u8, key: [key_length]u8) void {
            debug.assert(c.len == m.len);

            // Split key into K1 (for S2V) and K2 (for CTR)
            const k1 = key[0 .. Aes.key_bits / 8];
            const k2 = key[Aes.key_bits / 8 ..];

            // Prepare strings for S2V: AD components followed by plaintext
            var strings_buf: [128][]const u8 = undefined;
            var strings_len: usize = 0;

            if (ad) |a| {
                strings_buf[strings_len] = a;
                strings_len += 1;
            }
            if (nonce) |n| {
                strings_buf[strings_len] = n;
                strings_len += 1;
            }
            strings_buf[strings_len] = m;
            strings_len += 1;

            // Compute synthetic IV using S2V
            s2v(tag, k1.*, strings_buf[0..strings_len]);

            // Clear the 31st and 63rd bits for use as CTR IV
            var ctr_iv = tag.*;
            ctr_iv[8] &= 0x7f;
            ctr_iv[12] &= 0x7f;

            // Encrypt plaintext using CTR mode
            const aes_ctx = Aes.initEnc(k2.*);
            modes.ctr(@TypeOf(aes_ctx), aes_ctx, c, m, ctr_iv, .big);
        }

        /// Decrypt ciphertext using AES-SIV
        /// `m`: Output buffer for decrypted plaintext
        /// `c`: Ciphertext to decrypt
        /// `tag`: Authentication tag (synthetic IV)
        /// `ad`: Optional associated data (must match encryption)
        /// `nonce`: Optional nonce (must match encryption)
        /// `key`: Combined key (2x AES key size)
        pub fn decrypt(m: []u8, c: []const u8, tag: [tag_length]u8, ad: ?[]const u8, nonce: ?[]const u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);

            // Split key into K1 (for S2V) and K2 (for CTR)
            const k1 = key[0 .. Aes.key_bits / 8];
            const k2 = key[Aes.key_bits / 8 ..];

            // Clear the 31st and 63rd bits for use as CTR IV
            var ctr_iv = tag;
            ctr_iv[8] &= 0x7f;
            ctr_iv[12] &= 0x7f;

            // Decrypt ciphertext using CTR mode
            const aes_ctx = Aes.initEnc(k2.*);
            modes.ctr(@TypeOf(aes_ctx), aes_ctx, m, c, ctr_iv, .big);

            // Prepare strings for S2V: AD components followed by plaintext
            var strings_buf: [128][]const u8 = undefined;
            var strings_len: usize = 0;

            if (ad) |a| {
                strings_buf[strings_len] = a;
                strings_len += 1;
            }
            if (nonce) |n| {
                strings_buf[strings_len] = n;
                strings_len += 1;
            }
            strings_buf[strings_len] = m;
            strings_len += 1;

            // Verify synthetic IV using S2V
            var computed_tag: [tag_length]u8 = undefined;
            s2v(&computed_tag, k1.*, strings_buf[0..strings_len]);

            // Verify tag
            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
        }

        /// Encrypts plaintext with multiple associated data components.
        /// This is the most general form of AES-SIV encryption that accepts
        /// an arbitrary vector of associated data strings as specified in RFC 5297.
        pub fn encryptWithAdVector(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const []const u8, key: [key_length]u8) void {
            debug.assert(c.len == m.len);

            // Split key into K1 (for S2V) and K2 (for CTR)
            const k1 = key[0 .. Aes.key_bits / 8];
            const k2 = key[Aes.key_bits / 8 ..];

            // Prepare strings for S2V: AD components followed by plaintext
            var strings_buf: [128][]const u8 = undefined;
            var strings_len: usize = 0;

            for (ad) |a| {
                strings_buf[strings_len] = a;
                strings_len += 1;
            }
            strings_buf[strings_len] = m;
            strings_len += 1;

            // Compute synthetic IV using S2V
            s2v(tag, k1.*, strings_buf[0..strings_len]);

            // Clear the 31st and 63rd bits for use as CTR IV
            var ctr_iv = tag.*;
            ctr_iv[8] &= 0x7f;
            ctr_iv[12] &= 0x7f;

            // Encrypt plaintext using CTR mode
            const aes_ctx = Aes.initEnc(k2.*);
            modes.ctr(@TypeOf(aes_ctx), aes_ctx, c, m, ctr_iv, .big);
        }

        /// Decrypts ciphertext with multiple associated data components.
        /// This is the most general form of AES-SIV decryption that accepts
        /// an arbitrary vector of associated data strings as specified in RFC 5297.
        pub fn decryptWithAdVector(m: []u8, c: []const u8, tag: [tag_length]u8, ad: []const []const u8, key: [key_length]u8) AuthenticationError!void {
            assert(c.len == m.len);

            // Split key into K1 (for S2V) and K2 (for CTR)
            const k1 = key[0 .. Aes.key_bits / 8];
            const k2 = key[Aes.key_bits / 8 ..];

            // Clear the 31st and 63rd bits for use as CTR IV
            var ctr_iv = tag;
            ctr_iv[8] &= 0x7f;
            ctr_iv[12] &= 0x7f;

            // Decrypt ciphertext using CTR mode
            const aes_ctx = Aes.initEnc(k2.*);
            modes.ctr(@TypeOf(aes_ctx), aes_ctx, m, c, ctr_iv, .big);

            // Prepare strings for S2V: AD components followed by plaintext
            var strings_buf: [128][]const u8 = undefined;
            var strings_len: usize = 0;

            for (ad) |a| {
                strings_buf[strings_len] = a;
                strings_len += 1;
            }
            strings_buf[strings_len] = m;
            strings_len += 1;

            // Verify synthetic IV using S2V
            var computed_tag: [tag_length]u8 = undefined;
            s2v(&computed_tag, k1.*, strings_buf[0..strings_len]);

            // Verify tag
            const verify = crypto.timing_safe.eql([tag_length]u8, computed_tag, tag);
            if (!verify) {
                crypto.secureZero(u8, &computed_tag);
                @memset(m, undefined);
                return error.AuthenticationFailed;
            }
        }
    };
}

const htest = @import("test.zig");
const testing = std.testing;

test "AES-SIV double operation" {
    const AesSivTest = AesSiv(crypto.core.aes.Aes128);

    // Test vector from RFC 5297
    const input = [_]u8{ 0x0e, 0x04, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e };
    const expected = [_]u8{ 0x1c, 0x08, 0x02, 0x04, 0x06, 0x08, 0x0a, 0x0c, 0x0e, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1a, 0x1c };

    const result = AesSivTest.dbl(input);
    try testing.expectEqualSlices(u8, &expected, &result);
}

test "AES-SIV double operation with MSB set" {
    const AesSivTest = AesSiv(crypto.core.aes.Aes128);

    const input = [_]u8{ 0xe0, 0x40, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xa0, 0xb0, 0xc0, 0xd0, 0xe0 };
    const expected = [_]u8{ 0xc0, 0x80, 0x20, 0x40, 0x60, 0x80, 0xa0, 0xc0, 0xe1, 0x01, 0x21, 0x41, 0x61, 0x81, 0xa1, 0x47 };

    const result = AesSivTest.dbl(input);
    try testing.expectEqualSlices(u8, &expected, &result);
}

test "Aes128Siv - RFC 5297 Test Vector A.1" {
    // Test vector from RFC 5297 Appendix A.1
    const key = [_]u8{
        0xff, 0xfe, 0xfd, 0xfc, 0xfb, 0xfa, 0xf9, 0xf8, 0xf7, 0xf6, 0xf5, 0xf4, 0xf3, 0xf2, 0xf1, 0xf0,
        0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa, 0xfb, 0xfc, 0xfd, 0xfe, 0xff,
    };
    const ad = [_]u8{
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f,
        0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
    };
    const plaintext = [_]u8{
        0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee,
    };

    var ciphertext: [plaintext.len]u8 = undefined;
    var tag: [16]u8 = undefined;

    // Test using vector API for RFC compliance
    const ad_components = [_][]const u8{&ad};
    Aes128Siv.encryptWithAdVector(&ciphertext, &tag, &plaintext, &ad_components, key);

    // Expected values from RFC 5297
    try htest.assertEqual("85632d07c6e8f37f950acd320a2ecc93", &tag);
    try htest.assertEqual("40c02b9690c4dc04daef7f6afe5c", &ciphertext);

    // Test decryption
    var decrypted: [plaintext.len]u8 = undefined;
    try Aes128Siv.decryptWithAdVector(&decrypted, &ciphertext, tag, &ad_components, key);
    try testing.expectEqualSlices(u8, &plaintext, &decrypted);
}

test "Aes128Siv - RFC 5297 Test Vector A.2" {
    // Test vector from RFC 5297 Appendix A.2
    const key: [32]u8 = .{
        0x7f, 0x7e, 0x7d, 0x7c, 0x7b, 0x7a, 0x79, 0x78,
        0x77, 0x76, 0x75, 0x74, 0x73, 0x72, 0x71, 0x70,
        0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,
        0x48, 0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f,
    };
    const ad1 = [_]u8{
        0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,
        0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff,
        0xde, 0xad, 0xda, 0xda, 0xde, 0xad, 0xda, 0xda,
        0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88,
        0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00,
    };
    const ad2 = [_]u8{
        0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80,
        0x90, 0xa0,
    };
    const nonce: [16]u8 = .{
        0x09, 0xf9, 0x11, 0x02, 0x9d, 0x74, 0xe3, 0x5b,
        0xd8, 0x41, 0x56, 0xc5, 0x63, 0x56, 0x88, 0xc0,
    };
    const plaintext = [_]u8{
        0x74, 0x68, 0x69, 0x73, 0x20, 0x69, 0x73, 0x20,
        0x73, 0x6f, 0x6d, 0x65, 0x20, 0x70, 0x6c, 0x61,
        0x69, 0x6e, 0x74, 0x65, 0x78, 0x74, 0x20, 0x74,
        0x6f, 0x20, 0x65, 0x6e, 0x63, 0x72, 0x79, 0x70,
        0x74, 0x20, 0x75, 0x73, 0x69, 0x6e, 0x67, 0x20,
        0x53, 0x49, 0x56, 0x2d, 0x41, 0x45, 0x53,
    };

    var ciphertext: [plaintext.len]u8 = undefined;
    var tag: [16]u8 = undefined;

    Aes128Siv.encryptWithAdVector(&ciphertext, &tag, &plaintext, &.{ &ad1, &ad2, &nonce }, key);

    // Expected values from RFC 5297
    try htest.assertEqual("7bdb6e3b432667eb06f4d14bff2fbd0f", &tag);
    try htest.assertEqual("cb900f2fddbe404326601965c889bf17dba77ceb094fa663b7a3f748ba8af829ea64ad544a272e9c485b62a3fd5c0d", &ciphertext);
}

test "Aes128Siv - empty plaintext" {
    const key: [32]u8 = @splat(0x42);
    const plaintext = "";
    const ad = "additional data";

    var ciphertext: [plaintext.len]u8 = undefined;
    var tag: [16]u8 = undefined;

    Aes128Siv.encrypt(&ciphertext, &tag, plaintext, ad, null, key);

    var decrypted: [plaintext.len]u8 = undefined;
    try Aes128Siv.decrypt(&decrypted, &ciphertext, tag, ad, null, key);
}

test "Aes128Siv - with nonce" {
    const key: [32]u8 = @splat(0x69);
    const nonce: [16]u8 = @splat(0x42);
    const plaintext = "Hello, AES-SIV!";
    const ad = "metadata";

    var ciphertext: [plaintext.len]u8 = undefined;
    var tag: [16]u8 = undefined;

    Aes128Siv.encrypt(&ciphertext, &tag, plaintext, ad, &nonce, key);

    var decrypted: [plaintext.len]u8 = undefined;
    try Aes128Siv.decrypt(&decrypted, &ciphertext, tag, ad, &nonce, key);
    try testing.expectEqualSlices(u8, plaintext, &decrypted);
}

test "Aes256Siv - basic functionality" {
    const key: [64]u8 = @splat(0x96);
    const plaintext = "Test message for AES-256-SIV";
    const ad1 = "header";
    const ad2 = "more data";

    var ciphertext: [plaintext.len]u8 = undefined;
    var tag: [16]u8 = undefined;

    // Test with multiple AD components using the vector API
    const ad_components = [_][]const u8{ ad1, ad2 };
    Aes256Siv.encryptWithAdVector(&ciphertext, &tag, plaintext, &ad_components, key);

    var decrypted: [plaintext.len]u8 = undefined;
    try Aes256Siv.decryptWithAdVector(&decrypted, &ciphertext, tag, &ad_components, key);
    try testing.expectEqualSlices(u8, plaintext, &decrypted);
}

test "Aes128Siv - demonstrating optional parameters" {
    const key: [32]u8 = @splat(0x77);

    // Test 1: No AD, no nonce (pure deterministic)
    {
        const plaintext = "Deterministic encryption";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        Aes128Siv.encrypt(&ciphertext, &tag, plaintext, null, null, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try Aes128Siv.decrypt(&decrypted, &ciphertext, tag, null, null, key);
        try testing.expectEqualSlices(u8, plaintext, &decrypted);
    }

    // Test 2: With AD, no nonce
    {
        const plaintext = "With associated data";
        const ad = "some context";
        var ciphertext: [plaintext.len]u8 = undefined;
        var tag: [16]u8 = undefined;

        Aes128Siv.encrypt(&ciphertext, &tag, plaintext, ad, null, key);

        var decrypted: [plaintext.len]u8 = undefined;
        try Aes128Siv.decrypt(&decrypted, &ciphertext, tag, ad, null, key);
        try testing.expectEqualSlices(u8, plaintext, &decrypted);
    }

    // Test 3: No AD, with nonce
    {
        
```
