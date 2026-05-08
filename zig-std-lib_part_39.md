```
 |i| {
            ret.cs[i] = a.cs[i] - b.cs[i];
        }
        return ret;
    }

    // Executes a forward "NTT" on p.
    //
    // Assumes the coefficients are in absolute value ≤q.  The resulting
    // coefficients are in absolute value ≤7q.  If the input is in Montgomery
    // form, then the result is in Montgomery form and so (by linearity of the NTT)
    // if the input is in regular form, then the result is also in regular form.
    fn ntt(a: Poly) Poly {
        // Note that ℤ_q does not have a primitive 512ᵗʰ root of unity (as 512
        // does not divide into q-1) and so we cannot do a regular NTT.  ℤ_q
        // does have a primitive 256ᵗʰ root of unity, the smallest of which
        // is ζ := 17.
        //
        // Recall that our base ring R := ℤ_q[x] / (x²⁵⁶ + 1).  The polynomial
        // x²⁵⁶+1 will not split completely (as its roots would be 512ᵗʰ roots
        // of unity.)  However, it does split almost (using ζ¹²⁸ = -1):
        //
        // x²⁵⁶ + 1 = (x²)¹²⁸ - ζ¹²⁸
        //          = ((x²)⁶⁴ - ζ⁶⁴)((x²)⁶⁴ + ζ⁶⁴)
        //          = ((x²)³² - ζ³²)((x²)³² + ζ³²)((x²)³² - ζ⁹⁶)((x²)³² + ζ⁹⁶)
        //          ⋮
        //          = (x² - ζ)(x² + ζ)(x² - ζ⁶⁵)(x² + ζ⁶⁵) … (x² + ζ¹²⁷)
        //
        // Note that the powers of ζ that appear (from the second line down) are
        // in binary
        //
        // 0100000 1100000
        // 0010000 1010000 0110000 1110000
        // 0001000 1001000 0101000 1101000 0011000 1011000 0111000 1111000
        //         …
        //
        // That is: brv(2), brv(3), brv(4), …, where brv(x) denotes the 7-bit
        // bitreversal of x.  These powers of ζ are given by the Zetas array.
        //
        // The polynomials x² ± ζⁱ are irreducible and coprime, hence by
        // the Chinese Remainder Theorem we know
        //
        //  ℤ_q[x]/(x²⁵⁶+1) → ℤ_q[x]/(x²-ζ) x … x  ℤ_q[x]/(x²+ζ¹²⁷)
        //
        // given by a ↦ ( a mod x²-ζ, …, a mod x²+ζ¹²⁷ )
        // is an isomorphism, which is the "NTT".  It can be efficiently computed by
        //
        //
        //  a ↦ ( a mod (x²)⁶⁴ - ζ⁶⁴, a mod (x²)⁶⁴ + ζ⁶⁴ )
        //    ↦ ( a mod (x²)³² - ζ³², a mod (x²)³² + ζ³²,
        //        a mod (x²)⁹⁶ - ζ⁹⁶, a mod (x²)⁹⁶ + ζ⁹⁶ )
        //
        //      et cetera
        // If N was 8 then this can be pictured in the following diagram:
        //
        //  https://cnx.org/resources/17ee4dfe517a6adda05377b25a00bf6e6c93c334/File0026.png
        //
        // Each cross is a Cooley-Tukey butterfly: it's the map
        //
        //  (a, b) ↦ (a + ζb, a - ζb)
        //
        // for the appropriate power ζ for that column and row group.
        var p = a;
        var k: usize = 0; // index into zetas

        var l = N >> 1;
        while (l > 1) : (l >>= 1) {
            // On the nᵗʰ iteration of the l-loop, the absolute value of the
            // coefficients are bounded by nq.

            // offset effectively loops over the row groups in this column; it is
            // the first row in the row group.
            var offset: usize = 0;
            while (offset < N - l) : (offset += 2 * l) {
                k += 1;
                const z = @as(i32, zetas[k]);

                // j loops over each butterfly in the row group.
                for (offset..offset + l) |j| {
                    const t = montReduce(z * @as(i32, p.cs[j + l]));
                    p.cs[j + l] = p.cs[j] - t;
                    p.cs[j] += t;
                }
            }
        }

        return p;
    }

    // Executes an inverse "NTT" on p and multiply by the Montgomery factor R.
    //
    // Assumes the coefficients are in absolute value ≤q.  The resulting
    // coefficients are in absolute value ≤q.  If the input is in Montgomery
    // form, then the result is in Montgomery form and so (by linearity)
    // if the input is in regular form, then the result is also in regular form.
    fn invNTT(a: Poly) Poly {
        var k: usize = 127; // index into zetas
        var r: usize = 0; // index into invNTTReductions
        var p = a;

        // We basically do the oppposite of NTT, but postpone dividing by 2 in the
        // inverse of the Cooley-Tukey butterfly and accumulate that into a big
        // division by 2⁷ at the end.  See the comments in the ntt() function.

        var l: usize = 2;
        while (l < N) : (l <<= 1) {
            var offset: usize = 0;
            while (offset < N - l) : (offset += 2 * l) {
                // As we're inverting, we need powers of ζ⁻¹ (instead of ζ).
                // To be precise, we need ζᵇʳᵛ⁽ᵏ⁾⁻¹²⁸. However, as ζ⁻¹²⁸ = -1,
                // we can use the existing zetas table instead of
                // keeping a separate invZetas table as in Dilithium.

                const minZeta = @as(i32, zetas[k]);
                k -= 1;

                for (offset..offset + l) |j| {
                    // Gentleman-Sande butterfly: (a, b) ↦ (a + b, ζ(a-b))
                    const t = p.cs[j + l] - p.cs[j];
                    p.cs[j] += p.cs[j + l];
                    p.cs[j + l] = montReduce(minZeta * @as(i32, t));

                    // Note that if we had |a| < αq and |b| < βq before the
                    // butterfly, then now we have |a| < (α+β)q and |b| < q.
                }
            }

            // We let the invNTTReductions instruct us which coefficients to
            // Barrett reduce.
            while (true) {
                const i = inv_ntt_reductions[r];
                r += 1;
                if (i < 0) {
                    break;
                }
                p.cs[@as(usize, @intCast(i))] = feBarrettReduce(p.cs[@as(usize, @intCast(i))]);
            }
        }

        for (0..N) |j| {
            // Note 1441 = (128)⁻¹ R².  The coefficients are bounded by 9q, so
            // as 1441 * 9 ≈ 2¹⁴ < 2¹⁵, we're within the required bounds
            // for montReduce().
            p.cs[j] = montReduce(r2_over_128 * @as(i32, p.cs[j]));
        }

        return p;
    }

    // Normalizes coefficients.
    //
    // Ensures each coefficient is in {0, …, q-1}.
    fn normalize(a: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = csubq(feBarrettReduce(a.cs[i]));
        }
        return ret;
    }

    // Put p in Montgomery form.
    fn toMont(a: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = feToMont(a.cs[i]);
        }
        return ret;
    }

    // Barret reduce coefficients.
    //
    // Beware, this does not fully normalize coefficients.
    fn barrettReduce(a: Poly) Poly {
        var ret: Poly = undefined;
        for (0..N) |i| {
            ret.cs[i] = feBarrettReduce(a.cs[i]);
        }
        return ret;
    }

    fn compressedSize(comptime d: u8) usize {
        return @divTrunc(N * d, 8);
    }

    // Returns packed Compress_q(p, d).
    //
    // Assumes p is normalized.
    fn compress(p: Poly, comptime d: u8) [compressedSize(d)]u8 {
        @setEvalBranchQuota(10000);
        const q_over_2: u32 = comptime @divTrunc(Q, 2); // (q-1)/2
        const two_d_min_1: u32 = comptime (1 << d) - 1; // 2ᵈ-1
        var in_off: usize = 0;
        var out_off: usize = 0;

        const batch_size: usize = comptime math.lcm(d, 8);
        const in_batch_size: usize = comptime batch_size / d;
        const out_batch_size: usize = comptime batch_size / 8;

        const out_length: usize = comptime @divTrunc(N * d, 8);
        comptime assert(out_length * 8 == d * N);
        var out = [_]u8{0} ** out_length;

        while (in_off < N) {
            // First we compress into in.
            var in: [in_batch_size]u16 = undefined;
            inline for (0..in_batch_size) |i| {
                // Compress_q(x, d) = ⌈(2ᵈ/q)x⌋ mod⁺ 2ᵈ
                //                  = ⌊(2ᵈ/q)x+½⌋ mod⁺ 2ᵈ
                //                  = ⌊((x << d) + q/2) / q⌋ mod⁺ 2ᵈ
                //                  = DIV((x << d) + q/2, q) & ((1<<d) - 1)
                const t = @as(u24, @intCast(p.cs[in_off + i])) << d;
                // Division by invariant multiplication, equivalent to DIV(t + q/2, q).
                // A division may not be a constant-time operation, even with a constant denominator.
                // Here, side channels would leak information about the shared secret, see https://kyberslash.cr.yp.to
                // Multiplication, on the other hand, is a constant-time operation on the CPUs we currently support.
                comptime assert(d <= 11);
                comptime assert(((20642679 * @as(u64, Q)) >> 36) == 1);
                const u: u32 = @intCast((@as(u64, t + q_over_2) * 20642679) >> 36);
                in[i] = @intCast(u & two_d_min_1);
            }

            // Now we pack the d-bit integers from `in' into out as bytes.
            comptime var in_shift: usize = 0;
            comptime var j: usize = 0;
            comptime var i: usize = 0;
            inline while (i < in_batch_size) : (j += 1) {
                comptime var todo: usize = 8;
                inline while (todo > 0) {
                    const out_shift = comptime 8 - todo;
                    out[out_off + j] |= @as(u8, @truncate((in[i] >> in_shift) << out_shift));

                    const done = comptime @min(@min(d, todo), d - in_shift);
                    todo -= done;
                    in_shift += done;

                    if (in_shift == d) {
                        in_shift = 0;
                        i += 1;
                    }
                }
            }

            in_off += in_batch_size;
            out_off += out_batch_size;
        }

        return out;
    }

    // Set p to Decompress_q(m, d).
    fn decompress(comptime d: u8, in: *const [compressedSize(d)]u8) Poly {
        @setEvalBranchQuota(10000);
        const in_len = comptime @divTrunc(N * d, 8);
        comptime assert(in_len * 8 == d * N);
        var ret: Poly = undefined;
        var in_off: usize = 0;
        var out_off: usize = 0;

        const batch_size: usize = comptime math.lcm(d, 8);
        const in_batch_size: usize = comptime batch_size / 8;
        const out_batch_size: usize = comptime batch_size / d;

        while (out_off < N) {
            comptime var in_shift: usize = 0;
            comptime var j: usize = 0;
            comptime var i: usize = 0;
            inline while (i < out_batch_size) : (i += 1) {
                // First, unpack next coefficient.
                comptime var todo = d;
                var out: u16 = 0;

                inline while (todo > 0) {
                    const out_shift = comptime d - todo;
                    const m = comptime (1 << d) - 1;
                    out |= (@as(u16, in[in_off + j] >> in_shift) << out_shift) & m;

                    const done = comptime @min(@min(8, todo), 8 - in_shift);
                    todo -= done;
                    in_shift += done;

                    if (in_shift == 8) {
                        in_shift = 0;
                        j += 1;
                    }
                }

                // Decompress_q(x, d) = ⌈(q/2ᵈ)x⌋
                //                    = ⌊(q/2ᵈ)x+½⌋
                //                    = ⌊(qx + 2ᵈ⁻¹)/2ᵈ⌋
                //                    = (qx + (1<<(d-1))) >> d
                const qx = @as(u32, out) * @as(u32, Q);
                ret.cs[out_off + i] = @as(i16, @intCast((qx + (1 << (d - 1))) >> d));
            }

            in_off += in_batch_size;
            out_off += out_batch_size;
        }

        return ret;
    }

    // Returns the "pointwise" multiplication a o b.
    //
    // That is: invNTT(a o b) = invNTT(a) * invNTT(b).  Assumes a and b are in
    // Montgomery form.  Products between coefficients of a and b must be strictly
    // bounded in absolute value by 2¹⁵q.  a o b will be in Montgomery form and
    // bounded in absolute value by 2q.
    fn mulHat(a: Poly, b: Poly) Poly {
        // Recall from the discussion in ntt(), that a transformed polynomial is
        // an element of ℤ_q[x]/(x²-ζ) x … x  ℤ_q[x]/(x²+ζ¹²⁷);
        // that is: 128 degree-one polynomials instead of simply 256 elements
        // from ℤ_q as in the regular NTT.  So instead of pointwise multiplication,
        // we multiply the 128 pairs of degree-one polynomials modulo the
        // right equation:
        //
        //  (a₁ + a₂x)(b₁ + b₂x) = a₁b₁ + a₂b₂ζ' + (a₁b₂ + a₂b₁)x,
        //
        // where ζ' is the appropriate power of ζ.

        var p: Poly = undefined;
        var k: usize = 64;
        var i: usize = 0;
        while (i < N) : (i += 4) {
            const z = @as(i32, zetas[k]);
            k += 1;

            const a1b1 = montReduce(@as(i32, a.cs[i + 1]) * @as(i32, b.cs[i + 1]));
            const a0b0 = montReduce(@as(i32, a.cs[i]) * @as(i32, b.cs[i]));
            const a1b0 = montReduce(@as(i32, a.cs[i + 1]) * @as(i32, b.cs[i]));
            const a0b1 = montReduce(@as(i32, a.cs[i]) * @as(i32, b.cs[i + 1]));

            p.cs[i] = montReduce(a1b1 * z) + a0b0;
            p.cs[i + 1] = a0b1 + a1b0;

            const a3b3 = montReduce(@as(i32, a.cs[i + 3]) * @as(i32, b.cs[i + 3]));
            const a2b2 = montReduce(@as(i32, a.cs[i + 2]) * @as(i32, b.cs[i + 2]));
            const a3b2 = montReduce(@as(i32, a.cs[i + 3]) * @as(i32, b.cs[i + 2]));
            const a2b3 = montReduce(@as(i32, a.cs[i + 2]) * @as(i32, b.cs[i + 3]));

            p.cs[i + 2] = a2b2 - montReduce(a3b3 * z);
            p.cs[i + 3] = a2b3 + a3b2;
        }

        return p;
    }

    // Sample p from a centered binomial distribution with n=2η and p=½ - viz:
    // coefficients are in {-η, …, η} with probabilities
    //
    //  {ncr(0, 2η)/2^2η, ncr(1, 2η)/2^2η, …, ncr(2η,2η)/2^2η}
    fn noise(comptime eta: u8, nonce: u8, seed: *const [32]u8) Poly {
        var h = sha3.Shake256.init(.{});
        const suffix: [1]u8 = .{nonce};
        h.update(seed);
        h.update(&suffix);

        // The distribution at hand is exactly the same as that
        // of (a₁ + a₂ + … + a_η) - (b₁ + … + b_η) where a_i,b_i~U(1).
        // Thus we need 2η bits per coefficient.
        const buf_len = comptime 2 * eta * N / 8;
        var buf: [buf_len]u8 = undefined;
        h.squeeze(&buf);

        // buf is interpreted as a₁…a_ηb₁…b_ηa₁…a_ηb₁…b_η…. We process
        // multiple coefficients in one batch.

        const T = switch (builtin.target.cpu.arch) {
            .x86_64, .x86 => u32, // Generates better code on Intel CPUs
            else => u64, // u128 might be faster on some other CPUs.
        };

        comptime var batch_count: usize = undefined;
        comptime var batch_bytes: usize = undefined;
        comptime var mask: T = 0;
        comptime {
            batch_count = @bitSizeOf(T) / @as(usize, 2 * eta);
            while (@rem(N, batch_count) != 0 and batch_count > 0) : (batch_count -= 1) {}
            assert(batch_count > 0);
            assert(@rem(2 * eta * batch_count, 8) == 0);
            batch_bytes = 2 * eta * batch_count / 8;

            for (0..2 * eta * batch_count) |_| {
                mask <<= eta;
                mask |= 1;
            }
        }

        var ret: Poly = undefined;
        for (0..comptime N / batch_count) |i| {
            // Read coefficients into t. In the case of η=3,
            // we have t = a₁ + 2a₂ + 4a₃ + 8b₁ + 16b₂ + …
            var t: T = 0;
            inline for (0..batch_bytes) |j| {
                t |= @as(T, buf[batch_bytes * i + j]) << (8 * j);
            }

            // Accumulate `a's and `b's together by masking them out, shifting
            // and adding. For η=3, we have  d = a₁ + a₂ + a₃ + 8(b₁ + b₂ + b₃) + …
            var d: T = 0;
            inline for (0..eta) |j| {
                d += (t >> j) & mask;
            }

            // Extract each a and b separately and set coefficient in polynomial.
            inline for (0..batch_count) |j| {
                const mask2 = comptime (1 << eta) - 1;
                const a = @as(i16, @intCast((d >> (comptime (2 * j * eta))) & mask2));
                const b = @as(i16, @intCast((d >> (comptime ((2 * j + 1) * eta))) & mask2));
                ret.cs[batch_count * i + j] = a - b;
            }
        }

        return ret;
    }

    fn uniform(seed: [32]u8, x: u8, y: u8) Poly {
        const domain_sep: [2]u8 = .{ x, y };
        return sampleUniformRejection(
            Poly,
            Q,
            12,
            N,
            &seed,
            &domain_sep,
        );
    }

    // Packs p.
    //
    // Assumes p is normalized (and not just Barrett reduced).
    fn toBytes(p: Poly) [encoded_length]u8 {
        var ret: [encoded_length]u8 = undefined;
        for (0..comptime N / 2) |i| {
            const t0 = @as(u16, @intCast(p.cs[2 * i]));
            const t1 = @as(u16, @intCast(p.cs[2 * i + 1]));
            ret[3 * i] = @as(u8, @truncate(t0));
            ret[3 * i + 1] = @as(u8, @truncate((t0 >> 8) | (t1 << 4)));
            ret[3 * i + 2] = @as(u8, @truncate(t1 >> 4));
        }
        return ret;
    }

    // Unpacks a Poly from buf.
    //
    // p will not be normalized; instead 0 ≤ p[i] < 4096.
    fn fromBytes(buf: *const [encoded_length]u8) Poly {
        var ret: Poly = undefined;
        for (0..comptime N / 2) |i| {
            const b0 = @as(i16, buf[3 * i]);
            const b1 = @as(i16, buf[3 * i + 1]);
            const b2 = @as(i16, buf[3 * i + 2]);
            ret.cs[2 * i] = b0 | ((b1 & 0xf) << 8);
            ret.cs[2 * i + 1] = (b1 >> 4) | b2 << 4;
        }
        return ret;
    }
};

// A vector of k polynomials.
fn PolyVec(comptime k: u8) type {
    return struct {
        ps: [k]Poly,

        const Self = @This();
        const encoded_length = k * Poly.encoded_length;

        fn compressedSize(comptime d: u8) usize {
            return Poly.compressedSize(d) * k;
        }

        /// Apply unary operation to each polynomial
        fn map(v: Self, comptime op: fn (Poly) Poly) Self {
            var ret: Self = undefined;
            inline for (0..k) |i| {
                ret.ps[i] = op(v.ps[i]);
            }
            return ret;
        }

        /// Apply binary operation pairwise
        fn mapBinary(a: Self, b: Self, comptime op: fn (Poly, Poly) Poly) Self {
            var ret: Self = undefined;
            inline for (0..k) |i| {
                ret.ps[i] = op(a.ps[i], b.ps[i]);
            }
            return ret;
        }

        fn ntt(v: Self) Self {
            return map(v, Poly.ntt);
        }

        fn invNTT(v: Self) Self {
            return map(v, Poly.invNTT);
        }

        fn normalize(v: Self) Self {
            return map(v, Poly.normalize);
        }

        fn barrettReduce(v: Self) Self {
            return map(v, Poly.barrettReduce);
        }

        fn add(a: Self, b: Self) Self {
            return mapBinary(a, b, Poly.add);
        }

        fn sub(a: Self, b: Self) Self {
            return mapBinary(a, b, Poly.sub);
        }

        // Samples v[i] from centered binomial distribution with the given η,
        // seed and nonce+i.
        fn noise(comptime eta: u8, nonce: u8, seed: *const [32]u8) Self {
            var ret: Self = undefined;
            for (0..k) |i| {
                ret.ps[i] = Poly.noise(eta, nonce + @as(u8, @intCast(i)), seed);
            }
            return ret;
        }

        // Sets p to the inner product of a and b using "pointwise" multiplication.
        //
        // See MulHat() and NTT() for a description of the multiplication.
        // Assumes a and b are in Montgomery form.  p will be in Montgomery form,
        // and its coefficients will be bounded in absolute value by 2kq.
        // If a and b are not in Montgomery form, then the action is the same
        // as "pointwise" multiplication followed by multiplying by R⁻¹, the inverse
        // of the Montgomery factor.
        fn dotHat(a: Self, b: Self) Poly {
            var ret: Poly = Poly.zero;
            for (0..k) |i| {
                ret = ret.add(a.ps[i].mulHat(b.ps[i]));
            }
            return ret;
        }

        fn compress(v: Self, comptime d: u8) [compressedSize(d)]u8 {
            const cs = comptime Poly.compressedSize(d);
            var ret: [compressedSize(d)]u8 = undefined;
            inline for (0..k) |i| {
                ret[i * cs .. (i + 1) * cs].* = v.ps[i].compress(d);
            }
            return ret;
        }

        fn decompress(comptime d: u8, buf: *const [compressedSize(d)]u8) Self {
            const cs = comptime Poly.compressedSize(d);
            var ret: Self = undefined;
            inline for (0..k) |i| {
                ret.ps[i] = Poly.decompress(d, buf[i * cs .. (i + 1) * cs]);
            }
            return ret;
        }

        /// Serializes the key into a byte array.
        fn toBytes(v: Self) [encoded_length]u8 {
            var ret: [encoded_length]u8 = undefined;
            inline for (0..k) |i| {
                ret[i * Poly.encoded_length .. (i + 1) * Poly.encoded_length].* = v.ps[i].toBytes();
            }
            return ret;
        }

        /// Deserializes the key from a byte array.
        fn fromBytes(buf: *const [encoded_length]u8) Self {
            var ret: Self = undefined;
            inline for (0..k) |i| {
                ret.ps[i] = Poly.fromBytes(
                    buf[i * Poly.encoded_length .. (i + 1) * Poly.encoded_length],
                );
            }
            return ret;
        }
    };
}

// A matrix of k vectors
fn Mat(comptime k: u8) type {
    return struct {
        const Self = @This();
        rows: [k]PolyVec(k),

        fn uniform(seed: [32]u8, comptime transposed: bool) Self {
            var ret: Self = undefined;
            var i: u8 = 0;
            while (i < k) : (i += 1) {
                var j: u8 = 0;
                while (j < k) : (j += 1) {
                    ret.rows[i].ps[j] = Poly.uniform(
                        seed,
                        if (transposed) i else j,
                        if (transposed) j else i,
                    );
                }
            }
            return ret;
        }

        // Returns transpose of A
        fn transpose(m: Self) Self {
            var ret: Self = undefined;
            for (0..k) |i| {
                for (0..k) |j| {
                    ret.rows[i].ps[j] = m.rows[j].ps[i];
                }
            }
            return ret;
        }
    };
}

// Returns `true` if a ≠ b.
fn ctneq(comptime len: usize, a: [len]u8, b: [len]u8) u1 {
    return 1 - @intFromBool(crypto.timing_safe.eql([len]u8, a, b));
}

// Copy src into dst given b = 1.
fn cmov(comptime len: usize, dst: *[len]u8, src: [len]u8, b: u1) void {
    const mask = @as(u8, 0) -% b;
    for (0..len) |i| {
        dst[i] ^= mask & (dst[i] ^ src[i]);
    }
}

// Test helper: generates a random polynomial with each coefficient |x| ≤ q
fn randPolyAbsLeqQ(rnd: anytype) Poly {
    var ret: Poly = undefined;
    for (0..N) |i| {
        ret.cs[i] = rnd.random().intRangeAtMost(i16, -Q, Q);
    }
    return ret;
}

// Test helper: generates a random normalized polynomial
fn randPolyNormalized(rnd: anytype) Poly {
    var ret: Poly = undefined;
    for (0..N) |i| {
        ret.cs[i] = rnd.random().intRangeLessThan(i16, 0, Q);
    }
    return ret;
}

test "MulHat" {
    if (comptime builtin.cpu.has(.s390x, .vector)) return error.SkipZigTest;

    var rnd = RndGen.init(0);

    for (0..100) |_| {
        const a = randPolyAbsLeqQ(&rnd);
        const b = randPolyAbsLeqQ(&rnd);

        const p2 = a.ntt().mulHat(b.ntt()).barrettReduce().invNTT().normalize();
        var p: Poly = undefined;

        @memset(&p.cs, 0);

        for (0..N) |i| {
            for (0..N) |j| {
                var v = montReduce(@as(i32, a.cs[i]) * @as(i32, b.cs[j]));
                var k = i + j;
                if (k >= N) {
                    // Recall Xᴺ = -1.
                    k -= N;
                    v = -v;
                }
                p.cs[k] = feBarrettReduce(v + p.cs[k]);
            }
        }

        p = p.toMont().normalize();

        try testing.expectEqual(p, p2);
    }
}

test "NTT" {
    var rnd = RndGen.init(0);

    for (0..1000) |_| {
        var p = randPolyAbsLeqQ(&rnd);
        const q = p.toMont().normalize();
        p = p.ntt();

        for (0..N) |i| {
            try testing.expect(p.cs[i] <= 7 * Q and -7 * Q <= p.cs[i]);
        }

        p = p.normalize().invNTT();
        for (0..N) |i| {
            try testing.expect(p.cs[i] <= Q and -Q <= p.cs[i]);
        }

        p = p.normalize();

        try testing.expectEqual(p, q);
    }
}

test "Compression" {
    var rnd = RndGen.init(0);
    inline for (.{ 1, 4, 5, 10, 11 }) |d| {
        for (0..1000) |_| {
            const p = randPolyNormalized(&rnd);
            const pp = p.compress(d);
            const pq = Poly.decompress(d, &pp).compress(d);
            try testing.expectEqual(pp, pq);
        }
    }
}

test "noise" {
    var seed: [32]u8 = undefined;
    for (&seed, 0..) |*s, i| {
        s.* = @as(u8, @intCast(i));
    }
    try testing.expectEqual(Poly.noise(3, 37, &seed).cs, .{
        0,  0,  1,  -1, 0,  2,  0,  -1, -1, 3,  0,  1,  -2, -2, 0,  1,  -2,
        1,  0,  -2, 3,  0,  0,  0,  1,  3,  1,  1,  2,  1,  -1, -1, -1, 0,
        1,  0,  1,  0,  2,  0,  1,  -2, 0,  -1, -1, -2, 1,  -1, -1, 2,  -1,
        1,  1,  2,  -3, -1, -1, 0,  0,  0,  0,  1,  -1, -2, -2, 0,  -2, 0,
        0,  0,  1,  0,  -1, -1, 1,  -2, 2,  0,  0,  2,  -2, 0,  1,  0,  1,
        1,  1,  0,  1,  -2, -1, -2, -1, 1,  0,  0,  0,  0,  0,  1,  0,  -1,
        -1, 0,  -1, 1,  0,  1,  0,  -1, -1, 0,  -2, 2,  0,  -2, 1,  -1, 0,
        1,  -1, -1, 2,  1,  0,  0,  -2, -1, 2,  0,  0,  0,  -1, -1, 3,  1,
        0,  1,  0,  1,  0,  2,  1,  0,  0,  1,  0,  1,  0,  0,  -1, -1, -1,
        0,  1,  3,  1,  0,  1,  0,  1,  -1, -1, -1, -1, 0,  0,  -2, -1, -1,
        2,  0,  1,  0,  1,  0,  2,  -2, 0,  1,  1,  -3, -1, -2, -1, 0,  1,
        0,  1,  -2, 2,  2,  1,  1,  0,  -1, 0,  -1, -1, 1,  0,  -1, 2,  1,
        -1, 1,  2,  -2, 1,  2,  0,  1,  2,  1,  0,  0,  2,  1,  2,  1,  0,
        2,  1,  0,  0,  -1, -1, 1,  -1, 0,  1,  -1, 2,  2,  0,  0,  -1, 1,
        1,  1,  1,  0,  0,  -2, 0,  -1, 1,  2,  0,  0,  1,  1,  -1, 1,  0,
        1,
    });
    try testing.expectEqual(Poly.noise(2, 37, &seed).cs, .{
        1,  0,  1,  -1, -1, -2, -1, -1, 2,  0,  -1, 0,  0,  -1,
        1,  1,  -1, 1,  0,  2,  -2, 0,  1,  2,  0,  0,  -1, 1,
        0,  -1, 1,  -1, 1,  2,  1,  1,  0,  -1, 1,  -1, -2, -1,
        1,  -1, -1, -1, 2,  -1, -1, 0,  0,  1,  1,  -1, 1,  1,
        1,  1,  -1, -2, 0,  1,  0,  0,  2,  1,  -1, 2,  0,  0,
        1,  1,  0,  -1, 0,  0,  -1, -1, 2,  0,  1,  -1, 2,  -1,
        -1, -1, -1, 0,  -2, 0,  2,  1,  0,  0,  0,  -1, 0,  0,
        0,  -1, -1, 0,  -1, -1, 0,  -1, 0,  0,  -2, 1,  1,  0,
        1,  0,  1,  0,  1,  1,  -1, 2,  0,  1,  -1, 1,  2,  0,
        0,  0,  0,  -1, -1, -1, 0,  1,  0,  -1, 2,  0,  0,  1,
        1,  1,  0,  1,  -1, 1,  2,  1,  0,  2,  -1, 1,  -1, -2,
        -1, -2, -1, 1,  0,  -2, -2, -1, 1,  0,  0,  0,  0,  1,
        0,  0,  0,  2,  2,  0,  1,  0,  -1, -1, 0,  2,  0,  0,
        -2, 1,  0,  2,  1,  -1, -2, 0,  0,  -1, 1,  1,  0,  0,
        2,  0,  1,  1,  -2, 1,  -2, 1,  1,  0,  2,  0,  -1, 0,
        -1, 0,  1,  2,  0,  1,  0,  -2, 1,  -2, -2, 1,  -1, 0,
        -1, 1,  1,  0,  0,  0,  1,  0,  -1, 1,  1,  0,  0,  0,
        0,  1,  0,  1,  -1, 0,  1,  -1, -1, 2,  0,  0,  1,  -1,
        0,  1,  -1, 0,
    });
}

test "uniform sampling" {
    var seed: [32]u8 = undefined;
    for (&seed, 0..) |*s, i| {
        s.* = @as(u8, @intCast(i));
    }
    try testing.expectEqual(Poly.uniform(seed, 1, 0).cs, .{
        797,  993,  161,  6,    2608, 2385, 2096, 2661, 1676, 247,  2440,
        342,  634,  194,  1570, 2848, 986,  684,  3148, 3208, 2018, 351,
        2288, 612,  1394, 170,  1521, 3119, 58,   596,  2093, 1549, 409,
        2156, 1934, 1730, 1324, 388,  446,  418,  1719, 2202, 1812, 98,
        1019, 2369, 214,  2699, 28,   1523, 2824, 273,  402,  2899, 246,
        210,  1288, 863,  2708, 177,  3076, 349,  44,   949,  854,  1371,
        957,  292,  2502, 1617, 1501, 254,  7,    1761, 2581, 2206, 2655,
        1211, 629,  1274, 2358, 816,  2766, 2115, 2985, 1006, 2433, 856,
        2596, 3192, 1,    1378, 2345, 707,  1891, 1669, 536,  1221, 710,
        2511, 120,  1176, 322,  1897, 2309, 595,  2950, 1171, 801,  1848,
        695,  2912, 1396, 1931, 1775, 2904, 893,  2507, 1810, 2873, 253,
        1529, 1047, 2615, 1687, 831,  1414, 965,  3169, 1887, 753,  3246,
        1937, 115,  2953, 586,  545,  1621, 1667, 3187, 1654, 1988, 1857,
        512,  1239, 1219, 898,  3106, 391,  1331, 2228, 3169, 586,  2412,
        845,  768,  156,  662,  478,  1693, 2632, 573,  2434, 1671, 173,
        969,  364,  1663, 2701, 2169, 813,  1000, 1471, 720,  2431, 2530,
        3161, 733,  1691, 527,  2634, 335,  26,   2377, 1707, 767,  3020,
        950,  502,  426,  1138, 3208, 2607, 2389, 44,   1358, 1392, 2334,
        875,  2097, 173,  1697, 2578, 942,  1817, 974,  1165, 2853, 1958,
        2973, 3282, 271,  1236, 1677, 2230, 673,  1554, 96,   242,  1729,
        2518, 1884, 2272, 71,   1382, 924,  1807, 1610, 456,  1148, 2479,
        2152, 238,  2208, 2329, 713,  1175, 1196, 757,  1078, 3190, 3169,
        708,  3117, 154,  1751, 3225, 1364, 154,  23,   2842, 1105, 1419,
        79,   5,    2013,
    });
}

test "Polynomial packing" {
    var rnd = RndGen.init(0);

    for (0..1000) |_| {
        const p = randPolyNormalized(&rnd);
        try testing.expectEqual(Poly.fromBytes(&p.toBytes()), p);
    }
}

test "Test inner PKE" {
    if (comptime builtin.cpu.has(.s390x, .vector)) return error.SkipZigTest;

    var seed: [32]u8 = undefined;
    var pt: [32]u8 = undefined;
    for (&seed, &pt, 0..) |*s, *p, i| {
        s.* = @as(u8, @intCast(i));
        p.* = @as(u8, @intCast(i + 32));
    }
    inline for (modes) |mode| {
        for (0..10) |i| {
            var pk: mode.InnerPk = undefined;
            var sk: mode.InnerSk = undefined;
            seed[0] = @as(u8, @intCast(i));
            mode.innerKeyFromSeed(seed, &pk, &sk);
            for (0..10) |j| {
                seed[1] = @as(u8, @intCast(j));
                try testing.expectEqual(sk.decrypt(&pk.encrypt(&pt, &seed)), pt);
            }
        }
    }
}

test "Test happy flow" {
    if (comptime builtin.cpu.has(.s390x, .vector)) return error.SkipZigTest;

    var seed: [64]u8 = undefined;
    for (&seed, 0..) |*s, i| {
        s.* = @as(u8, @intCast(i));
    }
    inline for (modes) |mode| {
        for (0..10) |i| {
            seed[0] = @intCast(i);
            const kp = try mode.KeyPair.generateDeterministic(seed);
            const sk = try mode.SecretKey.fromBytes(&kp.secret_key.toBytes());
            try testing.expectEqual(sk, kp.secret_key);
            const pk = try mode.PublicKey.fromBytes(&kp.public_key.toBytes());
            try testing.expectEqual(pk, kp.public_key);
            for (0..10) |j| {
                seed[1] = @intCast(j);
                const e = pk.encapsDeterministic(seed[0..32]);
                try testing.expectEqual(e.shared_secret, try sk.decaps(&e.ciphertext));
            }
        }
    }
}

// Code to test NIST Known Answer Tests (KAT), see PQCgenKAT.c.

test "NIST KAT test d00.Kyber512" {
    if (comptime builtin.cpu.has(.loongarch, .lsx)) return error.SkipZigTest;
    if (comptime builtin.cpu.has(.s390x, .vector)) return error.SkipZigTest;

    try testNistKat(d00.Kyber512, "e9c2bd37133fcb40772f81559f14b1f58dccd1c816701be9ba6214d43baf4547");
}

test "NIST KAT test d00.Kyber1024" {
    if (comptime builtin.cpu.has(.loongarch, .lsx)) return error.SkipZigTest;
    if (comptime builtin.cpu.has(.s390x, .vector)) return error.SkipZigTest;

    try testNistKat(d00.Kyber1024, "89248f2f33f7f4f7051729111f3049c409a933ec904aedadf035f30fa5646cd5");
}

test "NIST KAT test d00.Kyber768" {
    if (comptime builtin.cpu.has(.loongarch, .lsx)) return error.SkipZigTest;
    if (comptime builtin.cpu.has(.s390x, .vector)) return error.SkipZigTest;

    try testNistKat(d00.Kyber768, "a1e122cad3c24bc51622e4c242d8b8acbcd3f618fee4220400605ca8f9ea02c2");
}

fn testNistKat(mode: type, hash: []const u8) !void {
    var seed: [48]u8 = undefined;
    for (&seed, 0..) |*s, i| {
        s.* = @as(u8, @intCast(i));
    }
    var fw: std.Io.Writer.Hashing(crypto.hash.sha2.Sha256) = .init(&.{});
    var g = NistDRBG.init(seed);
    try fw.writer.print("# {s}\n\n", .{mode.name});
    for (0..100) |i| {
        g.fill(&seed);
        try fw.writer.print("count = {}\n", .{i});
        try fw.writer.print("seed = {X}\n", .{&seed});
        var g2 = NistDRBG.init(seed);

        // This is not equivalent to g2.fill(kseed[:]). As the reference
        // implementation calls randombytes twice generating the keypair,
        // we have to do that as well.
        var kseed: [64]u8 = undefined;
        var eseed: [32]u8 = undefined;
        g2.fill(kseed[0..32]);
        g2.fill(kseed[32..64]);
        g2.fill(&eseed);
        const kp = try mode.KeyPair.generateDeterministic(kseed);
        const e = kp.public_key.encapsDeterministic(&eseed);
        const ss2 = try kp.secret_key.decaps(&e.ciphertext);
        try testing.expectEqual(ss2, e.shared_secret);
        try fw.writer.print("pk = {X}\n", .{&kp.public_key.toBytes()});
        try fw.writer.print("sk = {X}\n", .{&kp.secret_key.toBytes()});
        try fw.writer.print("ct = {X}\n", .{&e.ciphertext});
        try fw.writer.print("ss = {X}\n\n", .{&e.shared_secret});
    }

    var out: [32]u8 = undefined;
    fw.hasher.final(&out);
    var outHex: [64]u8 = undefined;
    _ = try std.fmt.bufPrint(&outHex, "{x}", .{&out});
    try testing.expectEqualStrings(&outHex, hash);
}

const NistDRBG = struct {
    key: [32]u8,
    v: [16]u8,

    fn incV(g: *NistDRBG) void {
        var j: usize = 15;
        while (j >= 0) : (j -= 1) {
            if (g.v[j] == 255) {
                g.v[j] = 0;
            } else {
                g.v[j] += 1;
                break;
            }
        }
    }

    // AES256_CTR_DRBG_Update(pd, &g.key, &g.v).
    fn update(g: *NistDRBG, pd: ?[48]u8) void {
        var buf: [48]u8 = undefined;
        const ctx = crypto.core.aes.Aes256.initEnc(g.key);
        var i: usize = 0;
        while (i < 3) : (i += 1) {
            g.incV();
            var block: [16]u8 = undefined;
            ctx.encrypt(&block, &g.v);
            buf[i * 16 ..][0..16].* = block;
        }
        if (pd) |p| {
            for (&buf, p) |*b, x| {
                b.* ^= x;
            }
        }
        g.key = buf[0..32].*;
        g.v = buf[32..48].*;
    }

    // randombytes.
    fn fill(g: *NistDRBG, out: []u8) void {
        var block: [16]u8 = undefined;
        var dst = out;

        const ctx = crypto.core.aes.Aes256.initEnc(g.key);
        while (dst.len > 0) {
            g.incV();
            ctx.encrypt(&block, &g.v);
            if (dst.len < 16) {
                @memcpy(dst, block[0..dst.len]);
                break;
            }
            dst[0..block.len].* = block;
            dst = dst[16..dst.len];
        }
        g.update(null);
    }

    fn init(seed: [48]u8) NistDRBG {
        var ret: NistDRBG = .{ .key = .{0} ** 32, .v = .{0} ** 16 };
        ret.update(seed);
        return ret;
    }
};

/// Extended Euclidian Algorithm
/// Only meant to be used on comptime values; correctness matters, performance doesn't.
fn extendedEuclidean(comptime T: type, comptime a_: T, comptime b_: T) struct { gcd: T, x: T, y: T } {
    var a = a_;
    var b = b_;
    var x0: T = 1;
    var x1: T = 0;
    var y0: T = 0;
    var y1: T = 1;

    while (b != 0) {
        const q = @divTrunc(a, b);
        const temp_a = a;
        a = b;
        b = temp_a - q * b;

        const temp_x = x0;
        x0 = x1;
        x1 = temp_x - q * x1;

        const temp_y = y0;
        y0 = y1;
        y1 = temp_y - q * y1;
    }

    return .{ .gcd = a, .x = x0, .y = y0 };
}

/// Modular inversion: computes a^(-1) mod p
/// Requires gcd(a,p) = 1. The result is normalized to the range [0, p).
fn modularInverse(comptime T: type, comptime a: T, comptime p: T) T {
    // Use a signed type for EEA computation
    const type_info = @typeInfo(T);
    const SignedT = if (type_info == .int and type_info.int.signedness == .unsigned)
        std.meta.Int(.signed, type_info.int.bits)
    else
        T;

    const a_signed = @as(SignedT, @intCast(a));
    const p_signed = @as(SignedT, @intCast(p));

    const r = extendedEuclidean(SignedT, a_signed, p_signed);
    assert(r.gcd == 1);

    // Normalize result to [0, p)
    var result = r.x;
    while (result < 0) {
        result += p_signed;
    }

    return @intCast(result);
}

/// Modular exponentiation: computes a^s mod p using square-and-multiply algorithm.
fn modularPow(comptime T: type, comptime a: T, s: T, comptime p: T) T {
    const type_info = @typeInfo(T);
    const bits = type_info.int.bits;
    const WideT = std.meta.Int(.unsigned, bits * 2);

    var ret: T = 1;
    var base: T = a;
    var exp = s;

    while (exp > 0) {
        if (exp & 1 == 1) {
            ret = @intCast((@as(WideT, ret) * @as(WideT, base)) % p);
        }
        base = @intCast((@as(WideT, base) * @as(WideT, base)) % p);
        exp >>= 1;
    }

    return ret;
}

/// Creates an all-ones or all-zeros mask from a single bit value.
/// Returns all 1s (0xFF...FF) if bit == 1, all 0s if bit == 0.
fn bitMask(comptime T: type, bit: T) T {
    const type_info = @typeInfo(T);
    if (type_info != .int or type_info.int.signedness != .unsigned) {
        @compileError("bitMask requires an unsigned integer type");
    }
    return -%bit;
}

/// Creates a mask from the sign bit of a signed integer.
/// Returns all 1s (0xFF...FF) if x < 0, all 0s if x >= 0.
fn signMask(comptime T: type, x: T) std.meta.Int(.unsigned, @typeInfo(T).int.bits) {
    const type_info = @typeInfo(T);
    if (type_info != .int) {
        @compileError("signMask requires an integer type");
    }

    const bits = type_info.int.bits;
    const SignedT = std.meta.Int(.signed, bits);

    // Convert to signed if needed, arithmetic right shift to propagate sign bit
    const x_signed: SignedT = if (type_info.int.signedness == .signed) x else @bitCast(x);
    const shifted = x_signed >> (bits - 1);
    return @bitCast(shifted);
}

test "bitMask and signMask helpers" {
    try testing.expectEqual(@as(u32, 0x00000000), bitMask(u32, 0));
    try testing.expectEqual(@as(u32, 0xFFFFFFFF), bitMask(u32, 1));
    try testing.expectEqual(@as(u8, 0x00), bitMask(u8, 0));
    try testing.expectEqual(@as(u8, 0xFF), bitMask(u8, 1));
    try testing.expectEqual(@as(u64, 0x0000000000000000), bitMask(u64, 0));
    try testing.expectEqual(@as(u64, 0xFFFFFFFFFFFFFFFF), bitMask(u64, 1));

    try testing.expectEqual(@as(u32, 0xFFFFFFFF), signMask(i32, -1));
    try testing.expectEqual(@as(u32, 0xFFFFFFFF), signMask(i32, -100));
    try testing.expectEqual(@as(u32, 0x00000000), signMask(i32, 0));
    try testing.expectEqual(@as(u32, 0x00000000), signMask(i32, 1));
    try testing.expectEqual(@as(u32, 0x00000000), signMask(i32, 100));

    try testing.expectEqual(@as(u32, 0xFFFFFFFF), signMask(u32, 0x80000000)); // MSB set
    try testing.expectEqual(@as(u32, 0x00000000), signMask(u32, 0x7FFFFFFF)); // MSB clear
}

/// Montgomery reduction: for input x, returns y where y ≡ x*R^(-1) (mod q).
/// This is a generic implementation parameterized by the modulus q, its inverse qInv,
/// the Montgomery constant R, and the result bound.
///
/// For ML-DSA: R = 2^32, returns y < 2q
/// For ML-KEM: R = 2^16, returns y in range (-q, q)
fn montgomeryReduce(
    comptime InT: type,
    comptime OutT: type,
    comptime q: comptime_int,
    comptime qInv: comptime_int,
    comptime r_bits: comptime_int,
    x: InT,
) OutT {
    const mask = (@as(InT, 1) << r_bits) - 1;
    const m_full = (x *% qInv) & mask;
    const m: OutT = @truncate(m_full);

    const yR = x -% @as(InT, m) * @as(InT, q);
    const y_shifted = @as(std.meta.Int(.unsigned, @typeInfo(InT).Int.bits), @bitCast(yR)) >> r_bits;
    return @bitCast(@as(std.meta.Int(.unsigned, @typeInfo(OutT).Int.bits), @truncate(y_shifted)));
}

/// Uniform sampling using SHAKE-128 with rejection sampling.
/// Samples polynomial coefficients uniformly from [0, q) using rejection sampling.
///
/// Parameters:
/// - PolyType: The polynomial type to return
/// - q: Modulus
/// - bits_per_coef: Number of bits per coefficient (12 or 23)
/// - n: Number of coefficients
/// - seed: Random seed
/// - domain_sep: Domain separation bytes (appended to seed)
fn sampleUniformRejection(
    comptime PolyType: type,
    comptime q: comptime_int,
    comptime bits_per_coef: comptime_int,
    comptime n: comptime_int,
    seed: []const u8,
    domain_sep: []const u8,
) PolyType {
    var h = sha3.Shake128.init(.{});
    h.update(seed);
    h.update(domain_sep);

    const buf_len = sha3.Shake128.block_length; // 168 bytes
    var buf: [buf_len]u8 = undefined;

    var ret: PolyType = undefined;
    var coef_idx: usize = 0;

    if (bits_per_coef == 12) {
        // ML-KEM path: pack 2 coefficients per 3 bytes (12 bits each)
        outer: while (true) {
            h.squeeze(&buf);

            var j: usize = 0;
            while (j < buf_len) : (j += 3) {
                const b0 = @as(u16, buf[j]);
                const b1 = @as(u16, buf[j + 1]);
                const b2 = @as(u16, buf[j + 2]);

                const ts: [2]u16 = .{
                    b0 | ((b1 & 0xf) << 8),
                    (b1 >> 4) | (b2 << 4),
                };

                inline for (ts) |t| {
                    if (t < q) {
                        ret.cs[coef_idx] = @intCast(t);
                        coef_idx += 1;
                        if (coef_idx == n) break :outer;
                    }
                }
            }
        }
    } else if (bits_per_coef == 23) {
        // ML-DSA path: 1 coefficient per 3 bytes (23 bits)
        while (coef_idx < n) {
            h.squeeze(&buf);

            var j: usize = 0;
            while (j < buf_len and coef_idx < n) : (j += 3) {
                const t = (@as(u32, buf[j]) |
                    (@as(u32, buf[j + 1]) << 8) |
                    (@as(u32, buf[j + 2]) << 16)) & 0x7fffff;

                if (t < q) {
                    ret.cs[coef_idx] = @intCast(t);
                    coef_idx += 1;
                }
            }
        }
    } else {
        @compileError("bits_per_coef must be 12 or 23");
    }

    return ret;
}



---
File: /std/crypto/modes.zig
---

// Based on Go stdlib implementation

const std = @import("../std.zig");
const mem = std.mem;
const debug = std.debug;

/// Counter mode.
///
/// This mode creates a key stream by encrypting an incrementing counter using a block cipher, and adding it to the source material.
///
/// Important: the counter mode doesn't provide authenticated encryption: the ciphertext can be trivially modified without this being detected.
/// As a result, applications should generally never use it directly, but only in a construction that includes a MAC.
pub fn ctr(comptime BlockCipher: anytype, block_cipher: BlockCipher, dst: []u8, src: []const u8, iv: [BlockCipher.block_length]u8, endian: std.builtin.Endian) void {
    ctrSlice(BlockCipher, block_cipher, dst, src, iv, endian, 0, BlockCipher.block_length);
}

/// Counter mode with configurable counter position and size.
///
/// This extended version allows specifying where the counter is located within the IV block
/// and how many bytes it occupies. This is useful for modes like AES-GCM-SIV which use a
/// 32-bit counter at the beginning of the block.
///
/// @param counter_offset: Byte offset where the counter starts
/// @param counter_size: Size of the counter in bytes
pub fn ctrSlice(
    comptime BlockCipher: anytype,
    block_cipher: BlockCipher,
    dst: []u8,
    src: []const u8,
    iv: [BlockCipher.block_length]u8,
    endian: std.builtin.Endian,
    comptime counter_offset: usize,
    comptime counter_size: usize,
) void {
    debug.assert(dst.len >= src.len);
    const block_length = BlockCipher.block_length;
    debug.assert(counter_offset + counter_size <= block_length);
    debug.assert(counter_size > 0 and counter_size <= block_length);

    var counterBlock = iv;
    var i: usize = 0;

    const CounterInt = std.meta.Int(.unsigned, counter_size * 8);

    const parallel_count = BlockCipher.block.parallel.optimal_parallel_blocks;
    const wide_block_length = parallel_count * block_length;
    var cnt_val = mem.readInt(CounterInt, counterBlock[counter_offset..][0..counter_size], endian);
    if (src.len >= wide_block_length) {
        var counters: [parallel_count * block_length]u8 = undefined;
        inline for (0..parallel_count) |j| {
            counters[j * block_length ..][0..block_length].* = iv;
        }
        while (i + wide_block_length <= src.len) : (i += wide_block_length) {
            comptime var j = 0;
            inline while (j < parallel_count) : (j += 1) {
                mem.writeInt(CounterInt, counters[j * block_length + counter_offset ..][0..counter_size], cnt_val +% j, endian);
            }
            cnt_val += parallel_count;
            block_cipher.xorWide(parallel_count, dst[i .. i + wide_block_length][0..wide_block_length], src[i .. i + wide_block_length][0..wide_block_length], counters);
        }
        mem.writeInt(CounterInt, counterBlock[counter_offset..][0..counter_size], cnt_val, endian);
    }
    while (i + block_length <= src.len) : (i += block_length) {
        block_cipher.xor(dst[i .. i + block_length][0..block_length], src[i .. i + block_length][0..block_length], counterBlock);
        cnt_val +%= 1;
        mem.writeInt(CounterInt, counterBlock[counter_offset..][0..counter_size], cnt_val, endian);
    }
    if (i < src.len) {
        var pad: [block_length]u8 = @splat(0);
        const src_slice = src[i..];
        @memcpy(pad[0..src_slice.len], src_slice);
        block_cipher.xor(&pad, &pad, counterBlock);
        const pad_slice = pad[0 .. src.len - i];
        @memcpy(dst[i..][0..pad_slice.len], pad_slice);
    }
}

test "ctr mode" {
    const testing = std.testing;
    const aes = std.crypto.core.aes;

    // Test key and IV from NIST SP 800-38A
    const key = [_]u8{ 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
    const iv = [_]u8{ 0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa, 0xfb, 0xfc, 0xfd, 0xfe, 0xff };
    const ctx = aes.Aes128.initEnc(key);

    // Test 1: Empty input
    {
        const in = [_]u8{};
        const expected = [_]u8{};
        var out: [0]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 2: Single byte
    {
        const in = [_]u8{0x6b};
        const expected = [_]u8{0x87};
        var out: [1]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 3: Less than one block (15 bytes)
    {
        const in = [_]u8{ 0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17 };
        const expected = [_]u8{ 0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6 };
        var out: [15]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 4: Exactly one block (16 bytes)
    {
        const in = [_]u8{ 0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a };
        const expected = [_]u8{ 0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6, 0xce };
        var out: [16]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 5: One block plus one byte (17 bytes)
    {
        const in = [_]u8{ 0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a, 0xae };
        const expected = [_]u8{ 0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6, 0xce, 0x98 };
        var out: [17]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 6: Exactly two blocks (32 bytes)
    {
        const in = [_]u8{
            0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
            0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
        };
        const expected = [_]u8{
            0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6, 0xce,
            0x98, 0x06, 0xf6, 0x6b, 0x79, 0x70, 0xfd, 0xff, 0x86, 0x17, 0x18, 0x7b, 0xb9, 0xff, 0xfd, 0xff,
        };
        var out: [32]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 7: Two blocks plus 5 bytes (37 bytes)
    {
        const in = [_]u8{
            0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
            0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
            0x30, 0xc8, 0x1c, 0x46, 0xa3,
        };
        const expected = [_]u8{
            0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6, 0xce,
            0x98, 0x06, 0xf6, 0x6b, 0x79, 0x70, 0xfd, 0xff, 0x86, 0x17, 0x18, 0x7b, 0xb9, 0xff, 0xfd, 0xff,
            0x5a, 0xe4, 0xdf, 0x3e, 0xdb,
        };
        var out: [37]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 8: Four blocks (64 bytes) - NIST test vector
    {
        const in = [_]u8{
            0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
            0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
            0x30, 0xc8, 0x1c, 0x46, 0xa3, 0x5c, 0xe4, 0x11, 0xe5, 0xfb, 0xc1, 0x19, 0x1a, 0x0a, 0x52, 0xef,
            0xf6, 0x9f, 0x24, 0x45, 0xdf, 0x4f, 0x9b, 0x17, 0xad, 0x2b, 0x41, 0x7b, 0xe6, 0x6c, 0x37, 0x10,
        };
        const expected = [_]u8{
            0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6, 0xce,
            0x98, 0x06, 0xf6, 0x6b, 0x79, 0x70, 0xfd, 0xff, 0x86, 0x17, 0x18, 0x7b, 0xb9, 0xff, 0xfd, 0xff,
            0x5a, 0xe4, 0xdf, 0x3e, 0xdb, 0xd5, 0xd3, 0x5e, 0x5b, 0x4f, 0x09, 0x02, 0x0d, 0xb0, 0x3e, 0xab,
            0x1e, 0x03, 0x1d, 0xda, 0x2f, 0xbe, 0x03, 0xd1, 0x79, 0x21, 0x70, 0xa0, 0xf3, 0x00, 0x9c, 0xee,
        };
        var out: [64]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 9: Large input (> 2*block_length, 100 bytes)
    {
        // Create a 100-byte input by extending with zeros
        var in: [100]u8 = [_]u8{0} ** 100;
        @memcpy(in[0..64], &[_]u8{
            0x6b, 0xc1, 0xbe, 0xe2, 0x2e, 0x40, 0x9f, 0x96, 0xe9, 0x3d, 0x7e, 0x11, 0x73, 0x93, 0x17, 0x2a,
            0xae, 0x2d, 0x8a, 0x57, 0x1e, 0x03, 0xac, 0x9c, 0x9e, 0xb7, 0x6f, 0xac, 0x45, 0xaf, 0x8e, 0x51,
            0x30, 0xc8, 0x1c, 0x46, 0xa3, 0x5c, 0xe4, 0x11, 0xe5, 0xfb, 0xc1, 0x19, 0x1a, 0x0a, 0x52, 0xef,
            0xf6, 0x9f, 0x24, 0x45, 0xdf, 0x4f, 0x9b, 0x17, 0xad, 0x2b, 0x41, 0x7b, 0xe6, 0x6c, 0x37, 0x10,
        });

        // Expected output: first 64 bytes from NIST, then CTR continues with zeros
        var expected: [100]u8 = undefined;
        @memcpy(expected[0..64], &[_]u8{
            0x87, 0x4d, 0x61, 0x91, 0xb6, 0x20, 0xe3, 0x26, 0x1b, 0xef, 0x68, 0x64, 0x99, 0x0d, 0xb6, 0xce,
            0x98, 0x06, 0xf6, 0x6b, 0x79, 0x70, 0xfd, 0xff, 0x86, 0x17, 0x18, 0x7b, 0xb9, 0xff, 0xfd, 0xff,
            0x5a, 0xe4, 0xdf, 0x3e, 0xdb, 0xd5, 0xd3, 0x5e, 0x5b, 0x4f, 0x09, 0x02, 0x0d, 0xb0, 0x3e, 0xab,
            0x1e, 0x03, 0x1d, 0xda, 0x2f, 0xbe, 0x03, 0xd1, 0x79, 0x21, 0x70, 0xa0, 0xf3, 0x00, 0x9c, 0xee,
        });
        // Compute the rest with zeros XORed with keystream
        @memcpy(expected[64..], &[_]u8{
            0xb0, 0x0d, 0x47, 0xf8, 0x14, 0x8a, 0x91, 0x0e, 0xf0, 0x68, 0x30, 0x97, 0x90, 0x4b, 0xa5, 0x02,
            0x58, 0x99, 0x44, 0x5a, 0x4d, 0xe1, 0x01, 0xf5, 0x13, 0xca, 0xd1, 0x98, 0x7d, 0x89, 0xe9, 0x1b,
            0x3b, 0xd9, 0xac, 0x79,
        });

        var out: [100]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], iv, std.builtin.Endian.big);
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }

    // Test 10: Test with different endianness (little-endian counter)
    {
        const le_iv = [_]u8{ 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        const in = [_]u8{ 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff };

        // We'll compute the expected value from the actual encryption
        var out: [16]u8 = undefined;
        ctr(aes.AesEncryptCtx(aes.Aes128), ctx, out[0..], in[0..], le_iv, std.builtin.Endian.little);

        // The actual output for this test with little-endian counter=1
        const expected = [_]u8{ 0x7e, 0x48, 0x15, 0xa8, 0x16, 0x66, 0xf0, 0xea, 0xad, 0x3c, 0x07, 0x97, 0x2f, 0xe8, 0x25, 0xc1 };
        try testing.expectEqualSlices(u8, expected[0..], out[0..]);
    }
}



---
File: /std/crypto/pbkdf2.zig
---

const std = @import("std");
const mem = std.mem;
const maxInt = std.math.maxInt;
const OutputTooLongError = std.crypto.errors.OutputTooLongError;
const WeakParametersError = std.crypto.errors.WeakParametersError;

// RFC 2898 Section 5.2
//
// FromSpec:
//
// PBKDF2 applies a pseudorandom function (see Appendix B.1 for an
// example) to derive keys. The length of the derived key is essentially
// unbounded. (However, the maximum effective search space for the
// derived key may be limited by the structure of the underlying
// pseudorandom function. See Appendix B.1 for further discussion.)
// PBKDF2 is recommended for new applications.
//
// PBKDF2 (P, S, c, dk_len)
//
// Options:        PRF        underlying pseudorandom function (h_len
//                            denotes the length in octets of the
//                            pseudorandom function output)
//
// Input:          P          password, an octet string
//                 S          salt, an octet string
//                 c          iteration count, a positive integer
//                 dk_len      intended length in octets of the derived
//                            key, a positive integer, at most
//                            (2^32 - 1) * h_len
//
// Output:         DK         derived key, a dk_len-octet string

// Based on Apple's CommonKeyDerivation, based originally on code by Damien Bergamini.

/// Apply PBKDF2 to generate a key from a password.
///
/// PBKDF2 is defined in RFC 2898, and is a recommendation of NIST SP 800-132.
///
/// dk: Slice of appropriate size for generated key. Generally 16 or 32 bytes in length.
///             May be uninitialized. All bytes will be overwritten.
///             Maximum size is `maxInt(u32) * Hash.digest_length`
///             It is a programming error to pass buffer longer than the maximum size.
///
/// password: Arbitrary sequence of bytes of any length, including empty.
///
/// salt: Arbitrary sequence of bytes of any length, including empty. A common length is 8 bytes.
///
/// rounds: Iteration count. Must be greater than 0. Common values range from 1,000 to 100,000.
///         Larger iteration counts improve security by increasing the time required to compute
///         the dk. It is common to tune this parameter to achieve approximately 100ms.
///
/// Prf: Pseudo-random function to use. A common choice is `std.crypto.auth.hmac.sha2.HmacSha256`.
pub fn pbkdf2(dk: []u8, password: []const u8, salt: []const u8, rounds: u32, comptime Prf: type) (WeakParametersError || OutputTooLongError)!void {
    if (rounds < 1) return error.WeakParameters;

    const dk_len = dk.len;
    const h_len = Prf.mac_length;
    comptime std.debug.assert(h_len >= 1);

    // FromSpec:
    //
    //   1. If dk_len > maxInt(u32) * h_len, output "derived key too long" and
    //      stop.
    //
    if (dk_len / h_len >= maxInt(u32)) {
        // Counter starts at 1 and is 32 bit, so if we have to return more blocks, we would overflow
        return error.OutputTooLong;
    }

    // FromSpec:
    //
    //   2. Let l be the number of h_len-long blocks of bytes in the derived key,
    //      rounding up, and let r be the number of bytes in the last
    //      block
    //

    const blocks_count = @as(u32, @intCast(std.math.divCeil(usize, dk_len, h_len) catch unreachable));
    var r = dk_len % h_len;
    if (r == 0) {
        r = h_len;
    }

    // FromSpec:
    //
    //   3. For each block of the derived key apply the function F defined
    //      below to the password P, the salt S, the iteration count c, and
    //      the block index to compute the block:
    //
    //                T_1 = F (P, S, c, 1) ,
    //                T_2 = F (P, S, c, 2) ,
    //                ...
    //                T_l = F (P, S, c, l) ,
    //
    //      where the function F is defined as the exclusive-or sum of the
    //      first c iterates of the underlying pseudorandom function PRF
    //      applied to the password P and the concatenation of the salt S
    //      and the block index i:
    //
    //                F (P, S, c, i) = U_1 \xor U_2 \xor ... \xor U_c
    //
    //  where
    //
    //            U_1 = PRF (P, S || INT (i)) ,
    //            U_2 = PRF (P, U_1) ,
    //            ...
    //            U_c = PRF (P, U_{c-1}) .
    //
    //  Here, INT (i) is a four-octet encoding of the integer i, most
    //  significant octet first.
    //
    //  4. Concatenate the blocks and extract the first dk_len octets to
    //  produce a derived key DK:
    //
    //            DK = T_1 || T_2 ||  ...  || T_l<0..r-1>

    var block: u32 = 0;
    while (block < blocks_count) : (block += 1) {
        var prev_block: [h_len]u8 = undefined;
        var new_block: [h_len]u8 = undefined;

        // U_1 = PRF (P, S || INT (i))
        const block_index = mem.toBytes(mem.nativeToBig(u32, block + 1)); // Block index starts at 0001
        var ctx = Prf.init(password);
        ctx.update(salt);
        ctx.update(block_index[0..]);
        ctx.final(prev_block[0..]);

        // Choose portion of DK to write into (T_n) and initialize
        const offset = block * h_len;
        const block_len = if (block != blocks_count - 1) h_len else r;
        const dk_block: []u8 = dk[offset..][0..block_len];
        @memcpy(dk_block, prev_block[0..dk_block.len]);

        var i: u32 = 1;
        while (i < rounds) : (i += 1) {
            // U_c = PRF (P, U_{c-1})
            Prf.create(&new_block, prev_block[0..], password);
            prev_block = new_block;

            // F (P, S, c, i) = U_1 \xor U_2 \xor ... \xor U_c
            for (dk_block, 0..) |_, j| {
                dk_block[j] ^= new_block[j];
            }
        }
    }
}

const htest = @import("test.zig");
const HmacSha1 = std.crypto.auth.hmac.HmacSha1;

// RFC 6070 PBKDF2 HMAC-SHA1 Test Vectors

test "RFC 6070 one iteration" {
    const p = "password";
    const s = "salt";
    const c = 1;
    const dk_len = 20;

    var dk: [dk_len]u8 = undefined;

    try pbkdf2(&dk, p, s, c, HmacSha1);

    const expected = "0c60c80f961f0e71f3a9b524af6012062fe037a6";

    try htest.assertEqual(expected, dk[0..]);
}

test "RFC 6070 two iterations" {
    const p = "password";
    const s = "salt";
    const c = 2;
    const dk_len = 20;

    var dk: [dk_len]u8 = undefined;

    try pbkdf2(&dk, p, s, c, HmacSha1);

    const expected = "ea6c014dc72d6f8ccd1ed92ace1d41f0d8de8957";

    try htest.assertEqual(expected, dk[0..]);
}

test "RFC 6070 4096 iterations" {
    const p = "password";
    const s = "salt";
    const c = 4096;
    const dk_len = 20;

    var dk: [dk_len]u8 = undefined;

    try pbkdf2(&dk, p, s, c, HmacSha1);

    const expected = "4b007901b765489abead49d926f721d065a429c1";

    try htest.assertEqual(expected, dk[0..]);
}

test "RFC 6070 16,777,216 iterations" {
    // These iteration tests are slow so we always skip them. Results have been verified.
    if (true) {
        return error.SkipZigTest;
    }

    const p = "password";
    const s = "salt";
    const c = 16777216;
    const dk_len = 20;

    var dk = [_]u8{0} ** dk_len;

    try pbkdf2(&dk, p, s, c, HmacSha1);

    const expected = "eefe3d61cd4da4e4e9945b3d6ba2158c2634e984";

    try htest.assertEqual(expected, dk[0..]);
}

test "RFC 6070 multi-block salt and password" {
    const p = "passwordPASSWORDpassword";
    const s = "saltSALTsaltSALTsaltSALTsaltSALTsalt";
    const c = 4096;
    const dk_len = 25;

    var dk: [dk_len]u8 = undefined;

    try pbkdf2(&dk, p, s, c, HmacSha1);

    const expected = "3d2eec4fe41c849b80c8d83662c0e44a8b291a964cf2f07038";

    try htest.assertEqual(expected, dk[0..]);
}

test "RFC 6070 embedded NUL" {
    const p = "pass\x00word";
    const s = "sa\x00lt";
    const c = 4096;
    const dk_len = 16;

    var dk: [dk_len]u8 = undefined;

    try pbkdf2(&dk, p, s, c, HmacSha1);

    const expected = "56fa6aa75548099dcc37d7f03425e0c3";

    try htest.assertEqual(expected, dk[0..]);
}

test "Very large dk_len" {
    // This test allocates 8GB of memory and is expected to take several hours to run.
    if (true) {
        return error.SkipZigTest;
    }
    const p = "password";
    const s = "salt";
    const c = 1;
    const dk_len = 1 << 33;

    const dk = try std.testing.allocator.alloc(u8, dk_len);
    defer std.testing.allocator.free(dk);

    // Just verify this doesn't crash with an overflow
    try pbkdf2(dk, p, s, c, HmacSha1);
}



---
File: /std/crypto/phc_encoding.zig
---

// https://github.com/P-H-C/phc-string-format

const std = @import("std");
const fmt = std.fmt;
const mem = std.mem;
const meta = std.meta;
const Writer = std.Io.Writer;

const fields_delimiter = "$";
const fields_delimiter_scalar = '$';
const version_param_name = "v";
const params_delimiter = ",";
const params_delimiter_scalar = ',';
const kv_delimiter = "=";
const kv_delimiter_scalar = '=';

pub const Error = std.crypto.errors.EncodingError || error{NoSpaceLeft};

const B64Decoder = std.base64.standard_no_pad.Decoder;
const B64Encoder = std.base64.standard_no_pad.Encoder;

/// A wrapped binary value whose maximum size is `max_len`.
///
/// This type must be used whenever a binary value is encoded in a PHC-formatted string.
/// This includes `salt`, `hash`, and any other binary parameters such as keys.
///
/// Once initialized, the actual value can be read with the `constSlice()` function.
pub fn BinValue(comptime max_len: usize) type {
    return struct {
        const Self = @This();
        const capacity = max_len;
        const max_encoded_length = B64Encoder.calcSize(max_len);

        buf: [max_len]u8 = undefined,
        len: usize = 0,

        /// Wrap an existing byte slice
        pub fn fromSlice(slice: []const u8) Error!Self {
            if (slice.len > capacity) return Error.NoSpaceLeft;
            var bin_value: Self = undefined;
            @memcpy(bin_value.buf[0..slice.len], slice);
            bin_value.len = slice.len;
            return bin_value;
        }

        /// Return the slice containing the actual value.
        pub fn constSlice(self: *const Self) []const u8 {
            return self.buf[0..self.len];
        }

        fn fromB64(self: *Self, str: []const u8) !void {
            const len = B64Decoder.calcSizeForSlice(str) catch return Error.InvalidEncoding;
            if (len > self.buf.len) return Error.NoSpaceLeft;
            B64Decoder.decode(&self.buf, str) catch return Error.InvalidEncoding;
            self.len = len;
        }

        fn toB64(self: *const Self, buf: []u8) ![]const u8 {
            const value = self.constSlice();
            const len = B64Encoder.calcSize(value.len);
            if (len > buf.len) return Error.NoSpaceLeft;
            return B64Encoder.encode(buf, value);
        }
    };
}

/// Deserialize a PHC-formatted string into a structure `HashResult`.
///
/// Required field in the `HashResult` structure:
///   - `alg_id`: algorithm identifier
/// Optional, special fields:
///   - `alg_version`: algorithm version (unsigned integer)
///   - `salt`: salt
///   - `hash`: output of the hash function
///
/// Other fields will also be deserialized from the function parameters section.
pub fn deserialize(comptime HashResult: type, str: []const u8) Error!HashResult {
    if (@hasField(HashResult, version_param_name)) {
        @compileError("Field name '" ++ version_param_name ++ "'' is reserved for the algorithm version");
    }

    var out = mem.zeroes(HashResult);
    var it = mem.splitScalar(u8, str, fields_delimiter_scalar);
    var set_fields: usize = 0;

    while (true) {
        // Read the algorithm identifier
        if ((it.next() orelse return Error.InvalidEncoding).len != 0) return Error.InvalidEncoding;
        out.alg_id = it.next() orelse return Error.InvalidEncoding;
        set_fields += 1;

        // Read the optional version number
        var field = it.next() orelse break;
        if (kvSplit(field)) |opt_version| {
            if (mem.eql(u8, opt_version.key, version_param_name)) {
                if (@hasField(HashResult, "alg_version")) {
                    const ValueType = switch (@typeInfo(@TypeOf(out.alg_version))) {
                        .optional => |opt| opt.child,
                        else => @TypeOf(out.alg_version),
                    };
                    out.alg_version = fmt.parseUnsigned(
                        ValueType,
                        opt_version.value,
                        10,
                    ) catch return Error.InvalidEncoding;
                    set_fields += 1;
                }
                field = it.next() orelse break;
            }
        } else |_| {}

        // Read optional parameters
        var has_params = false;
        var it_params = mem.splitScalar(u8, field, params_delimiter_scalar);
        while (it_params.next()) |params| {
            const param = kvSplit(params) catch break;
            var found = false;
            inline for (comptime meta.fields(HashResult)) |p| {
                if (mem.eql(u8, p.name, param.key)) {
                    switch (@typeInfo(p.type)) {
                        .int => @field(out, p.name) = fmt.parseUnsigned(
                            p.type,
                            param.value,
                            10,
                        ) catch return Error.InvalidEncoding,
                        .pointer => |ptr| {
                            if (!ptr.is_const) @compileError("Value slice must be constant");
                            @field(out, p.name) = param.value;
                        },
                        .@"struct" => try @field(out, p.name).fromB64(param.value),
                        else => std.debug.panic(
                            "Value for [{s}] must be an integer, a constant slice or a BinValue",
                            .{p.name},
                        ),
                    }
                    set_fields += 1;
                    found = true;
                    break;
                }
            }
            if (!found) return Error.InvalidEncoding; // An unexpected parameter was found in the string
            has_params = true;
        }

        // No separator between an empty parameters set and the salt
        if (has_params) field = it.next() orelse break;

        // Read an optional salt
        if (@hasField(HashResult, "salt")) {
            try out.salt.fromB64(field);
            set_fields += 1;
        } else {
            return Error.InvalidEncoding;
        }

        // Read an optional hash
        field = it.next() orelse break;
        if (@hasField(HashResult, "hash")) {
            try out.hash.fromB64(field);
            set_fields += 1;
        } else {
            return Error.InvalidEncoding;
        }
        break;
    }

    // Check that all the required fields have been set, excluding optional values and parameters
    // with default values
    var expected_fields: usize = 0;
    inline for (comptime meta.fields(HashResult)) |p| {
        if (@typeInfo(p.type) != .optional and p.default_value_ptr == null) {
            expected_fields += 1;
        }
    }
    if (set_fields < expected_fields) return Error.InvalidEncoding;

    return out;
}

/// Serialize parameters into a PHC string.
///
/// Required field for `params`:
///   - `alg_id`: algorithm identifier
/// Optional, special fields:
///   - `alg_version`: algorithm version (unsigned integer)
///   - `salt`: salt
///   - `hash`: output of the hash function
///
/// `params` can also include any additional parameters.
pub fn serialize(params: anytype, str: []u8) Error![]const u8 {
    var w: Writer = .fixed(str);
    serializeTo(params, &w) catch return error.NoSpaceLeft;
    return w.buffered();
}

/// Compute the number of bytes required to serialize `params`
pub fn calcSize(params: anytype) usize {
    var trash: [128]u8 = undefined;
    var d: Writer.Discarding = .init(&trash);
    serializeTo(params, &d.writer) catch unreachable;
    return @intCast(d.fullCount());
}

fn serializeTo(params: anytype, out: *std.Io.Writer) !void {
    const HashResult = @TypeOf(params);

    if (@hasField(HashResult, version_param_name)) {
        @compileError("Field name '" ++ version_param_name ++ "'' is reserved for the algorithm version");
    }

    try out.writeAll(fields_delimiter);
    try out.writeAll(params.alg_id);

    if (@hasField(HashResult, "alg_version")) {
        if (@typeInfo(@TypeOf(params.alg_version)) == .optional) {
            if (params.alg_version) |alg_version| {
                try out.print(
                    "{s}{s}{s}{}",
                    .{ fields_delimiter, version_param_name, kv_delimiter, alg_version },
                );
            }
        } else {
            try out.print(
                "{s}{s}{s}{}",
                .{ fields_delimiter, version_param_name, kv_delimiter, params.alg_version },
            );
        }
    }

    var has_params = false;
    inline for (comptime meta.fields(HashResult)) |p| {
        if (comptime !(mem.eql(u8, p.name, "alg_id") or
            mem.eql(u8, p.name, "alg_version") or
            mem.eql(u8, p.name, "hash") or
            mem.eql(u8, p.name, "salt")))
        {
            const value = @field(params, p.name);
            try out.writeAll(if (has_params) params_delimiter else fields_delimiter);
            if (@typeInfo(p.type) == .@"struct") {
                var buf: [@TypeOf(value).max_encoded_length]u8 = undefined;
                try out.print("{s}{s}{s}", .{ p.name, kv_delimiter, try value.toB64(&buf) });
            } else {
                try out.print(
                    if (@typeInfo(@TypeOf(value)) == .pointer) "{s}{s}{s}" else "{s}{s}{}",
                    .{ p.name, kv_delimiter, value },
                );
            }
            has_params = true;
        }
    }

    var has_salt = false;
    if (@hasField(HashResult, "salt")) {
        var buf: [@TypeOf(params.salt).max_encoded_length]u8 = undefined;
        try out.print("{s}{s}", .{ fields_delimiter, try params.salt.toB64(&buf) });
        has_salt = true;
    }

    if (@hasField(HashResult, "hash")) {
        var buf: [@TypeOf(params.hash).max_encoded_length]u8 = undefined;
        if (!has_salt) try out.writeAll(fields_delimiter);
        try out.print("{s}{s}", .{ fields_delimiter, try params.hash.toB64(&buf) });
    }
}

// Split a `key=value` string into `key` and `value`
fn kvSplit(str: []const u8) !struct { key: []const u8, value: []const u8 } {
    var it = mem.splitScalar(u8, str, kv_delimiter_scalar);
    const key = it.first();
    const value = it.next() orelse return Error.InvalidEncoding;
    return .{ .key = key, .value = value };
}

test "phc format - encoding/decoding" {
    const Input = struct {
        str: []const u8,
        HashResult: type,
    };
    const inputs = [_]Input{
        .{
            .str = "$argon2id$v=19$key=a2V5,m=4096,t=0,p=1$X1NhbHQAAAAAAAAAAAAAAA$bWh++MKN1OiFHKgIWTLvIi1iHicmHH7+Fv3K88ifFfI",
            .HashResult = struct {
                alg_id: []const u8,
                alg_version: u16,
                key: BinValue(16),
                m: usize,
                t: u64,
                p: u32,
                salt: BinValue(16),
                hash: BinValue(32),
            },
        },
        .{
            .str = "$scrypt$v=1$ln=15,r=8,p=1$c2FsdHNhbHQ$dGVzdHBhc3M",
            .HashResult = struct {
                alg_id: []const u8,
                alg_version: ?u30,
                ln: u6,
                r: u30,
                p: u30,
                salt: BinValue(16),
                hash: BinValue(16),
            },
        },
        .{
            .str = "$scrypt",
            .HashResult = struct { alg_id: []const u8 },
        },
        .{ .str = "$scrypt$v=1", .HashResult = struct { alg_id: []const u8, alg_version: u16 } },
        .{
            .str = "$scrypt$ln=15,r=8,p=1",
            .HashResult = struct { alg_id: []const u8, alg_version: ?u30, ln: u6, r: u30, p: u30 },
        },
        .{
            .str = "$scrypt$c2FsdHNhbHQ",
            .HashResult = struct { alg_id: []const u8, salt: BinValue(16) },
        },
        .{
            .str = "$scrypt$v=1$ln=15,r=8,p=1$c2FsdHNhbHQ",
            .HashResult = struct {
                alg_id: []const u8,
                alg_version: u16,
                ln: u6,
                r: u30,
                p: u30,
                salt: BinValue(16),
            },
        },
        .{
            .str = "$scrypt$v=1$ln=15,r=8,p=1",
            .HashResult = struct { alg_id: []const u8, alg_version: ?u30, ln: u6, r: u30, p: u30 },
        },
        .{
            .str = "$scrypt$v=1$c2FsdHNhbHQ$dGVzdHBhc3M",
            .HashResult = struct {
                alg_id: []const u8,
                alg_version: u16,
                salt: BinValue(16),
                hash: BinValue(16),
            },
        },
        .{
            .str = "$scrypt$v=1$c2FsdHNhbHQ",
            .HashResult = struct { alg_id: []const u8, alg_version: u16, salt: BinValue(16) },
        },
        .{
            .str = "$scrypt$c2FsdHNhbHQ$dGVzdHBhc3M",
            .HashResult = struct { alg_id: []const u8, salt: BinValue(16), hash: BinValue(16) },
        },
    };
    inline for (inputs) |input| {
        const v = try deserialize(input.HashResult, input.str);
        var buf: [input.str.len]u8 = undefined;
        const s1 = try serialize(v, &buf);
        try std.testing.expectEqualSlices(u8, input.str, s1);
    }
}

test "phc format - empty input string" {
    const s = "";
    const v = deserialize(struct { alg_id: []const u8 }, s);
    try std.testing.expectError(Error.InvalidEncoding, v);
}

test "phc format - hash without salt" {
    const s = "$scrypt";
    const v = deserialize(struct { alg_id: []const u8, hash: BinValue(16) }, s);
    try std.testing.expectError(Error.InvalidEncoding, v);
}

test "phc format - calcSize" {
    const s = "$scrypt$v=1$ln=15,r=8,p=1$c2FsdHNhbHQ$dGVzdHBhc3M";
    const v = try deserialize(struct {
        alg_id: []const u8,
        alg_version: u16,
        ln: u6,
        r: u30,
        p: u30,
        salt: BinValue(8),
        hash: BinValue(8),
    }, s);
    try std.testing.expectEqual(calcSize(v), s.len);
}



---
File: /std/crypto/poly1305.zig
---

const std = @import("../std.zig");
const mem = std.mem;
const mulWide = std.math.mulWide;

pub const Poly1305 = struct {
    pub const block_length: usize = 16;
    pub const mac_length = 16;
    pub const key_length = 32;

    // constant multiplier (from the secret key)
    r: [2]u64,
    // accumulated hash
    h: [3]u64 = [_]u64{ 0, 0, 0 },
    // random number added at the end (from the secret key)
    end_pad: [2]u64,
    // how many bytes are waiting to be processed in a partial block
    leftover: usize = 0,
    // partial block buffer
    buf: [block_length]u8 align(16) = undefined,

    pub fn init(key: *const [key_length]u8) Poly1305 {
        return Poly1305{
            .r = [_]u64{
                mem.readInt(u64, key[0..8], .little) & 0x0ffffffc0fffffff,
                mem.readInt(u64, key[8..16], .little) & 0x0ffffffc0ffffffc,
            },
            .end_pad = [_]u64{
                mem.readInt(u64, key[16..24], .little),
                mem.readInt(u64, key[24..32], .little),
            },
        };
    }

    fn add(a: u64, b: u64, c: u1) struct { u64, u1 } {
        const v1 = @addWithOverflow(a, b);
        const v2 = @addWithOverflow(v1[0], c);
        return .{ v2[0], v1[1] | v2[1] };
    }

    fn sub(a: u64, b: u64, c: u1) struct { u64, u1 } {
        const v1 = @subWithOverflow(a, b);
        const v2 = @subWithOverflow(v1[0], c);
        return .{ v2[0], v1[1] | v2[1] };
    }

    fn blocks(st: *Poly1305, m: []const u8, comptime last: bool) void {
        const hibit: u64 = if (last) 0 else 1;
        const r0 = st.r[0];
        const r1 = st.r[1];

        var h0 = st.h[0];
        var h1 = st.h[1];
        var h2 = st.h[2];

        var i: usize = 0;

        while (i + block_length <= m.len) : (i += block_length) {
            const in0 = mem.readInt(u64, m[i..][0..8], .little);
            const in1 = mem.readInt(u64, m[i + 8 ..][0..8], .little);

            // Add the input message to H
            var v = @addWithOverflow(h0, in0);
            h0 = v[0];
            v = add(h1, in1, v[1]);
            h1 = v[0];
            h2 +%= v[1] +% hibit;

            // Compute H * R
            const m0 = mulWide(u64, h0, r0);
            const h1r0 = mulWide(u64, h1, r0);
            const h0r1 = mulWide(u64, h0, r1);
            const h2r0 = mulWide(u64, h2, r0);
            const h1r1 = mulWide(u64, h1, r1);
            const m3 = mulWide(u64, h2, r1);
            const m1 = h1r0 +% h0r1;
            const m2 = h2r0 +% h1r1;

            const t0 = @as(u64, @truncate(m0));
            v = @addWithOverflow(@as(u64, @truncate(m1)), @as(u64, @truncate(m0 >> 64)));
            const t1 = v[0];
            v = add(@as(u64, @truncate(m2)), @as(u64, @truncate(m1 >> 64)), v[1]);
            const t2 = v[0];
            v = add(@as(u64, @truncate(m3)), @as(u64, @truncate(m2 >> 64)), v[1]);
            const t3 = v[0];

            // Partial reduction
            h0 = t0;
            h1 = t1;
            h2 = t2 & 3;

            // Add c*(4+1)
            const cclo = t2 & ~@as(u64, 3);
            const cchi = t3;
            v = @addWithOverflow(h0, cclo);
            h0 = v[0];
            v = add(h1, cchi, v[1]);
            h1 = v[0];
            h2 +%= v[1];
            const cc = (cclo | (@as(u128, cchi) << 64)) >> 2;
            v = @addWithOverflow(h0, @as(u64, @truncate(cc)));
            h0 = v[0];
            v = add(h1, @as(u64, @truncate(cc >> 64)), v[1]);
            h1 = v[0];
            h2 +%= v[1];
        }
        st.h = [_]u64{ h0, h1, h2 };
    }

    pub fn update(st: *Poly1305, m: []const u8) void {
        var mb = m;

        // handle leftover
        if (st.leftover > 0) {
            const want = @min(block_length - st.leftover, mb.len);
            const mc = mb[0..want];
            for (mc, 0..) |x, i| {
                st.buf[st.leftover + i] = x;
            }
            mb = mb[want..];
            st.leftover += want;
            if (st.leftover < block_length) {
                return;
            }
            st.blocks(&st.buf, false);
            st.leftover = 0;
        }

        // process full blocks
        if (mb.len >= block_length) {
            const want = mb.len & ~(block_length - 1);
            st.blocks(mb[0..want], false);
            mb = mb[want..];
        }

        // store leftover
        if (mb.len > 0) {
            for (mb, 0..) |x, i| {
                st.buf[st.leftover + i] = x;
            }
            st.leftover += mb.len;
        }
    }

    /// Zero-pad to align the next input to the first byte of a block
    pub fn pad(st: *Poly1305) void {
        if (st.leftover == 0) {
            return;
        }
        @memset(st.buf[st.leftover..], 0);
        st.blocks(&st.buf, false);
        st.leftover = 0;
    }

    pub fn final(st: *Poly1305, out: *[mac_length]u8) void {
        if (st.leftover > 0) {
            var i = st.leftover;
            st.buf[i] = 1;
            i += 1;
            @memset(st.buf[i..], 0);
            st.blocks(&st.buf, true);
        }

        var h0 = st.h[0];
        var h1 = st.h[1];
        const h2 = st.h[2];

        // H - (2^130 - 5)
        var v = @subWithOverflow(h0, 0xfffffffffffffffb);
        const h_p0 = v[0];
        v = sub(h1, 0xffffffffffffffff, v[1]);
        const h_p1 = v[0];
        v = sub(h2, 0x0000000000000003, v[1]);

        // Final reduction, subtract 2^130-5 from H if H >= 2^130-5
        const mask = @as(u64, v[1]) -% 1;
        h0 ^= mask & (h0 ^ h_p0);
        h1 ^= mask & (h1 ^ h_p1);

        // Add the first half of the key, we intentionally don't use @addWithOverflow() here.
        st.h[0] = h0 +% st.end_pad[0];
        const c = ((h0 & st.end_pad[0]) | ((h0 | st.end_pad[0]) & ~st.h[0])) >> 63;
        st.h[1] = h1 +% st.end_pad[1] +% c;

        mem.writeInt(u64, out[0..8], st.h[0], .little);
        mem.writeInt(u64, out[8..16], st.h[1], .little);

        std.crypto.secureZero(Poly1305, st[0..1]);
    }

    pub fn create(out: *[mac_length]u8, msg: []const u8, key: *const [key_length]u8) void {
        var st = Poly1305.init(key);
        st.update(msg);
        st.final(out);
    }
};

test "rfc7439 vector1" {
    const expected_mac = "\xa8\x06\x1d\xc1\x30\x51\x36\xc6\xc2\x2b\x8b\xaf\x0c\x01\x27\xa9";

    const msg = "Cryptographic Forum Research Group";
    const key = "\x85\xd6\xbe\x78\x57\x55\x6d\x33\x7f\x44\x52\xfe\x42\xd5\x06\xa8" ++
        "\x01\x03\x80\x8a\xfb\x0d\xb2\xfd\x4a\xbf\xf6\xaf\x41\x49\xf5\x1b";

    var mac: [16]u8 = undefined;
    Poly1305.create(mac[0..], msg, key);

    try std.testing.expectEqualSlices(u8, expected_mac, &mac);
}

test "requiring a final reduction" {
    const expected_mac = [_]u8{ 25, 13, 249, 42, 164, 57, 99, 60, 149, 181, 74, 74, 13, 63, 121, 6 };
    const msg = [_]u8{ 253, 193, 249, 146, 70, 6, 214, 226, 131, 213, 241, 116, 20, 24, 210, 224, 65, 151, 255, 104, 133 };
    const key = [_]u8{ 190, 63, 95, 57, 155, 103, 77, 170, 7, 98, 106, 44, 117, 186, 90, 185, 109, 118, 184, 24, 69, 41, 166, 243, 119, 132, 151, 61, 52, 43, 64, 250 };
    var mac: [16]u8 = undefined;
    Poly1305.create(mac[0..], &msg, &key);
    try std.testing.expectEqualSlices(u8, &expected_mac, &mac);
}



---
File: /std/crypto/salsa20.zig
---

const std = @import("std");
const builtin = @import("builtin");
const crypto = std.crypto;
const debug = std.debug;
const math = std.math;
const mem = std.mem;

const Poly1305 = crypto.onetimeauth.Poly1305;
const Blake2b = crypto.hash.blake2.Blake2b;
const X25519 = crypto.dh.X25519;

const AuthenticationError = crypto.errors.AuthenticationError;
const IdentityElementError = crypto.errors.IdentityElementError;
const WeakPublicKeyError = crypto.errors.WeakPublicKeyError;

/// The Salsa cipher with 20 rounds.
pub const Salsa20 = Salsa(20);

/// The XSalsa cipher with 20 rounds.
pub const XSalsa20 = XSalsa(20);

fn SalsaVecImpl(comptime rounds: comptime_int) type {
    return struct {
        const Lane = @Vector(4, u32);
        const Half = @Vector(2, u32);
        const BlockVec = [4]Lane;

        fn initContext(key: [8]u32, d: [4]u32) BlockVec {
            const c = "expand 32-byte k";
            const constant_le = comptime [4]u32{
                mem.readInt(u32, c[0..4], .little),
                mem.readInt(u32, c[4..8], .little),
                mem.readInt(u32, c[8..12], .little),
                mem.readInt(u32, c[12..16], .little),
            };
            return BlockVec{
                Lane{ key[0], key[1], key[2], key[3] },
                Lane{ key[4], key[5], key[6], key[7] },
                Lane{ constant_le[0], constant_le[1], constant_le[2], constant_le[3] },
                Lane{ d[0], d[1], d[2], d[3] },
            };
        }

        fn salsaCore(x: *BlockVec, input: BlockVec, comptime feedback: bool) void {
            const n1n2n3n0 = Lane{ input[3][1], input[3][2], input[3][3], input[3][0] };
            const n1n2 = Half{ n1n2n3n0[0], n1n2n3n0[1] };
            const n3n0 = Half{ n1n2n3n0[2], n1n2n3n0[3] };
            const k0k1 = Half{ input[0][0], input[0][1] };
            const k2k3 = Half{ input[0][2], input[0][3] };
            const k4k5 = Half{ input[1][0], input[1][1] };
            const k6k7 = Half{ input[1][2], input[1][3] };
            const n0k0 = Half{ n3n0[1], k0k1[0] };
            const k0n0 = Half{ n0k0[1], n0k0[0] };
            const k4k5k0n0 = Lane{ k4k5[0], k4k5[1], k0n0[0], k0n0[1] };
            const k1k6 = Half{ k0k1[1], k6k7[0] };
            const k6k1 = Half{ k1k6[1], k1k6[0] };
            const n1n2k6k1 = Lane{ n1n2[0], n1n2[1], k6k1[0], k6k1[1] };
            const k7n3 = Half{ k6k7[1], n3n0[0] };
            const n3k7 = Half{ k7n3[1], k7n3[0] };
            const k2k3n3k7 = Lane{ k2k3[0], k2k3[1], n3k7[0], n3k7[1] };

            var diag0 = input[2];
            var diag1 = @shuffle(u32, k4k5k0n0, undefined, [_]i32{ 1, 2, 3, 0 });
            var diag2 = @shuffle(u32, n1n2k6k1, undefined, [_]i32{ 1, 2, 3, 0 });
            var diag3 = @shuffle(u32, k2k3n3k7, undefined, [_]i32{ 1, 2, 3, 0 });

            const start0 = diag0;
            const start1 = diag1;
            const start2 = diag2;
            const start3 = diag3;

            var i: usize = 0;
            while (i < rounds) : (i += 2) {
                diag3 ^= math.rotl(Lane, diag1 +% diag0, 7);
                diag2 ^= math.rotl(Lane, diag0 +% diag3, 9);
                diag1 ^= math.rotl(Lane, diag3 +% diag2, 13);
                diag0 ^= math.rotl(Lane, diag2 +% diag1, 18);

                diag3 = @shuffle(u32, diag3, undefined, [_]i32{ 3, 0, 1, 2 });
                diag2 = @shuffle(u32, diag2, undefined, [_]i32{ 2, 3, 0, 1 });
                diag1 = @shuffle(u32, diag1, undefined, [_]i32{ 1, 2, 3, 0 });

                diag1 ^= math.rotl(Lane, diag3 +% diag0, 7);
                diag2 ^= math.rotl(Lane, diag0 +% diag1, 9);
                diag3 ^= math.rotl(Lane, diag1 +% diag2, 13);
                diag0 ^= math.rotl(Lane, diag2 +% diag3, 18);

                diag1 = @shuffle(u32, diag1, undefined, [_]i32{ 3, 0, 1, 2 });
                diag2 = @shuffle(u32, diag2, undefined, [_]i32{ 2, 3, 0, 1 });
                diag3 = @shuffle(u32, diag3, undefined, [_]i32{ 1, 2, 3, 0 });
            }

            if (feedback) {
                diag0 +%= start0;
                diag1 +%= start1;
                diag2 +%= start2;
                diag3 +%= start3;
            }

            const x0x1x10x11 = Lane{ diag0[0], diag1[1], diag0[2], diag1[3] };
            const x12x13x6x7 = Lane{ diag1[0], diag2[1], diag1[2], diag2[3] };
            const x8x9x2x3 = Lane{ diag2[0], diag3[1], diag2[2], diag3[3] };
            const x4x5x14x15 = Lane{ diag3[0], diag0[1], diag3[2], diag0[3] };

            x[0] = Lane{ x0x1x10x11[0], x0x1x10x11[1], x8x9x2x3[2], x8x9x2x3[3] };
            x[1] = Lane{ x4x5x14x15[0], x4x5x14x15[1], x12x13x6x7[2], x12x13x6x7[3] };
            x[2] = Lane{ x8x9x2x3[0], x8x9x2x3[1], x0x1x10x11[2], x0x1x10x11[3] };
            x[3] = Lane{ x12x13x6x7[0], x12x13x6x7[1], x4x5x14x15[2], x4x5x14x15[3] };
        }

        fn hashToBytes(out: *[64]u8, x: BlockVec) void {
            var i: usize = 0;
            while (i < 4) : (i += 1) {
                mem.writeInt(u32, out[16 * i + 0 ..][0..4], x[i][0], .little);
                mem.writeInt(u32, out[16 * i + 4 ..][0..4], x[i][1], .little);
                mem.writeInt(u32, out[16 * i + 8 ..][0..4], x[i][2], .little);
                mem.writeInt(u32, out[16 * i + 12 ..][0..4], x[i][3], .little);
            }
        }

        fn salsaXor(out: []u8, in: []const u8, key: [8]u32, d: [4]u32) void {
            var ctx = initContext(key, d);
            var x: BlockVec = undefined;
            var buf: [64]u8 = undefined;
            var i: usize = 0;
            while (i + 64 <= in.len) : (i += 64) {
                salsaCore(x[0..], ctx, true);
                hashToBytes(buf[0..], x);
                var xout = out[i..];
                const xin = in[i..];
                var j: usize = 0;
                while (j < 64) : (j += 1) {
                    xout[j] = xin[j];
                }
                j = 0;
                while (j < 64) : (j += 1) {
                    xout[j] ^= buf[j];
                }
                ctx[3][2] +%= 1;
                if (ctx[3][2] == 0) {
                    ctx[3][3] += 1;
                }
            }
            if (i < in.len) {
                salsaCore(x[0..], ctx, true);
                hashToBytes(buf[0..], x);

                var xout = out[i..];
                const xin = in[i..];
                var j: usize = 0;
                while (j < in.len % 64) : (j += 1) {
                    xout[j] = xin[j] ^ buf[j];
                }
            }
        }

        fn hsalsa(input: [16]u8, key: [32]u8) [32]u8 {
            var c: [4]u32 = undefined;
            for (c, 0..) |_, i| {
                c[i] = mem.readInt(u32, input[4 * i ..][0..4], .little);
            }
            const ctx = initContext(keyToWords(key), c);
            var x: BlockVec = undefined;
            salsaCore(x[0..], ctx, false);
            var out: [32]u8 = undefined;
            mem.writeInt(u32, out[0..4], x[0][0], .little);
            mem.writeInt(u32, out[4..8], x[1][1], .little);
            mem.writeInt(u32, out[8..12], x[2][2], .little);
            mem.writeInt(u32, out[12..16], x[3][3], .little);
            mem.writeInt(u32, out[16..20], x[1][2], .little);
            mem.writeInt(u32, out[20..24], x[1][3], .little);
            mem.writeInt(u32, out[24..28], x[2][0], .little);
            mem.writeInt(u32, out[28..32], x[2][1], .little);
            return out;
        }
    };
}

fn SalsaNonVecImpl(comptime rounds: comptime_int) type {
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
                constant_le[0], key[0],         key[1],         key[2],
                key[3],         constant_le[1], d[0],           d[1],
                d[2],           d[3],           constant_le[2], key[4],
                key[5],         key[6],         key[7],         constant_le[3],
            };
        }

        const QuarterRound = struct {
            a: usize,
            b: usize,
            c: usize,
            d: u6,
        };

        fn Rp(a: usize, b: usize, c: usize, d: u6) QuarterRound {
            return QuarterRound{
                .a = a,
                .b = b,
                .c = c,
                .d = d,
            };
        }

        fn salsaCore(x: *BlockVec, input: BlockVec, comptime feedback: bool) void {
            const arx_steps = comptime [_]QuarterRound{
                Rp(4, 0, 12, 7),   Rp(8, 4, 0, 9),    Rp(12, 8, 4, 13),   Rp(0, 12, 8, 18),
                Rp(9, 5, 1, 7),    Rp(13, 9, 5, 9),   Rp(1, 13, 9, 13),   Rp(5, 1, 13, 18),
                Rp(14, 10, 6, 7),  Rp(2, 14, 10, 9),  Rp(6, 2, 14, 13),   Rp(10, 6, 2, 18),
                Rp(3, 15, 11, 7),  Rp(7, 3, 15, 9),   Rp(11, 7, 3, 13),   Rp(15, 11, 7, 18),
                Rp(1, 0, 3, 7),    Rp(2, 1, 0, 9),    Rp(3, 2, 1, 13),    Rp(0, 3, 2, 18),
                Rp(6, 5, 4, 7),    Rp(7, 6, 5, 9),    Rp(4, 7, 6, 13),    Rp(5, 4, 7, 18),
                Rp(11, 10, 9, 7),  Rp(8, 11, 10, 9),  Rp(9, 8, 11, 13),   Rp(10, 9, 8, 18),
                Rp(12, 15, 14, 7), Rp(13, 12, 15, 9), Rp(14, 13, 12, 13), Rp(15, 14, 13, 18),
            };
            x.* = input;
            var j: usize = 0;
            while (j < rounds) : (j += 2) {
                inline for (arx_steps) |r| {
                    x[r.a] ^= math.rotl(u32, x[r.b] +% x[r.c], r.d);
                }
            }
            if (feedback) {
                j = 0;
                while (j < 16) : (j += 1) {
                    x[j] +%= input[j];
                }
            }
        }

        fn hashToBytes(out: *[64]u8, x: BlockVec) void {
            for (x, 0..) |w, i| {
                mem.writeInt(u32, out[i * 4 ..][0..4], w, .little);
            }
        }

        fn salsaXor(out: []u8, in: []const u8, key: [8]u32, d: [4]u32) void {
            var ctx = initContext(key, d);
            var x: BlockVec = undefined;
            var buf: [64]u8 = undefined;
            var i: usize = 0;
            while (i + 64 <= in.len) : (i += 64) {
                salsaCore(x[0..], ctx, true);
                hashToBytes(buf[0..], x);
                var xout = out[i..];
                const xin = in[i..];
                var j: usize = 0;
                while (j < 64) : (j += 1) {
                    xout[j] = xin[j];
                }
                j = 0;
                while (j < 64) : (j += 1) {
                    xout[j] ^= buf[j];
                }
                const ov = @addWithOverflow(ctx[8], 1);
                ctx[8] = ov[0];
                ctx[9] += ov[1];
            }
            if (i < in.len) {
                salsaCore(x[0..], ctx, true);
                hashToBytes(buf[0..], x);

                var xout = out[i..];
                const xin = in[i..];
                var j: usize = 0;
                while (j < in.len % 64) : (j += 1) {
                    xout[j] = xin[j] ^ buf[j];
                }
            }
        }

        fn hsalsa(input: [16]u8, key: [32]u8) [32]u8 {
            var c: [4]u32 = undefined;
            for (c, 0..) |_, i| {
                c[i] = mem.readInt(u32, input[4 * i ..][0..4], .little);
            }
            const ctx = initContext(keyToWords(key), c);
            var x: BlockVec = undefined;
            salsaCore(x[0..], ctx, false);
            var out: [32]u8 = undefined;
            mem.writeInt(u32, out[0..4], x[0], .little);
            mem.writeInt(u32, out[4..8], x[5], .little);
            mem.writeInt(u32, out[8..12], x[10], .little);
            mem.writeInt(u32, out[12..16], x[15], .little);
            mem.writeInt(u32, out[16..20], x[6], .little);
            mem.writeInt(u32, out[20..24], x[7], .little);
            mem.writeInt(u32, out[24..28], x[8], .little);
            mem.writeInt(u32, out[28..32], x[9], .little);
            return out;
        }
    };
}

const SalsaImpl = if (builtin.cpu.arch == .x86_64)
    SalsaVecImpl
else
    SalsaNonVecImpl;

fn keyToWords(key: [32]u8) [8]u32 {
    var k: [8]u32 = undefined;
    var i: usize = 0;
    while (i < 8) : (i += 1) {
        k[i] = mem.readInt(u32, key[i * 4 ..][0..4], .little);
    }
    return k;
}

fn extend(comptime rounds: comptime_int, key: [32]u8, nonce: [24]u8) struct { key: [32]u8, nonce: [8]u8 } {
    return .{
        .key = SalsaImpl(rounds).hsalsa(nonce[0..16].*, key),
        .nonce = nonce[16..24].*,
    };
}

/// The Salsa stream cipher.
pub fn Salsa(comptime rounds: comptime_int) type {
    return struct {
        /// Nonce length in bytes.
        pub const nonce_length = 8;
        /// Key length in bytes.
        pub const key_length = 32;

        /// Add the output of the Salsa stream cipher to `in` and stores the result into `out`.
        /// WARNING: This function doesn't provide authenticated encryption.
        /// Using the AEAD or one of the `box` versions is usually preferred.
        pub fn xor(out: []u8, in: []const u8, counter: u64, key: [key_length]u8, nonce: [nonce_length]u8) void {
            debug.assert(in.len == out.len);

            var d: [4]u32 = undefined;
            d[0] = mem.readInt(u32, nonce[0..4], .little);
            d[1] = mem.readInt(u32, nonce[4..8], .little);
            d[2] = @as(u32, @truncate(counter));
            d[3] = @as(u32, @truncate(counter >> 32));
            SalsaImpl(rounds).salsaXor(out, in, keyToWords(key), d);
        }
    };
}

/// The XSalsa stream cipher.
pub fn XSalsa(comptime rounds: comptime_int) type {
    return struct {
        /// Nonce length in bytes.
        pub const nonce_length = 24;
        /// Key length in bytes.
        pub const key_length = 32;

        /// Add the output of the XSalsa stream cipher to `in` and stores the result into `out`.
        /// WARNING: This function doesn't provide authenticated encryption.
        /// Using the AEAD or one of the `box` versions is usually preferred.
        pub fn xor(out: []u8, in: []const u8, counter: u64, key: [key_length]u8, nonce: [nonce_length]u8) void {
            const extended = extend(rounds, key, nonce);
            Salsa(rounds).xor(out, in, counter, extended.key, extended.nonce);
        }
    };
}

/// The XSalsa stream cipher, combined with the Poly1305 MAC
pub const XSalsa20Poly1305 = struct {
    /// Authentication tag length in bytes.
    pub const tag_length = Poly1305.mac_length;
    /// Nonce length in bytes.
    pub const nonce_length = XSalsa20.nonce_length;
    /// Key length in bytes.
    pub const key_length = XSalsa20.key_length;

    const rounds = 20;

    /// c: ciphertext: output buffer should be of size m.len
    /// tag: authentication tag: output MAC
    /// m: message
    /// ad: Associated Data
    /// npub: public nonce
    /// k: private key
    pub fn encrypt(c: []u8, tag: *[tag_length]u8, m: []const u8, ad: []const u8, npub: [nonce_length]u8, k: [key_length]u8) void {
        debug.assert(c.len == m.len);
    
```
