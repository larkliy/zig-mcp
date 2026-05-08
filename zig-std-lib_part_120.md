```
 },
                    .inferred_ptr => |ptr_inst| .{ .inferred_ptr = ptr_inst },
                    .discard => .discard,
                } };
                _ = try expr(gz, scope, elem_ri, elem_init);
            }
            return .void_value;
        },
    }
}

/// An array initialization expression using an `array_init_anon` instruction.
fn arrayInitExprAnon(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    elements: []const Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;

    const payload_index = try addExtra(astgen, Zir.Inst.MultiOp{
        .operands_len = @intCast(elements.len),
    });
    var extra_index = try reserveExtra(astgen, elements.len);

    for (elements) |elem_init| {
        const elem_ref = try expr(gz, scope, .{ .rl = .none }, elem_init);
        astgen.extra.items[extra_index] = @intFromEnum(elem_ref);
        extra_index += 1;
    }
    return try gz.addPlNodePayloadIndex(.array_init_anon, node, payload_index);
}

/// An array initialization expression using an `array_init` or `array_init_ref` instruction.
fn arrayInitExprTyped(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    elements: []const Ast.Node.Index,
    ty_inst: Zir.Inst.Ref,
    maybe_elem_ty_inst: Zir.Inst.Ref,
    is_ref: bool,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;

    const len = elements.len + 1; // +1 for type
    const payload_index = try addExtra(astgen, Zir.Inst.MultiOp{
        .operands_len = @intCast(len),
    });
    var extra_index = try reserveExtra(astgen, len);
    astgen.extra.items[extra_index] = @intFromEnum(ty_inst);
    extra_index += 1;

    if (maybe_elem_ty_inst != .none) {
        const elem_ri: ResultInfo = .{ .rl = .{ .coerced_ty = maybe_elem_ty_inst } };
        for (elements) |elem_init| {
            const elem_inst = try expr(gz, scope, elem_ri, elem_init);
            astgen.extra.items[extra_index] = @intFromEnum(elem_inst);
            extra_index += 1;
        }
    } else {
        for (elements, 0..) |elem_init, i| {
            const ri: ResultInfo = .{ .rl = .{ .coerced_ty = try gz.add(.{
                .tag = .array_init_elem_type,
                .data = .{ .bin = .{
                    .lhs = ty_inst,
                    .rhs = @enumFromInt(i),
                } },
            }) } };

            const elem_inst = try expr(gz, scope, ri, elem_init);
            astgen.extra.items[extra_index] = @intFromEnum(elem_inst);
            extra_index += 1;
        }
    }

    const tag: Zir.Inst.Tag = if (is_ref) .array_init_ref else .array_init;
    return try gz.addPlNodePayloadIndex(tag, node, payload_index);
}

/// An array initialization expression using element pointers.
fn arrayInitExprPtr(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    elements: []const Ast.Node.Index,
    ptr_inst: Zir.Inst.Ref,
) InnerError!void {
    const astgen = gz.astgen;

    const array_ptr_inst = try gz.addUnNode(.opt_eu_base_ptr_init, ptr_inst, node);

    const payload_index = try addExtra(astgen, Zir.Inst.Block{
        .body_len = @intCast(elements.len),
    });
    var extra_index = try reserveExtra(astgen, elements.len);

    for (elements, 0..) |elem_init, i| {
        const elem_ptr_inst = try gz.addPlNode(.array_init_elem_ptr, elem_init, Zir.Inst.ElemPtrImm{
            .ptr = array_ptr_inst,
            .index = @intCast(i),
        });
        astgen.extra.items[extra_index] = @intFromEnum(elem_ptr_inst.toIndex().?);
        extra_index += 1;
        _ = try expr(gz, scope, .{ .rl = .{ .ptr = .{ .inst = elem_ptr_inst } } }, elem_init);
    }

    _ = try gz.addPlNodePayloadIndex(.validate_ptr_array_init, node, payload_index);
}

fn structInitExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    struct_init: Ast.full.StructInit,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    if (struct_init.ast.type_expr == .none) {
        if (struct_init.ast.fields.len == 0) {
            // Anonymous init with no fields.
            switch (ri.rl) {
                .discard => return .void_value,
                .ref_coerced_ty => |ptr_ty_inst| return gz.addUnNode(.struct_init_empty_ref_result, ptr_ty_inst, node),
                .ty, .coerced_ty => |ty_inst| return gz.addUnNode(.struct_init_empty_result, ty_inst, node),
                .ptr => {
                    // TODO: should we modify this to use RLS for the field stores here?
                    const ty_inst = (try ri.rl.resultType(gz, node)).?;
                    const val = try gz.addUnNode(.struct_init_empty_result, ty_inst, node);
                    return rvalue(gz, ri, val, node);
                },
                .none, .ref, .ref_const, .inferred_ptr => {
                    return rvalue(gz, ri, .empty_tuple, node);
                },
                .destructure => |destructure| {
                    return astgen.failNodeNotes(node, "empty initializer cannot be destructured", .{}, &.{
                        try astgen.errNoteNode(destructure.src_node, "result destructured here", .{}),
                    });
                },
            }
        }
    } else array: {
        const type_expr = struct_init.ast.type_expr.unwrap().?;
        const array_type: Ast.full.ArrayType = tree.fullArrayType(type_expr) orelse {
            if (struct_init.ast.fields.len == 0) {
                const ty_inst = try typeExpr(gz, scope, type_expr);
                const result = try gz.addUnNode(.struct_init_empty, ty_inst, node);
                return rvalue(gz, ri, result, node);
            }
            break :array;
        };
        const is_inferred_array_len = tree.nodeTag(array_type.ast.elem_count) == .identifier and
            // This intentionally does not support `@"_"` syntax.
            mem.eql(u8, tree.tokenSlice(tree.nodeMainToken(array_type.ast.elem_count)), "_");
        if (struct_init.ast.fields.len == 0) {
            if (is_inferred_array_len) {
                const elem_type = try typeExpr(gz, scope, array_type.ast.elem_type);
                const array_type_inst = if (array_type.ast.sentinel == .none) blk: {
                    break :blk try gz.addPlNode(.array_type, type_expr, Zir.Inst.Bin{
                        .lhs = .zero_usize,
                        .rhs = elem_type,
                    });
                } else blk: {
                    const sentinel_node = array_type.ast.sentinel.unwrap().?;
                    const sentinel = try comptimeExpr(gz, scope, .{ .rl = .{ .ty = elem_type } }, sentinel_node, .array_sentinel);
                    break :blk try gz.addPlNode(
                        .array_type_sentinel,
                        type_expr,
                        Zir.Inst.ArrayTypeSentinel{
                            .len = .zero_usize,
                            .elem_type = elem_type,
                            .sentinel = sentinel,
                        },
                    );
                };
                const result = try gz.addUnNode(.struct_init_empty, array_type_inst, node);
                return rvalue(gz, ri, result, node);
            }
            const ty_inst = try typeExpr(gz, scope, type_expr);
            const result = try gz.addUnNode(.struct_init_empty, ty_inst, node);
            return rvalue(gz, ri, result, node);
        } else {
            return astgen.failNode(
                type_expr,
                "initializing array with struct syntax",
                .{},
            );
        }
    }

    {
        var sfba = std.heap.stackFallback(256, astgen.arena);
        const sfba_allocator = sfba.get();

        var duplicate_names: std.array_hash_map.Auto(Zir.NullTerminatedString, ArrayList(Ast.TokenIndex)) = .empty;
        try duplicate_names.ensureTotalCapacity(sfba_allocator, @intCast(struct_init.ast.fields.len));

        // When there aren't errors, use this to avoid a second iteration.
        var any_duplicate = false;

        for (struct_init.ast.fields) |field| {
            const name_token = tree.firstToken(field) - 2;
            const name_index = try astgen.identAsString(name_token);

            const gop = try duplicate_names.getOrPut(sfba_allocator, name_index);

            if (gop.found_existing) {
                try gop.value_ptr.append(sfba_allocator, name_token);
                any_duplicate = true;
            } else {
                gop.value_ptr.* = .empty;
                try gop.value_ptr.append(sfba_allocator, name_token);
            }
        }

        if (any_duplicate) {
            var it = duplicate_names.iterator();

            while (it.next()) |entry| {
                const record = entry.value_ptr.*;
                if (record.items.len > 1) {
                    var error_notes = std.array_list.Managed(u32).init(astgen.arena);

                    for (record.items[1..]) |duplicate| {
                        try error_notes.append(try astgen.errNoteTok(duplicate, "duplicate name here", .{}));
                    }

                    try error_notes.append(try astgen.errNoteNode(node, "struct declared here", .{}));

                    try astgen.appendErrorTokNotes(
                        record.items[0],
                        "duplicate struct field name",
                        .{},
                        error_notes.items,
                    );
                }
            }

            return error.AnalysisFail;
        }
    }

    if (struct_init.ast.type_expr.unwrap()) |type_expr| {
        // Typed inits do not use RLS for language simplicity.
        const ty_inst = try typeExpr(gz, scope, type_expr);
        _ = try gz.addUnNode(.validate_struct_init_ty, ty_inst, node);
        switch (ri.rl) {
            .ref, .ref_const => return structInitExprTyped(gz, scope, node, struct_init, ty_inst, true),
            else => {
                const struct_inst = try structInitExprTyped(gz, scope, node, struct_init, ty_inst, false);
                return rvalue(gz, ri, struct_inst, node);
            },
        }
    }

    switch (ri.rl) {
        .none => return structInitExprAnon(gz, scope, node, struct_init),
        .discard => {
            // Even if discarding we must perform side-effects.
            for (struct_init.ast.fields) |field_init| {
                _ = try expr(gz, scope, .{ .rl = .discard }, field_init);
            }
            return .void_value;
        },
        .ref, .ref_const => {
            const result = try structInitExprAnon(gz, scope, node, struct_init);
            return gz.addUnTok(.ref, result, tree.firstToken(node));
        },
        .ref_coerced_ty => |ptr_ty_inst| {
            const result_ty_inst = try gz.addUnNode(.elem_type, ptr_ty_inst, node);
            _ = try gz.addUnNode(.validate_struct_init_result_ty, result_ty_inst, node);
            return structInitExprTyped(gz, scope, node, struct_init, result_ty_inst, true);
        },
        .ty, .coerced_ty => |result_ty_inst| {
            _ = try gz.addUnNode(.validate_struct_init_result_ty, result_ty_inst, node);
            return structInitExprTyped(gz, scope, node, struct_init, result_ty_inst, false);
        },
        .ptr => |ptr| {
            try structInitExprPtr(gz, scope, node, struct_init, ptr.inst);
            return .void_value;
        },
        .inferred_ptr => {
            // We can't get field pointers of an untyped inferred alloc, so must perform a
            // standard anonymous initialization followed by an rvalue store.
            // See corresponding logic in arrayInitExpr.
            const struct_inst = try structInitExprAnon(gz, scope, node, struct_init);
            return rvalue(gz, ri, struct_inst, node);
        },
        .destructure => |destructure| {
            // This is an untyped init, so is an actual struct, which does
            // not support destructuring.
            return astgen.failNodeNotes(node, "struct value cannot be destructured", .{}, &.{
                try astgen.errNoteNode(destructure.src_node, "result destructured here", .{}),
            });
        },
    }
}

/// A struct initialization expression using a `struct_init_anon` instruction.
fn structInitExprAnon(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    struct_init: Ast.full.StructInit,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const payload_index = try addExtra(astgen, Zir.Inst.StructInitAnon{
        .abs_node = node,
        .abs_line = astgen.source_line,
        .fields_len = @intCast(struct_init.ast.fields.len),
    });
    const field_size = @typeInfo(Zir.Inst.StructInitAnon.Item).@"struct".fields.len;
    var extra_index: usize = try reserveExtra(astgen, struct_init.ast.fields.len * field_size);

    for (struct_init.ast.fields) |field_init| {
        const name_token = tree.firstToken(field_init) - 2;
        const str_index = try astgen.identAsString(name_token);
        setExtra(astgen, extra_index, Zir.Inst.StructInitAnon.Item{
            .field_name = str_index,
            .init = try expr(gz, scope, .{ .rl = .none }, field_init),
        });
        extra_index += field_size;
    }

    return gz.addPlNodePayloadIndex(.struct_init_anon, node, payload_index);
}

/// A struct initialization expression using a `struct_init` or `struct_init_ref` instruction.
fn structInitExprTyped(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    struct_init: Ast.full.StructInit,
    ty_inst: Zir.Inst.Ref,
    is_ref: bool,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const payload_index = try addExtra(astgen, Zir.Inst.StructInit{
        .abs_node = node,
        .abs_line = astgen.source_line,
        .fields_len = @intCast(struct_init.ast.fields.len),
    });
    const field_size = @typeInfo(Zir.Inst.StructInit.Item).@"struct".fields.len;
    var extra_index: usize = try reserveExtra(astgen, struct_init.ast.fields.len * field_size);

    for (struct_init.ast.fields) |field_init| {
        const name_token = tree.firstToken(field_init) - 2;
        const str_index = try astgen.identAsString(name_token);
        const field_ty_inst = try gz.addPlNode(.struct_init_field_type, field_init, Zir.Inst.FieldType{
            .container_type = ty_inst,
            .name_start = str_index,
        });
        setExtra(astgen, extra_index, Zir.Inst.StructInit.Item{
            .field_type = field_ty_inst.toIndex().?,
            .init = try expr(gz, scope, .{ .rl = .{ .coerced_ty = field_ty_inst } }, field_init),
        });
        extra_index += field_size;
    }

    const tag: Zir.Inst.Tag = if (is_ref) .struct_init_ref else .struct_init;
    return gz.addPlNodePayloadIndex(tag, node, payload_index);
}

/// A struct initialization expression using field pointers.
fn structInitExprPtr(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    struct_init: Ast.full.StructInit,
    ptr_inst: Zir.Inst.Ref,
) InnerError!void {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const struct_ptr_inst = try gz.addUnNode(.opt_eu_base_ptr_init, ptr_inst, node);

    const payload_index = try addExtra(astgen, Zir.Inst.Block{
        .body_len = @intCast(struct_init.ast.fields.len),
    });
    var extra_index = try reserveExtra(astgen, struct_init.ast.fields.len);

    for (struct_init.ast.fields) |field_init| {
        const name_token = tree.firstToken(field_init) - 2;
        const str_index = try astgen.identAsString(name_token);
        const field_ptr = try gz.addPlNode(.struct_init_field_ptr, field_init, Zir.Inst.Field{
            .lhs = struct_ptr_inst,
            .field_name_start = str_index,
        });
        astgen.extra.items[extra_index] = @intFromEnum(field_ptr.toIndex().?);
        extra_index += 1;
        _ = try expr(gz, scope, .{ .rl = .{ .ptr = .{ .inst = field_ptr } } }, field_init);
    }

    _ = try gz.addPlNodePayloadIndex(.validate_ptr_struct_init, node, payload_index);
}

/// This explicitly calls expr in a comptime scope by wrapping it in a `block_comptime` if
/// necessary. It should be used whenever we need to force compile-time evaluation of something,
/// such as a type.
/// The function corresponding to `comptime` expression syntax is `comptimeExprAst`.
fn comptimeExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    reason: std.zig.SimpleComptimeReason,
) InnerError!Zir.Inst.Ref {
    return comptimeExpr2(gz, scope, ri, node, node, reason);
}

/// Like `comptimeExpr`, but draws a distinction between `node`, the expression to evaluate at comptime,
/// and `src_node`, the node to attach to the `block_comptime`.
fn comptimeExpr2(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    src_node: Ast.Node.Index,
    reason: std.zig.SimpleComptimeReason,
) InnerError!Zir.Inst.Ref {
    if (gz.is_comptime) {
        // No need to change anything!
        return expr(gz, scope, ri, node);
    }

    // There's an optimization here: if the body will be evaluated at comptime regardless, there's
    // no need to wrap it in a block. This is hard to determine in general, but we can identify a
    // common subset of trivially comptime expressions to take down the size of the ZIR a bit.
    const tree = gz.astgen.tree;
    switch (tree.nodeTag(node)) {
        .identifier => {
            // Many identifiers can be handled without a `block_comptime`, so `AstGen.identifier` has
            // special handling for this case.
            return identifier(gz, scope, ri, node, .{ .src_node = src_node, .reason = reason });
        },

        // These are leaf nodes which are always comptime-known.
        .number_literal,
        .char_literal,
        .string_literal,
        .multiline_string_literal,
        .enum_literal,
        .error_value,
        .anyframe_literal,
        .error_set_decl,
        // These nodes are not leaves, but will force comptime evaluation of all sub-expressions, and
        // hence behave the same regardless of whether they're in a comptime scope.
        .error_union,
        .merge_error_sets,
        .optional_type,
        .anyframe_type,
        .ptr_type_aligned,
        .ptr_type_sentinel,
        .ptr_type,
        .ptr_type_bit_range,
        .array_type,
        .array_type_sentinel,
        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
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
            // No need to worry about result location here, we're not creating a comptime block!
            return expr(gz, scope, ri, node);
        },

        // Lastly, for labelled blocks, avoid emitting a labelled block directly inside this
        // comptime block, because that would be silly! Note that we don't bother doing this for
        // unlabelled blocks, since they don't generate blocks at comptime anyway (see `blockExpr`).
        .block_two, .block_two_semicolon, .block, .block_semicolon => {
            const lbrace = tree.nodeMainToken(node);
            // Careful! We can't pass in the real result location here, since it may
            // refer to runtime memory. A runtime-to-comptime boundary has to remove
            // result location information, compute the result, and copy it to the true
            // result location at runtime. We do this below as well.
            const ty_only_ri: ResultInfo = .{
                .ctx = ri.ctx,
                .rl = if (try ri.rl.resultType(gz, node)) |res_ty|
                    .{ .coerced_ty = res_ty }
                else
                    .none,
            };
            if (tree.isTokenPrecededByTags(lbrace, &.{ .identifier, .colon })) {
                var buf: [2]Ast.Node.Index = undefined;
                const stmts = tree.blockStatements(&buf, node).?;

                // Replace result location and copy back later - see above.
                const block_ref = try labeledBlockExpr(gz, scope, ty_only_ri, node, stmts, true, .normal);
                return rvalue(gz, ri, block_ref, node);
            }
        },

        // In other cases, we don't optimize anything - we need a wrapper comptime block.
        else => {},
    }

    var block_scope = gz.makeSubBlock(scope);
    block_scope.is_comptime = true;
    defer block_scope.unstack();

    const block_inst = try gz.makeBlockInst(.block_comptime, src_node);
    // Replace result location and copy back later - see above.
    const ty_only_ri: ResultInfo = .{
        .ctx = ri.ctx,
        .rl = if (try ri.rl.resultType(gz, src_node)) |res_ty|
            .{ .coerced_ty = res_ty }
        else
            .none,
    };
    const block_result = try fullBodyExpr(&block_scope, scope, ty_only_ri, node, .normal);
    if (!gz.refIsNoReturn(block_result)) {
        _ = try block_scope.addBreak(.break_inline, block_inst, block_result);
    }
    try block_scope.setBlockComptimeBody(block_inst, reason);
    try gz.instructions.append(gz.astgen.gpa, block_inst);

    return rvalue(gz, ri, block_inst.toRef(), src_node);
}

/// This one is for an actual `comptime` syntax, and will emit a compile error if
/// the scope is already known to be comptime-evaluated.
/// See `comptimeExpr` for the helper function for calling expr in a comptime scope.
fn comptimeExprAst(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    if (gz.is_comptime) {
        try astgen.appendErrorTok(tree.nodeMainToken(node), "redundant comptime keyword in already comptime scope", .{});
    }
    const body_node = tree.nodeData(node).node;
    return comptimeExpr2(gz, scope, ri, body_node, node, .comptime_keyword);
}

/// Restore the error return trace index. Performs the restore only if the result is a non-error or
/// if the result location is a non-error-handling expression.
fn restoreErrRetIndex(
    gz: *GenZir,
    bt: GenZir.BranchTarget,
    ri: ResultInfo,
    node: Ast.Node.Index,
    result: Zir.Inst.Ref,
) !void {
    const op = switch (nodeMayEvalToError(gz.astgen.tree, node)) {
        .always => return, // never restore/pop
        .never => .none, // always restore/pop
        .maybe => switch (ri.ctx) {
            .error_handling_expr, .@"return", .fn_arg, .const_init => switch (ri.rl) {
                .ptr => |ptr_res| try gz.addUnNode(.load, ptr_res.inst, node),
                .inferred_ptr => blk: {
                    // This is a terrible workaround for Sema's inability to load from a .alloc_inferred ptr
                    // before its type has been resolved. There is no valid operand to use here, so error
                    // traces will be popped prematurely.
                    // TODO: Update this to do a proper load from the rl_ptr, once Sema can support it.
                    break :blk .none;
                },
                .destructure => return, // value must be a tuple or array, so never restore/pop
                else => result,
            },
            else => .none, // always restore/pop
        },
    };
    _ = try gz.addRestoreErrRetIndex(bt, .{ .if_non_error = op }, node);
}

fn breakExpr(parent_gz: *GenZir, parent_scope: *Scope, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;
    const tree = astgen.tree;
    const opt_break_label, const opt_rhs = tree.nodeData(node).opt_token_and_opt_node;

    // Look for the label in the scope.
    find_scope: switch (parent_scope.unwrap()) {
        .gen_zir => |gen_zir| {
            const scope = &gen_zir.base;

            if (gen_zir.cur_defer_node.unwrap()) |cur_defer_node| {
                // We are breaking out of a `defer` block.
                return astgen.failNodeNotes(node, "cannot break out of defer expression", .{}, &.{
                    try astgen.errNoteNode(
                        cur_defer_node,
                        "defer expression here",
                        .{},
                    ),
                });
            }

            if (opt_break_label.unwrap()) |break_label| labeled: {
                if (gen_zir.label) |*label| {
                    if (try astgen.tokenIdentEql(label.token, break_label)) {
                        label.used = true;
                        break :labeled;
                    }
                }
                // gz without or with different label, continue to parent scopes.
                continue :find_scope gen_zir.parent.unwrap();
            } else if (!gen_zir.allow_unlabeled_control_flow) {
                // This `break` is unlabeled and the gz we've found doesn't allow
                // unlabeled control flow. Continue to parent scopes.
                continue :find_scope gen_zir.parent.unwrap();
            }

            const break_tag: Zir.Inst.Tag = if (gen_zir.is_inline)
                .break_inline
            else
                .@"break";

            if (opt_rhs.unwrap()) |rhs| {
                // We have a `break` operand.
                const operand = try reachableExpr(parent_gz, parent_scope, gen_zir.break_result_info, rhs, node);

                try genDefers(parent_gz, scope, parent_scope, .normal_only);

                // As our last action before the break, "pop" the error trace if needed
                if (!gen_zir.is_comptime) {
                    try restoreErrRetIndex(parent_gz, .{ .block = gen_zir.break_target }, gen_zir.break_result_info, rhs, operand);
                }
                switch (gen_zir.break_result_info.rl) {
                    .ptr => {
                        // In this case we don't have any mechanism to intercept it;
                        // we assume the result location is written, and we break with void.
                        _ = try parent_gz.addBreak(break_tag, gen_zir.break_target, .void_value);
                    },
                    .discard => {
                        _ = try parent_gz.addBreak(break_tag, gen_zir.break_target, .void_value);
                    },
                    else => {
                        _ = try parent_gz.addBreakWithSrcNode(break_tag, gen_zir.break_target, operand, rhs);
                    },
                }
                return .unreachable_value;
            } else {
                _ = try rvalue(parent_gz, gen_zir.break_result_info, .void_value, node);

                try genDefers(parent_gz, scope, parent_scope, .normal_only);

                // As our last action before the break, "pop" the error trace if needed
                if (!gen_zir.is_comptime)
                    _ = try parent_gz.addRestoreErrRetIndex(.{ .block = gen_zir.break_target }, .always, node);

                _ = try parent_gz.addBreak(break_tag, gen_zir.break_target, .void_value);
                return .unreachable_value;
            }
        },
        .local_val => |local_val| continue :find_scope local_val.parent.unwrap(),
        .local_ptr => |local_ptr| continue :find_scope local_ptr.parent.unwrap(),
        .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
        .namespace => {
            if (opt_break_label.unwrap()) |break_label| {
                const label_name = try astgen.identifierTokenString(break_label);
                return astgen.failTok(break_label, "label not found: '{s}'", .{label_name});
            } else {
                return astgen.failNode(node, "break expression outside loop", .{});
            }
        },
        .top => unreachable,
    }
}

fn continueExpr(parent_gz: *GenZir, parent_scope: *Scope, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;
    const tree = astgen.tree;
    const opt_break_label, const opt_rhs = tree.nodeData(node).opt_token_and_opt_node;

    if (opt_break_label == .none and opt_rhs != .none) {
        return astgen.failNode(node, "cannot continue with operand without label", .{});
    }

    // Look for the label in the scope.
    find_scope: switch (parent_scope.unwrap()) {
        .gen_zir => |gen_zir| {
            const scope = &gen_zir.base;

            if (gen_zir.cur_defer_node.unwrap()) |cur_defer_node| {
                return astgen.failNodeNotes(node, "cannot continue out of defer expression", .{}, &.{
                    try astgen.errNoteNode(
                        cur_defer_node,
                        "defer expression here",
                        .{},
                    ),
                });
            }

            if (opt_break_label.unwrap()) |break_label| labeled: {
                if (gen_zir.label) |*label| {
                    if (try astgen.tokenIdentEql(label.token, break_label)) {
                        switch (gen_zir.continue_target) {
                            .none => {
                                return astgen.failNode(node, "continue outside of loop or labeled switch expression", .{});
                            },
                            .@"break" => if (opt_rhs != .none) {
                                return astgen.failNode(node, "cannot continue loop with operand", .{});
                            },
                            .switch_continue => if (opt_rhs == .none) {
                                return astgen.failNode(node, "cannot continue switch without operand", .{});
                            },
                        }
                        label.used = true;
                        label.used_for_continue = true;
                        break :labeled;
                    }
                }
                // gz without or with different label, continue to parent scopes.
                continue :find_scope gen_zir.parent.unwrap();
            } else if (gen_zir.allow_unlabeled_control_flow) {
                // This `continue` is unlabeled. If the gz we've found doesn't
                // provide a `continue` target or corresponds to a labeled
                // `switch`, ignore it and continue to parent scopes.
                switch (gen_zir.continue_target) {
                    .none, .switch_continue => {
                        continue :find_scope gen_zir.parent.unwrap();
                    },
                    .@"break" => {},
                }
            } else {
                // We don't have a break label and the gz we found doesn't allow
                // unlabeled control flow, so we continue to its parent scopes.
                continue :find_scope gen_zir.parent.unwrap();
            }

            switch (gen_zir.continue_target) {
                .none => unreachable, // should have failed or continued to parent scopes by now
                .@"break" => |block| {
                    try genDefers(parent_gz, scope, parent_scope, .normal_only);

                    const break_tag: Zir.Inst.Tag = if (gen_zir.is_inline)
                        .break_inline
                    else
                        .@"break";
                    if (break_tag == .break_inline) {
                        _ = try parent_gz.addUnNode(.check_comptime_control_flow, block.toRef(), node);
                    }

                    // As our last action before the continue, "pop" the error trace if needed
                    if (!gen_zir.is_comptime) {
                        _ = try parent_gz.addRestoreErrRetIndex(.{ .block = block }, .always, node);
                    }
                    _ = try parent_gz.addBreak(break_tag, block, .void_value);
                    return .unreachable_value;
                },
                .switch_continue => |switch_block| {
                    const rhs = opt_rhs.unwrap().?; // checked above
                    const operand = try reachableExpr(parent_gz, parent_scope, gen_zir.continue_result_info, rhs, node);

                    try genDefers(parent_gz, scope, parent_scope, .normal_only);

                    // As our last action before the continue, "pop" the error trace if needed
                    if (!gen_zir.is_comptime) {
                        _ = try parent_gz.addRestoreErrRetIndex(.{ .block = switch_block }, .always, node);
                    }
                    _ = try parent_gz.addBreakWithSrcNode(.switch_continue, switch_block, operand, rhs);
                    return .unreachable_value;
                },
            }
        },
        .local_val => |local_val| continue :find_scope local_val.parent.unwrap(),
        .local_ptr => |local_ptr| continue :find_scope local_ptr.parent.unwrap(),
        .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
        .namespace => {
            if (opt_break_label.unwrap()) |break_label| {
                const label_name = try astgen.identifierTokenString(break_label);
                return astgen.failTok(break_label, "label not found: '{s}'", .{label_name});
            } else {
                return astgen.failNode(node, "continue expression outside loop", .{});
            }
        },
        .top => unreachable,
    }
}

/// Similar to `expr`, but intended for use when `gz` corresponds to a body
/// which will contain only this node's code. Differs from `expr` in that if the
/// root expression is an unlabeled block, does not emit an actual block.
/// Instead, the block contents are emitted directly into `gz`.
fn fullBodyExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    block_kind: BlockKind,
) InnerError!Zir.Inst.Ref {
    const tree = gz.astgen.tree;

    var stmt_buf: [2]Ast.Node.Index = undefined;
    const statements = tree.blockStatements(&stmt_buf, node) orelse
        return expr(gz, scope, ri, node);

    const lbrace = tree.nodeMainToken(node);

    if (tree.isTokenPrecededByTags(lbrace, &.{ .identifier, .colon })) {
        // Labeled blocks are tricky - forwarding result location information properly is non-trivial,
        // plus if this block is exited with a `break_inline` we aren't allowed multiple breaks. This
        // case is rare, so just treat it as a normal expression and create a nested block.
        return blockExpr(gz, scope, ri, node, statements, block_kind);
    }

    var sub_gz = gz.makeSubBlock(scope);
    try blockExprStmts(&sub_gz, &sub_gz.base, statements, block_kind);

    return rvalue(gz, ri, .void_value, node);
}

const BlockKind = enum { normal, allow_branch_hint };

fn blockExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    block_node: Ast.Node.Index,
    statements: []const Ast.Node.Index,
    kind: BlockKind,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lbrace = tree.nodeMainToken(block_node);
    if (tree.isTokenPrecededByTags(lbrace, &.{ .identifier, .colon })) {
        return labeledBlockExpr(gz, scope, ri, block_node, statements, false, kind);
    }

    if (!gz.is_comptime) {
        // Since this block is unlabeled, its control flow is effectively linear and we
        // can *almost* get away with inlining the block here. However, we actually need
        // to preserve the .block for Sema, to properly pop the error return trace.

        const block_tag: Zir.Inst.Tag = .block;
        const block_inst = try gz.makeBlockInst(block_tag, block_node);
        try gz.instructions.append(astgen.gpa, block_inst);

        var block_scope = gz.makeSubBlock(scope);
        defer block_scope.unstack();

        try blockExprStmts(&block_scope, &block_scope.base, statements, kind);

        if (!block_scope.endsWithNoReturn()) {
            // As our last action before the break, "pop" the error trace if needed
            _ = try gz.addRestoreErrRetIndex(.{ .block = block_inst }, .always, block_node);
            // No `rvalue` call here, as the block result is always `void`, so we do that below.
            _ = try block_scope.addBreak(.@"break", block_inst, .void_value);
        }

        try block_scope.setBlockBody(block_inst);
    } else {
        var sub_gz = gz.makeSubBlock(scope);
        try blockExprStmts(&sub_gz, &sub_gz.base, statements, kind);
    }

    return rvalue(gz, ri, .void_value, block_node);
}

fn checkLabelRedefinition(astgen: *AstGen, parent_scope: *Scope, label: Ast.TokenIndex) !void {
    // Look for the label in the scope.
    find_scope: switch (parent_scope.unwrap()) {
        .gen_zir => |gen_zir| {
            if (gen_zir.label) |prev_label| {
                if (try astgen.tokenIdentEql(label, prev_label.token)) {
                    const label_name = try astgen.identifierTokenString(label);
                    return astgen.failTokNotes(label, "redefinition of label '{s}'", .{
                        label_name,
                    }, &[_]u32{
                        try astgen.errNoteTok(
                            prev_label.token,
                            "previous definition here",
                            .{},
                        ),
                    });
                }
            }
            continue :find_scope gen_zir.parent.unwrap();
        },
        .local_val => |local_val| continue :find_scope local_val.parent.unwrap(),
        .local_ptr => |local_ptr| continue :find_scope local_ptr.parent.unwrap(),
        .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
        .namespace => break :find_scope,
        .top => unreachable,
    }
}

fn labeledBlockExpr(
    gz: *GenZir,
    parent_scope: *Scope,
    ri: ResultInfo,
    block_node: Ast.Node.Index,
    statements: []const Ast.Node.Index,
    force_comptime: bool,
    block_kind: BlockKind,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lbrace = tree.nodeMainToken(block_node);
    const label_token = lbrace - 2;
    assert(tree.tokenTag(label_token) == .identifier);

    try astgen.checkLabelRedefinition(parent_scope, label_token);

    const need_rl = astgen.nodes_need_rl.contains(block_node);
    const block_ri: ResultInfo = if (need_rl) ri else .{
        .rl = switch (ri.rl) {
            .ptr => .{ .ty = (try ri.rl.resultType(gz, block_node)).? },
            .inferred_ptr => .none,
            else => ri.rl,
        },
        .ctx = ri.ctx,
    };
    // We need to call `rvalue` to write through to the pointer only if we had a
    // result pointer and aren't forwarding it.
    const LocTag = @typeInfo(ResultInfo.Loc).@"union".tag_type.?;
    const need_result_rvalue = @as(LocTag, block_ri.rl) != @as(LocTag, ri.rl);

    // Reserve the Block ZIR instruction index so that we can put it into the GenZir struct
    // so that break statements can reference it.
    const block_inst = try gz.makeBlockInst(if (force_comptime) .block_comptime else .block, block_node);
    try gz.instructions.append(astgen.gpa, block_inst);
    var block_scope = gz.makeSubBlock(parent_scope);
    block_scope.is_inline = force_comptime;
    block_scope.label = .{ .token = label_token };
    block_scope.break_target = block_inst;
    block_scope.continue_target = .none;
    block_scope.setBreakResultInfo(block_ri);
    if (force_comptime) block_scope.is_comptime = true;
    defer block_scope.unstack();

    try blockExprStmts(&block_scope, &block_scope.base, statements, block_kind);
    if (!block_scope.endsWithNoReturn()) {
        // As our last action before the return, "pop" the error trace if needed
        _ = try gz.addRestoreErrRetIndex(.{ .block = block_inst }, .always, block_node);
        const result = try rvalue(gz, block_scope.break_result_info, .void_value, block_node);
        const break_tag: Zir.Inst.Tag = if (force_comptime) .break_inline else .@"break";
        _ = try block_scope.addBreak(break_tag, block_inst, result);
    }

    if (!block_scope.label.?.used) {
        try astgen.appendErrorTok(label_token, "unused block label", .{});
    }

    if (force_comptime) {
        try block_scope.setBlockComptimeBody(block_inst, .comptime_keyword);
    } else {
        try block_scope.setBlockBody(block_inst);
    }

    if (need_result_rvalue) {
        return rvalue(gz, ri, block_inst.toRef(), block_node);
    } else {
        return block_inst.toRef();
    }
}

fn blockExprStmts(gz: *GenZir, parent_scope: *Scope, statements: []const Ast.Node.Index, block_kind: BlockKind) !void {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    if (statements.len == 0) return;

    var block_arena = std.heap.ArenaAllocator.init(gz.astgen.gpa);
    defer block_arena.deinit();
    const block_arena_allocator = block_arena.allocator();

    var noreturn_src_node: Ast.Node.OptionalIndex = .none;
    var scope = parent_scope;
    for (statements, 0..) |statement, stmt_idx| {
        if (noreturn_src_node.unwrap()) |src_node| {
            try astgen.appendErrorNodeNotes(
                statement,
                "unreachable code",
                .{},
                &[_]u32{
                    try astgen.errNoteNode(
                        src_node,
                        "control flow is diverted here",
                        .{},
                    ),
                },
            );
        }
        const allow_branch_hint = switch (block_kind) {
            .normal => false,
            .allow_branch_hint => stmt_idx == 0,
        };
        var inner_node = statement;
        while (true) {
            switch (tree.nodeTag(inner_node)) {
                // zig fmt: off
                .global_var_decl,
                .local_var_decl,
                .simple_var_decl,
                .aligned_var_decl, => scope = try varDecl(gz, scope, statement, block_arena_allocator, tree.fullVarDecl(statement).?),

                .assign_destructure => scope = try assignDestructureMaybeDecls(gz, scope, statement, block_arena_allocator),

                .@"defer"    => scope = try deferStmt(gz, scope, statement, block_arena_allocator, .defer_normal),
                .@"errdefer" => scope = try deferStmt(gz, scope, statement, block_arena_allocator, .defer_error),

                .assign => try assign(gz, scope, statement),

                .assign_shl => try assignShift(gz, scope, statement, .shl),
                .assign_shr => try assignShift(gz, scope, statement, .shr),

                .assign_bit_and  => try assignOp(gz, scope, statement, .bit_and),
                .assign_bit_or   => try assignOp(gz, scope, statement, .bit_or),
                .assign_bit_xor  => try assignOp(gz, scope, statement, .xor),
                .assign_div      => try assignOp(gz, scope, statement, .div),
                .assign_sub      => try assignOp(gz, scope, statement, .sub),
                .assign_sub_wrap => try assignOp(gz, scope, statement, .subwrap),
                .assign_mod      => try assignOp(gz, scope, statement, .mod_rem),
                .assign_add      => try assignOp(gz, scope, statement, .add),
                .assign_add_wrap => try assignOp(gz, scope, statement, .addwrap),
                .assign_mul      => try assignOp(gz, scope, statement, .mul),
                .assign_mul_wrap => try assignOp(gz, scope, statement, .mulwrap),

                .grouped_expression => {
                    inner_node = tree.nodeData(inner_node).node_and_token[0];
                    continue;
                },

                .while_simple,
                .while_cont,
                .@"while", => _ = try whileExpr(gz, scope, .{ .rl = .none }, inner_node, tree.fullWhile(inner_node).?, true),

                .for_simple,
                .@"for", => _ = try forExpr(gz, scope, .{ .rl = .none }, inner_node, tree.fullFor(inner_node).?, true),
                // zig fmt: on

                // These cases are here to allow branch hints.
                .builtin_call_two,
                .builtin_call_two_comma,
                .builtin_call,
                .builtin_call_comma,
                => {
                    var buf: [2]Ast.Node.Index = undefined;
                    const params = tree.builtinCallParams(&buf, inner_node).?;

                    try emitDbgNode(gz, inner_node);
                    const result = try builtinCall(gz, scope, .{ .rl = .none }, inner_node, params, allow_branch_hint, .anon);
                    noreturn_src_node = try addEnsureResult(gz, result, inner_node);
                },

                else => noreturn_src_node = try unusedResultExpr(gz, scope, inner_node),
            }
            break;
        }
    }

    if (noreturn_src_node == .none) {
        try genDefers(gz, parent_scope, scope, .normal_only);
    }
    try checkUsed(gz, parent_scope, scope);
}

/// Returns AST source node of the thing that is noreturn if the statement is
/// definitely `noreturn`. Otherwise returns .none.
fn unusedResultExpr(gz: *GenZir, scope: *Scope, statement: Ast.Node.Index) InnerError!Ast.Node.OptionalIndex {
    try emitDbgNode(gz, statement);
    // We need to emit an error if the result is not `noreturn` or `void`, but
    // we want to avoid adding the ZIR instruction if possible for performance.
    const maybe_unused_result = try expr(gz, scope, .{ .rl = .none }, statement);
    return addEnsureResult(gz, maybe_unused_result, statement);
}

fn addEnsureResult(gz: *GenZir, maybe_unused_result: Zir.Inst.Ref, statement: Ast.Node.Index) InnerError!Ast.Node.OptionalIndex {
    var noreturn_src_node: Ast.Node.OptionalIndex = .none;
    const elide_check = if (maybe_unused_result.toIndex()) |inst| b: {
        // Note that this array becomes invalid after appending more items to it
        // in the above while loop.
        const zir_tags = gz.astgen.instructions.items(.tag);
        switch (zir_tags[@intFromEnum(inst)]) {
            // For some instructions, modify the zir data
            // so we can avoid a separate ensure_result_used instruction.
            .call, .field_call => {
                const break_extra = gz.astgen.instructions.items(.data)[@intFromEnum(inst)].pl_node.payload_index;
                comptime assert(std.meta.fieldIndex(Zir.Inst.Call, "flags") ==
                    std.meta.fieldIndex(Zir.Inst.FieldCall, "flags"));
                const flags: *Zir.Inst.Call.Flags = @ptrCast(&gz.astgen.extra.items[
                    break_extra + std.meta.fieldIndex(Zir.Inst.Call, "flags").?
                ]);
                flags.ensure_result_used = true;
                break :b true;
            },
            .builtin_call => {
                const break_extra = gz.astgen.instructions.items(.data)[@intFromEnum(inst)].pl_node.payload_index;
                const flags: *Zir.Inst.BuiltinCall.Flags = @ptrCast(&gz.astgen.extra.items[
                    break_extra + std.meta.fieldIndex(Zir.Inst.BuiltinCall, "flags").?
                ]);
                flags.ensure_result_used = true;
                break :b true;
            },

            // ZIR instructions that might be a type other than `noreturn` or `void`.
            .add,
            .addwrap,
            .add_sat,
            .add_unsafe,
            .param,
            .param_comptime,
            .param_anytype,
            .param_anytype_comptime,
            .alloc,
            .alloc_mut,
            .alloc_comptime_mut,
            .alloc_inferred,
            .alloc_inferred_mut,
            .alloc_inferred_comptime,
            .alloc_inferred_comptime_mut,
            .make_ptr_const,
            .array_cat,
            .array_mul,
            .array_type,
            .array_type_sentinel,
            .elem_type,
            .indexable_ptr_elem_type,
            .splat_op_result_ty,
            .reify_int,
            .vector_type,
            .indexable_ptr_len,
            .anyframe_type,
            .as_node,
            .as_shift_operand,
            .bit_and,
            .bitcast,
            .bit_or,
            .block,
            .block_comptime,
            .block_inline,
            .declaration,
            .suspend_block,
            .loop,
            .bool_br_and,
            .bool_br_or,
            .bool_not,
            .cmp_lt,
            .cmp_lte,
            .cmp_eq,
            .cmp_gte,
            .cmp_gt,
            .cmp_neq,
            .decl_ref,
            .decl_val,
            .load,
            .div,
            .elem_ptr,
            .elem_val,
            .elem_ptr_node,
            .elem_ptr_load,
            .elem_val_imm,
            .field_ptr,
            .field_ptr_load,
            .field_ptr_named,
            .field_ptr_named_load,
            .func,
            .func_inferred,
            .func_fancy,
            .int,
            .int_big,
            .float,
            .float128,
            .int_type,
            .is_non_null,
            .is_non_null_ptr,
            .is_non_err,
            .is_non_err_ptr,
            .ret_is_non_err,
            .mod_rem,
            .mul,
            .mulwrap,
            .mul_sat,
            .ref,
            .shl,
            .shl_sat,
            .shr,
            .str,
            .sub,
            .subwrap,
            .sub_sat,
            .negate,
            .negate_wrap,
            .typeof,
            .typeof_builtin,
            .xor,
            .optional_type,
            .optional_payload_safe,
            .optional_payload_unsafe,
            .optional_payload_safe_ptr,
            .optional_payload_unsafe_ptr,
            .err_union_payload_unsafe,
            .err_union_payload_unsafe_ptr,
            .err_union_code,
            .err_union_code_ptr,
            .ptr_type,
            .enum_literal,
            .decl_literal,
            .decl_literal_no_coerce,
            .merge_error_sets,
            .error_union_type,
            .bit_not,
            .error_value,
            .slice_start,
            .slice_end,
            .slice_sentinel,
            .slice_length,
            .slice_sentinel_ty,
            .import,
            .switch_block,
            .switch_block_ref,
            .switch_block_err_union,
            .union_init,
            .field_type_ref,
            .error_set_decl,
            .enum_from_int,
            .int_from_enum,
            .type_info,
            .size_of,
            .bit_size_of,
            .typeof_log2_int_type,
            .int_from_ptr,
            .align_of,
            .int_from_bool,
            .embed_file,
            .error_name,
            .sqrt,
            .sin,
            .cos,
            .tan,
            .exp,
            .exp2,
            .log,
            .log2,
            .log10,
            .abs,
            .floor,
            .ceil,
            .trunc,
            .round,
            .tag_name,
            .type_name,
            .frame_type,
            .int_from_float,
            .float_from_int,
            .ptr_from_int,
            .float_cast,
            .int_cast,
            .ptr_cast,
            .truncate,
            .has_decl,
            .has_field,
            .clz,
            .ctz,
            .pop_count,
            .byte_swap,
            .bit_reverse,
            .div_exact,
            .div_floor,
            .div_trunc,
            .mod,
            .rem,
            .shl_exact,
            .shr_exact,
            .bit_offset_of,
            .offset_of,
            .splat,
            .reduce,
            .shuffle,
            .atomic_load,
            .atomic_rmw,
            .mul_add,
            .max,
            .min,
            .c_import,
            .@"resume",
            .ret_err_value_code,
            .ret_ptr,
            .ret_type,
            .for_len,
            .@"try",
            .try_ptr,
            .opt_eu_base_ptr_init,
            .coerce_ptr_elem_ty,
            .struct_init_empty,
            .struct_init_empty_result,
            .struct_init_empty_ref_result,
            .struct_init_anon,
            .struct_init,
            .struct_init_ref,
            .struct_init_field_type,
            .struct_init_field_ptr,
            .array_init_anon,
            .array_init,
            .array_init_ref,
            .validate_array_init_ref_ty,
            .array_init_elem_type,
            .array_init_elem_ptr,
            => break :b false,

            .extended => switch (gz.astgen.instructions.items(.data)[@intFromEnum(inst)].extended.opcode) {
                .breakpoint,
                .disable_instrumentation,
                .disable_intrinsics,
                .set_float_mode,
                .branch_hint,
                => break :b true,
                else => break :b false,
            },

            // ZIR instructions that are always `noreturn`.
            .@"break",
            .break_inline,
            .condbr,
            .condbr_inline,
            .compile_error,
            .ret_node,
            .ret_load,
            .ret_implicit,
            .ret_err_value,
            .@"unreachable",
            .repeat,
            .repeat_inline,
            .panic,
            .trap,
            .check_comptime_control_flow,
            .switch_continue,
            => {
                noreturn_src_node = statement.toOptional();
                break :b true;
            },

            // ZIR instructions that are always `void`.
            .dbg_stmt,
            .dbg_var_ptr,
            .dbg_var_val,
            .ensure_result_used,
            .ensure_result_non_error,
            .ensure_err_union_payload_void,
            .@"export",
            .set_eval_branch_quota,
            .atomic_store,
            .store_node,
            .store_to_inferred_ptr,
            .resolve_inferred_alloc,
            .set_runtime_safety,
            .memcpy,
            .memset,
            .memmove,
            .validate_deref,
            .validate_destructure,
            .save_err_ret_index,
            .restore_err_ret_index_unconditional,
            .restore_err_ret_index_fn_entry,
            .validate_struct_init_ty,
            .validate_struct_init_result_ty,
            .validate_ptr_struct_init,
            .validate_array_init_ty,
            .validate_array_init_result_ty,
            .validate_ptr_array_init,
            .validate_ref_ty,
            .validate_const,
            => break :b true,

            .@"defer" => unreachable,
            .defer_err_code => unreachable,
        }
    } else switch (maybe_unused_result) {
        .none => unreachable,

        .unreachable_value => b: {
            noreturn_src_node = statement.toOptional();
            break :b true;
        },

        .void_value => true,

        else => false,
    };
    if (!elide_check) {
        _ = try gz.addUnNode(.ensure_result_used, maybe_unused_result, statement);
    }
    return noreturn_src_node;
}

fn countDefers(outer_scope: *Scope, inner_scope: *Scope) struct {
    have_any: bool,
    have_normal: bool,
    have_err: bool,
    need_err_code: bool,
} {
    var have_normal = false;
    var have_err = false;
    var need_err_code = false;
    var scope = inner_scope;
    while (scope != outer_scope) {
        switch (scope.unwrap()) {
            .gen_zir => |gen_zir| scope = gen_zir.parent,
            .local_val => |local_val| scope = local_val.parent,
            .local_ptr => |local_ptr| scope = local_ptr.parent,
            .defer_normal => |defer_scope| {
                scope = defer_scope.parent;

                have_normal = true;
            },
            .defer_error => |defer_scope| {
                scope = defer_scope.parent;

                have_err = true;

                const have_err_payload = defer_scope.remapped_err_code != .none;
                need_err_code = need_err_code or have_err_payload;
            },
            .namespace => unreachable,
            .top => unreachable,
        }
    }
    return .{
        .have_any = have_normal or have_err,
        .have_normal = have_normal,
        .have_err = have_err,
        .need_err_code = need_err_code,
    };
}

const DefersToEmit = union(enum) {
    both: Zir.Inst.Ref, // err code
    both_sans_err,
    normal_only,
};

fn genDefers(
    gz: *GenZir,
    outer_scope: *Scope,
    inner_scope: *Scope,
    which_ones: DefersToEmit,
) InnerError!void {
    const gpa = gz.astgen.gpa;

    var scope = inner_scope;
    while (scope != outer_scope) {
        switch (scope.unwrap()) {
            .gen_zir => |gen_zir| scope = gen_zir.parent,
            .local_val => |local_val| scope = local_val.parent,
            .local_ptr => |local_ptr| scope = local_ptr.parent,
            .defer_normal => |defer_scope| {
                scope = defer_scope.parent;
                try gz.addDefer(defer_scope.index, defer_scope.len);
            },
            .defer_error => |defer_scope| {
                scope = defer_scope.parent;
                switch (which_ones) {
                    .both_sans_err => {
                        try gz.addDefer(defer_scope.index, defer_scope.len);
                    },
                    .both => |err_code| {
                        if (defer_scope.remapped_err_code.unwrap()) |remapped_err_code| {
                            try gz.instructions.ensureUnusedCapacity(gpa, 1);
                            try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);

                            const payload_index = try gz.astgen.addExtra(Zir.Inst.DeferErrCode{
                                .remapped_err_code = remapped_err_code,
                                .index = defer_scope.index,
                                .len = defer_scope.len,
                            });
                            const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
                            gz.astgen.instructions.appendAssumeCapacity(.{
                                .tag = .defer_err_code,
                                .data = .{ .defer_err_code = .{
                                    .err_code = err_code,
                                    .payload_index = payload_index,
                                } },
                            });
                            gz.instructions.appendAssumeCapacity(new_index);
                        } else {
                            try gz.addDefer(defer_scope.index, defer_scope.len);
                        }
                    },
                    .normal_only => continue,
                }
            },
            .namespace => unreachable,
            .top => unreachable,
        }
    }
}

fn checkUsed(gz: *GenZir, outer_scope: *Scope, inner_scope: *Scope) InnerError!void {
    const astgen = gz.astgen;

    var scope = inner_scope;
    while (scope != outer_scope) {
        switch (scope.unwrap()) {
            .gen_zir => |gen_zir| scope = gen_zir.parent,
            .local_val => |s| {
                if (s.used == .none and s.discarded == .none) {
                    try astgen.appendErrorTok(s.token_src, "unused {s}", .{@tagName(s.id_cat)});
                } else if (s.used != .none and s.discarded != .none) {
                    try astgen.appendErrorTokNotes(s.discarded.unwrap().?, "pointless discard of {s}", .{@tagName(s.id_cat)}, &[_]u32{
                        try gz.astgen.errNoteTok(s.used.unwrap().?, "used here", .{}),
                    });
                }
                scope = s.parent;
            },
            .local_ptr => |s| {
                if (s.used == .none and s.discarded == .none) {
                    try astgen.appendErrorTok(s.token_src, "unused {s}", .{@tagName(s.id_cat)});
                } else {
                    if (s.used != .none and s.discarded != .none) {
                        try astgen.appendErrorTokNotes(s.discarded.unwrap().?, "pointless discard of {s}", .{@tagName(s.id_cat)}, &[_]u32{
                            try astgen.errNoteTok(s.used.unwrap().?, "used here", .{}),
                        });
                    }
                    if (s.id_cat == .@"local variable" and !s.used_as_lvalue) {
                        try astgen.appendErrorTokNotes(s.token_src, "local variable is never mutated", .{}, &.{
                            try astgen.errNoteTok(s.token_src, "consider using 'const'", .{}),
                        });
                    }
                }
                scope = s.parent;
            },
            .defer_normal, .defer_error => |defer_scope| scope = defer_scope.parent,
            .namespace => unreachable,
            .top => unreachable,
        }
    }
}

fn deferStmt(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    block_arena: Allocator,
    scope_tag: Scope.Tag,
) InnerError!*Scope {
    var defer_gen = gz.makeSubBlock(scope);
    defer_gen.cur_defer_node = node.toOptional();
    defer_gen.any_defer_node = node.toOptional();
    defer defer_gen.unstack();

    const tree = gz.astgen.tree;
    var local_val_scope: Scope.LocalVal = undefined;
    var opt_remapped_err_code: Zir.Inst.OptionalIndex = .none;
    const sub_scope = if (scope_tag != .defer_error) &defer_gen.base else blk: {
        const payload_token = tree.nodeData(node).opt_token_and_node[0].unwrap() orelse break :blk &defer_gen.base;
        const ident_name = try gz.astgen.identAsString(payload_token);
        if (std.mem.eql(u8, tree.tokenSlice(payload_token), "_")) {
            try gz.astgen.appendErrorTok(payload_token, "discard of error capture; omit it instead", .{});
            break :blk &defer_gen.base;
        }
        const remapped_err_code: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        opt_remapped_err_code = remapped_err_code.toOptional();
        _ = try gz.astgen.appendPlaceholder();
        const remapped_err_code_ref = remapped_err_code.toRef();
        local_val_scope = .{
            .parent = &defer_gen.base,
            .gen_zir = gz,
            .name = ident_name,
            .inst = remapped_err_code_ref,
            .token_src = payload_token,
            .id_cat = .capture,
        };
        try gz.addDbgVar(.dbg_var_val, ident_name, remapped_err_code_ref);
        break :blk &local_val_scope.base;
    };
    const expr_node = switch (scope_tag) {
        .defer_normal => tree.nodeData(node).node,
        .defer_error => tree.nodeData(node).opt_token_and_node[1],
        else => unreachable,
    };
    _ = try unusedResultExpr(&defer_gen, sub_scope, expr_node);
    try checkUsed(gz, scope, sub_scope);
    _ = try defer_gen.addBreak(.break_inline, @enumFromInt(0), .void_value);

    const body = defer_gen.instructionsSlice();
    const extra_insts: []const Zir.Inst.Index = if (opt_remapped_err_code.unwrap()) |ec| &.{ec} else &.{};
    const body_len = gz.astgen.countBodyLenAfterFixupsExtraRefs(body, extra_insts);

    const index: u32 = @intCast(gz.astgen.extra.items.len);
    try gz.astgen.extra.ensureUnusedCapacity(gz.astgen.gpa, body_len);
    gz.astgen.appendBodyWithFixupsExtraRefsArrayList(&gz.astgen.extra, body, extra_insts);

    const defer_scope = try block_arena.create(Scope.Defer);

    defer_scope.* = .{
        .base = .{ .tag = scope_tag },
        .parent = scope,
        .index = index,
        .len = body_len,
        .remapped_err_code = opt_remapped_err_code,
    };
    return &defer_scope.base;
}

fn varDecl(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    block_arena: Allocator,
    var_decl: Ast.full.VarDecl,
) InnerError!*Scope {
    try emitDbgNode(gz, node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const name_token = var_decl.ast.mut_token + 1;
    const ident_name_raw = tree.tokenSlice(name_token);
    if (mem.eql(u8, ident_name_raw, "_")) {
        return astgen.failTok(name_token, "'_' used as an identifier without @\"_\" syntax", .{});
    }
    const ident_name = try astgen.identAsString(name_token);

    try astgen.detectLocalShadowing(
        scope,
        ident_name,
        name_token,
        ident_name_raw,
        if (tree.tokenTag(var_decl.ast.mut_token) == .keyword_const) .@"local constant" else .@"local variable",
    );

    const init_node = var_decl.ast.init_node.unwrap() orelse {
        return astgen.failNode(node, "variables must be initialized", .{});
    };

    if (var_decl.ast.addrspace_node.unwrap()) |addrspace_node| {
        return astgen.failTok(tree.nodeMainToken(addrspace_node), "cannot set address space of local variable '{s}'", .{ident_name_raw});
    }

    if (var_decl.ast.section_node.unwrap()) |section_node| {
        return astgen.failTok(tree.nodeMainToken(section_node), "cannot set section of local variable '{s}'", .{ident_name_raw});
    }

    const align_inst: Zir.Inst.Ref = if (var_decl.ast.align_node.unwrap()) |align_node|
        try expr(gz, scope, coerced_align_ri, align_node)
    else
        .none;

    switch (tree.tokenTag(var_decl.ast.mut_token)) {
        .keyword_const => {
            if (var_decl.comptime_token) |comptime_token| {
                try astgen.appendErrorTok(comptime_token, "'comptime const' is redundant; instead wrap the initialization expression with 'comptime'", .{});
            }

            // `comptime const` is a non-fatal error; treat it like the init was marked `comptime`.
            const force_comptime = var_decl.comptime_token != null;

            // Depending on the type of AST the initialization expression is, we may need an lvalue
            // or an rvalue as a result location. If it is an rvalue, we can use the instruction as
            // the variable, no memory location needed.
            if (align_inst == .none and
                !astgen.nodes_need_rl.contains(node))
            {
                const result_info: ResultInfo = if (var_decl.ast.type_node.unwrap()) |type_node| .{
                    .rl = .{ .ty = try typeExpr(gz, scope, type_node) },
                    .ctx = .const_init,
                } else .{ .rl = .none, .ctx = .const_init };
                const init_inst: Zir.Inst.Ref = try nameStratExpr(gz, scope, result_info, init_node, .dbg_var) orelse
                    try reachableExprComptime(gz, scope, result_info, init_node, node, if (force_comptime) .comptime_keyword else null);

                _ = try gz.addUnNode(.validate_const, init_inst, init_node);
                try gz.addDbgVar(.dbg_var_val, ident_name, init_inst);

                // The const init expression may have modified the error return trace, so signal
                // to Sema that it should save the new index for restoring later.
                if (nodeMayAppendToErrorTrace(tree, init_node))
                    _ = try gz.addSaveErrRetIndex(.{ .if_of_error_type = init_inst });

                const sub_scope = try block_arena.create(Scope.LocalVal);
                sub_scope.* = .{
                    .parent = scope,
                    .gen_zir = gz,
                    .name = ident_name,
                    .inst = init_inst,
                    .token_src = name_token,
                    .id_cat = .@"local constant",
                };
                return &sub_scope.base;
            }

            const is_comptime = gz.is_comptime or
                tree.nodeTag(init_node) == .@"comptime";

            const init_rl: ResultInfo.Loc = if (var_decl.ast.type_node.unwrap()) |type_node| init_rl: {
                const type_inst = try typeExpr(gz, scope, type_node);
                if (align_inst == .none) {
                    break :init_rl .{ .ptr = .{ .inst = try gz.addUnNode(.alloc, type_inst, node) } };
                } else {
                    break :init_rl .{ .ptr = .{ .inst = try gz.addAllocExtended(.{
                        .node = node,
                        .type_inst = type_inst,
                        .align_inst = align_inst,
                        .is_const = true,
                        .is_comptime = is_comptime,
                    }) } };
                }
            } else init_rl: {
                const alloc_inst = if (align_inst == .none) ptr: {
                    const tag: Zir.Inst.Tag = if (is_comptime)
                        .alloc_inferred_comptime
                    else
                        .alloc_inferred;
                    break :ptr try gz.addNode(tag, node);
                } else ptr: {
                    break :ptr try gz.addAllocExtended(.{
                        .node = node,
                        .type_inst = .none,
                        .align_inst = align_inst,
                        .is_const = true,
                        .is_comptime = is_comptime,
                    });
                };
                break :init_rl .{ .inferred_ptr = alloc_inst };
            };
            const var_ptr: Zir.Inst.Ref, const resolve_inferred: bool = switch (init_rl) {
                .ptr => |ptr| .{ ptr.inst, false },
                .inferred_ptr => |inst| .{ inst, true },
                else => unreachable,
            };
            const init_result_info: ResultInfo = .{ .rl = init_rl, .ctx = .const_init };

            const init_inst: Zir.Inst.Ref = try nameStratExpr(gz, scope, init_result_info, init_node, .dbg_var) orelse
                try reachableExprComptime(gz, scope, init_result_info, init_node, node, if (force_comptime) .comptime_keyword else null);

            // The const init expression may have modified the error return trace, so signal
            // to Sema that it should save the new index for restoring later.
            if (nodeMayAppendToErrorTrace(tree, init_node))
                _ = try gz.addSaveErrRetIndex(.{ .if_of_error_type = init_inst });

            const const_ptr = if (resolve_inferred)
                try gz.addUnNode(.resolve_inferred_alloc, var_ptr, node)
            else
                try gz.addUnNode(.make_ptr_const, var_ptr, node);

            try gz.addDbgVar(.dbg_var_ptr, ident_name, const_ptr);

            const sub_scope = try block_arena.create(Scope.LocalPtr);
            sub_scope.* = .{
                .parent = scope,
                .gen_zir = gz,
                .name = ident_name,
                .ptr = const_ptr,
                .token_src = name_token,
                .maybe_comptime = true,
                .id_cat = .@"local constant",
            };
            return &sub_scope.base;
        },
        .keyword_var => {
            if (var_decl.comptime_token != null and gz.is_comptime)
                return astgen.failTok(var_decl.comptime_token.?, "'comptime var' is redundant in comptime scope", .{});
            const is_comptime = var_decl.comptime_token != null or gz.is_comptime;
            const alloc: Zir.Inst.Ref, const resolve_inferred: bool, const result_info: ResultInfo = if (var_decl.ast.type_node.unwrap()) |type_node| a: {
                const type_inst = try typeExpr(gz, scope, type_node);
                const alloc = alloc: {
                    if (align_inst == .none) {
                        const tag: Zir.Inst.Tag = if (is_comptime)
                            .alloc_comptime_mut
                        else
                            .alloc_mut;
                        break :alloc try gz.addUnNode(tag, type_inst, node);
                    } else {
                        break :alloc try gz.addAllocExtended(.{
                            .node = node,
                            .type_inst = type_inst,
                            .align_inst = align_inst,
                            .is_const = false,
                            .is_comptime = is_comptime,
                        });
                    }
                };
                break :a .{ alloc, false, .{ .rl = .{ .ptr = .{ .inst = alloc } } } };
            } else a: {
                const alloc = alloc: {
                    if (align_inst == .none) {
                        const tag: Zir.Inst.Tag = if (is_comptime)
                            .alloc_inferred_comptime_mut
                        else
                            .alloc_inferred_mut;
                        break :alloc try gz.addNode(tag, node);
                    } else {
                        break :alloc try gz.addAllocExtended(.{
                            .node = node,
                            .type_inst = .none,
                            .align_inst = align_inst,
                            .is_const = false,
                            .is_comptime = is_comptime,
                        });
                    }
                };
                break :a .{ alloc, true, .{ .rl = .{ .inferred_ptr = alloc } } };
            };
            _ = try nameStratExpr(
                gz,
                scope,
                result_info,
                init_node,
                .dbg_var,
            ) orelse try reachableExprComptime(
                gz,
                scope,
                result_info,
                init_node,
                node,
                if (var_decl.comptime_token != null) .comptime_keyword else null,
            );
            const final_ptr: Zir.Inst.Ref = if (resolve_inferred) ptr: {
                break :ptr try gz.addUnNode(.resolve_inferred_alloc, alloc, node);
            } else alloc;

            try gz.addDbgVar(.dbg_var_ptr, ident_name, final_ptr);

            const sub_scope = try block_arena.create(Scope.LocalPtr);
            sub_scope.* = .{
                .parent = scope,
                .gen_zir = gz,
                .name = ident_name,
                .ptr = final_ptr,
                .token_src = name_token,
                .maybe_comptime = is_comptime,
                .id_cat = .@"local variable",
            };
            return &sub_scope.base;
        },
        else => unreachable,
    }
}

fn emitDbgNode(gz: *GenZir, node: Ast.Node.Index) !void {
    // The instruction emitted here is for debugging runtime code.
    // If the current block will be evaluated only during semantic analysis
    // then no dbg_stmt ZIR instruction is needed.
    if (gz.is_comptime) return;
    const astgen = gz.astgen;
    astgen.advanceSourceCursorToNode(node);
    const line = astgen.source_line - gz.decl_line;
    const column = astgen.source_column;
    try emitDbgStmt(gz, .{ line, column });
}

fn assign(gz: *GenZir, scope: *Scope, infix_node: Ast.Node.Index) InnerError!void {
    try emitDbgNode(gz, infix_node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lhs, const rhs = tree.nodeData(infix_node).node_and_node;
    if (tree.nodeTag(lhs) == .identifier) {
        // This intentionally does not support `@"_"` syntax.
        const ident_name = tree.tokenSlice(tree.nodeMainToken(lhs));
        if (mem.eql(u8, ident_name, "_")) {
            _ = try expr(gz, scope, .{ .rl = .discard, .ctx = .assignment }, rhs);
            return;
        }
    }
    const lvalue = try lvalExpr(gz, scope, lhs);
    _ = try expr(gz, scope, .{ .rl = .{ .ptr = .{
        .inst = lvalue,
        .src_node = infix_node,
    } } }, rhs);
}

/// Handles destructure assignments where no LHS is a `const` or `var` decl.
fn assignDestructure(gz: *GenZir, scope: *Scope, node: Ast.Node.Index) InnerError!void {
    try emitDbgNode(gz, node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const full = tree.assignDestructure(node);
    if (full.comptime_token != null and gz.is_comptime) {
        return astgen.appendErrorTok(full.comptime_token.?, "redundant comptime keyword in already comptime scope", .{});
    }

    // If this expression is marked comptime, we must wrap the whole thing in a comptime block.
    var gz_buf: GenZir = undefined;
    const inner_gz = if (full.comptime_token) |_| bs: {
        gz_buf = gz.makeSubBlock(scope);
        gz_buf.is_comptime = true;
        break :bs &gz_buf;
    } else gz;
    defer if (full.comptime_token) |_| inner_gz.unstack();

    const rl_components = try astgen.arena.alloc(ResultInfo.Loc.DestructureComponent, full.ast.variables.len);
    for (rl_components, full.ast.variables) |*variable_rl, variable_node| {
        if (tree.nodeTag(variable_node) == .identifier) {
            // This intentionally does not support `@"_"` syntax.
            const ident_name = tree.tokenSlice(tree.nodeMainToken(variable_node));
            if (mem.eql(u8, ident_name, "_")) {
                variable_rl.* = .discard;
                continue;
            }
        }
        variable_rl.* = .{ .typed_ptr = .{
            .inst = try lvalExpr(inner_gz, scope, variable_node),
            .src_node = variable_node,
        } };
    }

    const ri: ResultInfo = .{ .rl = .{ .destructure = .{
        .src_node = node,
        .components = rl_components,
    } } };

    _ = try expr(inner_gz, scope, ri, full.ast.value_expr);

    if (full.comptime_token) |_| {
        const comptime_block_inst = try gz.makeBlockInst(.block_comptime, node);
        _ = try inner_gz.addBreak(.break_inline, comptime_block_inst, .void_value);
        try inner_gz.setBlockComptimeBody(comptime_block_inst, .comptime_keyword);
        try gz.instructions.append(gz.astgen.gpa, comptime_block_inst);
    }
}

/// Handles destructure assignments where the LHS may contain `const` or `var` decls.
fn assignDestructureMaybeDecls(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    block_arena: Allocator,
) InnerError!*Scope {
    try emitDbgNode(gz, node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const full = tree.assignDestructure(node);
    if (full.comptime_token != null and gz.is_comptime) {
        try astgen.appendErrorTok(full.comptime_token.?, "redundant comptime keyword in already comptime scope", .{});
    }

    const is_comptime = full.comptime_token != null or gz.is_comptime;
    const value_is_comptime = tree.nodeTag(full.ast.value_expr) == .@"comptime";

    // When declaring consts via a destructure, we always use a result pointer.
    // This avoids the need to create tuple types, and is also likely easier to
    // optimize, since it's a bit tricky for the optimizer to "split up" the
    // value into individual pointer writes down the line.

    // We know this rl information won't live past the evaluation of this
    // expression, so it may as well go in the block arena.
    const rl_components = try block_arena.alloc(ResultInfo.Loc.DestructureComponent, full.ast.variables.len);
    var any_non_const_variables = false;
    var any_lvalue_expr = false;
    for (rl_components, full.ast.variables) |*variable_rl, variable_node| {
        switch (tree.nodeTag(variable_node)) {
            .identifier => {
                // This intentionally does not support `@"_"` syntax.
                const ident_name = tree.tokenSlice(tree.nodeMainToken(variable_node));
                if (mem.eql(u8, ident_name, "_")) {
                    any_non_const_variables = true;
                    variable_rl.* = .discard;
                    continue;
                }
            },
            .global_var_decl, .local_var_decl, .simple_var_decl, .aligned_var_decl => {
                const full_var_decl = tree.fullVarDecl(variable_node).?;

                const name_token = full_var_decl.ast.mut_token + 1;
                const ident_name_raw = tree.tokenSlice(name_token);
                if (mem.eql(u8, ident_name_raw, "_")) {
                    return astgen.failTok(name_token, "'_' used as an identifier without @\"_\" syntax", .{});
                }

                // We detect shadowing in the second pass over these, while we're creating scopes.

                if (full_var_decl.ast.addrspace_node.unwrap()) |addrspace_node| {
                    return astgen.failTok(tree.nodeMainToken(addrspace_node), "cannot set address space of local variable '{s}'", .{ident_name_raw});
                }
                if (full_var_decl.ast.section_node.unwrap()) |section_node| {
                    return astgen.failTok(tree.nodeMainToken(section_node), "cannot set section of local variable '{s}'", .{ident_name_raw});
                }

                const is_const = switch (tree.tokenTag(full_var_decl.ast.mut_token)) {
                    .keyword_var => false,
                    .keyword_const => true,
                    else => unreachable,
                };
                if (!is_const) any_non_const_variables = true;

                // We also mark `const`s as comptime if the RHS is definitely comptime-known.
                const this_variable_comptime = is_comptime or (is_const and value_is_comptime);

                const align_inst: Zir.Inst.Ref = if (full_var_decl.ast.align_node.unwrap()) |align_node|
                    try expr(gz, scope, coerced_align_ri, align_node)
                else
                    .none;

                if (full_var_decl.ast.type_node.unwrap()) |type_node| {
                    // Typed alloc
                    const type_inst = try typeExpr(gz, scope, type_node);
                    const ptr = if (align_inst == .none) ptr: {
                        const tag: Zir.Inst.Tag = if (is_const)
                            .alloc
                        else if (this_variable_comptime)
                            .alloc_comptime_mut
                        else
                            .alloc_mut;
                        break :ptr try gz.addUnNode(tag, type_inst, node);
                    } else try gz.addAllocExtended(.{
                        .node = node,
                        .type_inst = type_inst,
                        .align_inst = align_inst,
                        .is_const = is_const,
                        .is_comptime = this_variable_comptime,
                    });
                    variable_rl.* = .{ .typed_ptr = .{ .inst = ptr } };
                } else {
                    // Inferred alloc
                    const ptr = if (align_inst == .none) ptr: {
                        const tag: Zir.Inst.Tag = if (is_const) tag: {
                            break :tag if (this_variable_comptime) .alloc_inferred_comptime else .alloc_inferred;
                        } else tag: {
                            break :tag if (this_variable_comptime) .alloc_inferred_comptime_mut else .alloc_inferred_mut;
                        };
                        break :ptr try gz.addNode(tag, node);
                    } else try gz.addAllocExtended(.{
                        .node = node,
                        .type_inst = .none,
                        .align_inst = align_inst,
                        .is_const = is_const,
                        .is_comptime = this_variable_comptime,
                    });
                    variable_rl.* = .{ .inferred_ptr = ptr };
                }

                continue;
            },
            else => {},
        }
        // This variable is just an lvalue expression.
        // We will fill in its result pointer later, inside a comptime block.
        any_non_const_variables = true;
        any_lvalue_expr = true;
        variable_rl.* = .{ .typed_ptr = .{
            .inst = undefined,
            .src_node = variable_node,
        } };
    }

    if (full.comptime_token != null and !any_non_const_variables) {
        try astgen.appendErrorTok(full.comptime_token.?, "'comptime const' is redundant; instead wrap the initialization expression with 'comptime'", .{});
        // Note that this is non-fatal; we will still evaluate at comptime.
    }

    // If this expression is marked comptime, we must wrap it in a comptime block.
    var gz_buf: GenZir = undefined;
    const inner_gz = if (full.comptime_token) |_| bs: {
        gz_buf = gz.makeSubBlock(scope);
        gz_buf.is_comptime = true;
        break :bs &gz_buf;
    } else gz;
    defer if (full.comptime_token) |_| inner_gz.unstack();

    if (any_lvalue_expr) {
        // At least one variable was an lvalue expr. Iterate again in order to
        // evaluate the lvalues from within the possible block_comptime.
        for (rl_components, full.ast.variables) |*variable_rl, variable_node| {
            if (variable_rl.* != .typed_ptr) continue;
            switch (tree.nodeTag(variable_node)) {
                .global_var_decl, .local_var_decl, .simple_var_decl, .aligned_var_decl => continue,
                else => {},
            }
            variable_rl.typed_ptr.inst = try lvalExpr(inner_gz, scope, variable_node);
        }
    }

    // We can't give a reasonable anon name strategy for destructured inits, so
    // leave it at its default of `.anon`.
    _ = try reachableExpr(inner_gz, scope, .{ .rl = .{ .destructure = .{
        .src_node = node,
        .components = rl_components,
    } } }, full.ast.value_expr, node);

    if (full.comptime_token) |_| {
        // Finish the block_comptime. Inferred alloc resolution etc will occur
        // in the parent block.
        const comptime_block_inst = try gz.makeBlockInst(.block_comptime, node);
        _ = try inner_gz.addBreak(.break_inline, comptime_block_inst, .void_value);
        try inner_gz.setBlockComptimeBody(comptime_block_inst, .comptime_keyword);
        try gz.instructions.append(gz.astgen.gpa, comptime_block_inst);
    }

    // Now, iterate over the variable exprs to construct any new scopes.
    // If there were any inferred allocations, resolve them.
    // If there were any `const` decls, make the pointer constant.
    var cur_scope = scope;
    for (rl_components, full.ast.variables) |variable_rl, variable_node| {
        switch (tree.nodeTag(variable_node)) {
            .local_var_decl, .simple_var_decl, .aligned_var_decl => {},
            else => continue, // We were mutating an existing lvalue - nothing to do
        }
        const full_var_decl = tree.fullVarDecl(variable_node).?;
        const raw_ptr, const resolve_inferred = switch (variable_rl) {
            .discard => unreachable,
            .typed_ptr => |typed_ptr| .{ typed_ptr.inst, false },
            .inferred_ptr => |ptr_inst| .{ ptr_inst, true },
        };
        const is_const = switch (tree.tokenTag(full_var_decl.ast.mut_token)) {
            .keyword_var => false,
            .keyword_const => true,
            else => unreachable,
        };

        // If the alloc was inferred, resolve it. If the alloc was const, make it const.
        const final_ptr = if (resolve_inferred)
            try gz.addUnNode(.resolve_inferred_alloc, raw_ptr, variable_node)
        else if (is_const)
            try gz.addUnNode(.make_ptr_const, raw_ptr, node)
        else
            raw_ptr;

        const name_token = full_var_decl.ast.mut_token + 1;
        const ident_name_raw = tree.tokenSlice(name_token);
        const ident_name = try astgen.identAsString(name_token);
        try astgen.detectLocalShadowing(
            cur_scope,
            ident_name,
            name_token,
            ident_name_raw,
            if (is_const) .@"local constant" else .@"local variable",
        );
        try gz.addDbgVar(.dbg_var_ptr, ident_name, final_ptr);
        // Finally, create the scope.
        const sub_scope = try block_arena.create(Scope.LocalPtr);
        sub_scope.* = .{
            .parent = cur_scope,
            .gen_zir = gz,
            .name = ident_name,
            .ptr = final_ptr,
            .token_src = name_token,
            .maybe_comptime = is_const or is_comptime,
            .id_cat = if (is_const) .@"local constant" else .@"local variable",
        };
        cur_scope = &sub_scope.base;
    }

    return cur_scope;
}

fn assignOp(
    gz: *GenZir,
    scope: *Scope,
    infix_node: Ast.Node.Index,
    op_inst_tag: Zir.Inst.Tag,
) InnerError!void {
    try emitDbgNode(gz, infix_node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lhs_node, const rhs_node = tree.nodeData(infix_node).node_and_node;
    const lhs_ptr = try lvalExpr(gz, scope, lhs_node);

    const cursor = switch (op_inst_tag) {
        .add, .sub, .mul, .div, .mod_rem => maybeAdvanceSourceCursorToMainToken(gz, infix_node),
        else => undefined,
    };
    const lhs = try gz.addUnNode(.load, lhs_ptr, infix_node);

    const rhs_res_ty = switch (op_inst_tag) {
        .add,
        .sub,
        => try gz.add(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .inplace_arith_result_ty,
                .small = @intFromEnum(@as(Zir.Inst.InplaceOp, switch (op_inst_tag) {
                    .add => .add_eq,
                    .sub => .sub_eq,
                    else => unreachable,
                })),
                .operand = @intFromEnum(lhs),
            } },
        }),
        else => try gz.addUnNode(.typeof, lhs, infix_node), // same as LHS type
    };
    // Not `coerced_ty` since `add`/etc won't coerce to this type.
    const rhs = try expr(gz, scope, .{ .rl = .{ .ty = rhs_res_ty } }, rhs_node);

    switch (op_inst_tag) {
        .add, .sub, .mul, .div, .mod_rem => {
            try emitDbgStmt(gz, cursor);
        },
        else => {},
    }
    const result = try gz.addPlNode(op_inst_tag, infix_node, Zir.Inst.Bin{
        .lhs = lhs,
        .rhs = rhs,
    });
    _ = try gz.addPlNode(.store_node, infix_node, Zir.Inst.Bin{
        .lhs = lhs_ptr,
        .rhs = result,
    });
}

fn assignShift(
    gz: *GenZir,
    scope: *Scope,
    infix_node: Ast.Node.Index,
    op_inst_tag: Zir.Inst.Tag,
) InnerError!void {
    try emitDbgNode(gz, infix_node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lhs_node, const rhs_node = tree.nodeData(infix_node).node_and_node;
    const lhs_ptr = try lvalExpr(gz, scope, lhs_node);
    const lhs = try gz.addUnNode(.load, lhs_ptr, infix_node);
    const rhs_type = try gz.addUnNode(.typeof_log2_int_type, lhs, infix_node);
    const rhs = try expr(gz, scope, .{ .rl = .{ .ty = rhs_type } }, rhs_node);

    const result = try gz.addPlNode(op_inst_tag, infix_node, Zir.Inst.Bin{
        .lhs = lhs,
        .rhs = rhs,
    });
    _ = try gz.addPlNode(.store_node, infix_node, Zir.Inst.Bin{
        .lhs = lhs_ptr,
        .rhs = result,
    });
}

fn assignShiftSat(gz: *GenZir, scope: *Scope, infix_node: Ast.Node.Index) InnerError!void {
    try emitDbgNode(gz, infix_node);
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lhs_node, const rhs_node = tree.nodeData(infix_node).node_and_node;
    const lhs_ptr = try lvalExpr(gz, scope, lhs_node);
    const lhs = try gz.addUnNode(.load, lhs_ptr, infix_node);
    // Saturating shift-left allows any integer type for both the LHS and RHS.
    const rhs = try expr(gz, scope, .{ .rl = .none }, rhs_node);

    const result = try gz.addPlNode(.shl_sat, infix_node, Zir.Inst.Bin{
        .lhs = lhs,
        .rhs = rhs,
    });
    _ = try gz.addPlNode(.store_node, infix_node, Zir.Inst.Bin{
        .lhs = lhs_ptr,
        .rhs = result,
    });
}

fn ptrType(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    ptr_info: Ast.full.PtrType,
) InnerError!Zir.Inst.Ref {
    if (ptr_info.size == .c and ptr_info.allowzero_token != null) {
        return gz.astgen.failTok(ptr_info.allowzero_token.?, "C pointers always allow address zero", .{});
    }

    const source_offset = gz.astgen.source_offset;
    const source_line = gz.astgen.source_line;
    const source_column = gz.astgen.source_column;
    const elem_type = try typeExpr(gz, scope, ptr_info.ast.child_type);

    var sentinel_ref: Zir.Inst.Ref = .none;
    var align_ref: Zir.Inst.Ref = .none;
    var addrspace_ref: Zir.Inst.Ref = .none;
    var bit_start_ref: Zir.Inst.Ref = .none;
    var bit_end_ref: Zir.Inst.Ref = .none;
    var trailing_count: u32 = 0;

    if (ptr_info.ast.sentinel.unwrap()) |sentinel| {
        // These attributes can appear in any order and they all come before the
        // element type so we need to reset the source cursor before generating them.
        gz.astgen.source_offset = source_offset;
        gz.astgen.source_line = source_line;
        gz.astgen.source_column = source_column;

        sentinel_ref = try comptimeExpr(
            gz,
            scope,
            .{ .rl = .{ .ty = elem_type } },
            sentinel,
            switch (ptr_info.size) {
                .slice => .slice_sentinel,
                else => .pointer_sentinel,
            },
        );
        trailing_count += 1;
    }
    if (ptr_info.ast.addrspace_node.unwrap()) |addrspace_node| {
        gz.astgen.source_offset = source_offset;
        gz.astgen.source_line = source_line;
        gz.astgen.source_column = source_column;

        const addrspace_ty = try gz.addBuiltinValue(addrspace_node, .address_space);
        addrspace_ref = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = addrspace_ty } }, addrspace_node, .@"addrspace");
        trailing_count += 1;
    }
    if (ptr_info.ast.align_node.unwrap()) |align_node| {
        gz.astgen.source_offset = source_offset;
        gz.astgen.source_line = source_line;
        gz.astgen.source_column = source_column;

        align_ref = try comptimeExpr(gz, scope, coerced_align_ri, align_node, .@"align");
        trailing_count += 1;
    }
    if (ptr_info.ast.bit_range_start.unwrap()) |bit_range_start| {
        const bit_range_end = ptr_info.ast.bit_range_end.unwrap().?;
        bit_start_ref = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u16_type } }, bit_range_start, .type);
        bit_end_ref = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u16_type } }, bit_range_end, .type);
        trailing_count += 2;
    }

    const gpa = gz.astgen.gpa;
    try gz.instructions.ensureUnusedCapacity(gpa, 1);
    try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);
    try gz.astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.PtrType).@"struct".fields.len +
        trailing_count);

    const payload_index = gz.astgen.addExtraAssumeCapacity(Zir.Inst.PtrType{
        .elem_type = elem_type,
        .src_node = gz.nodeIndexToRelative(node),
    });
    if (sentinel_ref != .none) {
        gz.astgen.extra.appendAssumeCapacity(@intFromEnum(sentinel_ref));
    }
    if (align_ref != .none) {
        gz.astgen.extra.appendAssumeCapacity(@intFromEnum(align_ref));
    }
    if (addrspace_ref != .none) {
        gz.astgen.extra.appendAssumeCapacity(@intFromEnum(addrspace_ref));
    }
    if (bit_start_ref != .none) {
        gz.astgen.extra.appendAssumeCapacity(@intFromEnum(bit_start_ref));
        gz.astgen.extra.appendAssumeCapacity(@intFromEnum(bit_end_ref));
    }

    const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
    const result = new_index.toRef();
    gz.astgen.instructions.appendAssumeCapacity(.{ .tag = .ptr_type, .data = .{
        .ptr_type = .{
            .flags = .{
                .is_allowzero = ptr_info.allowzero_token != null,
                .is_mutable = ptr_info.const_token == null,
                .is_volatile = ptr_info.volatile_token != null,
                .has_sentinel = sentinel_ref != .none,
                .has_align = align_ref != .none,
                .has_addrspace = addrspace_ref != .none,
                .has_bit_range = bit_start_ref != .none,
            },
            .size = ptr_info.size,
            .payload_index = payload_index,
        },
    } });
    gz.instructions.appendAssumeCapacity(new_index);

    return rvalue(gz, ri, result, node);
}

fn arrayType(gz: *GenZir, scope: *Scope, ri: ResultInfo, node: Ast.Node.Index) !Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const len_node, const elem_type_node = tree.nodeData(node).node_and_node;
    if (tree.nodeTag(len_node) == .identifier and
        mem.eql(u8, tree.tokenSlice(tree.nodeMainToken(len_node)), "_"))
    {
        return astgen.failNode(len_node, "unable to infer array size", .{});
    }
    const len = try reachableExprComptime(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, len_node, node, .type);
    const elem_type = try typeExpr(gz, scope, elem_type_node);

    const result = try gz.addPlNode(.array_type, node, Zir.Inst.Bin{
        .lhs = len,
        .rhs = elem_type,
    });
    return rvalue(gz, ri, result, node);
}

fn arrayTypeSentinel(gz: *GenZir, scope: *Scope, ri: ResultInfo, node: Ast.Node.Index) !Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const len_node, const extra_index = tree.nodeData(node).node_and_extra;
    const extra = tree.extraData(extra_index, Ast.Node.ArrayTypeSentinel);

    if (tree.nodeTag(len_node) == .identifier and
        mem.eql(u8, tree.tokenSlice(tree.nodeMainToken(len_node)), "_"))
    {
        return astgen.failNode(len_node, "unable to infer array size", .{});
    }
    const len = try reachableExprComptime(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, len_node, node, .array_length);
    const elem_type = try typeExpr(gz, scope, extra.elem_type);
    const sentinel = try reachableExprComptime(gz, scope, .{ .rl = .{ .coerced_ty = elem_type } }, extra.sentinel, node, .array_sentinel);

    const result = try gz.addPlNode(.array_type_sentinel, node, Zir.Inst.ArrayTypeSentinel{
        .len = len,
        .elem_type = elem_type,
        .sentinel = sentinel,
    });
    return rvalue(gz, ri, result, node);
}

const Scratch = struct {
    astgen: *AstGen,
    scratch_top: u32,
    fn init(astgen: *AstGen) Scratch {
        return .{
            .astgen = astgen,
            .scratch_top = @intCast(astgen.scratch.items.len),
        };
    }
    fn reset(s: *Scratch) void {
        s.astgen.scratch.shrinkRetainingCapacity(s.scratch_top);
        s.* = undefined;
    }
    fn addSlice(s: *Scratch, len: u32) Allocator.Error!Slice {
        const start: u32 = @intCast(s.astgen.scratch.items.len);
        try s.astgen.scratch.resize(s.astgen.gpa, start + len);
        return .{ .start = start, .len = len };
    }
    fn addOptionalSlice(s: *Scratch, present: bool, len: u32) Allocator.Error!?Slice {
        if (!present) return null;
        return try addSlice(s, len);
    }
    fn appendBodyWithFixups(s: *Scratch, body: []const Zir.Inst.Index) Allocator.Error!u32 {
        const len = countBodyLenAfterFixups(s.astgen, body);
        try s.astgen.scratch.ensureUnusedCapacity(s.astgen.gpa, len);
        appendBodyWithFixupsArrayList(s.astgen, &s.astgen.scratch, body);
        return len;
    }
    /// Returns the slice containing all data added to this `Scratch`.
    fn all(s: *Scratch) Slice {
        const len = s.astgen.scratch.items.len - s.scratch_top;
        return .{ .start = s.scratch_top, .len = @intCast(len) };
    }
    const Slice = struct {
        start: u32,
        len: u32,
        fn get(s: Slice, astgen: *AstGen) []u32 {
            return astgen.scratch.items[s.start..][0..s.len];
        }
    };
};

const WipDecls = struct {
    astgen: *AstGen,
    slice: Scratch.Slice,
    index: u32,

    fn init(scratch: *Scratch, decls_len: u32) Allocator.Error!WipDecls {
        return .{
            .astgen = scratch.astgen,
            .slice = try scratch.addSlice(decls_len),
            .index = 0,
        };
    }
    fn finish(wip: *WipDecls) void {
        assert(wip.index == wip.slice.len);
        wip.* = undefined;
    }
    fn nextDecl(wip: *WipDecls, decl_inst: Zir.Inst.Index) void {
        wip.slice.get(wip.astgen)[wip.index] = @intFromEnum(decl_inst);
        wip.index += 1;
    }
};

fn fnDecl(
    astgen: *AstGen,
    gz: *GenZir,
    scope: *Scope,
    wip_decls: *WipDecls,
    decl_node: Ast.Node.Index,
    body_node: Ast.Node.OptionalIndex,
    fn_proto: Ast.full.FnProto,
) InnerError!void {
    const tree = astgen.tree;

    const old_hasher = astgen.src_hasher;
    defer astgen.src_hasher = old_hasher;
    astgen.src_hasher = std.zig.SrcHasher.init(.{});

```
