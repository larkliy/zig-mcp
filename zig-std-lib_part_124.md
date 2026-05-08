```
len != 0) blk: {
        const notes_start = astgen.extra.items.len;
        try astgen.extra.ensureTotalCapacity(gpa, notes_start + 1 + notes.len);
        astgen.extra.appendAssumeCapacity(@intCast(notes.len));
        astgen.extra.appendSliceAssumeCapacity(notes);
        break :blk @intCast(notes_start);
    } else 0;
    try astgen.compile_errors.append(gpa, .{
        .msg = msg,
        .node = .none,
        .token = .fromToken(token),
        .byte_offset = byte_offset,
        .notes = notes_index,
    });
}

fn errNoteTok(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    comptime format: []const u8,
    args: anytype,
) Allocator.Error!u32 {
    return errNoteTokOff(astgen, token, 0, format, args);
}

fn errNoteTokOff(
    astgen: *AstGen,
    token: Ast.TokenIndex,
    byte_offset: u32,
    comptime format: []const u8,
    args: anytype,
) Allocator.Error!u32 {
    @branchHint(.cold);
    const string_bytes = &astgen.string_bytes;
    const msg: Zir.NullTerminatedString = @enumFromInt(string_bytes.items.len);
    try string_bytes.print(astgen.gpa, format ++ "\x00", args);
    return astgen.addExtra(Zir.Inst.CompileErrors.Item{
        .msg = msg,
        .node = .none,
        .token = .fromToken(token),
        .byte_offset = byte_offset,
        .notes = 0,
    });
}

fn errNoteNode(
    astgen: *AstGen,
    node: Ast.Node.Index,
    comptime format: []const u8,
    args: anytype,
) Allocator.Error!u32 {
    @branchHint(.cold);
    const string_bytes = &astgen.string_bytes;
    const msg: Zir.NullTerminatedString = @enumFromInt(string_bytes.items.len);
    try string_bytes.print(astgen.gpa, format ++ "\x00", args);
    return astgen.addExtra(Zir.Inst.CompileErrors.Item{
        .msg = msg,
        .node = node.toOptional(),
        .token = .none,
        .byte_offset = 0,
        .notes = 0,
    });
}

fn identAsString(astgen: *AstGen, ident_token: Ast.TokenIndex) !Zir.NullTerminatedString {
    const gpa = astgen.gpa;
    const string_bytes = &astgen.string_bytes;
    const str_index: u32 = @intCast(string_bytes.items.len);
    try astgen.appendIdentStr(ident_token, string_bytes);
    const key: []const u8 = string_bytes.items[str_index..];
    const gop = try astgen.string_table.getOrPutContextAdapted(gpa, key, StringIndexAdapter{
        .bytes = string_bytes,
    }, StringIndexContext{
        .bytes = string_bytes,
    });
    if (gop.found_existing) {
        string_bytes.shrinkRetainingCapacity(str_index);
        return @enumFromInt(gop.key_ptr.*);
    } else {
        gop.key_ptr.* = str_index;
        try string_bytes.append(gpa, 0);
        return @enumFromInt(str_index);
    }
}

const IndexSlice = struct { index: Zir.NullTerminatedString, len: u32 };

fn strLitAsString(astgen: *AstGen, str_lit_token: Ast.TokenIndex) !IndexSlice {
    const gpa = astgen.gpa;
    const string_bytes = &astgen.string_bytes;
    const str_index: u32 = @intCast(string_bytes.items.len);
    const token_bytes = astgen.tree.tokenSlice(str_lit_token);
    try astgen.parseStrLit(str_lit_token, string_bytes, token_bytes, 0);
    const key: []const u8 = string_bytes.items[str_index..];
    if (std.mem.findScalar(u8, key, 0)) |_| return .{
        .index = @enumFromInt(str_index),
        .len = @intCast(key.len),
    };
    const gop = try astgen.string_table.getOrPutContextAdapted(gpa, key, StringIndexAdapter{
        .bytes = string_bytes,
    }, StringIndexContext{
        .bytes = string_bytes,
    });
    if (gop.found_existing) {
        string_bytes.shrinkRetainingCapacity(str_index);
        return .{
            .index = @enumFromInt(gop.key_ptr.*),
            .len = @intCast(key.len),
        };
    } else {
        gop.key_ptr.* = str_index;
        // Still need a null byte because we are using the same table
        // to lookup null terminated strings, so if we get a match, it has to
        // be null terminated for that to work.
        try string_bytes.append(gpa, 0);
        return .{
            .index = @enumFromInt(str_index),
            .len = @intCast(key.len),
        };
    }
}

fn strLitNodeAsString(astgen: *AstGen, node: Ast.Node.Index) !IndexSlice {
    const tree = astgen.tree;

    const start, const end = tree.nodeData(node).token_and_token;

    const gpa = astgen.gpa;
    const string_bytes = &astgen.string_bytes;
    const str_index = string_bytes.items.len;

    // First line: do not append a newline.
    var tok_i = start;
    {
        const slice = tree.tokenSlice(tok_i);
        const line_bytes = slice[2..];
        try string_bytes.appendSlice(gpa, line_bytes);
        tok_i += 1;
    }
    // Following lines: each line prepends a newline.
    while (tok_i <= end) : (tok_i += 1) {
        const slice = tree.tokenSlice(tok_i);
        const line_bytes = slice[2..];
        try string_bytes.ensureUnusedCapacity(gpa, line_bytes.len + 1);
        string_bytes.appendAssumeCapacity('\n');
        string_bytes.appendSliceAssumeCapacity(line_bytes);
    }
    const len = string_bytes.items.len - str_index;
    try string_bytes.append(gpa, 0);
    return IndexSlice{
        .index = @enumFromInt(str_index),
        .len = @intCast(len),
    };
}

const Scope = struct {
    tag: Tag,

    fn cast(base: *Scope, comptime T: type) ?*T {
        if (T == Defer) {
            switch (base.tag) {
                .defer_normal, .defer_error => return @alignCast(@fieldParentPtr("base", base)),
                else => return null,
            }
        }
        if (T == Namespace) {
            switch (base.tag) {
                .namespace => return @alignCast(@fieldParentPtr("base", base)),
                else => return null,
            }
        }
        if (base.tag != T.base_tag)
            return null;

        return @alignCast(@fieldParentPtr("base", base));
    }

    fn parent(base: *Scope) ?*Scope {
        return switch (base.tag) {
            .gen_zir => base.cast(GenZir).?.parent,
            .local_val => base.cast(LocalVal).?.parent,
            .local_ptr => base.cast(LocalPtr).?.parent,
            .defer_normal, .defer_error => base.cast(Defer).?.parent,
            .namespace => base.cast(Namespace).?.parent,
            .top => null,
        };
    }

    fn unwrap(base: *Scope) Unwrapped {
        return switch (base.tag) {
            inline else => |tag| @unionInit(
                Unwrapped,
                @tagName(tag),
                @alignCast(@fieldParentPtr("base", base)),
            ),
        };
    }

    const Unwrapped = union(Tag) {
        gen_zir: *GenZir,
        local_val: *LocalVal,
        local_ptr: *LocalPtr,
        defer_normal: *Defer,
        defer_error: *Defer,
        namespace: *Namespace,
        top: *Top,
    };

    const Tag = enum {
        gen_zir,
        local_val,
        local_ptr,
        defer_normal,
        defer_error,
        namespace,
        top,
    };

    /// The category of identifier. These tag names are user-visible in compile errors.
    const IdCat = enum {
        @"function parameter",
        @"local constant",
        @"local variable",
        @"switch tag capture",
        capture,
    };

    /// This is always a `const` local and importantly the `inst` is a value type, not a pointer.
    /// This structure lives as long as the AST generation of the Block
    /// node that contains the variable.
    const LocalVal = struct {
        const base_tag: Tag = .local_val;
        base: Scope = Scope{ .tag = base_tag },
        /// Parents can be: `LocalVal`, `LocalPtr`, `GenZir`, `Defer`, `Namespace`.
        parent: *Scope,
        gen_zir: *GenZir,
        inst: Zir.Inst.Ref,
        /// Source location of the corresponding variable declaration.
        token_src: Ast.TokenIndex,
        /// Track the first identifier where it is referenced.
        /// .none means never referenced.
        used: Ast.OptionalTokenIndex = .none,
        /// Track the identifier where it is discarded, like this `_ = foo;`.
        /// .none means never discarded.
        discarded: Ast.OptionalTokenIndex = .none,
        is_used_or_discarded: ?*bool = null,
        /// String table index.
        name: Zir.NullTerminatedString,
        id_cat: IdCat,
    };

    /// This could be a `const` or `var` local. It has a pointer instead of a value.
    /// This structure lives as long as the AST generation of the Block
    /// node that contains the variable.
    const LocalPtr = struct {
        const base_tag: Tag = .local_ptr;
        base: Scope = Scope{ .tag = base_tag },
        /// Parents can be: `LocalVal`, `LocalPtr`, `GenZir`, `Defer`, `Namespace`.
        parent: *Scope,
        gen_zir: *GenZir,
        ptr: Zir.Inst.Ref,
        /// Source location of the corresponding variable declaration.
        token_src: Ast.TokenIndex,
        /// Track the first identifier where it is referenced.
        /// .none means never referenced.
        used: Ast.OptionalTokenIndex = .none,
        /// Track the identifier where it is discarded, like this `_ = foo;`.
        /// .none means never discarded.
        discarded: Ast.OptionalTokenIndex = .none,
        /// Whether this value is used as an lvalue after initialization.
        /// If not, we know it can be `const`, so will emit a compile error if it is `var`.
        used_as_lvalue: bool = false,
        /// String table index.
        name: Zir.NullTerminatedString,
        id_cat: IdCat,
        /// true means we find out during Sema whether the value is comptime.
        /// false means it is already known at AstGen the value is runtime-known.
        maybe_comptime: bool,
    };

    const Defer = struct {
        base: Scope,
        /// Parents can be: `LocalVal`, `LocalPtr`, `GenZir`, `Defer`, `Namespace`.
        parent: *Scope,
        index: u32,
        len: u32,
        remapped_err_code: Zir.Inst.OptionalIndex = .none,
    };

    /// Represents a global scope that has any number of declarations in it.
    /// Each declaration has this as the parent scope.
    const Namespace = struct {
        const base_tag: Tag = .namespace;
        base: Scope = Scope{ .tag = base_tag },

        /// Parents can be: `LocalVal`, `LocalPtr`, `GenZir`, `Defer`, `Namespace`.
        parent: *Scope,
        /// Maps string table index to the source location of declaration,
        /// for the purposes of reporting name shadowing compile errors.
        decls: std.AutoHashMapUnmanaged(Zir.NullTerminatedString, Ast.Node.Index) = .empty,
        node: Ast.Node.Index,
        inst: Zir.Inst.Index,
        maybe_generic: bool,

        /// The astgen scope containing this namespace.
        /// Only valid during astgen.
        declaring_gz: ?*GenZir,

        /// Set of captures used by this namespace.
        captures: std.AutoArrayHashMapUnmanaged(Zir.Inst.Capture, Zir.NullTerminatedString) = .empty,

        fn deinit(self: *Namespace, gpa: Allocator) void {
            self.decls.deinit(gpa);
            self.captures.deinit(gpa);
            self.* = undefined;
        }
    };

    const Top = struct {
        const base_tag: Scope.Tag = .top;
        base: Scope = Scope{ .tag = base_tag },
    };
};

/// This is a temporary structure; references to it are valid only
/// while constructing a `Zir`.
const GenZir = struct {
    const base_tag: Scope.Tag = .gen_zir;
    base: Scope = Scope{ .tag = base_tag },
    /// Whether we're already in a scope known to be comptime. This is set
    /// whenever we know Sema will analyze the current block with `is_comptime`,
    /// for instance when we're within a `struct_decl` or a `block_comptime`.
    is_comptime: bool,
    /// Whether we're in an expression within a `@TypeOf` operand. In this case,
    /// closure of runtime variables is permitted where it is usually not.
    is_typeof: bool = false,
    /// This is set to true for a `GenZir` of a `block_inline`, indicating that
    /// exits from this block should use `break_inline` rather than `break`.
    is_inline: bool = false,
    c_import: bool = false,
    /// The containing decl AST node.
    decl_node_index: Ast.Node.Index,
    /// The containing decl line index, absolute.
    decl_line: u32,
    /// Parents can be: `LocalVal`, `LocalPtr`, `GenZir`, `Defer`, `Namespace`.
    parent: *Scope,
    /// All `GenZir` scopes for the same ZIR share this.
    astgen: *AstGen,
    /// Keeps track of the list of instructions in this scope. Possibly shared.
    /// Indexes to instructions in `astgen`.
    instructions: *ArrayList(Zir.Inst.Index),
    /// A sub-block may share its instructions ArrayList with containing GenZir,
    /// if use is strictly nested. This saves prior size of list for unstacking.
    instructions_top: usize,
    label: ?Label = null,
    /// If `true`, unlabeled `break` and `continue` exprs can target this `GenZir`.
    allow_unlabeled_control_flow: bool = false,
    /// If `label` is `null` and `unlabeled_control_flow_target` is `false`,
    /// this is unused and may be `undefined`.
    /// Otherwise, this is the target for a `break` instruction when a `break`
    /// targets this `GenZir`.
    break_target: Zir.Inst.Index = undefined,
    /// If `label` is `null` and `unlabeled_control_flow_target` is `false`,
    /// this is unused and may be `undefined`.
    continue_target: union(enum) {
        /// A `continue` cannot target this `GenZir`; emit an error.
        none,
        /// Emit a `break` instruction targeting this block.
        @"break": Zir.Inst.Index,
        /// Emit a `switch_continue` instruction targeting this `switch_block`.
        switch_continue: Zir.Inst.Index,
    } = undefined,
    /// Only valid when setBreakResultInfo is called.
    break_result_info: AstGen.ResultInfo = undefined,
    /// If `continue_target` is *not* `switch_continue`, this is unused and may
    /// be `undefined`.
    continue_result_info: AstGen.ResultInfo = undefined,

    suspend_node: Ast.Node.OptionalIndex = .none,
    nosuspend_node: Ast.Node.OptionalIndex = .none,
    /// Set if this GenZir is a defer.
    cur_defer_node: Ast.Node.OptionalIndex = .none,
    // Set if this GenZir is a defer or it is inside a defer.
    any_defer_node: Ast.Node.OptionalIndex = .none,

    const unstacked_top = std.math.maxInt(usize);
    /// Call unstack before adding any new instructions to containing GenZir.
    fn unstack(self: *GenZir) void {
        if (self.instructions_top != unstacked_top) {
            self.instructions.items.len = self.instructions_top;
            self.instructions_top = unstacked_top;
        }
    }

    fn isEmpty(self: *const GenZir) bool {
        return (self.instructions_top == unstacked_top) or
            (self.instructions.items.len == self.instructions_top);
    }

    fn instructionsSlice(self: *const GenZir) []Zir.Inst.Index {
        return if (self.instructions_top == unstacked_top)
            &[0]Zir.Inst.Index{}
        else
            self.instructions.items[self.instructions_top..];
    }

    fn instructionsSliceUpto(self: *const GenZir, stacked_gz: *GenZir) []Zir.Inst.Index {
        return if (self.instructions_top == unstacked_top)
            &[0]Zir.Inst.Index{}
        else if (self.instructions == stacked_gz.instructions and stacked_gz.instructions_top != unstacked_top)
            self.instructions.items[self.instructions_top..stacked_gz.instructions_top]
        else
            self.instructions.items[self.instructions_top..];
    }

    fn instructionsSliceUptoOpt(gz: *const GenZir, maybe_stacked_gz: ?*GenZir) []Zir.Inst.Index {
        if (maybe_stacked_gz) |stacked_gz| {
            return gz.instructionsSliceUpto(stacked_gz);
        } else {
            return gz.instructionsSlice();
        }
    }

    fn makeSubBlock(gz: *GenZir, scope: *Scope) GenZir {
        return .{
            .is_comptime = gz.is_comptime,
            .is_typeof = gz.is_typeof,
            .c_import = gz.c_import,
            .decl_node_index = gz.decl_node_index,
            .decl_line = gz.decl_line,
            .parent = scope,
            .astgen = gz.astgen,
            .suspend_node = gz.suspend_node,
            .nosuspend_node = gz.nosuspend_node,
            .any_defer_node = gz.any_defer_node,
            .instructions = gz.instructions,
            .instructions_top = gz.instructions.items.len,
        };
    }

    const Label = struct {
        token: Ast.TokenIndex,
        used: bool = false,
        used_for_continue: bool = false,
    };

    /// Assumes nothing stacked on `gz`.
    fn endsWithNoReturn(gz: GenZir) bool {
        if (gz.isEmpty()) return false;
        const tags = gz.astgen.instructions.items(.tag);
        const last_inst = gz.instructions.items[gz.instructions.items.len - 1];
        return tags[@intFromEnum(last_inst)].isNoReturn();
    }

    /// TODO all uses of this should be replaced with uses of `endsWithNoReturn`.
    fn refIsNoReturn(gz: GenZir, inst_ref: Zir.Inst.Ref) bool {
        if (inst_ref == .unreachable_value) return true;
        if (inst_ref.toIndex()) |inst_index| {
            return gz.astgen.instructions.items(.tag)[@intFromEnum(inst_index)].isNoReturn();
        }
        return false;
    }

    fn nodeIndexToRelative(gz: GenZir, node_index: Ast.Node.Index) Ast.Node.Offset {
        return gz.decl_node_index.toOffset(node_index);
    }

    fn tokenIndexToRelative(gz: GenZir, token: Ast.TokenIndex) Ast.TokenOffset {
        return .init(gz.srcToken(), token);
    }

    fn srcToken(gz: GenZir) Ast.TokenIndex {
        return gz.astgen.tree.firstToken(gz.decl_node_index);
    }

    fn setBreakResultInfo(gz: *GenZir, parent_ri: AstGen.ResultInfo) void {
        // Depending on whether the result location is a pointer or value, different
        // ZIR needs to be generated. In the former case we rely on storing to the
        // pointer to communicate the result, and use breakvoid; in the latter case
        // the block break instructions will have the result values.
        switch (parent_ri.rl) {
            .coerced_ty => |ty_inst| {
                // Type coercion needs to happen before breaks.
                gz.break_result_info = .{ .rl = .{ .ty = ty_inst }, .ctx = parent_ri.ctx };
            },
            .discard => {
                // We don't forward the result context here. This prevents
                // "unnecessary discard" errors from being caused by expressions
                // far from the actual discard, such as a `break` from a
                // discarded block.
                gz.break_result_info = .{ .rl = .discard };
            },
            else => {
                gz.break_result_info = parent_ri;
            },
        }
    }

    /// Assumes nothing stacked on `gz`. Unstacks `gz`.
    fn setBoolBrBody(gz: *GenZir, bool_br: Zir.Inst.Index, bool_br_lhs: Zir.Inst.Ref) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;
        const body = gz.instructionsSlice();
        const body_len = astgen.countBodyLenAfterFixups(body);
        try astgen.extra.ensureUnusedCapacity(
            gpa,
            @typeInfo(Zir.Inst.BoolBr).@"struct".fields.len + body_len,
        );
        const zir_datas = astgen.instructions.items(.data);
        zir_datas[@intFromEnum(bool_br)].pl_node.payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.BoolBr{
            .lhs = bool_br_lhs,
            .body_len = body_len,
        });
        astgen.appendBodyWithFixups(body);
        gz.unstack();
    }

    /// Assumes nothing stacked on `gz`. Unstacks `gz`.
    /// Asserts `inst` is not a `block_comptime`.
    fn setBlockBody(gz: *GenZir, inst: Zir.Inst.Index) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;
        const body = gz.instructionsSlice();
        const body_len = astgen.countBodyLenAfterFixups(body);

        const zir_tags = astgen.instructions.items(.tag);
        assert(zir_tags[@intFromEnum(inst)] != .block_comptime); // use `setComptimeBlockBody` instead

        try astgen.extra.ensureUnusedCapacity(
            gpa,
            @typeInfo(Zir.Inst.Block).@"struct".fields.len + body_len,
        );
        const zir_datas = astgen.instructions.items(.data);
        zir_datas[@intFromEnum(inst)].pl_node.payload_index = astgen.addExtraAssumeCapacity(
            Zir.Inst.Block{ .body_len = body_len },
        );
        astgen.appendBodyWithFixups(body);
        gz.unstack();
    }

    /// Assumes nothing stacked on `gz`. Unstacks `gz`.
    /// Asserts `inst` is a `block_comptime`.
    fn setBlockComptimeBody(gz: *GenZir, inst: Zir.Inst.Index, comptime_reason: std.zig.SimpleComptimeReason) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;
        const body = gz.instructionsSlice();
        const body_len = astgen.countBodyLenAfterFixups(body);

        const zir_tags = astgen.instructions.items(.tag);
        assert(zir_tags[@intFromEnum(inst)] == .block_comptime); // use `setBlockBody` instead

        try astgen.extra.ensureUnusedCapacity(
            gpa,
            @typeInfo(Zir.Inst.BlockComptime).@"struct".fields.len + body_len,
        );
        const zir_datas = astgen.instructions.items(.data);
        zir_datas[@intFromEnum(inst)].pl_node.payload_index = astgen.addExtraAssumeCapacity(
            Zir.Inst.BlockComptime{
                .reason = comptime_reason,
                .body_len = body_len,
            },
        );
        astgen.appendBodyWithFixups(body);
        gz.unstack();
    }

    /// Assumes nothing stacked on `gz`. Unstacks `gz`.
    fn setTryBody(gz: *GenZir, inst: Zir.Inst.Index, operand: Zir.Inst.Ref) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;
        const body = gz.instructionsSlice();
        const body_len = astgen.countBodyLenAfterFixups(body);
        try astgen.extra.ensureUnusedCapacity(
            gpa,
            @typeInfo(Zir.Inst.Try).@"struct".fields.len + body_len,
        );
        const zir_datas = astgen.instructions.items(.data);
        zir_datas[@intFromEnum(inst)].pl_node.payload_index = astgen.addExtraAssumeCapacity(
            Zir.Inst.Try{
                .operand = operand,
                .body_len = body_len,
            },
        );
        astgen.appendBodyWithFixups(body);
        gz.unstack();
    }

    /// Must be called with the following stack set up:
    ///  * gz (bottom)
    ///  * ret_gz
    ///  * cc_gz
    ///  * body_gz (top)
    /// Unstacks all of those except for `gz`.
    fn addFunc(
        gz: *GenZir,
        args: struct {
            src_node: Ast.Node.Index,
            lbrace_line: u32 = 0,
            lbrace_column: u32 = 0,
            param_block: Zir.Inst.Index,

            ret_gz: ?*GenZir,
            body_gz: ?*GenZir,
            cc_gz: ?*GenZir,

            ret_param_refs: []Zir.Inst.Index,
            param_insts: []Zir.Inst.Index, // refs to params in `body_gz` should still be in `astgen.ref_table`
            ret_ty_is_generic: bool,

            cc_ref: Zir.Inst.Ref,
            ret_ref: Zir.Inst.Ref,

            noalias_bits: u32,
            is_var_args: bool,
            is_inferred_error: bool,
            is_noinline: bool,

            /// Ignored if `body_gz == null`.
            proto_hash: std.zig.SrcHash,
        },
    ) !Zir.Inst.Ref {
        assert(args.src_node != .root);
        const astgen = gz.astgen;
        const gpa = astgen.gpa;
        const ret_ref = if (args.ret_ref == .void_type) .none else args.ret_ref;
        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);

        const body, const cc_body, const ret_body = bodies: {
            var stacked_gz: ?*GenZir = null;
            const body: []const Zir.Inst.Index = if (args.body_gz) |body_gz| body: {
                const body = body_gz.instructionsSliceUptoOpt(stacked_gz);
                stacked_gz = body_gz;
                break :body body;
            } else &.{};
            const cc_body: []const Zir.Inst.Index = if (args.cc_gz) |cc_gz| body: {
                const cc_body = cc_gz.instructionsSliceUptoOpt(stacked_gz);
                stacked_gz = cc_gz;
                break :body cc_body;
            } else &.{};
            const ret_body: []const Zir.Inst.Index = if (args.ret_gz) |ret_gz| body: {
                const ret_body = ret_gz.instructionsSliceUptoOpt(stacked_gz);
                stacked_gz = ret_gz;
                break :body ret_body;
            } else &.{};
            break :bodies .{ body, cc_body, ret_body };
        };

        var src_locs_and_hash_buffer: [7]u32 = undefined;
        const src_locs_and_hash: []const u32 = if (args.body_gz != null) src_locs_and_hash: {
            const tree = astgen.tree;
            const fn_decl = args.src_node;
            const block = switch (tree.nodeTag(fn_decl)) {
                .fn_decl => tree.nodeData(fn_decl).node_and_node[1],
                .test_decl => tree.nodeData(fn_decl).opt_token_and_node[1],
                else => unreachable,
            };
            const rbrace_start = tree.tokenStart(tree.lastToken(block));
            astgen.advanceSourceCursor(rbrace_start);
            const rbrace_line: u32 = @intCast(astgen.source_line - gz.decl_line);
            const rbrace_column: u32 = @intCast(astgen.source_column);

            const columns = args.lbrace_column | (rbrace_column << 16);

            const proto_hash_arr: [4]u32 = @bitCast(args.proto_hash);

            src_locs_and_hash_buffer = .{
                args.lbrace_line,
                rbrace_line,
                columns,
                proto_hash_arr[0],
                proto_hash_arr[1],
                proto_hash_arr[2],
                proto_hash_arr[3],
            };
            break :src_locs_and_hash &src_locs_and_hash_buffer;
        } else &.{};

        const body_len = astgen.countBodyLenAfterFixupsExtraRefs(body, args.param_insts);

        const tag: Zir.Inst.Tag, const payload_index: u32 = if (args.cc_ref != .none or
            args.is_var_args or args.noalias_bits != 0 or args.is_noinline)
        inst_info: {
            try astgen.extra.ensureUnusedCapacity(
                gpa,
                @typeInfo(Zir.Inst.FuncFancy).@"struct".fields.len +
                    fancyFnExprExtraLen(astgen, &.{}, cc_body, args.cc_ref) +
                    fancyFnExprExtraLen(astgen, args.ret_param_refs, ret_body, ret_ref) +
                    body_len + src_locs_and_hash.len +
                    @intFromBool(args.noalias_bits != 0),
            );
            const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.FuncFancy{
                .param_block = args.param_block,
                .body_len = body_len,
                .bits = .{
                    .is_var_args = args.is_var_args,
                    .is_inferred_error = args.is_inferred_error,
                    .is_noinline = args.is_noinline,
                    .has_any_noalias = args.noalias_bits != 0,

                    .has_cc_ref = args.cc_ref != .none,
                    .has_ret_ty_ref = ret_ref != .none,

                    .has_cc_body = cc_body.len != 0,
                    .has_ret_ty_body = ret_body.len != 0,

                    .ret_ty_is_generic = args.ret_ty_is_generic,
                },
            });

            const zir_datas = astgen.instructions.items(.data);
            if (cc_body.len != 0) {
                astgen.extra.appendAssumeCapacity(astgen.countBodyLenAfterFixups(cc_body));
                astgen.appendBodyWithFixups(cc_body);
                const break_extra = zir_datas[@intFromEnum(cc_body[cc_body.len - 1])].@"break".payload_index;
                astgen.extra.items[break_extra + std.meta.fieldIndex(Zir.Inst.Break, "block_inst").?] =
                    @intFromEnum(new_index);
            } else if (args.cc_ref != .none) {
                astgen.extra.appendAssumeCapacity(@intFromEnum(args.cc_ref));
            }
            if (ret_body.len != 0) {
                astgen.extra.appendAssumeCapacity(
                    astgen.countBodyLenAfterFixups(args.ret_param_refs) +
                        astgen.countBodyLenAfterFixups(ret_body),
                );
                astgen.appendBodyWithFixups(args.ret_param_refs);
                astgen.appendBodyWithFixups(ret_body);
                const break_extra = zir_datas[@intFromEnum(ret_body[ret_body.len - 1])].@"break".payload_index;
                astgen.extra.items[break_extra + std.meta.fieldIndex(Zir.Inst.Break, "block_inst").?] =
                    @intFromEnum(new_index);
            } else if (ret_ref != .none) {
                astgen.extra.appendAssumeCapacity(@intFromEnum(ret_ref));
            }

            if (args.noalias_bits != 0) {
                astgen.extra.appendAssumeCapacity(args.noalias_bits);
            }

            astgen.appendBodyWithFixupsExtraRefsArrayList(&astgen.extra, body, args.param_insts);
            astgen.extra.appendSliceAssumeCapacity(src_locs_and_hash);

            break :inst_info .{ .func_fancy, payload_index };
        } else inst_info: {
            try astgen.extra.ensureUnusedCapacity(
                gpa,
                @typeInfo(Zir.Inst.Func).@"struct".fields.len + 1 +
                    fancyFnExprExtraLen(astgen, args.ret_param_refs, ret_body, ret_ref) +
                    body_len + src_locs_and_hash.len,
            );

            const ret_body_len = if (ret_body.len != 0)
                countBodyLenAfterFixups(astgen, args.ret_param_refs) + countBodyLenAfterFixups(astgen, ret_body)
            else
                @intFromBool(ret_ref != .none);

            const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.Func{
                .param_block = args.param_block,
                .ret_ty = .{
                    .body_len = @intCast(ret_body_len),
                    .is_generic = args.ret_ty_is_generic,
                },
                .body_len = body_len,
            });
            const zir_datas = astgen.instructions.items(.data);
            if (ret_body.len != 0) {
                astgen.appendBodyWithFixups(args.ret_param_refs);
                astgen.appendBodyWithFixups(ret_body);

                const break_extra = zir_datas[@intFromEnum(ret_body[ret_body.len - 1])].@"break".payload_index;
                astgen.extra.items[break_extra + std.meta.fieldIndex(Zir.Inst.Break, "block_inst").?] =
                    @intFromEnum(new_index);
            } else if (ret_ref != .none) {
                astgen.extra.appendAssumeCapacity(@intFromEnum(ret_ref));
            }
            astgen.appendBodyWithFixupsExtraRefsArrayList(&astgen.extra, body, args.param_insts);
            astgen.extra.appendSliceAssumeCapacity(src_locs_and_hash);

            break :inst_info .{
                if (args.is_inferred_error) .func_inferred else .func,
                payload_index,
            };
        };

        // Order is important when unstacking.
        if (args.body_gz) |body_gz| body_gz.unstack();
        if (args.cc_gz) |cc_gz| cc_gz.unstack();
        if (args.ret_gz) |ret_gz| ret_gz.unstack();

        astgen.instructions.appendAssumeCapacity(.{
            .tag = tag,
            .data = .{ .pl_node = .{
                .src_node = gz.nodeIndexToRelative(args.src_node),
                .payload_index = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    fn fancyFnExprExtraLen(astgen: *AstGen, param_refs_body: []const Zir.Inst.Index, main_body: []const Zir.Inst.Index, ref: Zir.Inst.Ref) u32 {
        return countBodyLenAfterFixups(astgen, param_refs_body) +
            countBodyLenAfterFixups(astgen, main_body) +
            // If there is a body, we need an element for its length; otherwise, if there is a ref, we need to include that.
            @intFromBool(main_body.len > 0 or ref != .none);
    }

    fn addInt(gz: *GenZir, integer: u64) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = .int,
            .data = .{ .int = integer },
        });
    }

    fn addIntBig(gz: *GenZir, limbs: []const std.math.big.Limb) !Zir.Inst.Ref {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.string_bytes.ensureUnusedCapacity(gpa, @sizeOf(std.math.big.Limb) * limbs.len);

        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        astgen.instructions.appendAssumeCapacity(.{
            .tag = .int_big,
            .data = .{ .str = .{
                .start = @enumFromInt(astgen.string_bytes.items.len),
                .len = @intCast(limbs.len),
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        astgen.string_bytes.appendSliceAssumeCapacity(mem.sliceAsBytes(limbs));
        return new_index.toRef();
    }

    fn addFloat(gz: *GenZir, number: f64) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = .float,
            .data = .{ .float = number },
        });
    }

    fn addUnNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        operand: Zir.Inst.Ref,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Ref {
        assert(operand != .none);
        return gz.add(.{
            .tag = tag,
            .data = .{ .un_node = .{
                .operand = operand,
                .src_node = gz.nodeIndexToRelative(src_node),
            } },
        });
    }

    fn makeUnNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        operand: Zir.Inst.Ref,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Index {
        assert(operand != .none);
        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        try gz.astgen.instructions.append(gz.astgen.gpa, .{
            .tag = tag,
            .data = .{ .un_node = .{
                .operand = operand,
                .src_node = gz.nodeIndexToRelative(src_node),
            } },
        });
        return new_index;
    }

    fn addPlNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
        extra: anytype,
    ) !Zir.Inst.Ref {
        const gpa = gz.astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);

        const payload_index = try gz.astgen.addExtra(extra);
        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        gz.astgen.instructions.appendAssumeCapacity(.{
            .tag = tag,
            .data = .{ .pl_node = .{
                .src_node = gz.nodeIndexToRelative(src_node),
                .payload_index = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    fn addPlNodePayloadIndex(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
        payload_index: u32,
    ) !Zir.Inst.Ref {
        return try gz.add(.{
            .tag = tag,
            .data = .{ .pl_node = .{
                .src_node = gz.nodeIndexToRelative(src_node),
                .payload_index = payload_index,
            } },
        });
    }

    /// Supports `param_gz` stacked on `gz`. Assumes nothing stacked on `param_gz`. Unstacks `param_gz`.
    fn addParam(
        gz: *GenZir,
        param_gz: *GenZir,
        /// Previous parameters, which might be referenced in `param_gz` (the new parameter type).
        /// `ref`s of these instructions will be put into this param's type body, and removed from `AstGen.ref_table`.
        prev_param_insts: []const Zir.Inst.Index,
        ty_is_generic: bool,
        tag: Zir.Inst.Tag,
        /// Absolute token index. This function does the conversion to Decl offset.
        abs_tok_index: Ast.TokenIndex,
        name: Zir.NullTerminatedString,
    ) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        const param_body = param_gz.instructionsSlice();
        const body_len = gz.astgen.countBodyLenAfterFixupsExtraRefs(param_body, prev_param_insts);
        try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);
        try gz.astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.Param).@"struct".fields.len + body_len);

        const payload_index = gz.astgen.addExtraAssumeCapacity(Zir.Inst.Param{
            .name = name,
            .type = .{
                .body_len = @intCast(body_len),
                .is_generic = ty_is_generic,
            },
        });
        gz.astgen.appendBodyWithFixupsExtraRefsArrayList(&gz.astgen.extra, param_body, prev_param_insts);
        param_gz.unstack();

        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        gz.astgen.instructions.appendAssumeCapacity(.{
            .tag = tag,
            .data = .{ .pl_tok = .{
                .src_tok = gz.tokenIndexToRelative(abs_tok_index),
                .payload_index = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index;
    }

    fn addBuiltinValue(gz: *GenZir, src_node: Ast.Node.Index, val: Zir.Inst.BuiltinValue) !Zir.Inst.Ref {
        return addExtendedNodeSmall(gz, .builtin_value, src_node, @intFromEnum(val));
    }

    fn addExtendedPayload(gz: *GenZir, opcode: Zir.Inst.Extended, extra: anytype) !Zir.Inst.Ref {
        return addExtendedPayloadSmall(gz, opcode, undefined, extra);
    }

    fn addExtendedPayloadSmall(
        gz: *GenZir,
        opcode: Zir.Inst.Extended,
        small: u16,
        extra: anytype,
    ) !Zir.Inst.Ref {
        const gpa = gz.astgen.gpa;

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);

        const payload_index = try gz.astgen.addExtra(extra);
        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        gz.astgen.instructions.appendAssumeCapacity(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = opcode,
                .small = small,
                .operand = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    fn addExtendedMultiOp(
        gz: *GenZir,
        opcode: Zir.Inst.Extended,
        node: Ast.Node.Index,
        operands: []const Zir.Inst.Ref,
    ) !Zir.Inst.Ref {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.extra.ensureUnusedCapacity(
            gpa,
            @typeInfo(Zir.Inst.NodeMultiOp).@"struct".fields.len + operands.len,
        );

        const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.NodeMultiOp{
            .src_node = gz.nodeIndexToRelative(node),
        });
        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        astgen.instructions.appendAssumeCapacity(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = opcode,
                .small = @intCast(operands.len),
                .operand = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        astgen.appendRefsAssumeCapacity(operands);
        return new_index.toRef();
    }

    fn addExtendedMultiOpPayloadIndex(
        gz: *GenZir,
        opcode: Zir.Inst.Extended,
        payload_index: u32,
        trailing_len: usize,
    ) !Zir.Inst.Ref {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);
        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        astgen.instructions.appendAssumeCapacity(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = opcode,
                .small = @intCast(trailing_len),
                .operand = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    fn addExtendedNodeSmall(
        gz: *GenZir,
        opcode: Zir.Inst.Extended,
        src_node: Ast.Node.Index,
        small: u16,
    ) !Zir.Inst.Ref {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);
        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        astgen.instructions.appendAssumeCapacity(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = opcode,
                .small = small,
                .operand = @bitCast(@intFromEnum(gz.nodeIndexToRelative(src_node))),
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    fn addUnTok(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        operand: Zir.Inst.Ref,
        /// Absolute token index. This function does the conversion to Decl offset.
        abs_tok_index: Ast.TokenIndex,
    ) !Zir.Inst.Ref {
        assert(operand != .none);
        return gz.add(.{
            .tag = tag,
            .data = .{ .un_tok = .{
                .operand = operand,
                .src_tok = gz.tokenIndexToRelative(abs_tok_index),
            } },
        });
    }

    fn makeUnTok(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        operand: Zir.Inst.Ref,
        /// Absolute token index. This function does the conversion to Decl offset.
        abs_tok_index: Ast.TokenIndex,
    ) !Zir.Inst.Index {
        const astgen = gz.astgen;
        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        assert(operand != .none);
        try astgen.instructions.append(astgen.gpa, .{
            .tag = tag,
            .data = .{ .un_tok = .{
                .operand = operand,
                .src_tok = gz.tokenIndexToRelative(abs_tok_index),
            } },
        });
        return new_index;
    }

    fn addStrTok(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        str_index: Zir.NullTerminatedString,
        /// Absolute token index. This function does the conversion to Decl offset.
        abs_tok_index: Ast.TokenIndex,
    ) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = tag,
            .data = .{ .str_tok = .{
                .start = str_index,
                .src_tok = gz.tokenIndexToRelative(abs_tok_index),
            } },
        });
    }

    fn addSaveErrRetIndex(
        gz: *GenZir,
        cond: union(enum) {
            always: void,
            if_of_error_type: Zir.Inst.Ref,
        },
    ) !Zir.Inst.Index {
        return gz.addAsIndex(.{
            .tag = .save_err_ret_index,
            .data = .{ .save_err_ret_index = .{
                .operand = switch (cond) {
                    .if_of_error_type => |x| x,
                    else => .none,
                },
            } },
        });
    }

    const BranchTarget = union(enum) {
        ret,
        block: Zir.Inst.Index,
    };

    fn addRestoreErrRetIndex(
        gz: *GenZir,
        bt: BranchTarget,
        cond: union(enum) {
            always: void,
            if_non_error: Zir.Inst.Ref,
        },
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Index {
        switch (cond) {
            .always => return gz.addAsIndex(.{
                .tag = .restore_err_ret_index_unconditional,
                .data = .{ .un_node = .{
                    .operand = switch (bt) {
                        .ret => .none,
                        .block => |b| b.toRef(),
                    },
                    .src_node = gz.nodeIndexToRelative(src_node),
                } },
            }),
            .if_non_error => |operand| switch (bt) {
                .ret => return gz.addAsIndex(.{
                    .tag = .restore_err_ret_index_fn_entry,
                    .data = .{ .un_node = .{
                        .operand = operand,
                        .src_node = gz.nodeIndexToRelative(src_node),
                    } },
                }),
                .block => |block| return (try gz.addExtendedPayload(
                    .restore_err_ret_index,
                    Zir.Inst.RestoreErrRetIndex{
                        .src_node = gz.nodeIndexToRelative(src_node),
                        .block = block.toRef(),
                        .operand = operand,
                    },
                )).toIndex().?,
            },
        }
    }

    fn addBreak(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        block_inst: Zir.Inst.Index,
        operand: Zir.Inst.Ref,
    ) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);

        const new_index = try gz.makeBreak(tag, block_inst, operand);
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index;
    }

    fn makeBreak(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        block_inst: Zir.Inst.Index,
        operand: Zir.Inst.Ref,
    ) !Zir.Inst.Index {
        return gz.makeBreakCommon(tag, block_inst, operand, null);
    }

    fn addBreakWithSrcNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        block_inst: Zir.Inst.Index,
        operand: Zir.Inst.Ref,
        operand_src_node: Ast.Node.Index,
    ) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);

        const new_index = try gz.makeBreakWithSrcNode(tag, block_inst, operand, operand_src_node);
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index;
    }

    fn makeBreakWithSrcNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        block_inst: Zir.Inst.Index,
        operand: Zir.Inst.Ref,
        operand_src_node: Ast.Node.Index,
    ) !Zir.Inst.Index {
        return gz.makeBreakCommon(tag, block_inst, operand, operand_src_node);
    }

    fn makeBreakCommon(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        block_inst: Zir.Inst.Index,
        operand: Zir.Inst.Ref,
        operand_src_node: ?Ast.Node.Index,
    ) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);
        try gz.astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.Break).@"struct".fields.len);

        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        gz.astgen.instructions.appendAssumeCapacity(.{
            .tag = tag,
            .data = .{ .@"break" = .{
                .operand = operand,
                .payload_index = gz.astgen.addExtraAssumeCapacity(Zir.Inst.Break{
                    .operand_src_node = if (operand_src_node) |src_node|
                        gz.nodeIndexToRelative(src_node).toOptional()
                    else
                        .none,
                    .block_inst = block_inst,
                }),
            } },
        });
        return new_index;
    }

    fn addBin(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        lhs: Zir.Inst.Ref,
        rhs: Zir.Inst.Ref,
    ) !Zir.Inst.Ref {
        assert(lhs != .none);
        assert(rhs != .none);
        return gz.add(.{
            .tag = tag,
            .data = .{ .bin = .{
                .lhs = lhs,
                .rhs = rhs,
            } },
        });
    }

    fn addDefer(gz: *GenZir, index: u32, len: u32) !void {
        _ = try gz.add(.{
            .tag = .@"defer",
            .data = .{ .@"defer" = .{
                .index = index,
                .len = len,
            } },
        });
    }

    fn addDecl(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        decl_index: u32,
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = tag,
            .data = .{ .pl_node = .{
                .src_node = gz.nodeIndexToRelative(src_node),
                .payload_index = decl_index,
            } },
        });
    }

    fn addNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = tag,
            .data = .{ .node = gz.nodeIndexToRelative(src_node) },
        });
    }

    fn addInstNode(
        gz: *GenZir,
        tag: Zir.Inst.Tag,
        inst: Zir.Inst.Index,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = tag,
            .data = .{ .inst_node = .{
                .inst = inst,
                .src_node = gz.nodeIndexToRelative(src_node),
            } },
        });
    }

    fn addNodeExtended(
        gz: *GenZir,
        opcode: Zir.Inst.Extended,
        /// Absolute node index. This function does the conversion to offset from Decl.
        src_node: Ast.Node.Index,
    ) !Zir.Inst.Ref {
        return gz.add(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = opcode,
                .small = undefined,
                .operand = @bitCast(@intFromEnum(gz.nodeIndexToRelative(src_node))),
            } },
        });
    }

    fn addAllocExtended(
        gz: *GenZir,
        args: struct {
            /// Absolute node index. This function does the conversion to offset from Decl.
            node: Ast.Node.Index,
            type_inst: Zir.Inst.Ref,
            align_inst: Zir.Inst.Ref,
            is_const: bool,
            is_comptime: bool,
        },
    ) !Zir.Inst.Ref {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.extra.ensureUnusedCapacity(
            gpa,
            @typeInfo(Zir.Inst.AllocExtended).@"struct".fields.len +
                @intFromBool(args.type_inst != .none) +
                @intFromBool(args.align_inst != .none),
        );
        const payload_index = gz.astgen.addExtraAssumeCapacity(Zir.Inst.AllocExtended{
            .src_node = gz.nodeIndexToRelative(args.node),
        });
        if (args.type_inst != .none) {
            astgen.extra.appendAssumeCapacity(@intFromEnum(args.type_inst));
        }
        if (args.align_inst != .none) {
            astgen.extra.appendAssumeCapacity(@intFromEnum(args.align_inst));
        }

        const has_type: u4 = @intFromBool(args.type_inst != .none);
        const has_align: u4 = @intFromBool(args.align_inst != .none);
        const is_const: u4 = @intFromBool(args.is_const);
        const is_comptime: u4 = @intFromBool(args.is_comptime);
        const small: u16 = has_type | (has_align << 1) | (is_const << 2) | (is_comptime << 3);

        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        astgen.instructions.appendAssumeCapacity(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .alloc,
                .small = small,
                .operand = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    fn addAsm(
        gz: *GenZir,
        args: struct {
            tag: Zir.Inst.Extended,
            /// Absolute node index. This function does the conversion to offset from Decl.
            node: Ast.Node.Index,
            asm_source: Zir.NullTerminatedString,
            output_type_bits: u32,
            is_volatile: bool,
            outputs: []const Zir.Inst.Asm.Output,
            inputs: []const Zir.Inst.Asm.Input,
            clobbers: Zir.Inst.Ref,
        },
    ) !Zir.Inst.Ref {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.instructions.ensureUnusedCapacity(gpa, 1);
        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.Asm).@"struct".fields.len +
            args.outputs.len * @typeInfo(Zir.Inst.Asm.Output).@"struct".fields.len +
            args.inputs.len * @typeInfo(Zir.Inst.Asm.Input).@"struct".fields.len);

        const payload_index = gz.astgen.addExtraAssumeCapacity(Zir.Inst.Asm{
            .src_node = gz.nodeIndexToRelative(args.node),
            .asm_source = args.asm_source,
            .output_type_bits = args.output_type_bits,
            .clobbers = args.clobbers,
        });
        for (args.outputs) |output| {
            _ = gz.astgen.addExtraAssumeCapacity(output);
        }
        for (args.inputs) |input| {
            _ = gz.astgen.addExtraAssumeCapacity(input);
        }

        const small: Zir.Inst.Asm.Small = .{
            .is_volatile = args.is_volatile,
            .outputs_len = @intCast(args.outputs.len),
            .inputs_len = @intCast(args.inputs.len),
        };

        const new_index: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
        astgen.instructions.appendAssumeCapacity(.{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = args.tag,
                .small = @bitCast(small),
                .operand = payload_index,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index.toRef();
    }

    /// Note that this returns a `Zir.Inst.Index` not a ref.
    /// Does *not* append the block instruction to the scope.
    /// Leaves the `payload_index` field undefined.
    fn makeBlockInst(gz: *GenZir, tag: Zir.Inst.Tag, node: Ast.Node.Index) !Zir.Inst.Index {
        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        const gpa = gz.astgen.gpa;
        try gz.astgen.instructions.append(gpa, .{
            .tag = tag,
            .data = .{ .pl_node = .{
                .src_node = gz.nodeIndexToRelative(node),
                .payload_index = undefined,
            } },
        });
        return new_index;
    }

    /// Note that this returns a `Zir.Inst.Index` not a ref.
    /// Does *not* append the block instruction to the scope.
    /// Leaves the `payload_index` field undefined. Use `setDeclaration` to finalize.
    fn makeDeclaration(gz: *GenZir, node: Ast.Node.Index) !Zir.Inst.Index {
        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        try gz.astgen.instructions.append(gz.astgen.gpa, .{
            .tag = .declaration,
            .data = .{ .declaration = .{
                .src_node = node,
                .payload_index = undefined,
            } },
        });
        return new_index;
    }

    /// Note that this returns a `Zir.Inst.Index` not a ref.
    /// Leaves the `payload_index` field undefined.
    fn addCondBr(gz: *GenZir, tag: Zir.Inst.Tag, node: Ast.Node.Index) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        try gz.astgen.instructions.append(gpa, .{
            .tag = tag,
            .data = .{ .pl_node = .{
                .src_node = gz.nodeIndexToRelative(node),
                .payload_index = undefined,
            } },
        });
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index;
    }

    fn setStruct(gz: *GenZir, inst: Zir.Inst.Index, args: struct {
        src_node: Ast.Node.Index,
        name_strat: Zir.Inst.NameStrategy,
        layout: std.builtin.Type.ContainerLayout,
        backing_int_type_body_len: ?u32,
        decls_len: u32,
        fields_len: u32,
        any_field_aligns: bool,
        any_field_defaults: bool,
        any_comptime_fields: bool,
        fields_hash: std.zig.SrcHash,
        captures: []const Zir.Inst.Capture,
        capture_names: []const Zir.NullTerminatedString,
        /// The trailing declaration list, field information, and body instructions.
        remaining: []const u32,
    }) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        // Node .root is valid for the root `struct_decl` of a file!
        assert(args.src_node != .root or gz.parent.tag == .top);

        const captures_len: u32 = @intCast(args.captures.len);
        assert(args.capture_names.len == captures_len);

        const fields_hash_arr: [4]u32 = @bitCast(args.fields_hash);

        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.StructDecl).@"struct".fields.len +
            4 + // `captures_len`, `decls_len`, `fields_len`, `backing_int_type_body_len`
            captures_len * 2 + // `capture`, `capture_name`
            args.remaining.len);

        const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.StructDecl{
            .fields_hash_0 = fields_hash_arr[0],
            .fields_hash_1 = fields_hash_arr[1],
            .fields_hash_2 = fields_hash_arr[2],
            .fields_hash_3 = fields_hash_arr[3],
            .src_line = astgen.source_line,
            .src_node = args.src_node,
        });

        if (captures_len != 0) astgen.extra.appendAssumeCapacity(captures_len);
        if (args.decls_len != 0) astgen.extra.appendAssumeCapacity(args.decls_len);
        if (args.fields_len != 0) astgen.extra.appendAssumeCapacity(args.fields_len);
        if (args.backing_int_type_body_len) |n| astgen.extra.appendAssumeCapacity(n);
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.captures));
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.capture_names));
        astgen.extra.appendSliceAssumeCapacity(args.remaining);

        astgen.instructions.set(@intFromEnum(inst), .{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .struct_decl,
                .small = @bitCast(Zir.Inst.StructDecl.Small{
                    .has_captures_len = captures_len != 0,
                    .has_decls_len = args.decls_len != 0,
                    .has_fields_len = args.fields_len != 0,
                    .name_strategy = args.name_strat,
                    .layout = args.layout,
                    .has_backing_int_type = args.backing_int_type_body_len != null,
                    .any_field_aligns = args.any_field_aligns,
                    .any_field_defaults = args.any_field_defaults,
                    .any_comptime_fields = args.any_comptime_fields,
                }),
                .operand = payload_index,
            } },
        });
    }

    fn setUnion(gz: *GenZir, inst: Zir.Inst.Index, args: struct {
        src_node: Ast.Node.Index,
        name_strat: Zir.Inst.NameStrategy,
        kind: Zir.Inst.UnionDecl.Kind,
        arg_type_body_len: ?u32,
        decls_len: u32,
        fields_len: u32,
        any_field_aligns: bool,
        any_field_values: bool,
        fields_hash: std.zig.SrcHash,
        captures: []const Zir.Inst.Capture,
        capture_names: []const Zir.NullTerminatedString,
        /// The trailing declaration list, field information, and body instructions.
        remaining: []const u32,
    }) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        assert(args.src_node != .root);

        const captures_len: u32 = @intCast(args.captures.len);
        assert(args.capture_names.len == captures_len);

        const fields_hash_arr: [4]u32 = @bitCast(args.fields_hash);

        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.UnionDecl).@"struct".fields.len +
            4 + // `captures_len`, `decls_len`, `fields_len`, `arg_type_body_len`
            captures_len * 2 + // `capture`, `capture_name`
            args.remaining.len);

        const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.UnionDecl{
            .fields_hash_0 = fields_hash_arr[0],
            .fields_hash_1 = fields_hash_arr[1],
            .fields_hash_2 = fields_hash_arr[2],
            .fields_hash_3 = fields_hash_arr[3],
            .src_line = astgen.source_line,
            .src_node = args.src_node,
        });

        if (captures_len != 0) astgen.extra.appendAssumeCapacity(captures_len);
        if (args.decls_len != 0) astgen.extra.appendAssumeCapacity(args.decls_len);
        if (args.fields_len != 0) astgen.extra.appendAssumeCapacity(args.fields_len);
        if (args.kind.hasArgType()) {
            astgen.extra.appendAssumeCapacity(args.arg_type_body_len.?);
        } else {
            assert(args.arg_type_body_len == null);
        }
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.captures));
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.capture_names));
        astgen.extra.appendSliceAssumeCapacity(args.remaining);

        astgen.instructions.set(@intFromEnum(inst), .{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .union_decl,
                .small = @bitCast(Zir.Inst.UnionDecl.Small{
                    .has_captures_len = captures_len != 0,
                    .has_decls_len = args.decls_len != 0,
                    .has_fields_len = args.fields_len != 0,
                    .name_strategy = args.name_strat,
                    .kind = args.kind,
                    .any_field_aligns = args.any_field_aligns,
                    .any_field_values = args.any_field_values,
                }),
                .operand = payload_index,
            } },
        });
    }

    fn setEnum(gz: *GenZir, inst: Zir.Inst.Index, args: struct {
        src_node: Ast.Node.Index,
        name_strat: Zir.Inst.NameStrategy,
        tag_type_body_len: ?u32,
        nonexhaustive: bool,
        decls_len: u32,
        fields_len: u32,
        any_field_values: bool,
        fields_hash: std.zig.SrcHash,
        captures: []const Zir.Inst.Capture,
        capture_names: []const Zir.NullTerminatedString,
        /// The trailing declaration list, field information, and body instructions.
        remaining: []const u32,
    }) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        assert(args.src_node != .root);

        const captures_len: u32 = @intCast(args.captures.len);
        assert(args.capture_names.len == captures_len);

        const fields_hash_arr: [4]u32 = @bitCast(args.fields_hash);

        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.EnumDecl).@"struct".fields.len +
            4 + // `captures_len`, `decls_len`, `fields_len`, `tag_type_body_len`
            captures_len * 2 + // `capture`, `capture_name`
            args.remaining.len);

        const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.EnumDecl{
            .fields_hash_0 = fields_hash_arr[0],
            .fields_hash_1 = fields_hash_arr[1],
            .fields_hash_2 = fields_hash_arr[2],
            .fields_hash_3 = fields_hash_arr[3],
            .src_line = astgen.source_line,
            .src_node = args.src_node,
        });

        if (captures_len != 0) astgen.extra.appendAssumeCapacity(captures_len);
        if (args.decls_len != 0) astgen.extra.appendAssumeCapacity(args.decls_len);
        if (args.fields_len != 0) astgen.extra.appendAssumeCapacity(args.fields_len);
        if (args.tag_type_body_len) |n| astgen.extra.appendAssumeCapacity(n);
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.captures));
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.capture_names));
        astgen.extra.appendSliceAssumeCapacity(args.remaining);

        astgen.instructions.set(@intFromEnum(inst), .{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .enum_decl,
                .small = @bitCast(Zir.Inst.EnumDecl.Small{
                    .has_captures_len = captures_len != 0,
                    .has_decls_len = args.decls_len != 0,
                    .has_fields_len = args.fields_len != 0,
                    .name_strategy = args.name_strat,
                    .has_tag_type = args.tag_type_body_len != null,
                    .nonexhaustive = args.nonexhaustive,
                    .any_field_values = args.any_field_values,
                }),
                .operand = payload_index,
            } },
        });
    }

    fn setOpaque(gz: *GenZir, inst: Zir.Inst.Index, args: struct {
        src_node: Ast.Node.Index,
        name_strat: Zir.Inst.NameStrategy,
        decls_len: u32,
        captures: []const Zir.Inst.Capture,
        capture_names: []const Zir.NullTerminatedString,
        decls: []const Zir.Inst.Index,
    }) !void {
        const astgen = gz.astgen;
        const gpa = astgen.gpa;

        assert(args.src_node != .root);

        const captures_len: u32 = @intCast(args.captures.len);
        assert(args.capture_names.len == captures_len);

        try astgen.extra.ensureUnusedCapacity(gpa, @typeInfo(Zir.Inst.OpaqueDecl).@"struct".fields.len +
            2 + // `captures_len`, `decls_len`
            captures_len * 2 + // `capture`, `capture_name`
            args.decls.len);

        const payload_index = astgen.addExtraAssumeCapacity(Zir.Inst.OpaqueDecl{
            .src_line = astgen.source_line,
            .src_node = args.src_node,
        });
        if (captures_len != 0) astgen.extra.appendAssumeCapacity(captures_len);
        if (args.decls_len != 0) astgen.extra.appendAssumeCapacity(args.decls_len);
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.captures));
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.capture_names));
        astgen.extra.appendSliceAssumeCapacity(@ptrCast(args.decls));

        astgen.instructions.set(@intFromEnum(inst), .{
            .tag = .extended,
            .data = .{ .extended = .{
                .opcode = .opaque_decl,
                .small = @bitCast(Zir.Inst.OpaqueDecl.Small{
                    .has_captures_len = captures_len != 0,
                    .has_decls_len = args.decls_len != 0,
                    .name_strategy = args.name_strat,
                }),
                .operand = payload_index,
            } },
        });
    }

    fn add(gz: *GenZir, inst: Zir.Inst) !Zir.Inst.Ref {
        return (try gz.addAsIndex(inst)).toRef();
    }

    fn addAsIndex(gz: *GenZir, inst: Zir.Inst) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);

        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        gz.astgen.instructions.appendAssumeCapacity(inst);
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index;
    }

    fn reserveInstructionIndex(gz: *GenZir) !Zir.Inst.Index {
        const gpa = gz.astgen.gpa;
        try gz.instructions.ensureUnusedCapacity(gpa, 1);
        try gz.astgen.instructions.ensureUnusedCapacity(gpa, 1);

        const new_index: Zir.Inst.Index = @enumFromInt(gz.astgen.instructions.len);
        gz.astgen.instructions.len += 1;
        gz.instructions.appendAssumeCapacity(new_index);
        return new_index;
    }

    fn addRet(gz: *GenZir, ri: ResultInfo, operand: Zir.Inst.Ref, node: Ast.Node.Index) !void {
        switch (ri.rl) {
            .ptr => |ptr_res| _ = try gz.addUnNode(.ret_load, ptr_res.inst, node),
            .coerced_ty => _ = try gz.addUnNode(.ret_node, operand, node),
            else => unreachable,
        }
    }

    fn addDbgVar(gz: *GenZir, tag: Zir.Inst.Tag, name: Zir.NullTerminatedString, inst: Zir.Inst.Ref) !void {
        if (gz.is_comptime) return;

        _ = try gz.add(.{ .tag = tag, .data = .{
            .str_op = .{
                .str = name,
                .operand = inst,
            },
        } });
    }
};

/// This can only be for short-lived references; the memory becomes invalidated
/// when another string is added.
fn nullTerminatedString(astgen: AstGen, index: Zir.NullTerminatedString) [*:0]const u8 {
    return @ptrCast(astgen.string_bytes.items[@intFromEnum(index)..]);
}

/// Local variables shadowing detection, including function parameters.
fn detectLocalShadowing(
    astgen: *AstGen,
    scope: *Scope,
    ident_name: Zir.NullTerminatedString,
    name_token: Ast.TokenIndex,
    ident_name_raw: []const u8,
    id_cat: Scope.IdCat,
) !void {
    const gpa = astgen.gpa;
    if (ident_name_raw[0] != '@' and isPrimitive(ident_name_raw)) {
        return astgen.failTokNotes(name_token, "name shadows primitive '{s}'", .{
            ident_name_raw,
        }, &[_]u32{
            try astgen.errNoteTok(name_token, "consider using @\"{s}\" to disambiguate", .{
                ident_name_raw,
            }),
        });
    }

    var outer_scope = false;
    find_scope: switch (scope.unwrap()) {
        .local_val => |local_val| {
            if (local_val.name == ident_name) {
                const name_slice = mem.span(astgen.nullTerminatedString(ident_name));
                const name = try gpa.dupe(u8, name_slice);
                defer gpa.free(name);
                if (outer_scope) {
                    return astgen.failTokNotes(name_token, "{s} '{s}' shadows {s} from outer scope", .{
                        @tagName(id_cat), name, @tagName(local_val.id_cat),
                    }, &[_]u32{
                        try astgen.errNoteTok(
                            local_val.token_src,
                            "previous declaration here",
                            .{},
                        ),
                    });
                }
                return astgen.failTokNotes(name_token, "redeclaration of {s} '{s}'", .{
                    @tagName(local_val.id_cat), name,
                }, &[_]u32{
                    try astgen.errNoteTok(
                        local_val.token_src,
                        "previous declaration here",
                        .{},
                    ),
                });
            }
            continue :find_scope local_val.parent.unwrap();
        },
        .local_ptr => |local_ptr| {
            if (local_ptr.name == ident_name) {
                const name_slice = mem.span(astgen.nullTerminatedString(ident_name));
                const name = try gpa.dupe(u8, name_slice);
                defer gpa.free(name);
                if (outer_scope) {
                    return astgen.failTokNotes(name_token, "{s} '{s}' shadows {s} from outer scope", .{
                        @tagName(id_cat), name, @tagName(local_ptr.id_cat),
                    }, &[_]u32{
                        try astgen.errNoteTok(
                            local_ptr.token_src,
                            "previous declaration here",
                            .{},
                        ),
                    });
                }
                return astgen.failTokNotes(name_token, "redeclaration of {s} '{s}'", .{
                    @tagName(local_ptr.id_cat), name,
                }, &[_]u32{
                    try astgen.errNoteTok(
                        local_ptr.token_src,
                        "previous declaration here",
                        .{},
                    ),
                });
            }
            continue :find_scope local_ptr.parent.unwrap();
        },
        .namespace => |ns| {
            outer_scope = true;
            const decl_node = ns.decls.get(ident_name) orelse {
                continue :find_scope ns.parent.unwrap();
            };
            const name_slice = mem.span(astgen.nullTerminatedString(ident_name));
            const name = try gpa.dupe(u8, name_slice);
            defer gpa.free(name);
            return astgen.failTokNotes(name_token, "{s} shadows declaration of '{s}'", .{
                @tagName(id_cat), name,
            }, &[_]u32{
                try astgen.errNoteNode(decl_node, "declared here", .{}),
            });
        },
        .gen_zir => |gen_zir| {
            outer_scope = true;
            continue :find_scope gen_zir.parent.unwrap();
        },
        .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
        .top => break :find_scope,
    }
}

const LineColumn = struct { u32, u32 };

/// Advances the source cursor to the main token of `node` if not in comptime scope.
/// Usually paired with `emitDbgStmt`.
fn maybeAdvanceSourceCursorToMainToken(gz: *GenZir, node: Ast.Node.Index) LineColumn {
    if (gz.is_comptime) return .{ gz.astgen.source_line - gz.decl_line, gz.astgen.source_column };

    const tree = gz.astgen.tree;
    const node_start = tree.tokenStart(tree.nodeMainToken(node));
    gz.astgen.advanceSourceCursor(node_start);

    return .{ gz.astgen.source_line - gz.decl_line, gz.astgen.source_column };
}

/// Advances the source cursor to the beginning of `node`.
fn advanceSourceCursorToNode(astgen: *AstGen, node: Ast.Node.Index) void {
    const tree = astgen.tree;
    const node_start = tree.tokenStart(tree.firstToken(node));
    astgen.advanceSourceCursor(node_start);
}

/// Advances the source cursor to an absolute byte offset `end` in the file.
fn advanceSourceCursor(astgen: *AstGen, end: usize) void {
    const source = astgen.tree.source;
    var i = astgen.source_offset;
    var line = astgen.source_line;
    var column = astgen.source_column;
    assert(i <= end);
    while (i < end) : (i += 1) {
        if (source[i] == '\n') {
            line += 1;
            column = 0;
        } else {
            column += 1;
        }
    }
    astgen.source_offset = i;
    astgen.source_line = line;
    astgen.source_column = column;
}

const SourceCursor = struct {
    offset: u32,
    line: u32,
    column: u32,
};

/// Get the current source cursor, to be restored later with `restoreSourceCursor`.
/// This is useful when analyzing source code out-of-order.
fn saveSourceCursor(astgen: *const AstGen) SourceCursor {
    return .{
        .offset = astgen.source_offset,
        .line = astgen.source_line,
        .column = astgen.source_column,
    };
}
fn restoreSourceCursor(astgen: *AstGen, cursor: SourceCursor) void {
    astgen.source_offset = cursor.offset;
    astgen.source_line = cursor.line;
    astgen.source_column = cursor.column;
}

const ScanContainerResult = struct {
    /// Includes unnamed declarations (e.g. `comptime` decls)
    decls_len: u32,
    fields_len: u32,
    any_field_aligns: bool,
    any_field_values: bool,
    any_comptime_fields: bool,
    /// Whether there is a field named `_` (indicating a non-exhaustive enum)
    has_underscore_field: bool,
};

/// Detects name conflicts for decls and fields, and populates `namespace.decls` with all named declarations.
fn scanContainer(
    astgen: *AstGen,
    namespace: *Scope.Namespace,
    members: []const Ast.Node.Index,
    container_kind: enum { @"struct", @"union", @"enum", @"opaque" },
) !ScanContainerResult {
    const gpa = astgen.gpa;
    const tree = astgen.tree;

    var any_invalid_declarations = false;

    // This type forms a linked list of source tokens declaring the same name.
    const NameEntry = struct {
        tok: Ast.TokenIndex,
        /// Using a linked list here simplifies memory management, and is acceptable since
        ///ewntries are only allocated in error situations. The entries are allocated into the
        /// AstGen arena.
        next: ?*@This(),
    };

    // The maps below are allocated into this SFBA to avoid using the GPA for small namespaces.
    var sfba_state = std.heap.stackFallback(512, astgen.gpa);
    const sfba = sfba_state.get();

    var names: std.AutoArrayHashMapUnmanaged(Zir.NullTerminatedString, NameEntry) = .empty;
    var test_names: std.AutoArrayHashMapUnmanaged(Zir.NullTerminatedString, NameEntry) = .empty;
    var decltest_names: std.AutoArrayHashMapUnmanaged(Zir.NullTerminatedString, NameEntry) = .empty;
    defer {
        names.deinit(sfba);
        test_names.deinit(sfba);
        decltest_names.deinit(sfba);
    }

    var any_duplicates = false;
    var decl_count: u32 = 0;
    var any_field_aligns = false;
    var any_field_values = false;
    var any_comptime_fields = false;
    var has_underscore_field = false;
    for (members) |member_node| {
        const Kind = enum { decl, field };
        const kind: Kind, const name_token = switch (tree.nodeTag(member_node)) {
            .container_field_init,
            .container_field_align,
            .container_field,
            => blk: {
                var full = tree.fullContainerField(member_node).?;
                switch (container_kind) {
                    .@"struct", .@"opaque" => {},
                    .@"union", .@"enum" => full.convertToNonTupleLike(astgen.tree),
                }
                if (full.ast.align_expr != .none) any_field_aligns = true;
                if (full.ast.value_expr != .none) any_field_values = true;
                if (full.comptime_token != null) any_comptime_fields = true;
                if (mem.eql(u8, tree.tokenSlice(full.ast.main_token), "_")) has_underscore_field = true;
                if (full.ast.tuple_like) continue;
                break :blk .{ .field, full.ast.main_token };
            },

            .global_var_decl,
            .local_var_decl,
            .simple_var_decl,
            .aligned_var_decl,
            => blk: {
                decl_count += 1;
                break :blk .{ .decl, tree.nodeMainToken(member_node) + 1 };
            },

            .fn_proto_simple,
            .fn_proto_multi,
            .fn_proto_one,
            .fn_proto,
            .fn_decl,
            => blk: {
                decl_count += 1;
                const ident = tree.nodeMainToken(member_node) + 1;
                if (tree.tokenTag(ident) != .identifier) {
                    try astgen.appendErrorNode(member_node, "missing function name", .{});
                    any_invalid_declarations = true;
                    continue;
                }
                break :blk .{ .decl, ident };
            },

            .@"comptime" => {
                decl_count += 1;
                continue;
            },

            .test_decl => {
                decl_count += 1;
                // We don't want shadowing detection here, and test names work a bit differently, so
                // we must do the redeclaration detection ourselves.
                const test_name_token = tree.nodeMainToken(member_node) + 1;
                const new_ent: NameEntry = .{
                    .tok = test_name_token,
                    .next = null,
                };
                switch (tree.tokenTag(test_name_token)) {
                    else => {}, // unnamed test
                    .string_literal => {
                        const name = try astgen.strLitAsString(test_name_token);
                        const gop = try test_names.getOrPut(sfba, name.index);
                        if (gop.found_existing) {
                            var e = gop.value_ptr;
                            while (e.next) |n| e = n;
                            e.next = try astgen.arena.create(NameEntry);
                            e.next.?.* = new_ent;
                            any_duplicates = true;
                        } else {
                            gop.value_ptr.* = new_ent;
                        }
                    },
                    .identifier => {
                        const name = try astgen.identAsString(test_name_token);
                        const gop = try decltest_names.getOrPut(sfba, name);
                        if (gop.found_existing) {
                            var e = gop.value_ptr;
                            while (e.next) |n| e = n;
                            e.next = try astgen.arena.create(NameEntry);
                            e.next.?.* = new_ent;
                            any_duplicates = true;
                        } else {
                            gop.value_ptr.* = new_ent;
                        }
                    },
                }
                continue;
            },

            else => unreachable,
        };

        const name_str_index = try astgen.identAsString(name_token);

        if (kind == .decl) {
            // Put the name straight into `decls`, even if there are compile errors.
            // This avoids incorrect "undeclared identifier" errors later on.
            try namespace.decls.put(gpa, name_str_index, member_node);
        }

        {
            const gop = try names.getOrPut(sfba, name_str_index);
            const new_ent: NameEntry = .{
                .tok = name_token,
                .next = null,
            };
            if (gop.found_existing) {
                var e = gop.value_ptr;
                while (e.next) |n| e = n;
                e.next = try astgen.arena.create(NameEntry);
                e.next.?.* = new_ent;
                any_duplicates = true;
                continue;
            } else {
                gop.value_ptr.* = new_ent;
            }
        }

        // For fields, we only needed the duplicate check! Decls have some more checks to do, though.
        switch (kind) {
            .decl => {},
            .field => continue,
        }

        const token_bytes = astgen.tree.tokenSlice(name_token);
        if (token_bytes[0] != '@' and isPrimitive(token_bytes)) {
            try astgen.appendErrorTokNotes(name_token, "name shadows primitive '{s}'", .{
                token_bytes,
            }, &.{
                try astgen.errNoteTok(name_token, "consider using @\"{s}\" to disambiguate", .{
                    token_bytes,
                }),
            });
            any_invalid_declarations = true;
            continue;
        }

        find_scope: switch (namespace.parent.unwrap()) {
            .local_val => |local_val| {
                if (local_val.name == name_str_index) {
                    try astgen.appendErrorTokNotes(name_token, "declaration '{s}' shadows {s} from outer scope", .{
                        token_bytes, @tagName(local_val.id_cat),
                    }, &.{
                        try astgen.errNoteTok(
                            local_val.token_src,
                            "previous declaration here",
                            .{},
                        ),
                    });
                    any_invalid_declarations = true;
                    break :find_scope;
                }
                continue :find_scope local_val.parent.unwrap();
            },
            .local_ptr => |local_ptr| {
                if (local_ptr.name == name_str_index) {
                    try astgen.appendErrorTokNotes(name_token, "declaration '{s}' shadows {s} from outer scope", .{
                        token_bytes, @tagName(local_ptr.id_cat),
                    }, &.{
                        try astgen.errNoteTok(
                            local_ptr.token_src,
                            "previous declaration here",
                            .{},
                        ),
                    });
                    any_invalid_declarations = true;
                    break :find_scope;
                }
                continue :find_scope local_ptr.parent.unwrap();
            },
            .namespace => |ns| continue :find_scope ns.parent.unwrap(),
            .gen_zir => |gen_zir| continue :find_scope gen_zir.parent.unwrap(),
            .defer_normal, .defer_error => |defer_scope| continue :find_scope defer_scope.parent.unwrap(),
            .top => break :find_scope,
        }
    }

    if (!any_duplicates) {
        if (any_invalid_declarations) return error.AnalysisFail;
        return .{
            .decls_len = decl_count,
            .fields_len = @intCast(members.len - decl_count),
            .any_field_aligns = any_field_aligns,
            .any_field_values = any_field_values,
            .any_comptime_fields = any_comptime_fields,
            .has_underscore_field = has_underscore_field,
        };
    }

    for (names.keys(), names.values()) |name, first| {
        if (first.next == null) continue;
        var notes: std.ArrayList(u32) = .empty;
        var prev: NameEntry = first;
        while (prev.next) |cur| : (prev = cur.*) {
            try notes.append(astgen.arena, try astgen.errNoteTok(cur.tok, "duplicate name here", .{}));
        }
        try notes.append(astgen.arena, try astgen.errNoteNode(namespace.node, "{s} declared here", .{@tagName(container_kind)}));
        const name_duped = try astgen.arena.dupe(u8, mem.span(astgen.nullTerminatedString(name)));
        try astgen.appendErrorTokNotes(first.tok, "duplicate {s} member name '{s}'", .{ @tagName(container_kind), name_duped }, notes.items);
        any_invalid_declarations = true;
    }

    for (test_names.keys(), test_names.values()) |name, first| {
        if (first.next == null) continue;
        var notes: std.ArrayList(u32) = .empty;
        var prev: NameEntry = first;
        while (prev.next) |cur| : (prev = cur.*) {
            try notes.append(astgen.arena, try astgen.errNoteTok(cur.tok, "duplicate test here", .{}));
        }
        try notes.append(astgen.arena, try astgen.errNoteNode(namespace.node, "{s} declared here", .{@tagName(container_kind)}));
        const name_duped = try astgen.arena.dupe(u8, mem.span(astgen.nullTerminatedString(name)));
        try astgen.appendErrorTokNotes(first.tok, "duplicate test name '{s}'", .{name_duped}, notes.items);
        any_invalid_declarations = true;
    }

    for (decltest_names.keys(), decltest_names.values()) |name, first| {
        if (first.next == null) continue;
        var notes: std.ArrayList(u32) = .empty;
        var prev: NameEntry = first;
        while (prev.next) |cur| : (prev = cur.*) {
            try notes.append(astgen.arena, try astgen.errNoteTok(cur.tok, "duplicate decltest here", .{}));
        }
        try notes.append(astgen.arena, try astgen.errNoteNode(namespace.node, "{s} declared here", .{@tagName(container_kind)}));
        const name_duped = try astgen.arena.dupe(u8, mem.span(astgen.nullTerminatedString(name)));
        try astgen.appendErrorTokNotes(first.tok, "duplicate decltest '{s}'", .{name_duped}, notes.items);
        any_invalid_declarations = true;
    }

    assert(any_invalid_declarations);
    return error.AnalysisFail;
}

fn appendPlaceholder(astgen: *AstGen) Allocator.Error!Zir.Inst.Index {
    const inst: Zir.Inst.Index = @enumFromInt(astgen.instructions.len);
    try astgen.instructions.append(astgen.gpa, .{
        .tag = .extended,
        .data = .{ .extended = .{
            .opcode = .value_placeholder,
            .small = undefined,
            .operand = undefined,
        } },
    });
    return inst;
}

/// Assumes capacity for body has already been added. Needed capacity taking into
/// account fixups can be found with `countBodyLenAfterFixups`.
fn appendBodyWithFixups(astgen: *AstGen, body: []const Zir.Inst.Index) void {
    return appendBodyWithFixupsArrayList(astgen, &astgen.extra, body);
}

fn appendBodyWithFixupsArrayList(
    astgen: *AstGen,
    list: *std.ArrayList(u32),
    body: []const Zir.Inst.Index,
) void {
    astgen.appendBodyWithFixupsExtraRefsArrayList(list, body, &.{});
}

fn appendBodyWithFixupsExtraRefsArrayList(
    astgen: *AstGen,
    list: *std.ArrayList(u32),
    body: []const Zir.Inst.Index,
    extra_refs: []const Zir.Inst.Index,
) void {
    for (extra_refs) |extra_inst| {
        if (astgen.ref_table.fetchRemove(extra_inst)) |kv| {
            appendPossiblyRefdBodyInst(astgen, list, kv.value);
        }
    }
    for (body) |body_inst| {
        appendPossiblyRefdBodyInst(astgen, list, body_inst);
    }
}

fn appendPossiblyRefdBodyInst(
    astgen: *AstGen,
    list: *std.ArrayList(u32),
    body_inst: Zir.Inst.Index,
) void {
    list.appendAssumeCapacity(@intFromEnum(body_inst));
    const kv = astgen.ref_table.fetchRemove(body_inst) orelse return;
    const ref_inst = kv.value;
    return appendPossiblyRefdBodyInst(astgen, list, ref_inst);
}

fn countBodyLenAfterFixups(astgen: *AstGen, body: []const Zir.Inst.Index) u32 {
    return astgen.countBodyLenAfterFixupsExtraRefs(body, &.{});
}

/// Return the number of instructions in `body` after prepending the `ref` instructions in `ref_table`.
/// As well as all instructions in `body`, we also prepend `ref`s of any instruction in `extra_refs`.
/// For instance, if an index has been reserved with a special meaning to a child block, it must be
/// passed to `extra_refs` to ensure `ref`s of that index are added correctly.
fn countBodyLenAfterFixupsExtraRefs(astgen: *AstGen, body: []const Zir.Inst.Index, extra_refs: []const Zir.Inst.Index) u32 {
    var count = body.len;
    for (body) |body_inst| {
        var check_inst = body_inst;
        while (astgen.ref_table.get(check_inst)) |ref_inst| {
            count += 1;
            check_inst = ref_inst;
        }
    }
    for (extra_refs) |extra_inst| {
        var check_inst = extra_inst;
        while (astgen.ref_table.get(check_inst)) |ref_inst| {
            count += 1;
            check_inst = ref_inst;
        }
    }
    return @intCast(count);
}

fn emitDbgStmt(gz: *GenZir, lc: LineColumn) !void {
    if (gz.is_comptime) return;
    if (gz.instructions.items.len > gz.instructions_top) {
        const astgen = gz.astgen;
        const last = gz.instructions.items[gz.instructions.items.len - 1];
        if (astgen.instructions.items(.tag)[@intFromEnum(last)] == .dbg_stmt) {
            astgen.instructions.items(.data)[@intFromEnum(last)].dbg_stmt = .{
                .line = lc[0],
                .column = lc[1],
            };
            return;
        }
    }

    _ = try gz.add(.{ .tag = .dbg_stmt, .data = .{
        .dbg_stmt = .{
            .line = lc[0],
            .column = lc[1],
        },
    } });
}

/// In some cases, Sema expects us to generate a `dbg_stmt` at the instruction
/// *index* directly preceding the next instruction (e.g. if a call is %10, it
/// expects a dbg_stmt at %9). TODO: this logic may allow redundant dbg_stmt
/// instructions; fix up Sema so we don't need it!
fn emitDbgStmtForceCurrentIndex(gz: *GenZir, lc: LineColumn) !void {
    const astgen = gz.astgen;
    if (gz.instructions.items.len > gz.instructions_top and
        @intFromEnum(gz.instructions.items[gz.instructions.items.len - 1]) == astgen.instructions.len - 1)
    {
        const last = astgen.instructions.len - 1;
        if (astgen.instructions.items(.tag)[last] == .dbg_stmt) {
            astgen.instructions.items(.data)[last].dbg_stmt = .{
                .line = lc[0],
                .column = lc[1],
            };
            return;
        }
    }

    _ = try gz.add(.{ .tag = .dbg_stmt, .data = .{
        .dbg_stmt = .{
            .line = lc[0],
            .column = lc[1],
        },
    } });
}

fn lowerAstErrors(astgen: *AstGen) error{OutOfMemory}!void {
    const gpa = astgen.gpa;
    const tree = astgen.tree;
    assert(tree.errors.len > 0);

    var msg: std.Io.Writer.Allocating = .init(gpa);
    defer msg.deinit();
    const msg_w = &msg.writer;

    var notes: std.ArrayList(u32) = .empty;
    defer notes.deinit(gpa);

    const token_starts = tree.tokens.items(.start);
    const token_tags = tree.tokens.items(.tag);
    const parse_err = tree.errors[0];
    const tok = parse_err.token + @intFromBool(parse_err.token_is_prev);
    const tok_start = token_starts[tok];
    const start_char = tree.source[tok_start];

    if (token_tags[tok] == .invalid and
        (start_char == '\"' or start_char == '\'' or start_char == '/' or mem.startsWith(u8, tree.source[tok_start..], "\\\\")))
    {
        const tok_len: u32 = @intCast(tree.tokenSlice(tok).len);
        const tok_end = tok_start + tok_len;
        const bad_off = blk: {
            var idx = tok_start;
            while (idx < tok_end) : (idx += 1) {
                switch (tree.source[idx]) {
                    0x00...0x09, 0x0b...0x1f, 0x7f => break,
                    else => {},
                }
            }
            break :blk idx - tok_start;
        };

        const ast_err: Ast.Error = .{
            .tag = Ast.Error.Tag.invalid_byte,
            .token = tok,
            .extra = .{ .offset = bad_off },
        };
        msg.clearRetainingCapacity();
        tree.renderError(ast_err, msg_w) catch return error.OutOfMemory;
        return try astgen.appendErrorTokNotesOff(tok, bad_off, "{s}", .{msg.written()}, notes.items);
    }

    var cur_err = tree.errors[0];
    for (tree.errors[1..]) |err| {
        if (err.is_note) {
            tree.renderError(err, msg_w) catch return error.OutOfMemory;
            try notes.append(gpa, try astgen.errNoteTok(err.token, "{s}", .{msg.written()}));
        } else {
            // Flush error
            const extra_offset = tree.errorOffset(cur_err);
            tree.renderError(cur_err, msg_w) catch return error.OutOfMemory;
            try astgen.appendErrorTokNotesOff(cur_err.token, extra_offset, "{s}", .{msg.written()}, notes.items);
            notes.clearRetainingCapacity();
            cur_err = err;

            // TODO: `Parse` currently does not have good error recovery mechanisms, so the remaining errors could be bogus.
            // As such, we'll ignore all remaining errors for now. We should improve `Parse` so that we can report all the errors.
            return;
        }
        msg.clearRetainingCapacity();
    }

    // Flush error
    const extra_offset = tree.errorOffset(cur_err);
    tree.renderError(cur_err, msg_w) catch return error.OutOfMemory;
    try astgen.appendErrorTokNotesOff(cur_err.token, extra_offset, "{s}", .{msg.written()}, notes.items);
}

const DeclarationName = union(enum) {
    named: Ast.TokenIndex,
    named_test: Ast.TokenIndex,
    decltest: Ast.TokenIndex,
    unnamed_test,
    @"comptime",
};

fn addFailedDeclaration(
    wip_decls: *WipDecls,
    gz: *GenZir,
    kind: Zir.Inst.Declaration.Unwrapped.Kind,
    name: Zir.NullTerminatedString,
    src_node: Ast.Node.Index,
    is_pub: bool,
) !void {
    const decl_inst = try gz.makeDeclaration(src_node);
    wip_decls.nextDecl(decl_inst);

    var dummy_gz = gz.makeSubBlock(&gz.base);

    var value_gz = gz.makeSubBlock(&gz.base); // scope doesn't matter here
    _ = try value_gz.add(.{
        .tag = .extended,
        .data = .{ .extended = .{
            .opcode = .astgen_error,
            .small = undefined,
            .operand = undefined,
        } },
    });

    try setDeclaration(decl_inst, .{
        .src_hash = @splat(0), // use a fixed hash to represent an AstGen failure; we don't care about source changes if AstGen still failed!
        .src_line = gz.astgen.source_line,
        .src_column = gz.astgen.source_column,
        .kind = kind,
        .name = name,
        .is_pub = is_pub,
        .is_threadlocal = false,
        .linkage = .normal,
        .type_gz = &dummy_gz,
        .align_gz = &dummy_gz,
        .linksection_gz = &dummy_gz,
        .addrspace_gz = &dummy_gz,
        .value_gz = &value_gz,
    });
}

/// Sets all extra data for a `declaration` instruction.
/// Unstacks `type_gz`, `align_gz`, `linksection_gz`, `addrspace_gz`, and `value_gz`.
fn setDeclaration(
    decl_inst: Zir.Inst.Index,
    args: struct {
        src_hash: std.zig.SrcHash,
        src_line: u32,
        src_column: u32,

        kind: Zir.Inst.Declaration.Unwrapped.Kind,
        name: Zir.NullTerminatedString,
        is_pub: bool,
        is_threadlocal: bool,
        linkage: Zir.Inst.Declaration.Unwrapped.Linkage,
        lib_name: Zir.NullTerminatedString = .empty,

        type_gz: *GenZir,
        /// Must be stacked on `type_gz`.
        align_gz: *GenZir,
        /// Must be stacked on `align_gz`.
        linksection_gz: *GenZir,
        /// Must be stacked on `linksection_gz`.
        addrspace_gz: *GenZir,
        /// Must be stacked on `addrspace_gz` and have nothing stacked on top of it.
        value_gz: *GenZir,
    },
) !void {
    const astgen = args.value_gz.astgen;
    const gpa = astgen.gpa;

    const type_body = args.type_gz.instructionsSliceUpto(args.align_gz);
    const align_body = args.align_gz.instructionsSliceUpto(args.linksection_gz);
    const linksection_body = args.linksection_gz.instructionsSliceUpto(args.addrspace_gz);
    const addrspace_body = args.addrspace_gz.instructionsSliceUpto(args.value_gz);
    const value_body = args.value_gz.instructionsSlice();

    const has_name = args.name != .empty;
    const has_lib_name = args.lib_name != .empty;
    const has_type_body = type_body.len != 0;
    const has_special_body = align_body.len != 0 or linksection_body.len != 0 or addrspace_body.len != 0;
    const has_value_body = value_body.len != 0;

    const id: Zir.Inst.Declaration.Flags.Id = switch (args.kind) {
        .unnamed_test => .unnamed_test,
        .@"test" => .@"test",
        .decltest => .decltest,
        .@"comptime" => .@"comptime",
        .@"const" => switch (args.linkage) {
            .normal => if (args.is_pub) id: {
                if (has_special_body) break :id .pub_const;
                if (has_type_body) break :id .pub_const_typed;
                break :id .pub_const_simple;
            } else id: {
                if (has_special_body) break :id .@"const";
                if (has_type_body) break :id .const_typed;
                break :id .const_simple;
            },
            .@"extern" => if (args.is_pub) id: {
                if (has_lib_name) break :id .pub_extern_const;
                if (has_special_body) break :id .pub_extern_const;
                break :id .pub_extern_const_simple;
            } else id: {
                if (has_lib_name) break :id .extern_const;
                if (has_special_body) break :id .extern_const;
                break :id .extern_const_simple;
            },
            .@"export" => if (args.is_pub) .pub_export_const else .export_const,
        },
        .@"var" => switch (args.linkage) {
            .normal => if (args.is_pub) id: {
                if (args.is_threadlocal) break :id .pub_var_threadlocal;
                if (has_special_body) break :id .pub_var;
                if (has_type_body) break :id .pub_var;
                break :id .pub_var_simple;
            } else id: {
                if (args.is_threadlocal) break :id .var_threadlocal;
                if (has_special_body) break :id .@"var";
                if (has_type_body) break :id .@"var";
                break :id .var_simple;
            },
            .@"extern" => if (args.is_pub) id: {
                if (args.is_threadlocal) break :id .pub_extern_var_threadlocal;
                break :id .pub_extern_var;
            } else id: {
                if (args.is_threadlocal) break :id .extern_var_threadlocal;
                break :id .extern_var;
            },
            .@"export" => if (args.is_pub) id: {
                if (args.is_threadlocal) break :id .pub_export_var_threadlocal;
                break :id .pub_export_var;
            } else id: {
                if (args.is_threadlocal) break :id .export_var_threadlocal;
                break :id .export_var;
            },
        },
    };

    assert(
```
