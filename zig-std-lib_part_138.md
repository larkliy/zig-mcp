```
internal/cpu/cpu_mips.go#L7
        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/internal/cpu/cpu_mipsle.go#L7
        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/internal/cpu/cpu_mips64x.go#L9
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/sparc/include/asm/cache.h#L14
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/microblaze/include/asm/cache.h#L15
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/sh/include/cpu-sh4/cpu/cache.h#L10
        .arm,
        .armeb,
        .thumb,
        .thumbeb,
        .microblaze,
        .microblazeel,
        .mips,
        .mipsel,
        .mips64,
        .mips64el,
        .sh,
        .sheb,
        .sparc,
        .sparc64,
        => 32,

        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/m68k/include/asm/cache.h#L10
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/openrisc/include/asm/cache.h#L24
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/parisc/include/asm/cache.h#L16
        .hppa,
        .hppa64,
        .m68k,
        .or1k,
        => 16,

        // - https://www.ti.com/lit/pdf/slaa498
        .msp430,
        => 8,

        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/internal/cpu/cpu_s390x.go#L7
        // - https://sxauroratsubasa.sakura.ne.jp/documents/guide/pdfs/Aurora_ISA_guide.pdf
        .s390x,
        .ve,
        => 256,

        // Other x86 and WASM platforms have 64-byte cache lines.
        // The rest of the architectures are assumed to be similar.
        // - https://github.com/golang/go/blob/dda2991c2ea0c5914714469c4defc2562a907230/src/internal/cpu/cpu_x86.go#L9
        // - https://github.com/golang/go/blob/0a9321ad7f8c91e1b0c7184731257df923977eb9/src/internal/cpu/cpu_loong64.go#L11
        // - https://github.com/golang/go/blob/3dd58676054223962cd915bb0934d1f9f489d4d2/src/internal/cpu/cpu_wasm.go#L7
        // - https://github.com/golang/go/blob/19e923182e590ae6568c2c714f20f32512aeb3e3/src/internal/cpu/cpu_riscv64.go#L7
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/xtensa/variants/csp/include/variant/core.h#L209
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/csky/Kconfig#L183
        // - https://github.com/torvalds/linux/blob/3a7e02c040b130b5545e4b115aada7bacd80a2b6/arch/alpha/include/asm/cache.h#L11
        // - https://www.xmos.com/download/The-XMOS-XS3-Architecture.pdf
        else => 64,
    };
}

/// The estimated size of the CPU's cache line when atomically updating memory.
/// Add this much padding or align to this boundary to avoid atomically-updated
/// memory from forcing cache invalidations on near, but non-atomic, memory.
///
/// https://en.wikipedia.org/wiki/False_sharing
/// https://github.com/golang/go/search?q=CacheLinePadSize
pub const cache_line: comptime_int = cacheLineForCpu(builtin.cpu);

test "current CPU has a cache line size" {
    _ = cache_line;
}

/// A lock-free single-owner resource.
pub const Mutex = enum(u8) {
    unlocked,
    locked,

    pub fn tryLock(m: *Mutex) bool {
        return @cmpxchgStrong(Mutex, m, .unlocked, .locked, .acquire, .monotonic) == null;
    }

    pub fn unlock(m: *Mutex) void {
        assert(@atomicLoad(Mutex, m, .unordered) == .locked);
        @atomicStore(Mutex, m, .unlocked, .release);
    }
};



---
File: /std/base64.zig
---

//! Base64 encoding/decoding as specified by
//! [RFC 4648](https://datatracker.ietf.org/doc/html/rfc4648).

const std = @import("std.zig");
const assert = std.debug.assert;
const builtin = @import("builtin");
const testing = std.testing;
const mem = std.mem;
const window = mem.window;

pub const Error = error{
    InvalidCharacter,
    InvalidPadding,
    NoSpaceLeft,
};

const decoderWithIgnoreProto = *const fn (ignore: []const u8) Base64DecoderWithIgnore;

/// Base64 codecs
pub const Codecs = struct {
    alphabet_chars: [64]u8,
    pad_char: ?u8,
    decoderWithIgnore: decoderWithIgnoreProto,
    Encoder: Base64Encoder,
    Decoder: Base64Decoder,
};

/// The Base64 alphabet defined in
/// [RFC 4648 section 4](https://datatracker.ietf.org/doc/html/rfc4648#section-4).
pub const standard_alphabet_chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".*;
fn standardBase64DecoderWithIgnore(ignore: []const u8) Base64DecoderWithIgnore {
    return Base64DecoderWithIgnore.init(standard_alphabet_chars, '=', ignore);
}

/// Standard Base64 codecs, with padding, as defined in
/// [RFC 4648 section 4](https://datatracker.ietf.org/doc/html/rfc4648#section-4).
pub const standard = Codecs{
    .alphabet_chars = standard_alphabet_chars,
    .pad_char = '=',
    .decoderWithIgnore = standardBase64DecoderWithIgnore,
    .Encoder = Base64Encoder.init(standard_alphabet_chars, '='),
    .Decoder = Base64Decoder.init(standard_alphabet_chars, '='),
};

/// Standard Base64 codecs, without padding, as defined in
/// [RFC 4648 section 3.2](https://datatracker.ietf.org/doc/html/rfc4648#section-3.2).
pub const standard_no_pad = Codecs{
    .alphabet_chars = standard_alphabet_chars,
    .pad_char = null,
    .decoderWithIgnore = standardBase64DecoderWithIgnore,
    .Encoder = Base64Encoder.init(standard_alphabet_chars, null),
    .Decoder = Base64Decoder.init(standard_alphabet_chars, null),
};

/// The URL-safe Base64 alphabet defined in
/// [RFC 4648 section 5](https://datatracker.ietf.org/doc/html/rfc4648#section-5).
pub const url_safe_alphabet_chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".*;
fn urlSafeBase64DecoderWithIgnore(ignore: []const u8) Base64DecoderWithIgnore {
    return Base64DecoderWithIgnore.init(url_safe_alphabet_chars, null, ignore);
}

/// URL-safe Base64 codecs, with padding, as defined in
/// [RFC 4648 section 5](https://datatracker.ietf.org/doc/html/rfc4648#section-5).
pub const url_safe = Codecs{
    .alphabet_chars = url_safe_alphabet_chars,
    .pad_char = '=',
    .decoderWithIgnore = urlSafeBase64DecoderWithIgnore,
    .Encoder = Base64Encoder.init(url_safe_alphabet_chars, '='),
    .Decoder = Base64Decoder.init(url_safe_alphabet_chars, '='),
};

/// URL-safe Base64 codecs, without padding, as defined in
/// [RFC 4648 section 3.2](https://datatracker.ietf.org/doc/html/rfc4648#section-3.2).
pub const url_safe_no_pad = Codecs{
    .alphabet_chars = url_safe_alphabet_chars,
    .pad_char = null,
    .decoderWithIgnore = urlSafeBase64DecoderWithIgnore,
    .Encoder = Base64Encoder.init(url_safe_alphabet_chars, null),
    .Decoder = Base64Decoder.init(url_safe_alphabet_chars, null),
};

pub const Base64Encoder = struct {
    alphabet_chars: [64]u8,
    pad_char: ?u8,

    /// A bunch of assertions, then simply pass the data right through.
    pub fn init(alphabet_chars: [64]u8, pad_char: ?u8) Base64Encoder {
        assert(alphabet_chars.len == 64);
        var char_in_alphabet = [_]bool{false} ** 256;
        for (alphabet_chars) |c| {
            assert(!char_in_alphabet[c]);
            assert(pad_char == null or c != pad_char.?);
            char_in_alphabet[c] = true;
        }
        return Base64Encoder{
            .alphabet_chars = alphabet_chars,
            .pad_char = pad_char,
        };
    }

    /// Compute the encoded length
    pub fn calcSize(encoder: *const Base64Encoder, source_len: usize) usize {
        if (encoder.pad_char != null) {
            return @divTrunc(source_len + 2, 3) * 4;
        } else {
            const leftover = source_len % 3;
            return @divTrunc(source_len, 3) * 4 + @divTrunc(leftover * 4 + 2, 3);
        }
    }

    pub fn encodeWriter(encoder: *const Base64Encoder, dest: *std.Io.Writer, source: []const u8) !void {
        var chunker = window(u8, source, 3, 3);
        while (chunker.next()) |chunk| {
            var temp: [5]u8 = undefined;
            const s = encoder.encode(&temp, chunk);
            try dest.writeAll(s);
        }
    }

    /// dest.len must at least be what you get from ::calcSize.
    pub fn encode(encoder: *const Base64Encoder, dest: []u8, source: []const u8) []const u8 {
        const out_len = encoder.calcSize(source.len);
        assert(dest.len >= out_len);

        var idx: usize = 0;
        var out_idx: usize = 0;
        while (idx + 15 < source.len) : (idx += 12) {
            const bits = std.mem.readInt(u128, source[idx..][0..16], .big);
            inline for (0..16) |i| {
                dest[out_idx + i] = encoder.alphabet_chars[@truncate((bits >> (122 - i * 6)) & 0x3f)];
            }
            out_idx += 16;
        }
        while (idx + 3 < source.len) : (idx += 3) {
            const bits = std.mem.readInt(u32, source[idx..][0..4], .big);
            dest[out_idx] = encoder.alphabet_chars[(bits >> 26) & 0x3f];
            dest[out_idx + 1] = encoder.alphabet_chars[(bits >> 20) & 0x3f];
            dest[out_idx + 2] = encoder.alphabet_chars[(bits >> 14) & 0x3f];
            dest[out_idx + 3] = encoder.alphabet_chars[(bits >> 8) & 0x3f];
            out_idx += 4;
        }
        if (idx + 2 < source.len) {
            dest[out_idx] = encoder.alphabet_chars[source[idx] >> 2];
            dest[out_idx + 1] = encoder.alphabet_chars[((source[idx] & 0x3) << 4) | (source[idx + 1] >> 4)];
            dest[out_idx + 2] = encoder.alphabet_chars[(source[idx + 1] & 0xf) << 2 | (source[idx + 2] >> 6)];
            dest[out_idx + 3] = encoder.alphabet_chars[source[idx + 2] & 0x3f];
            out_idx += 4;
        } else if (idx + 1 < source.len) {
            dest[out_idx] = encoder.alphabet_chars[source[idx] >> 2];
            dest[out_idx + 1] = encoder.alphabet_chars[((source[idx] & 0x3) << 4) | (source[idx + 1] >> 4)];
            dest[out_idx + 2] = encoder.alphabet_chars[(source[idx + 1] & 0xf) << 2];
            out_idx += 3;
        } else if (idx < source.len) {
            dest[out_idx] = encoder.alphabet_chars[source[idx] >> 2];
            dest[out_idx + 1] = encoder.alphabet_chars[(source[idx] & 0x3) << 4];
            out_idx += 2;
        }
        if (encoder.pad_char) |pad_char| {
            for (dest[out_idx..out_len]) |*pad| {
                pad.* = pad_char;
            }
        }
        return dest[0..out_len];
    }
};

pub const Base64Decoder = struct {
    const invalid_char: u8 = 0xff;
    const invalid_char_tst: u32 = 0xff000000;

    /// e.g. 'A' => 0.
    /// `invalid_char` for any value not in the 64 alphabet chars.
    char_to_index: [256]u8,
    fast_char_to_index: [4][256]u32,
    pad_char: ?u8,

    pub fn init(alphabet_chars: [64]u8, pad_char: ?u8) Base64Decoder {
        var result = Base64Decoder{
            .char_to_index = [_]u8{invalid_char} ** 256,
            .fast_char_to_index = .{[_]u32{invalid_char_tst} ** 256} ** 4,
            .pad_char = pad_char,
        };

        var char_in_alphabet = [_]bool{false} ** 256;
        for (alphabet_chars, 0..) |c, i| {
            assert(!char_in_alphabet[c]);
            assert(pad_char == null or c != pad_char.?);

            const ci = @as(u32, @intCast(i));
            result.fast_char_to_index[0][c] = ci << 2;
            result.fast_char_to_index[1][c] = (ci >> 4) | ((ci & 0x0f) << 12);
            result.fast_char_to_index[2][c] = ((ci & 0x3) << 22) | ((ci & 0x3c) << 6);
            result.fast_char_to_index[3][c] = ci << 16;

            result.char_to_index[c] = @as(u8, @intCast(i));
            char_in_alphabet[c] = true;
        }
        return result;
    }

    /// Return the maximum possible decoded size for a given input length - The actual length may be less if the input includes padding.
    /// `InvalidPadding` is returned if the input length is not valid.
    pub fn calcSizeUpperBound(decoder: *const Base64Decoder, source_len: usize) Error!usize {
        var result = source_len / 4 * 3;
        const leftover = source_len % 4;
        if (decoder.pad_char != null) {
            if (leftover % 4 != 0) return error.InvalidPadding;
        } else {
            if (leftover % 4 == 1) return error.InvalidPadding;
            result += leftover * 3 / 4;
        }
        return result;
    }

    /// Return the exact decoded size for a slice.
    /// `InvalidPadding` is returned if the input length is not valid.
    pub fn calcSizeForSlice(decoder: *const Base64Decoder, source: []const u8) Error!usize {
        const source_len = source.len;
        var result = try decoder.calcSizeUpperBound(source_len);
        if (decoder.pad_char) |pad_char| {
            if (source_len >= 1 and source[source_len - 1] == pad_char) result -= 1;
            if (source_len >= 2 and source[source_len - 2] == pad_char) result -= 1;
        }
        return result;
    }

    /// dest.len must be what you get from ::calcSize.
    /// Invalid characters result in `error.InvalidCharacter`.
    /// Invalid padding results in `error.InvalidPadding`.
    pub fn decode(decoder: *const Base64Decoder, dest: []u8, source: []const u8) Error!void {
        if (decoder.pad_char != null and source.len % 4 != 0) return error.InvalidPadding;
        var dest_idx: usize = 0;
        var fast_src_idx: usize = 0;
        var acc: u12 = 0;
        var acc_len: u4 = 0;
        var leftover_idx: ?usize = null;
        while (fast_src_idx + 16 < source.len and dest_idx + 15 < dest.len) : ({
            fast_src_idx += 16;
            dest_idx += 12;
        }) {
            var bits: u128 = 0;
            inline for (0..4) |i| {
                var new_bits: u128 = decoder.fast_char_to_index[0][source[fast_src_idx + i * 4]];
                new_bits |= decoder.fast_char_to_index[1][source[fast_src_idx + 1 + i * 4]];
                new_bits |= decoder.fast_char_to_index[2][source[fast_src_idx + 2 + i * 4]];
                new_bits |= decoder.fast_char_to_index[3][source[fast_src_idx + 3 + i * 4]];
                if ((new_bits & invalid_char_tst) != 0) return error.InvalidCharacter;
                bits |= (new_bits << (24 * i));
            }
            std.mem.writeInt(u128, dest[dest_idx..][0..16], bits, .little);
        }
        while (fast_src_idx + 4 < source.len and dest_idx + 3 < dest.len) : ({
            fast_src_idx += 4;
            dest_idx += 3;
        }) {
            var bits = decoder.fast_char_to_index[0][source[fast_src_idx]];
            bits |= decoder.fast_char_to_index[1][source[fast_src_idx + 1]];
            bits |= decoder.fast_char_to_index[2][source[fast_src_idx + 2]];
            bits |= decoder.fast_char_to_index[3][source[fast_src_idx + 3]];
            if ((bits & invalid_char_tst) != 0) return error.InvalidCharacter;
            std.mem.writeInt(u32, dest[dest_idx..][0..4], bits, .little);
        }
        const remaining = source[fast_src_idx..];
        for (remaining, fast_src_idx..) |c, src_idx| {
            const d = decoder.char_to_index[c];
            if (d == invalid_char) {
                if (decoder.pad_char == null or c != decoder.pad_char.?) return error.InvalidCharacter;
                leftover_idx = src_idx;
                break;
            }
            acc = (acc << 6) + d;
            acc_len += 6;
            if (acc_len >= 8) {
                acc_len -= 8;
                dest[dest_idx] = @as(u8, @truncate(acc >> acc_len));
                dest_idx += 1;
            }
        }
        if (acc_len > 4 or (acc & (@as(u12, 1) << acc_len) - 1) != 0) {
            return error.InvalidPadding;
        }
        if (leftover_idx == null) return;
        const leftover = source[leftover_idx.?..];
        if (decoder.pad_char) |pad_char| {
            const padding_len = acc_len / 2;
            var padding_chars: usize = 0;
            for (leftover) |c| {
                if (c != pad_char) {
                    return if (c == Base64Decoder.invalid_char) error.InvalidCharacter else error.InvalidPadding;
                }
                padding_chars += 1;
            }
            if (padding_chars != padding_len) return error.InvalidPadding;
        }
    }
};

pub const Base64DecoderWithIgnore = struct {
    decoder: Base64Decoder,
    char_is_ignored: [256]bool,

    pub fn init(alphabet_chars: [64]u8, pad_char: ?u8, ignore_chars: []const u8) Base64DecoderWithIgnore {
        var result = Base64DecoderWithIgnore{
            .decoder = Base64Decoder.init(alphabet_chars, pad_char),
            .char_is_ignored = [_]bool{false} ** 256,
        };
        for (ignore_chars) |c| {
            assert(result.decoder.char_to_index[c] == Base64Decoder.invalid_char);
            assert(!result.char_is_ignored[c]);
            assert(result.decoder.pad_char != c);
            result.char_is_ignored[c] = true;
        }
        return result;
    }

    /// Return the maximum possible decoded size for a given input length - The actual length may be
    /// less if the input includes padding or ignored characters.
    pub fn calcSizeUpperBound(decoder_with_ignore: *const Base64DecoderWithIgnore, source_len: usize) usize {
        var result = source_len / 4 * 3;
        if (decoder_with_ignore.decoder.pad_char == null) {
            const leftover = source_len % 4;
            result += leftover * 3 / 4;
        }
        return result;
    }

    /// Invalid characters that are not ignored result in error.InvalidCharacter.
    /// Invalid padding results in error.InvalidPadding.
    /// Decoding more data than can fit in dest results in error.NoSpaceLeft. See also ::calcSizeUpperBound.
    /// Returns the number of bytes written to dest.
    pub fn decode(decoder_with_ignore: *const Base64DecoderWithIgnore, dest: []u8, source: []const u8) Error!usize {
        const decoder = &decoder_with_ignore.decoder;
        var acc: u12 = 0;
        var acc_len: u4 = 0;
        var dest_idx: usize = 0;
        var leftover_idx: ?usize = null;
        for (source, 0..) |c, src_idx| {
            if (decoder_with_ignore.char_is_ignored[c]) continue;
            const d = decoder.char_to_index[c];
            if (d == Base64Decoder.invalid_char) {
                if (decoder.pad_char == null or c != decoder.pad_char.?) return error.InvalidCharacter;
                leftover_idx = src_idx;
                break;
            }
            acc = (acc << 6) + d;
            acc_len += 6;
            if (acc_len >= 8) {
                if (dest_idx == dest.len) return error.NoSpaceLeft;
                acc_len -= 8;
                dest[dest_idx] = @as(u8, @truncate(acc >> acc_len));
                dest_idx += 1;
            }
        }
        if (acc_len > 4 or (acc & (@as(u12, 1) << acc_len) - 1) != 0) {
            return error.InvalidPadding;
        }
        const padding_len = acc_len / 2;
        if (leftover_idx == null) {
            if (decoder.pad_char != null and padding_len != 0) return error.InvalidPadding;
            return dest_idx;
        }
        const leftover = source[leftover_idx.?..];
        if (decoder.pad_char) |pad_char| {
            var padding_chars: usize = 0;
            for (leftover) |c| {
                if (decoder_with_ignore.char_is_ignored[c]) continue;
                if (c != pad_char) {
                    return if (c == Base64Decoder.invalid_char) error.InvalidCharacter else error.InvalidPadding;
                }
                padding_chars += 1;
            }
            if (padding_chars != padding_len) return error.InvalidPadding;
        }
        return dest_idx;
    }
};

test "base64" {
    @setEvalBranchQuota(8000);
    try testBase64();
    try comptime testAllApis(standard, "comptime", "Y29tcHRpbWU=");
}

test "base64 padding dest overflow" {
    const input = "foo";

    var expect: [128]u8 = undefined;
    @memset(&expect, 0);
    _ = url_safe.Encoder.encode(expect[0..url_safe.Encoder.calcSize(input.len)], input);

    var got: [128]u8 = undefined;
    @memset(&got, 0);
    _ = url_safe.Encoder.encode(&got, input);

    try std.testing.expectEqualSlices(u8, &expect, &got);
}

test "base64 url_safe_no_pad" {
    @setEvalBranchQuota(8000);
    try testBase64UrlSafeNoPad();
    try comptime testAllApis(url_safe_no_pad, "comptime", "Y29tcHRpbWU");
}

fn testBase64() !void {
    const codecs = standard;

    try testAllApis(codecs, "", "");
    try testAllApis(codecs, "f", "Zg==");
    try testAllApis(codecs, "fo", "Zm8=");
    try testAllApis(codecs, "foo", "Zm9v");
    try testAllApis(codecs, "foob", "Zm9vYg==");
    try testAllApis(codecs, "fooba", "Zm9vYmE=");
    try testAllApis(codecs, "foobar", "Zm9vYmFy");
    try testAllApis(codecs, "foobarfoobarfoo", "Zm9vYmFyZm9vYmFyZm9v");
    try testAllApis(codecs, "foobarfoobarfoob", "Zm9vYmFyZm9vYmFyZm9vYg==");
    try testAllApis(codecs, "foobarfoobarfooba", "Zm9vYmFyZm9vYmFyZm9vYmE=");
    try testAllApis(codecs, "foobarfoobarfoobar", "Zm9vYmFyZm9vYmFyZm9vYmFy");

    try testDecodeIgnoreSpace(codecs, "", " ");
    try testDecodeIgnoreSpace(codecs, "f", "Z g= =");
    try testDecodeIgnoreSpace(codecs, "fo", "    Zm8=");
    try testDecodeIgnoreSpace(codecs, "foo", "Zm9v    ");
    try testDecodeIgnoreSpace(codecs, "foob", "Zm9vYg = = ");
    try testDecodeIgnoreSpace(codecs, "fooba", "Zm9v YmE=");
    try testDecodeIgnoreSpace(codecs, "foobar", " Z m 9 v Y m F y ");

    // test getting some api errors
    try testError(codecs, "A", error.InvalidPadding);
    try testError(codecs, "AA", error.InvalidPadding);
    try testError(codecs, "AAA", error.InvalidPadding);
    try testError(codecs, "A..A", error.InvalidCharacter);
    try testError(codecs, "AA=A", error.InvalidPadding);
    try testError(codecs, "AA/=", error.InvalidPadding);
    try testError(codecs, "A/==", error.InvalidPadding);
    try testError(codecs, "A===", error.InvalidPadding);
    try testError(codecs, "====", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vYmFyA..A", error.InvalidCharacter);
    try testError(codecs, "Zm9vYmFyZm9vYmFyAA=A", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vYmFyAA/=", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vYmFyA/==", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vYmFyA===", error.InvalidPadding);
    try testError(codecs, "A..AZm9vYmFyZm9vYmFy", error.InvalidCharacter);
    try testError(codecs, "Zm9vYmFyZm9vAA=A", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vAA/=", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vA/==", error.InvalidPadding);
    try testError(codecs, "Zm9vYmFyZm9vA===", error.InvalidPadding);

    try testNoSpaceLeftError(codecs, "AA==");
    try testNoSpaceLeftError(codecs, "AAA=");
    try testNoSpaceLeftError(codecs, "AAAA");
    try testNoSpaceLeftError(codecs, "AAAAAA==");

    try testFourBytesDestNoSpaceLeftError(codecs, "AAAAAAAAAAAAAAAA");
}

fn testBase64UrlSafeNoPad() !void {
    const codecs = url_safe_no_pad;

    try testAllApis(codecs, "", "");
    try testAllApis(codecs, "f", "Zg");
    try testAllApis(codecs, "fo", "Zm8");
    try testAllApis(codecs, "foo", "Zm9v");
    try testAllApis(codecs, "foob", "Zm9vYg");
    try testAllApis(codecs, "fooba", "Zm9vYmE");
    try testAllApis(codecs, "foobar", "Zm9vYmFy");
    try testAllApis(codecs, "foobarfoobarfoobar", "Zm9vYmFyZm9vYmFyZm9vYmFy");

    try testDecodeIgnoreSpace(codecs, "", " ");
    try testDecodeIgnoreSpace(codecs, "f", "Z g ");
    try testDecodeIgnoreSpace(codecs, "fo", "    Zm8");
    try testDecodeIgnoreSpace(codecs, "foo", "Zm9v    ");
    try testDecodeIgnoreSpace(codecs, "foob", "Zm9vYg   ");
    try testDecodeIgnoreSpace(codecs, "fooba", "Zm9v YmE");
    try testDecodeIgnoreSpace(codecs, "foobar", " Z m 9 v Y m F y ");

    // test getting some api errors
    try testError(codecs, "A", error.InvalidPadding);
    try testError(codecs, "AAA=", error.InvalidCharacter);
    try testError(codecs, "A..A", error.InvalidCharacter);
    try testError(codecs, "AA=A", error.InvalidCharacter);
    try testError(codecs, "AA/=", error.InvalidCharacter);
    try testError(codecs, "A/==", error.InvalidCharacter);
    try testError(codecs, "A===", error.InvalidCharacter);
    try testError(codecs, "====", error.InvalidCharacter);
    try testError(codecs, "Zm9vYmFyZm9vYmFyA..A", error.InvalidCharacter);
    try testError(codecs, "A..AZm9vYmFyZm9vYmFy", error.InvalidCharacter);

    try testNoSpaceLeftError(codecs, "AA");
    try testNoSpaceLeftError(codecs, "AAA");
    try testNoSpaceLeftError(codecs, "AAAA");
    try testNoSpaceLeftError(codecs, "AAAAAA");

    try testFourBytesDestNoSpaceLeftError(codecs, "AAAAAAAAAAAAAAAA");
}

fn testAllApis(codecs: Codecs, expected_decoded: []const u8, expected_encoded: []const u8) !void {
    // Base64Encoder
    {
        // raw encode
        var buffer: [0x100]u8 = undefined;
        const encoded = codecs.Encoder.encode(&buffer, expected_decoded);
        try testing.expectEqualSlices(u8, expected_encoded, encoded);
    }
    {
        // stream encode
        var buffer: [0x100]u8 = undefined;
        var writer: std.Io.Writer = .fixed(&buffer);
        try codecs.Encoder.encodeWriter(&writer, expected_decoded);
        try testing.expectEqualSlices(u8, expected_encoded, writer.buffered());
    }

    // Base64Decoder
    {
        var buffer: [0x100]u8 = undefined;
        const decoded = buffer[0..try codecs.Decoder.calcSizeForSlice(expected_encoded)];
        try codecs.Decoder.decode(decoded, expected_encoded);
        try testing.expectEqualSlices(u8, expected_decoded, decoded);
    }

    // Base64DecoderWithIgnore
    {
        const decoder_ignore_nothing = codecs.decoderWithIgnore("");
        var buffer: [0x100]u8 = undefined;
        const decoded = buffer[0..decoder_ignore_nothing.calcSizeUpperBound(expected_encoded.len)];
        const written = try decoder_ignore_nothing.decode(decoded, expected_encoded);
        try testing.expect(written <= decoded.len);
        try testing.expectEqualSlices(u8, expected_decoded, decoded[0..written]);
    }
}

fn testDecodeIgnoreSpace(codecs: Codecs, expected_decoded: []const u8, encoded: []const u8) !void {
    const decoder_ignore_space = codecs.decoderWithIgnore(" ");
    var buffer: [0x100]u8 = undefined;
    const decoded = buffer[0..decoder_ignore_space.calcSizeUpperBound(encoded.len)];
    const written = try decoder_ignore_space.decode(decoded, encoded);
    try testing.expectEqualSlices(u8, expected_decoded, decoded[0..written]);
}

fn testError(codecs: Codecs, encoded: []const u8, expected_err: anyerror) !void {
    const decoder_ignore_space = codecs.decoderWithIgnore(" ");
    var buffer: [0x100]u8 = undefined;
    if (codecs.Decoder.calcSizeForSlice(encoded)) |decoded_size| {
        const decoded = buffer[0..decoded_size];
        if (codecs.Decoder.decode(decoded, encoded)) |_| {
            return error.ExpectedError;
        } else |err| if (err != expected_err) return err;
    } else |err| if (err != expected_err) return err;

    if (decoder_ignore_space.decode(buffer[0..], encoded)) |_| {
        return error.ExpectedError;
    } else |err| if (err != expected_err) return err;
}

fn testNoSpaceLeftError(codecs: Codecs, encoded: []const u8) !void {
    const decoder_ignore_space = codecs.decoderWithIgnore(" ");
    var buffer: [0x100]u8 = undefined;
    const decoded = buffer[0 .. (try codecs.Decoder.calcSizeForSlice(encoded)) - 1];
    if (decoder_ignore_space.decode(decoded, encoded)) |_| {
        return error.ExpectedError;
    } else |err| if (err != error.NoSpaceLeft) return err;
}

fn testFourBytesDestNoSpaceLeftError(codecs: Codecs, encoded: []const u8) !void {
    const decoder_ignore_space = codecs.decoderWithIgnore(" ");
    var buffer: [0x100]u8 = undefined;
    const decoded = buffer[0..4];
    if (decoder_ignore_space.decode(decoded, encoded)) |_| {
        return error.ExpectedError;
    } else |err| if (err != error.NoSpaceLeft) return err;
}



---
File: /std/bit_set.zig
---

//! This file defines several variants of bit sets.  A bit set
//! is a densely stored set of integers with a known maximum,
//! in which each integer gets a single bit.  Bit sets have very
//! fast presence checks, update operations, and union and intersection
//! operations.  However, if the number of possible items is very
//! large and the number of actual items in a given set is usually
//! small, they may be less memory efficient than an array set.
//!
//! There are five variants defined here:
//!
//! IntegerBitSet:
//!   A bit set with static size, which is backed by a single integer.
//!   This set is good for sets with a small size, but may generate
//!   inefficient code for larger sets, especially in debug mode.
//!
//! ArrayBitSet:
//!   A bit set with static size, which is backed by an array of usize.
//!   This set is good for sets with a larger size, but may use
//!   more bytes than necessary if your set is small.
//!
//! StaticBitSet:
//!   Picks either IntegerBitSet or ArrayBitSet depending on the requested
//!   size.  The interfaces of these two types match exactly, except for fields.
//!
//! DynamicBitSet:
//!   A bit set with runtime-known size, backed by an allocated slice
//!   of usize.
//!
//! DynamicBitSetUnmanaged:
//!   A variant of DynamicBitSet which does not store a pointer to its
//!   allocator, in order to save space.

const std = @import("std.zig");
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const builtin = @import("builtin");

/// Returns the optimal static bit set type for the specified number
/// of elements: either `IntegerBitSet` or `ArrayBitSet`,
/// both of which fulfill the same interface.
/// The returned type will perform no allocations,
/// can be copied by value, and does not require deinitialization.
pub fn StaticBitSet(comptime size: usize) type {
    if (size <= @bitSizeOf(usize)) {
        return IntegerBitSet(size);
    } else {
        return ArrayBitSet(usize, size);
    }
}

/// A bit set with static size, which is backed by a single integer.
/// This set is good for sets with a small size, but may generate
/// inefficient code for larger sets, especially in debug mode.
pub fn IntegerBitSet(comptime size: u16) type {
    return packed struct(MaskInt) {
        const Self = @This();

        // TODO: Make this a comptime field once those are fixed
        /// The number of items in this bit set
        pub const bit_length: usize = size;

        /// The integer type used to represent a mask in this bit set
        pub const MaskInt = std.meta.Int(.unsigned, size);

        /// The integer type used to shift a mask in this bit set
        pub const ShiftInt = std.math.Log2Int(MaskInt);

        /// The bit mask, as a single integer
        mask: MaskInt,

        /// Deprecated: use `.empty`.
        /// Creates a bit set with no elements present.
        pub fn initEmpty() Self {
            return .{ .mask = 0 };
        }

        /// Deprecated: use `.full`.
        /// Creates a bit set with all elements present.
        pub fn initFull() Self {
            return .{ .mask = ~@as(MaskInt, 0) };
        }

        /// A bit set with no elements present.
        pub const empty: Self = .{ .mask = 0 };

        /// A bit set with all elements present.
        pub const full: Self = .{ .mask = ~@as(MaskInt, 0) };

        /// Returns the number of bits in this bit set
        pub inline fn capacity(self: Self) usize {
            _ = self;
            return bit_length;
        }

        /// Returns true if the bit at the specified index
        /// is present in the set, false otherwise.
        pub fn isSet(self: Self, index: usize) bool {
            assert(index < bit_length);
            return (self.mask & maskBit(index)) != 0;
        }

        /// Returns the total number of set bits in this bit set.
        pub fn count(self: Self) usize {
            return @popCount(self.mask);
        }

        /// Changes the value of the specified bit of the bit
        /// set to match the passed boolean.
        pub fn setValue(self: *Self, index: usize, value: bool) void {
            assert(index < bit_length);
            if (MaskInt == u0) return;
            const bit = maskBit(index);
            const new_bit = bit & std.math.boolMask(MaskInt, value);
            self.mask = (self.mask & ~bit) | new_bit;
        }

        /// Adds a specific bit to the bit set
        pub fn set(self: *Self, index: usize) void {
            assert(index < bit_length);
            self.mask |= maskBit(index);
        }

        /// Changes the value of all bits in the specified range to
        /// match the passed boolean.
        pub fn setRangeValue(self: *Self, range: Range, value: bool) void {
            assert(range.end <= bit_length);
            assert(range.start <= range.end);
            if (range.start == range.end) return;
            if (MaskInt == u0) return;

            const start_bit = @as(ShiftInt, @intCast(range.start));

            var mask = std.math.boolMask(MaskInt, true) << start_bit;
            if (range.end != bit_length) {
                const end_bit = @as(ShiftInt, @intCast(range.end));
                mask &= std.math.boolMask(MaskInt, true) >> @as(ShiftInt, @truncate(@as(usize, @bitSizeOf(MaskInt)) - @as(usize, end_bit)));
            }
            self.mask &= ~mask;

            mask = std.math.boolMask(MaskInt, value) << start_bit;
            if (range.end != bit_length) {
                const end_bit = @as(ShiftInt, @intCast(range.end));
                mask &= std.math.boolMask(MaskInt, value) >> @as(ShiftInt, @truncate(@as(usize, @bitSizeOf(MaskInt)) - @as(usize, end_bit)));
            }
            self.mask |= mask;
        }

        /// Removes a specific bit from the bit set
        pub fn unset(self: *Self, index: usize) void {
            assert(index < bit_length);
            // Workaround for #7953
            if (MaskInt == u0) return;
            self.mask &= ~maskBit(index);
        }

        /// Flips a specific bit in the bit set
        pub fn toggle(self: *Self, index: usize) void {
            assert(index < bit_length);
            self.mask ^= maskBit(index);
        }

        /// Flips all bits in this bit set which are present
        /// in the toggles bit set.
        pub fn toggleSet(self: *Self, toggles: Self) void {
            self.mask ^= toggles.mask;
        }

        /// Flips every bit in the bit set.
        pub fn toggleAll(self: *Self) void {
            self.mask = ~self.mask;
        }

        /// Performs a union of two bit sets, and stores the
        /// result in the first one.  Bits in the result are
        /// set if the corresponding bits were set in either input.
        pub fn setUnion(self: *Self, other: Self) void {
            self.mask |= other.mask;
        }

        /// Performs an intersection of two bit sets, and stores
        /// the result in the first one.  Bits in the result are
        /// set if the corresponding bits were set in both inputs.
        pub fn setIntersection(self: *Self, other: Self) void {
            self.mask &= other.mask;
        }

        /// Finds the index of the first set bit.
        /// If no bits are set, returns null.
        pub fn findFirstSet(self: Self) ?usize {
            const mask = self.mask;
            if (mask == 0) return null;
            return @ctz(mask);
        }

        /// Finds the index of the last set bit.
        /// If no bits are set, returns null.
        pub fn findLastSet(self: Self) ?usize {
            const mask = self.mask;
            if (mask == 0) return null;
            return bit_length - @clz(mask) - 1;
        }

        /// Finds the index of the first set bit, and unsets it.
        /// If no bits are set, returns null.
        pub fn toggleFirstSet(self: *Self) ?usize {
            const mask = self.mask;
            if (mask == 0) return null;
            const index = @ctz(mask);
            self.mask = mask & (mask - 1);
            return index;
        }

        /// Returns true iff every corresponding bit in both
        /// bit sets are the same.
        pub fn eql(self: Self, other: Self) bool {
            return bit_length == 0 or self.mask == other.mask;
        }

        /// Returns true iff the first bit set is the subset
        /// of the second one.
        pub fn subsetOf(self: Self, other: Self) bool {
            return self.intersectWith(other).eql(self);
        }

        /// Returns true iff the first bit set is the superset
        /// of the second one.
        pub fn supersetOf(self: Self, other: Self) bool {
            return other.subsetOf(self);
        }

        /// Returns the complement bit sets. Bits in the result
        /// are set if the corresponding bits were not set.
        pub fn complement(self: Self) Self {
            var result = self;
            result.toggleAll();
            return result;
        }

        /// Returns the union of two bit sets. Bits in the
        /// result are set if the corresponding bits were set
        /// in either input.
        pub fn unionWith(self: Self, other: Self) Self {
            var result = self;
            result.setUnion(other);
            return result;
        }

        /// Returns the intersection of two bit sets. Bits in
        /// the result are set if the corresponding bits were
        /// set in both inputs.
        pub fn intersectWith(self: Self, other: Self) Self {
            var result = self;
            result.setIntersection(other);
            return result;
        }

        /// Returns the xor of two bit sets. Bits in the
        /// result are set if the corresponding bits were
        /// not the same in both inputs.
        pub fn xorWith(self: Self, other: Self) Self {
            var result = self;
            result.toggleSet(other);
            return result;
        }

        /// Returns the difference of two bit sets. Bits in
        /// the result are set if set in the first but not
        /// set in the second set.
        pub fn differenceWith(self: Self, other: Self) Self {
            var result = self;
            result.setIntersection(other.complement());
            return result;
        }

        /// Iterates through the items in the set, according to the options.
        /// The default options (.{}) will iterate indices of set bits in
        /// ascending order.  Modifications to the underlying bit set may
        /// or may not be observed by the iterator.
        pub fn iterator(self: *const Self, comptime options: IteratorOptions) Iterator(options) {
            return .{
                .bits_remain = switch (options.kind) {
                    .set => self.mask,
                    .unset => ~self.mask,
                },
            };
        }

        pub fn Iterator(comptime options: IteratorOptions) type {
            return SingleWordIterator(options.direction);
        }

        fn SingleWordIterator(comptime direction: IteratorOptions.Direction) type {
            return struct {
                const IterSelf = @This();
                // all bits which have not yet been iterated over
                bits_remain: MaskInt,

                /// Returns the index of the next unvisited set bit
                /// in the bit set, in ascending order.
                pub fn next(self: *IterSelf) ?usize {
                    if (self.bits_remain == 0) return null;

                    switch (direction) {
                        .forward => {
                            const next_index = @ctz(self.bits_remain);
                            self.bits_remain &= self.bits_remain - 1;
                            return next_index;
                        },
                        .reverse => {
                            const leading_zeroes = @clz(self.bits_remain);
                            const top_bit = (@bitSizeOf(MaskInt) - 1) - leading_zeroes;
                            self.bits_remain &= (@as(MaskInt, 1) << @as(ShiftInt, @intCast(top_bit))) - 1;
                            return top_bit;
                        },
                    }
                }
            };
        }

        fn maskBit(index: usize) MaskInt {
            if (MaskInt == u0) return 0;
            return @as(MaskInt, 1) << @as(ShiftInt, @intCast(index));
        }
        fn boolMaskBit(index: usize, value: bool) MaskInt {
            if (MaskInt == u0) return 0;
            return @as(MaskInt, @intFromBool(value)) << @as(ShiftInt, @intCast(index));
        }
    };
}

/// A bit set with static size, which is backed by an array of usize.
/// This set is good for sets with a larger size, but may use
/// more bytes than necessary if your set is small.
pub fn ArrayBitSet(comptime MaskIntType: type, comptime size: usize) type {
    const mask_info: std.builtin.Type = @typeInfo(MaskIntType);

    // Make sure the mask int is indeed an int
    if (mask_info != .int) @compileError("ArrayBitSet can only operate on integer masks, but was passed " ++ @typeName(MaskIntType));

    // It must also be unsigned.
    if (mask_info.int.signedness != .unsigned) @compileError("ArrayBitSet requires an unsigned integer mask type, but was passed " ++ @typeName(MaskIntType));

    // And it must not be empty.
    if (MaskIntType == u0)
        @compileError("ArrayBitSet requires a sized integer for its mask int.  u0 does not work.");

    const byte_size = std.mem.byte_size_in_bits;

    // We use shift and truncate to decompose indices into mask indices and bit indices.
    // This operation requires that the mask has an exact power of two number of bits.
    if (!std.math.isPowerOfTwo(@bitSizeOf(MaskIntType))) {
        var desired_bits = std.math.ceilPowerOfTwoAssert(usize, @bitSizeOf(MaskIntType));
        if (desired_bits < byte_size) desired_bits = byte_size;
        const FixedMaskType = std.meta.Int(.unsigned, desired_bits);
        @compileError("ArrayBitSet was passed integer type " ++ @typeName(MaskIntType) ++
            ", which is not a power of two.  Please round this up to a power of two integer size (i.e. " ++ @typeName(FixedMaskType) ++ ").");
    }

    // Make sure the integer has no padding bits.
    // Those would be wasteful here and are probably a mistake by the user.
    // This case may be hit with small powers of two, like u4.
    if (@bitSizeOf(MaskIntType) != @sizeOf(MaskIntType) * byte_size) {
        var desired_bits = @sizeOf(MaskIntType) * byte_size;
        desired_bits = std.math.ceilPowerOfTwoAssert(usize, desired_bits);
        const FixedMaskType = std.meta.Int(.unsigned, desired_bits);
        @compileError("ArrayBitSet was passed integer type " ++ @typeName(MaskIntType) ++
            ", which contains padding bits.  Please round this up to an unpadded integer size (i.e. " ++ @typeName(FixedMaskType) ++ ").");
    }

    return extern struct {
        const Self = @This();

        // TODO: Make this a comptime field once those are fixed
        /// The number of items in this bit set
        pub const bit_length: usize = size;

        /// The integer type used to represent a mask in this bit set
        pub const MaskInt = MaskIntType;

        /// The integer type used to shift a mask in this bit set
        pub const ShiftInt = std.math.Log2Int(MaskInt);

        // bits in one mask
        const mask_len = @bitSizeOf(MaskInt);
        // total number of masks
        const num_masks = (size + mask_len - 1) / mask_len;
        // padding bits in the last mask (may be 0)
        const last_pad_bits = mask_len * num_masks - size;
        // Mask of valid bits in the last mask.
        // All functions will ensure that the invalid
        // bits in the last mask are zero.
        pub const last_item_mask = ~@as(MaskInt, 0) >> last_pad_bits;

        /// The bit masks, ordered with lower indices first.
        /// Padding bits at the end are undefined.
        masks: [num_masks]MaskInt,

        /// Deprecated: use `.empty`.
        /// Creates a bit set with no elements present.
        pub fn initEmpty() Self {
            return .{ .masks = [_]MaskInt{0} ** num_masks };
        }

        /// Deprecated: use `.full`.
        /// Creates a bit set with all elements present.
        pub fn initFull() Self {
            if (num_masks == 0) {
                return .{ .masks = .{} };
            } else {
                return .{ .masks = [_]MaskInt{~@as(MaskInt, 0)} ** (num_masks - 1) ++ [_]MaskInt{last_item_mask} };
            }
        }

        /// A bit set with no elements present.
        pub const empty: Self = .{ .masks = @splat(0) };

        /// A bit set with all elements present.
        pub const full: Self = .{ .masks = if (num_masks == 0) .{} else ([_]MaskInt{~@as(MaskInt, 0)} ** (num_masks - 1) ++ [_]MaskInt{last_item_mask}) };

        /// Returns the number of bits in this bit set
        pub inline fn capacity(self: Self) usize {
            _ = self;
            return bit_length;
        }

        /// Returns true if the bit at the specified index
        /// is present in the set, false otherwise.
        pub fn isSet(self: Self, index: usize) bool {
            assert(index < bit_length);
            if (num_masks == 0) return false; // doesn't compile in this case
            return (self.masks[maskIndex(index)] & maskBit(index)) != 0;
        }

        /// Returns the total number of set bits in this bit set.
        pub fn count(self: Self) usize {
            var total: usize = 0;
            for (self.masks) |mask| {
                total += @popCount(mask);
            }
            return total;
        }

        /// Changes the value of the specified bit of the bit
        /// set to match the passed boolean.
        pub fn setValue(self: *Self, index: usize, value: bool) void {
            assert(index < bit_length);
            if (num_masks == 0) return; // doesn't compile in this case
            const bit = maskBit(index);
            const mask_index = maskIndex(index);
            const new_bit = bit & std.math.boolMask(MaskInt, value);
            self.masks[mask_index] = (self.masks[mask_index] & ~bit) | new_bit;
        }

        /// Adds a specific bit to the bit set
        pub fn set(self: *Self, index: usize) void {
            assert(index < bit_length);
            if (num_masks == 0) return; // doesn't compile in this case
            self.masks[maskIndex(index)] |= maskBit(index);
        }

        /// Changes the value of all bits in the specified range to
        /// match the passed boolean.
        pub fn setRangeValue(self: *Self, range: Range, value: bool) void {
            assert(range.end <= bit_length);
            assert(range.start <= range.end);
            if (range.start == range.end) return;
            if (num_masks == 0) return;

            const start_mask_index = maskIndex(range.start);
            const start_bit = @as(ShiftInt, @truncate(range.start));

            const end_mask_index = maskIndex(range.end);
            const end_bit = @as(ShiftInt, @truncate(range.end));

            if (start_mask_index == end_mask_index) {
                var mask1 = std.math.boolMask(MaskInt, true) << start_bit;
                var mask2 = std.math.boolMask(MaskInt, true) >> (mask_len - 1) - (end_bit - 1);
                self.masks[start_mask_index] &= ~(mask1 & mask2);

                mask1 = std.math.boolMask(MaskInt, value) << start_bit;
                mask2 = std.math.boolMask(MaskInt, value) >> (mask_len - 1) - (end_bit - 1);
                self.masks[start_mask_index] |= mask1 & mask2;
            } else {
                var bulk_mask_index: usize = undefined;
                if (start_bit > 0) {
                    self.masks[start_mask_index] =
                        (self.masks[start_mask_index] & ~(std.math.boolMask(MaskInt, true) << start_bit)) |
                        (std.math.boolMask(MaskInt, value) << start_bit);
                    bulk_mask_index = start_mask_index + 1;
                } else {
                    bulk_mask_index = start_mask_index;
                }

                while (bulk_mask_index < end_mask_index) : (bulk_mask_index += 1) {
                    self.masks[bulk_mask_index] = std.math.boolMask(MaskInt, value);
                }

                if (end_bit > 0) {
                    self.masks[end_mask_index] =
                        (self.masks[end_mask_index] & (std.math.boolMask(MaskInt, true) << end_bit)) |
                        (std.math.boolMask(MaskInt, value) >> ((@bitSizeOf(MaskInt) - 1) - (end_bit - 1)));
                }
            }
        }

        /// Removes a specific bit from the bit set
        pub fn unset(self: *Self, index: usize) void {
            assert(index < bit_length);
            if (num_masks == 0) return; // doesn't compile in this case
            self.masks[maskIndex(index)] &= ~maskBit(index);
        }

        /// Flips a specific bit in the bit set
        pub fn toggle(self: *Self, index: usize) void {
            assert(index < bit_length);
            if (num_masks == 0) return; // doesn't compile in this case
            self.masks[maskIndex(index)] ^= maskBit(index);
        }

        /// Flips all bits in this bit set which are present
        /// in the toggles bit set.
        pub fn toggleSet(self: *Self, toggles: Self) void {
            for (&self.masks, 0..) |*mask, i| {
                mask.* ^= toggles.masks[i];
            }
        }

        /// Flips every bit in the bit set.
        pub fn toggleAll(self: *Self) void {
            for (&self.masks) |*mask| {
                mask.* = ~mask.*;
            }

            // Zero the padding bits
            if (num_masks > 0) {
                self.masks[num_masks - 1] &= last_item_mask;
            }
        }

        /// Performs a union of two bit sets, and stores the
        /// result in the first one.  Bits in the result are
        /// set if the corresponding bits were set in either input.
        pub fn setUnion(self: *Self, other: Self) void {
            for (&self.masks, 0..) |*mask, i| {
                mask.* |= other.masks[i];
            }
        }

        /// Performs an intersection of two bit sets, and stores
        /// the result in the first one.  Bits in the result are
        /// set if the corresponding bits were set in both inputs.
        pub fn setIntersection(self: *Self, other: Self) void {
            for (&self.masks, 0..) |*mask, i| {
                mask.* &= other.masks[i];
            }
        }

        /// Finds the index of the first set bit.
        /// If no bits are set, returns null.
        pub fn findFirstSet(self: Self) ?usize {
            var offset: usize = 0;
            const mask = for (self.masks) |mask| {
                if (mask != 0) break mask;
                offset += @bitSizeOf(MaskInt);
            } else return null;
            return offset + @ctz(mask);
        }

        /// Finds the index of the last set bit.
        /// If no bits are set, returns null.
        pub fn findLastSet(self: Self) ?usize {
            if (bit_length == 0) return null;
            const bs = @bitSizeOf(MaskInt);
            var len = bit_length / bs;
            if (bit_length % bs != 0) len += 1;
            var offset: usize = len * bs;
            var idx: usize = len - 1;
            while (self.masks[idx] == 0) : (idx -= 1) {
                offset -= bs;
                if (idx == 0) return null;
            }
            offset -= @clz(self.masks[idx]);
            offset -= 1;
            return offset;
        }

        /// Finds the index of the first set bit, and unsets it.
        /// If no bits are set, returns null.
        pub fn toggleFirstSet(self: *Self) ?usize {
            var offset: usize = 0;
            const mask = for (&self.masks) |*mask| {
                if (mask.* != 0) break mask;
                offset += @bitSizeOf(MaskInt);
            } else return null;
            const index = @ctz(mask.*);
            mask.* &= (mask.* - 1);
            return offset + index;
        }

        /// Returns true iff every corresponding bit in both
        /// bit sets are the same.
        pub fn eql(self: Self, other: Self) bool {
            var i: usize = 0;
            return while (i < num_masks) : (i += 1) {
                if (self.masks[i] != other.masks[i]) {
                    break false;
                }
            } else true;
        }

        /// Returns true iff the first bit set is the subset
        /// of the second one.
        pub fn subsetOf(self: Self, other: Self) bool {
            return self.intersectWith(other).eql(self);
        }

        /// Returns true iff the first bit set is the superset
        /// of the second one.
        pub fn supersetOf(self: Self, other: Self) bool {
            return other.subsetOf(self);
        }

        /// Returns the complement bit sets. Bits in the result
        /// are set if the corresponding bits were not set.
        pub fn complement(self: Self) Self {
            var result = self;
            result.toggleAll();
            return result;
        }

        /// Returns the union of two bit sets. Bits in the
        /// result are set if the corresponding bits were set
        /// in either input.
        pub fn unionWith(self: Self, other: Self) Self {
            var result = self;
            result.setUnion(other);
            return result;
        }

        /// Returns the intersection of two bit sets. Bits in
        /// the result are set if the corresponding bits were
        /// set in both inputs.
        pub fn intersectWith(self: Self, other: Self) Self {
            var result = self;
            result.setIntersection(other);
            return result;
        }

        /// Returns the xor of two bit sets. Bits in the
        /// result are set if the corresponding bits were
        /// not the same in both inputs.
        pub fn xorWith(self: Self, other: Self) Self {
            var result = self;
            result.toggleSet(other);
            return result;
        }

        /// Returns the difference of two bit sets. Bits in
        /// the result are set if set in the first but not
        /// set in the second set.
        pub fn differenceWith(self: Self, other: Self) Self {
            var result = self;
            result.setIntersection(other.complement());
            return result;
        }

        /// Iterates through the items in the set, according to the options.
        /// The default options (.{}) will iterate indices of set bits in
        /// ascending order.  Modifications to the underlying bit set may
        /// or may not be observed by the iterator.
        pub fn iterator(self: *const Self, comptime options: IteratorOptions) Iterator(options) {
            return Iterator(options).init(&self.masks, last_item_mask);
        }

        pub fn Iterator(comptime options: IteratorOptions) type {
            return BitSetIterator(MaskInt, options);
        }

        fn maskBit(index: usize) MaskInt {
            return @as(MaskInt, 1) << @as(ShiftInt, @truncate(index));
        }
        fn maskIndex(index: usize) usize {
            return index >> @bitSizeOf(ShiftInt);
        }
        fn boolMaskBit(index: usize, value: bool) MaskInt {
            return @as(MaskInt, @intFromBool(value)) << @as(ShiftInt, @intCast(index));
        }
    };
}

/// A bit set with runtime-known size, backed by an allocated slice
/// of usize.  The allocator must be tracked externally by the user.
pub const DynamicBitSetUnmanaged = struct {
    const Self = @This();

    /// The integer type used to represent a mask in this bit set
    pub const MaskInt = usize;

    /// The integer type used to shift a mask in this bit set
    pub const ShiftInt = std.math.Log2Int(MaskInt);

    /// The number of valid items in this bit set
    bit_length: usize = 0,

    /// The bit masks, ordered with lower indices first.
    /// Padding bits at the end must be zeroed.
    masks: [*]MaskInt = empty_masks_ptr,
    // This pointer is one usize after the actual allocation.
    // That slot holds the size of the true allocation, which
    // is needed by Zig's allocator interface in case a shrink
    // fails.

    // Don't modify this value.  Ideally it would go in const data so
    // modifications would cause a bus error, but the only way
    // to discard a const qualifier is through intFromPtr, which
    // cannot currently round trip at comptime.
    var empty_masks_data = [_]MaskInt{ 0, undefined };
    const empty_masks_ptr = empty_masks_data[1..2];

    /// Creates a bit set with no elements present.
    /// If bit_length is not zero, deinit must eventually be called.
    pub fn initEmpty(allocator: Allocator, bit_length: usize) !Self {
        var self = Self{};
        try self.resize(allocator, bit_length, false);
        return self;
    }

    /// Creates a bit set with all elements present.
    /// If bit_length is not zero, deinit must eventually be called.
    pub fn initFull(allocator: Allocator, bit_length: usize) !Self {
        var self = Self{};
        try self.resize(allocator, bit_length, true);
        return self;
    }

    /// Resizes to a new bit_length.  If the new length is larger
    /// than the old length, fills any added bits with `fill`.
    /// If new_len is not zero, deinit must eventually be called.
    pub fn resize(self: *@This(), allocator: Allocator, new_len: usize, fill: bool) !void {
        const old_len = self.bit_length;

        const old_masks = numMasks(old_len);
        const new_masks = numMasks(new_len);

        const old_allocation = (self.masks - 1)[0..(self.masks - 1)[0]];

        if (new_masks == 0) {
            assert(new_len == 0);
            allocator.free(old_allocation);
            self.masks = empty_masks_ptr;
            self.bit_length = 0;
            return;
        }

        if (old_allocation.len != new_masks + 1) realloc: {
            // If realloc fails, it may mean one of two things.
            // If we are growing, it means we are out of memory.
            // If we are shrinking, it means the allocator doesn't
            // want to move the allocation.  This means we need to
            // hold on to the extra 8 bytes required to be able to free
            // this allocation properly.
            const new_allocation = allocator.realloc(old_allocation, new_masks + 1) catch |err| {
                if (new_masks + 1 > old_allocation.len) return err;
                break :realloc;
            };

            new_allocation[0] = new_allocation.len;
            self.masks = new_allocation.ptr + 1;
        }

        // If we increased in size, we need to set any new bits
        // to the fill value.
        if (new_len > old_len) {
            // set the padding bits in the old last item to 1
            if (fill and old_masks > 0) {
                const old_padding_bits = old_masks * @bitSizeOf(MaskInt) - old_len;
                const old_mask = (~@as(MaskInt, 0)) >> @as(ShiftInt, @intCast(old_padding_bits));
                self.masks[old_masks - 1] |= ~old_mask;
            }

            // fill in any new masks
            if (new_masks > old_masks) {
                const fill_value = std.math.boolMask(MaskInt, fill);
                @memset(self.masks[old_masks..new_masks], fill_value);
            }
        }

        // Zero out the padding bits
        if (new_len > 0) {
            const padding_bits = new_masks * @bitSizeOf(MaskInt) - new_len;
            const last_item_mask = (~@as(MaskInt, 0)) >> @as(ShiftInt, @intCast(padding_bits));
            self.masks[new_masks - 1] &= last_item_mask;
        }

        // And finally, save the new length.
        self.bit_length = new_len;
    }

    /// Deinitializes the array and releases its memory.
    /// The passed allocator must be the same one used for
    /// init* or resize in the past.
    pub fn deinit(self: *Self, allocator: Allocator) void {
        self.resize(allocator, 0, false) catch unreachable;
    }

    /// Creates a duplicate of this bit set, using the new allocator.
    pub fn clone(self: *const Self, new_allocator: Allocator) !Self {
        const num_masks = numMasks(self.bit_length);
        var copy = Self{};
        try copy.resize(new_allocator, self.bit_length, false);
        @memcpy(copy.masks[0..num_masks], self.masks[0..num_masks]);
        return copy;
    }

    /// Returns the number of bits in this bit set
    pub inline fn capacity(self: Self) usize {
        return self.bit_length;
    }

    /// Returns true if the bit at the specified index
    /// is present in the set, false otherwise.
    pub fn isSet(self: Self, index: usize) bool {
        assert(index < self.bit_length);
        return (self.masks[maskIndex(index)] & maskBit(index)) != 0;
    }

    /// Returns the total number of set bits in this bit set.
    pub fn count(self: Self) usize {
        const num_masks = (self.bit_length + (@bitSizeOf(MaskInt) - 1)) / @bitSizeOf(MaskInt);
        var total: usize = 0;
        for (self.masks[0..num_masks]) |mask| {
            // Note: This is where we depend on padding bits being zero
            total += @popCount(mask);
        }
        return total;
    }

    /// Changes the value of the specified bit of the bit
    /// set to match the passed boolean.
    pub fn setValue(self: *Self, index: usize, value: bool) void {
        assert(index < self.bit_length);
        const bit = maskBit(index);
        const mask_index = maskIndex(index);
        const new_bit = bit & std.math.boolMask(MaskInt, value);
        self.masks[mask_index] = (self.masks[mask_index] & ~bit) | new_bit;
    }

    /// Adds a specific bit to the bit set
    pub fn set(self: *Self, index: usize) void {
        assert(index < self.bit_length);
        self.masks[maskIndex(index)] |= maskBit(index);
    }

    /// Changes the value of all bits in the specified range to
    /// match the passed boolean.
    pub fn setRangeValue(self: *Self, range: Range, value: bool) void {
        assert(range.end <= self.bit_length);
        assert(range.start <= range.end);
        if (range.start == range.end) return;

        const start_mask_index = maskIndex(range.start);
        const start_bit = @as(ShiftInt, @truncate(range.start));

        const end_mask_index = maskIndex(range.end);
        const end_bit = @as(ShiftInt, @truncate(range.end));

        if (start_mask_index == end_mask_index) {
            var mask1 = std.math.boolMask(MaskInt, true) << start_bit;
            var mask2 = std.math.boolMask(MaskInt, true) >> (@bitSizeOf(MaskInt) - 1) - (end_bit - 1);
            self.masks[start_mask_index] &= ~(mask1 & mask2);

            mask1 = std.math.boolMask(MaskInt, value) << start_bit;
            mask2 = std.math.boolMask(MaskInt, value) >> (@bitSizeOf(MaskInt) - 1) - (end_bit - 1);
            self.masks[start_mask_index] |= mask1 & mask2;
        } else {
            var bulk_mask_index: usize = undefined;
            if (start_bit > 0) {
                self.masks[start_mask_index] =
                    (self.masks[start_mask_index] & ~(std.math.boolMask(MaskInt, true) << start_bit)) |
                    (std.math.boolMask(MaskInt, value) << start_bit);
                bulk_mask_index = start_mask_index + 1;
            } else {
                bulk_mask_index = start_mask_index;
            }

            while (bulk_mask_index < end_mask_index) : (bulk_mask_index += 1) {
                self.masks[bulk_mask_index] = std.math.boolMask(MaskInt, value);
            }

            if (end_bit > 0) {
                self.masks[end_mask_index] =
                    (self.masks[end_mask_index] & (std.math.boolMask(MaskInt, true) << end_bit)) |
                    (std.math.boolMask(MaskInt, value) >> ((@bitSizeOf(MaskInt) - 1) - (end_bit - 1)));
            }
        }
    }

    /// Removes a specific bit from the bit set
    pub fn unset(self: *Self, index: usize) void {
        assert(index < self.bit_length);
        self.masks[maskIndex(index)] &= ~maskBit(index);
    }

    /// Set all bits to 0.
    pub fn unsetAll(self: *Self) void {
        const masks_len = numMasks(self.bit_length);
        @memset(self.masks[0..masks_len], 0);
    }

    /// Set all bits to 1.
    pub fn setAll(self: *Self) void {
        const masks_len = numMasks(self.bit_length);
        @memset(self.masks[0..masks_len], std.math.maxInt(MaskInt));
    }

    /// Flips a specific bit in the bit set
    pub fn toggle(self: *Self, index: usize) void {
        assert(index < self.bit_length);
        self.masks[maskIndex(index)] ^= maskBit(index);
    }

    /// Flips all bits in this bit set which are present
    /// in the toggles bit set.  Both sets must have the
    /// same bit_length.
    pub fn toggleSet(self: *Self, toggles: Self) void {
        assert(toggles.bit_length == self.bit_length);
        const num_masks = numMasks(self.bit_length);
        for (self.masks[0..num_masks], 0..) |*mask, i| {
            mask.* ^= toggles.masks[i];
        }
    }

    /// Flips every bit in the bit set.
    pub fn toggleAll(self: *Self) void {
        const bit_length = self.bit_length;
        // avoid underflow if bit_length is zero
        if (bit_length == 0) return;

        const num_masks = numMasks(self.bit_length);
        for (self.masks[0..num_masks]) |*mask| {
            mask.* = ~mask.*;
        }

        const padding_bits = num_masks * @bitSizeOf(MaskInt) - bit_length;
        const last_item_mask = (~@as(MaskInt, 0)) >> @as(ShiftInt, @intCast(padding_bits));
        self.masks[num_masks - 1] &= last_item_mask;
    }

    /// Performs a union of two bit sets, and stores the
    /// result in the first one.  Bits in the result are
    /// set if the corresponding bits were set in either input.
    /// The two sets must both be the same bit_length.
    pub fn setUnion(self: *Self, other: Self) void {
        assert(other.bit_length == self.bit_length);
        const num_masks = numMasks(self.bit_length);
        for (self.masks[0..num_masks], 0..) |*mask, i| {
            mask.* |= other.masks[i];
        }
    }

    /// Performs an intersection of two bit sets, and stores
    /// the result in the first one.  Bits in the result are
    /// set if the corresponding bits were set in both inputs.
    /// The two sets must both be the same bit_length.
    pub fn setIntersection(self: *Self, other: Self) void {
        assert(other.bit_length == self.bit_length);
        const num_masks = numMasks(self.bit_length);
        for (self.masks[0..num_masks], 0..) |*mask, i| {
            mask.* &= other.masks[i];
        }
    }

    /// Finds the index of the first set bit.
    /// If no bits are set, returns null.
    pub fn findFirstSet(self: Self) ?usize {
        var offset: usize = 0;
        var mask = self.masks;
        while (offset < self.bit_length) {
            if (mask[0] != 0) break;
            mask += 1;
            offset += @bitSizeOf(MaskInt);
        } else return null;
        return offset + @ctz(mask[0]);
    }

    /// Finds the index of the last set bit.
    /// If no bits are set, returns null.
    pub fn findLastSet(self: Self) ?usize {
        if (self.bit_length == 0) return null;
        const bs = @bitSizeOf(MaskInt);
        var len = self.bit_length / bs;
        if (self.bit_length % bs != 0) len += 1;
        var offset: usize = len * bs;
        var idx: usize = len - 1;
        while (self.masks[idx] == 0) : (idx -= 1) {
            offset -= bs;
            if (idx == 0) return null;
        }
        offset -= @clz(self.masks[idx]);
        offset -= 1;
        return offset;
    }

    /// Finds the index of the first set bit, and unsets it.
    /// If no bits are set, returns null.
    pub fn toggleFirstSet(self: *Self) ?usize {
        var offset: usize = 0;
        var mask = self.masks;
        while (offset < self.bit_length) {
            if (mask[0] != 0) break;
            mask += 1;
            offset += @bitSizeOf(MaskInt);
        } else return null;
        const index = @ctz(mask[0]);
        mask[0] &= (mask[0] - 1);
        return offset + index;
    }

    /// Returns true iff every corresponding bit in both
    /// bit sets are the same.
    pub fn eql(self: Self, other: Self) bool {
        if (self.bit_length != other.bit_length) {
            return false;
        }
        const num_masks = numMasks(self.bit_length);
        var i: usize = 0;
        return while (i < num_masks) : (i += 1) {
            if (self.masks[i] != other.masks[i]) {
                break false;
            }
        } else true;
    }

    /// Returns true iff the first bit set is the subset
    /// of the second one.
    pub fn subsetOf(self: Self, other: Self) bool {
        if (self.bit_length != other.bit_length) {
            return false;
        }
        const num_masks = numMasks(self.bit_length);
        var i: usize = 0;
        return while (i < num_masks) : (i += 1) {
            if (self.masks[i] & other.masks[i] != self.masks[i]) {
                break false;
            }
        } else true;
    }

    /// Returns true iff the first bit set is the superset
    /// of the second one.
    pub fn supersetOf(self: Self, other: Self) bool {
        if (self.bit_length != other.bit_length) {
            return false;
        }
        const num_masks = numMasks(self.bit_length);
        var i: usize = 0;
        return while (i < num_masks) : (i += 1) {
            if (self.masks[i] & other.masks[i] != other.masks[i]) {
                break false;
            }
        } else true;
    }

    /// Iterates through the items in the set, according to the options.
    /// The default options (.{}) will iterate indices of set bits in
    /// ascending order.  Modifications to the underlying bit set may
    /// or may not be observed by the iterator.  Resizing the underlying
    /// bit set invalidates the iterator.
    pub fn iterator(self: *const Self, comptime options: IteratorOptions) Iterator(options) {
        const num_masks = numMasks(self.bit_length);
        const padding_bits = num_masks * @bitSizeOf(MaskInt) - self.bit_length;
        const last_item_mask = (~@as(MaskInt, 0)) >> @as(ShiftInt, @intCast(padding_bits));
        return Iterator(options).init(self.masks[0..num_masks], last_item_mask);
    }

    pub fn Iterator(comptime options: IteratorOptions) type {
        return BitSetIterator(MaskInt, options);
    }

    fn maskBit(index: usize) MaskInt {
        return @as(MaskInt, 1) << @as(ShiftInt, @truncate(index));
    }
    fn maskIndex(index: usize) usize {
        return index >> @bitSizeOf(ShiftInt);
    }
    fn boolMaskBit(index: usize, value: bool) MaskInt {
        return @as(MaskInt, @intFromBool(value)) << @as(ShiftInt, @intCast(index));
    }
    fn numMasks(bit_length: usize) usize {
        return (bit_length + (@bitSizeOf(MaskInt) - 1)) / @bitSizeOf(MaskInt);
    }
};

/// A bit set with runtime-known size, backed by an allocated slice
/// of usize.  Thin wrapper around DynamicBitSetUnmanaged which keeps
/// track of the allocator instance.
pub const DynamicBitSet = struct {
    const Self = @This();

    /// The integer type used to represent a mask in this bit set
    pub const MaskInt = usize;

    /// The integer type used to shift a mask in this bit set
    pub const ShiftInt = std.math.Log2Int(MaskInt);

    allocator: Allocator,
    unmanaged: DynamicBitSetUnmanaged = .{},

    /// Creates a bit set with no elements present.
    pub fn initEmpty(allocator: Allocator, bit_length: usize) !Self {
        return Self{
            .unmanaged = try DynamicBitSetUnmanaged.initEmpty(allocator, bit_length),
            .allocator = allocator,
        };
    }

    /// Creates a bit set with all elements present.
    pub fn initFull(allocator: Allocator, bit_length: usize) !Self {
        return Self{
            .unmanaged = try DynamicBitSetUnmanaged.initFull(allocator, bit_length),
            .allocator = allocator,
        };
    }

    /// Resizes to a new length.  If the new length is larger
    /// than the old length, fills any added bits with `fill`.
    pub fn resize(self: *@This(), new_len: usize, fill: bool) !void {
        try self.unmanaged.resize(self.allocator, new_len, fill);
    }

    /// Deinitializes the array and releases its memory.
    /// The passed allocator must be the same one used for
    /// init* or resize in the past.
    pub fn deinit(self: *Self) void {
        self.unmanaged.deinit(self.allocator);
    }

    /// Creates a duplicate of this bit set, using the new allocator.
    pub fn clone(self: *const Self, new_allocator: Allocator) !Self {
        return Self{
            .unmanaged = try self.unmanaged.clone(new_allocator),
            .allocator = new_allocator,
        };
    }

    /// Returns the number of bits in this bit set
    pub inline fn capacity(self: Self) usize {
        return self.unmanaged.capacity();
    }

    /// Returns true if the bit at the specified index
    /// is present in the set, false otherwise.
    pub fn isSet(self: Self, index: usize) bool {
        return self.unmanaged.isSet(index);
    }

    /// Returns the total number of set bits in this bit set.
    pub fn count(self: Self) usize {
        return self.unmanaged.count();
    }

    /// Changes the value of the specified bit of the bit
    /// set to match the passed boolean.
    pub fn setValue(self: *Self, index: usize, value: bool) void {
        self.unmanaged.setValue(index, value);
    }

    /// Adds a specific bit to the bit set
    pub fn set(self: *Self, index: usize) void {
        self.unmanaged.set(index);
    }

    /// Changes the value of all bits in the specified range to
    /// match the passed boolean.
    pub fn setRangeValue(self: *Self, range: Range, value: bool) void {
        self.unmanaged.setRangeValue(range, value);
    }

    /// Removes a specific bit from the bit set
    pub fn unset(self: *Self, index: usize) void {
        self.unmanaged.unset(index);
    }

    /// Flips a specific bit in the bit set
    pub fn toggle(self: *Self, index: usize) void {
        self.unmanaged.toggle(index);
    }

    /// Flips all bits in this bit set which are present
    /// in the toggles bit set.  Both sets must have the
    /// same bit_length.
    pub fn toggleSet(self: *Self, toggles: Self) void {
        self.unmanaged.toggleSet(toggles.unmanaged);
    }

    /// Flips every bit in the bit set.
    pub fn toggleAll(self: *Self) void {
        self.unmanaged.toggleAll();
    }

    /// Performs a union of two bit sets, and stores the
    /// result in the first one.  Bits in the result are
    /// set if the corresponding bits were set in either input.
    /// The two sets must both be the same bit_length.
    pub fn setUnion(self: *Self, other: Self) void {
        self.unmanaged.setUnion(other.unmanaged);
    }

    /// Performs an intersection of two bit sets, and stores
    /// the result in the first one.  Bits in the result are
    /// set if the corresponding bits were set in both inputs.
    /// The two sets must both be the same bit_length.
    pub fn setIntersection(self: *Self, other: Self) void {
        self.unmanaged.setIntersection(other.unmanaged);
    }

    /// Finds the index of the first set bit.
    /// If no bits are set, returns null.
    pub fn findFirstSet(self: Self) ?usize {
        return self.unmanaged.findFirstSet();
    }

    /// Finds the index of the last set bit.
    /// If no bits are set, returns null.
    pub fn findLastSet(self: Self) ?usize {
        return self.unmanaged.findLastSet();
    }

    /// Finds the index of the first set bit, and unsets it.
    /// If no bits are set, returns null.
    pub fn toggleFirstSet(self: *Self) ?usize {
        return self.unmanaged.toggleFirstSet();
    }

    /// Returns true iff every corresponding bit in both
    /// bit sets are the same.
    pub fn eql(self: Self, other: Self) bool {
        return self.unmanaged.eql(other.unmanaged);
    }

    /// Iterates through the items in the set, according to the options.
    /// The default options (.{}) will iterate indices of set bits in
    /// ascending order.  Modifications to the underlying bit set may
    /// or may not be observed by the iterator.  Resizing the underlying
    /// bit set invalidates the iterator.
    pub fn iterator(self: *const Self, comptime options: IteratorOptions) Iterator(options) {
        return self.unmanaged.iterator(options);
    }

    pub const Iterator = DynamicBitSetUnmanaged.Iterator;
};

/// Options for configuring an iterator over a bit set
pub const IteratorOptions = struct {
    /// determines which bits should be visited
    kind: Type = .set,
    /// determines the order in which bit indices should be visited
    direction: Direction = .forward,

    pub const Type = enum {
        /// visit indexes of set bits
        set,
        /// visit indexes of unset bits
        unset,
    };

    pub const Direction = enum {
        /// visit indices in ascending order
        forward,
        /// visit indices in descending order.
        /// Note that this may be slightly more expensive than forward iteration.
        reverse,
    };
};

// The iterator is reusable between several bit set types
fn BitSetIterator(comptime MaskInt: type, comptime options: IteratorOptions) type {
    const ShiftInt = std.math.Log2Int(MaskInt);
    const kind = options.kind;
    const direction = options.direction;
    return struct {
        const Self = @This();

        // all bits which have not yet been iterated over
        bits_remain: MaskInt,
        // all words which have not yet been iterated over
        words_remain: []const MaskInt,
        // the offset of the current word
        bit_offset: usize,
        // the mask of the last word
        last_word_mask: MaskInt,

        fn init(masks: []const MaskInt, last_word_mask: MaskInt) Self {
            if (masks.len == 0) {
                return Self{
                    .bits_remain = 0,
                    .words_remain = &[_]MaskInt{},
                    .last_word_mask = last_word_mask,
                    .bit_offset = 0,
                };
            } else {
                var result = Self{
                    .bits_remain = 0,
                    .words_remain = masks,
                    .last_word_mask = last_word_mask,
                    .bit_offset = if (direction == .forward) 0 else (masks.len - 1) * @bitSizeOf(MaskInt),
                };
                result.nextWord(true);
                return result;
            }
        }

        /// Returns the index of the next unvisited set bit
        /// in the bit set, in ascending order.
        pub fn next(self: *Self) ?usize {
            while (self.bits_remain == 0) {
                if (self.words_remain.len == 0) return null;
                self.nextWord(false);
                switch (direction) {
                    .forward => self.bit_offset += @bitSizeOf(MaskInt),
                    .reverse => self.bit_offset -= @bitSizeOf(MaskInt),
                }
            }

            switch (direction) {
                .forward => {
                    const next_index = @ctz(self.bits_remain) + self.bit_offset;
                    self.bits_remain &= self.bits_remain - 1;
                    return next_index;
                },
                .reverse => {
                    const leading_zeroes = @clz(self.bits_remain);
                    const top_bit = (@bitSizeOf(MaskInt) - 1) - leading_zeroes;
                    const no_top_bit_mask = (@as(MaskInt, 1) << @as(ShiftInt, @intCast(top_bit))) - 1;
                    self.bits_remain &= no_top_bit_mask;
                    return top_bit + self.bit_offset;
                },
            }
        }

        // Load the next word.  Don't call this if there
        // isn't a next word.  If the next word is the
        // last word, mask off the padding bits so we
        // don't visit them.
        inline fn nextWord(self: *Self, comptime is_first_word: bool) void {
            var word = switch (direction) {
                .forward => self.words_remain[0],
                .reverse => self.words_remain[self.words_remain.len - 1],
            };
            switch (kind) {
                .set => {},
                .unset => {
                    word = ~word;
                    if ((direction == .reverse and is_first_word) or
                        (direction == .forward and self.words_remain.len == 1))
                    {
                        word &= self.last_word_mask;
                    }
                },
            }
            switch (direction) {
                .forward => self.words_remain = self.words_remain[1..],
                .reverse => self.words_remain.len -= 1,
            }
            self.bits_remain = word;
        }
    };
}

/// A range of indices within a bitset.
pub const Range = struct {
    /// The index of the first bit of interest.
    start: usize,
    /// The index immediately after the last bit of interest.
    end: usize,
};

// ---------------- Tests -----------------

const testing = std.testing;

fn testEql(empty: anytype, full: anytype, len: usize) !void {
    try testing.expect(empty.eql(empty));
    try testing.expect(full.eql(full));
    switch (len) {
        0 => {
            try testing.expect(empty.eql(full));
            try testing.expect(full.eql(empty));
        },
        else => {
            try testing.expect(!empty.eql(full));
            try testing.expect(!full.eql(empty));
        },
    }
}

fn testSubsetOf(empty: anytype, full: anytype, even: anytype, odd: anytype, len: usize) !void {
    try testing.expect(empty.subsetOf(empty));
    try testing.expect(empty.subsetOf(full));
    try testing.expect(full.subsetOf(full));
    switch (len) {
        0 => {
            try testing.expect(even.subsetOf(odd));
            try testing.expect(odd.subsetOf(even));
        },
        1 => {
            try testing.expect(!even.subsetOf(odd));
            try testing.expect(odd.subsetOf(even));
        },
        else => {
            try testing.expect(!even.subsetOf(odd));
            try testing.expect(!odd.subsetOf(even));
        },
    }
}

fn testSupersetOf(empty: anytype, full: anytype, even: anytype, odd: anytype, len: usize) !void {
    try testing.expect(full.supersetOf(full));
    try testing.expect(full.supersetOf(empty));
    try testing.expect(empty.supersetOf(empty));
    switch (len) {
        0 => {
            try testing.expect(even.supersetOf(odd));
            try testing.expect(odd.supersetOf(even));
        },
        1 => {
            try testing.expect(even.supersetOf(odd));
            try testing.expect(!odd.supersetOf(even));
        },
        else => {
            try testing.expect(!even.supersetOf(odd));
            try testing.expect(!odd.supersetOf(even));
        },
    }
}

fn testBitSet(a: anytype, b: anytype, len: usize) !void {
    try testing.expectEqual(len, a.capacity());
    try testing.expectEqual(len, b.capacity());

    {
        var i: usize = 0;
        while (i < len) : (i += 1) {
            a.setValue(i, i & 1 == 0);
            b.setValue(i, i & 2 == 0);
        }
    }

    try testing.expectEqual((len + 1) / 2, a.count());
    try testing.expectEqual((len + 3) / 4 + (len + 2) / 4, b.count());

    {
        var iter = a.iterator(.{});
        var i: usize = 0;
        while (i < len) : (i += 2) {
            try testing.expectEqual(@as(?usize, i), iter.next());
        }
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
    }
    a.toggleAll();
    {
        var iter = a.iterator(.{});
        var i: usize = 1;
        while (i < len) : (i += 2) {
            try testing.expectEqual(@as(?usize, i), iter.next());
        }
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
    }

    {
        var iter = b.iterator(.{ .kind = .unset });
        var i: usize = 2;
        while (i < len) : (i += 4) {
            try testing.expectEqual(@as(?usize, i), iter.next());
            if (i + 1 < len) {
                try testing.expectEqual(@as(?usize, i + 1), iter.next());
            }
        }
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
    }

    {
        var i: usize = 0;
        while (i < len) : (i += 1) {
            try testing.expectEqual(i & 1 != 0, a.isSet(i));
            try testing.expectEqual(i & 2 == 0, b.isSet(i));
        }
    }

    a.setUnion(b.*);
    {
        var i: usize = 0;
        while (i < len) : (i += 1) {
            try testing.expectEqual(i & 1 != 0 or i & 2 == 0, a.isSet(i));
            try testing.expectEqual(i & 2 == 0, b.isSet(i));
        }

        i = len;
        var set = a.iterator(.{ .direction = .reverse });
        var unset = a.iterator(.{ .kind = .unset, .direction = .reverse });
        while (i > 0) {
            i -= 1;
            if (i & 1 != 0 or i & 2 == 0) {
                try testing.expectEqual(@as(?usize, i), set.next());
            } else {
                try testing.expectEqual(@as(?usize, i), unset.next());
            }
        }
        try testing.expectEqual(@as(?usize, null), set.next());
        try testing.expectEqual(@as(?usize, null), set.next());
        try testing.expectEqual(@as(?usize, null), set.next());
        try testing.expectEqual(@as(?usize, null), unset.next());
        try testing.expectEqual(@as(?usize, null), unset.next());
        try testing.expectEqual(@as(?usize, null), unset.next());
    }

    a.toggleSet(b.*);
    {
        try testing.expectEqual(len / 4, a.count());

        var i: usize = 0;
        while (i < len) : (i += 1) {
            try testing.expectEqual(i & 1 != 0 and i & 2 != 0, a.isSet(i));
            try testing.expectEqual(i & 2 == 0, b.isSet(i));
            if (i & 1 == 0) {
                a.set(i);
            } else {
                a.unset(i);
            }
        }
    }

    a.setIntersection(b.*);
    {
        try testing.expectEqual((len + 3) / 4, a.count());

        var i: usize = 0;
        while (i < len) : (i += 1) {
            try testing.expectEqual(i & 1 == 0 and i & 2 == 0, a.isSet(i));
            try testing.expectEqual(i & 2 == 0, b.isSet(i));
        }
    }

    a.toggleSet(a.*);
    {
        var iter = a.iterator(.{});
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(usize, 0), a.count());
    }
    {
        var iter = a.iterator(.{ .direction = .reverse });
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(?usize, null), iter.next());
        try testing.expectEqual(@as(usize, 0), a.count());
    }

    const test_bits = [_]usize{
        0,  1,  2,   3,   4,   5,    6, 7, 9, 10, 11, 22, 31, 32, 63, 64,
        66, 95, 127, 160, 192, 1000,
    };
    for (test_bits) |i| {
        if (i < a.capacity()) {
            a.set(i);
        }
    }

    for (test_bits) |i| {
        if (i < a.capacity()) {
            try testing.expectEqual(@as(?usize, i), a.findFirstSet());
            try testing.expectEqual(@as(?usize, i), a.toggleFirstSet());
        }
    }
    try testing.expectEqual(@as(?usize, null), a.findFirstSet());
    try testing.expectEqual(@as(?usize, null), a.findLastSet());
    try testing.expectEqual(@as(?usize, null), a.toggleFirstSet());
    try testing.expectEqual(@as(?usize, null), a.findFirstSet());
    try testing.expectEqual(@as(?usize, null), a.findLastSet());
    try testing.expectEqual(@as(?usize, null), a.toggleFirstSet());
    try testing.expectEqual(@as(usize, 0), a.count());

    a.setRangeValue(.{ .start = 0, .end = len }, false);
    try testing.expectEqual(@as(usize, 0), a.count());

    a.setRangeValue(.{ .start = 0, .end = len }, true);
    try testing.expectEqual(len, a.count());

    a.setRangeValue(.{ .start = 0, .end = len }, false);
    a.setRangeValue(.{ .start = 0, .end = 0 }, true);
    try testing.expectEqual(@as(usize, 0), a.count());

    a.setRangeValue(.{ .start = len, .end = len }, true);
    try testing.expectEqual(@as(usize, 0), a.count());

    if (len >= 1) {
        a.setRangeValue(.{ .start = 0, .end = len }, false);
        a.setRangeValue(.{ .start = 0, .end = 1 }, true);
        try testing.expectEqual(@as(usize, 1), a.count());
        try testing.expect(a.isSet(0));

        a.setRangeValue(.{ .start = 0, .end = len }, false);
        a.setRangeValue(.{ .start = 0, .end = len - 1 }, true);
        try testing.expectEqual(len - 1, a.count());
        try testing.expect(!a.isSet(len - 1));

        a.setRangeValue(.{ .start = 0, .end = len }, false);
        a.setRangeValue(.{ .start = 1, .end = len }, true);
        try testing.expectEqual(@as(usize, len - 1), a.count());
        try testing.expect(!a.isSet(0));

        a.setRangeValue(.{ .start = 0, .end = len }, false);
        a.setRangeValue(.{ .start = len - 1, .end = len }, true);
        try testing.expectEqual(@as(usize, 1), a.count());
        try testing.expect(a.isSet(len - 1));

        if (len >= 4) {
            a.setRangeValue(.{ .start = 0, .end = len }, false);
            a.setRangeValue(.{ .start = 1, .end = len - 2 }, true);
            try testing.expectEqual(@as(usize, len - 3), a.count());
            try testing.expect(!a.isSet(0));
            try testing.expect(a.isSet(1));
            try testing.expect(a.isSet(len - 3));
            try testing.expect(!a.isSet(len - 2));
            try testing.expect(!a.isSet(len - 1));
        }
    }
}

fn fillEven(set: anytype, len: usize) void {
    var i: usize = 0;
    while (i < len) : (i += 1) {
        set.setValue(i, i & 1 == 0);
    }
}

fn fillOdd(set: anytype, len: usize) void {
    var i: usize = 0;
    while (i < len) : (i += 1) {
        set.setValue(i, i & 1 == 1);
    }
}

fn testPureBitSet(comptime Set: type) !void {
    const empty = Set.empty;
    const full = Set.full;

    const even = even: {
        var bit_set = Set.empty;
        fillEven(&bit_set, Set.bit_length);
        break :even bit_set;
    };

    const odd = odd: {
        var bit_set = Set.empty;
        fillOdd(&bit_set, Set.bit_length);
        break :odd bit_set;
    };

    try testSubsetOf(empty, full, even, odd, Set.bit_length);
    try testSupersetOf(empty, full, even, odd, Set.bit_length);

    try testing.expect(empty.complement().eql(full));
    try testing.expect(full.complement().eql(empty));
    try testing.expect(even.complement().eql(odd));
    try testing.expect(odd.complement().eql(even));

    try testing.expect(empty.unionWith(empty).eql(empty));
    try testing.expect(empty.unionWith(full).eql(full));
    try testing.expect(full.unionWith(full).eql(full));
    try testing.expect(full.unionWith(empty).eql(full));
    try testing.expect(even.unionWith(odd).eql(full));
    try testing.expect(odd.unionWith(even).eql(full));

    try testing.expect(empty.intersectWith(empty).eql(empty));
    try testing.expect(empty.intersectWith(full).eql(empty));
    try testing.expect(full.intersectWith(full).eql(full));
    try testing.expect(full.intersectWith(empty).eql(empty));
    try testing.expect(even.intersectWith(odd).eql(empty));
    try testing.expect(odd.intersectWith(even).eql(empty));

    try testing.expect(empty.xorWith(empty).eql(empty));
    try testing.expect(empty.xorWith(full).eql(full));
    try testing.expect(full.xorWith(full).eql(empty));
    try testing.expect(full.xorWith(empty).eql(full));
    try testing.expect(even.xorWith(odd).eql(full));
    try testing.expect(odd.xorWith(even).eql(full));

    try testing.expect(empty.differenceWith(empty).eql(empty));
    try testing.expect(empty.differenceWith(full).eql(empty));
    try testing.expect(full.differenceWith(full).eql(empty));
    try testing.expect(full.differenceWith(empty).eql(full));
    try testing.expect(full.differenceWith(odd).eql(even));
    try testing.expect(full.differenceWith(even).eql(odd));
}

fn testStaticBitSet(comptime Set: type) !void {
    var a = Set.empty;
    var b = Set.full;
    try testing.expectEqual(@as(usize, 0), a.count());
    try testing.expectEqual(@as(usize, Set.bit_length), b.count());

    try testEql(a, b, Set.bit_length);
    try testBitSet(&a, &b, Set.bit_length);

    try testPureBitSet(Set);
}

test IntegerBitSet {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest;
    if (comptime builtin.cpu.has(.riscv, .v) and builtin.zig_backend == .stage2_llvm) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/24300

    try testStaticBitSet(IntegerBitSet(0));
    try testStaticBitSet(IntegerBitSet(1));
    try testStaticBitSet(IntegerBitSet(2));
    try testStaticBitSet(IntegerBitSet(5));
    try testStaticBitSet(IntegerBitSet(8));
    try testStaticBitSet(IntegerBitSet(32));
    try testStaticBitSet(IntegerBitSet(64));
    try testStaticBitSet(IntegerBitSet(127));
}

test ArrayBitSet {
    inline for (.{ 0, 1, 2, 31, 32, 33, 63, 64, 65, 254, 500, 3000 }) |size| {
        try testStaticBitSet(ArrayBitSet(u8, size));
        try testStaticBitSet(ArrayBitSet(u16, size));
        try testStaticBitSet(ArrayBitSet(u32, size));
        try testStaticBitSet(ArrayBitSet(u64, size));
        try testStaticBitSet(ArrayBitSet(u128, size));
    }
}

test DynamicBitSetUnmanaged {
    const allocator = std.testing.allocator;
    var a = try DynamicBitSetUnmanaged.initEmpty(allocator, 300);
    try testing.expectEqual(@as(usize, 0), a.count());
    a.deinit(allocator);

    a = try DynamicBitSetUnmanaged.initEmpty(allocator, 0);
    defer a.deinit(allocator);
    for ([_]usize{ 1, 2, 31, 32, 33, 0, 65, 64, 63, 500, 254, 3000 }) |size| {
        const old_len = a.capacity();

        var empty = try a.clone(allocator);
        defer empty.deinit(allocator);
        try testing.expectEqual(old_len, empty.capacity());
        var i: usize = 0;
        while (i < old_len) : (i += 1) {
            try testing.expectEqual(a.isSet(i), empty.isSet(i));
        }

        a.toggleSet(a); // zero a
        empty.toggleSet(empty);

        try a.resize(allocator, size, true);
        try empty.resize(allocator, size, false);

        if (size > old_len) {
            try testing.expectEqual(size - old_len, a.count());
        } else {
            try testing.expectEqual(@as(usize, 0), a.count());
        }
        try testing.expectEqual(@as(usize, 0), empty.count());

        var full = try DynamicBitSetUnmanaged.initFull(allocator, size);
        defer full.deinit(allocator);
        try testing.expectEqual(@as(usize, size), full.count());

        try testEql(empty, full, size);
        {
            var even = try DynamicBitSetUnmanaged.initEmpty(allocator, size);
            defer even.deinit(allocator);
            fillEven(&even, size);

            var odd = try DynamicBitSetUnmanaged.initEmpty(allocator, size);
            defer odd.deinit(allocator);
            fillOdd(&odd, size);

            try testSubsetOf(empty, full, even, odd, size);
            try testSupersetOf(empty, full, even, odd, size);
        }
        try testBitSet(&a, &full, size);
    }
}

test DynamicBitSet {
    const allocator = std.testing.allocator;
    var a = try DynamicBitSet.initEmpty(allocator, 300);
    try testing.expectEqual(@as(usize, 0), a.count());
    a.deinit();

    a = try DynamicBitSet.initEmpty(allocator, 0);
    defer a.deinit();
    for ([_]usize{ 1, 2, 31, 32, 33, 0, 65, 64, 63, 500, 254, 3000 }) |size| {
        const old_len = a.capacity();

        var tmp = try a.clone(allocator);
        defer tmp.deinit();
        try testing.expectEqual(old_len, tmp.capacity());
        var i: usize = 0;
        while (i < old_len) : (i += 1) {
            try testing.expectEqual(a.isSet(i), tmp.isSet(i));
        }

        a.toggleSet(a); // zero a
        tmp.toggleSet(tmp); // zero tmp

        try a.resize(size, true);
        try tmp.resize(size, false);

        if (size > old_len) {
            try testing.expectEqual(size - old_len, a.count());
        } else {
            try testing.expectEqual(@as(usize, 0), a.count());
        }
        try testing.expectEqual(@as(usize, 0), tmp.count());

        var b = try DynamicBitSet.initFull(allocator, size);
        defer b.deinit();
        try testing.expectEqual(@as(usize, size), b.count());

        try testEql(tmp, b, size);
        try testBitSet(&a, &b, size);
    }
}

test StaticBitSet {
    try testing.expectEqual(IntegerBitSet(0), StaticBitSet(0));
    try testing.expectEqual(IntegerBitSet(5), StaticBitSet(5));
    try testing.expectEqual(IntegerBitSet(@bitSizeOf(usize)), StaticBitSet(@bitSizeOf(usize)));
    try testing.expectEqual(ArrayBitSet(usize, @bitSizeOf(usize) + 1), StaticBitSet(@bitSizeOf(usize) + 1));
    try testing.expectEqual(ArrayBitSet(usize, 500), StaticBitSet(500));
}



---
File: /std/BitStack.zig
---

//! Effectively a stack of u1 values implemented using ArrayList(u8).

const BitStack = @This();

const std = @import("std");
const Allocator = std.mem.Allocator;
const ArrayList = std.array_list.Managed;

bytes: std.array_list.Managed(u8),
bit_len: usize = 0,

pub fn init(allocator: Allocator) @This() {
    return .{
        .bytes = std.array_list.Managed(u8).init(allocator),
    };
}

pub fn deinit(self: *@This()) void {
    self.bytes.deinit();
    self.* = undefined;
}

pub fn ensureTotalCapacity(self: *@This(), bit_capacity: usize) Allocator.Error!void {
    const byte_capacity = (bit_capacity + 7) >> 3;
    try self.bytes.ensureTotalCapacity(byte_capacity);
}

pub fn push(self: *@This(), b: u1) Allocator.Error!void {
    const byte_index = self.bit_len >> 3;
    if (self.bytes.items.len <= byte_index) {
        try self.bytes.append(0);
    }

    pushWithStateAssumeCapacity(self.bytes.items, &self.bit_len, b);
}

pub fn peek(self: *const @This()) u1 {
    return peekWithState(self.bytes.items, self.bit_len);
}

pub fn pop(self: *@This()) u1 {
    return popWithState(self.bytes.items, &self.bit_len);
}

/// Standalone function for working with a fixed-size buffer.
pub fn pushWithStateAssumeCapacity(buf: []u8, bit_len: *usize, b: u1) void {
    const byte_index = bit_len.* >> 3;
    const bit_index = @as(u3, @intCast(bit_len.* & 7));

    buf[byte_index] &= ~(@as(u8, 1) << bit_index);
    buf[byte_index] |= @as(u8, b) << bit_index;

    bit_len.* += 1;
}

/// Standalone function for working with a fixed-size buffer.
pub fn peekWithState(buf: []const u8, bit_len: usize) u1 {
    const byte_index = (bit_len - 1) >> 3;
    const bit_index = @as(u3, @intCast((bit_len - 1) & 7));
    return @as(u1, @intCast((buf[byte_index] >> bit_index) & 1));
}

/// Standalone function for working with a fixed-size buffer.
pub fn popWithState(buf: []const u8, bit_len: *usize) u1 {
    const b = peekWithState(buf, bit_len.*);
    bit_len.* -= 1;
    return b;
}

const testing = std.testing;
test BitStack {
    var stack = BitStack.init(testing.allocator);
    defer stack.deinit();

    try stack.push(1);
    try stack.push(0);
    try s
```
