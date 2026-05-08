```
ng.expectApproxEqAbs(0x1.134p1, acosBinary16(-0x1.18cp-1), math.floatEpsAt(f16, 0x1.134p1));
    try testing.expectApproxEqAbs(0x1.0dp1, acosBinary16(-0x1.03p-1), math.floatEpsAt(f16, 0x1.0dp1));
}

test "acosBinary32.special" {
    try testing.expectApproxEqAbs(0x1.921fb6p+0, acosBinary32(0x0p+0), math.floatEpsAt(f32, 0x1.921fb6p+0));
    try testing.expectApproxEqAbs(0x1.921fb6p+1, acosBinary32(-0x1p+0), math.floatEpsAt(f32, 0x1.921fb6p+1));
    try testing.expectEqual(0x0p+0, acosBinary32(0x1p+0));
    try testing.expect(math.isNan(acosBinary32(0x1.000002p+0)));
    try testing.expect(math.isNan(acosBinary32(-0x1.000002p+0)));
    try testing.expect(math.isNan(acosBinary32(math.inf(f32))));
    try testing.expect(math.isNan(acosBinary32(-math.inf(f32))));
    try testing.expect(math.isNan(acosBinary32(math.nan(f32))));
}

test "acosBinary32" {
    try testing.expectApproxEqAbs(0x1.d7c4e6p+0, acosBinary32(-0x1.13284cp-2), math.floatEpsAt(f32, 0x1.d7c4e6p+0));
    try testing.expectApproxEqAbs(0x1.8e6756p-1, acosBinary32(0x1.6ca8ep-1), math.floatEpsAt(f32, 0x1.8e6756p-1));
    try testing.expectApproxEqAbs(0x1.f9d74cp-2, acosBinary32(0x1.c2ca6p-1), math.floatEpsAt(f32, 0x1.f9d74cp-2));
    try testing.expectApproxEqAbs(0x1.26abdcp+1, acosBinary32(-0x1.55f12p-1), math.floatEpsAt(f32, 0x1.26abdcp+1));
    try testing.expectApproxEqAbs(0x1.d85a44p+0, acosBinary32(-0x1.15679ep-2), math.floatEpsAt(f32, 0x1.d85a44p+0));
    try testing.expectApproxEqAbs(0x1.9c2f68p+0, acosBinary32(-0x1.41e132p-5), math.floatEpsAt(f32, 0x1.9c2f68p+0));
    try testing.expectApproxEqAbs(0x1.e881bp-1, acosBinary32(0x1.281b0ep-1), math.floatEpsAt(f32, 0x1.e881bp-1));
    try testing.expectApproxEqAbs(0x1.1713f6p-1, acosBinary32(0x1.b5ce34p-1), math.floatEpsAt(f32, 0x1.1713f6p-1));
    try testing.expectApproxEqAbs(0x1.bd5accp+0, acosBinary32(-0x1.583482p-3), math.floatEpsAt(f32, 0x1.bd5accp+0));
    try testing.expectApproxEqAbs(0x1.6ce7d8p+1, acosBinary32(-0x1.ea8224p-1), math.floatEpsAt(f32, 0x1.6ce7d8p+1));
}

test "acosBinary64.special" {
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+0, acosBinary64(0x0p+0), math.floatEpsAt(f64, 0x1.921fb54442d18p+0));
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+1, acosBinary64(-0x1p+0), math.floatEpsAt(f64, 0x1.921fb54442d18p+1));
    try testing.expectEqual(0x0p+0, acosBinary64(0x1p+0));
    try testing.expect(math.isNan(acosBinary64(0x1.0000000000001p+0)));
    try testing.expect(math.isNan(acosBinary64(-0x1.0000000000001p+0)));
    try testing.expect(math.isNan(acosBinary64(math.inf(f64))));
    try testing.expect(math.isNan(acosBinary64(-math.inf(f64))));
    try testing.expect(math.isNan(acosBinary64(math.nan(f64))));
}

test "acosBinary64" {
    try testing.expectApproxEqAbs(0x1.d7c4e61020905p+0, acosBinary64(-0x1.13284b2b5006dp-2), math.floatEpsAt(f64, 0x1.d7c4e61020905p+0));
    try testing.expectApproxEqAbs(0x1.8e6756e27c366p-1, acosBinary64(0x1.6ca8dfb825911p-1), math.floatEpsAt(f64, 0x1.8e6756e27c366p-1));
    try testing.expectApproxEqAbs(0x1.f9d748eaf956p-2, acosBinary64(0x1.c2ca609de7505p-1), math.floatEpsAt(f64, 0x1.f9d748eaf956p-2));
    try testing.expectApproxEqAbs(0x1.26abdc68d07aap+1, acosBinary64(-0x1.55f11fba96889p-1), math.floatEpsAt(f64, 0x1.26abdc68d07aap+1));
    try testing.expectApproxEqAbs(0x1.d85a44ea44fe4p+0, acosBinary64(-0x1.15679e27084ddp-2), math.floatEpsAt(f64, 0x1.d85a44ea44fe4p+0));
    try testing.expectApproxEqAbs(0x1.9c2f688eee8abp+0, acosBinary64(-0x1.41e131b093c41p-5), math.floatEpsAt(f64, 0x1.9c2f688eee8abp+0));
    try testing.expectApproxEqAbs(0x1.e881b1d4eb2a1p-1, acosBinary64(0x1.281b0d18455f5p-1), math.floatEpsAt(f64, 0x1.e881b1d4eb2a1p-1));
    try testing.expectApproxEqAbs(0x1.1713f567a87efp-1, acosBinary64(0x1.b5ce34a51b239p-1), math.floatEpsAt(f64, 0x1.1713f567a87efp-1));
    try testing.expectApproxEqAbs(0x1.bd5acbe8fcc59p+0, acosBinary64(-0x1.583481079de4dp-3), math.floatEpsAt(f64, 0x1.bd5acbe8fcc59p+0));
    try testing.expectApproxEqAbs(0x1.6ce7d66f628e5p+1, acosBinary64(-0x1.ea8223103b871p-1), math.floatEpsAt(f64, 0x1.6ce7d66f628e5p+1));
}

test "acosExtended80.special" {
    try testing.expectApproxEqAbs(0x1.921fb54442d1846ap+0, acosExtended80(0x0p+0), math.floatEpsAt(f80, 0x1.921fb54442d1846ap+0));
    try testing.expectApproxEqAbs(0x1.921fb54442d1846ap+1, acosExtended80(-0x1p+0), math.floatEpsAt(f80, 0x1.921fb54442d1846ap+1));
    try testing.expectEqual(0x0p+0, acosExtended80(0x1p+0));
    try testing.expect(math.isNan(acosExtended80(0x1.0000000000000002p+0)));
    try testing.expect(math.isNan(acosExtended80(-0x1.0000000000000002p+0)));
    try testing.expect(math.isNan(acosExtended80(math.inf(f80))));
    try testing.expect(math.isNan(acosExtended80(-math.inf(f80))));
    try testing.expect(math.isNan(acosExtended80(math.nan(f80))));
}

test "acosExtended80" {
    try testing.expectApproxEqAbs(0x1.86b349040d28f794p-1, acosExtended80(0x1.72068a321edc8804p-1), math.floatEpsAt(f80, 0x1.86b349040d28f794p-1));
    try testing.expectApproxEqAbs(0x1.d4923ade73ec379cp0, acosExtended80(-0x1.06d0a467d22977ecp-2), math.floatEpsAt(f80, 0x1.d4923ade73ec379cp0));
    try testing.expectApproxEqAbs(0x1.62e0e8898c6d04f2p0, acosExtended80(0x1.77d21385faa9798ap-3), math.floatEpsAt(f80, 0x1.62e0e8898c6d04f2p0));
    try testing.expectApproxEqAbs(0x1.3123cbcd5dc4bd58p1, acosExtended80(-0x1.73ee3e8bc2a44dbep-1), math.floatEpsAt(f80, 0x1.3123cbcd5dc4bd58p1));
    try testing.expectApproxEqAbs(0x1.062a6d562df2d316p0, acosExtended80(0x1.0a2dd1f6ffcf668ap-1), math.floatEpsAt(f80, 0x1.062a6d562df2d316p0));
    try testing.expectApproxEqAbs(0x1.5ffd68b520aa55fap0, acosExtended80(0x1.8e835c490a3aff9ep-3), math.floatEpsAt(f80, 0x1.5ffd68b520aa55fap0));
    try testing.expectApproxEqAbs(0x1.5bfe6cabda700684p0, acosExtended80(0x1.add20cdc1565064cp-3), math.floatEpsAt(f80, 0x1.5bfe6cabda700684p0));
    try testing.expectApproxEqAbs(0x1.90fe1c993b571924p0, acosExtended80(0x1.21986d43727fca72p-8), math.floatEpsAt(f80, 0x1.90fe1c993b571924p0));
    try testing.expectApproxEqAbs(0x1.18044ccc626e7f9ep0, acosExtended80(0x1.d61e0b3fae6a0564p-2), math.floatEpsAt(f80, 0x1.18044ccc626e7f9ep0));
    try testing.expectApproxEqAbs(0x1.a39513b6c16532b4p0, acosExtended80(-0x1.171e7c4a41883ccap-4), math.floatEpsAt(f80, 0x1.a39513b6c16532b4p0));
}

test "acosBinary128.special" {
    try testing.expectApproxEqAbs(0x1.921fb54442d18469898cc51701b8p0, acosBinary128(0x0p+0), math.floatEpsAt(f128, 0x1.921fb54442d18469898cc51701b8p0));
    try testing.expectApproxEqAbs(0x1.921fb54442d18469898cc51701b8p1, acosBinary128(-0x1p+0), math.floatEpsAt(f128, 0x1.921fb54442d18469898cc51701b8p1));
    try testing.expectEqual(0x0p+0, acosBinary128(0x1p+0));
    try testing.expect(math.isNan(acosBinary128(0x1.0000000000000000000000000001p0)));
    try testing.expect(math.isNan(acosBinary128(-0x1.0000000000000000000000000001p0)));
    try testing.expect(math.isNan(acosBinary128(math.inf(f128))));
    try testing.expect(math.isNan(acosBinary128(-math.inf(f128))));
    try testing.expect(math.isNan(acosBinary128(math.nan(f128))));
}

test "acosBinary128" {
    try testing.expectApproxEqAbs(0x1.250e9a58f049eeafa99db4360c88p1, acosBinary128(-0x1.511bdb99a3c4373bedf834ef4f68p-1), math.floatEpsAt(f128, 0x1.250e9a58f049eeafa99db4360c88p1));
    try testing.expectApproxEqAbs(0x1.2786664b1c676c99437b68590004p1, acosBinary128(-0x1.5879cc3ad6dfd2a52e9891c69808p-1), math.floatEpsAt(f128, 0x1.2786664b1c676c99437b68590004p1));
    try testing.expectApproxEqAbs(0x1.cb190cd361c7c03a09c470b4caebp-1, acosBinary128(0x1.3f988ba64a7eb97a751c5f0b3077p-1), math.floatEpsAt(f128, 0x1.cb190cd361c7c03a09c470b4caebp-1));
    try testing.expectApproxEqAbs(0x1.1f373be697880111758f582b1a96p1, acosBinary128(-0x1.3f2d96c7768e4c4fa02315727959p-1), math.floatEpsAt(f128, 0x1.1f373be697880111758f582b1a96p1));
    try testing.expectApproxEqAbs(0x1.0d92fd2a0a6ca3e4853c1de9ea6ap0, acosBinary128(0x1.fad303c2e28c1f4d8f9fd0e5686fp-2), math.floatEpsAt(f128, 0x1.0d92fd2a0a6ca3e4853c1de9ea6ap0));
    try testing.expectApproxEqAbs(0x1.15d4b306e16fbf9ea4f29e82b154p0, acosBinary128(0x1.ddde322bd1a2ee50c5ba30c9c617p-2), math.floatEpsAt(f128, 0x1.15d4b306e16fbf9ea4f29e82b154p0));
    try testing.expectApproxEqAbs(0x1.49b0a0355a5539052388e8a6dc11p1, acosBinary128(-0x1.b02f6adefcbeb1d48666b827ff17p-1), math.floatEpsAt(f128, 0x1.49b0a0355a5539052388e8a6dc11p1));
    try testing.expectApproxEqAbs(0x1.1be0b757f4cef022f5d2422b9c78p0, acosBinary128(0x1.c8581cce7cd3f6efab0fc60d9b7dp-2), math.floatEpsAt(f128, 0x1.1be0b757f4cef022f5d2422b9c78p0));
    try testing.expectApproxEqAbs(0x1.513270e671db2d840f20b0186c2cp1, acosBinary128(-0x1.bf887b8c4e33cbef59993056f3dep-1), math.floatEpsAt(f128, 0x1.513270e671db2d840f20b0186c2cp1));
    try testing.expectApproxEqAbs(0x1.70851a509f0e8bfbe780aa8f29f9p0, acosBinary128(0x1.0c0f600ab6f9c84c6102942044cep-3), math.floatEpsAt(f128, 0x1.70851a509f0e8bfbe780aa8f29f9p0));
}

fn acosBinary32Vec(comptime vec_len: comptime_int, x: @Vector(vec_len, f32)) @TypeOf(x) {
    const pi: @Vector(vec_len, f32) = @splat(math.pi);
    const pi_over_2: @Vector(vec_len, f32) = @splat(math.pi / 2.0);
    const zero: @Vector(vec_len, f32) = @splat(0.0);
    const half: @Vector(vec_len, f32) = @splat(0.5);
    const neg_one: @Vector(vec_len, f32) = @splat(-1.0);
    const two: @Vector(vec_len, f32) = @splat(2.0);
    const c0: @Vector(vec_len, f32) = @splat(0x1.55555ep-3);
    const c1: @Vector(vec_len, f32) = @splat(0x1.33261ap-4);
    const c2: @Vector(vec_len, f32) = @splat(0x1.70d7dcp-5);
    const c3: @Vector(vec_len, f32) = @splat(0x1.b059dp-6);
    const c4: @Vector(vec_len, f32) = @splat(0x1.3af7d8p-5);

    const ax = @abs(x);
    const ax_lt_half = ax < half;
    const is_neg = x < zero;
    const z2 = @select(f32, ax_lt_half, x * x, @mulAdd(@Vector(vec_len, f32), -half, ax, half));
    const z = @select(f32, ax_lt_half, ax, @sqrt(z2));
    const z3 = z2 * z;
    const p3_4 = @mulAdd(@Vector(vec_len, f32), z2, c4, c3);
    const p2_4 = @mulAdd(@Vector(vec_len, f32), z2, p3_4, c2);
    const p1_4 = @mulAdd(@Vector(vec_len, f32), z2, p2_4, c1);
    const p0_4 = @mulAdd(@Vector(vec_len, f32), z2, p1_4, c0);
    const p = @mulAdd(@Vector(vec_len, f32), z3, p0_4, z);
    const mul = @select(f32, ax_lt_half, neg_one, two);
    const add = @select(f32, ax_lt_half, pi_over_2, @select(f32, is_neg, pi, zero));
    return @mulAdd(@Vector(vec_len, f32), mul, @select(f32, is_neg, -p, p), add);
}

fn acosBinary64Vec(comptime vec_len: comptime_int, x: @Vector(vec_len, f64)) @TypeOf(x) {
    const pi: @Vector(vec_len, f64) = @splat(math.pi);
    const pi_over_2: @Vector(vec_len, f64) = @splat(math.pi / 2.0);
    const zero: @Vector(vec_len, f64) = @splat(0.0);
    const half: @Vector(vec_len, f64) = @splat(0.5);
    const neg_one: @Vector(vec_len, f64) = @splat(-1.0);
    const two: @Vector(vec_len, f64) = @splat(2.0);
    const c0: @Vector(vec_len, f64) = @splat(0x1.555555555554ep-3);
    const c1: @Vector(vec_len, f64) = @splat(0x1.3333333337233p-4);
    const c2: @Vector(vec_len, f64) = @splat(0x1.6db6db67f6d9fp-5);
    const c3: @Vector(vec_len, f64) = @splat(0x1.f1c71fbd29fbbp-6);
    const c4: @Vector(vec_len, f64) = @splat(0x1.6e8b264d467d6p-6);
    const c5: @Vector(vec_len, f64) = @splat(0x1.1c5997c357e9dp-6);
    const c6: @Vector(vec_len, f64) = @splat(0x1.c86a22cd9389dp-7);
    const c7: @Vector(vec_len, f64) = @splat(0x1.856073c22ebbep-7);
    const c8: @Vector(vec_len, f64) = @splat(0x1.fd1151acb6bedp-8);
    const c9: @Vector(vec_len, f64) = @splat(0x1.087182f799c1dp-6);
    const c10: @Vector(vec_len, f64) = @splat(-0x1.6602748120927p-7);
    const c11: @Vector(vec_len, f64) = @splat(0x1.cfa0dd1f9478p-6);

    const ax = @abs(x);
    const ax_lt_half = ax < half;
    const is_neg = x < zero;
    const z2 = @select(f64, ax_lt_half, x * x, @mulAdd(@Vector(vec_len, f64), -half, ax, half));
    const z = @select(f64, ax_lt_half, ax, @sqrt(z2));
    const z3 = z2 * z;
    const z4 = z2 * z2;
    const z8 = z4 * z4;
    const p0_1 = @mulAdd(@Vector(vec_len, f64), z2, c1, c0);
    const p2_3 = @mulAdd(@Vector(vec_len, f64), z2, c3, c2);
    const p0_3 = @mulAdd(@Vector(vec_len, f64), z4, p2_3, p0_1);
    const p4_5 = @mulAdd(@Vector(vec_len, f64), z2, c5, c4);
    const p6_7 = @mulAdd(@Vector(vec_len, f64), z2, c7, c6);
    const p4_7 = @mulAdd(@Vector(vec_len, f64), z4, p6_7, p4_5);
    const p8_9 = @mulAdd(@Vector(vec_len, f64), z2, c9, c8);
    const p10_11 = @mulAdd(@Vector(vec_len, f64), z2, c11, c10);
    const p8_11 = @mulAdd(@Vector(vec_len, f64), z4, p10_11, p8_9);
    const p4_11 = @mulAdd(@Vector(vec_len, f64), z8, p8_11, p4_7);
    const p0_11 = @mulAdd(@Vector(vec_len, f64), z8, p4_11, p0_3);
    const p = @mulAdd(@Vector(vec_len, f64), z3, p0_11, z);
    const mul = @select(f64, ax_lt_half, neg_one, two);
    const add = @select(f64, ax_lt_half, pi_over_2, @select(f64, is_neg, pi, zero));
    return @mulAdd(@Vector(vec_len, f64), mul, @select(f64, is_neg, -p, p), add);
}

test "acosBinary32Vec.special" {
    const input: @Vector(8, f32) = .{
        0x0p+0,
        -0x1p+0,
        0x1p+0,
        0x1.000002p+0,
        -0x1.000002p+0,
        math.inf(f32),
        -math.inf(f32),
        math.nan(f32),
    };
    const output = acosBinary32Vec(8, input);
    try testing.expectApproxEqAbs(0x1.921fb6p+0, output[0], math.floatEpsAt(f32, 0x1.921fb6p+0));
    try testing.expectApproxEqAbs(0x1.921fb6p+1, output[1], math.floatEpsAt(f32, 0x1.921fb6p+1));
    try testing.expectEqual(0x0p+0, output[2]);
    try testing.expect(math.isNan(output[3]));
    try testing.expect(math.isNan(output[4]));
    try testing.expect(math.isNan(output[5]));
    try testing.expect(math.isNan(output[6]));
    try testing.expect(math.isNan(output[7]));
}

test "acosBinary32Vec" {
    const input: @Vector(10, f32) = .{
        -0x1.13284cp-2,
        0x1.6ca8ep-1,
        0x1.c2ca6p-1,
        -0x1.55f12p-1,
        -0x1.15679ep-2,
        -0x1.41e132p-5,
        0x1.281b0ep-1,
        0x1.b5ce34p-1,
        -0x1.583482p-3,
        -0x1.ea8224p-1,
    };
    const output = acosBinary32Vec(10, input);
    try testing.expectApproxEqAbs(0x1.d7c4e6p+0, output[0], math.floatEpsAt(f32, 0x1.d7c4e6p+0));
    try testing.expectApproxEqAbs(0x1.8e6756p-1, output[1], math.floatEpsAt(f32, 0x1.8e6756p-1));
    try testing.expectApproxEqAbs(0x1.f9d74cp-2, output[2], math.floatEpsAt(f32, 0x1.f9d74cp-2));
    try testing.expectApproxEqAbs(0x1.26abdcp+1, output[3], math.floatEpsAt(f32, 0x1.26abdcp+1));
    try testing.expectApproxEqAbs(0x1.d85a44p+0, output[4], math.floatEpsAt(f32, 0x1.d85a44p+0));
    try testing.expectApproxEqAbs(0x1.9c2f68p+0, output[5], math.floatEpsAt(f32, 0x1.9c2f68p+0));
    try testing.expectApproxEqAbs(0x1.e881bp-1, output[6], math.floatEpsAt(f32, 0x1.e881bp-1));
    try testing.expectApproxEqAbs(0x1.1713f6p-1, output[7], math.floatEpsAt(f32, 0x1.1713f6p-1));
    try testing.expectApproxEqAbs(0x1.bd5accp+0, output[8], math.floatEpsAt(f32, 0x1.bd5accp+0));
    try testing.expectApproxEqAbs(0x1.6ce7d8p+1, output[9], math.floatEpsAt(f32, 0x1.6ce7d8p+1));
}

test "acosBinary64Vec.special" {
    const input: @Vector(8, f64) = .{
        0x0p+0,
        -0x1p+0,
        0x1p+0,
        0x1.0000000000001p+0,
        -0x1.0000000000001p+0,
        math.inf(f64),
        -math.inf(f64),
        math.nan(f64),
    };
    const output = acosBinary64Vec(8, input);
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+0, output[0], math.floatEpsAt(f64, 0x1.921fb54442d18p+0));
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+1, output[1], math.floatEpsAt(f64, 0x1.921fb54442d18p+1));
    try testing.expectEqual(0x0p+0, output[2]);
    try testing.expect(math.isNan(output[3]));
    try testing.expect(math.isNan(output[4]));
    try testing.expect(math.isNan(output[5]));
    try testing.expect(math.isNan(output[6]));
    try testing.expect(math.isNan(output[7]));
}

test "acosBinary64Vec" {
    const input: @Vector(10, f64) = .{
        -0x1.13284b2b5006dp-2,
        0x1.6ca8dfb825911p-1,
        0x1.c2ca609de7505p-1,
        -0x1.55f11fba96889p-1,
        -0x1.15679e27084ddp-2,
        -0x1.41e131b093c41p-5,
        0x1.281b0d18455f5p-1,
        0x1.b5ce34a51b239p-1,
        -0x1.583481079de4dp-3,
        -0x1.ea8223103b871p-1,
    };
    const output = acosBinary64Vec(10, input);
    try testing.expectApproxEqAbs(0x1.d7c4e61020905p+0, output[0], math.floatEpsAt(f64, 0x1.d7c4e61020905p+0));
    try testing.expectApproxEqAbs(0x1.8e6756e27c366p-1, output[1], math.floatEpsAt(f64, 0x1.8e6756e27c366p-1));
    try testing.expectApproxEqAbs(0x1.f9d748eaf956p-2, output[2], math.floatEpsAt(f64, 0x1.f9d748eaf956p-2));
    try testing.expectApproxEqAbs(0x1.26abdc68d07aap+1, output[3], math.floatEpsAt(f64, 0x1.26abdc68d07aap+1));
    try testing.expectApproxEqAbs(0x1.d85a44ea44fe4p+0, output[4], math.floatEpsAt(f64, 0x1.d85a44ea44fe4p+0));
    try testing.expectApproxEqAbs(0x1.9c2f688eee8abp+0, output[5], math.floatEpsAt(f64, 0x1.9c2f688eee8abp+0));
    try testing.expectApproxEqAbs(0x1.e881b1d4eb2a1p-1, output[6], math.floatEpsAt(f64, 0x1.e881b1d4eb2a1p-1));
    try testing.expectApproxEqAbs(0x1.1713f567a87efp-1, output[7], math.floatEpsAt(f64, 0x1.1713f567a87efp-1));
    try testing.expectApproxEqAbs(0x1.bd5acbe8fcc59p+0, output[8], math.floatEpsAt(f64, 0x1.bd5acbe8fcc59p+0));
    try testing.expectApproxEqAbs(0x1.6ce7d66f628e5p+1, output[9], math.floatEpsAt(f64, 0x1.6ce7d66f628e5p+1));
}



---
File: /std/math/acosh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/acoshf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/acosh.c

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns the hyperbolic arc-cosine of x.
///
/// Special cases:
///  - acosh(x)   = nan if x < 1
///  - acosh(nan) = nan
pub fn acosh(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => acosh32(x),
        f64 => acosh64(x),
        else => @compileError("acosh not implemented for " ++ @typeName(T)),
    };
}

// acosh(x) = log(x + sqrt(x * x - 1))
fn acosh32(x: f32) f32 {
    const u = @as(u32, @bitCast(x));
    const i = u & 0x7FFFFFFF;

    // |x| < 2, invalid if x < 1 or nan
    if (i < 0x3F800000 + (1 << 23)) {
        return math.log1p(x - 1 + @sqrt((x - 1) * (x - 1) + 2 * (x - 1)));
    }
    // |x| < 0x1p12
    else if (i < 0x3F800000 + (12 << 23)) {
        return @log(2 * x - 1 / (x + @sqrt(x * x - 1)));
    }
    // |x| >= 0x1p12
    else {
        return @log(x) + 0.693147180559945309417232121458176568;
    }
}

fn acosh64(x: f64) f64 {
    const u = @as(u64, @bitCast(x));
    const e = (u >> 52) & 0x7FF;

    // |x| < 2, invalid if x < 1 or nan
    if (e < 0x3FF + 1) {
        return math.log1p(x - 1 + @sqrt((x - 1) * (x - 1) + 2 * (x - 1)));
    }
    // |x| < 0x1p26
    else if (e < 0x3FF + 26) {
        return @log(2 * x - 1 / (x + @sqrt(x * x - 1)));
    }
    // |x| >= 0x1p26 or nan
    else {
        return @log(x) + 0.693147180559945309417232121458176568;
    }
}

test acosh {
    try expect(acosh(@as(f32, 1.5)) == acosh32(1.5));
    try expect(acosh(@as(f64, 1.5)) == acosh64(1.5));
}

test acosh32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, acosh32(1.5), 0.962424, epsilon));
    try expect(math.approxEqAbs(f32, acosh32(37.45), 4.315976, epsilon));
    try expect(math.approxEqAbs(f32, acosh32(89.123), 5.183133, epsilon));
    try expect(math.approxEqAbs(f32, acosh32(123123.234375), 12.414088, epsilon));
}

test acosh64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, acosh64(1.5), 0.962424, epsilon));
    try expect(math.approxEqAbs(f64, acosh64(37.45), 4.315976, epsilon));
    try expect(math.approxEqAbs(f64, acosh64(89.123), 5.183133, epsilon));
    try expect(math.approxEqAbs(f64, acosh64(123123.234375), 12.414088, epsilon));
}

test "acosh32.special" {
    try expect(math.isNan(acosh32(math.nan(f32))));
    try expect(math.isNan(acosh32(0.5)));
}

test "acosh64.special" {
    try expect(math.isNan(acosh64(math.nan(f64))));
    try expect(math.isNan(acosh64(0.5)));
}



---
File: /std/math/asin.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/asinf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/asin.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/asinl.c
//
// Ported from ARM-software, which is licensed under the MIT license:
// https://github.com/ARM-software/optimized-routines/blob/master/LICENSE
//
// https://github.com/ARM-software/optimized-routines/blob/master/math/aarch64/advsimd/asinf.c
// https://github.com/ARM-software/optimized-routines/blob/master/math/aarch64/advsimd/asin.c

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const testing = std.testing;
const builtin = @import("builtin");
const native_endian = builtin.cpu.arch.endian();

/// Returns the arc-sin of x.
///
/// Special Cases:
///  - asin(+-0) = +-0
///  - asin(x)   = nan if x < -1 or x > 1
pub fn asin(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    switch (@typeInfo(T)) {
        .float => |info| switch (info.bits) {
            16 => return asinBinary16(x),
            32 => return asinBinary32(x),
            64 => return asinBinary64(x),
            80 => return asinExtended80(x),
            128 => return asinBinary128(x),
            else => comptime unreachable,
        },
        .vector => |info| switch (info.child) {
            f32 => return asinBinary32Vec(info.len, x),
            f64 => return asinBinary64Vec(info.len, x),
            else => @compileError("unimplemented"),
        },
        else => comptime unreachable,
    }
}

fn approxBinary16(z: f32) f32 {
    const S0: f32 = 1.0000001e0;
    const S1: f32 = 1.6664918e-1;
    const S2: f32 = 7.55022e-2;
    const S3: f32 = 3.9513987e-2;
    const S4: f32 = 5.0883885e-2;
    return S0 + z * (S1 + z * (S2 + z * (S3 + z * S4)));
}

fn asinBinary16(x: f16) f16 {
    const pio2: f32 = math.pi / 2.0;

    const hx: u16 = @bitCast(x);
    const ix = hx & 0x7fff;

    // |x| >= 1
    if (ix >= 0x3c00) {
        // |x| == 1
        if (ix == 0x3c00) {
            // asin(+-1) = +-pi/2 with inexact
            return @floatCast(x * pio2 + 0x1.0p-120);
        }
        // asin(|x| > 1) is nan
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (ix < 0x3800) {
        return @floatCast(x * approxBinary16(x * x));
    }

    // 1 > |x| >= 0.5
    const z = (1.0 - @abs(x)) * 0.5;
    const s = @sqrt(z);
    const x_local = pio2 - 2.0 * s * approxBinary16(z);
    if (hx >> 15 != 0) {
        return @floatCast(-x_local);
    }
    return @floatCast(x_local);
}

fn rationalApproxBinary32(z: f32) f32 {
    const pS0: f32 = 1.6666586697e-01;
    const pS1: f32 = -4.2743422091e-02;
    const pS2: f32 = -8.6563630030e-03;
    const qS1: f32 = -7.0662963390e-01;

    const p = z * (pS0 + z * (pS1 + z * pS2));
    const q = 1.0 + z * qS1;
    return p / q;
}

fn asinBinary32(x: f32) f32 {
    const pio2: f64 = 1.570796326794896558e+00;

    const hx: u32 = @bitCast(x);
    const ix = hx & 0x7fff_ffff;

    // |x| >= 1
    if (ix >= 0x3f80_0000) {
        // |x| == 1
        if (ix == 0x3f80_0000) {
            // asin(+-1) = +-pi/2 with inexact
            return @floatCast(@as(f64, @floatCast(x)) * pio2 + 0x1.0p-120);
        }
        // asin(|x| > 1) is nan
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (ix < 0x3f00_0000) {
        // 0x1p-126 <= |x| < 0x1p-12
        if (ix < 0x3980_0000 and ix >= 0x0080_0000) {
            return x;
        }
        return x + x * rationalApproxBinary32(x * x);
    }

    // 1 > |x| >= 0.5
    const z = (1.0 - @abs(x)) * 0.5;
    const s: f64 = @floatCast(@sqrt(z));
    const x_local: f32 = @floatCast(pio2 - 2.0 * (s + s * @as(f64, @floatCast(rationalApproxBinary32(z)))));
    return if (hx >> 31 != 0) -x_local else x_local;
}

fn rationalApproxBinary64(z: f64) f64 {
    const pS0: f64 = 1.66666666666666657415e-01;
    const pS1: f64 = -3.25565818622400915405e-01;
    const pS2: f64 = 2.01212532134862925881e-01;
    const pS3: f64 = -4.00555345006794114027e-02;
    const pS4: f64 = 7.91534994289814532176e-04;
    const pS5: f64 = 3.47933107596021167570e-05;
    const qS1: f64 = -2.40339491173441421878e+00;
    const qS2: f64 = 2.02094576023350569471e+00;
    const qS3: f64 = -6.88283971605453293030e-01;
    const qS4: f64 = 7.70381505559019352791e-02;

    const p = z * (pS0 + z * (pS1 + z * (pS2 + z * (pS3 + z * (pS4 + z * pS5)))));
    const q = 1.0 + z * (qS1 + z * (qS2 + z * (qS3 + z * qS4)));
    return p / q;
}

fn asinBinary64(x: f64) f64 {
    const pio2_hi: f64 = 1.57079632679489655800e+00;
    const pio2_lo: f64 = 6.12323399573676603587e-17;

    const hx: u32 = @intCast(@as(u64, @bitCast(x)) >> 32);
    const ix = hx & 0x7fffffff;

    // |x| >= 1 or nan
    if (ix >= 0x3ff0_0000) {
        const lx: u32 = @truncate(@as(u64, @bitCast(x)));
        // asin(1) = +-pi/2 with inexact
        if ((ix - 0x3ff0_0000 | lx) == 0) {
            return x * pio2_hi + 0x1.0p-120;
        }
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (ix < 0x3fe0_0000) {
        // if 0x1p-1022 <= |x| < 0x1p-26 avoid raising overflow
        if (ix < 0x3e50_0000 and ix >= 0x0010_0000) {
            return x;
        }
        return x + x * rationalApproxBinary64(x * x);
    }

    // 1 > |x| >= 0.5
    const z = (1.0 - @abs(x)) * 0.5;
    const s = @sqrt(z);
    const r = rationalApproxBinary64(z);
    // |x| > 0.975
    if (ix >= 0x3fef_3333) {
        const x_local = pio2_hi - (2 * (s + s * r) - pio2_lo);
        return if (hx >> 31 != 0) -x_local else x_local;
    }
    // f+c = sqrt(z)
    const hs: u64 = @bitCast(s);
    const f: f64 = @bitCast(hs & 0xffff_ffff_0000_0000);
    const c: f64 = (z - f * f) / (s + f);
    const x_local = 0.5 * pio2_hi - (2.0 * s * r - (pio2_lo - 2.0 * c) - (0.5 * pio2_hi - 2.0 * f));
    return if (hx >> 31 != 0) -x_local else x_local;
}

fn rationalApproxExtended80(z: f80) f80 {
    const pS0: f80 = 1.66666666666666666631e-01;
    const pS1: f80 = -4.16313987993683104320e-01;
    const pS2: f80 = 3.69068046323246813704e-01;
    const pS3: f80 = -1.36213932016738603108e-01;
    const pS4: f80 = 1.78324189708471965733e-02;
    const pS5: f80 = -2.19216428382605211588e-04;
    const pS6: f80 = -7.10526623669075243183e-06;
    const qS1: f80 = -2.94788392796209867269e+00;
    const qS2: f80 = 3.27309890266528636716e+00;
    const qS3: f80 = -1.68285799854822427013e+00;
    const qS4: f80 = 3.90699412641738801874e-01;
    const qS5: f80 = -3.14365703596053263322e-02;

    const p = z * (pS0 + z * (pS1 + z * (pS2 + z * (pS3 + z * (pS4 + z * (pS5 + z * pS6))))));
    const q = 1.0 + z * (qS1 + z * (qS2 + z * (qS3 + z * (qS4 + z * qS5))));
    return p / q;
}

fn asinExtended80(x: f80) f80 {
    const pio2_hi: f80 = 1.57079632679489661926;
    const pio2_lo: f80 = -2.50827880633416601173e-20;

    const hx: u80 = @bitCast(x);
    const se: u16 = @truncate(hx >> 64);
    const e = se & 0x7fff;
    const sign = se >> 15 != 0;

    // |x| >= 1 or nan
    if (e >= 0x3fff) {
        // asin(+-1)=+-pi/2 with inexact
        if (x == 1.0 or x == -1.0) {
            return x * pio2_hi + 0x1p-120;
        }
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (e < 0x3fff - 1) {
        if (e < 0x3fff - (math.floatMantissaBits(f80) + 1) / 2) {
            // return x with inexact if x!=0
            mem.doNotOptimizeAway(x + 0x1p120);
            return x;
        }
        return x + x * rationalApproxExtended80(x * x);
    }

    // 1 > |x| >= 0.5
    const z = (1.0 - @abs(x)) * 0.5;
    const s = @sqrt(z);
    const r = rationalApproxExtended80(z);

    const m: u64 = @truncate(hx & 0x0000_ffff_ffff_ffff_ffff);
    if ((m >> 56) >= 0xf7) {
        const x_local = pio2_hi - (2.0 * (s + s * r) - pio2_lo);
        return if (sign) -x_local else x_local;
    }

    const hs: u80 = @bitCast(s);
    const f: f80 = @bitCast(hs & 0xffff_ffff_ffff_0000_0000);
    const c = (z - f * f) / (s + f);
    const x_local = 0.5 * pio2_hi - (2.0 * s * r - (pio2_lo - 2.0 * c) - (0.5 * pio2_hi - 2.0 * f));
    return if (sign) -x_local else x_local;
}

fn rationalApproxBinary128(z: f128) f128 {
    const pS0: f128 = 1.66666666666666666666666666666700314e-01;
    const pS1: f128 = -7.32816946414566252574527475428622708e-01;
    const pS2: f128 = 1.34215708714992334609030036562143589e+00;
    const pS3: f128 = -1.32483151677116409805070261790752040e+00;
    const pS4: f128 = 7.61206183613632558824485341162121989e-01;
    const pS5: f128 = -2.56165783329023486777386833928147375e-01;
    const pS6: f128 = 4.80718586374448793411019434585413855e-02;
    const pS7: f128 = -4.42523267167024279410230886239774718e-03;
    const pS8: f128 = 1.44551535183911458253205638280410064e-04;
    const pS9: f128 = -2.10558957916600254061591040482706179e-07;
    const qS1: f128 = -4.84690167848739751544716485245697428e+00;
    const qS2: f128 = 9.96619113536172610135016921140206980e+00;
    const qS3: f128 = -1.13177895428973036660836798461641458e+01;
    const qS4: f128 = 7.74004374389488266169304117714658761e+00;
    const qS5: f128 = -3.25871986053534084709023539900339905e+00;
    const qS6: f128 = 8.27830318881232209752469022352928864e-01;
    const qS7: f128 = -1.18768052702942805423330715206348004e-01;
    const qS8: f128 = 8.32600764660522313269101537926539470e-03;
    const qS9: f128 = -1.99407384882605586705979504567947007e-04;

    const p = z * (pS0 + z * (pS1 + z * (pS2 + z * (pS3 + z * (pS4 + z * (pS5 + z * (pS6 + z * (pS7 + z * (pS8 + z * pS9)))))))));
    const q = 1.0 + z * (qS1 + z * (qS2 + z * (qS3 + z * (qS4 + z * (qS5 + z * (qS6 + z * (qS7 + z * (qS8 + z * qS9))))))));
    return p / q;
}

fn asinBinary128(x: f128) f128 {
    const pio2_hi: f128 = 1.57079632679489661923132169163975140;
    const pio2_lo: f128 = 4.33590506506189051239852201302167613e-35;

    const hx: u128 = @bitCast(x);
    const se: u16 = @truncate(hx >> 112);
    const e = se & 0x7fff;
    const sign = se >> 15 != 0;

    // |x| >= 1 or nan
    if (e >= 0x3fff) {
        // asin(+-1)=+-pi/2 with inexact
        if (x == 1.0 or x == -1.0) {
            return x * pio2_hi + 0x1p-120;
        }
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (e < 0x3fff - 1) {
        if (e < 0x3fff - (math.floatMantissaBits(f128) + 2) / 2) {
            // return x with inexact if x!=0
            mem.doNotOptimizeAway(x + 0x1p120);
            return x;
        }
        return x + x * rationalApproxBinary128(x * x);
    }

    // 1 > |x| >= 0.5
    const z = (1.0 - @abs(x)) * 0.5;
    const s = @sqrt(z);
    const r = rationalApproxBinary128(z);

    const top: u16 = @truncate((hx >> 96) & 0x0000_ffff);
    if (top >= 0xee00) {
        const x_local = pio2_hi - (2.0 * (s + s * r) - pio2_lo);
        return if (sign) -x_local else x_local;
    }

    const hs: u128 = @bitCast(s);
    const f: f128 = @bitCast(hs & 0xffff_ffff_ffff_ffff_0000_0000_0000_0000);
    const c = (z - f * f) / (s + f);
    const x_local = 0.5 * pio2_hi - (2.0 * s * r - (pio2_lo - 2.0 * c) - (0.5 * pio2_hi - 2.0 * f));
    return if (sign) -x_local else x_local;
}

test "asinBinary16.special" {
    try testing.expectApproxEqAbs(0x1.92p0, asinBinary16(0x1p+0), math.floatEpsAt(f16, 0x1.92p0));
    try testing.expectApproxEqAbs(-0x1.92p0, asinBinary16(-0x1p+0), math.floatEpsAt(f16, -0x1.92p0));
    try testing.expectEqual(0x0p+0, asinBinary16(0x0p+0));
    try testing.expectEqual(0x0p+0, asinBinary16(-0x0p+0));
    try testing.expect(math.isNan(asinBinary16(0x1.004p0)));
    try testing.expect(math.isNan(asinBinary16(-0x1.004p0)));
    try testing.expect(math.isNan(asinBinary16(math.inf(f16))));
    try testing.expect(math.isNan(asinBinary16(-math.inf(f16))));
    try testing.expect(math.isNan(asinBinary16(math.nan(f16))));
}

test "asinBinary16" {
    try testing.expectApproxEqAbs(-0x1.e4cp-6, asinBinary16(-0x1.e4cp-6), math.floatEpsAt(f16, -0x1.e4cp-6));
    try testing.expectApproxEqAbs(0x1.2a8p0, asinBinary16(0x1.d68p-1), math.floatEpsAt(f16, 0x1.2a8p0));
    try testing.expectApproxEqAbs(-0x1.eep-1, asinBinary16(-0x1.a4cp-1), math.floatEpsAt(f16, -0x1.eep-1));
    try testing.expectApproxEqAbs(-0x1.0d4p-2, asinBinary16(-0x1.0a4p-2), math.floatEpsAt(f16, -0x1.0d4p-2));
    try testing.expectApproxEqAbs(0x1.3c8p-1, asinBinary16(0x1.28cp-1), math.floatEpsAt(f16, 0x1.3c8p-1));
    try testing.expectApproxEqAbs(0x1.298p-3, asinBinary16(0x1.284p-3), math.floatEpsAt(f16, 0x1.298p-3));
    try testing.expectApproxEqAbs(-0x1.784p-1, asinBinary16(-0x1.574p-1), math.floatEpsAt(f16, -0x1.784p-1));
    try testing.expectApproxEqAbs(-0x1.6a4p-1, asinBinary16(-0x1.4ccp-1), math.floatEpsAt(f16, -0x1.6a4p-1));
    try testing.expectApproxEqAbs(0x1.e84p-1, asinBinary16(0x1.a18p-1), math.floatEpsAt(f16, 0x1.e84p-1));
    try testing.expectApproxEqAbs(0x1.83cp-2, asinBinary16(0x1.7a8p-2), math.floatEpsAt(f16, 0x1.83cp-2));
}

test "asinBinary32.special" {
    try testing.expectApproxEqAbs(0x1.921fb6p+0, asinBinary32(0x1p+0), math.floatEpsAt(f32, 0x1.921fb6p+0));
    try testing.expectApproxEqAbs(-0x1.921fb6p+0, asinBinary32(-0x1p+0), math.floatEpsAt(f32, -0x1.921fb6p+0));
    try testing.expectEqual(0x0p+0, asinBinary32(0x0p+0));
    try testing.expectEqual(0x0p+0, asinBinary32(-0x0p+0));
    try testing.expect(math.isNan(asinBinary32(0x1.000002p+0)));
    try testing.expect(math.isNan(asinBinary32(-0x1.000002p+0)));
    try testing.expect(math.isNan(asinBinary32(math.inf(f32))));
    try testing.expect(math.isNan(asinBinary32(-math.inf(f32))));
    try testing.expect(math.isNan(asinBinary32(math.nan(f32))));
}

test "asinBinary32" {
    try testing.expectApproxEqAbs(-0x1.4c868p-4, asinBinary32(-0x1.4c2906p-4), math.floatEpsAt(f32, -0x1.4c868p-4));
    try testing.expectApproxEqAbs(0x1.130648p-1, asinBinary32(0x1.05fcfap-1), math.floatEpsAt(f32, 0x1.130648p-1));
    try testing.expectApproxEqAbs(0x1.090abcp-1, asinBinary32(0x1.fab976p-2), math.floatEpsAt(f32, 0x1.090abcp-1));
    try testing.expectApproxEqAbs(0x1.c39fa2p-1, asinBinary32(0x1.8b4b8cp-1), math.floatEpsAt(f32, 0x1.c39fa2p-1));
    try testing.expectApproxEqAbs(0x1.9c332p-1, asinBinary32(0x1.7117c2p-1), math.floatEpsAt(f32, 0x1.9c332p-1));
    try testing.expectApproxEqAbs(0x1.e62a1cp-5, asinBinary32(0x1.e5e112p-5), math.floatEpsAt(f32, 0x1.e62a1cp-5));
    try testing.expectApproxEqAbs(-0x1.0a65dep-2, asinBinary32(-0x1.07673p-2), math.floatEpsAt(f32, -0x1.0a65dep-2));
    try testing.expectApproxEqAbs(-0x1.25046p-2, asinBinary32(-0x1.2108dep-2), math.floatEpsAt(f32, -0x1.25046p-2));
    try testing.expectApproxEqAbs(-0x1.6c6f0cp-1, asinBinary32(-0x1.4e6e6cp-1), math.floatEpsAt(f32, -0x1.6c6f0cp-1));
    try testing.expectApproxEqAbs(0x1.350f7ap-1, asinBinary32(0x1.22a16ap-1), math.floatEpsAt(f32, 0x1.350f7ap-1));
}

test "asinBinary64.special" {
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+0, asinBinary64(0x1p+0), math.floatEpsAt(f64, 0x1.921fb54442d18p+0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18p+0, asinBinary64(-0x1p+0), math.floatEpsAt(f64, -0x1.921fb54442d18p+0));
    try testing.expectEqual(0x0p+0, asinBinary64(0x0p+0));
    try testing.expectEqual(0x0p+0, asinBinary64(-0x0p+0));
    try testing.expect(math.isNan(asinBinary64(0x1.000002p+0)));
    try testing.expect(math.isNan(asinBinary64(-0x1.000002p+0)));
    try testing.expect(math.isNan(asinBinary64(math.inf(f64))));
    try testing.expect(math.isNan(asinBinary64(-math.inf(f64))));
    try testing.expect(math.isNan(asinBinary64(math.nan(f64))));
}

test "asinBinary64" {
    try testing.expectApproxEqAbs(0x1.fae86c5941692p-2, asinBinary64(0x1.e674fba3e40d5p-2), math.floatEpsAt(f64, 0x1.fae86c5941692p-2));
    try testing.expectApproxEqAbs(-0x1.46b6ad730c93ap-1, asinBinary64(-0x1.30fd0566fd979p-1), math.floatEpsAt(f64, -0x1.46b6ad730c93ap-1));
    try testing.expectApproxEqAbs(0x1.6be0be8074eep-2, asinBinary64(0x1.6444a25abfeaap-2), math.floatEpsAt(f64, 0x1.6be0be8074eep-2));
    try testing.expectApproxEqAbs(0x1.5a7e98f53f717p-1, asinBinary64(0x1.40a53228d1a13p-1), math.floatEpsAt(f64, 0x1.5a7e98f53f717p-1));
    try testing.expectApproxEqAbs(-0x1.1ea2602d14e8p0, asinBinary64(-0x1.ccc6d64845cfdp-1), math.floatEpsAt(f64, -0x1.1ea2602d14e8p0));
    try testing.expectApproxEqAbs(-0x1.d2c2634193158p-1, asinBinary64(-0x1.94bd91b7fc74bp-1), math.floatEpsAt(f64, -0x1.d2c2634193158p-1));
    try testing.expectApproxEqAbs(-0x1.982d5f1895d2p-2, asinBinary64(-0x1.8d741b5797fccp-2), math.floatEpsAt(f64, -0x1.982d5f1895d2p-2));
    try testing.expectApproxEqAbs(-0x1.3fdaf7dfdc864p-3, asinBinary64(-0x1.3e8e7e15881c5p-3), math.floatEpsAt(f64, -0x1.3fdaf7dfdc864p-3));
    try testing.expectApproxEqAbs(-0x1.9269540735b7bp-2, asinBinary64(-0x1.88222d8ab8ca9p-2), math.floatEpsAt(f64, -0x1.9269540735b7bp-2));
    try testing.expectApproxEqAbs(-0x1.474c4c6625527p-2, asinBinary64(-0x1.41c0e9babcbd2p-2), math.floatEpsAt(f64, -0x1.474c4c6625527p-2));
}

test "asinExtended80.special" {
    try testing.expectApproxEqAbs(0x1.921fb54442d1846ap+0, asinExtended80(0x1p+0), math.floatEpsAt(f80, 0x1.921fb54442d1846ap+0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d1846ap+0, asinExtended80(-0x1p+0), math.floatEpsAt(f80, -0x1.921fb54442d1846ap+0));
    try testing.expectEqual(0x0p+0, asinExtended80(0x0p+0));
    try testing.expectEqual(0x0p+0, asinExtended80(-0x0p+0));
    try testing.expect(math.isNan(asinExtended80(0x1.0000000000000002p+0)));
    try testing.expect(math.isNan(asinExtended80(-0x1.0000000000000002p+0)));
    try testing.expect(math.isNan(asinExtended80(math.inf(f80))));
    try testing.expect(math.isNan(asinExtended80(-math.inf(f80))));
    try testing.expect(math.isNan(asinExtended80(math.nan(f80))));
}

test "asinExtended80" {
    try testing.expectApproxEqAbs(0x1.63cfb560149daa9p-9, asinExtended80(0x1.63cf98bc52ce0da8p-9), math.floatEpsAt(f80, 0x1.63cfb560149daa9p-9));
    try testing.expectApproxEqAbs(-0x1.113cbacd8cd1b96cp-1, asinExtended80(-0x1.0473756f7ae930dp-1), math.floatEpsAt(f80, -0x1.113cbacd8cd1b96cp-1));
    try testing.expectApproxEqAbs(-0x1.2721b231d197b064p-2, asinExtended80(-0x1.2310057e005cc288p-2), math.floatEpsAt(f80, -0x1.2721b231d197b064p-2));
    try testing.expectApproxEqAbs(0x1.547c408c5d2b05aap0, asinExtended80(0x1.f13b03bd685d96eap-1), math.floatEpsAt(f80, 0x1.547c408c5d2b05aap0));
    try testing.expectApproxEqAbs(-0x1.296b76bfadbb5cecp0, asinExtended80(-0x1.d5c507e3ef84041cp-1), math.floatEpsAt(f80, -0x1.296b76bfadbb5cecp0));
    try testing.expectApproxEqAbs(0x1.b572da8729a84f2ap-1, asinExtended80(0x1.8222cbc9147153d8p-1), math.floatEpsAt(f80, 0x1.b572da8729a84f2ap-1));
    try testing.expectApproxEqAbs(-0x1.42c9e80ac0524dap-11, asinExtended80(-0x1.42c9e6b4a088a246p-11), math.floatEpsAt(f80, -0x1.42c9e80ac0524dap-11));
    try testing.expectApproxEqAbs(-0x1.920ca86aef6c3028p-3, asinExtended80(-0x1.8f78d49deadb521cp-3), math.floatEpsAt(f80, -0x1.920ca86aef6c3028p-3));
    try testing.expectApproxEqAbs(-0x1.b91cb4f7204d92fp-2, asinExtended80(-0x1.ab98792783515774p-2), math.floatEpsAt(f80, -0x1.b91cb4f7204d92fp-2));
    try testing.expectApproxEqAbs(-0x1.1f20815fdc4c5304p-1, asinExtended80(-0x1.104fe30cef6800aap-1), math.floatEpsAt(f80, -0x1.1f20815fdc4c5304p-1));
}

test "asinBinary128.special" {
    try testing.expectApproxEqAbs(0x1.921fb54442d18469898cc51701b8p0, asinBinary128(0x1p+0), math.floatEpsAt(f128, 0x1.921fb54442d18469898cc51701b8p0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18469898cc51701b8p0, asinBinary128(-0x1p+0), math.floatEpsAt(f128, -0x1.921fb54442d18469898cc51701b8p0));
    try testing.expectEqual(0x0p+0, asinBinary128(0x0p+0));
    try testing.expectEqual(0x0p+0, asinBinary128(-0x0p+0));
    try testing.expect(math.isNan(asinBinary128(0x1.0000000000000000000000000001p0)));
    try testing.expect(math.isNan(asinBinary128(-0x1.0000000000000000000000000001p0)));
    try testing.expect(math.isNan(asinBinary128(math.inf(f128))));
    try testing.expect(math.isNan(asinBinary128(-math.inf(f128))));
    try testing.expect(math.isNan(asinBinary128(math.nan(f128))));
}

test "asinBinary128" {
    try testing.expectApproxEqAbs(0x1.87e9c740d7837f8e8fa667988fbep-3, asinBinary128(0x1.85868ce287ca0196b01c25fec5ffp-3), math.floatEpsAt(f128, 0x1.87e9c740d7837f8e8fa667988fbep-3));
    try testing.expectApproxEqAbs(0x1.bd11a474e864213b48e0f005f1f4p-1, asinBinary128(0x1.8718d6d30b4daed08d04ef59f478p-1), math.floatEpsAt(f128, 0x1.bd11a474e864213b48e0f005f1f4p-1));
    try testing.expectApproxEqAbs(0x1.20b56f8b42649fe72d1f8d68a378p-1, asinBinary128(0x1.11a67640cd7f0ba5d5e362f3abfap-1), math.floatEpsAt(f128, 0x1.20b56f8b42649fe72d1f8d68a378p-1));
    try testing.expectApproxEqAbs(-0x1.0dc3a7ddb9736e5ad699bf338566p0, asinBinary128(-0x1.bd13bf14a9dce22188e52650daa7p-1), math.floatEpsAt(f128, -0x1.0dc3a7ddb9736e5ad699bf338566p0));
    try testing.expectApproxEqAbs(-0x1.f250716038f70fa50a5826c03802p-2, asinBinary128(-0x1.dee0bc217fc462af57c484eefa71p-2), math.floatEpsAt(f128, -0x1.f250716038f70fa50a5826c03802p-2));
    try testing.expectApproxEqAbs(-0x1.47a8b4cdd327f90056722feddbabp0, asinBinary128(-0x1.ea7df9139371c10b9d6fd2bbccd3p-1), math.floatEpsAt(f128, -0x1.47a8b4cdd327f90056722feddbabp0));
    try testing.expectApproxEqAbs(0x1.079178d52be662dec67e2cd7f6e9p-2, asinBinary128(0x1.04aaea6de3b5a616460702f26dfcp-2), math.floatEpsAt(f128, 0x1.079178d52be662dec67e2cd7f6e9p-2));
    try testing.expectApproxEqAbs(-0x1.192df5a8d71702cf1e27014887b2p0, asinBinary128(-0x1.c7ea85e6b61be666435a7d99444cp-1), math.floatEpsAt(f128, -0x1.192df5a8d71702cf1e27014887b2p0));
    try testing.expectApproxEqAbs(-0x1.97f1092fd94ac0fdfddae2e1222bp-1, asinBinary128(-0x1.6e210214e40edf6c8479998189d1p-1), math.floatEpsAt(f128, -0x1.97f1092fd94ac0fdfddae2e1222bp-1));
    try testing.expectApproxEqAbs(-0x1.97b62bc5ae6512093828828325e1p-3, asinBinary128(-0x1.95061bf93ed6986a45d20f0e1064p-3), math.floatEpsAt(f128, -0x1.97b62bc5ae6512093828828325e1p-3));
}

fn asinBinary32Vec(comptime vec_len: comptime_int, x: @Vector(vec_len, f32)) @TypeOf(x) {
    const pi_over_2: @Vector(vec_len, f32) = @splat(math.pi / 2.0);
    const zero: @Vector(vec_len, f32) = @splat(0.0);
    const half: @Vector(vec_len, f32) = @splat(0.5);
    const neg_two: @Vector(vec_len, f32) = @splat(-2.0);
    const c0: @Vector(vec_len, f32) = @splat(0x1.55555ep-3);
    const c1: @Vector(vec_len, f32) = @splat(0x1.33261ap-4);
    const c2: @Vector(vec_len, f32) = @splat(0x1.70d7dcp-5);
    const c3: @Vector(vec_len, f32) = @splat(0x1.b059dp-6);
    const c4: @Vector(vec_len, f32) = @splat(0x1.3af7d8p-5);

    const ax = @abs(x);
    const ax_lt_half = ax < half;
    const z2 = @select(f32, ax_lt_half, x * x, @mulAdd(@Vector(vec_len, f32), -half, ax, half));
    const z = @select(f32, ax_lt_half, ax, @sqrt(z2));
    const z3 = z2 * z;
    const p3_4 = @mulAdd(@Vector(vec_len, f32), z2, c4, c3);
    const p2_4 = @mulAdd(@Vector(vec_len, f32), z2, p3_4, c2);
    const p1_4 = @mulAdd(@Vector(vec_len, f32), z2, p2_4, c1);
    const p0_4 = @mulAdd(@Vector(vec_len, f32), z2, p1_4, c0);
    const p = @mulAdd(@Vector(vec_len, f32), z3, p0_4, z);
    const y = @select(f32, ax_lt_half, p, @mulAdd(@Vector(vec_len, f32), p, neg_two, pi_over_2));
    return @select(f32, x < zero, -y, y);
}

fn asinBinary64Vec(comptime vec_len: comptime_int, x: @Vector(vec_len, f64)) @TypeOf(x) {
    const pi_over_2: @Vector(vec_len, f64) = @splat(math.pi / 2.0);
    const zero: @Vector(vec_len, f64) = @splat(0.0);
    const half: @Vector(vec_len, f64) = @splat(0.5);
    const neg_two: @Vector(vec_len, f64) = @splat(-2.0);
    const c0: @Vector(vec_len, f64) = @splat(0x1.555555555554ep-3);
    const c1: @Vector(vec_len, f64) = @splat(0x1.3333333337233p-4);
    const c2: @Vector(vec_len, f64) = @splat(0x1.6db6db67f6d9fp-5);
    const c3: @Vector(vec_len, f64) = @splat(0x1.f1c71fbd29fbbp-6);
    const c4: @Vector(vec_len, f64) = @splat(0x1.6e8b264d467d6p-6);
    const c5: @Vector(vec_len, f64) = @splat(0x1.1c5997c357e9dp-6);
    const c6: @Vector(vec_len, f64) = @splat(0x1.c86a22cd9389dp-7);
    const c7: @Vector(vec_len, f64) = @splat(0x1.856073c22ebbep-7);
    const c8: @Vector(vec_len, f64) = @splat(0x1.fd1151acb6bedp-8);
    const c9: @Vector(vec_len, f64) = @splat(0x1.087182f799c1dp-6);
    const c10: @Vector(vec_len, f64) = @splat(-0x1.6602748120927p-7);
    const c11: @Vector(vec_len, f64) = @splat(0x1.cfa0dd1f9478p-6);

    const ax = @abs(x);
    const ax_lt_half = ax < half;
    const z2 = @select(f64, ax_lt_half, x * x, @mulAdd(@Vector(vec_len, f64), -half, ax, half));
    const z = @select(f64, ax_lt_half, ax, @sqrt(z2));
    const z3 = z2 * z;
    const z4 = z2 * z2;
    const z8 = z4 * z4;
    const p0_1 = @mulAdd(@Vector(vec_len, f64), z2, c1, c0);
    const p2_3 = @mulAdd(@Vector(vec_len, f64), z2, c3, c2);
    const p0_3 = @mulAdd(@Vector(vec_len, f64), z4, p2_3, p0_1);
    const p4_5 = @mulAdd(@Vector(vec_len, f64), z2, c5, c4);
    const p6_7 = @mulAdd(@Vector(vec_len, f64), z2, c7, c6);
    const p4_7 = @mulAdd(@Vector(vec_len, f64), z4, p6_7, p4_5);
    const p8_9 = @mulAdd(@Vector(vec_len, f64), z2, c9, c8);
    const p10_11 = @mulAdd(@Vector(vec_len, f64), z2, c11, c10);
    const p8_11 = @mulAdd(@Vector(vec_len, f64), z4, p10_11, p8_9);
    const p4_11 = @mulAdd(@Vector(vec_len, f64), z8, p8_11, p4_7);
    const p0_11 = @mulAdd(@Vector(vec_len, f64), z8, p4_11, p0_3);
    const p = @mulAdd(@Vector(vec_len, f64), z3, p0_11, z);
    const y = @select(f64, ax_lt_half, p, @mulAdd(@Vector(vec_len, f64), p, neg_two, pi_over_2));
    return @select(f64, x < zero, -y, y);
}

test "asinBinary32Vec.special" {
    const input: @Vector(9, f32) = .{
        0x1p+0,
        -0x1p+0,
        0x0p+0,
        -0x0p+0,
        0x1.000002p+0,
        -0x1.000002p+0,
        math.inf(f32),
        -math.inf(f32),
        math.nan(f32),
    };
    const output = asinBinary32Vec(9, input);
    try testing.expectApproxEqAbs(0x1.921fb6p+0, output[0], math.floatEpsAt(f32, 0x1.921fb6p+0));
    try testing.expectApproxEqAbs(-0x1.921fb6p+0, output[1], math.floatEpsAt(f32, -0x1.921fb6p+0));
    try testing.expectEqual(0x0p+0, output[2]);
    try testing.expectEqual(0x0p+0, output[3]);
    try testing.expect(math.isNan(output[4]));
    try testing.expect(math.isNan(output[5]));
    try testing.expect(math.isNan(output[6]));
    try testing.expect(math.isNan(output[7]));
    try testing.expect(math.isNan(output[8]));
}

test "asinBinary32Vec" {
    const input: @Vector(10, f32) = .{
        -0x1.4c2906p-4,
        0x1.05fcfap-1,
        0x1.fab976p-2,
        0x1.8b4b8cp-1,
        0x1.7117c2p-1,
        0x1.e5e112p-5,
        -0x1.07673p-2,
        -0x1.2108dep-2,
        -0x1.4e6e6cp-1,
        0x1.22a16ap-1,
    };
    const output = asinBinary32Vec(10, input);
    try testing.expectApproxEqAbs(-0x1.4c868p-4, output[0], math.floatEpsAt(f32, -0x1.4c868p-4));
    try testing.expectApproxEqAbs(0x1.130648p-1, output[1], math.floatEpsAt(f32, 0x1.130648p-1));
    try testing.expectApproxEqAbs(0x1.090abcp-1, output[2], math.floatEpsAt(f32, 0x1.090abcp-1));
    try testing.expectApproxEqAbs(0x1.c39fa2p-1, output[3], math.floatEpsAt(f32, 0x1.c39fa2p-1));
    try testing.expectApproxEqAbs(0x1.9c332p-1, output[4], math.floatEpsAt(f32, 0x1.9c332p-1));
    try testing.expectApproxEqAbs(0x1.e62a1cp-5, output[5], math.floatEpsAt(f32, 0x1.e62a1cp-5));
    try testing.expectApproxEqAbs(-0x1.0a65dep-2, output[6], math.floatEpsAt(f32, -0x1.0a65dep-2));
    try testing.expectApproxEqAbs(-0x1.25046p-2, output[7], math.floatEpsAt(f32, -0x1.25046p-2));
    try testing.expectApproxEqAbs(-0x1.6c6f0cp-1, output[8], math.floatEpsAt(f32, -0x1.6c6f0cp-1));
    try testing.expectApproxEqAbs(0x1.350f7ap-1, output[9], math.floatEpsAt(f32, 0x1.350f7ap-1));
}

test "asinBinary64Vec.special" {
    const input: @Vector(9, f64) = .{
        0x1p+0,
        -0x1p+0,
        0x0p+0,
        -0x0p+0,
        0x1.000002p+0,
        -0x1.000002p+0,
        math.inf(f64),
        -math.inf(f64),
        math.nan(f64),
    };
    const output = asinBinary64Vec(9, input);
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+0, output[0], math.floatEpsAt(f64, 0x1.921fb54442d18p+0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18p+0, output[1], math.floatEpsAt(f64, -0x1.921fb54442d18p+0));
    try testing.expectEqual(0x0p+0, output[2]);
    try testing.expectEqual(0x0p+0, output[3]);
    try testing.expect(math.isNan(output[4]));
    try testing.expect(math.isNan(output[5]));
    try testing.expect(math.isNan(output[6]));
    try testing.expect(math.isNan(output[7]));
    try testing.expect(math.isNan(output[8]));
}

test "asinBinary64Vec" {
    const input: @Vector(10, f64) = .{
        0x1.e674fba3e40d5p-2,
        -0x1.30fd0566fd979p-1,
        0x1.6444a25abfeaap-2,
        0x1.40a53228d1a13p-1,
        -0x1.ccc6d64845cfdp-1,
        -0x1.94bd91b7fc74bp-1,
        -0x1.8d741b5797fccp-2,
        -0x1.3e8e7e15881c5p-3,
        -0x1.88222d8ab8ca9p-2,
        -0x1.41c0e9babcbd2p-2,
    };
    const output = asinBinary64Vec(10, input);
    try testing.expectApproxEqAbs(0x1.fae86c5941692p-2, output[0], math.floatEpsAt(f64, 0x1.fae86c5941692p-2));
    try testing.expectApproxEqAbs(-0x1.46b6ad730c93ap-1, output[1], math.floatEpsAt(f64, -0x1.46b6ad730c93ap-1));
    try testing.expectApproxEqAbs(0x1.6be0be8074eep-2, output[2], math.floatEpsAt(f64, 0x1.6be0be8074eep-2));
    try testing.expectApproxEqAbs(0x1.5a7e98f53f717p-1, output[3], math.floatEpsAt(f64, 0x1.5a7e98f53f717p-1));
    try testing.expectApproxEqAbs(-0x1.1ea2602d14e8p0, output[4], math.floatEpsAt(f64, -0x1.1ea2602d14e8p0));
    try testing.expectApproxEqAbs(-0x1.d2c2634193158p-1, output[5], math.floatEpsAt(f64, -0x1.d2c2634193158p-1));
    try testing.expectApproxEqAbs(-0x1.982d5f1895d2p-2, output[6], math.floatEpsAt(f64, -0x1.982d5f1895d2p-2));
    try testing.expectApproxEqAbs(-0x1.3fdaf7dfdc864p-3, output[7], math.floatEpsAt(f64, -0x1.3fdaf7dfdc864p-3));
    try testing.expectApproxEqAbs(-0x1.9269540735b7bp-2, output[8], math.floatEpsAt(f64, -0x1.9269540735b7bp-2));
    try testing.expectApproxEqAbs(-0x1.474c4c6625527p-2, output[9], math.floatEpsAt(f64, -0x1.474c4c6625527p-2));
}



---
File: /std/math/asinh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/asinhf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/asinh.c

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const expect = std.testing.expect;
const maxInt = std.math.maxInt;

/// Returns the hyperbolic arc-sin of x.
///
/// Special Cases:
///  - asinh(+-0)   = +-0
///  - asinh(+-inf) = +-inf
///  - asinh(nan)   = nan
pub fn asinh(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    return switch (T) {
        f32 => asinh32(x),
        f64 => asinh64(x),
        else => @compileError("asinh not implemented for " ++ @typeName(T)),
    };
}

// asinh(x) = sign(x) * log(|x| + sqrt(x * x + 1)) ~= x - x^3/6 + o(x^5)
fn asinh32(x: f32) f32 {
    const u = @as(u32, @bitCast(x));
    const i = u & 0x7FFFFFFF;
    const s = u >> 31;

    var rx = @as(f32, @bitCast(i)); // |x|

    // |x| >= 0x1p12 or inf or nan
    if (i >= 0x3F800000 + (12 << 23)) {
        rx = @log(rx) + 0.69314718055994530941723212145817656;
    }
    // |x| >= 2
    else if (i >= 0x3F800000 + (1 << 23)) {
        rx = @log(2 * rx + 1 / (@sqrt(rx * rx + 1) + rx));
    }
    // |x| >= 0x1p-12, up to 1.6ulp error
    else if (i >= 0x3F800000 - (12 << 23)) {
        rx = math.log1p(rx + rx * rx / (@sqrt(rx * rx + 1) + 1));
    }
    // |x| < 0x1p-12, inexact if x != 0
    else {
        mem.doNotOptimizeAway(rx + 0x1.0p120);
    }

    return if (s != 0) -rx else rx;
}

fn asinh64(x: f64) f64 {
    const u = @as(u64, @bitCast(x));
    const e = (u >> 52) & 0x7FF;
    const s = u >> 63;

    var rx = @as(f64, @bitCast(u & (maxInt(u64) >> 1))); // |x|

    // |x| >= 0x1p26 or inf or nan
    if (e >= 0x3FF + 26) {
        rx = @log(rx) + 0.693147180559945309417232121458176568;
    }
    // |x| >= 2
    else if (e >= 0x3FF + 1) {
        rx = @log(2 * rx + 1 / (@sqrt(rx * rx + 1) + rx));
    }
    // |x| >= 0x1p-12, up to 1.6ulp error
    else if (e >= 0x3FF - 26) {
        rx = math.log1p(rx + rx * rx / (@sqrt(rx * rx + 1) + 1));
    }
    // |x| < 0x1p-12, inexact if x != 0
    else {
        mem.doNotOptimizeAway(rx + 0x1.0p120);
    }

    return if (s != 0) -rx else rx;
}

test asinh {
    try expect(asinh(@as(f32, 0.0)) == asinh32(0.0));
    try expect(asinh(@as(f64, 0.0)) == asinh64(0.0));
}

test asinh32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, asinh32(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(-0.2), -0.198690, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(0.2), 0.198690, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(0.8923), 0.803133, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(1.5), 1.194763, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(37.45), 4.316332, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(89.123), 5.183196, epsilon));
    try expect(math.approxEqAbs(f32, asinh32(123123.234375), 12.414088, epsilon));
}

test asinh64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, asinh64(0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(-0.2), -0.198690, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(0.2), 0.198690, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(0.8923), 0.803133, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(1.5), 1.194763, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(37.45), 4.316332, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(89.123), 5.183196, epsilon));
    try expect(math.approxEqAbs(f64, asinh64(123123.234375), 12.414088, epsilon));
}

test "asinh32.special" {
    try expect(math.isPositiveZero(asinh32(0.0)));
    try expect(math.isNegativeZero(asinh32(-0.0)));
    try expect(math.isPositiveInf(asinh32(math.inf(f32))));
    try expect(math.isNegativeInf(asinh32(-math.inf(f32))));
    try expect(math.isNan(asinh32(math.nan(f32))));
}

test "asinh64.special" {
    try expect(math.isPositiveZero(asinh64(0.0)));
    try expect(math.isNegativeZero(asinh64(-0.0)));
    try expect(math.isPositiveInf(asinh64(math.inf(f64))));
    try expect(math.isNegativeInf(asinh64(-math.inf(f64))));
    try expect(math.isNan(asinh64(math.nan(f64))));
}



---
File: /std/math/atan.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/atanf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/atan.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/atanl.c
//
// Ported from ARM-software, which is licensed under the MIT license:
// https://github.com/ARM-software/optimized-routines/blob/master/LICENSE
//
// https://github.com/ARM-software/optimized-routines/blob/master/math/aarch64/advsimd/atanf.c
// https://github.com/ARM-software/optimized-routines/blob/master/math/aarch64/advsimd/atan.c

const std = @import("../std.zig");
const math = std.math;
const mem = std.mem;
const testing = std.testing;

/// Returns the arc-tangent of x.
///
/// Special Cases:
///  - atan(+-0)   = +-0
///  - atan(+-inf) = +-pi/2
pub fn atan(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    switch (@typeInfo(T)) {
        .float => |info| switch (info.bits) {
            16 => return atanBinary16(x),
            32 => return atanBinary32(x),
            64 => return atanBinary64(x),
            80 => return atanExtended80(x),
            128 => return atanBinary128(x),
            else => comptime unreachable,
        },
        .vector => |info| switch (info.child) {
            f32 => return atanBinary32Vec(info.len, x),
            f64 => return atanBinary64Vec(info.len, x),
            else => @compileError("unimplemented"),
        },
        else => comptime unreachable,
    }
}

fn atanBinary16(x: f16) f16 {
    const atanhi: []const f32 = &.{
        4.6364760399e-01, // atan(0.5)hi 0x3eed6338
        7.8539812565e-01, // atan(1.0)hi 0x3f490fda
        9.8279368877e-01, // atan(1.5)hi 0x3f7b985e
        1.5707962513e+00, // atan(inf)hi 0x3fc90fda
    };
    const aT: []const f32 = &.{
        0x1.fffcccp-1,
        -0x1.52e8ccp-2,
        0x1.522336p-3,
    };

    const hx: u16 = @bitCast(x);
    const ix = hx & 0x7fff;
    const sign = (hx >> 15) != 0;
    // if |x| >= 2^11
    if (ix >= 0x6800) {
        if (math.isNan(x)) {
            return x;
        }
        const z = atanhi[3] + 0x1p-120;
        return @floatCast(if (sign) -z else z);
    }
    const x_: f32, const id: ?usize = blk: {
        // |x| < 0.4375
        if (ix < 0x3700) {
            // |x| < 2^(-6)
            if (ix < 0x2400) {
                if (ix < 0x400) {
                    // raise underflow for subnormal x
                    mem.doNotOptimizeAway(x * x);
                }
                return x;
            }
            break :blk .{ @floatCast(x), null };
        } else {
            const x_: f32 = @floatCast(@abs(x));
            // |x| < 1.1875
            if (ix < 0x3cc0) {
                // 7/16 <= |x| < 11/16
                if (ix < 0x3980) {
                    break :blk .{ (2.0 * x_ - 1.0) / (2.0 + x_), 0 };
                }
                // 11/16 <= |x| < 19/16
                else {
                    break :blk .{ (x_ - 1.0) / (x_ + 1.0), 1 };
                }
            } else {
                // |x| < 2.4375
                if (ix < 0x40e0) {
                    break :blk .{ (x_ - 1.5) / (1.0 + 1.5 * x_), 2 };
                }
                // 2.4375 <= |x| < 2^11
                else {
                    break :blk .{ -1.0 / x_, 3 };
                }
            }
        }
    };
    // end of argument reduction
    const z = x_ * x_;
    const s = aT[0] + z * (aT[1] + z * aT[2]);
    if (id) |id_| {
        const z_ = atanhi[id_] + x_ * s;
        return @floatCast(if (sign) -z_ else z_);
    } else {
        return @floatCast(x_ * s);
    }
}

fn atanBinary32(x: f32) f32 {
    const atanhi: []const f32 = &.{
        4.6364760399e-01, // atan(0.5)hi 0x3eed6338
        7.8539812565e-01, // atan(1.0)hi 0x3f490fda
        9.8279368877e-01, // atan(1.5)hi 0x3f7b985e
        1.5707962513e+00, // atan(inf)hi 0x3fc90fda
    };
    const atanlo: []const f32 = &.{
        5.0121582440e-09, // atan(0.5)lo 0x31ac3769
        3.7748947079e-08, // atan(1.0)lo 0x33222168
        3.4473217170e-08, // atan(1.5)lo 0x33140fb4
        7.5497894159e-08, // atan(inf)lo 0x33a22168
    };
    const aT: []const f32 = &.{
        3.3333328366e-01,
        -1.9999158382e-01,
        1.4253635705e-01,
        -1.0648017377e-01,
        6.1687607318e-02,
    };

    const hx: u32 = @bitCast(x);
    const ix = hx & 0x7fff_ffff;
    const sign = (hx >> 31) != 0;
    // if |x| >= 2^26
    if (ix >= 0x4c80_0000) {
        if (math.isNan(x)) {
            return x;
        }
        const z = atanhi[3] + 0x1p-120;
        return if (sign) -z else z;
    }
    const x_, const id: ?usize = blk: {
        // |x| < 0.4375
        if (ix < 0x3ee00000) {
            // |x| < 2^(-12)
            if (ix < 0x39800000) {
                if (ix < 0x00800000) {
                    // raise underflow for subnormal x
                    mem.doNotOptimizeAway(x * x);
                }
                return x;
            }
            break :blk .{ x, null };
        } else {
            const x_ = @abs(x);
            // |x| < 1.1875
            if (ix < 0x3f98_0000) {
                // 7/16 <= |x| < 11/16
                if (ix < 0x3f30_0000) {
                    break :blk .{ (2.0 * x_ - 1.0) / (2.0 + x_), 0 };
                }
                // 11/16 <= |x| < 19/16
                else {
                    break :blk .{ (x_ - 1.0) / (x_ + 1.0), 1 };
                }
            } else {
                // |x| < 2.4375
                if (ix < 0x401c_0000) {
                    break :blk .{ (x_ - 1.5) / (1.0 + 1.5 * x_), 2 };
                }
                // 2.4375 <= |x| < 2^26
                else {
                    break :blk .{ -1.0 / x_, 3 };
                }
            }
        }
    };
    // end of argument reduction
    const z = x_ * x_;
    const w = z * z;
    // break sum from i=0 to 10 aT[i]z^(i+1) into odd and even poly
    const s1 = z * (aT[0] + w * (aT[2] + w * aT[4]));
    const s2 = w * (aT[1] + w * aT[3]);
    if (id) |id_| {
        const z_ = atanhi[id_] - ((x_ * (s1 + s2) - atanlo[id_]) - x_);
        return if (sign) -z_ else z_;
    } else {
        return x_ - x_ * (s1 + s2);
    }
}

fn atanBinary64(x: f64) f64 {
    const atanhi: []const f64 = &.{
        4.63647609000806093515e-01, // atan(0.5)hi 0x3FDDAC67, 0x0561BB4F
        7.85398163397448278999e-01, // atan(1.0)hi 0x3FE921FB, 0x54442D18
        9.82793723247329054082e-01, // atan(1.5)hi 0x3FEF730B, 0xD281F69B
        1.57079632679489655800e+00, // atan(inf)hi 0x3FF921FB, 0x54442D18
    };
    const atanlo: []const f64 = &.{
        2.26987774529616870924e-17, // atan(0.5)lo 0x3C7A2B7F, 0x222F65E2
        3.06161699786838301793e-17, // atan(1.0)lo 0x3C81A626, 0x33145C07
        1.39033110312309984516e-17, // atan(1.5)lo 0x3C700788, 0x7AF0CBBD
        6.12323399573676603587e-17, // atan(inf)lo 0x3C91A626, 0x33145C07
    };
    const aT: []const f64 = &.{
        3.33333333333329318027e-01, // 0x3FD55555, 0x5555550D
        -1.99999999998764832476e-01, // 0xBFC99999, 0x9998EBC4
        1.42857142725034663711e-01, // 0x3FC24924, 0x920083FF
        -1.11111104054623557880e-01, // 0xBFBC71C6, 0xFE231671
        9.09088713343650656196e-02, // 0x3FB745CD, 0xC54C206E
        -7.69187620504482999495e-02, // 0xBFB3B0F2, 0xAF749A6D
        6.66107313738753120669e-02, // 0x3FB10D66, 0xA0D03D51
        -5.83357013379057348645e-02, // 0xBFADDE2D, 0x52DEFD9A
        4.97687799461593236017e-02, // 0x3FA97B4B, 0x24760DEB
        -3.65315727442169155270e-02, // 0xBFA2B444, 0x2C6A6C2F
        1.62858201153657823623e-02, // 0x3F90AD3A, 0xE322DA11
    };

    const hx: u64 = @bitCast(x);
    const ix: u32 = @truncate((hx >> 32) & 0x7fffffff);
    const sign = (hx >> 63) != 0;
    // if |x| >= 2^66
    if (ix >= 0x44100000) {
        if (math.isNan(x)) {
            return x;
        }
        const z = atanhi[3] + 0x1p-120;
        return if (sign) -z else z;
    }
    const x_, const id: ?usize = blk: {
        // |x| < 0.4375
        if (ix < 0x3fdc_0000) {
            // |x| < 2^(-27)
            if (ix < 0x3e40_0000) {
                if (ix < 0x0010_0000) {
                    // raise underflow for subnormal x
                    mem.doNotOptimizeAway(@as(f32, @floatCast(x)));
                }
                return x;
            }
            break :blk .{ x, null };
        } else {
            const x_ = @abs(x);
            // |x| < 1.1875
            if (ix < 0x3ff3_0000) {
                // 7/16 <= |x| < 11/16
                if (ix < 0x3fe6_0000) {
                    break :blk .{ (2.0 * x_ - 1.0) / (2.0 + x_), 0 };
                }
                // 11/16 <= |x| < 19/16
                else {
                    break :blk .{ (x_ - 1.0) / (x_ + 1.0), 1 };
                }
            } else {
                // |x| < 2.4375
                if (ix < 0x4003_8000) {
                    break :blk .{ (x_ - 1.5) / (1.0 + 1.5 * x_), 2 };
                }
                // 2.4375 <= |x| < 2^66
                else {
                    break :blk .{ -1.0 / x_, 3 };
                }
            }
        }
    };
    // end of argument reduction
    const z = x_ * x_;
    const w = z * z;
    // break sum from i=0 to 10 aT[i]z^(i+1) into odd and even poly
    const s1 = z * (aT[0] + w * (aT[2] + w * (aT[4] + w * (aT[6] + w * (aT[8] + w * aT[10])))));
    const s2 = w * (aT[1] + w * (aT[3] + w * (aT[5] + w * (aT[7] + w * aT[9]))));
    if (id) |id_| {
        const z_ = atanhi[id_] - (x_ * (s1 + s2) - atanlo[id_] - x_);
        return if (sign) -z_ else z_;
    } else {
        return x_ - x_ * (s1 + s2);
    }
}

fn atanExtended80(x: f80) f80 {
    const atanhi: []const f80 = &.{
        4.63647609000806116202e-01,
        7.85398163397448309628e-01,
        9.82793723247329067960e-01,
        1.57079632679489661926e+00,
    };
    const atanlo: []const f80 = &.{
        1.18469937025062860669e-20,
        -1.25413940316708300586e-20,
        2.55232234165405176172e-20,
        -2.50827880633416601173e-20,
    };
    const aT: []const f80 = &.{
        3.33333333333333333017e-01,
        -1.99999999999999632011e-01,
        1.42857142857046531280e-01,
        -1.11111111100562372733e-01,
        9.09090902935647302252e-02,
        -7.69230552476207730353e-02,
        6.66661718042406260546e-02,
        -5.88158892835030888692e-02,
        5.25499891539726639379e-02,
        -4.70119845393155721494e-02,
        4.03539201366454414072e-02,
        -2.91303858419364158725e-02,
        1.24822046299269234080e-02,
    };

    const hx: u80 = @bitCast(x);
    const se: u16 = @truncate(hx >> 64);
    const e = se & 0x7fff;
    const sign = se >> 15 != 0;
    // if |x| is large, atan(x)~=pi/2
    if (e >= 0x3fff + math.floatMantissaBits(f80) + 1) {
        if (math.isNan(x)) {
            return x;
        }
        return if (sign) -atanhi[3] else atanhi[3];
    }
    // Extract the exponent and the first few bits of the mantissa.
    const m: u64 = @truncate(hx & 0x0000_ffff_ffff_ffff_ffff);
    const expman = ((@as(u32, @intCast(se)) & 0x7fff) << 8) | (@as(u32, @truncate(m >> 55)) & 0xff);
    const x_, const id: ?usize = blk: {
        // |x| < 0.4375
        if (expman < ((0x3fff - 2) << 8) + 0xc0) {
            // if |x| is small, atanl(x)~=x
            if (e < 0x3fff - (math.floatMantissaBits(f80) + 1) / 2) {
                // raise underflow if subnormal
                if (e == 0) {
                    std.mem.doNotOptimizeAway(@as(f32, @floatCast(x)));
                }
                return x;
            }
            break :blk .{ x, null };
        } else {
            const x_ = @abs(x);
            // |x| < 1.1875
            if (expman < (0x3fff << 8) + 0x30) {
                // 7/16 <= |x| < 11/16
                if (expman < ((0x3fff - 1) << 8) + 0x60) {
                    break :blk .{ (2.0 * x_ - 1.0) / (2.0 + x_), 0 };
                }
                // 11/16 <= |x| < 19/16
                else {
                    break :blk .{ (x_ - 1.0) / (x_ + 1.0), 1 };
                }
            } else {
                // |x| < 2.4375
                if (expman < ((0x3fff + 1) << 8) + 0x38) {
                    break :blk .{ (x_ - 1.5) / (1.0 + 1.5 * x_), 2 };
                }
                // 2.4375 <= |x|
                else {
                    break :blk .{ -1.0 / x_, 3 };
                }
            }
        }
    };
    // end of argument reduction
    const z = x_ * x_;
    const w = z * z;
    // break sum aT[i]z^(i+1) into odd and even poly
    const s1 = z * (aT[0] + w * (aT[2] + w * (aT[4] + w * (aT[6] + w * (aT[8] + w * (aT[10] + w * aT[12]))))));
    const s2 = w * (aT[1] + w * (aT[3] + w * (aT[5] + w * (aT[7] + w * (aT[9] + w * aT[11])))));
    if (id) |id_| {
        const z_ = atanhi[id_] - ((x_ * (s1 + s2) - atanlo[id_]) - x_);
        return if (sign) -z_ else z_;
    } else {
        return x_ - x_ * (s1 + s2);
    }
}

fn atanBinary128(x: f128) f128 {
    const atanhi: []const f128 = &.{
        4.63647609000806116214256231461214397e-01,
        7.85398163397448309615660845819875699e-01,
        9.82793723247329067985710611014666038e-01,
        1.57079632679489661923132169163975140e+00,
    };
    const atanlo: []const f128 = &.{
        4.89509642257333492668618435220297706e-36,
        2.16795253253094525619926100651083806e-35,
        -2.31288434538183565909319952098066272e-35,
        4.33590506506189051239852201302167613e-35,
    };
    const aT: []const f128 = &.{
        3.33333333333333333333333333333333125e-01,
        -1.99999999999999999999999999999180430e-01,
        1.42857142857142857142857142125269827e-01,
        -1.11111111111111111111110834490810169e-01,
        9.09090909090909090908522355708623681e-02,
        -7.69230769230769230696553844935357021e-02,
        6.66666666666666660390096773046256096e-02,
        -5.88235294117646671706582985209643694e-02,
        5.26315789473666478515847092020327506e-02,
        -4.76190476189855517021024424991436144e-02,
        4.34782608678695085948531993458097026e-02,
        -3.99999999632663469330634215991142368e-02,
        3.70370363987423702891250829918659723e-02,
        -3.44827496515048090726669907612335954e-02,
        3.22579620681420149871973710852268528e-02,
        -3.03020767654269261041647570626778067e-02,
        2.85641979882534783223403715930946138e-02,
        -2.69824879726738568189929461383741323e-02,
        2.54194698498808542954187110873675769e-02,
        -2.35083879708189059926183138130183215e-02,
        2.04832358998165364349957325067131428e-02,
        -1.54489555488544397858507248612362957e-02,
        8.64492360989278761493037861575248038e-03,
        -2.58521121597609872727919154569765469e-03,
    };

    const hx: u128 = @bitCast(x);
    const se: u16 = @truncate(hx >> 112);
    const e = se & 0x7fff;
    const sign = se >> 15 != 0;
    // if |x| is large, atan(x)~=pi/2
    if (e >= 0x3fff + math.floatMantissaBits(f128) + 2) {
        if (math.isNan(x)) {
            return x;
        }
        return if (sign) -atanhi[3] else atanhi[3];
    }
    // Extract the exponent and the first few bits of the mantissa.
    const top: u16 = @truncate((hx >> 96) & 0x0000_ffff);
    const expman = ((@as(u32, @intCast(se)) & 0x7fff) << 8) | (@as(u32, @intCast(top)) >> 8);
    const x_, const id: ?usize = blk: {
        // |x| < 0.4375
        if (expman < ((0x3fff - 2) << 8) + 0xc0) {
            // if |x| is small, atanl(x)~=x
            if (e < 0x3fff - (math.floatMantissaBits(f128) + 2) / 2) {
                // raise underflow if subnormal
                if (e == 0) {
                    mem.doNotOptimizeAway(@as(f32, @floatCast(x)));
                }
                return x;
            }
            break :blk .{ x, null };
        } else {
            const x_ = @abs(x);
            // |x| < 1.1875
            if (expman < (0x3fff << 8) + 0x30) {
                // 7/16 <= |x| < 11/16
                if (expman < ((0x3fff - 1) << 8) + 0x60) {
                    break :blk .{ (2.0 * x_ - 1.0) / (2.0 + x_), 0 };
                }
                // 11/16 <= |x| < 19/16
                else {
                    break :blk .{ (x_ - 1.0) / (x_ + 1.0), 1 };
                }
            } else {
                // |x| < 2.4375
                if (expman < ((0x3fff + 1) << 8) + 0x38) {
                    break :blk .{ (x_ - 1.5) / (1.0 + 1.5 * x_), 2 };
                }
                // 2.4375 <= |x|
                else {
                    break :blk .{ -1.0 / x_, 3 };
                }
            }
        }
    };
    // end of argument reduction
    const z = x_ * x_;
    const w = z * z;
    // break sum aT[i]z^(i+1) into odd and even poly
    const s1 = z * (aT[0] + w * (aT[2] + w * (aT[4] + w * (aT[6] + w * (aT[8] + w * (aT[10] + w * (aT[12] + w * (aT[14] + w * (aT[16] + w * (aT[18] + w * (aT[20] + w * aT[22])))))))))));
    const s2 = w * (aT[1] + w * (aT[3] + w * (aT[5] + w * (aT[7] + w * (aT[9] + w * (aT[11] + w * (aT[13] + w * (aT[15] + w * (aT[17] + w * (aT[19] + w * (aT[21] + w * aT[23])))))))))));
    if (id) |id_| {
        const z_ = atanhi[id_] - ((x_ * (s1 + s2) - atanlo[id_]) - x_);
        return if (sign) -z_ else z_;
    } else {
        return x_ - x_ * (s1 + s2);
    }
}

test "atanBinary16.special" {
    try testing.expectEqual(0x0p+0, atanBinary16(0x0p+0));
    try testing.expectEqual(-0x0p+0, atanBinary16(-0x0p+0));
    try testing.expectApproxEqAbs(0x1.92p-1, atanBinary16(0x1p+0), math.floatEpsAt(f16, 0x1.92p-1));
    try testing.expectApproxEqAbs(-0x1.92p-1, atanBinary16(-0x1p+0), math.floatEpsAt(f16, -0x1.92p-1));
    try testing.expectApproxEqAbs(0x1.92p0, atanBinary16(math.inf(f16)), math.floatEpsAt(f16, 0x1.92p0));
    try testing.expectApproxEqAbs(-0x1.92p0, atanBinary16(-math.inf(f16)), math.floatEpsAt(f16, -0x1.92p0));
    try testing.expect(math.isNan(atanBinary16(math.nan(f16))));
}

test "atanBinary16" {
    try testing.expectApproxEqAbs(-0x1.74cp-2, atanBinary16(-0x1.864p-2), math.floatEpsAt(f16, -0x1.74cp-2));
    try testing.expectApproxEqAbs(-0x1.374p0, atanBinary16(-0x1.59cp1), math.floatEpsAt(f16, -0x1.374p0));
    try testing.expectApproxEqAbs(-0x1.11cp0, atanBinary16(-0x1.d2cp0), math.floatEpsAt(f16, -0x1.11cp0));
    try testing.expectApproxEqAbs(-0x1.33cp-1, atanBinary16(-0x1.5f4p-1), math.floatEpsAt(f16, -0x1.33cp-1));
    try testing.expectApproxEqAbs(0x1.37p0, atanBinary16(0x1.588p1), math.floatEpsAt(f16, 0x1.37p0));
    try testing.expectApproxEqAbs(-0x1.99cp-2, atanBinary16(-0x1.b14p-2), math.floatEpsAt(f16, -0x1.99cp-2));
    try testing.expectApproxEqAbs(0x1.2fcp0, atanBinary16(0x1.3ccp1), math.floatEpsAt(f16, 0x1.2fcp0));
    try testing.expectApproxEqAbs(-0x1.08cp-2, atanBinary16(-0x1.0ecp-2), math.floatEpsAt(f16, -0x1.08cp-2));
    try testing.expectApproxEqAbs(0x1.2ap0, atanBinary16(0x1.298p1), math.floatEpsAt(f16, 0x1.2ap0));
    try testing.expectApproxEqAbs(-0x1.1c8p0, atanBinary16(-0x1.028p1), math.floatEpsAt(f16, -0x1.1c8p0));
}

test "atanBinary32.special" {
    try testing.expectEqual(0x0p+0, atanBinary32(0x0p+0));
    try testing.expectEqual(-0x0p+0, atanBinary32(-0x0p+0));
    try testing.expectApproxEqAbs(0x1.921fb6p-1, atanBinary32(0x1p+0), math.floatEpsAt(f32, 0x1.921fb6p-1));
    try testing.expectApproxEqAbs(-0x1.921fb6p-1, atanBinary32(-0x1p+0), math.floatEpsAt(f32, -0x1.921fb6p-1));
    try testing.expectApproxEqAbs(0x1.921fb6p+0, atanBinary32(math.inf(f32)), math.floatEpsAt(f32, 0x1.921fb6p+0));
    try testing.expectApproxEqAbs(-0x1.921fb6p+0, atanBinary32(-math.inf(f32)), math.floatEpsAt(f32, -0x1.921fb6p+0));
    try testing.expect(math.isNan(atanBinary32(math.nan(f32))));
}

test "atanBinary32" {
    try testing.expectApproxEqAbs(-0x1.74c62p-2, atanBinary32(-0x1.8629dp-2), math.floatEpsAt(f32, -0x1.74c62p-2));
    try testing.expectApproxEqAbs(-0x1.375fd8p0, atanBinary32(-0x1.59d42ep1), math.floatEpsAt(f32, -0x1.375fd8p0));
    try testing.expectApproxEqAbs(-0x1.11b8aep0, atanBinary32(-0x1.d2dbe2p0), math.floatEpsAt(f32, -0x1.11b8aep0));
    try testing.expectApproxEqAbs(-0x1.33d28cp-1, atanBinary32(-0x1.5f314ep-1), math.floatEpsAt(f32, -0x1.33d28cp-1));
    try testing.expectApproxEqAbs(0x1.37082ep0, atanBinary32(0x1.5869bp1), math.floatEpsAt(f32, 0x1.37082ep0));
    try testing.expectApproxEqAbs(-0x1.99d7cap-2, atanBinary32(-0x1.b13a06p-2), math.floatEpsAt(f32, -0x1.99d7cap-2));
    try testing.expectApproxEqAbs(0x1.2fcb12p0, atanBinary32(0x1.3cb0f2p1), math.floatEpsAt(f32, 0x1.2fcb12p0));
    try testing.expectApproxEqAbs(-0x1.08c71ap-2, atanBinary32(-0x1.0ed746p-2), math.floatEpsAt(f32, -0x1.08c71ap-2));
    try testing.expectApproxEqAbs(0x1.2a24e2p0, atanBinary32(0x1.299d54p1), math.floatEpsAt(f32, 0x1.2a24e2p0));
    try testing.expectApproxEqAbs(-0x1.1c6178p0, atanBinary32(-0x1.0264fcp1), math.floatEpsAt(f32, -0x1.1c6178p0));
}

test "atanBinary64.special" {
    try testing.expectEqual(0x0p+0, atanBinary64(0x0p+0));
    try testing.expectEqual(-0x0p+0, atanBinary64(-0x0p+0));
    try testing.expectApproxEqAbs(0x1.921fb54442d18p-1, atanBinary64(0x1p+0), math.floatEpsAt(f64, 0x1.921fb54442d18p-1));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18p-1, atanBinary64(-0x1p+0), math.floatEpsAt(f64, -0x1.921fb54442d18p-1));
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+0, atanBinary64(math.inf(f64)), math.floatEpsAt(f64, 0x1.921fb54442d18p+0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18p+0, atanBinary64(-math.inf(f64)), math.floatEpsAt(f64, -0x1.921fb54442d18p+0));
    try testing.expect(math.isNan(atanBinary64(math.nan(f64))));
}

test "atanBinary64" {
    try testing.expectApproxEqAbs(-0x1.74c61f4377016p-2, atanBinary64(-0x1.8629d0244cdccp-2), math.floatEpsAt(f64, -0x1.74c61f4377016p-2));
    try testing.expectApproxEqAbs(-0x1.375fd7987cc2p0, atanBinary64(-0x1.59d42d4659937p1), math.floatEpsAt(f64, -0x1.375fd7987cc2p0));
    try testing.expectApproxEqAbs(-0x1.11b8adeba5616p0, atanBinary64(-0x1.d2dbe23d04f06p0), math.floatEpsAt(f64, -0x1.11b8adeba5616p0));
    try testing.expectApproxEqAbs(-0x1.33d28ca762539p-1, atanBinary64(-0x1.5f314e72398e8p-1), math.floatEpsAt(f64, -0x1.33d28ca762539p-1));
    try testing.expectApproxEqAbs(0x1.37082ce2dd03p0, atanBinary64(0x1.5869af37b7d08p1), math.floatEpsAt(f64, 0x1.37082ce2dd03p0));
    try testing.expectApproxEqAbs(-0x1.99d7cac66dd44p-2, atanBinary64(-0x1.b13a05a662618p-2), math.floatEpsAt(f64, -0x1.99d7cac66dd44p-2));
    try testing.expectApproxEqAbs(0x1.2fcb120468e8ep0, atanBinary64(0x1.3cb0f12f39d8ap1), math.floatEpsAt(f64, 0x1.2fcb120468e8ep0));
    try testing.expectApproxEqAbs(-0x1.08c71aa0e509p-2, atanBinary64(-0x1.0ed746b39cbb7p-2), math.floatEpsAt(f64, -0x1.08c71aa0e509p-2));
    try testing.expectApproxEqAbs(0x1.2a24e22d861dfp0, atanBinary64(0x1.299d54ac7d6bp1), math.floatEpsAt(f64, 0x1.2a24e22d861dfp0));
    try testing.expectApproxEqAbs(-0x1.1c617825f9751p0, atanBinary64(-0x1.0264fb9f3d50ep1), math.floatEpsAt(f64, -0x1.1c617825f9751p0));
}

test "atanExtended80.special" {
    try testing.expectEqual(0x0p+0, atanExtended80(0x0p+0));
    try testing.expectEqual(-0x0p+0, atanExtended80(-0x0p+0));
    try testing.expectApproxEqAbs(0x1.921fb54442d1846ap-1, atanExtended80(0x1p+0), math.floatEpsAt(f80, 0x1.921fb54442d1846ap-1));
    try testing.expectApproxEqAbs(-0x1.921fb54442d1846ap-1, atanExtended80(-0x1p+0), math.floatEpsAt(f80, -0x1.921fb54442d1846ap-1));
    try testing.expectApproxEqAbs(0x1.921fb54442d1846ap0, atanExtended80(math.inf(f80)), math.floatEpsAt(f80, 0x1.921fb54442d1846ap0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d1846ap0, atanExtended80(-math.inf(f80)), math.floatEpsAt(f80, -0x1.921fb54442d1846ap0));
    try testing.expect(math.isNan(atanExtended80(math.nan(f80))));
}

test "atanExtended80" {
    try testing.expectApproxEqAbs(-0x1.74c61f437701661p-2, atanExtended80(-0x1.8629d0244cdcbed8p-2), math.floatEpsAt(f80, -0x1.74c61f437701661p-2));
    try testing.expectApproxEqAbs(-0x1.375fd7987cc1fd02p0, atanExtended80(-0x1.59d42d4659936d9ep1), math.floatEpsAt(f80, -0x1.375fd7987cc1fd02p0));
    try testing.expectApproxEqAbs(-0x1.11b8adeba5615e04p0, atanExtended80(-0x1.d2dbe23d04f067b4p0), math.floatEpsAt(f80, -0x1.11b8adeba5615e04p0));
    try testing.expectApproxEqAbs(-0x1.33d28ca76253964cp-1, atanExtended80(-0x1.5f314e72398e7dbcp-1), math.floatEpsAt(f80, -0x1.33d28ca76253964cp-1));
    try testing.expectApproxEqAbs(0x1.37082ce2dd03010cp0, atanExtended80(0x1.5869af37b7d078cap1), math.floatEpsAt(f80, 0x1.37082ce2dd03010cp0));
    try testing.expectApproxEqAbs(-0x1.99d7cac66dd4438p-2, atanExtended80(-0x1.b13a05a66261821ap-2), math.floatEpsAt(f80, -0x1.99d7cac66dd4438p-2));
    try testing.expectApproxEqAbs(0x1.2fcb120468e8d9ecp0, atanExtended80(0x1.3cb0f12f39d899cp1), math.floatEpsAt(f80, 0x1.2fcb120468e8d9ecp0));
    try testing.expectApproxEqAbs(-0x1.08c71aa0e5090998p-2, atanExtended80(-0x1.0ed746b39cbb7614p-2), math.floatEpsAt(f80, -0x1.08c71aa0e5090998p-2));
    try testing.expectApproxEqAbs(0x1.2a24e22d861debfep0, atanExtended80(0x1.299d54ac7d6afc52p1), math.floatEpsAt(f80, 0x1.2a24e22d861debfep0));
    try testing.expectApproxEqAbs(-0x1.1c617825f97512b8p0, atanExtended80(-0x1.0264fb9f3d50e4fp1), math.floatEpsAt(f80, -0x1.1c617825f97512b8p0));
}

test "atanBinary128.special" {
    try testing.expectEqual(0x0p+0, atanBinary128(0x0p+0));
    try testing.expectEqual(-0x0p+0, atanBinary128(-0x0p+0));
    try testing.expectApproxEqAbs(0x1.921fb54442d18469898cc51701b8p-1, atanBinary128(0x1p+0), math.floatEpsAt(f128, 0x1.921fb54442d18469898cc51701b8p-1));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18469898cc51701b8p-1, atanBinary128(-0x1p+0), math.floatEpsAt(f128, -0x1.921fb54442d18469898cc51701b8p-1));
    try testing.expectApproxEqAbs(0x1.921fb54442d18469898cc51701b8p0, atanBinary128(math.inf(f128)), math.floatEpsAt(f128, 0x1.921fb54442d18469898cc51701b8p0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18469898cc51701b8p0, atanBinary128(-math.inf(f128)), math.floatEpsAt(f128, -0x1.921fb54442d18469898cc51701b8p0));
    try testing.expect(math.isNan(atanBinary128(math.nan(f128))));
}

test "atanBinary128" {
    try testing.expectApproxEqAbs(-0x1.74c61f437701660ff76989d23707p-2, atanBinary128(-0x1.8629d0244cdcbed71792ccdec26dp-2), math.floatEpsAt(f128, -0x1.74c61f437701660ff76989d23707p-2));
    try testing.expectApproxEqAbs(-0x1.375fd7987cc1fd0119cf0cc5b708p0, atanBinary128(-0x1.59d42d4659936d9e22b5dea4faefp1), math.floatEpsAt(f128, -0x1.375fd7987cc1fd0119cf0cc5b708p0));
    try testing.expectApproxEqAbs(-0x1.11b8adeba5615e0370722b511231p0, atanBinary128(-0x1.d2dbe23d04f067b42da3f8efdf57p0), math.floatEpsAt(f128, -0x1.11b8adeba5615e0370722b511231p0));
    try testing.expectApproxEqAbs(-0x1.33d28ca76253964cb5d3581cdd88p-1, atanBinary128(-0x1.5f314e72398e7dbbe70fb072983ep-1), math.floatEpsAt(f128, -0x1.33d28ca76253964cb5d3581cdd88p-1));
    try testing.expectApproxEqAbs(0x1.37082ce2dd03010bbea814dc5882p0, atanBinary128(0x1.5869af37b7d078caa3456c44aecep1), math.floatEpsAt(f128, 0x1.37082ce2dd03010bbea814dc5882p0));
    try testing.expectApproxEqAbs(-0x1.99d7cac66dd4438077284b491a91p-2, atanBinary128(-0x1.b13a05a66261821a364ad8c6c999p-2), math.floatEpsAt(f128, -0x1.99d7cac66dd4438077284b491a91p-2));
    try testing.expectApproxEqAbs(0x1.2fcb120468e8d9ebdb74702314c8p0, atanBinary128(0x1.3cb0f12f39d899c0d963ac413297p1), math.floatEpsAt(f128, 0x1.2fcb120468e8d9ebdb74702314c8p0));
    try testing.expectApproxEqAbs(-0x1.08c71aa0e5090998206fbbe2090fp-2, atanBinary128(-0x1.0ed746b39cbb7614d8735e8315a8p-2), math.floatEpsAt(f128, -0x1.08c71aa0e5090998206fbbe2090fp-2));
    try testing.expectApproxEqAbs(0x1.2a24e22d861debfd6f974500567fp0, atanBinary128(0x1.299d54ac7d6afc5154643b601519p1), math.floatEpsAt(f128, 0x1.2a24e22d861debfd6f974500567fp0));
    try testing.expectApproxEqAbs(-0x1.1c617825f97512b7f38656ab12cdp0, atanBinary128(-0x1.0264fb9f3d50e4f0f966f0686064p1), math.floatEpsAt(f128, -0x1.1c617825f97512b7f38656ab12cdp0));
}

fn atanBinary32Vec(comptime vec_len: comptime_int, x: @Vector(vec_len, f32)) @TypeOf(x) {
    const sign_mask: @Vector(vec_len, u32) = @splat(0x80000000);
    const neg_one: @Vector(vec_len, f32) = @splat(-1.0);
    const pi_over_2: @Vector(vec_len, u32) = @splat(0x3fc90fdb);
    const zero: @Vector(vec_len, u32) = @splat(0);
    const c0: @Vector(vec_len, f32) = @splat(-0x1.5554dcp-2);
    const c1: @Vector(vec_len, f32) = @splat(0x1.9978ecp-3);
    const c2: @Vector(vec_len, f32) = @splat(-0x1.230a94p-3);
    const c3: @Vector(vec_len, f32) = @splat(0x1.b4debp-4);
    const c4: @Vector(vec_len, f32) = @splat(-0x1.3550dap-4);
    const c5: @Vector(vec_len, f32) = @splat(0x1.61eebp-5);
    const c6: @Vector(vec_len, f32) = @splat(-0x1.0c17d4p-6);
    const c7: @Vector(vec_len, f32) = @splat(0x1.7ea694p-9);

    const ix: @Vector(vec_len, u32) = @bitCast(x);
    const sign = ix & sign_mask;
    const pred = @abs(x) > @abs(neg_one);
    const z = @select(f32, pred, neg_one / x, x);
    const shift: @Vector(vec_len, f32) = @bitCast(@select(u32, pred, pi_over_2 ^ sign, zero));
    const z2 = z * z;
    const z3 = z * z2;
    const z4 = z2 * z2;
    const z8 = z4 * z4;
    const p0_1 = @mulAdd(@Vector(vec_len, f32), z2, c1, c0);
    const p2_3 = @mulAdd(@Vector(vec_len, f32), z2, c3, c2);
    const p4_5 = @mulAdd(@Vector(vec_len, f32), z2, c5, c4);
    const p6_7 = @mulAdd(@Vector(vec_len, f32), z2, c7, c6);
    const p0_3 = @mulAdd(@Vector(vec_len, f32), z4, p2_3, p0_1);
    const p4_7 = @mulAdd(@Vector(vec_len, f32), z4, p6_7, p4_5);
    const p0_7 = @mulAdd(@Vector(vec_len, f32), z8, p4_7, p0_3);
    return @mulAdd(@Vector(vec_len, f32), z3, p0_7, shift + z);
}

fn atanBinary64Vec(comptime vec_len: comptime_int, x: @Vector(vec_len, f64)) @TypeOf(x) {
    const sign_mask: @Vector(vec_len, u64) = @splat(0x8000000000000000);
    const neg_one: @Vector(vec_len, f64) = @splat(-1.0);
    const pi_over_2: @Vector(vec_len, u64) = @splat(0x3ff921fb54442d18);
    const zero: @Vector(vec_len, u64) = @splat(0);
    const c0: @Vector(vec_len, f64) = @splat(-0x1.555555555552ap-2);
    const c1: @Vector(vec_len, f64) = @splat(0x1.9999999995aebp-3);
    const c2: @Vector(vec_len, f64) = @splat(-0x1.24924923923f6p-3);
    const c3: @Vector(vec_len, f64) = @splat(0x1.c71c7184288a2p-4);
    const c4: @Vector(vec_len, f64) = @splat(-0x1.745d11fb3d32bp-4);
    const c5: @Vector(vec_len, f64) = @splat(0x1.3b136a18051b9p-4);
    const c6: @Vector(vec_len, f64) = @splat(-0x1.110e6d985f496p-4);
    const c7: @Vector(vec_len, f64) = @splat(0x1.e1bcf7f08801dp-5);
    const c8: @Vector(vec_len, f64) = @splat(-0x1.ae644e28058c3p-5);
    const c9: @Vector(vec_len, f64) = @splat(0x1.82eeb1fed85c6p-5);
    const c10: @Vector(vec_len, f64) = @splat(-0x1.59d7f901566cbp-5);
    const c11: @Vector(vec_len, f64) = @splat(0x1.2c982855ab069p-5);
    const c12: @Vector(vec_len, f64) = @splat(-0x1.eb49592998177p-6);
    const c13: @Vector(vec_len, f64) = @splat(0x1.69d8b396e3d38p-6);
    const c14: @Vector(vec_len, f64) = @splat(-0x1.ca980345c4204p-7);
    const c15: @Vector(vec_len, f64) = @splat(0x1.dc050eafde0b3p-8);
    const c16: @Vector(vec_len, f64) = @splat(-0x1.7ea70755b8eccp-9);
    const c17: @Vector(vec_len, f64) = @splat(0x1.ba3da3de903e8p-11);
    const c18: @Vector(vec_len, f64) = @splat(-0x1.44a4b059b6f67p-13);
    const c19: @Vector(vec_len, f64) = @splat(0x1.c4a45029e5a91p-17);

    const ix: @Vector(vec_len, u64) = @bitCast(x);
    const sign = ix & sign_mask;
    const pred = @abs(x) > @abs(neg_one);
    const shift: @Vector(vec_len, f64) = @bitCast(@select(u64, pred, pi_over_2 ^ sign, zero));
    const z = @select(f64, pred, neg_one / x, x);
    const z2 = z * z;
    const z3 = z * z2;
    const z4 = z2 * z2;
    const z8 = z4 * z4;
    const z16 = z8 * z8;
    const p0_1 = @mulAdd(@Vector(vec_len, f64), z2, c1, c0);
    const p2_3 = @mulAdd(@Vector(vec_len, f64), z2, c3, c2);
    const p0_3 = @mulAdd(@Vector(vec_len, f64), z4, p2_3, p0_1);
    const p4_5 = @mulAdd(@Vector(vec_len, f64), z2, c5, c4);
    const p6_7 = @mulAdd(@Vector(vec_len, f64), z2, c7, c6);
    const p4_7 = @mulAdd(@Vector(vec_len, f64), z4, p6_7, p4_5);
    const p0_7 = @mulAdd(@Vector(vec_len, f64), z8, p4_7, p0_3);
    const p8_9 = @mulAdd(@Vector(vec_len, f64), z2, c9, c8);
    const p10_11 = @mulAdd(@Vector(vec_len, f64), z2, c11, c10);
    const p8_11 = @mulAdd(@Vector(vec_len, f64), z4, p10_11, p8_9);
    const p12_13 = @mulAdd(@Vector(vec_len, f64), z2, c13, c12);
    const p14_15 = @mulAdd(@Vector(vec_len, f64), z2, c15, c14);
    const p12_15 = @mulAdd(@Vector(vec_len, f64), z4, p14_15, p12_13);
    const p16_17 = @mulAdd(@Vector(vec_len, f64), z2, c17, c16);
    const p18_19 = @mulAdd(@Vector(vec_len, f64), z2, c19, c18);
    const p16_19 = @mulAdd(@Vector(vec_len, f64), z4, p18_19, p16_17);
    const p8_15 = @mulAdd(@Vector(vec_len, f64), z8, p12_15, p8_11);
    const p8_19 = @mulAdd(@Vector(vec_len, f64), z16, p16_19, p8_15);
    const p0_19 = @mulAdd(@Vector(vec_len, f64), p8_19, z16, p0_7);
    return @mulAdd(@Vector(vec_len, f64), z3, p0_19, shift + z);
}

test "atanBinary32Vec.special" {
    const input: @Vector(7, f32) = .{
        0x0p+0,
        -0x0p+0,
        0x1p+0,
        -0x1p+0,
        math.inf(f32),
        -math.inf(f32),
        math.nan(f32),
    };
    const output = atanBinary32Vec(7, input);
    try testing.expectEqual(0x0p+0, output[0]);
    try testing.expectEqual(-0x0p+0, output[1]);
    try testing.expectApproxEqAbs(0x1.921fb6p-1, output[2], math.floatEpsAt(f32, 0x1.921fb6p-1));
    try testing.expectApproxEqAbs(-0x1.921fb6p-1, output[3], math.floatEpsAt(f32, -0x1.921fb6p-1));
    try testing.expectApproxEqAbs(0x1.921fb6p+0, output[4], math.floatEpsAt(f32, 0x1.921fb6p+0));
    try testing.expectApproxEqAbs(-0x1.921fb6p+0, output[5], math.floatEpsAt(f32, -0x1.921fb6p+0));
    try testing.expect(math.isNan(output[6]));
}

test "atanBinary32Vec" {
    const input: @Vector(10, f32) = .{
        -0x1.8629dp-2,
        -0x1.59d42ep1,
        -0x1.d2dbe2p0,
        -0x1.5f314ep-1,
        0x1.5869bp1,
        -0x1.b13a06p-2,
        0x1.3cb0f2p1,
        -0x1.0ed746p-2,
        0x1.299d54p1,
        -0x1.0264fcp1,
    };
    const output = atanBinary32Vec(10, input);
    try testing.expectApproxEqAbs(-0x1.74c62p-2, output[0], math.floatEpsAt(f32, -0x1.74c62p-2));
    try testing.expectApproxEqAbs(-0x1.375fd8p0, output[1], math.floatEpsAt(f32, -0x1.375fd8p0));
    try testing.expectApproxEqAbs(-0x1.11b8aep0, output[2], math.floatEpsAt(f32, -0x1.11b8aep0));
    try testing.expectApproxEqAbs(-0x1.33d28cp-1, output[3], math.floatEpsAt(f32, -0x1.33d28cp-1));
    try testing.expectApproxEqAbs(0x1.37082ep0, output[4], math.floatEpsAt(f32, 0x1.37082ep0));
    try testing.expectApproxEqAbs(-0x1.99d7cap-2, output[5], math.floatEpsAt(f32, -0x1.99d7cap-2));
    try testing.expectApproxEqAbs(0x1.2fcb12p0, output[6], math.floatEpsAt(f32, 0x1.2fcb12p0));
    try testing.expectApproxEqAbs(-0x1.08c71ap-2, output[7], math.floatEpsAt(f32, -0x1.08c71ap-2));
    try testing.expectApproxEqAbs(0x1.2a24e2p0, output[8], math.floatEpsAt(f32, 0x1.2a24e2p0));
    try testing.expectApproxEqAbs(-0x1.1c6178p0, output[9], math.floatEpsAt(f32, -0x1.1c6178p0));
}

test "atanBinary64Vec.special" {
    const input: @Vector(7, f64) = .{
        0x0p+0,
        -0x0p+0,
        0x1p+0,
        -0x1p+0,
        math.inf(f64),
        -math.inf(f64),
        math.nan(f64),
    };
    const output = atanBinary64Vec(7, input);
    try testing.expectEqual(0x0p+0, output[0]);
    try testing.expectEqual(-0x0p+0, output[1]);
    try testing.expectApproxEqAbs(0x1.921fb54442d18p-1, output[2], math.floatEpsAt(f64, 0x1.921fb54442d18p-1));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18p-1, output[3], math.floatEpsAt(f64, -0x1.921fb54442d18p-1));
    try testing.expectApproxEqAbs(0x1.921fb54442d18p+0, output[4], math.floatEpsAt(f64, 0x1.921fb54442d18p+0));
    try testing.expectApproxEqAbs(-0x1.921fb54442d18p+0, output[5], math.floatEpsAt(f64, -0x1.921fb54442d18p+0));
    try testing.expect(math.isNan(output[6]));
}

test "atanBinary64Vec" {
    const input: @Vector(10, f64) = .{
        -0x1.8629d0244cdccp-2,
        -0x1.59d42d4659937p1,
        -0x1.d2dbe23d04f06p0,
        -0x1.5f314e72398e8p-1,
        0x1.5869af37b7d08p1,
        -0x1.b13a05a662618p-2,
        0x1.3cb0f12f39d8ap1,
        -0x1.0ed746b39cbb7p-2,
        0x1.299d54ac7d6bp1,
        -0x1.0264fb9f3d50ep1,
    };
    const output = atanBinary64Vec(10, input);
    try testing.expectApproxEqAbs(-0x1.74c61f4377016p-2, output[0], math.floatEpsAt(f64, -0x1.74c61f4377016p-2));
    try testing.expectApproxEqAbs(-0x1.375fd7987cc2p0, output[1], math.floatEpsAt(f64, -0x1.375fd7987cc2p0));
    try testing.expectApproxEqAbs(-0x1.11b8adeba5616p0, output[2], math.floatEpsAt(f64, -0x1.11b8adeba5616p0));
    try testing.expectApproxEqAbs(-0x1.33d28ca762539p-1, output[3], math.floatEpsAt(f64, -0x1.33d28ca762539p-1));
    try testing.expectApproxEqAbs(0x1.37082ce2dd03p0, output[4], math.floatEpsAt(f64, 0x1.37082ce2dd03p0));
    try testing.expectApproxEqAbs(-0x1.99d7cac66dd44p-2, output[5], math.floatEpsAt(f64, -0x1.99d7cac66dd44p-2));
    try testing.expectApproxEqAbs(0x1.2fcb120468e8ep0, output[6], math.floatEpsAt(f64, 0x1.2fcb120468e8ep0));
    try testing.expectApproxEqAbs(-0x1.08c71aa0e509p-2, output[7], math.floatEpsAt(f64, -0x1.08c71aa0e509p-2));
    try testing.expectApproxEqAbs(0x1.2a24e22d861dfp0, output[8], math.floatEpsAt(f64, 0x1.2a24e22d861dfp0));
    try testing.expectApproxEqAbs(-0x1.1c617825f9751p0, output[9], math.floatEpsAt(f64, -0x1.1c617825f9751p0));
}



---
File: /std/math/atan2.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/atan2f.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/atan2.c

const std = @import("../std.zig");
const math = std.math;
const expect = std.testing.expect;

/// Returns the arc-tangent of y/x.
///
///      Special Cases:
/// |   y   |   x   | radians |
/// |-------|-------|---------|
/// |  fin  |  nan  |   nan   |
/// |  nan  |  fin  |   nan   |
/// |  +0   | >=+0  |   +0    |
/// |  -0   | >=+0  |   -0    |
/// |  +0   | <=-0  |   pi    |
/// |  -0   | <=-0  |  -pi    |
/// |  pos  |   0   |  +pi/2  |
/// |  neg  |   0   |  -pi/2  |
/// | +inf  | +inf  |  +pi/4  |
/// | -inf  | +inf  |  -pi/4  |
/// | +inf  | -inf  |  3pi/4  |
/// | -inf  | -inf  | -3pi/4  |
/// |  fin  | +inf  |    0    |
/// |  pos  | -inf  |  +pi    |
/// |  neg  | -inf  |  -pi    |
/// | +inf  |  fin  |  +pi/2  |
/// | -inf  |  fin  |  -pi/2  |
pub fn atan2(y: anytype, x: anytype) @TypeOf(x, y) {
    const T = @TypeOf(x, y);
    return switch (T) {
        f32 => atan2_32(y, x),
        f64 => atan2_64(y, x),
        else => @compileError("atan2 not implemented for " ++ @typeName(T)),
    };
}

fn atan2_32(y: f32, x: f32) f32 {
    const pi: f32 = 3.1415927410e+00;
    const pi_lo: f32 = -8.7422776573e-08;

    if (math.isNan(x) or math.isNan(y)) {
        return x + y;
    }

    var ix = @as(u32, @bitCast(x));
    var iy = @as(u32, @bitCast(y));

    // x = 1.0
    if (ix == 0x3F800000) {
        return math.atan(y);
    }

    // 2 * sign(x) + sign(y)
    const m = ((iy >> 31) & 1) | ((ix >> 30) & 2);
    ix &= 0x7FFFFFFF;
    iy &= 0x7FFFFFFF;

    if (iy == 0) {
        switch (m) {
            0, 1 => return y, // atan(+-0, +...)
            2 => return pi, // atan(+0, -...)
            3 => return -pi, // atan(-0, -...)
            else => unreachable,
        }
    }

    if (ix == 0) {
        if (m & 1 != 0) {
            return -pi / 2;
        } else {
            return pi / 2;
        }
    }

    if (ix == 0x7F800000) {
        if (iy == 0x7F800000) {
            switch (m) {
                0 => return pi / 4, // atan(+inf, +inf)
                1 => return -pi / 4, // atan(-inf, +inf)
                2 => return 3 * pi / 4, // atan(+inf, -inf)
                3 => return -3 * pi / 4, // atan(-inf, -inf)
                else => unreachable,
            }
        } else {
            switch (m) {
                0 => return 0.0, // atan(+..., +inf)
                1 => return -0.0, // atan(-..., +inf)
                2 => return pi, // atan(+..., -inf)
                3 => return -pi, // atan(-...f, -inf)
                else => unreachable,
            }
        }
    }

    // |y / x| > 0x1p26
    if (ix + (26 << 23) < iy or iy == 0x7F800000) {
        if (m & 1 != 0) {
            return -pi / 2;
        } else {
            return pi / 2;
        }
    }

    // z = atan(|y / x|) with correct underflow
    const z = z: {
        if ((m & 2) != 0 and iy + (26 << 23) < ix) {
            break :z 0.0;
        } else {
            break :z math.atan(@abs(y / x));
        }
    };

    switch (m) {
        0 => return z, // atan(+, +)
        1 => return -z, // atan(-, +)
        2 => return pi - (z - pi_lo), // atan(+, -)
        3 => return (z - pi_lo) - pi, // atan(-, -)
        else => unreachable,
    }
}

fn atan2_64(y: f64, x: f64) f64 {
    const pi: f64 = 3.1415926535897931160E+00;
    const pi_lo: f64 = 1.2246467991473531772E-16;

    if (math.isNan(x) or math.isNan(y)) {
        return x + y;
    }

    const ux: u64 = @bitCast(x);
    var ix: u32 = @intCast(ux >> 32);
    const lx: u32 = @intCast(ux & 0xFFFFFFFF);

    const uy: u64 = @bitCast(y);
    var iy: u32 = @intCast(uy >> 32);
    const ly: u32 = @intCast(uy & 0xFFFFFFFF);

    // x = 1.0
    if ((ix -% 0x3FF00000) | lx == 0) {
        return math.atan(y);
    }

    // 2 * sign(x) + sign(y)
    const m = ((iy >> 31) & 1) | ((ix >> 30) & 2);
    ix &= 0x7FFFFFFF;
    iy &= 0x7FFFFFFF;

    if (iy | ly == 0) {
        switch (m) {
            0, 1 => return y, // atan(+-0, +...)
            2 => return pi, // atan(+0, -...)
            3 => return -pi, // atan(-0, -...)
            else => unreachable,
        }
    }

    if (ix | lx == 0) {
        if (m & 1 != 0) {
            return -pi / 2;
        } else {
            return pi / 2;
        }
    }

    if (ix == 0x7FF00000) {
        if (iy == 0x7FF00000) {
            switch (m) {
                0 => return pi / 4, // atan(+inf, +inf)
                1 => return -pi / 4, // atan(-inf, +inf)
                2 => return 3 * pi / 4, // atan(+inf, -inf)
                3 => return -3 * pi / 4, // atan(-inf, -inf)
                else => unreachable,
            }
        } else {
            switch (m) {
                0 => return 0.0, // atan(+..., +inf)
                1 => return -0.0, // atan(-..., +inf)
                2 => return pi, // atan(+..., -inf)
                3 => return -pi, // atan(-...f, -inf)
                else => unreachable,
            }
        }
    }

    // |y / x| > 0x1p64
    if (ix +% (64 << 20) < iy or iy == 0x7FF00000) {
        if (m & 1 != 0) {
            return -pi / 2;
        } else {
            return pi / 2;
        }
    }

    // z = atan(|y / x|) with correct underflow
    const z = z: {
        if ((m & 2) != 0 and iy +% (64 << 20) < ix) {
            break :z 0.0;
        } else {
            break :z math.atan(@abs(y / x));
        }
    };

    switch (m) {
        0 => return z, // atan(+, +)
        1 => return -z, // atan(-, +)
        2 => return pi - (z - pi_lo), // atan(+, -)
        3 => return (z - pi_lo) - pi, // atan(-, -)
        else => unreachable,
    }
}

test atan2 {
    const y32: f32 = 0.2;
    const x32: f32 = 0.21;
    const y64: f64 = 0.2;
    const x64: f64 = 0.21;
    try expect(atan2(y32, x32) == atan2_32(0.2, 0.21));
    try expect(atan2(y64, x64) == atan2_64(0.2, 0.21));
}

test atan2_32 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f32, atan2_32(0.0, 0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(0.2, 0.2), 0.785398, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-0.2, 0.2), -0.785398, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(0.2, -0.2), 2.356194, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(-0.2, -0.2), -2.356194, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(0.34, -0.4), 2.437099, epsilon));
    try expect(math.approxEqAbs(f32, atan2_32(0.34, 1.243), 0.267001, epsilon));
}

test atan2_64 {
    const epsilon = 0.000001;

    try expect(math.approxEqAbs(f64, atan2_64(0.0, 0.0), 0.0, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(0.2, 0.2), 0.785398, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-0.2, 0.2), -0.785398, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(0.2, -0.2), 2.356194, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(-0.2, -0.2), -2.356194, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(0.34, -0.4), 2.437099, epsilon));
    try expect(math.approxEqAbs(f64, atan2_64(0.34, 1.243), 0.267001, epsilon));
}

test "atan2_32.special" {
    const epsilon = 0.000001;

    
```
