```
ry bc.readChar6()),
                    },
                    .align_32_bits, .block_len => return error.UnsupportedArrayElement,
                    .abbrev_op => switch (try bc.readFixed(u1, 1)) {
                        1 => try operands.appendSlice(&.{
                            Abbrev.Operand.literal_id,
                            try bc.readVbr(u64, 8),
                        }),
                        0 => {
                            const encoding: Abbrev.Operand.Encoding =
                                @enumFromInt(try bc.readFixed(u3, 3));
                            try operands.append(@intFromEnum(encoding));
                            switch (encoding) {
                                .fixed, .vbr => try operands.append(try bc.readVbr(u7, 5)),
                                .array, .char6, .blob => {},
                                _ => return error.UnsuportedAbbrevEncoding,
                            }
                        },
                    },
                };
                break;
            },
            .char6 => operands.appendAssumeCapacity(try bc.readChar6()),
            .blob => |len_width| {
                assert(abbrev_operand_i + 1 == abbrev.operands.len);
                const len = std.math.cast(usize, try bc.readVbr(u32, len_width)) orelse
                    return error.Overflow;
                bc.align32Bits();
                try bc.readBytes(try blob.addManyAsSlice(len));
                bc.align32Bits();
            },
        },
        .align_32_bits => bc.align32Bits(),
        .block_len => operands.appendAssumeCapacity(try bc.read32Bits()),
        .abbrev_op => unreachable,
    };
    return .{
        .name = name: {
            if (operands.items.len < 1) break :name &.{};
            const record_id = std.math.cast(u32, operands.items[0]) orelse break :name &.{};
            if (state.block_id) |block_id| {
                if (bc.block_info.get(block_id)) |block_info| {
                    break :name block_info.record_names.get(record_id) orelse break :name &.{};
                }
            }
            break :name &.{};
        },
        .id = std.math.cast(u32, operands.items[0]) orelse return error.InvalidRecordId,
        .operands = operands.items[1..],
        .blob = blob.items,
    };
}

fn startBlock(bc: *BitcodeReader, block_id: ?u32, new_abbrev_len: u6) !void {
    const abbrevs = if (block_id) |id|
        if (bc.block_info.get(id)) |block_info| block_info.abbrevs.abbrevs.items else &.{}
    else
        &.{};

    const state = try bc.stack.addOne(bc.allocator);
    state.* = .{
        .block_id = block_id,
        .abbrev_id_width = new_abbrev_len,
        .abbrevs = .{ .abbrevs = .empty },
    };
    try state.abbrevs.abbrevs.ensureTotalCapacity(
        bc.allocator,
        @typeInfo(Abbrev.Builtin).@"enum".fields.len + abbrevs.len,
    );

    assert(state.abbrevs.abbrevs.items.len == @intFromEnum(Abbrev.Builtin.end_block));
    try state.abbrevs.addAbbrevAssumeCapacity(bc.allocator, .{
        .operands = &.{
            .{ .literal = Abbrev.Builtin.end_block.toRecordId() },
            .align_32_bits,
        },
    });
    assert(state.abbrevs.abbrevs.items.len == @intFromEnum(Abbrev.Builtin.enter_subblock));
    try state.abbrevs.addAbbrevAssumeCapacity(bc.allocator, .{
        .operands = &.{
            .{ .literal = Abbrev.Builtin.enter_subblock.toRecordId() },
            .{ .encoding = .{ .vbr = 8 } }, // blockid
            .{ .encoding = .{ .vbr = 4 } }, // newabbrevlen
            .align_32_bits,
            .block_len,
        },
    });
    assert(state.abbrevs.abbrevs.items.len == @intFromEnum(Abbrev.Builtin.define_abbrev));
    try state.abbrevs.addAbbrevAssumeCapacity(bc.allocator, .{
        .operands = &.{
            .{ .literal = Abbrev.Builtin.define_abbrev.toRecordId() },
            .{ .encoding = .{ .array = 5 } }, // numabbrevops
            .abbrev_op,
        },
    });
    assert(state.abbrevs.abbrevs.items.len == @intFromEnum(Abbrev.Builtin.unabbrev_record));
    try state.abbrevs.addAbbrevAssumeCapacity(bc.allocator, .{
        .operands = &.{
            .{ .encoding = .{ .vbr = 6 } }, // code
            .{ .encoding = .{ .array = 6 } }, // numops
            .{ .encoding = .{ .vbr = 6 } }, // ops
        },
    });
    assert(state.abbrevs.abbrevs.items.len == @typeInfo(Abbrev.Builtin).@"enum".fields.len);
    for (abbrevs) |abbrev| try state.abbrevs.addAbbrevAssumeCapacity(bc.allocator, abbrev);
}

fn endBlock(bc: *BitcodeReader) !void {
    if (bc.stack.items.len == 0) return error.InvalidEndBlock;
    bc.stack.items[bc.stack.items.len - 1].deinit(bc.allocator);
    bc.stack.items.len -= 1;
}

fn parseBlockInfoBlock(bc: *BitcodeReader) !void {
    var block_id: ?u32 = null;
    while (true) {
        const record = (try bc.nextRecord()) orelse return error.EndOfStream;
        switch (record.id) {
            Abbrev.Builtin.end_block.toRecordId() => break,
            Abbrev.Builtin.define_abbrev.toRecordId() => {
                const gop = try bc.block_info.getOrPut(bc.allocator, block_id orelse
                    return error.UnspecifiedBlockId);
                if (!gop.found_existing) gop.value_ptr.* = Block.Info.default;
                try gop.value_ptr.abbrevs.addOwnedAbbrev(
                    bc.allocator,
                    try record.toOwnedAbbrev(bc.allocator),
                );
            },
            Block.Info.set_bid_id => block_id = std.math.cast(u32, record.operands[0]) orelse
                return error.Overflow,
            Block.Info.block_name_id => if (bc.keep_names) {
                const gop = try bc.block_info.getOrPut(bc.allocator, block_id orelse
                    return error.UnspecifiedBlockId);
                if (!gop.found_existing) gop.value_ptr.* = Block.Info.default;
                const name = try bc.allocator.alloc(u8, record.operands.len);
                errdefer bc.allocator.free(name);
                for (name, record.operands) |*byte, operand|
                    byte.* = std.math.cast(u8, operand) orelse return error.InvalidName;
                gop.value_ptr.block_name = name;
            },
            Block.Info.set_record_name_id => if (bc.keep_names) {
                const gop = try bc.block_info.getOrPut(bc.allocator, block_id orelse
                    return error.UnspecifiedBlockId);
                if (!gop.found_existing) gop.value_ptr.* = Block.Info.default;
                const name = try bc.allocator.alloc(u8, record.operands.len - 1);
                errdefer bc.allocator.free(name);
                for (name, record.operands[1..]) |*byte, operand|
                    byte.* = std.math.cast(u8, operand) orelse return error.InvalidName;
                try gop.value_ptr.record_names.put(
                    bc.allocator,
                    std.math.cast(u32, record.operands[0]) orelse return error.Overflow,
                    name,
                );
            },
            else => return error.UnsupportedBlockInfoRecord,
        }
    }
}

fn align32Bits(bc: *BitcodeReader) void {
    bc.bit_offset = 0;
}

fn read32Bits(bc: *BitcodeReader) !u32 {
    assert(bc.bit_offset == 0);
    return bc.reader.takeInt(u32, .little);
}

fn readBytes(bc: *BitcodeReader, bytes: []u8) !void {
    assert(bc.bit_offset == 0);
    try bc.reader.readSliceAll(bytes);

    const trailing_bytes = bytes.len % 4;
    if (trailing_bytes > 0) {
        var bit_buffer: [4]u8 = @splat(0);
        try bc.reader.readSliceAll(bit_buffer[trailing_bytes..]);
        bc.bit_buffer = std.mem.readInt(u32, &bit_buffer, .little);
        bc.bit_offset = @intCast(8 * trailing_bytes);
    }
}

fn readFixed(bc: *BitcodeReader, comptime T: type, bits: u7) !T {
    var result: T = 0;
    var shift: std.math.Log2IntCeil(T) = 0;
    var remaining = bits;
    while (remaining > 0) {
        if (bc.bit_offset == 0) bc.bit_buffer = try bc.read32Bits();
        const chunk_len = @min(@as(u6, 32) - bc.bit_offset, remaining);
        const chunk_mask = @as(u32, std.math.maxInt(u32)) >> @intCast(32 - chunk_len);
        result |= @as(T, @intCast(bc.bit_buffer >> bc.bit_offset & chunk_mask)) << @intCast(shift);
        shift += @intCast(chunk_len);
        remaining -= chunk_len;
        bc.bit_offset = @truncate(bc.bit_offset + chunk_len);
    }
    return result;
}

fn readVbr(bc: *BitcodeReader, comptime T: type, bits: u7) !T {
    const chunk_bits: u6 = @intCast(bits - 1);
    const chunk_msb = @as(u64, 1) << chunk_bits;

    var result: u64 = 0;
    var shift: u6 = 0;
    while (true) {
        const chunk = try bc.readFixed(u64, bits);
        result |= (chunk & (chunk_msb - 1)) << shift;
        if (chunk & chunk_msb == 0) break;
        shift += chunk_bits;
    }
    return @intCast(result);
}

fn readChar6(bc: *BitcodeReader) !u8 {
    return switch (try bc.readFixed(u6, 6)) {
        0...25 => |c| @as(u8, c - 0) + 'a',
        26...51 => |c| @as(u8, c - 26) + 'A',
        52...61 => |c| @as(u8, c - 52) + '0',
        62 => '.',
        63 => '_',
    };
}

const State = struct {
    block_id: ?u32,
    abbrev_id_width: u6,
    abbrevs: Abbrev.Store,

    fn deinit(state: *State, allocator: std.mem.Allocator) void {
        state.abbrevs.deinit(allocator);
        state.* = undefined;
    }
};

const Abbrev = struct {
    operands: []const Operand,

    const Builtin = enum(u2) {
        end_block,
        enter_subblock,
        define_abbrev,
        unabbrev_record,

        const first_record_id: u32 = std.math.maxInt(u32) - @typeInfo(Builtin).@"enum".fields.len + 1;
        fn toRecordId(builtin: Builtin) u32 {
            return first_record_id + @intFromEnum(builtin);
        }
    };

    const Operand = union(enum) {
        literal: u64,
        encoding: union(Encoding) {
            fixed: u7,
            vbr: u6,
            array: u3,
            char6,
            blob: u3,
        },
        align_32_bits,
        block_len,
        abbrev_op,

        const literal_id = std.math.maxInt(u64);
        const Encoding = enum(u3) {
            fixed = 1,
            vbr = 2,
            array = 3,
            char6 = 4,
            blob = 5,
            _,
        };
    };

    const Store = struct {
        abbrevs: std.ArrayList(Abbrev),

        fn deinit(store: *Store, allocator: std.mem.Allocator) void {
            for (store.abbrevs.items) |abbrev| allocator.free(abbrev.operands);
            store.abbrevs.deinit(allocator);
            store.* = undefined;
        }

        fn addAbbrev(store: *Store, allocator: std.mem.Allocator, abbrev: Abbrev) !void {
            try store.ensureUnusedCapacity(allocator, 1);
            store.addAbbrevAssumeCapacity(abbrev);
        }

        fn addAbbrevAssumeCapacity(store: *Store, allocator: std.mem.Allocator, abbrev: Abbrev) !void {
            store.abbrevs.appendAssumeCapacity(.{
                .operands = try allocator.dupe(Abbrev.Operand, abbrev.operands),
            });
        }

        fn addOwnedAbbrev(store: *Store, allocator: std.mem.Allocator, abbrev: Abbrev) !void {
            try store.abbrevs.ensureUnusedCapacity(allocator, 1);
            store.addOwnedAbbrevAssumeCapacity(abbrev);
        }

        fn addOwnedAbbrevAssumeCapacity(store: *Store, abbrev: Abbrev) void {
            store.abbrevs.appendAssumeCapacity(abbrev);
        }
    };
};

test {
    _ = &skipBlock;
}



---
File: /std/zig/llvm/Builder.zig
---

const builtin = @import("builtin");
const Builder = @This();

const std = @import("../../std.zig");
const Io = std.Io;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const DW = std.dwarf;
const log = std.log.scoped(.llvm);
const maxInt = std.math.maxInt;
const Writer = std.Io.Writer;

const bitcode_writer = @import("bitcode_writer.zig");
const ir = @import("ir.zig");

gpa: Allocator,
strip: bool,

source_filename: String,
data_layout: String,
target_triple: String,
module_asm: std.ArrayList(u8),

string_map: std.AutoArrayHashMapUnmanaged(void, void),
string_indices: std.ArrayList(u32),
string_bytes: std.ArrayList(u8),

types: std.AutoArrayHashMapUnmanaged(String, Type),
next_unnamed_type: String,
next_unique_type_id: std.AutoHashMapUnmanaged(String, u32),
type_map: std.AutoArrayHashMapUnmanaged(void, void),
type_items: std.ArrayList(Type.Item),
type_extra: std.ArrayList(u32),

attributes: std.AutoArrayHashMapUnmanaged(Attribute.Storage, void),
attributes_map: std.AutoArrayHashMapUnmanaged(void, void),
attributes_indices: std.ArrayList(u32),
attributes_extra: std.ArrayList(u32),

function_attributes_set: std.AutoArrayHashMapUnmanaged(FunctionAttributes, void),

globals: std.AutoArrayHashMapUnmanaged(StrtabString, Global),
next_unnamed_global: StrtabString,
next_replaced_global: StrtabString,
next_unique_global_id: std.AutoHashMapUnmanaged(StrtabString, u32),
aliases: std.ArrayList(Alias),
variables: std.ArrayList(Variable),
functions: std.ArrayList(Function),

strtab_string_map: std.AutoArrayHashMapUnmanaged(void, void),
strtab_string_indices: std.ArrayList(u32),
strtab_string_bytes: std.ArrayList(u8),

constant_map: std.AutoArrayHashMapUnmanaged(void, void),
constant_items: std.MultiArrayList(Constant.Item),
constant_extra: std.ArrayList(u32),
constant_limbs: std.ArrayList(std.math.big.Limb),

alignment_forward_references: std.ArrayList(Alignment),

metadata_map: std.AutoArrayHashMapUnmanaged(void, void),
metadata_items: std.MultiArrayList(Metadata.Item),
metadata_extra: std.ArrayList(u32),
metadata_limbs: std.ArrayList(std.math.big.Limb),
metadata_forward_references: std.ArrayList(Metadata.Optional),
metadata_named: std.AutoArrayHashMapUnmanaged(String, struct {
    len: u32,
    index: Metadata.Item.ExtraIndex,
}),

metadata_string_map: std.AutoArrayHashMapUnmanaged(void, void),
metadata_string_indices: std.ArrayList(u32),
metadata_string_bytes: std.ArrayList(u8),

pub const expected_args_len = 16;
pub const expected_attrs_len = 16;
pub const expected_fields_len = 32;
pub const expected_gep_indices_len = 8;
pub const expected_cases_len = 8;
pub const expected_incoming_len = 8;

pub const Options = struct {
    allocator: Allocator,
    strip: bool = true,
    name: []const u8 = &.{},
    target: *const std.Target = &builtin.target,
    triple: []const u8 = &.{},
};

pub const String = enum(u32) {
    none = maxInt(u31),
    empty,
    _,

    pub fn isAnon(self: String) bool {
        assert(self != .none);
        return self.toIndex() == null;
    }

    pub fn slice(self: String, builder: *const Builder) ?[]const u8 {
        const index = self.toIndex() orelse return null;
        const start = builder.string_indices.items[index];
        const end = builder.string_indices.items[index + 1];
        return builder.string_bytes.items[start..end];
    }

    const FormatData = struct {
        string: String,
        builder: *const Builder,
        quote_behavior: ?QuoteBehavior,
    };
    fn format(data: FormatData, w: *Writer) Writer.Error!void {
        assert(data.string != .none);
        const string_slice = data.string.slice(data.builder) orelse
            return w.print("{d}", .{@intFromEnum(data.string)});
        const quote_behavior = data.quote_behavior orelse return w.writeAll(string_slice);
        return printEscapedString(string_slice, quote_behavior, w);
    }

    pub fn fmt(self: String, builder: *const Builder) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{
            .string = self,
            .builder = builder,
            .quote_behavior = .quote_unless_valid_identifier,
        } };
    }

    pub fn fmtQ(self: String, builder: *const Builder) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{
            .string = self,
            .builder = builder,
            .quote_behavior = .always_quote,
        } };
    }

    pub fn fmtRaw(self: String, builder: *const Builder) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{
            .string = self,
            .builder = builder,
            .quote_behavior = null,
        } };
    }

    fn fromIndex(index: ?usize) String {
        return @enumFromInt(@as(u32, @intCast((index orelse return .none) +
            @intFromEnum(String.empty))));
    }

    fn toIndex(self: String) ?usize {
        return std.math.sub(u32, @intFromEnum(self), @intFromEnum(String.empty)) catch null;
    }

    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: Adapter, key: []const u8) u32 {
            return @truncate(std.hash.Wyhash.hash(0, key));
        }
        pub fn eql(ctx: Adapter, lhs_key: []const u8, _: void, rhs_index: usize) bool {
            return std.mem.eql(u8, lhs_key, String.fromIndex(rhs_index).slice(ctx.builder).?);
        }
    };
};

pub const BinaryOpcode = enum(u4) {
    add = 0,
    sub = 1,
    mul = 2,
    udiv = 3,
    sdiv = 4,
    urem = 5,
    srem = 6,
    shl = 7,
    lshr = 8,
    ashr = 9,
    @"and" = 10,
    @"or" = 11,
    xor = 12,
};

pub const CastOpcode = enum(u4) {
    trunc = 0,
    zext = 1,
    sext = 2,
    fptoui = 3,
    fptosi = 4,
    uitofp = 5,
    sitofp = 6,
    fptrunc = 7,
    fpext = 8,
    ptrtoint = 9,
    inttoptr = 10,
    bitcast = 11,
    addrspacecast = 12,
};

pub const CmpPredicate = enum(u6) {
    fcmp_false = 0,
    fcmp_oeq = 1,
    fcmp_ogt = 2,
    fcmp_oge = 3,
    fcmp_olt = 4,
    fcmp_ole = 5,
    fcmp_one = 6,
    fcmp_ord = 7,
    fcmp_uno = 8,
    fcmp_ueq = 9,
    fcmp_ugt = 10,
    fcmp_uge = 11,
    fcmp_ult = 12,
    fcmp_ule = 13,
    fcmp_une = 14,
    fcmp_true = 15,
    icmp_eq = 32,
    icmp_ne = 33,
    icmp_ugt = 34,
    icmp_uge = 35,
    icmp_ult = 36,
    icmp_ule = 37,
    icmp_sgt = 38,
    icmp_sge = 39,
    icmp_slt = 40,
    icmp_sle = 41,
};

pub const Type = enum(u32) {
    void,
    half,
    bfloat,
    float,
    double,
    fp128,
    x86_fp80,
    ppc_fp128,
    x86_amx,
    x86_mmx,
    label,
    token,
    metadata,

    i1,
    i8,
    i16,
    i29,
    i32,
    i64,
    i80,
    i128,
    ptr,
    @"ptr addrspace(4)",

    none = maxInt(u32),
    _,

    pub const ptr_amdgpu_constant =
        @field(Type, std.fmt.comptimePrint("ptr{f}", .{AddrSpace.amdgpu.constant.fmt(" ")}));

    pub const Tag = enum(u4) {
        simple,
        function,
        vararg_function,
        integer,
        pointer,
        target,
        vector,
        scalable_vector,
        small_array,
        array,
        structure,
        packed_structure,
        named_structure,
    };

    pub const Simple = enum(u5) {
        const Code = ir.ModuleBlock.TypeBlock.Code;
        void = @intFromEnum(Code.VOID),
        half = @intFromEnum(Code.HALF),
        bfloat = @intFromEnum(Code.BFLOAT),
        float = @intFromEnum(Code.FLOAT),
        double = @intFromEnum(Code.DOUBLE),
        fp128 = @intFromEnum(Code.FP128),
        x86_fp80 = @intFromEnum(Code.X86_FP80),
        ppc_fp128 = @intFromEnum(Code.PPC_FP128),
        x86_amx = @intFromEnum(Code.X86_AMX),
        x86_mmx = @intFromEnum(Code.X86_MMX),
        label = @intFromEnum(Code.LABEL),
        token = @intFromEnum(Code.TOKEN),
        metadata = @intFromEnum(Code.METADATA),
    };

    pub const Function = struct {
        ret: Type,
        params_len: u32,
        //params: [params_len]Value,

        pub const Kind = enum { normal, vararg };
    };

    pub const Target = extern struct {
        name: String,
        types_len: u32,
        ints_len: u32,
        //types: [types_len]Type,
        //ints: [ints_len]u32,
    };

    pub const Vector = extern struct {
        len: u32,
        child: Type,

        fn length(self: Vector) u32 {
            return self.len;
        }

        pub const Kind = enum { normal, scalable };
    };

    pub const Array = extern struct {
        len_lo: u32,
        len_hi: u32,
        child: Type,

        fn length(self: Array) u64 {
            return @as(u64, self.len_hi) << 32 | self.len_lo;
        }
    };

    pub const Structure = struct {
        fields_len: u32,
        //fields: [fields_len]Type,

        pub const Kind = enum { normal, @"packed" };
    };

    pub const NamedStructure = struct {
        id: String,
        body: Type,
    };

    pub const Item = packed struct(u32) {
        tag: Tag,
        data: ExtraIndex,

        pub const ExtraIndex = u28;
    };

    pub fn tag(self: Type, builder: *const Builder) Tag {
        return builder.type_items.items[@intFromEnum(self)].tag;
    }

    pub fn unnamedTag(self: Type, builder: *const Builder) Tag {
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .named_structure => builder.typeExtraData(Type.NamedStructure, item.data).body
                .unnamedTag(builder),
            else => item.tag,
        };
    }

    pub fn scalarTag(self: Type, builder: *const Builder) Tag {
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .vector, .scalable_vector => builder.typeExtraData(Type.Vector, item.data)
                .child.tag(builder),
            else => item.tag,
        };
    }

    pub fn isFloatingPoint(self: Type) bool {
        return switch (self) {
            .half, .bfloat, .float, .double, .fp128, .x86_fp80, .ppc_fp128 => true,
            else => false,
        };
    }

    pub fn isInteger(self: Type, builder: *const Builder) bool {
        return switch (self) {
            .i1, .i8, .i16, .i29, .i32, .i64, .i80, .i128 => true,
            else => switch (self.tag(builder)) {
                .integer => true,
                else => false,
            },
        };
    }

    pub fn isPointer(self: Type, builder: *const Builder) bool {
        return switch (self) {
            .ptr => true,
            else => switch (self.tag(builder)) {
                .pointer => true,
                else => false,
            },
        };
    }

    pub fn pointerAddrSpace(self: Type, builder: *const Builder) AddrSpace {
        switch (self) {
            .ptr => return .default,
            else => {
                const item = builder.type_items.items[@intFromEnum(self)];
                assert(item.tag == .pointer);
                return @enumFromInt(item.data);
            },
        }
    }

    pub fn isFunction(self: Type, builder: *const Builder) bool {
        return switch (self.tag(builder)) {
            .function, .vararg_function => true,
            else => false,
        };
    }

    pub fn functionKind(self: Type, builder: *const Builder) Type.Function.Kind {
        return switch (self.tag(builder)) {
            .function => .normal,
            .vararg_function => .vararg,
            else => unreachable,
        };
    }

    pub fn functionParameters(self: Type, builder: *const Builder) []const Type {
        const item = builder.type_items.items[@intFromEnum(self)];
        switch (item.tag) {
            .function,
            .vararg_function,
            => {
                var extra = builder.typeExtraDataTrail(Type.Function, item.data);
                return extra.trail.next(extra.data.params_len, Type, builder);
            },
            else => unreachable,
        }
    }

    pub fn functionReturn(self: Type, builder: *const Builder) Type {
        const item = builder.type_items.items[@intFromEnum(self)];
        switch (item.tag) {
            .function,
            .vararg_function,
            => return builder.typeExtraData(Type.Function, item.data).ret,
            else => unreachable,
        }
    }

    pub fn isVector(self: Type, builder: *const Builder) bool {
        return switch (self.tag(builder)) {
            .vector, .scalable_vector => true,
            else => false,
        };
    }

    pub fn vectorKind(self: Type, builder: *const Builder) Type.Vector.Kind {
        return switch (self.tag(builder)) {
            .vector => .normal,
            .scalable_vector => .scalable,
            else => unreachable,
        };
    }

    pub fn isStruct(self: Type, builder: *const Builder) bool {
        return switch (self.tag(builder)) {
            .structure, .packed_structure, .named_structure => true,
            else => false,
        };
    }

    pub fn structKind(self: Type, builder: *const Builder) Type.Structure.Kind {
        return switch (self.unnamedTag(builder)) {
            .structure => .normal,
            .packed_structure => .@"packed",
            else => unreachable,
        };
    }

    pub fn isAggregate(self: Type, builder: *const Builder) bool {
        return switch (self.tag(builder)) {
            .small_array, .array, .structure, .packed_structure, .named_structure => true,
            else => false,
        };
    }

    pub fn scalarBits(self: Type, builder: *const Builder) u24 {
        return switch (self) {
            .void, .label, .token, .metadata, .none, .x86_amx => unreachable,
            .i1 => 1,
            .i8 => 8,
            .half, .bfloat, .i16 => 16,
            .i29 => 29,
            .float, .i32 => 32,
            .double, .i64, .x86_mmx => 64,
            .x86_fp80, .i80 => 80,
            .fp128, .ppc_fp128, .i128 => 128,
            .ptr, .@"ptr addrspace(4)" => @panic("TODO: query data layout"),
            _ => {
                const item = builder.type_items.items[@intFromEnum(self)];
                return switch (item.tag) {
                    .simple,
                    .function,
                    .vararg_function,
                    => unreachable,
                    .integer => @intCast(item.data),
                    .pointer => @panic("TODO: query data layout"),
                    .target => unreachable,
                    .vector,
                    .scalable_vector,
                    => builder.typeExtraData(Type.Vector, item.data).child.scalarBits(builder),
                    .small_array,
                    .array,
                    .structure,
                    .packed_structure,
                    .named_structure,
                    => unreachable,
                };
            },
        };
    }

    pub fn childType(self: Type, builder: *const Builder) Type {
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .vector,
            .scalable_vector,
            .small_array,
            => builder.typeExtraData(Type.Vector, item.data).child,
            .array => builder.typeExtraData(Type.Array, item.data).child,
            .named_structure => builder.typeExtraData(Type.NamedStructure, item.data).body,
            else => unreachable,
        };
    }

    pub fn scalarType(self: Type, builder: *const Builder) Type {
        if (self.isFloatingPoint()) return self;
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .integer,
            .pointer,
            => self,
            .vector,
            .scalable_vector,
            => builder.typeExtraData(Type.Vector, item.data).child,
            else => unreachable,
        };
    }

    pub fn changeScalar(self: Type, scalar: Type, builder: *Builder) Allocator.Error!Type {
        try builder.ensureUnusedTypeCapacity(1, Type.Vector, 0);
        return self.changeScalarAssumeCapacity(scalar, builder);
    }

    pub fn changeScalarAssumeCapacity(self: Type, scalar: Type, builder: *Builder) Type {
        if (self.isFloatingPoint()) return scalar;
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .integer,
            .pointer,
            => scalar,
            inline .vector,
            .scalable_vector,
            => |kind| builder.vectorTypeAssumeCapacity(
                switch (kind) {
                    .vector => .normal,
                    .scalable_vector => .scalable,
                    else => unreachable,
                },
                builder.typeExtraData(Type.Vector, item.data).len,
                scalar,
            ),
            else => unreachable,
        };
    }

    pub fn vectorLen(self: Type, builder: *const Builder) u32 {
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .vector,
            .scalable_vector,
            => builder.typeExtraData(Type.Vector, item.data).len,
            else => unreachable,
        };
    }

    pub fn changeLength(self: Type, len: u32, builder: *Builder) Allocator.Error!Type {
        try builder.ensureUnusedTypeCapacity(1, Type.Array, 0);
        return self.changeLengthAssumeCapacity(len, builder);
    }

    pub fn changeLengthAssumeCapacity(self: Type, len: u32, builder: *Builder) Type {
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            inline .vector,
            .scalable_vector,
            => |kind| builder.vectorTypeAssumeCapacity(
                switch (kind) {
                    .vector => .normal,
                    .scalable_vector => .scalable,
                    else => unreachable,
                },
                len,
                builder.typeExtraData(Type.Vector, item.data).child,
            ),
            .small_array => builder.arrayTypeAssumeCapacity(
                len,
                builder.typeExtraData(Type.Vector, item.data).child,
            ),
            .array => builder.arrayTypeAssumeCapacity(
                len,
                builder.typeExtraData(Type.Array, item.data).child,
            ),
            else => unreachable,
        };
    }

    pub fn aggregateLen(self: Type, builder: *const Builder) usize {
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .vector,
            .scalable_vector,
            .small_array,
            => builder.typeExtraData(Type.Vector, item.data).len,
            .array => @intCast(builder.typeExtraData(Type.Array, item.data).length()),
            .structure,
            .packed_structure,
            => builder.typeExtraData(Type.Structure, item.data).fields_len,
            .named_structure => builder.typeExtraData(Type.NamedStructure, item.data).body
                .aggregateLen(builder),
            else => unreachable,
        };
    }

    pub fn structFields(self: Type, builder: *const Builder) []const Type {
        const item = builder.type_items.items[@intFromEnum(self)];
        switch (item.tag) {
            .structure,
            .packed_structure,
            => {
                var extra = builder.typeExtraDataTrail(Type.Structure, item.data);
                return extra.trail.next(extra.data.fields_len, Type, builder);
            },
            .named_structure => return builder.typeExtraData(Type.NamedStructure, item.data).body
                .structFields(builder),
            else => unreachable,
        }
    }

    pub fn childTypeAt(self: Type, indices: []const u32, builder: *const Builder) Type {
        if (indices.len == 0) return self;
        const item = builder.type_items.items[@intFromEnum(self)];
        return switch (item.tag) {
            .small_array => builder.typeExtraData(Type.Vector, item.data).child
                .childTypeAt(indices[1..], builder),
            .array => builder.typeExtraData(Type.Array, item.data).child
                .childTypeAt(indices[1..], builder),
            .structure,
            .packed_structure,
            => {
                var extra = builder.typeExtraDataTrail(Type.Structure, item.data);
                const fields = extra.trail.next(extra.data.fields_len, Type, builder);
                return fields[indices[0]].childTypeAt(indices[1..], builder);
            },
            .named_structure => builder.typeExtraData(Type.NamedStructure, item.data).body
                .childTypeAt(indices, builder),
            else => unreachable,
        };
    }

    pub fn targetLayoutType(self: Type, builder: *const Builder) Type {
        _ = self;
        _ = builder;
        @panic("TODO: implement targetLayoutType");
    }

    pub fn isSized(self: Type, builder: *const Builder) Allocator.Error!bool {
        var visited: IsSizedVisited = .{};
        defer visited.deinit(builder.gpa);
        const result = try self.isSizedVisited(&visited, builder);
        return result;
    }

    const FormatData = struct {
        type: Type,
        builder: *const Builder,
        mode: Mode,

        const Mode = enum { default, m, lt, gt, percent };
    };
    fn format(data: FormatData, w: *Writer) Writer.Error!void {
        assert(data.type != .none);
        if (data.mode == .m) {
            const item = data.builder.type_items.items[@intFromEnum(data.type)];
            switch (item.tag) {
                .simple => try w.writeAll(switch (@as(Simple, @enumFromInt(item.data))) {
                    .void => "isVoid",
                    .half => "f16",
                    .bfloat => "bf16",
                    .float => "f32",
                    .double => "f64",
                    .fp128 => "f128",
                    .x86_fp80 => "f80",
                    .ppc_fp128 => "ppcf128",
                    .x86_amx => "x86amx",
                    .x86_mmx => "x86mmx",
                    .label, .token => unreachable,
                    .metadata => "Metadata",
                }),
                .function, .vararg_function => |kind| {
                    var extra = data.builder.typeExtraDataTrail(Type.Function, item.data);
                    const params = extra.trail.next(extra.data.params_len, Type, data.builder);
                    try w.print("f_{f}", .{extra.data.ret.fmt(data.builder, .m)});
                    for (params) |param| try w.print("{f}", .{param.fmt(data.builder, .m)});
                    switch (kind) {
                        .function => {},
                        .vararg_function => try w.writeAll("vararg"),
                        else => unreachable,
                    }
                    try w.writeByte('f');
                },
                .integer => try w.print("i{d}", .{item.data}),
                .pointer => try w.print("p{d}", .{item.data}),
                .target => {
                    var extra = data.builder.typeExtraDataTrail(Type.Target, item.data);
                    const types = extra.trail.next(extra.data.types_len, Type, data.builder);
                    const ints = extra.trail.next(extra.data.ints_len, u32, data.builder);
                    try w.print("t{s}", .{extra.data.name.slice(data.builder).?});
                    for (types) |ty| try w.print("_{f}", .{ty.fmt(data.builder, .m)});
                    for (ints) |int| try w.print("_{d}", .{int});
                    try w.writeByte('t');
                },
                .vector, .scalable_vector => |kind| {
                    const extra = data.builder.typeExtraData(Type.Vector, item.data);
                    try w.print("{s}v{d}{f}", .{
                        switch (kind) {
                            .vector => "",
                            .scalable_vector => "nx",
                            else => unreachable,
                        },
                        extra.len,
                        extra.child.fmt(data.builder, .m),
                    });
                },
                inline .small_array, .array => |kind| {
                    const extra = data.builder.typeExtraData(switch (kind) {
                        .small_array => Type.Vector,
                        .array => Type.Array,
                        else => unreachable,
                    }, item.data);
                    try w.print("a{d}{f}", .{ extra.length(), extra.child.fmt(data.builder, .m) });
                },
                .structure, .packed_structure => {
                    var extra = data.builder.typeExtraDataTrail(Type.Structure, item.data);
                    const fields = extra.trail.next(extra.data.fields_len, Type, data.builder);
                    try w.writeAll("sl_");
                    for (fields) |field| try w.print("{f}", .{field.fmt(data.builder, .m)});
                    try w.writeByte('s');
                },
                .named_structure => {
                    const extra = data.builder.typeExtraData(Type.NamedStructure, item.data);
                    try w.writeAll("s_");
                    if (extra.id.slice(data.builder)) |id| try w.writeAll(id);
                },
            }
            return;
        }
        if (std.enums.tagName(Type, data.type)) |name| return w.writeAll(name);
        const item = data.builder.type_items.items[@intFromEnum(data.type)];
        switch (item.tag) {
            .simple => unreachable,
            .function, .vararg_function => |kind| {
                var extra = data.builder.typeExtraDataTrail(Type.Function, item.data);
                const params = extra.trail.next(extra.data.params_len, Type, data.builder);
                if (data.mode != .gt)
                    try w.print("{f} ", .{extra.data.ret.fmt(data.builder, .percent)});
                if (data.mode != .lt) {
                    try w.writeByte('(');
                    for (params, 0..) |param, index| {
                        if (index > 0) try w.writeAll(", ");
                        try w.print("{f}", .{param.fmt(data.builder, .percent)});
                    }
                    switch (kind) {
                        .function => {},
                        .vararg_function => {
                            if (params.len > 0) try w.writeAll(", ");
                            try w.writeAll("...");
                        },
                        else => unreachable,
                    }
                    try w.writeByte(')');
                }
            },
            .integer => try w.print("i{d}", .{item.data}),
            .pointer => try w.print("ptr{f}", .{@as(AddrSpace, @enumFromInt(item.data)).fmt(" ")}),
            .target => {
                var extra = data.builder.typeExtraDataTrail(Type.Target, item.data);
                const types = extra.trail.next(extra.data.types_len, Type, data.builder);
                const ints = extra.trail.next(extra.data.ints_len, u32, data.builder);
                try w.print(
                    \\target({f}
                , .{extra.data.name.fmtQ(data.builder)});
                for (types) |ty| try w.print(", {f}", .{ty.fmt(data.builder, .percent)});
                for (ints) |int| try w.print(", {d}", .{int});
                try w.writeByte(')');
            },
            .vector, .scalable_vector => |kind| {
                const extra = data.builder.typeExtraData(Type.Vector, item.data);
                try w.print("<{s}{d} x {f}>", .{
                    switch (kind) {
                        .vector => "",
                        .scalable_vector => "vscale x ",
                        else => unreachable,
                    },
                    extra.len,
                    extra.child.fmt(data.builder, .percent),
                });
            },
            inline .small_array, .array => |kind| {
                const extra = data.builder.typeExtraData(switch (kind) {
                    .small_array => Type.Vector,
                    .array => Type.Array,
                    else => unreachable,
                }, item.data);
                try w.print("[{d} x {f}]", .{ extra.length(), extra.child.fmt(data.builder, .percent) });
            },
            .structure, .packed_structure => |kind| {
                var extra = data.builder.typeExtraDataTrail(Type.Structure, item.data);
                const fields = extra.trail.next(extra.data.fields_len, Type, data.builder);
                switch (kind) {
                    .structure => {},
                    .packed_structure => try w.writeByte('<'),
                    else => unreachable,
                }
                try w.writeAll("{ ");
                for (fields, 0..) |field, index| {
                    if (index > 0) try w.writeAll(", ");
                    try w.print("{f}", .{field.fmt(data.builder, .percent)});
                }
                try w.writeAll(" }");
                switch (kind) {
                    .structure => {},
                    .packed_structure => try w.writeByte('>'),
                    else => unreachable,
                }
            },
            .named_structure => {
                const extra = data.builder.typeExtraData(Type.NamedStructure, item.data);
                if (data.mode == .percent) try w.print("%{f}", .{
                    extra.id.fmt(data.builder),
                }) else switch (extra.body) {
                    .none => try w.writeAll("opaque"),
                    else => try format(.{
                        .type = extra.body,
                        .builder = data.builder,
                        .mode = data.mode,
                    }, w),
                }
            },
        }
    }
    pub fn fmt(self: Type, builder: *const Builder, mode: FormatData.Mode) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{ .type = self, .builder = builder, .mode = mode } };
    }

    const IsSizedVisited = std.AutoHashMapUnmanaged(Type, void);
    fn isSizedVisited(
        self: Type,
        visited: *IsSizedVisited,
        builder: *const Builder,
    ) Allocator.Error!bool {
        return switch (self) {
            .void,
            .label,
            .token,
            .metadata,
            => false,
            .half,
            .bfloat,
            .float,
            .double,
            .fp128,
            .x86_fp80,
            .ppc_fp128,
            .x86_amx,
            .x86_mmx,
            .i1,
            .i8,
            .i16,
            .i29,
            .i32,
            .i64,
            .i80,
            .i128,
            .ptr,
            .@"ptr addrspace(4)",
            => true,
            .none => unreachable,
            _ => {
                const item = builder.type_items.items[@intFromEnum(self)];
                return switch (item.tag) {
                    .simple => unreachable,
                    .function,
                    .vararg_function,
                    => false,
                    .integer,
                    .pointer,
                    => true,
                    .target => self.targetLayoutType(builder).isSizedVisited(visited, builder),
                    .vector,
                    .scalable_vector,
                    .small_array,
                    => builder.typeExtraData(Type.Vector, item.data)
                        .child.isSizedVisited(visited, builder),
                    .array => builder.typeExtraData(Type.Array, item.data)
                        .child.isSizedVisited(visited, builder),
                    .structure,
                    .packed_structure,
                    => {
                        if (try visited.fetchPut(builder.gpa, self, {})) |_| return false;

                        var extra = builder.typeExtraDataTrail(Type.Structure, item.data);
                        const fields = extra.trail.next(extra.data.fields_len, Type, builder);
                        for (fields) |field| {
                            if (field.isVector(builder) and field.vectorKind(builder) == .scalable)
                                return false;
                            if (!try field.isSizedVisited(visited, builder))
                                return false;
                        }
                        return true;
                    },
                    .named_structure => {
                        const body = builder.typeExtraData(Type.NamedStructure, item.data).body;
                        return body != .none and try body.isSizedVisited(visited, builder);
                    },
                };
            },
        };
    }
};

pub const Attribute = union(Kind) {
    // Parameter Attributes
    zeroext,
    signext,
    inreg,
    byval: Type,
    byref: Type,
    preallocated: Type,
    inalloca: Type,
    sret: Type,
    elementtype: Type,
    @"align": Alignment.Lazy,
    @"noalias",
    nocapture,
    nofree,
    nest,
    returned,
    nonnull,
    dereferenceable: u32,
    dereferenceable_or_null: u32,
    swiftself,
    swiftasync,
    swifterror,
    immarg,
    noundef,
    nofpclass: FpClass,
    alignstack: Alignment.Lazy,
    allocalign,
    allocptr,
    readnone,
    readonly,
    writeonly,

    // Function Attributes
    //alignstack: Alignment.Lazy,
    allockind: AllocKind,
    allocsize: AllocSize,
    alwaysinline,
    builtin,
    cold,
    convergent,
    disable_sanitizer_information,
    fn_ret_thunk_extern,
    hot,
    inlinehint,
    jumptable,
    memory: Memory,
    minsize,
    naked,
    nobuiltin,
    nocallback,
    noduplicate,
    //nofree,
    noimplicitfloat,
    @"noinline",
    nomerge,
    nonlazybind,
    noprofile,
    skipprofile,
    noredzone,
    noreturn,
    norecurse,
    willreturn,
    nosync,
    nounwind,
    nosanitize_bounds,
    nosanitize_coverage,
    null_pointer_is_valid,
    optforfuzzing,
    optnone,
    optsize,
    //preallocated: Type,
    returns_twice,
    safestack,
    sanitize_address,
    sanitize_memory,
    sanitize_thread,
    sanitize_hwaddress,
    sanitize_memtag,
    speculative_load_hardening,
    speculatable,
    ssp,
    sspstrong,
    sspreq,
    strictfp,
    uwtable: UwTable,
    nocf_check,
    shadowcallstack,
    mustprogress,
    vscale_range: VScaleRange,

    // Global Attributes
    no_sanitize_address,
    no_sanitize_hwaddress,
    //sanitize_memtag,
    sanitize_address_dyninit,

    string: struct { kind: String, value: String },
    none: noreturn,

    pub const Index = enum(u32) {
        _,

        pub fn getKind(self: Index, builder: *const Builder) Kind {
            return self.toStorage(builder).kind;
        }

        pub fn toAttribute(self: Index, builder: *const Builder) Attribute {
            @setEvalBranchQuota(2_000);
            const storage = self.toStorage(builder);
            if (storage.kind.toString()) |kind| return .{ .string = .{
                .kind = kind,
                .value = @enumFromInt(storage.value),
            } } else return switch (storage.kind) {
                inline .zeroext,
                .signext,
                .inreg,
                .byval,
                .byref,
                .preallocated,
                .inalloca,
                .sret,
                .elementtype,
                .@"align",
                .@"noalias",
                .nocapture,
                .nofree,
                .nest,
                .returned,
                .nonnull,
                .dereferenceable,
                .dereferenceable_or_null,
                .swiftself,
                .swiftasync,
                .swifterror,
                .immarg,
                .noundef,
                .nofpclass,
                .alignstack,
                .allocalign,
                .allocptr,
                .readnone,
                .readonly,
                .writeonly,
                //.alignstack,
                .allockind,
                .allocsize,
                .alwaysinline,
                .builtin,
                .cold,
                .convergent,
                .disable_sanitizer_information,
                .fn_ret_thunk_extern,
                .hot,
                .inlinehint,
                .jumptable,
                .memory,
                .minsize,
                .naked,
                .nobuiltin,
                .nocallback,
                .noduplicate,
                //.nofree,
                .noimplicitfloat,
                .@"noinline",
                .nomerge,
                .nonlazybind,
                .noprofile,
                .skipprofile,
                .noredzone,
                .noreturn,
                .norecurse,
                .willreturn,
                .nosync,
                .nounwind,
                .nosanitize_bounds,
                .nosanitize_coverage,
                .null_pointer_is_valid,
                .optforfuzzing,
                .optnone,
                .optsize,
                //.preallocated,
                .returns_twice,
                .safestack,
                .sanitize_address,
                .sanitize_memory,
                .sanitize_thread,
                .sanitize_hwaddress,
                .sanitize_memtag,
                .speculative_load_hardening,
                .speculatable,
                .ssp,
                .sspstrong,
                .sspreq,
                .strictfp,
                .uwtable,
                .nocf_check,
                .shadowcallstack,
                .mustprogress,
                .vscale_range,
                .no_sanitize_address,
                .no_sanitize_hwaddress,
                .sanitize_address_dyninit,
                => |kind| {
                    const field = comptime blk: {
                        @setEvalBranchQuota(10_000);
                        for (@typeInfo(Attribute).@"union".fields) |field| {
                            if (std.mem.eql(u8, field.name, @tagName(kind))) break :blk field;
                        }
                        unreachable;
                    };
                    comptime assert(std.mem.eql(u8, @tagName(kind), field.name));
                    return @unionInit(Attribute, field.name, switch (field.type) {
                        void => {},
                        u32 => storage.value,
                        Alignment.Lazy, String, Type, UwTable => @enumFromInt(storage.value),
                        AllocKind, AllocSize, FpClass, Memory, VScaleRange => @bitCast(storage.value),
                        else => @compileError("bad payload type: " ++ field.name ++ ": " ++
                            @typeName(field.type)),
                    });
                },
                .string, .none => unreachable,
                _ => unreachable,
            };
        }

        const FormatData = struct {
            attribute_index: Index,
            builder: *const Builder,
            flags: Flags = .{},
            const Flags = struct {
                pound: bool = false,
                quote: bool = false,
            };
        };
        fn format(data: FormatData, w: *Writer) Writer.Error!void {
            const attribute = data.attribute_index.toAttribute(data.builder);
            switch (attribute) {
                .zeroext,
                .signext,
                .inreg,
                .@"noalias",
                .nocapture,
                .nofree,
                .nest,
                .returned,
                .nonnull,
                .swiftself,
                .swiftasync,
                .swifterror,
                .immarg,
                .noundef,
                .allocalign,
                .allocptr,
                .readnone,
                .readonly,
                .writeonly,
                .alwaysinline,
                .builtin,
                .cold,
                .convergent,
                .disable_sanitizer_information,
                .fn_ret_thunk_extern,
                .hot,
                .inlinehint,
                .jumptable,
                .minsize,
                .naked,
                .nobuiltin,
                .nocallback,
                .noduplicate,
                .noimplicitfloat,
                .@"noinline",
                .nomerge,
                .nonlazybind,
                .noprofile,
                .skipprofile,
                .noredzone,
                .noreturn,
                .norecurse,
                .willreturn,
                .nosync,
                .nounwind,
                .nosanitize_bounds,
                .nosanitize_coverage,
                .null_pointer_is_valid,
                .optforfuzzing,
                .optnone,
                .optsize,
                .returns_twice,
                .safestack,
                .sanitize_address,
                .sanitize_memory,
                .sanitize_thread,
                .sanitize_hwaddress,
                .sanitize_memtag,
                .speculative_load_hardening,
                .speculatable,
                .ssp,
                .sspstrong,
                .sspreq,
                .strictfp,
                .nocf_check,
                .shadowcallstack,
                .mustprogress,
                .no_sanitize_address,
                .no_sanitize_hwaddress,
                .sanitize_address_dyninit,
                => try w.print(" {s}", .{@tagName(attribute)}),
                .byval,
                .byref,
                .preallocated,
                .inalloca,
                .sret,
                .elementtype,
                => |ty| try w.print(" {s}({f})", .{ @tagName(attribute), ty.fmt(data.builder, .percent) }),
                .@"align" => |alignment| try w.print("{f}", .{alignment.resolve(data.builder).fmt(" ")}),
                .dereferenceable,
                .dereferenceable_or_null,
                => |size| try w.print(" {s}({d})", .{ @tagName(attribute), size }),
                .nofpclass => |fpclass| {
                    const Int = @typeInfo(FpClass).@"struct".backing_integer.?;
                    try w.print(" {s}(", .{@tagName(attribute)});
                    var any = false;
                    var remaining: Int = @bitCast(fpclass);
                    inline for (@typeInfo(FpClass).@"struct".decls) |decl| {
                        const pattern: Int = @bitCast(@field(FpClass, decl.name));
                        if (remaining & pattern == pattern) {
                            if (!any) {
                                try w.writeByte(' ');
                                any = true;
                            }
                            try w.writeAll(decl.name);
                            remaining &= ~pattern;
                        }
                    }
                    try w.writeByte(')');
                },
                .alignstack => |alignment| {
                    try w.print(" {t}", .{attribute});
                    const alignment_bytes = alignment.resolve(data.builder).toByteUnits() orelse return;
                    if (data.flags.pound) {
                        try w.print("={d}", .{alignment_bytes});
                    } else {
                        try w.print("({d})", .{alignment_bytes});
                    }
                },
                .allockind => |allockind| {
                    try w.print(" {t}(\"", .{attribute});
                    var any = false;
                    inline for (@typeInfo(AllocKind).@"struct".fields) |field| {
                        if (comptime std.mem.eql(u8, field.name, "_")) continue;
                        if (@field(allockind, field.name)) {
                            if (!any) {
                                try w.writeByte(',');
                                any = true;
                            }
                            try w.writeAll(field.name);
                        }
                    }
                    try w.writeAll("\")");
                },
                .allocsize => |allocsize| {
                    try w.print(" {t}({d}", .{ attribute, allocsize.elem_size });
                    if (allocsize.num_elems != AllocSize.none)
                        try w.print(",{d}", .{allocsize.num_elems});
                    try w.writeByte(')');
                },
                .memory => |memory| {
                    try w.print(" {t}(", .{attribute});
                    var any = memory.other != .none or
                        (memory.argmem == .none and memory.inaccessiblemem == .none);
                    if (any) try w.writeAll(@tagName(memory.other));
                    inline for (.{ "argmem", "inaccessiblemem" }) |kind| {
                        if (@field(memory, kind) != memory.other) {
                            if (any) try w.writeAll(", ");
                            try w.print("{s}: {s}", .{ kind, @tagName(@field(memory, kind)) });
                            any = true;
                        }
                    }
                    try w.writeByte(')');
                },
                .uwtable => |uwtable| if (uwtable != .none) {
                    try w.print(" {s}", .{@tagName(attribute)});
                    if (uwtable != UwTable.default) try w.print("({s})", .{@tagName(uwtable)});
                },
                .vscale_range => |vscale_range| try w.print(" {s}({d},{d})", .{
                    @tagName(attribute),
                    vscale_range.min.toByteUnits().?,
                    vscale_range.max.toByteUnits() orelse 0,
                }),
                .string => |string_attr| if (data.flags.quote) {
                    try w.print(" {f}", .{string_attr.kind.fmtQ(data.builder)});
                    if (string_attr.value != .empty)
                        try w.print("={f}", .{string_attr.value.fmtQ(data.builder)});
                },
                .none => unreachable,
            }
        }
        pub fn fmt(self: Index, builder: *const Builder, flags: FormatData.Flags) std.fmt.Alt(FormatData, format) {
            return .{ .data = .{ .attribute_index = self, .builder = builder, .flags = flags } };
        }

        fn toStorage(self: Index, builder: *const Builder) Storage {
            return builder.attributes.keys()[@intFromEnum(self)];
        }
    };

    pub const Kind = enum(u32) {
        // Parameter Attributes
        zeroext = 34,
        signext = 24,
        inreg = 5,
        byval = 3,
        byref = 69,
        preallocated = 65,
        inalloca = 38,
        sret = 29, // TODO: ?
        elementtype = 77,
        @"align" = 1,
        @"noalias" = 9,
        nocapture = 11,
        nofree = 62,
        nest = 8,
        returned = 22,
        nonnull = 39,
        dereferenceable = 41,
        dereferenceable_or_null = 42,
        swiftself = 46,
        swiftasync = 75,
        swifterror = 47,
        immarg = 60,
        noundef = 68,
        nofpclass = 87,
        alignstack = 25,
        allocalign = 80,
        allocptr = 81,
        readnone = 20,
        readonly = 21,
        writeonly = 52,

        // Function Attributes
        //alignstack,
        allockind = 82,
        allocsize = 51,
        alwaysinline = 2,
        builtin = 35,
        cold = 36,
        convergent = 43,
        disable_sanitizer_information = 78,
        fn_ret_thunk_extern = 84,
        hot = 72,
        inlinehint = 4,
        jumptable = 40,
        memory = 86,
        minsize = 6,
        naked = 7,
        nobuiltin = 10,
        nocallback = 71,
        noduplicate = 12,
        //nofree,
        noimplicitfloat = 13,
        @"noinline" = 14,
        nomerge = 66,
        nonlazybind = 15,
        noprofile = 73,
        skipprofile = 85,
        noredzone = 16,
        noreturn = 17,
        norecurse = 48,
        willreturn = 61,
        nosync = 63,
        nounwind = 18,
        nosanitize_bounds = 79,
        nosanitize_coverage = 76,
        null_pointer_is_valid = 67,
        optforfuzzing = 57,
        optnone = 37,
        optsize = 19,
        //preallocated,
        returns_twice = 23,
        safestack = 44,
        sanitize_address = 30,
        sanitize_memory = 32,
        sanitize_thread = 31,
        sanitize_hwaddress = 55,
        sanitize_memtag = 64,
        speculative_load_hardening = 59,
        speculatable = 53,
        ssp = 26,
        sspstrong = 28,
        sspreq = 27,
        strictfp = 54,
        uwtable = 33,
        nocf_check = 56,
        shadowcallstack = 58,
        mustprogress = 70,
        vscale_range = 74,

        // Global Attributes
        no_sanitize_address = 100,
        no_sanitize_hwaddress = 101,
        //sanitize_memtag,
        sanitize_address_dyninit = 102,

        string = maxInt(u31),
        none = maxInt(u32),
        _,

        pub const len = @typeInfo(Kind).@"enum".fields.len - 2;

        pub fn fromString(str: String) Kind {
            assert(!str.isAnon());
            const kind: Kind = @enumFromInt(@intFromEnum(str));
            assert(kind != .none);
            return kind;
        }

        fn toString(self: Kind) ?String {
            assert(self != .none);
            const str: String = @enumFromInt(@intFromEnum(self));
            return if (str.isAnon()) null else str;
        }
    };

    pub const FpClass = packed struct(u32) {
        signaling_nan: bool = false,
        quiet_nan: bool = false,
        negative_infinity: bool = false,
        negative_normal: bool = false,
        negative_subnormal: bool = false,
        negative_zero: bool = false,
        positive_zero: bool = false,
        positive_subnormal: bool = false,
        positive_normal: bool = false,
        positive_infinity: bool = false,
        _: u22 = 0,

        pub const all = FpClass{
            .signaling_nan = true,
            .quiet_nan = true,
            .negative_infinity = true,
            .negative_normal = true,
            .negative_subnormal = true,
            .negative_zero = true,
            .positive_zero = true,
            .positive_subnormal = true,
            .positive_normal = true,
            .positive_infinity = true,
        };

        pub const nan = FpClass{ .signaling_nan = true, .quiet_nan = true };
        pub const snan = FpClass{ .signaling_nan = true };
        pub const qnan = FpClass{ .quiet_nan = true };

        pub const inf = FpClass{ .negative_infinity = true, .positive_infinity = true };
        pub const ninf = FpClass{ .negative_infinity = true };
        pub const pinf = FpClass{ .positive_infinity = true };

        pub const zero = FpClass{ .positive_zero = true, .negative_zero = true };
        pub const nzero = FpClass{ .negative_zero = true };
        pub const pzero = FpClass{ .positive_zero = true };

        pub const sub = FpClass{ .positive_subnormal = true, .negative_subnormal = true };
        pub const nsub = FpClass{ .negative_subnormal = true };
        pub const psub = FpClass{ .positive_subnormal = true };

        pub const norm = FpClass{ .positive_normal = true, .negative_normal = true };
        pub const nnorm = FpClass{ .negative_normal = true };
        pub const pnorm = FpClass{ .positive_normal = true };
    };

    pub const AllocKind = packed struct(u32) {
        alloc: bool,
        realloc: bool,
        free: bool,
        uninitialized: bool,
        zeroed: bool,
        aligned: bool,
        _: u26 = 0,
    };

    pub const AllocSize = packed struct(u32) {
        elem_size: u16,
        num_elems: u16,

        pub const none = maxInt(u16);

        fn toLlvm(self: AllocSize) packed struct(u64) { num_elems: u32, elem_size: u32 } {
            return .{ .num_elems = switch (self.num_elems) {
                else => self.num_elems,
                none => maxInt(u32),
            }, .elem_size = self.elem_size };
        }
    };

    pub const Memory = packed struct(u32) {
        argmem: Effect = .none,
        inaccessiblemem: Effect = .none,
        other: Effect = .none,
        _: u26 = 0,

        pub const Effect = enum(u2) { none, read, write, readwrite };

        fn all(effect: Effect) Memory {
            return .{ .argmem = effect, .inaccessiblemem = effect, .other = effect };
        }
    };

    pub const UwTable = enum(u32) {
        none,
        sync,
        async,

        pub const default = UwTable.async;
    };

    pub const VScaleRange = packed struct(u32) {
        min: Alignment,
        max: Alignment,
        _: u20 = 0,

        fn toLlvm(self: VScaleRange) packed struct(u64) { max: u32, min: u32 } {
            return .{
                .max = @intCast(self.max.toByteUnits() orelse 0),
                .min = @intCast(self.min.toByteUnits().?),
            };
        }
    };

    pub fn getKind(self: Attribute) Kind {
        return switch (self) {
            else => self,
            .string => |string_attr| Kind.fromString(string_attr.kind),
        };
    }

    const Storage = extern struct {
        kind: Kind,
        value: u32,
    };

    fn toStorage(self: Attribute) Storage {
        return switch (self) {
            inline else => |value, tag| .{ .kind = @as(Kind, self), .value = switch (@TypeOf(value)) {
                void => 0,
                u32 => value,
                Alignment.Lazy, String, Type, UwTable => @intFromEnum(value),
                AllocKind, AllocSize, FpClass, Memory, VScaleRange => @bitCast(value),
                else => @compileError("bad payload type: " ++ @tagName(tag) ++ @typeName(@TypeOf(value))),
            } },
            .string => |string_attr| .{
                .kind = Kind.fromString(string_attr.kind),
                .value = @intFromEnum(string_attr.value),
            },
            .none => unreachable,
        };
    }
};

pub const Attributes = enum(u32) {
    none,
    _,

    pub fn slice(self: Attributes, builder: *const Builder) []const Attribute.Index {
        const start = builder.attributes_indices.items[@intFromEnum(self)];
        const end = builder.attributes_indices.items[@intFromEnum(self) + 1];
        return @ptrCast(builder.attributes_extra.items[start..end]);
    }

    const FormatData = struct {
        attributes: Attributes,
        builder: *const Builder,
        flags: Flags = .{},
        const Flags = Attribute.Index.FormatData.Flags;
    };
    fn format(data: FormatData, w: *Writer) Writer.Error!void {
        for (data.attributes.slice(data.builder)) |attribute_index| try Attribute.Index.format(.{
            .attribute_index = attribute_index,
            .builder = data.builder,
            .flags = data.flags,
        }, w);
    }
    pub fn fmt(self: Attributes, builder: *const Builder, flags: FormatData.Flags) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{ .attributes = self, .builder = builder, .flags = flags } };
    }
};

pub const FunctionAttributes = enum(u32) {
    none,
    _,

    const function_index = 0;
    const return_index = 1;
    const params_index = 2;

    pub const Wip = struct {
        maps: Maps = .empty,

        const Map = std.AutoArrayHashMapUnmanaged(Attribute.Kind, Attribute.Index);
        const Maps = std.ArrayList(Map);

        pub fn deinit(self: *Wip, builder: *const Builder) void {
            for (self.maps.items) |*map| map.deinit(builder.gpa);
            self.maps.deinit(builder.gpa);
            self.* = undefined;
        }

        pub fn addFnAttr(self: *Wip, attribute: Attribute, builder: *Builder) Allocator.Error!void {
            try self.addAttr(function_index, attribute, builder);
        }

        pub fn addFnAttrIndex(
            self: *Wip,
            attribute_index: Attribute.Index,
            builder: *const Builder,
        ) Allocator.Error!void {
            try self.addAttrIndex(function_index, attribute_index, builder);
        }

        pub fn removeFnAttr(self: *Wip, attribute_kind: Attribute.Kind) Allocator.Error!bool {
            return self.removeAttr(function_index, attribute_kind);
        }

        pub fn addRetAttr(self: *Wip, attribute: Attribute, builder: *Builder) Allocator.Error!void {
            try self.addAttr(return_index, attribute, builder);
        }

        pub fn addRetAttrIndex(
            self: *Wip,
            attribute_index: Attribute.Index,
            builder: *const Builder,
        ) Allocator.Error!void {
            try self.addAttrIndex(return_index, attribute_index, builder);
        }

        pub fn removeRetAttr(self: *Wip, attribute_kind: Attribute.Kind) Allocator.Error!bool {
            return self.removeAttr(return_index, attribute_kind);
        }

        pub fn addParamAttr(
            self: *Wip,
            param_index: usize,
            attribute: Attribute,
            builder: *Builder,
        ) Allocator.Error!void {
            try self.addAttr(params_index + param_index, attribute, builder);
        }

        pub fn addParamAttrIndex(
            self: *Wip,
            param_index: usize,
            attribute_index: Attribute.Index,
            builder: *const Builder,
        ) Allocator.Error!void {
            try self.addAttrIndex(params_index + param_index, attribute_index, builder);
        }

        pub fn removeParamAttr(
            self: *Wip,
            param_index: usize,
            attribute_kind: Attribute.Kind,
        ) Allocator.Error!bool {
            return self.removeAttr(params_index + param_index, attribute_kind);
        }

        pub fn finish(self: *const Wip, builder: *Builder) Allocator.Error!FunctionAttributes {
            const attributes = try builder.gpa.alloc(Attributes, self.maps.items.len);
            defer builder.gpa.free(attributes);
            for (attributes, self.maps.items) |*attribute, map|
                attribute.* = try builder.attrs(map.values());
            return builder.fnAttrs(attributes);
        }

        fn addAttr(
            self: *Wip,
            index: usize,
            attribute: Attribute,
            builder: *Builder,
        ) Allocator.Error!void {
            const map = try self.getOrPutMap(builder.gpa, index);
            try map.put(builder.gpa, attribute.getKind(), try builder.attr(attribute));
        }

        fn addAttrIndex(
            self: *Wip,
            index: usize,
            attribute_index: Attribute.Index,
            builder: *const Builder,
        ) Allocator.Error!void {
            const map = try self.getOrPutMap(builder.gpa, index);
            try map.put(builder.gpa, attribute_index.getKind(builder), attribute_index);
        }

        fn removeAttr(self: *Wip, index: usize, attribute_kind: Attribute.Kind) Allocator.Error!bool {
            const map = self.getMap(index) orelse return false;
            return map.swapRemove(attribute_kind);
        }

        fn getOrPutMap(self: *Wip, allocator: Allocator, index: usize) Allocator.Error!*Map {
            if (index >= self.maps.items.len)
                try self.maps.appendNTimes(allocator, .{}, index + 1 - self.maps.items.len);
            return &self.maps.items[index];
        }

        fn getMap(self: *Wip, index: usize) ?*Map {
            return if (index >= self.maps.items.len) null else &self.maps.items[index];
        }

        fn ensureTotalLength(self: *Wip, new_len: usize) Allocator.Error!void {
            try self.maps.appendNTimes(
                .{},
                std.math.sub(usize, new_len, self.maps.items.len) catch return,
            );
        }
    };

    pub fn func(self: FunctionAttributes, builder: *const Builder) Attributes {
        return self.get(function_index, builder);
    }

    pub fn ret(self: FunctionAttributes, builder: *const Builder) Attributes {
        return self.get(return_index, builder);
    }

    pub fn param(self: FunctionAttributes, param_index: usize, builder: *const Builder) Attributes {
        return self.get(params_index + param_index, builder);
    }

    pub fn toWip(self: FunctionAttributes, builder: *const Builder) Allocator.Error!Wip {
        var wip: Wip = .{};
        errdefer wip.deinit(builder);
        const attributes_slice = self.slice(builder);
        try wip.maps.ensureTotalCapacityPrecise(builder.gpa, attributes_slice.len);
        for (attributes_slice) |attributes| {
            const map = wip.maps.addOneAssumeCapacity();
            map.* = .{};
            const attribute_slice = attributes.slice(builder);
            try map.ensureTotalCapacity(builder.gpa, attribute_slice.len);
            for (attributes.slice(builder)) |attribute|
                map.putAssumeCapacityNoClobber(attribute.getKind(builder), attribute);
        }
        return wip;
    }

    fn get(self: FunctionAttributes, index: usize, builder: *const Builder) Attributes {
        const attribute_slice = self.slice(builder);
        return if (index < attribute_slice.len) attribute_slice[index] else .none;
    }

    fn slice(self: FunctionAttributes, builder: *const Builder) []const Attributes {
        const start = builder.attributes_indices.items[@intFromEnum(self)];
        const end = builder.attributes_indices.items[@intFromEnum(self) + 1];
        return @ptrCast(builder.attributes_extra.items[start..end]);
    }
};

pub const Linkage = enum(u4) {
    private = 9,
    internal = 3,
    weak = 1,
    weak_odr = 10,
    linkonce = 4,
    linkonce_odr = 11,
    available_externally = 12,
    appending = 2,
    common = 8,
    extern_weak = 7,
    external = 0,

    pub fn format(self: Linkage, w: *Writer) Writer.Error!void {
        if (self != .external) try w.print(" {s}", .{@tagName(self)});
    }

    fn formatOptional(data: ?Linkage, w: *Writer) Writer.Error!void {
        if (data) |linkage| try w.print(" {s}", .{@tagName(linkage)});
    }
    pub fn fmtOptional(self: ?Linkage) std.fmt.Alt(?Linkage, formatOptional) {
        return .{ .data = self };
    }
};

pub const Preemption = enum {
    dso_preemptable,
    dso_local,
    implicit_dso_local,

    pub fn format(self: Preemption, w: *Writer) Writer.Error!void {
        if (self == .dso_local) try w.print(" {s}", .{@tagName(self)});
    }
};

pub const Visibility = enum(u2) {
    default = 0,
    hidden = 1,
    protected = 2,

    pub fn fromSymbolVisibility(sv: std.builtin.SymbolVisibility) Visibility {
        return switch (sv) {
            .default => .default,
            .hidden => .hidden,
            .protected => .protected,
        };
    }

    pub fn format(self: Visibility, writer: *Writer) Writer.Error!void {
        if (self != .default) try writer.print(" {s}", .{@tagName(self)});
    }
};

pub const DllStorageClass = enum(u2) {
    default = 0,
    dllimport = 1,
    dllexport = 2,

    pub fn format(self: DllStorageClass, w: *Writer) Writer.Error!void {
        if (self != .default) try w.print(" {s}", .{@tagName(self)});
    }
};

pub const ThreadLocal = enum(u3) {
    default = 0,
    generaldynamic = 1,
    localdynamic = 2,
    initialexec = 3,
    localexec = 4,

    pub fn format(tl: ThreadLocal, w: *Writer) Writer.Error!void {
        return Prefixed.format(.{ .thread_local = tl, .prefix = "" }, w);
    }

    pub const Prefixed = struct {
        thread_local: ThreadLocal,
        prefix: []const u8,

        pub fn format(p: Prefixed, w: *Writer) Writer.Error!void {
            switch (p.thread_local) {
                .default => return,
                .generaldynamic => {
                    var vecs: [2][]const u8 = .{ p.prefix, "thread_local" };
                    return w.writeVecAll(&vecs);
                },
                else => {
                    var vecs: [4][]const u8 = .{ p.prefix, "thread_local(", @tagName(p.thread_local), ")" };
                    return w.writeVecAll(&vecs);
                },
            }
        }
    };

    pub fn fmt(tl: ThreadLocal, prefix: []const u8) Prefixed {
        return .{ .thread_local = tl, .prefix = prefix };
    }
};

pub const Mutability = enum { global, constant };

pub const UnnamedAddr = enum(u2) {
    default = 0,
    unnamed_addr = 1,
    local_unnamed_addr = 2,

    pub fn format(self: UnnamedAddr, w: *Writer) Writer.Error!void {
        if (self != .default) try w.print(" {s}", .{@tagName(self)});
    }
};

pub const AddrSpace = enum(u24) {
    default,
    _,

    // See llvm/lib/Target/X86/X86.h
    pub const x86 = struct {
        pub const gs: AddrSpace = @enumFromInt(256);
        pub const fs: AddrSpace = @enumFromInt(257);
        pub const ss: AddrSpace = @enumFromInt(258);

        pub const ptr32_sptr: AddrSpace = @enumFromInt(270);
        pub const ptr32_uptr: AddrSpace = @enumFromInt(271);
        pub const ptr64: AddrSpace = @enumFromInt(272);
    };
    pub const x86_64 = x86;

    // See llvm/lib/Target/AVR/AVR.h
    pub const avr = struct {
        pub const data: AddrSpace = @enumFromInt(0);
        pub const program: AddrSpace = @enumFromInt(1);
        pub const program1: AddrSpace = @enumFromInt(2);
        pub const program2: AddrSpace = @enumFromInt(3);
        pub const program3: AddrSpace = @enumFromInt(4);
        pub const program4: AddrSpace = @enumFromInt(5);
        pub const program5: AddrSpace = @enumFromInt(6);
    };

    // See llvm/lib/Target/NVPTX/NVPTX.h
    pub const nvptx = struct {
        pub const generic: AddrSpace = @enumFromInt(0);
        pub const global: AddrSpace = @enumFromInt(1);
        pub const constant: AddrSpace = @enumFromInt(2);
        pub const shared: AddrSpace = @enumFromInt(3);
        pub const param: AddrSpace = @enumFromInt(4);
        pub const local: AddrSpace = @enumFromInt(5);
    };

    // See llvm/lib/Target/AMDGPU/AMDGPU.h
    pub const amdgpu = struct {
        pub const flat: AddrSpace = @enumFromInt(0);
        pub const global: AddrSpace = @enumFromInt(1);
        pub const region: AddrSpace = @enumFromInt(2);
        pub const local: AddrSpace = @enumFromInt(3);
        pub const constant: AddrSpace = @enumFromInt(4);
        pub const private: AddrSpace = @enumFromInt(5);
        pub const constant_32bit: AddrSpace = @enumFromInt(6);
        pub const buffer_fat_pointer: AddrSpace = @enumFromInt(7);
        pub const buffer_resource: AddrSpace = @enumFromInt(8);
        pub const buffer_strided_pointer: AddrSpace = @enumFromInt(9);
        pub const param_d: AddrSpace = @enumFromInt(6);
        pub const param_i: AddrSpace = @enumFromInt(7);
        pub const constant_buffer_0: AddrSpace = @enumFromInt(8);
        pub const constant_buffer_1: AddrSpace = @enumFromInt(9);
        pub const constant_buffer_2: AddrSpace = @enumFromInt(10);
        pub const constant_buffer_3: AddrSpace = @enumFromInt(11);
        pub const constant_buffer_4: AddrSpace = @enumFromInt(12);
        pub const constant_buffer_5: AddrSpace = @enumFromInt(13);
        pub const constant_buffer_6: AddrSpace = @enumFromInt(14);
        pub const constant_buffer_7: AddrSpace = @enumFromInt(15);
        pub const constant_buffer_8: AddrSpace = @enumFromInt(16);
        pub const constant_buffer_9: AddrSpace = @enumFromInt(17);
        pub const constant_buffer_10: AddrSpace = @enumFromInt(18);
        pub const constant_buffer_11: AddrSpace = @enumFromInt(19);
        pub const constant_buffer_12: AddrSpace = @enumFromInt(20);
        pub const constant_buffer_13: AddrSpace = @enumFromInt(21);
        pub const constant_buffer_14: AddrSpace = @enumFromInt(22);
        pub const constant_buffer_15: AddrSpace = @enumFromInt(23);
        pub const streamout_register: AddrSpace = @enumFromInt(128);
    };

    pub const spirv = struct {
        pub const function: AddrSpace = @enumFromInt(0);
        pub const cross_workgroup: AddrSpace = @enumFromInt(1);
        pub const uniform_constant: AddrSpace = @enumFromInt(2);
        pub const workgroup: AddrSpace = @enumFromInt(3);
        pub const generic: AddrSpace = @enumFromInt(4);
        pub const device_only_intel: AddrSpace = @enumFromInt(5);
        pub const host_only_intel: AddrSpace = @enumFromInt(6);
        pub const input: AddrSpace = @enumFromInt(7);
    };

    // See llvm/include/llvm/CodeGen/WasmAddressSpaces.h
    pub const wasm = struct {
        pub const default: AddrSpace = @enumFromInt(0);
        pub const variable: AddrSpace = @enumFromInt(1);
        pub const externref: AddrSpace = @enumFromInt(10);
        pub const funcref: AddrSpace = @enumFromInt(20);
    };

    pub fn format(addr_space: AddrSpace, w: *Writer) Writer.Error!void {
        return Prefixed.format(.{ .addr_space = addr_space, .prefix = "" }, w);
    }

    pub const Prefixed = struct {
        addr_space: AddrSpace,
        prefix: []const u8,

        pub fn format(p: Prefixed, w: *Writer) Writer.Error!void {
            switch (p.addr_space) {
                .default => return,
                else => return w.print("{s}addrspace({d})", .{ p.prefix, p.addr_space }),
            }
        }
    };

    pub fn fmt(addr_space: AddrSpace, prefix: []const u8) Prefixed {
        return .{ .addr_space = addr_space, .prefix = prefix };
    }
};

pub const ExternallyInitialized = enum {
    default,
    externally_initialized,

    pub fn format(self: ExternallyInitialized, w: *Writer) Writer.Error!void {
        if (self != .default) try w.print(" {s}", .{@tagName(self)});
    }
};

pub const Alignment = enum(u6) {
    default = maxInt(u6),
    _,

    pub const Lazy = enum(u32) {
        /// Values which fit in a `u6` are already-resolved `Alignment` values. Other values are
        /// indices into `Builder.alignment_forward_references`, offset by `maxInt(u6)`.
        _,

        pub fn wrap(a: Alignment) Lazy {
            return @enumFromInt(@intFromEnum(a));
        }
        pub fn resolve(l: Lazy, b: *const Builder) Alignment {
            return switch (@intFromEnum(l)) {
                0...maxInt(u6) => |raw| @enumFromInt(raw),
                else => |offset_index| b.alignment_forward_references.items[offset_index - maxInt(u6)],
            };
        }

        fn fromFwdRefIndex(index: usize) Lazy {
            return @enumFromInt(index + maxInt(u6));
        }
        fn toFwdRefIndex(l: Lazy) usize {
            return @intFromEnum(l) - maxInt(u6);
        }
    };

    pub fn fromByteUnits(bytes: u64) Alignment {
        if (bytes == 0) return .default;
        assert(std.math.isPowerOfTwo(bytes));
        assert(bytes <= 1 << 32);
        return @enumFromInt(@ctz(bytes));
    }

    pub fn toByteUnits(self: Alignment) ?u64 {
        return switch (self) {
            .default => null,
            else => @as(u64, 1) << @intFromEnum(self),
        };
    }

    pub fn toLlvm(self: Alignment) u6 {
        return switch (self) {
            .default => 0,
            else => @intFromEnum(self) + 1,
        };
    }

    pub const Prefixed = struct {
        alignment: Alignment,
        prefix: []const u8,

        pub fn format(p: Prefixed, w: *Writer) Writer.Error!void {
            const byte_units = p.alignment.toByteUnits() orelse return;
            return w.print("{s}align {d}", .{ p.prefix, byte_units });
        }
    };

    pub fn fmt(alignment: Alignment, prefix: []const u8) Prefixed {
        return .{ .alignment = alignment, .prefix = prefix };
    }
};

pub const CallConv = enum(u10) {
    ccc,

    fastcc = 8,
    coldcc,
    ghccc,

    webkit_jscc = 12,
    anyregcc,
    preserve_mostcc,
    preserve_allcc,
    swiftcc,
    cxx_fast_tlscc,
    tailcc,
    cfguard_checkcc,
    swifttailcc,

    x86_stdcallcc = 64,
    x86_fastcallcc,
    arm_apcscc,
    arm_aapcscc,
    arm_aapcs_vfpcc,
    msp430_intrcc,
    x86_thiscallcc,
    ptx_kernel,
    ptx_device,

    spir_func = 75,
    spir_kernel,
    intel_ocl_bicc,
    x86_64_sysvcc,
    win64cc,
    x86_vectorcallcc,
    hhvmcc,
    hhvm_ccc,
    x86_intrcc,
    avr_intrcc,
    avr_signalcc,
    avr_builtincc,

    amdgpu_vs = 87,
    amdgpu_gs,
    amdgpu_ps,
    amdgpu_cs,
    amdgpu_kernel,
    x86_regcallcc,
    amdgpu_hs,
    msp430_builtincc,

    amdgpu_ls = 95,
    amdgpu_es,
    aarch64_vector_pcs,
    aarch64_sve_vector_pcs,

    amdgpu_gfx = 100,

    m68k_intrcc,

    aarch64_sme_preservemost_from_x0 = 102,
    aarch64_sme_preservemost_from_x2,

    m68k_rtdcc = 106,

    riscv_vectorcallcc = 110,

    _,

    pub const default = CallConv.ccc;

    pub fn format(self: CallConv, w: *Writer) Writer.Error!void {
        switch (self) {
            default => {},
            .fastcc,
            .coldcc,
            .ghccc,
            .webkit_jscc,
            .anyregcc,
            .preserve_mostcc,
            .preserve_allcc,
            .swiftcc,
            .cxx_fast_tlscc,
            .tailcc,
            .cfguard_checkcc,
            .swifttailcc,
            .x86_stdcallcc,
            .x86_fastcallcc,
            .arm_apcscc,
            .arm_aapcscc,
            .arm_aapcs_vfpcc,
            .msp430_intrcc,
            .x86_thiscallcc,
            .ptx_kernel,
            .ptx_device,
            .spir_func,
            .spir_kernel,
            .intel_ocl_bicc,
            .x86_64_sysvcc,
            .win64cc,
            .x86_vectorcallcc,
            .hhvmcc,
            .hhvm_ccc,
            .x86_intrcc,
            .avr_intrcc,
            .avr_signalcc,
            .avr_builtincc,
            .amdgpu_vs,
            .amdgpu_gs,
            .amdgpu_ps,
            .amdgpu_cs,
            .amdgpu_kernel,
            .x86_regcallcc,
            .amdgpu_hs,
            .msp430_builtincc,
            .amdgpu_ls,
            .amdgpu_es,
            .aarch64_vector_pcs,
            .aarch64_sve_vector_pcs,
            .amdgpu_gfx,
            .m68k_intrcc,
            .aarch64_sme_preservemost_from_x0,
            .aarch64_sme_preservemost_from_x2,
            .m68k_rtdcc,
            .riscv_vectorcallcc,
            => try w.print(" {s}", .{@tagName(self)}),
            _ => try w.print(" cc{d}", .{@intFromEnum(self)}),
        }
    }
};

pub const StrtabString = enum(u32) {
    none = maxInt(u31),
    empty,
    _,

    pub fn isAnon(self: StrtabString) bool {
        assert(self != .none);
        return self.toIndex() == null;
    }

    pub fn slice(self: StrtabString, builder: *const Builder) ?[]const u8 {
        const index = self.toIndex() orelse return null;
        const start = builder.strtab_string_indices.items[index];
        const end = builder.strtab_string_indices.items[index + 1];
        return builder.strtab_string_bytes.items[start..end];
    }

    const FormatData = struct {
        string: StrtabString,
        builder: *const Builder,
        quote_behavior: ?QuoteBehavior,
    };
    fn format(data: FormatData, w: *Writer) Writer.Error!void {
        assert(data.string != .none);
        const string_slice = data.string.slice(data.builder) orelse
            return w.print("{d}", .{@intFromEnum(data.string)});
        const quote_behavior = data.quote_behavior orelse return w.writeAll(string_slice);
        return printEscapedString(string_slice, quote_behavior, w);
    }
    pub fn fmt(
        self: StrtabString,
        builder: *const Builder,
        quote_behavior: ?QuoteBehavior,
    ) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{
            .string = self,
            .builder = builder,
            .quote_behavior = quote_behavior,
        } };
    }

    fn fromIndex(index: ?usize) StrtabString {
        return @enumFromInt(@as(u32, @intCast((index orelse return .none) +
            @intFromEnum(StrtabString.empty))));
    }

    fn toIndex(self: StrtabString) ?usize {
        return std.math.sub(u32, @intFromEnum(self), @intFromEnum(StrtabString.empty)) catch null;
    }

    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: Adapter, key: []const u8) u32 {
            return @truncate(std.hash.Wyhash.hash(0, key));
        }
        pub fn eql(ctx: Adapter, lhs_key: []const u8, _: void, rhs_index: usize) bool {
            return std.mem.eql(u8, lhs_key, StrtabString.fromIndex(rhs_index).slice(ctx.builder).?);
        }
    };
};

pub fn strtabString(self: *Builder, bytes: []const u8) Allocator.Error!StrtabString {
    try self.strtab_string_bytes.ensureUnusedCapacity(self.gpa, bytes.len);
    try self.strtab_string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.strtab_string_map.ensureUnusedCapacity(self.gpa, 1);

    const gop = self.strtab_string_map.getOrPutAssumeCapacityAdapted(bytes, StrtabString.Adapter{ .builder = self });
    if (!gop.found_existing) {
        self.strtab_string_bytes.appendSliceAssumeCapacity(bytes);
        self.strtab_string_indices.appendAssumeCapacity(@intCast(self.strtab_string_bytes.items.len));
    }
    return StrtabString.fromIndex(gop.index);
}

pub fn strtabStringIfExists(self: *const Builder, bytes: []const u8) ?StrtabString {
    return StrtabString.fromIndex(
        self.strtab_string_map.getIndexAdapted(bytes, StrtabString.Adapter{ .builder = self }) orelse return null,
    );
}

pub fn strtabStringFmt(self: *Builder, comptime fmt_str: []const u8, fmt_args: anytype) Allocator.Error!StrtabString {
    try self.strtab_string_map.ensureUnusedCapacity(self.gpa, 1);
    try self.strtab_string_bytes.ensureUnusedCapacity(self.gpa, @intCast(std.fmt.count(fmt_str, fmt_args)));
    try self.strtab_string_indices.ensureUnusedCapacity(self.gpa, 1);
    return self.strtabStringFmtAssumeCapacity(fmt_str, fmt_args);
}

pub fn strtabStringFmtAssumeCapacity(self: *Builder, comptime fmt_str: []const u8, fmt_args: anytype) StrtabString {
    self.strtab_string_bytes.printAssumeCapacity(fmt_str, fmt_args);
    return self.trailingStrtabStringAssumeCapacity();
}

pub fn trailingStrtabString(self: *Builder) Allocator.Error!StrtabString {
    try self.strtab_string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.strtab_string_map.ensureUnusedCapacity(self.gpa, 1);
    return self.trailingStrtabStringAssumeCapacity();
}

pub fn trailingStrtabStringAssumeCapacity(self: *Builder) StrtabString {
    const start = self.strtab_string_indices.getLast();
    const bytes: []const u8 = self.strtab_string_bytes.items[start..];
    const gop = self.strtab_string_map.getOrPutAssumeCapacityAdapted(bytes, StrtabString.Adapter{ .builder = self });
    if (gop.found_existing) {
        self.strtab_string_bytes.shrinkRetainingCapacity(start);
    } else {
        self.strtab_string_indices.appendAssumeCapacity(@intCast(self.strtab_string_bytes.items.len));
    }
    return StrtabString.fromIndex(gop.index);
}

pub const Global = struct {
    linkage: Linkage = .external,
    preemption: Preemption = .dso_preemptable,
    visibility: Visibility = .default,
    dll_storage_class: DllStorageClass = .default,
    unnamed_addr: UnnamedAddr = .default,
    addr_space: AddrSpace = .default,
    externally_initialized: ExternallyInitialized = .default,
    type: Type,
    partition: String = .none,
    dbg: Metadata.Optional = .none,
    kind: union(enum) {
        alias: Alias.Index,
        variable: Variable.Index,
        function: Function.Index,
        replaced: Global.Index,
    },

    pub const Index = enum(u32) {
        none = maxInt(u32),
        _,

        pub fn unwrap(orig_index: Index, builder: *const Builder) Index {
            var cur = orig_index;
            while (true) {
                switch (builder.globals.values()[@intFromEnum(cur)].kind) {
                    .replaced => |replacement| cur = replacement,
                    else => return cur,
                }
            }
        }

        pub fn eql(self: Index, other: Index, builder: *const Builder) bool {
            return self.unwrap(builder) == other.unwrap(builder);
        }

        pub fn ptr(self: Index, builder: *Builder) *Global {
            return &builder.globals.values()[@intFromEnum(self.unwrap(builder))];
        }

        pub fn ptrConst(self: Index, builder: *const Builder) *const Global {
            return &builder.globals.values()[@intFromEnum(self.unwrap(builder))];
        }

        pub fn name(self: Index, builder: *const Builder) StrtabString {
            return builder.globals.keys()[@intFromEnum(self.unwrap(builder))];
        }

        pub fn strtab(self: Index, builder: *const Builder) struct {
            offset: u32,
            size: u32,
        } {
            const name_index = self.name(builder).toIndex() orelse return .{
                .offset = 0,
                .size = 0,
            };

            return .{
                .offset = builder.strtab_string_indices.items[name_index],
                .size = builder.strtab_string_indices.items[name_index + 1] -
                    builder.strtab_string_indices.items[name_index],
            };
        }

        pub fn typeOf(self: Index, builder: *const Builder) Type {
            return self.ptrConst(builder).type;
        }

        pub fn toConst(global: Index) Constant {
            return @enumFromInt(@intFromEnum(Constant.first_global) + @intFromEnum(global));
        }

        pub fn toValue(global: Index) Value {
            return global.toConst().toValue();
        }

        pub fn setLinkage(self: Index, linkage: Linkage, builder: *Builder) void {
            self.ptr(builder).linkage = linkage;
            self.updateDsoLocal(builder);
        }

        pub fn setVisibility(self: Index, visibility: Visibility, builder: *Builder) void {
            self.ptr(builder).visibility = visibility;
            self.updateDsoLocal(builder);
        }

        pub fn setDllStorageClass(self: Index, class: DllStorageClass, builder: *Builder) void {
            self.ptr(builder).dll_storage_class = class;
        }

        pub fn setUnnamedAddr(self: Index, unnamed_addr: UnnamedAddr, builder: *Builder) void {
            self.ptr(builder).unnamed_addr = unnamed_addr;
        }

        pub fn setDebugMetadata(self: Index, dbg: Metadata, builder: *Builder) void {
            self.ptr(builder).dbg = dbg.toOptional();
        }

        pub fn getDebugMetadata(self: Index, builder: *const Builder) Metadata.Optional {
            return self.ptrConst(builder).dbg;
        }

        const FormatData = struct {
            global: Index,
            builder: *const Builder,
        };
        fn format(data: FormatData, w: *Writer) Writer.Error!void {
            try w.print("@{f}", .{
                data.global.unwrap(data.builder).name(data.builder).fmt(data.builder, .quote_unless_valid_identifier),
            });
        }
        pub fn fmt(self: Index, builder: *const Builder) std.fmt.Alt(FormatData, format) {
            return .{ .data = .{ .global = self, .builder = builder } };
        }

        pub fn rename(self: Index, new_name: StrtabString, builder: *Builder) Allocator.Error!void {
            try builder.ensureUnusedGlobalCapacity(new_name);
            self.renameAssumeCapacity(new_name, builder);
        }

        pub fn takeName(self: Index, other: Index, builder: *Builder) Allocator.Error!void {
            try builder.ensureUnusedGlobalCapacity(.empty);
            self.takeNameAssumeCapacity(other, builder);
        }

        pub fn replace(self: Index, other: Index, builder: *Builder) Allocator.Error!void {
            try builder.ensureUnusedGlobalCapacity(.empty);
            self.replaceAssumeCapacity(other, builder);
        }

        pub fn delete(self: Index, builder: *Builder) void {
            self.ptr(builder).kind = .{ .replaced = .none };
        }

        /// Replaces whatever this `Global` currently contains with a new `Function`. Similar to
        /// `Builder.addFunction`, but the same `Global` is reused.
        pub fn toNewFunction(global: Index, builder: *Builder) Allocator.Error!Function.Index {
            try builder.functions.ensureUnusedCapacity(builder.gpa, 1);
            errdefer comptime unreachable;
            const function: Function.Index = @enumFromInt(builder.functions.items.len);
            builder.functions.appendAssumeCapacity(.{
                .global = global,
                .strip = undefined,
            });
            global.ptr(builder).kind = .{ .function = function };
            return function;
        }

        /// Replaces whatever this `Global` currently contains with a new `Variable`. Similar to
        /// `Builder.addVariable`, but the same `Global` is reused.
        pub fn toNewVariable(global: Index, builder: *Builder) Allocator.Error!Variable.Index {
            try builder.variables.ensureUnusedCapacity(builder.gpa, 1);
            errdefer comptime unreachable;
            const variable: Variable.Index = @enumFromInt(builder.variables.items.len);
            builder.variables.appendAssumeCapacity(.{ .global = global });
            global.ptr(builder).kind = .{ .variable = variable };
            return variable;
        }

        /// Replaces whatever this `Global` currently contains with a new `Alias`. Similar to
        /// `Builder.addAlias`, but the same `Global` is reused.
        pub fn toNewAlias(global: Index, builder: *Builder) Allocator.Error!Alias.Index {
            try builder.aliases.ensureUnusedCapacity(builder.gpa, 1);
            errdefer comptime unreachable;
            const alias: Alias.Index = @enumFromInt(builder.aliases.items.len);
            builder.aliass.appendAssumeCapacity(.{ .global = global, .aliasee = .none });
            global.ptr(builder).kind = .{ .alias = alias };
            return alias;
        }

        fn updateDsoLocal(self: Index, builder: *Builder) void {
            const self_ptr = self.ptr(builder);
            switch (self_ptr.linkage) {
                .private, .internal => {
                    self_ptr.visibility = .default;
                    self_ptr.dll_storage_class = .default;
                    self_ptr.preemption = .implicit_dso_local;
                },
                .extern_weak => if (self_ptr.preemption == .implicit_dso_local) {
                    self_ptr.preemption = .dso_local;
                },
                else => switch (self_ptr.visibility) {
                    .default => if (self_ptr.preemption == .implicit_dso_local) {
                        self_ptr.preemption = .dso_local;
                    },
                    else => self_ptr.preemption = .implicit_dso_local,
                },
            }
        }

        fn renameAssumeCapacity(self: Index, new_name: StrtabString, builder: *Builder) void {
            const old_name = self.name(builder);
            if (new_name == old_name) return;
            const index = @intFromEnum(self.unwrap(builder));
            _ = builder.addGlobalAssumeCapacity(new_name, builder.globals.values()[index]);
            builder.globals.swapRemoveAt(index);
            if (!old_name.isAnon()) return;
            builder.next_unnamed_global = @enumFromInt(@intFromEnum(builder.next_unnamed_global) - 1);
            if (builder.next_unnamed_global == old_name) return;
            builder.getGlobal(builder.next_unnamed_global).?.renameAssumeCapacity(old_name, builder);
        }

        fn takeNameAssumeCapacity(self: Index, other: Index, builder: *Builder) void {
            const other_name = other.name(builder);
            other.renameAssumeCapacity(.empty, builder);
            self.renameAssumeCapacity(other_name, builder);
        }

        fn replaceAssumeCapacity(self: Index, other: Index, builder: *Builder) void {
            if (self.eql(other, builder)) return;
            builder.next_replaced_global = @enumFromInt(@intFromEnum(builder.next_replaced_global) - 1);
            self.renameAssumeCapacity(builder.next_replaced_global, builder);
            self.ptr(builder).kind = .{ .replaced = other.unwrap(builder) };
        }
    };
};

pub const Alias = struct {
    global: Global.Index,
    thread_local: ThreadLocal = .default,
    aliasee: Constant = .no_init,

    pub const Index = enum(u32) {
        none = maxInt(u32),
        _,

        pub fn ptr(self: Index, builder: *Builder) *Alias {
            return &builder.aliases.items[@intFromEnum(self)];
        }

        pub fn ptrConst(self: Index, builder: *const Builder) *const Alias {
            return &builder.aliases.items[@intFromEnum(self)];
        }

        pub fn name(self: Index, builder: *const Builder) StrtabString {
            return self.ptrConst(builder).global.name(builder);
        }

        pub fn rename(self: Index, new_name: StrtabString, builder: *Builder) Allocator.Error!void {
            return self.ptrConst(builder).global.rename(new_name, builder);
        }

        pub fn typeOf(self: Index, builder: *const Builder) Type {
            return self.ptrConst(builder).global.typeOf(builder);
        }

       
```
