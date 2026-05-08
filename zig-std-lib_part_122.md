```
r only if we had a
    // result pointer and aren't forwarding it.
    const LocTag = @typeInfo(ResultInfo.Loc).@"union".tag_type.?;
    const need_result_rvalue = @as(LocTag, block_ri.rl) != @as(LocTag, ri.rl);

    if (while_full.label_token) |label_token| {
        try astgen.checkLabelRedefinition(scope, label_token);
    }

    const is_inline = while_full.inline_token != null;
    if (parent_gz.is_comptime and is_inline) {
        try astgen.appendErrorTok(while_full.inline_token.?, "redundant inline keyword in comptime scope", .{});
    }
    const loop_tag: Zir.Inst.Tag = if (is_inline) .block_inline else .loop;
    const loop_block = try parent_gz.makeBlockInst(loop_tag, node);
    try parent_gz.instructions.append(astgen.gpa, loop_block);

    var loop_scope = parent_gz.makeSubBlock(scope);
    loop_scope.is_inline = is_inline;
    defer loop_scope.unstack();

    var cond_scope = parent_gz.makeSubBlock(&loop_scope.base);
    defer cond_scope.unstack();

    const payload_is_ref = if (while_full.payload_token) |payload_token|
        tree.tokenTag(payload_token) == .asterisk
    else
        false;

    try emitDbgNode(parent_gz, while_full.ast.cond_expr);
    const cond: struct {
        inst: Zir.Inst.Ref,
        bool_bit: Zir.Inst.Ref,
    } = c: {
        if (while_full.error_token) |_| {
            const cond_ri: ResultInfo = .{ .rl = if (payload_is_ref) .ref else .none };
            const err_union = try fullBodyExpr(&cond_scope, &cond_scope.base, cond_ri, while_full.ast.cond_expr, .normal);
            const tag: Zir.Inst.Tag = if (payload_is_ref) .is_non_err_ptr else .is_non_err;
            break :c .{
                .inst = err_union,
                .bool_bit = try cond_scope.addUnNode(tag, err_union, while_full.ast.cond_expr),
            };
        } else if (while_full.payload_token) |_| {
            const cond_ri: ResultInfo = .{ .rl = if (payload_is_ref) .ref else .none };
            const optional = try fullBodyExpr(&cond_scope, &cond_scope.base, cond_ri, while_full.ast.cond_expr, .normal);
            const tag: Zir.Inst.Tag = if (payload_is_ref) .is_non_null_ptr else .is_non_null;
            break :c .{
                .inst = optional,
                .bool_bit = try cond_scope.addUnNode(tag, optional, while_full.ast.cond_expr),
            };
        } else {
            const cond = try fullBodyExpr(&cond_scope, &cond_scope.base, coerced_bool_ri, while_full.ast.cond_expr, .normal);
            break :c .{
                .inst = cond,
                .bool_bit = cond,
            };
        }
    };

    const condbr_tag: Zir.Inst.Tag = if (is_inline) .condbr_inline else .condbr;
    const condbr = try cond_scope.addCondBr(condbr_tag, node);
    const block_tag: Zir.Inst.Tag = if (is_inline) .block_inline else .block;
    const cond_block = try loop_scope.makeBlockInst(block_tag, node);
    try cond_scope.setBlockBody(cond_block);
    // cond_scope unstacked now, can add new instructions to loop_scope
    try loop_scope.instructions.append(astgen.gpa, cond_block);

    // make scope now but don't stack on parent_gz until loop_scope
    // gets unstacked after cont_expr is emitted and added below
    var then_scope = parent_gz.makeSubBlock(&cond_scope.base);
    then_scope.instructions_top = GenZir.unstacked_top;
    defer then_scope.unstack();

    var dbg_var_name: Zir.NullTerminatedString = .empty;
    var dbg_var_inst: Zir.Inst.Ref = undefined;
    var opt_payload_inst: Zir.Inst.OptionalIndex = .none;
    var payload_val_scope: Scope.LocalVal = undefined;
    const then_sub_scope = s: {
        if (while_full.error_token != null) {
            if (while_full.payload_token) |payload_token| {
                const tag: Zir.Inst.Tag = if (payload_is_ref)
                    .err_union_payload_unsafe_ptr
                else
                    .err_union_payload_unsafe;
                // will add this instruction to then_scope.instructions below
                const payload_inst = try then_scope.makeUnNode(tag, cond.inst, while_full.ast.cond_expr);
                opt_payload_inst = payload_inst.toOptional();
                const ident_token = payload_token + @intFromBool(payload_is_ref);
                const ident_bytes = tree.tokenSlice(ident_token);
                if (mem.eql(u8, "_", ident_bytes)) {
                    if (payload_is_ref) return astgen.failTok(payload_token, "pointer modifier invalid on discard", .{});
                    break :s &then_scope.base;
                }
                const ident_name = try astgen.identAsString(ident_token);
                try astgen.detectLocalShadowing(&then_scope.base, ident_name, ident_token, ident_bytes, .capture);
                payload_val_scope = .{
                    .parent = &then_scope.base,
                    .gen_zir = &then_scope,
                    .name = ident_name,
                    .inst = payload_inst.toRef(),
                    .token_src = ident_token,
                    .id_cat = .capture,
                };
                dbg_var_name = ident_name;
                dbg_var_inst = payload_inst.toRef();
                break :s &payload_val_scope.base;
            } else {
                _ = try then_scope.addUnNode(.ensure_err_union_payload_void, cond.inst, node);
                break :s &then_scope.base;
            }
        } else if (while_full.payload_token) |payload_token| {
            const tag: Zir.Inst.Tag = if (payload_is_ref)
                .optional_payload_unsafe_ptr
            else
                .optional_payload_unsafe;
            // will add this instruction to then_scope.instructions below
            const payload_inst = try then_scope.makeUnNode(tag, cond.inst, while_full.ast.cond_expr);
            opt_payload_inst = payload_inst.toOptional();
            const ident_token = payload_token + @intFromBool(payload_is_ref);
            const ident_name = try astgen.identAsString(ident_token);
            const ident_bytes = tree.tokenSlice(ident_token);
            if (mem.eql(u8, "_", ident_bytes)) {
                if (payload_is_ref) return astgen.failTok(payload_token, "pointer modifier invalid on discard", .{});
                break :s &then_scope.base;
            }
            try astgen.detectLocalShadowing(&then_scope.base, ident_name, ident_token, ident_bytes, .capture);
            payload_val_scope = .{
                .parent = &then_scope.base,
                .gen_zir = &then_scope,
                .name = ident_name,
                .inst = payload_inst.toRef(),
                .token_src = ident_token,
                .id_cat = .capture,
            };
            dbg_var_name = ident_name;
            dbg_var_inst = payload_inst.toRef();
            break :s &payload_val_scope.base;
        } else {
            break :s &then_scope.base;
        }
    };

    var continue_scope = parent_gz.makeSubBlock(then_sub_scope);
    continue_scope.instructions_top = GenZir.unstacked_top;
    defer continue_scope.unstack();
    const continue_block = try then_scope.makeBlockInst(block_tag, node);

    const repeat_tag: Zir.Inst.Tag = if (is_inline) .repeat_inline else .repeat;
    _ = try loop_scope.addNode(repeat_tag, node);

    try loop_scope.setBlockBody(loop_block);
    if (while_full.label_token) |label_token| {
        loop_scope.label = .{ .token = label_token };
    }
    loop_scope.allow_unlabeled_control_flow = true;
    loop_scope.break_target = loop_block;
    loop_scope.continue_target = .{ .@"break" = continue_block };
    loop_scope.setBreakResultInfo(block_ri);

    // done adding instructions to loop_scope, can now stack then_scope
    then_scope.instructions_top = then_scope.instructions.items.len;

    const then_node = while_full.ast.then_expr;
    if (opt_payload_inst.unwrap()) |payload_inst| {
        try then_scope.instructions.append(astgen.gpa, payload_inst);
    }
    if (dbg_var_name != .empty) try then_scope.addDbgVar(.dbg_var_val, dbg_var_name, dbg_var_inst);
    try then_scope.instructions.append(astgen.gpa, continue_block);
    // This code could be improved to avoid emitting the continue expr when there
    // are no jumps to it. This happens when the last statement of a while body is noreturn
    // and there are no `continue` statements.
    // Tracking issue: https://github.com/ziglang/zig/issues/9185
    if (while_full.ast.cont_expr.unwrap()) |cont_expr| {
        _ = try unusedResultExpr(&then_scope, then_sub_scope, cont_expr);
    }

    continue_scope.instructions_top = continue_scope.instructions.items.len;
    {
        try emitDbgNode(&continue_scope, then_node);
        const unused_result = try fullBodyExpr(&continue_scope, &continue_scope.base, .{ .rl = .none }, then_node, .allow_branch_hint);
        _ = try addEnsureResult(&continue_scope, unused_result, then_node);
    }
    try checkUsed(parent_gz, &then_scope.base, then_sub_scope);
    const break_tag: Zir.Inst.Tag = if (is_inline) .break_inline else .@"break";
    if (!continue_scope.endsWithNoReturn()) {
        astgen.advanceSourceCursor(tree.tokenStart(tree.lastToken(then_node)));
        try emitDbgStmt(parent_gz, .{ astgen.source_line - parent_gz.decl_line, astgen.source_column });
        _ = try parent_gz.add(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .dbg_empty_stmt,
                .small = undefined,
                .operand = undefined,
            } },
        });
        _ = try continue_scope.addBreak(break_tag, continue_block, .void_value);
    }
    try continue_scope.setBlockBody(continue_block);
    _ = try then_scope.addBreak(break_tag, cond_block, .void_value);

    var else_scope = parent_gz.makeSubBlock(&cond_scope.base);
    defer else_scope.unstack();

    if (while_full.ast.else_expr.unwrap()) |else_node| {
        const sub_scope = s: {
            if (while_full.error_token) |error_token| {
                const tag: Zir.Inst.Tag = if (payload_is_ref)
                    .err_union_code_ptr
                else
                    .err_union_code;
                const else_payload_inst = try else_scope.addUnNode(tag, cond.inst, while_full.ast.cond_expr);
                const ident_name = try astgen.identAsString(error_token);
                const ident_bytes = tree.tokenSlice(error_token);
                if (mem.eql(u8, ident_bytes, "_"))
                    break :s &else_scope.base;
                try astgen.detectLocalShadowing(&else_scope.base, ident_name, error_token, ident_bytes, .capture);
                payload_val_scope = .{
                    .parent = &else_scope.base,
                    .gen_zir = &else_scope,
                    .name = ident_name,
                    .inst = else_payload_inst,
                    .token_src = error_token,
                    .id_cat = .capture,
                };
                try else_scope.addDbgVar(.dbg_var_val, ident_name, else_payload_inst);
                break :s &payload_val_scope.base;
            } else {
                break :s &else_scope.base;
            }
        };
        // Disallow unlabeled control flow to this scope so that bare `continue`
        // and `break` control flow apply to outer loops; not this one.
        // Also disallow `continue` targeting the loop label.
        loop_scope.allow_unlabeled_control_flow = false;
        loop_scope.continue_target = .none;
        const else_result = try fullBodyExpr(&else_scope, sub_scope, loop_scope.break_result_info, else_node, .allow_branch_hint);
        if (is_statement) {
            _ = try addEnsureResult(&else_scope, else_result, else_node);
        }

        try checkUsed(parent_gz, &else_scope.base, sub_scope);
        if (!else_scope.endsWithNoReturn()) {
            _ = try else_scope.addBreakWithSrcNode(break_tag, loop_block, else_result, else_node);
        }
    } else {
        const result = try rvalue(&else_scope, ri, .void_value, node);
        _ = try else_scope.addBreak(break_tag, loop_block, result);
    }

    if (loop_scope.label) |some| {
        if (!some.used) {
            try astgen.appendErrorTok(some.token, "unused while loop label", .{});
        }
    }

    try setCondBrPayload(condbr, cond.bool_bit, &then_scope, &else_scope);

    const result = if (need_result_rvalue)
        try rvalue(parent_gz, ri, loop_block.toRef(), node)
    else
        loop_block.toRef();

    if (is_statement) {
        _ = try parent_gz.addUnNode(.ensure_result_used, result, node);
    }

    return result;
}

fn forExpr(
    parent_gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    for_full: Ast.full.For,
    is_statement: bool,
) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;

    if (for_full.label_token) |label_token| {
        try astgen.checkLabelRedefinition(scope, label_token);
    }

    const need_rl = astgen.nodes_need_rl.contains(node);
    const block_ri: ResultInfo = if (need_rl) ri else .{
        .rl = switch (ri.rl) {
            .ptr => .{ .ty = (try ri.rl.resultType(parent_gz, node)).? },
            .inferred_ptr => .none,
            else => ri.rl,
        },
        .ctx = ri.ctx,
    };
    // We need to call `rvalue` to write through to the pointer only if we had a
    // result pointer and aren't forwarding it.
    const LocTag = @typeInfo(ResultInfo.Loc).@"union".tag_type.?;
    const need_result_rvalue = @as(LocTag, block_ri.rl) != @as(LocTag, ri.rl);

    const is_inline = for_full.inline_token != null;
    if (parent_gz.is_comptime and is_inline) {
        try astgen.appendErrorTok(for_full.inline_token.?, "redundant inline keyword in comptime scope", .{});
    }
    const tree = astgen.tree;
    const gpa = astgen.gpa;

    // For counters, this is the start value; for indexables, this is the base
    // pointer that can be used with elem_ptr and similar instructions.
    // Special value `none` means that this is a counter and its start value is
    // zero, indicating that the main index counter can be used directly.
    const indexables = try gpa.alloc(Zir.Inst.Ref, for_full.ast.inputs.len);
    defer gpa.free(indexables);
    // elements of this array can be `none`, indicating no length check.
    const lens = try gpa.alloc([2]Zir.Inst.Ref, for_full.ast.inputs.len);
    defer gpa.free(lens);

    // We will use a single zero-based counter no matter how many indexables there are.
    const index_ptr = blk: {
        const alloc_tag: Zir.Inst.Tag = if (is_inline) .alloc_comptime_mut else .alloc;
        const index_ptr = try parent_gz.addUnNode(alloc_tag, .usize_type, node);
        // initialize to zero
        _ = try parent_gz.addPlNode(.store_node, node, Zir.Inst.Bin{
            .lhs = index_ptr,
            .rhs = .zero_usize,
        });
        break :blk index_ptr;
    };

    var any_len_checks = false;

    {
        var capture_token = for_full.payload_token;
        for (for_full.ast.inputs, indexables, lens) |input, *indexable_ref, *len_refs| {
            const capture_is_ref = tree.tokenTag(capture_token) == .asterisk;
            const ident_tok = capture_token + @intFromBool(capture_is_ref);
            const is_discard = mem.eql(u8, tree.tokenSlice(ident_tok), "_");

            if (is_discard and capture_is_ref) {
                return astgen.failTok(capture_token, "pointer modifier invalid on discard", .{});
            }
            // Skip over the comma, and on to the next capture (or the ending pipe character).
            capture_token = ident_tok + 2;

            try emitDbgNode(parent_gz, input);
            if (tree.nodeTag(input) == .for_range) {
                if (capture_is_ref) {
                    return astgen.failTok(ident_tok, "cannot capture reference to range", .{});
                }
                const start_node, const end_node = tree.nodeData(input).node_and_opt_node;
                const start_val = try expr(parent_gz, scope, .{ .rl = .{ .ty = .usize_type } }, start_node);

                const end_val = if (end_node.unwrap()) |end|
                    try expr(parent_gz, scope, .{ .rl = .{ .ty = .usize_type } }, end)
                else
                    .none;

                if (end_val == .none and is_discard) {
                    try astgen.appendErrorTok(ident_tok, "discard of unbounded counter", .{});
                }

                if (end_val == .none) {
                    len_refs.* = .{ .none, .none };
                } else {
                    any_len_checks = true;
                    len_refs.* = .{ start_val, end_val };
                }

                const start_is_zero = nodeIsTriviallyZero(tree, start_node);
                indexable_ref.* = if (start_is_zero) .none else start_val;
            } else {
                const indexable = try expr(parent_gz, scope, .{ .rl = .none }, input);

                any_len_checks = true;
                indexable_ref.* = indexable;
                len_refs.* = .{ indexable, .none };
            }
        }
    }

    if (!any_len_checks) {
        return astgen.failNode(node, "unbounded for loop", .{});
    }

    // We use a dedicated ZIR instruction to assert the lengths to assist with
    // nicer error reporting as well as fewer ZIR bytes emitted.
    const len: Zir.Inst.Ref = len: {
        const all_lens = @as([*]Zir.Inst.Ref, @ptrCast(lens))[0 .. lens.len * 2];
        const lens_len: u32 = @intCast(all_lens.len);
        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.MultiOp).@"struct".fields.len + lens_len);
        const len = try parent_gz.addPlNode(.for_len, node, Zir.Inst.MultiOp{
            .operands_len = lens_len,
        });
        appendRefsAssumeCapacity(astgen, all_lens);
        break :len len;
    };

    const loop_tag: Zir.Inst.Tag = if (is_inline) .block_inline else .loop;
    const loop_block = try parent_gz.makeBlockInst(loop_tag, node);
    try parent_gz.instructions.append(gpa, loop_block);

    var loop_scope = parent_gz.makeSubBlock(scope);
    loop_scope.is_inline = is_inline;
    loop_scope.setBreakResultInfo(block_ri);
    defer loop_scope.unstack();

    // We need to finish loop_scope later once we have the deferred refs from then_scope. However, the
    // load must be removed from instructions in the meantime or it appears to be part of parent_gz.
    const index = try loop_scope.addUnNode(.load, index_ptr, node);
    _ = loop_scope.instructions.pop();

    var cond_scope = parent_gz.makeSubBlock(&loop_scope.base);
    defer cond_scope.unstack();

    // Check the condition.
    const cond = try cond_scope.addPlNode(.cmp_lt, node, Zir.Inst.Bin{
        .lhs = index,
        .rhs = len,
    });

    const condbr_tag: Zir.Inst.Tag = if (is_inline) .condbr_inline else .condbr;
    const condbr = try cond_scope.addCondBr(condbr_tag, node);
    const block_tag: Zir.Inst.Tag = if (is_inline) .block_inline else .block;
    const cond_block = try loop_scope.makeBlockInst(block_tag, node);
    try cond_scope.setBlockBody(cond_block);

    if (for_full.label_token) |label_token| {
        loop_scope.label = .{ .token = label_token };
    }
    loop_scope.allow_unlabeled_control_flow = true;
    loop_scope.break_target = loop_block;
    loop_scope.continue_target = .{ .@"break" = cond_block };

    const then_node = for_full.ast.then_expr;
    var then_scope = parent_gz.makeSubBlock(&cond_scope.base);
    defer then_scope.unstack();

    const capture_scopes = try gpa.alloc(Scope.LocalVal, for_full.ast.inputs.len);
    defer gpa.free(capture_scopes);

    const then_sub_scope = blk: {
        var capture_token = for_full.payload_token;
        var capture_sub_scope: *Scope = &then_scope.base;
        for (for_full.ast.inputs, indexables, capture_scopes) |input, indexable_ref, *capture_scope| {
            const capture_is_ref = tree.tokenTag(capture_token) == .asterisk;
            const ident_tok = capture_token + @intFromBool(capture_is_ref);
            const capture_name = tree.tokenSlice(ident_tok);
            // Skip over the comma, and on to the next capture (or the ending pipe character).
            capture_token = ident_tok + 2;

            if (mem.eql(u8, capture_name, "_")) continue;

            const name_str_index = try astgen.identAsString(ident_tok);
            try astgen.detectLocalShadowing(capture_sub_scope, name_str_index, ident_tok, capture_name, .capture);

            const capture_inst = inst: {
                const is_counter = tree.nodeTag(input) == .for_range;

                if (indexable_ref == .none) {
                    // Special case: the main index can be used directly.
                    assert(is_counter);
                    assert(!capture_is_ref);
                    break :inst index;
                }

                // For counters, we add the index variable to the start value; for
                // indexables, we use it as an element index. This is so similar
                // that they can share the same code paths, branching only on the
                // ZIR tag.
                const switch_cond = (@as(u2, @intFromBool(capture_is_ref)) << 1) | @intFromBool(is_counter);
                const tag: Zir.Inst.Tag = switch (switch_cond) {
                    0b00 => .elem_val,
                    0b01 => .add,
                    0b10 => .elem_ptr,
                    0b11 => unreachable, // compile error emitted already
                };
                break :inst try then_scope.addPlNode(tag, input, Zir.Inst.Bin{
                    .lhs = indexable_ref,
                    .rhs = index,
                });
            };

            capture_scope.* = .{
                .parent = capture_sub_scope,
                .gen_zir = &then_scope,
                .name = name_str_index,
                .inst = capture_inst,
                .token_src = ident_tok,
                .id_cat = .capture,
            };

            try then_scope.addDbgVar(.dbg_var_val, name_str_index, capture_inst);
            capture_sub_scope = &capture_scope.base;
        }

        break :blk capture_sub_scope;
    };

    const then_result = try fullBodyExpr(&then_scope, then_sub_scope, .{ .rl = .none }, then_node, .allow_branch_hint);
    _ = try addEnsureResult(&then_scope, then_result, then_node);

    try checkUsed(parent_gz, &then_scope.base, then_sub_scope);

    astgen.advanceSourceCursor(tree.tokenStart(tree.lastToken(then_node)));
    try emitDbgStmt(parent_gz, .{ astgen.source_line - parent_gz.decl_line, astgen.source_column });
    _ = try parent_gz.add(.{
        .tag = .extended,
        .data = .{ .extended = .{
            .opcode = .dbg_empty_stmt,
            .small = undefined,
            .operand = undefined,
        } },
    });

    const break_tag: Zir.Inst.Tag = if (is_inline) .break_inline else .@"break";
    _ = try then_scope.addBreak(break_tag, cond_block, .void_value);

    var else_scope = parent_gz.makeSubBlock(&cond_scope.base);
    defer else_scope.unstack();

    if (for_full.ast.else_expr.unwrap()) |else_node| {
        const sub_scope = &else_scope.base;
        // Disallow unlabeled control flow to this scope so that bare `continue`
        // and `break` control flow apply to outer loops; not this one.
        // Also disallow `continue` targeting the loop label.
        loop_scope.allow_unlabeled_control_flow = false;
        loop_scope.continue_target = .none;
        const else_result = try fullBodyExpr(&else_scope, sub_scope, loop_scope.break_result_info, else_node, .allow_branch_hint);
        if (is_statement) {
            _ = try addEnsureResult(&else_scope, else_result, else_node);
        }
        if (!else_scope.endsWithNoReturn()) {
            _ = try else_scope.addBreakWithSrcNode(break_tag, loop_block, else_result, else_node);
        }
    } else {
        const result = try rvalue(&else_scope, ri, .void_value, node);
        _ = try else_scope.addBreak(break_tag, loop_block, result);
    }

    if (loop_scope.label) |some| {
        if (!some.used) {
            try astgen.appendErrorTok(some.token, "unused for loop label", .{});
        }
    }

    try setCondBrPayload(condbr, cond, &then_scope, &else_scope);

    // then_block and else_block unstacked now, can resurrect loop_scope to finally finish it
    {
        loop_scope.instructions_top = loop_scope.instructions.items.len;
        try loop_scope.instructions.appendSlice(gpa, &.{ index.toIndex().?, cond_block });

        // Increment the index variable.
        const index_plus_one = try loop_scope.addPlNode(.add_unsafe, node, Zir.Inst.Bin{
            .lhs = index,
            .rhs = .one_usize,
        });
        _ = try loop_scope.addPlNode(.store_node, node, Zir.Inst.Bin{
            .lhs = index_ptr,
            .rhs = index_plus_one,
        });

        const repeat_tag: Zir.Inst.Tag = if (is_inline) .repeat_inline else .repeat;
        _ = try loop_scope.addNode(repeat_tag, node);

        try loop_scope.setBlockBody(loop_block);
    }

    const result = if (need_result_rvalue)
        try rvalue(parent_gz, ri, loop_block.toRef(), node)
    else
        loop_block.toRef();

    if (is_statement) {
        _ = try parent_gz.addUnNode(.ensure_result_used, result, node);
    }
    return result;
}

const SwitchNonErr = union(enum) {
    /// A regular switch expression.
    /// Emits `switch_block[_ref]`.
    none,
    /// `eu catch |err| switch (err) { ... }`
    ///
    /// `switch` must not be labeled.
    /// Emits `switch_block_err_union`.
    @"catch",
    /// `if (eu) |payload| { ... } else |err| switch (err) { ... }`
    ///
    /// `switch` must not be labeled.
    /// Emits `switch_block_err_union`.
    @"if": Ast.full.If,
    /// `eu catch |err| label: switch (err) { ... }`
    /// `if (eu) |payload| { ... } else |err| label: switch (err) { ... }`
    ///
    /// `switch` must be labeled.
    /// Emits a `condbr` on the non-error body and a regular switch, though the
    /// non-error prong and all `break`s from switch prongs are peers.
    /// Exists to avoid a rather complex special case of `switch_block_err_union`.
    peer_break_target: struct {
        /// Refers to the enclosing block of the entire switch-on-err expression.
        block_inst: Zir.Inst.Index,
        /// Belongs to `block_inst`.
        block_ri: ResultInfo,
    },
};

fn switchExpr(
    parent_gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    switch_full: Ast.full.Switch,
    non_err: SwitchNonErr,
) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;
    const gpa = astgen.gpa;
    const tree = astgen.tree;

    const switch_node, const operand_node, const err_token = switch (non_err) {
        .none, .peer_break_target => .{
            node,
            switch_full.ast.condition,
            undefined,
        },
        .@"catch" => .{
            tree.nodeData(node).node_and_node[1],
            tree.nodeData(node).node_and_node[0],
            tree.nodeMainToken(node) + 2,
        },
        .@"if" => |if_full| .{
            if_full.ast.else_expr.unwrap().?,
            if_full.ast.cond_expr,
            if_full.error_token.?,
        },
    };
    const case_nodes = switch_full.ast.cases;

    const is_err_switch = non_err != .none;
    const needs_non_err_handling = switch (non_err) {
        .none => false,
        .peer_break_target => false, // handled by parent expression
        .@"catch", .@"if" => true,
    };

    const need_rl = astgen.nodes_need_rl.contains(node);
    const block_ri: ResultInfo = if (need_rl) ri else .{
        .rl = switch (ri.rl) {
            .ptr => .{ .ty = (try ri.rl.resultType(parent_gz, node)).? },
            .inferred_ptr => .none,
            else => ri.rl,
        },
        .ctx = ri.ctx,
    };

    // We need to call `rvalue` to write through to the pointer only if we had a
    // result pointer and aren't forwarding it.
    const LocTag = @typeInfo(ResultInfo.Loc).@"union".tag_type.?;
    const need_result_rvalue = @as(LocTag, block_ri.rl) != @as(LocTag, ri.rl);

    const catch_or_if_node = if (needs_non_err_handling) node else undefined;
    const do_err_trace = needs_non_err_handling and astgen.fn_block != null;
    const non_err_is_ref: enum { no, yes, yes_const } = switch (non_err) {
        .none, .peer_break_target => undefined,
        .@"catch" => switch (ri.rl) {
            .ref, .ref_coerced_ty => .yes,
            .ref_const => .yes_const,
            else => .no,
        },
        .@"if" => |if_full| if (if_full.payload_token != null and
            tree.tokenTag(if_full.payload_token.?) == .asterisk) .yes else .no,
    };

    if (switch_full.label_token) |label_token| {
        try astgen.checkLabelRedefinition(scope, label_token);
    }

    const err_capture_name: Zir.NullTerminatedString = if (needs_non_err_handling) blk: {
        const err_str = tree.tokenSlice(err_token);
        if (mem.eql(u8, err_str, "_")) {
            // This is fatal because we already know we're switching on the captured error.
            return astgen.failTok(err_token, "discard of error capture; omit it instead", .{});
        }
        const err_name = try astgen.identAsString(err_token);
        try astgen.detectLocalShadowing(scope, err_name, err_token, err_str, .capture);
        break :blk err_name;
    } else undefined;

    // We perform two passes over the AST. This first pass is to collect information
    // for the following variables, make note of the special prong AST node indices,
    // and bail out with a compile error if there are incompatible special prongs present.
    var any_payload_is_ref = false;
    var any_has_payload_capture = false;
    var any_has_tag_capture = false;
    var any_maybe_runtime_capture = false;
    var scalar_cases_len: u32 = 0;
    var multi_cases_len: u32 = 0;
    var total_items_len: usize = 0;
    var total_ranges_len: usize = 0;
    var else_case_node: Ast.Node.OptionalIndex = .none;
    var underscore_node: Ast.Node.OptionalIndex = .none;
    for (case_nodes) |case_node| {
        const case = tree.fullSwitchCase(case_node).?;
        if (case.payload_token) |payload_token| {
            const ident = if (tree.tokenTag(payload_token) == .asterisk) blk: {
                // Capturing errors by reference is never allowed, but as we will
                // check for this again later we will fail as late as possible.
                any_payload_is_ref = true;
                break :blk payload_token + 1;
            } else payload_token;

            if (!mem.eql(u8, tree.tokenSlice(ident), "_")) {
                any_has_payload_capture = true;

                // If we're capturing a union, its payload value cannot always be
                // comptime-known, even if its prong is inlined as inlining only
                // affects its enum tag.
                // This check isn't perfect, because for things like enums, the
                // entire capture *is* comptime-known for inline prongs! But such
                // knowledge requires semantic analysis.
                any_maybe_runtime_capture = true;
            }
            if (tree.tokenTag(ident + 1) == .comma) {
                any_has_tag_capture = true;

                if (case.inline_token == null) {
                    any_maybe_runtime_capture = true;
                }
            }
        }

        // Check for else prong.
        if (case.ast.values.len == 0) {
            if (else_case_node.unwrap()) |prev_case_node| {
                const prev_else_tok = tree.fullSwitchCase(prev_case_node).?.ast.arrow_token - 1;
                const else_tok = case.ast.arrow_token - 1;
                return astgen.failTokNotes(
                    else_tok,
                    "multiple else prongs in switch expression",
                    .{},
                    &.{try astgen.errNoteTok(prev_else_tok, "previous else prong here", .{})},
                );
            }
            else_case_node = case_node.toOptional();
            continue;
        }

        // Check for '_' prong and ranges.
        var case_has_ranges = false;
        for (case.ast.values) |val| {
            switch (tree.nodeTag(val)) {
                .switch_range => {
                    total_ranges_len += 1;
                    case_has_ranges = true;
                },
                .string_literal => return astgen.failNode(val, "cannot switch on strings", .{}),
                else => |tag| {
                    total_items_len += 1;
                    if (tag == .identifier and
                        mem.eql(u8, tree.tokenSlice(tree.nodeMainToken(val)), "_"))
                    {
                        if (is_err_switch) {
                            const case_src = case.ast.arrow_token - 1;
                            return astgen.failTokNotes(
                                case_src,
                                "'_' prong is not allowed when switching on errors",
                                .{},
                                &.{
                                    try astgen.errNoteTok(
                                        case_src,
                                        "consider using 'else'",
                                        .{},
                                    ),
                                },
                            );
                        }
                        if (underscore_node.unwrap()) |prev_src| {
                            return astgen.failNodeNotes(
                                val,
                                "multiple '_' prongs in switch expression",
                                .{},
                                &.{try astgen.errNoteNode(prev_src, "previous '_' prong here", .{})},
                            );
                        }
                        if (case.inline_token != null) {
                            return astgen.failNode(val, "cannot inline '_' prong", .{});
                        }
                        underscore_node = val.toOptional();
                    }
                },
            }
        }

        const case_len = case.ast.values.len;
        if (case_len == 1 and !case_has_ranges) {
            scalar_cases_len += 1;
        } else if (case_len >= 1) {
            multi_cases_len += 1;
        }
    }

    const has_else = else_case_node != .none;
    const has_under = underscore_node != .none;
    if (is_err_switch) assert(!has_under); // should have failed by now
    const any_ranges = total_ranges_len > 0;

    // This contains all of the body lengths (already in the correct order) and
    // the bodies they belong to that go into the `extra` array later, except the
    // first item_table_end slots are a table that indexes the item bodies (and
    // also indirectly the prong bodies, as they are always trailing after their
    // item bodies).
    const payloads = &astgen.scratch;
    const scratch_top = astgen.scratch.items.len;
    var payloads_end = scratch_top;

    // Since range item body pairs are always contiguous we don't technically
    // have to keep track of the position of the second body. However handling
    // all of the several indices and offsets is complicated enough as it is,
    // so for the sake of keeping this function a little bit more simple we do
    // it anyway.

    const scalar_body_table = payloads_end;
    payloads_end += scalar_cases_len;
    const multi_item_body_table = payloads_end;
    payloads_end += total_items_len + 2 * total_ranges_len - scalar_cases_len;
    const multi_prong_body_table = payloads_end;
    payloads_end += multi_cases_len;
    const body_table_end = payloads_end;

    const scalar_prong_infos_start = payloads_end;
    payloads_end += scalar_cases_len;
    const multi_prong_infos_start = payloads_end;
    payloads_end += multi_cases_len;
    const multi_case_items_lens_start = payloads_end;
    payloads_end += multi_cases_len;
    const multi_case_ranges_lens_start = if (any_ranges) blk: {
        const multi_case_ranges_lens_start = payloads_end;
        payloads_end += multi_cases_len;
        break :blk multi_case_ranges_lens_start;
    } else undefined;
    const scalar_item_infos_start = payloads_end;
    payloads_end += scalar_cases_len;
    const multi_items_infos_start = payloads_end;
    payloads_end += total_items_len - scalar_cases_len + 2 * total_ranges_len;
    const bodies_start = payloads_end;

    try payloads.resize(gpa, bodies_start);
    defer astgen.scratch.items.len = scratch_top;

    var non_err_prong_body_start: u32 = undefined;
    var else_prong_body_start: u32 = undefined;
    var non_err_info: Zir.Inst.SwitchBlock.ProngInfo.NonErr = undefined;
    var else_info: Zir.Inst.SwitchBlock.ProngInfo.Else = undefined;

    var block_scope = parent_gz.makeSubBlock(scope);
    // block_scope not used for collecting instructions
    block_scope.instructions_top = GenZir.unstacked_top;

    const operand_ri: ResultInfo = .{
        .rl = loc: {
            if (any_payload_is_ref) break :loc .ref;
            if (needs_non_err_handling and non_err_is_ref == .yes) break :loc .ref;
            if (needs_non_err_handling and non_err_is_ref == .yes_const) break :loc .ref_const;
            break :loc .none;
        },
        .ctx = if (do_err_trace) .error_handling_expr else .none,
    };

    astgen.advanceSourceCursorToNode(operand_node);
    const operand_lc: LineColumn = .{ astgen.source_line - parent_gz.decl_line, astgen.source_column };

    const raw_operand: Zir.Inst.Ref = if (needs_non_err_handling)
        try reachableExpr(parent_gz, scope, operand_ri, operand_node, switch_node)
    else
        try expr(parent_gz, scope, operand_ri, operand_node);

    // Sema expects a dbg_stmt immediately before any kind of switch_block inst.
    try emitDbgStmtForceCurrentIndex(parent_gz, operand_lc);
    // This gets added to the parent block later, after the item expressions.
    const switch_tag: Zir.Inst.Tag = switch (non_err) {
        .none, .peer_break_target => if (any_payload_is_ref) .switch_block_ref else .switch_block,
        .@"if", .@"catch" => .switch_block_err_union,
    };
    const switch_block = try parent_gz.makeBlockInst(switch_tag, switch_node);

    // Set `break` target if applicable; `continue` target may differ!
    switch (non_err) {
        .none => {
            if (switch_full.label_token != null) {
                block_scope.break_target = switch_block;
            }
            block_scope.setBreakResultInfo(block_ri);
        },
        .@"catch", .@"if" => {
            assert(switch_full.label_token == null); // use `peer_break_target` code path instead!
            block_scope.setBreakResultInfo(block_ri);
        },
        .peer_break_target => |peer_break_target| {

            // Special case; we have an error switch + label situation and we
            // want to generate this:
            // ```
            // %1 = block({
            //   %2 = is_non_err(%operand)
            //   %3 = condbr(%2, {
            //     %4 = err_union_payload_unsafe(%operand)
            //     %5 = break(%1, result) // targets enclosing `block`
            //   }, {
            //     %6 = err_union_code(%operand)
            //     %7 = switch_block(%6,
            //       { ... } => {
            //         %8 = break(%1, result) // targets enclosing `block`
            //       },
            //       { ... } => {
            //         %9 = switch_continue(%7, result) // targets `switch_block`
            //       },
            //     )
            //     %10 = break(%1, @void_value)
            //   })
            // })
            // ```
            // to ensure that the non-err case and the switch are only peers when
            // breaking from either, but not when continuing the switch. We use
            // this lowering to avoiding a rather complex special case in Sema.

            assert(switch_full.label_token != null); // use `switch_block_err_union` code path instead!
            assert(.block == astgen.instructions.items(.tag)[@intFromEnum(peer_break_target.block_inst)]);
            block_scope.break_target = peer_break_target.block_inst;
            block_scope.setBreakResultInfo(peer_break_target.block_ri);
        },
    }

    // We need a bunch of separate locations to store several capture values:
    // `... |err| switch (err) { else => |e| { ... } }` // `err` and `e`
    // `... => |payload, tag| { ... }` // `payload` and `tag`
    // and result types:
    // `foo => { ... }` // `foo` needs a result type
    // `... => continue :sw val` // `val` needs a result type
    // Some observations:
    // - If we just use the switch inst itself we don't need a placeholder!
    // - We can always tell for sure whether a capture exists. We also know
    //   that its existence implies that it has to be used.
    // - We can't know whether there are any `continue`s before analyzing all
    //   prong bodies. At that point we already need a result location. We do
    //   know whether there even *could* be any though by looking for a label.
    // - Sema wants a result location in `zirSwitchContinue`. If that's the
    //   switch inst itself, there's no need to look at the switch inst data.
    // Some conclusions:
    // - We should use the switch inst as the continue result location if needed.
    // - If we need more insts for captures and our switch inst is already used
    //   for something else, we start creating placeholder insts.

    // Prong items use the switch block instruction as their result type.
    // No other components of the switch statement are in scope while they are
    // being resolved, so this is never a problem.
    const item_ri: ResultInfo = .{ .rl = .{ .coerced_ty = switch_block.toRef() } };

    var switch_block_inst_is_occupied: bool = false;

    if (switch_full.label_token) |label_token| {
        block_scope.label = .{ .token = label_token };
        block_scope.continue_target = .{ .switch_continue = switch_block };
        block_scope.continue_result_info = .{
            .rl = if (any_payload_is_ref)
                .{ .ref_coerced_ty = switch_block.toRef() }
            else
                .{ .coerced_ty = switch_block.toRef() },
        };
        switch_block_inst_is_occupied = true;

        // `break_target` and `break_result_info` already set above.
    }
    if (needs_non_err_handling) {
        // `switch_block_err_union` uses the switch block inst as its err capture/
        // switch operand. This is always ok as its switch can never have a label.
        assert(!switch_block_inst_is_occupied);
        switch_block_inst_is_occupied = true;
    }
    // `... => |payload| { ... }`
    const payload_capture_inst, const payload_capture_inst_is_placeholder = inst: {
        if (!any_has_payload_capture) break :inst .{ undefined, false };
        if (!switch_block_inst_is_occupied) {
            switch_block_inst_is_occupied = true;
            break :inst .{ switch_block, false };
        }
        break :inst .{ try astgen.appendPlaceholder(), true };
    };
    // `... => |_, tag| { ... }`
    const tag_capture_inst, const tag_capture_inst_is_placeholder = inst: {
        if (!any_has_tag_capture) break :inst .{ undefined, false };
        if (!switch_block_inst_is_occupied) {
            switch_block_inst_is_occupied = true;
            break :inst .{ switch_block, false };
        }
        break :inst .{ try astgen.appendPlaceholder(), true };
    };

    var prong_body_extra_insts_buf: [3]Zir.Inst.Index = undefined;
    const prong_body_extra_insts: []const Zir.Inst.Index = extra_insts: {
        var extra_insts: std.ArrayList(Zir.Inst.Index) = .initBuffer(&prong_body_extra_insts_buf);
        if (switch_block_inst_is_occupied) extra_insts.appendAssumeCapacity(switch_block);
        if (payload_capture_inst_is_placeholder) extra_insts.appendAssumeCapacity(payload_capture_inst);
        if (tag_capture_inst_is_placeholder) extra_insts.appendAssumeCapacity(tag_capture_inst);
        break :extra_insts extra_insts.items;
    };

    const switch_operand, const catch_or_if_operand = if (needs_non_err_handling)
        .{ switch_block.toRef(), raw_operand }
    else
        .{ raw_operand, undefined };

    // We re-use this same scope for all case items and contents.
    var scratch_scope = parent_gz.makeSubBlock(&block_scope.base);
    scratch_scope.instructions_top = GenZir.unstacked_top;

    // We have to take care of the non-error body first if there is one.
    non_err_body: {
        if (!needs_non_err_handling) break :non_err_body;

        scratch_scope.instructions_top = parent_gz.instructions.items.len;
        defer scratch_scope.unstack();

        // It's always ok to use the switch block inst to refer to the error union
        // payload as the actual switch statement isn't even in scope yet.
        const non_err_payload_inst = switch_block;
        var non_err_capture: Zir.Inst.SwitchBlock.ProngInfo.Capture = .none;

        switch (non_err) {
            .none, .peer_break_target => unreachable,
            .@"catch" => {
                // We always effectively capture the error union payload; we use
                // it to `break` from the entire `switch_block_err_union`.
                non_err_capture = if (non_err_is_ref != .no) .by_ref else .by_val;

                const then_result = switch (ri.rl) {
                    .ref, .ref_const, .ref_coerced_ty => non_err_payload_inst.toRef(),
                    else => try rvalue(
                        &scratch_scope,
                        block_scope.break_result_info,
                        non_err_payload_inst.toRef(),
                        catch_or_if_node,
                    ),
                };
                _ = try scratch_scope.addBreakWithSrcNode(
                    .@"break",
                    switch_block,
                    then_result,
                    catch_or_if_node,
                );
            },
            .@"if" => |if_full| {
                var payload_val_scope: Scope.LocalVal = undefined;

                const then_node = if_full.ast.then_expr;
                const then_sub_scope: *Scope = scope: {
                    if (if_full.payload_token) |payload_token| {
                        const ident_token = payload_token + @intFromBool(non_err_is_ref != .no);
                        const ident_name = try astgen.identAsString(ident_token);
                        const ident_name_str = tree.tokenSlice(ident_token);
                        if (mem.eql(u8, "_", ident_name_str)) {
                            break :scope &scratch_scope.base;
                        }
                        non_err_capture = if (non_err_is_ref != .no) .by_ref else .by_val;
                        try astgen.detectLocalShadowing(&scratch_scope.base, ident_name, ident_token, ident_name_str, .capture);
                        payload_val_scope = .{
                            .parent = &scratch_scope.base,
                            .gen_zir = &scratch_scope,
                            .name = ident_name,
                            .inst = non_err_payload_inst.toRef(),
                            .token_src = ident_token,
                            .id_cat = .capture,
                        };
                        try scratch_scope.addDbgVar(.dbg_var_val, ident_name, non_err_payload_inst.toRef());
                        break :scope &payload_val_scope.base;
                    } else {
                        _ = try scratch_scope.addUnNode(
                            .ensure_err_union_payload_void,
                            catch_or_if_operand,
                            catch_or_if_node,
                        );
                        break :scope &scratch_scope.base;
                    }
                };
                const then_result = try fullBodyExpr(&scratch_scope, then_sub_scope, block_scope.break_result_info, then_node, .allow_branch_hint);
                try checkUsed(parent_gz, &scratch_scope.base, then_sub_scope);
                if (!scratch_scope.endsWithNoReturn()) {
                    _ = try scratch_scope.addBreakWithSrcNode(.@"break", switch_block, then_result, then_node);
                }
            },
        }
        const body_slice = scratch_scope.instructionsSlice();
        const body_start: u32 = @intCast(payloads.items.len);
        const body_len = astgen.countBodyLenAfterFixupsExtraRefs(body_slice, &.{non_err_payload_inst});
        try payloads.ensureUnusedCapacity(gpa, body_len);
        astgen.appendBodyWithFixupsExtraRefsArrayList(payloads, body_slice, &.{non_err_payload_inst});

        non_err_prong_body_start = body_start;
        non_err_info = .{
            .body_len = @intCast(body_len),
            .capture = non_err_capture,
            .operand_is_ref = non_err_is_ref != .no,
        };
    }

    // In this pass we generate all the item and prong expressions.
    var multi_case_index: u32 = 0;
    var scalar_case_index: u32 = 0;
    var multi_item_offset: usize = 0;
    for (case_nodes) |case_node| {
        const case = tree.fullSwitchCase(case_node).?;

        const ranges_len: u32 = if (any_ranges) blk: {
            var ranges_len: u32 = 0;
            for (case.ast.values) |value| {
                ranges_len += @intFromBool(tree.nodeTag(value) == .switch_range);
            }
            break :blk ranges_len;
        } else 0;
        const items_len: u32 = @intCast(case.ast.values.len - ranges_len);
        const is_multi_case = items_len > 1 or ranges_len > 0;

        // item/range bodies in order of occurence
        var item_i: usize = 0;
        var range_i: usize = 0;
        for (case.ast.values) |value| {
            const is_range = tree.nodeTag(value) == .switch_range;
            const range: [2]Ast.Node.Index = if (is_range) tree.nodeData(value).node_and_node else undefined;
            const nodes: []const Ast.Node.Index = if (is_range) &range else &.{value};
            for (nodes) |item| {
                // We lower enum literals, error values and number literals
                // manually to save space since they are very commonly used as
                // switch case items.
                const body_start: u32 = @intCast(payloads.items.len);
                const item_info: Zir.Inst.SwitchBlock.ItemInfo = blk: switch (tree.nodeTag(item)) {
                    .enum_literal => {
                        const str_index = try astgen.identAsString(tree.nodeMainToken(item));
                        break :blk .wrap(.{ .enum_literal = str_index });
                    },
                    .error_value => {
                        const ident_token = tree.nodeMainToken(item) + 2; // skip 'error', '.'
                        const str_index = try astgen.identAsString(ident_token);
                        break :blk .wrap(.{ .error_value = str_index });
                    },
                    else => if (value.toOptional() == underscore_node) {
                        break :blk .wrap(.under);
                    } else {
                        scratch_scope.instructions_top = parent_gz.instructions.items.len;
                        defer scratch_scope.unstack();
                        const item_result = try fullBodyExpr(&scratch_scope, scope, item_ri, item, .normal);
                        if (!scratch_scope.endsWithNoReturn()) {
                            _ = try scratch_scope.addBreakWithSrcNode(.break_inline, switch_block, item_result, item);
                        }
                        const item_slice = scratch_scope.instructionsSlice();
                        const body_len = astgen.countBodyLenAfterFixupsExtraRefs(item_slice, &.{switch_block});
                        try payloads.ensureUnusedCapacity(gpa, body_len);
                        astgen.appendBodyWithFixupsExtraRefsArrayList(payloads, item_slice, &.{switch_block});
                        break :blk .wrap(.{ .body_len = body_len });
                    },
                };
                if (is_multi_case) {
                    if (is_range) {
                        const offset = multi_item_offset + items_len + range_i;
                        payloads.items[multi_item_body_table + offset] = body_start;
                        payloads.items[multi_items_infos_start + offset] = @bitCast(item_info);
                        range_i += 1;
                    } else {
                        const offset = multi_item_offset + item_i;
                        payloads.items[multi_item_body_table + offset] = body_start;
                        payloads.items[multi_items_infos_start + offset] = @bitCast(item_info);
                        item_i += 1;
                    }
                } else {
                    payloads.items[scalar_body_table + scalar_case_index] = body_start;
                    payloads.items[scalar_item_infos_start + scalar_case_index] = @bitCast(item_info);
                }
            }
        }
        if (is_multi_case) {
            assert(item_i == items_len and range_i == 2 * ranges_len);
            payloads.items[multi_case_items_lens_start + multi_case_index] = items_len;
            if (any_ranges) {
                payloads.items[multi_case_ranges_lens_start + multi_case_index] = ranges_len;
            }
            multi_item_offset += items_len + 2 * ranges_len;
        }

        // Capture and prong body

        var dbg_var_payload_name: Zir.NullTerminatedString = .empty;
        var dbg_var_payload_inst: Zir.Inst.Ref = undefined;
        var dbg_var_tag_name: Zir.NullTerminatedString = .empty;
        var dbg_var_tag_inst: Zir.Inst.Ref = undefined;
        var has_tag_capture = false;
        var err_capture_scope: Scope.LocalVal = undefined;
        var payload_capture_scope: Scope.LocalVal = undefined;
        var tag_capture_scope: Scope.LocalVal = undefined;

        var capture: Zir.Inst.SwitchBlock.ProngInfo.Capture = .none;

        // Check all captures and make them available to the prong body.
        // Potential captures are:
        // - for regular switch: payload and tag
        // - for error switch: switch operand and payload
        const prong_body_scope: *Scope = scope: {
            const switch_scope: *Scope = if (needs_non_err_handling) blk: {
                // We want to have the captured error we're switching on in scope!
                err_capture_scope = .{
                    .parent = &scratch_scope.base,
                    .gen_zir = &scratch_scope,
                    .name = err_capture_name,
                    .inst = switch_operand,
                    .token_src = err_token,
                    .id_cat = .capture,
                };
                break :blk &err_capture_scope.base;
            } else &scratch_scope.base;

            const payload_token = case.payload_token orelse break :scope switch_scope;
            const capture_is_ref = tree.tokenTag(payload_token) == .asterisk;
            const ident = payload_token + @intFromBool(capture_is_ref);

            capture = if (capture_is_ref) .by_ref else .by_val;

            const ident_slice = tree.tokenSlice(ident);
            var payload_sub_scope: *Scope = undefined;
            if (mem.eql(u8, ident_slice, "_")) {
                if (capture_is_ref) {
                    // |*_, tag| is invalid, so we can fail early
                    return astgen.failTok(payload_token, "pointer modifier invalid on discard", .{});
                }
                capture = .none;
                payload_sub_scope = switch_scope;
            } else {
                const capture_name = try astgen.identAsString(ident);
                try astgen.detectLocalShadowing(switch_scope, capture_name, ident, ident_slice, .capture);
                payload_capture_scope = .{
                    .parent = switch_scope,
                    .gen_zir = &scratch_scope,
                    .name = capture_name,
                    .inst = payload_capture_inst.toRef(),
                    .token_src = ident,
                    .id_cat = .capture,
                };
                dbg_var_payload_name = payload_capture_scope.name;
                dbg_var_payload_inst = payload_capture_scope.inst;
                payload_sub_scope = &payload_capture_scope.base;
            }

            if (is_err_switch and capture == .by_ref) {
                return astgen.failTok(ident, "error set cannot be captured by reference", .{});
            }

            const tag_token = if (tree.tokenTag(ident + 1) == .comma) blk: {
                break :blk ident + 2;
            } else if (capture == .none) {
                // discarding the capture is only valid if the tag is captured
                // whether the tag capture is discarded is handled below
                return astgen.failTok(payload_token, "discard of capture; omit it instead", .{});
            } else break :scope payload_sub_scope;

            const tag_slice = tree.tokenSlice(tag_token);
            if (mem.eql(u8, tag_slice, "_")) {
                return astgen.failTok(tag_token, "discard of tag capture; omit it instead", .{});
            }
            const tag_name = try astgen.identAsString(tag_token);
            try astgen.detectLocalShadowing(payload_sub_scope, tag_name, tag_token, tag_slice, .@"switch tag capture");

            assert(any_has_tag_capture);
            has_tag_capture = true;

            if (is_err_switch) {
                return astgen.failTok(tag_token, "cannot capture tag of error union", .{});
            }

            tag_capture_scope = .{
                .parent = payload_sub_scope,
                .gen_zir = &scratch_scope,
                .name = tag_name,
                .inst = tag_capture_inst.toRef(),
                .token_src = tag_token,
                .id_cat = .@"switch tag capture",
            };
            dbg_var_tag_name = tag_capture_scope.name;
            dbg_var_tag_inst = tag_capture_scope.inst;
            break :scope &tag_capture_scope.base;
        };

        if (capture != .none) assert(any_has_payload_capture);
        if (is_err_switch) {
            assert(!any_payload_is_ref); // should have failed by now
            assert(!any_has_tag_capture); // should have failed by now
        }

        prong_body: {
            scratch_scope.instructions_top = parent_gz.instructions.items.len;
            defer scratch_scope.unstack();

            if (dbg_var_payload_name != .empty) {
                try scratch_scope.addDbgVar(.dbg_var_val, dbg_var_payload_name, dbg_var_payload_inst);
            }
            if (dbg_var_tag_name != .empty) {
                try scratch_scope.addDbgVar(.dbg_var_val, dbg_var_tag_name, dbg_var_tag_inst);
            }
            if (do_err_trace and nodeMayAppendToErrorTrace(tree, operand_node)) {
                _ = try scratch_scope.addSaveErrRetIndex(.always);
            }
            const target_expr_node = case.ast.target_expr;
            const case_result = try fullBodyExpr(&scratch_scope, prong_body_scope, block_scope.break_result_info, target_expr_node, .allow_branch_hint);
            if (needs_non_err_handling) {
                // If we would check `scratch_scope` here, we would get a false
                // positive, that being the switch operand itself!
                try checkUsed(parent_gz, &err_capture_scope.base, prong_body_scope);
            } else {
                try checkUsed(parent_gz, &scratch_scope.base, prong_body_scope);
            }
            if (!scratch_scope.endsWithNoReturn()) {
                // As our last action before the break, "pop" the error trace if needed
                if (do_err_trace) {
                    try restoreErrRetIndex(
                        &scratch_scope,
                        .{ .block = switch_block },
                        block_scope.break_result_info,
                        target_expr_node,
                        case_result,
                    );
                }
                _ = try scratch_scope.addBreakWithSrcNode(.@"break", switch_block, case_result, target_expr_node);
            }

            const body_slice = scratch_scope.instructionsSlice();
            const body_start: u32 = @intCast(payloads.items.len);
            const body_len = astgen.countBodyLenAfterFixupsExtraRefs(body_slice, prong_body_extra_insts);
            try payloads.ensureUnusedCapacity(gpa, body_len);
            astgen.appendBodyWithFixupsExtraRefsArrayList(payloads, body_slice, prong_body_extra_insts);

            if (case_node.toOptional() == else_case_node) {
                assert(case.ast.values.len == 0);

                // Specific `else` bodies can cause Sema to omit the
                // "unreachable else prong" error so that certain generic code
                // patterns don't trigger it. We do that for these bodies:
                // `else => unreachable,`
                // `else => return,`
                // `else => |e| return e,` (where `e` is any identifier)
                const is_simple_noreturn = switch (tree.nodeTag(target_expr_node)) {
                    .unreachable_literal => true, // `=> unreachable,`
                    .@"return" => simple_noreturn: {
                        const retval_node = tree.nodeData(target_expr_node).opt_node.unwrap() orelse {
                            break :simple_noreturn true; // `=> return,`
                        };
                        // Check for `=> |e| return e,`
                        if (capture != .by_val) break :simple_noreturn false;
                        if (tree.nodeTag(retval_node) != .identifier) break :simple_noreturn false;
                        const payload_name = try astgen.identAsString(case.payload_token.?);
                        const retval_name = try astgen.identAsString(tree.nodeMainToken(retval_node));
                        break :simple_noreturn payload_name == retval_name;
                    },
                    else => false,
                };

                else_info = .{
                    .body_len = @intCast(body_len),
                    .capture = capture,
                    .is_inline = case.inline_token != null,
                    .has_tag_capture = has_tag_capture,
                    .is_simple_noreturn = is_simple_noreturn,
                };
                else_prong_body_start = body_start;
                break :prong_body;
            }

            // We allow prongs with error items which are not inside the error set
            // being switched on if their body is `=> comptime unreachable,`.
            const is_comptime_unreach = comptime_unreach: {
                if (tree.nodeTag(target_expr_node) != .@"comptime") break :comptime_unreach false;
                const comptime_node = tree.nodeData(target_expr_node).node;
                break :comptime_unreach tree.nodeTag(comptime_node) == .unreachable_literal;
            };

            const prong_info: Zir.Inst.SwitchBlock.ProngInfo = .{
                .body_len = @intCast(body_len),
                .capture = capture,
                .is_inline = case.inline_token != null,
                .has_tag_capture = has_tag_capture,
                .is_comptime_unreach = is_comptime_unreach,
            };

            if (is_multi_case) {
                payloads.items[multi_prong_body_table + multi_case_index] = body_start;
                payloads.items[multi_prong_infos_start + multi_case_index] = @bitCast(prong_info);
                multi_case_index += 1;
            } else {
                // prong body start is implicit, it's right behind our only item.
                payloads.items[scalar_prong_infos_start + scalar_case_index] = @bitCast(prong_info);
                scalar_case_index += 1;
            }
        }
    }
    assert(scalar_case_index + multi_case_index + @intFromBool(has_else) == case_nodes.len);
    assert(multi_items_infos_start + multi_item_offset == bodies_start);

    if (switch_full.label_token) |label_token| if (!block_scope.label.?.used) {
        try astgen.appendErrorTok(label_token, "unused switch label", .{});
    };

    // Now that the item expressions are generated we can add this.
    try parent_gz.instructions.append(gpa, switch_block);

    // We've collected all of the data we need! Now we just have to finalize it
    // by copying our bodies from `payloads` to `extra`, this time in the order
    // expected by ZIR consumers.

    try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.SwitchBlock).@"struct".fields.len +
        @intFromBool(multi_cases_len > 0) + // multi_cases_len
        @intFromBool(payload_capture_inst_is_placeholder) + // payload_capture_placeholder
        @intFromBool(tag_capture_inst_is_placeholder) + // tag_capture_placeholder
        @intFromBool(needs_non_err_handling) + // catch_or_if_src_node_offset
        @intFromBool(needs_non_err_handling) + // non_err_info
        @intFromBool(has_else) + // else_info
        payloads.items.len - body_table_end); // item infos and bodies

    // singular pieces of data
    const zir_payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.SwitchBlock{
        .raw_operand = raw_operand,
        .bits = .{
            .has_multi_cases = multi_cases_len > 0,
            .any_ranges = any_ranges,
            .has_else = has_else,
            .has_under = has_under,
            .has_continue = switch_full.label_token != null and block_scope.label.?.used_for_continue,
            .any_maybe_runtime_capture = any_maybe_runtime_capture,
            .payload_capture_inst_is_placeholder = payload_capture_inst_is_placeholder,
            .tag_capture_inst_is_placeholder = tag_capture_inst_is_placeholder,
            .scalar_cases_len = @intCast(scalar_cases_len),
        },
    });
    astgen.instructions.items(.data)[@intFromEnum(switch_block)].pl_node.payload_index = zir_payload_index;

    if (multi_cases_len > 0) astgen.extra.appendAssumeCapacity(multi_cases_len);
    if (payload_capture_inst_is_placeholder) astgen.extra.appendAssumeCapacity(@intFromEnum(payload_capture_inst));
    if (tag_capture_inst_is_placeholder) astgen.extra.appendAssumeCapacity(@intFromEnum(tag_capture_inst));
    if (needs_non_err_handling) {
        const catch_or_if_src_node_offset = parent_gz.nodeIndexToRelative(catch_or_if_node);
        astgen.extra.appendAssumeCapacity(@bitCast(@intFromEnum(catch_or_if_src_node_offset)));
        astgen.extra.appendAssumeCapacity(@bitCast(non_err_info));
    }
    if (has_else) astgen.extra.appendAssumeCapacity(@bitCast(else_info));

    const extra_payloads_start = astgen.extra.items.len;

    // body lens
    astgen.extra.appendSliceAssumeCapacity(payloads.items[body_table_end..bodies_start]);

    // bodies
    if (needs_non_err_handling) {
        const body = payloads.items[non_err_prong_body_start..][0..non_err_info.body_len];
        astgen.extra.appendSliceAssumeCapacity(body);
    }
    if (has_else) {
        const body = payloads.items[else_prong_body_start..][0..else_info.body_len];
        astgen.extra.appendSliceAssumeCapacity(body);
    }
    for (0..scalar_cases_len) |scalar_i| {
        const item_info: Zir.Inst.SwitchBlock.ItemInfo = @bitCast(payloads.items[scalar_item_infos_start + scalar_i]);
        const item_body_start = payloads.items[scalar_body_table + scalar_i];
        const item_body = payloads.items[item_body_start..][0 .. item_info.bodyLen() orelse 0];
        const prong_info: Zir.Inst.SwitchBlock.ProngInfo = @bitCast(payloads.items[scalar_prong_infos_start + scalar_i]);
        const prong_body_start = item_body_start + item_body.len;
        const prong_body = payloads.items[prong_body_start..][0..prong_info.body_len];
        astgen.extra.appendSliceAssumeCapacity(prong_body);
        astgen.extra.appendSliceAssumeCapacity(item_body);
    }
    var multi_item_i: usize = 0;
    for (0..multi_cases_len) |multi_i| {
        const prong_body_start = payloads.items[multi_prong_body_table + multi_i];
        const prong_info: Zir.Inst.SwitchBlock.ProngInfo = @bitCast(payloads.items[multi_prong_infos_start + multi_i]);
        const prong_body = payloads.items[prong_body_start..][0..prong_info.body_len];
        astgen.extra.appendSliceAssumeCapacity(prong_body);

        const items_len = payloads.items[multi_case_items_lens_start + multi_i];
        const ranges_len = if (any_ranges) ranges_len: {
            break :ranges_len payloads.items[multi_case_ranges_lens_start + multi_i];
        } else 0;
        // The table entries and body lens are already in the correct order so we
        // don't have to differentiate between items and ranges here.
        for (0..items_len + 2 * ranges_len) |_| {
            const item_info: Zir.Inst.SwitchBlock.ItemInfo = @bitCast(payloads.items[multi_items_infos_start + multi_item_i]);
            if (item_info.bodyLen()) |body_len| {
                const body_start = payloads.items[multi_item_body_table + multi_item_i];
                const body = payloads.items[body_start..][0..body_len];
                astgen.extra.appendSliceAssumeCapacity(body);
            }
            multi_item_i += 1;
        }
    }

    // Make sure we didn't forget anything...
    assert(multi_item_i == total_items_len + 2 * total_ranges_len - scalar_cases_len);
    assert(astgen.extra.items.len - extra_payloads_start == payloads.items.len - body_table_end);

    if (need_result_rvalue) {
        return rvalue(parent_gz, ri, switch_block.toRef(), switch_node);
    } else {
        return switch_block.toRef();
    }
}

fn ret(gz: *GenZir, scope: *Scope, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    if (astgen.fn_block == null) {
        return astgen.failNode(node, "'return' outside function scope", .{});
    }

    if (gz.any_defer_node.unwrap()) |any_defer_node| {
        return astgen.failNodeNotes(node, "cannot return from defer expression", .{}, &.{
            try astgen.errNoteNode(
                any_defer_node,
                "defer expression here",
                .{},
            ),
        });
    }

    // Ensure debug line/column information is emitted for this return expression.
    // Then we will save the line/column so that we can emit another one that goes
    // "backwards" because we want to evaluate the operand, but then put the debug
    // info back at the return keyword for error return tracing.
    if (!gz.is_comptime) {
        try emitDbgNode(gz, node);
    }
    const ret_lc: LineColumn = .{ astgen.source_line - gz.decl_line, astgen.source_column };

    const defer_outer = &astgen.fn_block.?.base;

    const operand_node = tree.nodeData(node).opt_node.unwrap() orelse {
        // Returning a void value; skip error defers.
        try genDefers(gz, defer_outer, scope, .normal_only);

        // As our last action before the return, "pop" the error trace if needed
        _ = try gz.addRestoreErrRetIndex(.ret, .always, node);

        _ = try gz.addUnNode(.ret_node, .void_value, node);
        return Zir.Inst.Ref.unreachable_value;
    };

    if (tree.nodeTag(operand_node) == .error_value) {
        // Hot path for `return error.Foo`. This bypasses result location logic as well as logic
        // for detecting whether to add something to the function's inferred error set.
        const ident_token = tree.nodeMainToken(operand_node) + 2;
        const err_name_str_index = try astgen.identAsString(ident_token);
        const defer_counts = countDefers(defer_outer, scope);
        if (!defer_counts.need_err_code) {
            try genDefers(gz, defer_outer, scope, .both_sans_err);
            try emitDbgStmt(gz, ret_lc);
            _ = try gz.addStrTok(.ret_err_value, err_name_str_index, ident_token);
            return Zir.Inst.Ref.unreachable_value;
        }
        const err_code = try gz.addStrTok(.ret_err_value_code, err_name_str_index, ident_token);
        try genDefers(gz, defer_outer, scope, .{ .both = err_code });
        try emitDbgStmt(gz, ret_lc);
        _ = try gz.addUnNode(.ret_node, err_code, node);
        return Zir.Inst.Ref.unreachable_value;
    }

    const ri: ResultInfo = if (astgen.nodes_need_rl.contains(node)) .{
        .rl = .{ .ptr = .{ .inst = try gz.addNode(.ret_ptr, node) } },
        .ctx = .@"return",
    } else .{
        .rl = .{ .coerced_ty = astgen.fn_ret_ty },
        .ctx = .@"return",
    };
    const operand: Zir.Inst.Ref = try nameStratExpr(gz, scope, ri, operand_node, .func) orelse
        try reachableExpr(gz, scope, ri, operand_node, node);

    switch (nodeMayEvalToError(tree, operand_node)) {
        .never => {
            // Returning a value that cannot be an error; skip error defers.
            try genDefers(gz, defer_outer, scope, .normal_only);

            // As our last action before the return, "pop" the error trace if needed
            _ = try gz.addRestoreErrRetIndex(.ret, .always, node);

            try emitDbgStmt(gz, ret_lc);
            try gz.addRet(ri, operand, node);
            return Zir.Inst.Ref.unreachable_value;
        },
        .always => {
            // Value is always an error. Emit both error defers and regular defers.
            const err_code = if (ri.rl == .ptr) try gz.addUnNode(.load, ri.rl.ptr.inst, node) else operand;
            try genDefers(gz, defer_outer, scope, .{ .both = err_code });
            try emitDbgStmt(gz, ret_lc);
            try gz.addRet(ri, operand, node);
            return Zir.Inst.Ref.unreachable_value;
        },
        .maybe => {
            const defer_counts = countDefers(defer_outer, scope);
            if (!defer_counts.have_err) {
                // Only regular defers; no branch needed.
                try genDefers(gz, defer_outer, scope, .normal_only);
                try emitDbgStmt(gz, ret_lc);

                // As our last action before the return, "pop" the error trace if needed
                const result = if (ri.rl == .ptr) try gz.addUnNode(.load, ri.rl.ptr.inst, node) else operand;
                _ = try gz.addRestoreErrRetIndex(.ret, .{ .if_non_error = result }, node);

                try gz.addRet(ri, operand, node);
                return Zir.Inst.Ref.unreachable_value;
            }

            // Emit conditional branch for generating errdefers.
            const result = if (ri.rl == .ptr) try gz.addUnNode(.load, ri.rl.ptr.inst, node) else operand;
            const is_non_err = try gz.addUnNode(.ret_is_non_err, result, node);
            const condbr = try gz.addCondBr(.condbr, node);

            var then_scope = gz.makeSubBlock(scope);
            defer then_scope.unstack();

            try genDefers(&then_scope, defer_outer, scope, .normal_only);

            // As our last action before the return, "pop" the error trace if needed
            _ = try then_scope.addRestoreErrRetIndex(.ret, .always, node);

            try emitDbgStmt(&then_scope, ret_lc);
            try then_scope.addRet(ri, operand, node);

            var else_scope = gz.makeSubBlock(scope);
            defer else_scope.unstack();

            const which_ones: DefersToEmit = if (!defer_counts.need_err_code) .both_sans_err else .{
                .both = try else_scope.addUnNode(.err_union_code, result, node),
            };
            try genDefers(&else_scope, defer_outer, scope, which_ones);
            try emitDbgStmt(&else_scope, ret_lc);
            try else_scope.addRet(ri, operand, node);

            try setCondBrPayload(condbr, is_non_err, &then_scope, &else_scope);

            return Zir.Inst.Ref.unreachable_value;
        },
    }
}

/// Parses the string `buf` as a base 10 integer of type `u16`.
///
/// Unlike std.fmt.parseInt, does not allow the '_' character in `buf`.
fn parseBitCount(buf: []const u8) std.fmt.ParseIntError!u16 {
    if (buf.len == 0) return error.InvalidCharacter;

    var x: u16 = 0;

    for (buf) |c| {
        const digit = switch (c) {
            '0'...'9' => c - '0',
            else => return error.InvalidCharacter,
        };

        if (x != 0) x = try std.math.mul(u16, x, 10);
        x = try std.math.add(u16, x, digit);
    }

    return x;
}

const ComptimeBlockInfo = struct {
    src_node: Ast.Node.Index,
    reason: std.zig.SimpleComptimeReason,
};

fn identifier(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    ident: Ast.Node.Index,
    force_comptime: ?ComptimeBlockInfo,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const ident_token = tree.nodeMainToken(ident);
    const ident_name_raw = tree.tokenSlice(ident_token);
    if (mem.eql(u8, ident_name_raw, "_")) {
        return astgen.failNode(ident, "'_' used as an identifier without @\"_\" syntax", .{});
    }

    // if not @"" syntax, just use raw token slice
    if (ident_name_raw[0] != '@') {
        if (primitive_instrs.get(ident_name_raw)) |zir_const_ref| {
            return rvalue(gz, ri, zir_const_ref, ident);
        }

        if (ident_name_raw.len >= 2) integer: {
            // Keep in sync with logic in `comptimeExpr2`.
            const first_c = ident_name_raw[0];
            if (first_c == 'i' or first_c == 'u') {
                const signedness: std.builtin.Signedness = switch (first_c == 'i') {
                    true => .signed,
                    false => .unsigned,
                };
                if (ident_name_raw.len >= 3 and ident_name_raw[1] == '0') {
                    return astgen.failNode(
                        ident,
                        "primitive integer type '{s}' has leading zero",
                        .{ident_name_raw},
                    );
                }
                const bit_count = parseBitCount(ident_name_raw[1..]) catch |err| switch (err) {
                    error.Overflow => return astgen.failNode(
                        ident,
                        "primitive integer type '{s}' exceeds maximum bit width of 65535",
                        .{ident_name_raw},
                    ),
                    error.InvalidCharacter => break :integer,
                };
                const result = try gz.add(.{
                    .tag = .int_type,
                    .data = .{ .int_type = .{
                        .src_node = gz.nodeIndexToRelative(ident),
                        .signedness = signedness,
                        .bit_count = bit_count,
                    } },
                });
                return rvalue(gz, ri, result, ident);
            }
        }
    }

    // Local variables, including function parameters, and container-level declarations.

    if (force_comptime) |fc| {
        // Mirrors the logic at the end of `comptimeExpr2`.
        const block_inst = try gz.makeBlockInst(.block_comptime, fc.src_node);

        var comptime_gz = gz.makeSubBlock(scope);
        comptime_gz.is_comptime = true;
        defer comptime_gz.unstack();

        const sub_ri: ResultInfo = .{
            .ctx = ri.ctx,
            .rl = .none, // no point providing a result type, it won't change anything
        };
        const block_result = try localVarRef(&comptime_gz, scope, sub_ri, ident, ident_token);
        assert(!comptime_gz.endsWithNoReturn());
        _ = try comptime_gz.addBreak(.break_inline, block_inst, block_result);

        try comptime_gz.setBlockComptimeBody(block_inst, fc.reason);
        try gz.instructions.append(astgen.gpa, block_inst);

        return rvalue(gz, ri, block_inst.toRef(), fc.src_node);
    } else {
        return localVarRef(gz, scope, ri, ident, ident_token);
    }
}

fn localVarRef(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    ident: Ast.Node.Index,
    ident_token: Ast.TokenIndex,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const name_str_index = try astgen.identAsString(ident_token);
    var found_already: ?Ast.Node.Index = null; // we have found a decl with the same name already
    var found_needs_tunnel: bool = undefined; // defined when `found_already != null`
    var found_namespaces_out: u32 = undefined; // defined when `found_already != null`

    // The number of namespaces above `gz` we currently are
    var num_namespaces_out: u32 = 0;
    // defined by `num_namespaces_out != 0`
    var capturing_namespace: *Scope.Namespace = undefined;

    find_scope: switch (scope.unwrap()) {
        .local_val => |local_val| {
            if (local_val.name == name_str_index) {
                // Locals cannot shadow anything, so we do not need to look for ambiguous
                // references in this case.
                if (ri.rl == .discard and ri.ctx == .assignment) {
                    local_val.discarded = .fromToken(ident_token);
                } else {
                    local_val.used = .fromToken(ident_token);
                }

                if (local_val.is_used_or_discarded) |ptr| ptr.* = true;

                const value_inst = if (num_namespaces_out != 0) try tunnelThroughClosure(
                    gz,
                    ident,
                    num_namespaces_out,
                    .{ .ref = local_val.inst },
                    .{ .token = local_val.token_src },
                    name_str_index,
                ) else local_val.inst;

                return rvalueNoCoercePreRef(gz, ri, value_inst, ident);
            }
            continue :find_scope local_val.parent.unwrap();
        },
        .local_ptr => |local_ptr| {
            if (local_ptr.name == name_str_index) {
                if (ri.rl == .discard and ri.ctx == .assignment) {
                    local_ptr.discarded = .fromToken(ident_token);
                } else {
                    local_ptr.used = .fromToken(ident_token);
                }

                if (!local_ptr.maybe_comptime and !gz.is_typeof) {
                    if (num_namespaces_out != 0) {
                        const ident_name = try astgen.identifierTokenString(ident_token);
                        return astgen.failNodeNotes(ident, "mutable '{s}' not accessible from here", .{ident_name}, &.{
                            try astgen.errNoteTok(local_ptr.token_src, "declared mutable here", .{}),
                            try astgen.errNoteNode(capturing_namespace.node, "crosses namespace boundary here", .{}),
                        });
                    } else if (ri.ctx == .return_addrof) {
                        const ident_name = try astgen.identifierTokenString(ident_token);
                        return astgen.failNodeNotes(ident, "returning address of expired local variable '{s}'", .{ident_name}, &.{
                            try astgen.errNoteTok(local_ptr.token_src, "declared runtime-known here", .{}),
                        });
                    }
                }

                switch (ri.rl) {
                    .ref, .ref_const, .ref_coerced_ty => {
                        const ptr_inst = if (num_namespaces_out != 0) try tunnelThroughClosure(
                            gz,
                            ident,
                            num_namespaces_out,
                            .{ .ref = local_ptr.ptr },
                            .{ .token = local_ptr.token_src },
                            name_str_index,
                        ) else local_ptr.ptr;
                        if (ri.rl != .ref_const) local_ptr.used_as_lvalue = true;
                        return ptr_inst;
                    },
                    else => {
                        const val_inst = if (num_namespaces_out != 0) try tunnelThroughClosure(
                            gz,
                            ident,
                            num_namespaces_out,
                            .{ .ref_load = local_ptr.ptr },
                            .{ .token = local_ptr.token_src },
                            name_str_index,
                        ) else try gz.addUnNode(.load, local_ptr.ptr, ident);
                        return rvalueNoCoercePreRef(gz, ri, val_inst, ident);
                    },
                }
            }
            continue :find_scope local_ptr.parent.unwrap();
        },
        .gen_zir => |gen_zir| continue :find_scope gen_zir.parent.unwrap(),
        .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
        .namespace => |ns| {
            if (ns.decls.get(name_str_index)) |i| {
                if (found_already) |f| {
                    return astgen.failNodeNotes(ident, "ambiguous reference", .{}, &.{
                        try astgen.errNoteNode(f, "declared here", .{}),
                        try astgen.errNoteNode(i, "also declared here", .{}),
                    });
                }
                // We found a match but must continue looking for ambiguous references to decls.
                found_already = i;
                found_needs_tunnel = ns.maybe_generic;
                found_namespaces_out = num_namespaces_out;
            }
            num_namespaces_out += 1;
            capturing_namespace = ns;
            continue :find_scope ns.parent.unwrap();
        },
        .top => break :find_scope,
    }
    if (found_already == null) {
        const ident_name = try astgen.identifierTokenString(ident_token);
        return astgen.failNode(ident, "use of undeclared identifier '{s}'", .{ident_name});
    }

    // Decl references happen by name rather than ZIR index so that when unrelated
    // decls are modified, ZIR code containing references to them can be unmodified.

    if (found_namespaces_out > 0 and found_needs_tunnel) {
        switch (ri.rl) {
            .ref, .ref_const, .ref_coerced_ty => return tunnelThroughClosure(
                gz,
                ident,
                found_namespaces_out,
                .{ .decl_ref = name_str_index },
                .{ .node = found_already.? },
                name_str_index,
            ),
            else => {
                const result = try tunnelThroughClosure(
                    gz,
                    ident,
                    found_namespaces_out,
                    .{ .decl_val = name_str_index },
                    .{ .node = found_already.? },
                    name_str_index,
                );
                return rvalueNoCoercePreRef(gz, ri, result, ident);
            },
        }
    }

    switch (ri.rl) {
        .ref, .ref_const, .ref_coerced_ty => return gz.addStrTok(.decl_ref, name_str_index, ident_token),
        else => {
            const result = try gz.addStrTok(.decl_val, name_str_index, ident_token);
            return rvalueNoCoercePreRef(gz, ri, result, ident);
        },
    }
}

/// Access a ZIR instruction through closure. May tunnel through arbitrarily
/// many namespaces, adding closure captures as required.
/// Returns the index of the `closure_get` instruction added to `gz`.
fn tunnelThroughClosure(
    gz: *GenZir,
    /// The node which references the value to be captured.
    inner_ref_node: Ast.Node.Index,
    /// The number of namespaces being tunnelled through. At least 1.
    num_tunnels: u32,
    /// The value being captured.
    value: union(enum) {
        ref: Zir.Inst.Ref,
        ref_load: Zir.Inst.Ref,
        decl_val: Zir.NullTerminatedString,
        decl_ref: Zir.NullTerminatedString,
    },
    /// The location of the value's declaration.
    decl_src: union(enum) {
        token: Ast.TokenIndex,
        node: Ast.Node.Index,
    },
    name_str_index: Zir.NullTerminatedString,
) !Zir.Inst.Ref {
    switch (value) {
        .ref => |v| if (v.toIndex() == null) return v, // trivial value; do not need tunnel
        .ref_load => |v| assert(v.toIndex() != null), // there are no constant pointer refs
        .decl_val, .decl_ref => {},
    }

    const astgen = gz.astgen;
    const gpa = astgen.gpa;

    // Otherwise we need a tunnel. First, figure out the path of namespaces we
    // are tunneling through. This is usually only going to be one or two, so
    // use an SFBA to optimize for the common case.
    var sfba = std.heap.stackFallback(@sizeOf(usize) * 2, astgen.arena);
    var intermediate_tunnels = try sfba.get().alloc(*Scope.Namespace, num_tunnels - 1);

    const root_ns = ns: {
        var i: usize = num_tunnels - 1;
        var scope: *Scope = gz.parent;
        while (i > 0) {
            if (scope.cast(Scope.Namespace)) |mid_ns| {
                i -= 1;
                intermediate_tunnels[i] = mid_ns;
            }
            scope = scope.parent().?;
        }
        while (true) {
            if (scope.cast(Scope.Namespace)) |ns| break :ns ns;
            scope = scope.parent().?;
        }
    };

    // Now that we know the scopes we're tunneling through, begin adding
    // captures as required, starting with the outermost namespace.
    const root_capture: Zir.Inst.Capture = .wrap(switch (value) {
        .ref => |v| .{ .instruction = v.toIndex().? },
        .ref_load => |v| .{ .instruction_load = v.toIndex().? },
        .decl_val => |str| .{ .decl_val = str },
        .decl_ref => |str| .{ .decl_ref = str },
    });

    const root_gop = try root_ns.captures.getOrPut(gpa, root_capture);
    root_gop.value_ptr.* = name_str_index;
    var cur_capture_index = std.math.cast(u16, root_gop.index) orelse return astgen.failNodeNotes(
        root_ns.node,
        "this compiler implementation only supports up to 65536 captures per namespace",
        .{},
        &.{
            switch (decl_src) {
                .token => |t| try astgen.errNoteTok(t, "captured value here", .{}),
                .node => |n| try astgen.errNoteNode(n, "captured value here", .{}),
            },
            try astgen.errNoteNode(inner_ref_node, "value used here", .{}),
        },
    );

    for (intermediate_tunnels) |tunnel_ns| {
        const tunnel_gop = try tunnel_ns.captures.getOrPut(gpa, .wrap(.{ .nested = cur_capture_index }));
        tunnel_gop.value_ptr.* = name_str_index;
        cur_capture_index = std.math.cast(u16, tunnel_gop.index) orelse return astgen.failNodeNotes(
            tunnel_ns.node,
            "this compiler implementation only supports up to 65536 captures per namespace",
            .{},
            &.{
                switch (decl_src) {
                    .token => |t| try astgen.errNoteTok(t, "captured value here", .{}),
                    .node => |n| try astgen.errNoteNode(n, "captured value here", .{}),
                },
                try astgen.errNoteNode(inner_ref_node, "value used here", .{}),
            },
        );
    }

    // Incorporate the capture index into the source hash, so that changes in
    // the order of captures cause suitable re-analysis.
    astgen.src_hasher.update(std.mem.asBytes(&cur_capture_index));

    // Add an instruction to get the value from the closure.
    return gz.addExtendedNodeSmall(.closure_get, inner_ref_node, cur_capture_index);
}

fn stringLiteral(
    gz: *GenZir,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    const str_lit_token = tree.nodeMainToken(node);
    const str = try astgen.strLitAsString(str_lit_token);
    const result = try gz.add(.{
        .tag = .str,
        .data = .{ .str = .{
            .start = str.index,
            .len = str.len,
        } },
    });
    return rvalue(gz, ri, result, node);
}

fn multilineStringLiteral(
    gz: *GenZir,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const str = try astgen.strLitNodeAsString(node);
    const result = try gz.add(.{
        .tag = .str,
        .data = .{ .str = .{
            .start = str.index,
            .len = str.len,
        } },
    });
    return rvalue(gz, ri, result, node);
}

fn charLiteral(gz: *GenZir, ri: ResultInfo, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    const main_token = tree.nodeMainToken(node);
    const slice = tree.tokenSlice(main_token);

    switch (std.zig.parseCharLiteral(slice)) {
        .success => |codepoint| {
            const result = try gz.addInt(codepoint);
            return rvalue(gz, ri, result, node);
        },
        .failure => |err| return astgen.failWithStrLitError(err, main_token, slice, 0),
    }
}

const Sign = enum { negative, positive };

fn numberLiteral(gz: *GenZir, ri: ResultInfo, node: Ast.Node.Index, source_node: Ast.Node.Index, sign: Sign) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    const num_token = tree.nodeMainToken(node);
    const bytes = tree.tokenSlice(num_token);

    const result: Zir.Inst.Ref = switch (std.zig.parseNumberLiteral(bytes)) {
        .int => |num| switch (num) {
            0 => if (sign == .positive) .zero else return astgen.failTokNotes(
                num_token,
                "integer literal '-0' is ambiguous",
                .{},
                &.{
                    try astgen.errNoteTok(num_token, "use '0' for an integer zero", .{}),
                    try astgen.errNoteTok(num_token, "use '-0.0' for a floating-point signed zero", .{}),
                },
            ),
            1 => {
                // Handle the negation here!
                const result: Zir.Inst.Ref = switch (sign) {
                    .positive => .one,
                    .negative => .negative_one,
                };
                return rvalue(gz, ri, result, source_node);
            },
            else => try gz.addInt(num),
        },
        .big_int => |base| big: {
            const gpa = astgen.gpa;
            var big_int = try std.math.big.int.Managed.init(gpa);
            defer big_int.deinit();
            const prefix_offset: usize = if (base == .decimal) 0 else 2;
            big_int.setString(@intFromEnum(base), bytes[prefix_offset..]) catch |err| switch (err) {
                error.InvalidCharacter => unreachable, // caught in `parseNumberLiteral`
                error.InvalidBase => unreachable, // we only pass 16, 8, 2, see above
                error.OutOfMemory => return error.OutOfMemory,
            };

            const limbs = big_int.limbs[0..big_int.len()];
            assert(big_int.isPositive());
            break :big try gz.addIntBig(limbs);
        },
        .float => {
            const unsigned_float_number = std.fmt.parseFloat(f128, bytes) catch |err| switch (err) {
                error.InvalidCharacter => unreachable, // validated by tokenizer
            };
            const float_number = switch (sign) {
                .negative => -unsigned_float_number,
                .positive => unsigned_float_number,
            };
            // If the value fits into a f64 without losing any precision, store it that way.
            @setFloatMode(.strict);
            const smaller_float: f64 = @floatCast(float_number);
            const bigger_again: f128 = smaller_float;
            if (bigger_again == float_number) {
                const result = try gz.addFloat(smaller_float);
                return rvalue(gz, ri, result, source_node);
            }
            // We need to use 128 bits. Break the float into 4 u32 values so we can
            // put it into the `extra` array.
            const int_bits: u128 = @bitCast(float_number);
            const result = try gz.addPlNode(.float128, node, Zir.Inst.Float128{
                .piece0 = @truncate(int_bits),
                .piece1 = @truncate(int_bits >> 32),
                .piece2 = @truncate(int_bits >> 64),
                .piece3 = @truncate(int_bits >> 96),
            });
            return rvalue(gz, ri, result, source_node);
        },
        .failure => |err| return astgen.failWithNumberError(err, num_token, bytes),
    };

    if (sign == .positive) {
        return rvalue(gz, ri, result, source_node);
    } else {
        const negated = try gz.addUnNode(.negate, result, source_node);
        return rvalue(gz, ri, negated, source_node);
    }
}

fn failWithNumberError(astgen: *AstGen, err: std.zig.number_literal.Error, token: Ast.TokenIndex, bytes: []const u8) InnerError {
    const is_float = std.mem.findScalar(u8, bytes, '.') != null;
    switch (err) {
        .leading_zero => if (is_float) {
            return astgen.failTok(token, "number '{s}' has leading zero", .{bytes});
        } else {
            return astgen.failTokNotes(token, "number '{s}' has leading zero", .{bytes}, &.{
                try astgen.errNoteTok(token, "use '0o' prefix for octal literals", .{}),
            });
        },
        .digit_after_base => return astgen.failTok(token, "expected a digit after base prefix", .{}),
        .upper_case_base => |i| return astgen.failOff(token, @intCast(i), "base prefix must be lowercase", .{}),
        .invalid_float_base => |i| return astgen.failOff(token, @intCast(i), "invalid base for float literal", .{}),
        .repeated_underscore => |i| return astgen.failOff(token, @intCast(i), "repeated digit separator", .{}),
        .invalid_underscore_after_special => |i| return astgen.failOff(token, @intCast(i), "expected digit before digit separator", .{}),
        .invalid_digit => |info| return astgen.failOff(token, @intCast(info.i), "invalid digit '{c}' for {s} base", .{ bytes[info.i], @tagName(info.base) }),
        .invalid_digit_exponent => |i| return astgen.failOff(token, @intCast(i), "invalid digit '{c}' in exponent", .{bytes[i]}),
        .duplicate_exponent => |i| return astgen.failOff(token, @intCast(i), "duplicate exponent", .{}),
        .exponent_after_underscore => |i| return astgen.failOff(token, @intCast(i), "expected digit before exponent", .{}),
        .special_after_underscore => |i| return astgen.failOff(token, @intCast(i), "expected digit before '{c}'", .{bytes[i]}),
        .trailing_special => |i| return astgen.failOff(token, @intCast(i), "expected digit after '{c}'", .{bytes[i - 1]}),
        .trailing_underscore => |i| return astgen.failOff(token, @intCast(i), "trailing digit separator", .{}),
        .duplicate_period => unreachable, // Validated by tokenizer
        .invalid_character => unreachable, // Validated by tokenizer
        .invalid_exponent_sign => |i| {
            assert(bytes.len >= 2 and bytes[0] == '0' and bytes[1] == 'x'); // Validated by tokenizer
            return astgen.failOff(token, @intCast(i), "sign '{c}' cannot follow digit '{c}' in hex base", .{ bytes[i], bytes[i - 1] });
        },
        .period_after_exponent => |i| return astgen.failOff(token, @intCast(i), "unexpected period after exponent", .{}),
    }
}

fn asmExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    full: Ast.full.Asm,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const TagAndTmpl = struct { tag: Zir.Inst.Extended, tmpl: Zir.NullTerminatedString };
    const tag_and_tmpl: TagAndTmpl = switch (tree.nodeTag(full.ast.template)) {
        .string_literal => .{
            .tag = .@"asm",
            .tmpl = (try astgen.strLitAsString(tree.nodeMainToken(full.ast.template))).index,
        },
        .multiline_string_literal => .{
            .tag = .@"asm",
            .tmpl = (try astgen.strLitNodeAsString(full.ast.template)).index,
        },
        else => .{
            .tag = .asm_expr,
            .tmpl = @enumFromInt(@intFromEnum(try comptimeExpr(gz, scope, .{ .rl = .none }, full.ast.template, .inline_assembly_code))),
        },
    };

    // See https://github.com/ziglang/zig/issues/215 and related issues discussing
    // possible inline assembly improvements. Until then here is status quo AstGen
    // for assembly syntax. It's used by std lib crypto aesni.zig.
    const is_container_asm = astgen.fn_block == null;
    if (is_container_asm) {
        if (full.volatile_token) |t|
            return astgen.failTok(t, "volatile is
```
