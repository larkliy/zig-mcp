```
      else if (@intFromEnum(self) < first_metadata)
            .{ .constant = @enumFromInt(@intFromEnum(self) - first_constant) }
        else
            .{ .metadata = @bitCast(@intFromEnum(self) - first_metadata) };
    }

    pub fn typeOfWip(self: Value, wip: *const WipFunction) Type {
        return switch (self.unwrap()) {
            .instruction => |instruction| instruction.typeOfWip(wip),
            .constant => |constant| constant.typeOf(wip.builder),
            .metadata => .metadata,
        };
    }

    pub fn typeOf(self: Value, function: Function.Index, builder: *Builder) Type {
        return switch (self.unwrap()) {
            .instruction => |instruction| instruction.typeOf(function, builder),
            .constant => |constant| constant.typeOf(builder),
            .metadata => .metadata,
        };
    }

    pub fn toConst(self: Value) ?Constant {
        return switch (self.unwrap()) {
            .instruction, .metadata => null,
            .constant => |constant| constant,
        };
    }

    const FormatData = struct {
        value: Value,
        function: Function.Index,
        builder: *Builder,
        flags: FormatFlags,
    };
    fn format(data: FormatData, w: *Writer) Writer.Error!void {
        switch (data.value.unwrap()) {
            .instruction => |instruction| try Function.Instruction.Index.format(.{
                .instruction = instruction,
                .function = data.function,
                .builder = data.builder,
                .flags = data.flags,
            }, w),
            .constant => |constant| try Constant.format(.{
                .constant = constant,
                .builder = data.builder,
                .flags = data.flags,
            }, w),
            .metadata => unreachable,
        }
    }
    pub fn fmt(self: Value, function: Function.Index, builder: *Builder, flags: FormatFlags) std.fmt.Alt(FormatData, format) {
        return .{ .data = .{ .value = self, .function = function, .builder = builder, .flags = flags } };
    }
};

pub const Metadata = packed struct(u32) {
    index: u29,
    kind: Kind,
    unused: enum(u1) { unused = 0 } = .unused,

    pub const Kind = enum(u2) {
        string,
        node,
        forward,
        local,
    };

    pub const empty_tuple: Metadata = .{ .kind = .node, .index = 0 };

    pub const Optional = packed struct(u32) {
        index: u29,
        kind: Metadata.Kind,
        is_none: bool,

        pub const none: Metadata.Optional = .{ .index = 0, .kind = .string, .is_none = true };
        pub const empty_tuple: Metadata.Optional = Metadata.empty_tuple.toOptional();

        pub fn wrap(metadata: ?Metadata) Metadata.Optional {
            return (metadata orelse return .none).toOptional();
        }
        pub fn unwrap(metadata: Metadata.Optional) ?Metadata {
            return if (metadata.is_none) null else .{ .index = metadata.index, .kind = metadata.kind };
        }
        pub fn toValue(metadata: Metadata.Optional) Value {
            return if (metadata.unwrap()) |m| m.toValue() else .none;
        }
        pub fn toString(metadata: Metadata.Optional) Metadata.String.Optional {
            return if (metadata.unwrap()) |m| m.toString().toOptional() else .none;
        }
    };
    pub fn toOptional(metadata: Metadata) Metadata.Optional {
        return .{ .index = metadata.index, .kind = metadata.kind, .is_none = false };
    }
    pub fn toValue(metadata: Metadata) Value {
        return @enumFromInt(Value.first_metadata + @as(u32, @bitCast(metadata)));
    }

    pub const String = enum(u32) {
        _,

        pub const Optional = enum(u32) {
            none = @bitCast(Metadata.Optional.none),
            _,

            pub fn wrap(metadata: ?Metadata.String) Metadata.String.Optional {
                return (metadata orelse return .none).toOptional();
            }
            pub fn unwrap(metadata: Metadata.String.Optional) ?Metadata.String {
                return switch (metadata) {
                    .none => null,
                    else => @enumFromInt(@intFromEnum(metadata)),
                };
            }
            pub fn toMetadata(metadata: Metadata.String.Optional) Metadata.Optional {
                return if (metadata.unwrap()) |m| m.toMetadata().toOptional() else .none;
            }
        };
        pub fn toOptional(metadata: Metadata.String) Metadata.String.Optional {
            return @enumFromInt(@intFromEnum(metadata));
        }
        pub fn toMetadata(metadata: Metadata.String) Metadata {
            return .{ .index = @intCast(@intFromEnum(metadata)), .kind = .string };
        }

        pub fn slice(metadata: Metadata.String, builder: *const Builder) []const u8 {
            const index = @intFromEnum(metadata);
            const start = builder.metadata_string_indices.items[index];
            const end = builder.metadata_string_indices.items[index + 1];
            return builder.metadata_string_bytes.items[start..end];
        }

        const Adapter = struct {
            builder: *const Builder,
            pub fn hash(_: Adapter, key: []const u8) u32 {
                return @truncate(std.hash.Wyhash.hash(0, key));
            }
            pub fn eql(ctx: Adapter, lhs_key: []const u8, _: void, rhs_index: usize) bool {
                const rhs_metadata: Metadata.String = @enumFromInt(rhs_index);
                return std.mem.eql(u8, lhs_key, rhs_metadata.slice(ctx.builder));
            }
        };

        const FormatData = struct {
            metadata: Metadata.String,
            builder: *const Builder,
        };
        fn format(data: FormatData, w: *Writer) Writer.Error!void {
            try printEscapedString(data.metadata.slice(data.builder), .always_quote, w);
        }
        fn fmt(self: Metadata.String, builder: *const Builder) std.fmt.Alt(FormatData, format) {
            return .{ .data = .{ .metadata = self, .builder = builder } };
        }
    };
    pub fn toString(metadata: Metadata) Metadata.String {
        assert(metadata.kind == .string);
        return @enumFromInt(metadata.index);
    }

    pub const Tag = enum(u6) {
        file,
        compile_unit,
        @"compile_unit optimized",
        subprogram,
        @"subprogram local",
        @"subprogram definition",
        @"subprogram local definition",
        @"subprogram optimized",
        @"subprogram optimized local",
        @"subprogram optimized definition",
        @"subprogram optimized local definition",
        lexical_block,
        location,
        basic_bool_type,
        basic_unsigned_type,
        basic_signed_type,
        basic_float_type,
        composite_struct_type,
        composite_union_type,
        composite_enumeration_type,
        composite_array_type,
        composite_vector_type,
        derived_pointer_type,
        derived_member_type,
        derived_typedef_type,
        subroutine_type,
        enumerator_unsigned,
        enumerator_signed_positive,
        enumerator_signed_negative,
        subrange,
        tuple,
        expression,
        local_var,
        parameter,
        global_var,
        @"global_var local",
        global_var_expression,
        constant,

        pub fn isInline(metadata_tag: Metadata.Tag) bool {
            return switch (metadata_tag) {
                .expression,
                .constant,
                => true,
                .file,
                .compile_unit,
                .@"compile_unit optimized",
                .subprogram,
                .@"subprogram local",
                .@"subprogram definition",
                .@"subprogram local definition",
                .@"subprogram optimized",
                .@"subprogram optimized local",
                .@"subprogram optimized definition",
                .@"subprogram optimized local definition",
                .lexical_block,
                .location,
                .basic_bool_type,
                .basic_unsigned_type,
                .basic_signed_type,
                .basic_float_type,
                .composite_struct_type,
                .composite_union_type,
                .composite_enumeration_type,
                .composite_array_type,
                .composite_vector_type,
                .derived_pointer_type,
                .derived_member_type,
                .derived_typedef_type,
                .subroutine_type,
                .enumerator_unsigned,
                .enumerator_signed_positive,
                .enumerator_signed_negative,
                .subrange,
                .tuple,
                .local_var,
                .parameter,
                .global_var,
                .@"global_var local",
                .global_var_expression,
                => false,
            };
        }
    };

    pub fn tag(metadata: Metadata, builder: *const Builder) Tag {
        assert(metadata.kind == .node);
        return builder.metadata_items.items(.tag)[metadata.index];
    }

    pub fn item(metadata: Metadata, builder: *const Builder) Item {
        assert(metadata.kind == .node);
        return builder.metadata_items.get(metadata.index);
    }

    pub fn isInline(metadata: Metadata, builder: *const Builder) bool {
        return metadata.tag(builder).isInline();
    }

    pub fn unwrap(metadata: Metadata, builder: *const Builder) Metadata {
        switch (metadata.kind) {
            .string, .node, .local => return metadata,
            .forward => {
                const referenced = builder.metadata_forward_references.items[metadata.index].unwrap().?;
                switch (referenced.kind) {
                    .string, .node => return referenced,
                    .forward, .local => unreachable,
                }
            },
        }
    }

    pub const Item = struct {
        tag: Tag,
        data: ExtraIndex,

        const ExtraIndex = u32;
    };

    pub const DIFlags = packed struct(u32) {
        Visibility: enum(u2) { Zero, Private, Protected, Public } = .Zero,
        FwdDecl: bool = false,
        AppleBlock: bool = false,
        ReservedBit4: u1 = 0,
        Virtual: bool = false,
        Artificial: bool = false,
        Explicit: bool = false,
        Prototyped: bool = false,
        ObjcClassComplete: bool = false,
        ObjectPointer: bool = false,
        Vector: bool = false,
        StaticMember: bool = false,
        LValueReference: bool = false,
        RValueReference: bool = false,
        ExportSymbols: bool = false,
        Inheritance: enum(u2) {
            Zero,
            SingleInheritance,
            MultipleInheritance,
            VirtualInheritance,
        } = .Zero,
        IntroducedVirtual: bool = false,
        BitField: bool = false,
        NoReturn: bool = false,
        ReservedBit21: u1 = 0,
        TypePassbyValue: bool = false,
        TypePassbyReference: bool = false,
        EnumClass: bool = false,
        Thunk: bool = false,
        NonTrivial: bool = false,
        BigEndian: bool = false,
        LittleEndian: bool = false,
        AllCallsDescribed: bool = false,
        Unused: u2 = 0,

        pub fn format(self: DIFlags, w: *Writer) Writer.Error!void {
            var need_pipe = false;
            inline for (@typeInfo(DIFlags).@"struct".fields) |field| {
                switch (@typeInfo(field.type)) {
                    .bool => if (@field(self, field.name)) {
                        if (need_pipe) try w.writeAll(" | ") else need_pipe = true;
                        try w.print("DIFlag{s}", .{field.name});
                    },
                    .@"enum" => if (@field(self, field.name) != .Zero) {
                        if (need_pipe) try w.writeAll(" | ") else need_pipe = true;
                        try w.print("DIFlag{s}", .{@tagName(@field(self, field.name))});
                    },
                    .int => assert(@field(self, field.name) == 0),
                    else => @compileError("bad field type: " ++ field.name ++ ": " ++
                        @typeName(field.type)),
                }
            }
            if (!need_pipe) try w.writeByte('0');
        }
    };

    pub const File = struct {
        filename: Metadata.String.Optional,
        directory: Metadata.String.Optional,
    };

    pub const CompileUnit = struct {
        pub const Options = struct {
            optimized: bool,
        };

        file: Metadata.Optional,
        producer: Metadata.String.Optional,
        enums: Metadata.Optional,
        globals: Metadata.Optional,
    };

    pub const Subprogram = struct {
        pub const Options = struct {
            di_flags: DIFlags,
            sp_flags: DISPFlags,
        };

        pub const DISPFlags = packed struct(u32) {
            Virtuality: enum(u2) { Zero, Virtual, PureVirtual } = .Zero,
            LocalToUnit: bool = false,
            Definition: bool = false,
            Optimized: bool = false,
            Pure: bool = false,
            Elemental: bool = false,
            Recursive: bool = false,
            MainSubprogram: bool = false,
            Deleted: bool = false,
            ReservedBit10: u1 = 0,
            ObjCDirect: bool = false,
            Unused: u20 = 0,

            pub fn format(self: DISPFlags, w: *Writer) Writer.Error!void {
                var need_pipe = false;
                inline for (@typeInfo(DISPFlags).@"struct".fields) |field| {
                    switch (@typeInfo(field.type)) {
                        .bool => if (@field(self, field.name)) {
                            if (need_pipe) try w.writeAll(" | ") else need_pipe = true;
                            try w.print("DISPFlag{s}", .{field.name});
                        },
                        .@"enum" => if (@field(self, field.name) != .Zero) {
                            if (need_pipe) try w.writeAll(" | ") else need_pipe = true;
                            try w.print("DISPFlag{s}", .{@tagName(@field(self, field.name))});
                        },
                        .int => assert(@field(self, field.name) == 0),
                        else => @compileError("bad field type: " ++ field.name ++ ": " ++
                            @typeName(field.type)),
                    }
                }
                if (!need_pipe) try w.writeByte('0');
            }
        };

        file: Metadata.Optional,
        name: Metadata.String.Optional,
        linkage_name: Metadata.String.Optional,
        line: u32,
        scope_line: u32,
        ty: Metadata.Optional,
        di_flags: DIFlags,
        compile_unit: Metadata.Optional,
    };
    pub fn getSubprogram(metadata: Metadata, builder: *const Builder) Subprogram {
        const metadata_item = metadata.item(builder);
        switch (metadata_item.tag) {
            else => unreachable,
            .subprogram,
            .@"subprogram local",
            .@"subprogram definition",
            .@"subprogram local definition",
            .@"subprogram optimized",
            .@"subprogram optimized local",
            .@"subprogram optimized definition",
            .@"subprogram optimized local definition",
            => return builder.metadataExtraData(Metadata.Subprogram, metadata_item.data),
        }
    }

    pub const LexicalBlock = struct {
        scope: Metadata.Optional,
        file: Metadata.Optional,
        line: u32,
        column: u32,
    };

    pub const Location = struct {
        line: u32,
        column: u32,
        scope: Metadata,
        inlined_at: Metadata.Optional,
    };

    pub const BasicType = struct {
        name: Metadata.String.Optional,
        size_in_bits_lo: u32,
        size_in_bits_hi: u32,

        pub fn bitSize(self: BasicType) u64 {
            return @as(u64, self.size_in_bits_hi) << 32 | self.size_in_bits_lo;
        }
    };

    pub const CompositeType = struct {
        name: Metadata.String.Optional,
        file: Metadata.Optional,
        scope: Metadata.Optional,
        line: u32,
        underlying_type: Metadata.Optional,
        size_in_bits_lo: u32,
        size_in_bits_hi: u32,
        align_in_bits_lo: u32,
        align_in_bits_hi: u32,
        fields_tuple: Metadata.Optional,

        pub fn bitSize(self: CompositeType) u64 {
            return @as(u64, self.size_in_bits_hi) << 32 | self.size_in_bits_lo;
        }
        pub fn bitAlign(self: CompositeType) u64 {
            return @as(u64, self.align_in_bits_hi) << 32 | self.align_in_bits_lo;
        }
    };

    pub const DerivedType = struct {
        name: Metadata.String.Optional,
        file: Metadata.Optional,
        scope: Metadata.Optional,
        line: u32,
        underlying_type: Metadata.Optional,
        size_in_bits_lo: u32,
        size_in_bits_hi: u32,
        align_in_bits_lo: u32,
        align_in_bits_hi: u32,
        offset_in_bits_lo: u32,
        offset_in_bits_hi: u32,

        pub fn bitSize(self: DerivedType) u64 {
            return @as(u64, self.size_in_bits_hi) << 32 | self.size_in_bits_lo;
        }
        pub fn bitAlign(self: DerivedType) u64 {
            return @as(u64, self.align_in_bits_hi) << 32 | self.align_in_bits_lo;
        }
        pub fn bitOffset(self: DerivedType) u64 {
            return @as(u64, self.offset_in_bits_hi) << 32 | self.offset_in_bits_lo;
        }
    };

    pub const SubroutineType = struct {
        types_tuple: Metadata.Optional,
    };

    pub const Enumerator = struct {
        name: Metadata.String.Optional,
        bit_width: u32,
        limbs_index: u32,
        limbs_len: u32,
    };

    pub const Subrange = struct {
        lower_bound: Metadata.Optional,
        count: Metadata.Optional,
    };

    pub const Expression = struct {
        elements_len: u32,

        // elements: [elements_len]u32
    };

    pub const Tuple = struct {
        elements_len: u32,

        // elements: [elements_len]Metadata
    };

    pub const LocalVar = struct {
        name: Metadata.String.Optional,
        file: Metadata.Optional,
        scope: Metadata.Optional,
        line: u32,
        ty: Metadata.Optional,
    };

    pub const Parameter = struct {
        name: Metadata.String.Optional,
        file: Metadata.Optional,
        scope: Metadata.Optional,
        line: u32,
        ty: Metadata.Optional,
        arg_no: u32,
    };

    pub const GlobalVar = struct {
        pub const Options = struct {
            local: bool,
        };

        name: Metadata.String.Optional,
        linkage_name: Metadata.String.Optional,
        file: Metadata.Optional,
        scope: Metadata.Optional,
        line: u32,
        ty: Metadata.Optional,
        variable: Variable.Index,
    };

    pub const GlobalVarExpression = struct {
        variable: Metadata.Optional,
        expression: Metadata.Optional,
    };

    const Formatter = struct {
        builder: *Builder,
        need_comma: bool,
        map: std.AutoArrayHashMapUnmanaged(union(enum) {
            metadata: Metadata,
            debug_location: DebugLocation.Location,
        }, void) = .empty,

        const FormatData = struct {
            formatter: *Formatter,
            prefix: []const u8 = "",
            node: Node,
            specialized: ?FormatFlags,

            const Node = union(enum) {
                none,
                @"inline": Metadata,
                index: u32,

                local_value: ValueData,
                local_metadata: ValueData,
                local_inline: Metadata,
                local_index: u32,

                string: Metadata.String,
                bool: bool,
                u32: u32,
                u64: u64,
                di_flags: DIFlags,
                sp_flags: Subprogram.DISPFlags,
                raw: []const u8,

                const ValueData = struct {
                    value: Value,
                    function: Function.Index,
                };
            };
        };
        fn format(data: FormatData, w: *Writer) Writer.Error!void {
            if (data.node == .none) return;

            const is_specialized = data.specialized != null;

            if (data.formatter.need_comma) try w.writeAll(", ");
            defer data.formatter.need_comma = true;
            try w.writeAll(data.prefix);

            const builder = data.formatter.builder;
            switch (data.node) {
                .none => unreachable,
                .@"inline" => |node| {
                    const needed_comma = data.formatter.need_comma;
                    defer data.formatter.need_comma = needed_comma;
                    data.formatter.need_comma = false;

                    const node_item = node.item(builder);
                    switch (node_item.tag) {
                        .expression => {
                            var extra = builder.metadataExtraDataTrail(Expression, node_item.data);
                            const elements = extra.trail.next(extra.data.elements_len, u32, builder);
                            try w.writeAll("!DIExpression(");
                            for (elements) |element| try format(.{
                                .formatter = data.formatter,
                                .node = .{ .u64 = element },
                                .specialized = .{ .percent = true },
                            }, w);
                            try w.writeByte(')');
                        },
                        .constant => try Constant.format(.{
                            .constant = @enumFromInt(node_item.data),
                            .builder = builder,
                            .flags = data.specialized orelse .{},
                        }, w),
                        else => unreachable,
                    }
                },
                .index => |node| try w.print("!{d}", .{node}),
                inline .local_value, .local_metadata => |node, node_tag| try Value.format(.{
                    .value = node.value,
                    .function = node.function,
                    .builder = builder,
                    .flags = switch (node_tag) {
                        .local_value => data.specialized orelse .{},
                        .local_metadata => .{ .percent = true },
                        else => unreachable,
                    },
                }, w),
                inline .local_inline, .local_index => |node, node_tag| {
                    if (data.specialized) |flags| {
                        if (flags.onlyPercent()) {
                            try w.print("{f} ", .{Type.metadata.fmt(builder, .percent)});
                        }
                    }
                    try format(.{
                        .formatter = data.formatter,
                        .node = @unionInit(FormatData.Node, @tagName(node_tag)["local_".len..], node),
                        .specialized = .{ .percent = true },
                    }, w);
                },
                .string => |s| {
                    if (is_specialized) try w.writeByte('!');
                    try w.print("{f}", .{s.fmt(builder)});
                },
                inline .bool, .u32, .u64 => |node| try w.print("{}", .{node}),
                inline .di_flags, .sp_flags => |node| try w.print("{f}", .{node}),
                .raw => |node| try w.writeAll(node),
            }
        }
        inline fn fmt(formatter: *Formatter, prefix: []const u8, node: anytype, special: ?FormatFlags) switch (@TypeOf(node)) {
            Metadata, Metadata.Optional, ?Metadata => Allocator.Error,
            else => error{},
        }!std.fmt.Alt(FormatData, format) {
            const Node = @TypeOf(node);
            const MaybeNode = switch (Node) {
                Metadata.Optional => ?Metadata,
                Metadata.String.Optional => ?Metadata.String,
                else => switch (@typeInfo(Node)) {
                    .optional => Node,
                    .null => ?noreturn,
                    else => ?Node,
                },
            };
            const Some = @typeInfo(MaybeNode).optional.child;
            return .{ .data = .{
                .formatter = formatter,
                .prefix = prefix,
                .node = if (@as(MaybeNode, switch (Node) {
                    Metadata.Optional, Metadata.String.Optional => node.unwrap(),
                    else => node,
                })) |some| switch (@typeInfo(Some)) {
                    .@"enum" => |enum_info| switch (Some) {
                        Metadata.String => .{ .string = some },
                        else => if (enum_info.is_exhaustive)
                            .{ .raw = @tagName(some) }
                        else
                            @compileError("unknown type to format: " ++ @typeName(Node)),
                    },
                    .enum_literal => .{ .raw = @tagName(some) },
                    .bool => .{ .bool = some },
                    .@"struct" => switch (Some) {
                        DIFlags => .{ .di_flags = some },
                        Metadata => switch (some.kind) {
                            .string => .{ .string = some.toString() },
                            .node, .forward => try formatter.refUnwrapped(some.unwrap(formatter.builder)),
                            .local => unreachable,
                        },
                        Subprogram.DISPFlags => .{ .sp_flags = some },
                        else => @compileError("unknown type to format: " ++ @typeName(Node)),
                    },
                    .int, .comptime_int => .{ .u64 = some },
                    .pointer => .{ .raw = some },
                    else => @compileError("unknown type to format: " ++ @typeName(Node)),
                } else switch (Node) {
                    Metadata.Optional, Metadata.String.Optional => .none,
                    else => switch (@typeInfo(Node)) {
                        .optional, .null => .none,
                        else => unreachable,
                    },
                },
                .specialized = special,
            } };
        }
        inline fn fmtLocal(
            formatter: *Formatter,
            prefix: []const u8,
            value: Value,
            function: Function.Index,
        ) Allocator.Error!std.fmt.Alt(FormatData, format) {
            return .{ .data = .{
                .formatter = formatter,
                .prefix = prefix,
                .node = node: switch (value.unwrap()) {
                    .instruction, .constant => .{ .local_value = .{
                        .value = value,
                        .function = function,
                    } },
                    .metadata => |metadata| if (value == .none) .none else {
                        const unwrapped = metadata.unwrap(formatter.builder);
                        break :node switch (unwrapped.kind) {
                            .string, .node => switch (try formatter.refUnwrapped(unwrapped)) {
                                .@"inline" => |node| .{ .local_inline = node },
                                .index => |node| .{ .local_index = node },
                                else => unreachable,
                            },
                            .forward => unreachable,
                            .local => .{ .local_metadata = .{
                                .value = function.ptrConst(formatter.builder).debug_values[
                                    unwrapped.index
                                ].toValue(),
                                .function = function,
                            } },
                        };
                    },
                },
                .specialized = null,
            } };
        }
        fn refUnwrapped(formatter: *Formatter, node: Metadata) Allocator.Error!FormatData.Node {
            const builder = formatter.builder;
            const unwrapped_metadata = node.unwrap(builder);
            switch (unwrapped_metadata.tag(builder)) {
                .expression, .constant => return .{ .@"inline" = unwrapped_metadata },
                else => |metadata_tag| {
                    assert(!metadata_tag.isInline());
                    const gop = try formatter.map.getOrPut(builder.gpa, .{ .metadata = unwrapped_metadata });
                    return .{ .index = @intCast(gop.index) };
                },
            }
        }

        inline fn specialized(
            formatter: *Formatter,
            distinct: enum { @"!", @"distinct !" },
            node: enum {
                DIFile,
                DICompileUnit,
                DISubprogram,
                DILexicalBlock,
                DILocation,
                DIBasicType,
                DICompositeType,
                DIDerivedType,
                DISubroutineType,
                DIEnumerator,
                DISubrange,
                DILocalVariable,
                DIGlobalVariable,
                DIGlobalVariableExpression,
            },
            nodes: anytype,
            w: *Writer,
        ) !void {
            const names = comptime std.meta.fieldNames(@TypeOf(nodes));

            comptime var fmt_str: []const u8 = "{[distinct]s}{[node]s}(";
            inline for (names) |name| fmt_str = fmt_str ++ "{[" ++ name ++ "]f}";
            fmt_str = fmt_str ++ ")\n";

            const field_names = @as([]const []const u8, &.{ "distinct", "node" }) ++ names;
            comptime var field_types: [2 + names.len]type = undefined;
            @memset(field_types[0..2], []const u8);
            @memset(field_types[2..], std.fmt.Alt(FormatData, format));

            var fmt_args: @Struct(.auto, null, field_names, &field_types, &@splat(.{})) = undefined;
            fmt_args.distinct = @tagName(distinct);
            fmt_args.node = @tagName(node);
            inline for (names) |name| @field(fmt_args, name) = try formatter.fmt(
                name ++ ": ",
                @field(nodes, name),
                null,
            );
            try w.print(fmt_str, fmt_args);
        }
    };
};

pub fn init(options: Options) Allocator.Error!Builder {
    var self: Builder = .{
        .gpa = options.allocator,
        .strip = options.strip,

        .source_filename = .none,
        .data_layout = .none,
        .target_triple = .none,
        .module_asm = .empty,

        .string_map = .empty,
        .string_indices = .empty,
        .string_bytes = .empty,

        .types = .empty,
        .next_unnamed_type = @enumFromInt(0),
        .next_unique_type_id = .empty,
        .type_map = .empty,
        .type_items = .empty,
        .type_extra = .empty,

        .attributes = .empty,
        .attributes_map = .empty,
        .attributes_indices = .empty,
        .attributes_extra = .empty,

        .function_attributes_set = .empty,

        .globals = .empty,
        .next_unnamed_global = @enumFromInt(0),
        .next_replaced_global = .none,
        .next_unique_global_id = .empty,
        .aliases = .empty,
        .variables = .empty,
        .functions = .empty,

        .strtab_string_map = .empty,
        .strtab_string_indices = .empty,
        .strtab_string_bytes = .empty,

        .constant_map = .empty,
        .constant_items = .empty,
        .constant_extra = .empty,
        .constant_limbs = .empty,

        .alignment_forward_references = .empty,

        .metadata_map = .empty,
        .metadata_items = .empty,
        .metadata_extra = .empty,
        .metadata_limbs = .empty,
        .metadata_forward_references = .empty,
        .metadata_named = .empty,
        .metadata_string_map = .empty,
        .metadata_string_indices = .empty,
        .metadata_string_bytes = .empty,
    };
    errdefer self.deinit();

    try self.string_indices.append(self.gpa, 0);
    assert(try self.string("") == .empty);

    try self.strtab_string_indices.append(self.gpa, 0);
    assert(try self.strtabString("") == .empty);

    if (options.name.len > 0) self.source_filename = try self.string(options.name);

    if (options.triple.len > 0) {
        self.target_triple = try self.string(options.triple);
    }

    {
        const static_len = @typeInfo(Type).@"enum".fields.len - 1;
        try self.type_map.ensureTotalCapacity(self.gpa, static_len);
        try self.type_items.ensureTotalCapacity(self.gpa, static_len);
        inline for (@typeInfo(Type.Simple).@"enum".fields) |simple_field| {
            const result = self.getOrPutTypeNoExtraAssumeCapacity(
                .{ .tag = .simple, .data = simple_field.value },
            );
            assert(result.new and result.type == @field(Type, simple_field.name));
        }
        inline for (.{ 1, 8, 16, 29, 32, 64, 80, 128 }) |bits|
            assert(self.intTypeAssumeCapacity(bits) ==
                @field(Type, std.fmt.comptimePrint("i{d}", .{bits})));
        inline for (.{ 0, 4 }) |addr_space_index| {
            const addr_space: AddrSpace = @enumFromInt(addr_space_index);
            assert(self.ptrTypeAssumeCapacity(addr_space) ==
                @field(Type, std.fmt.comptimePrint("ptr{f}", .{addr_space.fmt(" ")})));
        }
    }

    {
        try self.attributes_indices.append(self.gpa, 0);
        assert(try self.attrs(&.{}) == .none);
        assert(try self.fnAttrs(&.{}) == .none);
    }

    assert(try self.intConst(.i1, 0) == .false);
    assert(try self.intConst(.i1, 1) == .true);
    assert(try self.intConst(.i32, 0) == .@"0");
    assert(try self.intConst(.i32, 1) == .@"1");
    assert(try self.noneConst(.token) == .none);

    assert(try self.metadataTuple(&.{}) == Metadata.empty_tuple);

    try self.metadata_string_indices.append(self.gpa, 0);

    return self;
}

pub fn clearAndFree(self: *Builder) void {
    self.module_asm.clearAndFree(self.gpa);

    self.string_map.clearAndFree(self.gpa);
    self.string_indices.clearAndFree(self.gpa);
    self.string_bytes.clearAndFree(self.gpa);

    self.types.clearAndFree(self.gpa);
    self.next_unique_type_id.clearAndFree(self.gpa);
    self.type_map.clearAndFree(self.gpa);
    self.type_items.clearAndFree(self.gpa);
    self.type_extra.clearAndFree(self.gpa);

    self.attributes.clearAndFree(self.gpa);
    self.attributes_map.clearAndFree(self.gpa);
    self.attributes_indices.clearAndFree(self.gpa);
    self.attributes_extra.clearAndFree(self.gpa);

    self.function_attributes_set.clearAndFree(self.gpa);

    self.globals.clearAndFree(self.gpa);
    self.next_unique_global_id.clearAndFree(self.gpa);
    self.aliases.clearAndFree(self.gpa);
    self.variables.clearAndFree(self.gpa);
    for (self.functions.items) |*function| function.deinit(self.gpa);
    self.functions.clearAndFree(self.gpa);

    self.strtab_string_map.clearAndFree(self.gpa);
    self.strtab_string_indices.clearAndFree(self.gpa);
    self.strtab_string_bytes.clearAndFree(self.gpa);

    self.constant_map.clearAndFree(self.gpa);
    self.constant_items.shrinkAndFree(self.gpa, 0);
    self.constant_extra.clearAndFree(self.gpa);
    self.constant_limbs.clearAndFree(self.gpa);

    self.metadata_map.clearAndFree(self.gpa);
    self.metadata_items.shrinkAndFree(self.gpa, 0);
    self.metadata_extra.clearAndFree(self.gpa);
    self.metadata_limbs.clearAndFree(self.gpa);
    self.metadata_forward_references.clearAndFree(self.gpa);
    self.metadata_named.clearAndFree(self.gpa);

    self.metadata_string_map.clearAndFree(self.gpa);
    self.metadata_string_indices.clearAndFree(self.gpa);
    self.metadata_string_bytes.clearAndFree(self.gpa);
}

pub fn deinit(self: *Builder) void {
    const gpa = self.gpa;

    self.module_asm.deinit(gpa);

    self.string_map.deinit(gpa);
    self.string_indices.deinit(gpa);
    self.string_bytes.deinit(gpa);

    self.types.deinit(gpa);
    self.next_unique_type_id.deinit(gpa);
    self.type_map.deinit(gpa);
    self.type_items.deinit(gpa);
    self.type_extra.deinit(gpa);

    self.attributes.deinit(gpa);
    self.attributes_map.deinit(gpa);
    self.attributes_indices.deinit(gpa);
    self.attributes_extra.deinit(gpa);

    self.function_attributes_set.deinit(gpa);

    self.globals.deinit(gpa);
    self.next_unique_global_id.deinit(gpa);
    self.aliases.deinit(gpa);
    self.variables.deinit(gpa);
    for (self.functions.items) |*function| function.deinit(gpa);
    self.functions.deinit(gpa);

    self.strtab_string_map.deinit(gpa);
    self.strtab_string_indices.deinit(gpa);
    self.strtab_string_bytes.deinit(gpa);

    self.constant_map.deinit(gpa);
    self.constant_items.deinit(gpa);
    self.constant_extra.deinit(gpa);
    self.constant_limbs.deinit(gpa);

    self.alignment_forward_references.deinit(gpa);

    self.metadata_map.deinit(gpa);
    self.metadata_items.deinit(gpa);
    self.metadata_extra.deinit(gpa);
    self.metadata_limbs.deinit(gpa);
    self.metadata_forward_references.deinit(gpa);
    self.metadata_named.deinit(gpa);

    self.metadata_string_map.deinit(gpa);
    self.metadata_string_indices.deinit(gpa);
    self.metadata_string_bytes.deinit(gpa);

    self.* = undefined;
}

pub fn finishModuleAsm(self: *Builder, aw: *Writer.Allocating) Allocator.Error!void {
    self.module_asm = aw.toArrayList();
    if (self.module_asm.getLastOrNull()) |last| if (last != '\n')
        try self.module_asm.append(self.gpa, '\n');
}

pub fn string(self: *Builder, bytes: []const u8) Allocator.Error!String {
    try self.string_bytes.ensureUnusedCapacity(self.gpa, bytes.len);
    try self.string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.string_map.ensureUnusedCapacity(self.gpa, 1);

    const gop = self.string_map.getOrPutAssumeCapacityAdapted(bytes, String.Adapter{ .builder = self });
    if (!gop.found_existing) {
        self.string_bytes.appendSliceAssumeCapacity(bytes);
        self.string_indices.appendAssumeCapacity(@intCast(self.string_bytes.items.len));
    }
    return String.fromIndex(gop.index);
}

pub fn stringNull(self: *Builder, bytes: [:0]const u8) Allocator.Error!String {
    return self.string(bytes[0 .. bytes.len + 1]);
}

pub fn stringIfExists(self: *const Builder, bytes: []const u8) ?String {
    return String.fromIndex(
        self.string_map.getIndexAdapted(bytes, String.Adapter{ .builder = self }) orelse return null,
    );
}

pub fn fmt(self: *Builder, comptime fmt_str: []const u8, fmt_args: anytype) Allocator.Error!String {
    try self.string_map.ensureUnusedCapacity(self.gpa, 1);
    try self.string_bytes.ensureUnusedCapacity(self.gpa, @intCast(std.fmt.count(fmt_str, fmt_args)));
    try self.string_indices.ensureUnusedCapacity(self.gpa, 1);
    return self.fmtAssumeCapacity(fmt_str, fmt_args);
}

pub fn fmtAssumeCapacity(self: *Builder, comptime fmt_str: []const u8, fmt_args: anytype) String {
    self.string_bytes.printAssumeCapacity(fmt_str, fmt_args);
    return self.trailingStringAssumeCapacity();
}

pub fn trailingString(self: *Builder) Allocator.Error!String {
    try self.string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.string_map.ensureUnusedCapacity(self.gpa, 1);
    return self.trailingStringAssumeCapacity();
}

pub fn trailingStringAssumeCapacity(self: *Builder) String {
    const start = self.string_indices.getLast();
    const bytes: []const u8 = self.string_bytes.items[start..];
    const gop = self.string_map.getOrPutAssumeCapacityAdapted(bytes, String.Adapter{ .builder = self });
    if (gop.found_existing) {
        self.string_bytes.shrinkRetainingCapacity(start);
    } else {
        self.string_indices.appendAssumeCapacity(@intCast(self.string_bytes.items.len));
    }
    return String.fromIndex(gop.index);
}

pub fn fnType(
    self: *Builder,
    ret: Type,
    params: []const Type,
    kind: Type.Function.Kind,
) Allocator.Error!Type {
    try self.ensureUnusedTypeCapacity(1, Type.Function, params.len);
    switch (kind) {
        inline else => |comptime_kind| return self.fnTypeAssumeCapacity(ret, params, comptime_kind),
    }
}

pub fn intType(self: *Builder, bits: u24) Allocator.Error!Type {
    try self.ensureUnusedTypeCapacity(1, NoExtra, 0);
    return self.intTypeAssumeCapacity(bits);
}

pub fn ptrType(self: *Builder, addr_space: AddrSpace) Allocator.Error!Type {
    try self.ensureUnusedTypeCapacity(1, NoExtra, 0);
    return self.ptrTypeAssumeCapacity(addr_space);
}

pub fn vectorType(
    self: *Builder,
    kind: Type.Vector.Kind,
    len: u32,
    child: Type,
) Allocator.Error!Type {
    try self.ensureUnusedTypeCapacity(1, Type.Vector, 0);
    switch (kind) {
        inline else => |comptime_kind| return self.vectorTypeAssumeCapacity(comptime_kind, len, child),
    }
}

pub fn arrayType(self: *Builder, len: u64, child: Type) Allocator.Error!Type {
    comptime assert(@sizeOf(Type.Array) >= @sizeOf(Type.Vector));
    try self.ensureUnusedTypeCapacity(1, Type.Array, 0);
    return self.arrayTypeAssumeCapacity(len, child);
}

pub fn structType(
    self: *Builder,
    kind: Type.Structure.Kind,
    fields: []const Type,
) Allocator.Error!Type {
    try self.ensureUnusedTypeCapacity(1, Type.Structure, fields.len);
    switch (kind) {
        inline else => |comptime_kind| return self.structTypeAssumeCapacity(comptime_kind, fields),
    }
}

pub fn opaqueType(self: *Builder, name: String) Allocator.Error!Type {
    try self.string_map.ensureUnusedCapacity(self.gpa, 1);
    if (name.slice(self)) |id| {
        const count: usize = comptime std.fmt.count("{d}", .{maxInt(u32)});
        try self.string_bytes.ensureUnusedCapacity(self.gpa, id.len + count);
    }
    try self.string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.types.ensureUnusedCapacity(self.gpa, 1);
    try self.next_unique_type_id.ensureUnusedCapacity(self.gpa, 1);
    try self.ensureUnusedTypeCapacity(1, Type.NamedStructure, 0);
    return self.opaqueTypeAssumeCapacity(name);
}

pub fn namedTypeSetBody(
    self: *Builder,
    named_type: Type,
    body_type: Type,
) void {
    const named_item = self.type_items.items[@intFromEnum(named_type)];
    self.type_extra.items[named_item.data + std.meta.fieldIndex(Type.NamedStructure, "body").?] =
        @intFromEnum(body_type);
}

pub fn attr(self: *Builder, attribute: Attribute) Allocator.Error!Attribute.Index {
    try self.attributes.ensureUnusedCapacity(self.gpa, 1);

    const gop = self.attributes.getOrPutAssumeCapacity(attribute.toStorage());
    if (!gop.found_existing) gop.value_ptr.* = {};
    return @enumFromInt(gop.index);
}

pub fn attrs(self: *Builder, attributes: []Attribute.Index) Allocator.Error!Attributes {
    std.sort.heap(Attribute.Index, attributes, self, struct {
        pub fn lessThan(builder: *const Builder, lhs: Attribute.Index, rhs: Attribute.Index) bool {
            const lhs_kind = lhs.getKind(builder);
            const rhs_kind = rhs.getKind(builder);
            assert(lhs_kind != rhs_kind);
            return @intFromEnum(lhs_kind) < @intFromEnum(rhs_kind);
        }
    }.lessThan);
    return @enumFromInt(try self.attrGeneric(@ptrCast(attributes)));
}

pub fn fnAttrs(self: *Builder, fn_attributes: []const Attributes) Allocator.Error!FunctionAttributes {
    try self.function_attributes_set.ensureUnusedCapacity(self.gpa, 1);
    const function_attributes: FunctionAttributes = @enumFromInt(try self.attrGeneric(@ptrCast(
        fn_attributes[0..if (std.mem.lastIndexOfNone(Attributes, fn_attributes, &.{.none})) |last|
            last + 1
        else
            0],
    )));

    _ = self.function_attributes_set.getOrPutAssumeCapacity(function_attributes);
    return function_attributes;
}

pub fn addGlobal(self: *Builder, name: StrtabString, global: Global) Allocator.Error!Global.Index {
    assert(!name.isAnon());
    try self.ensureUnusedTypeCapacity(1, NoExtra, 0);
    try self.ensureUnusedGlobalCapacity(name);
    return self.addGlobalAssumeCapacity(name, global);
}

pub fn addGlobalAssumeCapacity(self: *Builder, name: StrtabString, global: Global) Global.Index {
    _ = self.ptrTypeAssumeCapacity(global.addr_space);
    var id = name;
    if (name == .empty) {
        id = self.next_unnamed_global;
        assert(id != self.next_replaced_global);
        self.next_unnamed_global = @enumFromInt(@intFromEnum(id) + 1);
    }
    while (true) {
        const global_gop = self.globals.getOrPutAssumeCapacity(id);
        if (!global_gop.found_existing) {
            global_gop.value_ptr.* = global;
            const global_index: Global.Index = @enumFromInt(global_gop.index);
            global_index.updateDsoLocal(self);
            return global_index;
        }

        const unique_gop = self.next_unique_global_id.getOrPutAssumeCapacity(name);
        if (!unique_gop.found_existing) unique_gop.value_ptr.* = 2;
        id = self.strtabStringFmtAssumeCapacity("{s}.{d}", .{ name.slice(self).?, unique_gop.value_ptr.* });
        unique_gop.value_ptr.* += 1;
    }
}

pub fn getGlobal(self: *const Builder, name: StrtabString) ?Global.Index {
    return @enumFromInt(self.globals.getIndex(name) orelse return null);
}

pub fn addAlias(
    self: *Builder,
    name: StrtabString,
    ty: Type,
    addr_space: AddrSpace,
    aliasee: Constant,
) Allocator.Error!Alias.Index {
    assert(!name.isAnon());
    try self.ensureUnusedTypeCapacity(1, NoExtra, 0);
    try self.ensureUnusedGlobalCapacity(name);
    try self.aliases.ensureUnusedCapacity(self.gpa, 1);
    return self.addAliasAssumeCapacity(name, ty, addr_space, aliasee);
}

pub fn addAliasAssumeCapacity(
    self: *Builder,
    name: StrtabString,
    ty: Type,
    addr_space: AddrSpace,
    aliasee: Constant,
) Alias.Index {
    const alias_index: Alias.Index = @enumFromInt(self.aliases.items.len);
    self.aliases.appendAssumeCapacity(.{ .global = self.addGlobalAssumeCapacity(name, .{
        .addr_space = addr_space,
        .type = ty,
        .kind = .{ .alias = alias_index },
    }), .aliasee = aliasee });
    return alias_index;
}

pub fn addVariable(
    self: *Builder,
    name: StrtabString,
    ty: Type,
    addr_space: AddrSpace,
) Allocator.Error!Variable.Index {
    assert(!name.isAnon());
    try self.ensureUnusedTypeCapacity(1, NoExtra, 0);
    try self.ensureUnusedGlobalCapacity(name);
    try self.variables.ensureUnusedCapacity(self.gpa, 1);
    return self.addVariableAssumeCapacity(ty, name, addr_space);
}

pub fn addVariableAssumeCapacity(
    self: *Builder,
    ty: Type,
    name: StrtabString,
    addr_space: AddrSpace,
) Variable.Index {
    const variable_index: Variable.Index = @enumFromInt(self.variables.items.len);
    self.variables.appendAssumeCapacity(.{ .global = self.addGlobalAssumeCapacity(name, .{
        .addr_space = addr_space,
        .type = ty,
        .kind = .{ .variable = variable_index },
    }) });
    return variable_index;
}

pub fn addFunction(
    self: *Builder,
    ty: Type,
    name: StrtabString,
    addr_space: AddrSpace,
) Allocator.Error!Function.Index {
    assert(!name.isAnon());
    try self.ensureUnusedTypeCapacity(1, NoExtra, 0);
    try self.ensureUnusedGlobalCapacity(name);
    try self.functions.ensureUnusedCapacity(self.gpa, 1);
    return self.addFunctionAssumeCapacity(ty, name, addr_space);
}

pub fn addFunctionAssumeCapacity(
    self: *Builder,
    ty: Type,
    name: StrtabString,
    addr_space: AddrSpace,
) Function.Index {
    assert(ty.isFunction(self));
    const function_index: Function.Index = @enumFromInt(self.functions.items.len);
    self.functions.appendAssumeCapacity(.{
        .global = self.addGlobalAssumeCapacity(name, .{
            .addr_space = addr_space,
            .type = ty,
            .kind = .{ .function = function_index },
        }),
        .strip = undefined,
    });
    return function_index;
}

pub fn getIntrinsic(
    self: *Builder,
    id: Intrinsic,
    overload: []const Type,
) Allocator.Error!Function.Index {
    const ExpectedContents = extern union {
        attrs: extern struct {
            params: [expected_args_len]Type,
            fn_attrs: [FunctionAttributes.params_index + expected_args_len]Attributes,
            attrs: [expected_attrs_len]Attribute.Index,
            fields: [expected_fields_len]Type,
        },
    };
    var stack align(@max(@alignOf(std.heap.StackFallbackAllocator(0)), @alignOf(ExpectedContents))) =
        std.heap.stackFallback(@sizeOf(ExpectedContents), self.gpa);
    const allocator = stack.get();

    const name = name: {
        {
            var aw: Writer.Allocating = .fromArrayList(self.gpa, &self.strtab_string_bytes);
            const w = &aw.writer;
            defer self.strtab_string_bytes = aw.toArrayList();
            w.print("llvm.{s}", .{@tagName(id)}) catch return error.OutOfMemory;
            for (overload) |ty| w.print(".{f}", .{ty.fmt(self, .m)}) catch return error.OutOfMemory;
        }
        break :name try self.trailingStrtabString();
    };
    if (self.getGlobal(name)) |global| return global.ptrConst(self).kind.function;

    const signature = Intrinsic.signatures.get(id);
    const param_types = try allocator.alloc(Type, signature.params.len);
    defer allocator.free(param_types);
    const function_attributes = try allocator.alloc(
        Attributes,
        FunctionAttributes.params_index + (signature.params.len - signature.ret_len),
    );
    defer allocator.free(function_attributes);

    var attributes: struct {
        builder: *Builder,
        list: std.array_list.Managed(Attribute.Index),

        fn deinit(state: *@This()) void {
            state.list.deinit();
            state.* = undefined;
        }

        fn get(state: *@This(), attributes: []const Attribute) Allocator.Error!Attributes {
            try state.list.resize(attributes.len);
            for (state.list.items, attributes) |*item, attribute|
                item.* = try state.builder.attr(attribute);
            return state.builder.attrs(state.list.items);
        }
    } = .{ .builder = self, .list = std.array_list.Managed(Attribute.Index).init(allocator) };
    defer attributes.deinit();

    var overload_index: usize = 0;
    function_attributes[FunctionAttributes.function_index] = try attributes.get(signature.attrs);
    function_attributes[FunctionAttributes.return_index] = .none; // needed for void return
    for (0.., param_types, signature.params) |param_index, *param_type, signature_param| {
        switch (signature_param.kind) {
            .type => |ty| param_type.* = ty,
            .overloaded => {
                param_type.* = overload[overload_index];
                overload_index += 1;
            },
            .matches, .matches_scalar, .matches_changed_scalar => {},
        }
        function_attributes[
            if (param_index < signature.ret_len)
                FunctionAttributes.return_index
            else
                FunctionAttributes.params_index + (param_index - signature.ret_len)
        ] = try attributes.get(signature_param.attrs);
    }
    assert(overload_index == overload.len);
    for (param_types, signature.params) |*param_type, signature_param| {
        param_type.* = switch (signature_param.kind) {
            .type, .overloaded => continue,
            .matches => |param_index| param_types[param_index],
            .matches_scalar => |param_index| param_types[param_index].scalarType(self),
            .matches_changed_scalar => |info| try param_types[info.index]
                .changeScalar(info.scalar, self),
        };
    }

    const function_index = try self.addFunction(try self.fnType(switch (signature.ret_len) {
        0 => .void,
        1 => param_types[0],
        else => try self.structType(.normal, param_types[0..signature.ret_len]),
    }, param_types[signature.ret_len..], .normal), name, .default);
    function_index.ptr(self).attributes = try self.fnAttrs(function_attributes);
    return function_index;
}

pub fn intConst(self: *Builder, ty: Type, value: anytype) Allocator.Error!Constant {
    const int_value = switch (@typeInfo(@TypeOf(value))) {
        .int, .comptime_int => value,
        .@"enum" => @intFromEnum(value),
        else => @compileError("intConst expected an integral value, got " ++ @typeName(@TypeOf(value))),
    };
    var limbs: [
        switch (@typeInfo(@TypeOf(int_value))) {
            .int => |info| std.math.big.int.calcTwosCompLimbCount(info.bits),
            .comptime_int => std.math.big.int.calcLimbLen(int_value),
            else => unreachable,
        }
    ]std.math.big.Limb = undefined;
    return self.bigIntConst(ty, std.math.big.int.Mutable.init(&limbs, int_value).toConst());
}

pub fn intValue(self: *Builder, ty: Type, value: anytype) Allocator.Error!Value {
    return (try self.intConst(ty, value)).toValue();
}

pub fn bigIntConst(self: *Builder, ty: Type, value: std.math.big.int.Const) Allocator.Error!Constant {
    try self.constant_map.ensureUnusedCapacity(self.gpa, 1);
    try self.constant_items.ensureUnusedCapacity(self.gpa, 1);
    try self.constant_limbs.ensureUnusedCapacity(self.gpa, Constant.Integer.limbs + value.limbs.len);
    return self.bigIntConstAssumeCapacity(ty, value);
}

pub fn bigIntValue(self: *Builder, ty: Type, value: std.math.big.int.Const) Allocator.Error!Value {
    return (try self.bigIntConst(ty, value)).toValue();
}

pub fn fpConst(self: *Builder, ty: Type, comptime val: comptime_float) Allocator.Error!Constant {
    return switch (ty) {
        .half => try self.halfConst(val),
        .bfloat => try self.bfloatConst(val),
        .float => try self.floatConst(val),
        .double => try self.doubleConst(val),
        .fp128 => try self.fp128Const(val),
        .x86_fp80 => try self.x86_fp80Const(val),
        .ppc_fp128 => try self.ppc_fp128Const(.{ val, -0.0 }),
        else => unreachable,
    };
}

pub fn fpValue(self: *Builder, ty: Type, comptime value: comptime_float) Allocator.Error!Value {
    return (try self.fpConst(ty, value)).toValue();
}

pub fn nanConst(self: *Builder, ty: Type) Allocator.Error!Constant {
    return switch (ty) {
        .half => try self.halfConst(std.math.nan(f16)),
        .bfloat => try self.bfloatConst(std.math.nan(f32)),
        .float => try self.floatConst(std.math.nan(f32)),
        .double => try self.doubleConst(std.math.nan(f64)),
        .fp128 => try self.fp128Const(std.math.nan(f128)),
        .x86_fp80 => try self.x86_fp80Const(std.math.nan(f80)),
        .ppc_fp128 => try self.ppc_fp128Const(.{std.math.nan(f64)} ** 2),
        else => unreachable,
    };
}

pub fn nanValue(self: *Builder, ty: Type) Allocator.Error!Value {
    return (try self.nanConst(ty)).toValue();
}

pub fn halfConst(self: *Builder, val: f16) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.halfConstAssumeCapacity(val);
}

pub fn halfValue(self: *Builder, value: f16) Allocator.Error!Value {
    return (try self.halfConst(value)).toValue();
}

pub fn bfloatConst(self: *Builder, val: f32) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.bfloatConstAssumeCapacity(val);
}

pub fn bfloatValue(self: *Builder, value: f32) Allocator.Error!Value {
    return (try self.bfloatConst(value)).toValue();
}

pub fn floatConst(self: *Builder, val: f32) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.floatConstAssumeCapacity(val);
}

pub fn floatValue(self: *Builder, value: f32) Allocator.Error!Value {
    return (try self.floatConst(value)).toValue();
}

pub fn doubleConst(self: *Builder, val: f64) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Double, 0);
    return self.doubleConstAssumeCapacity(val);
}

pub fn doubleValue(self: *Builder, value: f64) Allocator.Error!Value {
    return (try self.doubleConst(value)).toValue();
}

pub fn fp128Const(self: *Builder, val: f128) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Fp128, 0);
    return self.fp128ConstAssumeCapacity(val);
}

pub fn fp128Value(self: *Builder, value: f128) Allocator.Error!Value {
    return (try self.fp128Const(value)).toValue();
}

pub fn x86_fp80Const(self: *Builder, val: f80) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Fp80, 0);
    return self.x86_fp80ConstAssumeCapacity(val);
}

pub fn x86_fp80Value(self: *Builder, value: f80) Allocator.Error!Value {
    return (try self.x86_fp80Const(value)).toValue();
}

pub fn ppc_fp128Const(self: *Builder, val: [2]f64) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Fp128, 0);
    return self.ppc_fp128ConstAssumeCapacity(val);
}

pub fn ppc_fp128Value(self: *Builder, value: [2]f64) Allocator.Error!Value {
    return (try self.ppc_fp128Const(value)).toValue();
}

pub fn nullConst(self: *Builder, ty: Type) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.nullConstAssumeCapacity(ty);
}

pub fn nullValue(self: *Builder, ty: Type) Allocator.Error!Value {
    return (try self.nullConst(ty)).toValue();
}

pub fn noneConst(self: *Builder, ty: Type) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.noneConstAssumeCapacity(ty);
}

pub fn noneValue(self: *Builder, ty: Type) Allocator.Error!Value {
    return (try self.noneConst(ty)).toValue();
}

pub fn structConst(self: *Builder, ty: Type, vals: []const Constant) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Aggregate, vals.len);
    return self.structConstAssumeCapacity(ty, vals);
}

pub fn structValue(self: *Builder, ty: Type, vals: []const Constant) Allocator.Error!Value {
    return (try self.structConst(ty, vals)).toValue();
}

pub fn arrayConst(self: *Builder, ty: Type, vals: []const Constant) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Aggregate, vals.len);
    return self.arrayConstAssumeCapacity(ty, vals);
}

pub fn arrayValue(self: *Builder, ty: Type, vals: []const Constant) Allocator.Error!Value {
    return (try self.arrayConst(ty, vals)).toValue();
}

pub fn stringConst(self: *Builder, val: String) Allocator.Error!Constant {
    try self.ensureUnusedTypeCapacity(1, Type.Array, 0);
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.stringConstAssumeCapacity(val);
}

pub fn stringValue(self: *Builder, val: String) Allocator.Error!Value {
    return (try self.stringConst(val)).toValue();
}

pub fn vectorConst(self: *Builder, ty: Type, vals: []const Constant) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Aggregate, vals.len);
    return self.vectorConstAssumeCapacity(ty, vals);
}

pub fn vectorValue(self: *Builder, ty: Type, vals: []const Constant) Allocator.Error!Value {
    return (try self.vectorConst(ty, vals)).toValue();
}

pub fn splatConst(self: *Builder, ty: Type, val: Constant) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Splat, 0);
    return self.splatConstAssumeCapacity(ty, val);
}

pub fn splatValue(self: *Builder, ty: Type, val: Constant) Allocator.Error!Value {
    return (try self.splatConst(ty, val)).toValue();
}

pub fn zeroInitConst(self: *Builder, ty: Type) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Fp128, 0);
    try self.constant_limbs.ensureUnusedCapacity(
        self.gpa,
        Constant.Integer.limbs + comptime std.math.big.int.calcLimbLen(0),
    );
    return self.zeroInitConstAssumeCapacity(ty);
}

pub fn zeroInitValue(self: *Builder, ty: Type) Allocator.Error!Value {
    return (try self.zeroInitConst(ty)).toValue();
}

pub fn undefConst(self: *Builder, ty: Type) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.undefConstAssumeCapacity(ty);
}

pub fn undefValue(self: *Builder, ty: Type) Allocator.Error!Value {
    return (try self.undefConst(ty)).toValue();
}

pub fn poisonConst(self: *Builder, ty: Type) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.poisonConstAssumeCapacity(ty);
}

pub fn poisonValue(self: *Builder, ty: Type) Allocator.Error!Value {
    return (try self.poisonConst(ty)).toValue();
}

pub fn blockAddrConst(
    self: *Builder,
    function: Function.Index,
    block: Function.Block.Index,
) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.BlockAddress, 0);
    return self.blockAddrConstAssumeCapacity(function, block);
}

pub fn blockAddrValue(
    self: *Builder,
    function: Function.Index,
    block: Function.Block.Index,
) Allocator.Error!Value {
    return (try self.blockAddrConst(function, block)).toValue();
}

pub fn dsoLocalEquivalentConst(self: *Builder, function: Function.Index) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.dsoLocalEquivalentConstAssumeCapacity(function);
}

pub fn dsoLocalEquivalentValue(self: *Builder, function: Function.Index) Allocator.Error!Value {
    return (try self.dsoLocalEquivalentConst(function)).toValue();
}

pub fn noCfiConst(self: *Builder, function: Function.Index) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, NoExtra, 0);
    return self.noCfiConstAssumeCapacity(function);
}

pub fn noCfiValue(self: *Builder, function: Function.Index) Allocator.Error!Value {
    return (try self.noCfiConst(function)).toValue();
}

pub fn convConst(
    self: *Builder,
    val: Constant,
    ty: Type,
) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Cast, 0);
    return self.convConstAssumeCapacity(val, ty);
}

pub fn convValue(
    self: *Builder,
    val: Constant,
    ty: Type,
) Allocator.Error!Value {
    return (try self.convConst(val, ty)).toValue();
}

pub fn castConst(self: *Builder, tag: Constant.Tag, val: Constant, ty: Type) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Cast, 0);
    return self.castConstAssumeCapacity(tag, val, ty);
}

pub fn castValue(self: *Builder, tag: Constant.Tag, val: Constant, ty: Type) Allocator.Error!Value {
    return (try self.castConst(tag, val, ty)).toValue();
}

pub fn gepConst(
    self: *Builder,
    comptime kind: Constant.GetElementPtr.Kind,
    ty: Type,
    base: Constant,
    inrange: ?u16,
    indices: []const Constant,
) Allocator.Error!Constant {
    try self.ensureUnusedTypeCapacity(1, Type.Vector, 0);
    try self.ensureUnusedConstantCapacity(1, Constant.GetElementPtr, indices.len);
    return self.gepConstAssumeCapacity(kind, ty, base, inrange, indices);
}

pub fn gepValue(
    self: *Builder,
    comptime kind: Constant.GetElementPtr.Kind,
    ty: Type,
    base: Constant,
    inrange: ?u16,
    indices: []const Constant,
) Allocator.Error!Value {
    return (try self.gepConst(kind, ty, base, inrange, indices)).toValue();
}

pub fn binConst(
    self: *Builder,
    tag: Constant.Tag,
    lhs: Constant,
    rhs: Constant,
) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Binary, 0);
    return self.binConstAssumeCapacity(tag, lhs, rhs);
}

pub fn binValue(self: *Builder, tag: Constant.Tag, lhs: Constant, rhs: Constant) Allocator.Error!Value {
    return (try self.binConst(tag, lhs, rhs)).toValue();
}

pub fn asmConst(
    self: *Builder,
    ty: Type,
    info: Constant.Assembly.Info,
    assembly: String,
    constraints: String,
) Allocator.Error!Constant {
    try self.ensureUnusedConstantCapacity(1, Constant.Assembly, 0);
    return self.asmConstAssumeCapacity(ty, info, assembly, constraints);
}

pub fn asmValue(
    self: *Builder,
    ty: Type,
    info: Constant.Assembly.Info,
    assembly: String,
    constraints: String,
) Allocator.Error!Value {
    return (try self.asmConst(ty, info, assembly, constraints)).toValue();
}

/// The initial "resolved" value of the forward reference is `Alignment.default`.
pub fn alignmentForwardReference(b: *Builder) Allocator.Error!Alignment.Lazy {
    const index = b.alignment_forward_references.items.len;
    try b.alignment_forward_references.append(b.gpa, .default);
    return .fromFwdRefIndex(index);
}

/// Updates the "resolved" value of the alignment forward reference `fwd_ref` to `value`.
///
/// Asserts that `fwd_ref` is a forward reference, as opposed to a resolved alignment value.
pub fn resolveAlignmentForwardReference(b: *Builder, fwd_ref: Alignment.Lazy, value: Alignment) void {
    const index = fwd_ref.toFwdRefIndex();
    b.alignment_forward_references.items[index] = value;
}

pub fn dump(b: *Builder, io: Io) void {
    var buffer: [4000]u8 = undefined;
    const stderr: Io.File = .stderr();
    b.printToFile(io, stderr, &buffer) catch {};
}

pub fn printToFilePath(b: *Builder, io: Io, dir: Io.Dir, path: []const u8) !void {
    var buffer: [4000]u8 = undefined;
    const file = try dir.createFile(io, path, .{});
    defer file.close(io);
    try b.printToFile(io, file, &buffer);
}

pub fn printToFile(b: *Builder, io: Io, file: Io.File, buffer: []u8) !void {
    var fw = file.writer(io, buffer);
    try print(b, &fw.interface);
    try fw.interface.flush();
}

pub fn print(self: *Builder, w: *Writer) (Writer.Error || Allocator.Error)!void {
    var need_newline = false;
    var metadata_formatter: Metadata.Formatter = .{ .builder = self, .need_comma = undefined };
    defer metadata_formatter.map.deinit(self.gpa);

    if (self.source_filename != .none or self.data_layout != .none or self.target_triple != .none) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        if (self.source_filename != .none) try w.print(
            \\; ModuleID = '{s}'
            \\source_filename = {f}
            \\
        , .{ self.source_filename.slice(self).?, self.source_filename.fmtQ(self) });
        if (self.data_layout != .none) try w.print(
            \\target datalayout = {f}
            \\
        , .{self.data_layout.fmtQ(self)});
        if (self.target_triple != .none) try w.print(
            \\target triple = {f}
            \\
        , .{self.target_triple.fmtQ(self)});
    }

    if (self.module_asm.items.len > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        var line_it = std.mem.tokenizeScalar(u8, self.module_asm.items, '\n');
        while (line_it.next()) |line| {
            try w.writeAll("module asm ");
            try printEscapedString(line, .always_quote, w);
            try w.writeByte('\n');
        }
    }

    if (self.types.count() > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        for (self.types.keys(), self.types.values()) |id, ty| try w.print(
            \\%{f} = type {f}
            \\
        , .{ id.fmt(self), ty.fmt(self, .default) });
    }

    if (self.variables.items.len > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        for (self.variables.items, 0..) |variable, variable_i| {
            // Skip the variable if its global has been repurposed for something else.
            switch (variable.global.ptrConst(self).kind) {
                .variable => |v| if (@intFromEnum(v) != variable_i) continue,
                else => continue,
            }
            const global = variable.global.ptrConst(self);
            metadata_formatter.need_comma = true;
            defer metadata_formatter.need_comma = undefined;
            try w.print(
                \\{f} ={f}{f}{f}{f}{f}{f}{f}{f} {s} {f}{f}{f}{f}
                \\
            , .{
                variable.global.fmt(self),
                Linkage.fmtOptional(
                    if (global.linkage == .external and variable.init != .no_init) null else global.linkage,
                ),
                global.preemption,
                global.visibility,
                global.dll_storage_class,
                variable.thread_local.fmt(" "),
                global.unnamed_addr,
                global.addr_space.fmt(" "),
                global.externally_initialized,
                @tagName(variable.mutability),
                global.type.fmt(self, .percent),
                variable.init.fmt(self, .{ .space = true }),
                variable.alignment.fmt(", "),
                try metadata_formatter.fmt("!dbg ", global.dbg, null),
            });
        }
    }

    if (self.aliases.items.len > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        for (self.aliases.items, 0..) |alias, alias_i| {
            // Skip the alias if its global has been repurposed for something else.
            switch (alias.global.ptrConst(self).kind) {
                .alias => |a| if (@intFromEnum(a) != alias_i) continue,
                else => continue,
            }
            const global = alias.global.ptrConst(self);
            metadata_formatter.need_comma = true;
            defer metadata_formatter.need_comma = undefined;
            try w.print(
                \\{f} ={f}{f}{f}{f}{f}{f} alias {f}, {f}{f}
                \\
            , .{
                alias.global.fmt(self),
                global.linkage,
                global.preemption,
                global.visibility,
                global.dll_storage_class,
                alias.thread_local.fmt(" "),
                global.unnamed_addr,
                global.type.fmt(self, .percent),
                alias.aliasee.fmt(self, .{ .percent = true }),
                try metadata_formatter.fmt("!dbg ", global.dbg, null),
            });
        }
    }

    var attribute_groups: std.AutoArrayHashMapUnmanaged(Attributes, void) = .empty;
    defer attribute_groups.deinit(self.gpa);

    for (0.., self.functions.items) |function_i, function| {
        // Skip the function if its global has been repurposed for something else.
        switch (function.global.ptrConst(self).kind) {
            .function => |f| if (@intFromEnum(f) != function_i) continue,
            else => continue,
        }
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        const function_index: Function.Index = @enumFromInt(function_i);
        const global = function.global.ptrConst(self);
        const params_len = global.type.functionParameters(self).len;
        const function_attributes = function.attributes.func(self);
        if (function_attributes != .none) try w.print(
            \\; Function Attrs:{f}
            \\
        , .{function_attributes.fmt(self, .{})});
        try w.print(
            \\{s}{f}{f}{f}{f}{f}{f} {f} {f}(
        , .{
            if (function.instructions.len > 0) "define" else "declare",
            global.linkage,
            global.preemption,
            global.visibility,
            global.dll_storage_class,
            function.call_conv,
            function.attributes.ret(self).fmt(self, .{}),
            global.type.functionReturn(self).fmt(self, .percent),
            function.global.fmt(self),
        });
        for (0..params_len) |arg| {
            if (arg > 0) try w.writeAll(", ");
            try w.print(
                \\{f}{f}
            , .{
                global.type.functionParameters(self)[arg].fmt(self, .percent),
                function.attributes.param(arg, self).fmt(self, .{}),
            });
            if (function.instructions.len > 0)
                try w.print(" {f}", .{function.arg(@intCast(arg)).fmt(function_index, self, .{})})
            else
                try w.print(" %{d}", .{arg});
        }
        switch (global.type.functionKind(self)) {
            .normal => {},
            .vararg => {
                if (params_len > 0) try w.writeAll(", ");
                try w.writeAll("...");
            },
        }
        try w.print("){f}{f}", .{ global.unnamed_addr, global.addr_space.fmt(" ") });
        if (function_attributes != .none) try w.print(" #{d}", .{
            (try attribute_groups.getOrPutValue(self.gpa, function_attributes, {})).index,
        });
        {
            metadata_formatter.need_comma = false;
            defer metadata_formatter.need_comma = undefined;
            try w.print("{f}{f}", .{
                function.alignment.fmt(" "),
                try metadata_formatter.fmt(" !dbg ", global.dbg, null),
            });
        }
        if (function.instructions.len > 0) {
            var block_incoming_len: u32 = undefined;
            try w.writeAll(" {\n");
            var maybe_dbg_index: ?u32 = null;
            for (params_len..function.instructions.len) |instruction_i| {
                const instruction_index: Function.Instruction.Index = @enumFromInt(instruction_i);
                const instruction = function.instructions.get(@intFromEnum(instruction_index));
                if (function.debug_locations.get(instruction_index)) |debug_location| switch (debug_location) {
                    .no_location => maybe_dbg_index = null,
                    .location => |location| {
                        const gop = try metadata_formatter.map.getOrPut(self.gpa, .{
                            .debug_location = location,
                        });
                        maybe_dbg_index = @intCast(gop.index);
                    },
                };
                switch (instruction.tag) {
                    .add,
                    .@"add nsw",
                    .@"add nuw",
                    .@"add nuw nsw",
                    .@"and",
                    .ashr,
                    .@"ashr exact",
                    .fadd,
                    .@"fadd fast",
                    .@"fcmp false",
                    .@"fcmp fast false",
                    .@"fcmp fast oeq",
                    .@"fcmp fast oge",
                    .@"fcmp fast ogt",
                    .@"fcmp fast ole",
                    .@"fcmp fast olt",
                    .@"fcmp fast one",
                    .@"fcmp fast ord",
                    .@"fcmp fast true",
                    .@"fcmp fast ueq",
                    .@"fcmp fast uge",
                    .@"fcmp fast ugt",
                    .@"fcmp fast ule",
                    .@"fcmp fast ult",
                    .@"fcmp fast une",
                    .@"fcmp fast uno",
                    .@"fcmp oeq",
                    .@"fcmp oge",
                    .@"fcmp ogt",
                    .@"fcmp ole",
                    .@"fcmp olt",
                    .@"fcmp one",
                    .@"fcmp ord",
                    .@"fcmp true",
                    .@"fcmp ueq",
                    .@"fcmp uge",
                    .@"fcmp ugt",
                    .@"fcmp ule",
                    .@"fcmp ult",
                    .@"fcmp une",
                    .@"fcmp uno",
                    .fdiv,
                    .@"fdiv fast",
                    .fmul,
                    .@"fmul fast",
                    .frem,
                    .@"frem fast",
                    .fsub,
                    .@"fsub fast",
                    .@"icmp eq",
                    .@"icmp ne",
                    .@"icmp sge",
                    .@"icmp sgt",
                    .@"icmp sle",
                    .@"icmp slt",
                    .@"icmp uge",
                    .@"icmp ugt",
                    .@"icmp ule",
                    .@"icmp ult",
                    .lshr,
                    .@"lshr exact",
                    .mul,
                    .@"mul nsw",
                    .@"mul nuw",
                    .@"mul nuw nsw",
                    .@"or",
                    .sdiv,
                    .@"sdiv exact",
                    .srem,
                    .shl,
                    .@"shl nsw",
                    .@"shl nuw",
                    .@"shl nuw nsw",
                    .sub,
                    .@"sub nsw",
                    .@"sub nuw",
                    .@"sub nuw nsw",
                    .udiv,
                    .@"udiv exact",
                    .urem,
                    .xor,
                    => |tag| {
                        const extra = function.extraData(Function.Instruction.Binary, instruction.data);
                        try w.print("  %{f} = {s} {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.lhs.fmt(function_index, self, .{ .percent = true }),
                            extra.rhs.fmt(function_index, self, .{}),
                        });
                    },
                    .addrspacecast,
                    .bitcast,
                    .fpext,
                    .fptosi,
                    .fptoui,
                    .fptrunc,
                    .inttoptr,
                    .ptrtoint,
                    .sext,
                    .sitofp,
                    .trunc,
                    .uitofp,
                    .zext,
                    => |tag| {
                        const extra = function.extraData(Function.Instruction.Cast, instruction.data);
                        try w.print("  %{f} = {s} {f} to {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.val.fmt(function_index, self, .{ .percent = true }),
                            extra.type.fmt(self, .percent),
                        });
                    },
                    .alloca,
                    .@"alloca inalloca",
                    => |tag| {
                        const extra = function.extraData(Function.Instruction.Alloca, instruction.data);
                        try w.print("  %{f} = {s} {f}{f}{f}{f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.type.fmt(self, .percent),
                            Value.fmt(switch (extra.len) {
                                .@"1" => .none,
                                else => extra.len,
                            }, function_index, self, .{
                                .comma = true,
                                .percent = true,
                            }),
                            extra.info.alignment.fmt(", "),
                            extra.info.addr_space.fmt(", "),
                        });
                    },
                    .arg => unreachable,
                    .atomicrmw => |tag| {
                        const extra =
                            function.extraData(Function.Instruction.AtomicRmw, instruction.data);
                        try w.print("  %{f} = {t}{f} {t} {f}, {f}{f}{f}{f}", .{
                            instruction_index.name(&function).fmt(self),
                            tag,
                            extra.info.access_kind.fmt(" "),
                            extra.info.atomic_rmw_operation,
                            extra.ptr.fmt(function_index, self, .{ .percent = true }),
                            extra.val.fmt(function_index, self, .{ .percent = true }),
                            extra.info.sync_scope.fmt(" "),
                            extra.info.success_ordering.fmt(" "),
                            extra.info.alignment.fmt(", "),
                        });
                    },
                    .block => {
                        block_incoming_len = instruction.data;
                        const name = instruction_index.name(&function);
                        if (@intFromEnum(instruction_index) > params_len)
                            try w.writeByte('\n');
                        try w.print("{f}:\n", .{name.fmt(self)});
                        continue;
                    },
                    .br => |tag| {
                        const target: Function.Block.Index = @enumFromInt(instruction.data);
                        try w.print("  {s} {f}", .{
                            @tagName(tag), target.toInst(&function).fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .br_cond => {
                        const extra = function.extraData(Function.Instruction.BrCond, instruction.data);
                        try w.print("  br {f}, {f}, {f}", .{
                            extra.cond.fmt(function_index, self, .{ .percent = true }),
                            extra.then.toInst(&function).fmt(function_index, self, .{ .percent = true }),
                            extra.@"else".toInst(&function).fmt(function_index, self, .{ .percent = true }),
                        });
                        metadata_formatter.need_comma = true;
                        defer metadata_formatter.need_comma = undefined;
                        switch (extra.weights) {
                            .none => {},
                            .unpredictable => try w.writeAll("!unpredictable !{}"),
                            _ => try w.print("{f}", .{
                                try metadata_formatter.fmt("!prof ", extra.weights.toMetadata(), null),
                            }),
                        }
                    },
                    .call,
                    .@"call fast",
                    .@"musttail call",
                    .@"musttail call fast",
                    .@"notail call",
                    .@"notail call fast",
                    .@"tail call",
                    .@"tail call fast",
                    => |tag| {
                        var extra =
                            function.extraDataTrail(Function.Instruction.Call, instruction.data);
                        const args = extra.trail.next(extra.data.args_len, Value, &function);
                        try w.writeAll("  ");
                        const ret_ty = extra.data.ty.functionReturn(self);
                        switch (ret_ty) {
                            .void => {},
                            else => try w.print("%{f} = ", .{
                                instruction_index.name(&function).fmt(self),
                            }),
                            .none => unreachable,
                        }
                        try w.print("{t}{f}{f}{f} {f} {f}(", .{
                            tag,
                            extra.data.info.call_conv,
                            extra.data.attributes.ret(self).fmt(self, .{}),
                            extra.data.callee.typeOf(function_index, self).pointerAddrSpace(self),
                            switch (extra.data.ty.functionKind(self)) {
                                .normal => ret_ty,
                                .vararg => extra.data.ty,
                            }.fmt(self, .percent),
                            extra.data.callee.fmt(function_index, self, .{}),
                        });
                        for (0.., args) |arg_index, arg| {
                            if (arg_index > 0) try w.writeAll(", ");
                            metadata_formatter.need_comma = false;
                            defer metadata_formatter.need_comma = undefined;
                            try w.print("{f}{f}{f}", .{
                                arg.typeOf(function_index, self).fmt(self, .percent),
                                extra.data.attributes.param(arg_index, self).fmt(self, .{}),
                                try metadata_formatter.fmtLocal(" ", arg, function_index),
                            });
                        }
                        try w.writeByte(')');
                        if (extra.data.info.has_op_bundle_cold) {
                            try w.writeAll(" [ \"cold\"() ]");
                        }
                        const call_function_attributes = extra.data.attributes.func(self);
                        if (call_function_attributes != .none) try w.print(" #{d}", .{
                            (try attribute_groups.getOrPutValue(
                                self.gpa,
                                call_function_attributes,
                                {},
                            )).index,
                        });
                    },
                    .cmpxchg,
                    .@"cmpxchg weak",
                    => |tag| {
                        const extra =
                            function.extraData(Function.Instruction.CmpXchg, instruction.data);
                        try w.print("  %{f} = {t}{f} {f}, {f}, {f}{f}{f}{f}{f}", .{
                            instruction_index.name(&function).fmt(self),
                            tag,
                            extra.info.access_kind.fmt(" "),
                            extra.ptr.fmt(function_index, self, .{ .percent = true }),
                            extra.cmp.fmt(function_index, self, .{ .percent = true }),
                            extra.new.fmt(function_index, self, .{ .percent = true }),
                            extra.info.sync_scope.fmt(" "),
                            extra.info.success_ordering.fmt(" "),
                            extra.info.failure_ordering.fmt(" "),
                            extra.info.alignment.fmt(", "),
                        });
                    },
                    .extractelement => |tag| {
                        const extra =
                            function.extraData(Function.Instruction.ExtractElement, instruction.data);
                        try w.print("  %{f} = {s} {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.val.fmt(function_index, self, .{ .percent = true }),
                            extra.index.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .extractvalue => |tag| {
                        var extra = function.extraDataTrail(
                            Function.Instruction.ExtractValue,
                            instruction.data,
                        );
                        const indices = extra.trail.next(extra.data.indices_len, u32, &function);
                        try w.print("  %{f} = {s} {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.data.val.fmt(function_index, self, .{ .percent = true }),
                        });
                        for (indices) |index| try w.print(", {d}", .{index});
                    },
                    .fence => |tag| {
                        const info: MemoryAccessInfo = @bitCast(instruction.data);
                        try w.print("  {t}{f}{f}", .{
                            tag,
                            info.sync_scope.fmt(" "),
                            info.success_ordering.fmt(" "),
                        });
                    },
                    .fneg,
                    .@"fneg fast",
                    => |tag| {
                        const val: Value = @enumFromInt(instruction.data);
                        try w.print("  %{f} = {s} {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            val.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .getelementptr,
                    .@"getelementptr inbounds",
                    => |tag| {
                        var extra = function.extraDataTrail(
                            Function.Instruction.GetElementPtr,
                            instruction.data,
                        );
                        const indices = extra.trail.next(extra.data.indices_len, Value, &function);
                        try w.print("  %{f} = {s} {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.data.type.fmt(self, .percent),
                            extra.data.base.fmt(function_index, self, .{ .percent = true }),
                        });
                        for (indices) |index| try w.print(", {f}", .{
                            index.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .indirectbr => |tag| {
                        var extra =
                            function.extraDataTrail(Function.Instruction.IndirectBr, instruction.data);
                        const targets =
                            extra.trail.next(extra.data.targets_len, Function.Block.Index, &function);
                        try w.print("  {s} {f}, [", .{
                            @tagName(tag),
                            extra.data.addr.fmt(function_index, self, .{ .percent = true }),
                        });
                        for (0.., targets) |target_index, target| {
                            if (target_index > 0) try w.writeAll(", ");
                            try w.print("{f}", .{
                                target.toInst(&function).fmt(function_index, self, .{ .percent = true }),
                            });
                        }
                        try w.writeByte(']');
                    },
                    .insertelement => |tag| {
                        const extra =
                            function.extraData(Function.Instruction.InsertElement, instruction.data);
                        try w.print("  %{f} = {s} {f}, {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.val.fmt(function_index, self, .{ .percent = true }),
                            extra.elem.fmt(function_index, self, .{ .percent = true }),
                            extra.index.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .insertvalue => |tag| {
                        var extra =
                            function.extraDataTrail(Function.Instruction.InsertValue, instruction.data);
                        const indices = extra.trail.next(extra.data.indices_len, u32, &function);
                        try w.print("  %{f} = {s} {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.data.val.fmt(function_index, self, .{ .percent = true }),
                            extra.data.elem.fmt(function_index, self, .{ .percent = true }),
                        });
                        for (indices) |index| try w.print(", {d}", .{index});
                    },
                    .load,
                    .@"load atomic",
                    => |tag| {
                        const extra = function.extraData(Function.Instruction.Load, instruction.data);
                        try w.print("  %{f} = {t}{f} {f}, {f}{f}{f}{f}", .{
                            instruction_index.name(&function).fmt(self),
                            tag,
                            extra.info.access_kind.fmt(" "),
                            extra.type.fmt(self, .percent),
                            extra.ptr.fmt(function_index, self, .{ .percent = true }),
                            extra.info.sync_scope.fmt(" "),
                            extra.info.success_ordering.fmt(" "),
                            extra.info.alignment.fmt(", "),
                        });
                    },
                    .phi,
                    .@"phi fast",
                    => |tag| {
                        var extra = function.extraDataTrail(Function.Instruction.Phi, instruction.data);
                        const vals = extra.trail.next(block_incoming_len, Value, &function);
                        const blocks =
                            extra.trail.next(block_incoming_len, Function.Block.Index, &function);
                        try w.print("  %{f} = {s} {f} ", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            vals[0].typeOf(function_index, self).fmt(self, .percent),
                        });
                        for (0.., vals, blocks) |incoming_index, incoming_val, incoming_block| {
                            if (incoming_index > 0) try w.writeAll(", ");
                            try w.print("[ {f}, {f} ]", .{
                                incoming_val.fmt(function_index, self, .{}),
                                incoming_block.toInst(&function).fmt(function_index, self, .{}),
                            });
                        }
                    },
                    .ret => |tag| {
                        const val: Value = @enumFromInt(instruction.data);
                        try w.print("  {s} {f}", .{
                            @tagName(tag),
                            val.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .@"ret void",
                    .@"unreachable",
                    => |tag| try w.print("  {s}", .{@tagName(tag)}),
                    .select,
                    .@"select fast",
                    => |tag| {
                        const extra = function.extraData(Function.Instruction.Select, instruction.data);
                        try w.print("  %{f} = {s} {f}, {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.cond.fmt(function_index, self, .{ .percent = true }),
                            extra.lhs.fmt(function_index, self, .{ .percent = true }),
                            extra.rhs.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .shufflevector => |tag| {
                        const extra =
                            function.extraData(Function.Instruction.ShuffleVector, instruction.data);
                        try w.print("  %{f} = {s} {f}, {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.lhs.fmt(function_index, self, .{ .percent = true }),
                            extra.rhs.fmt(function_index, self, .{ .percent = true }),
                            extra.mask.fmt(function_index, self, .{ .percent = true }),
                        });
                    },
                    .store,
                    .@"store atomic",
                    => |tag| {
                        const extra = function.extraData(Function.Instruction.Store, instruction.data);
                        try w.print("  {t}{f} {f}, {f}{f}{f}{f}", .{
                            tag,
                            extra.info.access_kind.fmt(" "),
                            extra.val.fmt(function_index, self, .{ .percent = true }),
                            extra.ptr.fmt(function_index, self, .{ .percent = true }),
                            extra.info.sync_scope.fmt(" "),
                            extra.info.success_ordering.fmt(" "),
                            extra.info.alignment.fmt(", "),
                        });
                    },
                    .@"switch" => |tag| {
                        var extra =
                            function.extraDataTrail(Function.Instruction.Switch, instruction.data);
                        const vals = extra.trail.next(extra.data.cases_len, Constant, &function);
                        const blocks =
                            extra.trail.next(extra.data.cases_len, Function.Block.Index, &function);
                        try w.print("  {s} {f}, {f} [\n", .{
                            @tagName(tag),
                            extra.data.val.fmt(function_index, self, .{ .percent = true }),
                            extra.data.default.toInst(&function).fmt(function_index, self, .{ .percent = true }),
                        });
                        for (vals, blocks) |case_val, case_block| try w.print(
                            "    {f}, {f}\n",
                            .{
                                case_val.fmt(self, .{ .percent = true }),
                                case_block.toInst(&function).fmt(function_index, self, .{ .percent = true }),
                            },
                        );
                        try w.writeAll("  ]");
                        metadata_formatter.need_comma = true;
                        defer metadata_formatter.need_comma = undefined;
                        switch (extra.data.weights) {
                            .none => {},
                            .unpredictable => try w.writeAll("!unpredictable !{}"),
                            _ => try w.print("{f}", .{
                                try metadata_formatter.fmt("!prof ", extra.data.weights.toMetadata(), null),
                            }),
                        }
                    },
                    .va_arg => |tag| {
                        const extra = function.extraData(Function.Instruction.VaArg, instruction.data);
                        try w.print("  %{f} = {s} {f}, {f}", .{
                            instruction_index.name(&function).fmt(self),
                            @tagName(tag),
                            extra.list.fmt(function_index, self, .{ .percent = true }),
                            extra.type.fmt(self, .percent),
                        });
                    },
                }

                if (maybe_dbg_index) |dbg_index| {
                    try w.print(", !dbg !{d}", .{dbg_index});
                }
                try w.writeByte('\n');
            }
            try w.writeByte('}');
        }
        try w.writeByte('\n');
    }

    if (attribute_groups.count() > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        for (0.., attribute_groups.keys()) |attribute_group_index, attribute_group|
            try w.print(
                \\attributes #{d} = {{{f} }}
                \\
            , .{ attribute_group_index, attribute_group.fmt(self, .{ .pound = true, .quote = true }) });
    }

    if (self.metadata_named.count() > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        for (self.metadata_named.keys(), self.metadata_named.values()) |name, data| {
            const elements: []const Metadata =
                @ptrCast(self.metadata_extra.items[data.index..][0..data.len]);
            try w.writeByte('!');
            try printEscapedString(name.slice(self).?, .quote_unless_valid_identifier, w);
            try w.writeAll(" = !{");
            metadata_formatter.need_comma = false;
            defer metadata_formatter.need_comma = undefined;
            for (elements) |element| try w.print("{f}", .{try metadata_formatter.fmt("", element, null)});
            try w.writeAll("}\n");
        }
    }

    if (metadata_formatter.map.count() > 0) {
        if (need_newline) try w.writeByte('\n') else need_newline = true;
        var metadata_index: usize = 0;
        while (metadata_index < metadata_formatter.map.count()) : (metadata_index += 1) {
            @setEvalBranchQuota(10_000);
            try w.print("!{d} = ", .{metadata_index});
            metadata_formatter.need_comma = false;
            defer metadata_formatter.need_comma = undefined;

            const key = metadata_formatter.map.keys()[metadata_index];
            const metadata_item = switch (key) {
                .debug_location => |location| {
                    try metadata_formatter.specialized(.@"!", .DILocation, .{
                        .line = location.line,
                        .column = location.column,
                        .scope = location.scope,
                        .inlin
```
