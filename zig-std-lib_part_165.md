```
f (a_tag != b_tag) return false;
        return switch (a) {
            .none, .fast, .uuid, .sha1, .md5 => true,
            .hexstring => |a_hexstring| mem.eql(u8, a_hexstring.toSlice(), b.hexstring.toSlice()),
        };
    }

    pub const HexString = struct {
        bytes: [32]u8,
        len: u8,

        /// Result is byte values, *not* hex-encoded.
        pub fn toSlice(hs: *const HexString) []const u8 {
            return hs.bytes[0..hs.len];
        }
    };

    /// Input is byte values, *not* hex-encoded.
    /// Asserts `bytes` fits inside `HexString`
    pub fn initHexString(bytes: []const u8) BuildId {
        var result: BuildId = .{ .hexstring = .{
            .bytes = undefined,
            .len = @intCast(bytes.len),
        } };
        @memcpy(result.hexstring.bytes[0..bytes.len], bytes);
        return result;
    }

    /// Converts UTF-8 text to a `BuildId`.
    pub fn parse(text: []const u8) !BuildId {
        if (mem.eql(u8, text, "none")) {
            return .none;
        } else if (mem.eql(u8, text, "fast")) {
            return .fast;
        } else if (mem.eql(u8, text, "uuid")) {
            return .uuid;
        } else if (mem.eql(u8, text, "sha1") or mem.eql(u8, text, "tree")) {
            return .sha1;
        } else if (mem.eql(u8, text, "md5")) {
            return .md5;
        } else if (mem.startsWith(u8, text, "0x")) {
            var result: BuildId = .{ .hexstring = undefined };
            const slice = try std.fmt.hexToBytes(&result.hexstring.bytes, text[2..]);
            result.hexstring.len = @as(u8, @intCast(slice.len));
            return result;
        }
        return error.InvalidBuildIdStyle;
    }

    test parse {
        try std.testing.expectEqual(BuildId.md5, try parse("md5"));
        try std.testing.expectEqual(BuildId.none, try parse("none"));
        try std.testing.expectEqual(BuildId.fast, try parse("fast"));
        try std.testing.expectEqual(BuildId.uuid, try parse("uuid"));
        try std.testing.expectEqual(BuildId.sha1, try parse("sha1"));
        try std.testing.expectEqual(BuildId.sha1, try parse("tree"));

        try std.testing.expect(BuildId.initHexString("").eql(try parse("0x")));
        try std.testing.expect(BuildId.initHexString("\x12\x34\x56").eql(try parse("0x123456")));
        try std.testing.expectError(error.InvalidLength, parse("0x12-34"));
        try std.testing.expectError(error.InvalidCharacter, parse("0xfoobbb"));
        try std.testing.expectError(error.InvalidBuildIdStyle, parse("yaddaxxx"));
    }

    pub fn format(id: BuildId, writer: *Writer) Writer.Error!void {
        switch (id) {
            .none, .fast, .uuid, .sha1, .md5 => {
                try writer.writeAll(@tagName(id));
            },
            .hexstring => |hs| {
                try writer.print("0x{x}", .{hs.toSlice()});
            },
        }
    }

    test format {
        try std.testing.expectFmt("none", "{f}", .{@as(BuildId, .none)});
        try std.testing.expectFmt("fast", "{f}", .{@as(BuildId, .fast)});
        try std.testing.expectFmt("uuid", "{f}", .{@as(BuildId, .uuid)});
        try std.testing.expectFmt("sha1", "{f}", .{@as(BuildId, .sha1)});
        try std.testing.expectFmt("md5", "{f}", .{@as(BuildId, .md5)});
        try std.testing.expectFmt("0x", "{f}", .{BuildId.initHexString("")});
        try std.testing.expectFmt("0x1234cdef", "{f}", .{BuildId.initHexString("\x12\x34\xcd\xef")});
    }
};

pub const LtoMode = enum { none, full, thin };

pub const Subsystem = enum {
    console,
    windows,
    posix,
    native,
    efi_application,
    efi_boot_service_driver,
    efi_rom,
    efi_runtime_driver,

    /// Deprecated; use '.console' instead. To be removed after 0.16.0 is tagged.
    pub const Console: Subsystem = .console;
    /// Deprecated; use '.windows' instead. To be removed after 0.16.0 is tagged.
    pub const Windows: Subsystem = .windows;
    /// Deprecated; use '.posix' instead. To be removed after 0.16.0 is tagged.
    pub const Posix: Subsystem = .posix;
    /// Deprecated; use '.native' instead. To be removed after 0.16.0 is tagged.
    pub const Native: Subsystem = .native;
    /// Deprecated; use '.efi_application' instead. To be removed after 0.16.0 is tagged.
    pub const EfiApplication: Subsystem = .efi_application;
    /// Deprecated; use '.efi_boot_service_driver' instead. To be removed after 0.16.0 is tagged.
    pub const EfiBootServiceDriver: Subsystem = .efi_boot_service_driver;
    /// Deprecated; use '.efi_rom' instead. To be removed after 0.16.0 is tagged.
    pub const EfiRom: Subsystem = .efi_rom;
    /// Deprecated; use '.efi_runtime_driver' instead. To be removed after 0.16.0 is tagged.
    pub const EfiRuntimeDriver: Subsystem = .efi_runtime_driver;
};

pub const CompressDebugSections = enum { none, zlib, zstd };

pub const RcIncludes = enum {
    /// Use MSVC if available, fall back to MinGW.
    any,
    /// Use MSVC include paths (MSVC install + Windows SDK, must be present on the system).
    msvc,
    /// Use MinGW include paths (distributed with Zig).
    gnu,
    /// Do not use any autodetected include paths.
    none,
};

/// Renders a `std.Target.Cpu` value into a textual representation that can be parsed
/// via the `-mcpu` flag passed to the Zig compiler.
/// Appends the result to `buffer`.
pub fn serializeCpu(buffer: *std.array_list.Managed(u8), cpu: std.Target.Cpu) Allocator.Error!void {
    const all_features = cpu.arch.allFeaturesList();
    var populated_cpu_features = cpu.model.features;
    populated_cpu_features.populateDependencies(all_features);

    try buffer.appendSlice(cpu.model.name);

    if (populated_cpu_features.eql(cpu.features)) {
        // The CPU name alone is sufficient.
        return;
    }

    for (all_features, 0..) |feature, i_usize| {
        const i: std.Target.Cpu.Feature.Set.Index = @intCast(i_usize);
        const in_cpu_set = populated_cpu_features.isEnabled(i);
        const in_actual_set = cpu.features.isEnabled(i);
        try buffer.ensureUnusedCapacity(feature.name.len + 1);
        if (in_cpu_set and !in_actual_set) {
            buffer.appendAssumeCapacity('-');
            buffer.appendSliceAssumeCapacity(feature.name);
        } else if (!in_cpu_set and in_actual_set) {
            buffer.appendAssumeCapacity('+');
            buffer.appendSliceAssumeCapacity(feature.name);
        }
    }
}

pub fn serializeCpuAlloc(ally: Allocator, cpu: std.Target.Cpu) Allocator.Error![]u8 {
    var buffer = std.array_list.Managed(u8).init(ally);
    try serializeCpu(&buffer, cpu);
    return buffer.toOwnedSlice();
}

/// Return a Formatter for a Zig identifier, escaping it with `@""` syntax if needed.
///
/// See also `fmtIdFlags`.
pub fn fmtId(bytes: []const u8) FormatId {
    return .{ .bytes = bytes, .flags = .{} };
}

/// Return a Formatter for a Zig identifier, escaping it with `@""` syntax if needed.
///
/// See also `fmtId`.
pub fn fmtIdFlags(bytes: []const u8, flags: FormatId.Flags) FormatId {
    return .{ .bytes = bytes, .flags = flags };
}

pub fn fmtIdPU(bytes: []const u8) FormatId {
    return .{ .bytes = bytes, .flags = .{ .allow_primitive = true, .allow_underscore = true } };
}

pub fn fmtIdP(bytes: []const u8) FormatId {
    return .{ .bytes = bytes, .flags = .{ .allow_primitive = true } };
}

test fmtId {
    const expectFmt = std.testing.expectFmt;
    try expectFmt("@\"while\"", "{f}", .{fmtId("while")});
    try expectFmt("@\"while\"", "{f}", .{fmtIdFlags("while", .{ .allow_primitive = true })});
    try expectFmt("@\"while\"", "{f}", .{fmtIdFlags("while", .{ .allow_underscore = true })});
    try expectFmt("@\"while\"", "{f}", .{fmtIdFlags("while", .{ .allow_primitive = true, .allow_underscore = true })});

    try expectFmt("hello", "{f}", .{fmtId("hello")});
    try expectFmt("hello", "{f}", .{fmtIdFlags("hello", .{ .allow_primitive = true })});
    try expectFmt("hello", "{f}", .{fmtIdFlags("hello", .{ .allow_underscore = true })});
    try expectFmt("hello", "{f}", .{fmtIdFlags("hello", .{ .allow_primitive = true, .allow_underscore = true })});

    try expectFmt("@\"type\"", "{f}", .{fmtId("type")});
    try expectFmt("type", "{f}", .{fmtIdFlags("type", .{ .allow_primitive = true })});
    try expectFmt("@\"type\"", "{f}", .{fmtIdFlags("type", .{ .allow_underscore = true })});
    try expectFmt("type", "{f}", .{fmtIdFlags("type", .{ .allow_primitive = true, .allow_underscore = true })});

    try expectFmt("@\"_\"", "{f}", .{fmtId("_")});
    try expectFmt("@\"_\"", "{f}", .{fmtIdFlags("_", .{ .allow_primitive = true })});
    try expectFmt("_", "{f}", .{fmtIdFlags("_", .{ .allow_underscore = true })});
    try expectFmt("_", "{f}", .{fmtIdFlags("_", .{ .allow_primitive = true, .allow_underscore = true })});

    try expectFmt("@\"i123\"", "{f}", .{fmtId("i123")});
    try expectFmt("i123", "{f}", .{fmtIdFlags("i123", .{ .allow_primitive = true })});
    try expectFmt("@\"4four\"", "{f}", .{fmtId("4four")});
    try expectFmt("_underscore", "{f}", .{fmtId("_underscore")});
    try expectFmt("@\"11\\\"23\"", "{f}", .{fmtId("11\"23")});
    try expectFmt("@\"11\\x0f23\"", "{f}", .{fmtId("11\x0F23")});

    // These are technically not currently legal in Zig.
    try expectFmt("@\"\"", "{f}", .{fmtId("")});
    try expectFmt("@\"\\x00\"", "{f}", .{fmtId("\x00")});
}

pub const FormatId = struct {
    bytes: []const u8,
    flags: Flags,
    pub const Flags = struct {
        allow_primitive: bool = false,
        allow_underscore: bool = false,
    };

    /// Print the string as a Zig identifier, escaping it with `@""` syntax if needed.
    pub fn format(ctx: FormatId, writer: *Writer) Writer.Error!void {
        const bytes = ctx.bytes;
        if (isValidId(bytes) and
            (ctx.flags.allow_primitive or !std.zig.isPrimitive(bytes)) and
            (ctx.flags.allow_underscore or !isUnderscore(bytes)))
        {
            return writer.writeAll(bytes);
        }
        try writer.writeAll("@\"");
        try stringEscape(bytes, writer);
        try writer.writeByte('"');
    }
};

/// Return a formatter for escaping a double quoted Zig string.
pub fn fmtString(bytes: []const u8) std.fmt.Alt([]const u8, stringEscape) {
    return .{ .data = bytes };
}

/// Return a formatter for escaping a single quoted Zig string.
pub fn fmtChar(c: u21) std.fmt.Alt(u21, charEscape) {
    return .{ .data = c };
}

test fmtString {
    try std.testing.expectFmt("\\x0f", "{f}", .{fmtString("\x0f")});
    try std.testing.expectFmt(
        \\" \\ hi \x07 \x11 \" derp '"
    , "\"{f}\"", .{fmtString(" \\ hi \x07 \x11 \" derp '")});
}

test fmtChar {
    try std.testing.expectFmt("c \\u{26a1}", "{f} {f}", .{ fmtChar('c'), fmtChar('⚡') });
}

/// Print the string as escaped contents of a double quoted string.
pub fn stringEscape(bytes: []const u8, w: *Writer) Writer.Error!void {
    for (bytes) |byte| switch (byte) {
        '\n' => try w.writeAll("\\n"),
        '\r' => try w.writeAll("\\r"),
        '\t' => try w.writeAll("\\t"),
        '\\' => try w.writeAll("\\\\"),
        '"' => try w.writeAll("\\\""),
        '\'' => try w.writeByte('\''),
        ' ', '!', '#'...'&', '('...'[', ']'...'~' => try w.writeByte(byte),
        else => {
            try w.writeAll("\\x");
            try w.printInt(byte, 16, .lower, .{ .width = 2, .fill = '0' });
        },
    };
}

/// Print as escaped contents of a single-quoted string.
pub fn charEscape(codepoint: u21, w: *Writer) Writer.Error!void {
    switch (codepoint) {
        '\n' => try w.writeAll("\\n"),
        '\r' => try w.writeAll("\\r"),
        '\t' => try w.writeAll("\\t"),
        '\\' => try w.writeAll("\\\\"),
        '\'' => try w.writeAll("\\'"),
        '"', ' ', '!', '#'...'&', '('...'[', ']'...'~' => try w.writeByte(@intCast(codepoint)),
        else => {
            if (std.math.cast(u8, codepoint)) |byte| {
                try w.writeAll("\\x");
                try w.printInt(byte, 16, .lower, .{ .width = 2, .fill = '0' });
            } else {
                try w.writeAll("\\u{");
                try w.printInt(codepoint, 16, .lower, .{});
                try w.writeByte('}');
            }
        },
    }
}

pub fn isValidId(bytes: []const u8) bool {
    if (bytes.len == 0) return false;
    for (bytes, 0..) |c, i| {
        switch (c) {
            '_', 'a'...'z', 'A'...'Z' => {},
            '0'...'9' => if (i == 0) return false,
            else => return false,
        }
    }
    return std.zig.Token.getKeyword(bytes) == null;
}

test isValidId {
    try std.testing.expect(!isValidId(""));
    try std.testing.expect(isValidId("foobar"));
    try std.testing.expect(!isValidId("a b c"));
    try std.testing.expect(!isValidId("3d"));
    try std.testing.expect(!isValidId("enum"));
    try std.testing.expect(isValidId("i386"));
}

pub fn isUnderscore(bytes: []const u8) bool {
    return bytes.len == 1 and bytes[0] == '_';
}

test isUnderscore {
    try std.testing.expect(isUnderscore("_"));
    try std.testing.expect(!isUnderscore("__"));
    try std.testing.expect(!isUnderscore("_foo"));
    try std.testing.expect(isUnderscore("\x5f"));
    try std.testing.expect(!isUnderscore("\\x5f"));
}

/// If the source can be UTF-16LE encoded, this function asserts that `gpa`
/// will align a byte-sized allocation to at least 2. Allocators that don't do
/// this are rare.
pub fn readSourceFileToEndAlloc(gpa: Allocator, file_reader: *Io.File.Reader) ![:0]u8 {
    var buffer: std.ArrayList(u8) = .empty;
    defer buffer.deinit(gpa);

    if (file_reader.getSize()) |size| {
        const casted_size = std.math.cast(u32, size) orelse return error.StreamTooLong;
        // +1 to avoid resizing for the null byte added in toOwnedSliceSentinel below.
        try buffer.ensureTotalCapacityPrecise(gpa, casted_size + 1);
    } else |_| {}

    try file_reader.interface.appendRemaining(gpa, &buffer, .limited(max_src_size));

    // Detect unsupported file types with their Byte Order Mark
    const unsupported_boms = [_][]const u8{
        "\xff\xfe\x00\x00", // UTF-32 little endian
        "\xfe\xff\x00\x00", // UTF-32 big endian
        "\xfe\xff", // UTF-16 big endian
    };
    for (unsupported_boms) |bom| {
        if (mem.startsWith(u8, buffer.items, bom)) {
            return error.UnsupportedEncoding;
        }
    }

    // If the file starts with a UTF-16 little endian BOM, translate it to UTF-8
    if (mem.startsWith(u8, buffer.items, "\xff\xfe")) {
        if (buffer.items.len % 2 != 0) return error.InvalidEncoding;
        return std.unicode.utf16LeToUtf8AllocZ(gpa, @ptrCast(@alignCast(buffer.items))) catch |err| switch (err) {
            error.DanglingSurrogateHalf => error.UnsupportedEncoding,
            error.ExpectedSecondSurrogateHalf => error.UnsupportedEncoding,
            error.UnexpectedSecondSurrogateHalf => error.UnsupportedEncoding,
            else => |e| return e,
        };
    }

    return buffer.toOwnedSliceSentinel(gpa, 0);
}

pub fn printAstErrorsToStderr(gpa: Allocator, io: Io, tree: Ast, path: []const u8, color: Color) !void {
    var wip_errors: std.zig.ErrorBundle.Wip = undefined;
    try wip_errors.init(gpa);
    defer wip_errors.deinit();

    try putAstErrorsIntoBundle(gpa, tree, path, &wip_errors);

    var error_bundle = try wip_errors.toOwnedBundle("");
    defer error_bundle.deinit(gpa);
    return error_bundle.renderToStderr(io, .{}, color);
}

pub fn putAstErrorsIntoBundle(
    gpa: Allocator,
    tree: Ast,
    path: []const u8,
    wip_errors: *std.zig.ErrorBundle.Wip,
) Allocator.Error!void {
    switch (tree.mode) {
        .zig => {
            var zir = try AstGen.generate(gpa, tree);
            defer zir.deinit(gpa);

            try wip_errors.addZirErrorMessages(zir, tree, tree.source, path);
        },
        .zon => {
            var zoir = try ZonGen.generate(gpa, tree, .{});
            defer zoir.deinit(gpa);

            try wip_errors.addZoirErrorMessages(zoir, tree, tree.source, path);
        },
    }
}

pub fn resolveTargetQueryOrFatal(io: Io, target_query: std.Target.Query) std.Target {
    return std.zig.system.resolveTargetQuery(io, target_query) catch |err|
        std.process.fatal("unable to resolve target: {s}", .{@errorName(err)});
}

pub fn parseTargetQueryOrReportFatalError(
    allocator: Allocator,
    opts: std.Target.Query.ParseOptions,
) std.Target.Query {
    var opts_with_diags = opts;
    var diags: std.Target.Query.ParseOptions.Diagnostics = .{};
    if (opts_with_diags.diagnostics == null) {
        opts_with_diags.diagnostics = &diags;
    }
    return std.Target.Query.parse(opts_with_diags) catch |err| switch (err) {
        error.UnknownCpuModel => {
            help: {
                var help_text = std.array_list.Managed(u8).init(allocator);
                defer help_text.deinit();
                for (diags.arch.?.allCpuModels()) |cpu| {
                    help_text.print(" {s}\n", .{cpu.name}) catch break :help;
                }
                std.log.info("available CPUs for architecture '{s}':\n{s}", .{
                    @tagName(diags.arch.?), help_text.items,
                });
            }
            std.process.fatal("unknown CPU: '{s}'", .{diags.cpu_name.?});
        },
        error.UnknownCpuFeature => {
            help: {
                var help_text = std.array_list.Managed(u8).init(allocator);
                defer help_text.deinit();
                for (diags.arch.?.allFeaturesList()) |feature| {
                    help_text.print(" {s}: {s}\n", .{ feature.name, feature.description }) catch break :help;
                }
                std.log.info("available CPU features for architecture '{s}':\n{s}", .{
                    @tagName(diags.arch.?), help_text.items,
                });
            }
            std.process.fatal("unknown CPU feature: '{s}'", .{diags.unknown_feature_name.?});
        },
        error.UnknownObjectFormat => {
            help: {
                var help_text = std.array_list.Managed(u8).init(allocator);
                defer help_text.deinit();
                inline for (@typeInfo(std.Target.ObjectFormat).@"enum".fields) |field| {
                    help_text.print(" {s}\n", .{field.name}) catch break :help;
                }
                std.log.info("available object formats:\n{s}", .{help_text.items});
            }
            std.process.fatal("unknown object format: '{s}'", .{opts.object_format.?});
        },
        error.UnknownArchitecture => {
            help: {
                var help_text = std.array_list.Managed(u8).init(allocator);
                defer help_text.deinit();
                inline for (@typeInfo(std.Target.Cpu.Arch).@"enum".fields) |field| {
                    help_text.print(" {s}\n", .{field.name}) catch break :help;
                }
                std.log.info("available architectures:\n{s} native\n", .{help_text.items});
            }
            std.process.fatal("unknown architecture: '{s}'", .{diags.unknown_architecture_name.?});
        },
        else => |e| std.process.fatal("unable to parse target query '{s}': {s}", .{
            opts.arch_os_abi, @errorName(e),
        }),
    };
}

/// Collects all the environment variables that Zig could possibly inspect, so
/// that we can do reflection on this and print them with `zig env`.
pub const EnvVar = enum {
    ZIG_GLOBAL_CACHE_DIR,
    ZIG_LOCAL_CACHE_DIR,
    ZIG_LIB_DIR,
    ZIG_LIBC,
    ZIG_BUILD_RUNNER,
    ZIG_BUILD_ERROR_STYLE,
    ZIG_BUILD_MULTILINE_ERRORS,
    ZIG_VERBOSE_LINK,
    ZIG_VERBOSE_CC,
    ZIG_DEBUG_CMD,
    ZIG_IS_DETECTING_LIBC_PATHS,
    ZIG_IS_TRYING_TO_NOT_CALL_ITSELF,

    // C toolchain integration
    NIX_CFLAGS_COMPILE,
    NIX_CFLAGS_LINK,
    NIX_LDFLAGS,
    C_INCLUDE_PATH,
    CPLUS_INCLUDE_PATH,
    LIBRARY_PATH,
    CC,

    // Terminal integration
    NO_COLOR,
    CLICOLOR_FORCE,

    // Debug info integration
    XDG_CACHE_HOME,
    LOCALAPPDATA,
    HOME,

    // Windows SDK integration
    PROGRAMDATA,

    // Homebrew integration
    HOMEBREW_PREFIX,

    pub fn isSet(ev: EnvVar, map: *const std.process.Environ.Map) bool {
        return map.contains(@tagName(ev));
    }

    pub fn get(ev: EnvVar, map: *const std.process.Environ.Map) ?[]const u8 {
        return map.get(@tagName(ev));
    }
};

pub const SimpleComptimeReason = enum(u32) {
    // Evaluating at comptime because a builtin operand must be comptime-known.
    // These messages all mention a specific builtin.
    operand_setEvalBranchQuota,
    operand_setFloatMode,
    operand_branchHint,
    operand_setRuntimeSafety,
    operand_embedFile,
    operand_cImport,
    operand_cDefine_macro_name,
    operand_cDefine_macro_value,
    operand_cInclude_file_name,
    operand_cUndef_macro_name,
    operand_shuffle_mask,
    operand_atomicRmw_operation,
    operand_reduce_operation,

    // Evaluating at comptime because an operand must be comptime-known.
    // These messages do not mention a specific builtin (and may not be about a builtin at all).
    export_target,
    export_options,
    extern_options,
    prefetch_options,
    call_modifier,
    compile_error_string,
    inline_assembly_code,
    atomic_order,
    array_mul_factor,
    slice_cat_operand,
    inline_call_target,
    generic_call_target,
    wasm_memory_index,
    work_group_dim_index,
    clobber,

    // Evaluating at comptime because types must be comptime-known.
    // Reasons other than `.type` are just more specific messages.
    type,
    int_signedness,
    int_bit_width,
    array_sentinel,
    array_length,
    pointer_size,
    pointer_attrs,
    pointer_sentinel,
    slice_sentinel,
    vector_length,
    fn_ret_ty,
    fn_param_types,
    fn_param_attrs,
    fn_attrs,
    struct_layout,
    struct_field_names,
    struct_field_types,
    struct_field_attrs,
    union_layout,
    union_field_names,
    union_field_types,
    union_field_attrs,
    tuple_field_types,
    enum_field_names,
    enum_field_values,
    union_enum_tag_type,
    enum_int_tag_type,
    packed_struct_backing_int_type,
    packed_union_backing_int_type,

    // Evaluating at comptime because decl/field name must be comptime-known.
    decl_name,
    field_name,
    tuple_field_index,

    // Evaluating at comptime because it is an attribute of a global declaration.
    container_var_init,
    @"callconv",
    @"align",
    @"addrspace",
    @"linksection",

    // Miscellaneous reasons.
    comptime_keyword,
    comptime_call_modifier,
    inline_loop_operand,
    switch_item,
    tuple_field_default_value,
    struct_field_default_value,
    enum_field_tag_value,
    slice_single_item_ptr_bounds,
    stored_to_comptime_field,
    stored_to_comptime_var,
    casted_to_comptime_enum,
    casted_to_comptime_int,
    casted_to_comptime_float,
    std_builtin_decl,

    pub fn message(r: SimpleComptimeReason) []const u8 {
        return switch (r) {
            // zig fmt: off
            .operand_setEvalBranchQuota  => "operand to '@setEvalBranchQuota' must be comptime-known",
            .operand_setFloatMode        => "operand to '@setFloatMode' must be comptime-known",
            .operand_branchHint          => "operand to '@branchHint' must be comptime-known",
            .operand_setRuntimeSafety    => "operand to '@setRuntimeSafety' must be comptime-known",
            .operand_embedFile           => "operand to '@embedFile' must be comptime-known",
            .operand_cImport             => "operand to '@cImport' is evaluated at comptime",
            .operand_cDefine_macro_name  => "'@cDefine' macro name must be comptime-known",
            .operand_cDefine_macro_value => "'@cDefine' macro value must be comptime-known",
            .operand_cInclude_file_name  => "'@cInclude' file name must be comptime-known",
            .operand_cUndef_macro_name   => "'@cUndef' macro name must be comptime-known",
            .operand_shuffle_mask        => "'@shuffle' mask must be comptime-known",
            .operand_atomicRmw_operation => "'@atomicRmw' operation must be comptime-known",
            .operand_reduce_operation    => "'@reduce' operation must be comptime-known",

            .export_target        => "export target must be comptime-known",
            .export_options       => "export options must be comptime-known",
            .extern_options       => "extern options must be comptime-known",
            .prefetch_options     => "prefetch options must be comptime-known",
            .call_modifier        => "call modifier must be comptime-known",
            .compile_error_string => "compile error string must be comptime-known",
            .inline_assembly_code => "inline assembly code must be comptime-known",
            .atomic_order         => "atomic order must be comptime-known",
            .array_mul_factor     => "array multiplication factor must be comptime-known",
            .slice_cat_operand    => "slice being concatenated must be comptime-known",
            .inline_call_target   => "function being called inline must be comptime-known",
            .generic_call_target  => "generic function being called must be comptime-known",
            .wasm_memory_index    => "wasm memory index must be comptime-known",
            .work_group_dim_index => "work group dimension index must be comptime-known",
            .clobber              => "clobber must be comptime-known",

            .type                => "types must be comptime-known",
            .int_signedness      => "integer signedness must be comptime-known",
            .int_bit_width       => "integer bit width must be comptime-known",
            .array_sentinel      => "array sentinel value must be comptime-known",
            .array_length        => "array length must be comptime-known",
            .pointer_size        => "pointer size must be comptime-known",
            .pointer_attrs       => "pointer attributes must be comptime-known",
            .pointer_sentinel    => "pointer sentinel value must be comptime-known",
            .slice_sentinel      => "slice sentinel value must be comptime-known",
            .vector_length       => "vector length must be comptime-known",
            .fn_ret_ty           => "function return type must be comptime-known",
            .fn_param_types      => "function parameter types must be comptime-known",
            .fn_param_attrs      => "function parameter attributes must be comptime-known",
            .fn_attrs            => "function attributes must be comptime-known",
            .struct_layout       => "struct layout must be comptime-known",
            .struct_field_names  => "struct field names must be comptime-known",
            .struct_field_types  => "struct field types must be comptime-known",
            .struct_field_attrs  => "struct field attributes must be comptime-known",
            .union_layout        => "union layout must be comptime-known",
            .union_field_names   => "union field names must be comptime-known",
            .union_field_types   => "union field types must be comptime-known",
            .union_field_attrs   => "union field attributes must be comptime-known",
            .tuple_field_types   => "tuple field types must be comptime-known",
            .enum_field_names    => "enum field names must be comptime-known",
            .enum_field_values   => "enum field values must be comptime-known",

            .union_enum_tag_type            => "enum tag type of union must be comptime-known",
            .enum_int_tag_type              => "integer tag type of enum must be comptime-known",
            .packed_struct_backing_int_type => "packed struct backing integer type must be comptime-known",
            .packed_union_backing_int_type  => "packed struct backing integer type must be comptime-known",

            .decl_name         => "declaration name must be comptime-known",
            .field_name        => "field name must be comptime-known",
            .tuple_field_index => "tuple field index must be comptime-known",

            .container_var_init => "initializer of container-level variable must be comptime-known",
            .@"callconv"        => "calling convention must be comptime-known",
            .@"align"           => "alignment must be comptime-known",
            .@"addrspace"       => "address space must be comptime-known",
            .@"linksection"     => "linksection must be comptime-known",

            .comptime_keyword             => "'comptime' keyword forces comptime evaluation",
            .comptime_call_modifier       => "'.compile_time' call modifier forces comptime evaluation",
            .inline_loop_operand          => "inline loop condition must be comptime-known",
            .switch_item                  => "switch prong values must be comptime-known",
            .tuple_field_default_value    => "tuple field default value must be comptime-known",
            .struct_field_default_value   => "struct field default value must be comptime-known",
            .enum_field_tag_value         => "enum field tag value must be comptime-known",
            .slice_single_item_ptr_bounds => "slice of single-item pointer must have comptime-known bounds",
            .stored_to_comptime_field     => "value stored to a comptime field must be comptime-known",
            .stored_to_comptime_var       => "value stored to a comptime variable must be comptime-known",
            .casted_to_comptime_enum      => "value casted to enum with 'comptime_int' tag type must be comptime-known",
            .casted_to_comptime_int       => "value casted to 'comptime_int' must be comptime-known",
            .casted_to_comptime_float     => "value casted to 'comptime_float' must be comptime-known",
            .std_builtin_decl             => "'std.builtin' declaration values must be comptime-known",
            // zig fmt: on
        };
    }
};

/// Every kind of artifact which the compiler can emit.
pub const EmitArtifact = enum {
    bin,
    @"asm",
    implib,
    llvm_ir,
    llvm_bc,
    docs,
    pdb,
    h,
    compiler_rt_dyn_lib,

    /// If using `Server` to communicate with the compiler, it will place requested artifacts in
    /// paths under the output directory, where those paths are named according to this function.
    /// Returned string is allocated with `gpa` and owned by the caller.
    pub fn cacheName(ea: EmitArtifact, gpa: Allocator, opts: BinNameOptions) Allocator.Error![]const u8 {
        // hack for stage2_x86_64 + coff. See Coff.flush.
        if (ea == .compiler_rt_dyn_lib) return "compiler_rt.dll";
        const suffix: []const u8 = switch (ea) {
            .bin => return binNameAlloc(gpa, opts),
            .@"asm" => ".s",
            .implib => ".lib",
            .llvm_ir => ".ll",
            .llvm_bc => ".bc",
            .docs => "-docs",
            .pdb => ".pdb",
            .h => ".h",
            .compiler_rt_dyn_lib => unreachable,
        };
        return std.fmt.allocPrint(gpa, "{s}{s}", .{ opts.root_name, suffix });
    }
};

/// The defaults are chosen here to reduce the size of src/clang_options.zon
pub const ClangCliParam = struct {
    name: []const u8,
    ze: ZigEquivalent = .other,
    syntax: Syntax = .flag,
    /// Prefixed by "-"
    pd1: bool = true,
    /// Prefixed by "--"
    pd2: bool = false,
    /// Prefixed by "/"
    psl: bool = false,

    pub const Syntax = union(enum) {
        /// A flag with no values.
        flag,
        /// An option which prefixes its (single) value.
        joined,
        /// An option which is followed by its value.
        separate,
        /// An option which is either joined to its (non-empty) value, or followed by its value.
        joined_or_separate,
        /// An option which is both joined to its (first) value, and followed by its (second) value.
        joined_and_separate,
        /// An option followed by its values, which are separated by commas.
        comma_joined,
        /// An option which consumes an optional joined argument and any other remaining arguments.
        remaining_args_joined,
        /// An option which is which takes multiple (separate) arguments.
        multi_arg: u8,
    };

    pub const ZigEquivalent = enum {
        target,
        o,
        c,
        r,
        m,
        x,
        other,
        positional,
        l,
        ignore,
        driver_punt,
        pic,
        no_pic,
        pie,
        no_pie,
        lto,
        no_lto,
        unwind_tables,
        no_unwind_tables,
        asynchronous_unwind_tables,
        no_asynchronous_unwind_tables,
        nostdlib,
        nostdlib_cpp,
        shared,
        rdynamic,
        wl,
        wp,
        preprocess_only,
        asm_only,
        optimize,
        debug,
        gdwarf32,
        gdwarf64,
        sanitize,
        no_sanitize,
        sanitize_trap,
        no_sanitize_trap,
        linker_script,
        dry_run,
        verbose,
        for_linker,
        linker_input_z,
        lib_dir,
        mcpu,
        dep_file,
        dep_file_to_stdout,
        framework_dir,
        framework,
        nostdlibinc,
        red_zone,
        no_red_zone,
        omit_frame_pointer,
        no_omit_frame_pointer,
        function_sections,
        no_function_sections,
        data_sections,
        no_data_sections,
        builtin,
        no_builtin,
        color_diagnostics,
        no_color_diagnostics,
        stack_check,
        no_stack_check,
        stack_protector,
        no_stack_protector,
        strip,
        exec_model,
        emit_llvm,
        sysroot,
        entry,
        force_undefined_symbol,
        weak_library,
        weak_framework,
        headerpad_max_install_names,
        compress_debug_sections,
        install_name,
        undefined,
        force_load_objc,
        mingw_unicode_entry_point,
        san_cov_trace_pc_guard,
        san_cov,
        no_san_cov,
        rtlib,
        static,
        dynamic,
        version,
    };

    pub fn matchEql(self: @This(), arg: []const u8) u2 {
        if (self.pd1 and arg.len >= self.name.len + 1 and
            mem.startsWith(u8, arg, "-") and mem.eql(u8, arg[1..], self.name))
        {
            return 1;
        }
        if (self.pd2 and arg.len >= self.name.len + 2 and
            mem.startsWith(u8, arg, "--") and mem.eql(u8, arg[2..], self.name))
        {
            return 2;
        }
        if (self.psl and arg.len >= self.name.len + 1 and
            mem.startsWith(u8, arg, "/") and mem.eql(u8, arg[1..], self.name))
        {
            return 1;
        }
        return 0;
    }

    pub fn matchStartsWith(self: @This(), arg: []const u8) usize {
        if (self.pd1 and arg.len >= self.name.len + 1 and
            mem.startsWith(u8, arg, "-") and mem.startsWith(u8, arg[1..], self.name))
        {
            return self.name.len + 1;
        }
        if (self.pd2 and arg.len >= self.name.len + 2 and
            mem.startsWith(u8, arg, "--") and mem.startsWith(u8, arg[2..], self.name))
        {
            return self.name.len + 2;
        }
        if (self.psl and arg.len >= self.name.len + 1 and
            mem.startsWith(u8, arg, "/") and mem.startsWith(u8, arg[1..], self.name))
        {
            return self.name.len + 1;
        }
        return 0;
    }
};

test {
    _ = Ast;
    _ = AstRlAnnotate;
    _ = AstSmith;
    _ = BuiltinFn;
    _ = Client;
    _ = ErrorBundle;
    _ = LibCDirs;
    _ = LibCInstallation;
    _ = Server;
    _ = TokenSmith;
    _ = WindowsSdk;
    _ = number_literal;
    _ = primitives;
    _ = string_literal;
    _ = system;
    _ = target;
    _ = c_translation;
    _ = llvm;
}



---
File: /std/zip.zig
---

//! The .ZIP File Format Specification is found here:
//!    https://pkwaredownloads.blob.core.windows.net/pem/APPNOTE.txt
//!
//! Note that this file uses the abbreviation "cd" for "central directory"

const builtin = @import("builtin");
const is_le = builtin.target.cpu.arch.endian() == .little;

const std = @import("std");
const Io = std.Io;
const File = std.Io.File;
const Writer = std.Io.Writer;
const Reader = std.Io.Reader;
const flate = std.compress.flate;

pub const CompressionMethod = enum(u16) {
    store = 0,
    deflate = 8,
    _,
};

pub const central_file_header_sig = [4]u8{ 'P', 'K', 1, 2 };
pub const local_file_header_sig = [4]u8{ 'P', 'K', 3, 4 };
pub const end_record_sig = [4]u8{ 'P', 'K', 5, 6 };
pub const end_record64_sig = [4]u8{ 'P', 'K', 6, 6 };
pub const end_locator64_sig = [4]u8{ 'P', 'K', 6, 7 };
pub const ExtraHeader = enum(u16) {
    zip64_info = 0x1,
    _,
};

const GeneralPurposeFlags = packed struct(u16) {
    encrypted: bool,
    _: u15,
};

pub const LocalFileHeader = extern struct {
    signature: [4]u8 align(1),
    version_needed_to_extract: u16 align(1),
    flags: GeneralPurposeFlags align(1),
    compression_method: CompressionMethod align(1),
    last_modification_time: u16 align(1),
    last_modification_date: u16 align(1),
    crc32: u32 align(1),
    compressed_size: u32 align(1),
    uncompressed_size: u32 align(1),
    filename_len: u16 align(1),
    extra_len: u16 align(1),
};

pub const CentralDirectoryFileHeader = extern struct {
    signature: [4]u8 align(1),
    version_made_by: u16 align(1),
    version_needed_to_extract: u16 align(1),
    flags: GeneralPurposeFlags align(1),
    compression_method: CompressionMethod align(1),
    last_modification_time: u16 align(1),
    last_modification_date: u16 align(1),
    crc32: u32 align(1),
    compressed_size: u32 align(1),
    uncompressed_size: u32 align(1),
    filename_len: u16 align(1),
    extra_len: u16 align(1),
    comment_len: u16 align(1),
    disk_number: u16 align(1),
    internal_file_attributes: u16 align(1),
    external_file_attributes: u32 align(1),
    local_file_header_offset: u32 align(1),
};

pub const EndRecord64 = extern struct {
    signature: [4]u8 align(1),
    end_record_size: u64 align(1),
    version_made_by: u16 align(1),
    version_needed_to_extract: u16 align(1),
    disk_number: u32 align(1),
    central_directory_disk_number: u32 align(1),
    record_count_disk: u64 align(1),
    record_count_total: u64 align(1),
    central_directory_size: u64 align(1),
    central_directory_offset: u64 align(1),
};

pub const EndLocator64 = extern struct {
    signature: [4]u8 align(1),
    zip64_disk_count: u32 align(1),
    record_file_offset: u64 align(1),
    total_disk_count: u32 align(1),
};

pub const EndRecord = extern struct {
    signature: [4]u8 align(1),
    disk_number: u16 align(1),
    central_directory_disk_number: u16 align(1),
    record_count_disk: u16 align(1),
    record_count_total: u16 align(1),
    central_directory_size: u32 align(1),
    central_directory_offset: u32 align(1),
    comment_len: u16 align(1),

    pub fn need_zip64(self: EndRecord) bool {
        return isMaxInt(self.record_count_disk) or
            isMaxInt(self.record_count_total) or
            isMaxInt(self.central_directory_size) or
            isMaxInt(self.central_directory_offset);
    }

    pub const FindBufferError = error{ ZipNoEndRecord, ZipTruncated };

    /// TODO audit this logic
    pub fn findBuffer(buffer: []const u8) FindBufferError!EndRecord {
        const pos = std.mem.lastIndexOf(u8, buffer, &end_record_sig) orelse return error.ZipNoEndRecord;
        if (pos + @sizeOf(EndRecord) > buffer.len) return error.EndOfStream;
        const record_ptr: *EndRecord = @ptrCast(buffer[pos..][0..@sizeOf(EndRecord)]);
        var record = record_ptr.*;
        if (!is_le) std.mem.byteSwapAllFields(EndRecord, &record);
        return record;
    }

    pub const FindFileError = File.Reader.SizeError || File.SeekError || File.Reader.Error || error{
        ZipNoEndRecord,
        EndOfStream,
        ReadFailed,
    };

    pub fn findFile(fr: *File.Reader) FindFileError!EndRecord {
        const end_pos = try fr.getSize();

        var buf: [@sizeOf(EndRecord) + std.math.maxInt(u16)]u8 = undefined;
        const record_len_max = @min(end_pos, buf.len);
        var loaded_len: u32 = 0;
        var comment_len: u16 = 0;
        while (true) {
            const record_len: u32 = @as(u32, comment_len) + @sizeOf(EndRecord);
            if (record_len > record_len_max)
                return error.ZipNoEndRecord;

            if (record_len > loaded_len) {
                const new_loaded_len = @min(loaded_len + 300, record_len_max);
                const read_len = new_loaded_len - loaded_len;

                try fr.seekTo(end_pos - @as(u64, new_loaded_len));
                const read_buf: []u8 = buf[buf.len - new_loaded_len ..][0..read_len];
                fr.interface.readSliceAll(read_buf) catch |err| switch (err) {
                    error.ReadFailed => return fr.err.?,
                    error.EndOfStream => return error.EndOfStream,
                };
                loaded_len = new_loaded_len;
            }

            const record_bytes = buf[buf.len - record_len ..][0..@sizeOf(EndRecord)];
            if (std.mem.eql(u8, record_bytes[0..4], &end_record_sig) and
                std.mem.readInt(u16, record_bytes[20..22], .little) == comment_len)
            {
                const record: *align(1) EndRecord = @ptrCast(record_bytes.ptr);
                if (!is_le) std.mem.byteSwapAllFields(EndRecord, record);
                return record.*;
            }

            if (comment_len == std.math.maxInt(u16))
                return error.ZipNoEndRecord;
            comment_len += 1;
        }
    }
};

pub const Decompress = struct {
    interface: Reader,
    state: union {
        inflate: flate.Decompress,
        store: *Reader,
    },

    pub fn init(reader: *Reader, method: CompressionMethod, buffer: []u8) Reader {
        return switch (method) {
            .store => .{
                .state = .{ .store = reader },
                .interface = .{
                    .context = undefined,
                    .vtable = &.{ .stream = streamStore },
                    .buffer = buffer,
                    .end = 0,
                    .seek = 0,
                },
            },
            .deflate => .{
                .state = .{ .inflate = .init(reader, .raw) },
                .interface = .{
                    .context = undefined,
                    .vtable = &.{ .stream = streamDeflate },
                    .buffer = buffer,
                    .end = 0,
                    .seek = 0,
                },
            },
            else => unreachable,
        };
    }

    fn streamStore(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
        const d: *Decompress = @fieldParentPtr("interface", r);
        return d.store.read(w, limit);
    }

    fn streamDeflate(r: *Reader, w: *Writer, limit: std.Io.Limit) Reader.StreamError!usize {
        const d: *Decompress = @fieldParentPtr("interface", r);
        return flate.Decompress.read(&d.inflate, w, limit);
    }
};

fn isBadFilename(filename: []const u8) bool {
    if (filename.len == 0 or filename[0] == '/')
        return true;

    var it = std.mem.splitScalar(u8, filename, '/');
    while (it.next()) |part| {
        if (std.mem.eql(u8, part, ".."))
            return true;
    }

    return false;
}

fn isMaxInt(uint: anytype) bool {
    return uint == std.math.maxInt(@TypeOf(uint));
}

const FileExtents = struct {
    uncompressed_size: u64,
    compressed_size: u64,
    local_file_header_offset: u64,
};

fn readZip64FileExtents(comptime T: type, header: T, extents: *FileExtents, data: []u8) !void {
    var data_offset: usize = 0;
    if (isMaxInt(header.uncompressed_size)) {
        if (data_offset + 8 > data.len)
            return error.ZipBadCd64Size;
        extents.uncompressed_size = std.mem.readInt(u64, data[data_offset..][0..8], .little);
        data_offset += 8;
    }
    if (isMaxInt(header.compressed_size)) {
        if (data_offset + 8 > data.len)
            return error.ZipBadCd64Size;
        extents.compressed_size = std.mem.readInt(u64, data[data_offset..][0..8], .little);
        data_offset += 8;
    }

    switch (T) {
        CentralDirectoryFileHeader => {
            if (isMaxInt(header.local_file_header_offset)) {
                if (data_offset + 8 > data.len)
                    return error.ZipBadCd64Size;
                extents.local_file_header_offset = std.mem.readInt(u64, data[data_offset..][0..8], .little);
                data_offset += 8;
            }
            if (isMaxInt(header.disk_number)) {
                if (data_offset + 4 > data.len)
                    return error.ZipInvalid;
                const disk_number = std.mem.readInt(u32, data[data_offset..][0..4], .little);
                if (disk_number != 0)
                    return error.ZipMultiDiskUnsupported;
                data_offset += 4;
            }
            if (data_offset > data.len)
                return error.ZipBadCd64Size;
        },
        else => {},
    }
}

pub const Iterator = struct {
    input: *File.Reader,

    cd_record_count: u64,
    cd_zip_offset: u64,
    cd_size: u64,

    cd_record_index: u64 = 0,
    cd_record_offset: u64 = 0,

    pub fn init(input: *File.Reader) !Iterator {
        const end_record = try EndRecord.findFile(input);

        if (!isMaxInt(end_record.record_count_disk) and end_record.record_count_disk > end_record.record_count_total)
            return error.ZipDiskRecordCountTooLarge;

        if (end_record.disk_number != 0 or end_record.central_directory_disk_number != 0)
            return error.ZipMultiDiskUnsupported;

        {
            const counts_valid = !isMaxInt(end_record.record_count_disk) and !isMaxInt(end_record.record_count_total);
            if (counts_valid and end_record.record_count_disk != end_record.record_count_total)
                return error.ZipMultiDiskUnsupported;
        }

        var result: Iterator = .{
            .input = input,
            .cd_record_count = end_record.record_count_total,
            .cd_zip_offset = end_record.central_directory_offset,
            .cd_size = end_record.central_directory_size,
        };
        if (!end_record.need_zip64()) return result;

        const locator_end_offset: u64 = @as(u64, end_record.comment_len) + @sizeOf(EndRecord) + @sizeOf(EndLocator64);
        const stream_len = try input.getSize();

        if (locator_end_offset > stream_len)
            return error.ZipTruncated;
        try input.seekTo(stream_len - locator_end_offset);
        const locator = input.interface.takeStruct(EndLocator64, .little) catch |err| switch (err) {
            error.ReadFailed => return input.err.?,
            error.EndOfStream => return error.EndOfStream,
        };
        if (!std.mem.eql(u8, &locator.signature, &end_locator64_sig))
            return error.ZipBadLocatorSig;
        if (locator.zip64_disk_count != 0)
            return error.ZipUnsupportedZip64DiskCount;
        if (locator.total_disk_count != 1)
            return error.ZipMultiDiskUnsupported;

        try input.seekTo(locator.record_file_offset);

        const record64 = input.interface.takeStruct(EndRecord64, .little) catch |err| switch (err) {
            error.ReadFailed => return input.err.?,
            error.EndOfStream => return error.EndOfStream,
        };

        if (!std.mem.eql(u8, &record64.signature, &end_record64_sig))
            return error.ZipBadEndRecord64Sig;

        if (record64.end_record_size < @sizeOf(EndRecord64) - 12)
            return error.ZipEndRecord64SizeTooSmall;
        if (record64.end_record_size > @sizeOf(EndRecord64) - 12)
            return error.ZipEndRecord64UnhandledExtraData;

        if (record64.version_needed_to_extract > 45)
            return error.ZipUnsupportedVersion;

        {
            const is_multidisk = record64.disk_number != 0 or
                record64.central_directory_disk_number != 0 or
                record64.record_count_disk != record64.record_count_total;
            if (is_multidisk)
                return error.ZipMultiDiskUnsupported;
        }

        if (isMaxInt(end_record.record_count_total)) {
            result.cd_record_count = record64.record_count_total;
        } else if (end_record.record_count_total != record64.record_count_total)
            return error.Zip64RecordCountTotalMismatch;

        if (isMaxInt(end_record.central_directory_offset)) {
            result.cd_zip_offset = record64.central_directory_offset;
        } else if (end_record.central_directory_offset != record64.central_directory_offset)
            return error.Zip64CentralDirectoryOffsetMismatch;

        if (isMaxInt(end_record.central_directory_size)) {
            result.cd_size = record64.central_directory_size;
        } else if (end_record.central_directory_size != record64.central_directory_size)
            return error.Zip64CentralDirectorySizeMismatch;

        return result;
    }

    pub fn next(self: *Iterator) !?Entry {
        if (self.cd_record_index == self.cd_record_count) {
            if (self.cd_record_offset != self.cd_size)
                return if (self.cd_size > self.cd_record_offset)
                    error.ZipCdOversized
                else
                    error.ZipCdUndersized;

            return null;
        }

        const header_zip_offset = self.cd_zip_offset + self.cd_record_offset;
        const input = self.input;
        try input.seekTo(header_zip_offset);
        const header = input.interface.takeStruct(CentralDirectoryFileHeader, .little) catch |err| switch (err) {
            error.ReadFailed => return input.err.?,
            error.EndOfStream => return error.EndOfStream,
        };
        if (!std.mem.eql(u8, &header.signature, &central_file_header_sig))
            return error.ZipBadCdOffset;

        self.cd_record_index += 1;
        self.cd_record_offset += @sizeOf(CentralDirectoryFileHeader) + header.filename_len + header.extra_len + header.comment_len;

        // Note: checking the version_needed_to_extract doesn't seem to be helpful, i.e. the zip file
        // at https://github.com/ninja-build/ninja/releases/download/v1.12.0/ninja-linux.zip
        // has an undocumented version 788 but extracts just fine.

        if (header.flags.encrypted)
            return error.ZipEncryptionUnsupported;
        // TODO: check/verify more flags
        if (header.disk_number != 0)
            return error.ZipMultiDiskUnsupported;

        var extents: FileExtents = .{
            .uncompressed_size = header.uncompressed_size,
            .compressed_size = header.compressed_size,
            .local_file_header_offset = header.local_file_header_offset,
        };

        if (header.extra_len > 0) {
            var extra_buf: [std.math.maxInt(u16)]u8 = undefined;
            const extra = extra_buf[0..header.extra_len];

            try input.seekTo(header_zip_offset + @sizeOf(CentralDirectoryFileHeader) + header.filename_len);
            input.interface.readSliceAll(extra) catch |err| switch (err) {
                error.ReadFailed => return input.err.?,
                error.EndOfStream => return error.EndOfStream,
            };

            var extra_offset: usize = 0;
            while (extra_offset + 4 <= extra.len) {
                const header_id = std.mem.readInt(u16, extra[extra_offset..][0..2], .little);
                const data_size = std.mem.readInt(u16, extra[extra_offset..][2..4], .little);
                const end = extra_offset + 4 + data_size;
                if (end > extra.len)
                    return error.ZipBadExtraFieldSize;
                const data = extra[extra_offset + 4 .. end];
                switch (@as(ExtraHeader, @enumFromInt(header_id))) {
                    .zip64_info => try readZip64FileExtents(CentralDirectoryFileHeader, header, &extents, data),
                    else => {}, // ignore
                }
                extra_offset = end;
            }
        }

        return .{
            .version_needed_to_extract = header.version_needed_to_extract,
            .flags = header.flags,
            .compression_method = header.compression_method,
            .last_modification_time = header.last_modification_time,
            .last_modification_date = header.last_modification_date,
            .header_zip_offset = header_zip_offset,
            .crc32 = header.crc32,
            .filename_len = header.filename_len,
            .compressed_size = extents.compressed_size,
            .uncompressed_size = extents.uncompressed_size,
            .file_offset = extents.local_file_header_offset,
        };
    }

    pub const Entry = struct {
        version_needed_to_extract: u16,
        flags: GeneralPurposeFlags,
        compression_method: CompressionMethod,
        last_modification_time: u16,
        last_modification_date: u16,
        header_zip_offset: u64,
        crc32: u32,
        filename_len: u32,
        compressed_size: u64,
        uncompressed_size: u64,
        file_offset: u64,

        pub fn extract(
            self: Entry,
            stream: *File.Reader,
            options: ExtractOptions,
            filename_buf: []u8,
            dest: Io.Dir,
        ) !void {
            const io = stream.io;

            if (filename_buf.len < self.filename_len)
                return error.ZipInsufficientBuffer;
            switch (self.compression_method) {
                .store, .deflate => {},
                else => return error.UnsupportedCompressionMethod,
            }
            const filename = filename_buf[0..self.filename_len];
            {
                try stream.seekTo(self.header_zip_offset + @sizeOf(CentralDirectoryFileHeader));
                try stream.interface.readSliceAll(filename);
            }

            const local_data_header_offset: u64 = local_data_header_offset: {
                const local_header = blk: {
                    try stream.seekTo(self.file_offset);
                    break :blk try stream.interface.takeStruct(LocalFileHeader, .little);
                };
                if (!std.mem.eql(u8, &local_header.signature, &local_file_header_sig))
                    return error.ZipBadFileOffset;
                if (local_header.version_needed_to_extract != self.version_needed_to_extract)
                    return error.ZipMismatchVersionNeeded;
                if (local_header.last_modification_time != self.last_modification_time)
                    return error.ZipMismatchModTime;
                if (local_header.last_modification_date != self.last_modification_date)
                    return error.ZipMismatchModDate;

                if (@as(u16, @bitCast(local_header.flags)) != @as(u16, @bitCast(self.flags)))
                    return error.ZipMismatchFlags;
                if (local_header.crc32 != 0 and local_header.crc32 != self.crc32)
                    return error.ZipMismatchCrc32;
                var extents: FileExtents = .{
                    .uncompressed_size = local_header.uncompressed_size,
                    .compressed_size = local_header.compressed_size,
                    .local_file_header_offset = 0,
                };
                if (local_header.extra_len > 0) {
                    var extra_buf: [std.math.maxInt(u16)]u8 = undefined;
                    const extra = extra_buf[0..local_header.extra_len];

                    {
                        try stream.seekTo(self.file_offset + @sizeOf(LocalFileHeader) + local_header.filename_len);
                        try stream.interface.readSliceAll(extra);
                    }

                    var extra_offset: usize = 0;
                    while (extra_offset + 4 <= local_header.extra_len) {
                        const header_id = std.mem.readInt(u16, extra[extra_offset..][0..2], .little);
                        const data_size = std.mem.readInt(u16, extra[extra_offset..][2..4], .little);
                        const end = extra_offset + 4 + data_size;
                        if (end > local_header.extra_len)
                            return error.ZipBadExtraFieldSize;
                        const data = extra[extra_offset + 4 .. end];
                        switch (@as(ExtraHeader, @enumFromInt(header_id))) {
                            .zip64_info => try readZip64FileExtents(LocalFileHeader, local_header, &extents, data),
                            else => {}, // ignore
                        }
                        extra_offset = end;
                    }
                }

                if (extents.compressed_size != 0 and
                    extents.compressed_size != self.compressed_size)
                    return error.ZipMismatchCompLen;
                if (extents.uncompressed_size != 0 and
                    extents.uncompressed_size != self.uncompressed_size)
                    return error.ZipMismatchUncompLen;

                if (local_header.filename_len != self.filename_len)
                    return error.ZipMismatchFilenameLen;

                break :local_data_header_offset @as(u64, local_header.filename_len) +
                    @as(u64, local_header.extra_len);
            };

            if (options.allow_backslashes) {
                std.mem.replaceScalar(u8, filename, '\\', '/');
            } else {
                if (std.mem.findScalar(u8, filename, '\\')) |_|
                    return error.ZipFilenameHasBackslash;
            }

            if (isBadFilename(filename))
                return error.ZipBadFilename;

            // All entries that end in '/' are directories
            if (filename[filename.len - 1] == '/') {
                if (self.uncompressed_size != 0)
                    return error.ZipBadDirectorySize;
                try dest.createDirPath(io, filename[0 .. filename.len - 1]);
                return;
            }

            const out_file = blk: {
                if (std.fs.path.dirname(filename)) |dirname| {
                    var parent_dir = try dest.createDirPathOpen(io, dirname, .{});
                    defer parent_dir.close(io);

                    const basename = std.fs.path.basename(filename);
                    break :blk try parent_dir.createFile(io, basename, .{ .exclusive = true });
                }
                break :blk try dest.createFile(io, filename, .{ .exclusive = true });
            };
            defer out_file.close(io);
            var out_file_buffer: [1024]u8 = undefined;
            var file_writer = out_file.writer(io, &out_file_buffer);
            const local_data_file_offset: u64 =
                @as(u64, self.file_offset) +
                @as(u64, @sizeOf(LocalFileHeader)) +
                local_data_header_offset;
            try stream.seekTo(local_data_file_offset);

            // TODO limit based on self.compressed_size

            switch (self.compression_method) {
                .store => {
                    stream.interface.streamExact64(&file_writer.interface, self.uncompressed_size) catch |err| switch (err) {
                        error.ReadFailed => return stream.err.?,
                        error.WriteFailed => return file_writer.err.?,
                        error.EndOfStream => return error.ZipDecompressTruncated,
                    };
                },
                .deflate => {
                    var flate_buffer: [flate.max_window_len]u8 = undefined;
                    var decompress: flate.Decompress = .init(&stream.interface, .raw, &flate_buffer);
                    decompress.reader.streamExact64(&file_writer.interface, self.uncompressed_size) catch |err| switch (err) {
                        error.ReadFailed => return stream.err.?,
                        error.WriteFailed => return file_writer.err orelse decompress.err.?,
                        error.EndOfStream => return error.ZipDecompressTruncated,
                    };
                },
                else => return error.UnsupportedCompressionMethod,
            }
            try file_writer.end();
        }
    };
};

// returns true if `filename` starts with `root` followed by a forward slash
fn filenameInRoot(filename: []const u8, root: []const u8) bool {
    return (filename.len >= root.len + 1) and
        (filename[root.len] == '/') and
        std.mem.eql(u8, filename[0..root.len], root);
}

pub const Diagnostics = struct {
    allocator: std.mem.Allocator,

    /// The common root directory for all extracted files if there is one.
    root_dir: []const u8 = "",

    saw_first_file: bool = false,

    pub fn deinit(self: *Diagnostics) void {
        self.allocator.free(self.root_dir);
        self.* = undefined;
    }

    // This function assumes name is a filename from a zip file which has already been verified to
    // not start with a slash, backslashes have been normalized to forward slashes, and directories
    // always end in a slash.
    pub fn nextFilename(self: *Diagnostics, name: []const u8) error{OutOfMemory}!void {
        if (!self.saw_first_file) {
            self.saw_first_file = true;
            std.debug.assert(self.root_dir.len == 0);
            const root_len = std.mem.findScalar(u8, name, '/') orelse return;
            std.debug.assert(root_len > 0);
            self.root_dir = try self.allocator.dupe(u8, name[0..root_len]);
        } else if (self.root_dir.len > 0) {
            if (!filenameInRoot(name, self.root_dir)) {
                self.allocator.free(self.root_dir);
                self.root_dir = "";
            }
        }
    }
};

pub const ExtractOptions = struct {
    /// Allow filenames within the zip to use backslashes.  Back slashes are normalized
    /// to forward slashes before forwarding them to platform APIs.
    allow_backslashes: bool = false,
    diagnostics: ?*Diagnostics = null,
    verify_checksums: bool = false,
};

/// Extract the zipped files to the given `dest` directory.
pub fn extract(dest: Io.Dir, fr: *File.Reader, options: ExtractOptions) !void {
    if (options.verify_checksums) @panic("TODO unimplemented");

    var iter = try Iterator.init(fr);

    var filename_buf: [std.fs.max_path_bytes]u8 = undefined;
    while (try iter.next()) |entry| {
        try entry.extract(fr, options, &filename_buf, dest);
        if (options.diagnostics) |d| {
            try d.nextFilename(filename_buf[0..entry.filename_len]);
        }
    }
}



---
File: /std/zon.zig
---

//! ZON parsing and stringification.
//!
//! ZON ("Zig Object Notation") is a textual file format. Outside of `nan` and `inf` literals, ZON's
//! grammar is a subset of Zig's.
//!
//! Supported Zig primitives:
//! * boolean literals
//! * number literals (including `nan` and `inf`)
//! * character literals
//! * enum literals
//! * `null` literals
//! * string literals
//! * multiline string literals
//!
//! Supported Zig container types:
//! * anonymous struct literals
//! * anonymous tuple literals
//!
//! Here is an example ZON object:
//! ```
//! .{
//!     .a = 1.5,
//!     .b = "hello, world!",
//!     .c = .{ true, false },
//!     .d = .{ 1, 2, 3 },
//!     .e = .{ .x = 13, .y = 67 },
//! }
//! ```
//!
//! Individual primitives are also valid ZON, for example:
//! ```
//! "This string is a valid ZON object."
//! ```
//!
//! ZON may not contain type names.
//!
//! ZON does not have syntax for pointers, but the parsers will allocate as needed to match the
//! given Zig types. Similarly, the serializer will traverse pointers.

pub const parse = @import("zon/parse.zig");
pub const stringify = @import("zon/stringify.zig");
pub const Serializer = @import("zon/Serializer.zig");

test {
    _ = parse;
    _ = stringify;
}


```
