```
ting.expect((divExact(i32, 10, 5) catch unreachable) == 2);
    try testing.expect((divExact(i32, -10, 5) catch unreachable) == -2);
    try testing.expectError(error.DivisionByZero, divExact(i8, -5, 0));
    try testing.expectError(error.Overflow, divExact(i8, -128, -1));
    try testing.expectError(error.UnexpectedRemainder, divExact(i32, 5, 2));

    try testing.expect((divExact(f32, 10.0, 5.0) catch unreachable) == 2.0);
    try testing.expect((divExact(f32, -10.0, 5.0) catch unreachable) == -2.0);
    try testing.expectError(error.UnexpectedRemainder, divExact(f32, 5.0, 2.0));
}

/// Returns numerator modulo denominator, or an error if denominator is
/// zero or negative. Negative numerators never result in negative
/// return values.
pub fn mod(comptime T: type, numerator: T, denominator: T) !T {
    @setRuntimeSafety(false);
    if (denominator == 0) return error.DivisionByZero;
    if (denominator < 0) return error.NegativeDenominator;
    return @mod(numerator, denominator);
}

test mod {
    try testMod();
    try comptime testMod();
}
fn testMod() !void {
    try testing.expect((mod(i32, -5, 3) catch unreachable) == 1);
    try testing.expect((mod(i32, 5, 3) catch unreachable) == 2);
    try testing.expectError(error.NegativeDenominator, mod(i32, 10, -1));
    try testing.expectError(error.DivisionByZero, mod(i32, 10, 0));

    try testing.expect((mod(f32, -5, 3) catch unreachable) == 1);
    try testing.expect((mod(f32, 5, 3) catch unreachable) == 2);
    try testing.expectError(error.NegativeDenominator, mod(f32, 10, -1));
    try testing.expectError(error.DivisionByZero, mod(f32, 10, 0));
}

/// Returns the remainder when numerator is divided by denominator, or
/// an error if denominator is zero or negative. Negative numerators
/// can give negative results.
pub fn rem(comptime T: type, numerator: T, denominator: T) !T {
    @setRuntimeSafety(false);
    if (denominator == 0) return error.DivisionByZero;
    if (denominator < 0) return error.NegativeDenominator;
    return @rem(numerator, denominator);
}

test rem {
    try testRem();
    try comptime testRem();
}
fn testRem() !void {
    try testing.expect((rem(i32, -5, 3) catch unreachable) == -2);
    try testing.expect((rem(i32, 5, 3) catch unreachable) == 2);
    try testing.expectError(error.NegativeDenominator, rem(i32, 10, -1));
    try testing.expectError(error.DivisionByZero, rem(i32, 10, 0));

    try testing.expect((rem(f32, -5, 3) catch unreachable) == -2);
    try testing.expect((rem(f32, 5, 3) catch unreachable) == 2);
    try testing.expectError(error.NegativeDenominator, rem(f32, 10, -1));
    try testing.expectError(error.DivisionByZero, rem(f32, 10, 0));
}

/// Returns the negation of the integer parameter.
/// Result is a signed integer.
pub fn negateCast(x: anytype) !std.meta.Int(.signed, @bitSizeOf(@TypeOf(x))) {
    if (@typeInfo(@TypeOf(x)).int.signedness == .signed) return negate(x);

    const int = std.meta.Int(.signed, @bitSizeOf(@TypeOf(x)));
    if (x > -minInt(int)) return error.Overflow;

    if (x == -minInt(int)) return minInt(int);

    return -@as(int, @intCast(x));
}

test negateCast {
    try testing.expect((negateCast(@as(u32, 999)) catch unreachable) == -999);
    try testing.expect(@TypeOf(negateCast(@as(u32, 999)) catch unreachable) == i32);

    try testing.expect((negateCast(@as(u32, -minInt(i32))) catch unreachable) == minInt(i32));
    try testing.expect(@TypeOf(negateCast(@as(u32, -minInt(i32))) catch unreachable) == i32);

    try testing.expectError(error.Overflow, negateCast(@as(u32, maxInt(i32) + 10)));
}

/// Cast an integer to a different integer type. If the value doesn't fit,
/// return null.
pub fn cast(comptime T: type, x: anytype) ?T {
    comptime assert(@typeInfo(T) == .int); // must pass an integer
    const is_comptime = @TypeOf(x) == comptime_int;
    comptime assert(is_comptime or @typeInfo(@TypeOf(x)) == .int); // must pass an integer
    if ((is_comptime or maxInt(@TypeOf(x)) > maxInt(T)) and x > maxInt(T)) {
        return null;
    } else if ((is_comptime or minInt(@TypeOf(x)) < minInt(T)) and x < minInt(T)) {
        return null;
    } else {
        return @as(T, @intCast(x));
    }
}

test cast {
    try testing.expect(cast(u8, 300) == null);
    try testing.expect(cast(u8, @as(u32, 300)) == null);
    try testing.expect(cast(i8, -200) == null);
    try testing.expect(cast(i8, @as(i32, -200)) == null);
    try testing.expect(cast(u8, -1) == null);
    try testing.expect(cast(u8, @as(i8, -1)) == null);
    try testing.expect(cast(u64, -1) == null);
    try testing.expect(cast(u64, @as(i8, -1)) == null);

    try testing.expect(cast(u8, 255).? == @as(u8, 255));
    try testing.expect(cast(u8, @as(u32, 255)).? == @as(u8, 255));
    try testing.expect(@TypeOf(cast(u8, 255).?) == u8);
    try testing.expect(@TypeOf(cast(u8, @as(u32, 255)).?) == u8);
}

pub const AlignCastError = error{UnalignedMemory};

fn AlignCastResult(comptime alignment: Alignment, comptime Ptr: type) type {
    const orig = @typeInfo(Ptr).pointer;
    return @Pointer(orig.size, .{
        .@"const" = orig.is_const,
        .@"volatile" = orig.is_volatile,
        .@"allowzero" = orig.is_allowzero,
        .@"align" = alignment.toByteUnits(),
        .@"addrspace" = orig.address_space,
    }, orig.child, orig.sentinel());
}

/// Align cast a pointer but return an error if it's the wrong alignment
pub fn alignCast(comptime alignment: Alignment, ptr: anytype) AlignCastError!AlignCastResult(alignment, @TypeOf(ptr)) {
    if (alignment.check(@intFromPtr(ptr))) return @alignCast(ptr);
    return error.UnalignedMemory;
}

/// Asserts `int > 0`.
pub fn isPowerOfTwo(int: anytype) bool {
    assert(int > 0);
    return (int & (int - 1)) == 0;
}

test isPowerOfTwo {
    try testing.expect(isPowerOfTwo(@as(u8, 1)));
    try testing.expect(isPowerOfTwo(2));
    try testing.expect(!isPowerOfTwo(@as(i16, 3)));
    try testing.expect(isPowerOfTwo(4));
    try testing.expect(!isPowerOfTwo(@as(u32, 31)));
    try testing.expect(isPowerOfTwo(32));
    try testing.expect(!isPowerOfTwo(@as(i64, 63)));
    try testing.expect(isPowerOfTwo(128));
    try testing.expect(isPowerOfTwo(@as(u128, 256)));
}

/// Aligns the given integer type bit width to a width divisible by 8.
pub fn ByteAlignedInt(comptime T: type) type {
    const info = @typeInfo(T).int;
    const bits = (info.bits + 7) / 8 * 8;
    const extended_type = std.meta.Int(info.signedness, bits);
    return extended_type;
}

test ByteAlignedInt {
    try testing.expect(ByteAlignedInt(u0) == u0);
    try testing.expect(ByteAlignedInt(i0) == i0);
    try testing.expect(ByteAlignedInt(u3) == u8);
    try testing.expect(ByteAlignedInt(u8) == u8);
    try testing.expect(ByteAlignedInt(i111) == i112);
    try testing.expect(ByteAlignedInt(u129) == u136);
}

/// Rounds the given floating point number to the nearest integer.
/// If two integers are equally close, rounds away from zero.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @round
pub inline fn round(value: anytype) @TypeOf(value) {
    return @round(value);
}

/// Rounds the given floating point number to an integer, towards zero.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @trunc
pub inline fn trunc(value: anytype) @TypeOf(value) {
    return @trunc(value);
}

/// Returns the largest integral value not greater than the given floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @floor
pub inline fn floor(value: anytype) @TypeOf(value) {
    return @floor(value);
}

/// Returns the nearest power of two less than or equal to value, or
/// zero if value is less than or equal to zero.
pub fn floorPowerOfTwo(comptime T: type, value: T) T {
    const uT = std.meta.Int(.unsigned, @typeInfo(T).int.bits);
    if (value <= 0) return 0;
    return @as(T, 1) << log2_int(uT, @as(uT, @intCast(value)));
}

test floorPowerOfTwo {
    try testFloorPowerOfTwo();
    try comptime testFloorPowerOfTwo();
}

fn testFloorPowerOfTwo() !void {
    try testing.expect(floorPowerOfTwo(u32, 63) == 32);
    try testing.expect(floorPowerOfTwo(u32, 64) == 64);
    try testing.expect(floorPowerOfTwo(u32, 65) == 64);
    try testing.expect(floorPowerOfTwo(u32, 0) == 0);
    try testing.expect(floorPowerOfTwo(u4, 7) == 4);
    try testing.expect(floorPowerOfTwo(u4, 8) == 8);
    try testing.expect(floorPowerOfTwo(u4, 9) == 8);
    try testing.expect(floorPowerOfTwo(u4, 0) == 0);
    try testing.expect(floorPowerOfTwo(i4, 7) == 4);
    try testing.expect(floorPowerOfTwo(i4, -8) == 0);
    try testing.expect(floorPowerOfTwo(i4, -1) == 0);
    try testing.expect(floorPowerOfTwo(i4, 0) == 0);
}

/// Returns the smallest integral value not less than the given floating point number.
/// Uses a dedicated hardware instruction when available.
/// This is the same as calling the builtin @ceil
pub inline fn ceil(value: anytype) @TypeOf(value) {
    return @ceil(value);
}

/// Returns the next power of two (if the value is not already a power of two).
/// Only unsigned integers can be used. Zero is not an allowed input.
/// Result is a type with 1 more bit than the input type.
pub fn ceilPowerOfTwoPromote(comptime T: type, value: T) std.meta.Int(@typeInfo(T).int.signedness, @typeInfo(T).int.bits + 1) {
    comptime assert(@typeInfo(T) == .int);
    comptime assert(@typeInfo(T).int.signedness == .unsigned);
    assert(value != 0);
    const PromotedType = std.meta.Int(@typeInfo(T).int.signedness, @typeInfo(T).int.bits + 1);
    const ShiftType = std.math.Log2Int(PromotedType);
    return @as(PromotedType, 1) << @as(ShiftType, @intCast(@typeInfo(T).int.bits - @clz(value - 1)));
}

/// Returns the next power of two (if the value is not already a power of two).
/// Only unsigned integers can be used. Zero is not an allowed input.
/// If the value doesn't fit, returns an error.
pub fn ceilPowerOfTwo(comptime T: type, value: T) (error{Overflow}!T) {
    comptime assert(@typeInfo(T) == .int);
    const info = @typeInfo(T).int;
    comptime assert(info.signedness == .unsigned);
    const PromotedType = std.meta.Int(info.signedness, info.bits + 1);
    const overflowBit = @as(PromotedType, 1) << info.bits;
    const x = ceilPowerOfTwoPromote(T, value);
    if (overflowBit & x != 0) {
        return error.Overflow;
    }
    return @as(T, @intCast(x));
}

/// Returns the next power of two (if the value is not already a power
/// of two). Only unsigned integers can be used. Zero is not an
/// allowed input. Asserts that the value fits.
pub fn ceilPowerOfTwoAssert(comptime T: type, value: T) T {
    return ceilPowerOfTwo(T, value) catch unreachable;
}

test ceilPowerOfTwoPromote {
    try testCeilPowerOfTwoPromote();
    try comptime testCeilPowerOfTwoPromote();
}

fn testCeilPowerOfTwoPromote() !void {
    try testing.expectEqual(@as(u33, 1), ceilPowerOfTwoPromote(u32, 1));
    try testing.expectEqual(@as(u33, 2), ceilPowerOfTwoPromote(u32, 2));
    try testing.expectEqual(@as(u33, 64), ceilPowerOfTwoPromote(u32, 63));
    try testing.expectEqual(@as(u33, 64), ceilPowerOfTwoPromote(u32, 64));
    try testing.expectEqual(@as(u33, 128), ceilPowerOfTwoPromote(u32, 65));
    try testing.expectEqual(@as(u6, 8), ceilPowerOfTwoPromote(u5, 7));
    try testing.expectEqual(@as(u6, 8), ceilPowerOfTwoPromote(u5, 8));
    try testing.expectEqual(@as(u6, 16), ceilPowerOfTwoPromote(u5, 9));
    try testing.expectEqual(@as(u5, 16), ceilPowerOfTwoPromote(u4, 9));
}

test ceilPowerOfTwo {
    try testCeilPowerOfTwo();
    try comptime testCeilPowerOfTwo();
}

fn testCeilPowerOfTwo() !void {
    try testing.expectEqual(@as(u32, 1), try ceilPowerOfTwo(u32, 1));
    try testing.expectEqual(@as(u32, 2), try ceilPowerOfTwo(u32, 2));
    try testing.expectEqual(@as(u32, 64), try ceilPowerOfTwo(u32, 63));
    try testing.expectEqual(@as(u32, 64), try ceilPowerOfTwo(u32, 64));
    try testing.expectEqual(@as(u32, 128), try ceilPowerOfTwo(u32, 65));
    try testing.expectEqual(@as(u5, 8), try ceilPowerOfTwo(u5, 7));
    try testing.expectEqual(@as(u5, 8), try ceilPowerOfTwo(u5, 8));
    try testing.expectEqual(@as(u5, 16), try ceilPowerOfTwo(u5, 9));
    try testing.expectError(error.Overflow, ceilPowerOfTwo(u4, 9));
}

/// Return the log base 2 of integer value x, rounding down to the
/// nearest integer.
pub fn log2_int(comptime T: type, x: T) Log2Int(T) {
    if (@typeInfo(T) != .int or @typeInfo(T).int.signedness != .unsigned)
        @compileError("log2_int requires an unsigned integer, found " ++ @typeName(T));
    assert(x != 0);
    return @as(Log2Int(T), @intCast(@typeInfo(T).int.bits - 1 - @clz(x)));
}

test log2_int {
    try testing.expect(log2_int(u32, 1) == 0);
    try testing.expect(log2_int(u32, 2) == 1);
    try testing.expect(log2_int(u32, 3) == 1);
    try testing.expect(log2_int(u32, 4) == 2);
    try testing.expect(log2_int(u32, 5) == 2);
    try testing.expect(log2_int(u32, 6) == 2);
    try testing.expect(log2_int(u32, 7) == 2);
    try testing.expect(log2_int(u32, 8) == 3);
    try testing.expect(log2_int(u32, 9) == 3);
    try testing.expect(log2_int(u32, 10) == 3);
}

/// Return the log base 2 of integer value x, rounding up to the
/// nearest integer.
pub fn log2_int_ceil(comptime T: type, x: T) Log2IntCeil(T) {
    if (@typeInfo(T) != .int or @typeInfo(T).int.signedness != .unsigned)
        @compileError("log2_int_ceil requires an unsigned integer, found " ++ @typeName(T));
    assert(x != 0);
    if (x == 1) return 0;
    const log2_val: Log2IntCeil(T) = log2_int(T, x - 1);
    return log2_val + 1;
}

test log2_int_ceil {
    try testing.expect(log2_int_ceil(u32, 1) == 0);
    try testing.expect(log2_int_ceil(u32, 2) == 1);
    try testing.expect(log2_int_ceil(u32, 3) == 2);
    try testing.expect(log2_int_ceil(u32, 4) == 2);
    try testing.expect(log2_int_ceil(u32, 5) == 3);
    try testing.expect(log2_int_ceil(u32, 6) == 3);
    try testing.expect(log2_int_ceil(u32, 7) == 3);
    try testing.expect(log2_int_ceil(u32, 8) == 3);
    try testing.expect(log2_int_ceil(u32, 9) == 4);
    try testing.expect(log2_int_ceil(u32, 10) == 4);
}

/// Cast a value to a different type. If the value doesn't fit in, or
/// can't be perfectly represented by, the new type, it will be
/// converted to the closest possible representation.
pub fn lossyCast(comptime T: type, value: anytype) T {
    switch (@typeInfo(T)) {
        .float => {
            switch (@typeInfo(@TypeOf(value))) {
                .int => return @floatFromInt(value),
                .float => return @floatCast(value),
                .comptime_int => return value,
                .comptime_float => return value,
                else => @compileError("bad type"),
            }
        },
        .int => {
            switch (@typeInfo(@TypeOf(value))) {
                .int, .comptime_int => {
                    if (value >= maxInt(T)) {
                        return maxInt(T);
                    } else if (value <= minInt(T)) {
                        return minInt(T);
                    } else {
                        return @intCast(value);
                    }
                },
                .float, .comptime_float => {
                    // In extreme cases, we probably need a language enhancement to be able to
                    // specify a rounding mode here to prevent `@intFromFloat` panics.
                    const max: @TypeOf(value) = @floatFromInt(maxInt(T));
                    const min: @TypeOf(value) = @floatFromInt(minInt(T));
                    if (isNan(value)) {
                        return 0;
                    } else if (value >= max) {
                        return maxInt(T);
                    } else if (value <= min) {
                        return minInt(T);
                    } else {
                        return @intFromFloat(value);
                    }
                },
                else => @compileError("bad type"),
            }
        },
        else => @compileError("bad result type"),
    }
}

test lossyCast {
    try testing.expect(lossyCast(i16, 70000.0) == @as(i16, 32767));
    try testing.expect(lossyCast(u32, @as(i16, -255)) == @as(u32, 0));
    try testing.expect(lossyCast(i9, @as(u32, 200)) == @as(i9, 200));
    try testing.expect(lossyCast(u32, @as(f32, @floatFromInt(maxInt(u32)))) == maxInt(u32));
    try testing.expect(lossyCast(u32, nan(f32)) == 0);
}

/// Performs linear interpolation between *a* and *b* based on *t*.
/// *t* ranges from 0.0 to 1.0, but may exceed these bounds.
/// Supports floats and vectors of floats.
///
/// This does not guarantee returning *b* if *t* is 1 due to floating-point errors.
/// This is monotonic.
pub fn lerp(a: anytype, b: anytype, t: anytype) @TypeOf(a, b, t) {
    const Type = @TypeOf(a, b, t);
    return @mulAdd(Type, b - a, t, a);
}

test lerp {
    if (builtin.zig_backend == .stage2_c) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/17884
    if (builtin.zig_backend == .stage2_x86_64 and !comptime builtin.cpu.has(.x86, .fma)) return error.SkipZigTest; // https://github.com/ziglang/zig/issues/17884

    try testing.expectEqual(@as(f64, 75), lerp(50, 100, 0.5));
    try testing.expectEqual(@as(f32, 43.75), lerp(50, 25, 0.25));
    try testing.expectEqual(@as(f64, -31.25), lerp(-50, 25, 0.25));

    try testing.expectEqual(@as(f64, 30), lerp(10, 20, 2.0));
    try testing.expectEqual(@as(f64, 5), lerp(10, 20, -0.5));

    try testing.expectApproxEqRel(@as(f32, -7.16067345e+03), lerp(-10000.12345, -5000.12345, 0.56789), 1e-19);
    try testing.expectApproxEqRel(@as(f64, 7.010987590521e+62), lerp(0.123456789e-64, 0.123456789e64, 0.56789), 1e-33);

    try testing.expectEqual(@as(f32, 0.0), lerp(@as(f32, 1.0e8), 1.0, 1.0));
    try testing.expectEqual(@as(f64, 0.0), lerp(@as(f64, 1.0e16), 1.0, 1.0));
    try testing.expectEqual(@as(f32, 1.0), lerp(@as(f32, 1.0e7), 1.0, 1.0));
    try testing.expectEqual(@as(f64, 1.0), lerp(@as(f64, 1.0e15), 1.0, 1.0));

    {
        const a: @Vector(3, f32) = @splat(0);
        const b: @Vector(3, f32) = @splat(50);
        const t: @Vector(3, f32) = @splat(0.5);
        try testing.expectEqual(
            @Vector(3, f32){ 25, 25, 25 },
            lerp(a, b, t),
        );
    }
    {
        const a: @Vector(3, f64) = @splat(50);
        const b: @Vector(3, f64) = @splat(100);
        const t: @Vector(3, f64) = @splat(0.5);
        try testing.expectEqual(
            @Vector(3, f64){ 75, 75, 75 },
            lerp(a, b, t),
        );
    }
    {
        const a: @Vector(2, f32) = @splat(40);
        const b: @Vector(2, f32) = @splat(80);
        const t: @Vector(2, f32) = @Vector(2, f32){ 0.25, 0.75 };
        try testing.expectEqual(
            @Vector(2, f32){ 50, 70 },
            lerp(a, b, t),
        );
    }
}

/// Returns the maximum value of integer type T.
pub fn maxInt(comptime T: type) comptime_int {
    const info = @typeInfo(T);
    const bit_count = info.int.bits;
    if (bit_count == 0) return 0;
    return (1 << (bit_count - @intFromBool(info.int.signedness == .signed))) - 1;
}

/// Returns the minimum value of integer type T.
pub fn minInt(comptime T: type) comptime_int {
    const info = @typeInfo(T);
    const bit_count = info.int.bits;
    if (info.int.signedness == .unsigned) return 0;
    if (bit_count == 0) return 0;
    return -(1 << (bit_count - 1));
}

test maxInt {
    try testing.expect(maxInt(u0) == 0);
    try testing.expect(maxInt(u1) == 1);
    try testing.expect(maxInt(u8) == 255);
    try testing.expect(maxInt(u16) == 65535);
    try testing.expect(maxInt(u32) == 4294967295);
    try testing.expect(maxInt(u64) == 18446744073709551615);
    try testing.expect(maxInt(u128) == 340282366920938463463374607431768211455);

    try testing.expect(maxInt(i0) == 0);
    try testing.expect(maxInt(i1) == 0);
    try testing.expect(maxInt(i8) == 127);
    try testing.expect(maxInt(i16) == 32767);
    try testing.expect(maxInt(i32) == 2147483647);
    try testing.expect(maxInt(i63) == 4611686018427387903);
    try testing.expect(maxInt(i64) == 9223372036854775807);
    try testing.expect(maxInt(i128) == 170141183460469231731687303715884105727);
}

test minInt {
    try testing.expect(minInt(u0) == 0);
    try testing.expect(minInt(u1) == 0);
    try testing.expect(minInt(u8) == 0);
    try testing.expect(minInt(u16) == 0);
    try testing.expect(minInt(u32) == 0);
    try testing.expect(minInt(u63) == 0);
    try testing.expect(minInt(u64) == 0);
    try testing.expect(minInt(u128) == 0);

    try testing.expect(minInt(i0) == 0);
    try testing.expect(minInt(i1) == -1);
    try testing.expect(minInt(i8) == -128);
    try testing.expect(minInt(i16) == -32768);
    try testing.expect(minInt(i32) == -2147483648);
    try testing.expect(minInt(i63) == -4611686018427387904);
    try testing.expect(minInt(i64) == -9223372036854775808);
    try testing.expect(minInt(i128) == -170141183460469231731687303715884105728);
}

test "max value type" {
    const x: u32 = maxInt(i32);
    try testing.expect(x == 2147483647);
}

/// Multiply a and b. Return type is wide enough to guarantee no
/// overflow.
pub fn mulWide(comptime T: type, a: T, b: T) std.meta.Int(
    @typeInfo(T).int.signedness,
    @typeInfo(T).int.bits * 2,
) {
    const ResultInt = std.meta.Int(
        @typeInfo(T).int.signedness,
        @typeInfo(T).int.bits * 2,
    );
    return @as(ResultInt, a) * @as(ResultInt, b);
}

test mulWide {
    try testing.expect(mulWide(u8, 5, 5) == 25);
    try testing.expect(mulWide(i8, 5, -5) == -25);
    try testing.expect(mulWide(u8, 100, 100) == 10000);
}

/// See also `CompareOperator`.
pub const Order = enum {
    /// Greater than (`>`)
    gt,

    /// Less than (`<`)
    lt,

    /// Equal (`==`)
    eq,

    pub fn invert(self: Order) Order {
        return switch (self) {
            .lt => .gt,
            .eq => .eq,
            .gt => .lt,
        };
    }

    test invert {
        try testing.expect(Order.invert(order(0, 0)) == .eq);
        try testing.expect(Order.invert(order(1, 0)) == .lt);
        try testing.expect(Order.invert(order(-1, 0)) == .gt);
    }

    pub fn differ(self: Order) ?Order {
        return if (self != .eq) self else null;
    }

    test differ {
        const neg: i32 = -1;
        const zero: i32 = 0;
        const pos: i32 = 1;
        try testing.expect(order(zero, neg).differ() orelse
            order(pos, zero) == .gt);
        try testing.expect(order(zero, zero).differ() orelse
            order(zero, zero) == .eq);
        try testing.expect(order(pos, pos).differ() orelse
            order(neg, zero) == .lt);
        try testing.expect(order(zero, zero).differ() orelse
            order(pos, neg).differ() orelse
            order(neg, zero) == .gt);
        try testing.expect(order(pos, pos).differ() orelse
            order(pos, pos).differ() orelse
            order(neg, neg) == .eq);
        try testing.expect(order(zero, pos).differ() orelse
            order(neg, pos).differ() orelse
            order(pos, neg) == .lt);
    }

    pub fn compare(self: Order, op: CompareOperator) bool {
        return switch (self) {
            .lt => switch (op) {
                .lt => true,
                .lte => true,
                .eq => false,
                .gte => false,
                .gt => false,
                .neq => true,
            },
            .eq => switch (op) {
                .lt => false,
                .lte => true,
                .eq => true,
                .gte => true,
                .gt => false,
                .neq => false,
            },
            .gt => switch (op) {
                .lt => false,
                .lte => false,
                .eq => false,
                .gte => true,
                .gt => true,
                .neq => true,
            },
        };
    }

    // https://github.com/ziglang/zig/issues/19295
    test "compare" {
        try testing.expect(order(-1, 0).compare(.lt));
        try testing.expect(order(-1, 0).compare(.lte));
        try testing.expect(order(0, 0).compare(.lte));
        try testing.expect(order(0, 0).compare(.eq));
        try testing.expect(order(0, 0).compare(.gte));
        try testing.expect(order(1, 0).compare(.gte));
        try testing.expect(order(1, 0).compare(.gt));
        try testing.expect(order(1, 0).compare(.neq));
    }
};

/// Given two numbers, this function returns the order they are with respect to each other.
pub fn order(a: anytype, b: anytype) Order {
    if (a == b) {
        return .eq;
    } else if (a < b) {
        return .lt;
    } else if (a > b) {
        return .gt;
    } else {
        unreachable;
    }
}

/// See also `Order`.
pub const CompareOperator = enum {
    /// Less than (`<`)
    lt,
    /// Less than or equal (`<=`)
    lte,
    /// Equal (`==`)
    eq,
    /// Greater than or equal (`>=`)
    gte,
    /// Greater than (`>`)
    gt,
    /// Not equal (`!=`)
    neq,

    /// Reverse the direction of the comparison.
    /// Use when swapping the left and right hand operands.
    pub fn reverse(op: CompareOperator) CompareOperator {
        return switch (op) {
            .lt => .gt,
            .lte => .gte,
            .gt => .lt,
            .gte => .lte,
            .eq => .eq,
            .neq => .neq,
        };
    }

    test reverse {
        inline for (@typeInfo(CompareOperator).@"enum".fields) |op_field| {
            const op = @as(CompareOperator, @enumFromInt(op_field.value));
            try testing.expect(compare(2, op, 3) == compare(3, op.reverse(), 2));
            try testing.expect(compare(3, op, 3) == compare(3, op.reverse(), 3));
            try testing.expect(compare(4, op, 3) == compare(3, op.reverse(), 4));
        }
    }
};

/// This function does the same thing as comparison operators, however the
/// operator is a runtime-known enum value. Works on any operands that
/// support comparison operators.
pub fn compare(a: anytype, op: CompareOperator, b: anytype) bool {
    return switch (op) {
        .lt => a < b,
        .lte => a <= b,
        .eq => a == b,
        .neq => a != b,
        .gt => a > b,
        .gte => a >= b,
    };
}

test compare {
    try testing.expect(compare(@as(i8, -1), .lt, @as(u8, 255)));
    try testing.expect(compare(@as(i8, 2), .gt, @as(u8, 1)));
    try testing.expect(!compare(@as(i8, -1), .gte, @as(u8, 255)));
    try testing.expect(compare(@as(u8, 255), .gt, @as(i8, -1)));
    try testing.expect(!compare(@as(u8, 255), .lte, @as(i8, -1)));
    try testing.expect(compare(@as(i8, -1), .lt, @as(u9, 255)));
    try testing.expect(!compare(@as(i8, -1), .gte, @as(u9, 255)));
    try testing.expect(compare(@as(u9, 255), .gt, @as(i8, -1)));
    try testing.expect(!compare(@as(u9, 255), .lte, @as(i8, -1)));
    try testing.expect(compare(@as(i9, -1), .lt, @as(u8, 255)));
    try testing.expect(!compare(@as(i9, -1), .gte, @as(u8, 255)));
    try testing.expect(compare(@as(u8, 255), .gt, @as(i9, -1)));
    try testing.expect(!compare(@as(u8, 255), .lte, @as(i9, -1)));
    try testing.expect(compare(@as(u8, 1), .lt, @as(u8, 2)));
    try testing.expect(@as(u8, @bitCast(@as(i8, -1))) == @as(u8, 255));
    try testing.expect(!compare(@as(u8, 255), .eq, @as(i8, -1)));
    try testing.expect(compare(@as(u8, 1), .eq, @as(u8, 1)));
}

test order {
    try testing.expect(order(0, 0) == .eq);
    try testing.expect(order(1, 0) == .gt);
    try testing.expect(order(-1, 0) == .lt);
}

/// Returns a mask of all ones if value is true,
/// and a mask of all zeroes if value is false.
/// Compiles to one instruction for register sized integers.
pub inline fn boolMask(comptime MaskInt: type, value: bool) MaskInt {
    if (@typeInfo(MaskInt) != .int)
        @compileError("boolMask requires an integer mask type.");

    if (MaskInt == u0 or MaskInt == i0)
        @compileError("boolMask cannot convert to u0 or i0, they are too small.");

    // The u1 and i1 cases tend to overflow,
    // so we special case them here.
    if (MaskInt == u1) return @intFromBool(value);
    if (MaskInt == i1) {
        // The @as here is a workaround for #7950
        return @as(i1, @bitCast(@as(u1, @intFromBool(value))));
    }

    return -%@as(MaskInt, @intCast(@intFromBool(value)));
}

test boolMask {
    const runTest = struct {
        fn runTest() !void {
            try testing.expectEqual(@as(u1, 0), boolMask(u1, false));
            try testing.expectEqual(@as(u1, 1), boolMask(u1, true));

            try testing.expectEqual(@as(i1, 0), boolMask(i1, false));
            try testing.expectEqual(@as(i1, -1), boolMask(i1, true));

            try testing.expectEqual(@as(u13, 0), boolMask(u13, false));
            try testing.expectEqual(@as(u13, 0x1FFF), boolMask(u13, true));

            try testing.expectEqual(@as(i13, 0), boolMask(i13, false));
            try testing.expectEqual(@as(i13, -1), boolMask(i13, true));

            try testing.expectEqual(@as(u32, 0), boolMask(u32, false));
            try testing.expectEqual(@as(u32, 0xFFFF_FFFF), boolMask(u32, true));

            try testing.expectEqual(@as(i32, 0), boolMask(i32, false));
            try testing.expectEqual(@as(i32, -1), boolMask(i32, true));
        }
    }.runTest;
    try runTest();
    try comptime runTest();
}

/// Return the mod of `num` with the smallest integer type
pub fn comptimeMod(num: anytype, comptime denom: comptime_int) IntFittingRange(0, denom - 1) {
    return @as(IntFittingRange(0, denom - 1), @intCast(@mod(num, denom)));
}

pub const F80 = struct {
    fraction: u64,
    exp: u16,

    pub fn toFloat(self: F80) f80 {
        const int = (@as(u80, self.exp) << 64) | self.fraction;
        return @as(f80, @bitCast(int));
    }

    pub fn fromFloat(x: f80) F80 {
        const int = @as(u80, @bitCast(x));
        return .{
            .fraction = @as(u64, @truncate(int)),
            .exp = @as(u16, @truncate(int >> 64)),
        };
    }
};

fn SignOf(T: type) type {
    return switch (@typeInfo(T)) {
        .comptime_int, .comptime_float => comptime_int,
        .int => IntFittingRange(@max(minInt(T), -1), @min(maxInt(T), 1)),
        .float => IntFittingRange(-1, 1),
        .vector => |vec| @Vector(vec.len, SignOf(vec.child)),
        else => @compileError("Expected an int, float, or a vector of one, found " ++ @typeName(T)),
    };
}

/// Returns -1, 0, or 1.
/// Supports integer and float types and vectors of integer and float types.
/// Unsigned integer types will always return 0 or 1.
/// The returned integer type is the smallest that fits the possible values.
/// Branchless.
pub inline fn sign(n: anytype) SignOf(@TypeOf(n)) {
    const T = SignOf(@TypeOf(n));
    const zero: T = if (@typeInfo(T) == .vector) @splat(0) else 0;
    const pos: T = @intCast(@intFromBool(n > zero));
    const neg: T = @intCast(@intFromBool(n < zero));
    return pos - neg;
}

fn testSign() !void {
    // each of the following blocks checks the inputs
    // 2, -2, 0, { 2, -2, 0 } provide expected output
    // 1, -1, 0, { 1, -1, 0 } for the given T
    // (negative values omitted for unsigned types)
    {
        const T = i8;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, -1), sign(@as(T, -2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(3, T){ 1, -1, 0 }, sign(@Vector(3, T){ 2, -2, 0 }));
    }
    {
        const T = i32;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, -1), sign(@as(T, -2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(3, T){ 1, -1, 0 }, sign(@Vector(3, T){ 2, -2, 0 }));
    }
    {
        const T = i64;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, -1), sign(@as(T, -2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(3, T){ 1, -1, 0 }, sign(@Vector(3, T){ 2, -2, 0 }));
    }
    {
        const T = u8;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(2, T){ 1, 0 }, sign(@Vector(2, T){ 2, 0 }));
    }
    {
        const T = u32;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(2, T){ 1, 0 }, sign(@Vector(2, T){ 2, 0 }));
    }
    {
        const T = u64;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(2, T){ 1, 0 }, sign(@Vector(2, T){ 2, 0 }));
    }
    {
        const T = f16;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, -1), sign(@as(T, -2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(3, T){ 1, -1, 0 }, sign(@Vector(3, T){ 2, -2, 0 }));
    }
    {
        const T = f32;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, -1), sign(@as(T, -2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(3, T){ 1, -1, 0 }, sign(@Vector(3, T){ 2, -2, 0 }));
    }
    {
        const T = f64;
        try std.testing.expectEqual(@as(T, 1), sign(@as(T, 2)));
        try std.testing.expectEqual(@as(T, -1), sign(@as(T, -2)));
        try std.testing.expectEqual(@as(T, 0), sign(@as(T, 0)));
        try std.testing.expectEqual(@Vector(3, T){ 1, -1, 0 }, sign(@Vector(3, T){ 2, -2, 0 }));
    }

    // comptime_int
    try std.testing.expectEqual(-1, sign(-10));
    try std.testing.expectEqual(1, sign(10));
    try std.testing.expectEqual(0, sign(0));
    // comptime_float
    try std.testing.expectEqual(-1.0, sign(-10.0));
    try std.testing.expectEqual(1.0, sign(10.0));
    try std.testing.expectEqual(0.0, sign(0.0));
}

test sign {
    try testSign();
    try comptime testSign();
}



---
File: /std/mem.zig
---

const std = @import("std.zig");
const builtin = @import("builtin");
const debug = std.debug;
const assert = debug.assert;
const math = std.math;
const mem = @This();
const testing = std.testing;
const Endian = std.builtin.Endian;
const native_endian = builtin.cpu.arch.endian();

/// The standard library currently thoroughly depends on byte size
/// being 8 bits.  (see the use of u8 throughout allocation code as
/// the "byte" type.)  Code which depends on this can reference this
/// declaration.  If we ever try to port the standard library to a
/// non-8-bit-byte platform, this will allow us to search for things
/// which need to be updated.
pub const byte_size_in_bits = 8;

pub const Allocator = @import("mem/Allocator.zig");

/// Stored as a power-of-two.
pub const Alignment = enum(math.Log2Int(usize)) {
    @"1" = 0,
    @"2" = 1,
    @"4" = 2,
    @"8" = 3,
    @"16" = 4,
    @"32" = 5,
    @"64" = 6,
    _,

    pub fn toByteUnits(a: Alignment) usize {
        return @as(usize, 1) << @intFromEnum(a);
    }

    pub fn fromByteUnits(n: usize) Alignment {
        assert(std.math.isPowerOfTwo(n));
        return @enumFromInt(@ctz(n));
    }

    pub fn fromByteUnitsOptional(maybe_n: ?usize) ?Alignment {
        return if (maybe_n) |n| .fromByteUnits(n) else null;
    }

    pub inline fn of(comptime T: type) Alignment {
        return comptime fromByteUnits(@alignOf(T));
    }

    pub fn order(lhs: Alignment, rhs: Alignment) std.math.Order {
        return std.math.order(@intFromEnum(lhs), @intFromEnum(rhs));
    }

    pub fn compare(lhs: Alignment, op: std.math.CompareOperator, rhs: Alignment) bool {
        return std.math.compare(@intFromEnum(lhs), op, @intFromEnum(rhs));
    }

    pub fn max(lhs: Alignment, rhs: Alignment) Alignment {
        return @enumFromInt(@max(@intFromEnum(lhs), @intFromEnum(rhs)));
    }

    pub fn min(lhs: Alignment, rhs: Alignment) Alignment {
        return @enumFromInt(@min(@intFromEnum(lhs), @intFromEnum(rhs)));
    }

    /// Return next address with this alignment.
    pub fn forward(a: Alignment, address: usize) usize {
        const x = (@as(usize, 1) << @intFromEnum(a)) - 1;
        return (address + x) & ~x;
    }

    /// Return previous address with this alignment.
    pub fn backward(a: Alignment, address: usize) usize {
        const x = (@as(usize, 1) << @intFromEnum(a)) - 1;
        return address & ~x;
    }

    /// Return whether address is aligned to this amount.
    pub fn check(a: Alignment, address: usize) bool {
        return @ctz(address) >= @intFromEnum(a);
    }
};

/// Detects and asserts if the std.mem.Allocator interface is violated by the caller
/// or the allocator.
pub fn ValidationAllocator(comptime T: type) type {
    return struct {
        const Self = @This();

        underlying_allocator: T,

        pub fn init(underlying_allocator: T) @This() {
            return .{
                .underlying_allocator = underlying_allocator,
            };
        }

        pub fn allocator(self: *Self) Allocator {
            return .{
                .ptr = self,
                .vtable = &.{
                    .alloc = alloc,
                    .resize = resize,
                    .remap = remap,
                    .free = free,
                },
            };
        }

        fn getUnderlyingAllocatorPtr(self: *Self) Allocator {
            if (T == Allocator) return self.underlying_allocator;
            return self.underlying_allocator.allocator();
        }

        pub fn alloc(
            ctx: *anyopaque,
            n: usize,
            alignment: mem.Alignment,
            ret_addr: usize,
        ) ?[*]u8 {
            assert(n > 0);
            const self: *Self = @ptrCast(@alignCast(ctx));
            const underlying = self.getUnderlyingAllocatorPtr();
            const result = underlying.rawAlloc(n, alignment, ret_addr) orelse
                return null;
            assert(alignment.check(@intFromPtr(result)));
            return result;
        }

        pub fn resize(
            ctx: *anyopaque,
            buf: []u8,
            alignment: Alignment,
            new_len: usize,
            ret_addr: usize,
        ) bool {
            const self: *Self = @ptrCast(@alignCast(ctx));
            assert(buf.len > 0);
            const underlying = self.getUnderlyingAllocatorPtr();
            return underlying.rawResize(buf, alignment, new_len, ret_addr);
        }

        pub fn remap(
            ctx: *anyopaque,
            buf: []u8,
            alignment: Alignment,
            new_len: usize,
            ret_addr: usize,
        ) ?[*]u8 {
            const self: *Self = @ptrCast(@alignCast(ctx));
            assert(buf.len > 0);
            const underlying = self.getUnderlyingAllocatorPtr();
            return underlying.rawRemap(buf, alignment, new_len, ret_addr);
        }

        pub fn free(
            ctx: *anyopaque,
            buf: []u8,
            alignment: Alignment,
            ret_addr: usize,
        ) void {
            const self: *Self = @ptrCast(@alignCast(ctx));
            assert(buf.len > 0);
            const underlying = self.getUnderlyingAllocatorPtr();
            underlying.rawFree(buf, alignment, ret_addr);
        }

        pub fn reset(self: *Self) void {
            self.underlying_allocator.reset();
        }
    };
}

/// Wraps an allocator with basic validation checks.
/// Asserts that allocation sizes are greater than zero and returned pointers have correct alignment.
pub fn validationWrap(allocator: anytype) ValidationAllocator(@TypeOf(allocator)) {
    return ValidationAllocator(@TypeOf(allocator)).init(allocator);
}

test "Allocator basics" {
    try testing.expectError(error.OutOfMemory, testing.failing_allocator.alloc(u8, 1));
    try testing.expectError(error.OutOfMemory, testing.failing_allocator.allocSentinel(u8, 1, 0));
}

test "Allocator.resize" {
    const primitiveIntTypes = .{
        i8,
        u8,
        i16,
        u16,
        i32,
        u32,
        i64,
        u64,
        i128,
        u128,
        isize,
        usize,
    };
    inline for (primitiveIntTypes) |T| {
        var values = try testing.allocator.alloc(T, 100);
        defer testing.allocator.free(values);

        for (values, 0..) |*v, i| v.* = @as(T, @intCast(i));
        if (!testing.allocator.resize(values, values.len + 10)) return error.OutOfMemory;
        values = values.ptr[0 .. values.len + 10];
        try testing.expect(values.len == 110);
    }

    const primitiveFloatTypes = .{
        f16,
        f32,
        f64,
        f128,
    };
    inline for (primitiveFloatTypes) |T| {
        var values = try testing.allocator.alloc(T, 100);
        defer testing.allocator.free(values);

        for (values, 0..) |*v, i| v.* = @as(T, @floatFromInt(i));
        if (!testing.allocator.resize(values, values.len + 10)) return error.OutOfMemory;
        values = values.ptr[0 .. values.len + 10];
        try testing.expect(values.len == 110);
    }
}

test "Allocator alloc and remap with zero-bit type" {
    var values = try testing.allocator.alloc(void, 10);
    defer testing.allocator.free(values);

    try testing.expectEqual(10, values.len);
    const remaped = testing.allocator.remap(values, 200);
    try testing.expect(remaped != null);

    values = remaped.?;
    try testing.expectEqual(200, values.len);
}

/// Copy all of source into dest at position 0.
/// dest.len must be >= source.len.
/// If the slices overlap, dest.ptr must be <= src.ptr.
/// This function is deprecated; use @memmove instead.
pub fn copyForwards(comptime T: type, dest: []T, source: []const T) void {
    for (dest[0..source.len], source) |*d, s| d.* = s;
}

/// Copy all of source into dest at position 0.
/// dest.len must be >= source.len.
/// If the slices overlap, dest.ptr must be >= src.ptr.
/// This function is deprecated; use @memmove instead.
pub fn copyBackwards(comptime T: type, dest: []T, source: []const T) void {
    // TODO instead of manually doing this check for the whole array
    // and turning off runtime safety, the compiler should detect loops like
    // this and automatically omit safety checks for loops
    @setRuntimeSafety(false);
    assert(dest.len >= source.len);
    var i = source.len;
    while (i > 0) {
        i -= 1;
        dest[i] = source[i];
    }
}

/// Generally, Zig users are encouraged to explicitly initialize all fields of a struct explicitly rather than using this function.
/// However, it is recognized that there are sometimes use cases for initializing all fields to a "zero" value. For example, when
/// interfacing with a C API where this practice is more common and relied upon. If you are performing code review and see this
/// function used, examine closely - it may be a code smell.
/// Zero initializes the type.
/// This can be used to zero-initialize any type for which it makes sense. Structs will be initialized recursively.
pub fn zeroes(comptime T: type) T {
    switch (@typeInfo(T)) {
        .comptime_int, .int, .comptime_float, .float => {
            return @as(T, 0);
        },
        .@"enum" => {
            return @as(T, @enumFromInt(0));
        },
        .void => {
            return {};
        },
        .bool => {
            return false;
        },
        .optional, .null => {
            return null;
        },
        .@"struct" => |struct_info| {
            if (@sizeOf(T) == 0) return undefined;
            if (struct_info.layout == .@"extern") {
                var item: T = undefined;
                @memset(asBytes(&item), 0);
                return item;
            } else {
                var structure: T = undefined;
                inline for (struct_info.fields) |field| {
                    if (!field.is_comptime) {
                        @field(structure, field.name) = zeroes(field.type);
                    }
                }
                return structure;
            }
        },
        .pointer => |ptr_info| {
            switch (ptr_info.size) {
                .slice => {
                    if (ptr_info.sentinel()) |sentinel| {
                        if (ptr_info.child == u8 and sentinel == 0) {
                            return ""; // A special case for the most common use-case: null-terminated strings.
                        }
                        @compileError("Can't set a sentinel slice to zero. This would require allocating memory.");
                    } else {
                        return &[_]ptr_info.child{};
                    }
                },
                .c => {
                    return null;
                },
                .one, .many => {
                    if (ptr_info.is_allowzero) return @ptrFromInt(0);
                    @compileError("Only nullable and allowzero pointers can be set to zero.");
                },
            }
        },
        .array => |info| {
            return @splat(zeroes(info.child));
        },
        .vector => |info| {
            return @splat(zeroes(info.child));
        },
        .@"union" => |info| {
            if (info.layout == .@"extern") {
                var item: T = undefined;
                @memset(asBytes(&item), 0);
                return item;
            }
            @compileError("Can't set a " ++ @typeName(T) ++ " to zero.");
        },
        .enum_literal,
        .error_union,
        .error_set,
        .@"fn",
        .type,
        .noreturn,
        .undefined,
        .@"opaque",
        .frame,
        .@"anyframe",
        => {
            @compileError("Can't set a " ++ @typeName(T) ++ " to zero.");
        },
    }
}

test zeroes {
    const C_struct = extern struct {
        x: u32,
        y: u32 align(128),
    };

    var a = zeroes(C_struct);

    // Extern structs should have padding zeroed out.
    try testing.expectEqualSlices(u8, &[_]u8{0} ** @sizeOf(@TypeOf(a)), asBytes(&a));

    a.y += 10;

    try testing.expect(a.x == 0);
    try testing.expect(a.y == 10);

    const ZigStruct = struct {
        comptime comptime_field: u8 = 5,

        integral_types: struct {
            integer_0: i0,
            integer_8: i8,
            integer_16: i16,
            integer_32: i32,
            integer_64: i64,
            integer_128: i128,
            unsigned_0: u0,
            unsigned_8: u8,
            unsigned_16: u16,
            unsigned_32: u32,
            unsigned_64: u64,
            unsigned_128: u128,

            float_32: f32,
            float_64: f64,
        },

        pointers: struct {
            optional: ?*u8,
            c_pointer: [*c]u8,
            slice: []u8,
            nullTerminatedString: [:0]const u8,
        },

        array: [2]u32,
        vector_u32: @Vector(2, u32),
        vector_f32: @Vector(2, f32),
        vector_bool: @Vector(2, bool),
        optional_int: ?u8,
        empty: void,
        sentinel: [3:0]u8,
    };

    const b = zeroes(ZigStruct);
    try testing.expectEqual(@as(u8, 5), b.comptime_field);
    try testing.expectEqual(@as(i8, 0), b.integral_types.integer_0);
    try testing.expectEqual(@as(i8, 0), b.integral_types.integer_8);
    try testing.expectEqual(@as(i16, 0), b.integral_types.integer_16);
    try testing.expectEqual(@as(i32, 0), b.integral_types.integer_32);
    try testing.expectEqual(@as(i64, 0), b.integral_types.integer_64);
    try testing.expectEqual(@as(i128, 0), b.integral_types.integer_128);
    try testing.expectEqual(@as(u8, 0), b.integral_types.unsigned_0);
    try testing.expectEqual(@as(u8, 0), b.integral_types.unsigned_8);
    try testing.expectEqual(@as(u16, 0), b.integral_types.unsigned_16);
    try testing.expectEqual(@as(u32, 0), b.integral_types.unsigned_32);
    try testing.expectEqual(@as(u64, 0), b.integral_types.unsigned_64);
    try testing.expectEqual(@as(u128, 0), b.integral_types.unsigned_128);
    try testing.expectEqual(@as(f32, 0), b.integral_types.float_32);
    try testing.expectEqual(@as(f64, 0), b.integral_types.float_64);
    try testing.expectEqual(@as(?*u8, null), b.pointers.optional);
    try testing.expectEqual(@as([*c]u8, null), b.pointers.c_pointer);
    try testing.expectEqual(@as([]u8, &[_]u8{}), b.pointers.slice);
    try testing.expectEqual(@as([:0]const u8, ""), b.pointers.nullTerminatedString);
    for (b.array) |e| {
        try testing.expectEqual(@as(u32, 0), e);
    }
    try testing.expectEqual(@as(@TypeOf(b.vector_u32), @splat(0)), b.vector_u32);
    try testing.expectEqual(@as(@TypeOf(b.vector_f32), @splat(0.0)), b.vector_f32);
    if (!(builtin.zig_backend == .stage2_llvm and builtin.cpu.arch == .hexagon)) {
        try testing.expectEqual(@as(@TypeOf(b.vector_bool), @splat(false)), b.vector_bool);
    }
    try testing.expectEqual(@as(?u8, null), b.optional_int);
    for (b.sentinel) |e| {
        try testing.expectEqual(@as(u8, 0), e);
    }

    const C_union = extern union {
        a: u8,
        b: u32,
    };

    const c = zeroes(C_union);
    try testing.expectEqual(@as(u8, 0), c.a);
    try testing.expectEqual(@as(u32, 0), c.b);

    const comptime_union = comptime zeroes(C_union);
    try testing.expectEqual(@as(u8, 0), comptime_union.a);
    try testing.expectEqual(@as(u32, 0), comptime_union.b);

    // Ensure zero sized struct with fields is initialized correctly.
    _ = zeroes(struct { handle: void });
}

/// Initializes all fields of the struct with their default value, or zero values if no default value is present.
/// If the field is present in the provided initial values, it will have that value instead.
/// Structs are initialized recursively.
pub fn zeroInit(comptime T: type, init: anytype) T {
    const Init = @TypeOf(init);

    switch (@typeInfo(T)) {
        .@"struct" => |struct_info| {
            switch (@typeInfo(Init)) {
                .@"struct" => |init_info| {
                    if (init_info.is_tuple) {
                        if (init_info.fields.len > struct_info.fields.len) {
                            @compileError("Tuple initializer has more elements than there are fields in `" ++ @typeName(T) ++ "`");
                        }
                    } else {
                        inline for (init_info.fields) |field| {
                            if (!@hasField(T, field.name)) {
                                @compileError("Encountered an initializer for `" ++ field.name ++ "`, but it is not a field of " ++ @typeName(T));
                            }
                        }
                    }

                    var value: T = if (struct_info.layout == .@"extern") zeroes(T) else undefined;

                    inline for (struct_info.fields, 0..) |field, i| {
                        if (field.is_comptime) {
                            continue;
                        }

                        if (init_info.is_tuple and init_info.fields.len > i) {
                            @field(value, field.name) = @field(init, init_info.fields[i].name);
                        } else if (@hasField(@TypeOf(init), field.name)) {
                            switch (@typeInfo(field.type)) {
                                .@"struct" => {
                                    @field(value, field.name) = zeroInit(field.type, @field(init, field.name));
                                },
                                else => {
                                    @field(value, field.name) = @field(init, field.name);
                                },
                            }
                        } else if (field.defaultValue()) |val| {
                            @field(value, field.name) = val;
                        } else {
                            switch (@typeInfo(field.type)) {
                                .@"struct" => {
                                    @field(value, field.name) = std.mem.zeroInit(field.type, .{});
                                },
                                else => {
                                    @field(value, field.name) = std.mem.zeroes(@TypeOf(@field(value, field.name)));
                                },
                            }
                        }
                    }

                    return value;
                },
                else => {
                    @compileError("The initializer must be a struct");
                },
            }
        },
        else => {
            @compileError("Can't default init a " ++ @typeName(T));
        },
    }
}

test zeroInit {
    const I = struct {
        d: f64,
    };

    const S = struct {
        a: u32,
        b: ?bool,
        c: I,
        e: [3]u8,
        f: i64 = -1,
    };

    const s = zeroInit(S, .{
        .a = 42,
    });

    try testing.expectEqual(S{
        .a = 42,
        .b = null,
        .c = .{
            .d = 0,
        },
        .e = [3]u8{ 0, 0, 0 },
        .f = -1,
    }, s);

    const Color = struct {
        r: u8,
        g: u8,
        b: u8,
        a: u8,
    };

    const c = zeroInit(Color, .{ 255, 255 });
    try testing.expectEqual(Color{
        .r = 255,
        .g = 255,
        .b = 0,
        .a = 0,
    }, c);

    const Foo = struct {
        foo: u8 = 69,
        bar: u8,
    };

    const f = zeroInit(Foo, .{});
    try testing.expectEqual(Foo{
        .foo = 69,
        .bar = 0,
    }, f);

    const Bar = struct {
        foo: u32 = 666,
        bar: u32 = 420,
    };

    const b = zeroInit(Bar, .{69});
    try testing.expectEqual(Bar{
        .foo = 69,
        .bar = 420,
    }, b);

    const Baz = struct {
        foo: [:0]const u8 = "bar",
    };

    const baz1 = zeroInit(Baz, .{});
    try testing.expectEqual(Baz{}, baz1);

    const baz2 = zeroInit(Baz, .{ .foo = "zab" });
    try testing.expectEqualSlices(u8, "zab", baz2.foo);

    const NestedBaz = struct {
        bbb: Baz,
    };
    const nested_baz = zeroInit(NestedBaz, .{});
    try testing.expectEqual(NestedBaz{
        .bbb = Baz{},
    }, nested_baz);
}

/// Sorts a slice in-place using a stable algorithm (maintains relative order of equal elements).
/// Average time complexity: O(n log n), worst case: O(n log n)
/// Space complexity: O(log n) for recursive calls
///
/// For slice of primitives with default ordering, consider using `std.sort.block` directly.
/// For unstable but potentially faster sorting, see `sortUnstable`.
pub fn sort(
    comptime T: type,
    items: []T,
    context: anytype,
    comptime lessThanFn: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    std.sort.block(T, items, context, lessThanFn);
}

/// Sorts a slice in-place using an unstable algorithm (does not preserve relative order of equal elements).
/// Time complexity: O(n) best case, O(n log n) worst case and average case.
/// Generally faster than stable sort but order of equal elements is undefined.
///
/// Uses pattern-defeating quicksort (PDQ) algorithm which performs well on many data patterns.
/// For stable sorting that preserves equal element order, use `sort`.
pub fn sortUnstable(
    comptime T: type,
    items: []T,
    context: anytype,
    comptime lessThanFn: fn (@TypeOf(context), lhs: T, rhs: T) bool,
) void {
    std.sort.pdq(T, items, context, lessThanFn);
}

/// TODO: currently this just calls `insertionSortContext`. The block sort implementation
/// in this file needs to be adapted to use the sort context.
pub fn sortContext(a: usize, b: usize, context: anytype) void {
    std.sort.insertionContext(a, b, context);
}

/// Sorts a range [a, b) using an unstable algorithm with custom context.
/// This is a lower-level interface for sorting that works with indices instead of slices.
/// Does not preserve relative order of equal elements.
///
/// The context must provide lessThan(a_idx, b_idx) and swap(a_idx, b_idx) methods.
/// Uses pattern-defeating quicksort (PDQ) algorithm.
pub fn sortUnstableContext(a: usize, b: usize, context: anytype) void {
    std.sort.pdqContext(a, b, context);
}

/// Compares two slices of numbers lexicographically. O(n).
pub fn order(comptime T: type, lhs: []const T, rhs: []const T) math.Order {
    if (lhs.ptr != rhs.ptr) {
        const n = @min(lhs.len, rhs.len);
        for (lhs[0..n], rhs[0..n]) |lhs_elem, rhs_elem| {
            switch (math.order(lhs_elem, rhs_elem)) {
                .eq => continue,
                .lt => return .lt,
                .gt => return .gt,
            }
        }
    }
    return math.order(lhs.len, rhs.len);
}

/// Compares two many-item pointers with NUL-termination lexicographically.
pub fn orderZ(comptime T: type, lhs: [*:0]const T, rhs: [*:0]const T) math.Order {
    return boundedOrderZ(T, lhs, rhs, std.math.maxInt(usize));
}

/// Compares two many-item pointers with NUL-termination lexicographically until some specified bound.
pub fn boundedOrderZ(comptime T: type, lhs: [*:0]const T, rhs: [*:0]const T, bound: usize) math.Order {
    if (lhs == rhs) return .eq;
    var i: usize = 0;
    while (lhs[i] == rhs[i] and lhs[i] != 0 and i < bound) : (i += 1) {}
    return if (i < bound) math.order(lhs[i], rhs[i]) else .eq;
}

test order {
    try testing.expect(order(u8, "abcd", "bee") == .lt);
    try testing.expect(order(u8, "abc", "abc") == .eq);
    try testing.expect(order(u8, "abc", "abc0") == .lt);
    try testing.expect(order(u8, "", "") == .eq);
    try testing.expect(order(u8, "", "a") == .lt);

    const s: []const u8 = "abc";
    try testing.expect(order(u8, s, s) == .eq);
    try testing.expect(order(u8, s[0..2], s) == .lt);
}

test orderZ {
    try testing.expect(orderZ(u8, "abcd", "bee") == .lt);
    try testing.expect(orderZ(u8, "abc", "abc") == .eq);
    try testing.expect(orderZ(u8, "abc", "abc0") == .lt);
    try testing.expect(orderZ(u8, "", "") == .eq);
    try testing.expect(orderZ(u8, "", "a") == .lt);

    const s: [*:0]const u8 = "abc";
    try testing.expect(orderZ(u8, s, s) == .eq);
}

/// Returns true if lhs < rhs, false otherwise
pub fn lessThan(comptime T: type, lhs: []const T, rhs: []const T) bool {
    return order(T, lhs, rhs) == .lt;
}

test lessThan {
    try testing.expect(lessThan(u8, "abcd", "bee"));
    try testing.expect(!lessThan(u8, "abc", "abc"));
    try testing.expect(lessThan(u8, "abc", "abc0"));
    try testing.expect(!lessThan(u8, "", ""));
    try testing.expect(lessThan(u8, "", "a"));
}

const use_vectors = switch (builtin.zig_backend) {
    // These backends don't support vectors yet.
    .stage2_aarch64,
    .stage2_powerpc,
    .stage2_riscv64,
    => false,
    // The SPIR-V backend does not support the optimized path yet.
    .stage2_spirv => false,
    else => true,
};

// The naive memory comparison implementation is more useful for fuzzers to find interesting inputs.
const use_vectors_for_comparison = use_vectors and !builtin.fuzz;

/// Returns true if and only if the slices have the same length and all elements
/// compare true using equality operator.
pub fn eql(comptime T: type, a: []const T, b: []const T) bool {
    if (!@inComptime() and @sizeOf(T) != 0 and std.meta.hasUniqueRepresentation(T) and
        use_vectors_for_comparison)
    {
        return eqlBytes(sliceAsBytes(a), sliceAsBytes(b));
    }

    if (a.len != b.len) return false;
    if (a.len == 0 or a.ptr == b.ptr) return true;

    for (a, b) |a_elem, b_elem| {
        if (a_elem != b_elem) return false;
    }
    return true;
}

test eql {
    try testing.expect(eql(u8, "abcd", "abcd"));
    try testing.expect(!eql(u8, "abcdef", "abZdef"));
    try testing.expect(!eql(u8, "abcdefg", "abcdef"));

    comptime {
        try testing.expect(eql(type, &.{ bool, f32 }, &.{ bool, f32 }));
        try testing.expect(!eql(type, &.{ bool, f32 }, &.{ f32, bool }));
        try testing.expect(!eql(type, &.{ bool, f32 }, &.{bool}));

        try testing.expect(eql(comptime_int, &.{ 1, 2, 3 }, &.{ 1, 2, 3 }));
        try testing.expect(!eql(comptime_int, &.{ 1, 2, 3 }, &.{ 3, 2, 1 }));
        try testing.expect(!eql(comptime_int, &.{1}, &.{ 1, 2 }));
    }

    try testing.expect(eql(void, &.{ {}, {} }, &.{ {}, {} }));
    try testing.expect(!eql(void, &.{{}}, &.{ {}, {} }));
}

/// std.mem.eql heavily optimized for slices of bytes.
fn eqlBytes(a: []const u8, b: []const u8) bool {
    comptime assert(use_vectors_for_comparison);

    if (a.len != b.len) return false;
    if (a.len == 0 or a.ptr == b.ptr) return true;

    if (a.len <= 16) {
        if (a.len < 4) {
            const x = (a[0] ^ b[0]) | (a[a.len - 1] ^ b[a.len - 1]) | (a[a.len / 2] ^ b[a.len / 2]);
            return x == 0;
        }
        var x: u32 = 0;
        for ([_]usize{ 0, a.len - 4, (a.len / 8) * 4, a.len - 4 - ((a.len / 8) * 4) }) |n| {
            x |= @as(u32, @bitCast(a[n..][0..4].*)) ^ @as(u32, @bitCast(b[n..][0..4].*));
        }
        return x == 0;
    }

    // Figure out the fastest way to scan through the input in chunks.
    // Uses vectors when supported and falls back to usize/words when not.
    const Scan = if (std.simd.suggestVectorLength(u8)) |vec_size|
        struct {
            pub const size = vec_size;
            pub const Chunk = @Vector(size, u8);
            pub inline fn isNotEqual(chunk_a: Chunk, chunk_b: Chunk) bool {
                return @reduce(.Or, chunk_a != chunk_b);
            }
        }
    else
        struct {
            pub const size = @sizeOf(usize);
            pub const Chunk = usize;
            pub inline fn isNotEqual(chunk_a: Chunk, chunk_b: Chunk) bool {
                return chunk_a != chunk_b;
            }
        };

    inline for (1..6) |s| {
        const n = 16 << s;
        if (n <= Scan.size and a.len <= n) {
            const V = @Vector(n / 2, u8);
            var x = @as(V, a[0 .. n / 2].*) ^ @as(V, b[0 .. n / 2].*);
            x |= @as(V, a[a.len - n / 2 ..][0 .. n / 2].*) ^ @as(V, b[a.len - n / 2 ..][0 .. n / 2].*);
            const zero: V = @splat(0);
            return !@reduce(.Or, x != zero);
        }
    }
    // Compare inputs in chunks at a time (excluding the last chunk).
    for (0..(a.len - 1) / Scan.size) |i| {
        const a_chunk: Scan.Chunk = @bitCast(a[i * Scan.size ..][0..Scan.size].*);
        const b_chunk: Scan.Chunk = @bitCast(b[i * Scan.size ..][0..Scan.size].*);
        if (Scan.isNotEqual(a_chunk, b_chunk)) return false;
    }

    // Compare the last chunk using an overlapping read (similar to the previous size strategies).
    const last_a_chunk: Scan.Chunk = @bitCast(a[a.len - Scan.size ..][0..Scan.size].*);
    const last_b_chunk: Scan.Chunk = @bitCast(b[a.len - Scan.size ..][0..Scan.size].*);
    return !Scan.isNotEqual(last_a_chunk, last_b_chunk);
}

/// Deprecated in favor of `findDiff`.
pub const indexOfDiff = findDiff;

/// Compares two slices and returns the index of the first inequality.
/// Returns null if the slices are equal.
pub fn findDiff(comptime T: type, a: []const T, b: []const T) ?usize {
    const shortest = @min(a.len, b.len);
    if (a.ptr == b.ptr)
        return if (a.len == b.len) null else shortest;
    var index: usize = 0;
    while (index < shortest) : (index += 1) if (a[index] != b[index]) return index;
    return if (a.len == b.len) null else shortest;
}

test findDiff {
    try testing.expectEqual(findDiff(u8, "one", "one"), null);
    try testing.expectEqual(findDiff(u8, "one two", "one"), 3);
    try testing.expectEqual(findDiff(u8, "one", "one two"), 3);
    try testing.expectEqual(findDiff(u8, "one twx", "one two"), 6);
    try testing.expectEqual(findDiff(u8, "xne", "one"), 0);
}

/// Takes a sentinel-terminated pointer and returns a slice preserving pointer attributes.
/// `[*c]` pointers are assumed to be 0-terminated and assumed to not be allowzero.
fn Span(comptime T: type) type {
    switch (@typeInfo(T)) {
        .optional => |optional_info| {
            return ?Span(optional_info.child);
        },
        .pointer => |ptr_info| {
            const new_sentinel: ?ptr_info.child = switch (ptr_info.size) {
                .one, .slice => @compileError("invalid type given to std.mem.span: " ++ @typeName(T)),
                .many => ptr_info.sentinel() orelse @compileError("invalid type given to std.mem.span: " ++ @typeName(T)),
                .c => 0,
            };
            return @Pointer(.slice, .{
                .@"const" = ptr_info.is_const,
                .@"volatile" = ptr_info.is_volatile,
                .@"allowzero" = ptr_info.is_allowzero and ptr_info.size != .c,
                .@"align" = ptr_info.alignment,
                .@"addrspace" = ptr_info.address_space,
            }, ptr_info.child, new_sentinel);
        },
        else => {},
    }
    @compileError("invalid type given to std.mem.span: " ++ @typeName(T));
}

test Span {
    try testing.expect(Span([*:1]u16) == [:1]u16);
    try testing.expect(Span(?[*:1]u16) == ?[:1]u16);
    try testing.expect(Span([*:1]const u8) == [:1]const u8);
    try testing.expect(Span(?[*:1]const u8) == ?[:1]const u8);
    try testing.expect(Span([*c]u16) == [:0]u16);
    try testing.expect(Span(?[*c]u16) == ?[:0]u16);
    try testing.expect(Span([*c]const u8) == [:0]const u8);
    try testing.expect(Span(?[*c]const u8) == ?[:0]const u8);
}

/// Takes a sentinel-terminated pointer and returns a slice, iterating over the
/// memory to find the sentinel and determine the length.
/// Pointer attributes such as const are preserved.
/// `[*c]` pointers are assumed to be non-null and 0-terminated.
pub fn span(ptr: anytype) Span(@TypeOf(ptr)) {
    if (@typeInfo(@TypeOf(ptr)) == .optional) {
        if (ptr) |non_null| {
            return span(non_null);
        } else {
            return null;
        }
    }
    const Result = Span(@TypeOf(ptr));
    const l = len(ptr);
    const ptr_info = @typeInfo(Result).pointer;
    if (ptr_info.sentinel()) |s| {
        return ptr[0..l :s];
    } else {
        return ptr[0..l];
    }
}

test span {
    var array: [5]u16 = [_]u16{ 1, 2, 3, 4, 5 };
    const ptr = @as([*:3]u16, array[0..2 :3]);
    try testing.expect(eql(u16, span(ptr), &[_]u16{ 1, 2 }));
    try testing.expectEqual(@as(?[:0]u16, null), span(@as(?[*:0]u16, null)));
}

/// Helper for the return type of sliceTo()
fn SliceTo(comptime T: type, comptime end: std.meta.Elem(T)) type {
    switch (@typeInfo(T)) {
        .optional => |optional_info| {
            return ?SliceTo(optional_info.child, end);
        },
        .pointer => |ptr_info| {
            const Elem = std.meta.Elem(T);
            const have_sentinel: bool = switch (ptr_info.size) {
                .one, .slice => if (std.meta.sentinel(T)) |s| s == end else false,
                .many => if (std.meta.sentinel(T)) |s| s == end else true,
                .c => true,
            };
            return @Pointer(.slice, .{
                .@"const" = ptr_info.is_const,
                .@"volatile" = ptr_info.is_volatile,
                .@"allowzero" = ptr_info.is_allowzero and ptr_info.size != .c,
                .@"align" = ptr_info.alignment,
                .@"addrspace" = ptr_info.address_space,
            }, Elem, if (have_sentinel) end else null);
        },
        else => {},
    }
    @compileError("invalid type given to std.mem.sliceTo: " ++ @typeName(T));
}

/// Takes a pointer to an array, a many-item pointer, or a slice, and returns a
/// slice of the items up to the first occurrence of `end`.
/// If `end` is not found, the resulting slice will include all items up to the
/// input's length or sentinel.
/// If the pointer type is unbounded (no length or sentinel), `end` will be the
/// sentinel for the resulting slice.
/// If the pointer type is sentinel-terminated by `end`, the resulting slice
/// will also be sentinel-terminated by `end`.
/// Pointer properties such as mutability and alignment are preserved.
/// C pointers are assumed to be non-null.
pub fn sliceTo(ptr: anytype, comptime end: std.meta.Elem(@TypeOf(ptr))) SliceTo(@TypeOf(ptr), end) {
    if (@typeInfo(@TypeOf(ptr)) == .optional) {
        const non_null = ptr orelse return null;
        return sliceTo(non_null, end);
    }
    const Result = SliceTo(@TypeOf(ptr), end);
    const length = lenSliceTo(ptr, end);
    const ptr_info = @typeInfo(Result).pointer;
    if (ptr_info.sentinel()) |s| {
        return ptr[0..length :s];
    } else {
        return ptr[0..length];
    }
}

test sliceTo {
    try testing.expectEqualSlices(u8, "aoeu", sliceTo("aoeu", 0));

    {
        var array: [5]u16 = [_]u16{ 1, 2, 3, 4, 5 };
        try testing.expectEqualSlices(u16, &array, sliceTo(&array, 0));
        try testing.expectEqualSlices(u16, array[0..3], sliceTo(array[0..3], 0));
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(&array, 3));
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(array[0..3], 3));

        const many_ptr: [*]u16 = &array;
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(many_ptr, 3));
        try testing.expectEqual([:3]u16, @TypeOf(sliceTo(many_ptr, 3)));

        const sentinel_ptr = @as([*:5]u16, @ptrCast(&array));
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(sentinel_ptr, 3));
        try testing.expectEqual([]u16, @TypeOf(sliceTo(sentinel_ptr, 3)));
        try testing.expectEqualSlices(u16, array[0..4], sliceTo(sentinel_ptr, 5));
        try testing.expectEqual([:5]u16, @TypeOf(sliceTo(sentinel_ptr, 5)));
        try testing.expectEqualSlices(u16, array[0..4], sliceTo(sentinel_ptr, 99));

        const optional_sentinel_ptr = @as(?[*:5]u16, @ptrCast(&array));
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(optional_sentinel_ptr, 3).?);
        try testing.expectEqualSlices(u16, array[0..4], sliceTo(optional_sentinel_ptr, 99).?);

        const c_ptr = @as([*c]u16, &array);
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(c_ptr, 3));
        try testing.expectEqual([:3]u16, @TypeOf(sliceTo(c_ptr, 3)));

        const slice: []u16 = &array;
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(slice, 3));
        try testing.expectEqualSlices(u16, &array, sliceTo(slice, 99));

        const sentinel_slice: [:5]u16 = array[0..4 :5];
        try testing.expectEqualSlices(u16, array[0..2], sliceTo(sentinel_slice, 3));
        try testing.expectEqualSlices(u16, array[0..4], sliceTo(sentinel_slice, 99));
    }
    {
        var sentinel_array: [5:0]u16 = [_:0]u16{ 1, 2, 3, 4, 5 };
        try testing.expectEqualSlices(u16, sentinel_array[0..2], sliceTo(&sentinel_array, 3));
        try testing.expectEqualSlices(u16, &sentinel_array, sliceTo(&sentinel_array, 0));
        try testing.expectEqualSlices(u16, &sentinel_array, sliceTo(&sentinel_array, 99));
    }

    try testing.expectEqual(@as(?[]u8, null), sliceTo(@as(?[]u8, null), 0));
}

/// Private helper for sliceTo(). If you want the length, use sliceTo(foo, x).len
fn lenSliceTo(ptr: anytype, comptime end: std.meta.Elem(@TypeOf(ptr))) usize {
    switch (@typeInfo(@TypeOf(ptr))) {
        .pointer => |ptr_info| switch (ptr_info.size) {
            .one => switch (@typeInfo(ptr_info.child)) {
                .array => |array_info| {
                    if (array_info.sentinel()) |s| {
                        if (s == end) {
                            return findSentinel(array_info.child, end, ptr);
                        }
                    }
                    return findScalar(array_info.child, ptr, end) orelse array_info.len;
                },
                else => {},
            },
            .many => if (ptr_info.sentinel()) |s| {
                if (s == end) {
                    return findSentinel(ptr_info.child, end, ptr);
                }
                // We're looking for something other than the sentinel,
                // but iterating past the sentinel would be a bug so we need
                // to check for both.
                var i: usize = 0;
                while (ptr[i] != end and ptr[i] != s) i += 1;
                return i;
            } else {
                return findSentinel(ptr_info.child, end, @ptrCast(ptr));
            },
            .c => {
                assert(ptr != null);
                return findSentinel(ptr_info.child, end, ptr);
            },
            .slice => {
                if (ptr_info.sentinel()) |s| {
                    if (s == end) {
                        return findSentinel(ptr_info.child, s, ptr);
                    }
                }
                return findScalar(ptr_info.child, ptr, end) orelse ptr.len;
            },
        },
        else => {},
    }
    @compileError("invalid type given to std.mem.sliceTo: " ++ @typeName(@TypeOf(ptr)));
}

test lenSliceTo {
    try testing.expect(lenSliceTo("aoeu", 0) == 4);

    {
        var array: [5]u16 = [_]u16{ 1, 2, 3, 4, 5 };
        try testing.expectEqual(@as(usize, 5), lenSliceTo(&array, 0));
        try testing.expectEqual(@as(usize, 3), lenSliceTo(array[0..3], 0));
        try testing.expectEqual(@as(usize, 2), lenSliceTo(&array, 3));
        try testing.expectEqual(@as(usize, 2), lenSliceTo(array[0..3], 3));

        const sentinel_ptr = @as([*:5]u16, @ptrCast(&array));
        try testing.expectEqual(@as(usize, 2), lenSliceTo(sentinel_ptr, 3));
        try testing.expectEqual(@as(usize, 4), lenSliceTo(sentinel_ptr, 99));

        const c_ptr = @as([*c]u16, &array);
        try testing.expectEqual(@as(usize, 2), lenSliceTo(c_ptr, 3));

        const slice: []u16 = &array;
        try testing.expectEqual(@as(usize, 2), lenSliceTo(slice, 3));
        try testing.expectEqual(@as(usize, 5), lenSliceTo(slice, 99));

        const sentinel_slice: [:5]u16 = array[0..4 :5];
        try testing.expectEqual(@as(usize, 2), lenSliceTo(sentinel_slice, 3));
        try testing.expectEqual(@as(usize, 4), lenSliceTo(sentinel_slice, 99));
    }
    {
        var sentinel_array: [5:0]u16 = [_:0]u16{ 1, 2, 3, 4, 5 };
        try testing.expectEqual(@as(usize, 2), lenSliceTo(&sentinel_array, 3));
        try testing.expectEqual(@as(usize, 5), lenSliceTo(&sentinel_array, 0));
        try testing.expectEqual(@as(usize, 5), lenSliceTo(&sentinel_array, 99));
    }
}

/// Takes a sentinel-terminated pointer and iterates over the memory to find the
/// sentinel and determine the length.
/// `[*c]` pointers are assumed to be non-null and 0-terminated.
pub fn len(value: anytype) usize {
    switch (@typeInfo(@TypeOf(value))) {
        .pointer => |info| switch (info.size) {
            .many => {
                const sentinel = info.sentinel() orelse
                    @compileError("invalid type given to std.mem.len: " ++ @typeName(@TypeOf(value)));
                return findSentinel(info.child, sentinel, value);
            },
            .c => {
                assert(value != null);
                return findSentinel(info.child, 0, value);
            },
            else => @compileError("invalid type given to std.mem.len: " ++ @typeName(@TypeOf(value))),
        },
        else => @compileError("invalid type given to std.mem.len: " ++ @typeName(@TypeOf(value))),
    }
}

test len {
    var array: [5]u16 = [_]u16{ 1, 2, 0, 4, 5 };
    const ptr = @as([*:4]u16, array[0..3 :4]);
    try testing.expect(len(ptr) == 3);
    const c_ptr = @as([*c]u16, ptr);
    try testing.expect(len(c_ptr) == 2);
}

/// Deprecated in favor of `findSentinel`.
pub const indexOfSentinel = findSentinel;

/// Returns the index of the sentinel value in a sentinel-terminated pointer.
/// Linear search through memory until the sentinel is found.
pub fn findSentinel(comptime T: type, comptime sentinel: T, p: [*:sentinel]const T) usize {
    var i: usize = 0;
    while (p[i] != sentinel) {
        i += 1;
    }
    return i;
}

test "findSentinel vector paths" {
    const Types = [_]type{ u8, u16, u32, u64 };
    const allocator = std.testing.allocator;
    const page_size = std.heap.page_size_min;

    inline for (Types) |T| {
        const block_len = std.simd.suggestVectorLength(T) orelse continue;

        // Allocate three pages so we guarantee a page-crossing address with a full page after
        const memory = try allocator.alloc(T, 3 * page_size / @sizeOf(T));
        defer allocator.free(memory);
        @memset(memory, 0xaa);

        // Find starting page-alignment = 0
        var start: usize = 0;
        const start_addr = @intFromPtr(&memory);
        start += (std.mem.alignForward(usize, start_addr, page_size) - start_addr) / @sizeOf(T);
        try testing.expect(start < page_size / @sizeOf(T));

        // Validate all sub-block alignments
        const search_len = page_size / @sizeOf(T);
        memory[start + search_len] = 0;
        for (0..block_len) |offset| {
            try testing.expectEqual(search_len - offset, findSentinel(T, 0, @ptrCast(&memory[start + offset])));
        }
        memory[start + search_len] = 0xaa;

        // Validate page boundary crossing
        const start_page_boundary = start + (page_size / @sizeOf(T));
        memory[start_page_boundary + block_len] = 0;
        for (0..block_len) |offset| {
            try testing.expectEqual(2 * block_len - offset, findSentinel(T, 0, @ptrCast(&memory[start_page_boundary - block_len + offset])));
        }
    }
}

/// Returns true if all elements in a slice are equal to the scalar value provided
pub fn allEqual(comptime T: type, slice: []const T, scalar: T) bool {
    for (slice) |item| {
        if (item != scalar) return false;
    }
    return true;
}

/// Remove a set of values from the beginning of a slice.
pub fn trimStart(comptime T: type, slice: []const T, values_to_strip: []const T) []const T {
    var begin: usize = 0;
    while (begin < slice.len and findScalar(T, values_to_strip, slice[begin]) != null) : (begin += 1) {}
    return slice[begin..];
}

test trimStart {
    try testing.expectEqualSlices(u8, "foo\n ", trimStart(u8, " foo\n ", " \n"));
}

/// Remove a set of values from the end of a slice.
pub fn trimEnd(comptime T: type, slice: []const T, values_to_strip: []const T) []const T {
    var end: usize = slice.len;
    while (end > 0 and findScalar(T, values_to_strip, slice[end - 1]) != null) : (end -= 1) {}
    return slice[0..end];
}

test trimEnd {
    try testing.expectEqualSlices(u8, " foo", trimEnd(u8, " foo\n ", " \n"));
}

/// Remove a set of values from the beginning and end of a slice.
pub fn trim(comptime T: type, slice: []const T, values_to_strip: []const T) []const T {
    var begin: usize = 0;
    var end: usize = slice.len;
    while (begin < end and findScalar(T, values_to_strip, slice[begin]) != null) : (begin += 1) {}
    while (end > begin and findScalar(T, values_to_strip, slice[end - 1]) != null) : (end -= 1) {}
    return slice[begin..end];
}

test trim {
    try testing.expectEqualSlices(u8, "foo", trim(u8, " foo\n ", " \n"));
    try testing.expectEqualSlices(u8, "foo", trim(u8, "foo", " \n"));
}

/// Deprecated in favor of `findScalar`.
pub const indexOfScalar = findScalar;

/// Linear search for the index of a scalar value inside a slice.
pub fn findScalar(comptime T: type, slice: []const T, value: T) ?usize {
    return findScalarPos(T, slice, 0, value);
}

/// Deprecated in favor of `findScalarLast`.
pub const lastIndexOfScalar = findScalarLast;

/// Linear search for the last index of a scalar value inside a slice.
pub fn findScalarLast(comptime T: type, slice: []const T, value: T) ?usize {
    var i: usize = slice.len;
    while (i != 0) {
        i -= 1;
        if (slice[i] == value) return i;
    }
    return null;
}

/// Deprecated in favor of `findScalarPos`.
pub const indexOfScalarPos = findScalarPos;

/// Linear search for the index of a scalar value inside a slice, starting from a given position.
/// Returns null if the value is not found.
pub fn findScalarPos(comptime T: type, slice: []const T, start_index: usize, value: T) ?usize {
    if (start_index >= slice.len) return null;

    var i: usize = start_index;
    if (use_vectors_for_comparison and
        !std.debug.inValgrind() and // https://github.com/ziglang/zig/issues/17717
        !@inComptime() and
        (@typeInfo(T) == .int or @typeInfo(T) == .float) and std.math.isPowerOfTwo(@bitSizeOf(T)))
    {
        if (std.simd.suggestVectorLength(T)) |block_len| {
            // For Intel Nehalem (2009) and AMD Bulldozer (2012) or later, unaligned loads on aligned data result
            // in the same execution as aligned loads. We ignore older arch's here and don't bother pre-aligning.
            //
            // Use `std.simd.suggestVectorLength(T)` to get the same alignment as used in this function
            // however this usually isn't necessary unless your arch has a performance penalty due to this.
            //
            // This may differ for other arch's. Arm for example costs a cycle when loading across a cache
            // line so explicit alignment prologues may be worth exploration.

            // Unrolling here is ~10% improvement. We can then do one bounds check every 2 blocks
            // instead of one which adds up.
            const Block = @Vector(block_len, T);
            if (i + 2 * block_len < slice.len) {
                const mask: Block = @splat(value);
                while (true) {
                    inline for (0..2) |_| {
                        const block: Block = slice[i..][0..block_len].*;
                        const matches = block == mask;
                        if (@reduce(.Or, matches)) {
                            return i + std.simd.firstTrue(matches).?;
                        }
                        i += block_len;
                    }
                    if (i + 2 * block_len >= slice.len) break;
                }
            }

            // {block_len, block_len / 2} check
            inline for (0..2) |j| {
                const block_x_len = block_len / (1 << j);
                comptime if (block_x_len < 4) break;

                const BlockX = @Vector(block_x_len, T);
                if (i + block_x_len < slice.len) {
                    const mask: BlockX = @splat(value);
                    const block: BlockX = slice[i..][0..block_x_len].*;
                    const matches = block == mask;
                    if (@reduce(.Or, matches)) {
                        return i + std.simd.firstTrue(matches).?;
                    }
                    i += block_x_len;
                }
            }
        }
    }

    for (slice[i..], i..) |c, j| {
        if (c == value) return j;
    }
    return null;
}

test findScalarPos {
    const Types = [_]type{ u8, u16, u32, u64 };

    inline for (Types) |T| {
        var memory: [64 / @sizeOf(T)]T = undefined;
        @memset(&memory, 0xaa);
        memory[memory.len - 1] = 0;

        for (0..memory.len) |i| {
            try testing.expectEqual(memory.len - i - 1, findScalarPos(T, memory[i..], 0, 0).?);
        }
    }
}

/// Deprecated in favor of `findAny`.
pub const indexOfAny = findAny;

/// Linear search for the index of any value in the provided list inside a slice.
/// Returns null if no values are found.
pub fn findAny(comptime T: type, slice: []const T, values: []const T) ?usize {
    return findAnyPos(T, slice, 0, values);
}

/// Deprecated in favor of `findLastAny`.
pub const lastIndexOfAny = findLastAny;

/// Linear search for the last index of any value in the provided list inside a slice.
/// Returns null if no values are found.
pub fn findLastAny(comptime T: type, slice: []const T, values: []const T) ?usize {
    var i: usize = slice.len;
    while (i != 0) {
        i -= 1;
        for (values) |value| {
            if (slice[i] == value) return i;
        }
    }
    return null;
}

/// Deprecated in favor of `findAnyPos`.
pub const indexOfAnyPos = findAnyPos;

/// Linear search for the index of any value in the provided list inside a slice, starting from a given position.
/// Returns null if no values are found.
pub fn findAnyPos(comptime T: type, slice: []const T, start_index: usize, values: []const T) ?usize {
    if (start_index >= slice.len) return null;
    for (slice[start_index..], start_index..) |c, i| {
        for (values) |value| {
            if (c == value) return i;
        }
    }
    return null;
}

/// Deprecated in favor of `findNone`.
pub const indexOfNone = findNone;

/// Find the first item in `slice` which is not contained in `values`.
///
/// Comparable to `strspn` in the C standard library.
pub fn findNone(comptime T: type, slice: []const T, values: []const T) ?usize {
    return findNonePos(T, slice, 0, values);
}

test findNone {
    try testing.expect(findNone(u8, "abc123", "123").? == 0);
    try testing.expect(findLastNone(u8, "abc123", "123").? == 2);
    try testing.expect(findNone(u8, "123abc", "123").? == 3);
    try testing.expect(findLastNone(u8, "123abc", "123").? == 5);
    try testing.expect(findNone(u8, "123123", "123") == null);
    try testing.expect(findNone(u8, "333333", "123") == null);

    try testing.expect(findNonePos(u8, "abc123", 3, "321") == null);
}

/// Deprecated in favor of `findLastNone`.
pub const lastIndexOfNone = findLastNone;

/// Find the last item in `slice` which is not contained in `values`.
///
/// Like `strspn` in the C standard library, but searches from the end.
pub fn findLastNone(comptime T: type, slice: []const T, values: []const T) ?usize {
    var i: usize = slice.len;
    outer: while (i != 0) {
        i -= 1;
        for (values) |value| {
            if (slice[i] == value) continue :outer;
        }
        return i;
    }
    return null;
}

pub const indexOfNonePos = findNonePos;

/// Find the first item in `slice[start_index..]` which is not contained in `values`.
/// The returned index will be relative to the start of `slice`, and never less than `start_index`.
///
/// Comparable to `strspn` in the C standard library.
pub fn findNonePos(comptime T: type, slice: []const T, start_index: usize, values: []const T) ?usize {
    if (start_index >= slice.len) return null;
    outer: for (slice[start_index..], start_index..) |c, i| {
        for (values) |value| {
            if (c == value) continue :outer;
        }
        return i;
    }
    return null;
}

/// Deprecated in favor of `find`.
pub const indexOf = find;

/// Search for needle in haystack and return the index of the first occurrence.
/// Uses Boyer-Moore-Horspool algorithm on large inputs; linear search on small inputs.
/// Returns null if needle is not found.
pub fn find(comptime T: type, haystack: []const T, needle: []const T) ?usize {
    return findPos(T, haystack, 0, needle);
}

/// Deprecated in favor of `findLastLinear`.
pub const lastIndexOfLinear = findLastLinear;

/// Find the index in a slice of a sub-slice, searching from the end backwards.
/// To start looking at a different index, slice the haystack first.
/// Consider using `lastIndexOf` instead of this, which will automatically use a
/// more sophisticated algorithm on larger inputs.
pub fn findLastLinear(comptime T: type, haystack: []const T, needle: []const T) ?usize {
    if (needle.len > haystack.len) return null;
    var i: usize = haystack.len - needle.len;
    while (true) : (i -= 1) {
        if (mem.eql(T, haystack[i..][0..needle.len], needle)) return i;
        if (i == 0) return null;
    }
}

pub const indexOfPosLinear = findPosLinear;

/// Consider using `findPos` instead of this, which will automatically use a
/// more sophisticated algorithm on larger inputs.
pub fn findPosLinear(comptime T: type, haystack: []const T, start_index: usize, needle: []const T) ?usize {
    if (needle.len > haystack.len) return null;
    var i: usize = start_index;
    const end = haystack.len - needle.len;
    while (i <= end) : (i += 1) {
        if (eql(T, haystack[i..][0..needle.len], needle)) return i;
    }
    return null;
}

test findPosLinear {
    try testing.expectEqual(0, findPosLinear(u8, "", 0, ""));
    try testing.expectEqual(0, findPosLinear(u8, "123", 0, ""));

    try testing.expectEqual(null, findPosLinear(u8, "", 0, "1"));
    try testing.expectEqual(0, findPosLinear(u8, "1", 0, "1"));
    try testing.expectEqual(null, findPosLinear(u8, "2", 0, "1"));
    try testing.expectEqual(1, findPosLinear(u8, "21", 0, "1"));
    try testing.expectEqual(null, findPosLinear(u8, "222", 0, "1"));

    try testing.expectEqual(null, findPosLinear(u8, "", 0, "12"));
    try testing.expectEqual(null, findPosLinear(u8, "1", 0, "12"));
    try testing.expectEqual(null, findPosLinear(u8, "2", 0, "12"));
    try testing.expectEqual(0, findPosLinear(u8, "12", 0, "12"));
    try testing.expectEqual(null, findPosLinear(u8, "21", 0, "12"));
    try testing.expectEqual(1, findPosLinear(u8, "212", 0, "12"));
    try testing.expectEqual(0, findPosLinear(u8, "122", 0, "12"));
    try testing.expectEqual(1, findPosLinear(u8, "212112", 0, "12"));
}

fn boyerMooreHorspoolPreprocessReverse(pattern: []const u8, table: *[256]usize) void {
    for (table) |*c| {
        c.* = pattern.len;
    }

    var i: usize = pattern.len - 1;
    // The first item is intentionally ignored and the skip size will be pattern.len.
    // This is the standard way Boyer-Moore-Horspool is implemented.
    while (i > 0) : (i -= 1) {
        table[pattern[i]] = i;
    }
}

fn boyerMooreHorspoolPreprocess(pattern: []const u8, table: *[256]usize) void {
    for (table) |*c| {
        c.* = pattern.len;
    }

    var i: usize = 0;
    // The last item is intentionally ignored and the skip size will be pattern.len.
    // This is the standard way Boyer-Moore-Horspool is implemented.
    while (i < pattern.len - 1) : (i += 1) {
        table[pattern[i]] = pattern.len - 1 - i;
    }
}

/// Deprecated in favor of `find`.
pub const lastIndexOf = findLast;

/// Find the index in a slice of a sub-slice, searching from the end backwards.
/// To start looking at a different index, slice the haystack first.
/// Uses the Reverse Boyer-Moore-Horspool algorithm on large inputs;
/// `lastIndexOfLinear` on small inputs.
pub fn findLast(comptime T: type, haystack: []const T, needle: []const T) ?usize {
    if (needle.len > haystack.len) return null;
    if (needle.len == 0) return haystack.len;

    if (!std.meta.hasUniqueRepresentation(T) or haystack.len < 52 or needle.len <= 4)
        return lastIndexOfLinear(T, haystack, needle);

    const haystack_bytes = sliceAsBytes(haystack);
    const needle_bytes = sliceAsBytes(needle);

    var skip_table: [256]usize = undefined;
    boyerMooreHorspoolPreprocessReverse(needle_bytes, skip_table[0..]);

    var i: usize = haystack_bytes.len - needle_bytes.len;
    while (true) {
        if (i % @sizeOf(T) == 0 and mem.eql(u8, haystack_bytes[i .. i + needle_bytes.len], needle_bytes)) {
            return @divExact(i, @sizeOf(T));
        }
        const skip = skip_table[haystack_bytes[i]];
        if (skip > i) break;
        i -= skip;
    }

    return null;
}

/// Deprecated in favor of `findPos`.
pub const indexOfPos = findPos;

/// Uses Boyer-Moore-Horspool algorithm on large inputs; `findPosLinear` on small inputs.
pub fn findPos(comptime T: type, haystack: []const T, start_index: usize, needle: []const T) ?usize {
    if (needle.len > haystack.len) return null;
    if (needle.len < 2) {
        if (needle.len == 0) return start_index;
        // findScalarPos is significantly faster than findPosLinear
        return findScalarPos(T, haystack, start_index, needle[0]);
    }

    if (!std.meta.hasUniqueRepresentation(T) or haystack.len < 52 or needle.len <= 4)
        return findPosLinear(T, haystack, start_index, needle);

    const haystack_bytes = sliceAsBytes(haystack);
    const needle_bytes = sliceAsBytes(needle);

    var skip_table: [256]usize = undefined;
    boyerMooreHorspoolPreprocess(needle_bytes, skip_table[0..]);

    var i: usize = start_index * @sizeOf(T);
    while (i <= haystack_bytes.len - needle_bytes.len) {
        if (i % @sizeOf(T) == 0 and mem.eql(u8, haystack_bytes[i .. i + needle_bytes.len], needle_bytes)) {
            return @divExact(i, @sizeOf(T));
        }
        i += skip_table[haystack_bytes[i + needle_bytes.len - 1]];
    }

    return null;
}

test find {
    try testing.expect(find(u8, "one two three four five six seven eight nine ten eleven", "three four").? == 8);
    try testing.expect(lastIndexOf(u8, "one two three four five six seven eight nine ten eleven", "three four").? == 8);
    try testing.expect(find(u8, "one two three four five six seven eight nine ten eleven", "two two") == null);
    try testing.expect(lastIndexOf(u8, "one two three four five six seven eight nine ten eleven", "two two") == null);

    try testing.expect(find(u8, "one two three four five six seven eight nine ten", "").? == 0);
    try testing.expect(lastIndexOf(u8, "one two three four five six seven eight nine ten", "").? == 48);

    try testing.expect(find(u8, "one two three four", "four").? == 14);
    try testing.expect(lastIndexOf(u8, "one two three two four", "two").? == 14);
    try testing.expect(find(u8, "one two three four", "gour") == null);
    try testing.expect(lastIndexOf(u8, "one two three four", "gour") == null);
    try testing.expect(find(u8, "foo", "foo").? == 0);
    try testing.expect(lastIndexOf(u8, "foo", "foo").? == 0);
    try testing.expect(find(u8, "foo", "fool") == null);
    try testing.expect(lastIndexOf(u8, "foo", "lfoo") == null);
    try testing.expect(lastIndexOf(u8, "foo", "fool") == null);

    try testing.expect(find(u8, "foo foo", "foo").? == 0);
    try testing.expect(lastIndexOf(u8, "foo foo", "foo").? == 4);
    try testing.expect(lastIndexOfAny(u8, "boo, cat", "abo").? == 6);
    try testing.expect(findScalarLast(u8, "boo", 'o').? == 2);
}

test "find multibyte" {
    {
        // make haystack and needle long enough to trigger Boyer-Moore-Horspool algorithm
        const haystack = [1]u16{0} ** 100 ++ [_]u16{ 0xbbaa, 0xccbb, 0xddcc, 0xeedd, 0xffee, 0x00ff };
        const needle = [_]u16{ 0xbbaa, 0xccbb, 0xddcc, 0xeedd, 0xffee };
        try testing.expectEqual(findPos(u16, &haystack, 0, &needle), 100);

        // check for misaligned false positives (little and big endian)
        const needleLE = [_]u16{ 0xbbbb, 0xcccc, 0xdddd, 0xeeee, 0xffff };
        try testing.expectEqual(findPos(u16, &haystack, 0, &needleLE), null);
        const needleBE = [_]u16{ 0xaacc, 0xbbdd, 0xccee, 0xddff, 0xee00 };
        try testing.expectEqual(findPos(u16, &haystack, 0, &needleBE), null);
    }

    {
        // make haystack and needle long enough to trigger Boyer-Moore-Horspool algorithm
        const haystack = [_]u16{ 0xbbaa, 0xccbb, 0xddcc, 0xeedd, 0xffee, 0x00ff } ++ [1]u16{0} ** 100;
        const needle = [_]u16{ 0xbbaa, 0xccbb, 0xddcc, 0xeedd, 0xffee };
        try testing.expectEqual(lastIndexOf(u16, &haystack, &needle), 0);

        // check for misaligned false positives (little and big endian)
        const needleLE = [_]u16{ 0xbbbb, 0xcccc, 0xdddd, 0xeeee, 0xffff };
        try testing.expectEqual(lastIndexOf(u16, &haystack, &needleLE), null);
        const needleBE = [_]u16{ 0xaacc, 0xbbdd, 0xccee, 0xddff, 0xee00 };
        try testing.expectEqual(lastIndexOf(u16, &haystack, &needleBE), null);
    }
}

test "findPos empty needle" {
    try testing.expectEqual(findPos(u8, "abracadabra", 5, ""), 5);
}

/// Returns the number of needles inside the haystack
/// needle.len must be > 0
/// does not count overlapping needles
pub fn count(comptime T: type, haystack: []const T, needle: []const T) usize {
    if (needle.len == 1) return countScalar(T, haystack, needle[0]);
    assert(needle.len > 0);
    var i: usize = 0;
    var found: usize = 0;

    while (findPos(T, haystack, i, needle)) |idx| {
        i = idx + needle.len;
        found += 1;
    }

    return found;
}

test count {
    try testing.expect(count(u8, "", "h") == 0);
    try testing.expect(count(u8, "h", "h") == 1);
    try testing.expect(count(u8, "hh", "h") == 2);
    try testing.expect(count(u8, "world!", "hello") == 0);
    try testing.expect(count(u8, "hello world!", "hello") == 1);
    try testing.expect(count(u8, "   abcabc   abc", "abc") == 3);
    try testing.expect(count(u8, "udexdcbvbruhasdrw", "bruh") == 1);
    try testing.expect(count(u8, "foo bar", "o bar") == 1);
    try testing.expect(count(u8, "foofoofoo", "foo") == 3);
    try testing.expect(count(u8, "fffffff", "ff") == 3);
    try testing.expect(count(u8, "owowowu", "owowu") == 1);
}

/// Returns the number of times `element` appears in a slice of memory.
pub fn countScalar(comptime T: type, list: []const T, element: T) usize {
    const n = list.len;
    var i: usize = 0;
    var found: usize = 0;

    if (use_vectors_for_comparison and
        (@typeInfo(T) == .int or @typeInfo(T) == .float) and std.math.isPowerOfTwo(@bitSizeOf(T)))
    {
        if (std.simd.suggestVectorLength(T)) |block_size| {
            const Block = @Vector(block_size, T);

            const letter_mask: Block = @splat(element);
            while (n - i >= block_size) : (i += block_size) {
                const haystack_block: Block = list[i..][0..block_size].*;
                found += std.simd.countTrues(letter_mask == haystack_block);
            }
        }
    }

    for (list[i..n]) |item| {
        found += @intFromBool(item == element);
    }

    return found;
}

test countScalar {
    try testing.expectEqual(0, countScalar(u8, "", 'h'));
    try testing.expectEqual(1, countScalar(u8, "h", 'h'));
    try testing.expectEqual(2, countScalar(u8, "hh", 'h'));
    try testing.expectEqual(2, countScalar(u8, "ahhb", 'h'));
    try testing.expectEqual(3, countScalar(u8, "   abcabc   abc", 'b'));
}

/// Returns true if the haystack contains expected_count or more needles
/// needle.len must be > 0
/// does not count overlapping needles
//
/// See also: `containsAtLeastScalar`
pub fn containsAtLeast(comptime T: type, haystack: []const T, expected_count: usize, needle: []const T) bool {
    if (needle.len == 1) return containsAtLeastScalar(T, haystack, expected_count, needle[0]);
    assert(needle.len > 0);
    if (expected_count == 0) return true;

    var i: usize = 0;
    var found: usize = 0;

    while (findPos(T, haystack, i, needle)) |idx| {
        i = idx + needle.len;
        found += 1;
        if (found == expected_count) return true;
    }
    return false;
}

test containsAtLeast {
    try testing.expect(containsAtLeast(u8, "aa", 0, "a"));
    try testing.expect(containsAtLeast(u8, "aa", 1, "a"));
    try testing.expect(containsAtLeast(u8, "aa", 2, "a"));
    try testing.expect(!containsAtLeast(u8, "aa", 3, "a"));

    try testing.expect(containsAtLeast(u8, "radaradar", 1, "radar"));
    try testing.expect(!containsAtLeast(u8, "radaradar", 2, "radar"));

    try testing.expect(containsAtLeast(u8, "radarradaradarradar", 3, "radar"));
    try testing.expect(!containsAtLeast(u8, "radarradaradarradar", 4, "radar"));

    try testing.expect(containsAtLeast(u8, "   radar      radar   ", 2, "radar"));
    try testing.expect(!containsAtLeast(u8, "   radar      radar   ", 3, "radar"));
}

/// Deprecated in favor of `containsAtLeastScalar2`.
pub fn containsAtLeastScalar(comptime T: type, list: []const T, minimum: usize, element: T) bool {
    return containsAtLeastScalar2(T, list, element, minimum);
}

/// Returns true if `element` appears at least `minimum` number of times in `list`.
//
/// Related:
/// * `containsAtLeast`
/// * `countScalar`
pub fn containsAtLeastScalar2(comptime T: type, list: []const T, element: T, minimum: usize) bool {
    const n = list.len;
    var i: usize = 0;
    var found: usize = 0;

    if (use_vectors_for_comparison and
        (@typeInfo(T) == .int or @typeInfo(T) == .float) and std.math.isPowerOfTwo(@bitSizeOf(T)))
    {
        if (std.si
```
