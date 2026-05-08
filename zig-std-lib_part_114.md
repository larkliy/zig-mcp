```
edAt = location.inlined_at,
                        .isImplicitCode = false,
                    }, w);
                    continue;
                },
                .metadata => |metadata| metadata.item(self),
            };

            switch (metadata_item.tag) {
                .expression, .constant => unreachable,
                .file => {
                    const extra = self.metadataExtraData(Metadata.File, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DIFile, .{
                        .filename = extra.filename,
                        .directory = extra.directory,
                        .checksumkind = null,
                        .checksum = null,
                        .source = null,
                    }, w);
                },
                .compile_unit,
                .@"compile_unit optimized",
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.CompileUnit, metadata_item.data);
                    try metadata_formatter.specialized(.@"distinct !", .DICompileUnit, .{
                        .language = .DW_LANG_C99,
                        .file = extra.file,
                        .producer = extra.producer,
                        .isOptimized = switch (kind) {
                            .compile_unit => false,
                            .@"compile_unit optimized" => true,
                            else => unreachable,
                        },
                        .flags = null,
                        .runtimeVersion = 0,
                        .splitDebugFilename = null,
                        .emissionKind = .FullDebug,
                        .enums = extra.enums,
                        .retainedTypes = null,
                        .globals = extra.globals,
                        .imports = null,
                        .macros = null,
                        .dwoId = null,
                        .splitDebugInlining = false,
                        .debugInfoForProfiling = null,
                        .nameTableKind = null,
                        .rangesBaseAddress = null,
                        .sysroot = null,
                        .sdk = null,
                    }, w);
                },
                .subprogram,
                .@"subprogram local",
                .@"subprogram definition",
                .@"subprogram local definition",
                .@"subprogram optimized",
                .@"subprogram optimized local",
                .@"subprogram optimized definition",
                .@"subprogram optimized local definition",
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.Subprogram, metadata_item.data);
                    try metadata_formatter.specialized(.@"distinct !", .DISubprogram, .{
                        .name = extra.name,
                        .linkageName = extra.linkage_name,
                        .scope = extra.file,
                        .file = extra.file,
                        .line = extra.line,
                        .type = extra.ty,
                        .scopeLine = extra.scope_line,
                        .containingType = null,
                        .virtualIndex = null,
                        .thisAdjustment = null,
                        .flags = extra.di_flags,
                        .spFlags = @as(Metadata.Subprogram.DISPFlags, @bitCast(@as(u32, @as(u3, @intCast(
                            @intFromEnum(kind) - @intFromEnum(Metadata.Tag.subprogram),
                        ))) << 2)),
                        .unit = extra.compile_unit,
                        .templateParams = null,
                        .declaration = null,
                        .retainedNodes = null,
                        .thrownTypes = null,
                        .annotations = null,
                        .targetFuncName = null,
                    }, w);
                },
                .lexical_block => {
                    const extra = self.metadataExtraData(Metadata.LexicalBlock, metadata_item.data);
                    try metadata_formatter.specialized(.@"distinct !", .DILexicalBlock, .{
                        .scope = extra.scope,
                        .file = extra.file,
                        .line = extra.line,
                        .column = extra.column,
                    }, w);
                },
                .location => {
                    const extra = self.metadataExtraData(Metadata.Location, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DILocation, .{
                        .line = extra.line,
                        .column = extra.column,
                        .scope = extra.scope,
                        .inlinedAt = extra.inlined_at,
                        .isImplicitCode = false,
                    }, w);
                },
                .basic_bool_type,
                .basic_unsigned_type,
                .basic_signed_type,
                .basic_float_type,
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.BasicType, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DIBasicType, .{
                        .tag = null,
                        .name = extra.name,
                        .size = extra.bitSize(),
                        .@"align" = null,
                        .encoding = @as(enum {
                            DW_ATE_boolean,
                            DW_ATE_unsigned,
                            DW_ATE_signed,
                            DW_ATE_float,
                        }, switch (kind) {
                            .basic_bool_type => .DW_ATE_boolean,
                            .basic_unsigned_type => .DW_ATE_unsigned,
                            .basic_signed_type => .DW_ATE_signed,
                            .basic_float_type => .DW_ATE_float,
                            else => unreachable,
                        }),
                        .flags = null,
                    }, w);
                },
                .composite_struct_type,
                .composite_union_type,
                .composite_enumeration_type,
                .composite_array_type,
                .composite_vector_type,
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.CompositeType, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DICompositeType, .{
                        .tag = @as(enum {
                            DW_TAG_structure_type,
                            DW_TAG_union_type,
                            DW_TAG_enumeration_type,
                            DW_TAG_array_type,
                        }, switch (kind) {
                            .composite_struct_type => .DW_TAG_structure_type,
                            .composite_union_type => .DW_TAG_union_type,
                            .composite_enumeration_type => .DW_TAG_enumeration_type,
                            .composite_array_type, .composite_vector_type => .DW_TAG_array_type,
                            else => unreachable,
                        }),
                        .name = extra.name,
                        .scope = extra.scope,
                        .file = null,
                        .line = null,
                        .baseType = extra.underlying_type,
                        .size = extra.bitSize(),
                        .@"align" = extra.bitAlign(),
                        .offset = null,
                        .flags = null,
                        .elements = extra.fields_tuple,
                        .runtimeLang = null,
                        .vtableHolder = null,
                        .templateParams = null,
                        .identifier = null,
                        .discriminator = null,
                        .dataLocation = null,
                        .associated = null,
                        .allocated = null,
                        .rank = null,
                        .annotations = null,
                    }, w);
                },
                .derived_pointer_type,
                .derived_member_type,
                .derived_typedef_type,
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.DerivedType, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DIDerivedType, .{
                        .tag = @as(enum {
                            DW_TAG_pointer_type,
                            DW_TAG_member,
                            DW_TAG_typedef,
                        }, switch (kind) {
                            .derived_pointer_type => .DW_TAG_pointer_type,
                            .derived_member_type => .DW_TAG_member,
                            .derived_typedef_type => .DW_TAG_typedef,
                            else => unreachable,
                        }),
                        .name = extra.name,
                        .scope = extra.scope,
                        .file = null,
                        .line = null,
                        .baseType = extra.underlying_type,
                        .size = extra.bitSize(),
                        .@"align" = extra.bitAlign(),
                        .offset = switch (extra.bitOffset()) {
                            0 => null,
                            else => |bit_offset| bit_offset,
                        },
                        .flags = null,
                        .extraData = null,
                        .dwarfAddressSpace = null,
                        .annotations = null,
                    }, w);
                },
                .subroutine_type => {
                    const extra = self.metadataExtraData(Metadata.SubroutineType, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DISubroutineType, .{
                        .flags = null,
                        .cc = null,
                        .types = extra.types_tuple,
                    }, w);
                },
                .enumerator_unsigned,
                .enumerator_signed_positive,
                .enumerator_signed_negative,
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.Enumerator, metadata_item.data);

                    const ExpectedContents = extern struct {
                        const expected_limbs = @divExact(512, @bitSizeOf(std.math.big.Limb));
                        string: [
                            (std.math.big.int.Const{
                                .limbs = &([1]std.math.big.Limb{
                                    maxInt(std.math.big.Limb),
                                } ** expected_limbs),
                                .positive = false,
                            }).sizeInBaseUpperBound(10)
                        ]u8,
                        limbs: [
                            std.math.big.int.calcToStringLimbsBufferLen(expected_limbs, 10)
                        ]std.math.big.Limb,
                    };
                    var stack align(@alignOf(ExpectedContents)) =
                        std.heap.stackFallback(@sizeOf(ExpectedContents), self.gpa);
                    const allocator = stack.get();

                    const limbs = self.metadata_limbs.items[extra.limbs_index..][0..extra.limbs_len];
                    const bigint: std.math.big.int.Const = .{
                        .limbs = limbs,
                        .positive = switch (kind) {
                            .enumerator_unsigned,
                            .enumerator_signed_positive,
                            => true,
                            .enumerator_signed_negative => false,
                            else => unreachable,
                        },
                    };
                    const str = try bigint.toStringAlloc(allocator, 10, undefined);
                    defer allocator.free(str);

                    try metadata_formatter.specialized(.@"!", .DIEnumerator, .{
                        .name = extra.name,
                        .value = str,
                        .isUnsigned = switch (kind) {
                            .enumerator_unsigned => true,
                            .enumerator_signed_positive,
                            .enumerator_signed_negative,
                            => false,
                            else => unreachable,
                        },
                    }, w);
                },
                .subrange => {
                    const extra = self.metadataExtraData(Metadata.Subrange, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DISubrange, .{
                        .count = extra.count,
                        .lowerBound = extra.lower_bound,
                        .upperBound = null,
                        .stride = null,
                    }, w);
                },
                .tuple => {
                    var extra = self.metadataExtraDataTrail(Metadata.Tuple, metadata_item.data);
                    const elements = extra.trail.next(extra.data.elements_len, Metadata, self);
                    try w.writeAll("!{");
                    for (elements) |element| try w.print("{[element]f}", .{
                        .element = try metadata_formatter.fmt("", element, .{ .percent = true }),
                    });
                    try w.writeAll("}\n");
                },
                .local_var => {
                    const extra = self.metadataExtraData(Metadata.LocalVar, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DILocalVariable, .{
                        .name = extra.name,
                        .arg = null,
                        .scope = extra.scope,
                        .file = extra.file,
                        .line = extra.line,
                        .type = extra.ty,
                        .flags = null,
                        .@"align" = null,
                        .annotations = null,
                    }, w);
                },
                .parameter => {
                    const extra = self.metadataExtraData(Metadata.Parameter, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DILocalVariable, .{
                        .name = extra.name,
                        .arg = extra.arg_no,
                        .scope = extra.scope,
                        .file = extra.file,
                        .line = extra.line,
                        .type = extra.ty,
                        .flags = null,
                        .@"align" = null,
                        .annotations = null,
                    }, w);
                },
                .global_var,
                .@"global_var local",
                => |kind| {
                    const extra = self.metadataExtraData(Metadata.GlobalVar, metadata_item.data);
                    try metadata_formatter.specialized(.@"distinct !", .DIGlobalVariable, .{
                        .name = extra.name,
                        .linkageName = extra.linkage_name,
                        .scope = extra.scope,
                        .file = extra.file,
                        .line = extra.line,
                        .type = extra.ty,
                        .isLocal = switch (kind) {
                            .global_var => false,
                            .@"global_var local" => true,
                            else => unreachable,
                        },
                        .isDefinition = true,
                        .declaration = null,
                        .templateParams = null,
                        .@"align" = null,
                        .annotations = null,
                    }, w);
                },
                .global_var_expression => {
                    const extra =
                        self.metadataExtraData(Metadata.GlobalVarExpression, metadata_item.data);
                    try metadata_formatter.specialized(.@"!", .DIGlobalVariableExpression, .{
                        .@"var" = extra.variable,
                        .expr = extra.expression,
                    }, w);
                },
            }
        }
    }
}

const NoExtra = struct {};

fn isValidIdentifier(id: []const u8) bool {
    for (id, 0..) |byte, index| switch (byte) {
        '$', '-', '.', 'A'...'Z', '_', 'a'...'z' => {},
        '0'...'9' => if (index == 0) return false,
        else => return false,
    };
    return true;
}

const QuoteBehavior = enum { always_quote, quote_unless_valid_identifier };
fn printEscapedString(slice: []const u8, quotes: QuoteBehavior, w: *Writer) Writer.Error!void {
    const need_quotes = switch (quotes) {
        .always_quote => true,
        .quote_unless_valid_identifier => !isValidIdentifier(slice),
    };
    if (need_quotes) try w.writeByte('"');
    for (slice) |byte| switch (byte) {
        '\\' => try w.writeAll("\\\\"),
        ' '...'"' - 1, '"' + 1...'\\' - 1, '\\' + 1...'~' => try w.writeByte(byte),
        else => try w.print("\\{X:0>2}", .{byte}),
    };
    if (need_quotes) try w.writeByte('"');
}

fn ensureUnusedGlobalCapacity(self: *Builder, name: StrtabString) Allocator.Error!void {
    try self.strtab_string_map.ensureUnusedCapacity(self.gpa, 1);
    if (name.slice(self)) |id| {
        const count: usize = comptime std.fmt.count("{d}", .{maxInt(u32)});
        try self.strtab_string_bytes.ensureUnusedCapacity(self.gpa, id.len + count);
    }
    try self.strtab_string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.globals.ensureUnusedCapacity(self.gpa, 1);
    try self.next_unique_global_id.ensureUnusedCapacity(self.gpa, 1);
}

fn fnTypeAssumeCapacity(
    self: *Builder,
    ret: Type,
    params: []const Type,
    comptime kind: Type.Function.Kind,
) Type {
    const tag: Type.Tag = switch (kind) {
        .normal => .function,
        .vararg => .vararg_function,
    };
    const Key = struct { ret: Type, params: []const Type };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = std.hash.Wyhash.init(comptime std.hash.int(@intFromEnum(tag)));
            hasher.update(std.mem.asBytes(&key.ret));
            hasher.update(std.mem.sliceAsBytes(key.params));
            return @truncate(hasher.final());
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            const rhs_data = ctx.builder.type_items.items[rhs_index];
            if (rhs_data.tag != tag) return false;
            var rhs_extra = ctx.builder.typeExtraDataTrail(Type.Function, rhs_data.data);
            const rhs_params = rhs_extra.trail.next(rhs_extra.data.params_len, Type, ctx.builder);
            return lhs_key.ret == rhs_extra.data.ret and std.mem.eql(Type, lhs_key.params, rhs_params);
        }
    };
    const gop = self.type_map.getOrPutAssumeCapacityAdapted(
        Key{ .ret = ret, .params = params },
        Adapter{ .builder = self },
    );
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.type_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addTypeExtraAssumeCapacity(Type.Function{
                .ret = ret,
                .params_len = @intCast(params.len),
            }),
        });
        self.type_extra.appendSliceAssumeCapacity(@ptrCast(params));
    }
    return @enumFromInt(gop.index);
}

fn intTypeAssumeCapacity(self: *Builder, bits: u24) Type {
    assert(bits > 0);
    const result = self.getOrPutTypeNoExtraAssumeCapacity(.{ .tag = .integer, .data = bits });
    return result.type;
}

fn ptrTypeAssumeCapacity(self: *Builder, addr_space: AddrSpace) Type {
    const result = self.getOrPutTypeNoExtraAssumeCapacity(
        .{ .tag = .pointer, .data = @intFromEnum(addr_space) },
    );
    return result.type;
}

fn vectorTypeAssumeCapacity(
    self: *Builder,
    comptime kind: Type.Vector.Kind,
    len: u32,
    child: Type,
) Type {
    assert(child.isFloatingPoint() or child.isInteger(self) or child.isPointer(self));
    const tag: Type.Tag = switch (kind) {
        .normal => .vector,
        .scalable => .scalable_vector,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Type.Vector) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(tag)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Type.Vector, _: void, rhs_index: usize) bool {
            const rhs_data = ctx.builder.type_items.items[rhs_index];
            return rhs_data.tag == tag and
                std.meta.eql(lhs_key, ctx.builder.typeExtraData(Type.Vector, rhs_data.data));
        }
    };
    const data = Type.Vector{ .len = len, .child = child };
    const gop = self.type_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.type_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addTypeExtraAssumeCapacity(data),
        });
    }
    return @enumFromInt(gop.index);
}

fn arrayTypeAssumeCapacity(self: *Builder, len: u64, child: Type) Type {
    if (std.math.cast(u32, len)) |small_len| {
        const Adapter = struct {
            builder: *const Builder,
            pub fn hash(_: @This(), key: Type.Vector) u32 {
                return @truncate(std.hash.Wyhash.hash(
                    comptime std.hash.int(@intFromEnum(Type.Tag.small_array)),
                    std.mem.asBytes(&key),
                ));
            }
            pub fn eql(ctx: @This(), lhs_key: Type.Vector, _: void, rhs_index: usize) bool {
                const rhs_data = ctx.builder.type_items.items[rhs_index];
                return rhs_data.tag == .small_array and
                    std.meta.eql(lhs_key, ctx.builder.typeExtraData(Type.Vector, rhs_data.data));
            }
        };
        const data = Type.Vector{ .len = small_len, .child = child };
        const gop = self.type_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
        if (!gop.found_existing) {
            gop.key_ptr.* = {};
            gop.value_ptr.* = {};
            self.type_items.appendAssumeCapacity(.{
                .tag = .small_array,
                .data = self.addTypeExtraAssumeCapacity(data),
            });
        }
        return @enumFromInt(gop.index);
    } else {
        const Adapter = struct {
            builder: *const Builder,
            pub fn hash(_: @This(), key: Type.Array) u32 {
                return @truncate(std.hash.Wyhash.hash(
                    comptime std.hash.int(@intFromEnum(Type.Tag.array)),
                    std.mem.asBytes(&key),
                ));
            }
            pub fn eql(ctx: @This(), lhs_key: Type.Array, _: void, rhs_index: usize) bool {
                const rhs_data = ctx.builder.type_items.items[rhs_index];
                return rhs_data.tag == .array and
                    std.meta.eql(lhs_key, ctx.builder.typeExtraData(Type.Array, rhs_data.data));
            }
        };
        const data = Type.Array{
            .len_lo = @truncate(len),
            .len_hi = @intCast(len >> 32),
            .child = child,
        };
        const gop = self.type_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
        if (!gop.found_existing) {
            gop.key_ptr.* = {};
            gop.value_ptr.* = {};
            self.type_items.appendAssumeCapacity(.{
                .tag = .array,
                .data = self.addTypeExtraAssumeCapacity(data),
            });
        }
        return @enumFromInt(gop.index);
    }
}

fn structTypeAssumeCapacity(
    self: *Builder,
    comptime kind: Type.Structure.Kind,
    fields: []const Type,
) Type {
    const tag: Type.Tag = switch (kind) {
        .normal => .structure,
        .@"packed" => .packed_structure,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: []const Type) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(tag)),
                std.mem.sliceAsBytes(key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: []const Type, _: void, rhs_index: usize) bool {
            const rhs_data = ctx.builder.type_items.items[rhs_index];
            if (rhs_data.tag != tag) return false;
            var rhs_extra = ctx.builder.typeExtraDataTrail(Type.Structure, rhs_data.data);
            const rhs_fields = rhs_extra.trail.next(rhs_extra.data.fields_len, Type, ctx.builder);
            return std.mem.eql(Type, lhs_key, rhs_fields);
        }
    };
    const gop = self.type_map.getOrPutAssumeCapacityAdapted(fields, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.type_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addTypeExtraAssumeCapacity(Type.Structure{
                .fields_len = @intCast(fields.len),
            }),
        });
        self.type_extra.appendSliceAssumeCapacity(@ptrCast(fields));
    }
    return @enumFromInt(gop.index);
}

fn opaqueTypeAssumeCapacity(self: *Builder, name: String) Type {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: String) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Type.Tag.named_structure)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: String, _: void, rhs_index: usize) bool {
            const rhs_data = ctx.builder.type_items.items[rhs_index];
            return rhs_data.tag == .named_structure and
                lhs_key == ctx.builder.typeExtraData(Type.NamedStructure, rhs_data.data).id;
        }
    };
    var id = name;
    if (name == .empty) {
        id = self.next_unnamed_type;
        assert(id != .none);
        self.next_unnamed_type = @enumFromInt(@intFromEnum(id) + 1);
    } else assert(!name.isAnon());
    while (true) {
        const type_gop = self.types.getOrPutAssumeCapacity(id);
        if (!type_gop.found_existing) {
            const gop = self.type_map.getOrPutAssumeCapacityAdapted(id, Adapter{ .builder = self });
            assert(!gop.found_existing);
            gop.key_ptr.* = {};
            gop.value_ptr.* = {};
            self.type_items.appendAssumeCapacity(.{
                .tag = .named_structure,
                .data = self.addTypeExtraAssumeCapacity(Type.NamedStructure{
                    .id = id,
                    .body = .none,
                }),
            });
            const result: Type = @enumFromInt(gop.index);
            type_gop.value_ptr.* = result;
            return result;
        }

        const unique_gop = self.next_unique_type_id.getOrPutAssumeCapacity(name);
        if (!unique_gop.found_existing) unique_gop.value_ptr.* = 2;
        id = self.fmtAssumeCapacity("{s}.{d}", .{ name.slice(self).?, unique_gop.value_ptr.* });
        unique_gop.value_ptr.* += 1;
    }
}

fn ensureUnusedTypeCapacity(
    self: *Builder,
    count: usize,
    comptime Extra: type,
    trail_len: usize,
) Allocator.Error!void {
    try self.type_map.ensureUnusedCapacity(self.gpa, count);
    try self.type_items.ensureUnusedCapacity(self.gpa, count);
    try self.type_extra.ensureUnusedCapacity(
        self.gpa,
        count * (@typeInfo(Extra).@"struct".fields.len + trail_len),
    );
}

fn getOrPutTypeNoExtraAssumeCapacity(self: *Builder, item: Type.Item) struct { new: bool, type: Type } {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Type.Item) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Type.Tag.simple)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Type.Item, _: void, rhs_index: usize) bool {
            const lhs_bits: u32 = @bitCast(lhs_key);
            const rhs_bits: u32 = @bitCast(ctx.builder.type_items.items[rhs_index]);
            return lhs_bits == rhs_bits;
        }
    };
    const gop = self.type_map.getOrPutAssumeCapacityAdapted(item, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.type_items.appendAssumeCapacity(item);
    }
    return .{ .new = !gop.found_existing, .type = @enumFromInt(gop.index) };
}

fn addTypeExtraAssumeCapacity(self: *Builder, extra: anytype) Type.Item.ExtraIndex {
    const result: Type.Item.ExtraIndex = @intCast(self.type_extra.items.len);
    inline for (@typeInfo(@TypeOf(extra)).@"struct".fields) |field| {
        const value = @field(extra, field.name);
        self.type_extra.appendAssumeCapacity(switch (field.type) {
            u32 => value,
            String, Type => @intFromEnum(value),
            else => @compileError("bad field type: " ++ field.name ++ ": " ++ @typeName(field.type)),
        });
    }
    return result;
}

const TypeExtraDataTrail = struct {
    index: Type.Item.ExtraIndex,

    fn nextMut(self: *TypeExtraDataTrail, len: u32, comptime Item: type, builder: *Builder) []Item {
        const items: []Item = @ptrCast(builder.type_extra.items[self.index..][0..len]);
        self.index += @intCast(len);
        return items;
    }

    fn next(
        self: *TypeExtraDataTrail,
        len: u32,
        comptime Item: type,
        builder: *const Builder,
    ) []const Item {
        const items: []const Item = @ptrCast(builder.type_extra.items[self.index..][0..len]);
        self.index += @intCast(len);
        return items;
    }
};

fn typeExtraDataTrail(
    self: *const Builder,
    comptime T: type,
    index: Type.Item.ExtraIndex,
) struct { data: T, trail: TypeExtraDataTrail } {
    var result: T = undefined;
    const fields = @typeInfo(T).@"struct".fields;
    inline for (fields, self.type_extra.items[index..][0..fields.len]) |field, value|
        @field(result, field.name) = switch (field.type) {
            u32 => value,
            String, Type => @enumFromInt(value),
            else => @compileError("bad field type: " ++ @typeName(field.type)),
        };
    return .{
        .data = result,
        .trail = .{ .index = index + @as(Type.Item.ExtraIndex, @intCast(fields.len)) },
    };
}

fn typeExtraData(self: *const Builder, comptime T: type, index: Type.Item.ExtraIndex) T {
    return self.typeExtraDataTrail(T, index).data;
}

fn attrGeneric(self: *Builder, data: []const u32) Allocator.Error!u32 {
    try self.attributes_map.ensureUnusedCapacity(self.gpa, 1);
    try self.attributes_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.attributes_extra.ensureUnusedCapacity(self.gpa, data.len);

    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: []const u32) u32 {
            return @truncate(std.hash.Wyhash.hash(1, std.mem.sliceAsBytes(key)));
        }
        pub fn eql(ctx: @This(), lhs_key: []const u32, _: void, rhs_index: usize) bool {
            const start = ctx.builder.attributes_indices.items[rhs_index];
            const end = ctx.builder.attributes_indices.items[rhs_index + 1];
            return std.mem.eql(u32, lhs_key, ctx.builder.attributes_extra.items[start..end]);
        }
    };
    const gop = self.attributes_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        self.attributes_extra.appendSliceAssumeCapacity(data);
        self.attributes_indices.appendAssumeCapacity(@intCast(self.attributes_extra.items.len));
    }
    return @intCast(gop.index);
}

fn bigIntConstAssumeCapacity(
    self: *Builder,
    ty: Type,
    value: std.math.big.int.Const,
) Allocator.Error!Constant {
    const type_item = self.type_items.items[@intFromEnum(ty)];
    assert(type_item.tag == .integer);
    const bits = type_item.data;

    const ExpectedContents = [64 / @sizeOf(std.math.big.Limb)]std.math.big.Limb;
    var stack align(@alignOf(ExpectedContents)) =
        std.heap.stackFallback(@sizeOf(ExpectedContents), self.gpa);
    const allocator = stack.get();

    var limbs: []std.math.big.Limb = &.{};
    defer allocator.free(limbs);
    const canonical_value = if (value.fitsInTwosComp(.signed, bits)) value else canon: {
        assert(value.fitsInTwosComp(.unsigned, bits));
        limbs = try allocator.alloc(std.math.big.Limb, std.math.big.int.calcTwosCompLimbCount(bits));
        var temp_value = std.math.big.int.Mutable.init(limbs, 0);
        temp_value.truncate(value, .signed, bits);
        break :canon temp_value.toConst();
    };
    assert(canonical_value.fitsInTwosComp(.signed, bits));

    const ExtraPtr = *align(@alignOf(std.math.big.Limb)) Constant.Integer;
    const Key = struct { tag: Constant.Tag, type: Type, limbs: []const std.math.big.Limb };
    const tag: Constant.Tag = switch (canonical_value.positive) {
        true => .positive_integer,
        false => .negative_integer,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = std.hash.Wyhash.init(std.hash.int(@intFromEnum(key.tag)));
            hasher.update(std.mem.asBytes(&key.type));
            hasher.update(std.mem.sliceAsBytes(key.limbs));
            return @truncate(hasher.final());
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.constant_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra: ExtraPtr =
                @ptrCast(ctx.builder.constant_limbs.items[rhs_data..][0..Constant.Integer.limbs]);
            const rhs_limbs = ctx.builder.constant_limbs
                .items[rhs_data + Constant.Integer.limbs ..][0..rhs_extra.limbs_len];
            return lhs_key.type == rhs_extra.type and
                std.mem.eql(std.math.big.Limb, lhs_key.limbs, rhs_limbs);
        }
    };

    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(
        Key{ .tag = tag, .type = ty, .limbs = canonical_value.limbs },
        Adapter{ .builder = self },
    );
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = @intCast(self.constant_limbs.items.len),
        });
        const extra: ExtraPtr =
            @ptrCast(self.constant_limbs.addManyAsArrayAssumeCapacity(Constant.Integer.limbs));
        extra.* = .{ .type = ty, .limbs_len = @intCast(canonical_value.limbs.len) };
        self.constant_limbs.appendSliceAssumeCapacity(canonical_value.limbs);
    }
    return @enumFromInt(gop.index);
}

fn halfConstAssumeCapacity(self: *Builder, val: f16) Constant {
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .half, .data = @as(u16, @bitCast(val)) },
    );
    return result.constant;
}

fn bfloatConstAssumeCapacity(self: *Builder, val: f32) Constant {
    assert(@as(u16, @truncate(@as(u32, @bitCast(val)))) == 0);
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .bfloat, .data = @bitCast(val) },
    );
    return result.constant;
}

fn floatConstAssumeCapacity(self: *Builder, val: f32) Constant {
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .float, .data = @bitCast(val) },
    );
    return result.constant;
}

fn doubleConstAssumeCapacity(self: *Builder, val: f64) Constant {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: f64) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Constant.Tag.double)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: f64, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != .double) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Double, rhs_data);
            return @as(u64, @bitCast(lhs_key)) == @as(u64, rhs_extra.hi) << 32 | rhs_extra.lo;
        }
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(val, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = .double,
            .data = self.addConstantExtraAssumeCapacity(Constant.Double{
                .lo = @truncate(@as(u64, @bitCast(val))),
                .hi = @intCast(@as(u64, @bitCast(val)) >> 32),
            }),
        });
    }
    return @enumFromInt(gop.index);
}

fn fp128ConstAssumeCapacity(self: *Builder, val: f128) Constant {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: f128) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Constant.Tag.fp128)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: f128, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != .fp128) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Fp128, rhs_data);
            return @as(u128, @bitCast(lhs_key)) == @as(u128, rhs_extra.hi_hi) << 96 |
                @as(u128, rhs_extra.hi_lo) << 64 | @as(u128, rhs_extra.lo_hi) << 32 | rhs_extra.lo_lo;
        }
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(val, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = .fp128,
            .data = self.addConstantExtraAssumeCapacity(Constant.Fp128{
                .lo_lo = @truncate(@as(u128, @bitCast(val))),
                .lo_hi = @truncate(@as(u128, @bitCast(val)) >> 32),
                .hi_lo = @truncate(@as(u128, @bitCast(val)) >> 64),
                .hi_hi = @intCast(@as(u128, @bitCast(val)) >> 96),
            }),
        });
    }
    return @enumFromInt(gop.index);
}

fn x86_fp80ConstAssumeCapacity(self: *Builder, val: f80) Constant {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: f80) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Constant.Tag.x86_fp80)),
                std.mem.asBytes(&key)[0..10],
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: f80, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != .x86_fp80) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Fp80, rhs_data);
            return @as(u80, @bitCast(lhs_key)) == @as(u80, rhs_extra.hi) << 64 |
                @as(u80, rhs_extra.lo_hi) << 32 | rhs_extra.lo_lo;
        }
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(val, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = .x86_fp80,
            .data = self.addConstantExtraAssumeCapacity(Constant.Fp80{
                .lo_lo = @truncate(@as(u80, @bitCast(val))),
                .lo_hi = @truncate(@as(u80, @bitCast(val)) >> 32),
                .hi = @intCast(@as(u80, @bitCast(val)) >> 64),
            }),
        });
    }
    return @enumFromInt(gop.index);
}

fn ppc_fp128ConstAssumeCapacity(self: *Builder, val: [2]f64) Constant {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: [2]f64) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Constant.Tag.ppc_fp128)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: [2]f64, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != .ppc_fp128) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Fp128, rhs_data);
            return @as(u64, @bitCast(lhs_key[0])) == @as(u64, rhs_extra.lo_hi) << 32 | rhs_extra.lo_lo and
                @as(u64, @bitCast(lhs_key[1])) == @as(u64, rhs_extra.hi_hi) << 32 | rhs_extra.hi_lo;
        }
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(val, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = .ppc_fp128,
            .data = self.addConstantExtraAssumeCapacity(Constant.Fp128{
                .lo_lo = @truncate(@as(u64, @bitCast(val[0]))),
                .lo_hi = @intCast(@as(u64, @bitCast(val[0])) >> 32),
                .hi_lo = @truncate(@as(u64, @bitCast(val[1]))),
                .hi_hi = @intCast(@as(u64, @bitCast(val[1])) >> 32),
            }),
        });
    }
    return @enumFromInt(gop.index);
}

fn nullConstAssumeCapacity(self: *Builder, ty: Type) Constant {
    assert(self.type_items.items[@intFromEnum(ty)].tag == .pointer);
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .null, .data = @intFromEnum(ty) },
    );
    return result.constant;
}

fn noneConstAssumeCapacity(self: *Builder, ty: Type) Constant {
    assert(ty == .token);
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .none, .data = @intFromEnum(ty) },
    );
    return result.constant;
}

fn structConstAssumeCapacity(self: *Builder, ty: Type, vals: []const Constant) Constant {
    const type_item = self.type_items.items[@intFromEnum(ty)];
    var extra = self.typeExtraDataTrail(Type.Structure, switch (type_item.tag) {
        .structure, .packed_structure => type_item.data,
        .named_structure => data: {
            const body_ty = self.typeExtraData(Type.NamedStructure, type_item.data).body;
            const body_item = self.type_items.items[@intFromEnum(body_ty)];
            switch (body_item.tag) {
                .structure, .packed_structure => break :data body_item.data,
                else => unreachable,
            }
        },
        else => unreachable,
    });
    const fields = extra.trail.next(extra.data.fields_len, Type, self);
    for (fields, vals) |field, val| assert(field == val.typeOf(self));

    for (vals) |val| {
        if (!val.isZeroInit(self)) break;
    } else return self.zeroInitConstAssumeCapacity(ty);

    const tag: Constant.Tag = switch (ty.unnamedTag(self)) {
        .structure => .structure,
        .packed_structure => .packed_structure,
        else => unreachable,
    };
    const result = self.getOrPutConstantAggregateAssumeCapacity(tag, ty, vals);
    return result.constant;
}

fn arrayConstAssumeCapacity(self: *Builder, ty: Type, vals: []const Constant) Constant {
    const type_item = self.type_items.items[@intFromEnum(ty)];
    const type_extra: struct { len: u64, child: Type } = switch (type_item.tag) {
        inline .small_array, .array => |kind| extra: {
            const extra = self.typeExtraData(switch (kind) {
                .small_array => Type.Vector,
                .array => Type.Array,
                else => unreachable,
            }, type_item.data);
            break :extra .{ .len = extra.length(), .child = extra.child };
        },
        else => unreachable,
    };
    assert(type_extra.len == vals.len);
    for (vals) |val| assert(type_extra.child == val.typeOf(self));

    for (vals) |val| {
        if (!val.isZeroInit(self)) break;
    } else return self.zeroInitConstAssumeCapacity(ty);

    const result = self.getOrPutConstantAggregateAssumeCapacity(.array, ty, vals);
    return result.constant;
}

fn stringConstAssumeCapacity(self: *Builder, val: String) Constant {
    const slice = val.slice(self).?;
    const ty = self.arrayTypeAssumeCapacity(slice.len, .i8);
    if (std.mem.allEqual(u8, slice, 0)) return self.zeroInitConstAssumeCapacity(ty);
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .string, .data = @intFromEnum(val) },
    );
    return result.constant;
}

fn vectorConstAssumeCapacity(self: *Builder, ty: Type, vals: []const Constant) Constant {
    assert(ty.isVector(self));
    assert(ty.vectorLen(self) == vals.len);
    for (vals) |val| assert(ty.childType(self) == val.typeOf(self));

    for (vals[1..]) |val| {
        if (vals[0] != val) break;
    } else return self.splatConstAssumeCapacity(ty, vals[0]);
    for (vals) |val| {
        if (!val.isZeroInit(self)) break;
    } else return self.zeroInitConstAssumeCapacity(ty);

    const result = self.getOrPutConstantAggregateAssumeCapacity(.vector, ty, vals);
    return result.constant;
}

fn splatConstAssumeCapacity(self: *Builder, ty: Type, val: Constant) Constant {
    assert(ty.scalarType(self) == val.typeOf(self));

    if (!ty.isVector(self)) return val;
    if (val.isZeroInit(self)) return self.zeroInitConstAssumeCapacity(ty);

    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Constant.Splat) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Constant.Tag.splat)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Constant.Splat, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != .splat) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Splat, rhs_data);
            return std.meta.eql(lhs_key, rhs_extra);
        }
    };
    const data = Constant.Splat{ .type = ty, .value = val };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = .splat,
            .data = self.addConstantExtraAssumeCapacity(data),
        });
    }
    return @enumFromInt(gop.index);
}

fn zeroInitConstAssumeCapacity(self: *Builder, ty: Type) Constant {
    switch (ty) {
        inline .half,
        .bfloat,
        .float,
        .double,
        .fp128,
        .x86_fp80,
        => |tag| return @field(Builder, @tagName(tag) ++ "ConstAssumeCapacity")(self, 0.0),
        .ppc_fp128 => return self.ppc_fp128ConstAssumeCapacity(.{ 0.0, 0.0 }),
        .token => return .none,
        .i1 => return .false,
        else => switch (self.type_items.items[@intFromEnum(ty)].tag) {
            .simple,
            .function,
            .vararg_function,
            => unreachable,
            .integer => {
                var limbs: [std.math.big.int.calcLimbLen(0)]std.math.big.Limb = undefined;
                const bigint = std.math.big.int.Mutable.init(&limbs, 0);
                return self.bigIntConstAssumeCapacity(ty, bigint.toConst()) catch unreachable;
            },
            .pointer => return self.nullConstAssumeCapacity(ty),
            .target,
            .vector,
            .scalable_vector,
            .small_array,
            .array,
            .structure,
            .packed_structure,
            .named_structure,
            => {},
        },
    }
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .zeroinitializer, .data = @intFromEnum(ty) },
    );
    return result.constant;
}

fn undefConstAssumeCapacity(self: *Builder, ty: Type) Constant {
    switch (self.type_items.items[@intFromEnum(ty)].tag) {
        .simple => switch (ty) {
            .void, .label => unreachable,
            else => {},
        },
        .function, .vararg_function => unreachable,
        else => {},
    }
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .undef, .data = @intFromEnum(ty) },
    );
    return result.constant;
}

fn poisonConstAssumeCapacity(self: *Builder, ty: Type) Constant {
    switch (self.type_items.items[@intFromEnum(ty)].tag) {
        .simple => switch (ty) {
            .void, .label => unreachable,
            else => {},
        },
        .function, .vararg_function => unreachable,
        else => {},
    }
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .poison, .data = @intFromEnum(ty) },
    );
    return result.constant;
}

fn blockAddrConstAssumeCapacity(
    self: *Builder,
    function: Function.Index,
    block: Function.Block.Index,
) Constant {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Constant.BlockAddress) u32 {
            return @truncate(std.hash.Wyhash.hash(
                comptime std.hash.int(@intFromEnum(Constant.Tag.blockaddress)),
                std.mem.asBytes(&key),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Constant.BlockAddress, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != .blockaddress) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.BlockAddress, rhs_data);
            return std.meta.eql(lhs_key, rhs_extra);
        }
    };
    const data = Constant.BlockAddress{ .function = function, .block = block };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = .blockaddress,
            .data = self.addConstantExtraAssumeCapacity(data),
        });
    }
    return @enumFromInt(gop.index);
}

fn dsoLocalEquivalentConstAssumeCapacity(self: *Builder, function: Function.Index) Constant {
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .dso_local_equivalent, .data = @intFromEnum(function) },
    );
    return result.constant;
}

fn noCfiConstAssumeCapacity(self: *Builder, function: Function.Index) Constant {
    const result = self.getOrPutConstantNoExtraAssumeCapacity(
        .{ .tag = .no_cfi, .data = @intFromEnum(function) },
    );
    return result.constant;
}

fn convTag(
    self: *Builder,
    signedness: Constant.Cast.Signedness,
    val_ty: Type,
    ty: Type,
) Function.Instruction.Tag {
    assert(val_ty != ty);
    return switch (val_ty.scalarTag(self)) {
        .simple => switch (ty.scalarTag(self)) {
            .simple => switch (std.math.order(val_ty.scalarBits(self), ty.scalarBits(self))) {
                .lt => .fpext,
                .eq => unreachable,
                .gt => .fptrunc,
            },
            .integer => switch (signedness) {
                .unsigned => .fptoui,
                .signed => .fptosi,
                .unneeded => unreachable,
            },
            else => unreachable,
        },
        .integer => switch (ty.scalarTag(self)) {
            .simple => switch (signedness) {
                .unsigned => .uitofp,
                .signed => .sitofp,
                .unneeded => unreachable,
            },
            .integer => switch (std.math.order(val_ty.scalarBits(self), ty.scalarBits(self))) {
                .lt => switch (signedness) {
                    .unsigned => .zext,
                    .signed => .sext,
                    .unneeded => unreachable,
                },
                .eq => unreachable,
                .gt => .trunc,
            },
            .pointer => .inttoptr,
            else => unreachable,
        },
        .pointer => switch (ty.scalarTag(self)) {
            .integer => .ptrtoint,
            .pointer => .addrspacecast,
            else => unreachable,
        },
        else => unreachable,
    };
}

fn convConstTag(
    self: *Builder,
    val_ty: Type,
    ty: Type,
) Constant.Tag {
    assert(val_ty != ty);
    return switch (val_ty.scalarTag(self)) {
        .integer => switch (ty.scalarTag(self)) {
            .integer => switch (std.math.order(val_ty.scalarBits(self), ty.scalarBits(self))) {
                .gt => .trunc,
                else => unreachable,
            },
            .pointer => .inttoptr,
            else => unreachable,
        },
        .pointer => switch (ty.scalarTag(self)) {
            .integer => .ptrtoint,
            .pointer => .addrspacecast,
            else => unreachable,
        },
        else => unreachable,
    };
}

fn convConstAssumeCapacity(
    self: *Builder,
    val: Constant,
    ty: Type,
) Constant {
    const val_ty = val.typeOf(self);
    if (val_ty == ty) return val;
    return self.castConstAssumeCapacity(self.convConstTag(val_ty, ty), val, ty);
}

fn castConstAssumeCapacity(self: *Builder, tag: Constant.Tag, val: Constant, ty: Type) Constant {
    const Key = struct { tag: Constant.Tag, cast: Constant.Cast };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            return @truncate(std.hash.Wyhash.hash(
                std.hash.int(@intFromEnum(key.tag)),
                std.mem.asBytes(&key.cast),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.constant_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Cast, rhs_data);
            return std.meta.eql(lhs_key.cast, rhs_extra);
        }
    };
    const data = Key{ .tag = tag, .cast = .{ .val = val, .type = ty } };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addConstantExtraAssumeCapacity(data.cast),
        });
    }
    return @enumFromInt(gop.index);
}

fn gepConstAssumeCapacity(
    self: *Builder,
    comptime kind: Constant.GetElementPtr.Kind,
    ty: Type,
    base: Constant,
    inrange: ?u16,
    indices: []const Constant,
) Constant {
    const tag: Constant.Tag = switch (kind) {
        .normal => .getelementptr,
        .inbounds => .@"getelementptr inbounds",
    };
    const base_ty = base.typeOf(self);
    const base_is_vector = base_ty.isVector(self);

    const VectorInfo = struct {
        kind: Type.Vector.Kind,
        len: u32,

        fn init(vector_ty: Type, builder: *const Builder) @This() {
            return .{ .kind = vector_ty.vectorKind(builder), .len = vector_ty.vectorLen(builder) };
        }
    };
    var vector_info: ?VectorInfo = if (base_is_vector) VectorInfo.init(base_ty, self) else null;
    for (indices) |index| {
        const index_ty = index.typeOf(self);
        switch (index_ty.tag(self)) {
            .integer => {},
            .vector, .scalable_vector => {
                const index_info = VectorInfo.init(index_ty, self);
                if (vector_info) |info|
                    assert(std.meta.eql(info, index_info))
                else
                    vector_info = index_info;
            },
            else => unreachable,
        }
    }
    if (!base_is_vector) if (vector_info) |info| switch (info.kind) {
        inline else => |vector_kind| _ = self.vectorTypeAssumeCapacity(vector_kind, info.len, base_ty),
    };

    const Key = struct {
        type: Type,
        base: Constant,
        inrange: Constant.GetElementPtr.InRangeIndex,
        indices: []const Constant,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = std.hash.Wyhash.init(comptime std.hash.int(@intFromEnum(tag)));
            hasher.update(std.mem.asBytes(&key.type));
            hasher.update(std.mem.asBytes(&key.base));
            hasher.update(std.mem.asBytes(&key.inrange));
            hasher.update(std.mem.sliceAsBytes(key.indices));
            return @truncate(hasher.final());
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (ctx.builder.constant_items.items(.tag)[rhs_index] != tag) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            var rhs_extra = ctx.builder.constantExtraDataTrail(Constant.GetElementPtr, rhs_data);
            const rhs_indices =
                rhs_extra.trail.next(rhs_extra.data.info.indices_len, Constant, ctx.builder);
            return lhs_key.type == rhs_extra.data.type and lhs_key.base == rhs_extra.data.base and
                lhs_key.inrange == rhs_extra.data.info.inrange and
                std.mem.eql(Constant, lhs_key.indices, rhs_indices);
        }
    };
    const data = Key{
        .type = ty,
        .base = base,
        .inrange = if (inrange) |index| @enumFromInt(index) else .none,
        .indices = indices,
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addConstantExtraAssumeCapacity(Constant.GetElementPtr{
                .type = ty,
                .base = base,
                .info = .{ .indices_len = @intCast(indices.len), .inrange = data.inrange },
            }),
        });
        self.constant_extra.appendSliceAssumeCapacity(@ptrCast(indices));
    }
    return @enumFromInt(gop.index);
}

fn binConstAssumeCapacity(
    self: *Builder,
    tag: Constant.Tag,
    lhs: Constant,
    rhs: Constant,
) Constant {
    switch (tag) {
        .add,
        .@"add nsw",
        .@"add nuw",
        .sub,
        .@"sub nsw",
        .@"sub nuw",
        .shl,
        .xor,
        => {},
        else => unreachable,
    }
    const Key = struct { tag: Constant.Tag, extra: Constant.Binary };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            return @truncate(std.hash.Wyhash.hash(
                std.hash.int(@intFromEnum(key.tag)),
                std.mem.asBytes(&key.extra),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.constant_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Binary, rhs_data);
            return std.meta.eql(lhs_key.extra, rhs_extra);
        }
    };
    const data = Key{ .tag = tag, .extra = .{ .lhs = lhs, .rhs = rhs } };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addConstantExtraAssumeCapacity(data.extra),
        });
    }
    return @enumFromInt(gop.index);
}

fn asmConstAssumeCapacity(
    self: *Builder,
    ty: Type,
    info: Constant.Assembly.Info,
    assembly: String,
    constraints: String,
) Constant {
    assert(ty.functionKind(self) == .normal);

    const Key = struct { tag: Constant.Tag, extra: Constant.Assembly };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            return @truncate(std.hash.Wyhash.hash(
                std.hash.int(@intFromEnum(key.tag)),
                std.mem.asBytes(&key.extra),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.constant_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.constantExtraData(Constant.Assembly, rhs_data);
            return std.meta.eql(lhs_key.extra, rhs_extra);
        }
    };

    const data = Key{
        .tag = @enumFromInt(@intFromEnum(Constant.Tag.@"asm") + @as(u4, @bitCast(info))),
        .extra = .{ .type = ty, .assembly = assembly, .constraints = constraints },
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(data, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = data.tag,
            .data = self.addConstantExtraAssumeCapacity(data.extra),
        });
    }
    return @enumFromInt(gop.index);
}

fn ensureUnusedConstantCapacity(
    self: *Builder,
    count: usize,
    comptime Extra: type,
    trail_len: usize,
) Allocator.Error!void {
    try self.constant_map.ensureUnusedCapacity(self.gpa, count);
    try self.constant_items.ensureUnusedCapacity(self.gpa, count);
    try self.constant_extra.ensureUnusedCapacity(
        self.gpa,
        count * (@typeInfo(Extra).@"struct".fields.len + trail_len),
    );
}

fn getOrPutConstantNoExtraAssumeCapacity(
    self: *Builder,
    item: Constant.Item,
) struct { new: bool, constant: Constant } {
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Constant.Item) u32 {
            return @truncate(std.hash.Wyhash.hash(
                std.hash.int(@intFromEnum(key.tag)),
                std.mem.asBytes(&key.data),
            ));
        }
        pub fn eql(ctx: @This(), lhs_key: Constant.Item, _: void, rhs_index: usize) bool {
            return std.meta.eql(lhs_key, ctx.builder.constant_items.get(rhs_index));
        }
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(item, Adapter{ .builder = self });
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(item);
    }
    return .{ .new = !gop.found_existing, .constant = @enumFromInt(gop.index) };
}

fn getOrPutConstantAggregateAssumeCapacity(
    self: *Builder,
    tag: Constant.Tag,
    ty: Type,
    vals: []const Constant,
) struct { new: bool, constant: Constant } {
    switch (tag) {
        .structure, .packed_structure, .array, .vector => {},
        else => unreachable,
    }
    const Key = struct { tag: Constant.Tag, type: Type, vals: []const Constant };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = std.hash.Wyhash.init(std.hash.int(@intFromEnum(key.tag)));
            hasher.update(std.mem.asBytes(&key.type));
            hasher.update(std.mem.sliceAsBytes(key.vals));
            return @truncate(hasher.final());
        }
        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.constant_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.constant_items.items(.data)[rhs_index];
            var rhs_extra = ctx.builder.constantExtraDataTrail(Constant.Aggregate, rhs_data);
            if (lhs_key.type != rhs_extra.data.type) return false;
            const rhs_vals = rhs_extra.trail.next(@intCast(lhs_key.vals.len), Constant, ctx.builder);
            return std.mem.eql(Constant, lhs_key.vals, rhs_vals);
        }
    };
    const gop = self.constant_map.getOrPutAssumeCapacityAdapted(
        Key{ .tag = tag, .type = ty, .vals = vals },
        Adapter{ .builder = self },
    );
    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.constant_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addConstantExtraAssumeCapacity(Constant.Aggregate{ .type = ty }),
        });
        self.constant_extra.appendSliceAssumeCapacity(@ptrCast(vals));
    }
    return .{ .new = !gop.found_existing, .constant = @enumFromInt(gop.index) };
}

fn addConstantExtraAssumeCapacity(self: *Builder, extra: anytype) Constant.Item.ExtraIndex {
    const result: Constant.Item.ExtraIndex = @intCast(self.constant_extra.items.len);
    inline for (@typeInfo(@TypeOf(extra)).@"struct".fields) |field| {
        const value = @field(extra, field.name);
        self.constant_extra.appendAssumeCapacity(switch (field.type) {
            u32 => value,
            String, Type, Constant, Function.Index, Function.Block.Index => @intFromEnum(value),
            Constant.GetElementPtr.Info => @bitCast(value),
            else => @compileError("bad field type: " ++ @typeName(field.type)),
        });
    }
    return result;
}

const ConstantExtraDataTrail = struct {
    index: Constant.Item.ExtraIndex,

    fn nextMut(self: *ConstantExtraDataTrail, len: u32, comptime Item: type, builder: *Builder) []Item {
        const items: []Item = @ptrCast(builder.constant_extra.items[self.index..][0..len]);
        self.index += @intCast(len);
        return items;
    }

    fn next(
        self: *ConstantExtraDataTrail,
        len: u32,
        comptime Item: type,
        builder: *const Builder,
    ) []const Item {
        const items: []const Item = @ptrCast(builder.constant_extra.items[self.index..][0..len]);
        self.index += @intCast(len);
        return items;
    }
};

fn constantExtraDataTrail(
    self: *const Builder,
    comptime T: type,
    index: Constant.Item.ExtraIndex,
) struct { data: T, trail: ConstantExtraDataTrail } {
    var result: T = undefined;
    const fields = @typeInfo(T).@"struct".fields;
    inline for (fields, self.constant_extra.items[index..][0..fields.len]) |field, value|
        @field(result, field.name) = switch (field.type) {
            u32 => value,
            String, Type, Constant, Function.Index, Function.Block.Index => @enumFromInt(value),
            Constant.GetElementPtr.Info => @bitCast(value),
            else => @compileError("bad field type: " ++ @typeName(field.type)),
        };
    return .{
        .data = result,
        .trail = .{ .index = index + @as(Constant.Item.ExtraIndex, @intCast(fields.len)) },
    };
}

fn constantExtraData(self: *const Builder, comptime T: type, index: Constant.Item.ExtraIndex) T {
    return self.constantExtraDataTrail(T, index).data;
}

fn ensureUnusedMetadataCapacity(
    self: *Builder,
    count: usize,
    comptime Extra: type,
    trail_len: usize,
) Allocator.Error!void {
    try self.metadata_map.ensureUnusedCapacity(self.gpa, count);
    try self.metadata_items.ensureUnusedCapacity(self.gpa, count);
    try self.metadata_extra.ensureUnusedCapacity(
        self.gpa,
        count * (@typeInfo(Extra).@"struct".fields.len + trail_len),
    );
}

fn addMetadataExtraAssumeCapacity(self: *Builder, extra: anytype) Metadata.Item.ExtraIndex {
    const result: Metadata.Item.ExtraIndex = @intCast(self.metadata_extra.items.len);
    inline for (@typeInfo(@TypeOf(extra)).@"struct".fields) |field| {
        const value = @field(extra, field.name);
        self.metadata_extra.appendAssumeCapacity(switch (field.type) {
            u32 => value,
            Metadata.String, Metadata.String.Optional, Variable.Index, Value => @intFromEnum(value),
            Metadata, Metadata.Optional, Metadata.DIFlags => @bitCast(value),
            else => @compileError("bad field type: " ++ @typeName(field.type)),
        });
    }
    return result;
}

const MetadataExtraDataTrail = struct {
    index: Metadata.Item.ExtraIndex,

    fn nextMut(self: *MetadataExtraDataTrail, len: u32, comptime Item: type, builder: *Builder) []Item {
        const items: []Item = @ptrCast(builder.metadata_extra.items[self.index..][0..len]);
        self.index += @intCast(len);
        return items;
    }

    fn next(
        self: *MetadataExtraDataTrail,
        len: u32,
        comptime Item: type,
        builder: *const Builder,
    ) []const Item {
        const items: []const Item = @ptrCast(builder.metadata_extra.items[self.index..][0..len]);
        self.index += @intCast(len);
        return items;
    }
};

fn metadataExtraDataTrail(
    self: *const Builder,
    comptime T: type,
    index: Metadata.Item.ExtraIndex,
) struct { data: T, trail: MetadataExtraDataTrail } {
    var result: T = undefined;
    const fields = @typeInfo(T).@"struct".fields;
    inline for (fields, self.metadata_extra.items[index..][0..fields.len]) |field, value|
        @field(result, field.name) = switch (field.type) {
            u32 => value,
            Metadata.String, Metadata.String.Optional, Variable.Index, Value => @enumFromInt(value),
            Metadata, Metadata.Optional, Metadata.DIFlags => @bitCast(value),
            else => @compileError("bad field type: " ++ @typeName(field.type)),
        };
    return .{
        .data = result,
        .trail = .{ .index = index + @as(Metadata.Item.ExtraIndex, @intCast(fields.len)) },
    };
}

fn metadataExtraData(self: *const Builder, comptime T: type, index: Metadata.Item.ExtraIndex) T {
    return self.metadataExtraDataTrail(T, index).data;
}

pub fn metadataString(self: *Builder, bytes: []const u8) Allocator.Error!Metadata.String {
    assert(bytes.len > 0);
    try self.metadata_string_bytes.ensureUnusedCapacity(self.gpa, bytes.len);
    try self.metadata_string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.metadata_string_map.ensureUnusedCapacity(self.gpa, 1);

    const gop = self.metadata_string_map.getOrPutAssumeCapacityAdapted(
        bytes,
        Metadata.String.Adapter{ .builder = self },
    );
    if (!gop.found_existing) {
        self.metadata_string_bytes.appendSliceAssumeCapacity(bytes);
        self.metadata_string_indices.appendAssumeCapacity(
            @intCast(self.metadata_string_bytes.items.len),
        );
    }
    return @enumFromInt(gop.index);
}

pub fn metadataStringFromStrtabString(
    self: *Builder,
    str: StrtabString,
) Allocator.Error!Metadata.String {
    return try self.metadataString(str.slice(self).?);
}

pub fn metadataStringFmt(
    self: *Builder,
    comptime fmt_str: []const u8,
    fmt_args: anytype,
) Allocator.Error!Metadata.String {
    try self.metadata_string_map.ensureUnusedCapacity(self.gpa, 1);
    try self.metadata_string_bytes.ensureUnusedCapacity(
        self.gpa,
        @intCast(std.fmt.count(fmt_str, fmt_args)),
    );
    try self.metadata_string_indices.ensureUnusedCapacity(self.gpa, 1);
    return self.metadataStringFmtAssumeCapacity(fmt_str, fmt_args);
}

pub fn metadataStringFmtAssumeCapacity(
    self: *Builder,
    comptime fmt_str: []const u8,
    fmt_args: anytype,
) Metadata.String {
    self.metadata_string_bytes.printAssumeCapacity(fmt_str, fmt_args);
    return self.trailingMetadataStringAssumeCapacity();
}

pub fn trailingMetadataString(self: *Builder) Allocator.Error!Metadata.String {
    try self.metadata_string_indices.ensureUnusedCapacity(self.gpa, 1);
    try self.metadata_string_map.ensureUnusedCapacity(self.gpa, 1);
    return self.trailingMetadataStringAssumeCapacity();
}

pub fn trailingMetadataStringAssumeCapacity(self: *Builder) Metadata.String {
    const start = self.metadata_string_indices.getLast();
    const bytes: []const u8 = self.metadata_string_bytes.items[start..];
    assert(bytes.len > 0);
    const gop = self.metadata_string_map.getOrPutAssumeCapacityAdapted(bytes, Metadata.String.Adapter{ .builder = self });
    if (gop.found_existing) {
        self.metadata_string_bytes.shrinkRetainingCapacity(start);
    } else {
        self.metadata_string_indices.appendAssumeCapacity(@intCast(self.metadata_string_bytes.items.len));
    }
    return @enumFromInt(gop.index);
}

pub fn addNamedMetadata(self: *Builder, name: String, operands: []const Metadata) Allocator.Error!void {
    try self.metadata_extra.ensureUnusedCapacity(self.gpa, operands.len);
    try self.metadata_named.ensureUnusedCapacity(self.gpa, 1);
    self.addNamedMetadataAssumeCapacity(name, operands);
}

pub fn debugFile(
    self: *Builder,
    filename: ?Metadata.String,
    directory: ?Metadata.String,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.File, 0);
    return self.debugFileAssumeCapacity(filename, directory);
}

pub fn debugCompileUnit(
    self: *Builder,
    file: ?Metadata,
    producer: ?Metadata.String,
    enums: ?Metadata,
    globals: ?Metadata,
    options: Metadata.CompileUnit.Options,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.CompileUnit, 0);
    return self.debugCompileUnitAssumeCapacity(file, producer, enums, globals, options);
}

pub fn debugSubprogram(
    self: *Builder,
    file: ?Metadata,
    name: ?Metadata.String,
    linkage_name: ?Metadata.String,
    line: u32,
    scope_line: u32,
    ty: ?Metadata,
    options: Metadata.Subprogram.Options,
    compile_unit: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.Subprogram, 0);
    return self.debugSubprogramAssumeCapacity(
        file,
        name,
        linkage_name,
        line,
        scope_line,
        ty,
        options,
        compile_unit,
    );
}

pub fn debugLexicalBlock(
    self: *Builder,
    scope: ?Metadata,
    file: ?Metadata,
    line: u32,
    column: u32,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.LexicalBlock, 0);
    return self.debugLexicalBlockAssumeCapacity(scope, file, line, column);
}

pub fn debugLocation(
    self: *Builder,
    line: u32,
    column: u32,
    scope: Metadata,
    inlined_at: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.Location, 0);
    return self.debugLocationAssumeCapacity(line, column, scope, inlined_at);
}

pub fn debugBoolType(
    self: *Builder,
    name: ?Metadata.String,
    size_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.BasicType, 0);
    return self.debugBoolTypeAssumeCapacity(name, size_in_bits);
}

pub fn debugUnsignedType(
    self: *Builder,
    name: ?Metadata.String,
    size_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.BasicType, 0);
    return self.debugUnsignedTypeAssumeCapacity(name, size_in_bits);
}

pub fn debugSignedType(
    self: *Builder,
    name: ?Metadata.String,
    size_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.BasicType, 0);
    return self.debugSignedTypeAssumeCapacity(name, size_in_bits);
}

pub fn debugFloatType(
    self: *Builder,
    name: ?Metadata.String,
    size_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.BasicType, 0);
    return self.debugFloatTypeAssumeCapacity(name, size_in_bits);
}

pub fn debugForwardReference(self: *Builder) Allocator.Error!Metadata {
    try self.metadata_forward_references.ensureUnusedCapacity(self.gpa, 1);
    return self.debugForwardReferenceAssumeCapacity();
}

pub fn debugStructType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.CompositeType, 0);
    return self.debugStructTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

pub fn debugUnionType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.CompositeType, 0);
    return self.debugUnionTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

pub fn debugEnumerationType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.CompositeType, 0);
    return self.debugEnumerationTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

pub fn debugArrayType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.CompositeType, 0);
    return self.debugArrayTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

pub fn debugVectorType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.CompositeType, 0);
    return self.debugVectorTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

pub fn debugPointerType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    offset_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.DerivedType, 0);
    return self.debugPointerTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        offset_in_bits,
    );
}

pub fn debugMemberType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    offset_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.DerivedType, 0);
    return self.debugMemberTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        offset_in_bits,
    );
}

pub fn debugTypedefType(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    offset_in_bits: u64,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.DerivedType, 0);
    return self.debugTypedefTypeAssumeCapacity(
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        offset_in_bits,
    );
}

pub fn debugSubroutineType(self: *Builder, types_tuple: ?Metadata) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.SubroutineType, 0);
    return self.debugSubroutineTypeAssumeCapacity(types_tuple);
}

pub fn debugEnumerator(
    self: *Builder,
    name: ?Metadata.String,
    unsigned: bool,
    bit_width: u32,
    value: std.math.big.int.Const,
) Allocator.Error!Metadata {
    assert(!(unsigned and !value.positive));
    try self.ensureUnusedMetadataCapacity(1, Metadata.Enumerator, 0);
    try self.metadata_limbs.ensureUnusedCapacity(self.gpa, value.limbs.len);
    return self.debugEnumeratorAssumeCapacity(name, unsigned, bit_width, value);
}

pub fn debugSubrange(
    self: *Builder,
    lower_bound: ?Metadata,
    count: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.Subrange, 0);
    return self.debugSubrangeAssumeCapacity(lower_bound, count);
}

pub fn debugExpression(self: *Builder, elements: []const u32) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.Expression, elements.len);
    return self.debugExpressionAssumeCapacity(elements);
}

pub fn metadataTuple(self: *Builder, elements: []const Metadata) Allocator.Error!Metadata {
    return self.metadataTupleOptionals(@ptrCast(elements));
}

pub fn metadataTupleOptionals(
    self: *Builder,
    elements: []const Metadata.Optional,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.Tuple, elements.len);
    return self.metadataTupleOptionalsAssumeCapacity(elements);
}

pub fn debugLocalVar(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    ty: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.LocalVar, 0);
    return self.debugLocalVarAssumeCapacity(name, file, scope, line, ty);
}

pub fn debugParameter(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    ty: ?Metadata,
    arg_no: u32,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.Parameter, 0);
    return self.debugParameterAssumeCapacity(name, file, scope, line, ty, arg_no);
}

pub fn debugGlobalVar(
    self: *Builder,
    name: ?Metadata.String,
    linkage_name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    ty: ?Metadata,
    variable: Variable.Index,
    options: Metadata.GlobalVar.Options,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.GlobalVar, 0);
    return self.debugGlobalVarAssumeCapacity(
        name,
        linkage_name,
        file,
        scope,
        line,
        ty,
        variable,
        options,
    );
}

pub fn debugGlobalVarExpression(
    self: *Builder,
    variable: ?Metadata,
    expression: ?Metadata,
) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, Metadata.GlobalVarExpression, 0);
    return self.debugGlobalVarExpressionAssumeCapacity(variable, expression);
}

pub fn metadataConstant(self: *Builder, value: Constant) Allocator.Error!Metadata {
    try self.ensureUnusedMetadataCapacity(1, NoExtra, 0);
    return self.metadataConstantAssumeCapacity(value);
}

/// Resolves the given forward reference to the given value (which is not itself a forward
/// reference). If the forward reference is already resolved, its target is replaced.
pub fn resolveDebugForwardReference(self: *Builder, fwd_ref: Metadata, value: Metadata) void {
    assert(fwd_ref.kind == .forward);
    assert(value.kind != .forward);
    self.metadata_forward_references.items[fwd_ref.index] = value.toOptional();
}

fn metadataSimpleAssumeCapacity(self: *Builder, tag: Metadata.Tag, value: anytype) Metadata {
    const Key = struct {
        tag: Metadata.Tag,
        value: @TypeOf(value),
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = std.hash.Wyhash.init(std.hash.int(@intFromEnum(key.tag)));
            inline for (std.meta.fields(@TypeOf(value))) |field| {
                hasher.update(std.mem.asBytes(&@field(key.value, field.name)));
            }
            return @truncate(hasher.final());
        }

        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.metadata_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.metadata_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.metadataExtraData(@TypeOf(value), rhs_data);
            return std.meta.eql(lhs_key.value, rhs_extra);
        }
    };

    const gop = self.metadata_map.getOrPutAssumeCapacityAdapted(
        Key{ .tag = tag, .value = value },
        Adapter{ .builder = self },
    );

    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.metadata_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addMetadataExtraAssumeCapacity(value),
        });
    }
    return .{ .index = @intCast(gop.index), .kind = .node };
}

fn metadataDistinctAssumeCapacity(self: *Builder, tag: Metadata.Tag, value: anytype) Metadata {
    const index = self.metadata_items.len;
    _ = self.metadata_map.entries.addOneAssumeCapacity();
    self.metadata_items.appendAssumeCapacity(.{
        .tag = tag,
        .data = self.addMetadataExtraAssumeCapacity(value),
    });
    return .{ .index = @intCast(index), .kind = .node };
}

fn addNamedMetadataAssumeCapacity(self: *Builder, name: String, operands: []const Metadata) void {
    assert(name != .none);
    const extra_index: u32 = @intCast(self.metadata_extra.items.len);
    self.metadata_extra.appendSliceAssumeCapacity(@ptrCast(operands));

    const gop = self.metadata_named.getOrPutAssumeCapacity(name);
    gop.value_ptr.* = .{
        .index = extra_index,
        .len = @intCast(operands.len),
    };
}

fn debugFileAssumeCapacity(
    self: *Builder,
    filename: ?Metadata.String,
    directory: ?Metadata.String,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.file, Metadata.File{
        .filename = .wrap(filename),
        .directory = .wrap(directory),
    });
}

pub fn debugCompileUnitAssumeCapacity(
    self: *Builder,
    file: ?Metadata,
    producer: ?Metadata.String,
    enums: ?Metadata,
    globals: ?Metadata,
    options: Metadata.CompileUnit.Options,
) Metadata {
    assert(!self.strip);
    return self.metadataDistinctAssumeCapacity(
        if (options.optimized) .@"compile_unit optimized" else .compile_unit,
        Metadata.CompileUnit{
            .file = .wrap(file),
            .producer = .wrap(producer),
            .enums = .wrap(enums),
            .globals = .wrap(globals),
        },
    );
}

fn debugSubprogramAssumeCapacity(
    self: *Builder,
    file: ?Metadata,
    name: ?Metadata.String,
    linkage_name: ?Metadata.String,
    line: u32,
    scope_line: u32,
    ty: ?Metadata,
    options: Metadata.Subprogram.Options,
    compile_unit: ?Metadata,
) Metadata {
    assert(!self.strip);
    const tag: Metadata.Tag = @enumFromInt(@intFromEnum(Metadata.Tag.subprogram) +
        @as(u3, @truncate(@as(u32, @bitCast(options.sp_flags)) >> 2)));
    return self.metadataDistinctAssumeCapacity(tag, Metadata.Subprogram{
        .file = .wrap(file),
        .name = .wrap(name),
        .linkage_name = .wrap(linkage_name),
        .line = line,
        .scope_line = scope_line,
        .ty = .wrap(ty),
        .di_flags = options.di_flags,
        .compile_unit = .wrap(compile_unit),
    });
}

fn debugLexicalBlockAssumeCapacity(
    self: *Builder,
    scope: ?Metadata,
    file: ?Metadata,
    line: u32,
    column: u32,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.lexical_block, Metadata.LexicalBlock{
        .scope = .wrap(scope),
        .file = .wrap(file),
        .line = line,
        .column = column,
    });
}

fn debugLocationAssumeCapacity(
    self: *Builder,
    line: u32,
    column: u32,
    scope: Metadata,
    inlined_at: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.location, Metadata.Location{
        .line = line,
        .column = column,
        .scope = scope,
        .inlined_at = .wrap(inlined_at),
    });
}

fn debugBoolTypeAssumeCapacity(self: *Builder, name: ?Metadata.String, size_in_bits: u64) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.basic_bool_type, Metadata.BasicType{
        .name = .wrap(name),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
    });
}

fn debugUnsignedTypeAssumeCapacity(self: *Builder, name: ?Metadata.String, size_in_bits: u64) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.basic_unsigned_type, Metadata.BasicType{
        .name = .wrap(name),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
    });
}

fn debugSignedTypeAssumeCapacity(self: *Builder, name: ?Metadata.String, size_in_bits: u64) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.basic_signed_type, Metadata.BasicType{
        .name = .wrap(name),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
    });
}

fn debugFloatTypeAssumeCapacity(self: *Builder, name: ?Metadata.String, size_in_bits: u64) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.basic_float_type, Metadata.BasicType{
        .name = .wrap(name),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
    });
}

fn debugForwardReferenceAssumeCapacity(self: *Builder) Metadata {
    assert(!self.strip);
    const index = self.metadata_forward_references.items.len;
    self.metadata_forward_references.appendAssumeCapacity(.none);
    return .{ .index = @intCast(index), .kind = .forward };
}

fn debugStructTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.debugCompositeTypeAssumeCapacity(
        .composite_struct_type,
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

fn debugUnionTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.debugCompositeTypeAssumeCapacity(
        .composite_union_type,
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

fn debugEnumerationTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.debugCompositeTypeAssumeCapacity(
        .composite_enumeration_type,
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

fn debugArrayTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.debugCompositeTypeAssumeCapacity(
        .composite_array_type,
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

fn debugVectorTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.debugCompositeTypeAssumeCapacity(
        .composite_vector_type,
        name,
        file,
        scope,
        line,
        underlying_type,
        size_in_bits,
        align_in_bits,
        fields_tuple,
    );
}

fn debugCompositeTypeAssumeCapacity(
    self: *Builder,
    tag: Metadata.Tag,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    fields_tuple: ?Metadata,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(tag, Metadata.CompositeType{
        .name = .wrap(name),
        .file = .wrap(file),
        .scope = .wrap(scope),
        .line = line,
        .underlying_type = .wrap(underlying_type),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
        .align_in_bits_lo = @truncate(align_in_bits),
        .align_in_bits_hi = @truncate(align_in_bits >> 32),
        .fields_tuple = .wrap(fields_tuple),
    });
}

fn debugPointerTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    offset_in_bits: u64,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.derived_pointer_type, Metadata.DerivedType{
        .name = .wrap(name),
        .file = .wrap(file),
        .scope = .wrap(scope),
        .line = line,
        .underlying_type = .wrap(underlying_type),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
        .align_in_bits_lo = @truncate(align_in_bits),
        .align_in_bits_hi = @truncate(align_in_bits >> 32),
        .offset_in_bits_lo = @truncate(offset_in_bits),
        .offset_in_bits_hi = @truncate(offset_in_bits >> 32),
    });
}

fn debugMemberTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    offset_in_bits: u64,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.derived_member_type, Metadata.DerivedType{
        .name = .wrap(name),
        .file = .wrap(file),
        .scope = .wrap(scope),
        .line = line,
        .underlying_type = .wrap(underlying_type),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
        .align_in_bits_lo = @truncate(align_in_bits),
        .align_in_bits_hi = @truncate(align_in_bits >> 32),
        .offset_in_bits_lo = @truncate(offset_in_bits),
        .offset_in_bits_hi = @truncate(offset_in_bits >> 32),
    });
}

fn debugTypedefTypeAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    file: ?Metadata,
    scope: ?Metadata,
    line: u32,
    underlying_type: ?Metadata,
    size_in_bits: u64,
    align_in_bits: u64,
    offset_in_bits: u64,
) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.derived_typedef_type, Metadata.DerivedType{
        .name = .wrap(name),
        .file = .wrap(file),
        .scope = .wrap(scope),
        .line = line,
        .underlying_type = .wrap(underlying_type),
        .size_in_bits_lo = @truncate(size_in_bits),
        .size_in_bits_hi = @truncate(size_in_bits >> 32),
        .align_in_bits_lo = @truncate(align_in_bits),
        .align_in_bits_hi = @truncate(align_in_bits >> 32),
        .offset_in_bits_lo = @truncate(offset_in_bits),
        .offset_in_bits_hi = @truncate(offset_in_bits >> 32),
    });
}

fn debugSubroutineTypeAssumeCapacity(self: *Builder, types_tuple: ?Metadata) Metadata {
    assert(!self.strip);
    return self.metadataSimpleAssumeCapacity(.subroutine_type, Metadata.SubroutineType{
        .types_tuple = .wrap(types_tuple),
    });
}

fn debugEnumeratorAssumeCapacity(
    self: *Builder,
    name: ?Metadata.String,
    unsigned: bool,
    bit_width: u32,
    value: std.math.big.int.Const,
) Metadata {
    assert(!self.strip);
    const Key = struct {
        tag: Metadata.Tag,
        name: Metadata.String.Optional,
        bit_width: u32,
        value: std.math.big.int.Const,
    };
    const Adapter = struct {
        builder: *const Builder,
        pub fn hash(_: @This(), key: Key) u32 {
            var hasher = std.hash.Wyhash.init(std.hash.int(@intFromEnum(key.tag)));
            hasher.update(std.mem.asBytes(&key.name));
            hasher.update(std.mem.asBytes(&key.bit_width));
            hasher.update(std.mem.sliceAsBytes(key.value.limbs));
            return @truncate(hasher.final());
        }

        pub fn eql(ctx: @This(), lhs_key: Key, _: void, rhs_index: usize) bool {
            if (lhs_key.tag != ctx.builder.metadata_items.items(.tag)[rhs_index]) return false;
            const rhs_data = ctx.builder.metadata_items.items(.data)[rhs_index];
            const rhs_extra = ctx.builder.metadataExtraData(Metadata.Enumerator, rhs_data);
            const limbs = ctx.builder.metadata_limbs
                .items[rhs_extra.limbs_index..][0..rhs_extra.limbs_len];
            const rhs_value = std.math.big.int.Const{
                .limbs = limbs,
                .positive = lhs_key.value.positive,
            };
            return lhs_key.name == rhs_extra.name and
                lhs_key.bit_width == rhs_extra.bit_width and
                lhs_key.value.eql(rhs_value);
        }
    };

    const tag: Metadata.Tag = if (unsigned)
        .enumerator_unsigned
    else if (value.positive)
        .enumerator_signed_positive
    else
        .enumerator_signed_negative;

    assert(!(tag == .enumerator_unsigned and !value.positive));

    const gop = self.metadata_map.getOrPutAssumeCapacityAdapted(Key{
        .tag = tag,
        .name = .wrap(name),
        .bit_width = bit_width,
        .value = value,
    }, Adapter{ .builder = self });

    if (!gop.found_existing) {
        gop.key_ptr.* = {};
        gop.value_ptr.* = {};
        self.metadata_items.appendAssumeCapacity(.{
            .tag = tag,
            .data = self.addMetadataExtraAssumeCapacity(Metadata.Enumerator{
                .name = .wrap(name),
                .bit_width = bit_width,
                .limbs_index = @intCast(self.metadata_limbs.items.len),
                .limbs_len = @intCast(value.limbs.len),
            }),
        });
        self.metadata_limbs.appendSliceAssumeCapacity(value.limbs);
    }
    return .{ .index = @intCast(gop.index), .kind = .node };
}

fn debugSubrangeAssumeCapacity(self: *Bu
```
