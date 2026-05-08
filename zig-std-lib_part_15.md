```
)
        const u2u2 = u2_.sq(); // (1+s^2)^2
        const v = Fe.edwards25519d.mul(u1u1).neg().sub(u2u2); // -(d*u1^2)-u2^2
        const v_u2u2 = v.mul(u2u2); // v*u2^2

        const inv_sqrt = sqrtRatioM1(Fe.one, v_u2u2);
        var x = inv_sqrt.root.mul(u2_);
        const y = inv_sqrt.root.mul(x).mul(v).mul(u1_);
        x = x.mul(s_);
        x = x.add(x).abs();
        const t = x.mul(y);
        if ((1 - inv_sqrt.ratio_is_square) | @intFromBool(t.isNegative()) | @intFromBool(y.isZero()) != 0) {
            return error.InvalidEncoding;
        }
        const p: Curve = .{
            .x = x,
            .y = y,
            .z = Fe.one,
            .t = t,
        };
        return Ristretto255{ .p = p };
    }

    /// Encode to a Ristretto255 representative.
    pub fn toBytes(e: Ristretto255) [encoded_length]u8 {
        const p = &e.p;
        var u1_ = p.z.add(p.y); // Z+Y
        const zmy = p.z.sub(p.y); // Z-Y
        u1_ = u1_.mul(zmy); // (Z+Y)*(Z-Y)
        const u2_ = p.x.mul(p.y); // X*Y
        const u1_u2u2 = u2_.sq().mul(u1_); // u1*u2^2
        const inv_sqrt = sqrtRatioM1(Fe.one, u1_u2u2);
        const den1 = inv_sqrt.root.mul(u1_);
        const den2 = inv_sqrt.root.mul(u2_);
        const z_inv = den1.mul(den2).mul(p.t); // den1*den2*T
        const ix = p.x.mul(Fe.sqrtm1); // X*sqrt(-1)
        const iy = p.y.mul(Fe.sqrtm1); // Y*sqrt(-1)
        const eden = den1.mul(Fe.edwards25519sqrtamd); // den1/sqrt(a-d)
        const t_z_inv = p.t.mul(z_inv); // T*z_inv

        const rotate = @intFromBool(t_z_inv.isNegative());
        var x = p.x;
        var y = p.y;
        var den_inv = den2;
        x.cMov(iy, rotate);
        y.cMov(ix, rotate);
        den_inv.cMov(eden, rotate);

        const x_z_inv = x.mul(z_inv);
        const yneg = y.neg();
        y.cMov(yneg, @intFromBool(x_z_inv.isNegative()));

        return p.z.sub(y).mul(den_inv).abs().toBytes();
    }

    fn elligator(t: Fe) Curve {
        const r = t.sq().mul(Fe.sqrtm1); // sqrt(-1)*t^2
        const u = r.add(Fe.one).mul(Fe.edwards25519eonemsqd); // (r+1)*(1-d^2)
        var c = comptime Fe.one.neg(); // -1
        const v = c.sub(r.mul(Fe.edwards25519d)).mul(r.add(Fe.edwards25519d)); // (c-r*d)*(r+d)
        const ratio_sqrt = sqrtRatioM1(u, v);
        const wasnt_square = 1 - ratio_sqrt.ratio_is_square;
        var s = ratio_sqrt.root;
        const s_prime = s.mul(t).abs().neg(); // -|s*t|
        s.cMov(s_prime, wasnt_square);
        c.cMov(r, wasnt_square);

        const n = r.sub(Fe.one).mul(c).mul(Fe.edwards25519sqdmone).sub(v); // c*(r-1)*(d-1)^2-v
        const w0 = s.add(s).mul(v); // 2s*v
        const w1 = n.mul(Fe.edwards25519sqrtadm1); // n*sqrt(ad-1)
        const ss = s.sq(); // s^2
        const w2 = Fe.one.sub(ss); // 1-s^2
        const w3 = Fe.one.add(ss); // 1+s^2

        return .{ .x = w0.mul(w3), .y = w2.mul(w1), .z = w1.mul(w3), .t = w0.mul(w2) };
    }

    /// Map a 64-bit string into a Ristretto255 group element
    pub fn fromUniform(h: [64]u8) Ristretto255 {
        const p0 = elligator(Fe.fromBytes(h[0..32].*));
        const p1 = elligator(Fe.fromBytes(h[32..64].*));
        return Ristretto255{ .p = p0.add(p1) };
    }

    /// Double a Ristretto255 element.
    pub fn dbl(p: Ristretto255) Ristretto255 {
        return .{ .p = p.p.dbl() };
    }

    /// Add two Ristretto255 elements.
    pub fn add(p: Ristretto255, q: Ristretto255) Ristretto255 {
        return .{ .p = p.p.add(q.p) };
    }

    /// Subtract two Ristretto255 elements.
    pub fn sub(p: Ristretto255, q: Ristretto255) Ristretto255 {
        return .{ .p = p.p.sub(q.p) };
    }

    /// Multiply a Ristretto255 element with a scalar.
    /// Return error.WeakPublicKey if the resulting element is
    /// the identity element.
    pub fn mul(p: Ristretto255, s: [encoded_length]u8) (IdentityElementError || WeakPublicKeyError)!Ristretto255 {
        return .{ .p = try p.p.mul(s) };
    }

    /// Return true if two Ristretto255 elements are equivalent
    pub fn equivalent(p: Ristretto255, q: Ristretto255) bool {
        const p_ = &p.p;
        const q_ = &q.p;
        const a = p_.x.mul(q_.y).equivalent(p_.y.mul(q_.x));
        const b = p_.y.mul(q_.y).equivalent(p_.x.mul(q_.x));
        return (@intFromBool(a) | @intFromBool(b)) != 0;
    }
};

test "ristretto255" {
    const p = Ristretto255.basePoint;
    var buf: [256]u8 = undefined;
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&p.toBytes()}), "E2F2AE0A6ABC4E71A884A961C500515F58E30B6AA582DD8DB6A65945E08D2D76");

    var r: [Ristretto255.encoded_length]u8 = undefined;
    _ = try fmt.hexToBytes(r[0..], "6a493210f7499cd17fecb510ae0cea23a110e8d5b901f8acadd3095c73a3b919");
    var q = try Ristretto255.fromBytes(r);
    q = q.dbl().add(p);
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&q.toBytes()}), "E882B131016B52C1D3337080187CF768423EFCCBB517BB495AB812C4160FF44E");

    const s = [_]u8{15} ++ [_]u8{0} ** 31;
    const w = try p.mul(s);
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&w.toBytes()}), "E0C418F7C8D9C4CDD7395B93EA124F3AD99021BB681DFC3302A9D99A2E53E64E");

    try std.testing.expect(p.dbl().dbl().dbl().dbl().equivalent(w.add(p)));

    const h = [_]u8{69} ** 32 ++ [_]u8{42} ** 32;
    const ph = Ristretto255.fromUniform(h);
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&ph.toBytes()}), "DCCA54E037A4311EFBEEF413ACD21D35276518970B7A61DC88F8587B493D5E19");
}



---
File: /std/crypto/25519/scalar.zig
---

const std = @import("std");
const crypto = std.crypto;
const mem = std.mem;

const NonCanonicalError = std.crypto.errors.NonCanonicalError;

/// The scalar field order.
pub const field_order: u256 = 7237005577332262213973186563042994240857116359379907606001950938285454250989;

/// A compressed scalar
pub const CompressedScalar = [32]u8;

/// Zero
pub const zero = [_]u8{0} ** 32;

const field_order_s = s: {
    var s: [32]u8 = undefined;
    mem.writeInt(u256, &s, field_order, .little);
    break :s s;
};

/// Reject a scalar whose encoding is not canonical.
pub fn rejectNonCanonical(s: CompressedScalar) NonCanonicalError!void {
    var c: u8 = 0;
    var n: u8 = 1;
    var i: usize = 31;
    while (true) : (i -= 1) {
        const xs = @as(u16, s[i]);
        const xfield_order_s = @as(u16, field_order_s[i]);
        c |= @as(u8, @intCast(((xs -% xfield_order_s) >> 8) & n));
        n &= @as(u8, @intCast(((xs ^ xfield_order_s) -% 1) >> 8));
        if (i == 0) break;
    }
    if (c == 0) {
        return error.NonCanonical;
    }
}

/// Reduce a scalar to the field size.
pub fn reduce(s: CompressedScalar) CompressedScalar {
    var scalar = Scalar.fromBytes(s);
    return scalar.toBytes();
}

/// Reduce a 64-bytes scalar to the field size.
pub fn reduce64(s: [64]u8) CompressedScalar {
    var scalar = ScalarDouble.fromBytes64(s);
    return scalar.toBytes();
}

/// Perform the X25519 "clamping" operation.
/// The scalar is then guaranteed to be a multiple of the cofactor.
pub fn clamp(s: *CompressedScalar) void {
    s[0] &= 248;
    s[31] = (s[31] & 127) | 64;
}

/// Return a*b (mod L)
pub fn mul(a: CompressedScalar, b: CompressedScalar) CompressedScalar {
    return Scalar.fromBytes(a).mul(Scalar.fromBytes(b)).toBytes();
}

/// Return a*b+c (mod L)
pub fn mulAdd(a: CompressedScalar, b: CompressedScalar, c: CompressedScalar) CompressedScalar {
    return Scalar.fromBytes(a).mul(Scalar.fromBytes(b)).add(Scalar.fromBytes(c)).toBytes();
}

/// Return a*8 (mod L)
pub fn mul8(s: CompressedScalar) CompressedScalar {
    var x = Scalar.fromBytes(s);
    x = x.add(x);
    x = x.add(x);
    x = x.add(x);
    return x.toBytes();
}

/// Return a+b (mod L)
pub fn add(a: CompressedScalar, b: CompressedScalar) CompressedScalar {
    return Scalar.fromBytes(a).add(Scalar.fromBytes(b)).toBytes();
}

/// Return -s (mod L)
pub fn neg(s: CompressedScalar) CompressedScalar {
    const fs: [64]u8 = field_order_s ++ [_]u8{0} ** 32;
    var sx: [64]u8 = undefined;
    sx[0..32].* = s;
    @memset(sx[32..], 0);
    var carry: u32 = 0;
    var i: usize = 0;
    while (i < 64) : (i += 1) {
        carry = @as(u32, fs[i]) -% sx[i] -% @as(u32, carry);
        sx[i] = @as(u8, @truncate(carry));
        carry = (carry >> 8) & 1;
    }
    return reduce64(sx);
}

/// Return (a-b) (mod L)
pub fn sub(a: CompressedScalar, b: CompressedScalar) CompressedScalar {
    return add(a, neg(b));
}

/// Return a random scalar < L
pub fn random(io: std.Io) CompressedScalar {
    return Scalar.random(io).toBytes();
}

/// A scalar in unpacked representation
pub const Scalar = struct {
    const Limbs = [5]u64;
    limbs: Limbs = undefined,

    /// Unpack a 32-byte representation of a scalar
    pub fn fromBytes(bytes: CompressedScalar) Scalar {
        var scalar = ScalarDouble.fromBytes32(bytes);
        return scalar.reduce(5);
    }

    /// Unpack a 64-byte representation of a scalar
    pub fn fromBytes64(bytes: [64]u8) Scalar {
        var scalar = ScalarDouble.fromBytes64(bytes);
        return scalar.reduce(5);
    }

    /// Pack a scalar into bytes
    pub fn toBytes(expanded: *const Scalar) CompressedScalar {
        var bytes: CompressedScalar = undefined;
        var i: usize = 0;
        while (i < 4) : (i += 1) {
            mem.writeInt(u64, bytes[i * 7 ..][0..8], expanded.limbs[i], .little);
        }
        mem.writeInt(u32, bytes[i * 7 ..][0..4], @intCast(expanded.limbs[i]), .little);
        return bytes;
    }

    /// Return true if the scalar is zero
    pub fn isZero(n: Scalar) bool {
        const limbs = n.limbs;
        return (limbs[0] | limbs[1] | limbs[2] | limbs[3] | limbs[4]) == 0;
    }

    /// Return x+y (mod L)
    pub fn add(x: Scalar, y: Scalar) Scalar {
        const carry0 = (x.limbs[0] + y.limbs[0]) >> 56;
        const t0 = (x.limbs[0] + y.limbs[0]) & 0xffffffffffffff;
        const t00 = t0;
        const c0 = carry0;
        const carry1 = (x.limbs[1] + y.limbs[1] + c0) >> 56;
        const t1 = (x.limbs[1] + y.limbs[1] + c0) & 0xffffffffffffff;
        const t10 = t1;
        const c1 = carry1;
        const carry2 = (x.limbs[2] + y.limbs[2] + c1) >> 56;
        const t2 = (x.limbs[2] + y.limbs[2] + c1) & 0xffffffffffffff;
        const t20 = t2;
        const c2 = carry2;
        const carry = (x.limbs[3] + y.limbs[3] + c2) >> 56;
        const t3 = (x.limbs[3] + y.limbs[3] + c2) & 0xffffffffffffff;
        const t30 = t3;
        const c3 = carry;
        const t4 = x.limbs[4] + y.limbs[4] + c3;

        const y01: u64 = 5175514460705773;
        const y11: u64 = 70332060721272408;
        const y21: u64 = 5342;
        const y31: u64 = 0;
        const y41: u64 = 268435456;

        const b5 = (t00 -% y01) >> 63;
        const t5 = ((b5 << 56) + t00) -% y01;
        const b0 = b5;
        const t01 = t5;
        const b6 = (t10 -% (y11 + b0)) >> 63;
        const t6 = ((b6 << 56) + t10) -% (y11 + b0);
        const b1 = b6;
        const t11 = t6;
        const b7 = (t20 -% (y21 + b1)) >> 63;
        const t7 = ((b7 << 56) + t20) -% (y21 + b1);
        const b2 = b7;
        const t21 = t7;
        const b8 = (t30 -% (y31 + b2)) >> 63;
        const t8 = ((b8 << 56) + t30) -% (y31 + b2);
        const b3 = b8;
        const t31 = t8;
        const b = (t4 -% (y41 + b3)) >> 63;
        const t = ((b << 56) + t4) -% (y41 + b3);
        const b4 = b;
        const t41 = t;

        const mask = (b4 -% 1);
        const z00 = t00 ^ (mask & (t00 ^ t01));
        const z10 = t10 ^ (mask & (t10 ^ t11));
        const z20 = t20 ^ (mask & (t20 ^ t21));
        const z30 = t30 ^ (mask & (t30 ^ t31));
        const z40 = t4 ^ (mask & (t4 ^ t41));

        return Scalar{ .limbs = .{ z00, z10, z20, z30, z40 } };
    }

    /// Return x*r (mod L)
    pub fn mul(x: Scalar, y: Scalar) Scalar {
        const xy000 = @as(u128, x.limbs[0]) * @as(u128, y.limbs[0]);
        const xy010 = @as(u128, x.limbs[0]) * @as(u128, y.limbs[1]);
        const xy020 = @as(u128, x.limbs[0]) * @as(u128, y.limbs[2]);
        const xy030 = @as(u128, x.limbs[0]) * @as(u128, y.limbs[3]);
        const xy040 = @as(u128, x.limbs[0]) * @as(u128, y.limbs[4]);
        const xy100 = @as(u128, x.limbs[1]) * @as(u128, y.limbs[0]);
        const xy110 = @as(u128, x.limbs[1]) * @as(u128, y.limbs[1]);
        const xy120 = @as(u128, x.limbs[1]) * @as(u128, y.limbs[2]);
        const xy130 = @as(u128, x.limbs[1]) * @as(u128, y.limbs[3]);
        const xy140 = @as(u128, x.limbs[1]) * @as(u128, y.limbs[4]);
        const xy200 = @as(u128, x.limbs[2]) * @as(u128, y.limbs[0]);
        const xy210 = @as(u128, x.limbs[2]) * @as(u128, y.limbs[1]);
        const xy220 = @as(u128, x.limbs[2]) * @as(u128, y.limbs[2]);
        const xy230 = @as(u128, x.limbs[2]) * @as(u128, y.limbs[3]);
        const xy240 = @as(u128, x.limbs[2]) * @as(u128, y.limbs[4]);
        const xy300 = @as(u128, x.limbs[3]) * @as(u128, y.limbs[0]);
        const xy310 = @as(u128, x.limbs[3]) * @as(u128, y.limbs[1]);
        const xy320 = @as(u128, x.limbs[3]) * @as(u128, y.limbs[2]);
        const xy330 = @as(u128, x.limbs[3]) * @as(u128, y.limbs[3]);
        const xy340 = @as(u128, x.limbs[3]) * @as(u128, y.limbs[4]);
        const xy400 = @as(u128, x.limbs[4]) * @as(u128, y.limbs[0]);
        const xy410 = @as(u128, x.limbs[4]) * @as(u128, y.limbs[1]);
        const xy420 = @as(u128, x.limbs[4]) * @as(u128, y.limbs[2]);
        const xy430 = @as(u128, x.limbs[4]) * @as(u128, y.limbs[3]);
        const xy440 = @as(u128, x.limbs[4]) * @as(u128, y.limbs[4]);
        const z00 = xy000;
        const z10 = xy010 + xy100;
        const z20 = xy020 + xy110 + xy200;
        const z30 = xy030 + xy120 + xy210 + xy300;
        const z40 = xy040 + xy130 + xy220 + xy310 + xy400;
        const z50 = xy140 + xy230 + xy320 + xy410;
        const z60 = xy240 + xy330 + xy420;
        const z70 = xy340 + xy430;
        const z80 = xy440;

        const carry0 = z00 >> 56;
        const t10 = @as(u64, @truncate(z00)) & 0xffffffffffffff;
        const c00 = carry0;
        const t00 = t10;
        const carry1 = (z10 + c00) >> 56;
        const t11 = @as(u64, @truncate((z10 + c00))) & 0xffffffffffffff;
        const c10 = carry1;
        const t12 = t11;
        const carry2 = (z20 + c10) >> 56;
        const t13 = @as(u64, @truncate((z20 + c10))) & 0xffffffffffffff;
        const c20 = carry2;
        const t20 = t13;
        const carry3 = (z30 + c20) >> 56;
        const t14 = @as(u64, @truncate((z30 + c20))) & 0xffffffffffffff;
        const c30 = carry3;
        const t30 = t14;
        const carry4 = (z40 + c30) >> 56;
        const t15 = @as(u64, @truncate((z40 + c30))) & 0xffffffffffffff;
        const c40 = carry4;
        const t40 = t15;
        const carry5 = (z50 + c40) >> 56;
        const t16 = @as(u64, @truncate((z50 + c40))) & 0xffffffffffffff;
        const c50 = carry5;
        const t50 = t16;
        const carry6 = (z60 + c50) >> 56;
        const t17 = @as(u64, @truncate((z60 + c50))) & 0xffffffffffffff;
        const c60 = carry6;
        const t60 = t17;
        const carry7 = (z70 + c60) >> 56;
        const t18 = @as(u64, @truncate((z70 + c60))) & 0xffffffffffffff;
        const c70 = carry7;
        const t70 = t18;
        const carry8 = (z80 + c70) >> 56;
        const t19 = @as(u64, @truncate((z80 + c70))) & 0xffffffffffffff;
        const c80 = carry8;
        const t80 = t19;
        const t90 = (@as(u64, @truncate(c80)));
        const r0 = t00;
        const r1 = t12;
        const r2 = t20;
        const r3 = t30;
        const r4 = t40;
        const r5 = t50;
        const r6 = t60;
        const r7 = t70;
        const r8 = t80;
        const r9 = t90;

        const m0: u64 = 5175514460705773;
        const m1: u64 = 70332060721272408;
        const m2: u64 = 5342;
        const m3: u64 = 0;
        const m4: u64 = 268435456;
        const mu0: u64 = 44162584779952923;
        const mu1: u64 = 9390964836247533;
        const mu2: u64 = 72057594036560134;
        const mu3: u64 = 72057594037927935;
        const mu4: u64 = 68719476735;

        const y_ = (r5 & 0xffffff) << 32;
        const x_ = r4 >> 24;
        const z01 = (x_ | y_);
        const y_0 = (r6 & 0xffffff) << 32;
        const x_0 = r5 >> 24;
        const z11 = (x_0 | y_0);
        const y_1 = (r7 & 0xffffff) << 32;
        const x_1 = r6 >> 24;
        const z21 = (x_1 | y_1);
        const y_2 = (r8 & 0xffffff) << 32;
        const x_2 = r7 >> 24;
        const z31 = (x_2 | y_2);
        const y_3 = (r9 & 0xffffff) << 32;
        const x_3 = r8 >> 24;
        const z41 = (x_3 | y_3);
        const q0 = z01;
        const q1 = z11;
        const q2 = z21;
        const q3 = z31;
        const q4 = z41;
        const xy001 = @as(u128, q0) * @as(u128, mu0);
        const xy011 = @as(u128, q0) * @as(u128, mu1);
        const xy021 = @as(u128, q0) * @as(u128, mu2);
        const xy031 = @as(u128, q0) * @as(u128, mu3);
        const xy041 = @as(u128, q0) * @as(u128, mu4);
        const xy101 = @as(u128, q1) * @as(u128, mu0);
        const xy111 = @as(u128, q1) * @as(u128, mu1);
        const xy121 = @as(u128, q1) * @as(u128, mu2);
        const xy131 = @as(u128, q1) * @as(u128, mu3);
        const xy14 = @as(u128, q1) * @as(u128, mu4);
        const xy201 = @as(u128, q2) * @as(u128, mu0);
        const xy211 = @as(u128, q2) * @as(u128, mu1);
        const xy221 = @as(u128, q2) * @as(u128, mu2);
        const xy23 = @as(u128, q2) * @as(u128, mu3);
        const xy24 = @as(u128, q2) * @as(u128, mu4);
        const xy301 = @as(u128, q3) * @as(u128, mu0);
        const xy311 = @as(u128, q3) * @as(u128, mu1);
        const xy32 = @as(u128, q3) * @as(u128, mu2);
        const xy33 = @as(u128, q3) * @as(u128, mu3);
        const xy34 = @as(u128, q3) * @as(u128, mu4);
        const xy401 = @as(u128, q4) * @as(u128, mu0);
        const xy41 = @as(u128, q4) * @as(u128, mu1);
        const xy42 = @as(u128, q4) * @as(u128, mu2);
        const xy43 = @as(u128, q4) * @as(u128, mu3);
        const xy44 = @as(u128, q4) * @as(u128, mu4);
        const z02 = xy001;
        const z12 = xy011 + xy101;
        const z22 = xy021 + xy111 + xy201;
        const z32 = xy031 + xy121 + xy211 + xy301;
        const z42 = xy041 + xy131 + xy221 + xy311 + xy401;
        const z5 = xy14 + xy23 + xy32 + xy41;
        const z6 = xy24 + xy33 + xy42;
        const z7 = xy34 + xy43;
        const z8 = xy44;

        const carry9 = z02 >> 56;
        const c01 = carry9;
        const carry10 = (z12 + c01) >> 56;
        const c11 = carry10;
        const carry11 = (z22 + c11) >> 56;
        const c21 = carry11;
        const carry12 = (z32 + c21) >> 56;
        const c31 = carry12;
        const carry13 = (z42 + c31) >> 56;
        const t24 = @as(u64, @truncate(z42 + c31)) & 0xffffffffffffff;
        const c41 = carry13;
        const t41 = t24;
        const carry14 = (z5 + c41) >> 56;
        const t25 = @as(u64, @truncate(z5 + c41)) & 0xffffffffffffff;
        const c5 = carry14;
        const t5 = t25;
        const carry15 = (z6 + c5) >> 56;
        const t26 = @as(u64, @truncate(z6 + c5)) & 0xffffffffffffff;
        const c6 = carry15;
        const t6 = t26;
        const carry16 = (z7 + c6) >> 56;
        const t27 = @as(u64, @truncate(z7 + c6)) & 0xffffffffffffff;
        const c7 = carry16;
        const t7 = t27;
        const carry17 = (z8 + c7) >> 56;
        const t28 = @as(u64, @truncate(z8 + c7)) & 0xffffffffffffff;
        const c8 = carry17;
        const t8 = t28;
        const t9 = @as(u64, @truncate(c8));

        const qmu4_ = t41;
        const qmu5_ = t5;
        const qmu6_ = t6;
        const qmu7_ = t7;
        const qmu8_ = t8;
        const qmu9_ = t9;
        const y_4 = (qmu5_ & 0xffffffffff) << 16;
        const x_4 = qmu4_ >> 40;
        const z03 = (x_4 | y_4);
        const y_5 = (qmu6_ & 0xffffffffff) << 16;
        const x_5 = qmu5_ >> 40;
        const z13 = (x_5 | y_5);
        const y_6 = (qmu7_ & 0xffffffffff) << 16;
        const x_6 = qmu6_ >> 40;
        const z23 = (x_6 | y_6);
        const y_7 = (qmu8_ & 0xffffffffff) << 16;
        const x_7 = qmu7_ >> 40;
        const z33 = (x_7 | y_7);
        const y_8 = (qmu9_ & 0xffffffffff) << 16;
        const x_8 = qmu8_ >> 40;
        const z43 = (x_8 | y_8);
        const qdiv0 = z03;
        const qdiv1 = z13;
        const qdiv2 = z23;
        const qdiv3 = z33;
        const qdiv4 = z43;
        const r01 = r0;
        const r11 = r1;
        const r21 = r2;
        const r31 = r3;
        const r41 = (r4 & 0xffffffffff);

        const xy00 = @as(u128, qdiv0) * @as(u128, m0);
        const xy01 = @as(u128, qdiv0) * @as(u128, m1);
        const xy02 = @as(u128, qdiv0) * @as(u128, m2);
        const xy03 = @as(u128, qdiv0) * @as(u128, m3);
        const xy04 = @as(u128, qdiv0) * @as(u128, m4);
        const xy10 = @as(u128, qdiv1) * @as(u128, m0);
        const xy11 = @as(u128, qdiv1) * @as(u128, m1);
        const xy12 = @as(u128, qdiv1) * @as(u128, m2);
        const xy13 = @as(u128, qdiv1) * @as(u128, m3);
        const xy20 = @as(u128, qdiv2) * @as(u128, m0);
        const xy21 = @as(u128, qdiv2) * @as(u128, m1);
        const xy22 = @as(u128, qdiv2) * @as(u128, m2);
        const xy30 = @as(u128, qdiv3) * @as(u128, m0);
        const xy31 = @as(u128, qdiv3) * @as(u128, m1);
        const xy40 = @as(u128, qdiv4) * @as(u128, m0);
        const carry18 = xy00 >> 56;
        const t29 = @as(u64, @truncate(xy00)) & 0xffffffffffffff;
        const c0 = carry18;
        const t01 = t29;
        const carry19 = (xy01 + xy10 + c0) >> 56;
        const t31 = @as(u64, @truncate(xy01 + xy10 + c0)) & 0xffffffffffffff;
        const c12 = carry19;
        const t110 = t31;
        const carry20 = (xy02 + xy11 + xy20 + c12) >> 56;
        const t32 = @as(u64, @truncate(xy02 + xy11 + xy20 + c12)) & 0xffffffffffffff;
        const c22 = carry20;
        const t210 = t32;
        const carry = (xy03 + xy12 + xy21 + xy30 + c22) >> 56;
        const t33 = @as(u64, @truncate(xy03 + xy12 + xy21 + xy30 + c22)) & 0xffffffffffffff;
        const c32 = carry;
        const t34 = t33;
        const t42 = @as(u64, @truncate(xy04 + xy13 + xy22 + xy31 + xy40 + c32)) & 0xffffffffff;

        const qmul0 = t01;
        const qmul1 = t110;
        const qmul2 = t210;
        const qmul3 = t34;
        const qmul4 = t42;
        const b5 = (r01 -% qmul0) >> 63;
        const t35 = ((b5 << 56) + r01) -% qmul0;
        const c1 = b5;
        const t02 = t35;
        const b6 = (r11 -% (qmul1 + c1)) >> 63;
        const t36 = ((b6 << 56) + r11) -% (qmul1 + c1);
        const c2 = b6;
        const t111 = t36;
        const b7 = (r21 -% (qmul2 + c2)) >> 63;
        const t37 = ((b7 << 56) + r21) -% (qmul2 + c2);
        const c3 = b7;
        const t211 = t37;
        const b8 = (r31 -% (qmul3 + c3)) >> 63;
        const t38 = ((b8 << 56) + r31) -% (qmul3 + c3);
        const c4 = b8;
        const t39 = t38;
        const b9 = (r41 -% (qmul4 + c4)) >> 63;
        const t43 = ((b9 << 40) + r41) -% (qmul4 + c4);
        const t44 = t43;
        const s0 = t02;
        const s1 = t111;
        const s2 = t211;
        const s3 = t39;
        const s4 = t44;

        const y01: u64 = 5175514460705773;
        const y11: u64 = 70332060721272408;
        const y21: u64 = 5342;
        const y31: u64 = 0;
        const y41: u64 = 268435456;

        const b10 = (s0 -% y01) >> 63;
        const t45 = ((b10 << 56) + s0) -% y01;
        const b0 = b10;
        const t0 = t45;
        const b11 = (s1 -% (y11 + b0)) >> 63;
        const t46 = ((b11 << 56) + s1) -% (y11 + b0);
        const b1 = b11;
        const t1 = t46;
        const b12 = (s2 -% (y21 + b1)) >> 63;
        const t47 = ((b12 << 56) + s2) -% (y21 + b1);
        const b2 = b12;
        const t2 = t47;
        const b13 = (s3 -% (y31 + b2)) >> 63;
        const t48 = ((b13 << 56) + s3) -% (y31 + b2);
        const b3 = b13;
        const t3 = t48;
        const b = (s4 -% (y41 + b3)) >> 63;
        const t = ((b << 56) + s4) -% (y41 + b3);
        const b4 = b;
        const t4 = t;
        const mask = (b4 -% @as(u64, @intCast(((1)))));
        const z04 = s0 ^ (mask & (s0 ^ t0));
        const z14 = s1 ^ (mask & (s1 ^ t1));
        const z24 = s2 ^ (mask & (s2 ^ t2));
        const z34 = s3 ^ (mask & (s3 ^ t3));
        const z44 = s4 ^ (mask & (s4 ^ t4));

        return Scalar{ .limbs = .{ z04, z14, z24, z34, z44 } };
    }

    /// Return x^2 (mod L)
    pub fn sq(x: Scalar) Scalar {
        return x.mul(x);
    }

    /// Square a scalar `n` times
    fn sqn(x: Scalar, comptime n: comptime_int) Scalar {
        var i: usize = 0;
        var t = x;
        while (i < n) : (i += 1) {
            t = t.sq();
        }
        return t;
    }

    /// Square and multiply
    fn sqn_mul(x: Scalar, comptime n: comptime_int, y: Scalar) Scalar {
        return x.sqn(n).mul(y);
    }

    /// Return the inverse of a scalar (mod L), or 0 if x=0.
    pub fn invert(x: Scalar) Scalar {
        const _10 = x.sq();
        const _11 = x.mul(_10);
        const _100 = x.mul(_11);
        const _1000 = _100.sq();
        const _1010 = _10.mul(_1000);
        const _1011 = x.mul(_1010);
        const _10000 = _1000.sq();
        const _10110 = _1011.sq();
        const _100000 = _1010.mul(_10110);
        const _100110 = _10000.mul(_10110);
        const _1000000 = _100000.sq();
        const _1010000 = _10000.mul(_1000000);
        const _1010011 = _11.mul(_1010000);
        const _1100011 = _10000.mul(_1010011);
        const _1100111 = _100.mul(_1100011);
        const _1101011 = _100.mul(_1100111);
        const _10010011 = _1000000.mul(_1010011);
        const _10010111 = _100.mul(_10010011);
        const _10111101 = _100110.mul(_10010111);
        const _11010011 = _10110.mul(_10111101);
        const _11100111 = _1010000.mul(_10010111);
        const _11101011 = _100.mul(_11100111);
        const _11110101 = _1010.mul(_11101011);
        return _1011.mul(_11110101).sqn_mul(126, _1010011).sqn_mul(9, _10).mul(_11110101)
            .sqn_mul(7, _1100111).sqn_mul(9, _11110101).sqn_mul(11, _10111101).sqn_mul(8, _11100111)
            .sqn_mul(9, _1101011).sqn_mul(6, _1011).sqn_mul(14, _10010011).sqn_mul(10, _1100011)
            .sqn_mul(9, _10010111).sqn_mul(10, _11110101).sqn_mul(8, _11010011).sqn_mul(8, _11101011);
    }

    /// Return a random scalar < L.
    pub fn random(io: std.Io) Scalar {
        var s: [64]u8 = undefined;
        while (true) {
            io.random(&s);
            const n = Scalar.fromBytes64(s);
            if (!n.isZero()) {
                return n;
            }
        }
    }
};

const ScalarDouble = struct {
    const Limbs = [10]u64;
    limbs: Limbs = undefined,

    fn fromBytes64(bytes: [64]u8) ScalarDouble {
        var limbs: Limbs = undefined;
        var i: usize = 0;
        while (i < 9) : (i += 1) {
            limbs[i] = mem.readInt(u64, bytes[i * 7 ..][0..8], .little) & 0xffffffffffffff;
        }
        limbs[i] = @as(u64, bytes[i * 7]);
        return ScalarDouble{ .limbs = limbs };
    }

    fn fromBytes32(bytes: CompressedScalar) ScalarDouble {
        var limbs: Limbs = undefined;
        var i: usize = 0;
        while (i < 4) : (i += 1) {
            limbs[i] = mem.readInt(u64, bytes[i * 7 ..][0..8], .little) & 0xffffffffffffff;
        }
        limbs[i] = @as(u64, mem.readInt(u32, bytes[i * 7 ..][0..4], .little));
        @memset(limbs[5..], 0);
        return ScalarDouble{ .limbs = limbs };
    }

    fn toBytes(expanded_double: *ScalarDouble) CompressedScalar {
        return expanded_double.reduce(10).toBytes();
    }

    /// Barrett reduction
    fn reduce(expanded: *ScalarDouble, comptime limbs_count: usize) Scalar {
        const t = expanded.limbs;
        const t0 = if (limbs_count <= 0) 0 else t[0];
        const t1 = if (limbs_count <= 1) 0 else t[1];
        const t2 = if (limbs_count <= 2) 0 else t[2];
        const t3 = if (limbs_count <= 3) 0 else t[3];
        const t4 = if (limbs_count <= 4) 0 else t[4];
        const t5 = if (limbs_count <= 5) 0 else t[5];
        const t6 = if (limbs_count <= 6) 0 else t[6];
        const t7 = if (limbs_count <= 7) 0 else t[7];
        const t8 = if (limbs_count <= 8) 0 else t[8];
        const t9 = if (limbs_count <= 9) 0 else t[9];

        const m0: u64 = 5175514460705773;
        const m1: u64 = 70332060721272408;
        const m2: u64 = 5342;
        const m3: u64 = 0;
        const m4: u64 = 268435456;
        const mu0: u64 = 44162584779952923;
        const mu1: u64 = 9390964836247533;
        const mu2: u64 = 72057594036560134;
        const mu3: u64 = 0xffffffffffffff;
        const mu4: u64 = 68719476735;

        const y_ = (t5 & 0xffffff) << 32;
        const x_ = t4 >> 24;
        const z00 = x_ | y_;
        const y_0 = (t6 & 0xffffff) << 32;
        const x_0 = t5 >> 24;
        const z10 = x_0 | y_0;
        const y_1 = (t7 & 0xffffff) << 32;
        const x_1 = t6 >> 24;
        const z20 = x_1 | y_1;
        const y_2 = (t8 & 0xffffff) << 32;
        const x_2 = t7 >> 24;
        const z30 = x_2 | y_2;
        const y_3 = (t9 & 0xffffff) << 32;
        const x_3 = t8 >> 24;
        const z40 = x_3 | y_3;
        const q0 = z00;
        const q1 = z10;
        const q2 = z20;
        const q3 = z30;
        const q4 = z40;

        const xy000 = @as(u128, q0) * @as(u128, mu0);
        const xy010 = @as(u128, q0) * @as(u128, mu1);
        const xy020 = @as(u128, q0) * @as(u128, mu2);
        const xy030 = @as(u128, q0) * @as(u128, mu3);
        const xy040 = @as(u128, q0) * @as(u128, mu4);
        const xy100 = @as(u128, q1) * @as(u128, mu0);
        const xy110 = @as(u128, q1) * @as(u128, mu1);
        const xy120 = @as(u128, q1) * @as(u128, mu2);
        const xy130 = @as(u128, q1) * @as(u128, mu3);
        const xy14 = @as(u128, q1) * @as(u128, mu4);
        const xy200 = @as(u128, q2) * @as(u128, mu0);
        const xy210 = @as(u128, q2) * @as(u128, mu1);
        const xy220 = @as(u128, q2) * @as(u128, mu2);
        const xy23 = @as(u128, q2) * @as(u128, mu3);
        const xy24 = @as(u128, q2) * @as(u128, mu4);
        const xy300 = @as(u128, q3) * @as(u128, mu0);
        const xy310 = @as(u128, q3) * @as(u128, mu1);
        const xy32 = @as(u128, q3) * @as(u128, mu2);
        const xy33 = @as(u128, q3) * @as(u128, mu3);
        const xy34 = @as(u128, q3) * @as(u128, mu4);
        const xy400 = @as(u128, q4) * @as(u128, mu0);
        const xy41 = @as(u128, q4) * @as(u128, mu1);
        const xy42 = @as(u128, q4) * @as(u128, mu2);
        const xy43 = @as(u128, q4) * @as(u128, mu3);
        const xy44 = @as(u128, q4) * @as(u128, mu4);
        const z01 = xy000;
        const z11 = xy010 + xy100;
        const z21 = xy020 + xy110 + xy200;
        const z31 = xy030 + xy120 + xy210 + xy300;
        const z41 = xy040 + xy130 + xy220 + xy310 + xy400;
        const z5 = xy14 + xy23 + xy32 + xy41;
        const z6 = xy24 + xy33 + xy42;
        const z7 = xy34 + xy43;
        const z8 = xy44;

        const carry0 = z01 >> 56;
        const c00 = carry0;
        const carry1 = (z11 + c00) >> 56;
        const c10 = carry1;
        const carry2 = (z21 + c10) >> 56;
        const c20 = carry2;
        const carry3 = (z31 + c20) >> 56;
        const c30 = carry3;
        const carry4 = (z41 + c30) >> 56;
        const t103 = @as(u64, @as(u64, @truncate(z41 + c30))) & 0xffffffffffffff;
        const c40 = carry4;
        const t410 = t103;
        const carry5 = (z5 + c40) >> 56;
        const t104 = @as(u64, @as(u64, @truncate(z5 + c40))) & 0xffffffffffffff;
        const c5 = carry5;
        const t51 = t104;
        const carry6 = (z6 + c5) >> 56;
        const t105 = @as(u64, @as(u64, @truncate(z6 + c5))) & 0xffffffffffffff;
        const c6 = carry6;
        const t61 = t105;
        const carry7 = (z7 + c6) >> 56;
        const t106 = @as(u64, @as(u64, @truncate(z7 + c6))) & 0xffffffffffffff;
        const c7 = carry7;
        const t71 = t106;
        const carry8 = (z8 + c7) >> 56;
        const t107 = @as(u64, @as(u64, @truncate(z8 + c7))) & 0xffffffffffffff;
        const c8 = carry8;
        const t81 = t107;
        const t91 = @as(u64, @as(u64, @truncate(c8)));

        const qmu4_ = t410;
        const qmu5_ = t51;
        const qmu6_ = t61;
        const qmu7_ = t71;
        const qmu8_ = t81;
        const qmu9_ = t91;
        const y_4 = (qmu5_ & 0xffffffffff) << 16;
        const x_4 = qmu4_ >> 40;
        const z02 = x_4 | y_4;
        const y_5 = (qmu6_ & 0xffffffffff) << 16;
        const x_5 = qmu5_ >> 40;
        const z12 = x_5 | y_5;
        const y_6 = (qmu7_ & 0xffffffffff) << 16;
        const x_6 = qmu6_ >> 40;
        const z22 = x_6 | y_6;
        const y_7 = (qmu8_ & 0xffffffffff) << 16;
        const x_7 = qmu7_ >> 40;
        const z32 = x_7 | y_7;
        const y_8 = (qmu9_ & 0xffffffffff) << 16;
        const x_8 = qmu8_ >> 40;
        const z42 = x_8 | y_8;
        const qdiv0 = z02;
        const qdiv1 = z12;
        const qdiv2 = z22;
        const qdiv3 = z32;
        const qdiv4 = z42;
        const r0 = t0;
        const r1 = t1;
        const r2 = t2;
        const r3 = t3;
        const r4 = t4 & 0xffffffffff;

        const xy00 = @as(u128, qdiv0) * @as(u128, m0);
        const xy01 = @as(u128, qdiv0) * @as(u128, m1);
        const xy02 = @as(u128, qdiv0) * @as(u128, m2);
        const xy03 = @as(u128, qdiv0) * @as(u128, m3);
        const xy04 = @as(u128, qdiv0) * @as(u128, m4);
        const xy10 = @as(u128, qdiv1) * @as(u128, m0);
        const xy11 = @as(u128, qdiv1) * @as(u128, m1);
        const xy12 = @as(u128, qdiv1) * @as(u128, m2);
        const xy13 = @as(u128, qdiv1) * @as(u128, m3);
        const xy20 = @as(u128, qdiv2) * @as(u128, m0);
        const xy21 = @as(u128, qdiv2) * @as(u128, m1);
        const xy22 = @as(u128, qdiv2) * @as(u128, m2);
        const xy30 = @as(u128, qdiv3) * @as(u128, m0);
        const xy31 = @as(u128, qdiv3) * @as(u128, m1);
        const xy40 = @as(u128, qdiv4) * @as(u128, m0);
        const carry9 = xy00 >> 56;
        const t108 = @as(u64, @truncate(xy00)) & 0xffffffffffffff;
        const c0 = carry9;
        const t010 = t108;
        const carry10 = (xy01 + xy10 + c0) >> 56;
        const t109 = @as(u64, @truncate(xy01 + xy10 + c0)) & 0xffffffffffffff;
        const c11 = carry10;
        const t110 = t109;
        const carry11 = (xy02 + xy11 + xy20 + c11) >> 56;
        const t1010 = @as(u64, @truncate(xy02 + xy11 + xy20 + c11)) & 0xffffffffffffff;
        const c21 = carry11;
        const t210 = t1010;
        const carry = (xy03 + xy12 + xy21 + xy30 + c21) >> 56;
        const t1011 = @as(u64, @truncate(xy03 + xy12 + xy21 + xy30 + c21)) & 0xffffffffffffff;
        const c31 = carry;
        const t310 = t1011;
        const t411 = @as(u64, @truncate(xy04 + xy13 + xy22 + xy31 + xy40 + c31)) & 0xffffffffff;

        const qmul0 = t010;
        const qmul1 = t110;
        const qmul2 = t210;
        const qmul3 = t310;
        const qmul4 = t411;
        const b5 = (r0 -% qmul0) >> 63;
        const t1012 = ((b5 << 56) + r0) -% qmul0;
        const c1 = b5;
        const t011 = t1012;
        const b6 = (r1 -% (qmul1 + c1)) >> 63;
        const t1013 = ((b6 << 56) + r1) -% (qmul1 + c1);
        const c2 = b6;
        const t111 = t1013;
        const b7 = (r2 -% (qmul2 + c2)) >> 63;
        const t1014 = ((b7 << 56) + r2) -% (qmul2 + c2);
        const c3 = b7;
        const t211 = t1014;
        const b8 = (r3 -% (qmul3 + c3)) >> 63;
        const t1015 = ((b8 << 56) + r3) -% (qmul3 + c3);
        const c4 = b8;
        const t311 = t1015;
        const b9 = (r4 -% (qmul4 + c4)) >> 63;
        const t1016 = ((b9 << 40) + r4) -% (qmul4 + c4);
        const t412 = t1016;
        const s0 = t011;
        const s1 = t111;
        const s2 = t211;
        const s3 = t311;
        const s4 = t412;

        const y0: u64 = 5175514460705773;
        const y1: u64 = 70332060721272408;
        const y2: u64 = 5342;
        const y3: u64 = 0;
        const y4: u64 = 268435456;

        const b10 = (s0 -% y0) >> 63;
        const t1017 = ((b10 << 56) + s0) -% y0;
        const b0 = b10;
        const t01 = t1017;
        const b11 = (s1 -% (y1 + b0)) >> 63;
        const t1018 = ((b11 << 56) + s1) -% (y1 + b0);
        const b1 = b11;
        const t11 = t1018;
        const b12 = (s2 -% (y2 + b1)) >> 63;
        const t1019 = ((b12 << 56) + s2) -% (y2 + b1);
        const b2 = b12;
        const t21 = t1019;
        const b13 = (s3 -% (y3 + b2)) >> 63;
        const t1020 = ((b13 << 56) + s3) -% (y3 + b2);
        const b3 = b13;
        const t31 = t1020;
        const b = (s4 -% (y4 + b3)) >> 63;
        const t10 = ((b << 56) + s4) -% (y4 + b3);
        const b4 = b;
        const t41 = t10;
        const mask = b4 -% @as(u64, @as(u64, 1));
        const z03 = s0 ^ (mask & (s0 ^ t01));
        const z13 = s1 ^ (mask & (s1 ^ t11));
        const z23 = s2 ^ (mask & (s2 ^ t21));
        const z33 = s3 ^ (mask & (s3 ^ t31));
        const z43 = s4 ^ (mask & (s4 ^ t41));

        return Scalar{ .limbs = .{ z03, z13, z23, z33, z43 } };
    }
};

test "scalar25519" {
    const bytes: [32]u8 = .{ 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 255 };
    var x = Scalar.fromBytes(bytes);
    var y = x.toBytes();
    try rejectNonCanonical(y);
    var buf: [128]u8 = undefined;
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&y}), "1E979B917937F3DE71D18077F961F6CEFF01030405060708010203040506070F");

    const reduced = reduce(field_order_s);
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&reduced}), "0000000000000000000000000000000000000000000000000000000000000000");
}

test "non-canonical scalar25519" {
    const too_targe: [32]u8 = .{ 0xed, 0xd3, 0xf5, 0x5c, 0x1a, 0x63, 0x12, 0x58, 0xd6, 0x9c, 0xf7, 0xa2, 0xde, 0xf9, 0xde, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10 };
    try std.testing.expectError(error.NonCanonical, rejectNonCanonical(too_targe));
}

test "mulAdd overflow check" {
    const a: [32]u8 = [_]u8{0xff} ** 32;
    const b: [32]u8 = [_]u8{0xff} ** 32;
    const c: [32]u8 = [_]u8{0xff} ** 32;
    const x = mulAdd(a, b, c);
    var buf: [128]u8 = undefined;
    try std.testing.expectEqualStrings(try std.fmt.bufPrint(&buf, "{X}", .{&x}), "D14DF91389432C25AD60FF9791B9FD1D67BEF517D273ECCE3D9A307C1B419903");
}

test "scalar field inversion" {
    const bytes: [32]u8 = .{ 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8 };
    const x = Scalar.fromBytes(bytes);
    const inv = x.invert();
    const recovered_x = inv.invert();
    try std.testing.expectEqualSlices(u8, &bytes, &recovered_x.toBytes());
}

test "random scalar" {
    const io = std.testing.io;
    const s1 = random(io);
    const s2 = random(io);
    try std.testing.expect(!mem.eql(u8, &s1, &s2));
}

test "64-bit reduction" {
    const bytes = field_order_s ++ [_]u8{0} ** 32;
    const x = Scalar.fromBytes64(bytes);
    try std.testing.expect(x.isZero());
}



---
File: /std/crypto/25519/x25519.zig
---

const std = @import("std");
const crypto = std.crypto;
const mem = std.mem;
const fmt = std.fmt;

const Sha512 = crypto.hash.sha2.Sha512;

const EncodingError = crypto.errors.EncodingError;
const IdentityElementError = crypto.errors.IdentityElementError;
const WeakPublicKeyError = crypto.errors.WeakPublicKeyError;

/// X25519 DH function.
pub const X25519 = struct {
    /// The underlying elliptic curve.
    pub const Curve = @import("curve25519.zig").Curve25519;
    /// Length (in bytes) of a secret key.
    pub const secret_length = 32;
    /// Length (in bytes) of a public key.
    pub const public_length = 32;
    /// Length (in bytes) of the output of the DH function.
    pub const shared_length = 32;
    /// Seed (for key pair creation) length in bytes.
    pub const seed_length = 32;

    /// An X25519 key pair.
    pub const KeyPair = struct {
        /// Public part.
        public_key: [public_length]u8,
        /// Secret part.
        secret_key: [secret_length]u8,

        /// Deterministically derive a key pair from a cryptograpically secure secret seed.
        ///
        /// Except in tests, applications should generally call `generate()` instead of this function.
        pub fn generateDeterministic(seed: [seed_length]u8) IdentityElementError!KeyPair {
            const kp = KeyPair{
                .public_key = try X25519.recoverPublicKey(seed),
                .secret_key = seed,
            };
            return kp;
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

        /// Create a key pair from an Ed25519 key pair
        pub fn fromEd25519(ed25519_key_pair: crypto.sign.Ed25519.KeyPair) (IdentityElementError || EncodingError)!KeyPair {
            const seed = ed25519_key_pair.secret_key.seed();
            var az: [Sha512.digest_length]u8 = undefined;
            Sha512.hash(&seed, &az, .{});
            var sk = az[0..32].*;
            Curve.scalar.clamp(&sk);
            const pk = try publicKeyFromEd25519(ed25519_key_pair.public_key);
            return KeyPair{
                .public_key = pk,
                .secret_key = sk,
            };
        }
    };

    /// Compute the public key for a given private key.
    pub fn recoverPublicKey(secret_key: [secret_length]u8) IdentityElementError![public_length]u8 {
        const q = try Curve.basePoint.clampedMul(secret_key);
        return q.toBytes();
    }

    /// Compute the X25519 equivalent to an Ed25519 public eky.
    pub fn publicKeyFromEd25519(ed25519_public_key: crypto.sign.Ed25519.PublicKey) (IdentityElementError || EncodingError)![public_length]u8 {
        const pk_ed = try crypto.ecc.Edwards25519.fromBytes(ed25519_public_key.bytes);
        const pk = try Curve.fromEdwards25519(pk_ed);
        return pk.toBytes();
    }

    /// Compute the scalar product of a public key and a secret scalar.
    /// Note that the output should not be used as a shared secret without
    /// hashing it first.
    pub fn scalarmult(secret_key: [secret_length]u8, public_key: [public_length]u8) IdentityElementError![shared_length]u8 {
        const q = try Curve.fromBytes(public_key).clampedMul(secret_key);
        return q.toBytes();
    }
};

const htest = @import("../test.zig");

test "public key calculation from secret key" {
    var sk: [32]u8 = undefined;
    var pk_expected: [32]u8 = undefined;
    _ = try fmt.hexToBytes(sk[0..], "8052030376d47112be7f73ed7a019293dd12ad910b654455798b4667d73de166");
    _ = try fmt.hexToBytes(pk_expected[0..], "f1814f0e8ff1043d8a44d25babff3cedcae6c22c3edaa48f857ae70de2baae50");
    const pk_calculated = try X25519.recoverPublicKey(sk);
    try std.testing.expectEqual(pk_calculated, pk_expected);
}

test "rfc7748 vector1" {
    const secret_key = [32]u8{ 0xa5, 0x46, 0xe3, 0x6b, 0xf0, 0x52, 0x7c, 0x9d, 0x3b, 0x16, 0x15, 0x4b, 0x82, 0x46, 0x5e, 0xdd, 0x62, 0x14, 0x4c, 0x0a, 0xc1, 0xfc, 0x5a, 0x18, 0x50, 0x6a, 0x22, 0x44, 0xba, 0x44, 0x9a, 0xc4 };
    const public_key = [32]u8{ 0xe6, 0xdb, 0x68, 0x67, 0x58, 0x30, 0x30, 0xdb, 0x35, 0x94, 0xc1, 0xa4, 0x24, 0xb1, 0x5f, 0x7c, 0x72, 0x66, 0x24, 0xec, 0x26, 0xb3, 0x35, 0x3b, 0x10, 0xa9, 0x03, 0xa6, 0xd0, 0xab, 0x1c, 0x4c };

    const expected_output = [32]u8{ 0xc3, 0xda, 0x55, 0x37, 0x9d, 0xe9, 0xc6, 0x90, 0x8e, 0x94, 0xea, 0x4d, 0xf2, 0x8d, 0x08, 0x4f, 0x32, 0xec, 0xcf, 0x03, 0x49, 0x1c, 0x71, 0xf7, 0x54, 0xb4, 0x07, 0x55, 0x77, 0xa2, 0x85, 0x52 };

    const output = try X25519.scalarmult(secret_key, public_key);
    try std.testing.expectEqual(output, expected_output);
}

test "rfc7748 vector2" {
    const secret_key = [32]u8{ 0x4b, 0x66, 0xe9, 0xd4, 0xd1, 0xb4, 0x67, 0x3c, 0x5a, 0xd2, 0x26, 0x91, 0x95, 0x7d, 0x6a, 0xf5, 0xc1, 0x1b, 0x64, 0x21, 0xe0, 0xea, 0x01, 0xd4, 0x2c, 0xa4, 0x16, 0x9e, 0x79, 0x18, 0xba, 0x0d };
    const public_key = [32]u8{ 0xe5, 0x21, 0x0f, 0x12, 0x78, 0x68, 0x11, 0xd3, 0xf4, 0xb7, 0x95, 0x9d, 0x05, 0x38, 0xae, 0x2c, 0x31, 0xdb, 0xe7, 0x10, 0x6f, 0xc0, 0x3c, 0x3e, 0xfc, 0x4c, 0xd5, 0x49, 0xc7, 0x15, 0xa4, 0x93 };

    const expected_output = [32]u8{ 0x95, 0xcb, 0xde, 0x94, 0x76, 0xe8, 0x90, 0x7d, 0x7a, 0xad, 0xe4, 0x5c, 0xb4, 0xb8, 0x73, 0xf8, 0x8b, 0x59, 0x5a, 0x68, 0x79, 0x9f, 0xa1, 0x52, 0xe6, 0xf8, 0xf7, 0x64, 0x7a, 0xac, 0x79, 0x57 };

    const output = try X25519.scalarmult(secret_key, public_key);
    try std.testing.expectEqual(output, expected_output);
}

test "rfc7748 one iteration" {
    const initial_value = [32]u8{ 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    const expected_output = [32]u8{ 0x42, 0x2c, 0x8e, 0x7a, 0x62, 0x27, 0xd7, 0xbc, 0xa1, 0x35, 0x0b, 0x3e, 0x2b, 0xb7, 0x27, 0x9f, 0x78, 0x97, 0xb8, 0x7b, 0xb6, 0x85, 0x4b, 0x78, 0x3c, 0x60, 0xe8, 0x03, 0x11, 0xae, 0x30, 0x79 };

    var k: [32]u8 = initial_value;
    var u: [32]u8 = initial_value;

    var i: usize = 0;
    while (i < 1) : (i += 1) {
        const output = try X25519.scalarmult(k, u);
        u = k;
        k = output;
    }

    try std.testing.expectEqual(k, expected_output);
}

test "rfc7748 1,000 iterations" {
    // These iteration tests are slow so we always skip them. Results have been verified.
    if (true) {
        return error.SkipZigTest;
    }

    const initial_value = [32]u8{ 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    const expected_output = [32]u8{ 0x68, 0x4c, 0xf5, 0x9b, 0xa8, 0x33, 0x09, 0x55, 0x28, 0x00, 0xef, 0x56, 0x6f, 0x2f, 0x4d, 0x3c, 0x1c, 0x38, 0x87, 0xc4, 0x93, 0x60, 0xe3, 0x87, 0x5f, 0x2e, 0xb9, 0x4d, 0x99, 0x53, 0x2c, 0x51 };

    var k: [32]u8 = initial_value.*;
    var u: [32]u8 = initial_value.*;

    var i: usize = 0;
    while (i < 1000) : (i += 1) {
        const output = try X25519.scalarmult(&k, &u);
        u = k;
        k = output;
    }

    try std.testing.expectEqual(k, expected_output);
}

test "rfc7748 1,000,000 iterations" {
    if (true) {
        return error.SkipZigTest;
    }

    const initial_value = [32]u8{ 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    const expected_output = [32]u8{ 0x7c, 0x39, 0x11, 0xe0, 0xab, 0x25, 0x86, 0xfd, 0x86, 0x44, 0x97, 0x29, 0x7e, 0x57, 0x5e, 0x6f, 0x3b, 0xc6, 0x01, 0xc0, 0x88, 0x3c, 0x30, 0xdf, 0x5f, 0x4d, 0xd2, 0xd2, 0x4f, 0x66, 0x54, 0x24 };

    var k: [32]u8 = initial_value.*;
    var u: [32]u8 = initial_value.*;

    var i: usize = 0;
    while (i < 1000000) : (i += 1) {
        const output = try X25519.scalarmult(&k, &u);
        u = k;
        k = output;
    }

    try std.testing.expectEqual(k[0..], expected_output);
}

test "edwards25519 -> curve25519 map" {
    const ed_kp = try crypto.sign.Ed25519.KeyPair.generateDeterministic([_]u8{0x42} ** 32);
    const mont_kp = try X25519.KeyPair.fromEd25519(ed_kp);
    try htest.assertEqual("90e7595fc89e52fdfddce9c6a43d74dbf6047025ee0462d2d172e8b6a2841d6e", &mont_kp.secret_key);
    try htest.assertEqual("cc4f2cdb695dd766f34118eb67b98652fed1d8bc49c330b119bbfa8a64989378", &mont_kp.public_key);
}



---
File: /std/crypto/aes/aesni.zig
---

const std = @import("../../std.zig");
const builtin = @import("builtin");
const mem = std.mem;
const debug = std.debug;

const has_vaes = builtin.cpu.arch == .x86_64 and builtin.cpu.has(.x86, .vaes);
const has_avx512f = builtin.cpu.arch == .x86_64 and builtin.zig_backend != .stage2_x86_64 and builtin.cpu.has(.x86, .avx512f);

/// A single AES block.
pub const Block = struct {
    const Repr = @Vector(2, u64);

    /// The length of an AES block in bytes.
    pub const block_length: usize = 16;

    /// Internal representation of a block.
    repr: Repr,

    /// Convert a byte sequence into an internal representation.
    pub fn fromBytes(bytes: *const [16]u8) Block {
        const repr = mem.bytesToValue(Repr, bytes);
        return Block{ .repr = repr };
    }

    /// Convert the internal representation of a block into a byte sequence.
    pub fn toBytes(block: Block) [16]u8 {
        return mem.toBytes(block.repr);
    }

    /// XOR the block with a byte sequence.
    pub fn xorBytes(block: Block, bytes: *const [16]u8) [16]u8 {
        const x = block.repr ^ fromBytes(bytes).repr;
        return mem.toBytes(x);
    }

    /// Encrypt a block with a round key.
    pub fn encrypt(block: Block, round_key: Block) Block {
        return Block{
            .repr = asm (
                \\ vaesenc %[rk], %[in], %[out]
                : [out] "=x" (-> Repr),
                : [in] "x" (block.repr),
                  [rk] "x" (round_key.repr),
            ),
        };
    }

    /// Encrypt a block with the last round key.
    pub fn encryptLast(block: Block, round_key: Block) Block {
        return Block{
            .repr = asm (
                \\ vaesenclast %[rk], %[in], %[out]
                : [out] "=x" (-> Repr),
                : [in] "x" (block.repr),
                  [rk] "x" (round_key.repr),
            ),
        };
    }

    /// Decrypt a block with a round key.
    pub fn decrypt(block: Block, inv_round_key: Block) Block {
        return Block{
            .repr = asm (
                \\ vaesdec %[rk], %[in], %[out]
                : [out] "=x" (-> Repr),
                : [in] "x" (block.repr),
                  [rk] "x" (inv_round_key.repr),
            ),
        };
    }

    /// Decrypt a block with the last round key.
    pub fn decryptLast(block: Block, inv_round_key: Block) Block {
        return Block{
            .repr = asm (
                \\ vaesdeclast %[rk], %[in], %[out]
                : [out] "=x" (-> Repr),
                : [in] "x" (block.repr),
                  [rk] "x" (inv_round_key.repr),
            ),
        };
    }

    /// Apply the bitwise XOR operation to the content of two blocks.
    pub fn xorBlocks(block1: Block, block2: Block) Block {
        return Block{ .repr = block1.repr ^ block2.repr };
    }

    /// Apply the bitwise AND operation to the content of two blocks.
    pub fn andBlocks(block1: Block, block2: Block) Block {
        return Block{ .repr = block1.repr & block2.repr };
    }

    /// Apply the bitwise OR operation to the content of two blocks.
    pub fn orBlocks(block1: Block, block2: Block) Block {
        return Block{ .repr = block1.repr | block2.repr };
    }

    /// Apply the inverse MixColumns operation to a block.
    pub fn invMixColumns(block: Block) Block {
        return Block{
            .repr = asm (
                \\ vaesimc %[in], %[out]
                : [out] "=x" (-> Repr),
                : [in] "x" (block.repr),
            ),
        };
    }

    /// Perform operations on multiple blocks in parallel.
    pub const parallel = struct {
        const cpu = std.Target.x86.cpu;

        /// The recommended number of AES encryption/decryption to perform in parallel for the chosen implementation.
        pub const optimal_parallel_blocks = switch (builtin.cpu.model) {
            &cpu.westmere, &cpu.goldmont => 3,
            &cpu.cannonlake, &cpu.skylake, &cpu.skylake_avx512, &cpu.tremont, &cpu.goldmont_plus, &cpu.cascadelake => 4,
            &cpu.icelake_client, &cpu.icelake_server, &cpu.tigerlake, &cpu.rocketlake, &cpu.alderlake => 6,
            &cpu.haswell, &cpu.broadwell => 7,
            &cpu.sandybridge, &cpu.ivybridge => 8,
            &cpu.znver1, &cpu.znver2, &cpu.znver3, &cpu.znver4 => 8,
            else => 8,
        };

        /// Encrypt multiple blocks in parallel, each their own round key.
        pub fn encryptParallel(comptime count: usize, blocks: [count]Block, round_keys: [count]Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].encrypt(round_keys[i]);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel, each their own round key.
        pub fn decryptParallel(comptime count: usize, blocks: [count]Block, round_keys: [count]Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].decrypt(round_keys[i]);
            }
            return out;
        }

        /// Encrypt multiple blocks in parallel with the same round key.
        pub fn encryptWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].encrypt(round_key);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel with the same round key.
        pub fn decryptWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].decrypt(round_key);
            }
            return out;
        }

        /// Encrypt multiple blocks in parallel with the same last round key.
        pub fn encryptLastWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].encryptLast(round_key);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel with the same last round key.
        pub fn decryptLastWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].decryptLast(round_key);
            }
            return out;
        }
    };
};

/// A fixed-size vector of AES blocks.
/// All operations are performed in parallel, using SIMD instructions when available.
pub fn BlockVec(comptime blocks_count: comptime_int) type {
    return struct {
        const Self = @This();

        /// The number of AES blocks the target architecture can process with a single instruction.
        pub const native_vector_size = w: {
            if (has_avx512f and blocks_count % 4 == 0) break :w 4;
            if (has_vaes and blocks_count % 2 == 0) break :w 2;
            break :w 1;
        };

        /// The size of the AES block vector that the target architecture can process with a single instruction, in bytes.
        pub const native_word_size = native_vector_size * 16;

        const native_words = blocks_count / native_vector_size;

        const Repr = @Vector(native_vector_size * 2, u64);

        /// Internal representation of a block vector.
        repr: [native_words]Repr,

        /// Length of the block vector in bytes.
        pub const block_length: usize = blocks_count * 16;

        /// Convert a byte sequence into an internal representation.
        pub fn fromBytes(bytes: *const [blocks_count * 16]u8) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = mem.bytesToValue(Repr, bytes[i * native_word_size ..][0..native_word_size]);
            }
            return out;
        }

        /// Convert the internal representation of a block vector into a byte sequence.
        pub fn toBytes(block_vec: Self) [blocks_count * 16]u8 {
            var out: [blocks_count * 16]u8 = undefined;
            inline for (0..native_words) |i| {
                out[i * native_word_size ..][0..native_word_size].* = mem.toBytes(block_vec.repr[i]);
            }
            return out;
        }

        /// XOR the block vector with a byte sequence.
        pub fn xorBytes(block_vec: Self, bytes: *const [blocks_count * 16]u8) [blocks_count * 16]u8 {
            var x: Self = undefined;
            inline for (0..native_words) |i| {
                x.repr[i] = block_vec.repr[i] ^ mem.bytesToValue(Repr, bytes[i * native_word_size ..][0..native_word_size]);
            }
            return x.toBytes();
        }

        /// Apply the forward AES operation to the block vector with a vector of round keys.
        pub fn encrypt(block_vec: Self, round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = asm (
                    \\ vaesenc %[rk], %[in], %[out]
                    : [out] "=x" (-> Repr),
                    : [in] "x" (block_vec.repr[i]),
                      [rk] "x" (round_key_vec.repr[i]),
                );
            }
            return out;
        }

        /// Apply the forward AES operation to the block vector with a vector of last round keys.
        pub fn encryptLast(block_vec: Self, round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = asm (
                    \\ vaesenclast %[rk], %[in], %[out]
                    : [out] "=x" (-> Repr),
                    : [in] "x" (block_vec.repr[i]),
                      [rk] "x" (round_key_vec.repr[i]),
                );
            }
            return out;
        }

        /// Apply the inverse AES operation to the block vector with a vector of round keys.
        pub fn decrypt(block_vec: Self, inv_round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = asm (
                    \\ vaesdec %[rk], %[in], %[out]
                    : [out] "=x" (-> Repr),
                    : [in] "x" (block_vec.repr[i]),
                      [rk] "x" (inv_round_key_vec.repr[i]),
                );
            }
            return out;
        }

        /// Apply the inverse AES operation to the block vector with a vector of last round keys.
        pub fn decryptLast(block_vec: Self, inv_round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = asm (
                    \\ vaesdeclast %[rk], %[in], %[out]
                    : [out] "=x" (-> Repr),
                    : [in] "x" (block_vec.repr[i]),
                      [rk] "x" (inv_round_key_vec.repr[i]),
                );
            }
            return out;
        }

        /// Apply the bitwise XOR operation to the content of two block vectors.
        pub fn xorBlocks(block_vec1: Self, block_vec2: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i] ^ block_vec2.repr[i];
            }
            return out;
        }

        /// Apply the bitwise AND operation to the content of two block vectors.
        pub fn andBlocks(block_vec1: Self, block_vec2: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i] & block_vec2.repr[i];
            }
            return out;
        }

        /// Apply the bitwise OR operation to the content of two block vectors.
        pub fn orBlocks(block_vec1: Self, block_vec2: Block) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i] | block_vec2.repr[i];
            }
            return out;
        }

        /// Apply the inverse MixColumns operation to each block in the vector.
        pub fn invMixColumns(block_vec: Self) Self {
            var out_bytes: [blocks_count * 16]u8 = undefined;
            const in_bytes = block_vec.toBytes();
            inline for (0..blocks_count) |i| {
                const block = Block.fromBytes(in_bytes[i * 16 ..][0..16]);
                out_bytes[i * 16 ..][0..16].* = block.invMixColumns().toBytes();
            }
            return fromBytes(&out_bytes);
        }
    };
}

fn KeySchedule(comptime Aes: type) type {
    std.debug.assert(Aes.rounds == 10 or Aes.rounds == 14);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();

        const Repr = Aes.block.Repr;

        round_keys: [rounds + 1]Block,

        fn drc(comptime second: bool, comptime rc: u8, t: Repr, tx: Repr) Repr {
            var s: Repr = undefined;
            var ts: Repr = undefined;
            return asm (
                \\ vaeskeygenassist %[rc], %[t], %[s]
                \\ vpslldq $4, %[tx], %[ts]
                \\ vpxor   %[ts], %[tx], %[r]
                \\ vpslldq $8, %[r], %[ts]
                \\ vpxor   %[ts], %[r], %[r]
                \\ vpshufd %[mask], %[s], %[ts]
                \\ vpxor   %[ts], %[r], %[r]
                : [r] "=&x" (-> Repr),
                  [s] "=&x" (s),
                  [ts] "=&x" (ts),
                : [rc] "n" (rc),
                  [t] "x" (t),
                  [tx] "x" (tx),
                  [mask] "n" (@as(u8, if (second) 0xaa else 0xff)),
            );
        }

        fn expand128(t1: *Block) Self {
            var round_keys: [11]Block = undefined;
            const rcs = [_]u8{ 1, 2, 4, 8, 16, 32, 64, 128, 27, 54 };
            inline for (rcs, 0..) |rc, round| {
                round_keys[round] = t1.*;
                t1.repr = drc(false, rc, t1.repr, t1.repr);
            }
            round_keys[rcs.len] = t1.*;
            return Self{ .round_keys = round_keys };
        }

        fn expand256(t1: *Block, t2: *Block) Self {
            var round_keys: [15]Block = undefined;
            const rcs = [_]u8{ 1, 2, 4, 8, 16, 32 };
            round_keys[0] = t1.*;
            inline for (rcs, 0..) |rc, round| {
                round_keys[round * 2 + 1] = t2.*;
                t1.repr = drc(false, rc, t2.repr, t1.repr);
                round_keys[round * 2 + 2] = t1.*;
                t2.repr = drc(true, rc, t1.repr, t2.repr);
            }
            round_keys[rcs.len * 2 + 1] = t2.*;
            t1.repr = drc(false, 64, t2.repr, t1.repr);
            round_keys[rcs.len * 2 + 2] = t1.*;
            return Self{ .round_keys = round_keys };
        }

        /// Invert the key schedule.
        pub fn invert(key_schedule: Self) Self {
            const round_keys = &key_schedule.round_keys;
            var inv_round_keys: [rounds + 1]Block = undefined;
            inv_round_keys[0] = round_keys[rounds];
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                inv_round_keys[i] = Block{
                    .repr = asm (
                        \\ vaesimc %[rk], %[inv_rk]
                        : [inv_rk] "=x" (-> Repr),
                        : [rk] "x" (round_keys[rounds - i].repr),
                    ),
                };
            }
            inv_round_keys[rounds] = round_keys[0];
            return Self{ .round_keys = inv_round_keys };
        }
    };
}

/// A context to perform encryption using the standard AES key schedule.
pub fn AesEncryptCtx(comptime Aes: type) type {
    std.debug.assert(Aes.key_bits == 128 or Aes.key_bits == 256);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        pub const block = Aes.block;
        pub const block_length = block.block_length;
        key_schedule: KeySchedule(Aes),

        /// Create a new encryption context with the given key.
        pub fn init(key: [Aes.key_bits / 8]u8) Self {
            var t1 = Block.fromBytes(key[0..16]);
            const key_schedule = if (Aes.key_bits == 128) ks: {
                break :ks KeySchedule(Aes).expand128(&t1);
            } else ks: {
                var t2 = Block.fromBytes(key[16..32]);
                break :ks KeySchedule(Aes).expand256(&t1, &t2);
            };
            return Self{
                .key_schedule = key_schedule,
            };
        }

        /// Encrypt a single block.
        pub fn encrypt(ctx: Self, dst: *[16]u8, src: *const [16]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(src).xorBlocks(round_keys[0]);
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                t = t.encrypt(round_keys[i]);
            }
            t = t.encryptLast(round_keys[rounds]);
            dst.* = t.toBytes();
        }

        /// Encrypt+XOR a single block.
        pub fn xor(ctx: Self, dst: *[16]u8, src: *const [16]u8, counter: [16]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(&counter).xorBlocks(round_keys[0]);
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                t = t.encrypt(round_keys[i]);
            }
            t = t.encryptLast(round_keys[rounds]);
            dst.* = t.xorBytes(src);
        }

        /// Encrypt multiple blocks, possibly leveraging parallelization.
        pub fn encryptWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var ts: [count]Block = undefined;
            comptime var j = 0;
            inline while (j < count) : (j += 1) {
                ts[j] = Block.fromBytes(src[j * 16 .. j * 16 + 16][0..16]).xorBlocks(round_keys[0]);
            }
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                ts = Block.parallel.encryptWide(count, ts, round_keys[i]);
            }
            ts = Block.parallel.encryptLastWide(count, ts, round_keys[i]);
            j = 0;
            inline while (j < count) : (j += 1) {
                dst[16 * j .. 16 * j + 16].* = ts[j].toBytes();
            }
        }

        /// Encrypt+XOR multiple blocks, possibly leveraging parallelization.
        pub fn xorWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8, counters: [16 * count]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var ts: [count]Block = undefined;
            comptime var j = 0;
            inline while (j < count) : (j += 1) {
                ts[j] = Block.fromBytes(counters[j * 16 .. j * 16 + 16][0..16]).xorBlocks(round_keys[0]);
            }
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                ts = Block.parallel.encryptWide(count, ts, round_keys[i]);
            }
            ts = Block.parallel.encryptLastWide(count, ts, round_keys[i]);
            j = 0;
            inline while (j < count) : (j += 1) {
                dst[16 * j .. 16 * j + 16].* = ts[j].xorBytes(src[16 * j .. 16 * j + 16]);
            }
        }
    };
}

/// A context to perform decryption using the standard AES key schedule.
pub fn AesDecryptCtx(comptime Aes: type) type {
    std.debug.assert(Aes.key_bits == 128 or Aes.key_bits == 256);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        pub const block = Aes.block;
        pub const block_length = block.block_length;
        key_schedule: KeySchedule(Aes),

        /// Create a decryption context from an existing encryption context.
        pub fn initFromEnc(ctx: AesEncryptCtx(Aes)) Self {
            return Self{
                .key_schedule = ctx.key_schedule.invert(),
            };
        }

        /// Create a new decryption context with the given key.
        pub fn init(key: [Aes.key_bits / 8]u8) Self {
            const enc_ctx = AesEncryptCtx(Aes).init(key);
            return initFromEnc(enc_ctx);
        }

        /// Decrypt a single block.
        pub fn decrypt(ctx: Self, dst: *[16]u8, src: *const [16]u8) void {
            const inv_round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(src).xorBlocks(inv_round_keys[0]);
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                t = t.decrypt(inv_round_keys[i]);
            }
            t = t.decryptLast(inv_round_keys[rounds]);
            dst.* = t.toBytes();
        }

        /// Decrypt multiple blocks, possibly leveraging parallelization.
        pub fn decryptWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8) void {
            const inv_round_keys = ctx.key_schedule.round_keys;
            var ts: [count]Block = undefined;
            comptime var j = 0;
            inline while (j < count) : (j += 1) {
                ts[j] = Block.fromBytes(src[j * 16 .. j * 16 + 16][0..16]).xorBlocks(inv_round_keys[0]);
            }
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                ts = Block.parallel.decryptWide(count, ts, inv_round_keys[i]);
            }
            ts = Block.parallel.decryptLastWide(count, ts, inv_round_keys[i]);
            j = 0;
            inline while (j < count) : (j += 1) {
                dst[16 * j .. 16 * j + 16].* = ts[j].toBytes();
            }
        }
    };
}

/// AES-128 with the standard key schedule.
pub const Aes128 = struct {
    pub const key_bits: usize = 128;
    pub const rounds = ((key_bits - 64) / 32 + 8);
    pub const block = Block;

    /// Create a new context for encryption.
    pub fn initEnc(key: [key_bits / 8]u8) AesEncryptCtx(Aes128) {
        return AesEncryptCtx(Aes128).init(key);
    }

    /// Create a new context for decryption.
    pub fn initDec(key: [key_bits / 8]u8) AesDecryptCtx(Aes128) {
        return AesDecryptCtx(Aes128).init(key);
    }
};

/// AES-256 with the standard key schedule.
pub const Aes256 = struct {
    pub const key_bits: usize = 256;
    pub const rounds = ((key_bits - 64) / 32 + 8);
    pub const block = Block;

    /// Create a new context for encryption.
    pub fn initEnc(key: [key_bits / 8]u8) AesEncryptCtx(Aes256) {
        return AesEncryptCtx(Aes256).init(key);
    }

    /// Create a new context for decryption.
    pub fn initDec(key: [key_bits / 8]u8) AesDecryptCtx(Aes256) {
        return AesDecryptCtx(Aes256).init(key);
    }
};



---
File: /std/crypto/aes/armcrypto.zig
---

const std = @import("../../std.zig");
const mem = std.mem;
const debug = std.debug;

/// A single AES block.
pub const Block = struct {
    const Repr = @Vector(2, u64);

    pub const block_length: usize = 16;

    /// Internal representation of a block.
    repr: Repr,

    /// Convert a byte sequence into an internal representation.
    pub fn fromBytes(bytes: *const [16]u8) Block {
        const repr = mem.bytesToValue(Repr, bytes);
        return Block{ .repr = repr };
    }

    /// Convert the internal representation of a block into a byte sequence.
    pub fn toBytes(block: Block) [16]u8 {
        return mem.toBytes(block.repr);
    }

    /// XOR the block with a byte sequence.
    pub fn xorBytes(block: Block, bytes: *const [16]u8) [16]u8 {
        const x = block.repr ^ fromBytes(bytes).repr;
        return mem.toBytes(x);
    }

    const zero = @Vector(2, u64){ 0, 0 };

    /// Encrypt a block with a round key.
    pub fn encrypt(block: Block, round_key: Block) Block {
        return Block{
            .repr = (asm (
                \\ mov   %[out].16b, %[in].16b
                \\ aese  %[out].16b, %[zero].16b
                \\ aesmc %[out].16b, %[out].16b
                : [out] "=&x" (-> Repr),
                : [in] "x" (block.repr),
                  [zero] "x" (zero),
            )) ^ round_key.repr,
        };
    }

    /// Encrypt a block with the last round key.
    pub fn encryptLast(block: Block, round_key: Block) Block {
        return Block{
            .repr = (asm (
                \\ mov   %[out].16b, %[in].16b
                \\ aese  %[out].16b, %[zero].16b
                : [out] "=&x" (-> Repr),
                : [in] "x" (block.repr),
                  [zero] "x" (zero),
            )) ^ round_key.repr,
        };
    }

    /// Decrypt a block with a round key.
    pub fn decrypt(block: Block, inv_round_key: Block) Block {
        return Block{
            .repr = (asm (
                \\ mov   %[out].16b, %[in].16b
                \\ aesd  %[out].16b, %[zero].16b
                \\ aesimc %[out].16b, %[out].16b
                : [out] "=&x" (-> Repr),
                : [in] "x" (block.repr),
                  [zero] "x" (zero),
            )) ^ inv_round_key.repr,
        };
    }

    /// Decrypt a block with the last round key.
    pub fn decryptLast(block: Block, inv_round_key: Block) Block {
        return Block{
            .repr = (asm (
                \\ mov   %[out].16b, %[in].16b
                \\ aesd  %[out].16b, %[zero].16b
                : [out] "=&x" (-> Repr),
                : [in] "x" (block.repr),
                  [zero] "x" (zero),
            )) ^ inv_round_key.repr,
        };
    }

    /// Apply the bitwise XOR operation to the content of two blocks.
    pub fn xorBlocks(block1: Block, block2: Block) Block {
        return Block{ .repr = block1.repr ^ block2.repr };
    }

    /// Apply the bitwise AND operation to the content of two blocks.
    pub fn andBlocks(block1: Block, block2: Block) Block {
        return Block{ .repr = block1.repr & block2.repr };
    }

    /// Apply the bitwise OR operation to the content of two blocks.
    pub fn orBlocks(block1: Block, block2: Block) Block {
        return Block{ .repr = block1.repr | block2.repr };
    }

    /// Apply the inverse MixColumns operation to a block.
    pub fn invMixColumns(block: Block) Block {
        return Block{
            .repr = asm (
                \\ aesimc %[out].16b, %[in].16b
                : [out] "=x" (-> Repr),
                : [in] "x" (block.repr),
            ),
        };
    }

    /// Perform operations on multiple blocks in parallel.
    pub const parallel = struct {
        /// The recommended number of AES encryption/decryption to perform in parallel for the chosen implementation.
        pub const optimal_parallel_blocks = 6;

        /// Encrypt multiple blocks in parallel, each their own round key.
        pub fn encryptParallel(comptime count: usize, blocks: [count]Block, round_keys: [count]Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].encrypt(round_keys[i]);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel, each their own round key.
        pub fn decryptParallel(comptime count: usize, blocks: [count]Block, round_keys: [count]Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].decrypt(round_keys[i]);
            }
            return out;
        }

        /// Encrypt multiple blocks in parallel with the same round key.
        pub fn encryptWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].encrypt(round_key);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel with the same round key.
        pub fn decryptWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].decrypt(round_key);
            }
            return out;
        }

        /// Encrypt multiple blocks in parallel with the same last round key.
        pub fn encryptLastWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].encryptLast(round_key);
            }
            return out;
        }

        /// Decrypt multiple blocks in parallel with the same last round key.
        pub fn decryptLastWide(comptime count: usize, blocks: [count]Block, round_key: Block) [count]Block {
            comptime var i = 0;
            var out: [count]Block = undefined;
            inline while (i < count) : (i += 1) {
                out[i] = blocks[i].decryptLast(round_key);
            }
            return out;
        }
    };
};

/// A fixed-size vector of AES blocks.
/// All operations are performed in parallel, using SIMD instructions when available.
pub fn BlockVec(comptime blocks_count: comptime_int) type {
    return struct {
        const Self = @This();

        /// The number of AES blocks the target architecture can process with a single instruction.
        pub const native_vector_size = 1;

        /// The size of the AES block vector that the target architecture can process with a single instruction, in bytes.
        pub const native_word_size = native_vector_size * 16;

        const native_words = blocks_count;

        /// Internal representation of a block vector.
        repr: [native_words]Block,

        /// Length of the block vector in bytes.
        pub const block_length: usize = blocks_count * 16;

        /// Convert a byte sequence into an internal representation.
        pub fn fromBytes(bytes: *const [blocks_count * 16]u8) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = Block.fromBytes(bytes[i * native_word_size ..][0..native_word_size]);
            }
            return out;
        }

        /// Convert the internal representation of a block vector into a byte sequence.
        pub fn toBytes(block_vec: Self) [blocks_count * 16]u8 {
            var out: [blocks_count * 16]u8 = undefined;
            inline for (0..native_words) |i| {
                out[i * native_word_size ..][0..native_word_size].* = block_vec.repr[i].toBytes();
            }
            return out;
        }

        /// XOR the block vector with a byte sequence.
        pub fn xorBytes(block_vec: Self, bytes: *const [blocks_count * 16]u8) [32]u8 {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].xorBytes(bytes[i * native_word_size ..][0..native_word_size]);
            }
            return out;
        }

        /// Apply the forward AES operation to the block vector with a vector of round keys.
        pub fn encrypt(block_vec: Self, round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].encrypt(round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the forward AES operation to the block vector with a vector of last round keys.
        pub fn encryptLast(block_vec: Self, round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].encryptLast(round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the inverse AES operation to the block vector with a vector of round keys.
        pub fn decrypt(block_vec: Self, inv_round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].decrypt(inv_round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the inverse AES operation to the block vector with a vector of last round keys.
        pub fn decryptLast(block_vec: Self, inv_round_key_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].decryptLast(inv_round_key_vec.repr[i]);
            }
            return out;
        }

        /// Apply the bitwise XOR operation to the content of two block vectors.
        pub fn xorBlocks(block_vec1: Self, block_vec2: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i].xorBlocks(block_vec2.repr[i]);
            }
            return out;
        }

        /// Apply the bitwise AND operation to the content of two block vectors.
        pub fn andBlocks(block_vec1: Self, block_vec2: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i].andBlocks(block_vec2.repr[i]);
            }
            return out;
        }

        /// Apply the bitwise OR operation to the content of two block vectors.
        pub fn orBlocks(block_vec1: Self, block_vec2: Block) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec1.repr[i].orBlocks(block_vec2.repr[i]);
            }
            return out;
        }

        /// Apply the inverse MixColumns operation to each block in the vector.
        pub fn invMixColumns(block_vec: Self) Self {
            var out: Self = undefined;
            inline for (0..native_words) |i| {
                out.repr[i] = block_vec.repr[i].invMixColumns();
            }
            return out;
        }
    };
}

fn KeySchedule(comptime Aes: type) type {
    std.debug.assert(Aes.rounds == 10 or Aes.rounds == 14);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();

        const Repr = Aes.block.Repr;

        const zero = @Vector(2, u64){ 0, 0 };
        const mask1 = @Vector(16, u8){ 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12 };
        const mask2 = @Vector(16, u8){ 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15, 12, 13, 14, 15 };

        round_keys: [rounds + 1]Block,

        fn drc128(comptime rc: u8, t: Repr) Repr {
            var v1: Repr = undefined;
            var v2: Repr = undefined;
            var v3: Repr = undefined;
            var v4: Repr = undefined;

            return asm (
                \\ movi %[v2].4s, %[rc]
                \\ tbl  %[v4].16b, {%[t].16b}, %[mask].16b
                \\ ext  %[r].16b, %[zero].16b, %[t].16b, #12
                \\ aese %[v4].16b, %[zero].16b
                \\ eor  %[v2].16b, %[r].16b, %[v2].16b
                \\ ext  %[r].16b, %[zero].16b, %[r].16b, #12
                \\ eor  %[v1].16b, %[v2].16b, %[t].16b
                \\ ext  %[v3].16b, %[zero].16b, %[r].16b, #12
                \\ eor  %[v1].16b, %[v1].16b, %[r].16b
                \\ eor  %[r].16b, %[v1].16b, %[v3].16b
                \\ eor  %[r].16b, %[r].16b, %[v4].16b
                : [r] "=&x" (-> Repr),
                  [v1] "=&x" (v1),
                  [v2] "=&x" (v2),
                  [v3] "=&x" (v3),
                  [v4] "=&x" (v4),
                : [rc] "N" (rc),
                  [t] "x" (t),
                  [zero] "x" (zero),
                  [mask] "x" (mask1),
            );
        }

        fn drc256(comptime second: bool, comptime rc: u8, t: Repr, tx: Repr) Repr {
            var v1: Repr = undefined;
            var v2: Repr = undefined;
            var v3: Repr = undefined;
            var v4: Repr = undefined;

            return asm (
                \\ movi %[v2].4s, %[rc]
                \\ tbl  %[v4].16b, {%[t].16b}, %[mask].16b
                \\ ext  %[r].16b, %[zero].16b, %[tx].16b, #12
                \\ aese %[v4].16b, %[zero].16b
                \\ eor  %[v1].16b, %[tx].16b, %[r].16b
                \\ ext  %[r].16b, %[zero].16b, %[r].16b, #12
                \\ eor  %[v1].16b, %[v1].16b, %[r].16b
                \\ ext  %[v3].16b, %[zero].16b, %[r].16b, #12
                \\ eor  %[v1].16b, %[v1].16b, %[v2].16b
                \\ eor  %[v1].16b, %[v1].16b, %[v3].16b
                \\ eor  %[r].16b, %[v1].16b, %[v4].16b
                : [r] "=&x" (-> Repr),
                  [v1] "=&x" (v1),
                  [v2] "=&x" (v2),
                  [v3] "=&x" (v3),
                  [v4] "=&x" (v4),
                : [rc] "N" (if (second) @as(u8, 0) else rc),
                  [t] "x" (t),
                  [tx] "x" (tx),
                  [zero] "x" (zero),
                  [mask] "x" (if (second) mask2 else mask1),
            );
        }

        fn expand128(t1: *Block) Self {
            var round_keys: [11]Block = undefined;
            const rcs = [_]u8{ 1, 2, 4, 8, 16, 32, 64, 128, 27, 54 };
            inline for (rcs, 0..) |rc, round| {
                round_keys[round] = t1.*;
                t1.repr = drc128(rc, t1.repr);
            }
            round_keys[rcs.len] = t1.*;
            return Self{ .round_keys = round_keys };
        }

        fn expand256(t1: *Block, t2: *Block) Self {
            var round_keys: [15]Block = undefined;
            const rcs = [_]u8{ 1, 2, 4, 8, 16, 32 };
            round_keys[0] = t1.*;
            inline for (rcs, 0..) |rc, round| {
                round_keys[round * 2 + 1] = t2.*;
                t1.repr = drc256(false, rc, t2.repr, t1.repr);
                round_keys[round * 2 + 2] = t1.*;
                t2.repr = drc256(true, rc, t1.repr, t2.repr);
            }
            round_keys[rcs.len * 2 + 1] = t2.*;
            t1.repr = drc256(false, 64, t2.repr, t1.repr);
            round_keys[rcs.len * 2 + 2] = t1.*;
            return Self{ .round_keys = round_keys };
        }

        /// Invert the key schedule.
        pub fn invert(key_schedule: Self) Self {
            const round_keys = &key_schedule.round_keys;
            var inv_round_keys: [rounds + 1]Block = undefined;
            inv_round_keys[0] = round_keys[rounds];
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                inv_round_keys[i] = Block{
                    .repr = asm (
                        \\ aesimc %[inv_rk].16b, %[rk].16b
                        : [inv_rk] "=x" (-> Repr),
                        : [rk] "x" (round_keys[rounds - i].repr),
                    ),
                };
            }
            inv_round_keys[rounds] = round_keys[0];
            return Self{ .round_keys = inv_round_keys };
        }
    };
}

/// A context to perform encryption using the standard AES key schedule.
pub fn AesEncryptCtx(comptime Aes: type) type {
    std.debug.assert(Aes.key_bits == 128 or Aes.key_bits == 256);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        pub const block = Aes.block;
        pub const block_length = block.block_length;
        key_schedule: KeySchedule(Aes),

        /// Create a new encryption context with the given key.
        pub fn init(key: [Aes.key_bits / 8]u8) Self {
            var t1 = Block.fromBytes(key[0..16]);
            const key_schedule = if (Aes.key_bits == 128) ks: {
                break :ks KeySchedule(Aes).expand128(&t1);
            } else ks: {
                var t2 = Block.fromBytes(key[16..32]);
                break :ks KeySchedule(Aes).expand256(&t1, &t2);
            };
            return Self{
                .key_schedule = key_schedule,
            };
        }

        /// Encrypt a single block.
        pub fn encrypt(ctx: Self, dst: *[16]u8, src: *const [16]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(src).xorBlocks(round_keys[0]);
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                t = t.encrypt(round_keys[i]);
            }
            t = t.encryptLast(round_keys[rounds]);
            dst.* = t.toBytes();
        }

        /// Encrypt+XOR a single block.
        pub fn xor(ctx: Self, dst: *[16]u8, src: *const [16]u8, counter: [16]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(&counter).xorBlocks(round_keys[0]);
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                t = t.encrypt(round_keys[i]);
            }
            t = t.encryptLast(round_keys[rounds]);
            dst.* = t.xorBytes(src);
        }

        /// Encrypt multiple blocks, possibly leveraging parallelization.
        pub fn encryptWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var ts: [count]Block = undefined;
            comptime var j = 0;
            inline while (j < count) : (j += 1) {
                ts[j] = Block.fromBytes(src[j * 16 .. j * 16 + 16][0..16]).xorBlocks(round_keys[0]);
            }
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                ts = Block.parallel.encryptWide(count, ts, round_keys[i]);
            }
            ts = Block.parallel.encryptLastWide(count, ts, round_keys[i]);
            j = 0;
            inline while (j < count) : (j += 1) {
                dst[16 * j .. 16 * j + 16].* = ts[j].toBytes();
            }
        }

        /// Encrypt+XOR multiple blocks, possibly leveraging parallelization.
        pub fn xorWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8, counters: [16 * count]u8) void {
            const round_keys = ctx.key_schedule.round_keys;
            var ts: [count]Block = undefined;
            comptime var j = 0;
            inline while (j < count) : (j += 1) {
                ts[j] = Block.fromBytes(counters[j * 16 .. j * 16 + 16][0..16]).xorBlocks(round_keys[0]);
            }
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                ts = Block.parallel.encryptWide(count, ts, round_keys[i]);
            }
            ts = Block.parallel.encryptLastWide(count, ts, round_keys[i]);
            j = 0;
            inline while (j < count) : (j += 1) {
                dst[16 * j .. 16 * j + 16].* = ts[j].xorBytes(src[16 * j .. 16 * j + 16]);
            }
        }
    };
}

/// A context to perform decryption using the standard AES key schedule.
pub fn AesDecryptCtx(comptime Aes: type) type {
    std.debug.assert(Aes.key_bits == 128 or Aes.key_bits == 256);
    const rounds = Aes.rounds;

    return struct {
        const Self = @This();
        pub const block = Aes.block;
        pub const block_length = block.block_length;
        key_schedule: KeySchedule(Aes),

        /// Create a decryption context from an existing encryption context.
        pub fn initFromEnc(ctx: AesEncryptCtx(Aes)) Self {
            return Self{
                .key_schedule = ctx.key_schedule.invert(),
            };
        }

        /// Create a new decryption context with the given key.
        pub fn init(key: [Aes.key_bits / 8]u8) Self {
            const enc_ctx = AesEncryptCtx(Aes).init(key);
            return initFromEnc(enc_ctx);
        }

        /// Decrypt a single block.
        pub fn decrypt(ctx: Self, dst: *[16]u8, src: *const [16]u8) void {
            const inv_round_keys = ctx.key_schedule.round_keys;
            var t = Block.fromBytes(src).xorBlocks(inv_round_keys[0]);
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                t = t.decrypt(inv_round_keys[i]);
            }
            t = t.decryptLast(inv_round_keys[rounds]);
            dst.* = t.toBytes();
        }

        /// Decrypt multiple blocks, possibly leveraging parallelization.
        pub fn decryptWide(ctx: Self, comptime count: usize, dst: *[16 * count]u8, src: *const [16 * count]u8) void {
            const inv_round_keys = ctx.key_schedule.round_keys;
            var ts: [count]Block = undefined;
            comptime var j = 0;
            inline while (j < count) : (j += 1) {
                ts[j] = Block.fromBytes(src[j * 16 .. j * 16 + 16][0..16]).xorBlocks(inv_round_keys[0]);
            }
            comptime var i = 1;
            inline while (i < rounds) : (i += 1) {
                ts = Block.parallel.decryptWide(count, ts, inv_round_keys[i]);
            }
            ts = Block.parallel.decryptLastWide(count, ts, inv_round_keys[i]);
            j = 0;
            inline while (j < count) : (j += 1) {
                dst[16 * j .. 16 * j + 16].* = ts[j].toBytes();
            }
        }
    };
}

/// AES-128 with the standard key schedule.
pub const Aes128 = struct {
    pub const key_bits: usize = 128;
    pub const rounds = ((key_bits - 64) / 32 + 8);
    pub const block = Block;

    /// Create a new context for encryption.
    pub fn initEnc(key: [key_bits / 8]u8) AesEncryptCtx(Aes128) {
        return AesEncryptCtx(Aes128).init(key);
    }

    /// Create a new context for decryption.
    pub fn initDec(key: [key_bits / 8]u8) AesDecryptCtx(Aes128) {
        return AesDecryptCtx(Aes128).init(key);
    }
};

/// AES-256 with the standard key schedule.
pub const Aes256 = struct {
    pub const key_bits: usize = 256;
    pub const rounds = ((key_bits - 64) / 32 + 8);
    pub const block = Block;

    /// Create a new context for encryption.
    pub fn initEnc(key: [key_bits / 8]u8) AesEncryptCtx(Aes256) {
        return AesEncryptCtx(Aes256).init(key);
    }

    /// Create a new context for decryption.
    pub fn initDec(key: [key_bits / 8]u8) AesDecryptCtx(Aes256) {
        return AesDecryptCtx(Aes256).init(key);
    }
};



---
File: /std/crypto/aes/soft.zig
---

const std = @import("../../std.zig");
const math = std.math;
const mem = std.mem;

const side_channels_mitigations = std.options.side_channels_mitigations;

/// A single AES block.
pub const Block = struct {
    const Repr = [4]u32;

    pub const block_length: usize = 16;

    /// Internal representation of a block.
    repr: Repr align(16),

    /// Convert a byte sequence into an internal representation.
    pub fn fromBytes(bytes: *const [16]u8) Block {
        const s0 = mem.readInt(u32, bytes[0..4], .little);
        const s1 = mem.readInt(u32, bytes[4..8], .little);
        const s2 = mem.readInt(u32, bytes[8..12], .little);
        const s3 = mem.readInt(u32, bytes[12..16], .little);
        return Block{ .repr = Repr{ s0, s1, s2, s3 } };
    }

    /// Convert the internal representation of a block into a byte sequence.
    pub fn toBytes(block: Block) [16]u8 {
        var bytes: [16]u8 = undefined;
        mem.writeInt(u32, bytes[0..4], block.repr[0], .little);
        mem.writeInt(u32, bytes[4..8], block.repr[1], .little);
        mem.writeInt(u32, bytes[8..12], block.repr[2], .little);
        mem.writeInt(u32, bytes[12..16], block.repr[3], .little);
        return bytes;
    }

    /// XOR the block with a byte sequence.
    pub fn xorBytes(block: Block, bytes: *const [16]u8) [16]u8 {
        const block_bytes = block.toBytes();
        var x: [16]u8 = undefined;
        comptime var i: usize = 0;
        inline while (i < 16) : (i += 1) {
            x[i] = block_bytes[i] ^ bytes[i];
        }
        return x;
    }

    /// Encrypt a block with a round key.
    pub fn encrypt(block: Block, round_key: Block) Block {
        const s0 = block.repr[0];
        const s1 = block.repr[1];
        const s2 = block.repr[2];
        const s3 = block.repr[3];

        var x: [4]u32 = undefined;
        x = table_lookup(&table_encrypt, @as(u8, @truncate(s0)), @as(u8, @truncate(s1 >> 8)), @as(u8, @truncate(s2 >> 16)), @as(u8, @truncate(s3 >> 24)));
        var t0 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = table_lookup(&table_encrypt, @as(u8, @truncate(s1)), @as(u8, @truncate(s2 >> 8)), @as(u8, @truncate(s3 >> 16)), @as(u8, @truncate(s0 >> 24)));
        var t1 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = table_lookup(&table_encrypt, @as(u8, @truncate(s2)), @as(u8, @truncate(s3 >> 8)), @as(u8, @truncate(s0 >> 16)), @as(u8, @truncate(s1 >> 24)));
        var t2 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = table_lookup(&table_encrypt, @as(u8, @truncate(s3)), @as(u8, @truncate(s0 >> 8)), @as(u8, @truncate(s1 >> 16)), @as(u8, @truncate(s2 >> 24)));
        var t3 = x[0] ^ x[1] ^ x[2] ^ x[3];

        t0 ^= round_key.repr[0];
        t1 ^= round_key.repr[1];
        t2 ^= round_key.repr[2];
        t3 ^= round_key.repr[3];

        return Block{ .repr = Repr{ t0, t1, t2, t3 } };
    }

    /// Encrypt a block with a round key *WITHOUT ANY PROTECTION AGAINST SIDE CHANNELS*
    pub fn encryptUnprotected(block: Block, round_key: Block) Block {
        const s0 = block.repr[0];
        const s1 = block.repr[1];
        const s2 = block.repr[2];
        const s3 = block.repr[3];

        var x: [4]u32 = undefined;
        x = .{
            table_encrypt[0][@as(u8, @truncate(s0))],
            table_encrypt[1][@as(u8, @truncate(s1 >> 8))],
            table_encrypt[2][@as(u8, @truncate(s2 >> 16))],
            table_encrypt[3][@as(u8, @truncate(s3 >> 24))],
        };
        var t0 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = .{
            table_encrypt[0][@as(u8, @truncate(s1))],
            table_encrypt[1][@as(u8, @truncate(s2 >> 8))],
            table_encrypt[2][@as(u8, @truncate(s3 >> 16))],
            table_encrypt[3][@as(u8, @truncate(s0 >> 24))],
        };
        var t1 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = .{
            table_encrypt[0][@as(u8, @truncate(s2))],
            table_encrypt[1][@as(u8, @truncate(s3 >> 8))],
            table_encrypt[2][@as(u8, @truncate(s0 >> 16))],
            table_encrypt[3][@as(u8, @truncate(s1 >> 24))],
        };
        var t2 = x[0] ^ x[1] ^ x[2] ^ x[3];
        x = .{
            table_encrypt[0][@as(u8, @truncate(s3))],
            table_encrypt[1][@as(u8, @truncate(s0 >> 8))],
            table_encrypt[2][@as(u8, @truncate(s1 >> 16))],
            table_encrypt[3][@as(u8, @truncate(s2 >> 24))],
        };
        var t3 = x[0] ^ x[1] ^ x[2] ^ x[3];

        t0 ^= round_key.repr[0];
        t1 ^= round_key.repr[1];
        t2 ^= round_key.repr[2];
        t3 ^= round_key.repr[3];

        return Block{ .repr = Repr{ t0, t1, t2, t3 } };
    }

    /// Encrypt a block with the last round key.
    pub fn encryptLast(block: Block, round_key: Block) Block {
        const s0 = block.repr[0];
        const s1 = block.repr[1];
        const s2 = block.repr[2];
        const s3 = block.repr[3];

        // Last round uses s-box directly and XORs to produce output.
        var x: [4]u8 = undefined;
        x = sbox_lookup(&sbox_encrypt, @as(u8, @truncate(s0)), @as(u8, @truncate(s1 >> 8)), @as(u8, @truncate(s2 >> 16)), @as(u8, @truncate(s3 >> 24)));
        var t0 = mem.readInt(u32, &x, .little);
        x = sbox_lookup(&sbox_encrypt, @as(u8, @truncate(s1)), @as(u8, @truncate(s2 >> 8)), @as(u8, @truncate(s3 >> 16)), @as(u8, @truncate(s0 >> 24)));
        var t1 = mem.readInt(u32, &x, .little);
        x = sbox_lookup(&sbox_encrypt, @as(u8, @truncate(s2)), @as(u8, @truncate(s3 >> 8)), @as(u8,
```
