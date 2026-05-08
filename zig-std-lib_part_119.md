```
d_token`:
        ///   1. a `Node.Index` to the left side of the optional unwrap.
        ///   2. a `TokenIndex` to the `?` token.
        ///
        /// The `main_token` field is the `.` token.
        unwrap_optional,
        /// `lhs == rhs`. The `main_token` field is the `==` token.
        equal_equal,
        /// `lhs != rhs`. The `main_token` field is the `!=` token.
        bang_equal,
        /// `lhs < rhs`. The `main_token` field is the `<` token.
        less_than,
        /// `lhs > rhs`. The `main_token` field is the `>` token.
        greater_than,
        /// `lhs <= rhs`. The `main_token` field is the `<=` token.
        less_or_equal,
        /// `lhs >= rhs`. The `main_token` field is the `>=` token.
        greater_or_equal,
        /// `lhs *= rhs`. The `main_token` field is the `*=` token.
        assign_mul,
        /// `lhs /= rhs`. The `main_token` field is the `/=` token.
        assign_div,
        /// `lhs %= rhs`. The `main_token` field is the `%=` token.
        assign_mod,
        /// `lhs += rhs`. The `main_token` field is the `+=` token.
        assign_add,
        /// `lhs -= rhs`. The `main_token` field is the `-=` token.
        assign_sub,
        /// `lhs <<= rhs`. The `main_token` field is the `<<=` token.
        assign_shl,
        /// `lhs <<|= rhs`. The `main_token` field is the `<<|=` token.
        assign_shl_sat,
        /// `lhs >>= rhs`. The `main_token` field is the `>>=` token.
        assign_shr,
        /// `lhs &= rhs`. The `main_token` field is the `&=` token.
        assign_bit_and,
        /// `lhs ^= rhs`. The `main_token` field is the `^=` token.
        assign_bit_xor,
        /// `lhs |= rhs`. The `main_token` field is the `|=` token.
        assign_bit_or,
        /// `lhs *%= rhs`. The `main_token` field is the `*%=` token.
        assign_mul_wrap,
        /// `lhs +%= rhs`. The `main_token` field is the `+%=` token.
        assign_add_wrap,
        /// `lhs -%= rhs`. The `main_token` field is the `-%=` token.
        assign_sub_wrap,
        /// `lhs *|= rhs`. The `main_token` field is the `*%=` token.
        assign_mul_sat,
        /// `lhs +|= rhs`. The `main_token` field is the `+|=` token.
        assign_add_sat,
        /// `lhs -|= rhs`. The `main_token` field is the `-|=` token.
        assign_sub_sat,
        /// `lhs = rhs`. The `main_token` field is the `=` token.
        assign,
        /// `a, b, ... = rhs`.
        ///
        /// The `data` field is a `.extra_and_node`:
        ///   1. a `ExtraIndex`. Further explained below.
        ///   2. a `Node.Index` to the initialization expression.
        ///
        /// The `main_token` field is the `=` token.
        ///
        /// The `ExtraIndex` stores the following data:
        /// ```
        /// elem_count: u32,
        /// variables: [elem_count]Node.Index,
        /// ```
        ///
        /// Each node in `variables` has one of the following tags:
        ///   - `global_var_decl`
        ///   - `local_var_decl`
        ///   - `simple_var_decl`
        ///   - `aligned_var_decl`
        ///   - Any expression node
        ///
        /// The first 4 tags correspond to a `var` or `const` lhs node (note
        /// that their initialization expression is always `.none`).
        /// An expression node corresponds to a standard assignment LHS (which
        /// must be evaluated as an lvalue). There may be a preceding
        /// `comptime` token, which does not create a corresponding `comptime`
        /// node so must be manually detected.
        assign_destructure,
        /// `lhs || rhs`. The `main_token` field is the `||` token.
        merge_error_sets,
        /// `lhs * rhs`. The `main_token` field is the `*` token.
        mul,
        /// `lhs / rhs`. The `main_token` field is the `/` token.
        div,
        /// `lhs % rhs`. The `main_token` field is the `%` token.
        mod,
        /// `lhs ** rhs`. The `main_token` field is the `**` token.
        array_mult,
        /// `lhs *% rhs`. The `main_token` field is the `*%` token.
        mul_wrap,
        /// `lhs *| rhs`. The `main_token` field is the `*|` token.
        mul_sat,
        /// `lhs + rhs`. The `main_token` field is the `+` token.
        add,
        /// `lhs - rhs`. The `main_token` field is the `-` token.
        sub,
        /// `lhs ++ rhs`. The `main_token` field is the `++` token.
        array_cat,
        /// `lhs +% rhs`. The `main_token` field is the `+%` token.
        add_wrap,
        /// `lhs -% rhs`. The `main_token` field is the `-%` token.
        sub_wrap,
        /// `lhs +| rhs`. The `main_token` field is the `+|` token.
        add_sat,
        /// `lhs -| rhs`. The `main_token` field is the `-|` token.
        sub_sat,
        /// `lhs << rhs`. The `main_token` field is the `<<` token.
        shl,
        /// `lhs <<| rhs`. The `main_token` field is the `<<|` token.
        shl_sat,
        /// `lhs >> rhs`. The `main_token` field is the `>>` token.
        shr,
        /// `lhs & rhs`. The `main_token` field is the `&` token.
        bit_and,
        /// `lhs ^ rhs`. The `main_token` field is the `^` token.
        bit_xor,
        /// `lhs | rhs`. The `main_token` field is the `|` token.
        bit_or,
        /// `lhs orelse rhs`. The `main_token` field is the `orelse` token.
        @"orelse",
        /// `lhs and rhs`. The `main_token` field is the `and` token.
        bool_and,
        /// `lhs or rhs`. The `main_token` field is the `or` token.
        bool_or,
        /// `!expr`. The `main_token` field is the `!` token.
        bool_not,
        /// `-expr`. The `main_token` field is the `-` token.
        negation,
        /// `~expr`. The `main_token` field is the `~` token.
        bit_not,
        /// `-%expr`. The `main_token` field is the `-%` token.
        negation_wrap,
        /// `&expr`. The `main_token` field is the `&` token.
        address_of,
        /// `try expr`. The `main_token` field is the `try` token.
        @"try",
        /// `?expr`. The `main_token` field is the `?` token.
        optional_type,
        /// `[lhs]rhs`. The `main_token` field is the `[` token.
        array_type,
        /// `[lhs:a]b`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the length expression.
        ///   2. a `ExtraIndex` to `ArrayTypeSentinel`.
        ///
        /// The `main_token` field is the `[` token.
        array_type_sentinel,
        /// `[*]align(lhs) rhs`,
        /// `*align(lhs) rhs`,
        /// `[]rhs`.
        ///
        /// The `data` field is a `.opt_node_and_node`:
        ///   1. a `Node.OptionalIndex` to the alignment expression, if any.
        ///   2. a `Node.Index` to the element type expression.
        ///
        /// The `main_token` is the asterisk if a single item pointer or the
        /// lbracket if a slice, many-item pointer, or C-pointer.
        /// The `main_token` might be a ** token, which is shared with a
        /// parent/child pointer type and may require special handling.
        ptr_type_aligned,
        /// `[*:lhs]rhs`,
        /// `*rhs`,
        /// `[:lhs]rhs`.
        ///
        /// The `data` field is a `.opt_node_and_node`:
        ///   1. a `Node.OptionalIndex` to the sentinel expression, if any.
        ///   2. a `Node.Index` to the element type expression.
        ///
        /// The `main_token` is the asterisk if a single item pointer or the
        /// lbracket if a slice, many-item pointer, or C-pointer.
        /// The `main_token` might be a ** token, which is shared with a
        /// parent/child pointer type and may require special handling.
        ptr_type_sentinel,
        /// The `data` field is a `.extra_and_node`:
        ///   1. a `ExtraIndex` to `PtrType`.
        ///   2. a `Node.Index` to the element type expression.
        ///
        /// The `main_token` is the asterisk if a single item pointer or the
        /// lbracket if a slice, many-item pointer, or C-pointer.
        /// The `main_token` might be a ** token, which is shared with a
        /// parent/child pointer type and may require special handling.
        ptr_type,
        /// The `data` field is a `.extra_and_node`:
        ///   1. a `ExtraIndex` to `PtrTypeBitRange`.
        ///   2. a `Node.Index` to the element type expression.
        ///
        /// The `main_token` is the asterisk if a single item pointer or the
        /// lbracket if a slice, many-item pointer, or C-pointer.
        /// The `main_token` might be a ** token, which is shared with a
        /// parent/child pointer type and may require special handling.
        ptr_type_bit_range,
        /// `lhs[rhs..]`
        ///
        /// The `main_token` field is the `[` token.
        slice_open,
        /// `sliced[start..end]`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the sliced expression.
        ///   2. a `ExtraIndex` to `Slice`.
        ///
        /// The `main_token` field is the `[` token.
        slice,
        /// `sliced[start..end :sentinel]`,
        /// `sliced[start.. :sentinel]`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the sliced expression.
        ///   2. a `ExtraIndex` to `SliceSentinel`.
        ///
        /// The `main_token` field is the `[` token.
        slice_sentinel,
        /// `expr.*`.
        ///
        /// The `data` field is a `.node` to expr.
        ///
        /// The `main_token` field is the `*` token.
        deref,
        /// `lhs[rhs]`.
        ///
        /// The `main_token` field is the `[` token.
        array_access,
        /// `lhs{rhs}`.
        ///
        /// The `main_token` field is the `{` token.
        array_init_one,
        /// Same as `array_init_one` except there is known to be a trailing
        /// comma before the final rbrace.
        array_init_one_comma,
        /// `.{a}`,
        /// `.{a, b}`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first element. Never `.none`
        ///   2. a `Node.OptionalIndex` to the second element, if any.
        ///
        /// The `main_token` field is the `{` token.
        array_init_dot_two,
        /// Same as `array_init_dot_two` except there is known to be a trailing
        /// comma before the final rbrace.
        array_init_dot_two_comma,
        /// `.{a, b, c}`.
        ///
        /// The `data` field is a `.extra_range` that stores a `Node.Index` for
        /// each element.
        ///
        /// The `main_token` field is the `{` token.
        array_init_dot,
        /// Same as `array_init_dot` except there is known to be a trailing
        /// comma before the final rbrace.
        array_init_dot_comma,
        /// `a{b, c}`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the type expression.
        ///   2. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each element.
        ///
        /// The `main_token` field is the `{` token.
        array_init,
        /// Same as `array_init` except there is known to be a trailing comma
        /// before the final rbrace.
        array_init_comma,
        /// `a{.x = b}`, `a{}`.
        ///
        /// The `data` field is a `.node_and_opt_node`:
        ///   1. a `Node.Index` to the type expression.
        ///   2. a `Node.OptionalIndex` to the first field initialization, if any.
        ///
        /// The `main_token` field is the `{` token.
        ///
        /// The field name is determined by looking at the tokens preceding the
        /// field initialization.
        struct_init_one,
        /// Same as `struct_init_one` except there is known to be a trailing comma
        /// before the final rbrace.
        struct_init_one_comma,
        /// `.{.x = a, .y = b}`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first field initialization. Never `.none`
        ///   2. a `Node.OptionalIndex` to the second field initialization, if any.
        ///
        /// The `main_token` field is the '{' token.
        ///
        /// The field name is determined by looking at the tokens preceding the
        /// field initialization.
        struct_init_dot_two,
        /// Same as `struct_init_dot_two` except there is known to be a trailing
        /// comma before the final rbrace.
        struct_init_dot_two_comma,
        /// `.{.x = a, .y = b, .z = c}`.
        ///
        /// The `data` field is a `.extra_range` that stores a `Node.Index` for
        /// each field initialization.
        ///
        /// The `main_token` field is the `{` token.
        ///
        /// The field name is determined by looking at the tokens preceding the
        /// field initialization.
        struct_init_dot,
        /// Same as `struct_init_dot` except there is known to be a trailing
        /// comma before the final rbrace.
        struct_init_dot_comma,
        /// `a{.x = b, .y = c}`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the type expression.
        ///   2. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each field initialization.
        ///
        /// The `main_token` field is the `{` token.
        ///
        /// The field name is determined by looking at the tokens preceding the
        /// field initialization.
        struct_init,
        /// Same as `struct_init` except there is known to be a trailing comma
        /// before the final rbrace.
        struct_init_comma,
        /// `a(b)`, `a()`.
        ///
        /// The `data` field is a `.node_and_opt_node`:
        ///   1. a `Node.Index` to the function expression.
        ///   2. a `Node.OptionalIndex` to the first argument, if any.
        ///
        /// The `main_token` field is the `(` token.
        call_one,
        /// Same as `call_one` except there is known to be a trailing comma
        /// before the final rparen.
        call_one_comma,
        /// `a(b, c, d)`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the function expression.
        ///   2. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each argument.
        ///
        /// The `main_token` field is the `(` token.
        call,
        /// Same as `call` except there is known to be a trailing comma before
        /// the final rparen.
        call_comma,
        /// `switch(a) {}`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the switch operand.
        ///   2. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each switch case.
        ///
        /// `The `main_token` field` is the identifier of a preceding label, if any; otherwise `switch`.
        @"switch",
        /// Same as `switch` except there is known to be a trailing comma before
        /// the final rbrace.
        switch_comma,
        /// `a => b`,
        /// `else => b`.
        ///
        /// The `data` field is a `.opt_node_and_node`:
        ///   1. a `Node.OptionalIndex` where `.none` means `else`.
        ///   2. a `Node.Index` to the target expression.
        ///
        /// The `main_token` field is the `=>` token.
        switch_case_one,
        /// Same as `switch_case_one` but the case is inline.
        switch_case_inline_one,
        /// `a, b, c => d`.
        ///
        /// The `data` field is a `.extra_and_node`:
        ///   1. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each switch item.
        ///   2. a `Node.Index` to the target expression.
        ///
        /// The `main_token` field is the `=>` token.
        switch_case,
        /// Same as `switch_case` but the case is inline.
        switch_case_inline,
        /// `lhs...rhs`.
        ///
        /// The `main_token` field is the `...` token.
        switch_range,
        /// `while (a) b`,
        /// `while (a) |x| b`.
        while_simple,
        /// `while (a) : (b) c`,
        /// `while (a) |x| : (b) c`.
        while_cont,
        /// `while (a) : (b) c else d`,
        /// `while (a) |x| : (b) c else d`,
        /// `while (a) |x| : (b) c else |y| d`.
        /// The continue expression part `: (b)` may be omitted.
        @"while",
        /// `for (a) b`.
        for_simple,
        /// `for (lhs[0..inputs]) lhs[inputs + 1] else lhs[inputs + 2]`. `For[rhs]`.
        @"for",
        /// `lhs..rhs`, `lhs..`.
        for_range,
        /// `if (a) b`.
        /// `if (b) |x| b`.
        if_simple,
        /// `if (a) b else c`.
        /// `if (a) |x| b else c`.
        /// `if (a) |x| b else |y| d`.
        @"if",
        /// `suspend expr`.
        ///
        /// The `data` field is a `.node` to expr.
        ///
        /// The `main_token` field is the `suspend` token.
        @"suspend",
        /// `resume expr`.
        ///
        /// The `data` field is a `.node` to expr.
        ///
        /// The `main_token` field is the `resume` token.
        @"resume",
        /// `continue :label expr`,
        /// `continue expr`,
        /// `continue :label`,
        /// `continue`.
        ///
        /// The `data` field is a `.opt_token_and_opt_node`:
        ///   1. a `OptionalTokenIndex` to the label identifier, if any.
        ///   2. a `Node.OptionalIndex` to the target expression, if any.
        ///
        /// The `main_token` field is the `continue` token.
        @"continue",
        /// `break :label expr`,
        /// `break expr`,
        /// `break :label`,
        /// `break`.
        ///
        /// The `data` field is a `.opt_token_and_opt_node`:
        ///   1. a `OptionalTokenIndex` to the label identifier, if any.
        ///   2. a `Node.OptionalIndex` to the target expression, if any.
        ///
        /// The `main_token` field is the `break` token.
        @"break",
        /// `return expr`, `return`.
        ///
        /// The `data` field is a `.opt_node` to the return value, if any.
        ///
        /// The `main_token` field is the `return` token.
        @"return",
        /// `fn (a: type_expr) return_type`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first parameter type expression, if any.
        ///   2. a `Node.OptionalIndex` to the return type expression. Can't be
        ///      `.none` unless a parsing error occured.
        ///
        /// The `main_token` field is the `fn` token.
        ///
        /// `anytype` and `...` parameters are omitted from the AST tree.
        /// Extern function declarations use this tag.
        fn_proto_simple,
        /// `fn (a: b, c: d) return_type`.
        ///
        /// The `data` field is a `.extra_and_opt_node`:
        ///   1. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each parameter type expression.
        ///   2. a `Node.OptionalIndex` to the return type expression. Can't be
        ///      `.none` unless a parsing error occured.
        ///
        /// The `main_token` field is the `fn` token.
        ///
        /// `anytype` and `...` parameters are omitted from the AST tree.
        /// Extern function declarations use this tag.
        fn_proto_multi,
        /// `fn (a: b) addrspace(e) linksection(f) callconv(g) return_type`.
        /// zero or one parameters.
        ///
        /// The `data` field is a `.extra_and_opt_node`:
        ///   1. a `Node.ExtraIndex` to `FnProtoOne`.
        ///   2. a `Node.OptionalIndex` to the return type expression. Can't be
        ///      `.none` unless a parsing error occured.
        ///
        /// The `main_token` field is the `fn` token.
        ///
        /// `anytype` and `...` parameters are omitted from the AST tree.
        /// Extern function declarations use this tag.
        fn_proto_one,
        /// `fn (a: b, c: d) addrspace(e) linksection(f) callconv(g) return_type`.
        ///
        /// The `data` field is a `.extra_and_opt_node`:
        ///   1. a `Node.ExtraIndex` to `FnProto`.
        ///   2. a `Node.OptionalIndex` to the return type expression. Can't be
        ///      `.none` unless a parsing error occured.
        ///
        /// The `main_token` field is the `fn` token.
        ///
        /// `anytype` and `...` parameters are omitted from the AST tree.
        /// Extern function declarations use this tag.
        fn_proto,
        /// Extern function declarations use the fn_proto tags rather than this one.
        ///
        /// The `data` field is a `.node_and_node`:
        ///   1. a `Node.Index` to `fn_proto_*`.
        ///   2. a `Node.Index` to function body block.
        ///
        /// The `main_token` field is the `fn` token.
        fn_decl,
        /// `anyframe->return_type`.
        ///
        /// The `data` field is a `.token_and_node`:
        ///   1. a `TokenIndex` to the `->` token.
        ///   2. a `Node.Index` to the function frame return type expression.
        ///
        /// The `main_token` field is the `anyframe` token.
        anyframe_type,
        /// The `data` field is unused.
        anyframe_literal,
        /// The `data` field is unused.
        char_literal,
        /// The `data` field is unused.
        number_literal,
        /// The `data` field is unused.
        unreachable_literal,
        /// The `data` field is unused.
        ///
        /// Most identifiers will not have explicit AST nodes, however for
        /// expressions which could be one of many different kinds of AST nodes,
        /// there will be an identifier AST node for it.
        identifier,
        /// `.foo`.
        ///
        /// The `data` field is unused.
        ///
        /// The `main_token` field is the identifier.
        enum_literal,
        /// The `data` field is unused.
        ///
        /// The `main_token` field is the string literal token.
        string_literal,
        /// The `data` field is a `.token_and_token`:
        ///   1. a `TokenIndex` to the first `.multiline_string_literal_line` token.
        ///   2. a `TokenIndex` to the last `.multiline_string_literal_line` token.
        ///
        /// The `main_token` field is the first token index (redundant with `data`).
        multiline_string_literal,
        /// `(expr)`.
        ///
        /// The `data` field is a `.node_and_token`:
        ///   1. a `Node.Index` to the sub-expression
        ///   2. a `TokenIndex` to the `)` token.
        ///
        /// The `main_token` field is the `(` token.
        grouped_expression,
        /// `@a(b, c)`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first argument, if any.
        ///   2. a `Node.OptionalIndex` to the second argument, if any.
        ///
        /// The `main_token` field is the builtin token.
        builtin_call_two,
        /// Same as `builtin_call_two` except there is known to be a trailing comma
        /// before the final rparen.
        builtin_call_two_comma,
        /// `@a(b, c, d)`.
        ///
        /// The `data` field is a `.extra_range` that stores a `Node.Index` for
        /// each argument.
        ///
        /// The `main_token` field is the builtin token.
        builtin_call,
        /// Same as `builtin_call` except there is known to be a trailing comma
        /// before the final rparen.
        builtin_call_comma,
        /// `error{a, b}`.
        ///
        /// The `data` field is a `.token_and_token`:
        ///   1. a `TokenIndex` to the `{` token.
        ///   2. a `TokenIndex` to the `}` token.
        ///
        /// The `main_token` field is the `error`.
        error_set_decl,
        /// `struct {}`, `union {}`, `opaque {}`, `enum {}`.
        ///
        /// The `data` field is a `.extra_range` that stores a `Node.Index` for
        /// each container member.
        ///
        /// The `main_token` field is the `struct`, `union`, `opaque` or `enum` token.
        container_decl,
        /// Same as `container_decl` except there is known to be a trailing
        /// comma before the final rbrace.
        container_decl_trailing,
        /// `struct {lhs, rhs}`, `union {lhs, rhs}`, `opaque {lhs, rhs}`, `enum {lhs, rhs}`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first container member, if any.
        ///   2. a `Node.OptionalIndex` to the second container member, if any.
        ///
        /// The `main_token` field is the `struct`, `union`, `opaque` or `enum` token.
        container_decl_two,
        /// Same as `container_decl_two` except there is known to be a trailing
        /// comma before the final rbrace.
        container_decl_two_trailing,
        /// `struct(arg)`, `union(arg)`, `enum(arg)`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to arg.
        ///   2. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each container member.
        ///
        /// The `main_token` field is the `struct`, `union` or `enum` token.
        container_decl_arg,
        /// Same as `container_decl_arg` except there is known to be a trailing
        /// comma before the final rbrace.
        container_decl_arg_trailing,
        /// `union(enum) {}`.
        ///
        /// The `data` field is a `.extra_range` that stores a `Node.Index` for
        /// each container member.
        ///
        /// The `main_token` field is the `union` token.
        ///
        /// A tagged union with explicitly provided enums will instead be
        /// represented by `container_decl_arg`.
        tagged_union,
        /// Same as `tagged_union` except there is known to be a trailing comma
        /// before the final rbrace.
        tagged_union_trailing,
        /// `union(enum) {lhs, rhs}`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first container member, if any.
        ///   2. a `Node.OptionalIndex` to the second container member, if any.
        ///
        /// The `main_token` field is the `union` token.
        ///
        /// A tagged union with explicitly provided enums will instead be
        /// represented by `container_decl_arg`.
        tagged_union_two,
        /// Same as `tagged_union_two` except there is known to be a trailing
        /// comma before the final rbrace.
        tagged_union_two_trailing,
        /// `union(enum(arg)) {}`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to arg.
        ///   2. a `ExtraIndex` to a `SubRange` that stores a `Node.Index` for
        ///      each container member.
        ///
        /// The `main_token` field is the `union` token.
        tagged_union_enum_tag,
        /// Same as `tagged_union_enum_tag` except there is known to be a
        /// trailing comma before the final rbrace.
        tagged_union_enum_tag_trailing,
        /// `a: lhs = rhs,`,
        /// `a: lhs,`.
        ///
        /// The `data` field is a `.node_and_opt_node`:
        ///   1. a `Node.Index` to the field type expression.
        ///   2. a `Node.OptionalIndex` to the default value expression, if any.
        ///
        /// The `main_token` field is the field name identifier.
        ///
        /// `lastToken()` does not include the possible trailing comma.
        container_field_init,
        /// `a: lhs align(rhs),`.
        ///
        /// The `data` field is a `.node_and_node`:
        ///   1. a `Node.Index` to the field type expression.
        ///   2. a `Node.Index` to the alignment expression.
        ///
        /// The `main_token` field is the field name identifier.
        ///
        /// `lastToken()` does not include the possible trailing comma.
        container_field_align,
        /// `a: lhs align(c) = d,`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to the field type expression.
        ///   2. a `ExtraIndex` to `ContainerField`.
        ///
        /// The `main_token` field is the field name identifier.
        ///
        /// `lastToken()` does not include the possible trailing comma.
        container_field,
        /// `comptime expr`.
        ///
        /// The `data` field is a `.node` to expr.
        ///
        /// The `main_token` field is the `comptime` token.
        @"comptime",
        /// `nosuspend expr`.
        ///
        /// The `data` field is a `.node` to expr.
        ///
        /// The `main_token` field is the `nosuspend` token.
        @"nosuspend",
        /// `{lhs rhs}`.
        ///
        /// The `data` field is a `.opt_node_and_opt_node`:
        ///   1. a `Node.OptionalIndex` to the first statement, if any.
        ///   2. a `Node.OptionalIndex` to the second statement, if any.
        ///
        /// The `main_token` field is the `{` token.
        block_two,
        /// Same as `block_two` except there is known to be a trailing
        /// comma before the final rbrace.
        block_two_semicolon,
        /// `{a b}`.
        ///
        /// The `data` field is a `.extra_range` that stores a `Node.Index` for
        /// each statement.
        ///
        /// The `main_token` field is the `{` token.
        block,
        /// Same as `block` except there is known to be a trailing comma before
        /// the final rbrace.
        block_semicolon,
        /// `asm(a)`.
        ///
        /// The `main_token` field is the `asm` token.
        asm_simple,
        /// `asm(a, b)`.
        ///
        /// The `data` field is a `.node_and_extra`:
        ///   1. a `Node.Index` to a.
        ///   2. a `ExtraIndex` to `Asm`.
        ///
        /// The `main_token` field is the `asm` token.
        @"asm",
        /// `[a] "b" (c)`.
        /// `[a] "b" (-> lhs)`.
        ///
        /// The `data` field is a `.opt_node_and_token`:
        ///   1. a `Node.OptionalIndex` to lhs, if any.
        ///   2. a `TokenIndex` to the `)` token.
        ///
        /// The `main_token` field is `a`.
        asm_output,
        /// `[a] "b" (lhs)`.
        ///
        /// The `data` field is a `.node_and_token`:
        ///   1. a `Node.Index` to lhs.
        ///   2. a `TokenIndex` to the `)` token.
        ///
        /// The `main_token` field is `a`.
        asm_input,
        /// `error.a`.
        ///
        /// The `data` field is unused.
        ///
        /// The `main_token` field is `error` token.
        error_value,
        /// `lhs!rhs`.
        ///
        /// The `main_token` field is the `!` token.
        error_union,

        pub fn isContainerField(tag: Tag) bool {
            return switch (tag) {
                .container_field_init,
                .container_field_align,
                .container_field,
                => true,

                else => false,
            };
        }
    };

    pub const Data = union {
        node: Index,
        opt_node: OptionalIndex,
        token: TokenIndex,
        node_and_node: struct { Index, Index },
        opt_node_and_opt_node: struct { OptionalIndex, OptionalIndex },
        node_and_opt_node: struct { Index, OptionalIndex },
        opt_node_and_node: struct { OptionalIndex, Index },
        node_and_extra: struct { Index, ExtraIndex },
        extra_and_node: struct { ExtraIndex, Index },
        extra_and_opt_node: struct { ExtraIndex, OptionalIndex },
        node_and_token: struct { Index, TokenIndex },
        token_and_node: struct { TokenIndex, Index },
        token_and_token: struct { TokenIndex, TokenIndex },
        opt_node_and_token: struct { OptionalIndex, TokenIndex },
        opt_token_and_node: struct { OptionalTokenIndex, Index },
        opt_token_and_opt_node: struct { OptionalTokenIndex, OptionalIndex },
        opt_token_and_opt_token: struct { OptionalTokenIndex, OptionalTokenIndex },
        @"for": struct { ExtraIndex, For },
        extra_range: SubRange,
    };

    pub const LocalVarDecl = struct {
        type_node: Index,
        align_node: Index,
    };

    pub const ArrayTypeSentinel = struct {
        sentinel: Index,
        elem_type: Index,
    };

    pub const PtrType = struct {
        sentinel: OptionalIndex,
        align_node: OptionalIndex,
        addrspace_node: OptionalIndex,
    };

    pub const PtrTypeBitRange = struct {
        sentinel: OptionalIndex,
        align_node: Index,
        addrspace_node: OptionalIndex,
        bit_range_start: Index,
        bit_range_end: Index,
    };

    pub const SubRange = struct {
        /// Index into extra_data.
        start: ExtraIndex,
        /// Index into extra_data.
        end: ExtraIndex,
    };

    pub const If = struct {
        then_expr: Index,
        else_expr: Index,
    };

    pub const ContainerField = struct {
        align_expr: Index,
        value_expr: Index,
    };

    pub const GlobalVarDecl = struct {
        /// Populated if there is an explicit type ascription.
        type_node: OptionalIndex,
        /// Populated if align(A) is present.
        align_node: OptionalIndex,
        /// Populated if addrspace(A) is present.
        addrspace_node: OptionalIndex,
        /// Populated if linksection(A) is present.
        section_node: OptionalIndex,
    };

    pub const Slice = struct {
        start: Index,
        end: Index,
    };

    pub const SliceSentinel = struct {
        start: Index,
        /// May be .none if the slice is "open"
        end: OptionalIndex,
        sentinel: Index,
    };

    pub const While = struct {
        cont_expr: OptionalIndex,
        then_expr: Index,
        else_expr: Index,
    };

    pub const WhileCont = struct {
        cont_expr: Index,
        then_expr: Index,
    };

    pub const For = packed struct(u32) {
        inputs: u31,
        has_else: bool,
    };

    pub const FnProtoOne = struct {
        /// Populated if there is exactly 1 parameter. Otherwise there are 0 parameters.
        param: OptionalIndex,
        /// Populated if align(A) is present.
        align_expr: OptionalIndex,
        /// Populated if addrspace(A) is present.
        addrspace_expr: OptionalIndex,
        /// Populated if linksection(A) is present.
        section_expr: OptionalIndex,
        /// Populated if callconv(A) is present.
        callconv_expr: OptionalIndex,
    };

    pub const FnProto = struct {
        params_start: ExtraIndex,
        params_end: ExtraIndex,
        /// Populated if align(A) is present.
        align_expr: OptionalIndex,
        /// Populated if addrspace(A) is present.
        addrspace_expr: OptionalIndex,
        /// Populated if linksection(A) is present.
        section_expr: OptionalIndex,
        /// Populated if callconv(A) is present.
        callconv_expr: OptionalIndex,
    };

    pub const Asm = struct {
        items_start: ExtraIndex,
        items_end: ExtraIndex,
        clobbers: OptionalIndex,
        /// Needed to make lastToken() work.
        rparen: TokenIndex,
    };
};

pub fn nodeToSpan(tree: *const Ast, node: Ast.Node.Index) Span {
    return tokensToSpan(
        tree,
        tree.firstToken(node),
        tree.lastToken(node),
        tree.nodeMainToken(node),
    );
}

pub fn tokenToSpan(tree: *const Ast, token: Ast.TokenIndex) Span {
    return tokensToSpan(tree, token, token, token);
}

pub fn tokensToSpan(tree: *const Ast, start: Ast.TokenIndex, end: Ast.TokenIndex, main: Ast.TokenIndex) Span {
    var start_tok = start;
    var end_tok = end;

    if (tree.tokensOnSameLine(start, end)) {
        // do nothing
    } else if (tree.tokensOnSameLine(start, main)) {
        end_tok = main;
    } else if (tree.tokensOnSameLine(main, end)) {
        start_tok = main;
    } else {
        start_tok = main;
        end_tok = main;
    }
    const start_off = tree.tokenStart(start_tok);
    const end_off = tree.tokenStart(end_tok) + @as(u32, @intCast(tree.tokenSlice(end_tok).len));
    return Span{ .start = start_off, .end = end_off, .main = tree.tokenStart(main) };
}

test {
    _ = Parse;
    _ = Render;
}



---
File: /std/zig/AstGen.zig
---

//! Ingests an AST and produces ZIR code.
const AstGen = @This();

const std = @import("std");
const Ast = std.zig.Ast;
const mem = std.mem;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const ArrayList = std.ArrayList;
const StringIndexAdapter = std.hash_map.StringIndexAdapter;
const StringIndexContext = std.hash_map.StringIndexContext;

const isPrimitive = std.zig.primitives.isPrimitive;

const Zir = std.zig.Zir;
const BuiltinFn = std.zig.BuiltinFn;
const AstRlAnnotate = std.zig.AstRlAnnotate;

gpa: Allocator,
tree: *const Ast,
/// The set of nodes which, given the choice, must expose a result pointer to
/// sub-expressions. See `AstRlAnnotate` for details.
nodes_need_rl: *const AstRlAnnotate.RlNeededSet,
instructions: std.MultiArrayList(Zir.Inst) = .{},
extra: ArrayList(u32) = .empty,
string_bytes: ArrayList(u8) = .empty,
/// Tracks the current byte offset within the source file.
/// Used to populate line deltas in the ZIR. AstGen maintains
/// this "cursor" throughout the entire AST lowering process in order
/// to avoid starting over the line/column scan for every declaration, which
/// would be O(N^2).
source_offset: u32 = 0,
/// Tracks the corresponding line of `source_offset`.
/// This value is absolute.
source_line: u32 = 0,
/// Tracks the corresponding column of `source_offset`.
/// This value is absolute.
source_column: u32 = 0,
/// Used for temporary allocations; freed after AstGen is complete.
/// The resulting ZIR code has no references to anything in this arena.
arena: Allocator,
string_table: std.HashMapUnmanaged(u32, void, StringIndexContext, std.hash_map.default_max_load_percentage) = .empty,
compile_errors: ArrayList(Zir.Inst.CompileErrors.Item) = .empty,
/// The topmost block of the current function.
fn_block: ?*GenZir = null,
fn_var_args: bool = false,
/// Whether we are somewhere within a function. If `true`, any container decls may be
/// generic and thus must be tunneled through closure.
within_fn: bool = false,
/// The return type of the current function. This may be a trivial `Ref`, or
/// otherwise it refers to a `ret_type` instruction.
fn_ret_ty: Zir.Inst.Ref = .none,
/// Maps string table indexes to the first `@import` ZIR instruction
/// that uses this string as the operand.
imports: std.AutoArrayHashMapUnmanaged(Zir.NullTerminatedString, Ast.TokenIndex) = .empty,
/// Used for temporary storage when building payloads.
scratch: std.ArrayList(u32) = .empty,
/// Whenever a `ref` instruction is needed, it is created and saved in this
/// table instead of being immediately appended to the current block body.
/// Then, when the instruction is being added to the parent block (typically from
/// setBlockBody), if it has a ref_table entry, then the ref instruction is added
/// there. This makes sure two properties are upheld:
/// 1. All pointers to the same locals return the same address. This is required
///    to be compliant with the language specification.
/// 2. `ref` instructions will dominate their uses. This is a required property
///    of ZIR.
/// The key is the ref operand; the value is the ref instruction.
ref_table: std.AutoHashMapUnmanaged(Zir.Inst.Index, Zir.Inst.Index) = .empty,
/// Any information which should trigger invalidation of incremental compilation
/// data should be used to update this hasher. The result is the final source
/// hash of the enclosing declaration/etc.
src_hasher: std.zig.SrcHasher,

const InnerError = error{ OutOfMemory, AnalysisFail };

fn addExtra(astgen: *AstGen, extra: anytype) Allocator.Error!u32 {
    const fields = std.meta.fields(@TypeOf(extra));
    try astgen.extra.ensureUnusedCapacity(astgen.gpa, fields.len);
    return addExtraAssumeCapacity(astgen, extra);
}

fn addExtraAssumeCapacity(astgen: *AstGen, extra: anytype) u32 {
    const fields = std.meta.fields(@TypeOf(extra));
    const extra_index: u32 = @intCast(astgen.extra.items.len);
    astgen.extra.items.len += fields.len;
    setExtra(astgen, extra_index, extra);
    return extra_index;
}

fn setExtra(astgen: *AstGen, index: usize, extra: anytype) void {
    const fields = std.meta.fields(@TypeOf(extra));
    var i = index;
    inline for (fields) |field| {
        astgen.extra.items[i] = switch (field.type) {
            u32 => @field(extra, field.name),

            Zir.Inst.Ref,
            Zir.Inst.Index,
            Zir.Inst.Declaration.Name,
            std.zig.SimpleComptimeReason,
            Zir.NullTerminatedString,
            // Ast.TokenIndex is missing because it is a u32.
            Ast.OptionalTokenIndex,
            Ast.Node.Index,
            Ast.Node.OptionalIndex,
            => @intFromEnum(@field(extra, field.name)),

            Ast.TokenOffset,
            Ast.OptionalTokenOffset,
            Ast.Node.Offset,
            Ast.Node.OptionalOffset,
            => @bitCast(@intFromEnum(@field(extra, field.name))),

            i32,
            Zir.Inst.Call.Flags,
            Zir.Inst.BuiltinCall.Flags,
            Zir.Inst.SwitchBlock.Bits,
            Zir.Inst.FuncFancy.Bits,
            Zir.Inst.Param.Type,
            Zir.Inst.Func.RetTy,
            => @bitCast(@field(extra, field.name)),

            else => @compileError("bad field type"),
        };
        i += 1;
    }
}

fn reserveExtra(astgen: *AstGen, size: usize) Allocator.Error!u32 {
    const extra_index: u32 = @intCast(astgen.extra.items.len);
    try astgen.extra.resize(astgen.gpa, extra_index + size);
    return extra_index;
}

fn appendRefs(astgen: *AstGen, refs: []const Zir.Inst.Ref) !void {
    return astgen.extra.appendSlice(astgen.gpa, @ptrCast(refs));
}

fn appendRefsAssumeCapacity(astgen: *AstGen, refs: []const Zir.Inst.Ref) void {
    astgen.extra.appendSliceAssumeCapacity(@ptrCast(refs));
}

pub fn generate(gpa: Allocator, tree: Ast) Allocator.Error!Zir {
    assert(tree.mode == .zig);

    var arena = std.heap.ArenaAllocator.init(gpa);
    defer arena.deinit();

    var nodes_need_rl = try AstRlAnnotate.annotate(gpa, arena.allocator(), tree);
    defer nodes_need_rl.deinit(gpa);

    var astgen: AstGen = .{
        .gpa = gpa,
        .arena = arena.allocator(),
        .tree = &tree,
        .nodes_need_rl = &nodes_need_rl,
        .src_hasher = undefined, // `structDeclInner` for the root struct will set this
    };
    defer astgen.deinit(gpa);

    // String table index 0 is reserved for `NullTerminatedString.empty`.
    try astgen.string_bytes.append(gpa, 0);

    // We expect at least as many ZIR instructions and extra data items
    // as AST nodes.
    try astgen.instructions.ensureTotalCapacity(gpa, tree.nodes.len);

    // First few indexes of extra are reserved and set at the end.
    const reserved_count = @typeInfo(Zir.ExtraIndex).@"enum".fields.len;
    try astgen.extra.ensureTotalCapacity(gpa, tree.nodes.len + reserved_count);
    astgen.extra.items.len += reserved_count;

    var top_scope: Scope.Top = .{};

    var gz_instructions: std.ArrayList(Zir.Inst.Index) = .empty;
    var gen_scope: GenZir = .{
        .is_comptime = true,
        .parent = &top_scope.base,
        .decl_node_index = .root,
        .decl_line = 0,
        .astgen = &astgen,
        .instructions = &gz_instructions,
        .instructions_top = 0,
    };
    defer gz_instructions.deinit(gpa);

    // The AST -> ZIR lowering process assumes an AST that does not have any parse errors.
    // Parse errors, or AstGen errors in the root struct, are considered "fatal", so we emit no ZIR.
    const fatal = if (tree.errors.len == 0) fatal: {
        if (AstGen.structDeclInner(
            &gen_scope,
            &gen_scope.base,
            .root,
            tree.containerDeclRoot(),
            .auto,
            .none,
            .parent,
        )) |struct_decl_ref| {
            assert(struct_decl_ref.toIndex().? == .main_struct_inst);
            break :fatal false;
        } else |err| switch (err) {
            error.OutOfMemory => return error.OutOfMemory,
            error.AnalysisFail => break :fatal true, // Handled via compile_errors below.
        }
    } else fatal: {
        try lowerAstErrors(&astgen);
        break :fatal true;
    };

    const err_index = @intFromEnum(Zir.ExtraIndex.compile_errors);
    if (astgen.compile_errors.items.len == 0) {
        astgen.extra.items[err_index] = 0;
    } else {
        try astgen.extra.ensureUnusedCapacity(gpa, 1 + astgen.compile_errors.items.len *
            @typeInfo(Zir.Inst.CompileErrors.Item).@"struct".fields.len);

        astgen.extra.items[err_index] = astgen.addExtraAssumeCapacity(Zir.Inst.CompileErrors{
            .items_len = @intCast(astgen.compile_errors.items.len),
        });

        for (astgen.compile_errors.items) |item| {
            _ = astgen.addExtraAssumeCapacity(item);
        }
    }

    const imports_index = @intFromEnum(Zir.ExtraIndex.imports);
    if (astgen.imports.count() == 0) {
        astgen.extra.items[imports_index] = 0;
    } else {
        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.Imports).@"struct".fields.len +
            astgen.imports.count() * @typeInfo(Zir.Inst.Imports.Item).@"struct".fields.len);

        astgen.extra.items[imports_index] = astgen.addExtraAssumeCapacity(Zir.Inst.Imports{
            .imports_len = @intCast(astgen.imports.count()),
        });

        var it = astgen.imports.iterator();
        while (it.next()) |entry| {
            _ = astgen.addExtraAssumeCapacity(Zir.Inst.Imports.Item{
                .name = entry.key_ptr.*,
                .token = entry.value_ptr.*,
            });
        }
    }

    return .{
        .instructions = if (fatal) .empty else astgen.instructions.toOwnedSlice(),
        .string_bytes = try astgen.string_bytes.toOwnedSlice(gpa),
        .extra = try astgen.extra.toOwnedSlice(gpa),
    };
}

fn deinit(astgen: *AstGen, gpa: Allocator) void {
    astgen.instructions.deinit(gpa);
    astgen.extra.deinit(gpa);
    astgen.string_table.deinit(gpa);
    astgen.string_bytes.deinit(gpa);
    astgen.compile_errors.deinit(gpa);
    astgen.imports.deinit(gpa);
    astgen.scratch.deinit(gpa);
    astgen.ref_table.deinit(gpa);
}

const ResultInfo = struct {
    /// The semantics requested for the result location
    rl: Loc,

    /// The "operator" consuming the result location
    ctx: Context = .none,

    /// Turns a `coerced_ty` back into a `ty`. Should be called at branch points
    /// such as if and switch expressions.
    fn br(ri: ResultInfo) ResultInfo {
        return switch (ri.rl) {
            .coerced_ty => |ty| .{
                .rl = .{ .ty = ty },
                .ctx = ri.ctx,
            },
            else => ri,
        };
    }

    fn zirTag(ri: ResultInfo) Zir.Inst.Tag {
        switch (ri.rl) {
            .ty => return switch (ri.ctx) {
                .shift_op => .as_shift_operand,
                else => .as_node,
            },
            else => unreachable,
        }
    }

    const Loc = union(enum) {
        /// The expression is the right-hand side of assignment to `_`. Only the side-effects of the
        /// expression should be generated. The result instruction from the expression must
        /// be ignored.
        discard,
        /// The expression has an inferred type, and it will be evaluated as an rvalue.
        none,
        /// The expression will be coerced into this type, but it will be evaluated as an rvalue.
        ty: Zir.Inst.Ref,
        /// Same as `ty` but it is guaranteed that Sema will additionally perform the coercion,
        /// so no `as` instruction needs to be emitted.
        coerced_ty: Zir.Inst.Ref,
        /// The expression must generate a pointer rather than a value. For example, the left hand side
        /// of an assignment uses this kind of result location.
        ref,
        /// The expression must generate a pointer rather than a value, and the pointer will be coerced
        /// by other code to this type, which is guaranteed by earlier instructions to be a pointer type.
        ref_coerced_ty: Zir.Inst.Ref,
        /// Like `ref`, but the pointer will never be stored to, so local variables should not be
        /// marked as possibly being mutated.
        ref_const,
        /// The expression must store its result into this typed pointer. The result instruction
        /// from the expression must be ignored.
        ptr: PtrResultLoc,
        /// The expression must store its result into this allocation, which has an inferred type.
        /// The result instruction from the expression must be ignored.
        /// Always an instruction with tag `alloc_inferred`.
        inferred_ptr: Zir.Inst.Ref,
        /// The expression has a sequence of pointers to store its results into due to a destructure
        /// operation. Each of these pointers may or may not have an inferred type.
        destructure: struct {
            /// The AST node of the destructure operation itself.
            src_node: Ast.Node.Index,
            /// The pointers to store results into.
            components: []const DestructureComponent,
        },

        const DestructureComponent = union(enum) {
            typed_ptr: PtrResultLoc,
            inferred_ptr: Zir.Inst.Ref,
            discard,
        };

        const PtrResultLoc = struct {
            inst: Zir.Inst.Ref,
            src_node: ?Ast.Node.Index = null,
        };

        /// Find the result type for a cast builtin given the result location.
        /// If the location does not have a known result type, returns `null`.
        fn resultType(rl: Loc, gz: *GenZir, node: Ast.Node.Index) !?Zir.Inst.Ref {
            return switch (rl) {
                .discard, .none, .ref, .ref_const, .inferred_ptr, .destructure => null,
                .ty, .coerced_ty => |ty_ref| ty_ref,
                .ref_coerced_ty => |ptr_ty| try gz.addUnNode(.elem_type, ptr_ty, node),
                .ptr => |ptr| {
                    const ptr_ty = try gz.addUnNode(.typeof, ptr.inst, node);
                    return try gz.addUnNode(.elem_type, ptr_ty, node);
                },
            };
        }

        /// Find the result type for a cast builtin given the result location.
        /// If the location does not have a known result type, emits an error on
        /// the given node.
        fn resultTypeForCast(rl: Loc, gz: *GenZir, node: Ast.Node.Index, builtin_name: []const u8) !Zir.Inst.Ref {
            const astgen = gz.astgen;
            if (try rl.resultType(gz, node)) |ty| return ty;
            switch (rl) {
                .destructure => |destructure| return astgen.failNodeNotes(node, "{s} must have a known result type", .{builtin_name}, &.{
                    try astgen.errNoteNode(destructure.src_node, "destructure expressions do not provide a single result type", .{}),
                    try astgen.errNoteNode(node, "use @as to provide explicit result type", .{}),
                }),
                else => return astgen.failNodeNotes(node, "{s} must have a known result type", .{builtin_name}, &.{
                    try astgen.errNoteNode(node, "use @as to provide explicit result type", .{}),
                }),
            }
        }
    };

    const Context = enum {
        /// The expression is the operand to a return expression.
        @"return",
        /// The expression is the input to an error-handling operator (if-else, try, or catch).
        error_handling_expr,
        /// The expression is the right-hand side of a shift operation.
        shift_op,
        /// The expression is an argument in a function call.
        fn_arg,
        /// The expression is the right-hand side of an initializer for a `const` variable
        const_init,
        /// The expression is the right-hand side of an assignment expression.
        assignment,
        /// No specific operator in particular.
        none,
        /// The expression is operand to address-of which is the operand to a return expression.
        return_addrof,
    };
};

const coerced_align_ri: ResultInfo = .{ .rl = .{ .coerced_ty = .u29_type } };
const coerced_linksection_ri: ResultInfo = .{ .rl = .{ .coerced_ty = .slice_const_u8_type } };
const coerced_type_ri: ResultInfo = .{ .rl = .{ .coerced_ty = .type_type } };
const coerced_bool_ri: ResultInfo = .{ .rl = .{ .coerced_ty = .bool_type } };

fn typeExpr(gz: *GenZir, scope: *Scope, type_node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    return comptimeExpr(gz, scope, coerced_type_ri, type_node, .type);
}

fn reachableTypeExpr(
    gz: *GenZir,
    scope: *Scope,
    type_node: Ast.Node.Index,
    reachable_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    return reachableExprComptime(gz, scope, coerced_type_ri, type_node, reachable_node, .type);
}

/// Same as `expr` but fails with a compile error if the result type is `noreturn`.
fn reachableExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    reachable_node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    return reachableExprComptime(gz, scope, ri, node, reachable_node, null);
}

fn reachableExprComptime(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    reachable_node: Ast.Node.Index,
    /// If `null`, the expression is not evaluated in a comptime context.
    comptime_reason: ?std.zig.SimpleComptimeReason,
) InnerError!Zir.Inst.Ref {
    const result_inst = if (comptime_reason) |r|
        try comptimeExpr(gz, scope, ri, node, r)
    else
        try expr(gz, scope, ri, node);

    if (gz.refIsNoReturn(result_inst)) {
        try gz.astgen.appendErrorNodeNotes(reachable_node, "unreachable code", .{}, &[_]u32{
            try gz.astgen.errNoteNode(node, "control flow is diverted here", .{}),
        });
    }
    return result_inst;
}

fn lvalExpr(gz: *GenZir, scope: *Scope, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    switch (tree.nodeTag(node)) {
        .root => unreachable,
        .test_decl => unreachable,
        .global_var_decl => unreachable,
        .local_var_decl => unreachable,
        .simple_var_decl => unreachable,
        .aligned_var_decl => unreachable,
        .switch_case => unreachable,
        .switch_case_inline => unreachable,
        .switch_case_one => unreachable,
        .switch_case_inline_one => unreachable,
        .container_field_init => unreachable,
        .container_field_align => unreachable,
        .container_field => unreachable,
        .asm_output => unreachable,
        .asm_input => unreachable,

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
        .bit_and,
        .bit_or,
        .shl,
        .shl_sat,
        .shr,
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
        .@"asm",
        .asm_simple,
        .string_literal,
        .number_literal,
        .call,
        .call_comma,
        .call_one,
        .call_one_comma,
        .unreachable_literal,
        .@"return",
        .@"if",
        .if_simple,
        .@"while",
        .while_simple,
        .while_cont,
        .bool_not,
        .address_of,
        .optional_type,
        .block,
        .block_semicolon,
        .block_two,
        .block_two_semicolon,
        .@"break",
        .ptr_type_aligned,
        .ptr_type_sentinel,
        .ptr_type,
        .ptr_type_bit_range,
        .array_type,
        .array_type_sentinel,
        .enum_literal,
        .multiline_string_literal,
        .char_literal,
        .@"defer",
        .@"errdefer",
        .@"catch",
        .error_union,
        .merge_error_sets,
        .switch_range,
        .for_range,
        .bit_not,
        .negation,
        .negation_wrap,
        .@"resume",
        .@"try",
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
        .@"switch",
        .switch_comma,
        .@"for",
        .for_simple,
        .@"suspend",
        .@"continue",
        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
        .fn_decl,
        .anyframe_type,
        .anyframe_literal,
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
        .@"comptime",
        .@"nosuspend",
        .error_value,
        => return astgen.failNode(node, "invalid left-hand side to assignment", .{}),

        .builtin_call,
        .builtin_call_comma,
        .builtin_call_two,
        .builtin_call_two_comma,
        => {
            const builtin_token = tree.nodeMainToken(node);
            const builtin_name = tree.tokenSlice(builtin_token);
            // If the builtin is an invalid name, we don't cause an error here; instead
            // let it pass, and the error will be "invalid builtin function" later.
            if (BuiltinFn.list.get(builtin_name)) |info| {
                if (!info.allows_lvalue) {
                    return astgen.failNode(node, "invalid left-hand side to assignment", .{});
                }
            }
        },

        // These can be assigned to.
        .unwrap_optional,
        .deref,
        .field_access,
        .array_access,
        .identifier,
        .grouped_expression,
        .@"orelse",
        => {},
    }
    return expr(gz, scope, .{ .rl = .ref }, node);
}

/// Turn Zig AST into untyped ZIR instructions.
/// When `rl` is discard, ptr, inferred_ptr, or inferred_ptr, the
/// result instruction can be used to inspect whether it is isNoReturn() but that is it,
/// it must otherwise not be used.
fn expr(gz: *GenZir, scope: *Scope, ri: ResultInfo, node: Ast.Node.Index) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    switch (tree.nodeTag(node)) {
        .root => unreachable, // Top-level declaration.
        .test_decl => unreachable, // Top-level declaration.
        .container_field_init => unreachable, // Top-level declaration.
        .container_field_align => unreachable, // Top-level declaration.
        .container_field => unreachable, // Top-level declaration.
        .fn_decl => unreachable, // Top-level declaration.

        .global_var_decl => unreachable, // Handled in `blockExpr`.
        .local_var_decl => unreachable, // Handled in `blockExpr`.
        .simple_var_decl => unreachable, // Handled in `blockExpr`.
        .aligned_var_decl => unreachable, // Handled in `blockExpr`.
        .@"defer" => unreachable, // Handled in `blockExpr`.
        .@"errdefer" => unreachable, // Handled in `blockExpr`.

        .switch_case => unreachable, // Handled in `switchExpr`.
        .switch_case_inline => unreachable, // Handled in `switchExpr`.
        .switch_case_one => unreachable, // Handled in `switchExpr`.
        .switch_case_inline_one => unreachable, // Handled in `switchExpr`.
        .switch_range => unreachable, // Handled in `switchExpr`.

        .asm_output => unreachable, // Handled in `asmExpr`.
        .asm_input => unreachable, // Handled in `asmExpr`.

        .for_range => unreachable, // Handled in `forExpr`.

        .assign => {
            try assign(gz, scope, node);
            return rvalue(gz, ri, .void_value, node);
        },

        .assign_destructure => {
            // Note that this variant does not declare any new var/const: that
            // variant is handled by `blockExprStmts`.
            try assignDestructure(gz, scope, node);
            return rvalue(gz, ri, .void_value, node);
        },

        .assign_shl => {
            try assignShift(gz, scope, node, .shl);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_shl_sat => {
            try assignShiftSat(gz, scope, node);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_shr => {
            try assignShift(gz, scope, node, .shr);
            return rvalue(gz, ri, .void_value, node);
        },

        .assign_bit_and => {
            try assignOp(gz, scope, node, .bit_and);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_bit_or => {
            try assignOp(gz, scope, node, .bit_or);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_bit_xor => {
            try assignOp(gz, scope, node, .xor);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_div => {
            try assignOp(gz, scope, node, .div);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_sub => {
            try assignOp(gz, scope, node, .sub);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_sub_wrap => {
            try assignOp(gz, scope, node, .subwrap);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_sub_sat => {
            try assignOp(gz, scope, node, .sub_sat);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_mod => {
            try assignOp(gz, scope, node, .mod_rem);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_add => {
            try assignOp(gz, scope, node, .add);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_add_wrap => {
            try assignOp(gz, scope, node, .addwrap);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_add_sat => {
            try assignOp(gz, scope, node, .add_sat);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_mul => {
            try assignOp(gz, scope, node, .mul);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_mul_wrap => {
            try assignOp(gz, scope, node, .mulwrap);
            return rvalue(gz, ri, .void_value, node);
        },
        .assign_mul_sat => {
            try assignOp(gz, scope, node, .mul_sat);
            return rvalue(gz, ri, .void_value, node);
        },

        // zig fmt: off
        .shl => return shiftOp(gz, scope, ri, node, tree.nodeData(node).node_and_node[0], tree.nodeData(node).node_and_node[1], .shl),
        .shr => return shiftOp(gz, scope, ri, node, tree.nodeData(node).node_and_node[0], tree.nodeData(node).node_and_node[1], .shr),

        .add      => return simpleBinOp(gz, scope, ri, node, .add),
        .add_wrap => return simpleBinOp(gz, scope, ri, node, .addwrap),
        .add_sat  => return simpleBinOp(gz, scope, ri, node, .add_sat),
        .sub      => return simpleBinOp(gz, scope, ri, node, .sub),
        .sub_wrap => return simpleBinOp(gz, scope, ri, node, .subwrap),
        .sub_sat  => return simpleBinOp(gz, scope, ri, node, .sub_sat),
        .mul      => return simpleBinOp(gz, scope, ri, node, .mul),
        .mul_wrap => return simpleBinOp(gz, scope, ri, node, .mulwrap),
        .mul_sat  => return simpleBinOp(gz, scope, ri, node, .mul_sat),
        .div      => return simpleBinOp(gz, scope, ri, node, .div),
        .mod      => return simpleBinOp(gz, scope, ri, node, .mod_rem),
        .shl_sat  => return simpleBinOp(gz, scope, ri, node, .shl_sat),

        .bit_and          => return simpleBinOp(gz, scope, ri, node, .bit_and),
        .bit_or           => return simpleBinOp(gz, scope, ri, node, .bit_or),
        .bit_xor          => return simpleBinOp(gz, scope, ri, node, .xor),
        .bang_equal       => return simpleBinOp(gz, scope, ri, node, .cmp_neq),
        .equal_equal      => return simpleBinOp(gz, scope, ri, node, .cmp_eq),
        .greater_than     => return simpleBinOp(gz, scope, ri, node, .cmp_gt),
        .greater_or_equal => return simpleBinOp(gz, scope, ri, node, .cmp_gte),
        .less_than        => return simpleBinOp(gz, scope, ri, node, .cmp_lt),
        .less_or_equal    => return simpleBinOp(gz, scope, ri, node, .cmp_lte),
        .array_cat        => return simpleBinOp(gz, scope, ri, node, .array_cat),

        .array_mult => {
            // This syntax form does not currently use the result type in the language specification.
            // However, the result type can be used to emit more optimal code for large multiplications by
            // having Sema perform a coercion before the multiplication operation.
            const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;
            const result = try gz.addPlNode(.array_mul, node, Zir.Inst.ArrayMul{
                .res_ty = if (try ri.rl.resultType(gz, node)) |t| t else .none,
                .lhs = try expr(gz, scope, .{ .rl = .none }, lhs_node),
                .rhs = try comptimeExpr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, rhs_node, .array_mul_factor),
            });
            return rvalue(gz, ri, result, node);
        },

        .error_union, .merge_error_sets => |tag| {
            const inst_tag: Zir.Inst.Tag = switch (tag) {
                .error_union => .error_union_type,
                .merge_error_sets => .merge_error_sets,
                else => unreachable,
            };
            const lhs_node, const rhs_node = tree.nodeData(node).node_and_node;
            const lhs = try reachableTypeExpr(gz, scope, lhs_node, node);
            const rhs = try reachableTypeExpr(gz, scope, rhs_node, node);
            const result = try gz.addPlNode(inst_tag, node, Zir.Inst.Bin{ .lhs = lhs, .rhs = rhs });
            return rvalue(gz, ri, result, node);
        },

        .bool_and => return boolBinOp(gz, scope, ri, node, .bool_br_and),
        .bool_or  => return boolBinOp(gz, scope, ri, node, .bool_br_or),

        .bool_not => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none }, tree.nodeData(node).node, .bool_not),
        .bit_not  => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none }, tree.nodeData(node).node, .bit_not),

        .negation      => return   negation(gz, scope, ri, node),
        .negation_wrap => return simpleUnOp(gz, scope, ri, node, .{ .rl = .none }, tree.nodeData(node).node, .negate_wrap),

        .identifier => return identifier(gz, scope, ri, node, null),

        .asm_simple,
        .@"asm",
        => return asmExpr(gz, scope, ri, node, tree.fullAsm(node).?),

        .string_literal           => return stringLiteral(gz, ri, node),
        .multiline_string_literal => return multilineStringLiteral(gz, ri, node),

        .number_literal => return numberLiteral(gz, ri, node, node, .positive),
        // zig fmt: on

        .builtin_call_two,
        .builtin_call_two_comma,
        .builtin_call,
        .builtin_call_comma,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const params = tree.builtinCallParams(&buf, node).?;
            return builtinCall(gz, scope, ri, node, params, false, .anon);
        },

        .call_one,
        .call_one_comma,
        .call,
        .call_comma,
        => {
            var buf: [1]Ast.Node.Index = undefined;
            return callExpr(gz, scope, ri, .none, node, tree.fullCall(&buf, node).?);
        },

        .unreachable_literal => {
            try emitDbgNode(gz, node);
            _ = try gz.addAsIndex(.{
                .tag = .@"unreachable",
                .data = .{ .@"unreachable" = .{
                    .src_node = gz.nodeIndexToRelative(node),
                } },
            });
            return Zir.Inst.Ref.unreachable_value;
        },
        .@"return" => return ret(gz, scope, node),
        .field_access => return fieldAccess(gz, scope, ri, node),

        .if_simple,
        .@"if",
        => {
            const if_full = tree.fullIf(node).?;
            no_switch_on_err: {
                const error_token = if_full.error_token orelse break :no_switch_on_err;
                const else_node = if_full.ast.else_expr.unwrap() orelse break :no_switch_on_err;
                const switch_full = tree.fullSwitch(else_node) orelse break :no_switch_on_err;
                if (switch_full.label_token != null) break :no_switch_on_err; // handled in `ifExpr`
                if (tree.nodeTag(switch_full.ast.condition) != .identifier) break :no_switch_on_err;
                if (!try astgen.tokenIdentEql(error_token, tree.nodeMainToken(switch_full.ast.condition))) break :no_switch_on_err;
                return switchExpr(gz, scope, ri.br(), node, switch_full, .{ .@"if" = if_full });
            }
            return ifExpr(gz, scope, ri.br(), node, if_full);
        },

        .while_simple,
        .while_cont,
        .@"while",
        => return whileExpr(gz, scope, ri.br(), node, tree.fullWhile(node).?, false),

        .for_simple, .@"for" => return forExpr(gz, scope, ri.br(), node, tree.fullFor(node).?, false),

        .slice_open,
        .slice,
        .slice_sentinel,
        => {
            const full = tree.fullSlice(node).?;
            if (full.ast.end != .none and
                tree.nodeTag(full.ast.sliced) == .slice_open and
                nodeIsTriviallyZero(tree, full.ast.start))
            {
                const lhs_extra = tree.sliceOpen(full.ast.sliced).ast;

                const lhs = try expr(gz, scope, .{ .rl = .ref }, lhs_extra.sliced);
                const start = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, lhs_extra.start);
                const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
                const len = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, full.ast.end.unwrap().?);
                const sentinel = if (full.ast.sentinel.unwrap()) |sentinel| try expr(gz, scope, .{ .rl = .none }, sentinel) else .none;
                try emitDbgStmt(gz, cursor);
                const result = try gz.addPlNode(.slice_length, node, Zir.Inst.SliceLength{
                    .lhs = lhs,
                    .start = start,
                    .len = len,
                    .start_src_node_offset = gz.nodeIndexToRelative(full.ast.sliced),
                    .sentinel = sentinel,
                });
                return rvalue(gz, ri, result, node);
            }
            const lhs = try expr(gz, scope, .{ .rl = .ref }, full.ast.sliced);

            const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
            const start = try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, full.ast.start);
            const end = if (full.ast.end.unwrap()) |end| try expr(gz, scope, .{ .rl = .{ .coerced_ty = .usize_type } }, end) else .none;
            const sentinel = if (full.ast.sentinel.unwrap()) |sentinel| s: {
                const sentinel_ty = try gz.addUnNode(.slice_sentinel_ty, lhs, node);
                break :s try expr(gz, scope, .{ .rl = .{ .coerced_ty = sentinel_ty } }, sentinel);
            } else .none;
            try emitDbgStmt(gz, cursor);
            if (sentinel != .none) {
                const result = try gz.addPlNode(.slice_sentinel, node, Zir.Inst.SliceSentinel{
                    .lhs = lhs,
                    .start = start,
                    .end = end,
                    .sentinel = sentinel,
                });
                return rvalue(gz, ri, result, node);
            } else if (end != .none) {
                const result = try gz.addPlNode(.slice_end, node, Zir.Inst.SliceEnd{
                    .lhs = lhs,
                    .start = start,
                    .end = end,
                });
                return rvalue(gz, ri, result, node);
            } else {
                const result = try gz.addPlNode(.slice_start, node, Zir.Inst.SliceStart{
                    .lhs = lhs,
                    .start = start,
                });
                return rvalue(gz, ri, result, node);
            }
        },

        .deref => {
            const lhs = try expr(gz, scope, .{ .rl = .none }, tree.nodeData(node).node);
            _ = try gz.addUnNode(.validate_deref, lhs, node);
            switch (ri.rl) {
                .ref,
                .ref_coerced_ty,
                .ref_const,
                => return lhs,

                else => {
                    const result = try gz.addUnNode(.load, lhs, node);
                    return rvalue(gz, ri, result, node);
                },
            }
        },
        .address_of => {
            const operand_rl: ResultInfo.Loc = if (try ri.rl.resultType(gz, node)) |res_ty_inst| rl: {
                _ = try gz.addUnTok(.validate_ref_ty, res_ty_inst, tree.firstToken(node));
                break :rl .{ .ref_coerced_ty = res_ty_inst };
            } else .ref;
            const operand_node = tree.nodeData(node).node;
            const result = try expr(gz, scope, .{
                .rl = operand_rl,
                .ctx = switch (ri.ctx) {
                    .@"return" => .return_addrof,
                    else => .none,
                },
            }, operand_node);
            return rvalue(gz, ri, result, node);
        },
        .optional_type => {
            const operand = try typeExpr(gz, scope, tree.nodeData(node).node);
            const result = try gz.addUnNode(.optional_type, operand, node);
            return rvalue(gz, ri, result, node);
        },
        .unwrap_optional => switch (ri.rl) {
            .ref, .ref_coerced_ty => {
                const lhs = try expr(gz, scope, .{ .rl = .ref }, tree.nodeData(node).node_and_token[0]);

                const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
                try emitDbgStmt(gz, cursor);

                return gz.addUnNode(.optional_payload_safe_ptr, lhs, node);
            },
            .ref_const => {
                const lhs = try expr(gz, scope, .{ .rl = .ref_const }, tree.nodeData(node).node_and_token[0]);

                const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
                try emitDbgStmt(gz, cursor);

                return gz.addUnNode(.optional_payload_safe_ptr, lhs, node);
            },
            else => {
                const lhs = try expr(gz, scope, .{ .rl = .none }, tree.nodeData(node).node_and_token[0]);

                const cursor = maybeAdvanceSourceCursorToMainToken(gz, node);
                try emitDbgStmt(gz, cursor);

                return rvalue(gz, ri, try gz.addUnNode(.optional_payload_safe, lhs, node), node);
            },
        },
        .block_two,
        .block_two_semicolon,
        .block,
        .block_semicolon,
        => {
            var buf: [2]Ast.Node.Index = undefined;
            const statements = tree.blockStatements(&buf, node).?;
            return blockExpr(gz, scope, ri, node, statements, .normal);
        },
        .enum_literal => if (try ri.rl.resultType(gz, node)) |res_ty| {
            const str_index = try astgen.identAsString(tree.nodeMainToken(node));
            const res = try gz.addPlNode(.decl_literal, node, Zir.Inst.Field{
                .lhs = res_ty,
                .field_name_start = str_index,
            });
            switch (ri.rl) {
                .discard, .none, .ref, .ref_const, .inferred_ptr, .destructure => unreachable, // no result type
                .ty, .coerced_ty => return res, // `decl_literal` does the coercion for us
                .ref_coerced_ty, .ptr => return rvalue(gz, ri, res, node),
            }
        } else return simpleStrTok(gz, ri, tree.nodeMainToken(node), node, .enum_literal),
        .error_value => return simpleStrTok(gz, ri, tree.nodeMainToken(node) + 2, node, .error_value),
        // TODO restore this when implementing https://github.com/ziglang/zig/issues/6025
        // .anyframe_literal => return rvalue(gz, ri, .anyframe_type, node),
        .anyframe_literal => {
            const result = try gz.addUnNode(.anyframe_type, .void_type, node);
            return rvalue(gz, ri, result, node);
        },
        .anyframe_type => {
            const return_type = try typeExpr(gz, scope, tree.nodeData(node).token_and_node[1]);
            const result = try gz.addUnNode(.anyframe_type, return_type, node);
            return rvalue(gz, ri, result, node);
        },
        .@"catch" => {
            const catch_token = tree.nodeMainToken(node);
            const payload_token: ?Ast.TokenIndex = if (tree.tokenTag(catch_token + 1) == .pipe)
                catch_token + 2
            else
                null;
            no_switch_on_err: {
                const capture_token = payload_token orelse break :no_switch_on_err;
                const switch_full = tree.fullSwitch(tree.nodeData(node).node_and_node[1]) orelse break :no_switch_on_err;
                if (switch_full.label_token != null) break :no_switch_on_err; // handled in `orelseCatchExpr`
                if (tree.nodeTag(switch_full.ast.condition) != .identifier) break :no_switch_on_err;
                if (!try astgen.tokenIdentEql(capture_token, tree.nodeMainToken(switch_full.ast.condition))) break :no_switch_on_err;
                return switchExpr(gz, scope, ri.br(), node, switch_full, .@"catch");
            }
            switch (ri.rl) {
                .ref, .ref_const, .ref_coerced_ty => return orelseCatchExpr(
                    gz,
                    scope,
                    ri,
                    node,
                    .is_non_err_ptr,
                    .err_union_payload_unsafe_ptr,
                    .err_union_code_ptr,
                    payload_token,
                ),
                else => return orelseCatchExpr(
                    gz,
                    scope,
                    ri,
                    node,
                    .is_non_err,
                    .err_union_payload_unsafe,
                    .err_union_code,
                    payload_token,
                ),
            }
        },
        .@"orelse" => switch (ri.rl) {
            .ref, .ref_const, .ref_coerced_ty => return orelseCatchExpr(
                gz,
                scope,
                ri,
                node,
                .is_non_null_ptr,
                .optional_payload_unsafe_ptr,
                undefined,
                null,
            ),
            else => return orelseCatchExpr(
                gz,
                scope,
                ri,
                node,
                .is_non_null,
                .optional_payload_unsafe,
                undefined,
                null,
            ),
        },

        .ptr_type_aligned,
        .ptr_type_sentinel,
        .ptr_type,
        .ptr_type_bit_range,
        => return ptrType(gz, scope, ri, node, tree.fullPtrType(node).?),

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
            return containerDecl(gz, scope, ri, node, tree.fullContainerDecl(&buf, node).?, .anon);
        },

        .@"break" => return breakExpr(gz, scope, node),
        .@"continue" => return continueExpr(gz, scope, node),
        .grouped_expression => return expr(gz, scope, ri, tree.nodeData(node).node_and_token[0]),
        .array_type => return arrayType(gz, scope, ri, node),
        .array_type_sentinel => return arrayTypeSentinel(gz, scope, ri, node),
        .char_literal => return charLiteral(gz, ri, node),
        .error_set_decl => return errorSetDecl(gz, ri, node),
        .array_access => return arrayAccess(gz, scope, ri, node),
        .@"comptime" => return comptimeExprAst(gz, scope, ri, node),
        .@"switch", .switch_comma => return switchExpr(gz, scope, ri.br(), node, tree.fullSwitch(node).?, .none),

        .@"nosuspend" => return nosuspendExpr(gz, scope, ri, node),
        .@"suspend" => return suspendExpr(gz, scope, node),
        .@"resume" => return resumeExpr(gz, scope, ri, node),

        .@"try" => return tryExpr(gz, scope, ri, node, tree.nodeData(node).node),

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
            return arrayInitExpr(gz, scope, ri, node, tree.fullArrayInit(&buf, node).?);
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
            return structInitExpr(gz, scope, ri, node, tree.fullStructInit(&buf, node).?);
        },

        .fn_proto_simple,
        .fn_proto_multi,
        .fn_proto_one,
        .fn_proto,
        => {
            var buf: [1]Ast.Node.Index = undefined;
            return fnProtoExpr(gz, scope, ri, node, tree.fullFnProto(&buf, node).?);
        },
    }
}

/// When a name strategy other than `.anon` is available, for instance when analyzing the init expr
/// of a variable declaration, try this function before `expr`/`comptimeExpr`/etc, so that the name
/// strategy can be applied if necessary. If `null` is returned, then `node` does not consume a name
/// strategy, and a normal evaluation function like `expr` should be used instead. Otherwise, `node`
/// does consume a name strategy; the expression has been evaluated like `expr`, but using the given
/// name strategy.
fn nameStratExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    name_strat: Zir.Inst.NameStrategy,
) InnerError!?Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    switch (tree.nodeTag(node)) {
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
        => {
            var buf: [2]Ast.Node.Index = undefined;
            return try containerDecl(gz, scope, ri, node, tree.fullContainerDecl(&buf, node).?, name_strat);
        },
        .builtin_call_two,
        .builtin_call_two_comma,
        .builtin_call,
        .builtin_call_comma,
        => {
            const builtin_token = tree.nodeMainToken(node);
            const builtin_name = tree.tokenSlice(builtin_token);
            const info = BuiltinFn.list.get(builtin_name) orelse return null;
            switch (info.tag) {
                .Enum, .Struct, .Union => {
                    var buf: [2]Ast.Node.Index = undefined;
                    const params = tree.builtinCallParams(&buf, node).?;
                    return try builtinCall(gz, scope, ri, node, params, false, name_strat);
                },
                else => return null,
            }
        },
        else => return null,
    }
}

fn nosuspendExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    const body_node = tree.nodeData(node).node;
    if (gz.nosuspend_node.unwrap()) |nosuspend_node| {
        try astgen.appendErrorNodeNotes(node, "redundant nosuspend block", .{}, &[_]u32{
            try astgen.errNoteNode(nosuspend_node, "other nosuspend block here", .{}),
        });
    }
    gz.nosuspend_node = node.toOptional();
    defer gz.nosuspend_node = .none;
    return expr(gz, scope, ri, body_node);
}

fn suspendExpr(
    gz: *GenZir,
    scope: *Scope,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const gpa = astgen.gpa;
    const tree = astgen.tree;
    const body_node = tree.nodeData(node).node;

    if (gz.nosuspend_node.unwrap()) |nosuspend_node| {
        return astgen.failNodeNotes(node, "suspend inside nosuspend block", .{}, &[_]u32{
            try astgen.errNoteNode(nosuspend_node, "nosuspend block here", .{}),
        });
    }
    if (gz.suspend_node.unwrap()) |suspend_node| {
        return astgen.failNodeNotes(node, "cannot suspend inside suspend block", .{}, &[_]u32{
            try astgen.errNoteNode(suspend_node, "other suspend block here", .{}),
        });
    }

    const suspend_inst = try gz.makeBlockInst(.suspend_block, node);
    try gz.instructions.append(gpa, suspend_inst);

    var suspend_scope = gz.makeSubBlock(scope);
    suspend_scope.suspend_node = node.toOptional();
    defer suspend_scope.unstack();

    const body_result = try fullBodyExpr(&suspend_scope, &suspend_scope.base, .{ .rl = .none }, body_node, .normal);
    if (!gz.refIsNoReturn(body_result)) {
        _ = try suspend_scope.addBreak(.break_inline, suspend_inst, .void_value);
    }
    try suspend_scope.setBlockBody(suspend_inst);

    return suspend_inst.toRef();
}

fn resumeExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;
    const rhs_node = tree.nodeData(node).node;
    const operand = try expr(gz, scope, .{ .rl = .ref }, rhs_node);
    const result = try gz.addUnNode(.@"resume", operand, node);
    return rvalue(gz, ri, result, node);
}

fn fnProtoExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    fn_proto: Ast.full.FnProto,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    if (fn_proto.name_token) |some| {
        return astgen.failTok(some, "function type cannot have a name", .{});
    }

    if (fn_proto.ast.align_expr.unwrap()) |align_expr| {
        return astgen.failNode(align_expr, "function type cannot have an alignment", .{});
    }

    if (fn_proto.ast.addrspace_expr.unwrap()) |addrspace_expr| {
        return astgen.failNode(addrspace_expr, "function type cannot have an addrspace", .{});
    }

    if (fn_proto.ast.section_expr.unwrap()) |section_expr| {
        return astgen.failNode(section_expr, "function type cannot have a linksection", .{});
    }

    const return_type = fn_proto.ast.return_type.unwrap().?;
    const maybe_bang = tree.firstToken(return_type) - 1;
    const is_inferred_error = tree.tokenTag(maybe_bang) == .bang;
    if (is_inferred_error) {
        return astgen.failTok(maybe_bang, "function type cannot have an inferred error set", .{});
    }

    const is_extern = blk: {
        const maybe_extern_token = fn_proto.extern_export_inline_token orelse break :blk false;
        break :blk tree.tokenTag(maybe_extern_token) == .keyword_extern;
    };
    assert(!is_extern);

    return fnProtoExprInner(gz, scope, ri, node, fn_proto, false);
}

fn fnProtoExprInner(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    fn_proto: Ast.full.FnProto,
    implicit_ccc: bool,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    var block_scope = gz.makeSubBlock(scope);
    defer block_scope.unstack();

    const block_inst = try gz.makeBlockInst(.block_inline, node);

    var noalias_bits: u32 = 0;
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

            const param_name = if (param.name_token) |name_token| blk: {
                if (mem.eql(u8, "_", tree.tokenSlice(name_token)))
                    break :blk .empty;

                break :blk try astgen.identAsString(name_token);
            } else .empty;

            if (is_anytype) {
                const name_token = param.name_token orelse param.anytype_ellipsis3.?;

                const tag: Zir.Inst.Tag = if (is_comptime)
                    .param_anytype_comptime
                else
                    .param_anytype;
                _ = try block_scope.addStrTok(tag, param_name, name_token);
            } else {
                const param_type_node = param.type_expr.?;
                var param_gz = block_scope.makeSubBlock(scope);
                defer param_gz.unstack();
                param_gz.is_comptime = true;
                const param_type = try fullBodyExpr(&param_gz, scope, coerced_type_ri, param_type_node, .normal);
                const param_inst_expected: Zir.Inst.Index = @enumFromInt(astgen.instructions.len + 1);
                _ = try param_gz.addBreakWithSrcNode(.break_inline, param_inst_expected, param_type, param_type_node);
                const name_token = param.name_token orelse tree.nodeMainToken(param_type_node);
                const tag: Zir.Inst.Tag = if (is_comptime) .param_comptime else .param;
                // We pass `prev_param_insts` as `&.{}` here because a function prototype can't refer to previous
                // arguments (we haven't set up scopes here).
                const param_inst = try block_scope.addParam(&param_gz, &.{}, false, tag, name_token, param_name);
                assert(param_inst_expected == param_inst);
            }
        }
        break :is_var_args false;
    };

    const cc: Zir.Inst.Ref = if (fn_proto.ast.callconv_expr.unwrap()) |callconv_expr|
        try comptimeExpr(
            &block_scope,
            scope,
            .{ .rl = .{ .coerced_ty = try block_scope.addBuiltinValue(callconv_expr, .calling_convention) } },
            callconv_expr,
            .@"callconv",
        )
    else if (implicit_ccc)
        try block_scope.addBuiltinValue(node, .calling_convention_c)
    else
        .none;

    const ret_ty_node = fn_proto.ast.return_type.unwrap().?;
    const ret_ty = try comptimeExpr(&block_scope, scope, coerced_type_ri, ret_ty_node, .fn_ret_ty);

    const result = try block_scope.addFunc(.{
        .src_node = fn_proto.ast.proto_node,

        .cc_ref = cc,
        .cc_gz = null,
        .ret_ref = ret_ty,
        .ret_gz = null,

        .ret_param_refs = &.{},
        .param_insts = &.{},
        .ret_ty_is_generic = false,

        .param_block = block_inst,
        .body_gz = null,
        .is_var_args = is_var_args,
        .is_inferred_error = false,
        .is_noinline = false,
        .noalias_bits = noalias_bits,

        .proto_hash = undefined, // ignored for `body_gz == null`
    });

    _ = try block_scope.addBreak(.break_inline, block_inst, result);
    try block_scope.setBlockBody(block_inst);
    try gz.instructions.append(astgen.gpa, block_inst);

    return rvalue(gz, ri, block_inst.toRef(), fn_proto.ast.proto_node);
}

fn arrayInitExpr(
    gz: *GenZir,
    scope: *Scope,
    ri: ResultInfo,
    node: Ast.Node.Index,
    array_init: Ast.full.ArrayInit,
) InnerError!Zir.Inst.Ref {
    const astgen = gz.astgen;
    const tree = astgen.tree;

    assert(array_init.ast.elements.len != 0); // Otherwise it would be struct init.

    const array_ty: Zir.Inst.Ref, const elem_ty: Zir.Inst.Ref = inst: {
        const type_expr = array_init.ast.type_expr.unwrap() orelse break :inst .{ .none, .none };

        infer: {
            const array_type: Ast.full.ArrayType = tree.fullArrayType(type_expr) orelse break :infer;
            // This intentionally does not support `@"_"` syntax.
            if (tree.nodeTag(array_type.ast.elem_count) == .identifier and
                mem.eql(u8, tree.tokenSlice(tree.nodeMainToken(array_type.ast.elem_count)), "_"))
            {
                const len_inst = try gz.addInt(array_init.ast.elements.len);
                const elem_type = try typeExpr(gz, scope, array_type.ast.elem_type);
                if (array_type.ast.sentinel == .none) {
                    const array_type_inst = try gz.addPlNode(.array_type, type_expr, Zir.Inst.Bin{
                        .lhs = len_inst,
                        .rhs = elem_type,
                    });
                    break :inst .{ array_type_inst, elem_type };
                } else {
                    const sentinel_node = array_type.ast.sentinel.unwrap().?;
                    const sentinel = try comptimeExpr(gz, scope, .{ .rl = .{ .ty = elem_type } }, sentinel_node, .array_sentinel);
                    const array_type_inst = try gz.addPlNode(
                        .array_type_sentinel,
                        type_expr,
                        Zir.Inst.ArrayTypeSentinel{
                            .len = len_inst,
                            .elem_type = elem_type,
                            .sentinel = sentinel,
                        },
                    );
                    break :inst .{ array_type_inst, elem_type };
                }
            }
        }
        const array_type_inst = try typeExpr(gz, scope, type_expr);
        _ = try gz.addPlNode(.validate_array_init_ty, node, Zir.Inst.ArrayInit{
            .ty = array_type_inst,
            .init_count = @intCast(array_init.ast.elements.len),
        });
        break :inst .{ array_type_inst, .none };
    };

    if (array_ty != .none) {
        // Typed inits do not use RLS for language simplicity.
        switch (ri.rl) {
            .discard => {
                if (elem_ty != .none) {
                    const elem_ri: ResultInfo = .{ .rl = .{ .ty = elem_ty } };
                    for (array_init.ast.elements) |elem_init| {
                        _ = try expr(gz, scope, elem_ri, elem_init);
                    }
                } else {
                    for (array_init.ast.elements, 0..) |elem_init, i| {
                        const this_elem_ty = try gz.add(.{
                            .tag = .array_init_elem_type,
                            .data = .{ .bin = .{
                                .lhs = array_ty,
                                .rhs = @enumFromInt(i),
                            } },
                        });
                        _ = try expr(gz, scope, .{ .rl = .{ .ty = this_elem_ty } }, elem_init);
                    }
                }
                return .void_value;
            },
            .ref, .ref_const => return arrayInitExprTyped(gz, scope, node, array_init.ast.elements, array_ty, elem_ty, true),
            else => {
                const array_inst = try arrayInitExprTyped(gz, scope, node, array_init.ast.elements, array_ty, elem_ty, false);
                return rvalue(gz, ri, array_inst, node);
            },
        }
    }

    switch (ri.rl) {
        .none => return arrayInitExprAnon(gz, scope, node, array_init.ast.elements),
        .discard => {
            for (array_init.ast.elements) |elem_init| {
                _ = try expr(gz, scope, .{ .rl = .discard }, elem_init);
            }
            return Zir.Inst.Ref.void_value;
        },
        .ref, .ref_const => {
            const result = try arrayInitExprAnon(gz, scope, node, array_init.ast.elements);
            return gz.addUnTok(.ref, result, tree.firstToken(node));
        },
        .ref_coerced_ty => |ptr_ty_inst| {
            const dest_arr_ty_inst = try gz.addPlNode(.validate_array_init_ref_ty, node, Zir.Inst.ArrayInitRefTy{
                .ptr_ty = ptr_ty_inst,
                .elem_count = @intCast(array_init.ast.elements.len),
            });
            return arrayInitExprTyped(gz, scope, node, array_init.ast.elements, dest_arr_ty_inst, .none, true);
        },
        .ty, .coerced_ty => |result_ty_inst| {
            _ = try gz.addPlNode(.validate_array_init_result_ty, node, Zir.Inst.ArrayInit{
                .ty = result_ty_inst,
                .init_count = @intCast(array_init.ast.elements.len),
            });
            return arrayInitExprTyped(gz, scope, node, array_init.ast.elements, result_ty_inst, .none, false);
        },
        .ptr => |ptr| {
            try arrayInitExprPtr(gz, scope, node, array_init.ast.elements, ptr.inst);
            return .void_value;
        },
        .inferred_ptr => {
            // We can't get elem pointers of an untyped inferred alloc, so must perform a
            // standard anonymous initialization followed by an rvalue store.
            // See corresponding logic in structInitExpr.
            const result = try arrayInitExprAnon(gz, scope, node, array_init.ast.elements);
            return rvalue(gz, ri, result, node);
        },
        .destructure => |destructure| {
            // Untyped init - destructure directly into result pointers
            if (array_init.ast.elements.len != destructure.components.len) {
                return astgen.failNodeNotes(node, "expected {} elements for destructure, found {}", .{
                    destructure.components.len,
                    array_init.ast.elements.len,
                }, &.{
                    try astgen.errNoteNode(destructure.src_node, "result destructured here", .{}),
                });
            }
            for (array_init.ast.elements, destructure.components) |elem_init, ds_comp| {
                const elem_ri: ResultInfo = .{ .rl = switch (ds_comp) {
                    .typed_ptr => |ptr_rl| .{ .ptr = ptr_rl
```
