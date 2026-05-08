```
md.suggestVectorLength(T)) |block_size| {
            const Block = @Vector(block_size, T);

            const letter_mask: Block = @splat(element);
            while (n - i >= block_size) : (i += block_size) {
                const haystack_block: Block = list[i..][0..block_size].*;
                found += std.simd.countTrues(letter_mask == haystack_block);
                if (found >= minimum) return true;
            }
        }
    }

    for (list[i..n]) |item| {
        found += @intFromBool(item == element);
        if (found >= minimum) return true;
    }

    return false;
}

test containsAtLeastScalar2 {
    try testing.expect(containsAtLeastScalar2(u8, "aa", 'a', 0));
    try testing.expect(containsAtLeastScalar2(u8, "aa", 'a', 1));
    try testing.expect(containsAtLeastScalar2(u8, "aa", 'a', 2));
    try testing.expect(!containsAtLeastScalar2(u8, "aa", 'a', 3));

    try testing.expect(containsAtLeastScalar2(u8, "adadda", 'd', 3));
    try testing.expect(!containsAtLeastScalar2(u8, "adadda", 'd', 4));
}

/// Reads an integer from memory with size equal to bytes.len.
/// ReturnType specifies the return type, which must be large enough to store
/// the result.
pub fn readVarInt(comptime ReturnType: type, bytes: []const u8, endian: Endian) ReturnType {
    assert(@typeInfo(ReturnType).int.bits >= bytes.len * 8);
    const bits = @typeInfo(ReturnType).int.bits;
    const signedness = @typeInfo(ReturnType).int.signedness;
    const WorkType = std.meta.Int(signedness, @max(16, bits));
    var result: WorkType = 0;
    switch (endian) {
        .big => {
            for (bytes) |b| {
                result = (result << 8) | b;
            }
        },
        .little => {
            const ShiftType = math.Log2Int(WorkType);
            for (bytes, 0..) |b, index| {
                result = result | (@as(WorkType, b) << @as(ShiftType, @intCast(index * 8)));
            }
        },
    }
    return @truncate(result);
}

test readVarInt {
    try testing.expect(readVarInt(u0, &[_]u8{}, .big) == 0x0);
    try testing.expect(readVarInt(u0, &[_]u8{}, .little) == 0x0);
    try testing.expect(readVarInt(u8, &[_]u8{0x12}, .big) == 0x12);
    try testing.expect(readVarInt(u8, &[_]u8{0xde}, .little) == 0xde);
    try testing.expect(readVarInt(u16, &[_]u8{ 0x12, 0x34 }, .big) == 0x1234);
    try testing.expect(readVarInt(u16, &[_]u8{ 0x12, 0x34 }, .little) == 0x3412);

    try testing.expect(readVarInt(i8, &[_]u8{0xff}, .big) == -1);
    try testing.expect(readVarInt(i8, &[_]u8{0xfe}, .little) == -2);
    try testing.expect(readVarInt(i16, &[_]u8{ 0xff, 0xfd }, .big) == -3);
    try testing.expect(readVarInt(i16, &[_]u8{ 0xfc, 0xff }, .little) == -4);

    // Return type can be oversized (bytes.len * 8 < @typeInfo(ReturnType).int.bits)
    try testing.expect(readVarInt(u9, &[_]u8{0x12}, .little) == 0x12);
    try testing.expect(readVarInt(u9, &[_]u8{0xde}, .big) == 0xde);
    try testing.expect(readVarInt(u80, &[_]u8{ 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0x24 }, .big) == 0x123456789abcdef024);
    try testing.expect(readVarInt(u80, &[_]u8{ 0xec, 0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe }, .little) == 0xfedcba9876543210ec);

    try testing.expect(readVarInt(i9, &[_]u8{0xff}, .big) == 0xff);
    try testing.expect(readVarInt(i9, &[_]u8{0xfe}, .little) == 0xfe);
}

/// Loads an integer from packed memory with provided bit_count, bit_offset, and signedness.
/// Asserts that T is large enough to store the read value.
pub fn readVarPackedInt(
    comptime T: type,
    bytes: []const u8,
    bit_offset: usize,
    bit_count: usize,
    endian: std.builtin.Endian,
    signedness: std.builtin.Signedness,
) T {
    const uN = std.meta.Int(.unsigned, @bitSizeOf(T));
    const iN = std.meta.Int(.signed, @bitSizeOf(T));
    const Log2N = std.math.Log2Int(T);

    const read_size = (bit_count + (bit_offset % 8) + 7) / 8;
    const bit_shift = @as(u3, @intCast(bit_offset % 8));
    const pad = @as(Log2N, @intCast(@bitSizeOf(T) - bit_count));

    const lowest_byte = switch (endian) {
        .big => bytes.len - (bit_offset / 8) - read_size,
        .little => bit_offset / 8,
    };
    const read_bytes = bytes[lowest_byte..][0..read_size];

    if (@bitSizeOf(T) <= 8) {
        // These are the same shifts/masks we perform below, but adds `@truncate`/`@intCast`
        // where needed since int is smaller than a byte.
        const value = if (read_size == 1) b: {
            break :b @as(uN, @truncate(read_bytes[0] >> bit_shift));
        } else b: {
            const i: u1 = @intFromBool(endian == .big);
            const head = @as(uN, @truncate(read_bytes[i] >> bit_shift));
            const tail_shift = @as(Log2N, @intCast(@as(u4, 8) - bit_shift));
            const tail = @as(uN, @truncate(read_bytes[1 - i]));
            break :b (tail << tail_shift) | head;
        };
        switch (signedness) {
            .signed => return @as(T, @intCast((@as(iN, @bitCast(value)) << pad) >> pad)),
            .unsigned => return @as(T, @intCast((@as(uN, @bitCast(value)) << pad) >> pad)),
        }
    }

    // Copy the value out (respecting endianness), accounting for bit_shift
    var int: uN = 0;
    switch (endian) {
        .big => {
            for (read_bytes[0 .. read_size - 1]) |elem| {
                int = elem | (int << 8);
            }
            int = (read_bytes[read_size - 1] >> bit_shift) | (int << (@as(u4, 8) - bit_shift));
        },
        .little => {
            int = read_bytes[0] >> bit_shift;
            for (read_bytes[1..], 0..) |elem, i| {
                int |= (@as(uN, elem) << @as(Log2N, @intCast((8 * (i + 1) - bit_shift))));
            }
        },
    }
    switch (signedness) {
        .signed => return @as(T, @intCast((@as(iN, @bitCast(int)) << pad) >> pad)),
        .unsigned => return @as(T, @intCast((@as(uN, @bitCast(int)) << pad) >> pad)),
    }
}

test readVarPackedInt {
    const T = packed struct(u16) { a: u3, b: u7, c: u6 };
    var st = T{ .a = 1, .b = 2, .c = 4 };
    const b_field = readVarPackedInt(u64, std.mem.asBytes(&st), @bitOffsetOf(T, "b"), 7, builtin.cpu.arch.endian(), .unsigned);
    try std.testing.expectEqual(st.b, b_field);
}

/// Reads an integer from memory with bit count specified by T.
/// The bit count of T must be evenly divisible by 8.
/// This function cannot fail and cannot cause undefined behavior.
pub inline fn readInt(comptime T: type, buffer: *const [@divExact(@typeInfo(T).int.bits, 8)]u8, endian: Endian) T {
    const value: T = @bitCast(buffer.*);
    return if (endian == native_endian) value else @byteSwap(value);
}

test readInt {
    try testing.expect(readInt(u0, &[_]u8{}, .big) == 0x0);
    try testing.expect(readInt(u0, &[_]u8{}, .little) == 0x0);

    try testing.expect(readInt(u8, &[_]u8{0x32}, .big) == 0x32);
    try testing.expect(readInt(u8, &[_]u8{0x12}, .little) == 0x12);

    try testing.expect(readInt(u16, &[_]u8{ 0x12, 0x34 }, .big) == 0x1234);
    try testing.expect(readInt(u16, &[_]u8{ 0x12, 0x34 }, .little) == 0x3412);

    try testing.expect(readInt(u72, &[_]u8{ 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0x24 }, .big) == 0x123456789abcdef024);
    try testing.expect(readInt(u72, &[_]u8{ 0xec, 0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe }, .little) == 0xfedcba9876543210ec);

    try testing.expect(readInt(i8, &[_]u8{0xff}, .big) == -1);
    try testing.expect(readInt(i8, &[_]u8{0xfe}, .little) == -2);

    try testing.expect(readInt(i16, &[_]u8{ 0xff, 0xfd }, .big) == -3);
    try testing.expect(readInt(i16, &[_]u8{ 0xfc, 0xff }, .little) == -4);

    try moreReadIntTests();
    try comptime moreReadIntTests();
}

fn readPackedIntLittle(comptime T: type, bytes: []const u8, bit_offset: usize) T {
    const uN = std.meta.Int(.unsigned, @bitSizeOf(T));
    const Log2N = std.math.Log2Int(T);

    const bit_count = @as(usize, @bitSizeOf(T));
    const bit_shift = @as(u3, @intCast(bit_offset % 8));

    const load_size = (bit_count + 7) / 8;
    const load_tail_bits = @as(u3, @intCast((load_size * 8) - bit_count));
    const LoadInt = std.meta.Int(.unsigned, load_size * 8);

    if (bit_count == 0)
        return 0;

    // Read by loading a LoadInt, and then follow it up with a 1-byte read
    // of the tail if bit_offset pushed us over a byte boundary.
    const read_bytes = bytes[bit_offset / 8 ..];
    const val = @as(uN, @truncate(readInt(LoadInt, read_bytes[0..load_size], .little) >> bit_shift));
    if (bit_shift > load_tail_bits) {
        const tail_bits = @as(Log2N, @intCast(bit_shift - load_tail_bits));
        const tail_byte = read_bytes[load_size];
        const tail_truncated = if (bit_count < 8) @as(uN, @truncate(tail_byte)) else @as(uN, tail_byte);
        return @as(T, @bitCast(val | (tail_truncated << (@as(Log2N, @truncate(bit_count)) -% tail_bits))));
    } else return @as(T, @bitCast(val));
}

fn readPackedIntBig(comptime T: type, bytes: []const u8, bit_offset: usize) T {
    const uN = std.meta.Int(.unsigned, @bitSizeOf(T));
    const Log2N = std.math.Log2Int(T);

    const bit_count = @as(usize, @bitSizeOf(T));
    const bit_shift = @as(u3, @intCast(bit_offset % 8));
    const byte_count = (@as(usize, bit_shift) + bit_count + 7) / 8;

    const load_size = (bit_count + 7) / 8;
    const load_tail_bits = @as(u3, @intCast((load_size * 8) - bit_count));
    const LoadInt = std.meta.Int(.unsigned, load_size * 8);

    if (bit_count == 0)
        return 0;

    // Read by loading a LoadInt, and then follow it up with a 1-byte read
    // of the tail if bit_offset pushed us over a byte boundary.
    const end = bytes.len - (bit_offset / 8);
    const read_bytes = bytes[(end - byte_count)..end];
    const val = @as(uN, @truncate(readInt(LoadInt, bytes[(end - load_size)..end][0..load_size], .big) >> bit_shift));
    if (bit_shift > load_tail_bits) {
        const tail_bits = @as(Log2N, @intCast(bit_shift - load_tail_bits));
        const tail_byte = if (bit_count < 8) @as(uN, @truncate(read_bytes[0])) else @as(uN, read_bytes[0]);
        return @as(T, @bitCast(val | (tail_byte << (@as(Log2N, @truncate(bit_count)) -% tail_bits))));
    } else return @as(T, @bitCast(val));
}

/// Deprecated: use readPackedInt(T, bytes, bit_offset, value, .native)
pub const readPackedIntNative = switch (native_endian) {
    .little => readPackedIntLittle,
    .big => readPackedIntBig,
};

/// Deprecated: use readPackedInt(T, bytes, bit_offset, value, .foreign)
pub const readPackedIntForeign = switch (native_endian) {
    .little => readPackedIntBig,
    .big => readPackedIntLittle,
};

/// Loads an integer from packed memory.
/// Asserts that buffer contains at least bit_offset + @bitSizeOf(T) bits.
pub fn readPackedInt(comptime T: type, bytes: []const u8, bit_offset: usize, endian: Endian) T {
    switch (endian) {
        .little => return readPackedIntLittle(T, bytes, bit_offset),
        .big => return readPackedIntBig(T, bytes, bit_offset),
    }
}

test readPackedInt {
    const T = packed struct(u16) { a: u3, b: u7, c: u6 };
    var st = T{ .a = 1, .b = 2, .c = 4 };
    const b_field = readPackedInt(u7, std.mem.asBytes(&st), @bitOffsetOf(T, "b"), builtin.cpu.arch.endian());
    try std.testing.expectEqual(st.b, b_field);
}

test "comptime read/write int" {
    comptime {
        var bytes: [2]u8 = undefined;
        writeInt(u16, &bytes, 0x1234, .little);
        const result = readInt(u16, &bytes, .big);
        try testing.expect(result == 0x3412);
    }
    comptime {
        var bytes: [2]u8 = undefined;
        writeInt(u16, &bytes, 0x1234, .big);
        const result = readInt(u16, &bytes, .little);
        try testing.expect(result == 0x3412);
    }
}

/// Writes an integer to memory, storing it in twos-complement.
/// This function always succeeds, has defined behavior for all inputs, but
/// the integer bit width must be divisible by 8.
pub inline fn writeInt(comptime T: type, buffer: *[@divExact(@typeInfo(T).int.bits, 8)]u8, value: T, endian: Endian) void {
    buffer.* = @bitCast(if (endian == native_endian) value else @byteSwap(value));
}

test writeInt {
    var buf0: [0]u8 = undefined;
    var buf1: [1]u8 = undefined;
    var buf2: [2]u8 = undefined;
    var buf9: [9]u8 = undefined;

    writeInt(u0, &buf0, 0x0, .big);
    try testing.expect(eql(u8, buf0[0..], &[_]u8{}));
    writeInt(u0, &buf0, 0x0, .little);
    try testing.expect(eql(u8, buf0[0..], &[_]u8{}));

    writeInt(u8, &buf1, 0x12, .big);
    try testing.expect(eql(u8, buf1[0..], &[_]u8{0x12}));
    writeInt(u8, &buf1, 0x34, .little);
    try testing.expect(eql(u8, buf1[0..], &[_]u8{0x34}));

    writeInt(u16, &buf2, 0x1234, .big);
    try testing.expect(eql(u8, buf2[0..], &[_]u8{ 0x12, 0x34 }));
    writeInt(u16, &buf2, 0x5678, .little);
    try testing.expect(eql(u8, buf2[0..], &[_]u8{ 0x78, 0x56 }));

    writeInt(u72, &buf9, 0x123456789abcdef024, .big);
    try testing.expect(eql(u8, buf9[0..], &[_]u8{ 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0x24 }));
    writeInt(u72, &buf9, 0xfedcba9876543210ec, .little);
    try testing.expect(eql(u8, buf9[0..], &[_]u8{ 0xec, 0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe }));

    writeInt(i8, &buf1, -1, .big);
    try testing.expect(eql(u8, buf1[0..], &[_]u8{0xff}));
    writeInt(i8, &buf1, -2, .little);
    try testing.expect(eql(u8, buf1[0..], &[_]u8{0xfe}));

    writeInt(i16, &buf2, -3, .big);
    try testing.expect(eql(u8, buf2[0..], &[_]u8{ 0xff, 0xfd }));
    writeInt(i16, &buf2, -4, .little);
    try testing.expect(eql(u8, buf2[0..], &[_]u8{ 0xfc, 0xff }));
}

fn writePackedIntLittle(comptime T: type, bytes: []u8, bit_offset: usize, value: T) void {
    const uN = std.meta.Int(.unsigned, @bitSizeOf(T));
    const Log2N = std.math.Log2Int(T);

    const bit_count = @as(usize, @bitSizeOf(T));
    const bit_shift = @as(u3, @intCast(bit_offset % 8));

    const store_size = (@bitSizeOf(T) + 7) / 8;
    const store_tail_bits = @as(u3, @intCast((store_size * 8) - bit_count));
    const StoreInt = std.meta.Int(.unsigned, store_size * 8);

    if (bit_count == 0)
        return;

    // Write by storing a StoreInt, and then follow it up with a 1-byte tail
    // if bit_offset pushed us over a byte boundary.
    const write_bytes = bytes[bit_offset / 8 ..];
    const head = write_bytes[0] & ((@as(u8, 1) << bit_shift) - 1);

    var write_value = (@as(StoreInt, @as(uN, @bitCast(value))) << bit_shift) | @as(StoreInt, @intCast(head));
    if (bit_shift > store_tail_bits) {
        const tail_len = @as(Log2N, @intCast(bit_shift - store_tail_bits));
        write_bytes[store_size] &= ~((@as(u8, 1) << @as(u3, @intCast(tail_len))) - 1);
        write_bytes[store_size] |= @as(u8, @intCast((@as(uN, @bitCast(value)) >> (@as(Log2N, @truncate(bit_count)) -% tail_len))));
    } else if (bit_shift < store_tail_bits) {
        const tail_len = store_tail_bits - bit_shift;
        const tail = write_bytes[store_size - 1] & (@as(u8, 0xfe) << (7 - tail_len));
        write_value |= @as(StoreInt, tail) << (8 * (store_size - 1));
    }

    writeInt(StoreInt, write_bytes[0..store_size], write_value, .little);
}

fn writePackedIntBig(comptime T: type, bytes: []u8, bit_offset: usize, value: T) void {
    const uN = std.meta.Int(.unsigned, @bitSizeOf(T));
    const Log2N = std.math.Log2Int(T);

    const bit_count = @as(usize, @bitSizeOf(T));
    const bit_shift = @as(u3, @intCast(bit_offset % 8));
    const byte_count = (bit_shift + bit_count + 7) / 8;

    const store_size = (@bitSizeOf(T) + 7) / 8;
    const store_tail_bits = @as(u3, @intCast((store_size * 8) - bit_count));
    const StoreInt = std.meta.Int(.unsigned, store_size * 8);

    if (bit_count == 0)
        return;

    // Write by storing a StoreInt, and then follow it up with a 1-byte tail
    // if bit_offset pushed us over a byte boundary.
    const end = bytes.len - (bit_offset / 8);
    const write_bytes = bytes[(end - byte_count)..end];
    const head = write_bytes[byte_count - 1] & ((@as(u8, 1) << bit_shift) - 1);

    var write_value = (@as(StoreInt, @as(uN, @bitCast(value))) << bit_shift) | @as(StoreInt, @intCast(head));
    if (bit_shift > store_tail_bits) {
        const tail_len = @as(Log2N, @intCast(bit_shift - store_tail_bits));
        write_bytes[0] &= ~((@as(u8, 1) << @as(u3, @intCast(tail_len))) - 1);
        write_bytes[0] |= @as(u8, @intCast((@as(uN, @bitCast(value)) >> (@as(Log2N, @truncate(bit_count)) -% tail_len))));
    } else if (bit_shift < store_tail_bits) {
        const tail_len = store_tail_bits - bit_shift;
        const tail = write_bytes[0] & (@as(u8, 0xfe) << (7 - tail_len));
        write_value |= @as(StoreInt, tail) << (8 * (store_size - 1));
    }

    writeInt(StoreInt, write_bytes[(byte_count - store_size)..][0..store_size], write_value, .big);
}

/// Deprecated: use writePackedInt(T, bytes, bit_offset, value, .native)
pub const writePackedIntNative = switch (native_endian) {
    .little => writePackedIntLittle,
    .big => writePackedIntBig,
};

/// Deprecated: use writePackedInt(T, bytes, bit_offset, value, .foreign)
pub const writePackedIntForeign = switch (native_endian) {
    .little => writePackedIntBig,
    .big => writePackedIntLittle,
};

/// Stores an integer to packed memory.
/// Asserts that buffer contains at least bit_offset + @bitSizeOf(T) bits.
pub fn writePackedInt(comptime T: type, bytes: []u8, bit_offset: usize, value: T, endian: Endian) void {
    switch (endian) {
        .little => writePackedIntLittle(T, bytes, bit_offset, value),
        .big => writePackedIntBig(T, bytes, bit_offset, value),
    }
}

test writePackedInt {
    const T = packed struct(u16) { a: u3, b: u7, c: u6 };
    var st = T{ .a = 1, .b = 2, .c = 4 };
    writePackedInt(u7, std.mem.asBytes(&st), @bitOffsetOf(T, "b"), 0x7f, builtin.cpu.arch.endian());
    try std.testing.expectEqual(T{ .a = 1, .b = 0x7f, .c = 4 }, st);
}

/// Stores an integer to packed memory with provided bit_offset, bit_count, and signedness.
/// If negative, the written value is sign-extended.
pub fn writeVarPackedInt(bytes: []u8, bit_offset: usize, bit_count: usize, value: anytype, endian: std.builtin.Endian) void {
    const T = @TypeOf(value);
    const uN = std.meta.Int(.unsigned, @bitSizeOf(T));

    const bit_shift = @as(u3, @intCast(bit_offset % 8));
    const write_size = (bit_count + bit_shift + 7) / 8;
    const lowest_byte = switch (endian) {
        .big => bytes.len - (bit_offset / 8) - write_size,
        .little => bit_offset / 8,
    };
    const write_bytes = bytes[lowest_byte..][0..write_size];

    if (write_size == 0) {
        return;
    } else if (write_size == 1) {
        // Single byte writes are handled specially, since we need to mask bits
        // on both ends of the byte.
        const mask = (@as(u8, 0xff) >> @as(u3, @intCast(8 - bit_count)));
        const new_bits = @as(u8, @intCast(@as(uN, @bitCast(value)) & mask)) << bit_shift;
        write_bytes[0] = (write_bytes[0] & ~(mask << bit_shift)) | new_bits;
        return;
    }

    var remaining: T = value;

    // Iterate bytes forward for Little-endian, backward for Big-endian
    const delta: i2 = if (endian == .big) -1 else 1;
    const start = if (endian == .big) @as(isize, @intCast(write_bytes.len - 1)) else 0;

    var i: isize = start; // isize for signed index arithmetic

    // Write first byte, using a mask to protects bits preceding bit_offset
    const head_mask = @as(u8, 0xff) >> bit_shift;
    write_bytes[@intCast(i)] &= ~(head_mask << bit_shift);
    write_bytes[@intCast(i)] |= @as(u8, @intCast(@as(uN, @bitCast(remaining)) & head_mask)) << bit_shift;
    remaining = math.shr(T, remaining, @as(u4, 8) - bit_shift);
    i += delta;

    // Write bytes[1..bytes.len - 1]
    if (@bitSizeOf(T) > 8) {
        const loop_end = start + delta * (@as(isize, @intCast(write_size)) - 1);
        while (i != loop_end) : (i += delta) {
            write_bytes[@as(usize, @intCast(i))] = @as(u8, @truncate(@as(uN, @bitCast(remaining))));
            remaining >>= 8;
        }
    }

    // Write last byte, using a mask to protect bits following bit_offset + bit_count
    const following_bits = -%@as(u3, @truncate(bit_shift + bit_count));
    const tail_mask = (@as(u8, 0xff) << following_bits) >> following_bits;
    write_bytes[@as(usize, @intCast(i))] &= ~tail_mask;
    write_bytes[@as(usize, @intCast(i))] |= @as(u8, @intCast(@as(uN, @bitCast(remaining)) & tail_mask));
}

test writeVarPackedInt {
    const T = packed struct(u16) { a: u3, b: u7, c: u6 };
    var st = T{ .a = 1, .b = 2, .c = 4 };
    const value: u64 = 0x7f;
    writeVarPackedInt(std.mem.asBytes(&st), @bitOffsetOf(T, "b"), 7, value, builtin.cpu.arch.endian());
    try testing.expectEqual(T{ .a = 1, .b = value, .c = 4 }, st);
}

/// Swap the byte order of all the members of the fields of a struct
/// (Changing their endianness)
pub fn byteSwapAllFields(comptime S: type, ptr: *S) void {
    byteSwapAllFieldsAligned(S, .of(S), ptr);
}

/// Swap the byte order of all the members of the fields of a struct
/// (Changing their endianness)
pub fn byteSwapAllFieldsAligned(comptime S: type, comptime a: Alignment, ptr: *align(a.toByteUnits()) S) void {
    switch (@typeInfo(S)) {
        .@"struct" => |struct_info| {
            if (struct_info.backing_integer) |Int| {
                ptr.* = @bitCast(@byteSwap(@as(Int, @bitCast(ptr.*))));
            } else inline for (std.meta.fields(S)) |f| {
                switch (@typeInfo(f.type)) {
                    .@"struct" => byteSwapAllFieldsAligned(f.type, .fromByteUnits(f.alignment orelse @alignOf(f.type)), &@field(ptr, f.name)),
                    .@"union", .array => byteSwapAllFieldsAligned(f.type, .fromByteUnits(f.alignment orelse @alignOf(f.type)), &@field(ptr, f.name)),
                    .@"enum" => {
                        @field(ptr, f.name) = @enumFromInt(@byteSwap(@intFromEnum(@field(ptr, f.name))));
                    },
                    .bool => {},
                    .float => |float_info| {
                        @field(ptr, f.name) = @bitCast(@byteSwap(@as(std.meta.Int(.unsigned, float_info.bits), @bitCast(@field(ptr, f.name)))));
                    },
                    else => {
                        @field(ptr, f.name) = @byteSwap(@field(ptr, f.name));
                    },
                }
            }
        },
        .@"union" => |union_info| {
            if (union_info.tag_type != null) {
                @compileError("byteSwapAllFields expects an untagged union");
            }

            const first_size = @bitSizeOf(union_info.fields[0].type);
            inline for (union_info.fields) |field| {
                if (@bitSizeOf(field.type) != first_size) {
                    @compileError("Unable to byte-swap unions with varying field sizes");
                }
            }

            const BackingInt = std.meta.Int(.unsigned, @bitSizeOf(S));
            ptr.* = @bitCast(@byteSwap(@as(BackingInt, @bitCast(ptr.*))));
        },
        .array => |info| {
            byteSwapAllElements(info.child, ptr);
        },
        else => {
            ptr.* = @byteSwap(ptr.*);
        },
    }
}

test byteSwapAllFields {
    const T = extern struct {
        f0: u8,
        f1: u16,
        f2: u32,
        f3: [1]u8,
        f4: bool,
        f5: f32,
        f6: extern union { f0: u16, f1: u16 },
    };
    const K = extern struct {
        f0: u8,
        f1: T,
        f2: u16,
        f3: [1]u8,
        f4: bool,
        f5: f32,
    };
    const P = packed struct(u32) {
        f0: u1,
        f1: u7,
        f2: u4,
        f3: u4,
        f4: u16,
    };
    const A = extern struct {
        f0: u32,
        f1: extern struct {
            f0: u64,
        } align(4),
        f2: u32,
    };
    var s = T{
        .f0 = 0x12,
        .f1 = 0x1234,
        .f2 = 0x12345678,
        .f3 = .{0x12},
        .f4 = true,
        .f5 = @as(f32, @bitCast(@as(u32, 0x4640e400))),
        .f6 = .{ .f0 = 0x1234 },
    };
    var k = K{
        .f0 = 0x12,
        .f1 = s,
        .f2 = 0x1234,
        .f3 = .{0x12},
        .f4 = false,
        .f5 = @as(f32, @bitCast(@as(u32, 0x45d42800))),
    };
    var p: P = @bitCast(@as(u32, 0x01234567));
    var a: A = A{
        .f0 = 0x12345678,
        .f1 = .{ .f0 = 0x123456789ABCDEF0 },
        .f2 = 0x87654321,
    };
    byteSwapAllFields(T, &s);
    byteSwapAllFields(K, &k);
    byteSwapAllFields(P, &p);
    byteSwapAllFields(A, &a);
    try std.testing.expectEqual(T{
        .f0 = 0x12,
        .f1 = 0x3412,
        .f2 = 0x78563412,
        .f3 = .{0x12},
        .f4 = true,
        .f5 = @as(f32, @bitCast(@as(u32, 0x00e44046))),
        .f6 = .{ .f0 = 0x3412 },
    }, s);
    try std.testing.expectEqual(K{
        .f0 = 0x12,
        .f1 = s,
        .f2 = 0x3412,
        .f3 = .{0x12},
        .f4 = false,
        .f5 = @as(f32, @bitCast(@as(u32, 0x0028d445))),
    }, k);
    try std.testing.expectEqual(@as(P, @bitCast(@as(u32, 0x67452301))), p);
    try std.testing.expectEqual(A{
        .f0 = 0x78563412,
        .f1 = .{ .f0 = 0xF0DEBC9A78563412 },
        .f2 = 0x21436587,
    }, a);
}

/// Reverses the byte order of all elements in a slice.
/// Handles structs, unions, arrays, enums, floats, and integers recursively.
/// Useful for converting between little-endian and big-endian representations.
pub fn byteSwapAllElements(comptime Elem: type, slice: []Elem) void {
    for (slice) |*elem| {
        switch (@typeInfo(@TypeOf(elem.*))) {
            .@"struct", .@"union", .array => byteSwapAllFields(@TypeOf(elem.*), elem),
            .@"enum" => {
                elem.* = @enumFromInt(@byteSwap(@intFromEnum(elem.*)));
            },
            .bool => {},
            .float => |float_info| {
                elem.* = @bitCast(@byteSwap(@as(std.meta.Int(.unsigned, float_info.bits), @bitCast(elem.*))));
            },
            else => {
                elem.* = @byteSwap(elem.*);
            },
        }
    }
}

/// Returns an iterator that iterates over the slices of `buffer` that are not
/// any of the items in `delimiters`.
///
/// `tokenizeAny(u8, "   abc|def ||  ghi  ", " |")` will return slices
/// for "abc", "def", "ghi", null, in that order.
///
/// If `buffer` is empty, the iterator will return null.
/// If none of `delimiters` exist in buffer,
/// the iterator will return `buffer`, null, in that order.
///
/// See also: `tokenizeSequence`, `tokenizeScalar`,
///           `splitSequence`,`splitAny`, `splitScalar`,
///           `splitBackwardsSequence`, `splitBackwardsAny`, and `splitBackwardsScalar`
pub fn tokenizeAny(comptime T: type, buffer: []const T, delimiters: []const T) TokenIterator(T, .any) {
    return .{
        .index = 0,
        .buffer = buffer,
        .delimiter = delimiters,
    };
}

/// Returns an iterator that iterates over the slices of `buffer` that are not
/// the sequence in `delimiter`.
///
/// `tokenizeSequence(u8, "<>abc><def<><>ghi", "<>")` will return slices
/// for "abc><def", "ghi", null, in that order.
///
/// If `buffer` is empty, the iterator will return null.
/// If `delimiter` does not exist in buffer,
/// the iterator will return `buffer`, null, in that order.
/// The delimiter length must not be zero.
///
/// See also: `tokenizeAny`, `tokenizeScalar`,
///           `splitSequence`,`splitAny`, and `splitScalar`
///           `splitBackwardsSequence`, `splitBackwardsAny`, and `splitBackwardsScalar`
pub fn tokenizeSequence(comptime T: type, buffer: []const T, delimiter: []const T) TokenIterator(T, .sequence) {
    assert(delimiter.len != 0);
    return .{
        .index = 0,
        .buffer = buffer,
        .delimiter = delimiter,
    };
}

/// Returns an iterator that iterates over the slices of `buffer` that are not
/// `delimiter`.
///
/// `tokenizeScalar(u8, "   abc def     ghi  ", ' ')` will return slices
/// for "abc", "def", "ghi", null, in that order.
///
/// If `buffer` is empty, the iterator will return null.
/// If `delimiter` does not exist in buffer,
/// the iterator will return `buffer`, null, in that order.
///
/// See also: `tokenizeAny`, `tokenizeSequence`,
///           `splitSequence`,`splitAny`, and `splitScalar`
///           `splitBackwardsSequence`, `splitBackwardsAny`, and `splitBackwardsScalar`
pub fn tokenizeScalar(comptime T: type, buffer: []const T, delimiter: T) TokenIterator(T, .scalar) {
    return .{
        .index = 0,
        .buffer = buffer,
        .delimiter = delimiter,
    };
}

test tokenizeScalar {
    var it = tokenizeScalar(u8, "   abc def   ghi  ", ' ');
    try testing.expect(eql(u8, it.next().?, "abc"));
    try testing.expect(eql(u8, it.peek().?, "def"));
    try testing.expect(eql(u8, it.next().?, "def"));
    try testing.expect(eql(u8, it.next().?, "ghi"));
    try testing.expect(it.next() == null);

    it = tokenizeScalar(u8, "..\\bob", '\\');
    try testing.expect(eql(u8, it.next().?, ".."));
    try testing.expect(eql(u8, "..", "..\\bob"[0..it.index]));
    try testing.expect(eql(u8, it.next().?, "bob"));
    try testing.expect(it.next() == null);

    it = tokenizeScalar(u8, "//a/b", '/');
    try testing.expect(eql(u8, it.next().?, "a"));
    try testing.expect(eql(u8, it.next().?, "b"));
    try testing.expect(eql(u8, "//a/b", "//a/b"[0..it.index]));
    try testing.expect(it.next() == null);

    it = tokenizeScalar(u8, "|", '|');
    try testing.expect(it.next() == null);
    try testing.expect(it.peek() == null);

    it = tokenizeScalar(u8, "", '|');
    try testing.expect(it.next() == null);
    try testing.expect(it.peek() == null);

    it = tokenizeScalar(u8, "hello", ' ');
    try testing.expect(eql(u8, it.next().?, "hello"));
    try testing.expect(it.next() == null);

    var it16 = tokenizeScalar(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("hello"),
        ' ',
    );
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("hello")));
    try testing.expect(it16.next() == null);
}

test tokenizeAny {
    var it = tokenizeAny(u8, "a|b,c/d e", " /,|");
    try testing.expect(eql(u8, it.next().?, "a"));
    try testing.expect(eql(u8, it.peek().?, "b"));
    try testing.expect(eql(u8, it.next().?, "b"));
    try testing.expect(eql(u8, it.next().?, "c"));
    try testing.expect(eql(u8, it.next().?, "d"));
    try testing.expect(eql(u8, it.next().?, "e"));
    try testing.expect(it.next() == null);
    try testing.expect(it.peek() == null);

    it = tokenizeAny(u8, "hello", "");
    try testing.expect(eql(u8, it.next().?, "hello"));
    try testing.expect(it.next() == null);

    var it16 = tokenizeAny(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("a|b,c/d e"),
        std.unicode.utf8ToUtf16LeStringLiteral(" /,|"),
    );
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("a")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("b")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("c")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("d")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("e")));
    try testing.expect(it16.next() == null);
}

test tokenizeSequence {
    var it = tokenizeSequence(u8, "a<>b<><>c><>d><", "<>");
    try testing.expectEqualStrings("a", it.next().?);
    try testing.expectEqualStrings("b", it.peek().?);
    try testing.expectEqualStrings("b", it.next().?);
    try testing.expectEqualStrings("c>", it.next().?);
    try testing.expectEqualStrings("d><", it.next().?);
    try testing.expect(it.next() == null);
    try testing.expect(it.peek() == null);

    var it16 = tokenizeSequence(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("a<>b<><>c><>d><"),
        std.unicode.utf8ToUtf16LeStringLiteral("<>"),
    );
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("a")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("b")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("c>")));
    try testing.expect(eql(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("d><")));
    try testing.expect(it16.next() == null);
}

test "tokenize (reset)" {
    {
        var it = tokenizeAny(u8, "   abc def   ghi  ", " ");
        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));

        it.reset();

        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));
        try testing.expect(it.next() == null);
    }
    {
        var it = tokenizeSequence(u8, "<><>abc<>def<><>ghi<>", "<>");
        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));

        it.reset();

        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));
        try testing.expect(it.next() == null);
    }
    {
        var it = tokenizeScalar(u8, "   abc def   ghi  ", ' ');
        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));

        it.reset();

        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));
        try testing.expect(it.next() == null);
    }
}

/// Returns an iterator that iterates over the slices of `buffer` that
/// are separated by the byte sequence in `delimiter`.
///
/// `splitSequence(u8, "abc||def||||ghi", "||")` will return slices
/// for "abc", "def", "", "ghi", null, in that order.
///
/// If `delimiter` does not exist in buffer,
/// the iterator will return `buffer`, null, in that order.
/// The delimiter length must not be zero.
///
/// See also: `splitAny`, `splitScalar`, `splitBackwardsSequence`,
///           `splitBackwardsAny`,`splitBackwardsScalar`,
///           `tokenizeAny`, `tokenizeSequence`, and `tokenizeScalar`.
pub fn splitSequence(comptime T: type, buffer: []const T, delimiter: []const T) SplitIterator(T, .sequence) {
    assert(delimiter.len != 0);
    return .{
        .index = 0,
        .buffer = buffer,
        .delimiter = delimiter,
    };
}

/// Returns an iterator that iterates over the slices of `buffer` that
/// are separated by any item in `delimiters`.
///
/// `splitAny(u8, "abc,def||ghi", "|,")` will return slices
/// for "abc", "def", "", "ghi", null, in that order.
///
/// If none of `delimiters` exist in buffer,
/// the iterator will return `buffer`, null, in that order.
///
/// See also: `splitSequence`, `splitScalar`, `splitBackwardsSequence`,
///           `splitBackwardsAny`,`splitBackwardsScalar`,
///           `tokenizeAny`, `tokenizeSequence`, and `tokenizeScalar`.
pub fn splitAny(comptime T: type, buffer: []const T, delimiters: []const T) SplitIterator(T, .any) {
    return .{
        .index = 0,
        .buffer = buffer,
        .delimiter = delimiters,
    };
}

/// Returns an iterator that iterates over the slices of `buffer` that
/// are separated by `delimiter`.
///
/// `splitScalar(u8, "abc|def||ghi", '|')` will return slices
/// for "abc", "def", "", "ghi", null, in that order.
///
/// If `delimiter` does not exist in buffer,
/// the iterator will return `buffer`, null, in that order.
///
/// See also: `splitSequence`, `splitAny`, `splitBackwardsSequence`,
///           `splitBackwardsAny`,`splitBackwardsScalar`,
///           `tokenizeAny`, `tokenizeSequence`, and `tokenizeScalar`.
pub fn splitScalar(comptime T: type, buffer: []const T, delimiter: T) SplitIterator(T, .scalar) {
    return .{
        .index = 0,
        .buffer = buffer,
        .delimiter = delimiter,
    };
}

test splitScalar {
    var it = splitScalar(u8, "abc|def||ghi", '|');
    try testing.expectEqualSlices(u8, it.rest(), "abc|def||ghi");
    try testing.expectEqualSlices(u8, it.first(), "abc");

    try testing.expectEqualSlices(u8, it.rest(), "def||ghi");
    try testing.expectEqualSlices(u8, it.peek().?, "def");
    try testing.expectEqualSlices(u8, it.next().?, "def");

    try testing.expectEqualSlices(u8, it.rest(), "|ghi");
    try testing.expectEqualSlices(u8, it.next().?, "");

    try testing.expectEqualSlices(u8, it.rest(), "ghi");
    try testing.expectEqualSlices(u8, it.peek().?, "ghi");
    try testing.expectEqualSlices(u8, it.next().?, "ghi");

    try testing.expectEqualSlices(u8, it.rest(), "");
    try testing.expect(it.peek() == null);
    try testing.expect(it.next() == null);

    it = splitScalar(u8, "", '|');
    try testing.expectEqualSlices(u8, it.first(), "");
    try testing.expect(it.next() == null);

    it = splitScalar(u8, "|", '|');
    try testing.expectEqualSlices(u8, it.first(), "");
    try testing.expectEqualSlices(u8, it.next().?, "");
    try testing.expect(it.peek() == null);
    try testing.expect(it.next() == null);

    it = splitScalar(u8, "hello", ' ');
    try testing.expectEqualSlices(u8, it.first(), "hello");
    try testing.expect(it.next() == null);

    var it16 = splitScalar(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("hello"),
        ' ',
    );
    try testing.expectEqualSlices(u16, it16.first(), std.unicode.utf8ToUtf16LeStringLiteral("hello"));
    try testing.expect(it16.next() == null);
}

test splitSequence {
    var it = splitSequence(u8, "a, b ,, c, d, e", ", ");
    try testing.expectEqualSlices(u8, it.first(), "a");
    try testing.expectEqualSlices(u8, it.rest(), "b ,, c, d, e");
    try testing.expectEqualSlices(u8, it.next().?, "b ,");
    try testing.expectEqualSlices(u8, it.next().?, "c");
    try testing.expectEqualSlices(u8, it.next().?, "d");
    try testing.expectEqualSlices(u8, it.next().?, "e");
    try testing.expect(it.next() == null);

    var it16 = splitSequence(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("a, b ,, c, d, e"),
        std.unicode.utf8ToUtf16LeStringLiteral(", "),
    );
    try testing.expectEqualSlices(u16, it16.first(), std.unicode.utf8ToUtf16LeStringLiteral("a"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("b ,"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("c"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("d"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("e"));
    try testing.expect(it16.next() == null);
}

test splitAny {
    var it = splitAny(u8, "a,b, c d e", ", ");
    try testing.expectEqualSlices(u8, it.first(), "a");
    try testing.expectEqualSlices(u8, it.rest(), "b, c d e");
    try testing.expectEqualSlices(u8, it.next().?, "b");
    try testing.expectEqualSlices(u8, it.next().?, "");
    try testing.expectEqualSlices(u8, it.next().?, "c");
    try testing.expectEqualSlices(u8, it.next().?, "d");
    try testing.expectEqualSlices(u8, it.next().?, "e");
    try testing.expect(it.next() == null);

    it = splitAny(u8, "hello", "");
    try testing.expect(eql(u8, it.next().?, "hello"));
    try testing.expect(it.next() == null);

    var it16 = splitAny(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("a,b, c d e"),
        std.unicode.utf8ToUtf16LeStringLiteral(", "),
    );
    try testing.expectEqualSlices(u16, it16.first(), std.unicode.utf8ToUtf16LeStringLiteral("a"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("b"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral(""));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("c"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("d"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("e"));
    try testing.expect(it16.next() == null);
}

test "split (reset)" {
    {
        var it = splitSequence(u8, "abc def ghi", " ");
        try testing.expect(eql(u8, it.first(), "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));

        it.reset();

        try testing.expect(eql(u8, it.first(), "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));
        try testing.expect(it.next() == null);
    }
    {
        var it = splitAny(u8, "abc def,ghi", " ,");
        try testing.expect(eql(u8, it.first(), "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));

        it.reset();

        try testing.expect(eql(u8, it.first(), "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));
        try testing.expect(it.next() == null);
    }
    {
        var it = splitScalar(u8, "abc def ghi", ' ');
        try testing.expect(eql(u8, it.first(), "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));

        it.reset();

        try testing.expect(eql(u8, it.first(), "abc"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "ghi"));
        try testing.expect(it.next() == null);
    }
}

/// Returns an iterator that iterates backwards over the slices of `buffer` that
/// are separated by the sequence in `delimiter`.
///
/// `splitBackwardsSequence(u8, "abc||def||||ghi", "||")` will return slices
/// for "ghi", "", "def", "abc", null, in that order.
///
/// If `delimiter` does not exist in buffer,
/// the iterator will return `buffer`, null, in that order.
/// The delimiter length must not be zero.
///
/// See also: `splitBackwardsAny`, `splitBackwardsScalar`,
///           `splitSequence`, `splitAny`,`splitScalar`,
///           `tokenizeAny`, `tokenizeSequence`, and `tokenizeScalar`.
pub fn splitBackwardsSequence(comptime T: type, buffer: []const T, delimiter: []const T) SplitBackwardsIterator(T, .sequence) {
    assert(delimiter.len != 0);
    return .{
        .index = buffer.len,
        .buffer = buffer,
        .delimiter = delimiter,
    };
}

/// Returns an iterator that iterates backwards over the slices of `buffer` that
/// are separated by any item in `delimiters`.
///
/// `splitBackwardsAny(u8, "abc,def||ghi", "|,")` will return slices
/// for "ghi", "", "def", "abc", null, in that order.
///
/// If none of `delimiters` exist in buffer,
/// the iterator will return `buffer`, null, in that order.
///
/// See also: `splitBackwardsSequence`, `splitBackwardsScalar`,
///           `splitSequence`, `splitAny`,`splitScalar`,
///           `tokenizeAny`, `tokenizeSequence`, and `tokenizeScalar`.
pub fn splitBackwardsAny(comptime T: type, buffer: []const T, delimiters: []const T) SplitBackwardsIterator(T, .any) {
    return .{
        .index = buffer.len,
        .buffer = buffer,
        .delimiter = delimiters,
    };
}

/// Returns an iterator that iterates backwards over the slices of `buffer` that
/// are separated by `delimiter`.
///
/// `splitBackwardsScalar(u8, "abc|def||ghi", '|')` will return slices
/// for "ghi", "", "def", "abc", null, in that order.
///
/// If `delimiter` does not exist in buffer,
/// the iterator will return `buffer`, null, in that order.
///
/// See also: `splitBackwardsSequence`, `splitBackwardsAny`,
///           `splitSequence`, `splitAny`,`splitScalar`,
///           `tokenizeAny`, `tokenizeSequence`, and `tokenizeScalar`.
pub fn splitBackwardsScalar(comptime T: type, buffer: []const T, delimiter: T) SplitBackwardsIterator(T, .scalar) {
    return .{
        .index = buffer.len,
        .buffer = buffer,
        .delimiter = delimiter,
    };
}

test splitBackwardsScalar {
    var it = splitBackwardsScalar(u8, "abc|def||ghi", '|');
    try testing.expectEqualSlices(u8, it.rest(), "abc|def||ghi");
    try testing.expectEqualSlices(u8, it.first(), "ghi");

    try testing.expectEqualSlices(u8, it.rest(), "abc|def|");
    try testing.expectEqualSlices(u8, it.next().?, "");

    try testing.expectEqualSlices(u8, it.rest(), "abc|def");
    try testing.expectEqualSlices(u8, it.next().?, "def");

    try testing.expectEqualSlices(u8, it.rest(), "abc");
    try testing.expectEqualSlices(u8, it.next().?, "abc");

    try testing.expectEqualSlices(u8, it.rest(), "");
    try testing.expect(it.next() == null);

    it = splitBackwardsScalar(u8, "", '|');
    try testing.expectEqualSlices(u8, it.first(), "");
    try testing.expect(it.next() == null);

    it = splitBackwardsScalar(u8, "|", '|');
    try testing.expectEqualSlices(u8, it.first(), "");
    try testing.expectEqualSlices(u8, it.next().?, "");
    try testing.expect(it.next() == null);

    it = splitBackwardsScalar(u8, "hello", ' ');
    try testing.expectEqualSlices(u8, it.first(), "hello");
    try testing.expect(it.next() == null);

    var it16 = splitBackwardsScalar(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("hello"),
        ' ',
    );
    try testing.expectEqualSlices(u16, it16.first(), std.unicode.utf8ToUtf16LeStringLiteral("hello"));
    try testing.expect(it16.next() == null);
}

test splitBackwardsSequence {
    var it = splitBackwardsSequence(u8, "a, b ,, c, d, e", ", ");
    try testing.expectEqualSlices(u8, it.rest(), "a, b ,, c, d, e");
    try testing.expectEqualSlices(u8, it.first(), "e");

    try testing.expectEqualSlices(u8, it.rest(), "a, b ,, c, d");
    try testing.expectEqualSlices(u8, it.next().?, "d");

    try testing.expectEqualSlices(u8, it.rest(), "a, b ,, c");
    try testing.expectEqualSlices(u8, it.next().?, "c");

    try testing.expectEqualSlices(u8, it.rest(), "a, b ,");
    try testing.expectEqualSlices(u8, it.next().?, "b ,");

    try testing.expectEqualSlices(u8, it.rest(), "a");
    try testing.expectEqualSlices(u8, it.next().?, "a");

    try testing.expectEqualSlices(u8, it.rest(), "");
    try testing.expect(it.next() == null);

    var it16 = splitBackwardsSequence(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("a, b ,, c, d, e"),
        std.unicode.utf8ToUtf16LeStringLiteral(", "),
    );
    try testing.expectEqualSlices(u16, it16.first(), std.unicode.utf8ToUtf16LeStringLiteral("e"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("d"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("c"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("b ,"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("a"));
    try testing.expect(it16.next() == null);
}

test splitBackwardsAny {
    var it = splitBackwardsAny(u8, "a,b, c d e", ", ");
    try testing.expectEqualSlices(u8, it.rest(), "a,b, c d e");
    try testing.expectEqualSlices(u8, it.first(), "e");

    try testing.expectEqualSlices(u8, it.rest(), "a,b, c d");
    try testing.expectEqualSlices(u8, it.next().?, "d");

    try testing.expectEqualSlices(u8, it.rest(), "a,b, c");
    try testing.expectEqualSlices(u8, it.next().?, "c");

    try testing.expectEqualSlices(u8, it.rest(), "a,b,");
    try testing.expectEqualSlices(u8, it.next().?, "");

    try testing.expectEqualSlices(u8, it.rest(), "a,b");
    try testing.expectEqualSlices(u8, it.next().?, "b");

    try testing.expectEqualSlices(u8, it.rest(), "a");
    try testing.expectEqualSlices(u8, it.next().?, "a");

    try testing.expectEqualSlices(u8, it.rest(), "");
    try testing.expect(it.next() == null);

    var it16 = splitBackwardsAny(
        u16,
        std.unicode.utf8ToUtf16LeStringLiteral("a,b, c d e"),
        std.unicode.utf8ToUtf16LeStringLiteral(", "),
    );
    try testing.expectEqualSlices(u16, it16.first(), std.unicode.utf8ToUtf16LeStringLiteral("e"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("d"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("c"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral(""));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("b"));
    try testing.expectEqualSlices(u16, it16.next().?, std.unicode.utf8ToUtf16LeStringLiteral("a"));
    try testing.expect(it16.next() == null);
}

test "splitBackwards (reset)" {
    {
        var it = splitBackwardsSequence(u8, "abc def ghi", " ");
        try testing.expect(eql(u8, it.first(), "ghi"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "abc"));

        it.reset();

        try testing.expect(eql(u8, it.first(), "ghi"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(it.next() == null);
    }
    {
        var it = splitBackwardsAny(u8, "abc def,ghi", " ,");
        try testing.expect(eql(u8, it.first(), "ghi"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "abc"));

        it.reset();

        try testing.expect(eql(u8, it.first(), "ghi"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(it.next() == null);
    }
    {
        var it = splitBackwardsScalar(u8, "abc def ghi", ' ');
        try testing.expect(eql(u8, it.first(), "ghi"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "abc"));

        it.reset();

        try testing.expect(eql(u8, it.first(), "ghi"));
        try testing.expect(eql(u8, it.next().?, "def"));
        try testing.expect(eql(u8, it.next().?, "abc"));
        try testing.expect(it.next() == null);
    }
}

/// Returns an iterator with a sliding window of slices for `buffer`.
/// The sliding window has length `size` and on every iteration moves
/// forward by `advance`.
///
/// Extract data for moving average with:
/// `window(u8, "abcdefg", 3, 1)` will return slices
/// "abc", "bcd", "cde", "def", "efg", null, in that order.
///
/// Chunk or split every N items with:
/// `window(u8, "abcdefg", 3, 3)` will return slices
/// "abc", "def", "g", null, in that order.
///
/// Pick every even index with:
/// `window(u8, "abcdefg", 1, 2)` will return slices
/// "a", "c", "e", "g" null, in that order.
///
/// The `size` and `advance` must be not be zero.
pub fn window(comptime T: type, buffer: []const T, size: usize, advance: usize) WindowIterator(T) {
    assert(size != 0);
    assert(advance != 0);
    return .{
        .index = if (buffer.len > 0) 0 else null,
        .buffer = buffer,
        .size = size,
        .advance = advance,
    };
}

test window {
    {
        // moving average size 3
        var it = window(u8, "abcdefg", 3, 1);
        try testing.expectEqualSlices(u8, "abc", it.next().?);
        try testing.expectEqualSlices(u8, "bcd", it.next().?);
        try testing.expectEqualSlices(u8, "cde", it.next().?);
        try testing.expectEqualSlices(u8, "def", it.next().?);
        try testing.expectEqualSlices(u8, "efg", it.next().?);
        try testing.expectEqual(null, it.next());

        // multibyte
        var it16 = window(u16, std.unicode.utf8ToUtf16LeStringLiteral("abcdefg"), 3, 1);
        try testing.expectEqualSlices(u16, std.unicode.utf8ToUtf16LeStringLiteral("abc"), it16.next().?);
        try testing.expectEqualSlices(u16, std.unicode.utf8ToUtf16LeStringLiteral("bcd"), it16.next().?);
        try testing.expectEqualSlices(u16, std.unicode.utf8ToUtf16LeStringLiteral("cde"), it16.next().?);
        try testing.expectEqualSlices(u16, std.unicode.utf8ToUtf16LeStringLiteral("def"), it16.next().?);
        try testing.expectEqualSlices(u16, std.unicode.utf8ToUtf16LeStringLiteral("efg"), it16.next().?);
        try testing.expectEqual(it16.next(), null);
    }

    {
        // chunk/split every 3
        var it = window(u8, "abcdefg", 3, 3);
        try testing.expectEqualSlices(u8, "abc", it.next().?);
        try testing.expectEqualSlices(u8, "def", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());
    }

    {
        // pick even
        var it = window(u8, "abcdefg", 1, 2);
        try testing.expectEqualSlices(u8, "a", it.next().?);
        try testing.expectEqualSlices(u8, "c", it.next().?);
        try testing.expectEqualSlices(u8, "e", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());

        it = window(u8, "abcdefgh", 1, 2);
        try testing.expectEqualSlices(u8, "a", it.next().?);
        try testing.expectEqualSlices(u8, "c", it.next().?);
        try testing.expectEqualSlices(u8, "e", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());
    }

    {
        // empty
        var it = window(u8, "", 1, 1);
        try testing.expectEqual(null, it.next());

        it = window(u8, "", 10, 1);
        try testing.expectEqual(null, it.next());

        it = window(u8, "", 1, 10);
        try testing.expectEqual(null, it.next());

        it = window(u8, "", 10, 10);
        try testing.expectEqual(null, it.next());
    }

    {
        // first
        var it = window(u8, "abcdefg", 3, 3);
        try testing.expectEqualSlices(u8, "abc", it.next().?);
        it.reset();
        try testing.expectEqualSlices(u8, "abc", it.next().?);
    }

    {
        // reset
        var it = window(u8, "abcdefg", 3, 3);
        try testing.expectEqualSlices(u8, "abc", it.next().?);
        try testing.expectEqualSlices(u8, "def", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());

        it.reset();
        try testing.expectEqualSlices(u8, "abc", it.next().?);
        try testing.expectEqualSlices(u8, "def", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());
    }

    {
        // size > buffer.len
        var it = window(u8, "abcdefg", 100, 1);
        try testing.expectEqualSlices(u8, "abcdefg", it.next().?);
        try testing.expectEqual(null, it.next());
    }

    {
        // advance >= buffer.len
        var it = window(u8, "abcdefg", 1, 7);
        try testing.expectEqualSlices(u8, "a", it.next().?);
        try testing.expectEqual(null, it.next());
    }

    {
        // advance == 1 and size == 1
        var it = window(u8, "abcdefg", 1, 1);
        try testing.expectEqualSlices(u8, "a", it.next().?);
        try testing.expectEqualSlices(u8, "b", it.next().?);
        try testing.expectEqualSlices(u8, "c", it.next().?);
        try testing.expectEqualSlices(u8, "d", it.next().?);
        try testing.expectEqualSlices(u8, "e", it.next().?);
        try testing.expectEqualSlices(u8, "f", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());
    }

    {
        // advance > size
        var it = window(u8, "abcdefg", 2, 3);
        try testing.expectEqualSlices(u8, "ab", it.next().?);
        try testing.expectEqualSlices(u8, "de", it.next().?);
        try testing.expectEqualSlices(u8, "g", it.next().?);
        try testing.expectEqual(null, it.next());
    }
}

/// Iterator type returned by the `window` function for sliding window operations.
pub fn WindowIterator(comptime T: type) type {
    return struct {
        buffer: []const T,
        index: ?usize,
        size: usize,
        advance: usize,

        const Self = @This();

        /// Returns a slice of the next window, or null if window is at end.
        pub fn next(self: *Self) ?[]const T {
            const start = self.index orelse return null;
            const next_index = start + self.advance;
            const end = if (start + self.size < self.buffer.len) blk: {
                self.index = if (next_index < self.buffer.len) next_index else null;
                break :blk start + self.size;
            } else blk: {
                self.index = null;
                break :blk self.buffer.len;
            };
            return self.buffer[start..end];
        }

        /// Resets the iterator to the initial window.
        pub fn reset(self: *Self) void {
            self.index = 0;
        }
    };
}

/// Returns true if haystack starts with needle.
/// Time complexity: O(needle.len)
pub fn startsWith(comptime T: type, haystack: []const T, needle: []const T) bool {
    return if (needle.len > haystack.len) false else eql(T, haystack[0..needle.len], needle);
}

test startsWith {
    try testing.expect(startsWith(u8, "Bob", "Bo"));
    try testing.expect(!startsWith(u8, "Needle in haystack", "haystack"));
}

/// Returns true if haystack ends with needle.
/// Time complexity: O(needle.len)
pub fn endsWith(comptime T: type, haystack: []const T, needle: []const T) bool {
    return if (needle.len > haystack.len) false else eql(T, haystack[haystack.len - needle.len ..], needle);
}

test endsWith {
    try testing.expect(endsWith(u8, "Needle in haystack", "haystack"));
    try testing.expect(!endsWith(u8, "Bob", "Bo"));
}

/// If `slice` starts with `prefix`, returns the rest of `slice` starting at `prefix.len`.
pub fn cutPrefix(comptime T: type, slice: []const T, prefix: []const T) ?[]const T {
    return if (startsWith(T, slice, prefix)) slice[prefix.len..] else null;
}

test cutPrefix {
    try testing.expectEqualStrings("foo", cutPrefix(u8, "--example=foo", "--example=").?);
    try testing.expectEqual(null, cutPrefix(u8, "--example=foo", "-example="));
}

/// If `slice` ends with `suffix`, returns `slice` from beginning to start of `suffix`.
pub fn cutSuffix(comptime T: type, slice: []const T, suffix: []const T) ?[]const T {
    return if (endsWith(T, slice, suffix)) slice[0 .. slice.len - suffix.len] else null;
}

test cutSuffix {
    try testing.expectEqualStrings("foo", cutSuffix(u8, "foobar", "bar").?);
    try testing.expectEqual(null, cutSuffix(u8, "foobar", "baz"));
}

/// Returns slice of `haystack` before and after first occurrence of `needle`,
/// or `null` if not found.
///
/// See also:
/// * `cutScalar`
/// * `split`
/// * `tokenizeAny`
pub fn cut(comptime T: type, haystack: []const T, needle: []const T) ?struct { []const T, []const T } {
    const index = find(T, haystack, needle) orelse return null;
    return .{ haystack[0..index], haystack[index + needle.len ..] };
}

test cut {
    try testing.expectEqual(null, cut(u8, "a b c", "B"));
    const before, const after = cut(u8, "a be c", "be") orelse return error.TestFailed;
    try testing.expectEqualStrings("a ", before);
    try testing.expectEqualStrings(" c", after);
}

/// Returns slice of `haystack` before and after last occurrence of `needle`,
/// or `null` if not found.
///
/// See also:
/// * `cut`
/// * `cutScalarLast`
pub fn cutLast(comptime T: type, haystack: []const T, needle: []const T) ?struct { []const T, []const T } {
    const index = findLast(T, haystack, needle) orelse return null;
    return .{ haystack[0..index], haystack[index + needle.len ..] };
}

test cutLast {
    try testing.expectEqual(null, cutLast(u8, "a b c", "B"));
    const before, const after = cutLast(u8, "a be c be d", "be") orelse return error.TestFailed;
    try testing.expectEqualStrings("a be c ", before);
    try testing.expectEqualStrings(" d", after);
}

/// Returns slice of `haystack` before and after first occurrence `needle`, or
/// `null` if not found.
///
/// See also:
/// * `cut`
/// * `splitScalar`
/// * `tokenizeScalar`
pub fn cutScalar(comptime T: type, haystack: []const T, needle: T) ?struct { []const T, []const T } {
    const index = findScalar(T, haystack, needle) orelse return null;
    return .{ haystack[0..index], haystack[index + 1 ..] };
}

test cutScalar {
    try testing.expectEqual(null, cutScalar(u8, "a b c", 'B'));
    const before, const after = cutScalar(u8, "a b c", 'b') orelse return error.TestFailed;
    try testing.expectEqualStrings("a ", before);
    try testing.expectEqualStrings(" c", after);
}

/// Returns slice of `haystack` before and after last occurrence of `needle`,
/// or `null` if not found.
///
/// See also:
/// * `cut`
/// * `splitScalar`
/// * `tokenizeScalar`
pub fn cutScalarLast(comptime T: type, haystack: []const T, needle: T) ?struct { []const T, []const T } {
    const index = findScalarLast(T, haystack, needle) orelse return null;
    return .{ haystack[0..index], haystack[index + 1 ..] };
}

test cutScalarLast {
    try testing.expectEqual(null, cutScalarLast(u8, "a b c", 'B'));
    const before, const after = cutScalarLast(u8, "a b c b d", 'b') orelse return error.TestFailed;
    try testing.expectEqualStrings("a b c ", before);
    try testing.expectEqualStrings(" d", after);
}

/// Delimiter type for tokenization and splitting operations.
pub const DelimiterType = enum { sequence, any, scalar };

/// Iterator type for tokenization operations, skipping empty sequences and delimiter sequences.
pub fn TokenIterator(comptime T: type, comptime delimiter_type: DelimiterType) type {
    return struct {
        buffer: []const T,
        delimiter: switch (delimiter_type) {
            .sequence, .any => []const T,
            .scalar => T,
        },
        index: usize,

        const Self = @This();

        /// Returns a slice of the current token, or null if tokenization is
        /// complete, and advances to the next token.
        pub fn next(self: *Self) ?[]const T {
            const result = self.peek() orelse return null;
            self.index += result.len;
            return result;
        }

        /// Returns a slice of the current token, or null if tokenization is
        /// complete. Does not advance to the next token.
        pub fn peek(self: *Self) ?[]const T {
            // move to beginning of token
            while (self.index < self.buffer.len and self.isDelimiter(self.index)) : (self.index += switch (delimiter_type) {
                .sequence => self.delimiter.len,
                .any, .scalar => 1,
            }) {}
            const start = self.index;
            if (start == self.buffer.len) {
                return null;
            }

            // move to end of token
            var end = start;
            while (end < self.buffer.len and !self.isDelimiter(end)) : (end += 1) {}

            return self.buffer[start..end];
        }

        /// Returns a slice of the remaining bytes. Does not affect iterator state.
        pub fn rest(self: Self) []const T {
            // move to beginning of token
            var index: usize = self.index;
            while (index < self.buffer.len and self.isDelimiter(index)) : (index += switch (delimiter_type) {
                .sequence => self.delimiter.len,
                .any, .scalar => 1,
            }) {}
            return self.buffer[index..];
        }

        /// Resets the iterator to the initial token.
        pub fn reset(self: *Self) void {
            self.index = 0;
        }

        fn isDelimiter(self: Self, index: usize) bool {
            switch (delimiter_type) {
                .sequence => return startsWith(T, self.buffer[index..], self.delimiter),
                .any => {
                    const item = self.buffer[index];
                    for (self.delimiter) |delimiter_item| {
                        if (item == delimiter_item) {
                            return true;
                        }
                    }
                    return false;
                },
                .scalar => return self.buffer[index] == self.delimiter,
            }
        }
    };
}

/// Iterator type for splitting operations, including empty sequences between delimiters.
pub fn SplitIterator(comptime T: type, comptime delimiter_type: DelimiterType) type {
    return struct {
        buffer: []const T,
        index: ?usize,
        delimiter: switch (delimiter_type) {
            .sequence, .any => []const T,
            .scalar => T,
        },

        const Self = @This();

        /// Returns a slice of the first field.
        /// Call this only to get the first field and then use `next` to get all subsequent fields.
        /// Asserts that iteration has not begun.
        pub fn first(self: *Self) []const T {
            assert(self.index.? == 0);
            return self.next().?;
        }

        /// Returns a slice of the next field, or null if splitting is complete.
        pub fn next(self: *Self) ?[]const T {
            const start = self.index orelse return null;
            const end = if (switch (delimiter_type) {
                .sequence => findPos(T, self.buffer, start, self.delimiter),
                .any => findAnyPos(T, self.buffer, start, self.delimiter),
                .scalar => findScalarPos(T, self.buffer, start, self.delimiter),
            }) |delim_start| blk: {
                self.index = delim_start + switch (delimiter_type) {
                    .sequence => self.delimiter.len,
                    .any, .scalar => 1,
                };
                break :blk delim_start;
            } else blk: {
                self.index = null;
                break :blk self.buffer.len;
            };
            return self.buffer[start..end];
        }

        /// Returns a slice of the next field, or null if splitting is complete.
        /// This method does not alter self.index.
        pub fn peek(self: *const Self) ?[]const T {
            const start = self.index orelse return null;
            const end = if (switch (delimiter_type) {
                .sequence => findPos(T, self.buffer, start, self.delimiter),
                .any => findAnyPos(T, self.buffer, start, self.delimiter),
                .scalar => findScalarPos(T, self.buffer, start, self.delimiter),
            }) |delim_start| delim_start else self.buffer.len;
            return self.buffer[start..end];
        }

        /// Returns a slice of the remaining bytes. Does not affect iterator state.
        pub fn rest(self: Self) []const T {
            const end = self.buffer.len;
            const start = self.index orelse end;
            return self.buffer[start..end];
        }

        /// Resets the iterator to the initial slice.
        pub fn reset(self: *Self) void {
            self.index = 0;
        }
    };
}

/// Iterator type for splitting operations from the end backwards, including empty sequences.
pub fn SplitBackwardsIterator(comptime T: type, comptime delimiter_type: DelimiterType) type {
    return struct {
        buffer: []const T,
        index: ?usize,
        delimiter: switch (delimiter_type) {
            .sequence, .any => []const T,
            .scalar => T,
        },

        const Self = @This();

        /// Returns a slice of the first field.
        /// Call this only to get the first field and then use `next` to get all subsequent fields.
        /// Asserts that iteration has not begun.
        pub fn first(self: *Self) []const T {
            assert(self.index.? == self.buffer.len);
            return self.next().?;
        }

        /// Returns a slice of the next field, or null if splitting is complete.
        pub fn next(self: *Self) ?[]const T {
            const end = self.index orelse return null;
            const start = if (switch (delimiter_type) {
                .sequence => lastIndexOf(T, self.buffer[0..end], self.delimiter),
                .any => lastIndexOfAny(T, self.buffer[0..end], self.delimiter),
                .scalar => findScalarLast(T, self.buffer[0..end], self.delimiter),
            }) |delim_start| blk: {
                self.index = delim_start;
                break :blk delim_start + switch (delimiter_type) {
                    .sequence => self.delimiter.len,
                    .any, .scalar => 1,
                };
            } else blk: {
                self.index = null;
                break :blk 0;
            };
            return self.buffer[start..end];
        }

        /// Returns a slice of the remaining bytes. Does not affect iterator state.
        pub fn rest(self: Self) []const T {
            const end = self.index orelse 0;
            return self.buffer[0..end];
        }

        /// Resets the iterator to the initial slice.
        pub fn reset(self: *Self) void {
            self.index = self.buffer.len;
        }
    };
}

/// Naively combines a series of slices with a separator.
/// Allocates memory for the result, which must be freed by the caller.
pub fn join(allocator: Allocator, separator: []const u8, slices: []const []const u8) Allocator.Error![]u8 {
    return joinMaybeZ(allocator, separator, slices, false);
}

/// Naively combines a series of slices with a separator and null terminator.
/// Allocates memory for the result, which must be freed by the caller.
pub fn joinZ(allocator: Allocator, separator: []const u8, slices: []const []const u8) Allocator.Error![:0]u8 {
    const out = try joinMaybeZ(allocator, separator, slices, true);
    return out[0 .. out.len - 1 :0];
}

fn joinMaybeZ(allocator: Allocator, separator: []const u8, slices: []const []const u8, zero: bool) Allocator.Error![]u8 {
    if (slices.len == 0) return if (zero) try allocator.dupe(u8, &[1]u8{0}) else &[0]u8{};

    const total_len = blk: {
        var sum: usize = separator.len * (slices.len - 1);
        for (slices) |slice| sum += slice.len;
        if (zero) sum += 1;
        break :blk sum;
    };

    const buf = try allocator.alloc(u8, total_len);
    errdefer allocator.free(buf);

    @memcpy(buf[0..slices[0].len], slices[0]);
    var buf_index: usize = slices[0].len;
    for (slices[1..]) |slice| {
        @memcpy(buf[buf_index .. buf_index + separator.len], separator);
        buf_index += separator.len;
        @memcpy(buf[buf_index .. buf_index + slice.len], slice);
        buf_index += slice.len;
    }

    if (zero) buf[buf.len - 1] = 0;

    // No need for shrink since buf is exactly the correct size.
    return buf;
}

test join {
    {
        const str = try join(testing.allocator, ",", &[_][]const u8{});
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, ""));
    }
    {
        const str = try join(testing.allocator, ",", &[_][]const u8{ "a", "b", "c" });
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "a,b,c"));
    }
    {
        const str = try join(testing.allocator, ",", &[_][]const u8{"a"});
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "a"));
    }
    {
        const str = try join(testing.allocator, ",", &[_][]const u8{ "a", "", "b", "", "c" });
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "a,,b,,c"));
    }
}

test joinZ {
    {
        const str = try joinZ(testing.allocator, ",", &[_][]const u8{});
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, ""));
        try testing.expectEqual(str[str.len], 0);
    }
    {
        const str = try joinZ(testing.allocator, ",", &[_][]const u8{ "a", "b", "c" });
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "a,b,c"));
        try testing.expectEqual(str[str.len], 0);
    }
    {
        const str = try joinZ(testing.allocator, ",", &[_][]const u8{"a"});
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "a"));
        try testing.expectEqual(str[str.len], 0);
    }
    {
        const str = try joinZ(testing.allocator, ",", &[_][]const u8{ "a", "", "b", "", "c" });
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "a,,b,,c"));
        try testing.expectEqual(str[str.len], 0);
    }
}

/// Copies each T from slices into a new slice that exactly holds all the elements.
pub fn concat(allocator: Allocator, comptime T: type, slices: []const []const T) Allocator.Error![]T {
    return concatMaybeSentinel(allocator, T, slices, null);
}

/// Copies each T from slices into a new slice that exactly holds all the elements.
pub fn concatWithSentinel(allocator: Allocator, comptime T: type, slices: []const []const T, comptime s: T) Allocator.Error![:s]T {
    const ret = try concatMaybeSentinel(allocator, T, slices, s);
    return ret[0 .. ret.len - 1 :s];
}

/// Copies each T from slices into a new slice that exactly holds all the elements as well as the sentinel.
pub fn concatMaybeSentinel(allocator: Allocator, comptime T: type, slices: []const []const T, comptime s: ?T) Allocator.Error![]T {
    if (slices.len == 0) return if (s) |sentinel| try allocator.dupe(T, &[1]T{sentinel}) else &[0]T{};

    const total_len = blk: {
        var sum: usize = 0;
        for (slices) |slice| {
            sum += slice.len;
        }

        if (s) |_| {
            sum += 1;
        }

        break :blk sum;
    };

    const buf = try allocator.alloc(T, total_len);
    errdefer allocator.free(buf);

    var buf_index: usize = 0;
    for (slices) |slice| {
        @memcpy(buf[buf_index .. buf_index + slice.len], slice);
        buf_index += slice.len;
    }

    if (s) |sentinel| {
        buf[buf.len - 1] = sentinel;
    }

    // No need for shrink since buf is exactly the correct size.
    return buf;
}

test concat {
    {
        const str = try concat(testing.allocator, u8, &[_][]const u8{ "abc", "def", "ghi" });
        defer testing.allocator.free(str);
        try testing.expect(eql(u8, str, "abcdefghi"));
    }
    {
        const str = try concat(testing.allocator, u32, &[_][]const u32{
            &[_]u32{ 0, 1 },
            &[_]u32{ 2, 3, 4 },
            &[_]u32{},
            &[_]u32{5},
        });
        defer testing.allocator.free(str);
        try testing.expect(eql(u32, str, &[_]u32{ 0, 1, 2, 3, 4, 5 }));
    }
    {
        const str = try concatWithSentinel(testing.allocator, u8, &[_][]const u8{ "abc", "def", "ghi" }, 0);
        defer testing.allocator.free(str);
        try testing.expectEqualSentinel(u8, 0, str, "abcdefghi");
    }
    {
        const slice = try concatWithSentinel(testing.allocator, u8, &[_][]const u8{}, 0);
        defer testing.allocator.free(slice);
        try testing.expectEqualSentinel(u8, 0, slice, &[_:0]u8{});
    }
    {
        const slice = try concatWithSentinel(testing.allocator, u32, &[_][]const u32{
            &[_]u32{ 0, 1 },
            &[_]u32{ 2, 3, 4 },
            &[_]u32{},
            &[_]u32{5},
        }, 2);
        defer testing.allocator.free(slice);
        try testing.expectEqualSentinel(u32, 2, slice, &[_:2]u32{ 0, 1, 2, 3, 4, 5 });
    }
}

fn moreReadIntTests() !void {
    {
        const bytes = [_]u8{
            0x12,
            0x34,
            0x56,
            0x78,
        };
        try testing.expect(readInt(u32, &bytes, .big) == 0x12345678);
        try testing.expect(readInt(u32, &bytes, .big) == 0x12345678);
        try testing.expect(readInt(i32, &bytes, .big) == 0x12345678);
        try testing.expect(readInt(u32, &bytes, .little) == 0x78563412);
        try testing.expect(readInt(u32, &bytes, .little) == 0x78563412);
        try testing.expect(readInt(i32, &bytes, .little) == 0x78563412);
    }
    {
        const buf = [_]u8{
            0x00,
            0x00,
            0x12,
            0x34,
        };
        const answer = readInt(u32, &buf, .big);
        try testing.expect(answer == 0x00001234);
    }
    {
        const buf = [_]u8{
            0x12,
            0x34,
            0x00,
            0x00,
        };
        const answer = readInt(u32, &buf, .little);
        try testing.expect(answer == 0x00003412);
    }
    {
        const bytes = [_]u8{
            0xff,
            0xfe,
        };
        try testing.expect(readInt(u16, &bytes, .big) == 0xfffe);
        try testing.expect(readInt(i16, &bytes, .big) == -0x0002);
        try testing.expect(readInt(u16, &bytes, .little) == 0xfeff);
        try testing.expect(readInt(i16, &bytes, .little) == -0x0101);
    }
}

/// Returns the smallest number in a slice. O(n).
/// `slice` must not be empty.
pub fn min(comptime T: type, slice: []const T) T {
    assert(slice.len > 0);
    var best = slice[0];
    for (slice[1..]) |item| {
        best = @min(best, item);
    }
    return best;
}

test min {
    try testing.expectEqual(min(u8, "abcdefg"), 'a');
    try testing.expectEqual(min(u8, "bcdefga"), 'a');
    try testing.expectEqual(min(u8, "a"), 'a');
}

/// Returns the largest number in a slice. O(n).
/// `slice` must not be empty.
pub fn max(comptime T: type, slice: []const T) T {
    assert(slice.len > 0);
    var best = slice[0];
    for (slice[1..]) |item| {
        best = @max(best, item);
    }
    return best;
}

test max {
    try testing.expectEqual(max(u8, "abcdefg"), 'g');
    try testing.expectEqual(max(u8, "gabcdef"), 'g');
    try testing.expectEqual(max(u8, "g"), 'g');
}

/// Finds the smallest and largest number in a slice. O(n).
/// Returns an anonymous struct with the fields `min` and `max`.
/// `slice` must not be empty.
pub fn minMax(comptime T: type, slice: []const T) struct { T, T } {
    assert(slice.len > 0);
    var running_minimum = slice[0];
    var running_maximum = slice[0];
    for (slice[1..]) |item| {
        running_minimum = @min(running_minimum, item);
        running_maximum = @max(running_maximum, item);
    }
    return .{ running_minimum, running_maximum };
}

test minMax {
    {
        const actual_min, const actual_max = minMax(u8, "abcdefg");
        try testing.expectEqual(@as(u8, 'a'), actual_min);
        try testing.expectEqual(@as(u8, 'g'), actual_max);
    }
    {
        const actual_min, const actual_max = minMax(u8, "bcdefga");
        try testing.expectEqual(@as(u8, 'a'), actual_min);
        try testing.expectEqual(@as(u8, 'g'), actual_max);
    }
    {
        const actual_min, const actual_max = minMax(u8, "a");
        try testing.expectEqual(@as(u8, 'a'), actual_min);
        try testing.expectEqual(@as(u8, 'a'), actual_max);
    }
}

/// Deprecated in favor of `findMin`.
pub const indexOfMin = findMin;

/// Returns the index of the smallest number in a slice. O(n).
/// `slice` must not be empty.
pub fn findMin(comptime T: type, slice: []const T) usize {
    assert(slice.len > 0);
    var best = slice[0];
    var index: usize = 0;
    for (slice[1..], 0..) |item, i| {
        if (item < best) {
            best = item;
            index = i + 1;
        }
    }
    return index;
}

test findMin {
    try testing.expectEqual(findMin(u8, "abcdefg"), 0);
    try testing.expectEqual(findMin(u8, "bcdefga"), 6);
    try testing.expectEqual(findMin(u8, "a"), 0);
}

pub const indexOfMax = findMax;

/// Returns the index of the largest number in a slice. O(n).
/// `slice` must not be empty.
pub fn findMax(comptime T: type, slice: []const T) usize {
    assert(slice.len > 0);
    var best = slice[0];
    var index: usize = 0;
    for (slice[1..], 0..) |item, i| {
        if (item > best) {
            best = item;
            index = i + 1;
        }
    }
    return index;
}

test findMax {
    try testing.expectEqual(findMax(u8, "abcdefg"), 6);
    try testing.expectEqual(findMax(u8, "gabcdef"), 0);
    try testing.expectEqual(findMax(u8, "a"), 0);
}

/// Deprecated in favor of `findMinMax`.
pub const indexOfMinMax = findMinMax;

/// Finds the indices of the smallest and largest number in a slice. O(n).
/// Returns the indices of the smallest and largest numbers in that order.
/// `slice` must not be empty.
pub fn findMinMax(comptime T: type, slice: []const T) struct { usize, usize } {
    assert(slice.len > 0);
    var minVal = slice[0];
    var maxVal = slice[0];
    var minIdx: usize = 0;
    var maxIdx: usize = 0;
    for (slice[1..], 0..) |item, i| {
        if (item < minVal) {
            minVal = item;
            minIdx = i + 1;
        }
        if (item > maxVal) {
            maxVal = item;
            maxIdx = i + 1;
        }
    }
    return .{ minIdx, maxIdx };
}

test findMinMax {
    try testing.expectEqual(.{ 0, 6 }, findMinMax(u8, "abcdefg"));
    try testing.expectEqual(.{ 1, 0 }, findMinMax(u8, "gabcdef"));
    try testing.expectEqual(.{ 0, 0 }, findMinMax(u8, "a"));
}

/// Exchanges contents of two memory locations.
pub fn swap(comptime T: type, noalias a: *T, noalias b: *T) void {
    if (@inComptime()) {
        // In comptime, accessing bytes of values with no defined layout is a compile error.
        const tmp = a.*;
        a.* = b.*;
        b.* = tmp;
    } else {
        // Swapping in streaming nature from start to end instead of swapping
        // everything in one step allows easier optimizations and less stack usage.
        const a_bytes: []align(@alignOf(T)) u8 = @ptrCast(a);
        const b_bytes: []align(@alignOf(T)) u8 = @ptrCast(b);
        for (a_bytes, b_bytes) |*ab, *bb| {
            const tmp = ab.*;
            ab.* = bb.*;
            bb.* = tmp;
        }
    }
}

test "swap works at comptime with types with no defined layout" {
    comptime {
        const T = struct { val: u64 };
        var a: T = .{ .val = 0 };
        var b: T = .{ .val = 1 };
        swap(T, &a, &b);
        try testing.expectEqual(T{ .val = 1 }, a);
        try testing.expectEqual(T{ .val = 0 }, b);
    }
}

inline fn reverseVector(comptime N: usize, comptime T: type, a: []T) [N]T {
    var res: [N]T = undefined;
    inline for (0..N) |i| {
        res[i] = a[N - i - 1];
    }
    return res;
}

/// In-place order reversal of a slice
pub fn reverse(comptime T: type, items: []T) void {
    var i: usize = 0;
    const end = items.len / 2;
    if (use_vectors and
        !@inComptime() and
        @bitSizeOf(T) > 0 and
        std.math.isPowerOfTwo(@bitSizeOf(T)))
    {
        if (std.simd.suggestVectorLength(T)) |simd_size| {
            if (simd_size <= end) {
                const simd_end = end - (simd_size - 1);
                while (i < simd_end) : (i += simd_size) {
                    const left_slice = items[i .. i + simd_size];
                    const right_slice = items[items.len - i - simd_size .. items.len - i];

                    const left_shuffled: [simd_size]T = reverseVector(simd_size, T, left_slice);
                    const right_shuffled: [simd_size]T = reverseVector(simd_size, T, right_slice);

                    @memcpy(right_slice, &left_shuffled);
                    @memcpy(left_slice, &right_shuffled);
                }
            }
        }
    }

    while (i < end) : (i += 1) {
        swap(T, &items[i], &items[items.len - i - 1]);
    }
}

test reverse {
    {
        var arr = [_]i32{ 5, 3, 1, 2, 4 };
        reverse(i32, arr[0..]);
        try testing.expectEqualSlices(i32, &arr, &.{ 4, 2, 1, 3, 5 });
    }
    {
        var arr = [_]u0{};
        reverse(u0, arr[0..]);
        try testing.expectEqualSlices(u0, &arr, &.{});
    }
    {
        var arr = [_]i64{ 19, 17, 15, 13, 11, 9, 7, 5, 3, 1, 2, 4, 6, 8, 10, 12, 14, 16, 18 };
        reverse(i64, arr[0..]);
        try testing.expectEqualSlices(i64, &arr, &.{ 18, 16, 14, 12, 10, 8, 6, 4, 2, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 });
    }
    {
        var arr = [_][]const u8{ "a", "b", "c", "d" };
        reverse([]const u8, arr[0..]);
        try testing.expectEqualSlices([]const u8, &arr, &.{ "d", "c", "b", "a" });
    }
    {
        const MyType = union(enum) {
            a: [3]u8,
            b: u24,
            c,
        };
        var arr = [_]MyType{ .{ .a = .{ 0, 0, 0 } }, .{ .b = 0 }, .c };
        reverse(MyType, arr[0..]);
        try testing.expectEqualSlices(MyType, &arr, &([_]MyType{ .c, .{ .b = 0 }, .{ .a = .{ 0, 0, 0 } } }));
    }
}

/// Returned by `reverseIterator`.
pub fn ReverseIterator(comptime T: type) type {
    const ptr = switch (@typeInfo(T)) {
        .pointer => |ptr| ptr,
        else => @compileError("expected slice or pointer to array, found '" ++ @typeName(T) ++ "'"),
    };
    switch (ptr.size) {
        .slice => {},
        .one => if (@typeInfo(ptr.child) != .array) @compileError("expected slice or pointer to array, found '" ++ @typeName(T) ++ "'"),
        .many, .c => @compileError("expected slice or pointer to array, found '" ++ @typeName(T) ++ "'"),
    }
    const Element = std.meta.Elem(T);
    const attrs: std.builtin.Type.Pointer.Attributes = .{
        .@"const" = ptr.is_const,
        .@"volatile" = ptr.is_volatile,
        .@"allowzero" = ptr.is_allowzero,
        .@"align" = ptr.alignment,
        .@"addrspace" = ptr.address_space,
    };
    const Pointer = @Pointer(.many, attrs, Element, std.meta.sentinel(T));
    const ElementPointer = @Pointer(.one, attrs, Element, null);
    return struct {
        ptr: Pointer,
        index: usize,
        pub fn next(self: *@This()) ?Element {
            if (self.index == 0) return null;
            self.index -= 1;
            return self.ptr[self.index];
        }
        pub fn nextPtr(self: *@This()) ?ElementPointer {
            if (self.index == 0) return null;
            self.index -= 1;
            return &self.ptr[self.index];
        }
    };
}

/// Iterates over a slice in reverse.
pub fn reverseIterator(slice: anytype) ReverseIterator(@TypeOf(slice)) {
    return .{ .ptr = slice.ptr, .index = slice.len };
}

test reverseIterator {
    {
        var it = reverseIterator("abc");
        try testing.expectEqual(@as(?u8, 'c'), it.next());
        try testing.expectEqual(@as(?u8, 'b'), it.next());
        try testing.expectEqual(@as(?u8, 'a'), it.next());
        try testing.expectEqual(@as(?u8, null), it.next());
    }
    {
        var array = [2]i32{ 3, 7 };
        const slice: []const i32 = &array;
        var it = reverseIterator(slice);
        try testing.expectEqual(@as(?i32, 7), it.next());
        try testing.expectEqual(@as(?i32, 3), it.next());
        try testing.expectEqual(@as(?i32, null), it.next());

        it = reverseIterator(slice);
        try testing.expect(*const i32 == @TypeOf(it.nextPtr().?));
        try testing.expectEqual(@as(?i32, 7), it.nextPtr().?.*);
        try testing.expectEqual(@as(?i32, 3), it.nextPtr().?.*);
        try testing.expectEqual(@as(?*const i32, null), it.nextPtr());

        const mut_slice: []i32 = &array;
        var mut_it = reverseIterator(mut_slice);
        mut_it.nextPtr().?.* += 1;
        mut_it.nextPtr().?.* += 2;
        try testing.expectEqual([2]i32{ 5, 8 }, array);
    }
    {
        var array = [2]i32{ 3, 7 };
        const ptr_to_array: *const [2]i32 = &array;
        var it = reverseIterator(ptr_to_array);
        try testing.expectEqual(@as(?i32, 7), it.next());
        try testing.expectEqual(@as(?i32, 3), it.next());
        try testing.expectEqual(@as(?i32, null), it.next());

        it = reverseIterator(ptr_to_array);
        try testing.expect(*const i32 == @TypeOf(it.nextPtr().?));
        try testing.expectEqual(@as(?i32, 7), it.nextPtr().?.*);
        try testing.expectEqual(@as(?i32, 3), it.nextPtr().?.*);
        try testing.expectEqual(@as(?*const i32, null), it.nextPtr());

        const mut_ptr_to_array: *[2]i32 = &array;
        var mut_it = reverseIterator(mut_ptr_to_array);
        mut_it.nextPtr().?.* += 1;
        mut_it.nextPtr().?.* += 2;
        try testing.expectEqual([2]i32{ 5, 8 }, array);
    }
}

/// In-place rotation of the values in an array ([0 1 2 3] becomes [1 2 3 0] if we rotate by 1)
/// Assumes 0 <= amount <= items.len
pub fn rotate(comptime T: type, items: []T, amount: usize) void {
    reverse(T, items[0..amount]);
    reverse(T, items[amount..]);
    reverse(T, items);
}

test rotate {
    var arr = [_]i32{ 5, 3, 1, 2, 4 };
    rotate(i32, arr[0..], 2);

    try testing.expect(eql(i32, &arr, &[_]i32{ 1, 2, 4, 5, 3 }));
}

/// Replace needle with replacement as many times as possible, writing to an output buffer which is assumed to be of
/// appropriate size. Use replacementSize to calculate an appropriate buffer size.
/// The `input` and `output` slices must not overlap.
/// The needle must not be empty.
/// Returns the number of replacements made.
pub fn replace(comptime T: type, input: []const T, needle: []const T, replacement: []const T, output: []T) usize {
    // Empty needle will loop until output buffer overflows.
    assert(needle.len > 0);

    var i: usize = 0;
    var slide: usize = 0;
    var replacements: usize = 0;
    while (slide < input.len) {
        if (mem.startsWith(T, input[slide..], needle)) {
            @memcpy(output[i..][0..replacement.len], replacement);
            i += replacement.len;
            slide += needle.len;
            replacements += 1;
        } else {
            output[i] = input[slide];
            i += 1;
            slide += 1;
        }
    }

    return replacements;
}

test replace {
    var output: [29]u8 = undefined;
    var replacements = replace(u8, "All your base are belong to us", "base", "Zig", output[0..]);
    var expected: []const u8 = "All your Zig are belong to us";
    try testing.expect(replacements == 1);
    try testing.expectEqualStrings(expected, output[0..expected.len]);

    replacements = replace(u8, "Favor reading code over writing code.", "code", "", output[0..]);
    expected = "Favor reading  over writing .";
    try testing.expect(replacements == 2);
    try testing.expectEqualStrings(expected, output[0..expected.len]);

    // Empty needle is not allowed but input may be empty.
    replacements = replace(u8, "", "x", "y", output[0..0]);
    expected = "";
    try testing.expect(replacements == 0);
    try testing.expectEqualStrings(expected, output[0..expected.len]);

    // Adjacent replacements.

    replacements = replace(u8, "\\n\\n", "\\n", "\n", output[0..]);
    expected = "\n\n";
    try testing.expect(replacements == 2);
    try testing.expectEqualStrings(expected, output[0..expected.len]);

    replacements = replace(u8, "abbba", "b", "cd", output[0..]);
    expected = "acdcdcda";
    try testing.expect(replacements == 3);
    try testing.expectEqualStrings(expected, output[0..expected.len]);
}

/// Replace all occurrences of `match` with `replacement`.
pub fn replaceScalar(comptime T: type, slice: []T, match: T, replacement: T) void {
    for (slice) |*e| {
        if (e.* == match)
            e.* = replacement;
    }
}

/// Collapse consecutive duplicate elements into one entry.
pub fn collapseRepeatsLen(comptime T: type, slice: []T, elem: T) usize {
    if (slice.len == 0) return 0;
    var write_idx: usize = 1;
    var read_idx: usize = 1;
    while (read_idx < slice.len) : (read_idx += 1) {
        if (slice[read_idx - 1] != elem or slice[read_idx] != elem) {
            slice[write_idx] = slice[read_idx];
            write_idx += 1;
        }
    }
    return write_idx;
}

/// Collapse consecutive duplicate elements into one entry.
pub fn collapseRepeats(comptime T: type, slice: []T, elem: T) []T {
    return slice[0..collapseRepeatsLen(T, slice, elem)];
}

fn testCollapseRepeats(str: []const u8, elem: u8, expected: []const u8) !void {
    const mutable = try std.testing.allocator.dupe(u8, str);
    defer std.testing.allocator.free(mutable);
    try testing.expect(std.mem.eql(u8, collapseRepeats(u8, mutable, elem), expected));
}
test collapseRepeats {
    try testCollapseRepeats("", '/', "");
    try testCollapseRepeats("a", '/', "a");
    try testCollapseRepeats("/", '/', "/");
    try testCollapseRepeats("//", '/', "/");
    try testCollapseRepeats("/a", '/', "/a");
    try testCollapseRepeats("//a", '/', "/a");
    try testCollapseRepeats("a/", '/', "a/");
    try testCollapseRepeats("a//", '/', "a/");
    try testCollapseRepeats("a/a", '/', "a/a");
    try testCollapseRepeats("a//a", '/', "a/a");
    try testCollapseRepeats("//a///a////", '/', "/a/a/");
}

/// Calculate the size needed in an output buffer to perform a replacement.
/// The needle must not be empty.
pub fn replacementSize(comptime T: type, input: []const T, needle: []const T, replacement: []const T) usize {
    // Empty needle will loop forever.
    assert(needle.len > 0);

    var i: usize = 0;
    var size: usize = input.len;
    while (i < input.len) {
        if (mem.startsWith(T, input[i..], needle)) {
            size = size - needle.len + replacement.len;
            i += needle.len;
        } else {
            i += 1;
        }
    }

    return size;
}

test replacementSize {
    try testing.expect(replacementSize(u8, "All your base are belong to us", "base", "Zig") == 29);
    try testing.expect(replacementSize(u8, "Favor reading code over writing code.", "code", "") == 29);
    try testing.expect(replacementSize(u8, "Only one obvious way to do things.", "things.", "things in Zig.") == 41);

    // Empty needle is not allowed but input may be empty.
    try testing.expect(replacementSize(u8, "", "x", "y") == 0);

    // Adjacent replacements.
    try testing.expect(replacementSize(u8, "\\n\\n", "\\n", "\n") == 2);
    try testing.expect(replacementSize(u8, "abbba", "b", "cd") == 8);
}

/// Perform a replacement on an allocated buffer of pre-determined size. Caller must free returned memory.
pub fn replaceOwned(comptime T: type, allocator: Allocator, input: []const T, needle: []const T, replacement: []const T) Allocator.Error![]T {
    const output = try allocator.alloc(T, replacementSize(T, input, needle, replacement));
    _ = replace(T, input, needle, replacement, output);
    return output;
}

test replaceOwned {
    const gpa = std.testing.allocator;

    const base_replace = replaceOwned(u8, gpa, "All your base are belong to us", "base", "Zig") catch @panic("out of memory");
    defer gpa.free(base_replace);
    try testing.expect(eql(u8, base_replace, "All your Zig are belong to us"));

    const zen_replace = replaceOwned(u8, gpa, "Favor reading code over writing code.", " code", "") catch @panic("out of memory");
    defer gpa.free(zen_replace);
    try testing.expect(eql(u8, zen_replace, "Favor reading over writing."));
}

/// Converts a little-endian integer to host endianness.
pub fn littleToNative(comptime T: type, x: T) T {
    return switch (native_endian) {
        .little => x,
        .big => @byteSwap(x),
    };
}

/// Converts a big-endian integer to host endianness.
pub fn bigToNative(comptime T: type, x: T) T {
    return switch (native_endian) {
        .little => @byteSwap(x),
        .big => x,
    };
}

/// Converts an integer from specified endianness to host endianness.
pub fn toNative(comptime T: type, x: T, endianness_of_x: Endian) T {
    return switch (endianness_of_x) {
        .little => littleToNative(T, x),
        .big => bigToNative(T, x),
    };
}

/// Converts an integer which has host endianness to the desired endianness.
pub fn nativeTo(comptime T: type, x: T, desired_endianness: Endian) T {
    return switch (desired_endianness) {
        .little => nativeToLittle(T, x),
        .big => nativeToBig(T, x),
    };
}

/// Converts an integer which has host endianness to little endian.
pub fn nativeToLittle(comptime T: type, x: T) T {
    return switch (native_endian) {
        .little => x,
        .big => @byteSwap(x),
    };
}

/// Converts an integer which has host endianness to big endian.
pub fn nativeToBig(comptime T: type, x: T) T {
    return switch (native_endian) {
        .little => @byteSwap(x),
        .big => x,
    };
}

/// Returns the number of elements that, if added to the given pointer, align it
/// to a multiple of the given quantity, or `null` if one of the following
/// conditions is met:
/// - The aligned pointer would not fit the address space,
/// - The delta required to align the pointer is not a multiple of the pointee's
///   type.
pub fn alignPointerOffset(ptr: anytype, align_to: usize) ?usize {
    assert(isValidAlign(align_to));

    const T = @TypeOf(ptr);
    const info = @typeInfo(T);
    if (info != .pointer or info.pointer.size != .many)
        @compileError("expected many item pointer, got " ++ @typeName(T));

    // Do nothing if the pointer is already well-aligned.
    if (align_to <= info.pointer.alignment orelse @alignOf(info.pointer.child))
        return 0;

    // Calculate the aligned base address with an eye out for overflow.
    const addr = @intFromPtr(ptr);
    var ov = @addWithOverflow(addr, align_to - 1);
    if (ov[1] != 0) return null;
    ov[0] &= ~@as(usize, align_to - 1);

    // The delta is expressed in terms of bytes, turn it into a number of child
    // type elements.
    const delta = ov[0] - addr;
    const pointee_size = @sizeOf(info.pointer.child);
    if (delta % pointee_size != 0) return null;
    return delta / pointee_size;
}

/// Aligns a given pointer value to a specified alignment factor.
/// Returns an aligned pointer or null if one of the following conditions is
/// met:
/// - The aligned pointer would not fit the address space,
/// - The delta required to align the pointer is not a multiple of the pointee's
///   type.
pub fn alignPointer(ptr: anytype, align_to: usize) ?@TypeOf(ptr) {
    const adjust_off = alignPointerOffset(ptr, align_to) orelse return null;
    // Avoid the use of ptrFromInt to avoid losing the pointer provenance info.
    return @alignCast(ptr + adjust_off);
}

test alignPointer {
    const S = struct {
        fn checkAlign(comptime T: type, base: usize, align_to: usize, expected: usize) !void {
            const ptr: T = @ptrFromInt(base);
            const aligned = alignPointer(ptr, align_to);
            try testing.expectEqual(expected, @intFromPtr(aligned));
        }
    };

    try S.checkAlign([*]u8, 0x123, 0x200, 0x200);
    try S.checkAlign([*]align(4) u8, 0x10, 2, 0x10);
    try S.checkAlign([*]u32, 0x10, 2, 0x10);
    try S.checkAlign([*]u32, 0x4, 16, 0x10);
    // Misaligned.
    try S.checkAlign([*]align(1) u32, 0x3, 2, 0);
    // Overflow.
    try S.checkAlign([*]u32, math.maxInt(usize) - 3, 8, 0);
}

fn CopyPtrAttrs(
    comptime source: type,
    comptime size: std.builtin.Type.Pointer.Size,
    comptime child: type,
) type {
    const ptr = @typeInfo(source).pointer;
    return @Pointer(size, .{
        .@"const" = ptr.is_const,
        .@"volatile" = ptr.is_volatile,
        .@"allowzero" = ptr.is_allowzero,
        .@"align" = ptr.alignment orelse a: {
            // If the new child is aligned differently than the old one, explicitly align the type.
            const want = @alignOf(ptr.child);
            break :a if (@alignOf(child) == want) null else want;
        },
        .@"addrspace" = ptr.address_space,
    }, child, null);
}

fn AsBytesReturnType(comptime P: type) type {
    const pointer = @typeInfo(P).pointer;
    assert(pointer.size == .one);
    const size = @sizeOf(pointer.child);
    return CopyPtrAttrs(P, .one, [size]u8);
}

/// Given a pointer to a single item, returns a slice of the underlying bytes, preserving pointer attributes.
pub fn asBytes(ptr: anytype) AsBytesReturnType(@TypeOf(ptr)) {
    return @ptrCast(@alignCast(ptr));
}

test asBytes {
    const deadbeef = @as(u32, 0xDEADBEEF);
    const deadbeef_bytes = switch (native_endian) {
        .big => "\xDE\xAD\xBE\xEF",
        .little => "\xEF\xBE\xAD\xDE",
    };

    try testing.expect(eql(u8, asBytes(&deadbeef), deadbeef_bytes));

    var codeface = @as(u32, 0xC0DEFACE);
    for (asBytes(&codeface)) |*b|
        b.* = 0;
    try testing.expe
```
