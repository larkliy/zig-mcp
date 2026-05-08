```
   // `mul` contains `rem_data`,  however one more needs subtracted anyways
                        // since the next pattern is also being taken.
                        rem_splat -= mul;
                    } else {
                        // All of `data` has been consumed.
                        assert(block_limit == .nothing);
                        assert(rem_bytes == 0);
                        // Since `rem_bytes` and `block_limit` are zero, these won't be used.
                        rem_data = undefined;
                        rem_data_elem = undefined;
                        rem_splat = undefined;
                    }
                }
                if (block_limit == .nothing) break;
            }

            // Output block
            rem_splat = start_splat;
            rem_data = start_data;
            rem_data_elem = start_data_elem;
            block_limit = .limited(block_size);

            var codes_buf: CodesBuf = .init;
            if (try h.outputHeader(&freqs, &codes_buf, block_size, false)) |table| {
                while (true) {
                    const bytes = block_limit.sliceConst(rem_data_elem);
                    rem_data_elem = rem_data_elem[bytes.len..];
                    block_limit = block_limit.subtract(bytes.len).?;

                    h.hasher.update(bytes);
                    for (bytes) |b| {
                        try h.bit_writer.write(table.codes[b], table.bits[b]);
                    }

                    if (rem_data_elem.len == 0) {
                        rem_data_elem = rem_data[0];
                        if (rem_data.len != 1) {
                            rem_data = rem_data[1..];
                        } else if (rem_splat != 0) {
                            rem_splat -= 1;
                        } else {
                            // All of `data` has been consumed.
                            assert(block_limit == .nothing);
                            assert(rem_bytes == 0);
                            // Since `rem_bytes` and `block_limit` are zero, these won't be used.
                            rem_data = undefined;
                            rem_data_elem = undefined;
                            rem_splat = undefined;
                        }
                    }
                    if (block_limit == .nothing) break;
                }
                try h.bit_writer.write(table.codes[256], table.bits[256]);
            } else while (true) {
                // Store block

                // Write data that is not a full vector element
                const in_pattern = rem_splat != splat;
                const vec_elem_i, const in_data =
                    @subWithOverflow(data.len - (rem_data.len - @intFromBool(in_pattern)), 1);
                const is_elem = in_data == 0 and data[vec_elem_i].len == rem_data_elem.len;

                if (!is_elem or rem_data_elem.len > @intFromEnum(block_limit)) {
                    block_limit = block_limit.subtract(rem_data_elem.len) orelse {
                        try h.bit_writer.output.writeAll(rem_data_elem[0..@intFromEnum(block_limit)]);
                        h.hasher.update(rem_data_elem[0..@intFromEnum(block_limit)]);
                        rem_data_elem = rem_data_elem[@intFromEnum(block_limit)..];
                        assert(rem_data_elem.len != 0);
                        break;
                    };
                    try h.bit_writer.output.writeAll(rem_data_elem);
                    h.hasher.update(rem_data_elem);
                } else {
                    // Put `rem_data_elem` back in `rem_data`
                    if (!in_pattern) {
                        rem_data = data[vec_elem_i..];
                    } else {
                        rem_splat += 1;
                    }
                }
                rem_data_elem = undefined; // it is always updated below

                // Send through as much of the original vector as possible
                var vec_n: usize = 0;
                var vlimit = block_limit;
                const vec_splat = while (rem_data[vec_n..].len != 1) {
                    vlimit = vlimit.subtract(rem_data[vec_n].len) orelse break 1;
                    vec_n += 1;
                } else vec_splat: {
                    // For `pattern.len == 0`, the value of `vec_splat` does not matter.
                    const vec_splat = @intFromEnum(vlimit) / @max(1, pattern.len);
                    if (pattern.len != 0) assert(vec_splat <= rem_splat + 1);
                    vlimit = vlimit.subtract(pattern.len * vec_splat).?;
                    vec_n += 1;
                    break :vec_splat vec_splat;
                };

                const n = if (vec_n != 0) n: {
                    assert(@intFromEnum(block_limit) - @intFromEnum(vlimit) ==
                        Writer.countSplat(rem_data[0..vec_n], vec_splat));
                    break :n try h.bit_writer.output.writeSplat(rem_data[0..vec_n], vec_splat);
                } else 0; // Still go into the case below to advance the vector
                block_limit = block_limit.subtract(n).?;
                var consumed: Io.Limit = .limited(n);

                while (rem_data.len != 1) {
                    const elem = rem_data[0];
                    rem_data = rem_data[1..];
                    consumed = consumed.subtract(elem.len) orelse {
                        h.hasher.update(elem[0..@intFromEnum(consumed)]);
                        rem_data_elem = elem[@intFromEnum(consumed)..];
                        break;
                    };
                    h.hasher.update(elem);
                } else {
                    if (pattern.len == 0) {
                        // All of `data` has been consumed. However, the general
                        // case below does not work since it divides by zero.
                        assert(consumed == .nothing);
                        assert(block_limit == .nothing);
                        assert(rem_bytes == 0);
                        // Since `rem_bytes` and `block_limit` are zero, these won't be used.
                        rem_splat = undefined;
                        rem_data = undefined;
                        rem_data_elem = undefined;
                        break;
                    }

                    const splatted = @intFromEnum(consumed) / pattern.len;
                    const partial = @intFromEnum(consumed) % pattern.len;
                    for (0..splatted) |_| h.hasher.update(pattern);
                    h.hasher.update(pattern[0..partial]);

                    const taken_splat = splatted + 1;
                    if (rem_splat >= taken_splat) {
                        rem_splat -= taken_splat;
                        rem_data_elem = pattern[partial..];
                    } else {
                        // All of `data` has been consumed.
                        assert(partial == 0);
                        assert(block_limit == .nothing);
                        assert(rem_bytes == 0);
                        // Since `rem_bytes` and `block_limit` are zero, these won't be used.
                        rem_data = undefined;
                        rem_data_elem = undefined;
                        rem_splat = undefined;
                    }
                }

                if (block_limit == .nothing) break;
            }
        }

        if (rem_bytes > data_bytes) {
            assert(rem_bytes - data_bytes == rem_data_elem.len);
            assert(&rem_data_elem[0] == &w.buffer[total_bytes - rem_bytes]);
        }
        return w.consume(total_bytes - rem_bytes);
    }

    fn flush(w: *Writer) Writer.Error!void {
        errdefer w.* = .failing;
        const h: *Huffman = @fieldParentPtr("writer", w);
        try Huffman.rebaseInner(w, 0, w.buffer.len, false);
        try h.bit_writer.byteAlignBlocks();
    }

    fn finish(h: *Huffman) Writer.Error!void {
        defer h.writer = .failing;
        try Huffman.rebaseInner(&h.writer, 0, h.writer.buffer.len, true);
        try h.bit_writer.output.rebase(0, 1);
        h.bit_writer.byteAlign();
        try h.hasher.writeFooter(h.bit_writer.output);
    }

    fn rebase(w: *Writer, preserve: usize, capacity: usize) Writer.Error!void {
        errdefer w.* = .failing;
        try Huffman.rebaseInner(w, preserve, capacity, false);
    }

    fn rebaseInner(w: *Writer, preserve: usize, capacity: usize, eos: bool) Writer.Error!void {
        const h: *Huffman = @fieldParentPtr("writer", w);
        assert(preserve + capacity <= w.buffer.len);
        if (eos) assert(capacity == w.buffer.len);

        const preserved = @min(w.end, preserve);
        var remaining = w.buffer[0 .. w.end - preserved];
        while (remaining.len > max_tokens) { // not >= so there is always a block down below
            const bytes = remaining[0..max_tokens];
            remaining = remaining[max_tokens..];
            try h.outputBytes(bytes, false);
        }

        // eos check required for empty block
        if (w.buffer.len - (remaining.len + preserved) < capacity or eos) {
            const bytes = remaining;
            remaining = &.{};
            try h.outputBytes(bytes, eos);
        }

        _ = w.consume(w.end - preserved - remaining.len);
    }

    fn outputBytes(h: *Huffman, bytes: []const u8, eos: bool) Writer.Error!void {
        comptime assert(max_tokens != 65535);
        assert(bytes.len <= max_tokens);
        var freqs: [257]u16 = @splat(0);
        freqs[256] = 1;
        for (bytes) |b| freqs[b] += 1;
        h.hasher.update(bytes);

        var codes_buf: CodesBuf = .init;
        if (try h.outputHeader(&freqs, &codes_buf, @intCast(bytes.len), eos)) |table| {
            for (bytes) |b| {
                try h.bit_writer.write(table.codes[b], table.bits[b]);
            }
            try h.bit_writer.write(table.codes[256], table.bits[256]);
        } else {
            try h.bit_writer.output.writeAll(bytes);
        }
    }

    const CodesBuf = struct {
        dyn_codes: [258]u16,
        dyn_bits: [258]u4,

        pub const init: CodesBuf = .{
            .dyn_codes = @as([257]u16, undefined) ++ .{0},
            .dyn_bits = @as([257]u4, @splat(0)) ++ .{1},
        };
    };

    /// Returns null if the block is stored.
    fn outputHeader(
        h: *Huffman,
        freqs: *const [257]u16,
        buf: *CodesBuf,
        bytes: u16,
        eos: bool,
    ) Writer.Error!?struct {
        codes: *const [257]u16,
        bits: *const [257]u4,
    } {
        assert(freqs[256] == 1);
        const dyn_codes_bitsize, _ = huffman.build(
            freqs,
            buf.dyn_codes[0..257],
            buf.dyn_bits[0..257],
            15,
            true,
        );

        var clen_values: [258]u8 = undefined;
        var clen_extra: [258]u8 = undefined;
        var clen_freqs: [19]u16 = @splat(0);
        const clen_len, const clen_extra_bitsize = buildClen(
            &buf.dyn_bits,
            &clen_values,
            &clen_extra,
            &clen_freqs,
        );

        var clen_codes: [19]u16 = undefined;
        var clen_bits: [19]u4 = @splat(0);
        const clen_codes_bitsize, _ = huffman.build(
            &clen_freqs,
            &clen_codes,
            &clen_bits,
            7,
            false,
        );
        const hclen = clenHlen(clen_freqs);

        const dynamic_bitsize = @as(u32, 14) +
            (4 + @as(u6, hclen)) * 3 + clen_codes_bitsize + clen_extra_bitsize +
            dyn_codes_bitsize;
        const fixed_bitsize = n: {
            const freq7 = 1; // eos
            var freq9: u16 = 0;
            for (freqs[144..256]) |f| freq9 += f;
            const freq8: u16 = bytes - freq9;
            break :n @as(u32, freq7) * 7 + @as(u32, freq8) * 8 + @as(u32, freq9) * 9;
        };
        const stored_bitsize = n: {
            const stored_align_bits = -%(h.bit_writer.buffered_n +% 3);
            break :n stored_align_bits + @as(u32, 32) + @as(u32, bytes) * 8;
        };

        if (stored_bitsize <= @min(dynamic_bitsize, fixed_bitsize)) {
            try h.bit_writer.write(BlockHeader.int(.{ .kind = .stored, .final = eos }), 3);
            try h.bit_writer.output.rebase(0, 5);
            h.bit_writer.byteAlign();
            h.bit_writer.output.writeInt(u16, bytes, .little) catch unreachable;
            h.bit_writer.output.writeInt(u16, ~bytes, .little) catch unreachable;
            return null;
        }

        if (fixed_bitsize <= dynamic_bitsize) {
            try h.bit_writer.write(BlockHeader.int(.{ .final = eos, .kind = .fixed }), 3);
            return .{
                .codes = token.fixed_lit_codes[0..257],
                .bits = token.fixed_lit_bits[0..257],
            };
        } else {
            try h.bit_writer.write(BlockHeader.Dynamic.int(.{
                .regular = .{ .final = eos, .kind = .dynamic },
                .hlit = 0,
                .hdist = 0,
                .hclen = hclen,
            }), 17);
            try h.bit_writer.writeClen(
                hclen,
                clen_values[0..clen_len],
                clen_extra[0..clen_len],
                clen_codes,
                clen_bits,
            );
            return .{ .codes = buf.dyn_codes[0..257], .bits = buf.dyn_bits[0..257] };
        }
    }
};

test Huffman {
    const fbufs = try testingFreqBufs();
    defer std.testing.allocator.destroy(fbufs);
    try std.testing.fuzz(fbufs, testFuzzedHuffmanInput, .{});
}

fn fuzzedHuffmanDrainSpaceLimit(max_drain: usize, written: usize, eos: bool) usize {
    var block_lim = math.divCeil(usize, max_drain, Huffman.max_tokens) catch unreachable;
    block_lim = @max(block_lim, @intFromBool(eos));
    const footer_overhead = @as(u8, 8) * @intFromBool(eos);
    // 6 for a raw block header (the block header may span two bytes)
    return written + 6 * block_lim + max_drain + footer_overhead;
}

/// This function is derived from `testFuzzedRawInput` with a few changes for fuzzing `Huffman`.
fn testFuzzedHuffmanInput(fbufs: *const [2][65536]u8, smith: *std.testing.Smith) !void {
    @disableInstrumentation();
    const container = smith.value(flate.Container);
    var flate_buf: [2 * 65536]u8 = undefined;
    var flate_w: Writer = .fixed(&flate_buf);
    var expected_hash: flate.Container.Hasher = .init(container);
    var expected_size: u32 = 0;
    const max_size = 4 * @as(u32, Huffman.max_tokens);

    var h_buf: [2 * @as(usize, Huffman.max_tokens)]u8 = undefined;
    const h_buf_len = smith.valueWeighted(u32, &.{
        .value(u32, 0, @intCast(h_buf.len)), // unbuffered
        .rangeAtMost(u32, 0, @intCast(h_buf.len), 1),
    });
    var h: Huffman = try .init(&flate_w, h_buf[0..h_buf_len], container);

    var vecs: [32][]const u8 = undefined;
    var vecs_n: usize = 0;

    while (true) {
        const Op = packed struct {
            drain: bool = false,
            add_vec: bool = false,
            rebase: enum(u2) { none, rebase, flush } = .none,

            pub const drain_only: @This() = .{ .drain = true };
            pub const add_vec_only: @This() = .{ .add_vec = true };
            pub const add_vec_and_drain: @This() = .{ .add_vec = true, .drain = true };
            pub const drain_and_rebase: @This() = .{ .drain = true, .rebase = .rebase };
            pub const drain_and_flush: @This() = .{ .drain = true, .rebase = .flush };
        };

        const is_eos = expected_size == max_size or smith.eosWeightedSimple(7, 1);
        var op: Op = if (!is_eos) smith.valueWeighted(Op, &.{
            .value(Op, .add_vec_only, 5),
            .value(Op, .add_vec_and_drain, 1),
            .value(Op, .drain_and_rebase, 1),
            .value(Op, .drain_and_flush, 1),
        }) else .drain_only;

        if (op.add_vec) {
            const max_write = max_size - expected_size;
            const buffered: u32 = @intCast(h.writer.buffered().len + countVec(vecs[0..vecs_n]));
            const to_align = Huffman.max_tokens - buffered % Huffman.max_tokens;
            assert(to_align != 0); // otherwise, not helpful.

            const data_buf = &fbufs[
                smith.valueWeighted(u1, &.{
                    .value(FreqBufIndex, .gradient, 3),
                    .value(FreqBufIndex, .random, 1),
                })
            ];
            const data_buf_len: u32 = @intCast(data_buf.len);

            const max_data = @min(data_buf_len, max_write);
            const len = smith.valueWeighted(u32, &.{
                .rangeAtMost(u32, 0, max_data, 1),
                .rangeAtMost(u32, 0, @min(Huffman.max_tokens, max_data), 4),
                .value(u32, @min(to_align, max_data), max_data), // @min 2nd arg is an edge-case
            });
            const off = smith.valueRangeAtMost(u32, 0, data_buf_len - len);

            expected_size += len;
            vecs[vecs_n] = data_buf[off..][0..len];
            vecs_n += 1;
            op.drain |= vecs_n == vecs.len;
        }

        op.drain |= is_eos;
        op.drain &= vecs_n != 0;
        if (op.drain) {
            const pattern_len: u32 = @intCast(vecs[vecs_n - 1].len);
            const pattern_len_z = @max(pattern_len, 1);

            const max_write = max_size - (expected_size - pattern_len);
            const buffered: u32 = @intCast(h.writer.buffered().len + countVec(vecs[0 .. vecs_n - 1]));
            const to_align = Huffman.max_tokens - buffered % Huffman.max_tokens;
            assert(to_align != 0); // otherwise, not helpful.

            const max_splat = max_write / pattern_len_z;
            const weights: [3]std.testing.Smith.Weight = .{
                .rangeAtMost(u32, 0, max_splat, 1),
                .rangeAtMost(u32, 0, @min(
                    Huffman.max_tokens + pattern_len_z,
                    max_write,
                ) / pattern_len_z, 4),
                .value(u32, to_align / pattern_len_z, max_splat * 4),
            };
            const align_weight = to_align % pattern_len_z == 0 and to_align <= max_write;
            const n_weights = @as(u8, 2) + @intFromBool(align_weight);
            const splat = smith.valueWeighted(u32, weights[0..n_weights]);

            expected_size = expected_size - pattern_len + pattern_len * splat; // splat may be zero
            for (vecs[0 .. vecs_n - 1]) |v| expected_hash.update(v);
            for (0..splat) |_| expected_hash.update(vecs[vecs_n - 1]);

            const max_space = fuzzedHuffmanDrainSpaceLimit(
                buffered + pattern_len * splat,
                flate_w.buffered().len,
                false,
            );
            h.writer.writeSplatAll(vecs[0..vecs_n], splat) catch
                return if (max_space <= flate_w.buffer.len) error.OverheadTooLarge else {};
            if (flate_w.buffered().len > max_space) return error.OverheadTooLarge;

            vecs_n = 0;
        }

        if (op.rebase != .none) {
            const capacity = smith.valueRangeAtMost(u32, 0, h_buf_len);
            const preserve = smith.valueRangeAtMost(u32, 0, h_buf_len - capacity);

            const max_space = fuzzedHuffmanDrainSpaceLimit(
                h.writer.buffered().len,
                flate_w.buffered().len,
                false,
            ) + @as(usize, 8) * @intFromBool(op.rebase == .flush); // Overhead from byte alignment
            switch (op.rebase) {
                .none => unreachable,
                .rebase => h.writer.rebase(preserve, capacity) catch
                    return if (max_space <= flate_w.buffer.len) error.OverheadTooLarge else {},
                .flush => h.writer.flush() catch
                    return if (max_space <= flate_w.buffer.len) error.OverheadTooLarge else {},
            }
            if (flate_w.buffered().len > max_space) return error.OverheadTooLarge;
        }

        if (is_eos) break;
    }

    const max_space = fuzzedHuffmanDrainSpaceLimit(
        h.writer.buffered().len,
        flate_w.buffered().len,
        true,
    );
    h.finish() catch return if (max_space <= flate_w.buffer.len) error.OverheadTooLarge else {};
    if (flate_w.buffered().len > max_space) return error.OverheadTooLarge;

    try testingCheckDecompressedMatches(flate_w.buffered(), expected_size, expected_hash);
}



---
File: /std/compress/flate/Decompress.zig
---

const std = @import("../../std.zig");
const assert = std.debug.assert;
const flate = std.compress.flate;
const testing = std.testing;
const Writer = std.Io.Writer;
const Reader = std.Io.Reader;
const Container = flate.Container;

const Decompress = @This();
const token = @import("token.zig");

input: *Reader,
consumed_bits: u3,

reader: Reader,

container_metadata: Container.Metadata,

lit_dec: LiteralDecoder,
dst_dec: DistanceDecoder,

final_block: bool,
state: State,

err: ?Error,

const BlockType = enum(u2) {
    stored = 0,
    fixed = 1,
    dynamic = 2,
    invalid = 3,
};

const State = union(enum) {
    protocol_header,
    block_header,
    stored_block: u16,
    fixed_block,
    fixed_block_literal: u8,
    fixed_block_match: u16,
    dynamic_block,
    dynamic_block_literal: u8,
    dynamic_block_match: u16,
    protocol_footer,
    end,
};

pub const Error = Container.Error || error{
    InvalidCode,
    InvalidMatch,
    WrongStoredBlockNlen,
    InvalidBlockType,
    InvalidDynamicBlockHeader,
    ReadFailed,
    OversubscribedHuffmanTree,
    IncompleteHuffmanTree,
    MissingEndOfBlockCode,
    EndOfStream,
};

const direct_vtable: Reader.VTable = .{
    .stream = streamDirect,
    .rebase = rebaseFallible,
    .discard = discardDirect,
    .readVec = readVec,
};

const indirect_vtable: Reader.VTable = .{
    .stream = streamIndirect,
    .rebase = rebaseFallible,
    .discard = discardIndirect,
    .readVec = readVec,
};

/// `input` buffer is asserted to be at least 10 bytes, or EOF before then.
///
/// If `buffer` is provided then asserted to have `flate.max_window_len`
/// capacity.
pub fn init(input: *Reader, container: Container, buffer: []u8) Decompress {
    if (buffer.len != 0) assert(buffer.len >= flate.max_window_len);
    return .{
        .reader = .{
            .vtable = if (buffer.len == 0) &direct_vtable else &indirect_vtable,
            .buffer = buffer,
            .seek = 0,
            .end = 0,
        },
        .input = input,
        .consumed_bits = 0,
        .container_metadata = .init(container),
        .lit_dec = .{},
        .dst_dec = .{},
        .final_block = false,
        .state = .protocol_header,
        .err = null,
    };
}

fn rebaseFallible(r: *Reader, capacity: usize) Reader.RebaseError!void {
    rebase(r, capacity);
}

fn rebase(r: *Reader, capacity: usize) void {
    assert(capacity <= r.buffer.len - flate.history_len);
    assert(r.end + capacity > r.buffer.len);
    const discard_n = @min(r.seek, r.end - flate.history_len);
    const keep = r.buffer[discard_n..r.end];
    @memmove(r.buffer[0..keep.len], keep);
    r.end = keep.len;
    r.seek -= discard_n;
}

/// This could be improved so that when an amount is discarded that includes an
/// entire frame, skip decoding that frame.
fn discardDirect(r: *Reader, limit: std.Io.Limit) Reader.Error!usize {
    if (r.end + flate.history_len > r.buffer.len) rebase(r, flate.history_len);
    var writer: Writer = .{
        .vtable = &.{
            .drain = std.Io.Writer.Discarding.drain,
            .sendFile = std.Io.Writer.Discarding.sendFile,
        },
        .buffer = r.buffer,
        .end = r.end,
    };
    defer {
        assert(writer.end != 0);
        r.end = writer.end;
        r.seek = r.end;
    }
    const n = r.stream(&writer, limit) catch |err| switch (err) {
        error.WriteFailed => unreachable,
        error.ReadFailed => return error.ReadFailed,
        error.EndOfStream => return error.EndOfStream,
    };
    assert(n <= @intFromEnum(limit));
    return n;
}

fn discardIndirect(r: *Reader, limit: std.Io.Limit) Reader.Error!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    if (r.end + flate.history_len > r.buffer.len) rebase(r, flate.history_len);
    var writer: Writer = .{
        .buffer = r.buffer,
        .end = r.end,
        .vtable = &.{ .drain = Writer.unreachableDrain },
    };
    {
        defer r.end = writer.end;
        _ = streamFallible(d, &writer, .limited(writer.buffer.len - writer.end)) catch |err| switch (err) {
            error.WriteFailed => unreachable,
            else => |e| return e,
        };
    }
    const n = limit.minInt(r.end - r.seek);
    r.seek += n;
    return n;
}

fn readVec(r: *Reader, data: [][]u8) Reader.Error!usize {
    _ = data;
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    return streamIndirectInner(d);
}

fn streamIndirectInner(d: *Decompress) Reader.Error!usize {
    const r = &d.reader;
    if (r.buffer.len - r.end < flate.history_len) rebase(r, flate.history_len);
    var writer: Writer = .{
        .buffer = r.buffer,
        .end = r.end,
        .vtable = &.{
            .drain = Writer.unreachableDrain,
            .rebase = Writer.unreachableRebase,
        },
    };
    defer r.end = writer.end;
    _ = streamFallible(d, &writer, .limited(writer.buffer.len - writer.end)) catch |err| switch (err) {
        error.WriteFailed => unreachable,
        else => |e| return e,
    };
    return 0;
}

fn decodeLength(self: *Decompress, code_int: u5) !u16 {
    if (code_int > 28) return error.InvalidCode;
    const l: token.LenCode = .fromInt(code_int);
    const base = l.base();
    const extra = l.extraBits();
    return token.min_length + (base | try self.takeBits(extra));
}

fn decodeDistance(self: *Decompress, code_int: u5) !u16 {
    if (code_int > 29) return error.InvalidCode;
    const d: token.DistCode = .fromInt(code_int);
    const base = d.base();
    const extra = d.extraBits();
    return token.min_distance + (base | try self.takeBits(extra));
}

/// Decode code length symbol to code length. Writes decoded length into
/// lens slice starting at position pos. Returns number of positions
/// advanced.
fn dynamicCodeLength(self: *Decompress, code: u16, lens: []u4, pos: usize) !usize {
    if (pos >= lens.len)
        return error.InvalidDynamicBlockHeader;

    switch (code) {
        0...15 => {
            // Represent code lengths of 0 - 15
            lens[pos] = @intCast(code);
            return 1;
        },
        16 => {
            // Copy the previous code length 3 - 6 times.
            // The next 2 bits indicate repeat length
            const n: u8 = @as(u8, try self.takeIntBits(u2)) + 3;
            if (pos == 0 or pos + n > lens.len)
                return error.InvalidDynamicBlockHeader;
            for (0..n) |i| {
                lens[pos + i] = lens[pos + i - 1];
            }
            return n;
        },
        // Repeat a code length of 0 for 3 - 10 times. (3 bits of length)
        17 => return @as(u8, try self.takeIntBits(u3)) + 3,
        // Repeat a code length of 0 for 11 - 138 times (7 bits of length)
        18 => return @as(u8, try self.takeIntBits(u7)) + 11,
        else => return error.InvalidDynamicBlockHeader,
    }
}

fn decodeSymbol(self: *Decompress, decoder: anytype) !u16 {
    // Maximum code len is 15 bits.
    const sym = try decoder.find(try self.peekIntBitsShort(u15));
    try self.tossBitsShort(sym.code_bits);
    return sym.value;
}

fn streamDirect(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    return streamFallible(d, w, limit);
}

fn streamIndirect(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    _ = limit;
    _ = w;
    return streamIndirectInner(d);
}

fn streamFallible(d: *Decompress, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    return streamInner(d, w, limit) catch |err| switch (err) {
        error.EndOfStream => {
            if (d.state == .end) {
                return error.EndOfStream;
            } else {
                d.err = error.EndOfStream;
                return error.ReadFailed;
            }
        },
        error.WriteFailed => return error.WriteFailed,
        else => |e| {
            // In the event of an error, state is unmodified so that it can be
            // better used to diagnose the failure.
            d.err = e;
            return error.ReadFailed;
        },
    };
}

fn streamInner(d: *Decompress, w: *Writer, limit: std.Io.Limit) (Error || Reader.StreamError)!usize {
    var remaining = @intFromEnum(limit);
    const in = d.input;
    sw: switch (d.state) {
        .protocol_header => switch (d.container_metadata.container()) {
            .gzip => {
                const Header = extern struct {
                    magic: u16 align(1),
                    method: u8,
                    flags: packed struct(u8) {
                        text: bool,
                        hcrc: bool,
                        extra: bool,
                        name: bool,
                        comment: bool,
                        reserved: u3,
                    },
                    mtime: u32 align(1),
                    xfl: u8,
                    os: u8,
                };
                const header = try in.takeStruct(Header, .little);
                if (header.magic != 0x8b1f or header.method != 0x08)
                    return error.BadGzipHeader;
                if (header.flags.extra) {
                    const extra_len = try in.takeInt(u16, .little);
                    try in.discardAll(extra_len);
                }
                if (header.flags.name) {
                    _ = try in.discardDelimiterInclusive(0);
                }
                if (header.flags.comment) {
                    _ = try in.discardDelimiterInclusive(0);
                }
                if (header.flags.hcrc) {
                    try in.discardAll(2);
                }
                continue :sw .block_header;
            },
            .zlib => {
                const header = try in.takeArray(2);
                const cmf: packed struct(u8) { cm: u4, cinfo: u4 } = @bitCast(header[0]);
                if (cmf.cm != 8 or cmf.cinfo > 7) return error.BadZlibHeader;
                continue :sw .block_header;
            },
            .raw => continue :sw .block_header,
        },
        .block_header => {
            d.final_block = (try d.takeIntBits(u1)) != 0;
            const block_type: BlockType = @enumFromInt(try d.takeIntBits(u2));
            switch (block_type) {
                .stored => {
                    d.alignBitsForward();
                    // everything after this is byte aligned in stored block
                    const len = try in.takeInt(u16, .little);
                    const nlen = try in.takeInt(u16, .little);
                    if (len != ~nlen) return error.WrongStoredBlockNlen;
                    continue :sw .{ .stored_block = len };
                },
                .fixed => continue :sw .fixed_block,
                .dynamic => {
                    const hlit: u16 = @as(u16, try d.takeIntBits(u5)) + 257; // number of ll code entries present - 257
                    const hdist: u16 = @as(u16, try d.takeIntBits(u5)) + 1; // number of distance code entries - 1
                    const hclen: u8 = @as(u8, try d.takeIntBits(u4)) + 4; // hclen + 4 code lengths are encoded

                    if (hlit > 286 or hdist > 30)
                        return error.InvalidDynamicBlockHeader;

                    // lengths for code lengths
                    var cl_lens: [19]u4 = @splat(0);
                    for (token.codegen_order[0..hclen]) |i| {
                        cl_lens[i] = try d.takeIntBits(u3);
                    }
                    var cl_dec: CodegenDecoder = .{};
                    try cl_dec.generate(&cl_lens);

                    // decoded code lengths
                    var dec_lens: [286 + 30]u4 = @splat(0);
                    var pos: usize = 0;
                    while (pos < hlit + hdist) {
                        const peeked = try d.peekIntBitsShort(u7);
                        const sym = try cl_dec.find(peeked);
                        try d.tossBitsShort(sym.code_bits);
                        pos += try d.dynamicCodeLength(sym.value, &dec_lens, pos);
                    }
                    if (pos > hlit + hdist) {
                        return error.InvalidDynamicBlockHeader;
                    }

                    // literal code lengths to literal decoder
                    try d.lit_dec.generate(dec_lens[0..hlit]);

                    // distance code lengths to distance decoder
                    try d.dst_dec.generate(dec_lens[hlit..][0..hdist]);

                    continue :sw .dynamic_block;
                },
                .invalid => return error.InvalidBlockType,
            }
        },
        .stored_block => |remaining_len| {
            const out: []u8 = if (remaining != 0)
                try w.writableSliceGreedyPreserve(flate.history_len, 1)
            else
                &.{};
            var limited_out: [1][]u8 = .{limit.min(.limited(remaining_len)).slice(out)};
            const n = try in.readVec(&limited_out);
            if (remaining_len - n == 0) {
                d.state = if (d.final_block) .protocol_footer else .block_header;
            } else {
                d.state = .{ .stored_block = @intCast(remaining_len - n) };
            }
            w.advance(n);
            return @intFromEnum(limit) - remaining + n;
        },
        .fixed_block => while (true) {
            // Consume bytes
            const sym = try d.readFixedCode();

            if (sym >= 256) {
                @branchHint(.unlikely);

                if (sym == 256) {
                    @branchHint(.unlikely);
                    // End
                    d.state = if (d.final_block) .protocol_footer else .block_header;
                    continue :sw d.state;
                }

                // Match
                const length = try d.decodeLength(@intCast(sym - 257));
                continue :sw .{ .fixed_block_match = length };
            }

            const byte: u8 = @intCast(sym);
            if (remaining != 0) {
                @branchHint(.likely);
                remaining -= 1;
                try w.writeBytePreserve(flate.history_len, byte);
            } else {
                d.state = .{ .fixed_block_literal = byte };
                return @intFromEnum(limit) - remaining;
            }
        },
        .fixed_block_literal => |symbol| {
            assert(remaining != 0);
            remaining -= 1;
            try w.writeBytePreserve(flate.history_len, symbol);
            continue :sw .fixed_block;
        },
        .fixed_block_match => |length| {
            if (remaining >= length) {
                @branchHint(.likely);
                const distance = try d.decodeDistance(@bitReverse(try d.takeIntBits(u5)));
                try writeMatch(w, length, distance);
                remaining -= length;
                continue :sw .fixed_block;
            } else {
                d.state = .{ .fixed_block_match = length };
                return @intFromEnum(limit) - remaining;
            }
        },
        // In larger archives most blocks are usually dynamic, so
        // decompression performance depends on this logic.
        .dynamic_block => while (true) {
            // Consume bytes
            const sym = try d.decodeSymbol(&d.lit_dec);

            if (sym >= 256) {
                @branchHint(.unlikely);

                if (sym == 256) {
                    @branchHint(.unlikely);
                    // End
                    d.state = if (d.final_block) .protocol_footer else .block_header;
                    continue :sw d.state;
                }

                // Match
                const length = try d.decodeLength(@intCast(sym - 257));
                continue :sw .{ .dynamic_block_match = length };
            }

            const byte: u8 = @intCast(sym);
            if (remaining != 0) {
                @branchHint(.likely);
                remaining -= 1;
                try w.writeBytePreserve(flate.history_len, byte);
            } else {
                d.state = .{ .dynamic_block_literal = byte };
                return @intFromEnum(limit) - remaining;
            }
        },
        .dynamic_block_literal => |symbol| {
            assert(remaining != 0);
            remaining -= 1;
            try w.writeBytePreserve(flate.history_len, symbol);
            continue :sw .dynamic_block;
        },
        .dynamic_block_match => |length| {
            if (remaining >= length) {
                @branchHint(.likely);
                remaining -= length;
                const dsm = try d.decodeSymbol(&d.dst_dec);
                const distance = try d.decodeDistance(@intCast(dsm));
                try writeMatch(w, length, distance);
                continue :sw .dynamic_block;
            } else {
                d.state = .{ .dynamic_block_match = length };
                return @intFromEnum(limit) - remaining;
            }
        },
        .protocol_footer => {
            d.alignBitsForward();
            switch (d.container_metadata) {
                .gzip => |*gzip| {
                    gzip.crc = try in.takeInt(u32, .little);
                    gzip.count = try in.takeInt(u32, .little);
                },
                .zlib => |*zlib| {
                    zlib.adler = try in.takeInt(u32, .big);
                },
                .raw => {},
            }
            d.state = .end;
            return @intFromEnum(limit) - remaining;
        },
        .end => return error.EndOfStream,
    }
}

/// Write match (back-reference to the same data slice) starting at `distance`
/// back from current write position, and `length` of bytes.
fn writeMatch(w: *Writer, length: u16, distance: u16) !void {
    if (w.end < distance) return error.InvalidMatch;
    assert(length >= token.min_length);
    assert(length <= token.max_length);
    assert(distance >= token.min_distance);
    assert(distance <= token.max_distance);

    // This is not a @memmove; it intentionally repeats patterns caused by
    // iterating one byte at a time.
    const dest = try w.writableSlicePreserve(flate.history_len, length);
    const end = dest.ptr - w.buffer.ptr;
    const src = w.buffer[end - distance ..][0..length];
    if (distance >= length) {
        @memcpy(dest, src);
    } else if (distance == 1) {
        // Repeating copy of single byte
        @memset(dest, src[0]);
    } else {
        // Repeating copy of multiple bytes
        for (dest, src) |*d, s| d.* = s;
    }
}

fn peekBits(d: *Decompress, n: u4) !u16 {
    const bits = d.input.peekInt(u32, .little) catch |e| return switch (e) {
        error.ReadFailed => error.ReadFailed,
        error.EndOfStream => d.peekBitsEnding(n),
    };
    const mask = @shlExact(@as(u16, 1), n) - 1;
    return @intCast((bits >> d.consumed_bits) & mask);
}

fn peekBitsEnding(d: *Decompress, n: u4) !u16 {
    @branchHint(.unlikely);

    const left = d.input.buffered();
    if (left.len * 8 - d.consumed_bits < n) return error.EndOfStream;
    const bits = std.mem.readVarInt(u32, left, .little);
    const mask = @shlExact(@as(u16, 1), n) - 1;
    return @intCast((bits >> d.consumed_bits) & mask);
}

/// Safe only after `peekBits` has been called with a greater or equal `n` value.
fn tossBits(d: *Decompress, n: u4) void {
    d.input.toss((@as(u8, n) + d.consumed_bits) / 8);
    d.consumed_bits +%= @truncate(n);
}

fn takeBits(d: *Decompress, n: u4) !u16 {
    const bits = try d.peekBits(n);
    d.tossBits(n);
    return bits;
}

fn alignBitsForward(d: *Decompress) void {
    d.input.toss(@intFromBool(d.consumed_bits != 0));
    d.consumed_bits = 0;
}

fn peekBitsShort(d: *Decompress, n: u4) !u16 {
    const bits = d.input.peekInt(u32, .little) catch |e| return switch (e) {
        error.ReadFailed => error.ReadFailed,
        error.EndOfStream => d.peekBitsShortEnding(n),
    };
    const mask = @shlExact(@as(u16, 1), n) - 1;
    return @intCast((bits >> d.consumed_bits) & mask);
}

fn peekBitsShortEnding(d: *Decompress, n: u4) !u16 {
    @branchHint(.unlikely);

    const left = d.input.buffered();
    const bits = std.mem.readVarInt(u32, left, .little);
    const mask = @shlExact(@as(u16, 1), n) - 1;
    return @intCast((bits >> d.consumed_bits) & mask);
}

fn tossBitsShort(d: *Decompress, n: u4) !void {
    if (d.input.bufferedLen() * 8 + d.consumed_bits < n) return error.EndOfStream;
    d.tossBits(n);
}

fn takeIntBits(d: *Decompress, T: type) !T {
    return @intCast(try d.takeBits(@bitSizeOf(T)));
}

fn peekIntBitsShort(d: *Decompress, T: type) !T {
    return @intCast(try d.peekBitsShort(@bitSizeOf(T)));
}

/// Reads first 7 bits, and then maybe 1 or 2 more to get full 7,8 or 9 bit code.
/// ref: https://datatracker.ietf.org/doc/html/rfc1951#page-12
///         Lit Value    Bits        Codes
///          ---------    ----        -----
///            0 - 143     8          00110000 through
///                                   10111111
///          144 - 255     9          110010000 through
///                                   111111111
///          256 - 279     7          0000000 through
///                                   0010111
///          280 - 287     8          11000000 through
///                                   11000111
fn readFixedCode(d: *Decompress) !u16 {
    const code7 = @bitReverse(try d.takeIntBits(u7));
    return switch (code7) {
        0...0b0010_111 => @as(u16, code7) + 256,
        0b0010_111 + 1...0b1011_111 => (@as(u16, code7) << 1) + @as(u16, try d.takeIntBits(u1)) - 0b0011_0000,
        0b1011_111 + 1...0b1100_011 => (@as(u16, code7 - 0b1100000) << 1) + try d.takeIntBits(u1) + 280,
        else => (@as(u16, code7 - 0b1100_100) << 2) + @as(u16, @bitReverse(try d.takeIntBits(u2))) + 144,
    };
}

pub const Symbol = packed struct(u16) {
    value: u12 = 0,
    code_bits: u4 = 0, // number of bits in code 0-15
};

pub const LiteralDecoder = HuffmanDecoder(286, 15, 9);
pub const DistanceDecoder = HuffmanDecoder(30, 15, 9);
pub const CodegenDecoder = HuffmanDecoder(19, 7, 7);

/// Creates huffman tree codes from list of code lengths (in `build`).
///
/// `find` then finds symbol for code bits. Code can be any length between 1 and
/// 15 bits. When calling `find` we don't know how many bits will be used to
/// find symbol. When symbol is returned it has code_bits field which defines
/// how much we should advance in bit stream.
///
/// Lookup table is used to map 15 bit int to symbol. Same symbol is written
/// many times in this table; 32K places for 286 (at most) symbols.
/// Small lookup table is optimization for faster search.
/// It is variation of the algorithm explained in [zlib](https://github.com/madler/zlib/blob/643e17b7498d12ab8d15565662880579692f769d/doc/algorithm.txt#L92)
/// with difference that we here use statically allocated arrays.
fn HuffmanDecoder(
    comptime alphabet_size: u16,
    comptime max_code_bits: u4,
    comptime lookup_bits: u4,
) type {
    const lookup_shift = max_code_bits - lookup_bits;
    const lookup_mask = (1 << lookup_bits) - 1;

    return struct {
        // lookup table code -> symbol
        // for values with code_bits == 0, symbol is the index of the first node in linked
        // if the index of the first node is 0xfff, it is an invalid code
        lookup: [1 << lookup_bits]Symbol = undefined,
        linked: if (lookup_bits == max_code_bits) void else [alphabet_size]struct {
            // sym.value is the next index in linked where the current index ends the chain
            // the actual symbol is this nodes's index
            sym: Symbol,
            code: u16,
        } = undefined,

        const Self = @This();

        fn reverseIdx(idx: usize) u16 {
            return @bitReverse(@as(@Int(.unsigned, lookup_bits), @intCast(idx)));
        }

        /// Generates symbols and lookup tables from list of code lens for each symbol.
        pub fn generate(self: *Self, lens: []const u4) !void {
            try checkCompleteness(lens);

            var buckets: [1 + @as(usize, max_code_bits)][alphabet_size]Symbol = undefined;
            var bucket_len: [buckets.len]u16 = @splat(0);
            for (0.., lens) |symbol, bits| {
                buckets[bits][bucket_len[bits]] = .{
                    .value = @intCast(symbol),
                    .code_bits = bits,
                };
                bucket_len[bits] += 1;
            }

            var code: u16 = 0;
            var idx: u16 = 0;
            for (1..lookup_bits + 1) |bits| {
                const inc = @as(u16, 1) << @intCast(max_code_bits - bits);
                for (buckets[bits][0..bucket_len[bits]]) |lookup_sym| {
                    const next_code = code + inc;
                    const next_idx = next_code >> lookup_shift;
                    for (idx..next_idx) |i| {
                        self.lookup[reverseIdx(i)] = lookup_sym;
                    }
                    code = next_code;
                    idx = next_idx;
                }
            }
            for (lookup_bits + 1..buckets.len) |bits| {
                const inc = @as(u16, 1) << @intCast(max_code_bits - bits);
                for (buckets[bits][0..bucket_len[bits]]) |linked_sym| {
                    const next_code = code + inc;
                    const next_idx = next_code >> lookup_shift;

                    const ri = reverseIdx(idx);
                    const next: Symbol = .{
                        .value = self.lookup[ri].value,
                        .code_bits = linked_sym.code_bits,
                    };
                    self.linked[linked_sym.value] = .{
                        .sym = next,
                        .code = @bitReverse(@as(@Int(.unsigned, max_code_bits), @intCast(code))),
                    };
                    self.lookup[ri] = .{ .value = linked_sym.value, .code_bits = 0 };

                    code = next_code;
                    idx = next_idx;
                }
            }

            // Invalid codes
            for (idx..self.lookup.len) |i| {
                self.lookup[reverseIdx(i)] = .{ .value = 0xfff, .code_bits = 0 };
            }
        }

        /// Given the list of code lengths check that it represents a canonical
        /// Huffman code for n symbols.
        ///
        /// Reference: https://github.com/madler/zlib/blob/5c42a230b7b468dff011f444161c0145b5efae59/contrib/puff/puff.c#L340
        fn checkCompleteness(lens: []const u4) !void {
            if (alphabet_size == 286)
                if (lens[256] == 0) return error.MissingEndOfBlockCode;

            var count = [_]u16{0} ** (@as(usize, max_code_bits) + 1);
            var max: usize = 0;
            for (lens) |n| {
                if (n == 0) continue;
                if (n > max) max = n;
                count[n] += 1;
            }
            if (max == 0) // empty tree
                return;

            // check for an over-subscribed or incomplete set of lengths
            var left: usize = 1; // one possible code of zero length
            for (1..count.len) |len| {
                left <<= 1; // one more bit, double codes left
                if (count[len] > left)
                    return error.OversubscribedHuffmanTree;
                left -= count[len]; // deduct count from possible codes
            }
            if (left > 0) { // left > 0 means incomplete
                // incomplete code ok only for single length 1 code
                if (max_code_bits > 7 and max == count[0] + count[1]) return;
                return error.IncompleteHuffmanTree;
            }
        }

        /// Finds symbol for lookup table code.
        pub fn find(self: *Self, code: u16) !Symbol {
            // try to find in lookup table
            const idx = code & lookup_mask;
            const sym = self.lookup[idx];
            if (sym.code_bits != 0) return sym;
            // if not use linked list of symbols with same prefix
            return self.findLinked(code, sym.value);
        }

        fn findLinked(self: *Self, code: u16, start: u16) !Symbol {
            if (start == 0xfff) return error.InvalidCode;
            if (lookup_bits == max_code_bits) unreachable;
            var pos = start;
            while (true) {
                const node = self.linked[pos];
                const shift = -%node.sym.code_bits;
                // compare code_bits number of upper bits
                if ((code ^ node.code) << shift == 0)
                    return .{ .value = @intCast(pos), .code_bits = node.sym.code_bits };
                pos = node.sym.value;
            }
        }
    };
}

test "init/find" {
    // example data from: https://youtu.be/SJPvNi4HrWQ?t=8423
    const code_lens = [_]u4{ 4, 3, 0, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 3, 2 };
    var h: CodegenDecoder = .{};
    try h.generate(&code_lens);

    // All possible codes for each symbol.
    // Lookup table has 126 elements, to cover all possible 7 bit codes.
    for (0b0000_000..0b0100_000) |c| // 0..32 (32)
        try testing.expectEqual(
            Symbol{ .value = 3, .code_bits = 2 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );

    for (0b0100_000..0b1000_000) |c| // 32..64 (32)
        try testing.expectEqual(
            Symbol{ .value = 18, .code_bits = 2 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );

    for (0b1000_000..0b1010_000) |c| // 64..80 (16)
        try testing.expectEqual(
            Symbol{ .value = 1, .code_bits = 3 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );

    for (0b1010_000..0b1100_000) |c| // 80..96 (16)
        try testing.expectEqual(
            Symbol{ .value = 4, .code_bits = 3 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );

    for (0b1100_000..0b1110_000) |c| // 96..112 (16)
        try testing.expectEqual(
            Symbol{ .value = 17, .code_bits = 3 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );

    for (0b1110_000..0b1111_000) |c| // 112..120 (8)
        try testing.expectEqual(
            Symbol{ .value = 0, .code_bits = 4 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );

    for (0b1111_000..0b1_0000_000) |c| // 120...128 (8)
        try testing.expectEqual(
            Symbol{ .value = 16, .code_bits = 4 },
            try h.find(@bitReverse(@as(u7, @intCast(c)))),
        );
}

test "encode/decode literals" {
    // Check that the example in RFC 1951 section 3.2.2 works (plus some zeroes)
    const max_bits = 5;
    var decoder: HuffmanDecoder(16, max_bits, 3) = .{};
    try decoder.generate(&.{ 3, 3, 3, 3, 0, 0, 3, 2, 4, 4 });

    inline for (0.., .{
        @as(u3, 0b010),
        @as(u3, 0b011),
        @as(u3, 0b100),
        @as(u3, 0b101),
        @as(u0, 0),
        @as(u0, 0),
        @as(u3, 0b110),
        @as(u2, 0b00),
        @as(u4, 0b1110),
        @as(u4, 0b1111),
    }) |i, code| {
        const bits = @bitSizeOf(@TypeOf(code));
        if (bits == 0) continue;
        for (0..1 << (max_bits - bits)) |extra| {
            const full = (@as(u16, code) << (max_bits - bits)) | @as(u16, @intCast(extra));
            const symbol = try decoder.find(@bitReverse(@as(u5, @intCast(full))));
            try testing.expectEqual(i, symbol.value);
            try testing.expectEqual(bits, symbol.code_bits);
        }
    }
}

test "non compressed block (type 0)" {
    try testDecompress(.raw, &[_]u8{
        0b0000_0001, 0b0000_1100, 0x00, 0b1111_0011, 0xff, // deflate fixed buffer header len, nlen
        'H', 'e', 'l', 'l', 'o', ' ', 'w', 'o', 'r', 'l', 'd', 0x0a, // non compressed data
    }, "Hello world\n");
}

test "fixed code block (type 1)" {
    try testDecompress(.raw, &[_]u8{
        0xf3, 0x48, 0xcd, 0xc9, 0xc9, 0x57, 0x28, 0xcf, // deflate data block type 1
        0x2f, 0xca, 0x49, 0xe1, 0x02, 0x00,
    }, "Hello world\n");
}

test "dynamic block (type 2)" {
    try testDecompress(.raw, &[_]u8{
        0x3d, 0xc6, 0x39, 0x11, 0x00, 0x00, 0x0c, 0x02, // deflate data block type 2
        0x30, 0x2b, 0xb5, 0x52, 0x1e, 0xff, 0x96, 0x38,
        0x16, 0x96, 0x5c, 0x1e, 0x94, 0xcb, 0x6d, 0x01,
    }, "ABCDEABCD ABCDEABCD");
}

test "gzip non compressed block (type 0)" {
    try testDecompress(.gzip, &[_]u8{
        0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, // gzip header (10 bytes)
        0b0000_0001, 0b0000_1100, 0x00, 0b1111_0011, 0xff, // deflate fixed buffer header len, nlen
        'H', 'e', 'l', 'l', 'o', ' ', 'w', 'o', 'r', 'l', 'd', 0x0a, // non compressed data
        0xd5, 0xe0, 0x39, 0xb7, // gzip footer: checksum
        0x0c, 0x00, 0x00, 0x00, // gzip footer: size
    }, "Hello world\n");
}

test "gzip fixed code block (type 1)" {
    try testDecompress(.gzip, &[_]u8{
        0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x03, // gzip header (10 bytes)
        0xf3, 0x48, 0xcd, 0xc9, 0xc9, 0x57, 0x28, 0xcf, // deflate data block type 1
        0x2f, 0xca, 0x49, 0xe1, 0x02, 0x00,
        0xd5, 0xe0, 0x39, 0xb7, 0x0c, 0x00, 0x00, 0x00, // gzip footer (chksum, len)
    }, "Hello world\n");
}

test "gzip dynamic block (type 2)" {
    try testDecompress(.gzip, &[_]u8{
        0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, // gzip header (10 bytes)
        0x3d, 0xc6, 0x39, 0x11, 0x00, 0x00, 0x0c, 0x02, // deflate data block type 2
        0x30, 0x2b, 0xb5, 0x52, 0x1e, 0xff, 0x96, 0x38,
        0x16, 0x96, 0x5c, 0x1e, 0x94, 0xcb, 0x6d, 0x01,
        0x17, 0x1c, 0x39, 0xb4, 0x13, 0x00, 0x00, 0x00, // gzip footer (chksum, len)
    }, "ABCDEABCD ABCDEABCD");
}

test "gzip header with name" {
    try testDecompress(.gzip, &[_]u8{
        0x1f, 0x8b, 0x08, 0x08, 0xe5, 0x70, 0xb1, 0x65, 0x00, 0x03, 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x2e,
        0x74, 0x78, 0x74, 0x00, 0xf3, 0x48, 0xcd, 0xc9, 0xc9, 0x57, 0x28, 0xcf, 0x2f, 0xca, 0x49, 0xe1,
        0x02, 0x00, 0xd5, 0xe0, 0x39, 0xb7, 0x0c, 0x00, 0x00, 0x00,
    }, "Hello world\n");
}

test "zlib decompress non compressed block (type 0)" {
    try testDecompress(.zlib, &[_]u8{
        0x78, 0b10_0_11100, // zlib header (2 bytes)
        0b0000_0001, 0b0000_1100, 0x00, 0b1111_0011, 0xff, // deflate fixed buffer header len, nlen
        'H', 'e', 'l', 'l', 'o', ' ', 'w', 'o', 'r', 'l', 'd', 0x0a, // non compressed data
        0x1c, 0xf2, 0x04, 0x47, // zlib footer: checksum
    }, "Hello world\n");
}

test "failing end-of-stream" {
    try testFailure(.raw, @embedFile("testdata/fuzz/end-of-stream.input"), error.EndOfStream);
}
test "failing invalid-distance" {
    try testFailure(.raw, @embedFile("testdata/fuzz/invalid-distance.input"), error.InvalidMatch);
}
test "failing invalid-tree01" {
    try testFailure(.raw, @embedFile("testdata/fuzz/invalid-tree01.input"), error.IncompleteHuffmanTree);
}
test "failing invalid-tree02" {
    try testFailure(.raw, @embedFile("testdata/fuzz/invalid-tree02.input"), error.IncompleteHuffmanTree);
}
test "failing invalid-tree03" {
    try testFailure(.raw, @embedFile("testdata/fuzz/invalid-tree03.input"), error.IncompleteHuffmanTree);
}
test "failing lengths-overflow" {
    try testFailure(.raw, @embedFile("testdata/fuzz/lengths-overflow.input"), error.InvalidDynamicBlockHeader);
}
test "failing out-of-codes" {
    try testFailure(.raw, @embedFile("testdata/fuzz/out-of-codes.input"), error.InvalidCode);
}
test "failing puff01" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff01.input"), error.WrongStoredBlockNlen);
}
test "failing puff02" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff02.input"), error.EndOfStream);
}
test "failing puff04" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff04.input"), error.InvalidCode);
}
test "failing puff05" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff05.input"), error.EndOfStream);
}
test "failing puff06" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff06.input"), error.EndOfStream);
}
test "failing puff08" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff08.input"), error.InvalidCode);
}
test "failing puff10" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff10.input"), error.InvalidCode);
}
test "failing puff11" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff11.input"), error.InvalidMatch);
}
test "failing puff12" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff12.input"), error.InvalidDynamicBlockHeader);
}
test "failing puff13" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff13.input"), error.IncompleteHuffmanTree);
}
test "failing puff14" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff14.input"), error.EndOfStream);
}
test "failing puff15" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff15.input"), error.IncompleteHuffmanTree);
}
test "failing puff16" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff16.input"), error.InvalidDynamicBlockHeader);
}
test "failing puff17" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff17.input"), error.MissingEndOfBlockCode);
}
test "failing fuzz1" {
    try testFailure(.raw, @embedFile("testdata/fuzz/fuzz1.input"), error.InvalidDynamicBlockHeader);
}
test "failing fuzz2" {
    try testFailure(.raw, @embedFile("testdata/fuzz/fuzz2.input"), error.InvalidDynamicBlockHeader);
}
test "failing fuzz3" {
    try testFailure(.raw, @embedFile("testdata/fuzz/fuzz3.input"), error.InvalidMatch);
}
test "failing fuzz4" {
    try testFailure(.raw, @embedFile("testdata/fuzz/fuzz4.input"), error.OversubscribedHuffmanTree);
}
test "failing puff18" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff18.input"), error.OversubscribedHuffmanTree);
}
test "failing puff19" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff19.input"), error.OversubscribedHuffmanTree);
}
test "failing puff20" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff20.input"), error.OversubscribedHuffmanTree);
}
test "failing puff21" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff21.input"), error.OversubscribedHuffmanTree);
}
test "failing puff22" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff22.input"), error.OversubscribedHuffmanTree);
}
test "failing puff23" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff23.input"), error.OversubscribedHuffmanTree);
}
test "failing puff24" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff24.input"), error.IncompleteHuffmanTree);
}
test "failing puff25" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff25.input"), error.OversubscribedHuffmanTree);
}
test "failing puff26" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff26.input"), error.InvalidDynamicBlockHeader);
}
test "failing puff27" {
    try testFailure(.raw, @embedFile("testdata/fuzz/puff27.input"), error.InvalidDynamicBlockHeader);
}

test "deflate-stream" {
    try testDecompress(
        .raw,
        @embedFile("testdata/fuzz/deflate-stream.input"),
        @embedFile("testdata/fuzz/deflate-stream.expect"),
    );
}

test "empty-distance-alphabet01" {
    try testDecompress(.raw, @embedFile("testdata/fuzz/empty-distance-alphabet01.input"), "");
}

test "empty-distance-alphabet02" {
    try testDecompress(.raw, @embedFile("testdata/fuzz/empty-distance-alphabet02.input"), "");
}

test "puff03" {
    try testDecompress(.raw, @embedFile("testdata/fuzz/puff03.input"), &.{0xa});
}

test "puff09" {
    try testDecompress(.raw, @embedFile("testdata/fuzz/puff09.input"), "P");
}

test "invalid block type" {
    try testFailure(.raw, &[_]u8{0b110}, error.InvalidBlockType);
}

test "bug 18966" {
    try testDecompress(
        .gzip,
        @embedFile("testdata/fuzz/bug_18966.input"),
        @embedFile("testdata/fuzz/bug_18966.expect"),
    );
}

test "reading into empty buffer" {
    // Inspired by https://github.com/ziglang/zig/issues/19895
    const input = &[_]u8{
        0b0000_0001, 0b0000_1100, 0x00, 0b1111_0011, 0xff, // deflate fixed buffer header len, nlen
        'H', 'e', 'l', 'l', 'o', ' ', 'w', 'o', 'r', 'l', 'd', 0x0a, // non compressed data
    };
    var in: Reader = .fixed(input);
    var decomp: Decompress = .init(&in, .raw, &.{});
    const r = &decomp.reader;
    var bufs: [1][]u8 = .{&.{}};
    try testing.expectEqual(0, try r.readVec(&bufs));
}

test "zlib header" {
    // Truncated header
    try testFailure(.zlib, &[_]u8{0x78}, error.EndOfStream);

    // Wrong CM
    try testFailure(.zlib, &[_]u8{ 0x79, 0x94 }, error.BadZlibHeader);

    // Wrong CINFO
    try testFailure(.zlib, &[_]u8{ 0x88, 0x98 }, error.BadZlibHeader);

    // Truncated checksum
    try testFailure(.zlib, &[_]u8{ 0x78, 0xda, 0x03, 0x00, 0x00 }, error.EndOfStream);
}

test "gzip header" {
    // Truncated header
    try testFailure(.gzip, &[_]u8{ 0x1f, 0x8B }, error.EndOfStream);

    // Wrong CM
    try testFailure(.gzip, &[_]u8{
        0x1f, 0x8b, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x03,
    }, error.BadGzipHeader);

    // Truncated checksum
    try testFailure(.gzip, &[_]u8{
        0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x03, 0x03, 0x00, 0x00, 0x00, 0x00,
    }, error.EndOfStream);

    // Truncated initial size field
    try testFailure(.gzip, &[_]u8{
        0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00,
    }, error.EndOfStream);

    try testDecompress(.gzip, &[_]u8{
        // GZIP header
        0x1f, 0x8b, 0x08, 0x12, 0x00, 0x09, 0x6e, 0x88, 0x00, 0xff, 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x00,
        // header.FHCRC (should cover entire header)
        0x99, 0xd6,
        // GZIP data
        0x01, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    }, "");
}

test "zlib should not overshoot" {
    // Compressed zlib data with extra 4 bytes at the end.
    const data = [_]u8{
        0x78, 0x9c, 0x73, 0xce, 0x2f, 0xa8, 0x2c, 0xca, 0x4c, 0xcf, 0x28, 0x51, 0x08, 0xcf, 0xcc, 0xc9,
        0x49, 0xcd, 0x55, 0x28, 0x4b, 0xcc, 0x53, 0x08, 0x4e, 0xce, 0x48, 0xcc, 0xcc, 0xd6, 0x51, 0x08,
        0xce, 0xcc, 0x4b, 0x4f, 0x2c, 0xc8, 0x2f, 0x4a, 0x55, 0x30, 0xb4, 0xb4, 0x34, 0xd5, 0xb5, 0x34,
        0x03, 0x00, 0x8b, 0x61, 0x0f, 0xa4, 0x52, 0x5a, 0x94, 0x12,
    };

    var reader: std.Io.Reader = .fixed(&data);

    var decompress_buffer: [flate.max_window_len]u8 = undefined;
    var decompress: Decompress = .init(&reader, .zlib, &decompress_buffer);
    var out: [128]u8 = undefined;

    {
        const n = try decompress.reader.readSliceShort(&out);
        try std.testing.expectEqual(46, n);
        try std.testing.expectEqualStrings("Copyright Willem van Schaik, Singapore 1995-96", out[0..n]);
    }

    // 4 bytes after compressed chunk are available in reader.
    const n = try reader.readSliceShort(&out);
    try std.testing.expectEqual(n, 4);
    try std.testing.expectEqualSlices(u8, data[data.len - 4 .. data.len], out[0..n]);
}

fn testFailure(container: Container, in: []const u8, expected_err: anyerror) !void {
    var reader: Reader = .fixed(in);
    var aw: Writer.Allocating = .init(testing.allocator);
    defer aw.deinit();

    var decompress: Decompress = .init(&reader, container, &.{});
    try testing.expectError(error.ReadFailed, decompress.reader.streamRemaining(&aw.writer));
    try testing.expectEqual(expected_err, decompress.err orelse return error.TestFailed);
}

fn testDecompress(container: Container, compressed: []const u8, expected_plain: []const u8) !void {
    var in: std.Io.Reader = .fixed(compressed);
    var aw: std.Io.Writer.Allocating = .init(testing.allocator);
    defer aw.deinit();

    var decompress: Decompress = .init(&in, container, &.{});
    const decompressed_len = try decompress.reader.streamRemaining(&aw.writer);
    try testing.expectEqual(expected_plain.len, decompressed_len);
    try testing.expectEqualSlices(u8, expected_plain, aw.written());
}



---
File: /std/compress/flate/token.zig
---

const std = @import("std");
const builtin = @import("builtin");

pub const min_length = 3;
pub const max_length = 258;

pub const min_distance = 1;
pub const max_distance = std.compress.flate.history_len;

pub const codegen_order: [19]u8 = .{
    16, 17, 18,
    0, 8, //
    7, 9,
    6, 10,
    5, 11,
    4, 12,
    3, 13,
    2, 14,
    1, 15,
};

pub const fixed_lit_codes = fixed_lit[0];
pub const fixed_lit_bits = fixed_lit[1];
const fixed_lit = blk: {
    var codes: [286]u16 = undefined;
    var bits: [286]u4 = undefined;

    for (0..143 + 1, 0b00110000..0b10111111 + 1) |i, v| {
        codes[i] = @bitReverse(@as(u8, v));
        bits[i] = 8;
    }
    for (144..255 + 1, 0b110010000..0b111111111 + 1) |i, v| {
        codes[i] = @bitReverse(@as(u9, v));
        bits[i] = 9;
    }
    for (256..279 + 1, 0b0000000..0b0010111 + 1) |i, v| {
        codes[i] = @bitReverse(@as(u7, v));
        bits[i] = 7;
    }
    for (280..287 - 2 + 1, 0b11000000..0b11000111 - 2 + 1) |i, v| {
        codes[i] = @bitReverse(@as(u8, v));
        bits[i] = 8;
    }
    break :blk .{ codes, bits };
};

pub const fixed_dist_codes = fixed_dist[0];
pub const fixed_dist_bits = fixed_dist[1];
const fixed_dist = blk: {
    var codes: [30]u16 = undefined;
    const bits: [30]u4 = @splat(5);

    for (0..30) |i| {
        codes[i] = @bitReverse(@as(u5, i));
    }
    break :blk .{ codes, bits };
};

// All paramters of codes can be derived matchematically, however some are faster to
// do via lookup table. For ReleaseSmall, we do all mathematically to save space.
pub const LenCode = if (builtin.mode != .ReleaseSmall) LookupLenCode else ShortLenCode;
pub const DistCode = if (builtin.mode != .ReleaseSmall) LookupDistCode else ShortDistCode;
const ShortLenCode = ShortCode(u8, u2, u3, true);
const ShortDistCode = ShortCode(u15, u1, u4, false);
/// For length and distance codes, they having this format.
///
/// For example, length code 0b1101 (13 or literal 270) has high_bits=0b01 and high_log2=3
/// and is 1_01_xx (2 extra bits). It is then offsetted by the min length of 3.
///        ^ bit 4 = 2 + high_log2 - 1
///
/// An exception is Length codes, where value 255 is assigned the special zero-bit code 28 or
/// literal 285.
fn ShortCode(Value: type, HighBits: type, HighLog2: type, len_special: bool) type {
    return packed struct(u5) {
        /// Bits preceding high bit or start if none
        high_bits: HighBits,
        /// High bit, 0 means none, otherwise it is at bit `x + high_log2 - 1`
        high_log2: HighLog2,

        pub fn fromVal(v: Value) @This() {
            if (len_special and v == 255) return .fromInt(28);
            const high_bits = @bitSizeOf(HighBits) + 1;
            const bits = @bitSizeOf(Value) - @clz(v);
            if (bits <= high_bits) return @bitCast(@as(u5, @intCast(v)));
            const high = v >> @intCast(bits - high_bits);
            return .{ .high_bits = @truncate(high), .high_log2 = @intCast(bits - high_bits + 1) };
        }

        /// `@ctz(return) >= extraBits()`
        pub fn base(c: @This()) Value {
            if (len_special and c.toInt() == 28) return 255;
            if (c.high_log2 <= 1) return @as(u5, @bitCast(c));
            const high_value = (@as(Value, @intFromBool(c.high_log2 != 0)) << @bitSizeOf(HighBits)) | c.high_bits;
            const high_start = @as(std.math.Log2Int(Value), c.high_log2 - 1);
            return @shlExact(high_value, high_start);
        }

        const max_extra = @bitSizeOf(Value) - (1 + @bitSizeOf(HighLog2));
        pub fn extraBits(c: @This()) std.math.IntFittingRange(0, max_extra) {
            if (len_special and c.toInt() == 28) return 0;
            return @intCast(c.high_log2 -| 1);
        }

        pub fn toInt(c: @This()) u5 {
            return @bitCast(c);
        }

        pub fn fromInt(x: u5) @This() {
            return @bitCast(x);
        }
    };
}

const LookupLenCode = packed struct(u5) {
    code: ShortLenCode,

    const code_table = table: {
        var codes: [256]ShortLenCode = undefined;
        for (0.., &codes) |v, *c| {
            c.* = .fromVal(v);
        }
        break :table codes;
    };

    const base_table = table: {
        var bases: [29]u8 = undefined;
        for (0.., &bases) |c, *b| {
            b.* = ShortLenCode.fromInt(c).base();
        }
        break :table bases;
    };

    pub fn fromVal(v: u8) LookupLenCode {
        return .{ .code = code_table[v] };
    }

    /// `@ctz(return) >= extraBits()`
    pub fn base(c: LookupLenCode) u8 {
        return base_table[c.toInt()];
    }

    pub fn extraBits(c: LookupLenCode) u3 {
        return c.code.extraBits();
    }

    pub fn toInt(c: LookupLenCode) u5 {
        return @bitCast(c);
    }

    pub fn fromInt(x: u5) LookupLenCode {
        return @bitCast(x);
    }
};

const LookupDistCode = packed struct(u5) {
    code: ShortDistCode,

    const base_table = table: {
        var bases: [30]u15 = undefined;
        for (0.., &bases) |c, *b| {
            b.* = ShortDistCode.fromInt(c).base();
        }
        break :table bases;
    };

    pub fn fromVal(v: u15) LookupDistCode {
        return .{ .code = .fromVal(v) };
    }

    /// `@ctz(return) >= extraBits()`
    pub fn base(c: LookupDistCode) u15 {
        return base_table[c.toInt()];
    }

    pub fn extraBits(c: LookupDistCode) u4 {
        return c.code.extraBits();
    }

    pub fn toInt(c: LookupDistCode) u5 {
        return @bitCast(c);
    }

    pub fn fromInt(x: u5) LookupDistCode {
        return @bitCast(x);
    }
};

test LenCode {
    inline for ([_]type{ ShortLenCode, LookupLenCode }) |Code| {
        // Check against the RFC 1951 table
        for (0.., [_]struct {
            base: u8,
            extra_bits: u4,
        }{
            // zig fmt: off
            .{ .base = 3   - min_length, .extra_bits = 0 },
            .{ .base = 4   - min_length, .extra_bits = 0 },
            .{ .base = 5   - min_length, .extra_bits = 0 },
            .{ .base = 6   - min_length, .extra_bits = 0 },
            .{ .base = 7   - min_length, .extra_bits = 0 },
            .{ .base = 8   - min_length, .extra_bits = 0 },
            .{ .base = 9   - min_length, .extra_bits = 0 },
            .{ .base = 10  - min_length, .extra_bits = 0 },
            .{ .base = 11  - min_length, .extra_bits = 1 },
            .{ .base = 13  - min_length, .extra_bits = 1 },
            .{ .base = 15  - min_length, .extra_bits = 1 },
            .{ .base = 17  - min_length, .extra_bits = 1 },
            .{ .base = 19  - min_length, .extra_bits = 2 },
            .{ .base = 23  - min_length, .extra_bits = 2 },
            .{ .base = 27  - min_length, .extra_bits = 2 },
            .{ .base = 31  - min_length, .extra_bits = 2 },
            .{ .base = 35  - min_length, .extra_bits = 3 },
            .{ .base = 43  - min_length, .extra_bits = 3 },
            .{ .base = 51  - min_length, .extra_bits = 3 },
            .{ .base = 59  - min_length, .extra_bits = 3 },
            .{ .base = 67  - min_length, .extra_bits = 4 },
            .{ .base = 83  - min_length, .extra_bits = 4 },
            .{ .base = 99  - min_length, .extra_bits = 4 },
            .{ .base = 115 - min_length, .extra_bits = 4 },
            .{ .base = 131 - min_length, .extra_bits = 5 },
            .{ .base = 163 - min_length, .extra_bits = 5 },
            .{ .base = 195 - min_length, .extra_bits = 5 },
            .{ .base = 227 - min_length, .extra_bits = 5 },
            .{ .base = 258 - min_length, .extra_bits = 0 },
        }) |code, params| {
            // zig fmt: on
            const c: u5 = @intCast(code);
            try std.testing.expectEqual(params.extra_bits, Code.extraBits(.fromInt(@intCast(c))));
            try std.testing.expectEqual(params.base, Code.base(.fromInt(@intCast(c))));
            for (params.base..params.base + @shlExact(@as(u16, 1), params.extra_bits) -
                @intFromBool(c == 27)) |v|
            {
                try std.testing.expectEqual(c, Code.fromVal(@intCast(v)).toInt());
            }
        }
    }
}

test DistCode {
    inline for ([_]type{ ShortDistCode, LookupDistCode }) |Code| {
        for (0.., [_]struct {
            base: u15,
            extra_bits: u4,
        }{
            // zig fmt: off
            .{ .base = 1     - min_distance, .extra_bits =  0 },
            .{ .base = 2     - min_distance, .extra_bits =  0 },
            .{ .base = 3     - min_distance, .extra_bits =  0 },
            .{ .base = 4     - min_distance, .extra_bits =  0 },
            .{ .base = 5     - min_distance, .extra_bits =  1 },
            .{ .base = 7     - min_distance, .extra_bits =  1 },
            .{ .base = 9     - min_distance, .extra_bits =  2 },
            .{ .base = 13    - min_distance, .extra_bits =  2 },
            .{ .base = 17    - min_distance, .extra_bits =  3 },
            .{ .base = 25    - min_distance, .extra_bits =  3 },
            .{ .base = 33    - min_distance, .extra_bits =  4 },
            .{ .base = 49    - min_distance, .extra_bits =  4 },
            .{ .base = 65    - min_distance, .extra_bits =  5 },
            .{ .base = 97    - min_distance, .extra_bits =  5 },
            .{ .base = 129   - min_distance, .extra_bits =  6 },
            .{ .base = 193   - min_distance, .extra_bits =  6 },
            .{ .base = 257   - min_distance, .extra_bits =  7 },
            .{ .base = 385   - min_distance, .extra_bits =  7 },
            .{ .base = 513   - min_distance, .extra_bits =  8 },
            .{ .base = 769   - min_distance, .extra_bits =  8 },
            .{ .base = 1025  - min_distance, .extra_bits =  9 },
            .{ .base = 1537  - min_distance, .extra_bits =  9 },
            .{ .base = 2049  - min_distance, .extra_bits = 10 },
            .{ .base = 3073  - min_distance, .extra_bits = 10 },
            .{ .base = 4097  - min_distance, .extra_bits = 11 },
            .{ .base = 6145  - min_distance, .extra_bits = 11 },
            .{ .base = 8193  - min_distance, .extra_bits = 12 },
            .{ .base = 12289 - min_distance, .extra_bits = 12 },
            .{ .base = 16385 - min_distance, .extra_bits = 13 },
            .{ .base = 24577 - min_distance, .extra_bits = 13 },
        }) |code, params| {
            // zig fmt: on
            const c: u5 = @intCast(code);
            try std.testing.expectEqual(params.extra_bits, Code.extraBits(.fromInt(@intCast(c))));
            try std.testing.expectEqual(params.base, Code.base(.fromInt(@intCast(c))));
            for (params.base..params.base + @shlExact(@as(u16, 1), params.extra_bits)) |v| {
                try std.testing.expectEqual(c, Code.fromVal(@intCast(v)).toInt());
            }
        }
    }
}



---
File: /std/compress/lzma/test.zig
---




---
File: /std/compress/xz/Decompress.zig
---

const Decompress = @This();
const std = @import("../../std.zig");
const Allocator = std.mem.Allocator;
const ArrayList = std.ArrayList;
const Crc32 = std.hash.Crc32;
const Crc64 = std.hash.crc.Crc64Xz;
const Sha256 = std.crypto.hash.sha2.Sha256;
const lzma2 = std.compress.lzma2;
const Writer = std.Io.Writer;
const Reader = std.Io.Reader;
const assert = std.debug.assert;

/// Underlying compressed data stream to pull bytes from.
input: *Reader,
/// Uncompressed bytes output by this stream implementation.
reader: Reader,
gpa: Allocator,
check: Check,
block_count: usize,
err: ?Error,

pub const Error = error{
    ReadFailed,
    OutOfMemory,
    CorruptInput,
    EndOfStream,
    WrongChecksum,
    Unsupported,
    Overflow,
    InvalidRangeCode,
    DecompressedSizeMismatch,
    CompressedSizeMismatch,
};

pub const Check = enum(u4) {
    none = 0x00,
    crc32 = 0x01,
    crc64 = 0x04,
    sha256 = 0x0A,
    _,
};

pub const StreamFlags = packed struct(u16) {
    null: u8 = 0,
    check: Check,
    reserved: u4 = 0,
};

pub const InitError = error{
    NotXzStream,
    WrongChecksum,
};

/// XZ uses a series of LZMA2 blocks which each specify a dictionary size
/// anywhere from 4K to 4G. Thus, this API dynamically allocates the dictionary
/// as-needed.
pub fn init(
    input: *Reader,
    gpa: Allocator,
    /// Decompress takes ownership of this buffer and resizes it with `gpa`.
    buffer: []u8,
) !Decompress {
    const magic = try input.takeArray(6);
    if (!std.mem.eql(u8, magic, &.{ 0xFD, '7', 'z', 'X', 'Z', 0x00 }))
        return error.NotXzStream;

    const computed_checksum = Crc32.hash(try input.peek(@sizeOf(StreamFlags)));
    const stream_flags = input.takeStruct(StreamFlags, .little) catch unreachable;
    const stored_hash = try input.takeInt(u32, .little);
    if (computed_checksum != stored_hash) return error.WrongChecksum;

    return .{
        .input = input,
        .reader = .{
            .vtable = &.{
                .stream = stream,
                .readVec = readVec,
                .discard = discard,
            },
            .buffer = buffer,
            .seek = 0,
            .end = 0,
        },
        .gpa = gpa,
        .check = stream_flags.check,
        .block_count = 0,
        .err = null,
    };
}

/// Reclaim ownership of the buffer passed to `init`.
pub fn takeBuffer(d: *Decompress) []u8 {
    const buffer = d.reader.buffer;
    d.reader.buffer = &.{};
    return buffer;
}

pub fn deinit(d: *Decompress) void {
    const gpa = d.gpa;
    gpa.free(d.reader.buffer);
    d.* = undefined;
}

fn readVec(r: *Reader, data: [][]u8) Reader.Error!usize {
    _ = data;
    return readIndirect(r);
}

fn stream(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    _ = w;
    _ = limit;
    return readIndirect(r);
}

fn discard(r: *Reader, limit: std.Io.Limit) Reader.Error!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    _ = d;
    _ = limit;
    @panic("TODO");
}

fn readIndirect(r: *Reader) Reader.Error!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    const gpa = d.gpa;
    const input = d.input;

    var allocating = Writer.Allocating.initOwnedSlice(gpa, r.buffer);
    allocating.writer.end = r.end;
    defer {
        r.buffer = allocating.writer.buffer;
        r.end = allocating.writer.end;
    }

    if (d.err != null) return error.ReadFailed;
    if (d.block_count == std.math.maxInt(usize)) return error.EndOfStream;

    readBlock(input, &allocating) catch |err| switch (err) {
        error.WriteFailed => {
            d.err = error.OutOfMemory;
            return error.ReadFailed;
        },
        error.SuccessfulEndOfStream => {
            finish(d) catch |finish_err| {
                d.err = finish_err;
                return error.ReadFailed;
            };
            d.block_count = std.math.maxInt(usize);
            return error.EndOfStream;
        },
        else => |e| {
            d.err = e;
            return error.ReadFailed;
        },
    };
    switch (d.check) {
        .none => {},
        .crc32 => {
            const declared_checksum = try input.takeInt(u32, .little);
            // TODO
            //const hash_a = Crc32.hash(unpacked_bytes);
            //if (hash_a != hash_b) return error.WrongChecksum;
            _ = declared_checksum;
        },
        .crc64 => {
            const declared_checksum = try input.takeInt(u64, .little);
            // TODO
            //const hash_a = Crc64.hash(unpacked_bytes);
            //if (hash_a != hash_b) return error.WrongChecksum;
            _ = declared_checksum;
        },
        .sha256 => {
            const declared_hash = try input.take(Sha256.digest_length);
            // TODO
            //var hash_a: [Sha256.digest_length]u8 = undefined;
            //Sha256.hash(unpacked_bytes, &hash_a, .{});
            //if (!std.mem.eql(u8, &hash_a, &hash_b))
            //    return error.WrongChecksum;
            _ = declared_hash;
        },
        else => {
            d.err = error.Unsupported;
            return error.ReadFailed;
        },
    }
    d.block_count += 1;
    return 0;
}

fn readBlock(input: *Reader, allocating: *Writer.Allocating) !void {
    var packed_size: ?u64 = null;
    var unpacked_size: ?u64 = null;

    const header_size = h: {
        // Read the block header via peeking so that we can hash the whole thing too.
        const first_byte: usize = try input.peekByte();
        if (first_byte == 0) return error.SuccessfulEndOfStream;

        const declared_header_size = first_byte * 4;
        try input.fill(declared_header_size);
        const header_seek_start = input.seek;
        input.toss(1);

        const Flags = packed struct(u8) {
            last_filter_index: u2,
            reserved: u4,
            has_packed_size: bool,
            has_unpacked_size: bool,
        };
        const flags = try input.takeStruct(Flags, .little);

        const filter_count = @as(u3, flags.last_filter_index) + 1;
        if (filter_count > 1) return error.Unsupported;

        if (flags.has_packed_size) packed_size = try input.takeLeb128(u64);
        if (flags.has_unpacked_size) unpacked_size = try input.takeLeb128(u64);

        const FilterId = enum(u64) {
            lzma2 = 0x21,
            _,
        };

        const filter_id: FilterId = @enumFromInt(try input.takeLeb128(u64));
        if (filter_id != .lzma2) return error.Unsupported;

        const properties_size = try input.takeLeb128(u64);
        if (properties_size != 1) return error.CorruptInput;
        // TODO: use filter properties
        _ = try input.takeByte();

        const actual_header_size = input.seek - header_seek_start;
        if (actual_header_size > declared_header_size) return error.CorruptInput;
        const remaining_bytes = declared_header_size - actual_header_size;
        for (0..remaining_bytes) |_| {
            if (try input.takeByte() != 0) return error.CorruptInput;
        }

        const header_slice = input.buffer[header_seek_start..][0..declared_header_size];
        const computed_checksum = Crc32.hash(header_slice);
        const declared_checksum = try input.takeInt(u32, .little);
        if (computed_checksum != declared_checksum) return error.WrongChecksum;
        break :h declared_header_size;
    };

    // Compressed Data

    var lzma2_decode = try lzma2.Decode.init(allocating.allocator);
    defer lzma2_decode.deinit(allocating.allocator);
    const before_size = allocating.writer.end;
    const packed_bytes_read = try lzma2_decode.decompress(input, allocating);
    const unpacked_bytes = allocating.writer.end - before_size;

    if (packed_size) |s| {
        if (s != packed_bytes_read) return error.CorruptInput;
    }

    if (unpacked_size) |s| {
        if (s != unpacked_bytes) return error.CorruptInput;
    }

    // Block Padding
    const block_counter = header_size + packed_bytes_read;
    const padding = try input.take(@intCast((4 - (block_counter % 4)) % 4));
    for (padding) |byte| {
        if (byte != 0) return error.CorruptInput;
    }
}

fn finish(d: *Decompress) !void {
    const input = d.input;
    const index_size = blk: {
        // Assume that we already peeked a zero in readBlock().
        assert(input.buffered()[0] == 0);
        var input_counter: u64 = 1;
        var checksum: Crc32 = .init();
        checksum.update(&.{0});
        input.toss(1);

        const record_count = try countLeb128(input, u64, &input_counter, &checksum);
        if (record_count != d.block_count)
            return error.CorruptInput;

        for (0..@intCast(record_count)) |_| {
            // TODO: validate records
            _ = try countLeb128(input, u64, &input_counter, &checksum);
            _ = try countLeb128(input, u64, &input_counter, &checksum);
        }

        const padding = try input.take(@intCast((4 - (input_counter % 4)) % 4));
        for (padding) |byte| {
            if (byte != 0) return error.CorruptInput;
        }
        checksum.update(padding);

        const declared_checksum = try input.takeInt(u32, .little);
        const computed_checksum = checksum.final();
        if (computed_checksum != declared_checksum) return error.WrongChecksum;

        break :blk input_counter + padding.len + 4;
    };

    const declared_checksum = try input.takeInt(u32, .little);
    const computed_checksum = Crc32.hash(try input.peek(4 + @sizeOf(StreamFlags)));
    if (declared_checksum != computed_checksum) return error.WrongChecksum;
    const backward_size = (@as(u64, try input.takeInt(u32, .little)) + 1) * 4;
    if (backward_size != index_size) return error.CorruptInput;
    input.toss(@sizeOf(StreamFlags));
    if (!std.mem.eql(u8, try input.takeArray(2), &.{ 'Y', 'Z' }))
        return error.CorruptInput;
}

fn countLeb128(reader: *Reader, comptime T: type, counter: *u64, hasher: *Crc32) !T {
    try reader.fill(8);
    const start = reader.seek;
    const result = try reader.takeLeb128(T);
    const read_slice = reader.buffer[start..reader.seek];
    hasher.update(read_slice);
    counter.* += read_slice.len;
    return result;
}



---
File: /std/compress/xz/test.zig
---




---
File: /std/compress/zstd/Decompress.zig
---

const Decompress = @This();
const std = @import("std");
const assert = std.debug.assert;
const Reader = std.Io.Reader;
const Limit = std.Io.Limit;
const zstd = @import("../zstd.zig");
const Writer = std.Io.Writer;

input: *Reader,
reader: Reader,
state: State,
verify_checksum: bool,
window_len: u32,
err: ?Error = null,

const State = union(enum) {
    new_frame,
    in_frame: InFrame,
    skipping_frame: usize,

    const InFrame = struct {
        frame: Frame,
        checksum: ?u32,
        decompressed_size: usize,
        decode: Frame.Zstandard.Decode,
    };
};

pub const Options = struct {
    /// Verifying checksums is not implemented yet and will cause a panic if
    /// you set this to true.
    verify_checksum: bool = false,

    /// The output buffer is asserted to have capacity for `window_len` plus
    /// `zstd.block_size_max`.
    ///
    /// If `window_len` is too small, then some streams will fail to decompress
    /// with `error.OutputBufferUndersize`.
    window_len: u32 = zstd.default_window_len,
};

pub const Error = error{
    BadMagic,
    BlockOversize,
    ChecksumFailure,
    ContentOversize,
    DictionaryIdFlagUnsupported,
    EndOfStream,
    HuffmanTreeIncomplete,
    InvalidBitStream,
    MalformedAccuracyLog,
    MalformedBlock,
    MalformedCompressedBlock,
    MalformedFrame,
    MalformedFseBits,
    MalformedFseTable,
    MalformedHuffmanTree,
    MalformedLiteralsHeader,
    MalformedLiteralsLength,
    MalformedLiteralsSection,
    MalformedSequence,
    MissingStartBit,
    OutputBufferUndersize,
    InputBufferUndersize,
    ReadFailed,
    RepeatModeFirst,
    ReservedBitSet,
    ReservedBlock,
    SequenceBufferUndersize,
    TreelessLiteralsFirst,
    UnexpectedEndOfLiteralStream,
    WindowOversize,
    WindowSizeUnknown,
};

const direct_vtable: Reader.VTable = .{
    .stream = streamDirect,
    .rebase = rebaseFallible,
    .discard = discardDirect,
    .readVec = readVec,
};

const indirect_vtable: Reader.VTable = .{
    .stream = streamIndirect,
    .rebase = rebaseFallible,
    .discard = discardIndirect,
    .readVec = readVec,
};

/// When connecting `reader` to a `Writer`, `buffer` should be empty, and
/// `Writer.buffer` capacity has requirements based on `Options.window_len`.
///
/// Otherwise, `buffer` has those requirements.
pub fn init(input: *Reader, buffer: []u8, options: Options) Decompress {
    if (buffer.len != 0) assert(buffer.len >= options.window_len + zstd.block_size_max);
    return .{
        .input = input,
        .state = .new_frame,
        .verify_checksum = options.verify_checksum,
        .window_len = options.window_len,
        .reader = .{
            .vtable = if (buffer.len == 0) &direct_vtable else &indirect_vtable,
            .buffer = buffer,
            .seek = 0,
            .end = 0,
        },
    };
}

fn streamDirect(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    return stream(d, w, limit);
}

fn streamIndirect(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    _ = limit;
    _ = w;
    return streamIndirectInner(d);
}

fn rebaseFallible(r: *Reader, capacity: usize) Reader.RebaseError!void {
    rebase(r, capacity);
}

// Rebase the buffer, keeping at least the sliding window (`d.window_len` bytes) buffered
fn rebase(r: *Reader, capacity: usize) void {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    // `capacity` must fit in the buffer along with the required sliding window
    assert(capacity <= r.buffer.len - d.window_len);
    // According to the vtable contract, this function will only be called if the free space in the
    // buffer cannot already fit `capacity` bytes
    assert(r.end + capacity > r.buffer.len);
    const discard_n = @min(r.seek, r.end - d.window_len);
    const keep = r.buffer[discard_n..r.end];
    @memmove(r.buffer[0..keep.len], keep);
    r.end = keep.len;
    r.seek -= discard_n;
}

/// Rebase `d.reader.buffer` as much as needed for a discard limited by `limit`
fn rebaseForDiscard(d: *Decompress, limit: std.Io.Limit) void {
    // Number of bytes desired to rebase, always rebase for at least block_size
    const desire_n = limit.max(Limit.limited(zstd.block_size_max));
    // Maximum number of bytes possible to rebase
    const max_n = d.reader.buffer.len -| d.window_len;
    // Number of bytes to rebase
    const n = desire_n.minInt(max_n);

    // Current buffer free space
    const current_cap = d.reader.buffer.len - d.reader.end;
    if (current_cap < n) {
        rebase(&d.reader, n);
    }
}

/// This could be improved so that when an amount is discarded that includes an
/// entire frame, skip decoding that frame.
fn discardDirect(r: *Reader, limit: std.Io.Limit) Reader.Error!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    rebaseForDiscard(d, limit);
    var writer: Writer = .{
        .vtable = &.{
            .drain = std.Io.Writer.Discarding.drain,
            .sendFile = std.Io.Writer.Discarding.sendFile,
        },
        .buffer = r.buffer,
        .end = r.end,
    };
    defer {
        r.end = writer.end;
        r.seek = r.end;
    }
    const n = r.stream(&writer, limit) catch |err| switch (err) {
        error.WriteFailed => unreachable,
        error.ReadFailed => return error.ReadFailed,
        error.EndOfStream => return error.EndOfStream,
    };
    assert(n <= @intFromEnum(limit));
    return n;
}

fn discardIndirect(r: *Reader, limit: std.Io.Limit) Reader.Error!usize {
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    rebaseForDiscard(d, limit);
    var writer: Writer = .{
        .buffer = r.buffer,
        .end = r.end,
        .vtable = &.{ .drain = Writer.unreachableDrain },
    };
    {
        defer r.end = writer.end;
        _ = stream(d, &writer, .limited(writer.buffer.len - writer.end)) catch |err| switch (err) {
            error.WriteFailed => unreachable,
            else => |e| return e,
        };
    }
    const n = limit.minInt(r.end - r.seek);
    r.seek += n;
    return n;
}

fn readVec(r: *Reader, data: [][]u8) Reader.Error!usize {
    _ = data;
    const d: *Decompress = @alignCast(@fieldParentPtr("reader", r));
    return streamIndirectInner(d);
}

fn streamIndirectInner(d: *Decompress) Reader.Error!usize {
    const r = &d.reader;
    if (r.buffer.len - r.end < zstd.block_size_max) rebase(r, zstd.block_size_max);
    assert(r.buffer.len - r.end >= zstd.block_size_max);
    var writer: Writer = .{
        .buffer = r.buffer,
        .end = r.end,
        .vtable = &.{
            .drain = Writer.unreachableDrain,
            .rebase = Writer.unreachableRebase,
        },
    };
    defer r.end = writer.end;
    _ = stream(d, &writer, .limited(writer.buffer.len - writer.end)) catch |err| switch (err) {
        error.WriteFailed => unreachable,
        else => |e| return e,
    };
    return 0;
}

fn stream(d: *Decompress, w: *Writer, limit: Limit) Reader.StreamError!usize {
    const in = d.input;

    state: switch (d.state) {
        .new_frame => {
            // Only return EndOfStream when there are exactly 0 bytes remaining on the
            // frame magic. Any partial magic bytes should be considered a failure.
            in.fill(@sizeOf(Frame.Magic)) catch |err| switch (err) {
                error.EndOfStream => {
                    if (in.bufferedLen() != 0) {
                        d.err = error.BadMagic;
                        return error.ReadFailed;
                    }
                    return err;
                },
                else => |e| return e,
            };
            const magic = try in.takeEnumNonexhaustive(Frame.Magic, .little);
            initFrame(d, magic) catch |err| {
                d.err = err;
                return error.ReadFailed;
            };
            continue :state d.state;
        },
        .in_frame => |*in_frame| {
            return readInFrame(d, w, limit, in_frame) catch |err| switch (err) {
                error.ReadFailed => return error.ReadFailed,
                error.WriteFailed => return error.WriteFailed,
                else => |e| {
                    d.err = e;
                    return error.ReadFailed;
                },
            };
        },
        .skipping_frame => |*remaining| {
            const n = in.discard(.limited(remaining.*)) catch |err| {
                d.err = err;
                return error.ReadFailed;
            };
            remaining.* -= n;
            if (remaining.* == 0) d.state = .new_frame;
            return 0;
        },
    }
}

fn initFrame(d: *Decompress, magic: Frame.Magic) !void {
    const in = d.input;
    switch (magic.kind() orelse return error.BadMagic) {
        .zstandard => {
            const header = try Frame.Zstandard.Header.decode(in);
            d.state = .{ .in_frame = .{
                .frame = try Frame.init(header, d.window_len, d.verify_checksum),
                .checksum = null,
                .decompressed_size = 0,
                .decode = .init,
            } };
        },
        .skippable => {
            const frame_size = try in.takeInt(u32, .little);
            d.state = .{ .skipping_frame = frame_size };
        },
    }
}

fn readInFrame(d: *Decompress, w: *Writer, limit: Limit, state: *State.InFrame) !usize {
    const in = d.input;
    const window_len = d.window_len;

    const block_header = try in.takeStruct(Frame.Zstandard.Block.Header, .little);
    const block_size = block_header.size;
    const frame_block_size_max = state.frame.block_size_max;
    if (frame_block_size_max < block_size) return error.BlockOversize;
    if (@intFromEnum(limit) < block_size) return error.OutputBufferUndersize;
    var bytes_written: usize = 0;
    switch (block_header.type) {
        .raw => {
            try in.streamExactPreserve(w, window_len, block_size);
            bytes_written = block_size;
        },
        .rle => {
            const byte = try in.takeByte();
            try w.splatBytePreserve(window_len, byte, block_size);
            bytes_written = block_size;
        },
        .compressed => {
            var literals_buffer: [zstd.block_size_max]u8 = undefined;
            var sequence_buffer: [zstd.block_size_max]u8 = undefined;
            var remaining: Limit = .limited(block_size);
            const literals = try LiteralsSection.decode(in, &remaining, &literals_buffer);
            const sequences_header = try SequencesSection.Header.decode(in, &remaining);

            const decode = &state.decode;
            try decode.prepare(in, &remaining, literals, sequences_header);

            {
                if (sequence_buffer.len < @intFromEnum(remaining))
                    return error.SequenceBufferUndersize;
                const seq_slice = remaining.slice(&sequence_buffer);
                try in.readSliceAll(seq_slice);
                var bit_stream = try ReverseBitReader.init(seq_slice);

                if (sequences_header.sequence_count > 0) {
                    try decode.readInitialFseState(&bit_stream);

                    // Ensures the following calls to `decodeSequence` will not flush.
                    const dest = (try w.writableSliceGreedyPreserve(window_len, frame_block_size_max))[0..frame_block_size_max];
                    const write_pos = dest.ptr - w.buffer.ptr;
                    for (0..sequences_header.sequence_count - 1) |_| {
                        bytes_written += try decode.decodeSequence(w.buffer, write_pos + bytes_written, &bit_stream);
                        try decode.updateState(.literal, &bit_stream);
                        try decode.updateState(.match, &bit_stream);
                        try decode.updateState(.offset, &bit_stream);
                    }
                    bytes_written += try decode.decodeSequence(w.buffer, write_pos + bytes_written, &bit_stream);
                    if (bytes_written > dest.len) return error.MalformedSequence;
                    w.advance(bytes_written);
                }

                if (!bit_stream.isEmpty()) {
                    return error.MalformedCompressedBlock;
                }
            }

            if (decode.literal_written_count < literals.header.regenerated_size) {
                const len = literals.header.regenerated_size - decode.literal_written_count;
                try decode.decodeLiterals(w, len);
                decode.literal_written_count += len;
                bytes_written += len;
            }

            switch (decode.literal_header.block_type) {
                .treeless, .compressed => {
                    if (!decode.isLiteralStreamEmpty()) return error.MalformedCompressedBlock;
                },
                .raw, .rle => {},
            }

            if (bytes_written > frame_block_size_max) return error.BlockOversize;
        },
        .reserved => return error.ReservedBlock,
    }

    if (state.frame.hasher_opt) |*hasher| {
        if (bytes_written > 0) {
            _ = hasher;
            @panic("TODO all those bytes written needed to go through the hasher too");
        }
    }

    state.decompressed_size += bytes_written;

    if (block_header.last) {
        if (state.frame.has_checksum) {
            const expected_checksum = try in.takeInt(u32, .little);
            if (state.frame.hasher_opt) |*hasher| {
                const actual_checksum: u32 = @truncate(hasher.final());
                if (expected_checksum != actual_checksum) return error.ChecksumFailure;
            }
        }
        if (state.frame.content_size) |content_size| {
            if (content_size != state.decompressed_size) {
                return error.MalformedFrame;
            }
        }
        d.state = .new_frame;
    } else if (state.frame.content_size) |content_size| {
        if (state.decompressed_size > content_size) return error.MalformedFrame;
    }

    return bytes_written;
}

pub const Frame = struct {
    hasher_opt: ?std.hash.XxHash64,
    window_size: usize,
    has_checksum: bool,
    block_size_max: usize,
    content_size: ?usize,

    pub const Magic = enum(u32) {
        zstandard = 0xFD2FB528,
        _,

        pub fn kind(m: Magic) ?Kind {
            return switch (@intFromEnum(m)) {
                @intFromEnum(Magic.zstandard) => .zstandard,
                @intFromEnum(Skippable.magic_min)...@intFromEnum(Skippable.magic_max) => .skippable,
                else => null,
 
```
