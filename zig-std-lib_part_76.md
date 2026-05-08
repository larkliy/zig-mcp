```
try expect(math.isNan(atan2_32(1.0, math.nan(f32))));
    try expect(math.isNan(atan2_32(math.nan(f32), 1.0)));
    try expect(atan2_32(0.0, 5.0) == 0.0);
    try expect(atan2_32(-0.0, 5.0) == -0.0);
    try expect(math.approxEqAbs(f32, atan2_32(0.0, -5.0), math.pi, epsilon));
    //expect(math.approxEqAbs(f32, atan2_32(-0.0, -5.0), -math.pi, .{.rel=0,.abs=epsilon})); TODO support negative zero?
    try expect(math.approxEqAbs(f32, atan2_32(1.0, 0.0), math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(1.0, -0.0), math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-1.0, 0.0), -math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-1.0, -0.0), -math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(math.inf(f32), math.inf(f32)), math.pi / 4.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-math.inf(f32), math.inf(f32)), -math.pi / 4.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(math.inf(f32), -math.inf(f32)), 3.0 * math.pi / 4.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-math.inf(f32), -math.inf(f32)), -3.0 * math.pi / 4.0, epsilon));
    try expect(atan2_32(1.0, math.inf(f32)) == 0.0);
    try expect(math.approxEqAbs(f32, atan2_32(1.0, -math.inf(f32)), math.pi, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-1.0, -math.inf(f32)), -math.pi, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(math.inf(f32), 1.0), math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-math.inf(f32), 1.0), -math.pi / 2.0, epsilon));
}

test "atan2_64.special" {
    const epsilon = 0.000001;

    try expect(math.isNan(atan2_64(1.0, math.nan(f64))));
    try expect(math.isNan(atan2_64(math.nan(f64), 1.0)));
    try expect(atan2_64(0.0, 5.0) == 0.0);
    try expect(atan2_64(-0.0, 5.0) == -0.0);
    try expect(math.approxEqAbs(f64, atan2_64(0.0, -5.0), math.pi, epsilon));
    //expect(math.approxEqAbs(f64, atan2_64(-0.0, -5.0), -math.pi, .{.rel=0,.abs=epsilon})); TODO support negative zero?
    try expect(math.approxEqAbs(f64, atan2_64(1.0, 0.0), math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(1.0, -0.0), math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-1.0, 0.0), -math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-1.0, -0.0), -math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(math.inf(f64), math.inf(f64)), math.pi / 4.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-math.inf(f64), math.inf(f64)), -math.pi / 4.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(math.inf(f64), -math.inf(f64)), 3.0 * math.pi / 4.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-math.inf(f64), -math.inf(f64)), -3.0 * math.pi / 4.0, epsilon));
    try expect(atan2_64(1.0, math.inf(f64)) == 0.0);
    try expect(math.approxEqAbs(f64, atan2_64(1.0, -math.inf(f64)), math.pi, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-1.0, -math.inf(f64)), -math.pi, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(math.inf(f64), 1.0), math.pi / 2.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-math.inf(f64), 1.0), -math.pi / 2.0, epsilon));
}



---
File: /std/math/atanh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/atanhf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/atanh.c

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const expect = std.testing.expect;
const maxInt = std.math.maxInt;

/// Returns the hyperbolic arc-tangent of x.
///
/// Special Cases:
///  - atanh(+-1) = +-inf with signal
///  - atanh(x)   = nan if |x| > 1 with signal
///  - atanh(nan) = nan
pub fn atanh(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => atanh_32(x),
        f64 => atanh_64(x),
        else => @compileError("atanh not implemented for " ++ @typeName(T)),
    };
}

// atanh(x) = log((1 + x) / (1 - x)) / 2 = log1p(2x / (1 - x)) / 2 ~= x + x^3 / 3 + o(x^5)
fn atanh_32(x: f32) f32 {
    const u = @as(u32, @bitCast(x));
    const i = u & 0x7FFFFFFF;
    const s = u >> 31;

    var y = @as(f32, @bitCast(i)); // |x|

    if (y == 1.0) {
        return math.copysign(math.inf(f32), x);
    }

    if (u < 0x3F800000 - (1 << 23)) {
        if (u < 0x3F800000 - (32 << 23)) {
            // underflow
            if (u < (1 << 23)) {
                mem.doNotOptimizeAway(y * y);
            }
        }
        // |x| < 0.5
        else {
            y = 0.5 * math.log1p(2 * y + 2 * y * y / (1 - y));
        }
    } else {
        y = 0.5 * math.log1p(2 * (y / (1 - y)));
    }

    return if (s != 0) -y else y;
}

fn atanh_64(x: f64) f64 {
    const u: u64 = @bitCast(x);
    const e = (u >> 52) & 0x7FF;
    const s = u >> 63;

    var y: f64 = @bitCast(u & (maxInt(u64) >> 1)); // |x|

    if (y == 1.0) {
        return math.copysign(math.inf(f64), x);
    }

    if (e < 0x3FF - 1) {
        if (e < 0x3FF - 32) {
            // underflow
            if (e == 0) {
                mem.doNotOptimizeAway(@as(f32, @floatCast(y)));
            }
        }
        // |x| < 0.5
        else {
            y = 0.5 * math.log1p(2 * y + 2 * y * y / (1 - y));
        }
    } else {
        y = 0.5 * math.log1p(2 * (y / (1 - y)));
    }

    return if (s != 0) -y else y;
}

test atanh {
    try expect(atanh(@as(f32, 0.0)) == atanh_32(0.0));
    try expect(atanh(@as(f64, 0.0)) == atanh_64(0.0));
}

test atanh_32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, atanh_32(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f32, atanh_32(0.2), 0.202733, epsilon));
    try expect(math.approxEqAbs(f32, atanh_32(0.8923), 1.433099, epsilon));
}

test atanh_64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, atanh_64(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f64, atanh_64(0.2), 0.202733, epsilon));
    try expect(math.approxEqAbs(f64, atanh_64(0.8923), 1.433099, epsilon));
}

test "atanh32.special" {
    try expect(math.isPositiveInf(atanh_32(1)));
    try expect(math.isNegativeInf(atanh_32(-1)));
    try expect(math.isNan(atanh_32(1.5)));
    try expect(math.isNan(atanh_32(-1.5)));
    try expect(math.isNan(atanh_32(math.nan(f32))));
}

test "atanh64.special" {
    try expect(math.isPositiveInf(atanh_64(1)));
    try expect(math.isNegativeInf(atanh_64(-1)));
    try expect(math.isNan(atanh_64(1.5)));
    try expect(math.isNan(atanh_64(-1.5)));
    try expect(math.isNan(atanh_64(math.nan(f64))));
}



---
File: /std/math/big.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;

pub const int = @import("big/int.zig");
pub const Limb = usize;
const limb_info = @typeInfo(Limb).int;
pub const SignedLimb = std.meta.Int(.signed, limb_info.bits);
pub const DoubleLimb = std.meta.Int(.unsigned, 2 * limb_info.bits);
pub const HalfLimb = std.meta.Int(.unsigned, limb_info.bits / 2);
pub const SignedDoubleLimb = std.meta.Int(.signed, 2 * limb_info.bits);
pub const Log2Limb = std.math.Log2Int(Limb);

comptime {
    assert(std.math.floorPowerOfTwo(usize, limb_info.bits) == limb_info.bits);
    assert(limb_info.signedness == .unsigned);
}

test {
    _ = int;
    _ = Limb;
    _ = SignedLimb;
    _ = DoubleLimb;
    _ = SignedDoubleLimb;
    _ = Log2Limb;
}



---
File: /std/math/cbrt.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/cbrtf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/cbrt.c

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns the cube root of x.
///
/// Special Cases:
///  - cbrt(+-0)   = +-0
///  - cbrt(+-inf) = +-inf
///  - cbrt(nan)   = nan
pub fn cbrt(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => cbrt32(x),
        f64 => cbrt64(x),
        else => @compileError("cbrt not implemented for " ++ @typeName(T)),
    };
}

fn cbrt32(x: f32) f32 {
    const B1: u32 = 709958130; // (127 - 127.0 / 3 - 0.03306235651) * 2^23
    const B2: u32 = 642849266; // (127 - 127.0 / 3 - 24 / 3 - 0.03306235651) * 2^23

    var u = @as(u32, @bitCast(x));
    var hx = u & 0x7FFFFFFF;

    // cbrt(nan, inf) = itself
    if (hx >= 0x7F800000) {
        return x + x;
    }

    // cbrt to ~5bits
    if (hx < 0x00800000) {
        // cbrt(+-0) = itself
        if (hx == 0) {
            return x;
        }
        u = @as(u32, @bitCast(x * 0x1.0p24));
        hx = u & 0x7FFFFFFF;
        hx = hx / 3 + B2;
    } else {
        hx = hx / 3 + B1;
    }

    u &= 0x80000000;
    u |= hx;

    // first step newton to 16 bits
    var t: f64 = @as(f32, @bitCast(u));
    var r: f64 = t * t * t;
    t = t * (@as(f64, x) + x + r) / (x + r + r);

    // second step newton to 47 bits
    r = t * t * t;
    t = t * (@as(f64, x) + x + r) / (x + r + r);

    return @as(f32, @floatCast(t));
}

fn cbrt64(x: f64) f64 {
    const B1: u32 = 715094163; // (1023 - 1023 / 3 - 0.03306235651 * 2^20
    const B2: u32 = 696219795; // (1023 - 1023 / 3 - 54 / 3 - 0.03306235651 * 2^20

    // |1 / cbrt(x) - p(x)| < 2^(23.5)
    const P0: f64 = 1.87595182427177009643;
    const P1: f64 = -1.88497979543377169875;
    const P2: f64 = 1.621429720105354466140;
    const P3: f64 = -0.758397934778766047437;
    const P4: f64 = 0.145996192886612446982;

    var u = @as(u64, @bitCast(x));
    var hx = @as(u32, @intCast(u >> 32)) & 0x7FFFFFFF;

    // cbrt(nan, inf) = itself
    if (hx >= 0x7FF00000) {
        return x + x;
    }

    // cbrt to ~5bits
    if (hx < 0x00100000) {
        u = @as(u64, @bitCast(x * 0x1.0p54));
        hx = @as(u32, @intCast(u >> 32)) & 0x7FFFFFFF;

        // cbrt(+-0) = itself
        if (hx == 0) {
            return x;
        }
        hx = hx / 3 + B2;
    } else {
        hx = hx / 3 + B1;
    }

    u &= 1 << 63;
    u |= @as(u64, hx) << 32;
    var t = @as(f64, @bitCast(u));

    // cbrt to 23 bits
    // cbrt(x) = t * cbrt(x / t^3) ~= t * P(t^3 / x)
    const r = (t * t) * (t / x);
    t = t * ((P0 + r * (P1 + r * P2)) + ((r * r) * r) * (P3 + r * P4));

    // Round t away from 0 to 23 bits
    u = @as(u64, @bitCast(t));
    u = (u + 0x80000000) & 0xFFFFFFFFC0000000;
    t = @as(f64, @bitCast(u));

    // one step newton to 53 bits
    const s = t * t;
    var q = x / s;
    const w = t + t;
    q = (q - t) / (w + q);

    return t + t * q;
}

test cbrt {
    try expect(cbrt(@as(f32, 0.0)) == cbrt32(0.0));
    try expect(cbrt(@as(f64, 0.0)) == cbrt64(0.0));
}

test cbrt32 {
    const epsilon = 0.000001;

    try expect(math.isPositiveZero(cbrt32(0.0)));
    try expect(math.approxEqAbs(f32, cbrt32(0.2), 0.584804, epsilon));
    try expect(math.approxEqAbs(f32, cbrt32(0.8923), 0.962728, epsilon));
    try expect(math.approxEqAbs(f32, cbrt32(1.5), 1.144714, epsilon));
    try expect(math.approxEqAbs(f32, cbrt32(37.45), 3.345676, epsilon));
    try expect(math.approxEqAbs(f32, cbrt32(123123.234375), 49.748501, epsilon));
}

test cbrt64 {
    const epsilon = 0.000001;

    try expect(math.isPositiveZero(cbrt64(0.0)));
    try expect(math.approxEqAbs(f64, cbrt64(0.2), 0.584804, epsilon));
    try expect(math.approxEqAbs(f64, cbrt64(0.8923), 0.962728, epsilon));
    try expect(math.approxEqAbs(f64, cbrt64(1.5), 1.144714, epsilon));
    try expect(math.approxEqAbs(f64, cbrt64(37.45), 3.345676, epsilon));
    try expect(math.approxEqAbs(f64, cbrt64(123123.234375), 49.748501, epsilon));
}

test "cbrt.special" {
    try expect(math.isPositiveZero(cbrt32(0.0)));
    try expect(@as(u32, @bitCast(cbrt32(-0.0))) == @as(u32, 0x80000000));
    try expect(math.isPositiveInf(cbrt32(math.inf(f32))));
    try expect(math.isNegativeInf(cbrt32(-math.inf(f32))));
    try expect(math.isNan(cbrt32(math.nan(f32))));
}

test "cbrt64.special" {
    try expect(math.isPositiveZero(cbrt64(0.0)));
    try expect(math.isNegativeZero(cbrt64(-0.0)));
    try expect(math.isPositiveInf(cbrt64(math.inf(f64))));
    try expect(math.isNegativeInf(cbrt64(-math.inf(f64))));
    try expect(math.isNan(cbrt64(math.nan(f64))));
}



---
File: /std/math/complex.zig
---

const std = @import("../std.zig");
const testing = std.testing;
const math = std.math;

pub const abs = @import("complex/abs.zig").abs;
pub const acosh = @import("complex/acosh.zig").acosh;
pub const acos = @import("complex/acos.zig").acos;
pub const arg = @import("complex/arg.zig").arg;
pub const asinh = @import("complex/asinh.zig").asinh;
pub const asin = @import("complex/asin.zig").asin;
pub const atanh = @import("complex/atanh.zig").atanh;
pub const atan = @import("complex/atan.zig").atan;
pub const conj = @import("complex/conj.zig").conj;
pub const cosh = @import("complex/cosh.zig").cosh;
pub const cos = @import("complex/cos.zig").cos;
pub const exp = @import("complex/exp.zig").exp;
pub const log = @import("complex/log.zig").log;
pub const pow = @import("complex/pow.zig").pow;
pub const proj = @import("complex/proj.zig").proj;
pub const sinh = @import("complex/sinh.zig").sinh;
pub const sin = @import("complex/sin.zig").sin;
pub const sqrt = @import("complex/sqrt.zig").sqrt;
pub const tanh = @import("complex/tanh.zig").tanh;
pub const tan = @import("complex/tan.zig").tan;

/// A complex number consisting of a real an imaginary part. T must be a floating-point value.
pub fn Complex(comptime T: type) type {
    return struct {
        const Self = @This();

        /// Real part.
        re: T,

        /// Imaginary part.
        im: T,

        /// Create a new Complex number from the given real and imaginary parts.
        pub fn init(re: T, im: T) Self {
            return Self{
                .re = re,
                .im = im,
            };
        }

        /// Returns the sum of two complex numbers.
        pub fn add(self: Self, other: Self) Self {
            return Self{
                .re = self.re + other.re,
                .im = self.im + other.im,
            };
        }

        /// Returns the subtraction of two complex numbers.
        pub fn sub(self: Self, other: Self) Self {
            return Self{
                .re = self.re - other.re,
                .im = self.im - other.im,
            };
        }

        /// Returns the product of two complex numbers.
        pub fn mul(self: Self, other: Self) Self {
            return Self{
                .re = self.re * other.re - self.im * other.im,
                .im = self.im * other.re + self.re * other.im,
            };
        }

        /// Returns the quotient of two complex numbers.
        pub fn div(self: Self, other: Self) Self {
            const re_num = self.re * other.re + self.im * other.im;
            const im_num = self.im * other.re - self.re * other.im;
            const den = other.re * other.re + other.im * other.im;

            return Self{
                .re = re_num / den,
                .im = im_num / den,
            };
        }

        /// Returns the complex conjugate of a number.
        pub fn conjugate(self: Self) Self {
            return Self{
                .re = self.re,
                .im = -self.im,
            };
        }

        /// Returns the negation of a complex number.
        pub fn neg(self: Self) Self {
            return Self{
                .re = -self.re,
                .im = -self.im,
            };
        }

        /// Returns the product of complex number and i=sqrt(-1)
        pub fn mulbyi(self: Self) Self {
            return Self{
                .re = -self.im,
                .im = self.re,
            };
        }

        /// Returns the reciprocal of a complex number.
        pub fn reciprocal(self: Self) Self {
            const m = self.re * self.re + self.im * self.im;
            return Self{
                .re = self.re / m,
                .im = -self.im / m,
            };
        }

        /// Returns the magnitude of a complex number.
        pub fn magnitude(self: Self) T {
            return @sqrt(self.re * self.re + self.im * self.im);
        }

        pub fn squaredMagnitude(self: Self) T {
            return self.re * self.re + self.im * self.im;
        }
    };
}

const epsilon = 0.0001;

test "add" {
    const a = Complex(f32).init(5, 3);
    const b = Complex(f32).init(2, 7);
    const c = a.add(b);

    try testing.expect(c.re == 7 and c.im == 10);
}

test "sub" {
    const a = Complex(f32).init(5, 3);
    const b = Complex(f32).init(2, 7);
    const c = a.sub(b);

    try testing.expect(c.re == 3 and c.im == -4);
}

test "mul" {
    const a = Complex(f32).init(5, 3);
    const b = Complex(f32).init(2, 7);
    const c = a.mul(b);

    try testing.expect(c.re == -11 and c.im == 41);
}

test "div" {
    const a = Complex(f32).init(5, 3);
    const b = Complex(f32).init(2, 7);
    const c = a.div(b);

    try testing.expect(math.approxEqAbs(f32, c.re, @as(f32, 31) / 53, epsilon) and
        math.approxEqAbs(f32, c.im, @as(f32, -29) / 53, epsilon));
}

test "conjugate" {
    const a = Complex(f32).init(5, 3);
    const c = a.conjugate();

    try testing.expect(c.re == 5 and c.im == -3);
}

test "neg" {
    const a = Complex(f32).init(5, 3);
    const c = a.neg();

    try testing.expect(c.re == -5 and c.im == -3);
}

test "mulbyi" {
    const a = Complex(f32).init(5, 3);
    const c = a.mulbyi();

    try testing.expect(c.re == -3 and c.im == 5);
}

test "reciprocal" {
    const a = Complex(f32).init(5, 3);
    const c = a.reciprocal();

    try testing.expect(math.approxEqAbs(f32, c.re, @as(f32, 5) / 34, epsilon) and
        math.approxEqAbs(f32, c.im, @as(f32, -3) / 34, epsilon));
}

test "magnitude" {
    const a = Complex(f32).init(5, 3);
    const c = a.magnitude();

    try testing.expect(math.approxEqAbs(f32, c, 5.83095, epsilon));
}

test "squaredMagnitude" {
    const a = Complex(f32).init(5, 3);
    const c = a.squaredMagnitude();

    try testing.expect(math.approxEqAbs(f32, c, math.pow(f32, a.magnitude(), 2), epsilon));
}

test {
    _ = @import("complex/abs.zig");
    _ = @import("complex/acosh.zig");
    _ = @import("complex/acos.zig");
    _ = @import("complex/arg.zig");
    _ = @import("complex/asinh.zig");
    _ = @import("complex/asin.zig");
    _ = @import("complex/atanh.zig");
    _ = @import("complex/atan.zig");
    _ = @import("complex/conj.zig");
    _ = @import("complex/cosh.zig");
    _ = @import("complex/cos.zig");
    _ = @import("complex/exp.zig");
    _ = @import("complex/log.zig");
    _ = @import("complex/pow.zig");
    _ = @import("complex/proj.zig");
    _ = @import("complex/sinh.zig");
    _ = @import("complex/sin.zig");
    _ = @import("complex/sqrt.zig");
    _ = @import("complex/tanh.zig");
    _ = @import("complex/tan.zig");
}



---
File: /std/math/copysign.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns a value with the magnitude of `magnitude` and the sign of `sign`.
pub fn copysign(magnitude: anytype, sign: @TypeOf(magnitude)) @TypeOf(magnitude) {
    const T = @TypeOf(magnitude);
    const TBits = std.meta.Int(.unsigned, @typeInfo(T).float.bits);
    const sign_bit_mask = @as(TBits, 1) << (@bitSizeOf(T) - 1);
    const mag = @as(TBits, @bitCast(magnitude)) & ~sign_bit_mask;
    const sgn = @as(TBits, @bitCast(sign)) & sign_bit_mask;
    return @as(T, @bitCast(mag | sgn));
}

test copysign {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        try expect(copysign(@as(T, 1.0), @as(T, 1.0)) == 1.0);
        try expect(copysign(@as(T, 2.0), @as(T, -2.0)) == -2.0);
        try expect(copysign(@as(T, -3.0), @as(T, 3.0)) == 3.0);
        try expect(copysign(@as(T, -4.0), @as(T, -4.0)) == -4.0);
        try expect(copysign(@as(T, 5.0), @as(T, -500.0)) == -5.0);
        try expect(copysign(math.inf(T), @as(T, -0.0)) == -math.inf(T));
        try expect(copysign(@as(T, 6.0), -math.nan(T)) == -6.0);
    }
}



---
File: /std/math/cosh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/coshf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/cosh.c

const std = @import("../std.zig");
const math = std.math;
const expo2 = @import("expo2.zig").expo2;
const expect = std.testing.expect;
const maxInt = std.math.maxInt;

/// Returns the hyperbolic cosine of x.
///
/// Special Cases:
///  - cosh(+-0)   = 1
///  - cosh(+-inf) = +inf
///  - cosh(nan)   = nan
pub fn cosh(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => cosh32(x),
        f64 => cosh64(x),
        else => @compileError("cosh not implemented for " ++ @typeName(T)),
    };
}

// cosh(x) = (exp(x) + 1 / exp(x)) / 2
//         = 1 + 0.5 * (exp(x) - 1) * (exp(x) - 1) / exp(x)
//         = 1 + (x * x) / 2 + o(x^4)
fn cosh32(x: f32) f32 {
    const u = @as(u32, @bitCast(x));
    const ux = u & 0x7FFFFFFF;
    const ax = @as(f32, @bitCast(ux));

    // |x| < log(2)
    if (ux < 0x3F317217) {
        if (ux < 0x3F800000 - (12 << 23)) {
            math.raiseOverflow();
            return 1.0;
        }
        const t = math.expm1(ax);
        return 1 + t * t / (2 * (1 + t));
    }

    // |x| < log(FLT_MAX)
    if (ux < 0x42B17217) {
        const t = @exp(ax);
        return 0.5 * (t + 1 / t);
    }

    // |x| > log(FLT_MAX) or nan
    return expo2(ax);
}

fn cosh64(x: f64) f64 {
    const u = @as(u64, @bitCast(x));
    const w = @as(u32, @intCast(u >> 32)) & (maxInt(u32) >> 1);
    const ax = @as(f64, @bitCast(u & (maxInt(u64) >> 1)));

    // TODO: Shouldn't need this explicit check.
    if (x == 0.0) {
        return 1.0;
    }

    // |x| < log(2)
    if (w < 0x3FE62E42) {
        if (w < 0x3FF00000 - (26 << 20)) {
            if (x != 0) {
                math.raiseInexact();
            }
            return 1.0;
        }
        const t = math.expm1(ax);
        return 1 + t * t / (2 * (1 + t));
    }

    // |x| < log(DBL_MAX)
    if (w < 0x40862E42) {
        const t = @exp(ax);
        // NOTE: If x > log(0x1p26) then 1/t is not required.
        return 0.5 * (t + 1 / t);
    }

    // |x| > log(CBL_MAX) or nan
    return expo2(ax);
}

test cosh {
    try expect(cosh(@as(f32, 1.5)) == cosh32(1.5));
    try expect(cosh(@as(f64, 1.5)) == cosh64(1.5));
}

test cosh32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, cosh32(0.0), 1.0, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(0.2), 1.020067, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(0.8923), 1.425225, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(1.5), 2.352410, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(-0.0), 1.0, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(-0.2), 1.020067, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(-0.8923), 1.425225, epsilon));
    try expect(math.approxEqAbs(f32, cosh32(-1.5), 2.352410, epsilon));
}

test cosh64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, cosh64(0.0), 1.0, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(0.2), 1.020067, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(0.8923), 1.425225, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(1.5), 2.352410, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(-0.0), 1.0, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(-0.2), 1.020067, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(-0.8923), 1.425225, epsilon));
    try expect(math.approxEqAbs(f64, cosh64(-1.5), 2.352410, epsilon));
}

test "cosh32.special" {
    try expect(cosh32(0.0) == 1.0);
    try expect(cosh32(-0.0) == 1.0);
    try expect(math.isPositiveInf(cosh32(math.inf(f32))));
    try expect(math.isPositiveInf(cosh32(-math.inf(f32))));
    try expect(math.isNan(cosh32(math.nan(f32))));
}

test "cosh64.special" {
    try expect(cosh64(0.0) == 1.0);
    try expect(cosh64(-0.0) == 1.0);
    try expect(math.isPositiveInf(cosh64(math.inf(f64))));
    try expect(math.isPositiveInf(cosh64(-math.inf(f64))));
    try expect(math.isNan(cosh64(math.nan(f64))));
}



---
File: /std/math/expm1.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/expmf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/expm.c

// TODO: Updated recently.

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;

/// Returns e raised to the power of x, minus 1 (e^x - 1). This is more accurate than exp(e, x) - 1
/// when x is near 0.
///
/// Special Cases:
///  - expm1(+inf) = +inf
///  - expm1(-inf) = -1
///  - expm1(nan)  = nan
pub fn expm1(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => expm1_32(x),
        f64 => expm1_64(x),
        else => @compileError("exp1m not implemented for " ++ @typeName(T)),
    };
}

fn expm1_32(x_: f32) f32 {
    if (math.isNan(x_))
        return math.nan(f32);

    const o_threshold: f32 = 8.8721679688e+01;
    const ln2_hi: f32 = 6.9313812256e-01;
    const ln2_lo: f32 = 9.0580006145e-06;
    const invln2: f32 = 1.4426950216e+00;
    const Q1: f32 = -3.3333212137e-2;
    const Q2: f32 = 1.5807170421e-3;

    var x = x_;
    const ux: u32 = @bitCast(x);
    const hx = ux & 0x7FFFFFFF;
    const sign = ux >> 31;

    // TODO: Shouldn't need this check explicitly.
    if (math.isNegativeInf(x)) {
        return -1.0;
    }

    // |x| >= 27 * ln2
    if (hx >= 0x4195B844) {
        // nan
        if (hx > 0x7F800000) {
            return x;
        }
        if (sign != 0) {
            return -1;
        }
        if (x > o_threshold) {
            x *= 0x1.0p127;
            return x;
        }
    }

    var hi: f32 = undefined;
    var lo: f32 = undefined;
    var c: f32 = undefined;
    var k: i32 = undefined;

    // |x| > 0.5 * ln2
    if (hx > 0x3EB17218) {
        // |x| < 1.5 * ln2
        if (hx < 0x3F851592) {
            if (sign == 0) {
                hi = x - ln2_hi;
                lo = ln2_lo;
                k = 1;
            } else {
                hi = x + ln2_hi;
                lo = -ln2_lo;
                k = -1;
            }
        } else {
            var kf = invln2 * x;
            if (sign != 0) {
                kf -= 0.5;
            } else {
                kf += 0.5;
            }

            k = @as(i32, @intFromFloat(kf));
            const t = @as(f32, @floatFromInt(k));
            hi = x - t * ln2_hi;
            lo = t * ln2_lo;
        }

        x = hi - lo;
        c = (hi - x) - lo;
    }
    // |x| < 2^(-25)
    else if (hx < 0x33000000) {
        if (hx < 0x00800000) {
            mem.doNotOptimizeAway(x * x);
        }
        return x;
    } else {
        k = 0;
    }

    const hfx = 0.5 * x;
    const hxs = x * hfx;
    const r1 = 1.0 + hxs * (Q1 + hxs * Q2);
    const t = 3.0 - r1 * hfx;
    var e = hxs * ((r1 - t) / (6.0 - x * t));

    // c is 0
    if (k == 0) {
        return x - (x * e - hxs);
    }

    e = x * (e - c) - c;
    e -= hxs;

    // exp(x) ~ 2^k (x_reduced - e + 1)
    if (k == -1) {
        return 0.5 * (x - e) - 0.5;
    }
    if (k == 1) {
        if (x < -0.25) {
            return -2.0 * (e - (x + 0.5));
        } else {
            return 1.0 + 2.0 * (x - e);
        }
    }

    const twopk = @as(f32, @bitCast(@as(u32, @intCast((0x7F +% k) << 23))));

    if (k < 0 or k > 56) {
        var y = x - e + 1.0;
        if (k == 128) {
            y = y * 2.0 * 0x1.0p127;
        } else {
            y = y * twopk;
        }

        return y - 1.0;
    }

    const uf: f32 = @bitCast(@as(u32, @intCast(0x7F -% k)) << 23);
    if (k < 23) {
        return (x - e + (1 - uf)) * twopk;
    } else {
        return (x - (e + uf) + 1) * twopk;
    }
}

fn expm1_64(x_: f64) f64 {
    if (math.isNan(x_))
        return math.nan(f64);

    const o_threshold: f64 = 7.09782712893383973096e+02;
    const ln2_hi: f64 = 6.93147180369123816490e-01;
    const ln2_lo: f64 = 1.90821492927058770002e-10;
    const invln2: f64 = 1.44269504088896338700e+00;
    const Q1: f64 = -3.33333333333331316428e-02;
    const Q2: f64 = 1.58730158725481460165e-03;
    const Q3: f64 = -7.93650757867487942473e-05;
    const Q4: f64 = 4.00821782732936239552e-06;
    const Q5: f64 = -2.01099218183624371326e-07;

    var x = x_;
    const ux = @as(u64, @bitCast(x));
    const hx = @as(u32, @intCast(ux >> 32)) & 0x7FFFFFFF;
    const sign = ux >> 63;

    if (math.isNegativeInf(x)) {
        return -1.0;
    }

    // |x| >= 56 * ln2
    if (hx >= 0x4043687A) {
        // exp1md(nan) = nan
        if (hx > 0x7FF00000) {
            return x;
        }
        // exp1md(-ve) = -1
        if (sign != 0) {
            return -1;
        }
        if (x > o_threshold) {
            math.raiseOverflow();
            return math.inf(f64);
        }
    }

    var hi: f64 = undefined;
    var lo: f64 = undefined;
    var c: f64 = undefined;
    var k: i32 = undefined;

    // |x| > 0.5 * ln2
    if (hx > 0x3FD62E42) {
        // |x| < 1.5 * ln2
        if (hx < 0x3FF0A2B2) {
            if (sign == 0) {
                hi = x - ln2_hi;
                lo = ln2_lo;
                k = 1;
            } else {
                hi = x + ln2_hi;
                lo = -ln2_lo;
                k = -1;
            }
        } else {
            var kf = invln2 * x;
            if (sign != 0) {
                kf -= 0.5;
            } else {
                kf += 0.5;
            }

            k = @as(i32, @intFromFloat(kf));
            const t = @as(f64, @floatFromInt(k));
            hi = x - t * ln2_hi;
            lo = t * ln2_lo;
        }

        x = hi - lo;
        c = (hi - x) - lo;
    }
    // |x| < 2^(-54)
    else if (hx < 0x3C900000) {
        if (hx < 0x00100000) {
            mem.doNotOptimizeAway(@as(f32, @floatCast(x)));
        }
        return x;
    } else {
        k = 0;
    }

    const hfx = 0.5 * x;
    const hxs = x * hfx;
    const r1 = 1.0 + hxs * (Q1 + hxs * (Q2 + hxs * (Q3 + hxs * (Q4 + hxs * Q5))));
    const t = 3.0 - r1 * hfx;
    var e = hxs * ((r1 - t) / (6.0 - x * t));

    // c is 0
    if (k == 0) {
        return x - (x * e - hxs);
    }

    e = x * (e - c) - c;
    e -= hxs;

    // exp(x) ~ 2^k (x_reduced - e + 1)
    if (k == -1) {
        return 0.5 * (x - e) - 0.5;
    }
    if (k == 1) {
        if (x < -0.25) {
            return -2.0 * (e - (x + 0.5));
        } else {
            return 1.0 + 2.0 * (x - e);
        }
    }

    const twopk = @as(f64, @bitCast(@as(u64, @intCast(0x3FF +% k)) << 52));

    if (k < 0 or k > 56) {
        var y = x - e + 1.0;
        if (k == 1024) {
            y = y * 2.0 * 0x1.0p1023;
        } else {
            y = y * twopk;
        }

        return y - 1.0;
    }

    const uf = @as(f64, @bitCast(@as(u64, @intCast(0x3FF -% k)) << 52));
    if (k < 20) {
        return (x - e + (1 - uf)) * twopk;
    } else {
        return (x - (e + uf) + 1) * twopk;
    }
}

test "expm1_32() special" {
    try expect(math.isPositiveZero(expm1_32(0.0)));
    try expect(math.isNegativeZero(expm1_32(-0.0)));
    try expectEqual(expm1_32(math.ln2), 1.0);
    try expectEqual(expm1_32(math.inf(f32)), math.inf(f32));
    try expectEqual(expm1_32(-math.inf(f32)), -1.0);
    try expect(math.isNan(expm1_32(math.nan(f32))));
    try expect(math.isNan(expm1_32(math.snan(f32))));
}

test "expm1_32() sanity" {
    try expectEqual(expm1_32(-0x1.0223a0p+3), -0x1.ffd6e0p-1);
    try expectEqual(expm1_32(0x1.161868p+2), 0x1.30712ap+6);
    try expectEqual(expm1_32(-0x1.0c34b4p+3), -0x1.ffe1fap-1);
    try expectEqual(expm1_32(-0x1.a206f0p+2), -0x1.ff4116p-1);
    try expectEqual(expm1_32(0x1.288bbcp+3), 0x1.4ab480p+13); // Disagrees with GCC in last bit
    try expectEqual(expm1_32(0x1.52efd0p-1), 0x1.e09536p-1);
    try expectEqual(expm1_32(-0x1.a05cc8p-2), -0x1.561c3ep-2);
    try expectEqual(expm1_32(0x1.1f9efap-1), 0x1.81ec4ep-1);
    try expectEqual(expm1_32(0x1.8c5db0p-1), 0x1.2b3364p+0);
    try expectEqual(expm1_32(-0x1.5b86eap-1), -0x1.f8951ap-2);
}

test "expm1_32() boundary" {
    // TODO: The last value before inf is actually 0x1.62e300p+6 -> 0x1.ff681ep+127
    // try expectEqual(expm1_32(0x1.62e42ep+6), 0x1.ffff08p+127); // Last value before result is inf
    try expectEqual(expm1_32(0x1.62e430p+6), math.inf(f32)); // First value that gives inf
    try expectEqual(expm1_32(0x1.fffffep+127), math.inf(f32)); // Max input value
    try expectEqual(expm1_32(0x1p-149), 0x1p-149); // Min positive input value
    try expectEqual(expm1_32(-0x1p-149), -0x1p-149); // Min negative input value
    try expectEqual(expm1_32(0x1p-126), 0x1p-126); // First positive subnormal input
    try expectEqual(expm1_32(-0x1p-126), -0x1p-126); // First negative subnormal input
    try expectEqual(expm1_32(0x1.fffffep-125), 0x1.fffffep-125); // Last positive value before subnormal
    try expectEqual(expm1_32(-0x1.fffffep-125), -0x1.fffffep-125); // Last negative value before subnormal
    try expectEqual(expm1_32(-0x1.154244p+4), -0x1.fffffep-1); // Last value before result is -1
    try expectEqual(expm1_32(-0x1.154246p+4), -1); // First value where result is -1
}

test "expm1_64() special" {
    try expect(math.isPositiveZero(expm1_64(0.0)));
    try expect(math.isNegativeZero(expm1_64(-0.0)));
    try expectEqual(expm1_64(math.ln2), 1.0);
    try expectEqual(expm1_64(math.inf(f64)), math.inf(f64));
    try expectEqual(expm1_64(-math.inf(f64)), -1.0);
    try expect(math.isNan(expm1_64(math.nan(f64))));
    try expect(math.isNan(expm1_64(math.snan(f64))));
}

test "expm1_64() sanity" {
    try expectEqual(expm1_64(-0x1.02239f3c6a8f1p+3), -0x1.ffd6df9b02b3ep-1);
    try expectEqual(expm1_64(0x1.161868e18bc67p+2), 0x1.30712ed238c04p+6);
    try expectEqual(expm1_64(-0x1.0c34b3e01e6e7p+3), -0x1.ffe1f94e493e7p-1);
    try expectEqual(expm1_64(-0x1.a206f0a19dcc4p+2), -0x1.ff4115c03f78dp-1);
    try expectEqual(expm1_64(0x1.288bbb0d6a1e6p+3), 0x1.4ab477496e07ep+13);
    try expectEqual(expm1_64(0x1.52efd0cd80497p-1), 0x1.e095382100a01p-1);
    try expectEqual(expm1_64(-0x1.a05cc754481d1p-2), -0x1.561c3e0582be6p-2);
    try expectEqual(expm1_64(0x1.1f9ef934745cbp-1), 0x1.81ec4cd4d4a8fp-1);
    try expectEqual(expm1_64(0x1.8c5db097f7442p-1), 0x1.2b3363a944bf7p+0);
    try expectEqual(expm1_64(-0x1.5b86ea8118a0ep-1), -0x1.f8951aebffbafp-2);
}

test "expm1_64() boundary" {
    try expectEqual(expm1_64(0x1.62e42fefa39efp+9), 0x1.fffffffffff2ap+1023); // Last value before result is inf
    try expectEqual(expm1_64(0x1.62e42fefa39f0p+9), math.inf(f64)); // First value that gives inf
    try expectEqual(expm1_64(0x1.fffffffffffffp+1023), math.inf(f64)); // Max input value
    try expectEqual(expm1_64(0x1p-1074), 0x1p-1074); // Min positive input value
    try expectEqual(expm1_64(-0x1p-1074), -0x1p-1074); // Min negative input value
    try expectEqual(expm1_64(0x1p-1022), 0x1p-1022); // First positive subnormal input
    try expectEqual(expm1_64(-0x1p-1022), -0x1p-1022); // First negative subnormal input
    try expectEqual(expm1_64(0x1.fffffffffffffp-1021), 0x1.fffffffffffffp-1021); // Last positive value before subnormal
    try expectEqual(expm1_64(-0x1.fffffffffffffp-1021), -0x1.fffffffffffffp-1021); // Last negative value before subnormal
    try expectEqual(expm1_64(-0x1.2b708872320e1p+5), -0x1.fffffffffffffp-1); // Last value before result is -1
    try expectEqual(expm1_64(-0x1.2b708872320e2p+5), -1); // First value where result is -1
}



---
File: /std/math/expo2.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/__expo2f.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/__expo2.c

const math = @import("../math.zig");

/// Returns exp(x) / 2 for x >= log(maxFloat(T)).
pub fn expo2(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => expo2f(x),
        f64 => expo2d(x),
        else => @compileError("expo2 not implemented for " ++ @typeName(T)),
    };
}

fn expo2f(x: f32) f32 {
    const k: u32 = 235;
    const kln2 = 0x1.45C778p+7;

    const u = (0x7F + k / 2) << 23;
    const scale = @as(f32, @bitCast(u));
    return @exp(x - kln2) * scale * scale;
}

fn expo2d(x: f64) f64 {
    const k: u32 = 2043;
    const kln2 = 0x1.62066151ADD8BP+10;

    const u = (0x3FF + k / 2) << 20;
    const scale = @as(f64, @bitCast(@as(u64, u) << 32));
    return @exp(x - kln2) * scale * scale;
}



---
File: /std/math/float.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");
const assert = std.debug.assert;
const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;

pub fn FloatRepr(comptime Float: type) type {
    const fractional_bits = floatFractionalBits(Float);
    const exponent_bits = floatExponentBits(Float);
    return packed struct {
        const Repr = @This();

        mantissa: StoredMantissa,
        exponent: BiasedExponent,
        sign: std.math.Sign,

        pub const StoredMantissa = @Int(.unsigned, floatMantissaBits(Float));
        pub const Mantissa = @Int(.unsigned, 1 + fractional_bits);
        pub const Exponent = @Int(.signed, exponent_bits);
        pub const BiasedExponent = enum(@Int(.unsigned, exponent_bits)) {
            denormal = 0,
            min_normal = 1,
            zero = (1 << (exponent_bits - 1)) - 1,
            max_normal = (1 << exponent_bits) - 2,
            infinite = (1 << exponent_bits) - 1,
            _,

            pub const Int = @typeInfo(BiasedExponent).@"enum".tag_type;

            pub fn unbias(biased: BiasedExponent) Exponent {
                switch (biased) {
                    .denormal => unreachable,
                    else => return @bitCast(@intFromEnum(biased) -% @intFromEnum(BiasedExponent.zero)),
                    .infinite => unreachable,
                }
            }

            pub fn bias(unbiased: Exponent) BiasedExponent {
                return @enumFromInt(@intFromEnum(BiasedExponent.zero) +% @as(Int, @bitCast(unbiased)));
            }
        };

        pub const Normalized = struct {
            fraction: Fraction,
            exponent: Normalized.Exponent,

            pub const Fraction = @Int(.unsigned, fractional_bits);
            pub const Exponent = @Int(.signed, 1 + exponent_bits);

            /// This currently truncates denormal values, which needs to be fixed before this can be used to
            /// produce a rounded value.
            pub fn reconstruct(normalized: Normalized, sign: std.math.Sign) Float {
                if (normalized.exponent > BiasedExponent.max_normal.unbias()) return @bitCast(Repr{
                    .mantissa = 0,
                    .exponent = .infinite,
                    .sign = sign,
                });
                const mantissa = @as(Mantissa, 1 << fractional_bits) | normalized.fraction;
                if (normalized.exponent < BiasedExponent.min_normal.unbias()) return @bitCast(Repr{
                    .mantissa = @truncate(std.math.shr(
                        Mantissa,
                        mantissa,
                        BiasedExponent.min_normal.unbias() - normalized.exponent,
                    )),
                    .exponent = .denormal,
                    .sign = sign,
                });
                return @bitCast(Repr{
                    .mantissa = @truncate(mantissa),
                    .exponent = .bias(@intCast(normalized.exponent)),
                    .sign = sign,
                });
            }
        };

        pub const Classified = union(enum) { normalized: Normalized, infinity, nan, invalid };
        fn classify(repr: Repr) Classified {
            return switch (repr.exponent) {
                .denormal => {
                    const mantissa: Mantissa = repr.mantissa;
                    const shift = @clz(mantissa);
                    return .{ .normalized = .{
                        .fraction = @truncate(mantissa << shift),
                        .exponent = @as(Normalized.Exponent, comptime BiasedExponent.min_normal.unbias()) - shift,
                    } };
                },
                else => if (repr.mantissa <= std.math.maxInt(Normalized.Fraction)) .{ .normalized = .{
                    .fraction = @intCast(repr.mantissa),
                    .exponent = repr.exponent.unbias(),
                } } else .invalid,
                .infinite => switch (repr.mantissa) {
                    0 => .infinity,
                    else => .nan,
                },
            };
        }
    };
}

/// Creates a raw "1.0" mantissa for floating point type T. Used to dedupe f80 logic.
inline fn mantissaOne(comptime T: type) comptime_int {
    return if (@typeInfo(T).float.bits == 80) 1 << floatFractionalBits(T) else 0;
}

/// Creates floating point type T from an unbiased exponent and raw mantissa.
inline fn reconstructFloat(comptime T: type, comptime exponent: comptime_int, comptime mantissa: comptime_int) T {
    const TBits = @Int(.unsigned, @bitSizeOf(T));
    const biased_exponent = @as(TBits, exponent + floatExponentMax(T));
    return @as(T, @bitCast((biased_exponent << floatMantissaBits(T)) | @as(TBits, mantissa)));
}

/// Returns the number of bits in the exponent of floating point type T.
pub inline fn floatExponentBits(comptime T: type) comptime_int {
    comptime assert(@typeInfo(T) == .float);

    return switch (@typeInfo(T).float.bits) {
        16 => 5,
        32 => 8,
        64 => 11,
        80 => 15,
        128 => 15,
        else => @compileError("unknown floating point type " ++ @typeName(T)),
    };
}

/// Returns the number of bits in the mantissa of floating point type T.
pub inline fn floatMantissaBits(comptime T: type) comptime_int {
    comptime assert(@typeInfo(T) == .float);

    return switch (@typeInfo(T).float.bits) {
        16 => 10,
        32 => 23,
        64 => 52,
        80 => 64,
        128 => 112,
        else => @compileError("unknown floating point type " ++ @typeName(T)),
    };
}

/// Returns the number of fractional bits in the mantissa of floating point type T.
pub inline fn floatFractionalBits(comptime T: type) comptime_int {
    comptime assert(@typeInfo(T) == .float);

    // standard IEEE floats have an implicit 0.m or 1.m integer part
    // f80 is special and has an explicitly stored bit in the MSB
    // this function corresponds to `MANT_DIG - 1' from C
    return switch (@typeInfo(T).float.bits) {
        16 => 10,
        32 => 23,
        64 => 52,
        80 => 63,
        128 => 112,
        else => @compileError("unknown floating point type " ++ @typeName(T)),
    };
}

/// Returns the minimum exponent that can represent
/// a normalised value in floating point type T.
pub inline fn floatExponentMin(comptime T: type) comptime_int {
    return -floatExponentMax(T) + 1;
}

/// Returns the maximum exponent that can represent
/// a normalised value in floating point type T.
pub inline fn floatExponentMax(comptime T: type) comptime_int {
    return (1 << (floatExponentBits(T) - 1)) - 1;
}

/// Returns the smallest subnormal number representable in floating point type T.
pub inline fn floatTrueMin(comptime T: type) T {
    return reconstructFloat(T, floatExponentMin(T) - 1, 1);
}

/// Returns the smallest normal number representable in floating point type T.
pub inline fn floatMin(comptime T: type) T {
    return reconstructFloat(T, floatExponentMin(T), mantissaOne(T));
}

/// Returns the largest normal number representable in floating point type T.
pub inline fn floatMax(comptime T: type) T {
    const all1s_mantissa = (1 << floatMantissaBits(T)) - 1;
    return reconstructFloat(T, floatExponentMax(T), all1s_mantissa);
}

/// Returns the machine epsilon of floating point type T.
pub inline fn floatEps(comptime T: type) T {
    return reconstructFloat(T, -floatFractionalBits(T), mantissaOne(T));
}

/// Returns the local epsilon of floating point type T.
pub inline fn floatEpsAt(comptime T: type, x: T) T {
    switch (@typeInfo(T)) {
        .float => |F| {
            const U: type = @Int(.unsigned, F.bits);
            const u: U = @bitCast(x);
            const y: T = @bitCast(u ^ 1);
            return @abs(x - y);
        },
        else => @compileError("floatEpsAt only supports floats"),
    }
}

/// Returns the inf value for a floating point `Type`.
pub inline fn inf(comptime Type: type) Type {
    const RuntimeType = switch (Type) {
        else => Type,
        comptime_float => f128, // any float type will do
    };
    return reconstructFloat(RuntimeType, floatExponentMax(RuntimeType) + 1, mantissaOne(RuntimeType));
}

/// Returns the canonical quiet NaN representation for a floating point `Type`.
pub inline fn nan(comptime Type: type) Type {
    const RuntimeType = switch (Type) {
        else => Type,
        comptime_float => f128, // any float type will do
    };
    return reconstructFloat(
        RuntimeType,
        floatExponentMax(RuntimeType) + 1,
        mantissaOne(RuntimeType) | 1 << (floatFractionalBits(RuntimeType) - 1),
    );
}

/// Returns a signalling NaN representation for a floating point `Type`.
///
/// TODO: LLVM is known to miscompile on some architectures to quiet NaN -
///       this is tracked by https://github.com/ziglang/zig/issues/14366
pub inline fn snan(comptime Type: type) Type {
    const RuntimeType = switch (Type) {
        else => Type,
        comptime_float => f128, // any float type will do
    };
    return reconstructFloat(
        RuntimeType,
        floatExponentMax(RuntimeType) + 1,
        mantissaOne(RuntimeType) | 1 << (floatFractionalBits(RuntimeType) - 2),
    );
}

fn floatBits(comptime Type: type) !void {
    // (1 +) for the sign bit, since it is separate from the other bits
    const size = 1 + floatExponentBits(Type) + floatMantissaBits(Type);
    try expect(@bitSizeOf(Type) == size);
    try expect(floatFractionalBits(Type) <= floatMantissaBits(Type));

    // for machine epsilon, assert expmin <= -prec <= expmax
    try expect(floatExponentMin(Type) <= -floatFractionalBits(Type));
    try expect(-floatFractionalBits(Type) <= floatExponentMax(Type));
}
test floatBits {
    try floatBits(f16);
    try floatBits(f32);
    try floatBits(f64);
    try floatBits(f80);
    try floatBits(f128);
    try floatBits(c_longdouble);
}

test inf {
    const inf_u16: u16 = 0x7C00;
    const inf_u32: u32 = 0x7F800000;
    const inf_u64: u64 = 0x7FF0000000000000;
    const inf_u80: u80 = 0x7FFF8000000000000000;
    const inf_u128: u128 = 0x7FFF0000000000000000000000000000;
    try expectEqual(inf_u16, @as(u16, @bitCast(inf(f16))));
    try expectEqual(inf_u32, @as(u32, @bitCast(inf(f32))));
    try expectEqual(inf_u64, @as(u64, @bitCast(inf(f64))));
    try expectEqual(inf_u80, @as(u80, @bitCast(inf(f80))));
    try expectEqual(inf_u128, @as(u128, @bitCast(inf(f128))));
}

test nan {
    const qnan_u16: u16 = 0x7E00;
    const qnan_u32: u32 = 0x7FC00000;
    const qnan_u64: u64 = 0x7FF8000000000000;
    const qnan_u80: u80 = 0x7FFFC000000000000000;
    const qnan_u128: u128 = 0x7FFF8000000000000000000000000000;
    try expectEqual(qnan_u16, @as(u16, @bitCast(nan(f16))));
    try expectEqual(qnan_u32, @as(u32, @bitCast(nan(f32))));
    try expectEqual(qnan_u64, @as(u64, @bitCast(nan(f64))));
    try expectEqual(qnan_u80, @as(u80, @bitCast(nan(f80))));
    try expectEqual(qnan_u128, @as(u128, @bitCast(nan(f128))));
}

test snan {
    const snan_u16: u16 = 0x7D00;
    const snan_u32: u32 = 0x7FA00000;
    const snan_u64: u64 = 0x7FF4000000000000;
    const snan_u80: u80 = 0x7FFFA000000000000000;
    const snan_u128: u128 = 0x7FFF4000000000000000000000000000;
    try expectEqual(snan_u16, @as(u16, @bitCast(snan(f16))));
    try expectEqual(snan_u32, @as(u32, @bitCast(snan(f32))));
    try expectEqual(snan_u64, @as(u64, @bitCast(snan(f64))));
    try expectEqual(snan_u80, @as(u80, @bitCast(snan(f80))));
    try expectEqual(snan_u128, @as(u128, @bitCast(snan(f128))));
}



---
File: /std/math/frexp.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;
const expectApproxEqAbs = std.testing.expectApproxEqAbs;

pub fn Frexp(comptime T: type) type {
    return struct {
        significand: T,
        exponent: i32,
    };
}

/// Breaks x into a normalized fraction and an integral power of two.
/// f == frac * 2^exp, with |frac| in the interval [0.5, 1).
///
/// Special Cases:
///  - frexp(+-0)   = +-0, 0
///  - frexp(+-inf) = +-inf, 0
///  - frexp(nan)   = nan, undefined
pub fn frexp(x: anytype) Frexp(@TypeOf(x)) {
    const T: type = @TypeOf(x);

    const bits: comptime_int = @typeInfo(T).float.bits;
    const Int: type = std.meta.Int(.unsigned, bits);

    const exp_bits: comptime_int = math.floatExponentBits(T);
    const mant_bits: comptime_int = math.floatMantissaBits(T);
    const frac_bits: comptime_int = math.floatFractionalBits(T);
    const exp_min: comptime_int = math.floatExponentMin(T);

    const ExpInt: type = std.meta.Int(.unsigned, exp_bits);
    const MantInt: type = std.meta.Int(.unsigned, mant_bits);
    const FracInt: type = std.meta.Int(.unsigned, frac_bits);

    const unreal_exponent: comptime_int = (1 << exp_bits) - 1;
    const bias: comptime_int = (1 << (exp_bits - 1)) - 2;
    const exp_mask: comptime_int = unreal_exponent << mant_bits;
    const zero_exponent: comptime_int = bias << mant_bits;
    const sign_mask: comptime_int = 1 << (bits - 1);
    const not_exp: comptime_int = ~@as(Int, exp_mask);
    const ones_place: comptime_int = mant_bits - frac_bits;
    const extra_denorm_shift: comptime_int = 1 - ones_place;

    var result: Frexp(T) = undefined;
    var v: Int = @bitCast(x);

    const m: MantInt = @truncate(v);
    const e: ExpInt = @truncate(v >> mant_bits);

    switch (e) {
        0 => {
            if (m != 0) {
                // subnormal
                const offset = @clz(m);
                const shift = offset + extra_denorm_shift;

                v &= sign_mask;
                v |= zero_exponent;
                v |= math.shl(MantInt, m, shift);

                result.exponent = exp_min - @as(i32, offset) + ones_place;
            } else {
                // +-0 = (+-0, 0)
                result.exponent = 0;
            }
        },
        unreal_exponent => {
            // +-nan -> {+-nan, undefined}
            result.exponent = undefined;

            // +-inf -> {+-inf, 0}
            if (@as(FracInt, @truncate(v)) == 0)
                result.exponent = 0;
        },
        else => {
            // normal
            v &= not_exp;
            v |= zero_exponent;
            result.exponent = @as(i32, e) - bias;
        },
    }

    result.significand = @bitCast(v);
    return result;
}

/// Generate a namespace of tests for frexp on values of the given type
fn FrexpTests(comptime Float: type) type {
    return struct {
        const T = Float;
        test "normal" {
            const epsilon = 1e-6;
            var r: Frexp(T) = undefined;

            r = frexp(@as(T, 1.3));
            try expectApproxEqAbs(0.65, r.significand, epsilon);
            try expectEqual(1, r.exponent);

            r = frexp(@as(T, 78.0234));
            try expectApproxEqAbs(0.609558, r.significand, epsilon);
            try expectEqual(7, r.exponent);

            r = frexp(@as(T, -1234.5678));
            try expectEqual(11, r.exponent);
            try expectApproxEqAbs(-0.602816, r.significand, epsilon);
        }
        test "max" {
            const exponent = math.floatExponentMax(T) + 1;
            const significand = 1.0 - math.floatEps(T) / 2;
            const r: Frexp(T) = frexp(math.floatMax(T));
            try expectEqual(exponent, r.exponent);
            try expectEqual(significand, r.significand);
        }
        test "min" {
            const exponent = math.floatExponentMin(T) + 1;
            const r: Frexp(T) = frexp(math.floatMin(T));
            try expectEqual(exponent, r.exponent);
            try expectEqual(0.5, r.significand);
        }
        test "subnormal" {
            const normal_min_exponent = math.floatExponentMin(T) + 1;
            const exponent = normal_min_exponent - math.floatFractionalBits(T);
            const r: Frexp(T) = frexp(math.floatTrueMin(T));
            try expectEqual(exponent, r.exponent);
            try expectEqual(0.5, r.significand);
        }
        test "zero" {
            var r: Frexp(T) = undefined;

            r = frexp(@as(T, 0.0));
            try expectEqual(0, r.exponent);
            try expect(math.isPositiveZero(r.significand));

            r = frexp(@as(T, -0.0));
            try expectEqual(0, r.exponent);
            try expect(math.isNegativeZero(r.significand));
        }
        test "inf" {
            var r: Frexp(T) = undefined;

            r = frexp(math.inf(T));
            try expectEqual(0, r.exponent);
            try expect(math.isPositiveInf(r.significand));

            r = frexp(-math.inf(T));
            try expectEqual(0, r.exponent);
            try expect(math.isNegativeInf(r.significand));
        }
        test "nan" {
            const r: Frexp(T) = frexp(math.nan(T));
            try expect(math.isNan(r.significand));
        }
    };
}

// Generate tests for each floating point type
comptime {
    for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        _ = FrexpTests(T);
    }
}

test frexp {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        const max_exponent = math.floatExponentMax(T) + 1;
        const min_exponent = math.floatExponentMin(T) + 1;
        const truemin_exponent = min_exponent - math.floatFractionalBits(T);

        var result: Frexp(T) = undefined;
        comptime var x: T = undefined;

        // basic usage
        // value -> {significand, exponent},
        // value == significand * (2 ^ exponent)
        x = 1234.5678;
        result = frexp(x);
        try expectEqual(11, result.exponent);
        try expectApproxEqAbs(0.602816, result.significand, 1e-6);
        try expectEqual(x, math.ldexp(result.significand, result.exponent));

        // float maximum
        x = math.floatMax(T);
        result = frexp(x);
        try expectEqual(max_exponent, result.exponent);
        try expectEqual(1.0 - math.floatEps(T) / 2, result.significand);
        try expectEqual(x, math.ldexp(result.significand, result.exponent));

        // float minimum
        x = math.floatMin(T);
        result = frexp(x);
        try expectEqual(min_exponent, result.exponent);
        try expectEqual(0.5, result.significand);
        try expectEqual(x, math.ldexp(result.significand, result.exponent));

        // float true minimum
        // subnormal -> {normal, exponent}
        x = math.floatTrueMin(T);
        result = frexp(x);
        try expectEqual(truemin_exponent, result.exponent);
        try expectEqual(0.5, result.significand);
        try expectEqual(x, math.ldexp(result.significand, result.exponent));

        // infinity -> {infinity, zero} (+)
        result = frexp(math.inf(T));
        try expectEqual(0, result.exponent);
        try expect(math.isPositiveInf(result.significand));

        // infinity -> {infinity, zero} (-)
        result = frexp(-math.inf(T));
        try expectEqual(0, result.exponent);
        try expect(math.isNegativeInf(result.significand));

        // zero -> {zero, zero} (+)
        result = frexp(@as(T, 0.0));
        try expectEqual(0, result.exponent);
        try expect(math.isPositiveZero(result.significand));

        // zero -> {zero, zero} (-)
        result = frexp(@as(T, -0.0));
        try expectEqual(0, result.exponent);
        try expect(math.isNegativeZero(result.significand));

        // nan -> {nan, undefined}
        result = frexp(math.nan(T));
        try expect(math.isNan(result.significand));
    }
}



---
File: /std/math/gamma.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/tgamma.c

const builtin = @import("builtin");
const std = @import("../std.zig");

/// Returns the gamma function of x,
/// gamma(x) = factorial(x - 1) for integer x.
///
/// Special Cases:
///  - gamma(+-nan) = nan
///  - gamma(-inf)  = nan
///  - gamma(n)     = nan for negative integers
///  - gamma(-0.0)  = -inf
///  - gamma(+0.0)  = +inf
///  - gamma(+inf)  = +inf
pub fn gamma(comptime T: type, x: T) T {
    if (T != f32 and T != f64) {
        @compileError("gamma not implemented for " ++ @typeName(T));
    }
    // common integer case first
    if (x == @trunc(x)) {
        // gamma(-inf) = nan
        // gamma(n)    = nan for negative integers
        if (x < 0) {
            return std.math.nan(T);
        }
        // gamma(-0.0) = -inf
        // gamma(+0.0) = +inf
        if (x == 0) {
            return 1 / x;
        }
        if (x < integer_result_table.len) {
            const i = @as(u8, @intFromFloat(x));
            return @floatCast(integer_result_table[i]);
        }
    }
    // below this, result underflows, but has a sign
    // negative for (-1,  0)
    // positive for (-2, -1)
    // negative for (-3, -2)
    // ...
    const lower_bound = if (T == f64) -184 else -42;
    if (x < lower_bound) {
        return if (@mod(x, 2) > 1) -0.0 else 0.0;
    }
    // above this, result overflows
    // gamma(+inf) = +inf
    const upper_bound = if (T == f64) 172 else 36;
    if (x > upper_bound) {
        return std.math.inf(T);
    }

    const abs = @abs(x);
    // perfect precision here
    if (abs < 0x1p-54) {
        return 1 / x;
    }

    const base = abs + lanczos_minus_half;
    const exponent = abs - 0.5;
    // error of y for correction, see
    // https://github.com/python/cpython/blob/5dc79e3d7f26a6a871a89ce3efc9f1bcee7bb447/Modules/mathmodule.c#L286-L324
    const e = if (abs > lanczos_minus_half)
        base - abs - lanczos_minus_half
    else
        base - lanczos_minus_half - abs;
    const correction = lanczos * e / base;
    const initial = series(T, abs) * @exp(-base);

    // use reflection formula for negatives
    if (x < 0) {
        const reflected = -std.math.pi / (abs * sinpi(T, abs) * initial);
        const corrected = reflected - reflected * correction;
        const half_pow = std.math.pow(T, base, -0.5 * exponent);
        return corrected * half_pow * half_pow;
    } else {
        const corrected = initial + initial * correction;
        const half_pow = std.math.pow(T, base, 0.5 * exponent);
        return corrected * half_pow * half_pow;
    }
}

/// Returns the natural logarithm of the absolute value of the gamma function.
///
/// Special Cases:
///  - lgamma(+-nan) = nan
///  - lgamma(+-inf) = +inf
///  - lgamma(n)     = +inf for negative integers
///  - lgamma(+-0.0) = +inf
///  - lgamma(1)     = +0.0
///  - lgamma(2)     = +0.0
pub fn lgamma(comptime T: type, x: T) T {
    if (T != f32 and T != f64) {
        @compileError("gamma not implemented for " ++ @typeName(T));
    }
    // common integer case first
    if (x == @trunc(x)) {
        // lgamma(-inf)  = +inf
        // lgamma(n)     = +inf for negative integers
        // lgamma(+-0.0) = +inf
        if (x <= 0) {
            return std.math.inf(T);
        }
        // lgamma(1) = +0.0
        // lgamma(2) = +0.0
        if (x < integer_result_table.len) {
            const i = @as(u8, @intFromFloat(x));
            return @log(@as(T, @floatCast(integer_result_table[i])));
        }
        // lgamma(+inf) = +inf
        if (std.math.isPositiveInf(x)) {
            return x;
        }
    }

    const abs = @abs(x);
    // perfect precision here
    if (abs < 0x1p-54) {
        return -@log(abs);
    }
    // obvious approach when overflow is not a problem
    const upper_bound = if (T == f64) 128 else 26;
    if (abs < upper_bound) {
        return @log(@abs(gamma(T, x)));
    }

    const log_base = @log(abs + lanczos_minus_half) - 1;
    const exponent = abs - 0.5;
    const log_series = @log(series(T, abs));
    const initial = exponent * log_base + log_series - lanczos;

    // use reflection formula for negatives
    if (x < 0) {
        const reflected = std.math.pi / (abs * sinpi(T, abs));
        return @log(@abs(reflected)) - initial;
    }
    return initial;
}

// table of factorials for integer early return
// stops at 22 because 23 isn't representable with full precision on f64
const integer_result_table = [_]f64{
    std.math.inf(f64), // gamma(+0.0)
    1, // gamma(1)
    1, // ...
    2,
    6,
    24,
    120,
    720,
    5040,
    40320,
    362880,
    3628800,
    39916800,
    479001600,
    6227020800,
    87178291200,
    1307674368000,
    20922789888000,
    355687428096000,
    6402373705728000,
    121645100408832000,
    2432902008176640000,
    51090942171709440000, // gamma(22)
};

// "g" constant, arbitrary
const lanczos = 6.024680040776729583740234375;
const lanczos_minus_half = lanczos - 0.5;

fn series(comptime T: type, abs: T) T {
    const numerator = [_]T{
        23531376880.410759688572007674451636754734846804940,
        42919803642.649098768957899047001988850926355848959,
        35711959237.355668049440185451547166705960488635843,
        17921034426.037209699919755754458931112671403265390,
        6039542586.3520280050642916443072979210699388420708,
        1439720407.3117216736632230727949123939715485786772,
        248874557.86205415651146038641322942321632125127801,
        31426415.585400194380614231628318205362874684987640,
        2876370.6289353724412254090516208496135991145378768,
        186056.26539522349504029498971604569928220784236328,
        8071.6720023658162106380029022722506138218516325024,
        210.82427775157934587250973392071336271166969580291,
        2.5066282746310002701649081771338373386264310793408,
    };
    const denominator = [_]T{
        0.0,
        39916800.0,
        120543840.0,
        150917976.0,
        105258076.0,
        45995730.0,
        13339535.0,
        2637558.0,
        357423.0,
        32670.0,
        1925.0,
        66.0,
        1.0,
    };
    var num: T = 0;
    var den: T = 0;
    // split to avoid overflow
    if (abs < 8) {
        // big abs would overflow here
        for (0..numerator.len) |i| {
            num = num * abs + numerator[numerator.len - 1 - i];
            den = den * abs + denominator[numerator.len - 1 - i];
        }
    } else {
        // small abs would overflow here
        for (0..numerator.len) |i| {
            num = num / abs + numerator[i];
            den = den / abs + denominator[i];
        }
    }
    return num / den;
}

// precise sin(pi * x)
// but not for integer x or |x| < 2^-54, we handle those already
fn sinpi(comptime T: type, x: T) T {
    const xmod2 = @mod(x, 2); // [0, 2]
    const n = (@as(u8, @intFromFloat(4 * xmod2)) + 1) / 2; // {0, 1, 2, 3, 4}
    const y = xmod2 - 0.5 * @as(T, @floatFromInt(n)); // [-0.25, 0.25]
    return switch (n) {
        0, 4 => @sin(std.math.pi * y),
        1 => @cos(std.math.pi * y),
        2 => -@sin(std.math.pi * y),
        3 => -@cos(std.math.pi * y),
        else => unreachable,
    };
}

const expect = std.testing.expect;
const expectEqual = std.testing.expectEqual;
const expectApproxEqRel = std.testing.expectApproxEqRel;

test gamma {
    inline for (&.{ f32, f64 }) |T| {
        const eps = @sqrt(std.math.floatEps(T));
        try expectApproxEqRel(@as(T, 120.0), gamma(T, 6), eps);
        try expectApproxEqRel(@as(T, 362880.0), gamma(T, 10), eps);
        try expectApproxEqRel(@as(T, 6402373705728000.0), gamma(T, 19), eps);

        try expectApproxEqRel(@as(T, 332.7590766955334570), gamma(T, 0.003), eps);
        try expectApproxEqRel(@as(T, 1.377260301981044573), gamma(T, 0.654), eps);
        try expectApproxEqRel(@as(T, 1.025393882573518478), gamma(T, 0.959), eps);

        try expectApproxEqRel(@as(T, 7.361898021467681690), gamma(T, 4.16), eps);
        try expectApproxEqRel(@as(T, 198337.2940287730753), gamma(T, 9.73), eps);
        try expectApproxEqRel(@as(T, 113718145797241.1666), gamma(T, 17.6), eps);

        try expectApproxEqRel(@as(T, -1.13860211111081424930673), gamma(T, -2.80), eps);
        try expectApproxEqRel(@as(T, 0.00018573407931875070158), gamma(T, -7.74), eps);
        try expectApproxEqRel(@as(T, -0.00000001647990903942825), gamma(T, -12.1), eps);
    }
}

test "gamma.special" {
    if (builtin.cpu.arch.isArm() and builtin.target.abi.float() == .soft) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/21234

    inline for (&.{ f32, f64 }) |T| {
        try expect(std.math.isNan(gamma(T, -std.math.nan(T))));
        try expect(std.math.isNan(gamma(T, std.math.nan(T))));
        try expect(std.math.isNan(gamma(T, -std.math.inf(T))));

        try expect(std.math.isNan(gamma(T, -4)));
        try expect(std.math.isNan(gamma(T, -11)));
        try expect(std.math.isNan(gamma(T, -78)));

        try expectEqual(-std.math.inf(T), gamma(T, -0.0));
        try expectEqual(std.math.inf(T), gamma(T, 0.0));

        try expect(std.math.isNegativeZero(gamma(T, -200.5)));
        try expect(std.math.isPositiveZero(gamma(T, -201.5)));
        try expect(std.math.isNegativeZero(gamma(T, -202.5)));

        try expectEqual(std.math.inf(T), gamma(T, 200));
        try expectEqual(std.math.inf(T), gamma(T, 201));
        try expectEqual(std.math.inf(T), gamma(T, 202));

        try expectEqual(std.math.inf(T), gamma(T, std.math.inf(T)));
    }
}

test lgamma {
    inline for (&.{ f32, f64 }) |T| {
        const eps = @sqrt(std.math.floatEps(T));
        try expectApproxEqRel(@as(T, @log(24.0)), lgamma(T, 5), eps);
        try expectApproxEqRel(@as(T, @log(20922789888000.0)), lgamma(T, 17), eps);
        try expectApproxEqRel(@as(T, @log(2432902008176640000.0)), lgamma(T, 21), eps);

        try expectApproxEqRel(@as(T, 2.201821590438859327), lgamma(T, 0.105), eps);
        try expectApproxEqRel(@as(T, 1.275416975248413231), lgamma(T, 0.253), eps);
        try expectApproxEqRel(@as(T, 0.130463884049976732), lgamma(T, 0.823), eps);

        try expectApproxEqRel(@as(T, 43.24395772148497989), lgamma(T, 21.3), eps);
        try expectApproxEqRel(@as(T, 110.6908958012102623), lgamma(T, 41.1), eps);
        try expectApproxEqRel(@as(T, 215.2123266224689711), lgamma(T, 67.4), eps);

        try expectApproxEqRel(@as(T, -122.605958469563489), lgamma(T, -43.6), eps);
        try expectApproxEqRel(@as(T, -278.633885462703133), lgamma(T, -81.4), eps);
        try expectApproxEqRel(@as(T, -333.247676253238363), lgamma(T, -93.6), eps);
    }
}

test "lgamma.special" {
    inline for (&.{ f32, f64 }) |T| {
        try expect(std.math.isNan(lgamma(T, -std.math.nan(T))));
        try expect(std.math.isNan(lgamma(T, std.math.nan(T))));

        try expectEqual(std.math.inf(T), lgamma(T, -std.math.inf(T)));
        try expectEqual(std.math.inf(T), lgamma(T, std.math.inf(T)));

        try expectEqual(std.math.inf(T), lgamma(T, -5));
        try expectEqual(std.math.inf(T), lgamma(T, -8));
        try expectEqual(std.math.inf(T), lgamma(T, -15));

        try expectEqual(std.math.inf(T), lgamma(T, -0.0));
        try expectEqual(std.math.inf(T), lgamma(T, 0.0));

        try expect(std.math.isPositiveZero(lgamma(T, 1)));
        try expect(std.math.isPositiveZero(lgamma(T, 2)));
    }
}



---
File: /std/math/gcd.zig
---

//! Greatest common divisor (https://mathworld.wolfram.com/GreatestCommonDivisor.html)
const std = @import("std");

/// Returns the greatest common divisor (GCD) of two unsigned integers (`a` and `b`) which are not both zero.
/// For example, the GCD of `8` and `12` is `4`, that is, `gcd(8, 12) == 4`.
pub fn gcd(a: anytype, b: anytype) @TypeOf(a, b) {
    const N = switch (@TypeOf(a, b)) {
        // convert comptime_int to some sized int type for @ctz
        comptime_int => std.math.IntFittingRange(@min(a, b), @max(a, b)),
        else => |T| T,
    };
    if (@typeInfo(N) != .int or @typeInfo(N).int.signedness != .unsigned) {
        @compileError("`a` and `b` must be usigned integers");
    }

    // using an optimised form of Stein's algorithm:
    // https://en.wikipedia.org/wiki/Binary_GCD_algorithm
    std.debug.assert(a != 0 or b != 0);

    if (a == 0) return b;
    if (b == 0) return a;

    var x: N = a;
    var y: N = b;

    const xz = @ctz(x);
    const yz = @ctz(y);
    const shift = @min(xz, yz);
    x >>= @intCast(xz);
    y >>= @intCast(yz);

    var diff = y -% x;
    while (diff != 0) : (diff = y -% x) {
        // ctz is invariant under negation, we
        // put it here to ease data dependencies,
        // makes the CPU happy.
        const zeros = @ctz(diff);
        if (x > y) diff = -%diff;
        y = @min(x, y);
        x = diff >> @intCast(zeros);
    }
    return y << @intCast(shift);
}

test gcd {
    const expectEqual = std.testing.expectEqual;

    try expectEqual(gcd(0, 5), 5);
    try expectEqual(gcd(5, 0), 5);
    try expectEqual(gcd(8, 12), 4);
    try expectEqual(gcd(12, 8), 4);
    try expectEqual(gcd(33, 77), 11);
    try expectEqual(gcd(77, 33), 11);
    try expectEqual(gcd(49865, 69811), 9973);
    try expectEqual(gcd(300_000, 2_300_000), 100_000);
    try expectEqual(gcd(90000000_000_000_000_000_000, 2), 2);
    try expectEqual(gcd(@as(u80, 90000000_000_000_000_000_000), 2), 2);
    try expectEqual(gcd(300_000, @as(u32, 2_300_000)), 100_000);
}



---
File: /std/math/hypot.zig
---

const builtin = @import("builtin");
const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;
const isNan = math.isNan;
const isInf = math.isInf;
const inf = math.inf;
const nan = math.nan;
const floatEps = math.floatEps;
const floatMin = math.floatMin;
const floatMax = math.floatMax;
const floatTrueMin = math.floatTrueMin;

/// Returns sqrt(x * x + y * y), avoiding unnecessary overflow and underflow.
///
/// Special Cases:
///
/// |   x   |   y   | hypot |
/// |-------|-------|-------|
/// | +-inf |  any  | +inf  |
/// |  any  | +-inf | +inf  |
/// |  nan  |  fin  |  nan  |
/// |  fin  |  nan  |  nan  |
pub fn hypot(x: anytype, y: anytype) @TypeOf(x, y) {
    const T = @TypeOf(x, y);
    switch (@typeInfo(T)) {
        .float => {},
        .comptime_float => return @sqrt(x * x + y * y),
        else => @compileError("hypot not implemented for " ++ @typeName(T)),
    }
    const lower = @sqrt(floatMin(T));
    const upper = @sqrt(floatMax(T) / 2);
    const scale = floatTrueMin(T) * upper;
    const hypfn = if (emulateFma(T)) hypotUnfused else hypotFused;
    var major: T = x;
    var minor: T = y;
    if (isInf(major) or isInf(minor)) return inf(T);
    if (isNan(major) or isNan(minor)) return nan(T);
    if (T == f16) return @floatCast(@sqrt(@mulAdd(f32, x, x, @as(f32, y) * y)));
    if (T == f32) return @floatCast(@sqrt(@mulAdd(f64, x, x, @as(f64, y) * y)));
    major = @abs(major);
    minor = @abs(minor);
    if (minor > major) {
        const tempo = major;
        major = minor;
        minor = tempo;
    }
    if (minor == 0.0) return major;
    if (major - minor == major) return major;
    if (major > upper) return hypfn(T, major * scale, minor * scale) / scale;
    if (minor < lower) return hypfn(T, major / scale, minor / scale) * scale;
    return hypfn(T, major, minor);
}

inline fn emulateFma(comptime T: type) bool {
    // If @mulAdd lowers to the software implementation,
    // hypotUnfused should be used in place of hypotFused.
    // This takes an educated guess, but ideally we should
    // properly detect at comptime when that fallback will
    // occur.
    return (T == f128 or T == f80);
}

inline fn hypotFused(comptime F: type, x: F, y: F) F {
    const r = @sqrt(@mulAdd(F, x, x, y * y));
    const rr = r * r;
    const xx = x * x;
    const z = @mulAdd(F, -y, y, rr - xx) + @mulAdd(F, r, r, -rr) - @mulAdd(F, x, x, -xx);
    return r - z / (2 * r);
}

inline fn hypotUnfused(comptime F: type, x: F, y: F) F {
    const r = @sqrt(x * x + y * y);
    if (r <= 2 * y) { // 30deg or steeper
        const dx = r - y;
        const z = x * (2 * dx - x) + (dx - 2 * (x - y)) * dx;
        return r - z / (2 * r);
    } else { // shallower than 30 deg
        const dy = r - x;
        const z = 2 * dy * (x - 2 * y) + (4 * dy - y) * y + dy * dy;
        return r - z / (2 * r);
    }
}

const hypot_test_cases = .{
    .{ 0.0, -1.2, 1.2 },
    .{ 0.2, -0.34, 0.3944616584663203993612799816649560759946493601889826495362 },
    .{ 0.8923, 2.636890, 2.7837722899152509525110650481670176852603253522923737962880 },
    .{ 1.5, 5.25, 5.4600824169603887033229768686452745953332522619323580787836 },
    .{ 37.45, 159.835, 164.16372840856167640478217141034363907565754072954443805164 },
    .{ 89.123, 382.028905, 392.28687638576315875933966414927490685367196874260165618371 },
    .{ 123123.234375, 529428.707813, 543556.88524707706887251269205923830745438413088753096759371 },
};

test hypot {
    if (builtin.cpu.arch.isPowerPC() and builtin.mode != .Debug) return error.SkipZigTest; // https://github.com/llvm/llvm-project/issues/171869
    try expect(hypot(0.3, 0.4) == 0.5);
}

test "hypot.correct" {
    if (builtin.cpu.arch.isPowerPC() and builtin.mode != .Debug) return error.SkipZigTest; // https://github.com/llvm/llvm-project/issues/171869
    inline for (.{ f16, f32, f64, f128 }) |T| {
        inline for (hypot_test_cases) |v| {
            const a: T, const b: T, const c: T = v;
            try expect(math.approxEqRel(T, hypot(a, b), c, @sqrt(floatEps(T))));
        }
    }
}

test "hypot.precise" {
    if (builtin.cpu.arch.isPowerPC() and builtin.mode != .Debug) return error.SkipZigTest; // https://github.com/llvm/llvm-project/issues/171869
    inline for (.{ f16, f32, f64 }) |T| { // f128 seems to be 5 ulp
        inline for (hypot_test_cases) |v| {
            const a: T, const b: T, const c: T = v;
            try expect(math.approxEqRel(T, hypot(a, b), c, floatEps(T)));
        }
    }
}

test "hypot.special" {
    if (builtin.cpu.arch.isPowerPC() and builtin.mode != .Debug) return error.SkipZigTest; // https://github.com/llvm/llvm-project/issues/171869
    @setEvalBranchQuota(2000);
    inline for (.{ f16, f32, f64, f128 }) |T| {
        try expect(math.isNan(hypot(nan(T), 0.0)));
        try expect(math.isNan(hypot(0.0, nan(T))));

        try expect(math.isPositiveInf(hypot(inf(T), 0.0)));
        try expect(math.isPositiveInf(hypot(0.0, inf(T))));
        try expect(math.isPositiveInf(hypot(inf(T), nan(T))));
        try expect(math.isPositiveInf(hypot(nan(T), inf(T))));

        try expect(math.isPositiveInf(hypot(-inf(T), 0.0)));
        try expect(math.isPositiveInf(hypot(0.0, -inf(T))));
        try expect(math.isPositiveInf(hypot(-inf(T), nan(T))));
        try expect(math.isPositiveInf(hypot(nan(T), -inf(T))));
    }
}



---
File: /std/math/ilogb.zig
---

// Ported from musl, which is MIT licensed.
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/ilogbl.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/ilogbf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/ilogb.c

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;
const maxInt = std.math.maxInt;
const minInt = std.math.minInt;

/// Returns the binary exponent of x as an integer.
///
/// Special Cases:
///  - ilogb(+-inf) = maxInt(i32)
///  - ilogb(+-0)   = minInt(i32)
///  - ilogb(nan)   = minInt(i32)
pub fn ilogb(x: anytype) i32 {
    const T = @TypeOf(x);
    return ilogbX(T, x);
}

pub const fp_ilogbnan = minInt(i32);
pub const fp_ilogb0 = minInt(i32);

fn ilogbX(comptime T: type, x: T) i32 {
    const typeWidth = @typeInfo(T).float.bits;
    const significandBits = math.floatMantissaBits(T);
    const exponentBits = math.floatExponentBits(T);

    const Z = std.meta.Int(.unsigned, typeWidth);

    const signBit = (@as(Z, 1) << (significandBits + exponentBits));
    const maxExponent = ((1 << exponentBits) - 1);
    const exponentBias = (maxExponent >> 1);

    const absMask = signBit - 1;

    const u = @as(Z, @bitCast(x)) & absMask;
    const e: i32 = @intCast(u >> significandBits);

    if (e == 0) {
        if (u == 0) {
            math.raiseInvalid();
            return fp_ilogb0;
        }

        // offset sign bit, exponent bits, and integer bit (if present) + bias
        const offset = 1 + exponentBits + @as(comptime_int, @intFromBool(T == f80)) - exponentBias;
        return offset - @as(i32, @intCast(@clz(u)));
    }

    if (e == maxExponent) {
        math.raiseInvalid();
        if (u > @as(Z, @bitCast(math.inf(T)))) {
            return fp_ilogbnan; // u is a NaN
        } else return maxInt(i32);
    }

    return e - exponentBias;
}

test "type dispatch" {
    try expect(ilogb(@as(f32, 0.2)) == ilogbX(f32, 0.2));
    try expect(ilogb(@as(f64, 0.2)) == ilogbX(f64, 0.2));
}

test "16" {
    try expect(ilogbX(f16, 0.0) == fp_ilogb0);
    try expect(ilogbX(f16, 0.5) == -1);
    try expect(ilogbX(f16, 0.8923) == -1);
    try expect(ilogbX(f16, 10.0) == 3);
    try expect(ilogbX(f16, -65504) == 15);
    try expect(ilogbX(f16, 2398.23) == 11);

    try expect(ilogbX(f16, 0x1p-1) == -1);
    try expect(ilogbX(f16, 0x1p-17) == -17);
    try expect(ilogbX(f16, 0x1p-24) == -24);
}

test "32" {
    try expect(ilogbX(f32, 0.0) == fp_ilogb0);
    try expect(ilogbX(f32, 0.5) == -1);
    try expect(ilogbX(f32, 0.8923) == -1);
    try expect(ilogbX(f32, 10.0) == 3);
    try expect(ilogbX(f32, -123984) == 16);
    try expect(ilogbX(f32, 2398.23) == 11);

    try expect(ilogbX(f32, 0x1p-1) == -1);
    try expect(ilogbX(f32, 0x1p-122) == -122);
    try expect(ilogbX(f32, 0x1p-127) == -127);
}

test "64" {
    try expect(ilogbX(f64, 0.0) == fp_ilogb0);
    try expect(ilogbX(f64, 0.5) == -1);
    try expect(ilogbX(f64, 0.8923) == -1);
    try expect(ilogbX(f64, 10.0) == 3);
    try expect(ilogbX(f64, -123984) == 16);
    try expect(ilogbX(f64, 2398.23) == 11);

    try expect(ilogbX(f64, 0x1p-1) == -1);
    try expect(ilogbX(f64, 0x1p-127) == -127);
    try expect(ilogbX(f64, 0x1p-1012) == -1012);
    try expect(ilogbX(f64, 0x1p-1023) == -1023);
}

test "80" {
    try expect(ilogbX(f80, 0.0) == fp_ilogb0);
    try expect(ilogbX(f80, 0.5) == -1);
    try expect(ilogbX(f80, 0.8923) == -1);
    try expect(ilogbX(f80, 10.0) == 3);
    try expect(ilogbX(f80, -123984) == 16);
    try expect(ilogbX(f80, 2398.23) == 11);

    try expect(ilogbX(f80, 0x1p-1) == -1);
    try expect(ilogbX(f80, 0x1p-127) == -127);
    try expect(ilogbX(f80, 0x1p-1023) == -1023);
    try expect(ilogbX(f80, 0x1p-16383) == -16383);
}

test "128" {
    try expect(ilogbX(f128, 0.0) == fp_ilogb0);
    try expect(ilogbX(f128, 0.5) == -1);
    try expect(ilogbX(f128, 0.8923) == -1);
    try expect(ilogbX(f128, 10.0) == 3);
    try expect(ilogbX(f128, -123984) == 16);
    try expect(ilogbX(f128, 2398.23) == 11);

    try expect(ilogbX(f128, 0x1p-1) == -1);
    try expect(ilogbX(f128, 0x1p-127) == -127);
    try expect(ilogbX(f128, 0x1p-1023) == -1023);
    try expect(ilogbX(f128, 0x1p-16383) == -16383);
}

test "16 special" {
    try expect(ilogbX(f16, math.inf(f16)) == maxInt(i32));
    try expect(ilogbX(f16, -math.inf(f16)) == maxInt(i32));
    try expect(ilogbX(f16, 0.0) == minInt(i32));
    try expect(ilogbX(f16, math.nan(f16)) == fp_ilogbnan);
}

test "32 special" {
    try expect(ilogbX(f32, math.inf(f32)) == maxInt(i32));
    try expect(ilogbX(f32, -math.inf(f32)) == maxInt(i32));
    try expect(ilogbX(f32, 0.0) == minInt(i32));
    try expect(ilogbX(f32, math.nan(f32)) == fp_ilogbnan);
}

test "64 special" {
    try expect(ilogbX(f64, math.inf(f64)) == maxInt(i32));
    try expect(ilogbX(f64, -math.inf(f64)) == maxInt(i32));
    try expect(ilogbX(f64, 0.0) == minInt(i32));
    try expect(ilogbX(f64, math.nan(f64)) == fp_ilogbnan);
}

test "80 special" {
    try expect(ilogbX(f80, math.inf(f80)) == maxInt(i32));
    try expect(ilogbX(f80, -math.inf(f80)) == maxInt(i32));
    try expect(ilogbX(f80, 0.0) == minInt(i32));
    try expect(ilogbX(f80, math.nan(f80)) == fp_ilogbnan);
}

test "128 special" {
    try expect(ilogbX(f128, math.inf(f128)) == maxInt(i32));
    try expect(ilogbX(f128, -math.inf(f128)) == maxInt(i32));
    try expect(ilogbX(f128, 0.0) == minInt(i32));
    try expect(ilogbX(f128, math.nan(f128)) == fp_ilogbnan);
}



---
File: /std/math/isfinite.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns whether x is a finite value.
pub fn isFinite(x: anytype) bool {
    const T = @TypeOf(x);
    const TBits = std.meta.Int(.unsigned, @typeInfo(T).float.bits);
    const remove_sign = ~@as(TBits, 0) >> 1;
    return @as(TBits, @bitCast(x)) & remove_sign < @as(TBits, @bitCast(math.inf(T)));
}

test isFinite {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        // normals
        try expect(isFinite(@as(T, 1.0)));
        try expect(isFinite(-@as(T, 1.0)));

        // zero & subnormals
        try expect(isFinite(@as(T, 0.0)));
        try expect(isFinite(@as(T, -0.0)));
        try expect(isFinite(math.floatTrueMin(T)));

        // other float limits
        try expect(isFinite(math.floatMin(T)));
        try expect(isFinite(math.floatMax(T)));

        // inf & nan
        try expect(!isFinite(math.inf(T)));
        try expect(!isFinite(-math.inf(T)));
        try expect(!isFinite(math.nan(T)));
        try expect(!isFinite(-math.nan(T)));
    }
}



---
File: /std/math/isinf.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns whether x is an infinity, ignoring sign.
pub inline fn isInf(x: anytype) bool {
    const T = @TypeOf(x);
    const TBits = std.meta.Int(.unsigned, @typeInfo(T).float.bits);
    const remove_sign = ~@as(TBits, 0) >> 1;
    return @as(TBits, @bitCast(x)) & remove_sign == @as(TBits, @bitCast(math.inf(T)));
}

/// Returns whether x is an infinity with a positive sign.
pub inline fn isPositiveInf(x: anytype) bool {
    return x == math.inf(@TypeOf(x));
}

/// Returns whether x is an infinity with a negative sign.
pub inline fn isNegativeInf(x: anytype) bool {
    return x == -math.inf(@TypeOf(x));
}

test isInf {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        try expect(!isInf(@as(T, 0.0)));
        try expect(!isInf(@as(T, -0.0)));
        try expect(isInf(math.inf(T)));
        try expect(isInf(-math.inf(T)));
        try expect(!isInf(math.nan(T)));
        try expect(!isInf(-math.nan(T)));
    }
}

test isPositiveInf {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        try expect(!isPositiveInf(@as(T, 0.0)));
        try expect(!isPositiveInf(@as(T, -0.0)));
        try expect(isPositiveInf(math.inf(T)));
        try expect(!isPositiveInf(-math.inf(T)));
        try expect(!isInf(math.nan(T)));
        try expect(!isInf(-math.nan(T)));
    }
}

test isNegativeInf {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        try expect(!isNegativeInf(@as(T, 0.0)));
        try expect(!isNegativeInf(@as(T, -0.0)));
        try expect(!isNegativeInf(math.inf(T)));
        try expect(isNegativeInf(-math.inf(T)));
        try expect(!isInf(math.nan(T)));
        try expect(!isInf(-math.nan(T)));
    }
}



---
File: /std/math/isnan.zig
---

const std = @import("../std.zig");
const builtin = @import("builtin");
const math = std.math;
const meta = std.meta;
const expect = std.testing.expect;

pub fn isNan(x: anytype) bool {
    return x != x;
}

/// TODO: LLVM is known to miscompile on some architectures to quiet NaN -
///       this is tracked by https://github.com/ziglang/zig/issues/14366
pub fn isSignalNan(x: anytype) bool {
    const T = @TypeOf(x);
    const U = meta.Int(.unsigned, @bitSizeOf(T));
    const quiet_signal_bit_mask = 1 << (math.floatFractionalBits(T) - 1);
    return isNan(x) and (@as(U, @bitCast(x)) & quiet_signal_bit_mask == 0);
}

test isNan {
    inline for ([_]type{ f16, f32, f64, f80, f128, c_longdouble }) |T| {
        try expect(isNan(math.nan(T)));
        try expect(isNan(-math.nan(T)));
        try expect(isNan(math.snan(T)));
        try expect(!isNan(@as(T, 1.0)));
        try expect(!isNan(@as(T, math.inf(T))));
    }
}

test isSignalNan {
    if (builtin.zig_backend == .stage2_x86_64 and builtin.object_format == .coff and builtin.abi != .gnu) return error.SkipZigTest;

    inline for ([_]type{ f16, f32, f64, f80, f128, c_longdouble }) |T| {
        // TODO: Signalling NaN values get converted to quiet NaN values in
        //       some cases where they shouldn't such that this can fail.
        //       See https://github.com/ziglang/zig/issues/14366
        if (!builtin.cpu.arch.isArm() and
            !builtin.cpu.arch.isAARCH64() and
            builtin.cpu.arch != .hexagon and
            !builtin.cpu.arch.isMIPS32() and
            !builtin.cpu.arch.isPowerPC() and
            builtin.zig_backend != .stage2_c)
        {
            try expect(isSignalNan(math.snan(T)));
        }
        try expect(!isSignalNan(math.nan(T)));
        try expect(!isSignalNan(@as(T, 1.0)));
        try expect(!isSignalNan(math.inf(T)));
    }
}



---
File: /std/math/isnormal.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns whether x is neither zero, subnormal, infinity, or NaN.
pub fn isNormal(x: anytype) bool {
    const T = @TypeOf(x);
    const TBits = std.meta.Int(.unsigned, @typeInfo(T).float.bits);

    const increment_exp = 1 << math.floatMantissaBits(T);
    const remove_sign = ~@as(TBits, 0) >> 1;

    // We add 1 to the exponent, and if it overflows to 0 or becomes 1,
    // then it was all zeroes (subnormal) or all ones (special, inf/nan).
    // The sign bit is removed because all ones would overflow into it.
    // For f80, even though it has an explicit integer part stored,
    // the exponent effectively takes priority if mismatching.
    const value = @as(TBits, @bitCast(x)) +% increment_exp;
    return value & remove_sign >= (increment_exp << 1);
}

test isNormal {
    // TODO add `c_longdouble' when math.inf(T) supports it
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        const TBits = std.meta.Int(.unsigned, @bitSizeOf(T));

        // normals
        try expect(isNormal(@as(T, 1.0)));
        try expect(isNormal(math.floatMin(T)));
        try expect(isNormal(math.floatMax(T)));

        // subnormals
        try expect(!isNormal(@as(T, -0.0)));
        try expect(!isNormal(@as(T, 0.0)));
        try expect(!isNormal(@as(T, math.floatTrueMin(T))));

        // largest subnormal
        try expect(!isNormal(@as(T, @bitCast(~(~@as(TBits, 0) << math.floatFractionalBits(T))))));

        // non-finite numbers
        try expect(!isNormal(-math.inf(T)));
        try expect(!isNormal(math.inf(T)));
        try expect(!isNormal(math.nan(T)));

        // overflow edge-case (described in implementation, also see #10133)
        try expect(!isNormal(@as(T, @bitCast(~@as(TBits, 0)))));
    }
}



---
File: /std/math/iszero.zig
---

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns whether x is positive zero.
pub inline fn isPositiveZero(x: anytype) bool {
    const T = @TypeOf(x);
    const bit_count = @typeInfo(T).float.bits;
    const TBits = std.meta.Int(.unsigned, bit_count);
    return @as(TBits, @bitCast(x)) == @as(TBits, 0);
}

/// Returns whether x is negative zero.
pub inline fn isNegativeZero(x: anytype) bool {
    const T = @TypeOf(x);
    const bit_count = @typeInfo(T).float.bits;
    const TBits = std.meta.Int(.unsigned, bit_count);
    return @as(TBits, @bitCast(x)) == @as(TBits, 1) << (bit_count - 1);
}

test isPositiveZero {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        try expect(isPositiveZero(@as(T, 0.0)));
        try expect(!isPositiveZero(@as(T, -0.0)));
        try expect(!isPositiveZero(math.floatMin(T)));
        try expect(!isPositiveZero(math.floatMax(T)));
        try expect(!isPositiveZero(math.inf(T)));
        try expect(!isPositiveZero(-math.inf(T)));
    }
}

test isNegativeZero {
    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        try expect(isNegativeZero(@as(T, -0.0)));
        try expect(!isNegativeZero(@as(T, 0.0)));
        try expect(!isNegativeZero(math.floatMin(T)));
        try expect(!isNegativeZero(math.floatMax(T)));
        try expect(!isNegativeZero(math.inf(T)));
        try expect(!isNegativeZero(-math.inf(T)));
    }
}



---
File: /std/math/lcm.zig
---

//! Least common multiple (https://mathworld.wolfram.com/LeastCommonMultiple.html)
const std = @import("std");

/// Returns the least common multiple (LCM) of two integers (`a` and `b`).
/// For example, the LCM of `8` and `12` is `24`, that is, `lcm(8, 12) == 24`.
/// If any of the arguments is zero, then the returned value is 0.
pub fn lcm(a: anytype, b: anytype) @TypeOf(a, b) {
    // Behavior from C++ and Python
    // If an argument is zero, then the returned value is 0.
    if (a == 0 or b == 0) return 0;
    return @abs(b) * (@abs(a) / std.math.gcd(@abs(a), @abs(b)));
}

test lcm {
    const expectEqual = std.testing.expectEqual;

    try expectEqual(lcm(0, 0), 0);
    try expectEqual(lcm(1, 0), 0);
    try expectEqual(lcm(-1, 0), 0);
    try expectEqual(lcm(0, 1), 0);
    try expectEqual(lcm(0, -1), 0);
    try expectEqual(lcm(7, 1), 7);
    try expectEqual(lcm(7, -1), 7);
    try expectEqual(lcm(8, 12), 24);
    try expectEqual(lcm(-23, 15), 345);
    try expectEqual(lcm(120, 84), 840);
    try expectEqual(lcm(84, -120), 840);
    try expectEqual(lcm(1216342683557601535506311712, 436522681849110124616458784), 16592536571065866494401400422922201534178938447014944);
}



---
File: /std/math/ldexp.zig
---

const std = @import("std");
const math = std.math;
const Log2Int = std.math.Log2Int;
const assert = std.debug.assert;
const expect = std.testing.expect;

/// Returns x * 2^n.
pub fn ldexp(x: anytype, n: i32) @TypeOf(x) {
    const T = @TypeOf(x);
    const TBits = std.meta.Int(.unsigned, @typeInfo(T).float.bits);

    const exponent_bits = math.floatExponentBits(T);
    const mantissa_bits = math.floatMantissaBits(T);
    const fractional_bits = math.floatFractionalBits(T);

    const max_biased_exponent = 2 * math.floatExponentMax(T);
    const mantissa_mask = @as(TBits, (1 << mantissa_bits) - 1);

    const repr = @as(TBits, @bitCast(x));
    const sign_bit = repr & (1 << (exponent_bits + mantissa_bits));

    if (math.isNan(x) or !math.isFinite(x))
        return x;

    var exponent: i32 = @as(i32, @intCast((repr << 1) >> (mantissa_bits + 1)));
    if (exponent == 0)
        exponent += (@as(i32, exponent_bits) + @intFromBool(T == f80)) - @clz(repr << 1);

    if (n >= 0) {
        if (n > max_biased_exponent - exponent) {
            // Overflow. Return +/- inf
            return @as(T, @bitCast(@as(TBits, @bitCast(math.inf(T))) | sign_bit));
        } else if (exponent + n <= 0) {
            // Result is subnormal
            return @as(T, @bitCast((repr << @as(Log2Int(TBits), @intCast(n))) | sign_bit));
        } else if (exponent <= 0) {
            // Result is normal, but needs shifting
            var result = @as(TBits, @intCast(n + exponent)) << mantissa_bits;
            result |= (repr << @as(Log2Int(TBits), @intCast(1 - exponent))) & mantissa_mask;
            return @as(T, @bitCast(result | sign_bit));
        }

        // Result needs no shifting
        return @as(T, @bitCast(repr + (@as(TBits, @intCast(n)) << mantissa_bits)));
    } else {
        if (n <= -exponent) {
            if (n < -(mantissa_bits + exponent))
                return @as(T, @bitCast(sign_bit)); // Severe underflow. Return +/- 0

            // Result underflowed, we need to shift and round
            const shift = @as(Log2Int(TBits), @intCast(@min(-n, -(exponent + n) + 1)));
            const exact_tie: bool = @ctz(repr) == shift - 1;
            var result = repr & mantissa_mask;

            if (T != f80) // Include integer bit
                result |= @as(TBits, @intFromBool(exponent > 0)) << fractional_bits;
            result = @as(TBits, @intCast((result >> (shift - 1))));

            // Round result, including round-to-even for exact ties
            result = ((result + 1) >> 1) & ~@as(TBits, @intFromBool(exact_tie));
            return @as(T, @bitCast(result | sign_bit));
        }

        // Result is exact, and needs no shifting
        return @as(T, @bitCast(repr - (@as(TBits, @intCast(-n)) << mantissa_bits)));
    }
}

test ldexp {
    // subnormals
    try expect(ldexp(@as(f16, 0x1.1FFp14), -14 - 9 - 15) == math.floatTrueMin(f16));
    try expect(ldexp(@as(f32, 0x1.3FFFFFp-1), -126 - 22) == math.floatTrueMin(f32));
    try expect(ldexp(@as(f64, 0x1.7FFFFFFFFFFFFp-1), -1022 - 51) == math.floatTrueMin(f64));
    try expect(ldexp(@as(f80, 0x1.7FFFFFFFFFFFFFFEp-1), -16382 - 62) == math.floatTrueMin(f80));
    try expect(ldexp(@as(f128, 0x1.7FFFFFFFFFFFFFFFFFFFFFFFFFFFp-1), -16382 - 111) == math.floatTrueMin(f128));

    try expect(ldexp(math.floatMax(f32), -128 - 149) > 0.0);
    try expect(ldexp(math.floatMax(f32), -128 - 149 - 1) == 0.0);

    @setEvalBranchQuota(10_000);

    inline for ([_]type{ f16, f32, f64, f80, f128 }) |T| {
        const fractional_bits = math.floatFractionalBits(T);

        const min_exponent = math.floatExponentMin(T);
        const max_exponent = math.floatExponentMax(T);
        const exponent_bias = max_exponent;

        // basic usage
        try expect(ldexp(@as(T, 1.5), 4) == 24.0);

        // normals -> subnormals
        try expect(math.isNormal(ldexp(@as(T, 1.0), min_exponent)));
        try expect(!math.isNormal(ldexp(@as(T, 1.0), min_exponent - 1)));

        // normals -> zero
        try expect(ldexp(@as(T, 1.0), min_exponent - fractional_bits) > 0.0);
        try expect(ldexp(@as(T, 1.0), min_exponent - fractional_bits - 1) == 0.0);

        // subnormals -> zero
        try expect(ldexp(math.floatTrueMin(T), 0) > 0.0);
        try expect(ldexp(math.floatTrueMin(T), -1) == 0.0);

        // Multiplications might flush the denormals to zero, esp. at
        // runtime, so we manually construct the constants here instead.
        const Z = std.meta.Int(.unsigned, @bitSizeOf(T));
        const EightTimesTrueMin = @as(T, @bitCast(@as(Z, 8)));
        const TwoTimesTrueMin = @as(T, @bitCast(@as(Z, 2)));

        // subnormals -> subnormals
        try expect(ldexp(math.floatTrueMin(T), 3) == EightTimesTrueMin);
        try expect(ldexp(EightTimesTrueMin, -2) == TwoTimesTrueMin);
        try expect(ldexp(EightTimesTrueMin, -3) == math.floatTrueMin(T));

        // subnormals -> normals (+)
        try expect(ldexp(math.floatTrueMin(T), fractional_bits) == math.floatMin(T));
        try expect(ldexp(math.floatTrueMin(T), fractional_bits - 1) == math.floatMin(T) * 0.5);

        // subnormals -> normals (-)
        try expect(ldexp(-math.floatTrueMin(T), fractional_bits) == -math.floatMin(T));
        try expect(ldexp(-math.floatTrueMin(T), fractional_bits - 1) == -math.floatMin(T) * 0.5);

        // subnormals -> float limits (+inf)
        try expect(math.isFinite(ldexp(math.floatTrueMin(T), max_exponent + exponent_bias + fractional_bits - 1)));
        try expect(ldexp(math.floatTrueMin(T), max_exponent + exponent_bias + fractional_bits) == math.inf(T));

        // subnormals -> float limits (-inf)
        try expect(math.isFinite(ldexp(-math.floatTrueMin(T), max_exponent + exponent_bias + fractional_bits - 1)));
        try expect(ldexp(-math.floatTrueMin(T), max_exponent + exponent_bias + fractional_bits) == -math.inf(T));

        // infinity -> infinity
        try expect(ldexp(math.inf(T), math.maxInt(i32)) == math.inf(T));
        try expect(ldexp(math.inf(T), math.minInt(i32)) == math.inf(T));
        try expect(ldexp(math.inf(T), max_exponent) == math.inf(T));
        try expect(ldexp(math.inf(T), min_exponent) == math.inf(T));
        try expect(ldexp(-math.inf(T), math.maxInt(i32)) == -math.inf(T));
        try expect(ldexp(-math.inf(T), math.minInt(i32)) == -math.inf(T));

        // extremely large n
        try expect(ldexp(math.floatMax(T), math.maxInt(i32)) == math.inf(T));
        try expect(ldexp(math.floatMax(T), -math.maxInt(i32)) == 0.0);
        try expect(ldexp(math.floatMax(T), math.minInt(i32)) == 0.0);
        try expect(ldexp(math.floatTrueMin(T), math.maxInt(i32)) == math.inf(T));
        try expect(ldexp(math.floatTrueMin(T), -math.maxInt(i32)) == 0.0);
        try expect(ldexp(math.floatTrueMin(T), math.minInt(i32)) == 0.0);
    }
}



---
File: /std/math/log_int.zig
---

const std = @import("../std.zig");
const math = std.math;
const testing = std.testing;
const assert = std.debug.assert;
const Log2Int = math.Log2Int;

/// Returns the logarithm of `x` for the provided `base`, rounding down to the nearest integer.
/// Asserts that `base > 1` and `x > 0`.
pub fn log_int(comptime T: type, base: T, x: T) Log2Int(T) {
    const valid = switch (@typeInfo(T)) {
        .comptime_int => true,
        .int => |IntType| IntType.signedness == .unsigned,
        else => false,
    };
    if (!valid) @compileError("log_int requires an unsigned integer, found " ++ @typeName(T));

    assert(base > 1 and x > 0);
    if (base == 2) return math.log2_int(T, x);

    // Let's denote by [y] the integer part of y.

    // Throughout the iteration the following invariant is preserved:
    //     power = base ^ exponent

    // Safety and termination.
    //
    // We never overflow inside the loop because when we enter the loop we have
    //     power <= [maxInt(T) / base]
    // therefore
    //     power * base <= maxInt(T)
    // is a valid multiplication for type `T` and
    //     exponent + 1 <= log(base, maxInt(T)) <= log2(maxInt(T)) <= maxInt(Log2Int(T))
    // is a valid addition for type `Log2Int(T)`.
    //
    // This implies also termination because power is strictly increasing,
    // hence it must eventually surpass [x / base] < maxInt(T) and we then exit the loop.

    var exponent: Log2Int(T) = 0;
    var power: T = 1;
    while (power <= x / base) {
        power *= base;
        exponent += 1;
    }

    // If we never entered the loop we must have
    //     [x / base] < 1
    // hence
    //     x <= [x / base] * base < base
    // thus the result is 0. We can then return exponent, which is still 0.
    //
    // Otherwise, if we entered the loop at least once,
    // when we exit the loop we have that power is exactly divisible by base and
    //     power / base <= [x / base] < power
    // hence
    //     power <= [x / base] * base <= x < power * base
    // This means that
    //     base^exponent <= x < base^(exponent+1)
    // hence the result is exponent.

    return exponent;
}

test "log_int" {
    @setEvalBranchQuota(2000);
    // Test all unsigned integers with 2, 3, ..., 64 bits.
    // We cannot test 0 or 1 bits since base must be > 1.
    inline for (2..64 + 1) |bits| {
        const T = @Int(.unsigned, @intCast(bits));

        // for base = 2, 3, ..., min(maxInt(T),1024)
        var base: T = 1;
        while (base < math.maxInt(T) and base <= 1024) {
            base += 1;

            // test that `log_int(T, base, 1) == 0`
            try testing.expectEqual(@as(Log2Int(T), 0), log_int(T, base, 1));

            // For powers `pow = base^exp > 1` that fit inside T,
            // test that
```
