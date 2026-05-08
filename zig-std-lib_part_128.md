```
ode = mods.align_node.unwrap().?,
                            .addrspace_node = mods.addrspace_node,
                            .bit_range_start = mods.bit_range_start.unwrap().?,
                            .bit_range_end = mods.bit_range_end.unwrap().?,
                        }),
                        elem_type,
                    } },
                });
            } else if (mods.addrspace_node != .none) {
                return try p.addNode(.{
                    .tag = .ptr_type,
                    .main_token = asterisk,
                    .data = .{ .extra_and_node = .{
                        try p.addExtra(Node.PtrType{
                            .sentinel = .none,
                            .align_node = mods.align_node,
                            .addrspace_node = mods.addrspace_node,
                        }),
                        elem_type,
                    } },
                });
            } else {
                return try p.addNode(.{
                    .tag = .ptr_type_aligned,
                    .main_token = asterisk,
                    .data = .{ .opt_node_and_node = .{
                        mods.align_node,
                        elem_type,
                    } },
                });
            }
        },
        .asterisk_asterisk => {
            const asterisk = p.nextToken();
            const mods = try p.parsePtrModifiers();
            const elem_type = try p.expectTypeExpr();
            const inner: Node.Index = inner: {
                if (mods.bit_range_start != .none) {
                    break :inner try p.addNode(.{
                        .tag = .ptr_type_bit_range,
                        .main_token = asterisk,
                        .data = .{ .extra_and_node = .{
                            try p.addExtra(Node.PtrTypeBitRange{
                                .sentinel = .none,
                                .align_node = mods.align_node.unwrap().?,
                                .addrspace_node = mods.addrspace_node,
                                .bit_range_start = mods.bit_range_start.unwrap().?,
                                .bit_range_end = mods.bit_range_end.unwrap().?,
                            }),
                            elem_type,
                        } },
                    });
                } else if (mods.addrspace_node != .none) {
                    break :inner try p.addNode(.{
                        .tag = .ptr_type,
                        .main_token = asterisk,
                        .data = .{ .extra_and_node = .{
                            try p.addExtra(Node.PtrType{
                                .sentinel = .none,
                                .align_node = mods.align_node,
                                .addrspace_node = mods.addrspace_node,
                            }),
                            elem_type,
                        } },
                    });
                } else {
                    break :inner try p.addNode(.{
                        .tag = .ptr_type_aligned,
                        .main_token = asterisk,
                        .data = .{ .opt_node_and_node = .{
                            mods.align_node,
                            elem_type,
                        } },
                    });
                }
            };
            return try p.addNode(.{
                .tag = .ptr_type_aligned,
                .main_token = asterisk,
                .data = .{ .opt_node_and_node = .{
                    .none,
                    inner,
                } },
            });
        },
        .l_bracket => switch (p.tokenTag(p.tok_i + 1)) {
            .asterisk => {
                const l_bracket = p.nextToken();
                _ = p.nextToken();
                var sentinel: ?Node.Index = null;
                if (p.eatToken(.identifier)) |ident| {
                    const ident_slice = p.source[p.tokenStart(ident)..p.tokenStart(ident + 1)];
                    if (!std.mem.eql(u8, std.mem.trimEnd(u8, ident_slice, &std.ascii.whitespace), "c")) {
                        p.tok_i -= 1;
                    }
                } else if (p.eatToken(.colon)) |_| {
                    sentinel = try p.expectExpr();
                }
                _ = try p.expectToken(.r_bracket);
                const mods = try p.parsePtrModifiers();
                const elem_type = try p.expectTypeExpr();
                if (mods.bit_range_start.unwrap()) |bit_range_start| {
                    try p.warnMsg(.{
                        .tag = .invalid_bit_range,
                        .token = p.nodeMainToken(bit_range_start),
                    });
                }
                if (sentinel == null and mods.addrspace_node == .none) {
                    return try p.addNode(.{
                        .tag = .ptr_type_aligned,
                        .main_token = l_bracket,
                        .data = .{ .opt_node_and_node = .{
                            mods.align_node,
                            elem_type,
                        } },
                    });
                } else if (mods.align_node == .none and mods.addrspace_node == .none) {
                    return try p.addNode(.{
                        .tag = .ptr_type_sentinel,
                        .main_token = l_bracket,
                        .data = .{ .opt_node_and_node = .{
                            .fromOptional(sentinel),
                            elem_type,
                        } },
                    });
                } else {
                    return try p.addNode(.{
                        .tag = .ptr_type,
                        .main_token = l_bracket,
                        .data = .{ .extra_and_node = .{
                            try p.addExtra(Node.PtrType{
                                .sentinel = .fromOptional(sentinel),
                                .align_node = mods.align_node,
                                .addrspace_node = mods.addrspace_node,
                            }),
                            elem_type,
                        } },
                    });
                }
            },
            else => {
                const lbracket = p.nextToken();
                const len_expr = try p.parseExpr();
                const sentinel: ?Node.Index = if (p.eatToken(.colon)) |_|
                    try p.expectExpr()
                else
                    null;
                _ = try p.expectToken(.r_bracket);
                if (len_expr == null) {
                    const mods = try p.parsePtrModifiers();
                    const elem_type = try p.expectTypeExpr();
                    if (mods.bit_range_start.unwrap()) |bit_range_start| {
                        try p.warnMsg(.{
                            .tag = .invalid_bit_range,
                            .token = p.nodeMainToken(bit_range_start),
                        });
                    }
                    if (sentinel == null and mods.addrspace_node == .none) {
                        return try p.addNode(.{
                            .tag = .ptr_type_aligned,
                            .main_token = lbracket,
                            .data = .{ .opt_node_and_node = .{
                                mods.align_node,
                                elem_type,
                            } },
                        });
                    } else if (mods.align_node == .none and mods.addrspace_node == .none) {
                        return try p.addNode(.{
                            .tag = .ptr_type_sentinel,
                            .main_token = lbracket,
                            .data = .{ .opt_node_and_node = .{
                                .fromOptional(sentinel),
                                elem_type,
                            } },
                        });
                    } else {
                        return try p.addNode(.{
                            .tag = .ptr_type,
                            .main_token = lbracket,
                            .data = .{ .extra_and_node = .{
                                try p.addExtra(Node.PtrType{
                                    .sentinel = .fromOptional(sentinel),
                                    .align_node = mods.align_node,
                                    .addrspace_node = mods.addrspace_node,
                                }),
                                elem_type,
                            } },
                        });
                    }
                } else {
                    switch (p.tokenTag(p.tok_i)) {
                        .keyword_align,
                        .keyword_const,
                        .keyword_volatile,
                        .keyword_allowzero,
                        .keyword_addrspace,
                        => return p.fail(.ptr_mod_on_array_child_type),
                        else => {},
                    }
                    const elem_type = try p.expectTypeExpr();
                    if (sentinel == null) {
                        return try p.addNode(.{
                            .tag = .array_type,
                            .main_token = lbracket,
                            .data = .{ .node_and_node = .{
                                len_expr.?,
                                elem_type,
                            } },
                        });
                    } else {
                        return try p.addNode(.{
                            .tag = .array_type_sentinel,
                            .main_token = lbracket,
                            .data = .{ .node_and_extra = .{
                                len_expr.?,
                                try p.addExtra(Node.ArrayTypeSentinel{
                                    .sentinel = sentinel.?,
                                    .elem_type = elem_type,
                                }),
                            } },
                        });
                    }
                }
            },
        },
        else => return p.parseErrorUnionExpr(),
    }
}

fn expectTypeExpr(p: *Parse) Error!Node.Index {
    return try p.parseTypeExpr() orelse return p.fail(.expected_type_expr);
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
fn parsePrimaryExpr(p: *Parse) !?Node.Index {
    switch (p.tokenTag(p.tok_i)) {
        .keyword_asm => return try p.expectAsmExpr(),
        .keyword_if => return try p.parseIfExpr(),
        .keyword_break => {
            return try p.addNode(.{
                .tag = .@"break",
                .main_token = p.nextToken(),
                .data = .{ .opt_token_and_opt_node = .{
                    try p.parseBreakLabel(),
                    .fromOptional(try p.parseExpr()),
                } },
            });
        },
        .keyword_continue => {
            return try p.addNode(.{
                .tag = .@"continue",
                .main_token = p.nextToken(),
                .data = .{ .opt_token_and_opt_node = .{
                    try p.parseBreakLabel(),
                    .fromOptional(try p.parseExpr()),
                } },
            });
        },
        .keyword_comptime => {
            return try p.addNode(.{
                .tag = .@"comptime",
                .main_token = p.nextToken(),
                .data = .{ .node = try p.expectExpr() },
            });
        },
        .keyword_nosuspend => {
            return try p.addNode(.{
                .tag = .@"nosuspend",
                .main_token = p.nextToken(),
                .data = .{ .node = try p.expectExpr() },
            });
        },
        .keyword_resume => {
            return try p.addNode(.{
                .tag = .@"resume",
                .main_token = p.nextToken(),
                .data = .{ .node = try p.expectExpr() },
            });
        },
        .keyword_return => {
            return try p.addNode(.{
                .tag = .@"return",
                .main_token = p.nextToken(),
                .data = .{ .opt_node = .fromOptional(try p.parseExpr()) },
            });
        },
        .identifier => {
            if (p.tokenTag(p.tok_i + 1) == .colon) {
                switch (p.tokenTag(p.tok_i + 2)) {
                    .keyword_inline => {
                        p.tok_i += 3;
                        switch (p.tokenTag(p.tok_i)) {
                            .keyword_for => return try p.parseFor(expectExpr),
                            .keyword_while => return try p.parseWhileExpr(),
                            else => return p.fail(.expected_inlinable),
                        }
                    },
                    .keyword_for => {
                        p.tok_i += 2;
                        return try p.parseFor(expectExpr);
                    },
                    .keyword_while => {
                        p.tok_i += 2;
                        return try p.parseWhileExpr();
                    },
                    else => return try p.parseCurlySuffixExpr(),
                }
            } else {
                return try p.parseCurlySuffixExpr();
            }
        },
        .keyword_inline => {
            p.tok_i += 1;
            switch (p.tokenTag(p.tok_i)) {
                .keyword_for => return try p.parseFor(expectExpr),
                .keyword_while => return try p.parseWhileExpr(),
                else => return p.fail(.expected_inlinable),
            }
        },
        .keyword_for => return try p.parseFor(expectExpr),
        .keyword_while => return try p.parseWhileExpr(),
        .l_brace => return try p.parseBlock(),
        else => return try p.parseCurlySuffixExpr(),
    }
}

/// IfExpr <- IfPrefix Expr (KEYWORD_else Payload? Expr)? !ExprSuffix
fn parseIfExpr(p: *Parse) !?Node.Index {
    return try p.parseIf(expectExpr);
}

/// Block <- LBRACE BlockStatement* RBRACE
fn parseBlock(p: *Parse) !?Node.Index {
    const lbrace = p.eatToken(.l_brace) orelse return null;
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);
    while (true) {
        if (p.tokenTag(p.tok_i) == .r_brace) break;
        const statement = try p.expectStatementRecoverable() orelse break;
        try p.scratch.append(p.gpa, statement);
    }
    _ = try p.expectToken(.r_brace);
    const statements = p.scratch.items[scratch_top..];
    const semicolon = statements.len != 0 and (p.tokenTag(p.tok_i - 2)) == .semicolon;
    if (statements.len <= 2) {
        return try p.addNode(.{
            .tag = if (semicolon) .block_two_semicolon else .block_two,
            .main_token = lbrace,
            .data = .{ .opt_node_and_opt_node = .{
                if (statements.len >= 1) statements[0].toOptional() else .none,
                if (statements.len >= 2) statements[1].toOptional() else .none,
            } },
        });
    } else {
        return try p.addNode(.{
            .tag = if (semicolon) .block_semicolon else .block,
            .main_token = lbrace,
            .data = .{ .extra_range = try p.listToSpan(statements) },
        });
    }
}

/// ForPrefix <- KEYWORD_for LPAREN ForInput (COMMA ForInput)* COMMA? RPAREN ForPayload
///
/// ForInput <- Expr (DOT2 Expr?)?
///
/// ForPayload <- PIPE ASTERISK? IDENTIFIER (COMMA ASTERISK? IDENTIFIER)* PIPE
fn forPrefix(p: *Parse) Error!usize {
    const start = p.scratch.items.len;
    _ = try p.expectToken(.l_paren);

    while (true) {
        var input = try p.expectExpr();
        if (p.eatToken(.ellipsis2)) |ellipsis| {
            input = try p.addNode(.{
                .tag = .for_range,
                .main_token = ellipsis,
                .data = .{ .node_and_opt_node = .{
                    input,
                    .fromOptional(try p.parseExpr()),
                } },
            });
        }

        try p.scratch.append(p.gpa, input);
        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            .r_paren => {
                p.tok_i += 1;
                break;
            },
            .colon, .r_brace, .r_bracket => return p.failExpected(.r_paren),
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warn(.expected_comma_after_for_operand),
        }
        if (p.eatToken(.r_paren)) |_| break;
    }
    const inputs = p.scratch.items.len - start;

    _ = p.eatToken(.pipe) orelse {
        try p.warn(.expected_loop_payload);
        return inputs;
    };

    var warned_excess = false;
    var captures: u32 = 0;
    while (true) {
        _ = p.eatToken(.asterisk);
        const identifier = try p.expectToken(.identifier);
        captures += 1;
        if (captures > inputs and !warned_excess) {
            try p.warnMsg(.{ .tag = .extra_for_capture, .token = identifier });
            warned_excess = true;
        }
        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            .pipe => {
                p.tok_i += 1;
                break;
            },
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warn(.expected_comma_after_capture),
        }
        if (p.eatToken(.pipe)) |_| break;
    }

    if (captures < inputs) {
        const index = p.scratch.items.len - captures;
        const input = p.nodeMainToken(p.scratch.items[index]);
        try p.warnMsg(.{ .tag = .for_input_not_captured, .token = input });
    }
    return inputs;
}

/// WhilePrefix <- KEYWORD_while LPAREN Expr RPAREN PtrPayload? WhileContinueExpr?
///
/// WhileExpr <- WhilePrefix Expr (KEYWORD_else Payload? Expr)? !ExprSuffi
fn parseWhileExpr(p: *Parse) !?Node.Index {
    const while_token = p.eatToken(.keyword_while) orelse return null;
    _ = try p.expectToken(.l_paren);
    const condition = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    _ = try p.parsePtrPayload();
    const cont_expr = try p.parseWhileContinueExpr();

    const then_expr = try p.expectExpr();
    _ = p.eatToken(.keyword_else) orelse {
        if (cont_expr == null) {
            return try p.addNode(.{
                .tag = .while_simple,
                .main_token = while_token,
                .data = .{ .node_and_node = .{
                    condition,
                    then_expr,
                } },
            });
        } else {
            return try p.addNode(.{
                .tag = .while_cont,
                .main_token = while_token,
                .data = .{ .node_and_extra = .{
                    condition,
                    try p.addExtra(Node.WhileCont{
                        .cont_expr = cont_expr.?,
                        .then_expr = then_expr,
                    }),
                } },
            });
        }
    };
    _ = try p.parsePayload();
    const else_expr = try p.expectExpr();
    return try p.addNode(.{
        .tag = .@"while",
        .main_token = while_token,
        .data = .{ .node_and_extra = .{
            condition,
            try p.addExtra(Node.While{
                .cont_expr = .fromOptional(cont_expr),
                .then_expr = then_expr,
                .else_expr = else_expr,
            }),
        } },
    });
}

/// CurlySuffixExpr <- TypeExpr InitList?
///
/// InitList
///     <- LBRACE FieldInit (COMMA FieldInit)* COMMA? RBRACE
///      / LBRACE Expr (COMMA Expr)* COMMA? RBRACE
///      / LBRACE RBRACE
fn parseCurlySuffixExpr(p: *Parse) !?Node.Index {
    const lhs = try p.parseTypeExpr() orelse return null;
    const lbrace = p.eatToken(.l_brace) orelse return lhs;

    // If there are 0 or 1 items, we can use ArrayInitOne/StructInitOne;
    // otherwise we use the full ArrayInit/StructInit.

    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);
    const opt_field_init = try p.parseFieldInit();
    if (opt_field_init) |field_init| {
        try p.scratch.append(p.gpa, field_init);
        while (true) {
            switch (p.tokenTag(p.tok_i)) {
                .comma => p.tok_i += 1,
                .r_brace => {
                    p.tok_i += 1;
                    break;
                },
                .colon, .r_paren, .r_bracket => return p.failExpected(.r_brace),
                // Likely just a missing comma; give error but continue parsing.
                else => try p.warn(.expected_comma_after_initializer),
            }
            if (p.eatToken(.r_brace)) |_| break;
            const next = try p.expectFieldInit();
            try p.scratch.append(p.gpa, next);
        }
        const comma = (p.tokenTag(p.tok_i - 2)) == .comma;
        const inits = p.scratch.items[scratch_top..];
        std.debug.assert(inits.len != 0);
        if (inits.len <= 1) {
            return try p.addNode(.{
                .tag = if (comma) .struct_init_one_comma else .struct_init_one,
                .main_token = lbrace,
                .data = .{ .node_and_opt_node = .{
                    lhs,
                    inits[0].toOptional(),
                } },
            });
        } else {
            return try p.addNode(.{
                .tag = if (comma) .struct_init_comma else .struct_init,
                .main_token = lbrace,
                .data = .{ .node_and_extra = .{
                    lhs,
                    try p.addExtra(try p.listToSpan(inits)),
                } },
            });
        }
    }

    while (true) {
        if (p.eatToken(.r_brace)) |_| break;
        const elem_init = try p.expectExpr();
        try p.scratch.append(p.gpa, elem_init);
        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            .r_brace => {
                p.tok_i += 1;
                break;
            },
            .colon, .r_paren, .r_bracket => return p.failExpected(.r_brace),
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warn(.expected_comma_after_initializer),
        }
    }
    const comma = (p.tokenTag(p.tok_i - 2)) == .comma;
    const inits = p.scratch.items[scratch_top..];
    switch (inits.len) {
        0 => return try p.addNode(.{
            .tag = .struct_init_one,
            .main_token = lbrace,
            .data = .{ .node_and_opt_node = .{
                lhs,
                .none,
            } },
        }),
        1 => return try p.addNode(.{
            .tag = if (comma) .array_init_one_comma else .array_init_one,
            .main_token = lbrace,
            .data = .{ .node_and_node = .{
                lhs,
                inits[0],
            } },
        }),
        else => return try p.addNode(.{
            .tag = if (comma) .array_init_comma else .array_init,
            .main_token = lbrace,
            .data = .{ .node_and_extra = .{
                lhs,
                try p.addExtra(try p.listToSpan(inits)),
            } },
        }),
    }
}

/// ErrorUnionExpr <- SuffixExpr (EXCLAMATIONMARK TypeExpr)?
fn parseErrorUnionExpr(p: *Parse) !?Node.Index {
    const suffix_expr = try p.parseSuffixExpr() orelse return null;
    const bang = p.eatToken(.bang) orelse return suffix_expr;
    return try p.addNode(.{
        .tag = .error_union,
        .main_token = bang,
        .data = .{ .node_and_node = .{
            suffix_expr,
            try p.expectTypeExpr(),
        } },
    });
}

/// SuffixExpr
///     <- PrimaryTypeExpr (SuffixOp / FnCallArguments)*
///
/// FnCallArguments <- LPAREN ExprList RPAREN
///
/// ExprList <- (Expr COMMA)* Expr?
fn parseSuffixExpr(p: *Parse) !?Node.Index {
    var res = try p.parsePrimaryTypeExpr() orelse return null;
    while (true) {
        const opt_suffix_op = try p.parseSuffixOp(res);
        if (opt_suffix_op) |suffix_op| {
            res = suffix_op;
            continue;
        }
        const lparen = p.eatToken(.l_paren) orelse return res;
        const scratch_top = p.scratch.items.len;
        defer p.scratch.shrinkRetainingCapacity(scratch_top);
        while (true) {
            if (p.eatToken(.r_paren)) |_| break;
            const param = try p.expectExpr();
            try p.scratch.append(p.gpa, param);
            switch (p.tokenTag(p.tok_i)) {
                .comma => p.tok_i += 1,
                .r_paren => {
                    p.tok_i += 1;
                    break;
                },
                .colon, .r_brace, .r_bracket => return p.failExpected(.r_paren),
                // Likely just a missing comma; give error but continue parsing.
                else => try p.warn(.expected_comma_after_arg),
            }
        }
        const comma = (p.tokenTag(p.tok_i - 2)) == .comma;
        const params = p.scratch.items[scratch_top..];
        res = switch (params.len) {
            0, 1 => try p.addNode(.{
                .tag = if (comma) .call_one_comma else .call_one,
                .main_token = lparen,
                .data = .{ .node_and_opt_node = .{
                    res,
                    if (params.len >= 1) .fromOptional(params[0]) else .none,
                } },
            }),
            else => try p.addNode(.{
                .tag = if (comma) .call_comma else .call,
                .main_token = lparen,
                .data = .{ .node_and_extra = .{
                    res,
                    try p.addExtra(try p.listToSpan(params)),
                } },
            }),
        };
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
///
/// ContainerDecl <- (KEYWORD_extern / KEYWORD_packed)? ContainerDeclAuto
///
/// ContainerDeclAuto <- ContainerDeclType LBRACE ContainerMembers RBRACE
///
/// InitList
///     <- LBRACE FieldInit (COMMA FieldInit)* COMMA? RBRACE
///      / LBRACE Expr (COMMA Expr)* COMMA? RBRACE
///      / LBRACE RBRACE
///
/// ErrorSetDecl <- KEYWORD_error LBRACE IdentifierList RBRACE
///
/// GroupedExpr <- LPAREN Expr RPAREN
///
/// IfTypeExpr <- IfPrefix TypeExpr (KEYWORD_else Payload? TypeExpr)? !ExprSuffix
///
/// LabeledTypeExpr
///     <- BlockLabel Block
///      / BlockLabel? LoopTypeExpr
///      / BlockLabel? SwitchExpr
///
/// LoopTypeExpr <- KEYWORD_inline? (ForTypeExpr / WhileTypeExpr)
fn parsePrimaryTypeExpr(p: *Parse) !?Node.Index {
    switch (p.tokenTag(p.tok_i)) {
        .char_literal => return try p.addNode(.{
            .tag = .char_literal,
            .main_token = p.nextToken(),
            .data = undefined,
        }),
        .number_literal => return try p.addNode(.{
            .tag = .number_literal,
            .main_token = p.nextToken(),
            .data = undefined,
        }),
        .keyword_unreachable => return try p.addNode(.{
            .tag = .unreachable_literal,
            .main_token = p.nextToken(),
            .data = undefined,
        }),
        .keyword_anyframe => return try p.addNode(.{
            .tag = .anyframe_literal,
            .main_token = p.nextToken(),
            .data = undefined,
        }),
        .string_literal => {
            const main_token = p.nextToken();
            return try p.addNode(.{
                .tag = .string_literal,
                .main_token = main_token,
                .data = undefined,
            });
        },

        .builtin => return try p.parseBuiltinCall(),
        .keyword_fn => return try p.parseFnProto(),
        .keyword_if => return try p.parseIf(expectTypeExpr),
        .keyword_switch => return try p.expectSwitchExpr(false),

        .keyword_extern,
        .keyword_packed,
        => {
            p.tok_i += 1;
            return try p.parseContainerDeclAuto();
        },

        .keyword_struct,
        .keyword_opaque,
        .keyword_enum,
        .keyword_union,
        => return try p.parseContainerDeclAuto(),

        .keyword_comptime => return try p.addNode(.{
            .tag = .@"comptime",
            .main_token = p.nextToken(),
            .data = .{ .node = try p.expectTypeExpr() },
        }),
        .multiline_string_literal_line => {
            const first_line = p.nextToken();
            while (p.tokenTag(p.tok_i) == .multiline_string_literal_line) {
                p.tok_i += 1;
            }
            return try p.addNode(.{
                .tag = .multiline_string_literal,
                .main_token = first_line,
                .data = .{ .token_and_token = .{
                    first_line,
                    p.tok_i - 1,
                } },
            });
        },
        .identifier => switch (p.tokenTag(p.tok_i + 1)) {
            .colon => switch (p.tokenTag(p.tok_i + 2)) {
                .keyword_inline => {
                    p.tok_i += 3;
                    switch (p.tokenTag(p.tok_i)) {
                        .keyword_for => return try p.parseFor(expectTypeExpr),
                        .keyword_while => return try p.parseWhileTypeExpr(),
                        else => return p.fail(.expected_inlinable),
                    }
                },
                .keyword_for => {
                    p.tok_i += 2;
                    return try p.parseFor(expectTypeExpr);
                },
                .keyword_while => {
                    p.tok_i += 2;
                    return try p.parseWhileTypeExpr();
                },
                .keyword_switch => {
                    p.tok_i += 2;
                    return try p.expectSwitchExpr(true);
                },
                .l_brace => {
                    p.tok_i += 2;
                    return try p.parseBlock();
                },
                else => return try p.addNode(.{
                    .tag = .identifier,
                    .main_token = p.nextToken(),
                    .data = undefined,
                }),
            },
            else => return try p.addNode(.{
                .tag = .identifier,
                .main_token = p.nextToken(),
                .data = undefined,
            }),
        },
        .keyword_inline => {
            p.tok_i += 1;
            switch (p.tokenTag(p.tok_i)) {
                .keyword_for => return try p.parseFor(expectTypeExpr),
                .keyword_while => return try p.parseWhileTypeExpr(),
                else => return p.fail(.expected_inlinable),
            }
        },
        .keyword_for => return try p.parseFor(expectTypeExpr),
        .keyword_while => return try p.parseWhileTypeExpr(),
        .period => switch (p.tokenTag(p.tok_i + 1)) {
            .identifier => {
                p.tok_i += 1;
                return try p.addNode(.{
                    .tag = .enum_literal,
                    .main_token = p.nextToken(), // identifier
                    .data = undefined,
                });
            },
            .l_brace => {
                const lbrace = p.tok_i + 1;
                p.tok_i = lbrace + 1;

                // If there are 0, 1, or 2 items, we can use ArrayInitDotTwo/StructInitDotTwo;
                // otherwise we use the full ArrayInitDot/StructInitDot.

                const scratch_top = p.scratch.items.len;
                defer p.scratch.shrinkRetainingCapacity(scratch_top);
                const opt_field_init = try p.parseFieldInit();
                if (opt_field_init) |field_init| {
                    try p.scratch.append(p.gpa, field_init);
                    while (true) {
                        switch (p.tokenTag(p.tok_i)) {
                            .comma => p.tok_i += 1,
                            .r_brace => {
                                p.tok_i += 1;
                                break;
                            },
                            .colon, .r_paren, .r_bracket => return p.failExpected(.r_brace),
                            // Likely just a missing comma; give error but continue parsing.
                            else => try p.warn(.expected_comma_after_initializer),
                        }
                        if (p.eatToken(.r_brace)) |_| break;
                        const next = try p.expectFieldInit();
                        try p.scratch.append(p.gpa, next);
                    }
                    const comma = (p.tokenTag(p.tok_i - 2)) == .comma;
                    const inits = p.scratch.items[scratch_top..];
                    std.debug.assert(inits.len != 0);
                    if (inits.len <= 2) {
                        return try p.addNode(.{
                            .tag = if (comma) .struct_init_dot_two_comma else .struct_init_dot_two,
                            .main_token = lbrace,
                            .data = .{ .opt_node_and_opt_node = .{
                                if (inits.len >= 1) .fromOptional(inits[0]) else .none,
                                if (inits.len >= 2) .fromOptional(inits[1]) else .none,
                            } },
                        });
                    } else {
                        return try p.addNode(.{
                            .tag = if (comma) .struct_init_dot_comma else .struct_init_dot,
                            .main_token = lbrace,
                            .data = .{ .extra_range = try p.listToSpan(inits) },
                        });
                    }
                }

                while (true) {
                    if (p.eatToken(.r_brace)) |_| break;
                    const elem_init = try p.expectExpr();
                    try p.scratch.append(p.gpa, elem_init);
                    switch (p.tokenTag(p.tok_i)) {
                        .comma => p.tok_i += 1,
                        .r_brace => {
                            p.tok_i += 1;
                            break;
                        },
                        .colon, .r_paren, .r_bracket => return p.failExpected(.r_brace),
                        // Likely just a missing comma; give error but continue parsing.
                        else => try p.warn(.expected_comma_after_initializer),
                    }
                }
                const comma = (p.tokenTag(p.tok_i - 2)) == .comma;
                const inits = p.scratch.items[scratch_top..];
                if (inits.len <= 2) {
                    return try p.addNode(.{
                        .tag = if (inits.len == 0)
                            .struct_init_dot_two
                        else if (comma) .array_init_dot_two_comma else .array_init_dot_two,
                        .main_token = lbrace,
                        .data = .{ .opt_node_and_opt_node = .{
                            if (inits.len >= 1) inits[0].toOptional() else .none,
                            if (inits.len >= 2) inits[1].toOptional() else .none,
                        } },
                    });
                } else {
                    return try p.addNode(.{
                        .tag = if (comma) .array_init_dot_comma else .array_init_dot,
                        .main_token = lbrace,
                        .data = .{ .extra_range = try p.listToSpan(inits) },
                    });
                }
            },
            else => return null,
        },
        .keyword_error => switch (p.tokenTag(p.tok_i + 1)) {
            .l_brace => {
                const error_token = p.tok_i;
                p.tok_i += 2;
                while (true) {
                    if (p.eatToken(.r_brace)) |_| break;
                    _ = try p.eatDocComments();
                    _ = try p.expectToken(.identifier);
                    switch (p.tokenTag(p.tok_i)) {
                        .comma => p.tok_i += 1,
                        .r_brace => {
                            p.tok_i += 1;
                            break;
                        },
                        .colon, .r_paren, .r_bracket => return p.failExpected(.r_brace),
                        // Likely just a missing comma; give error but continue parsing.
                        else => try p.warn(.expected_comma_after_field),
                    }
                }
                return try p.addNode(.{
                    .tag = .error_set_decl,
                    .main_token = error_token,
                    .data = .{
                        .token_and_token = .{
                            error_token + 1, // lbrace
                            p.tok_i - 1, // rbrace
                        },
                    },
                });
            },
            else => {
                const main_token = p.nextToken();
                const period = p.eatToken(.period);
                if (period == null) return p.failExpected(.period);
                const identifier = p.eatToken(.identifier);
                if (identifier == null) return p.failExpected(.identifier);
                return try p.addNode(.{
                    .tag = .error_value,
                    .main_token = main_token,
                    .data = undefined,
                });
            },
        },
        .l_paren => return try p.addNode(.{
            .tag = .grouped_expression,
            .main_token = p.nextToken(),
            .data = .{ .node_and_token = .{
                try p.expectExpr(),
                try p.expectToken(.r_paren),
            } },
        }),
        else => return null,
    }
}

fn expectPrimaryTypeExpr(p: *Parse) !Node.Index {
    return try p.parsePrimaryTypeExpr() orelse return p.fail(.expected_primary_type_expr);
}

/// WhilePrefix <- KEYWORD_while LPAREN Expr RPAREN PtrPayload? WhileContinueExpr?
///
/// WhileTypeExpr <- WhilePrefix TypeExpr (KEYWORD_else Payload? TypeExpr)? !ExprSuffix
fn parseWhileTypeExpr(p: *Parse) !?Node.Index {
    const while_token = p.eatToken(.keyword_while) orelse return null;
    _ = try p.expectToken(.l_paren);
    const condition = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    _ = try p.parsePtrPayload();
    const cont_expr = try p.parseWhileContinueExpr();

    const then_expr = try p.expectTypeExpr();
    _ = p.eatToken(.keyword_else) orelse {
        if (cont_expr == null) {
            return try p.addNode(.{
                .tag = .while_simple,
                .main_token = while_token,
                .data = .{ .node_and_node = .{
                    condition,
                    then_expr,
                } },
            });
        } else {
            return try p.addNode(.{
                .tag = .while_cont,
                .main_token = while_token,
                .data = .{ .node_and_extra = .{
                    condition,
                    try p.addExtra(Node.WhileCont{
                        .cont_expr = cont_expr.?,
                        .then_expr = then_expr,
                    }),
                } },
            });
        }
    };
    _ = try p.parsePayload();
    const else_expr = try p.expectTypeExpr();
    return try p.addNode(.{
        .tag = .@"while",
        .main_token = while_token,
        .data = .{ .node_and_extra = .{
            condition,
            try p.addExtra(Node.While{
                .cont_expr = .fromOptional(cont_expr),
                .then_expr = then_expr,
                .else_expr = else_expr,
            }),
        } },
    });
}

/// SwitchExpr <- KEYWORD_switch LPAREN Expr RPAREN LBRACE SwitchProngList RBRACE
fn parseSwitchExpr(p: *Parse, is_labeled: bool) !?Node.Index {
    const switch_token = p.eatToken(.keyword_switch) orelse return null;
    return try p.expectSwitchSuffix(if (is_labeled) switch_token - 2 else switch_token);
}

fn expectSwitchExpr(p: *Parse, is_labeled: bool) !Node.Index {
    const switch_token = p.assertToken(.keyword_switch);
    return try p.expectSwitchSuffix(if (is_labeled) switch_token - 2 else switch_token);
}

fn expectSwitchSuffix(p: *Parse, main_token: TokenIndex) !Node.Index {
    _ = try p.expectToken(.l_paren);
    const expr_node = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    _ = try p.expectToken(.l_brace);
    const cases = try p.parseSwitchProngList();
    const trailing_comma = p.tokenTag(p.tok_i - 1) == .comma;
    _ = try p.expectToken(.r_brace);

    return p.addNode(.{
        .tag = if (trailing_comma) .switch_comma else .@"switch",
        .main_token = main_token,
        .data = .{ .node_and_extra = .{
            expr_node,
            try p.addExtra(Node.SubRange{
                .start = cases.start,
                .end = cases.end,
            }),
        } },
    });
}

/// AsmExpr <- KEYWORD_asm KEYWORD_volatile? LPAREN Expr AsmOutput? RPAREN
///
/// AsmOutput <- COLON AsmOutputList AsmInput?
///
/// AsmInput <- COLON AsmInputList AsmClobbers?
///
/// AsmClobbers <- COLON Expr
///
/// StringList <- (STRINGLITERAL COMMA)* STRINGLITERAL?
///
/// AsmOutputList <- (AsmOutputItem COMMA)* AsmOutputItem?
///
/// AsmInputList <- (AsmInputItem COMMA)* AsmInputItem?
fn expectAsmExpr(p: *Parse) !Node.Index {
    const asm_token = p.assertToken(.keyword_asm);
    _ = p.eatToken(.keyword_volatile);
    _ = try p.expectToken(.l_paren);
    const template = try p.expectExpr();

    if (p.eatToken(.r_paren)) |rparen| {
        return p.addNode(.{
            .tag = .asm_simple,
            .main_token = asm_token,
            .data = .{ .node_and_token = .{
                template,
                rparen,
            } },
        });
    }

    _ = try p.expectToken(.colon);

    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);

    while (true) {
        const output_item = try p.parseAsmOutputItem() orelse break;
        try p.scratch.append(p.gpa, output_item);
        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            // All possible delimiters.
            .colon, .r_paren, .r_brace, .r_bracket => break,
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warnExpected(.comma),
        }
    }

    const clobbers: Node.OptionalIndex = if (p.eatToken(.colon)) |_| clobbers: {
        while (true) {
            const input_item = try p.parseAsmInputItem() orelse break;
            try p.scratch.append(p.gpa, input_item);
            switch (p.tokenTag(p.tok_i)) {
                .comma => p.tok_i += 1,
                // All possible delimiters.
                .colon, .r_paren, .r_brace, .r_bracket => break,
                // Likely just a missing comma; give error but continue parsing.
                else => try p.warnExpected(.comma),
            }
        }

        _ = p.eatToken(.colon) orelse break :clobbers .none;

        break :clobbers (try p.expectExpr()).toOptional();
    } else .none;

    const rparen = try p.expectToken(.r_paren);
    const span = try p.listToSpan(p.scratch.items[scratch_top..]);
    return p.addNode(.{
        .tag = .@"asm",
        .main_token = asm_token,
        .data = .{ .node_and_extra = .{
            template,
            try p.addExtra(Node.Asm{
                .items_start = span.start,
                .items_end = span.end,
                .clobbers = clobbers,
                .rparen = rparen,
            }),
        } },
    });
}

/// AsmOutputItem <- LBRACKET IDENTIFIER RBRACKET STRINGLITERALSINGLE LPAREN (MINUSRARROW TypeExpr / IDENTIFIER) RPAREN
fn parseAsmOutputItem(p: *Parse) !?Node.Index {
    _ = p.eatToken(.l_bracket) orelse return null;
    const identifier = try p.expectToken(.identifier);
    _ = try p.expectToken(.r_bracket);
    _ = try p.expectToken(.string_literal);
    _ = try p.expectToken(.l_paren);
    const type_expr: Node.OptionalIndex = blk: {
        if (p.eatToken(.arrow)) |_| {
            break :blk .fromOptional(try p.expectTypeExpr());
        } else {
            _ = try p.expectToken(.identifier);
            break :blk .none;
        }
    };
    const rparen = try p.expectToken(.r_paren);
    return try p.addNode(.{
        .tag = .asm_output,
        .main_token = identifier,
        .data = .{ .opt_node_and_token = .{
            type_expr,
            rparen,
        } },
    });
}

/// AsmInputItem <- LBRACKET IDENTIFIER RBRACKET STRINGLITERALSINGLE LPAREN Expr RPAREN
fn parseAsmInputItem(p: *Parse) !?Node.Index {
    _ = p.eatToken(.l_bracket) orelse return null;
    const identifier = try p.expectToken(.identifier);
    _ = try p.expectToken(.r_bracket);
    _ = try p.expectToken(.string_literal);
    _ = try p.expectToken(.l_paren);
    const expr = try p.expectExpr();
    const rparen = try p.expectToken(.r_paren);
    return try p.addNode(.{
        .tag = .asm_input,
        .main_token = identifier,
        .data = .{ .node_and_token = .{
            expr,
            rparen,
        } },
    });
}

/// BreakLabel <- COLON IDENTIFIER
fn parseBreakLabel(p: *Parse) Error!OptionalTokenIndex {
    return if (p.eatTokens(&.{ .colon, .identifier })) |i| .fromToken(i + 1) else .none;
}

/// BlockLabel <- IDENTIFIER COLON
fn parseBlockLabel(p: *Parse) ?TokenIndex {
    return p.eatTokens(&.{ .identifier, .colon });
}

/// FieldInit <- DOT IDENTIFIER EQUAL Expr
fn parseFieldInit(p: *Parse) !?Node.Index {
    if (p.eatTokens(&.{ .period, .identifier, .equal })) |_| {
        return try p.expectExpr();
    }
    return null;
}

fn expectFieldInit(p: *Parse) !Node.Index {
    if (p.eatTokens(&.{ .period, .identifier, .equal })) |_| {
        return try p.expectExpr();
    }
    return p.fail(.expected_initializer);
}

/// WhileContinueExpr <- COLON LPAREN AssignExpr RPAREN
fn parseWhileContinueExpr(p: *Parse) !?Node.Index {
    _ = p.eatToken(.colon) orelse return null;
    _ = try p.expectToken(.l_paren);
    const node = try p.parseAssignExpr() orelse return p.fail(.expected_expr_or_assignment);
    _ = try p.expectToken(.r_paren);
    return node;
}

/// LinkSection <- KEYWORD_linksection LPAREN Expr RPAREN
fn parseLinkSection(p: *Parse) !?Node.Index {
    _ = p.eatToken(.keyword_linksection) orelse return null;
    _ = try p.expectToken(.l_paren);
    const expr_node = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    return expr_node;
}

/// CallConv <- KEYWORD_callconv LPAREN Expr RPAREN
fn parseCallconv(p: *Parse) !?Node.Index {
    _ = p.eatToken(.keyword_callconv) orelse return null;
    _ = try p.expectToken(.l_paren);
    const expr_node = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    return expr_node;
}

/// AddrSpace <- KEYWORD_addrspace LPAREN Expr RPAREN
fn parseAddrSpace(p: *Parse) !?Node.Index {
    _ = p.eatToken(.keyword_addrspace) orelse return null;
    _ = try p.expectToken(.l_paren);
    const expr_node = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    return expr_node;
}

/// This function can return null nodes and then still return nodes afterwards,
/// such as in the case of anytype and `...`. Caller must look for rparen to find
/// out when there are no more param decls left.
///
/// ParamDecl <- doc_comment? (KEYWORD_noalias / KEYWORD_comptime / !KEYWORD_comptime) (IDENTIFIER COLON / !(IDENTIFIER_COLON)) ParamType
///
/// ParamType
///     <- KEYWORD_anytype
///      / TypeExpr
fn expectParamDecl(p: *Parse) !?Node.Index {
    _ = try p.eatDocComments();
    switch (p.tokenTag(p.tok_i)) {
        .keyword_noalias, .keyword_comptime => p.tok_i += 1,
        .ellipsis3 => {
            p.tok_i += 1;
            return null;
        },
        else => {},
    }
    _ = p.eatTokens(&.{ .identifier, .colon });
    if (p.eatToken(.keyword_anytype)) |_| {
        return null;
    } else {
        return try p.expectTypeExpr();
    }
}

/// Payload <- PIPE IDENTIFIER PIPE
fn parsePayload(p: *Parse) Error!OptionalTokenIndex {
    _ = p.eatToken(.pipe) orelse return .none;
    const identifier = try p.expectToken(.identifier);
    _ = try p.expectToken(.pipe);
    return .fromToken(identifier);
}

/// PtrPayload <- PIPE ASTERISK? IDENTIFIER PIPE
fn parsePtrPayload(p: *Parse) Error!OptionalTokenIndex {
    _ = p.eatToken(.pipe) orelse return .none;
    _ = p.eatToken(.asterisk);
    const identifier = try p.expectToken(.identifier);
    _ = try p.expectToken(.pipe);
    return .fromToken(identifier);
}

/// Returns the first identifier token, if any.
///
/// PtrIndexPayload <- PIPE ASTERISK? IDENTIFIER (COMMA IDENTIFIER)? PIPE
fn parsePtrIndexPayload(p: *Parse) Error!OptionalTokenIndex {
    _ = p.eatToken(.pipe) orelse return .none;
    _ = p.eatToken(.asterisk);
    const identifier = try p.expectToken(.identifier);
    if (p.eatToken(.comma) != null) {
        _ = try p.expectToken(.identifier);
    }
    _ = try p.expectToken(.pipe);
    return .fromToken(identifier);
}

/// SwitchProng <- KEYWORD_inline? SwitchCase EQUALRARROW PtrIndexPayload? AssignExpr
///
/// SwitchCase
///     <- SwitchItem (COMMA SwitchItem)* COMMA?
///      / KEYWORD_else
fn parseSwitchProng(p: *Parse) !?Node.Index {
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);

    const is_inline = p.eatToken(.keyword_inline) != null;

    if (p.eatToken(.keyword_else) == null) {
        while (true) {
            const item = try p.parseSwitchItem() orelse break;
            try p.scratch.append(p.gpa, item);
            if (p.eatToken(.comma) == null) break;
        }
        if (scratch_top == p.scratch.items.len) {
            if (is_inline) p.tok_i -= 1;
            return null;
        }
    }
    const arrow_token = try p.expectToken(.equal_angle_bracket_right);
    _ = try p.parsePtrIndexPayload();

    const items = p.scratch.items[scratch_top..];
    if (items.len <= 1) {
        return try p.addNode(.{
            .tag = if (is_inline) .switch_case_inline_one else .switch_case_one,
            .main_token = arrow_token,
            .data = .{ .opt_node_and_node = .{
                if (items.len >= 1) items[0].toOptional() else .none,
                try p.expectSingleAssignExpr(),
            } },
        });
    } else {
        return try p.addNode(.{
            .tag = if (is_inline) .switch_case_inline else .switch_case,
            .main_token = arrow_token,
            .data = .{ .extra_and_node = .{
                try p.addExtra(try p.listToSpan(items)),
                try p.expectSingleAssignExpr(),
            } },
        });
    }
}

/// SwitchItem <- Expr (DOT3 Expr)?
fn parseSwitchItem(p: *Parse) !?Node.Index {
    const expr = try p.parseExpr() orelse return null;

    if (p.eatToken(.ellipsis3)) |token| {
        return try p.addNode(.{
            .tag = .switch_range,
            .main_token = token,
            .data = .{ .node_and_node = .{
                expr,
                try p.expectExpr(),
            } },
        });
    }
    return expr;
}

/// The following invariant will hold:
/// - `(bit_range_start == .none) == (bit_range_end == .none)`
/// - `bit_range_start != .none` implies `align_node != .none`
/// - `bit_range_end != .none` implies `align_node != .none`
const PtrModifiers = struct {
    align_node: Node.OptionalIndex,
    addrspace_node: Node.OptionalIndex,
    bit_range_start: Node.OptionalIndex,
    bit_range_end: Node.OptionalIndex,
};

fn parsePtrModifiers(p: *Parse) !PtrModifiers {
    var result: PtrModifiers = .{
        .align_node = .none,
        .addrspace_node = .none,
        .bit_range_start = .none,
        .bit_range_end = .none,
    };
    var saw_const = false;
    var saw_volatile = false;
    var saw_allowzero = false;
    while (true) {
        switch (p.tokenTag(p.tok_i)) {
            .keyword_align => {
                if (result.align_node != .none) {
                    try p.warn(.extra_align_qualifier);
                }
                p.tok_i += 1;
                _ = try p.expectToken(.l_paren);
                result.align_node = (try p.expectExpr()).toOptional();

                if (p.eatToken(.colon)) |_| {
                    result.bit_range_start = (try p.expectExpr()).toOptional();
                    _ = try p.expectToken(.colon);
                    result.bit_range_end = (try p.expectExpr()).toOptional();
                }

                _ = try p.expectToken(.r_paren);
            },
            .keyword_const => {
                if (saw_const) {
                    try p.warn(.extra_const_qualifier);
                }
                p.tok_i += 1;
                saw_const = true;
            },
            .keyword_volatile => {
                if (saw_volatile) {
                    try p.warn(.extra_volatile_qualifier);
                }
                p.tok_i += 1;
                saw_volatile = true;
            },
            .keyword_allowzero => {
                if (saw_allowzero) {
                    try p.warn(.extra_allowzero_qualifier);
                }
                p.tok_i += 1;
                saw_allowzero = true;
            },
            .keyword_addrspace => {
                if (result.addrspace_node != .none) {
                    try p.warn(.extra_addrspace_qualifier);
                }
                result.addrspace_node = .fromOptional(try p.parseAddrSpace());
            },
            else => return result,
        }
    }
}

/// SuffixOp
///     <- LBRACKET Expr (DOT2 (Expr? (COLON Expr)?)?)? RBRACKET
///      / DOT IDENTIFIER
///      / DOTASTERISK
///      / DOTQUESTIONMARK
fn parseSuffixOp(p: *Parse, lhs: Node.Index) !?Node.Index {
    switch (p.tokenTag(p.tok_i)) {
        .l_bracket => {
            const lbracket = p.nextToken();
            const index_expr = try p.expectExpr();

            if (p.eatToken(.ellipsis2)) |_| {
                const opt_end_expr = try p.parseExpr();
                if (p.eatToken(.colon)) |_| {
                    const sentinel = try p.expectExpr();
                    _ = try p.expectToken(.r_bracket);
                    return try p.addNode(.{
                        .tag = .slice_sentinel,
                        .main_token = lbracket,
                        .data = .{ .node_and_extra = .{
                            lhs,
                            try p.addExtra(Node.SliceSentinel{
                                .start = index_expr,
                                .end = .fromOptional(opt_end_expr),
                                .sentinel = sentinel,
                            }),
                        } },
                    });
                }
                _ = try p.expectToken(.r_bracket);
                const end_expr = opt_end_expr orelse {
                    return try p.addNode(.{
                        .tag = .slice_open,
                        .main_token = lbracket,
                        .data = .{ .node_and_node = .{
                            lhs,
                            index_expr,
                        } },
                    });
                };
                return try p.addNode(.{
                    .tag = .slice,
                    .main_token = lbracket,
                    .data = .{ .node_and_extra = .{
                        lhs,
                        try p.addExtra(Node.Slice{
                            .start = index_expr,
                            .end = end_expr,
                        }),
                    } },
                });
            }
            _ = try p.expectToken(.r_bracket);
            return try p.addNode(.{
                .tag = .array_access,
                .main_token = lbracket,
                .data = .{ .node_and_node = .{
                    lhs,
                    index_expr,
                } },
            });
        },
        .period_asterisk => return try p.addNode(.{
            .tag = .deref,
            .main_token = p.nextToken(),
            .data = .{ .node = lhs },
        }),
        .invalid_periodasterisks => {
            try p.warn(.asterisk_after_ptr_deref);
            return try p.addNode(.{
                .tag = .deref,
                .main_token = p.nextToken(),
                .data = .{ .node = lhs },
            });
        },
        .period => switch (p.tokenTag(p.tok_i + 1)) {
            .identifier => return try p.addNode(.{
                .tag = .field_access,
                .main_token = p.nextToken(),
                .data = .{ .node_and_token = .{
                    lhs,
                    p.nextToken(),
                } },
            }),
            .question_mark => return try p.addNode(.{
                .tag = .unwrap_optional,
                .main_token = p.nextToken(),
                .data = .{ .node_and_token = .{
                    lhs,
                    p.nextToken(),
                } },
            }),
            .l_brace => {
                // this a misplaced `.{`, handle the error somewhere else
                return null;
            },
            else => {
                p.tok_i += 1;
                try p.warn(.expected_suffix_op);
                return null;
            },
        },
        else => return null,
    }
}

/// Caller must have already verified the first token.
///
/// ContainerDeclAuto <- ContainerDeclType LBRACE ContainerMembers RBRACE
///
/// ContainerDeclType
///     <- KEYWORD_struct (LPAREN Expr RPAREN)?
///      / KEYWORD_opaque
///      / KEYWORD_enum (LPAREN Expr RPAREN)?
///      / KEYWORD_union (LPAREN (KEYWORD_enum (LPAREN Expr RPAREN)? / Expr) RPAREN)?
fn parseContainerDeclAuto(p: *Parse) !?Node.Index {
    const main_token = p.nextToken();
    const arg_expr = switch (p.tokenTag(main_token)) {
        .keyword_opaque => null,
        .keyword_struct, .keyword_enum => blk: {
            if (p.eatToken(.l_paren)) |_| {
                const expr = try p.expectExpr();
                _ = try p.expectToken(.r_paren);
                break :blk expr;
            } else {
                break :blk null;
            }
        },
        .keyword_union => blk: {
            if (p.eatToken(.l_paren)) |_| {
                if (p.eatToken(.keyword_enum)) |_| {
                    if (p.eatToken(.l_paren)) |_| {
                        const enum_tag_expr = try p.expectExpr();
                        _ = try p.expectToken(.r_paren);
                        _ = try p.expectToken(.r_paren);

                        _ = try p.expectToken(.l_brace);
                        const members = try p.parseContainerMembers();
                        const members_span = try members.toSpan(p);
                        _ = try p.expectToken(.r_brace);
                        return try p.addNode(.{
                            .tag = switch (members.trailing) {
                                true => .tagged_union_enum_tag_trailing,
                                false => .tagged_union_enum_tag,
                            },
                            .main_token = main_token,
                            .data = .{ .node_and_extra = .{
                                enum_tag_expr,
                                try p.addExtra(members_span),
                            } },
                        });
                    } else {
                        _ = try p.expectToken(.r_paren);

                        _ = try p.expectToken(.l_brace);
                        const members = try p.parseContainerMembers();
                        _ = try p.expectToken(.r_brace);
                        if (members.len <= 2) {
                            return try p.addNode(.{
                                .tag = switch (members.trailing) {
                                    true => .tagged_union_two_trailing,
                                    false => .tagged_union_two,
                                },
                                .main_token = main_token,
                                .data = members.data,
                            });
                        } else {
                            const span = try members.toSpan(p);
                            return try p.addNode(.{
                                .tag = switch (members.trailing) {
                                    true => .tagged_union_trailing,
                                    false => .tagged_union,
                                },
                                .main_token = main_token,
                                .data = .{ .extra_range = span },
                            });
                        }
                    }
                } else {
                    const expr = try p.expectExpr();
                    _ = try p.expectToken(.r_paren);
                    break :blk expr;
                }
            } else {
                break :blk null;
            }
        },
        else => {
            p.tok_i -= 1;
            return p.fail(.expected_container);
        },
    };
    _ = try p.expectToken(.l_brace);
    const members = try p.parseContainerMembers();
    _ = try p.expectToken(.r_brace);
    if (arg_expr == null) {
        if (members.len <= 2) {
            return try p.addNode(.{
                .tag = switch (members.trailing) {
                    true => .container_decl_two_trailing,
                    false => .container_decl_two,
                },
                .main_token = main_token,
                .data = members.data,
            });
        } else {
            const span = try members.toSpan(p);
            return try p.addNode(.{
                .tag = switch (members.trailing) {
                    true => .container_decl_trailing,
                    false => .container_decl,
                },
                .main_token = main_token,
                .data = .{ .extra_range = span },
            });
        }
    } else {
        const span = try members.toSpan(p);
        return try p.addNode(.{
            .tag = switch (members.trailing) {
                true => .container_decl_arg_trailing,
                false => .container_decl_arg,
            },
            .main_token = main_token,
            .data = .{ .node_and_extra = .{
                arg_expr.?,
                try p.addExtra(Node.SubRange{
                    .start = span.start,
                    .end = span.end,
                }),
            } },
        });
    }
}

/// Give a helpful error message for those transitioning from
/// C's 'struct Foo {};' to Zig's 'const Foo = struct {};'.
fn parseCStyleContainer(p: *Parse) Error!bool {
    const main_token = p.tok_i;
    switch (p.tokenTag(p.tok_i)) {
        .keyword_enum, .keyword_union, .keyword_struct => {},
        else => return false,
    }
    const identifier = p.tok_i + 1;
    if (p.tokenTag(identifier) != .identifier) return false;
    p.tok_i += 2;

    try p.warnMsg(.{
        .tag = .c_style_container,
        .token = identifier,
        .extra = .{ .expected_tag = p.tokenTag(main_token) },
    });
    try p.warnMsg(.{
        .tag = .zig_style_container,
        .is_note = true,
        .token = identifier,
        .extra = .{ .expected_tag = p.tokenTag(main_token) },
    });

    _ = try p.expectToken(.l_brace);
    _ = try p.parseContainerMembers();
    _ = try p.expectToken(.r_brace);
    try p.expectSemicolon(.expected_semi_after_decl, true);
    return true;
}

/// Holds temporary data until we are ready to construct the full ContainerDecl AST node.
///
/// ByteAlign <- KEYWORD_align LPAREN Expr RPAREN
fn parseByteAlign(p: *Parse) !?Node.Index {
    _ = p.eatToken(.keyword_align) orelse return null;
    _ = try p.expectToken(.l_paren);
    const expr = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    return expr;
}

/// SwitchProngList <- (SwitchProng COMMA)* SwitchProng?
fn parseSwitchProngList(p: *Parse) !Node.SubRange {
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);

    while (true) {
        const item = try parseSwitchProng(p) orelse break;

        try p.scratch.append(p.gpa, item);

        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            // All possible delimiters.
            .colon, .r_paren, .r_brace, .r_bracket => break,
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warn(.expected_comma_after_switch_prong),
        }
    }
    return p.listToSpan(p.scratch.items[scratch_top..]);
}

/// ParamDeclList <- (ParamDecl COMMA)* (ParamDecl / DOT3 COMMA?)?
fn parseParamDeclList(p: *Parse) !SmallSpan {
    _ = try p.expectToken(.l_paren);
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);
    var varargs: union(enum) { none, seen, nonfinal: TokenIndex } = .none;
    while (true) {
        if (p.eatToken(.r_paren)) |_| break;
        if (varargs == .seen) varargs = .{ .nonfinal = p.tok_i };
        const opt_param = try p.expectParamDecl();
        if (opt_param) |param| {
            try p.scratch.append(p.gpa, param);
        } else if (p.tokenTag(p.tok_i - 1) == .ellipsis3) {
            if (varargs == .none) varargs = .seen;
        }
        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            .r_paren => {
                p.tok_i += 1;
                break;
            },
            .colon, .r_brace, .r_bracket => return p.failExpected(.r_paren),
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warn(.expected_comma_after_param),
        }
    }
    if (varargs == .nonfinal) {
        try p.warnMsg(.{ .tag = .varargs_nonfinal, .token = varargs.nonfinal });
    }
    const params = p.scratch.items[scratch_top..];
    return switch (params.len) {
        0 => .{ .zero_or_one = .none },
        1 => .{ .zero_or_one = params[0].toOptional() },
        else => .{ .multi = try p.listToSpan(params) },
    };
}

/// FnCallArguments <- LPAREN ExprList RPAREN
///
/// ExprList <- (Expr COMMA)* Expr?
fn parseBuiltinCall(p: *Parse) !Node.Index {
    const builtin_token = p.assertToken(.builtin);
    _ = p.eatToken(.l_paren) orelse {
        try p.warn(.expected_param_list);
        // Pretend this was an identifier so we can continue parsing.
        return p.addNode(.{
            .tag = .identifier,
            .main_token = builtin_token,
            .data = undefined,
        });
    };
    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);
    while (true) {
        if (p.eatToken(.r_paren)) |_| break;
        const param = try p.expectExpr();
        try p.scratch.append(p.gpa, param);
        switch (p.tokenTag(p.tok_i)) {
            .comma => p.tok_i += 1,
            .r_paren => {
                p.tok_i += 1;
                break;
            },
            // Likely just a missing comma; give error but continue parsing.
            else => try p.warn(.expected_comma_after_arg),
        }
    }
    const comma = (p.tokenTag(p.tok_i - 2)) == .comma;
    const params = p.scratch.items[scratch_top..];
    if (params.len <= 2) {
        return p.addNode(.{
            .tag = if (comma) .builtin_call_two_comma else .builtin_call_two,
            .main_token = builtin_token,
            .data = .{ .opt_node_and_opt_node = .{
                if (params.len >= 1) .fromOptional(params[0]) else .none,
                if (params.len >= 2) .fromOptional(params[1]) else .none,
            } },
        });
    } else {
        const span = try p.listToSpan(params);
        return p.addNode(.{
            .tag = if (comma) .builtin_call_comma else .builtin_call,
            .main_token = builtin_token,
            .data = .{ .extra_range = span },
        });
    }
}

/// IfPrefix <- KEYWORD_if LPAREN Expr RPAREN PtrPayload?
fn parseIf(p: *Parse, comptime bodyParseFn: fn (p: *Parse) Error!Node.Index) !?Node.Index {
    const if_token = p.eatToken(.keyword_if) orelse return null;
    _ = try p.expectToken(.l_paren);
    const condition = try p.expectExpr();
    _ = try p.expectToken(.r_paren);
    _ = try p.parsePtrPayload();

    const then_expr = try bodyParseFn(p);

    _ = p.eatToken(.keyword_else) orelse return try p.addNode(.{
        .tag = .if_simple,
        .main_token = if_token,
        .data = .{ .node_and_node = .{
            condition,
            then_expr,
        } },
    });
    _ = try p.parsePayload();
    const else_expr = try bodyParseFn(p);

    return try p.addNode(.{
        .tag = .@"if",
        .main_token = if_token,
        .data = .{ .node_and_extra = .{
            condition,
            try p.addExtra(Node.If{
                .then_expr = then_expr,
                .else_expr = else_expr,
            }),
        } },
    });
}

/// ForExpr <- ForPrefix Expr (KEYWORD_else Expr / !KEYWORD_else) !ExprSuffix
///
/// ForTypeExpr <- ForPrefix TypeExpr (KEYWORD_else TypeExpr / !KEYWORD_else) !ExprSuffix
fn parseFor(p: *Parse, comptime bodyParseFn: fn (p: *Parse) Error!Node.Index) !?Node.Index {
    const for_token = p.eatToken(.keyword_for) orelse return null;

    const scratch_top = p.scratch.items.len;
    defer p.scratch.shrinkRetainingCapacity(scratch_top);
    const inputs = try p.forPrefix();

    const then_expr = try bodyParseFn(p);
    var has_else = false;
    if (p.eatToken(.keyword_else)) |_| {
        try p.scratch.append(p.gpa, then_expr);
        const else_expr = try bodyParseFn(p);
        try p.scratch.append(p.gpa, else_expr);
        has_else = true;
    } else if (inputs == 1) {
        return try p.addNode(.{
            .tag = .for_simple,
            .main_token = for_token,
            .data = .{ .node_and_node = .{
                p.scratch.items[scratch_top],
                then_expr,
            } },
        });
    } else {
        try p.scratch.append(p.gpa, then_expr);
    }
    return try p.addNode(.{
        .tag = .@"for",
        .main_token = for_token,
        .data = .{ .@"for" = .{
            (try p.listToSpan(p.scratch.items[scratch_top..])).start,
            .{ .inputs = @intCast(inputs), .has_else = has_else },
        } },
    });
}

/// Skips over doc comment tokens. Returns the first one, if any.
fn eatDocComments(p: *Parse) Allocator.Error!?TokenIndex {
    if (p.eatToken(.doc_comment)) |tok| {
        var first_line = tok;
        if (tok > 0 and tokensOnSameLine(p, tok - 1, tok)) {
            try p.warnMsg(.{
                .tag = .same_line_doc_comment,
                .token = tok,
            });
            first_line = p.eatToken(.doc_comment) orelse return null;
        }
        while (p.eatToken(.doc_comment)) |_| {}
        return first_line;
    }
    return null;
}

fn tokensOnSameLine(p: *Parse, token1: TokenIndex, token2: TokenIndex) bool {
    return std.mem.findScalar(u8, p.source[p.tokenStart(token1)..p.tokenStart(token2)], '\n') == null;
}

fn eatToken(p: *Parse, tag: Token.Tag) ?TokenIndex {
    return if (p.tokenTag(p.tok_i) == tag) p.nextToken() else null;
}

fn eatTokens(p: *Parse, tags: []const Token.Tag) ?TokenIndex {
    const available_tags = p.tokens.items(.tag)[p.tok_i..];
    if (!std.mem.startsWith(Token.Tag, available_tags, tags)) return null;
    const result = p.tok_i;
    p.tok_i += @intCast(tags.len);
    return result;
}

fn assertToken(p: *Parse, tag: Token.Tag) TokenIndex {
    const token = p.nextToken();
    assert(p.tokenTag(token) == tag);
    return token;
}

fn expectToken(p: *Parse, tag: Token.Tag) Error!TokenIndex {
    if (p.tokenTag(p.tok_i) != tag) {
        return p.failMsg(.{
            .tag = .expected_token,
            .token = p.tok_i,
            .extra = .{ .expected_tag = tag },
        });
    }
    return p.nextToken();
}

fn expectSemicolon(p: *Parse, error_tag: AstError.Tag, recoverable: bool) Error!void {
    if (p.tokenTag(p.tok_i) == .semicolon) {
        _ = p.nextToken();
        return;
    }
    try p.warn(error_tag);
    if (!recoverable) return error.ParseError;
}

fn nextToken(p: *Parse) TokenIndex {
    const result = p.tok_i;
    p.tok_i += 1;
    return result;
}

const Parse = @This();
const std = @import("../std.zig");
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;
const Ast = std.zig.Ast;
const Node = Ast.Node;
const AstError = Ast.Error;
const TokenIndex = Ast.TokenIndex;
const OptionalTokenIndex = Ast.OptionalTokenIndex;
const ExtraIndex = Ast.ExtraIndex;
const Token = std.zig.Token;

test {
    _ = @import("parser_test.zig");
}



---
File: /std/zig/parser_test.zig
---




---
File: /std/zig/perf_test.zig
---




---
File: /std/zig/primitives.zig
---

const std = @import("std");

/// Set of primitive type and value names.
/// Does not include `_` or integer type names.
pub const names = std.StaticStringMap(void).initComptime(.{
    .{"anyerror"},
    .{"anyframe"},
    .{"anyopaque"},
    .{"bool"},
    .{"c_int"},
    .{"c_long"},
    .{"c_longdouble"},
    .{"c_longlong"},
    .{"c_char"},
    .{"c_short"},
    .{"c_uint"},
    .{"c_ulong"},
    .{"c_ulonglong"},
    .{"c_ushort"},
    .{"comptime_float"},
    .{"comptime_int"},
    .{"f128"},
    .{"f16"},
    .{"f32"},
    .{"f64"},
    .{"f80"},
    .{"false"},
    .{"isize"},
    .{"noreturn"},
    .{"null"},
    .{"true"},
    .{"type"},
    .{"undefined"},
    .{"usize"},
    .{"void"},
});

/// Returns true if a name matches a primitive type or value, excluding `_`.
/// Integer type names like `u8` or `i32` are only matched for syntax,
/// so this will still return true when they have an oversized bit count
/// or leading zeroes.
pub fn isPrimitive(name: []const u8) bool {
    if (names.get(name) != null) return true;
    if (name.len < 2) return false;
    const first_c = name[0];
    if (first_c != 'i' and first_c != 'u') return false;
    for (name[1..]) |c| switch (c) {
        '0'...'9' => {},
        else => return false,
    };
    return true;
}

test isPrimitive {
    const expect = std.testing.expect;
    try expect(!isPrimitive(""));
    try expect(!isPrimitive("_"));
    try expect(!isPrimitive("haberdasher"));
    try expect(isPrimitive("bool"));
    try expect(isPrimitive("false"));
    try expect(isPrimitive("comptime_float"));
    try expect(isPrimitive("u1"));
    try expect(isPrimitive("i99999999999999"));
}



---
File: /std/zig/Server.zig
---

const Server = @This();

const builtin = @import("builtin");

const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const native_endian = builtin.target.cpu.arch.endian();
const need_bswap = native_endian != .little;
const Cache = std.Build.Cache;
const OutMessage = std.zig.Server.Message;
const InMessage = std.zig.Client.Message;
const Reader = std.Io.Reader;
const Writer = std.Io.Writer;

in: *Reader,
out: *Writer,

pub const Message = struct {
    pub const Header = extern struct {
        tag: Tag,
        /// Size of the body only; does not include this Header.
        bytes_len: u32,
    };

    pub const Tag = enum(u32) {
        /// Body is a UTF-8 string.
        zig_version,
        /// Body is an ErrorBundle.
        error_bundle,
        /// Body is a EmitDigest.
        emit_digest,
        /// Body is a TestMetadata
        test_metadata,
        /// Body is a TestResults
        test_results,
        /// Does not have a body.
        /// Notifies the build runner that the next test (requested by `Client.Message.Tag.run_test`)
        /// is starting execution. This message helps to ensure that the timestamp used by the build
        /// runner to enforce unit test time limits is relatively accurate under extreme system load
        /// (where there may be a non-trivial delay before the test process is scheduled).
        test_started,
        /// Body is a series of strings, delimited by null bytes.
        /// Each string is a prefixed file path.
        /// The first byte indicates the file prefix path (see prefixes fields
        /// of Cache). This byte is sent over the wire incremented so that null
        /// bytes are not confused with string terminators.
        /// The remaining bytes is the file path relative to that prefix.
        /// The prefixes are hard-coded in Compilation.create (cwd, zig lib dir, local cache dir)
        file_system_inputs,
        /// Body is:
        /// - a u64le that indicates the file path within the cache used
        ///   to store coverage information. The integer is a hash of the PCs
        ///   stored within that file.
        /// - u64le of total runs accumulated
        /// - u64le of unique runs accumulated
        /// - u64le of coverage accumulated
        coverage_id,
        /// Body is a u64le that indicates the function pointer virtual memory
        /// address of the fuzz unit test. This is used to provide a starting
        /// point to view coverage.
        fuzz_start_addr,
        /// Body is:
        /// - u32le test index.
        fuzz_test_change,
        /// Body is:
        /// - u32le test index
        /// - input in remaining bytes
        broadcast_fuzz_input,
        /// Body is a TimeReport.
        time_report,

        _,
    };

    pub const PathPrefix = enum(u8) {
        cwd,
        zig_lib,
        local_cache,
        global_cache,
    };

    /// Trailing:
    /// * extra: [extra_len]u32,
    /// * string_bytes: [string_bytes_len]u8,
    /// See `std.zig.ErrorBundle`.
    pub const ErrorBundle = extern struct {
        extra_len: u32,
        string_bytes_len: u32,
    };

    /// Trailing:
    /// * name: [tests_len]u32
    ///   - null-terminated string_bytes index
    /// * expected_panic_msg: [tests_len]u32,
    ///   - null-terminated string_bytes index
    ///   - 0 means does not expect panic
    /// * string_bytes: [string_bytes_len]u8,
    pub const TestMetadata = extern struct {
        string_bytes_len: u32,
        tests_len: u32,
    };

    pub const TestResults = extern struct {
        index: u32,
        flags: Flags align(4),

        pub const Flags = packed struct(u64) {
            status: Status,
            fuzz: bool,
            log_err_count: u30,
            leak_count: u31,
        };

        pub const Status = enum(u2) { pass, fail, skip };
    };

    /// Trailing is the same as in `std.Build.abi.time_report.CompileResult`, excluding `step_name`.
    pub const TimeReport = extern struct {
        stats: std.Build.abi.time_report.CompileResult.Stats align(4),
        llvm_pass_timings_len: u32,
        files_len: u32,
        decls_len: u32,
        flags: Flags,
        pub const Flags = packed struct(u32) {
            use_llvm: bool,
            _: u31 = 0,
        };
    };

    /// Trailing:
    /// * the hex digest of the cache directory within the /o/ subdirectory.
    pub const EmitDigest = extern struct {
        flags: Flags,

        pub const Flags = packed struct(u8) {
            cache_hit: bool,
            reserved: u7 = 0,
        };
    };
};

pub const Options = struct {
    in: *Reader,
    out: *Writer,
    zig_version: []const u8,
};

pub fn init(options: Options) !Server {
    var s: Server = .{
        .in = options.in,
        .out = options.out,
    };
    try s.serveStringMessage(.zig_version, options.zig_version);
    return s;
}

pub fn receiveMessage(s: *Server) !InMessage.Header {
    return s.in.takeStruct(InMessage.Header, .little);
}

pub fn receiveBody_u8(s: *Server) !u8 {
    return s.in.takeInt(u8, .little);
}
pub fn receiveBody_u32(s: *Server) !u32 {
    return s.in.takeInt(u32, .little);
}
pub fn receiveBody_u64(s: *Server) !u64 {
    return s.in.takeInt(u64, .little);
}

pub fn serveStringMessage(s: *Server, tag: OutMessage.Tag, msg: []const u8) !void {
    try s.serveMessageHeader(.{
        .tag = tag,
        .bytes_len = @intCast(msg.len),
    });
    try s.out.writeAll(msg);
    try s.out.flush();
}

/// Don't forget to flush!
pub fn serveMessageHeader(s: *const Server, header: OutMessage.Header) !void {
    try s.out.writeStruct(header, .little);
}

pub fn serveU32Message(s: *const Server, tag: OutMessage.Tag, int: u32) !void {
    try serveMessageHeader(s, .{
        .tag = tag,
        .bytes_len = @sizeOf(u32),
    });
    try s.out.writeInt(u32, int, .little);
    try s.out.flush();
}

pub fn serveU64Message(s: *const Server, tag: OutMessage.Tag, int: u64) !void {
    assert(tag != .coverage_id);
    try serveMessageHeader(s, .{
        .tag = tag,
        .bytes_len = @sizeOf(u64),
    });
    try s.out.writeInt(u64, int, .little);
    try s.out.flush();
}

pub fn serveCoverageIdMessage(s: *const Server, id: u64, runs: u64, unique: u64, cov: u64) !void {
    try serveMessageHeader(s, .{
        .tag = .coverage_id,
        .bytes_len = @sizeOf(u64) + @sizeOf(u64) + @sizeOf(u64) + @sizeOf(u64),
    });
    try s.out.writeInt(u64, id, .little);
    try s.out.writeInt(u64, runs, .little);
    try s.out.writeInt(u64, unique, .little);
    try s.out.writeInt(u64, cov, .little);
    try s.out.flush();
}

pub fn serveBroadcastFuzzInputMessage(s: *const Server, test_i: u32, bytes: []const u8) !void {
    try s.serveMessageHeader(.{
        .tag = .broadcast_fuzz_input,
        .bytes_len = @sizeOf(u32) + @as(u32, @intCast(bytes.len)),
    });
    try s.out.writeInt(u32, test_i, .little);
    try s.out.writeAll(bytes);
    try s.out.flush();
}

pub fn serveEmitDigest(
    s: *Server,
    digest: *const [Cache.bin_digest_len]u8,
    header: OutMessage.EmitDigest,
) !void {
    try s.serveMessageHeader(.{
        .tag = .emit_digest,
        .bytes_len = @intCast(digest.len + @sizeOf(OutMessage.EmitDigest)),
    });
    try s.out.writeStruct(header, .little);
    try s.out.writeAll(digest);
    try s.out.flush();
}

pub fn serveTestResults(s: *Server, msg: OutMessage.TestResults) !void {
    try s.serveMessageHeader(.{
        .tag = .test_results,
        .bytes_len = @intCast(@sizeOf(OutMessage.TestResults)),
    });
    try s.out.writeStruct(msg, .little);
    try s.out.flush();
}

pub fn serveErrorBundle(s: *Server, error_bundle: std.zig.ErrorBundle) !void {
    const eb_hdr: OutMessage.ErrorBundle = .{
        .extra_len = @intCast(error_bundle.extra.len),
        .string_bytes_len = @intCast(error_bundle.string_bytes.len),
    };
    const bytes_len = @sizeOf(OutMessage.ErrorBundle) +
        4 * error_bundle.extra.len + error_bundle.string_bytes.len;
    try s.serveMessageHeader(.{
        .tag = .error_bundle,
        .bytes_len = @intCast(bytes_len),
    });
    try s.out.writeStruct(eb_hdr, .little);
    try s.out.writeSliceEndian(u32, error_bundle.extra, .little);
    try s.out.writeAll(error_bundle.string_bytes);
    try s.out.flush();
}

pub fn allocErrorBundle(gpa: std.mem.Allocator, body: []const u8) error{ OutOfMemory, EndOfStream }!std.zig.ErrorBundle {
    var r: Reader = .fixed(body);
    const hdr = r.takeStruct(OutMessage.ErrorBundle, .little) catch |err| switch (err) {
        error.EndOfStream => |e| return e,
        error.ReadFailed => unreachable,
    };

    var eb: std.zig.ErrorBundle = .{
        .string_bytes = &.{},
        .extra = &.{},
    };
    errdefer eb.deinit(gpa);

    const extra = try gpa.alloc(u32, hdr.extra_len);
    eb.extra = extra;
    const string_bytes = try gpa.alloc(u8, hdr.string_bytes_len);
    eb.string_bytes = string_bytes;

    r.readSliceEndian(u32, extra, .little) catch |err| switch (err) {
        error.EndOfStream => |e| return e,
        error.ReadFailed => unreachable,
    };
    r.readSliceAll(string_bytes) catch |err| switch (err) {
        error.EndOfStream => |e| return e,
        error.ReadFailed => unreachable,
    };

    return eb;
}

pub const TestMetadata = struct {
    names: []const u32,
    expected_panic_msgs: []const u32,
    string_bytes: []const u8,
};

pub fn serveTestMetadata(s: *Server, test_metadata: TestMetadata) !void {
    const header: OutMessage.TestMetadata = .{
        .tests_len = @intCast(test_metadata.names.len),
        .string_bytes_len = @intCast(test_metadata.string_bytes.len),
    };
    const trailing = 2;
    const bytes_len = @sizeOf(OutMessage.TestMetadata) +
        trailing * @sizeOf(u32) * test_metadata.names.len + test_metadata.string_bytes.len;

    try s.serveMessageHeader(.{
        .tag = .test_metadata,
        .bytes_len = @intCast(bytes_len),
    });
    try s.out.writeStruct(header, .little);
    try s.out.writeSliceEndian(u32, test_metadata.names, .little);
    try s.out.writeSliceEndian(u32, test_metadata.expected_panic_msgs, .little);
    try s.out.writeAll(test_metadata.string_bytes);
    try s.out.flush();
}



---
File: /std/zig/string_literal.zig
---

const std = @import("../std.zig");
const assert = std.debug.assert;
const utf8Encode = std.unicode.utf8Encode;
const Writer = std.Io.Writer;

pub const ParseError = error{
    OutOfMemory,
    InvalidLiteral,
};

pub const ParsedCharLiteral = union(enum) {
    success: u21,
    failure: Error,
};

pub const Result = union(enum) {
    success,
    failure: Error,
};

pub const Error = union(enum) {
    /// The character after backslash is missing or not recognized.
    invalid_escape_character: usize,
    /// Expected hex digit at this index.
    expected_hex_digit: usize,
    /// Unicode escape sequence had no digits with rbrace at this index.
    empty_unicode_escape_sequence: usize,
    /// Expected hex digit or '}' at this index.
    expected_hex_digit_or_rbrace: usize,
    /// Invalid unicode codepoint at this index.
    invalid_unicode_codepoint: usize,
    /// Expected '{' at this index.
    expected_lbrace: usize,
    /// Expected '}' at this index.
    expected_rbrace: usize,
    /// Expected '\'' at this index.
    expected_single_quote: usize,
    /// The character at this index cannot be represented without an escape sequence.
    invalid_character: usize,
    /// `''`. Not returned for string literals.
    empty_char_literal,

    const FormatMessage = struct {
        err: Error,
        raw_string: []const u8,
    };

    fn formatMessage(self: FormatMessage, writer: *Writer) Writer.Error!void {
        switch (self.err) {
            .invalid_escape_character => |bad_index| try writer.print(
                "invalid escape character: '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .expected_hex_digit => |bad_index| try writer.print(
                "expected hex digit, found '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .empty_unicode_escape_sequence => try writer.writeAll(
                "empty unicode escape sequence",
            ),
            .expected_hex_digit_or_rbrace => |bad_index| try writer.print(
                "expected hex digit or '}}', found '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .invalid_unicode_codepoint => try writer.writeAll(
                "unicode escape does not correspond to a valid unicode scalar value",
            ),
            .expected_lbrace => |bad_index| try writer.print(
                "expected '{{', found '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .expected_rbrace => |bad_index| try writer.print(
                "expected '}}', found '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .expected_single_quote => |bad_index| try writer.print(
                "expected single quote ('), found '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .invalid_character => |bad_index| try writer.print(
                "invalid byte in string or character literal: '{c}'",
                .{self.raw_string[bad_index]},
            ),
            .empty_char_literal => try writer.writeAll(
                "empty character literal",
            ),
        }
    }

    pub fn fmt(self: @This(), raw_string: []const u8) std.fmt.Alt(FormatMessage, formatMessage) {
        return .{ .data = .{
            .err = self,
            .raw_string = raw_string,
        } };
    }

    pub fn offset(err: Error) usize {
        return switch (err) {
            inline .invalid_escape_character,
            .expected_hex_digit,
            .empty_unicode_escape_sequence,
            .expected_hex_digit_or_rbrace,
            .invalid_unicode_codepoint,
            .expected_lbrace,
            .expected_rbrace,
            .expected_single_quote,
            .invalid_character,
            => |n| n,
            .empty_char_literal => 0,
        };
    }
};

/// Asserts the slice starts and ends with single-quotes.
/// Returns an error if there is not exactly one UTF-8 codepoint in between.
pub fn parseCharLiteral(slice: []const u8) ParsedCharLiteral {
    if (slice.len < 3) return .{ .failure = .empty_char_literal };
    assert(slice[0] == '\'');
    assert(slice[slice.len - 1] == '\'');

    switch (slice[1]) {
        '\\' => {
            var offset: usize = 1;
            const result = parseEscapeSequence(slice, &offset);
            if (result == .success and (offset + 1 != slice.len or slice[offset] != '\''))
                return .{ .failure = .{ .expected_single_quote = offset } };

            return result;
        },
        0 => return .{ .failure = .{ .invalid_character = 1 } },
        else => {
            const inner = slice[1 .. slice.len - 1];
            const n = std.unicode.utf8ByteSequenceLength(inner[0]) catch return .{
                .failure = .{ .invalid_unicode_codepoint = 1 },
            };
            if (inner.len > n) return .{ .failure = .{ .expected_single_quote = 1 + n } };
            const codepoint = switch (n) {
                1 => inner[0],
                2 => std.unicode.utf8Decode2(inner[0..2].*),
                3 => std.unicode.utf8Decode3(inner[0..3].*),
                4 => std.unicode.utf8Decode4(inner[0..4].*),
                else => unreachable,
            } catch return .{ .failure = .{ .invalid_unicode_codepoint = 1 } };
            return .{ .success = codepoint };
        },
    }
}

/// Parse an escape sequence from `slice[offset..]`. If parsing is successful,
/// offset is updated to reflect the characters consumed.
pub fn parseEscapeSequence(slice: []const u8, offset: *usize) ParsedCharLiteral {
    assert(slice.len > offset.*);
    assert(slice[offset.*] == '\\');

    if (slice.len == offset.* + 1)
        return .{ .failure = .{ .invalid_escape_character = offset.* + 1 } };

    offset.* += 2;
    switch (slice[offset.* - 1]) {
        'n' => return .{ .success = '\n' },
        'r' => return .{ .success = '\r' },
        '\\' => return .{ .success = '\\' },
        't' => return .{ .success = '\t' },
        '\'' => return .{ .success = '\'' },
        '"' => return .{ .success = '"' },
        'x' => {
            var value: u8 = 0;
            var i: usize = offset.*;
            while (i < offset.* + 2) : (i += 1) {
                if (i == slice.len) return .{ .failure = .{ .expected_hex_digit = i } };

                const c = slice[i];
                switch (c) {
                    '0'...'9' => {
                        value *= 16;
                        value += c - '0';
                    },
                    'a'...'f' => {
                        value *= 16;
                        value += c - 'a' + 10;
                    },
                    'A'...'F' => {
                        value *= 16;
                        value += c - 'A' + 10;
                    },
                    else => {
                        return .{ .failure = .{ .expected_hex_digit = i } };
                    },
                }
            }
            offset.* = i;
            return .{ .success = value };
        },
        'u' => {
            var i: usize = offset.*;
            if (i >= slice.len or slice[i] != '{') return .{ .failure = .{ .expected_lbrace = i } };
            i += 1;
            if (i >= slice.len) return .{ .failure = .{ .expected_hex_digit_or_rbrace = i } };
            if (slice[i] == '}') return .{ .failure = .{ .empty_unicode_escape_sequence = i } };

            var value: u32 = 0;
            while (i < slice.len) : (i += 1) {
                const c = slice[i];
                switch (c) {
                    '0'...'9' => {
                        value *= 16;
                        value += c - '0';
                    },
                    'a'...'f' => {
                        value *= 16;
                        value += c - 'a' + 10;
                    },
                    'A'...'F' => {
                        value *= 16;
                        value += c - 'A' + 10;
                    },
                    '}' => {
                        i += 1;
                        break;
                    },
                    else => return .{ .failure = .{ .expected_hex_digit_or_rbrace = i } },
                }
                if (value > 0x10ffff) {
                    return .{ .failure = .{ .invalid_unicode_codepoint = i } };
                }
            } else {
                return .{ .failure = .{ .expected_rbrace = i } };
            }
            offset.* = i;
            return .{ .success = @as(u21, @intCast(value)) };
        },
        else => return .{ .failure = .{ .invalid_escape_character = offset.* - 1 } },
    }
}

test parseCharLiteral {
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 'a' },
        parseCharLiteral("'a'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 'ä' },
        parseCharLiteral("'ä'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0 },
        parseCharLiteral("'\\x00'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0x4f },
        parseCharLiteral("'\\x4f'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0x4f },
        parseCharLiteral("'\\x4F'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0x3041 },
        parseCharLiteral("'ぁ'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0 },
        parseCharLiteral("'\\u{0}'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0x3041 },
        parseCharLiteral("'\\u{3041}'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0x7f },
        parseCharLiteral("'\\u{7f}'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .success = 0x7fff },
        parseCharLiteral("'\\u{7FFF}'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .expected_hex_digit = 4 } },
        parseCharLiteral("'\\x0'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .expected_single_quote = 5 } },
        parseCharLiteral("'\\x000'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .invalid_escape_character = 2 } },
        parseCharLiteral("'\\y'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .expected_lbrace = 3 } },
        parseCharLiteral("'\\u'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .expected_lbrace = 3 } },
        parseCharLiteral("'\\uFFFF'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .empty_unicode_escape_sequence = 4 } },
        parseCharLiteral("'\\u{}'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .invalid_unicode_codepoint = 9 } },
        parseCharLiteral("'\\u{FFFFFF}'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .expected_hex_digit_or_rbrace = 8 } },
        parseCharLiteral("'\\u{FFFF'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .expected_single_quote = 9 } },
        parseCharLiteral("'\\u{FFFF}x'"),
    );
    try std.testing.expectEqual(
        ParsedCharLiteral{ .failure = .{ .invalid_character = 1 } },
        parseCharLiteral("'\x00'"),
    );
}

/// Parses `bytes` as a Zig string literal and writes the result to the `Writer` type.
///
/// Asserts `bytes` has '"' at beginning and end.
pub fn parseWrite(writer: *Writer, bytes: []const u8) Writer.Error!Result {
    assert(bytes.len >= 2 and bytes[0] == '"' and bytes[bytes.len - 1] == '"');

    var index: usize = 1;
    while (true) {
        const b = bytes[index];

        switch (b) {
            '\\' => {
                const escape_char_index = index + 1;
                const result = parseEscapeSequence(bytes, &index);
                switch (result) {
                    .success => |codepoint| {
                        if (bytes[escape_char_index] == 'u') {
                            var buf: [4]u8 = undefined;
                            const len = utf8Encode(codepoint, &buf) catch {
                                return .{ .failure = .{ .invalid_unicode_codepoint = escape_char_index + 1 } };
                            };
                            try writer.writeAll(buf[0..len]);
                        } else {
                            try writer.writeByte(@as(u8, @intCast(codepoint)));
                        }
                    },
                    .failure => |err| return .{ .failure = err },
                }
            },
            '\n' => return .{ .failure = .{ .invalid_character = index } },
            '"' => return .success,
            else => {
                try writer.writeByte(b);
                index += 1;
            },
        }
    }
}

/// Higher level API. Does not return extra info about parse errors.
/// Caller owns returned memory.
pub fn parseAlloc(allocator: std.mem.Allocator, bytes: []const u8) ParseError![]u8 {
    var aw: Writer.Allocating = .init(allocator);
    defer aw.deinit();
    const result = parseWrite(&aw.writer, bytes) catch |err| switch (err) {
        error.WriteFailed => return error.OutOfMemory,
    };
    switch (result) {
        .success => return aw.toOwnedSlice(),
        .failure => return error.InvalidLiteral,
    }
}

test parseAlloc {
    const expect = std.testing.expect;
    const expectError = std.testing.expectError;
    const eql = std.mem.eql;

    var fixed_buf_mem: [512]u8 = undefined;
    var fixed_buf_alloc = std.heap.FixedBufferAllocator.init(&fixed_buf_mem);
    const alloc = fixed_buf_alloc.allocator();

    try expectError(error.InvalidLiteral, parseAlloc(alloc, "\"\\x6\""));
    try expect(eql(u8, "foo\nbar", try parseAlloc(alloc, "\"foo\\nbar\"")));
    try expect(eql(u8, "\x12foo", try parseAlloc(alloc, "\"\\x12foo\"")));
    try expect(eql(u8, "bytes\u{1234}foo", try parseAlloc(alloc, "\"bytes\\u{1234}foo\"")));
    try expect(eql(u8, "foo", try parseAlloc(alloc, "\"foo\"")));
    try expect(eql(u8, "foo", try parseAlloc(alloc, "\"f\x6f\x6f\"")));
    try expect(eql(u8, "f💯", try parseAlloc(alloc, "\"f\u{1f4af}\"")));
}



---
File: /std/zig/system.zig
---

const builtin = @import("builtin");
const native_endian = builtin.cpu.arch.endian();

const std = @import("../std.zig");
const mem = std.mem;
const elf = std.elf;
const fs = std.fs;
const assert = std.debug.assert;
const Target = std.Target;
const posix = std.posix;
const Io = std.Io;

pub const NativePaths = @import("system/NativePaths.zig");

pub const windows = @import("system/windows.zig");
pub const darwin = @import("system/darwin.zig");
pub const linux = @import("system/linux.zig");

pub const Executor = union(enum) {
    native,
    rosetta,
    qemu: []const u8,
    wine: []const u8,
    wasmtime: []const u8,
    darling: [
```
