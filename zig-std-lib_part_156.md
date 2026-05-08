```
,
        list.items(.a),
    );
    try testing.expectEqualSlices(
        u8,
        &[_]u8{ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i' },
        list.items(.c),
    );

    list.shrinkAndFree(ally, 3);

    try testing.expectEqualSlices(u32, list.items(.a), &[_]u32{ 1, 2, 3 });
    try testing.expectEqualSlices(u8, list.items(.c), &[_]u8{ 'a', 'b', 'c' });

    try testing.expectEqual(@as(usize, 3), list.items(.b).len);
    try testing.expectEqualStrings("foobar", list.items(.b)[0]);
    try testing.expectEqualStrings("zigzag", list.items(.b)[1]);
    try testing.expectEqualStrings("fizzbuzz", list.items(.b)[2]);

    try testing.expectError(error.OutOfMemory, list.addOneBounded());

    list.set(try list.addOne(ally), .{
        .a = 4,
        .b = "xnopyt",
        .c = 'd',
    });
    try testing.expectEqualStrings("xnopyt", list.pop().?.b);
    try testing.expectEqual(@as(?u8, 'c'), if (list.pop()) |elem| elem.c else null);
    try testing.expectEqual(@as(u32, 2), list.pop().?.a);
    try testing.expectEqual(@as(u8, 'a'), list.pop().?.c);
    try testing.expectEqual(@as(?Foo, null), list.pop());

    list.clearRetainingCapacity();
    try testing.expectEqual(0, list.len);
    try testing.expect(list.capacity > 0);

    list.clearAndFree(ally);
    try testing.expectEqual(0, list.len);
    try testing.expectEqual(0, list.capacity);
}

// This was observed to fail on aarch64 with LLVM 11, when the capacityInBytes
// function used the @reduce code path.
test "regression test for @reduce bug" {
    const ally = testing.allocator;
    var list: MultiArrayList(struct {
        tag: std.zig.Token.Tag,
        start: u32,
    }) = .empty;
    defer list.deinit(ally);

    try list.ensureTotalCapacity(ally, 20);

    try list.append(ally, .{ .tag = .keyword_const, .start = 0 });
    try list.append(ally, .{ .tag = .identifier, .start = 6 });
    try list.append(ally, .{ .tag = .equal, .start = 10 });
    try list.append(ally, .{ .tag = .builtin, .start = 12 });
    try list.append(ally, .{ .tag = .l_paren, .start = 19 });
    try list.append(ally, .{ .tag = .string_literal, .start = 20 });
    try list.append(ally, .{ .tag = .r_paren, .start = 25 });
    try list.append(ally, .{ .tag = .semicolon, .start = 26 });
    try list.append(ally, .{ .tag = .keyword_pub, .start = 29 });
    try list.append(ally, .{ .tag = .keyword_fn, .start = 33 });
    try list.append(ally, .{ .tag = .identifier, .start = 36 });
    try list.append(ally, .{ .tag = .l_paren, .start = 40 });
    try list.append(ally, .{ .tag = .r_paren, .start = 41 });
    try list.append(ally, .{ .tag = .identifier, .start = 43 });
    try list.append(ally, .{ .tag = .bang, .start = 51 });
    try list.append(ally, .{ .tag = .identifier, .start = 52 });
    try list.append(ally, .{ .tag = .l_brace, .start = 57 });
    try list.append(ally, .{ .tag = .identifier, .start = 63 });
    try list.append(ally, .{ .tag = .period, .start = 66 });
    try list.append(ally, .{ .tag = .identifier, .start = 67 });
    try list.append(ally, .{ .tag = .period, .start = 70 });
    try list.append(ally, .{ .tag = .identifier, .start = 71 });
    try list.append(ally, .{ .tag = .l_paren, .start = 75 });
    try list.append(ally, .{ .tag = .string_literal, .start = 76 });
    try list.append(ally, .{ .tag = .comma, .start = 113 });
    try list.append(ally, .{ .tag = .period, .start = 115 });
    try list.append(ally, .{ .tag = .l_brace, .start = 116 });
    try list.append(ally, .{ .tag = .r_brace, .start = 117 });
    try list.append(ally, .{ .tag = .r_paren, .start = 118 });
    try list.append(ally, .{ .tag = .semicolon, .start = 119 });
    try list.append(ally, .{ .tag = .r_brace, .start = 121 });
    try list.append(ally, .{ .tag = .eof, .start = 123 });

    const tags = list.items(.tag);
    try testing.expectEqual(tags[1], .identifier);
    try testing.expectEqual(tags[2], .equal);
    try testing.expectEqual(tags[3], .builtin);
    try testing.expectEqual(tags[4], .l_paren);
    try testing.expectEqual(tags[5], .string_literal);
    try testing.expectEqual(tags[6], .r_paren);
    try testing.expectEqual(tags[7], .semicolon);
    try testing.expectEqual(tags[8], .keyword_pub);
    try testing.expectEqual(tags[9], .keyword_fn);
    try testing.expectEqual(tags[10], .identifier);
    try testing.expectEqual(tags[11], .l_paren);
    try testing.expectEqual(tags[12], .r_paren);
    try testing.expectEqual(tags[13], .identifier);
    try testing.expectEqual(tags[14], .bang);
    try testing.expectEqual(tags[15], .identifier);
    try testing.expectEqual(tags[16], .l_brace);
    try testing.expectEqual(tags[17], .identifier);
    try testing.expectEqual(tags[18], .period);
    try testing.expectEqual(tags[19], .identifier);
    try testing.expectEqual(tags[20], .period);
    try testing.expectEqual(tags[21], .identifier);
    try testing.expectEqual(tags[22], .l_paren);
    try testing.expectEqual(tags[23], .string_literal);
    try testing.expectEqual(tags[24], .comma);
    try testing.expectEqual(tags[25], .period);
    try testing.expectEqual(tags[26], .l_brace);
    try testing.expectEqual(tags[27], .r_brace);
    try testing.expectEqual(tags[28], .r_paren);
    try testing.expectEqual(tags[29], .semicolon);
    try testing.expectEqual(tags[30], .r_brace);
    try testing.expectEqual(tags[31], .eof);
}

test "ensure capacity on empty list" {
    const ally = testing.allocator;

    const Foo = struct {
        a: u32,
        b: u8,
    };

    var list: MultiArrayList(Foo) = .empty;
    defer list.deinit(ally);

    try list.ensureTotalCapacity(ally, 2);
    list.appendAssumeCapacity(.{ .a = 1, .b = 2 });
    list.appendAssumeCapacity(.{ .a = 3, .b = 4 });

    try testing.expectEqualSlices(u32, &[_]u32{ 1, 3 }, list.items(.a));
    try testing.expectEqualSlices(u8, &[_]u8{ 2, 4 }, list.items(.b));

    list.len = 0;
    list.appendAssumeCapacity(.{ .a = 5, .b = 6 });
    list.appendAssumeCapacity(.{ .a = 7, .b = 8 });

    try testing.expectEqualSlices(u32, &[_]u32{ 5, 7 }, list.items(.a));
    try testing.expectEqualSlices(u8, &[_]u8{ 6, 8 }, list.items(.b));

    list.len = 0;
    try list.ensureTotalCapacity(ally, 16);

    list.appendAssumeCapacity(.{ .a = 9, .b = 10 });
    list.appendAssumeCapacity(.{ .a = 11, .b = 12 });

    try testing.expectEqualSlices(u32, &[_]u32{ 9, 11 }, list.items(.a));
    try testing.expectEqualSlices(u8, &[_]u8{ 10, 12 }, list.items(.b));
}

test "insert elements" {
    const ally = testing.allocator;

    const Foo = struct {
        a: u8,
        b: u32,
    };

    var list = try MultiArrayList(Foo).initCapacity(ally, 2);
    defer list.deinit(ally);

    try list.insertBounded(0, .{ .a = 1, .b = 2 });
    list.insertAssumeCapacity(1, .{ .a = 2, .b = 3 });
    try list.insert(ally, 0, .{ .a = 3, .b = 4 });

    try testing.expectEqualSlices(u8, &[_]u8{ 3, 1, 2 }, list.items(.a));
    try testing.expectEqualSlices(u32, &[_]u32{ 4, 2, 3 }, list.items(.b));
}

test "initCapacity" {
    const gpa = testing.allocator;

    var list = try MultiArrayList(struct { a: u8, b: u32 }).initCapacity(gpa, 404);
    defer list.deinit(gpa);

    try testing.expectEqual(0, list.len);
    try testing.expectEqual(404, list.capacity);
}

test "union" {
    const ally = testing.allocator;

    const Foo = union(enum) {
        a: u32,
        b: []const u8,
    };

    var list: MultiArrayList(Foo) = .empty;
    defer list.deinit(ally);

    try testing.expectEqual(@as(usize, 0), list.items(.tags).len);

    try list.ensureTotalCapacity(ally, 3);

    list.appendAssumeCapacity(.{ .a = 1 });
    list.appendAssumeCapacity(.{ .b = "zigzag" });

    try testing.expectEqualSlices(meta.Tag(Foo), list.items(.tags), &.{ .a, .b });
    try testing.expectEqual(@as(usize, 2), list.items(.tags).len);

    list.appendAssumeCapacity(.{ .b = "foobar" });
    try testing.expectEqualStrings("zigzag", list.items(.data)[1].b);
    try testing.expectEqualStrings("foobar", list.items(.data)[2].b);

    // Add 6 more things to force a capacity increase.
    for (0..6) |i| {
        try list.append(ally, .{ .a = @as(u32, @intCast(4 + i)) });
    }

    try testing.expectEqualSlices(
        meta.Tag(Foo),
        &.{ .a, .b, .b, .a, .a, .a, .a, .a, .a },
        list.items(.tags),
    );
    try testing.expectEqual(Foo{ .a = 1 }, list.get(0));
    try testing.expectEqual(Foo{ .b = "zigzag" }, list.get(1));
    try testing.expectEqual(Foo{ .b = "foobar" }, list.get(2));
    try testing.expectEqual(Foo{ .a = 4 }, list.get(3));
    try testing.expectEqual(Foo{ .a = 5 }, list.get(4));
    try testing.expectEqual(Foo{ .a = 6 }, list.get(5));
    try testing.expectEqual(Foo{ .a = 7 }, list.get(6));
    try testing.expectEqual(Foo{ .a = 8 }, list.get(7));
    try testing.expectEqual(Foo{ .a = 9 }, list.get(8));

    list.shrinkAndFree(ally, 3);

    try testing.expectEqual(@as(usize, 3), list.items(.tags).len);
    try testing.expectEqualSlices(meta.Tag(Foo), list.items(.tags), &.{ .a, .b, .b });

    try testing.expectEqual(Foo{ .a = 1 }, list.get(0));
    try testing.expectEqual(Foo{ .b = "zigzag" }, list.get(1));
    try testing.expectEqual(Foo{ .b = "foobar" }, list.get(2));
}

test "sorting a span" {
    var list: MultiArrayList(struct { score: u32, chr: u8 }) = .empty;
    defer list.deinit(testing.allocator);

    try list.ensureTotalCapacity(testing.allocator, 42);
    for (
        // zig fmt: off
        [42]u8{ 'b', 'a', 'c', 'a', 'b', 'c', 'b', 'c', 'b', 'a', 'b', 'a', 'b', 'c', 'b', 'a', 'a', 'c', 'c', 'a', 'c', 'b', 'a', 'c', 'a', 'b', 'b', 'c', 'c', 'b', 'a', 'b', 'a', 'b', 'c', 'b', 'a', 'a', 'c', 'c', 'a', 'c' },
        [42]u32{ 1,   1,   1,   2,   2,   2,   3,   3,   4,   3,   5,   4,   6,   4,   7,   5,   6,   5,   6,   7,   7,   8,   8,   8,   9,   9,  10,   9,  10,  11,  10,  12,  11,  13,  11,  14,  12,  13,  12,  13,  14,  14 },
        // zig fmt: on
    ) |chr, score| {
        list.appendAssumeCapacity(.{ .chr = chr, .score = score });
    }

    const sliced = list.slice();
    list.sortSpan(6, 21, struct {
        chars: []const u8,

        fn lessThan(ctx: @This(), a: usize, b: usize) bool {
            return ctx.chars[a] < ctx.chars[b];
        }
    }{ .chars = sliced.items(.chr) });

    var i: u32 = undefined;
    var j: u32 = 6;
    var c: u8 = 'a';

    while (j < 21) {
        i = j;
        j += 5;
        var n: u32 = 3;
        for (sliced.items(.chr)[i..j], sliced.items(.score)[i..j]) |chr, score| {
            try testing.expectEqual(score, n);
            try testing.expectEqual(chr, c);
            n += 1;
        }
        c += 1;
    }
}

test "0 sized struct field" {
    const ally = testing.allocator;

    const Foo = struct {
        a: u0,
        b: f32,
    };

    var list: MultiArrayList(Foo) = .empty;
    defer list.deinit(ally);

    try testing.expectEqualSlices(u0, &[_]u0{}, list.items(.a));
    try testing.expectEqualSlices(f32, &[_]f32{}, list.items(.b));

    try list.append(ally, .{ .a = 0, .b = 42.0 });
    try testing.expectEqualSlices(u0, &[_]u0{0}, list.items(.a));
    try testing.expectEqualSlices(f32, &[_]f32{42.0}, list.items(.b));

    try list.insert(ally, 0, .{ .a = 0, .b = -1.0 });
    try testing.expectEqualSlices(u0, &[_]u0{ 0, 0 }, list.items(.a));
    try testing.expectEqualSlices(f32, &[_]f32{ -1.0, 42.0 }, list.items(.b));

    list.swapRemove(list.len - 1);
    try testing.expectEqualSlices(u0, &[_]u0{0}, list.items(.a));
    try testing.expectEqualSlices(f32, &[_]f32{-1.0}, list.items(.b));
}

test "0 sized struct" {
    const ally = testing.allocator;

    const Foo = struct {
        a: u0,
    };

    var list: MultiArrayList(Foo) = .empty;
    defer list.deinit(ally);

    try testing.expectEqualSlices(u0, &[_]u0{}, list.items(.a));

    try list.append(ally, .{ .a = 0 });
    try testing.expectEqualSlices(u0, &[_]u0{0}, list.items(.a));

    try list.insert(ally, 0, .{ .a = 0 });
    try testing.expectEqualSlices(u0, &[_]u0{ 0, 0 }, list.items(.a));

    list.swapRemove(list.len - 1);
    try testing.expectEqualSlices(u0, &[_]u0{0}, list.items(.a));
}

test "struct with many fields" {
    const ManyFields = struct {
        fn Type(count: comptime_int) type {
            @setEvalBranchQuota(50000);
            var field_names: [count][]const u8 = undefined;
            for (&field_names, 0..) |*n, i| n.* = std.fmt.comptimePrint("a{d}", .{i});
            return @Struct(.@"extern", null, &field_names, &@splat(u32), &@splat(.{}));
        }

        fn doTest(ally: std.mem.Allocator, count: comptime_int) !void {
            var list: MultiArrayList(Type(count)) = .empty;
            defer list.deinit(ally);

            try list.resize(ally, 1);
            list.items(.a0)[0] = 42;
        }
    };

    try ManyFields.doTest(testing.allocator, 25);
    try ManyFields.doTest(testing.allocator, 50);
    try ManyFields.doTest(testing.allocator, 100);
    try ManyFields.doTest(testing.allocator, 200);
}

test "orderedRemoveMany" {
    const gpa = testing.allocator;

    var list: MultiArrayList(struct { x: usize }) = .empty;
    defer list.deinit(gpa);

    for (0..10) |n| {
        try list.append(gpa, .{ .x = n });
    }

    list.orderedRemoveMany(&.{ 1, 5, 5, 7, 9 });
    try testing.expectEqualSlices(usize, &.{ 0, 2, 3, 4, 6, 8 }, list.items(.x));

    list.orderedRemoveMany(&.{0});
    try testing.expectEqualSlices(usize, &.{ 2, 3, 4, 6, 8 }, list.items(.x));

    list.orderedRemoveMany(&.{});
    try testing.expectEqualSlices(usize, &.{ 2, 3, 4, 6, 8 }, list.items(.x));

    list.orderedRemoveMany(&.{ 1, 2, 3, 4 });
    try testing.expectEqualSlices(usize, &.{2}, list.items(.x));

    list.orderedRemoveMany(&.{0});
    try testing.expectEqualSlices(usize, &.{}, list.items(.x));
}



---
File: /std/os.zig
---

const builtin = @import("builtin");
const native_os = builtin.os.tag;

pub const linux = @import("os/linux.zig");
pub const plan9 = @import("os/plan9.zig");
pub const uefi = @import("os/uefi.zig");
pub const wasi = @import("os/wasi.zig");
pub const emscripten = @import("os/emscripten.zig");
pub const windows = @import("os/windows.zig");

test {
    _ = linux;
    if (native_os == .uefi) _ = uefi;
    _ = wasi;
    _ = windows;
}



---
File: /std/pdb.zig
---

//! Program Data Base debugging information format.
//!
//! This namespace contains unopinionated types and data definitions only. For
//! an implementation of parsing and caching PDB information, see
//! `std.debug.Pdb`.
//!
//! Most of this is based on information gathered from LLVM source code,
//! documentation and/or contributors.

const std = @import("std.zig");
const math = std.math;
const mem = std.mem;
const coff = std.coff;
const fs = std.fs;
const File = std.Io.File;
const debug = std.debug;

const ArrayList = std.ArrayList;

/// https://llvm.org/docs/PDB/DbiStream.html#stream-header
pub const DbiStreamHeader = extern struct {
    version_signature: i32,
    version_header: u32,
    age: u32,
    global_stream_index: u16,
    build_number: u16,
    public_stream_index: u16,
    pdb_dll_version: u16,
    sym_record_stream: u16,
    pdb_dll_rbld: u16,
    mod_info_size: u32,
    section_contribution_size: u32,
    section_map_size: u32,
    source_info_size: i32,
    type_server_size: i32,
    mfc_type_server_index: u32,
    optional_dbg_header_size: i32,
    ec_substream_size: i32,
    flags: u16,
    machine: u16,
    padding: u32,
};

pub const SectionContribEntry = extern struct {
    /// COFF Section index, 1-based
    section: u16,
    padding1: [2]u8,
    offset: u32,
    size: u32,
    characteristics: u32,
    module_index: u16,
    padding2: [2]u8,
    data_crc: u32,
    reloc_crc: u32,
};

pub const ModInfo = extern struct {
    unused1: u32,
    section_contr: SectionContribEntry,
    flags: u16,
    module_sym_stream: u16,
    sym_byte_size: u32,
    c11_byte_size: u32,
    c13_byte_size: u32,
    source_file_count: u16,
    padding: [2]u8,
    unused2: u32,
    source_file_name_index: u32,
    pdb_file_path_name_index: u32,
    // These fields are variable length
    //module_name: char[],
    //obj_file_name: char[],
};

pub const SectionMapHeader = extern struct {
    /// Number of segment descriptors
    count: u16,

    /// Number of logical segment descriptors
    log_count: u16,
};

pub const SectionMapEntry = extern struct {
    /// See the SectionMapEntryFlags enum below.
    flags: u16,

    /// Logical overlay number
    ovl: u16,

    /// Group index into descriptor array.
    group: u16,
    frame: u16,

    /// Byte index of segment / group name in string table, or 0xFFFF.
    section_name: u16,

    /// Byte index of class in string table, or 0xFFFF.
    class_name: u16,

    /// Byte offset of the logical segment within physical segment.  If group is set in flags, this is the offset of the group.
    offset: u32,

    /// Byte count of the segment or group.
    section_length: u32,
};

pub const StreamType = enum(u16) {
    pdb = 1,
    tpi = 2,
    dbi = 3,
    ipi = 4,
};

/// Duplicate copy of SymbolRecordKind, but using the official CV names. Useful
/// for reference purposes and when dealing with unknown record types.
pub const SymbolKind = enum(u16) {
    compile = 1,
    register_16t = 2,
    constant_16t = 3,
    udt_16t = 4,
    ssearch = 5,
    skip = 7,
    cvreserve = 8,
    objname_st = 9,
    endarg = 10,
    coboludt_16t = 11,
    manyreg_16t = 12,
    @"return" = 13,
    entrythis = 14,
    bprel16 = 256,
    ldata16 = 257,
    gdata16 = 258,
    pub16 = 259,
    lproc16 = 260,
    gproc16 = 261,
    thunk16 = 262,
    block16 = 263,
    with16 = 264,
    label16 = 265,
    cexmodel16 = 266,
    vftable16 = 267,
    regrel16 = 268,
    bprel32_16t = 512,
    ldata32_16t = 513,
    gdata32_16t = 514,
    pub32_16t = 515,
    lproc32_16t = 516,
    gproc32_16t = 517,
    thunk32_st = 518,
    block32_st = 519,
    with32_st = 520,
    label32_st = 521,
    cexmodel32 = 522,
    vftable32_16t = 523,
    regrel32_16t = 524,
    lthread32_16t = 525,
    gthread32_16t = 526,
    slink32 = 527,
    lprocmips_16t = 768,
    gprocmips_16t = 769,
    procref_st = 1024,
    dataref_st = 1025,
    @"align" = 1026,
    lprocref_st = 1027,
    oem = 1028,
    ti16_max = 4096,
    register_st = 4097,
    constant_st = 4098,
    udt_st = 4099,
    coboludt_st = 4100,
    manyreg_st = 4101,
    bprel32_st = 4102,
    ldata32_st = 4103,
    gdata32_st = 4104,
    pub32_st = 4105,
    lproc32_st = 4106,
    gproc32_st = 4107,
    vftable32 = 4108,
    regrel32_st = 4109,
    lthread32_st = 4110,
    gthread32_st = 4111,
    lprocmips_st = 4112,
    gprocmips_st = 4113,
    compile2_st = 4115,
    manyreg2_st = 4116,
    lprocia64_st = 4117,
    gprocia64_st = 4118,
    localslot_st = 4119,
    paramslot_st = 4120,
    annotation = 4121,
    gmanproc_st = 4122,
    lmanproc_st = 4123,
    reserved1 = 4124,
    reserved2 = 4125,
    reserved3 = 4126,
    reserved4 = 4127,
    lmandata_st = 4128,
    gmandata_st = 4129,
    manframerel_st = 4130,
    manregister_st = 4131,
    manslot_st = 4132,
    manmanyreg_st = 4133,
    manregrel_st = 4134,
    manmanyreg2_st = 4135,
    mantypref = 4136,
    unamespace_st = 4137,
    st_max = 4352,
    with32 = 4356,
    manyreg = 4362,
    lprocmips = 4372,
    gprocmips = 4373,
    manyreg2 = 4375,
    lprocia64 = 4376,
    gprocia64 = 4377,
    localslot = 4378,
    paramslot = 4379,
    manframerel = 4382,
    manregister = 4383,
    manslot = 4384,
    manmanyreg = 4385,
    manregrel = 4386,
    manmanyreg2 = 4387,
    unamespace = 4388,
    dataref = 4390,
    annotationref = 4392,
    tokenref = 4393,
    gmanproc = 4394,
    lmanproc = 4395,
    attr_framerel = 4398,
    attr_register = 4399,
    attr_regrel = 4400,
    attr_manyreg = 4401,
    sepcode = 4402,
    local_2005 = 4403,
    defrange_2005 = 4404,
    defrange2_2005 = 4405,
    discarded = 4411,
    lprocmips_id = 4424,
    gprocmips_id = 4425,
    lprocia64_id = 4426,
    gprocia64_id = 4427,
    defrange_hlsl = 4432,
    gdata_hlsl = 4433,
    ldata_hlsl = 4434,
    local_dpc_groupshared = 4436,
    defrange_dpc_ptr_tag = 4439,
    dpc_sym_tag_map = 4440,
    armswitchtable = 4441,
    pogodata = 4444,
    inlinesite2 = 4445,
    mod_typeref = 4447,
    ref_minipdb = 4448,
    pdbmap = 4449,
    gdata_hlsl32 = 4450,
    ldata_hlsl32 = 4451,
    gdata_hlsl32_ex = 4452,
    ldata_hlsl32_ex = 4453,
    fastlink = 4455,
    inlinees = 4456,
    end = 6,
    inlinesite_end = 4430,
    proc_id_end = 4431,
    thunk32 = 4354,
    trampoline = 4396,
    section = 4406,
    coffgroup = 4407,
    @"export" = 4408,
    lproc32 = 4367,
    gproc32 = 4368,
    lproc32_id = 4422,
    gproc32_id = 4423,
    lproc32_dpc = 4437,
    lproc32_dpc_id = 4438,
    register = 4358,
    pub32 = 4366,
    procref = 4389,
    lprocref = 4391,
    envblock = 4413,
    inlinesite = 4429,
    local = 4414,
    defrange = 4415,
    defrange_subfield = 4416,
    defrange_register = 4417,
    defrange_framepointer_rel = 4418,
    defrange_subfield_register = 4419,
    defrange_framepointer_rel_full_scope = 4420,
    defrange_register_rel = 4421,
    block32 = 4355,
    label32 = 4357,
    objname = 4353,
    compile2 = 4374,
    compile3 = 4412,
    frameproc = 4114,
    callsiteinfo = 4409,
    filestatic = 4435,
    heapallocsite = 4446,
    framecookie = 4410,
    callees = 4442,
    callers = 4443,
    udt = 4360,
    coboludt = 4361,
    buildinfo = 4428,
    bprel32 = 4363,
    regrel32 = 4369,
    constant = 4359,
    manconstant = 4397,
    ldata32 = 4364,
    gdata32 = 4365,
    lmandata = 4380,
    gmandata = 4381,
    lthread32 = 4370,
    gthread32 = 4371,
};

pub const TypeIndex = u32;

pub const ProcSym = extern struct {
    record_len: u16,
    record_kind: SymbolKind,
    parent: u32,
    end: u32,
    next: u32,
    code_size: u32,
    dbg_start: u32,
    dbg_end: u32,
    function_type: TypeIndex,
    code_offset: u32,
    segment: u16,
    flags: ProcSymFlags,
    name: [1]u8, // null-terminated
};

pub const ProcSymFlags = packed struct(u8) {
    has_fp: bool,
    has_iret: bool,
    has_fret: bool,
    is_no_return: bool,
    is_unreachable: bool,
    has_custom_calling_conv: bool,
    is_no_inline: bool,
    has_optimized_debug_info: bool,
};

pub const SectionContrSubstreamVersion = enum(u32) {
    Ver60 = 0xeffe0000 + 19970605,
    V2 = 0xeffe0000 + 20140516,
    _,
};

pub const RecordPrefix = extern struct {
    /// Record length, starting from &record_kind.
    record_len: u16,

    /// Record kind enum (SymRecordKind or TypeRecordKind)
    record_kind: SymbolKind,
};

/// The following variable length array appears immediately after the header.
/// The structure definition follows.
/// LineBlockFragmentHeader Blocks[]
/// Each `LineBlockFragmentHeader` as specified below.
pub const LineFragmentHeader = extern struct {
    /// Code offset of line contribution.
    reloc_offset: u32,

    /// Code segment of line contribution.
    reloc_segment: u16,
    flags: LineFlags,

    /// Code size of this line contribution.
    code_size: u32,
};

pub const LineFlags = packed struct(u16) {
    /// CV_LINES_HAVE_COLUMNS
    have_columns: bool,
    unused: u15,
};

/// The following two variable length arrays appear immediately after the
/// header.  The structure definitions follow.
/// LineNumberEntry   Lines[NumLines];
/// ColumnNumberEntry Columns[NumLines];
pub const LineBlockFragmentHeader = extern struct {
    /// Offset of FileChecksum entry in File
    /// checksums buffer.  The checksum entry then
    /// contains another offset into the string
    /// table of the actual name.
    name_index: u32,
    num_lines: u32,

    /// code size of block, in bytes
    block_size: u32,
};

pub const LineNumberEntry = extern struct {
    /// Offset to start of code bytes for line number
    offset: u32,
    flags: Flags,

    pub const Flags = packed struct(u32) {
        /// Start line number
        start: u24,
        /// Delta of lines to the end of the expression. Still unclear.
        // TODO figure out the point of this field.
        end: u7,
        is_statement: bool,
    };
};

pub const ColumnNumberEntry = extern struct {
    start_column: u16,
    end_column: u16,
};

/// Checksum bytes follow.
pub const FileChecksumEntryHeader = extern struct {
    /// Byte offset of filename in global string table.
    file_name_offset: u32,
    /// Number of bytes of checksum.
    checksum_size: u8,
    /// FileChecksumKind
    checksum_kind: u8,
};

pub const DebugSubsectionKind = enum(u32) {
    none = 0,
    symbols = 0xf1,
    lines = 0xf2,
    string_table = 0xf3,
    file_checksums = 0xf4,
    frame_data = 0xf5,
    inlinee_lines = 0xf6,
    cross_scope_imports = 0xf7,
    cross_scope_exports = 0xf8,

    // These appear to relate to .Net assembly info.
    il_lines = 0xf9,
    func_md_token_map = 0xfa,
    type_md_token_map = 0xfb,
    merged_assembly_input = 0xfc,

    coff_symbol_rva = 0xfd,
};

pub const DebugSubsectionHeader = extern struct {
    /// codeview::DebugSubsectionKind enum
    kind: DebugSubsectionKind,

    /// number of bytes occupied by this record.
    length: u32,
};

pub const StringTableHeader = extern struct {
    /// PDBStringTableSignature
    signature: u32,
    /// 1 or 2
    hash_version: u32,
    /// Number of bytes of names buffer.
    byte_size: u32,
};

// https://llvm.org/docs/PDB/MsfFile.html#the-superblock
pub const SuperBlock = extern struct {
    /// The LLVM docs list a space between C / C++ but empirically this is not the case.
    pub const expect_magic = "Microsoft C/C++ MSF 7.00\r\n\x1a\x44\x53\x00\x00\x00";

    file_magic: [expect_magic.len]u8,

    /// The block size of the internal file system. Valid values are 512, 1024,
    /// 2048, and 4096 bytes. Certain aspects of the MSF file layout vary depending
    /// on the block sizes. For the purposes of LLVM, we handle only block sizes of
    /// 4KiB, and all further discussion assumes a block size of 4KiB.
    block_size: u32,

    /// The index of a block within the file, at which begins a bitfield representing
    /// the set of all blocks within the file which are “free” (i.e. the data within
    /// that block is not used). See The Free Block Map for more information. Important:
    /// FreeBlockMapBlock can only be 1 or 2!
    free_block_map_block: u32,

    /// The total number of blocks in the file. NumBlocks * BlockSize should equal the
    /// size of the file on disk.
    num_blocks: u32,

    /// The size of the stream directory, in bytes. The stream directory contains
    /// information about each stream’s size and the set of blocks that it occupies.
    /// It will be described in more detail later.
    num_directory_bytes: u32,

    unknown: u32,
    /// The index of a block within the MSF file. At this block is an array of
    /// ulittle32_t’s listing the blocks that the stream directory resides on.
    /// For large MSF files, the stream directory (which describes the block
    /// layout of each stream) may not fit entirely on a single block. As a
    /// result, this extra layer of indirection is introduced, whereby this
    /// block contains the list of blocks that the stream directory occupies,
    /// and the stream directory itself can be stitched together accordingly.
    /// The number of ulittle32_t’s in this array is given by
    /// ceil(NumDirectoryBytes / BlockSize).
    // Note: microsoft-pdb code actually suggests this is a variable-length
    // array. If the indices of blocks occupied by the Stream Directory didn't
    // fit in one page, there would be other u32 following it.
    // This would mean the Stream Directory is bigger than BlockSize / sizeof(u32)
    // blocks. We're not even close to this with a 1GB pdb file, and LLVM didn't
    // implement it so we're kind of safe making this assumption for now.
    block_map_addr: u32,
};

pub const IpiStreamVersion = enum(u32) {
    v40 = 19950410,
    v41 = 19951122,
    v50 = 19961031,
    v70 = 19990903,
    v80 = 20040203,
    _,
};

pub const IpiStreamHeader = extern struct {
    version: IpiStreamVersion,
    header_size: u32,
    type_index_begin: u32,
    type_index_end: u32,
    type_record_bytes: u32,
    hash_stream_index: u16,
    hash_aux_stream_index: u16,
    hash_key_size: u32,
    num_hash_buckets: u32,
    hash_value_buffer_offset: i32,
    hash_value_buffer_length: u32,
    index_offset_buffer_offset: i32,
    index_offset_buffer_length: u32,
    hash_adj_buffer_offset: i32,
    hash_adj_buffer_length: u32,
};

pub const LfRecordPrefix = extern struct {
    len: u16,
    kind: LfRecordKind,
};

pub const LfRecordKind = enum(u16) {
    pointer = 0x1002,
    modifier = 0x1001,
    procedure = 0x1008,
    mfunction = 0x1009,
    label = 0x000e,
    arglist = 0x1201,
    fieldlist = 0x1203,
    array = 0x1503,
    class = 0x1504,
    structure = 0x1505,
    interface = 0x1519,
    @"union" = 0x1506,
    @"enum" = 0x1507,
    typeserver2 = 0x1515,
    vftable = 0x151d,
    vtshape = 0x000a,
    bitfield = 0x1205,
    func_id = 0x1601,
    mfunc_id = 0x1602,
    buildinfo = 0x1603,
    substr_list = 0x1604,
    string_id = 0x1605,
    udt_src_line = 0x1606,
    udt_mod_src_line = 0x1607,
    methodlist = 0x1206,
    precomp = 0x1509,
    endprecomp = 0x0014,
    bclass = 0x1400,
    binterface = 0x151a,
    vbclass = 0x1401,
    ivbclass = 0x1402,
    vfunctab = 0x1409,
    stmember = 0x150e,
    method = 0x150f,
    member = 0x150d,
    nesttype = 0x1510,
    onemethod = 0x1511,
    enumerate = 0x1502,
    index = 0x1404,
    pad0 = 0xf0,
    _,
};

pub const LfFuncId = extern struct {
    len: u16,
    kind: LfRecordKind,
    scope_id: u32,
    type: u32,
    name: [1]u8, // null-terminated
};

pub const LfMFuncId = extern struct {
    len: u16,
    kind: LfRecordKind,
    parent_type: u32,
    type: u32,
    name: [1]u8, // null-terminated
};

pub const InlineSiteSym = extern struct {
    record_len: u16,
    record_kind: SymbolKind,
    parent: u32,
    end: u32,
    inlinee: u32,
};

pub const InlineSiteSym2 = extern struct {
    record_len: u16,
    record_kind: SymbolKind,
    parent: u32,
    end: u32,
    inlinee: u32,
    invocations: u32,
};

pub const InlineeSourceLineSignature = enum(u32) { normal = 0, ex = 1, _ };

pub const InlineeSourceLine = extern struct {
    inlinee: u32,
    file_id: u32,
    source_line_num: u32,
};

pub const InlineeSourceLineEx = extern struct {
    inlinee: u32,
    file_id: u32,
    source_line_num: u32,
    count_of_extra_files: u32,
};

pub const BinaryAnnotationOpcode = enum(u8) {
    invalid = 0,
    code_offset = 1,
    change_code_offset_base = 2,
    change_code_offset = 3,
    change_code_length = 4,
    change_file = 5,
    change_line_offset = 6,
    change_line_end_delta = 7,
    change_range_kind = 8,
    change_column_start = 9,
    change_column_end_delta = 10,
    change_code_offset_and_line_offset = 11,
    change_code_length_and_code_offset = 12,
    change_column_end = 13,
};



---
File: /std/pie.zig
---

const std = @import("std");
const builtin = @import("builtin");
const elf = std.elf;
const assert = std.debug.assert;

const R_ALPHA_RELATIVE = 27;
const R_AMD64_RELATIVE = 8;
const R_386_RELATIVE = 8;
const R_ARC_RELATIVE = 56;
const R_ARM_RELATIVE = 23;
const R_AARCH64_RELATIVE = 1027;
const R_CSKY_RELATIVE = 9;
const R_HEXAGON_RELATIVE = 35;
const R_KVX_RELATIVE = 39;
const R_LARCH_RELATIVE = 3;
const R_68K_RELATIVE = 22;
const R_MICROBLAZE_REL = 16;
const R_MIPS_RELATIVE = 128;
const R_OR1K_RELATIVE = 21;
const R_PPC_RELATIVE = 22;
const R_RISCV_RELATIVE = 3;
const R_390_RELATIVE = 12;
const R_SH_RELATIVE = 165;
const R_SPARC_RELATIVE = 22;

const R_RELATIVE = switch (builtin.cpu.arch) {
    .x86 => R_386_RELATIVE,
    .x86_64 => R_AMD64_RELATIVE,
    .arc, .arceb => R_ARC_RELATIVE,
    .arm, .armeb, .thumb, .thumbeb => R_ARM_RELATIVE,
    .aarch64, .aarch64_be => R_AARCH64_RELATIVE,
    .alpha => R_ALPHA_RELATIVE,
    .csky => R_CSKY_RELATIVE,
    .hexagon => R_HEXAGON_RELATIVE,
    .kvx => R_KVX_RELATIVE,
    .loongarch32, .loongarch64 => R_LARCH_RELATIVE,
    .m68k => R_68K_RELATIVE,
    .microblaze, .microblazeel => R_MICROBLAZE_REL,
    .mips, .mipsel, .mips64, .mips64el => R_MIPS_RELATIVE,
    .or1k => R_OR1K_RELATIVE,
    .powerpc, .powerpcle, .powerpc64, .powerpc64le => R_PPC_RELATIVE,
    .riscv32, .riscv32be, .riscv64, .riscv64be => R_RISCV_RELATIVE,
    .s390x => R_390_RELATIVE,
    .sh, .sheb => R_SH_RELATIVE,
    .sparc, .sparc64 => R_SPARC_RELATIVE,
    else => @compileError("Missing R_RELATIVE definition for this target"),
};

// Obtain a pointer to the _DYNAMIC array.
// We have to compute its address as a PC-relative quantity not to require a
// relocation that, at this point, is not yet applied.
inline fn getDynamicSymbol() [*]const elf.Dyn {
    return switch (builtin.zig_backend) {
        else => switch (builtin.cpu.arch) {
            .x86 => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ call 1f
                \\1:
                \\ pop %[ret]
                \\ lea _DYNAMIC - 1b(%[ret]), %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .x86_64 => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ lea _DYNAMIC(%%rip), %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .arc, .arceb => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ add %[ret], pcl, _DYNAMIC@pcl
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            // Work around the limited offset range of `ldr`
            .arm, .armeb, .thumb, .thumbeb => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ ldr %[ret], 1f
                \\ add %[ret], pc
                \\ b 2f
                \\1:
                \\ .word _DYNAMIC-1b
                \\2:
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            // A simple `adr` is not enough as it has a limited offset range
            .aarch64, .aarch64_be => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ adrp %[ret], _DYNAMIC
                \\ add %[ret], %[ret], #:lo12:_DYNAMIC
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            // The compiler is not required to load the GP register, so do it ourselves.
            .alpha => asm volatile (
                \\ br $29, 1f
                \\1:
                \\ ldgp $29, 0($29)
                \\ ldq %[ret], -0x8000($29)
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .r26 = true, .r29 = true }),
            // The CSKY ABI requires the gb register to point to the GOT. Additionally, the first
            // entry in the GOT is defined to hold the address of _DYNAMIC.
            .csky => asm volatile (
                \\ mov %[ret], gb
                \\ ldw %[ret], %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .hexagon => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ jump 1f
                \\ .word _DYNAMIC - .
                \\1:
                \\ r1 = pc
                \\ r1 = add(r1, #-4)
                \\ %[ret] = memw(r1)
                \\ %[ret] = add(r1, %[ret])
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .r1 = true }),
            .kvx => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ pcrel %[ret] = @pcrel(_DYNAMIC)
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .loongarch32, .loongarch64 => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ la.local %[ret], _DYNAMIC
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            // Note that the - 8 is needed because pc in the second lea instruction points into the
            // middle of that instruction. (The first lea is 6 bytes, the second is 4 bytes.)
            .m68k => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ lea _DYNAMIC - . - 8, %[ret]
                \\ lea (%[ret], %%pc), %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .microblaze, .microblazeel => asm volatile (
                \\ lwi %[ret], r20, 0
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .mips, .mipsel => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ bal 1f
                \\ .gpword _DYNAMIC
                \\1:
                \\ lw %[ret], 0($ra)
                \\ nop
                \\ addu %[ret], %[ret], $gp
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .lr = true }),
            .mips64, .mips64el => switch (builtin.abi) {
                .gnuabin32, .muslabin32 => asm volatile (
                    \\ .weak _DYNAMIC
                    \\ .hidden _DYNAMIC
                    \\ bal 1f
                    \\ .gpword _DYNAMIC
                    \\1:
                    \\ lw %[ret], 0($ra)
                    \\ addu %[ret], %[ret], $gp
                    : [ret] "=r" (-> [*]const elf.Dyn),
                    :
                    : .{ .lr = true }),
                else => asm volatile (
                    \\ .weak _DYNAMIC
                    \\ .hidden _DYNAMIC
                    \\ .balign 8
                    \\ bal 1f
                    \\ .gpdword _DYNAMIC
                    \\1:
                    \\ ld %[ret], 0($ra)
                    \\ daddu %[ret], %[ret], $gp
                    : [ret] "=r" (-> [*]const elf.Dyn),
                    :
                    : .{ .lr = true }),
            },
            .or1k => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ l.jal 1f
                \\ .word _DYNAMIC - .
                \\1:
                \\ l.lwz %[ret], 0(r9)
                \\ l.add %[ret], %[ret], r9
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .r9 = true }),
            .powerpc, .powerpcle => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ bl 1f
                \\ .long _DYNAMIC - .
                \\1:
                \\ mflr %[ret]
                \\ lwz 4, 0(%[ret])
                \\ add %[ret], 4, %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .lr = true, .r4 = true }),
            .powerpc64, .powerpc64le => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ bl 1f
                \\ .quad _DYNAMIC - .
                \\1:
                \\ mflr %[ret]
                \\ ld 4, 0(%[ret])
                \\ add %[ret], 4, %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .lr = true, .r4 = true }),
            .riscv32, .riscv32be, .riscv64, .riscv64be => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ lla %[ret], _DYNAMIC
                : [ret] "=r" (-> [*]const elf.Dyn),
            ),
            .s390x => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ larl %[ret], 1f
                \\ ag %[ret], 0(%[ret])
                \\ jg 2f
                \\1:
                \\ .quad _DYNAMIC - .
                \\2:
                : [ret] "=a" (-> [*]const elf.Dyn),
            ),
            .sh, .sheb => asm volatile (
                \\ .weak _DYNAMIC
                \\ .hidden _DYNAMIC
                \\ mova 1f, r0
                \\ mov.l 1f, %[ret]
                \\ add r0, %[ret]
                \\ bra 2f
                \\1:
                \\ .balign 4
                \\ .long DYNAMIC - .
                \\2:
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .r0 = true }),
            // The compiler does not necessarily have any obligation to load the `l7` register (pointing
            // to the GOT), so do it ourselves just in case.
            .sparc, .sparc64 => asm volatile (
                \\ sethi %%hi(_GLOBAL_OFFSET_TABLE_ - 4), %%l7
                \\ call 1f
                \\  add %%l7, %%lo(_GLOBAL_OFFSET_TABLE_ + 4), %%l7
                \\1:
                \\ add %%l7, %%o7, %[ret]
                : [ret] "=r" (-> [*]const elf.Dyn),
                :
                : .{ .l7 = true }),
            else => {
                @compileError("PIE startup is not yet supported for this target!");
            },
        },
        .stage2_x86_64 => @extern([*]const elf.Dyn, .{
            .name = "_DYNAMIC",
            .linkage = .weak,
            .visibility = .hidden,
            .relocation = .pcrel,
        }).?,
    };
}

pub fn relocate(phdrs: []const elf.Phdr) void {
    @setRuntimeSafety(false);
    @disableInstrumentation();

    const dynv = getDynamicSymbol();

    // Recover the delta applied by the loader by comparing the effective and
    // the theoretical load addresses for the `_DYNAMIC` symbol.
    const base_addr = base: {
        for (phdrs) |*phdr| {
            if (phdr.p_type != elf.PT_DYNAMIC) continue;
            break :base @intFromPtr(dynv) - phdr.p_vaddr;
        }
        // This is not supposed to happen for well-formed binaries.
        @trap();
    };

    var sorted_dynv: [elf.DT_NUM]elf.Addr = undefined;

    // Zero-initialized this way to prevent the compiler from turning this into
    // `memcpy` or `memset` calls (which can require relocations).
    for (&sorted_dynv) |*dyn| {
        const pdyn: *volatile elf.Addr = @ptrCast(dyn);
        pdyn.* = 0;
    }

    {
        // `dynv` has no defined order. Fix that.
        var i: usize = 0;
        while (dynv[i].d_tag != elf.DT_NULL) : (i += 1) {
            if (dynv[i].d_tag < elf.DT_NUM) sorted_dynv[@bitCast(dynv[i].d_tag)] = dynv[i].d_val;
        }
    }

    // Deal with the GOT relocations that MIPS uses first.
    if (builtin.cpu.arch.isMIPS()) {
        const count: elf.Addr = blk: {
            // This is an architecture-specific tag, so not part of `sorted_dynv`.
            var i: usize = 0;
            while (dynv[i].d_tag != elf.DT_NULL) : (i += 1) {
                if (dynv[i].d_tag == elf.DT_MIPS_LOCAL_GOTNO) break :blk dynv[i].d_val;
            }

            break :blk 0;
        };

        const got: [*]usize = @ptrFromInt(base_addr + sorted_dynv[elf.DT_PLTGOT]);

        for (0..count) |i| {
            got[i] += base_addr;
        }
    }

    // Apply normal relocations.

    const rel = sorted_dynv[elf.DT_REL];
    if (rel != 0) {
        const rels: []const elf.Rel = @ptrCast(@alignCast(
            @as([*]align(@alignOf(elf.Rel)) const u8, @ptrFromInt(base_addr + rel))[0..sorted_dynv[elf.DT_RELSZ]],
        ));
        for (rels) |r| {
            if (r.r_type() != R_RELATIVE) continue;
            @as(*usize, @ptrFromInt(base_addr + r.r_offset)).* += base_addr;
        }
    }

    const rela = sorted_dynv[elf.DT_RELA];
    if (rela != 0) {
        const relas: []const elf.Rela = @ptrCast(@alignCast(
            @as([*]align(@alignOf(elf.Rela)) const u8, @ptrFromInt(base_addr + rela))[0..sorted_dynv[elf.DT_RELASZ]],
        ));
        for (relas) |r| {
            if (r.r_type() != R_RELATIVE) continue;
            @as(*usize, @ptrFromInt(base_addr + r.r_offset)).* = base_addr + @as(usize, @bitCast(r.r_addend));
        }
    }

    const relr = sorted_dynv[elf.DT_RELR];
    if (relr != 0) {
        const relrs: []const elf.Relr = @ptrCast(
            @as([*]align(@alignOf(elf.Relr)) const u8, @ptrFromInt(base_addr + relr))[0..sorted_dynv[elf.DT_RELRSZ]],
        );
        var current: [*]usize = undefined;
        for (relrs) |r| {
            if ((r & 1) == 0) {
                current = @ptrFromInt(base_addr + r);
                current[0] += base_addr;
                current += 1;
            } else {
                // Skip the first bit; there are 63 locations in the bitmap.
                var i: if (@sizeOf(usize) == 8) u6 else u5 = 1;
                while (i < @bitSizeOf(elf.Relr)) : (i += 1) {
                    if (((r >> i) & 1) != 0) current[i] += base_addr;
                }

                current += @bitSizeOf(elf.Relr) - 1;
            }
        }
    }
}



---
File: /std/posix.zig
---

//! POSIX API layer.
//!
//! This is more cross platform than using OS-specific APIs, however, it is
//! lower-level and less portable than other namespaces such as `std.Io` and
//! `std.process`.
//!
//! These APIs are generally lowered to libc function calls if and only if libc
//! is linked. Most operating systems other than Windows, Linux, and WASI
//! require always linking libc because they use it as the stable syscall ABI.
const builtin = @import("builtin");
const native_os = builtin.os.tag;

const std = @import("std.zig");
const Io = std.Io;
const mem = std.mem;
const maxInt = std.math.maxInt;
const cast = std.math.cast;
const assert = std.debug.assert;
const page_size_min = std.heap.page_size_min;

test {
    _ = @import("posix/test.zig");
}

/// Whether to use libc for the POSIX API layer.
const use_libc = builtin.link_libc or switch (native_os) {
    .windows, .wasi => true,
    else => false,
};

const linux = std.os.linux;
const windows = std.os.windows;
const wasi = std.os.wasi;

/// A libc-compatible API layer.
pub const system = if (use_libc)
    std.c
else switch (native_os) {
    .linux => linux,
    .plan9 => std.os.plan9,
    .psp => struct {
        pub const fd_t = i32;
        pub const pid_t = void;
        pub const pollfd = void;
        pub const uid_t = void;
        pub const gid_t = void;
        pub const mode_t = u32;
        pub const nlink_t = u32;
        pub const blksize_t = u32;
        pub const ino_t = u64;
        pub const IFNAMESIZE = {};
        pub const SIG = void;

        // https://github.com/pspdev/newlib/blob/9e0a073634ad73e8e088f2e071c55a9fe5d39709/newlib/libc/sys/psp/sys/dirent.h#L19
        pub const NAME_MAX = 255;
    },
    else => struct {
        pub const pid_t = void;
        pub const pollfd = void;
        pub const fd_t = void;
        pub const uid_t = void;
        pub const gid_t = void;
        pub const mode_t = u0;
        pub const nlink_t = u0;
        pub const blksize_t = void;
        pub const ino_t = void;
        pub const IFNAMESIZE = {};
        pub const SIG = void;
    },
};

pub const AF = system.AF;
pub const AF_SUN = system.AF_SUN;
pub const AI = system.AI;
pub const ARCH = system.ARCH;
pub const AT = system.AT;
pub const AT_SUN = system.AT_SUN;
pub const CLOCK = system.CLOCK;
pub const CPU_COUNT = system.CPU_COUNT;
pub const CTL = system.CTL;
pub const DT = system.DT;
pub const E = system.E;
pub const Elf_Symndx = system.Elf_Symndx;
pub const F = system.F;
pub const FD_CLOEXEC = system.FD_CLOEXEC;
pub const Flock = system.Flock;
pub const HOST_NAME_MAX = system.HOST_NAME_MAX;
pub const HW = system.HW;
pub const IFNAMESIZE = system.IFNAMESIZE;
pub const IOV_MAX = system.IOV_MAX;
pub const IP = system.IP;
pub const IPV6 = system.IPV6;
pub const IPPROTO = system.IPPROTO;
pub const IPTOS = system.IPTOS;
pub const KERN = system.KERN;
pub const Kevent = system.Kevent;
pub const MADV = system.MADV;
pub const MAP = system.MAP;
pub const MAX_ADDR_LEN = system.MAX_ADDR_LEN;
pub const MCL = system.MCL;
pub const MFD = system.MFD;
pub const MLOCK = system.MLOCK;
pub const MREMAP = system.MREMAP;
pub const MSF = system.MSF;
pub const MSG = system.MSG;
pub const NAME_MAX = system.NAME_MAX;
pub const NSIG = system.NSIG;
pub const O = system.O;
pub const PATH_MAX = system.PATH_MAX;
pub const POLL = system.POLL;
pub const POSIX_FADV = system.POSIX_FADV;
pub const PR = system.PR;
pub const PROT = system.PROT;
pub const RLIM = system.RLIM;
pub const S = system.S;
pub const SA = system.SA;
pub const SC = system.SC;
pub const SCM = system.SCM;
pub const SEEK = system.SEEK;
pub const SHUT = system.SHUT;
pub const SIG = system.SIG;
pub const SIOCGIFINDEX = system.SIOCGIFINDEX;
pub const SO = system.SO;
pub const SOCK = system.SOCK;
pub const SOL = system.SOL;
pub const IFF = system.IFF;
pub const STDERR_FILENO = system.STDERR_FILENO;
pub const STDIN_FILENO = system.STDIN_FILENO;
pub const STDOUT_FILENO = system.STDOUT_FILENO;
pub const SYS = system.SYS;
pub const Sigaction = system.Sigaction;
/// Windows has no concept of `stat`.
///
/// On Linux, the `stat` bits/wrappers are removed due to having to maintain
/// the different varying stat structs per target and libc, leading to runtime
/// errors. Users targeting Linux should add a comptime check and use statx,
/// similar to how `Io.File.stat` does.
pub const Stat = switch (native_os) {
    .windows => void,
    .linux => void,
    else => system.Stat,
};
pub const T = system.T;
pub const TCP = system.TCP;
pub const VDSO = system.VDSO;
pub const W = system.W;
pub const _SC = system._SC;
pub const addrinfo = system.addrinfo;
pub const blkcnt_t = system.blkcnt_t;
pub const blksize_t = system.blksize_t;
pub const clock_t = system.clock_t;
pub const clockid_t = system.clockid_t;
pub const timerfd_clockid_t = system.timerfd_clockid_t;
pub const cpu_set_t = system.cpu_set_t;
pub const dev_t = system.dev_t;
pub const dl_phdr_info = system.dl_phdr_info;
pub const fd_t = system.fd_t;
pub const file_obj = system.file_obj;
pub const gid_t = system.gid_t;
pub const ifreq = system.ifreq;
pub const in_pktinfo = system.in_pktinfo;
pub const in6_pktinfo = system.in6_pktinfo;
pub const ino_t = system.ino_t;
pub const linger = system.linger;
pub const mode_t = system.mode_t;
pub const msghdr = system.msghdr;
pub const msghdr_const = system.msghdr_const;
pub const nfds_t = system.nfds_t;
pub const nlink_t = system.nlink_t;
pub const off_t = system.off_t;
pub const pid_t = system.pid_t;
pub const pollfd = system.pollfd;
pub const port_event = system.port_event;
pub const port_notify = system.port_notify;
pub const port_t = system.port_t;
pub const rlim_t = system.rlim_t;
pub const rlimit = system.rlimit;
pub const rlimit_resource = system.rlimit_resource;
pub const rusage = system.rusage;
pub const sa_family_t = system.sa_family_t;
pub const siginfo_t = system.siginfo_t;
pub const sigset_t = system.sigset_t;
pub const sigrtmin = system.sigrtmin;
pub const sigrtmax = system.sigrtmax;
pub const sockaddr = system.sockaddr;
pub const socklen_t = system.socklen_t;
pub const stack_t = system.stack_t;
pub const time_t = system.time_t;
pub const timespec = system.timespec;
pub const timestamp_t = system.timestamp_t;
pub const timeval = system.timeval;
pub const timezone = system.timezone;
pub const UTIME = system.UTIME;
pub const uid_t = system.uid_t;
pub const user_desc = system.user_desc;
pub const utsname = system.utsname;

pub const termios = system.termios;
pub const CSIZE = system.CSIZE;
pub const NCCS = system.NCCS;
pub const cc_t = system.cc_t;
pub const V = system.V;
pub const speed_t = system.speed_t;
pub const tc_iflag_t = system.tc_iflag_t;
pub const tc_oflag_t = system.tc_oflag_t;
pub const tc_cflag_t = system.tc_cflag_t;
pub const tc_lflag_t = system.tc_lflag_t;

pub const F_OK = system.F_OK;
pub const R_OK = system.R_OK;
pub const W_OK = system.W_OK;
pub const X_OK = system.X_OK;

pub const iovec = extern struct {
    base: [*]u8,
    len: usize,
};

pub const iovec_const = extern struct {
    base: [*]const u8,
    len: usize,
};

pub const ACCMODE = switch (native_os) {
    // POSIX has a note about the access mode values:
    //
    // In historical implementations the value of O_RDONLY is zero. Because of
    // that, it is not possible to detect the presence of O_RDONLY and another
    // option. Future implementations should encode O_RDONLY and O_WRONLY as
    // bit flags so that: O_RDONLY | O_WRONLY == O_RDWR
    //
    // In practice SerenityOS is the only system supported by Zig that
    // implements this suggestion.
    // https://github.com/SerenityOS/serenity/blob/4adc51fdf6af7d50679c48b39362e062f5a3b2cb/Kernel/API/POSIX/fcntl.h#L28-L30
    .serenity => enum(u2) {
        NONE = 0,
        RDONLY = 1,
        WRONLY = 2,
        RDWR = 3,
    },
    else => enum(u2) {
        RDONLY = 0,
        WRONLY = 1,
        RDWR = 2,
    },
};

pub const TCSA = enum(c_uint) {
    NOW,
    DRAIN,
    FLUSH,
    _,
};

pub const winsize = extern struct {
    row: u16,
    col: u16,
    xpixel: u16,
    ypixel: u16,
};

pub const LOCK = struct {
    pub const SH = 1;
    pub const EX = 2;
    pub const NB = 4;
    pub const UN = 8;
};

pub const LOG = struct {
    /// system is unusable
    pub const EMERG = 0;
    /// action must be taken immediately
    pub const ALERT = 1;
    /// critical conditions
    pub const CRIT = 2;
    /// error conditions
    pub const ERR = 3;
    /// warning conditions
    pub const WARNING = 4;
    /// normal but significant condition
    pub const NOTICE = 5;
    /// informational
    pub const INFO = 6;
    /// debug-level messages
    pub const DEBUG = 7;
};

pub const socket_t = fd_t;

/// Obtains errno from the return value of a system function call.
///
/// For some systems this will obtain the value directly from the syscall return value;
/// for others it will use a thread-local errno variable. Therefore, this
/// function only returns a well-defined value when it is called directly after
/// the system function call whose errno value is intended to be observed.
pub const errno = system.errno;

pub const RebootError = error{
    PermissionDenied,
} || UnexpectedError;

pub const RebootCommand = switch (native_os) {
    .linux => union(linux.LINUX_REBOOT.CMD) {
        RESTART: void,
        HALT: void,
        CAD_ON: void,
        CAD_OFF: void,
        POWER_OFF: void,
        RESTART2: [*:0]const u8,
        SW_SUSPEND: void,
        KEXEC: void,
    },
    else => @compileError("Unsupported OS"),
};

pub fn reboot(cmd: RebootCommand) RebootError!void {
    switch (native_os) {
        .linux => {
            switch (linux.errno(linux.reboot(
                .MAGIC1,
                .MAGIC2,
                cmd,
                switch (cmd) {
                    .RESTART2 => |s| s,
                    else => null,
                },
            ))) {
                .SUCCESS => {},
                .PERM => return error.PermissionDenied,
                else => |err| return std.posix.unexpectedErrno(err),
            }
            switch (cmd) {
                .CAD_OFF => {},
                .CAD_ON => {},
                .SW_SUSPEND => {},

                .HALT => unreachable,
                .KEXEC => unreachable,
                .POWER_OFF => unreachable,
                .RESTART => unreachable,
                .RESTART2 => unreachable,
            }
        },
        else => @compileError("Unsupported OS"),
    }
}

pub const RaiseError = UnexpectedError;

pub fn raise(sig: SIG) RaiseError!void {
    if (builtin.link_libc) {
        switch (errno(system.raise(sig))) {
            .SUCCESS => return,
            else => |err| return unexpectedErrno(err),
        }
    }

    if (native_os == .linux) {
        // Block all signals so a `fork` (from a signal handler) between the gettid() and kill() syscalls
        // cannot trigger an extra, unexpected, inter-process signal.  Signal paranoia inherited from Musl.
        const filled = linux.sigfillset();
        var orig: sigset_t = undefined;
        sigprocmask(SIG.BLOCK, &filled, &orig);
        const rc = linux.tkill(linux.gettid(), sig);
        sigprocmask(SIG.SETMASK, &orig, null);

        switch (errno(rc)) {
            .SUCCESS => return,
            else => |err| return unexpectedErrno(err),
        }
    }

    @compileError("std.posix.raise unimplemented for this target");
}

pub const KillError = error{ ProcessNotFound, PermissionDenied } || UnexpectedError;

pub fn kill(pid: pid_t, sig: SIG) KillError!void {
    switch (errno(system.kill(pid, sig))) {
        .SUCCESS => return,
        .INVAL => unreachable, // invalid signal
        .PERM => return error.PermissionDenied,
        .SRCH => return error.ProcessNotFound,
        else => |err| return unexpectedErrno(err),
    }
}

pub const ReadError = std.Io.File.Reader.Error;

/// Returns the number of bytes that were read, which can be less than
/// buf.len. If 0 bytes were read, that means EOF.
/// If `fd` is opened in non blocking mode, the function will return error.WouldBlock
/// when EAGAIN is received.
///
/// Linux has a limit on how many bytes may be transferred in one `read` call, which is `0x7ffff000`
/// on both 64-bit and 32-bit systems. This is due to using a signed C int as the return value, as
/// well as stuffing the errno codes into the last `4096` values. This is noted on the `read` man page.
/// The limit on Darwin is `0x7fffffff`, trying to read more than that returns EINVAL.
/// The corresponding POSIX limit is `maxInt(isize)`.
pub fn read(fd: fd_t, buf: []u8) ReadError!usize {
    if (buf.len == 0) return 0;
    if (native_os == .windows) @compileError("unsupported OS");
    if (native_os == .wasi) @compileError("unsupported OS");

    // Prevents EINVAL.
    const max_count = switch (native_os) {
        .linux => 0x7ffff000,
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => maxInt(i32),
        else => maxInt(isize),
    };
    while (true) {
        const rc = system.read(fd, buf.ptr, @min(buf.len, max_count));
        switch (errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .INTR => continue,
            .INVAL => unreachable,
            .FAULT => unreachable,
            .AGAIN => return error.WouldBlock,
            .CANCELED => return error.Canceled,
            .BADF => return error.Unexpected, // use after free
            .IO => return error.InputOutput,
            .ISDIR => return error.IsDir,
            .NOBUFS => return error.SystemResources,
            .NOMEM => return error.SystemResources,
            .NOTCONN => return error.SocketUnconnected,
            .CONNRESET => return error.ConnectionResetByPeer,
            .TIMEDOUT => return error.Unexpected,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub const OpenError = std.Io.File.OpenError || error{WouldBlock};

/// Open and possibly create a file. Keeps trying if it gets interrupted.
/// `file_path` is relative to the open directory handle `dir_fd`.
/// On Windows, `file_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `file_path` should be encoded as valid UTF-8.
/// On other platforms, `file_path` is an opaque sequence of bytes with no particular encoding.
/// See also `openatZ`.
pub fn openat(dir_fd: fd_t, file_path: []const u8, flags: O, mode: mode_t) OpenError!fd_t {
    if (native_os == .windows) {
        @compileError("Windows does not support POSIX; use Windows-specific API or cross-platform std.fs API");
    } else if (native_os == .wasi and !builtin.link_libc) {
        @compileError("use std.Io instead");
    }
    const file_path_c = try toPosixPath(file_path);
    return openatZ(dir_fd, &file_path_c, flags, mode);
}

/// Open and possibly create a file. Keeps trying if it gets interrupted.
/// `file_path` is relative to the open directory handle `dir_fd`.
/// On Windows, `file_path` should be encoded as [WTF-8](https://wtf-8.codeberg.page/).
/// On WASI, `file_path` should be encoded as valid UTF-8.
/// On other platforms, `file_path` is an opaque sequence of bytes with no particular encoding.
/// See also `openat`.
pub fn openatZ(dir_fd: fd_t, file_path: [*:0]const u8, flags: O, mode: mode_t) OpenError!fd_t {
    if (native_os == .windows) {
        @compileError("Windows does not support POSIX; use Windows-specific API or cross-platform std.fs API");
    } else if (native_os == .wasi and !builtin.link_libc) {
        return openat(dir_fd, mem.sliceTo(file_path, 0), flags, mode);
    }

    const openat_sym = if (lfs64_abi) system.openat64 else system.openat;
    while (true) {
        const rc = openat_sym(dir_fd, file_path, flags, mode);
        switch (errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .INTR => continue,

            .FAULT => unreachable,
            .INVAL => return error.BadPathName,
            .BADF => unreachable,
            .ACCES => return error.AccessDenied,
            .FBIG => return error.FileTooBig,
            .OVERFLOW => return error.FileTooBig,
            .ISDIR => return error.IsDir,
            .LOOP => return error.SymLinkLoop,
            .MFILE => return error.ProcessFdQuotaExceeded,
            .NAMETOOLONG => return error.NameTooLong,
            .NFILE => return error.SystemFdQuotaExceeded,
            .NODEV => return error.NoDevice,
            .NOENT => return error.FileNotFound,
            .SRCH => return error.FileNotFound,
            .NOMEM => return error.SystemResources,
            .NOSPC => return error.NoSpaceLeft,
            .NOTDIR => return error.NotDir,
            .PERM => return error.PermissionDenied,
            .EXIST => return error.PathAlreadyExists,
            .BUSY => return error.DeviceBusy,
            .OPNOTSUPP => return error.FileLocksUnsupported,
            .AGAIN => return error.WouldBlock,
            .TXTBSY => return error.FileBusy,
            .NXIO => return error.NoDevice,
            .ILSEQ => return error.BadPathName,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub fn getppid() pid_t {
    return system.getppid();
}

pub const GetSockNameError = error{
    /// Insufficient resources were available in the system to perform the operation.
    SystemResources,

    /// The network subsystem has failed.
    NetworkDown,

    /// Socket hasn't been bound yet
    SocketNotBound,

    FileDescriptorNotASocket,

    /// The socket is not connected (connection-oriented sockets only).
    SocketUnconnected,
} || UnexpectedError;

pub fn getpeername(sock: socket_t, addr: *sockaddr, addrlen: *socklen_t) GetSockNameError!void {
    if (native_os == .windows) {
        @compileError("use std.Io instead");
    } else {
        const rc = system.getpeername(sock, addr, addrlen);
        switch (errno(rc)) {
            .SUCCESS => return,
            else => |err| return unexpectedErrno(err),

            .BADF => unreachable, // always a race condition
            .FAULT => unreachable,
            .INVAL => unreachable, // invalid parameters
            .NOTSOCK => return error.FileDescriptorNotASocket,
            .NOBUFS => return error.SystemResources,
            .NOTCONN => return error.SocketUnconnected,
        }
    }
}

pub const FanotifyInitError = error{
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    SystemResources,
    PermissionDenied,
    /// The kernel does not recognize the flags passed, likely because it is an
    /// older version.
    UnsupportedFlags,
} || UnexpectedError;

pub fn fanotify_init(flags: std.os.linux.fanotify.InitFlags, event_f_flags: u32) FanotifyInitError!i32 {
    const rc = system.fanotify_init(flags, event_f_flags);
    switch (errno(rc)) {
        .SUCCESS => return @intCast(rc),
        .INVAL => return error.UnsupportedFlags,
        .MFILE => return error.ProcessFdQuotaExceeded,
        .NFILE => return error.SystemFdQuotaExceeded,
        .NOMEM => return error.SystemResources,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

pub const FanotifyMarkError = error{
    MarkAlreadyExists,
    IsDir,
    NotAssociatedWithFileSystem,
    FileNotFound,
    SystemResources,
    UserMarkQuotaExceeded,
    NotDir,
    OperationUnsupported,
    PermissionDenied,
    CrossDevice,
    NameTooLong,
} || UnexpectedError;

pub fn fanotify_mark(
    fanotify_fd: fd_t,
    flags: std.os.linux.fanotify.MarkFlags,
    mask: std.os.linux.fanotify.MarkMask,
    dirfd: fd_t,
    pathname: ?[]const u8,
) FanotifyMarkError!void {
    if (pathname) |path| {
        const path_c = try toPosixPath(path);
        return fanotify_markZ(fanotify_fd, flags, mask, dirfd, &path_c);
    } else {
        return fanotify_markZ(fanotify_fd, flags, mask, dirfd, null);
    }
}

pub fn fanotify_markZ(
    fanotify_fd: fd_t,
    flags: std.os.linux.fanotify.MarkFlags,
    mask: std.os.linux.fanotify.MarkMask,
    dirfd: fd_t,
    pathname: ?[*:0]const u8,
) FanotifyMarkError!void {
    const rc = system.fanotify_mark(fanotify_fd, flags, mask, dirfd, pathname);
    switch (errno(rc)) {
        .SUCCESS => return,
        .BADF => unreachable,
        .EXIST => return error.MarkAlreadyExists,
        .INVAL => unreachable,
        .ISDIR => return error.IsDir,
        .NODEV => return error.NotAssociatedWithFileSystem,
        .NOENT => return error.FileNotFound,
        .NOMEM => return error.SystemResources,
        .NOSPC => return error.UserMarkQuotaExceeded,
        .NOTDIR => return error.NotDir,
        .OPNOTSUPP => return error.OperationUnsupported,
        .PERM => return error.PermissionDenied,
        .XDEV => return error.CrossDevice,
        else => |err| return unexpectedErrno(err),
    }
}

pub const MMapError = error{
    /// The underlying filesystem of the specified file does not support memory mapping.
    MemoryMappingNotSupported,
    /// A file descriptor refers to a non-regular file. Or a file mapping was requested,
    /// but the file descriptor is not open for reading. Or `MAP.SHARED` was requested
    /// and `PROT_WRITE` is set, but the file descriptor is not open in `RDWR` mode.
    /// Or `PROT_WRITE` is set, but the file is append-only.
    AccessDenied,
    /// The `prot` argument asks for `PROT_EXEC` but the mapped area belongs to a file on
    /// a filesystem that was mounted no-exec.
    PermissionDenied,
    LockedMemoryLimitExceeded,
    ProcessFdQuotaExceeded,
    SystemFdQuotaExceeded,
    OutOfMemory,
    /// Using FIXED_NOREPLACE flag and the process has already mapped memory at the given address
    MappingAlreadyExists,
} || UnexpectedError;

/// Map files or devices into memory.
/// `length` does not need to be aligned.
/// Use of a mapped region can result in these signals:
/// * SIGSEGV - Attempted write into a region mapped as read-only.
/// * SIGBUS - Attempted  access to a portion of the buffer that does not correspond to the file
pub fn mmap(
    ptr: ?[*]align(page_size_min) u8,
    length: usize,
    prot: PROT,
    flags: MAP,
    fd: fd_t,
    offset: u64,
) MMapError![]align(page_size_min) u8 {
    const mmap_sym = if (lfs64_abi) system.mmap64 else system.mmap;
    const rc = mmap_sym(ptr, length, prot, @bitCast(flags), fd, @bitCast(offset));
    const err: E = if (builtin.link_libc) blk: {
        if (rc != std.c.MAP_FAILED) return @as([*]align(page_size_min) u8, @ptrCast(@alignCast(rc)))[0..length];
        break :blk @enumFromInt(system._errno().*);
    } else blk: {
        const err = errno(rc);
        if (err == .SUCCESS) return @as([*]align(page_size_min) u8, @ptrFromInt(rc))[0..length];
        break :blk err;
    };
    switch (err) {
        .SUCCESS => unreachable,
        .TXTBSY => return error.AccessDenied,
        .ACCES => return error.AccessDenied,
        .PERM => return error.PermissionDenied,
        .AGAIN => return error.LockedMemoryLimitExceeded,
        .BADF => unreachable, // Always a race condition.
        .OVERFLOW => unreachable, // The number of pages used for length + offset would overflow.
        .NODEV => return error.MemoryMappingNotSupported,
        .INVAL => unreachable, // Invalid parameters to mmap()
        .MFILE => return error.ProcessFdQuotaExceeded,
        .NFILE => return error.SystemFdQuotaExceeded,
        .NOMEM => return error.OutOfMemory,
        .EXIST => return error.MappingAlreadyExists,
        else => return unexpectedErrno(err),
    }
}

/// Deletes the mappings for the specified address range, causing
/// further references to addresses within the range to generate invalid memory references.
/// Note that while POSIX allows unmapping a region in the middle of an existing mapping,
/// Zig's munmap function does not, for two reasons:
/// * It violates the Zig principle that resource deallocation must succeed.
/// * The Windows function, NtFreeVirtualMemory, has this restriction.
pub fn munmap(memory: []align(page_size_min) const u8) void {
    switch (errno(system.munmap(memory.ptr, memory.len))) {
        .SUCCESS => return,
        .INVAL => unreachable, // Invalid parameters.
        .NOMEM => unreachable, // Attempted to unmap a region in the middle of an existing mapping.
        else => |e| if (std.options.unexpected_error_tracing) {
            std.debug.panic("unexpected errno: {d} ({t})", .{ @intFromEnum(e), e });
        } else unreachable,
    }
}

pub const MRemapError = error{
    LockedMemoryLimitExceeded,
    /// Either a bug in the calling code, or the operating system abused the
    /// EINVAL error code.
    InvalidSyscallParameters,
    OutOfMemory,
} || UnexpectedError;

pub fn mremap(
    old_address: ?[*]align(page_size_min) u8,
    old_len: usize,
    new_len: usize,
    flags: system.MREMAP,
    new_address: ?[*]align(page_size_min) u8,
) MRemapError![]align(page_size_min) u8 {
    const rc = system.mremap(old_address, old_len, new_len, flags, new_address);
    const err: E = if (builtin.link_libc) blk: {
        if (rc != std.c.MAP_FAILED) return @as([*]align(page_size_min) u8, @ptrCast(@alignCast(rc)))[0..new_len];
        break :blk @enumFromInt(system._errno().*);
    } else blk: {
        const err = errno(rc);
        if (err == .SUCCESS) return @as([*]align(page_size_min) u8, @ptrFromInt(rc))[0..new_len];
        break :blk err;
    };
    switch (err) {
        .SUCCESS => unreachable,
        .AGAIN => return error.LockedMemoryLimitExceeded,
        .INVAL => return error.InvalidSyscallParameters,
        .NOMEM => return error.OutOfMemory,
        .FAULT => unreachable,
        else => return unexpectedErrno(err),
    }
}

pub const MSyncError = error{
    UnmappedMemory,
    PermissionDenied,
} || UnexpectedError;

pub fn msync(memory: []align(page_size_min) u8, flags: i32) MSyncError!void {
    switch (errno(system.msync(memory.ptr, memory.len, flags))) {
        .SUCCESS => return,
        .PERM => return error.PermissionDenied,
        .NOMEM => return error.UnmappedMemory, // Unsuccessful, provided pointer does not point mapped memory
        .INVAL => unreachable, // Invalid parameters.
        else => unreachable,
    }
}

pub const SysCtlError = error{
    PermissionDenied,
    SystemResources,
    NameTooLong,
    UnknownName,
} || UnexpectedError;

pub fn sysctl(
    name: []const c_int,
    oldp: ?*anyopaque,
    oldlenp: ?*usize,
    newp: ?*anyopaque,
    newlen: usize,
) SysCtlError!void {
    if (native_os == .wasi) {
        @compileError("sysctl not supported on WASI");
    }
    if (native_os == .haiku) {
        @compileError("sysctl not supported on Haiku");
    }

    const name_len = cast(c_uint, name.len) orelse return error.NameTooLong;
    switch (errno(system.sysctl(name.ptr, name_len, oldp, oldlenp, newp, newlen))) {
        .SUCCESS => return,
        .FAULT => unreachable,
        .PERM => return error.PermissionDenied,
        .NOMEM => return error.SystemResources,
        .NOENT => return error.UnknownName,
        else => |err| return unexpectedErrno(err),
    }
}

pub fn getSelfPhdrs() []std.elf.ElfN.Phdr {
    const getauxval = if (builtin.link_libc) std.c.getauxval else std.os.linux.getauxval;
    assert(getauxval(std.elf.AT_PHENT) == @sizeOf(std.elf.ElfN.Phdr));
    const phdrs: [*]std.elf.ElfN.Phdr = @ptrFromInt(getauxval(std.elf.AT_PHDR));
    return phdrs[0..getauxval(std.elf.AT_PHNUM)];
}

pub fn dl_iterate_phdr(
    context: anytype,
    comptime Error: type,
    comptime callback: fn (info: *dl_phdr_info, size: usize, context: @TypeOf(context)) Error!void,
) Error!void {
    const Context = @TypeOf(context);
    const elf = std.elf;
    const dl = @import("dynamic_library.zig");

    switch (builtin.object_format) {
        .elf, .c => {},
        else => @compileError("dl_iterate_phdr is not available for this target"),
    }

    if (builtin.link_libc) {
        switch (system.dl_iterate_phdr(struct {
            fn callbackC(info: *dl_phdr_info, size: usize, data: ?*anyopaque) callconv(.c) c_int {
                const context_ptr: *const Context = @ptrCast(@alignCast(data));
                callback(info, size, context_ptr.*) catch |err| return @intFromError(err);
                return 0;
            }
        }.callbackC, @ptrCast(@constCast(&context)))) {
            0 => return,
            else => |err| return @as(Error, @errorCast(@errorFromInt(@as(std.meta.Int(.unsigned, @bitSizeOf(anyerror)), @intCast(err))))),
        }
    }

    var it = dl.linkmap_iterator() catch unreachable;

    // The executable has no dynamic link segment, create a single entry for
    // the whole ELF image.
    if (it.end()) {
        const getauxval = if (builtin.link_libc) std.c.getauxval else std.os.linux.getauxval;
        const phdrs = getSelfPhdrs();
        var info: dl_phdr_info = .{
            .addr = for (phdrs) |phdr| switch (phdr.type) {
                .PHDR => break @intFromPtr(phdrs.ptr) - phdr.vaddr,
                else => {},
            } else unreachable,
            .name = switch (getauxval(std.elf.AT_EXECFN)) {
                0 => "/proc/self/exe",
                else => |name| @ptrFromInt(name),
            },
            .phdr = phdrs.ptr,
            .phnum = @intCast(phdrs.len),
        };

        return callback(&info, @sizeOf(dl_phdr_info), context);
    }

    // Last return value from the callback function.
    while (it.next()) |entry| {
        const phdrs: []elf.ElfN.Phdr = if (entry.addr != 0) phdrs: {
            const ehdr: *elf.ElfN.Ehdr = @ptrFromInt(entry.addr);
            assert(mem.eql(u8, ehdr.ident[0..4], elf.MAGIC));
            const phdrs: [*]elf.ElfN.Phdr = @ptrFromInt(entry.addr + ehdr.phoff);
            break :phdrs phdrs[0..ehdr.phnum];
        } else getSelfPhdrs();

        var info: dl_phdr_info = .{
            .addr = entry.addr,
            .name = entry.name,
            .phdr = phdrs.ptr,
            .phnum = @intCast(phdrs.len),
        };

        try callback(&info, @sizeOf(dl_phdr_info), context);
    }
}

pub const SchedGetAffinityError = error{PermissionDenied} || UnexpectedError;

pub fn sched_getaffinity(pid: pid_t) SchedGetAffinityError!cpu_set_t {
    var set: cpu_set_t = undefined;
    switch (errno(system.sched_getaffinity(pid, @sizeOf(cpu_set_t), &set))) {
        .SUCCESS => return set,
        .FAULT => unreachable,
        .INVAL => unreachable,
        .SRCH => unreachable,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

pub const SigaltstackError = error{
    /// The supplied stack size was less than MINSIGSTKSZ.
    SizeTooSmall,

    /// Attempted to change the signal stack while it was active.
    PermissionDenied,
} || UnexpectedError;

pub fn sigaltstack(ss: ?*const stack_t, old_ss: ?*stack_t) SigaltstackError!void {
    switch (errno(system.sigaltstack(ss, old_ss))) {
        .SUCCESS => return,
        .FAULT => unreachable,
        .INVAL => unreachable,
        .NOMEM => return error.SizeTooSmall,
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

/// Return a filled sigset_t.
pub fn sigfillset() sigset_t {
    if (builtin.link_libc) {
        var set: sigset_t = undefined;
        switch (errno(system.sigfillset(&set))) {
            .SUCCESS => return set,
            else => unreachable,
        }
    }
    return system.sigfillset();
}

/// Return an empty sigset_t.
pub fn sigemptyset() sigset_t {
    if (builtin.link_libc) {
        var set: sigset_t = undefined;
        switch (errno(system.sigemptyset(&set))) {
            .SUCCESS => return set,
            else => unreachable,
        }
    }
    return system.sigemptyset();
}

pub fn sigaddset(set: *sigset_t, sig: SIG) void {
    if (builtin.link_libc) {
        switch (errno(system.sigaddset(set, sig))) {
            .SUCCESS => return,
            else => unreachable,
        }
    }
    system.sigaddset(set, sig);
}

pub fn sigdelset(set: *sigset_t, sig: SIG) void {
    if (builtin.link_libc) {
        switch (errno(system.sigdelset(set, sig))) {
            .SUCCESS => return,
            else => unreachable,
        }
    }
    system.sigdelset(set, sig);
}

pub fn sigismember(set: *const sigset_t, sig: SIG) bool {
    if (builtin.link_libc) {
        const rc = system.sigismember(set, sig);
        switch (errno(rc)) {
            .SUCCESS => return rc == 1,
            else => unreachable,
        }
    }
    return system.sigismember(set, sig);
}

/// Examine and change a signal action.
pub fn sigaction(sig: SIG, noalias act: ?*const Sigaction, noalias oact: ?*Sigaction) void {
    switch (errno(system.sigaction(sig, act, oact))) {
        .SUCCESS => return,
        // EINVAL means the signal is either invalid or some signal that cannot have its action
        // changed. For POSIX, this means SIGKILL/SIGSTOP. For e.g. illumos, this also includes the
        // non-standard SIGWAITING, SIGCANCEL, and SIGLWP. Either way, programmer error.
        .INVAL => unreachable,
        else => unreachable,
    }
}

/// Sets the thread signal mask.
pub fn sigprocmask(flags: u32, noalias set: ?*const sigset_t, noalias oldset: ?*sigset_t) void {
    switch (errno(system.sigprocmask(@bitCast(flags), set, oldset))) {
        .SUCCESS => return,
        .FAULT => unreachable,
        .INVAL => unreachable,
        else => unreachable,
    }
}

pub const GetHostNameError = error{PermissionDenied} || UnexpectedError;

pub fn gethostname(name_buffer: *[HOST_NAME_MAX]u8) GetHostNameError![]u8 {
    if (builtin.link_libc) {
        switch (errno(system.gethostname(name_buffer, name_buffer.len))) {
            .SUCCESS => return mem.sliceTo(name_buffer, 0),
            .FAULT => unreachable,
            .NAMETOOLONG => unreachable, // HOST_NAME_MAX prevents this
            .PERM => return error.PermissionDenied,
            else => |err| return unexpectedErrno(err),
        }
    }
    if (native_os == .linux) {
        const uts = uname();
        const hostname = mem.sliceTo(&uts.nodename, 0);
        const result = name_buffer[0..hostname.len];
        @memcpy(result, hostname);
        return result;
    }

    @compileError("TODO implement gethostname for this OS");
}

pub fn uname() utsname {
    var uts: utsname = undefined;
    switch (errno(system.uname(&uts))) {
        .SUCCESS => return uts,
        .FAULT => unreachable,
        else => unreachable,
    }
}

pub const PollError = error{
    /// The network subsystem has failed.
    NetworkDown,

    /// The kernel had no space to allocate file descriptor tables.
    SystemResources,
} || UnexpectedError;

pub fn poll(fds: []pollfd, timeout: i32) PollError!usize {
    if (native_os == .windows) {
        @compileError("use std.Io instead");
    }
    while (true) {
        const fds_count = cast(nfds_t, fds.len) orelse return error.SystemResources;
        const rc = system.poll(fds.ptr, fds_count, timeout);
        switch (errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .FAULT => unreachable,
            .INTR => continue,
            .INVAL => unreachable,
            .NOMEM => return error.SystemResources,
            else => |err| return unexpectedErrno(err),
        }
    }
    unreachable;
}

pub const PPollError = error{
    /// The operation was interrupted by a delivery of a signal before it could complete.
    SignalInterrupt,

    /// The kernel had no space to allocate file descriptor tables.
    SystemResources,
} || UnexpectedError;

pub fn ppoll(fds: []pollfd, timeout: ?*const timespec, mask: ?*const sigset_t) PPollError!usize {
    var ts: timespec = undefined;
    var ts_ptr: ?*timespec = null;
    if (timeout) |timeout_ns| {
        ts_ptr = &ts;
        ts = timeout_ns.*;
    }
    const fds_count = cast(nfds_t, fds.len) orelse return error.SystemResources;
    const rc = system.ppoll(fds.ptr, fds_count, ts_ptr, mask);
    switch (errno(rc)) {
        .SUCCESS => return @intCast(rc),
        .FAULT => unreachable,
        .INTR => return error.SignalInterrupt,
        .INVAL => unreachable,
        .NOMEM => return error.SystemResources,
        else => |err| return unexpectedErrno(err),
    }
}

pub const SetSockOptError = error{
    /// The socket is already connected, and a specified option cannot be set while the socket is connected.
    AlreadyConnected,

    /// The option is not supported by the protocol.
    InvalidProtocolOption,

    /// The send and receive timeout values are too big to fit into the timeout fields in the socket structure.
    TimeoutTooBig,

    /// Insufficient resources are available in the system to complete the call.
    SystemResources,

    /// Setting the socket option requires more elevated permissions.
    PermissionDenied,

    OperationUnsupported,
    NetworkDown,
    FileDescriptorNotASocket,
    SocketNotBound,
    NoDevice,
} || UnexpectedError;

/// Set a socket's options.
pub fn setsockopt(fd: socket_t, level: i32, optname: u32, opt: []const u8) SetSockOptError!void {
    if (native_os == .windows) {
        @compileError("use std.Io instead");
    } else {
        switch (errno(system.setsockopt(fd, level, optname, opt.ptr, @intCast(opt.len)))) {
            .SUCCESS => {},
            .BADF => unreachable, // always a race condition
            .NOTSOCK => unreachable, // always a race condition
            .INVAL => unreachable,
            .FAULT => unreachable,
            .DOM => return error.TimeoutTooBig,
            .ISCONN => return error.AlreadyConnected,
            .NOPROTOOPT => return error.InvalidProtocolOption,
            .NOMEM => return error.SystemResources,
            .NOBUFS => return error.SystemResources,
            .PERM => return error.PermissionDenied,
            .NODEV => return error.NoDevice,
            .OPNOTSUPP => return error.OperationUnsupported,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub const MemFdCreateError = error{
    SystemFdQuotaExceeded,
    ProcessFdQuotaExceeded,
    OutOfMemory,
    /// Either the name provided exceeded `NAME_MAX`, or invalid flags were passed.
    NameTooLong,
    SystemOutdated,
} || UnexpectedError;

pub fn memfd_createZ(name: [*:0]const u8, flags: u32) MemFdCreateError!fd_t {
    switch (native_os) {
        .linux => {
            // memfd_create is available only in glibc versions starting with 2.27 and bionic versions starting with 30.
            const use_c = std.c.versionCheck(if (builtin.abi.isAndroid()) .{ .major = 30, .minor = 0, .patch = 0 } else .{ .major = 2, .minor = 27, .patch = 0 });
            const sys = if (use_c) std.c else linux;
            const rc = sys.memfd_create(name, flags);
            switch (sys.errno(rc)) {
                .SUCCESS => return @intCast(rc),
                .FAULT => unreachable, // name has invalid memory
                .INVAL => return error.NameTooLong, // or, program has a bug and flags are faulty
                .NFILE => return error.SystemFdQuotaExceeded,
                .MFILE => return error.ProcessFdQuotaExceeded,
                .NOMEM => return error.OutOfMemory,
                else => |err| return unexpectedErrno(err),
            }
        },
        .freebsd => {
            if (comptime builtin.os.version_range.semver.max.order(.{ .major = 13, .minor = 0, .patch = 0 }) == .lt)
                @compileError("memfd_create is unavailable on FreeBSD < 13.0");
            const rc = system.memfd_create(name, flags);
            switch (errno(rc)) {
                .SUCCESS => return rc,
                .BADF => unreachable, // name argument NULL
                .INVAL => unreachable, // name too long or invalid/unsupported flags.
                .MFILE => return error.ProcessFdQuotaExceeded,
                .NFILE => return error.SystemFdQuotaExceeded,
                .NOSYS => return error.SystemOutdated,
                else => |err| return unexpectedErrno(err),
            }
        },
        else => @compileError("target OS does not support memfd_create()"),
    }
}

pub fn memfd_create(name: []const u8, flags: u32) MemFdCreateError!fd_t {
    var buffer: [NAME_MAX - "memfd:".len - 1:0]u8 = undefined;
    if (name.len > buffer.len) return error.NameTooLong;
    @memcpy(buffer[0..name.len], name);
    buffer[name.len] = 0;
    return memfd_createZ(&buffer, flags);
}

pub fn getrusage(who: i32) rusage {
    var result: rusage = undefined;
    const rc = system.getrusage(who, &result);
    switch (errno(rc)) {
        .SUCCESS => return result,
        .INVAL => unreachable,
        .FAULT => unreachable,
        else => unreachable,
    }
}

pub const TIOCError = error{NotATerminal};

pub const TermiosGetError = TIOCError || UnexpectedError;

pub fn tcgetattr(handle: fd_t) TermiosGetError!termios {
    while (true) {
        var term: termios = undefined;
        switch (errno(system.tcgetattr(handle, &term))) {
            .SUCCESS => return term,
            .INTR => continue,
            .BADF => unreachable,
            .NOTTY => return error.NotATerminal,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub const TermiosSetError = TermiosGetError || error{ProcessOrphaned};

pub fn tcsetattr(handle: fd_t, optional_action: TCSA, termios_p: termios) TermiosSetError!void {
    while (true) {
        switch (errno(system.tcsetattr(handle, optional_action, &termios_p))) {
            .SUCCESS => return,
            .BADF => unreachable,
            .INTR => continue,
            .INVAL => unreachable,
            .NOTTY => return error.NotATerminal,
            .IO => return error.ProcessOrphaned,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub const TermioGetPgrpError = TIOCError || UnexpectedError;

/// Returns the process group ID for the TTY associated with the given handle.
pub fn tcgetpgrp(handle: fd_t) TermioGetPgrpError!pid_t {
    while (true) {
        var pgrp: pid_t = undefined;
        switch (errno(system.tcgetpgrp(handle, &pgrp))) {
            .SUCCESS => return pgrp,
            .BADF => unreachable,
            .INVAL => unreachable,
            .INTR => continue,
            .NOTTY => return error.NotATerminal,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub const TermioSetPgrpError = TermioGetPgrpError || error{NotAPgrpMember};

/// Sets the controlling process group ID for given TTY.
/// handle must be valid fd_t to a TTY associated with calling process.
/// pgrp must be a valid process group, and the calling process must be a member
/// of that group.
pub fn tcsetpgrp(handle: fd_t, pgrp: pid_t) TermioSetPgrpError!void {
    while (true) {
        switch (errno(system.tcsetpgrp(handle, &pgrp))) {
            .SUCCESS => return,
            .BADF => unreachable,
            .INVAL => unreachable,
            .INTR => continue,
            .NOTTY => return error.NotATerminal,
            .PERM => return TermioSetPgrpError.NotAPgrpMember,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub fn signalfd(fd: fd_t, mask: *const sigset_t, flags: u32) !fd_t {
    const rc = system.signalfd(fd, mask, flags);
    switch (errno(rc)) {
        .SUCCESS => return @intCast(rc),
        .BADF, .INVAL => unreachable,
        .NFILE => return error.SystemFdQuotaExceeded,
        .NOMEM => return error.SystemResources,
        .MFILE => return error.ProcessResources,
        .NODEV => return error.InodeMountFail,
        else => |err| return unexpectedErrno(err),
    }
}

pub const SyncError = std.Io.File.SyncError;

/// Write all pending file contents and metadata modifications to all filesystems.
pub fn sync() void {
    system.sync();
}

/// Write all pending file contents and metadata modifications to the filesystem which contains the specified file.
pub fn syncfs(fd: fd_t) SyncError!void {
    const rc = system.syncfs(fd);
    switch (errno(rc)) {
        .SUCCESS => return,
        .BADF, .INVAL, .ROFS => unreachable,
        .IO => return error.InputOutput,
        .NOSPC => return error.NoSpaceLeft,
        .DQUOT => return error.DiskQuota,
        else => |err| return unexpectedErrno(err),
    }
}

/// Write all pending file contents for the specified file descriptor to the underlying filesystem, but not necessarily the metadata.
pub fn fdatasync(fd: fd_t) SyncError!void {
    const rc = system.fdatasync(fd);
    switch (errno(rc)) {
        .SUCCESS => return,
        .BADF, .INVAL, .ROFS => unreachable,
        .IO => return error.InputOutput,
        .NOSPC => return error.NoSpaceLeft,
        .DQUOT => return error.DiskQuota,
        else => |err| return unexpectedErrno(err),
    }
}

pub const PrctlError = error{
    /// Can only occur with PR_SET_SECCOMP/SECCOMP_MODE_FILTER or
    /// PR_SET_MM/PR_SET_MM_EXE_FILE
    AccessDenied,
    /// Can only occur with PR_SET_MM/PR_SET_MM_EXE_FILE
    InvalidFileDescriptor,
    InvalidAddress,
    /// Can only occur with PR_SET_SPECULATION_CTRL, PR_MPX_ENABLE_MANAGEMENT,
    /// or PR_MPX_DISABLE_MANAGEMENT
    UnsupportedFeature,
    /// Can only occur with PR_SET_FP_MODE
    OperationUnsupported,
    PermissionDenied,
} || UnexpectedError;

pub fn prctl(option: PR, args: anytype) PrctlError!u31 {
    if (@typeInfo(@TypeOf(args)) != .@"struct")
        @compileError("Expected tuple or struct argument, found " ++ @typeName(@TypeOf(args)));
    if (args.len > 4)
        @compileError("prctl takes a maximum of 4 optional arguments");

    var buf: [4]usize = undefined;
    {
        comptime var i = 0;
        inline while (i < args.len) : (i += 1) buf[i] = args[i];
    }

    const rc = system.prctl(@intFromEnum(option), buf[0], buf[1], buf[2], buf[3]);
    switch (errno(rc)) {
        .SUCCESS => return @intCast(rc),
        .ACCES => return error.AccessDenied,
        .BADF => return error.InvalidFileDescriptor,
        .FAULT => return error.InvalidAddress,
        .INVAL => unreachable,
        .NODEV, .NXIO => return error.UnsupportedFeature,
        .OPNOTSUPP => return error.OperationUnsupported,
        .PERM, .BUSY => return error.PermissionDenied,
        .RANGE => unreachable,
        else => |err| return unexpectedErrno(err),
    }
}

pub const GetrlimitError = UnexpectedError;

pub fn getrlimit(resource: rlimit_resource) GetrlimitError!rlimit {
    const getrlimit_sym = if (lfs64_abi) system.getrlimit64 else system.getrlimit;

    var limits: rlimit = undefined;
    switch (errno(getrlimit_sym(resource, &limits))) {
        .SUCCESS => return limits,
        .FAULT => unreachable, // bogus pointer
        .INVAL => unreachable,
        else => |err| return unexpectedErrno(err),
    }
}

pub const SetrlimitError = error{ PermissionDenied, LimitTooBig } || UnexpectedError;

pub fn setrlimit(resource: rlimit_resource, limits: rlimit) SetrlimitError!void {
    const setrlimit_sym = if (lfs64_abi) system.setrlimit64 else system.setrlimit;

    switch (errno(setrlimit_sym(resource, &limits))) {
        .SUCCESS => return,
        .FAULT => unreachable, // bogus pointer
        .INVAL => return error.LimitTooBig, // this could also mean "invalid resource", but that would be unreachable
        .PERM => return error.PermissionDenied,
        else => |err| return unexpectedErrno(err),
    }
}

pub const MincoreError = error{
    /// A kernel resource was temporarily unavailable.
    SystemResources,
    /// vec points to an invalid address.
    InvalidAddress,
    /// addr is not page-aligned.
    InvalidSyscall,
    /// One of the following:
    /// * length is greater than user space TASK_SIZE - addr
    /// * addr + length contains unmapped memory
    OutOfMemory,
    /// The mincore syscall is not available on this version and configuration
    /// of this UNIX-like kernel.
    MincoreUnavailable,
} || UnexpectedError;

/// Determine whether pages are resident in memory.
pub fn mincore(ptr: [*]align(page_size_min) u8, length: usize, vec: [*]u8) MincoreError!void {
    return switch (errno(system.mincore(ptr, length, vec))) {
        .SUCCESS => {},
        .AGAIN => error.SystemResources,
        .FAULT => error.InvalidAddress,
        .INVAL => error.InvalidSyscall,
        .NOMEM => error.OutOfMemory,
        .NOSYS => error.MincoreUnavailable,
        else => |err| unexpectedErrno(err),
    };
}

pub const MadviseError = error{
    /// advice is MADV.REMOVE, but the specified address range is not a shared writable mapping.
    AccessDenied,
    /// advice is MADV.HWPOISON, but the caller does not have the CAP_SYS_ADMIN capability.
    PermissionDenied,
    /// A kernel resource was temporarily unavailable.
    SystemResources,
    /// One of the following:
    /// * addr is not page-aligned or length is negative
    /// * advice is not valid
    /// * advice is MADV.DONTNEED or MADV.REMOVE and the specified address range
    ///   includes locked, Huge TLB pages, or VM_PFNMAP pages.
    /// * advice is MADV.MERGEABLE or MADV.UNMERGEABLE, but the kernel was not
    ///   configured with CONFIG_KSM.
    /// * advice is MADV.FREE or MADV.WIPEONFORK but the specified address range
    ///   includes file, Huge TLB, MAP.SHARED, or VM_PFNMAP ranges.
    InvalidSyscall,
    /// (for MADV.WILLNEED) Paging in this area would exceed the process's
    /// maximum resident set size.
    WouldExceedMaximumResidentSetSize,
    /// One of the following:
    /// * (for MADV.WILLNEED) Not enough memory: paging in failed.
    /// * Addresses in the specified range are not currently mapped, or
    ///   are outside the address space of the process.
    OutOfMemory,
    /// The madvise syscall is not available on this version and configuration
    /// of the Linux kernel.
    MadviseUnavailable,
    /// The operating system returned an undocumented error code.
    Unexpected,
};

/// Give advice about use of memory.
/// This syscall is optional and is sometimes configured to be disabled.
pub fn madvise(ptr: [*]align(page_size_min) u8, length: usize, advice: u32) MadviseError!void {
    switch (errno(system.madvise(ptr, length, advice))) {
        .SUCCESS => return,
        .PERM => return error.PermissionDenied,
        .ACCES => return error.AccessDenied,
        .AGAIN => return error.SystemResources,
        .BADF => unreachable, // The map exists, but the area maps something that isn't a file.
        .INVAL => return error.InvalidSyscall,
        .IO => return error.WouldExceedMaximumResidentSetSize,
        .NOMEM => return error.OutOfMemory,
        .NOSYS => return error.MadviseUnavailable,
        else => |err| return unexpectedErrno(err),
    }
}

pub const PerfEventOpenError = error{
    /// Returned if the perf_event_attr size value is too small (smaller
    /// than PERF_ATTR_SIZE_VER0), too big (larger than the page  size),
    /// or  larger  than the kernel supports and the extra bytes are not
    /// zero.  When E2BIG is returned, the perf_event_attr size field is
    /// overwritten by the kernel to be the size of the structure it was
    /// expecting.
    TooBig,
    /// Returned when the requested event requires CAP_SYS_ADMIN permis‐
    /// sions  (or a more permissive perf_event paranoid setting).  Some
    /// common cases where an unprivileged process  may  encounter  this
    /// error:  attaching  to a process owned by a different user; moni‐
    /// toring all processes on a given CPU (i.e.,  specifying  the  pid
    /// argument  as  -1); and not setting exclude_kernel when the para‐
    /// noid setting requires it.
    /// Also:
    /// Returned on many (but not all) architectures when an unsupported
    /// exclude_hv,  exclude_idle,  exclude_user, or exclude_kernel set‐
    /// ting is specified.
    /// It can also happen, as with EACCES, when the requested event re‐
    /// quires   CAP_SYS_ADMIN   permissions   (or   a  more  permissive
    /// perf_event paranoid setting).  This includes  setting  a  break‐
    /// point on a kernel address, and (since Linux 3.13) setting a ker‐
    /// nel function-trace tracepoint.
    PermissionDenied,
    /// Returned if another event already has exclusive  access  to  the
    /// PMU.
    DeviceBusy,
    /// Each  opened  event uses one file descriptor.  If a large number
    /// of events are opened, the per-process limit  on  the  number  of
    /// open file descriptors will be reached, and no more events can be
    /// created.
    ProcessResources,
    EventRequiresUnsupportedCpuFeature,
    /// Returned if  you  try  to  add  more  breakpoint
    /// events than supported by the hardware.
    TooManyBreakpoints,
    /// Returned  if PERF_SAMPLE_STACK_USER is set in sample_type and it
    /// is not supported by hardware.
    SampleStackNotSupported,
    /// Returned if an event requiring a specific  hardware  feature  is
    /// requested  but  there is no hardware support.  This includes re‐
    /// questing low-skid events if not supported, branch tracing if  it
    /// is not available, sampling if no PMU interrupt is available, and
    /// branch stacks for software events.
    EventNotSupported,
    /// Returned  if  PERF_SAMPLE_CALLCHAIN  is   requested   and   sam‐
    /// ple_max_stack   is   larger   than   the  maximum  specified  in
    /// /proc/sys/kernel/perf_event_max_stack.
    SampleMaxStackOverflow,
    /// Returned if attempting to attach to a process that does not  exist.
    ProcessNotFound,
} || UnexpectedError;

pub fn perf_event_open(
    attr: *system.perf_event_attr,
    pid: pid_t,
    cpu: i32,
    group_fd: fd_t,
    flags: usize,
) PerfEventOpenError!fd_t {
    if (native_os == .linux) {
        // There is no syscall wrapper for this function exposed by libcs
        const rc = linux.perf_event_open(attr, pid, cpu, group_fd, flags);
        switch (linux.errno(rc)) {
            .SUCCESS => return @intCast(rc),
            .@"2BIG" => return error.TooBig,
            .ACCES => return error.PermissionDenied,
            .BADF => unreachable, // group_fd file descriptor is not valid.
            .BUSY => return error.DeviceBusy,
            .FAULT => unreachable, // Segmentation fault.
            .INVAL => unreachable, // Bad attr settings.
            .INTR => unreachable, // Mixed perf and ftrace handling for a uprobe.
            .MFILE => return error.ProcessResources,
            .NODEV => return error.EventRequiresUnsupportedCpuFeature,
            .NOENT => unreachable, // Invalid type setting.
            .NOSPC => return error.TooManyBreakpoints,
            .NOSYS => return error.SampleStackNotSupported,
            .OPNOTSUPP => return error.EventNotSupported,
            .OVERFLOW => return error.SampleMaxStackOverflow,
            .PERM => return error.PermissionDenied,
            .SRCH => return error.ProcessNotFound,
            else => |err| return unexpectedErrno(err),
        }
    }
}

pub const PtraceError = error{
    DeadLock,
    DeviceBusy,
    InputOutput,
    NameTooLong,
    OperationUnsupported,
    OutOfMemory,
    ProcessNotFound,
    PermissionDenied,
} || UnexpectedError;

pub fn pt
```
