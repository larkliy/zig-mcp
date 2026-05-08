```
stToken(e);
            after_expr += @intFromBool(tree.tokenTag(after_expr + 1) == .comma);
            try renderSpace(&sub_r, after_expr, tokenSliceForRender(tree, after_expr).len, .none);

            buf.clearRetainingCapacity();
            // The following are needed to make sure isLineOverIndented is not influenced by
            // the previous element.
            sub_ais.indent_count = 0;
            sub_ais.applied_indent = 0;
        }
    }

    var remaining_exprs = array_init.ast.elements;
    var remaining_widths = expr_widths;
    while (remaining_exprs.len != 0) {
        var row_size: usize = 1;
        for (1.., remaining_exprs, remaining_widths) |len, e, w| {
            if (w == .nonprint) break;
            row_size = len;

            var after_expr = tree.lastToken(e);
            after_expr += @intFromBool(tree.tokenTag(after_expr + 1) == .comma);
            assert(tree.tokenTag(after_expr) == .comma or after_expr + 1 == rbrace);
            if (!tree.tokensOnSameLine(after_expr, after_expr + 1))
                break;
        } else {
            // All the expressions are on the same line.
            // However, if there is a trailing comma, we put them each on their own line.
            if (tree.tokenTag(rbrace - 1) == .comma)
                row_size = 1;
        }

        // Determine the size of this section
        const section_end = end: {
            var line_start = row_size; // Start after the first row to ignore comments on it
            break :end for (line_start.., remaining_exprs[line_start..]) |i, e| {
                const expr_first = tree.firstToken(e);
                // Any nonprint character terminates the line because they are always put on their
                // own line, so they will not end up on the same line as the trailing comment.
                if (expr_widths[i - 1] == .nonprint or !tree.tokensOnSameLine(expr_first - 1, expr_first)) {
                    line_start = i;
                }

                var after_expr = tree.lastToken(e);
                after_expr += @intFromBool(tree.tokenTag(after_expr + 1) == .comma);
                assert(tree.tokenTag(after_expr) == .comma or after_expr + 1 == rbrace);
                if (hasTrailingComment(tree, after_expr))
                    break line_start;
            } else remaining_exprs.len;
        };
        const section_exprs = remaining_exprs[0..section_end];
        const section_widths = remaining_widths[0..section_end];
        remaining_exprs = remaining_exprs[section_end..];
        remaining_widths = remaining_widths[section_end..];

        // Determine the width of each column
        var col_widths = try gpa.alloc(usize, row_size);
        defer gpa.free(col_widths);
        @memset(col_widths, 0);

        var col: usize = 0;
        for (section_widths) |w| {
            if (w == .nonprint) {
                col = 0;
                continue;
            }
            col_widths[col] = @max(col_widths[col], @intFromEnum(w));
            col += 1;
            if (col == row_size) {
                col = 0;
            }
        }

        // Render each expression
        col = 0;
        for (0.., section_exprs, section_widths) |i, e, w| {
            if (i + 1 == section_end or col + 1 == row_size or
                w == .nonprint or section_widths[i + 1] == .nonprint)
            {
                try renderExpression(r, e, .comma);
                col = 0;
                if (i + 1 != section_end) {
                    try renderExtraNewline(r, section_exprs[i + 1]);
                }
            } else {
                try renderExpression(r, e, .comma_space);
                try ais.splatByteAll(' ', col_widths[col] - @intFromEnum(w));
                col += 1;
            }
        }
    }

    ais.popSpace();
    ais.popIndent();
    return renderToken(r, rbrace, space); // rbrace
}

fn isOneLineErrorSetDecl(
    tree: Ast,
    lbrace: Ast.TokenIndex,
    rbrace: Ast.TokenIndex,
) bool {
    // If there is a trailing comma, comment, or document comment, then render each
    // item on its own line.
    return tree.tokenTag(rbrace - 1) != .comma and
        !hasDocComment(tree, lbrace + 1, rbrace) and
        !hasComment(tree, lbrace, rbrace);
}

fn isOneLineContainerDecl(
    tree: Ast,
    container_decl: Ast.full.ContainerDecl,
    lbrace: Ast.TokenIndex,
    rbrace: Ast.TokenIndex,
) bool {
    // We print all the members in one-line unless one of the following conditions are true:

    // 1. The container has comments or multiline strings.
    if (hasComment(tree, lbrace, rbrace) or hasMultilineString(tree, lbrace, rbrace)) {
        return false;
    }

    // 2. The container has a container comment.
    if (tree.tokenTag(lbrace + 1) == .container_doc_comment) return false;

    // 3. A member of the container has a doc comment.
    if (hasDocComment(tree, lbrace + 1, rbrace))
        return false;

    // 4. The container has non-field members.
    for (container_decl.ast.members) |member| {
        if (tree.fullContainerField(member) == null) return false;
    }

    return true;
}

fn renderContainerDecl(
    r: *Render,
    container_decl_node: Ast.Node.Index,
    container_decl: Ast.full.ContainerDecl,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    if (container_decl.layout_token) |layout_token| {
        try renderToken(r, layout_token, .space);
    }

    const container: Container = switch (tree.tokenTag(container_decl.ast.main_token)) {
        .keyword_enum => .@"enum",
        .keyword_struct => for (container_decl.ast.members) |member| {
            if (tree.fullContainerField(member)) |field| if (!field.ast.tuple_like) break .other;
        } else .tuple,
        else => .other,
    };

    var lbrace: Ast.TokenIndex = undefined;
    if (container_decl.ast.enum_token) |enum_token| {
        try renderToken(r, container_decl.ast.main_token, .none); // union
        try renderToken(r, enum_token - 1, .none); // lparen
        try renderToken(r, enum_token, .none); // enum
        if (container_decl.ast.arg.unwrap()) |arg| {
            try renderToken(r, enum_token + 1, .none); // lparen
            try renderExpression(r, arg, .none);
            const rparen = tree.lastToken(arg) + 1;
            try renderToken(r, rparen, .none); // rparen
            try renderToken(r, rparen + 1, .space); // rparen
            lbrace = rparen + 2;
        } else {
            try renderToken(r, enum_token + 1, .space); // rparen
            lbrace = enum_token + 2;
        }
    } else if (container_decl.ast.arg.unwrap()) |arg| {
        try renderToken(r, container_decl.ast.main_token, .none); // union
        try renderToken(r, container_decl.ast.main_token + 1, .none); // lparen
        try renderExpression(r, arg, .none);
        const rparen = tree.lastToken(arg) + 1;
        try renderToken(r, rparen, .space); // rparen
        lbrace = rparen + 1;
    } else {
        try renderToken(r, container_decl.ast.main_token, .space); // union
        lbrace = container_decl.ast.main_token + 1;
    }

    const rbrace = tree.lastToken(container_decl_node);

    if (container_decl.ast.members.len == 0) {
        try ais.pushIndent(.normal);
        if (tree.tokenTag(lbrace + 1) == .container_doc_comment) {
            try renderToken(r, lbrace, .newline); // lbrace
            try renderContainerDocComments(r, lbrace + 1);
        } else {
            try renderToken(r, lbrace, .none); // lbrace
        }
        ais.popIndent();
        return renderToken(r, rbrace, space); // rbrace
    }

    const src_has_trailing_comma = tree.tokenTag(rbrace - 1) == .comma;
    if (!src_has_trailing_comma and isOneLineContainerDecl(tree, container_decl, lbrace, rbrace)) {
        // Print all the declarations on the same line.
        try renderToken(r, lbrace, .space); // lbrace
        for (container_decl.ast.members) |member| {
            try renderMember(r, container, member, .space);
        }
        return renderToken(r, rbrace, space); // rbrace
    }

    // One member per line.
    try ais.pushIndent(.normal);
    try renderToken(r, lbrace, .newline); // lbrace
    if (tree.tokenTag(lbrace + 1) == .container_doc_comment) {
        try renderContainerDocComments(r, lbrace + 1);
    }
    for (container_decl.ast.members, 0..) |member, i| {
        if (i != 0) try renderExtraNewline(r, member);
        switch (tree.nodeTag(member)) {
            // For container fields, ensure a trailing comma is added if necessary.
            .container_field_init,
            .container_field_align,
            .container_field,
            => {
                try ais.pushSpace(.comma);
                try renderMember(r, container, member, .comma);
                ais.popSpace();
            },

            else => try renderMember(r, container, member, .newline),
        }
    }
    ais.popIndent();

    return renderToken(r, rbrace, space); // rbrace
}

fn renderAsm(
    r: *Render,
    asm_node: Ast.full.Asm,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    try renderToken(r, asm_node.ast.asm_token, .space); // asm

    if (asm_node.volatile_token) |volatile_token| {
        try renderToken(r, volatile_token, .space); // volatile
        try renderToken(r, volatile_token + 1, .none); // lparen
    } else {
        try renderToken(r, asm_node.ast.asm_token + 1, .none); // lparen
    }

    const render_colons: [3]?Ast.TokenIndex = colons: {
        var colons: [3]Ast.TokenIndex = undefined;
        var render: u2 = 0;

        const rparen = asm_node.ast.rparen;
        filled: {
            colons[0] = tree.lastToken(asm_node.ast.template) + 1;
            if (colons[0] == rparen) break :filled;

            if (asm_node.outputs.len != 0) {
                colons[1] = tree.lastToken(asm_node.outputs[asm_node.outputs.len - 1]) + 1;
                colons[1] += @intFromBool(tree.tokenTag(colons[1]) == .comma);
                render = 1;
            } else {
                colons[1] = colons[0] + 1;
                if (hasComment(tree, colons[0], colons[1])) render = 1;
            }
            if (colons[1] == rparen) break :filled;

            // Next colon is not checked for here since it cannot present without clobbers
            if (asm_node.inputs.len != 0) {
                render = 2;
            } else {
                const colon_or_rparen = colons[1] + 1;
                if (hasComment(tree, colons[1], colon_or_rparen)) render = 2;
            }

            if (asm_node.ast.clobbers.unwrap()) |clobbers| {
                colons[2] = tree.firstToken(clobbers) - 1;
                render = 3;
            }
        }

        var opt_colons: [3]?Ast.TokenIndex = @splat(null);
        for (0..render) |i| opt_colons[i] = colons[i];
        break :colons opt_colons;
    };

    try ais.forcePushIndent(.normal);

    if (asm_node.ast.items.len == 0) {
        if (asm_node.ast.clobbers.unwrap()) |clobbers| {
            // asm ("foo" ::: clobbers)
            try renderExpression(r, asm_node.ast.template, .space);
            // Render the three colons.
            const first_clobber = tree.firstToken(clobbers);
            try renderToken(r, first_clobber - 3, .none);
            try renderToken(r, first_clobber - 2, .none);
            try renderToken(r, first_clobber - 1, .maybe_space);
            try renderExpression(r, clobbers, .none);
            ais.popIndent();
            return renderToken(r, asm_node.ast.rparen, space); // rparen
        }

        if (render_colons[0] == null) {
            // asm ("foo")
            try renderExpression(r, asm_node.ast.template, .none);
            ais.popIndent();
            return renderToken(r, asm_node.ast.rparen, space); // rparen
        }
    }

    try renderExpression(r, asm_node.ast.template, .newline);
    ais.forceLastIndent(); // Might have been dedented by a multiline string literal
    assert(ais.current_line_empty);

    const prev_indent_delta = ais.indent_delta; // May be part of another asm expression
    // so indent_delta can't be unconditionally used
    ais.setIndentDelta(asm_indent_delta);

    rendered: {
        if (render_colons[0]) |colon1| {
            if (asm_node.outputs.len != 0) {
                try renderToken(r, colon1, .space);
                try ais.forcePushIndent(.normal);

                const final = asm_node.outputs.len - 1;
                for (asm_node.outputs[0..final], 0..) |asm_output, i| {
                    try renderAsmOutput(r, asm_output, .none);

                    const next_start = tree.firstToken(asm_node.outputs[i + 1]);
                    try renderToken(r, next_start - 1, .newline); // ,
                    try renderExtraNewlineToken(r, next_start);
                }

                try ais.pushSpace(.comma);
                try renderAsmOutput(r, asm_node.outputs[final], .comma);
                ais.popSpace();
                ais.popIndent();
            } else {
                try renderToken(r, colon1, .newline);
            }
        } else unreachable;

        if (render_colons[1]) |colon2| {
            if (asm_node.inputs.len != 0) {
                try renderToken(r, colon2, .space);
                try ais.forcePushIndent(.normal);

                const final = asm_node.inputs.len - 1;
                for (asm_node.inputs[0..final], 0..) |asm_input, i| {
                    try renderAsmInput(r, asm_input, .none);

                    const next_start = tree.firstToken(asm_node.inputs[i + 1]);
                    try renderToken(r, next_start - 1, .newline); // ,
                    try renderExtraNewlineToken(r, next_start);
                }

                try ais.pushSpace(.comma);
                try renderAsmInput(r, asm_node.inputs[final], .comma);
                ais.popSpace();
                ais.popIndent();
            } else {
                try renderToken(r, colon2, .newline);
            }
        } else break :rendered;

        if (render_colons[2]) |colon3| {
            const clobbers = asm_node.ast.clobbers.unwrap().?;
            try renderToken(r, colon3, .maybe_space);
            try renderExpression(r, clobbers, .none);
            ais.forceLastIndent(); // Might have been dedented by a multiline string literal
        }
    }

    ais.setIndentDelta(prev_indent_delta);
    ais.popIndent();
    return renderToken(r, asm_node.ast.rparen, space); // rparen
}

fn renderCall(
    r: *Render,
    call: Ast.full.Call,
    space: Space,
) Error!void {
    try renderExpression(r, call.ast.fn_expr, .none);
    try renderParamList(r, call.ast.lparen, call.ast.params, space);
}

fn renderParamList(
    r: *Render,
    lparen: Ast.TokenIndex,
    params: []const Ast.Node.Index,
    space: Space,
) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    if (params.len == 0) {
        try ais.pushIndent(.normal);
        try renderToken(r, lparen, .none); // (
        ais.popIndent();
        return renderToken(r, lparen + 1, space); // )
    }

    const last_param = params[params.len - 1];
    const after_last_param_tok = tree.lastToken(last_param) + 1;
    if (tree.tokenTag(after_last_param_tok) == .comma) {
        try ais.pushIndent(.normal);
        try renderToken(r, lparen, .newline); // (
        for (params, 0..) |param_node, i| {
            if (i + 1 < params.len) {
                try renderExpression(r, param_node, .none);

                const comma = tree.lastToken(param_node) + 1;
                try renderToken(r, comma, .newline); // ,

                try renderExtraNewline(r, params[i + 1]);
            } else {
                try ais.pushSpace(.comma);
                try renderExpression(r, param_node, .comma);
                ais.popSpace();
            }
        }
        ais.popIndent();
        return renderToken(r, after_last_param_tok + 1, space); // )
    }

    try ais.pushIndent(.normal);
    try renderToken(r, lparen, .none); // (
    for (params, 0..) |param_node, i| {
        try renderExpression(r, param_node, .none);

        if (i + 1 < params.len) {
            const comma = tree.lastToken(param_node) + 1;
            try renderToken(r, comma, .maybe_space);
        }
    }
    ais.popIndent();
    return renderToken(r, after_last_param_tok, space); // )
}

/// Render an expression, and the comma that follows it, if it is present in the source.
/// If a comma is present, and `space` is `Space.comma`, render only a single comma.
fn renderExpressionComma(r: *Render, node: Ast.Node.Index, space: Space) Error!void {
    const tree = r.tree;
    const maybe_comma = tree.lastToken(node) + 1;
    if (tree.tokenTag(maybe_comma) == .comma and space != .comma) {
        try renderExpression(r, node, .none);
        return renderToken(r, maybe_comma, space);
    } else {
        return renderExpression(r, node, space);
    }
}

/// Render a token, and the comma that follows it, if it is present in the source.
/// If a comma is present, and `space` is `Space.comma`, render only a single comma.
fn renderTokenComma(r: *Render, token: Ast.TokenIndex, space: Space) Error!void {
    const tree = r.tree;
    const maybe_comma = token + 1;
    if (tree.tokenTag(maybe_comma) == .comma and space != .comma) {
        try renderToken(r, token, .none);
        return renderToken(r, maybe_comma, space);
    } else {
        return renderToken(r, token, space);
    }
}

/// Render an identifier, and the comma that follows it, if it is present in the source.
/// If a comma is present, and `space` is `Space.comma`, render only a single comma.
fn renderIdentifierComma(r: *Render, token: Ast.TokenIndex, space: Space, quote: QuoteBehavior) Error!void {
    const tree = r.tree;
    const maybe_comma = token + 1;
    if (tree.tokenTag(maybe_comma) == .comma and space != .comma) {
        try renderIdentifier(r, token, .none, quote);
        return renderToken(r, maybe_comma, space);
    } else {
        return renderIdentifier(r, token, space, quote);
    }
}

const Space = enum {
    /// Output the token lexeme only.
    none,
    /// Output the token lexeme followed by a single space.
    space,
    /// Output the token lexeme followed by a newline.
    newline,
    /// If the next token is a comma, render it as well. If not, insert one.
    /// In either case, a newline will be inserted afterwards.
    comma,
    /// Additionally consume the next token if it is a comma.
    /// In either case, a space will be inserted afterwards.
    comma_space,
    /// Additionally consume the next token if it is a semicolon.
    /// In either case, a newline will be inserted afterwards.
    semicolon,
    /// If the next token is not a multiline string literal, this acts as .space,
    /// otherwise this acts as .none.
    maybe_space,
    /// Additionally consume the next token if it is a comma.
    /// In either case, a space will be inserted afterwards
    /// if the following token is not a multiline string literal.
    comma_maybe_space,
    /// Skip rendering whitespace and comments. If this is used, the caller
    /// *must* handle whitespace and comments manually.
    skip,
};

fn renderToken(r: *Render, token_index: Ast.TokenIndex, space: Space) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const lexeme = tokenSliceForRender(tree, token_index);
    try ais.writeAll(lexeme);
    try renderSpace(r, token_index, lexeme.len, space);
}

fn renderTokenOverrideSpaceMode(r: *Render, token_index: Ast.TokenIndex, space: Space, override_space: Space) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const lexeme = tokenSliceForRender(tree, token_index);
    try ais.writeAll(lexeme);
    ais.enableSpaceMode(override_space);
    defer ais.disableSpaceMode();
    try renderSpace(r, token_index, lexeme.len, space);
}

fn renderSpace(r: *Render, token_index: Ast.TokenIndex, lexeme_len: usize, space: Space) Error!void {
    const tree = r.tree;
    const ais = r.ais;

    const next_token_tag = tree.tokenTag(token_index + 1);

    if (space == .skip) return;

    if (space == .comma and next_token_tag != .comma) {
        try ais.writeByte(',');
    }
    if (space == .semicolon or space == .comma) ais.enableSpaceMode(space);
    defer ais.disableSpaceMode();
    const comment = try renderComments(
        r,
        tree.tokenStart(token_index) + lexeme_len,
        tree.tokenStart(token_index + 1),
    );
    switch (space) {
        .none => {},
        .space => if (!comment) try ais.writeByte(' '),
        .newline => if (!comment) try ais.insertNewline(),

        .comma => if (next_token_tag == .comma) {
            try renderToken(r, token_index + 1, .newline);
        } else if (!comment) {
            try ais.insertNewline();
        },

        .comma_space => if (next_token_tag == .comma) {
            try renderToken(r, token_index + 1, .space);
        } else if (!comment) {
            try ais.writeByte(' ');
        },

        .semicolon => if (next_token_tag == .semicolon) {
            try renderToken(r, token_index + 1, .newline);
        } else if (!comment) {
            try ais.insertNewline();
        },

        .maybe_space => if (!comment and next_token_tag != .multiline_string_literal_line) {
            try ais.writeByte(' ');
        },

        .comma_maybe_space => if (next_token_tag == .comma) {
            try renderToken(r, token_index + 1, .maybe_space);
        } else if (!comment) {
            try ais.writeByte(' ');
        },

        .skip => unreachable,
    }
}

fn renderOnlySpace(r: *Render, space: Space) Error!void {
    const ais = r.ais;
    switch (space) {
        .none => {},
        .space, .maybe_space => try ais.writeByte(' '),
        .newline => try ais.insertNewline(),
        .comma, .comma_maybe_space => try ais.writeAll(",\n"),
        .comma_space => try ais.writeAll(", "),
        .semicolon => try ais.writeAll(";\n"),
        .skip => unreachable,
    }
}

const QuoteBehavior = enum {
    preserve_when_shadowing,
    eagerly_unquote,
    eagerly_unquote_except_underscore,
};

fn renderIdentifier(r: *Render, token_index: Ast.TokenIndex, space: Space, quote: QuoteBehavior) Error!void {
    const tree = r.tree;
    assert(tree.tokenTag(token_index) == .identifier);
    const lexeme = tokenSliceForRender(tree, token_index);

    if (r.fixups.rename_identifiers.get(lexeme)) |mangled| {
        try r.ais.writeAll(mangled);
        try renderSpace(r, token_index, lexeme.len, space);
        return;
    }

    if (lexeme[0] != '@') {
        return renderToken(r, token_index, space);
    }

    assert(lexeme.len >= 3);
    assert(lexeme[0] == '@');
    assert(lexeme[1] == '\"');
    assert(lexeme[lexeme.len - 1] == '\"');
    const contents = lexeme[2 .. lexeme.len - 1]; // inside the @"" quotation

    // Empty name can't be unquoted.
    if (contents.len == 0) {
        return renderQuotedIdentifier(r, token_index, space, false);
    }

    // Special case for _.
    if (std.zig.isUnderscore(contents)) switch (quote) {
        .eagerly_unquote => return renderQuotedIdentifier(r, token_index, space, true),
        .eagerly_unquote_except_underscore,
        .preserve_when_shadowing,
        => return renderQuotedIdentifier(r, token_index, space, false),
    };

    // Scan the entire name for characters that would (after un-escaping) be illegal in a symbol,
    // i.e. contents don't match: [A-Za-z_][A-Za-z0-9_]*
    var contents_i: usize = 0;
    while (contents_i < contents.len) {
        switch (contents[contents_i]) {
            '0'...'9' => if (contents_i == 0) return renderQuotedIdentifier(r, token_index, space, false),
            'A'...'Z', 'a'...'z', '_' => {},
            '\\' => {
                var esc_offset = contents_i;
                const res = std.zig.string_literal.parseEscapeSequence(contents, &esc_offset);
                switch (res) {
                    .success => |char| switch (char) {
                        '0'...'9' => if (contents_i == 0) return renderQuotedIdentifier(r, token_index, space, false),
                        'A'...'Z', 'a'...'z', '_' => {},
                        else => return renderQuotedIdentifier(r, token_index, space, false),
                    },
                    .failure => return renderQuotedIdentifier(r, token_index, space, false),
                }
                contents_i = esc_offset;
                continue;
            },
            else => return renderQuotedIdentifier(r, token_index, space, false),
        }
        contents_i += 1;
    }

    // Read enough of the name (while un-escaping) to determine if it's a keyword or primitive.
    // If it's too long to fit in this buffer, we know it's neither and quoting is unnecessary.
    // If we read the whole thing, we have to do further checks.
    const longest_keyword_or_primitive_len = comptime blk: {
        var longest = 0;
        for (primitives.names.keys()) |key| {
            if (key.len > longest) longest = key.len;
        }
        for (std.zig.Token.keywords.keys()) |key| {
            if (key.len > longest) longest = key.len;
        }
        break :blk longest;
    };
    var buf: [longest_keyword_or_primitive_len]u8 = undefined;

    contents_i = 0;
    var buf_i: usize = 0;
    while (contents_i < contents.len and buf_i < longest_keyword_or_primitive_len) {
        if (contents[contents_i] == '\\') {
            const res = std.zig.string_literal.parseEscapeSequence(contents, &contents_i).success;
            buf[buf_i] = @as(u8, @intCast(res));
            buf_i += 1;
        } else {
            buf[buf_i] = contents[contents_i];
            contents_i += 1;
            buf_i += 1;
        }
    }

    // We read the whole thing, so it could be a keyword or primitive.
    if (contents_i == contents.len) {
        if (!std.zig.isValidId(buf[0..buf_i])) {
            return renderQuotedIdentifier(r, token_index, space, false);
        }
        if (primitives.isPrimitive(buf[0..buf_i])) switch (quote) {
            .eagerly_unquote,
            .eagerly_unquote_except_underscore,
            => return renderQuotedIdentifier(r, token_index, space, true),
            .preserve_when_shadowing => return renderQuotedIdentifier(r, token_index, space, false),
        };
    }

    try renderQuotedIdentifier(r, token_index, space, true);
}

// Renders a @"" quoted identifier, normalizing escapes.
// Unnecessary escapes are un-escaped, and \u escapes are normalized to \x when they fit.
// If unquote is true, the @"" is removed and the result is a bare symbol whose validity is asserted.
fn renderQuotedIdentifier(r: *Render, token_index: Ast.TokenIndex, space: Space, comptime unquote: bool) !void {
    const tree = r.tree;
    const ais = r.ais;
    assert(tree.tokenTag(token_index) == .identifier);
    const lexeme = tokenSliceForRender(tree, token_index);
    assert(lexeme.len >= 3 and lexeme[0] == '@');

    if (!unquote) try ais.writeAll("@\"");
    const contents = lexeme[2 .. lexeme.len - 1];
    try renderIdentifierContents(ais, contents);
    if (!unquote) try ais.writeByte('\"');

    try renderSpace(r, token_index, lexeme.len, space);
}

fn renderIdentifierContents(ais: *AutoIndentingStream, bytes: []const u8) !void {
    var pos: usize = 0;
    while (pos < bytes.len) {
        const byte = bytes[pos];
        switch (byte) {
            '\\' => {
                const old_pos = pos;
                const res = std.zig.string_literal.parseEscapeSequence(bytes, &pos);
                const escape_sequence = bytes[old_pos..pos];
                switch (res) {
                    .success => |codepoint| {
                        if (codepoint <= 0x7f) {
                            const buf = [1]u8{@as(u8, @intCast(codepoint))};
                            try ais.print("{f}", .{std.zig.fmtString(&buf)});
                        } else {
                            try ais.writeAll(escape_sequence);
                        }
                    },
                    .failure => {
                        // Escape the stray backslash
                        // This also avoids cases like "\x3\x39" becoming "\x39"
                        try ais.writeByte('\\');
                        try ais.writeAll(escape_sequence);
                    },
                }
            },
            0x00...('\\' - 1), ('\\' + 1)...0x7f => {
                const buf = [1]u8{byte};
                try ais.print("{f}", .{std.zig.fmtString(&buf)});
                pos += 1;
            },
            0x80...0xff => {
                try ais.writeByte(byte);
                pos += 1;
            },
        }
    }
}

/// Returns true if there exists a line comment between any of the tokens from
/// `start_token` to `end_token`. This is used to determine if e.g. a
/// fn_proto should be wrapped and have a trailing comma inserted even if
/// there is none in the source.
fn hasComment(tree: Ast, start_token: Ast.TokenIndex, end_token: Ast.TokenIndex) bool {
    for (start_token..end_token) |i| {
        const token: Ast.TokenIndex = @intCast(i);
        const start = tree.tokenStart(token) + tree.tokenSlice(token).len;
        const end = tree.tokenStart(token + 1);
        if (mem.findScalar(u8, tree.source[start..end], '/') != null) return true;
    }

    return false;
}

/// Returns true if there exists a multiline string literal between the start
/// of token `start_token` and the start of token `end_token`.
fn hasMultilineString(tree: Ast, start_token: Ast.TokenIndex, end_token: Ast.TokenIndex) bool {
    return std.mem.findScalar(
        Token.Tag,
        tree.tokens.items(.tag)[start_token..end_token],
        .multiline_string_literal_line,
    ) != null;
}

/// Returns true if there exists a doc comment between the start
/// of token `start_token` and the start of token `end_token`.
fn hasDocComment(tree: Ast, start_token: Ast.TokenIndex, end_token: Ast.TokenIndex) bool {
    return std.mem.indexOfScalar(
        Token.Tag,
        tree.tokens.items(.tag)[start_token..end_token],
        .doc_comment,
    ) != null;
}

/// Assumes that start is the first byte past the previous token and
/// that end is the last byte before the next token.
fn renderComments(r: *Render, start: usize, end: usize) Error!bool {
    const tree = r.tree;
    const ais = r.ais;

    var index: usize = start;
    while (mem.find(u8, tree.source[index..end], "//")) |offset| {
        const comment_start = index + offset;

        // If there is no newline, the comment ends with EOF
        const newline_index = mem.findScalar(u8, tree.source[comment_start..end], '\n');
        const newline = if (newline_index) |i| comment_start + i else null;

        const untrimmed_comment = tree.source[comment_start .. newline orelse tree.source.len];
        const trimmed_comment = mem.trimEnd(u8, untrimmed_comment, &std.ascii.whitespace);

        // Don't leave any whitespace at the start of the file
        if (index != 0) {
            if (index == start and mem.containsAtLeast(u8, tree.source[index..comment_start], 2, "\n")) {
                // Leave up to one empty line before the first comment
                try ais.insertNewline();
                try ais.insertNewline();
            } else if (mem.findScalar(u8, tree.source[index..comment_start], '\n') != null) {
                // Respect the newline directly before the comment.
                // Note: This allows an empty line between comments
                try ais.insertNewline();
            } else if (index == start) {
                // Otherwise if the first comment is on the same line as
                // the token before it, prefix it with a single space.
                try ais.writeByte(' ');
            }
        }

        index = 1 + (newline orelse end - 1);

        const comment_content = mem.trimStart(u8, trimmed_comment["//".len..], &std.ascii.whitespace);
        if (ais.disabled_offset != null and mem.eql(u8, comment_content, "zig fmt: on")) {
            // Write the source for which formatting was disabled directly
            // to the underlying writer, fixing up invalid whitespace.
            const disabled_source = tree.source[ais.disabled_offset.?..comment_start];
            try writeFixingWhitespace(ais.underlying_writer, disabled_source);
            // Write with the canonical single space.
            try ais.underlying_writer.writeAll("// zig fmt: on\n");
            ais.disabled_offset = null;
            ais.resetLine();
        } else if (ais.disabled_offset == null and mem.eql(u8, comment_content, "zig fmt: off")) {
            // Write with the canonical single space.
            try ais.writeAll("// zig fmt: off\n");
            ais.disabled_offset = index;
        } else {
            // Write the comment minus trailing whitespace.
            try ais.print("{s}\n", .{trimmed_comment});
        }
    }

    if (index != start and mem.containsAtLeast(u8, tree.source[index - 1 .. end], 2, "\n")) {
        // Don't leave any whitespace at the end of the file
        if (end != tree.source.len) {
            try ais.insertNewline();
        }
    }

    return index != start;
}

fn renderExtraNewline(r: *Render, node: Ast.Node.Index) Error!void {
    return renderExtraNewlineToken(r, r.tree.firstToken(node));
}

/// Check if there is an empty line immediately before the given token. If so, render it.
fn renderExtraNewlineToken(r: *Render, token_index: Ast.TokenIndex) Error!void {
    const tree = r.tree;
    const ais = r.ais;
    const token_start = tree.tokenStart(token_index);
    if (token_start == 0) return;
    const prev_token_end = if (token_index == 0)
        0
    else
        tree.tokenStart(token_index - 1) + tokenSliceForRender(tree, token_index - 1).len;

    // If there is a immediately preceding comment or doc_comment,
    // skip it because required extra newline has already been rendered.
    if (mem.find(u8, tree.source[prev_token_end..token_start], "//") != null) return;
    if (tree.isTokenPrecededByTags(token_index, &.{.doc_comment})) return;

    // Iterate backwards to the end of the previous token, stopping if a
    // non-whitespace character is encountered or two newlines have been found.
    var i = token_start - 1;
    var newlines: u2 = 0;
    while (std.ascii.isWhitespace(tree.source[i])) : (i -= 1) {
        if (tree.source[i] == '\n') newlines += 1;
        if (newlines == 2) return ais.insertNewline();
        if (i == prev_token_end) break;
    }
}

/// end_token is the token one past the last doc comment token. This function
/// searches backwards from there.
fn renderDocComments(r: *Render, end_token: Ast.TokenIndex) Error!void {
    const tree = r.tree;
    // Search backwards for the first doc comment.
    if (end_token == 0) return;
    var tok = end_token - 1;
    while (tree.tokenTag(tok) == .doc_comment) {
        if (tok == 0) break;
        tok -= 1;
    } else {
        tok += 1;
    }
    const first_tok = tok;
    if (first_tok == end_token) return;

    if (first_tok != 0) {
        const prev_token_tag = tree.tokenTag(first_tok - 1);

        // Prevent accidental use of `renderDocComments` for a function argument doc comment
        assert(prev_token_tag != .l_paren);

        if (prev_token_tag != .l_brace) {
            try renderExtraNewlineToken(r, first_tok);
        }
    }

    while (tree.tokenTag(tok) == .doc_comment) : (tok += 1) {
        try renderToken(r, tok, .newline);
    }
}

/// start_token is first container doc comment token.
fn renderContainerDocComments(r: *Render, start_token: Ast.TokenIndex) Error!void {
    const tree = r.tree;
    var tok = start_token;
    while (tree.tokenTag(tok) == .container_doc_comment) : (tok += 1) {
        try renderToken(r, tok, .newline);
    }
    // Render extra newline if there is one between final container doc comment and
    // the next token. If the next token is a doc comment, that code path
    // will have its own logic to insert a newline.
    if (tree.tokenTag(tok) != .doc_comment) {
        try renderExtraNewlineToken(r, tok);
    }
}

fn discardAllParams(r: *Render, fn_proto_node: Ast.Node.Index) Error!void {
    const tree = &r.tree;
    const ais = r.ais;
    var buf: [1]Ast.Node.Index = undefined;
    const fn_proto = tree.fullFnProto(&buf, fn_proto_node).?;
    var it = fn_proto.iterate(tree);
    while (it.next()) |param| {
        const name_ident = param.name_token.?;
        assert(tree.tokenTag(name_ident) == .identifier);
        try ais.writeAll("_ = ");
        try ais.writeAll(tokenSliceForRender(r.tree, name_ident));
        try ais.writeAll(";\n");
    }
}

fn tokenSliceForRender(tree: Ast, token_index: Ast.TokenIndex) []const u8 {
    var ret = tree.tokenSlice(token_index);
    switch (tree.tokenTag(token_index)) {
        .container_doc_comment, .doc_comment => {
            ret = mem.trimEnd(u8, ret, &std.ascii.whitespace);
        },
        else => {},
    }
    return ret;
}

fn hasTrailingComment(tree: Ast, t: Ast.TokenIndex) bool {
    const start = tree.tokenStart(t) + tree.tokenSlice(t).len;
    const between = tree.source[start..tree.tokenStart(t + 1)];
    for (between) |byte| switch (byte) {
        '\n' => return false,
        '/' => return true,
        else => continue,
    };
    return false;
}

/// Returns `true` if and only if there are any tokens or line comments between
/// start_token and end_token.
fn anythingBetween(tree: Ast, start_token: Ast.TokenIndex, end_token: Ast.TokenIndex) bool {
    if (start_token + 1 != end_token) return true;
    return hasComment(tree, start_token, end_token);
}

fn writeFixingWhitespace(w: *Writer, slice: []const u8) Error!void {
    for (slice) |byte| switch (byte) {
        '\t' => try w.splatByteAll(' ', indent_delta),
        '\r' => {},
        else => try w.writeByte(byte),
    };
}

fn nodeIsBlock(tag: Ast.Node.Tag) bool {
    return switch (tag) {
        .block,
        .block_semicolon,
        .block_two,
        .block_two_semicolon,
        => true,
        else => false,
    };
}

fn nodeIsIfForWhileSwitch(tag: Ast.Node.Tag) bool {
    return switch (tag) {
        .@"if",
        .if_simple,
        .@"for",
        .for_simple,
        .@"while",
        .while_simple,
        .while_cont,
        .@"switch",
        .switch_comma,
        => true,
        else => false,
    };
}

fn nodeCausesSliceOpSpace(tag: Ast.Node.Tag) bool {
    return switch (tag) {
        .@"catch",
        .add,
        .add_wrap,
        .array_cat,
        .array_mult,
        .assign,
        .assign_bit_and,
        .assign_bit_or,
        .assign_shl,
        .assign_shr,
        .assign_bit_xor,
        .assign_div,
        .assign_sub,
        .assign_sub_wrap,
        .assign_mod,
        .assign_add,
        .assign_add_wrap,
        .assign_mul,
        .assign_mul_wrap,
        .bang_equal,
        .bit_and,
        .bit_or,
        .shl,
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
        .sub,
        .sub_wrap,
        .@"orelse",
        => true,

        else => false,
    };
}

/// Automatically inserts indentation of written data by keeping
/// track of the current indentation level
///
/// We introduce a new indentation scope with pushIndent/popIndent whenever
/// we potentially want to introduce an indent after the next newline.
///
/// Indentation should only ever increment by one from one line to the next,
/// no matter how many new indentation scopes are introduced. This is done by
/// only realizing the indentation from the most recent scope. As an example:
///
///         while (foo) if (bar)
///             f(x);
///
/// The body of `while` introduces a new indentation scope and the body of
/// `if` also introduces a new indentation scope. When the newline is seen,
/// only the indentation scope of the `if` is realized, and the `while` is
/// not.
///
/// As comments are rendered during space rendering, we need to keep track
/// of the appropriate indentation level for them with pushSpace/popSpace.
/// This should be done whenever a scope that ends in a .semicolon or a
/// .comma is introduced.
const AutoIndentingStream = struct {
    underlying_writer: *Writer,

    /// Offset into the source at which formatting has been disabled with
    /// a `zig fmt: off` comment.
    ///
    /// If non-null, the AutoIndentingStream will not write any bytes
    /// to the underlying writer. It will however continue to track the
    /// indentation level.
    disabled_offset: ?usize = null,

    indent_count: usize = 0,
    indent_delta: usize,
    indent_stack: std.array_list.Managed(StackElem),
    space_stack: std.array_list.Managed(SpaceElem),
    space_mode: ?usize = null,
    disable_indent_committing: usize = 0,
    current_line_empty: bool = true,
    /// the most recently applied indent
    applied_indent: usize = 0,

    pub const IndentType = enum {
        normal,
        after_equals,
        binop,
        field_access,
    };
    const StackElem = struct {
        indent_type: IndentType,
        realized: bool,
    };
    const SpaceElem = struct {
        space: Space,
        indent_count: usize,
    };

    pub fn init(gpa: Allocator, w: *Writer, starting_indent_delta: usize) AutoIndentingStream {
        return .{
            .underlying_writer = w,
            .indent_delta = starting_indent_delta,
            .indent_stack = .init(gpa),
            .space_stack = .init(gpa),
        };
    }

    pub fn deinit(self: *AutoIndentingStream) void {
        self.indent_stack.deinit();
        self.space_stack.deinit();
    }

    pub fn writeAll(ais: *AutoIndentingStream, bytes: []const u8) Error!void {
        if (bytes.len == 0) return;
        try ais.applyIndent();
        if (ais.disabled_offset == null) try ais.underlying_writer.writeAll(bytes);
        if (bytes[bytes.len - 1] == '\n') ais.resetLine();
    }

    /// Assumes that if the printed data ends with a newline, it is directly
    /// contained in the format string.
    pub fn print(ais: *AutoIndentingStream, comptime format: []const u8, args: anytype) Error!void {
        try ais.applyIndent();
        if (ais.disabled_offset == null) try ais.underlying_writer.print(format, args);
        if (format[format.len - 1] == '\n') ais.resetLine();
    }

    pub fn writeByte(ais: *AutoIndentingStream, byte: u8) Error!void {
        try ais.applyIndent();
        if (ais.disabled_offset == null) try ais.underlying_writer.writeByte(byte);
        assert(byte != '\n');
    }

    pub fn splatByteAll(ais: *AutoIndentingStream, byte: u8, n: usize) Error!void {
        assert(byte != '\n');
        try ais.applyIndent();
        if (ais.disabled_offset == null) try ais.underlying_writer.splatByteAll(byte, n);
    }

    // Change the indent delta without changing the final indentation level
    pub fn setIndentDelta(ais: *AutoIndentingStream, new_indent_delta: usize) void {
        if (ais.indent_delta == new_indent_delta) {
            return;
        } else if (ais.indent_delta > new_indent_delta) {
            assert(ais.indent_delta % new_indent_delta == 0);
            ais.indent_count = ais.indent_count * (ais.indent_delta / new_indent_delta);
        } else {
            // assert that the current indentation (in spaces) in a multiple of the new delta
            assert((ais.indent_count * ais.indent_delta) % new_indent_delta == 0);
            ais.indent_count = ais.indent_count / (new_indent_delta / ais.indent_delta);
        }
        ais.indent_delta = new_indent_delta;
    }

    pub fn insertNewline(ais: *AutoIndentingStream) Error!void {
        if (ais.disabled_offset == null) try ais.underlying_writer.writeByte('\n');
        ais.resetLine();
    }

    /// Insert a newline unless the current line is blank
    pub fn maybeInsertNewline(ais: *AutoIndentingStream) Error!void {
        if (!ais.current_line_empty)
            try ais.insertNewline();
    }

    /// Checks to see if the most recent indentation exceeds the currently pushed indents
    pub fn isLineOverIndented(ais: *AutoIndentingStream) bool {
        if (ais.current_line_empty) return false;
        return ais.applied_indent > ais.currentIndent();
    }

    fn resetLine(ais: *AutoIndentingStream) void {
        ais.current_line_empty = true;

        if (ais.disable_indent_committing > 0) return;

        if (ais.indent_stack.items.len > 0) {
            // By default, we realize the most recent indentation scope.
            var to_realize = ais.indent_stack.items.len - 1;

            if (ais.indent_stack.items.len >= 2 and
                ais.indent_stack.items[to_realize - 1].indent_type == .after_equals and
                ais.indent_stack.items[to_realize - 1].realized and
                ais.indent_stack.items[to_realize].indent_type == .binop)
            {
                // If we are in a .binop scope and our direct parent is .after_equals, don't indent.
                // This ensures correct indentation in the below example:
                //
                //        const foo =
                //            (x >= 'a' and x <= 'z') or         //<-- we are here
                //            (x >= 'A' and x <= 'Z');
                //
                return;
            }

            if (ais.indent_stack.items[to_realize].indent_type == .field_access) {
                // Only realize the top-most field_access in a chain.
                while (to_realize > 0 and ais.indent_stack.items[to_realize - 1].indent_type == .field_access)
                    to_realize -= 1;
            }

            if (ais.indent_stack.items[to_realize].realized) return;
            ais.indent_stack.items[to_realize].realized = true;
            ais.indent_count += 1;
        }
    }

    /// Disables indentation level changes during the next newlines until re-enabled.
    pub fn disableIndentCommitting(ais: *AutoIndentingStream) void {
        ais.disable_indent_committing += 1;
    }

    pub fn enableIndentCommitting(ais: *AutoIndentingStream) void {
        assert(ais.disable_indent_committing > 0);
        ais.disable_indent_committing -= 1;
    }

    pub fn pushSpace(ais: *AutoIndentingStream, space: Space) !void {
        try ais.space_stack.append(.{ .space = space, .indent_count = ais.indent_count });
    }

    pub fn popSpace(ais: *AutoIndentingStream) void {
        _ = ais.space_stack.pop();
    }

    /// Sets current indentation level to be the same as that of the last pushSpace.
    pub fn enableSpaceMode(ais: *AutoIndentingStream, space: Space) void {
        if (ais.space_stack.items.len == 0) return;
        const curr = ais.space_stack.getLast();
        if (curr.space != space) return;
        ais.space_mode = curr.indent_count;
    }

    pub fn disableSpaceMode(ais: *AutoIndentingStream) void {
        ais.space_mode = null;
    }

    pub fn lastSpaceModeIndent(ais: *AutoIndentingStream) usize {
        if (ais.space_stack.items.len == 0) return 0;
        return ais.space_stack.getLast().indent_count * ais.indent_delta;
    }

    /// Push default indentation
    /// Doesn't actually write any indentation.
    /// Just primes the stream to be able to write the correct indentation if it needs to.
    pub fn pushIndent(ais: *AutoIndentingStream, indent_type: IndentType) !void {
        try ais.indent_stack.append(.{ .indent_type = indent_type, .realized = false });
    }

    /// Forces an indentation level to be realized.
    pub fn forcePushIndent(ais: *AutoIndentingStream, indent_type: IndentType) !void {
        try ais.indent_stack.append(.{ .indent_type = indent_type, .realized = true });
        ais.indent_count += 1;
    }

    pub fn popIndent(ais: *AutoIndentingStream) void {
        if (ais.indent_stack.pop().?.realized) {
            ais.indent_count -= 1;
        }
    }

    /// Forces the last pushed indent to be realized
    pub fn forceLastIndent(ais: *AutoIndentingStream) void {
        const top = &ais.indent_stack.items[ais.indent_stack.items.len - 1];
        if (!top.realized) {
            top.realized = true;
            ais.indent_count += 1;
        }
    }

    pub fn indentStackEmpty(ais: *AutoIndentingStream) bool {
        return ais.indent_stack.items.len == 0;
    }

    /// Writes ' ' bytes if the current line is empty
    fn applyIndent(ais: *AutoIndentingStream) Error!void {
        const current_indent = ais.currentIndent();
        if (ais.current_line_empty) {
            if (current_indent > 0 and ais.disabled_offset == null) {
                try ais.underlying_writer.splatByteAll(' ', current_indent);
            }
            ais.applied_indent = current_indent;
        }
        ais.current_line_empty = false;
    }

    fn currentIndent(ais: *AutoIndentingStream) usize {
        const indent_count = ais.space_mode orelse ais.indent_count;
        return indent_count * ais.indent_delta;
    }
};



---
File: /std/zig/c_translation/builtins.zig
---

const std = @import("std");

/// Standard C Library bug: The absolute value of the most negative integer remains negative.
pub inline fn abs(val: c_int) c_int {
    return if (val == std.math.minInt(c_int)) val else @intCast(@abs(val));
}

pub inline fn assume(cond: bool) void {
    if (!cond) unreachable;
}

pub inline fn bswap16(val: u16) u16 {
    return @byteSwap(val);
}

pub inline fn bswap32(val: u32) u32 {
    return @byteSwap(val);
}

pub inline fn bswap64(val: u64) u64 {
    return @byteSwap(val);
}

pub inline fn ceilf(val: f32) f32 {
    return @ceil(val);
}

pub inline fn ceil(val: f64) f64 {
    return @ceil(val);
}

/// Returns the number of leading 0-bits in x, starting at the most significant bit position.
/// In C if `val` is 0, the result is undefined; in zig it's the number of bits in a c_uint
pub inline fn clz(val: c_uint) c_int {
    @setRuntimeSafety(false);
    return @as(c_int, @bitCast(@as(c_uint, @clz(val))));
}

pub inline fn constant_p(expr: anytype) c_int {
    _ = expr;
    return @intFromBool(false);
}

pub inline fn cosf(val: f32) f32 {
    return @cos(val);
}

pub inline fn cos(val: f64) f64 {
    return @cos(val);
}

/// Returns the number of trailing 0-bits in val, starting at the least significant bit position.
/// In C if `val` is 0, the result is undefined; in zig it's the number of bits in a c_uint
pub inline fn ctz(val: c_uint) c_int {
    @setRuntimeSafety(false);
    return @as(c_int, @bitCast(@as(c_uint, @ctz(val))));
}

pub inline fn exp2f(val: f32) f32 {
    return @exp2(val);
}

pub inline fn exp2(val: f64) f64 {
    return @exp2(val);
}

pub inline fn expf(val: f32) f32 {
    return @exp(val);
}

pub inline fn exp(val: f64) f64 {
    return @exp(val);
}

/// The return value of __builtin_expect is `expr`. `c` is the expected value
/// of `expr` and is used as a hint to the compiler in C. Here it is unused.
pub inline fn expect(expr: c_long, c: c_long) c_long {
    _ = c;
    return expr;
}

pub inline fn fabsf(val: f32) f32 {
    return @abs(val);
}

pub inline fn fabs(val: f64) f64 {
    return @abs(val);
}

pub inline fn floorf(val: f32) f32 {
    return @floor(val);
}

pub inline fn floor(val: f64) f64 {
    return @floor(val);
}

pub inline fn has_builtin(func: anytype) c_int {
    _ = func;
    return @intFromBool(true);
}

pub inline fn huge_valf() f32 {
    return std.math.inf(f32);
}

pub inline fn inff() f32 {
    return std.math.inf(f32);
}

/// Similar to isinf, except the return value is -1 for an argument of -Inf and 1 for an argument of +Inf.
pub inline fn isinf_sign(x: anytype) c_int {
    if (!std.math.isInf(x)) return 0;
    return if (std.math.isPositiveInf(x)) 1 else -1;
}

pub inline fn isinf(x: anytype) c_int {
    return @intFromBool(std.math.isInf(x));
}

pub inline fn isnan(x: anytype) c_int {
    return @intFromBool(std.math.isNan(x));
}

/// Standard C Library bug: The absolute value of the most negative integer remains negative.
pub inline fn labs(val: c_long) c_long {
    return if (val == std.math.minInt(c_long)) val else @intCast(@abs(val));
}

/// Standard C Library bug: The absolute value of the most negative integer remains negative.
pub inline fn llabs(val: c_longlong) c_longlong {
    return if (val == std.math.minInt(c_longlong)) val else @intCast(@abs(val));
}

pub inline fn log10f(val: f32) f32 {
    return @log10(val);
}

pub inline fn log10(val: f64) f64 {
    return @log10(val);
}

pub inline fn log2f(val: f32) f32 {
    return @log2(val);
}

pub inline fn log2(val: f64) f64 {
    return @log2(val);
}

pub inline fn logf(val: f32) f32 {
    return @log(val);
}

pub inline fn log(val: f64) f64 {
    return @log(val);
}

pub inline fn memcpy_chk(
    noalias dst: ?*anyopaque,
    noalias src: ?*const anyopaque,
    len: usize,
    remaining: usize,
) ?*anyopaque {
    if (len > remaining) @panic("__builtin___memcpy_chk called with len > remaining");
    if (len > 0) @memcpy(
        @as([*]u8, @ptrCast(dst.?))[0..len],
        @as([*]const u8, @ptrCast(src.?)),
    );
    return dst;
}

pub inline fn memcpy(
    noalias dst: ?*anyopaque,
    noalias src: ?*const anyopaque,
    len: usize,
) ?*anyopaque {
    if (len > 0) @memcpy(
        @as([*]u8, @ptrCast(dst.?))[0..len],
        @as([*]const u8, @ptrCast(src.?)),
    );
    return dst;
}

pub inline fn memset_chk(
    dst: ?*anyopaque,
    val: c_int,
    len: usize,
    remaining: usize,
) ?*anyopaque {
    if (len > remaining) @panic("__builtin___memset_chk called with len > remaining");
    const dst_cast = @as([*c]u8, @ptrCast(dst));
    @memset(dst_cast[0..len], @as(u8, @bitCast(@as(i8, @truncate(val)))));
    return dst;
}

pub inline fn memset(dst: ?*anyopaque, val: c_int, len: usize) ?*anyopaque {
    const dst_cast = @as([*c]u8, @ptrCast(dst));
    @memset(dst_cast[0..len], @as(u8, @bitCast(@as(i8, @truncate(val)))));
    return dst;
}

pub fn mul_overflow(a: anytype, b: anytype, result: *@TypeOf(a, b)) c_int {
    const res = @mulWithOverflow(a, b);
    result.* = res[0];
    return res[1];
}

/// returns a quiet NaN. Quiet NaNs have many representations; tagp is used to select one in an
/// implementation-defined way.
/// This implementation is based on the description for nan provided in the GCC docs at
/// https://gcc.gnu.org/onlinedocs/gcc/Other-Builtins.html#index-_005f_005fbuiltin_005fnan
/// Comment is reproduced below:
/// Since ISO C99 defines this function in terms of strtod, which we do not implement, a description
/// of the parsing is in order.
/// The string is parsed as by strtol; that is, the base is recognized by leading ‘0’ or ‘0x’ prefixes.
/// The number parsed is placed in the significand such that the least significant bit of the number is
///    at the least significant bit of the significand.
/// The number is truncated to fit the significand field provided.
/// The significand is forced to be a quiet NaN.
///
/// If tagp contains any non-numeric characters, the function returns a NaN whose significand is zero.
/// If tagp is empty, the function returns a NaN whose significand is zero.
pub inline fn nanf(tagp: []const u8) f32 {
    const parsed = std.fmt.parseUnsigned(c_ulong, tagp, 0) catch 0;
    const bits: u23 = @truncate(parsed); // single-precision float trailing significand is 23 bits
    return @bitCast(@as(u32, bits) | @as(u32, @bitCast(std.math.nan(f32))));
}

pub inline fn object_size(ptr: ?*const anyopaque, ty: c_int) usize {
    _ = ptr;
    // clang semantics match gcc's: https://gcc.gnu.org/onlinedocs/gcc/Object-Size-Checking.html
    // If it is not possible to determine which objects ptr points to at compile time,
    // object_size should return (size_t) -1 for type 0 or 1 and (size_t) 0
    // for type 2 or 3.
    if (ty == 0 or ty == 1) return @as(usize, @bitCast(-@as(isize, 1)));
    if (ty == 2 or ty == 3) return 0;
    unreachable;
}

/// popcount of a c_uint will never exceed the capacity of a c_int
pub inline fn popcount(val: c_uint) c_int {
    @setRuntimeSafety(false);
    return @as(c_int, @bitCast(@as(c_uint, @popCount(val))));
}

pub inline fn roundf(val: f32) f32 {
    return @round(val);
}

pub inline fn round(val: f64) f64 {
    return @round(val);
}

pub inline fn signbitf(val: f32) c_int {
    return @intFromBool(std.math.signbit(val));
}

pub inline fn signbit(val: f64) c_int {
    return @intFromBool(std.math.signbit(val));
}

pub inline fn sinf(val: f32) f32 {
    return @sin(val);
}

pub inline fn sin(val: f64) f64 {
    return @sin(val);
}

pub inline fn sqrtf(val: f32) f32 {
    return @sqrt(val);
}

pub inline fn sqrt(val: f64) f64 {
    return @sqrt(val);
}

pub inline fn strcmp(s1: [*c]const u8, s2: [*c]const u8) c_int {
    return switch (std.mem.orderZ(u8, s1, s2)) {
        .lt => -1,
        .eq => 0,
        .gt => 1,
    };
}

pub inline fn strlen(s: [*c]const u8) usize {
    return std.mem.sliceTo(s, 0).len;
}

pub inline fn truncf(val: f32) f32 {
    return @trunc(val);
}

pub inline fn trunc(val: f64) f64 {
    return @trunc(val);
}

pub inline fn @"unreachable"() noreturn {
    unreachable;
}



---
File: /std/zig/c_translation/helpers.zig
---

const std = @import("std");

/// "Usual arithmetic conversions" from C11 standard 6.3.1.8
pub fn ArithmeticConversion(comptime A: type, comptime B: type) type {
    if (A == c_longdouble or B == c_longdouble) return c_longdouble;
    if (A == f80 or B == f80) return f80;
    if (A == f64 or B == f64) return f64;
    if (A == f32 or B == f32) return f32;

    const A_Promoted = PromotedIntType(A);
    const B_Promoted = PromotedIntType(B);
    comptime {
        std.debug.assert(integerRank(A_Promoted) >= integerRank(c_int));
        std.debug.assert(integerRank(B_Promoted) >= integerRank(c_int));
    }

    if (A_Promoted == B_Promoted) return A_Promoted;

    const a_signed = @typeInfo(A_Promoted).int.signedness == .signed;
    const b_signed = @typeInfo(B_Promoted).int.signedness == .signed;

    if (a_signed == b_signed) {
        return if (integerRank(A_Promoted) > integerRank(B_Promoted)) A_Promoted else B_Promoted;
    }

    const SignedType = if (a_signed) A_Promoted else B_Promoted;
    const UnsignedType = if (!a_signed) A_Promoted else B_Promoted;

    if (integerRank(UnsignedType) >= integerRank(SignedType)) return UnsignedType;

    if (std.math.maxInt(SignedType) >= std.math.maxInt(UnsignedType)) return SignedType;

    return ToUnsigned(SignedType);
}

/// Integer promotion described in C11 6.3.1.1.2
fn PromotedIntType(comptime T: type) type {
    return switch (T) {
        bool, c_short => c_int,
        c_ushort => if (@sizeOf(c_ushort) == @sizeOf(c_int)) c_uint else c_int,
        c_int, c_uint, c_long, c_ulong, c_longlong, c_ulonglong => T,
        else => switch (@typeInfo(T)) {
            .comptime_int => @compileError("Cannot promote `" ++ @typeName(T) ++ "`; a fixed-size number type is required"),
            // promote to c_int if it can represent all values of T
            .int => |int_info| if (int_info.bits < @bitSizeOf(c_int))
                c_int
                // otherwise, restore the original C type
            else if (int_info.bits == @bitSizeOf(c_int))
                if (int_info.signedness == .unsigned) c_uint else c_int
            else if (int_info.bits <= @bitSizeOf(c_long))
                if (int_info.signedness == .unsigned) c_ulong else c_long
            else if (int_info.bits <= @bitSizeOf(c_longlong))
                if (int_info.signedness == .unsigned) c_ulonglong else c_longlong
            else
                @compileError("Cannot promote `" ++ @typeName(T) ++ "`; a C ABI type is required"),
            else => @compileError("Attempted to promote invalid type `" ++ @typeName(T) ++ "`"),
        },
    };
}

/// C11 6.3.1.1.1
fn integerRank(comptime T: type) u8 {
    return switch (T) {
        bool => 0,
        u8, i8 => 1,
        c_short, c_ushort => 2,
        c_int, c_uint => 3,
        c_long, c_ulong => 4,
        c_longlong, c_ulonglong => 5,
        else => @compileError("integer rank not supported for `" ++ @typeName(T) ++ "`"),
    };
}

fn ToUnsigned(comptime T: type) type {
    return switch (T) {
        c_int => c_uint,
        c_long => c_ulong,
        c_longlong => c_ulonglong,
        else => @compileError("Cannot convert `" ++ @typeName(T) ++ "` to unsigned"),
    };
}

/// Constructs a [*c] pointer with the const and volatile annotations
/// from SelfType for pointing to a C flexible array of ElementType.
pub fn FlexibleArrayType(comptime SelfType: type, comptime ElementType: type) type {
    switch (@typeInfo(SelfType)) {
        .pointer => |ptr| {
            return @Pointer(.c, .{
                .@"const" = ptr.is_const,
                .@"volatile" = ptr.is_volatile,
                .@"allowzero" = true,
                .@"addrspace" = .generic,
                .@"align" = null,
            }, ElementType, null);
        },
        else => |info| @compileError("Invalid self type \"" ++ @tagName(info) ++ "\" for flexible array getter: " ++ @typeName(SelfType)),
    }
}

/// Promote the type of an integer literal until it fits as C would.
pub fn promoteIntLiteral(
    comptime SuffixType: type,
    comptime number: comptime_int,
    comptime base: CIntLiteralBase,
) PromoteIntLiteralReturnType(SuffixType, number, base) {
    return number;
}

const CIntLiteralBase = enum { decimal, octal, hex };

fn PromoteIntLiteralReturnType(comptime SuffixType: type, comptime number: comptime_int, comptime base: CIntLiteralBase) type {
    const signed_decimal = [_]type{ c_int, c_long, c_longlong, c_ulonglong };
    const signed_oct_hex = [_]type{ c_int, c_uint, c_long, c_ulong, c_longlong, c_ulonglong };
    const unsigned = [_]type{ c_uint, c_ulong, c_ulonglong };

    const list: []const type = if (@typeInfo(SuffixType).int.signedness == .unsigned)
        &unsigned
    else if (base == .decimal)
        &signed_decimal
    else
        &signed_oct_hex;

    var pos = std.mem.findScalar(type, list, SuffixType).?;
    while (pos < list.len) : (pos += 1) {
        if (number >= std.math.minInt(list[pos]) and number <= std.math.maxInt(list[pos])) {
            return list[pos];
        }
    }

    @compileError("Integer literal is too large");
}

/// Convert from clang __builtin_shufflevector index to Zig @shuffle index
/// clang requires __builtin_shufflevector index arguments to be integer constants.
/// negative values for `this_index` indicate "don't care".
/// clang enforces that `this_index` is less than the total number of vector elements
/// See https://ziglang.org/documentation/master/#shuffle
/// See https://clang.llvm.org/docs/LanguageExtensions.html#langext-builtin-shufflevector
pub fn shuffleVectorIndex(comptime this_index: c_int, comptime source_vector_len: usize) i32 {
    const positive_index = std.math.cast(usize, this_index) orelse return undefined;
    if (positive_index < source_vector_len) return @as(i32, @intCast(this_index));
    const b_index = positive_index - source_vector_len;
    return ~@as(i32, @intCast(b_index));
}

/// C `%` operator for signed integers
/// C standard states: "If the quotient a/b is representable, the expression (a/b)*b + a%b shall equal a"
/// The quotient is not representable if denominator is zero, or if numerator is the minimum integer for
/// the type and denominator is -1. C has undefined behavior for those two cases; this function has safety
/// checked undefined behavior
pub fn signedRemainder(numerator: anytype, denominator: anytype) @TypeOf(numerator, denominator) {
    std.debug.assert(@typeInfo(@TypeOf(numerator, denominator)).int.signedness == .signed);
    if (denominator > 0) return @rem(numerator, denominator);
    return numerator - @divTrunc(numerator, denominator) * denominator;
}

/// Given a type and value, cast the value to the type as c would.
pub fn cast(comptime DestType: type, target: anytype) DestType {
    // this function should behave like transCCast in translate-c, except it's for macros
    const SourceType = @TypeOf(target);
    switch (@typeInfo(DestType)) {
        .@"fn" => return castToPtr(*const DestType, SourceType, target),
        .pointer => return castToPtr(DestType, SourceType, target),
        .optional => |dest_opt| {
            if (@typeInfo(dest_opt.child) == .pointer) {
                return castToPtr(DestType, SourceType, target);
            } else if (@typeInfo(dest_opt.child) == .@"fn") {
                return castToPtr(?*const dest_opt.child, SourceType, target);
            }
        },
        .int => {
            switch (@typeInfo(SourceType)) {
                .pointer => {
                    return castInt(DestType, @intFromPtr(target));
                },
                .optional => |opt| {
                    if (@typeInfo(opt.child) == .pointer) {
                        return castInt(DestType, @intFromPtr(target));
                    }
                },
                .int => {
                    return castInt(DestType, target);
                },
                .@"fn" => {
                    return castInt(DestType, @intFromPtr(&target));
                },
                .bool => {
                    return @intFromBool(target);
                },
                else => {},
            }
        },
        .float => {
            switch (@typeInfo(SourceType)) {
                .int => return @as(DestType, @floatFromInt(target)),
                .float => return @as(DestType, @floatCast(target)),
                .bool => return @as(DestType, @floatFromInt(@intFromBool(target))),
                else => {},
            }
        },
        .@"union" => |info| {
            inline for (info.fields) |field| {
                if (field.type == SourceType) return @unionInit(DestType, field.name, target);
            }

            @compileError("cast to union type '" ++ @typeName(DestType) ++ "' from type '" ++ @typeName(SourceType) ++ "' which is not present in union");
        },
        .bool => return cast(usize, target) != 0,
        else => {},
    }

    return @as(DestType, target);
}

fn castInt(comptime DestType: type, target: anytype) DestType {
    const dest = @typeInfo(DestType).int;
    const source = @typeInfo(@TypeOf(target)).int;

    const Int = @Int(source.signedness, dest.bits);

    if (dest.bits < source.bits)
        return @as(DestType, @bitCast(@as(Int, @truncate(target))))
    else
        return @as(DestType, @bitCast(@as(Int, target)));
}

fn castPtr(comptime DestType: type, target: anytype) DestType {
    return @ptrCast(@alignCast(@constCast(@volatileCast(target))));
}

fn castToPtr(comptime DestType: type, comptime SourceType: type, target: anytype) DestType {
    switch (@typeInfo(SourceType)) {
        .int => {
            return @as(DestType, @ptrFromInt(castInt(usize, target)));
        },
        .comptime_int => {
            if (target < 0)
                return @as(DestType, @ptrFromInt(@as(usize, @bitCast(@as(isize, @intCast(target))))))
            else
                return @as(DestType, @ptrFromInt(@as(usize, @intCast(target))));
        },
        .pointer => {
            return castPtr(DestType, target);
        },
        .@"fn" => {
            return castPtr(DestType, &target);
        },
        .optional => |target_opt| {
            if (@typeInfo(target_opt.child) == .pointer) {
                return castPtr(DestType, target);
            }
        },
        else => {},
    }

    return @as(DestType, target);
}

/// Given a value returns its size as C's sizeof operator would.
pub fn sizeof(target: anytype) usize {
    const T: type = if (@TypeOf(target) == type) target else @TypeOf(target);
    switch (@typeInfo(T)) {
        .float, .int, .@"struct", .@"union", .array, .bool, .vector => return @sizeOf(T),
        .@"fn" => {
            // sizeof(main) in C returns 1
            return 1;
        },
        .null => return @sizeOf(*anyopaque),
        .void => {
            // Note: sizeof(void) is 1 on clang/gcc and 0 on MSVC.
            return 1;
        },
        .@"opaque" => {
            if (T == anyopaque) {
                // Note: sizeof(void) is 1 on clang/gcc and 0 on MSVC.
                return 1;
            } else {
                @compileError("Cannot use C sizeof on opaque type " ++ @typeName(T));
            }
        },
        .optional => |opt| {
            if (@typeInfo(opt.child) == .pointer) {
                return sizeof(opt.child);
            } else {
                @compileError("Cannot use C sizeof on non-pointer optional " ++ @typeName(T));
            }
        },
        .pointer => |ptr| {
            if (ptr.size == .slice) {
                @compileError("Cannot use C sizeof on slice type " ++ @typeName(T));
            }

            // for strings, sizeof("a") returns 2.
            // normal pointer decay scenarios from C are handled
            // in the .array case above, but strings remain literals
            // and are therefore always pointers, so they need to be
            // specially handled here.
            if (ptr.size == .one and ptr.is_const and @typeInfo(ptr.child) == .array) {
                const array_info = @typeInfo(ptr.child).array;
                if ((array_info.child == u8 or array_info.child == u16) and array_info.sentinel() == 0) {
                    // length of the string plus one for the null terminator.
                    return (array_info.len + 1) * @sizeOf(array_info.child);
                }
            }

            // When zero sized pointers are removed, this case will no
            // longer be reachable and can be deleted.
            if (@sizeOf(T) == 0) {
                return @sizeOf(*anyopaque);
            }

            return @sizeOf(T);
        },
        .comptime_float => return @sizeOf(f64), // TODO c_double #3999
        .comptime_int => {
            // TODO to get the correct result we have to translate
            // `1073741824 * 4` as `int(1073741824) *% int(4)` since
            // sizeof(1073741824 * 4) != sizeof(4294967296).

            // TODO test if target fits in int, long or long long
            return @sizeOf(c_int);
        },
        else => @compileError("__helpers.sizeof does not support type " ++ @typeName(T)),
    }
}

pub fn div(a: anytype, b: anytype) ArithmeticConversion(@TypeOf(a), @TypeOf(b)) {
    const ResType = ArithmeticConversion(@TypeOf(a), @TypeOf(b));
    const a_casted = cast(ResType, a);
    const b_casted = cast(ResType, b);
    switch (@typeInfo(ResType)) {
        .float => return a_casted / b_casted,
        .int => return @divTrunc(a_casted, b_casted),
        else => unreachable,
    }
}

pub fn rem(a: anytype, b: anytype) ArithmeticConversion(@TypeOf(a), @TypeOf(b)) {
    const ResType = ArithmeticConversion(@TypeOf(a), @TypeOf(b));
    const a_casted = cast(ResType, a);
    const b_casted = cast(ResType, b);
    switch (@typeInfo(ResType)) {
        .int => {
            if (@typeInfo(ResType).int.signedness == .signed) {
                return signedRemainder(a_casted, b_casted);
            } else {
                return a_casted % b_casted;
            }
        },
        else => unreachable,
    }
}

/// A 2-argument function-like macro defined as #define FOO(A, B) (A)(B)
/// could be either: cast B to A, or call A with the value B.
pub fn CAST_OR_CALL(a: anytype, b: anytype) switch (@typeInfo(@TypeOf(a))) {
    .type => a,
    .@"fn" => |fn_info| fn_info.return_type orelse void,
    else => |info| @compileError("Unexpected argument type: " ++ @tagName(info)),
} {
    switch (@typeInfo(@TypeOf(a))) {
        .type => return cast(a, b),
        .@"fn" => return a(b),
        else => unreachable, // return type will be a compile error otherwise
    }
}

pub inline fn DISCARD(x: anytype) void {
    _ = x;
}

pub fn F_SUFFIX(comptime f: comptime_float) f32 {
    return @as(f32, f);
}

fn L_SUFFIX_ReturnType(comptime number: anytype) type {
    switch (@typeInfo(@TypeOf(number))) {
        .int, .comptime_int => return @TypeOf(promoteIntLiteral(c_long, number, .decimal)),
        .float, .comptime_float => return c_longdouble,
        else => @compileError("Invalid value for L suffix"),
    }
}

pub fn L_SUFFIX(comptime number: anytype) L_SUFFIX_ReturnType(number) {
    switch (@typeInfo(@TypeOf(number))) {
        .int, .comptime_int => return promoteIntLiteral(c_long, number, .decimal),
        .float, .comptime_float => @compileError("TODO: c_longdouble initialization from comptime_float not supported"),
        else => @compileError("Invalid value for L suffix"),
    }
}

pub fn LL_SUFFIX(comptime n: comptime_int) @TypeOf(promoteIntLiteral(c_longlong, n, .decimal)) {
    return promoteIntLiteral(c_longlong, n, .decimal);
}

pub fn U_SUFFIX(comptime n: comptime_int) @TypeOf(promoteIntLiteral(c_uint, n, .decimal)) {
    return promoteIntLiteral(c_uint, n, .decimal);
}

pub fn UL_SUFFIX(comptime n: comptime_int) @TypeOf(promoteIntLiteral(c_ulong, n, .decimal)) {
    return promoteIntLiteral(c_ulong, n, .decimal);
}

pub fn ULL_SUFFIX(comptime n: comptime_int) @TypeOf(promoteIntLiteral(c_ulonglong, n, .decimal)) {
    return promoteIntLiteral(c_ulonglong, n, .decimal);
}

pub fn WL_CONTAINER_OF(ptr: anytype, sample: anytype, comptime member: []const u8) @TypeOf(sample) {
    return @fieldParentPtr(member, ptr);
}



---
File: /std/zig/llvm/bitcode_writer.zig
---

const std = @import("../../std.zig");

pub const AbbrevOp = union(enum) {
    literal: u32, // 0
    fixed: u16, // 1
    fixed_runtime: type, // 1
    vbr: u16, // 2
    char6: void, // 4
    blob: void, // 5
    array_fixed: u16, // 3, 1
    array_fixed_runtime: type, // 3, 1
    array_vbr: u16, // 3, 2
    array_char6: void, // 3, 4
};

pub const Error = error{OutOfMemory};

pub fn BitcodeWriter(comptime types: []const type) type {
    return struct {
        const BcWriter = @This();

        buffer: std.array_list.Managed(u32),
        bit_buffer: u32 = 0,
        bit_count: u5 = 0,

        widths: [types.len]u16,

        pub fn getTypeWidth(self: BcWriter, comptime Type: type) u16 {
            return self.widths[comptime std.mem.findScalar(type, types, Type).?];
        }

        pub fn init(allocator: std.mem.Allocator, widths: [types.len]u16) BcWriter {
            return .{
                .buffer = std.array_list.Managed(u32).init(allocator),
                .widths = widths,
            };
        }

        pub fn deinit(self: BcWriter) void {
            self.buffer.deinit();
        }

        pub fn toOwnedSlice(self: *BcWriter) Error![]const u32 {
            std.debug.assert(self.bit_count == 0);
            return self.buffer.toOwnedSlice();
        }

        pub fn length(self: BcWriter) usize {
            std.debug.assert(self.bit_count == 0);
            return self.buffer.items.len;
        }

        pub fn writeBits(self: *BcWriter, value: anytype, bits: u16) Error!void {
            if (bits == 0) return;

            var in_buffer = bufValue(value, 32);
            var in_bits = bits;

            // Store input bits in buffer if they fit otherwise store as many as possible and flush
            if (self.bit_count > 0) {
                const bits_remaining = 31 - self.bit_count + 1;
                const n: u5 = @intCast(@min(bits_remaining, in_bits));
                const v = @as(u32, @truncate(in_buffer)) << self.bit_count;
                self.bit_buffer |= v;
                in_buffer >>= n;

                self.bit_count +%= n;
                in_bits -= n;

                if (self.bit_count != 0) return;
                try self.buffer.append(std.mem.nativeToLittle(u32, self.bit_buffer));
                self.bit_buffer = 0;
            }

            // Write 32-bit chunks of input bits
            while (in_bits >= 32) {
                try self.buffer.append(std.mem.nativeToLittle(u32, @truncate(in_buffer)));

                in_buffer >>= 31;
                in_buffer >>= 1;
                in_bits -= 32;
            }

            // Store remaining input bits in buffer
            if (in_bits > 0) {
                self.bit_count = @intCast(in_bits);
                self.bit_buffer = @truncate(in_buffer);
            }
        }

        pub fn writeVbr(self: *BcWriter, value: anytype, comptime vbr_bits: usize) Error!void {
            comptime {
                std.debug.assert(vbr_bits > 1);
                if (@bitSizeOf(@TypeOf(value)) > 64) @compileError("Unsupported VBR block type: " ++ @typeName(@TypeOf(value)));
            }

            var in_buffer = bufValue(value, vbr_bits);

            const continue_bit = @as(@TypeOf(in_buffer), 1) << @intCast(vbr_bits - 1);
            const mask = continue_bit - 1;

            // If input is larger than one VBR block can store
            // then store vbr_bits - 1 bits and a continue bit
            while (in_buffer > mask) {
                try self.writeBits(in_buffer & mask | continue_bit, vbr_bits);
                in_buffer >>= @intCast(vbr_bits - 1);
            }

            // Store remaining bits
            try self.writeBits(in_buffer, vbr_bits);
        }

        pub fn bitsVbr(value: anytype, comptime vbr_bits: usize) u16 {
            comptime {
                std.debug.assert(vbr_bits > 1);
                if (@bitSizeOf(@TypeOf(value)) > 64) @compileError("Unsupported VBR block type: " ++ @typeName(@TypeOf(value)));
            }

            var bits: u16 = 0;

            var in_buffer = bufValue(value, vbr_bits);

            const continue_bit = @as(@TypeOf(in_buffer), 1) << @intCast(vbr_bits - 1);
            const mask = continue_bit - 1;

            // If input is larger than one VBR block can store
            // then store vbr_bits - 1 bits and a continue bit
            while (in_buffer > mask) {
                bits += @intCast(vbr_bits);
                in_buffer >>= @intCast(vbr_bits - 1);
            }

            // Store remaining bits
            bits += @intCast(vbr_bits);
            return bits;
        }

        pub fn write6BitChar(self: *BcWriter, c: u8) Error!void {
            try self.writeBits(charTo6Bit(c), 6);
        }

        pub fn writeBlob(self: *BcWriter, blob: []const u8) Error!void {
            const blob_word_size = std.mem.alignForward(usize, blob.len, 4);
            try self.buffer.ensureUnusedCapacity(blob_word_size + 1);
            self.alignTo32() catch unreachable;

            const slice = self.buffer.addManyAsSliceAssumeCapacity(blob_word_size / 4);
            const slice_bytes = std.mem.sliceAsBytes(slice);
            @memcpy(slice_bytes[0..blob.len], blob);
            @memset(slice_bytes[blob.len..], 0);
        }

        pub fn alignTo32(self: *BcWriter) Error!void {
            if (self.bit_count == 0) return;

            try self.buffer.append(std.mem.nativeToLittle(u32, self.bit_buffer));
            self.bit_buffer = 0;
            self.bit_count = 0;
        }

        pub fn enterTopBlock(self: *BcWriter, comptime SubBlock: type) Error!BlockWriter(SubBlock) {
            return BlockWriter(SubBlock).init(self, 2, true);
        }

        fn BlockWriter(comptime Block: type) type {
            return struct {
                const Self = @This();

                // The minimum abbrev id length based on the number of abbrevs present in the block
                pub const abbrev_len = std.math.log2_int_ceil(
                    u6,
                    4 + (if (@hasDecl(Block, "abbrevs")) Block.abbrevs.len else 0),
                );

                start: usize,
                bitcode: *BcWriter,

                pub fn init(bitcode: *BcWriter, comptime parent_abbrev_len: u6, comptime define_abbrevs: bool) Error!Self {
                    try bitcode.writeBits(1, parent_abbrev_len);
                    try bitcode.writeVbr(Block.id, 8);
                    try bitcode.writeVbr(abbrev_len, 4);
                    try bitcode.alignTo32();

                    // We store the index of the block size and store a dummy value as the number of words in the block
                    const start = bitcode.length();
                    try bitcode.writeBits(0, 32);

                    var self = Self{
                        .start = start,
                        .bitcode = bitcode,
                    };

                    // Predefine all block abbrevs
                    if (define_abbrevs) {
                        inline for (Block.abbrevs) |Abbrev| {
                            try self.defineAbbrev(&Abbrev.ops);
                        }
                    }

                    return self;
                }

                pub fn enterSubBlock(self: Self, comptime SubBlock: type, comptime define_abbrevs: bool) Error!BlockWriter(SubBlock) {
                    return BlockWriter(SubBlock).init(self.bitcode, abbrev_len, define_abbrevs);
                }

                pub fn end(self: *Self) Error!void {
                    try self.bitcode.writeBits(0, abbrev_len);
                    try self.bitcode.alignTo32();

                    // Set the number of words in the block at the start of the block
                    self.bitcode.buffer.items[self.start] = std.mem.nativeToLittle(u32, @truncate(self.bitcode.length() - self.start - 1));
                }

                pub fn writeUnabbrev(self: *Self, code: u32, values: []const u64) Error!void {
                    try self.bitcode.writeBits(3, abbrev_len);
                    try self.bitcode.writeVbr(code, 6);
                    try self.bitcode.writeVbr(values.len, 6);
                    for (values) |val| {
                        try self.bitcode.writeVbr(val, 6);
                    }
                }

                pub fn writeAbbrev(self: *Self, params: anytype) Error!void {
                    return self.writeAbbrevAdapted(params, struct {
                        pub fn get(_: @This(), param: anytype) @TypeOf(param) {
                            return param;
                        }
                    }{});
                }

                pub fn abbrevId(comptime Abbrev: type) u32 {
                    inline for (Block.abbrevs, 0..) |abbrev, i| {
                        if (Abbrev == abbrev) return i + 4;
                    }

                    @compileError("Unknown abbrev: " ++ @typeName(Abbrev));
                }

                pub fn writeAbbrevAdapted(
                    self: *Self,
                    params: anytype,
                    adapter: anytype,
                ) Error!void {
                    const Abbrev = @TypeOf(params);

                    try self.bitcode.writeBits(comptime abbrevId(Abbrev), abbrev_len);

                    const fields = std.meta.fields(Abbrev);

                    // This abbreviation might only contain literals
                    if (fields.len == 0) return;

                    comptime var field_index: usize = 0;
                    inline for (Abbrev.ops) |ty| {
                        const param = @field(params, fields[field_index].name);
                        switch (ty) {
                            .literal => continue,
                            .fixed => |len| try self.bitcode.writeBits(adapter.get(param), len),
                            .fixed_runtime => |width_ty| try self.bitcode.writeBits(
                                adapter.get(param),
                                self.bitcode.getTypeWidth(width_ty),
                            ),
                            .vbr => |len| try self.bitcode.writeVbr(adapter.get(param), len),
                            .char6 => try self.bitcode.write6BitChar(adapter.get(param)),
                            .blob => {
                                try self.bitcode.writeVbr(param.len, 6);
                                try self.bitcode.writeBlob(param);
                            },
                            .array_fixed => |len| {
                                try self.bitcode.writeVbr(param.len, 6);
                                for (param) |x| {
                                    try self.bitcode.writeBits(adapter.get(x), len);
                                }
                            },
                            .array_fixed_runtime => |width_ty| {
                                try self.bitcode.writeVbr(param.len, 6);
                                for (param) |x| {
                                    try self.bitcode.writeBits(
                                        adapter.get(x),
                                        self.bitcode.getTypeWidth(width_ty),
                                    );
                                }
                            },
                            .array_vbr => |len| {
                                try self.bitcode.writeVbr(param.len, 6);
                                for (param) |x| {
                                    try self.bitcode.writeVbr(adapter.get(x), len);
                                }
                            },
                            .array_char6 => {
                                try self.bitcode.writeVbr(param.len, 6);
                                for (param) |x| {
                                    try self.bitcode.write6BitChar(adapter.get(x));
                                }
                            },
                        }
                        field_index += 1;
                        if (field_index == fields.len) break;
                    }
                }

                pub fn defineAbbrev(self: *Self, comptime ops: []const AbbrevOp) Error!void {
                    const bitcode = self.bitcode;
                    try bitcode.writeBits(2, abbrev_len);

                    // ops.len is not accurate because arrays are actually two ops
                    try bitcode.writeVbr(blk: {
                        var count: usize = 0;
                        inline for (ops) |op| {
                            count += switch (op) {
                                .literal, .fixed, .fixed_runtime, .vbr, .char6, .blob => 1,
                                .array_fixed, .array_fixed_runtime, .array_vbr, .array_char6 => 2,
                            };
                        }
                        break :blk count;
                    }, 5);

                    inline for (ops) |op| {
                        switch (op) {
                            .literal => |value| {
                                try bitcode.writeBits(1, 1);
                                try bitcode.writeVbr(value, 8);
                            },
                            .fixed => |width| {
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(1, 3);
                                try bitcode.writeVbr(width, 5);
                            },
                            .fixed_runtime => |width_ty| {
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(1, 3);
                                try bitcode.writeVbr(bitcode.getTypeWidth(width_ty), 5);
                            },
                            .vbr => |width| {
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(2, 3);
                                try bitcode.writeVbr(width, 5);
                            },
                            .char6 => {
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(4, 3);
                            },
                            .blob => {
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(5, 3);
                            },
                            .array_fixed => |width| {
                                // Array op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(3, 3);

                                // Fixed or VBR op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(1, 3);
                                try bitcode.writeVbr(width, 5);
                            },
                            .array_fixed_runtime => |width_ty| {
                                // Array op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(3, 3);

                                // Fixed or VBR op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(1, 3);
                                try bitcode.writeVbr(bitcode.getTypeWidth(width_ty), 5);
                            },
                            .array_vbr => |width| {
                                // Array op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(3, 3);

                                // Fixed or VBR op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(2, 3);
                                try bitcode.writeVbr(width, 5);
                            },
                            .array_char6 => {
                                // Array op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(3, 3);

                                // Char6 op
                                try bitcode.writeBits(0, 1);
                                try bitcode.writeBits(4, 3);
                            },
                        }
                    }
                }
            };
        }
    };
}

fn charTo6Bit(c: u8) u8 {
    return switch (c) {
        'a'...'z' => c - 'a',
        'A'...'Z' => c - 'A' + 26,
        '0'...'9' => c - '0' + 52,
        '.' => 62,
        '_' => 63,
        else => @panic("Failed to encode byte as 6-bit char"),
    };
}

fn BufType(comptime T: type, comptime min_len: usize) type {
    return std.meta.Int(.unsigned, @max(min_len, @bitSizeOf(switch (@typeInfo(T)) {
        .comptime_int => u32,
        .int => |info| if (info.signedness == .unsigned)
            T
        else
            @compileError("Unsupported type: " ++ @typeName(T)),
        .@"enum" => |info| info.tag_type,
        .bool => u1,
        .@"struct" => |info| switch (info.layout) {
            .auto, .@"extern" => @compileError("Unsupported type: " ++ @typeName(T)),
            .@"packed" => std.meta.Int(.unsigned, @bitSizeOf(T)),
        },
        else => @compileError("Unsupported type: " ++ @typeName(T)),
    })));
}

fn bufValue(value: anytype, comptime min_len: usize) BufType(@TypeOf(value), min_len) {
    return switch (@typeInfo(@TypeOf(value))) {
        .comptime_int, .int => @intCast(value),
        .@"enum" => @intFromEnum(value),
        .bool => @intFromBool(value),
        .@"struct" => @intCast(@as(std.meta.Int(.unsigned, @bitSizeOf(@TypeOf(value))), @bitCast(value))),
        else => unreachable,
    };
}



---
File: /std/zig/llvm/BitcodeReader.zig
---

const BitcodeReader = @This();

const std = @import("../../std.zig");
const assert = std.debug.assert;

allocator: std.mem.Allocator,
record_arena: std.heap.ArenaAllocator.State,
reader: *std.Io.Reader,
keep_names: bool,
bit_buffer: u32,
bit_offset: u5,
stack: std.ArrayList(State),
block_info: std.AutoHashMapUnmanaged(u32, Block.Info),

pub const Item = union(enum) {
    start_block: Block,
    record: Record,
    end_block: Block,
};

pub const Block = struct {
    name: []const u8,
    id: u32,
    len: u32,

    const block_info: u32 = 0;
    const first_reserved: u32 = 1;
    const last_standard: u32 = 7;

    const Info = struct {
        block_name: []const u8,
        record_names: std.AutoHashMapUnmanaged(u32, []const u8),
        abbrevs: Abbrev.Store,

        const default: Info = .{
            .block_name = &.{},
            .record_names = .empty,
            .abbrevs = .{ .abbrevs = .empty },
        };

        const set_bid_id: u32 = 1;
        const block_name_id: u32 = 2;
        const set_record_name_id: u32 = 3;

        fn deinit(info: *Info, allocator: std.mem.Allocator) void {
            allocator.free(info.block_name);
            var record_names_it = info.record_names.valueIterator();
            while (record_names_it.next()) |record_name| allocator.free(record_name.*);
            info.record_names.deinit(allocator);
            info.abbrevs.deinit(allocator);
            info.* = undefined;
        }
    };
};

pub const Record = struct {
    name: []const u8,
    id: u32,
    operands: []const u64,
    blob: []const u8,

    fn toOwnedAbbrev(record: Record, allocator: std.mem.Allocator) !Abbrev {
        var operands = std.array_list.Managed(Abbrev.Operand).init(allocator);
        defer operands.deinit();

        assert(record.id == Abbrev.Builtin.define_abbrev.toRecordId());
        var i: usize = 0;
        while (i < record.operands.len) switch (record.operands[i]) {
            Abbrev.Operand.literal_id => {
                try operands.append(.{ .literal = record.operands[i + 1] });
                i += 2;
            },
            @intFromEnum(Abbrev.Operand.Encoding.fixed) => {
                try operands.append(.{ .encoding = .{ .fixed = @intCast(record.operands[i + 1]) } });
                i += 2;
            },
            @intFromEnum(Abbrev.Operand.Encoding.vbr) => {
                try operands.append(.{ .encoding = .{ .vbr = @intCast(record.operands[i + 1]) } });
                i += 2;
            },
            @intFromEnum(Abbrev.Operand.Encoding.array) => {
                try operands.append(.{ .encoding = .{ .array = 6 } });
                i += 1;
            },
            @intFromEnum(Abbrev.Operand.Encoding.char6) => {
                try operands.append(.{ .encoding = .char6 });
                i += 1;
            },
            @intFromEnum(Abbrev.Operand.Encoding.blob) => {
                try operands.append(.{ .encoding = .{ .blob = 6 } });
                i += 1;
            },
            else => unreachable,
        };

        return .{ .operands = try operands.toOwnedSlice() };
    }
};

pub const InitOptions = struct {
    reader: *std.Io.Reader,
    keep_names: bool = false,
};
pub fn init(allocator: std.mem.Allocator, options: InitOptions) BitcodeReader {
    return .{
        .allocator = allocator,
        .record_arena = .{},
        .reader = options.reader,
        .keep_names = options.keep_names,
        .bit_buffer = 0,
        .bit_offset = 0,
        .stack = .empty,
        .block_info = .empty,
    };
}

pub fn deinit(bc: *BitcodeReader) void {
    var block_info_it = bc.block_info.valueIterator();
    while (block_info_it.next()) |block_info| block_info.deinit(bc.allocator);
    bc.block_info.deinit(bc.allocator);
    for (bc.stack.items) |*state| state.deinit(bc.allocator);
    bc.stack.deinit(bc.allocator);
    bc.record_arena.promote(bc.allocator).deinit();
    bc.* = undefined;
}

pub fn checkMagic(bc: *BitcodeReader, magic: *const [4]u8) !void {
    var buffer: [4]u8 = undefined;
    try bc.readBytes(&buffer);
    if (!std.mem.eql(u8, &buffer, magic)) return error.InvalidMagic;

    try bc.startBlock(null, 2);
    try bc.block_info.put(bc.allocator, Block.block_info, Block.Info.default);
}

pub fn next(bc: *BitcodeReader) !?Item {
    while (true) {
        const record = (try bc.nextRecord()) orelse
            return if (bc.stack.items.len > 1) error.EndOfStream else null;
        switch (record.id) {
            else => return .{ .record = record },
            Abbrev.Builtin.end_block.toRecordId() => {
                const block_id = bc.stack.items[bc.stack.items.len - 1].block_id.?;
                try bc.endBlock();
                return .{ .end_block = .{
                    .name = if (bc.block_info.get(block_id)) |block_info|
                        block_info.block_name
                    else
                        &.{},
                    .id = block_id,
                    .len = 0,
                } };
            },
            Abbrev.Builtin.enter_subblock.toRecordId() => {
                const block_id: u32 = @intCast(record.operands[0]);
                switch (block_id) {
                    Block.block_info => {
                        try bc.startBlock(Block.block_info, @intCast(record.operands[1]));
                        try bc.parseBlockInfoBlock();
                        try bc.endBlock();
                    },
                    Block.first_reserved...Block.last_standard => return error.UnsupportedBlockId,
                    else => {
                        try bc.startBlock(block_id, @intCast(record.operands[1]));
                        return .{ .start_block = .{
                            .name = if (bc.block_info.get(block_id)) |block_info|
                                block_info.block_name
                            else
                                &.{},
                            .id = block_id,
                            .len = @intCast(record.operands[2]),
                        } };
                    },
                }
            },
            Abbrev.Builtin.define_abbrev.toRecordId() => try bc.stack.items[bc.stack.items.len - 1]
                .abbrevs.addOwnedAbbrev(bc.allocator, try record.toOwnedAbbrev(bc.allocator)),
        }
    }
}

pub fn skipBlock(bc: *BitcodeReader, block: Block) !void {
    assert(bc.bit_offset == 0);
    try bc.reader.discardAll(4 * @as(usize, block.len));
    try bc.endBlock();
}

fn nextRecord(bc: *BitcodeReader) !?Record {
    const state = &bc.stack.items[bc.stack.items.len - 1];
    const abbrev_id = bc.readFixed(u32, state.abbrev_id_width) catch |err| switch (err) {
        error.EndOfStream => return null,
        else => |e| return e,
    };
    if (abbrev_id >= state.abbrevs.abbrevs.items.len) return error.InvalidAbbrevId;
    const abbrev = state.abbrevs.abbrevs.items[abbrev_id];

    var record_arena = bc.record_arena.promote(bc.allocator);
    defer bc.record_arena = record_arena.state;
    _ = record_arena.reset(.retain_capacity);

    var operands = try std.array_list.Managed(u64).initCapacity(record_arena.allocator(), abbrev.operands.len);
    var blob = std.array_list.Managed(u8).init(record_arena.allocator());
    for (abbrev.operands, 0..) |abbrev_operand, abbrev_operand_i| switch (abbrev_operand) {
        .literal => |value| operands.appendAssumeCapacity(value),
        .encoding => |abbrev_encoding| switch (abbrev_encoding) {
            .fixed => |width| operands.appendAssumeCapacity(try bc.readFixed(u64, width)),
            .vbr => |width| operands.appendAssumeCapacity(try bc.readVbr(u64, width)),
            .array => |len_width| {
                assert(abbrev_operand_i + 2 == abbrev.operands.len);
                const len: usize = @intCast(try bc.readVbr(u32, len_width));
                try operands.ensureUnusedCapacity(len);
                for (0..len) |_| switch (abbrev.operands[abbrev.operands.len - 1]) {
                    .literal => |elem_value| operands.appendAssumeCapacity(elem_value),
                    .encoding => |elem_encoding| switch (elem_encoding) {
                        .fixed => |elem_width| operands.appendAssumeCapacity(try bc.readFixed(u64, elem_width)),
                        .vbr => |elem_width| operands.appendAssumeCapacity(try bc.readVbr(u64, elem_width)),
                        .array, .blob => return error.InvalidArrayElement,
                        .char6 => operands.appendAssumeCapacity(t
```
