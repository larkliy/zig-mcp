```
id.hasTypeBody() or !has_type_body);
    assert(id.hasSpecialBodies() or !has_special_body);
    assert(id.hasValueBody() == has_value_body);
    assert(id.linkage() == args.linkage);
    assert(id.hasName() == has_name);
    assert(id.hasLibName() or !has_lib_name);
    assert(id.isPub() == args.is_pub);
    assert(id.isThreadlocal() == args.is_threadlocal);

    const type_len = astgen.countBodyLenAfterFixups(type_body);
    const align_len = astgen.countBodyLenAfterFixups(align_body);
    const linksection_len = astgen.countBodyLenAfterFixups(linksection_body);
    const addrspace_len = astgen.countBodyLenAfterFixups(addrspace_body);
    const value_len = astgen.countBodyLenAfterFixups(value_body);

    const src_hash_arr: [4]u32 = @bitCast(args.src_hash);
    const flags: Zir.Inst.Declaration.Flags = .{
        .src_line = @intCast(args.src_line),
        .src_column = @intCast(args.src_column),
        .id = id,
    };
    const flags_arr: [2]u32 = @bitCast(flags);

    const need_extra: usize =
        @typeInfo(Zir.Inst.Declaration).@"struct".fields.len +
        @as(usize, @intFromBool(id.hasName())) +
        @as(usize, @intFromBool(id.hasLibName())) +
        @as(usize, @intFromBool(id.hasTypeBody())) +
        3 * @as(usize, @intFromBool(id.hasSpecialBodies())) +
        @as(usize, @intFromBool(id.hasValueBody())) +
        type_len + align_len + linksection_len + addrspace_len + value_len;

    try astgen.extra.ensureUnusedCapacity(gpa, need_extra);

    const extra: Zir.Inst.Declaration = .{
        .src_hash_0 = src_hash_arr[0],
        .src_hash_1 = src_hash_arr[1],
        .src_hash_2 = src_hash_arr[2],
        .src_hash_3 = src_hash_arr[3],
        .flags_0 = flags_arr[0],
        .flags_1 = flags_arr[1],
    };
    astgen.instructions.items(.data)[@intFromEnum(decl_inst)].declaration.payload_index =
        astgen.addExtraAssumeCapacity(extra);

    if (id.hasName()) {
        astgen.extra.appendAssumeCapacity(@intFromEnum(args.name));
    }
    if (id.hasLibName()) {
        astgen.extra.appendAssumeCapacity(@intFromEnum(args.lib_name));
    }
    if (id.hasTypeBody()) {
        astgen.extra.appendAssumeCapacity(type_len);
    }
    if (id.hasSpecialBodies()) {
        astgen.extra.appendSliceAssumeCapacity(&.{
            align_len,
            linksection_len,
            addrspace_len,
        });
    }
    if (id.hasValueBody()) {
        astgen.extra.appendAssumeCapacity(value_len);
    }

    astgen.appendBodyWithFixups(type_body);
    astgen.appendBodyWithFixups(align_body);
    astgen.appendBodyWithFixups(linksection_body);
    astgen.appendBodyWithFixups(addrspace_body);
    astgen.appendBodyWithFixups(value_body);

    args.value_gz.unstack();
    args.addrspace_gz.unstack();
    args.linksection_gz.unstack();
    args.align_gz.unstack();
    args.type_gz.unstack();
}

/// Given a list of instructions, returns a list of all instructions which are a `ref` of one of the originals,
/// from `astgen.ref_table`, non-recursively. The entries are removed from `astgen.ref_table`, and the returned
/// slice can then be treated as its own body, to append `ref` instructions to a body other than the one they
/// would normally exist in.
///
/// This is used when lowering functions. Very rarely, the callconv expression, align expression, etc may reference
/// function parameters via `&param`; in this case, we need to lower to a `ref` instruction in the callconv/align/etc
/// body, rather than in the declaration body. However, we don't append these bodies to `extra` until we've evaluated
/// *all* of the bodies into a big `GenZir` stack. Therefore, we use this function to pull out these per-body `ref`
/// instructions which must be emitted.
fn fetchRemoveRefEntries(astgen: *AstGen, param_insts: []const Zir.Inst.Index) ![]Zir.Inst.Index {
    var refs: std.ArrayList(Zir.Inst.Index) = .empty;
    for (param_insts) |param_inst| {
        if (astgen.ref_table.fetchRemove(param_inst)) |kv| {
            try refs.append(astgen.arena, kv.value);
        }
    }
    return refs.items;
}

test {
    _ = &generate;
}



---
File: /std/zig/AstRlAnnotate.zig
---

//! AstRlAnnotate is a simple pass which runs over the AST before AstGen to
//! determine which expressions require result locations.
//!
//! In some cases, AstGen can choose whether to provide a result pointer or to
//! just use standard `break` instructions from a block. The latter choice can
//! result in more efficient ZIR and runtime code, but does not allow for RLS to
//! occur. Thus, we want to provide a real result pointer (from an alloc) only
//! when necessary.
//!
//! To achieve this, we need to determine which expressions require a result
//! pointer. This pass is responsible for analyzing all syntax forms which may
//! provide a result location and, if sub-expressions consume this result
//! pointer non-trivially (e.g. writing through field pointers), marking the
//! node as requiring a result location.

const std = @import("std");
const AstRlAnnotate = @This();
const Ast = std.zig.Ast;
const Allocator = std.mem.Allocator;
const AutoHashMapUnmanaged = std.AutoHashMapUnmanaged;
const BuiltinFn = std.zig.BuiltinFn;
const assert = std.debug.assert;

gpa: Allocator,
arena: Allocator,
tree: *const Ast,

/// Certain nodes are placed in this set under the following conditions:
/// * if-else: either branch consumes the result location
/// * labeled block: any break consumes the result location
/// * switch: any prong consumes the result location
/// * orelse/catch: the RHS expression consumes the result location
/// * while/for: any break consumes the result location
/// * @as: the second operand consumes the result location
/// * const: the init expression consumes the result location
/// * return: the return expression consumes the result location
nodes_need_rl: RlNeededSet = .{},

pub const RlNeededSet = AutoHashMapUnmanaged(Ast.Node.Index, void);

const ResultInfo = packed struct {
    /// Do we have a known result type?
    have_type: bool,
    /// Do we (potentially) have a result pointer? Note that this pointer's type
    /// may not be known due to it being an inferred alloc.
    have_ptr: bool,

    const none: ResultInfo = .{ .have_type = false, .have_ptr = false };
    const typed_ptr: ResultInfo = .{ .have_type = true, .have_ptr = true };
    const inferred_ptr: ResultInfo = .{ .have_type = false, .have_ptr = true };
    const type_only: ResultInfo = .{ .have_type = true, .have_ptr = false };
};

/// A labeled block or a loop. When this block is broken from, `consumes_res_ptr`
/// should be set if the break expression consumed the result pointer.
const Block = struct {
    parent: ?*Block,
    label: ?[]const u8,
    is_loop: bool,
    ri: ResultInfo,
    consumes_res_ptr: bool,
};

pub fn annotate(gpa: Allocator, arena: Allocator, tree: Ast) Allocator.Error!RlNeededSet {
    var astrl: AstRlAnnotate = .{
        .gpa = gpa,
        .arena = arena,
        .tree = &tree,
    };
    defer astrl.deinit(gpa);

    if (tree.errors.len != 0) {
        // We can't perform analysis on a broken AST. AstGen will not run in
        // this case.
        return .{};
    }

    for (tree.containerDeclRoot().ast.members) |member_node| {
        _ = try astrl.expr(member_node, null, ResultInfo.none);
    }

    return astrl.nodes_need_rl.move();
}

fn deinit(astrl: *AstRlAnnotate, gpa: Allocator) void {
    astrl.nodes_need_rl.deinit(gpa);
}

fn containerDecl(
    astrl: *AstRlAnnotate,
    block: ?*Block,
    full: Ast.full.ContainerDecl,
) !void {
    const tree = astrl.tree;
    switch (tree.tokenTag(full.ast.main_token)) {
        .keyword_struct => {
            if (full.ast.arg.unwrap()) |arg| {
                _ = try astrl.expr(arg, block, ResultInfo.type_only);
            }
            for (full.ast.members) |member_node| {
                _ = try astrl.expr(member_node, block, ResultInfo.none);
            }
        },
        .keyword_union => {
            if (full.ast.arg.unwrap()) |arg| {
                _ = try astrl.expr(arg, block, ResultInfo.type_only);
            }
            for (full.ast.members) |member_node| {
                _ = try astrl.expr(member_node, block, ResultInfo.none);
            }
        },
        .keyword_enum => {
            if (full.ast.arg.unwrap()) |arg| {
                _ = try astrl.expr(arg, block, ResultInfo.type_only);
            }
            for (full.ast.members) |member_node| {
                _ = try astrl.expr(member_node, block, ResultInfo.none);
            }
        },
        .keyword_opaque => {
            for (full.ast.members) |member_node| {
                _ = try astrl.expr(member_node, block, ResultInfo.none);
            }
        },
        else => unreachable,
    }
}

/// Returns true if `rl` provides a result pointer and the expression consumes it.
fn expr(astrl: *AstRlAnnotate, node: Ast.Node.Index, block: ?*Block, ri: ResultInfo) Allocator.Error!bool {
    const tree = astrl.tree;
    switch (tree.nodeTag(node)) {
        .root,
        .switch_case_one,
        .switch_case_inline_one,
        .switch_case,
        .switch_case_inline,
        .switch_range,
        .for_range,
        .asm_output,
        .asm_input,
        => unreachable,

        .@"errdefer" => {
            _ = try astrl.expr(tree.nodeData(node).opt_token_and_node[1], block, ResultInfo.none);
            return false;
        },
        .@"defer" => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },

        .container_field_init,
        .container_field_align,
        .container_field,
        => {
            const full = tree.fullContainerField(node).?;
            const type_expr = full.ast.type_expr.unwrap().?;
            _ = try astrl.expr(type_expr, block, ResultInfo.type_only);
            if (full.ast.align_expr.unwrap()) |align_expr| {
                _ = try astrl.expr(align_expr, block, ResultInfo.type_only);
            }
            if (full.ast.value_expr.unwrap()) |value_expr| {
                _ = try astrl.expr(value_expr, block, ResultInfo.type_only);
            }
            return false;
        },
        .test_decl => {
            _ = try astrl.expr(tree.nodeData(node).opt_token_and_node[1], block, ResultInfo.none);
            return false;
        },
        .global_var_decl,
        .local_var_decl,
        .simple_var_decl,
        .aligned_var_decl,
        => {
            const full = tree.fullVarDecl(node).?;
            const init_ri = if (full.ast.type_node.unwrap()) |type_node| init_ri: {
                _ = try astrl.expr(type_node, block, ResultInfo.type_only);
                break :init_ri ResultInfo.typed_ptr;
            } else ResultInfo.inferred_ptr;
            const init_node = full.ast.init_node.unwrap() orelse {
                // No init node, so we're done.
                return false;
            };
            switch (tree.tokenTag(full.ast.mut_token)) {
                .keyword_const => {
                    const init_consumes_rl = try astrl.expr(init_node, block, init_ri);
                    if (init_consumes_rl) {
                        try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
                    }
                    return false;
                },
                .keyword_var => {
                    // We'll create an alloc either way, so don't care if the
                    // result pointer is consumed.
                    _ = try astrl.expr(init_node, block, init_ri);
                    return false;
                },
                else => unreachable,
            }
        },
        .assign_destructure => {
            const full = tree.assignDestructure(node);
            for (full.ast.variables) |variable_node| {
                _ = try astrl.expr(variable_node, block, ResultInfo.none);
            }
            // We don't need to gather any meaningful data here, because destructures always use RLS
            _ = try astrl.expr(full.ast.value_expr, block, ResultInfo.none);
            return false;
        },
        .assign => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.typed_ptr);
            return false;
        },
        .assign_shl,
        .assign_shl_sat,
        .assign_shr,
        .assign_bit_and,
        .assign_bit_or,
        .assign_bit_xor,
        .assign_div,
        .assign_sub,
        .assign_sub_wrap,
        .assign_sub_sat,
        .assign_mod,
        .assign_add,
        .assign_add_wrap,
        .assign_add_sat,
        .assign_mul,
        .assign_mul_wrap,
        .assign_mul_sat,
        => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.none);
            return false;
        },
        .shl, .shr => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.type_only);
            return false;
        },
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
        => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.none);
            return false;
        },

        .array_mult => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.type_only);
            return false;
        },
        .error_union, .merge_error_sets => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.none);
            return false;
        },
        .bool_and,
        .bool_or,
        => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.type_only);
            _ = try astrl.expr(rhs, block, ResultInfo.type_only);
            return false;
        },
        .bool_not => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.type_only);
            return false;
        },
        .bit_not, .negation, .negation_wrap => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },

        // These nodes are leaves and never consume a result location.
        .identifier,
        .string_literal,
        .multiline_string_literal,
        .number_literal,
        .unreachable_literal,
        .asm_simple,
        .@"asm",
        .enum_literal,
        .error_value,
        .anyframe_literal,
        .@"continue",
        .char_literal,
        .error_set_decl,
        => return false,

        .builtin_call_two,
        .builtin_call_two_comma,
        .builtin_call,
        .builtin_call_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const params = tree.builtinCallParams(&buf, node).?;
            return astrl.builtinCall(block, ri, node, params);
        },

        .call_one,
        .call_one_comma,
        .call,
        .call_comma,
        => {
            var buf: [1]Ast.Node.Index = undefined;
            const full = tree.fullCall(&buf, node).?;
            _ = try astrl.expr(full.ast.fn_expr, block, ResultInfo.none);
            for (full.ast.params) |param_node| {
                _ = try astrl.expr(param_node, block, ResultInfo.type_only);
            }
            return false; // TODO: once function calls are passed result locations this will change
        },

        .@"return" => {
            if (tree.nodeData(node).opt_node.unwrap()) |lhs| {
                const ret_val_consumes_rl = try astrl.expr(lhs, block, ResultInfo.typed_ptr);
                if (ret_val_consumes_rl) {
                    try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
                }
            }
            return false;
        },

        .field_access => {
            const lhs, _ = tree.nodeData(node).node_and_token;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            return false;
        },

        .if_simple, .@"if" => {
            const full = tree.fullIf(node).?;
            if (full.error_token != null or full.payload_token != null) {
                _ = try astrl.expr(full.ast.cond_expr, block, ResultInfo.none);
            } else {
                _ = try astrl.expr(full.ast.cond_expr, block, ResultInfo.type_only); // bool
            }

            if (full.ast.else_expr.unwrap()) |else_expr| {
                const then_uses_rl = try astrl.expr(full.ast.then_expr, block, ri);
                const else_uses_rl = try astrl.expr(else_expr, block, ri);
                const uses_rl = then_uses_rl or else_uses_rl;
                if (uses_rl) try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
                return uses_rl;
            } else {
                _ = try astrl.expr(full.ast.then_expr, block, ResultInfo.none);
                return false;
            }
        },

        .while_simple, .while_cont, .@"while" => {
            const full = tree.fullWhile(node).?;
            const label: ?[]const u8 = if (full.label_token) |label_token| label: {
                break :label try astrl.identString(label_token);
            } else null;
            if (full.error_token != null or full.payload_token != null) {
                _ = try astrl.expr(full.ast.cond_expr, block, ResultInfo.none);
            } else {
                _ = try astrl.expr(full.ast.cond_expr, block, ResultInfo.type_only); // bool
            }
            var new_block: Block = .{
                .parent = block,
                .label = label,
                .is_loop = true,
                .ri = ri,
                .consumes_res_ptr = false,
            };
            if (full.ast.cont_expr.unwrap()) |cont_expr| {
                _ = try astrl.expr(cont_expr, &new_block, ResultInfo.none);
            }
            _ = try astrl.expr(full.ast.then_expr, &new_block, ResultInfo.none);
            const else_consumes_rl = if (full.ast.else_expr.unwrap()) |else_expr| else_rl: {
                break :else_rl try astrl.expr(else_expr, block, ri);
            } else false;
            if (new_block.consumes_res_ptr or else_consumes_rl) {
                try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
                return true;
            } else {
                return false;
            }
        },

        .for_simple, .@"for" => {
            const full = tree.fullFor(node).?;
            const label: ?[]const u8 = if (full.label_token) |label_token| label: {
                break :label try astrl.identString(label_token);
            } else null;
            for (full.ast.inputs) |input| {
                if (tree.nodeTag(input) == .for_range) {
                    const lhs, const opt_rhs = tree.nodeData(input).node_and_opt_node;
                    _ = try astrl.expr(lhs, block, ResultInfo.type_only);
                    if (opt_rhs.unwrap()) |rhs| {
                        _ = try astrl.expr(rhs, block, ResultInfo.type_only);
                    }
                } else {
                    _ = try astrl.expr(input, block, ResultInfo.none);
                }
            }
            var new_block: Block = .{
                .parent = block,
                .label = label,
                .is_loop = true,
                .ri = ri,
                .consumes_res_ptr = false,
            };
            _ = try astrl.expr(full.ast.then_expr, &new_block, ResultInfo.none);
            const else_consumes_rl = if (full.ast.else_expr.unwrap()) |else_expr| else_rl: {
                break :else_rl try astrl.expr(else_expr, block, ri);
            } else false;
            if (new_block.consumes_res_ptr or else_consumes_rl) {
                try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
                return true;
            } else {
                return false;
            }
        },

        .slice_open => {
            const sliced, const start = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(sliced, block, ResultInfo.none);
            _ = try astrl.expr(start, block, ResultInfo.type_only);
            return false;
        },
        .slice => {
            const sliced, const extra_index = tree.nodeData(node).node_and_extra;
            const extra = tree.extraData(extra_index, Ast.Node.Slice);
            _ = try astrl.expr(sliced, block, ResultInfo.none);
            _ = try astrl.expr(extra.start, block, ResultInfo.type_only);
            _ = try astrl.expr(extra.end, block, ResultInfo.type_only);
            return false;
        },
        .slice_sentinel => {
            const sliced, const extra_index = tree.nodeData(node).node_and_extra;
            const extra = tree.extraData(extra_index, Ast.Node.SliceSentinel);
            _ = try astrl.expr(sliced, block, ResultInfo.none);
            _ = try astrl.expr(extra.start, block, ResultInfo.type_only);
            if (extra.end.unwrap()) |end| {
                _ = try astrl.expr(end, block, ResultInfo.type_only);
            }
            _ = try astrl.expr(extra.sentinel, block, ResultInfo.none);
            return false;
        },
        .deref => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },
        .address_of => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },
        .optional_type => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.type_only);
            return false;
        },
        .@"try",
        .@"nosuspend",
        => return astrl.expr(tree.nodeData(node).node, block, ri),
        .grouped_expression,
        .unwrap_optional,
        => return astrl.expr(tree.nodeData(node).node_and_token[0], block, ri),

        .block_two,
        .block_two_semicolon,
        .block,
        .block_semicolon,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const statements = tree.blockStatements(&buf, node).?;
            return astrl.blockExpr(block, ri, node, statements);
        },
        .anyframe_type => {
            _, const child_type = tree.nodeData(node).token_and_node;
            _ = try astrl.expr(child_type, block, ResultInfo.type_only);
            return false;
        },
        .@"catch", .@"orelse" => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            const rhs_consumes_rl = try astrl.expr(rhs, block, ri);
            if (rhs_consumes_rl) {
                try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
            }
            return rhs_consumes_rl;
        },

        .ptr_type_aligned,
        .ptr_type_sentinel,
        .ptr_type,
        .ptr_type_bit_range,
        => {
            const full = tree.fullPtrType(node).?;
            _ = try astrl.expr(full.ast.child_type, block, ResultInfo.type_only);
            if (full.ast.sentinel.unwrap()) |sentinel| {
                _ = try astrl.expr(sentinel, block, ResultInfo.type_only);
            }
            if (full.ast.addrspace_node.unwrap()) |addrspace_node| {
                _ = try astrl.expr(addrspace_node, block, ResultInfo.type_only);
            }
            if (full.ast.align_node.unwrap()) |align_node| {
                _ = try astrl.expr(align_node, block, ResultInfo.type_only);
            }
            if (full.ast.bit_range_start.unwrap()) |bit_range_start| {
                const bit_range_end = full.ast.bit_range_end.unwrap().?;
                _ = try astrl.expr(bit_range_start, block, ResultInfo.type_only);
                _ = try astrl.expr(bit_range_end, block, ResultInfo.type_only);
            }
            return false;
        },

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
        => {
            var buf: [2]Ast.Node.Index = undefined;
            try astrl.containerDecl(block, tree.fullContainerDecl(&buf, node).?);
            return false;
        },

        .@"break" => {
            const opt_label, const opt_rhs = tree.nodeData(node).opt_token_and_opt_node;
            const rhs = opt_rhs.unwrap() orelse {
                // Breaks with void are not interesting
                return false;
            };

            var opt_cur_block = block;
            if (opt_label.unwrap()) |label_token| {
                const break_label = try astrl.identString(label_token);
                while (opt_cur_block) |cur_block| : (opt_cur_block = cur_block.parent) {
                    const block_label = cur_block.label orelse continue;
                    if (std.mem.eql(u8, block_label, break_label)) break;
                }
            } else {
                // No label - we're breaking from a loop.
                while (opt_cur_block) |cur_block| : (opt_cur_block = cur_block.parent) {
                    if (cur_block.is_loop) break;
                }
            }

            if (opt_cur_block) |target_block| {
                const consumes_break_rl = try astrl.expr(rhs, block, target_block.ri);
                if (consumes_break_rl) target_block.consumes_res_ptr = true;
            } else {
                // No corresponding scope to break from - AstGen will emit an error.
                _ = try astrl.expr(rhs, block, ResultInfo.none);
            }

            return false;
        },

        .array_type => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.type_only);
            _ = try astrl.expr(rhs, block, ResultInfo.type_only);
            return false;
        },
        .array_type_sentinel => {
            const len_expr, const extra_index = tree.nodeData(node).node_and_extra;
            const extra = tree.extraData(extra_index, Ast.Node.ArrayTypeSentinel);
            _ = try astrl.expr(len_expr, block, ResultInfo.type_only);
            _ = try astrl.expr(extra.elem_type, block, ResultInfo.type_only);
            _ = try astrl.expr(extra.sentinel, block, ResultInfo.type_only);
            return false;
        },
        .array_access => {
            const lhs, const rhs = tree.nodeData(node).node_and_node;
            _ = try astrl.expr(lhs, block, ResultInfo.none);
            _ = try astrl.expr(rhs, block, ResultInfo.type_only);
            return false;
        },
        .@"comptime" => {
            // AstGen will emit an error if the scope is already comptime, so we can assume it is
            // not. This means the result location is not forwarded.
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },
        .@"switch", .switch_comma => {
            const operand_node, const extra_index = tree.nodeData(node).node_and_extra;
            const case_nodes = tree.extraDataSlice(tree.extraData(extra_index, Ast.Node.SubRange), Ast.Node.Index);

            _ = try astrl.expr(operand_node, block, ResultInfo.none);

            var any_prong_consumed_rl = false;
            for (case_nodes) |case_node| {
                const case = tree.fullSwitchCase(case_node).?;
                for (case.ast.values) |item_node| {
                    if (tree.nodeTag(item_node) == .switch_range) {
                        const lhs, const rhs = tree.nodeData(item_node).node_and_node;
                        _ = try astrl.expr(lhs, block, ResultInfo.none);
                        _ = try astrl.expr(rhs, block, ResultInfo.none);
                    } else {
                        _ = try astrl.expr(item_node, block, ResultInfo.none);
                    }
                }
                if (try astrl.expr(case.ast.target_expr, block, ri)) {
                    any_prong_consumed_rl = true;
                }
            }
            if (any_prong_consumed_rl) {
                try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
            }
            return any_prong_consumed_rl;
        },
        .@"suspend" => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },
        .@"resume" => {
            _ = try astrl.expr(tree.nodeData(node).node, block, ResultInfo.none);
            return false;
        },

        .array_init_one,
        .array_init_one_comma,
        .array_init_dot_two,
        .array_init_dot_two_comma,
        .array_init_dot,
        .array_init_dot_comma,
        .array_init,
        .array_init_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const full = tree.fullArrayInit(&buf, node).?;

            if (full.ast.type_expr.unwrap()) |type_expr| {
                // Explicitly typed init does not participate in RLS
                _ = try astrl.expr(type_expr, block, ResultInfo.none);
                for (full.ast.elements) |elem_init| {
                    _ = try astrl.expr(elem_init, block, ResultInfo.type_only);
                }
                return false;
            }

            if (ri.have_type) {
                // Always forward type information
                // If we have a result pointer, we use and forward it
                for (full.ast.elements) |elem_init| {
                    _ = try astrl.expr(elem_init, block, ri);
                }
                return ri.have_ptr;
            } else {
                // Untyped init does not consume result location
                for (full.ast.elements) |elem_init| {
                    _ = try astrl.expr(elem_init, block, ResultInfo.none);
                }
                return false;
            }
        },

        .struct_init_one,
        .struct_init_one_comma,
        .struct_init_dot_two,
        .struct_init_dot_two_comma,
        .struct_init_dot,
        .struct_init_dot_comma,
        .struct_init,
        .struct_init_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const full = tree.fullStructInit(&buf, node).?;

            if (full.ast.type_expr.unwrap()) |type_expr| {
                // Explicitly typed init does not participate in RLS
                _ = try astrl.expr(type_expr, block, ResultInfo.none);
                for (full.ast.fields) |field_init| {
                    _ = try astrl.expr(field_init, block, ResultInfo.type_only);
                }
                return false;
            }

            if (ri.have_type) {
                // Always forward type information
                // If we have a result pointer, we use and forward it
                for (full.ast.fields) |field_init| {
                    _ = try astrl.expr(field_init, block, ri);
                }
                return ri.have_ptr;
            } else {
                // Untyped init does not consume result location
                for (full.ast.fields) |field_init| {
                    _ = try astrl.expr(field_init, block, ResultInfo.none);
                }
                return false;
            }
        },

        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
        .fn_decl,
        => |tag| {
            var buf: [1]Ast.Node.Index = undefined;
            const full = tree.fullFnProto(&buf, node).?;
            const body_node = if (tag == .fn_decl) tree.nodeData(node).node_and_node[1].toOptional() else .none;
            {
                var it = full.iterate(tree);
                while (it.next()) |param| {
                    if (param.anytype_ellipsis3 == null) {
                        const type_expr = param.type_expr.?;
                        _ = try astrl.expr(type_expr, block, ResultInfo.type_only);
                    }
                }
            }
            if (full.ast.align_expr.unwrap()) |align_expr| {
                _ = try astrl.expr(align_expr, block, ResultInfo.type_only);
            }
            if (full.ast.addrspace_expr.unwrap()) |addrspace_expr| {
                _ = try astrl.expr(addrspace_expr, block, ResultInfo.type_only);
            }
            if (full.ast.section_expr.unwrap()) |section_expr| {
                _ = try astrl.expr(section_expr, block, ResultInfo.type_only);
            }
            if (full.ast.callconv_expr.unwrap()) |callconv_expr| {
                _ = try astrl.expr(callconv_expr, block, ResultInfo.type_only);
            }
            const return_type = full.ast.return_type.unwrap().?;
            _ = try astrl.expr(return_type, block, ResultInfo.type_only);
            if (body_node.unwrap()) |body| {
                _ = try astrl.expr(body, block, ResultInfo.none);
            }
            return false;
        },
    }
}

fn identString(astrl: *AstRlAnnotate, token: Ast.TokenIndex) ![]const u8 {
    const tree = astrl.tree;
    assert(tree.tokenTag(token) == .identifier);
    const ident_name = tree.tokenSlice(token);
    if (!std.mem.startsWith(u8, ident_name, "@")) {
        return ident_name;
    }
    return std.zig.string_literal.parseAlloc(astrl.arena, ident_name[1..]) catch |err| switch (err) {
        error.OutOfMemory => error.OutOfMemory,
        error.InvalidLiteral => "", // This pass can safely return garbage on invalid AST
    };
}

fn blockExpr(astrl: *AstRlAnnotate, parent_block: ?*Block, ri: ResultInfo, node: Ast.Node.Index, statements: []const Ast.Node.Index) !bool {
    const tree = astrl.tree;

    const lbrace = tree.nodeMainToken(node);
    if (tree.isTokenPrecededByTags(lbrace, &.{ .identifier, .colon })) {
        // Labeled block
        var new_block: Block = .{
            .parent = parent_block,
            .label = try astrl.identString(lbrace - 2),
            .is_loop = false,
            .ri = ri,
            .consumes_res_ptr = false,
        };
        for (statements) |statement| {
            _ = try astrl.expr(statement, &new_block, ResultInfo.none);
        }
        if (new_block.consumes_res_ptr) {
            try astrl.nodes_need_rl.putNoClobber(astrl.gpa, node, {});
        }
        return new_block.consumes_res_ptr;
    } else {
        // Unlabeled block
        for (statements) |statement| {
            _ = try astrl.expr(statement, parent_block, ResultInfo.none);
        }
        return false;
    }
}

fn builtinCall(astrl: *AstRlAnnotate, block: ?*Block, ri: ResultInfo, node: Ast.Node.Index, args: []const Ast.Node.Index) !bool {
    _ = ri; // Currently, no builtin consumes its result location.

    const tree = astrl.tree;
    const builtin_token = tree.nodeMainToken(node);
    const builtin_name = tree.tokenSlice(builtin_token);
    const info = BuiltinFn.list.get(builtin_name) orelse return false;
    if (info.param_count) |expected| {
        if (expected != args.len) return false;
    }
    switch (info.tag) {
        .import => return false,
        .branch_hint => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            return false;
        },
        .compile_log, .TypeOf => {
            for (args) |arg_node| {
                _ = try astrl.expr(arg_node, block, ResultInfo.none);
            }
            return false;
        },
        .as => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .bit_cast => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            return false;
        },
        .union_init => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            return false;
        },
        .c_import => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            return false;
        },
        .min, .max => {
            for (args) |arg_node| {
                _ = try astrl.expr(arg_node, block, ResultInfo.none);
            }
            return false;
        },
        .@"export" => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .@"extern" => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        // These builtins take no args and do not consume the result pointer.
        .src,
        .This,
        .EnumLiteral,
        .return_address,
        .error_return_trace,
        .frame,
        .breakpoint,
        .disable_instrumentation,
        .disable_intrinsics,
        .in_comptime,
        .panic,
        .trap,
        .c_va_start,
        => return false,
        // TODO: this is a workaround for llvm/llvm-project#68409
        // Zig tracking issue: #16876
        .frame_address => return true,
        // These builtins take a single argument with a known result type, but do not consume their
        // result pointer.
        .sqrt,
        .sin,
        .cos,
        .tan,
        .exp,
        .exp2,
        .log,
        .log2,
        .log10,
        .floor,
        .ceil,
        .trunc,
        .round,
        .size_of,
        .bit_size_of,
        .align_of,
        .compile_error,
        .set_eval_branch_quota,
        .int_from_bool,
        .int_from_error,
        .error_from_int,
        .embed_file,
        .error_name,
        .set_runtime_safety,
        .Tuple,
        .c_undef,
        .c_include,
        .wasm_memory_size,
        .splat,
        .set_float_mode,
        .type_info,
        .work_item_id,
        .work_group_size,
        .work_group_id,
        => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            return false;
        },
        // These builtins take a single argument with no result information and do not consume their
        // result pointer.
        .int_from_ptr,
        .int_from_enum,
        .abs,
        .tag_name,
        .type_name,
        .Frame,
        .int_from_float,
        .float_from_int,
        .ptr_from_int,
        .enum_from_int,
        .float_cast,
        .int_cast,
        .truncate,
        .error_cast,
        .ptr_cast,
        .align_cast,
        .addrspace_cast,
        .const_cast,
        .volatile_cast,
        .clz,
        .ctz,
        .pop_count,
        .byte_swap,
        .bit_reverse,
        => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            return false;
        },
        .div_exact,
        .div_floor,
        .div_trunc,
        .mod,
        .rem,
        => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            return false;
        },
        .shl_exact, .shr_exact => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .bit_offset_of,
        .offset_of,
        .has_decl,
        .has_field,
        .field,
        .FieldType,
        => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .field_parent_ptr => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            return false;
        },
        .wasm_memory_grow => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .c_define => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            return false;
        },
        .reduce => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            return false;
        },
        .add_with_overflow, .sub_with_overflow, .mul_with_overflow, .shl_with_overflow => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            return false;
        },
        .atomic_load => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            return false;
        },
        .atomic_rmw => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            _ = try astrl.expr(args[4], block, ResultInfo.type_only);
            return false;
        },
        .atomic_store => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            return false;
        },
        .mul_add => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            return false;
        },
        .call => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            _ = try astrl.expr(args[2], block, ResultInfo.none);
            return false;
        },
        .memcpy, .memmove => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            return false;
        },
        .memset => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .shuffle => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            _ = try astrl.expr(args[2], block, ResultInfo.none);
            _ = try astrl.expr(args[3], block, ResultInfo.none);
            return false;
        },
        .select => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.none);
            _ = try astrl.expr(args[2], block, ResultInfo.none);
            _ = try astrl.expr(args[3], block, ResultInfo.none);
            return false;
        },
        .Int => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .Pointer => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            return false;
        },
        .Fn => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            return false;
        },
        .Struct => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            _ = try astrl.expr(args[4], block, ResultInfo.type_only);
            return false;
        },
        .Union => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            _ = try astrl.expr(args[4], block, ResultInfo.type_only);
            return false;
        },
        .Enum => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            return false;
        },
        .Vector => {
            _ = try astrl.expr(args[0], block, ResultInfo.type_only);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .prefetch => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .c_va_arg => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            return false;
        },
        .c_va_copy => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            return false;
        },
        .c_va_end => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            return false;
        },
        .cmpxchg_strong, .cmpxchg_weak => {
            _ = try astrl.expr(args[0], block, ResultInfo.none);
            _ = try astrl.expr(args[1], block, ResultInfo.type_only);
            _ = try astrl.expr(args[2], block, ResultInfo.type_only);
            _ = try astrl.expr(args[3], block, ResultInfo.type_only);
            _ = try astrl.expr(args[4], block, ResultInfo.type_only);
            return false;
        },
    }
}



---
File: /std/zig/AstSmith.zig
---

//! Generates a valid AST and corresponding source.
//!
//! This is based directly off grammer.peg

const std = @import("../std.zig");
const assert = std.debug.assert;
const Token = std.zig.Token;
const Smith = std.testing.Smith;
const Weight = Smith.Weight;
const AstSmith = @This();

smith: *Smith,

source_buf: [16384]u8,
source_len: usize,

token_tag_buf: [2048]Token.Tag,
token_start_buf: [2048]std.zig.Ast.ByteOffset,
tokens_len: usize,

/// For `.asterisk`, this also includes `.asterisk2`
not_token: ?Token.Tag,
not_token_comptime: bool,
/// ExprSuffix
///     <- KEYWORD_or
///      / KEYWORD_and
///      / CompareOp
///      / BitwiseOp
///      / BitShiftOp
///      / AdditionOp
///      / MultiplyOp
///      / EXCLAMATIONMARK
///      / SuffixOp
///      / FnCallArguments
not_expr_suffix: bool,
/// LabelableExpr
///   <- Block
///    / SwitchExpr
///    / LoopExpr
not_labelable_expr: ?enum { colon, expr },
not_label: bool,
not_break_label: bool,
not_block_expr: bool,
not_expr_statement: bool,

prev_ids_buf: [256]struct { start: u16, len: u16 },
/// This may be larger than `prev_ids` in which case,
///   x % prev_ids.len = next index
///   @min(x, prev_ids) = length
prev_ids_len: usize,

/// `generate` must be called on the returned value before any other methods
pub fn init(smith: *Smith) AstSmith {
    return .{
        .smith = smith,

        .source_buf = undefined,
        .source_len = 0,

        .token_tag_buf = undefined,
        .token_start_buf = undefined,
        .tokens_len = 0,

        .not_token = null,
        .not_token_comptime = false,
        .not_expr_suffix = false,
        .not_labelable_expr = null,
        .not_label = false,
        .not_break_label = false,
        .not_block_expr = false,
        .not_expr_statement = false,

        .prev_ids_buf = undefined,
        .prev_ids_len = 0,
    };
}

pub fn source(t: *AstSmith) [:0]u8 {
    return t.source_buf[0..t.source_len :0];
}

/// The Slice is not backed by a MultiArrayList, so calling deinit or toMultiArrayList is illegal.
pub fn tokens(t: *AstSmith) std.zig.Ast.TokenList.Slice {
    var slice: std.zig.Ast.TokenList.Slice = .{
        .ptrs = undefined,
        .len = t.tokens_len,
        .capacity = t.tokens_len,
    };
    comptime assert(slice.ptrs.len == 2);
    slice.ptrs[@intFromEnum(std.zig.Ast.TokenList.Field.tag)] = @ptrCast(&t.token_tag_buf);
    slice.ptrs[@intFromEnum(std.zig.Ast.TokenList.Field.start)] = @ptrCast(&t.token_start_buf);
    return slice;
}

pub const Error = error{ OutOfMemory, SkipZigTest };
const SourceError = error{SkipZigTest};

pub fn generate(a: *AstSmith, gpa: std.mem.Allocator) Error!std.zig.Ast {
    try a.generateSource();
    const ast = try std.zig.Ast.parseTokens(gpa, a.source(), a.tokens(), .zig);
    assert(ast.errors.len == 0);
    return ast;
}

pub fn generateSource(a: *AstSmith) SourceError!void {
    try a.pegRoot();
    try a.ensureSourceCapacity(1);
    a.source_buf[a.source_len] = 0;
    try a.addTokenTag(.eof);
}

/// For choices which can introduce a variable number of expressions, this should be used to reduce
/// unbounded recursion.
//
// `inline` to propogate caller's return address
inline fn smithListItemBool(a: *AstSmith) bool {
    return a.smith.boolWeighted(63, 1);
}

/// For choices which can introduce a variable number of expressions, this should be used to reduce
/// unbounded recursion.
//
// `inline` to propogate caller's return address
inline fn smithListItemEos(a: *AstSmith) bool {
    return a.smith.eosWeightedSimple(1, 63);
}

fn sourceCapacity(a: *AstSmith) []u8 {
    return a.source_buf[a.source_len..];
}

fn sourceCapacityLen(a: *AstSmith) usize {
    return a.source_buf.len - a.source_len;
}

fn ensureSourceCapacity(a: *AstSmith, n: usize) SourceError!void {
    if (a.sourceCapacityLen() < n) return error.SkipZigTest;
}

fn addSourceByte(a: *AstSmith, byte: u8) SourceError!void {
    try a.ensureSourceCapacity(1);
    a.addSourceByteAssumeCapacity(byte);
}

fn addSourceByteAssumeCapacity(a: *AstSmith, byte: u8) void {
    a.sourceCapacity()[0] = byte;
    a.source_len += 1;
}

fn addSource(a: *AstSmith, bytes: []const u8) SourceError!void {
    try a.ensureSourceCapacity(bytes.len);
    a.addSourceAssumeCapacity(bytes);
}

fn addSourceAssumeCapacity(a: *AstSmith, bytes: []const u8) void {
    @memcpy(a.sourceCapacity()[0..bytes.len], bytes);
    a.source_len += bytes.len;
}

fn addSourceAsSlice(a: *AstSmith, len: usize) SourceError![]u8 {
    try a.ensureSourceCapacity(len);
    return a.addSourceAsSliceAssumeCapacity(len);
}

fn addSourceAsSliceAssumeCapacity(a: *AstSmith, len: usize) []u8 {
    const slice = a.sourceCapacity()[0..len];
    a.source_len += len;
    return slice;
}

fn tokenCapacityLen(a: *AstSmith) usize {
    return a.token_tag_buf.len - a.tokens_len;
}

fn ensureTokenCapacity(a: *AstSmith, n: usize) SourceError!void {
    if (a.tokenCapacityLen() < n) return error.SkipZigTest;
}

fn isAlphanumeric(c: u8) bool {
    return switch (c) {
        '_', 'a'...'z', 'A'...'Z', '0'...'9' => true,
        else => false,
    };
}

/// For tokens starting with alphanumerics, this ensures
/// previous tokens followed by end_of_word aren't altered.
///
/// end_of_word <- ![a-zA-Z0-9_] skip
fn preservePegEndOfWord(a: *AstSmith) SourceError!void {
    if (a.source_len > 0 and isAlphanumeric(a.source_buf[a.source_len - 1])) {
        try a.addSourceByte(' ');
    }
}

/// Assumes the token has not been written yet
fn addTokenTag(a: *AstSmith, tag: Token.Tag) SourceError!void {
    assert(tag != a.not_token);
    if (a.not_token == .asterisk) assert(tag != .asterisk_asterisk);
    a.not_token = null;

    if (a.not_token_comptime) assert(tag != .keyword_comptime);
    a.not_token_comptime = false;

    if (a.not_label and tag == .identifier) {
        a.not_token = .colon;
    }
    a.not_label = false;

    if (a.not_break_label and tag == .colon) {
        a.not_token = .identifier;
    }
    a.not_break_label = false;

    if (a.not_labelable_expr) |part| switch (part) {
        .colon => a.not_labelable_expr = if (tag == .colon) .expr else null,
        .expr => switch (tag) {
            .l_brace => unreachable,
            .keyword_inline => {},
            .keyword_for => unreachable,
            .keyword_while => unreachable,
            .keyword_switch => unreachable,
            else => a.not_labelable_expr = null,
        },
    };

    a.not_expr_suffix = false;
    a.not_block_expr = false;
    a.not_expr_statement = false;

    try a.ensureTokenCapacity(1);
    a.token_tag_buf[a.tokens_len] = tag;
    a.token_start_buf[a.tokens_len] = @intCast(a.source_len);
    a.tokens_len += 1;
}

/// Asserts the token has a lexeme (those without have corresponding methods)
fn pegToken(a: *AstSmith, tag: Token.Tag) SourceError!void {
    const lexeme = tag.lexeme().?;

    switch (lexeme[0]) {
        '_', 'a'...'z', 'A'...'Z', '0'...'9' => try a.preservePegEndOfWord(),
        '*' => if (a.tokens_len > 0 and a.source_buf[a.source_len - 1] == '*' and
            a.token_tag_buf[a.tokens_len - 1] != .asterisk_asterisk)
        {
            try a.addSourceByte(' ');
        },
        '.' => if (a.tokens_len > 0 and switch (a.source_buf[a.source_len - 1]) {
            '.' => true,
            '0'...'9', 'a'...'z', 'A'...'Z' => a.token_tag_buf[a.tokens_len - 1] == .number_literal,
            else => false,
        }) {
            try a.addSourceByte(' ');
        },
        '+', '-' => if (a.tokens_len > 0 and a.token_tag_buf[a.tokens_len - 1] == .number_literal and
            switch (a.source_buf[a.source_len - 1]) {
                'e', 'E', 'p', 'P' => true,
                else => false,
            })
        {
            // Would otherwise be tokenized as the sign of a float's exponent
            //
            // e.g. "0xFE" ++ "+" ++ "2" (number_literal, plus, number_literal)
            try a.addSourceByte(' ');
        },
        else => {},
    }

    if (isAlphanumeric(lexeme[0])) try a.preservePegEndOfWord();

    try a.addTokenTag(tag);
    try a.addSource(lexeme);
    try a.pegSkip();
}

/// Asserts `a.source_len != 0`
fn pegTokenWhitespaceAround(a: *AstSmith, tag: Token.Tag) SourceError!void {
    switch (a.source_buf[a.source_len - 1]) {
        ' ', '\n' => {},
        else => try a.addSourceByte(' '),
    }
    try a.addTokenTag(tag);
    try a.addSource(tag.lexeme().?);
    switch (a.smith.value(enum { space, line_break, cr_line_break })) {
        // This is not the same as 'skip' since comments are not whitespace
        .space => try a.addSourceByte(' '),
        .line_break => try a.addSourceByte('\n'),
        .cr_line_break => try a.addSource("\r\n"),
    }
    try a.pegSkip();
}

/// Root <- skip ContainerMembers eof
fn pegRoot(a: *AstSmith) SourceError!void {
    try a.pegSkip();
    try a.pegContainerMembers();
}

/// ContainerMembers <- container_doc_comment? ContainerDeclaration* (ContainerField COMMA)*
///                     (ContainerField / ContainerDeclaration*)
fn pegContainerMembers(a: *AstSmith) SourceError!void {
    if (a.smith.boolWeighted(63, 1)) {
        try a.pegContainerDocComment();
    }
    while (!a.smithListItemEos()) {
        try a.pegContainerDeclaration();
    }
    while (!a.smithListItemEos()) {
        try a.pegContainerField();
        try a.pegToken(.comma);
    }
    if (a.smithListItemBool()) {
        if (a.smith.value(bool)) {
            try a.pegContainerField();
        } else while (true) {
            try a.pegContainerDeclaration();
            if (a.smithListItemEos()) break;
        }
    }
}

/// ContainerDeclaration <- TestDecl / ComptimeDecl / doc_comment? KEYWORD_pub? Decl
fn pegContainerDeclaration(a: *AstSmith) SourceError!void {
    switch (a.smith.value(enum { TestDecl, ComptimeDecl, Decl })) {
        .TestDecl => try a.pegTestDecl(),
        .ComptimeDecl => try a.pegComptimeDecl(),
        .Decl => {
            try a.pegMaybeDocComment();
            if (a.smith.value(bool)) {
                try a.pegToken(.keyword_pub);
            }
            try a.pegDecl();
        },
    }
}

/// KEYWORD_test (STRINGLITERALSINGLE / IDENTIFIER)? Block
fn pegTestDecl(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_test);
    switch (a.smith.value(enum { none, string, id })) {
        .none => {},
        .string => try a.pegStringLiteralSingle(),
        .id => try a.pegIdentifier(),
    }
    try a.pegBlock();
}

/// ComptimeDecl <- KEYWORD_comptime Block
fn pegComptimeDecl(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_comptime);
    try a.pegBlock();
}

/// Decl
///    <- (KEYWORD_export / KEYWORD_inline / KEYWORD_noinline)? FnProto (SEMICOLON / Block)
///     / KEYWORD_extern STRINGLITERALSINGLE? FnProto SEMICOLON
///     / (KEYWORD_export / KEYWORD_extern STRINGLITERALSINGLE?)? KEYWORD_threadlocal?
///     GlobalVarDecl
fn pegDecl(a: *AstSmith) SourceError!void {
    const Modifier = enum(u8) {
        none,
        @"export",
        @"extern",
        extern_library,
        @"inline",
        @"noinline",
    };
    const is_fn = a.smith.value(bool);
    const fn_modifiers = Smith.baselineWeights(Modifier);
    const var_modifiers: []const Weight = &.{.rangeAtMost(Modifier, .none, .extern_library, 1)};
    const modifier = a.smith.valueWeighted(Modifier, if (is_fn) fn_modifiers else var_modifiers);

    switch (modifier) {
        .none => {},
        .@"export" => try a.pegToken(.keyword_export),
        .@"extern" => try a.pegToken(.keyword_extern),
        .extern_library => {
            try a.pegToken(.keyword_extern);
            try a.pegStringLiteralSingle();
        },
        .@"inline" => try a.pegToken(.keyword_inline),
        .@"noinline" => try a.pegToken(.keyword_noinline),
    }

    if (is_fn) {
        try a.pegFnProto();
        if (modifier == .@"extern" or modifier == .extern_library or a.smith.value(bool)) {
            try a.pegToken(.semicolon);
        } else {
            try a.pegBlock();
        }
    } else {
        if (a.smith.value(bool)) try a.pegToken(.keyword_threadlocal);
        try a.pegGlobalVarDecl();
    }
}

/// FnProto <- KEYWORD_fn IDENTIFIER? LPAREN ParamDeclList RPAREN ByteAlign? AddrSpace?
///          LinkSection? CallConv? EXCLAMATIONMARK? TypeExpr !ExprSuffix
fn pegFnProto(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_fn);
    if (a.smith.value(bool)) {
        try a.pegIdentifier();
    }
    try a.pegToken(.l_paren);
    try a.pegParamDeclList();
    try a.pegToken(.r_paren);
    if (a.smith.value(bool)) {
        try a.pegByteAlign();
    }
    if (a.smith.value(bool)) {
        try a.pegAddrSpace();
    }
    if (a.smith.value(bool)) {
        try a.pegLinkSection();
    }
    if (a.smith.value(bool)) {
        try a.pegCallConv();
    }
    if (a.smith.value(bool)) {
        try a.pegToken(.bang);
    }
    try a.pegTypeExpr();
    a.not_expr_suffix = true;
}

/// VarDeclProto <- (KEYWORD_const / KEYWORD_var) IDENTIFIER (COLON TypeExpr)? ByteAlign?
///               AddrSpace? LinkSection?
fn pegVarDeclProto(a: *AstSmith) SourceError!void {
    try a.pegToken(if (a.smith.value(bool)) .keyword_var else .keyword_const);
    try a.pegIdentifier();

    if (a.smith.value(bool)) {
        try a.pegToken(.colon);
        try a.pegTypeExpr();
    }

    if (a.smith.value(bool)) {
        try a.pegByteAlign();
    }

    if (a.smith.value(bool)) {
        try a.pegAddrSpace();
    }

    if (a.smith.value(bool)) {
        try a.pegLinkSection();
    }
}

/// GlobalVarDecl <- VarDeclProto (EQUAL Expr)? SEMICOLON
fn pegGlobalVarDecl(a: *AstSmith) SourceError!void {
    try a.pegVarDeclProto();
    if (a.smithListItemBool()) {
        try a.pegToken(.equal);
        try a.pegExpr();
    }
    try a.pegToken(.semicolon);
}

/// ContainerField <- doc_comment? (KEYWORD_comptime / !KEYWORD_comptime) !KEYWORD_fn
///                 (IDENTIFIER COLON !(IDENTIFIER COLON)) TypeExpr ByteAlign? (EQUAL Expr)?
fn pegContainerField(a: *AstSmith) SourceError!void {
    try a.pegMaybeDocComment();
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_comptime);
    }
    if (a.smith.value(bool)) {
        try a.pegIdentifier();
        try a.pegToken(.colon);
    } else {
        a.not_token = .keyword_fn;
        a.not_token_comptime = true;
        a.not_label = true;
    }
    try a.pegTypeExpr();
    if (a.smith.value(bool)) {
        try a.pegByteAlign();
    }
    if (a.smith.value(bool)) {
        try a.pegToken(.equal);
        try a.pegExpr();
    }
}

/// BlockStatement
///     <- Statement
///      / KEYWORD_defer BlockExprStatement
///      / KEYWORD_errdefer Payload? BlockExprStatement
///      / !ExprStatement (KEYWORD_comptime !BlockExpr)? VarAssignStatement
fn pegBlockStatement(a: *AstSmith) SourceError!void {
    const Kind = enum {
        statement,
        defer_statement,
        errdefer_statement,
        var_assign,
        comptime_var_assign,
    };
    const weights = Smith.baselineWeights(Kind) ++ &[1]Weight{.value(Kind, .statement, 4)};
    switch (a.smith.valueWeighted(Kind, weights)) {
        .statement => try a.pegStatement(),
        .defer_statement, .errdefer_statement => |kind| {
            try a.pegToken(switch (kind) {
                .defer_statement => .keyword_defer,
                .errdefer_statement => .keyword_errdefer,
                else => unreachable,
            });
            try a.pegBlockExprStatement();
        },
        .var_assign, .comptime_var_assign => |kind| {
            a.not_expr_statement = true;
            if (kind == .comptime_var_assign) {
                try a.pegToken(.keyword_comptime);
                a.not_block_expr = true;
            }
            try a.pegVarAssignStatement();
        },
    }
}

/// Statement
///     <- ExprStatement
///      / KEYWORD_suspend BlockExprStatement
///      / !ExprStatement (KEYWORD_comptime !BlockExpr)? AssignExpr SEMICOLON
///
/// ExprStatement
///     <- IfStatement
///      / LabeledStatement
///      / KEYWORD_nosuspend BlockExprStatement
///      / KEYWORD_comptime BlockExpr
fn pegStatement(a: *AstSmith) SourceError!void {
    switch (a.smith.value(enum {
        if_statement,
        labeled_statement,
        comptime_block_expr,

        nosuspend_statement,
        suspend_statement,
        assign_expr,
        comptime_assign_expr,
    })) {
        .if_statement => try a.pegIfStatement(),
        .labeled_statement => try a.pegLabeledStatement(),
        .comptime_block_expr => {
            try a.pegToken(.keyword_comptime);
            try a.pegBlockExpr();
        },

        .nosuspend_statement,
        .suspend_statement,
        => |kind| {
            try a.pegToken(switch (kind) {
                .nosuspend_statement => .keyword_nosuspend,
                .suspend_statement => .keyword_suspend,
                else => unreachable,
            });
            try a.pegBlockExprStatement();
        },
        .assign_expr, .comptime_assign_expr => |kind| {
            a.not_expr_statement = true;
            if (kind == .comptime_assign_expr) {
                try a.pegToken(.keyword_comptime);
                a.not_block_expr = true;
            }
            try a.pegAssignExpr();
            try a.pegToken(.semicolon);
        },
    }
}

/// IfStatement
///     <- IfPrefix BlockExpr ( KEYWORD_else Payload? Statement )?
///      / IfPrefix !BlockExpr AssignExpr ( SEMICOLON / KEYWORD_else Payload? Statement )
fn pegIfStatement(a: *AstSmith) SourceError!void {
    try a.pegIfPrefix();
    const is_assign = a.smith.value(bool);
    if (!is_assign) {
        try a.pegBlockExpr();
    } else {
        a.not_block_expr = true;
        try a.pegAssignExpr();
    }
    if (a.not_token != .keyword_else and a.smithListItemBool()) {
        try a.pegToken(.keyword_else);
        if (a.smith.value(bool)) {
            try a.pegPayload();
        }
        try a.pegStatement();
    } else if (is_assign) {
        try a.pegToken(.semicolon);
    } else {
        a.not_token = .keyword_else;
    }
}

/// LabeledStatement <- BlockLabel? (Block / LoopStatement / SwitchExpr)
fn pegLabeledStatement(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegBlockLabel();
    }
    switch (a.smith.value(enum { block, loop_statement, switch_expr })) {
        .block => try a.pegBlock(),
        .loop_statement => try a.pegLoopStatement(),
        .switch_expr => try a.pegSwitchExpr(),
    }
}

/// LoopStatement <- KEYWORD_inline? (ForStatement / WhileStatement)
fn pegLoopStatement(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_inline);
    }
    if (a.smith.value(bool)) {
        try a.pegForStatement();
    } else {
        try a.pegWhileStatement();
    }
}

/// ForStatement
///     <- ForPrefix BlockExpr ( KEYWORD_else Statement / !KEYWORD_else )
///      / ForPrefix !BlockExpr AssignExpr ( SEMICOLON / KEYWORD_else Statement )
fn pegForStatement(a: *AstSmith) SourceError!void {
    try a.pegForPrefix();
    const is_assign = a.smith.value(bool);
    if (!is_assign) {
        try a.pegBlockExpr();
    } else {
        a.not_block_expr = true;
        try a.pegAssignExpr();
    }
    if (a.not_token != .keyword_else and a.smithListItemBool()) {
        try a.pegToken(.keyword_else);
        try a.pegStatement();
    } else if (is_assign) {
        try a.pegToken(.semicolon);
    } else {
        a.not_token = .keyword_else;
    }
}

/// WhileStatement
///     <- WhilePrefix BlockExpr ( KEYWORD_else Payload? Statement )?
///      / WhilePrefix !BlockExpr AssignExpr ( SEMICOLON / KEYWORD_else Payload? Statement )
fn pegWhileStatement(a: *AstSmith) SourceError!void {
    try a.pegWhilePrefix();
    const is_assign = a.smith.value(bool);
    if (!is_assign) {
        try a.pegBlockExpr();
    } else {
        a.not_block_expr = true;
        try a.pegAssignExpr();
    }
    if (a.not_token != .keyword_else and a.smithListItemBool()) {
        try a.pegToken(.keyword_else);
        if (a.smith.value(bool)) {
            try a.pegPayload();
        }
        try a.pegStatement();
    } else if (is_assign) {
        try a.pegToken(.semicolon);
    } else {
        a.not_token = .keyword_else;
    }
}

/// BlockExprStatement
///     <- BlockExpr
///      / !BlockExpr AssignExpr SEMICOLON
fn pegBlockExprStatement(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegBlockExpr();
    } else {
        a.not_block_expr = true;
        try a.pegAssignExpr();
        try a.pegToken(.semicolon);
    }
}

/// BlockExpr <- BlockLabel? Block
fn pegBlockExpr(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegBlockLabel();
    }
    try a.pegBlock();
}

/// VarAssignStatement <- (Expr / VarDeclProto) (COMMA (Expr / VarDeclProto))* EQUAL Expr SEMICOLON
fn pegVarAssignStatement(a: *AstSmith) SourceError!void {
    while (true) {
        if (a.smith.value(bool)) {
            try a.pegVarDeclProto();
        } else {
            try a.pegExpr();
        }

        if (a.smithListItemEos()) {
            break;
        } else {
            try a.pegToken(.comma);
        }
    }

    try a.pegToken(.equal);
    try a.pegExpr();
    try a.pegToken(.semicolon);
}

/// AssignExpr <- Expr (AssignOp Expr / (COMMA Expr)+ EQUAL Expr)?
fn pegAssignExpr(a: *AstSmith) SourceError!void {
    try a.pegExpr();
    if (a.smith.value(bool)) {
        if (!a.smithListItemBool()) {
            try a.pegAssignOp();
        } else {
            while (true) {
                try a.pegToken(.comma);
                try a.pegExpr();
                if (a.smithListItemEos()) break;
            }
            try a.pegToken(.equal);
        }
        try a.pegExpr();
    }
}

/// SingleAssignExpr <- Expr (AssignOp Expr)?
fn pegSingleAssignExpr(a: *AstSmith) SourceError!void {
    try a.pegExpr();
    if (a.smith.value(bool)) {
        try a.pegAssignOp();
        try a.pegExpr();
    }
}

/// Expr <- BoolOrExpr
const pegExpr = pegBoolOrExpr;

/// BoolOrExpr <- BoolAndExpr (KEYWORD_or BoolAndExpr)*
fn pegBoolOrExpr(a: *AstSmith) SourceError!void {
    try a.pegBoolAndExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        try a.pegTokenWhitespaceAround(.keyword_or);
        try a.pegBoolAndExpr();
    }
}

/// BoolAndExpr <- CompareExpr (KEYWORD_and CompareExpr)*
fn pegBoolAndExpr(a: *AstSmith) SourceError!void {
    try a.pegCompareExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        try a.pegTokenWhitespaceAround(.keyword_and);
        try a.pegCompareExpr();
    }
}

/// CompareExpr <- BitwiseExpr (CompareOp BitwiseExpr)?
fn pegCompareExpr(a: *AstSmith) SourceError!void {
    try a.pegBitwiseExpr();
    if (!a.not_expr_suffix and a.smithListItemBool()) {
        try a.pegCompareOp();
        try a.pegBitwiseExpr();
    }
}

/// BitwiseExpr <- BitShiftExpr (BitwiseOp BitShiftExpr)*
fn pegBitwiseExpr(a: *AstSmith) SourceError!void {
    try a.pegBitShiftExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        try a.pegBitwiseOp();
        try a.pegBitShiftExpr();
    }
}

/// BitShiftExpr <- AdditionExpr (BitShiftOp AdditionExpr)*
fn pegBitShiftExpr(a: *AstSmith) SourceError!void {
    try a.pegAdditionExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        try a.pegBitShiftOp();
        try a.pegAdditionExpr();
    }
}

/// AdditionExpr <- MultiplyExpr (AdditionOp MultiplyExpr)*
fn pegAdditionExpr(a: *AstSmith) SourceError!void {
    try a.pegMultiplyExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        try a.pegAdditionOp();
        try a.pegMultiplyExpr();
    }
}

/// MultiplyExpr <- PrefixExpr (MultiplyOp PrefixExpr)*
fn pegMultiplyExpr(a: *AstSmith) SourceError!void {
    try a.pegPrefixExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        try a.pegMultiplyOp();
        try a.pegPrefixExpr();
    }
}

/// PrefixExpr <- PrefixOp* PrimaryExpr
fn pegPrefixExpr(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegPrefixOp();
    }
    try a.pegPrimaryExpr();
}

/// PrimaryExpr
///     <- AsmExpr
///      / IfExpr
///      / KEYWORD_break (BreakLabel / !BreakLabel) (Expr !ExprSuffix / !SinglePtrTypeStart)
///      / KEYWORD_comptime Expr !ExprSuffix
///      / KEYWORD_nosuspend Expr !ExprSuffix
///      / KEYWORD_continue (BreakLabel / !BreakLabel) (Expr !ExprSuffix / !SinglePtrTypeStart)
///      / KEYWORD_resume Expr !ExprSuffix
///      / KEYWORD_return (Expr !ExprSuffix / !SinglePtrTypeStart)
///      / BlockLabel? LoopExpr
///      / Block
///      / CurlySuffixExpr
fn pegPrimaryExpr(a: *AstSmith) SourceError!void {
    const Kind = enum(u8) {
        curly_suffix_expr,
        @"return",
        @"continue",
        @"break",
        block,
        asm_expr,
        // Always contain more expressions
        if_expr,
        loop_expr,
        @"resume",
        @"comptime",
        @"nosuspend",
    };

    switch (a.smith.valueWeighted(Kind, &.{
        .value(Kind, .curly_suffix_expr, 75),
        .rangeAtMost(Kind, .@"return", .asm_expr, 4),
        .rangeAtMost(Kind, .if_expr, .@"nosuspend", 1),
    })) {
        .curly_suffix_expr => try a.pegCurlySuffixExpr(),

        .block => if (a.not_labelable_expr != .expr and !a.not_block_expr and !a.not_expr_statement) {
            try a.pegBlock();
        } else {
            // Group
            try a.pegToken(.l_paren);
            try a.pegBlock();
            try a.pegToken(.r_paren);
        },
        .asm_expr => try a.pegAsmExpr(),
        .if_expr => if (!a.not_expr_statement) {
            try a.pegIfExpr();
        } else {
            // Group
            try a.pegToken(.l_paren);
            try a.pegIfExpr();
            try a.pegToken(.r_paren);
        },
        .loop_expr => {
            const group = a.not_labelable_expr == .expr or a.not_expr_statement;
            if (group) try a.pegToken(.l_paren);
            if (!a.not_label and a.not_token != .identifier and a.smith.value(bool)) {
                try a.pegBlockLabel();
            }
            try a.pegLoopExpr();
            if (group) try a.pegToken(.r_paren);
        },

        .@"return",
        .@"comptime",
        .@"nosuspend",
        .@"resume",
        .@"break",
        .@"continue",
        => |t| {
            const group = a.not_expr_statement and (t == .@"nosuspend" or t == .@"comptime");
            if (group) try a.pegToken(.l_paren);

            const kw: Token.Tag, const label, const expr = switch (t) {
                .@"return" => .{ .keyword_return, false, a.smithListItemBool() },
                .@"comptime" => .{ .keyword_comptime, false, true },
                .@"nosuspend" => .{ .keyword_nosuspend, false, true },
                .@"resume" => .{ .keyword_resume, false, true },
                .@"break" => .{ .keyword_break, a.smith.value(bool), a.smithListItemBool() },
                .@"continue" => .{ .keyword_continue, a.smith.value(bool), a.smithListItemBool() },
                else => unreachable,
            };
            try a.pegToken(kw);
            if (label) {
                try a.pegBreakLabel();
            } else {
                a.not_break_label = true;
            }
            if (expr) {
                try a.pegExpr();
                a.not_expr_suffix = true;
            } else {
                a.not_token = .asterisk;
            }

            if (group) try a.pegToken(.r_paren);
        },
    }
}

/// IfExpr <- IfPrefix Expr (KEYWORD_else Payload? Expr)? !ExprSuffix
fn pegIfExpr(a: *AstSmith) SourceError!void {
    try a.pegIfPrefix();
    try a.pegExpr();
    const Else = enum { none, @"else", else_payload };
    switch (if (a.not_token != .keyword_else) a.smith.value(Else) else .none) {
        .none => a.not_token = .keyword_else,
        .@"else" => {
            try a.pegToken(.keyword_else);
            try a.pegExpr();
        },
        .else_payload => {
            try a.pegToken(.keyword_else);
            try a.pegPayload();
            try a.pegExpr();
        },
    }
    a.not_expr_suffix = true;
}

/// Block <- LBRACE Statement* RBRACE
fn pegBlock(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_brace);
    while (!a.smithListItemEos()) {
        try a.pegBlockStatement();
    }
    try a.pegToken(.r_brace);
}

/// LoopExpr <- KEYWORD_inline? (ForExpr / WhileExpr)
fn pegLoopExpr(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_inline);
    }

    if (a.smith.value(bool)) {
        try a.pegForExpr();
    } else {
        try a.pegWhileExpr();
    }
}

/// ForExpr <- ForPrefix Expr (KEYWORD_else Expr / !KEYWORD_else) !ExprSuffix
fn pegForExpr(a: *AstSmith) SourceError!void {
    try a.pegForPrefix();
    try a.pegExpr();
    if (a.not_token != .keyword_else and a.smith.value(bool)) {
        try a.pegToken(.keyword_else);
        try a.pegExpr();
    } else {
        a.not_token = .keyword_else;
    }
    a.not_expr_suffix = true;
}

/// WhileExpr <- WhilePrefix Expr (KEYWORD_else Payload? Expr)? !ExprSuffix
fn pegWhileExpr(a: *AstSmith) SourceError!void {
    try a.pegWhilePrefix();
    try a.pegExpr();
    const Else = enum { none, @"else", else_payload };
    switch (if (a.not_token != .keyword_else) a.smith.value(Else) else .none) {
        .none => a.not_token = .keyword_else,
        .@"else" => {
            try a.pegToken(.keyword_else);
            try a.pegExpr();
        },
        .else_payload => {
            try a.pegToken(.keyword_else);
            try a.pegPayload();
            try a.pegExpr();
        },
    }
    a.not_expr_suffix = true;
}

/// CurlySuffixExpr <- TypeExpr InitList?
fn pegCurlySuffixExpr(a: *AstSmith) SourceError!void {
    try a.pegTypeExpr();
    if (!a.not_expr_suffix and a.smith.value(bool)) {
        try a.pegInitList();
    }
}

/// InitList
///     <- LBRACE FieldInit (COMMA FieldInit)* COMMA? RBRACE
///      / LBRACE Expr (COMMA Expr)* COMMA? RBRACE
///      / LBRACE RBRACE
fn pegInitList(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_brace);
    if (a.smithListItemBool()) {
        if (a.smith.value(bool)) {
            try a.pegFieldInit();
            while (!a.smithListItemEos()) {
                try a.pegToken(.comma);
                try a.pegFieldInit();
            }
        } else {
            try a.pegExpr();
            while (!a.smithListItemEos()) {
                try a.pegToken(.comma);
                try a.pegExpr();
            }
        }
        if (a.smith.value(bool)) {
            try a.pegToken(.comma);
        }
    }
    try a.pegToken(.r_brace);
}

/// PrefixTypeOp* ErrorUnionExpr
fn pegTypeExpr(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegPrefixTypeOp();
    }
    try a.pegErrorUnionExpr();
}

/// ErrorUnionExpr <- SuffixExpr (EXCLAMATIONMARK TypeExpr)?
fn pegErrorUnionExpr(a: *AstSmith) SourceError!void {
    try a.pegSuffixExpr();
    if (!a.not_expr_suffix and a.smithListItemBool()) {
        try a.pegToken(.bang);
        try a.pegTypeExpr();
    }
}

/// SuffixExpr
///    <- PrimaryTypeExpr (SuffixOp / FnCallArguments)*
fn pegSuffixExpr(a: *AstSmith) SourceError!void {
    try a.pegPrimaryTypeExpr();
    while (!a.not_expr_suffix and !a.smithListItemEos()) {
        if (a.smith.value(bool)) {
            try a.pegSuffixOp();
        } else {
            try a.pegFnCallArguments();
        }
    }
}

/// PrimaryTypeExpr
///     <- BUILTINIDENTIFIER FnCallArguments
///      / CHAR_LITERAL
///      / ContainerDecl
///      / DOT IDENTIFIER
///      / DOT InitList
///      / ErrorSetDecl
///      / FLOAT
///      / FnProto
///      / GroupedExpr
///      / LabeledTypeExpr
///      / IDENTIFIER !(COLON LabelableExpr)
///      / IfTypeExpr
///      / INTEGER
///      / KEYWORD_comptime TypeExpr !ExprSuffix
///      / KEYWORD_error DOT IDENTIFIER
///      / KEYWORD_anyframe
///      / KEYWORD_unreachable
///      / STRINGLITERAL
fn pegPrimaryTypeExpr(a: *AstSmith) SourceError!void {
    const Kind = enum(u8) {
        identifier,
        float,
        integer,
        char_literal,
        string_literal,
        enum_literal,
        error_literal,
        unreachable_type,
        anyframe_type,

        // Containing zero or more expressions
        builtin_call,
        array_literal,
        container_decl,
        fn_proto,
        error_set,

        // Containing one or more epressions
        grouped,
        labeled_type_expr,
        if_type_expr,
        comptime_expr,
    };

    switch (a.smith.valueWeighted(Kind, &.{
        .rangeAtMost(Kind, .identifier, .anyframe_type, 5),
        .rangeAtMost(Kind, .builtin_call, .error_set, 2),
        .rangeAtMost(Kind, .grouped, .comptime_expr, 1),
    })) {
        .identifier => if (a.not_token != .identifier) {
            try a.pegIdentifier();
            a.not_labelable_expr = .colon;
        } else {
            // Group
            try a.pegToken(.l_paren);
            try a.pegIdentifier();
            try a.pegToken(.r_paren);
        },
        .float => try a.pegFloat(),
        .integer => try a.pegInteger(),
        .char_literal => try a.pegCharLiteral(),
        .string_literal => try a.pegStringLiteral(),
        .enum_literal => {
            try a.pegToken(.period);
            try a.pegIdentifier();
        },
        .error_literal => {
            try a.pegToken(.keyword_error);
            try a.pegToken(.period);
            try a.pegIdentifier();
        },
        .unreachable_type => try a.pegToken(.keyword_unreachable),
        .anyframe_type => try a.pegToken(.keyword_anyframe),

        .builtin_call => {
            try a.pegBuiltinIdentifier();
            try a.pegFnCallArguments();
        },
        .array_literal => {
            try a.pegToken(.period);
            try a.pegInitList();
        },
        .container_decl => try a.pegContainerDecl(),
        .fn_proto => if (a.not_token != .keyword_fn) {
            try a.pegFnProto();
        } else {
            // Group
            try a.pegToken(.l_paren);
            try a.pegFnProto();
            try a.pegToken(.r_paren);
        },
        .error_set => try a.pegErrorSetDecl(),

        .grouped => try a.pegGroupedExpr(),
        .labeled_type_expr => try a.pegLabeledTypeExpr(),
        .if_type_expr => if (!a.not_expr_statement) {
            try a.pegIfTypeExpr();
        } else {
            // Group
            try a.pegToken(.l_paren);
            try a.pegIfTypeExpr();
            try a.pegToken(.r_paren);
        },
        .comptime_expr => if (!a.not_token_comptime and !a.not_expr_statement) {
            try a.pegToken(.keyword_comptime);
            try a.pegTypeExpr();
        } else {
            // Group
            try a.pegToken(.l_paren);
            try a.pegToken(.keyword_comptime);
            try a.pegTypeExpr();
            try a.pegToken(.r_paren);
        },
    }
}

/// ContainerDecl <- (KEYWORD_extern / KEYWORD_packed)? ContainerDeclAuto
fn pegContainerDecl(a: *AstSmith) SourceError!void {
    switch (a.smith.value(enum { auto, @"extern", @"packed" })) {
        .auto => {},
        .@"extern" => try a.pegToken(.keyword_extern),
        .@"packed" => try a.pegToken(.keyword_packed),
    }
    try a.pegContainerDeclAuto();
}

/// ErrorSetDecl <- KEYWORD_error LBRACE IdentifierList RBRACE
fn pegErrorSetDecl(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_error);
    try a.pegToken(.l_brace);
    try a.pegIdentifierList();
    try a.pegToken(.r_brace);
}

/// GroupedExpr <- LPAREN Expr RPAREN
fn pegGroupedExpr(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
}

/// IfTypeExpr <- IfPrefix TypeExpr (KEYWORD_else Payload? TypeExpr)? !ExprSuffix
fn pegIfTypeExpr(a: *AstSmith) SourceError!void {
    try a.pegIfPrefix();
    try a.pegTypeExpr();
    const Else = enum { none, @"else", else_payload };
    switch (if (a.not_token != .keyword_else) a.smith.value(Else) else .none) {
        .none => a.not_token = .keyword_else,
        .@"else" => {
            try a.pegToken(.keyword_else);
            try a.pegTypeExpr();
        },
        .else_payload => {
            try a.pegToken(.keyword_else);
            try a.pegPayload();
            try a.pegTypeExpr();
        },
    }
    a.not_expr_suffix = true;
}

/// LabeledTypeExpr
///     <- BlockLabel Block
///      / BlockLabel? LoopTypeExpr
///      / BlockLabel? SwitchExpr
fn pegLabeledTypeExpr(a: *AstSmith) SourceError!void {
    const kind = a.smith.value(enum { block, loop, @"switch" });
    const not_any = a.not_labelable_expr == .expr or a.not_expr_statement;
    const no_label = a.not_label or a.not_token == .identifier;
    const no_block = no_label or a.not_block_expr;
    const group = not_any or (kind == .block and no_block);
    if (group) try a.pegToken(.l_paren);

    switch (kind) {
        .block => {
            try a.pegBlockLabel();
            try a.pegBlock();
        },
        .loop => {
            if (!no_label and a.smith.value(bool)) {
                try a.pegBlockLabel();
            }
            try a.pegLoopTypeExpr();
        },
        .@"switch" => {
            if (!no_label and a.smith.value(bool)) {
                try a.pegBlockLabel();
            }
            try a.pegSwitchExpr();
        },
    }

    if (group) try a.pegToken(.r_paren);
}

/// LoopTypeExpr <- KEYWORD_inline? (ForTypeExpr / WhileTypeExpr)
fn pegLoopTypeExpr(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_inline);
    }

    if (a.smith.value(bool)) {
        try a.pegForTypeExpr();
    } else {
        try a.pegWhileTypeExpr();
    }
}

/// ForTypeExpr <- ForPrefix TypeExpr (KEYWORD_else TypeExpr / !KEYWORD_else) !ExprSuffix
fn pegForTypeExpr(a: *AstSmith) SourceError!void {
    try a.pegForPrefix();
    try a.pegTypeExpr();
    if (a.not_token != .keyword_else and a.smith.value(bool)) {
        try a.pegToken(.keyword_else);
        try a.pegTypeExpr();
    } else {
        a.not_token = .keyword_else;
    }
    a.not_expr_suffix = true;
}

/// WhileTypeExpr <- WhilePrefix TypeExpr (KEYWORD_else Payload? TypeExpr)? !ExprSuffix
fn pegWhileTypeExpr(a: *AstSmith) SourceError!void {
    try a.pegWhilePrefix();
    try a.pegTypeExpr();
    const Else = enum { none, @"else", else_payload };
    switch (if (a.not_token != .keyword_else) a.smith.value(Else) else .none) {
        .none => a.not_token = .keyword_else,
        .@"else" => {
            try a.pegToken(.keyword_else);
            try a.pegTypeExpr();
        },
        .else_payload => {
            try a.pegToken(.keyword_else);
            try a.pegPayload();
            try a.pegTypeExpr();
        },
    }
    a.not_expr_suffix = true;
}

/// SwitchExpr <- KEYWORD_switch LPAREN Expr RPAREN LBRACE SwitchProngList RBRACE
fn pegSwitchExpr(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_switch);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);

    try a.pegToken(.l_brace);
    try a.pegSwitchProngList();
    try a.pegToken(.r_brace);
}

/// AsmExpr <- KEYWORD_asm KEYWORD_volatile? LPAREN Expr AsmOutput? RPAREN
fn pegAsmExpr(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_asm);
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_volatile);
    }
    try a.pegToken(.l_paren);
    try a.pegExpr();
    if (a.smith.value(bool)) {
        try a.pegAsmOutput();
    }
    try a.pegToken(.r_paren);
}

/// AsmOutput <- COLON AsmOutputList AsmInput?
fn pegAsmOutput(a: *AstSmith) SourceError!void {
    try a.pegToken(.colon);
    try a.pegAsmOutputList();
    if (a.smith.value(bool)) {
        try a.pegAsmInput();
    }
}

/// AsmOutputItem <- LBRACKET IDENTIFIER RBRACKET STRINGLITERALSINGLE LPAREN (MINUSRARROW TypeExpr / IDENTIFIER) RPAREN
fn pegAsmOutputItem(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_bracket);
    try a.pegIdentifier();
    try a.pegToken(.r_bracket);
    try a.pegStringLiteralSingle();
    try a.pegToken(.l_paren);
    if (a.smith.value(bool)) {
        try a.pegToken(.arrow);
        try a.pegTypeExpr();
    } else {
        try a.pegIdentifier();
    }
    try a.pegToken(.r_paren);
}

/// AsmInput <- COLON AsmInputList AsmClobbers?
fn pegAsmInput(a: *AstSmith) SourceError!void {
    try a.pegToken(.colon);
    try a.pegAsmInputList();
    if (a.smith.value(bool)) {
        try a.pegAsmClobbers();
    }
}

/// AsmInputItem <- LBRACKET IDENTIFIER RBRACKET STRINGLITERALSINGLE LPAREN Expr RPAREN
fn pegAsmInputItem(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_bracket);
    try a.pegIdentifier();
    try a.pegToken(.r_bracket);
    try a.pegStringLiteralSingle();
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
}

/// AsmClobbers <- COLON Expr
fn pegAsmClobbers(a: *AstSmith) SourceError!void {
    try a.pegToken(.colon);
    try a.pegExpr();
}

/// BreakLabel <- COLON IDENTIFIER
fn pegBreakLabel(a: *AstSmith) SourceError!void {
    try a.pegToken(.colon);
    try a.pegIdentifier();
}

/// BlockLabel <- IDENTIFIER COLON
fn pegBlockLabel(a: *AstSmith) SourceError!void {
    try a.pegIdentifier();
    try a.pegToken(.colon);
}

/// FieldInit <- DOT IDENTIFIER EQUAL Expr
fn pegFieldInit(a: *AstSmith) SourceError!void {
    try a.pegToken(.period);
    try a.pegIdentifier();
    try a.pegToken(.equal);
    try a.pegExpr();
}

/// WhileContinueExpr <- COLON LPAREN AssignExpr RPAREN
fn pegWhileContinueExpr(a: *AstSmith) SourceError!void {
    try a.pegToken(.colon);
    try a.pegToken(.l_paren);
    try a.pegAssignExpr();
    try a.pegToken(.r_paren);
}

/// LinkSection <- KEYWORD_linksection LPAREN Expr RPAREN
fn pegLinkSection(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_linksection);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
}

/// AddrSpace <- KEYWORD_addrspace LPAREN Expr RPAREN
fn pegAddrSpace(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_addrspace);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
}

/// CallConv <- KEYWORD_callconv LPAREN Expr RPAREN
fn pegCallConv(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_callconv);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
}

/// ParamDecl <- doc_comment? (KEYWORD_noalias / KEYWORD_comptime)?
///            ((IDENTIFIER COLON) / !KEYWORD_comptime !(IDENTIFIER COLON))
///            ParamType
fn pegParamDecl(a: *AstSmith) SourceError!void {
    try a.pegMaybeDocComment();
    const modifier = a.smith.value(enum { none, @"noalias", @"comptime" });
    switch (modifier) {
        .none => a.not_token_comptime = true,
        .@"noalias" => try a.pegToken(.keyword_noalias),
        .@"comptime" => try a.pegToken(.keyword_comptime),
    }
    if (a.smith.value(bool)) {
        try a.pegIdentifier();
        try a.pegToken(.colon);
    } else {
        a.not_label = true;
    }
    try a.pegParamType();
}

/// ParamType
///     <- KEYWORD_anytype
///      / TypeExpr
fn pegParamType(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_anytype);
    } else {
        try a.pegTypeExpr();
    }
}

/// IfPrefix <- KEYWORD_if LPAREN Expr RPAREN PtrPayload?
fn pegIfPrefix(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_if);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
    try a.pegPtrPayload();
}

/// WhilePrefix <- KEYWORD_while LPAREN Expr RPAREN PtrPayload? WhileContinueExpr?
fn pegWhilePrefix(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_while);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);

    if (a.smith.value(bool)) {
        try a.pegPtrPayload();
    }

    if (a.smith.value(bool)) {
        try a.pegWhileContinueExpr();
    }
}

/// ForPrefix <- KEYWORD_for LPAREN ForArgumentsList RPAREN PtrListPayload
///
/// An additional requirement checked in the Parser is that the number of
/// arguments and payload elements are the same.
fn pegForPrefix(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_for);
    try a.pegToken(.l_paren);
    const n = try a.pegForArgumentsList();
    try a.pegToken(.r_paren);
    try a.pegPtrListPayload(n);
}

/// Payload <- PIPE IDENTIFIER PIPE
fn pegPayload(a: *AstSmith) SourceError!void {
    try a.pegToken(.pipe);
    try a.pegIdentifier();
    try a.pegToken(.pipe);
}

/// PtrPayload <- PIPE ASTERISK? IDENTIFIER PIPE
fn pegPtrPayload(a: *AstSmith) SourceError!void {
    try a.pegToken(.pipe);
    if (a.smith.value(bool)) {
        try a.pegToken(.asterisk);
    }
    try a.pegIdentifier();
    try a.pegToken(.pipe);
}

/// PtrIndexPayload <- PIPE ASTERISK? IDENTIFIER (COMMA IDENTIFIER)? PIPE
fn pegPtrIndexPayload(a: *AstSmith) SourceError!void {
    try a.pegToken(.pipe);
    if (a.smith.value(bool)) {
        try a.pegToken(.asterisk);
    }
    try a.pegIdentifier();
    if (a.smith.value(bool)) {
        try a.pegToken(.comma);
        try a.pegIdentifier();
    }
    try a.pegToken(.pipe);
}

/// PtrListPayload <- PIPE ASTERISK? IDENTIFIER (COMMA ASTERISK? IDENTIFIER)* COMMA? PIPE
fn pegPtrListPayload(a: *AstSmith, n: usize) SourceError!void {
    try a.pegToken(.pipe);
    if (a.smith.value(bool)) {
        try a.pegToken(.asterisk);
    }
    try a.pegIdentifier();

    for (1..n) |_| {
        try a.pegToken(.comma);
        if (a.smith.value(bool)) {
            try a.pegToken(.asterisk);
        }
        try a.pegIdentifier();
    }

    if (a.smith.value(bool)) {
        try a.pegToken(.comma);
    }
    try a.pegToken(.pipe);
}

/// SwitchProng <- KEYWORD_inline? SwitchCase EQUALRARROW PtrIndexPayload? SingleAssignExpr
fn pegSwitchProng(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegToken(.keyword_inline);
    }
    try a.pegSwitchCase();
    try a.pegToken(.equal_angle_bracket_right);
    if (a.smith.value(bool)) {
        try a.pegPtrIndexPayload();
    }
    try a.pegSingleAssignExpr();
}

/// SwitchCase
///     <- SwitchItem (COMMA SwitchItem)* COMMA?
///      / KEYWORD_else
fn pegSwitchCase(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegSwitchItem();
        while (!a.smithListItemEos()) {
            try a.pegToken(.comma);
            try a.pegSwitchItem();
        }
        if (a.smith.value(bool)) {
            try a.pegToken(.comma);
        }
    } else {
        try a.pegToken(.keyword_else);
    }
}

/// SwitchItem <- Expr (DOT3 Expr)?
fn pegSwitchItem(a: *AstSmith) SourceError!void {
    try a.pegExpr();
    if (a.smith.value(bool)) {
        try a.pegToken(.ellipsis3);
        try a.pegExpr();
    }
}

/// ForArgumentsList <- ForItem (COMMA ForItem)* COMMA?
fn pegForArgumentsList(a: *AstSmith) SourceError!usize {
    try a.pegForItem();
    var n: usize = 1;
    while (!a.smithListItemEos()) {
        try a.pegToken(.comma);
        try a.pegForItem();
        n += 1;
    }
    if (a.smith.value(bool)) {
        try a.pegToken(.comma);
    }
    return n;
}

/// ForItem <- Expr (DOT2 Expr?)?
fn pegForItem(a: *AstSmith) SourceError!void {
    try a.pegExpr();
    const components = a.smith.valueRangeAtMost(u2, 0, 2);
    if (components >= 1) try a.pegToken(.ellipsis2);
    if (components >= 2) try a.pegExpr();
}

/// AssignOp
///     <- ASTERISKEQUAL
///      / ASTERISKPIPEEQUAL
///      / SLASHEQUAL
///      / PERCENTEQUAL
///      / PLUSEQUAL
///      / PLUSPIPEEQUAL
///      / MINUSEQUAL
///      / MINUSPIPEEQUAL
///      / LARROW2EQUAL
///      / LARROW2PIPEEQUAL
///      / RARROW2EQUAL
///      / AMPERSANDEQUAL
///      / CARETEQUAL
///      / PIPEEQUAL
///      / ASTERISKPERCENTEQUAL
///      / PLUSPERCENTEQUAL
///      / MINUSPERCENTEQUAL
///      / EQUAL
fn pegAssignOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .asterisk_equal,
        .asterisk_pipe_equal,
        .slash_equal,
        .percent_equal,
        .plus_equal,
        .plus_pipe_equal,
        .minus_equal,
        .minus_pipe_equal,
        .angle_bracket_angle_bracket_left_equal,
        .angle_bracket_angle_bracket_left_pipe_equal,
        .angle_bracket_angle_bracket_right_equal,
        .ampersand_equal,
        .caret_equal,
        .pipe_equal,
        .asterisk_percent_equal,
        .plus_percent_equal,
        .minus_percent_equal,
        .equal,
    };
    try a.pegToken(tags[a.smith.index(tags.len)]);
}

/// CompareOp
///     <- EQUALEQUAL
///      / EXCLAMATIONMARKEQUAL
///      / LARROW
///      / RARROW
///      / LARROWEQUAL
///      / RARROWEQUAL
fn pegCompareOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .equal_equal,
        .bang_equal,
        .angle_bracket_left,
        .angle_bracket_right,
        .angle_bracket_left_equal,
        .angle_bracket_right_equal,
    };
    try a.pegTokenWhitespaceAround(tags[a.smith.index(tags.len)]);
}

/// BitwiseOp
///     <- AMPERSAND
///      / CARET
///      / PIPE
///      / KEYWORD_orelse
///      / KEYWORD_catch Payload?
fn pegBitwiseOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .ampersand,
        .caret,
        .pipe,
        .keyword_orelse,
        .keyword_catch,
    };
    const tag = tags[a.smith.index(tags.len)];
    try a.pegTokenWhitespaceAround(tag);
    if (tag == .keyword_catch and a.smith.value(bool)) {
        try a.pegPayload();
    }
}

/// BitShiftOp
///     <- LARROW2
///      / RARROW2
///      / LARROW2PIPE
fn pegBitShiftOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .angle_bracket_angle_bracket_left,
        .angle_bracket_angle_bracket_right,
        .angle_bracket_angle_bracket_left_pipe,
    };
    try a.pegTokenWhitespaceAround(tags[a.smith.index(tags.len)]);
}

/// AdditionOp
///     <- PLUS
///      / MINUS
///      / PLUS2
///      / PLUSPERCENT
///      / MINUSPERCENT
///      / PLUSPIPE
///      / MINUSPIPE
fn pegAdditionOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .plus,
        .minus,
        .plus_plus,
        .plus_percent,
        .minus_percent,
        .plus_pipe,
        .minus_pipe,
    };
    try a.pegTokenWhitespaceAround(tags[a.smith.index(tags.len)]);
}

/// MultiplyOp
///     <- PIPE2
///      / ASTERISK
///      / SLASH
///      / PERCENT
///      / ASTERISK2
///      / ASTERISKPERCENT
///      / ASTERISKPIPE
fn pegMultiplyOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .asterisk,
        .asterisk_asterisk,
        .pipe_pipe,
        .slash,
        .percent,
        .asterisk_percent,
        .asterisk_pipe,
    };
    const start = @as(u8, 2) * @intFromBool(a.not_token == .asterisk);
    try a.pegTokenWhitespaceAround(tags[a.smith.valueRangeLessThan(u8, start, tags.len)]);
}

/// PrefixOp
///     <- EXCLAMATIONMARK
///      / MINUS
///      / TILDE
///      / MINUSPERCENT
///      / AMPERSAND
///      / KEYWORD_try
fn pegPrefixOp(a: *AstSmith) SourceError!void {
    const tags = [_]Token.Tag{
        .bang,
        .minus,
        .tilde,
        .minus_percent,
        .ampersand,
        .keyword_try,
    };
    try a.pegToken(tags[a.smith.index(tags.len)]);
}

/// PrefixTypeOp
///     <- QUESTIONMARK
///      / KEYWORD_anyframe MINUSRARROW
///      / (ManyPtrTypeStart / SliceTypeStart) KEYWORD_allowzero? ByteAlign? AddrSpace?
///      KEYWORD_const? KEYWORD_volatile?
///      / SinglePtrTypeStart KEYWORD_allowzero? BitAlign? AddrSpace?
///      KEYWORD_const? KEYWORD_volatile?
///      / ArrayTypeStart
fn pegPrefixTypeOp(a: *AstSmith) SourceError!void {
    switch (a.smith.value(enum {
        optional,
        anyframe_arrow,
        array,
        single_pointer,
        many_pointer,
        slice,
    })) {
        .optional => try a.pegToken(.question_mark),
        .anyframe_arrow => {
            try a.pegToken(.keyword_anyframe);
            try a.pegToken(.arrow);
        },
        .array => try a.pegArrayTypeStart(),
        .single_pointer, .many_pointer, .slice => |kind| {
            const is_single = kind == .single_pointer and a.not_token != .asterisk;
            if (is_single) {
            
```
