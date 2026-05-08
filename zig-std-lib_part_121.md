```
    // We don't add the full source yet, because we also need the prototype hash!
    // The source slice is added towards the *end* of this function.
    astgen.src_hasher.update(std.mem.asBytes(&astgen.source_column));

    // missing function name already checked in scanContainer()
    const fn_name_token = fn_proto.name_token.?;

    // We insert this at the beginning so that its instruction index marks the
    // start of the top level declaration.
    const decl_inst = try gz.makeDeclaration(fn_proto.ast.proto_node);
    astgen.advanceSourceCursorToNode(decl_node);

    const saved_cursor = astgen.saveSourceCursor();

    const decl_column = astgen.source_column;

    // Set this now, since parameter types, return type, etc may be generic.
    const prev_within_fn = astgen.within_fn;
    defer astgen.within_fn = prev_within_fn;
    astgen.within_fn = true;

    const is_pub = fn_proto.visib_token != null;
    const is_export = blk: {
        const maybe_export_token = fn_proto.extern_export_inline_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_export_token) == .keyword_export;
    };
    const is_extern = blk: {
        const maybe_extern_token = fn_proto.extern_export_inline_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_extern_token) == .keyword_extern;
    };
    const has_inline_keyword = blk: {
        const maybe_inline_token = fn_proto.extern_export_inline_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_inline_token) == .keyword_inline;
    };
    const lib_name = if (fn_proto.lib_name) |lib_name_token| blk: {
        const lib_name_str = try astgen.strLitAsString(lib_name_token);
        const lib_name_slice = astgen.string_bytes.items[@intFromEnum(lib_name_str.index)..][0..lib_name_str.len];
        if (mem.findScalar(u8, lib_name_slice, 0) != null) {
            return astgen.failTok(lib_name_token, "library name cannot contain null bytes", .{});
        } else if (lib_name_str.len == 0) {
            return astgen.failTok(lib_name_token, "library name cannot be empty", .{});
        }
        break :blk lib_name_str.index;
    } else .empty;
    if (fn_proto.ast.callconv_expr != .none and has_inline_keyword) {
        return astgen.failNode(
            fn_proto.ast.callconv_expr.unwrap().?,
            "explicit callconv incompatible with inline keyword",
            .{},
        );
    }

    const return_type = fn_proto.ast.return_type.unwrap().?;
    const maybe_bang = tree.firstToken(return_type) - 1;
    const is_inferred_error = tree.tokenTag(maybe_bang) == .bang;
    if (body_node == .none) {
        if (!is_extern) {
            return astgen.failTok(fn_proto.ast.fn_token, "non-extern function has no body", .{});
        }
        if (is_inferred_error) {
            return astgen.failTok(maybe_bang, "function prototype may not have inferred error set", .{});
        }
    } else {
        assert(!is_extern); // validated by parser (TODO why???)
    }

    wip_decls.nextDecl(decl_inst);

    var type_gz: GenZir = .{
        .is_comptime = true,
        .decl_node_index = fn_proto.ast.proto_node,
        .decl_line = astgen.source_line,
        .parent = scope,
        .astgen = astgen,
        .instructions = gz.instructions,
        .instructions_top = gz.instructions.items.len,
    };
    defer type_gz.unstack();

    if (is_extern) {
        // We include a function *type*, not a value.
        const type_inst = try fnProtoExprInner(&type_gz, &type_gz.base, .{ .rl = .none }, decl_node, fn_proto, true);
        _ = try type_gz.addBreakWithSrcNode(.break_inline, decl_inst, type_inst, decl_node);
    }

    var align_gz = type_gz.makeSubBlock(scope);
    defer align_gz.unstack();

    if (fn_proto.ast.align_expr.unwrap()) |align_expr| {
        astgen.restoreSourceCursor(saved_cursor);
        const inst = try expr(&align_gz, &align_gz.base, coerced_align_ri, align_expr);
        _ = try align_gz.addBreakWithSrcNode(.break_inline, decl_inst, inst, decl_node);
    }

    var linksection_gz = align_gz.makeSubBlock(scope);
    defer linksection_gz.unstack();

    if (fn_proto.ast.section_expr.unwrap()) |section_expr| {
        astgen.restoreSourceCursor(saved_cursor);
        const inst = try expr(&linksection_gz, &linksection_gz.base, coerced_linksection_ri, section_expr);
        _ = try linksection_gz.addBreakWithSrcNode(.break_inline, decl_inst, inst, decl_node);
    }

    var addrspace_gz = linksection_gz.makeSubBlock(scope);
    defer addrspace_gz.unstack();

    if (fn_proto.ast.addrspace_expr.unwrap()) |addrspace_expr| {
        astgen.restoreSourceCursor(saved_cursor);
        const addrspace_ty = try addrspace_gz.addBuiltinValue(addrspace_expr, .address_space);
        const inst = try expr(&addrspace_gz, &addrspace_gz.base, .{ .rl = .{ .coerced_ty = addrspace_ty } }, addrspace_expr);
        _ = try addrspace_gz.addBreakWithSrcNode(.break_inline, decl_inst, inst, decl_node);
    }

    var value_gz = addrspace_gz.makeSubBlock(scope);
    defer value_gz.unstack();

    if (!is_extern) {
        // We include a function *value*, not a type.
        astgen.restoreSourceCursor(saved_cursor);
        try astgen.fnDeclInner(&value_gz, &value_gz.base, saved_cursor, decl_inst, decl_node, body_node.unwrap().?, fn_proto);
    }

    // *Now* we can incorporate the full source code into the hasher.
    astgen.src_hasher.update(tree.getNodeSource(decl_node));

    var hash: std.zig.SrcHash = undefined;
    astgen.src_hasher.final(&hash);
    try setDeclaration(decl_inst, .{
        .src_hash = hash,
        .src_line = type_gz.decl_line,
        .src_column = decl_column,

        .kind = .@"const",
        .name = try astgen.identAsString(fn_name_token),
        .is_pub = is_pub,
        .is_threadlocal = false,
        .linkage = if (is_extern) .@"extern" else if (is_export) .@"export" else .normal,
        .lib_name = lib_name,

        .type_gz = &type_gz,
        .align_gz = &align_gz,
        .linksection_gz = &linksection_gz,
        .addrspace_gz = &addrspace_gz,
        .value_gz = &value_gz,
    });
}

fn fnDeclInner(
    astgen: *AstGen,
    decl_gz: *GenZir,
    scope: *Scope,
    saved_cursor: SourceCursor,
    decl_inst: Zir.Inst.Index,
    decl_node: Ast.Node.Index,
    body_node: Ast.Node.Index,
    fn_proto: Ast.full.FnProto,
) InnerError!void {
    const tree = astgen.tree;

    const is_noinline = blk: {
        const maybe_noinline_token = fn_proto.extern_export_inline_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_noinline_token) == .keyword_noinline;
    };
    const has_inline_keyword = blk: {
        const maybe_inline_token = fn_proto.extern_export_inline_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_inline_token) == .keyword_inline;
    };

    const return_type = fn_proto.ast.return_type.unwrap().?;
    const maybe_bang = tree.firstToken(return_type) - 1;
    const is_inferred_error = tree.tokenTag(maybe_bang) == .bang;

    // Note that the capacity here may not be sufficient, as this does not include `anytype` parameters.
    var param_insts: std.ArrayList(Zir.Inst.Index) = try .initCapacity(astgen.arena, fn_proto.ast.params.len);

    // We use this as `is_used_or_discarded` to figure out if parameters / return types are generic.
    var any_param_used = false;

    var noalias_bits: u32 = 0;
    var params_scope = scope;
    const is_var_args = is_var_args: {
        var param_type_i: usize = 0;
        var it = fn_proto.iterate(tree);
        while (it.next()) |param| : (param_type_i += 1) {
            const is_comptime = if (param.comptime_noalias) |token| switch (tree.tokenTag(token)) {
                .keyword_noalias => is_comptime: {
                    noalias_bits |= @as(u32, 1) << (std.math.cast(u5, param_type_i) orelse
                        return astgen.failTok(token, "this compiler implementation only supports 'noalias' on the first 32 parameters", .{}));
                    break :is_comptime false;
                },
                .keyword_comptime => true,
                else => false,
            } else false;

            const is_anytype = if (param.anytype_ellipsis3) |token| blk: {
                switch (tree.tokenTag(token)) {
                    .keyword_anytype => break :blk true,
                    .ellipsis3 => break :is_var_args true,
                    else => unreachable,
                }
            } else false;

            const param_name: Zir.NullTerminatedString = if (param.name_token) |name_token| blk: {
                const name_bytes = tree.tokenSlice(name_token);
                if (mem.eql(u8, "_", name_bytes))
                    break :blk .empty;

                const param_name = try astgen.identAsString(name_token);
                try astgen.detectLocalShadowing(params_scope, param_name, name_token, name_bytes, .@"function parameter");
                break :blk param_name;
            } else {
                if (param.anytype_ellipsis3) |tok| {
                    return astgen.failTok(tok, "missing parameter name", .{});
                } else {
                    const type_expr = param.type_expr.?;
                    ambiguous: {
                        if (tree.nodeTag(type_expr) != .identifier) break :ambiguous;
                        const main_token = tree.nodeMainToken(type_expr);
                        const identifier_str = tree.tokenSlice(main_token);
                        if (isPrimitive(identifier_str)) break :ambiguous;
                        return astgen.failNodeNotes(
                            type_expr,
                            "missing parameter name or type",
                            .{},
                            &[_]u32{
                                try astgen.errNoteNode(
                                    type_expr,
                                    "if this is a name, annotate its type: '{s}: T'",
                                    .{identifier_str},
                                ),
                                try astgen.errNoteNode(
                                    type_expr,
                                    "if this is a type, give it a name: 'name: {s}'",
                                    .{identifier_str},
                                ),
                            },
                        );
                    }
                    return astgen.failNode(type_expr, "missing parameter name", .{});
                }
            };

            const param_inst = if (is_anytype) param: {
                const name_token = param.name_token orelse param.anytype_ellipsis3.?;
                const tag: Zir.Inst.Tag = if (is_comptime)
                    .param_anytype_comptime
                else
                    .param_anytype;
                break :param try decl_gz.addStrTok(tag, param_name, name_token);
            } else param: {
                const param_type_node = param.type_expr.?;
                any_param_used = false; // we will check this later
                var param_gz = decl_gz.makeSubBlock(scope);
                defer param_gz.unstack();
                const param_type = try fullBodyExpr(&param_gz, params_scope, coerced_type_ri, param_type_node, .normal);
                const param_inst_expected: Zir.Inst.Index = @enumFromInt(astgen.instructions.len + 1);
                _ = try param_gz.addBreakWithSrcNode(.break_inline, param_inst_expected, param_type, param_type_node);
                const param_type_is_generic = any_param_used;

                const name_token = param.name_token orelse tree.nodeMainToken(param_type_node);
                const tag: Zir.Inst.Tag = if (is_comptime) .param_comptime else .param;
                const param_inst = try decl_gz.addParam(&param_gz, param_insts.items, param_type_is_generic, tag, name_token, param_name);
                assert(param_inst_expected == param_inst);
                break :param param_inst.toRef();
            };

            if (param_name == .empty) continue;

            const sub_scope = try astgen.arena.create(Scope.LocalVal);
            sub_scope.* = .{
                .parent = params_scope,
                .gen_zir = decl_gz,
                .name = param_name,
                .inst = param_inst,
                .token_src = param.name_token.?,
                .id_cat = .@"function parameter",
                .is_used_or_discarded = &any_param_used,
            };
            params_scope = &sub_scope.base;
            try param_insts.append(astgen.arena, param_inst.toIndex().?);
        }
        break :is_var_args false;
    };

    // After creating the function ZIR instruction, it will need to update the break
    // instructions inside the expression blocks for cc and ret_ty to use the function
    // instruction as the body to break from.

    var ret_gz = decl_gz.makeSubBlock(params_scope);
    defer ret_gz.unstack();
    any_param_used = false; // we will check this later
    const ret_ref: Zir.Inst.Ref = inst: {
        // Parameters are in scope for the return type, so we use `params_scope` here.
        // The calling convention will not have parameters in scope, so we'll just use `scope`.
        // See #22263 for a proposal to solve the inconsistency here.
        const inst = try fullBodyExpr(&ret_gz, params_scope, coerced_type_ri, fn_proto.ast.return_type.unwrap().?, .normal);
        if (ret_gz.instructionsSlice().len == 0) {
            // In this case we will send a len=0 body which can be encoded more efficiently.
            break :inst inst;
        }
        _ = try ret_gz.addBreak(.break_inline, @enumFromInt(0), inst);
        break :inst inst;
    };
    const ret_body_param_refs = try astgen.fetchRemoveRefEntries(param_insts.items);
    const ret_ty_is_generic = any_param_used;

    // We're jumping back in source, so restore the cursor.
    astgen.restoreSourceCursor(saved_cursor);

    var cc_gz = decl_gz.makeSubBlock(scope);
    defer cc_gz.unstack();
    const cc_ref: Zir.Inst.Ref = blk: {
        if (fn_proto.ast.callconv_expr.unwrap()) |callconv_expr| {
            const inst = try expr(
                &cc_gz,
                scope,
                .{ .rl = .{ .coerced_ty = try cc_gz.addBuiltinValue(callconv_expr, .calling_convention) } },
                callconv_expr,
            );
            if (cc_gz.instructionsSlice().len == 0) {
                // In this case we will send a len=0 body which can be encoded more efficiently.
                break :blk inst;
            }
            _ = try cc_gz.addBreak(.break_inline, @enumFromInt(0), inst);
            break :blk inst;
        } else if (has_inline_keyword) {
            const inst = try cc_gz.addBuiltinValue(decl_node, .calling_convention_inline);
            _ = try cc_gz.addBreak(.break_inline, @enumFromInt(0), inst);
            break :blk inst;
        } else {
            break :blk .none;
        }
    };

    var body_gz: GenZir = .{
        .is_comptime = false,
        .decl_node_index = fn_proto.ast.proto_node,
        .decl_line = decl_gz.decl_line,
        .parent = params_scope,
        .astgen = astgen,
        .instructions = decl_gz.instructions,
        .instructions_top = decl_gz.instructions.items.len,
    };
    defer body_gz.unstack();

    // The scope stack looks like this:
    //  body_gz (top)
    //  param2
    //  param1
    //  param0
    //  decl_gz (bottom)

    // Construct the prototype hash.
    // Leave `astgen.src_hasher` unmodified; this will be used for hashing
    // the *whole* function declaration, including its body.
    var proto_hasher = astgen.src_hasher;
    const proto_node = tree.nodeData(decl_node).node_and_node[0];
    proto_hasher.update(tree.getNodeSource(proto_node));
    var proto_hash: std.zig.SrcHash = undefined;
    proto_hasher.final(&proto_hash);

    const prev_fn_block = astgen.fn_block;
    const prev_fn_ret_ty = astgen.fn_ret_ty;
    defer {
        astgen.fn_block = prev_fn_block;
        astgen.fn_ret_ty = prev_fn_ret_ty;
    }
    astgen.fn_block = &body_gz;
    astgen.fn_ret_ty = if (is_inferred_error or ret_ref.toIndex() != null) r: {
        // We're essentially guaranteed to need the return type at some point,
        // since the return type is likely not `void` or `noreturn` so there
        // will probably be an explicit return requiring RLS. Fetch this
        // return type now so the rest of the function can use it.
        break :r try body_gz.addNode(.ret_type, decl_node);
    } else ret_ref;

    const prev_var_args = astgen.fn_var_args;
    astgen.fn_var_args = is_var_args;
    defer astgen.fn_var_args = prev_var_args;

    astgen.advanceSourceCursorToNode(body_node);
    const lbrace_line = astgen.source_line - decl_gz.decl_line;
    const lbrace_column = astgen.source_column;

    _ = try fullBodyExpr(&body_gz, &body_gz.base, .{ .rl = .none }, body_node, .allow_branch_hint);
    try checkUsed(decl_gz, scope, params_scope);

    if (!body_gz.endsWithNoReturn()) {
        // As our last action before the return, "pop" the error trace if needed
        _ = try body_gz.addRestoreErrRetIndex(.ret, .always, decl_node);

        // Add implicit return at end of function.
        _ = try body_gz.addUnTok(.ret_implicit, .void_value, tree.lastToken(body_node));
    }

    const func_inst = try decl_gz.addFunc(.{
        .src_node = decl_node,
        .cc_ref = cc_ref,
        .cc_gz = &cc_gz,
        .ret_ref = ret_ref,
        .ret_gz = &ret_gz,
        .ret_param_refs = ret_body_param_refs,
        .ret_ty_is_generic = ret_ty_is_generic,
        .lbrace_line = lbrace_line,
        .lbrace_column = lbrace_column,
        .param_block = decl_inst,
        .param_insts = param_insts.items,
        .body_gz = &body_gz,
        .is_var_args = is_var_args,
        .is_inferred_error = is_inferred_error,
        .is_noinline = is_noinline,
        .noalias_bits = noalias_bits,
        .proto_hash = proto_hash,
    });
    _ = try decl_gz.addBreakWithSrcNode(.break_inline, decl_inst, func_inst, decl_node);
}

fn globalVarDecl(
    astgen: *AstGen,
    gz: *GenZir,
    scope: *Scope,
    wip_decls: *WipDecls,
    node: Ast.Node.Index,
    var_decl: Ast.full.VarDecl,
) InnerError!void {
    const tree = astgen.tree;

    const old_hasher = astgen.src_hasher;
    defer astgen.src_hasher = old_hasher;
    astgen.src_hasher = std.zig.SrcHasher.init(.{});
    astgen.src_hasher.update(tree.getNodeSource(node));
    astgen.src_hasher.update(std.mem.asBytes(&astgen.source_column));

    const is_mutable = tree.tokenTag(var_decl.ast.mut_token) == .keyword_var;
    const name_token = var_decl.ast.mut_token + 1;
    const is_pub = var_decl.visib_token != null;
    const is_export = blk: {
        const maybe_export_token = var_decl.extern_export_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_export_token) == .keyword_export;
    };
    const is_extern = blk: {
        const maybe_extern_token = var_decl.extern_export_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_extern_token) == .keyword_extern;
    };
    const is_threadlocal = if (var_decl.threadlocal_token) |tok| blk: {
        if (!is_mutable) {
            return astgen.failTok(tok, "threadlocal variable cannot be constant", .{});
        }
        break :blk true;
    } else false;
    const lib_name = if (var_decl.lib_name) |lib_name_token| blk: {
        const lib_name_str = try astgen.strLitAsString(lib_name_token);
        const lib_name_slice = astgen.string_bytes.items[@intFromEnum(lib_name_str.index)..][0..lib_name_str.len];
        if (mem.findScalar(u8, lib_name_slice, 0) != null) {
            return astgen.failTok(lib_name_token, "library name cannot contain null bytes", .{});
        } else if (lib_name_str.len == 0) {
            return astgen.failTok(lib_name_token, "library name cannot be empty", .{});
        }
        break :blk lib_name_str.index;
    } else .empty;

    astgen.advanceSourceCursorToNode(node);

    const decl_column = astgen.source_column;

    const decl_inst = try gz.makeDeclaration(node);
    wip_decls.nextDecl(decl_inst);

    if (var_decl.ast.init_node.unwrap()) |init_node| {
        if (is_extern) {
            return astgen.failNode(
                init_node,
                "extern variables have no initializers",
                .{},
            );
        }
    } else {
        if (!is_extern) {
            return astgen.failNode(node, "variables must be initialized", .{});
        }
    }

    if (is_extern and var_decl.ast.type_node == .none) {
        return astgen.failNode(node, "unable to infer variable type", .{});
    }

    assert(var_decl.comptime_token == null); // handled by parser

    var type_gz: GenZir = .{
        .parent = scope,
        .decl_node_index = node,
        .decl_line = astgen.source_line,
        .astgen = astgen,
        .is_comptime = true,
        .instructions = gz.instructions,
        .instructions_top = gz.instructions.items.len,
    };
    defer type_gz.unstack();

    if (var_decl.ast.type_node.unwrap()) |type_node| {
        const type_inst = try expr(&type_gz, &type_gz.base, coerced_type_ri, type_node);
        _ = try type_gz.addBreakWithSrcNode(.break_inline, decl_inst, type_inst, node);
    }

    var align_gz = type_gz.makeSubBlock(scope);
    defer align_gz.unstack();

    if (var_decl.ast.align_node.unwrap()) |align_node| {
        const align_inst = try expr(&align_gz, &align_gz.base, coerced_align_ri, align_node);
        _ = try align_gz.addBreakWithSrcNode(.break_inline, decl_inst, align_inst, node);
    }

    var linksection_gz = type_gz.makeSubBlock(scope);
    defer linksection_gz.unstack();

    if (var_decl.ast.section_node.unwrap()) |section_node| {
        const linksection_inst = try expr(&linksection_gz, &linksection_gz.base, coerced_linksection_ri, section_node);
        _ = try linksection_gz.addBreakWithSrcNode(.break_inline, decl_inst, linksection_inst, node);
    }

    var addrspace_gz = type_gz.makeSubBlock(scope);
    defer addrspace_gz.unstack();

    if (var_decl.ast.addrspace_node.unwrap()) |addrspace_node| {
        const addrspace_ty = try addrspace_gz.addBuiltinValue(addrspace_node, .address_space);
        const addrspace_inst = try expr(&addrspace_gz, &addrspace_gz.base, .{ .rl = .{ .coerced_ty = addrspace_ty } }, addrspace_node);
        _ = try addrspace_gz.addBreakWithSrcNode(.break_inline, decl_inst, addrspace_inst, node);
    }

    var init_gz = type_gz.makeSubBlock(scope);
    defer init_gz.unstack();

    if (var_decl.ast.init_node.unwrap()) |init_node| {
        const init_ri: ResultInfo = if (var_decl.ast.type_node != .none) .{
            .rl = .{ .coerced_ty = decl_inst.toRef() },
        } else .{ .rl = .none };
        const init_inst: Zir.Inst.Ref = try nameStratExpr(&init_gz, &init_gz.base, init_ri, init_node, .parent) orelse init: {
            break :init try expr(&init_gz, &init_gz.base, init_ri, init_node);
        };
        _ = try init_gz.addBreakWithSrcNode(.break_inline, decl_inst, init_inst, node);
    }

    var hash: std.zig.SrcHash = undefined;
    astgen.src_hasher.final(&hash);
    try setDeclaration(decl_inst, .{
        .src_hash = hash,
        .src_line = type_gz.decl_line,
        .src_column = decl_column,

        .kind = if (is_mutable) .@"var" else .@"const",
        .name = try astgen.identAsString(name_token),
        .is_pub = is_pub,
        .is_threadlocal = is_threadlocal,
        .linkage = if (is_extern) .@"extern" else if (is_export) .@"export" else .normal,
        .lib_name = lib_name,

        .type_gz = &type_gz,
        .align_gz = &align_gz,
        .linksection_gz = &linksection_gz,
        .addrspace_gz = &addrspace_gz,
        .value_gz = &init_gz,
    });
}

fn comptimeDecl(
    astgen: *AstGen,
    gz: *GenZir,
    scope: *Scope,
    wip_decls: *WipDecls,
    node: Ast.Node.Index,
) InnerError!void {
    const tree = astgen.tree;
    const body_node = tree.nodeData(node).node;

    const old_hasher = astgen.src_hasher;
    defer astgen.src_hasher = old_hasher;
    astgen.src_hasher = std.zig.SrcHasher.init(.{});
    astgen.src_hasher.update(tree.getNodeSource(node));
    astgen.src_hasher.update(std.mem.asBytes(&astgen.source_column));

    // Up top so the ZIR instruction index marks the start range of this
    // top-level declaration.
    const decl_inst = try gz.makeDeclaration(node);
    wip_decls.nextDecl(decl_inst);
    astgen.advanceSourceCursorToNode(node);

    // This is just needed for the `setDeclaration` call.
    var dummy_gz = gz.makeSubBlock(scope);
    defer dummy_gz.unstack();

    var comptime_gz: GenZir = .{
        .is_comptime = true,
        .decl_node_index = node,
        .decl_line = astgen.source_line,
        .parent = scope,
        .astgen = astgen,
        .instructions = dummy_gz.instructions,
        .instructions_top = dummy_gz.instructions.items.len,
    };
    defer comptime_gz.unstack();

    const decl_column = astgen.source_column;

    const block_result = try fullBodyExpr(&comptime_gz, &comptime_gz.base, .{ .rl = .none }, body_node, .normal);
    if (comptime_gz.isEmpty() or !comptime_gz.refIsNoReturn(block_result)) {
        _ = try comptime_gz.addBreak(.break_inline, decl_inst, .void_value);
    }

    var hash: std.zig.SrcHash = undefined;
    astgen.src_hasher.final(&hash);
    try setDeclaration(decl_inst, .{
        .src_hash = hash,
        .src_line = comptime_gz.decl_line,
        .src_column = decl_column,
        .kind = .@"comptime",
        .name = .empty,
        .is_pub = false,
        .is_threadlocal = false,
        .linkage = .normal,
        .type_gz = &dummy_gz,
        .align_gz = &dummy_gz,
        .linksection_gz = &dummy_gz,
        .addrspace_gz = &dummy_gz,
        .value_gz = &comptime_gz,
    });
}

fn testDecl(
    astgen: *AstGen,
    gz: *GenZir,
    scope: *Scope,
    wip_decls: *WipDecls,
    node: Ast.Node.Index,
) InnerError!void {
    const tree = astgen.tree;
    _, const body_node = tree.nodeData(node).opt_token_and_node;

    const old_hasher = astgen.src_hasher;
    defer astgen.src_hasher = old_hasher;
    astgen.src_hasher = std.zig.SrcHasher.init(.{});
    astgen.src_hasher.update(tree.getNodeSource(node));
    astgen.src_hasher.update(std.mem.asBytes(&astgen.source_column));

    // Up top so the ZIR instruction index marks the start range of this
    // top-level declaration.
    const decl_inst = try gz.makeDeclaration(node);

    wip_decls.nextDecl(decl_inst);
    astgen.advanceSourceCursorToNode(node);

    // This is just needed for the `setDeclaration` call.
    var dummy_gz: GenZir = gz.makeSubBlock(scope);
    defer dummy_gz.unstack();

    var decl_block: GenZir = .{
        .is_comptime = true,
        .decl_node_index = node,
        .decl_line = astgen.source_line,
        .parent = scope,
        .astgen = astgen,
        .instructions = dummy_gz.instructions,
        .instructions_top = dummy_gz.instructions.items.len,
    };
    defer decl_block.unstack();

    const decl_column = astgen.source_column;

    const test_token = tree.nodeMainToken(node);

    const test_name_token = test_token + 1;
    const test_name: Zir.NullTerminatedString = switch (tree.tokenTag(test_name_token)) {
        else => .empty,
        .string_literal => name: {
            const name = try astgen.strLitAsString(test_name_token);
            const slice = astgen.string_bytes.items[@intFromEnum(name.index)..][0..name.len];
            if (mem.findScalar(u8, slice, 0) != null) {
                return astgen.failTok(test_name_token, "test name cannot contain null bytes", .{});
            } else if (slice.len == 0) {
                return astgen.failTok(test_name_token, "empty test name must be omitted", .{});
            }
            break :name name.index;
        },
        .identifier => name: {
            const ident_name_raw = tree.tokenSlice(test_name_token);

            if (mem.eql(u8, ident_name_raw, "_")) return astgen.failTok(test_name_token, "'_' used as an identifier without @\"_\" syntax", .{});

            // if not @"" syntax, just use raw token slice
            if (ident_name_raw[0] != '@') {
                if (isPrimitive(ident_name_raw)) return astgen.failTok(test_name_token, "cannot test a primitive", .{});
            }

            // Local variables, including function parameters.
            const name_str_index = try astgen.identAsString(test_name_token);
            var found_already: ?Ast.Node.Index = null; // we have found a decl with the same name already
            var num_namespaces_out: u32 = 0;
            var capturing_namespace: ?*Scope.Namespace = null;
            find_scope: switch (scope.unwrap()) {
                .local_val => |local_val| {
                    if (local_val.name == name_str_index) {
                        local_val.used = .fromToken(test_name_token);
                        return astgen.failTokNotes(test_name_token, "cannot test a {s}", .{
                            @tagName(local_val.id_cat),
                        }, &[_]u32{
                            try astgen.errNoteTok(local_val.token_src, "{s} declared here", .{
                                @tagName(local_val.id_cat),
                            }),
                        });
                    }
                    continue :find_scope local_val.parent.unwrap();
                },
                .local_ptr => |local_ptr| {
                    if (local_ptr.name == name_str_index) {
                        local_ptr.used = .fromToken(test_name_token);
                        return astgen.failTokNotes(test_name_token, "cannot test a {s}", .{
                            @tagName(local_ptr.id_cat),
                        }, &[_]u32{
                            try astgen.errNoteTok(local_ptr.token_src, "{s} declared here", .{
                                @tagName(local_ptr.id_cat),
                            }),
                        });
                    }
                    continue :find_scope local_ptr.parent.unwrap();
                },
                .gen_zir => |gen_zir| continue :find_scope gen_zir.parent.unwrap(),
                .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
                .namespace => |ns| {
                    if (ns.decls.get(name_str_index)) |i| {
                        if (found_already) |f| {
                            return astgen.failTokNotes(test_name_token, "ambiguous reference", .{}, &.{
                                try astgen.errNoteNode(f, "declared here", .{}),
                                try astgen.errNoteNode(i, "also declared here", .{}),
                            });
                        }
                        // We found a match but must continue looking for ambiguous references to decls.
                        found_already = i;
                    }
                    num_namespaces_out += 1;
                    capturing_namespace = ns;
                    continue :find_scope ns.parent.unwrap();
                },
                .top => break :find_scope,
            }
            if (found_already == null) {
                const ident_name = try astgen.identifierTokenString(test_name_token);
                return astgen.failTok(test_name_token, "use of undeclared identifier '{s}'", .{ident_name});
            }

            break :name try astgen.identAsString(test_name_token);
        },
    };

    var fn_block: GenZir = .{
        .is_comptime = false,
        .decl_node_index = node,
        .decl_line = decl_block.decl_line,
        .parent = &decl_block.base,
        .astgen = astgen,
        .instructions = decl_block.instructions,
        .instructions_top = decl_block.instructions.items.len,
    };
    defer fn_block.unstack();

    const prev_within_fn = astgen.within_fn;
    const prev_fn_block = astgen.fn_block;
    const prev_fn_ret_ty = astgen.fn_ret_ty;
    astgen.within_fn = true;
    astgen.fn_block = &fn_block;
    astgen.fn_ret_ty = .anyerror_void_error_union_type;
    defer {
        astgen.within_fn = prev_within_fn;
        astgen.fn_block = prev_fn_block;
        astgen.fn_ret_ty = prev_fn_ret_ty;
    }

    astgen.advanceSourceCursorToNode(body_node);
    const lbrace_line = astgen.source_line - decl_block.decl_line;
    const lbrace_column = astgen.source_column;

    const block_result = try fullBodyExpr(&fn_block, &fn_block.base, .{ .rl = .none }, body_node, .normal);
    if (fn_block.isEmpty() or !fn_block.refIsNoReturn(block_result)) {

        // As our last action before the return, "pop" the error trace if needed
        _ = try fn_block.addRestoreErrRetIndex(.ret, .always, node);

        // Add implicit return at end of function.
        _ = try fn_block.addUnTok(.ret_implicit, .void_value, tree.lastToken(body_node));
    }

    const func_inst = try decl_block.addFunc(.{
        .src_node = node,

        .cc_ref = .none,
        .cc_gz = null,
        .ret_ref = .anyerror_void_error_union_type,
        .ret_gz = null,

        .ret_param_refs = &.{},
        .param_insts = &.{},
        .ret_ty_is_generic = false,

        .lbrace_line = lbrace_line,
        .lbrace_column = lbrace_column,
        .param_block = decl_inst,
        .body_gz = &fn_block,
        .is_var_args = false,
        .is_inferred_error = false,
        .is_noinline = false,
        .noalias_bits = 0,

        // Tests don't have a prototype that needs hashing
        .proto_hash = .{0} ** 16,
    });

    _ = try decl_block.addBreak(.break_inline, decl_inst, func_inst);

    var hash: std.zig.SrcHash = undefined;
    astgen.src_hasher.final(&hash);
    try setDeclaration(decl_inst, .{
        .src_hash = hash,
        .src_line = decl_block.decl_line,
        .src_column = decl_column,

        .kind = switch (tree.tokenTag(test_name_token)) {
            .string_literal => .@"test",
            .identifier => .decltest,
            else => .unnamed_test,
        },
        .name = test_name,
        .is_pub = false,
        .is_threadlocal = false,
        .linkage = .normal,

        .type_gz = &dummy_gz,
        .align_gz = &dummy_gz,
        .linksection_gz = &dummy_gz,
        .addrspace_gz = &dummy_gz,
        .value_gz = &decl_block,
    });
}

fn structDeclInner(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    container_decl: Ast.full.ContainerDecl,
    layout: std.builtin.Type.ContainerLayout,
    maybe_backing_int_node: Ast.Node.OptionalIndex,
    name_strat: Zir.Inst.NameStrategy,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;
    const tree = astgen.tree;

    is_tuple: {
        const tuple_field_node = for (container_decl.ast.members) |member_node| {
            const container_field = tree.fullContainerField(member_node) orelse continue;
            if (container_field.ast.tuple_like) break member_node;
        } else break :is_tuple;

        if (node == .root) {
            return astgen.failNode(tuple_field_node, "file cannot be a tuple", .{});
        } else {
            return tupleDecl(gz, scope, node, container_decl, layout, maybe_backing_int_node);
        }
    }

    astgen.advanceSourceCursorToNode(node);

    const decl_inst = try gz.reserveInstructionIndex();

    if (container_decl.ast.members.len == 0 and maybe_backing_int_node == .none) {
        try gz.setStruct(decl_inst, .{
            .src_node = node,
            .name_strat = name_strat,
            .layout = layout,
            .backing_int_type_body_len = null,
            .decls_len = 0,
            .fields_len = 0,
            .any_field_aligns = false,
            .any_field_defaults = false,
            .any_comptime_fields = false,
            .fields_hash = @splat(0),
            .captures = &.{},
            .capture_names = &.{},
            .remaining = &.{},
        });
        return decl_inst.toRef();
    }

    var namespace: Scope.Namespace = .{
        .parent = scope,
        .node = node,
        .inst = decl_inst,
        .declaring_gz = gz,
        .maybe_generic = astgen.within_fn,
    };
    defer namespace.deinit(gpa);

    // The struct_decl instruction introduces a scope in which the decls of the struct
    // are in scope, so that field types, alignments, and default value expressions
    // can refer to decls within the struct itself.
    var block_scope: GenZir = .{
        .parent = &namespace.base,
        .decl_node_index = node,
        .decl_line = gz.decl_line,
        .astgen = astgen,
        .is_comptime = true,
        .instructions = gz.instructions,
        .instructions_top = gz.instructions.items.len,
    };
    defer block_scope.unstack();

    const scan_result = try astgen.scanContainer(&namespace, container_decl.ast.members, .@"struct");

    var scratch: Scratch = .init(astgen);
    defer scratch.reset();

    // Replicate the structure of the ZIR trailing data in `scratch`
    var wip_decls: WipDecls = try .init(&scratch, scan_result.decls_len);
    const field_names = try scratch.addSlice(scan_result.fields_len);
    const field_type_body_lens = try scratch.addSlice(scan_result.fields_len);
    const field_align_body_lens = try scratch.addOptionalSlice(scan_result.any_field_aligns, scan_result.fields_len);
    const field_default_body_lens = try scratch.addOptionalSlice(scan_result.any_field_values, scan_result.fields_len);
    const field_comptime_bits = try scratch.addOptionalSlice(
        scan_result.any_comptime_fields,
        std.math.divCeil(u32, scan_result.fields_len, 32) catch unreachable,
    );
    if (field_comptime_bits) |bits| @memset(bits.get(astgen), 0);

    // Before any field bodies comes the backing int type, if specified.
    const backing_int_type_body_len: ?u32 = if (maybe_backing_int_node.unwrap()) |backing_int_node| len: {
        if (layout != .@"packed") return astgen.failNode(
            backing_int_node,
            "non-packed struct does not support backing integer type",
            .{},
        );
        const type_ref = try typeExpr(&block_scope, &namespace.base, backing_int_node);
        if (!block_scope.endsWithNoReturn()) {
            _ = try block_scope.addBreak(.break_inline, decl_inst, type_ref);
        }
        const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
        block_scope.instructions.items.len = block_scope.instructions_top;
        break :len body_len;
    } else null;

    const old_hasher = astgen.src_hasher;
    defer astgen.src_hasher = old_hasher;
    astgen.src_hasher = .init(.{});

    var next_field_idx: u32 = 0;
    for (container_decl.ast.members) |member_node| {
        var member = switch (try containerMember(&block_scope, &namespace.base, &wip_decls, member_node)) {
            .decl => continue,
            .field => |field| field,
        };
        const field_idx = next_field_idx;
        next_field_idx += 1;

        astgen.src_hasher.update(tree.getNodeSource(member_node));

        member.convertToNonTupleLike(astgen.tree);
        assert(!member.ast.tuple_like);

        field_names.get(astgen)[field_idx] = @intFromEnum(try astgen.identAsString(member.ast.main_token));

        {
            const type_node = member.ast.type_expr.unwrap() orelse {
                return astgen.failTok(member.ast.main_token, "struct field missing type", .{});
            };
            const type_ref = try typeExpr(&block_scope, &namespace.base, type_node);
            if (!block_scope.endsWithNoReturn()) {
                _ = try block_scope.addBreak(.break_inline, decl_inst, type_ref);
            }
            const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
            field_type_body_lens.get(astgen)[field_idx] = body_len;
            block_scope.instructions.items.len = block_scope.instructions_top;
        }

        if (member.ast.align_expr.unwrap()) |align_node| {
            if (layout == .@"packed") {
                return astgen.failNode(align_node, "unable to override alignment of packed struct fields", .{});
            }
            const align_ref = try expr(&block_scope, &namespace.base, coerced_align_ri, align_node);
            if (!block_scope.endsWithNoReturn()) {
                _ = try block_scope.addBreak(.break_inline, decl_inst, align_ref);
            }
            const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
            field_align_body_lens.?.get(astgen)[field_idx] = body_len;
            block_scope.instructions.items.len = block_scope.instructions_top;
        } else if (field_align_body_lens) |lens| {
            lens.get(astgen)[field_idx] = 0;
        }

        if (member.ast.value_expr.unwrap()) |default_node| {
            const ri: ResultInfo = .{ .rl = .{ .coerced_ty = decl_inst.toRef() } };
            const default_ref = try expr(&block_scope, &namespace.base, ri, default_node);
            if (!block_scope.endsWithNoReturn()) {
                _ = try block_scope.addBreak(.break_inline, decl_inst, default_ref);
            }
            const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
            field_default_body_lens.?.get(astgen)[field_idx] = body_len;
            block_scope.instructions.items.len = block_scope.instructions_top;
        } else if (field_default_body_lens) |lens| {
            lens.get(astgen)[field_idx] = 0;
        }

        if (member.comptime_token) |comptime_token| {
            switch (layout) {
                .@"packed", .@"extern" => return astgen.failTok(comptime_token, "{s} struct fields cannot be marked comptime", .{@tagName(layout)}),
                .auto => {},
            }
            if (member.ast.value_expr == .none) {
                return astgen.failTok(comptime_token, "comptime field without default initialization value", .{});
            }
            const mask = @as(u32, 1) << @intCast(field_idx % 32);
            field_comptime_bits.?.get(astgen)[field_idx / 32] |= mask;
        }
    }
    assert(next_field_idx == scan_result.fields_len);
    wip_decls.finish();

    var fields_hash: std.zig.SrcHash = undefined;
    astgen.src_hasher.final(&fields_hash);

    try gz.setStruct(decl_inst, .{
        .src_node = node,
        .name_strat = name_strat,
        .layout = layout,
        .backing_int_type_body_len = backing_int_type_body_len,
        .decls_len = scan_result.decls_len,
        .fields_len = scan_result.fields_len,
        .any_field_aligns = scan_result.any_field_aligns,
        .any_field_defaults = scan_result.any_field_values,
        .any_comptime_fields = scan_result.any_comptime_fields,
        .fields_hash = fields_hash,
        .captures = namespace.captures.keys(),
        .capture_names = namespace.captures.values(),
        .remaining = scratch.all().get(astgen),
    });

    block_scope.unstack();
    return decl_inst.toRef();
}

fn tupleDecl(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    container_decl: Ast.full.ContainerDecl,
    layout: std.builtin.Type.ContainerLayout,
    backing_int_node: Ast.Node.OptionalIndex,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;
    const tree = astgen.tree;

    switch (layout) {
        .auto => {},
        .@"extern", .@"packed" => return astgen.failNode(node, "{s} tuples are not supported", .{@tagName(layout)}),
    }

    if (backing_int_node.unwrap()) |arg| {
        return astgen.failNode(arg, "tuple does not support backing integer type", .{});
    }

    // We will use the scratch buffer, starting here, for the field data:
    // 1. fields: { // for every `fields_len` (stored in `extended.small`)
    //        type: Inst.Ref,
    //        init: Inst.Ref, // `.none` for non-`comptime` fields
    //    }
    const fields_start = astgen.scratch.items.len;
    defer astgen.scratch.items.len = fields_start;

    try astgen.scratch.ensureUnusedCapacity(gpa, container_decl.ast.members.len * 2);

    for (container_decl.ast.members) |member_node| {
        const field = tree.fullContainerField(member_node) orelse {
            const tuple_member = for (container_decl.ast.members) |maybe_tuple| switch (tree.nodeTag(maybe_tuple)) {
                .container_field_init,
                .container_field_align,
                .container_field,
                => break maybe_tuple,
                else => {},
            } else unreachable;
            return astgen.failNodeNotes(
                member_node,
                "tuple declarations cannot contain declarations",
                .{},
                &.{try astgen.errNoteNode(tuple_member, "tuple field here", .{})},
            );
        };

        if (!field.ast.tuple_like) {
            return astgen.failTok(field.ast.main_token, "tuple field has a name", .{});
        }

        if (field.ast.align_expr != .none) {
            return astgen.failTok(field.ast.main_token, "tuple field has alignment", .{});
        }

        if (field.ast.value_expr != .none and field.comptime_token == null) {
            return astgen.failTok(field.ast.main_token, "non-comptime tuple field has default initialization value", .{});
        }

        if (field.ast.value_expr == .none and field.comptime_token != null) {
            return astgen.failTok(field.comptime_token.?, "comptime field without default initialization value", .{});
        }

        const field_type_ref = try typeExpr(gz, scope, field.ast.type_expr.unwrap().?);
        astgen.scratch.appendAssumeCapacity(@intFromEnum(field_type_ref));

        if (field.ast.value_expr.unwrap()) |value_expr| {
            const field_init_ref = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = field_type_ref } }, value_expr, .tuple_field_default_value);
            astgen.scratch.appendAssumeCapacity(@intFromEnum(field_init_ref));
        } else {
            astgen.scratch.appendAssumeCapacity(@intFromEnum(Zir.Inst.Ref.none));
        }
    }

    const fields_len = std.math.cast(u16, container_decl.ast.members.len) orelse {
        return astgen.failNode(node, "this compiler implementation only supports 65535 tuple fields", .{});
    };

    const extra_trail = astgen.scratch.items[fields_start..];
    assert(extra_trail.len == fields_len * 2);
    try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.TupleDecl).@"struct".fields.len + extra_trail.len);
    const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.TupleDecl{
        .src_node = gz.nodeIndexToRelative(node),
    });
    astgen.extra.appendSliceAssumeCapacity(extra_trail);

    return gz.add(.{
        .tag = .extended,
        .data = .{ .extended = .{
            .opcode = .tuple_decl,
            .small = fields_len,
            .operand = payload_index,
        } },
    });
}

fn unionDeclInner(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
    members: []const Ast.Node.Index,
    layout: std.builtin.Type.ContainerLayout,
    opt_arg_node: Ast.Node.OptionalIndex,
    auto_enum_tok: ?Ast.TokenIndex,
    name_strat: Zir.Inst.NameStrategy,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;

    const explicit_int_or_enum_tag = switch (layout) {
        .auto => opt_arg_node != .none,
        .@"extern" => if (opt_arg_node.unwrap()) |arg_node| {
            return astgen.failNode(arg_node, "{s} union does not support enum tag type", .{@tagName(layout)});
        } else false,
        .@"packed" => false,
    };

    if (auto_enum_tok) |t| {
        if (layout != .auto) {
            return astgen.failTok(t, "{s} union does not support enum tag type", .{@tagName(layout)});
        }
    }

    const is_tagged = explicit_int_or_enum_tag or auto_enum_tok != null;

    astgen.advanceSourceCursorToNode(node);

    const decl_inst = try gz.reserveInstructionIndex();

    var namespace: Scope.Namespace = .{
        .parent = scope,
        .node = node,
        .inst = decl_inst,
        .declaring_gz = gz,
        .maybe_generic = astgen.within_fn,
    };
    defer namespace.deinit(gpa);

    // The union_decl instruction introduces a scope in which the decls of the union
    // are in scope, so that field types, alignments, and default value expressions
    // can refer to decls within the union itself.
    var block_scope: GenZir = .{
        .parent = &namespace.base,
        .decl_node_index = node,
        .decl_line = gz.decl_line,
        .astgen = astgen,
        .is_comptime = true,
        .instructions = gz.instructions,
        .instructions_top = gz.instructions.items.len,
    };
    defer block_scope.unstack();

    const scan_result = try astgen.scanContainer(&namespace, members, .@"union");

    var scratch: Scratch = .init(astgen);
    defer scratch.reset();

    // Replicate the structure of the ZIR trailing data in `scratch`
    var wip_decls: WipDecls = try .init(&scratch, scan_result.decls_len);
    const field_names = try scratch.addSlice(scan_result.fields_len);
    const field_type_body_lens = try scratch.addSlice(scan_result.fields_len);
    const field_align_body_lens = try scratch.addOptionalSlice(scan_result.any_field_aligns, scan_result.fields_len);
    const field_value_body_lens = try scratch.addOptionalSlice(scan_result.any_field_values, scan_result.fields_len);

    // Before any field bodies comes the tag/backing type, if specified.
    const arg_type_body_len: ?u32 = if (opt_arg_node.unwrap()) |arg_node| len: {
        const type_ref = try typeExpr(&block_scope, &namespace.base, arg_node);
        if (!block_scope.endsWithNoReturn()) {
            _ = try block_scope.addBreak(.break_inline, decl_inst, type_ref);
        }
        const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
        block_scope.instructions.items.len = block_scope.instructions_top;
        break :len body_len;
    } else null;

    const old_hasher = astgen.src_hasher;
    defer astgen.src_hasher = old_hasher;
    astgen.src_hasher = .init(.{});

    var next_field_idx: u32 = 0;
    for (members) |member_node| {
        var member = switch (try containerMember(&block_scope, &namespace.base, &wip_decls, member_node)) {
            .decl => continue,
            .field => |field| field,
        };
        const field_idx = next_field_idx;
        next_field_idx += 1;

        astgen.src_hasher.update(astgen.tree.getNodeSource(member_node));
        member.convertToNonTupleLike(astgen.tree);
        if (member.ast.tuple_like) {
            return astgen.failTok(member.ast.main_token, "union field missing name", .{});
        }
        if (member.comptime_token) |comptime_token| {
            return astgen.failTok(comptime_token, "union fields cannot be marked comptime", .{});
        }

        field_names.get(astgen)[field_idx] = @intFromEnum(try astgen.identAsString(member.ast.main_token));

        if (member.ast.type_expr.unwrap()) |type_node| {
            const type_ref = try typeExpr(&block_scope, &namespace.base, type_node);
            if (!block_scope.endsWithNoReturn()) {
                _ = try block_scope.addBreak(.break_inline, decl_inst, type_ref);
            }
            const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
            field_type_body_lens.get(astgen)[field_idx] = body_len;
            block_scope.instructions.items.len = block_scope.instructions_top;
        } else if (!is_tagged) {
            return astgen.failNode(member_node, "union field missing type", .{});
        } else {
            field_type_body_lens.get(astgen)[field_idx] = 0;
        }

        if (member.ast.align_expr.unwrap()) |align_node| {
            if (layout == .@"packed") {
                return astgen.failNode(align_node, "unable to override alignment of packed union fields", .{});
            }
            const align_ref = try expr(&block_scope, &namespace.base, coerced_align_ri, align_node);
            if (!block_scope.endsWithNoReturn()) {
                _ = try block_scope.addBreak(.break_inline, decl_inst, align_ref);
            }
            const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
            field_align_body_lens.?.get(astgen)[field_idx] = body_len;
            block_scope.instructions.items.len = block_scope.instructions_top;
        } else if (field_align_body_lens) |lens| {
            lens.get(astgen)[field_idx] = 0;
        }

        if (member.ast.value_expr.unwrap()) |value_node| {
            if (!explicit_int_or_enum_tag) return astgen.failNodeNotes(
                node,
                "explicitly valued tagged union missing integer tag type",
                .{},
                &.{try astgen.errNoteNode(value_node, "tag value specified here", .{})},
            );
            if (auto_enum_tok == null) return astgen.failNodeNotes(
                node,
                "explicitly valued tagged union requires inferred enum tag type",
                .{},
                &.{try astgen.errNoteNode(value_node, "tag value specified here", .{})},
            );
            const ri: ResultInfo = .{ .rl = .{ .coerced_ty = decl_inst.toRef() } };
            const value_ref = try expr(&block_scope, &namespace.base, ri, value_node);
            if (!block_scope.endsWithNoReturn()) {
                _ = try block_scope.addBreak(.break_inline, decl_inst, value_ref);
            }
            const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
            field_value_body_lens.?.get(astgen)[field_idx] = body_len;
            block_scope.instructions.items.len = block_scope.instructions_top;
        } else if (field_value_body_lens) |lens| {
            lens.get(astgen)[field_idx] = 0;
        }
    }
    assert(next_field_idx == scan_result.fields_len);
    wip_decls.finish();

    var fields_hash: std.zig.SrcHash = undefined;
    astgen.src_hasher.final(&fields_hash);

    try gz.setUnion(decl_inst, .{
        .src_node = node,
        .name_strat = name_strat,
        .kind = switch (layout) {
            .auto => if (auto_enum_tok == null) l: {
                break :l if (opt_arg_node == .none) .auto else .tagged_explicit;
            } else l: {
                break :l if (opt_arg_node == .none) .tagged_enum else .tagged_enum_explicit;
            },
            .@"extern" => .@"extern",
            .@"packed" => if (opt_arg_node != .none) .packed_explicit else .@"packed",
        },
        .arg_type_body_len = arg_type_body_len,
        .decls_len = scan_result.decls_len,
        .fields_len = scan_result.fields_len,
        .any_field_aligns = scan_result.any_field_aligns,
        .any_field_values = scan_result.any_field_values,
        .fields_hash = fields_hash,
        .captures = namespace.captures.keys(),
        .capture_names = namespace.captures.values(),
        .remaining = scratch.all().get(astgen),
    });

    block_scope.unstack();
    return decl_inst.toRef();
}

fn containerDecl(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    container_decl: Ast.full.ContainerDecl,
    name_strat: Zir.Inst.NameStrategy,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;
    const tree = astgen.tree;

    const prev_fn_block = astgen.fn_block;
    astgen.fn_block = null;
    defer astgen.fn_block = prev_fn_block;

    // We must not create any types until Sema. Here the goal is only to generate
    // ZIR for all the field types, alignments, and default value expressions.

    switch (tree.tokenTag(container_decl.ast.main_token)) {
        .keyword_struct => {
            const layout: std.builtin.Type.ContainerLayout = if (container_decl.layout_token) |t| switch (tree.tokenTag(t)) {
                .keyword_packed => .@"packed",
                .keyword_extern => .@"extern",
                else => unreachable,
            } else .auto;

            const result = try structDeclInner(gz, scope, node, container_decl, layout, container_decl.ast.arg, name_strat);
            return rvalue(gz, ri, result, node);
        },
        .keyword_union => {
            const layout: std.builtin.Type.ContainerLayout = if (container_decl.layout_token) |t| switch (tree.tokenTag(t)) {
                .keyword_packed => .@"packed",
                .keyword_extern => .@"extern",
                else => unreachable,
            } else .auto;

            const result = try unionDeclInner(gz, scope, node, container_decl.ast.members, layout, container_decl.ast.arg, container_decl.ast.enum_token, name_strat);
            return rvalue(gz, ri, result, node);
        },
        .keyword_enum => {
            if (container_decl.layout_token) |t| {
                return astgen.failTok(t, "enums do not support 'packed' or 'extern'; instead provide an explicit integer tag type", .{});
            }

            astgen.advanceSourceCursorToNode(node);

            const decl_inst = try gz.reserveInstructionIndex();

            var namespace: Scope.Namespace = .{
                .parent = scope,
                .node = node,
                .inst = decl_inst,
                .declaring_gz = gz,
                .maybe_generic = astgen.within_fn,
            };
            defer namespace.deinit(gpa);

            // The enum_decl instruction introduces a scope in which the decls of the enum
            // are in scope, so that tag values can refer to decls within the enum itself.
            var block_scope: GenZir = .{
                .parent = &namespace.base,
                .decl_node_index = node,
                .decl_line = gz.decl_line,
                .astgen = astgen,
                .is_comptime = true,
                .instructions = gz.instructions,
                .instructions_top = gz.instructions.items.len,
            };
            defer block_scope.unstack();

            const scan_result = try astgen.scanContainer(&namespace, container_decl.ast.members, .@"enum");
            // The name `_` is not actually a field; it marks a non-exhaustive enum.
            const fields_len: u32 = scan_result.fields_len - @intFromBool(scan_result.has_underscore_field);

            var scratch: Scratch = .init(astgen);
            defer scratch.reset();

            // Replicate the structure of the ZIR trailing data in `scratch`
            var wip_decls: WipDecls = try .init(&scratch, scan_result.decls_len);
            const field_names = try scratch.addSlice(fields_len);
            const field_value_body_lens = try scratch.addOptionalSlice(scan_result.any_field_values, fields_len);

            // Before any field bodies comes the tag type, if specified.
            const tag_type_body_len: ?u32 = if (container_decl.ast.arg.unwrap()) |tag_type_node| len: {
                const type_ref = try typeExpr(&block_scope, &namespace.base, tag_type_node);
                if (!block_scope.endsWithNoReturn()) {
                    _ = try block_scope.addBreak(.break_inline, decl_inst, type_ref);
                }
                const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
                block_scope.instructions.items.len = block_scope.instructions_top;
                break :len body_len;
            } else null;

            const old_hasher = astgen.src_hasher;
            defer astgen.src_hasher = old_hasher;
            astgen.src_hasher = .init(.{});

            var next_field_idx: u32 = 0;
            var opt_nonexhaustive_node: Ast.Node.OptionalIndex = .none;
            for (container_decl.ast.members) |member_node| {
                var member = switch (try containerMember(&block_scope, &namespace.base, &wip_decls, member_node)) {
                    .decl => continue,
                    .field => |field| field,
                };
                member.convertToNonTupleLike(astgen.tree);
                if (member.ast.tuple_like) return astgen.failTok(member.ast.main_token, "enum field missing name", .{});
                if (member.comptime_token) |t| return astgen.failTok(t, "enum fields cannot be marked comptime", .{});
                if (member.ast.type_expr.unwrap()) |type_node| {
                    return astgen.failNodeNotes(type_node, "enum fields do not have types", .{}, &.{
                        try astgen.errNoteNode(node, "consider 'union(enum)' here to make it a tagged union", .{}),
                    });
                }
                if (member.ast.align_expr.unwrap()) |n| return astgen.failNode(n, "enum fields cannot be aligned", .{});
                if (mem.eql(u8, tree.tokenSlice(member.ast.main_token), "_")) {
                    // non-exhaustive mark
                    assert(scan_result.has_underscore_field);
                    if (opt_nonexhaustive_node.unwrap()) |prev_node| {
                        return astgen.failNodeNotes(member_node, "redundant non-exhaustive enum mark", .{}, &.{
                            try astgen.errNoteNode(prev_node, "other mark here", .{}),
                        });
                    }
                    if (member.ast.value_expr.unwrap()) |value_node| {
                        return astgen.failNode(value_node, "'_' is used to mark an enum as non-exhaustive and cannot be assigned a value", .{});
                    }
                    if (next_field_idx != fields_len) {
                        return astgen.failNode(member_node, "'_' field of non-exhaustive enum must be last", .{});
                    }
                    if (tag_type_body_len == null) {
                        return astgen.failNodeNotes(node, "non-exhaustive enum missing integer tag type", .{}, &.{
                            try astgen.errNoteNode(member_node, "marked non-exhaustive here", .{}),
                        });
                    }
                    opt_nonexhaustive_node = member_node.toOptional();
                    continue;
                }

                // This is a real field rather than a non-exhaustive mark.
                const field_idx = next_field_idx;
                next_field_idx += 1;

                astgen.src_hasher.update(tree.getNodeSource(member_node));

                field_names.get(astgen)[field_idx] = @intFromEnum(try astgen.identAsString(member.ast.main_token));

                if (member.ast.value_expr.unwrap()) |value_node| {
                    if (tag_type_body_len == null) {
                        return astgen.failNodeNotes(node, "explicitly valued enum missing integer tag type", .{}, &.{
                            try astgen.errNoteNode(value_node, "tag value specified here", .{}),
                        });
                    }
                    const val_ri: ResultInfo = .{ .rl = .{ .coerced_ty = decl_inst.toRef() } };
                    const value_ref = try expr(&block_scope, &namespace.base, val_ri, value_node);
                    if (!block_scope.endsWithNoReturn()) {
                        _ = try block_scope.addBreak(.break_inline, decl_inst, value_ref);
                    }
                    const body_len = try scratch.appendBodyWithFixups(block_scope.instructionsSlice());
                    field_value_body_lens.?.get(astgen)[field_idx] = body_len;
                    block_scope.instructions.items.len = block_scope.instructions_top;
                } else if (field_value_body_lens) |lens| {
                    lens.get(astgen)[field_idx] = 0;
                }
            }
            assert(scan_result.has_underscore_field == (opt_nonexhaustive_node != .none));
            assert(next_field_idx == fields_len);
            wip_decls.finish();

            var fields_hash: std.zig.SrcHash = undefined;
            astgen.src_hasher.final(&fields_hash);

            try gz.setEnum(decl_inst, .{
                .src_node = node,
                .name_strat = name_strat,
                .tag_type_body_len = tag_type_body_len,
                .nonexhaustive = scan_result.has_underscore_field,
                .decls_len = scan_result.decls_len,
                .fields_len = fields_len,
                .any_field_values = scan_result.any_field_values,
                .fields_hash = fields_hash,
                .captures = namespace.captures.keys(),
                .capture_names = namespace.captures.values(),
                .remaining = scratch.all().get(astgen),
            });

            block_scope.unstack();
            return rvalue(gz, ri, decl_inst.toRef(), node);
        },
        .keyword_opaque => {
            assert(container_decl.ast.arg == .none);

            astgen.advanceSourceCursorToNode(node);

            const decl_inst = try gz.reserveInstructionIndex();

            var namespace: Scope.Namespace = .{
                .parent = scope,
                .node = node,
                .inst = decl_inst,
                .declaring_gz = gz,
                .maybe_generic = astgen.within_fn,
            };
            defer namespace.deinit(gpa);

            var block_scope: GenZir = .{
                .parent = &namespace.base,
                .decl_node_index = node,
                .decl_line = gz.decl_line,
                .astgen = astgen,
                .is_comptime = true,
                .instructions = gz.instructions,
                .instructions_top = gz.instructions.items.len,
            };
            defer block_scope.unstack();

            const scan_result = try astgen.scanContainer(&namespace, container_decl.ast.members, .@"opaque");

            var scratch: Scratch = .init(astgen);
            defer scratch.reset();
            var wip_decls: WipDecls = try .init(&scratch, scan_result.decls_len);

            if (container_decl.layout_token) |layout_token| {
                return astgen.failTok(layout_token, "opaque types do not support 'packed' or 'extern'", .{});
            }

            for (container_decl.ast.members) |member_node| {
                switch (try containerMember(&block_scope, &namespace.base, &wip_decls, member_node)) {
                    .decl => {},
                    .field => return astgen.failNode(member_node, "opaque types cannot have fields", .{}),
                }
            }

            wip_decls.finish();

            try gz.setOpaque(decl_inst, .{
                .src_node = node,
                .name_strat = name_strat,
                .decls_len = scan_result.decls_len,
                .captures = namespace.captures.keys(),
                .capture_names = namespace.captures.values(),
                .decls = @ptrCast(scratch.all().get(astgen)),
            });

            block_scope.unstack();
            return rvalue(gz, ri, decl_inst.toRef(), node);
        },
        else => unreachable,
    }
}

const ContainerMemberResult = union(enum) { decl, field: Ast.full.ContainerField };

fn containerMember(
    gz: *GenZir,
    scope: *Scope,
    wip_decls: *WipDecls,
    member_node: Ast.Node.Index,
) InnerError!ContainerMemberResult {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    switch (tree.nodeTag(member_node)) {
        .container_field_init,
        .container_field_align,
        .container_field,
        => return ContainerMemberResult{ .field = tree.fullContainerField(member_node).? },

        .fn_proto,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto_simple,
        .fn_decl,
        => {
            var buf: [1]Ast.Node.Index = undefined;
            const full = tree.fullFnProto(&buf, member_node).?;

            const body: Ast.Node.OptionalIndex = if (tree.nodeTag(member_node) == .fn_decl)
                tree.nodeData(member_node).node_and_node[1].toOptional()
            else
                .none;

            const prev_decl_index = wip_decls.index;
            astgen.fnDecl(gz, scope, wip_decls, member_node, body, full) catch |err| switch (err) {
                error.OutOfMemory => return error.OutOfMemory,
                error.AnalysisFail => {
                    wip_decls.index = prev_decl_index;
                    try addFailedDeclaration(
                        wip_decls,
                        gz,
                        .@"const",
                        try astgen.identAsString(full.name_token.?),
                        full.ast.proto_node,
                        full.visib_token != null,
                    );
                },
            };
        },

        .global_var_decl,
        .local_var_decl,
        .simple_var_decl,
        .aligned_var_decl,
        => {
            const full = tree.fullVarDecl(member_node).?;
            const prev_decl_index = wip_decls.index;
            astgen.globalVarDecl(gz, scope, wip_decls, member_node, full) catch |err| switch (err) {
                error.OutOfMemory => return error.OutOfMemory,
                error.AnalysisFail => {
                    wip_decls.index = prev_decl_index;
                    try addFailedDeclaration(
                        wip_decls,
                        gz,
                        .@"const", // doesn't really matter
                        try astgen.identAsString(full.ast.mut_token + 1),
                        member_node,
                        full.visib_token != null,
                    );
                },
            };
        },

        .@"comptime" => {
            const prev_decl_index = wip_decls.index;
            astgen.comptimeDecl(gz, scope, wip_decls, member_node) catch |err| switch (err) {
                error.OutOfMemory => return error.OutOfMemory,
                error.AnalysisFail => {
                    wip_decls.index = prev_decl_index;
                    try addFailedDeclaration(
                        wip_decls,
                        gz,
                        .@"comptime",
                        .empty,
                        member_node,
                        false,
                    );
                },
            };
        },
        .test_decl => {
            const prev_decl_index = wip_decls.index;
            // We need to have *some* decl here so that the decl count matches what's expected.
            // Since it doesn't strictly matter *what* this is, let's save ourselves the trouble
            // of duplicating the test name logic, and just assume this is an unnamed test.
            astgen.testDecl(gz, scope, wip_decls, member_node) catch |err| switch (err) {
                error.OutOfMemory => return error.OutOfMemory,
                error.AnalysisFail => {
                    wip_decls.index = prev_decl_index;
                    try addFailedDeclaration(
                        wip_decls,
                        gz,
                        .unnamed_test,
                        .empty,
                        member_node,
                        false,
                    );
                },
            };
        },
        else => unreachable,
    }
    return .decl;
}

fn errorSetDecl(gz: *GenZir, ri: ResultInfo, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;
    const tree = astgen.tree;

    const payload_index = try reserveExtra(astgen, @typeInfo(Zir.Inst.ErrorSetDecl).@"struct".fields.len);
    var fields_len: usize = 0;
    {
        var idents: std.AutoHashMapUnmanaged(Zir.NullTerminatedString, Ast.TokenIndex) = .empty;
        defer idents.deinit(gpa);

        const lbrace, const rbrace = tree.nodeData(node).token_and_token;
        for (lbrace + 1..rbrace) |i| {
            const tok_i: Ast.TokenIndex = @intCast(i);
            switch (tree.tokenTag(tok_i)) {
                .doc_comment, .comma => {},
                .identifier => {
                    const str_index = try astgen.identAsString(tok_i);
                    const gop = try idents.getOrPut(gpa, str_index);
                    if (gop.found_existing) {
                        const name = try gpa.dupe(u8, mem.span(astgen.nullTerminatedString(str_index)));
                        defer gpa.free(name);
                        return astgen.failTokNotes(
                            tok_i,
                            "duplicate error set field '{s}'",
                            .{name},
                            &[_]u32{
                                try astgen.errNoteTok(
                                    gop.value_ptr.*,
                                    "previous declaration here",
                                    .{},
                                ),
                            },
                        );
                    }
                    gop.value_ptr.* = tok_i;

                    try astgen.extra.append(gpa, @intFromEnum(str_index));
                    fields_len += 1;
                },
                else => unreachable,
            }
        }
    }

    setExtra(astgen, payload_index, Zir.Inst.ErrorSetDecl{
        .fields_len = @intCast(fields_len),
    });
    const result = try gz.addPlNodePayloadIndex(.error_set_decl, node, payload_index);
    return rvalue(gz, ri, result, node);
}

fn tryExpr(
    parent_gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    operand_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;

    const fn_block = astgen.fn_block orelse {
        return astgen.failNode(node, "'try' outside function scope", .{});
    };

    if (parent_gz.any_defer_node.unwrap()) |any_defer_node| {
        return astgen.failNodeNotes(node, "'try' not allowed inside defer expression", .{}, &.{
            try astgen.errNoteNode(
                any_defer_node,
                "defer expression here",
                .{},
            ),
        });
    }

    // Ensure debug line/column information is emitted for this try expression.
    // Then we will save the line/column so that we can emit another one that goes
    // "backwards" because we want to evaluate the operand, but then put the debug
    // info back at the try keyword for error return tracing.
    if (!parent_gz.is_comptime) {
        try emitDbgNode(parent_gz, node);
    }
    const try_lc: LineColumn = .{ astgen.source_line - parent_gz.decl_line, astgen.source_column };

    const operand_rl: ResultInfo.Loc, const block_tag: Zir.Inst.Tag = switch (ri.rl) {
        .ref, .ref_coerced_ty => .{ .ref, .try_ptr },
        .ref_const => .{ .ref_const, .try_ptr },
        else => .{ .none, .@"try" },
    };
    const operand_ri: ResultInfo = .{ .rl = operand_rl, .ctx = .error_handling_expr };
    const operand = operand: {
        // As a special case, we need to detect this form:
        // `try .foo(...)`
        // This is a decl literal form, even though we don't propagate a result type through `try`.
        var buf: [1]Ast.Node.Index = undefined;
        if (astgen.tree.fullCall(&buf, operand_node)) |full_call| {
            const res_ty: Zir.Inst.Ref = try ri.rl.resultType(parent_gz, operand_node) orelse .none;
            break :operand try callExpr(parent_gz, scope, operand_ri, res_ty, operand_node, full_call);
        }

        // This could be a pointer or value depending on the `ri` parameter.
        break :operand try reachableExpr(parent_gz, scope, operand_ri, operand_node, node);
    };

    const try_inst = try parent_gz.makeBlockInst(block_tag, node);
    try parent_gz.instructions.append(astgen.gpa, try_inst);

    var else_scope = parent_gz.makeSubBlock(scope);
    defer else_scope.unstack();

    const err_tag = switch (ri.rl) {
        .ref, .ref_const, .ref_coerced_ty => Zir.Inst.Tag.err_union_code_ptr,
        else => Zir.Inst.Tag.err_union_code,
    };
    const err_code = try else_scope.addUnNode(err_tag, operand, node);
    try genDefers(&else_scope, &fn_block.base, scope, .{ .both = err_code });
    try emitDbgStmt(&else_scope, try_lc);
    _ = try else_scope.addUnNode(.ret_node, err_code, node);

    try else_scope.setTryBody(try_inst, operand);
    const result = try_inst.toRef();
    switch (ri.rl) {
        .ref, .ref_const, .ref_coerced_ty => return result,
        else => return rvalue(parent_gz, ri, result, node),
    }
}

fn orelseCatchExpr(
    parent_gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    cond_op: Zir.Inst.Tag,
    unwrap_op: Zir.Inst.Tag,
    unwrap_code_op: Zir.Inst.Tag,
    payload_token: ?Ast.TokenIndex,
) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;
    const tree = astgen.tree;

    const lhs, const rhs = tree.nodeData(node).node_and_node;

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

    const do_err_trace = astgen.fn_block != null and (cond_op == .is_non_err or cond_op == .is_non_err_ptr);

    var block_scope = parent_gz.makeSubBlock(scope);
    block_scope.setBreakResultInfo(block_ri);
    defer block_scope.unstack();

    const operand_ri: ResultInfo = switch (block_scope.break_result_info.rl) {
        .ref, .ref_coerced_ty => .{ .rl = .ref, .ctx = if (do_err_trace) .error_handling_expr else .none },
        .ref_const => .{ .rl = .ref_const, .ctx = if (do_err_trace) .error_handling_expr else .none },
        else => .{ .rl = .none, .ctx = if (do_err_trace) .error_handling_expr else .none },
    };
    // This could be a pointer or value depending on the `operand_ri` parameter.
    // We cannot use `block_scope.break_result_info` because that has the bare
    // type, whereas this expression has the optional type. Later we make
    // up for this fact by calling rvalue on the else branch.
    const operand = try reachableExpr(&block_scope, &block_scope.base, operand_ri, lhs, rhs);
    const cond = try block_scope.addUnNode(cond_op, operand, node);
    const condbr = try block_scope.addCondBr(.condbr, node);

    const block = try parent_gz.makeBlockInst(.block, node);
    try block_scope.setBlockBody(block);
    // block_scope unstacked now, can add new instructions to parent_gz
    try parent_gz.instructions.append(astgen.gpa, block);

    var then_scope = block_scope.makeSubBlock(scope);
    defer then_scope.unstack();

    // This could be a pointer or value depending on `unwrap_op`.
    const unwrapped_payload = try then_scope.addUnNode(unwrap_op, operand, node);
    const then_result = switch (ri.rl) {
        .ref, .ref_const, .ref_coerced_ty => unwrapped_payload,
        else => try rvalue(&then_scope, block_scope.break_result_info, unwrapped_payload, node),
    };
    _ = try then_scope.addBreakWithSrcNode(.@"break", block, then_result, node);

    var else_scope = block_scope.makeSubBlock(scope);
    defer else_scope.unstack();

    // We know that the operand (almost certainly) modified the error return trace,
    // so signal to Sema that it should save the new index for restoring later.
    if (do_err_trace and nodeMayAppendToErrorTrace(tree, lhs))
        _ = try else_scope.addSaveErrRetIndex(.always);

    var err_val_scope: Scope.LocalVal = undefined;
    const else_sub_scope = blk: {
        const payload = payload_token orelse break :blk &else_scope.base;
        const err_str = tree.tokenSlice(payload);
        if (mem.eql(u8, err_str, "_")) {
            try astgen.appendErrorTok(payload, "discard of error capture; omit it instead", .{});
            break :blk &else_scope.base;
        }
        const err_name = try astgen.identAsString(payload);

        try astgen.detectLocalShadowing(scope, err_name, payload, err_str, .capture);

        err_val_scope = .{
            .parent = &else_scope.base,
            .gen_zir = &else_scope,
            .name = err_name,
            .inst = try else_scope.addUnNode(unwrap_code_op, operand, node),
            .token_src = payload,
            .id_cat = .capture,
        };
        break :blk &err_val_scope.base;
    };

    const else_result = else_result: {
        if (tree.fullSwitch(rhs)) |switch_full| no_switch_on_err: {
            if (tree.nodeTag(node) != .@"catch") break :no_switch_on_err;
            const catch_token = tree.nodeMainToken(node);
            const capture_token = if (tree.tokenTag(catch_token + 1) == .pipe) token: {
                break :token catch_token + 2;
            } else break :no_switch_on_err;
            if (switch_full.label_token == null) break :no_switch_on_err; // must use `switchExpr` with `non_err = .@"if"`
            if (tree.nodeTag(switch_full.ast.condition) != .identifier) break :no_switch_on_err;
            if (!try astgen.tokenIdentEql(capture_token, tree.nodeMainToken(switch_full.ast.condition))) break :no_switch_on_err;
            break :else_result try switchExpr(
                &else_scope,
                else_sub_scope,
                block_scope.break_result_info,
                rhs,
                switch_full,
                .{ .peer_break_target = .{
                    .block_inst = block,
                    .block_ri = block_ri,
                } },
            );
        }
        break :else_result try fullBodyExpr(&else_scope, else_sub_scope, block_scope.break_result_info, rhs, .allow_branch_hint);
    };
    if (!else_scope.endsWithNoReturn()) {
        // As our last action before the break, "pop" the error trace if needed
        if (do_err_trace)
            try restoreErrRetIndex(&else_scope, .{ .block = block }, block_scope.break_result_info, rhs, else_result);

        _ = try else_scope.addBreakWithSrcNode(.@"break", block, else_result, rhs);
    }
    try checkUsed(parent_gz, &else_scope.base, else_sub_scope);

    try setCondBrPayload(condbr, cond, &then_scope, &else_scope);

    if (need_result_rvalue) {
        return rvalue(parent_gz, ri, block.toRef(), node);
    } else {
        return block.toRef();
    }
}

/// Return whether the identifier names of two tokens are equal. Resolves @""
/// tokens without allocating.
/// OK in theory it could do it without allocating. This implementation
/// allocates when the @"" form is used.
fn tokenIdentEql(astgen: *AstGen, token1: Ast.TokenIndex, token2: Ast.TokenIndex) !bool {
    const ident_name_1 = try astgen.identifierTokenString(token1);
    const ident_name_2 = try astgen.identifierTokenString(token2);
    return mem.eql(u8, ident_name_1, ident_name_2);
}

fn fieldAccess(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    switch (ri.rl) {
        .ref, .ref_coerced_ty => return addFieldAccess(.field_ptr, gz, scope, .{ .rl = .ref }, node),
        .ref_const => return addFieldAccess(.field_ptr, gz, scope, .{ .rl = .ref_const }, node),
        else => {
            const access = try addFieldAccess(.field_ptr_load, gz, scope, .{ .rl = .ref_const }, node);
            return rvalue(gz, ri, access, node);
        },
    }
}

fn addFieldAccess(
    tag: Zir.Inst.Tag,
    gz: *GenZir,
    scope: *Scope,
    lhs_ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const object_node, const field_ident = tree.nodeData(node).node_and_token;
    const str_index = try astgen.identAsString(field_ident);
    const lhs = try expr(gz, scope, lhs_ri, object_node);

    const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
    try emitDbgStmt(gz, cursor);

    return gz.addPlNode(tag, node, Zir.Inst.Field{
        .lhs = lhs,
        .field_name_start = str_index,
    });
}

fn arrayAccess(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const tree = gz.astgen.tree;
    switch (ri.rl) {
        .ref, .ref_coerced_ty => {
            const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;
            const lhs = try expr(gz, scope, .{ .rl = .ref }, lhs_node);

            const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);

            const rhs = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, rhs_node);
            try emitDbgStmt(gz, cursor);

            return gz.addPlNode(.elem_ptr_node, node, Zir.Inst.Bin{ .lhs = lhs, .rhs = rhs });
        },
        .ref_const => {
            const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;
            const lhs = try expr(gz, scope, .{ .rl = .ref_const }, lhs_node);

            const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);

            const rhs = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, rhs_node);
            try emitDbgStmt(gz, cursor);

            return gz.addPlNode(.elem_ptr_node, node, Zir.Inst.Bin{ .lhs = lhs, .rhs = rhs });
        },
        else => {
            const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;
            const lhs = try expr(gz, scope, .{ .rl = .ref_const }, lhs_node);

            const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);

            const rhs = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, rhs_node);
            try emitDbgStmt(gz, cursor);

            return rvalue(gz, ri, try gz.addPlNode(.elem_ptr_load, node, Zir.Inst.Bin{ .lhs = lhs, .rhs = rhs }), node);
        },
    }
}

fn simpleBinOp(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    op_inst_tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;

    if (op_inst_tag == .cmp_neq or op_inst_tag == .cmp_eq) {
        const str = if (op_inst_tag == .cmp_eq) "==" else "!=";
        if (tree.nodeTag(lhs_node) == .string_literal or
            tree.nodeTag(rhs_node) == .string_literal)
            return astgen.failNode(node, "cannot compare strings with {s}", .{str});
    }

    const lhs = try reachableExpr(gz, scope, .{ .rl = .none }, lhs_node, node);
    const cursor = switch (op_inst_tag) {
        .add, .sub, .mul, .div, .mod_rem => maybeAdvanceSourceCursorToMainToken(gz, node),
        else => undefined,
    };
    const rhs = try reachableExpr(gz, scope, .{ .rl = .none }, rhs_node, node);

    switch (op_inst_tag) {
        .add, .sub, .mul, .div, .mod_rem => {
            try emitDbgStmt(gz, cursor);
        },
        else => {},
    }
    const result = try gz.addPlNode(op_inst_tag, node, Zir.Inst.Bin{ .lhs = lhs, .rhs = rhs });
    return rvalue(gz, ri, result, node);
}

fn simpleStrTok(
    gz: *GenZir,
    ri: ResultInfo,
    ident_token: Ast.TokenIndex,
    node: Ast.Node.Index,
    op_inst_tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const str_index = try astgen.identAsString(ident_token);
    const result = try gz.addStrTok(op_inst_tag, str_index, ident_token);
    return rvalue(gz, ri, result, node);
}

fn boolBinOp(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    zir_tag: Zir.Inst.Tag,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;
    const lhs = try expr(gz, scope, coerced_bool_ri, lhs_node);
    const bool_br = (try gz.addPlNodePayloadIndex(zir_tag, node, undefined)).toIndex().?;

    var rhs_scope = gz.makeSubBlock(scope);
    defer rhs_scope.unstack();
    const rhs = try fullBodyExpr(&rhs_scope, &rhs_scope.base, coerced_bool_ri, rhs_node, .allow_branch_hint);
    if (!gz.refIsNoReturn(rhs)) {
        _ = try rhs_scope.addBreakWithSrcNode(.break_inline, bool_br, rhs, rhs_node);
    }
    try rhs_scope.setBoolBrBody(bool_br, lhs);

    const block_ref = bool_br.toRef();
    return rvalue(gz, ri, block_ref, node);
}

fn ifExpr(
    parent_gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    if_full: Ast.full.If,
) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;
    const tree = astgen.tree;

    const do_err_trace = astgen.fn_block != null and if_full.error_token != null;

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

    var block_scope = parent_gz.makeSubBlock(scope);
    block_scope.setBreakResultInfo(block_ri);
    defer block_scope.unstack();

    const payload_is_ref = if (if_full.payload_token) |payload_token|
        tree.tokenTag(payload_token) == .asterisk
    else
        false;

    try emitDbgNode(parent_gz, if_full.ast.cond_expr);
    const cond: struct {
        inst: Zir.Inst.Ref,
        bool_bit: Zir.Inst.Ref,
    } = c: {
        if (if_full.error_token) |_| {
            const cond_ri: ResultInfo = .{ .rl = if (payload_is_ref) .ref else .none, .ctx = .error_handling_expr };
            const err_union = try expr(&block_scope, &block_scope.base, cond_ri, if_full.ast.cond_expr);
            const tag: Zir.Inst.Tag = if (payload_is_ref) .is_non_err_ptr else .is_non_err;
            break :c .{
                .inst = err_union,
                .bool_bit = try block_scope.addUnNode(tag, err_union, if_full.ast.cond_expr),
            };
        } else if (if_full.payload_token) |_| {
            const cond_ri: ResultInfo = .{ .rl = if (payload_is_ref) .ref else .none };
            const optional = try expr(&block_scope, &block_scope.base, cond_ri, if_full.ast.cond_expr);
            const tag: Zir.Inst.Tag = if (payload_is_ref) .is_non_null_ptr else .is_non_null;
            break :c .{
                .inst = optional,
                .bool_bit = try block_scope.addUnNode(tag, optional, if_full.ast.cond_expr),
            };
        } else {
            const cond = try expr(&block_scope, &block_scope.base, coerced_bool_ri, if_full.ast.cond_expr);
            break :c .{
                .inst = cond,
                .bool_bit = cond,
            };
        }
    };

    const condbr = try block_scope.addCondBr(.condbr, node);

    const block = try parent_gz.makeBlockInst(.block, node);
    try block_scope.setBlockBody(block);
    // block_scope unstacked now, can add new instructions to parent_gz
    try parent_gz.instructions.append(astgen.gpa, block);

    var then_scope = parent_gz.makeSubBlock(scope);
    defer then_scope.unstack();

    var payload_val_scope: Scope.LocalVal = undefined;

    const then_node = if_full.ast.then_expr;
    const then_sub_scope = s: {
        if (if_full.error_token != null) {
            if (if_full.payload_token) |payload_token| {
                const tag: Zir.Inst.Tag = if (payload_is_ref)
                    .err_union_payload_unsafe_ptr
                else
                    .err_union_payload_unsafe;
                const payload_inst = try then_scope.addUnNode(tag, cond.inst, then_node);
                const token_name_index = payload_token + @intFromBool(payload_is_ref);
                const ident_name = try astgen.identAsString(token_name_index);
                const token_name_str = tree.tokenSlice(token_name_index);
                if (mem.eql(u8, "_", token_name_str)) {
                    if (payload_is_ref) return astgen.failTok(payload_token, "pointer modifier invalid on discard", .{});
                    break :s &then_scope.base;
                }
                try astgen.detectLocalShadowing(&then_scope.base, ident_name, token_name_index, token_name_str, .capture);
                payload_val_scope = .{
                    .parent = &then_scope.base,
                    .gen_zir = &then_scope,
                    .name = ident_name,
                    .inst = payload_inst,
                    .token_src = token_name_index,
                    .id_cat = .capture,
                };
                try then_scope.addDbgVar(.dbg_var_val, ident_name, payload_inst);
                break :s &payload_val_scope.base;
            } else {
                _ = try then_scope.addUnNode(.ensure_err_union_payload_void, cond.inst, node);
                break :s &then_scope.base;
            }
        } else if (if_full.payload_token) |payload_token| {
            const ident_token = payload_token + @intFromBool(payload_is_ref);
            const tag: Zir.Inst.Tag = if (payload_is_ref)
                .optional_payload_unsafe_ptr
            else
                .optional_payload_unsafe;
            const ident_bytes = tree.tokenSlice(ident_token);
            if (mem.eql(u8, "_", ident_bytes)) {
                if (payload_is_ref) return astgen.failTok(payload_token, "pointer modifier invalid on discard", .{});
                break :s &then_scope.base;
            }
            const payload_inst = try then_scope.addUnNode(tag, cond.inst, then_node);
            const ident_name = try astgen.identAsString(ident_token);
            try astgen.detectLocalShadowing(&then_scope.base, ident_name, ident_token, ident_bytes, .capture);
            payload_val_scope = .{
                .parent = &then_scope.base,
                .gen_zir = &then_scope,
                .name = ident_name,
                .inst = payload_inst,
                .token_src = ident_token,
                .id_cat = .capture,
            };
            try then_scope.addDbgVar(.dbg_var_val, ident_name, payload_inst);
            break :s &payload_val_scope.base;
        } else {
            break :s &then_scope.base;
        }
    };

    const then_result = try fullBodyExpr(&then_scope, then_sub_scope, block_scope.break_result_info, then_node, .allow_branch_hint);
    try checkUsed(parent_gz, &then_scope.base, then_sub_scope);
    if (!then_scope.endsWithNoReturn()) {
        _ = try then_scope.addBreakWithSrcNode(.@"break", block, then_result, then_node);
    }

    var else_scope = parent_gz.makeSubBlock(scope);
    defer else_scope.unstack();

    // We know that the operand (almost certainly) modified the error return trace,
    // so signal to Sema that it should save the new index for restoring later.
    if (do_err_trace and nodeMayAppendToErrorTrace(tree, if_full.ast.cond_expr))
        _ = try else_scope.addSaveErrRetIndex(.always);

    if (if_full.ast.else_expr.unwrap()) |else_node| {
        const sub_scope = s: {
            if (if_full.error_token) |error_token| {
                const tag: Zir.Inst.Tag = if (payload_is_ref)
                    .err_union_code_ptr
                else
                    .err_union_code;
                const payload_inst = try else_scope.addUnNode(tag, cond.inst, if_full.ast.cond_expr);
                const ident_name = try astgen.identAsString(error_token);
                const error_token_str = tree.tokenSlice(error_token);
                if (mem.eql(u8, "_", error_token_str))
                    break :s &else_scope.base;
                try astgen.detectLocalShadowing(&else_scope.base, ident_name, error_token, error_token_str, .capture);
                payload_val_scope = .{
                    .parent = &else_scope.base,
                    .gen_zir = &else_scope,
                    .name = ident_name,
                    .inst = payload_inst,
                    .token_src = error_token,
                    .id_cat = .capture,
                };
                try else_scope.addDbgVar(.dbg_var_val, ident_name, payload_inst);
                break :s &payload_val_scope.base;
            } else {
                break :s &else_scope.base;
            }
        };
        const else_result = else_result: {
            if (tree.fullSwitch(else_node)) |switch_full| no_switch_on_err: {
                const error_token = if_full.error_token orelse break :no_switch_on_err;
                if (switch_full.label_token == null) break :no_switch_on_err; // must use `switchExpr` with `non_err = .@"if"`
                if (tree.nodeTag(switch_full.ast.condition) != .identifier) break :no_switch_on_err;
                if (!try astgen.tokenIdentEql(error_token, tree.nodeMainToken(switch_full.ast.condition))) break :no_switch_on_err;
                break :else_result try switchExpr(
                    &else_scope,
                    sub_scope,
                    block_scope.break_result_info,
                    else_node,
                    switch_full,
                    .{ .peer_break_target = .{
                        .block_inst = block,
                        .block_ri = block_ri,
                    } },
                );
            }
            break :else_result try fullBodyExpr(&else_scope, sub_scope, block_scope.break_result_info, else_node, .allow_branch_hint);
        };
        if (!else_scope.endsWithNoReturn()) {
            // As our last action before the break, "pop" the error trace if needed
            if (do_err_trace)
                try restoreErrRetIndex(&else_scope, .{ .block = block }, block_scope.break_result_info, else_node, else_result);
            _ = try else_scope.addBreakWithSrcNode(.@"break", block, else_result, else_node);
        }
        try checkUsed(parent_gz, &else_scope.base, sub_scope);
    } else {
        const result = try rvalue(&else_scope, ri, .void_value, node);
        _ = try else_scope.addBreak(.@"break", block, result);
    }

    try setCondBrPayload(condbr, cond.bool_bit, &then_scope, &else_scope);

    if (need_result_rvalue) {
        return rvalue(parent_gz, ri, block.toRef(), node);
    } else {
        return block.toRef();
    }
}

/// Supports `else_scope` stacked on `then_scope`. Unstacks `else_scope` then `then_scope`.
fn setCondBrPayload(
    condbr: Zir.Inst.Index,
    cond: Zir.Inst.Ref,
    then_scope: *GenZir,
    else_scope: *GenZir,
) !void {
    defer then_scope.unstack();
    defer else_scope.unstack();
    const astgen = then_scope.astgen;
    const then_body = then_scope.instructionsSliceUpto(else_scope);
    const else_body = else_scope.instructionsSlice();
    const then_body_len = astgen.countBodyLenAfterFixups(then_body);
    const else_body_len = astgen.countBodyLenAfterFixups(else_body);
    try astgen.extra.ensureUnusedCapacity(
        astgen.gpa,
        @typeInfo(Zir.Inst.CondBr).@"struct".fields.len + then_body_len + else_body_len,
    );

    const zir_datas = astgen.instructions.items(.data);
    zir_datas[@intFromEnum(condbr)].pl_node.payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.CondBr{
        .condition = cond,
        .then_body_len = then_body_len,
        .else_body_len = else_body_len,
    });
    astgen.appendBodyWithFixups(then_body);
    astgen.appendBodyWithFixups(else_body);
}

fn whileExpr(
    parent_gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    while_full: Ast.full.While,
    is_statement: bool,
) InnerError!Zir.Inst.Ref {
    const astgen = parent_gz.astgen;
    const tree = astgen.tree;

    const need_rl = astgen.nodes_need_rl.contains(node);
    const block_ri: ResultInfo = if (need_rl) ri else .{
        .rl = switch (ri.rl) {
            .ptr => .{ .ty = (try ri.rl.resultType(parent_gz, node)).? },
            .inferred_ptr => .none,
            else => ri.rl,
        },
        .ctx = ri.ctx,
    };
    // We need to call `rvalue` to write through to the pointe
```
