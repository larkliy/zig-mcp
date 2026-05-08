```
Iterator.next()
///        prong_body: {
///            body_inst: Inst.Index, // for every case.prong_info.body_len,
///        }
///        item_body: { // for each body_len in case.item_infos
///            body_inst: Inst.Index, // for every body_len
///        }
///        range_bodies: { // for each .{first_info, last_info} in case.range_infos
///            first_body_inst: Inst.Index, // for every first_info.body_len
///            last_body_inst: Inst.Index, // for every last_info.body_len
///        }
///    }
pub const UnwrappedSwitchBlock = struct {
    /// Either `catch`/`if` or `switch` operand.
    main_operand: Inst.Ref,
    switch_src_node_offset: Ast.Node.Offset,
    catch_or_if_src_node_offset: Ast.Node.OptionalOffset,
    payload_capture_placeholder: Inst.OptionalIndex,
    tag_capture_placeholder: Inst.OptionalIndex,
    has_continue: bool,
    any_maybe_runtime_capture: bool,
    non_err_case: ?Case.NonErr,
    else_case: ?Case.Else,
    has_under: bool,
    // Refer to doc comment and `iterateCases` to access everything below correctly.
    prong_infos: []const Inst.SwitchBlock.ProngInfo,
    multi_case_items_lens: []const u32,
    multi_case_ranges_lens: ?[]const u32,
    item_infos: []const Inst.SwitchBlock.ItemInfo,
    end: usize,

    pub fn anyRanges(unwrapped: *const UnwrappedSwitchBlock) bool {
        return unwrapped.multi_case_ranges_lens != null;
    }

    pub fn scalarCasesLen(unwrapped: *const UnwrappedSwitchBlock) u32 {
        return @intCast(unwrapped.prong_infos.len - unwrapped.multi_case_items_lens.len);
    }

    pub fn multiCasesLen(unwrapped: *const UnwrappedSwitchBlock) u32 {
        return @intCast(unwrapped.multi_case_items_lens.len);
    }

    pub fn totalItemsLen(unwrapped: *const UnwrappedSwitchBlock) u32 {
        var total_items_len: u32 = @intCast(unwrapped.item_infos.len);
        if (unwrapped.multi_case_ranges_lens) |ranges_lens| {
            for (ranges_lens) |len| total_items_len -= len;
        }
        return total_items_len;
    }

    pub const Case = struct {
        index: Case.Index,
        prong_info: Inst.SwitchBlock.ProngInfo,
        item_infos: []const Inst.SwitchBlock.ItemInfo,
        range_infos: []const [2]Inst.SwitchBlock.ItemInfo,

        pub const Index = packed struct(u32) {
            kind: enum(u1) { scalar, multi },
            value: u31,

            pub const @"else": Case.Index = .{
                .kind = .scalar,
                .value = std.math.maxInt(u31),
            };
        };

        pub const NonErr = struct {
            body: []const Inst.Index,
            capture: Inst.SwitchBlock.ProngInfo.Capture,
            operand_is_ref: bool,
        };

        pub const Else = struct {
            index: Case.Index,
            body: []const Inst.Index,
            capture: Inst.SwitchBlock.ProngInfo.Capture,
            is_inline: bool,
            has_tag_capture: bool,
            is_simple_noreturn: bool,
        };

        pub const Iterator = struct {
            next_idx: u32,
            prong_infos: []const Inst.SwitchBlock.ProngInfo,
            multi_case_items_lens: []const u32,
            multi_case_ranges_lens: ?[]const u32,
            item_infos: []const Inst.SwitchBlock.ItemInfo,

            pub fn next(it: *Iterator) ?Case {
                const idx = it.next_idx;
                if (idx == it.prong_infos.len) return null;
                it.next_idx += 1;
                const scalar_cases_len = it.prong_infos.len - it.multi_case_items_lens.len;
                return if (idx < scalar_cases_len) .{
                    .index = .{
                        .kind = .scalar,
                        .value = @intCast(idx),
                    },
                    .prong_info = it.prong_infos[idx],
                    .item_infos = it.itemInfos(1),
                    .range_infos = &.{},
                } else .{
                    .index = .{
                        .kind = .multi,
                        .value = @intCast(idx - scalar_cases_len),
                    },
                    .prong_info = it.prong_infos[idx],
                    .item_infos = it.itemInfos(it.multi_case_items_lens[idx - scalar_cases_len]),
                    .range_infos = if (it.multi_case_ranges_lens) |ranges_lens| b: {
                        break :b @ptrCast(it.itemInfos(2 * ranges_lens[idx - scalar_cases_len]));
                    } else &.{},
                };
            }
            fn itemInfos(it: *Iterator, count: u32) []const Inst.SwitchBlock.ItemInfo {
                const lens = it.item_infos[0..count];
                it.item_infos = it.item_infos[count..];
                return lens;
            }
        };
    };

    pub fn iterateCases(unwrapped: UnwrappedSwitchBlock) Case.Iterator {
        return .{
            .next_idx = 0,
            .prong_infos = unwrapped.prong_infos,
            .multi_case_items_lens = unwrapped.multi_case_items_lens,
            .multi_case_ranges_lens = unwrapped.multi_case_ranges_lens,
            .item_infos = unwrapped.item_infos,
        };
    }
};

/// When the ZIR update tracking logic must be modified to consider new instructions,
/// change this constant to trigger compile errors at all relevant locations.
pub const inst_tracking_version = 0;

/// Asserts that a ZIR instruction is tracked across incremental updates, and
/// thus may be given an `InternPool.TrackedInst`.
pub fn assertTrackable(zir: Zir, inst_idx: Zir.Inst.Index) void {
    comptime assert(Zir.inst_tracking_version == 0);
    const inst = zir.instructions.get(@intFromEnum(inst_idx));
    switch (inst.tag) {
        .struct_init,
        .struct_init_ref,
        .struct_init_anon,
        => {}, // tracked in order, as the owner instructions of anonymous struct types
        .func, .func_inferred => {
            // These are tracked provided they are actual function declarations, not just bodies.
            const extra = zir.extraData(Inst.Func, inst.data.pl_node.payload_index);
            assert(extra.data.body_len != 0);
        },
        .func_fancy => {
            // These are tracked provided they are actual function declarations, not just bodies.
            const extra = zir.extraData(Inst.FuncFancy, inst.data.pl_node.payload_index);
            assert(extra.data.body_len != 0);
        },
        .declaration => {}, // tracked by correlating names in the namespace of the parent container
        .extended => switch (inst.data.extended.opcode) {
            .struct_decl,
            .union_decl,
            .enum_decl,
            .opaque_decl,
            .reify_enum,
            .reify_struct,
            .reify_union,
            => {}, // tracked in order, as the owner instructions of explicit container types
            else => unreachable, // assertion failure; not trackable
        },
        else => unreachable, // assertion failure; not trackable
    }
}

pub fn typeDecls(zir: Zir, type_decl: Inst.Index) []const Zir.Inst.Index {
    const inst = zir.instructions.get(@intFromEnum(type_decl));
    assert(inst.tag == .extended);
    return switch (inst.data.extended.opcode) {
        .struct_decl => zir.getStructDecl(type_decl).decls,
        .union_decl => zir.getUnionDecl(type_decl).decls,
        .enum_decl => zir.getEnumDecl(type_decl).decls,
        .opaque_decl => zir.getOpaqueDecl(type_decl).decls,
        else => unreachable,
    };
}

pub fn getStructDecl(zir: *const Zir, struct_decl: Inst.Index) UnwrappedStructDecl {
    const inst_data = zir.instructions.get(@intFromEnum(struct_decl));
    assert(inst_data.tag == .extended);
    assert(inst_data.data.extended.opcode == .struct_decl);
    const small: Inst.StructDecl.Small = @bitCast(inst_data.data.extended.small);
    const extra = zir.extraData(Inst.StructDecl, inst_data.data.extended.operand);
    var extra_index = extra.end;
    const captures_len: u32 = if (small.has_captures_len) blk: {
        const captures_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk captures_len;
    } else 0;
    const decls_len: u32 = if (small.has_decls_len) blk: {
        const decls_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk decls_len;
    } else 0;
    const fields_len: u32 = if (small.has_fields_len) blk: {
        const fields_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk fields_len;
    } else 0;
    const backing_int_type_body_len: u32 = if (small.has_backing_int_type) len: {
        const body_len = zir.extra[extra_index];
        extra_index += 1;
        break :len body_len;
    } else 0;
    const captures: []const Inst.Capture = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const capture_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const decls: []const Inst.Index = @ptrCast(zir.extra[extra_index..][0..decls_len]);
    extra_index += decls_len;
    const field_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..fields_len]);
    extra_index += fields_len;
    const field_type_body_lens: []const u32 = @ptrCast(zir.extra[extra_index..][0..fields_len]);
    extra_index += fields_len;
    const field_align_body_lens: ?[]const u32 = if (small.any_field_aligns) lens: {
        const lens = zir.extra[extra_index..][0..fields_len];
        extra_index += fields_len;
        break :lens @ptrCast(lens);
    } else null;
    const field_default_body_lens: ?[]const u32 = if (small.any_field_defaults) lens: {
        const lens = zir.extra[extra_index..][0..fields_len];
        extra_index += fields_len;
        break :lens @ptrCast(lens);
    } else null;
    const field_comptime_bits: ?[]const u32 = if (small.any_comptime_fields) bits: {
        const bits_len = std.math.divCeil(u32, fields_len, 32) catch unreachable;
        const bits = zir.extra[extra_index..][0..bits_len];
        extra_index += bits_len;
        break :bits bits;
    } else null;
    const backing_int_type_body: ?[]const Zir.Inst.Index = switch (backing_int_type_body_len) {
        0 => null,
        else => |n| zir.bodySlice(extra_index, n),
    };
    extra_index += backing_int_type_body_len;
    const field_bodies_overlong: []const Inst.Index = @ptrCast(zir.extra[extra_index..]);
    return .{
        .src_line = extra.data.src_line,
        .src_node = extra.data.src_node,
        .name_strategy = small.name_strategy,
        .captures = captures,
        .capture_names = capture_names,
        .decls = decls,
        .layout = small.layout,
        .backing_int_type_body = backing_int_type_body,
        .field_names = field_names,
        .field_type_body_lens = field_type_body_lens,
        .field_align_body_lens = field_align_body_lens,
        .field_default_body_lens = field_default_body_lens,
        .field_comptime_bits = field_comptime_bits,
        .field_bodies_overlong = field_bodies_overlong,
    };
}
pub const UnwrappedStructDecl = struct {
    src_line: u32,
    src_node: Ast.Node.Index,
    name_strategy: Inst.NameStrategy,

    captures: []const Inst.Capture,
    capture_names: []const NullTerminatedString,

    decls: []const Inst.Index,

    layout: std.builtin.Type.ContainerLayout,
    backing_int_type_body: ?[]const Inst.Index,

    field_names: []const NullTerminatedString,
    field_type_body_lens: []const u32,
    field_align_body_lens: ?[]const u32,
    field_default_body_lens: ?[]const u32,
    field_comptime_bits: ?[]const u32,
    field_bodies_overlong: []const Inst.Index,

    pub fn iterateFields(struct_decl: UnwrappedStructDecl) FieldIterator {
        return .{
            .next_idx = 0,
            .names = struct_decl.field_names,
            .type_body_lens = struct_decl.field_type_body_lens,
            .align_body_lens = struct_decl.field_align_body_lens,
            .default_body_lens = struct_decl.field_default_body_lens,
            .comptime_bits = struct_decl.field_comptime_bits,
            .bodies_overlong = struct_decl.field_bodies_overlong,
        };
    }

    pub const FieldIterator = struct {
        next_idx: u32,
        names: []const NullTerminatedString,
        type_body_lens: []const u32,
        align_body_lens: ?[]const u32,
        default_body_lens: ?[]const u32,
        comptime_bits: ?[]const u32,
        bodies_overlong: []const Inst.Index,
        pub const Field = struct {
            idx: u32,
            name: NullTerminatedString,
            type_body: []const Inst.Index,
            align_body: ?[]const Inst.Index,
            default_body: ?[]const Inst.Index,
            is_comptime: bool,
        };
        pub fn next(it: *FieldIterator) ?Field {
            const idx = it.next_idx;
            if (idx == it.names.len) return null;
            it.next_idx += 1;
            return .{
                .idx = idx,
                .name = it.names[idx],
                .type_body = it.body(it.type_body_lens[idx]).?,
                .align_body = it.body(if (it.align_body_lens) |l| l[idx] else 0),
                .default_body = it.body(if (it.default_body_lens) |l| l[idx] else 0),
                .is_comptime = ct: {
                    const bits = it.comptime_bits orelse break :ct false;
                    const big = bits[idx / 32];
                    const shifted = big >> @intCast(idx % 32);
                    break :ct @as(u1, @truncate(shifted)) == 1;
                },
            };
        }
        fn body(it: *FieldIterator, len: u32) ?[]const Inst.Index {
            if (len == 0) return null;
            const b = it.bodies_overlong[0..len];
            it.bodies_overlong = it.bodies_overlong[len..];
            return b;
        }
    };
};

pub fn getUnionDecl(zir: *const Zir, union_decl: Inst.Index) UnwrappedUnionDecl {
    const inst_data = zir.instructions.get(@intFromEnum(union_decl));
    assert(inst_data.tag == .extended);
    assert(inst_data.data.extended.opcode == .union_decl);
    const small: Inst.UnionDecl.Small = @bitCast(inst_data.data.extended.small);
    const extra = zir.extraData(Inst.UnionDecl, inst_data.data.extended.operand);
    var extra_index = extra.end;
    const captures_len: u32 = if (small.has_captures_len) blk: {
        const captures_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk captures_len;
    } else 0;
    const decls_len: u32 = if (small.has_decls_len) blk: {
        const decls_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk decls_len;
    } else 0;
    const fields_len: u32 = if (small.has_fields_len) blk: {
        const fields_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk fields_len;
    } else 0;
    const arg_type_body_len: u32 = if (small.kind.hasArgType()) len: {
        const body_len = zir.extra[extra_index];
        extra_index += 1;
        break :len body_len;
    } else 0;
    const captures: []const Inst.Capture = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const capture_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const decls: []const Inst.Index = @ptrCast(zir.extra[extra_index..][0..decls_len]);
    extra_index += decls_len;
    const field_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..fields_len]);
    extra_index += fields_len;
    const field_type_body_lens: []const u32 = @ptrCast(zir.extra[extra_index..][0..fields_len]);
    extra_index += fields_len;
    const field_align_body_lens: ?[]const u32 = if (small.any_field_aligns) lens: {
        const lens = zir.extra[extra_index..][0..fields_len];
        extra_index += fields_len;
        break :lens @ptrCast(lens);
    } else null;
    const field_value_body_lens: ?[]const u32 = if (small.any_field_values) lens: {
        const lens = zir.extra[extra_index..][0..fields_len];
        extra_index += fields_len;
        break :lens @ptrCast(lens);
    } else null;
    const arg_type_body: ?[]const Zir.Inst.Index = switch (arg_type_body_len) {
        0 => null,
        else => |n| zir.bodySlice(extra_index, n),
    };
    extra_index += arg_type_body_len;
    const field_bodies_overlong: []const Inst.Index = @ptrCast(zir.extra[extra_index..]);
    return .{
        .src_line = extra.data.src_line,
        .src_node = extra.data.src_node,
        .name_strategy = small.name_strategy,
        .captures = captures,
        .capture_names = capture_names,
        .decls = decls,
        .kind = small.kind,
        .arg_type_body = arg_type_body,
        .field_names = field_names,
        .field_type_body_lens = field_type_body_lens,
        .field_align_body_lens = field_align_body_lens,
        .field_value_body_lens = field_value_body_lens,
        .field_bodies_overlong = field_bodies_overlong,
    };
}
pub const UnwrappedUnionDecl = struct {
    src_line: u32,
    src_node: Ast.Node.Index,
    name_strategy: Inst.NameStrategy,

    captures: []const Inst.Capture,
    capture_names: []const NullTerminatedString,

    decls: []const Inst.Index,

    kind: Inst.UnionDecl.Kind,
    arg_type_body: ?[]const Inst.Index,

    field_names: []const NullTerminatedString,
    field_type_body_lens: []const u32,
    field_align_body_lens: ?[]const u32,
    field_value_body_lens: ?[]const u32,
    field_bodies_overlong: []const Inst.Index,

    pub fn iterateFields(union_decl: UnwrappedUnionDecl) FieldIterator {
        return .{
            .next_idx = 0,
            .names = union_decl.field_names,
            .type_body_lens = union_decl.field_type_body_lens,
            .align_body_lens = union_decl.field_align_body_lens,
            .value_body_lens = union_decl.field_value_body_lens,
            .bodies_overlong = union_decl.field_bodies_overlong,
        };
    }

    pub const FieldIterator = struct {
        next_idx: u32,
        names: []const NullTerminatedString,
        type_body_lens: []const u32,
        align_body_lens: ?[]const u32,
        value_body_lens: ?[]const u32,
        bodies_overlong: []const Inst.Index,
        pub const Field = struct {
            idx: u32,
            name: NullTerminatedString,
            type_body: ?[]const Inst.Index,
            align_body: ?[]const Inst.Index,
            value_body: ?[]const Inst.Index,
        };
        pub fn next(it: *FieldIterator) ?Field {
            const idx = it.next_idx;
            if (idx == it.names.len) return null;
            it.next_idx += 1;
            return .{
                .idx = idx,
                .name = it.names[idx],
                .type_body = it.body(it.type_body_lens[idx]),
                .align_body = it.body(if (it.align_body_lens) |l| l[idx] else 0),
                .value_body = it.body(if (it.value_body_lens) |l| l[idx] else 0),
            };
        }
        fn body(it: *FieldIterator, len: u32) ?[]const Inst.Index {
            if (len == 0) return null;
            const b = it.bodies_overlong[0..len];
            it.bodies_overlong = it.bodies_overlong[len..];
            return b;
        }
    };
};

pub fn getEnumDecl(zir: *const Zir, enum_decl: Inst.Index) UnwrappedEnumDecl {
    const inst_data = zir.instructions.get(@intFromEnum(enum_decl));
    assert(inst_data.tag == .extended);
    assert(inst_data.data.extended.opcode == .enum_decl);
    const small: Inst.EnumDecl.Small = @bitCast(inst_data.data.extended.small);
    const extra = zir.extraData(Inst.EnumDecl, inst_data.data.extended.operand);
    var extra_index = extra.end;
    const captures_len: u32 = if (small.has_captures_len) blk: {
        const captures_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk captures_len;
    } else 0;
    const decls_len: u32 = if (small.has_decls_len) blk: {
        const decls_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk decls_len;
    } else 0;
    const fields_len: u32 = if (small.has_fields_len) blk: {
        const fields_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk fields_len;
    } else 0;
    const tag_type_body_len: u32 = if (small.has_tag_type) len: {
        const body_len = zir.extra[extra_index];
        extra_index += 1;
        break :len body_len;
    } else 0;
    const captures: []const Inst.Capture = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const capture_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const decls: []const Inst.Index = @ptrCast(zir.extra[extra_index..][0..decls_len]);
    extra_index += decls_len;
    const field_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..fields_len]);
    extra_index += fields_len;
    const field_value_body_lens: ?[]const u32 = if (small.any_field_values) lens: {
        const lens = zir.extra[extra_index..][0..fields_len];
        extra_index += fields_len;
        break :lens @ptrCast(lens);
    } else null;
    const tag_type_body: ?[]const Zir.Inst.Index = switch (tag_type_body_len) {
        0 => null,
        else => |n| zir.bodySlice(extra_index, n),
    };
    extra_index += tag_type_body_len;
    const field_bodies_overlong: []const Inst.Index = @ptrCast(zir.extra[extra_index..]);
    return .{
        .src_line = extra.data.src_line,
        .src_node = extra.data.src_node,
        .name_strategy = small.name_strategy,
        .captures = captures,
        .capture_names = capture_names,
        .decls = decls,
        .tag_type_body = tag_type_body,
        .nonexhaustive = small.nonexhaustive,
        .field_names = field_names,
        .field_value_body_lens = field_value_body_lens,
        .field_bodies_overlong = field_bodies_overlong,
    };
}
pub const UnwrappedEnumDecl = struct {
    src_line: u32,
    src_node: Ast.Node.Index,
    name_strategy: Inst.NameStrategy,

    captures: []const Inst.Capture,
    capture_names: []const NullTerminatedString,

    decls: []const Inst.Index,

    tag_type_body: ?[]const Inst.Index,
    nonexhaustive: bool,

    field_names: []const NullTerminatedString,
    field_value_body_lens: ?[]const u32,
    field_bodies_overlong: []const Inst.Index,

    pub fn iterateFields(enum_decl: UnwrappedEnumDecl) FieldIterator {
        return .{
            .next_idx = 0,
            .names = enum_decl.field_names,
            .value_body_lens = enum_decl.field_value_body_lens,
            .bodies_overlong = enum_decl.field_bodies_overlong,
        };
    }

    pub const FieldIterator = struct {
        next_idx: u32,
        names: []const NullTerminatedString,
        value_body_lens: ?[]const u32,
        bodies_overlong: []const Inst.Index,
        pub const Field = struct {
            idx: u32,
            name: NullTerminatedString,
            value_body: ?[]const Inst.Index,
        };
        pub fn next(it: *FieldIterator) ?Field {
            const idx = it.next_idx;
            if (idx == it.names.len) return null;
            it.next_idx += 1;
            return .{
                .idx = idx,
                .name = it.names[idx],
                .value_body = it.body(if (it.value_body_lens) |l| l[idx] else 0),
            };
        }
        fn body(it: *FieldIterator, len: u32) ?[]const Inst.Index {
            if (len == 0) return null;
            const b = it.bodies_overlong[0..len];
            it.bodies_overlong = it.bodies_overlong[len..];
            return b;
        }
    };
};

pub fn getOpaqueDecl(zir: *const Zir, opaque_decl: Inst.Index) UnwrappedOpaqueDecl {
    const inst_data = zir.instructions.get(@intFromEnum(opaque_decl));
    assert(inst_data.tag == .extended);
    assert(inst_data.data.extended.opcode == .opaque_decl);
    const small: Inst.OpaqueDecl.Small = @bitCast(inst_data.data.extended.small);
    const extra = zir.extraData(Inst.OpaqueDecl, inst_data.data.extended.operand);
    var extra_index = extra.end;
    const captures_len: u32 = if (small.has_captures_len) blk: {
        const captures_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk captures_len;
    } else 0;
    const decls_len: u32 = if (small.has_decls_len) blk: {
        const decls_len = zir.extra[extra_index];
        extra_index += 1;
        break :blk decls_len;
    } else 0;
    const captures: []const Inst.Capture = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const capture_names: []const NullTerminatedString = @ptrCast(zir.extra[extra_index..][0..captures_len]);
    extra_index += captures_len;
    const decls: []const Inst.Index = @ptrCast(zir.extra[extra_index..][0..decls_len]);
    extra_index += decls_len;
    return .{
        .src_line = extra.data.src_line,
        .src_node = extra.data.src_node,
        .name_strategy = small.name_strategy,
        .captures = captures,
        .capture_names = capture_names,
        .decls = decls,
    };
}
pub const UnwrappedOpaqueDecl = struct {
    src_line: u32,
    src_node: Ast.Node.Index,
    name_strategy: Inst.NameStrategy,
    captures: []const Inst.Capture,
    capture_names: []const NullTerminatedString,
    decls: []const Inst.Index,
};



---
File: /std/zig/Zoir.zig
---

//! Zig Object Intermediate Representation.
//! Simplified AST for the ZON (Zig Object Notation) format.
//! `ZonGen` converts `Ast` to `Zoir`.
const Zoir = @This();

const std = @import("std");
const Io = std.Io;
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const Ast = std.zig.Ast;

nodes: std.MultiArrayList(Node.Repr).Slice,
extra: []u32,
limbs: []std.math.big.Limb,
string_bytes: []u8,

compile_errors: []Zoir.CompileError,
error_notes: []Zoir.CompileError.Note,

/// The data stored at byte offset 0 when ZOIR is stored in a file.
pub const Header = extern struct {
    nodes_len: u32,
    extra_len: u32,
    limbs_len: u32,
    string_bytes_len: u32,
    compile_errors_len: u32,
    error_notes_len: u32,

    /// We could leave this as padding, however it triggers a Valgrind warning because
    /// we read and write undefined bytes to the file system. This is harmless, but
    /// it's essentially free to have a zero field here and makes the warning go away,
    /// making it more likely that following Valgrind warnings will be taken seriously.
    unused: u64 = 0,

    stat_inode: Io.File.INode,
    stat_size: u64,
    stat_mtime: i128,

    comptime {
        // Check that `unused` is working as expected
        assert(std.meta.hasUniqueRepresentation(Header));
    }
};

pub fn hasCompileErrors(zoir: Zoir) bool {
    if (zoir.compile_errors.len > 0) {
        assert(zoir.nodes.len == 0);
        assert(zoir.extra.len == 0);
        assert(zoir.limbs.len == 0);
        return true;
    } else {
        assert(zoir.error_notes.len == 0);
        return false;
    }
}

pub fn deinit(zoir: Zoir, gpa: Allocator) void {
    var nodes = zoir.nodes;
    nodes.deinit(gpa);

    gpa.free(zoir.extra);
    gpa.free(zoir.limbs);
    gpa.free(zoir.string_bytes);
    gpa.free(zoir.compile_errors);
    gpa.free(zoir.error_notes);
}

pub const Node = union(enum) {
    /// A literal `true` value.
    true,
    /// A literal `false` value.
    false,
    /// A literal `null` value.
    null,
    /// A literal `inf` value.
    pos_inf,
    /// A literal `-inf` value.
    neg_inf,
    /// A literal `nan` value.
    nan,
    /// An integer literal.
    int_literal: union(enum) {
        small: i32,
        big: std.math.big.int.Const,
    },
    /// A floating-point literal.
    float_literal: f128,
    /// A Unicode codepoint literal.
    char_literal: u21,
    /// An enum literal. The string is the literal, i.e. `foo` for `.foo`.
    enum_literal: NullTerminatedString,
    /// A string literal.
    string_literal: []const u8,
    /// An empty struct/array literal, i.e. `.{}`.
    empty_literal,
    /// An array literal. The `Range` gives the elements of the array literal.
    array_literal: Node.Index.Range,
    /// A struct literal. `names.len` is always equal to `vals.len`.
    struct_literal: struct {
        names: []const NullTerminatedString,
        vals: Node.Index.Range,
    },

    pub const Index = enum(u32) {
        root = 0,
        _,

        pub fn get(idx: Index, zoir: Zoir) Node {
            const repr = zoir.nodes.get(@intFromEnum(idx));
            return switch (repr.tag) {
                .true => .true,
                .false => .false,
                .null => .null,
                .pos_inf => .pos_inf,
                .neg_inf => .neg_inf,
                .nan => .nan,
                .int_literal_small => .{ .int_literal = .{ .small = @bitCast(repr.data) } },
                .int_literal_pos, .int_literal_neg => .{ .int_literal = .{ .big = .{
                    .limbs = l: {
                        const limb_count, const limbs_idx = zoir.extra[repr.data..][0..2].*;
                        break :l zoir.limbs[limbs_idx..][0..limb_count];
                    },
                    .positive = switch (repr.tag) {
                        .int_literal_pos => true,
                        .int_literal_neg => false,
                        else => unreachable,
                    },
                } } },
                .float_literal_small => .{ .float_literal = @as(f32, @bitCast(repr.data)) },
                .float_literal => .{ .float_literal = @bitCast(zoir.extra[repr.data..][0..4].*) },
                .char_literal => .{ .char_literal = @intCast(repr.data) },
                .enum_literal => .{ .enum_literal = @enumFromInt(repr.data) },
                .string_literal => .{ .string_literal = s: {
                    const start, const len = zoir.extra[repr.data..][0..2].*;
                    break :s zoir.string_bytes[start..][0..len];
                } },
                .string_literal_null => .{ .string_literal = NullTerminatedString.get(@enumFromInt(repr.data), zoir) },
                .empty_literal => .empty_literal,
                .array_literal => .{ .array_literal = a: {
                    const elem_count, const first_elem = zoir.extra[repr.data..][0..2].*;
                    break :a .{ .start = @enumFromInt(first_elem), .len = elem_count };
                } },
                .struct_literal => .{ .struct_literal = s: {
                    const elem_count, const first_elem = zoir.extra[repr.data..][0..2].*;
                    const field_names = zoir.extra[repr.data + 2 ..][0..elem_count];
                    break :s .{
                        .names = @ptrCast(field_names),
                        .vals = .{ .start = @enumFromInt(first_elem), .len = elem_count },
                    };
                } },
            };
        }

        pub fn getAstNode(idx: Index, zoir: Zoir) std.zig.Ast.Node.Index {
            return zoir.nodes.items(.ast_node)[@intFromEnum(idx)];
        }

        pub const Range = struct {
            start: Index,
            len: u32,

            pub fn at(r: Range, i: u32) Index {
                assert(i < r.len);
                return @enumFromInt(@intFromEnum(r.start) + i);
            }
        };
    };

    pub const Repr = struct {
        tag: Tag,
        data: u32,
        ast_node: std.zig.Ast.Node.Index,

        pub const Tag = enum(u8) {
            /// `data` is ignored.
            true,
            /// `data` is ignored.
            false,
            /// `data` is ignored.
            null,
            /// `data` is ignored.
            pos_inf,
            /// `data` is ignored.
            neg_inf,
            /// `data` is ignored.
            nan,
            /// `data` is the `i32` value.
            int_literal_small,
            /// `data` is index into `extra` of:
            /// * `limb_count: u32`
            /// * `limbs_idx: u32`
            int_literal_pos,
            /// Identical to `int_literal_pos`, except the value is negative.
            int_literal_neg,
            /// `data` is the `f32` value.
            float_literal_small,
            /// `data` is index into `extra` of 4 elements which are a bitcast `f128`.
            float_literal,
            /// `data` is the `u32` value.
            char_literal,
            /// `data` is a `NullTerminatedString`.
            enum_literal,
            /// `data` is index into `extra` of:
            /// * `start: u32`
            /// * `len: u32`
            string_literal,
            /// Null-terminated string literal,
            /// `data` is a `NullTerminatedString`.
            string_literal_null,
            /// An empty struct/array literal, `.{}`.
            /// `data` is ignored.
            empty_literal,
            /// `data` is index into `extra` of:
            /// * `elem_count: u32`
            /// * `first_elem: Node.Index`
            /// The nodes `first_elem .. first_elem + elem_count` are the children.
            array_literal,
            /// `data` is index into `extra` of:
            /// * `elem_count: u32`
            /// * `first_elem: Node.Index`
            /// * `field_name: NullTerminatedString` for each `elem_count`
            /// The nodes `first_elem .. first_elem + elem_count` are the children.
            struct_literal,
        };
    };
};

pub const NullTerminatedString = enum(u32) {
    _,
    pub fn get(nts: NullTerminatedString, zoir: Zoir) [:0]const u8 {
        const idx = std.mem.findScalar(u8, zoir.string_bytes[@intFromEnum(nts)..], 0).?;
        return zoir.string_bytes[@intFromEnum(nts)..][0..idx :0];
    }
};

pub const CompileError = extern struct {
    msg: NullTerminatedString,
    token: Ast.OptionalTokenIndex,
    /// If `token == .none`, this is an `Ast.Node.Index`.
    /// Otherwise, this is a byte offset into `token`.
    node_or_offset: u32,

    /// Ignored if `note_count == 0`.
    first_note: u32,
    note_count: u32,

    pub fn getNotes(err: CompileError, zoir: Zoir) []const Note {
        return zoir.error_notes[err.first_note..][0..err.note_count];
    }

    pub const Note = extern struct {
        msg: NullTerminatedString,
        token: Ast.OptionalTokenIndex,
        /// If `token == .none`, this is an `Ast.Node.Index`.
        /// Otherwise, this is a byte offset into `token`.
        node_or_offset: u32,
    };

    comptime {
        assert(std.meta.hasUniqueRepresentation(CompileError));
        assert(std.meta.hasUniqueRepresentation(Note));
    }
};



---
File: /std/zig/ZonGen.zig
---

//! Ingests an `Ast` and produces a `Zoir`.

const std = @import("std");
const assert = std.debug.assert;
const mem = std.mem;
const Allocator = mem.Allocator;
const StringIndexAdapter = std.hash_map.StringIndexAdapter;
const StringIndexContext = std.hash_map.StringIndexContext;
const ZonGen = @This();
const Zoir = @import("Zoir.zig");
const Ast = @import("Ast.zig");
const Writer = std.Io.Writer;

gpa: Allocator,
tree: Ast,

options: Options,

nodes: std.MultiArrayList(Zoir.Node.Repr),
extra: std.ArrayList(u32),
limbs: std.ArrayList(std.math.big.Limb),
string_bytes: std.ArrayList(u8),
string_table: std.HashMapUnmanaged(u32, void, StringIndexContext, std.hash_map.default_max_load_percentage),

compile_errors: std.ArrayList(Zoir.CompileError),
error_notes: std.ArrayList(Zoir.CompileError.Note),

pub const Options = struct {
    /// When false, string literals are not parsed. `string_literal` nodes will contain empty
    /// strings, and errors that normally occur during string parsing will not be raised.
    ///
    /// `parseStrLit` and `strLitSizeHint` may be used to parse string literals after the fact.
    parse_str_lits: bool = true,
};

pub fn generate(gpa: Allocator, tree: Ast, options: Options) Allocator.Error!Zoir {
    assert(tree.mode == .zon);

    var zg: ZonGen = .{
        .gpa = gpa,
        .tree = tree,
        .options = options,
        .nodes = .empty,
        .extra = .empty,
        .limbs = .empty,
        .string_bytes = .empty,
        .string_table = .empty,
        .compile_errors = .empty,
        .error_notes = .empty,
    };
    defer {
        zg.nodes.deinit(gpa);
        zg.extra.deinit(gpa);
        zg.limbs.deinit(gpa);
        zg.string_bytes.deinit(gpa);
        zg.string_table.deinit(gpa);
        zg.compile_errors.deinit(gpa);
        zg.error_notes.deinit(gpa);
    }

    if (tree.errors.len == 0) {
        const root_ast_node = tree.rootDecls()[0];
        try zg.nodes.append(gpa, undefined); // index 0; root node
        try zg.expr(root_ast_node, .root);
    } else {
        try zg.lowerAstErrors();
    }

    if (zg.compile_errors.items.len > 0) {
        const string_bytes = try zg.string_bytes.toOwnedSlice(gpa);
        errdefer gpa.free(string_bytes);
        const compile_errors = try zg.compile_errors.toOwnedSlice(gpa);
        errdefer gpa.free(compile_errors);
        const error_notes = try zg.error_notes.toOwnedSlice(gpa);
        errdefer gpa.free(error_notes);

        return .{
            .nodes = .empty,
            .extra = &.{},
            .limbs = &.{},
            .string_bytes = string_bytes,
            .compile_errors = compile_errors,
            .error_notes = error_notes,
        };
    } else {
        assert(zg.error_notes.items.len == 0);

        var nodes = zg.nodes.toOwnedSlice();
        errdefer nodes.deinit(gpa);
        const extra = try zg.extra.toOwnedSlice(gpa);
        errdefer gpa.free(extra);
        const limbs = try zg.limbs.toOwnedSlice(gpa);
        errdefer gpa.free(limbs);
        const string_bytes = try zg.string_bytes.toOwnedSlice(gpa);
        errdefer gpa.free(string_bytes);

        return .{
            .nodes = nodes,
            .extra = extra,
            .limbs = limbs,
            .string_bytes = string_bytes,
            .compile_errors = &.{},
            .error_notes = &.{},
        };
    }
}

fn expr(zg: *ZonGen, node: Ast.Node.Index, dest_node: Zoir.Node.Index) Allocator.Error!void {
    const gpa = zg.gpa;
    const tree = zg.tree;

    switch (tree.nodeTag(node)) {
        .root => unreachable,
        .test_decl => unreachable,
        .container_field_init => unreachable,
        .container_field_align => unreachable,
        .container_field => unreachable,
        .fn_decl => unreachable,
        .global_var_decl => unreachable,
        .local_var_decl => unreachable,
        .simple_var_decl => unreachable,
        .aligned_var_decl => unreachable,
        .@"defer" => unreachable,
        .@"errdefer" => unreachable,
        .switch_case => unreachable,
        .switch_case_inline => unreachable,
        .switch_case_one => unreachable,
        .switch_case_inline_one => unreachable,
        .switch_range => unreachable,
        .asm_output => unreachable,
        .asm_input => unreachable,
        .for_range => unreachable,
        .assign => unreachable,
        .assign_destructure => unreachable,
        .assign_shl => unreachable,
        .assign_shl_sat => unreachable,
        .assign_shr => unreachable,
        .assign_bit_and => unreachable,
        .assign_bit_or => unreachable,
        .assign_bit_xor => unreachable,
        .assign_div => unreachable,
        .assign_sub => unreachable,
        .assign_sub_wrap => unreachable,
        .assign_sub_sat => unreachable,
        .assign_mod => unreachable,
        .assign_add => unreachable,
        .assign_add_wrap => unreachable,
        .assign_add_sat => unreachable,
        .assign_mul => unreachable,
        .assign_mul_wrap => unreachable,
        .assign_mul_sat => unreachable,

        .shl,
        .shr,
        .add,
        .add_wrap,
        .add_sat,
        .sub,
        .sub_wrap,
        .sub_sat,
        .mul,
        .mul_wrap,
        .mul_sat,
        .div,
        .mod,
        .shl_sat,
        .bit_and,
        .bit_or,
        .bit_xor,
        .bang_equal,
        .equal_equal,
        .greater_than,
        .greater_or_equal,
        .less_than,
        .less_or_equal,
        .array_cat,
        .array_mult,
        .bool_and,
        .bool_or,
        .bool_not,
        .bit_not,
        .negation_wrap,
        => try zg.addErrorTok(tree.nodeMainToken(node), "operator '{s}' is not allowed in ZON", .{tree.tokenSlice(tree.nodeMainToken(node))}),

        .error_union,
        .merge_error_sets,
        .optional_type,
        .anyframe_literal,
        .anyframe_type,
        .ptr_type_aligned,
        .ptr_type_sentinel,
        .ptr_type,
        .ptr_type_bit_range,
        .container_decl,
        .container_decl_trailing,
        .container_decl_arg,
        .container_decl_arg_trailing,
        .container_decl_two,
        .container_decl_two_trailing,
        .tagged_union,
        .tagged_union_trailing,
        .tagged_union_enum_tag,
        .tagged_union_enum_tag_trailing,
        .tagged_union_two,
        .tagged_union_two_trailing,
        .array_type,
        .array_type_sentinel,
        .error_set_decl,
        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
        => try zg.addErrorNode(node, "types are not available in ZON", .{}),

        .call_one,
        .call_one_comma,
        .call,
        .call_comma,
        .@"return",
        .if_simple,
        .@"if",
        .while_simple,
        .while_cont,
        .@"while",
        .for_simple,
        .@"for",
        .@"catch",
        .@"orelse",
        .@"break",
        .@"continue",
        .@"switch",
        .switch_comma,
        .@"nosuspend",
        .@"suspend",
        .@"resume",
        .@"try",
        .unreachable_literal,
        => try zg.addErrorNode(node, "control flow is not allowed in ZON", .{}),

        .@"comptime" => try zg.addErrorNode(node, "keyword 'comptime' is not allowed in ZON", .{}),
        .asm_simple, .@"asm" => try zg.addErrorNode(node, "inline asm is not allowed in ZON", .{}),

        .builtin_call_two,
        .builtin_call_two_comma,
        .builtin_call,
        .builtin_call_comma,
        => try zg.addErrorNode(node, "builtin function calls are not allowed in ZON", .{}),

        .field_access => try zg.addErrorNode(node, "field accesses are not allowed in ZON", .{}),

        .slice_open,
        .slice,
        .slice_sentinel,
        => try zg.addErrorNode(node, "slice operator is not allowed in ZON", .{}),

        .deref, .address_of => try zg.addErrorTok(tree.nodeMainToken(node), "pointers are not available in ZON", .{}),
        .unwrap_optional => try zg.addErrorTok(tree.nodeMainToken(node), "optionals are not available in ZON", .{}),
        .error_value => try zg.addErrorNode(node, "errors are not available in ZON", .{}),

        .array_access => try zg.addErrorNode(node, "array indexing is not allowed in ZON", .{}),

        .block_two,
        .block_two_semicolon,
        .block,
        .block_semicolon,
        => {
            var buffer: [2]Ast.Node.Index = undefined;
            const statements = tree.blockStatements(&buffer, node).?;
            if (statements.len == 0) {
                try zg.addErrorNodeNotes(node, "void literals are not available in ZON", .{}, &.{
                    try zg.errNoteNode(node, "void union payloads can be represented by enum literals", .{}),
                });
            } else {
                try zg.addErrorNode(node, "blocks are not allowed in ZON", .{});
            }
        },

        .array_init_one,
        .array_init_one_comma,
        .array_init,
        .array_init_comma,
        .struct_init_one,
        .struct_init_one_comma,
        .struct_init,
        .struct_init_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;

            const type_node = if (tree.fullArrayInit(&buf, node)) |full|
                full.ast.type_expr.unwrap().?
            else if (tree.fullStructInit(&buf, node)) |full|
                full.ast.type_expr.unwrap().?
            else
                unreachable;

            try zg.addErrorNodeNotes(type_node, "types are not available in ZON", .{}, &.{
                try zg.errNoteNode(type_node, "replace the type with '.'", .{}),
            });
        },

        .grouped_expression => {
            try zg.addErrorTokNotes(tree.nodeMainToken(node), "expression grouping is not allowed in ZON", .{}, &.{
                try zg.errNoteTok(tree.nodeMainToken(node), "these parentheses are always redundant", .{}),
            });
            return zg.expr(tree.nodeData(node).node_and_token[0], dest_node);
        },

        .negation => {
            const child_node = tree.nodeData(node).node;
            switch (tree.nodeTag(child_node)) {
                .number_literal => return zg.numberLiteral(child_node, node, dest_node, .negative),
                .identifier => {
                    const child_ident = tree.tokenSlice(tree.nodeMainToken(child_node));
                    if (mem.eql(u8, child_ident, "inf")) {
                        zg.setNode(dest_node, .{
                            .tag = .neg_inf,
                            .data = 0, // ignored
                            .ast_node = node,
                        });
                        return;
                    }
                },
                else => {},
            }
            try zg.addErrorTok(tree.nodeMainToken(node), "expected number or 'inf' after '-'", .{});
        },
        .number_literal => try zg.numberLiteral(node, node, dest_node, .positive),
        .char_literal => try zg.charLiteral(node, dest_node),

        .identifier => try zg.identifier(node, dest_node),

        .enum_literal => {
            const str_index = zg.identAsString(tree.nodeMainToken(node)) catch |err| switch (err) {
                error.BadString => undefined, // doesn't matter, there's an error
                error.OutOfMemory => |e| return e,
            };
            zg.setNode(dest_node, .{
                .tag = .enum_literal,
                .data = @intFromEnum(str_index),
                .ast_node = node,
            });
        },
        .string_literal, .multiline_string_literal => if (zg.strLitAsString(node)) |result| switch (result) {
            .nts => |nts| zg.setNode(dest_node, .{
                .tag = .string_literal_null,
                .data = @intFromEnum(nts),
                .ast_node = node,
            }),
            .slice => |slice| {
                const extra_index: u32 = @intCast(zg.extra.items.len);
                try zg.extra.appendSlice(zg.gpa, &.{ slice.start, slice.len });
                zg.setNode(dest_node, .{
                    .tag = .string_literal,
                    .data = extra_index,
                    .ast_node = node,
                });
            },
        } else |err| switch (err) {
            error.BadString => {},
            error.OutOfMemory => |e| return e,
        },

        .array_init_dot_two,
        .array_init_dot_two_comma,
        .array_init_dot,
        .array_init_dot_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const full = tree.fullArrayInit(&buf, node).?;
            assert(full.ast.elements.len != 0); // Otherwise it would be a struct init
            assert(full.ast.type_expr == .none); // The tag was `array_init_dot_*`

            const first_elem: u32 = @intCast(zg.nodes.len);
            try zg.nodes.resize(gpa, zg.nodes.len + full.ast.elements.len);

            const extra_index: u32 = @intCast(zg.extra.items.len);
            try zg.extra.appendSlice(gpa, &.{
                @intCast(full.ast.elements.len),
                first_elem,
            });

            zg.setNode(dest_node, .{
                .tag = .array_literal,
                .data = extra_index,
                .ast_node = node,
            });

            for (full.ast.elements, first_elem..) |elem_node, elem_dest_node| {
                try zg.expr(elem_node, @enumFromInt(elem_dest_node));
            }
        },

        .struct_init_dot_two,
        .struct_init_dot_two_comma,
        .struct_init_dot,
        .struct_init_dot_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const full = tree.fullStructInit(&buf, node).?;
            assert(full.ast.type_expr == .none); // The tag was `struct_init_dot_*`

            if (full.ast.fields.len == 0) {
                zg.setNode(dest_node, .{
                    .tag = .empty_literal,
                    .data = 0, // ignored
                    .ast_node = node,
                });
                return;
            }

            const first_elem: u32 = @intCast(zg.nodes.len);
            try zg.nodes.resize(gpa, zg.nodes.len + full.ast.fields.len);

            const extra_index: u32 = @intCast(zg.extra.items.len);
            try zg.extra.ensureUnusedCapacity(gpa, 2 + full.ast.fields.len);
            zg.extra.appendSliceAssumeCapacity(&.{
                @intCast(full.ast.fields.len),
                first_elem,
            });
            const names_start = extra_index + 2;
            zg.extra.appendNTimesAssumeCapacity(undefined, full.ast.fields.len);

            zg.setNode(dest_node, .{
                .tag = .struct_literal,
                .data = extra_index,
                .ast_node = node,
            });

            // For short initializers, track the names on the stack rather than going through gpa.
            var sfba_state = std.heap.stackFallback(256, gpa);
            const sfba = sfba_state.get();
            var field_names: std.AutoHashMapUnmanaged(Zoir.NullTerminatedString, Ast.TokenIndex) = .empty;
            defer field_names.deinit(sfba);

            var reported_any_duplicate = false;

            for (full.ast.fields, names_start.., first_elem..) |elem_node, extra_name_idx, elem_dest_node| {
                const name_token = tree.firstToken(elem_node) - 2;
                if (zg.identAsString(name_token)) |name_str| {
                    zg.extra.items[extra_name_idx] = @intFromEnum(name_str);
                    const gop = try field_names.getOrPut(sfba, name_str);
                    if (gop.found_existing and !reported_any_duplicate) {
                        reported_any_duplicate = true;
                        const earlier_token = gop.value_ptr.*;
                        try zg.addErrorTokNotes(earlier_token, "duplicate struct field name", .{}, &.{
                            try zg.errNoteTok(name_token, "duplicate name here", .{}),
                        });
                    }
                    gop.value_ptr.* = name_token;
                } else |err| switch (err) {
                    error.BadString => {}, // there's an error, so it's fine to not populate `zg.extra`
                    error.OutOfMemory => |e| return e,
                }
                try zg.expr(elem_node, @enumFromInt(elem_dest_node));
            }
        },
    }
}

fn appendIdentStr(zg: *ZonGen, ident_token: Ast.TokenIndex) error{ OutOfMemory, BadString }!u32 {
    const gpa = zg.gpa;
    const tree = zg.tree;
    assert(tree.tokenTag(ident_token) == .identifier);
    const ident_name = tree.tokenSlice(ident_token);
    if (!mem.startsWith(u8, ident_name, "@")) {
        const start = zg.string_bytes.items.len;
        try zg.string_bytes.appendSlice(gpa, ident_name);
        return @intCast(start);
    }
    const offset = 1;
    const start: u32 = @intCast(zg.string_bytes.items.len);
    const raw_string = zg.tree.tokenSlice(ident_token)[offset..];
    try zg.string_bytes.ensureUnusedCapacity(gpa, raw_string.len);
    const result = r: {
        var aw: Writer.Allocating = .fromArrayList(gpa, &zg.string_bytes);
        defer zg.string_bytes = aw.toArrayList();
        break :r std.zig.string_literal.parseWrite(&aw.writer, raw_string) catch |err| switch (err) {
            error.WriteFailed => return error.OutOfMemory,
        };
    };
    switch (result) {
        .success => {},
        .failure => |err| {
            try zg.lowerStrLitError(err, ident_token, raw_string, offset);
            return error.BadString;
        },
    }

    const slice = zg.string_bytes.items[start..];
    if (mem.findScalar(u8, slice, 0) != null) {
        try zg.addErrorTok(ident_token, "identifier cannot contain null bytes", .{});
        return error.BadString;
    } else if (slice.len == 0) {
        try zg.addErrorTok(ident_token, "identifier cannot be empty", .{});
        return error.BadString;
    }
    return start;
}

/// Estimates the size of a string node without parsing it.
pub fn strLitSizeHint(tree: Ast, node: Ast.Node.Index) usize {
    switch (tree.nodeTag(node)) {
        // Parsed string literals are typically around the size of the raw strings.
        .string_literal => {
            const token = tree.nodeMainToken(node);
            const raw_string = tree.tokenSlice(token);
            return raw_string.len;
        },
        // Multiline string literal lengths can be computed exactly.
        .multiline_string_literal => {
            const first_tok, const last_tok = tree.nodeData(node).token_and_token;

            var size = tree.tokenSlice(first_tok)[2..].len;
            for (first_tok + 1..last_tok + 1) |tok_idx| {
                size += 1; // Newline
                size += tree.tokenSlice(@intCast(tok_idx))[2..].len;
            }
            return size;
        },
        else => unreachable,
    }
}

/// Parses the given node as a string literal.
pub fn parseStrLit(
    tree: Ast,
    node: Ast.Node.Index,
    writer: *Writer,
) Writer.Error!std.zig.string_literal.Result {
    switch (tree.nodeTag(node)) {
        .string_literal => {
            const token = tree.nodeMainToken(node);
            const raw_string = tree.tokenSlice(token);
            return std.zig.string_literal.parseWrite(writer, raw_string);
        },
        .multiline_string_literal => {
            const first_tok, const last_tok = tree.nodeData(node).token_and_token;

            // First line: do not append a newline.
            {
                const line_bytes = tree.tokenSlice(first_tok)[2..];
                try writer.writeAll(line_bytes);
            }

            // Following lines: each line prepends a newline.
            for (first_tok + 1..last_tok + 1) |tok_idx| {
                const line_bytes = tree.tokenSlice(@intCast(tok_idx))[2..];
                try writer.writeByte('\n');
                try writer.writeAll(line_bytes);
            }

            return .success;
        },
        // Node must represent a string
        else => unreachable,
    }
}

const StringLiteralResult = union(enum) {
    nts: Zoir.NullTerminatedString,
    slice: struct { start: u32, len: u32 },
};

fn strLitAsString(zg: *ZonGen, str_node: Ast.Node.Index) error{ OutOfMemory, BadString }!StringLiteralResult {
    if (!zg.options.parse_str_lits) return .{ .slice = .{ .start = 0, .len = 0 } };

    const gpa = zg.gpa;
    const string_bytes = &zg.string_bytes;
    const str_index: u32 = @intCast(zg.string_bytes.items.len);
    const size_hint = strLitSizeHint(zg.tree, str_node);
    try string_bytes.ensureUnusedCapacity(gpa, size_hint);
    const result = r: {
        var aw: Writer.Allocating = .fromArrayList(gpa, &zg.string_bytes);
        defer zg.string_bytes = aw.toArrayList();
        break :r parseStrLit(zg.tree, str_node, &aw.writer) catch |err| switch (err) {
            error.WriteFailed => return error.OutOfMemory,
        };
    };
    switch (result) {
        .success => {},
        .failure => |err| {
            const token = zg.tree.nodeMainToken(str_node);
            const raw_string = zg.tree.tokenSlice(token);
            try zg.lowerStrLitError(err, token, raw_string, 0);
            return error.BadString;
        },
    }
    const key: []const u8 = string_bytes.items[str_index..];
    if (std.mem.findScalar(u8, key, 0) != null) return .{ .slice = .{
        .start = str_index,
        .len = @intCast(key.len),
    } };
    const gop = try zg.string_table.getOrPutContextAdapted(
        gpa,
        key,
        StringIndexAdapter{ .bytes = string_bytes },
        StringIndexContext{ .bytes = string_bytes },
    );
    if (gop.found_existing) {
        string_bytes.shrinkRetainingCapacity(str_index);
        return .{ .nts = @enumFromInt(gop.key_ptr.*) };
    }
    gop.key_ptr.* = str_index;
    try string_bytes.append(gpa, 0);
    return .{ .nts = @enumFromInt(str_index) };
}

fn identAsString(zg: *ZonGen, ident_token: Ast.TokenIndex) !Zoir.NullTerminatedString {
    const gpa = zg.gpa;
    const string_bytes = &zg.string_bytes;
    const str_index = try zg.appendIdentStr(ident_token);
    const key: []const u8 = string_bytes.items[str_index..];
    const gop = try zg.string_table.getOrPutContextAdapted(
        gpa,
        key,
        StringIndexAdapter{ .bytes = string_bytes },
        StringIndexContext{ .bytes = string_bytes },
    );
    if (gop.found_existing) {
        string_bytes.shrinkRetainingCapacity(str_index);
        return @enumFromInt(gop.key_ptr.*);
    }
    gop.key_ptr.* = str_index;
    try string_bytes.append(gpa, 0);
    return @enumFromInt(str_index);
}

fn numberLiteral(zg: *ZonGen, num_node: Ast.Node.Index, src_node: Ast.Node.Index, dest_node: Zoir.Node.Index, sign: enum { negative, positive }) !void {
    const tree = zg.tree;
    const num_token = tree.nodeMainToken(num_node);
    const num_bytes = tree.tokenSlice(num_token);

    switch (std.zig.parseNumberLiteral(num_bytes)) {
        .int => |unsigned_num| {
            if (unsigned_num == 0 and sign == .negative) {
                try zg.addErrorTokNotes(num_token, "integer literal '-0' is ambiguous", .{}, &.{
                    try zg.errNoteTok(num_token, "use '0' for an integer zero", .{}),
                    try zg.errNoteTok(num_token, "use '-0.0' for a floating-point signed zero", .{}),
                });
                return;
            }
            const num: i65 = switch (sign) {
                .positive => unsigned_num,
                .negative => -@as(i65, unsigned_num),
            };
            if (std.math.cast(i32, num)) |x| {
                zg.setNode(dest_node, .{
                    .tag = .int_literal_small,
                    .data = @bitCast(x),
                    .ast_node = src_node,
                });
                return;
            }
            const max_limbs = comptime std.math.big.int.calcTwosCompLimbCount(@bitSizeOf(@TypeOf(num)));
            var limbs: [max_limbs]std.math.big.Limb = undefined;
            var big_int: std.math.big.int.Mutable = .init(&limbs, num);
            try zg.setBigIntLiteralNode(dest_node, src_node, big_int.toConst());
        },
        .big_int => |base| {
            const gpa = zg.gpa;
            const num_without_prefix = switch (base) {
                .decimal => num_bytes,
                .hex, .binary, .octal => num_bytes[2..],
            };
            var big_int: std.math.big.int.Managed = try .init(gpa);
            defer big_int.deinit();
            big_int.setString(@intFromEnum(base), num_without_prefix) catch |err| switch (err) {
                error.InvalidCharacter => unreachable, // caught in `parseNumberLiteral`
                error.InvalidBase => unreachable, // we only pass 16, 8, 2, see above
                error.OutOfMemory => return error.OutOfMemory,
            };
            switch (sign) {
                .positive => {},
                .negative => big_int.negate(),
            }
            try zg.setBigIntLiteralNode(dest_node, src_node, big_int.toConst());
        },
        .float => {
            const unsigned_num = std.fmt.parseFloat(f128, num_bytes) catch |err| switch (err) {
                error.InvalidCharacter => unreachable, // validated by tokenizer
            };
            const num: f128 = switch (sign) {
                .positive => unsigned_num,
                .negative => -unsigned_num,
            };

            {
                // If the value fits into an f32 without losing any precision, store it that way.
                @setFloatMode(.strict);
                const smaller_float: f32 = @floatCast(num);
                const bigger_again: f128 = smaller_float;
                if (bigger_again == num) {
                    zg.setNode(dest_node, .{
                        .tag = .float_literal_small,
                        .data = @bitCast(smaller_float),
                        .ast_node = src_node,
                    });
                    return;
                }
            }

            const elems: [4]u32 = @bitCast(num);
            const extra_index: u32 = @intCast(zg.extra.items.len);
            try zg.extra.appendSlice(zg.gpa, &elems);
            zg.setNode(dest_node, .{
                .tag = .float_literal,
                .data = extra_index,
                .ast_node = src_node,
            });
        },
        .failure => |err| try zg.lowerNumberError(err, num_token, num_bytes),
    }
}

fn setBigIntLiteralNode(zg: *ZonGen, dest_node: Zoir.Node.Index, src_node: Ast.Node.Index, val: std.math.big.int.Const) !void {
    try zg.extra.ensureUnusedCapacity(zg.gpa, 2);
    try zg.limbs.ensureUnusedCapacity(zg.gpa, val.limbs.len);

    const limbs_idx: u32 = @intCast(zg.limbs.items.len);
    zg.limbs.appendSliceAssumeCapacity(val.limbs);

    const extra_idx: u32 = @intCast(zg.extra.items.len);
    zg.extra.appendSliceAssumeCapacity(&.{ @intCast(val.limbs.len), limbs_idx });

    zg.setNode(dest_node, .{
        .tag = if (val.positive) .int_literal_pos else .int_literal_neg,
        .data = extra_idx,
        .ast_node = src_node,
    });
}

fn charLiteral(zg: *ZonGen, node: Ast.Node.Index, dest_node: Zoir.Node.Index) !void {
    const tree = zg.tree;
    assert(tree.nodeTag(node) == .char_literal);
    const main_token = tree.nodeMainToken(node);
    const slice = tree.tokenSlice(main_token);
    switch (std.zig.parseCharLiteral(slice)) {
        .success => |codepoint| zg.setNode(dest_node, .{
            .tag = .char_literal,
            .data = codepoint,
            .ast_node = node,
        }),
        .failure => |err| try zg.lowerStrLitError(err, main_token, slice, 0),
    }
}

fn identifier(zg: *ZonGen, node: Ast.Node.Index, dest_node: Zoir.Node.Index) !void {
    const tree = zg.tree;
    assert(tree.nodeTag(node) == .identifier);
    const main_token = tree.nodeMainToken(node);
    const ident = tree.tokenSlice(main_token);

    const tag: Zoir.Node.Repr.Tag = t: {
        if (mem.eql(u8, ident, "true")) break :t .true;
        if (mem.eql(u8, ident, "false")) break :t .false;
        if (mem.eql(u8, ident, "null")) break :t .null;
        if (mem.eql(u8, ident, "inf")) break :t .pos_inf;
        if (mem.eql(u8, ident, "nan")) break :t .nan;
        try zg.addErrorNodeNotes(node, "invalid expression", .{}, &.{
            try zg.errNoteNode(node, "ZON allows identifiers 'true', 'false', 'null', 'inf', and 'nan'", .{}),
            try zg.errNoteNode(node, "precede identifier with '.' for an enum literal", .{}),
        });
        return;
    };

    zg.setNode(dest_node, .{
        .tag = tag,
        .data = 0, // ignored
        .ast_node = node,
    });
}

fn setNode(zg: *ZonGen, dest: Zoir.Node.Index, repr: Zoir.Node.Repr) void {
    zg.nodes.set(@intFromEnum(dest), repr);
}

fn lowerStrLitError(
    zg: *ZonGen,
    err: std.zig.string_literal.Error,
    token: Ast.TokenIndex,
    raw_string: []const u8,
    offset: u32,
) Allocator.Error!void {
    return ZonGen.addErrorTokOff(zg, token, @intCast(offset + err.offset()), "{f}", .{err.fmt(raw_string)});
}

fn lowerNumberError(zg: *ZonGen, err: std.zig.number_literal.Error, token: Ast.TokenIndex, bytes: []const u8) Allocator.Error!void {
    const is_float = std.mem.findScalar(u8, bytes, '.') != null;
    switch (err) {
        .leading_zero => if (is_float) {
            try zg.addErrorTok(token, "number '{s}' has leading zero", .{bytes});
        } else {
            try zg.addErrorTokNotes(token, "number '{s}' has leading zero", .{bytes}, &.{
                try zg.errNoteTok(token, "use '0o' prefix for octal literals", .{}),
            });
        },
        .digit_after_base => try zg.addErrorTok(token, "expected a digit after base prefix", .{}),
        .upper_case_base => |i| try zg.addErrorTokOff(token, @intCast(i), "base prefix must be lowercase", .{}),
        .invalid_float_base => |i| try zg.addErrorTokOff(token, @intCast(i), "invalid base for float literal", .{}),
        .repeated_underscore => |i| try zg.addErrorTokOff(token, @intCast(i), "repeated digit separator", .{}),
        .invalid_underscore_after_special => |i| try zg.addErrorTokOff(token, @intCast(i), "expected digit before digit separator", .{}),
        .invalid_digit => |info| try zg.addErrorTokOff(token, @intCast(info.i), "invalid digit '{c}' for {s} base", .{ bytes[info.i], @tagName(info.base) }),
        .invalid_digit_exponent => |i| try zg.addErrorTokOff(token, @intCast(i), "invalid digit '{c}' in exponent", .{bytes[i]}),
        .duplicate_exponent => |i| try zg.addErrorTokOff(token, @intCast(i), "duplicate exponent", .{}),
        .exponent_after_underscore => |i| try zg.addErrorTokOff(token, @intCast(i), "expected digit before exponent", .{}),
        .special_after_underscore => |i| try zg.addErrorTokOff(token, @intCast(i), "expected digit before '{c}'", .{bytes[i]}),
        .trailing_special => |i| try zg.addErrorTokOff(token, @intCast(i), "expected digit after '{c}'", .{bytes[i - 1]}),
        .trailing_underscore => |i| try zg.addErrorTokOff(token, @intCast(i), "trailing digit separator", .{}),
        .duplicate_period => unreachable, // Validated by tokenizer
        .invalid_character => unreachable, // Validated by tokenizer
        .invalid_exponent_sign => |i| {
            assert(bytes.len >= 2 and bytes[0] == '0' and bytes[1] == 'x'); // Validated by tokenizer
            try zg.addErrorTokOff(token, @intCast(i), "sign '{c}' cannot follow digit '{c}' in hex base", .{ bytes[i], bytes[i - 1] });
        },
        .period_after_exponent => |i| try zg.addErrorTokOff(token, @intCast(i), "unexpected period after exponent", .{}),
    }
}

fn errNoteNode(zg: *ZonGen, node: Ast.Node.Index, comptime format: []const u8, args: anytype) Allocator.Error!Zoir.CompileError.Note {
    const message_idx: u32 = @intCast(zg.string_bytes.items.len);
    try zg.string_bytes.print(zg.gpa, format ++ "\x00", args);
    return .{
        .msg = @enumFromInt(message_idx),
        .token = .none,
        .node_or_offset = @intFromEnum(node),
    };
}

fn errNoteTok(zg: *ZonGen, tok: Ast.TokenIndex, comptime format: []const u8, args: anytype) Allocator.Error!Zoir.CompileError.Note {
    const message_idx: u32 = @intCast(zg.string_bytes.items.len);
    try zg.string_bytes.print(zg.gpa, format ++ "\x00", args);
    return .{
        .msg = @enumFromInt(message_idx),
        .token = .fromToken(tok),
        .node_or_offset = 0,
    };
}

fn addErrorNode(zg: *ZonGen, node: Ast.Node.Index, comptime format: []const u8, args: anytype) Allocator.Error!void {
    return zg.addErrorInner(.none, @intFromEnum(node), format, args, &.{});
}
fn addErrorTok(zg: *ZonGen, tok: Ast.TokenIndex, comptime format: []const u8, args: anytype) Allocator.Error!void {
    return zg.addErrorInner(.fromToken(tok), 0, format, args, &.{});
}
fn addErrorNodeNotes(zg: *ZonGen, node: Ast.Node.Index, comptime format: []const u8, args: anytype, notes: []const Zoir.CompileError.Note) Allocator.Error!void {
    return zg.addErrorInner(.none, @intFromEnum(node), format, args, notes);
}
fn addErrorTokNotes(zg: *ZonGen, tok: Ast.TokenIndex, comptime format: []const u8, args: anytype, notes: []const Zoir.CompileError.Note) Allocator.Error!void {
    return zg.addErrorInner(.fromToken(tok), 0, format, args, notes);
}
fn addErrorTokOff(zg: *ZonGen, tok: Ast.TokenIndex, offset: u32, comptime format: []const u8, args: anytype) Allocator.Error!void {
    return zg.addErrorInner(.fromToken(tok), offset, format, args, &.{});
}
fn addErrorTokNotesOff(zg: *ZonGen, tok: Ast.TokenIndex, offset: u32, comptime format: []const u8, args: anytype, notes: []const Zoir.CompileError.Note) Allocator.Error!void {
    return zg.addErrorInner(.fromToken(tok), offset, format, args, notes);
}

fn addErrorInner(
    zg: *ZonGen,
    token: Ast.OptionalTokenIndex,
    node_or_offset: u32,
    comptime format: []const u8,
    args: anytype,
    notes: []const Zoir.CompileError.Note,
) Allocator.Error!void {
    const gpa = zg.gpa;

    const first_note: u32 = @intCast(zg.error_notes.items.len);
    try zg.error_notes.appendSlice(gpa, notes);

    const message_idx: u32 = @intCast(zg.string_bytes.items.len);
    try zg.string_bytes.print(gpa, format ++ "\x00", args);

    try zg.compile_errors.append(gpa, .{
        .msg = @enumFromInt(message_idx),
        .token = token,
        .node_or_offset = node_or_offset,
        .first_note = first_note,
        .note_count = @intCast(notes.len),
    });
}

fn lowerAstErrors(zg: *ZonGen) Allocator.Error!void {
    const gpa = zg.gpa;
    const tree = zg.tree;
    assert(tree.errors.len > 0);

    var msg: Writer.Allocating = .init(gpa);
    defer msg.deinit();
    const msg_bw = &msg.writer;

    var notes: std.ArrayList(Zoir.CompileError.Note) = .empty;
    defer notes.deinit(gpa);

    var cur_err = tree.errors[0];
    for (tree.errors[1..]) |err| {
        if (err.is_note) {
            tree.renderError(err, msg_bw) catch return error.OutOfMemory;
            try notes.append(gpa, try zg.errNoteTok(err.token, "{s}", .{msg.written()}));
        } else {
            // Flush error
            tree.renderError(cur_err, msg_bw) catch return error.OutOfMemory;
            const extra_offset = tree.errorOffset(cur_err);
            try zg.addErrorTokNotesOff(cur_err.token, extra_offset, "{s}", .{msg.written()}, notes.items);
            notes.clearRetainingCapacity();
            cur_err = err;

            // TODO: `Parse` currently does not have good error recovery
            // mechanisms, so the remaining errors could be bogus. As such,
            // we'll ignore all remaining errors for now. We should improve
            // `Parse` so that we can report all the errors.
            return;
        }
        msg.clearRetainingCapacity();
    }

    // Flush error
    const extra_offset = tree.errorOffset(cur_err);
    tree.renderError(cur_err, msg_bw) catch return error.OutOfMemory;
    try zg.addErrorTokNotesOff(cur_err.token, extra_offset, "{s}", .{msg.written()}, notes.items);
}



---
File: /std/zon/parse.zig
---

//! The simplest way to parse ZON at runtime is to use `fromSlice`/`fromSliceAlloc`.
//!
//! Note that if you need to parse ZON at compile time, you may use `@import`.
//!
//! Parsing from individual Zoir nodes is also available:
//! * `fromZoir`/`fromZoirAlloc`
//! * `fromZoirNode`/`fromZoirNodeAlloc`
//!
//! For lower level control over parsing, see `std.zig.Zoir`.

const std = @import("std");
const builtin = @import("builtin");
const Allocator = std.mem.Allocator;
const Ast = std.zig.Ast;
const Zoir = std.zig.Zoir;
const ZonGen = std.zig.ZonGen;
const TokenIndex = std.zig.Ast.TokenIndex;
const Base = std.zig.number_literal.Base;
const StrLitErr = std.zig.string_literal.Error;
const NumberLiteralError = std.zig.number_literal.Error;
const assert = std.debug.assert;
const ArrayList = std.ArrayList;

/// Rename when adding or removing support for a type.
const valid_types = {};

/// Configuration for the runtime parser.
pub const Options = struct {
    /// If true, unknown fields do not error.
    ignore_unknown_fields: bool = false,
    /// If true, the parser cleans up partially parsed values on error. This requires some extra
    /// bookkeeping, so you may want to turn it off if you don't need this feature (e.g. because
    /// you're using arena allocation.)
    free_on_error: bool = true,
};

pub const Error = union(enum) {
    zoir: Zoir.CompileError,
    type_check: Error.TypeCheckFailure,

    pub const Note = union(enum) {
        zoir: Zoir.CompileError.Note,
        type_check: TypeCheckFailure.Note,

        pub const Iterator = struct {
            index: usize = 0,
            err: Error,
            diag: *const Diagnostics,

            pub fn next(self: *@This()) ?Note {
                switch (self.err) {
                    .zoir => |err| {
                        if (self.index >= err.note_count) return null;
                        const note = err.getNotes(self.diag.zoir)[self.index];
                        self.index += 1;
                        return .{ .zoir = note };
                    },
                    .type_check => |err| {
                        if (self.index >= err.getNoteCount()) return null;
                        const note = err.getNote(self.index);
                        self.index += 1;
                        return .{ .type_check = note };
                    },
                }
            }
        };

        fn formatMessage(self: []const u8, w: *std.Io.Writer) std.Io.Writer.Error!void {
            // Just writes the string for now, but we're keeping this behind a formatter so we have
            // the option to extend it in the future to print more advanced messages (like `Error`
            // does) without breaking the API.
            try w.writeAll(self);
        }

        pub fn fmtMessage(self: Note, diag: *const Diagnostics) std.fmt.Alt([]const u8, Note.formatMessage) {
            return .{ .data = switch (self) {
                .zoir => |note| note.msg.get(diag.zoir),
                .type_check => |note| note.msg,
            } };
        }

        pub fn getLocation(self: Note, diag: *const Diagnostics) Ast.Location {
            switch (self) {
                .zoir => |note| return zoirErrorLocation(diag.ast, note.token, note.node_or_offset),
                .type_check => |note| return diag.ast.tokenLocation(note.offset, note.token),
            }
        }
    };

    pub const Iterator = struct {
        index: usize = 0,
        diag: *const Diagnostics,

        pub fn next(self: *@This()) ?Error {
            if (self.index < self.diag.zoir.compile_errors.len) {
                const result: Error = .{ .zoir = self.diag.zoir.compile_errors[self.index] };
                self.index += 1;
                return result;
            }

            if (self.diag.type_check) |err| {
                if (self.index == self.diag.zoir.compile_errors.len) {
                    const result: Error = .{ .type_check = err };
                    self.index += 1;
                    return result;
                }
            }

            return null;
        }
    };

    const TypeCheckFailure = struct {
        const Note = struct {
            token: Ast.TokenIndex,
            offset: u32,
            msg: []const u8,
            owned: bool,

            fn deinit(self: @This(), gpa: Allocator) void {
                if (self.owned) gpa.free(self.msg);
            }
        };

        message: []const u8,
        owned: bool,
        token: Ast.TokenIndex,
        offset: u32,
        note: ?@This().Note,

        fn deinit(self: @This(), gpa: Allocator) void {
            if (self.note) |note| note.deinit(gpa);
            if (self.owned) gpa.free(self.message);
        }

        fn getNoteCount(self: @This()) usize {
            return @intFromBool(self.note != null);
        }

        fn getNote(self: @This(), index: usize) @This().Note {
            assert(index == 0);
            return self.note.?;
        }
    };

    const FormatMessage = struct {
        err: Error,
        diag: *const Diagnostics,
    };

    fn formatMessage(self: FormatMessage, w: *std.Io.Writer) std.Io.Writer.Error!void {
        switch (self.err) {
            .zoir => |err| try w.writeAll(err.msg.get(self.diag.zoir)),
            .type_check => |tc| try w.writeAll(tc.message),
        }
    }

    pub fn fmtMessage(self: @This(), diag: *const Diagnostics) std.fmt.Alt(FormatMessage, formatMessage) {
        return .{ .data = .{
            .err = self,
            .diag = diag,
        } };
    }

    pub fn getLocation(self: @This(), diag: *const Diagnostics) Ast.Location {
        return switch (self) {
            .zoir => |err| return zoirErrorLocation(
                diag.ast,
                err.token,
                err.node_or_offset,
            ),
            .type_check => |err| return diag.ast.tokenLocation(err.offset, err.token),
        };
    }

    pub fn iterateNotes(self: @This(), diag: *const Diagnostics) Note.Iterator {
        return .{ .err = self, .diag = diag };
    }

    fn zoirErrorLocation(ast: Ast, maybe_token: Ast.OptionalTokenIndex, node_or_offset: u32) Ast.Location {
        if (maybe_token.unwrap()) |token| {
            var location = ast.tokenLocation(0, token);
            location.column += node_or_offset;
            return location;
        } else {
            const ast_node: Ast.Node.Index = @enumFromInt(node_or_offset);
            const token = ast.nodeMainToken(ast_node);
            return ast.tokenLocation(0, token);
        }
    }
};

/// Information about the success or failure of a parse.
pub const Diagnostics = struct {
    ast: Ast = .{
        .source = "",
        .tokens = .empty,
        .nodes = .empty,
        .extra_data = &.{},
        .mode = .zon,
        .errors = &.{},
    },
    zoir: Zoir = .{
        .nodes = .empty,
        .extra = &.{},
        .limbs = &.{},
        .string_bytes = &.{},
        .compile_errors = &.{},
        .error_notes = &.{},
    },
    type_check: ?Error.TypeCheckFailure = null,

    fn assertEmpty(self: Diagnostics) void {
        assert(self.ast.tokens.len == 0);
        assert(self.zoir.nodes.len == 0);
        assert(self.type_check == null);
    }

    pub fn deinit(self: *Diagnostics, gpa: Allocator) void {
        self.ast.deinit(gpa);
        self.zoir.deinit(gpa);
        if (self.type_check) |tc| tc.deinit(gpa);
        self.* = undefined;
    }

    pub fn iterateErrors(self: *const Diagnostics) Error.Iterator {
        return .{ .diag = self };
    }

    pub fn format(self: *const @This(), w: *std.Io.Writer) std.Io.Writer.Error!void {
        var errors = self.iterateErrors();
        while (errors.next()) |err| {
            const loc = err.getLocation(self);
            const msg = err.fmtMessage(self);
            try w.print("{d}:{d}: error: {f}\n", .{ loc.line + 1, loc.column + 1, msg });

            var notes = err.iterateNotes(self);
            while (notes.next()) |note| {
                const note_loc = note.getLocation(self);
                const note_msg = note.fmtMessage(self);
                try w.print("{d}:{d}: note: {f}\n", .{
                    note_loc.line + 1,
                    note_loc.column + 1,
                    note_msg,
                });
            }
        }
    }
};

/// Parses the given slice as ZON.
///
/// Returns `error.OutOfMemory` on allocation failure, or `error.ParseZon` error if the ZON is
/// invalid or can not be deserialized into type `T`.
///
/// When the parser returns `error.ParseZon`, it will also store a human readable explanation in
/// `diag` if non null. If diag is not null, it must be initialized to `.{}`.
///
/// Asserts at compile time that the result type doesn't contain pointers. As such, the result
/// doesn't need to be freed.
///
/// An allocator is still required for temporary allocations made during parsing.
pub fn fromSlice(
    T: type,
    gpa: Allocator,
    source: [:0]const u8,
    diag: ?*Diagnostics,
    options: Options,
) error{ OutOfMemory, ParseZon }!T {
    comptime assert(!requiresAllocator(T));
    return fromSliceAlloc(T, gpa, source, diag, options);
}

/// Like `fromSlice`, but the result may contain pointers. To automatically free the result, see
/// `free`.
pub fn fromSliceAlloc(
    /// The type to deserialize into. May not be or contain any of the following types:
    /// * Any comptime-only type, except in a comptime field
    /// * `type`
    /// * `void`, except as a union payload
    /// * `noreturn`
    /// * An error set/error union
    /// * A many-pointer or C-pointer
    /// * An opaque type, including `anyopaque`
    /// * An async frame type, including `anyframe` and `anyframe->T`
    /// * A function
    ///
    /// All other types are valid. Unsupported types will fail at compile time.
    T: type,
    gpa: Allocator,
    source: [:0]const u8,
    diag: ?*Diagnostics,
    options: Options,
) error{ OutOfMemory, ParseZon }!T {
    if (diag) |s| s.assertEmpty();

    var ast = try std.zig.Ast.parse(gpa, source, .zon);
    defer if (diag == null) ast.deinit(gpa);
    if (diag) |s| s.ast = ast;

    // If there's no diagnostics, Zoir exists for the lifetime of this function. If there is a
    // diagnostics, ownership is transferred to diagnostics.
    var zoir = try ZonGen.generate(gpa, ast, .{ .parse_str_lits = false });
    defer if (diag == null) zoir.deinit(gpa);

    if (diag) |s| s.* = .{};
    return fromZoirAlloc(T, gpa, ast, zoir, diag, options);
}

/// Like `fromSlice`, but operates on `Zoir` instead of ZON source.
pub fn fromZoir(
    T: type,
    ast: Ast,
    zoir: Zoir,
    diag: ?*Diagnostics,
    options: Options,
) error{ParseZon}!T {
    comptime assert(!requiresAllocator(T));
    var buf: [0]u8 = .{};
    var failing_allocator = std.heap.FixedBufferAllocator.init(&buf);
    return fromZoirAlloc(
        T,
        failing_allocator.allocator(),
        ast,
        zoir,
        diag,
        options,
    ) catch |err| switch (err) {
        error.OutOfMemory => unreachable, // Checked by comptime assertion above
        else => |e| return e,
    };
}

/// Like `fromSliceAlloc`, but operates on `Zoir` instead of ZON source.
pub fn fromZoirAlloc(
    T: type,
    gpa: Allocator,
    ast: Ast,
    zoir: Zoir,
    diag: ?*Diagnostics,
    options: Options,
) error{ OutOfMemory, ParseZon }!T {
    return fromZoirNodeAlloc(T, gpa, ast, zoir, .root, diag, options);
}

/// Like `fromZoir`, but the parse starts at `node` instead of root.
pub fn fromZoirNode(
    T: type,
    ast: Ast,
    zoir: Zoir,
    node: Zoir.Node.Index,
    diag: ?*Diagnostics,
    options: Options,
) error{ParseZon}!T {
    comptime assert(!requiresAllocator(T));
    var buf: [0]u8 = .{};
    var failing_allocator = std.heap.FixedBufferAllocator.init(&buf);
    return fromZoirNodeAlloc(
        T,
        failing_allocator.allocator(),
        ast,
        zoir,
        node,
        diag,
        options,
    ) catch |err| switch (err) {
        error.OutOfMemory => unreachable, // Checked by comptime assertion above
        else => |e| return e,
    };
}

/// Like `fromZoirAlloc`, but the parse starts at `node` instead of root.
pub fn fromZoirNodeAlloc(
    T: type,
    gpa: Allocator,
    ast: Ast,
    zoir: Zoir,
    node: Zoir.Node.Index,
    diag: ?*Diagnostics,
    options: Options,
) error{ OutOfMemory, ParseZon }!T {
    comptime assert(canParseType(T));

    if (diag) |s| {
        s.assertEmpty();
        s.ast = ast;
        s.zoir = zoir;
    }

    if (zoir.hasCompileErrors()) {
        return error.ParseZon;
    }

    var parser: Parser = .{
        .gpa = gpa,
        .ast = ast,
        .zoir = zoir,
        .options = options,
        .diag = diag,
    };

    return parser.parseExpr(T, node);
}

/// Frees ZON values.
///
/// Provided for convenience, you may also free these values on your own using the same allocator
/// passed into the parser.
///
/// Asserts at comptime that sufficient information is available via the type system to free this
/// value. Untagged unions, for example, will fail this assert.
pub fn free(gpa: Allocator, value: anytype) void {
    const Value = @TypeOf(value);

    _ = valid_types;
    switch (@typeInfo(Value)) {
        .bool, .int, .float, .@"enum" => {},
        .pointer => |pointer| {
            switch (pointer.size) {
                .one => {
                    free(gpa, value.*);
                    gpa.destroy(value);
                },
                .slice => {
                    for (value) |item| {
                        free(gpa, item);
                    }
                    gpa.free(value);
                },
                .many, .c => comptime unreachable,
            }
        },
        .array => {
            freeArray(gpa, @TypeOf(value), &value);
        },
        .vector => |vector| {
            const array: [vector.len]vector.child = value;
            freeArray(gpa, @TypeOf(array), &array);
        },
        .@"struct" => |@"struct"| inline for (@"struct".fields) |field| {
            free(gpa, @field(value, field.name));
        },
        .@"union" => |@"union"| if (@"union".tag_type == null) {
            if (comptime requiresAllocator(Value)) unreachable;
        } else switch (value) {
            inline else => |_, tag| {
                free(gpa, @field(value, @tagName(tag)));
            },
        },
        .optional => if (value) |some| {
            free(gpa, some);
        },
        .void => {},
        else => comptime unreachable,
    }
}

fn freeArray(gpa: Allocator, comptime A: type, array: *const A) void {
    for (array) |elem| free(gpa, elem);
}

fn requiresAllocator(T: type) bool {
    _ = valid_types;
    return switch (@typeInfo(T)) {
        .pointer => true,
        .array => |array| return array.len > 0 and requiresAllocator(array.child),
        .@"struct" => |@"struct"| inline for (@"struct".fields) |field| {
            if (requiresAllocator(field.type)) {
                break true;
            }
        } else false,
        .@"union" => |@"union"| inline for (@"union".fields) |field| {
            if (requiresAllocator(field.type)) {
                break true;
            }
        } else false,
        .optional => |optional| requiresAllocator(optional.child),
        .vector => |vector| return vector.len > 0 and requiresAllocator(vector.child),
        else => false,
    };
}

const Parser = struct {
    gpa: Allocator,
    ast: Ast,
    zoir: Zoir,
    diag: ?*Diagnostics,
    options: Options,

    const ParseExprError = error{ ParseZon, OutOfMemory };

    fn parseExpr(self: *@This(), T: type, node: Zoir.Node.Index) ParseExprError!T {
        return self.parseExprInner(T, node) catch |err| switch (err) {
            error.WrongType => return self.failExpectedType(T, node),
            else => |e| return e,
        };
    }

    const ParseExprInnerError = error{ ParseZon, OutOfMemory, WrongType };

    fn parseExprInner(
        self: *@This(),
        T: type,
        node: Zoir.Node.Index,
    ) ParseExprInnerError!T {
        if (T == Zoir.Node.Index) {
            return node;
        }

        switch (@typeInfo(T)) {
            .optional => |optional| if (node.get(self.zoir) == .null) {
                return null;
            } else {
                return try self.parseExprInner(optional.child, node);
            },
            .bool => return self.parseBool(node),
            .int => return self.parseInt(T, node),
            .float => return self.parseFloat(T, node),
            .@"enum" => return self.parseEnumLiteral(T, node),
            .pointer => |pointer| switch (pointer.size) {
                .one => {
                    const result = try self.gpa.create(pointer.child);
                    errdefer self.gpa.destroy(result);
                    result.* = try self.parseExprInner(pointer.child, node);
                    return result;
                },
                .slice => return self.parseSlicePointer(T, node),
                else => comptime unreachable,
            },
            .array => return self.parseArray(T, node),
            .vector => |vector| {
                const A = [vector.len]vector.child;
                return try self.parseArray(A, node);
            },
            .@"struct" => |@"struct"| if (@"struct".is_tuple)
                return self.parseTuple(T, node)
            else
                return self.parseStruct(T, node),
            .@"union" => return self.parseUnion(T, node),

            else => comptime unreachable,
        }
    }

    /// Prints a message of the form `expected T` where T is first converted to a ZON type. For
    /// example, `**?**u8` becomes `?u8`, and types that involve user specified type names are just
    /// referred to by the type of container.
    fn failExpectedType(
        self: @This(),
        T: type,
        node: Zoir.Node.Index,
    ) error{ ParseZon, OutOfMemory } {
        @branchHint(.cold);
        return self.failExpectedTypeInner(T, false, node);
    }

    fn failExpectedTypeInner(
        self: @This(),
        T: type,
        opt: bool,
        node: Zoir.Node.Index,
    ) error{ ParseZon, OutOfMemory } {
        _ = valid_types;
        switch (@typeInfo(T)) {
            .@"struct" => |@"struct"| if (@"struct".is_tuple) {
                if (opt) {
                    return self.failNode(node, "expected optional tuple");
                } else {
                    return self.failNode(node, "expected tuple");
                }
            } else {
                if (opt) {
                    return self.failNode(node, "expected optional struct");
                } else {
                    return self.failNode(node, "expected struct");
                }
            },
            .@"union" => if (opt) {
                return self.failNode(node, "expected optional union");
            } else {
                return self.failNode(node, "expected union");
            },
            .array => if (opt) {
                return self.failNode(node, "expected optional array");
            } else {
                return self.failNode(node, "expected array");
            },
            .pointer => |pointer| switch (pointer.size) {
                .one => return self.failExpectedTypeInner(pointer.child, opt, node),
                .slice => {
                    if (pointer.child == u8 and
                        pointer.is_const and
                        (pointer.sentinel() == null or pointer.sentinel() == 0) and
                        (pointer.alignment == null or pointer.alignment == 1))
                    {
                        if (opt) {
                            return self.failNode(node, "expected optional string");
                        } else {
                            return self.failNode(node, "expected string");
                        }
                    } else {
                        if (opt) {
                            return self.failNode(node, "expected optional array");
                        } else {
                            return self.failNode(node, "expected array");
                        }
                    }
                },
                else => comptime unreachable,
            },
            .vector, .bool, .int, .float => if (opt) {
                return self.failNodeFmt(node, "expected type '{s}'", .{@typeName(?T)});
            } else {
                return self.failNodeFmt(node, "expected type '{s}'", .{@typeName(T)});
            },
            .@"enum" => if (opt) {
                return self.failNode(node, "expected optional enum literal");
            } else {
                return self.failNode(node, "expected enum literal");
            },
            .optional => |optional| {
                return self.failExpectedTypeInner(optional.child, true, node);
            },
            else => comptime unreachable,
        }
    }

    fn parseBool(self: @This(), node: Zoir.Node.Index) !bool {
        switch (node.get(self.zoir)) {
            .true => return true,
            .false => return false,
            else => return error.WrongType,
        }
    }

    fn parseInt(self: @This(), T: type, node: Zoir.Node.Index) !T {
        switch (node.get(self.zoir)) {
            .int_literal => |int| switch (int) {
                .small => |val| return std.math.cast(T, val) orelse
                    self.failCannotRepresent(T, node),
                .big => |val| return val.toInt(T) catch
                    self.failCannotRepresent(T, node),
            },
            .float_literal => |val| return intFromFloatExact(T, val) orelse
                self.failCannotRepresent(T, node),

            .char_literal => |val| return std.math.cast(T, val) orelse
                self.failCannotRepresent(T, node),
            else => return error.WrongType,
        }
    }

    fn parseFloat(self: @This(), T: type, node: Zoir.Node.Index) !T {
        switch (node.get(self.zoir)) {
            .int_literal => |int| switch (int) {
                .small => |val| return @floatFromInt(val),
                .big => |val| return val.toFloat(T, .nearest_even)[0],
            },
            .float_literal => |val| return @floatCast(val),
            .pos_inf => return std.math.inf(T),
            .neg_inf => return -std.math.inf(T),
            .nan => return std.math.nan(T),
            .char_literal => |val| return @floatFromInt(val),
            else => return error.WrongType,
        }
    }

    fn parseEnumLiteral(self: @This(), T: type, node: Zoir.Node.Index) !T {
        switch (node.get(self.zoir)) {
            .enum_literal => |field_name| {
                // Create a comptime string map for the enum fields
                const enum_fields = @typeInfo(T).@"enum".fields;
                comptime var kvs_list: [enum_fields.len]struct { []const u8, T } = undefined;
                inline for (enum_fields, 0..) |field, i| {
                    kvs_list[i] = .{ field.name, @enumFromInt(field.value) };
                }
                const enum_tags = std.StaticStringMap(T).initComptime(kvs_list);

                // Get the tag if it exists
                const field_name_str = field_name.get(self.zoir);
                return enum_tags.get(field_name_str) orelse
                    self.failUnexpected(T, "enum literal", node, null, field_name_str);
            },
            else => return error.WrongType,
        }
    }

    fn parseSlicePointer(self: *@This(), T: type, node: Zoir.Node.Index) ParseExprInnerError!T {
        switch (node.get(self.zoir)) {
            .string_literal => return self.parseString(T, node),
            .array_literal => |nodes| return self.parseSlice(T, nodes),
            .empty_literal => return self.parseSlice(T, .{ .start = node, .len = 0 }),
            else => return error.WrongType,
        }
    }

    fn parseString(self: *@This(), T: type, node: Zoir.Node.Index) ParseExprInnerError!T {
        const ast_node = node.getAstNode(self.zoir);
        const pointer = @typeInfo(T).pointer;
        var size_hint = ZonGen.strLitSizeHint(self.ast, ast_node);
        if (pointer.sentinel() != null) size_hint += 1;

        var aw: std.Io.Writer.Allocating = .init(self.gpa);
        try aw.ensureUnusedCapacity(size_hint);
        defer aw.deinit();
        const result = ZonGen.parseStrLit(self.ast, ast_node, &aw.writer) catch return error.OutOfMemory;
        switch (result) {
            .success => {},
            .failure => |err| {
                const token = self.ast.nodeMainToken(ast_node);
                const raw_string = self.ast.tokenSlice(token);
                return self.failTokenFmt(token, @intCast(err.offset()), "{f}", .{err.fmt(raw_string)});
            },
        }

        if (pointer.child != u8 or
            pointer.size != .slice or
            !pointer.is_const or
            (pointer.sentinel() != null and pointer.sentinel() != 0) or
            (pointer.alignment != null and pointer.alignment != 1))
        {
            return error.WrongType;
        }

        if (pointer.sentinel() != null) {
            return aw.toOwnedSliceSentinel(0);
        } else {
            return aw.toOwnedSlice();
        }
    }

    fn parseSlice(self: *@This(), T: type, nodes: Zoir.Node.Index.Range) !T {
        const pointer = @typeInfo(T).pointer;

        // Make sure we're working with a slice
        switch (pointer.size) {
            .slice => {},
            .one, .many, .c => comptime unreachable,
        }

        // Allocate the slice
        const slice = try self.gpa.allocWithOptions(
            pointer.child,
            nodes.len,
            .fromByteUnitsOptional(pointer.alignment),
            pointer.sentinel(),
        );
        errdefer self.gpa.free(slice);

        // Parse the elements and return the slice
        for (slice, 0..) |*elem, i| {
            errdefer if (self.options.free_on_error) {
                for (slice[0..i]) |item| {
                    free(self.gpa, item);
                }
            };
            elem.* = try self.parseExpr(pointer.child, nodes.at(@intCast(i)));
        }

        return slice;
    }

    fn parseArray(self: *@This(), T: type, node: Zoir.Node.Index) !T {
        const nodes: Zoir.Node.Index.Range = switch (node.get(self.zoir)) {
            .array_literal => |nodes| nodes,
            .empty_literal => .{ .start = node, .len = 0 },
            else => return error.WrongType,
        };

        const array_info = @typeInfo(T).array;

        // Check if the size matches
        if (nodes.len < array_info.len) {
            return self.failNodeFmt(
                node,
                "expected {} array elements; found {}",
                .{ array_info.len, nodes.len },
            );
        } else if (nodes.len > array_info.len) {
            return self.failNodeFmt(
                nodes.at(array_info.len),
                "index {} outside of array of length {}",
                .{ array_info.len, array_info.len },
            );
        }

        // Parse the elements and return the array
        var result: T = undefined;
        for (&result, 0..) |*elem, i| {
            // If we fail to parse this field, free all fields before it
            errdefer if (self.options.free_on_error) {
                for (result[0..i]) |item| {
                    free(self.gpa, item);
                }
            };

            elem.* = try self.parseExpr(array_info.child, nodes.at(@intCast(i)));
        }
        if (array_info.sentinel()) |s| result[result.len] = s;
        return result;
    }

    fn parseStruct(self: *@This(), T: type, node: Zoir.Node.Index) !T {
        const repr = node.get(self.zoir);
        const fields: @FieldType(Zoir.Node, "struct_literal") = switch (repr) {
            .struct_literal => |nodes| nodes,
            .empty_literal => .{ .names = &.{}, .vals = .{ .start = node, .len = 0 } },
            else => return error.WrongType,
        };

        const field_infos = @typeInfo(T).@"struct".fields;

        // Build a map from field name to index.
        // The special value `comptime_field` indicates that this is actually a comptime field.
        const comptime_field = std.math.maxInt(usize);
        const field_indices: std.StaticStringMap(usize) = comptime b: {
            var kvs_list: [field_infos.len]struct { []const u8, usize } = undefined;
            for (&kvs_list, field_infos, 0..) |*kv, field, i| {
                kv.* = .{ field.name, if (field.is_comptime) comptime_field else i };
            }
            break :b .initComptime(kvs_list);
        };

        // Parse the struct
        var result: T = undefined;
        var field_found: [field_infos.len]bool = @splat(false);

        // If we fail partway through, free all already initialized fields
        var initialized: usize = 0;
        errdefer if (self.options.free_on_error and field_infos.len > 0) {
            for (fields.names[0..initialized]) |name_runtime| {
                switch (field_indices.get(name_runtime.get(self.zoir)) orelse continue) {
                    inline 0...(field_infos.len - 1) => |name_index| {
                        const name = field_infos[name_index].name;
                        free(self.gpa, @field(result, name));
   
```
