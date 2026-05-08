```
 meaningless on global assembly", .{});
        if (full.outputs.len != 0 or full.inputs.len != 0 or full.ast.clobbers != .none)
            return astgen.failNode(node, "global assembly cannot have inputs, outputs, or clobbers", .{});
    } else {
        if (full.outputs.len == 0 and full.volatile_token == null) {
            return astgen.failNode(node, "assembly expression with no output must be marked volatile", .{});
        }
    }
    if (full.outputs.len >= 16) {
        return astgen.failNode(full.outputs[16], "too many asm outputs", .{});
    }
    var outputs_buffer: [15]Zir.Inst.Asm.Output = undefined;
    const outputs = outputs_buffer[0..full.outputs.len];

    var output_type_bits: u32 = 0;

    for (full.outputs, 0..) |output_node, i| {
        const symbolic_name = tree.nodeMainToken(output_node);
        const name = try astgen.identAsString(symbolic_name);
        const constraint_token = symbolic_name + 2;
        const constraint = (try astgen.strLitAsString(constraint_token)).index;
        const has_arrow = tree.tokenTag(symbolic_name + 4) == .arrow;
        if (has_arrow) {
            if (output_type_bits != 0) {
                return astgen.failNode(output_node, "inline assembly allows up to one output value", .{});
            }
            output_type_bits |= @as(u32, 1) << @intCast(i);
            const out_type_node = tree.nodeData(output_node).opt_node_and_token[0].unwrap().?;
            const out_type_inst = try typeExpr(gz, scope, out_type_node);
            outputs[i] = .{
                .name = name,
                .constraint = constraint,
                .operand = out_type_inst,
            };
        } else {
            const ident_token = symbolic_name + 4;
            // TODO have a look at #215 and related issues and decide how to
            // handle outputs. Do we want this to be identifiers?
            // Or maybe we want to force this to be expressions with a pointer type.
            outputs[i] = .{
                .name = name,
                .constraint = constraint,
                .operand = try localVarRef(gz, scope, .{ .rl = .ref }, node, ident_token),
            };
        }
    }

    if (full.inputs.len >= 32) {
        return astgen.failNode(full.inputs[32], "too many asm inputs", .{});
    }
    var inputs_buffer: [31]Zir.Inst.Asm.Input = undefined;
    const inputs = inputs_buffer[0..full.inputs.len];

    for (full.inputs, 0..) |input_node, i| {
        const symbolic_name = tree.nodeMainToken(input_node);
        const name = try astgen.identAsString(symbolic_name);
        const constraint_token = symbolic_name + 2;
        const constraint = (try astgen.strLitAsString(constraint_token)).index;
        const operand = try expr(gz, scope, .{ .rl = .none }, tree.nodeData(input_node).node_and_token[0]);
        inputs[i] = .{
            .name = name,
            .constraint = constraint,
            .operand = operand,
        };
    }

    const clobbers: Zir.Inst.Ref = if (full.ast.clobbers.unwrap()) |clobbers_node|
        try comptimeExpr(gz, scope, .{ .rl = .{
            .coerced_ty = try gz.addBuiltinValue(clobbers_node, .clobbers),
        } }, clobbers_node, .clobber)
    else
        .none;

    const result = try gz.addAsm(.{
        .tag = tag_and_tmpl.tag,
        .node = node,
        .asm_source = tag_and_tmpl.tmpl,
        .is_volatile = full.volatile_token != null,
        .output_type_bits = output_type_bits,
        .outputs = outputs,
        .inputs = inputs,
        .clobbers = clobbers,
    });
    return rvalue(gz, ri, result, node);
}

fn as(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    lhs: Ast.Node.Index,
    rhs: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const dest_type = try typeExpr(gz, scope, lhs);
    const result = try reachableExpr(gz, scope, .{ .rl = .{ .ty = dest_type } }, rhs, node);
    return rvalue(gz, ri, result, node);
}

fn unionInit(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    params: []const Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const union_type = try typeExpr(gz, scope, params[0]);
    const field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[1], .union_field_names);
    const field_type = try gz.addPlNode(.field_type_ref, node, Zir.Inst.FieldTypeRef{
        .container_type = union_type,
        .field_name = field_name,
    });
    const init = try reachableExpr(gz, scope, .{ .rl = .{ .ty = field_type } }, params[2], node);
    const result = try gz.addPlNode(.union_init, node, Zir.Inst.UnionInit{
        .union_type = union_type,
        .init = init,
        .field_name = field_name,
    });
    return rvalue(gz, ri, result, node);
}

fn bitCast(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const dest_type = try ri.rl.resultTypeForCast(gz, node, "@bitCast");
    const operand = try reachableExpr(gz, scope, .{ .rl = .none }, operand_node, node);
    const result = try gz.addPlNode(.bitcast, node, Zir.Inst.Bin{
        .lhs = dest_type,
        .rhs = operand,
    });
    return rvalue(gz, ri, result, node);
}

/// Handle one or more nested pointer cast builtins:
/// * @ptrCast
/// * @alignCast
/// * @addrSpaceCast
/// * @constCast
/// * @volatileCast
/// Any sequence of such builtins is treated as a single operation. This allowed
/// for sequences like `@ptrCast(@alignCast(ptr))` to work correctly despite the
/// intermediate result type being unknown.
fn ptrCast(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    root_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const FlagsInt = @typeInfo(Zir.Inst.FullPtrCastFlags).@"struct".backing_integer.?;
    var flags: Zir.Inst.FullPtrCastFlags = .{};

    // Note that all pointer cast builtins have one parameter, so we only need
    // to handle `builtin_call_two`.
    var node = root_node;
    while (true) {
        switch (tree.nodeTag(node)) {
            .builtin_call_two, .builtin_call_two_comma => {},
            .grouped_expression => {
                // Handle the chaining even with redundant parentheses
                node = tree.nodeData(node).node_and_token[0];
                continue;
            },
            else => break,
        }

        var buf: [2]Ast.Node.Index = undefined;
        const args = tree.builtinCallParams(&buf, node).?;
        std.debug.assert(args.len <= 2);

        if (args.len == 0) break; // 0 args

        const builtin_token = tree.nodeMainToken(node);
        const builtin_name = tree.tokenSlice(builtin_token);
        const info = BuiltinFn.list.get(builtin_name) orelse break;
        if (args.len == 1) {
            if (info.param_count != 1) break;

            switch (info.tag) {
                else => break,
                inline .ptr_cast,
                .align_cast,
                .addrspace_cast,
                .const_cast,
                .volatile_cast,
                => |tag| {
                    if (@field(flags, @tagName(tag))) {
                        return astgen.failNode(node, "redundant {s}", .{builtin_name});
                    }
                    @field(flags, @tagName(tag)) = true;
                },
            }

            node = args[0];
        } else {
            std.debug.assert(args.len == 2);
            if (info.param_count != 2) break;

            switch (info.tag) {
                else => break,
                .field_parent_ptr => {
                    if (flags.ptr_cast) break;

                    const flags_int: FlagsInt = @bitCast(flags);
                    const cursor = maybeAdvanceSourceCursorToMainToken(gz, root_node);
                    const parent_ptr_type = try ri.rl.resultTypeForCast(gz, root_node, "@alignCast");
                    const field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, args[0], .field_name);
                    const field_ptr = try expr(gz, scope, .{ .rl = .none }, args[1]);
                    try emitDbgStmt(gz, cursor);
                    const result = try gz.addExtendedPayloadSmall(.field_parent_ptr, flags_int, Zir.Inst.FieldParentPtr{
                        .src_node = gz.nodeIndexToRelative(node),
                        .parent_ptr_type = parent_ptr_type,
                        .field_name = field_name,
                        .field_ptr = field_ptr,
                    });
                    return rvalue(gz, ri, result, root_node);
                },
            }
        }
    }

    const flags_int: FlagsInt = @bitCast(flags);
    assert(flags_int != 0);

    const ptr_only: Zir.Inst.FullPtrCastFlags = .{ .ptr_cast = true };
    if (flags_int == @as(FlagsInt, @bitCast(ptr_only))) {
        // Special case: simpler representation
        return typeCast(gz, scope, ri, root_node, node, .ptr_cast, "@ptrCast");
    }

    const no_result_ty_flags: Zir.Inst.FullPtrCastFlags = .{
        .const_cast = true,
        .volatile_cast = true,
    };
    if ((flags_int & ~@as(FlagsInt, @bitCast(no_result_ty_flags))) == 0) {
        // Result type not needed
        const cursor = maybeAdvanceSourceCursorToMainToken(gz, root_node);
        const operand = try expr(gz, scope, .{ .rl = .none }, node);
        try emitDbgStmt(gz, cursor);
        const result = try gz.addExtendedPayloadSmall(.ptr_cast_no_dest, flags_int, Zir.Inst.UnNode{
            .node = gz.nodeIndexToRelative(root_node),
            .operand = operand,
        });
        return rvalue(gz, ri, result, root_node);
    }

    // Full cast including result type

    const cursor = maybeAdvanceSourceCursorToMainToken(gz, root_node);
    const result_type = try ri.rl.resultTypeForCast(gz, root_node, flags.needResultTypeBuiltinName());
    const operand = try expr(gz, scope, .{ .rl = .none }, node);
    try emitDbgStmt(gz, cursor);
    const result = try gz.addExtendedPayloadSmall(.ptr_cast_full, flags_int, Zir.Inst.BinNode{
        .node = gz.nodeIndexToRelative(root_node),
        .lhs = result_type,
        .rhs = operand,
    });
    return rvalue(gz, ri, result, root_node);
}

fn typeOf(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    args: []const Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    if (args.len < 1) {
        return astgen.failNode(node, "expected at least 1 argument, found 0", .{});
    }
    const gpa = astgen.gpa;
    if (args.len == 1) {
        const typeof_inst = try gz.makeBlockInst(.typeof_builtin, node);

        var typeof_scope = gz.makeSubBlock(scope);
        typeof_scope.is_comptime = false;
        typeof_scope.is_typeof = true;
        typeof_scope.c_import = false;
        defer typeof_scope.unstack();

        const ty_expr = try reachableExpr(&typeof_scope, &typeof_scope.base, .{ .rl = .none }, args[0], node);
        if (!gz.refIsNoReturn(ty_expr)) {
            _ = try typeof_scope.addBreak(.break_inline, typeof_inst, ty_expr);
        }
        try typeof_scope.setBlockBody(typeof_inst);

        // typeof_scope unstacked now, can add new instructions to gz
        try gz.instructions.append(gpa, typeof_inst);
        return rvalue(gz, ri, typeof_inst.toRef(), node);
    }
    const payload_size: u32 = std.meta.fields(Zir.Inst.TypeOfPeer).len;
    const payload_index = try reserveExtra(astgen, payload_size + args.len);
    const args_index = payload_index + payload_size;

    const typeof_inst = try gz.addExtendedMultiOpPayloadIndex(.typeof_peer, payload_index, args.len);

    var typeof_scope = gz.makeSubBlock(scope);
    typeof_scope.is_comptime = false;

    for (args, 0..) |arg, i| {
        const param_ref = try reachableExpr(&typeof_scope, &typeof_scope.base, .{ .rl = .none }, arg, node);
        astgen.extra.items[args_index + i] = @intFromEnum(param_ref);
    }
    _ = try typeof_scope.addBreak(.break_inline, typeof_inst.toIndex().?, .void_value);

    const body = typeof_scope.instructionsSlice();
    const body_len = astgen.countBodyLenAfterFixups(body);
    astgen.setExtra(payload_index, Zir.Inst.TypeOfPeer{
        .body_len = @intCast(body_len),
        .body_index = @intCast(astgen.extra.items.len),
        .src_node = gz.nodeIndexToRelative(node),
    });
    try astgen.extra.ensureUnusedCapacity(gpa, body_len);
    astgen.appendBodyWithFixups(body);
    typeof_scope.unstack();

    return rvalue(gz, ri, typeof_inst, node);
}

fn minMax(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    args: []const Ast.Node.Index,
    comptime op: enum { min, max },
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    if (args.len < 2) {
        return astgen.failNode(node, "expected at least 2 arguments, found {}", .{args.len});
    }
    if (args.len == 2) {
        const tag: Zir.Inst.Tag = switch (op) {
            .min => .min,
            .max => .max,
        };
        const a = try expr(gz, scope, .{ .rl = .none }, args[0]);
        const b = try expr(gz, scope, .{ .rl = .none }, args[1]);
        const result = try gz.addPlNode(tag, node, Zir.Inst.Bin{
            .lhs = a,
            .rhs = b,
        });
        return rvalue(gz, ri, result, node);
    }
    const payload_index = try addExtra(astgen, Zir.Inst.NodeMultiOp{
        .src_node = gz.nodeIndexToRelative(node),
    });
    var extra_index = try reserveExtra(gz.astgen, args.len);
    for (args) |arg| {
        const arg_ref = try expr(gz, scope, .{ .rl = .none }, arg);
        astgen.extra.items[extra_index] = @intFromEnum(arg_ref);
        extra_index += 1;
    }
    const tag: Zir.Inst.Extended = switch (op) {
        .min => .min_multi,
        .max => .max_multi,
    };
    const result = try gz.addExtendedMultiOpPayloadIndex(tag, payload_index, args.len);
    return rvalue(gz, ri, result, node);
}

fn builtinCall(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    params: []const Ast.Node.Index,
    allow_branch_hint: bool,
    reify_name_strat: Zir.Inst.NameStrategy,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const builtin_token = tree.nodeMainToken(node);
    const builtin_name = tree.tokenSlice(builtin_token);

    // We handle the different builtins manually because they have different semantics depending
    // on the function. For example, `@as` and others participate in result location semantics,
    // and `@cImport` creates a special scope that collects a .c source code text buffer.
    // Also, some builtins have a variable number of parameters.

    const info = BuiltinFn.list.get(builtin_name) orelse {
        return astgen.failNode(node, "invalid builtin function: '{s}'", .{
            builtin_name,
        });
    };
    if (info.param_count) |expected| {
        if (expected != params.len) {
            const s = if (expected == 1) "" else "s";
            return astgen.failNode(node, "expected {d} argument{s}, found {d}", .{
                expected, s, params.len,
            });
        }
    }

    // Check function scope-only builtins

    if (astgen.fn_block == null and info.illegal_outside_function)
        return astgen.failNode(node, "'{s}' outside function scope", .{builtin_name});

    switch (info.tag) {
        .branch_hint => {
            if (!allow_branch_hint) {
                return astgen.failNode(node, "'@branchHint' must appear as the first statement in a function or conditional branch", .{});
            }
            const hint_ty = try gz.addBuiltinValue(node, .branch_hint);
            const hint_val = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = hint_ty } }, params[0], .operand_branchHint);
            _ = try gz.addExtendedPayload(.branch_hint, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = hint_val,
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .import => {
            const operand_node = params[0];

            if (tree.nodeTag(operand_node) != .string_literal) {
                // Spec reference: https://github.com/ziglang/zig/issues/2206
                return astgen.failNode(operand_node, "@import operand must be a string literal", .{});
            }
            const str_lit_token = tree.nodeMainToken(operand_node);
            const str = try astgen.strLitAsString(str_lit_token);
            const str_slice = astgen.string_bytes.items[@intFromEnum(str.index)..][0..str.len];
            if (mem.findScalar(u8, str_slice, 0) != null) {
                return astgen.failTok(str_lit_token, "import path cannot contain null bytes", .{});
            } else if (str.len == 0) {
                return astgen.failTok(str_lit_token, "import path cannot be empty", .{});
            }
            const res_ty = try ri.rl.resultType(gz, node) orelse .none;
            const payload_index = try addExtra(gz.astgen, Zir.Inst.Import{
                .res_ty = res_ty,
                .path = str.index,
            });
            const result = try gz.add(.{
                .tag = .import,
                .data = .{ .pl_tok = .{
                    .src_tok = gz.tokenIndexToRelative(str_lit_token),
                    .payload_index = payload_index,
                } },
            });
            const gop = try astgen.imports.getOrPut(astgen.gpa, str.index);
            if (!gop.found_existing) {
                gop.value_ptr.* = str_lit_token;
            }
            return rvalue(gz, ri, result, node);
        },
        .compile_log => {
            const payload_index = try addExtra(gz.astgen, Zir.Inst.NodeMultiOp{
                .src_node = gz.nodeIndexToRelative(node),
            });
            var extra_index = try reserveExtra(gz.astgen, params.len);
            for (params) |param| {
                const param_ref = try expr(gz, scope, .{ .rl = .none }, param);
                astgen.extra.items[extra_index] = @intFromEnum(param_ref);
                extra_index += 1;
            }
            const result = try gz.addExtendedMultiOpPayloadIndex(.compile_log, payload_index, params.len);
            return rvalue(gz, ri, result, node);
        },
        .field => {
            switch (ri.rl) {
                .ref, .ref_coerced_ty => {
                    return gz.addPlNode(.field_ptr_named, node, Zir.Inst.FieldNamed{
                        .lhs = try expr(gz, scope, .{ .rl = .ref }, params[0]),
                        .field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[1], .field_name),
                    });
                },
                .ref_const => {
                    return gz.addPlNode(.field_ptr_named, node, Zir.Inst.FieldNamed{
                        .lhs = try expr(gz, scope, .{ .rl = .ref_const }, params[0]),
                        .field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[1], .field_name),
                    });
                },
                else => {
                    const result = try gz.addPlNode(.field_ptr_named_load, node, Zir.Inst.FieldNamed{
                        .lhs = try expr(gz, scope, .{ .rl = .ref_const }, params[0]),
                        .field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[1], .field_name),
                    });
                    return rvalue(gz, ri, result, node);
                },
            }
        },
        .FieldType => {
            const ty_inst = try typeExpr(gz, scope, params[0]);
            const name_inst = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[1], .field_name);
            const result = try gz.addPlNode(.field_type_ref, node, Zir.Inst.FieldTypeRef{
                .container_type = ty_inst,
                .field_name = name_inst,
            });
            return rvalue(gz, ri, result, node);
        },

        // zig fmt: off
        .as         => return as(       gz, scope, ri, node, params[0], params[1]),
        .bit_cast   => return bitCast(  gz, scope, ri, node, params[0]),
        .TypeOf     => return typeOf(   gz, scope, ri, node, params),
        .union_init => return unionInit(gz, scope, ri, node, params),
        .c_import   => return cImport(  gz, scope,     node, params[0]),
        .min        => return minMax(   gz, scope, ri, node, params, .min),
        .max        => return minMax(   gz, scope, ri, node, params, .max),
        // zig fmt: on

        .@"export" => {
            const exported = try expr(gz, scope, .{ .rl = .none }, params[0]);
            const export_options_ty = try gz.addBuiltinValue(node, .export_options);
            const options = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = export_options_ty } }, params[1], .export_options);
            _ = try gz.addPlNode(.@"export", node, Zir.Inst.Export{
                .exported = exported,
                .options = options,
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .@"extern" => {
            const type_inst = try typeExpr(gz, scope, params[0]);
            const extern_options_ty = try gz.addBuiltinValue(node, .extern_options);
            const options = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = extern_options_ty } }, params[1], .extern_options);
            const result = try gz.addExtendedPayload(.builtin_extern, Zir.Inst.BinNode{
                .node = gz.nodeIndexToRelative(node),
                .lhs = type_inst,
                .rhs = options,
            });
            return rvalue(gz, ri, result, node);
        },
        .set_float_mode => {
            const float_mode_ty = try gz.addBuiltinValue(node, .float_mode);
            const order = try expr(gz, scope, .{ .rl = .{ .coerced_ty = float_mode_ty } }, params[0]);
            _ = try gz.addExtendedPayload(.set_float_mode, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = order,
            });
            return rvalue(gz, ri, .void_value, node);
        },

        .src => {
            // Incorporate the source location into the source hash, so that
            // changes in the source location of `@src()` result in re-analysis.
            astgen.src_hasher.update(
                std.mem.asBytes(&astgen.source_line) ++
                    std.mem.asBytes(&astgen.source_column),
            );

            const node_start = tree.tokenStart(tree.firstToken(node));
            astgen.advanceSourceCursor(node_start);
            const result = try gz.addExtendedPayload(.builtin_src, Zir.Inst.Src{
                .node = gz.nodeIndexToRelative(node),
                .line = astgen.source_line,
                .column = astgen.source_column,
            });
            return rvalue(gz, ri, result, node);
        },

        // zig fmt: off
        .This                    => return rvalue(gz, ri, try gz.addNodeExtended(.this,                    node), node),
        .return_address          => return rvalue(gz, ri, try gz.addNodeExtended(.ret_addr,                node), node),
        .error_return_trace      => return rvalue(gz, ri, try gz.addNodeExtended(.error_return_trace,      node), node),
        .frame                   => return rvalue(gz, ri, try gz.addNodeExtended(.frame,                   node), node),
        .frame_address           => return rvalue(gz, ri, try gz.addNodeExtended(.frame_address,           node), node),
        .breakpoint              => return rvalue(gz, ri, try gz.addNodeExtended(.breakpoint,              node), node),
        .disable_instrumentation => return rvalue(gz, ri, try gz.addNodeExtended(.disable_instrumentation, node), node),
        .disable_intrinsics      => return rvalue(gz, ri, try gz.addNodeExtended(.disable_intrinsics,      node), node),

        .type_info   => return simpleUnOpType(gz, scope, ri, node, params[0], .type_info),
        .size_of     => return simpleUnOpType(gz, scope, ri, node, params[0], .size_of),
        .bit_size_of => return simpleUnOpType(gz, scope, ri, node, params[0], .bit_size_of),
        .align_of    => return simpleUnOpType(gz, scope, ri, node, params[0], .align_of),

        .int_from_ptr          => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .int_from_ptr),
        .compile_error         => return simpleUnOp(gz, scope, ri, node, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } },   params[0], .compile_error),
        .set_eval_branch_quota => return simpleUnOp(gz, scope, ri, node, .{ .rl = .{ .coerced_ty = .u32_type } },              params[0], .set_eval_branch_quota),
        .int_from_enum         => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .int_from_enum),
        .int_from_bool         => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .int_from_bool),
        .embed_file            => return simpleUnOp(gz, scope, ri, node, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } },   params[0], .embed_file),
        .error_name            => return simpleUnOp(gz, scope, ri, node, .{ .rl = .{ .coerced_ty = .anyerror_type } },         params[0], .error_name),
        .set_runtime_safety    => return simpleUnOp(gz, scope, ri, node, coerced_bool_ri,                                      params[0], .set_runtime_safety),
        .abs                   => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .abs),
        .tag_name              => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .tag_name),
        .type_name             => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .type_name),
        .Frame                 => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none },                                     params[0], .frame_type),

        .sqrt  => return floatUnOp(gz, scope, ri, node, params[0], .sqrt),
        .sin   => return floatUnOp(gz, scope, ri, node, params[0], .sin),
        .cos   => return floatUnOp(gz, scope, ri, node, params[0], .cos),
        .tan   => return floatUnOp(gz, scope, ri, node, params[0], .tan),
        .exp   => return floatUnOp(gz, scope, ri, node, params[0], .exp),
        .exp2  => return floatUnOp(gz, scope, ri, node, params[0], .exp2),
        .log   => return floatUnOp(gz, scope, ri, node, params[0], .log),
        .log2  => return floatUnOp(gz, scope, ri, node, params[0], .log2),
        .log10 => return floatUnOp(gz, scope, ri, node, params[0], .log10),
        .floor => return floatRoundOp(gz, scope, ri, node, params[0], .floor),
        .ceil  => return floatRoundOp(gz, scope, ri, node, params[0], .ceil),
        .trunc => return floatRoundOp(gz, scope, ri, node, params[0], .trunc),
        .round => return floatRoundOp(gz, scope, ri, node, params[0], .round),

        .int_from_float => return typeCast(gz, scope, ri, node, params[0], .int_from_float, builtin_name),
        .float_from_int => return typeCast(gz, scope, ri, node, params[0], .float_from_int, builtin_name),
        .ptr_from_int   => return typeCast(gz, scope, ri, node, params[0], .ptr_from_int, builtin_name),
        .enum_from_int  => return typeCast(gz, scope, ri, node, params[0], .enum_from_int, builtin_name),
        .float_cast     => return typeCast(gz, scope, ri, node, params[0], .float_cast, builtin_name),
        .int_cast       => return typeCast(gz, scope, ri, node, params[0], .int_cast, builtin_name),
        .truncate       => return typeCast(gz, scope, ri, node, params[0], .truncate, builtin_name),
        // zig fmt: on

        .in_comptime => if (gz.is_comptime) {
            return astgen.failNode(node, "redundant '@inComptime' in comptime scope", .{});
        } else {
            return rvalue(gz, ri, try gz.addNodeExtended(.in_comptime, node), node);
        },

        .EnumLiteral => return rvalue(gz, ri, .enum_literal_type, node),
        .Int => {
            const signedness_ty = try gz.addBuiltinValue(node, .signedness);
            const result = try gz.addPlNode(.reify_int, node, Zir.Inst.Bin{
                .lhs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = signedness_ty } }, params[0], .int_signedness),
                .rhs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u16_type } }, params[1], .int_bit_width),
            });
            return rvalue(gz, ri, result, node);
        },
        .Tuple => {
            const result = try gz.addExtendedPayload(.reify_tuple, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_type_type } }, params[0], .tuple_field_types),
            });
            return rvalue(gz, ri, result, node);
        },
        .Pointer => {
            const ptr_size_ty = try gz.addBuiltinValue(node, .pointer_size);
            const ptr_attrs_ty = try gz.addBuiltinValue(node, .pointer_attributes);
            const size = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = ptr_size_ty } }, params[0], .pointer_size);
            const attrs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = ptr_attrs_ty } }, params[1], .pointer_attrs);
            const elem_ty = try typeExpr(gz, scope, params[2]);
            const sentinel_ty = try gz.addExtendedPayload(.reify_pointer_sentinel_ty, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(params[2]),
                .operand = elem_ty,
            });
            const sentinel = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = sentinel_ty } }, params[3], .pointer_sentinel);
            const result = try gz.addExtendedPayload(.reify_pointer, Zir.Inst.ReifyPointer{
                .node = gz.nodeIndexToRelative(node),
                .size = size,
                .attrs = attrs,
                .elem_ty = elem_ty,
                .sentinel = sentinel,
            });
            return rvalue(gz, ri, result, node);
        },
        .Fn => {
            const fn_attrs_ty = try gz.addBuiltinValue(node, .fn_attributes);
            const param_types = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_type_type } }, params[0], .fn_param_types);
            const param_attrs_ty = try gz.addExtendedPayloadSmall(
                .reify_slice_arg_ty,
                @intFromEnum(Zir.Inst.ReifySliceArgInfo.type_to_fn_param_attrs),
                Zir.Inst.UnNode{ .node = gz.nodeIndexToRelative(params[0]), .operand = param_types },
            );
            const param_attrs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = param_attrs_ty } }, params[1], .fn_param_attrs);
            const ret_ty = try comptimeExpr(gz, scope, coerced_type_ri, params[2], .fn_ret_ty);
            const fn_attrs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = fn_attrs_ty } }, params[3], .fn_attrs);
            const result = try gz.addExtendedPayload(.reify_fn, Zir.Inst.ReifyFn{
                .node = gz.nodeIndexToRelative(node),
                .param_types = param_types,
                .param_attrs = param_attrs,
                .ret_ty = ret_ty,
                .fn_attrs = fn_attrs,
            });
            return rvalue(gz, ri, result, node);
        },
        .Struct => {
            const container_layout_ty = try gz.addBuiltinValue(node, .container_layout);
            const layout = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = container_layout_ty } }, params[0], .struct_layout);
            const backing_ty = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .optional_type_type } }, params[1], .type);
            const field_names = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_slice_const_u8_type } }, params[2], .struct_field_names);
            const field_types_ty = try gz.addExtendedPayloadSmall(
                .reify_slice_arg_ty,
                @intFromEnum(Zir.Inst.ReifySliceArgInfo.string_to_struct_field_type),
                Zir.Inst.UnNode{ .node = gz.nodeIndexToRelative(params[2]), .operand = field_names },
            );
            const field_attrs_ty = try gz.addExtendedPayloadSmall(
                .reify_slice_arg_ty,
                @intFromEnum(Zir.Inst.ReifySliceArgInfo.string_to_struct_field_attrs),
                Zir.Inst.UnNode{ .node = gz.nodeIndexToRelative(params[2]), .operand = field_names },
            );
            const field_types = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = field_types_ty } }, params[3], .struct_field_types);
            const field_attrs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = field_attrs_ty } }, params[4], .struct_field_attrs);
            const result = try gz.addExtendedPayloadSmall(.reify_struct, @intFromEnum(reify_name_strat), Zir.Inst.ReifyStruct{
                .src_line = gz.astgen.source_line,
                .node = node,
                .layout = layout,
                .backing_ty = backing_ty,
                .field_names = field_names,
                .field_types = field_types,
                .field_attrs = field_attrs,
            });
            return rvalue(gz, ri, result, node);
        },
        .Union => {
            const container_layout_ty = try gz.addBuiltinValue(node, .container_layout);
            const layout = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = container_layout_ty } }, params[0], .union_layout);
            const arg_ty = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .optional_type_type } }, params[1], .type);
            const field_names = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_slice_const_u8_type } }, params[2], .union_field_names);
            const field_types_ty = try gz.addExtendedPayloadSmall(
                .reify_slice_arg_ty,
                @intFromEnum(Zir.Inst.ReifySliceArgInfo.string_to_union_field_type),
                Zir.Inst.UnNode{ .node = gz.nodeIndexToRelative(params[2]), .operand = field_names },
            );
            const field_attrs_ty = try gz.addExtendedPayloadSmall(
                .reify_slice_arg_ty,
                @intFromEnum(Zir.Inst.ReifySliceArgInfo.string_to_union_field_attrs),
                Zir.Inst.UnNode{ .node = gz.nodeIndexToRelative(params[2]), .operand = field_names },
            );
            const field_types = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = field_types_ty } }, params[3], .union_field_types);
            const field_attrs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = field_attrs_ty } }, params[4], .union_field_attrs);
            const result = try gz.addExtendedPayloadSmall(.reify_union, @intFromEnum(reify_name_strat), Zir.Inst.ReifyUnion{
                .src_line = gz.astgen.source_line,
                .node = node,
                .layout = layout,
                .arg_ty = arg_ty,
                .field_names = field_names,
                .field_types = field_types,
                .field_attrs = field_attrs,
            });
            return rvalue(gz, ri, result, node);
        },
        .Enum => {
            const enum_mode_ty = try gz.addBuiltinValue(node, .enum_mode);
            const tag_ty = try typeExpr(gz, scope, params[0]);
            const mode = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = enum_mode_ty } }, params[1], .type);
            const field_names = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_slice_const_u8_type } }, params[2], .enum_field_names);
            const field_values_ty = try gz.addExtendedPayload(.reify_enum_value_slice_ty, Zir.Inst.BinNode{
                .node = gz.nodeIndexToRelative(node),
                .lhs = tag_ty,
                .rhs = field_names,
            });
            const field_values = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = field_values_ty } }, params[3], .enum_field_values);
            const result = try gz.addExtendedPayloadSmall(.reify_enum, @intFromEnum(reify_name_strat), Zir.Inst.ReifyEnum{
                .src_line = gz.astgen.source_line,
                .node = node,
                .tag_ty = tag_ty,
                .mode = mode,
                .field_names = field_names,
                .field_values = field_values,
            });
            return rvalue(gz, ri, result, node);
        },

        .panic => {
            try emitDbgNode(gz, node);
            return simpleUnOp(gz, scope, ri, node, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[0], .panic);
        },
        .trap => {
            try emitDbgNode(gz, node);
            _ = try gz.addNode(.trap, node);
            return rvalue(gz, ri, .unreachable_value, node);
        },
        .int_from_error => {
            const operand = try expr(gz, scope, .{ .rl = .none }, params[0]);
            const result = try gz.addExtendedPayload(.int_from_error, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = operand,
            });
            return rvalue(gz, ri, result, node);
        },
        .error_from_int => {
            const operand = try expr(gz, scope, .{ .rl = .none }, params[0]);
            const result = try gz.addExtendedPayload(.error_from_int, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = operand,
            });
            return rvalue(gz, ri, result, node);
        },
        .error_cast => {
            try emitDbgNode(gz, node);

            const result = try gz.addExtendedPayload(.error_cast, Zir.Inst.BinNode{
                .lhs = try ri.rl.resultTypeForCast(gz, node, builtin_name),
                .rhs = try expr(gz, scope, .{ .rl = .none }, params[0]),
                .node = gz.nodeIndexToRelative(node),
            });
            return rvalue(gz, ri, result, node);
        },
        .ptr_cast,
        .align_cast,
        .addrspace_cast,
        .const_cast,
        .volatile_cast,
        => return ptrCast(gz, scope, ri, node),

        // zig fmt: off
        .has_decl  => return hasDeclOrField(gz, scope, ri, node, params[0], params[1], .has_decl),
        .has_field => return hasDeclOrField(gz, scope, ri, node, params[0], params[1], .has_field),

        .clz         => return bitBuiltin(gz, scope, ri, node, params[0], .clz),
        .ctz         => return bitBuiltin(gz, scope, ri, node, params[0], .ctz),
        .pop_count   => return bitBuiltin(gz, scope, ri, node, params[0], .pop_count),
        .byte_swap   => return bitBuiltin(gz, scope, ri, node, params[0], .byte_swap),
        .bit_reverse => return bitBuiltin(gz, scope, ri, node, params[0], .bit_reverse),

        .div_exact => return divBuiltin(gz, scope, ri, node, params[0], params[1], .div_exact),
        .div_floor => return divBuiltin(gz, scope, ri, node, params[0], params[1], .div_floor),
        .div_trunc => return divBuiltin(gz, scope, ri, node, params[0], params[1], .div_trunc),
        .mod       => return divBuiltin(gz, scope, ri, node, params[0], params[1], .mod),
        .rem       => return divBuiltin(gz, scope, ri, node, params[0], params[1], .rem),

        .shl_exact => return shiftOp(gz, scope, ri, node, params[0], params[1], .shl_exact),
        .shr_exact => return shiftOp(gz, scope, ri, node, params[0], params[1], .shr_exact),

        .bit_offset_of => return offsetOf(gz, scope, ri, node, params[0], params[1], .bit_offset_of),
        .offset_of     => return offsetOf(gz, scope, ri, node, params[0], params[1], .offset_of),

        .c_undef   => return simpleCBuiltin(gz, scope, ri, node, params[0], .c_undef),
        .c_include => return simpleCBuiltin(gz, scope, ri, node, params[0], .c_include),

        .cmpxchg_strong => return cmpxchg(gz, scope, ri, node, params, 1),
        .cmpxchg_weak   => return cmpxchg(gz, scope, ri, node, params, 0),
        // zig fmt: on

        .wasm_memory_size => {
            const operand = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u32_type } }, params[0], .wasm_memory_index);
            const result = try gz.addExtendedPayload(.wasm_memory_size, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = operand,
            });
            return rvalue(gz, ri, result, node);
        },
        .wasm_memory_grow => {
            const index_arg = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u32_type } }, params[0], .wasm_memory_index);
            const delta_arg = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, params[1]);
            const result = try gz.addExtendedPayload(.wasm_memory_grow, Zir.Inst.BinNode{
                .node = gz.nodeIndexToRelative(node),
                .lhs = index_arg,
                .rhs = delta_arg,
            });
            return rvalue(gz, ri, result, node);
        },
        .c_define => {
            if (!gz.c_import) return gz.astgen.failNode(node, "C define valid only inside C import block", .{});
            const name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[0], .operand_cDefine_macro_name);
            const value = try comptimeExpr(gz, scope, .{ .rl = .none }, params[1], .operand_cDefine_macro_value);
            const result = try gz.addExtendedPayload(.c_define, Zir.Inst.BinNode{
                .node = gz.nodeIndexToRelative(node),
                .lhs = name,
                .rhs = value,
            });
            return rvalue(gz, ri, result, node);
        },
        .splat => {
            const result_type = try ri.rl.resultTypeForCast(gz, node, builtin_name);
            const elem_type = try gz.addUnNode(.splat_op_result_ty, result_type, node);
            const scalar = try expr(gz, scope, .{ .rl = .{ .ty = elem_type } }, params[0]);
            const result = try gz.addPlNode(.splat, node, Zir.Inst.Bin{
                .lhs = result_type,
                .rhs = scalar,
            });
            return rvalue(gz, ri, result, node);
        },
        .reduce => {
            const reduce_op_ty = try gz.addBuiltinValue(node, .reduce_op);
            const op = try expr(gz, scope, .{ .rl = .{ .coerced_ty = reduce_op_ty } }, params[0]);
            const scalar = try expr(gz, scope, .{ .rl = .none }, params[1]);
            const result = try gz.addPlNode(.reduce, node, Zir.Inst.Bin{
                .lhs = op,
                .rhs = scalar,
            });
            return rvalue(gz, ri, result, node);
        },

        .add_with_overflow => return overflowArithmetic(gz, scope, ri, node, params, .add_with_overflow),
        .sub_with_overflow => return overflowArithmetic(gz, scope, ri, node, params, .sub_with_overflow),
        .mul_with_overflow => return overflowArithmetic(gz, scope, ri, node, params, .mul_with_overflow),
        .shl_with_overflow => return overflowArithmetic(gz, scope, ri, node, params, .shl_with_overflow),

        .atomic_load => {
            const atomic_order_type = try gz.addBuiltinValue(node, .atomic_order);
            const result = try gz.addPlNode(.atomic_load, node, Zir.Inst.AtomicLoad{
                // zig fmt: off
                .elem_type = try typeExpr(gz, scope,                                                  params[0]),
                .ptr       = try expr    (gz, scope, .{ .rl = .none },                                params[1]),
                .ordering  = try expr    (gz, scope, .{ .rl = .{ .coerced_ty = atomic_order_type } }, params[2]),
                // zig fmt: on
            });
            return rvalue(gz, ri, result, node);
        },
        .atomic_rmw => {
            const atomic_order_type = try gz.addBuiltinValue(node, .atomic_order);
            const atomic_rmw_op_type = try gz.addBuiltinValue(node, .atomic_rmw_op);
            const int_type = try typeExpr(gz, scope, params[0]);
            const result = try gz.addPlNode(.atomic_rmw, node, Zir.Inst.AtomicRmw{
                // zig fmt: off
                .ptr       = try expr(gz, scope, .{ .rl = .none },                                 params[1]),
                .operation = try expr(gz, scope, .{ .rl = .{ .coerced_ty = atomic_rmw_op_type } }, params[2]),
                .operand   = try expr(gz, scope, .{ .rl = .{ .ty = int_type } },                   params[3]),
                .ordering  = try expr(gz, scope, .{ .rl = .{ .coerced_ty = atomic_order_type } },  params[4]),
                // zig fmt: on
            });
            return rvalue(gz, ri, result, node);
        },
        .atomic_store => {
            const atomic_order_type = try gz.addBuiltinValue(node, .atomic_order);
            const int_type = try typeExpr(gz, scope, params[0]);
            _ = try gz.addPlNode(.atomic_store, node, Zir.Inst.AtomicStore{
                // zig fmt: off
                .ptr      = try expr(gz, scope, .{ .rl = .none },                                params[1]),
                .operand  = try expr(gz, scope, .{ .rl = .{ .ty = int_type } },                  params[2]),
                .ordering = try expr(gz, scope, .{ .rl = .{ .coerced_ty = atomic_order_type } }, params[3]),
                // zig fmt: on
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .mul_add => {
            const float_type = try typeExpr(gz, scope, params[0]);
            const mulend1 = try expr(gz, scope, .{ .rl = .{ .coerced_ty = float_type } }, params[1]);
            const mulend2 = try expr(gz, scope, .{ .rl = .{ .coerced_ty = float_type } }, params[2]);
            const addend = try expr(gz, scope, .{ .rl = .{ .ty = float_type } }, params[3]);
            const result = try gz.addPlNode(.mul_add, node, Zir.Inst.MulAdd{
                .mulend1 = mulend1,
                .mulend2 = mulend2,
                .addend = addend,
            });
            return rvalue(gz, ri, result, node);
        },
        .call => {
            const call_modifier_ty = try gz.addBuiltinValue(node, .call_modifier);
            const modifier = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = call_modifier_ty } }, params[0], .call_modifier);
            const callee = try expr(gz, scope, .{ .rl = .none }, params[1]);
            const args = try expr(gz, scope, .{ .rl = .none }, params[2]);
            const result = try gz.addPlNode(.builtin_call, node, Zir.Inst.BuiltinCall{
                .modifier = modifier,
                .callee = callee,
                .args = args,
                .flags = .{
                    .is_nosuspend = gz.nosuspend_node != .none,
                    .ensure_result_used = false,
                },
            });
            return rvalue(gz, ri, result, node);
        },
        .field_parent_ptr => {
            const parent_ptr_type = try ri.rl.resultTypeForCast(gz, node, builtin_name);
            const field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, params[0], .field_name);
            const result = try gz.addExtendedPayloadSmall(.field_parent_ptr, 0, Zir.Inst.FieldParentPtr{
                .src_node = gz.nodeIndexToRelative(node),
                .parent_ptr_type = parent_ptr_type,
                .field_name = field_name,
                .field_ptr = try expr(gz, scope, .{ .rl = .none }, params[1]),
            });
            return rvalue(gz, ri, result, node);
        },
        .memcpy => {
            _ = try gz.addPlNode(.memcpy, node, Zir.Inst.Bin{
                .lhs = try expr(gz, scope, .{ .rl = .none }, params[0]),
                .rhs = try expr(gz, scope, .{ .rl = .none }, params[1]),
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .memset => {
            const lhs = try expr(gz, scope, .{ .rl = .none }, params[0]);
            const lhs_ty = try gz.addUnNode(.typeof, lhs, params[0]);
            const elem_ty = try gz.addUnNode(.indexable_ptr_elem_type, lhs_ty, params[0]);
            _ = try gz.addPlNode(.memset, node, Zir.Inst.Bin{
                .lhs = lhs,
                .rhs = try expr(gz, scope, .{ .rl = .{ .coerced_ty = elem_ty } }, params[1]),
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .memmove => {
            _ = try gz.addPlNode(.memmove, node, Zir.Inst.Bin{
                .lhs = try expr(gz, scope, .{ .rl = .none }, params[0]),
                .rhs = try expr(gz, scope, .{ .rl = .none }, params[1]),
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .shuffle => {
            const result = try gz.addPlNode(.shuffle, node, Zir.Inst.Shuffle{
                .elem_type = try typeExpr(gz, scope, params[0]),
                .a = try expr(gz, scope, .{ .rl = .none }, params[1]),
                .b = try expr(gz, scope, .{ .rl = .none }, params[2]),
                .mask = try comptimeExpr(gz, scope, .{ .rl = .none }, params[3], .operand_shuffle_mask),
            });
            return rvalue(gz, ri, result, node);
        },
        .select => {
            const result = try gz.addExtendedPayload(.select, Zir.Inst.Select{
                .node = gz.nodeIndexToRelative(node),
                .elem_type = try typeExpr(gz, scope, params[0]),
                .pred = try expr(gz, scope, .{ .rl = .none }, params[1]),
                .a = try expr(gz, scope, .{ .rl = .none }, params[2]),
                .b = try expr(gz, scope, .{ .rl = .none }, params[3]),
            });
            return rvalue(gz, ri, result, node);
        },
        .Vector => {
            const result = try gz.addPlNode(.vector_type, node, Zir.Inst.Bin{
                .lhs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u32_type } }, params[0], .type),
                .rhs = try typeExpr(gz, scope, params[1]),
            });
            return rvalue(gz, ri, result, node);
        },
        .prefetch => {
            const prefetch_options_ty = try gz.addBuiltinValue(node, .prefetch_options);
            const ptr = try expr(gz, scope, .{ .rl = .none }, params[0]);
            const options = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = prefetch_options_ty } }, params[1], .prefetch_options);
            _ = try gz.addExtendedPayload(.prefetch, Zir.Inst.BinNode{
                .node = gz.nodeIndexToRelative(node),
                .lhs = ptr,
                .rhs = options,
            });
            return rvalue(gz, ri, .void_value, node);
        },
        .c_va_arg => {
            const result = try gz.addExtendedPayload(.c_va_arg, Zir.Inst.BinNode{
                .node = gz.nodeIndexToRelative(node),
                .lhs = try expr(gz, scope, .{ .rl = .none }, params[0]),
                .rhs = try typeExpr(gz, scope, params[1]),
            });
            return rvalue(gz, ri, result, node);
        },
        .c_va_copy => {
            const result = try gz.addExtendedPayload(.c_va_copy, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = try expr(gz, scope, .{ .rl = .none }, params[0]),
            });
            return rvalue(gz, ri, result, node);
        },
        .c_va_end => {
            const result = try gz.addExtendedPayload(.c_va_end, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = try expr(gz, scope, .{ .rl = .none }, params[0]),
            });
            return rvalue(gz, ri, result, node);
        },
        .c_va_start => {
            if (!astgen.fn_var_args) {
                return astgen.failNode(node, "'@cVaStart' in a non-variadic function", .{});
            }
            return rvalue(gz, ri, try gz.addNodeExtended(.c_va_start, node), node);
        },

        .work_item_id => {
            const operand = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u32_type } }, params[0], .work_group_dim_index);
            const result = try gz.addExtendedPayload(.work_item_id, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = operand,
            });
            return rvalue(gz, ri, result, node);
        },
        .work_group_size => {
            const operand = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u32_type } }, params[0], .work_group_dim_index);
            const result = try gz.addExtendedPayload(.work_group_size, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = operand,
            });
            return rvalue(gz, ri, result, node);
        },
        .work_group_id => {
            const operand = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .u32_type } }, params[0], .work_group_dim_index);
            const result = try gz.addExtendedPayload(.work_group_id, Zir.Inst.UnNode{
                .node = gz.nodeIndexToRelative(node),
                .operand = operand,
            });
            return rvalue(gz, ri, result, node);
        },
    }
}

fn hasDeclOrField(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    lhs_node: Ast.Node.Index,
    rhs_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const container_type = try typeExpr(gz, scope, lhs_node);
    const name = try comptimeExpr(
        gz,
        scope,
        .{ .rl = .{ .coerced_ty = .slice_const_u8_type } },
        rhs_node,
        if (tag == .has_decl) .decl_name else .field_name,
    );
    const result = try gz.addPlNode(tag, node, Zir.Inst.Bin{
        .lhs = container_type,
        .rhs = name,
    });
    return rvalue(gz, ri, result, node);
}

fn typeCast(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
    builtin_name: []const u8,
) InnerError!Zir.Inst.Ref {
    const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
    const result_type = try ri.rl.resultTypeForCast(gz, node, builtin_name);
    const operand = try expr(gz, scope, .{ .rl = .none }, operand_node);

    try emitDbgStmt(gz, cursor);
    const result = try gz.addPlNode(tag, node, Zir.Inst.Bin{
        .lhs = result_type,
        .rhs = operand,
    });
    return rvalue(gz, ri, result, node);
}

fn simpleUnOpType(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const operand = try typeExpr(gz, scope, operand_node);
    const result = try gz.addUnNode(tag, operand, node);
    return rvalue(gz, ri, result, node);
}

fn simpleUnOp(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_ri: ResultInfo,
    operand_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
    const operand = if (tag == .compile_error)
        try comptimeExpr(gz, scope, operand_ri, operand_node, .compile_error_string)
    else
        try expr(gz, scope, operand_ri, operand_node);
    switch (tag) {
        .tag_name, .error_name, .int_from_ptr => try emitDbgStmt(gz, cursor),
        else => {},
    }
    const result = try gz.addUnNode(tag, operand, node);
    return rvalue(gz, ri, result, node);
}

fn floatRoundOp(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
    float_tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    if (try ri.rl.resultType(gz, node)) |dest_type| {
        const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);

        const operand_ty_inst = try gz.addExtendedPayload(.round_op_ty, Zir.Inst.UnNode{
            .node = gz.nodeIndexToRelative(node),
            .operand = dest_type,
        });

        const operand = try expr(gz, scope, .{ .rl = .{ .coerced_ty = operand_ty_inst } }, operand_node);

        try emitDbgStmt(gz, cursor);
        const round_op: Zir.Inst.RoundOp = switch (float_tag) {
            .round => .round,
            .floor => .floor,
            .ceil => .ceil,
            .trunc => .trunc,
            else => unreachable,
        };
        const result = try gz.addExtendedPayloadSmall(.round_op, @intFromEnum(round_op), Zir.Inst.BinNode{
            .node = gz.nodeIndexToRelative(node),
            .lhs = dest_type,
            .rhs = operand,
        });
        return rvalue(gz, ri, result, node);
    } else {
        return floatUnOp(gz, scope, ri, node, operand_node, float_tag);
    }
}

fn floatUnOp(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const result_type = try ri.rl.resultType(gz, node);
    const operand_ri: ResultInfo.Loc = if (result_type) |rt| .{
        .ty = try gz.addExtendedPayload(.float_op_result_ty, Zir.Inst.UnNode{
            .node = gz.nodeIndexToRelative(node),
            .operand = rt,
        }),
    } else .none;
    const operand = try expr(gz, scope, .{ .rl = operand_ri }, operand_node);
    const result = try gz.addUnNode(tag, operand, node);
    return rvalue(gz, ri, result, node);
}

fn negation(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    // Check for float literal as the sub-expression because we want to preserve
    // its negativity rather than having it go through comptime subtraction.
    const operand_node = tree.nodeData(node).node;
    if (tree.nodeTag(operand_node) == .number_literal) {
        return numberLiteral(gz, ri, operand_node, node, .negative);
    }

    const operand = try expr(gz, scope, .{ .rl = .none }, operand_node);
    const result = try gz.addUnNode(.negate, operand, node);
    return rvalue(gz, ri, result, node);
}

fn cmpxchg(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    params: []const Ast.Node.Index,
    small: u16,
) InnerError!Zir.Inst.Ref {
    const int_type = try typeExpr(gz, scope, params[0]);
    const atomic_order_type = try gz.addBuiltinValue(node, .atomic_order);
    const result = try gz.addExtendedPayloadSmall(.cmpxchg, small, Zir.Inst.Cmpxchg{
        // zig fmt: off
        .node           = gz.nodeIndexToRelative(node),
        .ptr            = try expr(gz, scope, .{ .rl = .none },                                params[1]),
        .expected_value = try expr(gz, scope, .{ .rl = .{ .ty = int_type } },                  params[2]),
        .new_value      = try expr(gz, scope, .{ .rl = .{ .coerced_ty = int_type } },          params[3]),
        .success_order  = try expr(gz, scope, .{ .rl = .{ .coerced_ty = atomic_order_type } }, params[4]),
        .failure_order  = try expr(gz, scope, .{ .rl = .{ .coerced_ty = atomic_order_type } }, params[5]),
        // zig fmt: on
    });
    return rvalue(gz, ri, result, node);
}

fn bitBuiltin(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const operand = try expr(gz, scope, .{ .rl = .none }, operand_node);
    const result = try gz.addUnNode(tag, operand, node);
    return rvalue(gz, ri, result, node);
}

fn divBuiltin(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    lhs_node: Ast.Node.Index,
    rhs_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
    const lhs = try expr(gz, scope, .{ .rl = .none }, lhs_node);
    const rhs = try expr(gz, scope, .{ .rl = .none }, rhs_node);

    try emitDbgStmt(gz, cursor);
    const result = try gz.addPlNode(tag, node, Zir.Inst.Bin{ .lhs = lhs, .rhs = rhs });
    return rvalue(gz, ri, result, node);
}

fn simpleCBuiltin(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
    tag: Zir.Inst.Extended,
) InnerError!Zir.Inst.Ref {
    const name: []const u8 = if (tag == .c_undef) "C undef" else "C include";
    if (!gz.c_import) return gz.astgen.failNode(node, "{s} valid only inside C import block", .{name});
    const operand = try comptimeExpr(
        gz,
        scope,
        .{ .rl = .{ .coerced_ty = .slice_const_u8_type } },
        operand_node,
        if (tag == .c_undef) .operand_cUndef_macro_name else .operand_cInclude_file_name,
    );
    _ = try gz.addExtendedPayload(tag, Zir.Inst.UnNode{
        .node = gz.nodeIndexToRelative(node),
        .operand = operand,
    });
    return rvalue(gz, ri, .void_value, node);
}

fn offsetOf(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    lhs_node: Ast.Node.Index,
    rhs_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const type_inst = try typeExpr(gz, scope, lhs_node);
    const field_name = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .slice_const_u8_type } }, rhs_node, .field_name);
    const result = try gz.addPlNode(tag, node, Zir.Inst.Bin{
        .lhs = type_inst,
        .rhs = field_name,
    });
    return rvalue(gz, ri, result, node);
}

fn shiftOp(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    lhs_node: Ast.Node.Index,
    rhs_node: Ast.Node.Index,
    tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const lhs = try expr(gz, scope, .{ .rl = .none }, lhs_node);

    const cursor = switch (gz.astgen.tree.nodeTag(node)) {
        .shl, .shr => maybeAdvanceSourceCursorToMainToken(gz, node),
        else => undefined,
    };

    const log2_int_type = try gz.addUnNode(.typeof_log2_int_type, lhs, lhs_node);
    const rhs = try expr(gz, scope, .{ .rl = .{ .ty = log2_int_type }, .ctx = .shift_op }, rhs_node);

    switch (gz.astgen.tree.nodeTag(node)) {
        .shl, .shr => try emitDbgStmt(gz, cursor),
        else => undefined,
    }

    const result = try gz.addPlNode(tag, node, Zir.Inst.Bin{
        .lhs = lhs,
        .rhs = rhs,
    });
    return rvalue(gz, ri, result, node);
}

fn cImport(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    body_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;

    if (gz.c_import) return gz.astgen.failNode(node, "cannot nest @cImport", .{});

    var block_scope = gz.makeSubBlock(scope);
    block_scope.is_comptime = true;
    block_scope.c_import = true;
    defer block_scope.unstack();

    const block_inst = try gz.makeBlockInst(.c_import, node);
    const block_result = try fullBodyExpr(&block_scope, &block_scope.base, .{ .rl = .none }, body_node, .normal);
    _ = try gz.addUnNode(.ensure_result_used, block_result, node);
    if (!gz.refIsNoReturn(block_result)) {
        _ = try block_scope.addBreak(.break_inline, block_inst, .void_value);
    }
    try block_scope.setBlockBody(block_inst);
    // block_scope unstacked now, can add new instructions to gz
    try gz.instructions.append(gpa, block_inst);

    return block_inst.toRef();
}

fn overflowArithmetic(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    params: []const Ast.Node.Index,
    tag: Zir.Inst.Extended,
) InnerError!Zir.Inst.Ref {
    const lhs = try expr(gz, scope, .{ .rl = .none }, params[0]);
    const rhs = try expr(gz, scope, .{ .rl = .none }, params[1]);
    const result = try gz.addExtendedPayload(tag, Zir.Inst.BinNode{
        .node = gz.nodeIndexToRelative(node),
        .lhs = lhs,
        .rhs = rhs,
    });
    return rvalue(gz, ri, result, node);
}

fn callExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    /// If this is not `.none` and this call is a decl literal form (`.foo(...)`), then this
    /// type is used as the decl literal result type instead of the result type from `ri.rl`.
    override_decl_literal_type: Zir.Inst.Ref,
    node: Ast.Node.Index,
    call: Ast.full.Call,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;

    const callee = try calleeExpr(gz, scope, ri.rl, override_decl_literal_type, call.ast.fn_expr);
    const modifier: std.builtin.CallModifier = blk: {
        if (gz.nosuspend_node != .none) {
            break :blk .no_suspend;
        }
        break :blk .auto;
    };

    {
        astgen.advanceSourceCursor(astgen.tree.tokenStart(call.ast.lparen));
        const line = astgen.source_line - gz.decl_line;
        const column = astgen.source_column;
        // Sema expects a dbg_stmt immediately before call,
        try emitDbgStmtForceCurrentIndex(gz, .{ line, column });
    }

    switch (callee) {
        .direct => |obj| assert(obj != .none),
        .field => |field| assert(field.obj_ptr != .none),
    }

    const call_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
    const call_inst = call_index.toRef();
    try gz.astgen.instructions.append(astgen.gpa, undefined);
    try gz.instructions.append(astgen.gpa, call_index);

    const scratch_top = astgen.scratch.items.len;
    defer astgen.scratch.items.len = scratch_top;

    var scratch_index = scratch_top;
    try astgen.scratch.resize(astgen.gpa, scratch_top + call.ast.params.len);

    for (call.ast.params) |param_node| {
        var arg_block = gz.makeSubBlock(scope);
        defer arg_block.unstack();

        // `call_inst` is reused to provide the param type.
        const arg_ref = try fullBodyExpr(&arg_block, &arg_block.base, .{ .rl = .{ .coerced_ty = call_inst }, .ctx = .fn_arg }, param_node, .normal);
        _ = try arg_block.addBreakWithSrcNode(.break_inline, call_index, arg_ref, param_node);

        const body = arg_block.instructionsSlice();
        try astgen.scratch.ensureUnusedCapacity(astgen.gpa, countBodyLenAfterFixups(astgen, body));
        appendBodyWithFixupsArrayList(astgen, &astgen.scratch, body);

        astgen.scratch.items[scratch_index] = @intCast(astgen.scratch.items.len - scratch_top);
        scratch_index += 1;
    }

    // If our result location is a try/catch/error-union-if/return, a function argument,
    // or an initializer for a `const` variable, the error trace propagates.
    // Otherwise, it should always be popped (handled in Sema).
    const propagate_error_trace = switch (ri.ctx) {
        .error_handling_expr, .@"return", .fn_arg, .const_init => true,
        else => false,
    };

    switch (callee) {
        .direct => |callee_obj| {
            const payload_index = try addExtra(astgen, Zir.Inst.Call{
                .callee = callee_obj,
                .flags = .{
                    .pop_error_return_trace = !propagate_error_trace,
                    .packed_modifier = @intCast(@intFromEnum(modifier)),
                    .args_len = @intCast(call.ast.params.len),
                },
            });
            if (call.ast.params.len != 0) {
                try astgen.extra.appendSlice(astgen.gpa, astgen.scratch.items[scratch_top..]);
            }
            gz.astgen.instructions.set(@intFromEnum(call_index), .{
                .tag = .call,
                .data = .{ .pl_node = .{
                    .src_node = gz.nodeIndexToRelative(node),
                    .payload_index = payload_index,
                } },
            });
        },
        .field => |callee_field| {
            const payload_index = try addExtra(astgen, Zir.Inst.FieldCall{
                .obj_ptr = callee_field.obj_ptr,
                .field_name_start = callee_field.field_name_start,
                .flags = .{
                    .pop_error_return_trace = !propagate_error_trace,
                    .packed_modifier = @intCast(@intFromEnum(modifier)),
                    .args_len = @intCast(call.ast.params.len),
                },
            });
            if (call.ast.params.len != 0) {
                try astgen.extra.appendSlice(astgen.gpa, astgen.scratch.items[scratch_top..]);
            }
            gz.astgen.instructions.set(@intFromEnum(call_index), .{
                .tag = .field_call,
                .data = .{ .pl_node = .{
                    .src_node = gz.nodeIndexToRelative(node),
                    .payload_index = payload_index,
                } },
            });
        },
    }
    return rvalue(gz, ri, call_inst, node); // TODO function call with result location
}

const Callee = union(enum) {
    field: struct {
        /// A *pointer* to the object the field is fetched on, so that we can
        /// promote the lvalue to an address if the first parameter requires it.
        obj_ptr: Zir.Inst.Ref,
        /// Offset into `string_bytes`.
        field_name_start: Zir.NullTerminatedString,
    },
    direct: Zir.Inst.Ref,
};

/// calleeExpr generates the function part of a call expression (f in f(x)), but
/// *not* the callee argument to the @call() builtin. Its purpose is to
/// distinguish between standard calls and method call syntax `a.b()`. Thus, if
/// the lhs is a field access, we return using the `field` union field;
/// otherwise, we use the `direct` union field.
fn calleeExpr(
    gz: *GenZir,
    scope: *Scope,
    call_rl: ResultInfo.Loc,
    /// If this is not `.none` and this call is a decl literal form (`.foo(...)`), then this
    /// type is used as the decl literal result type instead of the result type from `call_rl`.
    override_decl_literal_type: Zir.Inst.Ref,
    node: Ast.Node.Index,
) InnerError!Callee {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const tag = tree.nodeTag(node);
    switch (tag) {
        .field_access => {
            const object_node, const field_ident = tree.nodeData(node).node_and_token;
            const str_index = try astgen.identAsString(field_ident);
            // Capture the object by reference so we can promote it to an
            // address in Sema if needed.
            const lhs = try expr(gz, scope, .{ .rl = .ref }, object_node);

            const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
            try emitDbgStmt(gz, cursor);

            return .{ .field = .{
                .obj_ptr = lhs,
                .field_name_start = str_index,
            } };
        },
        .enum_literal => {
            const res_ty = res_ty: {
                if (override_decl_literal_type != .none) break :res_ty override_decl_literal_type;
                break :res_ty try call_rl.resultType(gz, node) orelse {
                    // No result type; lower to a literal call of an enum literal.
                    return .{ .direct = try expr(gz, scope, .{ .rl = .none }, node) };
                };
            };
            // Decl literal call syntax, e.g.
            // `const foo: T = .init();`
            // Look up `init` in `T`, but don't try and coerce it.
            const str_index = try astgen.identAsString(tree.nodeMainToken(node));
            const callee = try gz.addPlNode(.decl_literal_no_coerce, node, Zir.Inst.Field{
                .lhs = res_ty,
                .field_name_start = str_index,
            });
            return .{ .direct = callee };
        },
        else => return .{ .direct = try expr(gz, scope, .{ .rl = .none }, node) },
    }
}

const primitive_instrs = std.StaticStringMap(Zir.Inst.Ref).initComptime(.{
    .{ "anyerror", .anyerror_type },
    .{ "anyframe", .anyframe_type },
    .{ "anyopaque", .anyopaque_type },
    .{ "bool", .bool_type },
    .{ "c_int", .c_int_type },
    .{ "c_long", .c_long_type },
    .{ "c_longdouble", .c_longdouble_type },
    .{ "c_longlong", .c_longlong_type },
    .{ "c_char", .c_char_type },
    .{ "c_short", .c_short_type },
    .{ "c_uint", .c_uint_type },
    .{ "c_ulong", .c_ulong_type },
    .{ "c_ulonglong", .c_ulonglong_type },
    .{ "c_ushort", .c_ushort_type },
    .{ "comptime_float", .comptime_float_type },
    .{ "comptime_int", .comptime_int_type },
    .{ "f128", .f128_type },
    .{ "f16", .f16_type },
    .{ "f32", .f32_type },
    .{ "f64", .f64_type },
    .{ "f80", .f80_type },
    .{ "false", .bool_false },
    .{ "i16", .i16_type },
    .{ "i32", .i32_type },
    .{ "i64", .i64_type },
    .{ "i128", .i128_type },
    .{ "i8", .i8_type },
    .{ "isize", .isize_type },
    .{ "noreturn", .noreturn_type },
    .{ "null", .null_value },
    .{ "true", .bool_true },
    .{ "type", .type_type },
    .{ "u16", .u16_type },
    .{ "u29", .u29_type },
    .{ "u32", .u32_type },
    .{ "u64", .u64_type },
    .{ "u128", .u128_type },
    .{ "u1", .u1_type },
    .{ "u8", .u8_type },
    .{ "undefined", .undef },
    .{ "usize", .usize_type },
    .{ "void", .void_type },
});

comptime {
    // These checks ensure that std.zig.primitives stays in sync with the primitive->Zir map.
    const primitives = std.zig.primitives;
    for (primitive_instrs.keys(), primitive_instrs.values()) |key, value| {
        if (!primitives.isPrimitive(key)) {
            @compileError("std.zig.isPrimitive() is not aware of Zir instr '" ++ @tagName(value) ++ "'");
        }
    }
    for (primitives.names.keys()) |key| {
        if (primitive_instrs.get(key) == null) {
            @compileError("std.zig.primitives entry '" ++ key ++ "' does not have a corresponding Zir instr");
        }
    }
}

fn nodeIsTriviallyZero(tree: *const Ast, node: Ast.Node.Index) bool {
    switch (tree.nodeTag(node)) {
        .number_literal => {
            const ident = tree.nodeMainToken(node);
            return switch (std.zig.parseNumberLiteral(tree.tokenSlice(ident))) {
                .int => |number| switch (number) {
                    0 => true,
                    else => false,
                },
                else => false,
            };
        },
        else => return false,
    }
}

fn nodeMayAppendToErrorTrace(tree: *const Ast, start_node: Ast.Node.Index) bool {
    var node = start_node;
    while (true) {
        switch (tree.nodeTag(node)) {
            // These don't have the opportunity to call any runtime functions.
            .error_value,
            .identifier,
            .@"comptime",
            => return false,

            // Forward the question to the LHS sub-expression.
            .@"try",
            .@"nosuspend",
            => node = tree.nodeData(node).node,
            .grouped_expression,
            .unwrap_optional,
            => node = tree.nodeData(node).node_and_token[0],

            // Anything that does not eval to an error is guaranteed to pop any
            // additions to the error trace, so it effectively does not append.
            else => return nodeMayEvalToError(tree, start_node) != .never,
        }
    }
}

fn nodeMayEvalToError(tree: *const Ast, start_node: Ast.Node.Index) BuiltinFn.EvalToError {
    var node = start_node;
    while (true) {
        switch (tree.nodeTag(node)) {
            .root,
            .test_decl,
            .switch_case,
            .switch_case_inline,
            .switch_case_one,
            .switch_case_inline_one,
            .container_field_init,
            .container_field_align,
            .container_field,
            .asm_output,
            .asm_input,
            => unreachable,

            .error_value => return .always,

            .@"asm",
            .asm_simple,
            .identifier,
            .field_access,
            .deref,
            .array_access,
            .while_simple,
            .while_cont,
            .for_simple,
            .if_simple,
            .@"while",
            .@"if",
            .@"for",
            .@"switch",
            .switch_comma,
            .call_one,
            .call_one_comma,
            .call,
            .call_comma,
            => return .maybe,

            .@"return",
            .@"break",
            .@"continue",
            .bit_not,
            .bool_not,
            .global_var_decl,
            .local_var_decl,
            .simple_var_decl,
            .aligned_var_decl,
            .@"defer",
            .@"errdefer",
            .address_of,
            .optional_type,
            .negation,
            .negation_wrap,
            .@"resume",
            .array_type,
            .array_type_sentinel,
            .ptr_type_aligned,
            .ptr_type_sentinel,
            .ptr_type,
            .ptr_type_bit_range,
            .@"suspend",
            .fn_proto_simple,
            .fn_proto_multi,
            .fn_proto_one,
            .fn_proto,
            .fn_decl,
            .anyframe_type,
            .anyframe_literal,
            .number_literal,
            .enum_literal,
            .string_literal,
            .multiline_string_literal,
            .char_literal,
            .unreachable_literal,
            .error_set_decl,
            .container_decl,
            .container_decl_trailing,
            .container_decl_two,
            .container_decl_two_trailing,
            .container_decl_arg,
            .container_decl_arg_trailing,
            .tagged_union,
            .tagged_union_trailing,
            .tagged_union_two,
            .tagged_union_two_trailing,
            .tagged_union_enum_tag,
            .tagged_union_enum_tag_trailing,
            .add,
            .add_wrap,
            .add_sat,
            .array_cat,
            .array_mult,
            .assign,
            .assign_destructure,
            .assign_bit_and,
            .assign_bit_or,
            .assign_shl,
            .assign_shl_sat,
            .assign_shr,
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
            .bang_equal,
            .bit_and,
            .bit_or,
            .shl,
            .shl_sat,
            .shr,
            .bit_xor,
            .bool_and,
            .bool_or,
            .div,
            .equal_equal,
            .error_union,
            .greater_or_equal,
            .greater_than,
            .less_or_equal,
            .less_than,
            .merge_error_sets,
            .mod,
            .mul,
            .mul_wrap,
            .mul_sat,
            .switch_range,
            .for_range,
            .sub,
            .sub_wrap,
            .sub_sat,
            .slice,
            .slice_open,
            .slice_sentinel,
            .array_init_one,
            .array_init_one_comma,
            .array_init_dot_two,
            .array_init_dot_two_comma,
            .array_init_dot,
            .array_init_dot_comma,
            .array_init,
            .array_init_comma,
            .struct_init_one,
            .struct_init_one_comma,
            .struct_init_dot_two,
            .struct_init_dot_two_comma,
            .struct_init_dot,
            .struct_init_dot_comma,
            .struct_init,
            .struct_init_comma,
            => return .never,

            // Forward the question to the LHS sub-expression.
            .@"try",
            .@"comptime",
            .@"nosuspend",
            => node = tree.nodeData(node).node,
            .grouped_expression,
            .unwrap_optional,
            => node = tree.nodeData(node).node_and_token[0],

            // LHS sub-expression may still be an error under the outer optional or error union
            .@"catch",
            .@"orelse",
            => return .maybe,

            .block_two,
            .block_two_semicolon,
            .block,
            .block_semicolon,
            => {
                const lbrace = tree.nodeMainToken(node);
                if (tree.tokenTag(lbrace - 1) == .colon) {
                    // Labeled blocks may need a memory location to forward
                    // to their break statements.
                    return .maybe;
                } else {
                    return .never;
                }
            },

            .builtin_call,
            .builtin_call_comma,
            .builtin_call_two,
            .builtin_call_two_comma,
            => {
                const builtin_token = tree.nodeMainToken(node);
                const builtin_name = tree.tokenSlice(builtin_token);
                // If the builtin is an invalid name, we don't cause an error here; instead
                // let it pass, and the error will be "invalid builtin function" later.
                const builtin_info = BuiltinFn.list.get(builtin_name) orelse return .maybe;
                return builtin_info.eval_to_error;
            },
        }
    }
}

/// Applies `rl` semantics to `result`. Expressions which do not do their own handling of
/// result locations must call this function on their result.
/// As an example, if `ri.rl` is `.ptr`, it will write the result to the pointer.
/// If `ri.rl` is `.ty`, it will coerce the result to the type.
/// Assumes nothing stacked on `gz`.
fn rvalue(
    gz: *GenZir,
    ri: ResultInfo,
    raw_result: Zir.Inst.Ref,
    src_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    return rvalueInner(gz, ri, raw_result, src_node, true);
}

/// Like `rvalue`, but refuses to perform coercions before taking references for
/// the `ref_coerced_ty` result type. This is used for local variables which do
/// not have `alloc`s, because we want variables to have consistent addresses,
/// i.e. we want them to act like lvalues.
fn rvalueNoCoercePreRef(
    gz: *GenZir,
    ri: ResultInfo,
    raw_result: Zir.Inst.Ref,
    src_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    return rvalueInner(gz, ri, raw_result, src_node, false);
}

fn rvalueInner(
    gz: *GenZir,
    ri: ResultInfo,
    raw_result: Zir.Inst.Ref,
    src_node: Ast.Node.Index,
    allow_coerce_pre_ref: bool,
) InnerError!Zir.Inst.Ref {
    const result = r: {
        if (raw_result.toIndex()) |result_index| {
            const zir_tags = gz.astgen.instructions.items(.tag);
            const data = gz.astgen.instructions.items(.data)[@intFromEnum(result_index)];
            if (zir_tags[@intFromEnum(result_index)].isAlwaysVoid(data)) {
                break :r Zir.Inst.Ref.void_value;
            }
        }
        break :r raw_result;
    };
    if (gz.endsWithNoReturn()) return result;
    switch (ri.rl) {
        .none, .coerced_ty => return result,
        .discard => {
            // Emit a compile error for discarding error values.
            _ = try gz.addUnNode(.ensure_result_non_error, result, src_node);
            return .void_value;
        },
        .ref, .ref_const, .ref_coerced_ty => {
            const coerced_result = if (allow_coerce_pre_ref and ri.rl == .ref_coerced_ty) res: {
                const ptr_ty = ri.rl.ref_coerced_ty;
                break :res try gz.addPlNode(.coerce_ptr_elem_ty, src_node, Zir.Inst.Bin{
                    .lhs = ptr_ty,
                    .rhs = result,
                });
            } else result;
            // We need a pointer but we have a value.
            // Unfortunately it's not quite as simple as directly emitting a ref
            // instruction here because we need subsequent address-of operator on
            // const locals to return the same address.
            const astgen = gz.astgen;
            const tree = astgen.tree;
            const src_token = tree.firstToken(src_node);
            const result_index = coerced_result.toIndex() orelse
                return gz.addUnTok(.ref, coerced_result, src_token);
            const gop = try astgen.ref_table.getOrPut(astgen.gpa, result_index);
            if (!gop.found_existing) {
                gop.value_ptr.* = try gz.makeUnTok(.ref, coerced_result, src_token);
            }
            return gop.value_ptr.*.toRef();
        },
        .ty => |ty_inst| {
            // Quickly eliminate some common, unnecessary type coercion.
            const as_ty = @as(u64, @intFromEnum(Zir.Inst.Ref.type_type)) << 32;
            const as_bool = @as(u64, @intFromEnum(Zir.Inst.Ref.bool_type)) << 32;
            const as_void = @as(u64, @intFromEnum(Zir.Inst.Ref.void_type)) << 32;
            const as_comptime_int = @as(u64, @intFromEnum(Zir.Inst.Ref.comptime_int_type)) << 32;
            const as_usize = @as(u64, @intFromEnum(Zir.Inst.Ref.usize_type)) << 32;
            const as_u1 = @as(u64, @intFromEnum(Zir.Inst.Ref.u1_type)) << 32;
            const as_u8 = @as(u64, @intFromEnum(Zir.Inst.Ref.u8_type)) << 32;
            switch ((@as(u64, @intFromEnum(ty_inst)) << 32) | @as(u64, @intFromEnum(result))) {
                as_ty | @intFromEnum(Zir.Inst.Ref.u1_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.u8_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.i8_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.u16_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.u29_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.i16_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.u32_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.i32_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.u64_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.i64_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.u128_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.i128_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.usize_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.isize_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_char_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_short_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_ushort_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_int_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_uint_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_long_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_ulong_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_longlong_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_ulonglong_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.c_longdouble_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.f16_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.f32_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.f64_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.f80_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.f128_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.anyopaque_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.bool_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.void_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.type_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.anyerror_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.comptime_int_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.comptime_float_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.noreturn_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.anyframe_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.null_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.undefined_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.enum_literal_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.ptr_usize_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.ptr_const_comptime_int_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.manyptr_u8_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.manyptr_const_u8_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.manyptr_const_u8_sentinel_0_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.slice_const_u8_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.slice_const_u8_sentinel_0_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.anyerror_void_error_union_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.generic_poison_type),
                as_ty | @intFromEnum(Zir.Inst.Ref.empty_tuple_type),
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.zero),
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.one),
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.negative_one),
                as_usize | @intFromEnum(Zir.Inst.Ref.undef_usize),
                as_usize | @intFromEnum(Zir.Inst.Ref.zero_usize),
                as_usize | @intFromEnum(Zir.Inst.Ref.one_usize),
                as_u1 | @intFromEnum(Zir.Inst.Ref.undef_u1),
                as_u1 | @intFromEnum(Zir.Inst.Ref.zero_u1),
                as_u1 | @intFromEnum(Zir.Inst.Ref.one_u1),
                as_u8 | @intFromEnum(Zir.Inst.Ref.zero_u8),
                as_u8 | @intFromEnum(Zir.Inst.Ref.one_u8),
                as_u8 | @intFromEnum(Zir.Inst.Ref.four_u8),
                as_bool | @intFromEnum(Zir.Inst.Ref.undef_bool),
                as_bool | @intFromEnum(Zir.Inst.Ref.bool_true),
                as_bool | @intFromEnum(Zir.Inst.Ref.bool_false),
                as_void | @intFromEnum(Zir.Inst.Ref.void_value),
                => return result, // type of result is already correct

                as_bool | @intFromEnum(Zir.Inst.Ref.undef) => return .undef_bool,
                as_usize | @intFromEnum(Zir.Inst.Ref.undef) => return .undef_usize,
                as_usize | @intFromEnum(Zir.Inst.Ref.undef_u1) => return .undef_usize,
                as_u1 | @intFromEnum(Zir.Inst.Ref.undef) => return .undef_u1,

                as_usize | @intFromEnum(Zir.Inst.Ref.zero) => return .zero_usize,
                as_u1 | @intFromEnum(Zir.Inst.Ref.zero) => return .zero_u1,
                as_u8 | @intFromEnum(Zir.Inst.Ref.zero) => return .zero_u8,
                as_usize | @intFromEnum(Zir.Inst.Ref.one) => return .one_usize,
                as_u1 | @intFromEnum(Zir.Inst.Ref.one) => return .one_u1,
                as_u8 | @intFromEnum(Zir.Inst.Ref.one) => return .one_u8,
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.zero_usize) => return .zero,
                as_u1 | @intFromEnum(Zir.Inst.Ref.zero_usize) => return .zero_u1,
                as_u8 | @intFromEnum(Zir.Inst.Ref.zero_usize) => return .zero_u8,
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.one_usize) => return .one,
                as_u1 | @intFromEnum(Zir.Inst.Ref.one_usize) => return .one_u1,
                as_u8 | @intFromEnum(Zir.Inst.Ref.one_usize) => return .one_u8,
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.zero_u1) => return .zero,
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.zero_u8) => return .zero,
                as_usize | @intFromEnum(Zir.Inst.Ref.zero_u1) => return .zero_usize,
                as_usize | @intFromEnum(Zir.Inst.Ref.zero_u8) => return .zero_usize,
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.one_u1) => return .one,
                as_comptime_int | @intFromEnum(Zir.Inst.Ref.one_u8) => return .one,
                as_usize | @intFromEnum(Zir.Inst.Ref.one_u1) => return .one_usize,
                as_usize | @intFromEnum(Zir.Inst.Ref.one_u8) => return .one_usize,

                // Need an explicit type coercion instruction.
                else => return gz.addPlNode(ri.zirTag(), src_node, Zir.Inst.As{
                    .dest_type = ty_inst,
                    .operand = result,
                }),
            }
        },
        .ptr => |ptr_res| {
            _ = try gz.addPlNode(.store_node, ptr_res.src_node orelse src_node, Zir.Inst.Bin{
                .lhs = ptr_res.inst,
                .rhs = result,
            });
            return .void_value;
        },
        .inferred_ptr => |alloc| {
            _ = try gz.addPlNode(.store_to_inferred_ptr, src_node, Zir.Inst.Bin{
                .lhs = alloc,
                .rhs = result,
            });
            return .void_value;
        },
        .destructure => |destructure| {
            const components = destructure.components;
            _ = try gz.addPlNode(.validate_destructure, src_node, Zir.Inst.ValidateDestructure{
                .operand = result,
                .destructure_node = gz.nodeIndexToRelative(destructure.src_node),
                .expect_len = @intCast(components.len),
            });
            for (components, 0..) |component, i| {
                if (component == .discard) continue;
                const elem_val = try gz.add(.{
                    .tag = .elem_val_imm,
                    .data = .{ .elem_val_imm = .{
                        .operand = result,
                        .idx = @intCast(i),
                    } },
                });
                switch (component) {
                    .typed_ptr => |ptr_res| {
                        _ = try gz.addPlNode(.store_node, ptr_res.src_node orelse src_node, Zir.Inst.Bin{
                            .lhs = ptr_res.inst,
                            .rhs = elem_val,
                        });
                    },
                    .inferred_ptr => |ptr_inst| {
                        _ = try gz.addPlNode(.store_to_inferred_ptr, src_node, Zir.Inst.Bin{
                            .lhs = ptr_inst,
                            .rhs = elem_val,
                        });
                    },
                    .discard => unreachable,
                }
            }
            return .void_value;
        },
    }
}

/// Given an identifier token, obtain the string for it.
/// If the token uses @"" syntax, parses as a string, reports errors if applicable,
/// and allocates the result within `astgen.arena`.
/// Otherwise, returns a reference to the source code bytes directly.
/// See also `appendIdentStr` and `parseStrLit`.
fn identifierTokenString(astgen: *AstGen, token: Ast.TokenIndex) InnerError![]const u8 {
    const tree = astgen.tree;
    assert(tree.tokenTag(token) == .identifier);
    const ident_name = tree.tokenSlice(token);
    if (!mem.startsWith(u8, ident_name, "@")) {
        return ident_name;
    }
    var buf: ArrayList(u8) = .empty;
    defer buf.deinit(astgen.gpa);
    try astgen.parseStrLit(token, &buf, ident_name, 1);
    if (mem.findScalar(u8, buf.items, 0) != null) {
        return astgen.failTok(token, "identifier cannot contain null bytes", .{});
    } else if (buf.items.len == 0) {
        return astgen.failTok(token, "identifier cannot be empty", .{});
    }
    const duped = try astgen.arena.dupe(u8, buf.items);
    return duped;
}

/// Given an identifier token, obtain the string for it (possibly parsing as a string
/// literal if it is @"" syntax), and append the string to `buf`.
/// See also `identifierTokenString` and `parseStrLit`.
fn appendIdentStr(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    buf: *ArrayList(u8),
) InnerError!void {
    const tree = astgen.tree;
    assert(tree.tokenTag(token) == .identifier);
    const ident_name = tree.tokenSlice(token);
    if (!mem.startsWith(u8, ident_name, "@")) {
        return buf.appendSlice(astgen.gpa, ident_name);
    } else {
        const start = buf.items.len;
        try astgen.parseStrLit(token, buf, ident_name, 1);
        const slice = buf.items[start..];
        if (mem.findScalar(u8, slice, 0) != null) {
            return astgen.failTok(token, "identifier cannot contain null bytes", .{});
        } else if (slice.len == 0) {
            return astgen.failTok(token, "identifier cannot be empty", .{});
        }
    }
}

/// Appends the result to `buf`.
fn parseStrLit(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    buf: *ArrayList(u8),
    bytes: []const u8,
    offset: u32,
) InnerError!void {
    const raw_string = bytes[offset..];
    const result = r: {
        var aw: std.Io.Writer.Allocating = .fromArrayList(astgen.gpa, buf);
        defer buf.* = aw.toArrayList();
        break :r std.zig.string_literal.parseWrite(&aw.writer, raw_string) catch |err| switch (err) {
            error.WriteFailed => return error.OutOfMemory,
        };
    };
    switch (result) {
        .success => return,
        .failure => |err| return astgen.failWithStrLitError(err, token, bytes, offset),
    }
}

fn failWithStrLitError(
    astgen: *AstGen,
    err: std.zig.string_literal.Error,
    token: Ast.TokenIndex,
    bytes: []const u8,
    offset: u32,
) InnerError {
    const raw_string = bytes[offset..];
    return failOff(astgen, token, @intCast(offset + err.offset()), "{f}", .{err.fmt(raw_string)});
}

fn failNode(
    astgen: *AstGen,
    node: Ast.Node.Index,
    comptime format: []const u8,
    args: anytype,
) InnerError {
    return astgen.failNodeNotes(node, format, args, &[0]u32{});
}

fn appendErrorNode(
    astgen: *AstGen,
    node: Ast.Node.Index,
    comptime format: []const u8,
    args: anytype,
) Allocator.Error!void {
    try astgen.appendErrorNodeNotes(node, format, args, &[0]u32{});
}

fn appendErrorNodeNotes(
    astgen: *AstGen,
    node: Ast.Node.Index,
    comptime format: []const u8,
    args: anytype,
    notes: []const u32,
) Allocator.Error!void {
    @branchHint(.cold);
    const gpa = astgen.gpa;
    const string_bytes = &astgen.string_bytes;
    const msg: Zir.NullTerminatedString = @enumFromInt(string_bytes.items.len);
    try string_bytes.print(gpa, format ++ "\x00", args);
    const notes_index: u32 = if (notes.len != 0) blk: {
        const notes_start = astgen.extra.items.len;
        try astgen.extra.ensureTotalCapacity(gpa, notes_start + 1 + notes.len);
        astgen.extra.appendAssumeCapacity(@intCast(notes.len));
        astgen.extra.appendSliceAssumeCapacity(notes);
        break :blk @intCast(notes_start);
    } else 0;
    try astgen.compile_errors.append(gpa, .{
        .msg = msg,
        .node = node.toOptional(),
        .token = .none,
        .byte_offset = 0,
        .notes = notes_index,
    });
}

fn failNodeNotes(
    astgen: *AstGen,
    node: Ast.Node.Index,
    comptime format: []const u8,
    args: anytype,
    notes: []const u32,
) InnerError {
    try appendErrorNodeNotes(astgen, node, format, args, notes);
    return error.AnalysisFail;
}

fn failTok(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    comptime format: []const u8,
    args: anytype,
) InnerError {
    return astgen.failTokNotes(token, format, args, &[0]u32{});
}

fn appendErrorTok(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    comptime format: []const u8,
    args: anytype,
) !void {
    try astgen.appendErrorTokNotesOff(token, 0, format, args, &[0]u32{});
}

fn failTokNotes(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    comptime format: []const u8,
    args: anytype,
    notes: []const u32,
) InnerError {
    try appendErrorTokNotesOff(astgen, token, 0, format, args, notes);
    return error.AnalysisFail;
}

fn appendErrorTokNotes(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    comptime format: []const u8,
    args: anytype,
    notes: []const u32,
) !void {
    return appendErrorTokNotesOff(astgen, token, 0, format, args, notes);
}

/// Same as `fail`, except given a token plus an offset from its starting byte
/// offset.
fn failOff(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    byte_offset: u32,
    comptime format: []const u8,
    args: anytype,
) InnerError {
    try appendErrorTokNotesOff(astgen, token, byte_offset, format, args, &.{});
    return error.AnalysisFail;
}

fn appendErrorTokNotesOff(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    byte_offset: u32,
    comptime format: []const u8,
    args: anytype,
    notes: []const u32,
) !void {
    @branchHint(.cold);
    const gpa = astgen.gpa;
    const string_bytes = &astgen.string_bytes;
    const msg: Zir.NullTerminatedString = @enumFromInt(string_bytes.items.len);
    try string_bytes.print(gpa, format ++ "\x00", args);
    const notes_index: u32 = if (notes.
```
