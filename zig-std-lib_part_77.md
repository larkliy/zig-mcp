```
 `log_int` correctly detects the jump in the logarithm
            // from `log(pow-1) == exp-1` to `log(pow) == exp`.
            var exp: Log2Int(T) = 0;
            var pow: T = 1;
            while (pow <= math.maxInt(T) / base) {
                exp += 1;
                pow *= base;

                try testing.expectEqual(exp - 1, log_int(T, base, pow - 1));
                try testing.expectEqual(exp, log_int(T, base, pow));
            }
        }
    }
}

test "log_int vs math.log2" {
    const types = [_]type{ u2, u3, u4, u8, u16 };
    inline for (types) |T| {
        var n: T = 0;
        while (n < math.maxInt(T)) {
            n += 1;
            const special = math.log2_int(T, n);
            const general = log_int(T, 2, n);
            try testing.expectEqual(special, general);
        }
    }
}

test "log_int vs math.log10" {
    const types = [_]type{ u4, u5, u6, u8, u16 };
    inline for (types) |T| {
        var n: T = 0;
        while (n < math.maxInt(T)) {
            n += 1;
            const special = math.log10_int(n);
            const general = log_int(T, 10, n);
            try testing.expectEqual(special, general);
        }
    }
}

test "log_int at comptime" {
    const x = 59049; // 9 ** 5;
    comptime {
        if (math.log_int(comptime_int, 9, x) != 5) {
            @compileError("log(9, 59049) should be 5");
        }
    }
}



---
File: /std/math/log.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/logf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/log.c

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns the logarithm of x for the provided base.
pub fn log(comptime T: type, base: T, x: T) T {
    if (base == 2) {
        return math.log2(x);
    } else if (base == 10) {
        return math.log10(x);
    } else if ((@typeInfo(T) == .float or @typeInfo(T) == .comptime_float) and base == math.e) {
        return @log(x);
    }

    const float_base = math.lossyCast(f64, base);
    switch (@typeInfo(T)) {
        .comptime_float => {
            return @as(comptime_float, @log(@as(f64, x)) / @log(float_base));
        },

        .comptime_int => {
            return @as(comptime_int, math.log_int(comptime_int, base, x));
        },

        .int => |IntType| switch (IntType.signedness) {
            .signed => @compileError("log not implemented for signed integers"),
            .unsigned => return @as(T, math.log_int(T, base, x)),
        },

        .float => {
            switch (T) {
                f32 => return @as(f32, @floatCast(@log(@as(f64, x)) / @log(float_base))),
                f64 => return @log(x) / @log(float_base),
                else => @compileError("log not implemented for " ++ @typeName(T)),
            }
        },

        else => {
            @compileError("log expects integer or float, found '" ++ @typeName(T) ++ "'");
        },
    }
}

test "log integer" {
    try expect(log(u8, 2, 0x1) == 0);
    try expect(log(u8, 2, 0x2) == 1);
    try expect(log(u16, 2, 0x72) == 6);
    try expect(log(u32, 2, 0xFFFFFF) == 23);
    try expect(log(u64, 2, 0x7FF0123456789ABC) == 62);
}

test "log float" {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, log(f32, 6, 0.23947), -0.797723, epsilon));
    try expect(math.approxEqAbs(f32, log(f32, 89, 0.23947), -0.318432, epsilon));
    try expect(math.approxEqAbs(f64, log(f64, 123897, 12389216414), 1.981724596, epsilon));
}

test "log float_special" {
    try expect(log(f32, 2, 0.2301974) == math.log2(@as(f32, 0.2301974)));
    try expect(log(f32, 10, 0.2301974) == math.log10(@as(f32, 0.2301974)));

    try expect(log(f64, 2, 213.23019799993) == math.log2(@as(f64, 213.23019799993)));
    try expect(log(f64, 10, 213.23019799993) == math.log10(@as(f64, 213.23019799993)));
}



---
File: /std/math/log10.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");
const testing = std.testing;

/// Returns the base-10 logarithm of x.
///
/// Special Cases:
///  - log10(+inf)  = +inf
///  - log10(0)     = -inf
///  - log10(x)     = nan if x < 0
///  - log10(nan)   = nan
pub fn log10(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    switch (@typeInfo(T)) {
        .comptime_float => {
            return @as(comptime_float, @log10(x));
        },
        .float => return @log10(x),
        .comptime_int => {
            const Int = @Int(
                if (x < 0) .signed else .unsigned,
                // We let log10_int check if x is 0, for a nicer error message.
                @max(16, if (x == 0) 0 else 1 + std.math.log2(x)),
            );
            return @as(comptime_int, log10_int(@as(Int, x)));
        },
        .int => |IntType| switch (IntType.signedness) {
            .signed => @compileError("log10 not implemented for signed integers"),
            .unsigned => return log10_int(x),
        },
        else => @compileError("log10 not implemented for " ++ @typeName(T)),
    }
}

// Based on Rust, which is licensed under the MIT license.
// https://github.com/rust-lang/rust/blob/f63ccaf25f74151a5d8ce057904cd944074b01d2/LICENSE-MIT
//
// https://github.com/rust-lang/rust/blob/f63ccaf25f74151a5d8ce057904cd944074b01d2/library/core/src/num/int_log10.rs

/// Return the log base 10 of integer value x, rounding down to the
/// nearest integer.
pub fn log10_int(x: anytype) std.math.Log2Int(@TypeOf(x)) {
    const T = @TypeOf(x);
    const OutT = std.math.Log2Int(T);
    if (@typeInfo(T) != .int or @typeInfo(T).int.signedness != .unsigned)
        @compileError("log10_int requires an unsigned integer, found " ++ @typeName(T));

    std.debug.assert(x != 0);

    const bit_size = @typeInfo(T).int.bits;

    if (bit_size <= 8) {
        return @as(OutT, @intCast(log10_int_u8(x)));
    } else if (bit_size <= 16) {
        return @as(OutT, @intCast(less_than_5(x)));
    }

    var val = x;
    var log: u32 = 0;

    inline for (0..11) |i| {
        // Unnecessary branches should be removed by the compiler
        if (bit_size > (1 << (11 - i)) * 5 * @log2(10.0) and val >= pow10((1 << (11 - i)) * 5)) {
            const num_digits = (1 << (11 - i)) * 5;
            val /= pow10(num_digits);
            log += num_digits;
        }
    }

    if (val >= pow10(5)) {
        val /= pow10(5);
        log += 5;
    }

    return @as(OutT, @intCast(log + less_than_5(@as(u32, @intCast(val)))));
}

fn pow10(comptime y: comptime_int) comptime_int {
    if (y == 0) return 1;

    var squaring = 0;
    var s = 1;

    while (s <= y) : (s <<= 1) {
        squaring += 1;
    }

    squaring -= 1;

    var result = 10;

    for (0..squaring) |_| {
        result *= result;
    }

    const rest_exp = y - (1 << squaring);

    return result * pow10(rest_exp);
}

inline fn log10_int_u8(x: u8) u32 {
    // For better performance, avoid branches by assembling the solution
    // in the bits above the low 8 bits.

    // Adding c1 to val gives 10 in the top bits for val < 10, 11 for val >= 10
    const C1: u32 = 0b11_00000000 - 10; // 758
    // Adding c2 to val gives 01 in the top bits for val < 100, 10 for val >= 100
    const C2: u32 = 0b10_00000000 - 100; // 412

    // Value of top bits:
    //            +c1  +c2  1&2
    //     0..=9   10   01   00 = 0
    //   10..=99   11   01   01 = 1
    // 100..=255   11   10   10 = 2
    return ((x + C1) & (x + C2)) >> 8;
}

inline fn less_than_5(x: u32) u32 {
    // Similar to log10u8, when adding one of these constants to val,
    // we get two possible bit patterns above the low 17 bits,
    // depending on whether val is below or above the threshold.
    const C1: u32 = 0b011_00000000000000000 - 10; // 393206
    const C2: u32 = 0b100_00000000000000000 - 100; // 524188
    const C3: u32 = 0b111_00000000000000000 - 1000; // 916504
    const C4: u32 = 0b100_00000000000000000 - 10000; // 514288

    // Value of top bits:
    //                +c1  +c2  1&2  +c3  +c4  3&4   ^
    //         0..=9  010  011  010  110  011  010  000 = 0
    //       10..=99  011  011  011  110  011  010  001 = 1
    //     100..=999  011  100  000  110  011  010  010 = 2
    //   1000..=9999  011  100  000  111  011  011  011 = 3
    // 10000..=99999  011  100  000  111  100  100  100 = 4
    return (((x + C1) & (x + C2)) ^ ((x + C3) & (x + C4))) >> 17;
}

test log10_int {
    if (builtin.zig_backend == .stage2_wasm) return error.SkipZigTest; // TODO
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest; // TODO
    if (builtin.zig_backend == .stage2_sparc64) return error.SkipZigTest; // TODO
    if (builtin.zig_backend == .stage2_arm) return error.SkipZigTest; // TODO
    if (builtin.zig_backend == .stage2_llvm and comptime builtin.target.cpu.arch.isWasm()) return error.SkipZigTest; // TODO
    if (builtin.zig_backend == .stage2_llvm and comptime builtin.target.cpu.arch == .hexagon) return error.SkipZigTest;

    inline for (
        .{ u8, u16, u32, u64, u128, u256, u512 },
        .{ 2, 4, 9, 19, 38, 77, 154 },
    ) |T, max_exponent| {
        for (0..max_exponent + 1) |exponent_usize| {
            const exponent: std.math.Log2Int(T) = @intCast(exponent_usize);
            const power_of_ten = try std.math.powi(T, 10, exponent);

            if (exponent > 0) {
                try testing.expectEqual(exponent - 1, log10_int(power_of_ten - 9));
                try testing.expectEqual(exponent - 1, log10_int(power_of_ten - 1));
            }
            try testing.expectEqual(exponent, log10_int(power_of_ten));
            try testing.expectEqual(exponent, log10_int(power_of_ten + 1));
            try testing.expectEqual(exponent, log10_int(power_of_ten + 8));
        }
        try testing.expectEqual(max_exponent, log10_int(@as(T, std.math.maxInt(T))));
    }
}

test "log10 with comptime types" {
    try testing.expectEqual(0, log10(2));
    try testing.expectEqual(2.0, log10(100.0));
    try testing.expectEqual(2, log10(123));
    try testing.expectEqual(3.0, log10(1000.0));
}



---
File: /std/math/log1p.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/log1pf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/log1p.c

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;

/// Returns the natural logarithm of 1 + x with greater accuracy when x is near zero.
///
/// Special Cases:
///  - log1p(+inf)  = +inf
///  - log1p(+-0)   = +-0
///  - log1p(-1)    = -inf
///  - log1p(x)     = nan if x < -1
///  - log1p(nan)   = nan
pub fn log1p(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => log1p_32(x),
        f64 => log1p_64(x),
        else => @compileError("log1p not implemented for " ++ @typeName(T)),
    };
}

fn log1p_32(x: f32) f32 {
    const ln2_hi = 6.9313812256e-01;
    const ln2_lo = 9.0580006145e-06;
    const Lg1: f32 = 0xaaaaaa.0p-24;
    const Lg2: f32 = 0xccce13.0p-25;
    const Lg3: f32 = 0x91e9ee.0p-25;
    const Lg4: f32 = 0xf89e26.0p-26;

    const u: u32 = @bitCast(x);
    const ix = u;
    var k: i32 = 1;
    var f: f32 = undefined;
    var c: f32 = undefined;

    // 1 + x < sqrt(2)+
    if (ix < 0x3ED413D0 or ix >> 31 != 0) {
        // x <= -1.0
        if (ix >= 0xBF800000) {
            // log1p(-1) = -inf
            if (x == -1.0) {
                return -math.inf(f32);
            }
            // log1p(x < -1) = nan
            else {
                return math.nan(f32);
            }
        }
        // |x| < 2^(-24)
        if ((ix << 1) < (0x33800000 << 1)) {
            // underflow if subnormal
            if (ix & 0x7F800000 == 0) {
                mem.doNotOptimizeAway(x * x);
            }
            return x;
        }
        // sqrt(2) / 2- <= 1 + x < sqrt(2)+
        if (ix <= 0xBE95F619) {
            k = 0;
            c = 0;
            f = x;
        }
    } else if (ix >= 0x7F800000) {
        return x;
    }

    if (k != 0) {
        const uf = 1 + x;
        var iu = @as(u32, @bitCast(uf));
        iu += 0x3F800000 - 0x3F3504F3;
        k = @as(i32, @intCast(iu >> 23)) - 0x7F;

        // correction to avoid underflow in c / u
        if (k < 25) {
            c = if (k >= 2) 1 - (uf - x) else x - (uf - 1);
            c /= uf;
        } else {
            c = 0;
        }

        // u into [sqrt(2)/2, sqrt(2)]
        iu = (iu & 0x007FFFFF) + 0x3F3504F3;
        f = @as(f32, @bitCast(iu)) - 1;
    }

    const s = f / (2.0 + f);
    const z = s * s;
    const w = z * z;
    const t1 = w * (Lg2 + w * Lg4);
    const t2 = z * (Lg1 + w * Lg3);
    const R = t2 + t1;
    const hfsq = 0.5 * f * f;
    const dk = @as(f32, @floatFromInt(k));

    return s * (hfsq + R) + (dk * ln2_lo + c) - hfsq + f + dk * ln2_hi;
}

fn log1p_64(x: f64) f64 {
    const ln2_hi: f64 = 6.93147180369123816490e-01;
    const ln2_lo: f64 = 1.90821492927058770002e-10;
    const Lg1: f64 = 6.666666666666735130e-01;
    const Lg2: f64 = 3.999999999940941908e-01;
    const Lg3: f64 = 2.857142874366239149e-01;
    const Lg4: f64 = 2.222219843214978396e-01;
    const Lg5: f64 = 1.818357216161805012e-01;
    const Lg6: f64 = 1.531383769920937332e-01;
    const Lg7: f64 = 1.479819860511658591e-01;

    const ix: u64 = @bitCast(x);
    const hx: u32 = @intCast(ix >> 32);
    var k: i32 = 1;
    var c: f64 = undefined;
    var f: f64 = undefined;

    // 1 + x < sqrt(2)
    if (hx < 0x3FDA827A or hx >> 31 != 0) {
        // x <= -1.0
        if (hx >= 0xBFF00000) {
            // log1p(-1) = -inf
            if (x == -1.0) {
                return -math.inf(f64);
            }
            // log1p(x < -1) = nan
            else {
                return math.nan(f64);
            }
        }
        // |x| < 2^(-53)
        if ((hx << 1) < (0x3CA00000 << 1)) {
            if ((hx & 0x7FF00000) == 0) {
                math.raiseUnderflow();
            }
            return x;
        }
        // sqrt(2) / 2- <= 1 + x < sqrt(2)+
        if (hx <= 0xBFD2BEC4) {
            k = 0;
            c = 0;
            f = x;
        }
    } else if (hx >= 0x7FF00000) {
        return x;
    }

    if (k != 0) {
        const uf = 1 + x;
        const hu = @as(u64, @bitCast(uf));
        var iu = @as(u32, @intCast(hu >> 32));
        iu += 0x3FF00000 - 0x3FE6A09E;
        k = @as(i32, @intCast(iu >> 20)) - 0x3FF;

        // correction to avoid underflow in c / u
        if (k < 54) {
            c = if (k >= 2) 1 - (uf - x) else x - (uf - 1);
            c /= uf;
        } else {
            c = 0;
        }

        // u into [sqrt(2)/2, sqrt(2)]
        iu = (iu & 0x000FFFFF) + 0x3FE6A09E;
        const iq = (@as(u64, iu) << 32) | (hu & 0xFFFFFFFF);
        f = @as(f64, @bitCast(iq)) - 1;
    }

    const hfsq = 0.5 * f * f;
    const s = f / (2.0 + f);
    const z = s * s;
    const w = z * z;
    const t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
    const t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
    const R = t2 + t1;
    const dk = @as(f64, @floatFromInt(k));

    return s * (hfsq + R) + (dk * ln2_lo + c) - hfsq + f + dk * ln2_hi;
}

test "log1p_32() special" {
    try expect(math.isPositiveZero(log1p_32(0.0)));
    try expect(math.isNegativeZero(log1p_32(-0.0)));
    try expectEqual(log1p_32(-1.0), -math.inf(f32));
    try expectEqual(log1p_32(1.0), math.ln2);
    try expectEqual(log1p_32(math.inf(f32)), math.inf(f32));
    try expect(math.isNan(log1p_32(-2.0)));
    try expect(math.isNan(log1p_32(-math.inf(f32))));
    try expect(math.isNan(log1p_32(math.nan(f32))));
    try expect(math.isNan(log1p_32(math.snan(f32))));
}

test "log1p_32() sanity" {
    try expect(math.isNan(log1p_32(-0x1.0223a0p+3)));
    try expectEqual(log1p_32(0x1.161868p+2), 0x1.ad1bdcp+0);
    try expect(math.isNan(log1p_32(-0x1.0c34b4p+3)));
    try expect(math.isNan(log1p_32(-0x1.a206f0p+2)));
    try expectEqual(log1p_32(0x1.288bbcp+3), 0x1.2a1ab8p+1);
    try expectEqual(log1p_32(0x1.52efd0p-1), 0x1.041a4ep-1);
    try expectEqual(log1p_32(-0x1.a05cc8p-2), -0x1.0b3596p-1);
    try expectEqual(log1p_32(0x1.1f9efap-1), 0x1.c88344p-2);
    try expectEqual(log1p_32(0x1.8c5db0p-1), 0x1.258a8ep-1);
    try expectEqual(log1p_32(-0x1.5b86eap-1), -0x1.22b542p+0);
}

test "log1p_32() boundary" {
    try expectEqual(log1p_32(0x1.fffffep+127), 0x1.62e430p+6); // Max input value
    try expectEqual(log1p_32(0x1p-149), 0x1p-149); // Min positive input value
    try expectEqual(log1p_32(-0x1p-149), -0x1p-149); // Min negative input value
    try expectEqual(log1p_32(0x1p-126), 0x1p-126); // First subnormal
    try expectEqual(log1p_32(-0x1p-126), -0x1p-126); // First negative subnormal
    try expectEqual(log1p_32(-0x1.fffffep-1), -0x1.0a2b24p+4); // Last value before result is -inf
    try expect(math.isNan(log1p_32(-0x1.000002p+0))); // First value where result is nan
}

test "log1p_64() special" {
    try expect(math.isPositiveZero(log1p_64(0.0)));
    try expect(math.isNegativeZero(log1p_64(-0.0)));
    try expectEqual(log1p_64(-1.0), -math.inf(f64));
    try expectEqual(log1p_64(1.0), math.ln2);
    try expectEqual(log1p_64(math.inf(f64)), math.inf(f64));
    try expect(math.isNan(log1p_64(-2.0)));
    try expect(math.isNan(log1p_64(-math.inf(f64))));
    try expect(math.isNan(log1p_64(math.nan(f64))));
    try expect(math.isNan(log1p_64(math.snan(f64))));
}

test "log1p_64() sanity" {
    try expect(math.isNan(log1p_64(-0x1.02239f3c6a8f1p+3)));
    try expectEqual(log1p_64(0x1.161868e18bc67p+2), 0x1.ad1bdd1e9e686p+0); // Disagrees with GCC in last bit
    try expect(math.isNan(log1p_64(-0x1.0c34b3e01e6e7p+3)));
    try expect(math.isNan(log1p_64(-0x1.a206f0a19dcc4p+2)));
    try expectEqual(log1p_64(0x1.288bbb0d6a1e6p+3), 0x1.2a1ab8365b56fp+1);
    try expectEqual(log1p_64(0x1.52efd0cd80497p-1), 0x1.041a4ec2a680ap-1);
    try expectEqual(log1p_64(-0x1.a05cc754481d1p-2), -0x1.0b3595423aec1p-1);
    try expectEqual(log1p_64(0x1.1f9ef934745cbp-1), 0x1.c8834348a846ep-2);
    try expectEqual(log1p_64(0x1.8c5db097f7442p-1), 0x1.258a8e8a35bbfp-1);
    try expectEqual(log1p_64(-0x1.5b86ea8118a0ep-1), -0x1.22b5426327502p+0);
}

test "log1p_64() boundary" {
    try expectEqual(log1p_64(0x1.fffffffffffffp+1023), 0x1.62e42fefa39efp+9); // Max input value
    try expectEqual(log1p_64(0x1p-1074), 0x1p-1074); // Min positive input value
    try expectEqual(log1p_64(-0x1p-1074), -0x1p-1074); // Min negative input value
    try expectEqual(log1p_64(0x1p-1022), 0x1p-1022); // First subnormal
    try expectEqual(log1p_64(-0x1p-1022), -0x1p-1022); // First negative subnormal
    try expectEqual(log1p_64(-0x1.fffffffffffffp-1), -0x1.25e4f7b2737fap+5); // Last value before result is -inf
    try expect(math.isNan(log1p_64(-0x1.0000000000001p+0))); // First value where result is nan
}



---
File: /std/math/log2.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");
const math = std.math;
const expect = std.testing.expect;

/// Returns the base-2 logarithm of x.
///
/// Special Cases:
///  - log2(+inf)  = +inf
///  - log2(0)     = -inf
///  - log2(x)     = nan if x < 0
///  - log2(nan)   = nan
pub fn log2(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (@typeInfo(T)) {
        .comptime_float, .float => @log2(x),
        .comptime_int => comptime {
            std.debug.assert(x > 0);
            var x_shifted = x;
            // First, calculate floorPowerOfTwo(x)
            var shift_amt = 1;
            while (x_shifted >> (shift_amt << 1) != 0) shift_amt <<= 1;

            // Answer is in the range [shift_amt, 2 * shift_amt - 1]
            // We can find it in O(log(N)) using binary search.
            var result = 0;
            while (shift_amt != 0) : (shift_amt >>= 1) {
                if (x_shifted >> shift_amt != 0) {
                    x_shifted >>= shift_amt;
                    result += shift_amt;
                }
            }
            return result;
        },
        .int => |int_info| math.log2_int(switch (int_info.signedness) {
            .signed => @Int(.unsigned, int_info.bits -| 1),
            .unsigned => T,
        }, @intCast(x)),
        else => @compileError("log2 not implemented for " ++ @typeName(T)),
    };
}

test log2 {
    // https://github.com/ziglang/zig/issues/13703
    if (builtin.cpu.arch == .aarch64 and builtin.os.tag == .windows) return error.SkipZigTest;

    try expect(log2(@as(f32, 0.2)) == @log2(0.2));
    try expect(log2(@as(f64, 0.2)) == @log2(0.2));
    comptime {
        try expect(log2(1) == 0);
        try expect(log2(15) == 3);
        try expect(log2(16) == 4);
        try expect(log2(1 << 4073) == 4073);
    }
}



---
File: /std/math/modf.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");
const math = std.math;
const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;
const expectApproxEqAbs = std.testing.expectApproxEqAbs;

pub fn Modf(comptime T: type) type {
    return struct {
        fpart: T,
        ipart: T,
    };
}

/// Returns the integer and fractional floating-point numbers that sum to x. The sign of each
/// result is the same as the sign of x.
/// In comptime, may be used with comptime_float
///
/// Special Cases:
///  - modf(+-inf) = +-inf, nan
///  - modf(nan)   = nan, nan
pub fn modf(x: anytype) Modf(@TypeOf(x)) {
    const ipart = @trunc(x);
    return .{
        .ipart = ipart,
        .fpart = x - ipart,
    };
}

test modf {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        const epsilon: comptime_float = @max(1e-6, math.floatEps(T));

        var r: Modf(T) = undefined;

        r = modf(@as(T, 1.0));
        try expectEqual(1.0, r.ipart);
        try expectEqual(0.0, r.fpart);

        r = modf(@as(T, 0.34682));
        try expectEqual(0.0, r.ipart);
        try expectApproxEqAbs(@as(T, 0.34682), r.fpart, epsilon);

        r = modf(@as(T, 2.54576));
        try expectEqual(2.0, r.ipart);
        try expectApproxEqAbs(0.54576, r.fpart, epsilon);

        r = modf(@as(T, 3.9782));
        try expectEqual(3.0, r.ipart);
        try expectApproxEqAbs(0.9782, r.fpart, epsilon);
    }
}

/// Generate a namespace of tests for modf on values of the given type
fn ModfTests(comptime T: type) type {
    return struct {
        test "normal" {
            const epsilon: comptime_float = @max(1e-6, math.floatEps(T));
            var r: Modf(T) = undefined;

            r = modf(@as(T, 1.0));
            try expectEqual(1.0, r.ipart);
            try expectEqual(0.0, r.fpart);

            r = modf(@as(T, 0.34682));
            try expectEqual(0.0, r.ipart);
            try expectApproxEqAbs(0.34682, r.fpart, epsilon);

            r = modf(@as(T, 3.97812));
            try expectEqual(3.0, r.ipart);
            // account for precision error
            const expected_a: T = 3.97812 - @as(T, 3);
            try expectApproxEqAbs(expected_a, r.fpart, epsilon);

            r = modf(@as(T, 43874.3));
            try expectEqual(43874.0, r.ipart);
            // account for precision error
            const expected_b: T = 43874.3 - @as(T, 43874.0);
            try expectApproxEqAbs(expected_b, r.fpart, epsilon);

            r = modf(@as(T, 1234.340780));
            try expectEqual(1234.0, r.ipart);
            // account for precision error
            const expected_c: T = 1234.340780 - @as(T, 1234);
            try expectApproxEqAbs(expected_c, r.fpart, epsilon);
        }
        test "vector" {
            if (builtin.os.tag.isDarwin() and builtin.cpu.arch == .aarch64) return error.SkipZigTest;
            if (builtin.cpu.arch == .s390x) return error.SkipZigTest;
            if (comptime builtin.cpu.has(.loongarch, .lsx)) return error.SkipZigTest; // https://github.com/llvm/llvm-project/issues/159529

            const widths = [_]comptime_int{ 1, 2, 3, 4, 8, 16 };

            inline for (widths) |len| {
                const V: type = @Vector(len, T);
                var r: Modf(V) = undefined;

                r = modf(@as(V, @splat(1.0)));
                try expectEqual(@as(V, @splat(1.0)), r.ipart);
                try expectEqual(@as(V, @splat(0.0)), r.fpart);

                r = modf(@as(V, @splat(2.75)));
                try expectEqual(@as(V, @splat(2.0)), r.ipart);
                try expectEqual(@as(V, @splat(0.75)), r.fpart);

                r = modf(@as(V, @splat(0.2)));
                try expectEqual(@as(V, @splat(0.0)), r.ipart);
                try expectEqual(@as(V, @splat(0.2)), r.fpart);

                r = modf(std.simd.iota(T, len) + @as(V, @splat(0.5)));
                try expectEqual(std.simd.iota(T, len), r.ipart);
                try expectEqual(@as(V, @splat(0.5)), r.fpart);
            }
        }
        test "inf" {
            var r: Modf(T) = undefined;

            r = modf(math.inf(T));
            try expect(math.isPositiveInf(r.ipart) and math.isNan(r.fpart));

            r = modf(-math.inf(T));
            try expect(math.isNegativeInf(r.ipart) and math.isNan(r.fpart));
        }
        test "nan" {
            const r: Modf(T) = modf(math.nan(T));
            try expect(math.isNan(r.ipart) and math.isNan(r.fpart));
        }
    };
}

comptime {
    for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        _ = ModfTests(T);
    }
}



---
File: /std/math/nextafter.zig
---

const std = @import("../std.zig");
const math = std.math;
const assert = std.debug.assert;
const expect = std.testing.expect;

/// Returns the next representable value after `x` in the direction of `y`.
///
/// Special cases:
///
/// - If `x == y`, `y` is returned.
/// - For floats, if either `x` or `y` is a NaN, a NaN is returned.
/// - For floats, if `x == 0.0` and `@abs(y) > 0.0`, the smallest subnormal number with the sign of
///   `y` is returned.
///
pub fn nextAfter(comptime T: type, x: T, y: T) T {
    return switch (@typeInfo(T)) {
        .int, .comptime_int => nextAfterInt(T, x, y),
        .float => nextAfterFloat(T, x, y),
        else => @compileError("expected int or non-comptime float, found '" ++ @typeName(T) ++ "'"),
    };
}

fn nextAfterInt(comptime T: type, x: T, y: T) T {
    comptime assert(@typeInfo(T) == .int or @typeInfo(T) == .comptime_int);
    return if (@typeInfo(T) == .int and @bitSizeOf(T) < 2)
        // Special case for `i0`, `u0`, `i1`, and `u1`.
        y
    else if (y > x)
        x + 1
    else if (y < x)
        x - 1
    else
        y;
}

// Based on nextafterf/nextafterl from mingw-w64 which are both public domain.
// <https://github.com/mingw-w64/mingw-w64/blob/e89de847dd3e05bb8e46344378ce3e124f4e7d1c/mingw-w64-crt/math/nextafterf.c>
// <https://github.com/mingw-w64/mingw-w64/blob/e89de847dd3e05bb8e46344378ce3e124f4e7d1c/mingw-w64-crt/math/nextafterl.c>

fn nextAfterFloat(comptime T: type, x: T, y: T) T {
    comptime assert(@typeInfo(T) == .float);
    if (x == y) {
        // Returning `y` ensures that (0.0, -0.0) returns -0.0 and that (-0.0, 0.0) returns 0.0.
        return y;
    }
    if (math.isNan(x) or math.isNan(y)) {
        return math.nan(T);
    }
    if (x == 0.0) {
        return if (y > 0.0)
            math.floatTrueMin(T)
        else
            -math.floatTrueMin(T);
    }
    if (@bitSizeOf(T) == 80) {
        // Unlike other floats, `f80` has an explicitly stored integer bit between the fractional
        // part and the exponent and thus requires special handling. This integer bit *must* be set
        // when the value is normal, an infinity or a NaN and *should* be cleared otherwise.

        const fractional_bits_mask = (1 << math.floatFractionalBits(f80)) - 1;
        const integer_bit_mask = 1 << math.floatFractionalBits(f80);
        const exponent_bits_mask = (1 << math.floatExponentBits(f80)) - 1;

        var x_parts = math.F80.fromFloat(x);

        // Bitwise increment/decrement the fractional part while also taking care to update the
        // exponent if we overflow the fractional part. This might flip the integer bit; this is
        // intentional.
        if ((x > 0.0) == (y > x)) {
            x_parts.fraction +%= 1;
            if (x_parts.fraction & fractional_bits_mask == 0) {
                x_parts.exp += 1;
            }
        } else {
            if (x_parts.fraction & fractional_bits_mask == 0) {
                x_parts.exp -= 1;
            }
            x_parts.fraction -%= 1;
        }

        // If the new value is normal or an infinity (indicated by at least one bit in the exponent
        // being set), the integer bit might have been cleared from an overflow, so we must ensure
        // that it remains set.
        if (x_parts.exp & exponent_bits_mask != 0) {
            x_parts.fraction |= integer_bit_mask;
        }
        // Otherwise, the new value is subnormal and the integer bit will have either flipped from
        // set to cleared (if the old value was normal) or remained cleared (if the old value was
        // subnormal), both of which are the outcomes we want.

        return x_parts.toFloat();
    } else {
        const Bits = std.meta.Int(.unsigned, @bitSizeOf(T));
        var x_bits: Bits = @bitCast(x);
        if ((x > 0.0) == (y > x)) {
            x_bits += 1;
        } else {
            x_bits -= 1;
        }
        return @bitCast(x_bits);
    }
}

test "int" {
    try expect(nextAfter(i0, 0, 0) == 0);
    try expect(nextAfter(u0, 0, 0) == 0);
    try expect(nextAfter(i1, 0, 0) == 0);
    try expect(nextAfter(i1, 0, -1) == -1);
    try expect(nextAfter(i1, -1, -1) == -1);
    try expect(nextAfter(i1, -1, 0) == 0);
    try expect(nextAfter(u1, 0, 0) == 0);
    try expect(nextAfter(u1, 0, 1) == 1);
    try expect(nextAfter(u1, 1, 1) == 1);
    try expect(nextAfter(u1, 1, 0) == 0);
    inline for (.{ i8, i16, i32, i64, i128, i333 }) |T| {
        try expect(nextAfter(T, 3, 7) == 4);
        try expect(nextAfter(T, 3, -7) == 2);
        try expect(nextAfter(T, -3, -7) == -4);
        try expect(nextAfter(T, -3, 7) == -2);
        try expect(nextAfter(T, 5, 5) == 5);
        try expect(nextAfter(T, -5, -5) == -5);
        try expect(nextAfter(T, 0, 0) == 0);
        try expect(nextAfter(T, math.minInt(T), math.minInt(T)) == math.minInt(T));
        try expect(nextAfter(T, math.maxInt(T), math.maxInt(T)) == math.maxInt(T));
    }
    inline for (.{ u8, u16, u32, u64, u128, u333 }) |T| {
        try expect(nextAfter(T, 3, 7) == 4);
        try expect(nextAfter(T, 7, 3) == 6);
        try expect(nextAfter(T, 5, 5) == 5);
        try expect(nextAfter(T, 0, 0) == 0);
        try expect(nextAfter(T, math.minInt(T), math.minInt(T)) == math.minInt(T));
        try expect(nextAfter(T, math.maxInt(T), math.maxInt(T)) == math.maxInt(T));
    }
    comptime {
        try expect(nextAfter(comptime_int, 3, 7) == 4);
        try expect(nextAfter(comptime_int, 3, -7) == 2);
        try expect(nextAfter(comptime_int, -3, -7) == -4);
        try expect(nextAfter(comptime_int, -3, 7) == -2);
        try expect(nextAfter(comptime_int, 5, 5) == 5);
        try expect(nextAfter(comptime_int, -5, -5) == -5);
        try expect(nextAfter(comptime_int, 0, 0) == 0);
        try expect(nextAfter(comptime_int, math.maxInt(u512), math.maxInt(u512)) == math.maxInt(u512));
    }
}

test "float" {
    @setEvalBranchQuota(4000);

    // normal -> normal
    try expect(nextAfter(f16, 0x1.234p0, 2.0) == 0x1.238p0);
    try expect(nextAfter(f16, 0x1.234p0, -2.0) == 0x1.230p0);
    try expect(nextAfter(f16, 0x1.234p0, 0x1.234p0) == 0x1.234p0);
    try expect(nextAfter(f16, -0x1.234p0, -2.0) == -0x1.238p0);
    try expect(nextAfter(f16, -0x1.234p0, 2.0) == -0x1.230p0);
    try expect(nextAfter(f16, -0x1.234p0, -0x1.234p0) == -0x1.234p0);
    try expect(nextAfter(f32, 0x1.001234p0, 2.0) == 0x1.001236p0);
    try expect(nextAfter(f32, 0x1.001234p0, -2.0) == 0x1.001232p0);
    try expect(nextAfter(f32, 0x1.001234p0, 0x1.001234p0) == 0x1.001234p0);
    try expect(nextAfter(f32, -0x1.001234p0, -2.0) == -0x1.001236p0);
    try expect(nextAfter(f32, -0x1.001234p0, 2.0) == -0x1.001232p0);
    try expect(nextAfter(f32, -0x1.001234p0, -0x1.001234p0) == -0x1.001234p0);
    inline for (.{f64} ++ if (@bitSizeOf(c_longdouble) == 64) .{c_longdouble} else .{}) |T64| {
        try expect(nextAfter(T64, 0x1.0000000001234p0, 2.0) == 0x1.0000000001235p0);
        try expect(nextAfter(T64, 0x1.0000000001234p0, -2.0) == 0x1.0000000001233p0);
        try expect(nextAfter(T64, 0x1.0000000001234p0, 0x1.0000000001234p0) == 0x1.0000000001234p0);
        try expect(nextAfter(T64, -0x1.0000000001234p0, -2.0) == -0x1.0000000001235p0);
        try expect(nextAfter(T64, -0x1.0000000001234p0, 2.0) == -0x1.0000000001233p0);
        try expect(nextAfter(T64, -0x1.0000000001234p0, -0x1.0000000001234p0) == -0x1.0000000001234p0);
    }
    inline for (.{f80} ++ if (@bitSizeOf(c_longdouble) == 80) .{c_longdouble} else .{}) |T80| {
        try expect(nextAfter(T80, 0x1.0000000000001234p0, 2.0) == 0x1.0000000000001236p0);
        try expect(nextAfter(T80, 0x1.0000000000001234p0, -2.0) == 0x1.0000000000001232p0);
        try expect(nextAfter(T80, 0x1.0000000000001234p0, 0x1.0000000000001234p0) == 0x1.0000000000001234p0);
        try expect(nextAfter(T80, -0x1.0000000000001234p0, -2.0) == -0x1.0000000000001236p0);
        try expect(nextAfter(T80, -0x1.0000000000001234p0, 2.0) == -0x1.0000000000001232p0);
        try expect(nextAfter(T80, -0x1.0000000000001234p0, -0x1.0000000000001234p0) == -0x1.0000000000001234p0);
    }
    inline for (.{f128} ++ if (@bitSizeOf(c_longdouble) == 128) .{c_longdouble} else .{}) |T128| {
        try expect(nextAfter(T128, 0x1.0000000000000000000000001234p0, 2.0) == 0x1.0000000000000000000000001235p0);
        try expect(nextAfter(T128, 0x1.0000000000000000000000001234p0, -2.0) == 0x1.0000000000000000000000001233p0);
        try expect(nextAfter(T128, 0x1.0000000000000000000000001234p0, 0x1.0000000000000000000000001234p0) == 0x1.0000000000000000000000001234p0);
        try expect(nextAfter(T128, -0x1.0000000000000000000000001234p0, -2.0) == -0x1.0000000000000000000000001235p0);
        try expect(nextAfter(T128, -0x1.0000000000000000000000001234p0, 2.0) == -0x1.0000000000000000000000001233p0);
        try expect(nextAfter(T128, -0x1.0000000000000000000000001234p0, -0x1.0000000000000000000000001234p0) == -0x1.0000000000000000000000001234p0);
    }

    // subnormal -> subnormal
    try expect(nextAfter(f16, 0x0.234p-14, 1.0) == 0x0.238p-14);
    try expect(nextAfter(f16, 0x0.234p-14, -1.0) == 0x0.230p-14);
    try expect(nextAfter(f16, 0x0.234p-14, 0x0.234p-14) == 0x0.234p-14);
    try expect(nextAfter(f16, -0x0.234p-14, -1.0) == -0x0.238p-14);
    try expect(nextAfter(f16, -0x0.234p-14, 1.0) == -0x0.230p-14);
    try expect(nextAfter(f16, -0x0.234p-14, -0x0.234p-14) == -0x0.234p-14);
    try expect(nextAfter(f32, 0x0.001234p-126, 1.0) == 0x0.001236p-126);
    try expect(nextAfter(f32, 0x0.001234p-126, -1.0) == 0x0.001232p-126);
    try expect(nextAfter(f32, 0x0.001234p-126, 0x0.001234p-126) == 0x0.001234p-126);
    try expect(nextAfter(f32, -0x0.001234p-126, -1.0) == -0x0.001236p-126);
    try expect(nextAfter(f32, -0x0.001234p-126, 1.0) == -0x0.001232p-126);
    try expect(nextAfter(f32, -0x0.001234p-126, -0x0.001234p-126) == -0x0.001234p-126);
    inline for (.{f64} ++ if (@bitSizeOf(c_longdouble) == 64) .{c_longdouble} else .{}) |T64| {
        try expect(nextAfter(T64, 0x0.0000000001234p-1022, 1.0) == 0x0.0000000001235p-1022);
        try expect(nextAfter(T64, 0x0.0000000001234p-1022, -1.0) == 0x0.0000000001233p-1022);
        try expect(nextAfter(T64, 0x0.0000000001234p-1022, 0x0.0000000001234p-1022) == 0x0.0000000001234p-1022);
        try expect(nextAfter(T64, -0x0.0000000001234p-1022, -1.0) == -0x0.0000000001235p-1022);
        try expect(nextAfter(T64, -0x0.0000000001234p-1022, 1.0) == -0x0.0000000001233p-1022);
        try expect(nextAfter(T64, -0x0.0000000001234p-1022, -0x0.0000000001234p-1022) == -0x0.0000000001234p-1022);
    }
    inline for (.{f80} ++ if (@bitSizeOf(c_longdouble) == 80) .{c_longdouble} else .{}) |T80| {
        try expect(nextAfter(T80, 0x0.0000000000001234p-16382, 1.0) == 0x0.0000000000001236p-16382);
        try expect(nextAfter(T80, 0x0.0000000000001234p-16382, -1.0) == 0x0.0000000000001232p-16382);
        try expect(nextAfter(T80, 0x0.0000000000001234p-16382, 0x0.0000000000001234p-16382) == 0x0.0000000000001234p-16382);
        try expect(nextAfter(T80, -0x0.0000000000001234p-16382, -1.0) == -0x0.0000000000001236p-16382);
        try expect(nextAfter(T80, -0x0.0000000000001234p-16382, 1.0) == -0x0.0000000000001232p-16382);
        try expect(nextAfter(T80, -0x0.0000000000001234p-16382, -0x0.0000000000001234p-16382) == -0x0.0000000000001234p-16382);
    }
    inline for (.{f128} ++ if (@bitSizeOf(c_longdouble) == 128) .{c_longdouble} else .{}) |T128| {
        try expect(nextAfter(T128, 0x0.0000000000000000000000001234p-16382, 1.0) == 0x0.0000000000000000000000001235p-16382);
        try expect(nextAfter(T128, 0x0.0000000000000000000000001234p-16382, -1.0) == 0x0.0000000000000000000000001233p-16382);
        try expect(nextAfter(T128, 0x0.0000000000000000000000001234p-16382, 0x0.0000000000000000000000001234p-16382) == 0x0.0000000000000000000000001234p-16382);
        try expect(nextAfter(T128, -0x0.0000000000000000000000001234p-16382, -1.0) == -0x0.0000000000000000000000001235p-16382);
        try expect(nextAfter(T128, -0x0.0000000000000000000000001234p-16382, 1.0) == -0x0.0000000000000000000000001233p-16382);
        try expect(nextAfter(T128, -0x0.0000000000000000000000001234p-16382, -0x0.0000000000000000000000001234p-16382) == -0x0.0000000000000000000000001234p-16382);
    }

    // normal -> normal (change in exponent)
    try expect(nextAfter(f16, 0x1.FFCp3, math.inf(f16)) == 0x1p4);
    try expect(nextAfter(f16, 0x1p4, -math.inf(f16)) == 0x1.FFCp3);
    try expect(nextAfter(f16, -0x1.FFCp3, -math.inf(f16)) == -0x1p4);
    try expect(nextAfter(f16, -0x1p4, math.inf(f16)) == -0x1.FFCp3);
    try expect(nextAfter(f32, 0x1.FFFFFEp3, math.inf(f32)) == 0x1p4);
    try expect(nextAfter(f32, 0x1p4, -math.inf(f32)) == 0x1.FFFFFEp3);
    try expect(nextAfter(f32, -0x1.FFFFFEp3, -math.inf(f32)) == -0x1p4);
    try expect(nextAfter(f32, -0x1p4, math.inf(f32)) == -0x1.FFFFFEp3);
    inline for (.{f64} ++ if (@bitSizeOf(c_longdouble) == 64) .{c_longdouble} else .{}) |T64| {
        try expect(nextAfter(T64, 0x1.FFFFFFFFFFFFFp3, math.inf(T64)) == 0x1p4);
        try expect(nextAfter(T64, 0x1p4, -math.inf(T64)) == 0x1.FFFFFFFFFFFFFp3);
        try expect(nextAfter(T64, -0x1.FFFFFFFFFFFFFp3, -math.inf(T64)) == -0x1p4);
        try expect(nextAfter(T64, -0x1p4, math.inf(T64)) == -0x1.FFFFFFFFFFFFFp3);
    }
    inline for (.{f80} ++ if (@bitSizeOf(c_longdouble) == 80) .{c_longdouble} else .{}) |T80| {
        try expect(nextAfter(T80, 0x1.FFFFFFFFFFFFFFFEp3, math.inf(T80)) == 0x1p4);
        try expect(nextAfter(T80, 0x1p4, -math.inf(T80)) == 0x1.FFFFFFFFFFFFFFFEp3);
        try expect(nextAfter(T80, -0x1.FFFFFFFFFFFFFFFEp3, -math.inf(T80)) == -0x1p4);
        try expect(nextAfter(T80, -0x1p4, math.inf(T80)) == -0x1.FFFFFFFFFFFFFFFEp3);
    }
    inline for (.{f128} ++ if (@bitSizeOf(c_longdouble) == 128) .{c_longdouble} else .{}) |T128| {
        try expect(nextAfter(T128, 0x1.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp3, math.inf(T128)) == 0x1p4);
        try expect(nextAfter(T128, 0x1p4, -math.inf(T128)) == 0x1.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp3);
        try expect(nextAfter(T128, -0x1.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp3, -math.inf(T128)) == -0x1p4);
        try expect(nextAfter(T128, -0x1p4, math.inf(T128)) == -0x1.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp3);
    }

    // normal -> subnormal
    try expect(nextAfter(f16, 0x1p-14, -math.inf(f16)) == 0x0.FFCp-14);
    try expect(nextAfter(f16, -0x1p-14, math.inf(f16)) == -0x0.FFCp-14);
    try expect(nextAfter(f32, 0x1p-126, -math.inf(f32)) == 0x0.FFFFFEp-126);
    try expect(nextAfter(f32, -0x1p-126, math.inf(f32)) == -0x0.FFFFFEp-126);
    inline for (.{f64} ++ if (@bitSizeOf(c_longdouble) == 64) .{c_longdouble} else .{}) |T64| {
        try expect(nextAfter(T64, 0x1p-1022, -math.inf(T64)) == 0x0.FFFFFFFFFFFFFp-1022);
        try expect(nextAfter(T64, -0x1p-1022, math.inf(T64)) == -0x0.FFFFFFFFFFFFFp-1022);
    }
    inline for (.{f80} ++ if (@bitSizeOf(c_longdouble) == 80) .{c_longdouble} else .{}) |T80| {
        try expect(nextAfter(T80, 0x1p-16382, -math.inf(T80)) == 0x0.FFFFFFFFFFFFFFFEp-16382);
        try expect(nextAfter(T80, -0x1p-16382, math.inf(T80)) == -0x0.FFFFFFFFFFFFFFFEp-16382);
    }
    inline for (.{f128} ++ if (@bitSizeOf(c_longdouble) == 128) .{c_longdouble} else .{}) |T128| {
        try expect(nextAfter(T128, 0x1p-16382, -math.inf(T128)) == 0x0.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp-16382);
        try expect(nextAfter(T128, -0x1p-16382, math.inf(T128)) == -0x0.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp-16382);
    }

    // subnormal -> normal
    try expect(nextAfter(f16, 0x0.FFCp-14, math.inf(f16)) == 0x1p-14);
    try expect(nextAfter(f16, -0x0.FFCp-14, -math.inf(f16)) == -0x1p-14);
    try expect(nextAfter(f32, 0x0.FFFFFEp-126, math.inf(f32)) == 0x1p-126);
    try expect(nextAfter(f32, -0x0.FFFFFEp-126, -math.inf(f32)) == -0x1p-126);
    inline for (.{f64} ++ if (@bitSizeOf(c_longdouble) == 64) .{c_longdouble} else .{}) |T64| {
        try expect(nextAfter(T64, 0x0.FFFFFFFFFFFFFp-1022, math.inf(T64)) == 0x1p-1022);
        try expect(nextAfter(T64, -0x0.FFFFFFFFFFFFFp-1022, -math.inf(T64)) == -0x1p-1022);
    }
    inline for (.{f80} ++ if (@bitSizeOf(c_longdouble) == 80) .{c_longdouble} else .{}) |T80| {
        try expect(nextAfter(T80, 0x0.FFFFFFFFFFFFFFFEp-16382, math.inf(T80)) == 0x1p-16382);
        try expect(nextAfter(T80, -0x0.FFFFFFFFFFFFFFFEp-16382, -math.inf(T80)) == -0x1p-16382);
    }
    inline for (.{f128} ++ if (@bitSizeOf(c_longdouble) == 128) .{c_longdouble} else .{}) |T128| {
        try expect(nextAfter(T128, 0x0.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp-16382, math.inf(T128)) == 0x1p-16382);
        try expect(nextAfter(T128, -0x0.FFFFFFFFFFFFFFFFFFFFFFFFFFFFp-16382, -math.inf(T128)) == -0x1p-16382);
    }

    // special values
    inline for (.{ f16, f32, f64, f80, f128, c_longdouble }) |T| {
        try expect(bitwiseEqual(T, nextAfter(T, 0.0, 0.0), 0.0));
        try expect(bitwiseEqual(T, nextAfter(T, 0.0, -0.0), -0.0));
        try expect(bitwiseEqual(T, nextAfter(T, -0.0, -0.0), -0.0));
        try expect(bitwiseEqual(T, nextAfter(T, -0.0, 0.0), 0.0));
        try expect(nextAfter(T, 0.0, math.inf(T)) == math.floatTrueMin(T));
        try expect(nextAfter(T, 0.0, -math.inf(T)) == -math.floatTrueMin(T));
        try expect(nextAfter(T, -0.0, -math.inf(T)) == -math.floatTrueMin(T));
        try expect(nextAfter(T, -0.0, math.inf(T)) == math.floatTrueMin(T));
        try expect(bitwiseEqual(T, nextAfter(T, math.floatTrueMin(T), 0.0), 0.0));
        try expect(bitwiseEqual(T, nextAfter(T, math.floatTrueMin(T), -0.0), 0.0));
        try expect(bitwiseEqual(T, nextAfter(T, math.floatTrueMin(T), -math.inf(T)), 0.0));
        try expect(bitwiseEqual(T, nextAfter(T, -math.floatTrueMin(T), -0.0), -0.0));
        try expect(bitwiseEqual(T, nextAfter(T, -math.floatTrueMin(T), 0.0), -0.0));
        try expect(bitwiseEqual(T, nextAfter(T, -math.floatTrueMin(T), math.inf(T)), -0.0));
        try expect(nextAfter(T, math.inf(T), math.inf(T)) == math.inf(T));
        try expect(nextAfter(T, math.inf(T), -math.inf(T)) == math.floatMax(T));
        try expect(nextAfter(T, math.floatMax(T), math.inf(T)) == math.inf(T));
        try expect(nextAfter(T, -math.inf(T), -math.inf(T)) == -math.inf(T));
        try expect(nextAfter(T, -math.inf(T), math.inf(T)) == -math.floatMax(T));
        try expect(nextAfter(T, -math.floatMax(T), -math.inf(T)) == -math.inf(T));
        try expect(math.isNan(nextAfter(T, 1.0, math.nan(T))));
        try expect(math.isNan(nextAfter(T, math.nan(T), 1.0)));
        try expect(math.isNan(nextAfter(T, math.nan(T), math.nan(T))));
        try expect(math.isNan(nextAfter(T, math.inf(T), math.nan(T))));
        try expect(math.isNan(nextAfter(T, -math.inf(T), math.nan(T))));
        try expect(math.isNan(nextAfter(T, math.nan(T), math.inf(T))));
        try expect(math.isNan(nextAfter(T, math.nan(T), -math.inf(T))));
    }
}

/// Helps ensure that 0.0 doesn't compare equal to -0.0.
fn bitwiseEqual(comptime T: type, x: T, y: T) bool {
    comptime assert(@typeInfo(T) == .float);
    const Bits = std.meta.Int(.unsigned, @bitSizeOf(T));
    return @as(Bits, @bitCast(x)) == @as(Bits, @bitCast(y));
}



---
File: /std/math/pow.zig
---

// Ported from go, which is licensed under a BSD-3 license.
// https://golang.org/LICENSE
//
// https://golang.org/src/math/pow.go

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns x raised to the power of y (x^y).
///
/// Special Cases:
///  - pow(x, +-0)    = 1 for any x
///  - pow(1, y)      = 1 for any y
///  - pow(x, 1)      = x for any x
///  - pow(nan, y)    = nan for any y != 0
///  - pow(x, nan)    = nan for any x != 1
///  - pow(+-0, y)    = +-inf for y an odd integer < 0
///  - pow(+-0, -inf) = +inf
///  - pow(+-0, +inf) = +0
///  - pow(+-0, y)    = +inf for finite y < 0 and not an odd integer
///  - pow(+-0, y)    = +-0 for finite y an odd integer > 0
///  - pow(+-0, y)    = +0 for finite y > 0 and not an odd integer
///  - pow(-1, +-inf) = 1
///  - pow(x, +inf)   = +inf for |x| > 1
///  - pow(x, -inf)   = +0 for |x| > 1
///  - pow(x, +inf)   = +0 for |x| < 1
///  - pow(x, -inf)   = +inf for |x| < 1
///  - pow(+inf, y)   = +inf for y > 0
///  - pow(+inf, y)   = +0 for y < 0
///  - pow(-inf, y)   = pow(-0, -y)
///  - pow(x, y)      = nan for finite x < 0 and finite non-integer y
pub fn pow(comptime T: type, x: T, y: T) T {
    if (@typeInfo(T) == .int) {
        return math.powi(T, x, y) catch unreachable;
    }

    if (T != f32 and T != f64) {
        @compileError("pow not implemented for " ++ @typeName(T));
    }

    // pow(x, +-0) = 1      for any x
    // pow(1, y) = 1        for any y
    if (y == 0 or x == 1) {
        return 1;
    }

    // pow(nan, y) = nan    for any y != 0
    // pow(x, nan) = nan    for any x != 1
    if (math.isNan(x) or math.isNan(y)) {
        @branchHint(.unlikely);
        return math.nan(T);
    }

    // pow(x, 1) = x        for any x
    if (y == 1) {
        return x;
    }

    if (x == 0) {
        if (y < 0) {
            // pow(+-0, y) = +-inf  for y an odd integer
            if (isOddInteger(y)) {
                return math.copysign(math.inf(T), x);
            }
            // pow(+-0, y) = +inf   for y an even integer
            else {
                return math.inf(T);
            }
        } else {
            if (isOddInteger(y)) {
                return x;
            } else {
                return 0;
            }
        }
    }

    if (math.isInf(y)) {
        // pow(-1, inf) = 1
        if (x == -1) {
            return 1.0;
        }
        // pow(x, +inf) = +0    for |x| < 1
        // pow(x, -inf) = +0    for |x| > 1
        else if ((@abs(x) < 1) == math.isPositiveInf(y)) {
            return 0;
        }
        // pow(x, -inf) = +inf  for |x| < 1
        // pow(x, +inf) = +inf  for |x| > 1
        else {
            return math.inf(T);
        }
    }

    if (math.isInf(x)) {
        if (math.isNegativeInf(x)) {
            return pow(T, 1 / x, -y);
        }
        // pow(+inf, y) = +0    for y < 0
        else if (y < 0) {
            return 0;
        }
        // pow(+inf, y) = +0    for y > 0
        else if (y > 0) {
            return math.inf(T);
        }
    }

    // special case sqrt
    if (y == 0.5) {
        return @sqrt(x);
    }

    if (y == -0.5) {
        return 1 / @sqrt(x);
    }

    const r1 = math.modf(@abs(y));
    var yi = r1.ipart;
    var yf = r1.fpart;

    if (yf != 0 and x < 0) {
        return math.nan(T);
    }
    if (yi >= 1 << (@typeInfo(T).float.bits - 1)) {
        // yi is a large even int, so the result is always positive
        // and the sign of x doesn't matter
        return @exp(y * @log(@abs(x)));
    }

    // a = a1 * 2^ae
    var a1: T = 1.0;
    var ae: i32 = 0;

    // a *= x^yf
    if (yf != 0) {
        if (yf > 0.5) {
            yf -= 1;
            yi += 1;
        }
        a1 = @exp(yf * @log(x));
    }

    // a *= x^yi
    const r2 = math.frexp(x);
    var xe = r2.exponent;
    var x1 = r2.significand;

    var i = @as(std.meta.Int(.signed, @typeInfo(T).float.bits), @intFromFloat(yi));
    while (i != 0) : (i >>= 1) {
        const overflow_shift = math.floatExponentBits(T) + 1;
        if (xe < -(1 << overflow_shift) or (1 << overflow_shift) < xe) {
            // catch xe before it overflows the left shift below
            // Since i != 0 it has at least one bit still set, so ae will accumulate xe
            // on at least one more iteration, ae += xe is a lower bound on ae
            // the lower bound on ae exceeds the size of a float exp
            // so the final call to Ldexp will produce under/overflow (0/Inf)
            ae += xe;
            break;
        }
        if (i & 1 == 1) {
            a1 *= x1;
            ae += xe;
        }
        x1 *= x1;
        xe <<= 1;
        if (x1 < 0.5) {
            x1 += x1;
            xe -= 1;
        }
    }

    // a *= a1 * 2^ae
    if (y < 0) {
        a1 = 1 / a1;
        ae = -ae;
    }

    return math.scalbn(a1, ae);
}

fn isOddInteger(x: f64) bool {
    if (@abs(x) >= 1 << 53) {
        // From https://golang.org/src/math/pow.go
        // 1 << 53 is the largest exact integer in the float64 format.
        // Any number outside this range will be truncated before the decimal point and therefore will always be
        // an even integer.
        // Without this check and if x overflows i64 the @intFromFloat(r.ipart) conversion below will panic
        return false;
    }
    const r = math.modf(x);
    return r.fpart == 0.0 and @as(i64, @intFromFloat(r.ipart)) & 1 == 1;
}

test isOddInteger {
    try expect(isOddInteger(@floatFromInt(math.maxInt(i64) * 2)) == false);
    try expect(isOddInteger(@floatFromInt(math.maxInt(i64) * 2 + 1)) == false);
    try expect(isOddInteger(1 << 53) == false);
    try expect(isOddInteger(12.0) == false);
    try expect(isOddInteger(15.0) == true);
}

test pow {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, pow(f32, 0.0, 3.3), 0.0, epsilon));
    try expect(math.approxEqAbs(f32, pow(f32, 0.8923, 3.3), 0.686572, epsilon));
    try expect(math.approxEqAbs(f32, pow(f32, 0.2, 3.3), 0.004936, epsilon));
    try expect(math.approxEqAbs(f32, pow(f32, 1.5, 3.3), 3.811546, epsilon));
    try expect(math.approxEqAbs(f32, pow(f32, 37.45, 3.3), 155736.703125, epsilon));
    try expect(math.approxEqAbs(f32, pow(f32, 89.123, 3.3), 2722489.5, epsilon));
    try expect(math.approxEqAbs(f32, pow(f32, -1.0, 1e10), 1.0, epsilon));

    try expect(math.approxEqAbs(f64, pow(f64, 0.0, 3.3), 0.0, epsilon));
    try expect(math.approxEqAbs(f64, pow(f64, 0.8923, 3.3), 0.686572, epsilon));
    try expect(math.approxEqAbs(f64, pow(f64, 0.2, 3.3), 0.004936, epsilon));
    try expect(math.approxEqAbs(f64, pow(f64, 1.5, 3.3), 3.811546, epsilon));
    try expect(math.approxEqAbs(f64, pow(f64, 37.45, 3.3), 155736.7160616, epsilon));
    try expect(math.approxEqAbs(f64, pow(f64, 89.123, 3.3), 2722490.231436, epsilon));
    try expect(math.approxEqAbs(f64, pow(f64, -1.0, 1e20), 1.0, epsilon));
}

test "special" {
    // pow(x, +-0)    = 1 for any x
    try expect(pow(f32, 4, 0.0) == 1.0);
    try expect(pow(f32, 7, -0.0) == 1.0);
    try expect(pow(f32, math.nan(f32), -0.0) == 1.0);
    // pow(1, y)      = 1 for any y
    try expect(pow(f32, 1.0, 4) == 1.0);
    try expect(pow(f32, 1.0, 7) == 1.0);
    try expect(pow(f32, 1.0, -math.inf(f32)) == 1.0);
    try expect(pow(f32, 1.0, math.nan(f32)) == 1.0);
    // pow(x, 1)      = x for any x
    try expect(pow(f32, 45, 1.0) == 45);
    try expect(pow(f32, -45, 1.0) == -45);
    try expect(math.isPositiveZero(pow(f32, 0.0, 1.0)));
    try expect(math.isNegativeZero(pow(f32, -0.0, 1.0)));
    try expect(math.isPositiveInf(pow(f32, math.inf(f32), 1.0)));
    try expect(math.isNan(pow(f32, math.nan(f32), 1.0)));
    // pow(nan, y)    = nan for any y != 0
    try expect(math.isNan(pow(f32, math.nan(f32), 5.0)));
    // pow(x, nan)    = nan for any x != 1
    try expect(math.isNan(pow(f32, 5.0, math.nan(f32))));
    // pow(+-0, y)    = +-inf for y an odd integer < 0
    try expect(math.isPositiveInf(pow(f32, 0.0, -1.0)));
    try expect(math.isNegativeInf(pow(f32, -0.0, -5.0)));
    // pow(+-0, -inf) = +inf
    try expect(math.isPositiveInf(pow(f32, 0.0, -math.inf(f32))));
    try expect(math.isPositiveInf(pow(f32, -0.0, -math.inf(f32))));
    // pow(+-0, +inf) = +0
    try expect(math.isPositiveZero(pow(f32, 0.0, math.inf(f32))));
    try expect(math.isPositiveZero(pow(f32, -0.0, math.inf(f32))));
    // pow(+-0, y)    = +inf for finite y < 0 and not an odd integer
    try expect(math.isPositiveInf(pow(f32, 0.0, -2.0)));
    try expect(math.isPositiveInf(pow(f32, -0.0, -2.0)));
    try expect(math.isPositiveInf(pow(f32, 0.0, -5.2)));
    try expect(math.isPositiveInf(pow(f32, -0.0, -0.5)));
    // pow(+-0, y)    = +-0 for finite y an odd integer > 0
    try expect(math.isPositiveZero(pow(f32, 0.0, 3.0)));
    try expect(math.isNegativeZero(pow(f32, -0.0, 5.0)));
    // pow(+-0, y)    = +0 for finite y > 0 and not an odd integer
    try expect(math.isPositiveZero(pow(f32, 0.0, 2.0)));
    try expect(math.isPositiveZero(pow(f32, -0.0, 2.0)));
    try expect(math.isPositiveZero(pow(f32, 0.0, 5.2)));
    try expect(math.isPositiveZero(pow(f32, -0.0, 0.5)));
    // pow(-1, +-inf) = 1
    try expect(pow(f32, -1.0, math.inf(f32)) == 1.0);
    try expect(pow(f32, -1.0, -math.inf(f32)) == 1.0);
    // pow(x, +inf)   = +inf for |x| > 1
    try expect(math.isPositiveInf(pow(f32, 1.2, math.inf(f32))));
    try expect(math.isPositiveInf(pow(f32, -1.2, math.inf(f32))));
    // pow(x, -inf)   = +0 for |x| > 1
    try expect(math.isPositiveZero(pow(f32, 1.2, -math.inf(f32))));
    try expect(math.isPositiveZero(pow(f32, -1.2, -math.inf(f32))));
    // pow(x, +inf)   = +0 for |x| < 1
    try expect(math.isPositiveZero(pow(f32, 0.2, math.inf(f32))));
    try expect(math.isPositiveZero(pow(f32, -0.2, math.inf(f32))));
    // pow(x, -inf)   = +inf for |x| < 1
    try expect(math.isPositiveInf(pow(f32, 0.2, -math.inf(f32))));
    try expect(math.isPositiveInf(pow(f32, -0.2, -math.inf(f32))));
    // pow(+inf, y)   = +inf for y > 0
    try expect(math.isPositiveInf(pow(f32, math.inf(f32), 2.0)));
    try expect(math.isPositiveInf(pow(f32, math.inf(f32), 0.2)));
    // pow(+inf, y)   = +0 for y < 0
    try expect(math.isPositiveZero(pow(f32, math.inf(f32), -2.0)));
    try expect(math.isPositiveZero(pow(f32, math.inf(f32), -0.2)));
    // pow(-inf, y)    = -inf for y an odd integer > 0
    try expect(math.isNegativeInf(pow(f32, -math.inf(f32), 5.0)));
    // pow(-inf, +inf) = +inf
    try expect(math.isPositiveInf(pow(f32, -math.inf(f32), math.inf(f32))));
    // pow(-inf, -inf) = +0
    try expect(math.isPositiveZero(pow(f32, -math.inf(f32), -math.inf(f32))));
    // pow(-inf, y)    = +inf for finite y > 0 and not an odd integer
    try expect(math.isPositiveInf(pow(f32, -math.inf(f32), 4.0)));
    try expect(math.isPositiveInf(pow(f32, -math.inf(f32), 0.5)));
    // pow(-inf, y)    = -0 for finite y an odd integer < 0
    try expect(math.isNegativeZero(pow(f32, -math.inf(f32), -3.0)));
    // pow(-inf, y)    = +0 for finite y < 0 and not an odd integer
    try expect(math.isPositiveZero(pow(f32, -math.inf(f32), -2.0)));
    try expect(math.isPositiveZero(pow(f32, -math.inf(f32), -5.2)));
    // pow(x, y)      = nan for finite x < 0 and finite non-integer y
    try expect(math.isNan(pow(f32, -1.0, 1.2)));
    try expect(math.isNan(pow(f32, -12.4, -78.5)));
}

test "overflow" {
    try expect(math.isPositiveInf(pow(f64, 2, 1 << 32)));
    try expect(pow(f64, 2, -(1 << 32)) == 0);
    try expect(math.isNegativeInf(pow(f64, -2, (1 << 32) + 1)));
    try expect(pow(f64, 0.5, 1 << 45) == 0);
    try expect(math.isPositiveInf(pow(f64, 0.5, -(1 << 45))));
    try expect(math.isPositiveInf(pow(f64, -2, 1 << 64)));
    try expect(pow(f64, 0.5, 1 << 64) == 0);
}



---
File: /std/math/powi.zig
---

// Based on Rust, which is licensed under the MIT license.
// https://github.com/rust-lang/rust/blob/360432f1e8794de58cd94f34c9c17ad65871e5b5/LICENSE-MIT
//
// https://github.com/rust-lang/rust/blob/360432f1e8794de58cd94f34c9c17ad65871e5b5/src/libcore/num/mod.rs#L3423

const std = @import("../std.zig");
const math = std.math;
const assert = std.debug.assert;
const testing = std.testing;

/// Returns the power of x raised by the integer y (x^y).
///
/// Errors:
///  - Overflow: Integer overflow or Infinity
///  - Underflow: Absolute value of result smaller than 1
///
/// Edge case rules ordered by precedence:
///  - powi(T, x, 0)   = 1 unless T is i1, i0, u0
///  - powi(T, 0, x)   = 0 when x > 0
///  - powi(T, 0, x)   = Overflow
///  - powi(T, 1, y)   = 1
///  - powi(T, -1, y)  = -1 for y an odd integer
///  - powi(T, -1, y)  = 1 unless T is i1, i0, u0
///  - powi(T, -1, y)  = Overflow
///  - powi(T, x, y)   = Overflow when y >= @bitSizeOf(x)
///  - powi(T, x, y)   = Underflow when y < 0
pub fn powi(comptime T: type, x: T, y: T) (error{
    Overflow,
    Underflow,
}!T) {
    const bit_size = @typeInfo(T).int.bits;

    // `y & 1 == 0` won't compile when `does_one_overflow`.
    const does_one_overflow = math.maxInt(T) < 1;
    const is_y_even = !does_one_overflow and y & 1 == 0;

    if (x == 1 or y == 0 or (x == -1 and is_y_even)) {
        if (does_one_overflow) {
            return error.Overflow;
        } else {
            return 1;
        }
    }

    if (x == -1) {
        return -1;
    }

    if (x == 0) {
        if (y > 0) {
            return 0;
        } else {
            // Infinity/NaN, not overflow in strict sense
            return error.Overflow;
        }
    }
    // x >= 2 or x <= -2 from this point
    if (y >= bit_size) {
        return error.Overflow;
    }
    if (y < 0) {
        return error.Underflow;
    }

    // invariant :
    // return value = powi(T, base, exp) * acc;

    var base = x;
    var exp = y;
    var acc: T = if (does_one_overflow) unreachable else 1;

    while (exp > 1) {
        if (exp & 1 == 1) {
            const ov = @mulWithOverflow(acc, base);
            if (ov[1] != 0) return error.Overflow;
            acc = ov[0];
        }

        exp >>= 1;

        const ov = @mulWithOverflow(base, base);
        if (ov[1] != 0) return error.Overflow;
        base = ov[0];
    }

    if (exp == 1) {
        const ov = @mulWithOverflow(acc, base);
        if (ov[1] != 0) return error.Overflow;
        acc = ov[0];
    }

    return acc;
}

test powi {
    try testing.expectError(error.Overflow, powi(i8, -66, 6));
    try testing.expectError(error.Overflow, powi(i16, -13, 13));
    try testing.expectError(error.Overflow, powi(i32, -32, 21));
    try testing.expectError(error.Overflow, powi(i64, -24, 61));
    try testing.expectError(error.Overflow, powi(i17, -15, 15));
    try testing.expectError(error.Overflow, powi(i42, -6, 40));

    try testing.expect((try powi(i8, -5, 3)) == -125);
    try testing.expect((try powi(i16, -16, 3)) == -4096);
    try testing.expect((try powi(i32, -91, 3)) == -753571);
    try testing.expect((try powi(i64, -36, 6)) == 2176782336);
    try testing.expect((try powi(i17, -2, 15)) == -32768);
    try testing.expect((try powi(i42, -5, 7)) == -78125);

    try testing.expect((try powi(u8, 6, 2)) == 36);
    try testing.expect((try powi(u16, 5, 4)) == 625);
    try testing.expect((try powi(u32, 12, 6)) == 2985984);
    try testing.expect((try powi(u64, 34, 2)) == 1156);
    try testing.expect((try powi(u17, 16, 3)) == 4096);
    try testing.expect((try powi(u42, 34, 6)) == 1544804416);

    try testing.expectError(error.Overflow, powi(i8, 120, 7));
    try testing.expectError(error.Overflow, powi(i16, 73, 15));
    try testing.expectError(error.Overflow, powi(i32, 23, 31));
    try testing.expectError(error.Overflow, powi(i64, 68, 61));
    try testing.expectError(error.Overflow, powi(i17, 15, 15));
    try testing.expectError(error.Overflow, powi(i42, 121312, 41));

    try testing.expectError(error.Overflow, powi(u8, 123, 7));
    try testing.expectError(error.Overflow, powi(u16, 2313, 15));
    try testing.expectError(error.Overflow, powi(u32, 8968, 31));
    try testing.expectError(error.Overflow, powi(u64, 2342, 63));
    try testing.expectError(error.Overflow, powi(u17, 2723, 16));
    try testing.expectError(error.Overflow, powi(u42, 8234, 41));

    const minInt = std.math.minInt;
    try testing.expect((try powi(i8, -2, 7)) == minInt(i8));
    try testing.expect((try powi(i16, -2, 15)) == minInt(i16));
    try testing.expect((try powi(i32, -2, 31)) == minInt(i32));
    try testing.expect((try powi(i64, -2, 63)) == minInt(i64));

    try testing.expectError(error.Underflow, powi(i8, 6, -2));
    try testing.expectError(error.Underflow, powi(i16, 5, -4));
    try testing.expectError(error.Underflow, powi(i32, 12, -6));
    try testing.expectError(error.Underflow, powi(i64, 34, -2));
    try testing.expectError(error.Underflow, powi(i17, 16, -3));
    try testing.expectError(error.Underflow, powi(i42, 34, -6));
}

test "powi.special" {
    try testing.expectError(error.Overflow, powi(i8, -2, 8));
    try testing.expectError(error.Overflow, powi(i16, -2, 16));
    try testing.expectError(error.Overflow, powi(i32, -2, 32));
    try testing.expectError(error.Overflow, powi(i64, -2, 64));
    try testing.expectError(error.Overflow, powi(i17, -2, 17));
    try testing.expectError(error.Overflow, powi(i17, -2, 16));
    try testing.expectError(error.Overflow, powi(i42, -2, 42));

    try testing.expect((try powi(i8, -1, 3)) == -1);
    try testing.expect((try powi(i16, -1, 2)) == 1);
    try testing.expect((try powi(i32, -1, 16)) == 1);
    try testing.expect((try powi(i64, -1, 6)) == 1);
    try testing.expect((try powi(i17, -1, 15)) == -1);
    try testing.expect((try powi(i42, -1, 7)) == -1);

    try testing.expect((try powi(u8, 1, 2)) == 1);
    try testing.expect((try powi(u16, 1, 4)) == 1);
    try testing.expect((try powi(u32, 1, 6)) == 1);
    try testing.expect((try powi(u64, 1, 2)) == 1);
    try testing.expect((try powi(u17, 1, 3)) == 1);
    try testing.expect((try powi(u42, 1, 6)) == 1);

    try testing.expectError(error.Overflow, powi(i8, 2, 7));
    try testing.expectError(error.Overflow, powi(i16, 2, 15));
    try testing.expectError(error.Overflow, powi(i32, 2, 31));
    try testing.expectError(error.Overflow, powi(i64, 2, 63));
    try testing.expectError(error.Overflow, powi(i17, 2, 16));
    try testing.expectError(error.Overflow, powi(i42, 2, 41));

    try testing.expectError(error.Overflow, powi(u8, 2, 8));
    try testing.expectError(error.Overflow, powi(u16, 2, 16));
    try testing.expectError(error.Overflow, powi(u32, 2, 32));
    try testing.expectError(error.Overflow, powi(u64, 2, 64));
    try testing.expectError(error.Overflow, powi(u17, 2, 17));
    try testing.expectError(error.Overflow, powi(u42, 2, 42));

    try testing.expect((try powi(u8, 6, 0)) == 1);
    try testing.expect((try powi(u16, 5, 0)) == 1);
    try testing.expect((try powi(u32, 12, 0)) == 1);
    try testing.expect((try powi(u64, 34, 0)) == 1);
    try testing.expect((try powi(u17, 16, 0)) == 1);
    try testing.expect((try powi(u42, 34, 0)) == 1);
}

test "powi.narrow" {
    try testing.expectError(error.Overflow, powi(u0, 0, 0));
    try testing.expectError(error.Overflow, powi(i0, 0, 0));
    try testing.expectError(error.Overflow, powi(i1, 0, 0));
    try testing.expectError(error.Overflow, powi(i1, -1, 0));
    try testing.expectError(error.Overflow, powi(i1, 0, -1));
    try testing.expect((try powi(i1, -1, -1)) == -1);
}



---
File: /std/math/scalbn.zig
---

const std = @import("std");
const expect = std.testing.expect;

/// Returns a * FLT_RADIX ^ exp.
///
/// Zig only supports binary base IEEE-754 floats. Hence FLT_RADIX=2, and this is an alias for ldexp.
pub const scalbn = @import("ldexp.zig").ldexp;

test scalbn {
    // Verify we are using base 2.
    try expect(scalbn(@as(f16, 1.5), 4) == 24.0);
    try expect(scalbn(@as(f32, 1.5), 4) == 24.0);
    try expect(scalbn(@as(f64, 1.5), 4) == 24.0);
    try expect(scalbn(@as(f128, 1.5), 4) == 24.0);
}



---
File: /std/math/signbit.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns whether x is negative or negative 0.
pub fn signbit(x: anytype) bool {
    return switch (@typeInfo(@TypeOf(x))) {
        .int, .comptime_int => x,
        .float => |float| @as(@Int(.signed, float.bits), @bitCast(x)),
        .comptime_float => @as(i128, @bitCast(@as(f128, x))), // any float type will do
        else => @compileError("std.math.signbit does not support " ++ @typeName(@TypeOf(x))),
    } < 0;
}

test signbit {
    try testInts(i0);
    try testInts(u0);
    try testInts(i1);
    try testInts(u1);
    try testInts(i2);
    try testInts(u2);

    try testFloats(f16);
    try testFloats(f32);
    try testFloats(f64);
    try testFloats(f80);
    try testFloats(f128);
    try testFloats(c_longdouble);
    try testFloats(comptime_float);
}

fn testInts(comptime Type: type) !void {
    try expect((std.math.minInt(Type) < 0) == signbit(@as(Type, std.math.minInt(Type))));
    try expect(!signbit(@as(Type, 0)));
    try expect(!signbit(@as(Type, std.math.maxInt(Type))));
}

fn testFloats(comptime Type: type) !void {
    try expect(!signbit(@as(Type, 0.0)));
    try expect(!signbit(@as(Type, 1.0)));
    try expect(signbit(@as(Type, -2.0)));
    try expect(signbit(@as(Type, -0.0)));
    try expect(!signbit(math.inf(Type)));
    try expect(signbit(-math.inf(Type)));
    try expect(!signbit(math.nan(Type)));
    try expect(signbit(-math.nan(Type)));
}



---
File: /std/math/sinh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/sinhf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/sinh.c

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;
const expo2 = @import("expo2.zig").expo2;
const maxInt = std.math.maxInt;

/// Returns the hyperbolic sine of x.
///
/// Special Cases:
///  - sinh(+-0)   = +-0
///  - sinh(+-inf) = +-inf
///  - sinh(nan)   = nan
pub fn sinh(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => sinh32(x),
        f64 => sinh64(x),
        else => @compileError("sinh not implemented for " ++ @typeName(T)),
    };
}

// sinh(x) = (exp(x) - 1 / exp(x)) / 2
//         = (exp(x) - 1 + (exp(x) - 1) / exp(x)) / 2
//         = x + x^3 / 6 + o(x^5)
fn sinh32(x: f32) f32 {
    const u = @as(u32, @bitCast(x));
    const ux = u & 0x7FFFFFFF;
    const ax = @as(f32, @bitCast(ux));

    if (x == 0.0 or math.isNan(x)) {
        return x;
    }

    var h: f32 = 0.5;
    if (u >> 31 != 0) {
        h = -h;
    }

    // |x| < log(FLT_MAX)
    if (ux < 0x42B17217) {
        const t = math.expm1(ax);
        if (ux < 0x3F800000) {
            if (ux < 0x3F800000 - (12 << 23)) {
                return x;
            } else {
                return h * (2 * t - t * t / (t + 1));
            }
        }
        return h * (t + t / (t + 1));
    }

    // |x| > log(FLT_MAX) or nan
    return 2 * h * expo2(ax);
}

fn sinh64(x: f64) f64 {
    const u = @as(u64, @bitCast(x));
    const w = @as(u32, @intCast(u >> 32)) & (maxInt(u32) >> 1);
    const ax = @as(f64, @bitCast(u & (maxInt(u64) >> 1)));

    if (x == 0.0 or math.isNan(x)) {
        return x;
    }

    var h: f32 = 0.5;
    if (u >> 63 != 0) {
        h = -h;
    }

    // |x| < log(FLT_MAX)
    if (w < 0x40862E42) {
        const t = math.expm1(ax);
        if (w < 0x3FF00000) {
            if (w < 0x3FF00000 - (26 << 20)) {
                return x;
            } else {
                return h * (2 * t - t * t / (t + 1));
            }
        }
        // NOTE: |x| > log(0x1p26) + eps could be h * exp(x)
        return h * (t + t / (t + 1));
    }

    // |x| > log(DBL_MAX) or nan
    return 2 * h * expo2(ax);
}

test sinh {
    try expect(sinh(@as(f32, 1.5)) == sinh32(1.5));
    try expect(sinh(@as(f64, 1.5)) == sinh64(1.5));
}

test sinh32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, sinh32(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(0.2), 0.201336, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(0.8923), 1.015512, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(1.5), 2.129279, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(-0.0), -0.0, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(-0.2), -0.201336, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(-0.8923), -1.015512, epsilon));
    try expect(math.approxEqAbs(f32, sinh32(-1.5), -2.129279, epsilon));
}

test sinh64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, sinh64(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(0.2), 0.201336, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(0.8923), 1.015512, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(1.5), 2.129279, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(-0.0), -0.0, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(-0.2), -0.201336, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(-0.8923), -1.015512, epsilon));
    try expect(math.approxEqAbs(f64, sinh64(-1.5), -2.129279, epsilon));
}

test "sinh32.special" {
    try expect(math.isPositiveZero(sinh32(0.0)));
    try expect(math.isNegativeZero(sinh32(-0.0)));
    try expect(math.isPositiveInf(sinh32(math.inf(f32))));
    try expect(math.isNegativeInf(sinh32(-math.inf(f32))));
    try expect(math.isNan(sinh32(math.nan(f32))));
}

test "sinh64.special" {
    try expect(math.isPositiveZero(sinh64(0.0)));
    try expect(math.isNegativeZero(sinh64(-0.0)));
    try expect(math.isPositiveInf(sinh64(math.inf(f64))));
    try expect(math.isNegativeInf(sinh64(-math.inf(f64))));
    try expect(math.isNan(sinh64(math.nan(f64))));
}



---
File: /std/math/sqrt.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;
const TypeId = std.builtin.TypeId;
const maxInt = std.math.maxInt;

/// Returns the square root of x.
///
/// Special Cases:
///  - sqrt(+inf)  = +inf
///  - sqrt(+-0)   = +-0
///  - sqrt(x)     = nan if x < 0
///  - sqrt(nan)   = nan
/// TODO Decide if all this logic should be implemented directly in the @sqrt builtin function.
pub fn sqrt(x: anytype) Sqrt(@TypeOf(x)) {
    const T = @TypeOf(x);
    switch (@typeInfo(T)) {
        .float, .comptime_float => return @sqrt(x),
        .comptime_int => comptime {
            if (x > maxInt(u128)) {
                @compileError("sqrt not implemented for comptime_int greater than 128 bits");
            }
            if (x < 0) {
                @compileError("sqrt on negative number");
            }
            return @as(T, sqrt_int(u128, x));
        },
        .int => |IntType| switch (IntType.signedness) {
            .signed => @compileError("sqrt not implemented for signed integers"),
            .unsigned => return sqrt_int(T, x),
        },
        else => @compileError("sqrt not implemented for " ++ @typeName(T)),
    }
}

fn sqrt_int(comptime T: type, value: T) Sqrt(T) {
    if (@typeInfo(T).int.bits <= 2) {
        return if (value == 0) 0 else 1; // shortcut for small number of bits to simplify general case
    } else {
        const bits = @typeInfo(T).int.bits;
        const max = math.maxInt(T);
        const minustwo = (@as(T, 2) ^ max) + 1; // unsigned int cannot represent -2
        var op = value;
        var res: T = 0;
        var one: T = 1 << ((bits - 1) & minustwo); // highest power of four that fits into T

        // "one" starts at the highest power of four <= than the argument.
        while (one > op) {
            one >>= 2;
        }

        while (one != 0) {
            const c = op >= res + one;
            if (c) op -= res + one;
            res >>= 1;
            if (c) res += one;
            one >>= 2;
        }

        return @as(Sqrt(T), @intCast(res));
    }
}

test sqrt_int {
    try expect(sqrt_int(u32, 3) == 1);
    try expect(sqrt_int(u32, 4) == 2);
    try expect(sqrt_int(u32, 5) == 2);
    try expect(sqrt_int(u32, 8) == 2);
    try expect(sqrt_int(u32, 9) == 3);
    try expect(sqrt_int(u32, 10) == 3);

    try expect(sqrt_int(u0, 0) == 0);
    try expect(sqrt_int(u1, 1) == 1);
    try expect(sqrt_int(u2, 3) == 1);
    try expect(sqrt_int(u3, 4) == 2);
    try expect(sqrt_int(u4, 8) == 2);
    try expect(sqrt_int(u4, 9) == 3);
}

/// Returns the return type `sqrt` will return given an operand of type `T`.
pub fn Sqrt(comptime T: type) type {
    return switch (@typeInfo(T)) {
        .int => |int| @Int(.unsigned, (int.bits + 1) / 2),
        else => T,
    };
}



---
File: /std/math/tanh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/tanhf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/tanh.c

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const expect = std.testing.expect;
const expo2 = @import("expo2.zig").expo2;
const maxInt = std.math.maxInt;

/// Returns the hyperbolic tangent of x.
///
/// Special Cases:
///  - tanh(+-0)   = +-0
///  - tanh(+-inf) = +-1
///  - tanh(nan)   = nan
pub fn tanh(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => tanh32(x),
        f64 => tanh64(x),
        else => @compileError("tanh not implemented for " ++ @typeName(T)),
    };
}

// tanh(x) = (exp(x) - exp(-x)) / (exp(x) + exp(-x))
//         = (exp(2x) - 1) / (exp(2x) - 1 + 2)
//         = (1 - exp(-2x)) / (exp(-2x) - 1 + 2)
fn tanh32(x: f32) f32 {
    const u = @as(u32, @bitCast(x));
    const ux = u & 0x7FFFFFFF;
    const ax = @as(f32, @bitCast(ux));
    const sign = (u >> 31) != 0;

    var t: f32 = undefined;

    // |x| < log(3) / 2 ~= 0.5493 or nan
    if (ux > 0x3F0C9F54) {
        // |x| > 10
        if (ux > 0x41200000) {
            t = 1.0 + 0 / x;
        } else {
            t = math.expm1(2 * ax);
            t = 1 - 2 / (t + 2);
        }
    }
    // |x| > log(5 / 3) / 2 ~= 0.2554
    else if (ux > 0x3E82C578) {
        t = math.expm1(2 * ax);
        t = t / (t + 2);
    }
    // |x| >= 0x1.0p-126
    else if (ux >= 0x00800000) {
        t = math.expm1(-2 * ax);
        t = -t / (t + 2);
    }
    // |x| is subnormal
    else {
        mem.doNotOptimizeAway(ax * ax);
        t = ax;
    }

    return if (sign) -t else t;
}

fn tanh64(x: f64) f64 {
    const u = @as(u64, @bitCast(x));
    const ux = u & 0x7FFFFFFFFFFFFFFF;
    const w = @as(u32, @intCast(ux >> 32));
    const ax = @as(f64, @bitCast(ux));
    const sign = (u >> 63) != 0;

    var t: f64 = undefined;

    // |x| < log(3) / 2 ~= 0.5493 or nan
    if (w > 0x3FE193EA) {
        // |x| > 20 or nan
        if (w > 0x40340000) {
            t = 1.0 - 0 / ax;
        } else {
            t = math.expm1(2 * ax);
            t = 1 - 2 / (t + 2);
        }
    }
    // |x| > log(5 / 3) / 2 ~= 0.2554
    else if (w > 0x3FD058AE) {
        t = math.expm1(2 * ax);
        t = t / (t + 2);
    }
    // |x| >= 0x1.0p-1022
    else if (w >= 0x00100000) {
        t = math.expm1(-2 * ax);
        t = -t / (t + 2);
    }
    // |x| is subnormal
    else {
        mem.doNotOptimizeAway(@as(f32, @floatCast(ax)));
        t = ax;
    }

    return if (sign) -t else t;
}

test tanh {
    try expect(tanh(@as(f32, 1.5)) == tanh32(1.5));
    try expect(tanh(@as(f64, 1.5)) == tanh64(1.5));
}

test tanh32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, tanh32(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(0.2), 0.197375, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(0.8923), 0.712528, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(1.5), 0.905148, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(37.45), 1.0, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(-0.8923), -0.712528, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(-1.5), -0.905148, epsilon));
    try expect(math.approxEqAbs(f32, tanh32(-37.45), -1.0, epsilon));
}

test tanh64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, tanh64(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(0.2), 0.197375, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(0.8923), 0.712528, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(1.5), 0.905148, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(37.45), 1.0, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(-0.8923), -0.712528, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(-1.5), -0.905148, epsilon));
    try expect(math.approxEqAbs(f64, tanh64(-37.45), -1.0, epsilon));
}

test "tanh32.special" {
    try expect(math.isPositiveZero(tanh32(0.0)));
    try expect(math.isNegativeZero(tanh32(-0.0)));
    try expect(tanh32(math.inf(f32)) == 1.0);
    try expect(tanh32(-math.inf(f32)) == -1.0);
    try expect(math.isNan(tanh32(math.nan(f32))));
}

test "tanh64.special" {
    try expect(math.isPositiveZero(tanh64(0.0)));
    try expect(math.isNegativeZero(tanh64(-0.0)));
    try expect(tanh64(math.inf(f64)) == 1.0);
    try expect(tanh64(-math.inf(f64)) == -1.0);
    try expect(math.isNan(tanh64(math.nan(f64))));
}



---
File: /std/mem/Allocator.zig
---

//! The standard memory allocation interface.

const std = @import("../std.zig");
const assert = std.debug.assert;
const math = std.math;
const mem = std.mem;
const Allocator = @This();
const builtin = @import("builtin");
const Alignment = std.mem.Alignment;

pub const Error = error{OutOfMemory};
pub const Log2Align = math.Log2Int(usize);

/// The type erased pointer to the allocator implementation.
///
/// Any comparison of this field may result in illegal behavior, since it may
/// be set to `undefined` in cases where the allocator implementation does not
/// have any associated state.
ptr: *anyopaque,
vtable: *const VTable,

pub const VTable = struct {
    /// Return a pointer to `len` bytes with specified `alignment`, or return
    /// `null` indicating the allocation failed.
    ///
    /// `ret_addr` is optionally provided as the first return address of the
    /// allocation call stack. If the value is `0` it means no return address
    /// has been provided.
    alloc: *const fn (*anyopaque, len: usize, alignment: Alignment, ret_addr: usize) ?[*]u8,

    /// Attempt to expand or shrink memory in place.
    ///
    /// `memory.len` must equal the length requested from the most recent
    /// successful call to `alloc`, `resize`, or `remap`. `alignment` must
    /// equal the same value that was passed as the `alignment` parameter to
    /// the original `alloc` call.
    ///
    /// A result of `true` indicates the resize was successful and the
    /// allocation now has the same address but a size of `new_len`. `false`
    /// indicates the resize could not be completed without moving the
    /// allocation to a different address.
    ///
    /// `new_len` must be greater than zero.
    ///
    /// `ret_addr` is optionally provided as the first return address of the
    /// allocation call stack. If the value is `0` it means no return address
    /// has been provided.
    resize: *const fn (*anyopaque, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) bool,

    /// Attempt to expand or shrink memory, allowing relocation.
    ///
    /// `memory.len` must equal the length requested from the most recent
    /// successful call to `alloc`, `resize`, or `remap`. `alignment` must
    /// equal the same value that was passed as the `alignment` parameter to
    /// the original `alloc` call.
    ///
    /// A non-`null` return value indicates the resize was successful. The
    /// allocation may have same address, or may have been relocated. In either
    /// case, the allocation now has size of `new_len`. A `null` return value
    /// indicates that the resize would be equivalent to allocating new memory,
    /// copying the bytes from the old memory, and then freeing the old memory.
    /// In such case, it is more efficient for the caller to perform the copy.
    ///
    /// `new_len` must be greater than zero.
    ///
    /// `ret_addr` is optionally provided as the first return address of the
    /// allocation call stack. If the value is `0` it means no return address
    /// has been provided.
    remap: *const fn (*anyopaque, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) ?[*]u8,

    /// Free and invalidate a region of memory.
    ///
    /// `memory.len` must equal the length requested from the most recent
    /// successful call to `alloc`, `resize`, or `remap`. `alignment` must
    /// equal the same value that was passed as the `alignment` parameter to
    /// the original `alloc` call.
    ///
    /// `ret_addr` is optionally provided as the first return address of the
    /// allocation call stack. If the value is `0` it means no return address
    /// has been provided.
    free: *const fn (*anyopaque, memory: []u8, alignment: Alignment, ret_addr: usize) void,
};

pub fn noAlloc(
    self: *anyopaque,
    len: usize,
    alignment: Alignment,
    ret_addr: usize,
) ?[*]u8 {
    _ = self;
    _ = len;
    _ = alignment;
    _ = ret_addr;
    return null;
}

pub fn noResize(
    self: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    new_len: usize,
    ret_addr: usize,
) bool {
    _ = self;
    _ = memory;
    _ = alignment;
    _ = new_len;
    _ = ret_addr;
    return false;
}

pub fn noRemap(
    self: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    new_len: usize,
    ret_addr: usize,
) ?[*]u8 {
    _ = self;
    _ = memory;
    _ = alignment;
    _ = new_len;
    _ = ret_addr;
    return null;
}

pub fn noFree(
    self: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    ret_addr: usize,
) void {
    _ = self;
    _ = memory;
    _ = alignment;
    _ = ret_addr;
}

/// This function is not intended to be called except from within the
/// implementation of an `Allocator`.
pub inline fn rawAlloc(a: Allocator, len: usize, alignment: Alignment, ret_addr: usize) ?[*]u8 {
    return a.vtable.alloc(a.ptr, len, alignment, ret_addr);
}

/// This function is not intended to be called except from within the
/// implementation of an `Allocator`.
pub inline fn rawResize(a: Allocator, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) bool {
    return a.vtable.resize(a.ptr, memory, alignment, new_len, ret_addr);
}

/// This function is not intended to be called except from within the
/// implementation of an `Allocator`.
pub inline fn rawRemap(a: Allocator, memory: []u8, alignment: Alignment, new_len: usize, ret_addr: usize) ?[*]u8 {
    return a.vtable.remap(a.ptr, memory, alignment, new_len, ret_addr);
}

/// This function is not intended to be called except from within the
/// implementation of an `Allocator`.
pub inline fn rawFree(a: Allocator, memory: []u8, alignment: Alignment, ret_addr: usize) void {
    return a.vtable.free(a.ptr, memory, alignment, ret_addr);
}

/// Returns a pointer to undefined memory.
/// Call `destroy` with the result to free the memory.
pub fn create(a: Allocator, comptime T: type) Error!*T {
    if (@sizeOf(T) == 0) {
        const ptr = comptime std.mem.alignBackward(usize, math.maxInt(usize), @alignOf(T));
        return @ptrFromInt(ptr);
    }
    const ptr: *T = @ptrCast(try a.allocBytesWithAlignment(.of(T), @sizeOf(T), @returnAddress()));
    return ptr;
}

/// `ptr` should be the return value of `create`, or otherwise
/// have the same address and alignment property.
pub fn destroy(self: Allocator, ptr: anytype) void {
    const info = @typeInfo(@TypeOf(ptr)).pointer;
    if (info.size != .one) @compileError("ptr must be a single item pointer");
    const T = info.child;
    if (@sizeOf(T) == 0) return;
    const non_const_ptr = @as([*]u8, @ptrCast(@constCast(ptr)));
    self.rawFree(
        non_const_ptr[0..@sizeOf(T)],
        .fromByteUnits(info.alignment orelse @alignOf(T)),
        @returnAddress(),
    );
}

/// Allocates an array of `n` items of type `T` and sets all the
/// items to `undefined`. Depending on the Allocator
/// implementation, it may be required to call `free` once the
/// memory is no longer needed, to avoid a resource leak. If the
/// `Allocator` implementation is unknown, then correct code will
/// call `free` when done.
///
/// For allocating a single item, see `create`.
pub fn alloc(self: Allocator, comptime T: type, n: usize) Error![]T {
    return self.allocAdvancedWithRetAddr(T, null, n, @returnAddress());
}

pub fn allocWithOptions(
    self: Allocator,
    comptime Elem: type,
    n: usize,
    /// null means naturally aligned
    comptime optional_alignment: ?Alignment,
    comptime optional_sentinel: ?Elem,
) Error!AllocWithOptionsPayload(Elem, optional_alignment, optional_sentinel) {
    return self.allocWithOptionsRetAddr(Elem, n, optional_alignment, optional_sentinel, @returnAddress());
}

pub fn allocWithOptionsRetAddr(
    self: Allocator,
    comptime Elem: type,
    n: usize,
    /// null means naturally aligned
    comptime optional_alignment: ?Alignment,
    comptime optional_sentinel: ?Elem,
    return_address: usize,
) Error!AllocWithOptionsPayload(Elem, optional_alignment, optional_sentinel) {
    if (optional_sentinel) |sentinel| {
        const ptr = try self.allocAdvancedWithRetAddr(Elem, optional_alignment, n + 1, return_address);
        ptr[n] = sentinel;
        return ptr[0..n :sentinel];
    } else {
        return self.allocAdvancedWithRetAddr(Elem, optional_alignment, n, return_address);
    }
}

fn AllocWithOptionsPayload(comptime Elem: type, comptime alignment: ?Alignment, comptime sentinel: ?Elem) type {
    if (sentinel) |s| {
        return [:s]align(if (alignment) |a| a.toByteUnits() else @alignOf(Elem)) Elem;
    } else {
        return []align(if (alignment) |a| a.toByteUnits() else @alignOf(Elem)) Elem;
    }
}

/// Allocates an array of `n + 1` items of type `T` and sets the first `n`
/// items to `undefined` and the last item to `sentinel`. Depending on the
/// Allocator implementation, it may be required to call `free` once the
/// memory is no longer needed, to avoid a resource leak. If the
/// `Allocator` implementation is unknown, then correct code will
/// call `free` when done.
///
/// For allocating a single item, see `create`.
pub fn allocSentinel(
    self: Allocator,
    comptime Elem: type,
    n: usize,
    comptime sentinel: Elem,
) Error![:sentinel]Elem {
    return self.allocWithOptionsRetAddr(Elem, n, null, sentinel, @returnAddress());
}

pub fn alignedAlloc(
    self: Allocator,
    comptime T: type,
    /// null means naturally aligned
    comptime alignment: ?Alignment,
    n: usize,
) Error![]align(if (alignment) |a| a.toByteUnits() else @alignOf(T)) T {
    return self.allocAdvancedWithRetAddr(T, alignment, n, @returnAddress());
}

pub inline fn allocAdvancedWithRetAddr(
    self: Allocator,
    comptime T: type,
    /// null means naturally aligned
    comptime alignment: ?Alignment,
    n: usize,
    return_address: usize,
) Error![]align(if (alignment) |a| a.toByteUnits() else @alignOf(T)) T {
    const a: Alignment = alignment orelse comptime .of(T);
    const ptr: [*]align(a.toByteUnits()) T = @ptrCast(try self.allocWithSizeAndAlignment(@sizeOf(T), a, n, return_address));
    return ptr[0..n];
}

fn allocWithSizeAndAlignment(
    self: Allocator,
    comptime size: usize,
    comptime alignment: Alignment,
    n: usize,
    return_address: usize,
) Error![*]align(alignment.toByteUnits()) u8 {
    const byte_count = math.mul(usize, size, n) catch return error.OutOfMemory;
    return self.allocBytesWithAlignment(alignment, byte_count, return_address);
}

fn allocBytesWithAlignment(
    self: Allocator,
    comptime alignment: Alignment,
    byte_count: usize,
    return_address: usize,
) Error![*]align(alignment.toByteUnits()) u8 {
    if (byte_count == 0) {
        const ptr = comptime alignment.backward(math.maxInt(usize));
        return @as([*]align(alignment.toByteUnits()) u8, @ptrFromInt(ptr));
    }

    const byte_ptr = self.rawAlloc(byte_count, alignment, return_address) orelse return error.OutOfMemory;
    @memset(byte_ptr[0..byte_count], undefined);
    return @alignCast(byte_ptr);
}

/// Request to modify the size of an allocation.
///
/// It is guaranteed to not move the pointer, however the allocator
/// implementation may refuse the resize request by returning `false`.
///
/// `allocation` may be an empty slice, in which case `false` is returned,
/// unless `new_len` is also 0, in which case `true` is returned.
///
/// `new_len` may be zero, in which case the allocation is freed.
pub fn resize(self: Allocator, allocation: anytype, new_len: usize) bool {
    const slice_info = @typeInfo(@TypeOf(allocation)).pointer;
    comptime assert(slice_info.size == .slice);
    const T = slice_info.child;
    if (new_len == 0) {
        self.free(allocation);
        return true;
    }
    if (allocation.len == 0) {
        return false;
    }
    const old_memory: []u8 = @ptrCast(@constCast(mem.absorbSentinel(allocation)));
    // I would like to use saturating multiplication here, but LLVM cannot lower it
    // on WebAssembly: https://github.com/ziglang/zig/issues/9660
    //const new_len_bytes = new_len *| @sizeOf(T);
    const new_len_bytes = math.mul(usize, @sizeOf(T), new_len) catch return false;
    return self.rawResize(
        old_memory,
        .fromByteUnits(slice_info.alignment orelse @alignOf(T)),
        new_len_bytes,
        @returnAddress(),
    );
}

/// Request to modify the size of an allocation, allowing relocation.
///
/// A non-`null` return value indicates the resize was successful. The
/// allocation may have same address, or may have been relocated. In either
/// case, the allocation now has size of `new_len`. A `null` return value
/// indicates that the resize would be equivalent to allocating new memory,
/// copying the bytes from the old memory, and then freeing the old memory.
/// In such case, it is more efficient for the caller to perform those
/// operations.
///
/// `allocation` may be an empty slice, in which case `null` is returned,
/// unless `new_len` is also 0, in which case `allocation` is returned.
///
/// `new_len` may be zero, in which case the allocation is freed.
///
/// If the allocation's elements' type is zero bytes sized, `allocation.len` is set to `new_len`.
pub fn remap(self: Allocator, allocation: anytype, new_len: usize) ?@TypeOf(allocation) {
    const slice_info = @typeInfo(@TypeOf(allocation)).pointer;
    comptime assert(slice_info.size == .slice);
    const T = slice_info.child;

    if (new_len == 0) {
        self.free(allocation);
        return allocation[0..0];
    }
    if (allocation.len == 0) {
        return null;
    }
    if (@sizeOf(T) == 0) {
        var new_memory = allocation;
        new_memory.len = new_len;
        return new_memory;
    }
    const old_memory: []u8 = @ptrCast(@constCast(mem.absorbSentinel(allocation)));
    // I would like to use saturating multiplication here, but LLVM cannot lower it
    // on WebAssembly: https://github.com/ziglang/zig/issues/9660
    //const new_len_bytes = new_len *| @sizeOf(T);
    const new_len_bytes = math.mul(usize, @sizeOf(T), new_len) catch return null;
    const new_ptr = self.rawRemap(
        old_memory,
        .fromByteUnits(slice_info.alignment orelse @alignOf(T)),
        new_len_bytes,
        @returnAddress(),
    ) orelse return null;
    return @ptrCast(@alignCast(new_ptr[0..new_len_bytes]));
}

/// This function requests a new size for an existing allocation, which
/// can be larger, smaller, or the same size as the old memory allocation.
/// The result is an array of `new_n` items of the same type as the existing
/// allocation.
///
/// If `new_n` is 0, this is the same as `free` and it always succeeds.
///
/// `old_mem` may have length zero, which makes a new allocation.
///
/// This function only fails on out-of-memory conditions, unlike:
/// * `remap` which returns `null` when the `Allocator` implementation cannot
///   do the realloc more efficiently than the caller
/// * `resize` which returns `false` when the `Allocator` implementation cannot
///   change the size without relocating the allocation.
pub fn realloc(self: Allocator, old_mem: anytype, new_n: usize) Error!@TypeOf(old_mem) {
    return self.reallocAdvanced(old_mem, new_n, @returnAddress());
}

pub fn reallocAdvanced(
    self: Allocator,
    old_mem: anytype,
    new_n: usize,
    return_address: usize,
) Error!@TypeOf(old_mem) {
    const slice_info = @typeInfo(@TypeOf(old_mem)).pointer;
    comptime assert(slice_info.size == .slice);
    const T = slice_info.child;
    if (old_mem.len == 0) {
        return self.allocAdvancedWithRetAddr(T, .fromByteUnitsOptional(slice_info.alignment), new_n, return_address);
    }
    if (new_n == 0) {
        self.free(old_mem);
        const alignment = slice_info.alignment orelse @alignOf(T);
        const addr = comptime std.mem.alignBackward(usize, math.maxInt(usize), alignment);
        const ptr: *align(alignment) [0]T = @ptrFromInt(addr);
        return ptr;
    }

    const old_byte_slice: []u8 = @ptrCast(@constCast(mem.absorbSentinel(old_mem)));
    const byte_count = math.mul(usize, @sizeOf(T), new_n) catch return error.OutOfMemory;
    // Note: can't set shrunk memory to undefined as memory shouldn't be modified on realloc failure
    if (self.rawRemap(old_byte_slice, .fromByteUnits(slice_info.alignment orelse @alignOf(T)), byte_count, return_address)) |p| {
        return @ptrCast(@alignCast(p[0..byte_count]));
    }

    const new_mem = self.rawAlloc(byte_count, .fromByteUnits(slice_info.alignment orelse @alignOf(T)), return_address) orelse
        return error.OutOfMemory;
    const copy_len = @min(byte_count, old_byte_slice.len);
    @memcpy(new_mem[0..copy_len], old_byte_slice[0..copy_len]);
    @memset(old_byte_slice, undefined);
    self.rawFree(old_byte_slice, .fromByteUnits(slice_info.alignment orelse @alignOf(T)), return_address);

    return @ptrCast(@alignCast(new_mem[0..byte_count]));
}

/// Free an array allocated with `alloc`.
/// If memory has length 0, free is a no-op.
/// To free a single item, see `destroy`.
pub fn free(self: Allocator, memory: anytype) void {
    const slice_info = @typeInfo(@TypeOf(memory)).pointer;
    comptime assert(slice_info.size == .slice);
    const bytes: []u8 = @ptrCast(@constCast(mem.absorbSentinel(memory)));
    if (bytes.len == 0) return;
    @memset(bytes, undefined);
    self.rawFree(bytes, .fromByteUnits(slice_info.alignment orelse @alignOf(slice_info.child)), @returnAddress());
}

/// Copies `m` to newly allocated memory. Caller owns the memory.
pub fn dupe(allocator: Allocator, comptime T: type, m: []const T) Error![]T {
    const new_buf = try allocator.alloc(T, m.len);
    @memcpy(new_buf, m);
    return new_buf;
}

/// Deprecated in favor of `dupeSentinel`
/// Copies `m` to newly allocated memory, with a null-terminated element. Caller owns the memory.
pub fn dupeZ(allocator: Allocator, comptime T: type, m: []const T) Error![:0]T {
    return allocator.dupeSentinel(T, m, 0);
}

/// Copies `m` to newly allocated memory, with a null-terminated element. Caller owns the memory.
pub fn dupeSentinel(
    allocator: Allocator,
    comptime T: type,
    m: []const T,
    comptime sentinel: T,
) Error![:sentinel]T {
    const new_buf = try allocator.alloc(T, m.len + 1);
    @memcpy(new_buf[0..m.len], m);
    new_buf[m.len] = sentinel;
    return new_buf[0..m.len :sentinel];
}

/// An allocator that always fails to allocate.
pub const failing: Allocator = .{
    .ptr = undefined,
    .vtable = &.{
        .alloc = noAlloc,
        .resize = unreachableResize,
        .remap = unreachableRemap,
        .free = unreachableFree,
    },
};

fn unreachableResize(
    self: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    new_len: usize,
    ret_addr: usize,
) bool {
    _ = self;
    _ = memory;
    _ = alignment;
    _ = new_len;
    _ = ret_addr;
    unreachable;
}

fn unreachableRemap(
    self: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    new_len: usize,
    ret_addr: usize,
) ?[*]u8 {
    _ = self;
    _ = memory;
    _ = alignment;
    _ = new_len;
    _ = ret_addr;
    unreachable;
}

fn unreachableFree(
    self: *anyopaque,
    memory: []u8,
    alignment: Alignment,
    ret_addr: usize,
) void {
    _ = self;
    _ = memory;
    _ = alignment;
    _ = ret_addr;
    unreachable;
}

test failing {
    const f: Allocator = .failing;
    try std.testing.expectError(error.OutOfMemory, f.alloc(u8, 123));
}



---
File: /std/meta/trailer_flags.zig
---

const std = @import("../std.zig");
const meta = std.meta;
const testing = std.testing;
const mem = std.mem;
const assert = std.debug.assert;
const Type = std.builtin.Type;

/// This is useful for saving memory when allocating an object that has many
/// optional components. The optional objects are allocated sequentially in
/// memory, and a single integer is used to represent each optional object
/// and whether it is present based on each corresponding bit.
pub fn TrailerFlags(comptime Fields: type) type {
    return struct {
        bits: Int,

        pub const Int = meta.Int(.unsigned, bit_count);
        pub const bit_count = @typeInfo(Fields).@"struct".fields.len;

        pub const FieldEnum = std.meta.FieldEnum(Fields);

        pub const ActiveFields = std.enums.EnumFieldStruct(FieldEnum, bool, false);
        pub const FieldValues = blk: {
            var field_names: [bit_count][]const u8 = undefined;
            var field_types: [bit_count]type = undefined;
            var field_attrs: [bit_count]std.builtin.Type.StructField.Attributes = undefined;
            for (@typeInfo(Fields).@"struct".fields, &field_names, &field_types, &field_attrs) |field, *new_name, *NewType, *new_attrs| {
                new_name.* = field.name;
                NewType.* = ?field.type;
                const default: ?field.type = null;
                new_attrs.* = .{ .default_value_ptr = &default };
            }
            break :blk @Struct(.auto, null, &field_names, &field_types, &field_attrs);
        };

        pub const Self = @This();

        pub fn has(self: Self, comptime field: FieldEnum) bool {
            const field_index = @intFromEnum(field);
            return (self.bits & (1 << field_index)) != 0;
        }

        pub fn get(self: Self, p: [*]align(@alignOf(Fields)) const u8, comptime field: FieldEnum) ?Field(field) {
            if (!self.has(field))
                return null;
            return self.ptrConst(p, field).*;
        }

        pub fn setFlag(self: *Self, comptime field: FieldEnum) void {
            const field_index = @intFromEnum(field);
            self.bits |= 1 << field_index;
        }

        /// `fields` is a boolean struct where each active field is set to `true`
        pub fn init(fields: ActiveFields) Self {
            var self: Self = .{ .bits = 0 }
```
