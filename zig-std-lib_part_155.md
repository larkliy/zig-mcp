```
ct(codeface == 0);

    const S = packed struct {
        a: u8,
        b: u8,
        c: u8,
        d: u8,
    };

    const inst = S{
        .a = 0xBE,
        .b = 0xEF,
        .c = 0xDE,
        .d = 0xA1,
    };
    switch (native_endian) {
        .little => {
            try testing.expect(eql(u8, asBytes(&inst), "\xBE\xEF\xDE\xA1"));
        },
        .big => {
            try testing.expect(eql(u8, asBytes(&inst), "\xA1\xDE\xEF\xBE"));
        },
    }

    const ZST = struct {};
    const zero = ZST{};
    try testing.expect(eql(u8, asBytes(&zero), ""));
}

test "asBytes preserves pointer attributes" {
    const inArr: u32 align(16) = 0xDEADBEEF;
    const inPtr = @as(*align(16) const volatile u32, @ptrCast(&inArr));
    const outSlice = asBytes(inPtr);

    const in = @typeInfo(@TypeOf(inPtr)).pointer;
    const out = @typeInfo(@TypeOf(outSlice)).pointer;

    try testing.expectEqual(in.is_const, out.is_const);
    try testing.expectEqual(in.is_volatile, out.is_volatile);
    try testing.expectEqual(in.is_allowzero, out.is_allowzero);
    try testing.expectEqual(in.alignment, out.alignment);
}

/// Given any value, returns a copy of its bytes in an array.
pub fn toBytes(value: anytype) [@sizeOf(@TypeOf(value))]u8 {
    return asBytes(&value).*;
}

test toBytes {
    var my_bytes = toBytes(@as(u32, 0x12345678));
    switch (native_endian) {
        .big => try testing.expect(eql(u8, &my_bytes, "\x12\x34\x56\x78")),
        .little => try testing.expect(eql(u8, &my_bytes, "\x78\x56\x34\x12")),
    }

    my_bytes[0] = '\x99';
    switch (native_endian) {
        .big => try testing.expect(eql(u8, &my_bytes, "\x99\x34\x56\x78")),
        .little => try testing.expect(eql(u8, &my_bytes, "\x99\x56\x34\x12")),
    }
}

fn BytesAsValueReturnType(comptime T: type, comptime B: type) type {
    return CopyPtrAttrs(B, .one, T);
}

/// Given a pointer to an array of bytes, returns a pointer to a value of the specified type
/// backed by those bytes, preserving pointer attributes.
pub fn bytesAsValue(comptime T: type, bytes: anytype) BytesAsValueReturnType(T, @TypeOf(bytes)) {
    return @ptrCast(bytes);
}

test bytesAsValue {
    const deadbeef = @as(u32, 0xDEADBEEF);
    const deadbeef_bytes = switch (native_endian) {
        .big => "\xDE\xAD\xBE\xEF",
        .little => "\xEF\xBE\xAD\xDE",
    };

    try testing.expect(deadbeef == bytesAsValue(u32, deadbeef_bytes).*);

    var codeface_bytes: [4]u8 = switch (native_endian) {
        .big => "\xC0\xDE\xFA\xCE",
        .little => "\xCE\xFA\xDE\xC0",
    }.*;
    const codeface = bytesAsValue(u32, &codeface_bytes);
    try testing.expect(codeface.* == 0xC0DEFACE);
    codeface.* = 0;
    for (codeface_bytes) |b|
        try testing.expect(b == 0);

    const S = packed struct {
        a: u8,
        b: u8,
        c: u8,
        d: u8,
    };

    const inst = S{
        .a = 0xBE,
        .b = 0xEF,
        .c = 0xDE,
        .d = 0xA1,
    };
    const inst_bytes = switch (native_endian) {
        .little => "\xBE\xEF\xDE\xA1",
        .big => "\xA1\xDE\xEF\xBE",
    };
    const inst2 = bytesAsValue(S, inst_bytes);
    try testing.expect(std.meta.eql(inst, inst2.*));
}

test "bytesAsValue preserves pointer attributes" {
    const inArr align(16) = [4]u8{ 0xDE, 0xAD, 0xBE, 0xEF };
    const inSlice = @as(*align(16) const volatile [4]u8, @ptrCast(&inArr))[0..];
    const outPtr = bytesAsValue(u32, inSlice);

    const in = @typeInfo(@TypeOf(inSlice)).pointer;
    const out = @typeInfo(@TypeOf(outPtr)).pointer;

    try testing.expectEqual(in.is_const, out.is_const);
    try testing.expectEqual(in.is_volatile, out.is_volatile);
    try testing.expectEqual(in.is_allowzero, out.is_allowzero);
    try testing.expectEqual(in.alignment, out.alignment);
}

/// Given a pointer to an array of bytes, returns a value of the specified type backed by a
/// copy of those bytes.
pub fn bytesToValue(comptime T: type, bytes: anytype) T {
    return bytesAsValue(T, bytes).*;
}
test bytesToValue {
    const deadbeef_bytes = switch (native_endian) {
        .big => "\xDE\xAD\xBE\xEF",
        .little => "\xEF\xBE\xAD\xDE",
    };

    const deadbeef = bytesToValue(u32, deadbeef_bytes);
    try testing.expect(deadbeef == @as(u32, 0xDEADBEEF));
}

fn BytesAsSliceReturnType(comptime T: type, comptime bytesType: type) type {
    return CopyPtrAttrs(bytesType, .slice, T);
}

/// Given a slice of bytes, returns a slice of the specified type
/// backed by those bytes, preserving pointer attributes.
/// If `T` is zero-bytes sized, the returned slice has a len of zero.
pub fn bytesAsSlice(comptime T: type, bytes: anytype) BytesAsSliceReturnType(T, @TypeOf(bytes)) {
    // let's not give an undefined pointer to @ptrCast
    // it may be equal to zero and fail a null check
    if (bytes.len == 0 or @sizeOf(T) == 0) {
        return &[0]T{};
    }

    const cast_target = CopyPtrAttrs(@TypeOf(bytes), .many, T);

    return @as(cast_target, @ptrCast(bytes))[0..@divExact(bytes.len, @sizeOf(T))];
}

test bytesAsSlice {
    {
        const bytes = [_]u8{ 0xDE, 0xAD, 0xBE, 0xEF };
        const slice = bytesAsSlice(u16, bytes[0..]);
        try testing.expect(slice.len == 2);
        try testing.expect(bigToNative(u16, slice[0]) == 0xDEAD);
        try testing.expect(bigToNative(u16, slice[1]) == 0xBEEF);
    }
    {
        const bytes = [_]u8{ 0xDE, 0xAD, 0xBE, 0xEF };
        var runtime_zero: usize = 0;
        _ = &runtime_zero;
        const slice = bytesAsSlice(u16, bytes[runtime_zero..]);
        try testing.expect(slice.len == 2);
        try testing.expect(bigToNative(u16, slice[0]) == 0xDEAD);
        try testing.expect(bigToNative(u16, slice[1]) == 0xBEEF);
    }
}

test "bytesAsSlice keeps pointer alignment" {
    {
        var bytes = [_]u8{ 0x01, 0x02, 0x03, 0x04 };
        const numbers = bytesAsSlice(u32, bytes[0..]);
        try comptime testing.expect(@TypeOf(numbers) == []align(@alignOf(@TypeOf(bytes))) u32);
    }
    {
        var bytes = [_]u8{ 0x01, 0x02, 0x03, 0x04 };
        var runtime_zero: usize = 0;
        _ = &runtime_zero;
        const numbers = bytesAsSlice(u32, bytes[runtime_zero..]);
        try comptime testing.expect(@TypeOf(numbers) == []align(@alignOf(@TypeOf(bytes))) u32);
    }
}

test "bytesAsSlice on a packed struct" {
    const F = packed struct {
        a: u8,
    };

    const b: [1]u8 = .{9};
    const f = bytesAsSlice(F, &b);
    try testing.expect(f[0].a == 9);
}

test "bytesAsSlice with specified alignment" {
    var bytes align(4) = [_]u8{
        0x33,
        0x33,
        0x33,
        0x33,
    };
    const slice: []u32 = std.mem.bytesAsSlice(u32, bytes[0..]);
    try testing.expect(slice[0] == 0x33333333);
}

test "bytesAsSlice preserves pointer attributes" {
    const inArr align(16) = [4]u8{ 0xDE, 0xAD, 0xBE, 0xEF };
    const inSlice = @as(*align(16) const volatile [4]u8, @ptrCast(&inArr))[0..];
    const outSlice = bytesAsSlice(u16, inSlice);

    const in = @typeInfo(@TypeOf(inSlice)).pointer;
    const out = @typeInfo(@TypeOf(outSlice)).pointer;

    try testing.expectEqual(in.is_const, out.is_const);
    try testing.expectEqual(in.is_volatile, out.is_volatile);
    try testing.expectEqual(in.is_allowzero, out.is_allowzero);
    try testing.expectEqual(in.alignment, out.alignment);
}

test "bytesAsSlice with zero-bit element type" {
    {
        const bytes = [_]u8{};
        const slice = bytesAsSlice(void, &bytes);
        try testing.expectEqual(0, slice.len);
    }
    {
        const bytes = [_]u8{ 0x01, 0x02, 0x03, 0x04 };
        const slice = bytesAsSlice(u0, &bytes);
        try testing.expectEqual(0, slice.len);
    }
}

fn SliceAsBytesReturnType(comptime Slice: type) type {
    return CopyPtrAttrs(Slice, .slice, u8);
}

/// Given a slice, returns a slice of the underlying bytes, preserving pointer attributes.
pub fn sliceAsBytes(slice: anytype) SliceAsBytesReturnType(@TypeOf(slice)) {
    const Slice = @TypeOf(slice);

    // a slice of zero-bit values always occupies zero bytes
    if (@sizeOf(std.meta.Elem(Slice)) == 0) return &[0]u8{};

    // let's not give an undefined pointer to @ptrCast
    // it may be equal to zero and fail a null check
    if (slice.len == 0 and std.meta.sentinel(Slice) == null) return &[0]u8{};

    const cast_target = CopyPtrAttrs(Slice, .many, u8);

    return @as(cast_target, @ptrCast(slice))[0 .. slice.len * @sizeOf(std.meta.Elem(Slice))];
}

test sliceAsBytes {
    const bytes = [_]u16{ 0xDEAD, 0xBEEF };
    const slice = sliceAsBytes(bytes[0..]);
    try testing.expect(slice.len == 4);
    try testing.expect(eql(u8, slice, switch (native_endian) {
        .big => "\xDE\xAD\xBE\xEF",
        .little => "\xAD\xDE\xEF\xBE",
    }));
}

test "sliceAsBytes with sentinel slice" {
    const empty_string: [:0]const u8 = "";
    const bytes = sliceAsBytes(empty_string);
    try testing.expect(bytes.len == 0);
}

test "sliceAsBytes with zero-bit element type" {
    const lots_of_nothing = [1]void{{}} ** 10_000;
    const bytes = sliceAsBytes(&lots_of_nothing);
    try testing.expect(bytes.len == 0);
}

test "sliceAsBytes packed struct at runtime and comptime" {
    const Foo = packed struct {
        a: u4,
        b: u4,
    };
    const S = struct {
        fn doTheTest() !void {
            var foo: Foo = undefined;
            var slice = sliceAsBytes(@as(*[1]Foo, &foo)[0..1]);
            slice[0] = 0x13;
            try testing.expect(foo.a == 0x3);
            try testing.expect(foo.b == 0x1);
        }
    };
    try S.doTheTest();
    try comptime S.doTheTest();
}

test "sliceAsBytes and bytesAsSlice back" {
    try testing.expect(@sizeOf(i32) == 4);

    var big_thing_array = [_]i32{ 1, 2, 3, 4 };
    const big_thing_slice: []i32 = big_thing_array[0..];

    const bytes = sliceAsBytes(big_thing_slice);
    try testing.expect(bytes.len == 4 * 4);

    bytes[4] = 0;
    bytes[5] = 0;
    bytes[6] = 0;
    bytes[7] = 0;
    try testing.expect(big_thing_slice[1] == 0);

    const big_thing_again = bytesAsSlice(i32, bytes);
    try testing.expect(big_thing_again[2] == 3);

    big_thing_again[2] = -1;
    try testing.expect(bytes[8] == math.maxInt(u8));
    try testing.expect(bytes[9] == math.maxInt(u8));
    try testing.expect(bytes[10] == math.maxInt(u8));
    try testing.expect(bytes[11] == math.maxInt(u8));
}

test "sliceAsBytes preserves pointer attributes" {
    const inArr align(16) = [2]u16{ 0xDEAD, 0xBEEF };
    const inSlice = @as(*align(16) const volatile [2]u16, @ptrCast(&inArr))[0..];
    const outSlice = sliceAsBytes(inSlice);

    const in = @typeInfo(@TypeOf(inSlice)).pointer;
    const out = @typeInfo(@TypeOf(outSlice)).pointer;

    try testing.expectEqual(in.is_const, out.is_const);
    try testing.expectEqual(in.is_volatile, out.is_volatile);
    try testing.expectEqual(in.is_allowzero, out.is_allowzero);
    try testing.expectEqual(in.alignment, out.alignment);
}

fn AbsorbSentinelReturnType(comptime Slice: type) type {
    const info = @typeInfo(Slice).pointer;
    assert(info.size == .slice);
    return @Pointer(.slice, .{
        .@"const" = info.is_const,
        .@"volatile" = info.is_volatile,
        .@"allowzero" = info.is_allowzero,
        .@"addrspace" = info.address_space,
        .@"align" = info.alignment,
    }, info.child, null);
}

/// If the provided slice is not sentinel terminated, do nothing and return that slice.
/// If it is sentinel-terminated, return a non-sentinel-terminated slice with the
/// length increased by one to include the absorbed sentinel element.
pub fn absorbSentinel(slice: anytype) AbsorbSentinelReturnType(@TypeOf(slice)) {
    const info = @typeInfo(@TypeOf(slice)).pointer;
    comptime assert(info.size == .slice);
    if (info.sentinel_ptr == null) {
        return slice;
    } else {
        return slice.ptr[0 .. slice.len + 1];
    }
}

test absorbSentinel {
    {
        var buffer: [3:0]u8 = .{ 1, 2, 3 };
        const foo: [:0]const u8 = &buffer;
        const bar: []const u8 = &buffer;
        try testing.expectEqual([]const u8, @TypeOf(absorbSentinel(foo)));
        try testing.expectEqual([]const u8, @TypeOf(absorbSentinel(bar)));
        try testing.expectEqualSlices(u8, &.{ 1, 2, 3, 0 }, absorbSentinel(foo));
        try testing.expectEqualSlices(u8, &.{ 1, 2, 3 }, absorbSentinel(bar));
    }
    {
        var buffer: [3:0]u8 = .{ 1, 2, 3 };
        const foo: [:0]u8 = &buffer;
        const bar: []u8 = &buffer;
        try testing.expectEqual([]u8, @TypeOf(absorbSentinel(foo)));
        try testing.expectEqual([]u8, @TypeOf(absorbSentinel(bar)));
        var expected_foo = [_]u8{ 1, 2, 3, 0 };
        try testing.expectEqualSlices(u8, &expected_foo, absorbSentinel(foo));
        var expected_bar = [_]u8{ 1, 2, 3 };
        try testing.expectEqualSlices(u8, &expected_bar, absorbSentinel(bar));
    }
}

/// Round an address down to the next (or current) aligned address.
/// Unlike `alignForward`, `alignment` can be any positive number, not just a power of 2.
pub fn alignForwardAnyAlign(comptime T: type, addr: T, alignment: T) T {
    if (isValidAlignGeneric(T, alignment))
        return alignForward(T, addr, alignment);
    assert(alignment != 0);
    return alignBackwardAnyAlign(T, addr + (alignment - 1), alignment);
}

/// Round an address up to the next (or current) aligned address.
/// The alignment must be a power of 2 and greater than 0.
/// Asserts that rounding up the address does not cause integer overflow.
pub fn alignForward(comptime T: type, addr: T, alignment: T) T {
    assert(isValidAlignGeneric(T, alignment));
    return alignBackward(T, addr + (alignment - 1), alignment);
}

/// Rounds an address up to the next alignment boundary using log2 representation.
/// Equivalent to alignForward with alignment = 1 << log2_alignment.
/// More efficient when alignment is known to be a power of 2.
pub fn alignForwardLog2(addr: usize, log2_alignment: u8) usize {
    const alignment = @as(usize, 1) << @as(math.Log2Int(usize), @intCast(log2_alignment));
    return alignForward(usize, addr, alignment);
}

/// Force an evaluation of the expression; this tries to prevent
/// the compiler from optimizing the computation away even if the
/// result eventually gets discarded.
// TODO: use @declareSideEffect() when it is available - https://github.com/ziglang/zig/issues/6168
pub fn doNotOptimizeAway(val: anytype) void {
    if (@inComptime()) return;

    const max_gp_register_bits = @bitSizeOf(c_long);
    const t = @typeInfo(@TypeOf(val));
    switch (t) {
        .void, .null, .comptime_int, .comptime_float => return,
        .@"enum" => doNotOptimizeAway(@intFromEnum(val)),
        .bool => doNotOptimizeAway(@intFromBool(val)),
        .int => {
            const bits = t.int.bits;
            if (bits <= max_gp_register_bits and builtin.zig_backend != .stage2_c) {
                const val2 = @as(
                    std.meta.Int(t.int.signedness, @max(8, std.math.ceilPowerOfTwoAssert(u16, bits))),
                    val,
                );
                asm volatile (""
                    :
                    : [_] "r" (val2),
                );
            } else doNotOptimizeAway(&val);
        },
        .float => {
            // https://github.com/llvm/llvm-project/issues/159200
            if ((t.float.bits == 32 or t.float.bits == 64) and builtin.zig_backend != .stage2_c and !builtin.cpu.arch.isLoongArch()) {
                asm volatile (""
                    :
                    : [_] "rm" (val),
                );
            } else doNotOptimizeAway(&val);
        },
        .pointer => {
            if (builtin.zig_backend == .stage2_c) {
                doNotOptimizeAwayC(val);
            } else {
                asm volatile (""
                    :
                    : [_] "m" (val),
                    : .{ .memory = true });
            }
        },
        .array => {
            if (t.array.len * @sizeOf(t.array.child) <= 64) {
                for (val) |v| doNotOptimizeAway(v);
            } else doNotOptimizeAway(&val);
        },
        else => doNotOptimizeAway(&val),
    }
}

/// .stage2_c doesn't support asm blocks yet, so use volatile stores instead
var deopt_target: if (builtin.zig_backend == .stage2_c) u8 else void = undefined;
fn doNotOptimizeAwayC(ptr: anytype) void {
    const dest = @as(*volatile u8, @ptrCast(&deopt_target));
    for (asBytes(ptr)) |b| {
        dest.* = b;
    }
    dest.* = 0;
}

test doNotOptimizeAway {
    comptime doNotOptimizeAway("test");

    doNotOptimizeAway(null);
    doNotOptimizeAway(true);
    doNotOptimizeAway(0);
    doNotOptimizeAway(0.0);
    doNotOptimizeAway(@as(u1, 0));
    doNotOptimizeAway(@as(u3, 0));
    doNotOptimizeAway(@as(u8, 0));
    doNotOptimizeAway(@as(u16, 0));
    doNotOptimizeAway(@as(u32, 0));
    doNotOptimizeAway(@as(u64, 0));
    doNotOptimizeAway(@as(u128, 0));
    doNotOptimizeAway(@as(u13, 0));
    doNotOptimizeAway(@as(u37, 0));
    doNotOptimizeAway(@as(u96, 0));
    doNotOptimizeAway(@as(u200, 0));
    doNotOptimizeAway(@as(f32, 0.0));
    doNotOptimizeAway(@as(f64, 0.0));
    doNotOptimizeAway([_]u8{0} ** 4);
    doNotOptimizeAway([_]u8{0} ** 100);
    doNotOptimizeAway(@as(std.builtin.Endian, .little));
}

test alignForward {
    try testing.expect(alignForward(usize, 1, 1) == 1);
    try testing.expect(alignForward(usize, 2, 1) == 2);
    try testing.expect(alignForward(usize, 1, 2) == 2);
    try testing.expect(alignForward(usize, 2, 2) == 2);
    try testing.expect(alignForward(usize, 3, 2) == 4);
    try testing.expect(alignForward(usize, 4, 2) == 4);
    try testing.expect(alignForward(usize, 7, 8) == 8);
    try testing.expect(alignForward(usize, 8, 8) == 8);
    try testing.expect(alignForward(usize, 9, 8) == 16);
    try testing.expect(alignForward(usize, 15, 8) == 16);
    try testing.expect(alignForward(usize, 16, 8) == 16);
    try testing.expect(alignForward(usize, 17, 8) == 24);
}

/// Round an address down to the previous (or current) aligned address.
/// Unlike `alignBackward`, `alignment` can be any positive number, not just a power of 2.
pub fn alignBackwardAnyAlign(comptime T: type, addr: T, alignment: T) T {
    if (isValidAlignGeneric(T, alignment))
        return alignBackward(T, addr, alignment);
    assert(alignment != 0);
    return addr - @mod(addr, alignment);
}

/// Round an address down to the previous (or current) aligned address.
/// The alignment must be a power of 2 and greater than 0.
pub fn alignBackward(comptime T: type, addr: T, alignment: T) T {
    assert(isValidAlignGeneric(T, alignment));
    // 000010000 // example alignment
    // 000001111 // subtract 1
    // 111110000 // binary not
    return addr & ~(alignment - 1);
}

/// Returns whether `alignment` is a valid alignment, meaning it is
/// a positive power of 2.
pub fn isValidAlign(alignment: usize) bool {
    return isValidAlignGeneric(usize, alignment);
}

/// Returns whether `alignment` is a valid alignment, meaning it is
/// a positive power of 2.
pub fn isValidAlignGeneric(comptime T: type, alignment: T) bool {
    return alignment > 0 and std.math.isPowerOfTwo(alignment);
}

/// Returns true if i is aligned to the given alignment.
/// Works with any positive alignment value, not just powers of 2.
/// For power-of-2 alignments, `isAligned` is more efficient.
pub fn isAlignedAnyAlign(i: usize, alignment: usize) bool {
    if (isValidAlign(alignment))
        return isAligned(i, alignment);
    assert(alignment != 0);
    return 0 == @mod(i, alignment);
}

/// Returns true if addr is aligned to 2^log2_alignment.
/// More efficient than `isAligned` when alignment is known to be a power of 2.
/// log2_alignment must be < @bitSizeOf(usize).
pub fn isAlignedLog2(addr: usize, log2_alignment: u8) bool {
    return @ctz(addr) >= log2_alignment;
}

/// Given an address and an alignment, return true if the address is a multiple of the alignment
/// The alignment must be a power of 2 and greater than 0.
pub fn isAligned(addr: usize, alignment: usize) bool {
    return isAlignedGeneric(u64, addr, alignment);
}

/// Generic version of `isAligned` that works with any integer type.
/// Returns true if addr is aligned to the given alignment.
/// Alignment must be a power of 2 and greater than 0.
pub fn isAlignedGeneric(comptime T: type, addr: T, alignment: T) bool {
    return alignBackward(T, addr, alignment) == addr;
}

test isAligned {
    try testing.expect(isAligned(0, 4));
    try testing.expect(isAligned(1, 1));
    try testing.expect(isAligned(2, 1));
    try testing.expect(isAligned(2, 2));
    try testing.expect(!isAligned(2, 4));
    try testing.expect(isAligned(3, 1));
    try testing.expect(!isAligned(3, 2));
    try testing.expect(!isAligned(3, 4));
    try testing.expect(isAligned(4, 4));
    try testing.expect(isAligned(4, 2));
    try testing.expect(isAligned(4, 1));
    try testing.expect(!isAligned(4, 8));
    try testing.expect(!isAligned(4, 16));
}

test "freeing empty string with null-terminated sentinel" {
    const empty_string = try testing.allocator.dupeZ(u8, "");
    testing.allocator.free(empty_string);
}

/// Returns a slice with the given new alignment,
/// all other pointer attributes copied from `AttributeSource`.
fn AlignedSlice(comptime AttributeSource: type, comptime new_alignment: usize) type {
    const ptr = @typeInfo(AttributeSource).pointer;
    return @Pointer(.slice, .{
        .@"const" = ptr.is_const,
        .@"volatile" = ptr.is_volatile,
        .@"allowzero" = ptr.is_allowzero,
        .@"align" = new_alignment,
        .@"addrspace" = ptr.address_space,
    }, ptr.child, null);
}

/// Returns the largest slice in the given bytes that conforms to the new alignment,
/// or `null` if the given bytes contain no conforming address.
pub fn alignInBytes(bytes: []u8, comptime new_alignment: usize) ?[]align(new_alignment) u8 {
    const begin_address = @intFromPtr(bytes.ptr);
    const end_address = begin_address + bytes.len;

    const begin_address_aligned = mem.alignForward(usize, begin_address, new_alignment);
    const new_length = std.math.sub(usize, end_address, begin_address_aligned) catch |e| switch (e) {
        error.Overflow => return null,
    };
    const alignment_offset = begin_address_aligned - begin_address;
    return @alignCast(bytes[alignment_offset .. alignment_offset + new_length]);
}

/// Returns the largest sub-slice within the given slice that conforms to the new alignment,
/// or `null` if the given slice contains no conforming address.
pub fn alignInSlice(slice: anytype, comptime new_alignment: usize) ?AlignedSlice(@TypeOf(slice), new_alignment) {
    const bytes = sliceAsBytes(slice);
    const aligned_bytes = alignInBytes(bytes, new_alignment) orelse return null;

    const Element = @TypeOf(slice[0]);
    const slice_length_bytes = aligned_bytes.len - (aligned_bytes.len % @sizeOf(Element));
    const aligned_slice = bytesAsSlice(Element, aligned_bytes[0..slice_length_bytes]);
    return @alignCast(aligned_slice);
}

test "read/write(Var)PackedInt" {
    switch (builtin.cpu.arch) {
        // This test generates too much code to execute on WASI.
        // LLVM backend fails with "too many locals: locals exceed maximum"
        .wasm32, .wasm64 => return error.SkipZigTest,
        else => {},
    }

    const foreign_endian: Endian = if (native_endian == .big) .little else .big;
    const expect = std.testing.expect;
    var prng = std.Random.DefaultPrng.init(1234);
    const random = prng.random();

    @setEvalBranchQuota(10_000);
    inline for ([_]type{ u8, u16, u32, u128 }) |BackingType| {
        for ([_]BackingType{
            @as(BackingType, 0), // all zeros
            -%@as(BackingType, 1), // all ones
            random.int(BackingType), // random
            random.int(BackingType), // random
            random.int(BackingType), // random
        }) |init_value| {
            const uTs = [_]type{ u1, u3, u7, u8, u9, u10, u15, u16, u86 };
            const iTs = [_]type{ i1, i3, i7, i8, i9, i10, i15, i16, i86 };
            inline for (uTs ++ iTs) |PackedType| {
                if (@bitSizeOf(PackedType) > @bitSizeOf(BackingType))
                    continue;

                const iPackedType = std.meta.Int(.signed, @bitSizeOf(PackedType));
                const uPackedType = std.meta.Int(.unsigned, @bitSizeOf(PackedType));
                const Log2T = std.math.Log2Int(BackingType);

                const offset_at_end = @bitSizeOf(BackingType) - @bitSizeOf(PackedType);
                for ([_]usize{ 0, 1, 7, 8, 9, 10, 15, 16, 86, offset_at_end }) |offset| {
                    if (offset > offset_at_end or offset == @bitSizeOf(BackingType))
                        continue;

                    for ([_]PackedType{
                        ~@as(PackedType, 0), // all ones: -1 iN / maxInt uN
                        @as(PackedType, 0), // all zeros: 0 iN / 0 uN
                        @as(PackedType, @bitCast(@as(iPackedType, math.maxInt(iPackedType)))), // maxInt iN
                        @as(PackedType, @bitCast(@as(iPackedType, math.minInt(iPackedType)))), // maxInt iN
                        random.int(PackedType), // random
                        random.int(PackedType), // random
                    }) |write_value| {
                        { // Fixed-size Read/Write (Native-endian)

                            // Initialize Value
                            var value: BackingType = init_value;

                            // Read
                            const read_value1 = readPackedInt(PackedType, asBytes(&value), offset, native_endian);
                            try expect(read_value1 == @as(PackedType, @bitCast(@as(uPackedType, @truncate(value >> @as(Log2T, @intCast(offset)))))));

                            // Write
                            writePackedInt(PackedType, asBytes(&value), offset, write_value, native_endian);
                            try expect(write_value == @as(PackedType, @bitCast(@as(uPackedType, @truncate(value >> @as(Log2T, @intCast(offset)))))));

                            // Read again
                            const read_value2 = readPackedInt(PackedType, asBytes(&value), offset, native_endian);
                            try expect(read_value2 == write_value);

                            // Verify bits outside of the target integer are unmodified
                            const diff_bits = init_value ^ value;
                            if (offset != offset_at_end)
                                try expect(diff_bits >> @as(Log2T, @intCast(offset + @bitSizeOf(PackedType))) == 0);
                            if (offset != 0)
                                try expect(diff_bits << @as(Log2T, @intCast(@bitSizeOf(BackingType) - offset)) == 0);
                        }

                        { // Fixed-size Read/Write (Foreign-endian)

                            // Initialize Value
                            var value: BackingType = @byteSwap(init_value);

                            // Read
                            const read_value1 = readPackedInt(PackedType, asBytes(&value), offset, foreign_endian);
                            try expect(read_value1 == @as(PackedType, @bitCast(@as(uPackedType, @truncate(@byteSwap(value) >> @as(Log2T, @intCast(offset)))))));

                            // Write
                            writePackedInt(PackedType, asBytes(&value), offset, write_value, foreign_endian);
                            try expect(write_value == @as(PackedType, @bitCast(@as(uPackedType, @truncate(@byteSwap(value) >> @as(Log2T, @intCast(offset)))))));

                            // Read again
                            const read_value2 = readPackedInt(PackedType, asBytes(&value), offset, foreign_endian);
                            try expect(read_value2 == write_value);

                            // Verify bits outside of the target integer are unmodified
                            const diff_bits = init_value ^ @byteSwap(value);
                            if (offset != offset_at_end)
                                try expect(diff_bits >> @as(Log2T, @intCast(offset + @bitSizeOf(PackedType))) == 0);
                            if (offset != 0)
                                try expect(diff_bits << @as(Log2T, @intCast(@bitSizeOf(BackingType) - offset)) == 0);
                        }

                        const signedness = @typeInfo(PackedType).int.signedness;
                        const NextPowerOfTwoInt = std.meta.Int(signedness, try comptime std.math.ceilPowerOfTwo(u16, @bitSizeOf(PackedType)));
                        const ui64 = std.meta.Int(signedness, 64);
                        inline for ([_]type{ PackedType, NextPowerOfTwoInt, ui64 }) |U| {
                            { // Variable-size Read/Write (Native-endian)

                                if (@bitSizeOf(U) < @bitSizeOf(PackedType))
                                    continue;

                                // Initialize Value
                                var value: BackingType = init_value;

                                // Read
                                const read_value1 = readVarPackedInt(U, asBytes(&value), offset, @bitSizeOf(PackedType), native_endian, signedness);
                                try expect(read_value1 == @as(PackedType, @bitCast(@as(uPackedType, @truncate(value >> @as(Log2T, @intCast(offset)))))));

                                // Write
                                writeVarPackedInt(asBytes(&value), offset, @bitSizeOf(PackedType), @as(U, write_value), native_endian);
                                try expect(write_value == @as(PackedType, @bitCast(@as(uPackedType, @truncate(value >> @as(Log2T, @intCast(offset)))))));

                                // Read again
                                const read_value2 = readVarPackedInt(U, asBytes(&value), offset, @bitSizeOf(PackedType), native_endian, signedness);
                                try expect(read_value2 == write_value);

                                // Verify bits outside of the target integer are unmodified
                                const diff_bits = init_value ^ value;
                                if (offset != offset_at_end)
                                    try expect(diff_bits >> @as(Log2T, @intCast(offset + @bitSizeOf(PackedType))) == 0);
                                if (offset != 0)
                                    try expect(diff_bits << @as(Log2T, @intCast(@bitSizeOf(BackingType) - offset)) == 0);
                            }

                            { // Variable-size Read/Write (Foreign-endian)

                                if (@bitSizeOf(U) < @bitSizeOf(PackedType))
                                    continue;

                                // Initialize Value
                                var value: BackingType = @byteSwap(init_value);

                                // Read
                                const read_value1 = readVarPackedInt(U, asBytes(&value), offset, @bitSizeOf(PackedType), foreign_endian, signedness);
                                try expect(read_value1 == @as(PackedType, @bitCast(@as(uPackedType, @truncate(@byteSwap(value) >> @as(Log2T, @intCast(offset)))))));

                                // Write
                                writeVarPackedInt(asBytes(&value), offset, @bitSizeOf(PackedType), @as(U, write_value), foreign_endian);
                                try expect(write_value == @as(PackedType, @bitCast(@as(uPackedType, @truncate(@byteSwap(value) >> @as(Log2T, @intCast(offset)))))));

                                // Read again
                                const read_value2 = readVarPackedInt(U, asBytes(&value), offset, @bitSizeOf(PackedType), foreign_endian, signedness);
                                try expect(read_value2 == write_value);

                                // Verify bits outside of the target integer are unmodified
                                const diff_bits = init_value ^ @byteSwap(value);
                                if (offset != offset_at_end)
                                    try expect(diff_bits >> @as(Log2T, @intCast(offset + @bitSizeOf(PackedType))) == 0);
                                if (offset != 0)
                                    try expect(diff_bits << @as(Log2T, @intCast(@bitSizeOf(BackingType) - offset)) == 0);
                            }
                        }
                    }
                }
            }
        }
    }
}



---
File: /std/meta.zig
---

const builtin = @import("builtin");
const std = @import("std.zig");
const debug = std.debug;
const mem = std.mem;
const math = std.math;
const testing = std.testing;
const root = @import("root");

pub const TrailerFlags = @import("meta/trailer_flags.zig").TrailerFlags;

const Type = std.builtin.Type;

test {
    _ = TrailerFlags;
}

/// Returns the variant of an enum type, `T`, which is named `str`, or `null` if no such variant exists.
pub fn stringToEnum(comptime T: type, str: []const u8) ?T {
    // Using StaticStringMap here is more performant, but it will start to take too
    // long to compile if the enum is large enough, due to the current limits of comptime
    // performance when doing things like constructing lookup maps at comptime.
    // TODO The '100' here is arbitrary and should be increased when possible:
    // - https://github.com/ziglang/zig/issues/4055
    // - https://github.com/ziglang/zig/issues/3863
    if (@typeInfo(T).@"enum".fields.len <= 100) {
        const kvs = comptime build_kvs: {
            const EnumKV = struct { []const u8, T };
            var kvs_array: [@typeInfo(T).@"enum".fields.len]EnumKV = undefined;
            for (@typeInfo(T).@"enum".fields, 0..) |enumField, i| {
                kvs_array[i] = .{ enumField.name, @field(T, enumField.name) };
            }
            break :build_kvs kvs_array[0..];
        };
        const map = std.StaticStringMap(T).initComptime(kvs);
        return map.get(str);
    } else {
        inline for (@typeInfo(T).@"enum".fields) |enumField| {
            if (mem.eql(u8, str, enumField.name)) {
                return @field(T, enumField.name);
            }
        }
        return null;
    }
}

test stringToEnum {
    const E1 = enum {
        A,
        B,
    };
    try testing.expect(E1.A == stringToEnum(E1, "A").?);
    try testing.expect(E1.B == stringToEnum(E1, "B").?);
    try testing.expect(null == stringToEnum(E1, "C"));
}

/// Returns the alignment of type T.
/// Note that if T is a pointer type the result is different than the one
/// returned by @alignOf(T).
/// If T is a pointer type the alignment of the type it points to is returned.
pub fn alignment(comptime T: type) comptime_int {
    return switch (@typeInfo(T)) {
        .optional => |info| switch (@typeInfo(info.child)) {
            .pointer, .@"fn" => alignment(info.child),
            else => @alignOf(T),
        },
        .pointer => |info| info.alignment orelse @alignOf(info.child),
        else => @alignOf(T),
    };
}

test alignment {
    try testing.expect(alignment(u8) == 1);
    try testing.expect(alignment(*align(1) u8) == 1);
    try testing.expect(alignment(*align(2) u8) == 2);
    try testing.expect(alignment([]align(1) u8) == 1);
    try testing.expect(alignment([]align(2) u8) == 2);
    try testing.expect(alignment(fn () void) > 0);
    try testing.expect(alignment(*const fn () void) > 0);
    try testing.expect(alignment(*align(128) const fn () void) == 128);
}

/// Given a parameterized type (array, vector, pointer, optional), returns the "child type".
pub fn Child(comptime T: type) type {
    return switch (@typeInfo(T)) {
        .array => |info| info.child,
        .vector => |info| info.child,
        .pointer => |info| info.child,
        .optional => |info| info.child,
        else => @compileError("Expected pointer, optional, array or vector type, found '" ++ @typeName(T) ++ "'"),
    };
}

test Child {
    try testing.expect(Child([1]u8) == u8);
    try testing.expect(Child(*u8) == u8);
    try testing.expect(Child([]u8) == u8);
    try testing.expect(Child(?u8) == u8);
    try testing.expect(Child(@Vector(2, u8)) == u8);
}

/// Given a "memory span" type (array, slice, vector, or pointer to such), returns the "element type".
pub fn Elem(comptime T: type) type {
    switch (@typeInfo(T)) {
        .array => |info| return info.child,
        .vector => |info| return info.child,
        .pointer => |info| switch (info.size) {
            .one => switch (@typeInfo(info.child)) {
                .array => |array_info| return array_info.child,
                .vector => |vector_info| return vector_info.child,
                else => {},
            },
            .many, .c, .slice => return info.child,
        },
        .optional => |info| return Elem(info.child),
        else => {},
    }
    @compileError("Expected pointer, slice, array or vector type, found '" ++ @typeName(T) ++ "'");
}

test Elem {
    try testing.expect(Elem([1]u8) == u8);
    try testing.expect(Elem([*]u8) == u8);
    try testing.expect(Elem([]u8) == u8);
    try testing.expect(Elem(*[10]u8) == u8);
    try testing.expect(Elem(@Vector(2, u8)) == u8);
    try testing.expect(Elem(*@Vector(2, u8)) == u8);
    try testing.expect(Elem(?[*]u8) == u8);
}

/// Given a type which can have a sentinel e.g. `[:0]u8`, returns the sentinel value,
/// or `null` if there is not one.
/// Types which cannot possibly have a sentinel will be a compile error.
/// Result is always comptime-known.
pub inline fn sentinel(comptime T: type) ?Elem(T) {
    switch (@typeInfo(T)) {
        .array => |info| return info.sentinel(),
        .pointer => |info| {
            switch (info.size) {
                .many, .slice => return info.sentinel(),
                .one => switch (@typeInfo(info.child)) {
                    .array => |array_info| return array_info.sentinel(),
                    else => {},
                },
                else => {},
            }
        },
        else => {},
    }
    @compileError("type '" ++ @typeName(T) ++ "' cannot possibly have a sentinel");
}

test sentinel {
    try testSentinel();
    try comptime testSentinel();
}

fn testSentinel() !void {
    try testing.expectEqual(@as(u8, 0), sentinel([:0]u8).?);
    try testing.expectEqual(@as(u8, 0), sentinel([*:0]u8).?);
    try testing.expectEqual(@as(u8, 0), sentinel([5:0]u8).?);
    try testing.expectEqual(@as(u8, 0), sentinel(*const [5:0]u8).?);

    try testing.expect(sentinel([]u8) == null);
    try testing.expect(sentinel([*]u8) == null);
    try testing.expect(sentinel([5]u8) == null);
    try testing.expect(sentinel(*const [5]u8) == null);
}

/// Given a "memory span" type, returns the same type except with the given sentinel value.
pub fn Sentinel(comptime T: type, comptime sentinel_val: Elem(T)) type {
    switch (@typeInfo(T)) {
        .pointer => |info| switch (info.size) {
            .one => switch (@typeInfo(info.child)) {
                .array => |array_info| return @Pointer(.one, .{
                    .@"const" = info.is_const,
                    .@"volatile" = info.is_volatile,
                    .@"allowzero" = info.is_allowzero,
                    .@"align" = info.alignment,
                    .@"addrspace" = info.address_space,
                }, [array_info.len:sentinel_val]array_info.child, null),
                else => {},
            },
            .many, .slice => |size| return @Pointer(size, .{
                .@"const" = info.is_const,
                .@"volatile" = info.is_volatile,
                .@"allowzero" = info.is_allowzero,
                .@"align" = info.alignment,
                .@"addrspace" = info.address_space,
            }, info.child, sentinel_val),
            else => {},
        },
        .optional => |info| switch (@typeInfo(info.child)) {
            .pointer => |ptr_info| switch (ptr_info.size) {
                .many => return ?@Pointer(.many, .{
                    .@"const" = ptr_info.is_const,
                    .@"volatile" = ptr_info.is_volatile,
                    .@"allowzero" = ptr_info.is_allowzero,
                    .@"align" = ptr_info.alignment,
                    .@"addrspace" = ptr_info.address_space,
                    .child = ptr_info.child,
                }, ptr_info.child, sentinel_val),
                else => {},
            },
            else => {},
        },
        else => {},
    }
    @compileError("Unable to derive a sentinel pointer type from " ++ @typeName(T));
}

pub fn containerLayout(comptime T: type) Type.ContainerLayout {
    return switch (@typeInfo(T)) {
        .@"struct" => |info| info.layout,
        .@"union" => |info| info.layout,
        else => @compileError("expected struct or union type, found '" ++ @typeName(T) ++ "'"),
    };
}

test containerLayout {
    const S1 = struct {};
    const S2 = packed struct {};
    const S3 = extern struct {};
    const U1 = union {
        a: u8,
    };
    const U2 = packed union {
        a: u8,
    };
    const U3 = extern union {
        a: u8,
    };

    try testing.expect(containerLayout(S1) == .auto);
    try testing.expect(containerLayout(S2) == .@"packed");
    try testing.expect(containerLayout(S3) == .@"extern");
    try testing.expect(containerLayout(U1) == .auto);
    try testing.expect(containerLayout(U2) == .@"packed");
    try testing.expect(containerLayout(U3) == .@"extern");
}

/// Instead of this function, prefer to use e.g. `@typeInfo(foo).@"struct".decls`
/// directly when you know what kind of type it is.
pub fn declarations(comptime T: type) []const Type.Declaration {
    return switch (@typeInfo(T)) {
        .@"struct" => |info| info.decls,
        .@"enum" => |info| info.decls,
        .@"union" => |info| info.decls,
        .@"opaque" => |info| info.decls,
        else => @compileError("Expected struct, enum, union, or opaque type, found '" ++ @typeName(T) ++ "'"),
    };
}

test declarations {
    const E1 = enum {
        A,

        pub fn a() void {}
    };
    const S1 = struct {
        pub fn a() void {}
    };
    const U1 = union {
        b: u8,

        pub fn a() void {}
    };
    const O1 = opaque {
        pub fn a() void {}
    };

    const decls = comptime [_][]const Type.Declaration{
        declarations(E1),
        declarations(S1),
        declarations(U1),
        declarations(O1),
    };

    inline for (decls) |decl| {
        try testing.expect(decl.len == 1);
        try testing.expect(comptime mem.eql(u8, decl[0].name, "a"));
    }
}

pub fn declarationInfo(comptime T: type, comptime decl_name: []const u8) Type.Declaration {
    inline for (comptime declarations(T)) |decl| {
        if (comptime mem.eql(u8, decl.name, decl_name))
            return decl;
    }

    @compileError("'" ++ @typeName(T) ++ "' has no declaration '" ++ decl_name ++ "'");
}

test declarationInfo {
    const E1 = enum {
        A,

        pub fn a() void {}
    };
    const S1 = struct {
        pub fn a() void {}
    };
    const U1 = union {
        b: u8,

        pub fn a() void {}
    };

    const infos = comptime [_]Type.Declaration{
        declarationInfo(E1, "a"),
        declarationInfo(S1, "a"),
        declarationInfo(U1, "a"),
    };

    inline for (infos) |info| {
        try testing.expect(comptime mem.eql(u8, info.name, "a"));
    }
}
pub inline fn fields(comptime T: type) switch (@typeInfo(T)) {
    .@"struct" => []const Type.StructField,
    .@"union" => []const Type.UnionField,
    .@"enum" => []const Type.EnumField,
    .error_set => []const Type.Error,
    else => @compileError("Expected struct, union, error set or enum type, found '" ++ @typeName(T) ++ "'"),
} {
    return switch (@typeInfo(T)) {
        .@"struct" => |info| info.fields,
        .@"union" => |info| info.fields,
        .@"enum" => |info| info.fields,
        .error_set => |errors| errors.?, // must be non global error set
        else => @compileError("Expected struct, union, error set or enum type, found '" ++ @typeName(T) ++ "'"),
    };
}

test fields {
    const E1 = enum {
        A,
    };
    const E2 = error{A};
    const S1 = struct {
        a: u8,
    };
    const U1 = union {
        a: u8,
    };

    const e1f = comptime fields(E1);
    const e2f = comptime fields(E2);
    const sf = comptime fields(S1);
    const uf = comptime fields(U1);

    try testing.expect(e1f.len == 1);
    try testing.expect(e2f.len == 1);
    try testing.expect(sf.len == 1);
    try testing.expect(uf.len == 1);
    try testing.expect(mem.eql(u8, e1f[0].name, "A"));
    try testing.expect(mem.eql(u8, e2f[0].name, "A"));
    try testing.expect(mem.eql(u8, sf[0].name, "a"));
    try testing.expect(mem.eql(u8, uf[0].name, "a"));
    try testing.expect(comptime sf[0].type == u8);
    try testing.expect(comptime uf[0].type == u8);
}

pub fn fieldInfo(comptime T: type, comptime field: FieldEnum(T)) switch (@typeInfo(T)) {
    .@"struct" => Type.StructField,
    .@"union" => Type.UnionField,
    .@"enum" => Type.EnumField,
    .error_set => Type.Error,
    else => @compileError("Expected struct, union, error set or enum type, found '" ++ @typeName(T) ++ "'"),
} {
    return fields(T)[@intFromEnum(field)];
}

test fieldInfo {
    const E1 = enum {
        A,
    };
    const E2 = error{A};
    const S1 = struct {
        a: u8,
    };
    const U1 = union {
        a: u8,
    };

    const e1f = fieldInfo(E1, .A);
    const e2f = fieldInfo(E2, .A);
    const sf = fieldInfo(S1, .a);
    const uf = fieldInfo(U1, .a);

    try testing.expect(mem.eql(u8, e1f.name, "A"));
    try testing.expect(mem.eql(u8, e2f.name, "A"));
    try testing.expect(mem.eql(u8, sf.name, "a"));
    try testing.expect(mem.eql(u8, uf.name, "a"));
    try testing.expect(comptime sf.type == u8);
    try testing.expect(comptime uf.type == u8);
}

pub fn fieldNames(comptime T: type) *const [fields(T).len][:0]const u8 {
    return comptime blk: {
        const fieldInfos = fields(T);
        var names: [fieldInfos.len][:0]const u8 = undefined;
        for (&names, fieldInfos) |*name, field| name.* = field.name;
        const final = names;
        break :blk &final;
    };
}

test fieldNames {
    const E1 = enum { A, B };
    const E2 = error{A};
    const S1 = struct {
        a: u8,
    };
    const U1 = union {
        a: u8,
        b: void,
    };

    const e1names = fieldNames(E1);
    const e2names = fieldNames(E2);
    const s1names = fieldNames(S1);
    const u1names = fieldNames(U1);

    try testing.expect(e1names.len == 2);
    try testing.expectEqualSlices(u8, e1names[0], "A");
    try testing.expectEqualSlices(u8, e1names[1], "B");
    try testing.expect(e2names.len == 1);
    try testing.expectEqualSlices(u8, e2names[0], "A");
    try testing.expect(s1names.len == 1);
    try testing.expectEqualSlices(u8, s1names[0], "a");
    try testing.expect(u1names.len == 2);
    try testing.expectEqualSlices(u8, u1names[0], "a");
    try testing.expectEqualSlices(u8, u1names[1], "b");
}

/// Given an enum or error set type, returns a pointer to an array containing all tags for that
/// enum or error set.
pub fn tags(comptime T: type) *const [fields(T).len]T {
    return comptime blk: {
        const fieldInfos = fields(T);
        var res: [fieldInfos.len]T = undefined;
        for (fieldInfos, 0..) |field, i| {
            res[i] = @field(T, field.name);
        }
        const final = res;
        break :blk &final;
    };
}

test tags {
    const E1 = enum { A, B };
    const E2 = error{A};

    const e1_tags = tags(E1);
    const e2_tags = tags(E2);

    try testing.expect(e1_tags.len == 2);
    try testing.expectEqual(E1.A, e1_tags[0]);
    try testing.expectEqual(E1.B, e1_tags[1]);
    try testing.expect(e2_tags.len == 1);
    try testing.expectEqual(E2.A, e2_tags[0]);
}

/// Returns an enum with a variant named after each field of `T`.
pub fn FieldEnum(comptime T: type) type {
    const field_names = fieldNames(T);

    switch (@typeInfo(T)) {
        .@"union" => |@"union"| if (@"union".tag_type) |EnumTag| {
            for (std.enums.values(EnumTag), 0..) |v, i| {
                if (@intFromEnum(v) != i) break; // enum values not consecutive
                if (!std.mem.eql(u8, @tagName(v), field_names[i])) break; // fields out of order
            } else {
                return EnumTag;
            }
        },
        else => {},
    }

    const IntTag = std.math.IntFittingRange(0, field_names.len -| 1);
    return @Enum(IntTag, .exhaustive, field_names, &std.simd.iota(IntTag, field_names.len));
}

fn expectEqualEnum(expected: anytype, actual: @TypeOf(expected)) !void {
    try testing.expectEqual(@typeInfo(expected).@"enum", @typeInfo(actual).@"enum");
    try testing.expectEqual(
        @typeInfo(expected).@"enum".tag_type,
        @typeInfo(actual).@"enum".tag_type,
    );
    // For comparing decls and fields, we cannot use the meta eql function here
    // because the language does not guarantee that the slice pointers for field names
    // and decl names will be the same.
    comptime {
        const expected_fields = @typeInfo(expected).@"enum".fields;
        const actual_fields = @typeInfo(actual).@"enum".fields;
        if (expected_fields.len != actual_fields.len) return error.FailedTest;
        for (expected_fields, 0..) |expected_field, i| {
            const actual_field = actual_fields[i];
            try testing.expectEqual(expected_field.value, actual_field.value);
            try testing.expectEqualStrings(expected_field.name, actual_field.name);
        }
    }
    comptime {
        const expected_decls = @typeInfo(expected).@"enum".decls;
        const actual_decls = @typeInfo(actual).@"enum".decls;
        if (expected_decls.len != actual_decls.len) return error.FailedTest;
        for (expected_decls, 0..) |expected_decl, i| {
            const actual_decl = actual_decls[i];
            try testing.expectEqualStrings(expected_decl.name, actual_decl.name);
        }
    }
    try testing.expectEqual(
        @typeInfo(expected).@"enum".is_exhaustive,
        @typeInfo(actual).@"enum".is_exhaustive,
    );
}

test FieldEnum {
    try expectEqualEnum(enum {}, FieldEnum(struct {}));
    try expectEqualEnum(enum { a }, FieldEnum(struct { a: u8 }));
    try expectEqualEnum(enum { a, b, c }, FieldEnum(struct { a: u8, b: void, c: f32 }));
    try expectEqualEnum(enum { a, b, c }, FieldEnum(union { a: u8, b: void, c: f32 }));

    const Tagged = union(enum) { a: u8, b: void, c: f32 };
    try testing.expectEqual(Tag(Tagged), FieldEnum(Tagged));

    const Tag2 = enum { a, b, c };
    const Tagged2 = union(Tag2) { a: u8, b: void, c: f32 };
    try testing.expect(Tag(Tagged2) == FieldEnum(Tagged2));

    const Tag3 = enum(u8) { a, b, c = 7 };
    const Tagged3 = union(Tag3) { a: u8, b: void, c: f32 };
    try testing.expect(Tag(Tagged3) != FieldEnum(Tagged3));
}

pub fn DeclEnum(comptime T: type) type {
    const decls = declarations(T);
    var names: [decls.len][]const u8 = undefined;
    for (&names, decls) |*name, decl| name.* = decl.name;
    const IntTag = std.math.IntFittingRange(0, decls.len -| 1);
    return @Enum(IntTag, .exhaustive, &names, &std.simd.iota(IntTag, decls.len));
}

test DeclEnum {
    const A = struct {
        pub const a: u8 = 0;
    };
    const B = union {
        foo: void,

        pub const a: u8 = 0;
        pub const b: void = {};
        pub const c: f32 = 0;
    };
    const C = enum {
        bar,

        pub const a: u8 = 0;
        pub const b: void = {};
        pub const c: f32 = 0;
    };
    const D = struct {};

    try expectEqualEnum(enum { a }, DeclEnum(A));
    try expectEqualEnum(enum { a, b, c }, DeclEnum(B));
    try expectEqualEnum(enum { a, b, c }, DeclEnum(C));
    try expectEqualEnum(enum {}, DeclEnum(D));
}

pub fn Tag(comptime T: type) type {
    return switch (@typeInfo(T)) {
        .@"enum" => |info| info.tag_type,
        .@"union" => |info| info.tag_type orelse @compileError(@typeName(T) ++ " has no tag type"),
        else => @compileError("expected enum or union type, found '" ++ @typeName(T) ++ "'"),
    };
}

test Tag {
    const E = enum(u8) {
        C = 33,
        D,
    };
    const U = union(E) {
        C: u8,
        D: u16,
    };

    try testing.expect(Tag(E) == u8);
    try testing.expect(Tag(U) == E);
}

/// Returns the active tag of a tagged union
pub fn activeTag(u: anytype) Tag(@TypeOf(u)) {
    const T = @TypeOf(u);
    return @as(Tag(T), u);
}

test activeTag {
    const UE = enum {
        Int,
        Float,
    };

    const U = union(UE) {
        Int: u32,
        Float: f32,
    };

    var u = U{ .Int = 32 };
    try testing.expect(activeTag(u) == UE.Int);

    u = U{ .Float = 112.9876 };
    try testing.expect(activeTag(u) == UE.Float);
}

/// Compares two of any type for equality. Containers that do not support comparison
/// on their own are compared on a field-by-field basis. Pointers are not followed.
pub fn eql(a: anytype, b: @TypeOf(a)) bool {
    const T = @TypeOf(a);

    switch (@typeInfo(T)) {
        .@"struct" => |info| {
            if (info.layout == .@"packed") return a == b;

            inline for (info.fields) |field_info| {
                if (!eql(@field(a, field_info.name), @field(b, field_info.name))) return false;
            }
            return true;
        },
        .error_union => {
            if (a) |a_p| {
                if (b) |b_p| return eql(a_p, b_p) else |_| return false;
            } else |a_e| {
                if (b) |_| return false else |b_e| return a_e == b_e;
            }
        },
        .@"union" => |info| {
            if (info.layout == .@"packed") return a == b;
            const UnionTag = info.tag_type orelse
                @compileError("cannot compare untagged union type " ++ @typeName(T));

            const tag_a: UnionTag = a;
            const tag_b: UnionTag = b;
            if (tag_a != tag_b) return false;

            return switch (a) {
                inline else => |val, tag| return eql(val, @field(b, @tagName(tag))),
            };
        },
        .array => {
            for (a, b) |x, y| {
                if (!eql(x, y)) return false;
            }
            return true;
        },
        .vector => return @reduce(.And, a == b),
        .pointer => |info| {
            return switch (info.size) {
                .one, .many, .c => a == b,
                .slice => a.ptr == b.ptr and a.len == b.len,
            };
        },
        .optional => {
            const some_a = a orelse return b == null;
            const some_b = b orelse return false;
            return eql(some_a, some_b);
        },
        else => return a == b,
    }
}

test eql {
    const S = struct {
        a: u32,
        b: f64,
        c: [5]u8,
    };

    const U = union(enum) {
        s: S,
        f: ?f32,
    };

    const s_1 = S{
        .a = 134,
        .b = 123.3,
        .c = "12345".*,
    };

    var s_3 = S{
        .a = 134,
        .b = 123.3,
        .c = "12345".*,
    };

    const u_1 = U{ .f = 24 };
    const u_2 = U{ .s = s_1 };
    const u_3 = U{ .f = 24 };

    try testing.expect(eql(s_1, s_3));
    try testing.expect(eql(&s_1, &s_1));
    try testing.expect(!eql(&s_1, &s_3));
    try testing.expect(eql(u_1, u_3));
    try testing.expect(!eql(u_1, u_2));

    const a1 = "abcdef".*;
    const a2 = "abcdef".*;
    const a3 = "ghijkl".*;

    try testing.expect(eql(a1, a2));
    try testing.expect(!eql(a1, a3));

    const EU = struct {
        fn tst(err: bool) !u8 {
            if (err) return error.Error;
            return @as(u8, 5);
        }
    };

    try testing.expect(eql(EU.tst(true), EU.tst(true)));
    try testing.expect(eql(EU.tst(false), EU.tst(false)));
    try testing.expect(!eql(EU.tst(false), EU.tst(true)));

    const CU = union(enum) {
        a: void,
        b: void,
        c: comptime_int,
    };

    try testing.expect(eql(CU{ .a = {} }, .a));
    try testing.expect(!eql(CU{ .a = {} }, .b));

    if (builtin.cpu.arch == .hexagon) return error.SkipZigTest;

    const V = @Vector(4, u32);
    const v1: V = @splat(1);
    const v2: V = @splat(1);
    const v3: V = @splat(2);

    try testing.expect(eql(v1, v2));
    try testing.expect(!eql(v1, v3));
}

/// Given a type and a name, return the field index according to source order.
/// Returns `null` if the field is not found.
pub fn fieldIndex(comptime T: type, comptime name: []const u8) ?comptime_int {
    inline for (fields(T), 0..) |field, i| {
        if (mem.eql(u8, field.name, name))
            return i;
    }
    return null;
}

/// Deprecated: use @Int
pub fn Int(comptime signedness: std.builtin.Signedness, comptime bit_count: u16) type {
    return @Int(signedness, bit_count);
}

pub fn Float(comptime bit_count: u8) type {
    return switch (bit_count) {
        16 => f16,
        32 => f32,
        64 => f64,
        80 => f80,
        128 => f128,
        else => @compileError("invalid float bit count"),
    };
}
test Float {
    try testing.expectEqual(f16, Float(16));
    try testing.expectEqual(f32, Float(32));
    try testing.expectEqual(f64, Float(64));
    try testing.expectEqual(f80, Float(80));
    try testing.expectEqual(f128, Float(128));
}

/// For a given function type, returns a tuple type which fields will
/// correspond to the argument types.
///
/// Examples:
/// - `ArgsTuple(fn () void)` ⇒ `tuple { }`
/// - `ArgsTuple(fn (a: u32) u32)` ⇒ `tuple { u32 }`
/// - `ArgsTuple(fn (a: u32, b: f16) noreturn)` ⇒ `tuple { u32, f16 }`
pub fn ArgsTuple(comptime Function: type) type {
    const info = @typeInfo(Function);
    if (info != .@"fn")
        @compileError("ArgsTuple expects a function type");

    const function_info = info.@"fn";
    if (function_info.is_var_args)
        @compileError("Cannot create ArgsTuple for variadic function");

    var argument_field_list: [function_info.params.len]type = undefined;
    inline for (function_info.params, 0..) |arg, i| {
        const T = arg.type orelse @compileError("cannot create ArgsTuple for function with an 'anytype' parameter");
        argument_field_list[i] = T;
    }

    return Tuple(&argument_field_list);
}

/// Deprecated; use `@Tuple` instead.
///
/// To be removed after Zig 0.16.0 releases.
pub fn Tuple(comptime types: []const type) type {
    return @Tuple(types);
}

const TupleTester = struct {
    fn assertTypeEqual(comptime Expected: type, comptime Actual: type) void {
        if (Expected != Actual)
            @compileError("Expected type " ++ @typeName(Expected) ++ ", but got type " ++ @typeName(Actual));
    }

    fn assertTuple(comptime expected: anytype, comptime Actual: type) void {
        const info = @typeInfo(Actual);
        if (info != .@"struct")
            @compileError("Expected struct type");
        if (!info.@"struct".is_tuple)
            @compileError("Struct type must be a tuple type");

        const fields_list = std.meta.fields(Actual);
        if (expected.len != fields_list.len)
            @compileError("Argument count mismatch");

        inline for (fields_list, 0..) |fld, i| {
            if (expected[i] != fld.type) {
                @compileError("Field " ++ fld.name ++ " expected to be type " ++ @typeName(expected[i]) ++ ", but was type " ++ @typeName(fld.type));
            }
        }
    }
};

test ArgsTuple {
    TupleTester.assertTuple(.{}, ArgsTuple(fn () void));
    TupleTester.assertTuple(.{u32}, ArgsTuple(fn (a: u32) []const u8));
    TupleTester.assertTuple(.{ u32, f16 }, ArgsTuple(fn (a: u32, b: f16) noreturn));
    TupleTester.assertTuple(.{ u32, f16, []const u8, void }, ArgsTuple(fn (a: u32, b: f16, c: []const u8, void) noreturn));
    TupleTester.assertTuple(.{u32}, ArgsTuple(fn (comptime a: u32) []const u8));
}

test Tuple {
    TupleTester.assertTuple(.{}, Tuple(&[_]type{}));
    TupleTester.assertTuple(.{u32}, Tuple(&[_]type{u32}));
    TupleTester.assertTuple(.{ u32, f16 }, Tuple(&[_]type{ u32, f16 }));
    TupleTester.assertTuple(.{ u32, f16, []const u8, void }, Tuple(&[_]type{ u32, f16, []const u8, void }));
}

test "Tuple deduplication" {
    const T1 = std.meta.Tuple(&.{ u32, f32, i8 });
    const T2 = std.meta.Tuple(&.{ u32, f32, i8 });
    const T3 = std.meta.Tuple(&.{ u32, f32, i7 });

    if (T1 != T2) {
        @compileError("std.meta.Tuple doesn't deduplicate tuple types.");
    }
    if (T1 == T3) {
        @compileError("std.meta.Tuple fails to generate different types.");
    }
}

test "ArgsTuple forwarding" {
    const T1 = std.meta.Tuple(&.{ u32, f32, i8 });
    const T2 = std.meta.ArgsTuple(fn (u32, f32, i8) void);
    const T3 = std.meta.ArgsTuple(fn (u32, f32, i8) callconv(.c) noreturn);

    if (T1 != T2) {
        @compileError("std.meta.ArgsTuple produces different types than std.meta.Tuple");
    }
    if (T1 != T3) {
        @compileError("std.meta.ArgsTuple produces different types for the same argument lists.");
    }
}

/// Returns whether `error_union` contains an error.
pub fn isError(error_union: anytype) bool {
    return if (error_union) |_| false else |_| true;
}

test isError {
    try std.testing.expect(isError(math.divTrunc(u8, 5, 0)));
    try std.testing.expect(!isError(math.divTrunc(u8, 5, 5)));
}

/// Returns true if a type has a namespace and the namespace contains `name`;
/// `false` otherwise. Result is always comptime-known.
pub inline fn hasFn(comptime T: type, comptime name: []const u8) bool {
    switch (@typeInfo(T)) {
        .@"struct", .@"union", .@"enum", .@"opaque" => {},
        else => return false,
    }
    if (!@hasDecl(T, name))
        return false;

    return @typeInfo(@TypeOf(@field(T, name))) == .@"fn";
}

test hasFn {
    const S1 = struct {
        pub fn foo() void {}
    };

    try std.testing.expect(hasFn(S1, "foo"));
    try std.testing.expect(!hasFn(S1, "bar"));
    try std.testing.expect(!hasFn(*S1, "foo"));

    const S2 = struct {
        foo: fn () void,
    };

    try std.testing.expect(!hasFn(S2, "foo"));
}

/// Returns true if a type has a `name` method; `false` otherwise.
/// Result is always comptime-known.
pub inline fn hasMethod(comptime T: type, comptime name: []const u8) bool {
    return switch (@typeInfo(T)) {
        .pointer => |P| switch (P.size) {
            .one => hasFn(P.child, name),
            .many, .slice, .c => false,
        },
        else => hasFn(T, name),
    };
}

test hasMethod {
    try std.testing.expect(!hasMethod(u32, "foo"));
    try std.testing.expect(!hasMethod([]u32, "len"));
    try std.testing.expect(!hasMethod(struct { u32, u64 }, "len"));

    const S1 = struct {
        pub fn foo() void {}
    };

    try std.testing.expect(hasMethod(S1, "foo"));
    try std.testing.expect(hasMethod(*S1, "foo"));

    try std.testing.expect(!hasMethod(S1, "bar"));
    try std.testing.expect(!hasMethod(*[1]S1, "foo"));
    try std.testing.expect(!hasMethod(*[10]S1, "foo"));
    try std.testing.expect(!hasMethod([]S1, "foo"));

    const S2 = struct {
        foo: fn () void,
    };

    try std.testing.expect(!hasMethod(S2, "foo"));

    const U = union {
        pub fn foo() void {}
    };

    try std.testing.expect(hasMethod(U, "foo"));
    try std.testing.expect(hasMethod(*U, "foo"));
    try std.testing.expect(!hasMethod(U, "bar"));
}

/// True if every value of the type `T` has a unique bit pattern representing it.
/// In other words, `T` has no unused bits and no padding.
/// Result is always comptime-known.
pub inline fn hasUniqueRepresentation(comptime T: type) bool {
    return switch (@typeInfo(T)) {
        else => false, // TODO can we know if it's true for some of these types ?

        .@"anyframe",
        .error_set,
        .@"fn",
        => true,

        .bool => false,

        .@"enum" => |info| hasUniqueRepresentation(info.tag_type),
        .int => |info| @sizeOf(T) * 8 == info.bits,

        .pointer => |info| info.size != .slice,

        .optional => |info| switch (@typeInfo(info.child)) {
            .pointer => |ptr| !ptr.is_allowzero and switch (ptr.size) {
                .slice, .c => false,
                .one, .many => true,
            },
            else => false,
        },

        .array => |info| hasUniqueRepresentation(info.child),

        .@"struct" => |info| {
            if (info.layout == .@"packed") return @sizeOf(T) * 8 == @bitSizeOf(T);

            var sum_size = @as(usize, 0);

            inline for (info.fields) |field| {
                if (field.is_comptime) continue;
                if (!hasUniqueRepresentation(field.type)) return false;
                sum_size += @sizeOf(field.type);
            }

            return @sizeOf(T) == sum_size;
        },

        .vector => |info| hasUniqueRepresentation(info.child) and
            @sizeOf(T) == @sizeOf(info.child) * info.len,
    };
}

test hasUniqueRepresentation {
    const TestStruct1 = struct {
        a: u32,
        b: u32,
    };

    try testing.expect(hasUniqueRepresentation(TestStruct1));

    const TestStruct2 = struct {
        a: u32,
        b: u16,
    };

    try testing.expect(!hasUniqueRepresentation(TestStruct2));

    const TestStruct3 = struct {
        a: u32,
        b: u32,
    };

    try testing.expect(hasUniqueRepresentation(TestStruct3));

    const TestStruct4 = struct { a: []const u8 };

    try testing.expect(!hasUniqueRepresentation(TestStruct4));

    const TestStruct5 = struct { a: TestStruct4 };

    try testing.expect(!hasUniqueRepresentation(TestStruct5));

    const TestStruct6 = packed struct(u8) {
        @"0": bool,
        @"1": bool,
        @"2": bool,
        @"3": bool,
        @"4": bool,
        @"5": bool,
        @"6": bool,
        @"7": bool,
    };

    try testing.expect(hasUniqueRepresentation(TestStruct6));

    const TestUnion2 = extern union {
        a: u32,
        b: u16,
    };

    try testing.expect(!hasUniqueRepresentation(TestUnion2));

    const TestUnion3 = union {
        a: u32,
        b: u16,
    };

    try testing.expect(!hasUniqueRepresentation(TestUnion3));

    const TestUnion4 = union(enum) {
        a: u32,
        b: u16,
    };

    try testing.expect(!hasUniqueRepresentation(TestUnion4));

    inline for ([_]type{ i0, u8, i16, u32, i64 }) |T| {
        try testing.expect(hasUniqueRepresentation(T));
        try testing.expect(hasUniqueRepresentation(enum(T) { _ }));
    }
    inline for ([_]type{ i1, u9, i17, u33, i24 }) |T| {
        try testing.expect(!hasUniqueRepresentation(T));
        try testing.expect(!hasUniqueRepresentation(enum(T) { _ }));
    }

    try testing.expect(hasUniqueRepresentation(*u8));
    try testing.expect(hasUniqueRepresentation(*const u8));
    try testing.expect(hasUniqueRepresentation(?*u8));
    try testing.expect(hasUniqueRepresentation(?*const u8));

    try testing.expect(!hasUniqueRepresentation([]u8));
    try testing.expect(!hasUniqueRepresentation([]const u8));
    try testing.expect(!hasUniqueRepresentation(?[]u8));
    try testing.expect(!hasUniqueRepresentation(?[]const u8));

    try testing.expect(hasUniqueRepresentation(@Vector(std.simd.suggestVectorLength(u8) orelse 1, u8)));
    try testing.expect(@sizeOf(@Vector(3, u8)) == 3 or !hasUniqueRepresentation(@Vector(3, u8)));

    const StructWithComptimeFields = struct {
        comptime should_be_ignored: u64 = 42,
        comptime should_also_be_ignored: [*:0]const u8 = "hope you're having a good day :)",
        field: u32,
    };

    try testing.expect(hasUniqueRepresentation(StructWithComptimeFields));
}



---
File: /std/multi_array_list.zig
---

const std = @import("std");
const builtin = @import("builtin");
const assert = std.debug.assert;
const meta = std.meta;
const mem = std.mem;
const Allocator = mem.Allocator;
const testing = std.testing;

/// A MultiArrayList stores a list of a struct or tagged union type.
/// Instead of storing a single list of items, MultiArrayList
/// stores separate lists for each field of the struct or
/// lists of tags and bare unions.
/// This allows for memory savings if the struct or union has padding,
/// and also improves cache usage if only some fields or just tags
/// are needed for a computation.  The primary API for accessing fields is
/// the `slice()` function, which computes the start pointers
/// for the array of each field.  From the slice you can call
/// `.items(.<field_name>)` to obtain a slice of field values.
/// For unions you can call `.items(.tags)` or `.items(.data)`.
pub fn MultiArrayList(comptime T: type) type {
    return struct {
        /// This pointer is always aligned to the boundary `sizes.big_align`; this is not specified
        /// in the type to avoid `MultiArrayList(T)` depending on the alignment of `T` because this
        /// can lead to dependency loops. See `allocatedBytes` which `@alignCast`s this pointer to
        /// the correct type.
        bytes: [*]u8 = undefined,
        len: usize = 0,
        capacity: usize = 0,

        pub const empty: Self = .{
            .bytes = undefined,
            .len = 0,
            .capacity = 0,
        };

        /// Initialize with capacity to hold exactly `num` elements.
        /// Deinitialize with `deinit` or `toOwnedSlice`.
        pub fn initCapacity(gpa: Allocator, num: usize) Allocator.Error!Self {
            var self: Self = .empty;
            try self.setCapacity(gpa, num);
            return self;
        }

        const Elem = switch (@typeInfo(T)) {
            .@"struct" => T,
            .@"union" => |u| struct {
                pub const Bare = Bare: {
                    var field_names: [u.fields.len][]const u8 = undefined;
                    var field_types: [u.fields.len]type = undefined;
                    var field_attrs: [u.fields.len]std.builtin.Type.UnionField.Attributes = undefined;
                    for (u.fields, &field_names, &field_types, &field_attrs) |field, *name, *Type, *attrs| {
                        name.* = field.name;
                        Type.* = field.type;
                        attrs.* = .{ .@"align" = field.alignment };
                    }
                    break :Bare @Union(u.layout, null, &field_names, &field_types, &field_attrs);
                };
                pub const Tag =
                    u.tag_type orelse @compileError("MultiArrayList does not support untagged unions");
                tags: Tag,
                data: Bare,

                pub fn fromT(outer: T) @This() {
                    const tag = meta.activeTag(outer);
                    return .{
                        .tags = tag,
                        .data = switch (tag) {
                            inline else => |t| @unionInit(Bare, @tagName(t), @field(outer, @tagName(t))),
                        },
                    };
                }
                pub fn toT(tag: Tag, bare: Bare) T {
                    return switch (tag) {
                        inline else => |t| @unionInit(T, @tagName(t), @field(bare, @tagName(t))),
                    };
                }
            },
            else => @compileError("MultiArrayList only supports structs and tagged unions"),
        };

        pub const Field = meta.FieldEnum(Elem);

        /// A MultiArrayList.Slice contains cached start pointers for each field in the list.
        /// These pointers are not normally stored to reduce the size of the list in memory.
        /// If you are accessing multiple fields, call slice() first to compute the pointers,
        /// and then get the field arrays from the slice.
        pub const Slice = struct {
            /// This array is indexed by the field index which can be obtained
            /// by using @intFromEnum() on the Field enum
            ptrs: [fields.len][*]u8,
            len: usize,
            capacity: usize,

            pub const empty: Slice = .{
                .ptrs = undefined,
                .len = 0,
                .capacity = 0,
            };

            pub fn items(self: Slice, comptime field: Field) []FieldType(field) {
                const F = FieldType(field);
                if (self.capacity == 0) {
                    return &[_]F{};
                }
                const byte_ptr = self.ptrs[@intFromEnum(field)];
                const casted_ptr: [*]F = if (@sizeOf(F) == 0)
                    undefined
                else
                    @ptrCast(@alignCast(byte_ptr));
                return casted_ptr[0..self.len];
            }

            pub fn set(self: *Slice, index: usize, elem: T) void {
                const e = switch (@typeInfo(T)) {
                    .@"struct" => elem,
                    .@"union" => Elem.fromT(elem),
                    else => unreachable,
                };
                inline for (fields, 0..) |field_info, i| {
                    self.items(@as(Field, @enumFromInt(i)))[index] = @field(e, field_info.name);
                }
            }

            pub fn get(self: Slice, index: usize) T {
                var result: Elem = undefined;
                inline for (fields, 0..) |field_info, i| {
                    @field(result, field_info.name) = self.items(@as(Field, @enumFromInt(i)))[index];
                }
                return switch (@typeInfo(T)) {
                    .@"struct" => result,
                    .@"union" => Elem.toT(result.tags, result.data),
                    else => unreachable,
                };
            }

            pub fn toMultiArrayList(self: Slice) Self {
                if (self.ptrs.len == 0 or self.capacity == 0) {
                    return .{};
                }
                return .{
                    .bytes = self.ptrs[sizes.fields[0]],
                    .len = self.len,
                    .capacity = self.capacity,
                };
            }

            pub fn deinit(self: *Slice, gpa: Allocator) void {
                var other = self.toMultiArrayList();
                other.deinit(gpa);
                self.* = undefined;
            }

            /// Returns a `Slice` representing a range of elements in `s`, analagous to `arr[off..len]`.
            /// It is illegal to call `deinit` or `toMultiArrayList` on the returned `Slice`.
            /// Asserts that `off + len <= s.len`.
            pub fn subslice(s: Slice, off: usize, len: usize) Slice {
                assert(off + len <= s.len);
                var ptrs: [fields.len][*]u8 = undefined;
                inline for (s.ptrs, &ptrs, fields) |in, *out, field| {
                    out.* = in + (off * @sizeOf(field.type));
                }
                return .{
                    .ptrs = ptrs,
                    .len = len,
                    .capacity = len,
                };
            }

            /// This function is used in the debugger pretty formatters in tools/ to fetch the
            /// child field order and entry type to facilitate fancy debug printing for this type.
            fn dbHelper(self: *Slice, child: *Elem, field: *Field, entry: *Entry) void {
                _ = self;
                _ = child;
                _ = field;
                _ = entry;
            }
        };

        const Self = @This();

        const fields = meta.fields(Elem);
        /// `sizes.bytes` is an array of @sizeOf each T field. Sorted by alignment, descending.
        /// `sizes.fields` is an array mapping from `sizes.bytes` array index to field index.
        /// `sizes.big_align` is the overall alignment of the allocation, which equals the maximum field alignment.
        const sizes = blk: {
            const Data = struct {
                size: usize,
                size_index: usize,
                alignment: usize,
            };
            var data: [fields.len]Data = undefined;
            var big_align: usize = 1;
            for (fields, 0..) |field_info, i| {
                data[i] = .{
                    .size = @sizeOf(field_info.type),
                    .size_index = i,
                    .alignment = field_info.alignment orelse @alignOf(field_info.type),
                };
                big_align = @max(big_align, data[i].alignment);
            }
            const Sort = struct {
                fn lessThan(context: void, lhs: Data, rhs: Data) bool {
                    _ = context;
                    return lhs.alignment > rhs.alignment;
                }
            };
            @setEvalBranchQuota(3 * fields.len * std.math.log2(fields.len));
            mem.sort(Data, &data, {}, Sort.lessThan);
            var sizes_bytes: [fields.len]usize = undefined;
            var field_indexes: [fields.len]usize = undefined;
            for (data, 0..) |elem, i| {
                sizes_bytes[i] = elem.size;
                field_indexes[i] = elem.size_index;
            }
            break :blk .{
                .bytes = sizes_bytes,
                .fields = field_indexes,
                .big_align = mem.Alignment.fromByteUnits(big_align),
            };
        };

        /// Release all allocated memory.
        pub fn deinit(self: *Self, gpa: Allocator) void {
            gpa.free(self.allocatedBytes());
            self.* = undefined;
        }

        /// The caller owns the returned memory. Empties this MultiArrayList.
        pub fn toOwnedSlice(self: *Self) Slice {
            const result = self.slice();
            self.* = .{};
            return result;
        }

        /// Compute pointers to the start of each field of the array.
        /// If you need to access multiple fields, calling this may
        /// be more efficient than calling `items()` multiple times.
        pub fn slice(self: Self) Slice {
            var result: Slice = .{
                .ptrs = undefined,
                .len = self.len,
                .capacity = self.capacity,
            };
            var ptr: [*]u8 = self.bytes;
            for (sizes.bytes, sizes.fields) |field_size, i| {
                result.ptrs[i] = ptr;
                ptr += field_size * self.capacity;
            }
            return result;
        }

        /// Get the slice of values for a specified field.
        /// If you need multiple fields, consider calling slice()
        /// instead.
        pub fn items(self: Self, comptime field: Field) []FieldType(field) {
            return self.slice().items(field);
        }

        /// Overwrite one array element with new data.
        pub fn set(self: *Self, index: usize, elem: T) void {
            var slices = self.slice();
            slices.set(index, elem);
        }

        /// Obtain all the data for one array element.
        pub fn get(self: Self, index: usize) T {
            return self.slice().get(index);
        }

        /// Extend the list by 1 element.
        ///
        /// Allocates more memory as necessary.
        pub fn append(self: *Self, gpa: Allocator, elem: T) Allocator.Error!void {
            try self.ensureUnusedCapacity(gpa, 1);
            self.appendAssumeCapacity(elem);
        }

        /// Extend the list by 1 element.
        ///
        /// Asserts that capacity is sufficient to hold an additional item.
        pub fn appendAssumeCapacity(self: *Self, elem: T) void {
            assert(self.len < self.capacity);
            self.len += 1;
            self.set(self.len - 1, elem);
        }

        /// Extend the list by 1 element.
        ///
        /// If capacity is not sufficient to hold an additional
        /// item, returns `error.OutOfMemory`.
        pub fn appendBounded(self: *Self, elem: T) error{OutOfMemory}!void {
            if (self.capacity - self.len < 1) return error.OutOfMemory;
            return appendAssumeCapacity(self, elem);
        }

        /// Extend the list by 1 element, returning the newly reserved
        /// index with uninitialized data.
        ///
        /// Allocates more memory as necessary.
        pub fn addOne(self: *Self, gpa: Allocator) Allocator.Error!usize {
            try self.ensureUnusedCapacity(gpa, 1);
            return self.addOneAssumeCapacity();
        }

        /// Extend the list by 1 element, returning the newly reserved
        /// index with uninitialized data.
        ///
        /// Asserts that capacity is sufficient to hold an additional item.
        pub fn addOneAssumeCapacity(self: *Self) usize {
            assert(self.len < self.capacity);
            const index = self.len;
            self.len += 1;
            return index;
        }

        /// Extend the list by 1 element, returning the newly reserved
        /// index with uninitialized data.
        ///
        /// If capacity is not sufficient to hold an additional
        /// item, returns `error.OutOfMemory`.
        pub fn addOneBounded(self: *Self) error{OutOfMemory}!usize {
            if (self.capacity - self.len < 1) return error.OutOfMemory;
            return addOneAssumeCapacity(self);
        }

        /// Remove and return the last element from the list, or return `null` if list is empty.
        /// Invalidates pointers to fields of the removed element.
        pub fn pop(self: *Self) ?T {
            if (self.len == 0) return null;
            const val = self.get(self.len - 1);
            self.len -= 1;
            return val;
        }

        /// Inserts an item into the list. Shifts all elements
        /// after and including the specified index back by one and
        /// sets the given index to the specified element.
        ///
        /// Allocates more memory as necessary.
        pub fn insert(self: *Self, gpa: Allocator, index: usize, elem: T) !void {
            try self.ensureUnusedCapacity(gpa, 1);
            self.insertAssumeCapacity(index, elem);
        }

        /// Inserts an item into the list. Shifts all elements
        /// after and including the specified index back by one and
        /// sets the given index to the specified element.
        ///
        /// Asserts that capacity is sufficient to hold an additional item.
        pub fn insertAssumeCapacity(self: *Self, index: usize, elem: T) void {
            assert(self.len < self.capacity);
            assert(index <= self.len);
            self.len += 1;
            const entry = switch (@typeInfo(T)) {
                .@"struct" => elem,
                .@"union" => Elem.fromT(elem),
                else => unreachable,
            };
            const slices = self.slice();
            inline for (fields, 0..) |field_info, field_index| {
                const field_slice = slices.items(@as(Field, @enumFromInt(field_index)));
                var i: usize = self.len - 1;
                while (i > index) : (i -= 1) {
                    field_slice[i] = field_slice[i - 1];
                }
                field_slice[index] = @field(entry, field_info.name);
            }
        }

        /// Inserts an item into the list. Shifts all elements
        /// after and including the specified index back by one and
        /// sets the given index to the specified element.
        ///
        /// If capacity is not sufficient to hold an additional
        /// item, returns `error.OutOfMemory`.
        pub fn insertBounded(self: *Self, index: usize, elem: T) error{OutOfMemory}!void {
            if (self.capacity - self.len < 1) return error.OutOfMemory;
            return insertAssumeCapacity(self, index, elem);
        }

        /// Remove the specified item from the list, swapping the last
        /// item in the list into its position. Fast, but does not
        /// retain list ordering.
        pub fn swapRemove(self: *Self, index: usize) void {
            const slices = self.slice();
            inline for (fields, 0..) |_, i| {
                const field_slice = slices.items(@as(Field, @enumFromInt(i)));
                field_slice[index] = field_slice[self.len - 1];
                field_slice[self.len - 1] = undefined;
            }
            self.len -= 1;
        }

        /// Remove the specified item from the list, shifting items
        /// after it to preserve order.
        pub fn orderedRemove(self: *Self, index: usize) void {
            const slices = self.slice();
            inline for (fields, 0..) |_, field_index| {
                const field_slice = slices.items(@as(Field, @enumFromInt(field_index)));
                var i = index;
                while (i < self.len - 1) : (i += 1) {
                    field_slice[i] = field_slice[i + 1];
                }
                field_slice[i] = undefined;
            }
            self.len -= 1;
        }

        /// Remove the elements indexed by `sorted_indexes`. The indexes to be
        /// removed correspond to the array list before deletion.
        ///
        /// Asserts:
        /// * Each index to be removed is in bounds.
        /// * The indexes to be removed are sorted ascending.
        ///
        /// Duplicates in `sorted_indexes` are allowed.
        ///
        /// This operation is O(N).
        ///
        /// Invalidates element pointers beyond the first deleted index.
        pub fn orderedRemoveMany(self: *Self, sorted_indexes: []const usize) void {
            if (sorted_indexes.len == 0) return;
            const slices = self.slice();
            var shift: usize = 1;
            for (sorted_indexes[0 .. sorted_indexes.len - 1], sorted_indexes[1..]) |removed, end| {
                if (removed == end) continue; // allows duplicates in `sorted_indexes`
                const start = removed + 1;
                const len = end - start; // safety checks `sorted_indexes` are sorted
                inline for (fields, 0..) |_, field_index| {
                    const field_slice = slices.items(@enumFromInt(field_index));
                    @memmove(field_slice[start - shift ..][0..len], field_slice[start..][0..len]); // safety checks initial `sorted_indexes` are in range
                }
                shift += 1;
            }
            const start = sorted_indexes[sorted_indexes.len - 1] + 1;
            const end = self.len;
            const len = end - start; // safety checks final `sorted_indexes` are in range
            inline for (fields, 0..) |_, field_index| {
                const field_slice = slices.items(@enumFromInt(field_index));
                @memmove(field_slice[start - shift ..][0..len], field_slice[start..][0..len]);
            }
            self.len = end - shift;
        }

        /// Adjust the list's length to `new_len`.
        /// Does not initialize added items, if any.
        pub fn resize(self: *Self, gpa: Allocator, new_len: usize) Allocator.Error!void {
            try self.ensureTotalCapacity(gpa, new_len);
            self.len = new_len;
        }

        /// Attempt to reduce allocated capacity to `new_len`.
        /// If `new_len` is greater than zero, this may fail to reduce the capacity,
        /// but the data remains intact and the length is updated to new_len.
        pub fn shrinkAndFree(self: *Self, gpa: Allocator, new_len: usize) void {
            if (new_len == 0) return clearAndFree(self, gpa);

            assert(new_len <= self.capacity);
            assert(new_len <= self.len);

            const other_bytes = gpa.alignedAlloc(u8, sizes.big_align, capacityInBytes(new_len)) catch {
                const self_slice = self.slice();
                inline for (fields, 0..) |field_info, i| {
                    if (@sizeOf(field_info.type) != 0) {
                        const field = @as(Field, @enumFromInt(i));
                        const dest_slice = self_slice.items(field)[new_len..];
                        // We use memset here for more efficient codegen in safety-checked,
                        // valgrind-enabled builds. Otherwise the valgrind client request
                        // will be repeated for every element.
                        @memset(dest_slice, undefined);
                    }
                }
                self.len = new_len;
                return;
            };
            var other = Self{
                .bytes = other_bytes.ptr,
                .capacity = new_len,
                .len = new_len,
            };
            self.len = new_len;
            const self_slice = self.slice();
            const other_slice = other.slice();
            inline for (fields, 0..) |field_info, i| {
                if (@sizeOf(field_info.type) != 0) {
                    const field = @as(Field, @enumFromInt(i));
                    @memcpy(other_slice.items(field), self_slice.items(field));
                }
            }
            gpa.free(self.allocatedBytes());
            self.* = other;
        }

        pub fn clearAndFree(self: *Self, gpa: Allocator) void {
            gpa.free(self.allocatedBytes());
            self.* = .{};
        }

        /// Reduce length to `new_len`.
        /// Invalidates pointers to elements `items[new_len..]`.
        /// Keeps capacity the same.
        pub fn shrinkRetainingCapacity(self: *Self, new_len: usize) void {
            self.len = new_len;
        }

        /// Invalidates all element pointers.
        pub fn clearRetainingCapacity(self: *Self) void {
            self.len = 0;
        }

        /// Modify the array so that it can hold at least `new_capacity` items.
        /// Implements super-linear growth to achieve amortized O(1) append operations.
        /// Invalidates element pointers if additional memory is needed.
        pub fn ensureTotalCapacity(self: *Self, gpa: Allocator, new_capacity: usize) Allocator.Error!void {
            if (self.capacity >= new_capacity) return;
            return self.setCapacity(gpa, growCapacity(new_capacity));
        }

        const init_capacity: comptime_int = init: {
            var max: comptime_int = 1;
            for (fields) |field| max = @max(max, @sizeOf(field.type));
            break :init @max(1, std.atomic.cache_line / max);
        };

        /// Given a lower bound of required memory capacity, returns a larger value
        /// with super-linear growth.
        pub fn growCapacity(minimum: usize) usize {
            return minimum +| (minimum / 2 + init_capacity);
        }

        /// Modify the array so that it can hold at least `additional_count` **more** items.
        /// Invalidates pointers if additional memory is needed.
        pub fn ensureUnusedCapacity(self: *Self, gpa: Allocator, additional_count: usize) Allocator.Error!void {
            return self.ensureTotalCapacity(gpa, self.len + additional_count);
        }

        /// Modify the array so that it can hold exactly `new_capacity` items.
        /// Invalidates pointers if additional memory is needed.
        /// `new_capacity` must be greater or equal to `len`.
        pub fn setCapacity(self: *Self, gpa: Allocator, new_capacity: usize) Allocator.Error!void {
            assert(new_capacity >= self.len);
            const new_bytes = try gpa.alignedAlloc(u8, sizes.big_align, capacityInBytes(new_capacity));
            if (self.len == 0) {
                gpa.free(self.allocatedBytes());
                self.bytes = new_bytes.ptr;
                self.capacity = new_capacity;
                return;
            }
            var other = Self{
                .bytes = new_bytes.ptr,
                .capacity = new_capacity,
                .len = self.len,
            };
            const self_slice = self.slice();
            const other_slice = other.slice();
            inline for (fields, 0..) |field_info, i| {
                if (@sizeOf(field_info.type) != 0) {
                    const field = @as(Field, @enumFromInt(i));
                    @memcpy(other_slice.items(field), self_slice.items(field));
                }
            }
            gpa.free(self.allocatedBytes());
            self.* = other;
        }

        /// Create a copy of this list with a new backing store,
        /// using the specified allocator.
        pub fn clone(self: Self, gpa: Allocator) Allocator.Error!Self {
            var result = Self{};
            errdefer result.deinit(gpa);
            try result.ensureTotalCapacity(gpa, self.len);
            result.len = self.len;
            const self_slice = self.slice();
            const result_slice = result.slice();
            inline for (fields, 0..) |field_info, i| {
                if (@sizeOf(field_info.type) != 0) {
                    const field = @as(Field, @enumFromInt(i));
                    @memcpy(result_slice.items(field), self_slice.items(field));
                }
            }
            return result;
        }

        /// `ctx` has the following method:
        /// `fn lessThan(ctx: @TypeOf(ctx), a_index: usize, b_index: usize) bool`
        fn sortInternal(self: Self, a: usize, b: usize, ctx: anytype, comptime mode: std.sort.Mode) void {
            const sort_context: struct {
                sub_ctx: @TypeOf(ctx),
                slice: Slice,

                pub fn swap(sc: @This(), a_index: usize, b_index: usize) void {
                    inline for (fields, 0..) |field_info, i| {
                        if (@sizeOf(field_info.type) != 0) {
                            const field: Field = @enumFromInt(i);
                            const ptr = sc.slice.items(field);
                            mem.swap(field_info.type, &ptr[a_index], &ptr[b_index]);
                        }
                    }
                }

                pub fn lessThan(sc: @This(), a_index: usize, b_index: usize) bool {
                    return sc.sub_ctx.lessThan(a_index, b_index);
                }
            } = .{
                .sub_ctx = ctx,
                .slice = self.slice(),
            };

            switch (mode) {
                .stable => mem.sortContext(a, b, sort_context),
                .unstable => mem.sortUnstableContext(a, b, sort_context),
            }
        }

        /// This function guarantees a stable sort, i.e the relative order of equal elements is preserved during sorting.
        /// Read more about stable sorting here: https://en.wikipedia.org/wiki/Sorting_algorithm#Stability
        /// If this guarantee does not matter, `sortUnstable` might be a faster alternative.
        /// `ctx` has the following method:
        /// `fn lessThan(ctx: @TypeOf(ctx), a_index: usize, b_index: usize) bool`
        pub fn sort(self: Self, ctx: anytype) void {
            self.sortInternal(0, self.len, ctx, .stable);
        }

        /// Sorts only the subsection of items between indices `a` and `b` (excluding `b`)
        /// This function guarantees a stable sort, i.e the relative order of equal elements is preserved during sorting.
        /// Read more about stable sorting here: https://en.wikipedia.org/wiki/Sorting_algorithm#Stability
        /// If this guarantee does not matter, `sortSpanUnstable` might be a faster alternative.
        /// `ctx` has the following method:
        /// `fn lessThan(ctx: @TypeOf(ctx), a_index: usize, b_index: usize) bool`
        pub fn sortSpan(self: Self, a: usize, b: usize, ctx: anytype) void {
            self.sortInternal(a, b, ctx, .stable);
        }

        /// This function does NOT guarantee a stable sort, i.e the relative order of equal elements may change during sorting.
        /// Due to the weaker guarantees of this function, this may be faster than the stable `sort` method.
        /// Read more about stable sorting here: https://en.wikipedia.org/wiki/Sorting_algorithm#Stability
        /// `ctx` has the following method:
        /// `fn lessThan(ctx: @TypeOf(ctx), a_index: usize, b_index: usize) bool`
        pub fn sortUnstable(self: Self, ctx: anytype) void {
            self.sortInternal(0, self.len, ctx, .unstable);
        }

        /// Sorts only the subsection of items between indices `a` and `b` (excluding `b`)
        /// This function does NOT guarantee a stable sort, i.e the relative order of equal elements may change during sorting.
        /// Due to the weaker guarantees of this function, this may be faster than the stable `sortSpan` method.
        /// Read more about stable sorting here: https://en.wikipedia.org/wiki/Sorting_algorithm#Stability
        /// `ctx` has the following method:
        /// `fn lessThan(ctx: @TypeOf(ctx), a_index: usize, b_index: usize) bool`
        pub fn sortSpanUnstable(self: Self, a: usize, b: usize, ctx: anytype) void {
            self.sortInternal(a, b, ctx, .unstable);
        }

        pub fn capacityInBytes(capacity: usize) usize {
            comptime var elem_bytes: usize = 0;
            inline for (sizes.bytes) |size| elem_bytes += size;
            return elem_bytes * capacity;
        }

        fn allocatedBytes(self: Self) []align(sizes.big_align.toByteUnits()) u8 {
            return @alignCast(self.bytes[0..capacityInBytes(self.capacity)]);
        }

        fn FieldType(comptime field: Field) type {
            return @FieldType(Elem, @tagName(field));
        }

        const Entry = entry: {
            var field_names: [fields.len][]const u8 = undefined;
            var field_types: [fields.len]type = undefined;
            var field_attrs: [fields.len]std.builtin.Type.StructField.Attributes = undefined;
            for (sizes.fields, &field_names, &field_types, &field_attrs) |i, *name, *Type, *attrs| {
                name.* = fields[i].name ++ "_ptr";
                Type.* = *fields[i].type;
                attrs.* = .{
                    .@"comptime" = fields[i].is_comptime,
                    .@"align" = fields[i].alignment,
                };
            }
            break :entry @Struct(.@"extern", null, &field_names, &field_types, &field_attrs);
        };
        /// This function is used in the debugger pretty formatters in tools/ to fetch the
        /// child field order and entry type to facilitate fancy debug printing for this type.
        fn dbHelper(self: *Self, child: *Elem, field: *Field, entry: *Entry) void {
            _ = self;
            _ = child;
            _ = field;
            _ = entry;
        }

        comptime {
            if (builtin.zig_backend == .stage2_llvm and !builtin.strip_debug_info) {
                _ = &dbHelper;
                _ = &Slice.dbHelper;
            }
        }
    };
}

test "basic usage" {
    const ally = testing.allocator;

    const Foo = struct {
        a: u32,
        b: []const u8,
        c: u8,
    };

    var list: MultiArrayList(Foo) = .empty;
    defer list.deinit(ally);

    try testing.expectEqual(@as(usize, 0), list.items(.a).len);

    try list.ensureTotalCapacity(ally, 2);

    list.appendAssumeCapacity(.{
        .a = 1,
        .b = "foobar",
        .c = 'a',
    });

    try list.appendBounded(.{
        .a = 2,
        .b = "zigzag",
        .c = 'b',
    });

    try testing.expectEqualSlices(u32, list.items(.a), &[_]u32{ 1, 2 });
    try testing.expectEqualSlices(u8, list.items(.c), &[_]u8{ 'a', 'b' });

    try testing.expectEqual(@as(usize, 2), list.items(.b).len);
    try testing.expectEqualStrings("foobar", list.items(.b)[0]);
    try testing.expectEqualStrings("zigzag", list.items(.b)[1]);

    try list.append(ally, .{
        .a = 3,
        .b = "fizzbuzz",
        .c = 'c',
    });

    try testing.expectEqualSlices(u32, list.items(.a), &[_]u32{ 1, 2, 3 });
    try testing.expectEqualSlices(u8, list.items(.c), &[_]u8{ 'a', 'b', 'c' });

    try testing.expectEqual(@as(usize, 3), list.items(.b).len);
    try testing.expectEqualStrings("foobar", list.items(.b)[0]);
    try testing.expectEqualStrings("zigzag", list.items(.b)[1]);
    try testing.expectEqualStrings("fizzbuzz", list.items(.b)[2]);

    // Add 6 more things to force a capacity increase.
    var i: usize = 0;
    while (i < 6) : (i += 1) {
        try list.append(ally, .{
            .a = @as(u32, @intCast(4 + i)),
            .b = "whatever",
            .c = @as(u8, @intCast('d' + i)),
        });
    }

    try testing.expectEqualSlices(
        u32,
        &[_]u32{ 1, 2, 3, 4, 5, 6, 7, 8, 9 }
```
