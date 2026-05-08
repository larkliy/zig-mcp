```
   const limbs_buffer = try rma.allocator.alloc(Limb, needed_limbs);
        defer rma.allocator.free(limbs_buffer);

        const needed = (a.len() - 1) / 2 + 1;
        try rma.ensureAliasAwareCapacity(needed, aliased);
        var m = rma.toMutable();
        m.sqrt(a.toConst(), limbs_buffer);
        rma.setMetadata(m.positive, m.len);
    }

    /// r = truncate(Int(signedness, bit_count), a)
    pub fn truncate(r: *Managed, a: *const Managed, signedness: Signedness, bit_count: usize) !void {
        const aliased = limbsAliasDistinct(r, a);
        const needed = calcTwosCompLimbCount(bit_count);
        try r.ensureAliasAwareCapacity(needed, aliased);
        var m = r.toMutable();
        m.truncate(a.toConst(), signedness, bit_count);
        r.setMetadata(m.positive, m.len);
    }

    /// r = saturate(Int(signedness, bit_count), a)
    pub fn saturate(r: *Managed, a: *const Managed, signedness: Signedness, bit_count: usize) !void {
        const aliased = limbsAliasDistinct(r, a);
        const needed = calcTwosCompLimbCount(bit_count);
        try r.ensureAliasAwareCapacity(needed, aliased);
        var m = r.toMutable();
        m.saturate(a.toConst(), signedness, bit_count);
        r.setMetadata(m.positive, m.len);
    }

    /// r = @popCount(a) with 2s-complement semantics.
    /// r and a may be aliases.
    pub fn popCount(r: *Managed, a: *const Managed, bit_count: usize) !void {
        const aliased = limbsAliasDistinct(r, a);
        const needed = calcTwosCompLimbCount(bit_count);
        try r.ensureAliasAwareCapacity(needed, aliased);
        var m = r.toMutable();
        m.popCount(a.toConst(), bit_count);
        r.setMetadata(m.positive, m.len);
    }
};

/// Different operators which can be used in accumulation style functions
/// (llmulacc, llmulaccKaratsuba, llmulaccLong, llmulLimb). In all these functions,
/// a computed value is accumulated with an existing result.
const AccOp = enum {
    /// The computed value is added to the result.
    add,

    /// The computed value is subtracted from the result.
    sub,
};

/// Knuth 4.3.1, Algorithm M.
///
/// r = r (op) a * b
/// r MUST NOT alias any of a or b.
///
/// The result is computed modulo `r.len`. When `r.len >= a.len + b.len`, no overflow occurs.
fn llmulacc(comptime op: AccOp, opt_allocator: ?Allocator, r: []Limb, a: []const Limb, b: []const Limb) void {
    assert(r.len >= a.len);
    assert(r.len >= b.len);
    assert(!slicesOverlap(r, a));
    assert(!slicesOverlap(r, b));

    // Order greatest first.
    var x = a;
    var y = b;
    if (a.len < b.len) {
        x = b;
        y = a;
    }

    k_mul: {
        if (y.len > 48) {
            if (opt_allocator) |allocator| {
                llmulaccKaratsuba(op, allocator, r, x, y) catch |err| switch (err) {
                    error.OutOfMemory => break :k_mul, // handled below
                };
                return;
            }
        }
    }

    llmulaccLong(op, r, x, y);
}

/// Knuth 4.3.1, Algorithm M.
///
/// r = r (op) a * b
/// r MUST NOT alias any of a or b.
///
/// The result is computed modulo `r.len`. When `r.len >= a.len + b.len`, no overflow occurs.
fn llmulaccKaratsuba(
    comptime op: AccOp,
    allocator: Allocator,
    r: []Limb,
    a: []const Limb,
    b: []const Limb,
) error{OutOfMemory}!void {
    assert(r.len >= a.len);
    assert(a.len >= b.len);
    assert(!slicesOverlap(r, a));
    assert(!slicesOverlap(r, b));

    // Classical karatsuba algorithm:
    // a = a1 * B + a0
    // b = b1 * B + b0
    // Where a0, b0 < B
    //
    // We then have:
    // ab = a * b
    //    = (a1 * B + a0) * (b1 * B + b0)
    //    = a1 * b1 * B * B + a1 * B * b0 + a0 * b1 * B + a0 * b0
    //    = a1 * b1 * B * B + (a1 * b0 + a0 * b1) * B + a0 * b0
    //
    // Note that:
    // a1 * b0 + a0 * b1
    //    = (a1 + a0)(b1 + b0) - a1 * b1 - a0 * b0
    //    = (a0 - a1)(b1 - b0) + a1 * b1 + a0 * b0
    //
    // This yields:
    // ab = p2 * B^2 + (p0 + p1 + p2) * B + p0
    //
    // Where:
    // p0 = a0 * b0
    // p1 = (a0 - a1)(b1 - b0)
    // p2 = a1 * b1
    //
    // Note, (a0 - a1) and (b1 - b0) produce values -B < x < B, and so we need to mind the sign here.
    // We also have:
    // 0 <= p0 <= 2B
    // -2B <= p1 <= 2B
    //
    // Note, when B is a multiple of the limb size, multiplies by B amount to shifts or
    // slices of a limbs array.
    //
    // This function computes the result of the multiplication modulo r.len. This means:
    // - p2 and p1 only need to be computed modulo r.len - B.
    // - In the case of p2, p2 * B^2 needs to be added modulo r.len - 2 * B.

    const split = b.len / 2; // B

    const limbs_after_split = r.len - split; // Limbs to compute for p1 and p2.
    const limbs_after_split2 = r.len - split * 2; // Limbs to add for p2 * B^2.

    // For a0 and b0 we need the full range.
    const a0 = a[0..llnormalize(a[0..split])];
    const b0 = b[0..llnormalize(b[0..split])];

    // For a1 and b1 we only need `limbs_after_split` limbs.
    const a1 = blk: {
        var a1 = a[split..];
        a1.len = @min(llnormalize(a1), limbs_after_split);
        break :blk a1;
    };

    const b1 = blk: {
        var b1 = b[split..];
        b1.len = @min(llnormalize(b1), limbs_after_split);
        break :blk b1;
    };

    // Note that the above slices relative to `split` work because we have a.len > b.len.

    // We need some temporary memory to store intermediate results.
    // Note, we can reduce the amount of temporaries we need by reordering the computation here:
    // ab = p2 * B^2 + (p0 + p1 + p2) * B + p0
    //    = p2 * B^2 + (p0 * B + p1 * B + p2 * B) + p0
    //    = (p2 * B^2 + p2 * B) + (p0 * B + p0) + p1 * B

    // Allocate at least enough memory to be able to multiply the upper two segments of a and b, assuming
    // no overflow.
    const tmp = try allocator.alloc(Limb, a.len - split + b.len - split);
    defer allocator.free(tmp);

    // Compute p2.
    // Note, we don't need to compute all of p2, just enough limbs to satisfy r.
    const p2_limbs = @min(limbs_after_split, a1.len + b1.len);

    @memset(tmp[0..p2_limbs], 0);
    llmulacc(.add, allocator, tmp[0..p2_limbs], a1[0..@min(a1.len, p2_limbs)], b1[0..@min(b1.len, p2_limbs)]);
    const p2 = tmp[0..llnormalize(tmp[0..p2_limbs])];

    // Add p2 * B to the result.
    llaccum(op, r[split..], p2);

    // Add p2 * B^2 to the result if required.
    if (limbs_after_split2 > 0) {
        llaccum(op, r[split * 2 ..], p2[0..@min(p2.len, limbs_after_split2)]);
    }

    // Compute p0.
    // Since a0.len, b0.len <= split and r.len >= split * 2, the full width of p0 needs to be computed.
    const p0_limbs = a0.len + b0.len;
    @memset(tmp[0..p0_limbs], 0);
    llmulacc(.add, allocator, tmp[0..p0_limbs], a0, b0);
    const p0 = tmp[0..llnormalize(tmp[0..p0_limbs])];

    // Add p0 to the result.
    llaccum(op, r, p0);

    // Add p0 * B to the result. In this case, we may not need all of it.
    llaccum(op, r[split..], p0[0..@min(limbs_after_split, p0.len)]);

    // Finally, compute and add p1.
    // From now on we only need `limbs_after_split` limbs for a0 and b0, since the result of the
    // following computation will be added * B.
    const a0x = a0[0..@min(a0.len, limbs_after_split)];
    const b0x = b0[0..@min(b0.len, limbs_after_split)];

    const j0_sign = llcmp(a0x, a1);
    const j1_sign = llcmp(b1, b0x);

    if (j0_sign * j1_sign == 0) {
        // p1 is zero, we don't need to do any computation at all.
        return;
    }

    @memset(tmp, 0);

    // p1 is nonzero, so compute the intermediary terms j0 = a0 - a1 and j1 = b1 - b0.
    // Note that in this case, we again need some storage for intermediary results
    // j0 and j1. Since we have tmp.len >= 2B, we can store both
    // intermediaries in the already allocated array.
    const j0 = tmp[0 .. a.len - split];
    const j1 = tmp[a.len - split ..];

    // Ensure that no subtraction overflows.
    if (j0_sign == 1) {
        // a0 > a1.
        _ = llsubcarry(j0, a0x, a1);
    } else {
        // a0 < a1.
        _ = llsubcarry(j0, a1, a0x);
    }

    if (j1_sign == 1) {
        // b1 > b0.
        _ = llsubcarry(j1, b1, b0x);
    } else {
        // b1 > b0.
        _ = llsubcarry(j1, b0x, b1);
    }

    if (j0_sign * j1_sign == 1) {
        // If j0 and j1 are both positive, we now have:
        // p1 = j0 * j1
        // If j0 and j1 are both negative, we now have:
        // p1 = -j0 * -j1 = j0 * j1
        // In this case we can add p1 to the result using llmulacc.
        llmulacc(op, allocator, r[split..], j0[0..llnormalize(j0)], j1[0..llnormalize(j1)]);
    } else {
        // In this case either j0 or j1 is negative, an we have:
        // p1 = -(j0 * j1)
        // Now we need to subtract instead of accumulate.
        const inverted_op = if (op == .add) .sub else .add;
        llmulacc(inverted_op, allocator, r[split..], j0[0..llnormalize(j0)], j1[0..llnormalize(j1)]);
    }
}

/// r = r (op) a.
/// The result is computed modulo `r.len`.
fn llaccum(comptime op: AccOp, r: []Limb, a: []const Limb) void {
    assert(!slicesOverlap(r, a) or @intFromPtr(r.ptr) <= @intFromPtr(a.ptr));
    if (op == .sub) {
        _ = llsubcarry(r, r, a);
        return;
    }

    assert(r.len != 0 and a.len != 0);
    assert(r.len >= a.len);

    var i: usize = 0;
    var carry: Limb = 0;

    while (i < a.len) : (i += 1) {
        const ov1 = @addWithOverflow(r[i], a[i]);
        r[i] = ov1[0];
        const ov2 = @addWithOverflow(r[i], carry);
        r[i] = ov2[0];
        carry = @as(Limb, ov1[1]) + ov2[1];
    }

    while ((carry != 0) and i < r.len) : (i += 1) {
        const ov = @addWithOverflow(r[i], carry);
        r[i] = ov[0];
        carry = ov[1];
    }
}

/// Returns -1, 0, 1 if |a| < |b|, |a| == |b| or |a| > |b| respectively for limbs.
pub fn llcmp(a: []const Limb, b: []const Limb) i8 {
    const a_len = llnormalize(a);
    const b_len = llnormalize(b);
    if (a_len < b_len) {
        return -1;
    }
    if (a_len > b_len) {
        return 1;
    }

    var i: usize = a_len - 1;
    while (i != 0) : (i -= 1) {
        if (a[i] != b[i]) {
            break;
        }
    }

    if (a[i] < b[i]) {
        return -1;
    } else if (a[i] > b[i]) {
        return 1;
    } else {
        return 0;
    }
}

/// r = r (op) y * xi
/// The result is computed modulo `r.len`. When `r.len >= a.len + b.len`, no overflow occurs.
fn llmulaccLong(comptime op: AccOp, r: []Limb, a: []const Limb, b: []const Limb) void {
    assert(r.len >= a.len);
    assert(a.len >= b.len);

    var i: usize = 0;
    while (i < b.len) : (i += 1) {
        _ = llmulLimb(op, r[i..], a, b[i]);
    }
}

/// r = r (op) y * xi
/// The result is computed modulo `r.len`.
/// Returns whether the operation overflowed.
fn llmulLimb(comptime op: AccOp, acc: []Limb, y: []const Limb, xi: Limb) bool {
    assert(!slicesOverlap(acc, y) or @intFromPtr(acc.ptr) <= @intFromPtr(y.ptr));

    if (xi == 0) {
        return false;
    }

    const split = @min(y.len, acc.len);
    var a_lo = acc[0..split];
    var a_hi = acc[split..];

    switch (op) {
        .add => {
            var carry: Limb = 0;
            var j: usize = 0;
            while (j < a_lo.len) : (j += 1) {
                a_lo[j] = addMulLimbWithCarry(a_lo[j], y[j], xi, &carry);
            }

            j = 0;
            while ((carry != 0) and (j < a_hi.len)) : (j += 1) {
                const ov = @addWithOverflow(a_hi[j], carry);
                a_hi[j] = ov[0];
                carry = ov[1];
            }

            return carry != 0;
        },
        .sub => {
            var borrow: Limb = 0;
            var j: usize = 0;
            while (j < a_lo.len) : (j += 1) {
                a_lo[j] = subMulLimbWithBorrow(a_lo[j], y[j], xi, &borrow);
            }

            j = 0;
            while ((borrow != 0) and (j < a_hi.len)) : (j += 1) {
                const ov = @subWithOverflow(a_hi[j], borrow);
                a_hi[j] = ov[0];
                borrow = ov[1];
            }

            return borrow != 0;
        },
    }
}

/// returns the min length the limb could be.
fn llnormalize(a: []const Limb) usize {
    var j = a.len;
    while (j > 0) : (j -= 1) {
        if (a[j - 1] != 0) {
            break;
        }
    }

    // Handle zero
    return if (j != 0) j else 1;
}

/// Knuth 4.3.1, Algorithm S.
fn llsubcarry(r: []Limb, a: []const Limb, b: []const Limb) Limb {
    assert(a.len != 0 and b.len != 0);
    assert(a.len >= b.len);
    assert(r.len >= a.len);
    assert(!slicesOverlap(r, a) or @intFromPtr(r.ptr) <= @intFromPtr(a.ptr));
    assert(!slicesOverlap(r, b) or @intFromPtr(r.ptr) <= @intFromPtr(b.ptr));

    var i: usize = 0;
    var borrow: Limb = 0;

    while (i < b.len) : (i += 1) {
        const ov1 = @subWithOverflow(a[i], b[i]);
        r[i] = ov1[0];
        const ov2 = @subWithOverflow(r[i], borrow);
        r[i] = ov2[0];
        borrow = @as(Limb, ov1[1]) + ov2[1];
    }

    while (i < a.len) : (i += 1) {
        const ov = @subWithOverflow(a[i], borrow);
        r[i] = ov[0];
        borrow = ov[1];
    }

    return borrow;
}

fn llsub(r: []Limb, a: []const Limb, b: []const Limb) void {
    assert(a.len > b.len or (a.len == b.len and a[a.len - 1] >= b[b.len - 1]));
    assert(llsubcarry(r, a, b) == 0);
}

/// Knuth 4.3.1, Algorithm A.
fn lladdcarry(r: []Limb, a: []const Limb, b: []const Limb) Limb {
    assert(a.len != 0 and b.len != 0);
    assert(a.len >= b.len);
    assert(r.len >= a.len);
    assert(!slicesOverlap(r, a) or @intFromPtr(r.ptr) <= @intFromPtr(a.ptr));
    assert(!slicesOverlap(r, b) or @intFromPtr(r.ptr) <= @intFromPtr(b.ptr));

    var i: usize = 0;
    var carry: Limb = 0;

    while (i < b.len) : (i += 1) {
        const ov1 = @addWithOverflow(a[i], b[i]);
        r[i] = ov1[0];
        const ov2 = @addWithOverflow(r[i], carry);
        r[i] = ov2[0];
        carry = @as(Limb, ov1[1]) + ov2[1];
    }

    while (i < a.len) : (i += 1) {
        const ov = @addWithOverflow(a[i], carry);
        r[i] = ov[0];
        carry = ov[1];
    }

    return carry;
}

fn lladd(r: []Limb, a: []const Limb, b: []const Limb) void {
    assert(r.len >= a.len + 1);
    r[a.len] = lladdcarry(r, a, b);
}

/// Knuth 4.3.1, Exercise 16.
fn lldiv1(quo: []Limb, rem: *Limb, a: []const Limb, b: Limb) void {
    assert(a.len > 1 or a[0] >= b);
    assert(quo.len >= a.len);

    rem.* = 0;
    for (a, 0..) |_, ri| {
        const i = a.len - ri - 1;
        const pdiv = ((@as(DoubleLimb, rem.*) << limb_bits) | a[i]);

        if (pdiv == 0) {
            quo[i] = 0;
            rem.* = 0;
        } else if (pdiv < b) {
            quo[i] = 0;
            rem.* = @as(Limb, @truncate(pdiv));
        } else if (pdiv == b) {
            quo[i] = 1;
            rem.* = 0;
        } else {
            quo[i] = @as(Limb, @truncate(@divTrunc(pdiv, b)));
            rem.* = @as(Limb, @truncate(pdiv - (quo[i] *% b)));
        }
    }
}

fn lldiv0p5(quo: []Limb, rem: *Limb, a: []const Limb, b: HalfLimb) void {
    assert(a.len > 1 or a[0] >= b);
    assert(quo.len >= a.len);

    rem.* = 0;
    for (a, 0..) |_, ri| {
        const i = a.len - ri - 1;
        const ai_high = a[i] >> half_limb_bits;
        const ai_low = a[i] & ((1 << half_limb_bits) - 1);

        // Split the division into two divisions acting on half a limb each. Carry remainder.
        const ai_high_with_carry = (rem.* << half_limb_bits) | ai_high;
        const ai_high_quo = ai_high_with_carry / b;
        rem.* = ai_high_with_carry % b;

        const ai_low_with_carry = (rem.* << half_limb_bits) | ai_low;
        const ai_low_quo = ai_low_with_carry / b;
        rem.* = ai_low_with_carry % b;

        quo[i] = (ai_high_quo << half_limb_bits) | ai_low_quo;
    }
}

/// Performs r = a << shift and returns the amount of limbs affected
///
/// if a and r overlaps, then r.ptr >= a.ptr is asserted
/// r must have the capacity to store a << shift
fn llshl(r: []Limb, a: []const Limb, shift: usize) usize {
    std.debug.assert(a.len >= 1);
    if (slicesOverlap(a, r))
        std.debug.assert(@intFromPtr(r.ptr) >= @intFromPtr(a.ptr));

    if (shift == 0) {
        if (a.ptr != r.ptr) @memmove(r[0..a.len], a);
        return a.len;
    }
    if (shift >= limb_bits) {
        const limb_shift = shift / limb_bits;

        const affected = llshl(r[limb_shift..], a, shift % limb_bits);
        @memset(r[0..limb_shift], 0);

        return limb_shift + affected;
    }

    // shift is guaranteed to be < limb_bits
    const bit_shift: Log2Limb = @truncate(shift);
    const opposite_bit_shift: Log2Limb = @truncate(limb_bits - bit_shift);

    // We only need the extra limb if the shift of the last element overflows.
    // This is useful for the implementation of `shiftLeftSat`.
    const overflows = a[a.len - 1] >> opposite_bit_shift != 0;
    if (overflows) {
        std.debug.assert(r.len >= a.len + 1);
    } else {
        std.debug.assert(r.len >= a.len);
    }

    var i: usize = a.len;
    if (overflows) {
        // r is asserted to be large enough above
        r[a.len] = a[a.len - 1] >> opposite_bit_shift;
    }
    while (i > 1) {
        i -= 1;
        r[i] = (a[i - 1] >> opposite_bit_shift) | (a[i] << bit_shift);
    }
    r[0] = a[0] << bit_shift;

    return a.len + @intFromBool(overflows);
}

/// Performs r = a >> shift and returns the amount of limbs affected
///
/// if a and r overlaps, then r.ptr <= a.ptr is asserted
/// r must have the capacity to store a >> shift
///
/// See tests below for examples of behaviour
fn llshr(r: []Limb, a: []const Limb, shift: usize) usize {
    if (slicesOverlap(a, r))
        std.debug.assert(@intFromPtr(r.ptr) <= @intFromPtr(a.ptr));

    if (a.len == 0) return 0;

    if (shift == 0) {
        std.debug.assert(r.len >= a.len);

        if (a.ptr != r.ptr) @memmove(r[0..a.len], a);
        return a.len;
    }
    if (shift >= limb_bits) {
        if (shift / limb_bits >= a.len) {
            r[0] = 0;
            return 1;
        }
        return llshr(r, a[shift / limb_bits ..], shift % limb_bits);
    }

    // shift is guaranteed to be < limb_bits
    const bit_shift: Log2Limb = @truncate(shift);
    const opposite_bit_shift: Log2Limb = @truncate(limb_bits - bit_shift);

    // special case, where there is a risk to set r to 0
    if (a.len == 1) {
        r[0] = a[0] >> bit_shift;
        return 1;
    }
    if (a.len == 0) {
        r[0] = 0;
        return 1;
    }

    // if the most significant limb becomes 0 after the shift
    const shrink = a[a.len - 1] >> bit_shift == 0;
    std.debug.assert(r.len >= a.len - @intFromBool(shrink));

    var i: usize = 0;
    while (i < a.len - 1) : (i += 1) {
        r[i] = (a[i] >> bit_shift) | (a[i + 1] << opposite_bit_shift);
    }

    if (!shrink)
        r[i] = a[i] >> bit_shift;

    return a.len - @intFromBool(shrink);
}

// r = ~r
fn llnot(r: []Limb) void {
    for (r) |*elem| {
        elem.* = ~elem.*;
    }
}

// r = a | b with 2s complement semantics.
// r may alias.
// a and b must not be 0.
// Returns `true` when the result is positive.
// When b is positive, r requires at least `a.len` limbs of storage.
// When b is negative, r requires at least `b.len` limbs of storage.
fn llsignedor(r: []Limb, a: []const Limb, a_positive: bool, b: []const Limb, b_positive: bool) bool {
    assert(r.len >= a.len);
    assert(a.len >= b.len);

    if (a_positive and b_positive) {
        // Trivial case, result is positive.
        var i: usize = 0;
        while (i < b.len) : (i += 1) {
            r[i] = a[i] | b[i];
        }
        while (i < a.len) : (i += 1) {
            r[i] = a[i];
        }

        return true;
    } else if (!a_positive and b_positive) {
        // Result is negative.
        // r = (--a) | b
        //   = ~(-a - 1) | b
        //   = ~(-a - 1) | ~~b
        //   = ~((-a - 1) & ~b)
        //   = -(((-a - 1) & ~b) + 1)

        var i: usize = 0;
        var a_borrow: u1 = 1;
        var r_carry: u1 = 1;

        while (i < b.len) : (i += 1) {
            const ov1 = @subWithOverflow(a[i], a_borrow);
            a_borrow = ov1[1];
            const ov2 = @addWithOverflow(ov1[0] & ~b[i], r_carry);
            r[i] = ov2[0];
            r_carry = ov2[1];
        }

        // In order for r_carry to be nonzero at this point, ~b[i] would need to be
        // all ones, which would require b[i] to be zero. This cannot be when
        // b is normalized, so there cannot be a carry here.
        // Also, x & ~b can only clear bits, so (x & ~b) <= x, meaning (-a - 1) + 1 never overflows.
        assert(r_carry == 0);

        // With b = 0, we get (-a - 1) & ~0 = -a - 1.
        // Note, if a_borrow is zero we do not need to compute anything for
        // the higher limbs so we can early return here.
        while (i < a.len and a_borrow == 1) : (i += 1) {
            const ov = @subWithOverflow(a[i], a_borrow);
            r[i] = ov[0];
            a_borrow = ov[1];
        }

        assert(a_borrow == 0); // a was 0.

        return false;
    } else if (a_positive and !b_positive) {
        // Result is negative.
        // r = a | (--b)
        //   = a | ~(-b - 1)
        //   = ~~a | ~(-b - 1)
        //   = ~(~a & (-b - 1))
        //   = -((~a & (-b - 1)) + 1)

        var i: usize = 0;
        var b_borrow: u1 = 1;
        var r_carry: u1 = 1;

        while (i < b.len) : (i += 1) {
            const ov1 = @subWithOverflow(b[i], b_borrow);
            b_borrow = ov1[1];
            const ov2 = @addWithOverflow(~a[i] & ov1[0], r_carry);
            r[i] = ov2[0];
            r_carry = ov2[1];
        }

        // b is at least 1, so this should never underflow.
        assert(b_borrow == 0); // b was 0

        // x & ~a can only clear bits, so (x & ~a) <= x, meaning (-b - 1) + 1 never overflows.
        assert(r_carry == 0);

        // With b = 0 and b_borrow = 0, we get ~a & (0 - 0) = ~a & 0 = 0.
        // Omit setting the upper bytes, just deal with those when calling llsignedor.

        return false;
    } else {
        // Result is negative.
        // r = (--a) | (--b)
        //   = ~(-a - 1) | ~(-b - 1)
        //   = ~((-a - 1) & (-b - 1))
        //   = -(~(~((-a - 1) & (-b - 1))) + 1)
        //   = -((-a - 1) & (-b - 1) + 1)

        var i: usize = 0;
        var a_borrow: u1 = 1;
        var b_borrow: u1 = 1;
        var r_carry: u1 = 1;

        while (i < b.len) : (i += 1) {
            const ov1 = @subWithOverflow(a[i], a_borrow);
            a_borrow = ov1[1];
            const ov2 = @subWithOverflow(b[i], b_borrow);
            b_borrow = ov2[1];
            const ov3 = @addWithOverflow(ov1[0] & ov2[0], r_carry);
            r[i] = ov3[0];
            r_carry = ov3[1];
        }

        // b is at least 1, so this should never underflow.
        assert(b_borrow == 0); // b was 0

        // Can never overflow because in order for b_limb to be maxInt(Limb),
        // b_borrow would need to equal 1.

        // x & y can only clear bits, meaning x & y <= x and x & y <= y. This implies that
        // for x = a - 1 and y = b - 1, the +1 term would never cause an overflow.
        assert(r_carry == 0);

        // With b = 0 and b_borrow = 0 we get (-a - 1) & (0 - 0) = (-a - 1) & 0 = 0.
        // Omit setting the upper bytes, just deal with those when calling llsignedor.
        return false;
    }
}

// r = a & b with 2s complement semantics.
// r may alias.
// a and b must not be 0.
// Returns `true` when the result is positive.
// We assume `a.len >= b.len` here, so:
// 1. when b is positive, r requires at least `b.len` limbs of storage,
// 2. when b is negative but a is positive, r requires at least `a.len` limbs of storage,
// 3. when both a and b are negative, r requires at least `a.len + 1` limbs of storage.
fn llsignedand(r: []Limb, a: []const Limb, a_positive: bool, b: []const Limb, b_positive: bool) bool {
    assert(a.len != 0 and b.len != 0);
    assert(a.len >= b.len);
    assert(r.len >= if (b_positive) b.len else if (a_positive) a.len else a.len + 1);

    if (a_positive and b_positive) {
        // Trivial case, result is positive.
        var i: usize = 0;
        while (i < b.len) : (i += 1) {
            r[i] = a[i] & b[i];
        }

        // With b = 0 we have a & 0 = 0, so the upper bytes are zero.
        // Omit setting them here and simply discard them whenever
        // llsignedand is called.

        return true;
    } else if (!a_positive and b_positive) {
        // Result is positive.
        // r = (--a) & b
        //   = ~(-a - 1) & b

        var i: usize = 0;
        var a_borrow: u1 = 1;

        while (i < b.len) : (i += 1) {
            const ov = @subWithOverflow(a[i], a_borrow);
            a_borrow = ov[1];
            r[i] = ~ov[0] & b[i];
        }

        // With b = 0 we have ~(a - 1) & 0 = 0, so the upper bytes are zero.
        // Omit setting them here and simply discard them whenever
        // llsignedand is called.

        return true;
    } else if (a_positive and !b_positive) {
        // Result is positive.
        // r = a & (--b)
        //   = a & ~(-b - 1)

        var i: usize = 0;
        var b_borrow: u1 = 1;

        while (i < b.len) : (i += 1) {
            const ov = @subWithOverflow(b[i], b_borrow);
            b_borrow = ov[1];
            r[i] = a[i] & ~ov[0];
        }

        assert(b_borrow == 0); // b was 0

        // With b = 0 and b_borrow = 0 we have a & ~(0 - 0) = a & ~0 = a, so
        // the upper bytes are the same as those of a.

        while (i < a.len) : (i += 1) {
            r[i] = a[i];
        }

        return true;
    } else {
        // Result is negative.
        // r = (--a) & (--b)
        //   = ~(-a - 1) & ~(-b - 1)
        //   = ~((-a - 1) | (-b - 1))
        //   = -(((-a - 1) | (-b - 1)) + 1)

        var i: usize = 0;
        var a_borrow: u1 = 1;
        var b_borrow: u1 = 1;
        var r_carry: u1 = 1;

        while (i < b.len) : (i += 1) {
            const ov1 = @subWithOverflow(a[i], a_borrow);
            a_borrow = ov1[1];
            const ov2 = @subWithOverflow(b[i], b_borrow);
            b_borrow = ov2[1];
            const ov3 = @addWithOverflow(ov1[0] | ov2[0], r_carry);
            r[i] = ov3[0];
            r_carry = ov3[1];
        }

        // b is at least 1, so this should never underflow.
        assert(b_borrow == 0); // b was 0

        // With b = 0 and b_borrow = 0 we get (-a - 1) | (0 - 0) = (-a - 1) | 0 = -a - 1.
        while (i < a.len) : (i += 1) {
            const ov1 = @subWithOverflow(a[i], a_borrow);
            a_borrow = ov1[1];
            const ov2 = @addWithOverflow(ov1[0], r_carry);
            r[i] = ov2[0];
            r_carry = ov2[1];
        }

        assert(a_borrow == 0); // a was 0.

        // The final addition can overflow here, so we need to keep that in mind.
        r[i] = r_carry;

        return false;
    }
}

// r = a ^ b with 2s complement semantics.
// r may alias.
// a and b must not be -0.
// Returns `true` when the result is positive.
// If the sign of a and b is equal, then r requires at least `@max(a.len, b.len)` limbs are required.
// Otherwise, r requires at least `@max(a.len, b.len) + 1` limbs.
fn llsignedxor(r: []Limb, a: []const Limb, a_positive: bool, b: []const Limb, b_positive: bool) bool {
    assert(a.len != 0 and b.len != 0);
    assert(r.len >= a.len);
    assert(a.len >= b.len);

    // If a and b are positive, the result is positive and r = a ^ b.
    // If a negative, b positive, result is negative and we have
    // r = --(--a ^ b)
    //   = --(~(-a - 1) ^ b)
    //   = -(~(~(-a - 1) ^ b) + 1)
    //   = -(((-a - 1) ^ b) + 1)
    // Same if a is positive and b is negative, sides switched.
    // If both a and b are negative, the result is positive and we have
    // r = (--a) ^ (--b)
    //   = ~(-a - 1) ^ ~(-b - 1)
    //   = (-a - 1) ^ (-b - 1)
    // These operations can be made more generic as follows:
    // - If a is negative, subtract 1 from |a| before the xor.
    // - If b is negative, subtract 1 from |b| before the xor.
    // - if the result is supposed to be negative, add 1.

    var i: usize = 0;
    var a_borrow = @intFromBool(!a_positive);
    var b_borrow = @intFromBool(!b_positive);
    var r_carry = @intFromBool(a_positive != b_positive);

    while (i < b.len) : (i += 1) {
        const ov1 = @subWithOverflow(a[i], a_borrow);
        a_borrow = ov1[1];
        const ov2 = @subWithOverflow(b[i], b_borrow);
        b_borrow = ov2[1];
        const ov3 = @addWithOverflow(ov1[0] ^ ov2[0], r_carry);
        r[i] = ov3[0];
        r_carry = ov3[1];
    }

    while (i < a.len) : (i += 1) {
        const ov1 = @subWithOverflow(a[i], a_borrow);
        a_borrow = ov1[1];
        const ov2 = @addWithOverflow(ov1[0], r_carry);
        r[i] = ov2[0];
        r_carry = ov2[1];
    }

    // If both inputs don't share the same sign, an extra limb is required.
    if (a_positive != b_positive) {
        r[i] = r_carry;
    } else {
        assert(r_carry == 0);
    }

    assert(a_borrow == 0);
    assert(b_borrow == 0);

    return a_positive == b_positive;
}

/// r MUST NOT alias x.
fn llsquareBasecase(r: []Limb, x: []const Limb) void {
    const x_norm = x;
    assert(r.len >= 2 * x_norm.len + 1);
    assert(!slicesOverlap(r, x));

    // Compute the square of a N-limb bigint with only (N^2 + N)/2
    // multiplications by exploiting the symmetry of the coefficients around the
    // diagonal:
    //
    //           a   b   c *
    //           a   b   c =
    // -------------------
    //          ca  cb  cc +
    //      ba  bb  bc     +
    //  aa  ab  ac
    //
    // Note that:
    //  - Each mixed-product term appears twice for each column,
    //  - Squares are always in the 2k (0 <= k < N) column

    for (x_norm, 0..) |v, i| {
        // Accumulate all the x[i]*x[j] (with x!=j) products
        const overflow = llmulLimb(.add, r[2 * i + 1 ..], x_norm[i + 1 ..], v);
        assert(!overflow);
    }

    // Each product appears twice, multiply by 2
    _ = llshl(r, r[0 .. 2 * x_norm.len], 1);

    for (x_norm, 0..) |v, i| {
        // Compute and add the squares
        const overflow = llmulLimb(.add, r[2 * i ..], x[i..][0..1], v);
        assert(!overflow);
    }
}

/// Knuth 4.6.3
fn llpow(r: []Limb, a: []const Limb, b: u32, tmp_limbs: []Limb) void {
    var tmp1: []Limb = undefined;
    var tmp2: []Limb = undefined;

    // Multiplication requires no aliasing between the operand and the result
    // variable, use the output limbs and another temporary set to overcome this
    // limitation.
    // The initial assignment makes the result end in `r` so an extra memory
    // copy is saved, each 1 flips the index twice so it's only the zeros that
    // matter.
    const b_leading_zeros = @clz(b);
    const exp_zeros = @popCount(~b) - b_leading_zeros;
    if (exp_zeros & 1 != 0) {
        tmp1 = tmp_limbs;
        tmp2 = r;
    } else {
        tmp1 = r;
        tmp2 = tmp_limbs;
    }

    @memcpy(tmp1[0..a.len], a);
    @memset(tmp1[a.len..], 0);

    // Scan the exponent as a binary number, from left to right, dropping the
    // most significant bit set.
    // Square the result if the current bit is zero, square and multiply by a if
    // it is one.
    const exp_bits = 32 - 1 - b_leading_zeros;
    var exp = b << @as(u5, @intCast(1 + b_leading_zeros));

    var i: usize = 0;
    while (i < exp_bits) : (i += 1) {
        // Square
        @memset(tmp2, 0);
        llsquareBasecase(tmp2, tmp1[0..llnormalize(tmp1)]);
        mem.swap([]Limb, &tmp1, &tmp2);
        // Multiply by a
        const ov = @shlWithOverflow(exp, 1);
        exp = ov[0];
        if (ov[1] != 0) {
            @memset(tmp2, 0);
            llmulacc(.add, null, tmp2, tmp1[0..llnormalize(tmp1)], a);
            mem.swap([]Limb, &tmp1, &tmp2);
        }
    }
}

// Storage must live for the lifetime of the returned value
fn fixedIntFromSignedDoubleLimb(A: SignedDoubleLimb, storage: []Limb) Mutable {
    assert(storage.len >= 2);

    const A_is_positive = A >= 0;
    const Au = @as(DoubleLimb, @intCast(if (A < 0) -A else A));
    storage[0] = @as(Limb, @truncate(Au));
    storage[1] = @as(Limb, @truncate(Au >> limb_bits));
    return .{
        .limbs = storage[0..2],
        .positive = A_is_positive,
        .len = 2,
    };
}

fn slicesOverlap(a: []const Limb, b: []const Limb) bool {
    // there is no overlap if a.ptr + a.len <= b.ptr or b.ptr + b.len <= a.ptr
    return @intFromPtr(a.ptr + a.len) > @intFromPtr(b.ptr) and @intFromPtr(b.ptr + b.len) > @intFromPtr(a.ptr);
}

test {
    _ = @import("int_test.zig");
}

const testing_allocator = std.testing.allocator;
test "llshl shift by whole number of limb" {
    const padding = maxInt(Limb);

    var r: [10]Limb = @splat(padding);

    const A: Limb = @truncate(0xCCCCCCCCCCCCCCCCCCCCCCC);
    const B: Limb = @truncate(0x22222222222222222222222);

    const data = [2]Limb{ A, B };
    for (0..9) |i| {
        @memset(&r, padding);
        const len = llshl(&r, &data, i * @bitSizeOf(Limb));

        try std.testing.expectEqual(i + 2, len);
        try std.testing.expectEqualSlices(Limb, &data, r[i .. i + 2]);
        for (r[0..i]) |x|
            try std.testing.expectEqual(0, x);
        for (r[i + 2 ..]) |x|
            try std.testing.expectEqual(padding, x);
    }
}

test llshl {
    if (limb_bits != 64) return error.SkipZigTest;

    // 1 << 63
    const left_one = 0x8000000000000000;
    const maxint: Limb = 0xFFFFFFFFFFFFFFFF;

    // zig fmt: off
    try testOneShiftCase(.llshl, .{0,  &.{0},                               &.{0}});
    try testOneShiftCase(.llshl, .{0,  &.{1},                               &.{1}});
    try testOneShiftCase(.llshl, .{0,  &.{125484842448},                    &.{125484842448}});
    try testOneShiftCase(.llshl, .{0,  &.{0xdeadbeef},                      &.{0xdeadbeef}});
    try testOneShiftCase(.llshl, .{0,  &.{maxint},                          &.{maxint}});
    try testOneShiftCase(.llshl, .{0,  &.{left_one},                        &.{left_one}});
    try testOneShiftCase(.llshl, .{0,  &.{0, 1},                            &.{0, 1}});
    try testOneShiftCase(.llshl, .{0,  &.{1, 2},                            &.{1, 2}});
    try testOneShiftCase(.llshl, .{0,  &.{left_one, 1},                     &.{left_one, 1}});
    try testOneShiftCase(.llshl, .{1,  &.{0},                               &.{0}});
    try testOneShiftCase(.llshl, .{1,  &.{2},                               &.{1}});
    try testOneShiftCase(.llshl, .{1,  &.{250969684896},                    &.{125484842448}});
    try testOneShiftCase(.llshl, .{1,  &.{0x1bd5b7dde},                     &.{0xdeadbeef}});
    try testOneShiftCase(.llshl, .{1,  &.{0xfffffffffffffffe, 1},           &.{maxint}});
    try testOneShiftCase(.llshl, .{1,  &.{0, 1},                            &.{left_one}});
    try testOneShiftCase(.llshl, .{1,  &.{0, 2},                            &.{0, 1}});
    try testOneShiftCase(.llshl, .{1,  &.{2, 4},                            &.{1, 2}});
    try testOneShiftCase(.llshl, .{1,  &.{0, 3},                            &.{left_one, 1}});
    try testOneShiftCase(.llshl, .{5,  &.{32},                              &.{1}});
    try testOneShiftCase(.llshl, .{5,  &.{4015514958336},                   &.{125484842448}});
    try testOneShiftCase(.llshl, .{5,  &.{0x1bd5b7dde0},                    &.{0xdeadbeef}});
    try testOneShiftCase(.llshl, .{5,  &.{0xffffffffffffffe0, 0x1f},        &.{maxint}});
    try testOneShiftCase(.llshl, .{5,  &.{0, 16},                           &.{left_one}});
    try testOneShiftCase(.llshl, .{5,  &.{0, 32},                           &.{0, 1}});
    try testOneShiftCase(.llshl, .{5,  &.{32, 64},                          &.{1, 2}});
    try testOneShiftCase(.llshl, .{5,  &.{0, 48},                           &.{left_one, 1}});
    try testOneShiftCase(.llshl, .{64, &.{0, 1},                            &.{1}});
    try testOneShiftCase(.llshl, .{64, &.{0, 125484842448},                 &.{125484842448}});
    try testOneShiftCase(.llshl, .{64, &.{0, 0xdeadbeef},                   &.{0xdeadbeef}});
    try testOneShiftCase(.llshl, .{64, &.{0, maxint},                       &.{maxint}});
    try testOneShiftCase(.llshl, .{64, &.{0, left_one},                     &.{left_one}});
    try testOneShiftCase(.llshl, .{64, &.{0, 0, 1},                         &.{0, 1}});
    try testOneShiftCase(.llshl, .{64, &.{0, 1, 2},                         &.{1, 2}});
    try testOneShiftCase(.llshl, .{64, &.{0, left_one, 1},                  &.{left_one, 1}});
    try testOneShiftCase(.llshl, .{35, &.{0x800000000},                     &.{1}});
    try testOneShiftCase(.llshl, .{35, &.{13534986488655118336, 233},       &.{125484842448}});
    try testOneShiftCase(.llshl, .{35, &.{0xf56df77800000000, 6},           &.{0xdeadbeef}});
    try testOneShiftCase(.llshl, .{35, &.{0xfffffff800000000, 0x7ffffffff}, &.{maxint}});
    try testOneShiftCase(.llshl, .{35, &.{0, 17179869184},                  &.{left_one}});
    try testOneShiftCase(.llshl, .{35, &.{0, 0x800000000},                  &.{0, 1}});
    try testOneShiftCase(.llshl, .{35, &.{0x800000000, 0x1000000000},       &.{1, 2}});
    try testOneShiftCase(.llshl, .{35, &.{0, 0xc00000000},                  &.{left_one, 1}});
    try testOneShiftCase(.llshl, .{70, &.{0, 64},                           &.{1}});
    try testOneShiftCase(.llshl, .{70, &.{0, 8031029916672},                &.{125484842448}});
    try testOneShiftCase(.llshl, .{70, &.{0, 0x37ab6fbbc0},                 &.{0xdeadbeef}});
    try testOneShiftCase(.llshl, .{70, &.{0, 0xffffffffffffffc0, 63},       &.{maxint}});
    try testOneShiftCase(.llshl, .{70, &.{0, 0, 32},                        &.{left_one}});
    try testOneShiftCase(.llshl, .{70, &.{0, 0, 64},                        &.{0, 1}});
    try testOneShiftCase(.llshl, .{70, &.{0, 64, 128},                      &.{1, 2}});
    try testOneShiftCase(.llshl, .{70, &.{0, 0, 0x60},                      &.{left_one, 1}});
    // zig fmt: on
}

test "llshl shift 0" {
    const n = @bitSizeOf(Limb);
    if (n <= 20) return error.SkipZigTest;

    // zig fmt: off
    try testOneShiftCase(.llshl, .{0,   &.{0},    &.{0}});
    try testOneShiftCase(.llshl, .{1,   &.{0},    &.{0}});
    try testOneShiftCase(.llshl, .{5,   &.{0},    &.{0}});
    try testOneShiftCase(.llshl, .{13,  &.{0},    &.{0}});
    try testOneShiftCase(.llshl, .{20,  &.{0},    &.{0}});
    try testOneShiftCase(.llshl, .{0,   &.{0, 0}, &.{0, 0}});
    try testOneShiftCase(.llshl, .{2,   &.{0, 0}, &.{0, 0}});
    try testOneShiftCase(.llshl, .{7,   &.{0, 0}, &.{0, 0}});
    try testOneShiftCase(.llshl, .{11,  &.{0, 0}, &.{0, 0}});
    try testOneShiftCase(.llshl, .{19,  &.{0, 0}, &.{0, 0}});

    try testOneShiftCase(.llshl, .{0,   &.{0},                &.{0}});
    try testOneShiftCase(.llshl, .{n,   &.{0, 0},             &.{0}});
    try testOneShiftCase(.llshl, .{2*n, &.{0, 0, 0},          &.{0}});
    try testOneShiftCase(.llshl, .{3*n, &.{0, 0, 0, 0},       &.{0}});
    try testOneShiftCase(.llshl, .{4*n, &.{0, 0, 0, 0, 0},    &.{0}});
    try testOneShiftCase(.llshl, .{0,   &.{0, 0},             &.{0, 0}});
    try testOneShiftCase(.llshl, .{n,   &.{0, 0, 0},          &.{0, 0}});
    try testOneShiftCase(.llshl, .{2*n, &.{0, 0, 0, 0},       &.{0, 0}});
    try testOneShiftCase(.llshl, .{3*n, &.{0, 0, 0, 0, 0},    &.{0, 0}});
    try testOneShiftCase(.llshl, .{4*n, &.{0, 0, 0, 0, 0, 0}, &.{0, 0}});
    // zig fmt: on
}

test "llshr shift 0" {
    const n = @bitSizeOf(Limb);

    // zig fmt: off
    try testOneShiftCase(.llshr, .{0,   &.{0},    &.{0}});
    try testOneShiftCase(.llshr, .{1,   &.{0},    &.{0}});
    try testOneShiftCase(.llshr, .{5,   &.{0},    &.{0}});
    try testOneShiftCase(.llshr, .{13,  &.{0},    &.{0}});
    try testOneShiftCase(.llshr, .{20,  &.{0},    &.{0}});
    try testOneShiftCase(.llshr, .{0,   &.{0, 0}, &.{0, 0}});
    try testOneShiftCase(.llshr, .{2,   &.{0},    &.{0, 0}});
    try testOneShiftCase(.llshr, .{7,   &.{0},    &.{0, 0}});
    try testOneShiftCase(.llshr, .{11,  &.{0},    &.{0, 0}});
    try testOneShiftCase(.llshr, .{19,  &.{0},    &.{0, 0}});

    try testOneShiftCase(.llshr, .{n,   &.{0}, &.{0}});
    try testOneShiftCase(.llshr, .{2*n, &.{0}, &.{0}});
    try testOneShiftCase(.llshr, .{3*n, &.{0}, &.{0}});
    try testOneShiftCase(.llshr, .{4*n, &.{0}, &.{0}});
    try testOneShiftCase(.llshr, .{n,   &.{0}, &.{0, 0}});
    try testOneShiftCase(.llshr, .{2*n, &.{0}, &.{0, 0}});
    try testOneShiftCase(.llshr, .{3*n, &.{0}, &.{0, 0}});
    try testOneShiftCase(.llshr, .{4*n, &.{0}, &.{0, 0}});

    try testOneShiftCase(.llshr, .{1,  &.{}, &.{}});
    try testOneShiftCase(.llshr, .{2,  &.{}, &.{}});
    try testOneShiftCase(.llshr, .{64, &.{}, &.{}});
    // zig fmt: on
}

test "llshr to 0" {
    const n = @bitSizeOf(Limb);
    if (n != 64 and n != 32) return error.SkipZigTest;

    // zig fmt: off
    try testOneShiftCase(.llshr, .{1,   &.{0}, &.{0}});
    try testOneShiftCase(.llshr, .{1,   &.{0}, &.{1}});
    try testOneShiftCase(.llshr, .{5,   &.{0}, &.{1}});
    try testOneShiftCase(.llshr, .{65,  &.{0}, &.{0, 1}});
    try testOneShiftCase(.llshr, .{193, &.{0}, &.{0, 0, maxInt(Limb)}});
    try testOneShiftCase(.llshr, .{193, &.{0}, &.{maxInt(Limb), 1, maxInt(Limb)}});
    try testOneShiftCase(.llshr, .{193, &.{0}, &.{0xdeadbeef, 0xabcdefab, 0x1234}});
    // zig fmt: on
}

test "llshr single" {
    if (limb_bits != 64) return error.SkipZigTest;

    // 1 << 63
    const left_one = 0x8000000000000000;
    const maxint: Limb = 0xFFFFFFFFFFFFFFFF;

    // zig fmt: off
    try testOneShiftCase(.llshr, .{0,  &.{0},                  &.{0}});
    try testOneShiftCase(.llshr, .{0,  &.{1},                  &.{1}});
    try testOneShiftCase(.llshr, .{0,  &.{125484842448},       &.{125484842448}});
    try testOneShiftCase(.llshr, .{0,  &.{0xdeadbeef},         &.{0xdeadbeef}});
    try testOneShiftCase(.llshr, .{0,  &.{maxint},             &.{maxint}});
    try testOneShiftCase(.llshr, .{0,  &.{left_one},           &.{left_one}});
    try testOneShiftCase(.llshr, .{1,  &.{0},                  &.{0}});
    try testOneShiftCase(.llshr, .{1,  &.{1},                  &.{2}});
    try testOneShiftCase(.llshr, .{1,  &.{62742421224},        &.{125484842448}});
    try testOneShiftCase(.llshr, .{1,  &.{62742421223},        &.{125484842447}});
    try testOneShiftCase(.llshr, .{1,  &.{0x6f56df77},         &.{0xdeadbeef}});
    try testOneShiftCase(.llshr, .{1,  &.{0x7fffffffffffffff}, &.{maxint}});
    try testOneShiftCase(.llshr, .{1,  &.{0x4000000000000000}, &.{left_one}});
    try testOneShiftCase(.llshr, .{8,  &.{1},                  &.{256}});
    try testOneShiftCase(.llshr, .{8,  &.{490175165},          &.{125484842448}});
    try testOneShiftCase(.llshr, .{8,  &.{0xdeadbe},           &.{0xdeadbeef}});
    try testOneShiftCase(.llshr, .{8,  &.{0xffffffffffffff},   &.{maxint}});
    try testOneShiftCase(.llshr, .{8,  &.{0x80000000000000},   &.{left_one}});
    // zig fmt: on
}

test llshr {
    if (limb_bits != 64) return error.SkipZigTest;

    // 1 << 63
    const left_one = 0x8000000000000000;
    const maxint: Limb = 0xFFFFFFFFFFFFFFFF;

    // zig fmt: off
    try testOneShiftCase(.llshr, .{0,  &.{0, 0},                           &.{0, 0}});
    try testOneShiftCase(.llshr, .{0,  &.{0, 1},                           &.{0, 1}});
    try testOneShiftCase(.llshr, .{0,  &.{15, 1},                          &.{15, 1}});
    try testOneShiftCase(.llshr, .{0,  &.{987656565, 123456789456},        &.{987656565, 123456789456}});
    try testOneShiftCase(.llshr, .{0,  &.{0xfeebdaed, 0xdeadbeef},         &.{0xfeebdaed, 0xdeadbeef}});
    try testOneShiftCase(.llshr, .{0,  &.{1, maxint},                      &.{1, maxint}});
    try testOneShiftCase(.llshr, .{0,  &.{0, left_one},                    &.{0, left_one}});
    try testOneShiftCase(.llshr, .{1,  &.{0},                              &.{0, 0}});
    try testOneShiftCase(.llshr, .{1,  &.{left_one},                       &.{0, 1}});
    try testOneShiftCase(.llshr, .{1,  &.{0x8000000000000007},             &.{15, 1}});
    try testOneShiftCase(.llshr, .{1,  &.{493828282, 61728394728},         &.{987656565, 123456789456}});
    try testOneShiftCase(.llshr, .{1,  &.{0x800000007f75ed76, 0x6f56df77}, &.{0xfeebdaed, 0xdeadbeef}});
    try testOneShiftCase(.llshr, .{1,  &.{left_one, 0x7fffffffffffffff},   &.{1, maxint}});
    try testOneShiftCase(.llshr, .{1,  &.{0, 0x4000000000000000},          &.{0, left_one}});
    try testOneShiftCase(.llshr, .{64, &.{0},                              &.{0, 0}});
    try testOneShiftCase(.llshr, .{64, &.{1},                              &.{0, 1}});
    try testOneShiftCase(.llshr, .{64, &.{1},                              &.{15, 1}});
    try testOneShiftCase(.llshr, .{64, &.{123456789456},                   &.{987656565, 123456789456}});
    try testOneShiftCase(.llshr, .{64, &.{0xdeadbeef},                     &.{0xfeebdaed, 0xdeadbeef}});
    try testOneShiftCase(.llshr, .{64, &.{maxint},                         &.{1, maxint}});
    try testOneShiftCase(.llshr, .{64, &.{left_one},                       &.{0, left_one}});
    try testOneShiftCase(.llshr, .{72, &.{0},                              &.{0, 0}});
    try testOneShiftCase(.llshr, .{72, &.{0},                              &.{0, 1}});
    try testOneShiftCase(.llshr, .{72, &.{0},                              &.{15, 1}});
    try testOneShiftCase(.llshr, .{72, &.{482253083},                      &.{987656565, 123456789456}});
    try testOneShiftCase(.llshr, .{72, &.{0xdeadbe},                       &.{0xfeebdaed, 0xdeadbeef}});
    try testOneShiftCase(.llshr, .{72, &.{0xffffffffffffff},               &.{1, maxint}});
    try testOneShiftCase(.llshr, .{72, &.{0x80000000000000},               &.{0, left_one}});
    // zig fmt: on
}

const Case = struct { usize, []const Limb, []const Limb };

fn testOneShiftCase(comptime function: enum { llshr, llshl }, case: Case) !void {
    const func = if (function == .llshl) llshl else llshr;
    const shift_direction = if (function == .llshl) -1 else 1;

    try testOneShiftCaseNoAliasing(func, case);
    try testOneShiftCaseAliasing(func, case, shift_direction);
}

fn testOneShiftCaseNoAliasing(func: fn ([]Limb, []const Limb, usize) usize, case: Case) !void {
    const padding = maxInt(Limb);
    var r: [20]Limb = @splat(padding);

    const shift = case[0];
    const expected = case[1];
    const data = case[2];

    std.debug.assert(expected.len <= 20);

    const len = func(&r, data, shift);

    try std.testing.expectEqual(expected.len, len);
    try std.testing.expectEqualSlices(Limb, expected, r[0..len]);
    try std.testing.expect(mem.allEqual(Limb, r[len..], padding));
}

fn testOneShiftCaseAliasing(func: fn ([]Limb, []const Limb, usize) usize, case: Case, shift_direction: isize) !void {
    const padding = maxInt(Limb);
    var r: [60]Limb = @splat(padding);
    const base = 20;

    assert(shift_direction == 1 or shift_direction == -1);

    for (0..10) |limb_shift| {
        const shift = case[0];
        const expected = case[1];
        const data = case[2];

        std.debug.assert(expected.len <= 20);

        @memset(&r, padding);
        const final_limb_base: usize = @intCast(base + shift_direction * @as(isize, @intCast(limb_shift)));
        const written_data = r[final_limb_base..][0..data.len];
        @memcpy(written_data, data);

        const len = func(r[base..], written_data, shift);

        try std.testing.expectEqual(expected.len, len);
        try std.testing.expectEqualSlices(Limb, expected, r[base .. base + len]);
    }
}

test "format" {
    var a: Managed = try .init(std.testing.allocator);
    defer a.deinit();

    try a.set(123);
    try testFormat(a, "123");

    try a.set(-123);
    try testFormat(a, "-123");

    try a.set(20000000000000000000); // > maxInt(u64)
    try testFormat(a, "20000000000000000000");

    try a.set(1 << 64 * @sizeOf(usize) * 8);
    try testFormat(a, "(BigInt)");

    try a.set(-(1 << 64 * @sizeOf(usize) * 8));
    try testFormat(a, "(BigInt)");
}

fn testFormat(a: Managed, expected: []const u8) !void {
    try std.testing.expectFmt(expected, "{f}", .{a});
    try std.testing.expectFmt(expected, "{f}", .{a.toMutable()});
    try std.testing.expectFmt(expected, "{f}", .{a.toConst()});
}



---
File: /std/math/complex/abs.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the absolute value (modulus) of z.
pub fn abs(z: anytype) @TypeOf(z.re, z.im) {
    return math.hypot(z.re, z.im);
}

test abs {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = abs(a);
    try testing.expectApproxEqAbs(5.8309517, c, epsilon);
}



---
File: /std/math/complex/acos.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the arc-cosine of z.
pub fn acos(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const q = cmath.asin(z);
    return Complex(T).init(@as(T, math.pi) / 2 - q.re, -q.im);
}

test acos {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = acos(a);

    try testing.expectApproxEqAbs(0.5469737, c.re, epsilon);
    try testing.expectApproxEqAbs(-2.4529128, c.im, epsilon);
}



---
File: /std/math/complex/acosh.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the hyperbolic arc-cosine of z.
pub fn acosh(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const q = cmath.acos(z);

    return if (math.signbit(z.im))
        Complex(T).init(q.im, -q.re)
    else
        Complex(T).init(-q.im, q.re);
}

test acosh {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = acosh(a);

    try testing.expectApproxEqAbs(2.4529128, c.re, epsilon);
    try testing.expectApproxEqAbs(0.5469737, c.im, epsilon);
}



---
File: /std/math/complex/arg.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the angular component (in radians) of z.
pub fn arg(z: anytype) @TypeOf(z.re, z.im) {
    return math.atan2(z.im, z.re);
}

test arg {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = arg(a);
    try testing.expectApproxEqAbs(0.5404195, c, epsilon);
}



---
File: /std/math/complex/asin.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

// Returns the arc-sine of z.
pub fn asin(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const x = z.re;
    const y = z.im;

    const p = Complex(T).init(1.0 - (x - y) * (x + y), -2.0 * x * y);
    const q = Complex(T).init(-y, x);
    const r = cmath.log(q.add(cmath.sqrt(p)));

    return Complex(T).init(r.im, -r.re);
}

test asin {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = asin(a);

    try testing.expectApproxEqAbs(1.0238227, c.re, epsilon);
    try testing.expectApproxEqAbs(2.4529128, c.im, epsilon);
}



---
File: /std/math/complex/asinh.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the hyperbolic arc-sine of z.
pub fn asinh(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const q = Complex(T).init(-z.im, z.re);
    const r = cmath.asin(q);
    return Complex(T).init(r.im, -r.re);
}

test asinh {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = asinh(a);

    try testing.expectApproxEqAbs(2.4598298, c.re, epsilon);
    try testing.expectApproxEqAbs(0.5339993, c.im, epsilon);
}



---
File: /std/math/complex/atan.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/catanf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/catan.c

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the arc-tangent of z.
pub fn atan(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    return switch (T) {
        f32 => atan32(z),
        f64 => atan64(z),
        else => @compileError("atan not implemented for " ++ @typeName(z)),
    };
}

fn redupif32(x: f32) f32 {
    const DP1 = 3.140625;
    const DP2 = 9.67502593994140625e-4;
    const DP3 = 1.509957990978376432e-7;

    var t = x / math.pi;
    if (t >= 0.0) {
        t += 0.5;
    } else {
        t -= 0.5;
    }

    const u: f32 = @trunc(t);
    return ((x - u * DP1) - u * DP2) - u * DP3;
}

fn atan32(z: Complex(f32)) Complex(f32) {
    const x = z.re;
    const y = z.im;

    const x2 = x * x;
    var a = 1.0 - x2 - (y * y);

    var t = 0.5 * math.atan2(2.0 * x, a);
    const w = redupif32(t);

    t = y - 1.0;
    a = x2 + t * t;

    t = y + 1.0;
    a = (x2 + (t * t)) / a;
    return Complex(f32).init(w, 0.25 * @log(a));
}

fn redupif64(x: f64) f64 {
    const DP1 = 3.14159265160560607910;
    const DP2 = 1.98418714791870343106e-9;
    const DP3 = 1.14423774522196636802e-17;

    var t = x / math.pi;
    if (t >= 0.0) {
        t += 0.5;
    } else {
        t -= 0.5;
    }

    const u: f64 = @trunc(t);
    return ((x - u * DP1) - u * DP2) - u * DP3;
}

fn atan64(z: Complex(f64)) Complex(f64) {
    const x = z.re;
    const y = z.im;

    const x2 = x * x;
    var a = 1.0 - x2 - (y * y);

    var t = 0.5 * math.atan2(2.0 * x, a);
    const w = redupif64(t);

    t = y - 1.0;
    a = x2 + t * t;

    t = y + 1.0;
    a = (x2 + (t * t)) / a;
    return Complex(f64).init(w, 0.25 * @log(a));
}

test atan32 {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = atan(a);

    try testing.expectApproxEqAbs(1.423679, c.re, epsilon);
    try testing.expectApproxEqAbs(0.086569, c.im, epsilon);
}

test atan64 {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(5, 3);
    const c = atan(a);

    try testing.expectApproxEqAbs(1.4236790442393028, c.re, epsilon);
    try testing.expectApproxEqAbs(0.08656905917945844, c.im, epsilon);
}



---
File: /std/math/complex/atanh.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the hyperbolic arc-tangent of z.
pub fn atanh(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const q = Complex(T).init(-z.im, z.re);
    const r = cmath.atan(q);
    return Complex(T).init(r.im, -r.re);
}

test atanh {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = atanh(a);

    try testing.expectApproxEqAbs(0.14694665, c.re, epsilon);
    try testing.expectApproxEqAbs(1.4808695, c.im, epsilon);
}



---
File: /std/math/complex/conj.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the complex conjugate of z.
pub fn conj(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    return Complex(T).init(z.re, -z.im);
}

test conj {
    const a = Complex(f32).init(5, 3);
    const c = a.conjugate();

    try testing.expectEqual(5, c.re);
    try testing.expectEqual(-3, c.im);
}



---
File: /std/math/complex/cos.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the cosine of z.
pub fn cos(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const p = Complex(T).init(-z.im, z.re);
    return cmath.cosh(p);
}

test cos {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = cos(a);

    try testing.expectApproxEqAbs(2.8558152, c.re, epsilon);
    try testing.expectApproxEqAbs(9.606383, c.im, epsilon);
}



---
File: /std/math/complex/cosh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/ccoshf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/ccosh.c

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

const ldexp_cexp = @import("ldexp.zig").ldexp_cexp;

/// Returns the hyperbolic arc-cosine of z.
pub fn cosh(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    return switch (T) {
        f32 => cosh32(z),
        f64 => cosh64(z),
        else => @compileError("cosh not implemented for " ++ @typeName(z)),
    };
}

fn cosh32(z: Complex(f32)) Complex(f32) {
    const x = z.re;
    const y = z.im;

    const hx: u32 = @bitCast(x);
    const ix = hx & 0x7fffffff;

    const hy: u32 = @bitCast(y);
    const iy = hy & 0x7fffffff;

    if (ix < 0x7f800000 and iy < 0x7f800000) {
        if (iy == 0) {
            return Complex(f32).init(math.cosh(x), x * y);
        }
        // small x: normal case
        if (ix < 0x41100000) {
            return Complex(f32).init(math.cosh(x) * @cos(y), math.sinh(x) * @sin(y));
        }

        // |x|>= 9, so cosh(x) ~= exp(|x|)
        if (ix < 0x42b17218) {
            // x < 88.7: exp(|x|) won't overflow
            const h = @exp(@abs(x)) * 0.5;
            return Complex(f32).init(h * @cos(y), math.copysign(h, x) * @sin(y));
        }
        // x < 192.7: scale to avoid overflow
        else if (ix < 0x4340b1e7) {
            const v = Complex(f32).init(@abs(x), y);
            const r = ldexp_cexp(v, -1);
            return Complex(f32).init(r.re, r.im * math.copysign(@as(f32, 1.0), x));
        }
        // x >= 192.7: result always overflows
        else {
            const h = 0x1p127 * x;
            return Complex(f32).init(h * h * @cos(y), h * @sin(y));
        }
    }

    if (ix == 0 and iy >= 0x7f800000) {
        return Complex(f32).init(y - y, math.copysign(@as(f32, 0.0), x * (y - y)));
    }

    if (iy == 0 and ix >= 0x7f800000) {
        if (hx & 0x7fffff == 0) {
            return Complex(f32).init(x * x, math.copysign(@as(f32, 0.0), x) * y);
        }
        return Complex(f32).init(x * x, math.copysign(@as(f32, 0.0), (x + x) * y));
    }

    if (ix < 0x7f800000 and iy >= 0x7f800000) {
        return Complex(f32).init(y - y, x * (y - y));
    }

    if (ix >= 0x7f800000 and (hx & 0x7fffff) == 0) {
        if (iy >= 0x7f800000) {
            return Complex(f32).init(x * x, x * (y - y));
        }
        return Complex(f32).init((x * x) * @cos(y), x * @sin(y));
    }

    return Complex(f32).init((x * x) * (y - y), (x + x) * (y - y));
}

fn cosh64(z: Complex(f64)) Complex(f64) {
    const x = z.re;
    const y = z.im;

    const fx: u64 = @bitCast(x);
    const hx: u32 = @intCast(fx >> 32);
    const lx: u32 = @truncate(fx);
    const ix = hx & 0x7fffffff;

    const fy: u64 = @bitCast(y);
    const hy: u32 = @intCast(fy >> 32);
    const ly: u32 = @truncate(fy);
    const iy = hy & 0x7fffffff;

    // nearly non-exceptional case where x, y are finite
    if (ix < 0x7ff00000 and iy < 0x7ff00000) {
        if (iy | ly == 0) {
            return Complex(f64).init(math.cosh(x), x * y);
        }
        // small x: normal case
        if (ix < 0x40360000) {
            return Complex(f64).init(math.cosh(x) * @cos(y), math.sinh(x) * @sin(y));
        }

        // |x|>= 22, so cosh(x) ~= exp(|x|)
        if (ix < 0x40862e42) {
            // x < 710: exp(|x|) won't overflow
            const h = @exp(@abs(x)) * 0.5;
            return Complex(f64).init(h * @cos(y), math.copysign(h, x) * @sin(y));
        }
        // x < 1455: scale to avoid overflow
        else if (ix < 0x4096bbaa) {
            const v = Complex(f64).init(@abs(x), y);
            const r = ldexp_cexp(v, -1);
            return Complex(f64).init(r.re, r.im * math.copysign(@as(f64, 1.0), x));
        }
        // x >= 1455: result always overflows
        else {
            const h = 0x1p1023 * x;
            return Complex(f64).init(h * h * @cos(y), h * @sin(y));
        }
    }

    if (ix | lx == 0 and iy >= 0x7ff00000) {
        return Complex(f64).init(y - y, math.copysign(@as(f64, 0.0), x * (y - y)));
    }

    if (iy | ly == 0 and ix >= 0x7ff00000) {
        if ((hx & 0xfffff) | lx == 0) {
            return Complex(f64).init(x * x, math.copysign(@as(f64, 0.0), x) * y);
        }
        return Complex(f64).init(x * x, math.copysign(@as(f64, 0.0), (x + x) * y));
    }

    if (ix < 0x7ff00000 and iy >= 0x7ff00000) {
        return Complex(f64).init(y - y, x * (y - y));
    }

    if (ix >= 0x7ff00000 and (hx & 0xfffff) | lx == 0) {
        if (iy >= 0x7ff00000) {
            return Complex(f64).init(x * x, x * (y - y));
        }
        return Complex(f64).init(x * x * @cos(y), x * @sin(y));
    }

    return Complex(f64).init((x * x) * (y - y), (x + x) * (y - y));
}

test cosh32 {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = cosh(a);

    try testing.expectApproxEqAbs(-73.467300, c.re, epsilon);
    try testing.expectApproxEqAbs(10.471557, c.im, epsilon);
}

test cosh64 {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(5, 3);
    const c = cosh(a);

    try testing.expectApproxEqAbs(-73.46729221264526, c.re, epsilon);
    try testing.expectApproxEqAbs(10.471557674805572, c.im, epsilon);
}

test "cosh64 musl" {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(7.44648873421389e17, 1.6008058402057622e19);
    const c = cosh(a);

    try testing.expectApproxEqAbs(std.math.inf(f64), c.re, epsilon);
    try testing.expectApproxEqAbs(std.math.inf(f64), c.im, epsilon);
}



---
File: /std/math/complex/exp.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/cexpf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/cexp.c

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

const ldexp_cexp = @import("ldexp.zig").ldexp_cexp;

/// Returns e raised to the power of z (e^z).
pub fn exp(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);

    return switch (T) {
        f32 => exp32(z),
        f64 => exp64(z),
        else => @compileError("exp not implemented for " ++ @typeName(z)),
    };
}

fn exp32(z: Complex(f32)) Complex(f32) {
    const exp_overflow = 0x42b17218; // max_exp * ln2 ~= 88.72283955
    const cexp_overflow = 0x43400074; // (max_exp - min_denom_exp) * ln2

    const x = z.re;
    const y = z.im;

    const hy = @as(u32, @bitCast(y)) & 0x7fffffff;
    // cexp(x + i0) = exp(x) + i0
    if (hy == 0) {
        return Complex(f32).init(@exp(x), y);
    }

    const hx = @as(u32, @bitCast(x));
    // cexp(0 + iy) = cos(y) + isin(y)
    if ((hx & 0x7fffffff) == 0) {
        return Complex(f32).init(@cos(y), @sin(y));
    }

    if (hy >= 0x7f800000) {
        // cexp(finite|nan +- i inf|nan) = nan + i nan
        if ((hx & 0x7fffffff) != 0x7f800000) {
            return Complex(f32).init(y - y, y - y);
        } // cexp(-inf +- i inf|nan) = 0 + i0
        else if (hx & 0x80000000 != 0) {
            return Complex(f32).init(0, 0);
        } // cexp(+inf +- i inf|nan) = inf + i nan
        else {
            return Complex(f32).init(x, y - y);
        }
    }

    // 88.7 <= x <= 192 so must scale
    if (hx >= exp_overflow and hx <= cexp_overflow) {
        return ldexp_cexp(z, 0);
    } // - x < exp_overflow => exp(x) won't overflow (common)
    // - x > cexp_overflow, so exp(x) * s overflows for s > 0
    // - x = +-inf
    // - x = nan
    else {
        const exp_x = @exp(x);
        return Complex(f32).init(exp_x * @cos(y), exp_x * @sin(y));
    }
}

fn exp64(z: Complex(f64)) Complex(f64) {
    const exp_overflow = 0x40862e42; // high bits of max_exp * ln2 ~= 710
    const cexp_overflow = 0x4096b8e4; // (max_exp - min_denorm_exp) * ln2

    const x = z.re;
    const y = z.im;

    const fy: u64 = @bitCast(y);
    const hy: u32 = @intCast((fy >> 32) & 0x7fffffff);
    const ly: u32 = @truncate(fy);

    // cexp(x + i0) = exp(x) + i0
    if (hy | ly == 0) {
        return Complex(f64).init(@exp(x), y);
    }

    const fx: u64 = @bitCast(x);
    const hx: u32 = @intCast(fx >> 32);
    const lx: u32 = @truncate(fx);

    // cexp(0 + iy) = cos(y) + isin(y)
    if ((hx & 0x7fffffff) | lx == 0) {
        return Complex(f64).init(@cos(y), @sin(y));
    }

    if (hy >= 0x7ff00000) {
        // cexp(finite|nan +- i inf|nan) = nan + i nan
        if (lx != 0 or (hx & 0x7fffffff) != 0x7ff00000) {
            return Complex(f64).init(y - y, y - y);
        } // cexp(-inf +- i inf|nan) = 0 + i0
        else if (hx & 0x80000000 != 0) {
            return Complex(f64).init(0, 0);
        } // cexp(+inf +- i inf|nan) = inf + i nan
        else {
            return Complex(f64).init(x, y - y);
        }
    }

    // 709.7 <= x <= 1454.3 so must scale
    if (hx >= exp_overflow and hx <= cexp_overflow) {
        return ldexp_cexp(z, 0);
    } // - x < exp_overflow => exp(x) won't overflow (common)
    // - x > cexp_overflow, so exp(x) * s overflows for s > 0
    // - x = +-inf
    // - x = nan
    else {
        const exp_x = @exp(x);
        return Complex(f64).init(exp_x * @cos(y), exp_x * @sin(y));
    }
}

test exp32 {
    const tolerance_f32 = @sqrt(math.floatEps(f32));

    {
        const a = Complex(f32).init(5, 3);
        const c = exp(a);

        try testing.expectApproxEqRel(@as(f32, -1.46927917e+02), c.re, tolerance_f32);
        try testing.expectApproxEqRel(@as(f32, 2.0944065e+01), c.im, tolerance_f32);
    }

    {
        const a = Complex(f32).init(88.8, 0x1p-149);
        const c = exp(a);

        try testing.expectApproxEqAbs(math.inf(f32), c.re, tolerance_f32);
        try testing.expectApproxEqAbs(@as(f32, 5.15088629e-07), c.im, tolerance_f32);
    }
}

test exp64 {
    const tolerance_f64 = @sqrt(math.floatEps(f64));

    {
        const a = Complex(f64).init(5, 3);
        const c = exp(a);

        try testing.expectApproxEqRel(@as(f64, -1.469279139083189e+02), c.re, tolerance_f64);
        try testing.expectApproxEqRel(@as(f64, 2.094406620874596e+01), c.im, tolerance_f64);
    }

    {
        const a = Complex(f64).init(709.8, 0x1p-1074);
        const c = exp(a);

        try testing.expectApproxEqAbs(math.inf(f64), c.re, tolerance_f64);
        try testing.expectApproxEqAbs(@as(f64, 9.036659362159884e-16), c.im, tolerance_f64);
    }
}



---
File: /std/math/complex/ldexp.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/__cexpf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/__cexp.c

const std = @import("../../std.zig");
const debug = std.debug;
const math = std.math;
const testing = std.testing;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns exp(z) scaled to avoid overflow.
pub fn ldexp_cexp(z: anytype, expt: i32) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);

    return switch (T) {
        f32 => ldexp_cexp32(z, expt),
        f64 => ldexp_cexp64(z, expt),
        else => unreachable,
    };
}

fn frexp_exp32(x: f32, expt: *i32) f32 {
    const k = 235; // reduction constant
    const kln2 = 162.88958740; // k * ln2

    const exp_x = @exp(x - kln2);
    const hx = @as(u32, @bitCast(exp_x));
    // TODO zig should allow this cast implicitly because it should know the value is in range
    expt.* = @as(i32, @intCast(hx >> 23)) - (0x7f + 127) + k;
    return @as(f32, @bitCast((hx & 0x7fffff) | ((0x7f + 127) << 23)));
}

fn ldexp_cexp32(z: Complex(f32), expt: i32) Complex(f32) {
    var ex_expt: i32 = undefined;
    const exp_x = frexp_exp32(z.re, &ex_expt);
    const exptf = expt + ex_expt;

    const half_expt1 = @divTrunc(exptf, 2);
    const scale1 = @as(f32, @bitCast((0x7f + half_expt1) << 23));

    const half_expt2 = exptf - half_expt1;
    const scale2 = @as(f32, @bitCast((0x7f + half_expt2) << 23));

    return Complex(f32).init(
        @cos(z.im) * exp_x * scale1 * scale2,
        @sin(z.im) * exp_x * scale1 * scale2,
    );
}

fn frexp_exp64(x: f64, expt: *i32) f64 {
    const k = 1799; // reduction constant
    const kln2 = 1246.97177782734161156; // k * ln2

    const exp_x = @exp(x - kln2);

    const fx = @as(u64, @bitCast(exp_x));
    const hx = @as(u32, @intCast(fx >> 32));
    const lx = @as(u32, @truncate(fx));

    expt.* = @as(i32, @intCast(hx >> 20)) - (0x3ff + 1023) + k;

    const high_word = (hx & 0xfffff) | ((0x3ff + 1023) << 20);
    return @as(f64, @bitCast((@as(u64, high_word) << 32) | lx));
}

fn ldexp_cexp64(z: Complex(f64), expt: i32) Complex(f64) {
    var ex_expt: i32 = undefined;
    const exp_x = frexp_exp64(z.re, &ex_expt);
    const exptf = @as(i64, expt + ex_expt);

    const half_expt1 = @divTrunc(exptf, 2);
    const scale1 = @as(f64, @bitCast((0x3ff + half_expt1) << (20 + 32)));

    const half_expt2 = exptf - half_expt1;
    const scale2 = @as(f64, @bitCast((0x3ff + half_expt2) << (20 + 32)));

    return Complex(f64).init(
        @cos(z.im) * exp_x * scale1 * scale2,
        @sin(z.im) * exp_x * scale1 * scale2,
    );
}



---
File: /std/math/complex/log.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the natural logarithm of z.
pub fn log(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const r = cmath.abs(z);
    const phi = cmath.arg(z);

    return Complex(T).init(@log(r), phi);
}

test log {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = log(a);

    try testing.expectApproxEqAbs(1.7631803, c.re, epsilon);
    try testing.expectApproxEqAbs(0.5404195, c.im, epsilon);
}



---
File: /std/math/complex/pow.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns z raised to the complex power of c.
pub fn pow(z: anytype, s: anytype) Complex(@TypeOf(z.re, z.im, s.re, s.im)) {
    return cmath.exp(cmath.log(z).mul(s));
}

test pow {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const b = Complex(f32).init(2.3, -1.3);
    const c = pow(a, b);

    try testing.expectApproxEqAbs(58.049110, c.re, epsilon);
    try testing.expectApproxEqAbs(-101.003433, c.im, epsilon);
}



---
File: /std/math/complex/proj.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the projection of z onto the riemann sphere.
pub fn proj(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);

    if (math.isInf(z.re) or math.isInf(z.im)) {
        return Complex(T).init(math.inf(T), math.copysign(@as(T, 0.0), z.re));
    }

    return Complex(T).init(z.re, z.im);
}

test proj {
    const a = Complex(f32).init(5, 3);
    const c = proj(a);

    try testing.expectEqual(5, c.re);
    try testing.expectEqual(3, c.im);
}



---
File: /std/math/complex/sin.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the sine of z.
pub fn sin(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const p = Complex(T).init(-z.im, z.re);
    const q = cmath.sinh(p);
    return Complex(T).init(q.im, -q.re);
}

test sin {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = sin(a);

    try testing.expectApproxEqAbs(-9.654126, c.re, epsilon);
    try testing.expectApproxEqAbs(2.8416924, c.im, epsilon);
}



---
File: /std/math/complex/sinh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/csinhf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/csinh.c

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

const ldexp_cexp = @import("ldexp.zig").ldexp_cexp;

/// Returns the hyperbolic sine of z.
pub fn sinh(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    return switch (T) {
        f32 => sinh32(z),
        f64 => sinh64(z),
        else => @compileError("tan not implemented for " ++ @typeName(z)),
    };
}

fn sinh32(z: Complex(f32)) Complex(f32) {
    const x = z.re;
    const y = z.im;

    const hx = @as(u32, @bitCast(x));
    const ix = hx & 0x7fffffff;

    const hy = @as(u32, @bitCast(y));
    const iy = hy & 0x7fffffff;

    if (ix < 0x7f800000 and iy < 0x7f800000) {
        if (iy == 0) {
            return Complex(f32).init(math.sinh(x), y);
        }
        // small x: normal case
        if (ix < 0x41100000) {
            return Complex(f32).init(math.sinh(x) * @cos(y), math.cosh(x) * @sin(y));
        }

        // |x|>= 9, so cosh(x) ~= exp(|x|)
        if (ix < 0x42b17218) {
            // x < 88.7: exp(|x|) won't overflow
            const h = @exp(@abs(x)) * 0.5;
            return Complex(f32).init(math.copysign(h, x) * @cos(y), h * @sin(y));
        }
        // x < 192.7: scale to avoid overflow
        else if (ix < 0x4340b1e7) {
            const v = Complex(f32).init(@abs(x), y);
            const r = ldexp_cexp(v, -1);
            return Complex(f32).init(r.re * math.copysign(@as(f32, 1.0), x), r.im);
        }
        // x >= 192.7: result always overflows
        else {
            const h = 0x1p127 * x;
            return Complex(f32).init(h * @cos(y), h * h * @sin(y));
        }
    }

    if (ix == 0 and iy >= 0x7f800000) {
        return Complex(f32).init(math.copysign(@as(f32, 0.0), x * (y - y)), y - y);
    }

    if (iy == 0 and ix >= 0x7f800000) {
        if (hx & 0x7fffff == 0) {
            return Complex(f32).init(x, y);
        }
        return Complex(f32).init(x, math.copysign(@as(f32, 0.0), y));
    }

    if (ix < 0x7f800000 and iy >= 0x7f800000) {
        return Complex(f32).init(y - y, x * (y - y));
    }

    if (ix >= 0x7f800000 and (hx & 0x7fffff) == 0) {
        if (iy >= 0x7f800000) {
            return Complex(f32).init(x * x, x * (y - y));
        }
        return Complex(f32).init(x * @cos(y), math.inf(f32) * @sin(y));
    }

    return Complex(f32).init((x * x) * (y - y), (x + x) * (y - y));
}

fn sinh64(z: Complex(f64)) Complex(f64) {
    const x = z.re;
    const y = z.im;

    const fx: u64 = @bitCast(x);
    const hx: u32 = @intCast(fx >> 32);
    const lx: u32 = @truncate(fx);
    const ix = hx & 0x7fffffff;

    const fy: u64 = @bitCast(y);
    const hy: u32 = @intCast(fy >> 32);
    const ly: u32 = @truncate(fy);
    const iy = hy & 0x7fffffff;

    if (ix < 0x7ff00000 and iy < 0x7ff00000) {
        if (iy | ly == 0) {
            return Complex(f64).init(math.sinh(x), y);
        }
        // small x: normal case
        if (ix < 0x40360000) {
            return Complex(f64).init(math.sinh(x) * @cos(y), math.cosh(x) * @sin(y));
        }

        // |x|>= 22, so cosh(x) ~= exp(|x|)
        if (ix < 0x40862e42) {
            // x < 710: exp(|x|) won't overflow
            const h = @exp(@abs(x)) * 0.5;
            return Complex(f64).init(math.copysign(h, x) * @cos(y), h * @sin(y));
        }
        // x < 1455: scale to avoid overflow
        else if (ix < 0x4096bbaa) {
            const v = Complex(f64).init(@abs(x), y);
            const r = ldexp_cexp(v, -1);
            return Complex(f64).init(r.re * math.copysign(@as(f64, 1.0), x), r.im);
        }
        // x >= 1455: result always overflows
        else {
            const h = 0x1p1023 * x;
            return Complex(f64).init(h * @cos(y), h * h * @sin(y));
        }
    }

    if (ix | lx == 0 and iy >= 0x7ff00000) {
        return Complex(f64).init(math.copysign(@as(f64, 0.0), x * (y - y)), y - y);
    }

    if (iy | ly == 0 and ix >= 0x7ff00000) {
        if ((hx & 0xfffff) | lx == 0) {
            return Complex(f64).init(x, y);
        }
        return Complex(f64).init(x, math.copysign(@as(f64, 0.0), y));
    }

    if (ix < 0x7ff00000 and iy >= 0x7ff00000) {
        return Complex(f64).init(y - y, x * (y - y));
    }

    if (ix >= 0x7ff00000 and (hx & 0xfffff) | lx == 0) {
        if (iy >= 0x7ff00000) {
            return Complex(f64).init(x * x, x * (y - y));
        }
        return Complex(f64).init(x * @cos(y), math.inf(f64) * @sin(y));
    }

    return Complex(f64).init((x * x) * (y - y), (x + x) * (y - y));
}

test sinh32 {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = sinh(a);

    try testing.expectApproxEqAbs(-73.460617, c.re, epsilon);
    try testing.expectApproxEqAbs(10.472508, c.im, epsilon);
}

test sinh64 {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(5, 3);
    const c = sinh(a);

    try testing.expectApproxEqAbs(-73.46062169567367, c.re, epsilon);
    try testing.expectApproxEqAbs(10.472508533940392, c.im, epsilon);
}



---
File: /std/math/complex/sqrt.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/csqrtf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/csqrt.c

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the square root of z. The real and imaginary parts of the result have the same sign
/// as the imaginary part of z.
pub fn sqrt(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);

    return switch (T) {
        f32 => sqrt32(z),
        f64 => sqrt64(z),
        else => @compileError("sqrt not implemented for " ++ @typeName(T)),
    };
}

fn sqrt32(z: Complex(f32)) Complex(f32) {
    const x = z.re;
    const y = z.im;

    if (x == 0 and y == 0) {
        return Complex(f32).init(0, y);
    }
    if (math.isInf(y)) {
        return Complex(f32).init(math.inf(f32), y);
    }
    if (math.isNan(x)) {
        // raise invalid if y is not nan
        const t = (y - y) / (y - y);
        return Complex(f32).init(x, t);
    }
    if (math.isInf(x)) {
        // sqrt(inf + i nan)    = inf + nan i
        // sqrt(inf + iy)       = inf + i0
        // sqrt(-inf + i nan)   = nan +- inf i
        // sqrt(-inf + iy)      = 0 + inf i
        if (math.signbit(x)) {
            return Complex(f32).init(@abs(y - y), math.copysign(x, y));
        } else {
            return Complex(f32).init(x, math.copysign(y - y, y));
        }
    }

    // y = nan special case is handled fine below

    // double-precision avoids overflow with correct rounding.
    const dx = @as(f64, x);
    const dy = @as(f64, y);

    if (dx >= 0) {
        const t = @sqrt((dx + math.hypot(dx, dy)) * 0.5);
        return Complex(f32).init(
            @as(f32, @floatCast(t)),
            @as(f32, @floatCast(dy / (2.0 * t))),
        );
    } else {
        const t = @sqrt((-dx + math.hypot(dx, dy)) * 0.5);
        return Complex(f32).init(
            @as(f32, @floatCast(@abs(y) / (2.0 * t))),
            @as(f32, @floatCast(math.copysign(t, y))),
        );
    }
}

fn sqrt64(z: Complex(f64)) Complex(f64) {
    // may encounter overflow for im,re >= DBL_MAX / (1 + sqrt(2))
    const threshold = 0x1.a827999fcef32p+1022;

    var x = z.re;
    var y = z.im;

    if (x == 0 and y == 0) {
        return Complex(f64).init(0, y);
    }
    if (math.isInf(y)) {
        return Complex(f64).init(math.inf(f64), y);
    }
    if (math.isNan(x)) {
        // raise invalid if y is not nan
        const t = (y - y) / (y - y);
        return Complex(f64).init(x, t);
    }
    if (math.isInf(x)) {
        // sqrt(inf + i nan)    = inf + nan i
        // sqrt(inf + iy)       = inf + i0
        // sqrt(-inf + i nan)   = nan +- inf i
        // sqrt(-inf + iy)      = 0 + inf i
        if (math.signbit(x)) {
            return Complex(f64).init(@abs(y - y), math.copysign(x, y));
        } else {
            return Complex(f64).init(x, math.copysign(y - y, y));
        }
    }

    // y = nan special case is handled fine below

    // scale to avoid overflow
    var scale = false;
    if (@abs(x) >= threshold or @abs(y) >= threshold) {
        x *= 0.25;
        y *= 0.25;
        scale = true;
    }

    var result: Complex(f64) = undefined;
    if (x >= 0) {
        const t = @sqrt((x + math.hypot(x, y)) * 0.5);
        result = Complex(f64).init(t, y / (2.0 * t));
    } else {
        const t = @sqrt((-x + math.hypot(x, y)) * 0.5);
        result = Complex(f64).init(@abs(y) / (2.0 * t), math.copysign(t, y));
    }

    if (scale) {
        result.re *= 2;
        result.im *= 2;
    }

    return result;
}

test sqrt32 {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = sqrt(a);

    try testing.expectApproxEqAbs(2.3271174, c.re, epsilon);
    try testing.expectApproxEqAbs(0.6445742, c.im, epsilon);
}

test sqrt64 {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(5, 3);
    const c = sqrt(a);

    try testing.expectApproxEqAbs(2.3271175190399496, c.re, epsilon);
    try testing.expectApproxEqAbs(0.6445742373246469, c.im, epsilon);
}



---
File: /std/math/complex/tan.zig
---

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the tangent of z.
pub fn tan(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    const q = Complex(T).init(-z.im, z.re);
    const r = cmath.tanh(q);
    return Complex(T).init(r.im, -r.re);
}

test tan {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = tan(a);

    try testing.expectApproxEqAbs(-0.002708233, c.re, epsilon);
    try testing.expectApproxEqAbs(1.0041647, c.im, epsilon);
}



---
File: /std/math/complex/tanh.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/complex/ctanhf.c
// https://git.musl-libc.org/cgit/musl/tree/src/complex/ctanh.c

const std = @import("../../std.zig");
const testing = std.testing;
const math = std.math;
const cmath = math.complex;
const Complex = cmath.Complex;

/// Returns the hyperbolic tangent of z.
pub fn tanh(z: anytype) Complex(@TypeOf(z.re, z.im)) {
    const T = @TypeOf(z.re, z.im);
    return switch (T) {
        f32 => tanh32(z),
        f64 => tanh64(z),
        else => @compileError("tan not implemented for " ++ @typeName(z)),
    };
}

fn tanh32(z: Complex(f32)) Complex(f32) {
    const x = z.re;
    const y = z.im;

    const hx = @as(u32, @bitCast(x));
    const ix = hx & 0x7fffffff;

    if (ix >= 0x7f800000) {
        if (ix & 0x7fffff != 0) {
            const r = if (y == 0) y else x * y;
            return Complex(f32).init(x, r);
        }
        const xx = @as(f32, @bitCast(hx - 0x40000000));
        const r = if (math.isInf(y)) y else @sin(y) * @cos(y);
        return Complex(f32).init(xx, math.copysign(@as(f32, 0.0), r));
    }

    if (!math.isFinite(y)) {
        const r = if (ix != 0) y - y else x;
        return Complex(f32).init(r, y - y);
    }

    // x >= 11
    if (ix >= 0x41300000) {
        const exp_mx = @exp(-@abs(x));
        return Complex(f32).init(math.copysign(@as(f32, 1.0), x), 4 * @sin(y) * @cos(y) * exp_mx * exp_mx);
    }

    // Kahan's algorithm
    const t = @tan(y);
    const beta = 1.0 + t * t;
    const s = math.sinh(x);
    const rho = @sqrt(1 + s * s);
    const den = 1 + beta * s * s;

    return Complex(f32).init((beta * rho * s) / den, t / den);
}

fn tanh64(z: Complex(f64)) Complex(f64) {
    const x = z.re;
    const y = z.im;

    const fx: u64 = @bitCast(x);
    // TODO: zig should allow this conversion implicitly because it can notice that the value necessarily
    // fits in range.
    const hx: u32 = @intCast(fx >> 32);
    const lx: u32 = @truncate(fx);
    const ix = hx & 0x7fffffff;

    if (ix >= 0x7ff00000) {
        if ((ix & 0xfffff) | lx != 0) {
            const r = if (y == 0) y else x * y;
            return Complex(f64).init(x, r);
        }

        const xx: f64 = @bitCast((@as(u64, hx - 0x40000000) << 32) | lx);
        const r = if (math.isInf(y)) y else @sin(y) * @cos(y);
        return Complex(f64).init(xx, math.copysign(@as(f64, 0.0), r));
    }

    if (!math.isFinite(y)) {
        const r = if (ix != 0) y - y else x;
        return Complex(f64).init(r, y - y);
    }

    // x >= 22
    if (ix >= 0x40360000) {
        const exp_mx = @exp(-@abs(x));
        return Complex(f64).init(math.copysign(@as(f64, 1.0), x), 4 * @sin(y) * @cos(y) * exp_mx * exp_mx);
    }

    // Kahan's algorithm
    const t = @tan(y);
    const beta = 1.0 + t * t;
    const s = math.sinh(x);
    const rho = @sqrt(1 + s * s);
    const den = 1 + beta * s * s;

    return Complex(f64).init((beta * rho * s) / den, t / den);
}

test tanh32 {
    const epsilon = math.floatEps(f32);
    const a = Complex(f32).init(5, 3);
    const c = tanh(a);

    try testing.expectApproxEqAbs(0.99991274, c.re, epsilon);
    try testing.expectApproxEqAbs(-0.00002536878, c.im, epsilon);
}

test tanh64 {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(5, 3);
    const c = tanh(a);

    try testing.expectApproxEqAbs(0.9999128201513536, c.re, epsilon);
    try testing.expectApproxEqAbs(-0.00002536867620767604, c.im, epsilon);
}

test "tanh64 musl" {
    const epsilon = math.floatEps(f64);
    const a = Complex(f64).init(std.math.inf(f64), std.math.inf(f64));
    const c = tanh(a);

    try testing.expectApproxEqAbs(1, c.re, epsilon);
    try testing.expectApproxEqAbs(0, c.im, epsilon);
}



---
File: /std/math/acos.zig
---

// Ported from musl, which is licensed under the MIT license:
// https://git.musl-libc.org/cgit/musl/tree/COPYRIGHT
//
// https://git.musl-libc.org/cgit/musl/tree/src/math/acosf.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/acos.c
// https://git.musl-libc.org/cgit/musl/tree/src/math/acosl.c
//
// Ported from ARM-software, which is licensed under the MIT license:
// https://github.com/ARM-software/optimized-routines/blob/master/LICENSE
//
// https://github.com/ARM-software/optimized-routines/blob/master/math/aarch64/advsimd/acosf.c
// https://github.com/ARM-software/optimized-routines/blob/master/math/aarch64/advsimd/acos.c

const std = @import("../std.zig");
const math = std.math;
const testing = std.testing;
const builtin = @import("builtin");
const native_endian = builtin.cpu.arch.endian();

/// Returns the arc-cosine of x.
///
/// Special cases:
///  - acos(x)   = nan if x < -1 or x > 1
pub fn acos(x: anytype) @TypeOf(x) {
    const T = @TypeOf(x);
    switch (@typeInfo(T)) {
        .float => |info| switch (info.bits) {
            16 => return acosBinary16(x),
            32 => return acosBinary32(x),
            64 => return acosBinary64(x),
            80 => return acosExtended80(x),
            128 => return acosBinary128(x),
            else => comptime unreachable,
        },
        .vector => |info| switch (info.child) {
            f32 => return acosBinary32Vec(info.len, x),
            f64 => return acosBinary64Vec(info.len, x),
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

fn acosBinary16(x: f16) f16 {
    const pio2: f32 = math.pi / 2.0;

    const hx: u16 = @bitCast(x);
    const ix: u16 = hx & 0x7fff;

    // |x| >= 1 or nan
    if (ix >= 0x3c00) {
        if (ix == 0x3c00) {
            if (hx >> 15 != 0) {
                return @floatCast(2.0 * pio2 + 0x1p-120);
            }
            return 0.0;
        }
        return 0.0 / (x - x);
    }

    const xf: f32 = @floatCast(x);

    // |x| < 0.5
    if (ix < 0x3800) {
        return @floatCast(pio2 - xf * approxBinary16(xf * xf));
    }

    // x < -0.5
    if (hx >> 15 != 0) {
        const z = (1.0 + xf) * 0.5;
        const s = @sqrt(z);
        const w = approxBinary16(z) * s;
        return @floatCast(2.0 * (pio2 - w));
    }

    // x > 0.5
    const z = (1.0 - xf) * 0.5;
    const s = @sqrt(z);
    const w = approxBinary16(z) * s;
    return @floatCast(2.0 * w);
}

fn rationalApproxBinary32(z: f32) f32 {
    const pS0: f32 = 1.6666586697e-01;
    const pS1: f32 = -4.2743422091e-02;
    const pS2: f32 = -8.6563630030e-03;
    const qS1: f32 = -7.0662963390e-01;

    // f64 is used instead of f32 to avoid
    // a vectorization on x86_64. The vectorization
    // causes extra floating point execeptions
    // that are prohibited by libc-test.
    const p: f64 = @as(f64, @floatCast(z)) * (pS0 + z * (pS1 + z * pS2));
    const q: f64 = 1.0 + z * qS1;
    return @floatCast(p / q);
}

fn acosBinary32(x: f32) f32 {
    const pio2_hi: f32 = 1.5707962513e+00;
    const pio2_lo: f32 = 7.5497894159e-08;

    const hx: u32 = @bitCast(x);
    const ix: u32 = hx & 0x7fff_ffff;

    // |x| >= 1 or nan
    if (ix >= 0x3f800000) {
        if (ix == 0x3f800000) {
            if (hx >> 31 != 0) {
                return 2.0 * pio2_hi + 0x1.0p-120;
            }
            return 0.0;
        }
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (ix < 0x3f00_0000) {
        // |x| < 2^(-26)
        if (ix <= 0x3280_0000) {
            return pio2_hi + 0x1.0p-120;
        }
        return pio2_hi - (x - (pio2_lo - x * rationalApproxBinary32(x * x)));
    }

    // x < -0.5
    if (hx >> 31 != 0) {
        const z = (1 + x) * 0.5;
        const s = @sqrt(z);
        const w = rationalApproxBinary32(z) * s - pio2_lo;
        return 2.0 * (pio2_hi - (s + w));
    }

    // x > 0.5
    const z = (1.0 - x) * 0.5;
    const s = @sqrt(z);
    const hs: u32 = @bitCast(s);
    const df: f32 = @bitCast(hs & 0xffff_f000);
    const c = (z - df * df) / (s + df);
    const w = rationalApproxBinary32(z) * s + c;
    return 2.0 * (df + w);
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

fn acosBinary64(x: f64) f64 {
    const pio2_hi: f64 = 1.57079632679489655800e+00;
    const pio2_lo: f64 = 6.12323399573676603587e-17;

    const hx: u32 = @intCast(@as(u64, @bitCast(x)) >> 32);
    const ix: u32 = hx & 0x7fff_ffff;

    // |x| >= 1 or nan
    if (ix >= 0x3ff0_0000) {
        const lx: u32 = @truncate(@as(u64, @bitCast(x)));
        if ((ix - 0x3ff0_0000 | lx) == 0) {
            if (hx >> 31 != 0) {
                return 2.0 * pio2_hi + 0x1.0p-120;
            }
            return 0.0;
        }
        return 0.0 / (x - x);
    }

    // |x| < 0.5
    if (ix < 0x3fe0_0000) {
        // |x| < 2^(-57)
        if (ix <= 0x3c60_0000) {
            return pio2_hi + 0x1.0p-120;
        }
        return pio2_hi - (x - (pio2_lo - x * rationalApproxBinary64(x * x)));
    }

    // x < -0.5
    if (hx >> 31 != 0) {
        const z = (1.0 + x) * 0.5;
        const s = @sqrt(z);
        const w = rationalApproxBinary64(z) * s - pio2_lo;
        return 2 * (pio2_hi - (s + w));
    }

    // x > 0.5
    const z = (1.0 - x) * 0.5;
    const s = @sqrt(z);
    const df: f64 = @bitCast(@as(u64, @bitCast(s)) & 0xffff_ffff_0000_0000);
    const c = (z - df * df) / (s + df);
    const w = rationalApproxBinary64(z) * s + c;
    return 2.0 * (df + w);
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

fn acosExtended80(x: f80) f80 {
    const pio2_hi: f80 = 1.57079632679489661926;
    const pio2_lo: f80 = -2.50827880633416601173e-20;

    const hx: u80 = @bitCast(x);
    const se: u16 = @truncate(hx >> 64);
    const e = se & 0x7fff;

    // |x| >= 1 or nan
    if (e >= 0x3fff) {
        if (x == 1.0) {
            return 0.0;
        }
        if (x == -1.0) {
            return 2.0 * pio2_hi + 0x1p-120;
        }
        return 0.0 / (x - x);
    }
    // |x| < 0.5
    if (e < 0x3fff - 1) {
        if (e < 0x3fff - math.floatFractionalBits(f80)) {
            return pio2_hi + 0x1p-120;
        }
        return pio2_hi - (rationalApproxExtended80(x * x) * x - pio2_lo + x);
    }
    // x < -0.5
    if (se >> 15 != 0) {
        const z = (1 + x) * 0.5;
        const s = @sqrt(z);
        return 2.0 * (pio2_hi - (rationalApproxExtended80(z) * s - pio2_lo + s));
    }
    // x > 0.5
    const z = (1.0 - x) * 0.5;
    const s = @sqrt(z);
    const hs: u80 = @bitCast(s);
    const f: f80 = @bitCast(hs & 0xffff_ffff_ffff_0000_0000);
    const c = (z - f * f) / (s + f);
    return 2.0 * (rationalApproxExtended80(z) * s + c + f);
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

fn acosBinary128(x: f128) f128 {
    const pio2_hi: f128 = 1.57079632679489661923132169163975140;
    const pio2_lo: f128 = 4.33590506506189051239852201302167613e-35;

    const hx: u128 = @bitCast(x);
    const se: u16 = @truncate(hx >> 112);
    const e = se & 0x7fff;

    // |x| >= 1 or nan
    if (e >= 0x3fff) {
        if (x == 1.0) {
            return 0.0;
        }
        if (x == -1.0) {
            return 2 * pio2_hi + 0x1p-120;
        }
        return 0.0 / (x - x);
    }
    // |x| < 0.5
    if (e < 0x3fff - 1) {
        if (e < 0x3fff - math.floatFractionalBits(f128)) {
            return pio2_hi + 0x1p-120;
        }
        return pio2_hi - (rationalApproxBinary128(x * x) * x - pio2_lo + x);
    }
    // x < -0.5
    if (se >> 15 != 0) {
        const z = (1 + x) * 0.5;
        const s = @sqrt(z);
        return 2 * (pio2_hi - (rationalApproxBinary128(z) * s - pio2_lo + s));
    }
    // x > 0.5
    const z = (1.0 - x) * 0.5;
    const s = @sqrt(z);
    const hs: u128 = @bitCast(s);
    const f: f128 = @bitCast(hs & 0xffff_ffff_ffff_ffff_0000_0000_0000_0000);
    const c = (z - f * f) / (s + f);
    return 2.0 * (rationalApproxBinary128(z) * s + c + f);
}

test "acosBinary16.special" {
    try testing.expectApproxEqAbs(0x1.92p0, acosBinary16(0x0p+0), math.floatEpsAt(f16, 0x1.92p0));
    try testing.expectApproxEqAbs(0x1.92p1, acosBinary16(-0x1p+0), math.floatEpsAt(f16, 0x1.92p1));
    try testing.expectEqual(0x0p+0, acosBinary16(0x1p+0));
    try testing.expect(math.isNan(acosBinary16(0x1.004p0)));
    try testing.expect(math.isNan(acosBinary16(-0x1.004p0)));
    try testing.expect(math.isNan(acosBinary16(math.inf(f16))));
    try testing.expect(math.isNan(acosBinary16(-math.inf(f16))));
    try testing.expect(math.isNan(acosBinary16(math.nan(f16))));
}

test "acosBinary16" {
    try testing.expectApproxEqAbs(0x1.834p0, acosBinary16(0x1.db4p-5), math.floatEpsAt(f16, 0x1.834p0));
    try testing.expectApproxEqAbs(0x1.d48p0, acosBinary16(-0x1.068p-2), math.floatEpsAt(f16, 0x1.d48p0));
    try testing.expectApproxEqAbs(0x1.b7cp0, acosBinary16(-0x1.2c4p-3), math.floatEpsAt(f16, 0x1.b7cp0));
    try testing.expectApproxEqAbs(0x1.654p0, acosBinary16(0x1.65p-3), math.floatEpsAt(f16, 0x1.654p0));
    try testing.expectApproxEqAbs(0x1.6d8p-2, acosBinary16(0x1.dfcp-1), math.floatEpsAt(f16, 0x1.6d8p-2));
    try testing.expectApproxEqAbs(0x1.32p1, acosBinary16(-0x1.764p-1), math.floatEpsAt(f16, 0x1.32p1));
    try testing.expectApproxEqAbs(0x1.5b8p0, acosBinary16(0x1.b18p-3), math.floatEpsAt(f16, 0x1.5b8p0));
    try testing.expectApproxEqAbs(0x1.668p0, acosBinary16(0x1.5acp-3), math.floatEpsAt(f16, 0x1.668p0));
    try testi
```
