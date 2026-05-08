```
    try a.pegSinglePtrTypeStart();
            } else if (kind == .many_pointer) {
                try a.pegManyPtrTypeStart();
            } else {
                try a.pegSliceTypeStart();
            }

            if (a.smith.value(bool)) {
                try a.pegToken(.keyword_allowzero);
            }
            if (a.smith.value(bool)) {
                if (is_single) {
                    try a.pegBitAlign();
                } else {
                    try a.pegByteAlign();
                }
            }
            if (a.smith.value(bool)) {
                try a.pegAddrSpace();
            }
            if (a.smith.value(bool)) {
                try a.pegToken(.keyword_const);
            }
            if (a.smith.value(bool)) {
                try a.pegToken(.keyword_volatile);
            }
        },
    }
}

/// SuffixOp
///     <- LBRACKET Expr (DOT2 (Expr? (COLON Expr)?)?)? RBRACKET
///      / DOT IDENTIFIER
///      / DOTASTERISK
///      / DOTQUESTIONMARK
fn pegSuffixOp(a: *AstSmith) SourceError!void {
    switch (a.smith.value(enum { slice, field, deref, unwrap })) {
        .slice => {
            try a.pegToken(.l_bracket);
            try a.pegExpr();

            const components = a.smith.value(u2);
            if (components >= 1) try a.pegToken(.ellipsis2);
            if (components >= 2) try a.pegExpr();
            if (components >= 3) {
                try a.pegToken(.colon);
                try a.pegExpr();
            }

            try a.pegToken(.r_bracket);
        },
        .field => {
            try a.pegToken(.period);
            try a.pegIdentifier();
        },
        .deref => try a.pegToken(.period_asterisk),
        .unwrap => {
            try a.pegToken(.period);
            try a.pegToken(.question_mark);
        },
    }
}

/// FnCallArguments <- LPAREN ExprList RPAREN
fn pegFnCallArguments(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_paren);
    try a.pegExprList();
    try a.pegToken(.r_paren);
}

/// SliceTypeStart <- LBRACKET (COLON Expr)? RBRACKET
fn pegSliceTypeStart(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_bracket);
    if (a.smith.value(bool)) {
        try a.pegToken(.colon);
        try a.pegExpr();
    }
    try a.pegToken(.r_bracket);
}

/// SinglePtrTypeStart <- ASTERISK / ASTERISK2
fn pegSinglePtrTypeStart(a: *AstSmith) SourceError!void {
    try a.pegToken(if (!a.smith.value(bool)) .asterisk else .asterisk_asterisk);
}

/// ManyPtrTypeStart <- LBRACKET ASTERISK (LETTERC / COLON Expr)? RBRACKET
fn pegManyPtrTypeStart(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_bracket);
    try a.pegToken(.asterisk);
    switch (a.smith.value(enum { many, many_c, many_sentinel })) {
        .many => {},
        .many_c => {
            // No need for `preservePegEndOfWord` because the previous token is an asterisk
            try a.addTokenTag(.identifier);
            try a.addSourceByte('c');
        },
        .many_sentinel => {
            try a.pegToken(.colon);
            try a.pegExpr();
        },
    }
    try a.pegToken(.r_bracket);
}

/// ArrayTypeStart <- LBRACKET !(ASTERISK / ASTERISK2) Expr (COLON Expr)? RBRACKET
fn pegArrayTypeStart(a: *AstSmith) SourceError!void {
    try a.pegToken(.l_bracket);
    a.not_token = .asterisk;
    try a.pegExpr();
    if (a.smith.value(bool)) {
        try a.pegToken(.colon);
        try a.pegExpr();
    }
    try a.pegToken(.r_bracket);
}

/// ContainerDeclAuto <- ContainerDeclType LBRACE ContainerMembers RBRACE
fn pegContainerDeclAuto(a: *AstSmith) SourceError!void {
    try a.pegContainerDeclType();
    try a.pegToken(.l_brace);
    try a.pegContainerMembers();
    try a.pegToken(.r_brace);
}

/// ContainerDeclType
///     <- KEYWORD_struct (LPAREN Expr RPAREN)?
///      / KEYWORD_opaque
///      / KEYWORD_enum (LPAREN Expr RPAREN)?
///      / KEYWORD_union (LPAREN (KEYWORD_enum (LPAREN Expr RPAREN)? / !KEYWORD_enum Expr) RPAREN)?
fn pegContainerDeclType(a: *AstSmith) SourceError!void {
    switch (a.smith.value(enum { @"struct", @"opaque", @"enum", @"union" })) {
        .@"struct", .@"enum" => |c| {
            const is_struct = c == .@"struct" or a.not_token == .keyword_enum;
            try a.pegToken(if (is_struct) .keyword_struct else .keyword_enum);
            if (a.smith.value(bool)) {
                try a.pegToken(.l_paren);
                try a.pegExpr();
                try a.pegToken(.r_paren);
            }
        },
        .@"opaque" => try a.pegToken(.keyword_opaque),
        .@"union" => {
            try a.pegToken(.keyword_union);
            switch (a.smith.value(enum { no_tag, expr_tag, enum_tag, enum_expr_tag })) {
                .no_tag => {},
                .expr_tag => {
                    try a.pegToken(.l_paren);
                    a.not_token = .keyword_enum;
                    try a.pegExpr();
                    try a.pegToken(.r_paren);
                },
                .enum_tag => {
                    try a.pegToken(.l_paren);
                    try a.pegToken(.keyword_enum);
                    try a.pegToken(.r_paren);
                },
                .enum_expr_tag => {
                    try a.pegToken(.l_paren);
                    try a.pegToken(.keyword_enum);
                    try a.pegToken(.l_paren);
                    try a.pegExpr();
                    try a.pegToken(.r_paren);
                    try a.pegToken(.r_paren);
                },
            }
        },
    }
}

/// ByteAlign <- KEYWORD_align LPAREN Expr RPAREN
fn pegByteAlign(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_align);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    try a.pegToken(.r_paren);
}

/// BitAlign <- KEYWORD_align LPAREN Expr (COLON Expr COLON Expr)? RPAREN
fn pegBitAlign(a: *AstSmith) SourceError!void {
    try a.pegToken(.keyword_align);
    try a.pegToken(.l_paren);
    try a.pegExpr();
    if (a.smith.value(bool)) {
        try a.pegToken(.colon);
        try a.pegExpr();
        try a.pegToken(.colon);
        try a.pegExpr();
    }
    try a.pegToken(.r_paren);
}

/// IdentifierList <- (doc_comment? IDENTIFIER COMMA)* (doc_comment? IDENTIFIER)?
fn pegIdentifierList(a: *AstSmith) SourceError!void {
    while (!a.smith.eos()) {
        try a.pegMaybeDocComment();
        try a.pegIdentifier();
        try a.pegToken(.comma);
    }
    if (a.smith.value(bool)) {
        try a.pegMaybeDocComment();
        try a.pegIdentifier();
    }
}

/// SwitchProngList <- (SwitchProng COMMA)* SwitchProng?
fn pegSwitchProngList(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegSwitchProng();
        try a.pegToken(.comma);
    }
    if (a.smithListItemBool()) {
        try a.pegSwitchProng();
    }
}

/// AsmOutputList <- (AsmOutputItem COMMA)* AsmOutputItem?
fn pegAsmOutputList(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegAsmOutputItem();
        try a.pegToken(.comma);
    }
    if (a.smithListItemBool()) {
        try a.pegAsmOutputItem();
    }
}

/// AsmInputList <- (AsmInputItem COMMA)* AsmInputItem?
fn pegAsmInputList(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegAsmInputItem();
        try a.pegToken(.comma);
    }
    if (a.smithListItemBool()) {
        try a.pegAsmInputItem();
    }
}

/// ParamDeclList <- (ParamDecl COMMA)* (ParamDecl / DOT3 COMMA?)?
fn pegParamDeclList(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegParamDecl();
        try a.pegToken(.comma);
    }
    const Final = enum { none, dot3, dot3_comma, param };
    switch (a.smith.valueWeighted(Final, &.{
        .rangeLessThan(Final, .none, .param, 2),
        .value(Final, .param, 1),
    })) {
        .none => {},
        .dot3 => try a.pegToken(.ellipsis3),
        .dot3_comma => {
            try a.pegToken(.ellipsis3);
            try a.pegToken(.comma);
        },
        .param => try a.pegParamDecl(),
    }
}

/// ExprList <- (Expr COMMA)* Expr?
fn pegExprList(a: *AstSmith) SourceError!void {
    while (!a.smithListItemEos()) {
        try a.pegExpr();
        try a.pegToken(.comma);
    }
    if (a.smithListItemBool()) {
        try a.pegExpr();
    }
}

/// container_doc_comment <- ('//!' non_control_utf8* [ \n]* skip)+
fn pegContainerDocComment(a: *AstSmith) SourceError!void {
    while (true) {
        try a.addTokenTag(.container_doc_comment);
        try a.pegGenericLine("//!", .any);
        try a.pegSkip();
        if (a.smith.eos()) break;
    }
}

/// doc_comment?
fn pegMaybeDocComment(a: *AstSmith) SourceError!void {
    // A specific hash is provided here since this function is likely to be inlined,
    // however having all doc comments with the same uid is beneficial.
    if (a.smith.boolWeightedWithHash(63, 1, 0x39b94392)) {
        try a.pegDocComment();
    }
}

/// doc_comment <- ('///' non_control_utf8* [ \n]* skip)+
fn pegDocComment(a: *AstSmith) SourceError!void {
    if (a.source_len > 0 and a.source_buf[a.source_len - 1] != '\n') {
        try a.addSourceByte('\n');
    }
    while (true) {
        try a.addTokenTag(.doc_comment);
        try a.pegGenericLine("///", .doc_comment);
        try a.pegSkip();
        if (a.smith.eosWeightedSimple(1, 3)) break;
    }
}

/// line_comment <- '//' ![!/] non_control_utf8* / '////' non_control_utf8*
fn pegLineComment(a: *AstSmith) SourceError!void {
    return a.pegGenericLine("//", .line_comment);
}

/// line_string <- '\\\\' non_control_utf8* [ \n]*
fn pegLineString(a: *AstSmith) SourceError!void {
    try a.addTokenTag(.multiline_string_literal_line);
    return a.pegGenericLine("\\\\", .any);
}

/// non_control_utf8 <- [\040-\377]
///
/// Used for line, doc, and container comments as well as
/// multiline string literal lines.
fn pegGenericLine(
    a: *AstSmith,
    prefix: []const u8,
    /// Adds constraints to what the line contains
    prefix_kind: enum { any, line_comment, doc_comment },
) SourceError!void {
    const cr = a.smith.value(bool);
    const newline_len = @intFromBool(cr) + @as(usize, 1);

    try a.ensureSourceCapacity(prefix.len + newline_len);
    a.addSourceAssumeCapacity(prefix);

    const line = a.variableChar(newline_len, 0, &.{
        .rangeAtMost(u8, ' ', 0x7f - 1, 1),
        .rangeAtMost(u8, 0x7f + 1, 0xff, 1),
    });
    if (line.len >= 1) switch (prefix_kind) {
        .any => {},
        .line_comment => {
            // Convert doc comments to quadruple slashes when possible;
            // Otherwise, and for container doc comments, erase the '/' or '!'
            if (line[0] == '/' and line.len >= 2) {
                line[1] = '/';
            } else if (line[0] == '/' or line[0] == '!') {
                line[0] = ' ';
            }
        },
        .doc_comment => {
            // Avoid quadruple slashes
            if (line[0] == '/') {
                line[0] = ' ';
            }
        },
    };

    if (cr) a.addSourceByteAssumeCapacity('\r');
    a.addSourceByteAssumeCapacity('\n');
}

/// skip <- ([ \n] / line_comment)*
fn pegSkip(a: *AstSmith) SourceError!void {
    if (a.smith.boolWeighted(63, 1)) {
        while (true) {
            const Kind = enum {
                space,
                line_break,
                cr_line_break,
                line_comment,
                line_comment_zig_fmt_off,
                line_comment_zig_fmt_on,
            };

            const weights = Smith.baselineWeights(Kind) ++
                [_]Weight{.value(Kind, .space, 11)};
            switch (a.smith.valueWeighted(Kind, weights)) {
                .space => try a.addSourceByte(' '),
                .line_break => try a.addSourceByte('\n'),
                .cr_line_break => try a.addSource("\r\n"),
                .line_comment => try a.pegLineComment(),
                .line_comment_zig_fmt_off => try a.addSource("//zig fmt: off\n"),
                .line_comment_zig_fmt_on => try a.addSource("//zig fmt: on\n"),
            }

            if (a.smith.eos()) break;
        }
    }
}

const bin_weights: []const Weight = &.{.rangeAtMost(u8, '0', '1', 1)};
const oct_weights: []const Weight = &.{.rangeAtMost(u8, '0', '7', 1)};
const dec_weights: []const Weight = &.{.rangeAtMost(u8, '0', '9', 1)};
const hex_weights: []const Weight = &.{
    .rangeAtMost(u8, '0', '9', 1),
    .rangeAtMost(u8, 'a', 'f', 1),
    .rangeAtMost(u8, 'A', 'F', 1),
};

/// Asserts enough capacity for at `min + reserved_capacity`
fn variableChar(
    a: *AstSmith,
    reserved_capacity: usize,
    min: usize,
    weights: []const Weight,
) []u8 {
    const capacity = a.sourceCapacity();
    const max_out = capacity.len - reserved_capacity;

    const len_weights: [3]Weight = .{
        .rangeAtMost(u32, @intCast(min), @min(2, max_out), 32678),
        // For the below `.rangeAtMost` is not used because max may be less than min.
        // In this case, the weights are omitted.
        .{ .min = 3, .max = @min(16, max_out), .weight = 512 },
        // Still allow much longer sequences to test parsing overflows
        .{ .min = 17, .max = @min(256, max_out), .weight = 1 },
    };
    const n_weights = @as(usize, 1) + @intFromBool(max_out >= 3) + @intFromBool(max_out >= 17);

    const len = a.smith.sliceWeighted(capacity, len_weights[0..n_weights], weights);
    a.source_len += len;
    return capacity[0..len];
}

/// char_escape
///     <- "\\x" hex hex
///      / "\\u{" hex+ "}"
///      / "\\" [nr\\t'"]
/// char_char
///     <- multibyte_utf8
///      / char_escape
///      / ![\\'\n] non_control_ascii
///
/// string_char
///     <- multibyte_utf8
///      / char_escape
///      / ![\\"\n] non_control_ascii
fn pegChar(a: *AstSmith, quote: u8) SourceError!void {
    const Char = enum(u8) {
        ascii,
        unicode_2,
        unicode_3,
        unicode_4,
        hex_escape,
        unicode_escape,
        char_escape,
    };
    const weights = Smith.baselineWeights(Char) ++ &[_]Weight{.value(Char, .ascii, 32)};
    switch (a.smith.valueWeighted(Char, weights)) {
        .ascii => try a.addSourceByte(a.smith.valueWeighted(u8, &.{
            .rangeAtMost(u8, ' ', quote - 1, 1),
            .rangeAtMost(u8, quote + 1, '\\' - 1, 1),
            .rangeAtMost(u8, '\\' + 1, 0x7e, 1),
        })),
        .unicode_2 => assert(2 == std.unicode.wtf8Encode(
            a.smith.valueRangeLessThan(u21, 0x80, 0x800),
            try a.addSourceAsSlice(2),
        ) catch unreachable),
        .unicode_3 => assert(3 == std.unicode.wtf8Encode(
            a.smith.valueRangeLessThan(u21, 0x800, 0x10000),
            try a.addSourceAsSlice(3),
        ) catch unreachable),
        .unicode_4 => assert(4 == std.unicode.wtf8Encode(
            a.smith.valueRangeLessThan(u21, 0x10000, 0x110000),
            try a.addSourceAsSlice(4),
        ) catch unreachable),
        .hex_escape => {
            try a.ensureSourceCapacity(4);
            a.addSourceAssumeCapacity("\\x");
            a.smith.bytesWeighted(a.addSourceAsSliceAssumeCapacity(2), hex_weights);
        },
        .unicode_escape => {
            try a.ensureSourceCapacity(5);
            a.addSourceAssumeCapacity("\\u{");
            _ = a.variableChar(1, 1, hex_weights);
            a.addSourceByteAssumeCapacity('}');
        },
        .char_escape => {
            try a.ensureSourceCapacity(2);
            a.addSourceByteAssumeCapacity('\\');
            a.addSourceByteAssumeCapacity(a.smith.valueWeighted(u8, &.{
                .value(u8, 'n', 1),
                .value(u8, 'r', 1),
                .value(u8, 't', 1),
                .value(u8, '\\', 1),
                .value(u8, '\'', 1),
                .value(u8, '"', 1),
            }));
        },
    }
}

/// CHAR_LITERAL <- ['] char_char ['] skip
fn pegCharLiteral(a: *AstSmith) SourceError!void {
    try a.addTokenTag(.char_literal);
    try a.addSourceByte('\'');
    try a.pegChar('\'');
    try a.addSourceByte('\'');
    try a.pegSkip();
}

///FLOAT
///    <- '0x' hex_int '.' hex_int ([pP] [-+]? dec_int)? skip
///     /      dec_int '.' dec_int ([eE] [-+]? dec_int)? skip
///     / '0x' hex_int [pP] [-+]? dec_int skip
///     /      dec_int [eE] [-+]? dec_int skip
fn pegFloat(a: *AstSmith) SourceError!void {
    try a.preservePegEndOfWord();
    try a.addTokenTag(.number_literal);

    const hex = a.smith.value(bool);
    const exp = a.smith.value(packed struct(u3) {
        kind: enum(u2) { none, no_sign, minus, plus },
        upper: bool,
    });
    const dot = exp.kind == .none or a.smith.value(bool);

    var reserved: usize = @intFromBool(hex) * "0x".len + "0".len + @intFromBool(dot) * ".0".len +
        switch (exp.kind) {
            .none => 0,
            .no_sign => "e0".len,
            .minus => "e-0".len,
            .plus => "e+0".len,
        };
    try a.ensureSourceCapacity(reserved);

    if (hex) {
        reserved -= 2;
        a.addSourceAssumeCapacity("0x");
    }
    const digits = if (hex) hex_weights else dec_weights;

    reserved -= 1;
    _ = a.variableChar(reserved, 1, digits);

    if (dot) {
        reserved -= 2;
        a.addSourceByteAssumeCapacity('.');
        _ = a.variableChar(reserved, 1, digits);
    }

    if (exp.kind != .none) {
        reserved -= 1;
        const case_diff = @as(u8, 'a' - 'A') * @intFromBool(exp.upper);
        a.addSourceByteAssumeCapacity(@as(u8, if (hex) 'p' else 'e') - case_diff);

        if (exp.kind != .no_sign) {
            reserved -= 1;
            a.addSourceByteAssumeCapacity(if (exp.kind == .plus) '+' else '-');
        }

        reserved -= 1;
        assert(reserved == 0);
        _ = a.variableChar(reserved, 1, dec_weights);
    }
}

///INTEGER
///    <- '0b' bin_int skip
///     / '0o' oct_int skip
///     / '0x' hex_int skip
///     /      dec_int skip
fn pegInteger(a: *AstSmith) SourceError!void {
    try a.preservePegEndOfWord();
    try a.addTokenTag(.number_literal);
    const Base = enum { bin, dec, oct, hex };
    const base_weights: []const Weight = Smith.baselineWeights(Base) ++
        &[_]Weight{ .value(Base, .dec, 6), .value(Base, .hex, 2) };
    const digits, const prefix = switch (a.smith.valueWeighted(Base, base_weights)) {
        .bin => .{ bin_weights, "0b" },
        .oct => .{ oct_weights, "0o" },
        .dec => .{ dec_weights, "" },
        .hex => .{ hex_weights, "0x" },
    };
    try a.ensureSourceCapacity(prefix.len + 1);
    if (prefix.len != 0) a.addSourceAssumeCapacity(prefix);
    _ = a.variableChar(0, 1, digits);
}

/// Does not include 'skip'. Does not add any token tag.
fn stringLiteralSingleInner(a: *AstSmith) SourceError!void {
    try a.addSourceByte('"');
    while (!a.smith.eosWeightedSimple(3, 1)) {
        try a.pegChar('"');
    }
    try a.addSourceByte('"');
}

/// STRINGLITERALSINGLE <- ["] string_char* ["] skip
fn pegStringLiteralSingle(a: *AstSmith) SourceError!void {
    try a.addTokenTag(.string_literal);
    try a.stringLiteralSingleInner();
    try a.pegSkip();
}

/// STRINGLITERAL
///     <- STRINGLITERALSINGLE
///      / (line_string skip)+
fn pegStringLiteral(a: *AstSmith) SourceError!void {
    if (a.smith.value(bool)) {
        try a.pegStringLiteralSingle();
    } else {
        while (true) {
            try a.pegLineString();
            try a.pegSkip();
            if (a.smith.eos()) break;
        }
    }
}

const alphanumeric_weights: [4]Weight = .{
    .rangeAtMost(u8, '0', '9', 1),
    .rangeAtMost(u8, 'A', 'Z', 1),
    .rangeAtMost(u8, 'a', 'z', 1),
    .value(u8, '_', 1),
};

/// IDENTIFIER
///     <- !keyword [A-Za-z_] [A-Za-z0-9_]* skip
///      / '@' STRINGLITERALSINGLE
fn pegIdentifier(a: *AstSmith) SourceError!void {
    const Kind = enum(u2) { underscore, regular_identifier, quoted_identifier, copy_identifier };
    const kind_weights: [4]Weight = .{
        .value(Kind, .underscore, 6),
        .value(Kind, .regular_identifier, 3),
        .value(Kind, .quoted_identifier, 1),
        .value(Kind, .copy_identifier, 6),
    };
    const n_weights = @as(usize, kind_weights.len) - @intFromBool(a.prev_ids_len == 0);
    const kind = a.smith.valueWeighted(Kind, kind_weights[0..n_weights]);

    switch (kind) {
        .underscore => {
            try a.preservePegEndOfWord();
            try a.addTokenTag(.identifier);
            try a.addSourceByte('_');
        },
        .regular_identifier => {
            try a.preservePegEndOfWord();
            try a.addTokenTag(.identifier);

            const start = a.source_len;
            try a.addSourceByte(a.smith.valueWeighted(u8, alphanumeric_weights[1..]));
            _ = a.variableChar(0, 0, &alphanumeric_weights);

            if (Token.getKeyword(a.source_buf[start..a.source_len]) != null) {
                a.source_buf[start] = '_'; // No keywords start with '_'
            }
        },
        .quoted_identifier => {
            try a.addTokenTag(.identifier);
            try a.addSourceByte('@');
            try a.stringLiteralSingleInner();
        },
        .copy_identifier => {
            const n_prev = @min(a.prev_ids_len, a.prev_ids_buf.len);
            const prev_i = a.smith.valueRangeLessThan(u16, 0, n_prev);
            const prev = a.prev_ids_buf[prev_i];

            if (a.source_buf[prev.start] != '@') try a.preservePegEndOfWord();
            try a.addTokenTag(.identifier);
            try a.addSource(a.source_buf[prev.start..][0..prev.len]);
        },
    }
    try a.pegSkip();
    if (kind != .copy_identifier) {
        const start = a.token_start_buf[a.tokens_len - 1];
        a.prev_ids_buf[a.prev_ids_len % a.prev_ids_buf.len] = .{
            .start = @intCast(start),
            .len = @intCast(a.source_len - start),
        };
        a.prev_ids_len += 1;
    }
}

/// BUILTINIDENTIFIER <- '@'[A-Za-z_][A-Za-z0-9_]* skip
fn pegBuiltinIdentifier(a: *AstSmith) SourceError!void {
    try a.addTokenTag(.builtin);
    if (a.smith.boolWeighted(1, 31)) {
        if (a.smith.boolWeighted(1, 8)) {
            // Pointer cast (reordable with zig fmt)
            const ids = [_][]const u8{
                "@ptrCast",
                "@addrspaceCast",
                "@alignCast",
                "@constCast",
                "@volatileCast",
            };
            try a.addSource(ids[a.smith.index(ids.len)]);
        } else {
            const ids = std.zig.BuiltinFn.list.keys();
            try a.addSource(ids[a.smith.index(ids.len)]);
        }
    } else {
        try a.ensureSourceCapacity(2);
        a.addSourceByteAssumeCapacity('@');
        a.addSourceByteAssumeCapacity(a.smith.valueWeighted(u8, alphanumeric_weights[1..]));
        _ = a.variableChar(0, 0, &alphanumeric_weights);
    }
    try a.pegSkip();
}

test AstSmith {
    try std.testing.fuzz({}, checkGenerated, .{});
}

fn checkGenerated(_: void, smith: *Smith) !void {
    var a: AstSmith = .init(smith);
    try a.generateSource();

    { // Check tokenization matches source
        errdefer a.logBadSource(null);

        const token_tags = a.token_tag_buf[0..a.tokens_len];
        const token_starts = a.token_start_buf[0..a.tokens_len];
        try std.testing.expectEqual(Token.Tag.eof, token_tags[token_tags.len - 1]);

        var tokenizer: std.zig.Tokenizer = .init(a.source());
        for (token_tags, token_starts) |tag, start| {
            const tok = tokenizer.next();
            try std.testing.expectEqual(tok.tag, tag);
            try std.testing.expectEqual(tok.loc.start, start);
            if (tag == .invalid) return error.InvalidToken;
        }
    }

    var fba_buf: [1 << 18]u8 = undefined;
    var fba: std.heap.FixedBufferAllocator = .init(&fba_buf);
    const ast = std.zig.Ast.parseTokens(fba.allocator(), a.source(), a.tokens(), .zig) catch
        return error.SkipZigTest;

    errdefer a.logBadSource(ast);
    try std.testing.expectEqual(0, ast.errors.len);
}

fn logBadSource(a: *AstSmith, ast: ?std.zig.Ast) void {
    var buf: [256]u8 = undefined;
    const ls = std.debug.lockStderr(&buf);
    defer std.debug.unlockStderr();
    a.logBadSourceInner(ls.terminal(), ast) catch {};
}

fn logBadSourceInner(a: *AstSmith, t: std.Io.Terminal, ast: ?std.zig.Ast) std.Io.Writer.Error!void {
    try a.logSourceInner(t);
    const w = t.writer;

    if (ast) |bad_ast| {
        try w.writeAll("=== Parse Errors ===\n");
        for (bad_ast.errors) |err| {
            const loc = bad_ast.tokenLocation(0, err.token);
            try w.print("{}:{}: ", .{ loc.line + 1, loc.column + 1 });
            try bad_ast.renderError(err, w);
            try w.writeByte('\n');
        }
    } else {
        t.setColor(.dim) catch {};
        try w.writeAll("=== Tokens ===\n");
        t.setColor(.reset) catch {};
        for (
            0..,
            a.token_tag_buf[0..a.tokens_len],
            a.token_start_buf[0..a.tokens_len],
        ) |i, tag, start| {
            try w.print("#{} @{}: {t}\n", .{ i, start, tag });
        }

        t.setColor(.dim) catch {};
        try w.writeAll("\n=== Expected Tokens ===\n");
        t.setColor(.reset) catch {};

        var tokenizer: std.zig.Tokenizer = .init(a.source());
        var i: usize = 0;
        while (true) {
            const tok = tokenizer.next();
            try w.print("#{} @{}-{}: {t}\n", .{ i, tok.loc.start, tok.loc.end, tok.tag });
            i += 1;
            if (tok.tag == .invalid or tok.tag == .eof) break;
        }
    }
}

pub fn logSource(a: *AstSmith) void {
    var buf: [256]u8 = undefined;
    const ls = std.debug.lockStderr(&buf);
    defer std.debug.unlockStderr();
    a.logSourceInner(ls.terminal()) catch {};
}

fn logSourceInner(a: *AstSmith, t: std.Io.Terminal) std.Io.Writer.Error!void {
    const w = t.writer;

    t.setColor(.dim) catch {};
    try w.writeAll("=== Source ===\n");
    t.setColor(.reset) catch {};

    var line: usize = 1;
    try w.print("{: >5} ", .{line});
    for (a.source()) |c| switch (c) {
        ' '...0x7e => try w.writeByte(c),
        '\n' => {
            line += 1;
            try w.print("\n{: >5} ", .{line});
        },
        '\r' => {
            t.setColor(.cyan) catch {};
            try w.writeAll("\\r");
            t.setColor(.reset) catch {};
        },
        '\t' => {
            t.setColor(.cyan) catch {};
            try w.writeAll("\\t");
            t.setColor(.reset) catch {};
        },
        else => {
            t.setColor(.cyan) catch {};
            try w.print("\\x{x:0>2}", .{c});
            t.setColor(.reset) catch {};
        },
    };
    try w.writeByte('\n');
}



---
File: /std/zig/BuiltinFn.zig
---

pub const Tag = enum {
    add_with_overflow,
    addrspace_cast,
    align_cast,
    align_of,
    as,
    atomic_load,
    atomic_rmw,
    atomic_store,
    bit_cast,
    bit_offset_of,
    int_from_bool,
    bit_size_of,
    branch_hint,
    breakpoint,
    disable_instrumentation,
    disable_intrinsics,
    mul_add,
    byte_swap,
    bit_reverse,
    offset_of,
    call,
    c_define,
    c_import,
    c_include,
    clz,
    cmpxchg_strong,
    cmpxchg_weak,
    compile_error,
    compile_log,
    const_cast,
    ctz,
    c_undef,
    c_va_arg,
    c_va_copy,
    c_va_end,
    c_va_start,
    div_exact,
    div_floor,
    div_trunc,
    embed_file,
    int_from_enum,
    error_name,
    error_return_trace,
    int_from_error,
    error_cast,
    @"export",
    @"extern",
    field,
    field_parent_ptr,
    FieldType,
    float_cast,
    int_from_float,
    frame,
    Frame,
    frame_address,
    has_decl,
    has_field,
    import,
    in_comptime,
    int_cast,
    enum_from_int,
    error_from_int,
    float_from_int,
    ptr_from_int,
    max,
    memcpy,
    memset,
    memmove,
    min,
    wasm_memory_size,
    wasm_memory_grow,
    mod,
    mul_with_overflow,
    panic,
    pop_count,
    prefetch,
    ptr_cast,
    int_from_ptr,
    rem,
    return_address,
    select,
    set_eval_branch_quota,
    set_float_mode,
    set_runtime_safety,
    shl_exact,
    shl_with_overflow,
    shr_exact,
    shuffle,
    size_of,
    splat,
    reduce,
    src,
    sqrt,
    sin,
    cos,
    tan,
    exp,
    exp2,
    log,
    log2,
    log10,
    abs,
    floor,
    ceil,
    trunc,
    round,
    sub_with_overflow,
    tag_name,
    This,
    trap,
    truncate,
    EnumLiteral,
    Int,
    Tuple,
    Pointer,
    Fn,
    Struct,
    Union,
    Enum,
    type_info,
    type_name,
    TypeOf,
    union_init,
    Vector,
    volatile_cast,
    work_item_id,
    work_group_size,
    work_group_id,
};

pub const EvalToError = enum {
    /// The builtin cannot possibly evaluate to an error.
    never,
    /// The builtin will always evaluate to an error.
    always,
    /// The builtin may or may not evaluate to an error depending on the parameters.
    maybe,
};

tag: Tag,

/// Info about the builtin call's possibility of returning an error.
eval_to_error: EvalToError = .never,
/// `true` if the builtin call can be the left-hand side of an expression (assigned to).
allows_lvalue: bool = false,
/// `true` if builtin call is not available outside function scope
illegal_outside_function: bool = false,
/// The number of parameters to this builtin function. `null` means variable number
/// of parameters.
param_count: ?u8,

pub const list = list: {
    @setEvalBranchQuota(3000);
    break :list std.StaticStringMap(BuiltinFn).initComptime([_]struct { []const u8, BuiltinFn }{
        .{
            "@addWithOverflow",
            .{
                .tag = .add_with_overflow,
                .param_count = 2,
            },
        },
        .{
            "@addrSpaceCast",
            .{
                .tag = .addrspace_cast,
                .param_count = 1,
            },
        },
        .{
            "@alignCast",
            .{
                .tag = .align_cast,
                .param_count = 1,
            },
        },
        .{
            "@alignOf",
            .{
                .tag = .align_of,
                .param_count = 1,
            },
        },
        .{
            "@as",
            .{
                .tag = .as,
                .eval_to_error = .maybe,
                .param_count = 2,
            },
        },
        .{
            "@atomicLoad",
            .{
                .tag = .atomic_load,
                .param_count = 3,
            },
        },
        .{
            "@atomicRmw",
            .{
                .tag = .atomic_rmw,
                .param_count = 5,
            },
        },
        .{
            "@atomicStore",
            .{
                .tag = .atomic_store,
                .param_count = 4,
            },
        },
        .{
            "@bitCast",
            .{
                .tag = .bit_cast,
                .param_count = 1,
            },
        },
        .{
            "@bitOffsetOf",
            .{
                .tag = .bit_offset_of,
                .param_count = 2,
            },
        },
        .{
            "@intFromBool",
            .{
                .tag = .int_from_bool,
                .param_count = 1,
            },
        },
        .{
            "@bitSizeOf",
            .{
                .tag = .bit_size_of,
                .param_count = 1,
            },
        },
        .{
            "@branchHint",
            .{
                .tag = .branch_hint,
                .param_count = 1,
                .illegal_outside_function = true,
            },
        },
        .{
            "@breakpoint",
            .{
                .tag = .breakpoint,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@disableInstrumentation",
            .{
                .tag = .disable_instrumentation,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@disableIntrinsics",
            .{
                .tag = .disable_intrinsics,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@mulAdd",
            .{
                .tag = .mul_add,
                .param_count = 4,
            },
        },
        .{
            "@byteSwap",
            .{
                .tag = .byte_swap,
                .param_count = 1,
            },
        },
        .{
            "@bitReverse",
            .{
                .tag = .bit_reverse,
                .param_count = 1,
            },
        },
        .{
            "@offsetOf",
            .{
                .tag = .offset_of,
                .param_count = 2,
            },
        },
        .{
            "@call",
            .{
                .tag = .call,
                .eval_to_error = .maybe,
                .param_count = 3,
            },
        },
        .{
            "@cDefine",
            .{
                .tag = .c_define,
                .param_count = 2,
            },
        },
        .{
            "@cImport",
            .{
                .tag = .c_import,
                .param_count = 1,
            },
        },
        .{
            "@cInclude",
            .{
                .tag = .c_include,
                .param_count = 1,
            },
        },
        .{
            "@clz",
            .{
                .tag = .clz,
                .param_count = 1,
            },
        },
        .{
            "@cmpxchgStrong",
            .{
                .tag = .cmpxchg_strong,
                .param_count = 6,
            },
        },
        .{
            "@cmpxchgWeak",
            .{
                .tag = .cmpxchg_weak,
                .param_count = 6,
            },
        },
        .{
            "@compileError",
            .{
                .tag = .compile_error,
                .param_count = 1,
            },
        },
        .{
            "@compileLog",
            .{
                .tag = .compile_log,
                .param_count = null,
            },
        },
        .{
            "@constCast",
            .{
                .tag = .const_cast,
                .param_count = 1,
            },
        },
        .{
            "@ctz",
            .{
                .tag = .ctz,
                .param_count = 1,
            },
        },
        .{
            "@cUndef",
            .{
                .tag = .c_undef,
                .param_count = 1,
            },
        },
        .{
            "@cVaArg",
            .{
                .tag = .c_va_arg,
                .param_count = 2,
                .illegal_outside_function = true,
            },
        },
        .{
            "@cVaCopy",
            .{
                .tag = .c_va_copy,
                .param_count = 1,
                .illegal_outside_function = true,
            },
        },
        .{
            "@cVaEnd",
            .{
                .tag = .c_va_end,
                .param_count = 1,
                .illegal_outside_function = true,
            },
        },
        .{
            "@cVaStart",
            .{
                .tag = .c_va_start,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@divExact",
            .{
                .tag = .div_exact,
                .param_count = 2,
            },
        },
        .{
            "@divFloor",
            .{
                .tag = .div_floor,
                .param_count = 2,
            },
        },
        .{
            "@divTrunc",
            .{
                .tag = .div_trunc,
                .param_count = 2,
            },
        },
        .{
            "@embedFile",
            .{
                .tag = .embed_file,
                .param_count = 1,
            },
        },
        .{
            "@intFromEnum",
            .{
                .tag = .int_from_enum,
                .param_count = 1,
            },
        },
        .{
            "@errorName",
            .{
                .tag = .error_name,
                .param_count = 1,
            },
        },
        .{
            "@errorReturnTrace",
            .{
                .tag = .error_return_trace,
                .param_count = 0,
            },
        },
        .{
            "@intFromError",
            .{
                .tag = .int_from_error,
                .param_count = 1,
            },
        },
        .{
            "@errorCast",
            .{
                .tag = .error_cast,
                .eval_to_error = .maybe,
                .param_count = 1,
            },
        },
        .{
            "@export",
            .{
                .tag = .@"export",
                .param_count = 2,
            },
        },
        .{
            "@extern",
            .{
                .tag = .@"extern",
                .param_count = 2,
            },
        },
        .{
            "@field",
            .{
                .tag = .field,
                .eval_to_error = .maybe,
                .param_count = 2,
                .allows_lvalue = true,
            },
        },
        .{
            "@fieldParentPtr",
            .{
                .tag = .field_parent_ptr,
                .param_count = 2,
            },
        },
        .{
            "@FieldType",
            .{
                .tag = .FieldType,
                .param_count = 2,
            },
        },
        .{
            "@floatCast",
            .{
                .tag = .float_cast,
                .param_count = 1,
            },
        },
        .{
            "@intFromFloat",
            .{
                .tag = .int_from_float,
                .param_count = 1,
            },
        },
        .{
            "@frame",
            .{
                .tag = .frame,
                .param_count = 0,
            },
        },
        .{
            "@Frame",
            .{
                .tag = .Frame,
                .param_count = 1,
            },
        },
        .{
            "@frameAddress",
            .{
                .tag = .frame_address,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@hasDecl",
            .{
                .tag = .has_decl,
                .param_count = 2,
            },
        },
        .{
            "@hasField",
            .{
                .tag = .has_field,
                .param_count = 2,
            },
        },
        .{
            "@import",
            .{
                .tag = .import,
                .param_count = 1,
            },
        },
        .{
            "@inComptime",
            .{
                .tag = .in_comptime,
                .param_count = 0,
            },
        },
        .{
            "@intCast",
            .{
                .tag = .int_cast,
                .param_count = 1,
            },
        },
        .{
            "@enumFromInt",
            .{
                .tag = .enum_from_int,
                .param_count = 1,
            },
        },
        .{
            "@errorFromInt",
            .{
                .tag = .error_from_int,
                .eval_to_error = .always,
                .param_count = 1,
            },
        },
        .{
            "@floatFromInt",
            .{
                .tag = .float_from_int,
                .param_count = 1,
            },
        },
        .{
            "@ptrFromInt",
            .{
                .tag = .ptr_from_int,
                .param_count = 1,
            },
        },
        .{
            "@max",
            .{
                .tag = .max,
                .param_count = null,
            },
        },
        .{
            "@memcpy",
            .{
                .tag = .memcpy,
                .param_count = 2,
            },
        },
        .{
            "@memset",
            .{
                .tag = .memset,
                .param_count = 2,
            },
        },
        .{
            "@memmove",
            .{
                .tag = .memmove,
                .param_count = 2,
            },
        },
        .{
            "@min",
            .{
                .tag = .min,
                .param_count = null,
            },
        },
        .{
            "@wasmMemorySize",
            .{
                .tag = .wasm_memory_size,
                .param_count = 1,
            },
        },
        .{
            "@wasmMemoryGrow",
            .{
                .tag = .wasm_memory_grow,
                .param_count = 2,
            },
        },
        .{
            "@mod",
            .{
                .tag = .mod,
                .param_count = 2,
            },
        },
        .{
            "@mulWithOverflow",
            .{
                .tag = .mul_with_overflow,
                .param_count = 2,
            },
        },
        .{
            "@panic",
            .{
                .tag = .panic,
                .param_count = 1,
            },
        },
        .{
            "@popCount",
            .{
                .tag = .pop_count,
                .param_count = 1,
            },
        },
        .{
            "@prefetch",
            .{
                .tag = .prefetch,
                .param_count = 2,
            },
        },
        .{
            "@ptrCast",
            .{
                .tag = .ptr_cast,
                .param_count = 1,
            },
        },
        .{
            "@intFromPtr",
            .{
                .tag = .int_from_ptr,
                .param_count = 1,
            },
        },
        .{
            "@rem",
            .{
                .tag = .rem,
                .param_count = 2,
            },
        },
        .{
            "@returnAddress",
            .{
                .tag = .return_address,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@select",
            .{
                .tag = .select,
                .param_count = 4,
            },
        },
        .{
            "@setEvalBranchQuota",
            .{
                .tag = .set_eval_branch_quota,
                .param_count = 1,
            },
        },
        .{
            "@setFloatMode",
            .{
                .tag = .set_float_mode,
                .param_count = 1,
            },
        },
        .{
            "@setRuntimeSafety",
            .{
                .tag = .set_runtime_safety,
                .param_count = 1,
            },
        },
        .{
            "@shlExact",
            .{
                .tag = .shl_exact,
                .param_count = 2,
            },
        },
        .{
            "@shlWithOverflow",
            .{
                .tag = .shl_with_overflow,
                .param_count = 2,
            },
        },
        .{
            "@shrExact",
            .{
                .tag = .shr_exact,
                .param_count = 2,
            },
        },
        .{
            "@shuffle",
            .{
                .tag = .shuffle,
                .param_count = 4,
            },
        },
        .{
            "@sizeOf",
            .{
                .tag = .size_of,
                .param_count = 1,
            },
        },
        .{
            "@splat",
            .{
                .tag = .splat,
                .param_count = 1,
            },
        },
        .{
            "@reduce",
            .{
                .tag = .reduce,
                .param_count = 2,
            },
        },
        .{
            "@src",
            .{
                .tag = .src,
                .param_count = 0,
                .illegal_outside_function = true,
            },
        },
        .{
            "@sqrt",
            .{
                .tag = .sqrt,
                .param_count = 1,
            },
        },
        .{
            "@sin",
            .{
                .tag = .sin,
                .param_count = 1,
            },
        },
        .{
            "@cos",
            .{
                .tag = .cos,
                .param_count = 1,
            },
        },
        .{
            "@tan",
            .{
                .tag = .tan,
                .param_count = 1,
            },
        },
        .{
            "@exp",
            .{
                .tag = .exp,
                .param_count = 1,
            },
        },
        .{
            "@exp2",
            .{
                .tag = .exp2,
                .param_count = 1,
            },
        },
        .{
            "@log",
            .{
                .tag = .log,
                .param_count = 1,
            },
        },
        .{
            "@log2",
            .{
                .tag = .log2,
                .param_count = 1,
            },
        },
        .{
            "@log10",
            .{
                .tag = .log10,
                .param_count = 1,
            },
        },
        .{
            "@abs",
            .{
                .tag = .abs,
                .param_count = 1,
            },
        },
        .{
            "@floor",
            .{
                .tag = .floor,
                .param_count = 1,
            },
        },
        .{
            "@ceil",
            .{
                .tag = .ceil,
                .param_count = 1,
            },
        },
        .{
            "@trunc",
            .{
                .tag = .trunc,
                .param_count = 1,
            },
        },
        .{
            "@round",
            .{
                .tag = .round,
                .param_count = 1,
            },
        },
        .{
            "@subWithOverflow",
            .{
                .tag = .sub_with_overflow,
                .param_count = 2,
            },
        },
        .{
            "@tagName",
            .{
                .tag = .tag_name,
                .param_count = 1,
            },
        },
        .{
            "@This",
            .{
                .tag = .This,
                .param_count = 0,
            },
        },
        .{
            "@trap",
            .{
                .tag = .trap,
                .param_count = 0,
            },
        },
        .{
            "@truncate",
            .{
                .tag = .truncate,
                .param_count = 1,
            },
        },
        .{
            "@EnumLiteral",
            .{
                .tag = .EnumLiteral,
                .param_count = 0,
            },
        },
        .{
            "@Int",
            .{
                .tag = .Int,
                .param_count = 2,
            },
        },
        .{
            "@Tuple",
            .{
                .tag = .Tuple,
                .param_count = 1,
            },
        },
        .{
            "@Pointer",
            .{
                .tag = .Pointer,
                .param_count = 4,
            },
        },
        .{
            "@Fn",
            .{
                .tag = .Fn,
                .param_count = 4,
            },
        },
        .{
            "@Struct",
            .{
                .tag = .Struct,
                .param_count = 5,
            },
        },
        .{
            "@Union",
            .{
                .tag = .Union,
                .param_count = 5,
            },
        },
        .{
            "@Enum",
            .{
                .tag = .Enum,
                .param_count = 4,
            },
        },
        .{
            "@typeInfo",
            .{
                .tag = .type_info,
                .param_count = 1,
            },
        },
        .{
            "@typeName",
            .{
                .tag = .type_name,
                .param_count = 1,
            },
        },
        .{
            "@TypeOf",
            .{
                .tag = .TypeOf,
                .param_count = null,
            },
        },
        .{
            "@unionInit",
            .{
                .tag = .union_init,
                .param_count = 3,
            },
        },
        .{
            "@Vector",
            .{
                .tag = .Vector,
                .param_count = 2,
            },
        },
        .{
            "@volatileCast",
            .{
                .tag = .volatile_cast,
                .param_count = 1,
            },
        },
        .{
            "@workItemId",
            .{
                .tag = .work_item_id,
                .param_count = 1,
                .illegal_outside_function = true,
            },
        },
        .{
            "@workGroupSize",
            .{
                .tag = .work_group_size,
                .param_count = 1,
                .illegal_outside_function = true,
            },
        },
        .{
            "@workGroupId",
            .{
                .tag = .work_group_id,
                .param_count = 1,
                .illegal_outside_function = true,
            },
        },
    });
};

const std = @import("std");
const BuiltinFn = @This();



---
File: /std/zig/Client.zig
---

pub const Message = struct {
    pub const Header = extern struct {
        tag: Tag,
        /// Size of the body only; does not include this Header.
        bytes_len: u32,
    };

    pub const Tag = enum(u32) {
        /// Tells the compiler to shut down cleanly.
        /// No body.
        exit,
        /// Tells the compiler to detect changes in source files and update the
        /// affected output compilation artifacts.
        /// If one of the compilation artifacts is an executable that is
        /// running as a child process, the compiler will wait for it to exit
        /// before performing the update.
        /// No body.
        update,
        /// Tells the compiler to execute the executable as a child process.
        /// No body.
        run,
        /// Tells the compiler to detect changes in source files and update the
        /// affected output compilation artifacts.
        /// If one of the compilation artifacts is an executable that is
        /// running as a child process, the compiler will perform a hot code
        /// swap.
        /// No body.
        hot_update,
        /// Ask the test runner for metadata about all the unit tests that can
        /// be run. Server will respond with a `test_metadata` message.
        /// No body.
        query_test_metadata,
        /// Ask the test runner to run a particular test.
        /// The message body is a u32 test index.
        run_test,
        /// Ask the test runner to start fuzzing a set of test forever or each for a given amount of
        /// iterations. After this is sent, the only allowed message is `new_fuzz_input`.
        ///
        /// The message body is:
        /// - a u8 test limit kind (std.Build.api.fuzz.LimitKind)
        /// - a u64 value whose meaning depends on FuzzLimitKind (either a limit amount or an instance id)
        /// - a u32 number of tests followed by n elements of
        ///   - a u32 test name len.
        ///   - a test name with the above length
        start_fuzzing,
        /// The message body has the same format as in Server.
        new_fuzz_input,

        _,
    };

    comptime {
        const std = @import("std");
        std.debug.assert(@sizeOf(std.Build.abi.fuzz.LimitKind) == 1);
    }
};



---
File: /std/zig/ErrorBundle.zig
---

//! To support incremental compilation, errors are stored in various places
//! so that they can be created and destroyed appropriately. This structure
//! is used to collect all the errors from the various places into one
//! convenient place for API users to consume.
//!
//! There is one special encoding for this data structure. If both arrays are
//! empty, it means there are no errors. This special encoding exists so that
//! heap allocation is not needed in the common case of no errors.
const ErrorBundle = @This();

const std = @import("std");
const Io = std.Io;
const Writer = std.Io.Writer;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

string_bytes: []const u8,
/// The first thing in this array is an `ErrorMessageList`.
extra: []const u32,

/// Index into `string_bytes`.
pub const String = u32;
/// Index into `string_bytes`, or null.
pub const OptionalString = u32;

/// Special encoding when there are no errors.
pub const empty: ErrorBundle = .{
    .string_bytes = &.{},
    .extra = &.{},
};

// An index into `extra` pointing at an `ErrorMessage`.
pub const MessageIndex = enum(u32) {
    _,
};

// An index into `extra` pointing at an `SourceLocation`.
pub const SourceLocationIndex = enum(u32) {
    none = 0,
    _,
};

/// There will be a MessageIndex for each len at start.
pub const ErrorMessageList = struct {
    len: u32,
    start: u32,
    /// null-terminated string index. 0 means no compile log text.
    compile_log_text: OptionalString,
};

/// Trailing:
/// * ReferenceTrace for each reference_trace_len
pub const SourceLocation = struct {
    src_path: String,
    line: u32,
    column: u32,
    /// byte offset of starting token
    span_start: u32,
    /// byte offset of main error location
    span_main: u32,
    /// byte offset of end of last token
    span_end: u32,
    /// Does not include the trailing newline.
    source_line: OptionalString = 0,
    reference_trace_len: u32 = 0,
};

/// Trailing:
/// * MessageIndex for each notes_len.
pub const ErrorMessage = struct {
    msg: String,
    /// Usually one, but incremented for redundant messages.
    count: u32 = 1,
    src_loc: SourceLocationIndex = .none,
    notes_len: u32 = 0,
};

pub const ReferenceTrace = struct {
    /// null terminated string index
    /// Except for the sentinel ReferenceTrace element, in which case:
    /// * 0 means remaining references hidden
    /// * >0 means N references hidden
    decl_name: String,
    /// Index into extra of a SourceLocation
    /// If this is 0, this is the sentinel ReferenceTrace element.
    src_loc: SourceLocationIndex,
};

pub fn deinit(eb: *ErrorBundle, gpa: Allocator) void {
    gpa.free(eb.string_bytes);
    gpa.free(eb.extra);
    eb.* = undefined;
}

pub fn errorMessageCount(eb: ErrorBundle) u32 {
    if (eb.extra.len == 0) return 0;
    return eb.getErrorMessageList().len;
}

pub fn getErrorMessageList(eb: ErrorBundle) ErrorMessageList {
    return eb.extraData(ErrorMessageList, 0).data;
}

pub fn getMessages(eb: ErrorBundle) []const MessageIndex {
    const list = eb.getErrorMessageList();
    return @as([]const MessageIndex, @ptrCast(eb.extra[list.start..][0..list.len]));
}

pub fn getErrorMessage(eb: ErrorBundle, index: MessageIndex) ErrorMessage {
    return eb.extraData(ErrorMessage, @intFromEnum(index)).data;
}

pub fn getSourceLocation(eb: ErrorBundle, index: SourceLocationIndex) SourceLocation {
    assert(index != .none);
    return eb.extraData(SourceLocation, @intFromEnum(index)).data;
}

pub fn getNotes(eb: ErrorBundle, index: MessageIndex) []const MessageIndex {
    const notes_len = eb.getErrorMessage(index).notes_len;
    const start = @intFromEnum(index) + @typeInfo(ErrorMessage).@"struct".fields.len;
    return @as([]const MessageIndex, @ptrCast(eb.extra[start..][0..notes_len]));
}

pub fn getCompileLogOutput(eb: ErrorBundle) [:0]const u8 {
    return nullTerminatedString(eb, getErrorMessageList(eb).compile_log_text);
}

/// Returns the requested data, as well as the new index which is at the start of the
/// trailers for the object.
fn extraData(eb: ErrorBundle, comptime T: type, index: usize) struct { data: T, end: usize } {
    const fields = @typeInfo(T).@"struct".fields;
    var i: usize = index;
    var result: T = undefined;
    inline for (fields) |field| {
        @field(result, field.name) = switch (field.type) {
            u32 => eb.extra[i],
            MessageIndex => @as(MessageIndex, @enumFromInt(eb.extra[i])),
            SourceLocationIndex => @as(SourceLocationIndex, @enumFromInt(eb.extra[i])),
            else => @compileError("bad field type"),
        };
        i += 1;
    }
    return .{
        .data = result,
        .end = i,
    };
}

/// Given an index into `string_bytes` returns the null-terminated string found there.
pub fn nullTerminatedString(eb: ErrorBundle, index: String) [:0]const u8 {
    const string_bytes = eb.string_bytes;
    var end: usize = index;
    while (string_bytes[end] != 0) {
        end += 1;
    }
    return string_bytes[index..end :0];
}

pub const RenderOptions = struct {
    include_reference_trace: bool = true,
    include_source_line: bool = true,
    include_log_text: bool = true,
};

pub const RenderToStderrError = Io.Cancelable || Io.File.Writer.Error;

pub fn renderToStderr(eb: ErrorBundle, io: Io, options: RenderOptions, color: std.zig.Color) RenderToStderrError!void {
    var buffer: [256]u8 = undefined;
    const stderr = try io.lockStderr(&buffer, color.terminalMode());
    defer io.unlockStderr();
    renderToTerminal(eb, options, stderr.terminal()) catch |err| switch (err) {
        error.WriteFailed => return stderr.file_writer.err.?,
        else => |e| return e,
    };
}

pub fn renderToWriter(eb: ErrorBundle, options: RenderOptions, w: *Writer) Writer.Error!void {
    return renderToTerminal(eb, options, .{ .writer = w, .mode = .no_color }) catch |err| switch (err) {
        error.WriteFailed => |e| return e,
        else => unreachable,
    };
}

pub fn renderToTerminal(eb: ErrorBundle, options: RenderOptions, t: Io.Terminal) Io.Terminal.SetColorError!void {
    if (eb.extra.len == 0) return;
    for (eb.getMessages()) |err_msg| {
        try renderErrorMessage(eb, options, err_msg, t, "error", .red, 0);
    }

    if (options.include_log_text) {
        const log_text = eb.getCompileLogOutput();
        if (log_text.len != 0) {
            try t.writer.writeAll("\nCompile Log Output:\n");
            try t.writer.writeAll(log_text);
        }
    }
}

fn renderErrorMessage(
    eb: ErrorBundle,
    options: RenderOptions,
    err_msg_index: MessageIndex,
    t: Io.Terminal,
    kind: []const u8,
    color: Io.Terminal.Color,
    indent: usize,
) Io.Terminal.SetColorError!void {
    const w = t.writer;
    const err_msg = eb.getErrorMessage(err_msg_index);
    if (err_msg.src_loc != .none) {
        const src = eb.extraData(SourceLocation, @intFromEnum(err_msg.src_loc));
        var prefix: Writer.Discarding = .init(&.{});
        try w.splatByteAll(' ', indent);
        prefix.count += indent;
        try t.setColor(.bold);
        try w.print("{s}:{d}:{d}: ", .{
            eb.nullTerminatedString(src.data.src_path),
            src.data.line + 1,
            src.data.column + 1,
        });
        try prefix.writer.print("{s}:{d}:{d}: ", .{
            eb.nullTerminatedString(src.data.src_path),
            src.data.line + 1,
            src.data.column + 1,
        });
        try t.setColor(color);
        try w.writeAll(kind);
        prefix.count += kind.len;
        try w.writeAll(": ");
        prefix.count += 2;
        // This is the length of the part before the error message:
        // e.g. "file.zig:4:5: error: "
        const prefix_len: usize = @intCast(prefix.count);
        try t.setColor(.reset);
        try t.setColor(.bold);
        if (err_msg.count == 1) {
            try writeMsg(eb, err_msg, w, prefix_len);
            try w.writeByte('\n');
        } else {
            try writeMsg(eb, err_msg, w, prefix_len);
            try t.setColor(.dim);
            try w.print(" ({d} times)\n", .{err_msg.count});
        }
        try t.setColor(.reset);
        if (src.data.source_line != 0 and options.include_source_line) {
            try w.splatByteAll(' ', indent);
            const line = eb.nullTerminatedString(src.data.source_line);
            for (line) |b| switch (b) {
                '\t' => try w.writeByte(' '),
                else => try w.writeByte(b),
            };
            try w.writeByte('\n');
            try w.splatByteAll(' ', indent);
            // TODO basic unicode code point monospace width
            const before_caret = src.data.span_main - src.data.span_start;
            // -1 since span.main includes the caret
            const after_caret = src.data.span_end -| src.data.span_main -| 1;
            try w.splatByteAll(' ', src.data.column - before_caret);
            try t.setColor(.green);
            try w.splatByteAll('~', before_caret);
            try w.writeByte('^');
            try w.splatByteAll('~', after_caret);
            try w.writeByte('\n');
            try t.setColor(.reset);
        }
        for (eb.getNotes(err_msg_index)) |note| {
            try renderErrorMessage(eb, options, note, t, "note", .cyan, indent);
        }
        if (src.data.reference_trace_len > 0 and options.include_reference_trace) {
            try t.setColor(.reset);
            try t.setColor(.dim);
            try w.splatByteAll(' ', indent);
            try w.print("referenced by:\n", .{});
            var ref_index = src.end;
            for (0..src.data.reference_trace_len) |_| {
                const ref_trace = eb.extraData(ReferenceTrace, ref_index);
                ref_index = ref_trace.end;
                try w.splatByteAll(' ', indent);
                if (ref_trace.data.src_loc != .none) {
                    const ref_src = eb.getSourceLocation(ref_trace.data.src_loc);
                    try w.print("    {s}: {s}:{d}:{d}\n", .{
                        eb.nullTerminatedString(ref_trace.data.decl_name),
                        eb.nullTerminatedString(ref_src.src_path),
                        ref_src.line + 1,
                        ref_src.column + 1,
                    });
                } else if (ref_trace.data.decl_name != 0) {
                    const count = ref_trace.data.decl_name;
                    try w.print(
                        "    {d} reference(s) hidden; use '-freference-trace={d}' to see all references\n",
                        .{ count, count + src.data.reference_trace_len - 1 },
                    );
                } else {
                    try w.print(
                        "    remaining reference traces hidden; use '-freference-trace' to see all reference traces\n",
                        .{},
                    );
                }
            }
            try t.setColor(.reset);
        }
    } else {
        try t.setColor(color);
        try w.splatByteAll(' ', indent);
        try w.writeAll(kind);
        try w.writeAll(": ");
        try t.setColor(.reset);
        const msg = eb.nullTerminatedString(err_msg.msg);
        if (err_msg.count == 1) {
            try w.print("{s}\n", .{msg});
        } else {
            try w.print("{s}", .{msg});
            try t.setColor(.dim);
            try w.print(" ({d} times)\n", .{err_msg.count});
        }
        try t.setColor(.reset);
        for (eb.getNotes(err_msg_index)) |note| {
            try renderErrorMessage(eb, options, note, t, "note", .cyan, indent + 4);
        }
    }
}

/// Splits the error message up into lines to properly indent them
/// to allow for long, good-looking error messages.
///
/// This is used to split the message in `@compileError("hello\nworld")` for example.
fn writeMsg(eb: ErrorBundle, err_msg: ErrorMessage, w: *Writer, indent: usize) !void {
    var lines = std.mem.splitScalar(u8, eb.nullTerminatedString(err_msg.msg), '\n');
    while (lines.next()) |line| {
        try w.writeAll(line);
        if (lines.index == null) break;
        try w.writeByte('\n');
        try w.splatByteAll(' ', indent);
    }
}

pub const Wip = struct {
    gpa: Allocator,
    string_bytes: std.ArrayList(u8),
    /// The first thing in this array is a ErrorMessageList.
    extra: std.ArrayList(u32),
    root_list: std.ArrayList(MessageIndex),

    pub fn init(wip: *Wip, gpa: Allocator) !void {
        wip.* = .{
            .gpa = gpa,
            .string_bytes = .empty,
            .extra = .empty,
            .root_list = .empty,
        };

        // So that 0 can be used to indicate a null string.
        try wip.string_bytes.append(gpa, 0);

        assert(0 == try addExtra(wip, ErrorMessageList{
            .len = 0,
            .start = 0,
            .compile_log_text = 0,
        }));
    }

    pub fn deinit(wip: *Wip) void {
        const gpa = wip.gpa;
        wip.root_list.deinit(gpa);
        wip.string_bytes.deinit(gpa);
        wip.extra.deinit(gpa);
        wip.* = undefined;
    }

    pub fn toOwnedBundle(wip: *Wip, compile_log_text: []const u8) !ErrorBundle {
        const gpa = wip.gpa;
        if (wip.root_list.items.len == 0) {
            assert(compile_log_text.len == 0);
            // Special encoding when there are no errors.
            wip.deinit();
            wip.* = .{
                .gpa = gpa,
                .string_bytes = .empty,
                .extra = .empty,
                .root_list = .empty,
            };
            return empty;
        }

        const compile_log_str_index = if (compile_log_text.len == 0) 0 else str: {
            const str: u32 = @intCast(wip.string_bytes.items.len);
            try wip.string_bytes.ensureUnusedCapacity(gpa, compile_log_text.len + 1);
            wip.string_bytes.appendSliceAssumeCapacity(compile_log_text);
            wip.string_bytes.appendAssumeCapacity(0);
            break :str str;
        };

        wip.setExtra(0, ErrorMessageList{
            .len = @intCast(wip.root_list.items.len),
            .start = @intCast(wip.extra.items.len),
            .compile_log_text = compile_log_str_index,
        });
        try wip.extra.appendSlice(gpa, @as([]const u32, @ptrCast(wip.root_list.items)));
        wip.root_list.clearAndFree(gpa);
        return .{
            .string_bytes = try wip.string_bytes.toOwnedSlice(gpa),
            .extra = try wip.extra.toOwnedSlice(gpa),
        };
    }

    pub fn tmpBundle(wip: Wip) ErrorBundle {
        return .{
            .string_bytes = wip.string_bytes.items,
            .extra = wip.extra.items,
        };
    }

    pub fn addString(wip: *Wip, s: []const u8) Allocator.Error!String {
        const gpa = wip.gpa;
        const index: String = @intCast(wip.string_bytes.items.len);
        try wip.string_bytes.ensureUnusedCapacity(gpa, s.len + 1);
        wip.string_bytes.appendSliceAssumeCapacity(s);
        wip.string_bytes.appendAssumeCapacity(0);
        return index;
    }

    pub fn printString(wip: *Wip, comptime fmt: []const u8, args: anytype) Allocator.Error!String {
        const gpa = wip.gpa;
        const index: String = @intCast(wip.string_bytes.items.len);
        try wip.string_bytes.print(gpa, fmt, args);
        try wip.string_bytes.append(gpa, 0);
        return index;
    }

    pub fn addRootErrorMessage(wip: *Wip, em: ErrorMessage) Allocator.Error!void {
        try wip.root_list.ensureUnusedCapacity(wip.gpa, 1);
        wip.root_list.appendAssumeCapacity(try addErrorMessage(wip, em));
    }

    pub fn addRootErrorMessageWithNotes(
        wip: *Wip,
        msg: ErrorMessage,
        notes: []const ErrorMessage,
    ) !void {
        try wip.addRootErrorMessage(msg);
        const notes_start = try wip.reserveNotes(@intCast(notes.len));
        for (notes_start.., notes) |i, note| {
            wip.extra.items[i] = @intFromEnum(wip.addErrorMessageAssumeCapacity(note));
        }
    }

    pub fn addErrorMessage(wip: *Wip, em: ErrorMessage) Allocator.Error!MessageIndex {
        return @enumFromInt(try addExtra(wip, em));
    }

    pub fn addErrorMessageAssumeCapacity(wip: *Wip, em: ErrorMessage) MessageIndex {
        return @enumFromInt(addExtraAssumeCapacity(wip, em));
    }

    pub fn addSourceLocation(wip: *Wip, sl: SourceLocation) Allocator.Error!SourceLocationIndex {
        return @enumFromInt(try addExtra(wip, sl));
    }

    pub fn addReferenceTrace(wip: *Wip, rt: ReferenceTrace) Allocator.Error!void {
        _ = try addExtra(wip, rt);
    }

    pub fn addBundleAsNotes(wip: *Wip, other: ErrorBundle) Allocator.Error!void {
        const gpa = wip.gpa;

        try wip.string_bytes.ensureUnusedCapacity(gpa, other.string_bytes.len);
        try wip.extra.ensureUnusedCapacity(gpa, other.extra.len);

        const other_list = other.getMessages();

        // The ensureUnusedCapacity call above guarantees this.
        const notes_start = wip.reserveNotes(@intCast(other_list.len)) catch unreachable;
        for (notes_start.., other_list) |note, message| {
            // This line can cause `wip.extra.items` to be resized.
            const note_index = @intFromEnum(wip.addOtherMessage(other, message) catch unreachable);
            wip.extra.items[note] = note_index;
        }
    }

    pub fn addBundleAsRoots(wip: *Wip, other: ErrorBundle) !void {
        const gpa = wip.gpa;

        try wip.string_bytes.ensureUnusedCapacity(gpa, other.string_bytes.len);
        try wip.extra.ensureUnusedCapacity(gpa, other.extra.len);

        const other_list = other.getMessages();

        try wip.root_list.ensureUnusedCapacity(gpa, other_list.len);
        for (other_list) |other_msg| {
            // The ensureUnusedCapacity calls above guarantees this.
            wip.root_list.appendAssumeCapacity(wip.addOtherMessage(other, other_msg) catch unreachable);
        }
    }

    pub fn reserveNotes(wip: *Wip, notes_len: u32) !u32 {
        try wip.extra.ensureUnusedCapacity(wip.gpa, notes_len +
            notes_len * @typeInfo(ErrorBundle.ErrorMessage).@"struct".fields.len);
        wip.extra.items.len += notes_len;
        return @intCast(wip.extra.items.len - notes_len);
    }

    pub fn addZirErrorMessages(
        eb: *ErrorBundle.Wip,
        zir: std.zig.Zir,
        tree: std.zig.Ast,
        source: [:0]const u8,
        src_path: []const u8,
    ) !void {
        const Zir = std.zig.Zir;
        const payload_index = zir.extra[@intFromEnum(Zir.ExtraIndex.compile_errors)];
        assert(payload_index != 0);

        const header = zir.extraData(Zir.Inst.CompileErrors, payload_index);
        const items_len = header.data.items_len;
        var extra_index = header.end;
        for (0..items_len) |_| {
            const item = zir.extraData(Zir.Inst.CompileErrors.Item, extra_index);
            extra_index = item.end;
            const err_span = blk: {
                if (item.data.node.unwrap()) |node| {
                    break :blk tree.nodeToSpan(node);
                } else if (item.data.token.unwrap()) |token| {
                    const start = tree.tokenStart(token) + item.data.byte_offset;
                    const end = start + @as(u32, @intCast(tree.tokenSlice(token).len)) - item.data.byte_offset;
                    break :blk std.zig.Ast.Span{ .start = start, .end = end, .main = start };
                } else unreachable;
            };
            const err_loc = std.zig.findLineColumn(source, err_span.main);

            {
                const msg = zir.nullTerminatedString(item.data.msg);
                try eb.addRootErrorMessage(.{
                    .msg = try eb.addString(msg),
                    .src_loc = try eb.addSourceLocation(.{
                        .src_path = try eb.addString(src_path),
                        .span_start = err_span.start,
                        .span_main = err_span.main,
                        .span_end = err_span.end,
                        .line = @intCast(err_loc.line),
                        .column = @intCast(err_loc.column),
                        .source_line = try eb.addString(err_loc.source_line),
                    }),
                    .notes_len = item.data.notesLen(zir),
                });
            }

            if (item.data.notes != 0) {
                const notes_start = try eb.reserveNotes(item.data.notesLen(zir));
                const block = zir.extraData(Zir.Inst.Block, item.data.notes);
                const body = zir.extra[block.end..][0..block.data.body_len];
                for (notes_start.., body) |note_i, body_elem| {
                    const note_item = zir.extraData(Zir.Inst.CompileErrors.Item, body_elem);
                    const msg = zir.nullTerminatedString(note_item.data.msg);
                    const span = blk: {
                        if (note_item.data.node.unwrap()) |node| {
                            break :blk tree.nodeToSpan(node);
                        } else if (note_item.data.token.unwrap()) |token| {
                            const start = tree.tokenStart(token) + note_item.data.byte_offset;
                            const end = start + @as(u32, @intCast(tree.tokenSlice(token).len)) - item.data.byte_offset;
                            break :blk std.zig.Ast.Span{ .start = start, .end = end, .main = start };
                        } else unreachable;
                    };
                    const loc = std.zig.findLineColumn(source, span.main);

                    // This line can cause `wip.extra.items` to be resized.
                    const note_index = @intFromEnum(try eb.addErrorMessage(.{
                        .msg = try eb.addString(msg),
                        .src_loc = try eb.addSourceLocation(.{
                            .src_path = try eb.addString(src_path),
                            .span_start = span.start,
                            .span_main = span.main,
                            .span_end = span.end,
                            .line = @intCast(loc.line),
                            .column = @intCast(loc.column),
                            .source_line = if (loc.eql(err_loc))
                                0
                            else
                                try eb.addString(loc.source_line),
                        }),
                        .notes_len = 0, // TODO rework this function to be recursive
                    }));
                    eb.extra.items[note_i] = note_index;
                }
            }
        }
    }

    pub fn addZoirErrorMessages(
        eb: *ErrorBundle.Wip,
        zoir: std.zig.Zoir,
        tree: std.zig.Ast,
        source: [:0]const u8,
        src_path: []const u8,
    ) !void {
        assert(zoir.hasCompileErrors());

        for (zoir.compile_errors) |err| {
            const err_span: std.zig.Ast.Span = span: {
                if (err.token.unwrap()) |token| {
                    const token_start = tree.tokenStart(token);
                    const start = token_start + err.node_or_offset;
                    const end = token_start + @as(u32, @intCast(tree.tokenSlice(token).len));
                    break :span .{ .start = start, .end = end, .main = start };
                } else {
                    break :span tree.nodeToSpan(@enumFromInt(err.node_or_offset));
                }
            };
            const err_loc = std.zig.findLineColumn(source, err_span.main);

            try eb.addRootErrorMessage(.{
                .msg = try eb.addString(err.msg.get(zoir)),
                .src_loc = try eb.addSourceLocation(.{
                    .src_path = try eb.addString(src_path),
                    .span_start = err_span.start,
                    .span_main = err_span.main,
                    .span_end = err_span.end,
                    .line = @intCast(err_loc.line),
                    .column = @intCast(err_loc.column),
                    .source_line = try eb.addString(err_loc.source_line),
                }),
                .notes_len = err.note_count,
            });

            const notes_start = try eb.reserveNotes(err.note_count);
            for (notes_start.., err.first_note.., 0..err.note_count) |eb_note_idx, zoir_note_idx, _| {
                const note = zoir.error_notes[zoir_note_idx];
                const note_span: std.zig.Ast.Span = span: {
                    if (note.token.unwrap()) |token| {
                        const token_start = tree.tokenStart(token);
                        const start = token_start + note.node_or_offset;
                        const end = token_start + @as(u32, @intCast(tree.tokenSlice(token).len));
                        break :span .{ .start = start, .end = end, .main = start };
                    } else {
                        break :span tree.nodeToSpan(@enumFromInt(note.node_or_offset));
                    }
                };
                const note_loc = std.zig.findLineColumn(source, note_span.main);

                // This line can cause `wip.extra.items` to be resized.
                const note_index = @intFromEnum(try eb.addErrorMessage(.{
                    .msg = try eb.addString(note.msg.get(zoir)),
                    .src_loc = try eb.addSourceLocation(.{
                        .src_path = try eb.addString(src_path),
                        .span_start = note_span.start,
                        .span_main = note_span.main,
                        .span_end = note_span.end,
                        .line = @intCast(note_loc.line),
                        .column = @intCast(note_loc.column),
                        .source_line = if (note_loc.eql(err_loc))
                            0
                        else
                            try eb.addString(note_loc.source_line),
                    }),
                    .notes_len = 0,
                }));
                eb.extra.items[eb_note_idx] = note_index;
            }
        }
    }

    fn addOtherMessage(wip: *Wip, other: ErrorBundle, msg_index: MessageIndex) !MessageIndex {
        const other_msg = other.getErrorMessage(msg_index);
        const src_loc = try wip.addOtherSourceLocation(other, other_msg.src_loc);
        const msg = try wip.addErrorMessage(.{
            .msg = try wip.addString(other.nullTerminatedString(other_msg.msg)),
            .count = other_msg.count,
            .src_loc = src_loc,
            .notes_len = other_msg.notes_len,
        });
        const notes_start = try wip.reserveNotes(other_msg.notes_len);
        for (notes_start.., other.getNotes(msg_index)) |note, other_note| {
            wip.extra.items[note] = @intFromEnum(try wip.addOtherMessage(other, other_note));
        }
        return msg;
    }

    fn addOtherSourceLocation(
        wip: *Wip,
        other: ErrorBundle,
        index: SourceLocationIndex,
    ) !SourceLocationIndex {
        if (index == .none) return .none;
        const other_sl = other.getSourceLocation(index);

        var ref_traces: std.ArrayList(ReferenceTrace) = .empty;
        defer ref_traces.deinit(wip.gpa);

        if (other_sl.reference_trace_len > 0) {
            var ref_index = other.extraData(SourceLocation, @intFromEnum(index)).end;
            for (0..other_sl.reference_trace_len) |_| {
                const other_ref_trace_ed = other.extraData(ReferenceTrace, ref_index);
                const other_ref_trace = other_ref_trace_ed.data;
                ref_index = other_ref_trace_ed.end;

                const ref_trace: ReferenceTrace = if (other_ref_trace.src_loc == .none) .{
                    // sentinel ReferenceTrace does not store a string index in decl_name
                    .decl_name = other_ref_trace.decl_name,
                    .src_loc = .none,
                } else .{
                    .decl_name = try wip.addString(other.nullTerminatedString(other_ref_trace.decl_name)),
                    .src_loc = try wip.addOtherSourceLocation(other, other_ref_trace.src_loc),
                };
                try ref_traces.append(wip.gpa, ref_trace);
            }
        }

        const src_loc = try wip.addSourceLocation(.{
            .src_path = try wip.addString(other.nullTerminatedString(other_sl.src_path)),
            .line = other_sl.line,
            .column = other_sl.column,
            .span_start = other_sl.span_start,
            .span_main = other_sl.span_main,
            .span_end = other_sl.span_end,
            .source_line = if (other_sl.source_line != 0)
                try wip.addString(other.nullTerminatedString(other_sl.source_line))
            else
                0,
            .reference_trace_len = other_sl.reference_trace_len,
        });

        for (ref_traces.items) |ref_trace| {
            try wip.addReferenceTrace(ref_trace);
        }

        return src_loc;
    }

    fn addExtra(wip: *Wip, extra: anytype) Allocator.Error!u32 {
        const gpa = wip.gpa;
        const fields = @typeInfo(@TypeOf(extra)).@"struct".fields;
        try wip.extra.ensureUnusedCapacity(gpa, fields.len);
        return addExtraAssumeCapacity(wip, extra);
    }

    fn addExtraAssumeCapacity(wip: *Wip, extra: anytype) u32 {
        const fields = @typeInfo(@TypeOf(extra)).@"struct".fields;
        const result: u32 = @intCast(wip.extra.items.len);
        wip.extra.items.len += fields.len;
        setExtra(wip, result, extra);
        return result;
    }

    fn setExtra(wip: *Wip, index: usize, extra: anytype) void {
        const fields = @typeInfo(@TypeOf(extra)).@"struct".fields;
        var i = index;
        inline for (fields) |field| {
            wip.extra.items[i] = switch (field.type) {
                u32 => @field(extra, field.name),
                MessageIndex => @intFromEnum(@field(extra, field.name)),
                SourceLocationIndex => @intFromEnum(@field(extra, field.name)),
                else => @compileError("bad field type"),
            };
            i += 1;
        }
    }

    test addBundleAsRoots {
        var bundle = bundle: {
            var wip: ErrorBundle.Wip = undefined;
            try wip.init(std.testing.allocator);
            errdefer wip.deinit();

            var ref_traces: [3]ReferenceTrace = undefined;
            for (&ref_traces, 0..) |*ref_trace, i| {
                if (i == ref_traces.len - 1) {
                    // sentinel reference trace
                    ref_trace.* = .{
                        .decl_name = 3, // signifies 3 hidden references
                        .src_loc = .none,
                    };
                } else {
                    ref_trace.* = .{
                        .decl_name = try wip.addString("foo"),
                        .src_loc = try wip.addSourceLocation(.{
                            .src_path = try wip.addString("foo"),
                            .line = 1,
                            .column = 2,
                            .span_start = 3,
                            .span_main = 4,
                            .span_end = 5,
                            .source_line = 0,
                        }),
                    };
                }
            }

            const src_loc = try wip.addSourceLocation(.{
                .src_path = try wip.addString("foo"),
                .line = 1,
                .column = 2,
                .span_start = 3,
                .span_main = 4,
                .span_end = 5,
                .source_line = try wip.addString("some source code"),
                .reference_trace_len = ref_traces.len,
            });
            for (&ref_traces) |ref_trace| {
                try wip.addReferenceTrace(ref_trace);
            }

            try wip.addRootErrorMessage(ErrorMessage{
                .msg = try wip.addString("hello world"),
                .src_loc = src_loc,
                .notes_len = 1,
            });
            const i = try wip.reserveNotes(1);
            const note_index = @intFromEnum(wip.addErrorMessageAssumeCapacity(.{
                .msg = try wip.addString("this is a note"),
                .src_loc = try wip.addSourceLocation(.{
                    .src_path = try wip.addString("bar"),
                    .line = 1,
                    .column = 2,
                    .span_start = 3,
                    .span_main = 4,
                    .span_end = 5,
                    .source_line = try wip.addString("another line of source"),
                }),
            }));
            wip.extra.items[i] = note_index;

            break :bundle try wip.toOwnedBundle("");
        };
        defer bundle.deinit(std.testing.allocator);

        var bundle_buf: Writer.Allocating = .init(std.testing.allocator);
        const bundle_bw = &bundle_buf.interface;
        defer bundle_buf.deinit();
        try bundle.renderToWriter(bundle_bw);

        var copy = copy: {
            var wip: ErrorBundle.Wip = undefined;
            try wip.init(std.testing.allocator);
            errdefer wip.deinit();

            try wip.addBundleAsRoots(bundle);

            break :copy try wip.toOwnedBundle("");
        };
        defer copy.deinit(std.testing.allocator);

        var copy_buf: Writer.Allocating = .init(std.testing.allocator);
        const copy_bw = &copy_buf.interface;
        defer copy_buf.deinit();
        try copy.renderToWriter(copy_bw);

        try std.testing.expectEqualStrings(bundle_bw.written(), copy_bw.written());
    }
};



---
File: /std/zig/LibCDirs.zig
---

const LibCDirs = @This();
const builtin = @import("builtin");

const std = @import("../std.zig");
const Io = std.Io;
const LibCInstallation = std.zig.LibCInstallation;
const Allocator = std.mem.Allocator;

libc_include_dir_list: []const []const u8,
libc_installation: ?*const LibCInstallation,
libc_framework_dir_list: []const []const u8,
sysroot: ?[]const u8,
darwin_sdk_layout: ?DarwinSdkLayout,

/// The filesystem layout of darwin SDK elements.
pub const DarwinSdkLayout = enum {
    /// macOS SDK layout: TOP { /usr/include, /usr/lib, /System/Library/Frameworks }.
    sdk,
    /// Shipped libc layout: TOP { /lib/libc/include,  /lib/libc/darwin, <NONE> }.
    vendored,
};

pub fn detect(
    arena: Allocator,
    io: Io,
    zig_lib_dir: []const u8,
    target: *const std.Target,
    is_native_abi: bool,
    link_libc: bool,
    libc_installation: ?*const LibCInstallation,
    environ_map: *const std.process.Environ.Map,
) LibCInstallation.FindError!LibCDirs {
    if (!link_libc) {
        return .{
            .libc_include_dir_list = &[0][]u8{},
            .libc_installation = null,
            .libc_framework_dir_list = &.{},
            .sysroot = null,
            .darwin_sdk_layout = null,
        };
    }

    if (libc_installation) |lci| {
        return detectFromInstallation(arena, target, lci);
    }

    // If linking system libraries and targeting the native abi, default to
    // using the system libc installation.
    if (is_native_abi and !target.isMinGW()) {
        const libc = try arena.create(LibCInstallation);
        libc.* = LibCInstallation.findNative(arena, io, .{
            .target = target,
            .environ_map = environ_map,
        }) catch |err| switch (err) {
            error.CCompilerExitCode,
            error.CCompilerCrashed,
            error.CCompilerCannotFindHeaders,
            error.UnableToSpawnCCompiler,
            error.DarwinSdkNotFound,
            => |e| {
                // We tried to integrate with the native system C compiler,
                // however, it is not installed. So we must rely on our bundled
                // libc files.
                if (std.zig.target.canBuildLibC(target)) {
                    return detectFromBuilding(arena, zig_lib_dir, target);
                }
                return e;
            },
            else => |e| return e,
        };
        return detectFromInstallation(arena, target, libc);
    }

    // If not linking system libraries, build and provide our own libc by
    // default if possible.
    if (std.zig.target.canBuildLibC(target)) {
        return detectFromBuilding(arena, zig_lib_dir, target);
    }

    // If zig can't build the libc for the target and we are targeting the
    // native abi, fall back to using the system libc installation.
    // On windows, instead of the native (mingw) abi, we want to check
    // for the MSVC abi as a fallback.
    const use_system_abi = if (builtin.os.tag == .windows)
        target.abi == .msvc or target.abi == .itanium
    else
        is_native_abi;

    if (use_system_abi) {
        const libc = try arena.create(LibCInstallation);
        libc.* = try LibCInstallation.findNative(arena, io, .{
            .verbose = true,
            .target = target,
            .environ_map = environ_map,
        });
        return detectFromInstallation(arena, target, libc);
    }

    return .{
        .libc_include_dir_list = &.{},
        .libc_installation = null,
        .libc_framework_dir_list = &.{},
        .sysroot = null,
        .darwin_sdk_layout = null,
    };
}

fn detectFromInstallation(arena: Allocator, target: *const std.Target, lci: *const LibCInstallation) !LibCDirs {
    var list = try std.array_list.Managed([]const u8).initCapacity(arena, 5);
    var framework_list = std.array_list.Managed([]const u8).init(arena);

    list.appendAssumeCapacity(lci.include_dir.?);

    const is_redundant = std.mem.eql(u8, lci.sys_include_dir.?, lci.include_dir.?);
    if (!is_redundant) list.appendAssumeCapacity(lci.sys_include_dir.?);

    if (target.os.tag == .windows) {
        if (std.fs.path.dirname(lci.sys_include_dir.?)) |sys_include_dir_parent| {
            // This include path will only exist when the optional "Desktop development with C++"
            // is installed. It contains headers, .rc files, and resources. It is especially
            // necessary when working with Windows resources.
            const atlmfc_dir = try std.fs.path.join(arena, &[_][]const u8{ sys_include_dir_parent, "atlmfc", "include" });
            list.appendAssumeCapacity(atlmfc_dir);
        }
        if (std.fs.path.dirname(lci.include_dir.?)) |include_dir_parent| {
            const um_dir = try std.fs.path.join(arena, &[_][]const u8{ include_dir_parent, "um" });
            list.appendAssumeCapacity(um_dir);

            const shared_dir = try std.fs.path.join(arena, &[_][]const u8{ include_dir_parent, "shared" });
            list.appendAssumeCapacity(shared_dir);
        }
    }
    if (target.os.tag == .haiku) {
        const include_dir_path = lci.include_dir.?;
        const os_dir = try std.fs.path.join(arena, &[_][]const u8{ include_dir_path, "os" });
        list.appendAssumeCapacity(os_dir);
        // Errors.h
        const os_support_dir = try std.fs.path.join(arena, &[_][]const u8{ include_dir_path, "os/support" });
        list.appendAssumeCapacity(os_support_dir);

        const config_dir = try std.fs.path.join(arena, &[_][]const u8{ include_dir_path, "config" });
        list.appendAssumeCapacity(config_dir);
    }

    var sysroot: ?[]const u8 = null;

    if (target.os.tag.isDarwin()) d: {
        const down1 = std.fs.path.dirname(lci.sys_include_dir.?) orelse break :d;
        const down2 = std.fs.path.dirname(down1) orelse break :d;
        try framework_list.append(try std.fs.path.join(arena, &.{ down2, "System", "Library", "Frameworks" }));
        sysroot = down2;
    }

    return .{
        .libc_include_dir_list = list.items,
        .libc_installation = lci,
        .libc_framework_dir_list = framework_list.items,
        .sysroot = sysroot,
        .darwin_sdk_layout = if (sysroot == null) null else .sdk,
    };
}

pub fn detectFromBuilding(
    arena: Allocator,
    zig_lib_dir: []const u8,
    target: *const std.Target,
) !LibCDirs {
    const s = std.fs.path.sep_str;

    if (target.os.tag.isDarwin()) {
        const list = try arena.alloc([]const u8, 1);
        list[0] = try std.fmt.allocPrint(
            arena,
            "{s}" ++ s ++ "libc" ++ s ++ "include" ++ s ++ "any-darwin-any",
            .{zig_lib_dir},
        );
        return .{
            .libc_include_dir_list = list,
            .libc_installation = null,
            .libc_framework_dir_list = &.{},
            .sysroot = null,
            .darwin_sdk_layout = .vendored,
        };
    }

    const generic_name = libCGenericName(target);
    // Some architecture families are handled by the same set of headers.
    const arch_name = if (target.isMuslLibC() or target.isWasiLibC())
        std.zig.target.muslArchNameHeaders(target.cpu.arch)
    else if (target.isGnuLibC())
        std.zig.target.glibcArchNameHeaders(target.cpu.arch)
    else if (target.isFreeBSDLibC())
        std.zig.target.freebsdArchNameHeaders(target.cpu.arch)
    else if (target.isNetBSDLibC())
        std.zig.target.netbsdArchNameHeaders(target.cpu.arch)
    else if (target.isOpenBSDLibC())
        std.zig.target.openbsdArchNameHeaders(target.cpu.arch)
    else
        @tagName(target.cpu.arch);
    const os_name = @tagName(target.os.tag);
    const abi_name = if (target.isMuslLibC())
        std.zig.target.muslAbiNameHeaders(target.abi)
    else if (target.isGnuLibC())
        std.zig.target.glibcAbiNameHeaders(target.abi)
    else if (target.isNetBSDLibC())
        std.zig.target.netbsdAbiNameHeaders(target.abi)
    else
        @tagName(target.abi);
    const arch_include_dir = try std.fmt.allocPrint(
        arena,
        "{s}" ++ s ++ "libc" ++ s ++ "include" ++ s ++ "{s}-{s}-{s}",
        .{ zig_lib_dir, arch_name, os_name, abi_name },
    );
    const generic_include_dir = try std.fmt.allocPrint(
        arena,
        "{s}" ++ s ++ "libc" ++ s ++ "include" ++ s ++ "generic-{s}",
        .{ zig_lib_dir, generic_name },
    );
    const generic_arch_name = std.zig.target.osArchName(target);
    const arch_os_include_dir = try std.fmt.allocPrint(
        arena,
        "{s}" ++ s ++ "libc" ++ s ++ "include" ++ s ++ "{s}-{s}-any",
        .{ zig_lib_dir, generic_arch_name, os_name },
    );
    const generic_os_include_dir = try std.fmt.allocPrint(
        arena,
        "{s}" ++ s ++ "libc" ++ s ++ "include" ++ s ++ "any-{s}-any",
        .{ zig_lib_dir, os_name },
    );

    const list = try arena.alloc([]const u8, 4);
    list[0] = arch_include_dir;
    list[1] = generic_include_dir;
    list[2] = arch_os_include_dir;
    list[3] = generic_os_include_dir;

    return .{
        .libc_include_dir_list = list,
        .libc_installation = null,
        .libc_framework_dir_list = &.{},
        .sysroot = null,
        .darwin_sdk_layout = .vendored,
    };
}

fn libCGenericName(target: *const std.Target) [:0]const u8 {
    switch (target.os.tag) {
        .windows => return "mingw",
        .driverkit, .ios, .maccatalyst, .macos, .tvos, .visionos, .watchos => return "darwin",
        .freebsd => return "freebsd",
        .netbsd => return "netbsd",
        .openbsd => return "openbsd",
        else => {},
    }
    switch (target.abi) {
        .gnu,
        .gnuabin32,
        .gnuabi64,
        .gnueabi,
        .gnueabihf,
        .gnuf32,
        .gnusf,
        .gnux32,
        => return "glibc",
        .musl,
        .muslabin32,
        .muslabi64,
        .musleabi,
        .musleabihf,
        .muslf32,
        .muslsf,
        .muslx32,
        .none,
        .ohos,
        .ohoseabi,
        => return "musl",
        .eabi,
        .eabihf,
        .ilp32,
        .android,
        .androideabi,
        .msvc,
        .itanium,
        .simulator,
        => unreachable,
    }
}



---
File: /std/zig/LibCInstallation.zig
---

//! See the render function implementation for documentation of the fields.
const LibCInstallation = @This();

const builtin = @import("builtin");
const is_darwin = builtin.target.os.tag.isDarwin();
const is_windows = builtin.target.os.tag == .windows;
const is_haiku = builtin.target.os.tag == .haiku;

const std = @import("std");
const Io = std.Io;
const Target = std.Target;
const fs = std.fs;
const Allocator = std.mem.Allocator;
const Path = std.Build.Cache.Path;
const log = std.log.scoped(.libc_installation);
const Environ = std.process.Environ;

include_dir: ?[]const u8 = null,
sys_include_dir: ?[]const u8 = null,
crt_dir: ?[]const u8 = null,
msvc_lib_dir: ?[]const u8 = null,
kernel32_lib_dir: ?[]const u8 = null,
gcc_dir: ?[]const u8 = null,

pub const FindError = error{
    OutOfMemory,
    FileSystem,
    UnableToSpawnCCompiler,
    CCompilerExitCode,
    CCompilerCrashed,
    CCompilerCannotFindHeaders,
    LibCRuntimeNotFound,
    LibCStdLibHeaderNotFound,
    LibCKernel32LibNotFound,
    UnsupportedArchitecture,
    WindowsSdkNotFound,
    DarwinSdkNotFound,
    ZigIsTheCCompiler,
};

pub fn parse(allocator: Allocator, io: Io, libc_file: []const u8, target: *const std.Target) !LibCInstallation {
    var self: LibCInstallation = .{};

    const fields = std.meta.fields(LibCInstallation);
    const FoundKey = struct {
        found: bool,
        allocated: ?[:0]u8,
    };
    var found_keys = [1]FoundKey{FoundKey{ .found = false, .allocated = null }} ** fields.len;
    errdefer {
        self = .{};
        for (found_keys) |found_key| {
            if (found_key.allocated) |s| allocator.free(s);
        }
    }

    const contents = try Io.Dir.cwd().readFileAlloc(io, libc_file, allocator, .limited(std.math.maxInt(usize)));
    defer allocator.free(contents);

    var it = std.mem.tokenizeScalar(u8, contents, '\n');
    while (it.next()) |line| {
        if (line.len == 0 or line[0] == '#') continue;
        var line_it = std.mem.splitScalar(u8, line, '=');
        const name = line_it.first();
        const value = line_it.rest();
        inline for (fields, 0..) |field, i| {
            if (std.mem.eql(u8, name, field.name)) {
                found_keys[i].found = true;
                if (value.len == 0) {
                    @field(self, field.name) = null;
                } else {
                    found_keys[i].allocated = try allocator.dupeZ(u8, value);
                    @field(self, field.name) = found_keys[i].allocated;
                }
                break;
            }
        }
    }
    inline for (fields, 0..) |field, i| {
        if (!found_keys[i].found) {
            log.err("missing field: {s}", .{field.name});
            return error.ParseError;
        }
    }
    if (self.include_dir == null) {
        log.err("include_dir may not be empty", .{});
        return error.ParseError;
    }
    if (self.sys_include_dir == null) {
        log.err("sys_include_dir may not be empty", .{});
        return error.ParseError;
    }

    const os_tag = target.os.tag;
    if (self.crt_dir == null and !target.os.tag.isDarwin()) {
        log.err("crt_dir may not be empty for {s}", .{@tagName(os_tag)});
        return error.ParseError;
    }

    if (self.msvc_lib_dir == null and os_tag == .windows and (target.abi == .msvc or target.abi == .itanium)) {
        log.err("msvc_lib_dir may not be empty for {s}-{s}", .{
            @tagName(os_tag),
            @tagName(target.abi),
        });
        return error.ParseError;
    }
    if (self.kernel32_lib_dir == null and os_tag == .windows and (target.abi == .msvc or target.abi == .itanium)) {
        log.err("kernel32_lib_dir may not be empty for {s}-{s}", .{
            @tagName(os_tag),
            @tagName(target.abi),
        });
        return error.ParseError;
    }

    if (self.gcc_dir == null and os_tag == .haiku) {
        log.err("gcc_dir may not be empty for {s}", .{@tagName(os_tag)});
        return error.ParseError;
    }

    return self;
}

pub fn render(self: LibCInstallation, out: *std.Io.Writer) !void {
    @setEvalBranchQuota(4000);
    const include_dir = self.include_dir orelse "";
    const sys_include_dir = self.sys_include_dir orelse "";
    const crt_dir = self.crt_dir orelse "";
    const msvc_lib_dir = self.msvc_lib_dir orelse "";
    const kernel32_lib_dir = self.kernel32_lib_dir orelse "";
    const gcc_dir = self.gcc_dir orelse "";

    try out.print(
        \\# The directory that contains `stdlib.h`.
        \\# On POSIX-like systems, include directories be found with: `cc -E -Wp,-v -xc /dev/null`
        \\include_dir={s}
        \\
        \\# The system-specific include directory. May be the same as `include_dir`.
        \\# On Windows it's the directory that includes `vcruntime.h`.
        \\# On POSIX it's the directory that includes `sys/errno.h`.
        \\sys_include_dir={s}
        \\
        \\# The directory that contains `crt1.o` or `crt2.o`.
        \\# On POSIX, can b
```
