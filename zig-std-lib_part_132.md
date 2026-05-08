```
       vector_8_u16_type,
        vector_16_u16_type,
        vector_32_u16_type,
        vector_2_i32_type,
        vector_4_i32_type,
        vector_8_i32_type,
        vector_16_i32_type,
        vector_4_u32_type,
        vector_8_u32_type,
        vector_16_u32_type,
        vector_2_i64_type,
        vector_4_i64_type,
        vector_8_i64_type,
        vector_2_u64_type,
        vector_4_u64_type,
        vector_8_u64_type,
        vector_1_u128_type,
        vector_2_u128_type,
        vector_1_u256_type,
        vector_4_f16_type,
        vector_8_f16_type,
        vector_16_f16_type,
        vector_32_f16_type,
        vector_2_f32_type,
        vector_4_f32_type,
        vector_8_f32_type,
        vector_16_f32_type,
        vector_2_f64_type,
        vector_4_f64_type,
        vector_8_f64_type,
        optional_noreturn_type,
        anyerror_void_error_union_type,
        adhoc_inferred_error_set_type,
        generic_poison_type,
        empty_tuple_type,
        undef,
        undef_bool,
        undef_usize,
        undef_u1,
        zero,
        zero_usize,
        zero_u1,
        zero_u8,
        one,
        one_usize,
        one_u1,
        one_u8,
        four_u8,
        negative_one,
        void_value,
        unreachable_value,
        null_value,
        bool_true,
        bool_false,
        empty_tuple,

        /// This Ref does not correspond to any ZIR instruction or constant
        /// value and may instead be used as a sentinel to indicate null.
        none = std.math.maxInt(u32),

        _,

        pub const static_len = @typeInfo(@This()).@"enum".fields.len - 1;

        pub fn toIndex(inst: Ref) ?Index {
            assert(inst != .none);
            const ref_int = @intFromEnum(inst);
            if (ref_int >= static_len) {
                return @enumFromInt(ref_int - static_len);
            } else {
                return null;
            }
        }

        pub fn toIndexAllowNone(inst: Ref) ?Index {
            if (inst == .none) return null;
            return toIndex(inst);
        }
    };

    /// All instructions have an 8-byte payload, which is contained within
    /// this union. `Tag` determines which union field is active, as well as
    /// how to interpret the data within.
    pub const Data = union {
        /// Used for `Tag.extended`. The extended opcode determines the meaning
        /// of the `small` and `operand` fields.
        extended: Extended.InstData,
        /// Used for unary operators, with an AST node source location.
        un_node: struct {
            /// Offset from Decl AST node index.
            src_node: Ast.Node.Offset,
            /// The meaning of this operand depends on the corresponding `Tag`.
            operand: Ref,
        },
        /// Used for unary operators, with a token source location.
        un_tok: struct {
            /// Offset from Decl AST token index.
            src_tok: Ast.TokenOffset,
            /// The meaning of this operand depends on the corresponding `Tag`.
            operand: Ref,
        },
        pl_node: struct {
            /// Offset from Decl AST node index.
            /// `Tag` determines which kind of AST node this points to.
            src_node: Ast.Node.Offset,
            /// index into extra.
            /// `Tag` determines what lives there.
            payload_index: u32,
        },
        pl_tok: struct {
            /// Offset from Decl AST token index.
            src_tok: Ast.TokenOffset,
            /// index into extra.
            /// `Tag` determines what lives there.
            payload_index: u32,
        },
        bin: Bin,
        /// For strings which may contain null bytes.
        str: struct {
            /// Offset into `string_bytes`.
            start: NullTerminatedString,
            /// Number of bytes in the string.
            len: u32,

            pub fn get(self: @This(), code: Zir) []const u8 {
                return code.string_bytes[@intFromEnum(self.start)..][0..self.len];
            }
        },
        str_tok: struct {
            /// Offset into `string_bytes`. Null-terminated.
            start: NullTerminatedString,
            /// Offset from Decl AST token index.
            src_tok: Ast.TokenOffset,

            pub fn get(self: @This(), code: Zir) [:0]const u8 {
                return code.nullTerminatedString(self.start);
            }
        },
        /// Offset from Decl AST token index.
        tok: Ast.TokenOffset,
        /// Offset from Decl AST node index.
        node: Ast.Node.Offset,
        int: u64,
        float: f64,
        ptr_type: struct {
            flags: packed struct {
                is_allowzero: bool,
                is_mutable: bool,
                is_volatile: bool,
                has_sentinel: bool,
                has_align: bool,
                has_addrspace: bool,
                has_bit_range: bool,
                _: u1 = 0,
            },
            size: std.builtin.Type.Pointer.Size,
            /// Index into extra. See `PtrType`.
            payload_index: u32,
        },
        int_type: struct {
            /// Offset from Decl AST node index.
            /// `Tag` determines which kind of AST node this points to.
            src_node: Ast.Node.Offset,
            signedness: std.builtin.Signedness,
            bit_count: u16,
        },
        @"unreachable": struct {
            /// Offset from Decl AST node index.
            /// `Tag` determines which kind of AST node this points to.
            src_node: Ast.Node.Offset,
        },
        @"break": struct {
            operand: Ref,
            /// Index of a `Break` payload.
            payload_index: u32,
        },
        dbg_stmt: LineColumn,
        /// Used for unary operators which reference an inst,
        /// with an AST node source location.
        inst_node: struct {
            /// Offset from Decl AST node index.
            src_node: Ast.Node.Offset,
            /// The meaning of this operand depends on the corresponding `Tag`.
            inst: Index,
        },
        str_op: struct {
            /// Offset into `string_bytes`. Null-terminated.
            str: NullTerminatedString,
            operand: Ref,

            pub fn getStr(self: @This(), zir: Zir) [:0]const u8 {
                return zir.nullTerminatedString(self.str);
            }
        },
        @"defer": struct {
            index: u32,
            len: u32,
        },
        defer_err_code: struct {
            err_code: Ref,
            payload_index: u32,
        },
        save_err_ret_index: struct {
            operand: Ref, // If error type (or .none), save new trace index
        },
        elem_val_imm: struct {
            /// The indexable value being accessed.
            operand: Ref,
            /// The index being accessed.
            idx: u32,
        },
        declaration: struct {
            /// This node provides a new absolute baseline node for all instructions within this struct.
            src_node: Ast.Node.Index,
            /// index into extra to a `Declaration` payload.
            payload_index: u32,
        },

        // Make sure we don't accidentally add a field to make this union
        // bigger than expected. Note that in Debug builds, Zig is allowed
        // to insert a secret field for safety checks.
        comptime {
            if (builtin.mode != .Debug and builtin.mode != .ReleaseSafe) {
                assert(@sizeOf(Data) == 8);
            }
        }

        /// TODO this has to be kept in sync with `Data` which we want to be an untagged
        /// union. There is some kind of language awkwardness here and it has to do with
        /// deserializing an untagged union (in this case `Data`) from a file, and trying
        /// to preserve the hidden safety field.
        pub const FieldEnum = enum {
            extended,
            un_node,
            un_tok,
            pl_node,
            pl_tok,
            bin,
            str,
            str_tok,
            tok,
            node,
            int,
            float,
            ptr_type,
            int_type,
            @"unreachable",
            @"break",
            dbg_stmt,
            inst_node,
            str_op,
            @"defer",
            defer_err_code,
            save_err_ret_index,
            elem_val_imm,
            declaration,
        };
    };

    pub const Break = struct {
        operand_src_node: Ast.Node.OptionalOffset,
        block_inst: Index,
    };

    /// Trailing:
    /// 0. Output for every outputs_len
    /// 1. Input for every inputs_len
    pub const Asm = struct {
        src_node: Ast.Node.Offset,
        // null-terminated string index
        asm_source: NullTerminatedString,
        /// 1 bit for each outputs_len: whether it uses `-> T` or not.
        ///   0b0 - operand is a pointer to where to store the output.
        ///   0b1 - operand is a type; asm expression has the output as the result.
        /// 0b0X is the first output, 0bX0 is the second, etc.
        output_type_bits: u32,
        clobbers: Ref,

        pub const Small = packed struct(u16) {
            is_volatile: bool,
            outputs_len: u7,
            inputs_len: u8,
        };

        pub const Output = struct {
            /// index into string_bytes (null terminated)
            name: NullTerminatedString,
            /// index into string_bytes (null terminated)
            constraint: NullTerminatedString,
            /// How to interpret this is determined by `output_type_bits`.
            operand: Ref,
        };

        pub const Input = struct {
            /// index into string_bytes (null terminated)
            name: NullTerminatedString,
            /// index into string_bytes (null terminated)
            constraint: NullTerminatedString,
            operand: Ref,
        };
    };

    /// Trailing:
    /// if (ret_ty.body_len == 1) {
    ///   0. return_type: Ref
    /// }
    /// if (ret_ty.body_len > 1) {
    ///   1. return_type: Index // for each ret_ty.body_len
    /// }
    /// 2. body: Index // for each body_len
    /// 3. src_locs: SrcLocs // if body_len != 0
    /// 4. proto_hash: std.zig.SrcHash // if body_len != 0; hash of function prototype
    pub const Func = struct {
        ret_ty: RetTy,
        /// Points to the block that contains the param instructions for this function.
        /// If this is a `declaration`, it refers to the declaration's value body.
        param_block: Index,
        body_len: u32,

        pub const RetTy = packed struct(u32) {
            /// 0 means `void`.
            /// 1 means the type is a simple `Ref`.
            /// Otherwise, the length of a trailing body.
            body_len: u31,
            /// Whether the return type is generic, i.e. refers to one or more previous parameters.
            is_generic: bool,
        };

        pub const SrcLocs = struct {
            /// Line index in the source file relative to the parent decl.
            lbrace_line: u32,
            /// Line index in the source file relative to the parent decl.
            rbrace_line: u32,
            /// lbrace_column is least significant bits u16
            /// rbrace_column is most significant bits u16
            columns: u32,
        };
    };

    /// Trailing:
    /// if (has_cc_ref and !has_cc_body) {
    ///   0. cc: Ref,
    /// }
    /// if (has_cc_body) {
    ///   1. cc_body_len: u32
    ///   2. cc_body: u32 // for each cc_body_len
    /// }
    /// if (has_ret_ty_ref and !has_ret_ty_body) {
    ///   3. ret_ty: Ref,
    /// }
    /// if (has_ret_ty_body) {
    ///   4. ret_ty_body_len: u32
    ///   5. ret_ty_body: u32 // for each ret_ty_body_len
    /// }
    /// 6. noalias_bits: u32 // if has_any_noalias
    ///    - each bit starting with LSB corresponds to parameter indexes
    /// 7. body: Index // for each body_len
    /// 8. src_locs: Func.SrcLocs // if body_len != 0
    /// 9. proto_hash: std.zig.SrcHash // if body_len != 0; hash of function prototype
    pub const FuncFancy = struct {
        /// Points to the block that contains the param instructions for this function.
        /// If this is a `declaration`, it refers to the declaration's value body.
        param_block: Index,
        body_len: u32,
        bits: Bits,

        /// If both has_cc_ref and has_cc_body are false, it means auto calling convention.
        /// If both has_ret_ty_ref and has_ret_ty_body are false, it means void return type.
        pub const Bits = packed struct(u32) {
            is_var_args: bool,
            is_inferred_error: bool,
            is_noinline: bool,
            has_cc_ref: bool,
            has_cc_body: bool,
            has_ret_ty_ref: bool,
            has_ret_ty_body: bool,
            has_any_noalias: bool,
            ret_ty_is_generic: bool,
            _: u23 = 0,
        };
    };

    /// This data is stored inside extra, with trailing operands according to `operands_len`.
    /// Each operand is a `Ref`.
    pub const MultiOp = struct {
        operands_len: u32,
    };

    /// Trailing: operand: Ref, // for each `operands_len` (stored in `small`).
    pub const NodeMultiOp = struct {
        src_node: Ast.Node.Offset,
    };

    /// This data is stored inside extra, with trailing operands according to `body_len`.
    /// Each operand is an `Index`.
    pub const Block = struct {
        body_len: u32,
    };

    /// Trailing:
    /// * inst: Index // for each `body_len`
    pub const BlockComptime = struct {
        reason: std.zig.SimpleComptimeReason,
        body_len: u32,
    };

    /// Trailing:
    /// * inst: Index // for each `body_len`
    pub const BoolBr = struct {
        lhs: Ref,
        body_len: u32,
    };

    /// Trailing:
    /// 0. name: NullTerminatedString      // if `flags.id.hasName()`
    /// 1. lib_name: NullTerminatedString  // if `flags.id.hasLibName()`
    /// 2. type_body_len: u32              // if `flags.id.hasTypeBody()`
    /// 3. align_body_len: u32             // if `flags.id.hasSpecialBodies()`
    /// 4. linksection_body_len: u32       // if `flags.id.hasSpecialBodies()`
    /// 5. addrspace_body_len: u32         // if `flags.id.hasSpecialBodies()`
    /// 6. value_body_len: u32             // if `flags.id.hasValueBody()`
    /// 7. type_body_inst: Zir.Inst.Index
    ///    - for each `type_body_len`
    ///    - body to be exited via `break_inline` to this `declaration` instruction
    /// 8. align_body_inst: Zir.Inst.Index
    ///    - for each `align_body_len`
    ///    - body to be exited via `break_inline` to this `declaration` instruction
    /// 9. linksection_body_inst: Zir.Inst.Index
    ///    - for each `linksection_body_len`
    ///    - body to be exited via `break_inline` to this `declaration` instruction
    /// 10. addrspace_body_inst: Zir.Inst.Index
    ///    - for each `addrspace_body_len`
    ///    - body to be exited via `break_inline` to this `declaration` instruction
    /// 11. value_body_inst: Zir.Inst.Index
    ///    - for each `value_body_len`
    ///    - body to be exited via `break_inline` to this `declaration` instruction
    ///    - within this body, the `declaration` instruction refers to the resolved type from the type body
    pub const Declaration = struct {
        // These fields should be concatenated and reinterpreted as a `std.zig.SrcHash`.
        src_hash_0: u32,
        src_hash_1: u32,
        src_hash_2: u32,
        src_hash_3: u32,
        // These fields should be concatenated and reinterpreted as a `Flags`.
        flags_0: u32,
        flags_1: u32,

        pub const Unwrapped = struct {
            pub const Kind = enum {
                unnamed_test,
                @"test",
                decltest,
                @"comptime",
                @"const",
                @"var",
            };

            pub const Linkage = enum {
                normal,
                @"extern",
                @"export",
            };

            src_node: Ast.Node.Index,

            src_line: u32,
            src_column: u32,

            kind: Kind,
            /// Always `.empty` for `kind` of `unnamed_test`, `.@"comptime"`
            name: NullTerminatedString,
            /// Always `false` for `kind` of `unnamed_test`, `.@"test"`, `.decltest`, `.@"comptime"`.
            is_pub: bool,
            /// Always `false` for `kind != .@"var"`.
            is_threadlocal: bool,
            /// Always `.normal` for `kind != .@"const" and kind != .@"var"`.
            linkage: Linkage,
            /// Always `.empty` for `linkage != .@"extern"`.
            lib_name: NullTerminatedString,

            /// Always populated for `linkage == .@"extern".
            type_body: ?[]const Inst.Index,
            align_body: ?[]const Inst.Index,
            linksection_body: ?[]const Inst.Index,
            addrspace_body: ?[]const Inst.Index,
            /// Always populated for `linkage != .@"extern".
            value_body: ?[]const Inst.Index,
        };

        pub const Flags = packed struct(u64) {
            src_line: u30,
            src_column: u29,
            id: Id,

            pub const Id = enum(u5) {
                unnamed_test,
                @"test",
                decltest,
                @"comptime",

                const_simple,
                const_typed,
                @"const",
                pub_const_simple,
                pub_const_typed,
                pub_const,

                extern_const_simple,
                extern_const,
                pub_extern_const_simple,
                pub_extern_const,

                export_const,
                pub_export_const,

                var_simple,
                @"var",
                var_threadlocal,
                pub_var_simple,
                pub_var,
                pub_var_threadlocal,

                extern_var,
                extern_var_threadlocal,
                pub_extern_var,
                pub_extern_var_threadlocal,

                export_var,
                export_var_threadlocal,
                pub_export_var,
                pub_export_var_threadlocal,

                pub fn hasName(id: Id) bool {
                    return switch (id) {
                        .unnamed_test,
                        .@"comptime",
                        => false,
                        else => true,
                    };
                }

                pub fn hasLibName(id: Id) bool {
                    return switch (id) {
                        .extern_const,
                        .pub_extern_const,
                        .extern_var,
                        .extern_var_threadlocal,
                        .pub_extern_var,
                        .pub_extern_var_threadlocal,
                        => true,
                        else => false,
                    };
                }

                pub fn hasTypeBody(id: Id) bool {
                    return switch (id) {
                        .unnamed_test,
                        .@"test",
                        .decltest,
                        .@"comptime",
                        => false, // these constructs are untyped
                        .const_simple,
                        .pub_const_simple,
                        .var_simple,
                        .pub_var_simple,
                        => false, // these reprs omit type bodies
                        else => true,
                    };
                }

                pub fn hasValueBody(id: Id) bool {
                    return switch (id) {
                        .extern_const_simple,
                        .extern_const,
                        .pub_extern_const_simple,
                        .pub_extern_const,
                        .extern_var,
                        .extern_var_threadlocal,
                        .pub_extern_var,
                        .pub_extern_var_threadlocal,
                        => false, // externs do not have values
                        else => true,
                    };
                }

                pub fn hasSpecialBodies(id: Id) bool {
                    return switch (id) {
                        .unnamed_test,
                        .@"test",
                        .decltest,
                        .@"comptime",
                        => false, // these constructs are untyped
                        .const_simple,
                        .const_typed,
                        .pub_const_simple,
                        .pub_const_typed,
                        .extern_const_simple,
                        .pub_extern_const_simple,
                        .var_simple,
                        .pub_var_simple,
                        => false, // these reprs omit special bodies
                        else => true,
                    };
                }

                pub fn linkage(id: Id) Declaration.Unwrapped.Linkage {
                    return switch (id) {
                        .extern_const_simple,
                        .extern_const,
                        .pub_extern_const_simple,
                        .pub_extern_const,
                        .extern_var,
                        .extern_var_threadlocal,
                        .pub_extern_var,
                        .pub_extern_var_threadlocal,
                        => .@"extern",
                        .export_const,
                        .pub_export_const,
                        .export_var,
                        .export_var_threadlocal,
                        .pub_export_var,
                        .pub_export_var_threadlocal,
                        => .@"export",
                        else => .normal,
                    };
                }

                pub fn kind(id: Id) Declaration.Unwrapped.Kind {
                    return switch (id) {
                        .unnamed_test => .unnamed_test,
                        .@"test" => .@"test",
                        .decltest => .decltest,
                        .@"comptime" => .@"comptime",
                        .const_simple,
                        .const_typed,
                        .@"const",
                        .pub_const_simple,
                        .pub_const_typed,
                        .pub_const,
                        .extern_const_simple,
                        .extern_const,
                        .pub_extern_const_simple,
                        .pub_extern_const,
                        .export_const,
                        .pub_export_const,
                        => .@"const",
                        .var_simple,
                        .@"var",
                        .var_threadlocal,
                        .pub_var_simple,
                        .pub_var,
                        .pub_var_threadlocal,
                        .extern_var,
                        .extern_var_threadlocal,
                        .pub_extern_var,
                        .pub_extern_var_threadlocal,
                        .export_var,
                        .export_var_threadlocal,
                        .pub_export_var,
                        .pub_export_var_threadlocal,
                        => .@"var",
                    };
                }

                pub fn isPub(id: Id) bool {
                    return switch (id) {
                        .pub_const_simple,
                        .pub_const_typed,
                        .pub_const,
                        .pub_extern_const_simple,
                        .pub_extern_const,
                        .pub_export_const,
                        .pub_var_simple,
                        .pub_var,
                        .pub_var_threadlocal,
                        .pub_extern_var,
                        .pub_extern_var_threadlocal,
                        .pub_export_var,
                        .pub_export_var_threadlocal,
                        => true,
                        else => false,
                    };
                }

                pub fn isThreadlocal(id: Id) bool {
                    return switch (id) {
                        .var_threadlocal,
                        .pub_var_threadlocal,
                        .extern_var_threadlocal,
                        .pub_extern_var_threadlocal,
                        .export_var_threadlocal,
                        .pub_export_var_threadlocal,
                        => true,
                        else => false,
                    };
                }
            };
        };

        pub const Name = enum(u32) {
            @"comptime" = std.math.maxInt(u32),
            unnamed_test = std.math.maxInt(u32) - 1,
            /// Other values are `NullTerminatedString` values, i.e. index into
            /// `string_bytes`. If the byte referenced is 0, the decl is a named
            /// test, and the actual name begins at the following byte.
            _,

            pub fn isNamedTest(name: Name, zir: Zir) bool {
                return switch (name) {
                    .@"comptime", .unnamed_test => false,
                    _ => zir.string_bytes[@intFromEnum(name)] == 0,
                };
            }
            pub fn toString(name: Name, zir: Zir) ?NullTerminatedString {
                switch (name) {
                    .@"comptime", .unnamed_test => return null,
                    _ => {},
                }
                const idx: u32 = @intFromEnum(name);
                if (zir.string_bytes[idx] == 0) {
                    // Named test
                    return @enumFromInt(idx + 1);
                }
                return @enumFromInt(idx);
            }
        };

        pub const Bodies = struct {
            type_body: ?[]const Index,
            align_body: ?[]const Index,
            linksection_body: ?[]const Index,
            addrspace_body: ?[]const Index,
            value_body: ?[]const Index,
        };

        pub fn getBodies(declaration: Declaration, extra_end: u32, zir: Zir) Bodies {
            var extra_index: u32 = extra_end;
            const value_body_len = declaration.value_body_len;
            const type_body_len: u32 = len: {
                if (!declaration.flags().kind.hasTypeBody()) break :len 0;
                const len = zir.extra[extra_index];
                extra_index += 1;
                break :len len;
            };
            const align_body_len, const linksection_body_len, const addrspace_body_len = lens: {
                if (!declaration.flags.kind.hasSpecialBodies()) {
                    break :lens .{ 0, 0, 0 };
                }
                const lens = zir.extra[extra_index..][0..3].*;
                extra_index += 3;
                break :lens lens;
            };
            return .{
                .type_body = if (type_body_len == 0) null else b: {
                    const b = zir.bodySlice(extra_index, type_body_len);
                    extra_index += type_body_len;
                    break :b b;
                },
                .align_body = if (align_body_len == 0) null else b: {
                    const b = zir.bodySlice(extra_index, align_body_len);
                    extra_index += align_body_len;
                    break :b b;
                },
                .linksection_body = if (linksection_body_len == 0) null else b: {
                    const b = zir.bodySlice(extra_index, linksection_body_len);
                    extra_index += linksection_body_len;
                    break :b b;
                },
                .addrspace_body = if (addrspace_body_len == 0) null else b: {
                    const b = zir.bodySlice(extra_index, addrspace_body_len);
                    extra_index += addrspace_body_len;
                    break :b b;
                },
                .value_body = if (value_body_len == 0) null else b: {
                    const b = zir.bodySlice(extra_index, value_body_len);
                    extra_index += value_body_len;
                    break :b b;
                },
            };
        }
    };

    /// Stored inside extra, with trailing arguments according to `args_len`.
    /// Implicit 0. arg_0_start: u32, // always same as `args_len`
    /// 1. arg_end: u32, // for each `args_len`
    /// arg_N_start is the same as arg_N-1_end
    pub const Call = struct {
        // Note: Flags *must* come first so that unusedResultExpr
        // can find it when it goes to modify them.
        flags: Flags,
        callee: Ref,

        pub const Flags = packed struct {
            /// std.builtin.CallModifier in packed form
            pub const PackedModifier = u3;
            pub const PackedArgsLen = u27;

            packed_modifier: PackedModifier,
            ensure_result_used: bool = false,
            pop_error_return_trace: bool,
            args_len: PackedArgsLen,

            comptime {
                if (@sizeOf(Flags) != 4 or @bitSizeOf(Flags) != 32)
                    @compileError("Layout of Call.Flags needs to be updated!");
                if (@bitSizeOf(std.builtin.CallModifier) != @bitSizeOf(PackedModifier))
                    @compileError("Call.Flags.PackedModifier needs to be updated!");
            }
        };
    };

    /// Stored inside extra, with trailing arguments according to `args_len`.
    /// Implicit 0. arg_0_start: u32, // always same as `args_len`
    /// 1. arg_end: u32, // for each `args_len`
    /// arg_N_start is the same as arg_N-1_end
    pub const FieldCall = struct {
        // Note: Flags *must* come first so that unusedResultExpr
        // can find it when it goes to modify them.
        flags: Call.Flags,
        obj_ptr: Ref,
        /// Offset into `string_bytes`.
        field_name_start: NullTerminatedString,
    };

    /// There is a body of instructions at `extra[body_index..][0..body_len]`.
    /// Trailing:
    /// 0. operand: Ref // for each `operands_len`
    pub const TypeOfPeer = struct {
        src_node: Ast.Node.Offset,
        body_len: u32,
        body_index: u32,
    };

    pub const BuiltinCall = struct {
        // Note: Flags *must* come first so that unusedResultExpr
        // can find it when it goes to modify them.
        flags: Flags,
        modifier: Ref,
        callee: Ref,
        args: Ref,

        pub const Flags = packed struct {
            is_nosuspend: bool,
            ensure_result_used: bool,
            _: u30 = 0,

            comptime {
                if (@sizeOf(Flags) != 4 or @bitSizeOf(Flags) != 32)
                    @compileError("Layout of BuiltinCall.Flags needs to be updated!");
            }
        };
    };

    /// This data is stored inside extra, with two sets of trailing `Ref`:
    /// * 0. the then body, according to `then_body_len`.
    /// * 1. the else body, according to `else_body_len`.
    pub const CondBr = struct {
        condition: Ref,
        then_body_len: u32,
        else_body_len: u32,
    };

    /// This data is stored inside extra, trailed by:
    /// * 0. body: Index //  for each `body_len`.
    pub const Try = struct {
        /// The error union to unwrap.
        operand: Ref,
        body_len: u32,
    };

    /// Stored in extra. Depending on the flags in Data, there will be up to 5
    /// trailing Ref fields:
    /// 0. sentinel: Ref // if `has_sentinel` flag is set
    /// 1. align: Ref // if `has_align` flag is set
    /// 2. address_space: Ref // if `has_addrspace` flag is set
    /// 3. bit_start: Ref // if `has_bit_range` flag is set
    /// 4. host_size: Ref // if `has_bit_range` flag is set
    pub const PtrType = struct {
        elem_type: Ref,
        src_node: Ast.Node.Offset,
    };

    pub const ArrayTypeSentinel = struct {
        len: Ref,
        sentinel: Ref,
        elem_type: Ref,
    };

    pub const SliceStart = struct {
        lhs: Ref,
        start: Ref,
    };

    pub const SliceEnd = struct {
        lhs: Ref,
        start: Ref,
        end: Ref,
    };

    pub const SliceSentinel = struct {
        lhs: Ref,
        start: Ref,
        end: Ref,
        sentinel: Ref,
    };

    pub const SliceLength = struct {
        lhs: Ref,
        start: Ref,
        len: Ref,
        sentinel: Ref,
        start_src_node_offset: Ast.Node.Offset,
    };

    /// The meaning of these operands depends on the corresponding `Tag`.
    pub const Bin = struct {
        lhs: Ref,
        rhs: Ref,
    };

    pub const BinNode = struct {
        node: Ast.Node.Offset,
        lhs: Ref,
        rhs: Ref,
    };

    pub const ReifySliceArgInfo = enum(u16) {
        /// Input element type is `type`.
        /// Output element type is `std.builtin.Type.Fn.Param.Attributes`.
        type_to_fn_param_attrs,
        /// Input element type is `[]const u8`.
        /// Output element type is `type`.
        string_to_struct_field_type,
        /// Identical to `string_to_struct_field_type` aside from emitting slightly different error messages.
        string_to_union_field_type,
        /// Input element type is `[]const u8`.
        /// Output element type is `std.builtin.Type.StructField.Attributes`.
        string_to_struct_field_attrs,
        /// Input element type is `[]const u8`.
        /// Output element type is `std.builtin.Type.UnionField.Attributes`.
        string_to_union_field_attrs,
    };

    pub const RoundOp = enum(u16) {
        round,
        floor,
        ceil,
        trunc,
    };

    pub const UnNode = struct {
        node: Ast.Node.Offset,
        operand: Ref,
    };

    pub const ElemPtrImm = struct {
        ptr: Ref,
        index: u32,
    };

    pub const ReifyPointer = struct {
        node: Ast.Node.Offset,
        size: Ref,
        attrs: Ref,
        elem_ty: Ref,
        sentinel: Ref,
    };

    pub const ReifyFn = struct {
        node: Ast.Node.Offset,
        param_types: Ref,
        param_attrs: Ref,
        ret_ty: Ref,
        fn_attrs: Ref,
    };

    pub const ReifyStruct = struct {
        src_line: u32,
        /// This node is absolute, because `reify` instructions are tracked across updates, and
        /// this simplifies the logic for getting source locations for types.
        node: Ast.Node.Index,
        layout: Ref,
        backing_ty: Ref,
        field_names: Ref,
        field_types: Ref,
        field_attrs: Ref,
    };

    pub const ReifyUnion = struct {
        src_line: u32,
        /// This node is absolute, because `reify` instructions are tracked across updates, and
        /// this simplifies the logic for getting source locations for types.
        node: Ast.Node.Index,
        layout: Ref,
        arg_ty: Ref,
        field_names: Ref,
        field_types: Ref,
        field_attrs: Ref,
    };

    pub const ReifyEnum = struct {
        src_line: u32,
        /// This node is absolute, because `reify` instructions are tracked across updates, and
        /// this simplifies the logic for getting source locations for types.
        node: Ast.Node.Index,
        tag_ty: Ref,
        mode: Ref,
        field_names: Ref,
        field_values: Ref,
    };

    /// Trailing:
    /// 0. multi_cases_len: u32, // If has_multi_cases is set.
    /// 1. payload_capture_placeholder: Inst.Index, // If payload_capture_inst_is_placeholder is set.
    ///                                             // Index of instruction prongs use to refer to their payload capture.
    /// 2. tag_capture_placeholder: Inst.Index, // If tag_capture_inst_is_placeholder is set.
    ///                                         // Index of instruction prongs use to refer to their tag capture.
    /// 3. catch_or_if_src_node_offset: Ast.Node.Offset, // If inst is switch_block_err_union.
    /// 4. non_err_info: ProngInfo.NonErr, // If inst is switch_block_err_union.
    /// 5. else_info: ProngInfo.Else, // If has_else is set.
    /// 6. scalar_prong_info: ProngInfo, // for every scalar_cases_len
    /// 7. multi_prong_info: ProngInfo, // for every multi_cases_len
    /// 8. multi_case_items_len: u32, // for every multi_cases_len
    /// 9. multi_case_ranges_len: u32, // If has_ranges is set: for every multi_cases_len
    /// 10. scalar_item_info: ItemInfo, // for every scalar_cases_len
    /// 11. multi_items_info: { // for every multi_cases_len
    ///        item_info: ItemInfo, // for each multi_case_items_len
    ///        range_items_info: { // for each multi_case_ranges_len
    ///            first_info: ItemInfo,
    ///            last_info: ItemInfo,
    ///        }
    ///    }
    /// 12. non_err_body {
    ///        body_inst: Index // for every non_err_info.body_len
    ///     }
    /// 13. else_body: { // If has_else is set.
    ///        body_inst: Inst.Index, // for every else_info.body_len
    ///    }
    /// 14. scalar_bodies: { // for every scalar_cases_len
    ///        prong_body: { // for each body_len in scalar_prong_info
    ///            body_inst: Inst.Index, // for every body_len
    ///        }
    ///        item_body: { // for each body_len in scalar_item_info
    ///            body_inst: Inst.Index, // for every body_len
    ///        }
    ///    }
    /// 15. multi_bodies: { // for each multi_items_info
    ///        prong_body: {
    ///            body_inst: Inst.Index, // for each multi_prong_info.body_len
    ///        }
    ///        item_body: { // for each item_info
    ///            body_inst: Inst.Index, // for every item_info.body_len
    ///        }
    ///        range_bodies: { // for each .{first_info, last_info} in range_items_info
    ///            first_body_inst: Inst.Index, // for every first_info.body_len
    ///            last_body_inst: Inst.Index, // for every last_info.body_len
    ///        }
    ///    }
    pub const SwitchBlock = struct {
        /// Either `catch`/`if` or `switch` operand.
        raw_operand: Ref,
        bits: Bits,

        pub const Bits = packed struct(u32) {
            /// If true, one or more prongs have multiple items.
            has_multi_cases: bool,
            /// If true, one or more prongs have ranges.
            /// Only valid if `has_multi_cases` is also set.
            any_ranges: bool,
            has_else: bool,
            has_under: bool,
            /// If true, at least one prong contains a `continue`.
            /// Only valid if `has_label` is set.
            has_continue: bool,
            // If true, at least one prong has a non-inline payload/tag capture.
            any_maybe_runtime_capture: bool,
            payload_capture_inst_is_placeholder: bool,
            tag_capture_inst_is_placeholder: bool,
            scalar_cases_len: ScalarCasesLen,

            // NOTE maybe don't steal any more bits from poor `scalar_cases_len`
            // and split `Bits` into two parts instead, `raw_operand` surely
            // wouldn't mind donating a couple of bits for that purpose...
            pub const ScalarCasesLen = u24;
        };

        pub const ProngInfo = packed struct(u32) {
            body_len: u27,
            capture: ProngInfo.Capture,
            is_inline: bool,
            has_tag_capture: bool,
            is_comptime_unreach: bool,

            pub const Capture = enum(u2) {
                none,
                by_val,
                by_ref,
            };

            pub const NonErr = packed struct(u32) {
                body_len: u29,
                capture: ProngInfo.Capture,
                operand_is_ref: bool,
            };

            pub const Else = packed struct(u32) {
                body_len: u27,
                capture: ProngInfo.Capture,
                is_inline: bool,
                has_tag_capture: bool,
                is_simple_noreturn: bool,
            };
        };

        pub const ItemInfo = packed struct(u32) {
            kind: ItemInfo.Kind,
            data: u30,

            pub const Kind = enum(u2) {
                enum_literal,
                error_value,
                body_len,
                under,
            };

            pub const Unwrapped = union(ItemInfo.Kind) {
                enum_literal: Zir.NullTerminatedString,
                error_value: Zir.NullTerminatedString,
                body_len: u32,
                under,
            };

            pub fn wrap(unwrapped: ItemInfo.Unwrapped) ItemInfo {
                const data_uncasted: u32 = switch (unwrapped) {
                    .enum_literal => |str_index| @intFromEnum(str_index),
                    .error_value => |str_index| @intFromEnum(str_index),
                    .body_len => |body_len| body_len,
                    .under => 0,
                };
                return .{ .kind = unwrapped, .data = @intCast(data_uncasted) };
            }

            pub fn unwrap(item_info: ItemInfo) ItemInfo.Unwrapped {
                return switch (item_info.kind) {
                    .enum_literal => .{ .enum_literal = @enumFromInt(item_info.data) },
                    .error_value => .{ .error_value = @enumFromInt(item_info.data) },
                    .body_len => .{ .body_len = item_info.data },
                    .under => .under,
                };
            }

            pub fn bodyLen(item_info: ItemInfo) ?u32 {
                return if (item_info.kind == .body_len) item_info.data else null;
            }
        };
    };

    pub const ArrayInitRefTy = struct {
        ptr_ty: Ref,
        elem_count: u32,
    };

    pub const Field = struct {
        lhs: Ref,
        /// Offset into `string_bytes`.
        field_name_start: NullTerminatedString,
    };

    pub const FieldNamed = struct {
        lhs: Ref,
        field_name: Ref,
    };

    pub const As = struct {
        dest_type: Ref,
        operand: Ref,
    };

    /// Trailing:
    /// 0.  captures_len: u32 // if `has_captures_len`
    /// 1.  decls_len: u32, // if `has_decls_len`
    /// 2.  fields_len: u32, // if `has_fields_len`
    /// 3.  backing_int_body_len: u32 // if `has_backing_int`
    /// 4.  capture: Capture // for every `captures_len`
    /// 5.  capture_name: NullTerminatedString // for every `captures_len`
    /// 6.  decl: Index, // for every `decls_len`; points to a `declaration` instruction
    /// 7.  field_name: NullTerminatedString // for every `fields_len`
    /// 8.  field_type_body_len: u32 // for every `fields_len`
    /// 9.  field_align_body_len: u32 // for every `fields_len` if `any_field_aligns`
    /// 10. field_default_body_len: u32 // for every `fields_len` if `any_field_defaults`
    /// 11. field_comptime_bits: u32 // one bit per `fields_len` if `any_comptime_fields`
    ///                              // LSB is first field, minimum number of `u32` needed
    /// 12. backing_int_body_inst: Inst.Index // for each `backing_int_body_len`
    /// 13. body_inst: Inst.Index // type body, then align body, then default body, for each field
    pub const StructDecl = struct {
        // These fields should be concatenated and reinterpreted as a `std.zig.SrcHash`.
        // This hash contains the source of all fields, and any specified attributes (`extern`, backing type, etc).
        fields_hash_0: u32,
        fields_hash_1: u32,
        fields_hash_2: u32,
        fields_hash_3: u32,
        src_line: u32,
        /// This node provides a new absolute baseline node for all instructions within this struct.
        src_node: Ast.Node.Index,

        pub const Small = packed struct(u16) {
            has_captures_len: bool,
            has_decls_len: bool,
            has_fields_len: bool,
            name_strategy: NameStrategy,
            layout: std.builtin.Type.ContainerLayout,
            /// Always `false` if `layout != .@"packed"`.
            has_backing_int_type: bool,
            any_field_aligns: bool,
            any_field_defaults: bool,
            any_comptime_fields: bool,
            _: u5 = 0,
        };
    };

    /// Represents a single value being captured in a type declaration's closure.
    pub const Capture = packed struct(u32) {
        tag: enum(u3) {
            /// `data` is a `u16` index into the parent closure.
            nested,
            /// `data` is a `Zir.Inst.Index` to an instruction whose value is being captured.
            instruction,
            /// `data` is a `Zir.Inst.Index` to an instruction representing an alloc whose contents is being captured.
            instruction_load,
            /// `data` is a `NullTerminatedString` to a decl name.
            decl_val,
            /// `data` is a `NullTerminatedString` to a decl name.
            decl_ref,
        },
        data: u29,
        pub const Unwrapped = union(enum) {
            nested: u16,
            instruction: Zir.Inst.Index,
            instruction_load: Zir.Inst.Index,
            decl_val: NullTerminatedString,
            decl_ref: NullTerminatedString,
        };
        pub fn wrap(cap: Unwrapped) Capture {
            return switch (cap) {
                .nested => |idx| .{
                    .tag = .nested,
                    .data = idx,
                },
                .instruction => |inst| .{
                    .tag = .instruction,
                    .data = @intCast(@intFromEnum(inst)),
                },
                .instruction_load => |inst| .{
                    .tag = .instruction_load,
                    .data = @intCast(@intFromEnum(inst)),
                },
                .decl_val => |str| .{
                    .tag = .decl_val,
                    .data = @intCast(@intFromEnum(str)),
                },
                .decl_ref => |str| .{
                    .tag = .decl_ref,
                    .data = @intCast(@intFromEnum(str)),
                },
            };
        }
        pub fn unwrap(cap: Capture) Unwrapped {
            return switch (cap.tag) {
                .nested => .{ .nested = @intCast(cap.data) },
                .instruction => .{ .instruction = @enumFromInt(cap.data) },
                .instruction_load => .{ .instruction_load = @enumFromInt(cap.data) },
                .decl_val => .{ .decl_val = @enumFromInt(cap.data) },
                .decl_ref => .{ .decl_ref = @enumFromInt(cap.data) },
            };
        }
    };

    pub const NameStrategy = enum(u2) {
        /// Use the same name as the parent declaration name.
        /// e.g. `const Foo = struct {...};`.
        parent,
        /// Use the name of the currently executing comptime function call,
        /// with the current parameters. e.g. `ArrayList(i32)`.
        func,
        /// Create an anonymous name for this declaration.
        /// Like this: "ParentDeclName_struct_69"
        anon,
        /// Use the name specified in the next `dbg_var_{val,ptr}` instruction.
        dbg_var,
    };

    pub const FullPtrCastFlags = packed struct(u5) {
        ptr_cast: bool = false,
        align_cast: bool = false,
        addrspace_cast: bool = false,
        const_cast: bool = false,
        volatile_cast: bool = false,

        pub inline fn needResultTypeBuiltinName(flags: FullPtrCastFlags) []const u8 {
            if (flags.ptr_cast) return "@ptrCast";
            if (flags.align_cast) return "@alignCast";
            if (flags.addrspace_cast) return "@addrSpaceCast";
            unreachable;
        }
    };

    pub const BuiltinValue = enum(u16) {
        // Types
        atomic_order,
        atomic_rmw_op,
        calling_convention,
        address_space,
        float_mode,
        signedness,
        reduce_op,
        call_modifier,
        prefetch_options,
        export_options,
        extern_options,
        branch_hint,
        clobbers,
        pointer_size,
        pointer_attributes,
        fn_attributes,
        container_layout,
        enum_mode,
        // Values
        calling_convention_c,
        calling_convention_inline,
    };

    pub const InplaceOp = enum(u16) {
        add_eq,
        sub_eq,
    };

    /// Trailing:
    /// 0. captures_len: u32, // if has_captures_len
    /// 1. decls_len: u32, // if has_decls_len
    /// 2. fields_len: u32, // if has_fields_len
    /// 3. tag_type_body_len: u32, // if has_tag_type
    /// 4. capture: Capture // for every `captures_len`
    /// 5. capture_name: NullTerminatedString // for every `captures_len`
    /// 6. decl: Index, // for every `decls_len`; points to a `declaration` instruction
    /// 7. field_name: NullTerminatedString // for every `fields_len`
    /// 8. field_value_body_len: u32 // for every `fields_len` if `any_field_values`
    /// 9. tag_type_body_inst: Inst.Index // for each `tag_type_body_len`
    /// 10. body_inst: Inst.Index // value body for each field
    pub const EnumDecl = struct {
        // These fields should be concatenated and reinterpreted as a `std.zig.SrcHash`.
        // This hash contains the source of all fields, and the backing type if specified.
        fields_hash_0: u32,
        fields_hash_1: u32,
        fields_hash_2: u32,
        fields_hash_3: u32,
        src_line: u32,
        /// This node provides a new absolute baseline node for all instructions within this struct.
        src_node: Ast.Node.Index,

        pub const Small = packed struct(u16) {
            has_captures_len: bool,
            has_decls_len: bool,
            has_fields_len: bool,
            name_strategy: NameStrategy,
            has_tag_type: bool,
            nonexhaustive: bool,
            any_field_values: bool,
            _: u8 = 0,
        };
    };

    /// Trailing:
    /// 0.  captures_len: u32 // if `has_captures_len`
    /// 1.  decls_len: u32, // if `has_decls_len`
    /// 2.  fields_len: u32, // if `has_fields_len`
    /// 3.  arg_type_body_len: u32, // if `kind.hasArgType()`
    /// 4.  capture: Capture // for every `captures_len`
    /// 5.  capture_name: NullTerminatedString // for every `captures_len`
    /// 6.  decl: Index, // for every `decls_len`; points to a `declaration` instruction
    /// 7.  field_name: NullTerminatedString // for every `fields_len`
    /// 8.  field_type_body_len: u32 // for every `fields_len`
    /// 9 . field_align_body_len: u32 // for every `fields_len` if `any_field_aligns`
    /// 10. field_value_body_len: u32 // for every `fields_len` if `any_field_values`
    /// 11. arg_type_body_inst: Inst.Index // for each `arg_type_body_len`
    /// 12. body_inst: Inst.Index // type body, then align body, then value body, for each field
    pub const UnionDecl = struct {
        // These fields should be concatenated and reinterpreted as a `std.zig.SrcHash`.
        // This hash contains the source of all fields, and any specified attributes (`extern` etc).
        fields_hash_0: u32,
        fields_hash_1: u32,
        fields_hash_2: u32,
        fields_hash_3: u32,
        src_line: u32,
        /// This node provides a new absolute baseline node for all instructions within this struct.
        src_node: Ast.Node.Index,

        pub const Small = packed struct(u16) {
            has_captures_len: bool,
            has_decls_len: bool,
            has_fields_len: bool,
            name_strategy: NameStrategy,
            kind: Kind,
            any_field_aligns: bool,
            any_field_values: bool,
            _: u6 = 0,
        };

        pub const Kind = enum(u3) {
            /// `union`
            auto,
            /// `union(T)`
            tagged_explicit,
            /// `union(enum)`
            tagged_enum,
            /// `union(enum(T))`
            tagged_enum_explicit,
            /// `extern union`
            @"extern",
            /// `packed union`
            @"packed",
            /// `packed union(T)`
            packed_explicit,

            pub fn hasArgType(k: Kind) bool {
                return switch (k) {
                    .auto, .tagged_enum, .@"extern", .@"packed" => false,
                    .tagged_explicit, .tagged_enum_explicit, .packed_explicit => true,
                };
            }

            pub fn layout(k: Kind) std.builtin.Type.ContainerLayout {
                return switch (k) {
                    .auto, .tagged_explicit, .tagged_enum, .tagged_enum_explicit => .auto,
                    .@"extern" => .@"extern",
                    .@"packed", .packed_explicit => .@"packed",
                };
            }
        };
    };

    /// Trailing:
    /// 0. captures_len: u32, // if has_captures_len
    /// 1. decls_len: u32, // if has_decls_len
    /// 2. capture: Capture, // for every captures_len
    /// 3. capture_name: NullTerminatedString // for every captures_len
    /// 4. decl: Index, // for every decls_len; points to a `declaration` instruction
    pub const OpaqueDecl = struct {
        src_line: u32,
        /// This node provides a new absolute baseline node for all instructions within this struct.
        src_node: Ast.Node.Index,

        pub const Small = packed struct(u16) {
            has_captures_len: bool,
            has_decls_len: bool,
            name_strategy: NameStrategy,
            _: u12 = 0,
        };
    };

    /// Trailing:
    /// 1. fields: { // for every `fields_len` (stored in `extended.small`)
    ///        type: Inst.Ref,
    ///        init: Inst.Ref, // `.none` for non-`comptime` fields
    ///    }
    pub const TupleDecl = struct {
        src_node: Ast.Node.Offset,
    };

    /// Trailing:
    /// 0. field_name: NullTerminatedString // for every fields_len
    pub const ErrorSetDecl = struct {
        fields_len: u32,
    };

    /// A f128 value, broken up into 4 u32 parts.
    pub const Float128 = struct {
        piece0: u32,
        piece1: u32,
        piece2: u32,
        piece3: u32,

        pub fn get(self: Float128) f128 {
            const int_bits = @as(u128, self.piece0) |
                (@as(u128, self.piece1) << 32) |
                (@as(u128, self.piece2) << 64) |
                (@as(u128, self.piece3) << 96);
            return @as(f128, @bitCast(int_bits));
        }
    };

    /// Trailing is an item per field.
    pub const StructInit = struct {
        /// If this is an anonymous initialization (the operand is poison), this instruction becomes the owner of a type.
        /// To resolve source locations, we need an absolute source node.
        abs_node: Ast.Node.Index,
        /// Likewise, we need an absolute line number.
        abs_line: u32,
        fields_len: u32,

        pub const Item = struct {
            /// The `struct_init_field_type` ZIR instruction for this field init.
            field_type: Index,
            /// The field init expression to be used as the field value. This value will be coerced
            /// to the field type if not already.
            init: Ref,
        };
    };

    /// Trailing is an Item per field.
    /// TODO make this instead array of inits followed by array of names because
    /// it will be simpler Sema code and better for CPU cache.
    pub const StructInitAnon = struct {
        /// This is an anonymous initialization, meaning this instruction becomes the owner of a type.
        /// To resolve source locations, we need an absolute source node.
        abs_node: Ast.Node.Index,
        /// Likewise, we need an absolute line number.
        abs_line: u32,
        fields_len: u32,

        pub const Item = struct {
            /// Null-terminated string table index.
            field_name: NullTerminatedString,
            /// The field init expression to be used as the field value.
            init: Ref,
        };
    };

    pub const FieldType = struct {
        container_type: Ref,
        /// Offset into `string_bytes`, null terminated.
        name_start: NullTerminatedString,
    };

    pub const FieldTypeRef = struct {
        container_type: Ref,
        field_name: Ref,
    };

    pub const Cmpxchg = struct {
        node: Ast.Node.Offset,
        ptr: Ref,
        expected_value: Ref,
        new_value: Ref,
        success_order: Ref,
        failure_order: Ref,
    };

    pub const AtomicRmw = struct {
        ptr: Ref,
        operation: Ref,
        operand: Ref,
        ordering: Ref,
    };

    pub const UnionInit = struct {
        union_type: Ref,
        field_name: Ref,
        init: Ref,
    };

    pub const AtomicStore = struct {
        ptr: Ref,
        operand: Ref,
        ordering: Ref,
    };

    pub const AtomicLoad = struct {
        elem_type: Ref,
        ptr: Ref,
        ordering: Ref,
    };

    pub const MulAdd = struct {
        mulend1: Ref,
        mulend2: Ref,
        addend: Ref,
    };

    pub const FieldParentPtr = struct {
        src_node: Ast.Node.Offset,
        parent_ptr_type: Ref,
        field_name: Ref,
        field_ptr: Ref,
    };

    pub const Shuffle = struct {
        elem_type: Ref,
        a: Ref,
        b: Ref,
        mask: Ref,
    };

    pub const Select = struct {
        node: Ast.Node.Offset,
        elem_type: Ref,
        pred: Ref,
        a: Ref,
        b: Ref,
    };

    /// Trailing: inst: Index // for every body_len
    pub const Param = struct {
        /// Null-terminated string index.
        name: NullTerminatedString,
        type: Type,

        pub const Type = packed struct(u32) {
            /// The body contains the type of the parameter.
            body_len: u31,
            /// Whether the type is generic, i.e. refers to one or more previous parameters.
            is_generic: bool,
        };
    };

    /// Trailing:
    /// 0. type_inst: Ref,  // if small 0b000X is set
    /// 1. align_inst: Ref, // if small 0b00X0 is set
    pub const AllocExtended = struct {
        src_node: Ast.Node.Offset,

        pub const Small = packed struct(u16) {
            has_type: bool,
            has_align: bool,
            is_const: bool,
            is_comptime: bool,
            _: u12 = 0,
        };
    };

    pub const Export = struct {
        exported: Ref,
        options: Ref,
    };

    /// Trailing: `CompileErrors.Item` for each `items_len`.
    pub const CompileErrors = struct {
        items_len: u32,

        /// Trailing: `note_payload_index: u32` for each `notes_len`.
        /// It's a payload index of another `Item`.
        pub const Item = struct {
            /// null terminated string index
            msg: NullTerminatedString,
            node: Ast.Node.OptionalIndex,
            /// If node is .none then this will be populated.
            token: Ast.OptionalTokenIndex,
            /// Can be used in combination with `token`.
            byte_offset: u32,
            /// 0 or a payload index of a `Block`, each is a payload
            /// index of another `Item`.
            notes: u32,

            pub fn notesLen(item: Item, zir: Zir) u32 {
                if (item.notes == 0) return 0;
                const block = zir.extraData(Block, item.notes);
                return block.data.body_len;
            }
        };
    };

    /// Trailing: for each `imports_len` there is an Item
    pub const Imports = struct {
        imports_len: u32,

        pub const Item = struct {
            /// null terminated string index
            name: NullTerminatedString,
            /// points to the import name
            token: Ast.TokenIndex,
        };
    };

    pub const LineColumn = struct {
        line: u32,
        column: u32,
    };

    pub const ArrayInit = struct {
        ty: Ref,
        init_count: u32,
    };

    pub const Src = struct {
        node: Ast.Node.Offset,
        line: u32,
        column: u32,
    };

    pub const DeferErrCode = struct {
        remapped_err_code: Index,
        index: u32,
        len: u32,
    };

    pub const ValidateDestructure = struct {
        /// The value being destructured.
        operand: Ref,
        /// The `destructure_assign` node.
        destructure_node: Ast.Node.Offset,
        /// The expected field count.
        expect_len: u32,
    };

    pub const ArrayMul = struct {
        /// The result type of the array multiplication operation, or `.none` if none was available.
        res_ty: Ref,
        /// The LHS of the array multiplication.
        lhs: Ref,
        /// The RHS of the array multiplication.
        rhs: Ref,
    };

    pub const RestoreErrRetIndex = struct {
        src_node: Ast.Node.Offset,
        /// If `.none`, restore the trace to its state upon function entry.
        block: Ref,
        /// If `.none`, restore unconditionally.
        operand: Ref,
    };

    pub const Import = struct {
        /// The result type of the import, or `.none` if none was available.
        res_ty: Ref,
        /// The import path.
        path: NullTerminatedString,
    };
};

/// `DeclContents` contains all "interesting" instructions found within a declaration by `findTrackable`.
/// These instructions are partitioned into a few different sets, since this makes ZIR instruction mapping
/// more effective.
pub const DeclContents = struct {
    /// This is a simple optional because ZIR guarantees that a `func`/`func_inferred`/`func_fancy` instruction
    /// can only occur once per `declaration`.
    func_decl: ?Inst.Index,
    explicit_types: std.ArrayList(Inst.Index),
    other: std.ArrayList(Inst.Index),

    pub const init: DeclContents = .{
        .func_decl = null,
        .explicit_types = .empty,
        .other = .empty,
    };

    pub fn clear(contents: *DeclContents) void {
        contents.func_decl = null;
        contents.explicit_types.clearRetainingCapacity();
        contents.other.clearRetainingCapacity();
    }

    pub fn deinit(contents: *DeclContents, gpa: Allocator) void {
        contents.explicit_types.deinit(gpa);
        contents.other.deinit(gpa);
    }
};

/// Find all tracked ZIR instructions, recursively, within a `declaration` instruction. Does not recurse through
/// nested declarations; to find all declarations, call this function recursively on the type declarations discovered
/// in `contents.explicit_types`.
///
/// This populates an `ArrayList` because an iterator would need to allocate memory anyway.
pub fn findTrackable(zir: Zir, gpa: Allocator, contents: *DeclContents, decl_inst: Zir.Inst.Index) !void {
    contents.clear();

    const decl = zir.getDeclaration(decl_inst);

    // `defer` instructions duplicate the same body arbitrarily many times, but we only want to traverse
    // their contents once per defer. So, we store the extra index of the body here to deduplicate.
    var found_defers: std.AutoHashMapUnmanaged(u32, void) = .empty;
    defer found_defers.deinit(gpa);

    if (decl.type_body) |b| try zir.findTrackableBody(gpa, contents, &found_defers, b);
    if (decl.align_body) |b| try zir.findTrackableBody(gpa, contents, &found_defers, b);
    if (decl.linksection_body) |b| try zir.findTrackableBody(gpa, contents, &found_defers, b);
    if (decl.addrspace_body) |b| try zir.findTrackableBody(gpa, contents, &found_defers, b);
    if (decl.value_body) |b| try zir.findTrackableBody(gpa, contents, &found_defers, b);
}

/// Like `findTrackable`, but only considers the `main_struct_inst` instruction. This may return more than
/// just that instruction because it will also traverse fields.
pub fn findTrackableRoot(zir: Zir, gpa: Allocator, contents: *DeclContents) !void {
    contents.clear();

    var found_defers: std.AutoHashMapUnmanaged(u32, void) = .empty;
    defer found_defers.deinit(gpa);

    try zir.findTrackableInner(gpa, contents, &found_defers, .main_struct_inst);
}

fn findTrackableInner(
    zir: Zir,
    gpa: Allocator,
    contents: *DeclContents,
    defers: *std.AutoHashMapUnmanaged(u32, void),
    inst: Inst.Index,
) Allocator.Error!void {
    comptime assert(Zir.inst_tracking_version == 0);

    const tags = zir.instructions.items(.tag);
    const datas = zir.instructions.items(.data);

    switch (tags[@intFromEnum(inst)]) {
        .declaration => unreachable,

        // Boring instruction tags first. These have no body and are not declarations or type declarations.
        .add,
        .addwrap,
        .add_sat,
        .add_unsafe,
        .sub,
        .subwrap,
        .sub_sat,
        .mul,
        .mulwrap,
        .mul_sat,
        .div_exact,
        .div_floor,
        .div_trunc,
        .mod,
        .rem,
        .mod_rem,
        .shl,
        .shl_exact,
        .shl_sat,
        .shr,
        .shr_exact,
        .param_anytype,
        .param_anytype_comptime,
        .array_cat,
        .array_mul,
        .array_type,
        .array_type_sentinel,
        .reify_int,
        .vector_type,
        .elem_type,
        .indexable_ptr_elem_type,
        .splat_op_result_ty,
        .indexable_ptr_len,
        .anyframe_type,
        .as_node,
        .as_shift_operand,
        .bit_and,
        .bitcast,
        .bit_not,
        .bit_or,
        .bool_not,
        .bool_br_and,
        .bool_br_or,
        .@"break",
        .break_inline,
        .switch_continue,
        .check_comptime_control_flow,
        .builtin_call,
        .cmp_lt,
        .cmp_lte,
        .cmp_eq,
        .cmp_gte,
        .cmp_gt,
        .cmp_neq,
        .error_set_decl,
        .dbg_stmt,
        .dbg_var_ptr,
        .dbg_var_val,
        .decl_ref,
        .decl_val,
        .load,
        .div,
        .elem_ptr_node,
        .elem_ptr,
        .elem_ptr_load,
        .elem_val,
        .elem_val_imm,
        .ensure_result_used,
        .ensure_result_non_error,
        .ensure_err_union_payload_void,
        .error_union_type,
        .error_value,
        .@"export",
        .field_ptr,
        .field_ptr_load,
        .field_ptr_named,
        .field_ptr_named_load,
        .import,
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
        .repeat,
        .repeat_inline,
        .for_len,
        .merge_error_sets,
        .ref,
        .ret_node,
        .ret_load,
        .ret_implicit,
        .ret_err_value,
        .ret_err_value_code,
        .ret_ptr,
        .ret_type,
        .ptr_type,
        .slice_start,
        .slice_end,
        .slice_sentinel,
        .slice_length,
        .slice_sentinel_ty,
        .store_node,
        .store_to_inferred_ptr,
        .str,
        .negate,
        .negate_wrap,
        .typeof,
        .typeof_log2_int_type,
        .@"unreachable",
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
        .enum_literal,
        .decl_literal,
        .decl_literal_no_coerce,
        .validate_deref,
        .validate_destructure,
        .field_type_ref,
        .opt_eu_base_ptr_init,
        .coerce_ptr_elem_ty,
        .validate_ref_ty,
        .validate_const,
        .struct_init_empty,
        .struct_init_empty_result,
        .struct_init_empty_ref_result,
        .validate_struct_init_ty,
        .validate_struct_init_result_ty,
        .validate_ptr_struct_init,
        .struct_init_field_type,
        .struct_init_field_ptr,
        .array_init_anon,
        .array_init,
        .array_init_ref,
        .validate_array_init_ty,
        .validate_array_init_result_ty,
        .validate_array_init_ref_ty,
        .validate_ptr_array_init,
        .array_init_elem_type,
        .array_init_elem_ptr,
        .union_init,
        .type_info,
        .size_of,
        .bit_size_of,
        .int_from_ptr,
        .compile_error,
        .set_eval_branch_quota,
        .int_from_enum,
        .align_of,
        .int_from_bool,
        .embed_file,
        .error_name,
        .panic,
        .trap,
        .set_runtime_safety,
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
        .enum_from_int,
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
        .bit_offset_of,
        .offset_of,
        .splat,
        .reduce,
        .shuffle,
        .atomic_load,
        .atomic_rmw,
        .atomic_store,
        .mul_add,
        .memcpy,
        .memset,
        .memmove,
        .min,
        .max,
        .alloc,
        .alloc_mut,
        .alloc_comptime_mut,
        .alloc_inferred,
        .alloc_inferred_mut,
        .alloc_inferred_comptime,
        .alloc_inferred_comptime_mut,
        .resolve_inferred_alloc,
        .make_ptr_const,
        .@"resume",
        .save_err_ret_index,
        .restore_err_ret_index_unconditional,
        .restore_err_ret_index_fn_entry,
        => return,

        // Struct initializations need tracking, as they may create anonymous struct types.
        .struct_init,
        .struct_init_ref,
        .struct_init_anon,
        => return contents.other.append(gpa, inst),

        .extended => {
            const extended = datas[@intFromEnum(inst)].extended;
            switch (extended.opcode) {
                .value_placeholder => unreachable,

                // Once again, we start with the boring tags.
                .this,
                .ret_addr,
                .builtin_src,
                .error_return_trace,
                .frame,
                .frame_address,
                .alloc,
                .builtin_extern,
                .@"asm",
                .asm_expr,
                .compile_log,
                .min_multi,
                .max_multi,
                .add_with_overflow,
                .sub_with_overflow,
                .mul_with_overflow,
                .shl_with_overflow,
                .round_op,
                .c_undef,
                .c_include,
                .c_define,
                .wasm_memory_size,
                .wasm_memory_grow,
                .prefetch,
                .set_float_mode,
                .error_cast,
                .breakpoint,
                .disable_instrumentation,
                .disable_intrinsics,
                .select,
                .int_from_error,
                .error_from_int,
                .reify_slice_arg_ty,
                .reify_enum_value_slice_ty,
                .reify_pointer_sentinel_ty,
                .reify_tuple,
                .reify_pointer,
                .reify_fn,
                .cmpxchg,
                .c_va_arg,
                .c_va_copy,
                .c_va_end,
                .c_va_start,
                .ptr_cast_full,
                .ptr_cast_no_dest,
                .work_item_id,
                .work_group_size,
                .work_group_id,
                .in_comptime,
                .restore_err_ret_index,
                .closure_get,
                .field_parent_ptr,
                .builtin_value,
                .branch_hint,
                .inplace_arith_result_ty,
                .tuple_decl,
                .dbg_empty_stmt,
                .astgen_error,
                .float_op_result_ty,
                .round_op_ty,
                => return,

                // `@TypeOf` has a body.
                .typeof_peer => {
                    const extra = zir.extraData(Zir.Inst.TypeOfPeer, extended.operand);
                    const body = zir.bodySlice(extra.data.body_index, extra.data.body_len);
                    try zir.findTrackableBody(gpa, contents, defers, body);
                },

                // Reifications and opaque declarations need tracking, but have no bodies.
                .reify_enum,
                .reify_struct,
                .reify_union,
                .opaque_decl,
                => return contents.other.append(gpa, inst),

                // Struct declarations need tracking and have bodies.
                .struct_decl => {
                    try contents.explicit_types.append(gpa, inst);

                    const struct_decl = zir.getStructDecl(inst);
                    var it = struct_decl.iterateFields();
                    while (it.next()) |field| {
                        try zir.findTrackableBody(gpa, contents, defers, field.type_body);
                        if (field.align_body) |b| try zir.findTrackableBody(gpa, contents, defers, b);
                        if (field.default_body) |b| try zir.findTrackableBody(gpa, contents, defers, b);
                    }
                },

                // Union declarations need tracking and have bodies.
                .union_decl => {
                    try contents.explicit_types.append(gpa, inst);

                    const union_decl = zir.getUnionDecl(inst);
                    var it = union_decl.iterateFields();
                    while (it.next()) |field| {
                        if (field.type_body) |b| try zir.findTrackableBody(gpa, contents, defers, b);
                        if (field.align_body) |b| try zir.findTrackableBody(gpa, contents, defers, b);
                        if (field.value_body) |b| try zir.findTrackableBody(gpa, contents, defers, b);
                    }
                },

                // Enum declarations need tracking and have bodies.
                .enum_decl => {
                    try contents.explicit_types.append(gpa, inst);

                    const enum_decl = zir.getEnumDecl(inst);
                    var it = enum_decl.iterateFields();
                    while (it.next()) |field| {
                        if (field.value_body) |b| try zir.findTrackableBody(gpa, contents, defers, b);
                    }
                },
            }
        },

        // Functions instructions are interesting and have a body.
        .func,
        .func_inferred,
        => {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.Func, inst_data.payload_index);

            if (extra.data.body_len == 0) {
                // This is just a prototype. No need to track.
                assert(extra.data.ret_ty.body_len < 2);
                return;
            }

            assert(contents.func_decl == null);
            contents.func_decl = inst;

            var extra_index: usize = extra.end;
            switch (extra.data.ret_ty.body_len) {
                0 => {},
                1 => extra_index += 1,
                else => {
                    const body = zir.bodySlice(extra_index, extra.data.ret_ty.body_len);
                    extra_index += body.len;
                    try zir.findTrackableBody(gpa, contents, defers, body);
                },
            }
            const body = zir.bodySlice(extra_index, extra.data.body_len);
            return zir.findTrackableBody(gpa, contents, defers, body);
        },
        .func_fancy => {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.FuncFancy, inst_data.payload_index);

            if (extra.data.body_len == 0) {
                // This is just a prototype. No need to track.
                assert(!extra.data.bits.has_cc_body);
                assert(!extra.data.bits.has_ret_ty_body);
                return;
            }

            assert(contents.func_decl == null);
            contents.func_decl = inst;

            var extra_index: usize = extra.end;

            if (extra.data.bits.has_cc_body) {
                const body_len = zir.extra[extra_index];
                extra_index += 1;
                const body = zir.bodySlice(extra_index, body_len);
                try zir.findTrackableBody(gpa, contents, defers, body);
                extra_index += body.len;
            } else if (extra.data.bits.has_cc_ref) {
                extra_index += 1;
            }

            if (extra.data.bits.has_ret_ty_body) {
                const body_len = zir.extra[extra_index];
                extra_index += 1;
                const body = zir.bodySlice(extra_index, body_len);
                try zir.findTrackableBody(gpa, contents, defers, body);
                extra_index += body.len;
            } else if (extra.data.bits.has_ret_ty_ref) {
                extra_index += 1;
            }

            extra_index += @intFromBool(extra.data.bits.has_any_noalias);

            const body = zir.bodySlice(extra_index, extra.data.body_len);
            return zir.findTrackableBody(gpa, contents, defers, body);
        },

        // Block instructions, recurse over the bodies.

        .block,
        .block_inline,
        .c_import,
        .typeof_builtin,
        .loop,
        => {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.Block, inst_data.payload_index);
            const body = zir.bodySlice(extra.end, extra.data.body_len);
            return zir.findTrackableBody(gpa, contents, defers, body);
        },
        .block_comptime => {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.BlockComptime, inst_data.payload_index);
            const body = zir.bodySlice(extra.end, extra.data.body_len);
            return zir.findTrackableBody(gpa, contents, defers, body);
        },
        .condbr, .condbr_inline => {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.CondBr, inst_data.payload_index);
            const then_body = zir.bodySlice(extra.end, extra.data.then_body_len);
            const else_body = zir.bodySlice(extra.end + then_body.len, extra.data.else_body_len);
            try zir.findTrackableBody(gpa, contents, defers, then_body);
            try zir.findTrackableBody(gpa, contents, defers, else_body);
        },
        .@"try", .try_ptr => {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.Try, inst_data.payload_index);
            const body = zir.bodySlice(extra.end, extra.data.body_len);
            try zir.findTrackableBody(gpa, contents, defers, body);
        },

        .switch_block,
        .switch_block_ref,
        .switch_block_err_union,
        => {
            const zir_switch = zir.getSwitchBlock(inst);
            if (zir_switch.non_err_case) |non_err_case| {
                try zir.findTrackableBody(gpa, contents, defers, non_err_case.body);
            }
            if (zir_switch.else_case) |else_case| {
                try zir.findTrackableBody(gpa, contents, defers, else_case.body);
            }
            var extra_index = zir_switch.end;
            var case_it = zir_switch.iterateCases();
            while (case_it.next()) |case| {
                const prong_body = zir.bodySlice(extra_index, case.prong_info.body_len);
                extra_index += prong_body.len;
                try zir.findTrackableBody(gpa, contents, defers, prong_body);
                for (case.item_infos) |item_info| {
                    if (item_info.bodyLen()) |body_len| {
                        const item_body = zir.bodySlice(extra_index, body_len);
                        extra_index += item_body.len;
                        try zir.findTrackableBody(gpa, contents, defers, item_body);
                    }
                }
                for (case.range_infos) |range_info| {
                    if (range_info[0].bodyLen()) |body_len| {
                        const first_body = zir.bodySlice(extra_index, body_len);
                        extra_index += first_body.len;
                        try zir.findTrackableBody(gpa, contents, defers, first_body);
                    }
                    if (range_info[1].bodyLen()) |body_len| {
                        const last_body = zir.bodySlice(extra_index, body_len);
                        extra_index += last_body.len;
                        try zir.findTrackableBody(gpa, contents, defers, last_body);
                    }
                }
            }
        },

        .suspend_block => @panic("TODO iterate suspend block"),

        .param, .param_comptime => {
            const inst_data = datas[@intFromEnum(inst)].pl_tok;
            const extra = zir.extraData(Inst.Param, inst_data.payload_index);
            const body = zir.bodySlice(extra.end, extra.data.type.body_len);
            try zir.findTrackableBody(gpa, contents, defers, body);
        },

        inline .call, .field_call => |tag| {
            const inst_data = datas[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(switch (tag) {
                .call => Inst.Call,
                .field_call => Inst.FieldCall,
                else => unreachable,
            }, inst_data.payload_index);
            // It's easiest to just combine all the arg bodies into one body, like we do above for `struct_decl`.
            const args_len = extra.data.flags.args_len;
            if (args_len > 0) {
                const first_arg_start_off = args_len;
                const final_arg_end_off = zir.extra[extra.end + args_len - 1];
                const args_body = zir.bodySlice(extra.end + first_arg_start_off, final_arg_end_off - first_arg_start_off);
                try zir.findTrackableBody(gpa, contents, defers, args_body);
            }
        },
        .@"defer" => {
            const inst_data = datas[@intFromEnum(inst)].@"defer";
            const gop = try defers.getOrPut(gpa, inst_data.index);
            if (!gop.found_existing) {
                const body = zir.bodySlice(inst_data.index, inst_data.len);
                try zir.findTrackableBody(gpa, contents, defers, body);
            }
        },
        .defer_err_code => {
            const inst_data = datas[@intFromEnum(inst)].defer_err_code;
            const extra = zir.extraData(Inst.DeferErrCode, inst_data.payload_index).data;
            const gop = try defers.getOrPut(gpa, extra.index);
            if (!gop.found_existing) {
                const body = zir.bodySlice(extra.index, extra.len);
                try zir.findTrackableBody(gpa, contents, defers, body);
            }
        },
    }
}

fn findTrackableBody(
    zir: Zir,
    gpa: Allocator,
    contents: *DeclContents,
    defers: *std.AutoHashMapUnmanaged(u32, void),
    body: []const Inst.Index,
) Allocator.Error!void {
    for (body) |member| {
        try zir.findTrackableInner(gpa, contents, defers, member);
    }
}

pub const FnInfo = struct {
    param_body: []const Inst.Index,
    param_body_inst: Inst.Index,
    ret_ty_body: []const Inst.Index,
    body: []const Inst.Index,
    ret_ty_ref: Zir.Inst.Ref,
    ret_ty_is_generic: bool,
    total_params_len: u32,
    inferred_error_set: bool,
};

pub fn getParamBody(zir: Zir, fn_inst: Inst.Index) []const Zir.Inst.Index {
    const tags = zir.instructions.items(.tag);
    const datas = zir.instructions.items(.data);
    const inst_data = datas[@intFromEnum(fn_inst)].pl_node;

    const param_block_index = switch (tags[@intFromEnum(fn_inst)]) {
        .func, .func_inferred => blk: {
            const extra = zir.extraData(Inst.Func, inst_data.payload_index);
            break :blk extra.data.param_block;
        },
        .func_fancy => blk: {
            const extra = zir.extraData(Inst.FuncFancy, inst_data.payload_index);
            break :blk extra.data.param_block;
        },
        else => unreachable,
    };

    switch (tags[@intFromEnum(param_block_index)]) {
        .block, .block_comptime, .block_inline => {
            const param_block = zir.extraData(Inst.Block, datas[@intFromEnum(param_block_index)].pl_node.payload_index);
            return zir.bodySlice(param_block.end, param_block.data.body_len);
        },
        .declaration => {
            return zir.getDeclaration(param_block_index).value_body.?;
        },
        else => unreachable,
    }
}

pub fn getParamName(zir: Zir, param_inst: Inst.Index) ?NullTerminatedString {
    const inst = zir.instructions.get(@intFromEnum(param_inst));
    return switch (inst.tag) {
        .param, .param_comptime => zir.extraData(Inst.Param, inst.data.pl_tok.payload_index).data.name,
        .param_anytype, .param_anytype_comptime => inst.data.str_tok.start,
        else => null,
    };
}

pub fn getFnInfo(zir: Zir, fn_inst: Inst.Index) FnInfo {
    const tags = zir.instructions.items(.tag);
    const datas = zir.instructions.items(.data);
    const info: struct {
        param_block: Inst.Index,
        body: []const Inst.Index,
        ret_ty_ref: Inst.Ref,
        ret_ty_body: []const Inst.Index,
        ret_ty_is_generic: bool,
        ies: bool,
    } = switch (tags[@intFromEnum(fn_inst)]) {
        .func, .func_inferred => |tag| blk: {
            const inst_data = datas[@intFromEnum(fn_inst)].pl_node;
            const extra = zir.extraData(Inst.Func, inst_data.payload_index);

            var extra_index: usize = extra.end;
            var ret_ty_ref: Inst.Ref = .none;
            var ret_ty_body: []const Inst.Index = &.{};

            switch (extra.data.ret_ty.body_len) {
                0 => {
                    ret_ty_ref = .void_type;
                },
                1 => {
                    ret_ty_ref = @enumFromInt(zir.extra[extra_index]);
                    extra_index += 1;
                },
                else => {
                    ret_ty_body = zir.bodySlice(extra_index, extra.data.ret_ty.body_len);
                    extra_index += ret_ty_body.len;
                },
            }

            const body = zir.bodySlice(extra_index, extra.data.body_len);
            extra_index += body.len;

            break :blk .{
                .param_block = extra.data.param_block,
                .ret_ty_ref = ret_ty_ref,
                .ret_ty_body = ret_ty_body,
                .body = body,
                .ret_ty_is_generic = extra.data.ret_ty.is_generic,
                .ies = tag == .func_inferred,
            };
        },
        .func_fancy => blk: {
            const inst_data = datas[@intFromEnum(fn_inst)].pl_node;
            const extra = zir.extraData(Inst.FuncFancy, inst_data.payload_index);

            var extra_index: usize = extra.end;
            var ret_ty_ref: Inst.Ref = .none;
            var ret_ty_body: []const Inst.Index = &.{};

            if (extra.data.bits.has_cc_body) {
                extra_index += zir.extra[extra_index] + 1;
            } else if (extra.data.bits.has_cc_ref) {
                extra_index += 1;
            }
            if (extra.data.bits.has_ret_ty_body) {
                const body_len = zir.extra[extra_index];
                extra_index += 1;
                ret_ty_body = zir.bodySlice(extra_index, body_len);
                extra_index += ret_ty_body.len;
            } else if (extra.data.bits.has_ret_ty_ref) {
                ret_ty_ref = @enumFromInt(zir.extra[extra_index]);
                extra_index += 1;
            } else {
                ret_ty_ref = .void_type;
            }

            extra_index += @intFromBool(extra.data.bits.has_any_noalias);

            const body = zir.bodySlice(extra_index, extra.data.body_len);
            extra_index += body.len;
            break :blk .{
                .param_block = extra.data.param_block,
                .ret_ty_ref = ret_ty_ref,
                .ret_ty_body = ret_ty_body,
                .body = body,
                .ret_ty_is_generic = extra.data.bits.ret_ty_is_generic,
                .ies = extra.data.bits.is_inferred_error,
            };
        },
        else => unreachable,
    };
    const param_body = zir.getParamBody(fn_inst);
    var total_params_len: u32 = 0;
    for (param_body) |inst| {
        switch (tags[@intFromEnum(inst)]) {
            .param, .param_comptime, .param_anytype, .param_anytype_comptime => {
                total_params_len += 1;
            },
            else => continue,
        }
    }
    return .{
        .param_body = param_body,
        .param_body_inst = info.param_block,
        .ret_ty_body = info.ret_ty_body,
        .ret_ty_ref = info.ret_ty_ref,
        .body = info.body,
        .total_params_len = total_params_len,
        .ret_ty_is_generic = info.ret_ty_is_generic,
        .inferred_error_set = info.ies,
    };
}

pub fn getDeclaration(zir: Zir, inst: Zir.Inst.Index) Inst.Declaration.Unwrapped {
    assert(zir.instructions.items(.tag)[@intFromEnum(inst)] == .declaration);
    const pl_node = zir.instructions.items(.data)[@intFromEnum(inst)].declaration;
    const extra = zir.extraData(Inst.Declaration, pl_node.payload_index);

    const flags_vals: [2]u32 = .{ extra.data.flags_0, extra.data.flags_1 };
    const flags: Inst.Declaration.Flags = @bitCast(flags_vals);

    var extra_index = extra.end;

    const name: NullTerminatedString = if (flags.id.hasName()) name: {
        const name = zir.extra[extra_index];
        extra_index += 1;
        break :name @enumFromInt(name);
    } else .empty;

    const lib_name: NullTerminatedString = if (flags.id.hasLibName()) lib_name: {
        const lib_name = zir.extra[extra_index];
        extra_index += 1;
        break :lib_name @enumFromInt(lib_name);
    } else .empty;

    const type_body_len: u32 = if (flags.id.hasTypeBody()) len: {
        const len = zir.extra[extra_index];
        extra_index += 1;
        break :len len;
    } else 0;
    const align_body_len: u32, const linksection_body_len: u32, const addrspace_body_len: u32 = lens: {
        if (!flags.id.hasSpecialBodies()) break :lens .{ 0, 0, 0 };
        const lens = zir.extra[extra_index..][0..3].*;
        extra_index += 3;
        break :lens lens;
    };
    const value_body_len: u32 = if (flags.id.hasValueBody()) len: {
        const len = zir.extra[extra_index];
        extra_index += 1;
        break :len len;
    } else 0;

    const type_body = zir.bodySlice(extra_index, type_body_len);
    extra_index += type_body_len;
    const align_body = zir.bodySlice(extra_index, align_body_len);
    extra_index += align_body_len;
    const linksection_body = zir.bodySlice(extra_index, linksection_body_len);
    extra_index += linksection_body_len;
    const addrspace_body = zir.bodySlice(extra_index, addrspace_body_len);
    extra_index += addrspace_body_len;
    const value_body = zir.bodySlice(extra_index, value_body_len);
    extra_index += value_body_len;

    return .{
        .src_node = pl_node.src_node,

        .src_line = flags.src_line,
        .src_column = flags.src_column,

        .kind = flags.id.kind(),
        .name = name,
        .is_pub = flags.id.isPub(),
        .is_threadlocal = flags.id.isThreadlocal(),
        .linkage = flags.id.linkage(),
        .lib_name = lib_name,

        .type_body = if (type_body_len == 0) null else type_body,
        .align_body = if (align_body_len == 0) null else align_body,
        .linksection_body = if (linksection_body_len == 0) null else linksection_body,
        .addrspace_body = if (addrspace_body_len == 0) null else addrspace_body,
        .value_body = if (value_body_len == 0) null else value_body,
    };
}

pub fn getAssociatedSrcHash(zir: Zir, inst: Zir.Inst.Index) ?std.zig.SrcHash {
    const tag = zir.instructions.items(.tag);
    const data = zir.instructions.items(.data);
    switch (tag[@intFromEnum(inst)]) {
        .declaration => {
            const declaration = data[@intFromEnum(inst)].declaration;
            const extra = zir.extraData(Inst.Declaration, declaration.payload_index);
            return @bitCast([4]u32{
                extra.data.src_hash_0,
                extra.data.src_hash_1,
                extra.data.src_hash_2,
                extra.data.src_hash_3,
            });
        },
        .func, .func_inferred => {
            const pl_node = data[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.Func, pl_node.payload_index);
            if (extra.data.body_len == 0) {
                // Function type or extern fn - no associated hash
                return null;
            }
            const extra_index = extra.end +
                extra.data.ret_ty.body_len +
                extra.data.body_len +
                @typeInfo(Inst.Func.SrcLocs).@"struct".fields.len;
            return @bitCast([4]u32{
                zir.extra[extra_index + 0],
                zir.extra[extra_index + 1],
                zir.extra[extra_index + 2],
                zir.extra[extra_index + 3],
            });
        },
        .func_fancy => {
            const pl_node = data[@intFromEnum(inst)].pl_node;
            const extra = zir.extraData(Inst.FuncFancy, pl_node.payload_index);
            if (extra.data.body_len == 0) {
                // Function type or extern fn - no associated hash
                return null;
            }
            const bits = extra.data.bits;
            var extra_index = extra.end;
            if (bits.has_cc_body) {
                const body_len = zir.extra[extra_index];
                extra_index += 1 + body_len;
            } else extra_index += @intFromBool(bits.has_cc_ref);
            if (bits.has_ret_ty_body) {
                const body_len = zir.extra[extra_index];
                extra_index += 1 + body_len;
            } else extra_index += @intFromBool(bits.has_ret_ty_ref);
            extra_index += @intFromBool(bits.has_any_noalias);
            extra_index += extra.data.body_len;
            extra_index += @typeInfo(Zir.Inst.Func.SrcLocs).@"struct".fields.len;
            return @bitCast([4]u32{
                zir.extra[extra_index + 0],
                zir.extra[extra_index + 1],
                zir.extra[extra_index + 2],
                zir.extra[extra_index + 3],
            });
        },
        .extended => {},
        else => return null,
    }
    const extended = data[@intFromEnum(inst)].extended;
    switch (extended.opcode) {
        .struct_decl => {
            const extra = zir.extraData(Inst.StructDecl, extended.operand).data;
            return @bitCast([4]u32{
                extra.fields_hash_0,
                extra.fields_hash_1,
                extra.fields_hash_2,
                extra.fields_hash_3,
            });
        },
        .union_decl => {
            const extra = zir.extraData(Inst.UnionDecl, extended.operand).data;
            return @bitCast([4]u32{
                extra.fields_hash_0,
                extra.fields_hash_1,
                extra.fields_hash_2,
                extra.fields_hash_3,
            });
        },
        .enum_decl => {
            const extra = zir.extraData(Inst.EnumDecl, extended.operand).data;
            return @bitCast([4]u32{
                extra.fields_hash_0,
                extra.fields_hash_1,
                extra.fields_hash_2,
                extra.fields_hash_3,
            });
        },
        else => return null,
    }
}

pub fn getSwitchBlock(zir: *const Zir, switch_inst: Inst.Index) UnwrappedSwitchBlock {
    const has_non_err = switch (zir.instructions.items(.tag)[@intFromEnum(switch_inst)]) {
        .switch_block, .switch_block_ref => false,
        .switch_block_err_union => true,
        else => unreachable,
    };
    const inst_data = zir.instructions.items(.data)[@intFromEnum(switch_inst)].pl_node;
    const extra = zir.extraData(Inst.SwitchBlock, inst_data.payload_index);
    const bits = extra.data.bits;
    var extra_index = extra.end;
    const multi_cases_len = if (bits.has_multi_cases) len: {
        const multi_cases_len = zir.extra[extra_index];
        extra_index += 1;
        break :len multi_cases_len;
    } else 0;
    const payload_capture_placeholder: Inst.OptionalIndex = if (bits.payload_capture_inst_is_placeholder) inst: {
        const inst: Inst.Index = @enumFromInt(zir.extra[extra_index]);
        extra_index += 1;
        break :inst inst.toOptional();
    } else .none;
    const tag_capture_placeholder: Inst.OptionalIndex = if (bits.tag_capture_inst_is_placeholder) inst: {
        const inst: Inst.Index = @enumFromInt(zir.extra[extra_index]);
        extra_index += 1;
        break :inst inst.toOptional();
    } else .none;
    const catch_or_if_src_node_offset: Ast.Node.OptionalOffset = if (has_non_err) node_offset: {
        const node_offset: Ast.Node.Offset = @enumFromInt(@as(i32, @bitCast(zir.extra[extra_index])));
        extra_index += 1;
        break :node_offset node_offset.toOptional();
    } else .none;
    const non_err_info: Inst.SwitchBlock.ProngInfo.NonErr = if (has_non_err) non_err_info: {
        const non_err_info: Inst.SwitchBlock.ProngInfo.NonErr = @bitCast(zir.extra[extra_index]);
        extra_index += 1;
        break :non_err_info non_err_info;
    } else undefined;
    const else_info: Inst.SwitchBlock.ProngInfo.Else = if (bits.has_else) else_info: {
        const else_info: Inst.SwitchBlock.ProngInfo.Else = @bitCast(zir.extra[extra_index]);
        extra_index += 1;
        break :else_info else_info;
    } else undefined;
    const scalar_cases_len: u32 = bits.scalar_cases_len;
    const prong_infos: []const Inst.SwitchBlock.ProngInfo =
        @ptrCast(zir.extra[extra_index..][0 .. scalar_cases_len + multi_cases_len]);
    extra_index += prong_infos.len;
    const multi_case_items_lens = zir.extra[extra_index..][0..multi_cases_len];
    extra_index += multi_case_items_lens.len;
    const multi_case_ranges_lens: ?[]const u32 = if (bits.any_ranges) lens: {
        const multi_case_ranges_lens = zir.extra[extra_index..][0..multi_cases_len];
        extra_index += multi_case_ranges_lens.len;
        break :lens multi_case_ranges_lens;
    } else null;
    var total_items_len: usize = scalar_cases_len;
    for (multi_case_items_lens) |items_len| {
        total_items_len += items_len;
    }
    if (multi_case_ranges_lens) |ranges_lens| for (ranges_lens) |ranges_len| {
        total_items_len += 2 * ranges_len;
    };
    const item_infos: []const Inst.SwitchBlock.ItemInfo =
        @ptrCast(zir.extra[extra_index..][0..total_items_len]);
    extra_index += item_infos.len;
    const non_err_case: ?UnwrappedSwitchBlock.Case.NonErr = if (has_non_err) non_err_case: {
        const body = zir.bodySlice(extra_index, non_err_info.body_len);
        extra_index += body.len;
        break :non_err_case .{
            .body = body,
            .capture = non_err_info.capture,
            .operand_is_ref = non_err_info.operand_is_ref,
        };
    } else null;
    const else_case: ?UnwrappedSwitchBlock.Case.Else = if (bits.has_else) else_case: {
        const body = zir.bodySlice(extra_index, else_info.body_len);
        extra_index += body.len;
        break :else_case .{
            .index = .@"else",
            .body = body,
            .capture = else_info.capture,
            .is_inline = else_info.is_inline,
            .has_tag_capture = else_info.has_tag_capture,
            .is_simple_noreturn = else_info.is_simple_noreturn,
        };
    } else null;
    return .{
        .main_operand = extra.data.raw_operand,
        .switch_src_node_offset = inst_data.src_node,
        .catch_or_if_src_node_offset = catch_or_if_src_node_offset,
        .payload_capture_placeholder = payload_capture_placeholder,
        .tag_capture_placeholder = tag_capture_placeholder,
        .has_continue = bits.has_continue,
        .any_maybe_runtime_capture = bits.any_maybe_runtime_capture,
        .non_err_case = non_err_case,
        .else_case = else_case,
        .has_under = bits.has_under,
        .prong_infos = prong_infos,
        .multi_case_items_lens = multi_case_items_lens,
        .multi_case_ranges_lens = multi_case_ranges_lens,
        .item_infos = item_infos,
        .end = extra_index,
    };
}

/// Trailing (starting at `end`):
/// 0. case_bodies: { // for each case in Case.
```
