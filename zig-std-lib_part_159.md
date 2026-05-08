```
 are known to not work on MIPS.

const std = @import("std");
const builtin = @import("builtin");

pub fn suggestVectorLengthForCpu(comptime T: type, comptime cpu: std.Target.Cpu) ?comptime_int {
    @setEvalBranchQuota(2_000);

    // This is guesswork, if you have better suggestions can add it or edit the current here
    const element_bit_size = @max(8, std.math.ceilPowerOfTwo(u16, @bitSizeOf(T)) catch unreachable);
    const vector_bit_size: u16 = blk: {
        if (cpu.arch.isX86()) {
            if (T == bool and cpu.has(.x86, .prefer_mask_registers)) return 64;
            if (builtin.zig_backend != .stage2_x86_64 and cpu.has(.x86, .avx512f) and !cpu.hasAny(.x86, &.{ .prefer_256_bit, .prefer_128_bit })) break :blk 512;
            if (cpu.hasAny(.x86, &.{ .prefer_256_bit, .avx2 }) and !cpu.has(.x86, .prefer_128_bit)) break :blk 256;
            if (cpu.has(.x86, .sse)) break :blk 128;
            if (cpu.hasAny(.x86, &.{ .mmx, .@"3dnow" })) break :blk 64;
        } else if (cpu.arch.isArm()) {
            if (cpu.has(.arm, .neon)) break :blk 128;
        } else if (cpu.arch.isAARCH64()) {
            // NVIDIA Grace supports 128-bit SVE
            // AWS Graviton3 supports 256-bit SVE
            // Fujitsu A64FX supports 512-bit SVE
            // -> 256-bit seems like a good default for now.
            if (cpu.has(.aarch64, .sve)) break :blk 256;
            if (cpu.has(.aarch64, .neon)) break :blk 128;
        } else if (cpu.arch == .hexagon) {
            if (cpu.has(.hexagon, .hvx_length64b)) break :blk 512;
            if (cpu.has(.hexagon, .hvx)) break :blk 1024;
        } else if (cpu.arch.isLoongArch()) {
            if (cpu.has(.loongarch, .lasx)) break :blk 256;
            if (cpu.has(.loongarch, .lsx)) break :blk 128;
        } else if (cpu.arch.isMIPS()) {
            if (cpu.has(.mips, .msa)) break :blk 128;
            if (cpu.has(.mips, .mips3d)) break :blk 64;
        } else if (cpu.arch.isPowerPC()) {
            if (cpu.has(.powerpc, .vsx)) break :blk 128;
            if (cpu.has(.powerpc, .altivec)) break :blk 128;
        } else if (cpu.arch.isRISCV()) {
            // In RISC-V Vector Registers are length agnostic so there's no good way to determine the best size.
            // The usual vector length in most RISC-V cpus is 256 bits, however it can get to multiple kB.
            if (cpu.has(.riscv, .v)) {
                inline for (.{
                    .{ .zvl65536b, 65536 },
                    .{ .zvl32768b, 32768 },
                    .{ .zvl16384b, 16384 },
                    .{ .zvl8192b, 8192 },
                    .{ .zvl4096b, 4096 },
                    .{ .zvl2048b, 2048 },
                    .{ .zvl1024b, 1024 },
                    .{ .zvl512b, 512 },
                    .{ .zvl256b, 256 },
                    .{ .zvl128b, 128 },
                    .{ .zvl64b, 64 },
                    .{ .zvl32b, 32 },
                }) |mapping| {
                    if (cpu.has(.riscv, mapping[0])) break :blk mapping[1];
                }

                break :blk 256;
            }
        } else if (cpu.arch == .s390x) {
            if (cpu.has(.s390x, .vector)) break :blk 128;
        } else if (cpu.arch.isSPARC()) {
            if (cpu.hasAny(.sparc, &.{ .vis, .vis2, .vis3 })) break :blk 64;
        } else if (cpu.arch == .kvx) {
            break :blk 1024;
        } else if (cpu.arch == .ve) {
            if (cpu.has(.ve, .vpu)) break :blk 2048;
        } else if (cpu.arch.isWasm()) {
            if (cpu.has(.wasm, .simd128)) break :blk 128;
        }
        return null;
    };
    if (vector_bit_size <= element_bit_size) return null;

    return @divExact(vector_bit_size, element_bit_size);
}

/// Suggests a target-dependant vector length for a given type, or null if scalars are recommended.
/// Not yet implemented for every CPU architecture.
pub fn suggestVectorLength(comptime T: type) ?comptime_int {
    return suggestVectorLengthForCpu(T, builtin.cpu);
}

test "suggestVectorLengthForCpu works with signed and unsigned values" {
    comptime var cpu = std.Target.Cpu.baseline(std.Target.Cpu.Arch.x86_64, builtin.os);
    comptime cpu.features.addFeature(@intFromEnum(std.Target.x86.Feature.avx512f));
    comptime cpu.features.populateDependencies(&std.Target.x86.all_features);
    const expected_len: usize = switch (builtin.zig_backend) {
        .stage2_x86_64 => 8,
        else => 16,
    };
    const signed_integer_len = suggestVectorLengthForCpu(i32, cpu).?;
    const unsigned_integer_len = suggestVectorLengthForCpu(u32, cpu).?;
    try std.testing.expectEqual(expected_len, unsigned_integer_len);
    try std.testing.expectEqual(expected_len, signed_integer_len);
}

fn vectorLength(comptime VectorType: type) comptime_int {
    return switch (@typeInfo(VectorType)) {
        .vector => |info| info.len,
        .array => |info| info.len,
        else => @compileError("Invalid type " ++ @typeName(VectorType)),
    };
}

/// Returns the smallest type of unsigned ints capable of indexing any element within the given vector type.
pub fn VectorIndex(comptime VectorType: type) type {
    return std.math.IntFittingRange(0, vectorLength(VectorType) - 1);
}

/// Returns the smallest type of unsigned ints capable of holding the length of the given vector type.
pub fn VectorCount(comptime VectorType: type) type {
    return std.math.IntFittingRange(0, vectorLength(VectorType));
}

/// Returns a vector containing the first `len` integers in order from 0 to `len`-1.
/// For example, `iota(i32, 8)` will return a vector containing `.{0, 1, 2, 3, 4, 5, 6, 7}`.
pub inline fn iota(comptime T: type, comptime len: usize) @Vector(len, T) {
    comptime {
        var out: [len]T = undefined;
        for (&out, 0..) |*element, i| {
            element.* = switch (@typeInfo(T)) {
                .int => @as(T, @intCast(i)),
                .float => @as(T, @floatFromInt(i)),
                else => @compileError("Can't use type " ++ @typeName(T) ++ " in iota."),
            };
        }
        return @as(@Vector(len, T), out);
    }
}

/// Returns a vector containing the same elements as the input, but repeated until the desired length is reached.
/// For example, `repeat(8, [_]u32{1, 2, 3})` will return a vector containing `.{1, 2, 3, 1, 2, 3, 1, 2}`.
pub fn repeat(comptime len: usize, vec: anytype) @Vector(len, std.meta.Child(@TypeOf(vec))) {
    const Child = std.meta.Child(@TypeOf(vec));

    return @shuffle(Child, vec, undefined, iota(i32, len) % @as(@Vector(len, i32), @splat(@intCast(vectorLength(@TypeOf(vec))))));
}

/// Returns a vector containing all elements of the first vector at the lower indices followed by all elements of the second vector
/// at the higher indices.
pub fn join(a: anytype, b: anytype) @Vector(vectorLength(@TypeOf(a)) + vectorLength(@TypeOf(b)), std.meta.Child(@TypeOf(a))) {
    const Child = std.meta.Child(@TypeOf(a));
    const a_len = vectorLength(@TypeOf(a));
    const b_len = vectorLength(@TypeOf(b));

    return @shuffle(Child, a, b, @as([a_len]i32, iota(i32, a_len)) ++ @as([b_len]i32, ~iota(i32, b_len)));
}

/// Returns a vector whose elements alternates between those of each input vector.
/// For example, `interlace(.{[4]u32{11, 12, 13, 14}, [4]u32{21, 22, 23, 24}})` returns a vector containing `.{11, 21, 12, 22, 13, 23, 14, 24}`.
pub fn interlace(vecs: anytype) @Vector(vectorLength(@TypeOf(vecs[0])) * vecs.len, std.meta.Child(@TypeOf(vecs[0]))) {
    // interlace doesn't work on MIPS, for some reason.
    // Notes from earlier debug attempt:
    //  The indices are correct. The problem seems to be with the @shuffle builtin.
    //  On MIPS, the test that interlaces small_base gives { 0, 2, 0, 0, 64, 255, 248, 200, 0, 0 }.
    //  Calling this with two inputs seems to work fine, but I'll let the compile error trigger for all inputs, just to be safe.
    if (builtin.cpu.arch.isMIPS()) @compileError("TODO: Find out why interlace() doesn't work on MIPS");

    const VecType = @TypeOf(vecs[0]);
    const vecs_arr = @as([vecs.len]VecType, vecs);
    const Child = std.meta.Child(@TypeOf(vecs_arr[0]));

    if (vecs_arr.len == 1) return vecs_arr[0];

    const a_vec_count = (1 + vecs_arr.len) >> 1;
    const b_vec_count = vecs_arr.len >> 1;

    const a = interlace(@as(*const [a_vec_count]VecType, @ptrCast(vecs_arr[0..a_vec_count])).*);
    const b = interlace(@as(*const [b_vec_count]VecType, @ptrCast(vecs_arr[a_vec_count..])).*);

    const a_len = vectorLength(@TypeOf(a));
    const b_len = vectorLength(@TypeOf(b));
    const len = a_len + b_len;

    const indices = comptime blk: {
        const Vi32 = @Vector(len, i32);
        const count_up = iota(i32, len);
        const cycle = @divFloor(count_up, @as(Vi32, @splat(@intCast(vecs_arr.len))));
        const select_mask = repeat(len, join(@as(@Vector(a_vec_count, bool), @splat(true)), @as(@Vector(b_vec_count, bool), @splat(false))));
        const a_indices = count_up - cycle * @as(Vi32, @splat(@intCast(b_vec_count)));
        const b_indices = shiftElementsRight(count_up - cycle * @as(Vi32, @splat(@intCast(a_vec_count))), a_vec_count, 0);
        break :blk @select(i32, select_mask, a_indices, ~b_indices);
    };

    return @shuffle(Child, a, b, indices);
}

/// The contents of `interlaced` is evenly split between vec_count vectors that are returned as an array. They "take turns",
/// receiving one element from `interlaced` at a time.
pub fn deinterlace(
    comptime vec_count: usize,
    interlaced: anytype,
) [vec_count]@Vector(
    vectorLength(@TypeOf(interlaced)) / vec_count,
    std.meta.Child(@TypeOf(interlaced)),
) {
    const vec_len = vectorLength(@TypeOf(interlaced)) / vec_count;
    const Child = std.meta.Child(@TypeOf(interlaced));

    var out: [vec_count]@Vector(vec_len, Child) = undefined;

    comptime var i: usize = 0; // for-loops don't work for this, apparently.
    inline while (i < out.len) : (i += 1) {
        const indices = comptime iota(i32, vec_len) * @as(@Vector(vec_len, i32), @splat(@intCast(vec_count))) + @as(@Vector(vec_len, i32), @splat(@intCast(i)));
        out[i] = @shuffle(Child, interlaced, undefined, indices);
    }

    return out;
}

pub fn extract(
    vec: anytype,
    comptime first: VectorIndex(@TypeOf(vec)),
    comptime count: VectorCount(@TypeOf(vec)),
) @Vector(count, std.meta.Child(@TypeOf(vec))) {
    const Child = std.meta.Child(@TypeOf(vec));
    const len = vectorLength(@TypeOf(vec));

    std.debug.assert(@as(comptime_int, @intCast(first)) + @as(comptime_int, @intCast(count)) <= len);

    return @shuffle(Child, vec, undefined, iota(i32, count) + @as(@Vector(count, i32), @splat(@intCast(first))));
}

test "vector patterns" {
    if (builtin.cpu.arch == .hexagon) return error.SkipZigTest;

    const base = @Vector(4, u32){ 10, 20, 30, 40 };
    const other_base = @Vector(4, u32){ 55, 66, 77, 88 };

    const small_bases = [5]@Vector(2, u8){
        @Vector(2, u8){ 0, 1 },
        @Vector(2, u8){ 2, 3 },
        @Vector(2, u8){ 4, 5 },
        @Vector(2, u8){ 6, 7 },
        @Vector(2, u8){ 8, 9 },
    };

    try std.testing.expectEqual([6]u32{ 10, 20, 30, 40, 10, 20 }, repeat(6, base));
    try std.testing.expectEqual([8]u32{ 10, 20, 30, 40, 55, 66, 77, 88 }, join(base, other_base));
    try std.testing.expectEqual([2]u32{ 20, 30 }, extract(base, 1, 2));

    if (!builtin.cpu.arch.isMIPS()) {
        try std.testing.expectEqual([8]u32{ 10, 55, 20, 66, 30, 77, 40, 88 }, interlace(.{ base, other_base }));

        const small_braid = interlace(small_bases);
        try std.testing.expectEqual([10]u8{ 0, 2, 4, 6, 8, 1, 3, 5, 7, 9 }, small_braid);
        try std.testing.expectEqual(small_bases, deinterlace(small_bases.len, small_braid));
    }
}

/// Joins two vectors, shifts them leftwards (towards lower indices) and extracts the leftmost elements into a vector the length of a and b.
pub fn mergeShift(a: anytype, b: anytype, comptime shift: VectorCount(@TypeOf(a, b))) @TypeOf(a, b) {
    const len = vectorLength(@TypeOf(a, b));

    return extract(join(a, b), shift, len);
}

/// Elements are shifted rightwards (towards higher indices). New elements are added to the left, and the rightmost elements are cut off
/// so that the length of the vector stays the same.
pub fn shiftElementsRight(vec: anytype, comptime amount: VectorCount(@TypeOf(vec)), shift_in: std.meta.Child(@TypeOf(vec))) @TypeOf(vec) {
    // It may be possible to implement shifts and rotates with a runtime-friendly slice of two joined vectors, as the length of the
    // slice would be comptime-known. This would permit vector shifts and rotates by a non-comptime-known amount.
    // However, I am unsure whether compiler optimizations would handle that well enough on all platforms.
    const V = @TypeOf(vec);
    const len = vectorLength(V);

    return mergeShift(@as(V, @splat(shift_in)), vec, len - amount);
}

/// Elements are shifted leftwards (towards lower indices). New elements are added to the right, and the leftmost elements are cut off
/// so that no elements with indices below 0 remain.
pub fn shiftElementsLeft(vec: anytype, comptime amount: VectorCount(@TypeOf(vec)), shift_in: std.meta.Child(@TypeOf(vec))) @TypeOf(vec) {
    const V = @TypeOf(vec);

    return mergeShift(vec, @as(V, @splat(shift_in)), amount);
}

/// Elements are shifted leftwards (towards lower indices). Elements that leave to the left will reappear to the right in the same order.
pub fn rotateElementsLeft(vec: anytype, comptime amount: VectorCount(@TypeOf(vec))) @TypeOf(vec) {
    return mergeShift(vec, vec, amount);
}

/// Elements are shifted rightwards (towards higher indices). Elements that leave to the right will reappear to the left in the same order.
pub fn rotateElementsRight(vec: anytype, comptime amount: VectorCount(@TypeOf(vec))) @TypeOf(vec) {
    return rotateElementsLeft(vec, vectorLength(@TypeOf(vec)) - amount);
}

pub fn reverseOrder(vec: anytype) @TypeOf(vec) {
    const Child = std.meta.Child(@TypeOf(vec));
    const len = vectorLength(@TypeOf(vec));

    return @shuffle(Child, vec, undefined, @as(@Vector(len, i32), @splat(@as(i32, @intCast(len)) - 1)) - iota(i32, len));
}

test "vector shifting" {
    const base = @Vector(4, u32){ 10, 20, 30, 40 };

    try std.testing.expectEqual([4]u32{ 30, 40, 999, 999 }, shiftElementsLeft(base, 2, 999));
    try std.testing.expectEqual([4]u32{ 999, 999, 10, 20 }, shiftElementsRight(base, 2, 999));
    try std.testing.expectEqual([4]u32{ 20, 30, 40, 10 }, rotateElementsLeft(base, 1));
    try std.testing.expectEqual([4]u32{ 40, 10, 20, 30 }, rotateElementsRight(base, 1));
    try std.testing.expectEqual([4]u32{ 40, 30, 20, 10 }, reverseOrder(base));
}

pub fn firstTrue(vec: anytype) ?VectorIndex(@TypeOf(vec)) {
    const len = vectorLength(@TypeOf(vec));
    const IndexInt = VectorIndex(@TypeOf(vec));

    if (!@reduce(.Or, vec)) {
        return null;
    }
    const all_max: @Vector(len, IndexInt) = @splat(~@as(IndexInt, 0));
    const indices = @select(IndexInt, vec, iota(IndexInt, len), all_max);
    return @reduce(.Min, indices);
}

pub fn lastTrue(vec: anytype) ?VectorIndex(@TypeOf(vec)) {
    const len = vectorLength(@TypeOf(vec));
    const IndexInt = VectorIndex(@TypeOf(vec));

    if (!@reduce(.Or, vec)) {
        return null;
    }

    const all_zeroes: @Vector(len, IndexInt) = @splat(0);
    const indices = @select(IndexInt, vec, iota(IndexInt, len), all_zeroes);
    return @reduce(.Max, indices);
}

pub fn countTrues(vec: anytype) VectorCount(@TypeOf(vec)) {
    const len = vectorLength(@TypeOf(vec));
    const CountIntType = VectorCount(@TypeOf(vec));

    const all_ones: @Vector(len, CountIntType) = @splat(1);
    const all_zeroes: @Vector(len, CountIntType) = @splat(0);

    const one_if_true = @select(CountIntType, vec, all_ones, all_zeroes);
    return @reduce(.Add, one_if_true);
}

pub fn firstIndexOfValue(vec: anytype, value: std.meta.Child(@TypeOf(vec))) ?VectorIndex(@TypeOf(vec)) {
    const V = @TypeOf(vec);

    return firstTrue(vec == @as(V, @splat(value)));
}

pub fn lastIndexOfValue(vec: anytype, value: std.meta.Child(@TypeOf(vec))) ?VectorIndex(@TypeOf(vec)) {
    const V = @TypeOf(vec);

    return lastTrue(vec == @as(V, @splat(value)));
}

pub fn countElementsWithValue(vec: anytype, value: std.meta.Child(@TypeOf(vec))) VectorCount(@TypeOf(vec)) {
    const V = @TypeOf(vec);

    return countTrues(vec == @as(V, @splat(value)));
}

test "vector searching" {
    const base = @Vector(8, u32){ 6, 4, 7, 4, 4, 2, 3, 7 };

    try std.testing.expectEqual(@as(?u3, 1), firstIndexOfValue(base, 4));
    try std.testing.expectEqual(@as(?u3, 4), lastIndexOfValue(base, 4));
    try std.testing.expectEqual(@as(?u3, null), lastIndexOfValue(base, 99));
    try std.testing.expectEqual(@as(u4, 3), countElementsWithValue(base, 4));
}

/// Same as prefixScan, but with a user-provided, mathematically associative function.
pub fn prefixScanWithFunc(
    comptime hop: isize,
    vec: anytype,
    /// The error type that `func` might return. Set this to `void` if `func` doesn't return an error union.
    comptime ErrorType: type,
    comptime func: fn (@TypeOf(vec), @TypeOf(vec)) if (ErrorType == void) @TypeOf(vec) else ErrorType!@TypeOf(vec),
    /// When one operand of the operation performed by `func` is this value, the result must equal the other operand.
    /// For example, this should be 0 for addition or 1 for multiplication.
    comptime identity: std.meta.Child(@TypeOf(vec)),
) if (ErrorType == void) @TypeOf(vec) else ErrorType!@TypeOf(vec) {
    // I haven't debugged this, but it might be a cousin of sorts to what's going on with interlace.
    if (builtin.cpu.arch.isMIPS()) @compileError("TODO: Find out why prefixScan doesn't work on MIPS");

    const len = vectorLength(@TypeOf(vec));

    if (hop == 0) @compileError("hop can not be 0; you'd be going nowhere forever!");
    const abs_hop = if (hop < 0) -hop else hop;

    var acc = vec;
    comptime var i = 0;
    inline while ((abs_hop << i) < len) : (i += 1) {
        const shifted = if (hop < 0) shiftElementsLeft(acc, abs_hop << i, identity) else shiftElementsRight(acc, abs_hop << i, identity);

        acc = if (ErrorType == void) func(acc, shifted) else try func(acc, shifted);
    }
    return acc;
}

/// Returns a vector whose elements are the result of performing the specified operation on the corresponding
/// element of the input vector and every hop'th element that came before it (or after, if hop is negative).
/// Supports the same operations as the @reduce() builtin. Takes O(logN) to compute.
/// The scan is not linear, which may affect floating point errors. This may affect the determinism of
/// algorithms that use this function.
pub fn prefixScan(comptime op: std.builtin.ReduceOp, comptime hop: isize, vec: anytype) @TypeOf(vec) {
    const VecType = @TypeOf(vec);
    const Child = std.meta.Child(VecType);

    const identity = comptime switch (@typeInfo(Child)) {
        .bool => switch (op) {
            .Or, .Xor => false,
            .And => true,
            else => @compileError("Invalid prefixScan operation " ++ @tagName(op) ++ " for vector of booleans."),
        },
        .int => switch (op) {
            .Max => std.math.minInt(Child),
            .Add, .Or, .Xor => 0,
            .Mul => 1,
            .And, .Min => std.math.maxInt(Child),
        },
        .float => switch (op) {
            .Max => -std.math.inf(Child),
            .Add => 0,
            .Mul => 1,
            .Min => std.math.inf(Child),
            else => @compileError("Invalid prefixScan operation " ++ @tagName(op) ++ " for vector of floats."),
        },
        else => @compileError("Invalid type " ++ @typeName(VecType) ++ " for prefixScan."),
    };

    const fn_container = struct {
        fn opFn(a: VecType, b: VecType) VecType {
            return if (Child == bool) switch (op) {
                .And => @select(bool, a, b, @as(VecType, @splat(false))),
                .Or => @select(bool, a, @as(VecType, @splat(true)), b),
                .Xor => a != b,
                else => unreachable,
            } else switch (op) {
                .And => a & b,
                .Or => a | b,
                .Xor => a ^ b,
                .Add => a + b,
                .Mul => a * b,
                .Min => @min(a, b),
                .Max => @max(a, b),
            };
        }
    };

    return prefixScanWithFunc(hop, vec, void, fn_container.opFn, identity);
}

test "vector prefix scan" {
    if (builtin.cpu.arch == .aarch64_be and builtin.zig_backend == .stage2_llvm) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/21893
    if (builtin.zig_backend == .stage2_llvm and builtin.cpu.arch == .hexagon) return error.SkipZigTest;

    if (builtin.cpu.arch.isMIPS()) return error.SkipZigTest;

    const int_base = @Vector(4, i32){ 11, 23, 9, -21 };
    const float_base = @Vector(4, f32){ 2, 0.5, -10, 6.54321 };
    const bool_base = @Vector(4, bool){ true, false, true, false };

    const ones: @Vector(32, u8) = @splat(1);

    try std.testing.expectEqual(iota(u8, 32) + ones, prefixScan(.Add, 1, ones));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 3, 1, 1 }, prefixScan(.And, 1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 31, 31, -1 }, prefixScan(.Or, 1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 28, 21, -2 }, prefixScan(.Xor, 1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 34, 43, 22 }, prefixScan(.Add, 1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 253, 2277, -47817 }, prefixScan(.Mul, 1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 11, 9, -21 }, prefixScan(.Min, 1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 23, 23, 23 }, prefixScan(.Max, 1, int_base));

    // Trying to predict all inaccuracies when adding and multiplying floats with prefixScans would be a mess, so we don't test those.
    try std.testing.expectEqual(@Vector(4, f32){ 2, 0.5, -10, -10 }, prefixScan(.Min, 1, float_base));
    try std.testing.expectEqual(@Vector(4, f32){ 2, 2, 2, 6.54321 }, prefixScan(.Max, 1, float_base));

    try std.testing.expectEqual(@Vector(4, bool){ true, true, false, false }, prefixScan(.Xor, 1, bool_base));
    try std.testing.expectEqual(@Vector(4, bool){ true, true, true, true }, prefixScan(.Or, 1, bool_base));
    try std.testing.expectEqual(@Vector(4, bool){ true, false, false, false }, prefixScan(.And, 1, bool_base));

    try std.testing.expectEqual(@Vector(4, i32){ 11, 23, 20, 2 }, prefixScan(.Add, 2, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 22, 11, -12, -21 }, prefixScan(.Add, -1, int_base));
    try std.testing.expectEqual(@Vector(4, i32){ 11, 23, 9, -10 }, prefixScan(.Add, 3, int_base));
}



---
File: /std/SinglyLinkedList.zig
---

//! A singly-linked list is headed by a single forward pointer. The elements
//! are singly-linked for minimum space and pointer manipulation overhead at
//! the expense of O(n) removal for arbitrary elements. New elements can be
//! added to the list after an existing element or at the head of the list.
//!
//! A singly-linked list may only be traversed in the forward direction.
//!
//! Singly-linked lists are useful under these conditions:
//! * Ability to preallocate elements / requirement of infallibility for
//!   insertion.
//! * Ability to allocate elements intrusively along with other data.
//! * Homogenous elements.

const std = @import("std.zig");
const debug = std.debug;
const assert = debug.assert;
const testing = std.testing;
const SinglyLinkedList = @This();

first: ?*Node = null,

/// This struct contains only a next pointer and not any data payload. The
/// intended usage is to embed it intrusively into another data structure and
/// access the data with `@fieldParentPtr`.
pub const Node = struct {
    next: ?*Node = null,

    pub fn insertAfter(node: *Node, new_node: *Node) void {
        new_node.next = node.next;
        node.next = new_node;
    }

    /// Remove the node after the one provided, returning it.
    pub fn removeNext(node: *Node) ?*Node {
        const next_node = node.next orelse return null;
        node.next = next_node.next;
        return next_node;
    }

    /// Iterate over the singly-linked list from this node, until the final
    /// node is found.
    ///
    /// This operation is O(N). Instead of calling this function, consider
    /// using a different data structure.
    pub fn findLast(node: *Node) *Node {
        var it = node;
        while (true) {
            it = it.next orelse return it;
        }
    }

    /// Iterate over each next node, returning the count of all nodes except
    /// the starting one.
    ///
    /// This operation is O(N). Instead of calling this function, consider
    /// using a different data structure.
    pub fn countChildren(node: *const Node) usize {
        var count: usize = 0;
        var it: ?*const Node = node.next;
        while (it) |n| : (it = n.next) {
            count += 1;
        }
        return count;
    }

    /// Reverse the list starting from this node in-place.
    ///
    /// This operation is O(N). Instead of calling this function, consider
    /// using a different data structure.
    pub fn reverse(indirect: *?*Node) void {
        if (indirect.* == null) {
            return;
        }
        var current: *Node = indirect.*.?;
        while (current.next) |next| {
            current.next = next.next;
            next.next = indirect.*;
            indirect.* = next;
        }
    }
};

pub fn prepend(list: *SinglyLinkedList, new_node: *Node) void {
    new_node.next = list.first;
    list.first = new_node;
}

/// Remove `node` from the list.
/// Asserts that `node` is in the list.
pub fn remove(list: *SinglyLinkedList, node: *Node) void {
    if (list.first == node) {
        list.first = node.next;
    } else {
        var current_elm = list.first.?;
        while (current_elm.next != node) {
            current_elm = current_elm.next.?;
        }
        current_elm.next = node.next;
    }
}

/// Remove and return the first node in the list.
pub fn popFirst(list: *SinglyLinkedList) ?*Node {
    const first = list.first orelse return null;
    list.first = first.next;
    return first;
}

/// Iterate over all nodes, returning the count.
///
/// This operation is O(N). Consider tracking the length separately rather than
/// computing it.
pub fn len(list: SinglyLinkedList) usize {
    if (list.first) |n| {
        return 1 + n.countChildren();
    } else {
        return 0;
    }
}

test "basics" {
    const L = struct {
        data: u32,
        node: SinglyLinkedList.Node = .{},
    };
    var list: SinglyLinkedList = .{};

    try testing.expect(list.len() == 0);

    var one: L = .{ .data = 1 };
    var two: L = .{ .data = 2 };
    var three: L = .{ .data = 3 };
    var four: L = .{ .data = 4 };
    var five: L = .{ .data = 5 };

    list.prepend(&two.node); // {2}
    two.node.insertAfter(&five.node); // {2, 5}
    list.prepend(&one.node); // {1, 2, 5}
    two.node.insertAfter(&three.node); // {1, 2, 3, 5}
    three.node.insertAfter(&four.node); // {1, 2, 3, 4, 5}

    try testing.expect(list.len() == 5);

    // Traverse forwards.
    {
        var it = list.first;
        var index: u32 = 1;
        while (it) |node| : (it = node.next) {
            const l: *L = @fieldParentPtr("node", node);
            try testing.expect(l.data == index);
            index += 1;
        }
    }

    _ = list.popFirst(); // {2, 3, 4, 5}
    _ = list.remove(&five.node); // {2, 3, 4}
    _ = two.node.removeNext(); // {2, 4}

    try testing.expect(@as(*L, @fieldParentPtr("node", list.first.?)).data == 2);
    try testing.expect(@as(*L, @fieldParentPtr("node", list.first.?.next.?)).data == 4);
    try testing.expect(list.first.?.next.?.next == null);

    SinglyLinkedList.Node.reverse(&list.first);

    try testing.expect(@as(*L, @fieldParentPtr("node", list.first.?)).data == 4);
    try testing.expect(@as(*L, @fieldParentPtr("node", list.first.?.next.?)).data == 2);
    try testing.expect(list.first.?.next.?.next == null);
}



---
File: /std/sort.zig
---

const std = @import("std.zig");
const assert = std.debug.assert;
const testing = std.testing;
const mem = std.mem;
const math = std.math;

pub const Mode = enum { stable, unstable };

pub const block = @import("sort/block.zig").block;
pub const pdq = @import("sort/pdq.zig").pdq;
pub const pdqContext = @import("sort/pdq.zig").pdqContext;

/// Stable in-place sort. O(n) best case, O(pow(n, 2)) worst case.
/// O(1) memory (no allocator required).
/// Sorts in ascending order with respect to the given `lessThan` function.
pub fn insertion(
    comptime T: type,
    items: []T,
    context: anytype,
    comptime lessThanFn: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    const Context = struct {
        items: []T,
        sub_ctx: @TypeOf(context),

        pub fn lessThan(ctx: @This(), a: usize, b: usize) bool {
            return lessThanFn(ctx.sub_ctx, ctx.items[a], ctx.items[b]);
        }

        pub fn swap(ctx: @This(), a: usize, b: usize) void {
            return mem.swap(T, &ctx.items[a], &ctx.items[b]);
        }
    };
    insertionContext(0, items.len, Context{ .items = items, .sub_ctx = context });
}

/// Stable in-place sort. O(n) best case, O(pow(n, 2)) worst case.
/// O(1) memory (no allocator required).
/// `context` must have methods `swap` and `lessThan`,
/// which each take 2 `usize` parameters indicating the index of an item.
/// Sorts in ascending order with respect to `lessThan`.
pub fn insertionContext(a: usize, b: usize, context: anytype) void {
    assert(a <= b);

    var i = a + 1;
    while (i < b) : (i += 1) {
        var j = i;
        while (j > a and context.lessThan(j, j - 1)) : (j -= 1) {
            context.swap(j, j - 1);
        }
    }
}

/// Unstable in-place sort. O(n*log(n)) best case, worst case and average case.
/// O(1) memory (no allocator required).
/// Sorts in ascending order with respect to the given `lessThan` function.
pub fn heap(
    comptime T: type,
    items: []T,
    context: anytype,
    comptime lessThanFn: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    const Context = struct {
        items: []T,
        sub_ctx: @TypeOf(context),

        pub fn lessThan(ctx: @This(), a: usize, b: usize) bool {
            return lessThanFn(ctx.sub_ctx, ctx.items[a], ctx.items[b]);
        }

        pub fn swap(ctx: @This(), a: usize, b: usize) void {
            return mem.swap(T, &ctx.items[a], &ctx.items[b]);
        }
    };
    heapContext(0, items.len, Context{ .items = items, .sub_ctx = context });
}

/// Unstable in-place sort. O(n*log(n)) best case, worst case and average case.
/// O(1) memory (no allocator required).
/// `context` must have methods `swap` and `lessThan`,
/// which each take 2 `usize` parameters indicating the index of an item.
/// Sorts in ascending order with respect to `lessThan`.
pub fn heapContext(a: usize, b: usize, context: anytype) void {
    assert(a <= b);
    // build the heap in linear time.
    var i = a + (b - a) / 2;
    while (i > a) {
        i -= 1;
        siftDown(a, i, b, context);
    }

    // pop maximal elements from the heap.
    i = b;
    while (i > a) {
        i -= 1;
        context.swap(a, i);
        siftDown(a, a, i, context);
    }
}

fn siftDown(a: usize, target: usize, b: usize, context: anytype) void {
    var cur = target;
    while (true) {
        // When we don't overflow from the multiply below, the following expression equals (2*cur) - (2*a) + a + 1
        // The `+ a + 1` is safe because:
        //  for `a > 0` then `2a >= a + 1`.
        //  for `a = 0`, the expression equals `2*cur+1`. `2*cur` is an even number, therefore adding 1 is safe.
        var child = (math.mul(usize, cur - a, 2) catch break) + a + 1;

        // stop if we overshot the boundary
        if (!(child < b)) break;

        // `next_child` is at most `b`, therefore no overflow is possible
        const next_child = child + 1;

        // store the greater child in `child`
        if (next_child < b and context.lessThan(child, next_child)) {
            child = next_child;
        }

        // stop if the Heap invariant holds at `cur`.
        if (context.lessThan(child, cur)) break;

        // swap `cur` with the greater child,
        // move one step down, and continue sifting.
        context.swap(child, cur);
        cur = child;
    }
}

/// Use to generate a comparator function for a given type. e.g. `sort(u8, slice, {}, asc(u8))`.
pub fn asc(comptime T: type) fn (void, T, T) bool {
    return struct {
        pub fn inner(_: void, a: T, b: T) bool {
            return a < b;
        }
    }.inner;
}

/// Use to generate a comparator function for a given type. e.g. `sort(u8, slice, {}, desc(u8))`.
pub fn desc(comptime T: type) fn (void, T, T) bool {
    return struct {
        pub fn inner(_: void, a: T, b: T) bool {
            return a > b;
        }
    }.inner;
}

const asc_u8 = asc(u8);
const asc_i32 = asc(i32);
const desc_u8 = desc(u8);
const desc_i32 = desc(i32);

const sort_funcs = &[_]fn (comptime type, anytype, anytype, comptime anytype) void{
    block,
    pdq,
    insertion,
    heap,
};

const context_sort_funcs = &[_]fn (usize, usize, anytype) void{
    // blockContext,
    pdqContext,
    insertionContext,
    heapContext,
};

const IdAndValue = struct {
    id: usize,
    value: i32,

    fn lessThan(context: void, a: IdAndValue, b: IdAndValue) bool {
        _ = context;
        return a.value < b.value;
    }
};

test "stable sort" {
    const expected = [_]IdAndValue{
        IdAndValue{ .id = 0, .value = 0 },
        IdAndValue{ .id = 1, .value = 0 },
        IdAndValue{ .id = 2, .value = 0 },
        IdAndValue{ .id = 0, .value = 1 },
        IdAndValue{ .id = 1, .value = 1 },
        IdAndValue{ .id = 2, .value = 1 },
        IdAndValue{ .id = 0, .value = 2 },
        IdAndValue{ .id = 1, .value = 2 },
        IdAndValue{ .id = 2, .value = 2 },
    };

    var cases = [_][9]IdAndValue{
        [_]IdAndValue{
            IdAndValue{ .id = 0, .value = 0 },
            IdAndValue{ .id = 0, .value = 1 },
            IdAndValue{ .id = 0, .value = 2 },
            IdAndValue{ .id = 1, .value = 0 },
            IdAndValue{ .id = 1, .value = 1 },
            IdAndValue{ .id = 1, .value = 2 },
            IdAndValue{ .id = 2, .value = 0 },
            IdAndValue{ .id = 2, .value = 1 },
            IdAndValue{ .id = 2, .value = 2 },
        },
        [_]IdAndValue{
            IdAndValue{ .id = 0, .value = 2 },
            IdAndValue{ .id = 0, .value = 1 },
            IdAndValue{ .id = 0, .value = 0 },
            IdAndValue{ .id = 1, .value = 2 },
            IdAndValue{ .id = 1, .value = 1 },
            IdAndValue{ .id = 1, .value = 0 },
            IdAndValue{ .id = 2, .value = 2 },
            IdAndValue{ .id = 2, .value = 1 },
            IdAndValue{ .id = 2, .value = 0 },
        },
    };

    for (&cases) |*case| {
        block(IdAndValue, (case.*)[0..], {}, IdAndValue.lessThan);
        for (case.*, 0..) |item, i| {
            try testing.expect(item.id == expected[i].id);
            try testing.expect(item.value == expected[i].value);
        }
    }
}

test "stable sort fuzz testing" {
    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();
    const test_case_count = 10;

    for (0..test_case_count) |_| {
        const array_size = random.intRangeLessThan(usize, 0, 1000);
        const array = try testing.allocator.alloc(IdAndValue, array_size);
        defer testing.allocator.free(array);
        // Value is a small random numbers to create collisions.
        // Id is a  reverse index to make sure sorting function only uses provided `lessThan`.
        for (array, 0..) |*item, index| {
            item.* = .{
                .value = random.intRangeLessThan(i32, 0, 100),
                .id = array_size - index,
            };
        }
        block(IdAndValue, array, {}, IdAndValue.lessThan);
        if (array_size > 0) {
            for (array[0 .. array_size - 1], array[1..]) |x, y| {
                try testing.expect(x.value <= y.value);
                if (x.value == y.value) {
                    try testing.expect(x.id > y.id);
                }
            }
        }
    }
}

test "sort" {
    const u8cases = [_][]const []const u8{
        &[_][]const u8{
            "",
            "",
        },
        &[_][]const u8{
            "a",
            "a",
        },
        &[_][]const u8{
            "az",
            "az",
        },
        &[_][]const u8{
            "za",
            "az",
        },
        &[_][]const u8{
            "asdf",
            "adfs",
        },
        &[_][]const u8{
            "one",
            "eno",
        },
    };

    const i32cases = [_][]const []const i32{
        &[_][]const i32{
            &[_]i32{},
            &[_]i32{},
        },
        &[_][]const i32{
            &[_]i32{1},
            &[_]i32{1},
        },
        &[_][]const i32{
            &[_]i32{ 0, 1 },
            &[_]i32{ 0, 1 },
        },
        &[_][]const i32{
            &[_]i32{ 1, 0 },
            &[_]i32{ 0, 1 },
        },
        &[_][]const i32{
            &[_]i32{ 1, -1, 0 },
            &[_]i32{ -1, 0, 1 },
        },
        &[_][]const i32{
            &[_]i32{ 2, 1, 3 },
            &[_]i32{ 1, 2, 3 },
        },
        &[_][]const i32{
            &[_]i32{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 55, 32, 39, 58, 21, 88, 43, 22, 59 },
            &[_]i32{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 21, 22, 32, 39, 43, 55, 58, 59, 88 },
        },
    };

    inline for (sort_funcs) |sortFn| {
        for (u8cases) |case| {
            var buf: [20]u8 = undefined;
            const slice = buf[0..case[0].len];
            @memcpy(slice, case[0]);
            sortFn(u8, slice, {}, asc_u8);
            try testing.expect(mem.eql(u8, slice, case[1]));
        }

        for (i32cases) |case| {
            var buf: [20]i32 = undefined;
            const slice = buf[0..case[0].len];
            @memcpy(slice, case[0]);
            sortFn(i32, slice, {}, asc_i32);
            try testing.expect(mem.eql(i32, slice, case[1]));
        }
    }
}

test "sort descending" {
    const rev_cases = [_][]const []const i32{
        &[_][]const i32{
            &[_]i32{},
            &[_]i32{},
        },
        &[_][]const i32{
            &[_]i32{1},
            &[_]i32{1},
        },
        &[_][]const i32{
            &[_]i32{ 0, 1 },
            &[_]i32{ 1, 0 },
        },
        &[_][]const i32{
            &[_]i32{ 1, 0 },
            &[_]i32{ 1, 0 },
        },
        &[_][]const i32{
            &[_]i32{ 1, -1, 0 },
            &[_]i32{ 1, 0, -1 },
        },
        &[_][]const i32{
            &[_]i32{ 2, 1, 3 },
            &[_]i32{ 3, 2, 1 },
        },
    };

    inline for (sort_funcs) |sortFn| {
        for (rev_cases) |case| {
            var buf: [8]i32 = undefined;
            const slice = buf[0..case[0].len];
            @memcpy(slice, case[0]);
            sortFn(i32, slice, {}, desc_i32);
            try testing.expect(mem.eql(i32, slice, case[1]));
        }
    }
}

test "sort with context in the middle of a slice" {
    const Context = struct {
        items: []i32,

        pub fn lessThan(ctx: @This(), a: usize, b: usize) bool {
            return ctx.items[a] < ctx.items[b];
        }

        pub fn swap(ctx: @This(), a: usize, b: usize) void {
            return mem.swap(i32, &ctx.items[a], &ctx.items[b]);
        }
    };

    const i32cases = [_][]const []const i32{
        &[_][]const i32{
            &[_]i32{ 0, 1, 8, 3, 6, 5, 4, 2, 9, 7, 10, 55, 32, 39, 58, 21, 88, 43, 22, 59 },
            &[_]i32{ 50, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 21, 22, 32, 39, 43, 55, 58, 59, 88 },
        },
    };

    const ranges = [_]struct { start: usize, end: usize }{
        .{ .start = 10, .end = 20 },
        .{ .start = 1, .end = 11 },
        .{ .start = 3, .end = 7 },
    };

    inline for (context_sort_funcs) |sortFn| {
        for (i32cases) |case| {
            for (ranges) |range| {
                var buf: [20]i32 = undefined;
                const slice = buf[0..case[0].len];
                @memcpy(slice, case[0]);
                sortFn(range.start, range.end, Context{ .items = slice });
                try testing.expectEqualSlices(i32, case[1][range.start..range.end], slice[range.start..range.end]);
            }
        }
    }
}

test "sort fuzz testing" {
    var prng = std.Random.DefaultPrng.init(std.testing.random_seed);
    const random = prng.random();
    const test_case_count = 10;

    inline for (sort_funcs) |sortFn| {
        for (0..test_case_count) |_| {
            const array_size = random.intRangeLessThan(usize, 0, 1000);
            const array = try testing.allocator.alloc(i32, array_size);
            defer testing.allocator.free(array);
            // populate with random data
            for (array) |*item| {
                item.* = random.intRangeLessThan(i32, 0, 100);
            }
            sortFn(i32, array, {}, asc_i32);
            try testing.expect(isSorted(i32, array, {}, asc_i32));
        }
    }
}

/// Returns the index of an element in `items` returning `.eq` when given to `compareFn`.
/// - If there are multiple such elements, returns the index of any one of them.
/// - If there are no such elements, returns `null`.
///
/// `items` must be sorted in ascending order with respect to `compareFn`:
/// ```
/// [0]                                                   [len]
/// ┌───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┐
/// │.lt│.lt│ \ \ │.lt│.eq│.eq│ \ \ │.eq│.gt│.gt│ \ \ │.gt│
/// └───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┘
/// ├─────────────────┼─────────────────┼─────────────────┤
///  ↳ zero or more    ↳ zero or more    ↳ zero or more
///                   ├─────────────────┤
///                    ↳ if not null, returned
///                      index is in this range
/// ```
///
/// `O(log n)` time complexity.
///
/// See also: `lowerBound, `upperBound`, `partitionPoint`, `equalRange`.
pub fn binarySearch(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime compareFn: fn (@TypeOf(context), T) std.math.Order,
) ?usize {
    var low: usize = 0;
    var high: usize = items.len;

    while (low < high) {
        // Avoid overflowing in the midpoint calculation
        const mid = low + (high - low) / 2;
        switch (compareFn(context, items[mid])) {
            .eq => return mid,
            .gt => low = mid + 1,
            .lt => high = mid,
        }
    }
    return null;
}

test binarySearch {
    const S = struct {
        fn orderU32(context: u32, item: u32) std.math.Order {
            return std.math.order(context, item);
        }
        fn orderI32(context: i32, item: i32) std.math.Order {
            return std.math.order(context, item);
        }
        fn orderLength(context: usize, item: []const u8) std.math.Order {
            return std.math.order(context, item.len);
        }
    };
    const R = struct {
        b: i32,
        e: i32,

        fn r(b: i32, e: i32) @This() {
            return .{ .b = b, .e = e };
        }

        fn order(context: i32, item: @This()) std.math.Order {
            if (context < item.b) {
                return .lt;
            } else if (context > item.e) {
                return .gt;
            } else {
                return .eq;
            }
        }
    };

    try std.testing.expectEqual(null, binarySearch(u32, &[_]u32{}, @as(u32, 1), S.orderU32));
    try std.testing.expectEqual(0, binarySearch(u32, &[_]u32{1}, @as(u32, 1), S.orderU32));
    try std.testing.expectEqual(null, binarySearch(u32, &[_]u32{0}, @as(u32, 1), S.orderU32));
    try std.testing.expectEqual(null, binarySearch(u32, &[_]u32{1}, @as(u32, 0), S.orderU32));
    try std.testing.expectEqual(4, binarySearch(u32, &[_]u32{ 1, 2, 3, 4, 5 }, @as(u32, 5), S.orderU32));
    try std.testing.expectEqual(0, binarySearch(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 2), S.orderU32));
    try std.testing.expectEqual(1, binarySearch(i32, &[_]i32{ -7, -4, 0, 9, 10 }, @as(i32, -4), S.orderI32));
    try std.testing.expectEqual(3, binarySearch(i32, &[_]i32{ -100, -25, 2, 98, 99, 100 }, @as(i32, 98), S.orderI32));
    try std.testing.expectEqual(null, binarySearch(R, &[_]R{ R.r(-100, -50), R.r(-40, -20), R.r(-10, 20), R.r(30, 40) }, @as(i32, -45), R.order));
    try std.testing.expectEqual(2, binarySearch(R, &[_]R{ R.r(-100, -50), R.r(-40, -20), R.r(-10, 20), R.r(30, 40) }, @as(i32, 10), R.order));
    try std.testing.expectEqual(1, binarySearch(R, &[_]R{ R.r(-100, -50), R.r(-40, -20), R.r(-10, 20), R.r(30, 40) }, @as(i32, -20), R.order));
    try std.testing.expectEqual(2, binarySearch([]const u8, &[_][]const u8{ "", "abc", "1234", "vwxyz" }, @as(usize, 4), S.orderLength));
}

/// Returns the index of the first element in `items` that is greater than or equal to `context`,
/// as determined by `compareFn`. If no such element exists, returns `items.len`.
///
/// `items` must be sorted in ascending order with respect to `compareFn`:
/// ```
/// [0]                                                   [len]
/// ┌───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┐
/// │.lt│.lt│ \ \ │.lt│.eq│.eq│ \ \ │.eq│.gt│.gt│ \ \ │.gt│
/// └───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┘
/// ├─────────────────┼─────────────────┼─────────────────┤
///  ↳ zero or more    ↳ zero or more    ↳ zero or more
///                   ├───┤
///                    ↳ returned index
/// ```
///
/// `O(log n)` time complexity.
///
/// See also: `binarySearch`, `upperBound`, `partitionPoint`, `equalRange`.
pub fn lowerBound(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime compareFn: fn (@TypeOf(context), T) std.math.Order,
) usize {
    const S = struct {
        fn predicate(ctx: @TypeOf(context), item: T) bool {
            return compareFn(ctx, item).invert() == .lt;
        }
    };
    return partitionPoint(T, items, context, S.predicate);
}

test lowerBound {
    const S = struct {
        fn compareU32(context: u32, item: u32) std.math.Order {
            return std.math.order(context, item);
        }
        fn compareI32(context: i32, item: i32) std.math.Order {
            return std.math.order(context, item);
        }
        fn compareF32(context: f32, item: f32) std.math.Order {
            return std.math.order(context, item);
        }
    };
    const R = struct {
        val: i32,

        fn r(val: i32) @This() {
            return .{ .val = val };
        }

        fn compareFn(context: i32, item: @This()) std.math.Order {
            return std.math.order(context, item.val);
        }
    };

    try std.testing.expectEqual(0, lowerBound(u32, &[_]u32{}, @as(u32, 0), S.compareU32));
    try std.testing.expectEqual(0, lowerBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 0), S.compareU32));
    try std.testing.expectEqual(0, lowerBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 2), S.compareU32));
    try std.testing.expectEqual(2, lowerBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 5), S.compareU32));
    try std.testing.expectEqual(2, lowerBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 8), S.compareU32));
    try std.testing.expectEqual(6, lowerBound(u32, &[_]u32{ 2, 4, 7, 7, 7, 7, 16, 32, 64 }, @as(u32, 8), S.compareU32));
    try std.testing.expectEqual(2, lowerBound(u32, &[_]u32{ 2, 4, 8, 8, 8, 8, 16, 32, 64 }, @as(u32, 8), S.compareU32));
    try std.testing.expectEqual(5, lowerBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 64), S.compareU32));
    try std.testing.expectEqual(6, lowerBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 100), S.compareU32));
    try std.testing.expectEqual(2, lowerBound(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 5), S.compareI32));
    try std.testing.expectEqual(1, lowerBound(f32, &[_]f32{ -54.2, -26.7, 0.0, 56.55, 100.1, 322.0 }, @as(f32, -33.4), S.compareF32));
    try std.testing.expectEqual(2, lowerBound(R, &[_]R{ R.r(-100), R.r(-40), R.r(-10), R.r(30) }, @as(i32, -20), R.compareFn));
}

/// Returns the index of the first element in `items` that is greater than `context`, as determined
/// by `compareFn`. If no such element exists, returns `items.len`.
///
/// `items` must be sorted in ascending order with respect to `compareFn`:
/// ```
/// [0]                                                   [len]
/// ┌───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┐
/// │.lt│.lt│ \ \ │.lt│.eq│.eq│ \ \ │.eq│.gt│.gt│ \ \ │.gt│
/// └───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┘
/// ├─────────────────┼─────────────────┼─────────────────┤
///  ↳ zero or more    ↳ zero or more    ↳ zero or more
///                                     ├───┤
///                                      ↳ returned index
/// ```
///
/// `O(log n)` time complexity.
///
/// See also: `binarySearch`, `lowerBound`, `partitionPoint`, `equalRange`.
pub fn upperBound(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime compareFn: fn (@TypeOf(context), T) std.math.Order,
) usize {
    const S = struct {
        fn predicate(ctx: @TypeOf(context), item: T) bool {
            return compareFn(ctx, item).invert() != .gt;
        }
    };
    return partitionPoint(T, items, context, S.predicate);
}

test upperBound {
    const S = struct {
        fn compareU32(context: u32, item: u32) std.math.Order {
            return std.math.order(context, item);
        }
        fn compareI32(context: i32, item: i32) std.math.Order {
            return std.math.order(context, item);
        }
        fn compareF32(context: f32, item: f32) std.math.Order {
            return std.math.order(context, item);
        }
    };
    const R = struct {
        val: i32,

        fn r(val: i32) @This() {
            return .{ .val = val };
        }

        fn compareFn(context: i32, item: @This()) std.math.Order {
            return std.math.order(context, item.val);
        }
    };

    try std.testing.expectEqual(0, upperBound(u32, &[_]u32{}, @as(u32, 0), S.compareU32));
    try std.testing.expectEqual(0, upperBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 0), S.compareU32));
    try std.testing.expectEqual(1, upperBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 2), S.compareU32));
    try std.testing.expectEqual(2, upperBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 5), S.compareU32));
    try std.testing.expectEqual(6, upperBound(u32, &[_]u32{ 2, 4, 7, 7, 7, 7, 16, 32, 64 }, @as(u32, 8), S.compareU32));
    try std.testing.expectEqual(6, upperBound(u32, &[_]u32{ 2, 4, 8, 8, 8, 8, 16, 32, 64 }, @as(u32, 8), S.compareU32));
    try std.testing.expectEqual(3, upperBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 8), S.compareU32));
    try std.testing.expectEqual(6, upperBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 64), S.compareU32));
    try std.testing.expectEqual(6, upperBound(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 100), S.compareU32));
    try std.testing.expectEqual(2, upperBound(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 5), S.compareI32));
    try std.testing.expectEqual(1, upperBound(f32, &[_]f32{ -54.2, -26.7, 0.0, 56.55, 100.1, 322.0 }, @as(f32, -33.4), S.compareF32));
    try std.testing.expectEqual(2, upperBound(R, &[_]R{ R.r(-100), R.r(-40), R.r(-10), R.r(30) }, @as(i32, -20), R.compareFn));
}

/// Returns the index of the partition point of `items` in relation to the given predicate.
/// - If all elements of `items` satisfy the predicate the returned value is `items.len`.
///
/// `items` must contain a prefix for which all elements satisfy the predicate,
/// and beyond which none of the elements satisfy the predicate:
/// ```
/// [0]                                          [len]
/// ┌────┬────┬─/ /─┬────┬─────┬─────┬─/ /─┬─────┐
/// │true│true│ \ \ │true│false│false│ \ \ │false│
/// └────┴────┴─/ /─┴────┴─────┴─────┴─/ /─┴─────┘
/// ├────────────────────┼───────────────────────┤
///  ↳ zero or more       ↳ zero or more
///                      ├─────┤
///                       ↳ returned index
/// ```
///
/// `O(log n)` time complexity.
///
/// See also: `binarySearch`, `lowerBound, `upperBound`, `equalRange`.
pub fn partitionPoint(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime predicate: fn (@TypeOf(context), T) bool,
) usize {
    var it: usize = 0;
    var len: usize = items.len;

    while (len > 1) {
        const half: usize = len / 2;
        len -= half;
        if (predicate(context, items[it + half - 1])) {
            @branchHint(.unpredictable);
            it += half;
        }
    }

    if (it < items.len) {
        it += @intFromBool(predicate(context, items[it]));
    }

    return it;
}

test partitionPoint {
    const S = struct {
        fn lowerU32(context: u32, item: u32) bool {
            return item < context;
        }
        fn lowerI32(context: i32, item: i32) bool {
            return item < context;
        }
        fn lowerF32(context: f32, item: f32) bool {
            return item < context;
        }
        fn lowerEqU32(context: u32, item: u32) bool {
            return item <= context;
        }
        fn lowerEqI32(context: i32, item: i32) bool {
            return item <= context;
        }
        fn lowerEqF32(context: f32, item: f32) bool {
            return item <= context;
        }
        fn isEven(_: void, item: u8) bool {
            return item % 2 == 0;
        }
    };

    try std.testing.expectEqual(0, partitionPoint(u32, &[_]u32{}, @as(u32, 0), S.lowerU32));
    try std.testing.expectEqual(0, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 0), S.lowerU32));
    try std.testing.expectEqual(0, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 2), S.lowerU32));
    try std.testing.expectEqual(2, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 5), S.lowerU32));
    try std.testing.expectEqual(2, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 8), S.lowerU32));
    try std.testing.expectEqual(6, partitionPoint(u32, &[_]u32{ 2, 4, 7, 7, 7, 7, 16, 32, 64 }, @as(u32, 8), S.lowerU32));
    try std.testing.expectEqual(2, partitionPoint(u32, &[_]u32{ 2, 4, 8, 8, 8, 8, 16, 32, 64 }, @as(u32, 8), S.lowerU32));
    try std.testing.expectEqual(5, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 64), S.lowerU32));
    try std.testing.expectEqual(6, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 100), S.lowerU32));
    try std.testing.expectEqual(2, partitionPoint(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 5), S.lowerI32));
    try std.testing.expectEqual(1, partitionPoint(f32, &[_]f32{ -54.2, -26.7, 0.0, 56.55, 100.1, 322.0 }, @as(f32, -33.4), S.lowerF32));
    try std.testing.expectEqual(0, partitionPoint(u32, &[_]u32{}, @as(u32, 0), S.lowerEqU32));
    try std.testing.expectEqual(0, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 0), S.lowerEqU32));
    try std.testing.expectEqual(1, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 2), S.lowerEqU32));
    try std.testing.expectEqual(2, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 5), S.lowerEqU32));
    try std.testing.expectEqual(6, partitionPoint(u32, &[_]u32{ 2, 4, 7, 7, 7, 7, 16, 32, 64 }, @as(u32, 8), S.lowerEqU32));
    try std.testing.expectEqual(6, partitionPoint(u32, &[_]u32{ 2, 4, 8, 8, 8, 8, 16, 32, 64 }, @as(u32, 8), S.lowerEqU32));
    try std.testing.expectEqual(3, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 8), S.lowerEqU32));
    try std.testing.expectEqual(6, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 64), S.lowerEqU32));
    try std.testing.expectEqual(6, partitionPoint(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 100), S.lowerEqU32));
    try std.testing.expectEqual(2, partitionPoint(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 5), S.lowerEqI32));
    try std.testing.expectEqual(1, partitionPoint(f32, &[_]f32{ -54.2, -26.7, 0.0, 56.55, 100.1, 322.0 }, @as(f32, -33.4), S.lowerEqF32));
    try std.testing.expectEqual(4, partitionPoint(u8, &[_]u8{ 0, 50, 14, 2, 5, 71 }, {}, S.isEven));
}

/// Returns a tuple of the lower and upper indices in `items` between which all
/// elements return `.eq` when given to `compareFn`.
/// - If no element in `items` returns `.eq`, both indices are the
/// index of the first element in `items` returning `.gt`.
/// - If no element in `items` returns `.gt`, both indices equal `items.len`.
///
/// `items` must be sorted in ascending order with respect to `compareFn`:
/// ```
/// [0]                                                   [len]
/// ┌───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┬───┬───┬─/ /─┬───┐
/// │.lt│.lt│ \ \ │.lt│.eq│.eq│ \ \ │.eq│.gt│.gt│ \ \ │.gt│
/// └───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┴───┴───┴─/ /─┴───┘
/// ├─────────────────┼─────────────────┼─────────────────┤
///  ↳ zero or more    ↳ zero or more    ↳ zero or more
///                   ├─────────────────┤
///                    ↳ returned range
/// ```
///
/// `O(log n)` time complexity.
///
/// See also: `binarySearch`, `lowerBound, `upperBound`, `partitionPoint`.
pub fn equalRange(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime compareFn: fn (@TypeOf(context), T) std.math.Order,
) struct { usize, usize } {
    var low: usize = 0;
    var high: usize = items.len;

    while (low < high) {
        const mid = low + (high - low) / 2;
        switch (compareFn(context, items[mid])) {
            .gt => {
                low = mid + 1;
            },
            .lt => {
                high = mid;
            },
            .eq => {
                return .{
                    low + std.sort.lowerBound(
                        T,
                        items[low..mid],
                        context,
                        compareFn,
                    ),
                    mid + std.sort.upperBound(
                        T,
                        items[mid..high],
                        context,
                        compareFn,
                    ),
                };
            },
        }
    }

    return .{ low, low };
}

test equalRange {
    const S = struct {
        fn orderU32(context: u32, item: u32) std.math.Order {
            return std.math.order(context, item);
        }
        fn orderI32(context: i32, item: i32) std.math.Order {
            return std.math.order(context, item);
        }
        fn orderF32(context: f32, item: f32) std.math.Order {
            return std.math.order(context, item);
        }
        fn orderLength(context: usize, item: []const u8) std.math.Order {
            return std.math.order(context, item.len);
        }
    };

    try std.testing.expectEqual(.{ 0, 0 }, equalRange(i32, &[_]i32{}, @as(i32, 0), S.orderI32));
    try std.testing.expectEqual(.{ 0, 0 }, equalRange(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 0), S.orderI32));
    try std.testing.expectEqual(.{ 0, 1 }, equalRange(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 2), S.orderI32));
    try std.testing.expectEqual(.{ 2, 2 }, equalRange(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 5), S.orderI32));
    try std.testing.expectEqual(.{ 2, 3 }, equalRange(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 8), S.orderI32));
    try std.testing.expectEqual(.{ 5, 6 }, equalRange(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 64), S.orderI32));
    try std.testing.expectEqual(.{ 6, 6 }, equalRange(i32, &[_]i32{ 2, 4, 8, 16, 32, 64 }, @as(i32, 100), S.orderI32));
    try std.testing.expectEqual(.{ 2, 6 }, equalRange(i32, &[_]i32{ 2, 4, 8, 8, 8, 8, 15, 22 }, @as(i32, 8), S.orderI32));
    try std.testing.expectEqual(.{ 2, 2 }, equalRange(u32, &[_]u32{ 2, 4, 8, 16, 32, 64 }, @as(u32, 5), S.orderU32));
    try std.testing.expectEqual(.{ 3, 5 }, equalRange(u32, &[_]u32{ 2, 3, 4, 5, 5 }, @as(u32, 5), S.orderU32));
    try std.testing.expectEqual(.{ 1, 1 }, equalRange(f32, &[_]f32{ -54.2, -26.7, 0.0, 56.55, 100.1, 322.0 }, @as(f32, -33.4), S.orderF32));
    try std.testing.expectEqual(.{ 3, 5 }, equalRange(
        []const u8,
        &[_][]const u8{ "Mars", "Venus", "Earth", "Saturn", "Uranus", "Mercury", "Jupiter", "Neptune" },
        @as(usize, 6),
        S.orderLength,
    ));
}

pub fn argMin(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime lessThan: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) ?usize {
    if (items.len == 0) {
        return null;
    }

    var smallest = items[0];
    var smallest_index: usize = 0;
    for (items[1..], 0..) |item, i| {
        if (lessThan(context, item, smallest)) {
            smallest = item;
            smallest_index = i + 1;
        }
    }

    return smallest_index;
}

test argMin {
    try testing.expectEqual(@as(?usize, null), argMin(i32, &[_]i32{}, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMin(i32, &[_]i32{1}, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMin(i32, &[_]i32{ 1, 2, 3, 4, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 3), argMin(i32, &[_]i32{ 9, 3, 8, 2, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMin(i32, &[_]i32{ 1, 1, 1, 1, 1 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMin(i32, &[_]i32{ -10, 1, 10 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 3), argMin(i32, &[_]i32{ 6, 3, 5, 7, 6 }, {}, desc_i32));
}

pub fn min(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime lessThan: fn (context: @TypeOf(context), lhs: T, rhs: T) bool,
) ?T {
    const i = argMin(T, items, context, lessThan) orelse return null;
    return items[i];
}

test min {
    try testing.expectEqual(@as(?i32, null), min(i32, &[_]i32{}, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 1), min(i32, &[_]i32{1}, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 1), min(i32, &[_]i32{ 1, 2, 3, 4, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 2), min(i32, &[_]i32{ 9, 3, 8, 2, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 1), min(i32, &[_]i32{ 1, 1, 1, 1, 1 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, -10), min(i32, &[_]i32{ -10, 1, 10 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 7), min(i32, &[_]i32{ 6, 3, 5, 7, 6 }, {}, desc_i32));
}

pub fn argMax(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime lessThan: fn (context: @TypeOf(context), lhs: T, rhs: T) bool,
) ?usize {
    if (items.len == 0) {
        return null;
    }

    var biggest = items[0];
    var biggest_index: usize = 0;
    for (items[1..], 0..) |item, i| {
        if (lessThan(context, biggest, item)) {
            biggest = item;
            biggest_index = i + 1;
        }
    }

    return biggest_index;
}

test argMax {
    try testing.expectEqual(@as(?usize, null), argMax(i32, &[_]i32{}, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMax(i32, &[_]i32{1}, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 4), argMax(i32, &[_]i32{ 1, 2, 3, 4, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMax(i32, &[_]i32{ 9, 3, 8, 2, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 0), argMax(i32, &[_]i32{ 1, 1, 1, 1, 1 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 2), argMax(i32, &[_]i32{ -10, 1, 10 }, {}, asc_i32));
    try testing.expectEqual(@as(?usize, 1), argMax(i32, &[_]i32{ 6, 3, 5, 7, 6 }, {}, desc_i32));
}

pub fn max(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime lessThan: fn (context: @TypeOf(context), lhs: T, rhs: T) bool,
) ?T {
    const i = argMax(T, items, context, lessThan) orelse return null;
    return items[i];
}

test max {
    try testing.expectEqual(@as(?i32, null), max(i32, &[_]i32{}, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 1), max(i32, &[_]i32{1}, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 5), max(i32, &[_]i32{ 1, 2, 3, 4, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 9), max(i32, &[_]i32{ 9, 3, 8, 2, 5 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 1), max(i32, &[_]i32{ 1, 1, 1, 1, 1 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 10), max(i32, &[_]i32{ -10, 1, 10 }, {}, asc_i32));
    try testing.expectEqual(@as(?i32, 3), max(i32, &[_]i32{ 6, 3, 5, 7, 6 }, {}, desc_i32));
}

pub fn isSorted(
    comptime T: type,
    items: []const T,
    context: anytype,
    comptime lessThan: fn (context: @TypeOf(context), lhs: T, rhs: T) bool,
) bool {
    var i: usize = 1;
    while (i < items.len) : (i += 1) {
        if (lessThan(context, items[i], items[i - 1])) {
            return false;
        }
    }

    return true;
}

test isSorted {
    try testing.expect(isSorted(i32, &[_]i32{}, {}, asc_i32));
    try testing.expect(isSorted(i32, &[_]i32{10}, {}, asc_i32));
    try testing.expect(isSorted(i32, &[_]i32{ 1, 2, 3, 4, 5 }, {}, asc_i32));
    try testing.expect(isSorted(i32, &[_]i32{ -10, 1, 1, 1, 10 }, {}, asc_i32));

    try testing.expect(isSorted(i32, &[_]i32{}, {}, desc_i32));
    try testing.expect(isSorted(i32, &[_]i32{-20}, {}, desc_i32));
    try testing.expect(isSorted(i32, &[_]i32{ 3, 2, 1, 0, -1 }, {}, desc_i32));
    try testing.expect(isSorted(i32, &[_]i32{ 10, -10 }, {}, desc_i32));

    try testing.expect(isSorted(i32, &[_]i32{ 1, 1, 1, 1, 1 }, {}, asc_i32));
    try testing.expect(isSorted(i32, &[_]i32{ 1, 1, 1, 1, 1 }, {}, desc_i32));

    try testing.expectEqual(false, isSorted(i32, &[_]i32{ 5, 4, 3, 2, 1 }, {}, asc_i32));
    try testing.expectEqual(false, isSorted(i32, &[_]i32{ 1, 2, 3, 4, 5 }, {}, desc_i32));

    try testing.expect(isSorted(u8, "abcd", {}, asc_u8));
    try testing.expect(isSorted(u8, "zyxw", {}, desc_u8));

    try testing.expectEqual(false, isSorted(u8, "abcd", {}, desc_u8));
    try testing.expectEqual(false, isSorted(u8, "zyxw", {}, asc_u8));

    try testing.expect(isSorted(u8, "ffff", {}, asc_u8));
    try testing.expect(isSorted(u8, "ffff", {}, desc_u8));
}



---
File: /std/start.zig
---

// This file is included in the compilation unit when exporting an executable.

const builtin = @import("builtin");
const native_arch = builtin.cpu.arch;
const native_os = builtin.os.tag;
const is_wasm = native_arch.isWasm();

const std = @import("std.zig");
const assert = std.debug.assert;
const uefi = std.os.uefi;
const elf = std.elf;

const root = @import("root");

const start_sym_name = if (native_arch.isMIPS()) "__start" else "_start";

comptime {
    // No matter what, we import the root file, so that any export, test, comptime
    // decls there get run.
    _ = root;

    if (builtin.output_mode == .Lib and builtin.link_mode == .dynamic) {
        if (native_os == .windows and !@hasDecl(root, "_DllMainCRTStartup")) {
            @export(&_DllMainCRTStartup, .{ .name = "_DllMainCRTStartup" });
        }
    } else if (builtin.output_mode == .Exe or @hasDecl(root, "main")) {
        if (builtin.link_libc and @hasDecl(root, "main")) {
            if (is_wasm) {
                @export(&mainWithoutEnv, .{ .name = "__main_argc_argv" });
            } else if (!@typeInfo(@TypeOf(root.main)).@"fn".calling_convention.eql(.c)) {
                @export(&main, .{ .name = "main" });
            }
        } else if (native_os == .windows and builtin.link_libc and @hasDecl(root, "wWinMain")) {
            if (!@typeInfo(@TypeOf(root.wWinMain)).@"fn".calling_convention.eql(.c)) {
                @export(&wWinMain, .{ .name = "wWinMain" });
            }
        } else if (native_os == .windows) {
            if (!@hasDecl(root, "WinMain") and !@hasDecl(root, "WinMainCRTStartup") and
                !@hasDecl(root, "wWinMain") and !@hasDecl(root, "wWinMainCRTStartup"))
            {
                @export(&WinStartup, .{ .name = "wWinMainCRTStartup" });
            } else if (@hasDecl(root, "WinMain") and !@hasDecl(root, "WinMainCRTStartup") and
                !@hasDecl(root, "wWinMain") and !@hasDecl(root, "wWinMainCRTStartup"))
            {
                @compileError("WinMain not supported; declare wWinMain or main instead");
            } else if (@hasDecl(root, "wWinMain") and !@hasDecl(root, "wWinMainCRTStartup") and
                !@hasDecl(root, "WinMain") and !@hasDecl(root, "WinMainCRTStartup"))
            {
                @export(&wWinMainCRTStartup, .{ .name = "wWinMainCRTStartup" });
            }
        } else if (native_os == .uefi) {
            if (!@hasDecl(root, "EfiMain")) @export(&EfiMain, .{ .name = "EfiMain" });
        } else if (native_os == .wasi) {
            const wasm_start_sym = switch (builtin.wasi_exec_model) {
                .reactor => "_initialize",
                .command => "_start",
            };
            if (!@hasDecl(root, wasm_start_sym) and @hasDecl(root, "main")) {
                // Only call main when defined. For WebAssembly it's allowed to pass `-fno-entry` in which
                // case it's not required to provide an entrypoint such as main.
                @export(&startWasi, .{ .name = wasm_start_sym });
            }
        } else if (is_wasm and native_os == .freestanding) {
            // Only call main when defined. For WebAssembly it's allowed to pass `-fno-entry` in which
            // case it's not required to provide an entrypoint such as main.
            if (!@hasDecl(root, start_sym_name) and @hasDecl(root, "main")) @export(&wasm_freestanding_start, .{ .name = start_sym_name });
        } else switch (native_os) {
            .other, .freestanding, .@"3ds", .psp, .vita => {},
            else => if (!@hasDecl(root, start_sym_name)) @export(&_start, .{ .name = start_sym_name }),
        }
    }
}

fn _DllMainCRTStartup(
    hinstDLL: std.os.windows.HINSTANCE,
    fdwReason: std.os.windows.DWORD,
    lpReserved: std.os.windows.LPVOID,
) callconv(.winapi) std.os.windows.BOOL {
    if (!builtin.single_threaded and !builtin.link_libc) {
        _ = @import("os/windows/tls.zig");
    }

    if (@hasDecl(root, "DllMain")) {
        return root.DllMain(hinstDLL, fdwReason, lpReserved);
    }

    return .TRUE;
}

fn wasm_freestanding_start() callconv(.c) void {
    // This is marked inline because for some reason LLVM in
    // release mode fails to inline it, and we want fewer call frames in stack traces.
    _ = @call(.always_inline, callMain, .{ {}, std.process.Environ.Block.global });
}

fn startWasi() callconv(.c) void {
    // The function call is marked inline because for some reason LLVM in
    // release mode fails to inline it, and we want fewer call frames in stack traces.
    switch (builtin.wasi_exec_model) {
        .reactor => _ = @call(.always_inline, callMain, .{ {}, std.process.Environ.Block.global }),
        .command => std.os.wasi.proc_exit(@call(.always_inline, callMain, .{ {}, std.process.Environ.Block.global })),
    }
}

fn EfiMain(handle: uefi.Handle, system_table: *uefi.tables.SystemTable) callconv(.c) usize {
    uefi.handle = handle;
    uefi.system_table = system_table;

    switch (@typeInfo(@TypeOf(root.main)).@"fn".return_type.?) {
        noreturn => {
            root.main();
        },
        void => {
            root.main();
            return 0;
        },
        uefi.Status => {
            return @intFromEnum(root.main());
        },
        uefi.Error!void => {
            root.main() catch |err| switch (err) {
                error.Unexpected => @panic("EfiMain: unexpected error"),
                else => {
                    const status = uefi.Status.fromError(@errorCast(err));
                    return @intFromEnum(status);
                },
            };

            return 0;
        },
        else => @compileError(
            "expected return type of main to be 'void', 'noreturn', " ++
                "'uefi.Status', or 'uefi.Error!void'",
        ),
    }
}

fn _start() callconv(.naked) noreturn {
    // TODO set Top of Stack on non x86_64-plan9
    if (native_os == .plan9 and native_arch == .x86_64) {
        // from /sys/src/libc/amd64/main9.s
        std.os.plan9.tos = asm volatile (""
            : [tos] "={rax}" (-> *std.os.plan9.Tos),
        );
    }

    // This is the first userspace frame. Prevent DWARF-based unwinders from unwinding further. We
    // prevent FP-based unwinders from unwinding further by zeroing the register below.
    if (builtin.unwind_tables != .none or !builtin.strip_debug_info) asm volatile (switch (native_arch) {
            .aarch64, .aarch64_be => ".cfi_undefined lr",
            .alpha => ".cfi_undefined $26",
            .arc, .arceb => ".cfi_undefined blink",
            .arm, .armeb, .thumb, .thumbeb => "", // https://github.com/llvm/llvm-project/issues/115891
            .csky => ".cfi_undefined lr",
            .hexagon => ".cfi_undefined r31",
            .kvx => ".cfi_undefined r14",
            .loongarch32, .loongarch64 => ".cfi_undefined 1",
            .m68k => ".cfi_undefined %%pc",
            .microblaze, .microblazeel => ".cfi_undefined r15",
            .mips, .mipsel, .mips64, .mips64el => ".cfi_undefined $ra",
            .or1k => ".cfi_undefined r9",
            .powerpc, .powerpcle, .powerpc64, .powerpc64le => ".cfi_undefined lr",
            .riscv32, .riscv32be, .riscv64, .riscv64be => if (builtin.zig_backend == .stage2_riscv64)
                ""
            else
                ".cfi_undefined ra",
            .s390x => ".cfi_undefined %%r14",
            .sh, .sheb => ".cfi_undefined pr",
            .sparc, .sparc64 => ".cfi_undefined %%i7",
            .x86 => ".cfi_undefined %%eip",
            .x86_64 => ".cfi_undefined %%rip",
            else => @compileError("unsupported arch"),
        });

    // Move this to the riscv prong below when this is resolved: https://github.com/ziglang/zig/issues/20918
    if (builtin.cpu.arch.isRISCV() and builtin.zig_backend != .stage2_riscv64) asm volatile (
        \\ .weak __global_pointer$
        \\ .hidden __global_pointer$
        \\ .option push
        \\ .option norelax
        \\ lla gp, __global_pointer$
        \\ .option pop
    );

    // Note that we maintain a very low level of trust with regards to ABI guarantees at this point.
    // We will redundantly align the stack, clear the link register, etc. While e.g. the Linux
    // kernel is usually good about upholding the ABI guarantees, the same cannot be said of dynamic
    // linkers; musl's ldso, for example, opts to not align the stack when invoking the dynamic
    // linker explicitly.
    asm volatile (switch (native_arch) {
            .x86_64 =>
            \\ xorl %%ebp, %%ebp
            \\ movq %%rsp, %%rdi
            \\ andq $-16, %%rsp
            \\ callq %[posixCallMainAndExit:P]
            ,
            .x86 =>
            \\ xorl %%ebp, %%ebp
            \\ movl %%esp, %%eax
            \\ andl $-16, %%esp
            \\ subl $12, %%esp
            \\ pushl %%eax
            \\ calll %[posixCallMainAndExit:P]
            ,
            .aarch64, .aarch64_be =>
            \\ mov fp, #0
            \\ mov lr, #0
            \\ mov x0, sp
            \\ and sp, x0, #-16
            \\ b %[posixCallMainAndExit]
            ,
            .alpha =>
            // $15 = FP, $26 = LR, $29 = GP, $30 = SP
            \\ br $29, 1f
            \\1:
            \\ ldgp $29, 0($29)
            \\ mov 0, $15
            \\ mov 0, $26
            \\ mov $30, $16
            \\ ldi $1, -16
            \\ and $30, $30, $1
            \\ jsr $26, %[posixCallMainAndExit]
            ,
            .arc, .arceb =>
            // ARC v1 and v2 had a very low stack alignment requirement of 4; v3 increased it to 16.
            \\ mov fp, 0
            \\ mov blink, 0
            \\ mov r0, sp
            \\ and sp, sp, -16
            \\ b %[posixCallMainAndExit]
            ,
            .arm, .armeb, .thumb, .thumbeb =>
            // Note that this code must work for Thumb-1.
            // r7 = FP (local), r11 = FP (unwind)
            \\ movs v1, #0
            \\ mov r7, v1
            \\ mov r11, v1
            \\ mov lr, v1
            \\ mov a1, sp
            \\ subs v1, #16
            \\ ands v1, a1
            \\ mov sp, v1
            \\ b %[posixCallMainAndExit]
            ,
            .csky =>
            // The CSKY ABI assumes that `gb` is set to the address of the GOT in order for
            // position-independent code to work. We depend on this in `std.pie` to locate
            // `_DYNAMIC` as well.
            // r8 = FP
            \\ grs t0, 1f
            \\ 1:
            \\ lrw gb, 1b@GOTPC
            \\ addu gb, t0
            \\ movi r8, 0
            \\ movi lr, 0
            \\ mov a0, sp
            \\ andi sp, sp, -8
            \\ jmpi %[posixCallMainAndExit]
            ,
            .hexagon =>
            // r29 = SP, r30 = FP, r31 = LR
            \\ r30 = #0
            \\ r31 = #0
            \\ r0 = r29
            \\ r29 = and(r29, #-8)
            \\ memw(r29 + #-8) = r29
            \\ r29 = add(r29, #-8)
            \\ call %[posixCallMainAndExit]
            ,
            .kvx =>
            \\ make $fp = 0
            \\ ;;
            \\ set $ra = $fp
            \\ copyd $r0 = $sp
            \\ andd $sp = $sp, -32
            \\ ;;
            \\ goto %[posixCallMainAndExit]
            ,
            .loongarch32 =>
            \\ move $fp, $zero
            \\ move $ra, $zero
            \\ move $a0, $sp
            \\ srli.w $sp, $sp, 4
            \\ slli.w $sp, $sp, 4
            \\ b %[posixCallMainAndExit]
            ,
            .loongarch64 =>
            \\ move $fp, $zero
            \\ move $ra, $zero
            \\ move $a0, $sp
            \\ bstrins.d $sp, $zero, 3, 0
            \\ b %[posixCallMainAndExit]
            ,
            .or1k =>
            // r1 = SP, r2 = FP, r9 = LR
            \\ l.ori r2, r0, 0
            \\ l.ori r9, r0, 0
            \\ l.ori r3, r1, 0
            \\ l.andi r1, r1, -4
            \\ l.jal %[posixCallMainAndExit]
            ,
            .riscv32, .riscv32be, .riscv64, .riscv64be =>
            \\ li fp, 0
            \\ li ra, 0
            \\ mv a0, sp
            \\ andi sp, sp, -16
            \\ tail %[posixCallMainAndExit]@plt
            ,
            .m68k =>
            // Note that the - 8 is needed because pc in the jsr instruction points into the middle
            // of the jsr instruction. (The lea is 6 bytes, the jsr is 4 bytes.)
            \\ suba.l %%fp, %%fp
            \\ move.l %%sp, %%a0
            \\ move.l %%a0, %%d0
            \\ and.l #-4, %%d0
            \\ move.l %%d0, %%sp
            \\ move.l %%a0, -(%%sp)
            \\ lea %[posixCallMainAndExit] - . - 8, %%a0
            \\ jsr (%%pc, %%a0)
            ,
            .microblaze, .microblazeel =>
            // r1 = SP, r15 = LR, r19 = FP, r20 = GP
            \\ ori r15, r0, r0
            \\ ori r19, r0, r0
            \\ mfs r20, rpc
            \\ addik r20, r20, _GLOBAL_OFFSET_TABLE_ + 8
            \\ ori r5, r1, r0
            \\ andi r1, r1, -4
            \\ brlid r15, %[posixCallMainAndExit]
            ,
            .mips, .mipsel =>
            \\ move $fp, $zero
            \\ bal 1f
            \\ .gpword .
            \\ .gpword %[posixCallMainAndExit]
            \\1:
            // The `gp` register on MIPS serves a similar purpose to `r2` (ToC pointer) on PPC64.
            \\ lw $gp, 0($ra)
            \\ nop
            \\ subu $gp, $ra, $gp
            \\ lw $t9, 4($ra)
            \\ nop
            \\ addu $t9, $t9, $gp
            \\ move $ra, $zero
            \\ move $a0, $sp
            \\ and $sp, -8
            \\ subu $sp, $sp, 16
            \\ jalr $t9
            ,
            .mips64, .mips64el => switch (builtin.abi) {
                .gnuabin32, .muslabin32 =>
                \\ move $fp, $zero
                \\ bal 1f
                \\ .gpword .
                \\ .gpword %[posixCallMainAndExit]
                \\1:
                // The `gp` register on MIPS serves a similar purpose to `r2` (ToC pointer) on PPC64.
                \\ lw $gp, 0($ra)
                \\ subu $gp, $ra, $gp
                \\ lw $t9, 4($ra)
                \\ addu $t9, $t9, $gp
                \\ move $ra, $zero
                \\ move $a0, $sp
                \\ and $sp, -8
                \\ subu $sp, $sp, 16
                \\ jalr $t9
                ,
                else =>
                \\ move $fp, $zero
                // This is needed because early MIPS versions don't support misaligned loads. Without
                // this directive, the hidden `nop` inserted to fill the delay slot after `bal` would
                // cause the two doublewords to be aligned to 4 bytes instead of 8.
                \\ .balign 8
                \\ bal 1f
                \\ .gpdword .
                \\ .gpdword %[posixCallMainAndExit]
                \\1:
                // The `gp` register on MIPS serves a similar purpose to `r2` (ToC pointer) on PPC64.
                \\ ld $gp, 0($ra)
                \\ dsubu $gp, $ra, $gp
                \\ ld $t9, 8($ra)
                \\ daddu $t9, $t9, $gp
                \\ move $ra, $zero
                \\ move $a0, $sp
                \\ and $sp, -16
                \\ dsubu $sp, $sp, 16
                \\ jalr $t9
                ,
            },
            .powerpc, .powerpcle =>
            // Set up the initial stack frame, and clear the back chain pointer.
            // r1 = SP, r31 = FP
            \\ mr 3, 1
            \\ clrrwi 1, 1, 4
            \\ li 0, 0
            \\ stwu 1, -16(1)
            \\ stw 0, 0(1)
            \\ li 31, 0
            \\ mtlr 0
            \\ b %[posixCallMainAndExit]
            ,
            .powerpc64, .powerpc64le =>
            // Set up the ToC and initial stack frame, and clear the back chain pointer.
            // r1 = SP, r2 = ToC, r31 = FP
            \\ addis 2, 12, .TOC. - %[_start]@ha
            \\ addi 2, 2, .TOC. - %[_start]@l
            \\ mr 3, 1
            \\ clrrdi 1, 1, 4
            \\ li 0, 0
            \\ stdu 0, -32(1)
            \\ li 31, 0
            \\ mtlr 0
            \\ b %[posixCallMainAndExit]
            \\ nop
            ,
            .s390x =>
            // Set up the stack frame (register save area and cleared back-chain slot).
            // r11 = FP, r14 = LR, r15 = SP
            \\ lghi %%r11, 0
            \\ lghi %%r14, 0
            \\ lgr %%r2, %%r15
            \\ lghi %%r0, -16
            \\ ngr %%r15, %%r0
            \\ aghi %%r15, -160
            \\ lghi %%r0, 0
            \\ stg  %%r0, 0(%%r15)
            \\ jg %[posixCallMainAndExit]
            ,
            .sh, .sheb =>
            // r14 = FP, r15 = SP, pr = LR
            \\ mov #0, r0
            \\ lds r0, pr
            \\ mov r0, r14
            \\ mov r15, r4
            \\ mov #-4, r0
            \\ and r0, r15
            \\ mov.l 2f, r1
            \\1:
            \\ bsrf r1
            \\2:
            \\ .balign 4
            \\ .long %[posixCallMainAndExit]@PCREL - (1b + 4 - .)
            ,
            .sparc =>
            // argc is stored after a register window (16 registers * 4 bytes).
            // i7 = LR
            \\ mov %%g0, %%fp
            \\ mov %%g0, %%i7
            \\ add %%sp, 64, %%o0
            \\ and %%sp, -8, %%sp
            \\ ba,a %[posixCallMainAndExit]
            ,
            .sparc64 =>
            // argc is stored after a register window (16 registers * 8 bytes) plus the stack bias
            // (2047 bytes).
            // i7 = LR
            \\ mov %%g0, %%fp
            \\ mov %%g0, %%i7
            \\ add %%sp, 2175, %%o0
            \\ add %%sp, 2047, %%sp
            \\ and %%sp, -16, %%sp
            \\ sub %%sp, 2047, %%sp
            \\ ba,a %[posixCallMainAndExit]
            ,
            else => @compileError("unsupported arch"),
        }
        :
        : [_start] "X" (&_start),
          [posixCallMainAndExit] "X" (&posixCallMainAndExit),
    );
}

fn WinStartup() callconv(.withStackAlign(.c, 1)) noreturn {
    // Switch from the x87 fpu state set by windows to the state expected by the gnu abi.
    if (builtin.cpu.arch.isX86() and builtin.abi == .gnu) asm volatile ("fninit");

    if (!builtin.single_threaded and !builtin.link_libc) {
        _ = @import("os/windows/tls.zig");
    }

    std.Thread.maybeAttachSignalStack();
    std.debug.maybeEnableSegfaultHandler();

    std.os.windows.ntdll.RtlExitUserProcess(
        callMain(std.os.windows.peb().ProcessParameters.CommandLine.slice(), .global),
    );
}

fn wWinMainCRTStartup() callconv(.withStackAlign(.c, 1)) noreturn {
    // Switch from the x87 fpu state set by windows to the state expected by the gnu abi.
    if (builtin.cpu.arch.isX86() and builtin.abi == .gnu) asm volatile ("fninit");

    if (!builtin.single_threaded and !builtin.link_libc) {
        _ = @import("os/windows/tls.zig");
    }

    std.Thread.maybeAttachSignalStack();
    std.debug.maybeEnableSegfaultHandler();

    const result: std.os.windows.INT = call_wWinMain();
    std.os.windows.ntdll.RtlExitUserProcess(@as(std.os.windows.UINT, @bitCast(result)));
}

fn wWinMain(hInstance: *anyopaque, hPrevInstance: ?*anyopaque, pCmdLine: [*:0]u16, nCmdShow: c_int) callconv(.c) c_int {
    return root.wWinMain(@ptrCast(hInstance), @ptrCast(hPrevInstance), pCmdLine, @intCast(nCmdShow));
}

fn posixCallMainAndExit(argc_argv_ptr: [*]usize) callconv(.c) noreturn {
    // We're not ready to panic until thread local storage is initialized.
    @setRuntimeSafety(false);
    // Code coverage instrumentation might try to use thread local variables.
    @disableInstrumentation();
    const argc = argc_argv_ptr[0];
    const argv: [*][*:0]u8 = @ptrCast(argc_argv_ptr + 1);

    const envp_optional: [*:null]?[*:0]u8 = @ptrCast(@alignCast(argv + argc + 1));
    var envp_count: usize = 0;
    while (envp_optional[envp_count]) |_| : (envp_count += 1) {}
    const envp = envp_optional[0..envp_count :null];

    // Find the beginning of the auxiliary vector
    const auxv: [*]elf.Auxv = @ptrCast(@alignCast(envp.ptr + envp_count + 1));

    var at_hwcap: usize = 0;
    const phdrs = init: {
        var i: usize = 0;
        var at_phdr: usize = 0;
        var at_phnum: usize = 0;
        while (auxv[i].a_type != elf.AT_NULL) : (i += 1) {
            switch (auxv[i].a_type) {
                elf.AT_PHNUM => at_phnum = auxv[i].a_un.a_val,
                elf.AT_PHDR => at_phdr = auxv[i].a_un.a_val,
                elf.AT_HWCAP => at_hwcap = auxv[i].a_un.a_val,
                else => continue,
            }
        }
        break :init @as([*]elf.Phdr, @ptrFromInt(at_phdr))[0..at_phnum];
    };

    // Apply the initial relocations as early as possible in the startup process. We cannot
    // make calls yet on some architectures (e.g. MIPS) *because* they haven't been applied yet,
    // so this must be fully inlined.
    if (builtin.link_mode == .static and builtin.position_independent_executable) {
        @call(.always_inline, std.pie.relocate, .{phdrs});
    }

    if (native_os == .linux) {
        // This must be done after PIE relocations have been applied or we may crash
        // while trying to access the global variable (happens on MIPS at least).
        std.os.linux.elf_aux_maybe = auxv;

        if (!builtin.single_threaded) {
            // ARMv6 targets (and earlier) have no support for TLS in hardware.
            // FIXME: Elide the check for targets >= ARMv7 when the target feature API
            // becomes less verbose (and more usable).
            if (comptime native_arch.isArm()) {
                if (at_hwcap & std.os.linux.HWCAP.TLS == 0) {
                    // FIXME: Make __aeabi_read_tp call the kernel helper kuser_get_tls
                    // For the time being use a simple trap instead of a @panic call to
                    // keep the binary bloat under control.
                    @trap();
                }
            }

            // Initialize the TLS area.
            std.os.linux.tls.initStatic(phdrs);
        }

        // The way Linux executables represent stack size is via the PT_GNU_STACK
        // program header. However the kernel does not recognize it; it always gives 8 MiB.
        // Here we look for the stack size in our program headers and use setrlimit
        // to ask for more stack space.
        expandStackSize(phdrs);
    }

    const opt_init_array_start = @extern([*]const *const fn () callconv(.c) void, .{
        .name = "__init_array_start",
        .linkage = .weak,
    });
    const opt_init_array_end = @extern([*]const *const fn () callconv(.c) void, .{
        .name = "__init_array_end",
        .linkage = .weak,
    });
    if (opt_init_array_start) |init_array_start| {
        const init_array_end = opt_init_array_end.?;
        const slice = init_array_start[0 .. init_array_end - init_array_start];
        for (slice) |func| func();
    }

    std.process.exit(callMainWithArgs(argc, argv, envp));
}

fn expandStackSize(phdrs: []elf.Phdr) void {
    @disableInstrumentation();
    for (phdrs) |*phdr| {
        switch (phdr.p_type) {
            elf.PT_GNU_STACK => {
                if (phdr.p_memsz == 0) break;
                assert(phdr.p_memsz % std.heap.page_size_min == 0);

                // Silently fail if we are unable to get limits.
                const limits = std.posix.getrlimit(.STACK) catch break;

                // Clamp to limits.max .
                const wanted_stack_size = @min(phdr.p_memsz, limits.max);

                if (wanted_stack_size > limits.cur) {
                    std.posix.setrlimit(.STACK, .{
                        .cur = wanted_stack_size,
                        .max = limits.max,
                    }) catch {
                        // Because we could not increase the stack size to the upper bound,
                        // depending on what happens at runtime, a stack overflow may occur.
                        // However it would cause a segmentation fault, thanks to stack probing,
                        // so we do not have a memory safety issue here.
                        // This is intentional silent failure.
                        // This logic should be revisited when the following issues are addressed:
                        // https://github.com/ziglang/zig/issues/157
                        // https://github.com/ziglang/zig/issues/1006
                    };
                }
                break;
            },
            else => {},
        }
    }
}

inline fn callMainWithArgs(argc: usize, argv: [*][*:0]u8, envp: [:null]?[*:0]u8) u8 {
    const env_block: std.process.Environ.Block = .{ .slice = envp };
    if (std.Options.debug_threaded_io) |t| {
        if (@sizeOf(std.Io.Threaded.Argv0) != 0) t.argv0.value = argv[0];
        t.environ = .{ .process_environ = .{ .block = env_block } };
        t.environ_initialized = env_block.isEmpty();
    }
    std.Thread.maybeAttachSignalStack();
    std.debug.maybeEnableSegfaultHandler();
    return callMain(argv[0..argc], env_block);
}

fn main(c_argc: c_int, c_argv: [*][*:0]c_char, c_envp: [*:null]?[*:0]c_char) callconv(.c) c_int {
    var env_count: usize = 0;
    while (c_envp[env_count] != null) : (env_count += 1) {}
    const envp = c_envp[0..env_count :null];

    switch (builtin.os.tag) {
        .linux => {
            const at_phdr = std.c.getauxval(elf.AT_PHDR);
            const at_phnum = std.c.getauxval(elf.AT_PHNUM);
            const phdrs = (@as([*]elf.Phdr, @ptrFromInt(at_phdr)))[0..at_phnum];
            expandStackSize(phdrs);
        },
        .windows => {
            // On Windows, we ignore libc environment and argv and get those
            // values in their intended encoding from the PEB instead.
            std.Thread.maybeAttachSignalStack();
            std.debug.maybeEnableSegfaultHandler();
            return callMain(std.os.windows.peb().ProcessParameters.CommandLine.slice(), .global);
        },
        else => {},
    }

    return callMainWithArgs(@as(usize, @intCast(c_argc)), @as([*][*:0]u8, @ptrCast(c_argv)), @ptrCast(envp));
}

fn mainWithoutEnv(c_argc: c_int, c_argv: [*][*:0]c_char) callconv(.c) c_int {
    const argv = @as([*][*:0]u8, @ptrCast(c_argv))[0..@intCast(c_argc)];
    const environ: [:null]?[*:0]u8 = switch (builtin.os.tag) {
        .wasi, .emscripten => environ: {
            const c_environ = std.c.environ;
            var env_count: usize = 0;
            while (c_environ[env_count] != null) : (env_count += 1) {}
            break :environ c_environ[0..env_count :null];
        },
        else => &.{},
    };
    const env_block: std.process.Environ.Block = .{ .slice = environ };
    if (std.Options.debug_threaded_io) |t| {
        if (@sizeOf(std.Io.Threaded.Argv0) != 0) t.argv0.value = argv[0];
        t.environ = .{ .process_environ = .{ .block = env_block } };
        t.environ_initialized = env_block.isEmpty();
    }
    return callMain(argv, env_block);
}

/// General error message for a malformed return type
const bad_main_ret = "expected return type of main to be 'void', '!void', 'noreturn', 'u8', or '!u8'";

const use_debug_allocator = !is_wasm and switch (builtin.mode) {
    .Debug => true,
    .ReleaseSafe => !builtin.link_libc, // Not ideal, but the best we have for now.
    .ReleaseFast, .ReleaseSmall => !builtin.link_libc and builtin.single_threaded, // Also not ideal.
};
var debug_allocator: std.heap.DebugAllocator(.{}) = .init;

inline fn callMain(args: std.process.Args.Vector, environ: std.process.Environ.Block) u8 {
    const fn_info = @typeInfo(@TypeOf(root.main)).@"fn";
    if (fn_info.params.len == 0) return wrapMain(root.main());
    if (fn_info.params[0].type.? == std.process.Init.Minimal) return wrapMain(root.main(.{
        .args = .{ .vector = args },
        .environ = .{ .block = environ },
    }));

    const gpa = if (use_debug_allocator)
        debug_allocator.allocator()
    else if (builtin.link_libc)
        std.heap.c_allocator
    else if (is_wasm)
        std.heap.wasm_allocator
    else if (!builtin.single_threaded)
        std.heap.smp_allocator
    else
        comptime unreachable;

    defer if (use_debug_allocator) {
        _ = debug_allocator.deinit(); // Leaks do not affect return code.
    };

    const arena_backing_allocator = if (is_wasm) gpa else std.heap.page_allocator;

    var arena_allocator = std.heap.ArenaAllocator.init(arena_backing_allocator);
    defer arena_allocator.deinit();

    var threaded: std.Io.Threaded = .init(gpa, .{
        .argv0 = .init(.{ .vector = args }),
        .environ = .{ .block = environ },
    });
    defer threaded.deinit();

    var environ_map = std.process.Environ.createMap(.{ .block = environ }, gpa) catch |err|
        std.process.fatal("failed to parse environment variables: {t}", .{err});
    defer environ_map.deinit();

    const preopens = std.process.Preopens.init(arena_allocator.allocator()) catch |err|
        std.process.fatal("failed to init preopens: {t}", .{err});

    return wrapMain(root.main(.{
        .minimal = .{
            .args = .{ .vector = args },
            .environ = .{ .block = environ },
        },
        .arena = &arena_allocator,
        .gpa = gpa,
        .io = threaded.io(),
        .environ_map = &environ_map,
        .preopens = preopens,
    }));
}

inline fn wrapMain(result: anytype) u8 {
    const ReturnType = @TypeOf(result);
    switch (ReturnType) {
        void => return 0,
        noreturn => unreachable,
        u8 => return result,
        else => {},
    }
    if (@typeInfo(ReturnType) != .error_union) @compileError(bad_main_ret);

    const unwrapped_result = result catch |err| {
        std.log.err("{t}", .{err});
        switch (native_os) {
            .freestanding, .other => {},
            else => if (@errorReturnTrace()) |trace| std.debug.dumpErrorReturnTrace(trace),
        }
        return 1;
    };

    return switch (@TypeOf(unwrapped_result)) {
        noreturn => unreachable,
        void => 0,
        u8 => unwrapped_result,
        else => @compileError(bad_main_ret),
    };
}

fn call_wWinMain() std.os.windows.INT {
    const peb = std.os.windows.peb();
    const MAIN_HINSTANCE = @typeInfo(@TypeOf(root.wWinMain)).@"fn".params[0].type.?;
    const hInstance: MAIN_HINSTANCE = @ptrCast(peb.ImageBaseAddress);
    const lpCmdLine: [*:0]u16 = @ptrCast(peb.ProcessParameters.CommandLine.Buffer);

    // There are various types used for the 'show window' variable through the Win32 APIs:
    // - u16 in STARTUPINFOA.wShowWindow / STARTUPINFOW.wShowWindow
    // - c_int in ShowWindow
    // - u32 in PEB.ProcessParameters.dwShowWindow
    // Since STARTUPINFO is the bottleneck for the allowed values, we use `u16` as the
    // type which can coerce into i32/c_int/u32 depending on how the user defines their wWinMain
    // (the Win32 docs show wWinMain with `int` as the type for nShowCmd).
    const nShowCmd: u16 = nShowCmd: {
        // This makes Zig match the nShowCmd behavior of a C program with a WinMain symbol:
        // - With STARTF_USESHOWWINDOW set in STARTUPINFO.dwFlags of the CreateProcess call:
        //   - nShowCmd is STARTUPINFO.wShowWindow from the parent CreateProcess call
        // - With STARTF_USESHOWWINDOW unset:
        //   - nShowCmd is always SW_SHOWDEFAULT
        const SW_SHOWDEFAULT = 10;
        if (peb.ProcessParameters.dwFlags & std.os.windows.STARTF_USESHOWWINDOW != 0) {
            break :nShowCmd @truncate(peb.ProcessParameters.dwShowWindow);
        }
        break :nShowCmd SW_SHOWDEFAULT;
    };

    // second parameter hPrevInstance, MSDN: "This parameter is always NULL"
    return root.wWinMain(hInstance, null, lpCmdLine, nShowCmd);
}



---
File: /std/static_string_map.zig
---

const std = @import("std.zig");
const mem = std.mem;

/// Static string map optimized for small sets of disparate string keys.
/// Works by separating the keys by length at initialization and only checking
/// strings of equal length at runtime.
pub fn StaticStringMap(comptime
```
