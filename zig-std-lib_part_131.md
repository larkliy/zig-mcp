```
S\\VS7")) catch return error.PathNotFound;
            defer vs7_key.close();
            try_vs7_key: {
                const path_maybe_with_trailing_slash = vs7_key.getString(gpa, .{ .name = L("14.0") }, .wtf8) catch |err| switch (err) {
                    error.OutOfMemory => return error.OutOfMemory,
                    else => break :try_vs7_key,
                };

                if (path_maybe_with_trailing_slash.len > Dir.max_path_bytes or !Dir.path.isAbsolute(path_maybe_with_trailing_slash)) {
                    gpa.free(path_maybe_with_trailing_slash);
                    break :try_vs7_key;
                }

                var path = std.array_list.Managed(u8).fromOwnedSlice(gpa, path_maybe_with_trailing_slash);
                errdefer path.deinit();

                // String might contain trailing slash, so trim it here
                if (path.items.len > "C:\\".len and path.getLast() == '\\') _ = path.pop();
                break :base_path path;
            }
            return error.PathNotFound;
        };
        errdefer base_path.deinit();

        try base_path.appendSlice("\\VC\\lib\\");
        try base_path.appendSlice(switch (arch) {
            .thumb => "arm",
            .aarch64 => "arm64",
            .x86 => "", //x86 is in the root of the Lib folder
            .x86_64 => "amd64",
            else => unreachable,
        });

        if (!verifyLibDir(io, base_path.items)) {
            return error.PathNotFound;
        }

        const full_path = try base_path.toOwnedSlice();
        return full_path;
    }

    fn verifyLibDir(io: Io, lib_dir_path: []const u8) bool {
        std.debug.assert(Dir.path.isAbsolute(lib_dir_path)); // should be already handled in `findVia*`

        var dir = Dir.openDirAbsolute(io, lib_dir_path, .{}) catch return false;
        defer dir.close(io);

        const stat = dir.statFile(io, "vcruntime.lib", .{}) catch return false;
        if (stat.kind != .file)
            return false;

        return true;
    }

    /// Find path to MSVC's `lib/` directory.
    /// Caller owns the result.
    pub fn find(
        gpa: Allocator,
        io: Io,
        registry: *Registry,
        arch: std.Target.Cpu.Arch,
        environ_map: *const Environ.Map,
    ) error{ OutOfMemory, MsvcLibDirNotFound }![]const u8 {
        const full_path = MsvcLibDir.findViaCOM(gpa, io, registry, arch, environ_map) catch |err1| switch (err1) {
            error.OutOfMemory => return error.OutOfMemory,
            error.PathNotFound => MsvcLibDir.findViaRegistry(gpa, io, arch, environ_map) catch |err2| switch (err2) {
                error.OutOfMemory => return error.OutOfMemory,
                error.PathNotFound => MsvcLibDir.findViaVs7Key(gpa, io, registry, arch, environ_map) catch |err3| switch (err3) {
                    error.OutOfMemory => return error.OutOfMemory,
                    error.PathNotFound => return error.MsvcLibDirNotFound,
                },
            },
        };
        errdefer gpa.free(full_path);

        return full_path;
    }
};



---
File: /std/zig/Zir.zig
---

//! Zig Intermediate Representation.
//!
//! Astgen.zig converts AST nodes to these untyped IR instructions. Next,
//! Sema.zig processes these into AIR.
//! The minimum amount of information needed to represent a list of ZIR instructions.
//! Once this structure is completed, it can be used to generate AIR, followed by
//! machine code, without any memory access into the AST tree token list, node list,
//! or source bytes. Exceptions include:
//!  * Compile errors, which may need to reach into these data structures to
//!    create a useful report.
//!  * In the future, possibly inline assembly, which needs to get parsed and
//!    handled by the codegen backend, and errors reported there. However for now,
//!    inline assembly is not an exception.
const Zir = @This();
const builtin = @import("builtin");

const std = @import("std");
const Io = std.Io;
const mem = std.mem;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const BigIntConst = std.math.big.int.Const;
const BigIntMutable = std.math.big.int.Mutable;
const Ast = std.zig.Ast;

instructions: std.MultiArrayList(Inst).Slice,
/// In order to store references to strings in fewer bytes, we copy all
/// string bytes into here. String bytes can be null. It is up to whomever
/// is referencing the data here whether they want to store both index and length,
/// thus allowing null bytes, or store only index, and use null-termination. The
/// `string_bytes` array is agnostic to either usage.
/// Index 0 is reserved for special cases.
string_bytes: []u8,
/// The meaning of this data is determined by `Inst.Tag` value.
/// The first few indexes are reserved. See `ExtraIndex` for the values.
extra: []u32,

/// The data stored at byte offset 0 when ZIR is stored in a file.
pub const Header = extern struct {
    instructions_len: u32,
    string_bytes_len: u32,
    extra_len: u32,
    /// We could leave this as padding, however it triggers a Valgrind warning because
    /// we read and write undefined bytes to the file system. This is harmless, but
    /// it's essentially free to have a zero field here and makes the warning go away,
    /// making it more likely that following Valgrind warnings will be taken seriously.
    unused: u32 = 0,
    stat_inode: Io.File.INode,
    stat_size: u64,
    stat_mtime: i128,
};

pub const ExtraIndex = enum(u32) {
    /// If this is 0, no compile errors. Otherwise there is a `CompileErrors`
    /// payload at this index.
    compile_errors,
    /// If this is 0, this file contains no imports. Otherwise there is a `Imports`
    /// payload at this index.
    imports,

    _,
};

fn ExtraData(comptime T: type) type {
    return struct { data: T, end: usize };
}

/// Returns the requested data, as well as the new index which is at the start of the
/// trailers for the object.
pub fn extraData(code: Zir, comptime T: type, index: usize) ExtraData(T) {
    const fields = @typeInfo(T).@"struct".fields;
    var i: usize = index;
    var result: T = undefined;
    inline for (fields) |field| {
        @field(result, field.name) = switch (field.type) {
            u32 => code.extra[i],

            Inst.Ref,
            Inst.Index,
            Inst.Declaration.Name,
            std.zig.SimpleComptimeReason,
            NullTerminatedString,
            // Ast.TokenIndex is missing because it is a u32.
            Ast.OptionalTokenIndex,
            Ast.Node.Index,
            Ast.Node.OptionalIndex,
            => @enumFromInt(code.extra[i]),

            Ast.TokenOffset,
            Ast.OptionalTokenOffset,
            Ast.Node.Offset,
            Ast.Node.OptionalOffset,
            => @enumFromInt(@as(i32, @bitCast(code.extra[i]))),

            Inst.Call.Flags,
            Inst.BuiltinCall.Flags,
            Inst.SwitchBlock.Bits,
            Inst.FuncFancy.Bits,
            Inst.Declaration.Flags,
            Inst.Param.Type,
            Inst.Func.RetTy,
            => @bitCast(code.extra[i]),

            else => @compileError("bad field type"),
        };
        i += 1;
    }
    return .{
        .data = result,
        .end = i,
    };
}

pub const NullTerminatedString = enum(u32) {
    empty = 0,
    _,
};

/// Given an index into `string_bytes` returns the null-terminated string found there.
pub fn nullTerminatedString(code: Zir, index: NullTerminatedString) [:0]const u8 {
    const slice = code.string_bytes[@intFromEnum(index)..];
    return slice[0..std.mem.findScalar(u8, slice, 0).? :0];
}

pub fn refSlice(code: Zir, start: usize, len: usize) []Inst.Ref {
    return @ptrCast(code.extra[start..][0..len]);
}

pub fn bodySlice(zir: Zir, start: usize, len: usize) []Inst.Index {
    return @ptrCast(zir.extra[start..][0..len]);
}

pub fn hasCompileErrors(code: Zir) bool {
    if (code.extra[@intFromEnum(ExtraIndex.compile_errors)] != 0) {
        return true;
    } else {
        assert(code.instructions.len != 0); // i.e. lowering did not fail
        return false;
    }
}

pub fn loweringFailed(code: Zir) bool {
    if (code.instructions.len == 0) {
        assert(code.hasCompileErrors());
        return true;
    } else {
        return false;
    }
}

pub fn deinit(code: *Zir, gpa: Allocator) void {
    code.instructions.deinit(gpa);
    gpa.free(code.string_bytes);
    gpa.free(code.extra);
    code.* = undefined;
}

/// These are untyped instructions generated from an Abstract Syntax Tree.
/// The data here is immutable because it is possible to have multiple
/// analyses on the same ZIR happening at the same time.
pub const Inst = struct {
    tag: Tag,
    data: Data,

    /// These names are used directly as the instruction names in the text format.
    /// See `data_field_map` for a list of which `Data` fields are used by each `Tag`.
    pub const Tag = enum(u8) {
        /// Arithmetic addition, asserts no integer overflow.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        add,
        /// Twos complement wrapping integer addition.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        addwrap,
        /// Saturating addition.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        add_sat,
        /// The same as `add` except no safety check.
        add_unsafe,
        /// Arithmetic subtraction. Asserts no integer overflow.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        sub,
        /// Twos complement wrapping integer subtraction.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        subwrap,
        /// Saturating subtraction.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        sub_sat,
        /// Arithmetic multiplication. Asserts no integer overflow.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        mul,
        /// Twos complement wrapping integer multiplication.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        mulwrap,
        /// Saturating multiplication.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        mul_sat,
        /// Implements the `@divExact` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        div_exact,
        /// Implements the `@divFloor` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        div_floor,
        /// Implements the `@divTrunc` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        div_trunc,
        /// Implements the `@mod` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        mod,
        /// Implements the `@rem` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        rem,
        /// Ambiguously remainder division or modulus. If the computation would possibly have
        /// a different value depending on whether the operation is remainder division or modulus,
        /// a compile error is emitted. Otherwise the computation is performed.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        mod_rem,
        /// Integer shift-left. Zeroes are shifted in from the right hand side.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        shl,
        /// Implements the `@shlExact` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        shl_exact,
        /// Saturating shift-left.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        shl_sat,
        /// Integer shift-right. Arithmetic or logical depending on the signedness of
        /// the integer type.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        shr,
        /// Implements the `@shrExact` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        shr_exact,

        /// Declares a parameter of the current function. Used for:
        /// * debug info
        /// * checking shadowing against declarations in the current namespace
        /// * parameter type expressions referencing other parameters
        /// These occur in the block outside a function body (the same block as
        /// contains the func instruction).
        /// Uses the `pl_tok` field. Token is the parameter name, payload is a `Param`.
        param,
        /// Same as `param` except the parameter is marked comptime.
        param_comptime,
        /// Same as `param` except the parameter is marked anytype.
        /// Uses the `str_tok` field. Token is the parameter name. String is the parameter name.
        param_anytype,
        /// Same as `param` except the parameter is marked both comptime and anytype.
        /// Uses the `str_tok` field. Token is the parameter name. String is the parameter name.
        param_anytype_comptime,
        /// Array concatenation. `a ++ b`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        array_cat,
        /// Array multiplication `a ** b`
        /// Uses the `pl_node` union field. Payload is `ArrayMul`.
        array_mul,
        /// `[N]T` syntax. No source location provided.
        /// Uses the `pl_node` union field. Payload is `Bin`. lhs is length, rhs is element type.
        array_type,
        /// `[N:S]T` syntax. Source location is the array type expression node.
        /// Uses the `pl_node` union field. Payload is `ArrayTypeSentinel`.
        array_type_sentinel,
        /// `@Int` builtin.
        /// Uses the `pl_node` union field with `Bin` payload.
        /// lhs is signedness, rhs is bit count.
        reify_int,
        /// `@Vector` builtin.
        /// Uses the `pl_node` union field with `Bin` payload.
        /// lhs is length, rhs is element type.
        vector_type,
        /// Given a pointer type, returns its element type. Reaches through any optional or error
        /// union types wrapping the pointer. Asserts that the underlying type is a pointer type.
        /// Returns generic poison if the element type is `anyopaque`.
        /// Uses the `un_node` field.
        elem_type,
        /// Given an indexable pointer (slice, many-ptr, single-ptr-to-array), returns its
        /// element type. Emits a compile error if the type is not an indexable pointer.
        /// Uses the `un_node` field.
        indexable_ptr_elem_type,
        /// Given a vector or array type, strips off any error unions or
        /// optionals layered on top and returns its element type.
        ///
        /// `!?[N]T` -> `T`
        ///
        /// Uses the `un_node` field.
        splat_op_result_ty,
        /// Given a pointer to an indexable object, returns the len property. This is
        /// used by for loops. This instruction also emits a for-loop specific compile
        /// error if the indexable object is not indexable.
        /// Uses the `un_node` field. The AST node is the for loop node.
        indexable_ptr_len,
        /// Create a `anyframe->T` type.
        /// Uses the `un_node` field.
        anyframe_type,
        /// Type coercion to the function's return type.
        /// Uses the `pl_node` field. Payload is `As`. AST node could be many things.
        as_node,
        /// Same as `as_node` but ignores runtime to comptime int error.
        as_shift_operand,
        /// Bitwise AND. `&`
        bit_and,
        /// Reinterpret the memory representation of a value as a different type.
        /// Uses the pl_node field with payload `Bin`.
        bitcast,
        /// Bitwise NOT. `~`
        /// Uses `un_node`.
        bit_not,
        /// Bitwise OR. `|`
        bit_or,
        /// A labeled block of code, which can return a value.
        /// Uses the `pl_node` union field. Payload is `Block`.
        block,
        /// Like `block`, but forces full evaluation of its contents at compile-time.
        /// Exited with `break_inline`.
        /// Uses the `pl_node` union field. Payload is `BlockComptime`.
        block_comptime,
        /// A list of instructions which are analyzed in the parent context, without
        /// generating a runtime block. Must terminate with an "inline" variant of
        /// a noreturn instruction.
        /// Uses the `pl_node` union field. Payload is `Block`.
        block_inline,
        /// This instruction may only ever appear in the list of declarations for a
        /// namespace type, e.g. within a `struct_decl` instruction. It represents a
        /// single source declaration (`const`/`var`/`fn`), containing the name,
        /// attributes, type, and value of the declaration.
        /// Uses the `declaration` union field. Payload is `Declaration`.
        declaration,
        /// Implements `suspend {...}`.
        /// Uses the `pl_node` union field. Payload is `Block`.
        suspend_block,
        /// Boolean NOT. See also `bit_not`.
        /// Uses the `un_node` field.
        bool_not,
        /// Short-circuiting boolean `and`. `lhs` is a boolean `Ref` and the other operand
        /// is a block, which is evaluated if `lhs` is `true`.
        /// Uses the `pl_node` union field. Payload is `BoolBr`.
        bool_br_and,
        /// Short-circuiting boolean `or`. `lhs` is a boolean `Ref` and the other operand
        /// is a block, which is evaluated if `lhs` is `false`.
        /// Uses the `pl_node` union field. Payload is `BoolBr`.
        bool_br_or,
        /// Return a value from a block.
        /// Uses the `break` union field.
        /// Uses the source information from previous instruction.
        @"break",
        /// Return a value from a block. This instruction is used as the terminator
        /// of a `block_inline`. It allows using the return value from `Sema.analyzeBody`.
        /// This instruction may also be used when it is known that there is only one
        /// break instruction in a block, and the target block is the parent.
        /// Uses the `break` union field.
        break_inline,
        /// Branch from within a switch case to the case specified by the operand.
        /// Uses the `break` union field. `block_inst` refers to a `switch_block`/
        /// `switch_block_ref`/`switch_block_err_union`.
        switch_continue,
        /// Checks that comptime control flow does not happen inside a runtime block.
        /// Uses the `un_node` union field.
        check_comptime_control_flow,
        /// Function call.
        /// Uses the `pl_node` union field with payload `Call`.
        /// AST node is the function call.
        call,
        /// Function call using `a.b()` syntax.
        /// Uses the named field as the callee. If there is no such field, searches in the type for
        /// a decl matching the field name. The decl is resolved and we ensure that it's a function
        /// which can accept the object as the first parameter, with one pointer fixup. This
        /// function is then used as the callee, with the object as an implicit first parameter.
        /// Uses the `pl_node` union field with payload `FieldCall`.
        /// AST node is the function call.
        field_call,
        /// Implements the `@call` builtin.
        /// Uses the `pl_node` union field with payload `BuiltinCall`.
        /// AST node is the builtin call.
        builtin_call,
        /// `<`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        cmp_lt,
        /// `<=`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        cmp_lte,
        /// `==`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        cmp_eq,
        /// `>=`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        cmp_gte,
        /// `>`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        cmp_gt,
        /// `!=`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        cmp_neq,
        /// Conditional branch. Splits control flow based on a boolean condition value.
        /// Uses the `pl_node` union field. AST node is an if, while, for, etc.
        /// Payload is `CondBr`.
        condbr,
        /// Same as `condbr`, except the condition is coerced to a comptime value, and
        /// only the taken branch is analyzed. The then block and else block must
        /// terminate with an "inline" variant of a noreturn instruction.
        condbr_inline,
        /// Given an operand which is an error union, splits control flow. In
        /// case of error, control flow goes into the block that is part of this
        /// instruction, which is guaranteed to end with a return instruction
        /// and never breaks out of the block.
        /// In the case of non-error, control flow proceeds to the next instruction
        /// after the `try`, with the result of this instruction being the unwrapped
        /// payload value, as if `err_union_payload_unsafe` was executed on the operand.
        /// Uses the `pl_node` union field. Payload is `Try`.
        @"try",
        /// Same as `try` except the operand is a pointer and the result is a pointer.
        try_ptr,
        /// An error set type definition. Contains a list of field names.
        /// Uses the `pl_node` union field. Payload is `ErrorSetDecl`.
        error_set_decl,
        /// Declares the beginning of a statement. Used for debug info.
        /// Uses the `dbg_stmt` union field. The line and column are offset
        /// from the parent declaration.
        dbg_stmt,
        /// Marks a variable declaration. Used for debug info.
        /// Uses the `str_op` union field. The string is the local variable name,
        /// and the operand is the pointer to the variable's location. The local
        /// may be a const or a var.
        dbg_var_ptr,
        /// Same as `dbg_var_ptr` but the local is always a const and the operand
        /// is the local's value.
        dbg_var_val,
        /// Uses a name to identify a Decl and takes a pointer to it.
        ///
        /// Uses the `str_tok` union field.
        decl_ref,
        /// Uses a name to identify a Decl and uses it as a value.
        /// Uses the `str_tok` union field.
        decl_val,
        /// Load the value from a pointer. Assumes `x.*` syntax.
        /// Uses `un_node` field. AST node is the `x.*` syntax.
        load,
        /// Arithmetic division. Asserts no integer overflow.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        div,
        /// Given a pointer to an array, slice, or pointer, returns a pointer to the element at
        /// the provided index.
        /// Uses the `pl_node` union field. AST node is a[b] syntax. Payload is `Bin`.
        elem_ptr_node,
        /// Same as `elem_ptr_node` but used only for for loop.
        /// Uses the `pl_node` union field. AST node is the condition of a for loop.
        /// Payload is `Bin`.
        /// No OOB safety check is emitted.
        elem_ptr,
        /// Given a pointer to an array, slice, or pointer, loads the element
        /// at the provided index.
        ///
        /// Uses the `pl_node` union field. AST node is a[b] syntax. Payload is `Bin`.
        elem_ptr_load,
        /// Given an array, slice, or pointer, returns the element at the
        /// provided index.
        ///
        /// Uses the `pl_node` union field. AST node is the condition of a for
        /// loop. Payload is `Bin`.
        ///
        /// No OOB safety check is emitted.
        elem_val,
        /// Same as `elem_val` but takes the index as an immediate value.
        /// No OOB safety check is emitted. A prior instruction must validate this operation.
        /// Uses the `elem_val_imm` union field.
        elem_val_imm,
        /// Emits a compile error if the operand is not `void`.
        /// Uses the `un_node` field.
        ensure_result_used,
        /// Emits a compile error if an error is ignored.
        /// Uses the `un_node` field.
        ensure_result_non_error,
        /// Emits a compile error error union payload is not void.
        ensure_err_union_payload_void,
        /// Create a `E!T` type.
        /// Uses the `pl_node` field with `Bin` payload.
        error_union_type,
        /// `error.Foo` syntax. Uses the `str_tok` field of the Data union.
        error_value,
        /// Implements the `@export` builtin function.
        /// Uses the `pl_node` union field. Payload is `Export`.
        @"export",
        /// Given a pointer to a struct or object that contains virtual fields, returns a pointer
        /// to the named field. The field name is stored in string_bytes. Used by a.b syntax.
        /// Uses `pl_node` field. The AST node is the a.b syntax. Payload is Field.
        field_ptr,
        /// Given a pointer to a struct or object that contains virtual fields, loads from the
        /// named field.
        ///
        /// The field name is stored in string_bytes. Used by a.b syntax.
        ///
        /// This instruction also accepts a pointer.
        ///
        /// Uses `pl_node` field. The AST node is the a.b syntax. Payload is Field.
        field_ptr_load,
        /// Given a pointer to a struct or object that contains virtual fields, returns a pointer
        /// to the named field. The field name is a comptime instruction. Used by @field.
        /// Uses `pl_node` field. The AST node is the builtin call. Payload is FieldNamed.
        field_ptr_named,
        /// Given a pointer to a struct or object that contains virtual fields,
        /// loads from the named field.
        ///
        /// The field name is a comptime instruction. Used by @field.
        ///
        /// Uses `pl_node` field. The AST node is the builtin call. Payload is FieldNamed.
        field_ptr_named_load,
        /// Returns a function type, or a function instance, depending on whether
        /// the body_len is 0. Calling convention is auto.
        /// Uses the `pl_node` union field. `payload_index` points to a `Func`.
        func,
        /// Same as `func` but has an inferred error set.
        func_inferred,
        /// Represents a function declaration or function prototype, depending on
        /// whether body_len is 0.
        /// Uses the `pl_node` union field. `payload_index` points to a `FuncFancy`.
        func_fancy,
        /// Implements the `@import` builtin.
        /// Uses the `pl_tok` field.
        import,
        /// Integer literal that fits in a u64. Uses the `int` union field.
        int,
        /// Arbitrary sized integer literal. Uses the `str` union field.
        int_big,
        /// A float literal that fits in a f64. Uses the float union value.
        float,
        /// A float literal that fits in a f128. Uses the `pl_node` union value.
        /// Payload is `Float128`.
        float128,
        /// Make an integer type out of signedness and bit count.
        /// Payload is `int_type`
        int_type,
        /// Return a boolean false if an optional is null. `x != null`
        /// Uses the `un_node` field.
        is_non_null,
        /// Return a boolean false if an optional is null. `x.* != null`
        /// Uses the `un_node` field.
        is_non_null_ptr,
        /// Return a boolean false if value is an error
        /// Uses the `un_node` field.
        is_non_err,
        /// Return a boolean false if dereferenced pointer is an error
        /// Uses the `un_node` field.
        is_non_err_ptr,
        /// Same as `is_non_er` but doesn't validate that the type can be an error.
        /// Uses the `un_node` field.
        ret_is_non_err,
        /// A labeled block of code that loops forever. At the end of the body will have either
        /// a `repeat` instruction or a `repeat_inline` instruction.
        /// Uses the `pl_node` field. The AST node is either a for loop or while loop.
        /// This ZIR instruction is needed because AIR does not (yet?) match ZIR, and Sema
        /// needs to emit more than 1 AIR block for this instruction.
        /// The payload is `Block`.
        loop,
        /// Sends runtime control flow back to the beginning of the current block.
        /// Uses the `node` field.
        repeat,
        /// Sends comptime control flow back to the beginning of the current block.
        /// Uses the `node` field.
        repeat_inline,
        /// Asserts that all the lengths provided match. Used to build a for loop.
        /// Return value is the length as a usize.
        /// Uses the `pl_node` field with payload `MultiOp`.
        /// There are two items for each AST node inside the for loop condition.
        /// If both items in a pair are `.none`, then this node is an unbounded range.
        /// If only the second item in a pair is `.none`, then the first is an indexable.
        /// Otherwise, the node is a bounded range `a..b`, with the items being `a` and `b`.
        /// Illegal behaviors:
        ///  * If all lengths are unbounded ranges (always a compile error).
        ///  * If any two lengths do not match each other.
        for_len,
        /// Merge two error sets into one, `E1 || E2`.
        /// Uses the `pl_node` field with payload `Bin`.
        merge_error_sets,
        /// Turns an R-Value into a const L-Value. In other words, it takes a value,
        /// stores it in a memory location, and returns a const pointer to it. If the value
        /// is `comptime`, the memory location is global static constant data. Otherwise,
        /// the memory location is in the stack frame, local to the scope containing the
        /// instruction.
        /// Uses the `un_tok` union field.
        ref,
        /// Sends control flow back to the function's callee.
        /// Includes an operand as the return value.
        /// Includes an AST node source location.
        /// Uses the `un_node` union field.
        ret_node,
        /// Sends control flow back to the function's callee.
        /// The operand is a `ret_ptr` instruction, where the return value can be found.
        /// Includes an AST node source location.
        /// Uses the `un_node` union field.
        ret_load,
        /// Sends control flow back to the function's callee.
        /// Includes an operand as the return value.
        /// Includes a token source location.
        /// Uses the `un_tok` union field.
        ret_implicit,
        /// Sends control flow back to the function's callee.
        /// The return operand is `error.foo` where `foo` is given by the string.
        /// If the current function has an inferred error set, the error given by the
        /// name is added to it.
        /// Uses the `str_tok` union field.
        ret_err_value,
        /// A string name is provided which is an anonymous error set value.
        /// If the current function has an inferred error set, the error given by the
        /// name is added to it.
        /// Results in the error code. Note that control flow is not diverted with
        /// this instruction; a following 'ret' instruction will do the diversion.
        /// Uses the `str_tok` union field.
        ret_err_value_code,
        /// Obtains a pointer to the return value.
        /// Uses the `node` union field.
        ret_ptr,
        /// Obtains the return type of the in-scope function.
        /// Uses the `node` union field.
        ret_type,
        /// Create a pointer type which can have a sentinel, alignment, address space, and/or bit range.
        /// Uses the `ptr_type` union field.
        ptr_type,
        /// Slice operation `lhs[rhs..]`. No sentinel and no end offset.
        /// Returns a pointer to the subslice.
        /// Uses the `pl_node` field. AST node is the slice syntax. Payload is `SliceStart`.
        slice_start,
        /// Slice operation `array_ptr[start..end]`. No sentinel.
        /// Returns a pointer to the subslice.
        /// Uses the `pl_node` field. AST node is the slice syntax. Payload is `SliceEnd`.
        slice_end,
        /// Slice operation `array_ptr[start..end:sentinel]`.
        /// Returns a pointer to the subslice.
        /// Uses the `pl_node` field. AST node is the slice syntax. Payload is `SliceSentinel`.
        slice_sentinel,
        /// Slice operation `array_ptr[start..][0..len]`. Optional sentinel.
        /// Returns a pointer to the subslice.
        /// Uses the `pl_node` field. AST node is the slice syntax. Payload is `SliceLength`.
        slice_length,
        /// Given a value which is a pointer to the LHS of a slice operation, return the sentinel
        /// type, used as the result type of the slice sentinel (i.e. `s` in `lhs[a..b :s]`).
        /// Uses the `un_node` field. AST node is the slice syntax. Operand is `lhs`.
        slice_sentinel_ty,
        /// Same as `store` except provides a source location.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        store_node,
        /// Same as `store_node` but the type of the value being stored will be
        /// used to infer the pointer type of an `alloc_inferred`.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        store_to_inferred_ptr,
        /// String Literal. Makes an anonymous Decl and then takes a pointer to it.
        /// Uses the `str` union field.
        str,
        /// Arithmetic negation. Asserts no integer overflow.
        /// Same as sub with a lhs of 0, split into a separate instruction to save memory.
        /// Uses `un_node`.
        negate,
        /// Twos complement wrapping integer negation.
        /// Same as subwrap with a lhs of 0, split into a separate instruction to save memory.
        /// Uses `un_node`.
        negate_wrap,
        /// Returns the type of a value.
        /// Uses the `un_node` field.
        typeof,
        /// Implements `@TypeOf` for one operand.
        /// Uses the `pl_node` field. Payload is `Block`.
        typeof_builtin,
        /// Given a value, look at the type of it, which must be an integer type.
        /// Returns the integer type for the RHS of a shift operation.
        /// Uses the `un_node` field.
        typeof_log2_int_type,
        /// Asserts control-flow will not reach this instruction (`unreachable`).
        /// Uses the `@"unreachable"` union field.
        @"unreachable",
        /// Bitwise XOR. `^`
        /// Uses the `pl_node` union field. Payload is `Bin`.
        xor,
        /// Create an optional type '?T'
        /// Uses the `un_node` field.
        optional_type,
        /// ?T => T with safety.
        /// Given an optional value, returns the payload value, with a safety check that
        /// the value is non-null. Used for `orelse`, `if` and `while`.
        /// Uses the `un_node` field.
        optional_payload_safe,
        /// ?T => T without safety.
        /// Given an optional value, returns the payload value. No safety checks.
        /// Uses the `un_node` field.
        optional_payload_unsafe,
        /// *?T => *T with safety.
        /// Given a pointer to an optional value, returns a pointer to the payload value,
        /// with a safety check that the value is non-null. Used for `orelse`, `if` and `while`.
        /// Uses the `un_node` field.
        optional_payload_safe_ptr,
        /// *?T => *T without safety.
        /// Given a pointer to an optional value, returns a pointer to the payload value.
        /// No safety checks.
        /// Uses the `un_node` field.
        optional_payload_unsafe_ptr,
        /// E!T => T without safety.
        /// Given an error union value, returns the payload value. No safety checks.
        /// Uses the `un_node` field.
        err_union_payload_unsafe,
        /// *E!T => *T without safety.
        /// Given a pointer to a error union value, returns a pointer to the payload value.
        /// No safety checks.
        /// Uses the `un_node` field.
        err_union_payload_unsafe_ptr,
        /// E!T => E without safety.
        /// Given an error union value, returns the error code. No safety checks.
        /// Uses the `un_node` field.
        err_union_code,
        /// *E!T => E without safety.
        /// Given a pointer to an error union value, returns the error code. No safety checks.
        /// Uses the `un_node` field.
        err_union_code_ptr,
        /// An enum literal. Uses the `str_tok` union field.
        enum_literal,
        /// A decl literal. This is similar to `field`, but unwraps error unions and optionals,
        /// and coerces the result to the given type.
        /// Uses the `pl_node` union field. Payload is `Field`.
        decl_literal,
        /// The same as `decl_literal`, but the coercion is omitted. This is used for decl literal
        /// function call syntax, i.e. `.foo()`.
        /// Uses the `pl_node` union field. Payload is `Field`.
        decl_literal_no_coerce,
        /// A switch expression. Uses the `pl_node` union field.
        /// AST node is the switch, payload is `SwitchBlock`.
        switch_block,
        /// A switch expression. Uses the `pl_node` union field.
        /// AST node is the switch, payload is `SwitchBlock`. Operand is a pointer.
        switch_block_ref,
        /// A switch on an error union:
        /// - `eu catch |err| switch (err) {...}`, AST node is the `catch`.
        /// - `if (eu) |payload| {...} else |err| {...}`, AST node is the `if`.
        /// Uses the `pl_node` union field. Payload is `SwitchBlock`.
        switch_block_err_union,
        /// Check that operand type supports the dereference operand (.*).
        /// Uses the `un_node` field.
        validate_deref,
        /// Check that the operand's type is an array or tuple with the given number of elements.
        /// Uses the `pl_node` field. Payload is `ValidateDestructure`.
        validate_destructure,
        /// Given a struct or union, and a field name as a Ref,
        /// returns the field type. Uses the `pl_node` field. Payload is `FieldTypeRef`.
        field_type_ref,
        /// Given a pointer, initializes all error unions and optionals in the pointee to payloads,
        /// returning the base payload pointer. For instance, converts *E!?T into a valid *T
        /// (clobbering any existing error or null value).
        /// Uses the `un_node` field.
        opt_eu_base_ptr_init,
        /// Coerce a given value such that when a reference is taken, the resulting pointer will be
        /// coercible to the given type. For instance, given a value of type 'u32' and the pointer
        /// type '*u64', coerces the value to a 'u64'. Asserts that the type is a pointer type.
        /// Uses the `pl_node` field. Payload is `Bin`.
        /// LHS is the pointer type, RHS is the value.
        coerce_ptr_elem_ty,
        /// Given a type, validate that it is a pointer type suitable for return from the address-of
        /// operator. Emit a compile error if not.
        /// Uses the `un_tok` union field. Token is the `&` operator. Operand is the type.
        validate_ref_ty,
        /// Given a value, check whether it is a valid local constant in this scope.
        /// In a runtime scope, this is always a nop.
        /// In a comptime scope, raises a compile error if the value is runtime-known.
        /// Result is always void.
        /// Uses the `un_node` union field. Node is the initializer. Operand is the initializer value.
        validate_const,

        // The following tags all relate to struct initialization expressions.

        /// A struct literal with a specified explicit type, with no fields.
        /// Uses the `un_node` field.
        struct_init_empty,
        /// An anonymous struct literal with a known result type, with no fields.
        /// Uses the `un_node` field.
        struct_init_empty_result,
        /// An anonymous struct literal with no fields, returned by reference, with a known result
        /// type for the pointer. Asserts that the type is a pointer.
        /// Uses the `un_node` field.
        struct_init_empty_ref_result,
        /// Struct initialization without a type. Creates a value of an anonymous struct type.
        /// Uses the `pl_node` field. Payload is `StructInitAnon`.
        struct_init_anon,
        /// Finalizes a typed struct or union initialization, performs validation, and returns the
        /// struct or union value. The given type must be validated prior to this instruction, using
        /// `validate_struct_init_ty` or `validate_struct_init_result_ty`. If the given type is
        /// generic poison, this is downgraded to an anonymous initialization.
        /// Uses the `pl_node` field. Payload is `StructInit`.
        struct_init,
        /// Struct initialization syntax, make the result a pointer. Equivalent to `struct_init`
        /// followed by `ref` - this ZIR tag exists as an optimization for a common pattern.
        /// Uses the `pl_node` field. Payload is `StructInit`.
        struct_init_ref,
        /// Checks that the type supports struct init syntax. Always returns void.
        /// Uses the `un_node` field.
        validate_struct_init_ty,
        /// Like `validate_struct_init_ty`, but additionally accepts types which structs coerce to.
        /// Used on the known result type of a struct init expression. Always returns void.
        /// Uses the `un_node` field.
        validate_struct_init_result_ty,
        /// Given a set of `struct_init_field_ptr` instructions, assumes they are all part of a
        /// struct initialization expression, and emits compile errors for duplicate fields as well
        /// as missing fields, if applicable.
        /// This instruction asserts that there is at least one struct_init_field_ptr instruction,
        /// because it must use one of them to find out the struct type.
        /// Uses the `pl_node` field. Payload is `Block`.
        validate_ptr_struct_init,
        /// Given a type being used for a struct initialization expression, returns the type of the
        /// field with the given name.
        /// Uses the `pl_node` field. Payload is `FieldType`.
        struct_init_field_type,
        /// Given a pointer being used as the result pointer of a struct initialization expression,
        /// return a pointer to the field of the given name.
        /// Uses the `pl_node` field. The AST node is the field initializer. Payload is Field.
        struct_init_field_ptr,

        // The following tags all relate to array initialization expressions.

        /// Array initialization without a type. Creates a value of a tuple type.
        /// Uses the `pl_node` field. Payload is `MultiOp`.
        array_init_anon,
        /// Array initialization syntax with a known type. The given type must be validated prior to
        /// this instruction, using some `validate_array_init_*_ty` instruction.
        /// Uses the `pl_node` field. Payload is `MultiOp`, where the first operand is the type.
        array_init,
        /// Array initialization syntax, make the result a pointer. Equivalent to `array_init`
        /// followed by `ref`- this ZIR tag exists as an optimization for a common pattern.
        /// Uses the `pl_node` field. Payload is `MultiOp`, where the first operand is the type.
        array_init_ref,
        /// Checks that the type supports array init syntax. Always returns void.
        /// Uses the `pl_node` field. Payload is `ArrayInit`.
        validate_array_init_ty,
        /// Like `validate_array_init_ty`, but additionally accepts types which arrays coerce to.
        /// Used on the known result type of an array init expression. Always returns void.
        /// Uses the `pl_node` field. Payload is `ArrayInit`.
        validate_array_init_result_ty,
        /// Given a pointer or slice type and an element count, return the expected type of an array
        /// initializer such that a pointer to the initializer has the given pointer type, checking
        /// that this type supports array init syntax and emitting a compile error if not. Preserves
        /// error union and optional wrappers on the array type, if any.
        /// Asserts that the given type is a pointer or slice type.
        /// Uses the `pl_node` field. Payload is `ArrayInitRefTy`.
        validate_array_init_ref_ty,
        /// Given a set of `array_init_elem_ptr` instructions, assumes they are all part of an array
        /// initialization expression, and emits a compile error if the number of elements does not
        /// match the array type.
        /// This instruction asserts that there is at least one `array_init_elem_ptr` instruction,
        /// because it must use one of them to find out the array type.
        /// Uses the `pl_node` field. Payload is `Block`.
        validate_ptr_array_init,
        /// Given a type being used for an array initialization expression, returns the type of the
        /// element at the given index.
        /// Uses the `bin` union field. lhs is the indexable type, rhs is the index.
        array_init_elem_type,
        /// Given a pointer being used as the result pointer of an array initialization expression,
        /// return a pointer to the element at the given index.
        /// Uses the `pl_node` union field. AST node is an element inside array initialization
        /// syntax. Payload is `ElemPtrImm`.
        array_init_elem_ptr,

        /// Implements the `@unionInit` builtin.
        /// Uses the `pl_node` field. Payload is `UnionInit`.
        union_init,
        /// Implements the `@typeInfo` builtin. Uses `un_node`.
        type_info,
        /// Implements the `@sizeOf` builtin. Uses `un_node`.
        size_of,
        /// Implements the `@bitSizeOf` builtin. Uses `un_node`.
        bit_size_of,

        /// Implement builtin `@intFromPtr`. Uses `un_node`.
        /// Convert a pointer to a `usize` integer.
        int_from_ptr,
        /// Emit an error message and fail compilation.
        /// Uses the `un_node` field.
        compile_error,
        /// Changes the maximum number of backwards branches that compile-time
        /// code execution can use before giving up and making a compile error.
        /// Uses the `un_node` union field.
        set_eval_branch_quota,
        /// Converts an enum value into an integer. Resulting type will be the tag type
        /// of the enum. Uses `un_node`.
        int_from_enum,
        /// Implement builtin `@alignOf`. Uses `un_node`.
        align_of,
        /// Implement builtin `@intFromBool`. Uses `un_node`.
        int_from_bool,
        /// Implement builtin `@embedFile`. Uses `un_node`.
        embed_file,
        /// Implement builtin `@errorName`. Uses `un_node`.
        error_name,
        /// Implement builtin `@panic`. Uses `un_node`.
        panic,
        /// Implements `@trap`.
        /// Uses the `node` field.
        trap,
        /// Implement builtin `@setRuntimeSafety`. Uses `un_node`.
        set_runtime_safety,
        /// Implement builtin `@sqrt`. Uses `un_node`.
        sqrt,
        /// Implement builtin `@sin`. Uses `un_node`.
        sin,
        /// Implement builtin `@cos`. Uses `un_node`.
        cos,
        /// Implement builtin `@tan`. Uses `un_node`.
        tan,
        /// Implement builtin `@exp`. Uses `un_node`.
        exp,
        /// Implement builtin `@exp2`. Uses `un_node`.
        exp2,
        /// Implement builtin `@log`. Uses `un_node`.
        log,
        /// Implement builtin `@log2`. Uses `un_node`.
        log2,
        /// Implement builtin `@log10`. Uses `un_node`.
        log10,
        /// Implement builtin `@abs`. Uses `un_node`.
        abs,
        /// Implement builtin `@floor`. Uses `un_node`.
        floor,
        /// Implement builtin `@ceil`. Uses `un_node`.
        ceil,
        /// Implement builtin `@trunc`. Uses `un_node`.
        trunc,
        /// Implement builtin `@round`. Uses `un_node`.
        round,
        /// Implement builtin `@tagName`. Uses `un_node`.
        tag_name,
        /// Implement builtin `@typeName`. Uses `un_node`.
        type_name,
        /// Implement builtin `@Frame`. Uses `un_node`.
        frame_type,

        /// Implements the `@intFromFloat` builtin.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        int_from_float,
        /// Implements the `@floatFromInt` builtin.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        float_from_int,
        /// Implements the `@ptrFromInt` builtin.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        ptr_from_int,
        /// Converts an integer into an enum value.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        enum_from_int,
        /// Convert a larger float type to any other float type, possibly causing
        /// a loss of precision.
        /// Uses the `pl_node` field. AST is the `@floatCast` syntax.
        /// Payload is `Bin` with lhs as the dest type, rhs the operand.
        float_cast,
        /// Implements the `@intCast` builtin.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        /// Convert an integer value to another integer type, asserting that the destination type
        /// can hold the same mathematical value.
        int_cast,
        /// Implements the `@ptrCast` builtin.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        /// Not every `@ptrCast` will correspond to this instruction - see also
        /// `ptr_cast_full` in `Extended`.
        ptr_cast,
        /// Implements the `@truncate` builtin.
        /// Uses `pl_node` with payload `Bin`. `lhs` is dest type, `rhs` is operand.
        truncate,

        /// Implements the `@hasDecl` builtin.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        has_decl,
        /// Implements the `@hasField` builtin.
        /// Uses the `pl_node` union field. Payload is `Bin`.
        has_field,

        /// Implements the `@clz` builtin. Uses the `un_node` union field.
        clz,
        /// Implements the `@ctz` builtin. Uses the `un_node` union field.
        ctz,
        /// Implements the `@popCount` builtin. Uses the `un_node` union field.
        pop_count,
        /// Implements the `@byteSwap` builtin. Uses the `un_node` union field.
        byte_swap,
        /// Implements the `@bitReverse` builtin. Uses the `un_node` union field.
        bit_reverse,

        /// Implements the `@bitOffsetOf` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        bit_offset_of,
        /// Implements the `@offsetOf` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        offset_of,
        /// Implements the `@splat` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        splat,
        /// Implements the `@reduce` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        reduce,
        /// Implements the `@shuffle` builtin.
        /// Uses the `pl_node` union field with payload `Shuffle`.
        shuffle,
        /// Implements the `@atomicLoad` builtin.
        /// Uses the `pl_node` union field with payload `AtomicLoad`.
        atomic_load,
        /// Implements the `@atomicRmw` builtin.
        /// Uses the `pl_node` union field with payload `AtomicRmw`.
        atomic_rmw,
        /// Implements the `@atomicStore` builtin.
        /// Uses the `pl_node` union field with payload `AtomicStore`.
        atomic_store,
        /// Implements the `@mulAdd` builtin.
        /// Uses the `pl_node` union field with payload `MulAdd`.
        /// The addend communicates the type of the builtin.
        /// The mulends need to be coerced to the same type.
        mul_add,
        /// Implements the `@memcpy` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        memcpy,
        /// Implements the `@memmove` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        memmove,
        /// Implements the `@memset` builtin.
        /// Uses the `pl_node` union field with payload `Bin`.
        memset,
        /// Implements the `@min` builtin for 2 args.
        /// Uses the `pl_node` union field with payload `Bin`
        min,
        /// Implements the `@max` builtin for 2 args.
        /// Uses the `pl_node` union field with payload `Bin`
        max,
        /// Implements the `@cImport` builtin.
        /// Uses the `pl_node` union field with payload `Block`.
        c_import,

        /// Allocates stack local memory.
        /// Uses the `un_node` union field. The operand is the type of the allocated object.
        /// The node source location points to a var decl node.
        /// A `make_ptr_const` instruction should be used once the value has
        /// been stored to the allocation. To ensure comptime value detection
        /// functions, there are some restrictions on how this pointer should be
        /// used prior to the `make_ptr_const` instruction: no pointer derived
        /// from this `alloc` may be returned from a block or stored to another
        /// address. In other words, it must be trivial to determine whether any
        /// given pointer derives from this one.
        alloc,
        /// Same as `alloc` except mutable. As such, `make_ptr_const` need not be used,
        /// and there are no restrictions on the usage of the pointer.
        alloc_mut,
        /// Allocates comptime-mutable memory.
        /// Uses the `un_node` union field. The operand is the type of the allocated object.
        /// The node source location points to a var decl node.
        alloc_comptime_mut,
        /// Same as `alloc` except the type is inferred.
        /// Uses the `node` union field.
        alloc_inferred,
        /// Same as `alloc_inferred` except mutable.
        alloc_inferred_mut,
        /// Allocates comptime const memory.
        /// Uses the `node` union field. The type of the allocated object is inferred.
        /// The node source location points to a var decl node.
        alloc_inferred_comptime,
        /// Same as `alloc_comptime_mut` except the type is inferred.
        alloc_inferred_comptime_mut,
        /// Each `store_to_inferred_ptr` puts the type of the stored value into a set,
        /// and then `resolve_inferred_alloc` triggers peer type resolution on the set.
        /// The operand is a `alloc_inferred` or `alloc_inferred_mut` instruction, which
        /// is the allocation that needs to have its type inferred.
        /// Results in the final resolved pointer. The `alloc_inferred[_comptime][_mut]`
        /// instruction should never be referred to after this instruction.
        /// Uses the `un_node` field. The AST node is the var decl.
        resolve_inferred_alloc,
        /// Turns a pointer coming from an `alloc` or `Extended.alloc` into a constant
        /// version of the same pointer. For inferred allocations this is instead implicitly
        /// handled by the `resolve_inferred_alloc` instruction.
        /// Uses the `un_node` union field.
        make_ptr_const,

        /// Implements `resume` syntax. Uses `un_node` field.
        @"resume",

        /// A defer statement.
        /// Uses the `defer` union field.
        @"defer",
        /// An errdefer statement with a code.
        /// Uses the `err_defer_code` union field.
        defer_err_code,

        /// Requests that Sema update the saved error return trace index for the enclosing
        /// block, if the operand is .none or of an error/error-union type.
        /// Uses the `save_err_ret_index` field.
        save_err_ret_index,
        /// Specialized form of `Extended.restore_err_ret_index`.
        /// Unconditionally restores the error return index to its last saved state
        /// in the block referred to by `operand`. If `operand` is `none`, restores
        /// to the point of function entry.
        /// Uses the `un_node` field.
        restore_err_ret_index_unconditional,
        /// Specialized form of `Extended.restore_err_ret_index`.
        /// Restores the error return index to its state at the entry of
        /// the current function conditional on `operand` being a non-error.
        /// If `operand` is `none`, restores unconditionally.
        /// Uses the `un_node` field.
        restore_err_ret_index_fn_entry,

        /// The ZIR instruction tag is one of the `Extended` ones.
        /// Uses the `extended` union field.
        extended,

        /// Returns whether the instruction is one of the control flow "noreturn" types.
        /// Function calls do not count.
        pub fn isNoReturn(tag: Tag) bool {
            return switch (tag) {
                .param,
                .param_comptime,
                .param_anytype,
                .param_anytype_comptime,
                .add,
                .addwrap,
                .add_sat,
                .add_unsafe,
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
                .call,
                .field_call,
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
                .elem_ptr,
                .elem_val,
                .elem_ptr_node,
                .elem_ptr_load,
                .elem_val_imm,
                .ensure_result_used,
                .ensure_result_non_error,
                .ensure_err_union_payload_void,
                .@"export",
                .field_ptr,
                .field_ptr_load,
                .field_ptr_named,
                .field_ptr_named_load,
                .func,
                .func_inferred,
                .func_fancy,
                .has_decl,
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
                .store_node,
                .store_to_inferred_ptr,
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
                .typeof_log2_int_type,
                .resolve_inferred_alloc,
                .set_eval_branch_quota,
                .switch_block,
                .switch_block_ref,
                .switch_block_err_union,
                .validate_deref,
                .validate_destructure,
                .union_init,
                .field_type_ref,
                .enum_from_int,
                .int_from_enum,
                .type_info,
                .size_of,
                .bit_size_of,
                .int_from_ptr,
                .align_of,
                .int_from_bool,
                .embed_file,
                .error_name,
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
                .float_cast,
                .int_cast,
                .ptr_cast,
                .truncate,
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
                .atomic_store,
                .mul_add,
                .builtin_call,
                .max,
                .memcpy,
                .memset,
                .memmove,
                .min,
                .c_import,
                .@"resume",
                .ret_err_value_code,
                .extended,
                .ret_ptr,
                .ret_type,
                .@"try",
                .try_ptr,
                .@"defer",
                .defer_err_code,
                .save_err_ret_index,
                .for_len,
                .opt_eu_base_ptr_init,
                .coerce_ptr_elem_ty,
                .struct_init_empty,
                .struct_init_empty_result,
                .struct_init_empty_ref_result,
                .struct_init_anon,
                .struct_init,
                .struct_init_ref,
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
                .validate_ref_ty,
                .validate_const,
                .restore_err_ret_index_unconditional,
                .restore_err_ret_index_fn_entry,
                => false,

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
                => true,
            };
        }

        /// AstGen uses this to find out if `Ref.void_value` should be used in place
        /// of the result of a given instruction. This allows Sema to forego adding
        /// the instruction to the map after analysis.
        pub fn isAlwaysVoid(tag: Tag, data: Data) bool {
            return switch (tag) {
                .dbg_stmt,
                .dbg_var_ptr,
                .dbg_var_val,
                .ensure_result_used,
                .ensure_result_non_error,
                .ensure_err_union_payload_void,
                .set_eval_branch_quota,
                .atomic_store,
                .store_node,
                .store_to_inferred_ptr,
                .validate_deref,
                .validate_destructure,
                .@"export",
                .set_runtime_safety,
                .memcpy,
                .memset,
                .memmove,
                .check_comptime_control_flow,
                .@"defer",
                .defer_err_code,
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
                => true,

                .param,
                .param_comptime,
                .param_anytype,
                .param_anytype_comptime,
                .add,
                .addwrap,
                .add_sat,
                .add_unsafe,
                .alloc,
                .alloc_mut,
                .alloc_comptime_mut,
                .alloc_inferred,
                .alloc_inferred_mut,
                .alloc_inferred_comptime,
                .alloc_inferred_comptime_mut,
                .resolve_inferred_alloc,
                .make_ptr_const,
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
                .call,
                .field_call,
                .cmp_lt,
                .cmp_lte,
                .cmp_eq,
                .cmp_gte,
                .cmp_gt,
                .cmp_neq,
                .error_set_decl,
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
                .has_decl,
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
                .typeof_log2_int_type,
                .switch_block,
                .switch_block_ref,
                .switch_block_err_union,
                .union_init,
                .field_type_ref,
                .enum_from_int,
                .int_from_enum,
                .type_info,
                .size_of,
                .bit_size_of,
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
                .builtin_call,
                .max,
                .min,
                .c_import,
                .@"resume",
                .ret_err_value_code,
                .@"break",
                .break_inline,
                .condbr,
                .condbr_inline,
                .switch_continue,
                .compile_error,
                .ret_node,
                .ret_load,
                .ret_implicit,
                .ret_err_value,
                .ret_ptr,
                .ret_type,
                .@"unreachable",
                .repeat,
                .repeat_inline,
                .panic,
                .trap,
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
                => false,

                .extended => switch (data.extended.opcode) {
                    .branch_hint,
                    .breakpoint,
                    .disable_instrumentation,
                    .disable_intrinsics,
                    => true,
                    else => false,
                },
            };
        }

        /// Used by debug safety-checking code.
        pub const data_tags = list: {
            @setEvalBranchQuota(2000);
            break :list std.enums.directEnumArray(Tag, Data.FieldEnum, 0, .{
                .add = .pl_node,
                .addwrap = .pl_node,
                .add_sat = .pl_node,
                .add_unsafe = .pl_node,
                .sub = .pl_node,
                .subwrap = .pl_node,
                .sub_sat = .pl_node,
                .mul = .pl_node,
                .mulwrap = .pl_node,
                .mul_sat = .pl_node,

                .param = .pl_tok,
                .param_comptime = .pl_tok,
                .param_anytype = .str_tok,
                .param_anytype_comptime = .str_tok,
                .array_cat = .pl_node,
                .array_mul = .pl_node,
                .array_type = .pl_node,
                .array_type_sentinel = .pl_node,
                .reify_int = .pl_node,
                .vector_type = .pl_node,
                .elem_type = .un_node,
                .indexable_ptr_elem_type = .un_node,
                .splat_op_result_ty = .un_node,
                .indexable_ptr_len = .un_node,
                .anyframe_type = .un_node,
                .as_node = .pl_node,
                .as_shift_operand = .pl_node,
                .bit_and = .pl_node,
                .bitcast = .pl_node,
                .bit_not = .un_node,
                .bit_or = .pl_node,
                .block = .pl_node,
                .block_comptime = .pl_node,
                .block_inline = .pl_node,
                .declaration = .declaration,
                .suspend_block = .pl_node,
                .bool_not = .un_node,
                .bool_br_and = .pl_node,
                .bool_br_or = .pl_node,
                .@"break" = .@"break",
                .break_inline = .@"break",
                .switch_continue = .@"break",
                .check_comptime_control_flow = .un_node,
                .for_len = .pl_node,
                .call = .pl_node,
                .field_call = .pl_node,
                .cmp_lt = .pl_node,
                .cmp_lte = .pl_node,
                .cmp_eq = .pl_node,
                .cmp_gte = .pl_node,
                .cmp_gt = .pl_node,
                .cmp_neq = .pl_node,
                .condbr = .pl_node,
                .condbr_inline = .pl_node,
                .@"try" = .pl_node,
                .try_ptr = .pl_node,
                .error_set_decl = .pl_node,
                .dbg_stmt = .dbg_stmt,
                .dbg_var_ptr = .str_op,
                .dbg_var_val = .str_op,
                .decl_ref = .str_tok,
                .decl_val = .str_tok,
                .load = .un_node,
                .div = .pl_node,
                .elem_ptr = .pl_node,
                .elem_ptr_node = .pl_node,
                .elem_val = .pl_node,
                .elem_ptr_load = .pl_node,
                .elem_val_imm = .elem_val_imm,
                .ensure_result_used = .un_node,
                .ensure_result_non_error = .un_node,
                .ensure_err_union_payload_void = .un_node,
                .error_union_type = .pl_node,
                .error_value = .str_tok,
                .@"export" = .pl_node,
                .field_ptr = .pl_node,
                .field_ptr_load = .pl_node,
                .field_ptr_named = .pl_node,
                .field_ptr_named_load = .pl_node,
                .func = .pl_node,
                .func_inferred = .pl_node,
                .func_fancy = .pl_node,
                .import = .pl_tok,
                .int = .int,
                .int_big = .str,
                .float = .float,
                .float128 = .pl_node,
                .int_type = .int_type,
                .is_non_null = .un_node,
                .is_non_null_ptr = .un_node,
                .is_non_err = .un_node,
                .is_non_err_ptr = .un_node,
                .ret_is_non_err = .un_node,
                .loop = .pl_node,
                .repeat = .node,
                .repeat_inline = .node,
                .merge_error_sets = .pl_node,
                .mod_rem = .pl_node,
                .ref = .un_tok,
                .ret_node = .un_node,
                .ret_load = .un_node,
                .ret_implicit = .un_tok,
                .ret_err_value = .str_tok,
                .ret_err_value_code = .str_tok,
                .ret_ptr = .node,
                .ret_type = .node,
                .ptr_type = .ptr_type,
                .slice_start = .pl_node,
                .slice_end = .pl_node,
                .slice_sentinel = .pl_node,
                .slice_length = .pl_node,
                .slice_sentinel_ty = .un_node,
                .store_node = .pl_node,
                .store_to_inferred_ptr = .pl_node,
                .str = .str,
                .negate = .un_node,
                .negate_wrap = .un_node,
                .typeof = .un_node,
                .typeof_log2_int_type = .un_node,
                .@"unreachable" = .@"unreachable",
                .xor = .pl_node,
                .optional_type = .un_node,
                .optional_payload_safe = .un_node,
                .optional_payload_unsafe = .un_node,
                .optional_payload_safe_ptr = .un_node,
                .optional_payload_unsafe_ptr = .un_node,
                .err_union_payload_unsafe = .un_node,
                .err_union_payload_unsafe_ptr = .un_node,
                .err_union_code = .un_node,
                .err_union_code_ptr = .un_node,
                .enum_literal = .str_tok,
                .decl_literal = .pl_node,
                .decl_literal_no_coerce = .pl_node,
                .switch_block = .pl_node,
                .switch_block_ref = .pl_node,
                .switch_block_err_union = .pl_node,
                .validate_deref = .un_node,
                .validate_destructure = .pl_node,
                .field_type_ref = .pl_node,
                .union_init = .pl_node,
                .type_info = .un_node,
                .size_of = .un_node,
                .bit_size_of = .un_node,
                .opt_eu_base_ptr_init = .un_node,
                .coerce_ptr_elem_ty = .pl_node,
                .validate_ref_ty = .un_tok,
                .validate_const = .un_node,

                .int_from_ptr = .un_node,
                .compile_error = .un_node,
                .set_eval_branch_quota = .un_node,
                .int_from_enum = .un_node,
                .align_of = .un_node,
                .int_from_bool = .un_node,
                .embed_file = .un_node,
                .error_name = .un_node,
                .panic = .un_node,
                .trap = .node,
                .set_runtime_safety = .un_node,
                .sqrt = .un_node,
                .sin = .un_node,
                .cos = .un_node,
                .tan = .un_node,
                .exp = .un_node,
                .exp2 = .un_node,
                .log = .un_node,
                .log2 = .un_node,
                .log10 = .un_node,
                .abs = .un_node,
                .floor = .un_node,
                .ceil = .un_node,
                .trunc = .un_node,
                .round = .un_node,
                .tag_name = .un_node,
                .type_name = .un_node,
                .frame_type = .un_node,

                .int_from_float = .pl_node,
                .float_from_int = .pl_node,
                .ptr_from_int = .pl_node,
                .enum_from_int = .pl_node,
                .float_cast = .pl_node,
                .int_cast = .pl_node,
                .ptr_cast = .pl_node,
                .truncate = .pl_node,
                .typeof_builtin = .pl_node,

                .has_decl = .pl_node,
                .has_field = .pl_node,

                .clz = .un_node,
                .ctz = .un_node,
                .pop_count = .un_node,
                .byte_swap = .un_node,
                .bit_reverse = .un_node,

                .div_exact = .pl_node,
                .div_floor = .pl_node,
                .div_trunc = .pl_node,
                .mod = .pl_node,
                .rem = .pl_node,

                .shl = .pl_node,
                .shl_exact = .pl_node,
                .shl_sat = .pl_node,
                .shr = .pl_node,
                .shr_exact = .pl_node,

                .bit_offset_of = .pl_node,
                .offset_of = .pl_node,
                .splat = .pl_node,
                .reduce = .pl_node,
                .shuffle = .pl_node,
                .atomic_load = .pl_node,
                .atomic_rmw = .pl_node,
                .atomic_store = .pl_node,
                .mul_add = .pl_node,
                .builtin_call = .pl_node,
                .max = .pl_node,
                .memcpy = .pl_node,
                .memset = .pl_node,
                .memmove = .pl_node,
                .min = .pl_node,
                .c_import = .pl_node,

                .alloc = .un_node,
                .alloc_mut = .un_node,
                .alloc_comptime_mut = .un_node,
                .alloc_inferred = .node,
                .alloc_inferred_mut = .node,
                .alloc_inferred_comptime = .node,
                .alloc_inferred_comptime_mut = .node,
                .resolve_inferred_alloc = .un_node,
                .make_ptr_const = .un_node,

                .@"resume" = .un_node,

                .@"defer" = .@"defer",
                .defer_err_code = .defer_err_code,

                .save_err_ret_index = .save_err_ret_index,
                .restore_err_ret_index_unconditional = .un_node,
                .restore_err_ret_index_fn_entry = .un_node,

                .struct_init_empty = .un_node,
                .struct_init_empty_result = .un_node,
                .struct_init_empty_ref_result = .un_node,
                .struct_init_anon = .pl_node,
                .struct_init = .pl_node,
                .struct_init_ref = .pl_node,
                .validate_struct_init_ty = .un_node,
                .validate_struct_init_result_ty = .un_node,
                .validate_ptr_struct_init = .pl_node,
                .struct_init_field_type = .pl_node,
                .struct_init_field_ptr = .pl_node,
                .array_init_anon = .pl_node,
                .array_init = .pl_node,
                .array_init_ref = .pl_node,
                .validate_array_init_ty = .pl_node,
                .validate_array_init_result_ty = .pl_node,
                .validate_array_init_ref_ty = .pl_node,
                .validate_ptr_array_init = .pl_node,
                .array_init_elem_type = .bin,
                .array_init_elem_ptr = .pl_node,

                .extended = .extended,
            });
        };

        // Uncomment to view how many tag slots are available.
        //comptime {
        //    @compileLog("ZIR tags left: ", 256 - @typeInfo(Tag).@"enum".fields.len);
        //}
    };

    /// Rarer instructions are here; ones that do not fit in the 8-bit `Tag` enum.
    /// `noreturn` instructions may not go here; they must be part of the main `Tag` enum.
    pub const Extended = enum(u16) {
        /// A struct type definition. Contains references to ZIR instructions for
        /// the field types, defaults, and alignments.
        /// `operand` is payload index to `StructDecl`.
        /// `small` is `StructDecl.Small`.
        struct_decl,
        /// An enum type definition. Contains references to ZIR instructions for
        /// the field value expressions and optional type tag expression.
        /// `operand` is payload index to `EnumDecl`.
        /// `small` is `EnumDecl.Small`.
        enum_decl,
        /// A union type definition. Contains references to ZIR instructions for
        /// the field types and optional type tag expression.
        /// `operand` is payload index to `UnionDecl`.
        /// `small` is `UnionDecl.Small`.
        union_decl,
        /// An opaque type definition. Contains references to decls and captures.
        /// `operand` is payload index to `OpaqueDecl`.
        /// `small` is `OpaqueDecl.Small`.
        opaque_decl,
        /// A tuple type. Note that tuples are not namespace/container types.
        /// `operand` is payload index to `TupleDecl`.
        /// `small` is `fields_len: u16`.
        tuple_decl,
        /// Implements the `@This` builtin.
        /// `operand` is `src_node: Ast.Node.Offset`.
        this,
        /// Implements the `@returnAddress` builtin.
        /// `operand` is `src_node: Ast.Node.Offset`.
        ret_addr,
        /// Implements the `@src` builtin.
        /// `operand` is payload index to `LineColumn`.
        builtin_src,
        /// Implements the `@errorReturnTrace` builtin.
        /// `operand` is `src_node: Ast.Node.Offset`.
        error_return_trace,
        /// Implements the `@frame` builtin.
        /// `operand` is `src_node: Ast.Node.Offset`.
        frame,
        /// Implements the `@frameAddress` builtin.
        /// `operand` is `src_node: Ast.Node.Offset`.
        frame_address,
        /// Same as `alloc` from `Tag` but may contain an alignment instruction.
        /// `operand` is payload index to `AllocExtended`.
        /// `small`:
        ///  * 0b000X - has type
        ///  * 0b00X0 - has alignment
        ///  * 0b0X00 - 1=const, 0=var
        ///  * 0bX000 - is comptime
        alloc,
        /// The `@extern` builtin.
        /// `operand` is payload index to `BinNode`.
        builtin_extern,
        /// Inline assembly.
        /// `operand` is payload index to `Asm`.
        @"asm",
        /// Same as `asm` except the assembly template is not a string literal but a comptime
        /// expression.
        /// The `asm_source` field of the Asm is not a null-terminated string
        /// but instead a Ref.
        asm_expr,
        /// Log compile time variables and emit an error message.
        /// `operand` is payload index to `NodeMultiOp`.
        /// `small` is `operands_len`.
        /// The AST node is the compile log builtin call.
        compile_log,
        /// The builtin `@TypeOf` which returns the type after Peer Type Resolution
        /// of one or more params.
        /// `operand` is payload index to `TypeOfPeer`.
        /// `small` is `operands_len`.
        /// The AST node is the builtin call.
        typeof_peer,
        /// Implements the `@min` builtin for more than 2 args.
        /// `operand` is payload index to `NodeMultiOp`.
        /// `small` is `operands_len`.
        /// The AST node is the builtin call.
        min_multi,
        /// Implements the `@max` builtin for more than 2 args.
        /// `operand` is payload index to `NodeMultiOp`.
        /// `small` is `operands_len`.
        /// The AST node is the builtin call.
        max_multi,
        /// Implements the `@addWithOverflow` builtin.
        /// `operand` is payload index to `BinNode`.
        /// `small` is unused.
        add_with_overflow,
        /// Implements the `@subWithOverflow` builtin.
        /// `operand` is payload index to `BinNode`.
        /// `small` is unused.
        sub_with_overflow,
        /// Implements the `@mulWithOverflow` builtin.
        /// `operand` is payload index to `BinNode`.
        /// `small` is unused.
        mul_with_overflow,
        /// Implements the `@shlWithOverflow` builtin.
        /// `operand` is payload index to `BinNode`.
        /// `small` is unused.
        shl_with_overflow,
        /// `@round`, `@floor`, `@ceil`, or `@trunc`, with a result type.
        /// `operand` is payload index to `BinNode`.
        /// `small` is a `RoundOp` representing the specific operation being performed.
        round_op,
        /// Returns the type for the operand of a rounding op.
        /// `operand` is `UnNode`.
        /// `small` is unused.
        round_op_ty,
        /// `operand` is payload index to `UnNode`.
        c_undef,
        /// `operand` is payload index to `UnNode`.
        c_include,
        /// `operand` is payload index to `BinNode`.
        c_define,
        /// `operand` is payload index to `UnNode`.
        wasm_memory_size,
        /// `operand` is payload index to `BinNode`.
        wasm_memory_grow,
        /// The `@prefetch` builtin.
        /// `operand` is payload index to `BinNode`.
        prefetch,
        /// Implement builtin `@setFloatMode`.
        /// `operand` is payload index to `UnNode`.
        set_float_mode,
        /// Implements the `@errorCast` builtin.
        /// `operand` is payload index to `BinNode`. `lhs` is dest type, `rhs` is operand.
        error_cast,
        /// Implements `@breakpoint`.
        /// `operand` is `src_node: Ast.Node.Offset`.
        breakpoint,
        /// Implement builtin `@disableInstrumentation`. `operand` is `src_node: Ast.Node.Offset`.
        disable_instrumentation,
        /// Implement builtin `@disableIntrinsics`. `operand` is `src_node: i32`.
        disable_intrinsics,
        /// Implements the `@select` builtin.
        /// `operand` is payload index to `Select`.
        select,
        /// Implement builtin `@errToInt`.
        /// `operand` is payload index to `UnNode`.
        int_from_error,
        /// Implement builtin `@errorFromInt`.
        /// `operand` is payload index to `UnNode`.
        error_from_int,
        /// Given a comptime-known operand of type `[]const A`, returns the type `*const [operand.len]B`.
        /// The types `A` and `B` are determined from `ReifySliceArgInfo`.
        /// This instruction is used to provide result types to arguments of `@Fn`, `@Struct`, etc.
        /// `operand` is payload index to `UnNode`.
        /// `small` is a bitcast `ReifySliceArgInfo`.
        reify_slice_arg_ty,
        /// Like `reify_slice_arg_ty` for the specific case of `[]const []const u8` to `[]const TagInt`,
        /// as needed for `@Enum`.
        /// `operand` is payload index to `BinNode`. lhs is the type `TagInt`. rhs is the `[]const []const u8` value.
        /// `small` is unused.
        reify_enum_value_slice_ty,
        /// Given a comptime-known operand of type `type`, returns the type `?operand` if possible, otherwise `?noreturn`.
        /// Used for the final arg of `@Pointer` to allow reifying pointers to opaque types.
        /// `operand` is payload index to `UnNode`.
        /// `small` is unused.
        reify_pointer_sentinel_ty,
        /// Implements builtin `@Tuple`.
        /// `operand` is payload index to `UnNode`.
        reify_tuple,
        /// Implements builtin `@Pointer`.
        /// `operand` is payload index to `ReifyPointer`.
        reify_pointer,
        /// Implements builtin `@Fn`.
        /// `operand` is payload index to `ReifyFn`.
        reify_fn,
        /// Implements builtin `@Struct`.
        /// `operand` is payload index to `ReifyStruct`.
        /// `small` contains `NameStrategy`.
        reify_struct,
        /// Implements builtin `@Union`.
        /// `operand` is payload index to `ReifyUnion`.
        /// `small` contains `NameStrategy`.
        reify_union,
        /// Implements builtin `@Enum`.
        /// `operand` is payload index to `ReifyEnum`.
        /// `small` contains `NameStrategy`.
        reify_enum,
        /// Implements the `@cmpxchgStrong` and `@cmpxchgWeak` builtins.
        /// `small` 0=>weak 1=>strong
        /// `operand` is payload index to `Cmpxchg`.
        cmpxchg,
        /// Implement builtin `@cVaArg`.
        /// `operand` is payload index to `BinNode`.
        c_va_arg,
        /// Implement builtin `@cVaCopy`.
        /// `operand` is payload index to `UnNode`.
        c_va_copy,
        /// Implement builtin `@cVaEnd`.
        /// `operand` is payload index to `UnNode`.
        c_va_end,
        /// Implement builtin `@cVaStart`.
        /// `operand` is `src_node: Ast.Node.Offset`.
        c_va_start,
        /// Implements the following builtins:
        /// `@ptrCast`, `@alignCast`, `@addrSpaceCast`, `@constCast`, `@volatileCast`.
        /// Represents an arbitrary nesting of the above builtins. Such a nesting is treated as a
        /// single operation which can modify multiple components of a pointer type.
        /// `operand` is payload index to `BinNode`.
        /// `small` contains `FullPtrCastFlags`.
        /// AST node is the root of the nested casts.
        /// `lhs` is dest type, `rhs` is operand.
        ptr_cast_full,
        /// `operand` is payload index to `UnNode`.
        /// `small` contains `FullPtrCastFlags`.
        /// Guaranteed to only have flags where no explicit destination type is
        /// required (const_cast and volatile_cast).
        /// AST node is the root of the nested casts.
        ptr_cast_no_dest,
        /// Implements the `@workItemId` builtin.
        /// `operand` is payload index to `UnNode`.
        work_item_id,
        /// Implements the `@workGroupSize` builtin.
        /// `operand` is payload index to `UnNode`.
        work_group_size,
        /// Implements the `@workGroupId` builtin.
        /// `operand` is payload index to `UnNode`.
        work_group_id,
        /// Implements the `@inComptime` builtin.
        /// `operand` is `src_node: Ast.Node.Offset`.
        in_comptime,
        /// Restores the error return index to its last saved state in a given
        /// block. If the block is `.none`, restores to the state from the point
        /// of function entry. If the operand is not `.none`, the restore is
        /// conditional on the operand value not being an error.
        /// `operand` is payload index to `RestoreErrRetIndex`.
        /// `small` is undefined.
        restore_err_ret_index,
        /// Retrieves a value from the current type declaration scope's closure.
        /// `operand` is `src_node: Ast.Node.Offset`.
        /// `small` is closure index.
        closure_get,
        /// Used as a placeholder instruction which is just a dummy index for Sema to replace
        /// with a specific value. For instance, this is used for the capture of an `errdefer`.
        /// This should never appear in a body.
        value_placeholder,
        /// Implements the `@fieldParentPtr` builtin.
        /// `operand` is payload index to `FieldParentPtr`.
        /// `small` contains `FullPtrCastFlags`.
        /// Guaranteed to not have the `ptr_cast` flag.
        /// Uses the `pl_node` union field with payload `FieldParentPtr`.
        field_parent_ptr,
        /// Get a type or value from `std.builtin`.
        /// `operand` is `src_node: Ast.Node.Offset`.
        /// `small` is an `Inst.BuiltinValue`.
        builtin_value,
        /// Provide a `@branchHint` for the current block.
        /// `operand` is payload index to `UnNode`.
        /// `small` is unused.
        branch_hint,
        /// Compute the result type for in-place arithmetic, e.g. `+=`.
        /// `operand` is `Zir.Inst.Ref` of the loaded LHS (*not* its type).
        /// `small` is an `Inst.InplaceOp`.
        inplace_arith_result_ty,
        /// Marks a statement that can be stepped to but produces no code.
        /// `operand` and `small` are ignored.
        dbg_empty_stmt,
        /// At this point, AstGen encountered a fatal error which terminated ZIR lowering for this body.
        /// A file-level error has been reported. Sema should terminate semantic analysis.
        /// `operand` and `small` are ignored.
        /// This instruction is always `noreturn`, however, it is not considered as such by ZIR-level queries. This allows AstGen to assume that
        /// any code may have gone here, avoiding false-positive "unreachable code" errors.
        astgen_error,
        /// Given a type, strips away any error unions or optionals stacked
        /// on top and returns the base type. That base type must be a float.
        /// For example: Provided with error{Foo}!?f64, returns f64.
        /// `operand` is `operand: Air.Inst.Ref`.
        float_op_result_ty,

        pub const InstData = struct {
            opcode: Extended,
            small: u16,
            operand: u32,
        };
    };

    /// The position of a ZIR instruction within the `Zir` instructions array.
    pub const Index = enum(u32) {
        /// ZIR is structured so that the outermost "main" struct of any file
        /// is always at index 0.
        main_struct_inst = 0,
        _,

        pub fn toRef(i: Index) Inst.Ref {
            return @enumFromInt(Ref.static_len + @intFromEnum(i));
        }

        pub fn toOptional(i: Index) OptionalIndex {
            return @enumFromInt(@intFromEnum(i));
        }
    };

    pub const OptionalIndex = enum(u32) {
        /// ZIR is structured so that the outermost "main" struct of any file
        /// is always at index 0.
        main_struct_inst = 0,
        none = std.math.maxInt(u32),
        _,

        pub fn unwrap(oi: OptionalIndex) ?Index {
            return if (oi == .none) null else @enumFromInt(@intFromEnum(oi));
        }
    };

    /// A reference to ZIR instruction, or to an InternPool index, or neither.
    ///
    /// If the integer tag value is < InternPool.static_len, then it
    /// corresponds to an InternPool index. Otherwise, this refers to a ZIR
    /// instruction.
    ///
    /// The tag type is specified so that it is safe to bitcast between `[]u32`
    /// and `[]Ref`.
    pub const Ref = enum(u32) {
        u0_type,
        i0_type,
        u1_type,
        u8_type,
        i8_type,
        u16_type,
        i16_type,
        u29_type,
        u32_type,
        i32_type,
        u64_type,
        i64_type,
        u80_type,
        u128_type,
        i128_type,
        u256_type,
        usize_type,
        isize_type,
        c_char_type,
        c_short_type,
        c_ushort_type,
        c_int_type,
        c_uint_type,
        c_long_type,
        c_ulong_type,
        c_longlong_type,
        c_ulonglong_type,
        c_longdouble_type,
        f16_type,
        f32_type,
        f64_type,
        f80_type,
        f128_type,
        anyopaque_type,
        bool_type,
        void_type,
        type_type,
        anyerror_type,
        comptime_int_type,
        comptime_float_type,
        noreturn_type,
        anyframe_type,
        null_type,
        undefined_type,
        enum_literal_type,
        ptr_usize_type,
        ptr_const_comptime_int_type,
        manyptr_u8_type,
        manyptr_const_u8_type,
        manyptr_const_u8_sentinel_0_type,
        slice_const_u8_type,
        slice_const_u8_sentinel_0_type,
        manyptr_const_slice_const_u8_type,
        slice_const_slice_const_u8_type,
        optional_type_type,
        manyptr_const_type_type,
        slice_const_type_type,
        vector_8_i8_type,
        vector_16_i8_type,
        vector_32_i8_type,
        vector_64_i8_type,
        vector_1_u8_type,
        vector_2_u8_type,
        vector_4_u8_type,
        vector_8_u8_type,
        vector_16_u8_type,
        vector_32_u8_type,
        vector_64_u8_type,
        vector_2_i16_type,
        vector_4_i16_type,
        vector_8_i16_type,
        vector_16_i16_type,
        vector_32_i16_type,
        vector_4_u16_type,
 
```
